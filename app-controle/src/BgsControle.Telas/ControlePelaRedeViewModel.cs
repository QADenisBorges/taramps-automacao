using System.ComponentModel;
using System.Globalization;
using BgsControle.Nucleo.Porta;
using BgsControle.Nucleo.Seguranca;

namespace BgsControle.Telas;

/// <summary>
/// A chave "Controle pela rede" no grupo APARELHO das Configurações (CAP-12, trava 1 e 2; decisão
/// do Denis, 30/10/2026). A linha é a mesma da tela Chaves de acesso (<see cref="Linha"/>), e ligar
/// abre a folha do risco das <see cref="Chaves"/>. Com a chave ligada, embaixo dela: o código de 6
/// dígitos enquanto ninguém pareou; pareado, o nome do cliente e a data; o endereço (IP:8787); e o
/// erro se a porta não abriu. Sem Revogar e sem modo do toque: desligar a chave revoga (o núcleo
/// já faz). Nada aqui menciona a IA.
/// </summary>
public sealed class ControlePelaRedeViewModel : TelaBase, IDisposable
{
    public const string TextoCodigo = "Código para parear";

    private static readonly CultureInfo Br = CultureInfo.GetCultureInfo("pt-BR");

    private readonly PortaComandos _porta;

    private bool _ligada;
    private bool _pareado;
    private string? _codigo;
    private string? _endereco;
    private string? _cliente;
    private string? _pareadoEm;
    private string? _erro;

    public ControlePelaRedeViewModel(PortaComandos porta, ChavesViewModel chaves, IDespachante despachante, TimeProvider relogio)
        : base(despachante, relogio)
    {
        _porta = porta;
        Chaves = chaves;
        Linha = chaves.Item(Chave.ControlePelaRede);
        _porta.Mudou += AoMudar;
        _porta.Pareamento.Mudou += AoMudar;
        Linha.PropertyChanged += AoMudarLinha;
        Atualizar();
    }

    /// <summary>A linha da chave (Lig./Desl.), a mesma das Chaves de acesso.</summary>
    public ItemChaveViewModel Linha { get; }

    /// <summary>As chaves de acesso: a folha do risco ao ligar.</summary>
    public ChavesViewModel Chaves { get; }

    /// <summary>A chave está ligada: o que vem embaixo dela aparece.</summary>
    public bool Ligada
    {
        get => _ligada;
        private set
        {
            if (_ligada == value)
                return;
            _ligada = value;
            Avisar();
        }
    }

    /// <summary>O código de 6 dígitos, enquanto a porta escuta e ninguém pareou; senão, null.</summary>
    public string? Codigo
    {
        get => _codigo;
        private set => Definir(ref _codigo, value);
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

    /// <summary>"192.168.0.5:8787" com a porta aberta, ou null.</summary>
    public string? Endereco
    {
        get => _endereco;
        private set => Definir(ref _endereco, value);
    }

    /// <summary>"Não consegui abrir a porta 8787.", ou null.</summary>
    public string? Erro
    {
        get => _erro;
        private set => Definir(ref _erro, value);
    }

    /// <summary>Um cliente pareado (o nome e a data no lugar do código).</summary>
    public bool Pareado
    {
        get => _pareado;
        private set
        {
            if (_pareado == value)
                return;
            _pareado = value;
            Avisar();
        }
    }

    private void AoMudar() => NaTela(Atualizar);

    private void AoMudarLinha(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ItemChaveViewModel.Ligada))
            Atualizar();
    }

    private void Atualizar()
    {
        Ligada = Linha.Ligada;
        var aberta = _porta.Aberta;
        var pareamento = _porta.Pareamento;
        var pareado = Ligada && pareamento.Pareado;
        Erro = Ligada ? _porta.Erro : null;
        Codigo = Ligada && aberta && !pareado ? pareamento.Codigo : null;
        Endereco = Ligada && aberta ? _porta.Endereco : null;
        Cliente = pareado ? pareamento.Cliente : null;
        PareadoEm = pareado ? pareamento.Desde?.ToString("dd/MM/yyyy HH:mm", Br) : null;
        Pareado = pareado;
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
        Linha.PropertyChanged -= AoMudarLinha;
    }
}
