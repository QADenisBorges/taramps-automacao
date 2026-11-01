using System.Text.Json;
using System.Text.Json.Nodes;

namespace Agente.Porta;

/// <summary>O cliente pareado: o endereço da porta e a chave dada pelo app.</summary>
public sealed record ClientePareado(string Endereco, string Chave)
{
    /// <summary>Sem a chave: um registro nunca a mostra.</summary>
    public override string ToString() => $"ClientePareado {{ Endereco = {Endereco} }}";
}

/// <summary>
/// O pareamento guardado em <c>porta.json</c>, na pasta local (fora do repositório): o endereço e a chave. É tudo o
/// que a ferramenta guarda.
/// </summary>
public static class PareamentoLocal
{
    public const string Arquivo = "porta.json";

    public static string Caminho(string pastaLocal) => Path.Combine(pastaLocal, Arquivo);

    /// <summary>O pareamento guardado, ou null (sem arquivo, ou estragado).</summary>
    public static ClientePareado? Ler(string pastaLocal)
    {
        try
        {
            var caminho = Caminho(pastaLocal);
            if (!File.Exists(caminho))
                return null;
            var o = JsonNode.Parse(File.ReadAllText(caminho)) as JsonObject;
            var endereco = Texto(o?["endereco"]);
            var chave = Texto(o?["chave"]);
            // O endereço é conferido de novo: um arquivo mexido nunca manda a chave para outro host ou porta.
            if (string.IsNullOrWhiteSpace(endereco) || string.IsNullOrWhiteSpace(chave) || !PedidoFerramenta.EnderecoValido(endereco))
                return null;
            return new ClientePareado(endereco, chave);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException)
        {
            return null;
        }
    }

    private static string? Texto(JsonNode? no) => no is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;

    /// <summary>Grava o pareamento (num arquivo temporário e depois no lugar, para nunca ficar pela metade).</summary>
    public static void Gravar(string pastaLocal, ClientePareado cliente)
    {
        Directory.CreateDirectory(pastaLocal);
        var json = new JsonObject { ["endereco"] = cliente.Endereco, ["chave"] = cliente.Chave }.ToJsonString();
        var caminho = Caminho(pastaLocal);
        var temporario = caminho + ".tmp";
        File.WriteAllText(temporario, json);
        File.Move(temporario, caminho, overwrite: true);
    }
}

/// <summary>Onde fica a pasta local do Agente (o pareamento, o estado e os logs), e a raiz de <c>agente/</c>.</summary>
public static class PastaLocal
{
    /// <summary>A variável de ambiente que aponta a pasta (o serviço e os testes passam aos processos filhos).</summary>
    public const string Variavel = "AGENTE_LOCAL";

    /// <summary>O arquivo que marca a raiz de <c>agente/</c>.</summary>
    public const string MarcaRaiz = "servico.config.json";

    /// <summary>O nome da pasta padrão, nos dados locais do usuário (<c>%LOCALAPPDATA%\BgsAgente</c> no Windows).</summary>
    public const string NomePadrao = "BgsAgente";

    /// <summary>
    /// A pasta dada (<c>--local</c>), ou a da variável <see cref="Variavel"/>, ou <see cref="Padrao"/>: fora do repositório,
    /// para o Agente (que roda numa pasta de trabalho) não achar a chave nem os arquivos do projeto.
    /// </summary>
    public static string Resolver(string? dada = null)
    {
        if (!string.IsNullOrWhiteSpace(dada))
            return Path.GetFullPath(dada);
        var variavel = Environment.GetEnvironmentVariable(Variavel);
        if (!string.IsNullOrWhiteSpace(variavel))
            return Path.GetFullPath(variavel);
        return Padrao;
    }

    /// <summary><c>%LOCALAPPDATA%\BgsAgente</c> (no Linux e no macOS, a pasta de dados locais do usuário).</summary>
    public static string Padrao
    {
        get
        {
            var dados = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrEmpty(dados))
                dados = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share");
            return Path.Combine(dados, NomePadrao);
        }
    }

    /// <summary>A pasta <c>agente/</c> acima de <paramref name="inicio"/> (com o <see cref="MarcaRaiz"/>), ou null.</summary>
    public static string? AcharRaiz(string inicio)
    {
        for (var pasta = new DirectoryInfo(inicio); pasta is not null; pasta = pasta.Parent)
        {
            if (File.Exists(Path.Combine(pasta.FullName, MarcaRaiz)))
                return pasta.FullName;
        }
        return null;
    }
}
