using System.Text;
using Agente.Porta;

// A ferramenta da porta (história 14a). Uso:
//   porta --json '{"acao":"ler","alvo":"/estado"}'
//   porta '{"acao":"ler","alvo":"/estado"}'
//   echo '{"acao":"ler","alvo":"/estado"}' | porta
//   porta --json '{"acao":"parear","endereco":"<ip-do-celular>:8787","codigo":"<6 dígitos>"}'
// Opções: --local <pasta> (onde fica o porta.json; senão AGENTE_LOCAL ou %LOCALAPPDATA%\BgsAgente).
// A saída é sempre um JSON numa linha; o código de saída é 0 no sucesso e 1 na recusa ou erro.

var utf8 = new UTF8Encoding(false);
Console.OutputEncoding = utf8;
Console.InputEncoding = utf8;

RespostaFerramenta resposta;
try
{
    resposta = await RodarAsync(args);
}
catch (Exception e)
{
    // Nada escapa sem o JSON: um erro que ninguém previu também sai numa linha, com o código 1.
    resposta = RespostaFerramenta.Recusa($"Erro inesperado na ferramenta ({e.GetType().Name}: {e.Message}).");
}
if (resposta.Ok && resposta.Status is null)
    return 0; // a ajuda já foi escrita
Console.WriteLine(resposta.ParaJson());
return resposta.CodigoSaida;

static async Task<RespostaFerramenta> RodarAsync(string[] args)
{
    string? json = null;
    string? local = null;
    for (var i = 0; i < args.Length; i++)
    {
        switch (args[i])
        {
            case "--json" when i + 1 < args.Length:
                json = args[++i];
                break;
            case "--local" when i + 1 < args.Length:
                local = args[++i];
                break;
            case "--ajuda" or "-h" or "--help":
                Console.WriteLine("""porta --json '{"acao":"ler|pedir|parear","metodo":"GET|POST|DELETE","alvo":"/estado","corpo":{...}}'  [--local <pasta>]""");
                return new RespostaFerramenta(true, null, null, null);
            default:
                if (json is null && args[i].TrimStart().StartsWith('{'))
                {
                    json = args[i];
                    break;
                }
                return RespostaFerramenta.Recusa($"Argumento desconhecido: {args[i]}. Use --json '<entrada>'.");
        }
    }

    if (json is null && Console.IsInputRedirected)
        json = await Console.In.ReadToEndAsync();

    return await FerramentaPorta.ExecutarAsync(json, PastaLocal.Resolver(local));
}
