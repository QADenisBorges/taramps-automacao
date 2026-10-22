using BgsControle.Nucleo.Porta;

namespace BgsControle.Telas;

/// <summary>
/// A folha "A IA pede para …" (CAP-12, trava 5): abre sobre qualquer tela quando a IA pede uma
/// operação de risco. Mostra o risco de <c>chaves-de-acesso.md</c> sem mudar o texto, o botão
/// branco que diz o que faz e "Não deixar". Sem toque em 60 s, a IA recebe a recusa.
/// </summary>
public sealed class ConfirmacaoIaViewModel : TelaBase, IDisposable
{
    public const string TextoNaoDeixar = "Não deixar";

    private readonly PedidosConfirmacao _pedidos;
    private PedidoConfirmacao? _atual;

    public ConfirmacaoIaViewModel(PedidosConfirmacao pedidos, IDespachante despachante, TimeProvider relogio)
        : base(despachante, relogio)
    {
        _pedidos = pedidos;
        // Só a folha que o usuário vê: um pedido novo no lugar não é decidido por um toque antigo.
        Confirmar = new Comando(() => Decidir(_pedidos.Confirmar));
        Recusar = new Comando(() => Decidir(_pedidos.Recusar));
        _pedidos.Mudou += AoMudar;
        Atualizar();
    }

    public bool FolhaAberta => _atual is not null;

    /// <summary>"A IA pede para carregar a FILME".</summary>
    public string? Titulo => _atual?.Titulo;

    /// <summary>O risco, sem mudar o texto de <c>chaves-de-acesso.md</c>.</summary>
    public string? Risco => _atual?.Risco;

    /// <summary>O botão branco, que diz o que faz ("Carregar a FILME").</summary>
    public string? TextoConfirmar => _atual?.TextoBotao;

    /// <summary>"Não deixar".</summary>
    public string TextoRecusar => TextoNaoDeixar;

    /// <summary>O botão branco: a operação segue.</summary>
    public Comando Confirmar { get; }

    /// <summary>"Não deixar": a IA recebe "O usuário recusou na tela.".</summary>
    public Comando Recusar { get; }

    private void Decidir(Action<PedidoConfirmacao?> decidir)
    {
        if (_atual is { } pedido)
            decidir(pedido);
    }

    private void AoMudar() => NaTela(Atualizar);

    private void Atualizar()
    {
        var atual = _pedidos.Atual;
        if (ReferenceEquals(atual, _atual))
            return;
        _atual = atual;
        Avisar(nameof(FolhaAberta));
        Avisar(nameof(Titulo));
        Avisar(nameof(Risco));
        Avisar(nameof(TextoConfirmar));
    }

    public void Dispose() => _pedidos.Mudou -= AoMudar;
}
