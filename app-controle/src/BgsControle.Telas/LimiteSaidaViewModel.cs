using System.Globalization;
using BgsControle.Nucleo;
using BgsControle.Nucleo.Escrita;
using BgsControle.Nucleo.Rendimento;
using BgsControle.Nucleo.Setups;

namespace BgsControle.Telas;

/// <summary>
/// Um dos três limites na folha "A conta" (história 13g; <c>telas-rendimento.md</c>, "Limite da saída"): o nome, o
/// valor em volts (ou "∞" quando o duto protege o cone, ou "—" sem o dado), o selo, a conta, os dados usados com a
/// origem de cada um e o dado que falta.
/// </summary>
/// <param name="Nome">"Térmico", "Curso do cone" ou "Ceifamento do módulo".</param>
/// <param name="Valor">"17,9 V", "∞" ou "—".</param>
/// <param name="Selo">"verificado" ou "estimado".</param>
/// <param name="Conta">"√(80 W × 4 Ω) = 17,9 V", ou null sem o dado.</param>
/// <param name="Dados">Os dados usados, com a origem: "Potência RMS: 80 W · oficial ✓".</param>
/// <param name="Falta">"Falta a Fb da caixa do Sub.", ou null.</param>
/// <param name="Manda">É o limite que manda (o menor).</param>
public sealed record LinhaContaLimite(TipoLimite Tipo, string Nome, string Valor, string Selo, string? Conta, IReadOnlyList<string> Dados,
    string? Falta, bool Manda)
{
    /// <summary>Tem algum dado usado para mostrar.</summary>
    public bool TemDados => Dados.Count > 0;

    /// <summary>Os dados numa linha cada (para um rótulo só).</summary>
    public string TextoDados => string.Join("\n", Dados);

    /// <summary>"manda" na linha do limite que manda, ou null.</summary>
    public string? TextoManda => Manda ? LimiteSaidaViewModel.TextoMandaSelo : null;
}

/// <summary>
/// O limite da saída no Ajuste da saída, perto do limitador (história 13g; <c>telas-rendimento.md</c>, "Limite da
/// saída" e "Sem calibração", [ASSUMPTION] o lugar): uma linha com o teto do nível em uso em volts e em dB, o que manda
/// (térmico, curso do cone ou módulo) e o selo ("17,9 V · −6,8 dB · pelo térmico · verificado"); um toque abre a folha
/// "A conta", com os três limites, cada um com o valor, a conta, os dados usados com a origem e o dado que falta. Sem
/// calibração, a faixa "Sem calibração: o app não sabe a tensão." com "Calibrar ›" (a calibração guiada desta saída);
/// sem limite, o motivo. O teto é o da cerca no nível da memória em uso (no Máximo e no Extra, min(X, C)); com um
/// nível, a linha de apoio diz até onde o nível leva o pico ("Nível Alto: até 16 V."). Lê
/// <see cref="GerenteSetups.LimitesEmUso"/> e <see cref="GerenteSetups.ContaEmUso"/> e refaz a cada mudança da ficha,
/// da calibração, do link, do nível ou do corte.
/// </summary>
public sealed class LimiteSaidaViewModel : TelaBase, IDisposable
{
    public const string RotuloLimite = "LIMITE DA SAÍDA";
    public const string TituloConta = "A conta";
    public const string TextoCalibrar = "Calibrar ›";
    public const string TextoFechar = "Fechar";
    public const string DicaConta = "Toque para ver a conta.";
    public const string RotuloDados = "Dados usados";
    public const string TextoMandaSelo = "manda";
    public const string TextoSemValor = "—";
    public const string TextoInfinito = "∞";
    public const string TextoLido = "lido do aparelho";
    public const string TextoSemPassaAlta = "desligado";
    public const string MsgFalhou = "Não consegui calcular o limite desta saída.";

    /// <summary>"Térmico", "Curso do cone" ou "Ceifamento do módulo".</summary>
    public static string NomeTitulo(TipoLimite tipo)
    {
        var nome = LimiteFisico.NomeLimite(tipo);
        return char.ToUpperInvariant(nome[0]) + nome[1..];
    }

    /// <summary>"17,9 V" (sempre com uma casa).</summary>
    public static string Volts(double v) => v.ToString("0.0", CultureInfo.InvariantCulture).Replace('.', ',') + " V";

    /// <summary>"17,9 V · −6,8 dB · pelo térmico · verificado" (sem o dB sem calibração).</summary>
    public static string TextoLinha(double tetoV, decimal? tetoDb, TipoLimite manda, bool estimado)
    {
        var partes = new List<string> { Volts(tetoV) };
        if (tetoDb is { } db)
            partes.Add($"{LimiteFisico.Db(db)} dB");
        partes.Add($"pelo {LimiteFisico.NomeLimite(manda)}");
        partes.Add(estimado ? LimiteFisico.SeloEstimado : LimiteFisico.SeloVerificado);
        return string.Join(" · ", partes);
    }

    /// <summary>"Alto-falante que manda: JBL L".</summary>
    public static string TextoAltoFalante(string nome) => $"Alto-falante que manda: {nome}";

    /// <summary>"Nível Alto: até 16 V."</summary>
    public static string TextoNivelAte(NivelRendimento nivel, double v) => $"Nível {Niveis.Nome(nivel)}: até {Niveis.Volts(v)}.";

    /// <summary>"Ganho do módulo: 19,5 × · medido ✓" (o que converte os volts em dB do limitador).</summary>
    public static string TextoGanho(DadoTecnico? ganho, bool ponte)
    {
        var nome = ponte ? "Ganho do módulo em ponte" : "Ganho do módulo";
        return ganho is null ? $"{nome}: {TextoSemValor}" : $"{nome}: {Num(ganho.Valor)} × · {DadoViewModel.TextoSelo(ganho.Origem)}";
    }

    private readonly GerenteSetups _setups;
    private readonly EstadoAparelho _estado;
    private readonly LinkCanais _link;
    private readonly INavegacao _nav;
    private readonly int _pedido;
    private Retrato _retrato = Retrato.Vazio;
    private bool _contaAberta;
    private bool _descartado;

    /// <summary>O que a tela mostra agora (refeito a cada mudança).</summary>
    private sealed record Retrato(
        bool Visivel,
        int Canal,
        string? Linha,
        string? MotivoSemLimite,
        string? Aviso,
        string? FaixaCalibracao,
        string? Nivel,
        string? AltoFalante,
        string? Ganho,
        IReadOnlyList<LinhaContaLimite> Limites)
    {
        public static Retrato Vazio { get; } = new(false, 0, null, null, null, null, null, null, null, []);

        public bool Igual(Retrato o) =>
            Visivel == o.Visivel && Canal == o.Canal && Linha == o.Linha && MotivoSemLimite == o.MotivoSemLimite && Aviso == o.Aviso
            && FaixaCalibracao == o.FaixaCalibracao && Nivel == o.Nivel && AltoFalante == o.AltoFalante && Ganho == o.Ganho
            && Limites.Count == o.Limites.Count && Limites.Zip(o.Limites).All(p => Iguais(p.First, p.Second));

        private static bool Iguais(LinhaContaLimite a, LinhaContaLimite b) =>
            a.Tipo == b.Tipo && a.Nome == b.Nome && a.Valor == b.Valor && a.Selo == b.Selo && a.Conta == b.Conta && a.Falta == b.Falta
            && a.Manda == b.Manda && a.Dados.SequenceEqual(b.Dados);
    }

    /// <param name="canal">O canal pedido (0 a 3): o limite é o da saída que tem ele.</param>
    public LimiteSaidaViewModel(int canal, GerenteSetups setups, EstadoAparelho estado, LinkCanais link, INavegacao nav,
        IDespachante despachante, TimeProvider relogio)
        : base(despachante, relogio)
    {
        _pedido = canal;
        _setups = setups;
        _estado = estado;
        _link = link;
        _nav = nav;
        AbrirConta = new Comando(() => ContaAberta = PodeAbrirConta);
        FecharConta = new Comando(() => ContaAberta = false);
        Calibrar = new Comando(() =>
        {
            ContaAberta = false;
            _nav.AbrirCalibracao(_retrato.Canal);
        });
        _setups.Mudou += AoMudar;
        _estado.Recarregou += AoMudar;
        _estado.Mudou += AoMudarValor;
        _link.Mudou += AoMudarLink;
        _retrato = Montar();
    }

    // ------------------------------------------------------------------ a linha

    /// <summary>"LIMITE DA SAÍDA".</summary>
    public string Rotulo => RotuloLimite;

    /// <summary>Há Setup em uso e a saída do canal: o bloco aparece.</summary>
    public bool Visivel => _retrato.Visivel;

    /// <summary>"17,9 V · −6,8 dB · pelo térmico · verificado", ou null sem limite.</summary>
    public string? Linha => _retrato.Linha;

    /// <summary>O motivo de a saída ficar sem limite ("Sem alto-falante passivo: …", "Sem nenhum dos três limites…"), ou null.</summary>
    public string? MotivoSemLimite => _retrato.MotivoSemLimite;

    /// <summary>"O limitador não chega ao limite físico desta saída." (o teto abaixo de −48 dB), ou null.</summary>
    public string? Aviso => _retrato.Aviso;

    /// <summary>"Nível Alto: até 16 V." com um nível na memória em uso, ou null.</summary>
    public string? TextoNivel => _retrato.Nivel;

    /// <summary>"Toque para ver a conta." quando a conta se abre.</summary>
    public string? Dica => PodeAbrirConta ? DicaConta : null;

    // ------------------------------------------------------------------ sem calibração

    /// <summary>"Sem calibração: o app não sabe a tensão." (história 13b) numa saída com alto-falante passivo sem o ganho medido, ou null.</summary>
    public string? FaixaCalibracao => _retrato.FaixaCalibracao;

    /// <summary>"Calibrar ›".</summary>
    public string TextoBotaoCalibrar => TextoCalibrar;

    /// <summary>Abre a calibração guiada desta saída (pelo canal mestre).</summary>
    public Comando Calibrar { get; }

    // ------------------------------------------------------------------ a folha "A conta"

    /// <summary>A saída tem um alto-falante com os limites: a conta se abre.</summary>
    public bool PodeAbrirConta => _retrato.Limites.Count > 0;

    public bool ContaAberta
    {
        get => _contaAberta;
        private set
        {
            if (_contaAberta == value)
                return;
            _contaAberta = value;
            Avisar();
        }
    }

    public Comando AbrirConta { get; }

    public Comando FecharConta { get; }

    /// <summary>"A conta".</summary>
    public string Titulo => TituloConta;

    /// <summary>"Fechar".</summary>
    public string TextoBotaoFechar => TextoFechar;

    /// <summary>"Dados usados".</summary>
    public string TituloDados => RotuloDados;

    /// <summary>"Alto-falante que manda: JBL L", ou null.</summary>
    public string? AltoFalante => _retrato.AltoFalante;

    /// <summary>"Ganho do módulo: 19,5 × · medido ✓" (sem calibração, "—"), ou null.</summary>
    public string? Ganho => _retrato.Ganho;

    /// <summary>Térmico, curso do cone e ceifamento do módulo, nessa ordem.</summary>
    public IReadOnlyList<LinhaContaLimite> Limites => _retrato.Limites;

    // ------------------------------------------------------------------ a conta

    private Retrato Montar()
    {
        if (_setups.SetupEmUso is not { } setup)
            return Retrato.Vazio;
        try
        {
            var saidas = _setups.SaidasEmUso;
            var saida = saidas.FirstOrDefault(s => s.Canais.Contains(_pedido));
            if (saida is null)
                return Retrato.Vazio;
            var limite = _setups.LimitesEmUso.FirstOrDefault(l => l.Saida.Canais.Contains(_pedido));
            var faixa = SemCalibracao.Sem(setup.Equipamentos, saida) ? SemCalibracao.MsgAviso : null;
            if (limite is null)
                return Retrato.Vazio with { Visivel = true, Canal = saida.Mestre, FaixaCalibracao = faixa };

            string? linha = limite is { TetoV: { } v, Manda: { } manda } ? TextoLinha(v, limite.TetoDb, manda, limite.Estimado) : null;
            var af = AltoFalanteQueManda(setup.Equipamentos, limite);
            var modulo = af?.Ligado is { } l ? setup.Equipamentos.FirstOrDefault(e => e.Id == l.IdModulo) : null;
            var ponte = af?.Ligado?.Bridge is not null;
            var canalDoLimite = limite.PorCanal.FirstOrDefault(c => c.AltoFalante == limite.AltoFalante)?.Canal ?? saida.Mestre;
            var passaAlta = LimiteFisico.PassaAltaDe(_estado.Valor, canalDoLimite);

            var limites = new List<LinhaContaLimite>();
            if (limite.Termico is { } t)
                limites.Add(LinhaDe(t, limite.Manda, af is null ? [] : DadosTermico(af)));
            if (limite.Excursao is { } x)
                limites.Add(LinhaDe(x, limite.Manda, af is null ? [] : DadosExcursao(af, passaAlta)));
            if (limite.Ceifamento is { } c)
                limites.Add(LinhaDe(c, limite.Manda, modulo is null ? [] : DadosCeifamento(modulo, ponte)));

            var ganho = modulo is null ? null : TextoGanho(ponte ? modulo.Dados.GanhoPonte : modulo.Dados.Ganho, ponte);
            return new Retrato(true, saida.Mestre, linha, limite.SemLimite, limite.Aviso, faixa, Nivel(limite),
                limite.AltoFalante is { } nome ? TextoAltoFalante(nome) : null, ganho, limites);
        }
        catch (Exception)
        {
            // Sem a conta, a tela diz que não conseguiu (a cerca segue no núcleo, que recusa por conta própria).
            return Retrato.Vazio with { Visivel = true, Canal = _pedido, MotivoSemLimite = MsgFalhou };
        }
    }

    /// <summary>"Nível Alto: até 16 V." pela conta do nível em uso, no canal desta saída que manda nela; ou null.</summary>
    private string? Nivel(LimiteSaida limite)
    {
        if (_setups.NivelEmUso is not { } nivel)
            return null;
        ContaNivel? conta;
        try
        {
            conta = _setups.ContaEmUso();
        }
        catch (Exception)
        {
            return null;
        }
        var canal = conta?.Canais.Where(c => limite.Saida.Canais.Contains(c.Canal) && c.TetoV is not null).MinBy(c => c.TetoV);
        if (canal?.TetoV is not { } teto)
            return null;
        return TextoNivelAte(nivel, teto * Math.Pow(10, -(double)Niveis.Margem(nivel) / 20));
    }

    /// <summary>O alto-falante passivo que manda na saída (pelo nome, entre os que os canais dela alimentam), ou null.</summary>
    private static Equipamento? AltoFalanteQueManda(IReadOnlyList<Equipamento> equipamentos, LimiteSaida limite) =>
        limite.AltoFalante is not { } nome
            ? null
            : Ligacoes.Alimentados(equipamentos, limite.Saida.Canais.ToArray())
                .FirstOrDefault(e => e.Grupo == GrupoEquipamento.AltoFalante && e.Nome == nome);

    private static LinhaContaLimite LinhaDe(Limite l, TipoLimite? manda, IReadOnlyList<string> dados)
    {
        var valor = l.Infinito ? TextoInfinito : l.V is { } v ? Volts(v) : TextoSemValor;
        return new LinhaContaLimite(l.Tipo, NomeTitulo(l.Tipo), valor, l.Selo, l.Conta, dados, l.Falta, manda == l.Tipo);
    }

    private static IReadOnlyList<string> DadosTermico(Equipamento af) =>
        new[] { Dado("Potência RMS", af.Dados.PotenciaRms, "W"), Dado("Impedância", af.Dados.Impedancia, "Ω") }.OfType<string>().ToArray();

    private static IReadOnlyList<string> DadosExcursao(Equipamento af, PassaAlta? passaAlta)
    {
        var d = af.Dados;
        var dados = new List<string?>
        {
            Dado("Re", d.Re, "Ω"), Dado("Fs", d.Fs, "Hz"), Dado("Qms", d.Qms, null), Dado("Qes", d.Qes, null), Dado("Vas", d.Vas, "L"),
            Dado("Bl", d.Bl, "T·m"), Dado("Sd", d.Sd, "cm²"), Dado("Xmax", d.Xmax, "mm"), Dado("Le", d.Le, "mH"),
            d.TipoCaixa is { } tipo ? $"Tipo da caixa: {tipo.Valor} · {DadoViewModel.TextoSelo(tipo.Origem)}" : null,
            Dado("Volume da caixa", d.VolumeCaixa, "L"), Dado("Fb", d.Fb, "Hz"),
        };
        var filtro = passaAlta is { } pa ? $"{pa.Rotulo} em {Num((decimal)pa.Frequencia)} Hz" : TextoSemPassaAlta;
        dados.Add($"Passa-alta: {filtro} · {TextoLido}");
        return dados.OfType<string>().ToArray();
    }

    private static IReadOnlyList<string> DadosCeifamento(Equipamento modulo, bool ponte)
    {
        var dado = ponte ? modulo.Dados.TensaoCeifamentoPonte : modulo.Dados.TensaoCeifamentoCanal;
        var nome = ponte ? $"Ceifamento em ponte do {modulo.Nome}" : $"Ceifamento por canal do {modulo.Nome}";
        return new[] { Dado(nome, dado, "V") }.OfType<string>().ToArray();
    }

    /// <summary>"Potência RMS: 80 W · oficial ✓", ou null sem o dado.</summary>
    private static string? Dado(string nome, DadoTecnico? dado, string? unidade) =>
        dado is null ? null : $"{nome}: {Num(dado.Valor)}{(unidade is null ? "" : " " + unidade)} · {DadoViewModel.TextoSelo(dado.Origem)}";

    /// <summary>"3,5", "80", "1,75".</summary>
    private static string Num(decimal v) => v.ToString("0.##", CultureInfo.InvariantCulture).Replace('.', ',');

    // ------------------------------------------------------------------ atualização

    private void AoMudar() => NaTela(Atualizar);

    private void AoMudarLink(ParCanais _) => NaTela(Atualizar);

    private void AoMudarValor(Endereco e)
    {
        if (Cerca.Toca(e.Codigo) || GerenteNiveis.CodigosDaCadeia.Contains(e.Codigo))
            NaTela(Atualizar);
    }

    private void Atualizar()
    {
        if (_descartado)
            return;
        var novo = Montar();
        if (novo.Igual(_retrato))
            return;
        _retrato = novo;
        if (!PodeAbrirConta)
            ContaAberta = false;
        foreach (var nome in new[]
                 {
                     nameof(Visivel), nameof(Linha), nameof(MotivoSemLimite), nameof(Aviso), nameof(TextoNivel), nameof(Dica),
                     nameof(FaixaCalibracao), nameof(PodeAbrirConta), nameof(AltoFalante), nameof(Ganho), nameof(Limites),
                 })
            Avisar(nome);
    }

    public void Dispose()
    {
        _descartado = true;
        _setups.Mudou -= AoMudar;
        _estado.Recarregou -= AoMudar;
        _estado.Mudou -= AoMudarValor;
        _link.Mudou -= AoMudarLink;
    }
}
