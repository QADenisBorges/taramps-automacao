using BgsControle.Nucleo.Seguranca;

namespace BgsControle.Telas;

/// <summary>As chaves de acesso em JSON, nos arquivos privados do app (allowBackup=false).</summary>
public sealed class RepositorioChavesArquivos(IArquivosApp arquivos) : IRepositorioChaves
{
    public const string Arquivo = "chaves-de-acesso.json";

    public Task<string?> LerAsync() => arquivos.LerTextoAsync(Arquivo);

    public Task GravarAsync(string json) => arquivos.GravarTextoAsync(Arquivo, json);
}
