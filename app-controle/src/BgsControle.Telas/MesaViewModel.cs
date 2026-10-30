using BgsControle.Nucleo;
using BgsControle.Nucleo.Escrita;
using BgsControle.Nucleo.Setups;

namespace BgsControle.Telas;

/// <summary>
/// Um canal da Mesa (uma saída do Setup): nome na cor do canal mestre, "CH1 · B", o fader do ganho com o
/// campo (o motivo da trava e o aviso embaixo), o resumo, o aviso do bridge e "Ajustar".
/// </summary>
public sealed class CanalMesaViewModel : TelaBase
{
    private readonly EstadoAparelho _estado;
    private bool _clip;

    internal CanalMesaViewModel(SaidaTela saida, int coluna, ControleParametroViewModel ganho, EstadoAparelho estado, INavegacao nav,
        IDespachante despachante, TimeProvider relogio)
        : base(despachante, relogio)
    {
        Saida = saida;
        Coluna = coluna;
        Ganho = ganho;
        _estado = estado;
        Ajustar = new Comando(() => nav.AbrirAjuste(saida.Canal));
    }

    public SaidaTela Saida { get; }

    /// <summary>O canal mestre (0 a 3): a cor e onde o fader escreve.</summary>
    public int Canal => Saida.Canal;

    /// <summary>A coluna na Mesa (0 a 3).</summary>
    public int Coluna { get; }

    public string Nome => Saida.Nome.ToUpperInvariant();

    /// <summary>"CH1 · B", "CH3 + CH4 · A+B".</summary>
    public string Subtitulo => $"{Saida.Rotulo} · {TextosParametro.Rota(_estado, Canal)}";

    /// <summary>O aviso do bridge sem o link, ou null.</summary>
    public string? AvisoSaida => Saida.Aviso;

    /// <summary>O ganho da saída (−45,0 a +15,0 dB): fader, campo e − e +.</summary>
    public ControleParametroViewModel Ganho { get; }

    /// <summary>"PA 80 · PB OFF · Lim −18,0" ("—" com o tipo do filtro ainda não lido).</summary>
    public string Resumo =>
        $"PA {TextosParametro.Filtro(_estado, "040", "042", Canal)} · PB {TextosParametro.Filtro(_estado, "041", "043", Canal)} · Lim {TextosParametro.Curto(_estado, "071", Canal)}";

    public Comando Ajustar { get; }

    /// <summary>O ponto de clip no topo da coluna: aceso quando a saída clipa (o mesmo da faixa de monitoração).</summary>
    public bool Clip
    {
        get => _clip;
        internal set
        {
            if (_clip == value)
                return;
            _clip = value;
            Avisar();
        }
    }

    internal void Atualizar()
    {
        Avisar(nameof(Subtitulo));
        Avisar(nameof(Resumo));
    }
}

/// <summary>
/// A Mesa (EXPERIENCE, Information Architecture; DESIGN, Layout › Mesa): no topo, a memória em uso e os
/// mesmos 3 botões de memória + "…" do Início; os faders de ganho das saídas do Setup em uso (de 1 a 4,
/// refeitos quando as saídas mudam) e o MASTER (caráter,
/// vai para a memória). Arrastar muda o som na hora; tocar no número vira campo, que vale no ✓. Entre os
/// canais e o MASTER, a faixa de monitoração (história 11): o voltímetro e o clip por saída do Setup.
/// </summary>
public sealed class MesaViewModel : TelaBase, IDisposable
{
    public const string RotuloMaster = "MASTER · todas as saídas";

    private readonly GerenteSetups _setups;
    private readonly Sessao _sessao;
    private readonly EstadoAparelho _estado;
    private readonly LinkCanais _link;
    private readonly FabricaControles _fabrica;
    private readonly INavegacao _nav;
    private readonly MonitoracaoViewModel? _monitoracao;
    private IReadOnlyList<CanalMesaViewModel> _canais = [];

    public MesaViewModel(
        GerenteSetups setups,
        Sessao sessao,
        EstadoAparelho estado,
        LinkCanais link,
        FabricaControles fabrica,
        BotoesMemoriaViewModel botoes,
        INavegacao nav,
        IDespachante despachante,
        TimeProvider relogio,
        MonitoracaoViewModel? monitoracao = null)
        : base(despachante, relogio)
    {
        _monitoracao = monitoracao;
        _setups = setups;
        _sessao = sessao;
        _estado = estado;
        _link = link;
        _fabrica = fabrica;
        _nav = nav;
        Memorias = botoes;
        RefazerCanais();
        Master = fabrica.Criar("080", null);
        Master.Expandir();

        _setups.Mudou += AoMudar;
        _sessao.Mudou += AoMudar;
        _estado.Recarregou += AoMudar;
        _estado.Mudou += AoMudarValor;
        _link.Mudou += AoMudarLink;
        if (_monitoracao is not null)
            _monitoracao.Atualizou += AtualizarClip;
        AtualizarClip();
    }

    /// <summary>A faixa de monitoração (voltímetro e clip por saída), ou null.</summary>
    public MonitoracaoViewModel? Monitoracao => _monitoracao;

    /// <summary>"MEMÓRIA EM USO".</summary>
    public string RotuloMemoria => InicioViewModel.RotuloMemoria;

    /// <summary>
    /// O nome da memória em uso ("FILME"), ou o que o app sabe ("Som do aparelho"); "—" sem conexão.
    /// </summary>
    public string NomeMemoria
    {
        get
        {
            var som = SomEmUsoTexto.De(_setups, _sessao);
            return som.Nome ?? "—";
        }
    }

    public BotoesMemoriaViewModel Memorias { get; }

    /// <summary>Um canal da Mesa por saída do Setup em uso (1 a 4), na ordem dos canais; refeitos quando as saídas mudam (no Casa 2.1: JBL L, JBL R e Sub).</summary>
    public IReadOnlyList<CanalMesaViewModel> Canais => _canais;

    /// <summary>Quantas colunas a Mesa tem (1 a 4).</summary>
    public int Colunas => _canais.Count;

    /// <summary>O canal da Mesa da saída que tem o canal do processador (0 a 3), ou null.</summary>
    public CanalMesaViewModel? Canal(int canal) => _canais.FirstOrDefault(c => c.Saida.App.Canais.Contains(canal));

    /// <summary>O MASTER, de −80,0 a 0,0 dB.</summary>
    public ControleParametroViewModel Master { get; }


    private void AoMudar() => NaTela(Atualizar);

    private void AoMudarLink(ParCanais _) => NaTela(Atualizar);

    /// <summary>As saídas mudaram (link, equipamento, Setup): os canais se refazem, cada um com o fader do mestre.</summary>
    private void RefazerCanais()
    {
        var agora = SaidaTela.EmUso(_setups);
        if (SaidaTela.Iguais(agora, _canais.Select(c => c.Saida).ToArray()))
            return;
        var antigos = _canais;
        // Na Mesa os faders ficam sempre abertos: tocar no número vira campo direto.
        _canais = agora.Select((s, i) =>
        {
            var ganho = _fabrica.Criar("081", s.Canal);
            ganho.Expandir();
            return new CanalMesaViewModel(s, i, ganho, _estado, _nav, Despachante, Relogio);
        }).ToArray();
        foreach (var c in antigos)
            c.Ganho.Dispose();
        Avisar(nameof(Canais));
        Avisar(nameof(Colunas));
    }

    private void AoMudarValor(Endereco e)
    {
        if (e.Codigo is "010" or "040" or "041" or "042" or "043" or "071")
            NaTela(Atualizar);
    }

    private void Atualizar()
    {
        RefazerCanais();
        foreach (var c in Canais)
            c.Atualizar();
        AtualizarClip();
        Avisar(nameof(NomeMemoria));
    }

    private void AtualizarClip()
    {
        if (_monitoracao is null)
            return;
        foreach (var c in _canais)
            c.Clip = _monitoracao.Clipou(c.Saida.App);
    }

    public void Dispose()
    {
        _setups.Mudou -= AoMudar;
        _sessao.Mudou -= AoMudar;
        _estado.Recarregou -= AoMudar;
        _estado.Mudou -= AoMudarValor;
        _link.Mudou -= AoMudarLink;
        if (_monitoracao is not null)
            _monitoracao.Atualizou -= AtualizarClip;
        foreach (var c in Canais)
            c.Ganho.Dispose();
        Master.Dispose();
    }
}
