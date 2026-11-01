using System.Globalization;
using BgsControle.Nucleo.Player;
using BgsControle.Nucleo.Registro;
using BgsControle.Nucleo.Rendimento;
using BgsControle.Nucleo.Setups;

namespace BgsControle.Telas;

/// <summary>
/// Uma linha da playlist: nome, duração em m:ss, pasta, a marca de "tocando" e a de arquivo sumido, e o que ela pesa no
/// teto do Extra (história 13d).
/// </summary>
public sealed class LinhaPlaylistViewModel : TelaBase
{
    private int _posicao;
    private bool _tocando;
    private bool _indisponivel;
    private bool _semAcesso;
    private bool _editando;
    private bool _emAnalise;
    private string? _textoAnalise;
    private string? _textoMarca;

    internal LinhaPlaylistViewModel(Faixa faixa, PlayerViewModel tela, IDespachante despachante, TimeProvider relogio)
        : base(despachante, relogio)
    {
        Id = faixa.Id;
        Uri = faixa.Uri;
        Nome = faixa.Nome;
        Pasta = faixa.Pasta;
        Duracao = PlayerViewModel.FormatarDuracao(faixa.Duracao);
        Tocar = new Comando(() => tela.TocarFaixaAsync(Id));
        Remover = new Comando(() => tela.RemoverAsync(Id));
    }

    public string Id { get; }

    /// <summary>A URI do arquivo (o app não copia a música).</summary>
    public string Uri { get; }

    public string Nome { get; }

    public string Pasta { get; }

    /// <summary>"3:07".</summary>
    public string Duracao { get; }

    /// <summary>A posição na playlist (1 = a primeira).</summary>
    public int Posicao
    {
        get => _posicao;
        internal set
        {
            if (_posicao == value)
                return;
            _posicao = value;
            Avisar();
        }
    }

    /// <summary>É a faixa que toca (ou está pausada).</summary>
    public bool Tocando
    {
        get => _tocando;
        internal set
        {
            if (_tocando == value)
                return;
            _tocando = value;
            Avisar();
        }
    }

    /// <summary>O arquivo sumiu do celular: a linha fica apagada e é pulada ao tocar.</summary>
    public bool Indisponivel
    {
        get => _indisponivel;
        internal set
        {
            if (_indisponivel == value)
                return;
            _indisponivel = value;
            Avisar();
            Avisar(nameof(TextoIndisponivel));
        }
    }

    /// <summary>O Android negou ler esta música: "Sem acesso ao arquivo: adicione de novo".</summary>
    public bool SemAcesso
    {
        get => _semAcesso;
        internal set
        {
            if (_semAcesso == value)
                return;
            _semAcesso = value;
            Avisar();
            Avisar(nameof(TextoIndisponivel));
        }
    }

    /// <summary>"Arquivo não encontrado", "Sem acesso ao arquivo: adicione de novo", ou null.</summary>
    public string? TextoIndisponivel => !_indisponivel ? null : _semAcesso ? PlayerViewModel.MsgSemAcessoArquivo : Player.TextoIndisponivel;

    /// <summary>A faixa está em análise (história 13d): ela não toca até terminar.</summary>
    public bool EmAnalise
    {
        get => _emAnalise;
        internal set
        {
            if (_emAnalise == value)
                return;
            _emAnalise = value;
            Avisar();
        }
    }

    /// <summary>
    /// Quem está pesando (história 13d): "−3,0 dB no teto", "Em análise" ou "Não deu para analisar: …"; null sem análise
    /// ou com a faixa sem peso.
    /// </summary>
    public string? TextoAnalise
    {
        get => _textoAnalise;
        internal set
        {
            if (_textoAnalise == value)
                return;
            _textoAnalise = value;
            Avisar();
        }
    }

    /// <summary>
    /// A faixa marcada por um clip não previsto no uso do Extra (história 13e): "Clip na Sub aos 42,3 s" (a última marca),
    /// ou null.
    /// </summary>
    public string? TextoMarca
    {
        get => _textoMarca;
        internal set
        {
            if (_textoMarca == value)
                return;
            _textoMarca = value;
            Avisar();
        }
    }

    /// <summary>O modo Editar da tela: a linha mostra − e ≡.</summary>
    public bool Editando
    {
        get => _editando;
        internal set
        {
            if (_editando == value)
                return;
            _editando = value;
            Avisar();
        }
    }

    /// <summary>Tocar na linha: toca a partir desta faixa.</summary>
    public Comando Tocar { get; }

    /// <summary>O − do modo Editar: tira a faixa da playlist (o arquivo continua no celular), sem confirmação.</summary>
    public Comando Remover { get; }
}

/// <summary>
/// A tela Player (CAP-15): a playlist da memória do celular em uso, sem abas (decisão do Denis,
/// 22/10). O título "Playlist da NOME"; sem memória do celular em uso, "Sem memória do celular em
/// uso, não há playlist. Aplique uma memória do celular." e nenhum comando. As linhas (nome, m:ss, pasta, tocando, "Arquivo não encontrado"); Editar e Pronto;
/// Adicionar músicas (o seletor do Android, vários de uma vez, no fim); Remover e Mover; Tocar/pausar
/// e Próxima. Nada daqui manda nada ao processador, menos o Ligar do Bluetooth de áudio (251).
/// <para>
/// Os avisos do topo (história 11), um de cada vez, nesta ordem: "Sem acesso" a músicas da playlist (com
/// Adicionar de novo, o seletor; as outras faixas tocam), "Bluetooth de áudio desligado" (com Ligar; Tocar
/// travado até ligar) e "Celular não está mandando o som" (com o atalho para o Bluetooth do Android). A queda do BLE não tira a playlist (decisão do Denis, 24/10/2026).
/// </para>
/// </summary>
public sealed class PlayerViewModel : TelaBase, IDisposable
{
    public const string TextoAdicionar = "Adicionar músicas";
    public const string TextoEditar = "Editar";
    public const string TextoPronto = "Pronto";
    public const string TextoTocar = "Tocar";
    public const string TextoPausar = "Pausar";
    public const string TextoProxima = "Próxima";
    public const string MsgSemAcessoArquivo = "Sem acesso ao arquivo: adicione de novo";
    public const string TextoAdicionarDeNovo = "Adicionar de novo";
    public const string MsgAcessoDevolvido = "As músicas voltaram a tocar.";

    /// <summary>"Sem acesso a 2 músicas: adicione de novo."</summary>
    public static string MsgSemAcesso(int n) => n == 1
        ? "Sem acesso a 1 música: adicione de novo."
        : $"Sem acesso a {n} músicas: adicione de novo.";
    public const string MsgBluetoothDesligado = "O Bluetooth de áudio do processador está desligado.";
    public const string TextoLigar = "Ligar";
    public const string MsgSomPeloCelular = "O som está saindo pelo celular. Conecte o Bluetooth do celular ao processador.";
    public const string TextoAbrirBluetooth = "Abrir o Bluetooth";

    /// <summary>O botão da folha do outro player (história 13e).</summary>
    public const string TextoVoltarAoPlayer = "Voltar ao player do app";

    /// <summary>"Clip na Sub aos 42,3 s" (ou "Clip na Sub" sem o momento).</summary>
    public static string TextoClip(MarcaClip marca) => marca.Segundos is { } s
        ? $"Clip na {marca.Saida} aos {s.ToString("0.0", CultureInfo.InvariantCulture).Replace('.', ',')} s"
        : $"Clip na {marca.Saida}";

    /// <summary>"3:07": minutos e segundos (os minutos passam de 59 sem virar hora).</summary>
    public static string FormatarDuracao(TimeSpan d)
    {
        if (d < TimeSpan.Zero)
            d = TimeSpan.Zero;
        var segundos = (long)d.TotalSeconds;
        return string.Create(CultureInfo.InvariantCulture, $"{segundos / 60}:{segundos % 60:00}");
    }

    private readonly Player _player;
    private readonly GerentePlaylists _playlists;
    private readonly GerenteSetups _setups;
    private readonly ISeletorAudio _seletor;
    private readonly AparelhoViewModel? _aparelho;
    private readonly IAudioDoCelular? _audio;
    private readonly AnalisePlaylist? _analise;
    private readonly LogBle? _log;
    private readonly FiscalExtra? _fiscal;

    private readonly Dictionary<string, LinhaPlaylistViewModel> _porId = [];
    private IReadOnlyList<LinhaPlaylistViewModel> _linhas = [];
    private string? _mensagem;
    private string? _ultimoAvisoPlayer;
    private bool _editando;
    private bool _agindo;
    private string? _memoriaVista;

    public PlayerViewModel(
        Player player,
        GerentePlaylists playlists,
        GerenteSetups setups,
        ISeletorAudio seletor,
        IDespachante despachante,
        TimeProvider relogio,
        AparelhoViewModel? aparelho = null,
        IAudioDoCelular? audio = null,
        AnalisePlaylist? analise = null,
        LogBle? log = null,
        FiscalExtra? fiscal = null,
        ExtraAoVivoViewModel? extraAoVivo = null,
        OtimizarViewModel? otimizar = null)
        : base(despachante, relogio)
    {
        ExtraAoVivo = extraAoVivo;
        Otimizar = otimizar;
        _aparelho = aparelho;
        _audio = audio;
        _analise = analise;
        _log = log;
        _fiscal = fiscal;
        _player = player;
        _playlists = playlists;
        _setups = setups;
        _seletor = seletor;

        Adicionar = new Comando(AdicionarAsync);
        Editar = new Comando(() => Editando = true);
        Pronto = new Comando(() => Editando = false);
        TocarPausar = new Comando(TocarPausarAsync);
        Proxima = new Comando(ProximaAsync);
        AcaoAviso = new Comando(AgirNoAvisoAsync);
        VoltarAoPlayer = new Comando(() =>
        {
            _fiscal?.FecharFolha();
            Avisar(nameof(FolhaOutroPlayer));
        });

        _ultimoAvisoPlayer = _player.Aviso;
        _memoriaVista = _player.Memoria;
        _player.Mudou += AoMudarAlgo;
        _playlists.Mudou += AoMudarAlgo;
        _setups.Mudou += AoMudarAlgo;
        if (_aparelho is not null)
            _aparelho.PropertyChanged += AoMudarAparelho;
        if (_audio is not null)
            _audio.Mudou += AoMudarAlgo;
        if (_analise is not null)
            _analise.Mudou += AoMudarAlgo;
        if (_fiscal is not null)
            _fiscal.Mudou += AoMudarAlgo;
        Atualizar();
    }

    // ------------------------------------------------------------------ o Rendimento no Player (história 13g)

    /// <summary>O Extra ao vivo, no topo do Player ("Extra: 13:52", "Descansando: 38:04"), ou null.</summary>
    public ExtraAoVivoViewModel? ExtraAoVivo { get; }

    /// <summary>"Otimizar configuração na playlist", na playlist da memória, ou null.</summary>
    public OtimizarViewModel? Otimizar { get; }

    /// <summary>
    /// Um aviso para o aviso rápido de 2,6 s da casca (história 13g): o do fiscal do Extra ("No Extra o volume fica no
    /// máximo; use o master."). Chega na thread da tela.
    /// </summary>
    public event Action<string>? AvisoRapido;

    // ------------------------------------------------------------------ o fiscal do Extra (história 13e)

    /// <summary>A folha "Outro app começou a tocar. O som foi mutado." está aberta.</summary>
    public bool FolhaOutroPlayer => _fiscal?.FolhaOutroPlayer == true;

    /// <summary>"Outro app começou a tocar. O som foi mutado."</summary>
    public string TextoOutroPlayer => FiscalExtra.MsgOutroPlayer;

    /// <summary>"Voltar ao player do app".</summary>
    public string TextoVoltar => TextoVoltarAoPlayer;

    /// <summary>Fecha a folha do outro player (o mudo sai quando o usuário toca no player do app).</summary>
    public Comando VoltarAoPlayer { get; }

    // ------------------------------------------------------------------ avisos do topo

    /// <summary>Quantas músicas da playlist o Android negou ler (a permissão da URI se perdeu).</summary>
    public int QuantasSemAcesso => _audio is null ? 0 : _linhas.Count(l => _audio.SemAcesso(l.Uri));

    /// <summary>O Bluetooth de áudio do processador (251) foi lido desligado. Sem leitura (desconectado), não se sabe: false.</summary>
    public bool BluetoothAudioDesligado => _aparelho?.BluetoothAudioLigado == false;

    /// <summary>O Android manda o som pelo alto-falante do celular, não por Bluetooth.</summary>
    public bool SomPeloCelular => _audio?.SaindoPeloBluetooth == false;

    /// <summary>O aviso do topo, um de cada vez (sem permissão, Bluetooth de áudio desligado, som pelo celular), ou null.</summary>
    public string? AvisoTopo => !TemMemoria ? null
        : QuantasSemAcesso > 0 ? MsgSemAcesso(QuantasSemAcesso)
        : BluetoothAudioDesligado ? MsgBluetoothDesligado
        : SomPeloCelular ? MsgSomPeloCelular
        : null;

    /// <summary>O botão do aviso: "Adicionar de novo", "Ligar" ou "Abrir o Bluetooth", ou null.</summary>
    public string? TextoAcaoAviso => AvisoTopo switch
    {
        null => null,
        MsgBluetoothDesligado => TextoLigar,
        MsgSomPeloCelular => TextoAbrirBluetooth,
        _ => TextoAdicionarDeNovo,
    };

    /// <summary>
    /// O botão do aviso: abre o seletor para escolher de novo as músicas sem acesso, liga o Bluetooth de
    /// áudio (251, sem confirmação) ou abre o Bluetooth do Android.
    /// </summary>
    public Comando AcaoAviso { get; }

    /// <summary>
    /// Por que Tocar está travado ("O Bluetooth de áudio do processador está desligado."), ou null. Uma
    /// música sem acesso não trava as outras: ela é pulada como indisponível.
    /// </summary>
    public string? TravaTocar => BluetoothAudioDesligado ? MsgBluetoothDesligado : null;

    private async Task AgirNoAvisoAsync()
    {
        switch (AvisoTopo)
        {
            case { } aviso when aviso == MsgSemAcesso(QuantasSemAcesso) && QuantasSemAcesso > 0:
                await AdicionarAsync();
                break;
            case MsgBluetoothDesligado when _aparelho is not null:
                await _aparelho.DefinirBluetoothAudioAsync(true);
                await NaTelaAsync(() =>
                {
                    // "Bluetooth de áudio ligado." ou o motivo ("Chave desligada").
                    if (_aparelho.Mensagem is { } frase)
                        Mensagem = frase;
                });
                break;
            case MsgSomPeloCelular when _audio is not null:
                _audio.AbrirBluetooth();
                break;
        }
        await NaTelaAsync(Atualizar);
    }

    private void AoMudarAparelho(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(AparelhoViewModel.BluetoothAudioLigado) or null or "")
            NaTela(Atualizar);
    }

    // ------------------------------------------------------------------ o que aparece

    /// <summary>Há memória do celular em uso: a playlist e os comandos aparecem.</summary>
    public bool TemMemoria => _player.Memoria is not null;

    /// <summary>"Playlist da FILME"; null sem memória do celular em uso.</summary>
    public string? Titulo => _player.Memoria is { } n ? Player.TituloPlaylist(_player.NomeMemoria(n)) : null;

    /// <summary>
    /// O aviso fixo: "Sem memória do celular em uso, não há playlist…" sem memória; "Playlist da FILME
    /// vazia. Toque em Adicionar músicas." com a playlist vazia; null com faixas.
    /// </summary>
    public string? TextoAviso => _player.Memoria is not { } n
        ? Player.MsgSemMemoria
        : _linhas.Count == 0 ? Player.MsgVazia(_player.NomeMemoria(n)) : null;

    /// <summary>As faixas da playlist, na ordem; a lista inteira é trocada quando muda.</summary>
    public IReadOnlyList<LinhaPlaylistViewModel> Linhas => _linhas;

    /// <summary>A linha pelo nome da faixa, ou null.</summary>
    public LinhaPlaylistViewModel? Linha(string nome) => _linhas.FirstOrDefault(l => l.Nome == nome);

    /// <summary>O nome da faixa atual, em cima dos botões, ou null com o Player parado.</summary>
    public string? FaixaAtual => _player.Atual?.Nome;

    public EstadoPlayer Estado => _player.Estado;

    /// <summary>"Pausar" enquanto toca; "Tocar" parado ou pausado.</summary>
    public string TextoTocarPausar => _player.Estado == EstadoPlayer.Tocando ? TextoPausar : TextoTocar;

    /// <summary>A última frase: "Nenhuma faixa disponível…", "Não consegui guardar a playlist no celular.".</summary>
    public string? Mensagem
    {
        get => _mensagem;
        private set
        {
            if (_mensagem == value)
                return;
            _mensagem = value;
            Avisar();
        }
    }

    /// <summary>Uma edição da playlist está andando.</summary>
    public bool Ocupado => _agindo;

    // ------------------------------------------------------------------ editar

    /// <summary>O modo Editar: mostra − para remover e ≡ para ordenar.</summary>
    public bool Editando
    {
        get => _editando;
        private set
        {
            if (_editando == value)
                return;
            _editando = value;
            Avisar();
            foreach (var l in _linhas)
                l.Editando = value;
        }
    }

    public Comando Editar { get; }

    /// <summary>Sai do modo Editar.</summary>
    public Comando Pronto { get; }

    /// <summary>Abre o seletor do Android; as escolhidas entram no fim, na ordem escolhida.</summary>
    public Comando Adicionar { get; }

    private async Task AdicionarAsync()
    {
        if (SemMemoria() is not { } n)
            return;
        // O seletor aberto já conta como edição: um segundo toque não abre outro.
        await EditarPlaylist(async () =>
        {
            var escolhidos = await _seletor.EscolherAsync();
            if (escolhidos.Count == 0)
                return _seletor.Motivo is { } motivo ? ResultadoPlaylist.Falha(motivo) : null; // desistiu: nada muda
            // Escolher de novo devolve o acesso (o seletor guarda a permissão da URI); as que já estão na
            // playlist não entram de novo.
            _audio?.LimparSemAcesso();
            var jaNaLista = _playlists.Playlist(n).Select(f => f.Uri).ToHashSet(StringComparer.Ordinal);
            var novos = escolhidos.Where(a => !jaNaLista.Contains(a.Uri)).ToArray();
            if (novos.Length == 0)
                return new ResultadoPlaylist(true, MsgAcessoDevolvido);
            return await _playlists.AdicionarAsync(n, novos);
        });
    }

    /// <summary>Tira a faixa da playlist. Se é a que toca, o Player para.</summary>
    public async Task RemoverAsync(string id)
    {
        if (SemMemoria() is not { } n)
            return;
        await EditarPlaylist(async () => await _playlists.RemoverAsync(n, id));
    }

    /// <summary>A alça ≡: move a faixa da posição <paramref name="de"/> para a <paramref name="para"/> (1 = a primeira).</summary>
    public async Task MoverAsync(int de, int para)
    {
        if (SemMemoria() is not { } n)
            return;
        if (de == para)
            return;
        await EditarPlaylist(async () => await _playlists.MoverAsync(n, de - 1, para - 1));
    }

    // ------------------------------------------------------------------ tocar

    /// <summary>Tocar/pausar: parado, toca da primeira; tocando, pausa; pausado, continua.</summary>
    public Comando TocarPausar { get; }

    /// <summary>Próxima: a faixa seguinte; depois da última, para.</summary>
    public Comando Proxima { get; }

    private Task TocarPausarAsync()
    {
        // Pausar nunca trava; tocar ou continuar, sim (sem acesso, ou o Bluetooth de áudio desligado).
        if (_player.Estado != EstadoPlayer.Tocando && TravaTocar is { } trava && TemMemoria)
        {
            Mensagem = trava;
            return Task.CompletedTask;
        }
        return Comandar(_player.TocarPausarAsync);
    }

    private Task ProximaAsync() => Comandar(_player.ProximaAsync);

    /// <summary>Tocar numa linha: toca aquela faixa.</summary>
    public Task TocarFaixaAsync(string id)
    {
        if (TravaTocar is { } trava && TemMemoria)
        {
            Mensagem = trava;
            return Task.CompletedTask;
        }
        return Comandar(() => _player.TocarFaixaAsync(id));
    }

    private async Task Comandar(Func<Task> comando)
    {
        if (SemMemoria() is null)
            return;
        Mensagem = null;
        _ultimoAvisoPlayer = null;
        await comando();
        await NaTelaAsync(Atualizar);
    }

    // ------------------------------------------------------------------ apoio

    /// <summary>A memória do celular em uso, ou null com a frase "Sem memória do celular em uso…".</summary>
    private string? SemMemoria()
    {
        if (_player.Memoria is { } n)
            return n;
        Mensagem = Player.MsgSemMemoria;
        return null;
    }

    /// <summary>
    /// Roda uma edição da playlist, uma por vez: com outra andando (ou o seletor aberto), recusa sem
    /// fazer nada. Mostra a frase do resultado; null (desistiu) não muda a frase. A tela se atualiza na sua thread.
    /// </summary>
    private async Task EditarPlaylist(Func<Task<ResultadoPlaylist?>> acao)
    {
        if (_agindo)
            return;
        _agindo = true;
        Avisar(nameof(Ocupado));
        ResultadoPlaylist? resultado = null;
        string? erro = null;
        try
        {
            resultado = await acao();
        }
        catch (Exception e)
        {
            erro = $"Erro inesperado ({e.GetType().Name}).";
        }
        await NaTelaAsync(() =>
        {
            _agindo = false;
            if (erro is not null)
                Mensagem = erro;
            else if (resultado is not null)
                Mensagem = resultado.Mensagem;
            Atualizar();
        });
    }

    private async Task NaTelaAsync(Action acao)
    {
        var feito = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        NaTela(() =>
        {
            try
            {
                acao();
            }
            finally
            {
                feito.SetResult();
            }
        });
        await feito.Task;
    }

    private void AoMudarAlgo() => NaTela(Atualizar);

    private void Atualizar()
    {
        // Outra memória: a frase e o modo Editar da anterior saem.
        if (_player.Memoria != _memoriaVista)
        {
            _memoriaVista = _player.Memoria;
            Mensagem = null;
            Editando = false;
        }

        MontarLinhas();

        var atual = _player.Atual?.Id;
        var extra = TetoDaPlaylist();
        var marcas = _player.Memoria is { } idMemoria ? _setups.Memoria(idMemoria)?.Marcas ?? [] : [];
        foreach (var l in _linhas)
        {
            l.Tocando = l.Id == atual;
            l.Indisponivel = _player.Indisponivel(l.Id);
            l.SemAcesso = _audio?.SemAcesso(l.Uri) == true;
            l.EmAnalise = _analise?.EmAnalise(l.Id) == true;
            // "Em análise" só na faixa nova (que não toca); numa reanálise a faixa segue tocando e fica sem o texto até o fim.
            l.TextoAnalise = l.EmAnalise ? AnalisePlaylist.TextoEmAnalise
                : extra?.FirstOrDefault(f => f.Faixa.Id == l.Id) is { Situacao: not SituacaoFaixa.EmAnalise } fe ? fe.Texto
                : null;
            l.TextoMarca = marcas.LastOrDefault(m => m.IdFaixa == l.Id) is { } marca ? TextoClip(marca) : null;
        }

        // O aviso do fiscal do Extra ("No Extra o volume fica no máximo; use o master.", o master que não desceu) aparece uma vez.
        if (_fiscal?.Aviso is { } avisoFiscal)
        {
            _fiscal.LimparAviso();
            Mensagem = avisoFiscal;
            AvisoRapido?.Invoke(avisoFiscal);
        }

        // Um aviso novo do Player (nenhuma faixa disponível, por exemplo) aparece.
        var aviso = _player.Aviso;
        if (aviso != _ultimoAvisoPlayer)
        {
            _ultimoAvisoPlayer = aviso;
            if (aviso is not null)
                Mensagem = aviso;
        }

        // Sem memória, não há o que editar.
        if (!TemMemoria)
            Editando = false;

        Avisar(nameof(TemMemoria));
        Avisar(nameof(Titulo));
        Avisar(nameof(TextoAviso));
        Avisar(nameof(FaixaAtual));
        Avisar(nameof(Estado));
        Avisar(nameof(TextoTocarPausar));
        Avisar(nameof(Ocupado));
        Avisar(nameof(QuantasSemAcesso));
        Avisar(nameof(BluetoothAudioDesligado));
        Avisar(nameof(SomPeloCelular));
        Avisar(nameof(AvisoTopo));
        Avisar(nameof(TextoAcaoAviso));
        Avisar(nameof(TravaTocar));
        Avisar(nameof(Tocando));
        Avisar(nameof(FolhaOutroPlayer));
    }

    /// <summary>As faixas da playlist no teto do Extra (o peso, a análise ou a falha), ou null sem análise.</summary>
    private IReadOnlyList<FaixaExtra>? TetoDaPlaylist()
    {
        if (_analise is null || _player.Memoria is not { } n)
            return null;
        try
        {
            return _analise.TetoDa(n).Faixas;
        }
        catch (Exception e)
        {
            // Sem a conta, a linha fica sem o peso.
            _log?.Evento($"player: falha ao ler o teto do Extra ({e.GetType().Name})");
            return null;
        }
    }

    /// <summary>O Player está tocando (o botão mostra pausar).</summary>
    public bool Tocando => _player.Estado == EstadoPlayer.Tocando;

    /// <summary>Refaz as linhas a partir do Player, mantendo o mesmo objeto para o mesmo id.</summary>
    private void MontarLinhas()
    {
        var faixas = _player.Faixas;
        var novas = new List<LinhaPlaylistViewModel>(faixas.Count);
        for (var i = 0; i < faixas.Count; i++)
        {
            var f = faixas[i];
            if (!_porId.TryGetValue(f.Id, out var linha))
                _porId[f.Id] = linha = new LinhaPlaylistViewModel(f, this, Despachante, Relogio);
            linha.Posicao = i + 1;
            linha.Editando = _editando;
            novas.Add(linha);
        }
        foreach (var id in _porId.Keys.Where(id => faixas.All(f => f.Id != id)).ToArray())
            _porId.Remove(id);

        if (_linhas.Count == novas.Count && _linhas.Zip(novas).All(par => ReferenceEquals(par.First, par.Second)))
            return;
        _linhas = novas;
        Avisar(nameof(Linhas));
    }

    public void Dispose()
    {
        _player.Mudou -= AoMudarAlgo;
        _playlists.Mudou -= AoMudarAlgo;
        _setups.Mudou -= AoMudarAlgo;
        if (_aparelho is not null)
            _aparelho.PropertyChanged -= AoMudarAparelho;
        if (_audio is not null)
            _audio.Mudou -= AoMudarAlgo;
        if (_analise is not null)
            _analise.Mudou -= AoMudarAlgo;
        if (_fiscal is not null)
            _fiscal.Mudou -= AoMudarAlgo;
    }
}
