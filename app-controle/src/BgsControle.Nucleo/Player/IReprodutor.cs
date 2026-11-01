namespace BgsControle.Nucleo.Player;

/// <summary>
/// A porta para quem toca o áudio no celular (no app, o <c>MediaPlayer</c> do Android, história 11).
/// O som sai pelo Bluetooth de áudio até o processador; nada passa pelo BLE.
/// </summary>
public interface IReprodutor
{
    /// <summary>
    /// Começa a tocar o arquivo do início, no lugar do que tocava. False = o arquivo não abriu
    /// (sumiu do celular ou não é áudio): nada toca.
    /// </summary>
    Task<bool> TocarAsync(string uri);

    /// <summary>Pausa onde está.</summary>
    Task PausarAsync();

    /// <summary>Continua de onde pausou. False = não deu para continuar (o tocador se perdeu): fica parado.</summary>
    Task<bool> ContinuarAsync();

    /// <summary>Para e solta o arquivo.</summary>
    Task PararAsync();

    /// <summary>
    /// Toca um trecho do arquivo (história 13e, o teste a seco), no lugar do que tocava: do <paramref name="inicio"/> por
    /// <paramref name="duracao"/>, e para no fim. Completa quando o trecho termina (true) ou quando o arquivo não abre,
    /// falha no meio, o reprodutor para (<see cref="PararAsync"/>) ou o <paramref name="cancelar"/> pede (false). No
    /// Android, o <c>SeekTo</c> e uma parada no fim do trecho.
    /// </summary>
    Task<bool> TocarTrechoAsync(string uri, TimeSpan inicio, TimeSpan duracao, CancellationToken cancelar = default);

    /// <summary>Onde está a faixa (ou o trecho) que toca ou está pausada, ou null sem nada aberto (história 13e: a marca do clip).</summary>
    TimeSpan? Posicao { get; }

    /// <summary>A faixa com esta URI chegou ao fim sozinha. Pode chegar em qualquer thread.</summary>
    event Action<string>? Terminou;

    /// <summary>
    /// O reprodutor pausou sozinho a faixa com esta URI (o Bluetooth de áudio caiu e o som iria para o
    /// alto-falante do celular, ou outro app tomou o foco de áudio). Pode chegar em qualquer thread.
    /// </summary>
    event Action<string>? Pausou;

    /// <summary>A faixa com esta URI falhou no meio (não é fim de faixa). Pode chegar em qualquer thread.</summary>
    event Action<string>? Falhou;
}
