using System.Globalization;
using BgsControle.Nucleo.Escrita;
using BgsControle.Nucleo.Presets;
using BgsControle.Nucleo.Registro;
using BgsControle.Nucleo.Seguranca;
using BgsControle.Nucleo.Setups;
using BgsControle.Nucleo.Uso;

namespace BgsControle.Nucleo.Rendimento;

/// <summary>
/// O nível de rendimento de uma memória do celular (história 13c, spec-rendimento CAP-4). Null na memória = sem
/// nível: o master é do usuário. O Extra (história 13e) usa o master validado pelo teste a seco.
/// </summary>
public enum NivelRendimento
{
    /// <summary>Teto = min(T, X, C) − 6 dB.</summary>
    Baixo,

    /// <summary>Teto = min(T, X, C) − 3 dB.</summary>
    Medio,

    /// <summary>Teto = min(T, X, C) − 1 dB.</summary>
    Alto,

    /// <summary>Teto = min(X, C) − 1 dB: passa da potência da ficha (com o aviso do calor).</summary>
    Maximo,

    /// <summary>
    /// Teto = min(X, C) − 1 dB pelos picos reais da playlist (história 13e): o master é o validado pelo teste a seco
    /// (<see cref="Setups.MemoriaCelular.ExtraValidado"/>), e só baixa pela revalidação e pelo fiscal.
    /// </summary>
    Extra,
}

/// <summary>
/// Os níveis Baixo a Máximo (história 13c, <c>limites-e-niveis.md</c>): a margem, o teto, os nomes, os textos e
/// quando o nível é oferecido. A conta do master fica no <see cref="PiorCaso"/>.
/// </summary>
public static class Niveis
{
    /// <summary>Os níveis do pior caso, na ordem da tela (o Extra tem a linha dele).</summary>
    public static IReadOnlyList<NivelRendimento> Todos { get; } =
        [NivelRendimento.Baixo, NivelRendimento.Medio, NivelRendimento.Alto, NivelRendimento.Maximo];

    public const string NomeExtra = "Extra";

    /// <summary>O motivo da linha do Extra sem a análise da playlist (história 13d).</summary>
    public const string MsgExtra = "Falta analisar a playlist.";

    /// <summary>O Extra sem o master validado do preset em uso (história 13e).</summary>
    public const string MsgFaltaTesteSeco = "Falta o teste a seco.";

    /// <summary>O Extra com o validado de outra cadeia (a memória, o preset, a ficha ou a calibração mudou depois do teste).</summary>
    public const string MsgRefazerTesteSeco = "A configuração mudou depois do teste a seco. Faça o teste de novo.";

    /// <summary>A escolha do Extra na tela, apagada até a 13f (CAP-7).</summary>
    public const string MsgFaltaTermico = "Falta o modelo térmico.";

    /// <summary>O motivo do "revalidar" quando a ficha ou o ganho medido do Setup muda (história 13e).</summary>
    public const string MsgFichaMudou = "A ficha ou a calibração mudou.";

    /// <summary>O selo da memória em "revalidar".</summary>
    public const string SeloRevalidar = "revalidar";

    /// <summary>"Clip duas vezes na Faixa 3." (o 2º clip não previsto na mesma faixa).</summary>
    public static string MsgClipDuasVezes(string faixa) => $"Clip duas vezes na {faixa}.";

    /// <summary>O aviso do Máximo, na folha de risco.</summary>
    public const string MsgMaximo =
        "Passa da potência da ficha. Seguro com música; um tom contínuo ou música muito comprimida por muito tempo podem aquecer a bobina.";

    public const string MsgBluetooth = "Meça o nível do Bluetooth.";
    public const string MsgSemSetup = "Escolha um Setup para usar os níveis.";
    public const string MsgSemLimite = "Nenhuma saída tem limite: o app não sabe o teto.";
    public const string MsgFaltaGanho = "O nível não chega ao teto: falta ganho na memória.";
    public const string MsgMasterCurto = "O nível não chega ao teto: o master não desce o bastante.";
    public const string MsgFalhou = "Não consegui conferir o nível da memória. Nada foi enviado.";
    public const string MsgSoEmUso = "O nível se escolhe com a memória em uso.";

    /// <summary>A fonte dos níveis Baixo a Máximo.</summary>
    public const string TextoQualquerFonte = "qualquer fonte";

    /// <summary>"Calibre a saída JBL L."</summary>
    public static string MsgCalibre(string saida) => $"Calibre a saída {saida}.";

    /// <summary>"O nível não chega ao teto: falta ganho na memória. Saída: JBL L."</summary>
    public static string MsgFaltaGanhoNa(string saida) => $"{MsgFaltaGanho} Saída: {saida}.";

    /// <summary>"O nível não chega ao teto: o master não desce o bastante. Saída: JBL L."</summary>
    public static string MsgMasterCurtoNa(string saida) => $"{MsgMasterCurto} Saída: {saida}.";

    /// <summary>"Baixe o limitador da JBL L para −6,8 dB antes de sair do Máximo."</summary>
    public static string MsgSairDoMaximo(string saida, decimal teto) => MsgSairDo(NivelRendimento.Maximo, saida, teto);

    /// <summary>"Baixe o limitador da JBL L para −6,8 dB antes de sair do Extra." (do Máximo ou do Extra para um nível com o térmico).</summary>
    public static string MsgSairDo(NivelRendimento nivel, string saida, decimal teto) =>
        $"Baixe o limitador da {saida} para {LimiteFisico.Db(teto)} dB antes de sair do {Nome(nivel)}.";

    /// <summary>"O master não foi escrito: …" (o nível ficou gravado; a conta na hora tenta de novo).</summary>
    public static string MsgMasterNaoEscrito(string motivo) => $"O master não foi escrito: {motivo}";

    /// <summary>"Nível Alto: o master vai até −8,8 dB."</summary>
    public static string MsgMasterAcima(NivelRendimento nivel, decimal master) =>
        $"Nível {Nome(nivel)}: o master vai até {LimiteFisico.Db(master)} dB.";

    /// <summary>"Nível Alto na FILME."</summary>
    public static string MsgDefinido(NivelRendimento nivel, string memoria) => $"Nível {Nome(nivel)} na {memoria}.";

    /// <summary>"A FILME ficou sem nível."</summary>
    public static string MsgSemNivel(string memoria) => $"A {memoria} ficou sem nível.";

    /// <summary>A margem abaixo do teto, em dB: 6, 3, 1 e 1.</summary>
    public static decimal Margem(NivelRendimento nivel) => nivel switch
    {
        NivelRendimento.Baixo => 6m,
        NivelRendimento.Medio => 3m,
        _ => 1m,
    };

    /// <summary>O Máximo e o Extra: o teto é min(X, C) (o térmico fica de fora), na conta e na cerca.</summary>
    public static bool SemTermico(NivelRendimento? nivel) => nivel is NivelRendimento.Maximo or NivelRendimento.Extra;

    /// <summary>"Baixo", "Médio", "Alto", "Máximo" ou "Extra".</summary>
    public static string Nome(NivelRendimento nivel) => nivel switch
    {
        NivelRendimento.Baixo => "Baixo",
        NivelRendimento.Medio => "Médio",
        NivelRendimento.Alto => "Alto",
        NivelRendimento.Maximo => "Máximo",
        NivelRendimento.Extra => NomeExtra,
        _ => throw new ArgumentOutOfRangeException(nameof(nivel), nivel, "Nível desconhecido."),
    };

    /// <summary>O nome no JSON (<c>setups.json</c> e a porta): <c>baixo</c>, <c>medio</c>, <c>alto</c>, <c>maximo</c> ou <c>extra</c>.</summary>
    public static string Chave(NivelRendimento nivel) => nivel switch
    {
        NivelRendimento.Baixo => "baixo",
        NivelRendimento.Medio => "medio",
        NivelRendimento.Alto => "alto",
        NivelRendimento.Maximo => "maximo",
        NivelRendimento.Extra => "extra",
        _ => throw new ArgumentOutOfRangeException(nameof(nivel), nivel, "Nível desconhecido."),
    };

    /// <summary>O nível pelo nome do JSON; um texto que não vale (ou ausente) fica null (sem nível).</summary>
    public static NivelRendimento? De(string? chave) =>
        Todos.Append(NivelRendimento.Extra).Cast<NivelRendimento?>().FirstOrDefault(n => Chave(n!.Value) == chave);

    /// <summary>
    /// Por que os níveis não são oferecidos, ou null: sem Setup em uso; uma saída com alto-falante passivo sem o
    /// ganho medido ("Calibre a saída JBL L."); ou o nível do Bluetooth sem a origem "medido" ("Meça o nível do
    /// Bluetooth.").
    /// </summary>
    public static string? Indisponivel(Setup? setup, IReadOnlyList<SaidaApp> saidas)
    {
        if (setup is null)
            return MsgSemSetup;
        var nomes = Ligacoes.NomesNaTela(saidas);
        for (var i = 0; i < saidas.Count; i++)
        {
            if (SemCalibracao.Sem(setup.Equipamentos, saidas[i]))
                return MsgCalibre(nomes[i]);
        }
        return setup.NivelBluetooth is { Origem: OrigemDado.Medido } ? null : MsgBluetooth;
    }

    /// <summary>"16 V", "8,9 V": inteiro a partir de 10 V (já arredondado: 9,96 V é "10 V"), uma casa abaixo.</summary>
    public static string Volts(double v)
    {
        var umaCasa = Math.Round(v, 1, MidpointRounding.AwayFromZero);
        return (umaCasa >= 10
            ? Math.Round(v, MidpointRounding.AwayFromZero).ToString("0", CultureInfo.InvariantCulture)
            : umaCasa.ToString("0.0", CultureInfo.InvariantCulture).Replace('.', ',')) + " V";
    }
}

/// <summary>
/// A conta do nível refeita na hora (história 13c, spec-rendimento "Revalidação"): com a memória do celular em uso
/// num nível e sem a marca da cópia (nenhuma operação andando ou pela metade), quando muda um endereço da cadeia no
/// espelho (<c>030</c>–<c>032</c>, <c>020</c>, <c>010</c>, <c>081</c>, <c>090</c>–<c>092</c>, <c>040</c>–<c>043</c>),
/// a ficha ou o ganho medido do Setup, o nível do Bluetooth ou o preset, o app escreve o master da conta, com a
/// origem App, se ele difere do atual em 0,1 dB ou mais (baixar e subir valem do mesmo jeito), e guarda o valor na
/// memória (<see cref="MemoriaCelular.Masters"/>). Uma conta por vez; um pedido no meio refaz no fim.
/// </summary>
public sealed class GerenteNiveis
{
    /// <summary>Os códigos da cadeia que mudam o pior caso (o 220, o preset de EQ, reescreve as 15 bandas).</summary>
    public static IReadOnlySet<string> CodigosDaCadeia { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        "030", "031", "032", "020", "220", "010", "081", "090", "091", "092", "040", "041", "042", "043",
    };

    /// <summary>A menor diferença que faz o app escrever o master.</summary>
    public const decimal Passo = 0.1m;

    private static readonly Parametro Master = Catalogo.ParametroDe(Camadas.CodigoMaster)!;

    private readonly EstadoAparelho _estado;
    private readonly Escritor _escritor;
    private readonly GerenteSetups _setups;
    private readonly GerentePresets _presets;
    private readonly CopiaAntes _copia;
    private readonly LogBle _log;

    private readonly object _trava = new();
    private bool _rodando;
    private bool _deNovo;

    public GerenteNiveis(EstadoAparelho estado, Escritor escritor, GerenteSetups setups, GerentePresets presets, CopiaAntes copia, LogBle log)
    {
        _estado = estado;
        _escritor = escritor;
        _setups = setups;
        _presets = presets;
        _copia = copia;
        _log = log;
        _estado.Mudou += e =>
        {
            if (CodigosDaCadeia.Contains(e.Codigo))
                Pedir();
        };
        _estado.Recarregou += Pedir;
        // A ficha, o ganho medido, o Bluetooth, o preset lembrado, o nível ou o fim de uma operação do Setup.
        _setups.Mudou += Pedir;
        _presets.Mudou += Pedir;
        // A marca da cópia saiu: o que mudou no meio da operação entra na conta.
        _copia.Mudou += Pedir;
    }

    /// <summary>Uma conta anda (ou espera para andar).</summary>
    public bool Refazendo
    {
        get
        {
            lock (_trava)
                return _rodando;
        }
    }

    /// <summary>O último master que a conta escreveu, ou null.</summary>
    public decimal? UltimoEscrito { get; private set; }

    /// <summary>Pede a conta (ela roda numa tarefa; com uma andando, ela roda de novo no fim).</summary>
    public void Pedir()
    {
        lock (_trava)
        {
            if (_rodando)
            {
                _deNovo = true;
                return;
            }
            _rodando = true;
        }
        _ = Task.Run(RodarAsync);
    }

    private async Task RodarAsync()
    {
        while (true)
        {
            try
            {
                await RefazerAsync();
            }
            catch (Exception e)
            {
                _log.Evento($"nível: falha ao refazer a conta ({e.GetType().Name})");
            }
            lock (_trava)
            {
                if (!_deNovo)
                {
                    _rodando = false;
                    return;
                }
                _deNovo = false;
            }
        }
    }

    /// <summary>A conta da memória em uso agora e, se o master difere, a escrita dele e o registro na memória.</summary>
    private async Task RefazerAsync()
    {
        if (!_estado.Pronto || _estado.Lendo || _copia.MotivoBloqueio() is not null || _setups.Ocupado || _presets.Ocupado)
            return;
        // História 13e: o master do Extra só muda pela revalidação e pelo fiscal (FiscalExtra), não pela conta na hora.
        if (_setups.MemoriaEmUso is not { Nivel: { } nivel } memoria || nivel == NivelRendimento.Extra)
            return;
        if (_setups.Conta(nivel) is not { Master: { } master })
            return;
        // O corte do usuário (o master baixado à mão) fica: a conta escreve conta − corte.
        var alvo = _setups.MasterComCorte(master);
        var atual = _estado.Valor(Camadas.CodigoMaster)?.Numero;
        if (atual is null || Math.Abs(atual.Value - alvo) >= Passo)
        {
            ResultadoEscrita r;
            using (Origem.Como(OrigemComando.App))
                r = await _escritor.EscreverAsync(Master, null, alvo);
            if (!r.Ok)
            {
                _log.Evento($"nível {Niveis.Nome(nivel)} da {memoria.Nome}: o master não foi escrito ({r.Motivo})");
                return;
            }
            UltimoEscrito = r.Valor?.Numero ?? alvo;
            _log.Evento($"nível {Niveis.Nome(nivel)} da {memoria.Nome}: master de {Texto(atual)} para {Texto(alvo)} dB");
        }
        var idPreset = _setups.IdPresetEmUso;
        if (_setups.MemoriaEmUso is { Id: var id } emUso && id == memoria.Id
            && (!emUso.Masters.TryGetValue(idPreset ?? "", out var guardado) || guardado != master))
            await _setups.GuardarMasterAsync(memoria.Id, idPreset, master);
    }

    private static string Texto(decimal? db) => db is { } v ? v.ToString("0.0", CultureInfo.InvariantCulture) : "—";
}
