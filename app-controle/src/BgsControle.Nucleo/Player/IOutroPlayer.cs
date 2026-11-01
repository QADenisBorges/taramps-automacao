namespace BgsControle.Nucleo.Player;

/// <summary>
/// Outro app do celular começou a tocar (história 13e, o fiscal do Extra, spec-rendimento CAP-8). No Android, as
/// configurações de reprodução do <c>AudioManager</c> e a perda do foco de áudio; nos testes, um dublê.
/// </summary>
public interface IOutroPlayer
{
    /// <summary>Outro app (não o player do app) começou a tocar. Pode chegar em qualquer thread.</summary>
    event Action? Comecou;
}
