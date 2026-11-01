using Android.Content;
using Android.Media;
using BgsControle.Nucleo.Player;

namespace BgsControle.App.Plataforma;

/// <summary>
/// O decodificador real da análise da playlist (história 13d): o <c>MediaExtractor</c> acha a trilha de áudio da URI de
/// conteúdo guardada (o app não copia a música) e o <c>MediaCodec</c> a decodifica em PCM, do começo ao fim, fora da
/// thread da tela. O app pede a saída em float (os picos acima de 0 dBFS de uma faixa com perda não são cortados); se o
/// codec der outra, o de 16 bits é dividido por 32768 e o de 8 bits centrado em 128; qualquer outra (24 bits, inteiro de 32)
/// recusa a faixa. Mono vira L = R; acima de 2 canais, ficam os 2 primeiros. Nada toca. Uma falha (o arquivo sumiu, sem
/// acesso, um formato que não abre, o codec que não termina) vira <see cref="ErroDecodificacao"/> com o motivo; o
/// cancelamento para entre os blocos.
/// </summary>
public sealed class DecodificadorAndroid(Context contexto) : IDecodificadorAudio
{
    /// <summary>Quanto o codec espera por um buffer, em microssegundos.</summary>
    private const long EsperaUs = 10_000;

    /// <summary>Depois do fim da entrada, quantas esperas seguidas sem saída (de <see cref="EsperaUs"/>) antes de desistir (~5 s).</summary>
    private const int EsperasNoFim = 500;

    public Task LerAsync(string uri, Action<float[], int, int> bloco, CancellationToken cancelar) =>
        Task.Run(() => Ler(uri, bloco, cancelar), cancelar);

    private void Ler(string uri, Action<float[], int, int> bloco, CancellationToken cancelar)
    {
        using var extrator = new MediaExtractor();
        try
        {
            extrator.SetDataSource(contexto, Android.Net.Uri.Parse(uri)!, null);
        }
        catch (Java.Lang.Exception)
        {
            throw new ErroDecodificacao(ErroDecodificacao.MsgArquivo);
        }
        catch (IOException)
        {
            throw new ErroDecodificacao(ErroDecodificacao.MsgArquivo);
        }

        var (trilha, formato, mime) = TrilhaDeAudio(extrator);
        if (trilha < 0 || formato is null || mime is null)
            throw new ErroDecodificacao(ErroDecodificacao.MsgFormato);
        extrator.SelectTrack(trilha);

        MediaCodec codec;
        try
        {
            codec = MediaCodec.CreateDecoderByType(mime);
        }
        catch (Java.Lang.Exception)
        {
            throw new ErroDecodificacao(ErroDecodificacao.MsgFormato);
        }
        try
        {
            // A saída em float: os picos acima de 0 dBFS (comuns em MP3 e AAC) passam sem corte.
            formato.SetInteger(MediaFormat.KeyPcmEncoding, (int)Encoding.PcmFloat);
            codec.Configure(formato, null, null, MediaCodecConfigFlags.None);
            codec.Start();
        }
        catch (Java.Lang.Exception)
        {
            codec.Release();
            codec.Dispose();
            throw new ErroDecodificacao(ErroDecodificacao.MsgFormato);
        }

        try
        {
            Decodificar(extrator, codec, formato, bloco, cancelar);
        }
        catch (Java.Lang.Exception)
        {
            throw new ErroDecodificacao(ErroDecodificacao.MsgFormato);
        }
        finally
        {
            try
            {
                codec.Stop();
            }
            catch (Java.Lang.Exception)
            {
                // O codec já estava parado ou com erro; só falta soltar.
            }
            codec.Release();
            codec.Dispose();
        }
    }

    private static (int Trilha, MediaFormat? Formato, string? Mime) TrilhaDeAudio(MediaExtractor extrator)
    {
        for (var i = 0; i < extrator.TrackCount; i++)
        {
            var formato = extrator.GetTrackFormat(i);
            var mime = formato.GetString(MediaFormat.KeyMime);
            if (mime is not null && mime.StartsWith("audio/", StringComparison.Ordinal))
                return (i, formato, mime);
        }
        return (-1, null, null);
    }

    private static void Decodificar(MediaExtractor extrator, MediaCodec codec, MediaFormat formatoEntrada, Action<float[], int, int> bloco,
        CancellationToken cancelar)
    {
        var info = new MediaCodec.BufferInfo();
        var canais = Inteiro(formatoEntrada, MediaFormat.KeyChannelCount, 2);
        var taxa = Inteiro(formatoEntrada, MediaFormat.KeySampleRate, 44100);
        var codificacao = Inteiro(formatoEntrada, MediaFormat.KeyPcmEncoding, (int)Encoding.Pcm16bit);
        var fimEntrada = false;
        var esperasNoFim = 0;
        while (true)
        {
            cancelar.ThrowIfCancellationRequested();
            if (!fimEntrada)
            {
                var i = codec.DequeueInputBuffer(EsperaUs);
                if (i >= 0)
                {
                    var entrada = codec.GetInputBuffer(i)!;
                    var n = extrator.ReadSampleData(entrada, 0);
                    if (n < 0)
                    {
                        codec.QueueInputBuffer(i, 0, 0, 0, MediaCodecBufferFlags.EndOfStream);
                        fimEntrada = true;
                    }
                    else
                    {
                        codec.QueueInputBuffer(i, 0, n, extrator.SampleTime, MediaCodecBufferFlags.None);
                        extrator.Advance();
                    }
                }
            }

            var o = codec.DequeueOutputBuffer(info, EsperaUs);
            if (o == (int)MediaCodecInfoState.OutputFormatChanged)
            {
                var saida = codec.OutputFormat;
                canais = Inteiro(saida, MediaFormat.KeyChannelCount, canais);
                taxa = Inteiro(saida, MediaFormat.KeySampleRate, taxa);
                codificacao = Inteiro(saida, MediaFormat.KeyPcmEncoding, codificacao);
                continue;
            }
            if (o < 0)
            {
                // Depois do fim da entrada, um codec que não devolve mais nada (nem o fim) não prende a fila para sempre.
                if (fimEntrada && o == (int)MediaCodecInfoState.TryAgainLater && ++esperasNoFim > EsperasNoFim)
                    throw new ErroDecodificacao(ErroDecodificacao.MsgFormato);
                continue;
            }
            esperasNoFim = 0;

            if (info.Size > 0)
            {
                var buffer = codec.GetOutputBuffer(o)!;
                buffer.Position(info.Offset);
                buffer.Limit(info.Offset + info.Size);
                var bytes = new byte[info.Size];
                buffer.Get(bytes);
                var amostras = ParaFloat(bytes, codificacao);
                var estereo = ParaEstereo(amostras, canais);
                if (estereo.Length > 0)
                    bloco(estereo, 2, taxa);
            }
            codec.ReleaseOutputBuffer(o, false);
            if ((info.Flags & MediaCodecBufferFlags.EndOfStream) != 0)
                return;
        }
    }

    private static int Inteiro(MediaFormat formato, string chave, int padrao) =>
        formato.ContainsKey(chave) ? formato.GetInteger(chave) : padrao;

    /// <summary>Os bytes do codec em amostras em ±1,0 (float, 16 bits ou 8 bits); outra codificação recusa a faixa.</summary>
    private static float[] ParaFloat(byte[] bytes, int codificacao)
    {
        if (codificacao == (int)Encoding.PcmFloat)
        {
            var f = new float[bytes.Length / 4];
            Buffer.BlockCopy(bytes, 0, f, 0, f.Length * 4);
            return f;
        }
        if (codificacao == (int)Encoding.Pcm8bit)
            return bytes.Select(b => (b - 128) / 128f).ToArray();
        if (codificacao != (int)Encoding.Pcm16bit)
            throw new ErroDecodificacao(ErroDecodificacao.MsgFormato);
        var s = new float[bytes.Length / 2];
        for (var i = 0; i < s.Length; i++)
            s[i] = BitConverter.ToInt16(bytes, i * 2) / 32768f;
        return s;
    }

    /// <summary>Intercalado em 2 canais: mono vira L = R; acima de 2, ficam os 2 primeiros.</summary>
    private static float[] ParaEstereo(float[] amostras, int canais)
    {
        if (canais == 2)
            return amostras;
        if (canais < 1)
            return [];
        var quadros = amostras.Length / canais;
        var estereo = new float[quadros * 2];
        for (var i = 0; i < quadros; i++)
        {
            estereo[2 * i] = amostras[i * canais];
            estereo[2 * i + 1] = canais == 1 ? amostras[i] : amostras[i * canais + 1];
        }
        return estereo;
    }
}
