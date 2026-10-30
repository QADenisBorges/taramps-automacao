using System.Globalization;
using System.Net;
using System.Text.Json;
using BgsControle.Nucleo.Aparelho;
using BgsControle.Nucleo.Escrita;
using BgsControle.Nucleo.Gerador;
using BgsControle.Nucleo.Registro;
using BgsControle.Nucleo.Seguranca;
using BgsControle.Nucleo.Setups;
using BgsControle.Nucleo.Uso;

namespace BgsControle.Nucleo.Porta;

/// <summary>Um evento para a IA consultar (<c>GET /eventos?desde=N</c>): pausou, retomou, parada ou um comando da tela enviado.</summary>
public sealed record EventoPorta(long Numero, DateTimeOffset Hora, string Tipo, EntradaHistorico? Comando);

/// <summary>
/// A porta de comandos da IA (CAP-12, <c>porta-de-comandos.md</c>): as cinco travas num lugar só.
/// <list type="number">
/// <item><b>Desligada por padrão:</b> escuta (todas as interfaces, na porta do construtor, 8787 no
/// app) só com a chave "Controle pela rede" ligada (grupo APARELHO das Configurações). Ao desligar
/// a chave, a porta fecha, a IA para e o cliente é apagado (revogar).</item>
/// <item><b>Pareamento único</b> (<see cref="Porta.Pareamento"/>): todo caminho, menos
/// <c>/parear</c> e <c>/ajuda</c>, exige a chave do cliente (401).</item>
/// <item><b>Só a rede local</b> (<see cref="IRedeLocal"/>): fora dela, 403, sem ler o corpo e
/// sem histórico (só o <see cref="LogBle"/>).</item>
/// <item><b>Tudo registrado:</b> cada comando roda em <c>Origem.Como(OrigemComando.Ia)</c> e passa
/// pela <see cref="Sessao"/>, que registra no histórico (lido pela porta, fora do app). As recusas
/// da própria porta a um cliente pareado (folha recusada ou sem resposta, firmware, pedido
/// esperando) também entram. Desligar a chave revoga o cliente na hora.</item>
/// <item><b>Risco pede confirmação</b> (<see cref="PedidosConfirmacao"/>): as operações com
/// <see cref="ConfirmacaoIa.Sempre"/>, na folha de risco do app, que não menciona a IA. Antes da
/// folha, o <see cref="Sessao.Conferir"/> do primeiro quadro recusa na hora.</item>
/// </list>
/// <b>Sequência pela porta pede o toque</b> (<c>chaves-de-acesso.md</c>, decisão D4 do Denis de 29/10/2026): a
/// cada <see cref="AjustesSemToque"/> <c>/ajuste</c> da IA sem um toque do usuário no meio (uma escrita
/// da tela, uma confirmação na folha ou o revogar), o próximo espera "Continuar" na folha; confirmar
/// zera a conta e executa; recusar ou deixar vencer o prazo recusa só aquele pedido. A conta zera
/// também ao desconectar e ao revogar o cliente.
/// Com firmware diferente de R003, só os GET respondem. Com o Setup em dúvida (ou trocando, ou
/// ainda sem conferir nesta conexão), todo POST de um cliente pareado é recusado com 409 "Setup em
/// dúvida; escolha na tela." (CAP-16). A IA não salva: a porta não tem comando de salvar. Um
/// cliente que passa 30 s sem pedidos com a IA ativa termina a tarefa ("A IA perdeu a conexão.").
/// </summary>
public sealed class PortaComandos : IDisposable
{
    public const int PortaPadrao = 8787;
    public const int MaximoEventos = 200;
    public const int MaximoHistorico = 200;

    public const string MsgForaDaRede = "Só a rede local da casa.";
    public const string MsgNaoPareado = "Cliente não pareado. Peça o código no celular: Configurações, chave Controle pela rede.";
    public const string MsgFirmware = "Firmware diferente do R003: a porta só responde às leituras.";
    public const string MsgPerdeuConexao = "A IA perdeu a conexão.";
    public const string MsgCaminho = "Caminho desconhecido. Veja GET /ajuda.";
    public const string MsgChaveDesligada = "A chave Controle pela rede está desligada.";
    public const string MsgErroInesperado = "Erro inesperado no app.";

    /// <summary>Quantos <c>/ajuste</c> seguidos da IA passam sem o toque; o próximo espera Continuar (o 3 é provisório, do Denis).</summary>
    public const int AjustesSemToque = 3;

    /// <summary>O título da folha do Continuar (a folha de risco do app, sem mencionar a IA).</summary>
    public const string TituloContinuar = "Seguir mexendo no som?";

    /// <summary>O risco da folha do Continuar.</summary>
    public static string RiscoContinuar => $"Já foram {AjustesSemToque} ajustes seguidos pela rede sem um toque seu. Ajuste por ajuste, o som inteiro pode mudar.";

    public static string MsgNaoAbriu(int porta) => $"Não consegui abrir a porta {porta}.";

    /// <summary>Sem nenhum pedido do cliente por este tempo, com a IA ativa, a tarefa termina.</summary>
    public static TimeSpan PrazoCliente { get; } = TimeSpan.FromSeconds(30);

    private static readonly CultureInfo Fio = CultureInfo.InvariantCulture;
    private static readonly CultureInfo Tela = CultureInfo.GetCultureInfo("pt-BR");

    private readonly Sessao _sessao;
    private readonly IChaves _chaves;
    private readonly UsoCompartilhado _uso;
    private readonly Historico _historico;
    private readonly IRedeLocal _rede;
    private readonly EstadoAparelho _estado;
    private readonly Escritor _escritor;
    private readonly GerenteSetups _setups;
    private readonly GeradorSinal _gerador;
    private readonly ComandosAparelho _aparelho;
    private readonly MonitorAparelho _monitor;
    private readonly LogBle _log;
    private readonly TimeProvider _relogio;
    private readonly int _porta;
    private readonly ServidorHttp _servidor;

    private readonly object _trava = new();
    private readonly SemaphoreSlim _abrir = new(1, 1);
    private readonly LinkedList<EventoPorta> _eventos = new();
    private long _ultimoEvento;
    private bool _ligadaVista;
    private bool _paradaVista;
    private string? _erro;
    private string? _avisoConexao;
    private int _emAndamento;
    private DateTimeOffset? _ultimoPedido;
    private ITimer? _vigia;
    private bool _descartada;
    private int _ajustesSeguidos;
    private long _geracaoSequencia;

    public PortaComandos(
        Sessao sessao,
        IChaves chaves,
        UsoCompartilhado uso,
        Historico historico,
        Pareamento pareamento,
        PedidosConfirmacao confirmacoes,
        IRedeLocal rede,
        EstadoAparelho estado,
        Escritor escritor,
        GerenteSetups setups,
        GeradorSinal gerador,
        ComandosAparelho aparelho,
        MonitorAparelho monitor,
        LogBle log,
        TimeProvider? relogio = null,
        int porta = PortaPadrao)
    {
        _sessao = sessao;
        _chaves = chaves;
        _uso = uso;
        _historico = historico;
        Pareamento = pareamento;
        Confirmacoes = confirmacoes;
        _rede = rede;
        _estado = estado;
        _escritor = escritor;
        _setups = setups;
        _gerador = gerador;
        _aparelho = aparelho;
        _monitor = monitor;
        _log = log;
        _relogio = relogio ?? TimeProvider.System;
        _porta = porta;
        _servidor = new ServidorHttp(porta, AtenderAsync, Triagem, log);
        _paradaVista = uso.Parada;

        _chaves.Mudou += AoMudarChave;
        _uso.Mudou += AoMudarUso;
        _uso.Pausou += AoPausar;
        _uso.Retomou += AoRetomar;
        _historico.Registrou += AoRegistrar;
        Pareamento.Mudou += AoMudarPareamento;
        _sessao.Mudou += AoMudarSessao;
        // A chave pode já estar carregada (sem o aviso de agora): confere uma vez.
        Inicio = ServidorHttp.SemFluxo(AtualizarSeguroAsync);
    }

    /// <summary>A primeira conferência da chave (abrir ou não) terminou.</summary>
    public Task Inicio { get; }

    public Pareamento Pareamento { get; }

    public PedidosConfirmacao Confirmacoes { get; }

    /// <summary>A porta abriu, fechou, falhou, ou o aviso mudou. Pode chegar em qualquer thread.</summary>
    public event Action? Mudou;

    /// <summary>A porta está escutando.</summary>
    public bool Aberta => _servidor.Aberto;

    /// <summary>Quantos <c>/ajuste</c> da IA passaram desde o último toque do usuário (a conta do Continuar).</summary>
    public int AjustesSeguidos
    {
        get
        {
            lock (_trava)
                return _ajustesSeguidos;
        }
    }

    /// <summary>"Não consegui abrir a porta 8787.", ou null.</summary>
    public string? Erro
    {
        get
        {
            lock (_trava)
                return _erro;
        }
    }

    /// <summary>"A IA perdeu a conexão.", ou null.</summary>
    public string? AvisoConexao
    {
        get
        {
            lock (_trava)
                return _avisoConexao;
        }
    }

    /// <summary>O número da porta (o que escuta agora, ou o do construtor).</summary>
    public int NumeroPorta => _servidor.PortaLocal ?? _porta;

    /// <summary>"192.168.0.5:8787" para a tela, ou null sem rede local.</summary>
    public string? Endereco => _rede.Endereco is { } ip ? $"{ip}:{NumeroPorta.ToString(Fio)}" : null;

    // ------------------------------------------------------------------ abrir e fechar (trava 1)

    /// <summary>
    /// Abre ou fecha conforme a chave "Controle pela rede". Desligar a chave (só o usuário, na tela) é o que
    /// revoga: a porta fecha, o pedido esperando é recusado, a IA para (a parada interna) e o cliente sai.
    /// </summary>
    public async Task AtualizarAsync()
    {
        await _chaves.Carregada;
        await Pareamento.Carregado;
        await _abrir.WaitAsync();
        try
        {
            if (_descartada)
                return;
            if (_chaves.Ligada(Chave.ControlePelaRede))
            {
                lock (_trava)
                    _ligadaVista = true;
                if (_servidor.Aberto)
                    return;
                Pareamento.NovoCodigo();
                var abriu = _servidor.Abrir();
                lock (_trava)
                    _erro = abriu ? null : MsgNaoAbriu(_porta);
                _log.Evento(abriu ? $"porta da IA aberta ({NumeroPorta.ToString(Fio)})" : $"porta da IA: {MsgNaoAbriu(_porta)}");
            }
            else
            {
                bool estava;
                lock (_trava)
                {
                    estava = _ligadaVista;
                    _ligadaVista = false;
                    _erro = null;
                    _avisoConexao = null;
                }
                var aberta = _servidor.Aberto;
                _servidor.Fechar();
                if (aberta)
                    _log.Evento("porta da IA fechada");
                if (estava)
                {
                    Confirmacoes.Cancelar();
                    _uso.PararIa();
                    await Pareamento.RevogarAsync();
                }
                else if (Pareamento.Pareado)
                {
                    // Chave desligada com um cliente guardado (ao abrir o app, ou uma revogação que
                    // não chegou ao arquivo): ele não volta quando a chave ligar.
                    await Pareamento.RevogarAsync();
                }
            }
        }
        finally
        {
            _abrir.Release();
            Avisar();
        }
    }

    /// <summary>
    /// Revogar: a parada interna da IA e o cliente apagado (nasce um código novo). Não há botão na tela: o
    /// usuário revoga desligando a chave "Controle pela rede" (<see cref="AtualizarAsync"/>).
    /// </summary>
    public Task RevogarAsync()
    {
        Confirmacoes.Cancelar();
        _uso.PararIa();
        return Pareamento.RevogarAsync();
    }

    private async Task AtualizarSeguroAsync()
    {
        try
        {
            await AtualizarAsync();
        }
        catch (Exception e)
        {
            _log.Evento($"porta da IA: falha ao abrir ou fechar ({e.GetType().Name})");
        }
    }

    private void AoMudarChave(Chave chave)
    {
        if (chave != Chave.ControlePelaRede)
            return;
        _ = ServidorHttp.SemFluxo(AtualizarSeguroAsync);
    }

    // ------------------------------------------------------------------ parada interna, eventos e o cliente sumido

    private void AoMudarUso()
    {
        var parada = _uso.Parada;
        bool novaParada;
        lock (_trava)
        {
            novaParada = parada && !_paradaVista;
            _paradaVista = parada;
        }
        if (novaParada)
        {
            // A parada interna (o efeito do revogar): o pedido esperando é recusado e o cliente sai.
            ZerarSequencia();
            Registrar("parada", null);
            Confirmacoes.Cancelar();
            _ = Pareamento.RevogarAsync();
        }
        if (_uso.IaAtiva)
        {
            lock (_trava)
            {
                if (_vigia is null)
                    AgendarVigia(_ultimoPedido is { } u ? u + PrazoCliente - _relogio.GetUtcNow() : PrazoCliente);
            }
        }
        Avisar();
    }

    private void AoPausar() => Registrar("pausou", null);

    private void AoRetomar() => Registrar("retomou", null);

    private void AoRegistrar(EntradaHistorico entrada)
    {
        if (entrada.Origem == OrigemComando.Tela && entrada.Enviado)
        {
            ZerarSequencia(); // uma escrita da tela é um toque do usuário no meio
            Registrar("comando", entrada);
        }
    }

    /// <summary>O cliente foi revogado (ou um novo pareou): a conta do Continuar recomeça.</summary>
    private void AoMudarPareamento()
    {
        ZerarSequencia();
        Avisar();
    }

    /// <summary>Ao desconectar, a conta do Continuar recomeça.</summary>
    private void AoMudarSessao()
    {
        if (!(_sessao.Estado == EstadoSessao.Conectado && _sessao.SegurandoConexao))
            ZerarSequencia();
    }

    private void ZerarSequencia()
    {
        lock (_trava)
        {
            _ajustesSeguidos = 0;
            _geracaoSequencia++;
        }
    }

    /// <summary>
    /// Reserva a vaga de um <c>/ajuste</c> na conta, na mesma trava da conferência (pedidos chegam ao
    /// mesmo tempo). Devolve a geração da conta reservada, ou null quando a conta já chegou a
    /// <see cref="AjustesSemToque"/> e o pedido precisa do Continuar.
    /// </summary>
    private long? ReservarAjuste()
    {
        lock (_trava)
        {
            if (_ajustesSeguidos >= AjustesSemToque)
                return null;
            _ajustesSeguidos++;
            return _geracaoSequencia;
        }
    }

    /// <summary>Devolve a vaga de um ajuste que não passou (se a conta não recomeçou no meio).</summary>
    private void DevolverAjuste(long geracao)
    {
        lock (_trava)
        {
            if (_geracaoSequencia == geracao && _ajustesSeguidos > 0)
                _ajustesSeguidos--;
        }
    }

    private void Registrar(string tipo, EntradaHistorico? comando)
    {
        lock (_trava)
        {
            _eventos.AddLast(new EventoPorta(++_ultimoEvento, _relogio.GetLocalNow(), tipo, comando));
            while (_eventos.Count > MaximoEventos)
                _eventos.RemoveFirst();
        }
    }

    /// <summary>Os eventos depois do número <paramref name="desde"/> (os últimos 200 ficam).</summary>
    public IReadOnlyList<EventoPorta> Eventos(long desde = 0)
    {
        lock (_trava)
            return _eventos.Where(e => e.Numero > desde).ToArray();
    }

    /// <summary>Um pedido do cliente começou ou acabou: ele está aí.</summary>
    private void Atividade(bool comecou)
    {
        bool limpou;
        lock (_trava)
        {
            _emAndamento += comecou ? 1 : -1;
            _ultimoPedido = _relogio.GetUtcNow();
            limpou = _avisoConexao is not null;
            _avisoConexao = null;
            if (!comecou)
                AgendarVigia(PrazoCliente);
        }
        if (limpou)
            Avisar();
    }

    /// <summary>Chamar com a trava.</summary>
    private void AgendarVigia(TimeSpan quando)
    {
        if (_descartada)
            return;
        if (quando < TimeSpan.Zero)
            quando = TimeSpan.Zero;
        if (_vigia is null)
            _vigia = _relogio.CreateTimer(_ => AoVencerVigia(), null, quando, Timeout.InfiniteTimeSpan);
        else
            _vigia.Change(quando, Timeout.InfiniteTimeSpan);
    }

    private void AoVencerVigia()
    {
        lock (_trava)
        {
            _vigia?.Dispose();
            _vigia = null;
            if (!_uso.IaAtiva)
                return;
            if (_emAndamento > 0)
            {
                AgendarVigia(PrazoCliente);
                return;
            }
            var falta = _ultimoPedido is { } u ? u + PrazoCliente - _relogio.GetUtcNow() : TimeSpan.Zero;
            if (falta > TimeSpan.Zero)
            {
                AgendarVigia(falta);
                return;
            }
            _avisoConexao = MsgPerdeuConexao;
        }
        _log.Evento("porta: o cliente da IA sumiu (30 s sem pedidos); tarefa terminada");
        _uso.TerminarTarefa();
        Avisar();
    }

    // ------------------------------------------------------------------ os pedidos

    /// <summary>A trava 3, antes de ler o corpo: fora da rede local, 403 (e só o log).</summary>
    public RespostaPorta? Triagem(IPAddress remoto)
    {
        if (_rede.Aceita(remoto))
            return null;
        _log.Evento($"porta: pedido de {RedeLocal.Normalizar(remoto)} recusado (fora da rede local)");
        return new RespostaPorta(403, JsonPorta.Recusa(MsgForaDaRede));
    }

    /// <summary>Para os testes: vê cada pedido ao chegar, antes de qualquer trava (a origem ambiente de quem atende).</summary>
    internal Action<PedidoPorta>? AoChegar { get; set; }

    /// <summary>Atende um pedido (sem socket nos testes). Nunca lança.</summary>
    public async Task<RespostaPorta> AtenderAsync(PedidoPorta pedido)
    {
        try
        {
            AoChegar?.Invoke(pedido);
            if (Triagem(pedido.Remoto) is { } fora)
                return fora;
            if (!_chaves.Ligada(Chave.ControlePelaRede))
                return Recusa(409, MsgChaveDesligada);
            await Pareamento.Carregado;

            if (pedido.Metodo == "GET" && pedido.Caminho == "/ajuda")
                return new RespostaPorta(200, JsonPorta.Ok(w => w.WriteString("texto", TextoAjuda)));
            if (pedido.Metodo == "POST" && pedido.Caminho == "/parear")
                return await ParearAsync(pedido);
            if (!Pareamento.Confere(pedido.Bearer))
                return Recusa(401, MsgNaoPareado);

            Atividade(comecou: true);
            try
            {
                return await AtenderPareadoAsync(pedido);
            }
            finally
            {
                Atividade(comecou: false);
            }
        }
        catch (PedidoInvalidoException e)
        {
            return Recusa(400, e.Message);
        }
        catch (Exception e)
        {
            _log.Evento($"porta: erro inesperado em {pedido.Metodo} {pedido.Caminho} ({e.GetType().Name})");
            return Recusa(500, MsgErroInesperado);
        }
    }

    private async Task<RespostaPorta> ParearAsync(PedidoPorta pedido)
    {
        if (Pareamento.Pareado)
            return Recusa(409, Pareamento.MsgJaPareado);
        var corpo = CorpoPedido.Ler(pedido.Corpo);
        var codigo = corpo.Texto("codigo").Trim();
        var nome = corpo.Texto("nome").Trim();
        if (nome.Length is 0 or > Pareamento.TamanhoMaximoNome)
            throw new PedidoInvalidoException($"O campo \"nome\" tem de 1 a {Pareamento.TamanhoMaximoNome} caracteres.");
        var r = await Pareamento.ParearAsync(codigo, nome);
        if (!r.Ok)
            return Recusa(r.Status, r.Motivo!);
        _uso.Liberar();
        return new RespostaPorta(200, JsonPorta.Ok(w => w.WriteString("chave", r.Chave)));
    }

    private async Task<RespostaPorta> AtenderPareadoAsync(PedidoPorta pedido)
    {
        if (pedido.Metodo == "GET")
        {
            return pedido.Caminho switch
            {
                "/estado" => new RespostaPorta(200, JsonPorta.Ok(EscreverEstado)),
                "/catalogo" => new RespostaPorta(200, JsonPorta.Ok(EscreverCatalogo)),
                "/eventos" => RespostaEventos(pedido),
                "/historico" => RespostaHistorico(pedido),
                _ => Recusa(404, MsgCaminho),
            };
        }

        Func<CorpoPedido, Operacao>? montar = (pedido.Metodo, pedido.Caminho) switch
        {
            ("POST", "/ajuste") => Ajuste,
            ("POST", "/memoria/aplicar") => AplicarMemoria,
            ("POST", "/gerador/tom") => Tom,
            ("POST", "/gerador/sweep") => Sweep,
            ("POST", "/gerador/parar") => PararGerador,
            ("POST", "/aparelho/remote-out") => RemoteOut,
            ("POST", "/aparelho/bluetooth") => Bluetooth,
            ("POST", "/aparelho/nome") => NomeProduto,
            ("POST", "/aparelho/reset-audio") => ResetAudio,
            ("POST", "/aparelho/reset-fabrica") => ResetFabrica,
            ("POST", "/tarefa") => IniciarTarefa,
            ("DELETE", "/tarefa") => TerminarTarefa,
            _ => null,
        };
        if (montar is null)
            return Recusa(404, MsgCaminho);
        var op = montar(CorpoPedido.Ler(pedido.Corpo));
        // A dúvida do Setup bloqueia a IA (regra 5): nenhum POST até o Denis escolher na tela. O
        // parar do gerador fica de fora: desligar o tom nunca é travado.
        if (pedido.Metodo == "POST" && pedido.Caminho != "/gerador/parar" && _setups.MotivoBloqueioIa is { } duvida)
            return RecusaDaPorta(op, duvida);
        return await RodarAsync(op);
    }

    /// <summary>Firmware, a confirmação na tela e a operação, tudo como IA.</summary>
    private async Task<RespostaPorta> RodarAsync(Operacao op)
    {
        if (_sessao.SoLeitura)
            return RecusaDaPorta(op, MsgFirmware);

        using (Origem.Como(OrigemComando.Ia))
        {
            long? vaga = null;
            if (op.ContaSequencia && (vaga = ReservarAjuste()) is null)
            {
                // A sequência da IA pede o toque: o Continuar, antes do ajuste (chaves-de-acesso.md).
                if (op.Quadro is { } quadroSequencia && _sessao.Conferir(quadroSequencia) is { } motivoSequencia)
                    return RecusaDaPorta(op, motivoSequencia);
                var continuar = await Confirmacoes.PedirAsync(new PedidoConfirmacao(TituloContinuar, RiscoContinuar));
                if (Recusada(continuar) is { } recusaSequencia)
                    return RecusaDaPorta(op, recusaSequencia);
                ZerarSequencia();
                _log.Evento("porta: o usuário deixou a IA continuar");
                return await op.Executar(); // o ajuste do Continuar já veio com o toque: não conta
            }
            if (op.Confirmacao is { } pedido)
            {
                // A chave, o só leitura, a conexão e o uso compartilhado recusam já, sem folha.
                if (op.ChaveAntes is { } chave && !_chaves.Ligada(chave))
                    return RecusaDaPorta(op, Sessao.MotivoChaveDesligada);
                if (op.Quadro is { } quadro && _sessao.Conferir(quadro) is { } motivo)
                    return RecusaDaPorta(op, motivo); // o Conferir não registra: entra aqui, uma vez
                var decisao = await Confirmacoes.PedirAsync(pedido);
                if (Recusada(decisao) is { } recusa)
                    return RecusaDaPorta(op, recusa);
                ZerarSequencia(); // uma confirmação na tela é um toque do usuário no meio
            }
            RespostaPorta? resposta = null;
            try
            {
                resposta = await op.Executar();
                return resposta;
            }
            finally
            {
                // O ajuste que não passou não conta: a vaga reservada volta.
                if (vaga is { } geracao && resposta?.Status != 200)
                    DevolverAjuste(geracao);
            }
        }
    }

    /// <summary>O motivo da recusa de uma folha, ou null se o usuário confirmou.</summary>
    private static string? Recusada(DecisaoConfirmacao decisao) => decisao switch
    {
        DecisaoConfirmacao.Confirmou => null,
        DecisaoConfirmacao.Recusou => PedidosConfirmacao.MsgRecusou,
        DecisaoConfirmacao.SemResposta => PedidosConfirmacao.MsgSemResposta,
        DecisaoConfirmacao.Parada => UsoCompartilhado.MsgParada,
        _ => PedidosConfirmacao.MsgOcupado,
    };

    /// <summary>Uma recusa da própria porta a um cliente pareado: vai ao histórico como IA, recusado.</summary>
    private RespostaPorta RecusaDaPorta(Operacao op, string motivo)
    {
        if (op.Comando is { } comando)
            _historico.Registrar(comando, OrigemComando.Ia, false, motivo);
        _log.Evento($"porta: recusado à IA ({motivo})");
        return Recusa(409, motivo);
    }

    private static RespostaPorta Recusa(int status, string motivo) => new(status, JsonPorta.Recusa(motivo));

    private static RespostaPorta Feito(string? mensagem, Action<Utf8JsonWriter>? mais = null) =>
        new(200, JsonPorta.Ok(w =>
        {
            if (mensagem is not null)
                w.WriteString("mensagem", mensagem);
            mais?.Invoke(w);
        }));

    // ------------------------------------------------------------------ as operações

    /// <summary>
    /// Uma operação montada do corpo: o comando para o histórico das recusas da porta, o primeiro
    /// quadro para o <see cref="Sessao.Conferir"/>, a folha (null = não confirma), o que ela faz e,
    /// quando há, a chave própria dela, conferida antes da folha (a de aplicar memória do celular).
    /// <see cref="ContaSequencia"/>: um <c>/ajuste</c>, que entra na conta do Continuar.
    /// </summary>
    private sealed record Operacao(ComandoNaPorta? Comando, string? Quadro, PedidoConfirmacao? Confirmacao, Func<Task<RespostaPorta>> Executar,
        Chave? ChaveAntes = null, bool ContaSequencia = false);

    private static bool Confirma(Chave chave) => ChavesPadrao.De(chave).ConfirmacaoIa == ConfirmacaoIa.Sempre;

    /// <summary>A folha de risco do app: o título diz o que muda, sem mencionar a IA.</summary>
    private static PedidoConfirmacao? Folha(Chave chave, string titulo) =>
        Confirma(chave) ? new PedidoConfirmacao(titulo, ChavesPadrao.De(chave).Risco) : null;

    private static string Codificar(Func<string> codificar)
    {
        try
        {
            return codificar();
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException)
        {
            throw new PedidoInvalidoException(e.Message);
        }
    }

    private Operacao Ajuste(CorpoPedido corpo)
    {
        var codigo = corpo.Texto("codigo");
        var p = Catalogo.Marco1.FirstOrDefault(x => x.Codigo == codigo)
                ?? throw new PedidoInvalidoException($"O código {codigo} não é um ajuste de áudio (veja GET /catalogo).");
        var indice = corpo.InteiroOpcional("indice");
        if (p.Escopo == Escopo.Global && indice is not null)
            throw new PedidoInvalidoException($"O {codigo} é global: sem \"indice\".");
        if (p.Escopo != Escopo.Global && indice is null)
            throw new PedidoInvalidoException($"Falta o campo \"indice\" (0 a {p.Indices - 1}).");

        string quadro;
        Func<Task<ResultadoEscrita>> escrever;
        if (p.Tipo == TipoValor.Roteamento)
        {
            var texto = corpo.Texto("valor");
            quadro = Codificar(() => p.CodificarEscrita(indice, texto));
            escrever = () => _escritor.EscreverAsync(p, indice, texto);
        }
        else
        {
            var numero = corpo.Numero("valor");
            quadro = Codificar(() => p.CodificarEscrita(indice, numero));
            escrever = () => _escritor.EscreverAsync(p, indice, numero);
        }
        // Os ajustes de áudio não pedem o toque (ConfirmacaoIa.Nunca), menos o Continuar da sequência.
        return new Operacao(ComandoNaPorta.Ler(quadro), quadro, null, ContaSequencia: true, Executar: async () =>
        {
            var r = await escrever();
            if (!r.Ok)
                return Recusa(409, r.Motivo ?? Sessao.MotivoFalhaEscrita);
            return Feito(r.Aviso, w =>
            {
                w.WriteString("valor", r.Valor?.NoFio);
                w.WriteString("exibicao", r.Valor?.Exibicao);
                w.WriteBoolean("relido", r.Relido);
                w.WriteBoolean("diferente", r.Diferente);
            });
        });
    }

    /// <summary>
    /// Aplicar uma memória do celular (CAP-12): pelo id ou pelo nome (sem diferença de caixa), só entre
    /// as memórias do Setup em uso; sem Setup em uso, ou uma memória de fora dele, é 400 antes da folha.
    /// Sempre com a folha ("Troca todo o som de uma vez."), depois da chave "Aplicar memória do celular"
    /// e da trava de um ajuste de áudio (o aplicar escreve ajustes de áudio). Com o Setup em dúvida, a
    /// memória não é procurada: a recusa é a da dúvida (409). As recusas vão ao histórico.
    /// </summary>
    private Operacao AplicarMemoria(CorpoPedido corpo)
    {
        var pedido = corpo.Texto("memoria").Trim();
        if (pedido.Length == 0)
            throw new PedidoInvalidoException("O campo \"memoria\" é o id ou o nome de uma memória do celular.");
        var quadro = Catalogo.ParametroDe(Camadas.CodigoMaster)!.CodificarEscrita(null, 0m);
        if (_setups.MotivoBloqueioIa is not null)
            return new Operacao(ComandoNaPorta.AplicarMemoria(pedido), quadro, null,
                () => Task.FromResult(Recusa(409, GerenteSetups.MsgDuvida)), Chave.AplicarMemoriaCelular);

        var setup = _setups.SetupEmUso
                    ?? throw new PedidoInvalidoException("Sem Setup em uso, nenhuma memória do celular se aplica. Escolha um Setup na tela.");
        var doSetup = _setups.MemoriasDe(setup.Id);
        var memoria = doSetup.FirstOrDefault(m => m.Id == pedido)
                      ?? doSetup.FirstOrDefault(m => string.Equals(m.Nome, pedido, StringComparison.OrdinalIgnoreCase))
                      ?? throw new PedidoInvalidoException($"Não há memória do celular \"{pedido}\" no {setup.Nome} (veja GET /estado).");
        return new Operacao(ComandoNaPorta.AplicarMemoria(memoria.Nome), quadro,
            Folha(Chave.AplicarMemoriaCelular, $"Aplicar a memória {memoria.Nome}?"),
            async () =>
            {
                var r = await _setups.AplicarAsync(memoria.Id);
                return r.Ok ? Feito(r.Mensagem) : Recusa(409, r.Mensagem ?? r.Motivo ?? "falhou");
            },
            Chave.AplicarMemoriaCelular);
    }

    private static string Db(decimal db) => db.ToString("+0.0;-0.0;0.0", Tela);

    private Operacao Tom(CorpoPedido corpo)
    {
        var hz = corpo.Inteiro("hz");
        var db = corpo.Numero("db");
        var primeiro = Codificar(() => Catalogo.ParametroDe(Catalogo.CodigoSweepVelocidade)!.CodificarEscrita(null, 0m));
        Codificar(() => Catalogo.ParametroDe(Catalogo.CodigoGeradorFrequencia)!.CodificarEscrita(null, hz));
        Codificar(() => Catalogo.ParametroDe(Catalogo.CodigoGeradorGanho)!.CodificarEscrita(null, db));
        return new Operacao(ComandoNaPorta.Ler(Catalogo.LigarGerador), primeiro,
            Folha(Chave.Gerador, $"Tocar um tom de {hz.ToString(Fio)} Hz a {Db(db)} dB?"),
            async () => Gerador(await _gerador.TocarTomAsync(hz, db)));
    }

    private Operacao Sweep(CorpoPedido corpo)
    {
        var inicio = corpo.Inteiro("inicioHz");
        var fim = corpo.Inteiro("fimHz");
        var db = corpo.Numero("db");
        var velocidade = corpo.Inteiro("velocidade");
        if (velocidade is < 1 or > 3)
            throw new PedidoInvalidoException(GeradorSinal.MsgVelocidade);
        var primeiro = Codificar(() => Catalogo.ParametroDe(Catalogo.CodigoSweepInicio)!.CodificarEscrita(null, inicio));
        Codificar(() => Catalogo.ParametroDe(Catalogo.CodigoSweepFim)!.CodificarEscrita(null, fim));
        Codificar(() => Catalogo.ParametroDe(Catalogo.CodigoSweepGanho)!.CodificarEscrita(null, db));
        return new Operacao(ComandoNaPorta.Ler(Catalogo.LigarGerador), primeiro,
            Folha(Chave.Gerador, $"Tocar um sweep de {inicio.ToString(Fio)} a {fim.ToString(Fio)} Hz a {Db(db)} dB?"),
            async () => Gerador(await _gerador.TocarSweepAsync(inicio, fim, db, velocidade)));
    }

    /// <summary>O parar nunca é travado e não confirma.</summary>
    private Operacao PararGerador(CorpoPedido _) =>
        new(ComandoNaPorta.Ler(Catalogo.DesligarGerador), Catalogo.DesligarGerador, null,
            async () => Gerador(await _gerador.PararAsync()));

    private static RespostaPorta Gerador(ResultadoGerador r) =>
        r.Ok ? Feito(r.Mensagem) : Recusa(409, r.Mensagem ?? r.Motivo ?? "falhou");

    private static RespostaPorta Aparelho(ResultadoAparelho r) =>
        r.Ok ? Feito(r.Mensagem) : Recusa(409, r.Mensagem ?? r.Motivo ?? "falhou");

    private Operacao RemoteOut(CorpoPedido corpo)
    {
        var ligar = corpo.Booleano("ligado");
        var quadro = Catalogo.RemoteOut(ligar);
        return new Operacao(ComandoNaPorta.Ler(quadro), quadro,
            Folha(Chave.RemoteOut, ligar ? "Ligar o remote out?" : "Desligar o remote out?"),
            async () => Aparelho(await _aparelho.DefinirRemoteOutAsync(ligar)));
    }

    private Operacao Bluetooth(CorpoPedido corpo)
    {
        var ligar = corpo.Booleano("ligado");
        var quadro = Catalogo.BluetoothAudio(ligar);
        return new Operacao(ComandoNaPorta.Ler(quadro), quadro,
            Folha(Chave.BluetoothAudio, ligar ? "Ligar o Bluetooth de áudio?" : "Desligar o Bluetooth de áudio?"),
            async () => Aparelho(await _aparelho.DefinirBluetoothAudioAsync(ligar)));
    }

    private Operacao NomeProduto(CorpoPedido corpo)
    {
        var nome = Catalogo.NormalizarNomeProduto(corpo.Texto("nome"))
                   ?? throw new PedidoInvalidoException(ComandosAparelho.MsgNomeInvalido);
        var quadro = Catalogo.NomearProduto(nome);
        return new Operacao(ComandoNaPorta.Ler(quadro), quadro,
            Folha(Chave.NomeProduto, $"Dar ao aparelho o nome {nome}?"),
            async () => Aparelho(await _aparelho.NomearProdutoAsync(nome)));
    }

    private Operacao ResetAudio(CorpoPedido _) =>
        new(ComandoNaPorta.Ler(Catalogo.ResetAudio), Catalogo.ResetAudio,
            Folha(Chave.ResetAudio, "Fazer o reset de áudio?"),
            async () => Aparelho(await _aparelho.ResetAudioAsync()));

    private Operacao ResetFabrica(CorpoPedido _) =>
        new(ComandoNaPorta.Ler(Catalogo.ResetFabrica), Catalogo.ResetFabrica,
            Folha(Chave.ResetFabrica, "Fazer o reset de fábrica?"),
            async () => Aparelho(await _aparelho.ResetFabricaAsync()));

    private Operacao IniciarTarefa(CorpoPedido corpo)
    {
        var frase = corpo.Texto("frase");
        return new Operacao(null, null, null, () =>
        {
            var r = _uso.IniciarTarefa(frase);
            return Task.FromResult(r.Ok ? Feito(null) : Recusa(409, r.Motivo!));
        });
    }

    private Operacao TerminarTarefa(CorpoPedido _) =>
        new(null, null, null, () =>
        {
            _uso.TerminarTarefa();
            return Task.FromResult(Feito(null));
        });

    // ------------------------------------------------------------------ as leituras

    private void EscreverEstado(Utf8JsonWriter w)
    {
        w.WriteStartObject("conexao");
        w.WriteString("estado", _sessao.Estado.ToString());
        w.WriteBoolean("conectado", _sessao.Estado == EstadoSessao.Conectado && _sessao.SegurandoConexao);
        w.WriteString("resumo", _sessao.Resumo);
        w.WriteString("firmware", _sessao.Firmware);
        w.WriteBoolean("soLeitura", _sessao.SoLeitura);
        w.WriteBoolean("lido", _estado.Pronto);
        w.WriteBoolean("lendo", _estado.Lendo);
        w.WriteEndObject();

        var setup = _setups.SetupEmUso;
        w.WriteStartObject("setup");
        w.WriteString("nome", setup?.Nome);
        w.WriteBoolean("duvida", _setups.MotivoBloqueioIa is not null);
        w.WriteString("estado", _setups.Estado switch
        {
            EstadoConferencia.Confere => "confere",
            EstadoConferencia.SomDiferente => "som-diferente",
            EstadoConferencia.Duvida => "duvida",
            EstadoConferencia.Trocando => "trocando",
            _ => "nenhum",
        });
        // As memórias do celular do Setup em uso e o preset em uso (CAP-12).
        var somEmUso = _setups.EmUso;
        w.WriteStartArray("memorias");
        foreach (var m in _setups.MemoriasDe(setup?.Id))
        {
            w.WriteStartObject();
            w.WriteString("id", m.Id);
            w.WriteString("nome", m.Nome);
            w.WriteBoolean("emUso", somEmUso.Tipo == TipoSomEmUso.MemoriaCelular && somEmUso.IdMemoria == m.Id);
            var lembrado = _setups.PresetDe(m);
            w.WriteString("preset", lembrado?.Nome);
            w.WriteString("idPreset", lembrado?.Id);
            w.WriteEndObject();
        }
        w.WriteEndArray();
        if (_setups.PresetEmUso is { } preset)
        {
            w.WriteStartObject("presetEmUso");
            w.WriteString("id", preset.Id);
            w.WriteString("nome", preset.Nome);
            w.WriteEndObject();
        }
        else
        {
            w.WriteNull("presetEmUso");
        }
        w.WriteEndObject();

        w.WriteStartObject("ia");
        w.WriteString("estado", _uso.Estado switch
        {
            EstadoIa.Ativa => "ativa",
            EstadoIa.Pausada => "pausada",
            _ => "inativa",
        });
        w.WriteString("tarefa", _uso.Tarefa);
        w.WriteBoolean("parada", _uso.Parada);
        w.WriteEndObject();

        w.WriteStartObject("gerador");
        w.WriteString("estado", _gerador.Estado.ToString());
        w.WriteBoolean("talvezLigado", _gerador.TalvezLigado);
        w.WriteBoolean("alarme", _gerador.Alarme);
        if (_gerador.Restante is { } restante)
            w.WriteNumber("restanteS", Math.Round(restante.TotalSeconds, 1));
        else
            w.WriteNull("restanteS");
        w.WriteString("mensagem", _gerador.Mensagem);
        w.WriteEndObject();

        if (_monitor.Volts is { } volts)
            w.WriteNumber("voltimetro", volts);
        else
            w.WriteNull("voltimetro");
        w.WriteStartObject("clips");
        w.WriteStartArray("saidas");
        for (var s = 1; s <= MonitorAparelho.Saidas; s++)
            w.WriteBooleanValue(_monitor.ClipSaida(s));
        w.WriteEndArray();
        w.WriteBoolean("entrada", _monitor.ClipEntrada);
        w.WriteEndObject();

        var valores = _estado.Copiar();
        w.WriteStartArray("parametros");
        foreach (var c in Catalogo.Consultas)
        {
            if (!valores.TryGetValue(new Endereco(c.Parametro.Codigo, c.Indice), out var v))
                continue;
            w.WriteStartObject();
            w.WriteString("codigo", c.Parametro.Codigo);
            if (c.Indice is { } i)
                w.WriteNumber("indice", i);
            else
                w.WriteNull("indice");
            w.WriteString("onde", c.Onde);
            w.WriteString("nome", c.Parametro.Nome);
            w.WriteString("noFio", v.NoFio);
            w.WriteString("exibicao", v.Exibicao);
            w.WriteEndObject();
        }
        w.WriteEndArray();
    }

    private static void EscreverCatalogo(Utf8JsonWriter w)
    {
        w.WriteStartArray("parametros");
        foreach (var p in Catalogo.Marco1)
        {
            w.WriteStartObject();
            w.WriteString("codigo", p.Codigo);
            w.WriteString("nome", p.Nome);
            w.WriteString("tipo", p.Tipo.ToString());
            w.WriteString("escopo", p.Escopo switch
            {
                Escopo.Canal => "canal",
                Escopo.Banda => "banda",
                _ => "global",
            });
            w.WriteNumber("indices", p.Indices);
            if (p.Indices > 0)
            {
                w.WriteStartArray("ondeIndices");
                for (var i = 0; i < p.Indices; i++)
                    w.WriteStringValue(p.Onde(i));
                w.WriteEndArray();
            }
            if (p.Minimo is { } min)
                w.WriteNumber("minimo", min);
            if (p.Maximo is { } max)
                w.WriteNumber("maximo", max);
            w.WriteNumber("casas", p.Casas);
            if (p.Unidade is not null)
                w.WriteString("unidade", p.Unidade);
            if (p.Tipo == TipoValor.Roteamento)
            {
                w.WriteStartArray("valores");
                foreach (var v in new[] { "a", "b", "a+b" })
                    w.WriteStringValue(v);
                w.WriteEndArray();
            }
            if (p.Tipo == TipoValor.Booleano)
            {
                w.WriteStartObject("opcoes");
                w.WriteString("0", p.RotuloDesligado);
                w.WriteString("1", p.RotuloLigado);
                w.WriteEndObject();
            }
            if (p.Opcoes is not null)
            {
                w.WriteStartObject("opcoes");
                foreach (var (k, v) in p.Opcoes.OrderBy(o => o.Key))
                    w.WriteString(k.ToString(Fio), v);
                w.WriteEndObject();
            }
            if (p.Especiais is not null)
            {
                w.WriteStartObject("especiais");
                foreach (var (k, v) in p.Especiais.OrderBy(o => o.Key))
                    w.WriteString(k.ToString(Fio), v);
                w.WriteEndObject();
            }
            w.WriteEndObject();
        }
        w.WriteEndArray();
    }

    private RespostaPorta RespostaEventos(PedidoPorta pedido)
    {
        long desde = 0;
        if (pedido.Query.TryGetValue("desde", out var texto)
            && (!long.TryParse(texto, NumberStyles.None, Fio, out desde)))
            return Recusa(400, "O \"desde\" é um número inteiro (o último evento lido, ou 0).");
        long ultimo;
        lock (_trava)
            ultimo = _ultimoEvento;
        // Um "desde" à frente do último: o app reabriu e a contagem recomeçou; vão todos os guardados.
        if (desde > ultimo)
            desde = 0;
        var eventos = Eventos(desde);
        return new RespostaPorta(200, JsonPorta.Ok(w =>
        {
            w.WriteNumber("ultimo", ultimo);
            w.WriteStartArray("eventos");
            foreach (var e in eventos)
            {
                w.WriteStartObject();
                w.WriteNumber("n", e.Numero);
                w.WriteString("hora", e.Hora.ToString("O", Fio));
                w.WriteString("tipo", e.Tipo);
                if (e.Comando is { } c)
                    EscreverEntrada(w, c, comHora: false);
                w.WriteEndObject();
            }
            w.WriteEndArray();
        }));
    }

    private RespostaPorta RespostaHistorico(PedidoPorta pedido)
    {
        var limite = MaximoHistorico;
        if (pedido.Query.TryGetValue("limite", out var texto)
            && (!int.TryParse(texto, NumberStyles.None, Fio, out limite) || limite < 1))
            return Recusa(400, "O \"limite\" é um número de 1 a 200.");
        limite = Math.Min(limite, MaximoHistorico);
        var entradas = _historico.Entradas.Take(limite).ToArray();
        return new RespostaPorta(200, JsonPorta.Ok(w =>
        {
            w.WriteStartArray("entradas");
            foreach (var e in entradas)
            {
                w.WriteStartObject();
                EscreverEntrada(w, e, comHora: true);
                w.WriteEndObject();
            }
            w.WriteEndArray();
        }));
    }

    private static void EscreverEntrada(Utf8JsonWriter w, EntradaHistorico e, bool comHora)
    {
        if (comHora)
            w.WriteString("hora", e.Hora.ToString("O", Fio));
        w.WriteString("origem", e.Origem.ToString());
        w.WriteString("comando", e.Tipo.ToString());
        if (e.Endereco is { } endereco)
        {
            w.WriteString("codigo", endereco.Codigo);
            if (endereco.Indice is { } i)
                w.WriteNumber("indice", i);
        }
        if (e.Valor is not null)
            w.WriteString("valor", e.Valor);
        w.WriteBoolean("enviado", e.Enviado);
        if (e.Motivo is not null)
            w.WriteString("motivo", e.Motivo);
    }

    /// <summary>O texto do <c>GET /ajuda</c>: os caminhos e os corpos.</summary>
    public static readonly string TextoAjuda =
        $$"""
        Porta de comandos da IA (BGS Controle). JSON UTF-8; as respostas trazem {"ok":true,...} ou {"ok":false,"motivo":"..."}.
        Só a rede local da casa. Todo caminho, menos /parear e /ajuda, exige o cabeçalho Authorization: Bearer <chave>.

        POST /parear {"codigo":"123456","nome":"Claude no notebook"} -> {"chave":"..."}: o código de 6 dígitos aparece no celular, embaixo da chave Controle pela rede (Configurações, grupo APARELHO). Um cliente por vez.
        GET  /ajuda: este texto.
        GET  /estado: a conexão, o firmware, o só leitura, o Setup (nome em uso ou null, duvida, as memórias do celular dele com id, nome, emUso e o preset lembrado, e o presetEmUso), a IA e a tarefa, o gerador, o voltímetro, os clips e cada parâmetro lido (codigo, indice, onde, nome, noFio, exibicao).
        GET  /catalogo: os ajustes de áudio graváveis (codigo, escopo, indices, minimo, maximo, casas, unidade, opcoes).
        GET  /eventos?desde=N: os eventos depois do N (pausou, retomou, parada e cada comando da tela enviado). Ficam os últimos 200.
        GET  /historico?limite=N: até 200 entradas do histórico, a mais nova primeiro.
        POST /ajuste {"codigo":"081","indice":2,"valor":2.0}: um ajuste de áudio. O indice é o do /catalogo (saída 1 = 0; banda 25 Hz = 0); sem indice num global. O roteamento leva "a", "b" ou "a+b".
        POST /memoria/aplicar {"memoria":"FILME"}: aplica uma memória do celular, pelo id ou pelo nome (sem diferença de caixa), só entre as memórias do Setup em uso: a regulagem da memória e o preset que ela lembra. Pede o toque do usuário na folha de risco. Sem Setup em uso, ou memória que não é do Setup em uso: 400.
        POST /gerador/tom {"hz":1000,"db":-30.0}: pede o toque do usuário na folha de risco. O gerador desliga sozinho em 30 s.
        POST /gerador/sweep {"inicioHz":20,"fimHz":200,"db":-30.0,"velocidade":1}: velocidade de 1 (lento) a 3 (rápido). Pede o toque do usuário na folha de risco.
        POST /gerador/parar: desliga o gerador. Não pede o toque nem a chave do gerador, e a pausa da IA não o trava; com firmware diferente do R003, recusa como todo POST.
        POST /aparelho/remote-out {"ligado":true}: pede o toque do usuário na folha de risco.
        POST /aparelho/bluetooth {"ligado":true}
        POST /aparelho/nome {"nome":"SALA"}
        POST /aparelho/reset-audio: pede o toque do usuário na folha de risco.
        POST /aparelho/reset-fabrica: pede o toque do usuário na folha de risco.
        POST /tarefa {"frase":"ajustando o EQ da FILME"}: a IA fica ativa com esta tarefa (a frase volta no /estado; a tela não a mostra).
        DELETE /tarefa: a IA terminou.

        A IA não salva nem renomeia memórias: a porta não tem esse comando. As 3 memórias do aparelho não aparecem: a porta não as carrega nem lê os nomes (isso fica no app da Taramps).
        Com o Setup em dúvida (ou trocando de Setup, ou antes de o app conferir o Setup na conexão), todo POST é recusado com 409 "Setup em dúvida; escolha na tela.", menos o /gerador/parar; os GET seguem.
        Uma operação que pede o toque espera até 60 s. Só cabe um pedido esperando.
        O toque do usuário na tela nunca é recusado: ele pausa a IA, que retoma depois de 10 s sem toques. Um ajuste que o usuário acabou de mexer é recusado pela porta por 10 s ("o usuário está mexendo nisso.").
        A cada {{AjustesSemToque}} /ajuste seguidos sem um toque do usuário no meio (uma escrita na tela ou uma confirmação na folha), o próximo espera o Continuar na folha de risco do celular; recusado ou sem resposta, só ele é recusado (409).
        Uma chave desligada em Chaves de acesso no celular recusa com 409 na hora, sem abrir a folha; a IA nunca liga uma chave.
        Com firmware diferente do R003, só os GET respondem.
        Sem pedidos por 30 s com a IA ativa, a tarefa termina. Consulte /eventos para saber da pausa e da parada (o usuário desligou a chave Controle pela rede e o acesso foi revogado).
        Códigos: 400 corpo ou campo inválido; 401 sem chave ou chave errada; 403 fora da rede local; 404 caminho desconhecido; 409 recusa do app (com o motivo); 413 corpo acima de 64 KB; 431 cabeçalho grande demais; 500 erro inesperado no app.
        """;

    private void Avisar()
    {
        try
        {
            Mudou?.Invoke();
        }
        catch (Exception e)
        {
            _log.Evento($"falha no aviso da porta ({e.GetType().Name})");
        }
    }

    public void Dispose()
    {
        lock (_trava)
        {
            _descartada = true;
            _vigia?.Dispose();
            _vigia = null;
        }
        _chaves.Mudou -= AoMudarChave;
        _uso.Mudou -= AoMudarUso;
        _uso.Pausou -= AoPausar;
        _uso.Retomou -= AoRetomar;
        _historico.Registrou -= AoRegistrar;
        Pareamento.Mudou -= AoMudarPareamento;
        _sessao.Mudou -= AoMudarSessao;
        _servidor.Fechar();
    }
}
