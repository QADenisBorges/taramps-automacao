using System.Text.RegularExpressions;
using BgsControle.Nucleo;
using BgsControle.Nucleo.Aparelho;
using BgsControle.Nucleo.Escrita;
using BgsControle.Nucleo.Gerador;
using BgsControle.Nucleo.Leitura;
using BgsControle.Nucleo.Memorias;
using BgsControle.Nucleo.Player;
using BgsControle.Nucleo.Presets;
using BgsControle.Nucleo.Registro;
using BgsControle.Nucleo.Seguranca;
using BgsControle.Nucleo.Setups;
using BgsControle.Nucleo.Uso;
using BgsControle.Simulador;

namespace BgsControle.Telas.Testes;

/// <summary>
/// O app inteiro sem MAUI, contra o processador simulado: sessão com as chaves, leitura
/// automática, espelho, escritor e a thread da tela falsa.
/// </summary>
internal sealed class Bancada
{
    public static readonly TemposSessao Tempos = new(
        Scan: TimeSpan.FromSeconds(1),
        TentativaConexao: TimeSpan.FromSeconds(1),
        PausaEntreTentativas: TimeSpan.FromMilliseconds(1),
        RespostaSenha: TimeSpan.FromSeconds(2),
        PrazoSenha: TimeSpan.FromSeconds(60));

    private static readonly TemposLeitura LeituraRapida = new(TimeSpan.Zero, TimeSpan.FromMilliseconds(100));
    private static readonly Regex Senha = new(@"^\*000\*[0-9A-Z]{4}\*$");

    /// <param name="usoCompartilhado">
    /// História 9a: o histórico e o uso compartilhado na porta, guardados em <see cref="ArquivosUso"/>
    /// (à parte, para não mexer nas contagens de gravação dos outros testes).
    /// </param>
    /// <param name="setups">
    /// História 8c: os Setups e as memórias do celular (a conferência roda em cada conexão), guardados
    /// em <see cref="Arquivos"/>. Fora disso, nenhuma conferência mexe no mute dos outros testes.
    /// </param>
    public Bancada(ArquivosFalso? arquivos = null, bool usoCompartilhado = false, bool setups = false)
    {
        // A sessão e o leitor esperam em tempo real e o relógio falso não anda enquanto conectam:
        // a senha e as leituras respondem na hora; os ecos e os acks seguem no relógio falso.
        Simulador = new ProcessadorSimulado(Relogio) { AtrasoSenha = TimeSpan.Zero, AtrasoLeitura = TimeSpan.Zero };
        Arquivos = arquivos ?? new ArquivosFalso();
        Chaves = new Chaves(new RepositorioChavesArquivos(Arquivos));
        if (usoCompartilhado)
        {
            Historico = new Historico(new RepositorioTextoArquivos(ArquivosUso, RepositorioTextoArquivos.ArquivoHistorico), Relogio, Log);
            Uso = new UsoCompartilhado(new RepositorioTextoArquivos(ArquivosUso, RepositorioTextoArquivos.ArquivoUsoIa), Relogio, Log);
        }
        Sessao = new Sessao(Simulador, Cofre, Log, Tempos, Chaves, Historico, Uso);
        Leitor = new LeitorCompleto(Sessao, Log, LeituraRapida);
        Leitura = new LeituraAutomatica(Sessao, Leitor);
        Estado = new EstadoAparelho(Sessao, Leitura);
        Escritor = new Escritor(Sessao, Estado, Log, relogio: Relogio);
        Fabrica = new FabricaControles(Sessao, Chaves, Estado, Escritor, Tela, Relogio);
        // História 6: a cópia antes, o link e as memórias, guardados pelos arquivos falsos.
        Copia = new CopiaAntes(Sessao, Estado, Escritor, Leitura,
            new RepositorioTextoArquivos(Arquivos, RepositorioTextoArquivos.ArquivoCopia), Log, Relogio);
        Link = new LinkCanais(Sessao, Estado, Escritor,
            new RepositorioTextoArquivos(Arquivos, RepositorioTextoArquivos.ArquivoLink), Log);
        Memorias = new GerenteMemorias(Sessao, Estado, Leitura, Copia, Log, relogio: Relogio);
        // História 8: os presets no celular, guardados nos arquivos falsos.
        Presets = new GerentePresets(Sessao, Estado, Escritor, Leitura, Copia,
            new RepositorioTextoArquivos(Arquivos, RepositorioTextoArquivos.ArquivoPresets), Log, Relogio);
        // História 8d: as playlists por memória do celular, guardadas nos arquivos falsos.
        Playlists = new GerentePlaylists(new RepositorioTextoArquivos(Arquivos, RepositorioTextoArquivos.ArquivoPlaylists), Log);
        if (setups)
        {
            Setups = new GerenteSetups(Sessao, Estado, Escritor, Leitura, Copia, Link, Memorias, Presets, Playlists, Chaves,
                new RepositorioTextoArquivos(Arquivos, RepositorioTextoArquivos.ArquivoSetups),
                new RepositorioTextoArquivos(Arquivos, RepositorioTextoArquivos.ArquivoSetupMudo), Log, Relogio);
        }
        // História 7a: o gerador, criado antes da primeira conexão (a regra 2 confere o 202 em cada leitura).
        Gerador = new GeradorSinal(Sessao, Estado, Leitura, Copia, Log, Memorias, relogio: Relogio);
        // História 7b: a monitoração e os comandos de aparelho.
        Monitor = new MonitorAparelho(Sessao, Leitura, Relogio);
        Comandos = new ComandosAparelho(Sessao, Estado, Leitura, Copia, Cofre, Log, Memorias, Gerador, relogio: Relogio);
    }

    public CofreMemoria Cofre { get; } = new() { Senha = "AB12" };

    /// <summary>Os arquivos do histórico e do modo do toque (só com o uso compartilhado).</summary>
    public ArquivosFalso ArquivosUso { get; } = new();

    public Historico? Historico { get; }
    public UsoCompartilhado? Uso { get; }

    public HistoricoViewModel CriarHistoricoViewModel() => new(Historico!, Tela, Relogio);

    public IaAtivaViewModel CriarIaAtivaViewModel() => new(Uso!, Historico!, Tela, Relogio);
    public MonitorAparelho Monitor { get; }
    public ComandosAparelho Comandos { get; }

    public MonitoracaoViewModel CriarMonitoracaoViewModel() => new(Monitor, Tela, Relogio);

    public AparelhoViewModel CriarAparelhoViewModel() => new(Comandos, Sessao, Chaves, Estado, Copia, Tela, Relogio, Memorias, Gerador);

    public GeradorSinal Gerador { get; }

    public GeradorViewModel CriarGeradorViewModel() => new(Gerador, Sessao, Chaves, Estado, Copia, Tela, Relogio, Memorias);

    public CopiaAntes Copia { get; }
    public LinkCanais Link { get; }
    public GerenteMemorias Memorias { get; }

    /// <summary>Os Setups (só com <c>setups: true</c>).</summary>
    public GerenteSetups? Setups { get; }

    public SalvarViewModel CriarSalvarViewModel() => new(Setups!, Estado, Link, Tela, Relogio);

    public SetupViewModel CriarSetupViewModel() => new(Setups!, Sessao, Tela, Relogio);

    public MemoriasViewModel CriarMemoriasViewModel(SalvarViewModel? salvar = null) =>
        new(Setups!, Memorias, Copia, Sessao, Chaves, Estado, salvar ?? CriarSalvarViewModel(), Tela, Relogio);

    public LinkViewModel CriarLinkViewModel() => new(Link, Sessao, Chaves, Estado, Tela, Relogio);

    public GerentePresets Presets { get; }

    /// <summary>A tela Presets (só com <c>setups: true</c>: a memória do celular em uso lembra o preset aplicado).</summary>
    public PresetsViewModel CriarPresetsViewModel() => new(Presets, Setups!, Copia, Sessao, Chaves, Estado, Tela, Relogio);

    public GerentePlaylists Playlists { get; }

    public RelogioFalso Relogio { get; } = new();
    public ProcessadorSimulado Simulador { get; }
    public ArquivosFalso Arquivos { get; }
    public Chaves Chaves { get; }
    public LogBle Log { get; } = new();
    public Sessao Sessao { get; }
    public LeitorCompleto Leitor { get; }
    public LeituraAutomatica Leitura { get; }
    public EstadoAparelho Estado { get; }
    public Escritor Escritor { get; }
    public DespachanteFila Tela { get; } = new();
    public FabricaControles Fabrica { get; }

    public async Task Conectar()
    {
        await Chaves.Carregada;
        await Copia.Carregada;
        await Link.Carregada;
        await Presets.Carregada;
        if (Setups is not null)
            await Setups.Carregada;
        await Sessao.ConectarAsync();
        Assert.Equal(EstadoSessao.Conectado, Sessao.Estado);
        await Apoio.Esperar(() => Estado.Pronto && !Estado.Lendo && !Leitura.Lendo && !Copia.EmAndamento);
        if (Setups is not null)
        {
            // A conferência pode mutar (os ecos andam no relógio falso).
            for (var i = 0; i < 3000 && !Setups.Conferido; i++)
            {
                Relogio.Avancar(TimeSpan.FromMilliseconds(10));
                await Task.Delay(1);
            }
            Assert.True(Setups.Conferido, "a conferência do Setup não terminou");
        }
        await Tela.EsvaziarAsync();
    }

    /// <summary>Anda o relógio falso em passos curtos; a thread da tela anda junto.</summary>
    public async Task Andar(TimeSpan quanto, int passoMs = 10)
    {
        var passo = TimeSpan.FromMilliseconds(passoMs);
        for (var t = TimeSpan.Zero; t < quanto; t += passo)
        {
            Relogio.Avancar(passo);
            await Task.Delay(1);
            await Tela.EsvaziarAsync();
        }
    }

    /// <summary>Anda o relógio até a tarefa acabar (no máximo 20 s de tempo falso).</summary>
    public async Task Rodar(Task tarefa)
    {
        for (var i = 0; i < 2000 && !tarefa.IsCompleted; i++)
        {
            Relogio.Avancar(TimeSpan.FromMilliseconds(10));
            await Task.Delay(1);
        }
        Assert.True(tarefa.IsCompleted, "a escrita não terminou");
        await tarefa;
        await Tela.EsvaziarAsync();
    }

    public int EscritasDe(string prefixo) =>
        Simulador.Escritas.Count(e => e.StartsWith(prefixo, StringComparison.Ordinal));

    /// <summary>
    /// Critério de aceite: só a senha, as leituras do catálogo, as escritas de áudio do catálogo, o
    /// 241 e o <c>*202*0*</c> (o gerador desliga por segurança quando uma leitura não traz o 202:
    /// história 7a, regra 2). Nenhum 240 nem 23N (história 8c).
    /// </summary>
    public void ConferirSoAudio()
    {
        Assert.DoesNotContain(Simulador.Escritas, e => e.StartsWith("*240*", StringComparison.Ordinal) || e.StartsWith("*23", StringComparison.Ordinal));
        Assert.All(Simulador.Escritas, e =>
            Assert.True(Senha.IsMatch(e) || Catalogo.EhLeitura(e) || Catalogo.EscritaDeAudio(e) is not null
                        || Catalogo.ComandoDeMemoria(e) is not null || e == Catalogo.DesligarGerador, e));
    }
}

/// <summary>O controle de um parâmetro contra o processador simulado (história 5).</summary>
public sealed class ControleParametroViewModelTestes : IDisposable
{
    private readonly Bancada _b = new();

    public void Dispose()
    {
        _b.ConferirSoAudio();
        Assert.Empty(_b.Tela.Erros);
    }

    private ControleParametroViewModel Controle(string codigo, int? indice = 0, GrupoControles? grupo = null) =>
        _b.Fabrica.Criar(Catalogo.ParametroDe(codigo)!, indice, grupo);

    // ----------------------------------------------------------------- critério de aceite

    [Fact]
    public async Task Ganho_arrastado_e_solto_o_simulador_termina_no_ultimo_valor_e_o_espelho_bate()
    {
        await _b.Conectar();
        var c = Controle("081");
        Assert.Equal("+10,0 dB", c.TextoValor);

        for (var i = 0; i < 20; i++)
        {
            c.Arrastando(-10.0m - i * 0.5m);
            Assert.Equal(-10.0m - i * 0.5m, c.ValorMostrado); // o fader mostra o arrasto na hora
            await _b.Andar(TimeSpan.FromMilliseconds(50));
        }
        await _b.Rodar(c.Soltou());

        Assert.Equal("-19.5", _b.Simulador.Valor("081", 0));
        Assert.Equal(-19.5m, _b.Estado.Valor("081", 0)!.Numero);
        Assert.Equal(-19.5m, c.ValorMostrado);
        Assert.Equal("−19,5 dB", c.TextoValor);
        Assert.InRange(_b.EscritasDe("*081*0*"), 2, 4);
        Assert.Null(c.Aviso);
    }

    // ----------------------------------------------------------------- campo

    [Fact]
    public async Task Campo_com_virgula_vale_no_check_e_o_espelho_mostra()
    {
        await _b.Conectar();
        var c = Controle("081");
        c.Expandir();
        c.AbrirCampo();
        Assert.Equal("10,0", c.TextoCampo);

        c.TextoCampo = "-3,0";
        Assert.Equal(0, _b.EscritasDe("*081*")); // até o ✓, nada sai
        await c.Confirmar.ExecutarAsync();
        await _b.Rodar(c.UltimaEscrita);

        Assert.False(c.EditandoCampo);
        Assert.Contains("*081*0*-3.0*", _b.Simulador.Escritas);
        Assert.Equal("−3,0 dB", c.TextoValor);
    }

    [Theory]
    [InlineData("2.5", "*081*0*2.5*")]
    [InlineData("−4,5", "*081*0*-4.5*")]
    [InlineData("+1", "*081*0*1.0*")]
    public async Task Campo_aceita_ponto_menos_verdadeiro_e_mais(string texto, string quadro)
    {
        await _b.Conectar();
        var c = Controle("081");
        c.Expandir();
        c.AbrirCampo();
        c.TextoCampo = texto;

        await c.Confirmar.ExecutarAsync();
        await _b.Rodar(c.UltimaEscrita);

        Assert.Contains(quadro, _b.Simulador.Escritas);
    }

    [Fact]
    public async Task Campo_fora_da_faixa_e_recusado_com_a_faixa_e_nada_sai()
    {
        await _b.Conectar();
        var c = Controle("081");
        c.Expandir();
        c.AbrirCampo();
        c.TextoCampo = "20";

        await c.Confirmar.ExecutarAsync();
        await _b.Andar(TimeSpan.FromMilliseconds(200));

        Assert.Equal("−45,0 a +15,0 dB", c.Faixa);
        Assert.Contains("−45,0 a +15,0 dB", c.ErroCampo);
        Assert.Equal("10,0", c.TextoCampo); // o valor anterior volta
        Assert.True(c.EditandoCampo);
        Assert.Equal(0, _b.EscritasDe("*081*"));
        Assert.Equal("+10,0 dB", c.TextoValor);
    }

    [Theory]
    [InlineData("abc", ControleParametroViewModel.MsgNaoNumero)]
    [InlineData("-3,05", "Use no máximo 1 casa decimal.")]
    public async Task Campo_que_nao_e_numero_da_faixa_e_recusado(string texto, string erro)
    {
        await _b.Conectar();
        var c = Controle("081");
        c.Expandir();
        c.AbrirCampo();
        c.TextoCampo = texto;

        await c.Confirmar.ExecutarAsync();

        Assert.Equal(erro, c.ErroCampo);
        Assert.Equal(0, _b.EscritasDe("*081*"));
    }

    [Fact]
    public async Task Fechar_o_campo_sem_check_descarta()
    {
        await _b.Conectar();
        var c = Controle("081");
        c.Expandir();
        c.AbrirCampo();
        c.TextoCampo = "-3,0";

        await c.FecharCampo.ExecutarAsync();
        await _b.Andar(TimeSpan.FromMilliseconds(200));

        Assert.False(c.EditandoCampo);
        Assert.Equal("", c.TextoCampo);
        Assert.Equal(0, _b.EscritasDe("*081*"));
    }

    // ----------------------------------------------------------------- eco diferente

    [Fact]
    public async Task Eco_diferente_a_tela_mostra_o_valor_do_aparelho()
    {
        _b.Simulador.EcosDiferentes["*081*0*-3.0*"] = "#081#0#-2.9#";
        await _b.Conectar();
        var c = Controle("081");
        c.Expandir();
        c.AbrirCampo();
        c.TextoCampo = "-3,0";

        await c.Confirmar.ExecutarAsync();
        await _b.Rodar(c.UltimaEscrita);

        Assert.Equal(-2.9m, c.ValorMostrado);
        Assert.Equal("−2,9 dB", c.TextoValor);
        Assert.Equal("O aparelho ficou em −2,9 dB", c.Aviso);
    }

    // ----------------------------------------------------------------- − e +

    [Fact]
    public async Task Mais_em_2000_Hz_vai_a_2119_e_menos_a_1888()
    {
        _b.Simulador.Definir("040", 0, "2000");
        await _b.Conectar();
        var c = Controle("040");
        Assert.Equal("10 a 22000 Hz · passo 1/12 de oitava", c.FaixaEPasso);

        await c.Aumentar.ExecutarAsync();
        await _b.Rodar(c.UltimaEscrita);
        Assert.Contains("*040*0*2119*", _b.Simulador.Escritas);
        Assert.Equal("2119 Hz", c.TextoValor);

        Assert.Equal(1888m, c.ValorDoPasso(2000m, -1));
    }

    [Theory]
    [InlineData(10, 1, 11)] // 1/12 de oitava de 10 Hz é menos de 1 Hz: anda 1 Hz
    [InlineData(11, -1, 10)]
    [InlineData(10, -1, 10)] // não passa da faixa
    [InlineData(21000, 1, 22000)]
    [InlineData(1000, 1, 1059)]
    public void Passo_de_frequencia_proporcional_com_no_minimo_1_Hz(int de, int direcao, int para)
    {
        var c = Controle("040");

        Assert.Equal(para, c.ValorDoPasso(de, direcao));
    }

    [Theory]
    [InlineData("081", 2.0, 1, 2.1)]
    [InlineData("081", 15.0, 1, 15.0)]
    [InlineData("050", 42.5, 1, 43.0)]
    [InlineData("073", 720, -1, 719)]
    [InlineData("092", 0.1, -1, 0.1)]
    [InlineData("071", -48.0, -1, -48.0)]
    public void Passos_fixos_dentro_da_faixa(string codigo, double de, int direcao, double para)
    {
        var c = Controle(codigo);

        Assert.Equal((decimal)para, c.ValorDoPasso((decimal)de, direcao));
    }

    [Fact]
    public async Task Segurar_repete_depois_de_0_4_s_e_acelera_depois_de_2_s()
    {
        await _b.Conectar();
        var c = Controle("081"); // começa em +10,0
        c.Expandir();

        c.ComecarSegurar(-1);
        await _b.Tela.EsvaziarAsync();
        Assert.Equal(9.9m, c.ValorMostrado); // o toque já dá um passo

        await _b.Andar(TimeSpan.FromMilliseconds(390), passoMs: 5);
        Assert.Equal(9.9m, c.ValorMostrado); // ainda não repetiu

        await _b.Andar(TimeSpan.FromMilliseconds(610), passoMs: 5); // t = 1,0 s
        var em1s = c.ValorMostrado!.Value;
        Assert.InRange(em1s, 9.1m, 9.3m); // ~7 repetições lentas
        await _b.Andar(TimeSpan.FromSeconds(1), passoMs: 5); // t = 2,0 s
        var em2s = c.ValorMostrado!.Value;
        await _b.Andar(TimeSpan.FromSeconds(1), passoMs: 5); // t = 3,0 s
        var em3s = c.ValorMostrado!.Value;
        c.PararSegurar();

        var lento = em1s - em2s;
        var rapido = em2s - em3s;
        Assert.InRange(lento, 0.9m, 1.1m);
        Assert.True(rapido > 2 * lento, $"lento {lento}, rápido {rapido}");

        // Soltou: para de repetir, e o último valor vai.
        var parado = c.ValorMostrado;
        await _b.Rodar(c.UltimaEscrita);
        await _b.Andar(TimeSpan.FromMilliseconds(500));
        Assert.Equal(parado, c.ValorMostrado);
        Assert.Equal(parado, _b.Estado.Valor("081", 0)!.Numero);
    }

    // ----------------------------------------------------------------- escolhas diretas

    [Fact]
    public async Task Tipo_do_filtro_e_escolha_direta_com_OFF()
    {
        await _b.Conectar();
        var c = Controle("042");

        Assert.True(c.EhEscolha);
        Assert.False(c.TemPasso);
        Assert.Equal("OFF", c.Opcoes[0].Rotulo);
        Assert.Equal(16, c.Opcoes.Count);

        await _b.Rodar(c.Escolher(c.Opcoes.Single(o => o.Rotulo == "LR24")));

        Assert.Contains("*042*0*3*", _b.Simulador.Escritas);
        Assert.Equal("LR24", c.TextoValor);
        Assert.Equal("LR24", c.OpcaoEscolhida!.Rotulo);
    }

    [Fact]
    public async Task Fase_e_rota_sao_escolhas_diretas()
    {
        await _b.Conectar();
        var fase = Controle("060", 3);
        var rota = Controle("010", 1);

        Assert.Equal(["0°", "180°"], fase.Opcoes.Select(o => o.Rotulo));
        await _b.Rodar(fase.Escolher(fase.Opcoes[1]));
        await _b.Rodar(rota.Escolher(rota.Opcoes.Single(o => o.Rotulo == "A+B")));

        Assert.Contains("*060*3*180*", _b.Simulador.Escritas);
        Assert.Contains("*010*1*a+b*", _b.Simulador.Escritas);
        Assert.Equal("180°", fase.TextoValor);
        Assert.Equal("A+B", rota.TextoValor);
    }

    [Fact]
    public async Task Limitador_tem_OFF_direto_e_volta_ao_ultimo_limiar()
    {
        _b.Simulador.Definir("071", 0, "-10.0");
        await _b.Conectar();
        var c = Controle("071");
        Assert.True(c.TemOff);
        Assert.Equal("−48,0 a 0,0 dB ou OFF", c.Faixa);

        await c.AlternarOff.ExecutarAsync();
        await _b.Rodar(c.UltimaEscrita);
        Assert.Contains("*071*0*0.5*", _b.Simulador.Escritas);
        Assert.True(c.EmOff);
        Assert.Equal("OFF", c.TextoValor);

        await _b.Andar(TimeSpan.FromMilliseconds(400));
        await c.AlternarOff.ExecutarAsync();
        await _b.Rodar(c.UltimaEscrita);
        Assert.Equal("*071*0*-10.0*", _b.Simulador.Escritas[^1]);
        Assert.Equal("−10,0 dB", c.TextoValor);
    }

    // ----------------------------------------------------------------- travas

    [Fact]
    public async Task Com_a_chave_desligada_fica_travado_e_nada_sai()
    {
        await _b.Conectar();
        var c = Controle("081");
        c.Expandir();

        var ia = await _b.Chaves.DefinirAsync(Chave.AjustesAudio, false, OrigemComando.Ia);
        Assert.False(ia.Ok);
        await _b.Chaves.DefinirAsync(Chave.AjustesAudio, false, OrigemComando.Tela);
        await _b.Tela.EsvaziarAsync();

        Assert.True(c.Travado);
        Assert.Equal("Chave desligada", c.MotivoTrava);
        await c.Aumentar.ExecutarAsync();
        c.Arrastando(-3.0m);
        c.ComecarSegurar(1);
        c.AbrirCampo();
        await _b.Andar(TimeSpan.FromSeconds(1));
        Assert.Equal(0, _b.EscritasDe("*081*"));
        Assert.False(c.EditandoCampo);
        Assert.Equal("Chave desligada", c.Aviso);

        await _b.Chaves.DefinirAsync(Chave.AjustesAudio, true, OrigemComando.Tela);
        await _b.Tela.EsvaziarAsync();
        Assert.False(c.Travado);
        Assert.Null(c.MotivoTrava);
    }

    [Fact]
    public async Task Firmware_R004_fica_travado_em_modo_so_leitura()
    {
        _b.Simulador.Firmware = "R004";
        await _b.Conectar();
        var c = Controle("081");

        Assert.True(c.Travado);
        Assert.Equal("Modo só leitura", c.MotivoTrava);
        Assert.Equal("+10,0 dB", c.TextoValor); // a leitura continua valendo
        await c.Aumentar.ExecutarAsync();
        await _b.Andar(TimeSpan.FromMilliseconds(200));
        Assert.Equal(0, _b.EscritasDe("*081*"));
    }

    [Fact]
    public async Task Sem_conexao_fica_travado_e_sem_valor()
    {
        var c = Controle("081");
        Assert.Equal("Sem conexão", c.MotivoTrava);
        Assert.Equal("—", c.TextoValor);

        await _b.Conectar();
        Assert.False(c.Travado);
        await _b.Sessao.DesconectarAsync();
        await _b.Tela.EsvaziarAsync();

        Assert.Equal("Sem conexão", c.MotivoTrava);
        Assert.Equal("—", c.TextoValor); // nunca mostra valor que não veio do aparelho
    }

    [Fact]
    public async Task Leitura_que_falhou_diz_que_o_app_ainda_nao_leu()
    {
        var falhar = 1;
        _b.Leitor.Avancou += _ =>
        {
            if (Interlocked.Exchange(ref falhar, 0) == 1)
                throw new InvalidOperationException("falha de teste");
        };
        await _b.Chaves.Carregada;
        await _b.Sessao.ConectarAsync();
        // A leitura que falha ainda passa pela regra 2 do gerador: o *202*0* de segurança espera
        // o eco no relógio falso, que precisa andar.
        for (var i = 0; i < 2000 && !(_b.Leitura.Iniciadas == 1 && !_b.Leitura.Lendo); i++)
        {
            _b.Relogio.Avancar(TimeSpan.FromMilliseconds(10));
            await Task.Delay(1);
        }
        Assert.True(_b.Leitura.Iniciadas == 1 && !_b.Leitura.Lendo);
        await _b.Tela.EsvaziarAsync();
        var c = Controle("081");

        Assert.False(_b.Estado.Pronto);
        Assert.False(_b.Estado.Lendo);
        Assert.Equal(ControleParametroViewModel.MsgNaoLeu, c.MotivoTrava);
    }

    [Fact]
    public async Task Queda_no_meio_do_arrasto_volta_ao_valor_do_aparelho()
    {
        await _b.Conectar();
        var c = Controle("081");
        c.Arrastando(-3.0m);
        Assert.Equal(-3.0m, c.ValorMostrado);

        await _b.Sessao.DesconectarAsync();
        await _b.Andar(TimeSpan.FromMilliseconds(100));

        Assert.Equal("Sem conexão", c.MotivoTrava);
        Assert.Null(c.ValorMostrado); // nada de valor não confirmado na tela
        Assert.Equal("—", c.TextoValor);
    }

    [Fact]
    public async Task Chave_desligada_no_meio_do_arrasto_mostra_o_valor_do_aparelho_e_encerra_o_arrasto()
    {
        _b.Simulador.Silenciar.Add("*081*0*-3.0*"); // a escrita fica sem confirmação
        await _b.Conectar();
        var c = Controle("081");
        c.Arrastando(-3.0m);
        Assert.Equal(-3.0m, c.ValorMostrado);

        await _b.Chaves.DefinirAsync(Chave.AjustesAudio, false, OrigemComando.Tela);
        await _b.Tela.EsvaziarAsync();

        Assert.Equal("Chave desligada", c.MotivoTrava);
        Assert.Equal(_b.Estado.Valor("081", 0)!.Numero, c.ValorMostrado);
        Assert.Equal(10.0m, c.ValorMostrado);

        // Religada, a tela segue o aparelho (o arrasto acabou, nada fica preso).
        await _b.Chaves.DefinirAsync(Chave.AjustesAudio, true, OrigemComando.Tela);
        await _b.Rodar(c.UltimaEscrita);
        Assert.Equal(_b.Estado.Valor("081", 0)!.Numero, c.ValorMostrado);
    }

    // ----------------------------------------------------------------- valores especiais

    [Theory]
    [InlineData("0.05")]
    [InlineData("0.01")]
    public async Task Ataque_em_auto_mostra_auto(string noFio)
    {
        _b.Simulador.Definir("072", 0, noFio);
        await _b.Conectar();
        var c = Controle("072");

        Assert.Equal("auto", c.TextoValor);
    }

    [Fact]
    public async Task Valor_do_aparelho_fora_da_faixa_mantem_a_marca()
    {
        _b.Simulador.Definir("072", 0, "150.0");
        await _b.Conectar();
        var c = Controle("072");

        Assert.Contains("fora da faixa", c.TextoValor);
    }

    // ----------------------------------------------------------------- recolhido e aberto

    [Fact]
    public async Task Comeca_recolhido_e_tocar_no_valor_expande_sem_abrir_o_campo()
    {
        await _b.Conectar();
        var c = Controle("081");

        Assert.False(c.Expandido);
        Assert.Equal("⌄", c.IndicadorExpandir);
        Assert.Equal("ganho", c.Nome);
        Assert.Equal("−45,0 a +15,0 dB · passo 0,1 dB", c.FaixaEPasso);
        Assert.Equal("+10,0 dB", c.TextoValor);

        await c.TocarNoValor.ExecutarAsync();

        Assert.True(c.Expandido);
        Assert.False(c.EditandoCampo);
        Assert.Equal("⌃", c.IndicadorExpandir);
    }

    [Fact]
    public async Task Com_o_controle_aberto_tocar_no_valor_abre_o_campo()
    {
        await _b.Conectar();
        var c = Controle("081");
        await c.TocarNoValor.ExecutarAsync();

        await c.TocarNoValor.ExecutarAsync();

        Assert.True(c.EditandoCampo);
        Assert.Equal("10,0", c.TextoCampo);
        Assert.Equal(0, _b.EscritasDe("*081*"));
    }

    [Fact]
    public async Task O_indicador_alterna_e_recolher_descarta_o_campo()
    {
        await _b.Conectar();
        var c = Controle("081");

        await c.AlternarExpandido.ExecutarAsync();
        Assert.True(c.Expandido);
        c.AbrirCampo();
        c.TextoCampo = "-3,0";
        await c.AlternarExpandido.ExecutarAsync();

        Assert.False(c.Expandido);
        Assert.False(c.EditandoCampo);
        await _b.Andar(TimeSpan.FromMilliseconds(200));
        Assert.Equal(0, _b.EscritasDe("*081*"));
    }

    [Fact]
    public async Task Abrir_um_controle_recolhe_o_outro_do_mesmo_grupo()
    {
        await _b.Conectar();
        var grupo = new GrupoControles();
        var outroGrupo = new GrupoControles();
        var ganho = Controle("081", 0, grupo);
        var hpf = Controle("040", 0, grupo);
        var fora = Controle("081", 1, outroGrupo);

        await ganho.TocarNoValor.ExecutarAsync();
        ganho.AbrirCampo();
        await fora.TocarNoValor.ExecutarAsync();
        Assert.True(ganho.Expandido);
        Assert.True(fora.Expandido);

        await hpf.TocarNoValor.ExecutarAsync();

        Assert.True(hpf.Expandido);
        Assert.False(ganho.Expandido);
        Assert.False(ganho.EditandoCampo);
        Assert.True(fora.Expandido); // outro grupo
        Assert.Same(hpf, grupo.Aberto);

        hpf.Recolher();
        Assert.Null(grupo.Aberto);
        hpf.Dispose();
        ganho.Dispose();
        fora.Dispose();
    }
}
