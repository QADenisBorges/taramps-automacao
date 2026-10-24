namespace BgsControle.Nucleo.Setups;

/// <summary>Uma diferença por canal antes de virar linha: o código, o índice no fio, o canal (só nos parâmetros por canal), o rótulo-base e os dois valores.</summary>
/// <param name="Canal">O canal (0 a 3) de um parâmetro por canal; null num global (MASTER, EQ de entrada, bandas).</param>
/// <param name="Base">"Atraso", "Equilíbrio"; num global, o rótulo inteiro ("Ganho do EQ de entrada").</param>
/// <param name="Sufixo">O que vem depois do nome da saída (" · ganho" no equilíbrio).</param>
public sealed record DiferencaPorCanal(string Codigo, int? Indice, int? Canal, string Base, string Antes, string Depois, string Sufixo = "")
{
    /// <summary>Uma diferença de um valor do catálogo: por canal quando o parâmetro é por canal.</summary>
    public static DiferencaPorCanal De(string codigo, int? indice, string antes, string depois)
    {
        var porCanal = Catalogo.ParametroDe(codigo)?.Escopo == Escopo.Canal && indice is not null;
        return new(codigo, indice, porCanal ? indice : null, Camadas.Rotulo(codigo, porCanal ? null : indice), antes, depois);
    }
}

/// <summary>Uma linha pronta: o rótulo pelo nome da saída e o canal mestre dela (a cor), ou null num global.</summary>
public sealed record LinhaRotulada(string Codigo, int? Indice, string Rotulo, string Antes, string Depois, int? Canal);

/// <summary>
/// Os rótulos da folha do Salvar e da lista "O que não bate" pelo nome da saída do Setup (história 10;
/// EXPERIENCE, "Atraso · Sub: 80 → 85 cm"). O canal mestre de uma saída é a saída: "Atraso · Sub". O
/// outro canal de um par com link entra na mesma linha quando muda igual; senão, ganha a linha dele, com
/// o canal ("Atraso · Sub · CH4"). Os globais e o que não é de um canal ficam como estão.
/// </summary>
public static class RotulosSaida
{
    public static IReadOnlyList<LinhaRotulada> Juntar(IEnumerable<DiferencaPorCanal> diferencas, IReadOnlyList<SaidaApp> saidas)
    {
        var nomes = Ligacoes.NomesNaTela(saidas);
        var lista = diferencas.ToList();
        var usados = new bool[lista.Count];
        var linhas = new List<LinhaRotulada>();
        for (var i = 0; i < lista.Count; i++)
        {
            if (usados[i])
                continue;
            var d = lista[i];
            var k = d.Canal is { } canal ? IndiceDaSaida(saidas, canal) : -1;
            if (k < 0)
            {
                usados[i] = true;
                linhas.Add(new(d.Codigo, d.Indice, d.Base + d.Sufixo, d.Antes, d.Depois, d.Canal));
                continue;
            }

            // Todas as diferenças desta saída com o mesmo código e o mesmo rótulo, na ordem.
            var s = saidas[k];
            var grupo = new List<DiferencaPorCanal>();
            for (var j = i; j < lista.Count; j++)
            {
                var o = lista[j];
                if (usados[j] || o.Codigo != d.Codigo || o.Base != d.Base || o.Sufixo != d.Sufixo || o.Canal is not { } c || !s.Canais.Contains(c))
                    continue;
                usados[j] = true;
                grupo.Add(o);
            }

            var rotulo = $"{d.Base} · {nomes[k]}"; // sem artigo: o app não sabe o gênero do nome
            var mestre = grupo.FirstOrDefault(o => o.Canal == s.Mestre);
            if (mestre is not null)
                linhas.Add(new(mestre.Codigo, mestre.Indice, rotulo + d.Sufixo, mestre.Antes, mestre.Depois, s.Mestre));
            foreach (var o in grupo.Where(o => o.Canal != s.Mestre))
            {
                if (mestre is not null && o.Antes == mestre.Antes && o.Depois == mestre.Depois)
                    continue; // o par mudou igual: uma linha só
                linhas.Add(new(o.Codigo, o.Indice, $"{rotulo}{d.Sufixo} · CH{o.Canal!.Value + 1}", o.Antes, o.Depois, s.Mestre));
            }
        }
        return linhas;
    }

    private static int IndiceDaSaida(IReadOnlyList<SaidaApp> saidas, int canal)
    {
        for (var k = 0; k < saidas.Count; k++)
        {
            if (saidas[k].Canais.Contains(canal))
                return k;
        }
        return -1;
    }
}
