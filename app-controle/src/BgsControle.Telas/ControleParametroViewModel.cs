using BgsControle.Nucleo;
using BgsControle.Nucleo.Escrita;
using BgsControle.Nucleo.Seguranca;

namespace BgsControle.Telas;

/// <summary>Uma escolha direta: tipo do filtro (OFF incluído), fase, rota, liga/desliga, preset.</summary>
/// <param name="Numero">O valor numérico no fio (null para a rota).</param>
/// <param name="Texto">A rota no fio (<c>a</c>, <c>b</c>, <c>a+b</c>).</param>
public sealed record OpcaoValor(string Rotulo, decimal? Numero, string? Texto);

/// <summary>
/// Um grupo de controles em que só um fica aberto por vez: abrir um recolhe o anterior.
/// </summary>
public sealed class GrupoControles
{
    private ControleParametroViewModel? _aberto;

    /// <summary>O controle aberto no grupo, ou null.</summary>
    public ControleParametroViewModel? Aberto => _aberto;

    internal void Abriu(ControleParametroViewModel controle)
    {
        var anterior = _aberto;
        _aberto = controle;
        if (anterior is not null && anterior != controle)
            anterior.Recolher();
    }

    internal void Fechou(ControleParametroViewModel controle)
    {
        if (_aberto == controle)
            _aberto = null;
    }
}

/// <summary>
/// O controle de um parâmetro de áudio (fader + campo + − e +, ou as escolhas diretas). Mexer
/// muda o som na hora, pelo <see cref="Escritor"/>; o valor mostrado é o do espelho
/// (<see cref="EstadoAparelho"/>), ou o que o usuário acabou de pedir até o aparelho confirmar.
/// <list type="bullet">
/// <item>Vem recolhido: nome, faixa e passo, e o valor. Tocar no valor (ou no ⌄) abre; com o
/// controle aberto, tocar no valor abre o campo. Num <see cref="GrupoControles"/>, abrir um
/// recolhe o outro.</item>
/// <item>Arrastar (<see cref="Arrastando"/> / <see cref="Soltou"/>) segue o ritmo do escritor.</item>
/// <item>− e + andam o passo (frequências: 1/12 de oitava, no mínimo 1 Hz). Segurar repete:
/// começa depois de 0,4 s e acelera depois de 2 s.</item>
/// <item>O campo aceita vírgula ou ponto e só vale no ✓; fora da faixa é recusado com a faixa
/// ao lado; fechar sem ✓ descarta.</item>
/// <item>Sem conexão, em modo só leitura, com a chave "Ajustes de áudio" desligada ou lendo o
/// aparelho, fica travado e diz por quê.</item>
/// </list>
/// </summary>
public sealed class ControleParametroViewModel : TelaBase, IDisposable
{
    public const string MsgSemConexao = "Sem conexão";
    public const string MsgSoLeitura = "Modo só leitura";
    public const string MsgChaveDesligada = "Chave desligada";
    public const string MsgLendo = "Lendo o aparelho";
    public const string MsgNaoLeu = "O app ainda não leu o aparelho";
    public const string MsgSemValor = "Sem leitura deste ajuste";
    public const string MsgNaoNumero = "Escreva um número.";
    public const string PrefixoForaDaFaixa = "Fora da faixa: ";

    /// <summary>Segurar − ou +: a repetição começa depois disto.</summary>
    public static readonly TimeSpan InicioRepeticao = TimeSpan.FromMilliseconds(400);

    /// <summary>Segurar − ou +: depois disto, a repetição acelera.</summary>
    public static readonly TimeSpan InicioAceleracao = TimeSpan.FromSeconds(2);

    public static readonly TimeSpan RepeticaoLenta = TimeSpan.FromMilliseconds(100);
    public static readonly TimeSpan RepeticaoRapida = TimeSpan.FromMilliseconds(25);

    /// <summary>1/12 de oitava.</summary>
    private static readonly double FatorFrequencia = Math.Pow(2, 1.0 / 12);

    private readonly Sessao _sessao;
    private readonly IChaves _chaves;
    private readonly EstadoAparelho _estado;
    private readonly Escritor _escritor;
    private readonly GrupoControles? _grupo;
    private readonly Endereco _endereco;

    private decimal? _localNumero; // pedido pelo usuário e ainda não confirmado
    private string? _localTexto;
    private int _pedidos;
    private bool _arrastando;
    private decimal? _ultimoLimiar;
    private string? _aviso;
    private bool _expandido;
    private bool _editando;
    private string _textoCampo = "";
    private string? _erroCampo;

    private ITimer? _repeticao;
    private int _direcao;
    private DateTimeOffset _inicioSegurar;
    private bool _acelerado;
    private bool _descartado;

    public ControleParametroViewModel(
        Parametro parametro,
        int? indice,
        Sessao sessao,
        IChaves chaves,
        EstadoAparelho estado,
        Escritor escritor,
        IDespachante despachante,
        TimeProvider relogio,
        GrupoControles? grupo = null)
        : base(despachante, relogio)
    {
        Parametro = parametro;
        Indice = indice;
        _sessao = sessao;
        _chaves = chaves;
        _estado = estado;
        _escritor = escritor;
        _grupo = grupo;
        _endereco = new Endereco(parametro.Codigo, indice);
        Opcoes = MontarOpcoes(parametro);

        _sessao.Mudou += AoMudarAlgo;
        _chaves.Mudou += AoMudarChave;
        _estado.Recarregou += AoMudarAlgo;
        _estado.Mudou += AoMudarValor;

        Aumentar = new Comando(() => Passo(+1));
        Diminuir = new Comando(() => Passo(-1));
        AlternarExpandido = new Comando(() =>
        {
            if (Expandido)
                Recolher();
            else
                Expandir();
        });
        TocarNoValor = new Comando(TocarValor);
        Confirmar = new Comando(ConfirmarCampo);
        FecharCampo = new Comando(DescartarCampo);
        AlternarOff = new Comando(AlternarLimitadorOff);
    }

    public Parametro Parametro { get; }
    public int? Indice { get; }

    public string Nome => Parametro.Nome;

    /// <summary>"saída 2", "banda 160 Hz" ou "—".</summary>
    public string Onde => Parametro.Onde(Indice);

    /// <summary>"−45,0 a +15,0 dB". Vazio nas escolhas diretas.</summary>
    public string Faixa => FormatoValor.Faixa(Parametro);

    /// <summary>"passo 0,1 dB", "passo 1/12 de oitava". Vazio nas escolhas diretas.</summary>
    public string TextoPasso => Parametro.Tipo switch
    {
        TipoValor.Hertz => "passo 1/12 de oitava",
        _ when TemPasso => $"passo {PassoFixo.ToString(PassoFixo == decimal.Truncate(PassoFixo) ? "0" : "0.0", System.Globalization.CultureInfo.InvariantCulture).Replace('.', ',')}{UnidadeDoPasso}",
        _ => "",
    };

    private string UnidadeDoPasso => string.IsNullOrEmpty(Parametro.Unidade) || Parametro.Tipo == TipoValor.Q ? "" : $" {Parametro.Unidade}";

    /// <summary>A linha de cima do controle: faixa e passo.</summary>
    public string FaixaEPasso => string.Join(" · ", new[] { Faixa, TextoPasso }.Where(t => t.Length > 0));

    /// <summary>Escolhas diretas (filtro, fase, rota, liga/desliga, preset); vazio num número.</summary>
    public IReadOnlyList<OpcaoValor> Opcoes { get; }

    public bool EhEscolha => Opcoes.Count > 0;

    /// <summary>Número com − e + e campo (ganho, frequência, Q, atraso, limitador…).</summary>
    public bool TemPasso => !EhEscolha;

    /// <summary>O limitador tem OFF como escolha direta.</summary>
    public bool TemOff => FormatoValor.TemOff(Parametro);

    // ------------------------------------------------------------------ valor mostrado

    /// <summary>O valor confirmado pelo aparelho (o espelho).</summary>
    public Valor? ValorAparelho => _estado.Valor(_endereco);

    /// <summary>O número mostrado: o pedido em andamento, ou o do aparelho.</summary>
    public decimal? ValorMostrado => _localNumero ?? (_localTexto is null ? ValorAparelho?.Numero : null);

    public bool EmOff => TemOff && ValorMostrado is { } v && v == FormatoValor.ValorOff(Parametro);

    /// <summary>O valor para a tela: "−3,0 dB", "OFF", "LR24", "A+B" ou "—".</summary>
    public string TextoValor
    {
        get
        {
            if (EhEscolha)
                return OpcaoEscolhida?.Rotulo ?? ValorAparelho?.Exibicao ?? "—";
            if (EmOff)
                return "OFF";
            // Um valor especial do aparelho ("auto" no ataque) ou fora da faixa: como o núcleo o mostra.
            if (_localNumero is null && _localTexto is null && ValorAparelho is { } va && (va.Rotulo is not null || va.ForaDaFaixa))
                return va.Exibicao;
            return ValorMostrado is { } v ? FormatoValor.ComUnidade(Parametro, v) : "—";
        }
    }

    public OpcaoValor? OpcaoEscolhida
    {
        get
        {
            var numero = _localNumero ?? (_localTexto is null ? ValorAparelho?.Numero : null);
            var texto = _localTexto ?? (_localNumero is null ? ValorAparelho?.Texto : null);
            return Opcoes.FirstOrDefault(o =>
                (o.Numero is not null && o.Numero == numero) || (o.Texto is not null && o.Texto == texto));
        }
    }

    /// <summary>"O aparelho ficou em −2,9 dB", ou o motivo de uma escrita que falhou.</summary>
    public string? Aviso
    {
        get => _aviso;
        private set
        {
            if (_aviso == value)
                return;
            _aviso = value;
            Avisar();
        }
    }

    // ------------------------------------------------------------------ travas

    /// <summary>Por que o controle está travado, ou null.</summary>
    public string? MotivoTrava
    {
        get
        {
            if (_sessao.Estado != EstadoSessao.Conectado || !_sessao.SegurandoConexao)
                return MsgSemConexao;
            if (_sessao.SoLeitura)
                return MsgSoLeitura;
            if (!_chaves.Ligada(Chave.AjustesAudio))
                return MsgChaveDesligada;
            if (_estado.Lendo)
                return MsgLendo;
            if (!_estado.Pronto)
                return MsgNaoLeu;
            if (ValorAparelho is null)
                return MsgSemValor;
            return null;
        }
    }

    public bool Travado => MotivoTrava is not null;

    // ------------------------------------------------------------------ recolhido e aberto

    /// <summary>Aberto: o controle completo. Recolhido (o padrão): nome, faixa e passo, e o valor.</summary>
    public bool Expandido
    {
        get => _expandido;
        private set
        {
            if (_expandido == value)
                return;
            _expandido = value;
            Avisar();
            Avisar(nameof(IndicadorExpandir));
        }
    }

    /// <summary>⌄ recolhido, ⌃ aberto.</summary>
    public string IndicadorExpandir => Expandido ? "⌃" : "⌄";

    public Comando AlternarExpandido { get; }

    /// <summary>Recolhido: abre o controle. Aberto: abre o campo para escrever.</summary>
    public Comando TocarNoValor { get; }

    public void Expandir()
    {
        Expandido = true;
        _grupo?.Abriu(this);
    }

    public void Recolher()
    {
        PararSegurar();
        if (EditandoCampo)
            DescartarCampo();
        Expandido = false;
        _grupo?.Fechou(this);
    }

    private void TocarValor()
    {
        if (!Expandido)
        {
            Expandir();
            return;
        }
        AbrirCampo();
    }

    // ------------------------------------------------------------------ campo

    public bool EditandoCampo
    {
        get => _editando;
        private set
        {
            if (_editando == value)
                return;
            _editando = value;
            Avisar();
        }
    }

    /// <summary>O texto do campo. Só vale no ✓.</summary>
    public string TextoCampo
    {
        get => _textoCampo;
        set
        {
            if (_textoCampo == value)
                return;
            _textoCampo = value;
            Avisar();
        }
    }

    /// <summary>"Fora da faixa: −45,0 a +15,0 dB", ou null.</summary>
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

    /// <summary>O ✓ do teclado.</summary>
    public Comando Confirmar { get; }

    /// <summary>Fechar o teclado sem ✓: descarta.</summary>
    public Comando FecharCampo { get; }

    public void AbrirCampo()
    {
        if (EhEscolha || Travado)
            return;
        ErroCampo = null;
        TextoCampo = TextoParaCampo();
        EditandoCampo = true;
    }

    private string TextoParaCampo()
    {
        if (EmOff)
            return "OFF";
        // Para o teclado numérico: "-3,0" e "10,0", sem o + nem o − tipográfico.
        return ValorMostrado is { } v ? FormatoValor.Numero(Parametro, v).Replace(FormatoValor.Menos, '-').TrimStart('+') : "";
    }

    private void ConfirmarCampo()
    {
        if (!EditandoCampo)
            return;
        if (Travado)
        {
            ErroCampo = MotivoTrava;
            return;
        }

        decimal valor;
        if (TemOff && string.Equals(TextoCampo.Trim(), "OFF", StringComparison.OrdinalIgnoreCase))
        {
            valor = FormatoValor.ValorOff(Parametro)!.Value;
        }
        else
        {
            if (FormatoValor.Ler(TextoCampo) is not { } lido)
            {
                Recusar(MsgNaoNumero);
                return;
            }
            if ((Parametro.Minimo is { } min && lido < min) || (Parametro.Maximo is { } max && lido > max))
            {
                Recusar(PrefixoForaDaFaixa + Faixa);
                return;
            }
            if (decimal.Round(lido, Parametro.Casas) != lido)
            {
                Recusar(Parametro.Casas == 0 ? "Use um número inteiro." : $"Use no máximo {Parametro.Casas} casa decimal.");
                return;
            }
            valor = lido;
        }

        EditandoCampo = false;
        ErroCampo = null;
        TextoCampo = "";
        Pedir(valor, null);
    }

    /// <summary>Recusado: o valor anterior volta ao campo, com o motivo ao lado.</summary>
    private void Recusar(string motivo)
    {
        ErroCampo = motivo;
        TextoCampo = TextoParaCampo();
    }

    private void DescartarCampo()
    {
        EditandoCampo = false;
        ErroCampo = null;
        TextoCampo = "";
    }

    // ------------------------------------------------------------------ − e +

    public Comando Aumentar { get; }
    public Comando Diminuir { get; }

    private decimal PassoFixo => Parametro.Tipo switch
    {
        TipoValor.Centimetro => 0.5m,
        TipoValor.Release => 1m,
        _ => 0.1m,
    };

    /// <summary>O valor um passo acima (+1) ou abaixo (−1), dentro da faixa.</summary>
    public decimal ValorDoPasso(decimal atual, int direcao)
    {
        decimal novo;
        if (Parametro.Tipo == TipoValor.Hertz)
        {
            var fator = direcao > 0 ? FatorFrequencia : 1 / FatorFrequencia;
            novo = (decimal)Math.Round((double)atual * fator, MidpointRounding.AwayFromZero);
            if (direcao > 0 && novo < atual + 1)
                novo = atual + 1;
            if (direcao < 0 && novo > atual - 1)
                novo = atual - 1;
        }
        else
        {
            novo = atual + direcao * PassoFixo;
        }
        return Limitar(novo);
    }

    private void Passo(int direcao)
    {
        if (!TemPasso)
            return;
        if (Travado)
        {
            Aviso = MotivoTrava;
            return;
        }
        decimal atual;
        if (EmOff)
            atual = _ultimoLimiar ?? Parametro.Maximo ?? 0m;
        else if (ValorMostrado is { } v)
            atual = v;
        else
            return;
        var novo = ValorDoPasso(atual, direcao);
        if (novo == ValorMostrado)
            return; // já no limite
        Pedir(novo, null);
    }

    /// <summary>O dedo desceu em − (−1) ou + (+1): um passo já, e a repetição se segurar.</summary>
    public void ComecarSegurar(int direcao)
    {
        PararSegurar();
        if (Travado || !TemPasso)
            return;
        _direcao = Math.Sign(direcao);
        _inicioSegurar = Relogio.GetUtcNow();
        _acelerado = false;
        Passo(_direcao);
        _repeticao = Relogio.CreateTimer(_ => NaTela(Repetir), null, InicioRepeticao, RepeticaoLenta);
    }

    /// <summary>O dedo subiu.</summary>
    public void PararSegurar()
    {
        _direcao = 0;
        _repeticao?.Dispose();
        _repeticao = null;
    }

    private void Repetir()
    {
        if (_direcao == 0 || _repeticao is null)
            return;
        if (Travado)
        {
            PararSegurar();
            return;
        }
        Passo(_direcao);
        if (!_acelerado && Relogio.GetUtcNow() - _inicioSegurar >= InicioAceleracao)
        {
            _acelerado = true;
            _repeticao.Change(RepeticaoRapida, RepeticaoRapida);
        }
    }

    // ------------------------------------------------------------------ arrastar

    /// <summary>O fader está em <paramref name="valor"/>. O som muda na hora, no ritmo do escritor.</summary>
    public void Arrastando(decimal valor)
    {
        if (!TemPasso || Travado)
            return;
        _arrastando = true;
        var v = Limitar(valor);
        if (Parametro.Tipo == TipoValor.Centimetro)
            v = Limitar(decimal.Round(v * 2, MidpointRounding.AwayFromZero) / 2);
        if (v == _localNumero)
            return;
        Pedir(v, null);
    }

    /// <summary>O dedo soltou o fader: o último valor sempre vai. Termina quando o aparelho confirma.</summary>
    public Task Soltou()
    {
        _arrastando = false;
        if (UltimaEscrita.IsCompleted && (_localNumero is not null || _localTexto is not null))
        {
            _localNumero = null;
            _localTexto = null;
            Atualizar();
        }
        return UltimaEscrita;
    }

    // ------------------------------------------------------------------ escolhas diretas e OFF

    /// <summary>Escolhe uma opção (tipo do filtro, fase, rota…). Aplica na hora.</summary>
    public Task Escolher(OpcaoValor opcao)
    {
        if (!EhEscolha || Travado)
        {
            if (Travado)
                Aviso = MotivoTrava;
            return Task.CompletedTask;
        }
        return Pedir(opcao.Numero, opcao.Texto);
    }

    /// <summary>O botão OFF do limitador: liga o OFF; tocado de novo, volta ao último limiar.</summary>
    public Comando AlternarOff { get; }

    private void AlternarLimitadorOff()
    {
        if (!TemOff)
            return;
        if (Travado)
        {
            Aviso = MotivoTrava;
            return;
        }
        if (EmOff)
        {
            Pedir(_ultimoLimiar ?? Parametro.Maximo ?? 0m, null);
            return;
        }
        _ultimoLimiar = ValorMostrado;
        Pedir(FormatoValor.ValorOff(Parametro)!.Value, null);
    }

    // ------------------------------------------------------------------ envio

    /// <summary>A última escrita pedida por este controle (termina depois de a tela se atualizar).</summary>
    public Task UltimaEscrita { get; private set; } = Task.CompletedTask;

    private Task Pedir(decimal? numero, string? texto)
    {
        var id = ++_pedidos;
        _localNumero = numero;
        _localTexto = numero is null ? texto : null;
        Aviso = null;
        Atualizar();
        var escrita = numero is { } n
            ? _escritor.EscreverAsync(Parametro, Indice, n)
            : _escritor.EscreverAsync(Parametro, Indice, texto!);
        UltimaEscrita = ConcluirAsync(escrita, id);
        return UltimaEscrita;
    }

    private async Task ConcluirAsync(Task<ResultadoEscrita> escrita, int id)
    {
        var r = await escrita;
        var feito = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        NaTela(() =>
        {
            try
            {
                if (id != _pedidos || _descartado)
                    return; // um pedido mais novo manda na tela
                if (!_arrastando)
                {
                    _localNumero = null;
                    _localTexto = null;
                }
                Aviso = !r.Ok
                    ? Frase(r.Motivo)
                    : r.Diferente && r.Valor is { } v ? $"O aparelho ficou em {Exibir(v)}" : null;
                Atualizar();
            }
            finally
            {
                feito.SetResult();
            }
        });
        await feito.Task;
    }

    private string Exibir(Valor v)
    {
        if (EhEscolha)
            return Opcoes.FirstOrDefault(o => (o.Numero is not null && o.Numero == v.Numero) || (o.Texto is not null && o.Texto == v.Texto))?.Rotulo
                   ?? v.Exibicao;
        if (TemOff && v.Numero == FormatoValor.ValorOff(Parametro))
            return "OFF";
        if (v.Rotulo is not null || v.ForaDaFaixa)
            return v.Exibicao;
        return v.Numero is { } n ? FormatoValor.ComUnidade(Parametro, n) : v.Exibicao;
    }

    private static string? Frase(string? motivo) =>
        string.IsNullOrEmpty(motivo) ? null : char.ToUpperInvariant(motivo[0]) + motivo[1..];

    private decimal Limitar(decimal v)
    {
        if (Parametro.Minimo is { } min && v < min)
            v = min;
        if (Parametro.Maximo is { } max && v > max)
            v = max;
        return decimal.Round(v, Parametro.Casas, MidpointRounding.AwayFromZero);
    }

    // ------------------------------------------------------------------ eventos

    private void AoMudarAlgo() => NaTela(Atualizar);

    private void AoMudarChave(Chave chave)
    {
        if (chave == Chave.AjustesAudio)
            NaTela(Atualizar);
    }

    private void AoMudarValor(Endereco e)
    {
        if (e == _endereco)
            NaTela(Atualizar);
    }

    private void Atualizar()
    {
        if (Travado)
        {
            PararSegurar();
            if (EditandoCampo)
                DescartarCampo();
            // Travou (ou caiu) no meio de um arrasto ou pedido: a tela volta ao valor do aparelho.
            _arrastando = false;
            _localNumero = null;
            _localTexto = null;
        }
        Avisar(nameof(ValorAparelho));
        Avisar(nameof(ValorMostrado));
        Avisar(nameof(EmOff));
        Avisar(nameof(TextoValor));
        Avisar(nameof(OpcaoEscolhida));
        Avisar(nameof(MotivoTrava));
        Avisar(nameof(Travado));
    }

    private static IReadOnlyList<OpcaoValor> MontarOpcoes(Parametro p) => p.Tipo switch
    {
        TipoValor.Roteamento => [new("A", null, "a"), new("B", null, "b"), new("A+B", null, "a+b")],
        TipoValor.Booleano => [new(p.RotuloDesligado, 0m, null), new(p.RotuloLigado, 1m, null)],
        _ when p.Opcoes is not null => p.Opcoes.Select(o => new OpcaoValor(o.Value, o.Key, null)).ToList(),
        _ => [],
    };

    public void Dispose()
    {
        _descartado = true;
        PararSegurar();
        _sessao.Mudou -= AoMudarAlgo;
        _chaves.Mudou -= AoMudarChave;
        _estado.Recarregou -= AoMudarAlgo;
        _estado.Mudou -= AoMudarValor;
        _grupo?.Fechou(this);
    }
}

/// <summary>Cria os controles de parâmetro com as dependências do app (registrada no DI).</summary>
public sealed class FabricaControles(
    Sessao sessao,
    IChaves chaves,
    EstadoAparelho estado,
    Escritor escritor,
    IDespachante despachante,
    TimeProvider relogio)
{
    public ControleParametroViewModel Criar(Parametro parametro, int? indice, GrupoControles? grupo = null) =>
        new(parametro, indice, sessao, chaves, estado, escritor, despachante, relogio, grupo);
}
