using System.Net;
using System.Net.Sockets;
using BgsControle.Nucleo.Aparelho;
using BgsControle.Nucleo.Escrita;
using BgsControle.Nucleo.Gerador;
using BgsControle.Nucleo.Leitura;
using BgsControle.Nucleo.Player;
using BgsControle.Nucleo.Porta;
using BgsControle.Nucleo.Presets;
using BgsControle.Nucleo.Registro;
using BgsControle.Nucleo.Seguranca;
using BgsControle.Nucleo;
using BgsControle.Nucleo.Setups;
using BgsControle.Nucleo.Uso;
using BgsControle.Simulador;

namespace Agente.Testes;

/// <summary>Um texto guardado em memória (os arquivos privados do celular).</summary>
internal sealed class TextoMemoria(string? inicial = null) : IRepositorioTexto
{
    private string? _texto = inicial;

    public string? Texto => Volatile.Read(ref _texto);

    public Task<string?> LerAsync() => Task.FromResult(Texto);

    public Task GravarAsync(string conteudo)
    {
        Volatile.Write(ref _texto, conteudo);
        return Task.CompletedTask;
    }
}

internal sealed class ChavesMemoria : IRepositorioChaves
{
    private string? _json;

    public Task<string?> LerAsync() => Task.FromResult(Volatile.Read(ref _json));

    public Task GravarAsync(string json)
    {
        Volatile.Write(ref _json, json);
        return Task.CompletedTask;
    }
}

/// <summary>O cofre da senha do processador, já com a senha do simulador.</summary>
internal sealed class CofreMemoria(string senha) : ICofreSenha
{
    private string? _senha = senha;

    public Task<string?> LerAsync() => Task.FromResult(_senha);

    public Task GuardarAsync(string senha)
    {
        _senha = senha;
        return Task.CompletedTask;
    }

    public Task ApagarAsync()
    {
        _senha = null;
        return Task.CompletedTask;
    }
}

/// <summary>
/// A rede local dos testes: só o loopback (o notebook e o "celular" são a mesma máquina). A <see cref="RedeLocal"/> do app
/// recusa o loopback; a porta aceita outra <see cref="IRedeLocal"/>, então a regra do app não muda.
/// </summary>
internal sealed class RedeLocalhost : IRedeLocal
{
    public bool Aceita(IPAddress remoto) => IPAddress.IsLoopback(RedeLocal.Normalizar(remoto));

    public string? Endereco => "127.0.0.1";
}

/// <summary>
/// O núcleo do app sobre o <see cref="ProcessadorSimulado"/>, no tempo real (a ferramenta e o modelo falso rodam como
/// processos de verdade; o serviço, no processo dos testes): o cenário de teste do app (um Setup com uma memória, só como dado de teste), conectado, lido, com a chave
/// "Controle pela rede" ligada e a <see cref="PortaComandos"/> com a <see cref="CriacaoMemoria"/> numa porta livre de
/// localhost. O lado do usuário vai direto no <see cref="Criacao"/>.
/// </summary>
internal sealed class Bancada : IAsyncDisposable
{
    private static readonly TemposSessao Tempos = new(
        Scan: TimeSpan.FromSeconds(1),
        TentativaConexao: TimeSpan.FromSeconds(2),
        PausaEntreTentativas: TimeSpan.FromMilliseconds(10),
        RespostaSenha: TimeSpan.FromSeconds(2),
        PrazoSenha: TimeSpan.FromSeconds(60));

    private static readonly TemposLeitura LeituraRapida = new(TimeSpan.Zero, TimeSpan.FromMilliseconds(500));

    private static readonly TemposEscrita Escrita = new(
        Intervalo: TimeSpan.FromMilliseconds(30),
        Arrasto: TimeSpan.FromMilliseconds(300),
        PrazoEco: TimeSpan.FromMilliseconds(600),
        PrazoReleitura: TimeSpan.FromMilliseconds(800));

    private readonly LogBle _log = new();
    private readonly ChavesMemoria _repositorioChaves = new();
    private readonly TextoMemoria _guardadoPorta = new();
    private readonly Historico _historico;
    private readonly UsoCompartilhado _uso;
    private readonly LeituraAutomatica _leitura;
    private readonly EstadoAparelho _estado;
    private readonly Escritor _escritor;
    private readonly CopiaAntes _copia;
    private readonly GerenteSetups _setups;
    private readonly GeradorSinal _gerador;
    private readonly MonitorAparelho _monitor;
    private readonly ComandosAparelho _comandos;
    private readonly PedidosConfirmacao _confirmacoes;

    public Bancada()
    {
        Simulador = new ProcessadorSimulado { AtrasoSenha = TimeSpan.Zero, AtrasoLeitura = TimeSpan.Zero, AtrasoEco = TimeSpan.FromMilliseconds(20) };
        var cofre = new CofreMemoria(Simulador.Senha);
        Chaves = new Chaves(_repositorioChaves);
        _historico = new Historico(new TextoMemoria(), null, _log);
        _uso = new UsoCompartilhado(null, _log);
        Sessao = new Sessao(Simulador, cofre, _log, Tempos, Chaves, _historico, _uso);
        var leitor = new LeitorCompleto(Sessao, _log, LeituraRapida);
        _leitura = new LeituraAutomatica(Sessao, leitor);
        _estado = new EstadoAparelho(Sessao, _leitura);
        _escritor = new Escritor(Sessao, _estado, _log, Escrita);
        _copia = new CopiaAntes(Sessao, _estado, _escritor, _leitura, new TextoMemoria(), _log);
        var link = new LinkCanais(Sessao, _estado, _escritor, new TextoMemoria(), _log);
        var presets = new GerentePresets(Sessao, _estado, _escritor, _leitura, _copia, new TextoMemoria(CenarioFilme.PresetsJson), _log);
        var playlists = new GerentePlaylists(new TextoMemoria(), _log);
        _setups = new GerenteSetups(Sessao, _estado, _escritor, _leitura, _copia, link, presets, playlists, Chaves,
            new TextoMemoria(CenarioFilme.SetupsJson), new TextoMemoria(), _log);
        _gerador = new GeradorSinal(Sessao, _estado, _leitura, _copia, _log, Escrita, new TemposGerador(TimeSpan.FromSeconds(30), TimeSpan.FromMilliseconds(100)));
        _monitor = new MonitorAparelho(Sessao, _leitura);
        _comandos = new ComandosAparelho(Sessao, _estado, _leitura, _copia, cofre, _log, _gerador, Escrita);
        Pareamento = new Pareamento(_guardadoPorta, null, _log);
        _confirmacoes = new PedidosConfirmacao(null, _log);
        Criacao = new CriacaoMemoria(Sessao, _estado, _escritor, _copia, _setups, _uso, _log);
        NumeroPorta = PortaLivre();
        Porta = NovaPorta();
    }

    public ProcessadorSimulado Simulador { get; }

    public Chaves Chaves { get; }

    public Sessao Sessao { get; }

    public CriacaoMemoria Criacao { get; }

    public Pareamento Pareamento { get; private set; }

    public PortaComandos Porta { get; private set; }

    public int NumeroPorta { get; }

    /// <summary>O endereço que a ferramenta usa para parear.</summary>
    public string Endereco => $"127.0.0.1:{NumeroPorta}";

    /// <summary>Conectada, lida, com o Setup conferido e a porta aberta.</summary>
    public static async Task<Bancada> ProntaAsync()
    {
        var b = new Bancada();
        await b.ConectarAsync();
        await b.LigarPortaAsync();
        return b;
    }

    private PortaComandos NovaPorta() =>
        new(Sessao, Chaves, _uso, _historico, Pareamento, _confirmacoes, new RedeLocalhost(), _estado, _escritor, _setups, _gerador,
            _comandos, _monitor, _log, null, NumeroPorta, Criacao);

    public async Task ConectarAsync()
    {
        await Chaves.Carregada;
        await _copia.Carregada;
        await _historico.Carregado;
        await Pareamento.Carregado;
        await Sessao.ConectarAsync();
        Assert.Equal(EstadoSessao.Conectado, Sessao.Estado);
        await Esperar.Ate(() => _estado.Pronto && !_estado.Lendo && !_leitura.Lendo && !_copia.EmAndamento
                                && _gerador.Estado == EstadoGerador.Desligado && _setups.Conferido && !_setups.Ocupado,
            TimeSpan.FromSeconds(30));
    }

    public async Task LigarPortaAsync()
    {
        Assert.True((await Chaves.DefinirAsync(Chave.ControlePelaRede, true, OrigemComando.Tela)).Ok);
        await Porta.Inicio;
        await Porta.AtualizarAsync();
        Assert.True(Porta.Aberta, Porta.Erro);
    }

    /// <summary>A porta cai (o servidor fecha sem revogar o cliente).</summary>
    public void DerrubarPorta() => Porta.Dispose();

    /// <summary>
    /// A porta volta (o app reaberto: a contagem dos eventos recomeça), no mesmo número, com o mesmo cliente pareado (o
    /// guardado no celular). <paramref name="antesDeAbrir"/> roda com a porta nova já ouvindo a criação, antes de ela abrir.
    /// </summary>
    public async Task ReabrirPortaAsync(Action? antesDeAbrir = null)
    {
        Pareamento = new Pareamento(_guardadoPorta, null, _log);
        Porta = NovaPorta();
        antesDeAbrir?.Invoke();
        await Porta.Inicio;
        await Porta.AtualizarAsync();
        Assert.True(Porta.Aberta, Porta.Erro);
        Assert.True(Pareamento.Pareado);
    }

    /// <summary>Uma porta livre de localhost.</summary>
    private static int PortaLivre()
    {
        var escuta = new TcpListener(IPAddress.Loopback, 0);
        escuta.Start();
        var porta = ((IPEndPoint)escuta.LocalEndpoint).Port;
        escuta.Stop();
        return porta;
    }

    /// <summary>Nenhum 240, 241 ou 23N chegou ao processador.</summary>
    public void NadaDeMemoriaDoAparelho() =>
        Assert.DoesNotContain(Simulador.Escritas, e => e.StartsWith("*24", StringComparison.Ordinal) || e.StartsWith("*23", StringComparison.Ordinal));

    public async ValueTask DisposeAsync()
    {
        Porta.Dispose();
        Criacao.Dispose();
        if (Sessao.Estado == EstadoSessao.Conectado)
            await Sessao.DesconectarAsync();
    }
}

internal static class Esperar
{
    /// <summary>Espera, no tempo real, a condição ficar verdadeira.</summary>
    public static async Task Ate(Func<bool> condicao, TimeSpan prazo, string? oQue = null)
    {
        var limite = DateTime.UtcNow + prazo;
        while (!condicao())
        {
            if (DateTime.UtcNow > limite)
                Assert.Fail($"A condição não ficou verdadeira em {prazo.TotalSeconds} s{(oQue is null ? "" : $": {oQue}")}.");
            await Task.Delay(50);
        }
    }
}
