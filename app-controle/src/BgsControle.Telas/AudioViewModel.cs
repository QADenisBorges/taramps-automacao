using BgsControle.Nucleo;
using BgsControle.Nucleo.Escrita;
using BgsControle.Nucleo.Gerador;
using BgsControle.Nucleo.Memorias;
using BgsControle.Nucleo.Seguranca;
using BgsControle.Nucleo.Setups;

namespace BgsControle.Telas;

/// <summary>Um bloco da grade Áudio: nome, o resumo do valor atual (números na cor de cada saída) e ›.</summary>
public sealed class BlocoFuncaoViewModel : TelaBase
{
    private IReadOnlyList<TrechoResumo> _resumo = [];
    private string? _trava;

    internal BlocoFuncaoViewModel(FuncaoAudio funcao, string? trava, INavegacao nav, IDespachante despachante, TimeProvider relogio)
        : base(despachante, relogio)
    {
        Funcao = funcao;
        _trava = trava;
        Abrir = new Comando(() =>
        {
            if (funcao == FuncaoAudio.Gerador)
            {
                // Travado pela chave: tocar abre Chaves de acesso (EXPERIENCE, Bloco de função).
                if (Trava is null)
                    nav.AbrirGerador();
                else
                    nav.AbrirChaves();
            }
            else if (Trava is null)
            {
                nav.AbrirFuncao(funcao);
            }
        });
    }

    public FuncaoAudio Funcao { get; }

    /// <summary>A linha e a coluna na grade 2 × 5.</summary>
    public int Linha => (int)Funcao / 2;

    public int Coluna => (int)Funcao % 2;

    public string Titulo => AudioViewModel.Titulo(Funcao);

    /// <summary>O resumo em pedaços: os números de cada saída na cor dela, o resto em apoio.</summary>
    public IReadOnlyList<TrechoResumo> Resumo
    {
        get => _resumo;
        internal set
        {
            if (_resumo.SequenceEqual(value))
                return;
            _resumo = value;
            Avisar();
            Avisar(nameof(TextoResumo));
        }
    }

    /// <summary>O resumo em uma linha (testes e leitor de tela).</summary>
    public string TextoResumo => string.Concat(_resumo.Select(t => t.Texto));

    /// <summary>Travado: "Chave desligada" (o Gerador com a chave desligada), ou null.</summary>
    public string? Trava
    {
        get => _trava;
        internal set
        {
            if (_trava == value)
                return;
            _trava = value;
            Avisar();
            Avisar(nameof(Travado));
        }
    }

    public bool Travado => Trava is not null;

    /// <summary>Abre a tela da função. O Gerador travado pela chave abre Chaves de acesso; os outros travados, nada.</summary>
    public Comando Abrir { get; }
}

/// <summary>
/// A grade Áudio (2 × 5, na ordem do app da Taramps): Roteamento, EQ 15 bandas, EQ de entrada,
/// Crossover, Alinhamento, Fase, Limitador, Nível das saídas, EQ de saída e Gerador. Cada bloco mostra
/// o resumo do valor lido e abre a tela da função. O Gerador abre a tela Gerador (história 11); com a
/// chave "Gerador" desligada, fica travado com "Chave desligada" e o toque abre Chaves de acesso.
/// </summary>
public sealed class AudioViewModel : TelaBase, IDisposable
{
    /// <summary>O bloco Gerador com a chave desligada.</summary>
    public const string TravaGerador = ControleParametroViewModel.MsgChaveDesligada;

    private readonly FabricaControles _fabrica;
    private readonly GerenteSetups _setups;
    private readonly GerenteMemorias _memorias;
    private readonly Sessao _sessao;
    private readonly EstadoAparelho _estado;
    private readonly LinkCanais _link;
    private readonly LinkViewModel _linkTela;
    private readonly SalvarViewModel _salvar;
    private readonly INavegacao _nav;
    private readonly IChaves? _chaves;
    private readonly GeradorSinal? _gerador;
    private readonly Dictionary<FuncaoAudio, FuncaoViewModel> _telas = [];

    public AudioViewModel(
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
        TimeProvider relogio,
        IChaves? chaves = null,
        GeradorSinal? gerador = null)
        : base(despachante, relogio)
    {
        _chaves = chaves;
        _gerador = gerador;
        _fabrica = fabrica;
        _setups = setups;
        _memorias = memorias;
        _sessao = sessao;
        _estado = estado;
        _link = link;
        _linkTela = linkTela;
        _salvar = salvar;
        _nav = nav;
        Blocos = Enum.GetValues<FuncaoAudio>()
            .Select(f => new BlocoFuncaoViewModel(f, f == FuncaoAudio.Gerador ? TravaDoGerador() : null, nav, despachante, relogio))
            .ToArray();
        if (_chaves is not null)
        {
            _chaves.Mudou += AoMudarChave;
            _ = _chaves.Carregada.ContinueWith(_ => NaTela(Atualizar), TaskScheduler.Default);
        }
        if (_gerador is not null)
            _gerador.Mudou += AoMudar;

        _estado.Mudou += AoMudarValor;
        _estado.Recarregou += AoMudar;
        _sessao.Mudou += AoMudar;
        _setups.Mudou += AoMudar;
        _link.Mudou += AoMudarLink;
        Atualizar();
    }

    /// <summary>Os 10 blocos, na ordem do app da Taramps.</summary>
    public IReadOnlyList<BlocoFuncaoViewModel> Blocos { get; }

    public BlocoFuncaoViewModel Bloco(FuncaoAudio funcao) => Blocos[(int)funcao];

    public static string Titulo(FuncaoAudio f) => f switch
    {
        FuncaoAudio.Roteamento => "Roteamento",
        FuncaoAudio.Eq15Bandas => "EQ 15 bandas",
        FuncaoAudio.EqEntrada => "EQ de entrada",
        FuncaoAudio.Crossover => "Crossover",
        FuncaoAudio.Alinhamento => "Alinhamento",
        FuncaoAudio.Fase => "Fase",
        FuncaoAudio.Limitador => "Limitador",
        FuncaoAudio.NivelSaidas => "Nível das saídas",
        FuncaoAudio.EqSaida => "EQ de saída",
        _ => "Gerador",
    };

    /// <summary>A tela de uma função (criada uma vez e guardada: os controles assinam o núcleo).</summary>
    public FuncaoViewModel Tela(FuncaoAudio funcao)
    {
        if (!_telas.TryGetValue(funcao, out var tela))
        {
            tela = new FuncaoViewModel(funcao, _fabrica, _setups, _memorias, _sessao, _estado, _link, _linkTela, _salvar, _nav, Despachante, Relogio);
            _telas[funcao] = tela;
        }
        return tela;
    }

    // ------------------------------------------------------------------ resumos

    /// <summary>As saídas do Setup em uso (o mestre de cada uma), na ordem dos canais.</summary>
    private IReadOnlyList<int> Mestres => _setups.SaidasEmUso.Select(s => s.Mestre).ToArray();

    private IReadOnlyList<TrechoResumo> PorSaida(string codigo, string? unidade)
    {
        var trechos = new List<TrechoResumo>();
        foreach (var c in Mestres)
        {
            if (trechos.Count > 0)
                trechos.Add(new(" · "));
            trechos.Add(new(TextosParametro.Curto(_estado, codigo, c), c));
        }
        if (unidade is not null)
            trechos.Add(new($" {unidade}"));
        return trechos;
    }

    /// <summary>"PA 80" · "PB 120" · "PA 80 PB 3k" (os dois ligados) · "OFF" · "—" (tipo ainda não lido).</summary>
    private IReadOnlyList<TrechoResumo> Crossover()
    {
        var trechos = new List<TrechoResumo>();
        foreach (var c in Mestres)
        {
            if (trechos.Count > 0)
                trechos.Add(new(" · "));
            trechos.Add(new(ResumoCrossover(_estado, c), c));
        }
        return trechos;
    }

    /// <summary>O resumo do corte de um canal: o passa-alta e o passa-baixa ligados, "OFF" com os dois desligados, "—" sem o tipo lido.</summary>
    internal static string ResumoCrossover(EstadoAparelho estado, int canal)
    {
        if (estado.Valor("042", canal) is not { } pa || estado.Valor("043", canal) is not { } pb)
            return "—";
        var partes = new List<string>();
        if (pa.Numero != 0)
            partes.Add($"PA {TextosParametro.Curto(estado, "040", canal)}");
        if (pb.Numero != 0)
            partes.Add($"PB {TextosParametro.Curto(estado, "041", canal)}");
        return partes.Count == 0 ? "OFF" : string.Join(" ", partes);
    }

    private IReadOnlyList<TrechoResumo> Resumo(FuncaoAudio f) => f switch
    {
        FuncaoAudio.Roteamento => PorSaida("010", null),
        FuncaoAudio.Eq15Bandas => [new(_setups.PresetEmUso is { } p ? $"Preset {p.Nome}" : _estado.Pronto ? "Sem preset" : "—")],
        FuncaoAudio.EqEntrada => [new($"{TextosParametro.Curto(_estado, "031", null)} Hz · {TextosParametro.Curto(_estado, "030", null)} dB")],
        FuncaoAudio.Crossover => Crossover(),
        FuncaoAudio.Alinhamento => PorSaida("050", "cm"),
        FuncaoAudio.Fase => PorSaida("060", null),
        FuncaoAudio.Limitador => PorSaida("071", "dB"),
        FuncaoAudio.NivelSaidas => PorSaida("081", "dB"),
        FuncaoAudio.EqSaida => PorSaida("091", "dB"),
        _ => ResumoGerador(),
    };

    /// <summary>"Chave desligada" sem a chave "Gerador" (sem as chaves, também travado), ou null.</summary>
    private string? TravaDoGerador() => _chaves?.Ligada(Chave.Gerador) == true ? null : TravaGerador;

    /// <summary>"Desligado", "Tocando · 23 s", "Ligando", "Desligando"; com a chave desligada, "Chave desligada".</summary>
    private IReadOnlyList<TrechoResumo> ResumoGerador()
    {
        if (TravaDoGerador() is { } trava)
            return [new(trava)];
        return _gerador?.Estado switch
        {
            EstadoGerador.Tocando when _gerador.Restante is { } falta => [new($"Tocando · {(int)Math.Ceiling(falta.TotalSeconds)} s")],
            EstadoGerador.Tocando => [new("Tocando")],
            EstadoGerador.Ligando => [new("Ligando")],
            EstadoGerador.Desligando => [new("Desligando")],
            _ => [new("Desligado")],
        };
    }

    private void AoMudarChave(Chave chave)
    {
        if (chave == Chave.Gerador)
            NaTela(Atualizar);
    }

    private void AoMudar() => NaTela(Atualizar);

    private void AoMudarValor(Endereco _) => NaTela(Atualizar);

    private void AoMudarLink(ParCanais _) => NaTela(Atualizar);

    private void Atualizar()
    {
        Bloco(FuncaoAudio.Gerador).Trava = TravaDoGerador();
        foreach (var b in Blocos)
            b.Resumo = Resumo(b.Funcao);
    }

    public void Dispose()
    {
        _estado.Mudou -= AoMudarValor;
        _estado.Recarregou -= AoMudar;
        _sessao.Mudou -= AoMudar;
        _setups.Mudou -= AoMudar;
        _link.Mudou -= AoMudarLink;
        if (_chaves is not null)
            _chaves.Mudou -= AoMudarChave;
        if (_gerador is not null)
            _gerador.Mudou -= AoMudar;
        foreach (var t in _telas.Values)
            t.Dispose();
    }
}
