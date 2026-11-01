using System.Text;
using System.Text.Json;
using Agente.Porta;
using Agente.Servico;
using Agente.Servico.Programa;

// O serviço do Agente (história 14a): escuta o /eventos da porta e acorda um Agente por vez.
//   servico [--agente <pasta agente/>] [--local <pasta local>]
// A pasta agente/ é a do servico.config.json (achada subindo da pasta atual, se não for dada). O pareamento, o
// estado, os logs e a pasta de trabalho do Agente ficam na pasta local (--local, AGENTE_LOCAL ou
// %LOCALAPPDATA%\BgsAgente), fora do repositório. Ctrl+C para. Código 2: configuração ou pasta inválida.

Console.OutputEncoding = new UTF8Encoding(false);

string? pastaAgente = null;
string? local = null;
for (var i = 0; i + 1 < args.Length; i += 2)
{
    if (args[i] == "--agente")
        pastaAgente = args[i + 1];
    else if (args[i] == "--local")
        local = args[i + 1];
}

ConfigServico config;
string pastaLocal;
ILancadorAgente lancador;
try
{
    pastaAgente = pastaAgente is not null
        ? Path.GetFullPath(pastaAgente)
        : PastaLocal.AcharRaiz(Directory.GetCurrentDirectory()) ?? PastaLocal.AcharRaiz(AppContext.BaseDirectory);
    if (pastaAgente is null || !File.Exists(Path.Combine(pastaAgente, ConfigServico.Arquivo)))
    {
        Console.Error.WriteLine($"Não achei o {ConfigServico.Arquivo}. Rode na pasta agente/ ou passe --agente <pasta>.");
        return 2;
    }
    config = ConfigServico.Carregar(Path.Combine(pastaAgente, ConfigServico.Arquivo));
    pastaLocal = PastaLocal.Resolver(local);
    lancador = FabricaLancador.Criar(config, pastaAgente, pastaLocal);
}
catch (JsonException e)
{
    Console.Error.WriteLine($"O {ConfigServico.Arquivo} não é um JSON válido: {e.Message}");
    return 2;
}
catch (ConfigInvalidaException e)
{
    Console.Error.WriteLine($"O {ConfigServico.Arquivo} tem um valor inválido: {e.Message}");
    return 2;
}
catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
{
    Console.Error.WriteLine($"Não consegui ler a configuração ou a pasta local: {e.Message}");
    return 2;
}

var log = new LogServico(Path.Combine(pastaLocal, ServicoAgente.ArquivoLog), console: true);
using var parar = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    parar.Cancel();
};
using var servico = new ServicoAgente(config, lancador, log, pastaLocal);
await servico.RodarAsync(parar.Token);
return 0;
