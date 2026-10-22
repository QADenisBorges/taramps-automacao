using System.Text;
using System.Text.Json;
using BgsControle.Nucleo.Escrita;
using BgsControle.Nucleo.Registro;
using BgsControle.Nucleo.Seguranca;

namespace BgsControle.Nucleo.Uso;

/// <summary>Como terminou um pedido ao uso compartilhado.</summary>
public sealed record ResultadoUso(bool Ok, string? Motivo);

/// <summary>
/// O uso ao mesmo tempo (Denis e IA, CAP-13 e <c>porta-de-comandos.md</c>): pela origem de cada
/// comando que chega à porta, decide o que passa enquanto a IA trabalha. A porta pergunta aqui
/// depois das chaves de acesso; a recusa de uma chave desligada não muda.
/// <list type="bullet">
/// <item><b>IA ativa:</b> uma tarefa (com a frase da faixa) foi iniciada, ou chegou um comando da
/// IA. Termina ao fim da tarefa ou no PARAR IA.</item>
/// <item><b>Travado</b> (IA ativa, modo padrão): um comando da tela é recusado com
/// <see cref="MsgTravado"/>.</item>
/// <item><b>Pausa</b> (IA ativa, modo "O toque pausa a IA"): um comando da tela passa e pausa a
/// IA; cada novo comando da tela reinicia os 10 s; com 10 s sem comandos da tela, a IA retoma
/// sozinha. <see cref="Pausou"/> e <see cref="Retomou"/> avisam a 9b.</item>
/// <item><b>Recusas à IA:</b> pausada (<see cref="MsgPausada"/>); um endereço que a tela mexeu
/// há menos de 10 s, mesmo sem tarefa (<see cref="MsgMexendo"/>); depois do PARAR IA
/// (<see cref="MsgParada"/>), até a 9b liberar num pareamento novo.</item>
/// <item><b>Nunca travados:</b> o <c>*202*0*</c> (desligar o gerador), o PARAR IA e o que o
/// app faz sozinho (origem App).</item>
/// </list>
/// O modo do toque fica em <c>uso-ia.json</c> (formato 1); só a tela muda. Até ser lido, vale o
/// padrão (travar).
/// </summary>
public sealed class UsoCompartilhado
{
    public const int Formato = 1;

    public const string MsgTravado = "Controles travados. Toque em PARAR IA para mexer.";
    public const string MsgPausada = "IA pausada: o usuário está mexendo.";
    public const string MsgMexendo = "o usuário está mexendo nisso.";
    public const string MsgParada = "A IA foi parada.";
    public const string MsgSoATela = "Só a tela muda o modo do toque.";
    public const string MsgNaoGuardou = "Não consegui guardar o modo do toque no celular.";

    /// <summary>A janela sem toques: para a IA retomar e para um endereço tocado voltar à IA.</summary>
    public static TimeSpan Janela { get; } = TimeSpan.FromSeconds(10);

    private readonly IRepositorioTexto _repositorio;
    private readonly TimeProvider _relogio;
    private readonly LogBle? _log;

    private readonly object _trava = new();
    private readonly SemaphoreSlim _gravacao = new(1, 1);
    private readonly Dictionary<Endereco, DateTimeOffset> _toques = [];
    private ModoToque _modo = ModoToque.TravarAtePararIa;
    private EstadoIa _estado = EstadoIa.Inativa;
    private bool _parada;
    private string? _tarefa;
    private DateTimeOffset? _ativaDesde;
    private DateTimeOffset? _ultimoToque;
    private ITimer? _retomar;

    public UsoCompartilhado(IRepositorioTexto repositorio, TimeProvider? relogio = null, LogBle? log = null)
    {
        _repositorio = repositorio;
        _relogio = relogio ?? TimeProvider.System;
        _log = log;
        Carregado = Task.Run(CarregarAsync);
    }

    /// <summary>Completa quando o <c>uso-ia.json</c> foi lido (ausente ou estragado = o padrão).</summary>
    public Task Carregado { get; }

    /// <summary>O estado, o modo, a tarefa ou a parada mudaram. Pode chegar em qualquer thread.</summary>
    public event Action? Mudou;

    /// <summary>A IA foi pausada por um toque (modo "O toque pausa a IA"). Para a 9b avisar a IA.</summary>
    public event Action? Pausou;

    /// <summary>10 s sem comandos da tela: a IA voltou a ativa. Para a 9b avisar a IA.</summary>
    public event Action? Retomou;

    public ModoToque Modo
    {
        get
        {
            lock (_trava)
                return _modo;
        }
    }

    public EstadoIa Estado
    {
        get
        {
            lock (_trava)
                return _estado;
        }
    }

    /// <summary>A IA está ativa ou pausada.</summary>
    public bool IaAtiva => Estado != EstadoIa.Inativa;

    /// <summary>Os controles da tela estão travados agora (IA ativa, modo padrão).</summary>
    public bool Travado
    {
        get
        {
            lock (_trava)
                return _estado != EstadoIa.Inativa && _modo == ModoToque.TravarAtePararIa;
        }
    }

    /// <summary>Depois do PARAR IA, até a 9b liberar: todo comando da IA é recusado.</summary>
    public bool Parada
    {
        get
        {
            lock (_trava)
                return _parada;
        }
    }

    /// <summary>A frase da faixa ("IA ajustando o EQ da FILME"), ou null sem tarefa.</summary>
    public string? Tarefa
    {
        get
        {
            lock (_trava)
                return _tarefa;
        }
    }

    /// <summary>Desde quando a IA está ativa (ou pausada), ou null se está inativa.</summary>
    public DateTimeOffset? AtivaDesde
    {
        get
        {
            lock (_trava)
                return _ativaDesde;
        }
    }

    // ------------------------------------------------------------------ a porta

    /// <summary>
    /// O que a porta faz com este comando agora, com os efeitos: null = passa (e conta como toque,
    /// pausa ou ativa a IA); senão, o motivo da recusa. A porta chama depois das chaves.
    /// </summary>
    internal string? Admitir(ComandoNaPorta comando, OrigemComando origem) => Avaliar(comando, origem, aplicar: true);

    /// <summary>O mesmo que <see cref="Admitir"/>, sem nenhum efeito (o <see cref="Sessao.Conferir"/>).</summary>
    internal string? Consultar(ComandoNaPorta comando, OrigemComando origem) => Avaliar(comando, origem, aplicar: false);

    private string? Avaliar(ComandoNaPorta comando, OrigemComando origem, bool aplicar)
    {
        if (comando.DesligaGerador || origem == OrigemComando.App)
            return null;

        bool pausou = false, ativou = false;
        string? motivo;
        lock (_trava)
        {
            var agora = _relogio.GetUtcNow();
            motivo = origem == OrigemComando.Tela
                ? AvaliarTela(comando, agora, aplicar, ref pausou)
                : AvaliarIa(comando, agora, aplicar, ref ativou);
        }
        if (pausou)
        {
            _log?.Evento("IA pausada: o usuário está mexendo");
            Avisar(Pausou);
        }
        if (pausou || ativou)
            Avisar(Mudou);
        return motivo;
    }

    /// <summary>Chamar com a trava.</summary>
    private string? AvaliarTela(ComandoNaPorta comando, DateTimeOffset agora, bool aplicar, ref bool pausou)
    {
        if (_estado != EstadoIa.Inativa && _modo == ModoToque.TravarAtePararIa)
            return MsgTravado;
        if (!aplicar)
            return null;

        if (comando.Endereco is { } endereco)
            _toques[endereco] = agora;
        _ultimoToque = agora;
        if (_estado == EstadoIa.Ativa)
        {
            _estado = EstadoIa.Pausada;
            pausou = true;
        }
        if (_estado == EstadoIa.Pausada)
            AgendarRetomada(Janela);
        return null;
    }

    /// <summary>Chamar com a trava.</summary>
    private string? AvaliarIa(ComandoNaPorta comando, DateTimeOffset agora, bool aplicar, ref bool ativou)
    {
        if (_parada)
            return MsgParada;
        if (_estado == EstadoIa.Pausada)
            return MsgPausada;
        if (comando.Endereco is { } endereco && _toques.TryGetValue(endereco, out var toque))
        {
            if (agora - toque < Janela)
                return MsgMexendo;
            if (aplicar)
                _toques.Remove(endereco);
        }
        if (aplicar && _estado == EstadoIa.Inativa)
        {
            _estado = EstadoIa.Ativa;
            _ativaDesde = agora;
            ativou = true;
        }
        return null;
    }

    // ------------------------------------------------------------------ a IA e o PARAR IA

    /// <summary>
    /// A IA começou uma tarefa (a 9b chama): a IA fica ativa e a faixa mostra a frase. Recusado
    /// depois do PARAR IA.
    /// </summary>
    public ResultadoUso IniciarTarefa(string frase)
    {
        lock (_trava)
        {
            if (_parada)
                return new ResultadoUso(false, MsgParada);
            _tarefa = string.IsNullOrWhiteSpace(frase) ? null : frase.Trim();
            if (_estado == EstadoIa.Inativa)
            {
                _estado = EstadoIa.Ativa;
                _ativaDesde = _relogio.GetUtcNow();
            }
        }
        Avisar(Mudou);
        return new ResultadoUso(true, null);
    }

    /// <summary>A IA terminou a tarefa (a 9b chama): a IA fica inativa e os controles destravam.</summary>
    public void TerminarTarefa()
    {
        lock (_trava)
            Desativar();
        Avisar(Mudou);
    }

    /// <summary>
    /// PARAR IA: nunca travado e sem confirmação. A IA fica inativa, e todo comando dela é recusado
    /// com <see cref="MsgParada"/> até <see cref="Liberar"/>.
    /// </summary>
    public void PararIa()
    {
        lock (_trava)
        {
            Desativar();
            _parada = true;
        }
        _log?.Evento("PARAR IA");
        Avisar(Mudou);
    }

    /// <summary>A 9b libera a IA de novo, num pareamento novo.</summary>
    public void Liberar()
    {
        lock (_trava)
        {
            if (!_parada)
                return;
            _parada = false;
        }
        Avisar(Mudou);
    }

    /// <summary>Chamar com a trava.</summary>
    private void Desativar()
    {
        _estado = EstadoIa.Inativa;
        _tarefa = null;
        _ativaDesde = null;
        _retomar?.Dispose();
        _retomar = null;
    }

    /// <summary>Chamar com a trava.</summary>
    private void AgendarRetomada(TimeSpan quando)
    {
        if (_retomar is null)
            _retomar = _relogio.CreateTimer(_ => AoVencerPausa(), null, quando, Timeout.InfiniteTimeSpan);
        else
            _retomar.Change(quando, Timeout.InfiniteTimeSpan);
    }

    private void AoVencerPausa()
    {
        lock (_trava)
        {
            if (_estado != EstadoIa.Pausada)
                return;
            var falta = _ultimoToque is { } t ? t + Janela - _relogio.GetUtcNow() : TimeSpan.Zero;
            if (falta > TimeSpan.Zero)
            {
                AgendarRetomada(falta);
                return;
            }
            _estado = EstadoIa.Ativa;
            _retomar?.Dispose();
            _retomar = null;
        }
        _log?.Evento("IA retomada: 10 s sem toques");
        Avisar(Retomou);
        Avisar(Mudou);
    }

    // ------------------------------------------------------------------ o modo do toque

    /// <summary>
    /// Muda o modo do toque e guarda. Só a tela muda: outra origem (no parâmetro ou a ambiente) é
    /// recusada. Sem guardar, o modo volta ao de antes.
    /// </summary>
    public async Task<ResultadoUso> DefinirModoAsync(ModoToque modo, OrigemComando origem)
    {
        if (origem != OrigemComando.Tela || Origem.Atual != OrigemComando.Tela)
            return new ResultadoUso(false, MsgSoATela);
        if (!Enum.IsDefined(modo))
            throw new ArgumentOutOfRangeException(nameof(modo));
        await Carregado;

        await _gravacao.WaitAsync();
        try
        {
            lock (_trava)
            {
                if (_modo == modo)
                    return new ResultadoUso(true, null);
            }
            // O modo só muda depois de guardado: um comando no meio é julgado pelo modo de antes.
            if (!await Gravacao.GravarAsync(_repositorio, ParaJson(modo)))
            {
                _log?.Evento("não consegui guardar o modo do toque");
                return new ResultadoUso(false, MsgNaoGuardou);
            }
            lock (_trava)
                _modo = modo;
            Avisar(Mudou);
            return new ResultadoUso(true, null);
        }
        finally
        {
            _gravacao.Release();
        }
    }

    private async Task CarregarAsync()
    {
        var json = await Gravacao.LerAsync(_repositorio);
        if (json is null || DeJson(json) is not { } modo)
            return;
        lock (_trava)
            _modo = modo;
        Avisar(Mudou);
    }

    /// <summary>O JSON do <c>uso-ia.json</c> (formato 1).</summary>
    public static string ParaJson(ModoToque modo)
    {
        using var fluxo = new MemoryStream();
        using (var w = new Utf8JsonWriter(fluxo, new JsonWriterOptions { Indented = true }))
        {
            w.WriteStartObject();
            w.WriteNumber("formato", Formato);
            w.WriteString("modoToque", modo == ModoToque.PausarIa ? "pausar-ia" : "travar-ate-parar-ia");
            w.WriteEndObject();
        }
        return Encoding.UTF8.GetString(fluxo.ToArray());
    }

    /// <summary>O modo guardado, ou null se o arquivo está estragado ou fora do formato 1.</summary>
    public static ModoToque? DeJson(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var raiz = doc.RootElement;
            if (raiz.ValueKind != JsonValueKind.Object
                || !raiz.TryGetProperty("formato", out var f) || f.ValueKind != JsonValueKind.Number
                || !f.TryGetInt32(out var formato) || formato != Formato
                || !raiz.TryGetProperty("modoToque", out var m) || m.ValueKind != JsonValueKind.String)
                return null;
            return m.GetString() switch
            {
                "pausar-ia" => ModoToque.PausarIa,
                "travar-ate-parar-ia" => ModoToque.TravarAtePararIa,
                _ => null,
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private void Avisar(Action? evento)
    {
        try
        {
            evento?.Invoke();
        }
        catch (Exception e)
        {
            _log?.Evento($"falha no aviso do uso compartilhado ({e.GetType().Name})");
        }
    }
}
