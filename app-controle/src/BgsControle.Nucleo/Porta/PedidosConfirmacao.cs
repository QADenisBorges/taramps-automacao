using BgsControle.Nucleo.Registro;

namespace BgsControle.Nucleo.Porta;

/// <summary>
/// A folha de risco do app (decisão do Denis, 30/10/2026): o título diz o que muda ("Aplicar a
/// memória FILME?") e o risco vem de <c>chaves-de-acesso.md</c> sem mudar o texto. Os botões são
/// sempre "Confirmar" e "Cancelar". Nada aqui menciona a IA.
/// </summary>
public sealed record PedidoConfirmacao(string Titulo, string? Risco);

/// <summary>Como terminou um pedido de confirmação.</summary>
public enum DecisaoConfirmacao
{
    /// <summary>O usuário tocou em "Confirmar": a operação segue.</summary>
    Confirmou,

    /// <summary>"Cancelar".</summary>
    Recusou,

    /// <summary>60 s sem resposta.</summary>
    SemResposta,

    /// <summary>O revogar (a chave desligada, a porta fechou) durante a espera.</summary>
    Parada,

    /// <summary>Já havia um pedido esperando: este nem abriu a folha.</summary>
    Ocupado,
}

/// <summary>
/// A trava 5 (<c>porta-de-comandos.md</c>): uma operação de risco pedida pela porta só roda depois
/// do toque do usuário na folha de risco do app. Um pedido por vez; o prazo é <see cref="Prazo"/> (60 s) pelo
/// <see cref="TimeProvider"/>.
/// </summary>
public sealed class PedidosConfirmacao
{
    public const string MsgRecusou = "O usuário recusou na tela.";
    public const string MsgSemResposta = "O usuário não confirmou a tempo.";
    public const string MsgOcupado = "Já há um pedido esperando confirmação na tela.";

    public static TimeSpan Prazo { get; } = TimeSpan.FromSeconds(60);

    private readonly TimeProvider _relogio;
    private readonly LogBle? _log;
    private readonly object _trava = new();
    private PedidoConfirmacao? _atual;
    private DateTimeOffset? _abertoEm;
    private TaskCompletionSource<DecisaoConfirmacao>? _decisao;
    private ITimer? _prazo;

    public PedidosConfirmacao(TimeProvider? relogio = null, LogBle? log = null)
    {
        _relogio = relogio ?? TimeProvider.System;
        _log = log;
    }

    /// <summary>Uma folha abriu ou fechou. Pode chegar em qualquer thread.</summary>
    public event Action? Mudou;

    /// <summary>O pedido esperando o toque, ou null.</summary>
    public PedidoConfirmacao? Atual
    {
        get
        {
            lock (_trava)
                return _atual;
        }
    }

    /// <summary>Quando a folha do pedido atual abriu (o prazo de 60 s conta daqui), ou null.</summary>
    public DateTimeOffset? AbertoEm
    {
        get
        {
            lock (_trava)
                return _abertoEm;
        }
    }

    /// <summary>Abre a folha e espera a decisão (no máximo 60 s). Com outro esperando: <see cref="DecisaoConfirmacao.Ocupado"/>.</summary>
    public Task<DecisaoConfirmacao> PedirAsync(PedidoConfirmacao pedido)
    {
        TaskCompletionSource<DecisaoConfirmacao> decisao;
        lock (_trava)
        {
            if (_atual is not null)
                return Task.FromResult(DecisaoConfirmacao.Ocupado);
            _atual = pedido;
            _abertoEm = _relogio.GetUtcNow();
            decisao = new TaskCompletionSource<DecisaoConfirmacao>(TaskCreationOptions.RunContinuationsAsynchronously);
            _decisao = decisao;
            _prazo = _relogio.CreateTimer(_ => Decidir(decisao, DecisaoConfirmacao.SemResposta), null, Prazo, Timeout.InfiniteTimeSpan);
        }
        _log?.Evento($"porta: a IA pede confirmação ({pedido.Titulo})");
        Avisar();
        return decisao.Task;
    }

    /// <summary>
    /// "Confirmar". Com <paramref name="pedido"/>, só vale se ele ainda é o que espera (a folha
    /// que o usuário viu, e não um pedido novo que chegou no lugar).
    /// </summary>
    public void Confirmar(PedidoConfirmacao? pedido = null) => DecidirAtual(DecisaoConfirmacao.Confirmou, pedido);

    /// <summary>"Cancelar".</summary>
    public void Recusar(PedidoConfirmacao? pedido = null) => DecidirAtual(DecisaoConfirmacao.Recusou, pedido);

    /// <summary>O revogar ou a porta fechando: a folha fecha e o pedido é recusado.</summary>
    public void Cancelar() => DecidirAtual(DecisaoConfirmacao.Parada, null);

    private void DecidirAtual(DecisaoConfirmacao decisao, PedidoConfirmacao? pedido)
    {
        TaskCompletionSource<DecisaoConfirmacao>? alvo;
        lock (_trava)
        {
            if (pedido is not null && !ReferenceEquals(pedido, _atual))
                return;
            alvo = _decisao;
        }
        if (alvo is not null)
            Decidir(alvo, decisao);
    }

    private void Decidir(TaskCompletionSource<DecisaoConfirmacao> alvo, DecisaoConfirmacao decisao)
    {
        lock (_trava)
        {
            if (_decisao != alvo)
                return; // já decidido
            _decisao = null;
            _atual = null;
            _abertoEm = null;
            _prazo?.Dispose();
            _prazo = null;
        }
        _log?.Evento($"porta: confirmação {decisao}");
        alvo.TrySetResult(decisao);
        Avisar();
    }

    private void Avisar()
    {
        try
        {
            Mudou?.Invoke();
        }
        catch (Exception e)
        {
            _log?.Evento($"falha no aviso da confirmação ({e.GetType().Name})");
        }
    }
}
