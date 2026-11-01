using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Agente.Porta;

namespace Agente.Servico;

/// <summary>
/// O serviço que acorda o Agente (história 14a, spec-agente-memoria CAP-1). Roda no notebook:
/// <list type="bullet">
/// <item><b>Ciclo:</b> lê <c>GET /eventos?desde=N</c> a cada <see cref="ConfigServico.Intervalo"/> e guarda o último N em
/// <c>servico.json</c> (na pasta local). Ao subir, começa do evento atual, sem reprocessar o passado. O N guardado é o
/// maior entre o <c>ultimo</c> e o maior evento lido. Depois de uma queda, a primeira leitura pega todos os eventos e só
/// trata os mais novos que o último visto (pela hora): se o app reabriu, a contagem recomeçou.</item>
/// <item><b>Gatilhos</b> (<see cref="ConfigServico.Gatilhos"/>): o tipo do evento → a tarefa do Agente.</item>
/// <item><b>Um Agente por vez:</b> um gatilho com um Agente rodando é ignorado, com a linha <see cref="MsgJaRodando"/>.</item>
/// <item><b>Prazo:</b> passado <see cref="ConfigServico.PrazoAgente"/>, o processo é encerrado.</item>
/// <item><b>Fim</b> (por qualquer motivo: saiu sozinho, prazo, erro do lançador, serviço parando): com a criação aberta em
/// perguntas, ajustando ou resumo, vai o <c>POST /criacao/cancelar</c>; no "Como ficou?" e no Salvar, não (o usuário ainda
/// pode salvar). O <c>DELETE /tarefa</c> só vai se não havia tarefa antes deste Agente. O código de saída e a duração
/// vão ao log.</item>
/// <item><b>Queda da porta:</b> nova tentativa a cada <see cref="ConfigServico.EsperaQueda"/>, com uma linha no log a cada
/// mudança de estado.</item>
/// </list>
/// Fala com a porta pelo mesmo caminho da ferramenta (<see cref="FerramentaPorta.PedirAsync"/>), com o pareamento de
/// <c>porta.json</c>.
/// </summary>
public sealed class ServicoAgente : IDisposable
{
    public const string ArquivoEstado = "servico.json";
    public const string ArquivoLog = "servico.log";

    public const string MsgJaRodando = "Já há um Agente rodando; pedido ignorado.";
    public const string MsgConectado = "Porta conectada.";
    public const string MsgSemUltimo = "A porta respondeu ao /eventos sem o \"ultimo\".";
    public const string MsgCriacaoCancelada = "Criação aberta cancelada (POST /criacao/cancelar).";

    /// <summary>As etapas em que a criação fica sem dono quando o Agente acaba: aí ela é cancelada.</summary>
    public static IReadOnlyList<string> EtapasDoAgente { get; } = ["perguntas", "ajustando", "resumo"];

    public static string MsgSemResposta(string motivo, TimeSpan espera) =>
        $"Porta sem resposta ({motivo}); nova tentativa a cada {espera.TotalSeconds.ToString("0.#", CultureInfo.InvariantCulture)} s.";

    public static string MsgRecusada(string motivo) => $"A porta recusou: {motivo}";

    private static readonly TimeSpan PrazoFim = TimeSpan.FromSeconds(75);
    private static readonly TimeSpan PrazoAntes = TimeSpan.FromSeconds(10);

    private enum Conexao
    {
        Nenhuma,
        Conectada,
        SemResposta,
        Recusada,
    }

    private readonly ConfigServico _config;
    private readonly ILancadorAgente _lancador;
    private readonly LogServico _log;
    private readonly string _pastaLocal;
    private readonly ClientePorta _cliente;
    private readonly object _trava = new();

    private Conexao _conexao = Conexao.Nenhuma;
    private string? _motivoConexao;
    private long? _ultimo;
    private DateTimeOffset? _ultimaHora;
    private bool _releitura;
    private Task? _agente;
    private int _lancamentos;

    public ServicoAgente(ConfigServico config, ILancadorAgente lancador, LogServico log, string pastaLocal, ClientePorta? cliente = null)
    {
        _config = config;
        _lancador = lancador;
        _log = log;
        _pastaLocal = pastaLocal;
        _cliente = cliente ?? new ClientePorta();
    }

    /// <summary>O último evento lido (null antes da primeira leitura).</summary>
    public long? Ultimo
    {
        get
        {
            lock (_trava)
                return _ultimo;
        }
    }

    /// <summary>Quantos Agentes foram lançados.</summary>
    public int Lancamentos
    {
        get
        {
            lock (_trava)
                return _lancamentos;
        }
    }

    public bool AgenteRodando
    {
        get
        {
            lock (_trava)
                return _agente is not null;
        }
    }

    /// <summary>O ciclo, até o cancelamento; ao parar, encerra o Agente que estiver rodando e espera o fim dele.</summary>
    public async Task RodarAsync(CancellationToken cancelar)
    {
        _log.Escrever("Serviço iniciado.");
        try
        {
            while (!cancelar.IsCancellationRequested)
            {
                bool leu;
                try
                {
                    leu = await UmaVoltaAsync(cancelar);
                }
                catch (Exception e) when (e is not OperationCanceledException)
                {
                    _log.Escrever($"Erro no ciclo do serviço ({e.GetType().Name}: {e.Message}).");
                    leu = false;
                }
                await Task.Delay(leu ? _config.Intervalo : _config.EsperaQueda, cancelar);
            }
        }
        catch (OperationCanceledException) when (cancelar.IsCancellationRequested)
        {
            // parando
        }
        finally
        {
            Task? agente;
            lock (_trava)
                agente = _agente;
            if (agente is not null)
                await agente;
            _log.Escrever("Serviço parado.");
        }
    }

    /// <summary>Uma leitura do <c>/eventos</c>. False: a porta não respondeu ou recusou (a próxima espera a queda).</summary>
    private async Task<bool> UmaVoltaAsync(CancellationToken cancelar)
    {
        long? cursor;
        bool releitura;
        lock (_trava)
        {
            cursor = _ultimo;
            releitura = _releitura && _ultimo is not null;
        }
        // Depois de uma queda, todos os guardados: se o app reabriu, a contagem recomeçou e o N antigo não vale.
        var desde = releitura ? 0 : cursor ?? 0;
        var r = await FerramentaPorta.PedirAsync(_pastaLocal, "GET", $"/eventos?desde={desde.ToString(CultureInfo.InvariantCulture)}", null, _cliente, cancelar);
        if (!r.Ok)
        {
            // Sem status: a porta não respondeu (menos o pareamento que falta, que é uma recusa nossa).
            var semResposta = r.Status is null && r.Motivo != FerramentaPorta.MsgSemPareamento;
            MudarConexao(semResposta ? Conexao.SemResposta : Conexao.Recusada, r.Motivo);
            return false;
        }
        var corpo = r.Corpo as JsonObject;
        if (Numero(corpo?["ultimo"]) is not { } ultimo)
        {
            // Um corpo sem o "ultimo" não zera o cursor: conta como recusa.
            MudarConexao(Conexao.Recusada, MsgSemUltimo);
            return false;
        }
        MudarConexao(Conexao.Conectada, null);

        var eventos = (corpo!["eventos"] as JsonArray ?? [])
            .OfType<JsonObject>()
            .Select(e => (Evento: e, N: Numero(e["n"]) ?? 0, Hora: Hora(e["hora"]), Tipo: Texto(e["tipo"])))
            .OrderBy(e => e.N)
            .ToArray();
        // Guarda o maior: um evento que entrou entre o "ultimo" e a lista não volta na próxima leitura.
        var novoCursor = eventos.Length > 0 ? Math.Max(ultimo, eventos.Max(e => e.N)) : ultimo;
        var maiorHora = eventos.Select(e => e.Hora).Where(h => h is not null).DefaultIfEmpty(null).Max();

        if (cursor is null)
        {
            // Ao subir, começa do evento atual: nada do passado acorda o Agente.
            Guardar(novoCursor, maiorHora);
            _log.Escrever($"Começando do evento {novoCursor.ToString(CultureInfo.InvariantCulture)}.");
            return true;
        }

        DateTimeOffset? vistaAntes;
        lock (_trava)
            vistaAntes = _ultimaHora;
        foreach (var e in eventos)
        {
            // Na releitura, só o que é mais novo que o último visto (pela hora; sem hora, pelo número).
            if (releitura && !(vistaAntes is null || (e.Hora is { } h ? h > vistaAntes : e.N > cursor)))
                continue;
            if (e.Tipo is not null && _config.Gatilhos.TryGetValue(e.Tipo, out var tarefa))
                Acordar(e.Tipo, tarefa, cancelar);
        }
        Guardar(novoCursor, maiorHora);
        lock (_trava)
            _releitura = false;
        return true;
    }

    private void MudarConexao(Conexao nova, string? motivo)
    {
        lock (_trava)
        {
            if (nova != Conexao.Conectada && _ultimo is not null)
                _releitura = true;
            if (_conexao == nova && (nova != Conexao.Recusada || _motivoConexao == motivo))
                return;
            _conexao = nova;
            _motivoConexao = motivo;
        }
        _log.Escrever(nova switch
        {
            Conexao.Conectada => MsgConectado,
            Conexao.SemResposta => MsgSemResposta(motivo ?? "?", _config.EsperaQueda),
            _ => MsgRecusada(motivo ?? "?"),
        });
    }

    private void Guardar(long ultimo, DateTimeOffset? hora)
    {
        bool mudou;
        lock (_trava)
        {
            mudou = _ultimo != ultimo;
            _ultimo = ultimo;
            if (hora is { } h && (_ultimaHora is null || h > _ultimaHora))
                _ultimaHora = h;
        }
        if (!mudou)
            return;
        try
        {
            Directory.CreateDirectory(_pastaLocal);
            var caminho = Path.Combine(_pastaLocal, ArquivoEstado);
            File.WriteAllText(caminho + ".tmp", new JsonObject { ["ultimo"] = ultimo }.ToJsonString());
            File.Move(caminho + ".tmp", caminho, overwrite: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            _log.Escrever($"Não consegui guardar o {ArquivoEstado} ({e.GetType().Name}).");
        }
    }

    // ------------------------------------------------------------------ o Agente

    private void Acordar(string tipo, string tarefa, CancellationToken cancelar)
    {
        lock (_trava)
        {
            if (_agente is not null)
            {
                _log.Escrever(MsgJaRodando);
                return;
            }
            _lancamentos++;
            _log.Escrever($"Agente acordado pelo {tipo}: tarefa {tarefa}.");
            _agente = Task.Run(() => RodarAgenteAsync(tarefa, cancelar), CancellationToken.None);
        }
    }

    private async Task RodarAgenteAsync(string tarefa, CancellationToken parar)
    {
        var inicio = Stopwatch.GetTimestamp();
        int? codigo = null;
        try
        {
            // A tarefa de antes (de outro cliente ou de outro Agente) não é deste Agente: o fim não a apaga.
            var haviaTarefa = await HaTarefaAsync(parar);

            using var prazo = CancellationTokenSource.CreateLinkedTokenSource(parar);
            try
            {
                prazo.CancelAfter(_config.PrazoAgente);
                codigo = await _lancador.RodarAsync(tarefa, prazo.Token);
            }
            catch (OperationCanceledException) when (!parar.IsCancellationRequested)
            {
                _log.Escrever($"Prazo de {Minutos(_config.PrazoAgente)} vencido: o processo do Agente foi encerrado.");
            }
            catch (OperationCanceledException)
            {
                _log.Escrever("Serviço parando: o processo do Agente foi encerrado.");
            }
            catch (Exception e)
            {
                _log.Escrever($"Não consegui rodar o Agente: {e.Message}");
            }

            try
            {
                using var fim = new CancellationTokenSource(PrazoFim);
                await CancelarCriacaoSemDonoAsync(fim.Token);
                if (haviaTarefa == false)
                    await TerminarTarefaAsync(fim.Token);
            }
            catch (OperationCanceledException)
            {
                _log.Escrever("A porta não respondeu ao fim do Agente.");
            }
            catch (Exception e)
            {
                _log.Escrever($"Erro ao fechar o fim do Agente ({e.GetType().Name}: {e.Message}).");
            }
        }
        catch (Exception e)
        {
            _log.Escrever($"Erro no Agente ({e.GetType().Name}: {e.Message}).");
        }
        finally
        {
            var duracao = Stopwatch.GetElapsedTime(inicio);
            _log.Escrever($"Agente terminou: código {(codigo is { } c ? c.ToString(CultureInfo.InvariantCulture) : "nenhum")}, " +
                          $"duração {duracao.TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture)} s.");
            lock (_trava)
                _agente = null;
        }
    }

    /// <summary>A porta mostra uma tarefa agora? Null sem saber (aí o fim não apaga nada).</summary>
    private async Task<bool?> HaTarefaAsync(CancellationToken parar)
    {
        try
        {
            using var prazo = CancellationTokenSource.CreateLinkedTokenSource(parar);
            prazo.CancelAfter(PrazoAntes);
            var estado = await FerramentaPorta.PedirAsync(_pastaLocal, "GET", "/estado", null, _cliente, prazo.Token);
            if (!estado.Ok || (estado.Corpo as JsonObject)?["ia"] is not JsonObject ia)
                return null;
            return !string.IsNullOrEmpty(Texto(ia["tarefa"]));
        }
        catch (OperationCanceledException)
        {
            return null;
        }
    }

    /// <summary>
    /// Com a criação aberta em perguntas, ajustando ou resumo (sem dono agora), o <c>POST /criacao/cancelar</c> (volta à cópia
    /// de antes). No "Como ficou?" e no Salvar, o usuário ainda pode salvar: fica.
    /// </summary>
    private async Task CancelarCriacaoSemDonoAsync(CancellationToken cancelar)
    {
        var criacao = await FerramentaPorta.PedirAsync(_pastaLocal, "GET", "/criacao", null, _cliente, cancelar);
        var etapa = Texto((criacao.Corpo as JsonObject)?["etapa"]);
        if (!criacao.Ok || etapa is null || !EtapasDoAgente.Contains(etapa))
            return;
        var r = await FerramentaPorta.PedirAsync(_pastaLocal, "POST", "/criacao/cancelar", null, _cliente, cancelar);
        _log.Escrever(r.Ok ? MsgCriacaoCancelada : $"O /criacao/cancelar foi recusado: {r.Motivo}");
    }

    /// <summary>O <c>DELETE /tarefa</c>, se a porta mostra uma tarefa (que só pode ser deste Agente).</summary>
    private async Task TerminarTarefaAsync(CancellationToken cancelar)
    {
        var estado = await FerramentaPorta.PedirAsync(_pastaLocal, "GET", "/estado", null, _cliente, cancelar);
        var ia = (estado.Corpo as JsonObject)?["ia"] as JsonObject;
        if (!estado.Ok || string.IsNullOrEmpty(Texto(ia?["tarefa"])))
            return;
        var r = await FerramentaPorta.PedirAsync(_pastaLocal, "DELETE", "/tarefa", null, _cliente, cancelar);
        _log.Escrever(r.Ok ? "Tarefa terminada (DELETE /tarefa)." : $"O DELETE /tarefa foi recusado: {r.Motivo}");
    }

    private static string Minutos(TimeSpan t) =>
        t.TotalMinutes >= 1 ? $"{t.TotalMinutes.ToString("0.#", CultureInfo.InvariantCulture)} min" : $"{t.TotalSeconds.ToString("0.#", CultureInfo.InvariantCulture)} s";

    /// <summary>Um número inteiro do JSON (lido do texto ou montado em memória), ou null.</summary>
    private static long? Numero(JsonNode? no) =>
        no is JsonValue v && v.GetValueKind() == JsonValueKind.Number
        && long.TryParse(v.ToJsonString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : null;

    private static string? Texto(JsonNode? no) => no is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;

    private static DateTimeOffset? Hora(JsonNode? no) =>
        Texto(no) is { } t && DateTimeOffset.TryParse(t, CultureInfo.InvariantCulture, DateTimeStyles.None, out var h) ? h : null;

    public void Dispose() => _cliente.Dispose();
}
