using System.Text.RegularExpressions;
using BgsControle.Nucleo;
using BgsControle.Nucleo.Herdado;
using BgsControle.Nucleo.Player;
using BgsControle.Nucleo.Setups;

namespace BgsControle.Telas.Testes;

/// <summary>
/// O reprodutor falso: toca na hora, recusa as URIs de <see cref="Ausentes"/> (o arquivo sumiu) e
/// termina a faixa atual quando o teste manda (<see cref="Terminar"/>).
/// </summary>
internal sealed class ReprodutorFalso : IReprodutor
{
    private readonly object _trava = new();
    private string? _tocando;
    private bool _pausado;

    public HashSet<string> Ausentes { get; } = [];

    public List<string> Pedidas { get; } = [];

    public int Paradas { get; private set; }

    /// <summary>A URI que está tocando (ou pausada), ou null.</summary>
    public string? Tocando
    {
        get
        {
            lock (_trava)
                return _tocando;
        }
    }

    public bool Pausado
    {
        get
        {
            lock (_trava)
                return _pausado;
        }
    }

    public event Action<string>? Terminou;

    public Task<bool> TocarAsync(string uri)
    {
        lock (_trava)
        {
            Pedidas.Add(uri);
            _pausado = false;
            if (Ausentes.Contains(uri))
            {
                _tocando = null;
                return Task.FromResult(false);
            }
            _tocando = uri;
            return Task.FromResult(true);
        }
    }

    public Task PausarAsync()
    {
        lock (_trava)
            _pausado = true;
        return Task.CompletedTask;
    }

    public Task ContinuarAsync()
    {
        lock (_trava)
            _pausado = false;
        return Task.CompletedTask;
    }

    public Task PararAsync()
    {
        lock (_trava)
        {
            _tocando = null;
            _pausado = false;
            Paradas++;
        }
        return Task.CompletedTask;
    }

    /// <summary>A faixa atual chega ao fim sozinha.</summary>
    public void Terminar()
    {
        string? uri;
        lock (_trava)
        {
            uri = _tocando;
            _tocando = null;
        }
        if (uri is not null)
            Terminou?.Invoke(uri);
    }
}

/// <summary>O seletor do Android falso: devolve os arquivos de <see cref="Escolhidos"/>, na ordem.</summary>
internal sealed class SeletorFalso : ISeletorAudio
{
    public IReadOnlyList<ArquivoAudio> Escolhidos { get; set; } = [];

    /// <summary>Se definido, o seletor fica aberto até o teste completar o portão.</summary>
    public TaskCompletionSource? Portao { get; set; }

    private int _aberturas;

    public int Aberturas => Volatile.Read(ref _aberturas);

    public async Task<IReadOnlyList<ArquivoAudio>> EscolherAsync()
    {
        Interlocked.Increment(ref _aberturas);
        if (Portao is { } portao)
            await portao.Task;
        return Escolhidos;
    }
}

/// <summary>
/// As histórias 8b e 8d nas telas, contra o processador simulado: o <see cref="PlayerViewModel"/> com o
/// <see cref="Player"/>, o <see cref="GerentePlaylists"/> e a memória do celular em uso de verdade (o
/// <see cref="GerenteSetups"/>). Ao conectar, a conferência põe a FILME herdada em uso.
/// </summary>
public sealed class PlayerViewModelTestes : IDisposable
{
    private static readonly Regex Senha = new(@"^\*000\*[0-9A-Z]{4}\*$");

    private readonly Bancada _b = new(setups: true);
    private readonly ReprodutorFalso _reprodutor = new();
    private readonly SeletorFalso _seletor = new();
    private readonly GerentePlaylists _playlists;
    private readonly Player _player;
    private readonly PlayerViewModel _vm;
    private readonly List<string> _carregadas = [];

    public PlayerViewModelTestes()
    {
        _playlists = _b.Playlists;
        _player = new Player(_playlists, _b.Setups!, _reprodutor, _b.Log);
        _vm = new PlayerViewModel(_player, _playlists, _b.Setups!, _seletor, _b.Tela, _b.Relogio);
    }

    /// <summary>
    /// Critério de aceite: nenhuma escrita do Player chegou ao aparelho. Fora a senha e as leituras, só
    /// os carregar de memória do aparelho que o próprio teste pediu.
    /// </summary>
    public void Dispose()
    {
        _vm.Dispose();
        _player.Dispose();
        var escritas = _b.Simulador.Escritas.Where(e => !Senha.IsMatch(e) && !Catalogo.EhLeitura(e)).ToList();
        Assert.Equal(_carregadas, escritas);
        Assert.Empty(_b.Tela.Erros);
    }

    private GerentePlaylists NovoGerente() =>
        new(new RepositorioTextoArquivos(_b.Arquivos, RepositorioTextoArquivos.ArquivoPlaylists), _b.Log);

    private static ArquivoAudio Arquivo(int i, int segundos = 187) =>
        new($"content://musica/{i}", $"Faixa {i}", "Music/Trilhas", TimeSpan.FromSeconds(segundos));

    private static string Uri(int i) => $"content://musica/{i}";

    private static string Filme => ConhecimentoHerdado.IdMemoriaFilme;

    /// <summary>Conecta: a conferência põe a FILME herdada (memória do celular) em uso.</summary>
    private async Task Conectar()
    {
        if (_b.Sessao.Estado != EstadoSessao.Conectado)
            await _b.Conectar();
        await Apoio.Esperar(() => _player.Memoria == Filme);
        await _b.Tela.EsvaziarAsync();
    }

    /// <summary>Carrega a memória n (0 a 2) do aparelho: sem memória do celular em uso, não há playlist.</summary>
    private async Task CarregarDoAparelho(int n)
    {
        _carregadas.Add($"*241*{n}*");
        var r = _b.Memorias.CarregarAsync(n);
        await _b.Rodar(r);
        Assert.True((await r).Ok, (await r).Mensagem);
        await Apoio.Esperar(() => _player.Memoria is null);
        await _b.Tela.EsvaziarAsync();
    }

    /// <summary>Cria a memória do celular SHOW (do som atual, que é o da FILME): ela fica em uso, sem nada pelo BLE.</summary>
    private async Task<string> CriarShow()
    {
        Assert.True((await _b.Setups!.CriarMemoriaAsync("SHOW")).Ok);
        var id = _b.Setups.MemoriasDe(ConhecimentoHerdado.IdSetupCasa).Single(m => m.Nome == "SHOW").Id;
        await Apoio.Esperar(() => _player.Memoria == id);
        await _b.Tela.EsvaziarAsync();
        return id;
    }

    /// <summary>Aplica uma memória do celular (o som é o mesmo: nada sai pelo BLE).</summary>
    private async Task Aplicar(string id)
    {
        var r = _b.Setups!.AplicarAsync(id);
        await _b.Rodar(r);
        Assert.True((await r).Ok, (await r).Mensagem);
        await Apoio.Esperar(() => _player.Memoria == id);
        await _b.Tela.EsvaziarAsync();
    }

    /// <summary>A FILME (memória do celular) em uso, com as faixas 1 a <paramref name="quantas"/> na playlist.</summary>
    private async Task FilmeCom(int quantas)
    {
        await Conectar();
        _seletor.Escolhidos = Enumerable.Range(1, quantas).Select(i => Arquivo(i)).ToArray();
        await _vm.Adicionar.ExecutarAsync();
        Assert.Equal(quantas, _vm.Linhas.Count);
    }

    /// <summary>O reprodutor termina a faixa; espera o Player seguir e a tela andar.</summary>
    private async Task TerminarFaixa(Func<bool> depois)
    {
        _reprodutor.Terminar();
        await Apoio.Esperar(depois);
        await _b.Tela.EsvaziarAsync();
    }

    private string[] Nomes() => _vm.Linhas.Select(l => l.Nome).ToArray();

    // ----------------------------------------------------------------- sem memória e vazia

    private const string SemMemoria = "Sem memória do celular em uso, não há playlist. Aplique uma memória do celular.";

    [Fact]
    public async Task Desconectado_sem_memoria_do_celular_nada_de_playlist_nem_de_comandos()
    {
        await _b.Setups!.Carregada;
        await _b.Tela.EsvaziarAsync();
        _seletor.Escolhidos = [Arquivo(1)];

        Assert.Equal(SomEmUso.Nenhum, _b.Setups.EmUso);
        Assert.False(_vm.TemMemoria);
        Assert.Null(_vm.Titulo);
        Assert.Equal(SemMemoria, _vm.TextoAviso);
        Assert.Empty(_vm.Linhas);

        await _vm.TocarPausar.ExecutarAsync();
        Assert.Equal(SemMemoria, _vm.Mensagem);
        await _vm.Proxima.ExecutarAsync();
        await _vm.Adicionar.ExecutarAsync();

        Assert.Equal(0, _seletor.Aberturas);
        Assert.Empty(_reprodutor.Pedidas);
        Assert.Null(_b.Arquivos.Conteudo(RepositorioTextoArquivos.ArquivoPlaylists));
    }

    [Fact]
    public async Task Com_uma_memoria_do_aparelho_em_uso_nao_ha_playlist_e_tocar_fica_travado()
    {
        await FilmeCom(2);
        await _vm.TocarPausar.ExecutarAsync();
        Assert.Equal(Uri(1), _reprodutor.Tocando);

        await CarregarDoAparelho(0);

        Assert.False(_vm.TemMemoria);
        Assert.Equal(SemMemoria, _vm.TextoAviso);
        Assert.Empty(_vm.Linhas);
        Assert.Null(_reprodutor.Tocando); // trocar de memória para o Player
        await _vm.TocarPausar.ExecutarAsync();
        Assert.Equal(SemMemoria, _vm.Mensagem);
        Assert.Equal([Uri(1)], _reprodutor.Pedidas);
    }

    [Fact]
    public async Task BLE_cai_tocando_a_faixa_2_continua_tocando_e_a_playlist_da_FILME_fica_na_tela()
    {
        // Decisão do Denis (24/10/2026): a queda não troca nem limpa a playlist do Player.
        await FilmeCom(3);
        await _vm.Linhas[1].Tocar.ExecutarAsync();
        Assert.Equal(Uri(2), _reprodutor.Tocando);

        await _b.Sessao.DesconectarAsync();
        await Apoio.Esperar(() => _player.Retida);
        await _b.Tela.EsvaziarAsync();

        Assert.True(_vm.TemMemoria);
        Assert.Equal("Playlist da FILME", _vm.Titulo);
        Assert.Equal(3, _vm.Linhas.Count);
        Assert.True(_vm.Linhas[1].Tocando);
        Assert.Equal(Uri(2), _reprodutor.Tocando);
        Assert.Equal(0, _reprodutor.Paradas);
        Assert.Equal(PlayerViewModel.TextoPausar, _vm.TextoTocarPausar);

        // Desconectado, os controles seguem valendo.
        await _vm.Proxima.ExecutarAsync();
        Assert.Equal(Uri(3), _reprodutor.Tocando);
    }

    [Fact]
    public async Task Reconectar_na_FILME_segue_tocando_e_aplicar_outra_memoria_depois_para_e_mostra_a_playlist_dela()
    {
        await FilmeCom(2);
        var show = await CriarShow();
        await Aplicar(Filme);
        await _vm.TocarPausar.ExecutarAsync();
        Assert.Equal(Uri(1), _reprodutor.Tocando);

        await _b.Sessao.DesconectarAsync();
        await Apoio.Esperar(() => _player.Retida);
        await _b.Conectar(); // a conferência confere a FILME: a mesma memória, segue tocando
        await Apoio.Esperar(() => !_player.Retida);
        await _b.Tela.EsvaziarAsync();
        Assert.Equal("Playlist da FILME", _vm.Titulo);
        Assert.Equal(Uri(1), _reprodutor.Tocando);
        Assert.Equal(0, _reprodutor.Paradas);

        await Aplicar(show);

        Assert.Equal("Playlist da SHOW", _vm.Titulo);
        Assert.Null(_reprodutor.Tocando);
        Assert.Equal(EstadoPlayer.Parado, _vm.Estado);
    }

    [Fact]
    public async Task Vazia_mostra_o_titulo_e_o_aviso_de_adicionar()
    {
        await Conectar();

        Assert.True(_vm.TemMemoria);
        Assert.Equal("Playlist da FILME", _vm.Titulo);
        Assert.Equal("Playlist da FILME vazia. Toque em Adicionar músicas.", _vm.TextoAviso);
        Assert.Empty(_vm.Linhas);

        // Tocar com a playlist vazia não chama o reprodutor.
        await _vm.TocarPausar.ExecutarAsync();
        Assert.Empty(_reprodutor.Pedidas);
        Assert.Equal(EstadoPlayer.Parado, _vm.Estado);
    }

    // ----------------------------------------------------------------- editar

    [Fact]
    public async Task Adicionar_tres_arquivos_entram_no_fim_na_ordem_e_o_arquivo_e_gravado()
    {
        await Conectar();
        _seletor.Escolhidos = [Arquivo(1, 187), Arquivo(2, 59), Arquivo(3, 3725)];
        await _vm.Adicionar.ExecutarAsync();

        Assert.Equal(1, _seletor.Aberturas);
        Assert.Equal(["Faixa 1", "Faixa 2", "Faixa 3"], Nomes());
        Assert.Equal(["3:07", "0:59", "62:05"], _vm.Linhas.Select(l => l.Duracao));
        Assert.All(_vm.Linhas, l => Assert.Equal("Music/Trilhas", l.Pasta));
        Assert.Equal([1, 2, 3], _vm.Linhas.Select(l => l.Posicao));
        Assert.Null(_vm.TextoAviso);
        Assert.Equal(3, _vm.Linhas.Select(l => l.Id).Distinct().Count());

        // Mais um entra no fim.
        _seletor.Escolhidos = [Arquivo(4)];
        await _vm.Adicionar.ExecutarAsync();
        Assert.Equal(["Faixa 1", "Faixa 2", "Faixa 3", "Faixa 4"], Nomes());

        var gravado = GerentePlaylists.DeJson(_b.Arquivos.Conteudo(RepositorioTextoArquivos.ArquivoPlaylists)!);
        Assert.Equal([Uri(1), Uri(2), Uri(3), Uri(4)], gravado.PorMemoria[Filme].Select(f => f.Uri));
        Assert.Equal([Filme], gravado.PorMemoria.Keys);
    }

    [Fact]
    public async Task Desistir_no_seletor_nao_muda_nada()
    {
        await Conectar();
        _seletor.Escolhidos = [];
        await _vm.Adicionar.ExecutarAsync();

        Assert.Equal(1, _seletor.Aberturas);
        Assert.Empty(_vm.Linhas);
        Assert.Null(_b.Arquivos.Conteudo(RepositorioTextoArquivos.ArquivoPlaylists));
    }

    [Fact]
    public async Task Toque_duplo_em_Adicionar_abre_o_seletor_uma_vez_e_adiciona_uma_vez()
    {
        await Conectar();
        _seletor.Escolhidos = [Arquivo(1), Arquivo(2)];
        _seletor.Portao = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var primeiro = _vm.Adicionar.ExecutarAsync();
        Assert.True(_vm.Ocupado); // o seletor aberto já conta como edição
        var segundo = _vm.Adicionar.ExecutarAsync();
        await segundo; // recusado na hora
        await _vm.MoverAsync(1, 2); // outra edição também espera
        _seletor.Portao.SetResult();
        await primeiro;

        Assert.Equal(1, _seletor.Aberturas);
        Assert.Equal(["Faixa 1", "Faixa 2"], Nomes());
        Assert.False(_vm.Ocupado);
        var gravado = GerentePlaylists.DeJson(_b.Arquivos.Conteudo(RepositorioTextoArquivos.ArquivoPlaylists)!);
        Assert.Equal([Uri(1), Uri(2)], gravado.PorMemoria[Filme].Select(f => f.Uri));
    }

    [Fact]
    public async Task Desistir_no_seletor_destrava_a_proxima_edicao()
    {
        await Conectar();
        _seletor.Escolhidos = [];
        await _vm.Adicionar.ExecutarAsync();
        Assert.False(_vm.Ocupado);

        _seletor.Escolhidos = [Arquivo(1)];
        await _vm.Adicionar.ExecutarAsync();
        Assert.Equal(["Faixa 1"], Nomes());
    }

    [Fact]
    public async Task Ordenar_mover_a_3_para_a_1_grava_a_nova_ordem()
    {
        await FilmeCom(3);
        await _vm.Editar.ExecutarAsync();
        Assert.True(_vm.Editando);

        await _vm.MoverAsync(3, 1);

        Assert.Equal(["Faixa 3", "Faixa 1", "Faixa 2"], Nomes());
        Assert.Equal([1, 2, 3], _vm.Linhas.Select(l => l.Posicao));
        var gravado = GerentePlaylists.DeJson(_b.Arquivos.Conteudo(RepositorioTextoArquivos.ArquivoPlaylists)!);
        Assert.Equal([Uri(3), Uri(1), Uri(2)], gravado.PorMemoria[Filme].Select(f => f.Uri));

        await _vm.Pronto.ExecutarAsync();
        Assert.False(_vm.Editando);
    }

    [Fact]
    public async Task Remover_tira_a_faixa_e_grava()
    {
        await FilmeCom(3);
        await _vm.Linha("Faixa 2")!.Remover.ExecutarAsync();

        Assert.Equal(["Faixa 1", "Faixa 3"], Nomes());
        var gravado = GerentePlaylists.DeJson(_b.Arquivos.Conteudo(RepositorioTextoArquivos.ArquivoPlaylists)!);
        Assert.Equal([Uri(1), Uri(3)], gravado.PorMemoria[Filme].Select(f => f.Uri));
    }

    [Fact]
    public async Task Falha_ao_gravar_a_lista_nao_muda_e_a_frase_aparece()
    {
        await FilmeCom(2);
        _b.Arquivos.Falhar = true;

        _seletor.Escolhidos = [Arquivo(3)];
        await _vm.Adicionar.ExecutarAsync();
        Assert.Equal("Não consegui guardar a playlist no celular.", _vm.Mensagem);
        Assert.Equal(["Faixa 1", "Faixa 2"], Nomes());

        await _vm.MoverAsync(2, 1);
        Assert.Equal(["Faixa 1", "Faixa 2"], Nomes());
        await _vm.Linha("Faixa 1")!.Remover.ExecutarAsync();
        Assert.Equal(["Faixa 1", "Faixa 2"], Nomes());
        Assert.Equal("Não consegui guardar a playlist no celular.", _vm.Mensagem);

        // O arquivo continua com o que foi gravado antes da falha.
        var gravado = GerentePlaylists.DeJson(_b.Arquivos.Conteudo(RepositorioTextoArquivos.ArquivoPlaylists)!);
        Assert.Equal([Uri(1), Uri(2)], gravado.PorMemoria[Filme].Select(f => f.Uri));
    }

    // ----------------------------------------------------------------- tocar

    [Fact]
    public async Task Tocar_e_seguir_a_faixa_1_termina_e_toca_a_2_com_a_marca_andando()
    {
        await FilmeCom(3);

        await _vm.TocarPausar.ExecutarAsync();
        Assert.Equal(Uri(1), _reprodutor.Tocando);
        Assert.Equal(EstadoPlayer.Tocando, _vm.Estado);
        Assert.Equal("Pausar", _vm.TextoTocarPausar);
        Assert.Equal("Faixa 1", _vm.FaixaAtual);
        Assert.Equal([true, false, false], _vm.Linhas.Select(l => l.Tocando));

        await TerminarFaixa(() => _player.Atual?.Uri == Uri(2));

        Assert.Equal(Uri(2), _reprodutor.Tocando);
        Assert.Equal("Faixa 2", _vm.FaixaAtual);
        Assert.Equal([false, true, false], _vm.Linhas.Select(l => l.Tocando));
    }

    [Fact]
    public async Task Pausar_continuar_proxima_e_tocar_numa_linha()
    {
        await FilmeCom(3);
        await _vm.TocarPausar.ExecutarAsync();

        await _vm.TocarPausar.ExecutarAsync();
        Assert.True(_reprodutor.Pausado);
        Assert.Equal(EstadoPlayer.Pausado, _vm.Estado);
        Assert.Equal("Tocar", _vm.TextoTocarPausar);
        Assert.Equal("Faixa 1", _vm.FaixaAtual);

        await _vm.TocarPausar.ExecutarAsync();
        Assert.False(_reprodutor.Pausado);
        Assert.Equal(EstadoPlayer.Tocando, _vm.Estado);

        await _vm.Proxima.ExecutarAsync();
        Assert.Equal(Uri(2), _reprodutor.Tocando);
        Assert.Equal("Faixa 2", _vm.FaixaAtual);

        await _vm.Linha("Faixa 3")!.Tocar.ExecutarAsync();
        Assert.Equal(Uri(3), _reprodutor.Tocando);
        Assert.Equal([false, false, true], _vm.Linhas.Select(l => l.Tocando));

        // Próxima depois da última: para.
        await _vm.Proxima.ExecutarAsync();
        Assert.Equal(EstadoPlayer.Parado, _vm.Estado);
        Assert.Null(_reprodutor.Tocando);
        Assert.Null(_vm.FaixaAtual);
    }

    [Fact]
    public async Task Arquivo_sumiu_a_2_aparece_indisponivel_e_toca_a_3()
    {
        await FilmeCom(3);
        _reprodutor.Ausentes.Add(Uri(2));
        await _vm.TocarPausar.ExecutarAsync();

        await TerminarFaixa(() => _player.Atual?.Uri == Uri(3));

        Assert.Equal(Uri(3), _reprodutor.Tocando);
        var dois = _vm.Linha("Faixa 2")!;
        Assert.True(dois.Indisponivel);
        Assert.Equal("Arquivo não encontrado", dois.TextoIndisponivel);
        Assert.False(_vm.Linha("Faixa 3")!.Indisponivel);
        Assert.Null(_vm.Linha("Faixa 3")!.TextoIndisponivel);
        Assert.True(_vm.Linha("Faixa 3")!.Tocando);
        Assert.Null(_vm.Mensagem);

        // A indisponível continua na lista e sai no modo Editar.
        await _vm.Editar.ExecutarAsync();
        await dois.Remover.ExecutarAsync();
        Assert.Equal(["Faixa 1", "Faixa 3"], Nomes());
        Assert.Equal(Uri(3), _reprodutor.Tocando); // remover outra faixa não para a música
    }

    [Fact]
    public async Task Arquivo_que_volta_e_tentado_de_novo_toca_e_perde_a_marca()
    {
        await FilmeCom(3);
        _reprodutor.Ausentes.Add(Uri(2));
        await _vm.TocarPausar.ExecutarAsync();
        await TerminarFaixa(() => _player.Atual?.Uri == Uri(3));
        Assert.True(_vm.Linha("Faixa 2")!.Indisponivel);

        _reprodutor.Ausentes.Remove(Uri(2)); // o arquivo voltou ao celular
        await _vm.Linha("Faixa 2")!.Tocar.ExecutarAsync();

        Assert.Equal(Uri(2), _reprodutor.Tocando);
        var dois = _vm.Linha("Faixa 2")!;
        Assert.True(dois.Tocando);
        Assert.False(dois.Indisponivel);
        Assert.Null(dois.TextoIndisponivel);
    }

    [Fact]
    public async Task Ultimas_indisponiveis_no_fim_da_faixa_1_para_sem_a_frase_e_tocar_volta_a_1()
    {
        await FilmeCom(3);
        _reprodutor.Ausentes.Add(Uri(2));
        _reprodutor.Ausentes.Add(Uri(3));
        await _vm.TocarPausar.ExecutarAsync();
        Assert.Equal(Uri(1), _reprodutor.Tocando);

        await TerminarFaixa(() => _player.Estado == EstadoPlayer.Parado);

        Assert.Equal(EstadoPlayer.Parado, _vm.Estado);
        Assert.Null(_vm.Mensagem); // a faixa 1 está disponível: não é "Nenhuma faixa…"
        Assert.True(_vm.Linha("Faixa 2")!.Indisponivel);
        Assert.True(_vm.Linha("Faixa 3")!.Indisponivel);

        await _vm.TocarPausar.ExecutarAsync();
        Assert.Equal(Uri(1), _reprodutor.Tocando);
        Assert.Equal("Faixa 1", _vm.FaixaAtual);
    }

    [Fact]
    public async Task Nenhuma_disponivel_fica_parado_com_a_frase()
    {
        await FilmeCom(3);
        foreach (var i in new[] { 1, 2, 3 })
            _reprodutor.Ausentes.Add(Uri(i));

        await _vm.TocarPausar.ExecutarAsync();

        Assert.Equal(EstadoPlayer.Parado, _vm.Estado);
        Assert.Null(_reprodutor.Tocando);
        Assert.Null(_vm.FaixaAtual);
        Assert.Equal("Nenhuma faixa disponível na playlist da FILME.", _vm.Mensagem);
        Assert.All(_vm.Linhas, l => Assert.Equal("Arquivo não encontrado", l.TextoIndisponivel));
        Assert.Equal([Uri(1), Uri(2), Uri(3)], _reprodutor.Pedidas);
    }

    [Fact]
    public async Task Fim_da_lista_para_e_o_proximo_tocar_volta_a_primeira()
    {
        await FilmeCom(2);
        await _vm.TocarPausar.ExecutarAsync();
        await TerminarFaixa(() => _player.Atual?.Uri == Uri(2));

        await TerminarFaixa(() => _player.Estado == EstadoPlayer.Parado);

        Assert.Null(_vm.FaixaAtual);
        Assert.All(_vm.Linhas, l => Assert.False(l.Tocando));
        Assert.Equal("Tocar", _vm.TextoTocarPausar);
        Assert.Null(_vm.Mensagem);

        await _vm.TocarPausar.ExecutarAsync();
        Assert.Equal(Uri(1), _reprodutor.Tocando);
        Assert.Equal("Faixa 1", _vm.FaixaAtual);
    }

    [Fact]
    public async Task Troca_de_memoria_tocando_para_na_hora_e_mostra_a_playlist_da_nova_parada()
    {
        // A SHOW já tem uma faixa na playlist dela.
        await Conectar();
        var show = await CriarShow();
        Assert.True((await _playlists.AdicionarAsync(show, [Arquivo(9)])).Ok);
        await Aplicar(Filme);
        await FilmeCom(2);
        await _vm.TocarPausar.ExecutarAsync();
        Assert.Equal(Uri(1), _reprodutor.Tocando);
        var paradas = _reprodutor.Paradas;

        // A memória em uso passa da FILME para a SHOW.
        await Aplicar(show);

        Assert.True(_reprodutor.Paradas > paradas);

        Assert.Equal("Playlist da SHOW", _vm.Titulo);
        Assert.Equal(["Faixa 9"], Nomes());
        Assert.Equal(EstadoPlayer.Parado, _vm.Estado);
        Assert.Null(_vm.FaixaAtual);
        Assert.All(_vm.Linhas, l => Assert.False(l.Tocando));
        Assert.Null(_reprodutor.Tocando);
        Assert.Equal([Uri(1)], _reprodutor.Pedidas); // nada da playlist nova tocou sozinho
    }

    [Fact]
    public async Task Troca_de_memoria_tira_a_frase_e_o_modo_Editar_da_anterior()
    {
        await FilmeCom(2);
        _reprodutor.Ausentes.Add(Uri(1));
        _reprodutor.Ausentes.Add(Uri(2));
        await _vm.TocarPausar.ExecutarAsync();
        await _vm.Editar.ExecutarAsync();
        Assert.Equal("Nenhuma faixa disponível na playlist da FILME.", _vm.Mensagem);
        Assert.True(_vm.Editando);

        await CriarShow();
        await Apoio.Esperar(() => _vm.Titulo == "Playlist da SHOW");
        await _b.Tela.EsvaziarAsync();

        Assert.Null(_vm.Mensagem);
        Assert.False(_vm.Editando);
    }

    [Fact]
    public async Task Remover_a_que_toca_para_e_grava_a_lista()
    {
        await FilmeCom(3);
        await _vm.TocarPausar.ExecutarAsync();
        await _vm.Proxima.ExecutarAsync();
        Assert.Equal(Uri(2), _reprodutor.Tocando);

        await _vm.Linha("Faixa 2")!.Remover.ExecutarAsync();
        await Apoio.Esperar(() => _player.Estado == EstadoPlayer.Parado);
        await _b.Tela.EsvaziarAsync();

        Assert.Null(_reprodutor.Tocando);
        Assert.Null(_vm.FaixaAtual);
        Assert.Equal(["Faixa 1", "Faixa 3"], Nomes());
        var gravado = GerentePlaylists.DeJson(_b.Arquivos.Conteudo(RepositorioTextoArquivos.ArquivoPlaylists)!);
        Assert.Equal([Uri(1), Uri(3)], gravado.PorMemoria[Filme].Select(f => f.Uri));
    }

    [Fact]
    public async Task Mover_a_que_toca_nao_para_e_a_proxima_segue_a_ordem_nova()
    {
        await FilmeCom(3);
        await _vm.TocarPausar.ExecutarAsync();

        await _vm.MoverAsync(1, 3); // a faixa 1, que toca, vai para o fim
        await _b.Tela.EsvaziarAsync();
        Assert.Equal(["Faixa 2", "Faixa 3", "Faixa 1"], Nomes());
        Assert.Equal(Uri(1), _reprodutor.Tocando);
        Assert.True(_vm.Linha("Faixa 1")!.Tocando);

        await TerminarFaixa(() => _player.Estado == EstadoPlayer.Parado);
        Assert.Null(_reprodutor.Tocando); // era a última da ordem nova
    }

    // ----------------------------------------------------------------- guardado

    [Fact]
    public async Task Reabrir_as_playlists_por_memoria_voltam_na_ordem()
    {
        await FilmeCom(3);
        await _vm.MoverAsync(2, 1);
        Assert.True((await _playlists.AdicionarAsync("memoria-a", [Arquivo(7), Arquivo(8)])).Ok);
        Assert.True((await _playlists.AdicionarAsync("memoria-b", [Arquivo(5)])).Ok);

        var outro = NovoGerente();
        await outro.Carregada;

        Assert.Equal([Uri(7), Uri(8)], outro.Playlist("memoria-a").Select(f => f.Uri));
        Assert.Equal([Uri(5)], outro.Playlist("memoria-b").Select(f => f.Uri));
        Assert.Equal([Uri(2), Uri(1), Uri(3)], outro.Playlist(Filme).Select(f => f.Uri));
        Assert.Equal(_playlists.Playlist(Filme), outro.Playlist(Filme)); // os mesmos ids, nomes, pastas e durações
    }

    [Fact]
    public async Task Arquivo_estragado_vira_nenhuma_playlist_e_adicionar_funciona()
    {
        await _b.Playlists.Carregada;
        await _b.Arquivos.GravarTextoAsync(RepositorioTextoArquivos.ArquivoPlaylists, "{ isto não é json");
        var playlists = NovoGerente();
        await playlists.Carregada;
        using var player = new Player(playlists, _b.Setups!, _reprodutor, _b.Log);
        using var vm = new PlayerViewModel(player, playlists, _b.Setups!, _seletor, _b.Tela, _b.Relogio);

        Assert.Empty(playlists.Playlist(Filme));
        await Conectar();
        await Apoio.Esperar(() => player.Memoria == Filme);
        await _b.Tela.EsvaziarAsync();
        Assert.Equal("Playlist da FILME vazia. Toque em Adicionar músicas.", vm.TextoAviso);

        _seletor.Escolhidos = [Arquivo(1)];
        await vm.Adicionar.ExecutarAsync();

        Assert.Equal(["Faixa 1"], vm.Linhas.Select(l => l.Nome));
        var gravado = GerentePlaylists.DeJson(_b.Arquivos.Conteudo(RepositorioTextoArquivos.ArquivoPlaylists)!);
        Assert.Equal([Uri(1)], gravado.PorMemoria[Filme].Select(f => f.Uri));
    }

    // ----------------------------------------------------------------- textos

    [Theory]
    [InlineData(0, "0:00")]
    [InlineData(5, "0:05")]
    [InlineData(65, "1:05")]
    [InlineData(600, "10:00")]
    [InlineData(3725, "62:05")]
    public void Duracao_em_m_ss(int segundos, string esperado) =>
        Assert.Equal(esperado, PlayerViewModel.FormatarDuracao(TimeSpan.FromSeconds(segundos)));
}
