using System.Text.RegularExpressions;
using BgsControle.Nucleo.Escrita;
using BgsControle.Nucleo.Leitura;
using BgsControle.Nucleo.Memorias;
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
/// A história 6 no núcleo, contra o processador simulado: a trava das memórias, o carregar das
/// memórias do aparelho, a cópia antes com o "aplicado pela metade" e o link de canais. Desde a
/// história 8c, o salvar (240) e o nome (23N) não saem do app.
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
    private readonly GerenteMemorias _memorias;

    private static Parametro Ganho => Catalogo.ParametroDe("081")!;

    public MemoriasTestes()
    {
        // A sessão e o leitor esperam em tempo real e o relógio falso não anda enquanto conectam:
        // a senha e as leituras respondem na hora; os ecos e os acks seguem no relógio falso.
        _simulador = new ProcessadorSimulado(_relogio) { AtrasoSenha = TimeSpan.Zero, AtrasoLeitura = TimeSpan.Zero };
        _chaves = new Chaves(_repositorioChaves);
        _sessao = new Sessao(_simulador, new CofreFalso { Senha = "AB12" }, _log, Tempos, _chaves);
        _leitor = new LeitorCompleto(_sessao, _log, LeituraRapida);
        _leitura = new LeituraAutomatica(_sessao, _leitor);
        _estado = new EstadoAparelho(_sessao, _leitura);
        _escritor = new Escritor(_sessao, _estado, _log, relogio: _relogio);
        _copia = new CopiaAntes(_sessao, _estado, _escritor, _leitura, _guardadoCopia, _log, _relogio);
        _link = new LinkCanais(_sessao, _estado, _escritor, _guardadoLink, _log);
        _memorias = new GerenteMemorias(_sessao, _estado, _leitura, _copia, _log, relogio: _relogio);
    }

    /// <summary>
    /// Critério de aceite: em qualquer teste, só chegaram ao simulador a senha, as leituras do
    /// catálogo, as escritas de áudio e o 241; nenhum 240 nem 23N (história 8c).
    /// </summary>
    public void Dispose() =>
        Assert.All(_simulador.Escritas, e =>
            Assert.True(Senha.IsMatch(e) || Catalogo.EhLeitura(e) || Catalogo.EscritaDeAudio(e) is not null
                        || Catalogo.ComandoDeMemoria(e) is not null, e));

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

    [Theory]
    [InlineData("*241*0*", 0)]
    [InlineData("*241*1*", 1)]
    [InlineData("*241*2*", 2)]
    public void A_forma_exata_do_carregar(string quadro, int memoria)
    {
        Assert.Equal(new ComandoMemoria(TipoComandoMemoria.Carregar, memoria), Catalogo.ComandoDeMemoria(quadro));
    }

    [Theory]
    [InlineData("*241*3*")]
    [InlineData("*241*-1*")]
    [InlineData("*241*0")]
    [InlineData("241*0*")]
    [InlineData("*241*0**")]
    [InlineData("*241*0*\n")]
    [InlineData("*240*0*")] // o salvar não é comando deste app (história 8c)
    [InlineData("*240*2*")]
    [InlineData("*231*FILME*")] // nem o nome
    [InlineData("*232*MUSICA ORIG2*")]
    public void Fora_da_forma_exata_nao_e_comando_de_memoria(string quadro) =>
        Assert.Null(Catalogo.ComandoDeMemoria(quadro));

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
    public void Nome_de_memoria_vai_em_maiusculas_com_12_caracteres_no_maximo(string? digitado, string? esperado) =>
        Assert.Equal(esperado, Catalogo.NormalizarNomeMemoria(digitado));

    [Theory]
    [InlineData("*241*0*", Chave.CarregarMemoria)]
    public async Task Trava_com_a_chave_desligada_recusa_antes_do_BLE_com_o_motivo_no_log(string quadro, Chave chave)
    {
        await Conectar();
        Assert.True((await _chaves.DefinirAsync(chave, false, OrigemComando.Tela)).Ok);
        var antes = _simulador.Escritas.Count;

        var r = await _sessao.EnviarAsync(quadro);

        Assert.False(r.Enviado);
        Assert.Equal(Sessao.MotivoChaveDesligada, r.Motivo);
        Assert.Equal(antes, _simulador.Escritas.Count);
        Assert.Contains(_log.Linhas, l => l.Contains($"✗ {quadro}") && l.Contains(Sessao.MotivoChaveDesligada));
    }

    /// <summary>Critério de aceite da 8c: o 240 e o 23N nunca saem, em qualquer forma, com as chaves ligadas.</summary>
    [Theory]
    [InlineData("*240*0*")]
    [InlineData("*240*1*")]
    [InlineData("*240*5*")]
    [InlineData("240*0*")]
    [InlineData("#240#0#")]
    [InlineData("*231*FILME*")]
    [InlineData("*232*MUSICA ORIG2*")]
    [InlineData("*233*X*")]
    [InlineData("*231*filme*")]
    [InlineData("*231* FILME*")]
    [InlineData("233*X")]
    public async Task Trava_240_e_23N_sao_recusados_em_qualquer_forma_como_nao_liberados(string quadro)
    {
        await Conectar();
        var antes = _simulador.Escritas.Count;

        var r = await _sessao.EnviarAsync(quadro);

        Assert.False(r.Enviado);
        Assert.Equal(Sessao.MotivoNaoLiberado, r.Motivo);
        Assert.Equal(antes, _simulador.Escritas.Count);
        Assert.Contains(_log.Linhas, l => l.Contains($"✗ {quadro}") && l.Contains(Sessao.MotivoNaoLiberado));
    }

    [Fact]
    public async Task Trava_241_fora_do_formato_continua_memoria_fora_do_formato()
    {
        await Conectar();
        var antes = _simulador.Escritas.Count;

        var r = await _sessao.EnviarAsync("*241*5*");

        Assert.False(r.Enviado);
        Assert.Equal(Sessao.MotivoMemoriaForaDoFormato, r.Motivo);
        Assert.Equal(antes, _simulador.Escritas.Count);
    }

    [Fact]
    public async Task Trava_240_sem_conexao_tambem_e_nao_liberado()
    {
        await _chaves.Carregada;

        Assert.Equal(Sessao.MotivoNaoLiberado, (await _sessao.EnviarAsync("*240*0*")).Motivo);
        Assert.Empty(_simulador.Escritas);
    }

    [Fact]
    public async Task Trava_em_modo_so_leitura_nenhuma_memoria_passa()
    {
        _simulador.Firmware = "R004";
        await Conectar();
        Assert.True(_sessao.SoLeitura);

        Assert.Equal(Sessao.MotivoSoLeitura, (await _sessao.EnviarAsync("*241*0*")).Motivo);
        foreach (var q in new[] { "*240*0*", "*231*X*" })
            Assert.Equal(Sessao.MotivoNaoLiberado, (await _sessao.EnviarAsync(q)).Motivo);
        var r = await Rodar(_memorias.CarregarAsync(2));
        Assert.False(r.Ok);
        Assert.Equal(0, EscritasDe("*241*"));
    }

    [Fact]
    public async Task Trava_sem_conexao_as_memorias_sao_recusadas()
    {
        await _chaves.Carregada;

        Assert.Equal(Sessao.MotivoSemConexao, (await _sessao.EnviarAsync("*241*0*")).Motivo);
        var r = await _memorias.CarregarAsync(0);
        Assert.False(r.Ok);
        Assert.Equal(Sessao.MotivoSemConexao, r.Motivo);
        Assert.Empty(_simulador.Escritas);
        Assert.Equal(0, _guardadoCopia.Gravacoes);
    }

    // ----------------------------------------------------------------- carregar

    [Fact]
    public async Task Carregar_a_memoria_3_guarda_a_copia_envia_241_2_le_tudo_e_FILME_fica_em_uso()
    {
        _simulador.DefinirMemoria(2, "081", 0, "-20.0");
        await Conectar();
        var inicio = _simulador.Escritas.Count;
        var iniciadas = _leitura.Iniciadas;

        var r = await Rodar(_memorias.CarregarAsync(2));

        Assert.True(r.Ok, r.Mensagem);
        var escritas = EscritasDesde(inicio);
        Assert.Equal("*241*2*", escritas[0]);
        Assert.Equal(Catalogo.Consultas.Select(c => c.Texto), escritas.Skip(1)); // a leitura completa nova
        Assert.Equal(iniciadas + 1, _leitura.Iniciadas);
        Assert.Equal(2, _memorias.EmUso);
        Assert.Equal("FILME", _memorias.Nome(2));
        Assert.Equal("Memória 3 FILME em uso.", r.Mensagem);
        Assert.Equal(-20.0m, _estado.Valor("081", 0)!.Numero); // o espelho é a memória carregada

        var copia = CopiaAntes.DeJson(_guardadoCopia.Texto!)!;
        Assert.Contains(copia.Valores, v => v is { Codigo: "081", Indice: 0, NoFio: "+10.0" }); // o estado antes
        Assert.Null(copia.Operacao);
    }

    [Fact]
    public async Task Carregar_com_a_chave_desligada_nada_sai()
    {
        await Conectar();
        await _chaves.DefinirAsync(Chave.CarregarMemoria, false, OrigemComando.Tela);
        var inicio = _simulador.Escritas.Count;

        var r = await _memorias.CarregarAsync(2);

        Assert.False(r.Ok);
        Assert.Equal(Sessao.MotivoChaveDesligada, r.Motivo);
        Assert.Equal(inicio, _simulador.Escritas.Count);
        Assert.Equal(0, _guardadoCopia.Gravacoes);
        Assert.Contains(_log.Linhas, l => l.Contains("✗ *241*2*") && l.Contains(Sessao.MotivoChaveDesligada));
    }

    [Fact]
    public async Task Carregar_recusado_antes_do_BLE_mantem_a_memoria_em_uso()
    {
        await Conectar();
        Assert.True((await Rodar(_memorias.CarregarAsync(2))).Ok);
        Assert.Equal(2, _memorias.EmUso);
        // A chave é desligada depois da conferência e antes do 241 (enquanto a cópia é guardada).
        _guardadoCopia.AoGravar = () => _chaves.DefinirAsync(Chave.CarregarMemoria, false, OrigemComando.Tela).GetAwaiter().GetResult();

        var r = await Rodar(_memorias.CarregarAsync(0));

        Assert.False(r.Ok);
        Assert.Equal(Sessao.MotivoChaveDesligada, r.Motivo);
        Assert.Equal(0, EscritasDe("*241*0*"));
        Assert.Equal(2, _memorias.EmUso); // nada mudou no aparelho
        Assert.Null(_copia.PelaMetade);
        Assert.Null(CopiaAntes.DeJson(_guardadoCopia.Texto!)!.Operacao);
    }

    [Fact]
    public async Task Carregar_com_a_conexao_caindo_antes_do_241_nada_mudou_e_nao_fica_pela_metade()
    {
        await Conectar();
        _guardadoCopia.AoGravar = _simulador.Derrubar; // cai enquanto a cópia antes é guardada

        var r = await Rodar(_memorias.CarregarAsync(2));

        Assert.False(r.Ok);
        Assert.Equal(0, EscritasDe("*241*"));
        Assert.Null(CopiaAntes.DeJson(_guardadoCopia.Texto!)!.Operacao);

        await Conectar();
        await Andar(TimeSpan.FromMilliseconds(200));
        Assert.Null(_copia.PelaMetade);
        Assert.Null(_copia.MotivoBloqueio());
    }

    [Fact]
    public async Task Carregar_com_a_leitura_parada_e_a_sessao_aberta_fica_pela_metade_ja()
    {
        await Conectar();
        foreach (var c in Catalogo.Consultas)
            _simulador.Silenciar.Add(c.Texto); // depois do 241, o aparelho não responde às leituras

        var r = await Rodar(_memorias.CarregarAsync(2));

        Assert.False(r.Ok);
        Assert.Equal(EstadoSessao.Conectado, _sessao.Estado);
        Assert.NotNull(_copia.PelaMetade); // já, sem esperar outra conexão
        Assert.Equal(GerenteMemorias.TipoCarregar, _copia.PelaMetade!.Tipo);
        Assert.Null(_memorias.EmUso);
    }

    [Fact]
    public async Task A_leitura_nova_do_carregar_espera_a_que_anda_e_le_de_novo_sem_prazo_fixo()
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

    [Fact]
    public async Task Durante_o_carregar_uma_escrita_de_audio_e_recusada_mesmo_depois_do_241()
    {
        _simulador.DefinirMemoria(2, "081", 0, "-20.0");
        await Conectar();

        var carga = _memorias.CarregarAsync(2);
        await AndarAte(() => _simulador.Escritas.Contains("*241*2*"));
        var escrita = await Rodar(_escritor.EscreverAsync(Ganho, 0, -1.0m));
        var r = await Rodar(carga);

        Assert.True(r.Ok, r.Mensagem);
        Assert.False(escrita.Ok);
        Assert.Equal(CopiaAntes.MsgOutraOperacao, escrita.Motivo);
        Assert.Equal(0, EscritasDe("*081*0*-1.0*"));
        Assert.Equal("-20.0", _simulador.Valor("081", 0));
    }

    /// <summary>Carrega a memória 3 diferente em dois ajustes e derruba logo depois do 241; reconecta.</summary>
    private async Task<string> CarregarComQueda()
    {
        _simulador.DefinirMemoria(2, "081", 0, "-20.0");
        var hpfOriginal = _simulador.Valor("040", 0)!;
        _simulador.DefinirMemoria(2, "040", 0, hpfOriginal == "123" ? "124" : "123");
        await Conectar();
        _simulador.CairDepoisDaEscrita = _simulador.Escritas.Count + 1;

        var r = await Rodar(_memorias.CarregarAsync(2));

        Assert.False(r.Ok);
        Assert.Equal(GerenteMemorias.MsgCaiuCarregar, r.Mensagem);
        Assert.Equal("*241*2*", _simulador.Escritas[^1]);
        Assert.Equal("-20.0", _simulador.Valor("081", 0)); // o aparelho carregou
        Assert.Null(_memorias.EmUso);
        Assert.Null(_copia.PelaMetade); // só depois da leitura da conexão nova

        await Conectar();
        await Esperar(() => _copia.PelaMetade is not null);
        return hpfOriginal;
    }

    [Fact]
    public async Task Queda_no_carregar_ao_reconectar_fica_aplicado_pela_metade()
    {
        await CarregarComQueda();

        var op = _copia.PelaMetade!;
        Assert.Equal(GerenteMemorias.TipoCarregar, op.Tipo);
        Assert.Equal("2", op.Alvo);
        Assert.Equal("Carregar a memória 3 (FILME)", op.Descricao);
    }

    [Fact]
    public async Task Pela_metade_so_Terminar_ou_Voltar_audio_link_e_outra_memoria_sao_recusados()
    {
        await CarregarComQueda();
        var inicio = _simulador.Escritas.Count;

        var escrita = await Rodar(_escritor.EscreverAsync(Ganho, 0, -1.0m));
        var link = await Rodar(_link.VincularAsync(ParCanais.Saidas12));
        var carregar = await Rodar(_memorias.CarregarAsync(0));

        Assert.Equal(CopiaAntes.MsgPelaMetadePendente, escrita.Motivo);
        Assert.Equal(CopiaAntes.MsgPelaMetadePendente, link.Motivo);
        Assert.False(_link.Ligado(ParCanais.Saidas12));
        Assert.Equal(CopiaAntes.MsgPelaMetadePendente, carregar.Motivo);
        Assert.Equal(inicio, _simulador.Escritas.Count);
        Assert.NotNull(_copia.PelaMetade);
    }

    [Fact]
    public async Task Terminar_com_o_241_recusado_e_a_sessao_aberta_continua_pela_metade()
    {
        await CarregarComQueda();
        // A chave desliga enquanto o Terminar grava a marca, antes do 241.
        _guardadoCopia.AoGravar = () => _chaves.DefinirAsync(Chave.CarregarMemoria, false, OrigemComando.Tela).GetAwaiter().GetResult();
        var inicio = _simulador.Escritas.Count;

        var r = await Rodar(_copia.TerminarAsync());

        Assert.False(r.Ok);
        Assert.Equal(inicio, _simulador.Escritas.Count);
        Assert.NotNull(_copia.PelaMetade);
        Assert.NotNull(CopiaAntes.DeJson(_guardadoCopia.Texto!)!.Operacao);
    }

    [Fact]
    public async Task Queda_no_Voltar_a_copia_mantem_a_marca_e_o_pela_metade_volta_ao_reconectar()
    {
        await CarregarComQueda();
        _simulador.CairDepoisDaEscrita = _simulador.Escritas.Count + 1; // cai depois da primeira volta

        var r = await Rodar(_copia.VoltarAsync());

        Assert.False(r.Ok);
        Assert.Equal(EstadoSessao.Desconectado, _sessao.Estado);
        Assert.NotNull(CopiaAntes.DeJson(_guardadoCopia.Texto!)!.Operacao);

        await Conectar();
        await Esperar(() => _copia.PelaMetade is not null);
        Assert.Equal(GerenteMemorias.TipoCarregar, _copia.PelaMetade!.Tipo);
    }

    /// <summary>Uma CopiaAntes nova (o app reabriu) com esta marca guardada, ligada às memórias.</summary>
    private async Task<CopiaAntes> ReabrirCom(Copia guardada)
    {
        var copia = new CopiaAntes(_sessao, _estado, _escritor, _leitura, new TextoMemoria(CopiaAntes.ParaJson(guardada)), _log, _relogio);
        _ = new GerenteMemorias(_sessao, _estado, _leitura, copia, _log, relogio: _relogio);
        await copia.Carregada;
        return copia;
    }

    [Theory]
    [InlineData(GerenteMemorias.TipoCarregar, "x")]
    [InlineData(GerenteMemorias.TipoCarregar, "7")]
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
            new OperacaoPendente(GerenteMemorias.TipoCarregar, "2", "Carregar a memória 3 (FILME)", "FILME")));
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
        var hpfOriginal = await CarregarComQueda();
        var inicio = _simulador.Escritas.Count;

        var r = await Rodar(_copia.VoltarAsync());

        Assert.True(r.Ok, r.Motivo);
        var audio = EscritasDesde(inicio).Where(e => Catalogo.EscritaDeAudio(e) is not null).ToList();
        Assert.Equal(["*081*0*10.0*", $"*040*0*{hpfOriginal}*"], audio);
        Assert.Equal("+10.0", _simulador.Valor("081", 0));
        Assert.Equal(hpfOriginal, _simulador.Valor("040", 0));
        var copia = CopiaAntes.DeJson(_guardadoCopia.Texto!)!;
        Assert.Null(copia.Operacao);
        foreach (var v in copia.Valores.Where(v => Catalogo.ParametroDe(v.Codigo) is { Extra: false }))
        {
            var p = Catalogo.ParametroDe(v.Codigo)!;
            Assert.True(CopiaAntes.Iguais(p.Decodificar(v.NoFio).Valor!, p.Decodificar(_simulador.Valor(v.Codigo, v.Indice)!).Valor!),
                $"{v.Codigo} {v.Indice}: cópia {v.NoFio}, aparelho {_simulador.Valor(v.Codigo, v.Indice)}");
        }
        Assert.Null(_copia.PelaMetade);
    }

    [Fact]
    public async Task Voltar_a_copia_com_a_chave_de_ajustes_desligada_nada_sai()
    {
        await CarregarComQueda();
        await _chaves.DefinirAsync(Chave.AjustesAudio, false, OrigemComando.Tela);
        var inicio = _simulador.Escritas.Count;

        var r = await Rodar(_copia.VoltarAsync());

        Assert.False(r.Ok);
        Assert.Equal(Sessao.MotivoChaveDesligada, r.Motivo);
        Assert.Equal(inicio, _simulador.Escritas.Count);
        Assert.NotNull(_copia.PelaMetade); // continua pela metade
    }

    [Fact]
    public async Task Terminar_refaz_o_carregar_com_a_mesma_copia_e_a_memoria_fica_em_uso()
    {
        await CarregarComQueda();
        var copiaAntes = CopiaAntes.DeJson(_guardadoCopia.Texto!)!;
        var inicio = _simulador.Escritas.Count;

        var r = await Rodar(_copia.TerminarAsync());

        Assert.True(r.Ok, r.Motivo);
        Assert.Equal("*241*2*", EscritasDesde(inicio)[0]);
        Assert.Equal(2, _memorias.EmUso);
        Assert.Null(_copia.PelaMetade);
        var copiaDepois = CopiaAntes.DeJson(_guardadoCopia.Texto!)!;
        Assert.Null(copiaDepois.Operacao);
        Assert.Equal(copiaAntes.Valores, copiaDepois.Valores); // a cópia de antes da queda
        Assert.Contains(copiaDepois.Valores, v => v is { Codigo: "081", Indice: 0, NoFio: "+10.0" });
    }

    [Fact]
    public async Task A_marca_sobrevive_ao_app_fechar_e_a_conexao_seguinte_mostra_pela_metade()
    {
        // O app fechou no meio de um carregar: só o arquivo ficou.
        var copia = await ReabrirCom(new Copia(DateTimeOffset.UnixEpoch, [new ValorCopiado("081", 0, "+10.0")],
            new OperacaoPendente(GerenteMemorias.TipoCarregar, "2", "Carregar a memória 3 (FILME)", "FILME")));

        await Conectar();
        await Esperar(() => copia.PelaMetade is not null);

        Assert.Equal("Carregar a memória 3 (FILME)", copia.PelaMetade!.Descricao);
    }

    // ----------------------------------------------------------------- link

    [Fact]
    public async Task Vincular_1_2_copia_a_saida_1_na_2_e_o_link_fica_guardado()
    {
        await Conectar();
        var rotaSaida2 = _simulador.Valor("010", 1);

        var r = await Rodar(_link.VincularAsync(ParCanais.Saidas12));

        Assert.True(r.Ok, r.Motivo);
        Assert.True(_link.Ligado(ParCanais.Saidas12));
        Assert.False(_link.Ligado(ParCanais.Saidas34));
        foreach (var p in LinkCanais.Parametros)
        {
            var mestre = p.Decodificar(_simulador.Valor(p.Codigo, 0)!).Valor!;
            var outro = p.Decodificar(_simulador.Valor(p.Codigo, 1)!).Valor!;
            Assert.True(CopiaAntes.Iguais(mestre, outro), $"{p.Codigo}: {mestre.NoFio} × {outro.NoFio}");
        }
        Assert.Equal(rotaSaida2, _simulador.Valor("010", 1)); // o roteamento não vai (o par continua estéreo)
        Assert.Equal(0, _simulador.Escritas.Count(e => e.StartsWith("*", StringComparison.Ordinal) && Catalogo.EscritaDeAudio(e) is { Indice: not 1 }));
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
        Assert.True((await Rodar(_link.VincularAsync(ParCanais.Saidas12))).Ok);

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
        Assert.True((await Rodar(_link.VincularAsync(ParCanais.Saidas12))).Ok);
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
    public async Task Vincular_com_a_chave_de_ajustes_desligada_nada_sai_e_o_link_fica_desligado()
    {
        await Conectar();
        await _chaves.DefinirAsync(Chave.AjustesAudio, false, OrigemComando.Tela);
        var inicio = _simulador.Escritas.Count;

        var r = await Rodar(_link.VincularAsync(ParCanais.Saidas12));

        Assert.False(r.Ok);
        Assert.Equal(Sessao.MotivoChaveDesligada, r.Motivo);
        Assert.False(_link.Ligado(ParCanais.Saidas12));
        Assert.Equal(inicio, _simulador.Escritas.Count);
    }

    [Fact]
    public async Task Desvincular_nao_mexe_no_aparelho_e_guarda()
    {
        await Conectar();
        Assert.True((await Rodar(_link.VincularAsync(ParCanais.Saidas34))).Ok);
        var inicio = _simulador.Escritas.Count;

        var r = await _link.DesvincularAsync(ParCanais.Saidas34);

        Assert.True(r.Ok);
        Assert.False(_link.Ligado(ParCanais.Saidas34));
        Assert.Equal(inicio, _simulador.Escritas.Count);
        Assert.Contains("\"3-4\": false", _guardadoLink.Texto);
    }

    [Fact]
    public async Task Voltar_a_copia_com_o_link_ligado_nao_repete_na_outra_saida()
    {
        await _link.Carregada;
        await Conectar();
        Assert.True((await Rodar(_link.VincularAsync(ParCanais.Saidas12))).Ok);
        await _sessao.DesconectarAsync();
        var hpfOriginal = await CarregarComQueda();
        var inicio = _simulador.Escritas.Count;

        Assert.True((await Rodar(_copia.VoltarAsync())).Ok);
        await Andar(TimeSpan.FromMilliseconds(500));

        // A cópia tem as saídas 1 e 2 iguais (link): cada uma é escrita uma vez, pela cópia; a
        // escrita na mestre não é repetida pelo link.
        Assert.Equal(1, EscritasDesde(inicio).Count(e => e.StartsWith("*081*0*", StringComparison.Ordinal)));
        Assert.True(EscritasDesde(inicio).Count(e => e.StartsWith("*081*1*", StringComparison.Ordinal)) <= 1);
        Assert.Equal("+10.0", _simulador.Valor("081", 0));
        Assert.Equal("+10.0", _simulador.Valor("081", 1));
        Assert.Equal(hpfOriginal, _simulador.Valor("040", 0));
    }

    private static Parametro Roteamento => Catalogo.ParametroDe("010")!;

    [Fact]
    public async Task Link_1_2_ligado_o_roteamento_da_saida_1_nao_vai_para_a_2()
    {
        await Conectar();
        Assert.True((await Rodar(_link.VincularAsync(ParCanais.Saidas12))).Ok);
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
        Assert.True((await Rodar(_link.VincularAsync(ParCanais.Saidas34))).Ok);
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
        Assert.True((await Rodar(_link.VincularAsync(ParCanais.Saidas12))).Ok);
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
    public async Task Vincular_sem_conseguir_guardar_fica_desligado()
    {
        await Conectar();
        _guardadoLink.FalharAoGravar = true;

        var r = await Rodar(_link.VincularAsync(ParCanais.Saidas12));

        Assert.False(r.Ok);
        Assert.Equal(LinkCanais.MsgNaoGuardou, r.Motivo);
        Assert.False(_link.Ligado(ParCanais.Saidas12));
    }

    [Fact]
    public async Task Desvincular_sem_conseguir_guardar_desliga_e_avisa()
    {
        await Conectar();
        Assert.True((await Rodar(_link.VincularAsync(ParCanais.Saidas12))).Ok);
        _guardadoLink.FalharAoGravar = true;

        var r = await _link.DesvincularAsync(ParCanais.Saidas12);

        Assert.True(r.Ok);
        Assert.Equal(LinkCanais.MsgDesligadoSemGuardar, r.Motivo);
        Assert.False(_link.Ligado(ParCanais.Saidas12));
    }

    [Fact]
    public async Task Queda_no_meio_da_copia_do_link_fica_desligado_e_nada_guardado()
    {
        _simulador.Definir("081", 1, "-7.0"); // a saída 2 diferente da 1: há o que copiar
        await Conectar();
        _simulador.CairDepoisDaEscrita = _simulador.Escritas.Count + 1;

        var r = await Rodar(_link.VincularAsync(ParCanais.Saidas12));

        Assert.False(r.Ok);
        Assert.False(_link.Ligado(ParCanais.Saidas12));
        Assert.Equal(0, _guardadoLink.Gravacoes);
    }

    [Fact]
    public async Task Vincular_sem_o_valor_da_mestre_no_espelho_e_recusado()
    {
        var p = LinkCanais.Parametros[0];
        _simulador.Silenciar.Add(Catalogo.Consultas.First(c => c.Parametro == p && c.Indice == 0).Texto);
        await Conectar();
        Assert.Null(_estado.Valor(p.Codigo, 0));
        var inicio = _simulador.Escritas.Count;

        var r = await Rodar(_link.VincularAsync(ParCanais.Saidas12));

        Assert.False(r.Ok);
        Assert.Equal(Escritor.MsgSemLeitura, r.Motivo);
        Assert.False(_link.Ligado(ParCanais.Saidas12));
        Assert.Equal(inicio, _simulador.Escritas.Count);
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
    public async Task Link_ligado_com_as_saidas_diferentes_depois_da_leitura_avisa_sem_escrever()
    {
        await Conectar();
        Assert.True((await Rodar(_link.VincularAsync(ParCanais.Saidas12))).Ok);
        Assert.False(_link.Diferentes(ParCanais.Saidas12));
        await _sessao.DesconectarAsync();
        _simulador.Definir("081", 1, "-7.0"); // mudou fora do app
        var inicio = _simulador.Escritas.Count;

        await Conectar();
        await Esperar(() => _link.Diferentes(ParCanais.Saidas12));

        Assert.Equal("Link 1–2: as saídas estão diferentes", _link.AvisoDiferenca(ParCanais.Saidas12));
        Assert.Null(_link.AvisoDiferenca(ParCanais.Saidas34));
        Assert.All(EscritasDesde(inicio), e => Assert.True(Catalogo.EhLeitura(e) || Senha.IsMatch(e), e)); // nada escrito
        Assert.True(_link.Ligado(ParCanais.Saidas12));
    }
}
