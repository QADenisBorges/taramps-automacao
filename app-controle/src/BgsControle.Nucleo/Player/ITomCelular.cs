namespace BgsControle.Nucleo.Player;

/// <summary>
/// Um tom puro tocado pelo próprio celular (história 13b, a medida do nível do Bluetooth): um seno a 0 dBFS
/// que sai pelo Bluetooth de áudio até o processador. No app é um <c>AudioTrack</c> do Android (PCM de 16 bits
/// a 48 kHz); nos testes, um dublê que registra os pedidos.
/// </summary>
public interface ITomCelular
{
    /// <summary>
    /// Toca o seno na frequência, a 0 dBFS, no lugar do que este tom tocava, e para sozinho depois da duração.
    /// False = não deu para tocar (o áudio do celular não abriu).
    /// </summary>
    Task<bool> TocarAsync(int hz, TimeSpan duracao);

    /// <summary>Para o tom (se tocava). Nunca falha.</summary>
    Task PararAsync();
}
