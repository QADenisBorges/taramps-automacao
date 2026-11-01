using System.Text.RegularExpressions;
using BgsControle.Simulador;

namespace Agente.Testes;

/// <summary>
/// Nenhum dado do sistema de quem usa (Setup, fichas, memórias, endereço, chave) fica nos arquivos versionados de
/// <c>agente/</c>. Os nomes proibidos vêm do cenário de teste do app (só como dado de teste), lidos na hora: nenhum fica
/// escrito aqui.
/// </summary>
public sealed partial class SemSegredoTestes
{
    /// <summary>As pastas fora do git (<c>agente/.gitignore</c>).</summary>
    private static readonly string[] Ignoradas = [".local", "bin", "obj", ".vs", "TestResults"];

    /// <summary>Um IPv4 (o 127.0.0.1 do localhost dos testes e o 0.0.0.0 passam).</summary>
    [GeneratedRegex(@"(?<![\d.])(\d{1,3})\.(\d{1,3})\.(\d{1,3})\.(\d{1,3})(?![\d.])")]
    private static partial Regex Ipv4();

    /// <summary>Uma chave como a do pareamento (32 bytes em Base64Url: 43 caracteres).</summary>
    [GeneratedRegex(@"(?<![A-Za-z0-9_-])[A-Za-z0-9_-]{43}(?![A-Za-z0-9_-])")]
    private static partial Regex Chave();

    /// <summary>Um endereço Bluetooth.</summary>
    [GeneratedRegex(@"\b[0-9A-F]{2}(:[0-9A-F]{2}){5}\b", RegexOptions.IgnoreCase)]
    private static partial Regex Mac();

    private static IEnumerable<string> Versionados()
    {
        var raiz = Caminhos.Agente;
        return Directory.EnumerateFiles(raiz, "*", SearchOption.AllDirectories)
            .Where(f => !Path.GetRelativePath(raiz, f).Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Any(parte => Ignoradas.Contains(parte, StringComparer.OrdinalIgnoreCase)));
    }

    /// <summary>Os nomes do cenário de teste: o Setup, os equipamentos, os modelos, a memória e o preset.</summary>
    private static IReadOnlyList<string> NomesDoCenario()
    {
        var d = CenarioFilme.Dados;
        return d.Setups.Select(s => s.Nome)
            .Concat(d.Setups.SelectMany(s => s.Equipamentos).SelectMany(e => new[] { e.Nome, e.Modelo }))
            .Concat(d.Memorias.Select(m => m.Nome))
            .Append(CenarioFilme.NomePreset)
            .OfType<string>()
            .Select(n => n.Trim())
            .Where(n => n.Length >= 4) // nomes curtos ("Sub") são palavras comuns
            .Distinct()
            .ToArray();
    }

    [Fact]
    public void Nenhum_arquivo_versionado_de_agente_tem_chave_endereco_de_rede_ou_dado_do_cenario()
    {
        var arquivos = Versionados().ToArray();
        Assert.Contains(arquivos, f => f.EndsWith("README.md", StringComparison.Ordinal));
        Assert.Contains(arquivos, f => f.EndsWith("criacao-basica.json", StringComparison.Ordinal));
        var nomes = NomesDoCenario();
        Assert.NotEmpty(nomes);
        string[] dono = ["Den" + "is"]; // o nome de quem usa, montado para não ficar escrito aqui

        var achados = new List<string>();
        foreach (var arquivo in arquivos)
        {
            var texto = File.ReadAllText(arquivo);
            var nome = Path.GetRelativePath(Caminhos.Agente, arquivo);
            foreach (Match m in Ipv4().Matches(texto))
            {
                if (m.Value is not ("127.0.0.1" or "0.0.0.0") && m.Groups.Cast<Group>().Skip(1).All(g => int.Parse(g.Value) <= 255))
                    achados.Add($"{nome}: endereço {m.Value}");
            }
            foreach (Match m in Chave().Matches(texto))
            {
                // Uma chave sorteada mistura maiúsculas e dígitos; um nome de método longo, não.
                if (m.Value.Count(char.IsAsciiDigit) >= 3 && m.Value.Count(char.IsAsciiLetterUpper) >= 3)
                    achados.Add($"{nome}: parece uma chave ({m.Value[..6]}…)");
            }
            foreach (Match m in Mac().Matches(texto))
                achados.Add($"{nome}: endereço Bluetooth {m.Value}");
            foreach (var n in nomes.Concat(dono))
            {
                if (texto.Contains(n, StringComparison.Ordinal))
                    achados.Add($"{nome}: dado do cenário ({n})");
            }
        }

        Assert.True(achados.Count == 0, string.Join("\n", achados));
    }

    [Fact]
    public void O_gitignore_de_agente_deixa_a_pasta_local_fora()
    {
        var gitignore = File.ReadAllLines(Path.Combine(Caminhos.Agente, ".gitignore"));
        Assert.Contains(".local/", gitignore);
    }
}
