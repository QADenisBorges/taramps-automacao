using BgsControle.Nucleo.Porta;

namespace BgsControle.Telas;

/// <summary>
/// A folha "A IA pede para …" (CAP-12, trava 5): abre sobre qualquer tela quando a IA pede uma
/// operação de risco. Mostra o risco de <c>chaves-de-acesso.md</c> sem mudar o texto, o botão
/// branco que diz o que faz e "Não deixar". Sem toque em 60 s, a IA recebe a recusa; a folha mostra
/// quanto falta ("Sem resposta em 42 s, a IA recebe a recusa."). Fica por cima de tudo (história 11).
/// </summary>
public sealed class ConfirmacaoIaViewModel : TelaBase, IDisposable
{
    public const string TextoNaoDeixar = "Não deixar";

    /// <summary>"Sem resposta em 42 s, a IA recebe a recusa."</summary>
    public static string MsgPrazo(int segundos) => $"Sem resposta em {segundos} s, a IA recebe a recusa.";

    private readonly PedidosConfirmacao _pedidos;
    private PedidoConfirmacao? _atual;
    private ITimer? _contagem;
    private bool _descartado;

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

    /// <summary>"Sem resposta em 42 s, a IA recebe a recusa.", ou null sem folha.</summary>
    public string? TextoPrazo => SegundosRestantes is { } s ? MsgPrazo(s) : null;

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
        Avisar(nameof(TextoConfirmar));
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
