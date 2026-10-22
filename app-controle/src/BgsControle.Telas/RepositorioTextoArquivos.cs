using BgsControle.Nucleo.Escrita;

namespace BgsControle.Telas;

/// <summary>Um texto do núcleo (a cópia antes, o link) num arquivo dos arquivos privados do app (allowBackup=false).</summary>
public sealed class RepositorioTextoArquivos(IArquivosApp arquivos, string arquivo) : IRepositorioTexto
{
    /// <summary>A cópia antes de mexer e a marca da operação em andamento.</summary>
    public const string ArquivoCopia = "copia-antes.json";

    /// <summary>O link 1–2 e 3–4.</summary>
    public const string ArquivoLink = "link-canais.json";

    public string Arquivo { get; } = arquivo;

    public Task<string?> LerAsync() => arquivos.LerTextoAsync(Arquivo);

    public Task GravarAsync(string conteudo) => arquivos.GravarTextoAsync(Arquivo, conteudo);
}
