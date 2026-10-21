using System.Xml.Linq;

namespace BgsControle.Telas.Testes;

/// <summary>O simulador vive só nos testes, e as telas não dependem do MAUI.</summary>
public sealed class ArquiteturaTestes
{
    /// <summary>A pasta <c>app-controle</c> (a que tem o <c>BgsControle.slnx</c>).</summary>
    private static DirectoryInfo Raiz()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d is not null; d = d.Parent)
        {
            if (File.Exists(Path.Combine(d.FullName, "BgsControle.slnx")))
                return d;
        }
        throw new InvalidOperationException("Não achei o BgsControle.slnx acima da pasta dos testes.");
    }

    private static IEnumerable<string> Referencias(string csproj) =>
        XDocument.Load(csproj).Descendants()
            .Where(e => e.Name.LocalName is "ProjectReference" or "PackageReference" or "Reference")
            .Select(e => (string?)e.Attribute("Include") ?? "");

    private static string[] ProjetosDeSrc() =>
        Directory.GetFiles(Path.Combine(Raiz().FullName, "src"), "*.csproj", SearchOption.AllDirectories);

    [Fact]
    public void Nenhum_projeto_de_src_referencia_o_simulador()
    {
        var projetos = ProjetosDeSrc();
        Assert.Contains(projetos, p => Path.GetFileName(p) == "BgsControle.App.csproj");
        Assert.Contains(projetos, p => Path.GetFileName(p) == "BgsControle.Telas.csproj");

        foreach (var csproj in projetos)
        {
            Assert.DoesNotContain(Referencias(csproj), r => r.Contains("Simulador", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain("Simulador", File.ReadAllText(csproj), StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void O_csproj_do_App_nao_referencia_o_simulador()
    {
        var app = Path.Combine(Raiz().FullName, "src", "BgsControle.App", "BgsControle.App.csproj");

        var referencias = Referencias(app).ToList();

        Assert.Contains(referencias, r => r.EndsWith("BgsControle.Telas.csproj", StringComparison.Ordinal));
        Assert.DoesNotContain(referencias, r => r.Contains("Simulador", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(referencias, r => r.Contains("Testes", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void As_telas_nao_dependem_do_MAUI()
    {
        var telas = Path.Combine(Raiz().FullName, "src", "BgsControle.Telas", "BgsControle.Telas.csproj");
        var xml = XDocument.Load(telas);

        Assert.DoesNotContain(xml.Descendants(), e => e.Name.LocalName == "UseMaui");
        Assert.DoesNotContain(Referencias(telas), r => r.Contains("Maui", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(typeof(ConexaoViewModel).Assembly.GetReferencedAssemblies(),
            a => a.Name!.StartsWith("Microsoft.Maui", StringComparison.Ordinal));
    }

    [Fact]
    public void O_simulador_nao_esta_em_src()
    {
        var src = Path.Combine(Raiz().FullName, "src");

        var arquivos = Directory.GetFiles(src, "*", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                        && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"));

        Assert.DoesNotContain(arquivos, f => Path.GetFileName(f).Contains("Simulad", StringComparison.OrdinalIgnoreCase));
    }
}
