using System.Globalization;
using System.Text;
using BgsControle.Nucleo;
using BgsControle.Nucleo.Leitura;
using BgsControle.Nucleo.Registro;

namespace BgsControle.Telas;

/// <summary>Uma caixa da senha: "•" ou vazia, e se é a atual.</summary>
public sealed record CaixaSenha(string Texto, bool Atual);

/// <summary>
/// A tela de conexão: estado, firmware, senha, leitura completa e os pedidos ao celular. A
/// leitura automática ao entrar em Conectado é do núcleo (<see cref="LeituraAutomatica"/>);
/// aqui só se observa o resultado e se salva o retrato.
/// </summary>
public sealed class ConexaoViewModel : TelaBase
{
    private const int LinhasDoRegistro = 60;

    /// <summary>O último retrato, nos arquivos privados do app (allowBackup=false).</summary>
    public const string ArquivoRetrato = "ultimo-retrato.json";

    private readonly Sessao _sessao;
    private readonly LogBle _log;
    private readonly LeituraAutomatica _leitura;
    private readonly IArquivosApp _arquivos;
    private readonly IPreparoBluetooth _preparo;
    private string? _mensagemDaTela;
    private string _senhaDigitada = "";
    private bool _registroVisivel;
    private bool _preparando;
    private bool _lendo;
    private int _leituraAtual; // muda a cada leitura: o fim de um salvamento antigo não encerra a nova
    private Retrato? _retrato;
    private string? _avisoLeitura;

    public ConexaoViewModel(
        Sessao sessao,
        LogBle log,
        LeituraAutomatica leitura,
        IDespachante despachante,
        IArquivosApp arquivos,
        IPreparoBluetooth preparo,
        TimeProvider relogio)
        : base(despachante, relogio)
    {
        _sessao = sessao;
        _log = log;
        _leitura = leitura;
        _arquivos = arquivos;
        _preparo = preparo;
        _sessao.Mudou += () => NaTela(() =>
        {
            _mensagemDaTela = null; // a mensagem nova da sessão vale mais
            Atualizar();
        });
        _log.Registrou += _ => NaTela(() => Avisar(nameof(Registro)));

        // A leitura (automática ou "Ler de novo") é do núcleo; a tela só acompanha e salva.
        _leitura.Comecou += () => NaTela(() =>
        {
            _leituraAtual++;
            _lendo = true;
            _retrato = null;
            _avisoLeitura = null;
            AtualizarLeitura();
        });
        _leitura.Avancou += r => NaTela(() =>
        {
            if (!_lendo)
                return;
            _retrato = r;
            AtualizarLeitura();
        });
        _leitura.Terminou += r => NaTela(() =>
        {
            _retrato = r;
            AtualizarLeitura();
            _ = SalvarAsync(r, _leituraAtual);
        });
        _leitura.Falhou += e => NaTela(() =>
        {
            _avisoLeitura = $"A leitura falhou ({e.GetType().Name}).";
            _lendo = false;
            AtualizarLeitura();
        });

        LerDeNovo = new Comando(LerAsync);
        Conectar = new Comando(ConectarAsync);
        EnviarSenha = new Comando(EnviarSenhaAsync);
        Desconectar = new Comando(_sessao.DesconectarAsync);
        EsquecerSenha = new Comando(EsquecerAsync);
        AlternarRegistro = new Comando(() =>
        {
            RegistroVisivel = !RegistroVisivel;
            Avisar(nameof(TextoBotaoRegistro));
        });
    }

    public Comando Conectar { get; }
    public Comando EnviarSenha { get; }
    public Comando Desconectar { get; }
    public Comando EsquecerSenha { get; }
    public Comando AlternarRegistro { get; }
    public Comando LerDeNovo { get; }

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
            Avisar(nameof(Caixas));
        }
    }

    /// <summary>
    /// As 4 caixas da senha (DESIGN, caixas-da-senha): cada caractere vira • na hora, sem "mostrar"; a
    /// caixa atual tem o contorno branco.
    /// </summary>
    public IReadOnlyList<CaixaSenha> Caixas =>
        Enumerable.Range(0, 4).Select(i => new CaixaSenha(i < _senhaDigitada.Length ? "•" : "", i == Math.Min(_senhaDigitada.Length, 3))).ToArray();

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

    /// <summary>"Ler de novo": pede a leitura ao núcleo, que avisa pelos eventos.</summary>
    private async Task LerAsync()
    {
        if (_lendo)
            return;
        await _leitura.LerAsync();
    }

    /// <summary>Guarda o retrato em JSON. A leitura só acaba para a tela depois de salvar.</summary>
    private async Task SalvarAsync(Retrato retrato, int leitura)
    {
        string? aviso = null;
        try
        {
            await _arquivos.GravarTextoAsync(ArquivoRetrato, retrato.ParaJson());
        }
        catch (Exception e)
        {
            aviso = $"Não consegui salvar o retrato ({e.GetType().Name}).";
        }
        NaTela(() =>
        {
            if (leitura != _leituraAtual)
                return; // outra leitura já começou
            if (aviso is not null)
                _avisoLeitura = aviso;
            _lendo = false;
            AtualizarLeitura();
        });
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
            // Pede a permissão e o Bluetooth ligado ao celular, e explica o que falta.
            var falta = await _preparo.PrepararAsync();
            if (falta is not null)
            {
                _mensagemDaTela = falta;
                return;
            }
        }
        finally
        {
            _preparando = false;
            Atualizar();
        }
        await _sessao.ConectarAsync();
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
}
