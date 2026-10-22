using System.Text.RegularExpressions;
using BgsControle.Nucleo;
using BgsControle.Nucleo.Player;

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
/// A história 8b nas telas, contra o processador simulado: o <see cref="PlayerViewModel"/> com o
/// <see cref="Player"/>, o <see cref="GerentePlaylists"/> e a memória em uso de verdade. Uma linha de
/// teste para cada linha da matriz de <c>8b-player-playlist-por-memoria.md</c>.
/// </summary>
public sealed class PlayerViewModelTestes : IDisposable
{
    private static readonly Regex Senha = new(@"^\*000\*[0-9A-Z]{4}\*$");

    private readonly Bancada _b = new();
    private readonly ReprodutorFalso _reprodutor = new();
    private readonly SeletorFalso _seletor = new();
    private readonly GerentePlaylists _playlists;
    private readonly Player _player;
    private readonly PlayerViewModel _vm;
    private readonly List<string> _carregadas = [];

    public PlayerViewModelTestes()
    {
        _playlists = NovoGerente();
        _player = new Player(_playlists, _b.Memorias, _reprodutor, _b.Log);
        _vm = new PlayerViewModel(_player, _playlists, _b.Memorias, _seletor, _b.Tela, _b.Relogio);
    }

    /// <summary>
    /// Critério de aceite: nenhuma escrita da 8b chegou ao aparelho. Fora a senha e as leituras, só
    /// os carregar de memória que o próprio teste pediu para pôr a memória em uso.
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

    /// <summary>Conecta e carrega a memória n (0 a 2): o único jeito de a memória ficar em uso.</summary>
    private async Task Carregar(int n)
    {
        if (_b.Sessao.Estado != EstadoSessao.Conectado)
            await _b.Conectar();
        _carregadas.Add($"*241*{n}*");
        var r = _b.Memorias.CarregarAsync(n);
        await _b.Rodar(r);
        Assert.True((await r).Ok, (await r).Mensagem);
        await Apoio.Esperar(() => _player.Memoria == n);
        await _b.Tela.EsvaziarAsync();
    }

    /// <summary>A FILME (memória 3) em uso, com as faixas 1 a <paramref name="quantas"/> na playlist.</summary>
    private async Task FilmeCom(int quantas)
    {
        await Carregar(2);
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

    [Fact]
    public async Task Sem_memoria_nada_de_playlist_nem_de_comandos()
    {
        await _b.Conectar();
        await _b.Tela.EsvaziarAsync();
        _seletor.Escolhidos = [Arquivo(1)];

        Assert.Null(_b.Memorias.EmUso);
        Assert.False(_vm.TemMemoria);
        Assert.Null(_vm.Titulo);
        Assert.Equal("Carregue uma memória para usar o Player.", _vm.TextoAviso);
        Assert.Empty(_vm.Linhas);

        await _vm.TocarPausar.ExecutarAsync();
        Assert.Equal("Carregue uma memória para usar o Player.", _vm.Mensagem);
        await _vm.Proxima.ExecutarAsync();
        await _vm.Adicionar.ExecutarAsync();

        Assert.Equal(0, _seletor.Aberturas);
        Assert.Empty(_reprodutor.Pedidas);
        Assert.Null(_b.Arquivos.Conteudo(RepositorioTextoArquivos.ArquivoPlaylists));
    }

    [Fact]
    public async Task Vazia_mostra_o_titulo_e_o_aviso_de_adicionar()
    {
        await Carregar(2);

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
        await Carregar(2);
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
        Assert.Equal([Uri(1), Uri(2), Uri(3), Uri(4)], gravado[2].Select(f => f.Uri));
        Assert.Empty(gravado[0]);
        Assert.Empty(gravado[1]);
    }

    [Fact]
    public async Task Desistir_no_seletor_nao_muda_nada()
    {
        await Carregar(2);
        _seletor.Escolhidos = [];
        await _vm.Adicionar.ExecutarAsync();

        Assert.Equal(1, _seletor.Aberturas);
        Assert.Empty(_vm.Linhas);
        Assert.Null(_b.Arquivos.Conteudo(RepositorioTextoArquivos.ArquivoPlaylists));
    }

    [Fact]
    public async Task Toque_duplo_em_Adicionar_abre_o_seletor_uma_vez_e_adiciona_uma_vez()
    {
        await Carregar(2);
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
        Assert.Equal([Uri(1), Uri(2)], gravado[2].Select(f => f.Uri));
    }

    [Fact]
    public async Task Desistir_no_seletor_destrava_a_proxima_edicao()
    {
        await Carregar(2);
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
        Assert.Equal([Uri(3), Uri(1), Uri(2)], gravado[2].Select(f => f.Uri));

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
        Assert.Equal([Uri(1), Uri(3)], gravado[2].Select(f => f.Uri));
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
        Assert.Equal([Uri(1), Uri(2)], gravado[2].Select(f => f.Uri));
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
        // A playlist da memória 1 (posição 0) já tem uma faixa.
        Assert.True((await _playlists.AdicionarAsync(0, [Arquivo(9)])).Ok);
        await FilmeCom(2);
        await _vm.TocarPausar.ExecutarAsync();
        Assert.Equal(Uri(1), _reprodutor.Tocando);
        var paradas = _reprodutor.Paradas;

        // A memória em uso passa da 2 para a 0 (no meio, sem memória: o 241 saiu e a releitura anda).
        await Carregar(0);

        Assert.True(_reprodutor.Paradas > paradas);

        Assert.Equal("Playlist da SPL", _vm.Titulo);
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

        await Carregar(0);
        await Apoio.Esperar(() => _vm.Titulo == "Playlist da SPL");
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
        Assert.Equal([Uri(1), Uri(3)], gravado[2].Select(f => f.Uri));
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
    public async Task Reabrir_as_3_playlists_voltam_na_ordem()
    {
        await FilmeCom(3);
        await _vm.MoverAsync(2, 1);
        Assert.True((await _playlists.AdicionarAsync(0, [Arquivo(7), Arquivo(8)])).Ok);
        Assert.True((await _playlists.AdicionarAsync(1, [Arquivo(5)])).Ok);

        var outro = NovoGerente();
        await outro.Carregada;

        Assert.Equal([Uri(7), Uri(8)], outro.Playlist(0).Select(f => f.Uri));
        Assert.Equal([Uri(5)], outro.Playlist(1).Select(f => f.Uri));
        Assert.Equal([Uri(2), Uri(1), Uri(3)], outro.Playlist(2).Select(f => f.Uri));
        Assert.Equal(_playlists.Playlist(2), outro.Playlist(2)); // os mesmos ids, nomes, pastas e durações
    }

    [Fact]
    public async Task Arquivo_estragado_vira_3_vazias_e_adicionar_funciona()
    {
        await _b.Arquivos.GravarTextoAsync(RepositorioTextoArquivos.ArquivoPlaylists, "{ isto não é json");
        var playlists = NovoGerente();
        await playlists.Carregada;
        using var player = new Player(playlists, _b.Memorias, _reprodutor, _b.Log);
        using var vm = new PlayerViewModel(player, playlists, _b.Memorias, _seletor, _b.Tela, _b.Relogio);

        Assert.All(Enumerable.Range(0, 3), n => Assert.Empty(playlists.Playlist(n)));
        await Carregar(2);
        await Apoio.Esperar(() => player.Memoria == 2);
        await _b.Tela.EsvaziarAsync();
        Assert.Equal("Playlist da FILME vazia. Toque em Adicionar músicas.", vm.TextoAviso);

        _seletor.Escolhidos = [Arquivo(1)];
        await vm.Adicionar.ExecutarAsync();

        Assert.Equal(["Faixa 1"], vm.Linhas.Select(l => l.Nome));
        var gravado = GerentePlaylists.DeJson(_b.Arquivos.Conteudo(RepositorioTextoArquivos.ArquivoPlaylists)!);
        Assert.Equal([Uri(1)], gravado[2].Select(f => f.Uri));
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
