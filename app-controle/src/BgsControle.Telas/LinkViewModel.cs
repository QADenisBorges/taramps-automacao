using BgsControle.Nucleo.Escrita;

namespace BgsControle.Telas;

/// <summary>
/// Uma linha só de leitura de um par com o link ligado: "CH3 e CH4 ligados: alimentam o mesmo bridge",
/// e o aviso de quando a última leitura achou as duas saídas diferentes.
/// </summary>
public sealed record LinhaLinkViewModel(ParCanais Par, string Frase, string? AvisoDiferenca);

/// <summary>
/// O link 1–2 e 3–4 (só do app, MAPA §6), só de leitura: ele sai sozinho das ligações do Setup em uso
/// (decisão D1 do Denis, 29/10/2026), e o usuário não mexe nele. A tela mostra uma linha por par
/// ligado ("CH3 e CH4 ligados: alimentam o mesmo bridge") e, quando a leitura acha as duas saídas
/// diferentes, o aviso. Sem par ligado, nada.
/// </summary>
public sealed class LinkViewModel : TelaBase, IDisposable
{
    private readonly LinkCanais _link;
    private IReadOnlyList<LinhaLinkViewModel> _linhas = [];

    public LinkViewModel(LinkCanais link, IDespachante despachante, TimeProvider relogio)
        : base(despachante, relogio)
    {
        _link = link;
        _link.Mudou += AoMudarLink;
        Atualizar();
        _ = link.Carregada.ContinueWith(_ => NaTela(Atualizar), TaskScheduler.Default);
    }

    /// <summary>"CH3 e CH4 ligados: alimentam o mesmo bridge".</summary>
    public static string Frase(ParCanais par)
    {
        var (mestre, outro) = LinkCanais.Canais(par);
        return $"CH{mestre + 1} e CH{outro + 1} ligados: alimentam o mesmo bridge";
    }

    /// <summary>Uma por par ligado, na ordem dos canais.</summary>
    public IReadOnlyList<LinhaLinkViewModel> Linhas => _linhas;

    /// <summary>Algum par está ligado.</summary>
    public bool TemLink => _linhas.Count > 0;

    private void AoMudarLink(ParCanais _) => NaTela(Atualizar);

    private void Atualizar()
    {
        _linhas = LinkCanais.Pares
            .Where(_link.Ligado)
            .Select(p => new LinhaLinkViewModel(p, Frase(p), _link.AvisoDiferenca(p)))
            .ToArray();
        Avisar(nameof(Linhas));
        Avisar(nameof(TemLink));
    }

    public void Dispose() => _link.Mudou -= AoMudarLink;
}
