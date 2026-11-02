using System.Text.Json.Nodes;
using Agente.Porta;
using Agente.Servico;
using BgsControle.Nucleo.Setups;

namespace Agente.Avaliacao;

/// <summary>As opções de uma rodada da avaliação.</summary>
/// <param name="PrazoAgente">Quanto o Agente pode rodar em cada perfil.</param>
/// <param name="EsperaComoFicou">Quanto o ouvinte espera as respostas do "Como ficou?" antes de responder sem elas.</param>
/// <param name="Paralelo">Os perfis rodam ao mesmo tempo (cada um com o seu núcleo, a sua porta e a sua pasta local).</param>
public sealed record OpcoesAvaliacao(TimeSpan PrazoAgente, TimeSpan EsperaComoFicou, bool Paralelo)
{
    /// <summary>Para os Agentes falsos dos testes: rápido e em paralelo.</summary>
    public static OpcoesAvaliacao Falsos { get; } = new(TimeSpan.FromMinutes(2), TimeSpan.FromSeconds(10), true);
}

/// <summary>
/// O avaliador (spec-agente-memoria CAP-8, história 14f): para cada perfil, sobe o núcleo do app e a porta sobre o simulador,
/// com o Setup do perfil e o som de partida, numa pasta local temporária; pareia; pede a criação ("+ Nova memória"); lança o
/// Agente com a tarefa <c>criar-memoria</c> e o evento; e deixa o <see cref="OuvinteSimulado"/> responder. No fim, as
/// <see cref="Metricas"/>. O avaliador roda o lançador que recebe: os testes usam os Agentes falsos; o comando
/// <c>real</c>, o <see cref="LancadorClaude"/>.
/// </summary>
public static class Avaliador
{
    public const string Tarefa = "criar-memoria";

    /// <summary>Quanto o ouvinte ainda tem para salvar depois que o Agente termina, além da espera do "Como ficou?".</summary>
    private static readonly TimeSpan EsperaFim = TimeSpan.FromSeconds(15);

    /// <summary>Roda todos os perfis e monta o relatório.</summary>
    /// <param name="fabrica">O lançador do Agente, pela pasta local do perfil.</param>
    public static async Task<Relatorio> RodarAsync(IReadOnlyList<Perfil> perfis, Func<string, ILancadorAgente> fabrica, OpcoesAvaliacao opcoes,
        string tipo, string hashCamadas, TextWriter? log = null, CancellationToken cancelar = default)
    {
        ResultadoPerfil[] resultados;
        if (opcoes.Paralelo)
        {
            resultados = await Task.WhenAll(perfis.Select(p => RodarPerfilAsync(p, fabrica, opcoes, log, cancelar)));
        }
        else
        {
            var lista = new List<ResultadoPerfil>();
            foreach (var p in perfis)
                lista.Add(await RodarPerfilAsync(p, fabrica, opcoes, log, cancelar));
            resultados = lista.ToArray();
        }
        return Relatorio.Montar(resultados, tipo, hashCamadas);
    }

    /// <summary>
    /// Roda um perfil: nunca lança (fora o cancelamento de quem chamou); uma falha (o núcleo que não sobe, o ouvinte, o Agente)
    /// vira o motivo do resultado, e a rodada segue.
    /// </summary>
    public static async Task<ResultadoPerfil> RodarPerfilAsync(Perfil perfil, Func<string, ILancadorAgente> fabrica, OpcoesAvaliacao opcoes,
        TextWriter? log = null, CancellationToken cancelar = default)
    {
        PastaTemporaria? local = null;
        NucleoSimulado? nucleo = null;
        try
        {
            try
            {
                local = new PastaTemporaria();
                nucleo = await NucleoSimulado.ProntoAsync(Perfis.SetupsJson(perfil), Perfis.EstadoPartida);
            }
            catch (Exception e) when (e is not OperationCanceledException || !cancelar.IsCancellationRequested)
            {
                var semNucleo = Metricas.Calcular(perfil, new OuvinteSimulado(perfil, null), (_, _) => null, [], [],
                    $"O núcleo não subiu ({e.GetType().Name}: {e.Message}).");
                log?.WriteLine($"{perfil.Id}: não passou — {semNucleo.Motivo}");
                return semNucleo;
            }
            return await RodarNoNucleoAsync(perfil, nucleo, local.Caminho, fabrica, opcoes, log, cancelar);
        }
        finally
        {
            if (nucleo is not null)
                await nucleo.DisposeAsync();
            local?.Dispose();
        }
    }

    private static async Task<ResultadoPerfil> RodarNoNucleoAsync(Perfil perfil, NucleoSimulado nucleo, string pastaLocal,
        Func<string, ILancadorAgente> fabrica, OpcoesAvaliacao opcoes, TextWriter? log, CancellationToken cancelar)
    {
        var recusas = new List<string>();
        nucleo.Log.Registrou += linha =>
        {
            if (RecusaDaCerca(linha) is { } motivo)
            {
                lock (recusas)
                    recusas.Add(motivo);
            }
        };

        var ouvinte = new OuvinteSimulado(perfil, nucleo, opcoes.EsperaComoFicou);
        using var pararOuvinte = CancellationTokenSource.CreateLinkedTokenSource(cancelar);
        var ouvindo = ouvinte.RodarAsync(pararOuvinte.Token);
        string? falha = null;
        try
        {
            falha = await LancarAsync(nucleo, pastaLocal, fabrica, opcoes, cancelar);
            // O Agente terminou: o ouvinte ainda pode esperar as respostas do "Como ficou?" e salvar (no "Como ficou?" e no
            // Salvar, a criação fica aberta).
            await Task.WhenAny(ouvindo, Task.Delay(opcoes.EsperaComoFicou + EsperaFim, cancelar));
            if (nucleo.Criacao.Aberta)
            {
                falha ??= "A criação não terminou salva.";
                await nucleo.Criacao.CancelarAsync(CriacaoMemoria.PorPorta);
            }
        }
        catch (Exception e) when (e is not OperationCanceledException || !cancelar.IsCancellationRequested)
        {
            falha ??= $"A avaliação do perfil falhou ({e.GetType().Name}: {e.Message}).";
        }
        finally
        {
            await pararOuvinte.CancelAsync();
            try
            {
                await ouvindo;
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                falha ??= $"O ouvinte falhou ({e.GetType().Name}: {e.Message}).";
            }
        }
        cancelar.ThrowIfCancellationRequested();

        if (falha is null && !ouvinte.Salvou)
            falha = ouvinte.FalhaSalvar is { } motivo ? $"O Salvar falhou ({motivo})." : "A memória não foi salva.";
        string[] lista;
        lock (recusas)
            lista = recusas.ToArray();
        var resultado = Metricas.Calcular(perfil, ouvinte, (codigo, indice) => nucleo.Simulador.Valor(codigo, indice), lista,
            nucleo.Setups.LimitesEmUso, falha);
        log?.WriteLine($"{perfil.Id}: {(resultado.Passou ? "passou" : "não passou")} — {resultado.Motivo}");
        return resultado;
    }

    /// <summary>Pareia, pede a criação e roda o Agente até ele terminar ou o prazo vencer. Devolve a falha, ou null.</summary>
    private static async Task<string?> LancarAsync(NucleoSimulado nucleo, string pastaLocal, Func<string, ILancadorAgente> fabrica,
        OpcoesAvaliacao opcoes, CancellationToken cancelar)
    {
        var parear = new JsonObject { ["acao"] = "parear", ["endereco"] = nucleo.Endereco, ["codigo"] = nucleo.Pareamento.Codigo, ["nome"] = "Avaliação" };
        var pareado = await FerramentaPorta.ExecutarAsync(parear.ToJsonString(), pastaLocal, cancelar: cancelar);
        if (!pareado.Ok)
            return $"O pareamento falhou ({pareado.Motivo}).";

        // O toque no "+ Nova memória": o evento que acorda o Agente.
        var pedido = nucleo.Criacao.Pedir();
        if (!pedido.Ok)
            return $"O pedido de criação falhou ({pedido.Motivo}).";
        JsonObject? evento;
        using (var cliente = new ClientePorta())
        {
            var eventos = await FerramentaPorta.PedirAsync(pastaLocal, "GET", "/eventos?desde=0", null, cliente, cancelar);
            evento = (eventos.Corpo?["eventos"] as JsonArray)?.OfType<JsonObject>()
                .LastOrDefault(e => e["tipo"]?.GetValue<string>() == CriacaoMemoria.EventoPedida)?.DeepClone() as JsonObject;
        }
        if (evento is null)
            return "O evento criacao-pedida não chegou à porta.";

        var lancador = fabrica(pastaLocal);
        using var prazo = CancellationTokenSource.CreateLinkedTokenSource(cancelar);
        prazo.CancelAfter(opcoes.PrazoAgente);
        try
        {
            var codigo = await lancador.RodarAsync(Tarefa, evento, prazo.Token);
            return codigo == 0 ? null : $"O Agente saiu com o código {codigo}.";
        }
        catch (OperationCanceledException) when (prazo.IsCancellationRequested && !cancelar.IsCancellationRequested)
        {
            return $"O Agente passou do prazo de {opcoes.PrazoAgente.TotalMinutes:0.#} min.";
        }
        catch (Exception e) when (e is not OperationCanceledException || !cancelar.IsCancellationRequested)
        {
            return $"O Agente falhou ({e.GetType().Name}: {e.Message}).";
        }
    }

    /// <summary>A recusa da cerca numa linha do log do núcleo (o motivo "Limite: …"), ou null.</summary>
    public static string? RecusaDaCerca(string linha)
    {
        foreach (var marca in new[] { "(recusado: Limite: ", "escuta recusada (Limite: " })
        {
            var i = linha.IndexOf(marca, StringComparison.Ordinal);
            if (i >= 0)
            {
                var motivo = linha[(i + marca.Length - "Limite: ".Length)..];
                return motivo.EndsWith(')') ? motivo[..^1] : motivo; // só o ")" que fecha a marca
            }
        }
        return null;
    }
}

/// <summary>Uma pasta temporária, apagada no fim (a pasta local de um perfil: o pareamento, a pasta de trabalho, os logs).</summary>
public sealed class PastaTemporaria : IDisposable
{
    public PastaTemporaria()
    {
        Caminho = Path.Combine(Path.GetTempPath(), "agente-avaliacao-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Caminho);
    }

    public string Caminho { get; }

    public void Dispose()
    {
        for (var tentativa = 0; tentativa < 5; tentativa++)
        {
            try
            {
                Directory.Delete(Caminho, recursive: true);
                return;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                Thread.Sleep(200); // um processo filho acabando de soltar o arquivo
            }
        }
    }
}
