using BgsControle.Nucleo.Player;
using BgsControle.Nucleo.Rendimento;
using BgsControle.Nucleo.Setups;

namespace BgsControle.Telas;

/// <summary>
/// "Otimizar configuração na playlist" (história 13d; <c>telas-rendimento.md</c>, "Otimizar na playlist"; sem XAML, que é
/// da 13g), no Player, na playlist da memória: o comando, o progresso da análise sem números (o anel duplo, enquanto
/// <see cref="Analisando"/>), o resultado ("Teto do Extra: −4,9 dB. Falta o teste a seco.") e o cancelar (a memória deixa
/// de ser otimizada e a análise em curso para, sem gravar nada). A tela e o player seguem livres durante a análise. A
/// análise não muda o som: aplicar o teto é da 13e e da 13f.
/// </summary>
public sealed class OtimizarViewModel : TelaBase, IDisposable
{
    public const string TextoOtimizarBotao = "Otimizar configuração na playlist";
    public const string TextoCancelarBotao = "Cancelar";

    private readonly AnalisePlaylist _analise;
    private readonly GerenteSetups _setups;
    private readonly Player _player;
    private string? _mensagem;
    private bool _descartado;

    public OtimizarViewModel(AnalisePlaylist analise, GerenteSetups setups, Player player, IDespachante despachante, TimeProvider relogio)
        : base(despachante, relogio)
    {
        _analise = analise;
        _setups = setups;
        _player = player;
        Otimizar = new Comando(OtimizarAsync);
        Cancelar = new Comando(CancelarAsync);
        _analise.Mudou += AoMudar;
        _setups.Mudou += AoMudar;
        _player.Mudou += AoMudar;
    }

    /// <summary>"Otimizar configuração na playlist".</summary>
    public string TextoOtimizar => TextoOtimizarBotao;

    /// <summary>"Cancelar".</summary>
    public string TextoCancelar => TextoCancelarBotao;

    /// <summary>A memória da playlist do Player (o id), ou null.</summary>
    private string? IdMemoria => _player.Memoria;

    /// <summary>Há uma playlist de memória no Player: o comando aparece.</summary>
    public bool TemMemoria => IdMemoria is not null;

    /// <summary>A memória da playlist já é otimizada (a análise segue sozinha a cada faixa nova).</summary>
    public bool Otimizada => _setups.Memoria(IdMemoria)?.Otimizada == true;

    /// <summary>A análise anda: o anel duplo, sem números.</summary>
    public bool Analisando => Otimizada && _analise.Analisando(IdMemoria);

    /// <summary>O cancelar aparece enquanto a análise anda.</summary>
    public bool PodeCancelar => Analisando;

    private TetoExtra? Teto
    {
        get
        {
            try
            {
                return IdMemoria is { } id ? _analise.TetoDa(id) : null;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }

    /// <summary>"Teto do Extra: −4,9 dB. Falta o teste a seco." com a análise pronta, ou null.</summary>
    public string? Resultado => Analisando ? null : Teto?.Resultado;

    /// <summary>
    /// Por que não há teto com a memória otimizada e a análise parada ("Nenhuma faixa da playlist foi analisada.", "Calibre
    /// a saída JBL L.", "A memória não lembra um preset.", o dado da cadeia que falta), ou null.
    /// </summary>
    public string? Motivo => !Otimizada || Analisando ? null : Teto is { Resultado: null } t ? t.Motivo : null;

    /// <summary>"O nível não chega ao teto: falta ganho na memória. Saída: JBL L." com a análise pronta, ou null.</summary>
    public string? Aviso => Analisando ? null : Teto?.Aviso;

    /// <summary>A frase do último toque ("Calibre a saída JBL L."), ou null.</summary>
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

    /// <summary>Começa a análise da playlist (volta logo: ela anda em segundo plano).</summary>
    public Comando Otimizar { get; }

    /// <summary>Para a análise: a memória deixa de ser otimizada, e nada da análise pela metade é gravado.</summary>
    public Comando Cancelar { get; }

    private async Task OtimizarAsync()
    {
        if (IdMemoria is not { } id)
        {
            Mensagem = Player.MsgSemMemoria;
            return;
        }
        var r = await _analise.OtimizarAsync(id);
        NaTela(() =>
        {
            Mensagem = r.Ok ? null : r.Mensagem ?? r.Motivo;
            Atualizar();
        });
    }

    private async Task CancelarAsync()
    {
        if (IdMemoria is not { } id)
            return;
        var r = await _analise.DesotimizarAsync(id);
        NaTela(() =>
        {
            Mensagem = r.Ok ? null : r.Mensagem ?? r.Motivo;
            Atualizar();
        });
    }

    private void AoMudar() => NaTela(Atualizar);

    private void Atualizar()
    {
        if (_descartado)
            return;
        Avisar(nameof(TemMemoria));
        Avisar(nameof(Otimizada));
        Avisar(nameof(Analisando));
        Avisar(nameof(PodeCancelar));
        Avisar(nameof(Resultado));
        Avisar(nameof(Motivo));
        Avisar(nameof(Aviso));
    }

    public void Dispose()
    {
        _descartado = true;
        _analise.Mudou -= AoMudar;
        _setups.Mudou -= AoMudar;
        _player.Mudou -= AoMudar;
    }
}
