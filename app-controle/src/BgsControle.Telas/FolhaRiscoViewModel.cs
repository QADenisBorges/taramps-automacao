using BgsControle.Nucleo.Porta;

namespace BgsControle.Telas;

/// <summary>
/// A folha de risco do app (CAP-12, trava 5; decisão do Denis, 30/10/2026): abre sobre qualquer tela
/// quando a porta de comandos pede uma operação de risco ou o Continuar. O título diz o que muda
/// ("Aplicar a memória FILME?"), o risco vem de <c>chaves-de-acesso.md</c> sem mudar o texto, e os
/// botões são "Confirmar" e "Cancelar". Sem toque em 60 s, o pedido é recusado; a folha mostra quanto
/// falta ("Sem resposta em 42 s, o pedido é recusado."). Nada nela menciona a IA. Fica por cima de tudo.
/// </summary>
public sealed class FolhaRiscoViewModel : TelaBase, IDisposable
{
    public const string TextoConfirmarBotao = "Confirmar";
    public const string TextoCancelarBotao = "Cancelar";

    /// <summary>"Sem resposta em 42 s, o pedido é recusado."</summary>
    public static string MsgPrazo(int segundos) => $"Sem resposta em {segundos} s, o pedido é recusado.";

    private readonly PedidosConfirmacao _pedidos;
    private PedidoConfirmacao? _atual;
    private ITimer? _contagem;
    private bool _descartado;

    public FolhaRiscoViewModel(PedidosConfirmacao pedidos, IDespachante despachante, TimeProvider relogio)
        : base(despachante, relogio)
    {
        _pedidos = pedidos;
        // Só a folha que o usuário vê: um pedido novo no lugar não é decidido por um toque antigo.
        Confirmar = new Comando(() => Decidir(_pedidos.Confirmar));
        Cancelar = new Comando(() => Decidir(_pedidos.Recusar));
        _pedidos.Mudou += AoMudar;
        Atualizar();
    }

    public bool FolhaAberta => _atual is not null;

    /// <summary>O que muda, como pergunta ("Aplicar a memória FILME?").</summary>
    public string? Titulo => _atual?.Titulo;

    /// <summary>O risco, sem mudar o texto de <c>chaves-de-acesso.md</c>.</summary>
    public string? Risco => _atual?.Risco;

    /// <summary>"Confirmar".</summary>
    public string TextoConfirmar => TextoConfirmarBotao;

    /// <summary>"Cancelar".</summary>
    public string TextoCancelar => TextoCancelarBotao;

    /// <summary>Os segundos que faltam para a recusa sem resposta, ou null sem folha.</summary>
    public int? SegundosRestantes
    {
        get
        {
            if (_atual is null || _pedidos.AbertoEm is not { } desde)
                return null;
            var falta = PedidosConfirmacao.Prazo - (Relogio.GetUtcNow() - desde);
            return Math.Max(0, (int)Math.Ceiling(falta.TotalSeconds));
        }
    }

    /// <summary>"Sem resposta em 42 s, o pedido é recusado.", ou null sem folha.</summary>
    public string? TextoPrazo => SegundosRestantes is { } s ? MsgPrazo(s) : null;

    /// <summary>"Confirmar": a operação segue.</summary>
    public Comando Confirmar { get; }

    /// <summary>"Cancelar" (também o véu e o Voltar): a porta recebe "O usuário recusou na tela.".</summary>
    public Comando Cancelar { get; }

    private void Decidir(Action<PedidoConfirmacao?> decidir)
    {
        if (_atual is { } pedido)
            decidir(pedido);
    }

    private void AoMudar() => NaTela(Atualizar);

    private void Atualizar()
    {
        if (_descartado)
            return;
        var atual = _pedidos.Atual;
        if (ReferenceEquals(atual, _atual))
            return;
        _atual = atual;
        // A contagem anda só com a folha aberta.
        if (_atual is null)
        {
            _contagem?.Dispose();
            _contagem = null;
        }
        else
        {
            _contagem ??= Relogio.CreateTimer(_ => NaTela(AvisarPrazo), null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
        }
        Avisar(nameof(FolhaAberta));
        Avisar(nameof(Titulo));
        Avisar(nameof(Risco));
        AvisarPrazo();
    }

    private void AvisarPrazo()
    {
        if (_descartado)
            return;
        Avisar(nameof(SegundosRestantes));
        Avisar(nameof(TextoPrazo));
    }

    public void Dispose()
    {
        _descartado = true;
        _contagem?.Dispose();
        _contagem = null;
        _pedidos.Mudou -= AoMudar;
    }
}
