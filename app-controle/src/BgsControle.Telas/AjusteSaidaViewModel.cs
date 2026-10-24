using BgsControle.Nucleo;
using BgsControle.Nucleo.Escrita;
using BgsControle.Nucleo.Memorias;
using BgsControle.Nucleo.Seguranca;
using BgsControle.Nucleo.Setups;

namespace BgsControle.Telas;

/// <summary>
/// Um valor do equilíbrio do Setup de uma saída (ganho ou limitador), em dB com passo de 0,1, de −12,0 a
/// +12,0 (EXPERIENCE, Ajuste da saída). Vale para qualquer saída; num par com link, o mesmo valor vai aos
/// dois canais. Mexer muda o som na hora: o app escreve o valor final (memória + equilíbrio) pelo
/// <see cref="GerenteSetups.AjustarEquilibrioAsync"/> no canal mestre, e o Salvar leva o equilíbrio para a
/// calibração do Setup. Os toques seguidos (e o segurar) viram um envio por vez, sempre do último pedido:
/// nenhum passo pedido se perde.
/// </summary>
public sealed class ControleEquilibrioViewModel : TelaBase, ICampoEditavel, IDisposable
{
    public const string MsgSemSetup = "Escolha um Setup para mexer no equilíbrio.";
    public const string FaixaEPassoTexto = "−12,0 a +12,0 dB · passo 0,1 dB";

    private readonly GerenteSetups _setups;
    private readonly Sessao _sessao;
    private readonly IChaves _chaves;
    private readonly EstadoAparelho _estado;
    private readonly int _canal;
    private readonly bool _limiter;

    private decimal? _alvo;
    private bool _enviando;
    private string? _aviso;
    private bool _editando;
    private string _textoCampo = "";
    private string? _erroCampo;
    private ITimer? _repeticao;
    private int _direcao;

    internal ControleEquilibrioViewModel(int canal, bool limiter, GerenteSetups setups, Sessao sessao, IChaves chaves,
        EstadoAparelho estado, IDespachante despachante, TimeProvider relogio)
        : base(despachante, relogio)
    {
        _canal = canal;
        _limiter = limiter;
        _setups = setups;
        _sessao = sessao;
        _chaves = chaves;
        _estado = estado;
        Aumentar = new Comando(() => Passo(+1));
        Diminuir = new Comando(() => Passo(-1));
        ApertarMais = new Comando(() => ComecarSegurar(+1));
        ApertarMenos = new Comando(() => ComecarSegurar(-1));
        SoltarPasso = new Comando(PararSegurar);
        TocarNoValor = new Comando(AbrirCampo);
        Confirmar = new Comando(ConfirmarCampo);
        FecharCampo = new Comando(DescartarCampo);
    }

    /// <summary>O canal mestre da saída (0 a 3): a cor e o canal do equilíbrio.</summary>
    public int Canal => _canal;

    int? ICampoEditavel.Canal => _canal;

    /// <summary>"Equilíbrio do Casa 2.1 · ganho".</summary>
    public string Titulo => $"Equilíbrio do {_setups.SetupEmUso?.Nome ?? "Setup"} · {(_limiter ? "limitador" : "ganho")}";

    public string FaixaEPasso => FaixaEPassoTexto;

    /// <summary>O equilíbrio que vale agora (o pedido em andamento, ou o efetivo do Setup).</summary>
    public decimal Valor => _alvo ?? _setups.EquilibrioEfetivo.Valor(_canal, _limiter);

    /// <summary>"−3,0 dB".</summary>
    public string TextoValor => _setups.SetupEmUso is null ? "—" : Camadas.ExibirDb(Valor);

    /// <summary>"No limite da faixa com o equilíbrio do Casa 2.1.", o motivo de uma recusa, ou null.</summary>
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

    public string? MotivoTrava
    {
        get
        {
            if (_sessao.Estado != EstadoSessao.Conectado || !_sessao.SegurandoConexao)
                return ControleParametroViewModel.MsgSemConexao;
            if (_sessao.SoLeitura)
                return ControleParametroViewModel.MsgSoLeitura;
            if (!_chaves.Ligada(Chave.AjustesAudio))
                return ControleParametroViewModel.MsgChaveDesligada;
            if (_estado.Lendo)
                return ControleParametroViewModel.MsgLendo;
            if (!_estado.Pronto)
                return ControleParametroViewModel.MsgNaoLeu;
            if (_setups.EmEscolha)
                return GerenteSetups.MsgDuvida;
            if (_setups.SetupEmUso is null)
                return MsgSemSetup;
            return null;
        }
    }

    public bool Travado => MotivoTrava is not null;

    public Comando Aumentar { get; }
    public Comando Diminuir { get; }
    public Comando ApertarMais { get; }
    public Comando ApertarMenos { get; }
    public Comando SoltarPasso { get; }

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

    public Comando TocarNoValor { get; }
    public Comando Confirmar { get; }
    public Comando FecharCampo { get; }

    private void AbrirCampo()
    {
        if (Travado)
        {
            Aviso = MotivoTrava;
            return;
        }
        ErroCampo = null;
        TextoCampo = Valor.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture).Replace('.', ',');
        EditandoCampo = true;
    }

    private async Task ConfirmarCampo()
    {
        if (!EditandoCampo)
            return;
        if (FormatoValor.Ler(TextoCampo) is not { } v)
        {
            ErroCampo = ControleParametroViewModel.MsgNaoNumero;
            return;
        }
        if (v is < Camadas.EquilibrioMinimo or > Camadas.EquilibrioMaximo)
        {
            ErroCampo = GerenteSetups.MsgEquilibrioFaixa;
            return;
        }
        if (decimal.Round(v, 1) != v)
        {
            ErroCampo = "Use no máximo 1 casa decimal.";
            return;
        }
        DescartarCampo();
        await PedirAsync(v);
    }

    private void DescartarCampo()
    {
        EditandoCampo = false;
        ErroCampo = null;
        TextoCampo = "";
    }

    // ------------------------------------------------------------------ − e +

    private Task Passo(int direcao)
    {
        if (Travado)
        {
            PararSegurar();
            Aviso = MotivoTrava;
            return Task.CompletedTask;
        }
        // O passo parte do último pedido (_alvo), não do que o aparelho já confirmou: três toques
        // seguidos sobem 0,3 mesmo com o primeiro ainda indo.
        var novo = Math.Clamp(Valor + direcao * 0.1m, Camadas.EquilibrioMinimo, Camadas.EquilibrioMaximo);
        return novo == Valor ? Task.CompletedTask : PedirAsync(novo);
    }

    private void ComecarSegurar(int direcao)
    {
        PararSegurar();
        if (Travado)
        {
            Aviso = MotivoTrava;
            return;
        }
        _direcao = direcao;
        _ = Passo(direcao);
        _repeticao = Relogio.CreateTimer(_ => NaTela(() =>
        {
            if (_direcao != 0)
                _ = Passo(_direcao);
        }), null, ControleParametroViewModel.InicioRepeticao, ControleParametroViewModel.RepeticaoLenta);
    }

    private void PararSegurar()
    {
        _direcao = 0;
        _repeticao?.Dispose();
        _repeticao = null;
    }

    // ------------------------------------------------------------------ envio

    /// <summary>A última ida ao aparelho (testes): termina quando não há mais pedido pendente.</summary>
    public Task UltimoEnvio { get; private set; } = Task.CompletedTask;

    /// <summary>
    /// Guarda o pedido (sempre na thread da tela). Se nada está indo, começa o envio; se já está, o envio
    /// em curso manda este depois do que está no fio.
    /// </summary>
    private Task PedirAsync(decimal valor)
    {
        _alvo = valor;
        Aviso = null;
        AvisarValor();
        if (!_enviando)
        {
            _enviando = true;
            UltimoEnvio = EnviarAsync();
        }
        return UltimoEnvio;
    }

    private async Task EnviarAsync()
    {
        while (true)
        {
            var enviado = _alvo!.Value;
            ResultadoSetup r;
            try
            {
                r = await _setups.AjustarEquilibrioAsync(_canal, _limiter, enviado);
            }
            catch (Exception e)
            {
                r = ResultadoSetup.Falha($"Erro inesperado ({e.GetType().Name}).");
            }

            // A decisão de parar é tomada na thread da tela, a mesma dos pedidos: um passo pedido entre
            // a resposta e este ponto não se perde (ou ele é o próximo envio, ou chegou depois do fim e
            // começa um envio novo).
            var feito = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            NaTela(() =>
            {
                try
                {
                    if (r.Ok && _alvo is { } a && a != enviado)
                    {
                        feito.SetResult(true);
                        return;
                    }
                    _alvo = null;
                    _enviando = false;
                    Aviso = r.Mensagem;
                    AvisarValor();
                    feito.SetResult(false);
                }
                catch (Exception e)
                {
                    _enviando = false;
                    feito.SetException(e);
                }
            });
            if (!await feito.Task)
                return;
        }
    }

    // ------------------------------------------------------------------ eventos

    internal void Atualizar()
    {
        if (Travado)
        {
            PararSegurar();
            if (EditandoCampo)
                DescartarCampo();
        }
        AvisarValor();
        Avisar(nameof(Titulo));
        Avisar(nameof(MotivoTrava));
        Avisar(nameof(Travado));
    }

    private void AvisarValor()
    {
        Avisar(nameof(Valor));
        Avisar(nameof(TextoValor));
    }

    public void Dispose() => PararSegurar();
}

/// <summary>
/// O Ajuste da saída (EXPERIENCE, "Ajuste da saída"; DESIGN, giratório e Avançado), para uma saída do
/// Setup em uso: o ganho no giratório, sempre aberto; à vista o passa-alta, o passa-baixa e o limitador;
/// em Avançado o atraso, a fase, o equilíbrio do Setup daquela saída, o EQ de saída, o ataque, a
/// liberação, o Auto do limitador e o mute. Os controles escrevem no canal mestre; num par com link, o
/// aparelho repete no outro canal. Quando as saídas mudam (link, equipamento, Setup), a tela segue a
/// saída que tem o canal pedido, e os controles se refazem se o mestre dela mudou. O Salvar fica fixo
/// no pé.
/// </summary>
public sealed class AjusteSaidaViewModel : TelaBase, IDisposable
{
    private readonly FabricaControles _fabrica;
    private readonly GerenteSetups _setups;
    private readonly GerenteMemorias _memorias;
    private readonly Sessao _sessao;
    private readonly IChaves _chaves;
    private readonly EstadoAparelho _estado;
    private readonly LinkCanais _link;
    private readonly int _pedido;
    private bool _avancadoAberto;
    private SaidaTela _saida;
    private Controles _controles;

    private sealed record Controles(
        int Canal,
        ControleParametroViewModel Ganho,
        IReadOnlyList<ItemControleViewModel> Vista,
        IReadOnlyList<ItemControleViewModel> AvancadoInicio,
        IReadOnlyList<ControleEquilibrioViewModel> Equilibrio,
        IReadOnlyList<ItemControleViewModel> AvancadoFim);

    /// <param name="canal">O canal pedido (0 a 3): a tela é a da saída que tem ele.</param>
    public AjusteSaidaViewModel(
        int canal,
        FabricaControles fabrica,
        GerenteSetups setups,
        GerenteMemorias memorias,
        Sessao sessao,
        IChaves chaves,
        EstadoAparelho estado,
        LinkCanais link,
        SalvarViewModel salvar,
        INavegacao nav,
        IDespachante despachante,
        TimeProvider relogio)
        : base(despachante, relogio)
    {
        _pedido = canal;
        _fabrica = fabrica;
        _setups = setups;
        _memorias = memorias;
        _sessao = sessao;
        _chaves = chaves;
        _estado = estado;
        _link = link;
        Salvar = salvar;
        _saida = SaidaAgora();
        _controles = Montar(_saida.Canal);

        AlternarAvancado = new Comando(() =>
        {
            _avancadoAberto = !_avancadoAberto;
            Avisar(nameof(AvancadoAberto));
            Avisar(nameof(IndicadorAvancado));
        });
        Voltar = new Comando(nav.Voltar);

        _setups.Mudou += AoMudar;
        _memorias.Mudou += AoMudar;
        _sessao.Mudou += AoMudar;
        _chaves.Mudou += AoMudarChave;
        _estado.Recarregou += AoMudar;
        _estado.Mudou += AoMudarValor;
        _link.Mudou += AoMudarLink;
    }

    /// <summary>A saída desta tela.</summary>
    public SaidaTela Saida => _saida;

    /// <summary>O canal mestre (0 a 3): a cor da saída e onde os controles escrevem.</summary>
    public int Canal => _saida.Canal;

    /// <summary>"JBL L", "Sub", na cor da saída.</summary>
    public string Titulo => _saida.Nome;

    /// <summary>"CH1 · rota B · FILME", "CH3 + CH4 · rota A+B · FILME".</summary>
    public string Subtitulo
    {
        get
        {
            var texto = $"{_saida.Rotulo} · rota {TextosParametro.Rota(_estado, Canal)}";
            return SomEmUsoTexto.De(_setups, _memorias, _sessao).Nome is { } nome ? $"{texto} · {nome}" : texto;
        }
    }

    /// <summary>O aviso do bridge sem o link, ou null.</summary>
    public string? AvisoSaida => _saida.Aviso;

    /// <summary>O ganho (−45,0 a +15,0 dB), no giratório: arrastar, − e + grandes, tocar no número para escrever.</summary>
    public ControleParametroViewModel Ganho => _controles.Ganho;

    public string DicaGiratorio => "Gire o botão ou toque em − e +";

    public string DicaCampo => "Toque no número para escrever";

    /// <summary>Passa-alta, passa-baixa (frequência e tipo) e limitador.</summary>
    public IReadOnlyList<ItemControleViewModel> Vista => _controles.Vista;

    /// <summary>Avançado, primeiro: atraso e fase.</summary>
    public IReadOnlyList<ItemControleViewModel> AvancadoInicio => _controles.AvancadoInicio;

    /// <summary>Avançado, depois do atraso e da fase: o equilíbrio do Setup desta saída (ganho e limitador).</summary>
    public IReadOnlyList<ControleEquilibrioViewModel> Equilibrio => _controles.Equilibrio;

    public bool TemEquilibrio => Equilibrio.Count > 0;

    /// <summary>Avançado, por fim: EQ de saída, ataque, liberação, Auto e mute.</summary>
    public IReadOnlyList<ItemControleViewModel> AvancadoFim => _controles.AvancadoFim;

    public bool AvancadoAberto => _avancadoAberto;

    public string IndicadorAvancado => _avancadoAberto ? "⌃" : "⌄";

    public Comando AlternarAvancado { get; }

    /// <summary>O Salvar fixo no pé.</summary>
    public SalvarViewModel Salvar { get; }

    public string Lembrete => FuncaoViewModel.LembreteSalvar;

    public Comando Voltar { get; }

    /// <summary>O controle pelo código, à vista ou no Avançado (testes).</summary>
    public ControleParametroViewModel? Controle(string codigo) =>
        codigo == "081" ? Ganho : Vista.Concat(AvancadoInicio).Concat(AvancadoFim).Select(i => i.Controle).FirstOrDefault(x => x.Parametro.Codigo == codigo);

    public ControleEquilibrioViewModel? EquilibrioGanho => Equilibrio.FirstOrDefault();

    public ControleEquilibrioViewModel? EquilibrioLimitador => Equilibrio.Skip(1).FirstOrDefault();

    // ------------------------------------------------------------------ montagem

    /// <summary>A saída em uso que tem o canal pedido (sem Setup, a do canal sozinho).</summary>
    private SaidaTela SaidaAgora()
    {
        var saidas = SaidaTela.EmUso(_setups);
        return saidas.FirstOrDefault(s => s.App.Canais.Contains(_pedido))
               ?? new SaidaTela(new SaidaApp(_pedido, [_pedido], Ligacoes.RotuloCanais([_pedido]), null), Ligacoes.RotuloCanais([_pedido]));
    }

    private Controles Montar(int c)
    {
        var grupo = new GrupoControles();
        var ganho = _fabrica.Criar("081", c);
        ganho.Expandir(); // o giratório fica sempre aberto

        ItemControleViewModel Item(string codigo, string? titulo = null, ControleParametroViewModel? dependeDe = null) =>
            new(_fabrica.Criar(codigo, c, grupo, titulo), dependeDe, Despachante, Relogio);

        var tipoPa = Item("042", "Passa-alta · tipo do filtro");
        var tipoPb = Item("043", "Passa-baixa · tipo do filtro");
        return new Controles(
            c,
            ganho,
            [Item("040", dependeDe: tipoPa.Controle), tipoPa, Item("041", dependeDe: tipoPb.Controle), tipoPb, Item("071")],
            [Item("050"), Item("060")],
            [
                new ControleEquilibrioViewModel(c, false, _setups, _sessao, _chaves, _estado, Despachante, Relogio),
                new ControleEquilibrioViewModel(c, true, _setups, _sessao, _chaves, _estado, Despachante, Relogio),
            ],
            [
                Item("090", "EQ de saída · frequência"), Item("091", "EQ de saída · ganho"), Item("092", "EQ de saída · Q"),
                Item("072", "Ataque do limitador"), Item("073", "Liberação do limitador"), Item("070"), Item("100"),
            ]);
    }

    private static void Descartar(Controles c)
    {
        c.Ganho.Dispose();
        foreach (var i in c.Vista.Concat(c.AvancadoInicio).Concat(c.AvancadoFim))
            i.Dispose();
        foreach (var e in c.Equilibrio)
            e.Dispose();
    }

    /// <summary>As saídas mudaram: a tela segue a saída do canal pedido; mudou o mestre, os controles se refazem.</summary>
    private void Refazer()
    {
        var agora = SaidaAgora();
        if (agora.Igual(_saida))
            return;
        _saida = agora;
        if (agora.Canal != _controles.Canal)
        {
            var antigos = _controles;
            _controles = Montar(agora.Canal);
            Descartar(antigos);
            Avisar(nameof(Ganho));
            Avisar(nameof(Vista));
            Avisar(nameof(AvancadoInicio));
            Avisar(nameof(Equilibrio));
            Avisar(nameof(TemEquilibrio));
            Avisar(nameof(AvancadoFim));
            Avisar(nameof(EquilibrioGanho));
            Avisar(nameof(EquilibrioLimitador));
        }
        Avisar(nameof(Saida));
        Avisar(nameof(Canal));
        Avisar(nameof(Titulo));
        Avisar(nameof(AvisoSaida));
        Avisar(nameof(Subtitulo));
    }

    // ------------------------------------------------------------------ eventos

    private void AoMudar() => NaTela(Atualizar);

    private void AoMudarChave(Chave _) => NaTela(Atualizar);

    private void AoMudarValor(Endereco e)
    {
        if (e.Codigo == "010")
            NaTela(() => Avisar(nameof(Subtitulo)));
    }

    private void AoMudarLink(ParCanais _) => NaTela(() =>
    {
        Refazer();
        Avisar(nameof(Subtitulo));
    });

    private void Atualizar()
    {
        Refazer();
        foreach (var e in Equilibrio)
            e.Atualizar();
        Avisar(nameof(Subtitulo));
    }

    public void Dispose()
    {
        _setups.Mudou -= AoMudar;
        _memorias.Mudou -= AoMudar;
        _sessao.Mudou -= AoMudar;
        _chaves.Mudou -= AoMudarChave;
        _estado.Recarregou -= AoMudar;
        _estado.Mudou -= AoMudarValor;
        _link.Mudou -= AoMudarLink;
        Descartar(_controles);
    }
}
