using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Agente.Porta;

namespace Agente.ModeloFalso;

/// <summary>Quem chama a ferramenta da porta: a entrada e a saída em JSON (no processo de verdade, o executável <c>porta</c>).</summary>
public interface IFerramenta
{
    Task<RespostaFerramenta> RodarAsync(JsonObject entrada, CancellationToken cancelar);
}

/// <summary>
/// Roda um <see cref="Roteiro"/> pela ferramenta da porta, como o modelo de verdade faria: cada <c>pedir</c> é uma chamada
/// da ferramenta e cada <c>esperar</c> lê o <c>/eventos</c> por ela, a partir do evento atual de quando o roteiro começou.
/// </summary>
public sealed class ExecutorRoteiro(IFerramenta ferramenta, TextWriter log, TimeSpan? intervaloEspera = null)
{
    public const int Terminou = 0;
    public const int Falhou = 1;

    /// <summary>A criação acabou: o usuário salvou, cancelou, ou ela foi interrompida.</summary>
    private static readonly string[] FimDaCriacao = ["criacao-salva", "criacao-cancelada", "criacao-interrompida"];

    /// <summary>Quantos passos um roteiro roda no máximo (um "ir" em ciclo não roda para sempre).</summary>
    public const int MaximoPassos = 1000;

    /// <summary>Quantas vezes a primeira leitura do /eventos é tentada antes de o roteiro falhar.</summary>
    public const int TentativasInicio = 10;

    private readonly TimeSpan _intervalo = intervaloEspera ?? TimeSpan.FromMilliseconds(300);
    private long _cursor;

    /// <summary>Roda até o fim (0) ou até um passo falhar (1).</summary>
    public async Task<int> RodarAsync(Roteiro roteiro, CancellationToken cancelar = default)
    {
        if (await UltimoEventoAsync(cancelar) is not { } inicio)
        {
            Escrever($"a porta não respondeu ao /eventos em {TentativasInicio} tentativas; o roteiro falhou");
            return Falhou;
        }
        _cursor = inicio;
        Escrever($"roteiro começou no evento {_cursor.ToString(CultureInfo.InvariantCulture)}");
        var i = 0;
        var rodados = 0;
        while (i < roteiro.Passos.Count)
        {
            if (++rodados > MaximoPassos)
            {
                Escrever($"mais de {MaximoPassos} passos; o roteiro falhou (um ciclo?)");
                return Falhou;
            }
            var passo = roteiro.Passos[i];
            var nome = passo.Id ?? $"#{i + 1}";
            if (passo.Fim)
            {
                Escrever($"{nome}: fim");
                return Terminou;
            }

            JsonNode? resultado;
            string? proximo = null;
            if (passo.Pedir is { } pedir)
            {
                var r = await PedirAsync(pedir, cancelar);
                Escrever($"{nome}: {pedir.Metodo} {pedir.Alvo} -> {(r.Ok ? "ok" : "recusado")} {r.Status?.ToString(CultureInfo.InvariantCulture)} {r.Motivo}".TrimEnd());
                if (!r.Ok)
                {
                    if (passo.SeFalhar is null)
                        return Falhou;
                    proximo = passo.SeFalhar;
                }
                resultado = r.ParaObjeto();
            }
            else
            {
                var espera = passo.Esperar!;
                var (evento, saiu) = await EsperarAsync(espera, cancelar);
                if (saiu)
                {
                    Escrever($"{nome}: a criação foi cancelada ou interrompida; fim");
                    return Terminou;
                }
                if (evento is null)
                {
                    Escrever($"{nome}: nenhum {string.Join(" ou ", espera.Tipos)} em {espera.PrazoS.ToString(CultureInfo.InvariantCulture)} s");
                    if (passo.SeVencer is null)
                        return Falhou;
                    proximo = passo.SeVencer;
                }
                else
                {
                    Escrever($"{nome}: {evento["tipo"]} {evento.ToJsonString(RespostaFerramenta.Formato)}");
                }
                resultado = evento;
            }

            proximo ??= Ramificar(passo.Se, resultado) ?? passo.Ir;
            i = proximo is null ? i + 1 : roteiro.Indice(proximo);
        }
        Escrever("roteiro terminou");
        return Terminou;
    }

    private Task<RespostaFerramenta> PedirAsync(PedidoPasso pedir, CancellationToken cancelar)
    {
        var metodo = pedir.Metodo.ToUpperInvariant();
        var entrada = new JsonObject
        {
            ["acao"] = metodo == "GET" ? PedidoFerramenta.Ler : PedidoFerramenta.Pedir,
            ["metodo"] = metodo,
            ["alvo"] = pedir.Alvo,
        };
        if (pedir.Corpo is not null)
            entrada["corpo"] = pedir.Corpo.DeepClone();
        return ferramenta.RodarAsync(entrada, cancelar);
    }

    private Task<RespostaFerramenta> LerEventosAsync(long desde, CancellationToken cancelar) =>
        ferramenta.RodarAsync(new JsonObject
        {
            ["acao"] = PedidoFerramenta.Ler,
            ["alvo"] = $"/eventos?desde={desde.ToString(CultureInfo.InvariantCulture)}",
        }, cancelar);

    /// <summary>O evento atual, com algumas tentativas; null se a porta não respondeu (nunca o 0 por engano).</summary>
    private async Task<long?> UltimoEventoAsync(CancellationToken cancelar)
    {
        for (var tentativa = 1; tentativa <= TentativasInicio; tentativa++)
        {
            var r = await LerEventosAsync(0, cancelar);
            if (r.Ok && Numero((r.Corpo as JsonObject)?["ultimo"]) is { } ultimo)
                return ultimo;
            if (tentativa < TentativasInicio)
                await Task.Delay(_intervalo, cancelar);
        }
        return null;
    }

    /// <summary>O primeiro evento de um dos tipos depois do cursor (null se o prazo venceu); <c>saiu</c>: a criação acabou antes.</summary>
    private async Task<(JsonObject? Evento, bool Saiu)> EsperarAsync(EsperaPasso espera, CancellationToken cancelar)
    {
        var limite = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(espera.PrazoS);
        while (true)
        {
            var r = await LerEventosAsync(_cursor, cancelar);
            if (r.Ok && r.Corpo is JsonObject corpo)
            {
                var ultimo = Numero(corpo["ultimo"]) ?? _cursor;
                foreach (var e in (corpo["eventos"] as JsonArray ?? []).OfType<JsonObject>().OrderBy(e => Numero(e["n"]) ?? 0))
                {
                    var n = Numero(e["n"]) ?? 0;
                    var tipo = e["tipo"] is JsonValue t && t.TryGetValue<string>(out var texto) ? texto : null;
                    if (tipo is not null && espera.Tipos.Contains(tipo))
                    {
                        _cursor = n;
                        return ((JsonObject)e.DeepClone(), false);
                    }
                    if (tipo is not null && FimDaCriacao.Contains(tipo))
                    {
                        _cursor = n;
                        return (null, true);
                    }
                }
                _cursor = ultimo;
            }
            if (DateTimeOffset.UtcNow >= limite)
                return (null, false);
            await Task.Delay(_intervalo, cancelar);
        }
    }

    /// <summary>O primeiro ramo cujo campo, no resultado, é igual ao valor dado; ou null.</summary>
    public static string? Ramificar(IReadOnlyList<Ramo> ramos, JsonNode? resultado)
    {
        foreach (var ramo in ramos)
        {
            if (Valor(resultado, ramo.Campo) is { } valor && string.Equals(valor, ramo.Igual, StringComparison.Ordinal))
                return ramo.Ir;
        }
        return null;
    }

    /// <summary>O valor de um caminho com pontos (<c>corpo.etapa</c>), como texto: o texto, <c>true</c>/<c>false</c> ou o número.</summary>
    public static string? Valor(JsonNode? no, string caminho)
    {
        foreach (var parte in caminho.Split('.'))
        {
            no = no switch
            {
                JsonObject o => o[parte],
                JsonArray a when int.TryParse(parte, NumberStyles.None, CultureInfo.InvariantCulture, out var i) && i < a.Count => a[i],
                _ => null,
            };
            if (no is null)
                return null;
        }
        return no switch
        {
            JsonValue v when v.GetValueKind() == JsonValueKind.String => v.GetValue<string>(),
            JsonValue v => v.ToJsonString(),
            { } outro => outro.ToJsonString(),
            _ => null,
        };
    }

    /// <summary>Um número inteiro do JSON (lido do texto ou montado em memória), ou null.</summary>
    private static long? Numero(JsonNode? no) =>
        no is JsonValue v && v.GetValueKind() == JsonValueKind.Number
        && long.TryParse(v.ToJsonString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : null;

    private void Escrever(string texto)
    {
        lock (log)
            log.WriteLine($"{DateTimeOffset.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture)} modelo falso: {texto}");
    }
}
