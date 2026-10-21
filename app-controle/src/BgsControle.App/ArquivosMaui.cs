using BgsControle.Telas;

namespace BgsControle.App;

/// <summary>Os arquivos privados do app (<c>FileSystem.AppDataDirectory</c>; o manifesto tem allowBackup=false).</summary>
public sealed class ArquivosMaui : IArquivosApp
{
    public Task GravarTextoAsync(string nome, string conteudo) =>
        File.WriteAllTextAsync(Caminho(nome), conteudo);

    public async Task<string?> LerTextoAsync(string nome)
    {
        var caminho = Caminho(nome);
        return File.Exists(caminho) ? await File.ReadAllTextAsync(caminho) : null;
    }

    private static string Caminho(string nome)
    {
        if (string.IsNullOrWhiteSpace(nome) || Path.GetFileName(nome) != nome)
            throw new ArgumentException("Só o nome do arquivo, sem pasta.", nameof(nome));
        return Path.Combine(FileSystem.AppDataDirectory, nome);
    }
}
