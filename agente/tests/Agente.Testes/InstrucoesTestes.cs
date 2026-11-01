using System.Text.RegularExpressions;
using Agente.Servico;
using BgsControle.Nucleo.Porta;

namespace Agente.Testes;

/// <summary>
/// As instruções em camadas (história 14d), sem modelo: a base curta, as skills, o conhecimento com fonte e as tarefas.
/// As linhas Skills carregam, Fontes, Só a porta, Ferramentas e Base curta da matriz.
/// </summary>
public sealed partial class InstrucoesTestes
{
    /// <summary>As skills, uma por função (a cerca-fisica é código do app, não skill).</summary>
    public static IReadOnlyList<string> Skills { get; } = ["objetivo", "escuta", "diagnostico", "receita", "ficha-tecnica", "medicao-assistida"];

    private const string SoPorta = "Bash(porta:*)";

    private static string Pasta(string nome) => Path.Combine(Caminhos.Agente, nome);

    private static string Rel(string arquivo) => Path.GetRelativePath(Caminhos.Agente, arquivo).Replace('\\', '/');

    /// <summary>Todos os arquivos das camadas: a base, as skills, o conhecimento e as tarefas.</summary>
    private static IReadOnlyList<string> ArquivosDasCamadas() =>
        new[] { LancadorClaude.PastaInstrucoes, LancadorClaude.PastaSkills, LancadorClaude.PastaConhecimento, LancadorClaude.PastaTarefas }
            .SelectMany(p => Directory.EnumerateFiles(Pasta(p), "*", SearchOption.AllDirectories))
            .ToArray();

    /// <summary>O frontmatter (as linhas <c>chave: valor</c> entre os dois <c>---</c>) e o corpo de um SKILL.md.</summary>
    private static (Dictionary<string, string> Campos, string Corpo) Ler(string arquivo)
    {
        var linhas = File.ReadAllLines(arquivo);
        Assert.True(linhas.Length > 0 && linhas[0].Trim() == "---", $"{Rel(arquivo)}: começa sem o frontmatter (---)");
        var fim = Array.FindIndex(linhas, 1, l => l.Trim() == "---");
        Assert.True(fim > 0, $"{Rel(arquivo)}: o frontmatter não fecha (---)");
        var campos = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var linha in linhas[1..fim])
        {
            var dois = linha.IndexOf(':');
            Assert.True(dois > 0, $"{Rel(arquivo)}: linha do frontmatter sem \"chave: valor\": {linha}");
            campos[linha[..dois].Trim()] = linha[(dois + 1)..].Trim();
        }
        return (campos, string.Join('\n', linhas[(fim + 1)..]));
    }

    /// <summary>O texto de uma seção <c>## Título</c> até a próxima seção de mesmo nível (ou o fim).</summary>
    private static string? Secao(string texto, string titulo)
    {
        var m = Regex.Match(texto, $@"^## {Regex.Escape(titulo)}[ \t]*\r?$", RegexOptions.Multiline);
        if (!m.Success)
            return null;
        var resto = texto[(m.Index + m.Length)..];
        var proxima = Regex.Match(resto, @"^## ", RegexOptions.Multiline);
        return proxima.Success ? resto[..proxima.Index] : resto;
    }

    [GeneratedRegex(@"`(conhecimento/[^`\s]+\.md)`")]
    private static partial Regex ArquivoConhecimento();

    [Fact]
    public void Cada_skill_tem_nome_igual_a_pasta_descricao_ferramentas_Entrega_e_Fontes()
    {
        var pastas = Directory.EnumerateDirectories(Pasta(LancadorClaude.PastaSkills)).Select(Path.GetFileName).Order();
        Assert.Equal(Skills.Order(), pastas);

        var falhas = new List<string>();
        foreach (var skill in Skills)
        {
            var arquivo = Path.Combine(Pasta(LancadorClaude.PastaSkills), skill, "SKILL.md");
            if (!File.Exists(arquivo))
            {
                falhas.Add($"{skill}: falta o SKILL.md");
                continue;
            }
            var (campos, corpo) = Ler(arquivo);
            if (campos.GetValueOrDefault("name") != skill)
                falhas.Add($"{skill}: o name não é igual à pasta ({campos.GetValueOrDefault("name")})");
            if (campos.GetValueOrDefault("description") is not { Length: > 0 and <= 1024 })
                falhas.Add($"{skill}: a description falta ou passa de 1024 caracteres");
            // Um valor sem aspas que o YAML recusaria (": ", " #" ou um começo especial) faz o Claude Code pular a skill.
            foreach (var (chave, valor) in campos)
            {
                if (valor.Contains(": ", StringComparison.Ordinal) || valor.Contains(" #", StringComparison.Ordinal)
                    || (valor.Length > 0 && "\"'[{&*!|>%@`".Contains(valor[0])))
                    falhas.Add($"{skill}: o {chave} não é um valor simples de YAML");
            }
            if (string.IsNullOrWhiteSpace(campos.GetValueOrDefault("allowed-tools")))
                falhas.Add($"{skill}: falta o allowed-tools");
            if (string.IsNullOrWhiteSpace(Secao(corpo, "Entrega")))
                falhas.Add($"{skill}: falta a seção ## Entrega");
            if (string.IsNullOrWhiteSpace(Secao(corpo, "Fontes")))
                falhas.Add($"{skill}: falta a seção ## Fontes");
        }
        Assert.True(falhas.Count == 0, string.Join("\n", falhas));
    }

    [Fact]
    public void As_Fontes_de_cada_skill_existem_e_cada_regra_do_conhecimento_tem_a_sua_Fonte()
    {
        var falhas = new List<string>();
        foreach (var skill in Skills)
        {
            var (_, corpo) = Ler(Path.Combine(Pasta(LancadorClaude.PastaSkills), skill, "SKILL.md"));
            var citados = ArquivoConhecimento().Matches(Secao(corpo, "Fontes") ?? "").Select(m => m.Groups[1].Value).ToArray();
            if (citados.Length == 0)
                falhas.Add($"{skill}: as Fontes não citam nenhum arquivo de conhecimento/");
            foreach (var c in citados.Where(c => !File.Exists(Path.Combine(Caminhos.Agente, c))))
                falhas.Add($"{skill}: cita {c}, que não existe");
        }

        var arquivos = Directory.EnumerateFiles(Pasta(LancadorClaude.PastaConhecimento), "*.md").ToArray();
        Assert.NotEmpty(arquivos);
        foreach (var arquivo in arquivos)
        {
            var regras = Secao(File.ReadAllText(arquivo), "Regras");
            if (regras is null)
            {
                falhas.Add($"{Rel(arquivo)}: falta a seção ## Regras");
                continue;
            }
            // Cada regra é um item "- " no começo da linha; as linhas recuadas embaixo são dela.
            var itens = Regex.Split(regras, @"^- ", RegexOptions.Multiline).Skip(1).ToArray();
            if (itens.Length == 0)
                falhas.Add($"{Rel(arquivo)}: nenhuma regra");
            foreach (var item in itens)
            {
                if (!Regex.IsMatch(item, @"^[ \t]*Fonte:[ \t]*\S", RegexOptions.Multiline))
                    falhas.Add($"{Rel(arquivo)}: regra sem Fonte: \"{item.Split('\n')[0].Trim()}\"");
            }
        }
        Assert.True(falhas.Count == 0, string.Join("\n", falhas));
    }

    [Fact]
    public void As_camadas_falam_so_pela_porta_e_todo_alvo_citado_existe_na_ajuda()
    {
        string[] proibidos = [@"\bcurl\b", @"\bwget\b", @"Invoke-WebRequest", @"\badb\b", @"http://", @"8787"];
        var falhas = new List<string>();
        var alvos = 0;
        foreach (var arquivo in ArquivosDasCamadas())
        {
            var texto = File.ReadAllText(arquivo);
            foreach (var p in proibidos.Where(p => Regex.IsMatch(texto, p, RegexOptions.IgnoreCase)))
                falhas.Add($"{Rel(arquivo)}: usa {p}");
            foreach (var alvo in AlvosCitados(texto))
            {
                alvos++;
                if (!ExisteNaAjuda(alvo))
                    falhas.Add($"{Rel(arquivo)}: o alvo {alvo} não existe no /ajuda");
            }
        }
        Assert.True(falhas.Count == 0, string.Join("\n", falhas));
        Assert.True(alvos > 10, $"só {alvos} alvos citados: a busca dos alvos não está achando nada");
    }

    [Theory]
    [InlineData("""porta --json '{"acao":"ler","alvo":"/nao-existe"}'""")]
    [InlineData("mande `POST /criacao/inventada`")]
    [InlineData("leia o `/caminho-novo`")]
    public void Um_alvo_que_nao_existe_e_apontado(string texto)
    {
        var alvo = Assert.Single(AlvosCitados(texto));
        Assert.False(ExisteNaAjuda(alvo), alvo);
    }

    [Fact]
    public void As_ferramentas_de_cada_skill_sao_so_a_porta_e_a_ficha_tecnica_le_a_web()
    {
        foreach (var skill in Skills)
        {
            var (campos, _) = Ler(Path.Combine(Pasta(LancadorClaude.PastaSkills), skill, "SKILL.md"));
            var ferramentas = campos["allowed-tools"].Split([',', ' ', '\t'], StringSplitOptions.RemoveEmptyEntries).Order().ToArray();
            string[] esperadas = skill == "ficha-tecnica" ? [SoPorta, "WebFetch", "WebSearch"] : [SoPorta];
            Assert.True(esperadas.Order().SequenceEqual(ferramentas), $"{skill}: {campos["allowed-tools"]}");
        }
        // A configuração do serviço dá à tarefa o mesmo que a skill dela pede.
        var config = Caminhos.Config.Lancador;
        Assert.Equal([SoPorta], config.FerramentasDe("criar-memoria"));
        Assert.Equal([SoPorta, "WebSearch", "WebFetch"], config.FerramentasDe("ficha-tecnica"));
    }

    [Fact]
    public void A_base_e_curta_e_traz_as_regras_inviolaveis()
    {
        var texto = File.ReadAllText(Path.Combine(Pasta(LancadorClaude.PastaInstrucoes), LancadorClaude.ArquivoBase));
        var linhas = texto.Split('\n').Count(l => !string.IsNullOrWhiteSpace(l));
        Assert.True(linhas <= 40, $"a base tem {linhas} linhas não vazias (até 40)");

        // Cada regra inviolável, por um trecho que a diz.
        string[] trechos =
        [
            "GET /limites", "cerca", // a cerca do Rendimento, sempre
            "sem motivo", // nenhum valor sem motivo
            "ordem da física",
            "não salva", // salvar é do usuário
            "\"IA\"", // nenhum texto para a tela com a palavra
            "só pela porta", // os dados do caso
            "Todo padrão é dúvida", "direção fixa", "uso fixo",
            "pedido", "calculado", "recomendado", "estimativa",
        ];
        Assert.All(trechos, t => Assert.Contains(t, texto, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void As_tarefas_dizem_a_skill_de_cada_passo_e_o_esqueleto_saiu()
    {
        var criar = LancadorClaude.Prompt(Caminhos.Agente, "criar-memoria");
        var ficha = LancadorClaude.Prompt(Caminhos.Agente, "ficha-tecnica");

        Assert.All(new[] { criar, ficha }, t => Assert.DoesNotContain("Esqueleto da história 14a", t));
        Assert.All(new[] { "objetivo", "receita", "escuta", "diagnostico", "medicao-assistida" }, s => Assert.Contains($"**{s}**", criar));
        Assert.Contains("**ficha-tecnica**", ficha);
        Assert.Contains(LancadorClaude.TituloEvento, ficha);
        Assert.All(new[] { "`modelo`", "`grupo`", "`categoria`", "`idEquipamento`" }, c => Assert.Contains(c, ficha));
    }

    // ------------------------------------------------------------------ os alvos

    [GeneratedRegex(@"""alvo""\s*:\s*""(/[A-Za-z0-9/_-]*)")]
    private static partial Regex AlvoJson();

    [GeneratedRegex(@"\b(?:GET|POST|DELETE)\s+(/[A-Za-z0-9/_-]+)")]
    private static partial Regex AlvoComMetodo();

    [GeneratedRegex(@"`(/[A-Za-z0-9/_-]+)[^`]*`")]
    private static partial Regex AlvoEntreCrases();

    /// <summary>Os caminhos citados: no JSON (<c>"alvo":"/…"</c>), depois de um método (<c>POST /…</c>) e entre crases.</summary>
    private static IReadOnlyList<string> AlvosCitados(string texto) =>
        AlvoJson().Matches(texto).Concat(AlvoComMetodo().Matches(texto)).Concat(AlvoEntreCrases().Matches(texto))
            .Select(m => m.Groups[1].Value.TrimEnd('/'))
            .Where(a => a.Length > 1)
            .Distinct()
            .ToArray();

    /// <summary>O caminho aparece inteiro no texto do <c>GET /ajuda</c> da porta (não só como começo de outro).</summary>
    private static bool ExisteNaAjuda(string alvo) =>
        Regex.IsMatch(PortaComandos.TextoAjuda, $@"(?<![\w/-]){Regex.Escape(alvo)}(?![\w/-])");
}
