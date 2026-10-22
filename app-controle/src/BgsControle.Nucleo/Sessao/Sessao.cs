using System.Text;
using System.Text.RegularExpressions;
using BgsControle.Nucleo.Enlace;
using BgsControle.Nucleo.Protocolo;
using BgsControle.Nucleo.Registro;
using BgsControle.Nucleo.Seguranca;

namespace BgsControle.Nucleo;

public enum EstadoSessao
{
    Desconectado,
    Procurando,
    Conectando,
    Autenticando,
    PedindoSenha,
    Conectado,
}

/// <summary>Prazos da sessão. Os testes usam prazos curtos.</summary>
public sealed record TemposSessao(
    TimeSpan Scan,
    TimeSpan TentativaConexao,
    TimeSpan PausaEntreTentativas,
    TimeSpan RespostaSenha,
    TimeSpan PrazoSenha)
{
    public static TemposSessao Padrao { get; } = new(
        Scan: TimeSpan.FromSeconds(10),
        TentativaConexao: TimeSpan.FromSeconds(15),
        PausaEntreTentativas: TimeSpan.FromSeconds(1),
        RespostaSenha: TimeSpan.FromSeconds(5),
        PrazoSenha: TimeSpan.FromSeconds(60));
}

public sealed record ResultadoEnvio(bool Enviado, string? Motivo);

/// <summary>
/// A conexão com o processador: procura, conecta, autentica e guarda o estado.
/// <see cref="EnviarAsync"/> é a única porta de escrita para o BLE, e passa pela trava.
/// </summary>
public sealed class Sessao
{
    public const int TentativasDeConexao = 3;
    public const int TentativasDeSenha = 3;

    public const string MsgSenhaInvalida = "A senha tem 4 letras ou números.";
    public const string MsgNaoAchou = "Não achei o processador. O app da Taramps está fechado?";
    public const string MsgNaoConectou = "Não consegui conectar ao processador. Confira se ele está ligado e se o app da Taramps está fechado.";
    public const string MsgSemResposta = "O processador não respondeu à senha.";
    public const string MsgSenhaErrada = "Senha errada.";
    public const string MsgTresRecusas = "Senha recusada 3 vezes. Desconectei.";
    public const string MsgPrazoSenha = "O prazo para digitar a senha acabou. Toque em Conectar para tentar de novo.";
    public const string MsgCaiu = "A conexão caiu.";
    public const string MsgPedeSenha = "Digite a senha do processador.";
    public const string MsgSenhaApagada = "Senha apagada do celular.";
    public const string MsgBluetoothDesligado = "O Bluetooth está desligado. Ligue o Bluetooth e toque em Conectar.";
    public const string MsgSemPermissao = "Falta a permissão \"Dispositivos por perto\". Libere em Configurações > Apps > BGS Controle > Permissões e toque em Conectar.";
    public const string MsgSemBle = "Este celular não tem Bluetooth Low Energy.";
    public const string MsgFalhaEnvioSenha = "Não consegui enviar a senha.";
    public const string MsgErroInesperado = "Erro inesperado no Bluetooth. Toque em Conectar para tentar de novo.";

    public const string MotivoForaDoCatalogo = "leitura fora do catálogo";
    public const string MotivoSemConexao = "sem conexão autenticada";
    public const string MotivoSoLeitura = "modo só leitura";
    public const string MotivoChaveDesligada = "chave desligada";
    public const string MotivoNaoLiberado = "comando não liberado nesta versão do app";
    public const string MotivoEscritaForaDoCatalogo = "escrita fora do catálogo";
    public const string MotivoMemoriaForaDoFormato = "comando de memória fora do formato";

    // Os códigos das memórias (240, 241 e os nomes 231 a 233): só a forma exata passa
    // (Catalogo.ComandoDeMemoria); o resto é recusado com este motivo.
    private static readonly HashSet<string> CodigosDeMemoria =
        [Catalogo.CodigoSalvarMemoria, Catalogo.CodigoCarregarMemoria, .. Enumerable.Range(0, Catalogo.Memorias).Select(Catalogo.CodigoNomeMemoria)];

    // Comandos conhecidos fora dos ajustes de áudio e das memórias (MAPA §4 e §5): gerador,
    // 251, 252, 002, 004, 242 e 001. São da história 7; aqui só explicam a recusa.
    private static readonly HashSet<string> ComandosNaoLiberados =
        [.. Catalogo.Extras.Select(p => p.Codigo).Where(c => !CodigosDeMemoria.Contains(c)), "001", "002", "004", "242"];

    // A trava deixa passar as leituras do catálogo (Catalogo.EhLeitura), a senha, as escritas
    // de áudio do Marco 1 (Catalogo.EscritaDeAudio) com a chave "Ajustes de áudio" ligada e os
    // comandos de memória (Catalogo.ComandoDeMemoria) com a chave "Salvar memória" ou
    // "Carregar memória" ligada. Estes formatos só servem para explicar a recusa de uma leitura
    // desconhecida.
    private static readonly Regex FormaDeLeitura = new(@"^(#\d{3}#(\d{1,2}#)?|\d{3}#\d{1,2})$", RegexOptions.CultureInvariant);
    private static readonly Regex QuadroSenha = new(@"^\*000\*[0-9A-Z]{4}\*$", RegexOptions.CultureInvariant);

    private readonly ITransporte _transporte;
    private readonly ICofreSenha _cofre;
    private readonly LogBle _log;
    private readonly TemposSessao _tempos;
    private readonly IChaves? _chaves;

    private readonly object _trava = new();
    private readonly MontadorQuadros _montador = new();
    private readonly SemaphoreSlim _gatt = new(1, 1);

    private int _geracao;
    private CancellationTokenSource _operacao = new();
    private CancellationTokenSource? _prazo;
    private TaskCompletionSource<ResultadoAutenticacao?>? _resposta;
    private string? _senhaEnviada;
    private int _tentativasSenha;

    /// <param name="chaves">As chaves de acesso. Sem elas, nenhuma escrita passa (toda chave conta como desligada).</param>
    public Sessao(ITransporte transporte, ICofreSenha cofre, LogBle log, TemposSessao? tempos = null, IChaves? chaves = null)
    {
        _transporte = transporte;
        _cofre = cofre;
        _log = log;
        _tempos = tempos ?? TemposSessao.Padrao;
        _chaves = chaves;
        _transporte.Notificacao += AoReceber;
        _transporte.Caiu += AoCair;
    }

    public EstadoSessao Estado { get; private set; }

    /// <summary>
    /// A leitura completa (<see cref="Leitura.LeitorCompleto"/>) e cada escrita com a sua
    /// confirmação (<see cref="Escrita.Escritor"/>) não se misturam: uma de cada vez, para a
    /// resposta de uma nunca ser tomada pela outra.
    /// </summary>
    internal SemaphoreSlim ExclusaoLeituraEscrita { get; } = new(1, 1);

    /// <summary>A conexão BLE está aberta (de Autenticando em diante).</summary>
    public bool SegurandoConexao { get; private set; }

    public string? Firmware { get; private set; }
    public bool SoLeitura { get; private set; }
    public string? Aviso { get; private set; }
    public string? Mensagem { get; private set; }

    /// <summary>O estado em uma linha, para a tela.</summary>
    public string Resumo
    {
        get
        {
            lock (_trava)
            {
                return Estado switch
                {
                    EstadoSessao.Procurando => "Procurando o processador…",
                    EstadoSessao.Conectando => "Conectando…",
                    EstadoSessao.Autenticando => "Conferindo a senha…",
                    EstadoSessao.PedindoSenha => "Digite a senha",
                    EstadoSessao.Conectado when SoLeitura => $"{Firmware} · modo só leitura",
                    EstadoSessao.Conectado => $"Conectado · firmware {Firmware}",
                    _ => "Desconectado",
                };
            }
        }
    }

    /// <summary>Algo na sessão mudou. Pode chegar em qualquer thread.</summary>
    public event Action? Mudou;

    /// <summary>
    /// Um quadro recebido com a sessão conectada (fora da resposta da senha), já registrado
    /// no log. Quem consulta (o leitor) casa com o pedido; o resto é órfão para ele.
    /// Chega na thread das notificações, uma por vez.
    /// </summary>
    public event Action<string>? Recebeu;

    // ------------------------------------------------------------------ comandos da tela

    /// <summary>Toque em Conectar. Sem senha guardada, pede a senha antes de conectar.</summary>
    public async Task ConectarAsync()
    {
        int geracao;
        CancellationToken ct;
        lock (_trava)
        {
            if (Estado != EstadoSessao.Desconectado)
                return;
            LimparConexao();
            Mensagem = null;
            _tentativasSenha = 0;
            Estado = EstadoSessao.Procurando;
            (geracao, ct) = NovaOperacao();
        }
        Notificar();

        var senha = await LerSenhaGuardadaAsync();
        if (senha is null)
        {
            lock (_trava)
            {
                if (geracao != _geracao)
                    return;
                Estado = EstadoSessao.PedindoSenha;
                Mensagem = MsgPedeSenha;
            }
            Notificar();
            return;
        }

        await ConectarEAutenticarAsync(senha, geracao, ct);
    }

    /// <summary>A senha digitada na tela. Fora do formato, nada sai pelo BLE.</summary>
    public async Task InformarSenhaAsync(string? digitada)
    {
        var senha = Quadros.NormalizarSenha(digitada);
        int geracao;
        CancellationToken ct;
        bool segurando;
        CancellationTokenSource? prazo;
        lock (_trava)
        {
            if (Estado != EstadoSessao.PedindoSenha)
                return;
            if (senha is null)
            {
                Mensagem = MsgSenhaInvalida;
                prazo = null;
                segurando = false;
                geracao = 0;
                ct = default;
            }
            else
            {
                _log.ProtegerSegredo(senha);
                segurando = SegurandoConexao;
                prazo = _prazo;
                _prazo = null;
                if (segurando)
                {
                    geracao = _geracao;
                    ct = _operacao.Token;
                }
                else
                {
                    Estado = EstadoSessao.Procurando;
                    (geracao, ct) = NovaOperacao();
                }
            }
        }
        prazo?.Cancel();
        Notificar();
        if (senha is null)
            return;

        if (segurando)
            await AutenticarAsync(senha, geracao, ct);
        else
            await ConectarEAutenticarAsync(senha, geracao, ct);
    }

    /// <summary>Apaga a senha guardada. Se houver conexão, desconecta.</summary>
    public async Task EsquecerSenhaAsync()
    {
        await ApagarSenhaAsync();
        int geracao;
        bool encerrar;
        lock (_trava)
        {
            geracao = _geracao;
            encerrar = Estado is not (EstadoSessao.Desconectado or EstadoSessao.PedindoSenha) || SegurandoConexao;
            if (!encerrar)
                Mensagem = MsgSenhaApagada;
        }
        if (encerrar)
            await EncerrarAsync(geracao, MsgSenhaApagada);
        else
            Notificar();
        _log.Evento("senha apagada do celular");
    }

    public async Task DesconectarAsync()
    {
        int geracao;
        lock (_trava)
        {
            if (Estado == EstadoSessao.Desconectado)
                return;
            geracao = _geracao;
        }
        await EncerrarAsync(geracao, null);
    }

    // ------------------------------------------------------------------ porta de escrita

    /// <summary>
    /// A única porta para o BLE. Passa só o que a trava permite:
    /// <list type="bullet">
    /// <item>as leituras do catálogo (<c>#CCC#</c>, <c>#CCC#c#</c> e <c>020#b</c>, cada código no
    /// seu escopo) com a sessão conectada;</item>
    /// <item>o quadro da senha no estado Autenticando;</item>
    /// <item>uma escrita de ajuste de áudio do Marco 1, exatamente como o catálogo a codifica, com
    /// a sessão conectada, fora do modo só leitura e com a chave "Ajustes de áudio" ligada;</item>
    /// <item><c>*240*n*</c> e <c>*23N*NOME*</c> com a chave "Salvar memória", e <c>*241*n*</c>
    /// com a chave "Carregar memória", só na forma exata, conectada e fora do modo só leitura.</item>
    /// </list>
    /// Todo o resto (gerador, 252, 251, 002, 004, 242, 001, uma leitura fora do
    /// catálogo como <c>#004#</c>, qualquer texto desconhecido) é recusado antes do BLE, com o
    /// motivo no log.
    /// </summary>
    public async Task<ResultadoEnvio> EnviarAsync(string quadro)
    {
        string? motivo;
        CancellationToken ct;
        lock (_trava)
        {
            motivo = Trava(quadro);
            ct = _operacao.Token;
        }
        if (motivo is not null)
        {
            _log.Recusado(quadro, motivo);
            return new ResultadoEnvio(false, motivo);
        }

        try
        {
            await _gatt.WaitAsync(ct);
        }
        catch (OperationCanceledException)
        {
            return new ResultadoEnvio(false, "a conexão terminou");
        }

        try
        {
            lock (_trava)
                motivo = Trava(quadro); // o estado pode ter mudado na fila
            if (motivo is not null)
            {
                _log.Recusado(quadro, motivo);
                return new ResultadoEnvio(false, motivo);
            }
            _log.Enviado(quadro);
            await _transporte.EscreverAsync(Encoding.ASCII.GetBytes(quadro), ct);
            return new ResultadoEnvio(true, null);
        }
        catch (Exception e)
        {
            _log.Evento($"falha na escrita ({e.GetType().Name})");
            return new ResultadoEnvio(false, "falha na escrita");
        }
        finally
        {
            _gatt.Release();
        }
    }

    /// <summary>Null = pode passar; senão, o motivo da recusa. Chamar com a trava.</summary>
    private string? Trava(string quadro)
    {
        var conectado = Estado == EstadoSessao.Conectado && SegurandoConexao;
        if (Catalogo.EhLeitura(quadro))
            return conectado ? null : MotivoSemConexao;
        if (QuadroSenha.IsMatch(quadro))
            return Estado == EstadoSessao.Autenticando ? null : "a senha só sai na autenticação";
        if (FormaDeLeitura.IsMatch(quadro))
            return MotivoForaDoCatalogo;
        if (SoLeitura)
            return MotivoSoLeitura;

        Chave chave;
        if (Catalogo.ComandoDeMemoria(quadro) is { } memoria)
        {
            chave = memoria.Tipo == TipoComandoMemoria.Carregar ? Chave.CarregarMemoria : Chave.SalvarMemoria;
        }
        else if (Catalogo.EscritaDeAudio(quadro) is not null)
        {
            chave = Chave.AjustesAudio;
        }
        else
        {
            var codigo = Quadros.Analisar(quadro)?.Codigo;
            if (codigo is not null && CodigosDeMemoria.Contains(codigo))
                return MotivoMemoriaForaDoFormato;
            if (codigo is not null && ComandosNaoLiberados.Contains(codigo))
                return MotivoNaoLiberado;
            return MotivoEscritaForaDoCatalogo;
        }
        if (!conectado)
            return MotivoSemConexao;
        if (_chaves?.Ligada(chave) != true)
            return MotivoChaveDesligada;
        return null;
    }

    /// <summary>
    /// O que a trava diria agora para este quadro, sem enviar nada nem registrar: null = passaria.
    /// Quem prepara uma operação (a cópia antes, por exemplo) pergunta antes de começar.
    /// </summary>
    public string? Conferir(string quadro)
    {
        lock (_trava)
            return Trava(quadro);
    }

    // ------------------------------------------------------------------ conexão e senha

    private async Task ConectarEAutenticarAsync(string senha, int geracao, CancellationToken ct)
    {
        try
        {
            if (!Definir(geracao, EstadoSessao.Procurando))
                return;
            _log.Evento("procurando o processador");
            var dispositivo = await _transporte.ProcurarAsync(_tempos.Scan, ct);
            if (dispositivo is null)
            {
                await EncerrarAsync(geracao, MsgNaoAchou);
                return;
            }

            if (!Definir(geracao, EstadoSessao.Conectando))
                return;
            for (var tentativa = 1; ; tentativa++)
            {
                using var limite = CancellationTokenSource.CreateLinkedTokenSource(ct);
                limite.CancelAfter(_tempos.TentativaConexao);
                try
                {
                    _log.Evento($"conectando (tentativa {tentativa} de {TentativasDeConexao})");
                    await _transporte.ConectarAsync(dispositivo, limite.Token);
                    break;
                }
                catch (Exception e) when (e is FalhaConexaoException
                                          || (e is OperationCanceledException && !ct.IsCancellationRequested))
                {
                    var motivo = e is FalhaConexaoException ? e.Message : "tempo esgotado";
                    _log.Evento($"tentativa {tentativa} falhou: {motivo}");
                    if (tentativa == TentativasDeConexao)
                    {
                        await EncerrarAsync(geracao, MsgNaoConectou);
                        return;
                    }
                    await Task.Delay(_tempos.PausaEntreTentativas, ct);
                }
            }

            bool valida;
            lock (_trava)
            {
                valida = geracao == _geracao;
                if (valida)
                    SegurandoConexao = true;
            }
            if (!valida)
            {
                // Cancelado enquanto conectava: solta a conexão que acabou de abrir.
                await DesconectarTransporteAsync();
                return;
            }
            _log.Evento("conectado; notificação ligada");
            await AutenticarAsync(senha, geracao, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Desconectado pela tela ou pela queda: o estado já foi tratado.
        }
        catch (BluetoothIndisponivelException e)
        {
            await EncerrarAsync(geracao, e.Motivo switch
            {
                MotivoBluetooth.Desligado => MsgBluetoothDesligado,
                MotivoBluetooth.SemPermissao => MsgSemPermissao,
                _ => MsgSemBle,
            });
        }
        catch (Exception e)
        {
            _log.Evento($"erro inesperado ({e.GetType().Name})");
            await EncerrarAsync(geracao, MsgErroInesperado);
        }
    }

    private async Task AutenticarAsync(string senha, int geracao, CancellationToken ct)
    {
        var quadro = Quadros.MontarSenha(senha);
        TaskCompletionSource<ResultadoAutenticacao?> resposta;
        lock (_trava)
        {
            if (geracao != _geracao || !SegurandoConexao)
                return;
            Estado = EstadoSessao.Autenticando;
            _tentativasSenha++;
            // Nada que chegou antes da senha é resposta dela.
            foreach (var orfao in _montador.Esvaziar())
                _log.Recebido(orfao, "órfão");
            _senhaEnviada = senha;
            resposta = new TaskCompletionSource<ResultadoAutenticacao?>(TaskCreationOptions.RunContinuationsAsynchronously);
            _resposta = resposta;
        }
        Notificar();

        var envio = await EnviarAsync(quadro);
        if (!envio.Enviado)
        {
            await EncerrarAsync(geracao, MsgFalhaEnvioSenha);
            return;
        }

        ResultadoAutenticacao? resultado;
        using (var espera = CancellationTokenSource.CreateLinkedTokenSource(ct))
        {
            var prazo = Task.Delay(_tempos.RespostaSenha, espera.Token);
            var primeira = await Task.WhenAny(resposta.Task, prazo);
            espera.Cancel();
            if (primeira != resposta.Task)
            {
                if (ct.IsCancellationRequested)
                    return;
                await EncerrarAsync(geracao, MsgSemResposta);
                return;
            }
            resultado = await resposta.Task;
        }

        switch (resultado)
        {
            case ResultadoAutenticacao.Aceita aceita:
                await GuardarSenhaAsync(senha);
                lock (_trava)
                {
                    if (geracao != _geracao)
                        return;
                    Firmware = aceita.Firmware;
                    SoLeitura = aceita.SoLeitura;
                    Aviso = MontarAviso(aceita);
                    Mensagem = null;
                    Estado = EstadoSessao.Conectado;
                }
                _log.Evento(aceita.SoLeitura ? $"senha aceita; firmware {aceita.Firmware}; modo só leitura" : $"senha aceita; firmware {aceita.Firmware}");
                Notificar();
                break;

            case ResultadoAutenticacao.Recusada:
                await ApagarSenhaAsync();
                bool esgotou;
                lock (_trava)
                {
                    if (geracao != _geracao)
                        return;
                    esgotou = _tentativasSenha >= TentativasDeSenha;
                    if (!esgotou)
                    {
                        // O processador derruba a conexão uns 29 s depois de um ERRO (medido em
                        // 20/10). O app solta já; a próxima senha abre uma conexão nova, e as
                        // tentativas seguem contando até o próximo toque em Conectar.
                        SegurandoConexao = false;
                        LimparConexao();
                        Estado = EstadoSessao.PedindoSenha;
                        Mensagem = $"{MsgSenhaErrada} Digite de novo (tentativa {_tentativasSenha + 1} de {TentativasDeSenha}).";
                        IniciarPrazo(geracao);
                    }
                }
                _log.Evento("senha errada");
                if (esgotou)
                {
                    await EncerrarAsync(geracao, MsgTresRecusas);
                }
                else
                {
                    Notificar();
                    await DesconectarTransporteAsync();
                    _log.Evento("conexão solta; a próxima senha abre outra");
                }
                break;

            default:
                // null: a conexão caiu enquanto esperava; AoCair já tratou.
                break;
        }
    }

    private static string? MontarAviso(ResultadoAutenticacao.Aceita a)
    {
        if (!a.ProdutoConhecido)
            return $"Produto desconhecido (a resposta não começa com {Autenticacao.PrefixoProduto}). Modo só leitura: nenhum ajuste sai do celular.";
        if (a.Firmware != Autenticacao.FirmwareSuportado)
            return $"Firmware {a.Firmware}: este app só conhece o {Autenticacao.FirmwareSuportado}. Modo só leitura: nenhum ajuste sai do celular.";
        return null;
    }

    /// <summary>Chamar com a trava.</summary>
    private void IniciarPrazo(int geracao)
    {
        _prazo?.Cancel();
        var cts = new CancellationTokenSource();
        _prazo = cts;
        _ = VigiarPrazoAsync(geracao, cts.Token);
    }

    private async Task VigiarPrazoAsync(int geracao, CancellationToken ct)
    {
        try
        {
            await Task.Delay(_tempos.PrazoSenha, ct);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        bool vencer;
        lock (_trava)
            vencer = geracao == _geracao && Estado == EstadoSessao.PedindoSenha;
        if (vencer)
            await EncerrarAsync(geracao, MsgPrazoSenha);
    }

    // ------------------------------------------------------------------ eventos do transporte

    private void AoReceber(byte[] dados)
    {
        TaskCompletionSource<ResultadoAutenticacao?>? alvo = null;
        ResultadoAutenticacao? resolvido = null;
        List<string>? entregar = null;
        lock (_trava)
        {
            _montador.Alimentar(dados);
            var conectado = Estado == EstadoSessao.Conectado && SegurandoConexao;
            foreach (var texto in _montador.RetirarFechados())
            {
                if (resolvido is null && _resposta is not null)
                {
                    var r = AvaliarResposta(texto, out var nota);
                    if (r is ResultadoAutenticacao.Aceita or ResultadoAutenticacao.Recusada)
                    {
                        resolvido = r;
                        _log.Recebido(texto, "resposta da senha");
                        continue;
                    }
                    _log.Recebido(texto, nota);
                    continue;
                }
                if (conectado)
                {
                    _log.Recebido(texto);
                    (entregar ??= []).Add(texto);
                    continue;
                }
                _log.Recebido(texto, "órfão");
            }

            if (conectado && _resposta is null && Catalogo.RespostaInteira(_montador.Aberto))
            {
                // Uma resposta sem \r\n no fim, mas já inteira (020#3#-1.0, #080#+0.0#).
                var aberto = _montador.Aberto;
                _montador.DescartarAberto();
                _log.Recebido(aberto);
                (entregar ??= []).Add(aberto);
            }

            if (resolvido is null && _resposta is not null)
            {
                // A resposta sem \r\n no fim vale quando termina num separador.
                var aberto = _montador.Aberto;
                if (Quadros.TerminaEmSeparador(aberto))
                {
                    var r = AvaliarResposta(aberto, out _);
                    if (r is ResultadoAutenticacao.Aceita or ResultadoAutenticacao.Recusada)
                    {
                        resolvido = r;
                        _montador.DescartarAberto();
                        _log.Recebido(aberto, "resposta da senha");
                    }
                }
            }

            if (resolvido is not null)
            {
                alvo = _resposta;
                _resposta = null;
            }
        }
        alvo?.TrySetResult(resolvido);
        if (entregar is not null)
        {
            foreach (var texto in entregar)
                Recebeu?.Invoke(texto.TrimEnd('\r', '\n'));
        }
    }

    /// <summary>Lê um quadro como possível resposta da senha. Chamar com a trava.</summary>
    private ResultadoAutenticacao? AvaliarResposta(string texto, out string nota)
    {
        var q = Quadros.Analisar(texto);
        if (q is null || q.Codigo != Quadros.CodigoSenha)
        {
            nota = "órfão";
            return null;
        }
        if (texto.Contains('*') && q.Segmentos.Count == 1 && q.Segmentos[0] == _senhaEnviada)
        {
            nota = "eco do envio";
            return null;
        }
        var r = Autenticacao.Interpretar(q);
        nota = r is ResultadoAutenticacao.Incompleta ? "resposta incompleta" : "resposta da senha";
        return r;
    }

    private void AoCair()
    {
        int geracao;
        lock (_trava)
        {
            if (!SegurandoConexao)
                return;
            geracao = _geracao;
        }
        _log.Evento("a conexão caiu");
        _ = EncerrarAsync(geracao, MsgCaiu);
    }

    // ------------------------------------------------------------------ apoio

    /// <summary>Volta para Desconectado com a mensagem. Ignora se a geração já mudou.</summary>
    private async Task EncerrarAsync(int geracao, string? mensagem)
    {
        CancellationTokenSource operacao;
        CancellationTokenSource? prazo;
        TaskCompletionSource<ResultadoAutenticacao?>? resposta;
        bool segurava;
        lock (_trava)
        {
            if (geracao != _geracao)
                return;
            _geracao++;
            operacao = _operacao;
            _operacao = new CancellationTokenSource();
            prazo = _prazo;
            _prazo = null;
            resposta = _resposta;
            _resposta = null;
            segurava = SegurandoConexao;
            SegurandoConexao = false;
            LimparConexao();
            _tentativasSenha = 0;
            Estado = EstadoSessao.Desconectado;
            Mensagem = mensagem;
        }
        // Cancelar fora da trava: continuações podem rodar nesta thread.
        operacao.Cancel();
        prazo?.Cancel();
        resposta?.TrySetResult(null);
        Notificar();
        if (segurava)
            await DesconectarTransporteAsync(); // depois de uma queda, só libera o GATT
        _log.Evento(mensagem is null ? "desconectado" : $"desconectado: {mensagem}");
    }

    /// <summary>Chamar com a trava.</summary>
    private void LimparConexao()
    {
        Firmware = null;
        SoLeitura = false;
        Aviso = null;
        _senhaEnviada = null;
        _montador.Esvaziar();
    }

    /// <summary>Chamar com a trava.</summary>
    private (int, CancellationToken) NovaOperacao()
    {
        _geracao++;
        _operacao = new CancellationTokenSource();
        return (_geracao, _operacao.Token);
    }

    private bool Definir(int geracao, EstadoSessao estado)
    {
        lock (_trava)
        {
            if (geracao != _geracao)
                return false;
            Estado = estado;
        }
        Notificar();
        return true;
    }

    private async Task DesconectarTransporteAsync()
    {
        try
        {
            await _transporte.DesconectarAsync();
        }
        catch (Exception e)
        {
            _log.Evento($"falha ao desconectar ({e.GetType().Name})");
        }
    }

    private async Task<string?> LerSenhaGuardadaAsync()
    {
        string? guardada;
        try
        {
            guardada = await _cofre.LerAsync();
        }
        catch (Exception)
        {
            guardada = null;
            await ApagarSenhaAsync();
        }
        if (guardada is null)
            return null;
        var senha = Quadros.NormalizarSenha(guardada);
        if (senha is null)
        {
            await ApagarSenhaAsync();
            return null;
        }
        _log.ProtegerSegredo(guardada);
        _log.ProtegerSegredo(senha);
        return senha;
    }

    private async Task GuardarSenhaAsync(string senha)
    {
        try
        {
            await _cofre.GuardarAsync(senha);
        }
        catch (Exception e)
        {
            _log.Evento($"não consegui guardar a senha ({e.GetType().Name})");
        }
    }

    private async Task ApagarSenhaAsync()
    {
        try
        {
            await _cofre.ApagarAsync();
        }
        catch (Exception e)
        {
            _log.Evento($"não consegui apagar a senha ({e.GetType().Name})");
        }
    }

    private void Notificar() => Mudou?.Invoke();
}
