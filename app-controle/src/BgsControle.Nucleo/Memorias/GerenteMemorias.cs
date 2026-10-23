using System.Globalization;
using BgsControle.Nucleo.Escrita;
using BgsControle.Nucleo.Leitura;
using BgsControle.Nucleo.Protocolo;
using BgsControle.Nucleo.Registro;

namespace BgsControle.Nucleo.Memorias;

/// <summary>Como terminou uma operação de memória. <see cref="Mensagem"/> é a frase para a tela.</summary>
public sealed record ResultadoMemoria(bool Ok, string? Motivo, string? Mensagem = null)
{
    public static ResultadoMemoria Falha(string motivo, string? mensagem = null) => new(false, motivo, mensagem ?? motivo);
}

/// <summary>
/// As 3 memórias do aparelho (CAP-4): este app só lê os nomes e carrega (<c>*241*n*</c>), sempre
/// pela porta da sessão (<see cref="Sessao.EnviarAsync"/>) e com a chave "Carregar memória do
/// aparelho". Salvar (<c>240</c>) e renomear (<c>23N</c>) ficam no app da Taramps (história 8c).
/// No fio a memória é <c>n</c> de 0 a 2; na tela, 1 a 3.
/// <para>
/// <b>Carregar:</b> guarda a cópia antes; envia o <c>*241*n*</c>; faz uma leitura completa nova;
/// só no fim dela a memória fica "em uso" e sai o evento <see cref="Carregou"/>. Se cair depois do
/// 241, a próxima conexão mostra "aplicado pela metade" (<see cref="CopiaAntes"/>); se cair antes,
/// nada mudou.
/// </para>
/// Uma operação de memória por vez; enquanto anda, e enquanto algo ficou pela metade, nenhuma
/// escrita de áudio nem cópia do link entra (o bloqueio da <see cref="CopiaAntes"/>).
/// </summary>
public sealed class GerenteMemorias
{
    public const string TipoCarregar = "carregar-memoria";

    /// <summary>
    /// Quanto esperar a resposta ao 241. O 240 respondeu em ~1,17 s (medido em 20/10); 3 s dão
    /// folga. Suposição ACK-241: o 241 responde no mesmo prazo.
    /// </summary>
    public static readonly TimeSpan PrazoAckMemoria = TimeSpan.FromSeconds(3);

    public const string MsgOcupado = "Uma operação de memória já está em andamento.";
    public const string MsgCaiuCarregar = "A conexão caiu no meio do carregar. Ao reconectar, o app mostra o que ficou.";
    public const string MsgCaiu = "A conexão caiu.";
    public const string MsgLeituraParou = "A leitura depois de carregar parou no meio.";

    public static string MsgEmUso(int n, string? nome) => $"Memória {n + 1}{(string.IsNullOrEmpty(nome) ? "" : $" {nome}")} em uso.";

    private readonly Sessao _sessao;
    private readonly EstadoAparelho _estado;
    private readonly LeituraAutomatica _leitura;
    private readonly CopiaAntes _copia;
    private readonly LogBle _log;
    private readonly TemposEscrita _tempos;
    private readonly TimeProvider _relogio;

    private readonly object _trava = new();
    private int _ocupado;
    private int? _emUso;
    private string? _aviso;
    private DateTimeOffset? _ultimoEnvio;
    private Aguardo? _aguardo;

    public GerenteMemorias(
        Sessao sessao,
        EstadoAparelho estado,
        LeituraAutomatica leitura,
        CopiaAntes copia,
        LogBle log,
        TemposEscrita? tempos = null,
        TimeProvider? relogio = null)
    {
        _sessao = sessao;
        _estado = estado;
        _leitura = leitura;
        _copia = copia;
        _log = log;
        _tempos = tempos ?? TemposEscrita.Padrao;
        _relogio = relogio ?? TimeProvider.System;
        _sessao.Recebeu += AoReceber;
        _sessao.Mudou += AoMudarSessao;

        _copia.Registrar(TipoCarregar,
            _ => Task.FromResult(Retomada.PelaMetade),
            async op => ParaCopia(await CarregarInternoAsync(MemoriaDe(op), refazendo: true)));
    }

    /// <summary>A memória do aparelho em uso (0 a 2), ou null se o app não sabe (nenhuma carregada nesta sessão do app).</summary>
    public int? EmUso
    {
        get
        {
            lock (_trava)
                return _emUso;
        }
    }

    /// <summary>A última frase para a tela ("Memória 3 FILME em uso."), ou null.</summary>
    public string? Aviso
    {
        get
        {
            lock (_trava)
                return _aviso;
        }
    }

    /// <summary>Uma operação de memória está andando.</summary>
    public bool Ocupado => Volatile.Read(ref _ocupado) == 1;

    /// <summary>A memória em uso, o aviso, o andamento ou um nome mudou. Pode chegar em qualquer thread.</summary>
    public event Action? Mudou;

    /// <summary>Um carregar terminou bem (também o Terminar): a memória n (0 a 2) está no aparelho, relida.</summary>
    public event Action<int>? Carregou;

    /// <summary>
    /// Quem pode impedir um carregar novo agora (o <c>GerenteSetups</c>, com o Setup em dúvida): o
    /// motivo, ou null. O Terminar de um carregar que caiu no meio não passa por aqui.
    /// </summary>
    internal Func<string?>? Bloqueio { get; set; }

    /// <summary>O nome lido da memória n (0 a 2), ou null antes da leitura.</summary>
    public string? Nome(int n) => _estado.Valor(Catalogo.CodigoNomeMemoria(n))?.Texto;

    // ------------------------------------------------------------------ operações

    /// <summary>Carrega a memória n (0 a 2), sem confirmação, com a cópia antes e a leitura nova.</summary>
    public Task<ResultadoMemoria> CarregarAsync(int n) => CarregarInternoAsync(n, refazendo: false);

    private async Task<ResultadoMemoria> CarregarInternoAsync(int n, bool refazendo)
    {
        if (n is < 0 or >= Catalogo.Memorias)
            return ResultadoMemoria.Falha("Memória de 1 a 3.");
        var quadro = Catalogo.CarregarMemoria(n);
        if (!refazendo && MotivoBloqueio() is { } bloqueio)
            return ResultadoMemoria.Falha(bloqueio);
        if (Recusa(quadro) is { } recusa)
            return recusa;
        if (!Ocupar())
            return ResultadoMemoria.Falha(MsgOcupado);
        try
        {
            var nome = Nome(n);
            var operacao = new OperacaoPendente(TipoCarregar, Alvo(n),
                $"Carregar a memória {n + 1}{(string.IsNullOrEmpty(nome) ? "" : $" ({nome})")}", nome);
            var copia = await _copia.IniciarAsync(operacao, manterCopia: refazendo);
            if (!copia.Ok)
                return Terminar(ResultadoMemoria.Falha(copia.Motivo!));
            Avisar(null);

            ResultadoEnvio envio;
            var porta = _sessao.ExclusaoLeituraEscrita;
            await porta.WaitAsync();
            try
            {
                (envio, _) = await EnviarEEsperarAsync(quadro, CasarAck(Catalogo.CodigoCarregarMemoria, n), PrazoAckMemoria);
            }
            finally
            {
                porta.Release();
            }
            if (!envio.Enviado)
            {
                if (refazendo)
                {
                    // O Terminar não saiu: continua como estava, pela metade.
                    if (SessaoAberta)
                        _copia.MarcarPelaMetade();
                    else
                        _copia.Interromper();
                    return Terminar(ResultadoMemoria.Falha(envio.Motivo ?? MsgCaiu));
                }
                // Recusado antes do BLE, ou a conexão caiu antes do 241: nada mudou no aparelho.
                await _copia.ConcluirAsync();
                return Terminar(ResultadoMemoria.Falha(envio.Motivo ?? MsgCaiu));
            }

            // O 241 saiu: daqui em diante o app não sabe o que está em uso até a leitura nova.
            DefinirEmUso(null);

            // A leitura completa nova (CAP-4): o espelho passa a ser a memória carregada.
            var retrato = await _leitura.LerNovaAsync();
            if (retrato is not { Completo: true })
            {
                if (!SessaoAberta)
                {
                    _copia.Interromper();
                    return Terminar(ResultadoMemoria.Falha(MsgCaiu, MsgCaiuCarregar));
                }
                _copia.MarcarPelaMetade();
                return Terminar(ResultadoMemoria.Falha(retrato?.Motivo ?? MsgLeituraParou));
            }

            await _copia.ConcluirAsync();
            DefinirEmUso(n);
            var r = Terminar(new ResultadoMemoria(true, null, MsgEmUso(n, Nome(n))));
            AvisarCarregou(n);
            return r;
        }
        finally
        {
            Desocupar();
        }
    }

    // ------------------------------------------------------------------ apoio

    private ResultadoMemoria? Recusa(string quadro)
    {
        if (_sessao.Conferir(quadro) is not { } motivo)
            return null;
        _log.Recusado(quadro, motivo);
        return ResultadoMemoria.Falha(motivo);
    }

    /// <summary>O ack do 241 (Suposição ACK-241: <c>#241#n#</c>, como o do 240, medido).</summary>
    private static Func<string, string?> CasarAck(string codigo, int n)
    {
        var alvo = n.ToString(CultureInfo.InvariantCulture);
        return texto => !texto.TrimStart().StartsWith('*') && Quadros.Analisar(texto) is { } q
                        && q.Codigo == codigo && q.Segmentos.Count >= 1 && q.Segmentos[0] == alvo ? alvo : null;
    }

    /// <summary>Envia pela sessão e espera a resposta que casa. Null = sem resposta no prazo ou caiu.</summary>
    private async Task<(ResultadoEnvio Envio, string? Resposta)> EnviarEEsperarAsync(string quadro, Func<string, string?>? casar, TimeSpan prazo)
    {
        var espera = TimeSpan.Zero;
        lock (_trava)
        {
            if (_ultimoEnvio is { } ultimo)
                espera = ultimo + _tempos.Intervalo - _relogio.GetUtcNow();
        }
        if (espera > TimeSpan.Zero)
            await Task.Delay(espera, _relogio);

        var aguardo = casar is null ? null : new Aguardo(casar);
        if (aguardo is not null)
        {
            lock (_trava)
                _aguardo = aguardo;
        }
        try
        {
            var envio = await _sessao.EnviarAsync(quadro);
            if (envio.Enviado)
            {
                lock (_trava)
                    _ultimoEnvio = _relogio.GetUtcNow();
            }
            if (!envio.Enviado || aguardo is null)
                return (envio, null);
            if (!SessaoAberta)
                aguardo.Resposta.TrySetResult(null);

            using var cancelar = new CancellationTokenSource();
            var limite = Task.Delay(prazo, _relogio, cancelar.Token);
            await Task.WhenAny(aguardo.Resposta.Task, limite);
            cancelar.Cancel();
            return (envio, aguardo.Resposta.Task.IsCompleted ? await aguardo.Resposta.Task : null);
        }
        finally
        {
            if (aguardo is not null)
            {
                lock (_trava)
                {
                    if (_aguardo == aguardo)
                        _aguardo = null;
                }
            }
        }
    }

    private void AoReceber(string texto)
    {
        Aguardo? alvo;
        lock (_trava)
            alvo = _aguardo;
        if (alvo?.Casar(texto) is { } resposta)
            alvo.Resposta.TrySetResult(resposta);
    }

    private void AoMudarSessao()
    {
        if (SessaoAberta)
            return;
        Aguardo? alvo;
        lock (_trava)
            alvo = _aguardo;
        alvo?.Resposta.TrySetResult(null);
    }

    private bool SessaoAberta => _sessao.Estado == EstadoSessao.Conectado && _sessao.SegurandoConexao;

    private string? MotivoBloqueio()
    {
        try
        {
            return Bloqueio?.Invoke();
        }
        catch (Exception e)
        {
            _log.Evento($"falha ao conferir o bloqueio do carregar ({e.GetType().Name})");
            return MsgOcupado;
        }
    }

    private void AvisarCarregou(int n)
    {
        try
        {
            Carregou?.Invoke(n);
        }
        catch (Exception e)
        {
            _log.Evento($"falha no aviso do carregar ({e.GetType().Name})");
        }
    }

    private bool Ocupar()
    {
        if (Interlocked.CompareExchange(ref _ocupado, 1, 0) != 0)
            return false;
        Notificar();
        return true;
    }

    private void Desocupar()
    {
        Volatile.Write(ref _ocupado, 0);
        Notificar();
    }

    /// <summary>Guarda a frase do resultado como aviso e devolve o resultado.</summary>
    private ResultadoMemoria Terminar(ResultadoMemoria r)
    {
        Avisar(r.Mensagem);
        if (!r.Ok)
            _log.Evento($"memória: {r.Mensagem ?? r.Motivo}");
        return r;
    }

    private void Avisar(string? aviso)
    {
        lock (_trava)
            _aviso = aviso;
        Notificar();
    }

    private void DefinirEmUso(int? n)
    {
        lock (_trava)
            _emUso = n;
        Notificar();
    }

    private static string Alvo(int n) => n.ToString(CultureInfo.InvariantCulture);

    /// <summary>A memória da marca (0 a 2), ou -1 se o alvo guardado não é uma memória.</summary>
    private static int MemoriaDe(OperacaoPendente op) =>
        int.TryParse(op.Alvo, NumberStyles.None, CultureInfo.InvariantCulture, out var n) && n is >= 0 and < Catalogo.Memorias ? n : -1;

    private static ResultadoCopia ParaCopia(ResultadoMemoria r) => r.Ok ? ResultadoCopia.Sucesso : ResultadoCopia.Falha(r.Mensagem ?? r.Motivo ?? "falhou");

    private void Notificar()
    {
        try
        {
            Mudou?.Invoke();
        }
        catch (Exception e)
        {
            _log.Evento($"falha no aviso das memórias ({e.GetType().Name})");
        }
    }

    private sealed class Aguardo(Func<string, string?> casar)
    {
        public Func<string, string?> Casar { get; } = casar;

        public TaskCompletionSource<string?> Resposta { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
