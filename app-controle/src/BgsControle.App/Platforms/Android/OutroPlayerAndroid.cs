using Android.Content;
using Android.Media;
using BgsControle.Nucleo.Player;

namespace BgsControle.App.Plataforma;

/// <summary>
/// Outro app do celular começou a tocar (história 13e, o fiscal do Extra), por dois caminhos:
/// <list type="bullet">
/// <item>a perda do foco de áudio de vez no reprodutor do Player (outro app de mídia pediu o foco);</item>
/// <item>as configurações de reprodução do <see cref="AudioManager"/>: só as de mídia e de jogo (sons de notificação,
/// toques e navegação ficam de fora); o aviso sai quando as que não são do app sobem. As do app (o MediaPlayer do Player
/// tocando e o tom da calibração) saem pelo estado delas, não pela contagem.</item>
/// </list>
/// [ASSUMPTION: a lista de configurações ativas traz os players em uso de outros apps; a 12d confere no celular.]
/// </summary>
public sealed class OutroPlayerAndroid : IOutroPlayer
{
    private readonly AudioManager? _gerente;
    private readonly ReprodutorAndroid _reprodutor;
    private readonly TomCelularAndroid _tom;
    private readonly object _trava = new();
    private int _outrosAntes;

    public OutroPlayerAndroid(Context contexto, ReprodutorAndroid reprodutor, TomCelularAndroid tom)
    {
        _reprodutor = reprodutor;
        _tom = tom;
        _gerente = (AudioManager?)contexto.GetSystemService(Context.AudioService);
        try
        {
            _outrosAntes = Outros(_gerente?.ActivePlaybackConfigurations);
            _gerente?.RegisterAudioPlaybackCallback(new Ouvinte(this), null);
        }
        catch (Exception)
        {
            // Sem as configurações de reprodução, fica a perda do foco.
        }
        _reprodutor.PerdeuFoco += Avisar;
    }

    public event Action? Comecou;

    /// <summary>As configurações de mídia e de jogo que não são do app.</summary>
    private int Outros(IList<AudioPlaybackConfiguration>? configs)
    {
        var midia = configs?.Count(c => c.AudioAttributes?.Usage is AudioUsageKind.Media or AudioUsageKind.Game) ?? 0;
        var nossos = (_reprodutor.Tocando ? 1 : 0) + (_tom.Tocando ? 1 : 0);
        return Math.Max(0, midia - nossos);
    }

    private void AoMudar(IList<AudioPlaybackConfiguration>? configs)
    {
        var outros = Outros(configs);
        bool novo;
        lock (_trava)
        {
            novo = outros > _outrosAntes;
            _outrosAntes = outros;
        }
        if (novo)
            Avisar();
    }

    private void Avisar()
    {
        try
        {
            Comecou?.Invoke();
        }
        catch (Exception)
        {
            // Quem ouve não derruba o aviso do Android.
        }
    }

    private sealed class Ouvinte(OutroPlayerAndroid dono) : AudioManager.AudioPlaybackCallback
    {
        public override void OnPlaybackConfigChanged(IList<AudioPlaybackConfiguration>? configs) => dono.AoMudar(configs);
    }
}
