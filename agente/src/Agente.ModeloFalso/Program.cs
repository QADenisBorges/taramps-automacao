using System.Text;
using Agente.ModeloFalso;
using Agente.Porta;

// O modelo falso (história 14a): roda um roteiro gravado pela ferramenta porta, sem chamar modelo nenhum.
//   Agente.ModeloFalso --roteiro testes/roteiros/criacao-basica.json [--porta <porta.dll>] [--local <pasta .local>]
// Código de saída: 0 no fim do roteiro, 1 num passo que falhou, 2 num roteiro inválido.

Console.OutputEncoding = new UTF8Encoding(false);

string? caminhoRoteiro = null;
var porta = FerramentaProcesso.PortaAoLado;
string? local = null;
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
    }
}

if (caminhoRoteiro is null)
{
    Console.Error.WriteLine("Uso: Agente.ModeloFalso --roteiro <arquivo.json> [--porta <porta.dll>] [--local <pasta>]");
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

var pastaLocal = local is null ? null : PastaLocal.Resolver(local);
var executor = new ExecutorRoteiro(new FerramentaProcesso(porta, pastaLocal), Console.Out);
return await executor.RodarAsync(roteiro);
