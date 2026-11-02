using System.Text;
using System.Text.Json;
using Agente.Avaliacao;
using Agente.Servico;

// O avaliador do Agente (história 14f). Uso:
//   dotnet run --project agente/avaliacao/Agente.Avaliacao -- real                    (mostra o custo e sai com 3)
//   dotnet run --project agente/avaliacao/Agente.Avaliacao -- real --confirmo-custo   (roda o Agente de verdade)
//   dotnet run --project agente/avaliacao/Agente.Avaliacao -- conferir                (a trava das camadas)

Console.OutputEncoding = new UTF8Encoding(false);
using var parar = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    parar.Cancel();
};
try
{
    return await Comandos.RodarAsync(args, Console.Out, cancelar: parar.Token);
}
catch (OperationCanceledException)
{
    Console.WriteLine("Interrompido.");
    return 1;
}
catch (Exception e) when (e is PerfilInvalidoException or ConfigInvalidaException or JsonException or IOException or UnauthorizedAccessException)
{
    // Um perfil ou um servico.config.json que falta ou não vale, um arquivo das camadas preso: a mensagem, sem a pilha.
    Console.WriteLine(e.Message);
    return Comandos.UsoErrado;
}
