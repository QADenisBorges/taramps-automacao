using BgsControle.Nucleo.Escrita;

namespace BgsControle.Telas;

/// <summary>Um texto do núcleo (a cópia antes, o link, os presets, as playlists, o histórico, o modo do toque, o cliente da porta da IA) num arquivo dos arquivos privados do app (allowBackup=false).</summary>
public sealed class RepositorioTextoArquivos(IArquivosApp arquivos, string arquivo) : IRepositorioTexto
{
    /// <summary>A cópia antes de mexer e a marca da operação em andamento.</summary>
    public const string ArquivoCopia = "copia-antes.json";

    /// <summary>O link 1–2 e 3–4.</summary>
    public const string ArquivoLink = "link-canais.json";

    /// <summary>Os presets próprios do celular (os herdados vêm do código).</summary>
    public const string ArquivoPresets = "presets.json";

    /// <summary>As 3 playlists do Player, uma por posição de memória (só as URIs; a música fica onde está).</summary>
    public const string ArquivoPlaylists = "playlists.json";

    /// <summary>O histórico dos comandos (as últimas 1000 entradas, sem senhas).</summary>
    public const string ArquivoHistorico = "historico.json";

    /// <summary>O modo do toque durante a IA ("Controles travados até PARAR IA" ou "O toque pausa a IA").</summary>
    public const string ArquivoUsoIa = "uso-ia.json";

    /// <summary>O cliente pareado da porta da IA: o nome, a data e só o SHA-256 da chave.</summary>
    public const string ArquivoPortaIa = "porta-ia.json";

    public string Arquivo { get; } = arquivo;

    public Task<string?> LerAsync() => arquivos.LerTextoAsync(Arquivo);

    public Task GravarAsync(string conteudo) => arquivos.GravarTextoAsync(Arquivo, conteudo);
}
