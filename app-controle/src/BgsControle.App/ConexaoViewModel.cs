using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using BgsControle.App.Plataforma;
using BgsControle.Nucleo;
using BgsControle.Nucleo.Registro;

namespace BgsControle.App;

/// <summary>A tela de conexão: estado, firmware, senha e os pedidos ao Android.</summary>
public sealed class ConexaoViewModel : INotifyPropertyChanged
{
    private const int LinhasDoRegistro = 60;

    private readonly Sessao _sessao;
    private readonly LogBle _log;
    private string? _mensagemDaTela;
    private string _senhaDigitada = "";
    private bool _registroVisivel;
    private bool _preparando;

    public ConexaoViewModel(Sessao sessao, LogBle log)
    {
        _sessao = sessao;
        _log = log;
        _sessao.Mudou += () => MainThread.BeginInvokeOnMainThread(() =>
        {
            _mensagemDaTela = null; // a mensagem nova da sessão vale mais
            Atualizar();
        });
        _log.Registrou += _ => MainThread.BeginInvokeOnMainThread(() => Avisar(nameof(Registro)));

        Conectar = new Command(async () => await ConectarAsync());
        EnviarSenha = new Command(async () => await EnviarSenhaAsync());
        Desconectar = new Command(async () => await _sessao.DesconectarAsync());
        EsquecerSenha = new Command(async () => await EsquecerAsync());
        AlternarRegistro = new Command(() =>
        {
            RegistroVisivel = !RegistroVisivel;
            Avisar(nameof(TextoBotaoRegistro));
        });
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ICommand Conectar { get; }
    public ICommand EnviarSenha { get; }
    public ICommand Desconectar { get; }
    public ICommand EsquecerSenha { get; }
    public ICommand AlternarRegistro { get; }

    public string Resumo => _preparando ? "Preparando o Bluetooth…" : _sessao.Resumo;
    public string? Firmware => _sessao.Firmware;
    public bool SoLeitura => _sessao.Estado == EstadoSessao.Conectado && _sessao.SoLeitura;
    public string? Aviso => _sessao.Aviso;
    public string? Mensagem => _mensagemDaTela ?? _sessao.Mensagem;
    public bool TemMensagem => !string.IsNullOrEmpty(Mensagem);

    public bool PedindoSenha => _sessao.Estado == EstadoSessao.PedindoSenha;
    public bool PodeConectar => !_preparando && _sessao.Estado == EstadoSessao.Desconectado;
    public bool PodeDesconectar => _sessao.Estado != EstadoSessao.Desconectado;
    public string TextoDesconectar => _sessao.SegurandoConexao ? "Desconectar" : "Cancelar";
    public bool Ocupado => _preparando || _sessao.Estado is EstadoSessao.Procurando or EstadoSessao.Conectando or EstadoSessao.Autenticando;

    /// <summary>O texto do campo mascarado. É limpo logo depois de enviado.</summary>
    public string SenhaDigitada
    {
        get => _senhaDigitada;
        set
        {
            if (_senhaDigitada == value)
                return;
            _senhaDigitada = value;
            Avisar();
        }
    }

    public bool RegistroVisivel
    {
        get => _registroVisivel;
        private set
        {
            _registroVisivel = value;
            Avisar();
        }
    }

    public string TextoBotaoRegistro => RegistroVisivel ? "Esconder o registro" : "Mostrar o registro";

    /// <summary>As últimas linhas do log do BLE, já com a senha mascarada.</summary>
    public string Registro => string.Join("\n", _log.Linhas.TakeLast(LinhasDoRegistro));

    private async Task ConectarAsync()
    {
        _mensagemDaTela = null;
        _preparando = true;
        Atualizar();
        try
        {
            if (!await PrepararBluetoothAsync())
                return;
        }
        finally
        {
            _preparando = false;
            Atualizar();
        }
        await _sessao.ConectarAsync();
    }

    /// <summary>Pede a permissão e o Bluetooth ligado ao Android. Explica o que falta.</summary>
    private async Task<bool> PrepararBluetoothAsync()
    {
        var status = await Permissions.CheckStatusAsync<PermissaoBluetooth>();
        if (status != PermissionStatus.Granted)
            status = await Permissions.RequestAsync<PermissaoBluetooth>();
        if (status != PermissionStatus.Granted)
        {
            _mensagemDaTela = Sessao.MsgSemPermissao;
            return false;
        }

        var contexto = Android.App.Application.Context;
        if (!PedidoLigarBluetooth.Ligado(contexto) && !await PedidoLigarBluetooth.PedirAsync())
        {
            _mensagemDaTela = Sessao.MsgBluetoothDesligado;
            return false;
        }
        return true;
    }

    private async Task EnviarSenhaAsync()
    {
        var digitada = SenhaDigitada;
        SenhaDigitada = "";
        _mensagemDaTela = null;
        await _sessao.InformarSenhaAsync(digitada);
    }

    private async Task EsquecerAsync()
    {
        _mensagemDaTela = null;
        SenhaDigitada = "";
        await _sessao.EsquecerSenhaAsync();
    }

    private void Atualizar()
    {
        Avisar(nameof(Resumo));
        Avisar(nameof(Firmware));
        Avisar(nameof(SoLeitura));
        Avisar(nameof(Aviso));
        Avisar(nameof(Mensagem));
        Avisar(nameof(TemMensagem));
        Avisar(nameof(PedindoSenha));
        Avisar(nameof(PodeConectar));
        Avisar(nameof(PodeDesconectar));
        Avisar(nameof(TextoDesconectar));
        Avisar(nameof(Ocupado));
    }

    private void Avisar([CallerMemberName] string? nome = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nome));
}
