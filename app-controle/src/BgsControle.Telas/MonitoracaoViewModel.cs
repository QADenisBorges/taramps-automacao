using System.Globalization;
using BgsControle.Nucleo.Aparelho;

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
/// A monitoração ao vivo (faixa da Mesa e Configurações › Aparelho › Voltímetro e clip; sem XAML:
/// é da história 11): o voltímetro ("12,4 V", ou "—" sem valor) e o clip das saídas 1 a 4 e da
/// entrada, cada um aceso por 3 s depois do último aviso do aparelho. Ao desconectar, tudo volta a
/// "—" e a apagado. Só ouve: nada sai pelo BLE.
/// </summary>
public sealed class MonitoracaoViewModel : TelaBase, IDisposable
{
    public const string SemValor = "—";

    private readonly MonitorAparelho _monitor;
    private string _voltimetro = SemValor;

    public MonitoracaoViewModel(MonitorAparelho monitor, IDespachante despachante, TimeProvider relogio)
        : base(despachante, relogio)
    {
        _monitor = monitor;
        Saidas = Enumerable.Range(1, MonitorAparelho.Saidas)
            .Select(s => new PontoClipViewModel($"Saída {s}", despachante, relogio))
            .ToList();
        Entrada = new PontoClipViewModel("Entrada", despachante, relogio);
        _monitor.Mudou += AoMudar;
        Atualizar();
    }

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

    /// <summary>O clip das saídas 1 a 4, na ordem.</summary>
    public IReadOnlyList<PontoClipViewModel> Saidas { get; }

    /// <summary>O clip da entrada.</summary>
    public PontoClipViewModel Entrada { get; }

    /// <summary>O clip da saída <paramref name="saida"/> (1 a 4).</summary>
    public PontoClipViewModel Saida(int saida) => Saidas[saida - 1];

    private void AoMudar() => NaTela(Atualizar);

    private void Atualizar()
    {
        Voltimetro = _monitor.Volts is { } v
            ? $"{v.ToString("0.0", CultureInfo.InvariantCulture).Replace('.', ',')} V"
            : SemValor;
        for (var s = 1; s <= Saidas.Count; s++)
            Saidas[s - 1].Aceso = _monitor.ClipSaida(s);
        Entrada.Aceso = _monitor.ClipEntrada;
    }

    public void Dispose() => _monitor.Mudou -= AoMudar;
}
