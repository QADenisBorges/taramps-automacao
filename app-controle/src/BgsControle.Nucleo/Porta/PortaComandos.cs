using System.Globalization;
using System.Net;
using System.Text.Json;
using BgsControle.Nucleo.Aparelho;
using BgsControle.Nucleo.Escrita;
using BgsControle.Nucleo.Gerador;
using BgsControle.Nucleo.Memorias;
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
/// app) só com a chave "IA pela rede" ligada. Ao desligar a chave, a porta fecha, a IA para e o
/// cliente é apagado.</item>
/// <item><b>Pareamento único</b> (<see cref="Porta.Pareamento"/>): todo caminho, menos
/// <c>/parear</c> e <c>/ajuda</c>, exige a chave do cliente (401).</item>
/// <item><b>Só a rede local</b> (<see cref="IRedeLocal"/>): fora dela, 403, sem ler o corpo e
/// sem histórico (só o <see cref="LogBle"/>).</item>
/// <item><b>Tudo visível:</b> cada comando roda em <c>Origem.Como(OrigemComando.Ia)</c> e passa
/// pela <see cref="Sessao"/>, que registra no histórico. As recusas da própria porta a um cliente
/// pareado (folha recusada ou sem resposta, firmware, pedido esperando) também entram. O PARAR IA
/// revoga o cliente na hora.</item>
/// <item><b>Risco pede confirmação</b> (<see cref="PedidosConfirmacao"/>): as operações com
/// <see cref="ConfirmacaoIa.Sempre"/>. Antes da folha, o <see cref="Sessao.Conferir"/> do primeiro
/// quadro recusa na hora.</item>
/// </list>
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
    public const string MsgNaoPareado = "Cliente não pareado. Peça o código na tela IA pela rede do celular.";
    public const string MsgFirmware = "Firmware diferente do R003: a porta só responde às leituras.";
    public const string MsgPerdeuConexao = "A IA perdeu a conexão.";
    public const string MsgCaminho = "Caminho desconhecido. Veja GET /ajuda.";
    public const string MsgChaveDesligada = "A chave IA pela rede está desligada.";
    public const string MsgErroInesperado = "Erro inesperado no app.";

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
    private readonly GerenteMemorias _memorias;
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
        GerenteMemorias memorias,
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
        _memorias = memorias;
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
        Pareamento.Mudou += Avisar;
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

    /// <summary>Abre ou fecha conforme a chave "IA pela rede". Ao desligar: fecha, PARAR IA e revoga.</summary>
    public async Task AtualizarAsync()
    {
        await _chaves.Carregada;
        await Pareamento.Carregado;
        await _abrir.WaitAsync();
        try
        {
            if (_descartada)
                return;
            if (_chaves.Ligada(Chave.IaPelaRede))
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

    /// <summary>O Revogar da tela IA pela rede: PARAR IA e o cliente apagado (nasce um código novo).</summary>
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
        if (chave != Chave.IaPelaRede)
            return;
        _ = ServidorHttp.SemFluxo(AtualizarSeguroAsync);
    }

    // ------------------------------------------------------------------ PARAR IA, eventos e o cliente sumido

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
            // PARAR IA (na tela IA ativa, ou daqui): o pedido esperando é recusado e o cliente sai.
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
            Registrar("comando", entrada);
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
            if (!_chaves.Ligada(Chave.IaPelaRede))
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
            ("POST", "/memoria/carregar") => CarregarMemoria,
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
            if (op.Confirmacao is { } pedido)
            {
                // A chave, o só leitura, a conexão e o uso compartilhado recusam já, sem folha.
                if (op.Quadro is { } quadro && _sessao.Conferir(quadro) is { } motivo)
                    return RecusaDaPorta(op, motivo); // o Conferir não registra: entra aqui, uma vez
                var decisao = await Confirmacoes.PedirAsync(pedido);
                var recusa = decisao switch
                {
                    DecisaoConfirmacao.Confirmou => null,
                    DecisaoConfirmacao.Recusou => PedidosConfirmacao.MsgRecusou,
                    DecisaoConfirmacao.SemResposta => PedidosConfirmacao.MsgSemResposta,
                    DecisaoConfirmacao.Parada => UsoCompartilhado.MsgParada,
                    _ => PedidosConfirmacao.MsgOcupado,
                };
                if (recusa is not null)
                    return RecusaDaPorta(op, recusa);
            }
            return await op.Executar();
        }
    }

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
    /// quadro para o <see cref="Sessao.Conferir"/>, a folha (null = não confirma) e o que ela faz.
    /// </summary>
    private sealed record Operacao(ComandoNaPorta? Comando, string? Quadro, PedidoConfirmacao? Confirmacao, Func<Task<RespostaPorta>> Executar);

    private static bool Confirma(Chave chave) => ChavesPadrao.De(chave).ConfirmacaoIa == ConfirmacaoIa.Sempre;

    private static PedidoConfirmacao? Folha(Chave chave, string titulo, string botao) =>
        Confirma(chave) ? new PedidoConfirmacao(titulo, ChavesPadrao.De(chave).Risco, botao) : null;

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
        // Os ajustes de áudio nunca pedem o toque (ConfirmacaoIa.Nunca).
        return new Operacao(ComandoNaPorta.Ler(quadro), quadro, null, async () =>
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

    /// <summary>A memória de 1 a 3 (como na tela); o núcleo usa de 0 a 2.</summary>
    private static int Memoria(CorpoPedido corpo)
    {
        var n = corpo.Inteiro("memoria");
        if (n is < 1 or > Catalogo.Memorias)
            throw new PedidoInvalidoException("O campo \"memoria\" vai de 1 a 3.");
        return n - 1;
    }

    private string AlvoMemoria(int n) => _memorias.Nome(n) is { Length: > 0 } nome ? nome : $"memória {n + 1}";

    private Operacao CarregarMemoria(CorpoPedido corpo)
    {
        var n = Memoria(corpo);
        var quadro = Catalogo.CarregarMemoria(n);
        var alvo = AlvoMemoria(n);
        return new Operacao(ComandoNaPorta.Ler(quadro), quadro,
            Folha(Chave.CarregarMemoria, $"A IA pede para carregar a {alvo}", $"Carregar a {alvo}"), async () =>
            {
                var r = await _memorias.CarregarAsync(n);
                return r.Ok ? Feito(r.Mensagem) : Recusa(409, r.Mensagem ?? r.Motivo ?? "falhou");
            });
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
            Folha(Chave.Gerador, $"A IA pede para tocar um tom de {hz.ToString(Fio)} Hz a {Db(db)} dB", "Tocar o tom"),
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
            Folha(Chave.Gerador, $"A IA pede para tocar um sweep de {inicio.ToString(Fio)} a {fim.ToString(Fio)} Hz a {Db(db)} dB", "Tocar o sweep"),
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
        var acao = ligar ? "ligar o remote out" : "desligar o remote out";
        return new Operacao(ComandoNaPorta.Ler(quadro), quadro,
            Folha(Chave.RemoteOut, $"A IA pede para {acao}", ligar ? "Ligar o remote out" : "Desligar o remote out"),
            async () => Aparelho(await _aparelho.DefinirRemoteOutAsync(ligar)));
    }

    private Operacao Bluetooth(CorpoPedido corpo)
    {
        var ligar = corpo.Booleano("ligado");
        var quadro = Catalogo.BluetoothAudio(ligar);
        return new Operacao(ComandoNaPorta.Ler(quadro), quadro,
            Folha(Chave.BluetoothAudio, $"A IA pede para {(ligar ? "ligar" : "desligar")} o Bluetooth de áudio",
                ligar ? "Ligar o Bluetooth de áudio" : "Desligar o Bluetooth de áudio"),
            async () => Aparelho(await _aparelho.DefinirBluetoothAudioAsync(ligar)));
    }

    private Operacao NomeProduto(CorpoPedido corpo)
    {
        var nome = Catalogo.NormalizarNomeProduto(corpo.Texto("nome"))
                   ?? throw new PedidoInvalidoException(ComandosAparelho.MsgNomeInvalido);
        var quadro = Catalogo.NomearProduto(nome);
        return new Operacao(ComandoNaPorta.Ler(quadro), quadro,
            Folha(Chave.NomeProduto, $"A IA pede para dar ao aparelho o nome {nome}", $"Chamar de {nome}"),
            async () => Aparelho(await _aparelho.NomearProdutoAsync(nome)));
    }

    private Operacao ResetAudio(CorpoPedido _) =>
        new(ComandoNaPorta.Ler(Catalogo.ResetAudio), Catalogo.ResetAudio,
            Folha(Chave.ResetAudio, "A IA pede para fazer o reset de áudio", "Fazer o reset de áudio"),
            async () => Aparelho(await _aparelho.ResetAudioAsync()));

    private Operacao ResetFabrica(CorpoPedido _) =>
        new(ComandoNaPorta.Ler(Catalogo.ResetFabrica), Catalogo.ResetFabrica,
            Folha(Chave.ResetFabrica, "A IA pede para fazer o reset de fábrica", "Fazer o reset de fábrica"),
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

        w.WriteStartObject("memorias");
        if (_memorias.EmUso is { } emUso)
            w.WriteNumber("emUso", emUso + 1);
        else
            w.WriteNull("emUso");
        w.WriteStartArray("nomes");
        for (var n = 0; n < Catalogo.Memorias; n++)
            w.WriteStringValue(_memorias.Nome(n));
        w.WriteEndArray();
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
        w.WriteEndObject();

        w.WriteStartObject("ia");
        w.WriteString("estado", _uso.Estado switch
        {
            EstadoIa.Ativa => "ativa",
            EstadoIa.Pausada => "pausada",
            _ => "inativa",
        });
        w.WriteString("tarefa", _uso.Tarefa);
        w.WriteString("modoToque", _uso.Modo == ModoToque.PausarIa ? "pausar-ia" : "travar-ate-parar-ia");
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
    public const string TextoAjuda =
        """
        Porta de comandos da IA (BGS Controle). JSON UTF-8; as respostas trazem {"ok":true,...} ou {"ok":false,"motivo":"..."}.
        Só a rede local da casa. Todo caminho, menos /parear e /ajuda, exige o cabeçalho Authorization: Bearer <chave>.

        POST /parear {"codigo":"123456","nome":"Claude no notebook"} -> {"chave":"..."}: o código de 6 dígitos está na tela IA pela rede do celular. Um cliente por vez.
        GET  /ajuda: este texto.
        GET  /estado: a conexão, o firmware, o só leitura, a memória do aparelho em uso e os nomes, o Setup (nome em uso ou null, duvida), a IA e a tarefa, o gerador, o voltímetro, os clips e cada parâmetro lido (codigo, indice, onde, nome, noFio, exibicao).
        GET  /catalogo: os ajustes de áudio graváveis (codigo, escopo, indices, minimo, maximo, casas, unidade, opcoes).
        GET  /eventos?desde=N: os eventos depois do N (pausou, retomou, parada e cada comando da tela enviado). Ficam os últimos 200.
        GET  /historico?limite=N: até 200 entradas do histórico, a mais nova primeiro.
        POST /ajuste {"codigo":"081","indice":2,"valor":2.0}: um ajuste de áudio. O indice é o do /catalogo (saída 1 = 0; banda 25 Hz = 0); sem indice num global. O roteamento leva "a", "b" ou "a+b".
        POST /memoria/carregar {"memoria":1}: carrega a memória do aparelho de 1 a 3. Pede o toque do usuário na tela.
        POST /gerador/tom {"hz":1000,"db":-30.0}: pede o toque do usuário na tela. O gerador desliga sozinho em 30 s.
        POST /gerador/sweep {"inicioHz":20,"fimHz":200,"db":-30.0,"velocidade":1}: velocidade de 1 (lento) a 3 (rápido). Pede o toque.
        POST /gerador/parar: desliga o gerador. Não pede o toque nem a chave do gerador, e a pausa da IA não o trava; com firmware diferente do R003, recusa como todo POST.
        POST /aparelho/remote-out {"ligado":true}: pede o toque do usuário na tela.
        POST /aparelho/bluetooth {"ligado":true}
        POST /aparelho/nome {"nome":"SALA"}
        POST /aparelho/reset-audio: pede o toque do usuário na tela.
        POST /aparelho/reset-fabrica: pede o toque do usuário na tela.
        POST /tarefa {"frase":"IA ajustando o EQ da FILME"}: a faixa da tela mostra a frase; a IA fica ativa.
        DELETE /tarefa: a IA terminou; os controles da tela destravam.

        A IA não salva nem renomeia memórias: a porta não tem esse comando.
        Com o Setup em dúvida (ou trocando de Setup, ou antes de o app conferir o Setup na conexão), todo POST é recusado com 409 "Setup em dúvida; escolha na tela.", menos o /gerador/parar; os GET seguem.
        Uma operação que pede o toque espera até 60 s. Só cabe um pedido esperando.
        Uma chave desligada em Chaves de acesso no celular recusa com 409 na hora, sem abrir a folha; a IA nunca liga uma chave.
        Com firmware diferente do R003, só os GET respondem.
        Sem pedidos por 30 s com a IA ativa, a tarefa termina. Consulte /eventos para saber da pausa e do PARAR IA.
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
        Pareamento.Mudou -= Avisar;
        _servidor.Fechar();
    }
}
