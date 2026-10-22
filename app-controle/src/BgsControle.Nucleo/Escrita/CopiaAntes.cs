using System.Globalization;
using System.Text;
using System.Text.Json;
using BgsControle.Nucleo.Leitura;
using BgsControle.Nucleo.Registro;

namespace BgsControle.Nucleo.Escrita;

/// <summary>
/// Uma operação que mexe em vários parâmetros de uma vez (carregar ou salvar uma memória; na
/// história 8, aplicar um preset), guardada como marca enquanto anda.
/// </summary>
/// <param name="Tipo">Quem sabe refazê-la ("carregar-memoria", "salvar-memoria").</param>
/// <param name="Alvo">Com que alvo (a memória 0 a 2 no fio, o preset).</param>
/// <param name="Descricao">Para a tela: "Carregar a memória 3 (FILME)".</param>
/// <param name="Nome">O nome que a operação grava, quando há.</param>
/// <param name="Etapa">Até onde ela chegou, quando importa (o salvar: "nome" ou "240").</param>
public sealed record OperacaoPendente(string Tipo, string Alvo, string Descricao, string? Nome = null, string? Etapa = null);

/// <summary>Um valor do retrato guardado: o endereço e o texto como o aparelho o devolveu.</summary>
public sealed record ValorCopiado(string Codigo, int? Indice, string NoFio);

/// <summary>O retrato do estado antes de mexer, e a marca da operação em andamento (null = terminou).</summary>
public sealed record Copia(DateTimeOffset Quando, IReadOnlyList<ValorCopiado> Valores, OperacaoPendente? Operacao);

/// <summary>O que fazer com uma marca encontrada depois da leitura completa de uma conexão nova.</summary>
public enum Retomada
{
    /// <summary>Resolvida sozinha (o salvar só avisa que a memória pode não ter sido salva): a marca sai.</summary>
    Resolvida,

    /// <summary>O estado fica "aplicado pela metade", com Terminar e Voltar à cópia.</summary>
    PelaMetade,

    /// <summary>Não deu para decidir agora (caiu de novo): a marca fica para a próxima conexão.</summary>
    Adiada,
}

public sealed record ResultadoCopia(bool Ok, string? Motivo)
{
    public static ResultadoCopia Sucesso { get; } = new(true, null);

    public static ResultadoCopia Falha(string motivo) => new(false, motivo);
}

/// <summary>
/// A cópia antes de mexer (failure-modes, regra 3). Antes de uma operação de vários parâmetros,
/// guarda nos arquivos privados o retrato do estado (o espelho) e a marca da operação: qual é e
/// com que alvo. A marca sai quando a operação termina. Se ela ainda existe depois da leitura
/// completa de uma conexão nova, quem registrou o tipo decide: resolver sozinho, ou deixar o
/// estado "aplicado pela metade", com duas saídas:
/// <list type="bullet">
/// <item><see cref="TerminarAsync"/>: refaz a operação, com a mesma cópia;</item>
/// <item><see cref="VoltarAsync"/>: escreve pelo <see cref="Escritor"/> cada parâmetro de áudio da
/// cópia que difere do estado atual (precisa da chave "Ajustes de áudio"; o link não repete).</item>
/// </list>
/// Genérica: as memórias (história 6) e os presets do celular (história 8) usam.
/// </summary>
public sealed class CopiaAntes
{
    public const int Formato = 1;

    public const string MsgNaoGuardou = "Não consegui guardar a cópia antes de mexer. Nada foi enviado.";
    public const string MsgOutraOperacao = "Outra operação está em andamento.";
    public const string MsgSemCopia = "Não há cópia guardada.";
    public const string MsgNadaPelaMetade = "Nada ficou aplicado pela metade.";
    public const string MsgNaoSeiRefazer = "O app não sabe refazer esta operação.";
    public const string MsgPelaMetadePendente = "Uma operação ficou aplicada pela metade: escolha Terminar ou Voltar à cópia.";

    public static string MsgCopiaIlegivel(int n) => n == 1
        ? "1 ajuste da cópia não pode ser escrito de volta. Nada foi enviado."
        : $"{n} ajustes da cópia não podem ser escritos de volta. Nada foi enviado.";

    private readonly Sessao _sessao;
    private readonly EstadoAparelho _estado;
    private readonly Escritor _escritor;
    private readonly IRepositorioTexto _repositorio;
    private readonly LogBle _log;
    private readonly TimeProvider _relogio;

    private readonly object _trava = new();
    private readonly SemaphoreSlim _gravacao = new(1, 1);
    private readonly Dictionary<string, Registro> _registros = [];
    private Copia? _copia;
    private bool _emAndamento;
    private bool _resolvendo;
    private OperacaoPendente? _pelaMetade;

    public CopiaAntes(
        Sessao sessao,
        EstadoAparelho estado,
        Escritor escritor,
        LeituraAutomatica leitura,
        IRepositorioTexto repositorio,
        LogBle log,
        TimeProvider? relogio = null)
    {
        _sessao = sessao;
        _estado = estado;
        _escritor = escritor;
        _repositorio = repositorio;
        _log = log;
        _relogio = relogio ?? TimeProvider.System;
        leitura.Terminou += AoTerminarLeitura;
        _escritor.Bloqueio = MotivoBloqueio;
        Carregada = Task.Run(CarregarAsync);
    }

    /// <summary>
    /// Por que uma escrita de áudio (da tela ou da cópia do link) não pode sair agora, ou null:
    /// uma operação anda, ainda tem a marca ou ficou pela metade. Só o Voltar à cópia escreve.
    /// </summary>
    public string? MotivoBloqueio()
    {
        lock (_trava)
        {
            if (_pelaMetade is not null)
                return MsgPelaMetadePendente;
            if (_emAndamento || _resolvendo || _copia?.Operacao is not null)
                return MsgOutraOperacao;
            return null;
        }
    }

    /// <summary>Completa quando a cópia guardada foi lida.</summary>
    public Task Carregada { get; }

    /// <summary>A cópia, a marca ou o "aplicado pela metade" mudou. Pode chegar em qualquer thread.</summary>
    public event Action? Mudou;

    /// <summary>A última cópia guardada (com a marca, se a operação não terminou).</summary>
    public Copia? Ultima
    {
        get
        {
            lock (_trava)
                return _copia;
        }
    }

    /// <summary>A operação que caiu no meio, depois da leitura de uma conexão nova; null = nada pela metade.</summary>
    public OperacaoPendente? PelaMetade
    {
        get
        {
            lock (_trava)
                return _pelaMetade;
        }
    }

    /// <summary>Uma operação (ou o Voltar à cópia) está andando agora.</summary>
    public bool EmAndamento
    {
        get
        {
            lock (_trava)
                return _emAndamento || _resolvendo;
        }
    }

    /// <summary>
    /// Ensina a cópia a lidar com um tipo de operação: o que fazer ao achar a marca depois da
    /// leitura de uma conexão nova, e como refazê-la (Terminar).
    /// </summary>
    public void Registrar(string tipo, Func<OperacaoPendente, Task<Retomada>> aoReconectar, Func<OperacaoPendente, Task<ResultadoCopia>> refazer)
    {
        lock (_trava)
            _registros[tipo] = new Registro(aoReconectar, refazer);
    }

    // ------------------------------------------------------------------ a operação

    /// <summary>
    /// Guarda a cópia (o espelho agora) e a marca da operação, antes de mexer. Com
    /// <paramref name="manterCopia"/> (o Terminar), fica a cópia que já estava. Se não conseguir
    /// guardar, nada muda e a operação não deve começar.
    /// </summary>
    public async Task<ResultadoCopia> IniciarAsync(OperacaoPendente operacao, bool manterCopia = false)
    {
        await Carregada;
        if (!SessaoAberta)
            return ResultadoCopia.Falha(Sessao.MotivoSemConexao);

        IReadOnlyList<ValorCopiado>? retrato = null;
        if (!manterCopia || Ultima is null)
        {
            if (!_estado.Pronto || _estado.Lendo)
                return ResultadoCopia.Falha(_estado.Lendo ? Escritor.MsgLendo : Escritor.MsgSemLeitura);
            retrato = Retratar(_estado.Copiar());
        }

        Copia? anterior;
        OperacaoPendente? pelaMetadeAntes;
        lock (_trava)
        {
            if (_emAndamento || _resolvendo)
                return ResultadoCopia.Falha(MsgOutraOperacao);
            // Pela metade, só o Terminar (que mantém a cópia) ou o Voltar à cópia.
            if (_pelaMetade is not null && !manterCopia)
                return ResultadoCopia.Falha(MsgPelaMetadePendente);
            anterior = _copia;
            pelaMetadeAntes = _pelaMetade;
            var nova = retrato is null
                ? anterior! with { Operacao = operacao }
                : new Copia(_relogio.GetLocalNow(), retrato, operacao);
            _copia = nova;
            _emAndamento = true;
            _pelaMetade = null;
        }

        if (!await GravarAtualAsync())
        {
            lock (_trava)
            {
                _copia = anterior;
                _emAndamento = false;
                _pelaMetade = pelaMetadeAntes;
            }
            _log.Evento($"{operacao.Descricao}: {MsgNaoGuardou}");
            return ResultadoCopia.Falha(MsgNaoGuardou);
        }
        _log.Evento(retrato is null
            ? $"cópia antes mantida; marca: {operacao.Descricao}"
            : $"cópia antes guardada ({retrato.Count} valores); marca: {operacao.Descricao}");
        Notificar();
        return ResultadoCopia.Sucesso;
    }

    /// <summary>Anota até onde a operação chegou (o salvar: o 240 saiu). False = não gravou.</summary>
    public async Task<bool> AnotarEtapaAsync(string etapa)
    {
        lock (_trava)
        {
            if (_copia?.Operacao is not { } op)
                return false;
            _copia = _copia with { Operacao = op with { Etapa = etapa } };
        }
        var gravou = await GravarAtualAsync();
        if (!gravou)
            _log.Evento($"não consegui anotar a etapa {etapa} da operação");
        return gravou;
    }

    /// <summary>A operação terminou: a marca sai (a cópia fica, para consulta).</summary>
    public async Task ConcluirAsync()
    {
        bool mudou;
        lock (_trava)
        {
            _emAndamento = false;
            _pelaMetade = null;
            mudou = _copia?.Operacao is not null;
            if (mudou)
                _copia = _copia! with { Operacao = null };
        }
        if (mudou && !await GravarAtualAsync())
            _log.Evento("não consegui tirar a marca da operação do celular");
        Notificar();
    }

    /// <summary>A operação parou sem terminar (a conexão caiu): a marca fica para a próxima conexão.</summary>
    public void Interromper()
    {
        lock (_trava)
            _emAndamento = false;
        Notificar();
    }

    /// <summary>
    /// A operação parou no meio com a conexão aberta (a leitura depois de carregar parou): fica
    /// "aplicado pela metade" já, sem esperar outra conexão.
    /// </summary>
    public void MarcarPelaMetade()
    {
        lock (_trava)
        {
            _emAndamento = false;
            if (_copia?.Operacao is { } op)
                _pelaMetade = op;
        }
        Notificar();
    }

    // ------------------------------------------------------------------ aplicado pela metade

    /// <summary>Terminar: refaz a operação que caiu no meio, com a mesma cópia.</summary>
    public async Task<ResultadoCopia> TerminarAsync()
    {
        OperacaoPendente? op;
        Registro? registro;
        lock (_trava)
        {
            op = _pelaMetade;
            if (op is null)
                return ResultadoCopia.Falha(MsgNadaPelaMetade);
            if (_emAndamento || _resolvendo)
                return ResultadoCopia.Falha(MsgOutraOperacao);
            registro = _registros.GetValueOrDefault(op.Tipo);
        }
        if (registro is null)
            return ResultadoCopia.Falha(MsgNaoSeiRefazer);

        _log.Evento($"aplicado pela metade: terminar ({op.Descricao})");
        ResultadoCopia r;
        try
        {
            r = await registro.Refazer(op);
        }
        catch (Exception e)
        {
            _log.Evento($"falha ao refazer ({e.GetType().Name})");
            r = ResultadoCopia.Falha($"Erro inesperado ({e.GetType().Name}).");
        }

        if (!r.Ok)
        {
            // Não começou (trava, cópia) ou parou com a conexão aberta: continua pela metade.
            // Se a conexão caiu, a próxima leitura completa decide de novo.
            lock (_trava)
            {
                if (!_emAndamento && _copia?.Operacao is { } marca && SessaoAberta)
                    _pelaMetade = marca;
            }
            Notificar();
        }
        return r;
    }

    /// <summary>
    /// Voltar à cópia: escreve pelo escritor cada parâmetro de áudio da cópia que difere do estado
    /// atual (só esses), sem o link repetir. Precisa da chave "Ajustes de áudio". Se todos
    /// voltarem, a marca sai.
    /// </summary>
    public async Task<ResultadoCopia> VoltarAsync()
    {
        Copia? copia;
        OperacaoPendente? op;
        lock (_trava)
        {
            op = _pelaMetade;
            copia = _copia;
            if (op is null)
                return ResultadoCopia.Falha(MsgNadaPelaMetade);
            if (copia is null)
                return ResultadoCopia.Falha(MsgSemCopia);
            if (_emAndamento || _resolvendo)
                return ResultadoCopia.Falha(MsgOutraOperacao);
            _emAndamento = true;
        }
        Notificar();

        var concluir = false;
        try
        {
            if (!SessaoAberta)
                return ResultadoCopia.Falha(Sessao.MotivoSemConexao);
            if (_estado.Lendo || !_estado.Pronto)
                return ResultadoCopia.Falha(_estado.Lendo ? Escritor.MsgLendo : Escritor.MsgSemLeitura);

            var (escritas, ilegiveis) = Diferencas(copia, _estado.Copiar());
            if (ilegiveis > 0)
            {
                // Voltar só uma parte daria "voltou" sem ter voltado: nada sai.
                _log.Evento($"voltar à cópia: {ilegiveis} valores da cópia ilegíveis ({op.Descricao})");
                return ResultadoCopia.Falha(MsgCopiaIlegivel(ilegiveis));
            }
            if (escritas.Count > 0 && _sessao.Conferir(escritas[0].Quadro) is { } motivo)
            {
                // Recusado antes de mexer em qualquer parâmetro (chave desligada, só leitura).
                _log.Recusado(escritas[0].Quadro, motivo);
                return ResultadoCopia.Falha(motivo);
            }

            _log.Evento($"voltar à cópia: {escritas.Count} ajustes diferentes ({op.Descricao})");
            var falhas = 0;
            string? ultimoMotivo = null;
            foreach (var e in escritas)
            {
                var r = await _escritor.EscreverAsync(e.Parametro, e.Indice, e.Valor, espelhar: false, daCopia: true);
                if (!r.Ok || r.Diferente)
                {
                    falhas++;
                    ultimoMotivo = r.Motivo ?? $"o aparelho ficou em {r.Valor?.NoFio}";
                }
            }
            if (falhas > 0)
            {
                _log.Evento($"voltar à cópia: {falhas} ajustes não voltaram ({ultimoMotivo})");
                return ResultadoCopia.Falha(falhas == 1
                    ? $"1 ajuste não voltou: {ultimoMotivo}"
                    : $"{falhas} ajustes não voltaram: {ultimoMotivo}");
            }
            concluir = true;
            return ResultadoCopia.Sucesso;
        }
        finally
        {
            if (concluir)
            {
                await ConcluirAsync();
            }
            else
            {
                lock (_trava)
                    _emAndamento = false;
                Notificar();
            }
        }
    }

    /// <summary>
    /// Os parâmetros de áudio (Marco 1) da cópia diferentes do espelho, na ordem da leitura, e
    /// quantos valores da cópia não dá para decodificar ou escrever de volta.
    /// </summary>
    internal static (List<Escrita> Escritas, int Ilegiveis) Diferencas(Copia copia, IReadOnlyDictionary<Endereco, Valor> atual)
    {
        var guardados = new Dictionary<Endereco, string>();
        foreach (var v in copia.Valores)
            guardados[new Endereco(v.Codigo, v.Indice)] = v.NoFio;

        var escritas = new List<Escrita>();
        var ilegiveis = 0;
        foreach (var consulta in Catalogo.ConsultasMarco1)
        {
            var endereco = new Endereco(consulta.Parametro.Codigo, consulta.Indice);
            if (!guardados.TryGetValue(endereco, out var noFio))
                continue;
            if (consulta.Parametro.Decodificar(noFio).Valor is not { } valor)
            {
                ilegiveis++;
                continue;
            }
            if (atual.TryGetValue(endereco, out var agora) && Iguais(agora, valor))
                continue;
            var quadro = Codificar(consulta.Parametro, consulta.Indice, valor);
            if (quadro is null)
            {
                ilegiveis++;
                continue;
            }
            escritas.Add(new Escrita(consulta.Parametro, consulta.Indice, valor, quadro));
        }
        return (escritas, ilegiveis);
    }

    private static string? Codificar(Parametro p, int? indice, Valor v)
    {
        try
        {
            if (v.Numero is { } n)
                return p.CodificarEscrita(indice, n);
            return v.Texto is { } t ? p.CodificarEscrita(indice, t) : null;
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>Dois valores do aparelho dizem o mesmo (+0.0 e 0.0; a e A, sem diferença de caixa).</summary>
    public static bool Iguais(Valor a, Valor b)
    {
        if (a.Numero is { } x && b.Numero is { } y)
            return x == y;
        if (a.Texto is not null || b.Texto is not null)
            return string.Equals(a.Texto, b.Texto, StringComparison.OrdinalIgnoreCase);
        return string.Equals(a.NoFio.Trim(), b.NoFio.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    // ------------------------------------------------------------------ retomada

    private void AoTerminarLeitura(Retrato retrato)
    {
        if (!retrato.Completo || !SessaoAberta)
            return;
        _ = Task.Run(() => RetomarAsync());
    }

    private async Task RetomarAsync()
    {
        await Carregada;
        OperacaoPendente? op;
        Registro? registro;
        lock (_trava)
        {
            if (_emAndamento || _resolvendo || _copia?.Operacao is null)
                return;
            op = _copia.Operacao;
            registro = _registros.GetValueOrDefault(op.Tipo);
            _resolvendo = true;
        }
        Notificar();

        var retomada = Retomada.PelaMetade;
        try
        {
            if (registro is not null)
                retomada = await registro.AoReconectar(op);
        }
        catch (Exception e)
        {
            _log.Evento($"falha ao retomar ({e.GetType().Name})");
            retomada = Retomada.PelaMetade;
        }

        _log.Evento($"marca achada depois da leitura ({op.Descricao}): {retomada switch
        {
            Retomada.Resolvida => "resolvida",
            Retomada.Adiada => "fica para a próxima conexão",
            _ => "aplicado pela metade",
        }}");
        // O "resolvendo" só sai depois de a marca sair: uma operação nova não pode começar
        // no meio e ter a marca dela apagada.
        if (retomada == Retomada.Resolvida)
            await ConcluirAsync();
        lock (_trava)
        {
            _resolvendo = false;
            if (retomada == Retomada.PelaMetade && SessaoAberta)
                _pelaMetade = op;
        }
        Notificar();
    }

    // ------------------------------------------------------------------ guardado

    private bool SessaoAberta => _sessao.Estado == EstadoSessao.Conectado && _sessao.SegurandoConexao;

    private static IReadOnlyList<ValorCopiado> Retratar(IReadOnlyDictionary<Endereco, Valor> espelho)
    {
        var ordem = Catalogo.Consultas.Select((c, i) => (c, i)).ToDictionary(x => new Endereco(x.c.Parametro.Codigo, x.c.Indice), x => x.i);
        return espelho
            .OrderBy(kv => ordem.GetValueOrDefault(kv.Key, int.MaxValue))
            .Select(kv => new ValorCopiado(kv.Key.Codigo, kv.Key.Indice, kv.Value.NoFio))
            .ToArray();
    }

    /// <summary>
    /// Grava a cópia como ela está agora. O retrato é tirado já com a vez de gravar: um JSON mais
    /// velho nunca passa por cima de um mais novo.
    /// </summary>
    private async Task<bool> GravarAtualAsync()
    {
        await _gravacao.WaitAsync();
        try
        {
            string? json;
            lock (_trava)
                json = _copia is null ? null : ParaJson(_copia);
            return json is null || await Gravacao.GravarAsync(_repositorio, json);
        }
        finally
        {
            _gravacao.Release();
        }
    }

    private async Task CarregarAsync()
    {
        var json = await Gravacao.LerAsync(_repositorio);
        Copia? copia = null;
        if (json is not null)
        {
            try
            {
                copia = DeJson(json);
            }
            catch (Exception)
            {
                // Arquivo estragado: sem cópia.
            }
        }
        lock (_trava)
        {
            // Uma operação que começou antes de o arquivo ser lido vale mais.
            _copia ??= copia;
        }
        if (copia?.Operacao is { } op)
            _log.Evento($"marca guardada de uma operação que não terminou: {op.Descricao}");
        Notificar();
    }

    /// <summary>O JSON da cópia, para os arquivos privados.</summary>
    public static string ParaJson(Copia copia)
    {
        using var fluxo = new MemoryStream();
        using (var w = new Utf8JsonWriter(fluxo, new JsonWriterOptions
               {
                   Indented = true,
                   Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
               }))
        {
            w.WriteStartObject();
            w.WriteNumber("formato", Formato);
            w.WriteString("quando", copia.Quando.ToString("O", CultureInfo.InvariantCulture));
            if (copia.Operacao is { } op)
            {
                w.WriteStartObject("operacao");
                w.WriteString("tipo", op.Tipo);
                w.WriteString("alvo", op.Alvo);
                w.WriteString("descricao", op.Descricao);
                if (op.Nome is not null)
                    w.WriteString("nome", op.Nome);
                if (op.Etapa is not null)
                    w.WriteString("etapa", op.Etapa);
                w.WriteEndObject();
            }
            w.WriteStartArray("valores");
            foreach (var v in copia.Valores)
            {
                w.WriteStartObject();
                w.WriteString("codigo", v.Codigo);
                if (v.Indice is { } i)
                    w.WriteNumber("indice", i);
                w.WriteString("noFio", v.NoFio);
                w.WriteEndObject();
            }
            w.WriteEndArray();
            w.WriteEndObject();
        }
        return Encoding.UTF8.GetString(fluxo.ToArray());
    }

    /// <summary>Lê o JSON guardado. Fora do formato 1: null.</summary>
    public static Copia? DeJson(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var raiz = doc.RootElement;
        if (raiz.ValueKind != JsonValueKind.Object
            || !raiz.TryGetProperty("formato", out var formato) || !formato.TryGetInt32(out var f) || f != Formato
            || !raiz.TryGetProperty("valores", out var valores) || valores.ValueKind != JsonValueKind.Array)
            return null;

        var quando = raiz.TryGetProperty("quando", out var q) && q.ValueKind == JsonValueKind.String
                     && DateTimeOffset.TryParse(q.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var d)
            ? d
            : DateTimeOffset.MinValue;

        var lista = new List<ValorCopiado>();
        foreach (var v in valores.EnumerateArray())
        {
            if (v.ValueKind != JsonValueKind.Object
                || !v.TryGetProperty("codigo", out var codigo) || codigo.ValueKind != JsonValueKind.String
                || !v.TryGetProperty("noFio", out var noFio) || noFio.ValueKind != JsonValueKind.String)
                continue;
            int? indice = v.TryGetProperty("indice", out var i) && i.TryGetInt32(out var n) ? n : null;
            lista.Add(new ValorCopiado(codigo.GetString()!, indice, noFio.GetString()!));
        }

        OperacaoPendente? operacao = null;
        if (raiz.TryGetProperty("operacao", out var o) && o.ValueKind == JsonValueKind.Object)
        {
            string? Texto(string nome) => o.TryGetProperty(nome, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;
            if (Texto("tipo") is { } tipo && Texto("alvo") is { } alvo)
                operacao = new OperacaoPendente(tipo, alvo, Texto("descricao") ?? tipo, Texto("nome"), Texto("etapa"));
        }
        return new Copia(quando, lista, operacao);
    }

    private void Notificar()
    {
        try
        {
            Mudou?.Invoke();
        }
        catch (Exception e)
        {
            _log.Evento($"falha no aviso da cópia ({e.GetType().Name})");
        }
    }

    private sealed record Registro(Func<OperacaoPendente, Task<Retomada>> AoReconectar, Func<OperacaoPendente, Task<ResultadoCopia>> Refazer);

    /// <summary>Um ajuste a escrever: o parâmetro, onde, o valor e o quadro já montado.</summary>
    internal sealed record Escrita(Parametro Parametro, int? Indice, Valor Valor, string Quadro);
}
