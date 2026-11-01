using System.Text;
using Agente.Porta;
using Agente.Servico;

// A ferramenta do aprendizado (história 14e). Uso:
//   aprendizado --json '{"acao":"situacao","nome":"música"}'
//   aprendizado --json '{"acao":"variaveis","valores":{"grave":4.0,"cobertura":"varios"}}'
//   aprendizado --json '{"acao":"outro","texto":"som de show","variavel":"grave"}'
//   aprendizado --json '{"acao":"perfil","situacao":"música"}'
//   echo '{"acao":"perfil","situacao":"música"}' | aprendizado
// Opções: --local <pasta> (senão AGENTE_LOCAL ou %LOCALAPPDATA%\BgsAgente). Grava só em <pasta local>/aprendizado.
// A saída é sempre um JSON numa linha; o código de saída é 0 no ok e 1 na recusa.

var utf8 = new UTF8Encoding(false);
Console.OutputEncoding = utf8;
Console.InputEncoding = utf8;

SaidaAprendizado saida;
try
{
    saida = await RodarAsync(args);
}
catch (Exception e)
{
    // Nada escapa sem o JSON: um erro que ninguém previu também sai numa linha, com o código 1.
    saida = SaidaAprendizado.Recusa($"Erro inesperado na ferramenta ({e.GetType().Name}: {e.Message}).");
}
if (saida is { Ok: true, Campos: null })
    return 0; // a ajuda já foi escrita
Console.WriteLine(saida.ParaJson());
return saida.CodigoSaida;

static async Task<SaidaAprendizado> RodarAsync(string[] args)
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
                Console.WriteLine("""aprendizado --json '{"acao":"situacao|variaveis|outro|perfil",...}'  [--local <pasta>]""");
                return new SaidaAprendizado(true, null, null);
            default:
                if (json is null && args[i].TrimStart().StartsWith('{'))
                {
                    json = args[i];
                    break;
                }
                return SaidaAprendizado.Recusa($"Argumento desconhecido: {args[i]}. Use --json '<entrada>'.");
        }
    }

    if (json is null && Console.IsInputRedirected)
        json = await Console.In.ReadToEndAsync();

    return Aprendizado.Executar(json, PastaLocal.Resolver(local));
}
