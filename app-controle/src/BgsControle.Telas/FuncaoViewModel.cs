using System.ComponentModel;
using BgsControle.Nucleo;
using BgsControle.Nucleo.Escrita;
using BgsControle.Nucleo.Memorias;
using BgsControle.Nucleo.Setups;

namespace BgsControle.Telas;

/// <summary>Os blocos da grade Áudio, na ordem do app da Taramps.</summary>
public enum FuncaoAudio
{
    Roteamento,
    Eq15Bandas,
    EqEntrada,
    Crossover,
    Alinhamento,
    Fase,
    Limitador,
    NivelSaidas,
    EqSaida,
    Gerador,
}

/// <summary>Um controle numa tela de parâmetros, com o título e quando aparece.</summary>
public sealed class ItemControleViewModel : TelaBase, IDisposable
{
    private readonly ControleParametroViewModel? _dependeDe;

    /// <param name="dependeDe">O tipo do filtro: com ele em OFF, a frequência some da tela.</param>
    internal ItemControleViewModel(ControleParametroViewModel controle, ControleParametroViewModel? dependeDe,
        IDespachante despachante, TimeProvider relogio)
        : base(despachante, relogio)
    {
        Controle = controle;
        _dependeDe = dependeDe;
        if (dependeDe is not null)
            dependeDe.PropertyChanged += AoMudarDependencia;
    }

    public ControleParametroViewModel Controle { get; }

    public string Titulo => Controle.Titulo;

    /// <summary>Passa-alta ou passa-baixa em OFF: a frequência some e volta junto com o tipo escolhido.</summary>
    public bool Visivel => _dependeDe?.OpcaoEscolhida is not { Numero: 0 };

    private void AoMudarDependencia(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ControleParametroViewModel.OpcaoEscolhida))
            Avisar(nameof(Visivel));
    }

    public void Dispose()
    {
        if (_dependeDe is not null)
            _dependeDe.PropertyChanged -= AoMudarDependencia;
        Controle.Dispose();
    }
}

/// <summary>Uma aba de saída das telas de função: nome na cor do canal mestre, o resumo pequeno, a escolhida em branco.</summary>
public sealed class AbaSaidaViewModel : TelaBase
{
    private bool _escolhida;
    private string _resumo = "";

    internal AbaSaidaViewModel(SaidaTela saida, int coluna, FuncaoViewModel tela, IDespachante despachante, TimeProvider relogio)
        : base(despachante, relogio)
    {
        Saida = saida;
        Coluna = coluna;
        Escolher = new Comando(() => tela.EscolherSaida(saida.Canal));
    }

    public SaidaTela Saida { get; }

    /// <summary>O canal mestre (0 a 3): a cor e onde os controles da aba escrevem.</summary>
    public int Canal => Saida.Canal;

    /// <summary>A coluna (0 a 3) entre as abas.</summary>
    public int Coluna { get; }

    public string Nome => Saida.Nome;

    /// <summary>"PA 200 · PB OFF", "−18,0 dB"…</summary>
    public string Resumo
    {
        get => _resumo;
        internal set
        {
            if (_resumo == value)
                return;
            _resumo = value;
            Avisar();
        }
    }

    public bool Escolhida
    {
        get => _escolhida;
        internal set
        {
            if (_escolhida == value)
                return;
            _escolhida = value;
            Avisar();
        }
    }

    public Comando Escolher { get; }
}

/// <summary>
/// Uma tela de função da grade Áudio (EXPERIENCE, "O que fica à vista e o que vai para Avançado"): abas
/// das saídas do Setup em uso quando a função é por saída (de 1 a 4, refeitas quando as saídas mudam; um
/// par com link escreve no mestre e o aparelho repete no outro), os controles à vista e os do Avançado
/// (recolhido; lembra se estava aberto), o link 1–2 e 3–4 no Avançado do Crossover e o botão Salvar fixo
/// no pé. Todo controle é uma <see cref="ControleParametroViewModel"/>: o som muda na hora e a tela mostra
/// o valor relido.
/// </summary>
public sealed class FuncaoViewModel : TelaBase, IDisposable
{
    public const string LembreteSalvar = "Mexer muda o som na hora. Para guardar, toque em Salvar; é um passo à parte.";

    private readonly FabricaControles _fabrica;
    private readonly GerenteSetups _setups;
    private readonly GerenteMemorias _memorias;
    private readonly Sessao _sessao;
    private readonly EstadoAparelho _estado;
    private readonly LinkCanais _link;
    private readonly GrupoControles _grupo = new();
    private readonly Dictionary<int, (IReadOnlyList<ItemControleViewModel> Vista, IReadOnlyList<ItemControleViewModel> Avancado)> _porCanal = [];

    private int _saida;
    private bool _avancadoAberto;
    private IReadOnlyList<AbaSaidaViewModel> _abas = [];

    public FuncaoViewModel(
        FuncaoAudio funcao,
        FabricaControles fabrica,
        GerenteSetups setups,
        GerenteMemorias memorias,
        Sessao sessao,
        EstadoAparelho estado,
        LinkCanais link,
        LinkViewModel linkTela,
        SalvarViewModel salvar,
        INavegacao nav,
        IDespachante despachante,
        TimeProvider relogio)
        : base(despachante, relogio)
    {
        Funcao = funcao;
        _fabrica = fabrica;
        _setups = setups;
        _memorias = memorias;
        _sessao = sessao;
        _estado = estado;
        _link = link;
        Link = linkTela;
        Salvar = salvar;
        AlternarAvancado = new Comando(() =>
        {
            _avancadoAberto = !_avancadoAberto;
            Avisar(nameof(AvancadoAberto));
            Avisar(nameof(IndicadorAvancado));
        });
        Voltar = new Comando(nav.Voltar);
        RefazerAbas();

        _estado.Mudou += AoMudarValor;
        _estado.Recarregou += AoMudar;
        _link.Mudou += AoMudarLink;
        _setups.Mudou += AoMudar;
        _sessao.Mudou += AoMudar;
        _memorias.Mudou += AoMudar; // o subtítulo diz o nome lido de uma memória do aparelho
        EscolherSaida(_abas.FirstOrDefault()?.Canal ?? 0);
    }

    public FuncaoAudio Funcao { get; }

    /// <summary>"Crossover", "EQ 15 bandas"…</summary>
    public string Titulo => AudioViewModel.Titulo(Funcao);

    /// <summary>A memória em uso ("FILME", "Som do aparelho", o nome lido de uma memória do aparelho), ou "Sem conexão".</summary>
    public string Subtitulo => SomEmUsoTexto.De(_setups, _memorias, _sessao).Nome ?? ControleParametroViewModel.MsgSemConexao;

    /// <summary>A função é por saída: uma aba por saída do Setup em uso.</summary>
    public bool TemAbas => Funcao is not (FuncaoAudio.Eq15Bandas or FuncaoAudio.EqEntrada or FuncaoAudio.Gerador);

    public IReadOnlyList<AbaSaidaViewModel> Abas => _abas;

    /// <summary>O canal mestre da saída que se mexe agora.</summary>
    public int SaidaEscolhida => _saida;

    /// <summary>A aba da saída que se mexe agora, ou null (função sem abas).</summary>
    public AbaSaidaViewModel? AbaEscolhida => _abas.FirstOrDefault(a => a.Canal == _saida);

    /// <summary>A aba da saída que tem o canal (0 a 3), ou null.</summary>
    public AbaSaidaViewModel? Aba(int canal) => _abas.FirstOrDefault(a => a.Saida.App.Canais.Contains(canal));

    /// <summary>Os controles à vista (da aba escolhida).</summary>
    public IReadOnlyList<ItemControleViewModel> Vista => Itens().Vista;

    /// <summary>Os controles do Avançado (da aba escolhida).</summary>
    public IReadOnlyList<ItemControleViewModel> Avancado => Itens().Avancado;

    /// <summary>Há Avançado: controles, ou o link no Crossover.</summary>
    public bool TemAvancado => Avancado.Count > 0 || MostrarLink;

    public bool AvancadoAberto => _avancadoAberto;

    public string IndicadorAvancado => _avancadoAberto ? "⌃" : "⌄";

    public Comando AlternarAvancado { get; }

    /// <summary>A linha só de leitura do link no Avançado do Crossover, quando algum par está ligado.</summary>
    public bool MostrarLink => Funcao == FuncaoAudio.Crossover && LinkCanais.Pares.Any(_link.Ligado);

    public LinkViewModel Link { get; }

    /// <summary>O Salvar fixo no pé (o mesmo de todas as telas).</summary>
    public SalvarViewModel Salvar { get; }

    public string Lembrete => LembreteSalvar;

    /// <summary>
    /// O que a aba mexe quando não é só um canal: o aviso do bridge sem o link, ou, num par com link,
    /// "Link 3–4 ligado: mexer em Sub muda o CH3 e o CH4."; senão null.
    /// </summary>
    public string? AvisoLink
    {
        get
        {
            if (AbaEscolhida?.Saida is not { } s)
                return null;
            if (s.Aviso is { } aviso)
                return aviso;
            if (!s.Par)
                return null;
            var (a, b) = (s.App.Canais[0] + 1, s.App.Canais[1] + 1);
            return $"Link {a}–{b} ligado: mexer em {s.Nome} muda o CH{a} e o CH{b}.";
        }
    }

    public Comando Voltar { get; }

    /// <summary>Uma aba: escolhe a saída que se mexe (pelo canal; um canal de um par escolhe o par).</summary>
    public void EscolherSaida(int canal)
    {
        _saida = Aba(canal)?.Canal ?? canal;
        _grupo.Aberto?.Recolher();
        foreach (var a in _abas)
            a.Escolhida = a.Canal == _saida;
        AtualizarResumos();
        Avisar(nameof(SaidaEscolhida));
        Avisar(nameof(AbaEscolhida));
        Avisar(nameof(Vista));
        Avisar(nameof(Avancado));
        Avisar(nameof(TemAvancado));
        Avisar(nameof(AvisoLink));
    }

    /// <summary>O controle à vista ou do Avançado pelo código, na aba escolhida (testes e tela).</summary>
    public ControleParametroViewModel? Controle(string codigo, int? indice = null) =>
        Vista.Concat(Avancado).Select(i => i.Controle)
            .FirstOrDefault(c => c.Parametro.Codigo == codigo && (indice is null || c.Indice == indice));

    // ------------------------------------------------------------------ montagem

    private (IReadOnlyList<ItemControleViewModel> Vista, IReadOnlyList<ItemControleViewModel> Avancado) Itens()
    {
        var chave = TemAbas ? _saida : -1;
        if (!_porCanal.TryGetValue(chave, out var itens))
        {
            itens = Montar(TemAbas ? _saida : null);
            _porCanal[chave] = itens;
        }
        return itens;
    }

    private ItemControleViewModel Item(string codigo, int? indice, string? titulo = null, ControleParametroViewModel? dependeDe = null) =>
        new(_fabrica.Criar(codigo, indice, _grupo, titulo), dependeDe, Despachante, Relogio);

    private (IReadOnlyList<ItemControleViewModel>, IReadOnlyList<ItemControleViewModel>) Montar(int? c)
    {
        switch (Funcao)
        {
            case FuncaoAudio.Roteamento:
                return ([Item("010", c)], []);
            case FuncaoAudio.Eq15Bandas:
                return (Enumerable.Range(0, Parametro.Bandas).Select(b => Item("020", b)).ToArray(), [Item("220", null)]);
            case FuncaoAudio.EqEntrada:
                return ([Item("031", null), Item("030", null), Item("032", null)], []);
            case FuncaoAudio.Crossover:
            {
                var tipoPa = Item("042", c, "Passa-alta · tipo do filtro");
                var tipoPb = Item("043", c, "Passa-baixa · tipo do filtro");
                return ([Item("040", c, dependeDe: tipoPa.Controle), tipoPa, Item("041", c, dependeDe: tipoPb.Controle), tipoPb], []);
            }
            case FuncaoAudio.Alinhamento:
                return ([Item("050", c)], []);
            case FuncaoAudio.Fase:
                return ([Item("060", c)], []);
            case FuncaoAudio.Limitador:
                return ([Item("071", c)], [Item("072", c), Item("073", c), Item("070", c)]);
            case FuncaoAudio.NivelSaidas:
                return ([Item("081", c)], [Item("100", c)]);
            case FuncaoAudio.EqSaida:
                return ([Item("090", c), Item("091", c), Item("092", c)], []);
            default:
                return ([], []);
        }
    }

    /// <summary>
    /// As saídas mudaram (link, equipamento, Setup): as abas se refazem, e a escolhida continua a saída
    /// que tem o canal escolhido; sem ela, a primeira.
    /// </summary>
    private void RefazerAbas()
    {
        if (!TemAbas)
            return;
        var agora = SaidaTela.EmUso(_setups);
        if (SaidaTela.Iguais(agora, _abas.Select(a => a.Saida).ToArray()))
            return;
        _abas = agora.Select((s, i) => new AbaSaidaViewModel(s, i, this, Despachante, Relogio)).ToArray();
        Avisar(nameof(Abas));
        EscolherSaida(Aba(_saida) is null ? _abas.FirstOrDefault()?.Canal ?? 0 : _saida);
    }

    /// <summary>O resumo pequeno de cada aba, ao vivo.</summary>
    private void AtualizarResumos()
    {
        foreach (var a in _abas)
            a.Resumo = ResumoAba(a.Canal);
    }

    private string ResumoAba(int c)
    {
        string Curto(string codigo) => TextosParametro.Curto(_estado, codigo, c);
        string Filtro(string f, string t) => TextosParametro.Filtro(_estado, f, t, c);
        return Funcao switch
        {
            FuncaoAudio.Roteamento => $"Rota {Curto("010")}",
            FuncaoAudio.Crossover => $"PA {Filtro("040", "042")} · PB {Filtro("041", "043")}",
            FuncaoAudio.Alinhamento => $"{Curto("050")} cm",
            FuncaoAudio.Fase => Curto("060"),
            FuncaoAudio.Limitador => Curto("071") is var l && l is "OFF" or "—" ? l : $"{l} dB",
            FuncaoAudio.NivelSaidas => $"{Curto("081")} dB",
            FuncaoAudio.EqSaida => $"{Curto("090")} Hz · {Curto("091")} dB",
            _ => "",
        };
    }

    // ------------------------------------------------------------------ eventos

    private void AoMudar() => NaTela(() =>
    {
        RefazerAbas();
        AtualizarResumos();
        Avisar(nameof(Subtitulo));
        Avisar(nameof(AvisoLink));
    });

    private void AoMudarValor(Endereco _) => NaTela(AtualizarResumos);

    private void AoMudarLink(ParCanais _) => NaTela(() =>
    {
        RefazerAbas();
        Avisar(nameof(AvisoLink));
        Avisar(nameof(MostrarLink));
        Avisar(nameof(TemAvancado));
    });

    public void Dispose()
    {
        _estado.Mudou -= AoMudarValor;
        _estado.Recarregou -= AoMudar;
        _link.Mudou -= AoMudarLink;
        _setups.Mudou -= AoMudar;
        _sessao.Mudou -= AoMudar;
        _memorias.Mudou -= AoMudar;
        foreach (var (vista, avancado) in _porCanal.Values)
        {
            foreach (var i in vista.Concat(avancado))
                i.Dispose();
        }
    }
}
