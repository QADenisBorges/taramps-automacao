using BgsControle.Telas;

namespace BgsControle.App;

/// <summary>
/// O Exportar dos presets (história 11): grava o JSON no cache do app e abre o compartilhar do Android
/// (salvar no Drive, mandar por mensagem…). Os exportados antes são apagados. Nada vai pelo BLE.
/// </summary>
public sealed class ExportarMaui : IExportarArquivo
{
    public async Task<string?> ExportarAsync(string nomeArquivo, string conteudo)
    {
        if (string.IsNullOrWhiteSpace(nomeArquivo) || Path.GetFileName(nomeArquivo) != nomeArquivo)
            return "Nome de arquivo inválido.";
        // Uma pasta só dos exportados, limpa a cada exportação: o cache não acumula arquivos velhos.
        var pasta = Path.Combine(FileSystem.CacheDirectory, "exportados");
        var caminho = Path.Combine(pasta, nomeArquivo);
        try
        {
            if (Directory.Exists(pasta))
            {
                foreach (var velho in Directory.GetFiles(pasta))
                    File.Delete(velho);
            }
            Directory.CreateDirectory(pasta);
            await File.WriteAllTextAsync(caminho, conteudo);
            await MainThread.InvokeOnMainThreadAsync(() => Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = nomeArquivo,
                File = new ShareFile(caminho, "application/json"),
            }));
            return null;
        }
        catch (Exception e)
        {
            return $"Não consegui exportar ({e.GetType().Name}).";
        }
    }
}
