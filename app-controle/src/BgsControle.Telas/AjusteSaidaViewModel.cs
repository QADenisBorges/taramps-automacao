using BgsControle.Nucleo;
using BgsControle.Nucleo.Escrita;
using BgsControle.Nucleo.Seguranca;
using BgsControle.Nucleo.Setups;

namespace BgsControle.Telas;

/// <summary>
/// O Ajuste da saída (EXPERIENCE, "Ajuste da saída"; DESIGN, giratório e Avançado), para uma saída do
/// Setup em uso: o ganho no giratório, sempre aberto; à vista o passa-alta, o passa-baixa e o limitador;
/// em Avançado o atraso, a fase, o EQ de saída, o ataque, a liberação, o Auto do limitador e o mute
/// (o equilíbrio do Setup saiu em 29/10/2026: a memória guarda o valor final). Os controles escrevem no canal mestre; num par com link, o
/// aparelho repete no outro canal. Quando as saídas mudam (link, equipamento, Setup), a tela segue a
/// saída que tem o canal pedido, e os controles se refazem se o mestre dela mudou. O Salvar fica fixo
/// no pé.
/// </summary>
public sealed class AjusteSaidaViewModel : TelaBase, IDisposable
{
    private readonly FabricaControles _fabrica;
    private readonly GerenteSetups _setups;
    private readonly Sessao _sessao;
    private readonly IChaves _chaves;
    private readonly EstadoAparelho _estado;
    private readonly LinkCanais _link;
    private readonly int _pedido;
    private bool _avancadoAberto;
    private SaidaTela _saida;
    private Controles _controles;

    private sealed record Controles(
        int Canal,
        ControleParametroViewModel Ganho,
        IReadOnlyList<ItemControleViewModel> Vista,
        IReadOnlyList<ItemControleViewModel> AvancadoInicio,
        IReadOnlyList<ItemControleViewModel> AvancadoFim);

    /// <param name="canal">O canal pedido (0 a 3): a tela é a da saída que tem ele.</param>
    public AjusteSaidaViewModel(
        int canal,
        FabricaControles fabrica,
        GerenteSetups setups,
        Sessao sessao,
        IChaves chaves,
        EstadoAparelho estado,
        LinkCanais link,
        SalvarViewModel salvar,
        INavegacao nav,
        IDespachante despachante,
        TimeProvider relogio)
        : base(despachante, relogio)
    {
        _pedido = canal;
        _fabrica = fabrica;
        _setups = setups;
        _sessao = sessao;
        _chaves = chaves;
        _estado = estado;
        _link = link;
        Salvar = salvar;
        Limite = new LimiteSaidaViewModel(canal, setups, estado, link, nav, despachante, relogio);
        _saida = SaidaAgora();
        _controles = Montar(_saida.Canal);

        AlternarAvancado = new Comando(() =>
        {
            _avancadoAberto = !_avancadoAberto;
            Avisar(nameof(AvancadoAberto));
            Avisar(nameof(IndicadorAvancado));
        });
        Voltar = new Comando(nav.Voltar);

        _setups.Mudou += AoMudar;
        _sessao.Mudou += AoMudar;
        _chaves.Mudou += AoMudarChave;
        _estado.Recarregou += AoMudar;
        _estado.Mudou += AoMudarValor;
        _link.Mudou += AoMudarLink;
    }

    /// <summary>A saída desta tela.</summary>
    public SaidaTela Saida => _saida;

    /// <summary>O canal mestre (0 a 3): a cor da saída e onde os controles escrevem.</summary>
    public int Canal => _saida.Canal;

    /// <summary>"JBL L", "Sub", na cor da saída.</summary>
    public string Titulo => _saida.Nome;

    /// <summary>"CH1 · rota B · FILME", "CH3 + CH4 · rota A+B · FILME".</summary>
    public string Subtitulo
    {
        get
        {
            var texto = $"{_saida.Rotulo} · rota {TextosParametro.Rota(_estado, Canal)}";
            return SomEmUsoTexto.De(_setups, _sessao).Nome is { } nome ? $"{texto} · {nome}" : texto;
        }
    }

    /// <summary>O aviso do bridge (canais de saídas diferentes, sinal num canal só), ou null.</summary>
    public string? AvisoSaida => _saida.Aviso;

    /// <summary>
    /// "Sem calibração: o app não sabe a tensão." (história 13b): a saída tem alto-falante passivo e o módulo dela
    /// não tem o ganho medido. A tela deixa mexer; só a porta recusa.
    /// </summary>
    public string? AvisoCalibracao => _saida.AvisoCalibracao;

    /// <summary>
    /// O limite da saída, perto do limitador (história 13g): o teto, o que manda, o selo, a folha "A conta" e a faixa de
    /// sem calibração com o "Calibrar ›".
    /// </summary>
    public LimiteSaidaViewModel Limite { get; }

    /// <summary>O ganho (−45,0 a +15,0 dB), no giratório: arrastar, − e + grandes, tocar no número para escrever.</summary>
    public ControleParametroViewModel Ganho => _controles.Ganho;

    /// <summary>As duas frases entre os − e + grandes do ganho (DESIGN, botoes-mais-menos).</summary>
    public const string DicaGiratorio = "Gire o botão ou toque em − e +";

    public const string DicaCampo = "Toque no número para escrever";

    /// <summary>Passa-alta, passa-baixa (frequência e tipo) e limitador.</summary>
    public IReadOnlyList<ItemControleViewModel> Vista => _controles.Vista;

    /// <summary>Avançado, primeiro: atraso e fase.</summary>
    public IReadOnlyList<ItemControleViewModel> AvancadoInicio => _controles.AvancadoInicio;

    /// <summary>Avançado, por fim: EQ de saída, ataque, liberação, Auto e mute.</summary>
    public IReadOnlyList<ItemControleViewModel> AvancadoFim => _controles.AvancadoFim;

    public bool AvancadoAberto => _avancadoAberto;

    public string IndicadorAvancado => _avancadoAberto ? "⌃" : "⌄";

    public Comando AlternarAvancado { get; }

    /// <summary>O Salvar fixo no pé.</summary>
    public SalvarViewModel Salvar { get; }

    public string Lembrete => FuncaoViewModel.LembreteSalvar;

    public Comando Voltar { get; }

    /// <summary>O controle pelo código, à vista ou no Avançado (testes).</summary>
    public ControleParametroViewModel? Controle(string codigo) =>
        codigo == "081" ? Ganho : Vista.Concat(AvancadoInicio).Concat(AvancadoFim).Select(i => i.Controle).FirstOrDefault(x => x.Parametro.Codigo == codigo);

    // ------------------------------------------------------------------ montagem

    /// <summary>A saída em uso que tem o canal pedido (sem Setup, a do canal sozinho).</summary>
    private SaidaTela SaidaAgora()
    {
        var saidas = SaidaTela.EmUso(_setups);
        return saidas.FirstOrDefault(s => s.App.Canais.Contains(_pedido))
               ?? new SaidaTela(new SaidaApp(_pedido, [_pedido], Ligacoes.RotuloCanais([_pedido]), null), Ligacoes.RotuloCanais([_pedido]));
    }

    private Controles Montar(int c)
    {
        var grupo = new GrupoControles();
        var ganho = _fabrica.Criar("081", c);
        ganho.Expandir(); // o giratório fica sempre aberto

        ItemControleViewModel Item(string codigo, string? titulo = null, ControleParametroViewModel? dependeDe = null) =>
            new(_fabrica.Criar(codigo, c, grupo, titulo), dependeDe, Despachante, Relogio);

        var tipoPa = Item("042", "Passa-alta · tipo do filtro");
        var tipoPb = Item("043", "Passa-baixa · tipo do filtro");
        return new Controles(
            c,
            ganho,
            [Item("040", dependeDe: tipoPa.Controle), tipoPa, Item("041", dependeDe: tipoPb.Controle), tipoPb, Item("071")],
            [Item("050"), Item("060")],
            [
                Item("090", "EQ de saída · frequência"), Item("091", "EQ de saída · ganho"), Item("092", "EQ de saída · Q"),
                Item("072", "Ataque do limitador"), Item("073", "Liberação do limitador"), Item("070"), Item("100"),
            ]);
    }

    private static void Descartar(Controles c)
    {
        c.Ganho.Dispose();
        foreach (var i in c.Vista.Concat(c.AvancadoInicio).Concat(c.AvancadoFim))
            i.Dispose();
    }

    /// <summary>As saídas mudaram: a tela segue a saída do canal pedido; mudou o mestre, os controles se refazem.</summary>
    private void Refazer()
    {
        var agora = SaidaAgora();
        if (agora.Igual(_saida))
            return;
        _saida = agora;
        if (agora.Canal != _controles.Canal)
        {
            var antigos = _controles;
            _controles = Montar(agora.Canal);
            Descartar(antigos);
            Avisar(nameof(Ganho));
            Avisar(nameof(Vista));
            Avisar(nameof(AvancadoInicio));
            Avisar(nameof(AvancadoFim));
        }
        Avisar(nameof(Saida));
        Avisar(nameof(Canal));
        Avisar(nameof(Titulo));
        Avisar(nameof(AvisoSaida));
        Avisar(nameof(AvisoCalibracao));
        Avisar(nameof(Subtitulo));
    }

    // ------------------------------------------------------------------ eventos

    private void AoMudar() => NaTela(Atualizar);

    private void AoMudarChave(Chave _) => NaTela(Atualizar);

    private void AoMudarValor(Endereco e)
    {
        if (e.Codigo == "010")
            NaTela(() => Avisar(nameof(Subtitulo)));
    }

    private void AoMudarLink(ParCanais _) => NaTela(() =>
    {
        Refazer();
        Avisar(nameof(Subtitulo));
    });

    private void Atualizar()
    {
        Refazer();
        Avisar(nameof(Subtitulo));
    }

    public void Dispose()
    {
        _setups.Mudou -= AoMudar;
        _sessao.Mudou -= AoMudar;
        _chaves.Mudou -= AoMudarChave;
        _estado.Recarregou -= AoMudar;
        _estado.Mudou -= AoMudarValor;
        _link.Mudou -= AoMudarLink;
        Limite.Dispose();
        Descartar(_controles);
    }
}
