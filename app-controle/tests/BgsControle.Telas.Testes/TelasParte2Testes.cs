using BgsControle.Nucleo;
using BgsControle.Nucleo.Escrita;
using BgsControle.Nucleo.Herdado;
using BgsControle.Nucleo.Player;
using BgsControle.Nucleo.Porta;
using BgsControle.Nucleo.Seguranca;
using BgsControle.Nucleo.Uso;

namespace BgsControle.Telas.Testes;

/// <summary>
/// As telas da história 11 montadas como no app (os mesmos singletons do MauiProgram), sobre a
/// <see cref="Bancada"/> com os Setups e o uso compartilhado, a navegação falsa, o reprodutor falso, o som do
/// celular falso e o exportar falso.
/// </summary>
internal sealed class Telas11 : IDisposable
{
    public Telas11(Bancada b)
    {
        B = b;
        var setups = b.Setups!;
        Conexao = new ConexaoViewModel(b.Sessao, b.Log, b.Leitura, b.Tela, b.ArquivosUso, new PreparoFalso(), b.Relogio);
        Salvar = b.CriarSalvarViewModel();
        Setup = b.CriarSetupViewModel();
        Memorias = b.CriarMemoriasViewModel(Salvar);
        Link = b.CriarLinkViewModel();
        Botoes = new BotoesMemoriaViewModel(setups, b.Memorias, Memorias, b.Sessao, b.Tela, b.Relogio);
        Presets = new PresetsViewModel(b.Presets, setups, b.Copia, b.Sessao, b.Chaves, b.Estado, b.Tela, b.Relogio, Exportar);
        Monitoracao = new MonitoracaoViewModel(b.Monitor, b.Tela, b.Relogio, setups, Nav);
        Inicio = new InicioViewModel(setups, b.Memorias, b.Sessao, b.Estado, b.Link, Conexao, Setup, Memorias, Botoes, Nav, b.Tela, b.Relogio, Presets);
        Mesa = new MesaViewModel(setups, b.Memorias, b.Sessao, b.Estado, b.Link, b.Fabrica, Botoes, Nav, b.Tela, b.Relogio, Monitoracao);
        Audio = new AudioViewModel(b.Fabrica, setups, b.Memorias, b.Sessao, b.Estado, b.Link, Link, Salvar, Nav, b.Tela, b.Relogio, b.Chaves, b.Gerador);
        Gerador = b.CriarGeradorViewModel();
        Aparelho = b.CriarAparelhoViewModel();
        Player = new Player(b.Playlists, setups, Reprodutor, b.Log);
        PlayerTela = new PlayerViewModel(Player, b.Playlists, setups, Seletor, b.Tela, b.Relogio, Aparelho, Som);
        Chaves = new ChavesViewModel(b.Chaves, b.Tela, b.Relogio);
        Pedidos = new PedidosConfirmacao(b.Relogio, b.Log);
        Confirmacao = new ConfirmacaoIaViewModel(Pedidos, b.Tela, b.Relogio);
        IaAtiva = b.CriarIaAtivaViewModel();
        Casca = new CascaViewModel(b.Sessao, Conexao, Setup, Memorias, Inicio, Mesa, Audio, Salvar, Link, b.Fabrica, Nav, b.Tela, b.Relogio,
            PlayerTela, IaAtiva, Confirmacao, Gerador);
        Configuracoes = new ConfiguracoesViewModel(Nav, b.Tela, b.Relogio, Aparelho);
    }

    public Bancada B { get; }
    public NavegacaoFalsa Nav { get; } = new();
    public ReprodutorFalso Reprodutor { get; } = new();
    public SeletorFalso Seletor { get; } = new();
    public AudioDoCelularFalso Som { get; } = new();
    public ExportarFalso Exportar { get; } = new();
    public ConexaoViewModel Conexao { get; }
    public SalvarViewModel Salvar { get; }
    public SetupViewModel Setup { get; }
    public MemoriasViewModel Memorias { get; }
    public LinkViewModel Link { get; }
    public BotoesMemoriaViewModel Botoes { get; }
    public PresetsViewModel Presets { get; }
    public MonitoracaoViewModel Monitoracao { get; }
    public InicioViewModel Inicio { get; }
    public MesaViewModel Mesa { get; }
    public AudioViewModel Audio { get; }
    public GeradorViewModel Gerador { get; }
    public AparelhoViewModel Aparelho { get; }
    public Player Player { get; }
    public PlayerViewModel PlayerTela { get; }
    public ChavesViewModel Chaves { get; }
    public PedidosConfirmacao Pedidos { get; }
    public ConfirmacaoIaViewModel Confirmacao { get; }
    public IaAtivaViewModel IaAtiva { get; }
    public CascaViewModel Casca { get; }
    public ConfiguracoesViewModel Configuracoes { get; }

    public void Dispose()
    {
        Casca.Dispose();
        IaAtiva.Dispose();
        Confirmacao.Dispose();
        Chaves.Dispose();
        PlayerTela.Dispose();
        Player.Dispose();
        Aparelho.Dispose();
        Gerador.Dispose();
        Audio.Dispose();
        Mesa.Dispose();
        Inicio.Dispose();
        Monitoracao.Dispose();
        Presets.Dispose();
        Botoes.Dispose();
        Link.Dispose();
        Memorias.Dispose();
        Setup.Dispose();
        Salvar.Dispose();
    }
}

/// <summary>
/// História 11, a matriz da spec contra o simulador: a aba Player com os avisos do topo, a folha de presets do
/// chip, o bloco e a tela do Gerador, a faixa de monitoração por saída do Setup, a confirmação e a IA ativa
/// por cima de tudo, as linhas de Configurações e os links sem Setup nas telas.
/// </summary>
public sealed class TelasParte2Testes : IDisposable
{
    private const int Sub = 2;

    private readonly Bancada _b = new(usoCompartilhado: true, setups: true);
    private readonly Telas11 _t;

    public TelasParte2Testes() => _t = new Telas11(_b);

    public void Dispose()
    {
        _t.Dispose();
        Assert.Empty(_b.Tela.Erros);
    }

    private static string Uri(int i) => $"content://musica/{i}";

    private static ArquivoAudio Arquivo(int i) => new(Uri(i), $"Faixa {i}", "Music/Trilhas", TimeSpan.FromSeconds(187));

    private async Task Conectar()
    {
        await _b.Conectar();
        await Apoio.Esperar(() => _t.Player.Memoria == ConhecimentoHerdado.IdMemoriaFilme);
        await _b.Tela.EsvaziarAsync();
    }

    private async Task FilmeCom(int quantas)
    {
        await Conectar();
        _t.Seletor.Escolhidos = Enumerable.Range(1, quantas).Select(Arquivo).ToArray();
        await _t.PlayerTela.Adicionar.ExecutarAsync();
        await _b.Tela.EsvaziarAsync();
        Assert.Equal(quantas, _t.PlayerTela.Linhas.Count);
    }

    private async Task LigarChave(Chave chave)
    {
        Assert.True((await _b.Chaves.DefinirAsync(chave, true, OrigemComando.Tela)).Ok);
        await _b.Tela.EsvaziarAsync();
    }

    // ----------------------------------------------------------------- a barra e o Player

    [Fact]
    public async Task A_barra_tem_Inicio_Mesa_Audio_e_Player_e_o_Player_guarda_o_proprio_estado()
    {
        await Conectar();

        await _t.Casca.IrPlayer.ExecutarAsync();
        Assert.True(_t.Casca.NoPlayer);
        Assert.Equal(Aba.Player, _t.Casca.Aba);
        Assert.Same(_t.PlayerTela, _t.Casca.Player);

        await _t.Casca.IrMesa.ExecutarAsync();
        Assert.False(_t.Casca.NoPlayer);
        Assert.True(_t.Casca.NaMesa);
    }

    [Fact]
    public async Task Player_na_FILME_com_3_faixas_toca_a_primeira_e_pula_o_arquivo_que_sumiu()
    {
        await FilmeCom(3);

        Assert.Equal("Playlist da FILME", _t.PlayerTela.Titulo);
        Assert.Null(_t.PlayerTela.AvisoTopo);
        await _t.PlayerTela.TocarPausar.ExecutarAsync();
        await _b.Tela.EsvaziarAsync();
        Assert.Equal(Uri(1), _t.Reprodutor.Tocando);
        Assert.True(_t.PlayerTela.Tocando);

        // Arquivo sumiu: a faixa fica apagada com "Arquivo não encontrado" e é pulada.
        await _t.PlayerTela.TocarPausar.ExecutarAsync(); // pausa
        _t.Reprodutor.Ausentes.Add(Uri(2));
        await _t.PlayerTela.Linhas[1].Tocar.ExecutarAsync();
        await _b.Tela.EsvaziarAsync();
        Assert.Equal(Uri(3), _t.Reprodutor.Tocando);
        Assert.True(_t.PlayerTela.Linhas[1].Indisponivel);
        Assert.Equal("Arquivo não encontrado", _t.PlayerTela.Linhas[1].TextoIndisponivel);
    }

    [Fact]
    public async Task Bluetooth_de_audio_desligado_trava_o_Tocar_e_Ligar_manda_o_251_e_o_aviso_some()
    {
        _b.Simulador.Definir(Catalogo.CodigoBluetoothAudio, null, "0");
        await FilmeCom(1);

        Assert.Equal(PlayerViewModel.MsgBluetoothDesligado, _t.PlayerTela.AvisoTopo);
        Assert.Equal(PlayerViewModel.TextoLigar, _t.PlayerTela.TextoAcaoAviso);
        await _t.PlayerTela.TocarPausar.ExecutarAsync();
        Assert.Equal(PlayerViewModel.MsgBluetoothDesligado, _t.PlayerTela.Mensagem);
        Assert.Empty(_t.Reprodutor.Pedidas);

        await _b.Rodar(_t.PlayerTela.AcaoAviso.ExecutarAsync());
        await Apoio.Esperar(() => _t.Aparelho.BluetoothAudioLigado == true);
        await _b.Tela.EsvaziarAsync();

        Assert.Equal("1", _b.Simulador.Valor(Catalogo.CodigoBluetoothAudio));
        Assert.Null(_t.PlayerTela.AvisoTopo);
        await _t.PlayerTela.TocarPausar.ExecutarAsync();
        Assert.Equal(Uri(1), _t.Reprodutor.Tocando);
    }

    [Fact]
    public async Task Som_saindo_pelo_celular_avisa_e_abre_o_Bluetooth_do_Android_sem_travar_o_Tocar()
    {
        await FilmeCom(1);

        _t.Som.SaindoPeloBluetooth = false;
        await _b.Tela.EsvaziarAsync();

        Assert.Equal(PlayerViewModel.MsgSomPeloCelular, _t.PlayerTela.AvisoTopo);
        await _t.PlayerTela.AcaoAviso.ExecutarAsync();
        Assert.Equal(1, _t.Som.AberturasBluetooth);
        await _t.PlayerTela.TocarPausar.ExecutarAsync();
        Assert.Equal(Uri(1), _t.Reprodutor.Tocando);
    }

    [Fact]
    public async Task Musica_sem_acesso_fica_indisponivel_sem_travar_as_outras_e_escolher_de_novo_devolve()
    {
        await FilmeCom(2);
        // O Android negou ler a faixa 1 (a permissão da URI se perdeu): o reprodutor recusa e marca.
        _t.Reprodutor.Ausentes.Add(Uri(1));
        _t.Som.MarcarSemAcesso(Uri(1));
        await _b.Tela.EsvaziarAsync();

        Assert.Equal("Sem acesso a 1 música: adicione de novo.", _t.PlayerTela.AvisoTopo);
        Assert.Equal(PlayerViewModel.TextoAdicionarDeNovo, _t.PlayerTela.TextoAcaoAviso);
        Assert.Null(_t.PlayerTela.TravaTocar);

        // As outras tocam: a 1 é pulada e fica "Sem acesso ao arquivo: adicione de novo".
        await _t.PlayerTela.TocarPausar.ExecutarAsync();
        await _b.Tela.EsvaziarAsync();
        Assert.Equal(Uri(2), _t.Reprodutor.Tocando);
        Assert.Equal(PlayerViewModel.MsgSemAcessoArquivo, _t.PlayerTela.Linhas[0].TextoIndisponivel);

        // Adicionar de novo: o seletor abre; a mesma música volta a ter acesso e não entra duas vezes.
        _t.Seletor.Escolhidos = [Arquivo(1)];
        _t.Reprodutor.Ausentes.Remove(Uri(1));
        var limpezas = _t.Som.Limpezas;
        var aberturas = _t.Seletor.Aberturas;
        await _t.PlayerTela.AcaoAviso.ExecutarAsync();
        await _b.Tela.EsvaziarAsync();

        Assert.Equal(aberturas + 1, _t.Seletor.Aberturas);
        Assert.Equal(limpezas + 1, _t.Som.Limpezas);
        Assert.Equal(2, _t.PlayerTela.Linhas.Count);
        Assert.Equal(PlayerViewModel.MsgAcessoDevolvido, _t.PlayerTela.Mensagem);
        Assert.Null(_t.PlayerTela.AvisoTopo);
        await _t.PlayerTela.Linhas[0].Tocar.ExecutarAsync();
        Assert.Equal(Uri(1), _t.Reprodutor.Tocando);
    }

    [Fact]
    public async Task Seletor_que_nao_abre_diz_o_motivo_e_nada_muda()
    {
        await FilmeCom(1);
        _t.Seletor.Escolhidos = [];
        _t.Seletor.Motivo = "Este celular não tem um seletor de arquivos para escolher músicas.";

        await _t.PlayerTela.Adicionar.ExecutarAsync();
        await _b.Tela.EsvaziarAsync();

        Assert.Equal(_t.Seletor.Motivo, _t.PlayerTela.Mensagem);
        Assert.Single(_t.PlayerTela.Linhas);
    }

    [Fact]
    public async Task Bluetooth_de_audio_desligado_tocar_numa_linha_nao_pede_nada_e_diz_por_que()
    {
        _b.Simulador.Definir(Catalogo.CodigoBluetoothAudio, null, "0");
        await FilmeCom(2);
        // O 251 lido chega à tela por outra thread: a trava tem de estar lá antes do toque.
        await Apoio.Esperar(() => _t.PlayerTela.TravaTocar == PlayerViewModel.MsgBluetoothDesligado);

        await _t.PlayerTela.Linhas[1].Tocar.ExecutarAsync();
        await _b.Tela.EsvaziarAsync();

        Assert.Empty(_t.Reprodutor.Pedidas);
        Assert.Equal(PlayerViewModel.MsgBluetoothDesligado, _t.PlayerTela.Mensagem);
    }

    [Fact]
    public async Task Tocando_quando_a_trava_aparece_TocarPausar_ainda_pausa()
    {
        await FilmeCom(1);
        await _t.PlayerTela.TocarPausar.ExecutarAsync();
        await _b.Tela.EsvaziarAsync();
        Assert.True(_t.PlayerTela.Tocando);

        // O Bluetooth de áudio é lido desligado com a música tocando.
        await _b.Rodar(_t.Aparelho.DefinirBluetoothAudioAsync(false));
        await Apoio.Esperar(() => _t.Aparelho.BluetoothAudioLigado == false);
        await _b.Tela.EsvaziarAsync();
        Assert.Equal(PlayerViewModel.MsgBluetoothDesligado, _t.PlayerTela.TravaTocar);

        await _t.PlayerTela.TocarPausar.ExecutarAsync();
        await _b.Tela.EsvaziarAsync();

        Assert.True(_t.Reprodutor.Pausado);
        Assert.Equal(EstadoPlayer.Pausado, _t.PlayerTela.Estado);
    }

    [Fact]
    public async Task Sem_memoria_do_celular_nao_ha_aviso_do_topo_nem_Tocar()
    {
        _b.Simulador.Definir(Catalogo.CodigoBluetoothAudio, null, "0");
        await Conectar();
        var r = _b.Memorias.CarregarAsync(0);
        await _b.Rodar(r);
        await Apoio.Esperar(() => _t.Player.Memoria is null);
        await _b.Tela.EsvaziarAsync();

        Assert.False(_t.PlayerTela.TemMemoria);
        Assert.Null(_t.PlayerTela.AvisoTopo);
        Assert.Equal("Sem memória do celular em uso, não há playlist. Aplique uma memória do celular.", _t.PlayerTela.TextoAviso);
    }

    // ----------------------------------------------------------------- folha de presets do chip

    [Fact]
    public async Task Chip_de_preset_abre_a_folha_e_escolher_Rock_aplica_so_as_15_bandas_na_FILME()
    {
        await Conectar();
        var bandas = ConhecimentoHerdado.BandasCinema.Select(v => v.Indice == 0 ? v with { NoFio = "+4.0" } : v).ToArray();
        Assert.True((await _b.Presets.CriarAsync("Rock", bandas)).Ok);
        await _b.Tela.EsvaziarAsync();
        Assert.Equal("Cinema", _t.Inicio.NomePreset);

        await _t.Inicio.AbrirPresets.ExecutarAsync();
        Assert.True(_t.Presets.FolhaAberta);
        Assert.Equal("Preset da FILME", _t.Presets.TituloFolha);
        Assert.True(_t.Presets.Item("Cinema")!.EmUso);

        var antes = _b.Simulador.Escritas.Count;
        await _b.Rodar(_t.Presets.Item("Rock")!.Escolher.ExecutarAsync());
        await _b.Tela.EsvaziarAsync();

        Assert.False(_t.Presets.FolhaAberta);
        Assert.Equal("Preset Rock aplicado na FILME.", _t.Presets.Mensagem);
        Assert.Equal("+4.0", _b.Simulador.Valor("020", 0));
        Assert.All(_b.Simulador.Escritas.Skip(antes).Where(e => !Catalogo.EhLeitura(e)),
            e => Assert.StartsWith("020*", e)); // só as 15 bandas
        Assert.Equal("Rock", _t.Inicio.NomePreset); // a FILME passa a lembrar o Rock
    }

    [Fact]
    public async Task Folha_de_presets_com_a_trava_fica_aberta_com_o_motivo_e_nao_escreve_nada()
    {
        await Conectar();
        Assert.True((await _b.Presets.CriarAsync("Rock")).Ok);
        Assert.True((await _b.Chaves.DefinirAsync(Chave.AjustesAudio, false, OrigemComando.Tela)).Ok);
        await _b.Tela.EsvaziarAsync();
        await _t.Inicio.AbrirPresets.ExecutarAsync();
        var antes = _b.Simulador.Escritas.Count;

        await _t.Presets.Item("Rock")!.Escolher.ExecutarAsync();
        await _b.Tela.EsvaziarAsync();

        Assert.True(_t.Presets.FolhaAberta);
        Assert.Equal("Chave desligada", _t.Presets.Mensagem);
        Assert.Equal(antes, _b.Simulador.Escritas.Count);
        Assert.Equal("Cinema", _t.Inicio.NomePreset);
    }

    [Fact]
    public void O_nome_do_arquivo_exportado_troca_os_caracteres_que_nao_valem()
    {
        Assert.Equal("preset-Rock.json", PresetsViewModel.NomeArquivo("Rock"));
        Assert.Equal("preset-a_b_c_d_e_f_g_h_i_j.json", PresetsViewModel.NomeArquivo(@"a/b\c:d*e?f""g<h>i|j"));
        Assert.Equal("preset-Dia_de_sol.json", PresetsViewModel.NomeArquivo("Dia de sol"));
    }

    [Fact]
    public async Task Escolher_o_preset_em_uso_so_fecha_a_folha()
    {
        await Conectar();
        await _t.Inicio.AbrirPresets.ExecutarAsync();
        var antes = _b.Simulador.Escritas.Count;

        await _t.Presets.Item("Cinema")!.Escolher.ExecutarAsync();

        Assert.False(_t.Presets.FolhaAberta);
        Assert.Equal(antes, _b.Simulador.Escritas.Count);
    }

    [Fact]
    public async Task Exportar_o_Cinema_entrega_o_JSON_e_diz_exportado()
    {
        await Conectar();

        await _t.Presets.Item("Cinema")!.Exportar.ExecutarAsync();
        await _b.Tela.EsvaziarAsync();

        var (nome, conteudo) = Assert.Single(_t.Exportar.Exportados);
        Assert.Equal("preset-Cinema.json", nome);
        Assert.Contains("Cinema", conteudo);
        Assert.Equal("Preset Cinema exportado.", _t.Presets.Mensagem);
    }

    // ----------------------------------------------------------------- Gerador

    [Fact]
    public async Task Bloco_Gerador_travado_pela_chave_abre_Chaves_e_com_a_chave_abre_a_tela_Gerador()
    {
        await Conectar();
        var bloco = _t.Audio.Bloco(FuncaoAudio.Gerador);
        Assert.Equal("Chave desligada", bloco.Trava);

        await bloco.Abrir.ExecutarAsync();
        Assert.Equal("chaves", _t.Nav.Pedidos[^1]);

        await LigarChave(Chave.Gerador);

        Assert.Null(bloco.Trava);
        Assert.Equal("Desligado", bloco.TextoResumo);
        await bloco.Abrir.ExecutarAsync();
        Assert.Equal("gerador", _t.Nav.Pedidos[^1]);
    }

    [Fact]
    public async Task Gerador_tom_de_1_kHz_a_menos_20_dB_conta_30_s_e_no_fim_desliga_e_rele()
    {
        await Conectar();
        await LigarChave(Chave.Gerador);
        _t.Gerador.FrequenciaTom = "1000";
        _t.Gerador.GanhoTom = "-20";

        await _b.Rodar(_t.Gerador.Tocar.ExecutarAsync());
        await _b.Tela.EsvaziarAsync();

        Assert.True(_t.Gerador.Tocando);
        Assert.Equal("30 s", _t.Gerador.TextoContagem);
        Assert.True(_b.Simulador.GeradorLigado);
        Assert.StartsWith("Tocando", _t.Audio.Bloco(FuncaoAudio.Gerador).TextoResumo);

        await _b.Andar(TimeSpan.FromSeconds(31), passoMs: 100);
        await Apoio.Esperar(() => !_b.Simulador.GeradorLigado && !_t.Gerador.Tocando);
        await _b.Tela.EsvaziarAsync();

        Assert.False(_t.Gerador.Tocando);
        Assert.Null(_t.Gerador.TextoContagem);
    }

    [Fact]
    public async Task Alarme_do_gerador_sobe_por_cima_de_tudo_na_casca_e_sai_com_a_leitura_do_gerador_desligado()
    {
        _b.Simulador.Firmware = "R004";
        _b.Simulador.Definir("202", null, "1");
        _t.Casca.SincronizarCamadas(forcar: true);

        await _b.Conectar();
        await Apoio.Esperar(() => _t.Gerador.Alarme);
        await _b.Tela.EsvaziarAsync();

        Assert.Equal((false, false, true), _t.Nav.Camadas);
        Assert.True(_t.Casca.AlarmePorCima);

        // Uma leitura com o 202 = 0 tira o alarme.
        _b.Simulador.Definir("202", null, "0");
        await _b.Rodar(_t.Conexao.LerDeNovo.ExecutarAsync());
        await Apoio.Esperar(() => !_t.Gerador.Alarme);
        await _b.Tela.EsvaziarAsync();

        Assert.Equal((false, false, false), _t.Nav.Camadas);
        Assert.False(_t.Casca.AlarmePorCima);
    }

    // ----------------------------------------------------------------- monitoração

    [Fact]
    public async Task Faixa_de_monitoracao_por_saida_do_Setup_e_o_Sub_acende_com_o_CH4()
    {
        await Conectar();

        Assert.Equal(["JBL L", "JBL R", "Sub"], _t.Monitoracao.PorSaida.Select(p => p.Nome));
        Assert.Equal("—", _t.Monitoracao.Voltimetro);
        Assert.Same(_t.Monitoracao, _t.Mesa.Monitoracao);

        _b.Simulador.EmitirClip(8); // só o CH4
        await _b.Tela.EsvaziarAsync();

        Assert.Equal([false, false, true], _t.Monitoracao.PorSaida.Select(p => p.Aceso));
        Assert.Equal("Clip no Sub", _t.Monitoracao.PorSaida[2].TextoLeitor);
        Assert.True(_t.Mesa.Canal(Sub)!.Clip);
        Assert.False(_t.Mesa.Canal(0)!.Clip);

        await _t.Monitoracao.Abrir.ExecutarAsync();
        Assert.Equal("monitoracao", _t.Nav.Pedidos[^1]);
    }

    // ----------------------------------------------------------------- IA por cima de tudo

    [Fact]
    public async Task IA_pede_aplicar_a_FILME_a_folha_sobe_por_cima_com_o_prazo_e_sem_resposta_a_IA_recebe_a_recusa()
    {
        await Conectar();
        _t.Casca.SincronizarCamadas(forcar: true);

        var decisao = _t.Pedidos.PedirAsync(new PedidoConfirmacao(
            "A IA pede para aplicar a memória FILME", ChavesPadrao.De(Chave.AplicarMemoriaCelular).Risco, "Aplicar a FILME"));
        await _b.Tela.EsvaziarAsync();

        Assert.True(_t.Confirmacao.FolhaAberta);
        Assert.Equal("A IA pede para aplicar a memória FILME", _t.Confirmacao.Titulo);
        Assert.Equal("Troca todo o som de uma vez.", _t.Confirmacao.Risco);
        Assert.Equal("Sem resposta em 60 s, a IA recebe a recusa.", _t.Confirmacao.TextoPrazo);
        Assert.Equal((false, true, false), _t.Nav.Camadas);
        Assert.True(_t.Casca.ConfirmacaoIaPorCima);

        await _b.Andar(TimeSpan.FromSeconds(15), passoMs: 100);
        Assert.InRange(_t.Confirmacao.SegundosRestantes!.Value, 44, 46);

        await _b.Andar(TimeSpan.FromSeconds(46), passoMs: 100);
        Assert.True(decisao.IsCompleted);
        Assert.Equal(DecisaoConfirmacao.SemResposta, await decisao);
        await _b.Tela.EsvaziarAsync();
        Assert.False(_t.Confirmacao.FolhaAberta);
        Assert.Null(_t.Confirmacao.TextoPrazo);
        Assert.Equal((false, false, false), _t.Nav.Camadas);
    }

    [Fact]
    public async Task IA_ativa_abre_sozinha_e_PARAR_IA_fecha_e_volta()
    {
        await Conectar();
        _t.Casca.SincronizarCamadas(forcar: true);

        Assert.True(_b.Uso!.IniciarTarefa("IA ajustando o EQ da FILME").Ok);
        await _b.Tela.EsvaziarAsync();

        Assert.Equal((true, false, false), _t.Nav.Camadas);
        Assert.Equal("IA ajustando o EQ da FILME", _t.IaAtiva.Faixa);
        Assert.Equal(IaAtivaViewModel.MsgControlesTravados, _t.IaAtiva.AvisoControles);

        await _t.IaAtiva.PararIa.ExecutarAsync();
        await _b.Tela.EsvaziarAsync();

        Assert.Equal((false, false, false), _t.Nav.Camadas);
        Assert.False(_t.Casca.IaAtivaPorCima);
    }

    [Fact]
    public async Task IA_ativa_no_modo_pausar_ao_toque_um_toque_na_Mesa_pausa_e_mostra_a_faixa_da_pausa()
    {
        await Conectar();
        Assert.True((await _b.Uso!.DefinirModoAsync(ModoToque.PausarIa, OrigemComando.Tela)).Ok);
        Assert.True(_b.Uso.IniciarTarefa("IA ajustando o EQ da FILME").Ok);
        await _b.Tela.EsvaziarAsync();

        var ganho = _t.Mesa.Canal(Sub)!.Ganho;
        ganho.AbrirCampo();
        ganho.TextoCampo = "5";
        await _b.Rodar(ganho.Confirmar.ExecutarAsync());
        await _b.Rodar(ganho.UltimaEscrita);
        await _b.Tela.EsvaziarAsync();

        Assert.True(_t.IaAtiva.Pausada);
        Assert.Equal("IA pausada · retoma após 10 s sem toques", _t.IaAtiva.Faixa);
        Assert.Equal("+5.0", _b.Simulador.Valor("081", 2));
    }

    // ----------------------------------------------------------------- Configurações

    [Fact]
    public async Task Configuracoes_tem_as_linhas_completas_e_o_Avancado_recolhido_com_os_resets_travados_pela_chave()
    {
        await Conectar();
        var c = _t.Configuracoes;

        foreach (var cmd in new[] { c.AbrirSetups, c.AbrirMemorias, c.AbrirPresets, c.AbrirSenha, c.AbrirMonitoracao, c.AbrirChaves, c.AbrirIaPelaRede, c.AbrirHistorico })
            await cmd.ExecutarAsync();

        Assert.Equal(["setups", "memorias", "presets", "senha", "monitoracao", "chaves", "ia-pela-rede", "historico"], _t.Nav.Pedidos.TakeLast(8));
        Assert.False(c.AvancadoAberto);
        await c.AlternarAvancado.ExecutarAsync();
        Assert.True(c.AvancadoAberto);
        Assert.Same(_t.Aparelho, c.Aparelho);
        Assert.Equal("Chave desligada", c.Aparelho!.MotivoTravaResetAudio);
        Assert.Equal("Chave desligada", c.Aparelho.MotivoTravaResetFabrica);
        Assert.Equal("Chave desligada", c.Aparelho.MotivoTravaSenha);
    }

    [Fact]
    public async Task Sair_da_tela_da_senha_esquece_as_senhas_digitadas()
    {
        await Conectar();
        await LigarChave(Chave.TrocaSenha);
        _t.Aparelho.SenhaAtual = "AB12";
        _t.Aparelho.SenhaNova = "CD34";
        _t.Aparelho.SenhaNovaConfirmacao = "CD34";
        await _t.Aparelho.TrocarSenha.ExecutarAsync();
        Assert.True(_t.Aparelho.FolhaAberta);

        _t.Aparelho.EsquecerSenhasDigitadas();

        Assert.Equal("", _t.Aparelho.SenhaAtual);
        Assert.Equal("", _t.Aparelho.SenhaNova);
        Assert.Equal("", _t.Aparelho.SenhaNovaConfirmacao);
        Assert.False(_t.Aparelho.FolhaAberta);
    }

    [Fact]
    public async Task Reset_de_audio_com_a_chave_abre_a_folha_do_risco_e_cancelar_nao_manda_nada()
    {
        await Conectar();
        await LigarChave(Chave.ResetAudio);
        var antes = _b.Simulador.Escritas.Count;

        await _t.Aparelho.ResetAudio.ExecutarAsync();

        Assert.True(_t.Aparelho.FolhaAberta);
        Assert.Equal("O estado resultante é imprevisível.", _t.Aparelho.FolhaAviso);
        await _t.Aparelho.CancelarFolha.ExecutarAsync();
        Assert.False(_t.Aparelho.FolhaAberta);
        Assert.Equal(antes, _b.Simulador.Escritas.Count);
    }

    // ----------------------------------------------------------------- links sem Setup nas telas

    [Fact]
    public async Task Seguir_sem_Setup_no_Casa_2_1_mostra_CH1_a_CH4_e_desistir_da_troca_volta_os_links_do_Setup()
    {
        await Conectar();

        // Desistir da troca: os links do Casa 2.1 voltam (Link 3–4 ligado, três saídas).
        await _b.Rodar(_t.Inicio.TrocarSetup.ExecutarAsync());
        await _b.Tela.EsvaziarAsync();
        Assert.False(_b.Link.Ligado(ParCanais.Saidas34));
        await _b.Rodar(_t.Setup.Desistir.ExecutarAsync());
        await _b.Tela.EsvaziarAsync();
        Assert.True(_b.Link.Ligado(ParCanais.Saidas34));
        Assert.Equal(["JBL L", "JBL R", "Sub"], _t.Inicio.CartoesSaida.Select(c => c.Nome));

        // Seguir sem Setup: os links ficam desligados, quatro saídas.
        await _b.Rodar(_t.Inicio.TrocarSetup.ExecutarAsync());
        await _b.Rodar(_t.Setup.SeguirSemSetup.ExecutarAsync());
        await _b.Tela.EsvaziarAsync();

        Assert.False(_b.Link.Ligado(ParCanais.Saidas12));
        Assert.False(_b.Link.Ligado(ParCanais.Saidas34));
        Assert.Equal(["CH1", "CH2", "CH3", "CH4"], _t.Inicio.CartoesSaida.Select(c => c.Nome));
        Assert.Equal(["CH1", "CH2", "CH3", "CH4"], _t.Monitoracao.PorSaida.Select(p => p.Nome));
    }
}
