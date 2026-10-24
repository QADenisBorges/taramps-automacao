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
