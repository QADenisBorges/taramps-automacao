using BgsControle.Nucleo.Seguranca;

namespace BgsControle.Nucleo.Uso;

/// <summary>
/// A origem ambiente de um comando (CAP-13): quem pediu o que vai chegar à porta
/// (<see cref="Sessao.EnviarAsync"/>). Ela segue o fluxo assíncrono (<see cref="AsyncLocal{T}"/>),
/// para não ter de atravessar todos os gerentes como parâmetro. Sem escopo, a origem é a tela:
/// o caminho da tela não muda. A IA (9b) entra por um escopo explícito, e o desligar automático
/// do gerador vai como <see cref="OrigemComando.App"/>.
/// <code>
/// using (Origem.Como(OrigemComando.Ia))
///     await escritor.EscreverAsync(ganho, 2, 2.0m);
/// </code>
/// </summary>
public static class Origem
{
    private static readonly AsyncLocal<OrigemComando?> Valor = new();

    /// <summary>A origem do fluxo atual; sem escopo, a tela.</summary>
    public static OrigemComando Atual => Valor.Value ?? OrigemComando.Tela;

    /// <summary>Daqui até o fim do <c>using</c>, os comandos deste fluxo levam esta origem.</summary>
    public static IDisposable Como(OrigemComando origem)
    {
        var antes = Valor.Value;
        Valor.Value = origem;
        return new Escopo(antes);
    }

    private sealed class Escopo(OrigemComando? antes) : IDisposable
    {
        private bool _fechado;

        public void Dispose()
        {
            if (_fechado)
                return;
            _fechado = true;
            Valor.Value = antes;
        }
    }
}
