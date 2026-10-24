using BgsControle.Nucleo.Escrita;
using BgsControle.Nucleo.Player;
using BgsControle.Nucleo.Setups;

namespace BgsControle.Nucleo.Testes;

/// <summary>
/// As duas decisões do Denis de 24/10/2026 (história 11) no núcleo:
/// <list type="number">
/// <item>sem Setup em uso, os links 1–2 e 3–4 ficam desligados (na dúvida, na troca, em Seguir sem Setup);
/// desistir da troca devolve os links do Setup; um Setup criado do aparelho nasce com eles desligados;</item>
/// <item>a queda do BLE não tira a playlist do Player: ele continua tocando a da memória que estava em uso,
/// até uma memória do celular entrar em uso depois de reconectar.</item>
/// </list>
/// </summary>
public sealed partial class SetupsTestes
{
    private (bool L12, bool L34) Links => (_link.Ligado(ParCanais.Saidas12), _link.Ligado(ParCanais.Saidas34));

    // ----------------------------------------------------------------- decisão 2: links sem Setup

    [Fact]
    public async Task Casa_2_1_com_o_Link_3_4_ligado_na_duvida_os_links_desligam_e_escolher_o_Casa_religa()
    {
        await Conectar();
        Assert.Equal((false, true), Links); // o Casa 2.1 conferido: o Link 3–4 ligado

        // A próxima conexão não bate: dúvida, sem Setup em uso.
        await _sessao.DesconectarAsync();
        MexerNaCalibracao();
        await Conectar();
        Assert.Equal(EstadoConferencia.Duvida, _setups.Estado);

        Assert.Equal((false, false), Links);
        Assert.Equal(["CH1", "CH2", "CH3", "CH4"], _setups.SaidasEmUso.Select(s => s.Nome));

        Assert.True((await Rodar(_setups.EscolherAsync(Casa))).Ok);

        Assert.Equal((false, true), Links); // como o Setup guarda
        Assert.Equal(["JBL L", "JBL R", "Sub"], _setups.SaidasEmUso.Select(s => s.Nome));
    }

    [Fact]
    public async Task Duvida_interrompida_tambem_deixa_os_links_desligados()
    {
        await EntrarNaDuvida();
        Assert.Equal((false, false), Links);

        // A conexão cai com a dúvida aberta; o link guardado volta ligado (um arquivo antigo, por exemplo).
        await _sessao.DesconectarAsync();
        Assert.Null(await _link.DefinirEstadoAsync(false, true));
        await Conectar();

        Assert.Equal(EstadoConferencia.Duvida, _setups.Estado);
        Assert.Equal((false, false), Links);
    }

    [Fact]
    public async Task Trocar_de_Setup_desliga_os_links_e_desistir_devolve_os_do_Setup_em_uso()
    {
        await Conectar();
        Assert.Equal((false, true), Links);

        Assert.True((await Rodar(_setups.TrocarAsync())).Ok);
        Assert.Equal((false, false), Links);

        Assert.True((await Rodar(_setups.DesistirAsync())).Ok);
        Assert.Equal((false, true), Links);
        Assert.Equal(Casa, _setups.SetupEmUso!.Id);
    }

    [Fact]
    public async Task Seguir_sem_Setup_no_Casa_2_1_deixa_os_links_desligados_e_quatro_saidas()
    {
        await Conectar();
        Assert.True((await Rodar(_setups.TrocarAsync())).Ok);

        Assert.True((await Rodar(_setups.SeguirSemSetupAsync())).Ok);

        Assert.Null(_setups.SetupEmUso);
        Assert.Equal((false, false), Links);
        Assert.Equal(["CH1", "CH2", "CH3", "CH4"], _setups.SaidasEmUso.Select(s => s.Nome));
    }

    [Fact]
    public async Task Desistir_da_troca_sem_Setup_em_uso_antes_deixa_os_links_desligados()
    {
        await EntrarNaDuvida();
        Assert.True((await Rodar(_setups.SeguirSemSetupAsync())).Ok);
        Assert.True((await Rodar(_setups.TrocarAsync())).Ok);

        Assert.True((await Rodar(_setups.DesistirAsync())).Ok);

        Assert.Null(_setups.SetupEmUso);
        Assert.Equal((false, false), Links);
    }

    [Fact]
    public async Task Criar_Setup_novo_na_duvida_nasce_com_Link12_e_Link34_desligados()
    {
        await EntrarNaDuvida();

        Assert.True((await Rodar(_setups.CriarSetupAsync("Carro"))).Ok);

        var carro = _setups.SetupEmUso!;
        Assert.False(carro.Link12);
        Assert.False(carro.Link34);
        Assert.Equal((false, false), Links);

        // Na próxima conexão o Carro confere e o link dele (desligado) vale.
        await Reconectar();
        Assert.Equal(carro.Id, _setups.SetupEmUso!.Id);
        Assert.Equal((false, false), Links);
    }

    [Fact]
    public async Task Trocar_sem_conseguir_guardar_os_links_desligados_diz_na_frase()
    {
        await Conectar();
        _guardadoLink.FalharAoGravar = true;

        var r = await Rodar(_setups.TrocarAsync());

        Assert.True(r.Ok, r.Motivo);
        Assert.StartsWith("Não consegui desligar os links: ", r.Mensagem);
        Assert.Equal(r.Mensagem, _setups.Aviso);
        _guardadoLink.FalharAoGravar = false;
        Assert.True((await Rodar(_setups.DesistirAsync())).Ok);
    }

    // ----------------------------------------------------------------- decisão 1: o Player na queda

    /// <summary>O reprodutor do teste: toca na hora e conta as paradas.</summary>
    private sealed class Reprodutor : IReprodutor
    {
        public string? Tocando { get; private set; }

        public int Paradas { get; private set; }

        public event Action<string>? Terminou;

        public Task<bool> TocarAsync(string uri)
        {
            Tocando = uri;
            return Task.FromResult(true);
        }

        public Task PausarAsync() => Task.CompletedTask;

        public Task<bool> ContinuarAsync() => Task.FromResult(true);

        public event Action<string>? Pausou;

        public event Action<string>? Falhou;

        public void PausarSozinho()
        {
            if (Tocando is { } uri)
                Pausou?.Invoke(uri);
        }

        public void Falhar()
        {
            if (Tocando is { } uri)
            {
                Tocando = null;
                Falhou?.Invoke(uri);
            }
        }

        public Task PararAsync()
        {
            Tocando = null;
            Paradas++;
            return Task.CompletedTask;
        }

        public void Terminar()
        {
            if (Tocando is { } uri)
                Terminou?.Invoke(uri);
        }
    }

    private static ArquivoAudio Faixa(int i) => new($"content://musica/{i}", $"Faixa {i}", "Music", TimeSpan.FromSeconds(180));

    /// <summary>A FILME em uso com 3 faixas e o Player tocando a faixa 2.</summary>
    private async Task<(Player.Player Player, Reprodutor Reprodutor)> FilmeTocandoAFaixa2()
    {
        var reprodutor = new Reprodutor();
        var player = new Player.Player(_playlists, _setups, reprodutor, _log);
        await Conectar();
        await AndarAte(() => player.Memoria == Filme);
        Assert.True((await _playlists.AdicionarAsync(Filme, [Faixa(1), Faixa(2), Faixa(3)])).Ok);
        await player.TocarFaixaAsync(_playlists.Playlist(Filme)[1].Id);
        Assert.Equal("content://musica/2", reprodutor.Tocando);
        return (player, reprodutor);
    }

    /// <summary>A queda sem pedido (o simulador derruba a conexão): o Player fica com a playlist.</summary>
    private async Task Cair(Player.Player player)
    {
        _simulador.Derrubar();
        await AndarAte(() => _sessao.Estado != EstadoSessao.Conectado && player.Retida);
    }

    [Fact]
    public async Task BLE_cai_tocando_a_faixa_2_o_Player_continua_tocando_e_fica_na_playlist_da_FILME()
    {
        var (player, reprodutor) = await FilmeTocandoAFaixa2();

        await Cair(player);

        Assert.Equal(Filme, player.Memoria);
        Assert.Equal(EstadoPlayer.Tocando, player.Estado);
        Assert.Equal("content://musica/2", reprodutor.Tocando);
        Assert.Equal(0, reprodutor.Paradas);
        Assert.Equal(3, player.Faixas.Count);

        // Desconectado, os comandos seguem valendo: a próxima faixa e o fim dela.
        await player.ProximaAsync();
        Assert.Equal("content://musica/3", reprodutor.Tocando);
        player.Dispose();
    }

    [Fact]
    public async Task Reconectar_e_conferir_a_mesma_FILME_segue_tocando_sem_parar()
    {
        var (player, reprodutor) = await FilmeTocandoAFaixa2();
        await Cair(player);

        await Conectar();
        Assert.Equal(SomEmUso.DoCelular(Filme), _setups.EmUso);
        await AndarAte(() => !player.Retida);

        Assert.Equal(Filme, player.Memoria);
        Assert.Equal(EstadoPlayer.Tocando, player.Estado);
        Assert.Equal("content://musica/2", reprodutor.Tocando);
        Assert.Equal(0, reprodutor.Paradas);
        player.Dispose();
    }

    [Fact]
    public async Task Reconectar_com_som_diferente_mantem_a_FILME_e_aplicar_outra_memoria_para_e_troca()
    {
        var (player, reprodutor) = await FilmeTocandoAFaixa2();
        Assert.True((await _setups.CriarMemoriaAsync("SHOW")).Ok);
        var show = _setups.MemoriasDe(Casa).Single(m => m.Nome == "SHOW").Id;
        await AndarAte(() => player.Memoria == show); // criar a SHOW (conectado) troca: regra de 22/10
        Assert.True((await Rodar(_setups.AplicarAsync(Filme))).Ok);
        await AndarAte(() => player.Memoria == Filme);
        await player.TocarFaixaAsync(_playlists.Playlist(Filme)[0].Id);

        // Cai; o caráter muda por fora; a reconexão confere "som diferente" (nenhuma memória do celular em uso).
        await Cair(player);
        _simulador.Definir("081", 0, "-3.0");
        await Conectar();
        Assert.Equal(EstadoConferencia.SomDiferente, _setups.Estado);

        Assert.Equal(Filme, player.Memoria);
        Assert.Equal("content://musica/1", reprodutor.Tocando);

        // Uma memória diferente aplicada depois de reconectar: para e mostra a playlist dela.
        Assert.True((await Rodar(_setups.AplicarAsync(show))).Ok);
        await AndarAte(() => player.Memoria == show);

        Assert.False(player.Retida);
        Assert.Equal(EstadoPlayer.Parado, player.Estado);
        Assert.Null(reprodutor.Tocando);
        player.Dispose();
    }

    [Fact]
    public async Task Reconectar_em_duvida_e_escolher_o_Casa_com_a_FILME_segue_tocando()
    {
        var (player, reprodutor) = await FilmeTocandoAFaixa2();
        await Cair(player);

        MexerNaCalibracao();
        await Conectar();
        Assert.Equal(EstadoConferencia.Duvida, _setups.Estado);
        Assert.Equal(Filme, player.Memoria); // a dúvida não troca a playlist

        Assert.True((await Rodar(_setups.EscolherAsync(Casa))).Ok);
        await AndarAte(() => !player.Retida);

        Assert.Equal(Filme, player.Memoria);
        Assert.Equal("content://musica/2", reprodutor.Tocando);
        player.Dispose();
    }

    [Fact]
    public async Task Conectado_carregar_uma_memoria_do_aparelho_ainda_para_o_Player()
    {
        var (player, reprodutor) = await FilmeTocandoAFaixa2();

        Assert.True((await Rodar(_memorias.CarregarAsync(0))).Ok);
        await AndarAte(() => player.Memoria is null);

        Assert.False(player.Retida);
        Assert.Null(reprodutor.Tocando);
        player.Dispose();
    }

    [Fact]
    public async Task Reconectar_e_conferir_outra_memoria_para_e_troca_a_playlist()
    {
        // A SHOW (a ativa) toca e cai; desconectado, a SHOW é apagada: a ativa do Casa volta a ser a FILME.
        var (player, reprodutor) = await FilmeTocandoAFaixa2();
        Assert.True((await _setups.CriarMemoriaAsync("SHOW")).Ok);
        var show = _setups.MemoriasDe(Casa).Single(m => m.Nome == "SHOW").Id;
        await AndarAte(() => player.Memoria == show);
        Assert.True((await _playlists.AdicionarAsync(show, [Faixa(7)])).Ok);
        await player.TocarPausarAsync();
        Assert.Equal("content://musica/7", reprodutor.Tocando);
        await Cair(player);
        Assert.True((await _setups.ApagarMemoriaAsync(show)).Ok);

        await Conectar();
        Assert.Equal(SomEmUso.DoCelular(Filme), _setups.EmUso);
        await AndarAte(() => player.Memoria == Filme);

        Assert.False(player.Retida);
        Assert.Equal(EstadoPlayer.Parado, player.Estado);
        Assert.Null(reprodutor.Tocando);
        Assert.Equal(3, player.Faixas.Count); // a playlist da FILME
        player.Dispose();
    }

    [Fact]
    public async Task Reconectar_e_carregar_uma_memoria_do_aparelho_para_e_fica_sem_playlist()
    {
        var (player, reprodutor) = await FilmeTocandoAFaixa2();
        await Cair(player);
        await Conectar();
        await AndarAte(() => !player.Retida);
        Assert.Equal("content://musica/2", reprodutor.Tocando);

        Assert.True((await Rodar(_memorias.CarregarAsync(0))).Ok);
        await AndarAte(() => player.Memoria is null);

        Assert.False(player.Retida);
        Assert.Equal(EstadoPlayer.Parado, player.Estado);
        Assert.Null(reprodutor.Tocando);
        Assert.Empty(player.Faixas);
        player.Dispose();
    }

    [Fact]
    public async Task Reconectar_em_duvida_mantem_e_Seguir_sem_Setup_para_e_fica_sem_playlist()
    {
        var (player, reprodutor) = await FilmeTocandoAFaixa2();
        await Cair(player);
        MexerNaCalibracao();
        await Conectar();
        Assert.Equal(EstadoConferencia.Duvida, _setups.Estado);
        Assert.True(player.Retida);
        Assert.Equal("content://musica/2", reprodutor.Tocando);

        Assert.True((await Rodar(_setups.SeguirSemSetupAsync())).Ok);
        await AndarAte(() => player.Memoria is null);

        Assert.False(player.Retida);
        Assert.Equal(EstadoPlayer.Parado, player.Estado);
        Assert.Null(reprodutor.Tocando);
        player.Dispose();
    }

    [Fact]
    public async Task Retida_o_fim_da_faixa_segue_para_a_proxima()
    {
        var (player, reprodutor) = await FilmeTocandoAFaixa2();
        await Cair(player);

        reprodutor.Terminar();
        await AndarAte(() => reprodutor.Tocando == "content://musica/3");

        Assert.Equal(EstadoPlayer.Tocando, player.Estado);
        Assert.Equal("Faixa 3", player.Atual!.Nome);
        Assert.True(player.Retida);
        player.Dispose();
    }

    [Fact]
    public async Task Retida_remover_a_faixa_que_toca_para_o_Player()
    {
        var (player, reprodutor) = await FilmeTocandoAFaixa2();
        await Cair(player);

        Assert.True((await _playlists.RemoverAsync(Filme, player.Atual!.Id)).Ok);
        await AndarAte(() => player.Estado == EstadoPlayer.Parado);

        Assert.Null(reprodutor.Tocando);
        Assert.True(player.Retida);
        Assert.Equal(2, player.Faixas.Count);
        player.Dispose();
    }

    [Fact]
    public async Task O_reprodutor_que_pausa_sozinho_deixa_o_Player_Pausado_e_tocar_continua()
    {
        var (player, reprodutor) = await FilmeTocandoAFaixa2();

        reprodutor.PausarSozinho();
        await AndarAte(() => player.Estado == EstadoPlayer.Pausado);
        Assert.Equal("Faixa 2", player.Atual!.Nome);

        await player.TocarPausarAsync();
        Assert.Equal(EstadoPlayer.Tocando, player.Estado);
        player.Dispose();
    }

    [Fact]
    public async Task Uma_faixa_que_falha_no_meio_fica_indisponivel_e_o_Player_segue_para_a_proxima()
    {
        var (player, reprodutor) = await FilmeTocandoAFaixa2();
        var faixa2 = player.Atual!.Id;

        reprodutor.Falhar();
        await AndarAte(() => reprodutor.Tocando == "content://musica/3");

        Assert.True(player.Indisponivel(faixa2));
        Assert.Equal("Faixa 3", player.Atual!.Nome);
        player.Dispose();
    }
}
