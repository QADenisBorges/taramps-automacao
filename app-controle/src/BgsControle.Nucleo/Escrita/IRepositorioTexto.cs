namespace BgsControle.Nucleo.Escrita;

/// <summary>
/// Um texto guardado no celular (no app, um arquivo dos arquivos privados): a cópia antes de
/// mexer e o link de canais.
/// </summary>
public interface IRepositorioTexto
{
    /// <summary>O texto guardado, ou null se não há.</summary>
    Task<string?> LerAsync();

    /// <summary>Grava o texto, substituindo o que havia.</summary>
    Task GravarAsync(string conteudo);
}

internal static class Gravacao
{
    /// <summary>Tentativas de gravar antes de desistir (como as chaves de acesso).</summary>
    public const int Tentativas = 2;

    /// <summary>Grava, com uma nova tentativa antes de desistir. False = não gravou.</summary>
    public static async Task<bool> GravarAsync(IRepositorioTexto repositorio, string conteudo)
    {
        for (var tentativa = 1; tentativa <= Tentativas; tentativa++)
        {
            try
            {
                await repositorio.GravarAsync(conteudo);
                return true;
            }
            catch (Exception)
            {
                // Tenta de novo; depois da última, desiste.
            }
        }
        return false;
    }

    /// <summary>Lê; qualquer erro conta como "não há".</summary>
    public static async Task<string?> LerAsync(IRepositorioTexto repositorio)
    {
        try
        {
            return await repositorio.LerAsync();
        }
        catch (Exception)
        {
            return null;
        }
    }
}
