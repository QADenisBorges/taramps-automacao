using BgsControle.Nucleo.Escrita;
using BgsControle.Nucleo.Registro;
using BgsControle.Nucleo.Rendimento;
using BgsControle.Nucleo.Setups;

namespace BgsControle.Telas;

/// <summary>
/// Uma linha da folha "Nível da memória": o nível (null no Extra), o texto ("Alto · até 16 V nas JBL L e JBL R ·
/// qualquer fonte"), se dá para escolher e, apagada, o motivo; o aviso de calor no Máximo e o aviso de falta de
/// ganho, quando a conta passa de 0 dB.
/// </summary>
/// <param name="Nivel">O nível, ou null na linha do Extra.</param>
/// <param name="Nome">"Baixo", "Médio", "Alto", "Máximo" ou "Extra".</param>
/// <param name="Texto">"Alto · até 16 V nas JBL L e JBL R · qualquer fonte".</param>
/// <param name="Pode">A linha está acesa: o toque escolhe o nível.</param>
/// <param name="Motivo">Por que a linha está apagada ("Calibre a saída JBL L."), ou null.</param>
/// <param name="AvisoCalor">O aviso do Máximo (passa da potência da ficha), ou null.</param>
/// <param name="AvisoGanho">"O nível não chega ao teto: falta ganho na memória. Saída: JBL L.", ou null.</param>
/// <param name="Escolhida">É o nível da memória em uso.</param>
/// <param name="Selo">O selo "estimado" da linha do Extra com algum dado térmico só digitado (história 13f), ou null.</param>
public sealed record LinhaNivel(NivelRendimento? Nivel, string Nome, string Texto, bool Pode, string? Motivo, string? AvisoCalor,
    string? AvisoGanho, bool Escolhida, string? Selo = null);

/// <summary>
/// Uma linha da folha na tela (história 13g): a <see cref="LinhaNivel"/> e o toque, que escolhe o nível (no Extra, o
/// <see cref="NivelViewModel.EscolherExtra"/>; numa linha apagada, mostra o motivo).
/// </summary>
public sealed class ItemNivel(LinhaNivel linha, Comando tocar)
{
    public LinhaNivel Linha { get; } = linha;

    /// <summary>"Alto · até 16 V nas JBL L e JBL R · qualquer fonte".</summary>
    public string Texto => Linha.Texto;

    /// <summary>A linha está apagada (opacidade 0,35), com o motivo embaixo.</summary>
    public bool Apagada => !Linha.Pode;

    public string? Motivo => Linha.Motivo;

    /// <summary>O selo "estimado" da linha do Extra, ou null.</summary>
    public string? Selo => Linha.Selo;

    public string? AvisoGanho => Linha.AvisoGanho;

    /// <summary>É o nível da memória em uso (a linha em ciano).</summary>
    public bool Escolhida => Linha.Escolhida;

    public Comando Tocar { get; } = tocar;
}

/// <summary>
/// A folha "Nível da memória" (história 13c; <c>telas-rendimento.md</c>, "Nível da memória" e "Aviso do Máximo"; sem
/// XAML, que é da 13g): uma linha por nível, do Baixo ao Máximo, com o teto em volts da saída que manda e a fonte, e o
/// Extra apagado com o pé da análise da playlist (história 13d): "Analisando a playlist." durante a análise, "Falta o
/// teste a seco." com ela pronta e "Falta analisar a playlist." sem ela; com o teste a seco feito (13e) e o modelo térmico de
/// todo alto-falante (13f), o Extra se acende ("Extra · até 22 V nas JBL L e JBL R · só a playlist", com o selo "estimado"
/// se algum dado térmico é só digitado) e se escolhe; sem o térmico, "Falta o modelo térmico do Sub.". O selo "revalidar" da
/// memória em uso vem com o motivo. Sem calibração ou sem o Bluetooth medido, as linhas ficam apagadas
/// com o motivo. Escolher grava o nível na memória em uso e põe o master da combinação com o preset em uso; o Máximo
/// pede antes a folha de risco do app (Confirmar e Cancelar). "Sem nível" volta a memória a null, sem mexer no master.
/// </summary>
public sealed class NivelViewModel : TelaBase, IDisposable
{
    public const string TituloFolha = "Nível da memória";
    public const string TextoSemNivel = "Sem nível";
    public const string TituloRisco = "Usar o nível Máximo?";
    public const string TextoConfirmarBotao = FolhaRiscoViewModel.TextoConfirmarBotao;
    public const string TextoCancelarBotao = FolhaRiscoViewModel.TextoCancelarBotao;
    public const string MsgSemMemoria = "Sem memória do celular em uso.";
    public const string MsgMemoriaMudou = "A memória em uso mudou. Escolha o nível de novo.";

    /// <summary>"Fechar" (a folha; história 13g).</summary>
    public const string TextoFecharBotao = "Fechar";

    /// <summary>O chip da memória sem nível (história 13g).</summary>
    public const string TextoChipSemNivel = "Sem nível ›";

    /// <summary>"Nível: Alto ›" (o chip no cartão da memória em uso; história 13g).</summary>
    public static string TextoChipDe(NivelRendimento nivel) => $"Nível: {Niveis.Nome(nivel)} ›";

    /// <summary>"Alto · até 16 V nas JBL L e JBL R · qualquer fonte".</summary>
    public static string TextoLinha(NivelRendimento nivel, double? tetoV, IReadOnlyList<string> saidas)
    {
        var nome = Niveis.Nome(nivel);
        if (tetoV is not { } v || saidas.Count == 0)
            return $"{nome} · {Niveis.TextoQualquerFonte}";
        var onde = saidas.Count == 1 ? "na" : "nas";
        return $"{nome} · até {Niveis.Volts(v)} {onde} {Ligacoes.Lista(saidas)} · {Niveis.TextoQualquerFonte}";
    }

    /// <summary>A fonte do Extra (história 13f).</summary>
    public const string TextoSoPlaylist = "só a playlist";

    /// <summary>"Extra · só a playlist" (sem o teto).</summary>
    public static string TextoExtra => $"{Niveis.NomeExtra} · {TextoSoPlaylist}";

    /// <summary>"Extra · até 22 V nas JBL L e JBL R · só a playlist" (história 13f).</summary>
    public static string TextoLinhaExtra(double? tetoV, IReadOnlyList<string> saidas)
    {
        if (tetoV is not { } v || saidas.Count == 0)
            return TextoExtra;
        var onde = saidas.Count == 1 ? "na" : "nas";
        return $"{Niveis.NomeExtra} · até {Niveis.Volts(v)} {onde} {Ligacoes.Lista(saidas)} · {TextoSoPlaylist}";
    }

    private readonly GerenteSetups _setups;
    private readonly EstadoAparelho _estado;
    private readonly AnalisePlaylist? _analise;
    private readonly LogBle? _log;
    private readonly INavegacao? _nav;
    private IReadOnlyList<LinhaNivel>? _linhas;
    private IReadOnlyList<ItemNivel>? _itens;
    private bool _folhaAberta;
    private bool _riscoAberto;
    private string? _memoriaDoRisco;
    private string? _mensagem;
    private bool _descartado;

    public NivelViewModel(GerenteSetups setups, EstadoAparelho estado, IDespachante despachante, TimeProvider relogio,
        AnalisePlaylist? analise = null, LogBle? log = null, INavegacao? nav = null)
        : base(despachante, relogio)
    {
        _setups = setups;
        _estado = estado;
        _analise = analise;
        _log = log;
        _nav = nav;
        Calibrar = new Comando(() =>
        {
            if (CanalSemCalibracao is not { } canal)
                return;
            RiscoAberto = false;
            FolhaAberta = false;
            _nav?.AbrirCalibracao(canal);
        });
        Abrir = new Comando(() => FolhaAberta = true);
        Fechar = new Comando(() =>
        {
            RiscoAberto = false;
            FolhaAberta = false;
        });
        Escolher = new ComandoCom<NivelRendimento>(EscolherAsync);
        EscolherExtra = new Comando(() => EscolherAsync(NivelRendimento.Extra));
        SemNivel = new Comando(() => DefinirAsync(null));
        Confirmar = new Comando(ConfirmarAsync);
        Cancelar = new Comando(() =>
        {
            _memoriaDoRisco = null;
            RiscoAberto = false;
        });
        _setups.Mudou += AoMudar;
        _estado.Recarregou += AoMudar;
        _estado.Mudou += AoMudarValor;
        if (_analise is not null)
            _analise.Mudou += AoMudar;
    }

    // ------------------------------------------------------------------ a folha

    /// <summary>"Nível da memória".</summary>
    public string Titulo => TituloFolha;

    /// <summary>A memória em uso ("FILME"), ou null.</summary>
    public string? Memoria => _setups.MemoriaEmUso?.Nome;

    /// <summary>"Sem memória do celular em uso." quando não há a quem dar o nível, ou null.</summary>
    public string? MotivoSemMemoria => _setups.MemoriaEmUso is null ? MsgSemMemoria : null;

    public bool FolhaAberta
    {
        get => _folhaAberta;
        private set
        {
            if (_folhaAberta == value)
                return;
            _folhaAberta = value;
            if (value)
                _mensagem = null;
            Avisar();
            Avisar(nameof(Mensagem));
        }
    }

    public Comando Abrir { get; }

    public Comando Fechar { get; }

    /// <summary>As linhas: Baixo, Médio, Alto, Máximo e o Extra (apagado).</summary>
    public IReadOnlyList<LinhaNivel> Linhas => _linhas ??= Montar();

    /// <summary>As linhas na tela, cada uma com o toque (história 13g).</summary>
    public IReadOnlyList<ItemNivel> Itens => _itens ??= Linhas
        .Select(l => new ItemNivel(l, l.Nivel is { } n ? new Comando(() => Escolher.ExecutarAsync(n)) : EscolherExtra))
        .ToArray();

    // ------------------------------------------------------------------ o chip e a faixa de sem calibração (história 13g)

    /// <summary>Há memória do celular em uso: o chip aparece no cartão dela.</summary>
    public bool MostrarChip => _setups.MemoriaEmUso is not null;

    /// <summary>"Nível: Alto ›" ou "Sem nível ›", ou null sem memória do celular em uso.</summary>
    public string? TextoChip => _setups.MemoriaEmUso is null ? null
        : _setups.NivelEmUso is { } n ? TextoChipDe(n) : TextoChipSemNivel;

    /// <summary>O canal mestre da primeira saída do Setup em uso sem calibração, ou null.</summary>
    private int? CanalSemCalibracao =>
        _setups.SetupEmUso is null ? null : SaidaTela.EmUso(_setups).FirstOrDefault(s => s.AvisoCalibracao is not null)?.Canal;

    /// <summary>"Sem calibração: o app não sabe a tensão." com alguma saída sem o ganho medido, ou null.</summary>
    public string? FaixaCalibracao => CanalSemCalibracao is null ? null : SemCalibracao.MsgAviso;

    /// <summary>"Calibrar ›".</summary>
    public string TextoCalibrar => LimiteSaidaViewModel.TextoCalibrar;

    /// <summary>Fecha a folha e abre a calibração guiada da primeira saída sem calibração.</summary>
    public Comando Calibrar { get; }

    /// <summary>O nível da memória em uso, ou null (sem nível).</summary>
    public NivelRendimento? NivelEscolhido => _setups.NivelEmUso;

    /// <summary>O selo "revalidar" da memória em uso (história 13e), ou null.</summary>
    public string? SeloRevalidar => _setups.MemoriaEmUso is { Revalidar: true } ? Niveis.SeloRevalidar : null;

    /// <summary>Por que a memória em uso está em "revalidar" ("A ficha ou a calibração mudou."), ou null.</summary>
    public string? MotivoRevalidar => _setups.MemoriaEmUso is { Revalidar: true } m ? m.MotivoRevalidar : null;

    /// <summary>A memória em uso está sem nível (o master é do usuário).</summary>
    public bool SemNivelEscolhido => _setups.MemoriaEmUso is not null && _setups.NivelEmUso is null;

    /// <summary>"Sem nível".</summary>
    public string TextoSemNivelBotao => TextoSemNivel;

    /// <summary>"Fechar".</summary>
    public string TextoFechar => TextoFecharBotao;

    /// <summary>A frase do último escolher ("Nível Alto na FILME.", "Calibre a saída JBL L."), ou null.</summary>
    public string? Mensagem
    {
        get => _mensagem;
        private set
        {
            if (_mensagem == value)
                return;
            _mensagem = value;
            Avisar();
        }
    }

    /// <summary>Escolhe o nível (o Máximo abre antes a folha de risco; o Extra, só com a linha acesa).</summary>
    public ComandoCom<NivelRendimento> Escolher { get; }

    /// <summary>O toque na linha do Extra (história 13f): escolhe o Extra, ou mostra o motivo da linha apagada.</summary>
    public Comando EscolherExtra { get; }

    /// <summary>"Sem nível": a memória volta a null, sem mexer no master.</summary>
    public Comando SemNivel { get; }

    // ------------------------------------------------------------------ a folha de risco do Máximo

    /// <summary>A folha de risco do Máximo está aberta.</summary>
    public bool RiscoAberto
    {
        get => _riscoAberto;
        private set
        {
            if (_riscoAberto == value)
                return;
            _riscoAberto = value;
            Avisar();
        }
    }

    /// <summary>"Usar o nível Máximo?"</summary>
    public string TituloDoRisco => TituloRisco;

    /// <summary>"Passa da potência da ficha. Seguro com música; …"</summary>
    public string Risco => Niveis.MsgMaximo;

    public string TextoConfirmar => TextoConfirmarBotao;

    public string TextoCancelar => TextoCancelarBotao;

    /// <summary>"Confirmar": o Máximo vale.</summary>
    public Comando Confirmar { get; }

    /// <summary>"Cancelar": o nível fica como estava.</summary>
    public Comando Cancelar { get; }

    // ------------------------------------------------------------------ escolher

    private async Task EscolherAsync(NivelRendimento nivel)
    {
        var linha = nivel == NivelRendimento.Extra
            ? Linhas.FirstOrDefault(l => l.Nome == Niveis.NomeExtra)
            : Linhas.FirstOrDefault(l => l.Nivel == nivel);
        if (linha is null || !linha.Pode)
        {
            Mensagem = linha?.Motivo ?? MotivoSemMemoria;
            return;
        }
        if (nivel == NivelRendimento.Maximo)
        {
            // A folha confirma o Máximo desta memória: se a memória em uso mudar com ela aberta, o Confirmar não vale.
            _memoriaDoRisco = _setups.MemoriaEmUso?.Id;
            RiscoAberto = true;
            return;
        }
        await DefinirAsync(nivel);
    }

    private async Task ConfirmarAsync()
    {
        if (!RiscoAberto)
            return;
        RiscoAberto = false;
        var confirmada = _memoriaDoRisco;
        _memoriaDoRisco = null;
        if (confirmada is null || _setups.MemoriaEmUso?.Id != confirmada)
        {
            Mensagem = MsgMemoriaMudou;
            return;
        }
        await DefinirAsync(NivelRendimento.Maximo, confirmada);
    }

    private async Task DefinirAsync(NivelRendimento? nivel, string? idMemoria = null)
    {
        if ((idMemoria ?? _setups.MemoriaEmUso?.Id) is not { } id)
        {
            Mensagem = MsgSemMemoria;
            return;
        }
        var r = await _setups.DefinirNivelAsync(id, nivel);
        NaTela(() =>
        {
            Mensagem = r.Mensagem ?? r.Motivo;
            Recalcular();
        });
    }

    // ------------------------------------------------------------------ as linhas

    private IReadOnlyList<LinhaNivel> Montar()
    {
        var emUso = _setups.MemoriaEmUso;
        var indisponivel = emUso is null ? MsgSemMemoria : _setups.NivelIndisponivel;
        var linhas = new List<LinhaNivel>();
        foreach (var nivel in Niveis.Todos)
        {
            ContaNivel? conta = null;
            if (indisponivel is null)
            {
                try
                {
                    conta = _setups.Conta(nivel);
                }
                catch (Exception)
                {
                    // Sem a conta, a linha fica apagada com o motivo.
                }
            }
            var motivo = indisponivel ?? (conta is null ? Niveis.MsgFalhou : conta.Master is null ? conta.Falta ?? Niveis.MsgFalhou : null);
            linhas.Add(new LinhaNivel(
                nivel,
                Niveis.Nome(nivel),
                TextoLinha(nivel, conta?.TetoNivelV, conta?.SaidasQueMandam ?? []),
                motivo is null,
                motivo,
                nivel == NivelRendimento.Maximo ? Niveis.MsgMaximo : null,
                conta?.Aviso,
                emUso?.Nivel == nivel));
        }
        linhas.Add(LinhaExtra(emUso, indisponivel));
        return linhas;
    }

    /// <summary>
    /// A linha do Extra (história 13f): acesa com o teste a seco validado na cadeia de agora e o modelo térmico de todo
    /// alto-falante passivo das saídas; o teto em volts da saída que manda (min(X, C) − 1 dB) e o selo "estimado".
    /// </summary>
    private LinhaNivel LinhaExtra(MemoriaCelular? emUso, string? indisponivel)
    {
        var motivo = MotivoExtra() ?? indisponivel;
        ContaNivel? conta = null;
        string? selo = null;
        if (motivo is null)
        {
            try
            {
                conta = _setups.Conta(NivelRendimento.Extra);
                selo = _setups.TermicoEmUso.Selo;
            }
            catch (Exception e)
            {
                _log?.Evento($"nível: falha na conta do Extra ({e.GetType().Name})");
                motivo = Niveis.MsgFalhou;
            }
        }
        return new LinhaNivel(null, Niveis.NomeExtra, TextoLinhaExtra(conta?.TetoNivelV, conta?.SaidasQueMandam ?? []), motivo is null,
            motivo, null, conta?.Aviso, emUso?.Nivel == NivelRendimento.Extra, selo);
    }

    /// <summary>
    /// Por que a linha do Extra está apagada, ou null (acesa): com o teste a seco feito (história 13e), "A configuração mudou
    /// depois do teste a seco…" com o validado de outra cadeia, ou "Falta o modelo térmico do Sub." sem os cinco dados
    /// térmicos de algum alto-falante (13f); sem o validado, o pé da análise da playlist (13d).
    /// </summary>
    private string? MotivoExtra()
    {
        if (_analise is null)
            return Niveis.MsgExtra;
        try
        {
            if (_setups.MemoriaEmUso is { } memoria && _setups.ExtraValidadoEmUso is not null)
                return _setups.MotivoSemExtra(memoria);
            return _analise.TetoEmUso().TextoNivel;
        }
        catch (Exception e)
        {
            _log?.Evento($"nível: falha ao ler o teto do Extra ({e.GetType().Name})");
            return Niveis.MsgExtra;
        }
    }

    private void AoMudar() => NaTela(Recalcular);

    private void AoMudarValor(Endereco endereco)
    {
        if (GerenteNiveis.CodigosDaCadeia.Contains(endereco.Codigo) || endereco.Codigo == Camadas.CodigoMaster)
            NaTela(Recalcular);
    }

    private void Recalcular()
    {
        if (_descartado)
            return;
        _linhas = null;
        _itens = null;
        Avisar(nameof(Linhas));
        Avisar(nameof(Itens));
        Avisar(nameof(MostrarChip));
        Avisar(nameof(TextoChip));
        Avisar(nameof(FaixaCalibracao));
        Avisar(nameof(Memoria));
        Avisar(nameof(MotivoSemMemoria));
        Avisar(nameof(NivelEscolhido));
        Avisar(nameof(SemNivelEscolhido));
        Avisar(nameof(SeloRevalidar));
        Avisar(nameof(MotivoRevalidar));
    }

    public void Dispose()
    {
        _descartado = true;
        _setups.Mudou -= AoMudar;
        _estado.Recarregou -= AoMudar;
        _estado.Mudou -= AoMudarValor;
        if (_analise is not null)
            _analise.Mudou -= AoMudar;
    }
}
