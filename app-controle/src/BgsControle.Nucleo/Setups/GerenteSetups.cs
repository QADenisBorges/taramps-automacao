using System.Globalization;
using System.Text;
using System.Text.Json;
using BgsControle.Nucleo.Escrita;
using BgsControle.Nucleo.Herdado;
using BgsControle.Nucleo.Leitura;
using BgsControle.Nucleo.Memorias;
using BgsControle.Nucleo.Player;
using BgsControle.Nucleo.Presets;
using BgsControle.Nucleo.Registro;
using BgsControle.Nucleo.Seguranca;
using BgsControle.Nucleo.Uso;

namespace BgsControle.Nucleo.Setups;

/// <summary>O uso das memórias de um Setup: as usadas por último (a primeira é a ativa) e os 3 botões fixos.</summary>
public sealed record UsoDoSetup(IReadOnlyList<string> Recentes, IReadOnlyList<string?> Botoes)
{
    public static UsoDoSetup Vazio { get; } = new([], [null, null, null]);
}

/// <summary>O que fica em <c>setups.json</c>: os Setups, as memórias do celular, o último Setup usado e o uso de cada Setup.</summary>
public sealed record DadosSetups(
    IReadOnlyList<Setup> Setups,
    IReadOnlyList<MemoriaCelular> Memorias,
    string? UltimoSetup,
    IReadOnlyDictionary<string, UsoDoSetup> Uso)
{
    public UsoDoSetup UsoDe(string idSetup) => Uso.GetValueOrDefault(idSetup) ?? UsoDoSetup.Vazio;
}

/// <summary>
/// Os Setups e as memórias do celular (CAP-4, CAP-16, failure-modes regra 5). Guarda tudo em
/// <c>setups.json</c> (formato 2; lê também o 1) e a marca do mudo da dúvida em <c>setup-mudo.json</c>;
/// sem arquivo (ou com ele estragado), começa com o herdado: o Setup "Casa 2.1" (com os equipamentos e
/// o Link 3–4 ligado) e a memória FILME.
/// <list type="bullet">
/// <item><b>Conferência:</b> na primeira leitura completa de cada conexão nova (depois da regra 2 do
/// gerador), compara a calibração lida com a do último Setup usado. Bate: "Setup confere", ou "som
/// diferente" se só o caráter difere da memória ativa (sem mutar). Não bate: guarda a marca, muta os
/// 4 canais (<c>100</c> = 1) e fica "Setup em dúvida" até escolher um Setup, criar um ou seguir sem
/// Setup. Uma dúvida interrompida (marca guardada e os 4 canais lidos mutados) volta sem mutar de novo.
/// Quando o Setup confere, o link dele passa a valer no app.</item>
/// <item><b>Aplicar</b> uma memória, <b>escolher</b> um Setup e o <b>Manter a calibração</b> do Salvar
/// passam pela <see cref="CopiaAntes"/> (regra 3): escrevem só o que difere, relêem e conferem.</item>
/// <item>Criar, renomear e apagar Setups e memórias não mandam nada ao aparelho; nem adicionar, editar
/// ou tirar equipamentos (história 8e), que não têm chave.</item>
/// <item>As <b>saídas do app</b> (<see cref="SaidasEmUso"/>) vêm dos equipamentos do Setup em uso e do
/// link (<see cref="Ligacoes.Saidas"/>): um canal, ou um par com link, que é uma saída só.</item>
/// <item>O <b>Salvar</b> leva cada mudança para a camada dona dela (<see cref="Camadas"/>); as 15
/// bandas vão para um preset (<see cref="GerentePresets"/>).</item>
/// <item>Cada memória do celular <b>lembra um preset</b> (as 15 bandas), que volta junto quando ela é
/// aplicada; aplicar um preset com uma memória do celular em uso faz ela lembrar esse preset.</item>
/// <item>As <b>playlists</b> (<see cref="GerentePlaylists"/>) são por memória: apagar a memória (ou o
/// Setup dela) apaga a playlist; criar ou renomear memórias, e carregar, adota as pendentes pelo nome.</item>
/// </list>
/// Uma operação por vez. O ganho e o limiter finais de cada canal são a memória mais o equilíbrio
/// do Setup naquele canal (<see cref="Camadas.Somar"/>).
/// </summary>
public sealed class GerenteSetups
{
    /// <summary>O formato do <c>setups.json</c>: o 2 tem o equilíbrio por canal e os equipamentos (história 8e).</summary>
    public const int Formato = 2;

    /// <summary>O formato da 8c e da 8d: o equilíbrio L/R e sem equipamentos. Ainda se lê.</summary>
    public const int FormatoAntigo = 1;

    /// <summary>O formato do <c>setup-mudo.json</c> (não mudou na 8e).</summary>
    public const int FormatoMudo = 1;

    /// <summary>Os tipos registrados na <see cref="CopiaAntes"/>. O alvo da marca é o id da memória ou do Setup.</summary>
    public const string TipoAplicarMemoria = "aplicar-memoria-celular";

    public const string TipoAplicarSetup = "aplicar-setup";
    public const string TipoManterCalibracao = "manter-calibracao";

    public const int QuantosBotoes = 3;

    public const string MsgDuvida = "Setup em dúvida; escolha na tela.";
    public const string MsgSemSetup = "Escolha um Setup para usar as memórias do celular.";
    public const string MsgSemSetupSalvar = "Escolha um Setup para salvar.";
    public const string MsgNadaParaSalvar = "Nada para salvar.";
    public const string MsgOcupado = "Uma operação de Setup ou de memória já está em andamento.";
    public const string MsgNaoGuardou = "Não consegui guardar no celular.";
    public const string MsgNaoGuardouMarca = "não consegui guardar a marca do mudo no celular";
    public const string MsgSetupNaoExiste = "O Setup não existe mais.";
    public const string MsgMemoriaNaoExiste = "A memória não existe mais.";
    public const string MsgNaoExisteTerminar = "Não existe mais; use Voltar à cópia.";
    public const string MsgMemoriaEmUso = "Esta memória está em uso.";
    public const string MsgSetupEmUso = "Este Setup está em uso.";
    public const string MsgSetupHerdado = "O Setup herdado não se apaga.";
    public const string MsgNomeRepetidoSetup = "Já existe um Setup com esse nome.";
    public const string MsgSoNaEscolha = "Isso só se faz na tela de escolha do Setup.";
    public const string MsgOutroSetup = "A memória é de outro Setup.";
    public const string MsgPedeFolha = "Este som não é de uma memória do celular. Toque em Salvar para guardá-lo.";
    public const string MsgEscolha = "Escolha manter ou atualizar a calibração.";
    public const string MsgEscolhaPreset = "Escolha atualizar o preset ou criar um preset novo.";
    public const string MsgPresetSumiu = "O preset lembrado não existe mais; as 15 bandas ficaram como estavam.";
    public const string MsgSemConexao = "Sem conexão com o aparelho.";
    public const string MsgEquilibrioCanal = "O canal vai de CH1 a CH4.";
    public const string MsgEquilibrioFaixa = "Fora da faixa: −12,0 a +12,0 dB";
    public const string MsgCaiu = "A conexão caiu no meio. Ao reconectar, o app mostra o que ficou.";
    public const string MsgSemMudo = "O som não estava mutado.";

    public static string MsgNomeRepetidoMemoria(string setup) => $"Já existe uma memória com esse nome no {setup}.";

    /// <summary>"Nome do preset: Já existe um preset com esse nome.": a recusa diz que é o nome do preset, não o da memória.</summary>
    public static string MsgNomePreset(string motivo) => $"Nome do preset: {motivo}";

    public static string MsgCriada(string nome, string setup) => $"Memória {nome} criada no {setup}.";

    public static string MsgAplicada(string nome) => $"Memória {nome} aplicada.";

    public static string MsgAplicadaDiferente(string nome, int n) => n == 1
        ? $"Memória {nome} aplicada, mas 1 ajuste ficou diferente."
        : $"Memória {nome} aplicada, mas {n} ajustes ficaram diferentes.";

    public static string MsgFicaramDiferentes(int n) => n == 1 ? "1 ajuste ficou diferente." : $"{n} ajustes ficaram diferentes.";

    public static string MsgLinkNaoGuardado(string motivo) => $"O link do Setup não foi guardado: {motivo}";

    public static string MsgSomDiferente(string nome) => $"Som diferente da {nome}";

    public static string MsgSetupEscolhido(string nome) => $"Setup {nome} em uso.";

    public static string MsgSetupCriado(string nome) => $"Setup {nome} criado, ainda sem memórias.";

    public const string MsgSeguiuSemSetup = "Seguindo sem Setup. As memórias do celular ficam travadas até escolher um Setup.";

    public static string MsgContinuaMutado(string motivo) => $"O som continua mutado: {motivo}";

    public static string MsgNaoMutou(string motivo) => $"O som não foi mutado: {motivo}";

    public static string MsgNoLimite(string setup) => $"No limite da faixa com o equilíbrio do {setup}.";

    public static string MsgIncompleta(int n) => n == 1
        ? "A leitura está incompleta (1 ajuste sem valor)."
        : $"A leitura está incompleta ({n} ajustes sem valor).";

    public static string MsgNaoEscritos(int n, string? motivo) => n == 1
        ? $"1 ajuste não foi escrito: {motivo}"
        : $"{n} ajustes não foram escritos: {motivo}";

    public static string MsgRenomeado(string nome) => $"Agora se chama {nome}.";

    public static string MsgSetupApagado(string nome) => $"Setup {nome} apagado.";

    public const string MsgEquipamentoNaoExiste = "O equipamento não existe mais.";

    public static string MsgEquipamentoAdicionado(string nome, string setup) => $"{nome} adicionado ao {setup}.";

    public static string MsgEquipamentoGuardado(string nome) => $"{nome} guardado.";

    public static string MsgEquipamentoTirado(string nome, string setup) => $"{nome} tirado do {setup}.";

    /// <summary>"Ficaram sem ligação: Tweeter 1, Tweeter 2 e Woofer."</summary>
    public static string MsgSemLigacao(IReadOnlyList<string> nomes) => $"Ficaram sem ligação: {Ligacoes.Lista(nomes)}.";

    /// <summary>"Ficaram sem fonte: Módulo A e Módulo B."</summary>
    public static string MsgSemFonte(IReadOnlyList<string> nomes) => $"Ficaram sem fonte: {Ligacoes.Lista(nomes)}.";

    public static string MsgMemoriaApagada(string nome) => $"Memória {nome} apagada.";

    private static readonly Parametro Mute = Catalogo.ParametroDe(Camadas.CodigoMute)!;
    private static readonly CultureInfo Fio = CultureInfo.InvariantCulture;

    private readonly Sessao _sessao;
    private readonly EstadoAparelho _estado;
    private readonly Escritor _escritor;
    private readonly LeituraAutomatica _leitura;
    private readonly CopiaAntes _copia;
    private readonly LinkCanais _link;
    private readonly GerentePresets _presets;
    private readonly GerentePlaylists _playlists;
    private readonly IChaves _chaves;
    private readonly IRepositorioTexto _repositorio;
    private readonly IRepositorioTexto _repositorioMudo;
    private readonly LogBle _log;
    private readonly TimeProvider _relogio;

    private readonly object _trava = new();
    private readonly SemaphoreSlim _operacao = new(1, 1);
    private readonly SemaphoreSlim _gravacao = new(1, 1);

    private DadosSetups _dados = Herdados();
    private MarcaMudo? _marca;
    private EstadoConferencia _situacao = EstadoConferencia.Nenhuma;
    private string? _setupEmUso;
    private SomEmUso _emUso = SomEmUso.Nenhum;
    private string? _referencia;
    private string? _ultimoPreset;
    private IReadOnlyList<DiferencaCalibracao> _diferencas = [];
    private string? _motivoMudo;
    private string? _aviso;
    private Equilibrio? _pendente;
    private Antes? _antes;
    private bool _aberta;
    private bool _conferiu;
    private bool _conferindo;

    public GerenteSetups(
        Sessao sessao,
        EstadoAparelho estado,
        Escritor escritor,
        LeituraAutomatica leitura,
        CopiaAntes copia,
        LinkCanais link,
        GerenteMemorias memorias,
        GerentePresets presets,
        GerentePlaylists playlists,
        IChaves chaves,
        IRepositorioTexto repositorio,
        IRepositorioTexto repositorioMudo,
        LogBle log,
        TimeProvider? relogio = null)
    {
        _sessao = sessao;
        _estado = estado;
        _escritor = escritor;
        _leitura = leitura;
        _copia = copia;
        _link = link;
        _presets = presets;
        _playlists = playlists;
        _chaves = chaves;
        _repositorio = repositorio;
        _repositorioMudo = repositorioMudo;
        _log = log;
        _relogio = relogio ?? TimeProvider.System;
        _aberta = SessaoAberta;

        _sessao.Mudou += AoMudarSessao;
        // Depois do espelho (criado antes): quando este aviso chega, o espelho já é a leitura nova.
        _leitura.Terminou += AoTerminarLeitura;
        memorias.Carregou += AoCarregar;
        memorias.Bloqueio = () => EmEscolha ? MsgDuvida : null;
        // O preset lembrado: o apagar pergunta quem lembra; o aplicar faz a memória em uso lembrar.
        presets.Lembrados = id => Dados.Memorias.Count(m => m.IdPreset == id);
        presets.AoAplicar = AoAplicarPresetAsync;
        presets.Mudou += Notificar; // um preset renomeado muda o "Preset: Cinema" das memórias
        _link.Mudou += _ => Notificar(); // as saídas do app dependem do link

        // Ao reconectar com a marca de uma destas operações: pela metade (Terminar ou Voltar à cópia).
        _copia.Registrar(TipoAplicarMemoria,
            _ => Task.FromResult(Retomada.PelaMetade),
            async op => ParaCopia(await AplicarMemoriaComTravaAsync(op.Alvo, TipoAplicarMemoria, refazendo: true)));
        _copia.Registrar(TipoManterCalibracao,
            _ => Task.FromResult(Retomada.PelaMetade),
            async op => ParaCopia(await AplicarMemoriaComTravaAsync(op.Alvo, TipoManterCalibracao, refazendo: true)));
        _copia.Registrar(TipoAplicarSetup,
            _ => Task.FromResult(Retomada.PelaMetade),
            async op => ParaCopia(await EscolherInternoAsync(op.Alvo, refazendo: true)));

        Carregada = Task.Run(CarregarAsync);
    }

    /// <summary>Completa quando <c>setups.json</c> e <c>setup-mudo.json</c> foram lidos.</summary>
    public Task Carregada { get; }

    /// <summary>Os Setups, as memórias, a conferência ou o som em uso mudaram. Pode chegar em qualquer thread.</summary>
    public event Action? Mudou;

    // ------------------------------------------------------------------ o que está guardado

    public DadosSetups Dados
    {
        get
        {
            lock (_trava)
                return _dados;
        }
    }

    /// <summary>Os Setups, na ordem em que foram criados (o herdado primeiro).</summary>
    public IReadOnlyList<Setup> Setups => Dados.Setups;

    public Setup? Setup(string? id) => id is null ? null : Dados.Setups.FirstOrDefault(s => s.Id == id);

    public MemoriaCelular? Memoria(string? id) => id is null ? null : Dados.Memorias.FirstOrDefault(m => m.Id == id);

    /// <summary>As memórias do Setup, na ordem em que foram criadas.</summary>
    public IReadOnlyList<MemoriaCelular> MemoriasDe(string? idSetup) =>
        idSetup is null ? [] : Dados.Memorias.Where(m => m.IdSetup == idSetup).ToArray();

    /// <summary>O último Setup usado (a referência da próxima conferência).</summary>
    public string? UltimoSetup => Dados.UltimoSetup;

    /// <summary>A memória ativa do Setup: a última memória do celular usada nele, ou null.</summary>
    public MemoriaCelular? Ativa(string? idSetup)
    {
        var dados = Dados;
        return idSetup is null ? null : AtivaEm(dados, idSetup);
    }

    /// <summary>Os 3 botões do Setup, cada um com a memória ou null (vaga vazia). Não mudam de lugar.</summary>
    public IReadOnlyList<MemoriaCelular?> Botoes(string? idSetup)
    {
        var dados = Dados;
        if (idSetup is null)
            return [null, null, null];
        return dados.UsoDe(idSetup).Botoes.Select(id => dados.Memorias.FirstOrDefault(m => m.Id == id)).ToArray();
    }

    /// <summary>O preset que a memória lembra, ou null (nenhum, ou ele não existe mais).</summary>
    public Preset? PresetDe(MemoriaCelular? memoria) => _presets.Obter(memoria?.IdPreset);

    // ------------------------------------------------------------------ o que está acontecendo

    public EstadoConferencia Estado
    {
        get
        {
            lock (_trava)
                return _situacao;
        }
    }

    /// <summary>O Setup em uso, ou null (sem conferir ainda, em dúvida, ou seguindo sem Setup).</summary>
    public Setup? SetupEmUso
    {
        get
        {
            lock (_trava)
                return _setupEmUso is { } id ? _dados.Setups.FirstOrDefault(s => s.Id == id) : null;
        }
    }

    /// <summary>O que o app sabe que está tocando.</summary>
    public SomEmUso EmUso
    {
        get
        {
            lock (_trava)
                return _emUso;
        }
    }

    /// <summary>A memória do celular em uso, ou null.</summary>
    public MemoriaCelular? MemoriaEmUso => EmUso is { Tipo: TipoSomEmUso.MemoriaCelular } uso ? Memoria(uso.IdMemoria) : null;

    /// <summary>
    /// O id do preset em uso: o lembrado pela memória do celular em uso, ou o último aplicado nesta
    /// conexão; nenhum depois de carregar uma memória do aparelho ou de uma queda.
    /// </summary>
    public string? IdPresetEmUso
    {
        get
        {
            if (MemoriaEmUso is { } memoria)
                return memoria.IdPreset;
            lock (_trava)
                return _ultimoPreset;
        }
    }

    /// <summary>O preset em uso (<see cref="IdPresetEmUso"/>), ou null (nenhum, ou ele não existe mais).</summary>
    public Preset? PresetEmUso => _presets.Obter(IdPresetEmUso);

    /// <summary>Na dúvida ou na troca: o Setup que era a referência (o "Casa 2.1" da frase), ou null.</summary>
    public Setup? Referencia
    {
        get
        {
            lock (_trava)
                return _referencia is { } id ? _dados.Setups.FirstOrDefault(s => s.Id == id) : null;
        }
    }

    /// <summary>As diferenças de calibração da dúvida (ou da troca), para "O que não bate".</summary>
    public IReadOnlyList<DiferencaCalibracao> Diferencas
    {
        get
        {
            lock (_trava)
                return _diferencas;
        }
    }

    /// <summary>A marca do mudo guardada no celular, ou null.</summary>
    public MarcaMudo? Marca
    {
        get
        {
            lock (_trava)
                return _marca;
        }
    }

    /// <summary>O mudo da dúvida está no aparelho (a marca existe).</summary>
    public bool Mutado => Marca is not null;

    /// <summary>Por que o mudo da dúvida não foi posto ("O som não foi mutado: chave desligada"), ou null.</summary>
    public string? MotivoMudo
    {
        get
        {
            lock (_trava)
                return _motivoMudo;
        }
    }

    /// <summary>A frase da última operação, ou null.</summary>
    public string? Aviso
    {
        get
        {
            lock (_trava)
                return _aviso;
        }
    }

    /// <summary>Uma operação anda (ou a conferência).</summary>
    public bool Ocupado
    {
        get
        {
            lock (_trava)
                return _conferindo || _operacao.CurrentCount == 0;
        }
    }

    /// <summary>A conferência desta conexão terminou.</summary>
    public bool Conferido
    {
        get
        {
            lock (_trava)
                return _conferiu && !_conferindo;
        }
    }

    /// <summary>Na tela de escolha: Setup em dúvida ou trocando de Setup.</summary>
    public bool EmEscolha => Estado is EstadoConferencia.Duvida or EstadoConferencia.Trocando;

    /// <summary>
    /// Por que a IA não pode mandar nada agora, ou null: a dúvida, a troca, ou a conferência desta
    /// conexão que ainda não terminou (nada toca antes de conferir o Setup).
    /// </summary>
    public string? MotivoBloqueioIa
    {
        get
        {
            lock (_trava)
            {
                if (_situacao is EstadoConferencia.Duvida or EstadoConferencia.Trocando)
                    return MsgDuvida;
                if (_aberta && (!_conferiu || _conferindo))
                    return MsgDuvida;
                return null;
            }
        }
    }

    /// <summary>O equilíbrio mexido e ainda não salvo (do Setup em uso), ou null.</summary>
    public Equilibrio? EquilibrioPendente
    {
        get
        {
            lock (_trava)
                return _pendente;
        }
    }

    /// <summary>O equilíbrio que vale agora no Setup em uso: o pendente, ou o guardado; zero sem Setup.</summary>
    public Equilibrio EquilibrioEfetivo
    {
        get
        {
            lock (_trava)
                return _pendente ?? (_setupEmUso is { } id ? _dados.Setups.FirstOrDefault(s => s.Id == id)?.Equilibrio : null) ?? Equilibrio.Zero;
        }
    }

    // ------------------------------------------------------------------ saídas do app (história 8e)

    /// <summary>
    /// As saídas do app no Setup em uso, com o link que vale agora no app: cada par com link é uma
    /// saída só. Sem Setup em uso, CH1 a CH4 (também com os pares do link).
    /// </summary>
    public IReadOnlyList<SaidaApp> SaidasEmUso =>
        Ligacoes.Saidas(SetupEmUso?.Equipamentos ?? [], _link.Ligado(ParCanais.Saidas12), _link.Ligado(ParCanais.Saidas34));

    /// <summary>As saídas de um Setup guardado, com o link dele (vazio se o Setup não existe).</summary>
    public IReadOnlyList<SaidaApp> Saidas(string? idSetup) =>
        Setup(idSetup) is { } setup ? Ligacoes.Saidas(setup.Equipamentos, setup.Link12, setup.Link34) : [];

    /// <summary>A saída do app em uso que tem o canal (0 a 3), ou null.</summary>
    public SaidaApp? SaidaDe(int canal) => SaidasEmUso.FirstOrDefault(s => s.Canais.Contains(canal));

    /// <summary>O equipamento do Setup, ou null.</summary>
    public Equipamento? Equipamento(string? idSetup, string? idEquipamento) =>
        idEquipamento is null ? null : Setup(idSetup)?.Equipamentos.FirstOrDefault(e => e.Id == idEquipamento);

    /// <summary>
    /// A função acústica sugerida para o equipamento (que pode ainda não estar guardado) no Setup:
    /// pela categoria, senão pelo corte guardado na calibração do Setup (não o do aparelho) no primeiro
    /// canal que o alimenta. Só para quem toca (grupo 1 e alto-falante passivo); os outros, null.
    /// </summary>
    public static FuncaoAcustica? FuncaoSugerida(Setup? setup, Equipamento equipamento)
    {
        if (!equipamento.Toca)
            return null;
        var equipamentos = setup?.Equipamentos ?? [];
        var lista = equipamentos.Any(e => e.Id == equipamento.Id)
            ? equipamentos.Select(e => e.Id == equipamento.Id ? equipamento : e).ToArray()
            : [.. equipamentos, equipamento];
        var canais = Ligacoes.CanaisQueAlimentam(lista, equipamento.Id);
        return Ligacoes.SugerirFuncao(equipamento.Categoria, setup is not null && canais.Count > 0 ? Corte(setup, canais[0]) : null);
    }

    /// <summary>A função que vale: a escolhida pelo usuário, ou a sugerida. Só para quem toca; os outros, null.</summary>
    public FuncaoAcustica? Funcao(string? idSetup, Equipamento equipamento) =>
        !equipamento.Toca ? null : equipamento.Funcao ?? FuncaoSugerida(Setup(idSetup), equipamento);

    /// <summary>
    /// O corte guardado na calibração do Setup para o canal (0 a 3): a frequência de cada filtro, ou
    /// null com o filtro em OFF (ou sem o valor guardado).
    /// </summary>
    public static CorteCanal Corte(Setup setup, int canal)
    {
        decimal? Numero(string codigo) =>
            setup.Calibracao.FirstOrDefault(v => v.Codigo == codigo && v.Indice == canal) is { } v
                ? Catalogo.ParametroDe(codigo)?.Decodificar(v.NoFio).Valor?.Numero
                : null;
        decimal? Frequencia(string tipo, string frequencia) => Numero(tipo) is { } t && t != 0 ? Numero(frequencia) : null;
        return new CorteCanal(Frequencia("042", "040"), Frequencia("043", "041"));
    }

    // ------------------------------------------------------------------ nomes

    /// <summary>A regra dos presets: de 1 a 24 letras, números, espaço, <c>_</c> ou <c>-</c> (null = não vale).</summary>
    public static string? NomeValido(string? digitado) => GerentePresets.NomeValido(digitado);

    // ------------------------------------------------------------------ conferência

    private void AoMudarSessao()
    {
        var aberta = SessaoAberta;
        bool caiu;
        lock (_trava)
        {
            caiu = _aberta && !aberta;
            _aberta = aberta;
            if (!aberta)
                _conferiu = false;
            if (caiu)
            {
                _situacao = EstadoConferencia.Nenhuma;
                _setupEmUso = null; // a conferência da conexão nova decide de novo
                _emUso = SomEmUso.Nenhum;
                _diferencas = [];
                _referencia = null;
                _ultimoPreset = null;
                _pendente = null;
                _antes = null;
                _motivoMudo = null;
            }
        }
        if (caiu)
            Notificar();
    }

    private void AoTerminarLeitura(Retrato retrato)
    {
        if (!retrato.Completo || !SessaoAberta)
            return;
        lock (_trava)
        {
            if (_conferiu)
                return;
            _conferiu = true;
            _conferindo = true;
        }
        Notificar();
        _ = Task.Run(ConferirSeguroAsync);
    }

    private async Task ConferirSeguroAsync()
    {
        var conferiu = false;
        try
        {
            conferiu = await ConferirAsync();
        }
        catch (Exception e)
        {
            _log.Evento($"setup: falha na conferência ({e.GetType().Name})");
        }
        finally
        {
            lock (_trava)
            {
                _conferindo = false;
                // Não conferiu: a próxima leitura completa desta conexão tenta de novo (a IA segue bloqueada).
                if (!conferiu)
                    _conferiu = false;
            }
            Notificar();
        }
    }

    /// <summary>A conferência da regra 5, na primeira leitura completa da conexão. False = não conferiu (sem leitura).</summary>
    private async Task<bool> ConferirAsync()
    {
        await Carregada;
        await _operacao.WaitAsync();
        try
        {
            if (!SessaoAberta || !_estado.Pronto)
                return false;
            var espelho = _estado.Copiar();
            var dados = Dados;
            var referencia = ReferenciaEm(dados);

            // Dúvida interrompida: a marca guardada e os 4 canais lidos mutados.
            if (Marca is { } marca)
            {
                if (TodosMutados(espelho))
                {
                    var alvo = dados.Setups.FirstOrDefault(s => s.Id == marca.IdSetup) ?? referencia;
                    var diferencas = alvo is null ? [] : DiferencasDeCalibracao(alvo, espelho);
                    lock (_trava)
                    {
                        _situacao = EstadoConferencia.Duvida;
                        _referencia = alvo?.Id;
                        _diferencas = diferencas;
                        _setupEmUso = null;
                        _emUso = SomEmUso.SomDoAparelho;
                        _motivoMudo = null;
                        _antes = null;
                    }
                    _log.Evento("setup: dúvida interrompida; o aparelho continua mutado");
                    return true;
                }
                // O mudo saiu por fora (o app da Taramps): a marca não vale mais.
                _log.Evento("setup: a marca do mudo ficou, mas o aparelho não está mutado; confere normal");
                await GravarMarcaAsync(null);
            }

            if (referencia is null)
            {
                lock (_trava)
                {
                    _situacao = EstadoConferencia.Nenhuma;
                    _setupEmUso = null;
                    _emUso = SomEmUso.SomDoAparelho;
                }
                return true;
            }

            var lista = DiferencasDeCalibracao(referencia, espelho);
            if (lista.Count == 0)
            {
                var ativa = AtivaEm(dados, referencia.Id);
                var situacao = EstadoConferencia.Confere;
                var emUso = SomEmUso.Nenhum;
                if (ativa is not null)
                {
                    var bate = Camadas.Diferentes(Camadas.Compor(ativa.Carater, referencia.Equilibrio), espelho).Count == 0;
                    situacao = bate ? EstadoConferencia.Confere : EstadoConferencia.SomDiferente;
                    emUso = bate ? SomEmUso.DoCelular(ativa.Id) : SomEmUso.SomDoAparelho;
                }
                lock (_trava)
                {
                    _situacao = situacao;
                    _setupEmUso = referencia.Id;
                    _emUso = emUso;
                    _diferencas = [];
                    _referencia = null;
                    _motivoMudo = null;
                }
                _log.Evento(situacao == EstadoConferencia.Confere
                    ? $"setup: {referencia.Nome} confere"
                    : $"setup: {referencia.Nome} confere; o caráter difere da {ativa!.Nome}");
                // O Setup em uso manda no link (as saídas do app vêm dele). Não manda nada ao aparelho.
                if (await _link.DefinirEstadoAsync(referencia.Link12, referencia.Link34) is { } motivoLink)
                    _log.Evento($"setup: {MsgLinkNaoGuardado(motivoLink)}");
                return true;
            }

            // A calibração não bate: muta (como o app, não como a tela nem a IA) e pede a escolha.
            string? motivo;
            using (Origem.Como(OrigemComando.App))
                motivo = await MutarAsync(referencia.Id, espelho);
            lock (_trava)
            {
                _situacao = EstadoConferencia.Duvida;
                _referencia = referencia.Id;
                _diferencas = lista;
                _setupEmUso = null;
                _emUso = SomEmUso.SomDoAparelho;
                _motivoMudo = motivo is null ? null : MsgNaoMutou(motivo);
                _antes = null;
            }
            _log.Evento($"setup: em dúvida ({lista.Count} diferenças de calibração com o {referencia.Nome})"
                        + (motivo is null ? "; mutado" : $"; não mutou ({motivo})"));
            return true;
        }
        finally
        {
            _operacao.Release();
        }
    }

    /// <summary>
    /// Guarda a marca (o mute anterior e a referência) e muta os 4 canais. Devolve o motivo se não
    /// mutou (chave, só leitura, marca não guardada, escrita que falhou), ou null.
    /// </summary>
    private async Task<string?> MutarAsync(string? idReferencia, IReadOnlyDictionary<Endereco, Valor> espelho)
    {
        var quadro = Mute.CodificarEscrita(0, 1m);
        if (_sessao.Conferir(quadro) is { } recusa)
        {
            _log.Recusado(quadro, recusa);
            return recusa;
        }
        var anterior = Enumerable.Range(0, Parametro.Canais)
            .Select(c => espelho.TryGetValue(new Endereco(Camadas.CodigoMute, c), out var v) && v.Ligado == true ? "1" : "0")
            .ToArray();
        if (!await GravarMarcaAsync(new MarcaMudo(idReferencia, anterior, _relogio.GetLocalNow())))
            return MsgNaoGuardouMarca;

        string? motivo = null;
        for (var c = 0; c < Parametro.Canais; c++)
        {
            if (espelho.TryGetValue(new Endereco(Camadas.CodigoMute, c), out var v) && v.Ligado == true)
                continue;
            var r = await _escritor.EscreverAsync(Mute, c, 1m, espelhar: false, daCopia: true);
            if (!r.Ok || r.Diferente)
                motivo ??= r.Motivo ?? $"a saída {c + 1} ficou com som";
        }
        return motivo;
    }

    /// <summary>
    /// Desfaz o mudo da dúvida: escreve de volta o mute anterior da marca (só onde difere) e apaga
    /// a marca. Sem marca, nada. Devolve o motivo se não desmutou (a marca fica), ou null.
    /// </summary>
    private async Task<string?> DesmutarAsync()
    {
        if (Marca is not { } marca)
            return null;
        string? motivo = null;
        for (var c = 0; c < Parametro.Canais; c++)
        {
            var antes = c < marca.MuteAnterior.Count && marca.MuteAnterior[c] == "1" ? 1m : 0m;
            if (_estado.Valor(Camadas.CodigoMute, c) is { Numero: { } agora } && (agora != 0 ? 1m : 0m) == antes)
                continue;
            var r = await _escritor.EscreverAsync(Mute, c, antes, espelhar: false, daCopia: true);
            if (!r.Ok || r.Diferente)
                motivo ??= r.Motivo ?? $"a saída {c + 1} não voltou";
        }
        if (motivo is null)
            await GravarMarcaAsync(null);
        else
            _log.Evento($"setup: não desmutou ({motivo}); a marca fica");
        return motivo;
    }

    private static bool TodosMutados(IReadOnlyDictionary<Endereco, Valor> espelho) =>
        Enumerable.Range(0, Parametro.Canais).All(c =>
            espelho.TryGetValue(new Endereco(Camadas.CodigoMute, c), out var v) && v.Ligado == true);

    /// <summary>
    /// A calibração lida contra a do Setup: uma linha por valor diferente (ou sem leitura), pelo nome da
    /// saída do Setup ("Atraso · Sub"); o par com link que difere igual nos dois canais é uma linha só.
    /// </summary>
    private static IReadOnlyList<DiferencaCalibracao> DiferencasDeCalibracao(Setup setup, IReadOnlyDictionary<Endereco, Valor> espelho) =>
        RotulosSaida.Juntar(
                Camadas.Diferentes(setup.Calibracao, espelho)
                    .Select(v => DiferencaPorCanal.De(v.Codigo, v.Indice, Camadas.Exibir(v.Codigo, v.NoFio),
                        Camadas.Exibir(v.Codigo, espelho.GetValueOrDefault(new Endereco(v.Codigo, v.Indice))?.NoFio))),
                Ligacoes.Saidas(setup.Equipamentos, setup.Link12, setup.Link34))
            .Select(l => new DiferencaCalibracao(l.Codigo, l.Indice, l.Rotulo, l.Antes, l.Depois, l.Canal))
            .ToArray();

    // ------------------------------------------------------------------ escolher, criar, seguir, trocar

    /// <summary>
    /// Escolher o Setup (na dúvida ou na troca): cópia antes, escreve a calibração dele e a memória
    /// ativa (com o equilíbrio), relê, põe o link dele, desmuta para o mute anterior e apaga a marca.
    /// Passa pela chave "Aplicar memória do celular".
    /// </summary>
    public Task<ResultadoSetup> EscolherAsync(string idSetup) => EscolherInternoAsync(idSetup, refazendo: false);

    private async Task<ResultadoSetup> EscolherInternoAsync(string idSetup, bool refazendo)
    {
        await Carregada;
        if (!await _operacao.WaitAsync(0))
            return Terminar(ResultadoSetup.Falha(MsgOcupado));
        try
        {
            var setup = Setup(idSetup);
            if (setup is null)
                return Terminar(ResultadoSetup.Falha(refazendo ? MsgNaoExisteTerminar : MsgSetupNaoExiste));
            if (!refazendo)
            {
                if (!EmEscolha)
                    return Terminar(ResultadoSetup.Falha(MsgSoNaEscolha));
                if (!_chaves.Ligada(Chave.AplicarMemoriaCelular))
                    return Terminar(ResultadoSetup.Falha(Sessao.MotivoChaveDesligada));
            }

            var ativa = Ativa(setup.Id);
            var (alvo, sumiu) = Alvo(setup, ativa);
            var op = new OperacaoPendente(TipoAplicarSetup, setup.Id, $"Escolher o Setup {setup.Nome}", setup.Nome);
            var aplicado = await AplicarAlvoAsync(alvo, op, refazendo);
            if (!aplicado.Ok)
                return Terminar(ResultadoSetup.Falha(aplicado.Motivo!));

            var (falhou, link) = await FecharEscolhaAsync(setup, ativa, aplicado.Iniciou);
            if (falhou is not null)
                return Terminar(ResultadoSetup.Falha(MsgContinuaMutado(falhou)));
            var frase = MsgSetupEscolhido(setup.Nome);
            if (aplicado.Restam > 0)
                frase += $" {MsgFicaramDiferentes(aplicado.Restam)}";
            if (sumiu)
                frase += $" {MsgPresetSumiu}";
            if (link is not null)
                frase += $" {MsgLinkNaoGuardado(link)}";
            _log.Evento($"setup: {setup.Nome} escolhido");
            return Terminar(ResultadoSetup.Feito(frase));
        }
        finally
        {
            _operacao.Release();
        }
    }

    /// <summary>
    /// Depois de a calibração (e a memória ativa) estar no aparelho: o link do Setup, o desmutar, a
    /// marca da cópia concluída, o Setup em uso. Devolve o motivo se não desmutou (fica pela metade)
    /// e o motivo se o link do Setup não foi guardado.
    /// </summary>
    private async Task<(string? Desmutar, string? Link)> FecharEscolhaAsync(Setup setup, MemoriaCelular? ativa, bool iniciou)
    {
        var link = await _link.DefinirEstadoAsync(setup.Link12, setup.Link34);
        var desmutar = await DesmutarAsync();
        if (desmutar is not null)
        {
            if (iniciou)
            {
                if (SessaoAberta)
                    _copia.MarcarPelaMetade();
                else
                    _copia.Interromper();
            }
            return (desmutar, link);
        }
        if (iniciou)
            await _copia.ConcluirAsync();
        lock (_trava)
        {
            _situacao = EstadoConferencia.Confere;
            _setupEmUso = setup.Id;
            _emUso = ativa is null ? SomEmUso.Nenhum : SomEmUso.DoCelular(ativa.Id);
            if (ativa is not null)
                _ultimoPreset = _presets.Obter(ativa.IdPreset)?.Id;
            _diferencas = [];
            _referencia = null;
            _motivoMudo = null;
            _antes = null;
            _pendente = null;
        }
        await GravarUsoAsync(d => d with { UltimoSetup = setup.Id });
        return (null, link);
    }

    /// <summary>
    /// Criar Setup novo (na dúvida ou na troca): nasce com a calibração lida, equilíbrio 0 e o link de
    /// agora; fica em uso, sem memórias; desmuta. Não manda nada ao aparelho além do desmutar.
    /// </summary>
    public async Task<ResultadoSetup> CriarSetupAsync(string? nomeDigitado)
    {
        await Carregada;
        if (!await _operacao.WaitAsync(0))
            return Terminar(ResultadoSetup.Falha(MsgOcupado));
        try
        {
            if (!EmEscolha)
                return Terminar(ResultadoSetup.Falha(MsgSoNaEscolha));
            if (NomeValido(nomeDigitado) is not { } nome)
                return Terminar(ResultadoSetup.Falha(GerentePresets.MsgNomeInvalido));
            if (Dados.Setups.Any(s => MesmoNome(s.Nome, nome)))
                return Terminar(ResultadoSetup.Falha(MsgNomeRepetidoSetup));
            if (!_estado.Pronto || _estado.Lendo)
                return Terminar(ResultadoSetup.Falha(_estado.Lendo ? Escritor.MsgLendo : Escritor.MsgSemLeitura));
            var (calibracao, faltam) = Camadas.Tirar(_estado.Copiar(), Camada.Calibracao);
            if (faltam > 0)
                return Terminar(ResultadoSetup.Falha(MsgIncompleta(faltam)));

            await _link.Carregada;
            var setup = new Setup($"setup-{Guid.NewGuid():N}", nome, _relogio.GetLocalNow(), calibracao, Equilibrio.Zero,
                _link.Ligado(ParCanais.Saidas12), _link.Ligado(ParCanais.Saidas34));
            var dados = Dados;
            var novos = dados with
            {
                Setups = [.. dados.Setups, setup],
                UltimoSetup = setup.Id,
                Uso = Com(dados.Uso, setup.Id, UsoDoSetup.Vazio),
            };
            if (!await GravarAsync(novos))
                return Terminar(ResultadoSetup.Falha(MsgNaoGuardou));

            var desmutar = await DesmutarAsync();
            lock (_trava)
            {
                _situacao = EstadoConferencia.Confere;
                _setupEmUso = setup.Id;
                _emUso = SomEmUso.Nenhum;
                _diferencas = [];
                _referencia = null;
                _motivoMudo = null;
                _antes = null;
                _pendente = null;
            }
            _log.Evento($"setup: {nome} criado com a calibração do aparelho");
            var frase = MsgSetupCriado(nome);
            if (desmutar is not null)
                frase += $" {MsgContinuaMutado(desmutar)}";
            return Terminar(ResultadoSetup.Feito(frase));
        }
        finally
        {
            _operacao.Release();
        }
    }

    /// <summary>Seguir sem Setup (na dúvida ou na troca): desmuta sem aplicar nada; as memórias do celular ficam travadas.</summary>
    public async Task<ResultadoSetup> SeguirSemSetupAsync()
    {
        await Carregada;
        if (!await _operacao.WaitAsync(0))
            return Terminar(ResultadoSetup.Falha(MsgOcupado));
        try
        {
            if (!EmEscolha)
                return Terminar(ResultadoSetup.Falha(MsgSoNaEscolha));
            var desmutar = await DesmutarAsync();
            lock (_trava)
            {
                _situacao = EstadoConferencia.Nenhuma;
                _setupEmUso = null;
                _emUso = SomEmUso.SomDoAparelho;
                _diferencas = [];
                _referencia = null;
                _motivoMudo = null;
                _antes = null;
                _pendente = null;
            }
            _log.Evento("setup: seguindo sem Setup");
            return Terminar(desmutar is null
                ? ResultadoSetup.Feito(MsgSeguiuSemSetup)
                : ResultadoSetup.Feito($"{MsgSeguiuSemSetup} {MsgContinuaMutado(desmutar)}"));
        }
        finally
        {
            _operacao.Release();
        }
    }

    /// <summary>Tocar no Setup com o app conectado: guarda a marca, muta e abre "Trocar de Setup".</summary>
    public async Task<ResultadoSetup> TrocarAsync()
    {
        await Carregada;
        if (!await _operacao.WaitAsync(0))
            return Terminar(ResultadoSetup.Falha(MsgOcupado));
        try
        {
            if (EmEscolha)
                return Terminar(ResultadoSetup.Falha(MsgDuvida));
            if (!SessaoAberta)
                return Terminar(ResultadoSetup.Falha(MsgSemConexao));
            if (!_estado.Pronto || _estado.Lendo)
                return Terminar(ResultadoSetup.Falha(_estado.Lendo ? Escritor.MsgLendo : Escritor.MsgSemLeitura));
            var espelho = _estado.Copiar();
            Antes antes;
            Setup? referencia;
            lock (_trava)
            {
                antes = new Antes(_situacao, _setupEmUso, _emUso);
                referencia = _setupEmUso is { } id ? _dados.Setups.FirstOrDefault(s => s.Id == id) : null;
            }
            var motivo = await MutarAsync(referencia?.Id, espelho);
            var lista = referencia is null ? [] : DiferencasDeCalibracao(referencia, espelho);
            lock (_trava)
            {
                _situacao = EstadoConferencia.Trocando;
                _referencia = referencia?.Id;
                _diferencas = lista;
                _motivoMudo = motivo is null ? null : MsgNaoMutou(motivo);
                _antes = antes;
            }
            _log.Evento("setup: trocando de Setup" + (motivo is null ? "; mutado" : $"; não mutou ({motivo})"));
            return Terminar(ResultadoSetup.Feito(null));
        }
        finally
        {
            _operacao.Release();
        }
    }

    /// <summary>Desistir da troca (‹ ou Voltar): desmuta e volta ao estado de antes, sem aplicar nada.</summary>
    public async Task<ResultadoSetup> DesistirAsync()
    {
        await Carregada;
        if (!await _operacao.WaitAsync(0))
            return Terminar(ResultadoSetup.Falha(MsgOcupado));
        try
        {
            Antes? antes;
            lock (_trava)
                antes = _situacao == EstadoConferencia.Trocando ? _antes : null;
            if (antes is null)
                return Terminar(ResultadoSetup.Falha(MsgSoNaEscolha));
            var desmutar = await DesmutarAsync();
            lock (_trava)
            {
                _situacao = antes.Situacao;
                _setupEmUso = antes.SetupEmUso;
                _emUso = antes.EmUso;
                _diferencas = [];
                _referencia = null;
                _motivoMudo = null;
                _antes = null;
            }
            return Terminar(desmutar is null ? ResultadoSetup.Feito(null) : ResultadoSetup.Feito(MsgContinuaMutado(desmutar)));
        }
        finally
        {
            _operacao.Release();
        }
    }

    // ------------------------------------------------------------------ Setups: renomear e apagar

    public async Task<ResultadoSetup> RenomearSetupAsync(string id, string? nomeDigitado)
    {
        await Carregada;
        if (!await _operacao.WaitAsync(0))
            return Terminar(ResultadoSetup.Falha(MsgOcupado));
        try
        {
            if (Setup(id) is not { } setup)
                return Terminar(ResultadoSetup.Falha(MsgSetupNaoExiste));
            if (NomeValido(nomeDigitado) is not { } nome)
                return Terminar(ResultadoSetup.Falha(GerentePresets.MsgNomeInvalido));
            if (Dados.Setups.Any(s => s.Id != id && MesmoNome(s.Nome, nome)))
                return Terminar(ResultadoSetup.Falha(MsgNomeRepetidoSetup));
            var dados = Dados;
            if (!await GravarAsync(dados with { Setups = dados.Setups.Select(s => s.Id == id ? s with { Nome = nome } : s).ToArray() }))
                return Terminar(ResultadoSetup.Falha(MsgNaoGuardou));
            _log.Evento($"setup: {setup.Nome} renomeado para {nome}");
            return Terminar(ResultadoSetup.Feito(MsgRenomeado(nome)));
        }
        finally
        {
            _operacao.Release();
        }
    }

    /// <summary>Apaga um Setup, com as memórias dele. O Setup em uso e o herdado não se apagam.</summary>
    public async Task<ResultadoSetup> ApagarSetupAsync(string id)
    {
        await Carregada;
        if (!await _operacao.WaitAsync(0))
            return Terminar(ResultadoSetup.Falha(MsgOcupado));
        try
        {
            if (Setup(id) is not { } setup)
                return Terminar(ResultadoSetup.Falha(MsgSetupNaoExiste));
            if (setup.Herdado)
                return Terminar(ResultadoSetup.Falha(MsgSetupHerdado));
            if (SetupEmUso?.Id == id)
                return Terminar(ResultadoSetup.Falha(MsgSetupEmUso));
            var dados = Dados;
            var uso = new Dictionary<string, UsoDoSetup>(dados.Uso);
            uso.Remove(id);
            var novos = new DadosSetups(
                dados.Setups.Where(s => s.Id != id).ToArray(),
                dados.Memorias.Where(m => m.IdSetup != id).ToArray(),
                dados.UltimoSetup == id ? null : dados.UltimoSetup,
                uso);
            if (!await GravarAsync(novos))
                return Terminar(ResultadoSetup.Falha(MsgNaoGuardou));
            // As playlists das memórias dele somem junto.
            await _playlists.ApagarAsync(dados.Memorias.Where(m => m.IdSetup == id).Select(m => m.Id));
            _log.Evento($"setup: {setup.Nome} apagado");
            return Terminar(ResultadoSetup.Feito(MsgSetupApagado(setup.Nome)));
        }
        finally
        {
            _operacao.Release();
        }
    }

    // ------------------------------------------------------------------ equipamentos (história 8e)

    /// <summary>
    /// Adiciona um equipamento ao Setup, com as ligações dele (o id vem daqui). A lista inteira tem de
    /// continuar válida (<see cref="Ligacoes.Validar"/>): uma ligação quebrada, para o grupo errado,
    /// um bridge que não vale ou um ciclo no grupo 3 recusam sem mudar nada. Não manda nada ao aparelho.
    /// </summary>
    public Task<ResultadoEquipamento> AdicionarEquipamentoAsync(string idSetup, Equipamento rascunho) =>
        MudarEquipamentosAsync(idSetup, setup =>
        {
            var (limpo, motivo) = Ligacoes.Limpar(rascunho with { Id = $"equipamento-{Guid.NewGuid():N}" });
            if (limpo is null)
                return new Mudanca(null, motivo, null, null);
            return new Mudanca([.. setup.Equipamentos, limpo], null, MsgEquipamentoAdicionado(limpo.Nome, setup.Nome), limpo.Id);
        });

    /// <summary>Guarda o equipamento editado (pelo id), com as mesmas regras do adicionar.</summary>
    public Task<ResultadoEquipamento> EditarEquipamentoAsync(string idSetup, Equipamento editado) =>
        MudarEquipamentosAsync(idSetup, setup =>
        {
            if (setup.Equipamentos.All(e => e.Id != editado.Id))
                return new Mudanca(null, MsgEquipamentoNaoExiste, null, null);
            var (limpo, motivo) = Ligacoes.Limpar(editado);
            if (limpo is null)
                return new Mudanca(null, motivo, null, null);
            return new Mudanca(setup.Equipamentos.Select(e => e.Id == limpo.Id ? limpo : e).ToArray(), null,
                MsgEquipamentoGuardado(limpo.Nome), limpo.Id, Editado: limpo.Id);
        });

    /// <summary>
    /// Tira o equipamento do Setup. Tirar um módulo deixa os alto-falantes dele sem ligação; tirar um
    /// equipamento do grupo 3 deixa sem fonte quem ele alimentava; nos dois casos, a frase avisa.
    /// </summary>
    public Task<ResultadoEquipamento> TirarEquipamentoAsync(string idSetup, string idEquipamento) =>
        MudarEquipamentosAsync(idSetup, setup =>
        {
            if (setup.Equipamentos.FirstOrDefault(e => e.Id == idEquipamento) is not { } tirado)
                return new Mudanca(null, MsgEquipamentoNaoExiste, null, null);
            var r = Ligacoes.Tirar(setup.Equipamentos, idEquipamento);
            var frase = MsgEquipamentoTirado(tirado.Nome, setup.Nome);
            if (r.SemLigacao.Count > 0)
                frase += $" {MsgSemLigacao(r.SemLigacao)}";
            if (r.SemFonte.Count > 0)
                frase += $" {MsgSemFonte(r.SemFonte)}";
            return new Mudanca(r.Lista, null, frase, idEquipamento);
        });

    /// <param name="Editado">O equipamento editado: os alto-falantes que perderiam a ligação nele vêm na recusa.</param>
    private sealed record Mudanca(IReadOnlyList<Equipamento>? Lista, string? Motivo, string? Mensagem, string? Id, string? Editado = null);

    /// <summary>Uma mudança nos equipamentos de um Setup: confere a lista inteira e só guarda se ela vale. Uma operação por vez.</summary>
    private async Task<ResultadoEquipamento> MudarEquipamentosAsync(string idSetup, Func<Setup, Mudanca> mudar)
    {
        await Carregada;
        if (!await _operacao.WaitAsync(0))
            return TerminarEquipamento(ResultadoEquipamento.Falha(MsgOcupado));
        try
        {
            if (Setup(idSetup) is not { } setup)
                return TerminarEquipamento(ResultadoEquipamento.Falha(MsgSetupNaoExiste));
            var m = mudar(setup);
            if (m.Lista is null)
                return TerminarEquipamento(ResultadoEquipamento.Falha(m.Motivo!));
            if (Ligacoes.Validar(m.Lista, m.Editado) is { } invalido)
                return TerminarEquipamento(ResultadoEquipamento.Falha(invalido));
            var dados = Dados;
            var novos = dados with
            {
                Setups = dados.Setups.Select(s => s.Id == setup.Id ? s with { Equipamentos = m.Lista } : s).ToArray(),
            };
            if (!await GravarAsync(novos))
                return TerminarEquipamento(ResultadoEquipamento.Falha(MsgNaoGuardou));
            _log.Evento($"setup {setup.Nome}: {m.Mensagem}");
            return TerminarEquipamento(new ResultadoEquipamento(true, null, m.Mensagem, m.Id));
        }
        finally
        {
            _operacao.Release();
        }
    }

    private ResultadoEquipamento TerminarEquipamento(ResultadoEquipamento r)
    {
        lock (_trava)
            _aviso = r.Mensagem;
        if (!r.Ok)
            _log.Evento($"equipamento: {r.Mensagem ?? r.Motivo}");
        Notificar();
        return r;
    }

    // ------------------------------------------------------------------ memórias do celular

    /// <summary>
    /// Criar memória do som atual, no Setup em uso: guarda o caráter lido menos o equilíbrio. Vira a
    /// memória em uso e a ativa e entra nos botões. Com uma memória do aparelho (ou o som do aparelho)
    /// em uso, não cria: o caminho é a folha do Salvar.
    /// </summary>
    public async Task<ResultadoSetup> CriarMemoriaAsync(string? nomeDigitado)
    {
        await Carregada;
        if (!await _operacao.WaitAsync(0))
            return Terminar(ResultadoSetup.Falha(MsgOcupado));
        try
        {
            if (EmEscolha)
                return Terminar(ResultadoSetup.Falha(MsgDuvida));
            if (SetupEmUso is not { } setup)
                return Terminar(ResultadoSetup.Falha(MsgSemSetup));
            if (EmUso.Tipo is TipoSomEmUso.MemoriaAparelho or TipoSomEmUso.SomDoAparelho)
                return Terminar(new ResultadoSetup(false, MsgPedeFolha, MsgPedeFolha, PedeFolhaSalvar: true));
            if (!_estado.Pronto || _estado.Lendo)
                return Terminar(ResultadoSetup.Falha(_estado.Lendo ? Escritor.MsgLendo : Escritor.MsgSemLeitura));
            // A memória nova lembra o preset em uso.
            var (memoria, motivo) = NovaMemoria(setup, nomeDigitado, _estado.Copiar(), EquilibrioEfetivo, PresetEmUso?.Id);
            if (memoria is null)
                return Terminar(ResultadoSetup.Falha(motivo!));

            var dados = Dados;
            var novos = dados with
            {
                Memorias = [.. dados.Memorias, memoria],
                Uso = Com(dados.Uso, setup.Id, Usar(dados.UsoDe(setup.Id), memoria.Id)),
            };
            if (!await GravarAsync(novos))
                return Terminar(ResultadoSetup.Falha(MsgNaoGuardou));
            lock (_trava)
            {
                _emUso = SomEmUso.DoCelular(memoria.Id);
                if (_situacao == EstadoConferencia.SomDiferente)
                    _situacao = EstadoConferencia.Confere;
            }
            _log.Evento($"memória {memoria.Nome} criada no {setup.Nome}");
            await AdotarPlaylistsAsync();
            return Terminar(ResultadoSetup.Feito(MsgCriada(memoria.Nome, setup.Nome)));
        }
        finally
        {
            _operacao.Release();
        }
    }

    /// <summary>
    /// Aplica a memória do celular (do Setup em uso): cópia antes, escreve a calibração do Setup e o
    /// caráter da memória somado ao equilíbrio (só o que difere), relê e confere. As bandas, o mute e
    /// os extras não entram. Passa pela chave "Aplicar memória do celular".
    /// </summary>
    public Task<ResultadoSetup> AplicarAsync(string idMemoria) => AplicarMemoriaComTravaAsync(idMemoria, TipoAplicarMemoria, refazendo: false);

    /// <summary>Reaplicar a memória ativa do Setup em uso ("Som diferente da FILME").</summary>
    public Task<ResultadoSetup> ReaplicarAsync()
    {
        var ativa = Ativa(SetupEmUso?.Id);
        if (ativa is null)
            return Task.FromResult(Terminar(ResultadoSetup.Falha(SetupEmUso is null ? MsgSemSetup : MsgMemoriaNaoExiste)));
        return AplicarAsync(ativa.Id);
    }

    private async Task<ResultadoSetup> AplicarMemoriaComTravaAsync(string idMemoria, string tipo, bool refazendo)
    {
        await Carregada;
        if (!await _operacao.WaitAsync(0))
            return Terminar(ResultadoSetup.Falha(MsgOcupado));
        try
        {
            return Terminar(await AplicarMemoriaAsync(idMemoria, tipo, refazendo));
        }
        finally
        {
            _operacao.Release();
        }
    }

    /// <summary>Aplica a memória. Chamar com a operação.</summary>
    private async Task<ResultadoSetup> AplicarMemoriaAsync(string idMemoria, string tipo, bool refazendo)
    {
        var memoria = Memoria(idMemoria);
        if (memoria is null)
            return ResultadoSetup.Falha(refazendo ? MsgNaoExisteTerminar : MsgMemoriaNaoExiste);
        if (!refazendo)
        {
            if (EmEscolha)
                return ResultadoSetup.Falha(MsgDuvida);
            if (SetupEmUso is not { } emUso)
                return ResultadoSetup.Falha(MsgSemSetup);
            if (memoria.IdSetup != emUso.Id)
                return ResultadoSetup.Falha(MsgOutroSetup);
            if (tipo == TipoAplicarMemoria && !_chaves.Ligada(Chave.AplicarMemoriaCelular))
                return ResultadoSetup.Falha(Sessao.MotivoChaveDesligada);
        }
        if (Setup(memoria.IdSetup) is not { } setup)
            return ResultadoSetup.Falha(refazendo ? MsgNaoExisteTerminar : MsgSetupNaoExiste);

        var descricao = tipo == TipoManterCalibracao
            ? $"Reaplicar a calibração do {setup.Nome} com a {memoria.Nome}"
            : $"Aplicar a memória {memoria.Nome}";
        var op = new OperacaoPendente(tipo, memoria.Id, descricao, memoria.Nome);
        var (alvo, sumiu) = Alvo(setup, memoria);
        var aplicado = await AplicarAlvoAsync(alvo, op, refazendo);
        if (!aplicado.Ok)
            return ResultadoSetup.Falha(aplicado.Motivo!);

        string? link;
        if (Marca is not null)
        {
            // Um Terminar com a dúvida aberta: a calibração do Setup está no aparelho; a dúvida fecha.
            (var falhou, link) = await FecharEscolhaAsync(setup, memoria, aplicado.Iniciou);
            if (falhou is not null)
                return ResultadoSetup.Falha(MsgContinuaMutado(falhou));
        }
        else
        {
            if (aplicado.Iniciou)
                await _copia.ConcluirAsync();
            link = await _link.DefinirEstadoAsync(setup.Link12, setup.Link34);
        }

        lock (_trava)
        {
            _situacao = EstadoConferencia.Confere;
            _setupEmUso = setup.Id;
            _emUso = SomEmUso.DoCelular(memoria.Id);
            _ultimoPreset = sumiu ? null : memoria.IdPreset;
            _pendente = null;
        }
        await GravarUsoAsync(d => d with { Uso = Com(d.Uso, setup.Id, Usar(d.UsoDe(setup.Id), memoria.Id)) });
        _log.Evento($"memória {memoria.Nome} aplicada");
        var frase = aplicado.Restam == 0 ? MsgAplicada(memoria.Nome) : MsgAplicadaDiferente(memoria.Nome, aplicado.Restam);
        if (sumiu)
            frase += $" {MsgPresetSumiu}";
        if (link is not null)
            frase += $" {MsgLinkNaoGuardado(link)}";
        return ResultadoSetup.Feito(frase);
    }

    public async Task<ResultadoSetup> RenomearMemoriaAsync(string id, string? nomeDigitado)
    {
        await Carregada;
        if (!await _operacao.WaitAsync(0))
            return Terminar(ResultadoSetup.Falha(MsgOcupado));
        try
        {
            if (Memoria(id) is not { } memoria)
                return Terminar(ResultadoSetup.Falha(MsgMemoriaNaoExiste));
            if (NomeValido(nomeDigitado) is not { } nome)
                return Terminar(ResultadoSetup.Falha(GerentePresets.MsgNomeInvalido));
            var dados = Dados;
            if (dados.Memorias.Any(m => m.IdSetup == memoria.IdSetup && m.Id != id && MesmoNome(m.Nome, nome)))
                return Terminar(ResultadoSetup.Falha(MsgNomeRepetidoMemoria(Setup(memoria.IdSetup)?.Nome ?? "")));
            if (!await GravarAsync(dados with { Memorias = dados.Memorias.Select(m => m.Id == id ? m with { Nome = nome } : m).ToArray() }))
                return Terminar(ResultadoSetup.Falha(MsgNaoGuardou));
            _log.Evento($"memória {memoria.Nome} renomeada para {nome}");
            await AdotarPlaylistsAsync();
            return Terminar(ResultadoSetup.Feito(MsgRenomeado(nome)));
        }
        finally
        {
            _operacao.Release();
        }
    }

    /// <summary>Apaga a memória do celular e a playlist dela. A memória em uso não se apaga.</summary>
    public async Task<ResultadoSetup> ApagarMemoriaAsync(string id)
    {
        await Carregada;
        if (!await _operacao.WaitAsync(0))
            return Terminar(ResultadoSetup.Falha(MsgOcupado));
        try
        {
            if (Memoria(id) is not { } memoria)
                return Terminar(ResultadoSetup.Falha(MsgMemoriaNaoExiste));
            if (EmUso is { Tipo: TipoSomEmUso.MemoriaCelular } uso && uso.IdMemoria == id)
                return Terminar(ResultadoSetup.Falha(MsgMemoriaEmUso));
            var dados = Dados;
            var novos = dados with
            {
                Memorias = dados.Memorias.Where(m => m.Id != id).ToArray(),
                Uso = Com(dados.Uso, memoria.IdSetup, Remover(dados.UsoDe(memoria.IdSetup), id)),
            };
            if (!await GravarAsync(novos))
                return Terminar(ResultadoSetup.Falha(MsgNaoGuardou));
            await _playlists.ApagarAsync([id]); // a playlist dela some junto
            _log.Evento($"memória {memoria.Nome} apagada");
            return Terminar(ResultadoSetup.Feito(MsgMemoriaApagada(memoria.Nome)));
        }
        finally
        {
            _operacao.Release();
        }
    }

    // ------------------------------------------------------------------ equilíbrio

    /// <summary>
    /// Mexe no equilíbrio da saída do app que tem o <paramref name="canal"/> (0 a 3), no Setup em uso
    /// (ganho ou limiter): o mesmo valor vai para todos os canais da saída (os dois, num par com link).
    /// Escreve no canal mestre o valor final (a parte da memória mais o equilíbrio novo, presa à
    /// faixa); com o link ligado, o aparelho repete no outro canal. Guarda o equilíbrio como pendente
    /// até o Salvar. O limiter em OFF fica OFF (só o equilíbrio muda).
    /// </summary>
    public async Task<ResultadoSetup> AjustarEquilibrioAsync(int canal, bool limiter, decimal valor)
    {
        await Carregada;
        if (!await _operacao.WaitAsync(0))
            return Terminar(ResultadoSetup.Falha(MsgOcupado));
        try
        {
            if (!SessaoAberta)
                return Terminar(ResultadoSetup.Falha(MsgSemConexao));
            if (EmEscolha)
                return Terminar(ResultadoSetup.Falha(MsgDuvida));
            if (SetupEmUso is not { } setup)
                return Terminar(ResultadoSetup.Falha(MsgSemSetup));
            if (canal is < 0 or >= Equilibrio.Canais || SaidaDe(canal) is not { } saidaApp)
                return Terminar(ResultadoSetup.Falha(MsgEquilibrioCanal));
            if (valor is < Camadas.EquilibrioMinimo or > Camadas.EquilibrioMaximo || decimal.Round(valor, 1) != valor)
                return Terminar(ResultadoSetup.Falha(MsgEquilibrioFaixa));

            var saida = saidaApp.Mestre;
            var codigo = limiter ? Camadas.CodigoLimiter : Camadas.CodigoGanho;
            if (_estado.Valor(codigo, saida) is not { } lido)
                return Terminar(ResultadoSetup.Falha(Escritor.MsgSemLeitura));
            var atual = EquilibrioEfetivo;
            var novo = atual;
            foreach (var c in saidaApp.Canais)
                novo = novo.Com(c, limiter, valor);
            string? aviso = null;
            if (!(limiter && lido.Numero == Camadas.LimiterOff))
            {
                var parte = Camadas.Subtrair(codigo, saida, lido.NoFio, atual);
                var final = Camadas.Numero(Camadas.Somar(codigo, saida, parte, novo))!.Value;
                if (Camadas.Numero(parte)!.Value + valor != final)
                    aviso = MsgNoLimite(setup.Nome);
                var r = await _escritor.EscreverAsync(Catalogo.ParametroDe(codigo)!, saida, final);
                if (!r.Ok)
                    return Terminar(ResultadoSetup.Falha(r.Motivo ?? Sessao.MotivoFalhaEscrita));
                aviso ??= r.Aviso;
            }
            lock (_trava)
            {
                if (_setupEmUso == setup.Id)
                    _pendente = novo == setup.Equilibrio ? null : novo;
            }
            return Terminar(ResultadoSetup.Feito(aviso));
        }
        finally
        {
            _operacao.Release();
        }
    }

    // ------------------------------------------------------------------ Salvar por camadas

    /// <summary>A folha do Salvar agora: o que mudou em cada camada, as perguntas e a trava.</summary>
    public FolhaSalvar PrepararSalvar()
    {
        if (EmEscolha)
            return FolhaSalvar.Travada(MsgDuvida);
        if (SetupEmUso is not { } setup)
            return FolhaSalvar.Travada(MsgSemSetupSalvar);
        if (!SessaoAberta || !_estado.Pronto || _estado.Lendo)
            return FolhaSalvar.Travada(SessaoAberta ? (_estado.Lendo ? Escritor.MsgLendo : Escritor.MsgSemLeitura) : MsgSemConexao, setup.Nome);

        var espelho = _estado.Copiar();
        var equilibrio = EquilibrioEfetivo;
        var calibracao = MudancasDeCalibracao(setup, espelho, equilibrio);
        var linhas = new List<LinhaSalvar>();
        var emUso = EmUso;
        switch (emUso.Tipo)
        {
            case TipoSomEmUso.MemoriaCelular when Memoria(emUso.IdMemoria) is { } memoria:
            {
                var carater = MudancasDeCarater(memoria, espelho, setup.Equilibrio, equilibrio);
                if (calibracao.Count > 0)
                    linhas.Add(new LinhaSalvar(Camada.Calibracao, $"Calibração → Setup {setup.Nome}", calibracao));
                if (carater.Count > 0)
                    linhas.Add(new LinhaSalvar(Camada.Carater, $"Caráter → memória {memoria.Nome}", carater));

                // As 15 bandas contra o preset lembrado (sem ele, contra o EQ plano).
                var lembrado = PresetDe(memoria);
                var bandas = MudancasDeBandas(lembrado?.Valores ?? GerentePresets.BandasPlanas, espelho);
                if (bandas.Count > 0)
                {
                    if (lembrado is null)
                        linhas.Add(new LinhaSalvar(Camada.Bandas, "15 bandas → preset novo", bandas));
                    else
                        linhas.Add(new LinhaSalvar(Camada.Bandas, $"15 bandas → preset {lembrado.Nome}", bandas, lembrado.Herdado));
                }
                if (linhas.Count == 0)
                    return FolhaSalvar.Travada(MsgNadaParaSalvar, setup.Nome);
                var folha = new FolhaSalvar(null, linhas, PedeNome: false, PerguntaCalibracao: false, setup.Nome, []);
                if (bandas.Count == 0)
                    return folha;
                if (lembrado is null)
                    return folha with { PedeNomePreset = true };
                return folha with
                {
                    PerguntaPreset = true,
                    NomePreset = lembrado.Nome,
                    MotivoAtualizarTravado = lembrado.Herdado ? GerentePresets.MsgHerdadoNaoSobrescrito : null,
                    EscolhaPresetInicial = lembrado.Herdado ? EscolhaPreset.Criar : EscolhaPreset.Nenhuma,
                };
            }
            case TipoSomEmUso.MemoriaAparelho or TipoSomEmUso.SomDoAparelho:
            {
                linhas.Add(new LinhaSalvar(Camada.Carater, "Caráter → memória nova", []));
                if (calibracao.Count > 0)
                    linhas.Add(new LinhaSalvar(Camada.Calibracao, $"Calibração do aparelho × Setup {setup.Nome}", calibracao));
                var (linha, igual) = LinhaBandasMemoriaNova(espelho);
                linhas.Add(linha);
                return new FolhaSalvar(null, linhas, PedeNome: true, PerguntaCalibracao: calibracao.Count > 0, setup.Nome,
                    DiferencasDeCalibracao(setup, espelho))
                {
                    NomePreset = igual?.Nome,
                    PedeNomePreset = igual is null,
                };
            }
            default:
            {
                // Setup sem memórias: a calibração vai para o Setup; o caráter, para uma memória nova.
                if (calibracao.Count > 0)
                    linhas.Add(new LinhaSalvar(Camada.Calibracao, $"Calibração → Setup {setup.Nome}", calibracao));
                linhas.Add(new LinhaSalvar(Camada.Carater, "Caráter → memória nova", []));
                var (linha, igual) = LinhaBandasMemoriaNova(espelho);
                linhas.Add(linha);
                return new FolhaSalvar(null, linhas, PedeNome: true, PerguntaCalibracao: false, setup.Nome, [])
                {
                    NomePreset = igual?.Nome,
                    PedeNomePreset = igual is null,
                };
            }
        }
    }

    /// <summary>
    /// A linha das 15 bandas de uma memória nova: se as bandas lidas são as de um preset, a memória
    /// lembra ele, sem pergunta; senão, um preset novo, com o nome.
    /// </summary>
    private (LinhaSalvar Linha, Preset? Igual) LinhaBandasMemoriaNova(IReadOnlyDictionary<Endereco, Valor> espelho)
    {
        if (_presets.ComAsBandas(espelho) is { } igual)
            return (new LinhaSalvar(Camada.Bandas, $"15 bandas → preset {igual.Nome}", [], igual.Herdado), igual);
        return (new LinhaSalvar(Camada.Bandas, "15 bandas → preset novo", []), null);
    }

    /// <summary>
    /// Salva o que a folha mostra, cada mudança na sua camada; as 15 bandas vão para o preset
    /// (atualizar o lembrado, criar um novo com o nome, ou lembrar o igual). Só o
    /// <see cref="EscolhaCalibracao.Manter"/> manda algo ao aparelho: guarda a memória nova e reaplica
    /// a calibração do Setup, com cópia antes. Nomes inválidos ou repetidos recusam antes de guardar
    /// qualquer coisa.
    /// </summary>
    public async Task<ResultadoSetup> SalvarAsync(EscolhaCalibracao escolha, string? nomeMemoriaNova,
        EscolhaPreset escolhaPreset = EscolhaPreset.Nenhuma, string? nomePresetNovo = null)
    {
        await Carregada;
        if (!await _operacao.WaitAsync(0))
            return Terminar(ResultadoSetup.Falha(MsgOcupado));
        try
        {
            var folha = PrepararSalvar();
            if (folha.Trava is { } trava)
                return Terminar(ResultadoSetup.Falha(trava));
            if (!folha.PerguntaCalibracao)
                escolha = EscolhaCalibracao.Nenhuma; // uma escolha velha não vale quando a folha não pergunta
            else if (escolha == EscolhaCalibracao.Nenhuma)
                return Terminar(ResultadoSetup.Falha(MsgEscolha));

            // As 15 bandas: a escolha (com a memória do celular em uso) ou o caminho sem pergunta.
            if (folha.PerguntaPreset)
            {
                if (escolhaPreset == EscolhaPreset.Nenhuma)
                    escolhaPreset = folha.EscolhaPresetInicial;
                if (escolhaPreset == EscolhaPreset.Nenhuma)
                    return Terminar(ResultadoSetup.Falha(MsgEscolhaPreset));
                if (escolhaPreset == EscolhaPreset.Atualizar && folha.MotivoAtualizarTravado is { } travado)
                    return Terminar(ResultadoSetup.Falha(travado));
            }
            else
            {
                escolhaPreset = folha.PedeNomePreset ? EscolhaPreset.Criar : EscolhaPreset.Nenhuma;
            }

            // Manter reaplica a calibração pelo BLE: a chave vale antes de guardar qualquer coisa.
            if (escolha == EscolhaCalibracao.Manter && !_chaves.Ligada(Chave.AplicarMemoriaCelular))
                return Terminar(ResultadoSetup.Falha(Sessao.MotivoChaveDesligada));

            var setup = SetupEmUso!;
            var espelho = _estado.Copiar();
            var equilibrio = EquilibrioEfetivo;
            var calibracaoMudou = MudancasDeCalibracao(setup, espelho, equilibrio).Count > 0;
            var dados = Dados;
            var emUso = EmUso;
            var memoriaEmUso = emUso.Tipo == TipoSomEmUso.MemoriaCelular ? Memoria(emUso.IdMemoria) : null;
            var partes = new List<string>();

            // Tudo o que pode recusar vem antes de guardar qualquer coisa.
            MemoriaCelular? nova = null;
            if (folha.PedeNome)
            {
                var (memoria, motivo) = NovaMemoria(setup, nomeMemoriaNova, espelho, equilibrio, idPreset: null);
                if (memoria is null)
                    return Terminar(ResultadoSetup.Falha(motivo!));
                nova = memoria;
            }

            string? nomePreset = null;
            IReadOnlyList<ValorCopiado> bandas = [];
            if (escolhaPreset != EscolhaPreset.Nenhuma)
            {
                if (escolhaPreset == EscolhaPreset.Criar)
                {
                    var (nome, motivo) = _presets.ConferirNomeNovo(nomePresetNovo);
                    if (nome is null)
                        return Terminar(ResultadoSetup.Falha(MsgNomePreset(motivo!)));
                    nomePreset = nome;
                }
                (bandas, var faltamBandas) = GerentePresets.TirarBandas(espelho);
                if (faltamBandas > 0)
                    return Terminar(ResultadoSetup.Falha(GerentePresets.MsgIncompleta(faltamBandas)));
            }

            var atualizarSetup = calibracaoMudou && escolha != EscolhaCalibracao.Manter;
            var setupNovo = setup;
            if (atualizarSetup)
            {
                var (calibracao, faltam) = Camadas.Tirar(espelho, Camada.Calibracao);
                if (faltam > 0)
                    return Terminar(ResultadoSetup.Falha(MsgIncompleta(faltam)));
                await _link.Carregada;
                setupNovo = setup with
                {
                    Calibracao = calibracao,
                    Equilibrio = equilibrio,
                    Link12 = _link.Ligado(ParCanais.Saidas12),
                    Link34 = _link.Ligado(ParCanais.Saidas34),
                    Quando = _relogio.GetLocalNow(),
                };
                partes.Add($"calibração no {setup.Nome}");
            }

            IReadOnlyList<ValorCopiado>? caraterNovo = null;
            if (nova is null && memoriaEmUso is not null && MudancasDeCarater(memoriaEmUso, espelho, setup.Equilibrio, equilibrio).Count > 0)
            {
                var (carater, faltam) = Camadas.Tirar(espelho, Camada.Carater);
                if (faltam > 0)
                    return Terminar(ResultadoSetup.Falha(MsgIncompleta(faltam)));
                caraterNovo = Camadas.Separar(carater, equilibrio);
            }

            // O preset: criar (com o nome), atualizar o lembrado, ou lembrar o que tem as mesmas bandas.
            string? idPreset = null;
            string? parteBandas = null;
            switch (escolhaPreset)
            {
                case EscolhaPreset.Criar:
                {
                    var r = await _presets.CriarAsync(nomePreset, bandas);
                    if (!r.Ok)
                        return Terminar(ResultadoSetup.Falha(r.Mensagem ?? r.Motivo ?? MsgNaoGuardou));
                    idPreset = r.Preset!.Id;
                    parteBandas = $"15 bandas no preset {r.Preset.Nome}";
                    break;
                }
                case EscolhaPreset.Atualizar:
                {
                    var lembrado = PresetDe(memoriaEmUso)!;
                    var r = await _presets.AtualizarAsync(lembrado.Id, bandas);
                    if (!r.Ok)
                        return Terminar(ResultadoSetup.Falha(r.Mensagem ?? r.Motivo ?? MsgNaoGuardou));
                    idPreset = lembrado.Id;
                    parteBandas = $"15 bandas no preset {lembrado.Nome}";
                    break;
                }
                default:
                    // A memória nova com as bandas de um preset que já existe: lembra ele, sem pergunta.
                    if (nova is not null && _presets.ComAsBandas(espelho) is { } igual)
                        idPreset = igual.Id;
                    break;
            }

            var memorias = dados.Memorias.ToList();
            var uso = dados.UsoDe(setup.Id);
            string? idEmUso = null;
            if (nova is not null)
            {
                nova = nova with { IdPreset = idPreset };
                memorias.Add(nova);
                uso = Usar(uso, nova.Id);
                idEmUso = nova.Id;
                partes.Add($"caráter na {nova.Nome}");
            }
            else if (memoriaEmUso is not null)
            {
                var atualizada = memoriaEmUso;
                if (caraterNovo is not null)
                {
                    atualizada = atualizada with { Carater = caraterNovo, Quando = _relogio.GetLocalNow() };
                    partes.Add($"caráter na {memoriaEmUso.Nome}");
                }
                if (escolhaPreset == EscolhaPreset.Criar)
                    atualizada = atualizada with { IdPreset = idPreset };
                if (!ReferenceEquals(atualizada, memoriaEmUso))
                    memorias = memorias.Select(m => m.Id == memoriaEmUso.Id ? atualizada : m).ToList();
            }
            if (parteBandas is not null)
                partes.Add(parteBandas);

            var novos = dados with
            {
                Setups = dados.Setups.Select(s => s.Id == setup.Id ? setupNovo : s).ToArray(),
                Memorias = memorias,
                Uso = Com(dados.Uso, setup.Id, uso),
            };
            if (!await GravarAsync(novos))
                return Terminar(ResultadoSetup.Falha(MsgNaoGuardou));
            lock (_trava)
            {
                if (atualizarSetup || escolha == EscolhaCalibracao.Manter)
                    _pendente = null;
                if (idEmUso is not null)
                    _emUso = SomEmUso.DoCelular(idEmUso);
                if (idPreset is not null)
                    _ultimoPreset = idPreset;
                if (_situacao == EstadoConferencia.SomDiferente)
                    _situacao = EstadoConferencia.Confere;
            }
            if (nova is not null)
                await AdotarPlaylistsAsync();
            var frase = $"Salvo: {string.Join(", ", partes)}.";
            _log.Evento($"setup: {frase}");

            if (escolha == EscolhaCalibracao.Manter && calibracaoMudou && nova is not null)
            {
                // Manter: a calibração do Setup volta ao aparelho, com a memória nova por cima.
                // A memória já está guardada: o Salvar valeu mesmo se a calibração não voltar (o
                // "aplicado pela metade" da cópia cuida do resto).
                var r = await AplicarMemoriaAsync(nova.Id, TipoManterCalibracao, refazendo: false);
                return Terminar(ResultadoSetup.Feito(r.Ok
                    ? $"{frase} Calibração do {setup.Nome} reaplicada."
                    : $"{frase} A calibração do {setup.Nome} não voltou: {r.Motivo}"));
            }
            return Terminar(ResultadoSetup.Feito(frase));
        }
        finally
        {
            _operacao.Release();
        }
    }

    /// <summary>As 15 bandas do aparelho contra a referência (o preset lembrado, ou o EQ plano).</summary>
    private static List<MudancaSalvar> MudancasDeBandas(IReadOnlyList<ValorCopiado> referencia, IReadOnlyDictionary<Endereco, Valor> espelho) =>
        Camadas.Diferentes(referencia, espelho)
            .Select(v => new MudancaSalvar(Camadas.Rotulo(v.Codigo, v.Indice), Camadas.Exibir(v.Codigo, v.NoFio),
                Camadas.Exibir(v.Codigo, espelho.GetValueOrDefault(new Endereco(v.Codigo, v.Indice))?.NoFio)))
            .ToList();

    // ------------------------------------------------------------------ preset aplicado e playlists

    /// <summary>
    /// Um preset foi aplicado (pela tela ou pela IA): ele passa a ser o preset em uso desta conexão, e
    /// a memória do celular em uso passa a lembrá-lo.
    /// </summary>
    private async Task AoAplicarPresetAsync(Preset preset)
    {
        string? idMemoria;
        lock (_trava)
        {
            _ultimoPreset = preset.Id;
            idMemoria = _emUso is { Tipo: TipoSomEmUso.MemoriaCelular } uso ? uso.IdMemoria : null;
        }
        if (idMemoria is not null && Memoria(idMemoria) is { } memoria && memoria.IdPreset != preset.Id)
        {
            await GravarUsoAsync(d => d with
            {
                Memorias = d.Memorias.Select(m => m.Id == idMemoria ? m with { IdPreset = preset.Id } : m).ToArray(),
            });
            _log.Evento($"memória {memoria.Nome} lembra o preset {preset.Nome}");
            return;
        }
        Notificar();
    }

    /// <summary>
    /// As memórias na ordem em que uma playlist pendente procura a dona: as do Casa 2.1 primeiro,
    /// depois as dos outros Setups na ordem; dentro de cada Setup, na ordem de criação.
    /// </summary>
    public static IReadOnlyList<(string Id, string Nome)> OrdemDeAdocao(DadosSetups dados) =>
        dados.Setups
            .OrderBy(s => s.Id == ConhecimentoHerdado.IdSetupCasa ? 0 : 1) // estável: os outros ficam na ordem
            .SelectMany(s => dados.Memorias.Where(m => m.IdSetup == s.Id))
            .Select(m => (m.Id, m.Nome))
            .ToArray();

    /// <summary>As playlists pendentes procuram a memória com o nome delas.</summary>
    private async Task AdotarPlaylistsAsync()
    {
        try
        {
            await _playlists.AdotarAsync(OrdemDeAdocao(Dados));
        }
        catch (Exception e)
        {
            _log.Evento($"playlists: falha na adoção ({e.GetType().Name})");
        }
    }

    /// <summary>
    /// A calibração do aparelho contra a do Setup, mais o link e o equilíbrio pendente, pelo nome das
    /// saídas em uso ("Atraso · Sub: 80 cm → 85 cm"; o par com link que muda igual é uma linha só).
    /// </summary>
    private List<MudancaSalvar> MudancasDeCalibracao(Setup setup, IReadOnlyDictionary<Endereco, Valor> espelho, Equilibrio equilibrio)
    {
        var saidas = SaidasEmUso;
        var lista = Linhas(Camadas.Diferentes(setup.Calibracao, espelho)
                .Select(v => DiferencaPorCanal.De(v.Codigo, v.Indice, Camadas.Exibir(v.Codigo, v.NoFio),
                    Camadas.Exibir(v.Codigo, espelho.GetValueOrDefault(new Endereco(v.Codigo, v.Indice))?.NoFio))),
            saidas);
        foreach (var (par, antes) in new[] { (ParCanais.Saidas12, setup.Link12), (ParCanais.Saidas34, setup.Link34) })
        {
            var agora = _link.Ligado(par);
            if (agora != antes)
                lista.Add(new MudancaSalvar($"Link {LinkCanais.Rotulo(par)}", antes ? "ligado" : "desligado", agora ? "ligado" : "desligado"));
        }
        var equilibrios = new List<DiferencaPorCanal>();
        for (var canal = 0; canal < Equilibrio.Canais; canal++)
        {
            foreach (var limiter in new[] { false, true })
            {
                var antes = setup.Equilibrio.Valor(canal, limiter);
                var agora = equilibrio.Valor(canal, limiter);
                if (antes != agora)
                    equilibrios.Add(new DiferencaPorCanal(limiter ? "equilibrio-limitador" : "equilibrio-ganho", canal, canal, "Equilíbrio",
                        Camadas.ExibirDb(antes), Camadas.ExibirDb(agora), limiter ? " · limitador" : " · ganho"));
            }
        }
        lista.AddRange(Linhas(equilibrios, saidas));
        return lista;
    }

    /// <summary>As diferenças por canal em linhas da folha do Salvar, pelo nome das saídas.</summary>
    private static List<MudancaSalvar> Linhas(IEnumerable<DiferencaPorCanal> diferencas, IReadOnlyList<SaidaApp> saidas) =>
        RotulosSaida.Juntar(diferencas, saidas).Select(l => new MudancaSalvar(l.Rotulo, l.Antes, l.Depois, l.Canal)).ToList();

    /// <summary>
    /// O caráter da memória contra o lido (a parte da memória: o lido menos o equilíbrio de agora). O
    /// antes e o depois são o valor da tela, o final no aparelho.
    /// </summary>
    private List<MudancaSalvar> MudancasDeCarater(MemoriaCelular memoria, IReadOnlyDictionary<Endereco, Valor> espelho,
        Equilibrio guardado, Equilibrio efetivo)
    {
        var lista = new List<DiferencaPorCanal>();
        foreach (var v in memoria.Carater)
        {
            var lido = espelho.GetValueOrDefault(new Endereco(v.Codigo, v.Indice))?.NoFio;
            var parte = lido is null ? null : Camadas.Subtrair(v.Codigo, v.Indice, lido, efetivo);
            if (parte is not null && Camadas.Iguais(v.Codigo, parte, v.NoFio))
                continue;
            lista.Add(DiferencaPorCanal.De(v.Codigo, v.Indice,
                Camadas.Exibir(v.Codigo, Camadas.Somar(v.Codigo, v.Indice, v.NoFio, guardado)),
                Camadas.Exibir(v.Codigo, lido)));
        }
        return Linhas(lista, SaidasEmUso);
    }

    /// <summary>Uma memória nova com o caráter lido (menos o equilíbrio) e o preset lembrado, ou o motivo de não poder.</summary>
    private (MemoriaCelular? Memoria, string? Motivo) NovaMemoria(Setup setup, string? nomeDigitado,
        IReadOnlyDictionary<Endereco, Valor> espelho, Equilibrio equilibrio, string? idPreset)
    {
        if (NomeValido(nomeDigitado) is not { } nome)
            return (null, GerentePresets.MsgNomeInvalido);
        if (Dados.Memorias.Any(m => m.IdSetup == setup.Id && MesmoNome(m.Nome, nome)))
            return (null, MsgNomeRepetidoMemoria(setup.Nome));
        var (carater, faltam) = Camadas.Tirar(espelho, Camada.Carater);
        if (faltam > 0)
            return (null, MsgIncompleta(faltam));
        return (new MemoriaCelular($"memoria-{Guid.NewGuid():N}", setup.Id, nome, _relogio.GetLocalNow(),
            Camadas.Separar(carater, equilibrio), IdPreset: idPreset), null);
    }

    // ------------------------------------------------------------------ memória do aparelho

    private void AoCarregar(int n)
    {
        lock (_trava)
        {
            _emUso = SomEmUso.DoAparelho(n);
            _ultimoPreset = null; // o som do aparelho: nenhum preset em uso
            _pendente = null;
            if (_situacao == EstadoConferencia.SomDiferente)
                _situacao = EstadoConferencia.Confere;
        }
        Notificar();
    }

    // ------------------------------------------------------------------ aplicar (a regra 3)

    private sealed record Aplicado(bool Ok, string? Motivo, bool Iniciou, int Restam)
    {
        public static Aplicado Falha(string motivo) => new(false, motivo, false, 0);
    }

    /// <summary>
    /// A calibração do Setup e, se há memória, o caráter dela somado ao equilíbrio do Setup e as 15
    /// bandas do preset que ela lembra. <c>Sumiu</c>: ela lembra um preset que não existe mais (o
    /// arquivo foi editado); o alvo vai sem as bandas.
    /// </summary>
    private (IReadOnlyList<ValorCopiado> Alvo, bool Sumiu) Alvo(Setup setup, MemoriaCelular? memoria)
    {
        if (memoria is null)
            return (setup.Calibracao, false);
        IReadOnlyList<ValorCopiado> carater = [.. setup.Calibracao, .. Camadas.Compor(memoria.Carater, setup.Equilibrio)];
        if (memoria.IdPreset is null)
            return (carater, false);
        if (_presets.Obter(memoria.IdPreset) is not { } preset)
        {
            _log.Evento($"memória {memoria.Nome}: o preset lembrado não existe mais; aplica sem as bandas");
            return (carater, true);
        }
        return ([.. carater, .. preset.Valores], false);
    }

    /// <summary>
    /// Escreve o alvo com a cópia antes: só o que difere, uma escrita por vez, e relê tudo. Em caso de
    /// sucesso, a marca da cópia continua (quem chamou conclui, ou deixa pela metade); com
    /// <see cref="Aplicado.Iniciou"/> falso, não havia o que escrever e nenhuma marca começou.
    /// </summary>
    private async Task<Aplicado> AplicarAlvoAsync(IReadOnlyList<ValorCopiado> alvo, OperacaoPendente op, bool refazendo)
    {
        if (!SessaoAberta)
            return Aplicado.Falha(Sessao.MotivoSemConexao);
        if (_estado.Lendo)
            return Aplicado.Falha(Escritor.MsgLendo);
        if (!_estado.Pronto)
            return Aplicado.Falha(Escritor.MsgSemLeitura);
        if (!refazendo && _copia.MotivoBloqueio() is { } bloqueio)
            return Aplicado.Falha(bloqueio);

        var copiaAlvo = new Copia(_relogio.GetLocalNow(), alvo, null);
        var (escritas, ilegiveis) = CopiaAntes.Diferencas(copiaAlvo, _estado.Copiar());
        if (ilegiveis > 0)
            return Aplicado.Falha(CopiaAntes.MsgCopiaIlegivel(ilegiveis));
        if (escritas.Count == 0)
            return new Aplicado(true, null, Iniciou: refazendo, Restam: 0); // no Terminar, a marca sai no fim

        if (_sessao.Conferir(escritas[0].Quadro) is { } motivo)
        {
            _log.Recusado(escritas[0].Quadro, motivo);
            return Aplicado.Falha(motivo);
        }
        var copia = await _copia.IniciarAsync(op, manterCopia: refazendo);
        if (!copia.Ok)
            return Aplicado.Falha(copia.Motivo!);

        _log.Evento($"{op.Descricao}: {escritas.Count} ajustes diferentes");
        var falhas = 0;
        string? ultimo = null;
        foreach (var e in escritas)
        {
            var r = await _escritor.EscreverAsync(e.Parametro, e.Indice, e.Valor, espelhar: false, daCopia: true);
            if (!r.Ok || r.Diferente)
            {
                falhas++;
                ultimo = r.Motivo ?? $"o aparelho ficou em {r.Valor?.NoFio}";
            }
        }
        if (falhas > 0)
        {
            if (SessaoAberta)
                _copia.MarcarPelaMetade();
            else
                _copia.Interromper();
            return Aplicado.Falha(SessaoAberta ? MsgNaoEscritos(falhas, ultimo) : MsgCaiu);
        }

        var retrato = await _leitura.LerNovaAsync();
        if (retrato is not { Completo: true })
        {
            if (!SessaoAberta)
            {
                _copia.Interromper();
                return Aplicado.Falha(MsgCaiu);
            }
            _copia.MarcarPelaMetade();
            return Aplicado.Falha(GerenteMemorias.MsgLeituraParou);
        }
        var (restam, _) = CopiaAntes.Diferencas(copiaAlvo, _estado.Copiar());
        return new Aplicado(true, null, Iniciou: true, Restam: restam.Count);
    }

    // ------------------------------------------------------------------ uso das memórias (os 3 botões)

    /// <summary>
    /// A memória foi usada: vai para o começo das usadas por último. Se ela não está nos botões, entra
    /// numa vaga vazia ou no lugar da usada há mais tempo; os outros botões não mudam de lugar.
    /// </summary>
    public static UsoDoSetup Usar(UsoDoSetup uso, string idMemoria)
    {
        var botoes = Normalizar(uso.Botoes);
        if (!botoes.Contains(idMemoria))
        {
            var vaga = botoes.IndexOf(null);
            if (vaga < 0)
            {
                var antiga = -1;
                vaga = 0;
                for (var i = 0; i < botoes.Count; i++)
                {
                    var posicao = uso.Recentes.ToList().IndexOf(botoes[i]!);
                    var idade = posicao < 0 ? int.MaxValue : posicao;
                    if (idade > antiga)
                    {
                        antiga = idade;
                        vaga = i;
                    }
                }
            }
            botoes[vaga] = idMemoria;
        }
        return new UsoDoSetup([idMemoria, .. uso.Recentes.Where(r => r != idMemoria)], botoes);
    }

    /// <summary>A memória sumiu: sai das usadas; a vaga dela nos botões fica com a usada por último fora dos botões, ou vazia.</summary>
    public static UsoDoSetup Remover(UsoDoSetup uso, string idMemoria)
    {
        var recentes = uso.Recentes.Where(r => r != idMemoria).ToArray();
        var botoes = Normalizar(uso.Botoes);
        var vaga = botoes.IndexOf(idMemoria);
        if (vaga >= 0)
            botoes[vaga] = recentes.FirstOrDefault(r => !botoes.Contains(r));
        return new UsoDoSetup(recentes, botoes);
    }

    private static List<string?> Normalizar(IReadOnlyList<string?> botoes)
    {
        var lista = botoes.Take(QuantosBotoes).ToList();
        while (lista.Count < QuantosBotoes)
            lista.Add(null);
        return lista;
    }

    private static IReadOnlyDictionary<string, UsoDoSetup> Com(IReadOnlyDictionary<string, UsoDoSetup> uso, string idSetup, UsoDoSetup novo) =>
        new Dictionary<string, UsoDoSetup>(uso) { [idSetup] = novo };

    private static MemoriaCelular? AtivaEm(DadosSetups dados, string idSetup)
    {
        foreach (var id in dados.UsoDe(idSetup).Recentes)
        {
            if (dados.Memorias.FirstOrDefault(m => m.Id == id && m.IdSetup == idSetup) is { } m)
                return m;
        }
        return null;
    }

    /// <summary>A referência da conferência: o último Setup usado; no primeiro uso, o Casa 2.1.</summary>
    private static Setup? ReferenciaEm(DadosSetups dados) =>
        dados.Setups.FirstOrDefault(s => s.Id == dados.UltimoSetup)
        ?? dados.Setups.FirstOrDefault(s => s.Id == ConhecimentoHerdado.IdSetupCasa)
        ?? dados.Setups.FirstOrDefault();

    private static bool MesmoNome(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    // ------------------------------------------------------------------ guardado

    /// <summary>O que vem dos protótipos (CAP-6): o Casa 2.1 com a FILME, ativa e no primeiro botão.</summary>
    public static DadosSetups Herdados() => new(
        [ConhecimentoHerdado.SetupCasa],
        [ConhecimentoHerdado.MemoriaFilme],
        ConhecimentoHerdado.IdSetupCasa,
        new Dictionary<string, UsoDoSetup>
        {
            [ConhecimentoHerdado.IdSetupCasa] = new([ConhecimentoHerdado.IdMemoriaFilme], [ConhecimentoHerdado.IdMemoriaFilme, null, null]),
        });

    /// <summary>Grava os dados (com duas tentativas); só se gravou eles passam a valer.</summary>
    private async Task<bool> GravarAsync(DadosSetups novos)
    {
        await _gravacao.WaitAsync();
        try
        {
            if (!await Gravacao.GravarAsync(_repositorio, ParaJson(novos)))
            {
                _log.Evento("não consegui guardar os Setups no celular");
                return false;
            }
            lock (_trava)
                _dados = novos;
        }
        finally
        {
            _gravacao.Release();
        }
        Notificar();
        return true;
    }

    /// <summary>Muda o uso (último Setup, usadas, botões): vale na hora, mesmo se não gravar (fica só o log).</summary>
    private async Task GravarUsoAsync(Func<DadosSetups, DadosSetups> mudar)
    {
        await _gravacao.WaitAsync();
        try
        {
            DadosSetups novos;
            lock (_trava)
            {
                novos = mudar(_dados);
                _dados = novos;
            }
            if (!await Gravacao.GravarAsync(_repositorio, ParaJson(novos)))
                _log.Evento("não consegui guardar o uso das memórias no celular");
        }
        finally
        {
            _gravacao.Release();
        }
        Notificar();
    }

    private async Task<bool> GravarMarcaAsync(MarcaMudo? marca)
    {
        var gravou = await Gravacao.GravarAsync(_repositorioMudo, MarcaParaJson(marca));
        if (!gravou)
        {
            _log.Evento(marca is null ? "não consegui apagar a marca do mudo no celular" : "não consegui guardar a marca do mudo no celular");
            if (marca is not null)
                return false;
        }
        lock (_trava)
            _marca = marca;
        Notificar();
        return true;
    }

    private async Task CarregarAsync()
    {
        DadosSetups? lidos = null;
        if (await Gravacao.LerAsync(_repositorio) is { } json)
        {
            try
            {
                lidos = DeJson(json);
            }
            catch (Exception e) when (e is JsonException or InvalidOperationException or FormatException)
            {
                // Arquivo estragado: volta ao herdado, sem erro.
            }
            if (lidos is null)
                _log.Evento("setups.json ilegível: voltei ao herdado (Casa 2.1 e FILME)");
        }
        MarcaMudo? marca = null;
        if (await Gravacao.LerAsync(_repositorioMudo) is { } textoMarca)
        {
            try
            {
                marca = MarcaDeJson(textoMarca);
            }
            catch (Exception e) when (e is JsonException or InvalidOperationException or FormatException)
            {
                // Sem marca legível: a conferência decide pelo que ler.
            }
        }
        lock (_trava)
        {
            if (lidos is not null)
                _dados = lidos;
            _marca ??= marca;
        }
        Notificar();
        // As playlists que esperam a memória delas (as do playlists.json antigo) procuram pelo nome.
        await AdotarPlaylistsAsync();
    }

    /// <summary>O JSON do <c>setups.json</c> (formato 2: o equilíbrio por canal e os equipamentos de cada Setup).</summary>
    public static string ParaJson(DadosSetups dados)
    {
        using var fluxo = new MemoryStream();
        using (var w = NovoEscritor(fluxo))
        {
            w.WriteStartObject();
            w.WriteNumber("formato", Formato);
            if (dados.UltimoSetup is { } ultimo)
                w.WriteString("ultimoSetup", ultimo);
            else
                w.WriteNull("ultimoSetup");
            w.WriteStartArray("setups");
            foreach (var s in dados.Setups)
            {
                w.WriteStartObject();
                w.WriteString("id", s.Id);
                w.WriteString("nome", s.Nome);
                w.WriteString("quando", s.Quando.ToString("O", Fio));
                // O equilíbrio por canal: CH1 a CH4, na ordem.
                w.WriteStartObject("equilibrio");
                foreach (var limiter in new[] { false, true })
                {
                    w.WriteStartArray(limiter ? "limiter" : "ganho");
                    for (var c = 0; c < Equilibrio.Canais; c++)
                        w.WriteNumberValue(s.Equilibrio.Valor(c, limiter));
                    w.WriteEndArray();
                }
                w.WriteEndObject();
                w.WriteBoolean("link12", s.Link12);
                w.WriteBoolean("link34", s.Link34);
                EscreverValores(w, "calibracao", s.Calibracao);
                EscreverEquipamentos(w, s.Equipamentos);
                var uso = dados.UsoDe(s.Id);
                w.WriteStartArray("recentes");
                foreach (var r in uso.Recentes)
                    w.WriteStringValue(r);
                w.WriteEndArray();
                w.WriteStartArray("botoes");
                foreach (var b in Normalizar(uso.Botoes))
                {
                    if (b is null)
                        w.WriteNullValue();
                    else
                        w.WriteStringValue(b);
                }
                w.WriteEndArray();
                w.WriteEndObject();
            }
            w.WriteEndArray();
            w.WriteStartArray("memorias");
            foreach (var m in dados.Memorias)
            {
                w.WriteStartObject();
                w.WriteString("id", m.Id);
                w.WriteString("setup", m.IdSetup);
                w.WriteString("nome", m.Nome);
                w.WriteString("quando", m.Quando.ToString("O", Fio));
                if (m.IdPreset is { } preset)
                    w.WriteString("preset", preset);
                EscreverValores(w, "carater", m.Carater);
                w.WriteEndObject();
            }
            w.WriteEndArray();
            w.WriteEndObject();
        }
        return Encoding.UTF8.GetString(fluxo.ToArray());
    }

    /// <summary>
    /// Lê o <c>setups.json</c>. Fora dos formatos 1 e 2, sem nenhum Setup válido ou com outra forma:
    /// null (o gerente volta ao herdado). Uma entrada com id ou nome inválido, repetida, ou uma memória
    /// de um Setup que não existe, é descartada; as outras ficam. O herdado (o selo e a nota) vem pelo
    /// id. O preset lembrado de cada memória vem do campo <c>preset</c> (ausente = nenhum; na FILME
    /// herdada, ausente = o Cinema, como num <c>setups.json</c> gravado pela 8c).
    /// <list type="bullet">
    /// <item><b>Formato 2</b> (história 8e): o equilíbrio por canal (<c>ganho</c> e <c>limiter</c>, CH1 a
    /// CH4) e os equipamentos de cada Setup. Um equipamento ilegível é descartado; os outros ficam, e
    /// as ligações para ele se desfazem (<see cref="Ligacoes.Sanear"/>).</item>
    /// <item><b>Formato 1</b> (8c e 8d): <c>ganho1</c>/<c>limiter1</c>/<c>ganho2</c>/<c>limiter2</c> viram os
    /// canais 1 e 2, e os outros ficam 0. O Casa 2.1 herdado recebe os equipamentos do herdado e passa
    /// ao Link 3–4 ligado; um Setup próprio fica sem equipamentos.</item>
    /// </list>
    /// </summary>
    public static DadosSetups? DeJson(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var raiz = doc.RootElement;
        if (raiz.ValueKind != JsonValueKind.Object
            || !raiz.TryGetProperty("formato", out var f) || !f.TryGetInt32(out var formato) || formato is not (Formato or FormatoAntigo)
            || !raiz.TryGetProperty("setups", out var setupsJson) || setupsJson.ValueKind != JsonValueKind.Array)
            return null;

        var setups = new List<Setup>();
        var usos = new Dictionary<string, (List<string> Recentes, List<string?> Botoes)>();
        foreach (var item in setupsJson.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object || Texto(item, "id") is not { Length: > 0 } id
                || setups.Any(s => s.Id == id) || NomeGuardado(Texto(item, "nome"), id) is not { } nome
                || setups.Any(s => MesmoNome(s.Nome, nome)))
                continue;
            var eq = Equilibrio.Zero;
            if (item.TryGetProperty("equilibrio", out var e) && e.ValueKind == JsonValueKind.Object)
            {
                if (formato == FormatoAntigo)
                {
                    eq = new Equilibrio(NumeroEq(e, "ganho1"), NumeroEq(e, "limiter1"), NumeroEq(e, "ganho2"), NumeroEq(e, "limiter2"));
                }
                else
                {
                    for (var c = 0; c < Equilibrio.Canais; c++)
                    {
                        eq = eq.Com(c, limiter: false, NumeroEq(e, "ganho", c));
                        eq = eq.Com(c, limiter: true, NumeroEq(e, "limiter", c));
                    }
                }
            }
            var herdado = id == ConhecimentoHerdado.IdSetupCasa;
            var link34 = Booleano(item, "link34");
            IReadOnlyList<Equipamento> equipamentos;
            if (formato == FormatoAntigo)
            {
                // A migração da 8e: o Casa 2.1 herdado ganha os equipamentos e o Link 3–4 ligado. Um
                // par com link é uma saída só: o segundo canal fica com o equilíbrio do mestre.
                equipamentos = herdado ? ConhecimentoHerdado.EquipamentosCasa : [];
                link34 = link34 || herdado;
                foreach (var (mestre, ligado) in new[] { (0, Booleano(item, "link12")), (2, link34) })
                {
                    if (!ligado)
                        continue;
                    foreach (var limiter in new[] { false, true })
                        eq = eq.Com(mestre + 1, limiter, eq.Valor(mestre, limiter));
                }
            }
            else
            {
                equipamentos = LerEquipamentos(item);
            }
            setups.Add(new Setup(id, nome, Quando(item), LerValores(item, "calibracao", Camada.Calibracao), eq,
                Booleano(item, "link12"), link34, herdado, herdado ? ConhecimentoHerdado.NotaSetupCasa : null)
            {
                Equipamentos = equipamentos,
            });
            usos[id] = (Ids(item, "recentes").OfType<string>().ToList(), Ids(item, "botoes"));
        }
        if (setups.Count == 0)
            return null;

        var memorias = new List<MemoriaCelular>();
        if (raiz.TryGetProperty("memorias", out var memoriasJson) && memoriasJson.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in memoriasJson.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object || Texto(item, "id") is not { Length: > 0 } id
                    || memorias.Any(m => m.Id == id) || Texto(item, "setup") is not { } idSetup
                    || setups.All(s => s.Id != idSetup) || NomeValido(Texto(item, "nome")) is not { } nome
                    || memorias.Any(m => m.IdSetup == idSetup && MesmoNome(m.Nome, nome)))
                    continue;
                var herdada = id == ConhecimentoHerdado.IdMemoriaFilme;
                // Sem o campo "preset" (um setups.json da 8c), a FILME herdada lembra o Cinema; as outras, nenhum.
                var idPreset = Texto(item, "preset") is { Length: > 0 } preset ? preset
                    : herdada && !item.TryGetProperty("preset", out _) ? ConhecimentoHerdado.IdCinema
                    : null;
                memorias.Add(new MemoriaCelular(id, idSetup, nome, Quando(item), LerValores(item, "carater", Camada.Carater),
                    herdada, herdada ? ConhecimentoHerdado.NotaMemoriaFilme : null, idPreset));
            }
        }

        var uso = new Dictionary<string, UsoDoSetup>();
        foreach (var s in setups)
        {
            var dele = memorias.Where(m => m.IdSetup == s.Id).Select(m => m.Id).ToHashSet();
            var (recentes, botoes) = usos[s.Id];
            var validos = recentes.Where(dele.Contains).Distinct().ToArray();
            var vistos = new HashSet<string>();
            var fixos = Normalizar(botoes).Select(b => b is not null && dele.Contains(b) && vistos.Add(b) ? b : null).ToArray();
            uso[s.Id] = new UsoDoSetup(validos, fixos);
        }

        var ultimo = Texto(raiz, "ultimoSetup") is { } u && setups.Any(s => s.Id == u) ? u : null;
        return new DadosSetups(setups, memorias, ultimo, uso);
    }

    /// <summary>O JSON do <c>setup-mudo.json</c>: a marca, ou <c>"marca": null</c> quando não há.</summary>
    public static string MarcaParaJson(MarcaMudo? marca)
    {
        using var fluxo = new MemoryStream();
        using (var w = NovoEscritor(fluxo))
        {
            w.WriteStartObject();
            w.WriteNumber("formato", FormatoMudo);
            if (marca is null)
            {
                w.WriteNull("marca");
            }
            else
            {
                w.WriteStartObject("marca");
                if (marca.IdSetup is { } id)
                    w.WriteString("setup", id);
                else
                    w.WriteNull("setup");
                w.WriteStartArray("muteAnterior");
                foreach (var m in marca.MuteAnterior)
                    w.WriteStringValue(m);
                w.WriteEndArray();
                w.WriteString("quando", marca.Quando.ToString("O", Fio));
                w.WriteEndObject();
            }
            w.WriteEndObject();
        }
        return Encoding.UTF8.GetString(fluxo.ToArray());
    }

    /// <summary>Lê a marca do mudo; null se não há (ou fora do formato).</summary>
    public static MarcaMudo? MarcaDeJson(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var raiz = doc.RootElement;
        if (raiz.ValueKind != JsonValueKind.Object
            || !raiz.TryGetProperty("formato", out var f) || !f.TryGetInt32(out var formato) || formato != FormatoMudo
            || !raiz.TryGetProperty("marca", out var m) || m.ValueKind != JsonValueKind.Object
            || !m.TryGetProperty("muteAnterior", out var anterior) || anterior.ValueKind != JsonValueKind.Array)
            return null;
        var mutes = anterior.EnumerateArray().Select(v => v.ValueKind == JsonValueKind.String ? v.GetString()! : "0").ToList();
        while (mutes.Count < Parametro.Canais)
            mutes.Add("0");
        return new MarcaMudo(Texto(m, "setup"), mutes.Take(Parametro.Canais).ToArray(), Quando(m));
    }

    private static void EscreverValores(Utf8JsonWriter w, string nome, IEnumerable<ValorCopiado> valores)
    {
        w.WriteStartArray(nome);
        foreach (var v in valores)
        {
            w.WriteStartObject();
            w.WriteString("codigo", v.Codigo);
            if (v.Indice is { } i)
                w.WriteNumber("indice", i);
            w.WriteString("noFio", v.NoFio);
            w.WriteEndObject();
        }
        w.WriteEndArray();
    }

    /// <summary>Os valores guardados, só os da camada e sem repetir, na ordem da leitura.</summary>
    private static IReadOnlyList<ValorCopiado> LerValores(JsonElement item, string nome, Camada camada)
    {
        var lidos = new Dictionary<(string, int?), ValorCopiado>();
        if (item.TryGetProperty(nome, out var vs) && vs.ValueKind == JsonValueKind.Array)
        {
            foreach (var v in vs.EnumerateArray())
            {
                if (v.ValueKind != JsonValueKind.Object || Texto(v, "codigo") is not { } codigo || Texto(v, "noFio") is not { } noFio)
                    continue;
                int? indice = v.TryGetProperty("indice", out var i) && i.TryGetInt32(out var n) ? n : null;
                lidos.TryAdd((codigo, indice), new ValorCopiado(codigo, indice, noFio));
            }
        }
        return Camadas.Consultas(camada)
            .Select(c => lidos.GetValueOrDefault((c.Parametro.Codigo, c.Indice)))
            .OfType<ValorCopiado>()
            .ToArray();
    }

    /// <summary>
    /// O nome guardado, se vale: a regra dos presets, ou o nome de fábrica do herdado ("Casa 2.1"
    /// tem um ponto, que a regra não deixa digitar).
    /// </summary>
    private static string? NomeGuardado(string? nome, string id) =>
        NomeValido(nome) ?? (id == ConhecimentoHerdado.IdSetupCasa && nome == ConhecimentoHerdado.NomeSetupCasa ? nome : null);

    private static List<string?> Ids(JsonElement item, string nome)
    {
        var lista = new List<string?>();
        if (item.TryGetProperty(nome, out var a) && a.ValueKind == JsonValueKind.Array)
        {
            foreach (var v in a.EnumerateArray())
                lista.Add(v.ValueKind == JsonValueKind.String ? v.GetString() : null);
        }
        return lista;
    }

    private static decimal NumeroEq(JsonElement o, string nome)
    {
        if (!o.TryGetProperty(nome, out var p) || p.ValueKind != JsonValueKind.Number || !p.TryGetDecimal(out var v))
            return 0m;
        return Eq(v);
    }

    /// <summary>O valor do canal (0 a 3) na lista <paramref name="nome"/> do equilíbrio (formato 2), ou 0.</summary>
    private static decimal NumeroEq(JsonElement o, string nome, int canal)
    {
        if (!o.TryGetProperty(nome, out var a) || a.ValueKind != JsonValueKind.Array || a.GetArrayLength() <= canal)
            return 0m;
        var p = a[canal];
        return p.ValueKind == JsonValueKind.Number && p.TryGetDecimal(out var v) ? Eq(v) : 0m;
    }

    private static decimal Eq(decimal v) => Math.Clamp(decimal.Round(v, 1), Camadas.EquilibrioMinimo, Camadas.EquilibrioMaximo);

    // ------------------------------------------------------------------ equipamentos no JSON (formato 2)

    private static string NomeGrupo(GrupoEquipamento g) => g switch
    {
        GrupoEquipamento.AmplificaETocam => "ativo",
        GrupoEquipamento.Amplificador => "amplificador",
        GrupoEquipamento.Repassa => "repasse",
        GrupoEquipamento.SoRecebe => "receptor",
        _ => "alto-falante",
    };

    private static GrupoEquipamento? GrupoDe(string? nome) => nome switch
    {
        "ativo" => GrupoEquipamento.AmplificaETocam,
        "amplificador" => GrupoEquipamento.Amplificador,
        "repasse" => GrupoEquipamento.Repassa,
        "receptor" => GrupoEquipamento.SoRecebe,
        "alto-falante" => GrupoEquipamento.AltoFalante,
        _ => null,
    };

    private static string NomeFuncao(FuncaoAcustica f) => f switch
    {
        FuncaoAcustica.Sub => "sub",
        FuncaoAcustica.Grave => "grave",
        FuncaoAcustica.MedioGrave => "medio-grave",
        FuncaoAcustica.Medio => "medio",
        FuncaoAcustica.Agudo => "agudo",
        FuncaoAcustica.SuperAgudo => "super-agudo",
        FuncaoAcustica.FullRange => "full-range",
        _ => "outra",
    };

    private static FuncaoAcustica? FuncaoDe(string? nome) =>
        Ligacoes.Funcoes.Cast<FuncaoAcustica?>().FirstOrDefault(f => NomeFuncao(f!.Value) == nome);

    private static void EscreverEquipamentos(Utf8JsonWriter w, IReadOnlyList<Equipamento> equipamentos)
    {
        w.WriteStartArray("equipamentos");
        foreach (var e in equipamentos)
        {
            w.WriteStartObject();
            w.WriteString("id", e.Id);
            w.WriteString("grupo", NomeGrupo(e.Grupo));
            w.WriteString("categoria", e.Categoria);
            w.WriteString("nome", e.Nome);
            if (e.Modelo is { } modelo)
                w.WriteString("modelo", modelo);
            if (e.Funcao is { } funcao)
                w.WriteString("funcao", NomeFuncao(funcao));
            if (e.Nota is { } nota)
                w.WriteString("nota", nota);
            EscreverDados(w, e.Dados);
            if (e.Grupo != GrupoEquipamento.AltoFalante)
            {
                w.WriteStartArray(e.Grupo == GrupoEquipamento.Amplificador ? "canais" : "entradas");
                foreach (var entrada in e.Entradas)
                {
                    w.WriteStartObject();
                    w.WriteNumber("n", entrada.Numero);
                    w.WritePropertyName("fonte");
                    EscreverFonte(w, entrada.Fonte);
                    w.WriteEndObject();
                }
                w.WriteEndArray();
            }
            if (e.Grupo == GrupoEquipamento.Amplificador)
            {
                w.WriteStartArray("bridges");
                foreach (var b in e.Bridges)
                {
                    w.WriteStartArray();
                    w.WriteNumberValue(b.A);
                    w.WriteNumberValue(b.B);
                    w.WriteEndArray();
                }
                w.WriteEndArray();
            }
            if (e.Grupo == GrupoEquipamento.AltoFalante)
            {
                if (e.Ligado is { } l)
                {
                    w.WriteStartObject("ligado");
                    w.WriteString("equipamento", l.IdModulo);
                    if (l.Canal is { } c)
                        w.WriteNumber("canal", c);
                    if (l.Bridge is { } b)
                    {
                        w.WriteStartArray("bridge");
                        w.WriteNumberValue(b.A);
                        w.WriteNumberValue(b.B);
                        w.WriteEndArray();
                    }
                    w.WriteEndObject();
                }
                else
                {
                    w.WriteNull("ligado");
                }
            }
            w.WriteEndObject();
        }
        w.WriteEndArray();
    }

    /// <summary>A fonte: <c>"CH1"</c> a <c>"CH4"</c>, <c>{"equipamento": id}</c> ou <c>null</c>.</summary>
    private static void EscreverFonte(Utf8JsonWriter w, FonteSinal? fonte)
    {
        if (fonte?.Canal is { } canal)
        {
            w.WriteStringValue($"CH{canal + 1}");
        }
        else if (fonte?.IdEquipamento is { } id)
        {
            w.WriteStartObject();
            w.WriteString("equipamento", id);
            w.WriteEndObject();
        }
        else
        {
            w.WriteNullValue();
        }
    }

    private static void EscreverDados(Utf8JsonWriter w, DadosTecnicos d)
    {
        w.WriteStartObject("dados");
        void Numero(string nome, decimal? v)
        {
            if (v is { } n)
                w.WriteNumber(nome, n);
        }
        Numero("diametro", d.Diametro);
        Numero("impedancia", d.Impedancia);
        Numero("potenciaRms", d.PotenciaRms);
        Numero("sensibilidade", d.Sensibilidade);
        Numero("faixaMinima", d.FaixaMinima);
        Numero("faixaMaxima", d.FaixaMaxima);
        Numero("ganho", d.Ganho);
        if (d.Canais is { } canais)
            w.WriteNumber("canais", canais);
        if (d.Vias.Count > 0)
        {
            w.WriteStartArray("vias");
            foreach (var v in d.Vias)
            {
                w.WriteStartObject();
                w.WriteString("descricao", v.Descricao);
                Numero("diametro", v.Diametro);
                w.WriteEndObject();
            }
            w.WriteEndArray();
        }
        w.WriteEndObject();
    }

    /// <summary>
    /// Os equipamentos de um Setup (formato 2). Fica o primeiro de cada id. Um bridge que não vale sai
    /// (o módulo fica). Um equipamento ilegível (sem id, grupo, nome ou categoria válidos, ou com um
    /// campo de outra forma) é descartado; os outros ficam, e as ligações para ele se desfazem.
    /// </summary>
    private static IReadOnlyList<Equipamento> LerEquipamentos(JsonElement setup)
    {
        var lista = new List<Equipamento>();
        var vistos = new HashSet<string>();
        if (!setup.TryGetProperty("equipamentos", out var a) || a.ValueKind != JsonValueKind.Array)
            return lista;
        foreach (var item in a.EnumerateArray())
        {
            var id = item.ValueKind == JsonValueKind.Object ? Texto(item, "id") : null;
            if (id is not null && !vistos.Add(id))
                continue; // fica o primeiro de cada id
            Equipamento? e = null;
            try
            {
                e = LerEquipamento(item);
            }
            catch (Exception ex) when (ex is InvalidOperationException or FormatException or ArgumentException)
            {
                // Um campo com outra forma: ilegível.
            }
            if (e is not null && Ligacoes.SemBridgesInvalidos(e) is var limpo && Ligacoes.ValidarUm(limpo) is null)
                lista.Add(limpo);
        }
        return Ligacoes.Sanear(lista);
    }

    private static Equipamento? LerEquipamento(JsonElement item)
    {
        if (item.ValueKind != JsonValueKind.Object || Texto(item, "id") is not { Length: > 0 } id
            || GrupoDe(Texto(item, "grupo")) is not { } grupo
            || Ligacoes.TextoValido(Texto(item, "nome")) is not { } nome
            || Ligacoes.TextoValido(Texto(item, "categoria")) is not { } categoria)
            return null;
        FuncaoAcustica? funcao = null;
        if (Texto(item, "funcao") is { } textoFuncao && (funcao = FuncaoDe(textoFuncao)) is null)
            return null;

        var entradas = new List<Entrada>();
        if (item.TryGetProperty(grupo == GrupoEquipamento.Amplificador ? "canais" : "entradas", out var es))
        {
            if (es.ValueKind != JsonValueKind.Array)
                return null;
            foreach (var x in es.EnumerateArray())
            {
                if (x.ValueKind != JsonValueKind.Object || !x.TryGetProperty("n", out var n) || !n.TryGetInt32(out var numero))
                    return null;
                FonteSinal? fonte = null;
                if (x.TryGetProperty("fonte", out var fj) && fj.ValueKind != JsonValueKind.Null)
                {
                    fonte = LerFonte(fj);
                    if (fonte is null)
                        return null;
                }
                entradas.Add(new Entrada(numero, fonte));
            }
        }

        var bridges = new List<Bridge>();
        if (item.TryGetProperty("bridges", out var bs))
        {
            if (bs.ValueKind != JsonValueKind.Array)
                return null;
            foreach (var b in bs.EnumerateArray())
            {
                if (LerBridge(b) is not { } bridge)
                    return null;
                bridges.Add(bridge);
            }
        }

        LigacaoAltoFalante? ligado = null;
        if (item.TryGetProperty("ligado", out var l) && l.ValueKind != JsonValueKind.Null)
        {
            if (l.ValueKind != JsonValueKind.Object || Texto(l, "equipamento") is not { Length: > 0 } modulo)
                return null;
            // "canal": null (e sem bridge) é "sem ligação"; um canal de outra forma deixa o equipamento ilegível.
            int? canal = l.TryGetProperty("canal", out var c) && c.ValueKind != JsonValueKind.Null ? c.GetInt32() : null;
            Bridge? bridge = null;
            if (l.TryGetProperty("bridge", out var bj) && bj.ValueKind != JsonValueKind.Null && (bridge = LerBridge(bj)) is null)
                return null;
            ligado = canal is null && bridge is null ? null : new LigacaoAltoFalante(modulo, canal, bridge);
        }

        var modelo = Texto(item, "modelo");
        return new Equipamento(id, grupo, categoria, nome, string.IsNullOrWhiteSpace(modelo) ? null : modelo.Trim(), funcao)
        {
            Dados = LerDados(item),
            Entradas = entradas,
            Bridges = bridges,
            Ligado = ligado,
            Nota = Texto(item, "nota"),
        };
    }

    private static FonteSinal? LerFonte(JsonElement f)
    {
        if (f.ValueKind == JsonValueKind.String)
        {
            var texto = f.GetString();
            return texto is { Length: 3 } && texto.StartsWith("CH", StringComparison.Ordinal) && texto[2] is >= '1' and <= '4'
                ? FonteSinal.DoCanal(texto[2] - '1')
                : null;
        }
        return f.ValueKind == JsonValueKind.Object && Texto(f, "equipamento") is { Length: > 0 } id ? FonteSinal.DoEquipamento(id) : null;
    }

    private static Bridge? LerBridge(JsonElement b) =>
        b.ValueKind == JsonValueKind.Array && b.GetArrayLength() == 2 && b[0].TryGetInt32(out var x) && b[1].TryGetInt32(out var y)
            ? new Bridge(x, y)
            : null;

    private static DadosTecnicos LerDados(JsonElement item)
    {
        if (!item.TryGetProperty("dados", out var d) || d.ValueKind != JsonValueKind.Object)
            return DadosTecnicos.Nenhum;
        decimal? Numero(JsonElement o, string nome) =>
            o.TryGetProperty(nome, out var p) && p.ValueKind == JsonValueKind.Number && p.TryGetDecimal(out var v) ? v : null;
        int? canais = d.TryGetProperty("canais", out var c) && c.ValueKind == JsonValueKind.Number && c.TryGetInt32(out var n) ? n : null;
        var vias = new List<Via>();
        if (d.TryGetProperty("vias", out var vs) && vs.ValueKind == JsonValueKind.Array)
        {
            foreach (var v in vs.EnumerateArray())
            {
                if (v.ValueKind == JsonValueKind.Object && Texto(v, "descricao") is { Length: > 0 } descricao)
                    vias.Add(new Via(descricao, Numero(v, "diametro")));
            }
        }
        return new DadosTecnicos(Numero(d, "diametro"), Numero(d, "impedancia"), Numero(d, "potenciaRms"), Numero(d, "sensibilidade"),
            Numero(d, "faixaMinima"), Numero(d, "faixaMaxima"), Numero(d, "ganho"), canais)
        {
            Vias = vias,
        };
    }

    private static bool Booleano(JsonElement o, string nome) =>
        o.TryGetProperty(nome, out var p) && p.ValueKind == JsonValueKind.True;

    private static DateTimeOffset Quando(JsonElement o) =>
        Texto(o, "quando") is { } q && DateTimeOffset.TryParse(q, Fio, DateTimeStyles.RoundtripKind, out var d) ? d : DateTimeOffset.MinValue;

    private static string? Texto(JsonElement o, string nome) =>
        o.TryGetProperty(nome, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;

    private static Utf8JsonWriter NovoEscritor(Stream fluxo) => new(fluxo, new JsonWriterOptions
    {
        Indented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    });

    // ------------------------------------------------------------------ apoio

    private bool SessaoAberta => _sessao.Estado == EstadoSessao.Conectado && _sessao.SegurandoConexao;

    private static ResultadoCopia ParaCopia(ResultadoSetup r) =>
        r.Ok ? ResultadoCopia.Sucesso : ResultadoCopia.Falha(r.Mensagem ?? r.Motivo ?? "falhou");

    /// <summary>Guarda a frase do resultado como aviso e devolve o resultado.</summary>
    private ResultadoSetup Terminar(ResultadoSetup r)
    {
        lock (_trava)
            _aviso = r.Mensagem;
        if (!r.Ok)
            _log.Evento($"setup: {r.Mensagem ?? r.Motivo}");
        Notificar();
        return r;
    }

    private void Notificar()
    {
        try
        {
            Mudou?.Invoke();
        }
        catch (Exception e)
        {
            _log.Evento($"falha no aviso dos Setups ({e.GetType().Name})");
        }
    }

    /// <summary>O estado de antes de tocar no Setup, para o Desistir.</summary>
    private sealed record Antes(EstadoConferencia Situacao, string? SetupEmUso, SomEmUso EmUso);
}
