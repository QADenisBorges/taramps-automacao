using System.Text.RegularExpressions;
using BgsControle.Simulador;

namespace BgsControle.Telas.Testes;

/// <summary>
/// Um id para cada suposição: a lista de <see cref="Suposicoes"/> bate com a do
/// <c>SUPOSICOES.md</c> (id e texto), todo id marcado no código (<c>Suposição &lt;ID&gt;</c>, no
/// simulador e no núcleo) está nas duas listas, e todo id das listas está marcado no código.
/// </summary>
public sealed class SuposicoesTestes
{
    private static readonly Regex Marca = new(@"Suposição ([A-Z0-9]+(?:-[A-Z0-9]+)*)(?![A-Za-z0-9-])");
    private static readonly Regex Palavra = new("suposição", RegexOptions.IgnoreCase);
    private static readonly Regex LinhaDaTabela = new(@"^\|\s*([A-Z0-9]+(?:-[A-Z0-9]+)*)\s*\|\s*(.+?)\s*\|(?:[^|]*\|){2}\s*$");

    /// <summary>A pasta <c>app-controle</c> (a que tem o <c>BgsControle.slnx</c>).</summary>
    private static string Raiz()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d is not null; d = d.Parent)
        {
            if (File.Exists(Path.Combine(d.FullName, "BgsControle.slnx")))
                return d.FullName;
        }
        throw new InvalidOperationException("Não achei o BgsControle.slnx acima da pasta dos testes.");
    }

    private static string PastaSimulador => Path.Combine(Raiz(), "tests", "BgsControle.Simulador");

    /// <summary>O código onde as suposições são marcadas: o simulador (menos a lista) e o núcleo.</summary>
    private static List<string> Fontes()
    {
        var simulador = Directory.EnumerateFiles(PastaSimulador, "*.cs", SearchOption.TopDirectoryOnly)
            .Where(f => Path.GetFileName(f) != "Suposicoes.cs");
        var nucleo = Directory.EnumerateFiles(Path.Combine(Raiz(), "src", "BgsControle.Nucleo"), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Split(Path.DirectorySeparatorChar).Any(p => p is "bin" or "obj"));
        return simulador.Concat(nucleo).ToList();
    }

    private static Dictionary<string, string> DoMarkdown()
    {
        var saida = new Dictionary<string, string>();
        foreach (var l in File.ReadAllLines(Path.Combine(PastaSimulador, "SUPOSICOES.md")))
        {
            var m = LinhaDaTabela.Match(l);
            if (!m.Success)
                continue;
            Assert.True(saida.TryAdd(m.Groups[1].Value, m.Groups[2].Value), $"Id repetido no SUPOSICOES.md: {m.Groups[1].Value}");
        }
        return saida;
    }

    private static List<(string Arquivo, string Id)> MarcasNoCodigo() =>
        Fontes()
            .SelectMany(f => Marca.Matches(File.ReadAllText(f)).Select(m => (Path.GetFileName(f), m.Groups[1].Value)))
            .ToList();

    [Fact]
    public void O_codigo_e_o_md_tem_as_mesmas_suposicoes_com_o_mesmo_texto()
    {
        var md = DoMarkdown();

        Assert.NotEmpty(md);
        Assert.Equal(Suposicoes.Todas.Keys.Order(), md.Keys.Order());
        foreach (var (id, texto) in Suposicoes.Todas)
            Assert.Equal(texto, md[id]);
    }

    [Fact]
    public void Toda_marca_no_codigo_esta_na_lista()
    {
        var marcas = MarcasNoCodigo();

        Assert.NotEmpty(marcas);
        foreach (var (arquivo, id) in marcas)
            Assert.True(Suposicoes.Todas.ContainsKey(id), $"\"Suposição {id}\" ({arquivo}) não está em Suposicoes nem no SUPOSICOES.md");
    }

    [Fact]
    public void Cada_suposicao_da_lista_esta_marcada_no_codigo()
    {
        var marcadas = MarcasNoCodigo().Select(m => m.Id).ToHashSet();

        foreach (var id in Suposicoes.Todas.Keys)
            Assert.True(marcadas.Contains(id), $"Falta a marca \"Suposição {id}\" no simulador ou no núcleo");
    }

    [Fact]
    public void Nenhuma_suposicao_no_codigo_fica_sem_id()
    {
        foreach (var f in Fontes())
        {
            var texto = File.ReadAllText(f);
            var comId = Marca.Matches(texto).Select(m => m.Index).ToHashSet();
            foreach (Match m in Palavra.Matches(texto))
                Assert.True(comId.Contains(m.Index), $"{Path.GetFileName(f)}: \"{texto.Substring(m.Index, Math.Min(60, texto.Length - m.Index))}\" sem id");
        }
    }

    [Fact]
    public void As_constantes_sao_as_chaves_da_lista()
    {
        var constantes = typeof(Suposicoes).GetFields()
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToList();

        Assert.Equal(constantes.Order(), Suposicoes.Todas.Keys.Order());
    }

    [Fact]
    public void O_ack_do_240_e_medido_e_o_do_241_e_suposicao()
    {
        var md = File.ReadAllText(Path.Combine(PastaSimulador, "SUPOSICOES.md"));

        Assert.Contains("estado/log-ble/2026-10-20_1322-memoria.log", md);
        Assert.Contains(Suposicoes.Ack241, Suposicoes.Todas.Keys);
        // O app não salva memórias do aparelho desde a história 8c: nenhuma suposição sobre o 240.
        Assert.DoesNotContain(Suposicoes.Todas.Keys, id => id.Contains("240"));
        Assert.Equal(TimeSpan.FromMilliseconds(1170), ProcessadorSimulado.AtrasoAckSalvarReal);
    }
}
