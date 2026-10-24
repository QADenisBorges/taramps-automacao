using System.Globalization;
using BgsControle.Nucleo.Aparelho;
using BgsControle.Nucleo.Setups;

namespace BgsControle.Telas;

/// <summary>Um ponto de clip: o nome do canal (a cor nunca vai sozinha) e se está aceso.</summary>
public sealed class PontoClipViewModel : TelaBase
{
    private bool _aceso;

    internal PontoClipViewModel(string nome, IDespachante despachante, TimeProvider relogio)
        : base(despachante, relogio) => Nome = nome;

    /// <summary>"Saída 1" … "Saída 4", ou "Entrada".</summary>
    public string Nome { get; }

    public bool Aceso
    {
        get => _aceso;
        internal set
        {
            if (_aceso == value)
                return;
            _aceso = value;
            Avisar();
        }
    }
}

/// <summary>
/// O clip de uma saída do Setup em uso (história 11): o nome escrito (a cor nunca vai sozinha), o canal
/// mestre (a cor) e se está aceso. Um par com link acende se qualquer um dos dois canais clipar (o Sub
/// do Casa 2.1 acende com o CH3 ou o CH4).
/// </summary>
public sealed class ClipSaidaViewModel : TelaBase
{
    private bool _aceso;

    internal ClipSaidaViewModel(SaidaTela saida, IDespachante despachante, TimeProvider relogio)
        : base(despachante, relogio) => Saida = saida;

    public SaidaTela Saida { get; }

    /// <summary>"JBL L", "Sub", "CH3".</summary>
    public string Nome => Saida.Nome;

    /// <summary>O canal mestre (0 a 3): a cor.</summary>
    public int Canal => Saida.Canal;

    public bool Aceso
    {
        get => _aceso;
        internal set
        {
            if (_aceso == value)
                return;
            _aceso = value;
            Avisar();
            Avisar(nameof(TextoLeitor));
        }
    }

    /// <summary>"Clip no Sub" aceso; "Sub sem clip" apagado (TalkBack).</summary>
    public string TextoLeitor => Aceso ? $"Clip no {Nome}" : $"{Nome} sem clip";
}

/// <summary>
/// A monitoração ao vivo (a faixa da Mesa e Configurações › Aparelho › Voltímetro e clip): o voltímetro
/// ("12,4 V", ou "—" sem valor) e o clip das saídas 1 a 4 e da entrada, cada um aceso por 3 s depois do
/// último aviso do aparelho. Na tela, o clip vai por saída do Setup em uso (<see cref="PorSaida"/>,
/// <c>GerenteSetups.SaidasEmUso</c>), refeito quando as saídas mudam. Ao desconectar, tudo volta a "—" e a
/// apagado. Só ouve: nada sai pelo BLE.
/// </summary>
public sealed class MonitoracaoViewModel : TelaBase, IDisposable
{
    public const string SemValor = "—";

    private readonly MonitorAparelho _monitor;
    private readonly GerenteSetups? _setups;
    private string _voltimetro = SemValor;
    private IReadOnlyList<ClipSaidaViewModel> _porSaida = [];

    public MonitoracaoViewModel(
        MonitorAparelho monitor,
        IDespachante despachante,
        TimeProvider relogio,
        GerenteSetups? setups = null,
        INavegacao? nav = null)
        : base(despachante, relogio)
    {
        _monitor = monitor;
        _setups = setups;
        Saidas = Enumerable.Range(1, MonitorAparelho.Saidas)
            .Select(s => new PontoClipViewModel($"Saída {s}", despachante, relogio))
            .ToList();
        Entrada = new PontoClipViewModel("Entrada", despachante, relogio);
        Abrir = new Comando(() => nav?.AbrirMonitoracao());
        _monitor.Mudou += AoMudar;
        if (_setups is not null)
            _setups.Mudou += AoMudar;
        Atualizar();
    }

    /// <summary>A leitura de agora mudou (voltímetro ou algum clip). Na thread da tela.</summary>
    public event Action? Atualizou;

    /// <summary>"12,4 V", ou "—" sem valor do voltímetro.</summary>
    public string Voltimetro
    {
        get => _voltimetro;
        private set
        {
            if (_voltimetro == value)
                return;
            _voltimetro = value;
            Avisar();
        }
    }

    /// <summary>O clip dos canais 1 a 4 do processador, na ordem.</summary>
    public IReadOnlyList<PontoClipViewModel> Saidas { get; }

    /// <summary>O clip da entrada.</summary>
    public PontoClipViewModel Entrada { get; }

    /// <summary>O clip do canal <paramref name="saida"/> do processador (1 a 4).</summary>
    public PontoClipViewModel Saida(int saida) => Saidas[saida - 1];

    /// <summary>O clip por saída do Setup em uso (1 a 4), na ordem dos canais; sem Setups, vazio.</summary>
    public IReadOnlyList<ClipSaidaViewModel> PorSaida => _porSaida;

    /// <summary>A saída do Setup que tem o canal (0 a 3), ou null.</summary>
    public ClipSaidaViewModel? ClipDe(int canal) => _porSaida.FirstOrDefault(p => p.Saida.App.Canais.Contains(canal));

    /// <summary>Algum canal da saída clipou (há menos de 3 s).</summary>
    public bool Clipou(SaidaApp saida) => saida.Canais.Any(c => _monitor.ClipSaida(c + 1));

    /// <summary>Tocar na faixa da Mesa: abre Voltímetro e clip.</summary>
    public Comando Abrir { get; }

    private void AoMudar() => NaTela(Atualizar);

    private void Atualizar()
    {
        Voltimetro = _monitor.Volts is { } v
            ? $"{v.ToString("0.0", CultureInfo.InvariantCulture).Replace('.', ',')} V"
            : SemValor;
        for (var s = 1; s <= Saidas.Count; s++)
            Saidas[s - 1].Aceso = _monitor.ClipSaida(s);
        Entrada.Aceso = _monitor.ClipEntrada;

        if (_setups is not null)
        {
            var agora = SaidaTela.EmUso(_setups);
            if (!SaidaTela.Iguais(agora, _porSaida.Select(p => p.Saida).ToArray()))
            {
                _porSaida = agora.Select(s => new ClipSaidaViewModel(s, Despachante, Relogio)).ToArray();
                Avisar(nameof(PorSaida));
            }
            foreach (var p in _porSaida)
                p.Aceso = Clipou(p.Saida.App);
        }
        Atualizou?.Invoke();
    }

    public void Dispose()
    {
        _monitor.Mudou -= AoMudar;
        if (_setups is not null)
            _setups.Mudou -= AoMudar;
    }
}
