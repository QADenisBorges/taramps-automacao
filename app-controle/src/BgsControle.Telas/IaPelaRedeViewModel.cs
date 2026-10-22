using System.Globalization;
using BgsControle.Nucleo.Porta;
using BgsControle.Nucleo.Seguranca;
using BgsControle.Nucleo.Uso;

namespace BgsControle.Telas;

/// <summary>Em que pé está a porta da IA, para a tela.</summary>
public enum EstadoIaPelaRede
{
    /// <summary>A chave "IA pela rede" está desligada: nada escuta.</summary>
    Fechada,

    /// <summary>A porta escuta e ninguém pareou: a tela mostra o código.</summary>
    Pareando,

    /// <summary>Um cliente pareado (nome e data), com o Revogar.</summary>
    Pareado,

    /// <summary>A porta não abriu ("Não consegui abrir a porta 8787.").</summary>
    Erro,
}

/// <summary>
/// A tela IA pela rede (CAP-12): o estado da porta, o código de 6 dígitos para parear, o endereço
/// (IP:8787), o cliente pareado e a data, o Revogar (PARAR IA e o cliente apagado), o seletor do
/// modo do toque ("Travar até PARAR IA" ou "Pausar a IA") e o aviso "A IA perdeu a conexão.".
/// A chave "IA pela rede" continua na tela Chaves de acesso.
/// </summary>
public sealed class IaPelaRedeViewModel : TelaBase, IDisposable
{
    public const string TextoTravar = "Travar até PARAR IA";
    public const string TextoPausar = "Pausar a IA";
    public const string TextoRevogar = "Revogar";
    public const string MsgFechada = "Ligue a chave \"IA pela rede\" em Chaves de acesso para abrir a porta.";
    public const string MsgPareando = "Na IA, peça para parear e digite este código.";

    private static readonly CultureInfo Br = CultureInfo.GetCultureInfo("pt-BR");

    private readonly PortaComandos _porta;
    private readonly UsoCompartilhado _uso;

    private EstadoIaPelaRede _estado;
    private string? _codigo;
    private string? _endereco;
    private string? _cliente;
    private string? _pareadoEm;
    private string? _erro;
    private string? _avisoConexao;
    private ModoToque _modo;
    private string? _mensagem;

    public IaPelaRedeViewModel(PortaComandos porta, UsoCompartilhado uso, IDespachante despachante, TimeProvider relogio)
        : base(despachante, relogio)
    {
        _porta = porta;
        _uso = uso;
        Revogar = new Comando(RevogarAsync);
        TravarAtePararIa = new Comando(() => DefinirModoAsync(ModoToque.TravarAtePararIa));
        PausarIa = new Comando(() => DefinirModoAsync(ModoToque.PausarIa));
        _porta.Mudou += AoMudar;
        _porta.Pareamento.Mudou += AoMudar;
        _uso.Mudou += AoMudar;
        Atualizar();
    }

    public EstadoIaPelaRede Estado
    {
        get => _estado;
        private set
        {
            if (_estado == value)
                return;
            _estado = value;
            Avisar();
            Avisar(nameof(TextoEstado));
        }
    }

    /// <summary>Uma linha para o estado.</summary>
    public string TextoEstado => Estado switch
    {
        EstadoIaPelaRede.Pareando => MsgPareando,
        EstadoIaPelaRede.Pareado => "Cliente pareado.",
        EstadoIaPelaRede.Erro => _erro ?? "",
        _ => MsgFechada,
    };

    /// <summary>O código de 6 dígitos (só no Pareando), ou null.</summary>
    public string? Codigo
    {
        get => _codigo;
        private set => Definir(ref _codigo, value);
    }

    /// <summary>"192.168.0.5:8787" com a porta aberta, ou null.</summary>
    public string? Endereco
    {
        get => _endereco;
        private set => Definir(ref _endereco, value);
    }

    /// <summary>O nome que o cliente deu ao parear, ou null.</summary>
    public string? Cliente
    {
        get => _cliente;
        private set => Definir(ref _cliente, value);
    }

    /// <summary>"22/10/2026 14:05", quando o cliente pareou, ou null.</summary>
    public string? PareadoEm
    {
        get => _pareadoEm;
        private set => Definir(ref _pareadoEm, value);
    }

    /// <summary>"A IA perdeu a conexão.", ou null.</summary>
    public string? AvisoConexao
    {
        get => _avisoConexao;
        private set => Definir(ref _avisoConexao, value);
    }

    /// <summary>O modo do toque enquanto a IA trabalha.</summary>
    public ModoToque Modo
    {
        get => _modo;
        private set
        {
            if (_modo == value)
                return;
            _modo = value;
            Avisar();
            Avisar(nameof(Travar));
            Avisar(nameof(Pausar));
        }
    }

    /// <summary>"Travar até PARAR IA" está escolhido.</summary>
    public bool Travar => Modo == ModoToque.TravarAtePararIa;

    /// <summary>"Pausar a IA" está escolhido.</summary>
    public bool Pausar => Modo == ModoToque.PausarIa;

    /// <summary>Uma linha de aviso (o modo não foi guardado, por exemplo), ou null.</summary>
    public string? Mensagem
    {
        get => _mensagem;
        private set => Definir(ref _mensagem, value);
    }

    /// <summary>Só com um cliente pareado.</summary>
    public bool PodeRevogar => Estado == EstadoIaPelaRede.Pareado;

    /// <summary>Revogar: PARAR IA e o cliente apagado; nasce um código novo.</summary>
    public Comando Revogar { get; }

    public Comando TravarAtePararIa { get; }

    public Comando PausarIa { get; }

    private async Task RevogarAsync()
    {
        if (!PodeRevogar)
            return; // sem cliente: nada a revogar (nem PARAR IA, nem código novo)
        await _porta.RevogarAsync();
        NaTela(Atualizar);
    }

    private async Task DefinirModoAsync(ModoToque modo)
    {
        var r = await _uso.DefinirModoAsync(modo, OrigemComando.Tela);
        NaTela(() =>
        {
            Mensagem = r.Motivo;
            Atualizar();
        });
    }

    private void AoMudar() => NaTela(Atualizar);

    private void Atualizar()
    {
        _erro = _porta.Erro;
        var pareamento = _porta.Pareamento;
        Estado = _erro is not null ? EstadoIaPelaRede.Erro
            : !_porta.Aberta ? EstadoIaPelaRede.Fechada
            : pareamento.Pareado ? EstadoIaPelaRede.Pareado
            : EstadoIaPelaRede.Pareando;
        Avisar(nameof(TextoEstado));
        Avisar(nameof(PodeRevogar));
        Codigo = Estado == EstadoIaPelaRede.Pareando ? pareamento.Codigo : null;
        Endereco = Estado is EstadoIaPelaRede.Pareando or EstadoIaPelaRede.Pareado ? _porta.Endereco : null;
        Cliente = pareamento.Cliente;
        PareadoEm = pareamento.Desde?.ToString("dd/MM/yyyy HH:mm", Br);
        AvisoConexao = _porta.AvisoConexao;
        Modo = _uso.Modo;
    }

    private void Definir(ref string? campo, string? valor, [System.Runtime.CompilerServices.CallerMemberName] string? nome = null)
    {
        if (campo == valor)
            return;
        campo = valor;
        Avisar(nome);
    }

    public void Dispose()
    {
        _porta.Mudou -= AoMudar;
        _porta.Pareamento.Mudou -= AoMudar;
        _uso.Mudou -= AoMudar;
    }
}
