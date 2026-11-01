using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Agente.ModeloFalso;
using Agente.Porta;

// O modelo falso (história 14a): roda um roteiro gravado pela ferramenta porta, sem chamar modelo nenhum.
//   Agente.ModeloFalso --roteiro testes/roteiros/criacao-basica.json [--porta <porta.dll>] [--local <pasta .local>]
//                      [--evento <json do evento que acordou o Agente>]
// Código de saída: 0 no fim do roteiro, 1 num passo que falhou, 2 num roteiro ou num evento inválido.

Console.OutputEncoding = new UTF8Encoding(false);

string? caminhoRoteiro = null;
var porta = FerramentaProcesso.PortaAoLado;
string? local = null;
string? textoEvento = null;
for (var i = 0; i + 1 < args.Length; i += 2)
{
    switch (args[i])
    {
        case "--roteiro":
            caminhoRoteiro = args[i + 1];
            break;
        case "--porta":
            porta = args[i + 1];
            break;
        case "--local":
            local = args[i + 1];
            break;
        case "--evento":
            textoEvento = args[i + 1];
            break;
    }
}

if (caminhoRoteiro is null)
{
    Console.Error.WriteLine("Uso: Agente.ModeloFalso --roteiro <arquivo.json> [--porta <porta.dll>] [--local <pasta>] [--evento <json>]");
    return 2;
}

Roteiro roteiro;
try
{
    roteiro = Roteiro.Carregar(caminhoRoteiro);
}
catch (Exception e) when (e is RoteiroInvalidoException or IOException)
{
    Console.Error.WriteLine($"Roteiro inválido: {e.Message}");
    return 2;
}

JsonObject? evento = null;
if (textoEvento is not null)
{
    try
    {
        evento = JsonNode.Parse(textoEvento) as JsonObject ?? throw new JsonException("o evento não é um objeto JSON.");
    }
    catch (JsonException e)
    {
        Console.Error.WriteLine($"Evento inválido: {e.Message}");
        return 2;
    }
}

var pastaLocal = local is null ? null : PastaLocal.Resolver(local);
var executor = new ExecutorRoteiro(new FerramentaProcesso(porta, pastaLocal), Console.Out, evento: evento);
return await executor.RodarAsync(roteiro);
