using Android.Content;
using Android.Media;
using BgsControle.Nucleo.Player;

namespace BgsControle.App.Plataforma;

/// <summary>
/// O tom do celular da calibração guiada (história 13b): um <c>AudioTrack</c> em modo estático com um segundo de
/// seno a 0 dBFS (PCM de 16 bits, 48 kHz, mono; o Android leva aos dois lados do Bluetooth). A frequência é
/// inteira, então um segundo tem um número inteiro de ciclos e o laço não estala; o laço repete o segundo até a
/// duração. Enquanto toca, pede o foco de áudio exclusivo e passageiro (outro som não se mistura à medida) e o
/// devolve ao parar. Sai pelo que o Android estiver usando (com o celular no processador, o Bluetooth de áudio).
/// Um pedido de cada vez: parar o tom antigo e pôr o novo ficam sob a mesma trava, e nada vaza. Nunca lança: o
/// que não abre devolve false.
/// </summary>
public sealed class TomCelularAndroid : ITomCelular
{
    public const int TaxaAmostragem = 48000;

    private readonly AudioManager? _gerente;
    private readonly AudioAttributes _atributos;
    private readonly AudioFocusRequestClass _foco;
    private readonly object _trava = new();
    private AudioTrack? _faixa;
    private bool _comFoco;

    public TomCelularAndroid(Context contexto)
    {
        _gerente = (AudioManager?)contexto.GetSystemService(Context.AudioService);
        _atributos = new AudioAttributes.Builder()
            .SetUsage(AudioUsageKind.Media)!
            .SetContentType(AudioContentType.Music)!
            .Build()!;
        _foco = new AudioFocusRequestClass.Builder(AudioFocus.GainTransientExclusive)
            .SetAudioAttributes(_atributos)!
            .Build()!;
    }

    /// <summary>O tom do celular está tocando (história 13e: o fiscal do Extra não o conta como outro app).</summary>
    internal bool Tocando
    {
        get
        {
            lock (_trava)
                return _faixa is not null;
        }
    }

    public Task<bool> TocarAsync(int hz, TimeSpan duracao)
    {
        lock (_trava)
        {
            PararSemTrava();
            if (hz <= 0 || hz >= TaxaAmostragem / 2 || duracao <= TimeSpan.Zero)
                return Task.FromResult(false);
            AudioTrack? faixa = null;
            try
            {
                var amostras = new short[TaxaAmostragem];
                for (var i = 0; i < amostras.Length; i++)
                    amostras[i] = (short)Math.Round(short.MaxValue * Math.Sin(2 * Math.PI * hz * i / TaxaAmostragem));

                var formato = new AudioFormat.Builder()
                    .SetEncoding(Encoding.Pcm16bit)!
                    .SetSampleRate(TaxaAmostragem)!
                    .SetChannelMask(ChannelOut.Mono)!
                    .Build()!;
                faixa = new AudioTrack.Builder()
                    .SetAudioAttributes(_atributos)
                    .SetAudioFormat(formato)
                    .SetBufferSizeInBytes(amostras.Length * sizeof(short))
                    .SetTransferMode(AudioTrackMode.Static)
                    .Build();
                if (faixa.Write(amostras, 0, amostras.Length) != amostras.Length)
                    return Task.FromResult(Soltar(faixa));
                // O primeiro segundo e mais (segundos − 1) voltas do laço.
                var segundos = Math.Max(1, (int)Math.Ceiling(duracao.TotalSeconds));
                if (segundos > 1 && faixa.SetLoopPoints(0, amostras.Length, segundos - 1) != TrackStatus.Success)
                    return Task.FromResult(Soltar(faixa));
                _comFoco = _gerente?.RequestAudioFocus(_foco) == AudioFocusRequest.Granted;
                faixa.Play();
                _faixa = faixa;
                return Task.FromResult(true);
            }
            catch (Exception)
            {
                if (faixa is not null)
                    Soltar(faixa);
                DevolverFoco();
                return Task.FromResult(false);
            }
        }
    }

    public Task PararAsync()
    {
        lock (_trava)
            PararSemTrava();
        return Task.CompletedTask;
    }

    /// <summary>Para e solta a faixa (se havia) e devolve o foco. Chame com a trava.</summary>
    private void PararSemTrava()
    {
        var faixa = _faixa;
        _faixa = null;
        if (faixa is not null)
        {
            try
            {
                faixa.Stop();
            }
            catch (Exception)
            {
                // Já parado: só solta.
            }
            Soltar(faixa);
        }
        DevolverFoco();
    }

    private void DevolverFoco()
    {
        if (!_comFoco)
            return;
        _comFoco = false;
        try
        {
            _gerente?.AbandonAudioFocusRequest(_foco);
        }
        catch (Exception)
        {
            // O foco volta sozinho quando o app sai.
        }
    }

    /// <summary>Solta a faixa sem lançar. Sempre false (o tom não tocou).</summary>
    private static bool Soltar(AudioTrack faixa)
    {
        try
        {
            faixa.Release();
        }
        catch (Exception)
        {
            // Nada mais a fazer.
        }
        return false;
    }
}
