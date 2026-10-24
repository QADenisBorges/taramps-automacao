using BgsControle.Nucleo;
using BgsControle.Nucleo.Escrita;
using BgsControle.Nucleo.Gerador;
using BgsControle.Nucleo.Memorias;
using BgsControle.Nucleo.Seguranca;

namespace BgsControle.Telas;

/// <summary>
/// A tela Gerador (bloco Gerador da grade Áudio): tom fixo e sweep, com prazo de 30 s (sem
/// XAML: é da história 11).
/// <list type="bullet">
/// <item>Os campos do tom (frequência, ganho) e do sweep (inicial, final, ganho, velocidade)
/// vêm semeados pelo espelho; o que o Denis escreve fica até ele tocar.</item>
/// <item>Tocar passa pelo <see cref="GeradorSinal"/>; travado (sem conexão, só leitura, chave
/// desligada, lendo, operação de memória, gerador em uso), nada sai e o motivo aparece. Fora da
/// faixa, nada sai e a faixa aparece.</item>
/// <item>Tocando: a contagem regressiva em segundos. O Parar nunca fica travado.</item>
/// <item>A mensagem do gerador ("O prazo de 30 s acabou…", "O gerador estava ligado…") e o
/// alarme "DESLIGUE O MÓDULO AGORA".</item>
/// </list>
/// </summary>
public sealed class GeradorViewModel : TelaBase, IDisposable
{
    public const string MsgEmUso = "Gerador em uso";
    public const string TextoAlarme = GeradorSinal.MsgAlarme;

    /// <summary>De quanto em quanto a contagem regressiva se atualiza.</summary>
    public static readonly TimeSpan PassoContagem = TimeSpan.FromMilliseconds(250);

    private static readonly Parametro GanhoTomP = Catalogo.ParametroDe(Catalogo.CodigoGeradorGanho)!;
    private static readonly Parametro FrequenciaTomP = Catalogo.ParametroDe(Catalogo.CodigoGeradorFrequencia)!;
    private static readonly Parametro InicioSweepP = Catalogo.ParametroDe(Catalogo.CodigoSweepInicio)!;
    private static readonly Parametro FimSweepP = Catalogo.ParametroDe(Catalogo.CodigoSweepFim)!;
    private static readonly Parametro GanhoSweepP = Catalogo.ParametroDe(Catalogo.CodigoSweepGanho)!;
    private static readonly Parametro VelocidadeP = Catalogo.ParametroDe(Catalogo.CodigoSweepVelocidade)!;

    /// <summary>A velocidade quando o espelho diz 0 (tom fixo): médio.</summary>
    public const int VelocidadePadrao = 2;

    private readonly GeradorSinal _gerador;
    private readonly Sessao _sessao;
    private readonly IChaves _chaves;
    private readonly EstadoAparelho _estado;
    private readonly CopiaAntes _copia;
    private readonly GerenteMemorias? _memorias;
    private readonly Dictionary<string, string> _campos = [];
    private readonly HashSet<string> _escritos = [];

    private int _velocidade = VelocidadePadrao;
    private bool _velocidadeEscolhida;
    private bool _modoSweep;
    private string? _mensagem;
    private string? _ultimaDoGerador;
    private string? _erroCampo;
    private bool _agindo;
    private ITimer? _contagem;
    private bool _descartado;

    public GeradorViewModel(
        GeradorSinal gerador,
        Sessao sessao,
        IChaves chaves,
        EstadoAparelho estado,
        CopiaAntes copia,
        IDespachante despachante,
        TimeProvider relogio,
        GerenteMemorias? memorias = null)
        : base(despachante, relogio)
    {
        _gerador = gerador;
        _sessao = sessao;
        _chaves = chaves;
        _estado = estado;
        _copia = copia;
        _memorias = memorias;
        Velocidades = VelocidadeP.Opcoes!.Where(o => o.Key > 0).Select(o => new OpcaoValor(o.Value, o.Key, null)).ToList();

        Tocar = new Comando(TocarAsync);
        Parar = new Comando(PararAsync);
        UsarTom = new Comando(() => ModoSweep = false);
        UsarSweep = new Comando(() => ModoSweep = true);
        UsarLento = new Comando(() => VelocidadeSweep = 1);
        UsarMedio = new Comando(() => VelocidadeSweep = 2);
        UsarRapido = new Comando(() => VelocidadeSweep = 3);

        _ultimaDoGerador = _gerador.Mensagem;
        _gerador.Mudou += AoMudarAlgo;
        _sessao.Mudou += AoMudarAlgo;
        _chaves.Mudou += AoMudarChave;
        _copia.Mudou += AoMudarAlgo;
        if (_memorias is not null)
            _memorias.Mudou += AoMudarAlgo;
        _estado.Recarregou += AoRecarregar;
        _estado.Mudou += AoMudarValor;
        Semear();
        Atualizar();
    }

    // ------------------------------------------------------------------ campos

    /// <summary>Sweep (true) ou tom fixo (false).</summary>
    public bool ModoSweep
    {
        get => _modoSweep;
        set
        {
            if (_modoSweep == value)
                return;
            _modoSweep = value;
            ErroCampo = null;
            Avisar();
            Avisar(nameof(ModoTom));
        }
    }

    /// <summary>Tom fixo (o par de botões "Tom fixo · Sweep").</summary>
    public bool ModoTom => !_modoSweep;

    /// <summary>Escolhe o tom fixo.</summary>
    public Comando UsarTom { get; }

    /// <summary>Escolhe o sweep.</summary>
    public Comando UsarSweep { get; }

    /// <summary>A velocidade do sweep escolhida: lento, médio ou rápido.</summary>
    public bool Lento => _velocidade == 1;

    public bool Medio => _velocidade == 2;

    public bool Rapido => _velocidade == 3;

    public Comando UsarLento { get; }

    public Comando UsarMedio { get; }

    public Comando UsarRapido { get; }

    private void AvisarVelocidade()
    {
        Avisar(nameof(Lento));
        Avisar(nameof(Medio));
        Avisar(nameof(Rapido));
    }

    /// <summary>Frequência do tom fixo (201), como escrita no campo.</summary>
    public string FrequenciaTom
    {
        get => Campo(FrequenciaTomP);
        set => Escrever(FrequenciaTomP, value);
    }

    /// <summary>Ganho do tom fixo (200).</summary>
    public string GanhoTom
    {
        get => Campo(GanhoTomP);
        set => Escrever(GanhoTomP, value);
    }

    /// <summary>Frequência inicial do sweep (210).</summary>
    public string InicioSweep
    {
        get => Campo(InicioSweepP);
        set => Escrever(InicioSweepP, value);
    }

    /// <summary>Frequência final do sweep (211).</summary>
    public string FimSweep
    {
        get => Campo(FimSweepP);
        set => Escrever(FimSweepP, value);
    }

    /// <summary>Ganho do sweep (212).</summary>
    public string GanhoSweep
    {
        get => Campo(GanhoSweepP);
        set => Escrever(GanhoSweepP, value);
    }

    /// <summary>Velocidade do sweep (213): 1 lento, 2 médio, 3 rápido.</summary>
    public int VelocidadeSweep
    {
        get => _velocidade;
        set
        {
            if (value is < 1 or > 3 || _velocidade == value)
                return;
            _velocidade = value;
            _velocidadeEscolhida = true;
            Avisar();
            AvisarVelocidade();
        }
    }

    /// <summary>As velocidades do sweep (lento, médio, rápido).</summary>
    public IReadOnlyList<OpcaoValor> Velocidades { get; }

    /// <summary>"10 a 22000 Hz".</summary>
    public string FaixaFrequencia => FormatoValor.Faixa(FrequenciaTomP);

    /// <summary>"−60,0 a 0,0 dB".</summary>
    public string FaixaGanho => FormatoValor.Faixa(GanhoTomP);

    /// <summary>Um campo fora da faixa ou que não é número ("Fora da faixa: −60,0 a 0,0 dB"), ou null.</summary>
    public string? ErroCampo
    {
        get => _erroCampo;
        private set
        {
            if (_erroCampo == value)
                return;
            _erroCampo = value;
            Avisar();
        }
    }

    // ------------------------------------------------------------------ estado

    public Comando Tocar { get; }

    /// <summary>Desliga na hora. Nunca fica travado.</summary>
    public Comando Parar { get; }

    /// <summary>Por que Tocar está travado ("Chave desligada", "Sem conexão", "Modo só leitura"…), ou null.</summary>
    public string? MotivoTrava
    {
        get
        {
            if (_sessao.Estado != EstadoSessao.Conectado || !_sessao.SegurandoConexao)
                return ControleParametroViewModel.MsgSemConexao;
            if (_sessao.SoLeitura)
                return ControleParametroViewModel.MsgSoLeitura;
            if (!_chaves.Ligada(Chave.Gerador))
                return ControleParametroViewModel.MsgChaveDesligada;
            if (_estado.Lendo)
                return ControleParametroViewModel.MsgLendo;
            if (!_estado.Pronto)
                return ControleParametroViewModel.MsgNaoLeu;
            if (_copia.MotivoBloqueio() is not null || _memorias?.Ocupado == true)
                return MemoriasViewModel.MsgEmAndamento;
            if (_agindo || _gerador.Estado != EstadoGerador.Desligado || _gerador.TalvezLigado)
                return MsgEmUso;
            return null;
        }
    }

    public bool PodeTocar => MotivoTrava is null;

    /// <summary>Sempre: desligar é o lado seguro.</summary>
    public bool PodeParar => true;

    public EstadoGerador Estado => _gerador.Estado;

    public bool Tocando => _gerador.Estado == EstadoGerador.Tocando;

    /// <summary>Os segundos que faltam para o gerador desligar sozinho (tocando), ou null.</summary>
    public int? Contagem => _gerador.Restante is { } falta ? (int)Math.Ceiling(falta.TotalSeconds) : null;

    /// <summary>"30 s" (tocando), ou null.</summary>
    public string? TextoContagem => Contagem is { } s ? $"{s} s" : null;

    /// <summary>A última frase: "O prazo de 30 s acabou: o gerador foi desligado.", "Chave desligada"…</summary>
    public string? Mensagem
    {
        get => _mensagem;
        private set
        {
            if (_mensagem == value)
                return;
            _mensagem = value;
            Avisar();
        }
    }

    /// <summary>O alarme em destaque, por cima de tudo, até uma leitura mostrar 202 = 0.</summary>
    public bool Alarme => _gerador.Alarme;

    /// <summary>"DESLIGUE O MÓDULO AGORA" com o alarme, ou null.</summary>
    public string? TextoDoAlarme => Alarme ? TextoAlarme : null;

    // ------------------------------------------------------------------ ações

    private async Task TocarAsync()
    {
        if (MotivoTrava is { } motivo)
        {
            Mensagem = motivo;
            return;
        }

        Task<ResultadoGerador> tarefa;
        if (ModoSweep)
        {
            var e1 = Ler(InicioSweepP, out var inicio);
            var e2 = Ler(FimSweepP, out var fim);
            var e3 = Ler(GanhoSweepP, out var ganho);
            if ((e1 ?? e2 ?? e3) is { } erro)
            {
                Recusar(erro);
                return;
            }
            tarefa = _gerador.TocarSweepAsync((int)inicio, (int)fim, ganho, VelocidadeSweep);
        }
        else
        {
            var e1 = Ler(FrequenciaTomP, out var hz);
            var e2 = Ler(GanhoTomP, out var db);
            if ((e1 ?? e2) is { } erro)
            {
                Recusar(erro);
                return;
            }
            tarefa = _gerador.TocarTomAsync((int)hz, db);
        }

        ErroCampo = null;
        _agindo = true;
        Atualizar();
        ResultadoGerador r;
        try
        {
            r = await tarefa;
        }
        catch (Exception e)
        {
            r = ResultadoGerador.Falha($"Erro inesperado ({e.GetType().Name}).");
        }
        await NaTelaAsync(() =>
        {
            _agindo = false;
            _ultimaDoGerador = _gerador.Mensagem;
            Mensagem = r.Mensagem ?? r.Motivo;
            Atualizar();
        });
    }

    private async Task PararAsync()
    {
        ResultadoGerador r;
        try
        {
            r = await _gerador.PararAsync();
        }
        catch (Exception e)
        {
            r = ResultadoGerador.Falha($"Erro inesperado ({e.GetType().Name}).");
        }
        await NaTelaAsync(() =>
        {
            _ultimaDoGerador = _gerador.Mensagem;
            Mensagem = _gerador.Alarme ? GeradorSinal.MsgAlarme : _gerador.Mensagem ?? r.Mensagem;
            Atualizar();
        });
    }

    /// <summary>O número do campo, na faixa e com as casas do fio; senão, a frase da recusa.</summary>
    private string? Ler(Parametro p, out decimal valor)
    {
        valor = 0;
        if (FormatoValor.Ler(Campo(p)) is not { } lido)
            return ControleParametroViewModel.MsgNaoNumero;
        if ((p.Minimo is { } min && lido < min) || (p.Maximo is { } max && lido > max))
            return ControleParametroViewModel.PrefixoForaDaFaixa + FormatoValor.Faixa(p);
        if (decimal.Round(lido, p.Casas) != lido)
            return p.Casas == 0 ? "Use um número inteiro." : $"Use no máximo {p.Casas} casa decimal.";
        valor = lido;
        return null;
    }

    private void Recusar(string motivo)
    {
        ErroCampo = motivo;
        Mensagem = motivo;
    }

    // ------------------------------------------------------------------ espelho

    private string Campo(Parametro p) => _campos.GetValueOrDefault(p.Codigo, "");

    private void Escrever(Parametro p, string? texto)
    {
        texto ??= "";
        if (Campo(p) == texto)
            return;
        _campos[p.Codigo] = texto;
        _escritos.Add(p.Codigo);
        ErroCampo = null;
        Avisar(NomeDoCampo(p));
    }

    /// <summary>Os campos que o Denis não escreveu vêm do espelho.</summary>
    private void Semear()
    {
        foreach (var p in new[] { FrequenciaTomP, GanhoTomP, InicioSweepP, FimSweepP, GanhoSweepP })
        {
            if (_escritos.Contains(p.Codigo) || _estado.Valor(p.Codigo)?.Numero is not { } n || n < (p.Minimo ?? n) || n > (p.Maximo ?? n))
                continue;
            var texto = FormatoValor.Numero(p, n);
            if (Campo(p) == texto)
                continue;
            _campos[p.Codigo] = texto;
            Avisar(NomeDoCampo(p));
        }
        if (!_velocidadeEscolhida && _estado.Valor(VelocidadeP.Codigo)?.Numero is { } v && v is >= 1 and <= 3 && _velocidade != (int)v)
        {
            _velocidade = (int)v;
            Avisar(nameof(VelocidadeSweep));
            AvisarVelocidade();
        }
    }

    private static string NomeDoCampo(Parametro p) => p.Codigo switch
    {
        Catalogo.CodigoGeradorFrequencia => nameof(FrequenciaTom),
        Catalogo.CodigoGeradorGanho => nameof(GanhoTom),
        Catalogo.CodigoSweepInicio => nameof(InicioSweep),
        Catalogo.CodigoSweepFim => nameof(FimSweep),
        _ => nameof(GanhoSweep),
    };

    // ------------------------------------------------------------------ atualização

    private void AoMudarAlgo() => NaTela(Atualizar);

    private void AoRecarregar() => NaTela(() =>
    {
        Semear();
        Atualizar();
    });

    private void AoMudarValor(Endereco e)
    {
        if (e.Indice is null && Catalogo.CodigosDoGerador.Contains(e.Codigo))
            NaTela(Semear);
    }

    private void AoMudarChave(Chave chave)
    {
        if (chave == Chave.Gerador)
            NaTela(Atualizar);
    }

    private void Atualizar()
    {
        if (_descartado)
            return;
        // Uma frase nova do gerador (o prazo, a queda, o alarme) aparece.
        var doGerador = _gerador.Mensagem;
        if (doGerador != _ultimaDoGerador)
        {
            _ultimaDoGerador = doGerador;
            if (doGerador is not null)
                Mensagem = doGerador;
        }
        AjustarContagem();
        Avisar(nameof(MotivoTrava));
        Avisar(nameof(PodeTocar));
        Avisar(nameof(PodeParar));
        Avisar(nameof(Estado));
        Avisar(nameof(Tocando));
        Avisar(nameof(Contagem));
        Avisar(nameof(TextoContagem));
        Avisar(nameof(Alarme));
        Avisar(nameof(TextoDoAlarme));
    }

    /// <summary>Tocando, a contagem anda sozinha; parado, o relógio dela para.</summary>
    private void AjustarContagem()
    {
        if (Tocando)
        {
            _contagem ??= Relogio.CreateTimer(_ => NaTela(AvisarContagem), null, PassoContagem, PassoContagem);
            return;
        }
        _contagem?.Dispose();
        _contagem = null;
    }

    private void AvisarContagem()
    {
        if (_descartado)
            return;
        Avisar(nameof(Contagem));
        Avisar(nameof(TextoContagem));
        if (!Tocando)
            Atualizar();
    }

    private Task NaTelaAsync(Action acao)
    {
        var feito = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        NaTela(() =>
        {
            try
            {
                acao();
            }
            finally
            {
                feito.SetResult();
            }
        });
        return feito.Task;
    }

    public void Dispose()
    {
        _descartado = true;
        _contagem?.Dispose();
        _contagem = null;
        _gerador.Mudou -= AoMudarAlgo;
        _sessao.Mudou -= AoMudarAlgo;
        _chaves.Mudou -= AoMudarChave;
        _copia.Mudou -= AoMudarAlgo;
        if (_memorias is not null)
            _memorias.Mudou -= AoMudarAlgo;
        _estado.Recarregou -= AoRecarregar;
        _estado.Mudou -= AoMudarValor;
    }
}
