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
/// <item><b>IA ativa:</b> uma tarefa foi iniciada pela porta, ou chegou um comando da IA. Termina
/// ao fim da tarefa ou na parada interna (o efeito de desligar a chave "Controle pela rede").</item>
/// <item><b>O toque sempre pausa</b> (IA ativa): um comando da tela nunca é recusado; ele passa e
/// pausa a IA; cada novo comando da tela reinicia os 10 s; com 10 s sem comandos da tela, a IA
/// retoma sozinha. <see cref="Pausou"/> e <see cref="Retomou"/> avisam a porta. Não há outro modo
/// (decisão do Denis, 30/10/2026).</item>
/// <item><b>Recusas à IA:</b> pausada (<see cref="MsgPausada"/>); um endereço que a tela mexeu
/// há menos de 10 s, mesmo sem tarefa (<see cref="MsgMexendo"/>); depois da parada interna
/// (<see cref="MsgParada"/>), até a porta liberar num pareamento novo.</item>
/// <item><b>Nunca travados:</b> o <c>*202*0*</c> (desligar o gerador) e o que o app faz sozinho
/// (origem App).</item>
/// </list>
/// </summary>
public sealed class UsoCompartilhado
{
    public const string MsgPausada = "IA pausada: o usuário está mexendo.";
    public const string MsgMexendo = "o usuário está mexendo nisso.";
    public const string MsgParada = "A IA foi parada.";

    /// <summary>A janela sem toques: para a IA retomar e para um endereço tocado voltar à IA.</summary>
    public static TimeSpan Janela { get; } = TimeSpan.FromSeconds(10);

    private readonly TimeProvider _relogio;
    private readonly LogBle? _log;

    private readonly object _trava = new();
    private readonly Dictionary<Endereco, DateTimeOffset> _toques = [];
    private EstadoIa _estado = EstadoIa.Inativa;
    private bool _parada;
    private string? _tarefa;
    private DateTimeOffset? _ativaDesde;
    private DateTimeOffset? _ultimoToque;
    private ITimer? _retomar;

    public UsoCompartilhado(TimeProvider? relogio = null, LogBle? log = null)
    {
        _relogio = relogio ?? TimeProvider.System;
        _log = log;
    }

    /// <summary>O estado, a tarefa ou a parada mudaram. Pode chegar em qualquer thread.</summary>
    public event Action? Mudou;

    /// <summary>A IA foi pausada por um toque da tela (o toque sempre pausa). Para a porta avisar a IA.</summary>
    public event Action? Pausou;

    /// <summary>10 s sem comandos da tela: a IA voltou a ativa. Para a porta avisar a IA.</summary>
    public event Action? Retomou;

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

    /// <summary>Depois da parada interna (a chave desligada), até a porta liberar num pareamento novo: todo comando da IA é recusado.</summary>
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

    /// <summary>Chamar com a trava. O toque da tela nunca é recusado: ele passa e pausa a IA.</summary>
    private string? AvaliarTela(ComandoNaPorta comando, DateTimeOffset agora, bool aplicar, ref bool pausou)
    {
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

    // ------------------------------------------------------------------ a tarefa e a parada interna

    /// <summary>
    /// A IA começou uma tarefa (a porta chama): a IA fica ativa com a frase (a tela não a mostra).
    /// Recusado depois da parada interna.
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

    /// <summary>A IA terminou a tarefa (a porta chama): a IA fica inativa.</summary>
    public void TerminarTarefa()
    {
        lock (_trava)
            Desativar();
        Avisar(Mudou);
    }

    /// <summary>
    /// PARAR IA interno, só como efeito de revogar o acesso (a porta chama ao desligar a chave): a
    /// IA fica inativa, e todo comando dela é recusado com <see cref="MsgParada"/> até
    /// <see cref="Liberar"/>.
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

    /// <summary>A porta libera a IA de novo, num pareamento novo.</summary>
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
