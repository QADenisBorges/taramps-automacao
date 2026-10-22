using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using BgsControle.Nucleo.Aparelho;
using BgsControle.Nucleo.Escrita;
using BgsControle.Nucleo.Gerador;
using BgsControle.Nucleo.Leitura;
using BgsControle.Nucleo.Memorias;
using BgsControle.Nucleo.Porta;
using BgsControle.Nucleo.Registro;
using BgsControle.Nucleo.Seguranca;
using BgsControle.Nucleo.Uso;
using BgsControle.Simulador;

namespace BgsControle.Nucleo.Testes;

/// <summary>Uma rede local falsa: aceita pelo critério dado (o padrão: as faixas privadas, sem olhar a sub-rede).</summary>
internal sealed class RedeFalsa : IRedeLocal
{
    public Func<IPAddress, bool> Aceitar { get; set; } = ip => RedeLocal.Privado(ip) && !IPAddress.IsLoopback(RedeLocal.Normalizar(ip));

    public bool Aceita(IPAddress remoto) => Aceitar(remoto);

    public string? Endereco { get; set; } = "192.168.0.5";
}

/// <summary>Um texto guardado cuja leitura só termina em <see cref="Soltar"/>.</summary>
internal sealed class TextoSegurado(string? inicial) : IRepositorioTexto
{
    private readonly TaskCompletionSource _soltar = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private string? _texto = inicial;

    public string? Texto => Volatile.Read(ref _texto);

    public void Soltar() => _soltar.TrySetResult();

    public async Task<string?> LerAsync()
    {
        await _soltar.Task;
        return Texto;
    }

    public Task GravarAsync(string conteudo)
    {
        Volatile.Write(ref _texto, conteudo);
        return Task.CompletedTask;
    }
}

/// <summary>
/// A história 9b no núcleo, contra o processador simulado: a porta de comandos da IA, com as cinco
/// travas (chave, pareamento, rede local, tudo visível, confirmação na tela). Uma linha da matriz de
/// <c>9b-porta-de-comandos-da-ia.md</c> por teste, mais o HTTP de ponta a ponta no loopback, a
/// origem que não vaza e a senha que nunca aparece numa resposta.
/// </summary>
public sealed class PortaTestes : IDisposable
{
    private static readonly TemposSessao Tempos = new(
        Scan: TimeSpan.FromSeconds(1),
        TentativaConexao: TimeSpan.FromSeconds(1),
        PausaEntreTentativas: TimeSpan.FromMilliseconds(1),
        RespostaSenha: TimeSpan.FromSeconds(2),
        PrazoSenha: TimeSpan.FromSeconds(60));

    private static readonly TemposLeitura LeituraRapida = new(TimeSpan.Zero, TimeSpan.FromMilliseconds(100));

    private static readonly TemposEscrita Escrita = new(
        Intervalo: TimeSpan.FromMilliseconds(30),
        Arrasto: TimeSpan.FromMilliseconds(300),
        PrazoEco: TimeSpan.FromMilliseconds(300),
        PrazoReleitura: TimeSpan.FromMilliseconds(400));

    private static readonly TemposGerador TemposDoGerador = new(TimeSpan.FromSeconds(30), TimeSpan.FromMilliseconds(100));

    private static readonly Regex Senha = new(@"^\*000\*[0-9A-Z]{4}\*$");

    /// <summary>O notebook na rede da casa.</summary>
    private static readonly IPAddress Casa = IPAddress.Parse("192.168.0.10");

    private readonly RelogioFalso _relogio = new();
    private readonly ProcessadorSimulado _simulador;
    private readonly RepositorioMemoria _repositorioChaves = new();
    private readonly TextoMemoria _arquivoPorta = new();
    private readonly CofreFalso _cofre = new() { Senha = "AB12" };
    private readonly Chaves _chaves;
    private readonly LogBle _log = new();
    private readonly Historico _historico;
    private readonly UsoCompartilhado _uso;
    private readonly Sessao _sessao;
    private readonly LeituraAutomatica _leitura;
    private readonly EstadoAparelho _estado;
    private readonly Escritor _escritor;
    private readonly CopiaAntes _copia;
    private readonly GerenteMemorias _memorias;
    private readonly GeradorSinal _gerador;
    private readonly MonitorAparelho _monitor;
    private readonly ComandosAparelho _comandos;
    private readonly RedeFalsa _rede = new();
    private readonly Pareamento _pareamento;
    private readonly PedidosConfirmacao _confirmacoes;
    private readonly PortaComandos _porta;
    private readonly List<string> _respostas = [];

    public PortaTestes()
    {
        _simulador = new ProcessadorSimulado(_relogio) { AtrasoSenha = TimeSpan.Zero, AtrasoLeitura = TimeSpan.Zero };
        _chaves = new Chaves(_repositorioChaves);
        _historico = new Historico(new TextoMemoria(), _relogio, _log);
        _uso = new UsoCompartilhado(new TextoMemoria(), _relogio, _log);
        _sessao = new Sessao(_simulador, _cofre, _log, Tempos, _chaves, _historico, _uso);
        var leitor = new LeitorCompleto(_sessao, _log, LeituraRapida);
        _leitura = new LeituraAutomatica(_sessao, leitor);
        _estado = new EstadoAparelho(_sessao, _leitura);
        _escritor = new Escritor(_sessao, _estado, _log, Escrita, _relogio);
        _copia = new CopiaAntes(_sessao, _estado, _escritor, _leitura, new TextoMemoria(), _log, _relogio);
        _memorias = new GerenteMemorias(_sessao, _estado, _leitura, _copia, _log, Escrita, _relogio);
        _gerador = new GeradorSinal(_sessao, _estado, _leitura, _copia, _log, _memorias, Escrita, TemposDoGerador, _relogio);
        _monitor = new MonitorAparelho(_sessao, _leitura, _relogio);
        _comandos = new ComandosAparelho(_sessao, _estado, _leitura, _copia, _cofre, _log, _memorias, _gerador, Escrita, _relogio);
        _pareamento = new Pareamento(_arquivoPorta, _relogio, _log);
        _confirmacoes = new PedidosConfirmacao(_relogio, _log);
        _porta = NovaPorta(_pareamento);
    }

    /// <summary>Nada além do catálogo chega ao simulador; a senha nunca vai ao histórico nem a uma resposta.</summary>
    public void Dispose()
    {
        _porta.Dispose();
        Assert.All(_simulador.Escritas, e =>
            Assert.True(Senha.IsMatch(e) || Catalogo.EhLeitura(e) || Catalogo.EscritaDeAudio(e) is not null
                        || Catalogo.ComandoDeMemoria(e) is not null || Catalogo.ComandoDeGerador(e) is not null
                        || Catalogo.ComandoDeAparelho(e) is not null, e));
        Assert.DoesNotContain("AB12", Historico.ParaJson(_historico.Entradas));
        Assert.All(_respostas, r => Assert.DoesNotContain("AB12", r));
    }

    // ----------------------------------------------------------------- apoio

    private PortaComandos NovaPorta(Pareamento pareamento, int numero = 0, IRedeLocal? rede = null) =>
        new(_sessao, _chaves, _uso, _historico, pareamento, _confirmacoes, rede ?? _rede, _estado, _escritor,
            _memorias, _gerador, _comandos, _monitor, _log, _relogio, numero);

    private async Task Conectar()
    {
        await _chaves.Carregada;
        await _copia.Carregada;
        await _historico.Carregado;
        await _uso.Carregado;
        await _pareamento.Carregado;
        await _sessao.ConectarAsync();
        Assert.Equal(EstadoSessao.Conectado, _sessao.Estado);
        await AndarAte(() => _estado.Pronto && !_estado.Lendo && !_leitura.Lendo && !_copia.EmAndamento
                             && _gerador.Estado == EstadoGerador.Desligado);
    }

    private async Task LigarPorta(PortaComandos? porta = null)
    {
        Assert.True((await _chaves.DefinirAsync(Chave.IaPelaRede, true, OrigemComando.Tela)).Ok);
        await (porta ?? _porta).AtualizarAsync();
        Assert.True((porta ?? _porta).Aberta);
    }

    /// <summary>Conecta, abre a porta e pareia: devolve a chave do cliente.</summary>
    private async Task<string> Pronto()
    {
        await Conectar();
        await LigarPorta();
        return await Parear();
    }

    private async Task<string> Parear()
    {
        var r = await Pedir("POST", "/parear", $$"""{"codigo":"{{_pareamento.Codigo}}","nome":"Claude no notebook"}""");
        Assert.Equal(200, r.Status);
        return Json(r).GetProperty("chave").GetString()!;
    }

    private async Task<RespostaPorta> Pedir(string metodo, string alvo, string? corpo = null, string? chave = null, IPAddress? de = null, PortaComandos? porta = null)
    {
        var r = await (porta ?? _porta).AtenderAsync(PedidoPorta.De(metodo, alvo, de ?? Casa, corpo, chave));
        lock (_respostas)
            _respostas.Add(r.Json);
        return r;
    }

    private async Task<RespostaPorta> PedirRodando(string metodo, string alvo, string? corpo, string chave) =>
        await Rodar(Pedir(metodo, alvo, corpo, chave));

    private static JsonElement Json(RespostaPorta r) => JsonDocument.Parse(r.Json).RootElement;

    private static string? Motivo(RespostaPorta r) => Json(r).TryGetProperty("motivo", out var m) ? m.GetString() : null;

    private async Task AndarAte(Func<bool> condicao, int passos = 3000)
    {
        for (var i = 0; i < passos && !condicao(); i++)
        {
            _relogio.Avancar(TimeSpan.FromMilliseconds(10));
            await Task.Delay(1);
        }
        Assert.True(condicao(), "a condição não ficou verdadeira");
    }

    private static async Task EsperarSemAndar(Func<bool> condicao)
    {
        for (var i = 0; i < 500 && !condicao(); i++)
            await Task.Delay(10);
        Assert.True(condicao(), "a condição não ficou verdadeira");
    }

    private async Task<T> Rodar<T>(Task<T> tarefa)
    {
        await AndarAte(() => tarefa.IsCompleted);
        return await tarefa;
    }

    private EntradaHistorico Ultima() => _historico.Entradas[0];

    // ----------------------------------------------------------------- matriz

    [Fact]
    public async Task Chave_desligada_nada_escuta()
    {
        await _porta.Inicio;
        await _porta.AtualizarAsync();

        Assert.False(_chaves.Ligada(Chave.IaPelaRede));
        Assert.False(_porta.Aberta);
        Assert.Null(_porta.Erro);
        var r = await Pedir("GET", "/estado");
        Assert.Equal(409, r.Status);
        Assert.Equal(PortaComandos.MsgChaveDesligada, Motivo(r));
    }

    [Fact]
    public async Task Parear_com_o_codigo_certo_devolve_a_chave_o_codigo_some_e_o_cliente_fica_guardado()
    {
        await Conectar();
        await LigarPorta();
        var codigo = _pareamento.Codigo;
        Assert.Matches(@"^\d{6}$", codigo);

        var r = await Pedir("POST", "/parear", $$"""{"codigo":"{{codigo}}","nome":"Claude no notebook"}""");

        Assert.Equal(200, r.Status);
        Assert.True(Json(r).GetProperty("ok").GetBoolean());
        var chave = Json(r).GetProperty("chave").GetString()!;
        Assert.Matches(@"^[A-Za-z0-9_-]{43}$", chave); // 32 bytes em base64url
        Assert.Null(_pareamento.Codigo);
        Assert.True(_pareamento.Pareado);
        Assert.Equal("Claude no notebook", _pareamento.Cliente);
        Assert.NotNull(_pareamento.Desde);
        Assert.True(_pareamento.Confere(chave));

        // O celular guarda só o SHA-256 da chave.
        var guardado = _arquivoPorta.Texto!;
        Assert.DoesNotContain(chave, guardado);
        Assert.Contains("\"formato\": 1", guardado);
        Assert.Contains("Claude no notebook", guardado);
        Assert.Contains("sha256", guardado);
    }

    [Fact]
    public async Task Cinco_codigos_errados_dao_401_em_cada_um_e_nasce_outro_codigo()
    {
        await Conectar();
        await LigarPorta();
        var codigo = _pareamento.Codigo!;
        var errado = codigo == "000000" ? "111111" : "000000";

        for (var i = 1; i <= Pareamento.TentativasPorCodigo; i++)
        {
            var r = await Rodar(Pedir("POST", "/parear", $$"""{"codigo":"{{errado}}","nome":"x"}""")); // cada um espera 1 s
            Assert.Equal(401, r.Status);
            if (i < Pareamento.TentativasPorCodigo)
            {
                Assert.Equal(Pareamento.MsgCodigoErrado, Motivo(r));
                Assert.Equal(codigo, _pareamento.Codigo);
            }
            else
            {
                Assert.Equal(Pareamento.MsgCodigoNovo, Motivo(r));
            }
        }

        Assert.NotNull(_pareamento.Codigo);
        Assert.False(_pareamento.Pareado);
        // O código antigo não vale mais (salvo o acaso de nascer igual).
        if (_pareamento.Codigo != codigo)
            Assert.Equal(401, (await Rodar(Pedir("POST", "/parear", $$"""{"codigo":"{{codigo}}","nome":"x"}"""))).Status);
    }

    [Fact]
    public async Task Codigo_errado_espera_1_s_e_as_tentativas_vao_uma_por_vez()
    {
        await Conectar();
        await LigarPorta();
        var errado = _pareamento.Codigo == "000000" ? "111111" : "000000";

        var primeira = Pedir("POST", "/parear", $$"""{"codigo":"{{errado}}","nome":"x"}""");
        var segunda = Pedir("POST", "/parear", $$"""{"codigo":"{{errado}}","nome":"x"}""");
        await Task.Delay(50);
        Assert.False(primeira.IsCompleted);
        _relogio.Avancar(TimeSpan.FromMilliseconds(999));
        await Task.Delay(50);
        Assert.False(primeira.IsCompleted);

        _relogio.Avancar(TimeSpan.FromMilliseconds(1));
        Assert.Equal(401, (await primeira).Status);
        // A segunda só começou a esperar depois da primeira.
        await Task.Delay(50);
        Assert.False(segunda.IsCompleted);
        _relogio.Avancar(Pareamento.EsperaCodigoErrado);
        Assert.Equal(401, (await segunda).Status);
    }

    [Fact]
    public async Task Revogar_durante_o_pareamento_faz_o_pareamento_falhar_e_o_arquivo_fica_sem_cliente()
    {
        await Conectar();
        await LigarPorta();
        var codigo = _pareamento.Codigo;
        // O PARAR IA cai bem na gravação do pareamento (desligar a chave revoga do mesmo jeito).
        _arquivoPorta.AoGravar = _uso.PararIa;

        var r = await Pedir("POST", "/parear", $$"""{"codigo":"{{codigo}}","nome":"Claude"}""");

        Assert.Equal(409, r.Status);
        Assert.Equal(Pareamento.MsgRevogadoNoMeio, Motivo(r));
        Assert.False(_pareamento.Pareado);
        Assert.True(_uso.Parada); // o pareamento que falhou não libera a IA
        await EsperarSemAndar(() => !(_arquivoPorta.Texto ?? "").Contains("sha256"));
    }

    [Fact]
    public async Task Revogar_antes_de_ler_o_arquivo_o_cliente_guardado_nao_volta()
    {
        var arquivo = new TextoSegurado(Pareamento.ParaJson("Claude", System.Security.Cryptography.SHA256.HashData("k"u8), _relogio.GetLocalNow()));
        var p = new Pareamento(arquivo, _relogio, _log);

        var revogar = p.RevogarAsync();
        arquivo.Soltar();
        await p.Carregado;
        Assert.True(await revogar);

        Assert.False(p.Pareado);
        Assert.False(p.Confere("k"));
        Assert.DoesNotContain("sha256", arquivo.Texto);
    }

    [Fact]
    public async Task Chave_desligada_ao_abrir_apaga_o_cliente_guardado()
    {
        var guardado = new TextoMemoria(Pareamento.ParaJson("Claude", System.Security.Cryptography.SHA256.HashData("k"u8), _relogio.GetLocalNow()));
        var p = new Pareamento(guardado, _relogio, _log);
        await p.Carregado;
        Assert.True(p.Pareado);

        using var porta = NovaPorta(p);
        await porta.Inicio;

        Assert.False(_chaves.Ligada(Chave.IaPelaRede));
        Assert.False(p.Pareado);
        Assert.False(p.Confere("k"));
        await EsperarSemAndar(() => !(guardado.Texto ?? "").Contains("sha256"));
    }

    [Fact]
    public async Task Segundo_cliente_recebe_409()
    {
        await Pronto();

        var r = await Pedir("POST", "/parear", """{"codigo":"123456","nome":"outro"}""");

        Assert.Equal(409, r.Status);
        Assert.Equal("Já existe um cliente pareado. Revogue no celular para parear outro.", Motivo(r));
    }

    [Fact]
    public async Task Sem_chave_ou_com_a_chave_errada_401()
    {
        var chave = await Pronto();

        var sem = await Pedir("GET", "/estado");
        var errada = await Pedir("GET", "/estado", chave: chave[..^2] + "AA");

        Assert.Equal(401, sem.Status);
        Assert.Equal("Cliente não pareado. Peça o código na tela IA pela rede do celular.", Motivo(sem));
        Assert.Equal(401, errada.Status);
        Assert.Equal(200, (await Pedir("GET", "/estado", chave: chave)).Status);
        // /ajuda não pede a chave.
        Assert.Equal(200, (await Pedir("GET", "/ajuda")).Status);
    }

    [Theory]
    [InlineData("8.8.8.8")]
    [InlineData("127.0.0.1")]
    [InlineData("::1")]
    public async Task Fora_da_rede_403_sem_ler_o_corpo_e_nada_no_historico(string ip)
    {
        var chave = await Pronto();
        // A rede de verdade decide pelas faixas: público e loopback ficam fora, qualquer que seja a interface.
        var real = new RedeLocal();
        _rede.Aceitar = real.Aceita;
        Assert.False(real.Aceita(IPAddress.Parse(ip)));

        var r = await Pedir("POST", "/ajuste", """{"codigo":"081","indice":2,"valor":2.0}""", chave, IPAddress.Parse(ip));

        Assert.Equal(403, r.Status);
        Assert.Equal("Só a rede local da casa.", Motivo(r));
        Assert.Empty(_historico.Entradas);
        Assert.Contains(_log.Linhas, l => l.Contains("fora da rede local"));
    }

    [Fact]
    public async Task Ajuste_do_ganho_da_saida_3_responde_200_entra_no_historico_como_IA_e_o_espelho_mostra()
    {
        var chave = await Pronto();

        var r = await PedirRodando("POST", "/ajuste", """{"codigo":"081","indice":2,"valor":2.0}""", chave);

        Assert.Equal(200, r.Status);
        Assert.Equal("+2.0", Json(r).GetProperty("valor").GetString()); // o eco, como o aparelho devolve
        Assert.Equal("+2,0 dB", Json(r).GetProperty("exibicao").GetString());
        Assert.Contains("*081*2*2.0*", _simulador.Escritas);
        Assert.Equal(2.0m, _estado.Valor("081", 2)!.Numero);
        var e = Ultima();
        Assert.Equal(OrigemComando.Ia, e.Origem);
        Assert.Equal(TipoComando.AjusteAudio, e.Tipo);
        Assert.Equal(new Endereco("081", 2), e.Endereco);
        Assert.True(e.Enviado);
    }

    [Fact]
    public async Task Salvar_a_memoria_2_abre_a_folha_e_com_o_toque_salva_e_responde_200()
    {
        var chave = await Pronto();

        var pedido = Pedir("POST", "/memoria/salvar", """{"memoria":2}""", chave);
        await EsperarSemAndar(() => _confirmacoes.Atual is not null);

        var folha = _confirmacoes.Atual!;
        Assert.StartsWith("A IA pede para salvar sobre a MUSICA ORIG", folha.Titulo);
        Assert.Equal("Apaga a memória anterior.", folha.Risco);
        Assert.Equal("Salvar sobre a MUSICA ORIG", folha.TextoBotao);
        Assert.False(pedido.IsCompleted);
        Assert.Equal(0, _simulador.Escritas.Count(e => e == "*240*1*"));

        _confirmacoes.Confirmar(folha);
        var r = await Rodar(pedido);

        Assert.Equal(200, r.Status);
        Assert.Equal(1, _simulador.Escritas.Count(e => e == "*240*1*"));
        Assert.Contains(_historico.Entradas, e => e.Origem == OrigemComando.Ia && e.Tipo == TipoComando.SalvarMemoria && e.Enviado);
        Assert.Null(_confirmacoes.Atual);
    }

    [Fact]
    public async Task Nao_deixar_na_folha_do_carregar_recusa_com_409_e_entra_no_historico()
    {
        var chave = await Pronto();

        var pedido = Pedir("POST", "/memoria/carregar", """{"memoria":3}""", chave);
        await EsperarSemAndar(() => _confirmacoes.Atual is not null);
        Assert.Equal("A IA pede para carregar a FILME", _confirmacoes.Atual!.Titulo);
        Assert.Equal("Troca todo o som de uma vez.", _confirmacoes.Atual.Risco);
        Assert.Equal("Carregar a FILME", _confirmacoes.Atual.TextoBotao);
        _confirmacoes.Recusar();
        var r = await pedido;

        Assert.Equal(409, r.Status);
        Assert.Equal("O usuário recusou na tela.", Motivo(r));
        Assert.DoesNotContain("*241*2*", _simulador.Escritas);
        var e = Ultima();
        Assert.Equal(OrigemComando.Ia, e.Origem);
        Assert.Equal(TipoComando.CarregarMemoria, e.Tipo);
        Assert.False(e.Enviado);
        Assert.Equal("O usuário recusou na tela.", e.Motivo);
    }

    [Fact]
    public async Task Folha_sem_resposta_por_60_s_recusa_com_409()
    {
        var chave = await Pronto();
        Assert.Equal(200, (await Pedir("POST", "/tarefa", """{"frase":"IA conferindo as memórias"}""", chave)).Status);
        Assert.True(_uso.IaAtiva);

        var pedido = Pedir("POST", "/memoria/carregar", """{"memoria":1}""", chave);
        await EsperarSemAndar(() => _confirmacoes.Atual is not null);
        _relogio.Avancar(TimeSpan.FromSeconds(31));
        // O pedido esperando conta como o cliente presente: a IA não "sumiu".
        Assert.True(_uso.IaAtiva);
        Assert.Null(_porta.AvisoConexao);
        _relogio.Avancar(TimeSpan.FromSeconds(28));
        await Task.Delay(20);
        Assert.False(pedido.IsCompleted);
        _relogio.Avancar(TimeSpan.FromSeconds(1));
        var r = await pedido;

        Assert.Equal(409, r.Status);
        Assert.Equal("O usuário não confirmou a tempo.", Motivo(r));
        Assert.Null(_confirmacoes.Atual);
        Assert.Equal("O usuário não confirmou a tempo.", Ultima().Motivo);
    }

    [Fact]
    public async Task Um_toque_antigo_nao_decide_um_pedido_novo()
    {
        var chave = await Pronto();
        var a = Pedir("POST", "/memoria/carregar", """{"memoria":1}""", chave);
        await EsperarSemAndar(() => _confirmacoes.Atual is not null);
        var folhaA = _confirmacoes.Atual!;
        _relogio.Avancar(PedidosConfirmacao.Prazo);
        Assert.Equal("O usuário não confirmou a tempo.", Motivo(await a));

        var b = Pedir("POST", "/memoria/carregar", """{"memoria":3}""", chave);
        await EsperarSemAndar(() => _confirmacoes.Atual is not null);
        var folhaB = _confirmacoes.Atual!;

        _confirmacoes.Confirmar(folhaA); // o toque na folha A, que já fechou
        await Task.Delay(50);

        Assert.Same(folhaB, _confirmacoes.Atual);
        Assert.False(b.IsCompleted);
        _confirmacoes.Recusar(folhaB);
        Assert.Equal(409, (await b).Status);
        Assert.DoesNotContain(_simulador.Escritas, e => e.StartsWith("*241*", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Chave_do_gerador_desligada_recusa_sem_folha()
    {
        var chave = await Pronto();
        var folhas = 0;
        _confirmacoes.Mudou += () => Interlocked.Increment(ref folhas);

        var r = await Pedir("POST", "/gerador/tom", """{"hz":1000,"db":-30.0}""", chave);

        Assert.Equal(409, r.Status);
        Assert.Equal(Sessao.MotivoChaveDesligada, Motivo(r));
        Assert.Equal(0, folhas);
        // A recusa da porta antes da folha entra no histórico, uma vez, como IA.
        var e = Assert.Single(_historico.Entradas);
        Assert.Equal(OrigemComando.Ia, e.Origem);
        Assert.Equal(TipoComando.Gerador, e.Tipo);
        Assert.False(e.Enviado);
        Assert.Equal(Sessao.MotivoChaveDesligada, e.Motivo);
        Assert.Null(_confirmacoes.Atual);
        Assert.DoesNotContain(_simulador.Escritas, e => e.StartsWith("*20", StringComparison.Ordinal) || e.StartsWith("*21", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Com_uma_folha_aberta_outro_risco_recebe_409()
    {
        var chave = await Pronto();
        var primeiro = Pedir("POST", "/memoria/carregar", """{"memoria":1}""", chave);
        await EsperarSemAndar(() => _confirmacoes.Atual is not null);

        var segundo = await Pedir("POST", "/memoria/salvar", """{"memoria":2}""", chave);

        Assert.Equal(409, segundo.Status);
        Assert.Equal("Já há um pedido esperando confirmação na tela.", Motivo(segundo));
        var e = Ultima();
        Assert.Equal(TipoComando.SalvarMemoria, e.Tipo);
        Assert.Equal("Já há um pedido esperando confirmação na tela.", e.Motivo);
        Assert.Equal("A IA pede para carregar a SPL", _confirmacoes.Atual!.Titulo);

        _confirmacoes.Recusar();
        Assert.Equal(409, (await primeiro).Status);
    }

    [Fact]
    public async Task PARAR_IA_apaga_o_cliente_nasce_um_codigo_novo_e_o_proximo_pedido_da_401()
    {
        var chave = await Pronto();

        _uso.PararIa();

        Assert.False(_pareamento.Pareado);
        Assert.Matches(@"^\d{6}$", _pareamento.Codigo);
        var r = await Pedir("GET", "/estado", chave: chave);
        Assert.Equal(401, r.Status);
        Assert.Contains(_porta.Eventos(), e => e.Tipo == "parada");
        await EsperarSemAndar(() => !(_arquivoPorta.Texto ?? "").Contains("sha256"));

        // Um pareamento novo libera a IA.
        Assert.True(_uso.Parada);
        await Parear();
        Assert.False(_uso.Parada);
    }

    [Fact]
    public async Task PARAR_IA_com_uma_folha_aberta_recusa_o_pedido()
    {
        var chave = await Pronto();
        var pedido = Pedir("POST", "/memoria/carregar", """{"memoria":1}""", chave);
        await EsperarSemAndar(() => _confirmacoes.Atual is not null);

        _uso.PararIa();
        var r = await pedido;

        Assert.Equal(409, r.Status);
        Assert.Equal("A IA foi parada.", Motivo(r));
        Assert.Null(_confirmacoes.Atual);
    }

    [Fact]
    public async Task Firmware_R004_so_os_GET_respondem()
    {
        _simulador.Firmware = "R004";
        var chave = await Pronto();
        Assert.True(_sessao.SoLeitura);

        var ajuste = await Pedir("POST", "/ajuste", """{"codigo":"081","indice":2,"valor":2.0}""", chave);
        var tarefa = await Pedir("DELETE", "/tarefa", null, chave);
        var estado = await Pedir("GET", "/estado", chave: chave);

        Assert.Equal(409, ajuste.Status);
        Assert.Equal("Firmware diferente do R003: a porta só responde às leituras.", Motivo(ajuste));
        Assert.Equal(409, tarefa.Status);
        Assert.Equal(200, estado.Status);
        Assert.True(Json(estado).GetProperty("conexao").GetProperty("soLeitura").GetBoolean());
        var e = Ultima();
        Assert.Equal(OrigemComando.Ia, e.Origem);
        Assert.Equal(TipoComando.AjusteAudio, e.Tipo);
        Assert.False(e.Enviado);
        Assert.Equal(PortaComandos.MsgFirmware, e.Motivo);
        Assert.DoesNotContain("*081*2*2.0*", _simulador.Escritas);
    }

    [Fact]
    public async Task Na_pausa_os_eventos_trazem_pausou_e_o_comando_da_tela()
    {
        var chave = await Pronto();
        Assert.True((await _uso.DefinirModoAsync(ModoToque.PausarIa, OrigemComando.Tela)).Ok);
        Assert.Equal(200, (await Pedir("POST", "/tarefa", """{"frase":"IA ajustando o ganho"}""", chave)).Status);
        Assert.Equal(EstadoIa.Ativa, _uso.Estado);
        Assert.Equal("IA ajustando o ganho", _uso.Tarefa);
        // Um comando da IA antes: ele não vira evento (só os da tela).
        Assert.Equal(200, (await PedirRodando("POST", "/ajuste", """{"codigo":"081","indice":1,"valor":1.0}""", chave)).Status);

        var tela = await Rodar(_escritor.EscreverAsync(Catalogo.ParametroDe("081")!, 0, 3.0m)); // a tela mexe
        Assert.True(tela.Ok);
        var r = await Pedir("GET", "/eventos?desde=0", chave: chave);

        Assert.Equal(200, r.Status);
        var eventos = Json(r).GetProperty("eventos").EnumerateArray().ToArray();
        Assert.Contains(eventos, e => e.GetProperty("tipo").GetString() == "pausou");
        var comando = Assert.Single(eventos, e => e.GetProperty("tipo").GetString() == "comando");
        Assert.Equal("081", comando.GetProperty("codigo").GetString());
        Assert.Equal(0, comando.GetProperty("indice").GetInt32());
        Assert.Equal("3.0", comando.GetProperty("valor").GetString());
        Assert.Equal("Tela", comando.GetProperty("origem").GetString());

        // Desde o último: nada novo.
        var ultimo = Json(r).GetProperty("ultimo").GetInt64();
        var depois = await Pedir("GET", $"/eventos?desde={ultimo}", chave: chave);
        Assert.Empty(Json(depois).GetProperty("eventos").EnumerateArray());

        // Um "desde" à frente do último (o app reabriu e a contagem recomeçou): vêm todos.
        var adiante = await Pedir("GET", $"/eventos?desde={ultimo + 150}", chave: chave);
        Assert.Equal(eventos.Length, Json(adiante).GetProperty("eventos").GetArrayLength());
    }

    [Fact]
    public async Task Ligar_e_desligar_a_chave_pela_tela_abre_e_fecha_a_porta_sozinha()
    {
        await Conectar();
        await _porta.Inicio;

        Assert.True((await _chaves.DefinirAsync(Chave.IaPelaRede, true, OrigemComando.Tela)).Ok);
        await EsperarSemAndar(() => _porta.Aberta);
        await Parear();

        Assert.True((await _chaves.DefinirAsync(Chave.IaPelaRede, false, OrigemComando.Tela)).Ok);
        await EsperarSemAndar(() => !_porta.Aberta && !_pareamento.Pareado && _uso.Parada);
        await EsperarSemAndar(() => !(_arquivoPorta.Texto ?? "").Contains("sha256"));
    }

    [Theory]
    [InlineData("/aparelho/reset-fabrica", Chave.ResetFabrica, "A IA pede para fazer o reset de fábrica", "Apaga a senha e as 3 memórias.", "Fazer o reset de fábrica", "*004*")]
    [InlineData("/aparelho/reset-audio", Chave.ResetAudio, "A IA pede para fazer o reset de áudio", "O estado resultante é imprevisível.", "Fazer o reset de áudio", "*242*")]
    public async Task Resets_pedem_o_toque_e_Nao_deixar_nao_envia_nada(string caminho, Chave chaveDoReset, string titulo, string risco, string botao, string quadro)
    {
        var chave = await Pronto();
        Assert.True((await _chaves.DefinirAsync(chaveDoReset, true, OrigemComando.Tela)).Ok);

        var pedido = Pedir("POST", caminho, null, chave);
        await EsperarSemAndar(() => _confirmacoes.Atual is not null);
        Assert.Equal(titulo, _confirmacoes.Atual!.Titulo);
        Assert.Equal(risco, _confirmacoes.Atual.Risco);
        Assert.Equal(botao, _confirmacoes.Atual.TextoBotao);
        _confirmacoes.Recusar();
        var r = await pedido;

        Assert.Equal(409, r.Status);
        Assert.Equal("O usuário recusou na tela.", Motivo(r));
        Assert.DoesNotContain(quadro, _simulador.Escritas);
    }

    [Fact]
    public async Task Corpo_com_a_marca_UTF8_no_comeco_vale()
    {
        var chave = await Pronto();

        var r = await Pedir("POST", "/tarefa", "\uFEFF{\"frase\":\"IA ajustando o EQ\"}", chave);

        Assert.Equal(200, r.Status);
        Assert.Equal("IA ajustando o EQ", _uso.Tarefa);
    }

    [Fact]
    public async Task Cliente_sumido_por_30_s_com_a_IA_ativa_termina_a_tarefa_e_avisa()
    {
        var chave = await Pronto();
        Assert.Equal(200, (await Pedir("POST", "/tarefa", """{"frase":"IA ajustando o EQ"}""", chave)).Status);
        Assert.True(_uso.IaAtiva);

        _relogio.Avancar(TimeSpan.FromSeconds(29));
        Assert.True(_uso.IaAtiva);
        _relogio.Avancar(TimeSpan.FromSeconds(1));

        Assert.False(_uso.IaAtiva);
        Assert.Equal("A IA perdeu a conexão.", _porta.AvisoConexao);

        // O cliente voltou: o aviso some.
        Assert.Equal(200, (await Pedir("GET", "/estado", chave: chave)).Status);
        Assert.Null(_porta.AvisoConexao);
    }

    [Fact]
    public async Task Desligar_a_chave_fecha_a_porta_e_apaga_o_cliente()
    {
        var chave = await Pronto();

        Assert.True((await _chaves.DefinirAsync(Chave.IaPelaRede, false, OrigemComando.Tela)).Ok);
        await _porta.AtualizarAsync();

        Assert.False(_porta.Aberta);
        Assert.False(_pareamento.Pareado);
        Assert.False(_pareamento.Confere(chave));
        Assert.True(_uso.Parada);
        await EsperarSemAndar(() => !(_arquivoPorta.Texto ?? "").Contains("sha256"));
    }

    [Fact]
    public async Task Ao_reabrir_a_chave_antiga_vale()
    {
        var chave = await Pronto();

        var reaberto = new Pareamento(_arquivoPorta, _relogio, _log);
        await reaberto.Carregado;
        using var porta = NovaPorta(reaberto);
        await porta.AtualizarAsync();

        Assert.True(reaberto.Pareado);
        Assert.Equal("Claude no notebook", reaberto.Cliente);
        Assert.Null(reaberto.Codigo);
        Assert.Equal(200, (await Pedir("GET", "/estado", chave: chave, porta: porta)).Status);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("não é json")]
    [InlineData("""{"formato":2,"cliente":{"nome":"x","sha256":"AAAA"}}""")]
    [InlineData("""{"formato":1,"cliente":{"nome":"x","sha256":"curto"}}""")]
    public async Task Arquivo_estragado_comeca_sem_cliente(string guardado)
    {
        var p = new Pareamento(new TextoMemoria(guardado), _relogio, _log);
        await p.Carregado;

        Assert.False(p.Pareado);
        Assert.Matches(@"^\d{6}$", p.Codigo);
    }

    [Fact]
    public async Task Porta_ocupada_mostra_o_erro_e_nada_escuta()
    {
        await _chaves.Carregada;
        var ocupante = new TcpListener(IPAddress.IPv6Any, 0);
        ocupante.Server.DualMode = true;
        ocupante.Start();
        try
        {
            var numero = ((IPEndPoint)ocupante.LocalEndpoint).Port;
            using var porta = NovaPorta(new Pareamento(new TextoMemoria(), _relogio, _log), numero);
            Assert.True((await _chaves.DefinirAsync(Chave.IaPelaRede, true, OrigemComando.Tela)).Ok);
            await porta.AtualizarAsync();

            Assert.False(porta.Aberta);
            Assert.Equal($"Não consegui abrir a porta {numero}.", porta.Erro);
        }
        finally
        {
            ocupante.Stop();
        }
    }

    // ----------------------------------------------------------------- caminhos

    [Fact]
    public async Task Estado_traz_a_conexao_as_memorias_a_IA_o_gerador_e_os_parametros()
    {
        var chave = await Pronto();

        var r = Json(await Pedir("GET", "/estado", chave: chave));

        Assert.True(r.GetProperty("conexao").GetProperty("conectado").GetBoolean());
        Assert.Equal("R003", r.GetProperty("conexao").GetProperty("firmware").GetString());
        Assert.Equal(new[] { "SPL", "MUSICA ORIG", "FILME" }, r.GetProperty("memorias").GetProperty("nomes").EnumerateArray().Select(n => n.GetString()!));
        Assert.Equal("inativa", r.GetProperty("ia").GetProperty("estado").GetString());
        Assert.Equal("Desligado", r.GetProperty("gerador").GetProperty("estado").GetString());
        Assert.Equal(4, r.GetProperty("clips").GetProperty("saidas").GetArrayLength());
        var ganho3 = r.GetProperty("parametros").EnumerateArray()
            .Single(p => p.GetProperty("codigo").GetString() == "081" && p.GetProperty("indice").ValueKind == JsonValueKind.Number && p.GetProperty("indice").GetInt32() == 2);
        Assert.Equal("saída 3", ganho3.GetProperty("onde").GetString());
        Assert.Equal("ganho", ganho3.GetProperty("nome").GetString());
        Assert.Equal(_simulador.Valor("081", 2), ganho3.GetProperty("noFio").GetString());
    }

    [Fact]
    public async Task Catalogo_traz_os_ajustes_de_audio_com_faixa_casas_e_opcoes()
    {
        var chave = await Pronto();

        var r = Json(await Pedir("GET", "/catalogo", chave: chave));

        var parametros = r.GetProperty("parametros").EnumerateArray().ToArray();
        Assert.Equal(Catalogo.Marco1.Count, parametros.Length);
        var ganho = parametros.Single(p => p.GetProperty("codigo").GetString() == "081");
        Assert.Equal("canal", ganho.GetProperty("escopo").GetString());
        Assert.Equal(4, ganho.GetProperty("indices").GetInt32());
        Assert.Equal(-45, ganho.GetProperty("minimo").GetDecimal());
        Assert.Equal(15, ganho.GetProperty("maximo").GetDecimal());
        Assert.Equal(1, ganho.GetProperty("casas").GetInt32());
        Assert.Equal("dB", ganho.GetProperty("unidade").GetString());
        var filtro = parametros.Single(p => p.GetProperty("codigo").GetString() == "042");
        Assert.Equal("LR24", filtro.GetProperty("opcoes").GetProperty("3").GetString());
        Assert.DoesNotContain(parametros, p => p.GetProperty("codigo").GetString() is "005" or "202" or "231");
    }

    [Fact]
    public async Task Historico_vem_do_mais_novo_com_o_limite()
    {
        var chave = await Pronto();
        await PedirRodando("POST", "/ajuste", """{"codigo":"081","indice":0,"valor":1.0}""", chave);
        await Andar(TimeSpan.FromMilliseconds(400));
        await PedirRodando("POST", "/ajuste", """{"codigo":"081","indice":1,"valor":1.5}""", chave);

        var r = Json(await Pedir("GET", "/historico?limite=1", chave: chave));

        var entrada = Assert.Single(r.GetProperty("entradas").EnumerateArray());
        Assert.Equal("1.5", entrada.GetProperty("valor").GetString());
        Assert.Equal("Ia", entrada.GetProperty("origem").GetString());
        Assert.Equal(2, Json(await Pedir("GET", "/historico", chave: chave)).GetProperty("entradas").GetArrayLength());
        Assert.Equal(400, (await Pedir("GET", "/historico?limite=0", chave: chave)).Status);
    }

    [Theory]
    [InlineData("/ajuste", "não é json")]
    [InlineData("/ajuste", """{"codigo":"005","valor":1}""")]
    [InlineData("/ajuste", """{"codigo":"081","valor":1.0}""")]
    [InlineData("/ajuste", """{"codigo":"081","indice":2,"valor":99.0}""")]
    [InlineData("/ajuste", """{"codigo":"081","indice":2,"valor":"2"}""")]
    [InlineData("/memoria/salvar", """{"memoria":4}""")]
    [InlineData("/memoria/salvar", """{"memoria":1,"nome":"nome comprido demais"}""")]
    [InlineData("/gerador/sweep", """{"inicioHz":20,"fimHz":200,"db":-30.0,"velocidade":0}""")]
    [InlineData("/aparelho/remote-out", """{"ligado":"sim"}""")]
    [InlineData("/tarefa", "{}")]
    public async Task Corpo_ou_campo_invalido_400(string caminho, string corpo)
    {
        var chave = await Pronto();

        var r = await Pedir("POST", caminho, corpo, chave);

        Assert.Equal(400, r.Status);
        Assert.False(Json(r).GetProperty("ok").GetBoolean());
        Assert.NotNull(Motivo(r));
        Assert.Null(_confirmacoes.Atual);
    }

    [Theory]
    [InlineData("GET", "/nada")]
    [InlineData("POST", "/aparelho/senha")]
    [InlineData("POST", "/chaves")]
    [InlineData("POST", "/modo")]
    [InlineData("PUT", "/ajuste")]
    public async Task Caminho_desconhecido_404_e_nao_ha_caminho_da_senha_nem_das_chaves(string metodo, string caminho)
    {
        var chave = await Pronto();

        var r = await Pedir(metodo, caminho, "{}", chave);

        Assert.Equal(404, r.Status);
    }

    [Fact]
    public async Task Ajuda_lista_os_caminhos_e_nao_fala_de_senha_nem_de_chave_de_acesso()
    {
        await Conectar();
        await LigarPorta();

        var r = await Pedir("GET", "/ajuda");

        var texto = Json(r).GetProperty("texto").GetString()!;
        foreach (var caminho in new[] { "/parear", "/estado", "/catalogo", "/eventos", "/historico", "/ajuste", "/memoria/salvar",
                     "/memoria/carregar", "/gerador/tom", "/gerador/sweep", "/gerador/parar", "/aparelho/remote-out",
                     "/aparelho/bluetooth", "/aparelho/nome", "/aparelho/reset-audio", "/aparelho/reset-fabrica", "/tarefa" })
            Assert.Contains(caminho, texto);
        Assert.DoesNotContain("senha", texto, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Gerador_com_a_chave_ligada_pede_o_toque_e_o_parar_nunca_confirma()
    {
        var chave = await Pronto();
        Assert.True((await _chaves.DefinirAsync(Chave.Gerador, true, OrigemComando.Tela)).Ok);

        var pedido = Pedir("POST", "/gerador/tom", """{"hz":1000,"db":-30.0}""", chave);
        await EsperarSemAndar(() => _confirmacoes.Atual is not null);
        Assert.Equal("A IA pede para tocar um tom de 1000 Hz a -30,0 dB", _confirmacoes.Atual!.Titulo);
        Assert.Equal("Um tom forte pode queimar o tweeter ou o sub.", _confirmacoes.Atual.Risco);
        Assert.Equal("Tocar o tom", _confirmacoes.Atual.TextoBotao);
        _confirmacoes.Confirmar();
        var tocar = await Rodar(pedido);
        Assert.Equal(200, tocar.Status);
        Assert.Equal(EstadoGerador.Tocando, _gerador.Estado);

        var parar = await PedirRodando("POST", "/gerador/parar", null, chave);

        Assert.Equal(200, parar.Status);
        Assert.Equal(EstadoGerador.Desligado, _gerador.Estado);
        Assert.Contains(_historico.Entradas, e => e.Origem == OrigemComando.Ia && e.Tipo == TipoComando.Gerador && e.Valor == "0" && e.Enviado);
    }

    [Fact]
    public async Task Bluetooth_nao_pede_o_toque_e_o_remote_out_pede()
    {
        var chave = await Pronto();

        var bt = await PedirRodando("POST", "/aparelho/bluetooth", """{"ligado":false}""", chave);
        Assert.Equal(200, bt.Status);
        Assert.Contains("*251*0*", _simulador.Escritas);

        var pedido = Pedir("POST", "/aparelho/remote-out", """{"ligado":false}""", chave);
        await EsperarSemAndar(() => _confirmacoes.Atual is not null);
        Assert.Equal("A IA pede para desligar o remote out", _confirmacoes.Atual!.Titulo);
        Assert.Equal("Liga ou corta o som de repente.", _confirmacoes.Atual.Risco);
        Assert.Equal("Desligar o remote out", _confirmacoes.Atual.TextoBotao);
        _confirmacoes.Recusar();
        Assert.Equal(409, (await pedido).Status);
        Assert.DoesNotContain("*252*0*", _simulador.Escritas);
    }

    // ----------------------------------------------------------------- origem, senha e HTTP

    [Fact]
    public async Task Um_handler_da_porta_que_escreve_vai_como_IA_e_a_origem_nao_vaza()
    {
        var chave = await Pronto();
        OrigemComando? naEscrita = null;
        _estado.Mudou += e =>
        {
            if (e == new Endereco("081", 2))
                naEscrita = Origem.Atual;
        };

        var r = await PedirRodando("POST", "/ajuste", """{"codigo":"081","indice":2,"valor":2.5}""", chave);

        Assert.Equal(200, r.Status);
        Assert.Equal(OrigemComando.Ia, naEscrita);
        Assert.Equal(OrigemComando.Ia, Ultima().Origem);
        // Quem chamou continua com a sua origem (a tela, sem escopo).
        Assert.Equal(OrigemComando.Tela, Origem.Atual);
        _uso.TerminarTarefa(); // a IA terminou: a tela volta a mexer
        await Andar(TimeSpan.FromSeconds(1));
        var tela = await Rodar(_escritor.EscreverAsync(Catalogo.ParametroDe("081")!, 0, -1.0m));
        Assert.True(tela.Ok);
        Assert.Equal(OrigemComando.Tela, Ultima().Origem);
    }

    [Fact]
    public async Task HTTP_de_ponta_a_ponta_no_loopback_e_a_origem_de_quem_abriu_nao_entra_na_porta()
    {
        await Conectar();
        _rede.Aceitar = _ => true; // o loopback, só neste teste
        Assert.True((await _chaves.DefinirAsync(Chave.IaPelaRede, true, OrigemComando.Tela)).Ok);
        await _porta.AtualizarAsync();
        _porta.Dispose(); // a do fixture não serve: esta abre dentro de um escopo App

        var vistas = new List<OrigemComando>();
        using (Origem.Como(OrigemComando.App))
        {
            using var porta = NovaPorta(new Pareamento(new TextoMemoria(), _relogio, _log));
            porta.AoChegar = _ => { lock (vistas) vistas.Add(Origem.Atual); };
            await porta.Inicio;
            await porta.AtualizarAsync();
            Assert.True(porta.Aberta);
            using var http = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{porta.NumeroPorta}"), Timeout = TimeSpan.FromSeconds(30) };

            var parear = await http.PostAsync("/parear", Corpo($$"""{"codigo":"{{porta.Pareamento.Codigo}}","nome":"Claude"}"""));
            Assert.Equal(HttpStatusCode.OK, parear.StatusCode);
            Assert.Equal("application/json", parear.Content.Headers.ContentType?.MediaType);
            var chave = JsonDocument.Parse(await parear.Content.ReadAsStringAsync()).RootElement.GetProperty("chave").GetString()!;

            var semChave = await http.GetAsync("/estado");
            Assert.Equal(HttpStatusCode.Unauthorized, semChave.StatusCode);

            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", chave);
            var estado = await http.GetAsync("/estado");
            var texto = await estado.Content.ReadAsStringAsync();
            lock (_respostas)
                _respostas.Add(texto);
            Assert.Equal(HttpStatusCode.OK, estado.StatusCode);
            Assert.True(JsonDocument.Parse(texto).RootElement.GetProperty("ok").GetBoolean());

            var ajuste = http.PostAsync("/ajuste", Corpo("""{"codigo":"081","indice":2,"valor":2.0}"""));
            await AndarAte(() => ajuste.IsCompleted);
            Assert.Equal(HttpStatusCode.OK, (await ajuste).StatusCode);
            Assert.Equal(OrigemComando.Ia, Ultima().Origem);

            Assert.Equal(HttpStatusCode.NotFound, (await http.GetAsync("/nada")).StatusCode);

            // Um corpo acima de 64 KB: 413, sem ler o corpo (só o cabeçalho chega).
            using (var cru = new TcpClient())
            {
                await cru.ConnectAsync(IPAddress.Loopback, porta.NumeroPorta);
                var fluxo = cru.GetStream();
                var pedido = $"POST /ajuste HTTP/1.1\r\nHost: x\r\nAuthorization: Bearer {chave}\r\nContent-Length: {ServidorHttp.MaximoCorpo + 1}\r\n\r\n";
                await fluxo.WriteAsync(Encoding.ASCII.GetBytes(pedido));
                using var leitor = new StreamReader(fluxo, Encoding.UTF8);
                var resposta = await leitor.ReadToEndAsync();
                Assert.StartsWith("HTTP/1.1 413 ", resposta);
                Assert.Contains("Connection: close", resposta);
            }

            // Quem abriu a porta estava num escopo App; nada disso entrou na porta.
            Assert.Equal(OrigemComando.App, Origem.Atual);
        }
        lock (vistas)
        {
            Assert.NotEmpty(vistas);
            Assert.All(vistas, o => Assert.Equal(OrigemComando.Tela, o));
        }
    }

    [Fact]
    public async Task HTTP_fora_da_rede_403_antes_do_corpo()
    {
        await Conectar();
        _rede.Aceitar = _ => false;
        await LigarPorta();
        using var http = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{_porta.NumeroPorta}") };

        var r = await http.PostAsync("/parear", Corpo("""{"codigo":"123456","nome":"x"}"""));

        Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode);
        Assert.Contains("Só a rede local da casa.", await r.Content.ReadAsStringAsync());
        Assert.Empty(_historico.Entradas);
    }

    [Fact]
    public async Task HTTP_fora_da_rede_403_logo_ao_aceitar_sem_ler_nada()
    {
        await Conectar();
        _rede.Aceitar = _ => false;
        await LigarPorta();
        using var cru = new TcpClient();
        await cru.ConnectAsync(IPAddress.Loopback, _porta.NumeroPorta);

        // O cliente não manda nada: a resposta vem assim mesmo, sem esperar o cabeçalho.
        using var leitor = new StreamReader(cru.GetStream(), Encoding.UTF8);
        var resposta = await leitor.ReadToEndAsync().WaitAsync(TimeSpan.FromSeconds(5));

        Assert.StartsWith("HTTP/1.1 403 ", resposta);
        Assert.Contains("Só a rede local da casa.", resposta);
    }

    [Fact]
    public void A_mesma_sub_rede_vem_das_interfaces_do_celular()
    {
        var rede = new RedeLocal(() => [(IPAddress.Parse("192.168.0.5"), 24)]);

        Assert.True(rede.Aceita(IPAddress.Parse("192.168.0.10")));
        Assert.True(rede.Aceita(IPAddress.Parse("::ffff:192.168.0.10")));
        Assert.False(rede.Aceita(IPAddress.Parse("192.168.1.10")));
        Assert.False(rede.Aceita(IPAddress.Parse("10.0.0.1")));
        Assert.False(rede.Aceita(IPAddress.Parse("127.0.0.1")));
        Assert.Equal("192.168.0.5", rede.Endereco);
    }

    [Fact]
    public async Task A_senha_AB12_nunca_aparece_numa_resposta()
    {
        var chave = await Pronto();
        await PedirRodando("POST", "/ajuste", """{"codigo":"081","indice":2,"valor":2.0}""", chave);

        foreach (var caminho in new[] { "/estado", "/catalogo", "/eventos", "/historico", "/ajuda" })
        {
            var r = await Pedir("GET", caminho, chave: chave);
            Assert.Equal(200, r.Status);
            Assert.DoesNotContain("AB12", r.Json);
        }
        Assert.DoesNotContain("AB12", _arquivoPorta.Texto);
    }

    [Fact]
    public void A_rede_local_aceita_so_as_faixas_privadas_e_o_IPv4_mapeado()
    {
        Assert.True(RedeLocal.Privado(IPAddress.Parse("10.1.2.3")));
        Assert.True(RedeLocal.Privado(IPAddress.Parse("172.16.0.1")));
        Assert.True(RedeLocal.Privado(IPAddress.Parse("172.31.255.1")));
        Assert.False(RedeLocal.Privado(IPAddress.Parse("172.32.0.1")));
        Assert.True(RedeLocal.Privado(IPAddress.Parse("192.168.0.10")));
        Assert.True(RedeLocal.Privado(IPAddress.Parse("169.254.1.1")));
        Assert.True(RedeLocal.Privado(IPAddress.Parse("fe80::1")));
        Assert.True(RedeLocal.Privado(IPAddress.Parse("fd12::1")));
        Assert.True(RedeLocal.Privado(IPAddress.Parse("::ffff:192.168.0.10")));
        Assert.False(RedeLocal.Privado(IPAddress.Parse("8.8.8.8")));
        Assert.False(RedeLocal.Privado(IPAddress.Parse("2001:db8::1")));
        Assert.False(RedeLocal.Privado(IPAddress.Loopback));
        Assert.False(new RedeLocal().Aceita(IPAddress.Parse("::ffff:127.0.0.1")));
        Assert.True(RedeLocal.MesmaSubRede(IPAddress.Parse("192.168.0.10"), IPAddress.Parse("::ffff:192.168.0.5"), 24));
        Assert.False(RedeLocal.MesmaSubRede(IPAddress.Parse("192.168.1.10"), IPAddress.Parse("192.168.0.5"), 24));
        Assert.True(RedeLocal.MesmaSubRede(IPAddress.Parse("10.0.200.1"), IPAddress.Parse("10.0.0.5"), 16));
    }

    private static StringContent Corpo(string json) => new(json, Encoding.UTF8, "application/json");

    private async Task Andar(TimeSpan quanto)
    {
        var passo = TimeSpan.FromMilliseconds(100);
        for (var t = TimeSpan.Zero; t < quanto; t += passo)
        {
            _relogio.Avancar(passo);
            await Task.Delay(1);
        }
    }
}
