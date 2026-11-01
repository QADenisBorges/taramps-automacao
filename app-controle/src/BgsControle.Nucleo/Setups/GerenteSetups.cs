using System.Globalization;
using System.Text;
using System.Text.Json;
using BgsControle.Nucleo.Escrita;
using BgsControle.Nucleo.Leitura;
using BgsControle.Nucleo.Player;
using BgsControle.Nucleo.Presets;
using BgsControle.Nucleo.Registro;
using BgsControle.Nucleo.Rendimento;
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

    /// <summary>O app zerado (CAP-6): nenhum Setup, nenhuma memória, nenhum último Setup.</summary>
    public static DadosSetups Vazio { get; } = new([], [], null, new Dictionary<string, UsoDoSetup>());
}

/// <summary>
/// Os Setups e as memórias do celular (CAP-4, CAP-16, failure-modes regra 5), no modelo de 29/10/2026:
/// o Setup é só o físico (os equipamentos e as fichas) e a memória guarda toda a regulagem, com o valor
/// final. Guarda tudo em <c>setups.json</c> (formato 4; o 3 sobe sem perda, os formatos 1 e 2 são migrados) e a marca do
/// mudo da dúvida em <c>setup-mudo.json</c>; sem arquivo (ou com ele estragado), começa vazio: nenhum
/// Setup e nenhuma memória (CAP-6, decisão do Denis de 30/10/2026: o app nasce zerado).
/// <list type="bullet">
/// <item><b>Conferência:</b> na primeira leitura completa de cada conexão nova (depois da regra 2 do
/// gerador), compara a regulagem lida com a da memória ativa do último Setup usado. Bate: "Setup
/// confere". Só o ganho, o limitador, o EQ ou o MASTER diferem: "som diferente", sem mutar. A rota, o
/// atraso, a fase ou um corte diferem, o Setup não tem memória ativa, ou não há nenhum Setup: guarda a marca, muta os 4
/// canais (<c>100</c> = 1) e fica "Setup em dúvida" até escolher um Setup, criar um ou seguir sem
/// Setup. Uma dúvida interrompida (marca guardada e os 4 canais lidos mutados) volta sem mutar de novo.</item>
/// <item><b>Link automático</b> (decisão D1 do Denis, 29/10/2026): o link sai das ligações do Setup em
/// uso (<see cref="Ligacoes.Links"/>) e vai para o <see cref="LinkCanais"/> sempre que o Setup passa a
/// valer e quando os equipamentos dele mudam. Sem Setup em uso (na dúvida, ao trocar de Setup, em Seguir
/// sem Setup, ou sem Setup de referência), os links ficam desligados (decisão de 24/10/2026).</item>
/// <item><b>Aplicar</b> uma memória e <b>escolher</b> um Setup passam pela <see cref="CopiaAntes"/>
/// (regra 3): escrevem só o que difere (a regulagem e as bandas do preset lembrado), relêem e conferem.</item>
/// <item>Criar, renomear e apagar Setups e memórias não mandam nada ao aparelho; nem adicionar, editar
/// ou tirar equipamentos (história 8e), que não têm chave. Um equipamento confirmado com modelo vai
/// para a <see cref="BaseModelos"/>.</item>
/// <item>As <b>saídas do app</b> (<see cref="SaidasEmUso"/>) vêm dos equipamentos do Setup em uso e do
/// link (<see cref="Ligacoes.Saidas(IReadOnlyList{Equipamento}, bool, bool)"/>): um canal, ou um par com link, que é uma saída só.</item>
/// <item>O <b>Salvar</b> leva a regulagem para a memória em uso (ou uma nova) e as 15 bandas para um
/// preset (<see cref="GerentePresets"/>). Não manda nada pelo BLE.</item>
/// <item>Cada memória do celular <b>lembra um preset</b> (as 15 bandas), que volta junto quando ela é
/// aplicada; aplicar um preset com uma memória do celular em uso faz ela lembrar esse preset.</item>
/// <item>As <b>playlists</b> (<see cref="GerentePlaylists"/>) são por memória: apagar a memória (ou o
/// Setup dela) apaga a playlist; criar ou renomear memórias, e carregar, adota as pendentes pelo nome.</item>
/// </list>
/// Uma operação por vez.
/// </summary>
public sealed class GerenteSetups
{
    /// <summary>
    /// O formato do <c>setups.json</c>: o 4 acrescenta à memória o nível de rendimento e o master por preset
    /// (história 13c). O 3 é migrado sem perder nada.
    /// </summary>
    public const int Formato = 4;

    /// <summary>O formato da 8f (o Setup só físico, as fichas com origem e a memória com a regulagem). É migrado ao 4 sem perda.</summary>
    public const int Formato3 = 3;

    /// <summary>O formato da 8e (o equilíbrio por canal, a calibração no Setup). É migrado: ficam os Setups e os equipamentos.</summary>
    public const int Formato2 = 2;

    /// <summary>O formato da 8c e da 8d. É migrado como o 2.</summary>
    public const int FormatoAntigo = 1;

    /// <summary>O formato do <c>setup-mudo.json</c> (não mudou na 8e).</summary>
    public const int FormatoMudo = 1;

    /// <summary>Os tipos registrados na <see cref="CopiaAntes"/>. O alvo da marca é o id da memória ou do Setup.</summary>
    public const string TipoAplicarMemoria = "aplicar-memoria-celular";

    public const string TipoAplicarSetup = "aplicar-setup";

    /// <summary>O tipo do "Manter a calibração" de antes da 8f: uma marca antiga é retomada como aplicar a memória.</summary>
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
    public const string MsgNomeRepetidoSetup = "Já existe um Setup com esse nome.";
    public const string MsgNomeSetupInvalido = "Use de 1 a 24 letras, números, espaço, ponto, _ ou -";
    public const string MsgAmbienteInvalido = "Ambiente desconhecido.";
    public const string MsgSoNaEscolha = "Isso só se faz na tela de escolha do Setup.";
    public const string MsgOutroSetup = "A memória é de outro Setup.";
    public const string MsgPedeFolha = "Este som não é de uma memória do celular. Toque em Salvar para guardá-lo.";
    public const string MsgEscolhaPreset = "Escolha atualizar o preset ou criar um preset novo.";
    public const string MsgPresetSumiu = "O preset lembrado não existe mais; as 15 bandas ficaram como estavam.";
    public const string MsgSemConexao = "Sem conexão com o aparelho.";
    public const string MsgCaiu = "A conexão caiu no meio. Ao reconectar, o app mostra o que ficou.";
    public const string MsgSemMudo = "O som não estava mutado.";

    public static string MsgAmbienteDefinido(string setup, AmbienteSetup? ambiente) => ambiente is { } a
        ? $"{setup}: {Ambientes.Rotulo(a)}."
        : $"{setup}: ambiente não informado.";

    public static string MsgNomeRepetidoMemoria(string setup) => $"Já existe uma memória com esse nome no {setup}.";

    /// <summary>
    /// "Nome da memória: Use de 1 a 24 letras…": no Criar Setup novo, a recusa diz que é o nome da primeira
    /// memória, não o do Setup.
    /// </summary>
    public static string MsgNomeMemoria(string motivo) => $"Nome da memória: {motivo}";

    /// <summary>"Nome do preset: Já existe um preset com esse nome.": a recusa diz que é o nome do preset, não o da memória.</summary>
    public static string MsgNomePreset(string motivo) => $"Nome do preset: {motivo}";

    public static string MsgCriada(string nome, string setup) => $"Memória {nome} criada no {setup}.";

    public static string MsgAplicada(string nome) => $"Memória {nome} aplicada.";

    public static string MsgAplicadaDiferente(string nome, int n) => n == 1
        ? $"Memória {nome} aplicada, mas 1 ajuste ficou diferente."
        : $"Memória {nome} aplicada, mas {n} ajustes ficaram diferentes.";

    public static string MsgFicaramDiferentes(int n) => n == 1 ? "1 ajuste ficou diferente." : $"{n} ajustes ficaram diferentes.";

    public static string MsgLinkNaoGuardado(string motivo) => $"O link do Setup não foi guardado: {motivo}";

    /// <summary>"Não consegui desligar os links: …" (sem Setup em uso, decisão do Denis de 24/10/2026).</summary>
    public static string MsgLinksNaoDesligados(string motivo) => $"Não consegui desligar os links: {motivo}";

    public static string MsgSomDiferente(string nome) => $"Som diferente da {nome}";

    public static string MsgSetupEscolhido(string nome) => $"Setup {nome} em uso.";

    /// <summary>"Setup Carro criado, com a memória ESTRADA." (o som do aparelho vira a primeira memória dele).</summary>
    public static string MsgSetupCriado(string nome, string memoria) => $"Setup {nome} criado, com a memória {memoria}.";

    public const string MsgSeguiuSemSetup = "Seguindo sem Setup. As memórias do celular ficam travadas até escolher um Setup.";

    public static string MsgContinuaMutado(string motivo) => $"O som continua mutado: {motivo}";

    public static string MsgNaoMutou(string motivo) => $"O som não foi mutado: {motivo}";

    public static string MsgIncompleta(int n) => n == 1
        ? "A leitura está incompleta (1 ajuste sem valor)."
        : $"A leitura está incompleta ({n} ajustes sem valor).";

    public static string MsgNaoEscritos(int n, string? motivo) => n == 1
        ? $"1 ajuste não foi escrito: {motivo}"
        : $"{n} ajustes não foram escritos: {motivo}";

    public static string MsgRenomeado(string nome) => $"Agora se chama {nome}.";

    public static string MsgSetupApagado(string nome) => $"Setup {nome} apagado.";

    public const string MsgEquipamentoNaoExiste = "O equipamento não existe mais.";
    public const string MsgModeloNaoGuardado = "Não consegui guardar o modelo na base.";

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
    private readonly IRepositorioTexto? _repositorioFormato3;
    private readonly LogBle _log;
    private readonly TimeProvider _relogio;

    private readonly object _trava = new();
    private readonly SemaphoreSlim _operacao = new(1, 1);
    private readonly SemaphoreSlim _gravacao = new(1, 1);

    private DadosSetups _dados = DadosSetups.Vazio;
    private MarcaMudo? _marca;
    private EstadoConferencia _situacao = EstadoConferencia.Nenhuma;
    private string? _setupEmUso;
    private SomEmUso _emUso = SomEmUso.Nenhum;
    private string? _referencia;
    private string? _ultimoPreset;
    private IReadOnlyList<DiferencaConferencia> _diferencas = [];
    private string? _motivoMudo;
    private string? _aviso;
    private Antes? _antes;
    private bool _aberta;
    private bool _conferiu;
    private bool _conferindo;
    private Reaplicacao? _reaplicando;

    public GerenteSetups(
        Sessao sessao,
        EstadoAparelho estado,
        Escritor escritor,
        LeituraAutomatica leitura,
        CopiaAntes copia,
        LinkCanais link,
        GerentePresets presets,
        GerentePlaylists playlists,
        IChaves chaves,
        IRepositorioTexto repositorio,
        IRepositorioTexto repositorioMudo,
        LogBle log,
        TimeProvider? relogio = null,
        BaseModelos? modelos = null,
        IRepositorioTexto? repositorioFormato3 = null)
    {
        _sessao = sessao;
        _repositorioFormato3 = repositorioFormato3;
        Modelos = modelos;
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
        // O preset lembrado: o apagar pergunta quem lembra; o aplicar faz a memória em uso lembrar.
        presets.Lembrados = id => Dados.Memorias.Count(m => m.IdPreset == id);
        presets.AoAplicar = AoAplicarPresetAsync;
        presets.Mudou += Notificar; // um preset renomeado muda o "Preset: Cinema" das memórias
        _link.Mudou += _ => Notificar(); // as saídas do app dependem do link
        // História 13a: a cerca do limitador, no Setup em uso e nas saídas com o link que vale agora.
        // História 13c: o teto da cerca segue o nível da memória em uso (no Máximo, min(X, C)), também na volta à cópia.
        _escritor.Cerca = escritas => Cerca.Conferir(SetupEmUso?.Equipamentos, SaidasEmUso, _estado.Copiar(), escritas, NivelEmUso);
        _escritor.CercaNaVolta = escritas => Cerca.NaVolta(SetupEmUso?.Equipamentos, SaidasEmUso, _estado.Copiar(), escritas, NivelEmUso);
        // História 13b: a porta não mexe no nível de uma saída sem calibração (nem em nada global sem Setup em uso).
        _escritor.SemCalibracao = enderecos => SemCalibracao.Conferir(SetupEmUso?.Equipamentos, SaidasEmUso, enderecos);
        // História 13c: o master à mão acima da conta do nível em uso é recusado antes de enviar; o aplicar de um preset
        // leva o master da combinação nova no mesmo lote.
        _escritor.MasterNivel = RecusaDoMaster;
        presets.MasterDoPreset = MasterDoPreset;

        // Ao reconectar com a marca de uma destas operações: pela metade (Terminar ou Voltar à cópia).
        _copia.Registrar(TipoAplicarMemoria,
            _ => Task.FromResult(Retomada.PelaMetade),
            async op => ParaCopia(await AplicarMemoriaComTravaAsync(op.Alvo, TipoAplicarMemoria, refazendo: true)));
        // Uma marca do "Manter a calibração" de antes da 8f: o Terminar reaplica a memória (se ainda existe).
        _copia.Registrar(TipoManterCalibracao,
            _ => Task.FromResult(Retomada.PelaMetade),
            async op => ParaCopia(await AplicarMemoriaComTravaAsync(op.Alvo, TipoAplicarMemoria, refazendo: true)));
        _copia.Registrar(TipoAplicarSetup,
            _ => Task.FromResult(Retomada.PelaMetade),
            async op => ParaCopia(await EscolherInternoAsync(op.Alvo, refazendo: true)));

        Carregada = Task.Run(CarregarAsync);
    }

    /// <summary>Completa quando <c>setups.json</c> e <c>setup-mudo.json</c> foram lidos.</summary>
    public Task Carregada { get; }

    /// <summary>A base de modelos, onde vai a ficha de cada equipamento confirmado com modelo (null nos testes que não a usam).</summary>
    public BaseModelos? Modelos { get; }

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

    /// <summary>Os Setups, na ordem em que foram criados.</summary>
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

    /// <summary>
    /// A conferência ao conectar está reaplicando sozinha a memória ativa e o preset dela (regra 5,
    /// diferença leve; decisão do Denis, 30/10/2026), ou null. A casca mostra a tela toda
    /// "REAPLICANDO" enquanto não é null; o aviso <see cref="Mudou"/> chega ao começar e ao terminar.
    /// </summary>
    public Reaplicacao? Reaplicando
    {
        get
        {
            lock (_trava)
                return _reaplicando;
        }
    }

    /// <summary>A memória do celular em uso, ou null.</summary>
    public MemoriaCelular? MemoriaEmUso => EmUso is { Tipo: TipoSomEmUso.MemoriaCelular } uso ? Memoria(uso.IdMemoria) : null;

    /// <summary>
    /// A conexão com o aparelho está aberta (conectado e segurando a conexão). Quando ela cai, o som em
    /// uso volta a "nenhum" até a conferência da conexão nova; o Player usa isto para não largar a
    /// playlist na queda (decisão do Denis, 24/10/2026).
    /// </summary>
    public bool ConexaoAberta
    {
        get
        {
            lock (_trava)
                return _aberta;
        }
    }

    /// <summary>
    /// O id do preset em uso: o lembrado pela memória do celular em uso, ou o último aplicado nesta
    /// conexão; nenhum depois de uma queda.
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

    /// <summary>Na dúvida ou na troca: o Setup que era a referência (o "Casa" da frase), ou null.</summary>
    public Setup? Referencia
    {
        get
        {
            lock (_trava)
                return _referencia is { } id ? _dados.Setups.FirstOrDefault(s => s.Id == id) : null;
        }
    }

    /// <summary>
    /// A memória ativa do Setup que era a referência da dúvida (ou da troca): a das diferenças de "O que
    /// não bate" ("80 cm na FILME"), ou null (sem referência, ou o Setup não tem memória).
    /// </summary>
    public MemoriaCelular? MemoriaDaReferencia => Ativa(Referencia?.Id);

    /// <summary>
    /// As diferenças da dúvida (ou da troca) entre a memória ativa da referência e o aparelho, só nos
    /// códigos que mutam (rota, atraso, fase, cortes), para "O que não bate". Vazio sem memória ativa.
    /// </summary>
    public IReadOnlyList<DiferencaConferencia> Diferencas
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

    // ------------------------------------------------------------------ saídas do app (história 8e)

    /// <summary>
    /// As saídas do app no Setup em uso, com o link que vale agora no app (o tirado das ligações do
    /// Setup em uso): cada par com link é uma saída só. Sem Setup em uso, CH1 a CH4.
    /// </summary>
    /// <remarks>
    /// Sem Setup em uso o link guardado não conta: antes da conferência ele é o da última sessão (o Início
    /// desconectado mostrava "Sem Setup" com CH3 + CH4 juntos; 12b, 29/10/2026).
    /// </remarks>
    public IReadOnlyList<SaidaApp> SaidasEmUso =>
        SetupEmUso is { } setup
            ? Ligacoes.Saidas(setup.Equipamentos, _link.Ligado(ParCanais.Saidas12), _link.Ligado(ParCanais.Saidas34))
            : Ligacoes.Saidas([], false, false);

    /// <summary>
    /// O limite físico de cada saída do Setup em uso (história 13a), com o corte e o limitador do espelho
    /// agora; no nível Máximo da memória em uso (13c), o teto é min(X, C). Sem Setup em uso, nenhum.
    /// </summary>
    public IReadOnlyList<LimiteSaida> LimitesEmUso =>
        SetupEmUso is { } setup ? LimiteFisico.Calcular(setup.Equipamentos, SaidasEmUso, _estado.Valor, Niveis.SemTermico(NivelEmUso)) : [];

    /// <summary>Os alto-falantes passivos do Setup em uso que ficam fora da conta dos limites, com o motivo (história 13a).</summary>
    public IReadOnlyList<AltoFalanteFora> ForaDaContaEmUso => LimiteFisico.ForaDaConta(SetupEmUso?.Equipamentos);

    /// <summary>As saídas de um Setup guardado, com o link tirado das ligações dele (vazio se o Setup não existe).</summary>
    public IReadOnlyList<SaidaApp> Saidas(string? idSetup) =>
        Setup(idSetup) is { } setup ? Ligacoes.Saidas(setup.Equipamentos) : [];

    /// <summary>O link do Setup, tirado das ligações dele (<see cref="Ligacoes.Links"/>); desligado sem Setup.</summary>
    public static (bool Link12, bool Link34) LinksDe(Setup? setup) => setup is null ? (false, false) : Ligacoes.Links(setup.Equipamentos);

    /// <summary>A saída do app em uso que tem o canal (0 a 3), ou null.</summary>
    public SaidaApp? SaidaDe(int canal) => SaidasEmUso.FirstOrDefault(s => s.Canais.Contains(canal));

    /// <summary>O equipamento do Setup, ou null.</summary>
    public Equipamento? Equipamento(string? idSetup, string? idEquipamento) =>
        idEquipamento is null ? null : Setup(idSetup)?.Equipamentos.FirstOrDefault(e => e.Id == idEquipamento);

    /// <summary>
    /// A função acústica sugerida para o equipamento (que pode ainda não estar guardado) no Setup:
    /// pela categoria, senão pelo corte da memória ativa do Setup (não o do aparelho) no primeiro canal
    /// que o alimenta; sem memória, só pela categoria. Só para quem toca (grupo 1 e alto-falante
    /// passivo); os outros, null.
    /// </summary>
    public static FuncaoAcustica? FuncaoSugerida(Setup? setup, MemoriaCelular? ativa, Equipamento equipamento)
    {
        if (!equipamento.Toca)
            return null;
        var equipamentos = setup?.Equipamentos ?? [];
        var lista = equipamentos.Any(e => e.Id == equipamento.Id)
            ? equipamentos.Select(e => e.Id == equipamento.Id ? equipamento : e).ToArray()
            : [.. equipamentos, equipamento];
        var canais = Ligacoes.CanaisQueAlimentam(lista, equipamento.Id);
        return Ligacoes.SugerirFuncao(equipamento.Categoria, ativa is not null && canais.Count > 0 ? Corte(ativa, canais[0]) : null);
    }

    /// <summary>A função sugerida no Setup guardado, pelo corte da memória ativa dele.</summary>
    public FuncaoAcustica? FuncaoSugerida(string? idSetup, Equipamento equipamento) =>
        FuncaoSugerida(Setup(idSetup), Ativa(idSetup), equipamento);

    /// <summary>A função que vale: a escolhida pelo usuário, ou a sugerida. Só para quem toca; os outros, null.</summary>
    public FuncaoAcustica? Funcao(string? idSetup, Equipamento equipamento) =>
        !equipamento.Toca ? null : equipamento.Funcao ?? FuncaoSugerida(idSetup, equipamento);

    /// <summary>
    /// O corte guardado na memória para o canal (0 a 3): a frequência de cada filtro, ou null com o
    /// filtro em OFF (ou sem o valor guardado).
    /// </summary>
    public static CorteCanal Corte(MemoriaCelular memoria, int canal)
    {
        decimal? Numero(string codigo) =>
            memoria.Regulagem.FirstOrDefault(v => v.Codigo == codigo && v.Indice == canal) is { } v
                ? Catalogo.ParametroDe(codigo)?.Decodificar(v.NoFio).Valor?.Numero
                : null;
        decimal? Frequencia(string tipo, string frequencia) => Numero(tipo) is { } t && t != 0 ? Numero(frequencia) : null;
        return new CorteCanal(Frequencia("042", "040"), Frequencia("043", "041"));
    }

    // ------------------------------------------------------------------ níveis de rendimento (história 13c)

    /// <summary>O nível da memória do celular em uso, ou null (sem nível, ou sem memória do celular em uso).</summary>
    public NivelRendimento? NivelEmUso => MemoriaEmUso?.Nivel;

    /// <summary>
    /// Por que os níveis não são oferecidos agora, ou null: sem Setup em uso, uma saída sem calibração ("Calibre a
    /// saída JBL L.") ou o Bluetooth sem medir ("Meça o nível do Bluetooth.").
    /// </summary>
    public string? NivelIndisponivel => Niveis.Indisponivel(SetupEmUso, SaidasEmUso);

    /// <summary>
    /// A conta do nível na cadeia do aparelho agora (o espelho), no Setup em uso e com o link que vale, ou null
    /// (o nível não é oferecido). A conta pode vir sem master, com o dado que falta.
    /// </summary>
    public ContaNivel? Conta(NivelRendimento nivel) => Conta(nivel, _estado.Valor);

    /// <summary>A conta do nível da memória em uso agora, ou null (sem nível, ou o nível não é oferecido).</summary>
    public ContaNivel? ContaEmUso() => NivelEmUso is { } nivel ? Conta(nivel) : null;

    private ContaNivel? Conta(NivelRendimento nivel, Func<Endereco, Valor?> ler)
    {
        var setup = SetupEmUso;
        var saidas = SaidasEmUso;
        if (setup is null || Niveis.Indisponivel(setup, saidas) is not null)
            return null;
        return PiorCaso.Conta(setup.Equipamentos, saidas, ler, setup.NivelBluetooth!.Valor, nivel);
    }

    /// <summary>
    /// A conta do nível de uma memória sobre um alvo (a regulagem dela e as bandas do preset, por cima do espelho),
    /// no Setup dela, com o link tirado das ligações dele (como o aplicar): o master a pôr no aplicar, ou null.
    /// </summary>
    private ContaNivel? ContaDoAlvo(Setup setup, NivelRendimento nivel, IEnumerable<ValorCopiado> alvo)
    {
        var saidas = Ligacoes.Saidas(setup.Equipamentos);
        if (Niveis.Indisponivel(setup, saidas) is not null)
            return null;
        return PiorCaso.Conta(setup.Equipamentos, saidas, LeitorCom(alvo), setup.NivelBluetooth!.Valor, nivel);
    }

    /// <summary>O espelho agora com os valores por cima (o que um lote vai deixar no aparelho).</summary>
    private Func<Endereco, Valor?> LeitorCom(IEnumerable<ValorCopiado> valores)
    {
        var depois = new Dictionary<Endereco, Valor>(_estado.Copiar());
        foreach (var v in valores)
        {
            var endereco = new Endereco(v.Codigo, v.Indice);
            if (Catalogo.ParametroDe(v.Codigo)?.Decodificar(v.NoFio).Valor is { } valor)
                depois[endereco] = valor;
            else
                depois.Remove(endereco);
        }
        return e => depois.GetValueOrDefault(e);
    }

    // O corte do usuário no master (história 13c): com a memória em uso num nível, o MASTER pedido à mão (a tela ou a
    // porta) abaixo da conta fica como corte (conta − pedido, só na memória do app), e a conta na hora escreve
    // conta − corte: a cadeia ainda mexe no master para os dois lados, mas o corte do usuário fica. Aplicar a
    // memória, escolher o nível ou trocar o preset zeram o corte.
    private string? _corteMemoria;
    private decimal _corte;

    /// <summary>O corte do usuário no master da memória em uso (≥ 0 dB); 0 sem corte ou com outra memória em uso.</summary>
    public decimal CorteDoMaster
    {
        get
        {
            var id = MemoriaEmUso?.Id;
            lock (_trava)
                return id is not null && _corteMemoria == id ? _corte : 0m;
        }
    }

    /// <summary>O master que a conta na hora põe: a conta menos o corte do usuário, nunca abaixo de −80 dB.</summary>
    public decimal MasterComCorte(decimal conta) => Math.Max(conta - CorteDoMaster, PiorCaso.MinimoMaster);

    private void ZerarCorte()
    {
        lock (_trava)
        {
            _corteMemoria = null;
            _corte = 0m;
        }
    }

    /// <summary>
    /// A recusa do master à mão (a tela e a porta) com a memória em uso num nível: acima da conta, "Nível Alto: o
    /// master vai até −8,8 dB."; abaixo, passa e vira o corte do usuário. Com o nível sem conta (a calibração ou o
    /// Bluetooth perdidos, um dado que falta), só passa até o master de agora; acima, o motivo. Sem nível, passa.
    /// </summary>
    private string? RecusaDoMaster(decimal pedido)
    {
        if (MemoriaEmUso is not { Nivel: { } nivel } memoria)
            return null;
        var conta = Conta(nivel);
        if (conta?.Master is not { } master)
        {
            if (_estado.Valor(Camadas.CodigoMaster)?.Numero is { } agora && pedido <= agora)
                return null;
            return NivelIndisponivel ?? conta?.Falta ?? Niveis.MsgFalhou;
        }
        if (pedido > master)
            return Niveis.MsgMasterAcima(nivel, master);
        lock (_trava)
        {
            _corteMemoria = memoria.Id;
            _corte = master - pedido;
        }
        return null;
    }

    /// <summary>O master da combinação da memória em uso (com nível) e do preset, para o aplicar do preset, ou null.</summary>
    private decimal? MasterDoPreset(Preset preset) =>
        NivelEmUso is { } nivel ? Conta(nivel, LeitorCom(preset.Valores))?.Master : null;

    /// <summary>
    /// Sair do Máximo (história 13c): um limitador acima de min(T, X, C) ficaria acima da cerca. Devolve "Baixe o
    /// limitador da JBL L para −6,8 dB antes de sair do Máximo.", ou null.
    /// </summary>
    private string? LimitadorAcimaForaDoMaximo()
    {
        if (SetupEmUso is not { } setup)
            return null;
        foreach (var limite in LimiteFisico.Calcular(setup.Equipamentos, SaidasEmUso, _estado.Valor))
        {
            foreach (var c in limite.PorCanal)
            {
                if (_estado.Valor(Cerca.CodigoLimitador, c.Canal)?.Numero is { } limiar && limiar != Cerca.LimitadorOff && limiar > c.TetoCerca)
                    return Niveis.MsgSairDoMaximo(Cerca.Onde(limite, c.Canal), c.TetoCerca);
            }
        }
        return null;
    }

    /// <summary>
    /// Escolher o nível da memória (história 13c): grava o nível e, com ele, escreve o master da combinação memória
    /// + preset em uso (a conta na hora, origem App) e o guarda. "Sem nível" (null) grava, sem mexer no master, e
    /// limpa os masters guardados. O nível só se escolhe com a memória em uso e com os níveis oferecidos
    /// (<see cref="NivelIndisponivel"/>). Sair do Máximo com um limitador acima de min(T, X, C) é recusado.
    /// </summary>
    public async Task<ResultadoSetup> DefinirNivelAsync(string idMemoria, NivelRendimento? nivel)
    {
        await Carregada;
        if (!await _operacao.WaitAsync(0))
            return Terminar(ResultadoSetup.Falha(MsgOcupado));
        try
        {
            if (Memoria(idMemoria) is not { } memoria)
                return Terminar(ResultadoSetup.Falha(MsgMemoriaNaoExiste));
            if (nivel is { } pedido && !Enum.IsDefined(pedido))
                return Terminar(ResultadoSetup.Falha(Niveis.MsgFalhou));
            if (nivel is null && memoria.Nivel is null)
                return Terminar(ResultadoSetup.Feito(null));
            if (memoria.Nivel == NivelRendimento.Maximo && nivel != NivelRendimento.Maximo && MemoriaEmUso?.Id == memoria.Id)
            {
                string? acima;
                try
                {
                    acima = LimitadorAcimaForaDoMaximo();
                }
                catch (Exception e)
                {
                    _log.Evento($"memória {memoria.Nome}: falha ao conferir o limitador ({e.GetType().Name})");
                    acima = Niveis.MsgFalhou;
                }
                if (acima is not null)
                    return Terminar(ResultadoSetup.Falha(acima));
            }
            if (nivel is not { } novo)
            {
                if (!await GravarAsync(d => ComMemoria(d, memoria.Id, m => m with { Nivel = null, Masters = MemoriaCelular.SemMasters })))
                    return Terminar(ResultadoSetup.Falha(MsgNaoGuardou));
                ZerarCorte();
                _log.Evento($"memória {memoria.Nome} sem nível");
                return Terminar(ResultadoSetup.Feito(Niveis.MsgSemNivel(memoria.Nome)));
            }
            if (MemoriaEmUso?.Id != memoria.Id)
                return Terminar(ResultadoSetup.Falha(Niveis.MsgSoEmUso));
            if (NivelIndisponivel is { } motivo)
                return Terminar(ResultadoSetup.Falha(motivo));
            if (!SessaoAberta)
                return Terminar(ResultadoSetup.Falha(Sessao.MotivoSemConexao));
            if (!_estado.Pronto || _estado.Lendo)
                return Terminar(ResultadoSetup.Falha(_estado.Lendo ? Escritor.MsgLendo : Escritor.MsgSemLeitura));
            if (_copia.MotivoBloqueio() is { } bloqueio)
                return Terminar(ResultadoSetup.Falha(bloqueio));
            ContaNivel? conta;
            try
            {
                conta = Conta(novo);
            }
            catch (Exception e)
            {
                _log.Evento($"memória {memoria.Nome}: falha na conta do nível ({e.GetType().Name})");
                return Terminar(ResultadoSetup.Falha(Niveis.MsgFalhou));
            }
            if (conta?.Master is not { } master)
                return Terminar(ResultadoSetup.Falha(conta?.Falta ?? Niveis.MsgFalhou));

            // Primeiro guarda (com a memória como ela está no arquivo agora), depois escreve o master.
            var idPreset = IdPresetEmUso;
            if (!await GravarAsync(d => ComMemoria(d, memoria.Id, m => ComMaster(m with { Nivel = novo }, idPreset, master))))
                return Terminar(ResultadoSetup.Falha(MsgNaoGuardou));
            ZerarCorte();
            _log.Evento($"memória {memoria.Nome} no nível {Niveis.Nome(novo)}, master {master.ToString("0.0", Fio)} dB");
            var frase = Niveis.MsgDefinido(novo, memoria.Nome);
            if (conta.Aviso is { } aviso)
                frase += $" {aviso}";
            if (_estado.Valor(Camadas.CodigoMaster)?.Numero != master)
            {
                ResultadoEscrita r;
                using (Origem.Como(OrigemComando.App))
                    r = await _escritor.EscreverAsync(Catalogo.ParametroDe(Camadas.CodigoMaster)!, null, master);
                if (!r.Ok)
                {
                    var falha = r.Motivo ?? Escritor.MsgSemConfirmacao;
                    _log.Evento($"memória {memoria.Nome}: o master do nível não foi escrito ({falha})");
                    return Terminar(new ResultadoSetup(false, falha, $"{frase} {Niveis.MsgMasterNaoEscrito(falha)}"));
                }
            }
            return Terminar(ResultadoSetup.Feito(frase));
        }
        finally
        {
            _operacao.Release();
        }
    }

    /// <summary>
    /// Guarda o master que o nível pôs na memória (história 13c): no registro por preset (<see cref="MemoriaCelular.Masters"/>,
    /// "" sem preset) e no MASTER da regulagem, para a conferência ao conectar e o Salvar verem o que está no
    /// aparelho. Vale na hora, mesmo se não gravar.
    /// </summary>
    internal Task GuardarMasterAsync(string idMemoria, string? idPreset, decimal master) =>
        GravarUsoAsync(d => d with { Memorias = d.Memorias.Select(m => m.Id == idMemoria ? ComMaster(m, idPreset, master) : m).ToArray() });

    /// <summary>A memória com o master do nível guardado para o preset e na regulagem.</summary>
    private static MemoriaCelular ComMaster(MemoriaCelular memoria, string? idPreset, decimal master)
    {
        var masters = new Dictionary<string, decimal>(memoria.Masters) { [idPreset ?? ""] = master };
        var noFio = Camadas.Db(master);
        IReadOnlyList<ValorCopiado> regulagem = memoria.Regulagem.Any(v => v.Codigo == Camadas.CodigoMaster)
            ? memoria.Regulagem.Select(v => v.Codigo == Camadas.CodigoMaster ? v with { NoFio = noFio } : v).ToArray()
            : [.. memoria.Regulagem, new ValorCopiado(Camadas.CodigoMaster, null, noFio)];
        return memoria with { Masters = masters, Regulagem = regulagem };
    }

    /// <summary>
    /// Marca ou desmarca a memória como otimizada (história 13d): a playlist dela passa a ser analisada (ou deixa de
    /// ser). Só grava no celular; nada vai ao aparelho. Mesmo valor: nada muda.
    /// </summary>
    public async Task<ResultadoSetup> DefinirOtimizadaAsync(string idMemoria, bool otimizada)
    {
        await Carregada;
        if (Memoria(idMemoria) is not { } memoria)
            return ResultadoSetup.Falha(MsgMemoriaNaoExiste);
        if (memoria.Otimizada == otimizada)
            return ResultadoSetup.Feito(null);
        if (!await GravarAsync(d => ComMemoria(d, idMemoria, m => m with { Otimizada = otimizada })))
            return ResultadoSetup.Falha(MsgNaoGuardou);
        _log.Evento($"memória {memoria.Nome} {(otimizada ? "otimizada na playlist" : "sem otimizar na playlist")}");
        return ResultadoSetup.Feito(null);
    }

    /// <summary>Os dados com a memória mudada (a que está nos dados agora).</summary>
    private static DadosSetups ComMemoria(DadosSetups dados, string idMemoria, Func<MemoriaCelular, MemoriaCelular> mudar) =>
        dados with { Memorias = dados.Memorias.Select(m => m.Id == idMemoria ? mudar(m) : m).ToArray() };

    // ------------------------------------------------------------------ nomes

    /// <summary>A regra dos presets: de 1 a 24 letras, números, espaço, <c>_</c> ou <c>-</c> (null = não vale).</summary>
    public static string? NomeValido(string? digitado) => GerentePresets.NomeValido(digitado);

    /// <summary>
    /// O nome de um Setup: a regra dos presets e também o ponto ("Casa 2.1"); null = não vale. As
    /// memórias e os presets seguem a regra deles (<see cref="NomeValido"/>).
    /// </summary>
    public static string? NomeSetupValido(string? digitado)
    {
        var nome = digitado?.Normalize(NormalizationForm.FormC).Trim();
        return nome is not null && GerentePresets.NomeValido(nome.Replace('.', '_')) is not null ? nome : null;
    }

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
                    var diferencas = alvo is null ? [] : DiferencasDaConferencia(alvo, AtivaEm(dados, alvo.Id), espelho);
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
                    await DesligarLinksNaConferenciaAsync();
                    return true;
                }
                // O mudo saiu por fora (o app da Taramps): a marca não vale mais.
                _log.Evento("setup: a marca do mudo ficou, mas o aparelho não está mutado; confere normal");
                await GravarMarcaAsync(null);
            }

            if (referencia is null)
            {
                // Nenhum Setup cadastrado (ReferenciaEm só dá null sem Setups): não há memória para
                // comparar (regra 5, CAP-16). Muta como o app e pede a escolha (criar ou seguir sem Setup).
                string? motivoSem;
                using (Origem.Como(OrigemComando.App))
                    motivoSem = await MutarAsync(null, espelho);
                lock (_trava)
                {
                    _situacao = EstadoConferencia.Duvida;
                    _referencia = null;
                    _diferencas = [];
                    _setupEmUso = null;
                    _emUso = SomEmUso.SomDoAparelho;
                    _motivoMudo = motivoSem is null ? null : MsgNaoMutou(motivoSem);
                    _antes = null;
                }
                _log.Evento("setup: em dúvida (nenhum Setup cadastrado)" + (motivoSem is null ? "; mutado" : $"; não mutou ({motivoSem})"));
                await DesligarLinksNaConferenciaAsync();
                return true;
            }

            // A regulagem lida contra a da memória ativa do Setup (as bandas não entram).
            var ativa = AtivaEm(dados, referencia.Id);
            var diferentes = ativa is null ? [] : Camadas.Diferentes(ativa.Regulagem, espelho);
            if (ativa is not null && !diferentes.Any(v => Camadas.MutaNaConferencia(v.Codigo)))
            {
                var bate = diferentes.Count == 0;
                // Diferença leve: reaplica sozinho a memória ativa e o preset dela, sem mutar. Com a
                // chave desligada ou um "aplicado pela metade" pendente, fica como antes (aviso e Reaplicar).
                if (!bate && await ReaplicarNaConferenciaAsync(referencia, ativa, diferentes.Count))
                    return true;
                if (!SessaoAberta)
                    return true; // caiu no meio da reaplicação: a conexão nova confere de novo
                var situacao = bate ? EstadoConferencia.Confere : EstadoConferencia.SomDiferente;
                lock (_trava)
                {
                    _situacao = situacao;
                    _setupEmUso = referencia.Id;
                    _emUso = bate ? SomEmUso.DoCelular(ativa.Id) : SomEmUso.SomDoAparelho;
                    _diferencas = [];
                    _referencia = null;
                    _motivoMudo = null;
                }
                _log.Evento(bate
                    ? $"setup: {referencia.Nome} confere com a {ativa.Nome}"
                    : $"setup: {referencia.Nome} confere; {diferentes.Count} ajustes de ganho, limitador, EQ ou MASTER diferem da {ativa.Nome}");
                // O Setup em uso manda no link (tirado das ligações dele). Não manda nada ao aparelho.
                var (link12, link34) = LinksDe(referencia);
                if (await _link.DefinirEstadoAsync(link12, link34) is { } motivoLink)
                    _log.Evento($"setup: {MsgLinkNaoGuardado(motivoLink)}");
                return true;
            }

            // A rota, o atraso, a fase ou um corte não batem (ou não há memória para comparar): muta
            // (como o app, não como a tela nem a IA) e pede a escolha.
            var lista = DiferencasDaConferencia(referencia, ativa, espelho);
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
            _log.Evento((ativa is null
                            ? $"setup: em dúvida (o {referencia.Nome} não tem memória para comparar)"
                            : $"setup: em dúvida ({lista.Count} diferenças de rota, atraso, fase ou corte com a {ativa.Nome})")
                        + (motivo is null ? "; mutado" : $"; não mutou ({motivo})"));
            await DesligarLinksNaConferenciaAsync();
            return true;
        }
        finally
        {
            _operacao.Release();
        }
    }

    /// <summary>
    /// A regra 5 na diferença leve (só ganho, limitador, EQ ou MASTER): aplica a memória ativa do
    /// Setup com o preset que ela lembra, na mesma operação da conferência (já com a trava) e com
    /// origem App, sem mutar. Pula, sem escrever nada, com a chave "Aplicar memória do celular"
    /// desligada ou com uma operação pela metade (ou a marca dela) pendente. Enquanto escreve,
    /// <see cref="Reaplicando"/> diz a memória e o preset. True = reaplicou e ficou
    /// <see cref="EstadoConferencia.Confere"/>; false = pulou ou falhou (uma falha no meio segue a
    /// regra 3, pela metade). Chamar com a operação.
    /// </summary>
    private async Task<bool> ReaplicarNaConferenciaAsync(Setup referencia, MemoriaCelular ativa, int diferentes)
    {
        await _copia.Carregada;
        if (!_chaves.Ligada(Chave.AplicarMemoriaCelular))
        {
            _log.Evento($"setup: não reaplica a {ativa.Nome} sozinho (chave Aplicar memória do celular desligada)");
            return false;
        }
        if (_copia.MotivoBloqueio() is { } bloqueio)
        {
            _log.Evento($"setup: não reaplica a {ativa.Nome} sozinho ({bloqueio})");
            return false;
        }

        // O Setup em uso já é o da referência: o aplicar confere a memória contra ele.
        lock (_trava)
        {
            _situacao = EstadoConferencia.SomDiferente;
            _setupEmUso = referencia.Id;
            _emUso = SomEmUso.SomDoAparelho;
            _diferencas = [];
            _referencia = null;
            _motivoMudo = null;
            _reaplicando = new Reaplicacao(ativa.Nome, PresetDe(ativa)?.Nome);
        }
        _log.Evento($"setup: {referencia.Nome}; {diferentes} ajustes de ganho, limitador, EQ ou MASTER diferem da {ativa.Nome}; reaplicando sozinho");
        Notificar();

        ResultadoSetup r;
        try
        {
            // Como o app: sem o escopo, a origem seria a tela e contaria como toque.
            using (Origem.Como(OrigemComando.App))
                r = await AplicarMemoriaAsync(ativa.Id, TipoAplicarMemoria, refazendo: false);
        }
        finally
        {
            lock (_trava)
                _reaplicando = null;
            Notificar();
        }
        Terminar(r);
        return r.Ok;
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

    /// <summary>
    /// Sem Setup em uso, os links 1–2 e 3–4 ficam desligados (decisão do Denis, 24/10/2026). Não manda
    /// nada ao aparelho; só a falha de guardar vai para o registro.
    /// </summary>
    private async Task<string?> DesligarLinksAsync()
    {
        if (await _link.DefinirEstadoAsync(false, false) is not { } motivo)
            return null;
        var frase = MsgLinksNaoDesligados(motivo);
        _log.Evento($"setup: {frase}");
        return frase;
    }

    /// <summary>Na conferência (sem resultado para devolver), a falha dos links vira o aviso do Setup.</summary>
    private async Task DesligarLinksNaConferenciaAsync()
    {
        if (await DesligarLinksAsync() is { } frase)
        {
            lock (_trava)
                _aviso = frase;
        }
    }

    /// <summary>A frase com a falha dos links no fim, se houve.</summary>
    private static string? Juntar(string? frase, string? links) =>
        links is null ? frase : frase is null ? links : $"{frase} {links}";

    private static bool TodosMutados(IReadOnlyDictionary<Endereco, Valor> espelho) =>
        Enumerable.Range(0, Parametro.Canais).All(c =>
            espelho.TryGetValue(new Endereco(Camadas.CodigoMute, c), out var v) && v.Ligado == true);

    /// <summary>
    /// A regulagem lida contra a da memória ativa, só nos códigos que mutam (rota, atraso, fase,
    /// cortes): uma linha por valor diferente (ou sem leitura), pelo nome da saída do Setup ("Atraso ·
    /// Sub"); o par com link que difere igual nos dois canais é uma linha só. Sem memória, nenhuma.
    /// </summary>
    private static IReadOnlyList<DiferencaConferencia> DiferencasDaConferencia(Setup setup, MemoriaCelular? ativa,
        IReadOnlyDictionary<Endereco, Valor> espelho) =>
        ativa is null
            ? []
            : RotulosSaida.Juntar(
                    Camadas.Diferentes(ativa.Regulagem, espelho)
                        .Where(v => Camadas.MutaNaConferencia(v.Codigo))
                        .Select(v => DiferencaPorCanal.De(v.Codigo, v.Indice, Camadas.Exibir(v.Codigo, v.NoFio),
                            Camadas.Exibir(v.Codigo, espelho.GetValueOrDefault(new Endereco(v.Codigo, v.Indice))?.NoFio))),
                    Ligacoes.Saidas(setup.Equipamentos))
                .Select(l => new DiferencaConferencia(l.Codigo, l.Indice, l.Rotulo, l.Antes, l.Depois, l.Canal))
                .ToArray();

    // ------------------------------------------------------------------ escolher, criar, seguir, trocar

    /// <summary>
    /// Escolher o Setup (na dúvida ou na troca): cópia antes, escreve a memória ativa dele (a regulagem
    /// e as bandas do preset lembrado), relê, põe o link tirado das ligações, desmuta para o mute
    /// anterior e apaga a marca. Passa pela chave "Aplicar memória do celular". Num Setup sem memórias,
    /// desmuta sem escrever nada.
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
            var ativa = Ativa(setup.Id);
            if (!refazendo)
            {
                if (!EmEscolha)
                    return Terminar(ResultadoSetup.Falha(MsgSoNaEscolha));
                // Sem memória, nada é aplicado: a chave do aplicar não entra.
                if (ativa is not null && !_chaves.Ligada(Chave.AplicarMemoriaCelular))
                    return Terminar(ResultadoSetup.Falha(Sessao.MotivoChaveDesligada));
            }

            var aplicado = new Aplicado(true, null, Iniciou: false, Restam: 0);
            var sumiu = false;
            ContaNivel? conta = null;
            if (ativa is not null)
            {
                ZerarCorte();
                (var alvo, sumiu, conta) = Alvo(ativa);
                var op = new OperacaoPendente(TipoAplicarSetup, setup.Id, $"Escolher o Setup {setup.Nome}", setup.Nome);
                aplicado = await AplicarAlvoAsync(setup, alvo, op, refazendo, ativa.Nivel);
                if (!aplicado.Ok)
                    return Terminar(ResultadoSetup.Falha(aplicado.Motivo!));
            }
            else if (refazendo)
            {
                // O Terminar de uma escolha de um Setup que perdeu as memórias: só a marca sai.
                aplicado = aplicado with { Iniciou = true };
            }

            var (falhou, link) = await FecharEscolhaAsync(setup, ativa, aplicado.Iniciou);
            if (falhou is not null)
                return Terminar(ResultadoSetup.Falha(MsgContinuaMutado(falhou)));
            if (ativa is not null && conta?.Master is { } master)
                await GuardarMasterAsync(ativa.Id, sumiu ? null : ativa.IdPreset, master);
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
    /// Depois de a memória ativa estar no aparelho (ou sem memória): o link tirado das ligações do
    /// Setup, o desmutar, a marca da cópia concluída, o Setup em uso. Devolve o motivo se não desmutou
    /// (fica pela metade) e o motivo se o link do Setup não foi guardado.
    /// </summary>
    private async Task<(string? Desmutar, string? Link)> FecharEscolhaAsync(Setup setup, MemoriaCelular? ativa, bool iniciou)
    {
        var (link12, link34) = LinksDe(setup);
        var link = await _link.DefinirEstadoAsync(link12, link34);
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
        }
        await GravarUsoAsync(d => d with { UltimoSetup = setup.Id });
        return (null, link);
    }

    /// <summary>
    /// Criar Setup novo (na dúvida ou na troca): o Setup nasce sem equipamentos, e a regulagem lida vira
    /// a primeira memória dele, com o nome dado (nulo ou vazio = o nome do Setup); ela lembra o preset
    /// que tem as mesmas 15 bandas lidas, se há. O Setup e a memória ficam em uso, os links desligados
    /// (sem equipamentos não há bridge), e desmuta. Não manda nada ao aparelho além do desmutar. Um nome
    /// inválido ou repetido recusa sem guardar nada; o da memória com a recusa própria (<see cref="MsgNomeMemoria"/>,
    /// <see cref="ResultadoSetup.ErroNomeMemoria"/>). O ambiente é opcional (null = não informado).
    /// </summary>
    public async Task<ResultadoSetup> CriarSetupAsync(string? nomeSetup, string? nomeMemoria = null, AmbienteSetup? ambiente = null)
    {
        await Carregada;
        if (!await _operacao.WaitAsync(0))
            return Terminar(ResultadoSetup.Falha(MsgOcupado));
        try
        {
            if (!EmEscolha)
                return Terminar(ResultadoSetup.Falha(MsgSoNaEscolha));
            if (NomeSetupValido(nomeSetup) is not { } nome)
                return Terminar(ResultadoSetup.FalhaNomeSetup(MsgNomeSetupInvalido));
            if (ambiente is { } pedido && !Enum.IsDefined(pedido))
                return Terminar(ResultadoSetup.Falha(MsgAmbienteInvalido));
            if (Dados.Setups.Any(s => MesmoNome(s.Nome, nome)))
                return Terminar(ResultadoSetup.FalhaNomeSetup(MsgNomeRepetidoSetup));
            if (!_estado.Pronto || _estado.Lendo)
                return Terminar(ResultadoSetup.Falha(_estado.Lendo ? Escritor.MsgLendo : Escritor.MsgSemLeitura));
            var espelho = _estado.Copiar();
            var setup = new Setup($"setup-{Guid.NewGuid():N}", nome, _relogio.GetLocalNow(), ambiente);
            var igual = _presets.ComAsBandas(espelho);
            var (memoria, motivo, doNome) = NovaMemoria(setup, string.IsNullOrWhiteSpace(nomeMemoria) ? nome : nomeMemoria, espelho, igual?.Id);
            if (memoria is null)
            {
                // O nome da memória tem a recusa própria, embaixo do campo dela; a leitura incompleta, não.
                return Terminar(doNome ? ResultadoSetup.FalhaNomeMemoria(MsgNomeMemoria(motivo!)) : ResultadoSetup.Falha(motivo!));
            }

            var dados = Dados;
            var novos = dados with
            {
                Setups = [.. dados.Setups, setup],
                Memorias = [.. dados.Memorias, memoria],
                UltimoSetup = setup.Id,
                Uso = Com(dados.Uso, setup.Id, Usar(UsoDoSetup.Vazio, memoria.Id)),
            };
            if (!await GravarAsync(novos))
                return Terminar(ResultadoSetup.Falha(MsgNaoGuardou));

            var links = await DesligarLinksAsync();
            var desmutar = await DesmutarAsync();
            lock (_trava)
            {
                _situacao = EstadoConferencia.Confere;
                _setupEmUso = setup.Id;
                _emUso = SomEmUso.DoCelular(memoria.Id);
                _ultimoPreset = igual?.Id;
                _diferencas = [];
                _referencia = null;
                _motivoMudo = null;
                _antes = null;
            }
            _log.Evento($"setup: {nome} criado, com a memória {memoria.Nome} do som do aparelho"
                        + (ambiente is { } a ? $" ({Ambientes.Nome(a)})" : ""));
            await AdotarPlaylistsAsync();
            var frase = MsgSetupCriado(nome, memoria.Nome);
            if (desmutar is not null)
                frase += $" {MsgContinuaMutado(desmutar)}";
            return Terminar(ResultadoSetup.Feito(Juntar(frase, links)));
        }
        finally
        {
            _operacao.Release();
        }
    }

    /// <summary>
    /// Seguir sem Setup (na dúvida ou na troca): desmuta sem aplicar nada; as memórias do celular ficam
    /// travadas e os links ficam desligados.
    /// </summary>
    public async Task<ResultadoSetup> SeguirSemSetupAsync()
    {
        await Carregada;
        if (!await _operacao.WaitAsync(0))
            return Terminar(ResultadoSetup.Falha(MsgOcupado));
        try
        {
            if (!EmEscolha)
                return Terminar(ResultadoSetup.Falha(MsgSoNaEscolha));
            var links = await DesligarLinksAsync();
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
            }
            _log.Evento("setup: seguindo sem Setup");
            return Terminar(ResultadoSetup.Feito(Juntar(
                desmutar is null ? MsgSeguiuSemSetup : $"{MsgSeguiuSemSetup} {MsgContinuaMutado(desmutar)}", links)));
        }
        finally
        {
            _operacao.Release();
        }
    }

    /// <summary>
    /// Tocar no Setup com o app conectado: guarda a marca, muta, desliga os links (sem Setup em uso
    /// durante a escolha) e abre "Trocar de Setup".
    /// </summary>
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
            var lista = referencia is null ? [] : DiferencasDaConferencia(referencia, Ativa(referencia.Id), espelho);
            lock (_trava)
            {
                _situacao = EstadoConferencia.Trocando;
                _referencia = referencia?.Id;
                _diferencas = lista;
                _motivoMudo = motivo is null ? null : MsgNaoMutou(motivo);
                _antes = antes;
            }
            _log.Evento("setup: trocando de Setup" + (motivo is null ? "; mutado" : $"; não mutou ({motivo})"));
            return Terminar(ResultadoSetup.Feito(await DesligarLinksAsync()));
        }
        finally
        {
            _operacao.Release();
        }
    }

    /// <summary>
    /// Desistir da troca (‹ ou Voltar): desmuta e volta ao estado de antes, sem aplicar nada; os links
    /// do Setup em uso voltam (sem Setup em uso antes, ficam desligados).
    /// </summary>
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
            var (link12, link34) = LinksDe(Setup(antes.SetupEmUso));
            string? links = null;
            if (await _link.DefinirEstadoAsync(link12, link34) is { } motivoLink)
            {
                links = MsgLinkNaoGuardado(motivoLink);
                _log.Evento($"setup: {links}");
            }
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
            return Terminar(ResultadoSetup.Feito(Juntar(desmutar is null ? null : MsgContinuaMutado(desmutar), links)));
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
            if (NomeSetupValido(nomeDigitado) is not { } nome)
                return Terminar(ResultadoSetup.Falha(MsgNomeSetupInvalido));
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

    /// <summary>
    /// Define o ambiente do Setup (Sala, Carro, Ao ar livre, Outro; null = não informado). É um dado
    /// físico: não manda nada ao aparelho. Se não gravar, o ambiente fica como estava.
    /// </summary>
    public async Task<ResultadoSetup> DefinirAmbienteAsync(string id, AmbienteSetup? ambiente)
    {
        await Carregada;
        if (!await _operacao.WaitAsync(0))
            return Terminar(ResultadoSetup.Falha(MsgOcupado));
        try
        {
            if (ambiente is { } novo && !Enum.IsDefined(novo))
                return Terminar(ResultadoSetup.Falha(MsgAmbienteInvalido));
            if (Setup(id) is not { } setup)
                return Terminar(ResultadoSetup.Falha(MsgSetupNaoExiste));
            if (setup.Ambiente == ambiente)
                return Terminar(ResultadoSetup.Feito(null));
            var dados = Dados;
            if (!await GravarAsync(dados with { Setups = dados.Setups.Select(s => s.Id == id ? s with { Ambiente = ambiente } : s).ToArray() }))
                return Terminar(ResultadoSetup.Falha(MsgNaoGuardou));
            _log.Evento($"setup: {setup.Nome} no ambiente {(ambiente is { } a ? Ambientes.Nome(a) : "não informado")}");
            return Terminar(ResultadoSetup.Feito(MsgAmbienteDefinido(setup.Nome, ambiente)));
        }
        finally
        {
            _operacao.Release();
        }
    }

    /// <summary>
    /// Define o nível do Bluetooth do Setup (história 13b; null = não medido). É um dado físico: não manda
    /// nada ao aparelho. Se não gravar, fica como estava.
    /// </summary>
    public async Task<ResultadoSetup> DefinirNivelBluetoothAsync(string id, DadoTecnico? nivel)
    {
        await Carregada;
        if (!await _operacao.WaitAsync(0))
            return Terminar(ResultadoSetup.Falha(MsgOcupado));
        try
        {
            if (Setup(id) is not { } setup)
                return Terminar(ResultadoSetup.Falha(MsgSetupNaoExiste));
            if (nivel is { Fonte: { } fonte } && Ligacoes.TextoLongoValido(fonte) is null)
                return Terminar(ResultadoSetup.Falha(Ligacoes.MsgFonteDadoInvalida));
            if (setup.NivelBluetooth == nivel)
                return Terminar(ResultadoSetup.Feito(null));
            var dados = Dados;
            if (!await GravarAsync(dados with { Setups = dados.Setups.Select(s => s.Id == id ? s with { NivelBluetooth = nivel } : s).ToArray() }))
                return Terminar(ResultadoSetup.Falha(MsgNaoGuardou));
            _log.Evento($"setup: {setup.Nome} com o nível do Bluetooth {(nivel is { } n ? n.Valor.ToString("0.0", Fio) + " dB" : "não medido")}");
            return Terminar(ResultadoSetup.Feito(null));
        }
        finally
        {
            _operacao.Release();
        }
    }

    /// <summary>
    /// Grava o ganho medido do módulo (história 13b, a calibração guiada): o <c>Ganho</c> (por canal, vale para
    /// todos os canais dele) ou, na ponte, o <c>GanhoPonte</c>. É um dado físico da ficha: não manda nada ao
    /// aparelho e não vai para a base de modelos (o medido é da instalação).
    /// </summary>
    public Task<ResultadoEquipamento> GravarGanhoMedidoAsync(string idSetup, string idModulo, bool ponte, DadoTecnico ganho) =>
        MudarEquipamentosAsync(idSetup, setup =>
        {
            if (setup.Equipamentos.FirstOrDefault(e => e.Id == idModulo) is not { Grupo: GrupoEquipamento.Amplificador } modulo)
                return new Mudanca(null, MsgEquipamentoNaoExiste, null, null);
            var dados = ponte ? modulo.Dados with { GanhoPonte = ganho } : modulo.Dados with { Ganho = ganho };
            var novo = modulo with { Dados = dados };
            return new Mudanca(setup.Equipamentos.Select(e => e.Id == idModulo ? novo : e).ToArray(), null,
                MsgEquipamentoGuardado(modulo.Nome), idModulo);
        });

    /// <summary>Apaga um Setup, com as memórias dele. O Setup em uso não se apaga.</summary>
    public async Task<ResultadoSetup> ApagarSetupAsync(string id)
    {
        await Carregada;
        if (!await _operacao.WaitAsync(0))
            return Terminar(ResultadoSetup.Falha(MsgOcupado));
        try
        {
            if (Setup(id) is not { } setup)
                return Terminar(ResultadoSetup.Falha(MsgSetupNaoExiste));
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
            return new Mudanca([.. setup.Equipamentos, limpo], null, MsgEquipamentoAdicionado(limpo.Nome, setup.Nome), limpo.Id,
                Confirmado: limpo);
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
                MsgEquipamentoGuardado(limpo.Nome), limpo.Id, Editado: limpo.Id, Confirmado: limpo);
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
    /// <param name="Confirmado">O equipamento adicionado ou editado: com modelo, a ficha vai para a base de modelos.</param>
    private sealed record Mudanca(IReadOnlyList<Equipamento>? Lista, string? Motivo, string? Mensagem, string? Id, string? Editado = null,
        Equipamento? Confirmado = null);

    /// <summary>
    /// Uma mudança nos equipamentos de um Setup: confere a lista inteira e só guarda se ela vale. No
    /// Setup em uso, o link tirado das ligações novas passa a valer. Um equipamento confirmado com
    /// modelo vai para a base de modelos. Uma operação por vez.
    /// </summary>
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
            var frase = m.Mensagem;
            if (SetupEmUso?.Id == setup.Id)
            {
                // O link sai das ligações do Setup em uso (decisão D1 do Denis, 29/10/2026).
                var (link12, link34) = Ligacoes.Links(m.Lista);
                if (await _link.DefinirEstadoAsync(link12, link34) is { } motivoLink)
                {
                    frase = $"{frase} {MsgLinkNaoGuardado(motivoLink)}";
                    _log.Evento($"setup: {MsgLinkNaoGuardado(motivoLink)}");
                }
            }
            if (m.Confirmado is { Modelo: not null } confirmado && Modelos is { } modelos && !await modelos.GuardarAsync(confirmado))
                frase = $"{frase} {MsgModeloNaoGuardado}";
            return TerminarEquipamento(new ResultadoEquipamento(true, null, frase, m.Id));
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
    /// Criar memória do som atual, no Setup em uso: guarda a regulagem lida, com o valor final. Vira a
    /// memória em uso e a ativa e entra nos botões. Com o som do aparelho em uso, não cria: o caminho
    /// é a folha do Salvar.
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
            if (EmUso.Tipo is TipoSomEmUso.SomDoAparelho)
                return Terminar(new ResultadoSetup(false, MsgPedeFolha, MsgPedeFolha, PedeFolhaSalvar: true));
            if (!_estado.Pronto || _estado.Lendo)
                return Terminar(ResultadoSetup.Falha(_estado.Lendo ? Escritor.MsgLendo : Escritor.MsgSemLeitura));
            return Terminar(await CriarDoEspelhoAsync(setup, nomeDigitado));
        }
        finally
        {
            _operacao.Release();
        }
    }

    /// <summary>
    /// O Salvar da criação da memória (CAP-18, só o usuário): cria no Setup aberto a memória com a regulagem
    /// relida (o espelho), mesmo com o som do aparelho em uso, lembrando o preset em uso; ela vira a memória
    /// em uso e a ativa. Não manda nada ao aparelho. Um nome inválido ou repetido recusa sem guardar nada.
    /// </summary>
    internal async Task<ResultadoSetup> SalvarCriacaoAsync(string idSetup, string? nomeDigitado)
    {
        await Carregada;
        if (!await _operacao.WaitAsync(0))
            return Terminar(ResultadoSetup.Falha(MsgOcupado));
        try
        {
            if (EmEscolha)
                return Terminar(ResultadoSetup.Falha(MsgDuvida));
            if (Setup(idSetup) is not { } setup)
                return Terminar(ResultadoSetup.Falha(MsgSetupNaoExiste));
            if (!_estado.Pronto || _estado.Lendo)
                return Terminar(ResultadoSetup.Falha(_estado.Lendo ? Escritor.MsgLendo : Escritor.MsgSemLeitura));
            return Terminar(await CriarDoEspelhoAsync(setup, nomeDigitado));
        }
        finally
        {
            _operacao.Release();
        }
    }

    /// <summary>
    /// A memória nova do espelho no Setup, lembrando o preset em uso: entra nas memórias, vira a em uso e a
    /// ativa e vai aos botões. Chamar com a operação.
    /// </summary>
    private async Task<ResultadoSetup> CriarDoEspelhoAsync(Setup setup, string? nomeDigitado)
    {
        var (memoria, motivo, _) = NovaMemoria(setup, nomeDigitado, _estado.Copiar(), PresetEmUso?.Id);
        if (memoria is null)
            return ResultadoSetup.Falha(motivo!);

        var dados = Dados;
        var novos = dados with
        {
            Memorias = [.. dados.Memorias, memoria],
            Uso = Com(dados.Uso, setup.Id, Usar(dados.UsoDe(setup.Id), memoria.Id)),
        };
        if (!await GravarAsync(novos))
            return ResultadoSetup.Falha(MsgNaoGuardou);
        lock (_trava)
        {
            _emUso = SomEmUso.DoCelular(memoria.Id);
            if (_situacao == EstadoConferencia.SomDiferente)
                _situacao = EstadoConferencia.Confere;
        }
        _log.Evento($"memória {memoria.Nome} criada no {setup.Nome}");
        await AdotarPlaylistsAsync();
        return ResultadoSetup.Feito(MsgCriada(memoria.Nome, setup.Nome));
    }

    /// <summary>
    /// Aplica a memória do celular (do Setup em uso): cópia antes, escreve a regulagem dela e as 15
    /// bandas do preset que ela lembra (só o que difere; sem preset, as bandas ficam), relê e confere.
    /// O mute e os extras não entram. Passa pela chave "Aplicar memória do celular".
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

        var op = new OperacaoPendente(tipo, memoria.Id, $"Aplicar a memória {memoria.Nome}", memoria.Nome);
        ZerarCorte();
        var (alvo, sumiu, conta) = Alvo(memoria);
        var aplicado = await AplicarAlvoAsync(setup, alvo, op, refazendo, memoria.Nivel);
        if (!aplicado.Ok)
            return ResultadoSetup.Falha(aplicado.Motivo!);

        string? link;
        if (Marca is not null)
        {
            // Um Terminar com a dúvida aberta: a memória está no aparelho; a dúvida fecha.
            (var falhou, link) = await FecharEscolhaAsync(setup, memoria, aplicado.Iniciou);
            if (falhou is not null)
                return ResultadoSetup.Falha(MsgContinuaMutado(falhou));
        }
        else
        {
            if (aplicado.Iniciou)
                await _copia.ConcluirAsync();
            var (link12, link34) = LinksDe(setup);
            link = await _link.DefinirEstadoAsync(link12, link34);
        }

        lock (_trava)
        {
            _situacao = EstadoConferencia.Confere;
            _setupEmUso = setup.Id;
            _emUso = SomEmUso.DoCelular(memoria.Id);
            _ultimoPreset = sumiu ? null : memoria.IdPreset;
        }
        await GravarUsoAsync(d => d with { Uso = Com(d.Uso, setup.Id, Usar(d.UsoDe(setup.Id), memoria.Id)) });
        // História 13c: o master que o nível pôs fica registrado para a combinação com o preset.
        if (conta?.Master is { } master)
            await GuardarMasterAsync(memoria.Id, sumiu ? null : memoria.IdPreset, master);
        _log.Evento($"memória {memoria.Nome} aplicada");
        var frase = aplicado.Restam == 0 ? MsgAplicada(memoria.Nome) : MsgAplicadaDiferente(memoria.Nome, aplicado.Restam);
        if (sumiu)
            frase += $" {MsgPresetSumiu}";
        if (conta?.Aviso is { } aviso)
            frase += $" {aviso}";
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
        var linhas = new List<LinhaSalvar>();
        var emUso = EmUso;
        if (emUso.Tipo == TipoSomEmUso.MemoriaCelular && Memoria(emUso.IdMemoria) is { } memoria)
        {
            var regulagem = MudancasDaRegulagem(memoria, espelho);
            if (regulagem.Count > 0)
                linhas.Add(new LinhaSalvar(Camada.Regulagem, $"Regulagem → memória {memoria.Nome}", regulagem));

            // As 15 bandas contra o preset lembrado (sem ele, contra o EQ plano).
            var lembrado = PresetDe(memoria);
            var bandas = MudancasDeBandas(lembrado?.Valores ?? GerentePresets.BandasPlanas, espelho);
            if (bandas.Count > 0)
                linhas.Add(new LinhaSalvar(Camada.Bandas, lembrado is null ? "15 bandas → preset novo" : $"15 bandas → preset {lembrado.Nome}", bandas));
            if (linhas.Count == 0)
                return FolhaSalvar.Travada(MsgNadaParaSalvar, setup.Nome);
            var folha = new FolhaSalvar(null, linhas, PedeNome: false, setup.Nome);
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

        // O som do aparelho, ou o Setup sem memórias: a regulagem vai para uma
        // memória nova; as 15 bandas, para o preset igual ou um novo.
        linhas.Add(new LinhaSalvar(Camada.Regulagem, "Regulagem → memória nova", []));
        var (linha, igual) = LinhaBandasMemoriaNova(espelho);
        linhas.Add(linha);
        return new FolhaSalvar(null, linhas, PedeNome: true, setup.Nome)
        {
            NomePreset = igual?.Nome,
            PedeNomePreset = igual is null,
        };
    }

    /// <summary>
    /// A linha das 15 bandas de uma memória nova: se as bandas lidas são as de um preset, a memória
    /// lembra ele, sem pergunta; senão, um preset novo, com o nome.
    /// </summary>
    private (LinhaSalvar Linha, Preset? Igual) LinhaBandasMemoriaNova(IReadOnlyDictionary<Endereco, Valor> espelho)
    {
        if (_presets.ComAsBandas(espelho) is { } igual)
            return (new LinhaSalvar(Camada.Bandas, $"15 bandas → preset {igual.Nome}", []), igual);
        return (new LinhaSalvar(Camada.Bandas, "15 bandas → preset novo", []), null);
    }

    /// <summary>
    /// Salva o que a folha mostra, cada mudança na sua camada: a regulagem na memória em uso (ou numa
    /// nova, com o nome) e as 15 bandas no preset (atualizar o lembrado, criar um novo com o nome, ou
    /// lembrar o igual). Não manda nada ao aparelho. Nomes inválidos ou repetidos recusam antes de
    /// guardar qualquer coisa.
    /// </summary>
    public async Task<ResultadoSetup> SalvarAsync(string? nomeMemoriaNova,
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

            var setup = SetupEmUso!;
            var espelho = _estado.Copiar();
            var dados = Dados;
            var emUso = EmUso;
            var memoriaEmUso = emUso.Tipo == TipoSomEmUso.MemoriaCelular ? Memoria(emUso.IdMemoria) : null;
            var partes = new List<string>();

            // Tudo o que pode recusar vem antes de guardar qualquer coisa.
            MemoriaCelular? nova = null;
            if (folha.PedeNome)
            {
                var (memoria, motivo, _) = NovaMemoria(setup, nomeMemoriaNova, espelho, idPreset: null);
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

            IReadOnlyList<ValorCopiado>? regulagemNova = null;
            if (nova is null && memoriaEmUso is not null && MudancasDaRegulagem(memoriaEmUso, espelho).Count > 0)
            {
                var (regulagem, faltam) = Camadas.Tirar(espelho, Camada.Regulagem);
                if (faltam > 0)
                    return Terminar(ResultadoSetup.Falha(MsgIncompleta(faltam)));
                regulagemNova = regulagem;
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
                partes.Add($"regulagem na {nova.Nome}");
            }
            else if (memoriaEmUso is not null)
            {
                var atualizada = memoriaEmUso;
                if (regulagemNova is not null)
                {
                    atualizada = atualizada with { Regulagem = regulagemNova, Quando = _relogio.GetLocalNow() };
                    partes.Add($"regulagem na {memoriaEmUso.Nome}");
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
                Memorias = memorias,
                Uso = Com(dados.Uso, setup.Id, uso),
            };
            if (!await GravarAsync(novos))
                return Terminar(ResultadoSetup.Falha(MsgNaoGuardou));
            lock (_trava)
            {
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
        // História 13c: trocar o preset zera o corte do usuário no master (o lote pôs a conta da combinação nova).
        ZerarCorte();
        string? idMemoria;
        lock (_trava)
        {
            _ultimoPreset = preset.Id;
            idMemoria = _emUso is { Tipo: TipoSomEmUso.MemoriaCelular } uso ? uso.IdMemoria : null;
        }
        if (idMemoria is not null && Memoria(idMemoria) is { } memoria && memoria.IdPreset != preset.Id)
        {
            // História 13c: com o nível, o master que o aplicar pôs no lote fica registrado para a combinação nova.
            var master = memoria.Nivel is not null ? _estado.Valor(Camadas.CodigoMaster)?.Numero : null;
            await GravarUsoAsync(d => d with
            {
                Memorias = d.Memorias.Select(m => m.Id != idMemoria ? m
                    : master is { } valor ? ComMaster(m with { IdPreset = preset.Id }, preset.Id, valor)
                    : m with { IdPreset = preset.Id }).ToArray(),
            });
            _log.Evento($"memória {memoria.Nome} lembra o preset {preset.Nome}");
            return;
        }
        Notificar();
    }

    /// <summary>
    /// As memórias na ordem em que uma playlist pendente procura a dona: os Setups na ordem; dentro de
    /// cada Setup, na ordem de criação.
    /// </summary>
    public static IReadOnlyList<(string Id, string Nome)> OrdemDeAdocao(DadosSetups dados) =>
        dados.Setups
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

    /// <summary>As diferenças por canal em linhas da folha do Salvar, pelo nome das saídas.</summary>
    private static List<MudancaSalvar> Linhas(IEnumerable<DiferencaPorCanal> diferencas, IReadOnlyList<SaidaApp> saidas) =>
        RotulosSaida.Juntar(diferencas, saidas).Select(l => new MudancaSalvar(l.Rotulo, l.Antes, l.Depois, l.Canal)).ToList();

    /// <summary>
    /// A regulagem da memória contra a lida, pelo nome das saídas em uso ("Atraso · Sub: 80 cm → 85 cm";
    /// o par com link que muda igual é uma linha só). O antes é o valor guardado; o depois, o lido.
    /// </summary>
    private List<MudancaSalvar> MudancasDaRegulagem(MemoriaCelular memoria, IReadOnlyDictionary<Endereco, Valor> espelho) =>
        Linhas(Camadas.Diferentes(memoria.Regulagem, espelho)
                .Select(v => DiferencaPorCanal.De(v.Codigo, v.Indice, Camadas.Exibir(v.Codigo, v.NoFio),
                    Camadas.Exibir(v.Codigo, espelho.GetValueOrDefault(new Endereco(v.Codigo, v.Indice))?.NoFio))),
            SaidasEmUso);

    /// <summary>
    /// Uma memória nova com a regulagem lida e o preset lembrado, ou o motivo de não poder;
    /// <c>DoNome</c> diz que o motivo é o nome (inválido ou repetido), não a leitura.
    /// </summary>
    private (MemoriaCelular? Memoria, string? Motivo, bool DoNome) NovaMemoria(Setup setup, string? nomeDigitado,
        IReadOnlyDictionary<Endereco, Valor> espelho, string? idPreset)
    {
        if (NomeValido(nomeDigitado) is not { } nome)
            return (null, GerentePresets.MsgNomeInvalido, true);
        if (Dados.Memorias.Any(m => m.IdSetup == setup.Id && MesmoNome(m.Nome, nome)))
            return (null, MsgNomeRepetidoMemoria(setup.Nome), true);
        var (regulagem, faltam) = Camadas.Tirar(espelho, Camada.Regulagem);
        if (faltam > 0)
            return (null, MsgIncompleta(faltam), false);
        return (new MemoriaCelular($"memoria-{Guid.NewGuid():N}", setup.Id, nome, _relogio.GetLocalNow(), regulagem, idPreset), null, false);
    }

    // ------------------------------------------------------------------ aplicar (a regra 3)

    private sealed record Aplicado(bool Ok, string? Motivo, bool Iniciou, int Restam)
    {
        public static Aplicado Falha(string motivo) => new(false, motivo, false, 0);
    }

    /// <summary>
    /// A regulagem da memória (os valores finais) e as 15 bandas do preset que ela lembra; sem preset,
    /// as bandas não entram. <c>Sumiu</c>: ela lembra um preset que não existe mais (o arquivo foi
    /// editado); o alvo vai sem as bandas.
    /// </summary>
    /// <remarks>
    /// História 13c: com a memória num nível, o MASTER do alvo é o da conta na hora da combinação memória + preset
    /// (o guardado é só registro); <c>Conta</c> vem com ela, ou null (sem nível, ou o nível não é oferecido).
    /// </remarks>
    private (IReadOnlyList<ValorCopiado> Alvo, bool Sumiu, ContaNivel? Conta) Alvo(MemoriaCelular memoria)
    {
        var (alvo, sumiu) = AlvoGuardado(memoria);
        if (memoria.Nivel is not { } nivel || Setup(memoria.IdSetup) is not { } setup)
            return (alvo, sumiu, null);
        ContaNivel? conta;
        try
        {
            conta = ContaDoAlvo(setup, nivel, alvo);
        }
        catch (Exception e)
        {
            _log.Evento($"memória {memoria.Nome}: falha na conta do nível ({e.GetType().Name})");
            return (SemConta(memoria, alvo), sumiu, null);
        }
        if (conta?.Master is not { } master)
        {
            _log.Evento($"memória {memoria.Nome}: sem a conta do nível ({conta?.Falta ?? Niveis.Indisponivel(setup, Ligacoes.Saidas(setup.Equipamentos))})");
            return (SemConta(memoria, alvo), sumiu, null);
        }
        var noFio = Camadas.Db(master);
        IReadOnlyList<ValorCopiado> comMaster = alvo.Any(v => v.Codigo == Camadas.CodigoMaster)
            ? alvo.Select(v => v.Codigo == Camadas.CodigoMaster ? v with { NoFio = noFio } : v).ToArray()
            : [.. alvo, new ValorCopiado(Camadas.CodigoMaster, null, noFio)];
        return (comMaster, sumiu, conta);
    }

    /// <summary>
    /// O nível sem conta (a calibração ou o Bluetooth perdidos, um dado que falta): o master guardado não vai sem
    /// conferir; vai o menor entre ele e o de agora no aparelho.
    /// </summary>
    private IReadOnlyList<ValorCopiado> SemConta(MemoriaCelular memoria, IReadOnlyList<ValorCopiado> alvo)
    {
        var agora = _estado.Valor(Camadas.CodigoMaster)?.Numero;
        if (agora is not { } a || alvo.FirstOrDefault(v => v.Codigo == Camadas.CodigoMaster) is not { } guardado
            || Catalogo.ParametroDe(Camadas.CodigoMaster)?.Decodificar(guardado.NoFio).Valor?.Numero is not { } g || g <= a)
            return alvo;
        _log.Evento($"memória {memoria.Nome}: o master fica no de agora ({a.ToString("0.0", Fio)} dB), abaixo do guardado ({g.ToString("0.0", Fio)} dB)");
        var noFio = Camadas.Db(a);
        return alvo.Select(v => v.Codigo == Camadas.CodigoMaster ? v with { NoFio = noFio } : v).ToArray();
    }

    private (IReadOnlyList<ValorCopiado> Alvo, bool Sumiu) AlvoGuardado(MemoriaCelular memoria)
    {
        if (memoria.IdPreset is null)
            return (memoria.Regulagem, false);
        if (_presets.Obter(memoria.IdPreset) is not { } preset)
        {
            _log.Evento($"memória {memoria.Nome}: o preset lembrado não existe mais; aplica sem as bandas");
            return (memoria.Regulagem, true);
        }
        return ([.. memoria.Regulagem, .. preset.Valores], false);
    }

    /// <summary>
    /// Escreve o alvo com a cópia antes: só o que difere, uma escrita por vez, e relê tudo. Em caso de
    /// sucesso, a marca da cópia continua (quem chamou conclui, ou deixa pela metade); com
    /// <see cref="Aplicado.Iniciou"/> falso, não havia o que escrever e nenhuma marca começou.
    /// </summary>
    /// <param name="nivel">O nível da memória aplicada (história 13c): no Máximo, a cerca confere o lote com o teto min(X, C).</param>
    private async Task<Aplicado> AplicarAlvoAsync(Setup setup, IReadOnlyList<ValorCopiado> alvo, OperacaoPendente op, bool refazendo,
        NivelRendimento? nivel = null)
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
        // A cerca confere o lote inteiro antes da primeira escrita: com uma recusa, nada sai. O lote é do
        // Setup que ele aplica (no escolher Setup, ainda não é o em uso), com o link tirado das ligações
        // dele (o link automático, D1). Uma falha no cálculo recusa.
        string? cerca;
        try
        {
            cerca = Cerca.Conferir(setup.Equipamentos, Ligacoes.Saidas(setup.Equipamentos), _estado.Copiar(),
                escritas.Select(e => (new Endereco(e.Parametro.Codigo, e.Indice), (Valor?)e.Valor)), nivel);
        }
        catch (Exception e)
        {
            _log.Evento($"{op.Descricao}: falha ao conferir o limite ({e.GetType().Name})");
            cerca = Cerca.MsgFalhou;
        }
        if (cerca is not null)
        {
            _log.Evento($"{op.Descricao}: recusado pela cerca ({cerca})");
            return Aplicado.Falha(cerca);
        }
        // História 13b: pela porta, o lote inteiro passa pela regra da saída sem calibração antes da primeira escrita.
        if (Origem.Atual == OrigemComando.Ia)
        {
            string? semCalibracao;
            try
            {
                semCalibracao = SemCalibracao.Conferir(setup.Equipamentos, Ligacoes.Saidas(setup.Equipamentos),
                    escritas.Select(e => new Endereco(e.Parametro.Codigo, e.Indice)));
            }
            catch (Exception e)
            {
                _log.Evento($"{op.Descricao}: falha ao conferir a calibração ({e.GetType().Name})");
                semCalibracao = SemCalibracao.MsgFalhou;
            }
            if (semCalibracao is not null)
            {
                _log.Evento($"{op.Descricao}: recusado sem calibração ({semCalibracao})");
                return Aplicado.Falha(semCalibracao);
            }
        }
        var copia = await _copia.IniciarAsync(op, manterCopia: refazendo);
        if (!copia.Ok)
            return Aplicado.Falha(copia.Motivo!);

        _log.Evento($"{op.Descricao}: {escritas.Count} ajustes diferentes");
        var falhas = 0;
        string? ultimo = null;
        // O lote conferido inteiro não é conferido de novo escrita a escrita: o limitador sai primeiro e o
        // passa-alta por último, para uma falha no meio não deixar o corte novo com o limitador de antes.
        // História 13c: o MASTER que baixa sai junto com o limitador (o lado seguro), antes do resto da cadeia; o que
        // sobe, por último.
        var masterAgora = _estado.Valor(Camadas.CodigoMaster)?.Numero;
        foreach (var e in escritas.OrderBy(e => e.Parametro.Codigo switch
                 {
                     Cerca.CodigoLimitador => 0,
                     Camadas.CodigoMaster when masterAgora is null || e.Valor.Numero < masterAgora => 0,
                     Camadas.CodigoMaster => 3,
                     Cerca.CodigoPassaAlta or Cerca.CodigoFiltroPassaAlta => 2,
                     _ => 1,
                 }))
        {
            var r = await _escritor.EscreverAsync(e.Parametro, e.Indice, e.Valor, espelhar: false, daCopia: true, loteConferido: true);
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
            return Aplicado.Falha(CopiaAntes.MsgLeituraParou);
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

    /// <summary>
    /// A referência da conferência: o último Setup usado; sem ele, o primeiro Setup; sem nenhum Setup,
    /// null (a conferência muta e pede a escolha).
    /// </summary>
    private static Setup? ReferenciaEm(DadosSetups dados) =>
        dados.Setups.FirstOrDefault(s => s.Id == dados.UltimoSetup)
        ?? dados.Setups.FirstOrDefault();

    private static bool MesmoNome(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    // ------------------------------------------------------------------ guardado

    /// <summary>
    /// Grava os dados mudados a partir dos que valem na hora da gravação (com a vez da gravação: uma gravação no meio
    /// não se perde); só se gravou eles passam a valer.
    /// </summary>
    private async Task<bool> GravarAsync(Func<DadosSetups, DadosSetups> mudar)
    {
        await _gravacao.WaitAsync();
        try
        {
            DadosSetups novos;
            lock (_trava)
                novos = mudar(_dados);
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
        var migrou = false;
        var subiu = false;
        string? textoFormato3 = null;
        IReadOnlyList<string> memoriasQueSumiram = [];
        if (await Gravacao.LerAsync(_repositorio) is { } json)
        {
            try
            {
                lidos = DeJson(json);
                var formato = lidos is null ? null : FormatoDe(json);
                if (formato == Formato3)
                {
                    // História 13c: o 3 sobe ao 4 sem perder nada (as memórias ficam sem nível).
                    subiu = true;
                    textoFormato3 = json;
                }
                else if (lidos is not null && formato != Formato)
                {
                    migrou = true;
                    memoriasQueSumiram = IdsDasMemorias(json);
                }
            }
            catch (Exception e) when (e is JsonException or InvalidOperationException or FormatException)
            {
                // Arquivo estragado: volta ao vazio, sem erro.
            }
            if (lidos is null)
                _log.Evento("setups.json ilegível: voltei ao vazio (nenhum Setup)");
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
        if (subiu)
        {
            // História 13c: o formato 4 só acrescenta o nível e os masters à memória. Ficam os Setups, as memórias,
            // os botões, as usadas por último, as playlists e a marca; o arquivo já sai no 4.
            // Antes de regravar, a cópia do formato 3: um app mais antigo instalado depois não perde tudo sem aviso.
            if (_repositorioFormato3 is { } copia3 && textoFormato3 is not null)
            {
                _log.Evento(await Gravacao.GravarAsync(copia3, textoFormato3)
                    ? "setups.json formato 3 guardado em setups.formato3.json antes da migração"
                    : "não consegui guardar a cópia do setups.json formato 3");
            }
            _log.Evento("setups.json migrado ao formato 4: tudo ficou; as memórias estão sem nível");
            await _gravacao.WaitAsync();
            try
            {
                if (!await Gravacao.GravarAsync(_repositorio, ParaJson(Dados)))
                    _log.Evento("não consegui regravar o setups.json no formato 4");
            }
            finally
            {
                _gravacao.Release();
            }
        }
        if (migrou)
        {
            // O formato 3 começa do zero (decisão D2 do Denis, 29/10/2026): ficam os Setups e os
            // equipamentos; as memórias, os botões e as playlists delas somem. O arquivo já sai no 4.
            _log.Evento($"setups.json migrado ao formato {Formato}: ficaram os Setups e os equipamentos; as memórias do celular saíram");
            await _gravacao.WaitAsync();
            try
            {
                if (!await Gravacao.GravarAsync(_repositorio, ParaJson(Dados)))
                    _log.Evento($"não consegui regravar o setups.json no formato {Formato}");
            }
            finally
            {
                _gravacao.Release();
            }
            if (memoriasQueSumiram.Count > 0)
                await _playlists.ApagarAsync(memoriasQueSumiram);
            // As pendentes do playlists.json antigo eram das memórias de antes: somem também.
            await _playlists.DescartarPendentesAsync();
        }
        Notificar();
        // As playlists que esperam a memória delas (as do playlists.json antigo) procuram pelo nome.
        await AdotarPlaylistsAsync();
    }

    /// <summary>
    /// O JSON do <c>setups.json</c> (formato 4): cada Setup com os equipamentos e as fichas (cada dado
    /// com a origem) e o uso das memórias; cada memória com a regulagem, o preset lembrado e, da 13c, o nível
    /// (<c>nivel</c>: <c>baixo</c>, <c>medio</c>, <c>alto</c> ou <c>maximo</c>; ausente = sem nível) e os masters por
    /// preset (<c>masters</c>: {id do preset, ou "" sem preset → dB}); da 13d, <c>"otimizada": true</c> na memória cuja
    /// playlist é analisada (ausente = false; o formato não sobe).
    /// </summary>
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
                if (s.Ambiente is { } ambiente)
                    w.WriteString("ambiente", Ambientes.Nome(ambiente));
                w.WriteString("quando", s.Quando.ToString("O", Fio));
                if (s.NivelBluetooth is { } nivel)
                    EscreverNivelBluetooth(w, nivel);
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
                EscreverValores(w, "regulagem", m.Regulagem);
                if (m.Nivel is { } nivel)
                    w.WriteString("nivel", Niveis.Chave(nivel));
                if (m.Masters.Count > 0)
                {
                    w.WriteStartObject("masters");
                    foreach (var (idPreset, master) in m.Masters.OrderBy(x => x.Key, StringComparer.Ordinal))
                        w.WriteNumber(idPreset, master);
                    w.WriteEndObject();
                }
                if (m.Otimizada)
                    w.WriteBoolean("otimizada", true);
                w.WriteEndObject();
            }
            w.WriteEndArray();
            w.WriteEndObject();
        }
        return Encoding.UTF8.GetString(fluxo.ToArray());
    }

    /// <summary>O formato do <c>setups.json</c>, ou null fora da forma.</summary>
    internal static int? FormatoDe(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.ValueKind == JsonValueKind.Object
               && doc.RootElement.TryGetProperty("formato", out var f) && f.TryGetInt32(out var formato)
            ? formato
            : null;
    }

    /// <summary>Os ids das memórias de um <c>setups.json</c> (para apagar as playlists delas na migração).</summary>
    private static IReadOnlyList<string> IdsDasMemorias(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Object
            || !doc.RootElement.TryGetProperty("memorias", out var memorias) || memorias.ValueKind != JsonValueKind.Array)
            return [];
        return memorias.EnumerateArray()
            .Select(m => m.ValueKind == JsonValueKind.Object ? Texto(m, "id") : null)
            .OfType<string>()
            .Where(id => id.Length > 0)
            .Distinct()
            .ToArray();
    }

    /// <summary>
    /// Lê o <c>setups.json</c>. Fora dos formatos 1, 2, 3 e 4, com outra forma, ou com Setups e nenhum
    /// válido: null (o gerente volta ao vazio). Uma lista de Setups vazia vale (nenhum Setup). Uma
    /// entrada com id ou nome inválido, repetida, ou uma memória de um Setup que não existe, é
    /// descartada; as outras ficam. Um Setup com o id do antigo Casa 2.1 é um Setup comum.
    /// <list type="bullet">
    /// <item><b>Formato 4</b> (história 13c): o 3 com o nível e os masters por preset na memória (um nível que não
    /// vale fica null; um master que não é número sai).</item>
    /// <item><b>Formato 3</b> (história 8f): lido inteiro, sem perder nada, com as memórias sem nível. O Setup só físico (os equipamentos e as fichas com origem) e
    /// as memórias com a regulagem, e o ambiente opcional (<c>ambiente</c>; um texto que não vale fica
    /// null). Um equipamento ilegível é descartado; os outros ficam, e as ligações para ele se desfazem
    /// (<see cref="Ligacoes.Sanear"/>).</item>
    /// <item><b>Formatos 1 e 2</b> (a migração da 8f, decisão D2 do Denis): ficam os Setups, os
    /// equipamentos e o último Setup usado; somem as memórias, os botões, as memórias ativas, a
    /// calibração, o equilíbrio e o link. Os dados das fichas viram dados digitados. O formato 1 não
    /// tinha equipamentos: os Setups ficam sem nenhum.</item>
    /// </list>
    /// </summary>
    public static DadosSetups? DeJson(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var raiz = doc.RootElement;
        if (raiz.ValueKind != JsonValueKind.Object
            || !raiz.TryGetProperty("formato", out var f) || !f.TryGetInt32(out var formato)
            || formato is not (Formato or Formato3 or Formato2 or FormatoAntigo)
            || !raiz.TryGetProperty("setups", out var setupsJson) || setupsJson.ValueKind != JsonValueKind.Array)
            return null;
        // Os formatos 1 e 2 perdem as memórias (a migração da 8f); o 3 sobe ao 4 sem perder nada (13c).
        var migrando = formato is Formato2 or FormatoAntigo;
        var atual = formato is Formato or Formato3;

        var setups = new List<Setup>();
        var usos = new Dictionary<string, (List<string> Recentes, List<string?> Botoes)>();
        foreach (var item in setupsJson.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object || Texto(item, "id") is not { Length: > 0 } id
                || setups.Any(s => s.Id == id) || NomeSetupValido(Texto(item, "nome")) is not { } nome
                || setups.Any(s => MesmoNome(s.Nome, nome)))
                continue;
            IReadOnlyList<Equipamento> equipamentos = formato switch
            {
                FormatoAntigo => [],
                Formato2 => LerEquipamentos(item, legado: true),
                _ => LerEquipamentos(item, legado: false),
            };
            var ambiente = atual ? Ambientes.De(Texto(item, "ambiente")) : null;
            setups.Add(new Setup(id, nome, FichaJson.Quando(item), ambiente)
            {
                Equipamentos = equipamentos,
                NivelBluetooth = atual ? LerNivelBluetooth(item) : null,
            });
            usos[id] = migrando ? (new List<string>(), new List<string?>()) : (Ids(item, "recentes").OfType<string>().ToList(), Ids(item, "botoes"));
        }
        if (setups.Count == 0 && setupsJson.GetArrayLength() > 0)
            return null;

        var memorias = new List<MemoriaCelular>();
        if (!migrando && raiz.TryGetProperty("memorias", out var memoriasJson) && memoriasJson.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in memoriasJson.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object || Texto(item, "id") is not { Length: > 0 } id
                    || memorias.Any(m => m.Id == id) || Texto(item, "setup") is not { } idSetup
                    || setups.All(s => s.Id != idSetup) || NomeValido(Texto(item, "nome")) is not { } nome
                    || memorias.Any(m => m.IdSetup == idSetup && MesmoNome(m.Nome, nome)))
                    continue;
                var idPreset = Texto(item, "preset") is { Length: > 0 } preset ? preset : null;
                var memoria = new MemoriaCelular(id, idSetup, nome, FichaJson.Quando(item), LerValores(item, "regulagem", Camada.Regulagem), idPreset);
                if (formato == Formato)
                    memoria = memoria with
                    {
                        Nivel = Niveis.De(Texto(item, "nivel")),
                        Masters = LerMasters(item),
                        // História 13d: opcional, sem subir o formato; só o true vale.
                        Otimizada = item.TryGetProperty("otimizada", out var otimizada) && otimizada.ValueKind == JsonValueKind.True,
                    };
                memorias.Add(memoria);
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

    /// <summary>Os masters por preset de uma memória (história 13c): só os números; sem nenhum, o vazio de todas.</summary>
    private static IReadOnlyDictionary<string, decimal> LerMasters(JsonElement item)
    {
        if (!item.TryGetProperty("masters", out var ms) || ms.ValueKind != JsonValueKind.Object)
            return MemoriaCelular.SemMasters;
        var masters = new Dictionary<string, decimal>(StringComparer.Ordinal);
        foreach (var p in ms.EnumerateObject())
        {
            if (p.Value.ValueKind == JsonValueKind.Number && p.Value.TryGetDecimal(out var v) && v is >= PiorCaso.MinimoMaster and <= 0)
                masters[p.Name] = v;
        }
        return masters.Count == 0 ? MemoriaCelular.SemMasters : masters;
    }

    /// <summary>
    /// O nível do Bluetooth do Setup (história 13b): <c>"nivelBluetooth":{"valor":-13.0,"origem":"medido","fonte":"…"}</c>.
    /// O valor pode ser negativo; o formato do arquivo não muda (o campo é opcional).
    /// </summary>
    internal static void EscreverNivelBluetooth(Utf8JsonWriter w, DadoTecnico nivel)
    {
        w.WriteStartObject("nivelBluetooth");
        w.WriteNumber("valor", nivel.Valor);
        w.WriteString("origem", FichaJson.NomeOrigem(nivel.Origem));
        if (nivel.Fonte is { } fonte)
            w.WriteString("fonte", fonte);
        w.WriteEndObject();
    }

    /// <summary>O nível do Bluetooth guardado, ou null (ausente, sem número, sem origem conhecida ou com a fonte inválida).</summary>
    private static DadoTecnico? LerNivelBluetooth(JsonElement item)
    {
        if (!item.TryGetProperty("nivelBluetooth", out var n) || n.ValueKind != JsonValueKind.Object
            || !n.TryGetProperty("valor", out var valor) || valor.ValueKind != JsonValueKind.Number || !valor.TryGetDecimal(out var v)
            || FichaJson.OrigemDe(Texto(n, "origem")) is not { } origem)
            return null;
        var fonte = Texto(n, "fonte");
        if (fonte is not null && Ligacoes.TextoLongoValido(fonte) is null)
            return null;
        return new DadoTecnico(v, origem, fonte is null ? null : Ligacoes.TextoLongoValido(fonte));
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
        return new MarcaMudo(Texto(m, "setup"), mutes.Take(Parametro.Canais).ToArray(), FichaJson.Quando(m));
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

    // ------------------------------------------------------------------ equipamentos no JSON

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
            w.WriteString("grupo", FichaJson.NomeGrupo(e.Grupo));
            w.WriteString("categoria", e.Categoria);
            w.WriteString("nome", e.Nome);
            if (e.Modelo is { } modelo)
                w.WriteString("modelo", modelo);
            if (e.Funcao is { } funcao)
                w.WriteString("funcao", NomeFuncao(funcao));
            if (e.Nota is { } nota)
                w.WriteString("nota", nota);
            FichaJson.EscreverDados(w, e.Dados);
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

    /// <summary>
    /// Os equipamentos de um Setup. Fica o primeiro de cada id. Um bridge que não vale sai (o módulo
    /// fica). Um equipamento ilegível (sem id, grupo, nome ou categoria válidos, ou com um campo de outra
    /// forma) é descartado; os outros ficam, e as ligações para ele se desfazem. <paramref name="legado"/>
    /// (formato 2): os dados da ficha são números, que viram dados digitados.
    /// </summary>
    private static IReadOnlyList<Equipamento> LerEquipamentos(JsonElement setup, bool legado)
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
                e = LerEquipamento(item, legado);
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

    private static Equipamento? LerEquipamento(JsonElement item, bool legado)
    {
        if (item.ValueKind != JsonValueKind.Object || Texto(item, "id") is not { Length: > 0 } id
            || FichaJson.GrupoDe(Texto(item, "grupo")) is not { } grupo
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
            Dados = FichaJson.LerDados(item, legado),
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
