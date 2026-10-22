using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using BgsControle.Nucleo.Registro;

namespace BgsControle.Nucleo.Porta;

/// <summary>
/// Um pedido HTTP que chegou à porta, já separado: o método, o caminho (sem a query), a query, a
/// chave do cliente (<c>Authorization: Bearer</c>), o corpo e o endereço de quem pediu.
/// </summary>
public sealed record PedidoPorta(
    string Metodo,
    string Caminho,
    IReadOnlyDictionary<string, string> Query,
    string? Bearer,
    byte[] Corpo,
    IPAddress Remoto)
{
    /// <summary>Um pedido montado à mão (os testes sem socket).</summary>
    public static PedidoPorta De(string metodo, string alvo, IPAddress remoto, string? corpo = null, string? bearer = null)
    {
        var (caminho, query) = ServidorHttp.SepararAlvo(alvo);
        return new PedidoPorta(metodo, caminho, query, bearer, corpo is null ? [] : Encoding.UTF8.GetBytes(corpo), remoto);
    }
}

/// <summary>A resposta da porta: o código HTTP e o JSON (UTF-8).</summary>
public sealed record RespostaPorta(int Status, string Json);

/// <summary>
/// Um servidor HTTP/1.1 mínimo sobre <see cref="TcpListener"/> (sem http.sys nem Kestrel: igual no
/// Windows e no Android). Um pedido por conexão (<c>Connection: close</c>): a linha do pedido, os
/// cabeçalhos e o corpo pelo <c>Content-Length</c>.
/// <list type="bullet">
/// <item>Os cabeçalhos têm de chegar em <see cref="PrazoCabecalho"/> (10 s); senão, a conexão fecha
/// sem resposta.</item>
/// <item>Um corpo acima de <see cref="MaximoCorpo"/> (64 KB) recebe 413, sem ser lido.</item>
/// <item>Antes do corpo, a <c>triagem</c> confere quem pede: uma resposta dela (o 403 de fora da
/// rede) volta sem ler o corpo.</item>
/// </list>
/// As conexões são aceitas com <see cref="ExecutionContext.SuppressFlow"/>: nada do fluxo de quem
/// abriu (como a <see cref="Uso.Origem"/>) entra na porta.
/// </summary>
public sealed class ServidorHttp
{
    public const int MaximoCorpo = 64 * 1024;
    public const int MaximoCabecalho = 16 * 1024;

    /// <summary>Conexões ao mesmo tempo; acima disso, a conexão nova fecha na hora.</summary>
    public const int MaximoConexoes = 16;

    public static TimeSpan PrazoCabecalho { get; } = TimeSpan.FromSeconds(10);

    /// <summary>O prazo do corpo, depois dos cabeçalhos.</summary>
    public static TimeSpan PrazoCorpo { get; } = TimeSpan.FromSeconds(10);

    private static readonly byte[] FimCabecalho = "\r\n\r\n"u8.ToArray();

    private readonly int _porta;
    private readonly Func<PedidoPorta, Task<RespostaPorta>> _atender;
    private readonly Func<IPAddress, RespostaPorta?>? _triagem;
    private readonly LogBle? _log;

    private readonly object _trava = new();
    private readonly SemaphoreSlim _conexoes = new(MaximoConexoes, MaximoConexoes);
    private TcpListener? _escuta;
    private CancellationTokenSource? _fim;

    /// <param name="porta">O número da porta (0 = o sistema escolhe; os testes).</param>
    /// <param name="atender">Quem responde a cada pedido.</param>
    /// <param name="triagem">Confere quem pede antes de ler o corpo: uma resposta volta na hora.</param>
    public ServidorHttp(int porta, Func<PedidoPorta, Task<RespostaPorta>> atender, Func<IPAddress, RespostaPorta?>? triagem = null, LogBle? log = null)
    {
        _porta = porta;
        _atender = atender;
        _triagem = triagem;
        _log = log;
    }

    /// <summary>A porta escutando agora (a escolhida pelo sistema, com 0), ou null fechada.</summary>
    public int? PortaLocal
    {
        get
        {
            lock (_trava)
                return _escuta?.LocalEndpoint is IPEndPoint p ? p.Port : null;
        }
    }

    public bool Aberto
    {
        get
        {
            lock (_trava)
                return _escuta is not null;
        }
    }

    /// <summary>Escuta em todas as interfaces. False = não abriu (a porta está ocupada, por exemplo).</summary>
    public bool Abrir()
    {
        lock (_trava)
        {
            if (_escuta is not null)
                return true;
            TcpListener escuta;
            try
            {
                escuta = CriarEscuta();
                escuta.Start();
            }
            catch (Exception e) when (e is SocketException or NotSupportedException or ObjectDisposedException)
            {
                _log?.Evento($"porta: não abriu a {_porta} ({e.GetType().Name})");
                return false;
            }
            _escuta = escuta;
            _fim = new CancellationTokenSource();
            var fim = _fim.Token;
            _ = SemFluxo(() => AceitarAsync(escuta, fim));
            return true;
        }
    }

    /// <summary>Para de escutar e corta as conexões em andamento.</summary>
    public void Fechar()
    {
        TcpListener? escuta;
        CancellationTokenSource? fim;
        lock (_trava)
        {
            escuta = _escuta;
            fim = _fim;
            _escuta = null;
            _fim = null;
        }
        if (escuta is null)
            return;
        fim?.Cancel();
        try
        {
            escuta.Stop();
        }
        catch (SocketException)
        {
            // Já parada.
        }
        escuta.Dispose();
    }

    private TcpListener CriarEscuta()
    {
        if (Socket.OSSupportsIPv6)
        {
            try
            {
                // IPv6 e IPv4 no mesmo socket: o IPv4 chega mapeado (::ffff:192.168.0.10).
                var dupla = new TcpListener(IPAddress.IPv6Any, _porta);
                dupla.Server.DualMode = true;
                return dupla;
            }
            catch (Exception e) when (e is SocketException or NotSupportedException)
            {
                // Sem IPv6 de verdade: só o IPv4.
            }
        }
        return new TcpListener(IPAddress.Any, _porta);
    }

    private async Task AceitarAsync(TcpListener escuta, CancellationToken fim)
    {
        while (!fim.IsCancellationRequested)
        {
            TcpClient cliente;
            try
            {
                cliente = await escuta.AcceptTcpClientAsync(fim);
            }
            catch (Exception e) when (e is OperationCanceledException or ObjectDisposedException or SocketException or InvalidOperationException)
            {
                if (fim.IsCancellationRequested)
                    return;
                if (e is SocketException)
                    continue; // uma conexão que caiu antes de ser aceita
                return;
            }
            if (!_conexoes.Wait(0))
            {
                // Acima do limite: fecha já, sem ler nada.
                cliente.Dispose();
                continue;
            }
            _ = SemFluxo(() => AtenderConexaoAsync(cliente, fim));
        }
    }

    private async Task AtenderConexaoAsync(TcpClient cliente, CancellationToken fim)
    {
        using (cliente)
        {
            try
            {
                var remoto = (cliente.Client.RemoteEndPoint as IPEndPoint)?.Address ?? IPAddress.None;
                var fluxo = cliente.GetStream();
                // Quem pede, logo ao aceitar: fora da rede, 403 sem ler nada.
                var resposta = _triagem?.Invoke(remoto) ?? await LerEAtenderAsync(fluxo, remoto, fim);
                if (resposta is null)
                    return; // cabeçalho que não chegou no prazo: fecha sem resposta
                await EscreverAsync(fluxo, resposta, fim);
                try
                {
                    cliente.Client.Shutdown(SocketShutdown.Send);
                }
                catch (SocketException)
                {
                    // O cliente já foi.
                }
                await DescartarRestoAsync(fluxo, fim);
            }
            catch (Exception e) when (e is IOException or SocketException or OperationCanceledException or ObjectDisposedException)
            {
                // A conexão caiu ou a porta fechou.
            }
            catch (Exception e)
            {
                _log?.Evento($"porta: erro inesperado numa conexão ({e.GetType().Name})");
            }
            finally
            {
                _conexoes.Release();
            }
        }
    }

    /// <summary>Lê o pedido e atende. Null = fechar sem resposta (cabeçalho fora do prazo).</summary>
    private async Task<RespostaPorta?> LerEAtenderAsync(NetworkStream fluxo, IPAddress remoto, CancellationToken fim)
    {
        var buffer = new byte[MaximoCabecalho];
        var lidos = 0;
        int fimCabecalho;
        using (var prazo = CancellationTokenSource.CreateLinkedTokenSource(fim))
        {
            prazo.CancelAfter(PrazoCabecalho);
            try
            {
                while (true)
                {
                    fimCabecalho = buffer.AsSpan(0, lidos).IndexOf(FimCabecalho);
                    if (fimCabecalho >= 0)
                        break;
                    if (lidos == buffer.Length)
                        return Erro(431, "Cabeçalho grande demais.");
                    var n = await fluxo.ReadAsync(buffer.AsMemory(lidos), prazo.Token);
                    if (n == 0)
                        return null;
                    lidos += n;
                }
            }
            catch (OperationCanceledException) when (!fim.IsCancellationRequested)
            {
                return null; // 10 s sem o cabeçalho inteiro
            }
        }

        var texto = Encoding.ASCII.GetString(buffer, 0, fimCabecalho);
        var linhas = texto.Split("\r\n");
        var partes = linhas[0].Split(' ');
        if (partes.Length != 3 || !partes[2].StartsWith("HTTP/1.", StringComparison.Ordinal))
            return Erro(400, "Pedido HTTP inválido.");
        var metodo = partes[0].ToUpperInvariant();
        var (caminho, query) = SepararAlvo(partes[1]);

        long tamanho = 0;
        string? bearer = null;
        for (var i = 1; i < linhas.Length; i++)
        {
            var dois = linhas[i].IndexOf(':');
            if (dois <= 0)
                continue;
            var nome = linhas[i][..dois].Trim();
            var valor = linhas[i][(dois + 1)..].Trim();
            if (nome.Equals("Content-Length", StringComparison.OrdinalIgnoreCase))
            {
                if (!long.TryParse(valor, NumberStyles.None, CultureInfo.InvariantCulture, out tamanho))
                    return Erro(400, "Content-Length inválido.");
            }
            else if (nome.Equals("Authorization", StringComparison.OrdinalIgnoreCase)
                     && valor.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                bearer = valor["Bearer ".Length..].Trim();
            }
            else if (nome.Equals("Transfer-Encoding", StringComparison.OrdinalIgnoreCase))
            {
                return Erro(400, "Use Content-Length, não Transfer-Encoding.");
            }
        }

        if (tamanho > MaximoCorpo)
            return Erro(413, "O corpo passa de 64 KB.");

        var corpo = new byte[tamanho];
        var jaLidos = lidos - (fimCabecalho + FimCabecalho.Length);
        var copiar = (int)Math.Min(jaLidos, tamanho);
        Array.Copy(buffer, fimCabecalho + FimCabecalho.Length, corpo, 0, copiar);
        var recebidos = copiar;
        using (var prazo = CancellationTokenSource.CreateLinkedTokenSource(fim))
        {
            prazo.CancelAfter(PrazoCorpo);
            try
            {
                while (recebidos < tamanho)
                {
                    var n = await fluxo.ReadAsync(corpo.AsMemory(recebidos), prazo.Token);
                    if (n == 0)
                        return null;
                    recebidos += n;
                }
            }
            catch (OperationCanceledException) when (!fim.IsCancellationRequested)
            {
                return null;
            }
        }

        return await _atender(new PedidoPorta(metodo, caminho, query, bearer, corpo, remoto));
    }

    /// <summary>
    /// Depois da resposta, joga fora o que o cliente ainda manda (um corpo que não foi lido, no 403
    /// e no 413), por pouco tempo: fechar com dados por ler faz o sistema mandar um RST, e o cliente
    /// perderia a resposta. Nada disso é interpretado.
    /// </summary>
    private static async Task DescartarRestoAsync(NetworkStream fluxo, CancellationToken fim)
    {
        var lixo = new byte[4096];
        var total = 0;
        using var prazo = CancellationTokenSource.CreateLinkedTokenSource(fim);
        prazo.CancelAfter(TimeSpan.FromSeconds(1));
        try
        {
            while (total < 4 * MaximoCorpo)
            {
                var n = await fluxo.ReadAsync(lixo, prazo.Token);
                if (n == 0)
                    return;
                total += n;
            }
        }
        catch (OperationCanceledException)
        {
            // O cliente não fechou a tempo: fecha assim mesmo.
        }
    }

    private static async Task EscreverAsync(NetworkStream fluxo, RespostaPorta resposta, CancellationToken fim)
    {
        var corpo = Encoding.UTF8.GetBytes(resposta.Json);
        var cabecalho = $"HTTP/1.1 {resposta.Status} {Frase(resposta.Status)}\r\n" +
                        "Content-Type: application/json; charset=utf-8\r\n" +
                        $"Content-Length: {corpo.Length.ToString(CultureInfo.InvariantCulture)}\r\n" +
                        "Connection: close\r\n\r\n";
        await fluxo.WriteAsync(Encoding.ASCII.GetBytes(cabecalho), fim);
        await fluxo.WriteAsync(corpo, fim);
        await fluxo.FlushAsync(fim);
    }

    private static RespostaPorta Erro(int status, string motivo) => new(status, JsonPorta.Recusa(motivo));

    private static string Frase(int status) => status switch
    {
        200 => "OK",
        400 => "Bad Request",
        401 => "Unauthorized",
        403 => "Forbidden",
        404 => "Not Found",
        409 => "Conflict",
        413 => "Payload Too Large",
        500 => "Internal Server Error",
        431 => "Request Header Fields Too Large",
        _ => "Status",
    };

    /// <summary>
    /// Roda o trabalho numa tarefa à parte sem o fluxo de quem chama (<see cref="ExecutionContext.SuppressFlow"/>):
    /// a origem ambiente (<see cref="Uso.Origem"/>) nunca vaza para ele.
    /// </summary>
    internal static Task SemFluxo(Func<Task> trabalho)
    {
        if (ExecutionContext.IsFlowSuppressed())
            return Task.Run(trabalho);
        using (ExecutionContext.SuppressFlow())
            return Task.Run(trabalho);
    }

    /// <summary>Separa o caminho e a query (<c>/eventos?desde=3</c>). O caminho fica sem a barra do fim.</summary>
    public static (string Caminho, IReadOnlyDictionary<string, string> Query) SepararAlvo(string alvo)
    {
        var interrogacao = alvo.IndexOf('?');
        var caminho = interrogacao < 0 ? alvo : alvo[..interrogacao];
        var query = new Dictionary<string, string>(StringComparer.Ordinal);
        if (interrogacao >= 0)
        {
            foreach (var par in alvo[(interrogacao + 1)..].Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var igual = par.IndexOf('=');
                var nome = Uri.UnescapeDataString(igual < 0 ? par : par[..igual]);
                var valor = igual < 0 ? "" : Uri.UnescapeDataString(par[(igual + 1)..].Replace('+', ' '));
                query[nome] = valor;
            }
        }
        caminho = Uri.UnescapeDataString(caminho);
        if (caminho.Length > 1)
            caminho = caminho.TrimEnd('/');
        return (caminho, query);
    }
}
