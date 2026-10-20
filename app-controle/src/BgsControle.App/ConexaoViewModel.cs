using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using BgsControle.App.Plataforma;
using System.Globalization;
using System.Text;
using BgsControle.Nucleo;
using BgsControle.Nucleo.Leitura;
using BgsControle.Nucleo.Registro;

namespace BgsControle.App;

/// <summary>A tela de conexão: estado, firmware, senha, leitura completa e os pedidos ao Android.</summary>
public sealed class ConexaoViewModel : INotifyPropertyChanged
{
    private const int LinhasDoRegistro = 60;

    /// <summary>O último retrato, nos arquivos privados do app (allowBackup=false).</summary>
    public const string ArquivoRetrato = "ultimo-retrato.json";

    private readonly Sessao _sessao;
    private readonly LogBle _log;
    private readonly LeitorCompleto _leitor;
    private string? _mensagemDaTela;
    private string _senhaDigitada = "";
    private bool _registroVisivel;
    private bool _preparando;
    private bool _conectadoAntes;
    private bool _lendo;
    private Retrato? _retrato;
    private string? _avisoLeitura;

    public ConexaoViewModel(Sessao sessao, LogBle log, LeitorCompleto leitor)
    {
        _sessao = sessao;
        _log = log;
        _leitor = leitor;
        _sessao.Mudou += () => MainThread.BeginInvokeOnMainThread(() =>
        {
            _mensagemDaTela = null; // a mensagem nova da sessão vale mais
            Atualizar();
            // A leitura completa roda sozinha logo depois de a senha ser aceita.
            var conectado = _sessao.Estado == EstadoSessao.Conectado;
            if (conectado && !_conectadoAntes)
                _ = LerAsync();
            _conectadoAntes = conectado;
        });
        _log.Registrou += _ => MainThread.BeginInvokeOnMainThread(() => Avisar(nameof(Registro)));
        _leitor.Avancou += r => MainThread.BeginInvokeOnMainThread(() =>
        {
            if (!_lendo)
                return;
            _retrato = r;
            AtualizarLeitura();
        });

        LerDeNovo = new Command(async () => await LerAsync());
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
    public ICommand LerDeNovo { get; }

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

    // ------------------------------------------------------------------ leitura completa

    public bool MostrarLeitura => _lendo || _retrato is not null;
    public bool LendoAgora => _lendo;
    public bool PodeLerDeNovo => !_lendo && _sessao.Estado == EstadoSessao.Conectado;

    /// <summary>O placar do Marco 1: "84 de 84".</summary>
    public string PlacarMarco1 => _retrato?.PlacarMarco1.Texto ?? $"0 de {Catalogo.TotalMarco1}";

    public string PlacarExtras => $"Extras: {_retrato?.PlacarExtras.Texto ?? $"0 de {Catalogo.TotalExtras}"}";

    public string ResumoLeitura
    {
        get
        {
            var r = _retrato;
            string texto;
            if (_lendo || r is null)
                texto = $"Lendo… {r?.Feitas ?? 0} de {Catalogo.Consultas.Count} consultas";
            else if (r.Completo)
                texto = $"Leitura completa. Marco 1 em {Segundos(r.DuracaoMarco1)}, tudo em {Segundos(r.Duracao)}.";
            else
                texto = $"Leitura incompleta: {r.Motivo}";
            return _avisoLeitura is null ? texto : $"{texto}\n{_avisoLeitura}";
        }
    }

    /// <summary>A lista simples de texto: código, saída e valor; extras com o texto cru.</summary>
    public string ListaLeitura
    {
        get
        {
            var r = _retrato;
            if (r is null)
                return "";
            var sb = new StringBuilder();
            sb.AppendLine($"MARCO 1 · {r.PlacarMarco1.Texto}");
            foreach (var l in r.Lidos.Where(l => !l.Extra))
                sb.AppendLine(l.Linha);
            var falhasMarco1 = r.Falhas.Where(f => !f.Extra).ToList();
            if (falhasMarco1.Count > 0)
            {
                sb.AppendLine().AppendLine($"FALHAS · {falhasMarco1.Count}");
                foreach (var f in falhasMarco1)
                    sb.AppendLine(f.Linha);
            }
            if (r.Lidos.Any(l => l.Extra) || r.Falhas.Any(f => f.Extra))
            {
                sb.AppendLine().AppendLine($"EXTRAS · {r.PlacarExtras.Texto} (com o texto cru)");
                foreach (var l in r.Lidos.Where(l => l.Extra))
                    sb.AppendLine($"{l.Linha} · {l.Cru}");
                foreach (var f in r.Falhas.Where(f => f.Extra))
                    sb.AppendLine(f.Linha);
            }
            if (r.Orfaos.Count > 0)
            {
                sb.AppendLine().AppendLine($"ÓRFÃOS · {r.Orfaos.Count}");
                foreach (var o in r.Orfaos)
                    sb.AppendLine(o);
            }
            return sb.ToString().TrimEnd();
        }
    }

    private static string Segundos(TimeSpan? t) =>
        t is { } d ? $"{d.TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture).Replace('.', ',')} s" : "—";

    /// <summary>Lê o aparelho inteiro e guarda o retrato em JSON.</summary>
    private async Task LerAsync()
    {
        if (_lendo || _leitor.Lendo || _sessao.Estado != EstadoSessao.Conectado)
            return;
        _lendo = true;
        _retrato = null;
        _avisoLeitura = null;
        AtualizarLeitura();
        try
        {
            // Fora da thread da tela: as respostas chegam pelas notificações.
            var retrato = await Task.Run(() => _leitor.LerAsync());
            _retrato = retrato;
            try
            {
                await File.WriteAllTextAsync(Path.Combine(FileSystem.AppDataDirectory, ArquivoRetrato), retrato.ParaJson());
            }
            catch (Exception e)
            {
                _avisoLeitura = $"Não consegui salvar o retrato ({e.GetType().Name}).";
            }
        }
        catch (Exception e)
        {
            _avisoLeitura = $"A leitura falhou ({e.GetType().Name}).";
        }
        finally
        {
            _lendo = false;
            AtualizarLeitura();
        }
    }

    private void AtualizarLeitura()
    {
        Avisar(nameof(MostrarLeitura));
        Avisar(nameof(LendoAgora));
        Avisar(nameof(PodeLerDeNovo));
        Avisar(nameof(PlacarMarco1));
        Avisar(nameof(PlacarExtras));
        Avisar(nameof(ResumoLeitura));
        Avisar(nameof(ListaLeitura));
    }

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
        Avisar(nameof(PodeLerDeNovo));
    }

    private void Avisar([CallerMemberName] string? nome = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nome));
}
