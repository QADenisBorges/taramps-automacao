namespace BgsControle.Nucleo.Player;

/// <summary>
/// A porta para quem lê o áudio de uma música do celular em PCM (história 13d, a análise da playlist): no app, o
/// <c>MediaExtractor</c> com o <c>MediaCodec</c> do Android; nos testes, faixas sintéticas. A música não toca: só as
/// amostras passam, do começo ao fim.
/// </summary>
public interface IDecodificadorAudio
{
    /// <summary>
    /// Lê a música inteira e entrega o PCM em blocos, na ordem: as amostras intercaladas em ±1,0 (0 dBFS), o número de
    /// canais (1 = mono) e a taxa em Hz. Uma falha (o arquivo sumiu, o formato não abre) lança
    /// <see cref="ErroDecodificacao"/> com o motivo; o cancelamento lança <see cref="OperationCanceledException"/>.
    /// </summary>
    Task LerAsync(string uri, Action<float[], int, int> bloco, CancellationToken cancelar);
}

/// <summary>A música não pôde ser lida, com o motivo para a tela ("o arquivo não foi encontrado").</summary>
public sealed class ErroDecodificacao(string motivo) : Exception(motivo)
{
    public const string MsgArquivo = "o arquivo não foi encontrado";
    public const string MsgFormato = "o formato do áudio não abre";
    public const string MsgTaxa = "a taxa do áudio mudou no meio da faixa";

    /// <summary>O motivo, sem ponto final ("o arquivo não foi encontrado").</summary>
    public string Motivo => Message;
}
