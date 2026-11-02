using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using BgsControle.Nucleo;
using BgsControle.Nucleo.Escrita;
using BgsControle.Nucleo.Setups;
using BgsControle.Simulador;

namespace Agente.Avaliacao;

/// <summary>Um perfil mal formado (o motivo diz o arquivo e o campo).</summary>
public sealed class PerfilInvalidoException(string mensagem) : Exception(mensagem);

/// <summary>O módulo (grupo 2) do Setup do perfil: 4 canais, cada um alimentado pelo canal do processador de mesmo número.</summary>
/// <param name="Ganho">O ganho por canal, em × (medido: as saídas ficam calibradas e a cerca vale).</param>
public sealed record ModuloPerfil(string Nome, string? Modelo, decimal PotenciaCanal, decimal CeifamentoCanal, decimal Ganho);

/// <summary>Uma caixa (alto-falante passivo) num canal do módulo.</summary>
/// <param name="Canal">O canal do processador (0 a 3, o índice do <c>/catalogo</c>), que é também o canal do módulo (1 a 4).</param>
public sealed record CaixaPerfil(int Canal, string Nome, string Categoria, string? Modelo, decimal PotenciaRms, decimal Impedancia);

/// <summary>O Setup do perfil: só o físico (os equipamentos, as fichas e o ambiente).</summary>
/// <param name="Ambiente"><c>sala</c>, <c>carro</c>, <c>ar-livre</c> ou <c>outro</c>.</param>
public sealed record SetupPerfil(string Nome, string Ambiente, ModuloPerfil Modulo, IReadOnlyList<CaixaPerfil> Caixas);

/// <summary>Um alvo do gosto escondido: o valor final que o ouvinte quer no endereço.</summary>
public sealed record AlvoPerfil(string Codigo, int? Indice, decimal Valor)
{
    public Endereco Endereco => new(Codigo, Indice);

    /// <summary>"081[2]" ou "030".</summary>
    public string Rotulo => Indice is { } i ? $"{Codigo}[{i}]" : Codigo;
}

/// <summary>A rota esperada de uma saída com alto-falante: <c>a</c>, <c>b</c> ou <c>a+b</c>.</summary>
public sealed record RotaPerfil(int Canal, string Rota);

/// <summary>O gosto escondido: os alvos e a rota esperada por saída.</summary>
public sealed record GostoPerfil(IReadOnlyList<AlvoPerfil> Alvos, IReadOnlyList<RotaPerfil> Rota);

/// <summary>As frases de refino do ouvinte: uma queixa e um gosto.</summary>
public sealed record RefinoPerfil(string Queixa, string Gosto);

/// <summary>
/// Um ouvinte simulado (spec-agente-memoria CAP-8, <c>cerebro-do-agente.md</c>): um Setup, os fatos que ele sabe dizer em
/// palavras, o gosto escondido (os alvos e a rota) e as frases de refino. Só dados genéricos, nunca de um sistema real.
/// </summary>
public sealed record Perfil(
    string Id,
    string Descricao,
    SetupPerfil Setup,
    IReadOnlyDictionary<string, string> Fatos,
    GostoPerfil Gosto,
    RefinoPerfil Refino)
{
    /// <summary>O nome com que o ouvinte salva a memória.</summary>
    public string NomeMemoria => $"AVAL {Id}";

    /// <summary>O id do Setup no <c>setups.json</c>.</summary>
    public string IdSetup => $"aval-{Id}";
}

/// <summary>Os perfis: a carga e a conferência dos arquivos, e o Setup e o som de partida de cada um no núcleo.</summary>
public static partial class Perfis
{
    /// <summary>A pasta dos perfis, dentro de <c>agente/</c>.</summary>
    public static string Pasta(string pastaAgente) => Path.Combine(pastaAgente, "avaliacao", "perfis");

    public const string IdMemoriaPartida = "aval-partida";

    /// <summary>O nome da memória de partida (a ativa no Setup, igual ao som do aparelho).</summary>
    public const string NomeMemoriaPartida = "PARTIDA";

    /// <summary>O limitador de partida em todos os canais, em dB: abaixo de todo teto, dentro da cerca.</summary>
    public const string LimitadorPartida = "-30.0";

    private const string FonteTeste = "Dado de teste da avaliação";

    private static readonly DateTimeOffset Quando = new(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);

    private static readonly JsonSerializerOptions Formato = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex IdValido();

    /// <summary>Todos os perfis da pasta, em ordem de nome do arquivo.</summary>
    public static IReadOnlyList<Perfil> Carregar(string pasta)
    {
        if (!Directory.Exists(pasta))
            throw new PerfilInvalidoException($"Falta a pasta dos perfis: {pasta}.");
        var perfis = Directory.EnumerateFiles(pasta, "*.json").Order(StringComparer.Ordinal).Select(CarregarArquivo).ToArray();
        var repetido = perfis.GroupBy(p => p.Id).FirstOrDefault(g => g.Count() > 1);
        if (repetido is not null)
            throw new PerfilInvalidoException($"O id {repetido.Key} aparece em mais de um perfil.");
        return perfis;
    }

    public static Perfil CarregarArquivo(string caminho)
    {
        try
        {
            return Conferir(DeJson(File.ReadAllText(caminho)));
        }
        catch (Exception e) when (e is JsonException or PerfilInvalidoException or NotSupportedException)
        {
            throw new PerfilInvalidoException($"{Path.GetFileName(caminho)}: {e.Message}");
        }
    }

    public static Perfil DeJson(string json) =>
        JsonSerializer.Deserialize<Perfil>(json, Formato) ?? throw new PerfilInvalidoException("O perfil está vazio.");

    /// <summary>Confere o perfil inteiro; devolve o mesmo, ou lança com o motivo.</summary>
    public static Perfil Conferir(Perfil p)
    {
        void Exigir(bool ok, string motivo)
        {
            if (!ok)
                throw new PerfilInvalidoException(motivo);
        }

        Exigir(p.Id is { Length: >= 1 and <= 19 } && IdValido().IsMatch(p.Id), "O \"id\" tem de 1 a 19 letras minúsculas, dígitos e hífens.");
        Exigir(!string.IsNullOrWhiteSpace(p.Descricao), "Falta a \"descricao\".");
        var s = p.Setup;
        Exigir(s is not null, "Falta o \"setup\".");
        Exigir(s!.Nome is { Length: >= 1 and <= 24 }, "O \"setup.nome\" tem de 1 a 24 caracteres.");
        Exigir(Ambientes.De(s.Ambiente) is not null, "O \"setup.ambiente\" é sala, carro, ar-livre ou outro.");
        Exigir(s.Modulo is not null && !string.IsNullOrWhiteSpace(s.Modulo.Nome), "Falta o \"setup.modulo\" com o nome.");
        Exigir(s.Modulo!.PotenciaCanal > 0 && s.Modulo.CeifamentoCanal > 0 && s.Modulo.Ganho > 0, "O módulo precisa de potência, ceifamento e ganho maiores que zero.");
        Exigir(s.Caixas is { Count: >= 1 and <= 4 }, "O Setup tem de 1 a 4 caixas.");
        Exigir(s.Caixas.All(c => c is not null), "Um item vazio em \"setup.caixas\".");
        foreach (var c in s.Caixas)
        {
            Exigir(c.Canal is >= 0 and <= 3, $"O canal da caixa {c.Nome} vai de 0 a 3.");
            Exigir(!string.IsNullOrWhiteSpace(c.Nome) && !string.IsNullOrWhiteSpace(c.Categoria), "Cada caixa tem o nome e a categoria.");
            Exigir(c.PotenciaRms > 0 && c.Impedancia > 0, $"A caixa {c.Nome} precisa da potência RMS e da impedância.");
        }
        Exigir(s.Caixas.Select(c => c.Canal).Distinct().Count() == s.Caixas.Count, "Duas caixas no mesmo canal.");
        Exigir(p.Fatos is { Count: > 0 } && p.Fatos.All(f => !string.IsNullOrWhiteSpace(f.Key) && !string.IsNullOrWhiteSpace(f.Value)),
            "Os \"fatos\" são textos, ao menos um.");
        Exigir(p.Gosto is not null && p.Gosto.Alvos is { Count: > 0 } && p.Gosto.Rota is not null, "O \"gosto\" tem os alvos e a rota.");
        Exigir(p.Gosto!.Alvos.All(a => a is not null), "Um item vazio em \"gosto.alvos\".");
        foreach (var a in p.Gosto.Alvos)
        {
            Exigir(CriacaoMemoria.EntraNaCriacao(a.Codigo), $"O alvo {a.Rotulo} não é da regulagem.");
            var par = Catalogo.ParametroDe(a.Codigo)!;
            Exigir(par.Escopo == Escopo.Global ? a.Indice is null : a.Indice is { } i && i >= 0 && i < par.Indices, $"O índice do alvo {a.Rotulo} não vale.");
            Exigir(a.Codigo != Metricas.CodigoRota, "A rota fica em \"gosto.rota\", não nos alvos.");
        }
        Exigir(p.Gosto.Alvos.Select(a => a.Endereco).Distinct().Count() == p.Gosto.Alvos.Count, "Um alvo repetido.");
        var canais = s.Caixas.Select(c => c.Canal).Order().ToArray();
        var rotas = p.Gosto.Rota ?? throw new PerfilInvalidoException("Falta a \"gosto.rota\".");
        Exigir(rotas.All(r => r is not null), "Um item vazio em \"gosto.rota\".");
        Exigir(rotas.Select(r => r.Canal).Order().SequenceEqual(canais), "A \"gosto.rota\" tem uma linha para cada canal com caixa.");
        Exigir(rotas.All(r => r.Rota is "a" or "b" or "a+b"), "A rota é a, b ou a+b.");
        Exigir(p.Refino is not null && !string.IsNullOrWhiteSpace(p.Refino.Queixa) && !string.IsNullOrWhiteSpace(p.Refino.Gosto),
            "O \"refino\" tem a queixa e o gosto.");
        return p;
    }

    // ------------------------------------------------------------------ no núcleo

    /// <summary>
    /// O som de partida do aparelho, igual para todo perfil: o estado de fábrica do simulador (neutro: ganhos 0 dB, filtros
    /// em OFF, rota a/b/a/b, EQs planos), com o limitador em −30 dB em todos os canais (dentro da cerca de todo perfil).
    /// </summary>
    public static IReadOnlyDictionary<(string Codigo, int? Indice), string> EstadoPartida { get; } = MontarPartida();

    private static Dictionary<(string Codigo, int? Indice), string> MontarPartida()
    {
        var estado = new Dictionary<(string Codigo, int? Indice), string>(ProcessadorSimulado.Fabrica);
        foreach (var chave in estado.Keys.Where(k => k.Codigo == "071").ToArray())
            estado[chave] = LimitadorPartida;
        return estado;
    }

    /// <summary>O valor de partida de um endereço, como número (null num texto ou sem valor).</summary>
    public static decimal? NumeroPartida(Endereco e) =>
        EstadoPartida.TryGetValue((e.Codigo, e.Indice), out var noFio)
        && decimal.TryParse(noFio, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : null;

    /// <summary>
    /// O <c>setups.json</c> do perfil: o Setup (o módulo com o ganho medido e as caixas com a ficha oficial) e a memória de
    /// partida, ativa, com a regulagem do <see cref="EstadoPartida"/> (a conferência ao conectar dá "confere").
    /// </summary>
    public static string SetupsJson(Perfil p)
    {
        var s = p.Setup;
        const string idModulo = "modulo";
        DadoTecnico Oficial(decimal v) => new(v, OrigemDado.Oficial, FonteTeste);
        var equipamentos = new List<Equipamento>
        {
            new(idModulo, GrupoEquipamento.Amplificador, "módulo", s.Modulo.Nome, s.Modulo.Modelo)
            {
                Dados = new DadosTecnicos
                {
                    Canais = Oficial(4m),
                    PotenciaCanal = Oficial(s.Modulo.PotenciaCanal),
                    TensaoCeifamentoCanal = Oficial(s.Modulo.CeifamentoCanal),
                    Ganho = new DadoTecnico(s.Modulo.Ganho, OrigemDado.Medido, FonteTeste),
                },
                Entradas = Enumerable.Range(1, 4).Select(n => new Entrada(n, FonteSinal.DoCanal(n - 1))).ToArray(),
            },
        };
        equipamentos.AddRange(s.Caixas.OrderBy(c => c.Canal).Select(c =>
            new Equipamento($"caixa-{c.Canal}", GrupoEquipamento.AltoFalante, c.Categoria, c.Nome, c.Modelo)
            {
                Dados = new DadosTecnicos { PotenciaRms = Oficial(c.PotenciaRms), Impedancia = Oficial(c.Impedancia) },
                Ligado = LigacaoAltoFalante.NoCanal(idModulo, c.Canal + 1),
            }));

        var setup = new Setup(p.IdSetup, s.Nome, Quando, Ambientes.De(s.Ambiente)) { Equipamentos = equipamentos };
        var regulagem = Camadas.Consultas(Camada.Regulagem)
            .Select(c => new ValorCopiado(c.Parametro.Codigo, c.Indice, EstadoPartida[(c.Parametro.Codigo, c.Indice)]))
            .ToArray();
        var memoria = new MemoriaCelular(IdMemoriaPartida, p.IdSetup, NomeMemoriaPartida, Quando, regulagem);
        var dados = new DadosSetups([setup], [memoria], p.IdSetup,
            new Dictionary<string, UsoDoSetup> { [p.IdSetup] = new([IdMemoriaPartida], [IdMemoriaPartida, null, null]) });
        return GerenteSetups.ParaJson(dados);
    }
}
