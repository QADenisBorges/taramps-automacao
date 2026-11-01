using System.Globalization;
using BgsControle.Nucleo.Aparelho;
using BgsControle.Nucleo.Escrita;
using BgsControle.Nucleo.Gerador;
using BgsControle.Nucleo.Player;
using BgsControle.Nucleo.Registro;
using BgsControle.Nucleo.Seguranca;
using BgsControle.Nucleo.Setups;
using BgsControle.Nucleo.Uso;

namespace BgsControle.Nucleo.Rendimento;

/// <summary>Os passos do teste a seco (história 13e; <c>telas-rendimento.md</c>, "Teste a seco"), nesta ordem.</summary>
public enum EtapaTesteSeco
{
    /// <summary>Nenhum teste aberto.</summary>
    Fechado,

    /// <summary>1) "O módulo vai ser desligado." O usuário confirma.</summary>
    Aviso,

    /// <summary>2) O player para, o módulo desliga (252 = 0), a cópia antes e o master no candidato.</summary>
    Preparar,

    /// <summary>3) Os trechos fortes da análise tocando, com o 150 escutado.</summary>
    Tocando,

    /// <summary>4) O teto validado (ou a falha), com o módulo religado só no validado.</summary>
    Resultado,
}

/// <summary>Um clip no teste a seco: a faixa, a saída e o master para o qual o teto desceu.</summary>
/// <param name="Canal">O canal do clip (0 a 3).</param>
public sealed record ClipNoTeste(string IdFaixa, string Faixa, int Canal, string Saida, decimal Master);

/// <summary>Um trecho a tocar no teste a seco: a faixa (n de total) e o trecho (1 ou 2) com o início e o fim.</summary>
public sealed record TrechoDoTeste(Faixa Faixa, int Numero, int Total, int Indice, Trecho Tempo)
{
    /// <summary>"Faixa 2 de 3: menos 6, trecho 1".</summary>
    public string Texto => TesteSeco.TextoTrecho(Numero, Total, Faixa.Nome, Indice);
}

/// <summary>Como terminou um passo do teste a seco. <see cref="Mensagem"/> é a frase para a tela.</summary>
public sealed record ResultadoTesteSeco(bool Ok, string? Motivo, string? Mensagem = null)
{
    public static ResultadoTesteSeco Falha(string motivo) => new(false, motivo, motivo);

    public static ResultadoTesteSeco Feito(string? mensagem = null) => new(true, null, mensagem);
}

/// <summary>
/// O teste a seco do Extra (história 13e, spec-rendimento CAP-6): com o módulo desligado, o processador e o celular
/// rodam a cadeia inteira, mas nada chega aos alto-falantes. O player do app toca os trechos fortes que a análise da
/// playlist achou (13d) com o master no teto candidato, e o app escuta o clip (<c>150</c>): cada clip baixa o master
/// um passo (<see cref="Passo"/>) e o mesmo trecho toca de novo. No fim, o master que passou por todos os trechos é o
/// teto validado (<see cref="MemoriaCelular.ExtraValidado"/>), e só então o módulo religa.
/// <list type="bullet">
/// <item><b>Para começar:</b> a memória do celular em uso otimizada, com a análise "pronta" no preset em uso, conexão
/// e leitura prontas, nenhuma operação na cópia, as chaves do Remote out e dos Ajustes de áudio, o gerador livre.</item>
/// <item><b>Estado seguro:</b> o módulo só volta a ligar com o master no validado. O Cancelar e uma falha voltam o
/// master à cópia e deixam o módulo desligado; a queda deixa a cópia "pela metade" (o Terminar não refaz; o Voltar à
/// cópia devolve o master, e o módulo continua desligado até o usuário ligar). Fechar o app não religa nada.</item>
/// </list>
/// Crie cedo (antes da primeira conexão): o tipo <see cref="Tipo"/> fica registrado na <see cref="CopiaAntes"/>.
/// </summary>
public sealed class TesteSeco : IDisposable
{
    /// <summary>O tipo da marca na <see cref="CopiaAntes"/>; o alvo é o id da memória.</summary>
    public const string Tipo = "teste-seco";

    /// <summary>Quanto o master desce a cada clip [ASSUMPTION: o passo de 1 dB].</summary>
    public const decimal Passo = 1m;

    /// <summary>Quanto o app ainda escuta o 150 depois do fim de um trecho (o aviso pode chegar atrasado).</summary>
    public static readonly TimeSpan EsperaClipPadrao = TimeSpan.FromMilliseconds(500);

    public const string MsgAviso = "O módulo vai ser desligado.";
    public const string MsgPreparando = "Desligando o módulo e pondo o master no teto candidato.";
    public const string MsgNaoDesligou = "Não consegui desligar o módulo.";
    public const string MsgClipNoMinimo = "O clip não sumiu nem com o master no mínimo.";
    public const string MsgSemConexao = "Sem conexão com o aparelho.";
    public const string MsgSemMemoria = "Aplique uma memória do celular antes do teste a seco.";
    public const string MsgSemOtimizar = "Otimize a memória na playlist antes do teste a seco.";
    public const string MsgSemTrechos = "A análise não achou nenhum trecho para tocar.";
    public const string MsgChaveRemoteOut = "Ligue a chave do Remote out antes do teste a seco.";
    public const string MsgChaveAjustes = "Ligue a chave dos Ajustes de áudio antes do teste a seco.";
    public const string MsgJaAberto = "Já há um teste a seco em andamento.";
    public const string MsgFechado = "Nenhum teste a seco em andamento.";
    public const string MsgForaDaEtapa = "Este passo não é o do teste a seco agora.";
    public const string MsgCancelado = "Teste a seco cancelado. O master voltou ao de antes, e o módulo continua desligado.";
    public const string MsgCaiu = "A conexão caiu no meio do teste a seco. O módulo continua desligado.";
    public const string MsgFechouApp = "O app fechou no meio do teste a seco. O módulo continua desligado.";
    public const string MsgNaoGuardou = "Não consegui guardar o teto validado no celular.";
    public const string MsgPlayerOcupado = "O teste a seco está usando o player.";
    public const string MsgModuloDesligado = "O módulo continua desligado. Ligue quando quiser.";
    public const string MsgVolume = "Não consegui pôr o volume do celular no máximo. Confira o limite de volume e o Não perturbe.";
    public const string MsgMasterForaDoValidado = "o master no aparelho não está no validado.";

    /// <summary>Quantas vezes um trecho que não tocou inteiro toca de novo antes de o teste falhar.</summary>
    public const int MaximoTentativas = 3;

    /// <summary>"O limitador da JBL L está desligado." (com o limitador OFF o 150 não acusa nada).</summary>
    public static string MsgLimitadorDesligado(string saida) => $"O limitador da {saida} está desligado.";

    /// <summary>"Faixa 2 de 3: menos 6, trecho 1".</summary>
    public static string TextoTrecho(int numero, int total, string nome, int trecho) => $"Faixa {numero} de {total}: {nome}, trecho {trecho}";

    /// <summary>"Teto validado: −4,7 dB."</summary>
    public static string MsgValidado(decimal master) => $"Teto validado: {LimiteFisico.Db(master)} dB.";

    /// <summary>"Clip na menos 3, saída JBL L: o teto desceu para −5,7 dB."</summary>
    public static string MsgClipNa(string faixa, string saida, decimal master) =>
        $"Clip na {faixa}, saída {saida}: o teto desceu para {LimiteFisico.Db(master)} dB.";

    /// <summary>"O celular não conseguiu tocar a menos 3."</summary>
    public static string MsgNaoTocou(string faixa) => $"O celular não conseguiu tocar a {faixa}.";

    /// <summary>"O módulo não religou: … Ligue quando quiser."</summary>
    public static string MsgNaoReligou(string motivo) => $"O módulo não religou: {motivo} Ligue quando quiser.";

    /// <summary>"O master não foi escrito: …"</summary>
    public static string MsgMasterNaoEscrito(string motivo) => $"O master não foi escrito: {motivo}";

    private static readonly CultureInfo Fio = CultureInfo.InvariantCulture;
    private static readonly Parametro Master = Catalogo.ParametroDe(Camadas.CodigoMaster)!;

    private readonly Sessao _sessao;
    private readonly EstadoAparelho _estado;
    private readonly Escritor _escritor;
    private readonly CopiaAntes _copia;
    private readonly GerenteSetups _setups;
    private readonly AnalisePlaylist _analise;
    private readonly ComandosAparelho _comandos;
    private readonly MonitorAparelho _monitor;
    private readonly IReprodutor _reprodutor;
    private readonly IChaves _chaves;
    private readonly Player.Player? _player;
    private readonly GeradorSinal? _gerador;
    private readonly LogBle _log;
    private readonly TimeProvider _relogio;
    private readonly TimeSpan _esperaClip;
    private readonly IVolumeCelular? _volume;

    private readonly object _trava = new();
    private EtapaTesteSeco _etapa = EtapaTesteSeco.Fechado;
    private long _geracao;
    private string? _idMemoria;
    private string? _nomeMemoria;
    private string? _chavePreset;
    private string? _impressao;
    private decimal? _candidato;
    private decimal? _master;
    private IReadOnlyList<TrechoDoTeste> _plano = [];
    private int _indice = -1;
    private readonly List<ClipNoTeste> _clips = [];
    private readonly HashSet<int> _clipCanais = [];
    private bool _escutando;
    private bool _ok;
    private bool _copiaIniciada;
    private string? _mensagem;
    private CancellationTokenSource? _cancelar;
    private Task<ResultadoTesteSeco>? _execucao;
    private string? _fim;

    public TesteSeco(
        Sessao sessao,
        EstadoAparelho estado,
        Escritor escritor,
        CopiaAntes copia,
        GerenteSetups setups,
        AnalisePlaylist analise,
        ComandosAparelho comandos,
        MonitorAparelho monitor,
        IReprodutor reprodutor,
        IChaves chaves,
        LogBle log,
        Player.Player? player = null,
        GeradorSinal? gerador = null,
        TimeProvider? relogio = null,
        TimeSpan? esperaClip = null,
        IVolumeCelular? volume = null)
    {
        _volume = volume;
        _sessao = sessao;
        _estado = estado;
        _escritor = escritor;
        _copia = copia;
        _setups = setups;
        _analise = analise;
        _comandos = comandos;
        _monitor = monitor;
        _reprodutor = reprodutor;
        _chaves = chaves;
        _player = player;
        _gerador = gerador;
        _log = log;
        _relogio = relogio ?? TimeProvider.System;
        _esperaClip = esperaClip ?? EsperaClipPadrao;

        // Uma marca achada na conexão nova: pela metade. O Terminar não refaz (o teste pede o usuário); o Voltar à cópia
        // devolve o master, e o módulo (252, fora da cópia) continua desligado até o usuário ligar.
        _copia.Registrar(Tipo,
            _ => Task.FromResult(Retomada.PelaMetade),
            _ => Task.FromResult(ResultadoCopia.Falha(CopiaAntes.MsgNaoSeiRefazer)));
        _sessao.Mudou += AoMudarSessao;
        _monitor.Clip += AoClip;
        if (_player is not null)
            _player.Bloqueio = () => EmOperacao ? MsgPlayerOcupado : null;
    }

    /// <summary>A etapa, o trecho, a mensagem ou o master mudou. Pode chegar em qualquer thread.</summary>
    public event Action? Mudou;

    public EtapaTesteSeco Etapa
    {
        get
        {
            lock (_trava)
                return _etapa;
        }
    }

    /// <summary>O teste anda (do Preparar ao Resultado): o player do app fica com ele.</summary>
    public bool EmOperacao
    {
        get
        {
            lock (_trava)
                return _etapa is EtapaTesteSeco.Preparar or EtapaTesteSeco.Tocando || _execucao is { IsCompleted: false };
        }
    }

    /// <summary>A memória do teste ("FILME"), ou null.</summary>
    public string? NomeMemoria
    {
        get
        {
            lock (_trava)
                return _nomeMemoria;
        }
    }

    /// <summary>O teto candidato da análise (13d) com que o teste começou, ou null.</summary>
    public decimal? Candidato
    {
        get
        {
            lock (_trava)
                return _candidato;
        }
    }

    /// <summary>O master do teste agora (o candidato menos os passos dos clips), ou null.</summary>
    public decimal? MasterAtual
    {
        get
        {
            lock (_trava)
                return _master;
        }
    }

    /// <summary>Os trechos a tocar, na ordem (2 por faixa).</summary>
    public IReadOnlyList<TrechoDoTeste> Plano
    {
        get
        {
            lock (_trava)
                return _plano;
        }
    }

    /// <summary>O trecho que toca (ou tocou por último), ou null.</summary>
    public TrechoDoTeste? TrechoAtual
    {
        get
        {
            lock (_trava)
                return _indice >= 0 && _indice < _plano.Count ? _plano[_indice] : null;
        }
    }

    /// <summary>Quantos trechos já passaram sem clip (o progresso).</summary>
    public int TrechosFeitos
    {
        get
        {
            lock (_trava)
                return Math.Max(0, _indice);
        }
    }

    /// <summary>Os clips do teste, na ordem.</summary>
    public IReadOnlyList<ClipNoTeste> Clips
    {
        get
        {
            lock (_trava)
                return _clips.ToArray();
        }
    }

    /// <summary>O teste terminou com o teto validado.</summary>
    public bool Validado
    {
        get
        {
            lock (_trava)
                return _etapa == EtapaTesteSeco.Resultado && _ok;
        }
    }

    /// <summary>O módulo está desligado (252 = 0 no espelho).</summary>
    public bool ModuloDesligado => _estado.Valor(Catalogo.CodigoRemoteOut)?.Numero == 0m;

    /// <summary>A última frase (a recusa, o resultado, o cancelar), ou null.</summary>
    public string? Mensagem
    {
        get
        {
            lock (_trava)
                return _mensagem;
        }
    }

    private bool SessaoAberta => _sessao.Estado == EstadoSessao.Conectado && _sessao.SegurandoConexao;

    // ------------------------------------------------------------------ para começar

    /// <summary>
    /// Um impedimento de fora (história 13f: o descanso do Extra, com o Bluetooth travado, deixaria o teste sem sinal e
    /// qualquer teto passaria), ou null.
    /// </summary>
    public Func<string?>? Impedimento { get; set; }

    /// <summary>Por que não dá para fazer o teste a seco agora, ou null. Cada recusa com o seu motivo.</summary>
    public string? MotivoParaComecar() => Preparar().Motivo;

    /// <summary>
    /// O teste da memória em uso com o preset em uso: o candidato, a chave do preset, a impressão e os trechos (2 por
    /// faixa analisada com som, na ordem da playlist), ou o motivo.
    /// </summary>
    private (MemoriaCelular? Memoria, TetoExtra? Teto, string? Impressao, IReadOnlyList<TrechoDoTeste> Plano, string? Motivo) Preparar()
    {
        if (!SessaoAberta)
            return Recusa(MsgSemConexao);
        if (_sessao.SoLeitura)
            return Recusa(Calibracao.MsgSoLeitura);
        if (_estado.Lendo)
            return Recusa(Escritor.MsgLendo);
        if (!_estado.Pronto)
            return Recusa(Escritor.MsgSemLeitura);
        if (_setups.EmEscolha)
            return Recusa(GerenteSetups.MsgDuvida);
        if (_setups.MemoriaEmUso is not { } memoria)
            return Recusa(MsgSemMemoria);
        string? impedimento;
        try
        {
            impedimento = Impedimento?.Invoke();
        }
        catch (Exception e)
        {
            _log.Evento($"teste a seco: falha ao conferir o descanso do Extra ({e.GetType().Name})");
            impedimento = Niveis.MsgFalhou;
        }
        if (impedimento is not null)
            return Recusa(impedimento);
        if (!memoria.Otimizada)
            return Recusa(MsgSemOtimizar);
        TetoExtra teto;
        string? impressao;
        try
        {
            teto = _analise.TetoEmUso();
            impressao = _analise.ImpressaoAtual(memoria.Id, _setups.IdPresetEmUso);
        }
        catch (Exception e)
        {
            _log.Evento($"teste a seco: falha ao ler a análise ({e.GetType().Name})");
            return Recusa(Niveis.MsgFalhou);
        }
        if (teto.Estado != EstadoExtra.Pronta || teto.Master is null)
            return Recusa(teto.Motivo ?? teto.TextoNivel);
        if (impressao is null)
            return Recusa(teto.Motivo ?? Niveis.MsgFalhou);
        if (_copia.MotivoBloqueio() is { } bloqueio)
            return Recusa(bloqueio);
        if (!_chaves.Ligada(Chave.RemoteOut))
            return Recusa(MsgChaveRemoteOut);
        if (!_chaves.Ligada(Chave.AjustesAudio))
            return Recusa(MsgChaveAjustes);
        if (_gerador is { } g && (g.TalvezLigado || g.Estado != EstadoGerador.Desligado))
            return Recusa(GeradorSinal.MsgEmUso);
        if (_comandos.Ocupado)
            return Recusa(ComandosAparelho.MsgOcupado);
        // Com o limitador OFF numa saída calibrada, o 150 não acusa nada e qualquer teto passaria.
        try
        {
            foreach (var limite in _setups.LimitesEmUso.Where(l => l.Calibrada && l.TetoDb is not null))
            {
                foreach (var c in limite.PorCanal)
                {
                    if (_estado.Valor(Cerca.CodigoLimitador, c.Canal)?.Numero is not { } limiar || limiar == Cerca.LimitadorOff)
                        return Recusa(MsgLimitadorDesligado(limite.Nome));
                }
            }
        }
        catch (Exception e)
        {
            _log.Evento($"teste a seco: falha ao conferir o limitador ({e.GetType().Name})");
            return Recusa(Cerca.MsgFalhou);
        }

        var faixas = teto.Faixas.Where(f => f.Situacao == SituacaoFaixa.Pronta && f.Trechos.Count > 0).ToArray();
        var plano = new List<TrechoDoTeste>();
        for (var i = 0; i < faixas.Length; i++)
        {
            for (var t = 0; t < faixas[i].Trechos.Count; t++)
                plano.Add(new TrechoDoTeste(faixas[i].Faixa, i + 1, faixas.Length, t + 1, faixas[i].Trechos[t]));
        }
        if (plano.Count == 0)
            return Recusa(MsgSemTrechos);
        return (memoria, teto, impressao, plano, null);

        static (MemoriaCelular?, TetoExtra?, string?, IReadOnlyList<TrechoDoTeste>, string?) Recusa(string motivo) => (null, null, null, [], motivo);
    }

    // ------------------------------------------------------------------ os passos

    /// <summary>Abre o teste da memória em uso: o passo 1, "O módulo vai ser desligado."</summary>
    public ResultadoTesteSeco Abrir()
    {
        lock (_trava)
        {
            if (_etapa is not (EtapaTesteSeco.Fechado or EtapaTesteSeco.Resultado) || _execucao is { IsCompleted: false })
                return ResultadoTesteSeco.Falha(MsgJaAberto);
        }
        var (memoria, teto, impressao, plano, motivo) = Preparar();
        if (motivo is not null)
            return Recusar(motivo);
        lock (_trava)
        {
            if (_etapa is not (EtapaTesteSeco.Fechado or EtapaTesteSeco.Resultado) || _execucao is { IsCompleted: false })
                return ResultadoTesteSeco.Falha(MsgJaAberto);
            _geracao++;
            _idMemoria = memoria!.Id;
            _nomeMemoria = memoria.Nome;
            _chavePreset = _setups.ChaveExtra(_setups.IdPresetEmUso);
            _impressao = impressao;
            _candidato = teto!.Master;
            _master = teto.Master;
            _plano = plano;
            _indice = -1;
            _clips.Clear();
            _ok = false;
            _copiaIniciada = false;
            _fim = null;
            _mensagem = null;
            _etapa = EtapaTesteSeco.Aviso;
        }
        _log.Evento($"teste a seco da {memoria!.Nome}: aberto, candidato {Texto(teto!.Master!.Value)} dB, {plano.Count} trechos");
        Notificar();
        return ResultadoTesteSeco.Feito();
    }

    /// <summary>
    /// Passo 1, confirmar: o teste anda até o fim (o player para, o módulo desliga, a cópia, o master no candidato, os
    /// trechos e o resultado). Volta com o resultado; o Cancelar e a queda param no meio.
    /// </summary>
    public async Task<ResultadoTesteSeco> ConfirmarAsync()
    {
        lock (_trava)
        {
            if (_etapa != EtapaTesteSeco.Aviso)
                return ResultadoTesteSeco.Falha(_etapa == EtapaTesteSeco.Fechado ? MsgFechado : MsgForaDaEtapa);
        }
        // As condições de novo (a conexão, a análise e a cópia podem ter mudado desde o aviso): o candidato e os trechos de agora.
        var (memoria, teto, impressao, plano, motivo) = Preparar();
        if (motivo is not null)
            return Recusar(motivo);
        Task<ResultadoTesteSeco> execucao;
        lock (_trava)
        {
            if (_etapa != EtapaTesteSeco.Aviso)
                return ResultadoTesteSeco.Falha(_etapa == EtapaTesteSeco.Fechado ? MsgFechado : MsgForaDaEtapa);
            if (memoria!.Id != _idMemoria)
                return ResultadoTesteSeco.Falha(MsgSemMemoria);
            _chavePreset = _setups.ChaveExtra(_setups.IdPresetEmUso);
            _impressao = impressao;
            _candidato = teto!.Master;
            _master = teto.Master;
            _plano = plano;
            _cancelar?.Dispose();
            _cancelar = new CancellationTokenSource();
            _etapa = EtapaTesteSeco.Preparar;
            _mensagem = null;
            var geracao = _geracao;
            var token = _cancelar.Token;
            execucao = _execucao = Task.Run(() => ExecutarAsync(geracao, token));
        }
        Notificar();
        return await execucao;
    }

    /// <summary>
    /// Cancelar: no aviso, só fecha. Com o teste andando, o player para, o master volta à cópia e o módulo fica
    /// desligado. Volta quando o teste parou.
    /// </summary>
    public async Task<ResultadoTesteSeco> CancelarAsync()
    {
        Task<ResultadoTesteSeco>? execucao;
        lock (_trava)
        {
            if (_etapa == EtapaTesteSeco.Fechado)
                return ResultadoTesteSeco.Falha(MsgFechado);
            if (_etapa is EtapaTesteSeco.Aviso or EtapaTesteSeco.Resultado && _execucao is not { IsCompleted: false })
            {
                _geracao++;
                _etapa = EtapaTesteSeco.Fechado;
                _mensagem = null;
                execucao = null;
            }
            else
            {
                _fim ??= MsgCancelado;
                _cancelar?.Cancel();
                execucao = _execucao;
            }
        }
        if (execucao is null)
        {
            Notificar();
            return ResultadoTesteSeco.Feito();
        }
        _log.Evento("teste a seco: cancelar");
        await PararReprodutorAsync();
        return await execucao;
    }

    /// <summary>No resultado: fecha o teste, sem mexer no aparelho.</summary>
    public ResultadoTesteSeco Concluir()
    {
        lock (_trava)
        {
            if (_etapa != EtapaTesteSeco.Resultado || _execucao is { IsCompleted: false })
                return ResultadoTesteSeco.Falha(_etapa == EtapaTesteSeco.Fechado ? MsgFechado : MsgForaDaEtapa);
            _geracao++;
            _etapa = EtapaTesteSeco.Fechado;
            _mensagem = null;
        }
        Notificar();
        return ResultadoTesteSeco.Feito();
    }

    /// <summary>
    /// Fechar o app no meio do teste: o trecho para e a cópia fica "pela metade" (com a conexão aberta; sem ela, para a
    /// próxima conexão). Nada religa.
    /// </summary>
    public void InterromperAoFechar()
    {
        lock (_trava)
        {
            if (_execucao is not { IsCompleted: false })
                return;
            _fim = MsgFechouApp;
            _cancelar?.Cancel();
        }
        _log.Evento("teste a seco: o app fechou no meio; o módulo continua desligado");
        _ = PararReprodutorAsync();
    }

    // ------------------------------------------------------------------ a execução

    private async Task<ResultadoTesteSeco> ExecutarAsync(long geracao, CancellationToken token)
    {
        try
        {
            return await RodarAsync(geracao, token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            return await PararNoMeioAsync(geracao);
        }
        catch (Exception e)
        {
            _log.Evento($"teste a seco: erro inesperado ({e.GetType().Name})");
            return await FalharAsync(geracao, $"Erro inesperado ({e.GetType().Name}).");
        }
        finally
        {
            lock (_trava)
                _escutando = false;
        }
    }

    private async Task<ResultadoTesteSeco> RodarAsync(long geracao, CancellationToken token)
    {
        string idMemoria, nome;
        decimal master;
        IReadOnlyList<TrechoDoTeste> plano;
        lock (_trava)
        {
            idMemoria = _idMemoria!;
            nome = _nomeMemoria!;
            master = _master!.Value;
            plano = _plano;
        }

        // 1) O player do app pausa (o teste usa o reprodutor).
        if (_player is { } player)
            await player.PausarAsync("para o teste a seco");
        token.ThrowIfCancellationRequested();

        // 2) O volume do celular no máximo (o valor da conta): abaixo dele, o teste validaria um teto alto demais.
        if (await VolumeNoMaximoAsync() is { } semVolume)
            return await FalharAsync(geracao, semVolume);
        token.ThrowIfCancellationRequested();

        // 3) O módulo desliga, conferido pela releitura. Sem isso, nada mais acontece.
        _log.Evento($"teste a seco da {nome}: desligando o módulo");
        var desligar = await _comandos.DefinirRemoteOutAsync(false);
        if (!desligar.Ok || !ModuloDesligado)
        {
            _log.Evento($"teste a seco: o módulo não desligou ({desligar.Motivo ?? "o aparelho ficou ligado"})");
            return await FalharAsync(geracao, MsgNaoDesligou);
        }
        token.ThrowIfCancellationRequested();

        // 4) A cópia antes (o master volta por ela no Cancelar e na falha).
        var copia = await _copia.IniciarAsync(new OperacaoPendente(Tipo, idMemoria, $"Teste a seco da {nome}"));
        if (!copia.Ok)
            return await FalharAsync(geracao, copia.Motivo ?? CopiaAntes.MsgNaoGuardou);
        lock (_trava)
            _copiaIniciada = true;
        token.ThrowIfCancellationRequested();

        // 5) O master no candidato.
        if (await EscreverMasterAsync(master) is { } falhaMaster)
            return await FalharAsync(geracao, MsgMasterNaoEscrito(falhaMaster));
        Atualizar(() => _etapa = EtapaTesteSeco.Tocando);

        // 6) Os trechos, na ordem, com o 150 escutado e o volume do celular no máximo.
        for (var i = 0; i < plano.Count; i++)
        {
            var trecho = plano[i];
            var tentativas = 0;
            while (true)
            {
                token.ThrowIfCancellationRequested();
                if (await VolumeNoMaximoAsync() is { } volume)
                    return await FalharAsync(geracao, volume);
                Atualizar(() =>
                {
                    _indice = i;
                    _clipCanais.Clear();
                    _escutando = true;
                });
                _log.Evento($"teste a seco: {trecho.Texto} ({trecho.Tempo.Inicio.ToString("0.0", Fio)} a {trecho.Tempo.Fim.ToString("0.0", Fio)} s)");
                var tocou = await _reprodutor.TocarTrechoAsync(trecho.Faixa.Uri, TimeSpan.FromSeconds(trecho.Tempo.Inicio),
                    TimeSpan.FromSeconds(Math.Max(0, trecho.Tempo.Fim - trecho.Tempo.Inicio)), token);
                token.ThrowIfCancellationRequested();
                if (_esperaClip > TimeSpan.Zero)
                    await Task.Delay(_esperaClip, _relogio, token);
                int[] canais;
                lock (_trava)
                {
                    _escutando = false;
                    canais = _clipCanais.Order().ToArray();
                }
                if (canais.Length == 0)
                {
                    if (tocou)
                        break;
                    // O trecho não tocou inteiro (o reprodutor pausou sozinho, o arquivo falhou): toca de novo, até o limite.
                    if (++tentativas >= MaximoTentativas)
                        return await FalharAsync(geracao, MsgNaoTocou(trecho.Faixa.Nome));
                    _log.Evento($"teste a seco: {trecho.Texto} não tocou inteiro; de novo");
                    continue;
                }

                // Um clip (numa saída ou em várias): o master desce um passo e o mesmo trecho toca de novo. No mínimo, falha.
                var saidas = canais.Select(NomeSaida).ToArray();
                _log.Evento($"teste a seco: clip na {trecho.Faixa.Nome}, saída {string.Join(", ", saidas)}, com o master em {Texto(master)} dB");
                if (master <= PiorCaso.MinimoMaster)
                    return await FalharAsync(geracao, MsgClipNoMinimo);
                var novo = Math.Max(master - Passo, PiorCaso.MinimoMaster);
                if (await EscreverMasterAsync(novo) is { } falha)
                    return await FalharAsync(geracao, MsgMasterNaoEscrito(falha));
                master = novo;
                var clips = canais.Select((c, k) => new ClipNoTeste(trecho.Faixa.Id, trecho.Faixa.Nome, c, saidas[k], novo)).ToArray();
                Atualizar(() =>
                {
                    _master = novo;
                    _clips.AddRange(clips);
                });
                // O 150 fica aceso uns 3 s depois do último aviso: o que chega nesse tempo é o mesmo clip, e não custa outro passo.
                await Task.Delay(MonitorAparelho.ClipAceso, _relogio, token);
            }
        }
        return await ValidarAsync(geracao, idMemoria, master, token);
    }

    /// <summary>
    /// O resultado: o master confirmado no aparelho, depois o validado (com a impressão da cadeia) na memória e a cópia
    /// concluída, e só então o módulo religa. Depois de um Cancelar ou do fechar do app, o módulo não religa.
    /// </summary>
    private async Task<ResultadoTesteSeco> ValidarAsync(long geracao, string idMemoria, decimal master, CancellationToken token)
    {
        string chave, impressao;
        lock (_trava)
        {
            chave = _chavePreset ?? "";
            impressao = _impressao!;
        }
        Atualizar(() => _indice = _plano.Count);
        token.ThrowIfCancellationRequested();

        // 1) O master no validado, confirmado.
        if (_estado.Valor(Camadas.CodigoMaster)?.Numero != master && await EscreverMasterAsync(master) is { } falha)
            return await FalharAsync(geracao, MsgMasterNaoEscrito(falha));
        if (_estado.Valor(Camadas.CodigoMaster)?.Numero != master)
            return await FalharAsync(geracao, MsgMasterNaoEscrito(MsgMasterForaDoValidado));
        token.ThrowIfCancellationRequested();

        // 2) O validado na memória e a cópia concluída.
        if (!await _setups.GravarExtraValidadoAsync(idMemoria, chave, master, impressao))
            return await FalharAsync(geracao, MsgNaoGuardou);
        await _copia.ConcluirAsync();
        lock (_trava)
            _copiaIniciada = false;

        IReadOnlyList<ClipNoTeste> clips;
        lock (_trava)
            clips = _clips.ToArray();
        var frase = clips.Count == 0 ? MsgValidado(master) : MsgClipNa(clips[^1].Faixa, clips[^1].Saida, master);

        // 3) O módulo religa: nunca depois de um Cancelar ou do fechar do app, e só com o master no validado.
        string? parado;
        lock (_trava)
            parado = _fim ?? (token.IsCancellationRequested ? MsgCancelado : null);
        if (parado is not null)
        {
            _log.Evento($"teste a seco: teto validado em {Texto(master)} dB, mas o teste parou antes de religar; o módulo continua desligado");
            return Terminar(geracao, $"{frase} {MsgModuloDesligado}", false);
        }
        string? causa = null;
        if (!SessaoAberta)
        {
            causa = MsgSemConexao;
        }
        else if (_estado.Valor(Camadas.CodigoMaster)?.Numero != master)
        {
            causa = MsgMasterForaDoValidado;
        }
        else
        {
            _log.Evento($"teste a seco: teto validado em {Texto(master)} dB; religando o módulo");
            var religar = await _comandos.DefinirRemoteOutAsync(true);
            if (!religar.Ok)
                causa = religar.Mensagem ?? religar.Motivo ?? MsgNaoDesligou;
        }
        if (causa is not null)
        {
            _log.Evento($"teste a seco: o módulo não religou ({causa})");
            return Terminar(geracao, $"{frase} {MsgNaoReligou(causa)}", false);
        }
        return Terminar(geracao, frase, true);
    }

    /// <summary>
    /// O volume de mídia do celular no máximo (o valor da conta do nível do Bluetooth): põe e confere. O motivo, ou null.
    /// </summary>
    private async Task<string?> VolumeNoMaximoAsync()
    {
        if (_volume is not { } v || v.Volume >= v.Maximo)
            return null;
        bool chegou;
        try
        {
            chegou = await v.DefinirAsync(v.Maximo);
        }
        catch (Exception e)
        {
            _log.Evento($"teste a seco: falha ao pôr o volume no máximo ({e.GetType().Name})");
            chegou = false;
        }
        return chegou && v.Volume >= v.Maximo ? null : MsgVolume;
    }

    /// <summary>
    /// O Cancelar, a queda ou o fechar do app no meio: o trecho para. No Cancelar, o master volta à cópia (o módulo fica
    /// desligado); na queda, a marca fica para a próxima conexão; no fechar do app, fica "pela metade" já.
    /// </summary>
    private async Task<ResultadoTesteSeco> PararNoMeioAsync(long geracao)
    {
        string fim;
        bool copiaIniciada;
        lock (_trava)
        {
            fim = _fim ?? (SessaoAberta ? MsgCancelado : MsgCaiu);
            copiaIniciada = _copiaIniciada;
            _escutando = false;
        }
        await PararReprodutorAsync();
        if (copiaIniciada)
        {
            if (fim == MsgCaiu || !SessaoAberta)
            {
                _copia.Interromper();
                fim = MsgCaiu;
            }
            else if (fim == MsgFechouApp)
            {
                _copia.MarcarPelaMetade();
            }
            else
            {
                var volta = await VoltarAsync();
                if (!volta.Ok)
                    fim = $"{fim} {Calibracao.MsgNaoVoltou(volta.Motivo ?? "falhou")}";
            }
        }
        lock (_trava)
            _copiaIniciada = false;
        _log.Evento($"teste a seco: {fim}");
        return Fechar(geracao, fim);
    }

    /// <summary>Uma falha: o trecho para, o master volta à cópia e o módulo fica desligado. O resultado mostra o motivo.</summary>
    private async Task<ResultadoTesteSeco> FalharAsync(long geracao, string motivo)
    {
        // Uma falha que vem do Cancelar, da queda ou do fechar do app (a escrita recusada, por exemplo) é a parada no meio.
        bool parado;
        lock (_trava)
            parado = _cancelar?.IsCancellationRequested == true;
        if (parado)
            return await PararNoMeioAsync(geracao);
        bool copiaIniciada;
        lock (_trava)
        {
            copiaIniciada = _copiaIniciada;
            _escutando = false;
        }
        await PararReprodutorAsync();
        var frase = motivo;
        if (copiaIniciada)
        {
            if (SessaoAberta)
            {
                var volta = await VoltarAsync();
                if (!volta.Ok)
                    frase = $"{frase} {Calibracao.MsgNaoVoltou(volta.Motivo ?? "falhou")}";
            }
            else
            {
                _copia.Interromper();
            }
        }
        lock (_trava)
            _copiaIniciada = false;
        _log.Evento($"teste a seco: falhou ({motivo}); o módulo fica como está");
        return Terminar(geracao, frase, false);
    }

    /// <summary>O resultado (validado ou a falha) fica na tela até o Concluir.</summary>
    private ResultadoTesteSeco Terminar(long geracao, string frase, bool ok)
    {
        lock (_trava)
        {
            if (_geracao == geracao)
            {
                _etapa = EtapaTesteSeco.Resultado;
                _ok = ok;
                _mensagem = frase;
            }
        }
        Notificar();
        return ok ? ResultadoTesteSeco.Feito(frase) : ResultadoTesteSeco.Falha(frase);
    }

    /// <summary>Fecha o teste (da mesma geração) com a frase.</summary>
    private ResultadoTesteSeco Fechar(long geracao, string frase)
    {
        lock (_trava)
        {
            if (_geracao == geracao)
            {
                _geracao++;
                _etapa = EtapaTesteSeco.Fechado;
                _mensagem = frase;
            }
        }
        Notificar();
        return ResultadoTesteSeco.Falha(frase);
    }

    // ------------------------------------------------------------------ apoio

    /// <summary>Escreve o master pelo escritor, com a marca da cópia (origem App). O motivo da falha, ou null.</summary>
    private async Task<string?> EscreverMasterAsync(decimal valor)
    {
        ResultadoEscrita r;
        using (Origem.Como(OrigemComando.App))
            r = await _escritor.EscreverAsync(Master, null, valor, espelhar: false, daCopia: true);
        if (!r.Ok)
            return r.Motivo ?? Sessao.MotivoFalhaEscrita;
        if (r.Diferente)
            return $"o aparelho ficou em {r.Valor?.Exibicao ?? "outro valor"}.";
        return null;
    }

    /// <summary>Volta o master à cópia (o desfazer). Sem a sessão, a marca fica para a próxima conexão.</summary>
    private async Task<ResultadoCopia> VoltarAsync()
    {
        try
        {
            using (Origem.Como(OrigemComando.App))
                return await _copia.DesfazerAsync();
        }
        catch (Exception e)
        {
            _log.Evento($"teste a seco: falha ao voltar ({e.GetType().Name})");
            return ResultadoCopia.Falha($"Erro inesperado ({e.GetType().Name}).");
        }
    }

    private async Task PararReprodutorAsync()
    {
        try
        {
            await _reprodutor.PararAsync();
        }
        catch (Exception e)
        {
            _log.Evento($"teste a seco: falha ao parar o trecho ({e.GetType().Name})");
        }
    }

    private string NomeSaida(int canal) => NomeSaida(_setups.SaidasEmUso, canal);

    /// <summary>O nome da saída do canal ("Sub"), ou "CH3".</summary>
    internal static string NomeSaida(IReadOnlyList<SaidaApp> saidas, int canal)
    {
        var nomes = Ligacoes.NomesNaTela(saidas);
        for (var i = 0; i < saidas.Count; i++)
        {
            if (saidas[i].Canais.Contains(canal))
                return nomes[i];
        }
        return $"CH{canal + 1}";
    }

    /// <summary>O 150 chegou: durante um trecho, o primeiro canal com clip fica marcado.</summary>
    private void AoClip(IReadOnlyList<int> canais)
    {
        lock (_trava)
        {
            if (_escutando)
                _clipCanais.UnionWith(canais);
        }
    }

    /// <summary>A conexão caiu: com o teste andando, ele para e a cópia fica para a próxima conexão; no aviso, fecha.</summary>
    private void AoMudarSessao()
    {
        if (SessaoAberta)
            return;
        bool andando;
        lock (_trava)
        {
            andando = _execucao is { IsCompleted: false };
            if (andando)
            {
                _fim = MsgCaiu;
                _cancelar?.Cancel();
            }
            else if (_etapa == EtapaTesteSeco.Aviso)
            {
                _geracao++;
                _etapa = EtapaTesteSeco.Fechado;
                _mensagem = MsgSemConexao;
            }
            else
            {
                return;
            }
        }
        if (andando)
            _ = PararReprodutorAsync();
        Notificar();
    }

    private ResultadoTesteSeco Recusar(string motivo)
    {
        lock (_trava)
            _mensagem = motivo;
        Notificar();
        return ResultadoTesteSeco.Falha(motivo);
    }

    private void Atualizar(Action mudar)
    {
        lock (_trava)
            mudar();
        Notificar();
    }

    private static string Texto(decimal db) => db.ToString("0.0", Fio);

    private void Notificar()
    {
        try
        {
            Mudou?.Invoke();
        }
        catch (Exception e)
        {
            _log.Evento($"falha no aviso do teste a seco ({e.GetType().Name})");
        }
    }

    public void Dispose()
    {
        _sessao.Mudou -= AoMudarSessao;
        _monitor.Clip -= AoClip;
        lock (_trava)
            _cancelar?.Cancel();
    }
}
