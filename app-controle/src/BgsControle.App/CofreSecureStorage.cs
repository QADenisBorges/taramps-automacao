using BgsControle.Nucleo.Seguranca;

namespace BgsControle.App;

/// <summary>
/// A senha no SecureStorage do MAUI, cifrada pelo Android Keystore. O manifesto tem
/// allowBackup=false. Um erro do Keystore ao ler conta como "sem senha" e apaga a entrada.
/// </summary>
public sealed class CofreSecureStorage : ICofreSenha
{
    private const string Chave = "senha_processador";

    public async Task<string?> LerAsync()
    {
        try
        {
            return await SecureStorage.Default.GetAsync(Chave);
        }
        catch (Exception)
        {
            Apagar();
            return null;
        }
    }

    public Task GuardarAsync(string senha) => SecureStorage.Default.SetAsync(Chave, senha);

    public Task ApagarAsync()
    {
        Apagar();
        return Task.CompletedTask;
    }

    private static void Apagar()
    {
        try
        {
            SecureStorage.Default.Remove(Chave);
        }
        catch (Exception)
        {
            // Sem o que apagar.
        }
    }
}
