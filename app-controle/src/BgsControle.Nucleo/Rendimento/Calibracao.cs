using System.Globalization;
using BgsControle.Nucleo.Escrita;
using BgsControle.Nucleo.Gerador;
using BgsControle.Nucleo.Player;
using BgsControle.Nucleo.Registro;
using BgsControle.Nucleo.Seguranca;
using BgsControle.Nucleo.Setups;

namespace BgsControle.Nucleo.Rendimento;

/// <summary>Os passos da calibração guiada (história 13b; <c>telas-rendimento.md</c>), nesta ordem.</summary>
public enum EtapaCalibracao
{
    /// <summary>Nenhuma calibração aberta.</summary>
    Fechada,

    /// <summary>1) "Desligue os alto-falantes do módulo." O usuário confirma.</summary>
    Desligar,

    /// <summary>2) "Multímetro em V~ 200 na saída …". Ao seguir, a operação começa.</summary>
    Multimetro,

    /// <summary>3) O gerador toca o tom a 0 dB, com a contagem de 30 s.</summary>
    Tom,

    /// <summary>4) O usuário digita os volts lidos; o gerador para.</summary>
    DigitarTom,

    /// <summary>5) O celular a 100 % e o som pelo Bluetooth: o tom de 0 dBFS do celular.</summary>
    Bluetooth,

    /// <summary>6) O usuário digita os volts do tom do celular; o tom para.</summary>
    DigitarBluetooth,

    /// <summary>7) O som de antes voltou; o ganho e o nível gravados.</summary>
    Fim,
}

/// <summary>
/// O que se calibra numa saída: a saída do app, o nome na tela e o módulo do grupo 2 que ela alcança, com o
/// canal (a partir de 1) ou a ponte em que o alto-falante está.
/// </summary>
public sealed record AlvoCalibracao(SaidaApp Saida, string Nome, Equipamento Modulo, int? CanalModulo, Bridge? Bridge)
{
    /// <summary>Na ponte, grava o <c>GanhoPonte</c>; senão, o <c>Ganho</c> do módulo.</summary>
    public bool Ponte => Bridge is not null;

    /// <summary>"CH1", ou "ponte CH3+CH4": onde vai o multímetro, nos bornes do módulo.</summary>
    public string Onde => Bridge is { } b ? $"ponte CH{b.A}+CH{b.B}" : $"CH{CanalModulo}";
}

/// <summary>Uma saída do Setup em uso na escolha da calibração: o alvo, ou o motivo de não se calibrar.</summary>
/// <param name="Calibrada">A saída tem o ganho do módulo medido (<see cref="SemCalibracao.Sem"/> falso).</param>
public sealed record OpcaoCalibracao(SaidaApp Saida, string Nome, AlvoCalibracao? Alvo, string? Motivo, bool Calibrada);

/// <summary>Como terminou um passo da calibração. <see cref="Mensagem"/> é a frase para a tela.</summary>
public sealed record ResultadoCalibracao(bool Ok, string? Motivo, string? Mensagem = null)
{
    public static ResultadoCalibracao Falha(string motivo) => new(false, motivo, motivo);

    public static ResultadoCalibracao Feito(string? mensagem = null) => new(true, null, mensagem);
}

/// <summary>
/// A calibração guiada de uma saída (spec-rendimento CAP-3, história 13b): leva o usuário por uma saída do
/// Setup em uso que chega a um módulo do grupo 2, mede o ganho do módulo com o gerador e o multímetro e o nível
/// do Bluetooth com um tom de 0 dBFS do celular, grava os dois como "medido" e volta ao som de antes.
/// <list type="bullet">
/// <item><b>Para começar:</b> conexão, leitura pronta, firmware que escreve, Setup em uso sem dúvida, nenhuma
/// operação na cópia, as chaves do Gerador e dos Ajustes de áudio ligadas, o módulo ligado (<c>252</c> = 1), o
/// gerador livre, uma saída com módulo e um corte que deixa passar um tom entre 50 e 400 Hz.</item>
/// <item><b>A operação</b> começa ao seguir do multímetro: a cópia antes (o tipo <see cref="Tipo"/>, com o
/// gerador liberado) e as escritas: os outros canais mutados, o limitador em Lref = min(−11,0; teto da 13a),
/// o EQ de saída plano, o ganho e o MASTER em 0. O tom toca em f a 0 dB.</item>
/// <item><b>As contas:</b> ganho = V / (2,0 × 10^(Lref/20)), de ×1 a ×100; nível = 20·log10(V / (2,0 ×
/// ganho)), com o limitador em min(0; teto). O ganho vai para a ficha do módulo e o nível para o Setup, com a
/// origem "medido".</item>
/// <item><b>Fim e Cancelar</b> voltam ao som de antes (<see cref="CopiaAntes.DesfazerAsync"/>, com o limitador
/// no teto da 13a); o que já foi gravado fica. A queda segue a regra 1 (gerador) e a 3: ao reconectar, fica
/// "aplicado pela metade", e o Terminar não refaz.</item>
/// </list>
/// Crie cedo (antes da primeira conexão): o tipo fica registrado na <see cref="CopiaAntes"/>.
/// </summary>
public sealed class Calibracao : IDisposable
{
    /// <summary>O tipo da marca na <see cref="CopiaAntes"/>; o alvo é o id do Setup.</summary>
    public const string Tipo = "calibrar-saida";

    /// <summary>O limitador da medida do ganho: a saída do processador fica em 2,0 × 10^(−11/20) = 0,564 V.</summary>
    public const decimal LimitadorReferencia = -11.0m;

    public const int FrequenciaMinima = 50;
    public const int FrequenciaMaxima = 400;
    public const int PassoFrequencia = 10;

    /// <summary>Acima desta atenuação do corte em f, a calibração não começa.</summary>
    public const double AtenuacaoMaxima = 3.0;

    public const decimal VoltsMinimo = 0.1m;
    public const decimal VoltsMaximo = 200m;
    public const decimal GanhoMinimo = 1m;
    public const decimal GanhoMaximo = 100m;

    /// <summary>O nível medido a menos disto do limitador: o limitador segurou o tom.</summary>
    public const decimal FolgaLimitador = 0.5m;

    private const string CodigoLimitadorAuto = "070";
    private const string CodigoEqSaidaGanho = "091";
    private const string CodigoEqEntradaGanho = "030";
    private const string CodigoBanda = "020";

    /// <summary>O tom do celular dura até isto.</summary>
    public static TimeSpan DuracaoTomCelular { get; } = TimeSpan.FromSeconds(30);

    public const string MsgSemConexao = "Sem conexão com o aparelho.";
    public const string MsgSoLeitura = "Modo só leitura: este firmware não aceita ajustes.";
    public const string MsgSemSetup = "Escolha um Setup para calibrar.";
    public const string MsgChaveGerador = "Ligue a chave do Gerador antes de calibrar.";
    public const string MsgChaveAjustes = "Ligue a chave dos Ajustes de áudio antes de calibrar.";
    public const string MsgModuloDesligado = "Ligue o módulo (remote out) antes de calibrar.";
    public const string MsgSemModulo = "Esta saída não passa por um módulo.";
    public const string MsgCorte = "O corte desta saída não deixa passar um tom entre 50 e 400 Hz.";
    public const string MsgSaidaNaoExiste = "Esta saída não existe mais no Setup em uso.";
    public const string MsgJaAberta = "Já há uma calibração em andamento.";
    public const string MsgFechada = "Nenhuma calibração em andamento.";
    public const string MsgForaDaEtapa = "Este passo não é o da calibração agora.";
    public const string MsgDesligar = "Desligue os alto-falantes do módulo.";
    public const string MsgVolts = "Digite a tensão lida, de 0,1 a 200 V.";
    public const string MsgForaDoBluetooth = "O som do celular não está saindo pelo Bluetooth.";
    public const string MsgPedeBluetooth = "Ponha o volume do celular em 100 % e o som saindo pelo Bluetooth.";
    public const string MsgTomCelular = "O celular não conseguiu tocar o tom.";
    public const string MsgLimitadorSegurou = "O limitador segurou o tom; confira o volume.";
    public const string MsgReligue = "Religue os alto-falantes.";
    public const string MsgCancelada = "Calibração cancelada. O som voltou ao de antes.";
    public const string MsgCaiu = "A conexão caiu no meio da calibração. Ao reconectar, o app mostra o que ficou.";

    /// <summary>"Multímetro em V~ 200 na saída CH1 do Módulo SD800.4 EVO."</summary>
    public static string MsgMultimetro(AlvoCalibracao alvo) => $"Multímetro em V~ 200 na saída {alvo.Onde} do {alvo.Modulo.Nome}.";

    /// <summary>"Confira a leitura: o ganho daria ×0,5."</summary>
    public static string MsgConfiraLeitura(decimal ganho) => $"Confira a leitura: o ganho daria ×{Ganho(ganho)}.";

    /// <summary>"Ganho ×19,5 e Bluetooth −13,0 dB gravados."</summary>
    public static string MsgGravados(decimal ganho, decimal nivel) => $"Ganho ×{Ganho(ganho)} e Bluetooth {LimiteFisico.Db(nivel)} dB gravados.";

    /// <summary>"Ganho ×40,5 gravado." (o Bluetooth já estava medido).</summary>
    public static string MsgGanhoGravado(decimal ganho) => $"Ganho ×{Ganho(ganho)} gravado.";

    /// <summary>"Não consegui preparar a saída: … O som voltou ao de antes."</summary>
    public static string MsgNaoPreparou(string motivo) => $"Não consegui preparar a saída: {motivo}";

    /// <summary>"O som não voltou todo (…). Ficou aplicado pela metade."</summary>
    public static string MsgNaoVoltou(string motivo) => $"O som não voltou todo ({motivo}). Ficou aplicado pela metade.";

    /// <summary>A fonte do dado medido: "Calibração guiada em 31/10/2026".</summary>
    public static string Fonte(DateTimeOffset quando) => $"Calibração guiada em {quando.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)}";

    /// <summary>"19,5": o ganho com uma casa (o meio vai para cima).</summary>
    public static string Ganho(decimal ganho) =>
        decimal.Round(ganho, 1, MidpointRounding.AwayFromZero).ToString("0.0", CultureInfo.InvariantCulture).Replace('.', ',');

    /// <summary>ganho = V / (2,0 × 10^(Lref/20)), com duas casas.</summary>
    public static decimal GanhoDe(decimal volts, decimal limitador) =>
        decimal.Round((decimal)((double)volts / (LimiteFisico.ReferenciaLimitador * Math.Pow(10, (double)limitador / 20))), 2);

    /// <summary>
    /// nível = 20·log10(V / (2,0 × ganho)) + a atenuação do corte em f (o nível na entrada, antes do passa-alta e do
    /// passa-baixa da saída), com uma casa.
    /// </summary>
    public static decimal NivelDe(decimal volts, decimal ganho, double atenuacao = 0) =>
        decimal.Round((decimal)(20 * Math.Log10((double)volts / (LimiteFisico.ReferenciaLimitador * (double)ganho)) + atenuacao), 1);

    /// <summary>A tolerância da escolha de f: a menor frequência até 0,1 dB acima da menor atenuação.</summary>
    public const double ToleranciaFrequencia = 0.1;

    private readonly Sessao _sessao;
    private readonly EstadoAparelho _estado;
    private readonly Escritor _escritor;
    private readonly CopiaAntes _copia;
    private readonly GerenteSetups _setups;
    private readonly GeradorSinal _gerador;
    private readonly IChaves _chaves;
    private readonly ITomCelular _tom;
    private readonly Player.Player? _player;
    private readonly LogBle _log;
    private readonly TimeProvider _relogio;

    private readonly object _trava = new();

    /// <summary>Um passo de cada vez: o cancelar espera o passo que já anda.</summary>
    private readonly SemaphoreSlim _vez = new(1, 1);

    private EtapaCalibracao _etapa = EtapaCalibracao.Fechada;
    private long _geracao;
    private AlvoCalibracao? _alvo;
    private string? _idSetup;
    private int _frequencia;
    private decimal _limitador;
    private decimal _limitadorBluetooth;
    private bool _operacao;
    private bool _medirBluetooth;
    private bool _retomarPlayer;
    private double _atenuacao;
    private bool _ocupado;
    private decimal? _ganho;
    private decimal? _nivel;
    private string? _mensagem;

    public Calibracao(
        Sessao sessao,
        EstadoAparelho estado,
        Escritor escritor,
        CopiaAntes copia,
        GerenteSetups setups,
        GeradorSinal gerador,
        IChaves chaves,
        ITomCelular tom,
        LogBle log,
        Player.Player? player = null,
        TimeProvider? relogio = null)
    {
        _sessao = sessao;
        _estado = estado;
        _escritor = escritor;
        _copia = copia;
        _setups = setups;
        _gerador = gerador;
        _chaves = chaves;
        _tom = tom;
        _player = player;
        _log = log;
        _relogio = relogio ?? TimeProvider.System;

        // Uma marca achada na conexão nova: pela metade. Terminar não existe: as medidas eram do usuário.
        _copia.Registrar(Tipo,
            _ => Task.FromResult(Retomada.PelaMetade),
            _ => Task.FromResult(ResultadoCopia.Falha(CopiaAntes.MsgNaoSeiRefazer)),
            deixaGerador: true);
        _sessao.Mudou += AoMudarSessao;
    }

    /// <summary>A etapa, a mensagem ou o que foi medido mudou. Pode chegar em qualquer thread.</summary>
    public event Action? Mudou;

    public EtapaCalibracao Etapa
    {
        get
        {
            lock (_trava)
                return _etapa;
        }
    }

    /// <summary>A saída que se calibra, ou null.</summary>
    public AlvoCalibracao? Alvo
    {
        get
        {
            lock (_trava)
                return _alvo;
        }
    }

    /// <summary>A frequência do tom (50 a 400 Hz), ou null sem calibração aberta.</summary>
    public int? Frequencia
    {
        get
        {
            lock (_trava)
                return _etapa == EtapaCalibracao.Fechada ? null : _frequencia;
        }
    }

    /// <summary>O limitador da medida do ganho (Lref), ou null sem calibração aberta.</summary>
    public decimal? Limitador
    {
        get
        {
            lock (_trava)
                return _etapa == EtapaCalibracao.Fechada ? null : _limitador;
        }
    }

    /// <summary>O ganho gravado nesta calibração, ou null.</summary>
    public decimal? GanhoMedido
    {
        get
        {
            lock (_trava)
                return _ganho;
        }
    }

    /// <summary>O nível do Bluetooth gravado nesta calibração, ou null.</summary>
    public decimal? NivelMedido
    {
        get
        {
            lock (_trava)
                return _nivel;
        }
    }

    /// <summary>A última frase (a recusa, a leitura a conferir, o fim), ou null.</summary>
    public string? Mensagem
    {
        get
        {
            lock (_trava)
                return _mensagem;
        }
    }

    /// <summary>Um passo anda (as escritas, a gravação, a volta).</summary>
    public bool Ocupado
    {
        get
        {
            lock (_trava)
                return _ocupado;
        }
    }

    /// <summary>A operação começou (a cópia antes e as escritas): o cancelar volta ao som de antes.</summary>
    public bool EmOperacao
    {
        get
        {
            lock (_trava)
                return _operacao;
        }
    }

    /// <summary>O usuário pediu "Medir o Bluetooth de novo".</summary>
    public bool MedirBluetooth
    {
        get
        {
            lock (_trava)
                return _medirBluetooth;
        }
    }

    /// <summary>O nível do Bluetooth medido do Setup em uso, ou null: com ele, os passos 5 e 6 ficam de fora.</summary>
    public DadoTecnico? BluetoothMedido => _setups.SetupEmUso?.NivelBluetooth is { Origem: OrigemDado.Medido } n ? n : null;

    private bool SessaoAberta => _sessao.Estado == EstadoSessao.Conectado && _sessao.SegurandoConexao;

    // ------------------------------------------------------------------ para começar

    /// <summary>Por que não dá para calibrar agora (qualquer saída), ou null. Cada recusa com o seu motivo.</summary>
    public string? MotivoParaComecar()
    {
        if (!SessaoAberta)
            return MsgSemConexao;
        if (_sessao.SoLeitura)
            return MsgSoLeitura;
        if (_estado.Lendo)
            return Escritor.MsgLendo;
        if (!_estado.Pronto)
            return Escritor.MsgSemLeitura;
        if (_setups.EmEscolha)
            return GerenteSetups.MsgDuvida;
        if (_setups.SetupEmUso is null)
            return MsgSemSetup;
        if (_copia.MotivoBloqueio() is { } bloqueio)
            return bloqueio;
        if (!_chaves.Ligada(Chave.Gerador))
            return MsgChaveGerador;
        if (!_chaves.Ligada(Chave.AjustesAudio))
            return MsgChaveAjustes;
        if (_estado.Valor(Catalogo.CodigoRemoteOut)?.Numero != 1m)
            return MsgModuloDesligado;
        if (_gerador.TalvezLigado || _gerador.Estado != EstadoGerador.Desligado)
            return GeradorSinal.MsgEmUso;
        return null;
    }

    /// <summary>As saídas do Setup em uso, cada uma com o alvo ou o motivo de não se calibrar (o módulo, o corte).</summary>
    public IReadOnlyList<OpcaoCalibracao> Opcoes()
    {
        if (_setups.SetupEmUso is not { } setup)
            return [];
        var saidas = _setups.SaidasEmUso;
        var nomes = Ligacoes.NomesNaTela(saidas);
        return saidas.Select((s, i) =>
        {
            var alvo = AlvoDe(setup.Equipamentos, s, nomes[i]);
            string? motivo = alvo is null ? MsgSemModulo : EscolherFrequencia(_estado.Valor, s.Canais) is null ? MsgCorte : null;
            return new OpcaoCalibracao(s, nomes[i], alvo, motivo, !SemCalibracao.Sem(setup.Equipamentos, s));
        }).ToArray();
    }

    /// <summary>
    /// O módulo que a saída alcança: o primeiro alto-falante passivo dos canais dela (<see cref="Ligacoes.Caminhos"/>),
    /// com o módulo e o canal ou a ponte. Null: a saída não passa por um módulo (só caixa ativa, ou nada).
    /// </summary>
    public static AlvoCalibracao? AlvoDe(IReadOnlyList<Equipamento> equipamentos, SaidaApp saida, string nome) =>
        saida.Canais.SelectMany(c => Ligacoes.Caminhos(equipamentos, c)).FirstOrDefault() is { } caminho
            ? new AlvoCalibracao(saida, nome, caminho.Modulo, caminho.CanalModulo, caminho.Bridge)
            : null;

    /// <summary>
    /// A frequência do tom: de 50 a 400 Hz, numa grade de 10 Hz, a mais baixa com a atenuação do passa-alta e do
    /// passa-baixa dos canais (no par, a pior dos dois) até 0,1 dB acima da menor da grade (longe da borda da faixa
    /// AC do multímetro). Null se a menor passa de 3 dB.
    /// </summary>
    public static int? EscolherFrequencia(Func<Endereco, Valor?> ler, IReadOnlyList<int> canais)
    {
        var grade = Enumerable.Range(0, (FrequenciaMaxima - FrequenciaMinima) / PassoFrequencia + 1)
            .Select(i => FrequenciaMinima + i * PassoFrequencia)
            .Select(f => (Hz: f, Atenuacao: AtenuacaoDosCanais(ler, canais, f)))
            .ToArray();
        var menor = grade.Min(g => g.Atenuacao);
        if (menor > AtenuacaoMaxima)
            return null;
        return grade.First(g => g.Atenuacao <= menor + ToleranciaFrequencia + 1e-9).Hz;
    }

    /// <summary>A pior atenuação do corte em f entre os canais, em dB.</summary>
    public static double AtenuacaoDosCanais(Func<Endereco, Valor?> ler, IReadOnlyList<int> canais, double f) =>
        canais.Max(c => Atenuacao(ler, c, f));

    /// <summary>A atenuação do corte do canal em f, em dB (o passa-alta e o passa-baixa, só pelo módulo).</summary>
    public static double Atenuacao(Func<Endereco, Valor?> ler, int canal, double f)
    {
        var pa = LimiteFisico.PassaAltaDe(ler, canal);
        var pb = PassaBaixa.DoAparelho(ler(new Endereco("043", canal))?.Numero, ler(new Endereco("041", canal))?.Numero);
        var h = (pa?.Modulo(f) ?? 1.0) * (pb?.Modulo(f) ?? 1.0);
        return -20 * Math.Log10(h);
    }

    /// <summary>A saída do Setup em uso, já com o alvo e a frequência, ou o motivo.</summary>
    private (AlvoCalibracao? Alvo, int Frequencia, string? Motivo) Preparar(int mestre)
    {
        if (MotivoParaComecar() is { } motivo)
            return (null, 0, motivo);
        var opcao = Opcoes().FirstOrDefault(o => o.Saida.Mestre == mestre);
        if (opcao is null)
            return (null, 0, MsgSaidaNaoExiste);
        if (opcao.Alvo is null)
            return (null, 0, MsgSemModulo);
        if (EscolherFrequencia(_estado.Valor, opcao.Saida.Canais) is not { } f)
            return (null, 0, MsgCorte);
        return (opcao.Alvo, f, null);
    }

    // ------------------------------------------------------------------ os passos

    /// <summary>Abre a calibração da saída (pelo canal mestre): o passo 1, "Desligue os alto-falantes do módulo."</summary>
    public ResultadoCalibracao Abrir(int mestre)
    {
        lock (_trava)
        {
            if (_etapa is not (EtapaCalibracao.Fechada or EtapaCalibracao.Fim) || _ocupado)
                return ResultadoCalibracao.Falha(MsgJaAberta);
        }
        var (alvo, f, motivo) = Preparar(mestre);
        if (motivo is not null)
            return Recusar(motivo);
        lock (_trava)
        {
            if (_etapa is not (EtapaCalibracao.Fechada or EtapaCalibracao.Fim) || _ocupado)
                return ResultadoCalibracao.Falha(MsgJaAberta);
            _geracao++;
            _alvo = alvo;
            _idSetup = _setups.SetupEmUso?.Id;
            _frequencia = f;
            _limitador = LimitadorReferencia;
            _operacao = false;
            _medirBluetooth = false;
            _retomarPlayer = false;
            _ganho = null;
            _nivel = null;
            _mensagem = null;
            _etapa = EtapaCalibracao.Desligar;
        }
        _log.Evento($"calibração: {alvo!.Nome} ({alvo.Onde} do {alvo.Modulo.Nome}), tom de {f} Hz");
        Notificar();
        return ResultadoCalibracao.Feito();
    }

    /// <summary>Passo 1: o usuário confirma que desligou os alto-falantes do módulo.</summary>
    public ResultadoCalibracao ConfirmarDesligado()
    {
        lock (_trava)
        {
            if (_etapa != EtapaCalibracao.Desligar)
                return ResultadoCalibracao.Falha(_etapa == EtapaCalibracao.Fechada ? MsgFechada : MsgForaDaEtapa);
            _etapa = EtapaCalibracao.Multimetro;
            _mensagem = null;
        }
        Notificar();
        return ResultadoCalibracao.Feito();
    }

    /// <summary>
    /// "Medir o Bluetooth de novo": com o nível já medido no Setup, os passos 5 e 6 voltam depois do ganho. Vale
    /// até o ganho ser gravado.
    /// </summary>
    public ResultadoCalibracao MedirBluetoothDeNovo()
    {
        lock (_trava)
        {
            if (_etapa is not (EtapaCalibracao.Desligar or EtapaCalibracao.Multimetro or EtapaCalibracao.Tom or EtapaCalibracao.DigitarTom))
                return ResultadoCalibracao.Falha(_etapa == EtapaCalibracao.Fechada ? MsgFechada : MsgForaDaEtapa);
            _medirBluetooth = true;
        }
        Notificar();
        return ResultadoCalibracao.Feito();
    }

    /// <summary>
    /// Passo 2, seguir: a operação começa. A cópia antes (o tipo <see cref="Tipo"/>, com o gerador liberado), os
    /// outros canais mutados, o limitador em Lref, o EQ de saída plano, o ganho e o MASTER em 0, e o tom.
    /// Uma escrita que falha volta ao som de antes e fecha.
    /// </summary>
    public async Task<ResultadoCalibracao> SeguirAsync()
    {
        await _vez.WaitAsync();
        try
        {
            long geracao;
            AlvoCalibracao alvo;
            lock (_trava)
            {
                if (_etapa != EtapaCalibracao.Multimetro || _alvo is null)
                    return ResultadoCalibracao.Falha(_etapa == EtapaCalibracao.Fechada ? MsgFechada : MsgForaDaEtapa);
                geracao = _geracao;
                alvo = _alvo;
            }
            var (agora, f, motivo) = Preparar(alvo.Saida.Mestre);
            if (motivo is not null)
                return Recusar(motivo);
            // O Setup em uso, o módulo e a ponte são os do Abrir: senão, o ganho iria para o lugar errado.
            if (!agora!.Saida.Canais.SequenceEqual(alvo.Saida.Canais) || _setups.SetupEmUso?.Id != _idSetup
                || agora.Modulo.Id != alvo.Modulo.Id || agora.CanalModulo != alvo.CanalModulo || agora.Bridge != alvo.Bridge)
                return Recusar(MsgSaidaNaoExiste);

            // Lref = min(−11,0; o teto da cerca da 13a, num canal cercado).
            decimal lref;
            try
            {
                var teto = _setups.LimitesEmUso.FirstOrDefault(l => l.Saida.Mestre == alvo.Saida.Mestre)?.TetoCerca;
                lref = teto is { } t ? Math.Min(LimitadorReferencia, t) : LimitadorReferencia;
            }
            catch (Exception e)
            {
                _log.Evento($"calibração: falha ao conferir o limite ({e.GetType().Name})");
                return Recusar(Cerca.MsgFalhou);
            }

            var atenuacao = AtenuacaoDosCanais(_estado.Valor, alvo.Saida.Canais, f);
            var copia = await _copia.IniciarAsync(new OperacaoPendente(Tipo, _idSetup ?? "", $"Calibrar a saída {alvo.Nome}"));
            if (!copia.Ok)
                return Recusar(copia.Motivo!);
            if (!Atual(geracao))
            {
                // Cancelado (ou caiu) enquanto a cópia era guardada, antes de qualquer escrita. Com a sessão aberta,
                // foi o cancelar: a marca sai. Sem ela, a marca fica para a próxima conexão.
                if (SessaoAberta)
                {
                    await _copia.ConcluirAsync();
                    return ResultadoCalibracao.Falha(MsgCancelada);
                }
                _copia.Interromper();
                return ResultadoCalibracao.Falha(MsgCaiu);
            }
            lock (_trava)
            {
                if (_geracao != geracao)
                {
                    _copia.Interromper();
                    return ResultadoCalibracao.Falha(MsgCaiu);
                }
                _operacao = true;
                _ocupado = true;
                _frequencia = f;
                _atenuacao = atenuacao;
                _limitador = lref;
                _mensagem = null;
            }
            Notificar();

            var canais = alvo.Saida.Canais;
            var outros = Enumerable.Range(0, Parametro.Canais).Where(c => !canais.Contains(c)).ToArray();
            var escritas = new List<(string, int?, decimal)>();
            escritas.AddRange(outros.Select(c => (Camadas.CodigoMute, (int?)c, 1m)));
            // O limitador em manual (070 = 0) antes do limiar: a conta supõe o limiar fixo, que segura o tom.
            escritas.AddRange(canais.Select(c => (CodigoLimitadorAuto, (int?)c, 0m)));
            escritas.AddRange(canais.Select(c => (Cerca.CodigoLimitador, (int?)c, lref)));
            escritas.AddRange(canais.Select(c => (CodigoEqSaidaGanho, (int?)c, 0m)));
            // O EQ de entrada e as 15 bandas planos: um corte deles em f daria um tom abaixo do limiar, e o nível do
            // Bluetooth mediria o EQ. A cópia os devolve no fim.
            escritas.Add((CodigoEqEntradaGanho, null, 0m));
            escritas.AddRange(Enumerable.Range(0, Catalogo.BandasHz.Count).Select(b => (CodigoBanda, (int?)b, 0m)));
            escritas.AddRange(canais.Select(c => (Camadas.CodigoGanho, (int?)c, 0m)));
            escritas.AddRange(canais.Select(c => (Camadas.CodigoMute, (int?)c, 0m)));
            escritas.Add((Camadas.CodigoMaster, null, 0m));
            _log.Evento($"calibração: preparando a {alvo.Nome} (limitador em {lref.ToString("0.0", CultureInfo.InvariantCulture)} dB)");
            var falha = await EscreverAsync(escritas);

            if (falha is not null)
            {
                if (!Atual(geracao))
                    return ResultadoCalibracao.Falha(MsgCaiu);
                _log.Evento($"calibração: não preparou a saída ({falha}); voltando ao som de antes");
                var volta = await VoltarAsync();
                return Fechar(geracao, volta.Ok ? MsgNaoPreparou(falha) : MsgNaoVoltou(volta.Motivo ?? falha));
            }
            if (!Atual(geracao))
                return ResultadoCalibracao.Falha(MsgCaiu);
            lock (_trava)
            {
                _etapa = EtapaCalibracao.Tom;
                _ocupado = false;
            }
            Notificar();
            return await TocarGeradorAsync(geracao);
        }
        finally
        {
            lock (_trava)
                _ocupado = false;
            _vez.Release();
        }
    }

    /// <summary>Passo 3 → 4: o usuário vai digitar a leitura (o tom segue até ele confirmar ou até o prazo).</summary>
    public ResultadoCalibracao IrParaLeitura()
    {
        lock (_trava)
        {
            if (_etapa != EtapaCalibracao.Tom)
                return ResultadoCalibracao.Falha(_etapa == EtapaCalibracao.Fechada ? MsgFechada : MsgForaDaEtapa);
            _etapa = EtapaCalibracao.DigitarTom;
        }
        Notificar();
        return ResultadoCalibracao.Feito();
    }

    /// <summary>"Tocar de novo": o tom do gerador (passos 3 e 4) ou o do celular (passo 6), enquanto não houver resposta.</summary>
    public async Task<ResultadoCalibracao> TocarDeNovoAsync()
    {
        await _vez.WaitAsync();
        try
        {
            EtapaCalibracao etapa;
            long geracao;
            lock (_trava)
            {
                etapa = _etapa;
                geracao = _geracao;
            }
            return etapa switch
            {
                EtapaCalibracao.Tom or EtapaCalibracao.DigitarTom => await TocarGeradorAsync(geracao),
                EtapaCalibracao.DigitarBluetooth => await TocarCelularAsync(geracao),
                EtapaCalibracao.Fechada => ResultadoCalibracao.Falha(MsgFechada),
                _ => ResultadoCalibracao.Falha(MsgForaDaEtapa),
            };
        }
        finally
        {
            _vez.Release();
        }
    }

    /// <summary>
    /// Passo 4: os volts lidos (0,1 a 200). O gerador para; o ganho = V / (2,0 × 10^(Lref/20)) tem de ficar entre
    /// ×1 e ×100 (senão, "Confira a leitura" e o passo se repete) e vai para a ficha do módulo, como medido. Depois,
    /// o Bluetooth (se o Setup não tem o nível medido, ou se o usuário pediu) ou o fim.
    /// </summary>
    public async Task<ResultadoCalibracao> InformarTomAsync(decimal volts)
    {
        await _vez.WaitAsync();
        try
        {
            long geracao;
            AlvoCalibracao alvo;
            decimal lref;
            lock (_trava)
            {
                if (_etapa is not (EtapaCalibracao.Tom or EtapaCalibracao.DigitarTom) || _alvo is null)
                    return ResultadoCalibracao.Falha(_etapa == EtapaCalibracao.Fechada ? MsgFechada : MsgForaDaEtapa);
                geracao = _geracao;
                alvo = _alvo;
                lref = _limitador;
                _etapa = EtapaCalibracao.DigitarTom;
            }
            if (volts < VoltsMinimo || volts > VoltsMaximo)
                return Recusar(MsgVolts);

            await PararGeradorAsync();
            var ganho = GanhoDe(volts, lref);
            if (ganho < GanhoMinimo || ganho > GanhoMaximo)
            {
                _log.Evento($"calibração: {volts.ToString(CultureInfo.InvariantCulture)} V daria o ganho ×{ganho.ToString(CultureInfo.InvariantCulture)}");
                return Recusar(MsgConfiraLeitura(ganho));
            }

            Ocupar(true);
            var dado = new DadoTecnico(ganho, OrigemDado.Medido, Fonte(_relogio.GetLocalNow()));
            var r = await _setups.GravarGanhoMedidoAsync(_idSetup ?? "", alvo.Modulo.Id, alvo.Ponte, dado);
            Ocupar(false);
            if (!r.Ok)
                return Recusar(r.Mensagem ?? r.Motivo ?? GerenteSetups.MsgNaoGuardou);
            _log.Evento($"calibração: {(alvo.Ponte ? "ganho em ponte" : "ganho")} do {alvo.Modulo.Nome} ×{ganho.ToString(CultureInfo.InvariantCulture)} medido");
            bool bluetooth;
            lock (_trava)
            {
                if (_geracao != geracao)
                    return ResultadoCalibracao.Falha(MsgCaiu);
                _ganho = ganho;
                _mensagem = null;
                bluetooth = _medirBluetooth || BluetoothMedido is null;
                if (bluetooth)
                    _etapa = EtapaCalibracao.Bluetooth;
            }
            Notificar();
            return bluetooth ? ResultadoCalibracao.Feito() : await TerminarAsync(geracao);
        }
        finally
        {
            _vez.Release();
        }
    }

    /// <summary>
    /// Passo 5: com o celular a 100 % e o som saindo pelo Bluetooth, o limitador dos canais vai a min(0; teto),
    /// o Player pausa e o celular toca o seno de f a 0 dBFS, por até 30 s.
    /// </summary>
    /// <param name="saindoPeloBluetooth">O som do celular sai pelo Bluetooth (o <c>IAudioDoCelular</c> da tela).</param>
    public async Task<ResultadoCalibracao> TocarBluetoothAsync(bool? saindoPeloBluetooth)
    {
        await _vez.WaitAsync();
        try
        {
            long geracao;
            AlvoCalibracao alvo;
            lock (_trava)
            {
                if (_etapa != EtapaCalibracao.Bluetooth || _alvo is null)
                    return ResultadoCalibracao.Falha(_etapa == EtapaCalibracao.Fechada ? MsgFechada : MsgForaDaEtapa);
                geracao = _geracao;
                alvo = _alvo;
            }
            if (saindoPeloBluetooth != true)
                return Recusar(MsgForaDoBluetooth);

            // O limitador em min(0; teto): agora com o ganho medido, o canal pode estar cercado.
            Dictionary<int, decimal> limites;
            try
            {
                var porCanal = _setups.LimitesEmUso.FirstOrDefault(l => l.Saida.Mestre == alvo.Saida.Mestre)?.PorCanal ?? [];
                limites = alvo.Saida.Canais.ToDictionary(c => c,
                    c => porCanal.FirstOrDefault(p => p.Canal == c) is { } teto ? Math.Min(LimiteFisico.MaximoLimitador, teto.TetoCerca) : LimiteFisico.MaximoLimitador);
            }
            catch (Exception e)
            {
                _log.Evento($"calibração: falha ao conferir o limite ({e.GetType().Name})");
                return Recusar(Cerca.MsgFalhou);
            }

            Ocupar(true);
            var falha = await EscreverAsync(limites.Select(kv => (Cerca.CodigoLimitador, (int?)kv.Key, kv.Value)).ToArray());
            Ocupar(false);
            if (!Atual(geracao))
                return ResultadoCalibracao.Falha(MsgCaiu);
            if (falha is not null)
                return Recusar(falha);
            lock (_trava)
                _limitadorBluetooth = limites.Values.Min();

            if (_player is { } player)
            {
                lock (_trava)
                    _retomarPlayer |= player.Estado == EstadoPlayer.Tocando;
                await player.PausarAsync();
            }
            var r = await TocarCelularAsync(geracao);
            if (!r.Ok)
                return r;
            lock (_trava)
            {
                if (_geracao != geracao)
                    return ResultadoCalibracao.Falha(MsgCaiu);
                _etapa = EtapaCalibracao.DigitarBluetooth;
                _mensagem = null;
            }
            Notificar();
            return ResultadoCalibracao.Feito();
        }
        finally
        {
            _vez.Release();
        }
    }

    /// <summary>
    /// Passo 6: os volts do tom do celular. O tom para; o nível = 20·log10(V / (2,0 × ganho)), com uma casa. A menos
    /// de 0,5 dB do limitador, "O limitador segurou o tom" e o passo se repete. O nível vai para o Setup, como
    /// medido, e a calibração termina.
    /// </summary>
    public async Task<ResultadoCalibracao> InformarBluetoothAsync(decimal volts)
    {
        await _vez.WaitAsync();
        try
        {
            long geracao;
            decimal ganho;
            decimal limitador;
            double atenuacao;
            lock (_trava)
            {
                if (_etapa != EtapaCalibracao.DigitarBluetooth || _ganho is not { } g)
                    return ResultadoCalibracao.Falha(_etapa == EtapaCalibracao.Fechada ? MsgFechada : MsgForaDaEtapa);
                geracao = _geracao;
                ganho = g;
                limitador = _limitadorBluetooth;
                atenuacao = _atenuacao;
            }
            if (volts < VoltsMinimo || volts > VoltsMaximo)
                return Recusar(MsgVolts);

            await PararCelularAsync();
            // O limitador vê o nível na saída; o Setup guarda o da entrada (o corte em f somado de volta).
            var naSaida = NivelDe(volts, ganho);
            var nivel = NivelDe(volts, ganho, atenuacao);
            if (naSaida > limitador - FolgaLimitador)
            {
                _log.Evento($"calibração: o Bluetooth deu {naSaida.ToString("0.0", CultureInfo.InvariantCulture)} dB, perto do limitador ({limitador.ToString("0.0", CultureInfo.InvariantCulture)} dB)");
                return Recusar(MsgLimitadorSegurou);
            }

            Ocupar(true);
            var r = await _setups.DefinirNivelBluetoothAsync(_idSetup ?? "", new DadoTecnico(nivel, OrigemDado.Medido, Fonte(_relogio.GetLocalNow())));
            Ocupar(false);
            if (!r.Ok)
                return Recusar(r.Mensagem ?? r.Motivo ?? GerenteSetups.MsgNaoGuardou);
            _log.Evento($"calibração: nível do Bluetooth {nivel.ToString("0.0", CultureInfo.InvariantCulture)} dB medido");
            lock (_trava)
            {
                if (_geracao != geracao)
                    return ResultadoCalibracao.Falha(MsgCaiu);
                _nivel = nivel;
            }
            return await TerminarAsync(geracao);
        }
        finally
        {
            _vez.Release();
        }
    }

    /// <summary>
    /// Cancelar: antes da operação (passos 1 e 2), só fecha. Depois, para o gerador e o tom e volta ao som de antes;
    /// o que já foi gravado na ficha fica.
    /// </summary>
    public async Task<ResultadoCalibracao> CancelarAsync()
    {
        bool operacao;
        lock (_trava)
        {
            if (_etapa == EtapaCalibracao.Fechada)
                return ResultadoCalibracao.Falha(MsgFechada);
            operacao = _operacao;
            if (!operacao)
            {
                _geracao++;
                _etapa = EtapaCalibracao.Fechada;
                _alvo = null;
                _mensagem = null;
            }
        }
        if (!operacao)
        {
            Notificar();
            return ResultadoCalibracao.Feito();
        }
        // O passo que já anda termina antes da volta (o tom para já, sem esperar a vez).
        await PararGeradorAsync();
        await PararCelularAsync();
        await _vez.WaitAsync();
        try
        {
            long geracao;
            lock (_trava)
            {
                if (_etapa == EtapaCalibracao.Fechada)
                    return ResultadoCalibracao.Falha(MsgFechada);
                if (_etapa == EtapaCalibracao.Fim || !_operacao)
                    return Fechar(_geracao, null);
                geracao = _geracao;
            }
            _log.Evento("calibração cancelada; voltando ao som de antes");
            await PararGeradorAsync();
            await PararCelularAsync();
            var volta = await VoltarAsync();
            await RetomarPlayerAsync();
            return Fechar(geracao, volta.Ok ? MsgCancelada : MsgNaoVoltou(volta.Motivo ?? "falhou"), volta.Ok);
        }
        finally
        {
            _vez.Release();
        }
    }

    /// <summary>Fim (ou uma calibração que nem começou): fecha a calibração, sem mexer no aparelho.</summary>
    public ResultadoCalibracao Concluir()
    {
        lock (_trava)
        {
            if (_etapa is not (EtapaCalibracao.Fim or EtapaCalibracao.Desligar or EtapaCalibracao.Multimetro) || _ocupado)
                return ResultadoCalibracao.Falha(_etapa == EtapaCalibracao.Fechada ? MsgFechada : MsgForaDaEtapa);
            _geracao++;
            _etapa = EtapaCalibracao.Fechada;
            _alvo = null;
            _mensagem = null;
        }
        Notificar();
        return ResultadoCalibracao.Feito();
    }

    // ------------------------------------------------------------------ apoio

    /// <summary>Passo 7: para tudo, volta ao som de antes (com o limitador no teto, como na 13a) e mostra o que foi gravado.</summary>
    private async Task<ResultadoCalibracao> TerminarAsync(long geracao)
    {
        await PararGeradorAsync();
        await PararCelularAsync();
        lock (_trava)
        {
            if (_geracao != geracao)
                return ResultadoCalibracao.Falha(MsgCaiu);
            _etapa = EtapaCalibracao.Fim;
            _ocupado = true;
        }
        Notificar();
        var volta = await VoltarAsync();
        await RetomarPlayerAsync();
        string frase;
        lock (_trava)
        {
            _ocupado = false;
            _operacao = false;
            var ganho = _ganho ?? 0m;
            frase = !volta.Ok ? MsgNaoVoltou(volta.Motivo ?? "falhou")
                : _nivel is { } nivel ? MsgGravados(ganho, nivel)
                : MsgGanhoGravado(ganho);
            if (_geracao == geracao)
                _mensagem = frase;
        }
        _log.Evento($"calibração: {frase}");
        Notificar();
        return volta.Ok ? ResultadoCalibracao.Feito(frase) : ResultadoCalibracao.Falha(frase);
    }

    /// <summary>O Player que tocava antes do tom do celular volta a tocar quando a calibração termina (fim ou cancelar).</summary>
    private async Task RetomarPlayerAsync()
    {
        bool retomar;
        lock (_trava)
        {
            retomar = _retomarPlayer;
            _retomarPlayer = false;
        }
        if (retomar && _player is { Estado: EstadoPlayer.Pausado } player)
            await player.TocarPausarAsync();
    }

    /// <summary>Volta ao som de antes (o desfazer da cópia). Sem a sessão, a marca fica para a próxima conexão.</summary>
    private async Task<ResultadoCopia> VoltarAsync()
    {
        try
        {
            return await _copia.DesfazerAsync();
        }
        catch (Exception e)
        {
            _log.Evento($"calibração: falha ao voltar ({e.GetType().Name})");
            return ResultadoCopia.Falha($"Erro inesperado ({e.GetType().Name}).");
        }
    }

    /// <summary>Escreve na ordem, só o que difere do espelho, pelo escritor (com a marca da cópia). O motivo da primeira falha, ou null.</summary>
    private async Task<string?> EscreverAsync(IReadOnlyList<(string Codigo, int? Indice, decimal Valor)> escritas)
    {
        foreach (var (codigo, indice, valor) in escritas)
        {
            var p = Catalogo.ParametroDe(codigo)!;
            if (_estado.Valor(codigo, indice)?.Numero == valor)
                continue;
            var r = await _escritor.EscreverAsync(p, indice, valor, espelhar: false, daCopia: true);
            if (!r.Ok)
                return r.Motivo ?? Sessao.MotivoFalhaEscrita;
            if (r.Diferente)
                return $"o aparelho ficou em {r.Valor?.Exibicao ?? "outro valor"} em {p.Nome} ({p.Onde(indice)}).";
        }
        return null;
    }

    private async Task<ResultadoCalibracao> TocarGeradorAsync(long geracao)
    {
        int f;
        lock (_trava)
            f = _frequencia;
        var r = await _gerador.TocarTomAsync(f, 0m);
        lock (_trava)
        {
            if (_geracao != geracao)
                return ResultadoCalibracao.Falha(MsgCaiu);
            _mensagem = r.Ok ? null : r.Mensagem ?? r.Motivo;
        }
        Notificar();
        return r.Ok ? ResultadoCalibracao.Feito() : ResultadoCalibracao.Falha(r.Mensagem ?? r.Motivo ?? GeradorSinal.MsgNaoLigou);
    }

    private async Task<ResultadoCalibracao> TocarCelularAsync(long geracao)
    {
        int f;
        lock (_trava)
            f = _frequencia;
        bool ok;
        try
        {
            ok = await _tom.TocarAsync(f, DuracaoTomCelular);
        }
        catch (Exception e)
        {
            _log.Evento($"calibração: o tom do celular falhou ({e.GetType().Name})");
            ok = false;
        }
        if (!Atual(geracao))
        {
            // Caiu ou cancelou com o tom saindo: ele não pode seguir a 0 dBFS por 30 s.
            await PararCelularAsync();
            return ResultadoCalibracao.Falha(MsgCaiu);
        }
        if (!ok)
            return Recusar(MsgTomCelular);
        _log.Evento($"calibração: o celular toca {f} Hz a 0 dBFS");
        return ResultadoCalibracao.Feito();
    }

    /// <summary>Para o gerador, se ele pode estar ligado (o Parar nunca fica travado).</summary>
    private async Task PararGeradorAsync()
    {
        if (!_gerador.TalvezLigado && _gerador.Estado == EstadoGerador.Desligado)
            return;
        try
        {
            await _gerador.PararAsync();
        }
        catch (Exception e)
        {
            _log.Evento($"calibração: falha ao parar o gerador ({e.GetType().Name})");
        }
    }

    private async Task PararCelularAsync()
    {
        try
        {
            await _tom.PararAsync();
        }
        catch (Exception e)
        {
            _log.Evento($"calibração: falha ao parar o tom do celular ({e.GetType().Name})");
        }
    }

    private bool Atual(long geracao)
    {
        lock (_trava)
            return _geracao == geracao;
    }

    private void Ocupar(bool ocupado)
    {
        lock (_trava)
            _ocupado = ocupado;
        Notificar();
    }

    /// <summary>A recusa de um passo: a frase fica na tela, e a etapa não muda.</summary>
    private ResultadoCalibracao Recusar(string motivo)
    {
        lock (_trava)
            _mensagem = motivo;
        Notificar();
        return ResultadoCalibracao.Falha(motivo);
    }

    /// <summary>Fecha a calibração (da mesma geração) com a frase.</summary>
    private ResultadoCalibracao Fechar(long geracao, string? frase, bool ok = false)
    {
        lock (_trava)
        {
            if (_geracao == geracao)
            {
                _geracao++;
                _etapa = EtapaCalibracao.Fechada;
                _alvo = null;
                _operacao = false;
                _ocupado = false;
                _mensagem = frase;
            }
        }
        Notificar();
        return ok ? ResultadoCalibracao.Feito(frase) : frase is null ? ResultadoCalibracao.Feito() : ResultadoCalibracao.Falha(frase);
    }

    /// <summary>
    /// A conexão caiu: fecha a calibração. Com a operação começada (e antes do fim), o tom do celular para e a marca
    /// fica para a próxima conexão ("aplicado pela metade"); o gerador segue a regra 1 sozinho.
    /// </summary>
    private void AoMudarSessao()
    {
        if (SessaoAberta)
            return;
        bool operacao;
        lock (_trava)
        {
            if (_etapa is EtapaCalibracao.Fechada or EtapaCalibracao.Fim)
                return;
            operacao = _operacao;
            _geracao++;
            _etapa = EtapaCalibracao.Fechada;
            _alvo = null;
            _operacao = false;
            _ocupado = false;
            _mensagem = operacao ? MsgCaiu : MsgSemConexao;
        }
        if (operacao)
        {
            _copia.Interromper();
            _ = PararCelularAsync();
        }
        _log.Evento(operacao ? "calibração interrompida: a conexão caiu; aplicado pela metade" : "calibração fechada: a conexão caiu");
        Notificar();
    }

    private void Notificar()
    {
        try
        {
            Mudou?.Invoke();
        }
        catch (Exception e)
        {
            _log.Evento($"falha no aviso da calibração ({e.GetType().Name})");
        }
    }

    public void Dispose()
    {
        _sessao.Mudou -= AoMudarSessao;
        _ = PararCelularAsync();
        _vez.Dispose();
    }
}
