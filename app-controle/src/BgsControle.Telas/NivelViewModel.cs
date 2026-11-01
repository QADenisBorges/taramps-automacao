using BgsControle.Nucleo.Escrita;
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
public sealed record LinhaNivel(NivelRendimento? Nivel, string Nome, string Texto, bool Pode, string? Motivo, string? AvisoCalor,
    string? AvisoGanho, bool Escolhida);

/// <summary>
/// A folha "Nível da memória" (história 13c; <c>telas-rendimento.md</c>, "Nível da memória" e "Aviso do Máximo"; sem
/// XAML, que é da 13g): uma linha por nível, do Baixo ao Máximo, com o teto em volts da saída que manda e a fonte, e o
/// Extra apagado ("Falta analisar a playlist."). Sem calibração ou sem o Bluetooth medido, as linhas ficam apagadas
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

    /// <summary>"Alto · até 16 V nas JBL L e JBL R · qualquer fonte".</summary>
    public static string TextoLinha(NivelRendimento nivel, double? tetoV, IReadOnlyList<string> saidas)
    {
        var nome = Niveis.Nome(nivel);
        if (tetoV is not { } v || saidas.Count == 0)
            return $"{nome} · {Niveis.TextoQualquerFonte}";
        var onde = saidas.Count == 1 ? "na" : "nas";
        return $"{nome} · até {Niveis.Volts(v)} {onde} {Ligacoes.Lista(saidas)} · {Niveis.TextoQualquerFonte}";
    }

    /// <summary>"Extra · só a playlist auditada".</summary>
    public static string TextoExtra => $"{Niveis.NomeExtra} · só a playlist auditada";

    private readonly GerenteSetups _setups;
    private readonly EstadoAparelho _estado;
    private IReadOnlyList<LinhaNivel>? _linhas;
    private bool _folhaAberta;
    private bool _riscoAberto;
    private string? _memoriaDoRisco;
    private string? _mensagem;
    private bool _descartado;

    public NivelViewModel(GerenteSetups setups, EstadoAparelho estado, IDespachante despachante, TimeProvider relogio)
        : base(despachante, relogio)
    {
        _setups = setups;
        _estado = estado;
        Abrir = new Comando(() => FolhaAberta = true);
        Fechar = new Comando(() =>
        {
            RiscoAberto = false;
            FolhaAberta = false;
        });
        Escolher = new ComandoCom<NivelRendimento>(EscolherAsync);
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

    /// <summary>O nível da memória em uso, ou null (sem nível).</summary>
    public NivelRendimento? NivelEscolhido => _setups.NivelEmUso;

    /// <summary>A memória em uso está sem nível (o master é do usuário).</summary>
    public bool SemNivelEscolhido => _setups.MemoriaEmUso is not null && _setups.NivelEmUso is null;

    /// <summary>"Sem nível".</summary>
    public string TextoSemNivelBotao => TextoSemNivel;

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

    /// <summary>Escolhe o nível (o Máximo abre antes a folha de risco).</summary>
    public ComandoCom<NivelRendimento> Escolher { get; }

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
        var linha = Linhas.FirstOrDefault(l => l.Nivel == nivel);
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
        linhas.Add(new LinhaNivel(null, Niveis.NomeExtra, TextoExtra, false, Niveis.MsgExtra, null, null, false));
        return linhas;
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
        Avisar(nameof(Linhas));
        Avisar(nameof(Memoria));
        Avisar(nameof(MotivoSemMemoria));
        Avisar(nameof(NivelEscolhido));
        Avisar(nameof(SemNivelEscolhido));
    }

    public void Dispose()
    {
        _descartado = true;
        _setups.Mudou -= AoMudar;
        _estado.Recarregou -= AoMudar;
        _estado.Mudou -= AoMudarValor;
    }
}
