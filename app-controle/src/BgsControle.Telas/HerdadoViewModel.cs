using BgsControle.Nucleo.Herdado;

namespace BgsControle.Telas;

/// <summary>
/// O conhecimento herdado dos protótipos (CAP-6) para as telas: as seções de texto (Protocolo, O
/// sistema do Denis, As 3 memórias, Presets, Preferências e método, Cuidados com o app da Taramps,
/// Situação registrada) e o texto do selo HERDADO. Está pronto na primeira abertura, sem arquivo e
/// sem digitar nada: vem embutido no app.
/// </summary>
public sealed class HerdadoViewModel
{
    /// <summary>O texto do selo que marca o que veio dos protótipos (memórias e presets).</summary>
    public const string TextoSelo = "HERDADO";

    public string Selo => TextoSelo;

    public IReadOnlyList<SecaoHerdada> Secoes => ConhecimentoHerdado.Secoes;

    /// <summary>A seção pelo título, ou null.</summary>
    public SecaoHerdada? Secao(string titulo) => Secoes.FirstOrDefault(s => s.Titulo == titulo);

    /// <summary>A nota herdada da memória pela posição (1 a 3), ou null.</summary>
    public NotaMemoriaHerdada? NotaMemoria(int numero) => ConhecimentoHerdado.NotaMemoria(numero);
}
