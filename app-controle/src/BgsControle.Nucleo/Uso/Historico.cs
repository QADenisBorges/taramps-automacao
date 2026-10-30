using System.Globalization;
using System.Text;
using System.Text.Json;
using BgsControle.Nucleo.Escrita;
using BgsControle.Nucleo.Registro;
using BgsControle.Nucleo.Seguranca;

namespace BgsControle.Nucleo.Uso;

/// <summary>
/// Uma linha do histórico: a hora, a origem, o tipo, o endereço e o valor, e se o comando foi
/// enviado ou recusado (com o motivo). Nunca leva uma senha.
/// </summary>
public sealed record EntradaHistorico(
    DateTimeOffset Hora,
    OrigemComando Origem,
    TipoComando Tipo,
    Endereco? Endereco,
    string? Valor,
    bool Enviado,
    string? Motivo);

/// <summary>
/// O histórico dos comandos (CAP-13): cada comando de escrita que chega à porta
/// (<see cref="Sessao.EnviarAsync"/>), da tela, da IA ou do app, enviado ou recusado. As leituras
/// e o quadro da senha nunca entram; a troca de senha entra sem as senhas.
/// <list type="bullet">
/// <item>A mais nova fica em cima, e <see cref="Registrou"/> avisa cada entrada nova.</item>
/// <item>Fica no celular, em <c>historico.json</c> (formato 1), com as últimas
/// <see cref="Maximo"/> entradas. As gravações se juntam a cada <see cref="IntervaloGravacao"/>.</item>
/// <item>Ao abrir, o histórico volta. Um arquivo estragado vira um histórico vazio, sem erro. Se a
/// gravação falhar, a lista em memória continua, e a próxima gravação tenta de novo.</item>
/// </list>
/// </summary>
public sealed class Historico
{
    public const int Formato = 1;
    public const int Maximo = 1000;

    public static TimeSpan IntervaloGravacao { get; } = TimeSpan.FromSeconds(2);

    private readonly IRepositorioTexto _repositorio;
    private readonly TimeProvider _relogio;
    private readonly LogBle? _log;

    private readonly object _trava = new();
    private readonly SemaphoreSlim _gravacao = new(1, 1);
    private readonly List<EntradaHistorico> _entradas = []; // da mais antiga à mais nova
    private ITimer? _agendada;
    private bool _sujo;

    public Historico(IRepositorioTexto repositorio, TimeProvider? relogio = null, LogBle? log = null)
    {
        _repositorio = repositorio;
        _relogio = relogio ?? TimeProvider.System;
        _log = log;
        Carregado = Task.Run(CarregarAsync);
    }

    /// <summary>Completa quando o <c>historico.json</c> foi lido (ausente ou estragado = vazio).</summary>
    public Task Carregado { get; }

    /// <summary>Uma entrada nova. Pode chegar em qualquer thread.</summary>
    public event Action<EntradaHistorico>? Registrou;

    /// <summary>A lista inteira, da mais nova à mais antiga.</summary>
    public IReadOnlyList<EntradaHistorico> Entradas
    {
        get
        {
            lock (_trava)
            {
                var copia = _entradas.ToArray();
                Array.Reverse(copia);
                return copia;
            }
        }
    }

    /// <summary>Registra um comando que chegou à porta. Nunca lança.</summary>
    internal void Registrar(ComandoNaPorta comando, OrigemComando origem, bool enviado, string? motivo)
    {
        EntradaHistorico entrada;
        lock (_trava)
        {
            // A hora lida com a trava: a ordem da lista é a ordem das horas.
            entrada = new EntradaHistorico(_relogio.GetLocalNow(), origem, comando.Tipo, comando.Endereco, comando.Valor,
                enviado, enviado ? null : motivo);
            _entradas.Add(entrada);
            Aparar();
            _sujo = true;
            AgendarGravacao();
        }
        try
        {
            Registrou?.Invoke(entrada);
        }
        catch (Exception e)
        {
            _log?.Evento($"falha no aviso do histórico ({e.GetType().Name})");
        }
    }

    /// <summary>Grava agora o que ainda não foi gravado (ao sair do app). False = não gravou.</summary>
    public async Task<bool> GravarAgoraAsync()
    {
        lock (_trava)
        {
            _agendada?.Dispose();
            _agendada = null;
        }
        return await GravarAsync();
    }

    // ------------------------------------------------------------------ guardado

    /// <summary>Chamar com a trava.</summary>
    private void Aparar()
    {
        if (_entradas.Count > Maximo)
            _entradas.RemoveRange(0, _entradas.Count - Maximo);
    }

    /// <summary>Uma gravação por vez, juntando o que chegar até ela. Chamar com a trava.</summary>
    private void AgendarGravacao()
    {
        if (_agendada is not null)
            return;
        _agendada = _relogio.CreateTimer(_ => _ = GravarAgendadaAsync(), null, IntervaloGravacao, Timeout.InfiniteTimeSpan);
    }

    private async Task GravarAgendadaAsync()
    {
        lock (_trava)
        {
            _agendada?.Dispose();
            _agendada = null;
        }
        await GravarAsync();
    }

    private async Task<bool> GravarAsync()
    {
        await Carregado; // nunca grava por cima do guardado antes de lê-lo
        await _gravacao.WaitAsync();
        try
        {
            string json;
            lock (_trava)
            {
                if (!_sujo)
                    return true;
                _sujo = false;
                json = ParaJson(_entradas);
            }
            if (await Gravacao.GravarAsync(_repositorio, json))
                return true;
            // A lista em memória continua, e outra gravação fica agendada.
            lock (_trava)
            {
                _sujo = true;
                AgendarGravacao();
            }
            _log?.Evento("não consegui guardar o histórico no celular");
            return false;
        }
        catch (Exception e)
        {
            lock (_trava)
            {
                _sujo = true;
                AgendarGravacao();
            }
            _log?.Evento($"falha ao guardar o histórico ({e.GetType().Name})");
            return false;
        }
        finally
        {
            _gravacao.Release();
        }
    }

    private async Task CarregarAsync()
    {
        var json = await Gravacao.LerAsync(_repositorio);
        IReadOnlyList<EntradaHistorico> lidas = [];
        if (json is not null)
        {
            try
            {
                lidas = DeJson(json);
            }
            catch (Exception)
            {
                // Arquivo estragado: começa vazio, sem erro.
            }
        }
        lock (_trava)
        {
            // O que entrou durante a leitura é mais novo que o guardado.
            _entradas.InsertRange(0, lidas);
            Aparar();
        }
    }

    /// <summary>O JSON do <c>historico.json</c> (formato 1), da mais antiga à mais nova.</summary>
    public static string ParaJson(IEnumerable<EntradaHistorico> entradas)
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
            w.WriteStartArray("entradas");
            foreach (var e in entradas)
            {
                w.WriteStartObject();
                w.WriteString("hora", e.Hora.ToString("O", CultureInfo.InvariantCulture));
                w.WriteString("origem", e.Origem.ToString());
                w.WriteString("tipo", e.Tipo.ToString());
                if (e.Endereco is { } endereco)
                {
                    w.WriteString("codigo", endereco.Codigo);
                    if (endereco.Indice is { } i)
                        w.WriteNumber("indice", i);
                }
                if (e.Valor is not null)
                    w.WriteString("valor", e.Valor);
                w.WriteBoolean("enviado", e.Enviado);
                if (e.Motivo is not null)
                    w.WriteString("motivo", e.Motivo);
                w.WriteEndObject();
            }
            w.WriteEndArray();
            w.WriteEndObject();
        }
        return Encoding.UTF8.GetString(fluxo.ToArray());
    }

    /// <summary>
    /// Lê o <c>historico.json</c>, da mais antiga à mais nova. Fora do formato 1, ou com outra
    /// forma: vazio. Uma entrada sem hora, origem, tipo ou "enviado" legíveis é descartada (também a
    /// de um tipo que o app não tem mais, como o <c>CarregarMemoria</c>, que saiu com as memórias do
    /// processador em 29/10/2026).
    /// </summary>
    public static IReadOnlyList<EntradaHistorico> DeJson(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var raiz = doc.RootElement;
        if (raiz.ValueKind != JsonValueKind.Object
            || !raiz.TryGetProperty("formato", out var f) || f.ValueKind != JsonValueKind.Number
            || !f.TryGetInt32(out var formato) || formato != Formato
            || !raiz.TryGetProperty("entradas", out var lista) || lista.ValueKind != JsonValueKind.Array)
            return [];

        var saida = new List<EntradaHistorico>();
        foreach (var item in lista.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object
                || Texto(item, "hora") is not { } h
                || !DateTimeOffset.TryParse(h, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var hora)
                || Texto(item, "origem") is not { } o || !Enum.TryParse<OrigemComando>(o, out var origem) || !Enum.IsDefined(origem)
                || Texto(item, "tipo") is not { } t || !Enum.TryParse<TipoComando>(t, out var tipo) || !Enum.IsDefined(tipo)
                || !item.TryGetProperty("enviado", out var en) || en.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                continue;
            Endereco? endereco = Texto(item, "codigo") is { } codigo
                ? new Endereco(codigo, item.TryGetProperty("indice", out var i) && i.ValueKind == JsonValueKind.Number && i.TryGetInt32(out var n) ? n : null)
                : null;
            saida.Add(new EntradaHistorico(hora, origem, tipo, endereco, Texto(item, "valor"), en.GetBoolean(), Texto(item, "motivo")));
        }
        return saida.Count > Maximo ? saida[^Maximo..] : saida;
    }

    private static string? Texto(JsonElement o, string nome) =>
        o.TryGetProperty(nome, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;
}
