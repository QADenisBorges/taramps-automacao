using BgsControle.Nucleo.Escrita;

namespace BgsControle.Nucleo.Presets;

/// <summary>
/// Um preset do celular (CAP-5): os valores de áudio do Marco 1 (<see cref="Catalogo.ConsultasMarco1"/>),
/// cada um como o aparelho o devolve. Guardar um preset não manda nada ao aparelho.
/// </summary>
/// <param name="Id">Único e estável (um herdado tem o id fixo do código).</param>
/// <param name="Nome">De 1 a 24 caracteres (a regra está em <see cref="GerentePresets.NomeValido"/>).</param>
/// <param name="Quando">Quando o som foi guardado (o herdado: quando a leitura foi feita).</param>
/// <param name="Valores">Os valores do Marco 1, na ordem do catálogo.</param>
/// <param name="Herdado">Vem do conhecimento dos protótipos (CAP-6): fica sempre na lista e não muda.</param>
/// <param name="Nota">A nota herdada, quando há.</param>
public sealed record Preset(
    string Id,
    string Nome,
    DateTimeOffset Quando,
    IReadOnlyList<ValorCopiado> Valores,
    bool Herdado = false,
    string? Nota = null)
{
    /// <summary>Os valores como uma "cópia" sem marca: o alvo do cálculo do que difere do espelho.</summary>
    internal Copia ComoCopia() => new(Quando, Valores, null);
}

/// <summary>Como terminou uma operação de preset. <see cref="Mensagem"/> é a frase para a tela.</summary>
/// <param name="Ok">A operação valeu.</param>
/// <param name="Motivo">Por que não valeu, ou null.</param>
/// <param name="Mensagem">A frase para a tela (também nas falhas).</param>
/// <param name="Preset">O preset guardado, quando a operação criou um.</param>
public sealed record ResultadoPreset(bool Ok, string? Motivo, string? Mensagem = null, Preset? Preset = null)
{
    public static ResultadoPreset Falha(string motivo) => new(false, motivo, motivo);
}
