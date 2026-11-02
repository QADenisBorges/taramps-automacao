using System.Net;
using System.Net.Sockets;
using BgsControle.Nucleo;
using BgsControle.Nucleo.Aparelho;
using BgsControle.Nucleo.Escrita;
using BgsControle.Nucleo.Gerador;
using BgsControle.Nucleo.Leitura;
using BgsControle.Nucleo.Player;
using BgsControle.Nucleo.Porta;
using BgsControle.Nucleo.Presets;
using BgsControle.Nucleo.Registro;
using BgsControle.Nucleo.Seguranca;
using BgsControle.Nucleo.Setups;
using BgsControle.Nucleo.Uso;
using BgsControle.Simulador;

namespace Agente.Avaliacao;

/// <summary>
/// O núcleo do app sobre o <see cref="ProcessadorSimulado"/>, no tempo real, numa porta livre de localhost: os Setups, as
/// memórias e o estado do aparelho dados, conectado, lido, com a chave "Controle pela rede" ligada e a
/// <see cref="PortaComandos"/> com a <see cref="CriacaoMemoria"/>. O lado do usuário vai direto no <see cref="Criacao"/>.
/// É o mesmo que os testes do Agente usam, fora dos testes (o avaliador da história 14f).
/// </summary>
public sealed class NucleoSimulado : IAsyncDisposable
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
    private readonly BaseModelos _modelos;

    /// <param name="setupsJson">O <c>setups.json</c> do celular.</param>
    /// <param name="presetsJson">O <c>presets.json</c>, ou null (nenhum preset).</param>
    /// <param name="modelosJson">O <c>modelos.json</c>, ou null (a base vazia).</param>
    /// <param name="estado">Os valores do aparelho (no fio) que trocam os da semente do simulador, ou null.</param>
    public NucleoSimulado(string setupsJson, string? presetsJson = null, string? modelosJson = null,
        IReadOnlyDictionary<(string Codigo, int? Indice), string>? estado = null)
    {
        Simulador = new ProcessadorSimulado { AtrasoSenha = TimeSpan.Zero, AtrasoLeitura = TimeSpan.Zero, AtrasoEco = TimeSpan.FromMilliseconds(20) };
        if (estado is not null)
        {
            foreach (var ((codigo, indice), noFio) in estado)
                Simulador.Definir(codigo, indice, noFio);
        }
        var cofre = new CofreMemoria(Simulador.Senha);
        Chaves = new Chaves(_repositorioChaves);
        _historico = new Historico(new TextoMemoria(), null, Log);
        _uso = new UsoCompartilhado(null, Log);
        Sessao = new Sessao(Simulador, cofre, Log, Tempos, Chaves, _historico, _uso);
        var leitor = new LeitorCompleto(Sessao, Log, LeituraRapida);
        _leitura = new LeituraAutomatica(Sessao, leitor);
        _estado = new EstadoAparelho(Sessao, _leitura);
        _escritor = new Escritor(Sessao, _estado, Log, Escrita);
        _copia = new CopiaAntes(Sessao, _estado, _escritor, _leitura, new TextoMemoria(), Log);
        var link = new LinkCanais(Sessao, _estado, _escritor, new TextoMemoria(), Log);
        var presets = new GerentePresets(Sessao, _estado, _escritor, _leitura, _copia, new TextoMemoria(presetsJson), Log);
        var playlists = new GerentePlaylists(new TextoMemoria(), Log);
        _modelos = new BaseModelos(new TextoMemoria(modelosJson), Log);
        _setups = new GerenteSetups(Sessao, _estado, _escritor, _leitura, _copia, link, presets, playlists, Chaves,
            new TextoMemoria(setupsJson), new TextoMemoria(), Log, modelos: _modelos);
        Ficha = new PropostaFicha(_setups, Log);
        _gerador = new GeradorSinal(Sessao, _estado, _leitura, _copia, Log, Escrita, new TemposGerador(TimeSpan.FromSeconds(30), TimeSpan.FromMilliseconds(100)));
        _monitor = new MonitorAparelho(Sessao, _leitura);
        _comandos = new ComandosAparelho(Sessao, _estado, _leitura, _copia, cofre, Log, _gerador, Escrita);
        Pareamento = new Pareamento(_guardadoPorta, null, Log);
        _confirmacoes = new PedidosConfirmacao(null, Log);
        Criacao = new CriacaoMemoria(Sessao, _estado, _escritor, _copia, _setups, _uso, Log);
        NumeroPorta = PortaLivre();
        Porta = NovaPorta();
    }

    /// <summary>O log do núcleo (as recusas da cerca aparecem aqui).</summary>
    public LogBle Log { get; } = new();

    public ProcessadorSimulado Simulador { get; }

    public Chaves Chaves { get; }

    public Sessao Sessao { get; }

    public CriacaoMemoria Criacao { get; }

    /// <summary>Os Setups (o Setup em uso, as saídas e os limites).</summary>
    public GerenteSetups Setups => _setups;

    /// <summary>A base de modelos do celular.</summary>
    public BaseModelos Modelos => _modelos;

    /// <summary>A proposta de ficha: a folha do celular.</summary>
    public PropostaFicha Ficha { get; }

    /// <summary>O espelho do aparelho no app.</summary>
    public EstadoAparelho Estado => _estado;

    public Pareamento Pareamento { get; private set; }

    public PortaComandos Porta { get; private set; }

    public int NumeroPorta { get; }

    /// <summary>O endereço que a ferramenta usa para parear.</summary>
    public string Endereco => $"127.0.0.1:{NumeroPorta}";

    /// <summary>Quantas vezes o <see cref="ProntoAsync"/> tenta, cada vez com outra porta livre.</summary>
    public const int TentativasPorta = 3;

    /// <summary>
    /// Conectado, lido, com o Setup conferido e a porta aberta. A porta livre é solta antes de a porta de comandos a ocupar
    /// (outro núcleo em paralelo pode pegá-la): se ela não abre, tenta de novo com outra, até <see cref="TentativasPorta"/> vezes.
    /// </summary>
    public static async Task<NucleoSimulado> ProntoAsync(string setupsJson, IReadOnlyDictionary<(string Codigo, int? Indice), string>? estado = null)
    {
        for (var tentativa = 1; ; tentativa++)
        {
            var n = new NucleoSimulado(setupsJson, estado: estado);
            try
            {
                await n.ConectarAsync();
                await n.LigarPortaAsync();
                return n;
            }
            catch (PortaNaoAbriuException) when (tentativa < TentativasPorta)
            {
                await n.DisposeAsync();
            }
            catch
            {
                await n.DisposeAsync();
                throw;
            }
        }
    }

    private PortaComandos NovaPorta() =>
        new(Sessao, Chaves, _uso, _historico, Pareamento, _confirmacoes, new RedeLocalhost(), _estado, _escritor, _setups, _gerador,
            _comandos, _monitor, Log, null, NumeroPorta, Criacao, ficha: Ficha);

    public async Task ConectarAsync()
    {
        await Chaves.Carregada;
        await _copia.Carregada;
        await _historico.Carregado;
        await Pareamento.Carregado;
        await _modelos.Carregada;
        await Sessao.ConectarAsync();
        if (Sessao.Estado != EstadoSessao.Conectado)
            throw new InvalidOperationException($"O simulador não conectou ({Sessao.Estado}).");
        await Esperar.Ate(() => _estado.Pronto && !_estado.Lendo && !_leitura.Lendo && !_copia.EmAndamento
                                && _gerador.Estado == EstadoGerador.Desligado && _setups.Conferido && !_setups.Ocupado,
            TimeSpan.FromSeconds(30), "a leitura e a conferência do Setup");
    }

    public async Task LigarPortaAsync()
    {
        var ligou = await Chaves.DefinirAsync(Chave.ControlePelaRede, true, OrigemComando.Tela);
        if (!ligou.Ok)
            throw new InvalidOperationException($"A chave Controle pela rede não ligou ({ligou.Motivo}).");
        await Porta.Inicio;
        await Porta.AtualizarAsync();
        if (!Porta.Aberta)
            throw new PortaNaoAbriuException($"A porta não abriu ({Porta.Erro}).");
    }

    /// <summary>A porta cai (o servidor fecha sem revogar o cliente).</summary>
    public void DerrubarPorta() => Porta.Dispose();

    /// <summary>
    /// A porta volta (o app reaberto: a contagem dos eventos recomeça), no mesmo número, com o mesmo cliente pareado (o
    /// guardado no celular). <paramref name="antesDeAbrir"/> roda com a porta nova já ouvindo a criação, antes de ela abrir.
    /// </summary>
    public async Task ReabrirPortaAsync(Action? antesDeAbrir = null)
    {
        Pareamento = new Pareamento(_guardadoPorta, null, Log);
        Porta = NovaPorta();
        antesDeAbrir?.Invoke();
        await Porta.Inicio;
        await Porta.AtualizarAsync();
        if (!Porta.Aberta)
            throw new InvalidOperationException($"A porta não reabriu ({Porta.Erro}).");
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

    public async ValueTask DisposeAsync()
    {
        Porta.Dispose();
        Ficha.Dispose();
        Criacao.Dispose();
        if (Sessao.Estado == EstadoSessao.Conectado)
            await Sessao.DesconectarAsync();
    }
}

/// <summary>A porta de comandos não abriu no número escolhido (outro processo o pegou, por exemplo).</summary>
public sealed class PortaNaoAbriuException(string mensagem) : InvalidOperationException(mensagem);

/// <summary>Esperar uma condição no tempo real.</summary>
public static class Esperar
{
    /// <summary>Espera a condição ficar verdadeira; senão, <see cref="TimeoutException"/>.</summary>
    public static async Task Ate(Func<bool> condicao, TimeSpan prazo, string? oQue = null, CancellationToken cancelar = default)
    {
        var limite = DateTime.UtcNow + prazo;
        while (!condicao())
        {
            if (DateTime.UtcNow > limite)
                throw new TimeoutException($"A condição não ficou verdadeira em {prazo.TotalSeconds} s{(oQue is null ? "" : $": {oQue}")}.");
            await Task.Delay(50, cancelar);
        }
    }
}
