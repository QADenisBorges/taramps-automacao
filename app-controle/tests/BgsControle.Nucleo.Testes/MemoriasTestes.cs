using System.Text.RegularExpressions;
using BgsControle.Nucleo.Escrita;
using BgsControle.Nucleo.Leitura;
using BgsControle.Nucleo.Registro;
using BgsControle.Nucleo.Seguranca;
using BgsControle.Simulador;

namespace BgsControle.Nucleo.Testes;

/// <summary>Um texto guardado em memória (a cópia antes, o link).</summary>
internal sealed class TextoMemoria : IRepositorioTexto
{
    private string? _texto;

    public TextoMemoria(string? inicial = null) => _texto = inicial;

    public bool FalharAoGravar { get; set; }

    /// <summary>Da tentativa de gravar número N em diante (contando todas), falha.</summary>
    public int? FalharAPartirDaTentativa { get; set; }

    /// <summary>Roda uma vez, na próxima tentativa de gravar (derrubar a conexão, desligar uma chave).</summary>
    public Action? AoGravar { get; set; }

    public int Tentativas { get; private set; }

    public int Gravacoes { get; private set; }

    public string? Texto => Volatile.Read(ref _texto);

    public Task<string?> LerAsync() => Task.FromResult(Texto);

    public Task GravarAsync(string conteudo)
    {
        Tentativas++;
        var acao = AoGravar;
        AoGravar = null;
        acao?.Invoke();
        if (FalharAoGravar || Tentativas >= FalharAPartirDaTentativa)
            throw new IOException("disco cheio");
        Volatile.Write(ref _texto, conteudo);
        Gravacoes++;
        return Task.CompletedTask;
    }
}

/// <summary>
/// A história 6 no núcleo, contra o processador simulado: a trava das memórias do aparelho, a cópia
/// antes com o "aplicado pela metade" e o link de canais. As memórias do aparelho não saem do app: o
/// salvar (240) e o nome (23N) desde a história 8c, e o carregar (241) desde a decisão do Denis de
/// 29/10/2026 ("as memórias do processador não devem aparecer no app").
/// </summary>
public sealed class MemoriasTestes : IDisposable
{
    private static readonly TemposSessao Tempos = new(
        Scan: TimeSpan.FromSeconds(1),
        TentativaConexao: TimeSpan.FromSeconds(1),
        PausaEntreTentativas: TimeSpan.FromMilliseconds(1),
        RespostaSenha: TimeSpan.FromSeconds(2),
        PrazoSenha: TimeSpan.FromSeconds(60));

    private static readonly TemposLeitura LeituraRapida = new(TimeSpan.Zero, TimeSpan.FromMilliseconds(100));
    private static readonly Regex Senha = new(@"^\*000\*[0-9A-Z]{4}\*$");

    /// <summary>O tipo da marca de um carregar de antes de 29/10/2026 (o app fechou no meio dele).</summary>
    private const string MarcaCarregarAntiga = "carregar-memoria";

    private readonly RelogioFalso _relogio = new();
    private readonly ProcessadorSimulado _simulador;
    private readonly RepositorioMemoria _repositorioChaves = new();
    private readonly Chaves _chaves;
    private readonly LogBle _log = new();
    private readonly Sessao _sessao;
    private readonly LeitorCompleto _leitor;
    private readonly LeituraAutomatica _leitura;
    private readonly EstadoAparelho _estado;
    private readonly Escritor _escritor;
    private readonly TextoMemoria _guardadoCopia = new();
    private readonly TextoMemoria _guardadoLink = new();
    private readonly CopiaAntes _copia;
    private readonly LinkCanais _link;

    private static Parametro Ganho => Catalogo.ParametroDe("081")!;

    public MemoriasTestes()
    {
        // A sessão e o leitor esperam em tempo real e o relógio falso não anda enquanto conectam:
        // a senha e as leituras respondem na hora; os ecos seguem no relógio falso.
        _simulador = new ProcessadorSimulado(_relogio) { AtrasoSenha = TimeSpan.Zero, AtrasoLeitura = TimeSpan.Zero };
        _chaves = new Chaves(_repositorioChaves);
        _sessao = new Sessao(_simulador, new CofreFalso { Senha = "AB12" }, _log, Tempos, _chaves);
        _leitor = new LeitorCompleto(_sessao, _log, LeituraRapida);
        _leitura = new LeituraAutomatica(_sessao, _leitor);
        _estado = new EstadoAparelho(_sessao, _leitura);
        _escritor = new Escritor(_sessao, _estado, _log, relogio: _relogio);
        _copia = new CopiaAntes(_sessao, _estado, _escritor, _leitura, _guardadoCopia, _log, _relogio);
        _link = new LinkCanais(_sessao, _estado, _escritor, _guardadoLink, _log);
    }

    /// <summary>
    /// Critério de aceite: em qualquer teste, só chegaram ao simulador a senha, as leituras do
    /// catálogo e as escritas de áudio; nenhum 240, 241 nem 23N.
    /// </summary>
    public void Dispose() =>
        Assert.All(_simulador.Escritas, e =>
            Assert.True(Senha.IsMatch(e) || Catalogo.EhLeitura(e) || Catalogo.EscritaDeAudio(e) is not null, e));

    private async Task Conectar()
    {
        await _chaves.Carregada;
        await _copia.Carregada;
        await _link.Carregada;
        await _sessao.ConectarAsync();
        Assert.Equal(EstadoSessao.Conectado, _sessao.Estado);
        await Esperar(() => _estado.Pronto && !_estado.Lendo && !_leitura.Lendo && !_copia.EmAndamento);
    }

    private static async Task Esperar(Func<bool> condicao)
    {
        for (var i = 0; i < 1000 && !condicao(); i++)
            await Task.Delay(10);
        Assert.True(condicao());
    }

    /// <summary>Anda o relógio falso até a tarefa acabar.</summary>
    private async Task<T> Rodar<T>(Task<T> tarefa)
    {
        for (var i = 0; i < 4000 && !tarefa.IsCompleted; i++)
        {
            _relogio.Avancar(TimeSpan.FromMilliseconds(10));
            await Task.Delay(1);
        }
        Assert.True(tarefa.IsCompleted, "a operação não terminou");
        return await tarefa;
    }

    private async Task Andar(TimeSpan quanto)
    {
        for (var t = TimeSpan.Zero; t < quanto; t += TimeSpan.FromMilliseconds(10))
        {
            _relogio.Avancar(TimeSpan.FromMilliseconds(10));
            await Task.Delay(1);
        }
    }

    /// <summary>Anda o relógio falso até a condição valer.</summary>
    private async Task AndarAte(Func<bool> condicao)
    {
        for (var i = 0; i < 2000 && !condicao(); i++)
        {
            _relogio.Avancar(TimeSpan.FromMilliseconds(10));
            await Task.Delay(1);
        }
        Assert.True(condicao());
    }

    private int EscritasDe(string prefixo) => _simulador.Escritas.Count(e => e.StartsWith(prefixo, StringComparison.Ordinal));

    private List<string> EscritasDesde(int inicio) => _simulador.Escritas.Skip(inicio).ToList();

    // ----------------------------------------------------------------- trava

    [Fact]
    public void Os_codigos_das_memorias_do_aparelho_sao_o_240_o_241_e_os_nomes()
    {
        Assert.Equal(["240", "241", "231", "232", "233"], Catalogo.CodigosDasMemoriasDoAparelho);
        // Nenhum deles é lido nem escrito pelo app.
        Assert.All(Catalogo.CodigosDasMemoriasDoAparelho, c => Assert.Null(Catalogo.ParametroDe(c)));
    }

    [Theory]
    [InlineData("musica orig2", "MUSICA ORIG2")]
    [InlineData("musica orig 2", null)] // 13 caracteres
    [InlineData("  filme  ", "FILME")]
    [InlineData("a_b", "A_B")]
    [InlineData("DOZE LETRAS1", "DOZE LETRAS1")]
    [InlineData("música#1", null)]
    [InlineData("treze letras!", null)]
    [InlineData("TREZE LETRAS", "TREZE LETRAS")]
    [InlineData("TREZE LETRASX", null)]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData("a*b", null)]
    [InlineData(null, null)]
    public void Nome_do_produto_vai_em_maiusculas_com_12_caracteres_no_maximo(string? digitado, string? esperado) =>
        Assert.Equal(esperado, Catalogo.NormalizarNomeProduto(digitado));

    /// <summary>
    /// Critério de aceite da 8c e da decisão de 29/10/2026: o 240, o 241 e o 23N nunca saem, em
    /// qualquer forma, com as chaves ligadas; nem a leitura dos nomes.
    /// </summary>
    [Theory]
    [InlineData("*240*0*")]
    [InlineData("*240*1*")]
    [InlineData("*240*5*")]
    [InlineData("240*0*")]
    [InlineData("#240#0#")]
    [InlineData("*241*0*")]
    [InlineData("*241*1*")]
    [InlineData("*241*2*")]
    [InlineData("*241*3*")]
    [InlineData("*241*-1*")]
    [InlineData("*241*0")]
    [InlineData("241*0*")]
    [InlineData("*241*0**")]
    [InlineData("#241#2#")]
    [InlineData("*231*FILME*")]
    [InlineData("*232*MUSICA ORIG2*")]
    [InlineData("*233*X*")]
    [InlineData("*231*filme*")]
    [InlineData("*231* FILME*")]
    [InlineData("233*X")]
    [InlineData("#231#")]
    [InlineData("#233#")]
    public async Task Trava_240_241_e_23N_sao_recusados_em_qualquer_forma_como_nao_liberados(string quadro)
    {
        await Conectar();
        var antes = _simulador.Escritas.Count;

        var r = await _sessao.EnviarAsync(quadro);

        Assert.False(r.Enviado);
        Assert.Equal(Sessao.MotivoNaoLiberado, r.Motivo);
        Assert.Equal(antes, _simulador.Escritas.Count);
        Assert.Contains(_log.Linhas, l => l.Contains($"✗ {quadro}") && l.Contains(Sessao.MotivoNaoLiberado));
    }

    [Theory]
    [InlineData("*240*0*")]
    [InlineData("*241*0*")]
    [InlineData("*231*X*")]
    public async Task Trava_sem_conexao_as_memorias_do_aparelho_tambem_sao_nao_liberadas(string quadro)
    {
        await _chaves.Carregada;

        Assert.Equal(Sessao.MotivoNaoLiberado, (await _sessao.EnviarAsync(quadro)).Motivo);
        Assert.Empty(_simulador.Escritas);
        Assert.Equal(0, _guardadoCopia.Gravacoes);
    }

    [Fact]
    public async Task Trava_em_modo_so_leitura_nenhuma_memoria_passa()
    {
        _simulador.Firmware = "R004";
        await Conectar();
        Assert.True(_sessao.SoLeitura);
        var antes = _simulador.Escritas.Count;

        foreach (var q in new[] { "*240*0*", "*241*0*", "*231*X*" })
            Assert.Equal(Sessao.MotivoNaoLiberado, (await _sessao.EnviarAsync(q)).Motivo);
        Assert.Equal(antes, _simulador.Escritas.Count);
        Assert.Equal(0, EscritasDe("*241*"));
    }

    [Fact]
    public async Task A_leitura_completa_nao_pergunta_os_nomes_das_memorias()
    {
        await Conectar();

        Assert.DoesNotContain(_simulador.Escritas, e => e.StartsWith("#23", StringComparison.Ordinal));
        Assert.Null(_estado.Valor("231"));
    }

    // ----------------------------------------------------------------- leitura nova

    [Fact]
    public async Task A_leitura_nova_espera_a_que_anda_e_le_de_novo_sem_prazo_fixo()
    {
        await Conectar();
        _simulador.AtrasoLeitura = TimeSpan.FromMilliseconds(10); // a leitura anda no relógio falso
        var primeira = _leitura.LerAsync();
        await AndarAte(() => _leitor.Lendo);
        var iniciadas = _leitura.Iniciadas;

        var nova = _leitura.LerNovaAsync();
        Assert.False(nova.IsCompleted); // não desiste: espera a que anda
        var r = await Rodar(nova);

        Assert.True(primeira.IsCompleted);
        Assert.NotNull(r);
        Assert.True(r!.Completo);
        Assert.Equal(iniciadas + 1, _leitura.Iniciadas); // uma leitura própria, começada depois da outra
    }

    // ----------------------------------------------------------------- uma marca de antes (migração)

    /// <summary>Uma CopiaAntes nova (o app reabriu) com esta marca guardada.</summary>
    private async Task<CopiaAntes> ReabrirCom(Copia guardada)
    {
        var copia = new CopiaAntes(_sessao, _estado, _escritor, _leitura, new TextoMemoria(CopiaAntes.ParaJson(guardada)), _log, _relogio);
        await copia.Carregada;
        return copia;
    }

    /// <summary>
    /// O app fechou no meio de um carregar, antes de 29/10/2026 (desde então o app não carrega memória do
    /// aparelho): a cópia é o estado lido agora, o aparelho muda em dois ajustes fora do app, e o app
    /// reabre com a marca guardada. Na conexão seguinte, fica pela metade.
    /// </summary>
    private async Task<(CopiaAntes Copia, string HpfOriginal)> PelaMetadeDeUmCarregarAntigo()
    {
        await Conectar();
        var valores = _estado.Copiar().Select(v => new ValorCopiado(v.Key.Codigo, v.Key.Indice, v.Value.NoFio)).ToList();
        await _sessao.DesconectarAsync();
        var hpfOriginal = _simulador.Valor("040", 0)!;
        _simulador.Definir("081", 0, "-20.0");
        _simulador.Definir("040", 0, hpfOriginal == "123" ? "124" : "123");
        var copia = await ReabrirCom(new Copia(DateTimeOffset.UnixEpoch, valores,
            new OperacaoPendente(MarcaCarregarAntiga, "2", "Carregar a memória 3 (FILME)", "FILME")));

        await Conectar();
        await Esperar(() => copia.PelaMetade is not null);
        return (copia, hpfOriginal);
    }

    [Fact]
    public async Task Marca_de_carregar_antiga_fica_pela_metade_e_so_o_Voltar_a_copia_escreve()
    {
        var (copia, _) = await PelaMetadeDeUmCarregarAntigo();
        var inicio = _simulador.Escritas.Count;

        var op = copia.PelaMetade!;
        Assert.Equal(MarcaCarregarAntiga, op.Tipo);
        Assert.Equal("Carregar a memória 3 (FILME)", op.Descricao);
        var escrita = await Rodar(_escritor.EscreverAsync(Ganho, 0, -1.0m));
        var terminar = await Rodar(copia.TerminarAsync());

        Assert.Equal(CopiaAntes.MsgPelaMetadePendente, escrita.Motivo);
        Assert.False(terminar.Ok);
        Assert.Equal(CopiaAntes.MsgNaoSeiRefazer, terminar.Motivo); // o app não carrega mais: nada de 241
        Assert.Equal(inicio, _simulador.Escritas.Count);
        Assert.NotNull(copia.PelaMetade);
    }

    [Fact]
    public async Task Queda_no_Voltar_a_copia_mantem_a_marca_e_o_pela_metade_volta_ao_reconectar()
    {
        var (copia, _) = await PelaMetadeDeUmCarregarAntigo();
        _simulador.CairDepoisDaEscrita = _simulador.Escritas.Count + 1; // cai depois da primeira volta

        var r = await Rodar(copia.VoltarAsync());

        Assert.False(r.Ok);
        Assert.Equal(EstadoSessao.Desconectado, _sessao.Estado);
        Assert.NotNull(copia.Ultima!.Operacao);

        await Conectar();
        await Esperar(() => copia.PelaMetade is not null);
        Assert.Equal(MarcaCarregarAntiga, copia.PelaMetade!.Tipo);
    }

    [Theory]
    [InlineData(MarcaCarregarAntiga, "x")]
    [InlineData(MarcaCarregarAntiga, "7")]
    [InlineData("salvar-memoria", "1")] // a marca de um salvar de antes da 8c: fica pela metade, sem refazer
    public async Task Marca_com_alvo_que_nao_e_memoria_fica_pela_metade_sem_memoria_0(string tipo, string alvo)
    {
        var copia = await ReabrirCom(new Copia(DateTimeOffset.UnixEpoch, [new ValorCopiado("081", 0, "+10.0")],
            new OperacaoPendente(tipo, alvo, "Operação estranha", "X", "240")));

        await Conectar();
        await Esperar(() => copia.PelaMetade is not null);

        Assert.Equal(alvo, copia.PelaMetade!.Alvo);
        Assert.DoesNotContain(_log.Linhas, l => l.Contains("memória 0", StringComparison.OrdinalIgnoreCase));
        var r = await Rodar(copia.TerminarAsync());
        Assert.False(r.Ok);
        Assert.NotNull(copia.PelaMetade); // continua pela metade: o Denis volta à cópia
    }

    [Fact]
    public async Task Voltar_a_copia_com_valor_ilegivel_na_copia_falha_e_nada_sai()
    {
        var copia = await ReabrirCom(new Copia(DateTimeOffset.UnixEpoch,
            [new ValorCopiado("081", 0, "lixo"), new ValorCopiado("081", 1, "-9.0")],
            new OperacaoPendente(MarcaCarregarAntiga, "2", "Carregar a memória 3 (FILME)", "FILME")));
        await Conectar();
        await Esperar(() => copia.PelaMetade is not null);
        var inicio = _simulador.Escritas.Count;

        var r = await Rodar(copia.VoltarAsync());

        Assert.False(r.Ok);
        Assert.Equal(CopiaAntes.MsgCopiaIlegivel(1), r.Motivo);
        Assert.Equal(inicio, _simulador.Escritas.Count);
        Assert.NotNull(copia.PelaMetade);
    }

    [Fact]
    public void Iguais_compara_texto_sem_diferenca_de_caixa()
    {
        var a = new Valor { NoFio = "a", Texto = "a", Exibicao = "A" };
        var b = new Valor { NoFio = "A", Texto = "A", Exibicao = "A" };

        Assert.True(CopiaAntes.Iguais(a, b));
        Assert.False(CopiaAntes.Iguais(a, new Valor { NoFio = "b", Texto = "b", Exibicao = "B" }));
    }

    [Fact]
    public async Task Voltar_a_copia_escreve_so_os_parametros_diferentes_e_o_estado_bate_com_a_copia()
    {
        var (copia, hpfOriginal) = await PelaMetadeDeUmCarregarAntigo();
        var inicio = _simulador.Escritas.Count;

        var r = await Rodar(copia.VoltarAsync());

        Assert.True(r.Ok, r.Motivo);
        var audio = EscritasDesde(inicio).Where(e => Catalogo.EscritaDeAudio(e) is not null).ToList();
        Assert.Equal(["*081*0*10.0*", $"*040*0*{hpfOriginal}*"], audio);
        Assert.Equal("+10.0", _simulador.Valor("081", 0));
        Assert.Equal(hpfOriginal, _simulador.Valor("040", 0));
        var guardada = copia.Ultima!;
        Assert.Null(guardada.Operacao);
        foreach (var v in guardada.Valores.Where(v => Catalogo.ParametroDe(v.Codigo) is { Extra: false }))
        {
            var p = Catalogo.ParametroDe(v.Codigo)!;
            Assert.True(CopiaAntes.Iguais(p.Decodificar(v.NoFio).Valor!, p.Decodificar(_simulador.Valor(v.Codigo, v.Indice)!).Valor!),
                $"{v.Codigo} {v.Indice}: cópia {v.NoFio}, aparelho {_simulador.Valor(v.Codigo, v.Indice)}");
        }
        Assert.Null(copia.PelaMetade);
    }

    [Fact]
    public async Task Voltar_a_copia_com_a_chave_de_ajustes_desligada_nada_sai()
    {
        var (copia, _) = await PelaMetadeDeUmCarregarAntigo();
        await _chaves.DefinirAsync(Chave.AjustesAudio, false, OrigemComando.Tela);
        var inicio = _simulador.Escritas.Count;

        var r = await Rodar(copia.VoltarAsync());

        Assert.False(r.Ok);
        Assert.Equal(Sessao.MotivoChaveDesligada, r.Motivo);
        Assert.Equal(inicio, _simulador.Escritas.Count);
        Assert.NotNull(copia.PelaMetade); // continua pela metade
    }

    [Fact]
    public async Task A_marca_sobrevive_ao_app_fechar_e_a_conexao_seguinte_mostra_pela_metade()
    {
        // O app fechou no meio de um carregar (antes de 29/10/2026): só o arquivo ficou.
        var copia = await ReabrirCom(new Copia(DateTimeOffset.UnixEpoch, [new ValorCopiado("081", 0, "+10.0")],
            new OperacaoPendente(MarcaCarregarAntiga, "2", "Carregar a memória 3 (FILME)", "FILME")));

        await Conectar();
        await Esperar(() => copia.PelaMetade is not null);

        Assert.Equal("Carregar a memória 3 (FILME)", copia.PelaMetade!.Descricao);
    }

    // ----------------------------------------------------------------- link

    /// <summary>Liga ou desliga um par como o GerenteSetups faz (o link sai das ligações do Setup; história 8f).</summary>
    private async Task<string?> Ligar(ParCanais par, bool ligado = true)
    {
        await _link.Carregada;
        var l12 = par == ParCanais.Saidas12 ? ligado : _link.Ligado(ParCanais.Saidas12);
        var l34 = par == ParCanais.Saidas34 ? ligado : _link.Ligado(ParCanais.Saidas34);
        return await _link.DefinirEstadoAsync(l12, l34);
    }

    [Fact]
    public async Task Definir_o_estado_liga_o_link_sem_copiar_nada_e_fica_guardado()
    {
        await Conectar();
        var inicio = _simulador.Escritas.Count;

        Assert.Null(await Ligar(ParCanais.Saidas12));

        Assert.True(_link.Ligado(ParCanais.Saidas12));
        Assert.False(_link.Ligado(ParCanais.Saidas34));
        Assert.Equal(inicio, _simulador.Escritas.Count); // o link sai das ligações: não copia a mestre
        Assert.Contains("\"1-2\": true", _guardadoLink.Texto);

        // Um link novo lê o guardado.
        var outroLink = new LinkCanais(_sessao, _estado, new Escritor(_sessao, _estado, _log, relogio: _relogio), new TextoMemoria(_guardadoLink.Texto), _log);
        await outroLink.Carregada;
        Assert.True(outroLink.Ligado(ParCanais.Saidas12));
    }

    [Fact]
    public async Task Link_ligado_ganho_da_saida_1_em_menos_2_as_duas_saidas_ficam_em_menos_2()
    {
        await Conectar();
        Assert.Null(await Ligar(ParCanais.Saidas12));

        var r = await Rodar(_escritor.EscreverAsync(Ganho, 0, -2.0m));

        Assert.True(r.Ok, r.Motivo);
        Assert.Equal("-2.0", _simulador.Valor("081", 0));
        Assert.Equal("-2.0", _simulador.Valor("081", 1));
        Assert.Equal(-2.0m, _estado.Valor("081", 1)!.Numero);
    }

    [Fact]
    public async Task Link_ligado_mexer_na_outra_saida_escreve_so_nela()
    {
        await Conectar();
        Assert.Null(await Ligar(ParCanais.Saidas12));
        var mestre = _simulador.Valor("081", 0);

        Assert.True((await Rodar(_escritor.EscreverAsync(Ganho, 1, -4.0m))).Ok);
        await Andar(TimeSpan.FromMilliseconds(500));

        Assert.Equal("-4.0", _simulador.Valor("081", 1));
        Assert.Equal(mestre, _simulador.Valor("081", 0));
    }

    [Fact]
    public async Task Link_desligado_a_escrita_na_mestre_fica_so_nela()
    {
        await Conectar();
        var outro = _simulador.Valor("081", 1);

        Assert.True((await Rodar(_escritor.EscreverAsync(Ganho, 0, -2.0m))).Ok);
        await Andar(TimeSpan.FromMilliseconds(500));

        Assert.Equal(outro, _simulador.Valor("081", 1));
    }

    [Fact]
    public async Task Desligar_nao_mexe_no_aparelho_e_guarda()
    {
        await Conectar();
        Assert.Null(await Ligar(ParCanais.Saidas34));
        var inicio = _simulador.Escritas.Count;

        Assert.Null(await Ligar(ParCanais.Saidas34, false));

        Assert.False(_link.Ligado(ParCanais.Saidas34));
        Assert.Equal(inicio, _simulador.Escritas.Count);
        Assert.Contains("\"3-4\": false", _guardadoLink.Texto);
    }

    [Fact]
    public async Task Voltar_a_copia_com_o_link_ligado_nao_repete_na_outra_saida()
    {
        await _link.Carregada;
        await Conectar();
        Assert.Null(await Ligar(ParCanais.Saidas12));
        await _sessao.DesconectarAsync();
        var (copia, hpfOriginal) = await PelaMetadeDeUmCarregarAntigo();
        var inicio = _simulador.Escritas.Count;

        Assert.True((await Rodar(copia.VoltarAsync())).Ok);
        await Andar(TimeSpan.FromMilliseconds(500));

        // Cada saída é escrita uma vez, pela cópia; a escrita na mestre não é repetida pelo link (a
        // saída 2 volta ao valor dela na cópia, não ao da mestre).
        Assert.Equal(1, EscritasDesde(inicio).Count(e => e.StartsWith("*081*0*", StringComparison.Ordinal)));
        Assert.True(EscritasDesde(inicio).Count(e => e.StartsWith("*081*1*", StringComparison.Ordinal)) <= 1);
        Assert.Equal("+10.0", _simulador.Valor("081", 0));
        Assert.Equal("+7.0", _simulador.Valor("081", 1));
        Assert.Equal(hpfOriginal, _simulador.Valor("040", 0));
    }

    private static Parametro Roteamento => Catalogo.ParametroDe("010")!;

    [Fact]
    public async Task Link_1_2_ligado_o_roteamento_da_saida_1_nao_vai_para_a_2()
    {
        await Conectar();
        Assert.Null(await Ligar(ParCanais.Saidas12));
        var rota1 = _simulador.Valor("010", 0)!;
        var rota2 = _simulador.Valor("010", 1)!;
        var nova = rota1 == "a" ? "b" : "a";

        var r = await Rodar(_escritor.EscreverAsync(Roteamento, 0, nova));
        await Andar(TimeSpan.FromMilliseconds(500));

        Assert.True(r.Ok, r.Motivo);
        Assert.Equal(nova, _simulador.Valor("010", 0));
        Assert.Equal(rota2, _simulador.Valor("010", 1));
        Assert.Equal(0, EscritasDe("*010*1*"));
    }

    [Fact]
    public async Task Link_3_4_ligado_ganho_da_saida_3_vai_para_a_4()
    {
        await Conectar();
        Assert.Null(await Ligar(ParCanais.Saidas34));
        var saida1 = _simulador.Valor("081", 0);

        var r = await Rodar(_escritor.EscreverAsync(Ganho, 2, -5.0m));

        Assert.True(r.Ok, r.Motivo);
        Assert.Null(r.Aviso);
        Assert.Equal("-5.0", _simulador.Valor("081", 2));
        Assert.Equal("-5.0", _simulador.Valor("081", 3));
        Assert.Equal(saida1, _simulador.Valor("081", 0));
    }

    [Fact]
    public async Task Link_ligado_a_saida_2_diferente_volta_como_aviso_no_resultado_da_mestre()
    {
        await Conectar();
        Assert.Null(await Ligar(ParCanais.Saidas12));
        _simulador.EcosDiferentes["*081*1*-2.0*"] = "#081#1#-1.0#";

        var r = await Rodar(_escritor.EscreverAsync(Ganho, 0, -2.0m));

        Assert.True(r.Ok, r.Motivo);
        Assert.Equal("A saída 2 não acompanhou.", r.Aviso);
    }

    [Fact]
    public async Task Link_com_excecao_no_espelho_a_mestre_termina_com_o_aviso()
    {
        await Conectar();
        _escritor.Espelho = (_, _) => throw new InvalidOperationException("quebrado");

        var r = await Rodar(_escritor.EscreverAsync(Ganho, 0, -2.0m));

        Assert.True(r.Ok, r.Motivo);
        Assert.Equal(Escritor.MsgLinkFalhou, r.Aviso);
        Assert.Contains(_log.Linhas, l => l.Contains("falha no link"));
    }

    [Fact]
    public async Task Ligar_sem_conseguir_guardar_fica_desligado()
    {
        await Conectar();
        _guardadoLink.FalharAoGravar = true;

        var r = await Ligar(ParCanais.Saidas12);

        Assert.Equal(LinkCanais.MsgNaoGuardou, r);
        Assert.False(_link.Ligado(ParCanais.Saidas12));
    }

    [Fact]
    public async Task Desligar_sem_conseguir_guardar_desliga_e_avisa()
    {
        await Conectar();
        Assert.Null(await Ligar(ParCanais.Saidas12));
        _guardadoLink.FalharAoGravar = true;

        var r = await Ligar(ParCanais.Saidas12, false);

        Assert.Equal(LinkCanais.MsgDesligadoSemGuardar, r);
        Assert.False(_link.Ligado(ParCanais.Saidas12));
    }

    [Fact]
    public async Task Link_guardado_com_formato_que_nao_e_numero_fica_desligado_sem_erro()
    {
        var link = new LinkCanais(_sessao, _estado, new Escritor(_sessao, _estado, _log, relogio: _relogio),
            new TextoMemoria("{\"formato\": \"um\", \"pares\": {\"1-2\": true}}"), _log);

        await link.Carregada; // não falha

        Assert.False(link.Ligado(ParCanais.Saidas12));
    }

    [Fact]
    public async Task Ligar_o_par_com_as_saidas_ja_diferentes_avisa_na_hora_sem_escrever()
    {
        await Conectar(); // na leitura de 18/10, o ganho do CH1 (+10) e o do CH2 (+7) diferem
        var inicio = _simulador.Escritas.Count;

        Assert.Null(await Ligar(ParCanais.Saidas12));

        Assert.True(_link.Diferentes(ParCanais.Saidas12));
        Assert.Equal(inicio, _simulador.Escritas.Count);
    }

    [Fact]
    public async Task Link_ligado_com_as_saidas_diferentes_depois_da_leitura_avisa_sem_escrever()
    {
        await Conectar();
        Assert.Null(await Ligar(ParCanais.Saidas34));
        Assert.False(_link.Diferentes(ParCanais.Saidas34)); // na leitura de 18/10, o CH3 e o CH4 são iguais
        await _sessao.DesconectarAsync();
        _simulador.Definir("081", 3, "-7.0"); // mudou fora do app
        var inicio = _simulador.Escritas.Count;

        await Conectar();
        await Esperar(() => _link.Diferentes(ParCanais.Saidas34));

        Assert.Equal("Link 3–4: as saídas estão diferentes", _link.AvisoDiferenca(ParCanais.Saidas34));
        Assert.Null(_link.AvisoDiferenca(ParCanais.Saidas12));
        Assert.All(EscritasDesde(inicio), e => Assert.True(Catalogo.EhLeitura(e) || Senha.IsMatch(e), e)); // nada escrito
        Assert.True(_link.Ligado(ParCanais.Saidas34));
    }
}
