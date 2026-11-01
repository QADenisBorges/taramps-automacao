namespace BgsControle.Nucleo.Player;

/// <summary>
/// O volume de mídia do celular (história 13e, o fiscal do Extra): no Extra ele fica no máximo, o valor da conta do
/// nível do Bluetooth (medido a 100 %). No Android, o <c>AudioManager</c> e um <c>ContentObserver</c>; nos testes, um dublê.
/// </summary>
public interface IVolumeCelular
{
    /// <summary>O volume de mídia agora, de 0 a <see cref="Maximo"/>.</summary>
    int Volume { get; }

    /// <summary>O volume de mídia no máximo (100 %).</summary>
    int Maximo { get; }

    /// <summary>
    /// Põe o volume de mídia (0 a <see cref="Maximo"/>). False: ele não chegou ao pedido (o limite de volume seguro do
    /// Android ou o "Não perturbe"), e quem pediu avisa.
    /// </summary>
    Task<bool> DefinirAsync(int volume);

    /// <summary>O volume de mídia mudou (pelo usuário, por outro app ou pelo próprio app). Pode chegar em qualquer thread.</summary>
    event Action? Mudou;
}
