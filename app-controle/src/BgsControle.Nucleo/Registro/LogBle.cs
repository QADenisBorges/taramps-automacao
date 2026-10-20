using System.Text.RegularExpressions;

namespace BgsControle.Nucleo.Registro;

/// <summary>
/// Log do BLE em memória (nunca vai ao logcat). Toda linha passa pela máscara: o quadro da
/// senha vira <c>*000*••••*</c>, e qualquer senha conhecida pela sessão some do texto.
/// </summary>
public sealed class LogBle
{
    public const string Mascara = "••••";
    private const int MaximoDeLinhas = 500;

    private static readonly Regex QuadroSenha = new(@"\*000\*[^*#\r\n]*", RegexOptions.CultureInvariant);
    // Troca de senha (*001*antiga*nova*): as duas somem.
    private static readonly Regex QuadroTrocaSenha = new(@"\*001\*[^*#\r\n]*(\*[^*#\r\n]*)?", RegexOptions.CultureInvariant);

    private readonly object _trava = new();
    private readonly List<string> _linhas = [];
    private readonly HashSet<string> _segredos = new(StringComparer.OrdinalIgnoreCase);
    private readonly TimeProvider _relogio;

    public LogBle(TimeProvider? relogio = null) => _relogio = relogio ?? TimeProvider.System;

    /// <summary>Uma linha nova (já mascarada) entrou no log.</summary>
    public event Action<string>? Registrou;

    /// <summary>Faz o log esconder este texto em toda linha daqui em diante.</summary>
    public void ProtegerSegredo(string segredo)
    {
        if (string.IsNullOrEmpty(segredo))
            return;
        lock (_trava)
            _segredos.Add(segredo);
    }

    public void Enviado(string quadro) => Registrar("→ " + quadro);

    public void Recebido(string quadro, string? nota = null) =>
        Registrar("← " + quadro.TrimEnd('\r', '\n') + (nota is null ? "" : $" ({nota})"));

    public void Recusado(string quadro, string motivo) => Registrar($"✗ {quadro} (recusado: {motivo})");

    public void Evento(string texto) => Registrar("· " + texto);

    public IReadOnlyList<string> Linhas
    {
        get
        {
            lock (_trava)
                return _linhas.ToArray();
        }
    }

    /// <summary>O texto com a senha mascarada.</summary>
    public string Mascarar(string texto)
    {
        var saida = QuadroSenha.Replace(texto, "*000*" + Mascara);
        saida = QuadroTrocaSenha.Replace(saida, $"*001*{Mascara}*{Mascara}");
        lock (_trava)
        {
            foreach (var s in _segredos)
                saida = saida.Replace(s, Mascara, StringComparison.OrdinalIgnoreCase);
        }
        return saida;
    }

    private void Registrar(string texto)
    {
        var hora = _relogio.GetLocalNow().ToString("HH:mm:ss.fff");
        var linha = $"{hora} {Mascarar(texto)}";
        lock (_trava)
        {
            _linhas.Add(linha);
            if (_linhas.Count > MaximoDeLinhas)
                _linhas.RemoveRange(0, _linhas.Count - MaximoDeLinhas);
        }
        Registrou?.Invoke(linha);
    }
}
