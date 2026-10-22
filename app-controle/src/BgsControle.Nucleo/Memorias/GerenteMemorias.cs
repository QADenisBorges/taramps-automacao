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
/// As 3 memórias do DSP (CAP-4): salvar, carregar e nomear, sempre pela porta da sessão
/// (<see cref="Sessao.EnviarAsync"/>) e com as chaves "Salvar memória" e "Carregar memória".
/// No fio a memória é <c>n</c> de 0 a 2; na tela, 1 a 3.
/// <list type="bullet">
/// <item><b>Salvar</b> (resultado "honesto", decisão do Denis em 21/10): guarda a cópia antes;
/// anota a etapa; envia o <c>*240*n*</c> e depois o nome (<c>*23N*NOME*</c>). Com a resposta
/// <c>#240#n#</c> (medida em 20/10, ~1,17 s), "Memória N salva"; sem ela, "Memória N enviada (o
/// aparelho não confirma o salvar)" (Suposição ACK-240-SALVA). Reler o nome confirma só o
/// nome. Se a conexão cair no meio, ao reconectar diz "A memória N pode não ter sido salva; carregue-a para conferir".</item>
/// <item><b>Carregar:</b> guarda a cópia antes; envia o <c>*241*n*</c>; faz uma leitura completa
/// nova; só no fim dela a memória fica "em uso". Se cair depois do 241, a próxima conexão mostra
/// "aplicado pela metade" (<see cref="CopiaAntes"/>); se cair antes, nada mudou.</item>
/// <item><b>Renomear:</b> envia só o nome (o aparelho não ecoa) e confere relendo.</item>
/// </list>
/// Uma operação de memória por vez; enquanto anda, e enquanto algo ficou pela metade, nenhuma
/// escrita de áudio nem cópia do link entra (o bloqueio da <see cref="CopiaAntes"/>).
/// </summary>
public sealed class GerenteMemorias
{
    public const string TipoCarregar = "carregar-memoria";
    public const string TipoSalvar = "salvar-memoria";

    /// <summary>A etapa anotada antes de o 240 sair: daqui em diante ele pode ter saído.</summary>
    public const string EtapaSalvar = "240";

    /// <summary>
    /// Quanto esperar a resposta ao 240 e ao 241. O 240 respondeu em ~1,17 s (medido em 20/10);
    /// 3 s dão folga. Suposição ACK-241: o 241 responde no mesmo prazo.
    /// </summary>
    public static readonly TimeSpan PrazoAckMemoria = TimeSpan.FromSeconds(3);

    public const string MsgNomeInvalido = "Use até 12 letras, números, espaço ou _";
    public const string MsgOcupado = "Uma operação de memória já está em andamento.";
    public const string MsgCaiuSalvar = "A conexão caiu no meio do salvar. Ao reconectar, o app diz o que pode ter ficado.";
    public const string MsgCaiuCarregar = "A conexão caiu no meio do carregar. Ao reconectar, o app mostra o que ficou.";
    public const string MsgCaiu = "A conexão caiu.";
    public const string MsgNomeNaoConfirmado = "O aparelho não confirmou o nome.";
    public const string MsgLeituraParou = "A leitura depois de carregar parou no meio.";
    public const string MsgNaoGuardouEtapa = "Não consegui guardar o andamento do salvar no celular. Nada foi enviado.";

    /// <summary>"Memória 2 salva": o aparelho respondeu ao 240.</summary>
    public static string MsgSalva(int n) => $"Memória {n + 1} salva";

    /// <summary>"Memória 2 enviada (o aparelho não confirma o salvar)": sem resposta ao 240.</summary>
    public static string MsgEnviada(int n) => $"Memória {n + 1} enviada (o aparelho não confirma o salvar)";

    /// <summary>A conexão caiu no meio do salvar (o 240 pode ter saído).</summary>
    public static string MsgPodeNaoSalva(int n) => $"A memória {n + 1} pode não ter sido salva; carregue-a para conferir";

    /// <summary>O 240 não saiu.</summary>
    public static string MsgNaoSalva(int n) => $"A memória {n + 1} não foi salva. O som continua no aparelho; toque em Salvar de novo.";

    public static string MsgEmUso(int n, string? nome) => $"Memória {n + 1}{(string.IsNullOrEmpty(nome) ? "" : $" {nome}")} em uso.";

    public static string MsgRenomeada(int n, string nome) => $"A memória {n + 1} agora se chama {nome}.";

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
        _copia.Registrar(TipoSalvar,
            op => Task.FromResult(ConferirSalvo(op)),
            async op => ParaCopia(await SalvarInternoAsync(MemoriaDe(op), op.Nome, refazendo: true)));
    }

    /// <summary>A memória em uso (0 a 2), ou null se o app não sabe (nenhuma carregada ou salva nesta sessão do app).</summary>
    public int? EmUso
    {
        get
        {
            lock (_trava)
                return _emUso;
        }
    }

    /// <summary>A última frase para a tela ("Memória 2 salva"), ou null.</summary>
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

    /// <summary>O nome lido da memória n (0 a 2), ou null antes da leitura.</summary>
    public string? Nome(int n) => _estado.Valor(Catalogo.CodigoNomeMemoria(n))?.Texto;

    // ------------------------------------------------------------------ operações

    /// <summary>Salva o estado atual na memória n (0 a 2) com o nome (convertido para maiúsculas).</summary>
    public Task<ResultadoMemoria> SalvarAsync(int n, string? nome) => SalvarInternoAsync(n, nome, refazendo: false);

    /// <summary>Carrega a memória n (0 a 2), sem confirmação, com a cópia antes e a leitura nova.</summary>
    public Task<ResultadoMemoria> CarregarAsync(int n) => CarregarInternoAsync(n, refazendo: false);

    /// <summary>Renomeia a memória n (0 a 2): envia só o nome e confere relendo.</summary>
    public async Task<ResultadoMemoria> RenomearAsync(int n, string? nome)
    {
        if (n is < 0 or >= Catalogo.Memorias)
            return ResultadoMemoria.Falha("Memória de 1 a 3.");
        if (Catalogo.NormalizarNomeMemoria(nome) is not { } normalizado)
            return ResultadoMemoria.Falha(MsgNomeInvalido);
        var quadro = Catalogo.NomearMemoria(n, normalizado);
        if (Recusa(quadro) is { } recusa)
            return recusa;
        if (!Ocupar())
            return ResultadoMemoria.Falha(MsgOcupado);
        try
        {
            string? relido;
            var porta = _sessao.ExclusaoLeituraEscrita;
            await porta.WaitAsync();
            try
            {
                var envio = await EnviarAsync(quadro);
                if (!envio.Enviado)
                    return Terminar(ResultadoMemoria.Falha(envio.Motivo ?? MsgCaiu));
                relido = await LerNomeAsync(n);
            }
            finally
            {
                porta.Release();
            }

            if (relido == normalizado)
                return Terminar(new ResultadoMemoria(true, null, MsgRenomeada(n, normalizado)));
            return Terminar(ResultadoMemoria.Falha(SessaoAberta ? MsgNomeNaoConfirmado : MsgCaiu));
        }
        finally
        {
            Desocupar();
        }
    }

    private async Task<ResultadoMemoria> SalvarInternoAsync(int n, string? nomeDigitado, bool refazendo)
    {
        if (n is < 0 or >= Catalogo.Memorias)
            return ResultadoMemoria.Falha("Memória de 1 a 3.");
        if (Catalogo.NormalizarNomeMemoria(nomeDigitado) is not { } nome)
            return ResultadoMemoria.Falha(MsgNomeInvalido);
        var quadroSalvar = Catalogo.SalvarMemoria(n);
        var quadroNome = Catalogo.NomearMemoria(n, nome);
        if ((Recusa(quadroSalvar) ?? Recusa(quadroNome)) is { } recusa)
            return recusa;
        if (!Ocupar())
            return ResultadoMemoria.Falha(MsgOcupado);
        try
        {
            var operacao = new OperacaoPendente(TipoSalvar, Alvo(n), $"Salvar na memória {n + 1} ({nome})", nome);
            var copia = await _copia.IniciarAsync(operacao, manterCopia: refazendo);
            if (!copia.Ok)
                return Terminar(ResultadoMemoria.Falha(copia.Motivo!));
            Avisar(null);

            // A etapa vai antes do 240: se a conexão cair daqui em diante, ele pode ter saído.
            if (!await _copia.AnotarEtapaAsync(EtapaSalvar))
            {
                await _copia.ConcluirAsync();
                return Terminar(ResultadoMemoria.Falha(MsgNaoGuardouEtapa));
            }

            string? ack;
            ResultadoEnvio? envioNome = null;
            string? relido = null;
            var porta = _sessao.ExclusaoLeituraEscrita;
            await porta.WaitAsync();
            try
            {
                // 1. O 240, e a resposta dele (~1,17 s medidos).
                (var envioSalvar, ack) = await EnviarEEsperarAsync(quadroSalvar, CasarAck(Catalogo.CodigoSalvarMemoria, n), PrazoAckMemoria);
                if (!envioSalvar.Enviado)
                {
                    // O 240 não saiu: a memória não foi salva, com certeza.
                    await _copia.ConcluirAsync();
                    return Terminar(ResultadoMemoria.Falha(envioSalvar.Motivo ?? MsgCaiu, MsgNaoSalva(n)));
                }

                // 2. O nome (o aparelho não ecoa; medido em 20/10) e a releitura, que confirma só o nome.
                if (SessaoAberta)
                {
                    envioNome = await EnviarAsync(quadroNome);
                    if (envioNome.Enviado)
                        relido = await LerNomeAsync(n);
                }
            }
            finally
            {
                porta.Release();
            }

            if (!SessaoAberta)
            {
                // A marca fica: ao reconectar, "pode não ter sido salva".
                _copia.Interromper();
                return Terminar(ResultadoMemoria.Falha(MsgCaiu, MsgCaiuSalvar));
            }
            await _copia.ConcluirAsync();

            var confirmado = ack is not null;
            if (confirmado)
                DefinirEmUso(n);
            var frase = confirmado ? MsgSalva(n) : MsgEnviada(n);
            if (envioNome is { Enviado: false })
                frase += $". O nome não foi enviado: {envioNome.Motivo}";
            else if (relido != nome)
                frase += $". {MsgNomeNaoConfirmado}";
            return Terminar(new ResultadoMemoria(true, null, frase));
        }
        finally
        {
            Desocupar();
        }
    }

    private async Task<ResultadoMemoria> CarregarInternoAsync(int n, bool refazendo)
    {
        if (n is < 0 or >= Catalogo.Memorias)
            return ResultadoMemoria.Falha("Memória de 1 a 3.");
        var quadro = Catalogo.CarregarMemoria(n);
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
            return Terminar(new ResultadoMemoria(true, null, MsgEmUso(n, Nome(n))));
        }
        finally
        {
            Desocupar();
        }
    }

    /// <summary>Depois de uma queda no meio do salvar: o app não tem como ler o conteúdo da memória sem carregá-la.</summary>
    private Retomada ConferirSalvo(OperacaoPendente op)
    {
        var n = MemoriaDe(op);
        if (n < 0)
            return Retomada.PelaMetade; // a marca não diz qual memória: o Denis decide
        Avisar(op.Etapa == EtapaSalvar ? MsgPodeNaoSalva(n) : MsgNaoSalva(n));
        return Retomada.Resolvida;
    }

    // ------------------------------------------------------------------ apoio

    private ResultadoMemoria? Recusa(string quadro)
    {
        if (_sessao.Conferir(quadro) is not { } motivo)
            return null;
        _log.Recusado(quadro, motivo);
        return ResultadoMemoria.Falha(motivo);
    }

    /// <summary>Relê o nome. Null = sem resposta. Chamar com a exclusão da sessão.</summary>
    private async Task<string?> LerNomeAsync(int n)
    {
        var codigo = Catalogo.CodigoNomeMemoria(n);
        var consulta = Catalogo.ConsultaDe($"#{codigo}#")!;
        var (envio, noFio) = await EnviarEEsperarAsync(consulta.Texto, CasarNome(n), _tempos.PrazoReleitura);
        if (!envio.Enviado || noFio is null)
            return null;
        if (consulta.Parametro.Decodificar(noFio).Valor is { } valor)
            _estado.Atualizar(new Endereco(codigo, null), valor);
        return noFio.Trim();
    }

    /// <summary>
    /// A resposta de leitura do nome (<c>23N#NOME</c>). Um texto que começa com <c>*</c> é a forma
    /// de escrita (um eco atrasado do <c>*23N*NOME*</c>), não a releitura: não vale.
    /// </summary>
    private static Func<string, string?> CasarNome(int n)
    {
        var consulta = Catalogo.ConsultaDe($"#{Catalogo.CodigoNomeMemoria(n)}#")!;
        return texto => texto.TrimStart().StartsWith('*') ? null : LeitorCompleto.Casar(consulta, texto);
    }

    /// <summary>O ack do 240 (medido: <c>#240#n#</c>) ou do 241 (Suposição ACK-241: <c>#241#n#</c>).</summary>
    private static Func<string, string?> CasarAck(string codigo, int n)
    {
        var alvo = n.ToString(CultureInfo.InvariantCulture);
        return texto => !texto.TrimStart().StartsWith('*') && Quadros.Analisar(texto) is { } q
                        && q.Codigo == codigo && q.Segmentos.Count >= 1 && q.Segmentos[0] == alvo ? alvo : null;
    }

    /// <summary>Envia pela sessão, no ritmo do escritor, sem esperar resposta.</summary>
    private async Task<ResultadoEnvio> EnviarAsync(string quadro) =>
        (await EnviarEEsperarAsync(quadro, null, TimeSpan.Zero)).Envio;

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
