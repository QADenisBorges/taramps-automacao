namespace BgsControle.Nucleo.Seguranca;

/// <summary>
/// Onde a senha do processador fica guardada no celular. No app é o SecureStorage do MAUI
/// (cifrado pelo Android Keystore). A sessão só guarda uma senha depois de aceita.
/// </summary>
public interface ICofreSenha
{
    /// <summary>
    /// A senha guardada, ou null se não houver. Um erro ao ler (Keystore) conta como
    /// "sem senha guardada", e a entrada é apagada.
    /// </summary>
    Task<string?> LerAsync();

    Task GuardarAsync(string senha);

    Task ApagarAsync();
}
