using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Agente.Porta;

namespace Agente.Servico;

/// <summary>A saída da ferramenta <c>aprendizado</c>: <c>{"ok":true,...}</c> com os campos da ação, ou <c>{"ok":false,"motivo":"..."}</c>.</summary>
public sealed record SaidaAprendizado(bool Ok, JsonObject? Campos, string? Motivo)
{
    public static SaidaAprendizado Feito(JsonObject campos) => new(true, campos, null);

    public static SaidaAprendizado Recusa(string motivo) => new(false, null, motivo);

    /// <summary>0 no ok, 1 na recusa.</summary>
    public int CodigoSaida => Ok ? 0 : 1;

    public JsonObject ParaObjeto()
    {
        var o = new JsonObject { ["ok"] = Ok };
        if (Ok)
        {
            foreach (var (chave, valor) in Campos ?? [])
                o[chave] = valor?.DeepClone();
        }
        else
        {
            o["motivo"] = Motivo;
        }
        return o;
    }

    /// <summary>Um JSON numa linha, com os acentos como texto.</summary>
    public string ParaJson() => ParaObjeto().ToJsonString(RespostaFerramenta.Formato);
}

/// <summary>
/// O aprendizado em arquivos (história 14e, spec-agente-memoria CAP-7), tudo em <c>&lt;pasta local&gt;/aprendizado/</c>, fora
/// do repositório, e começa vazio:
/// <list type="bullet">
/// <item><b><c>agora.json</c></b>: a situação, as variáveis e os "Outro…" da criação de agora, ditos pelo Agente pela
/// ferramenta <c>aprendizado</c> (<see cref="Executar"/>). O serviço o apaga no fim da criação.</item>
/// <item><b><c>registros/&lt;hora UTC&gt;-&lt;n&gt;.json</c></b>: um registro por criação, gravado pelo serviço no fim
/// (<see cref="MontarRegistro"/>), e os <c>ajustesDepois</c> vistos ao abrir a criação seguinte
/// (<see cref="ConferirAjustesDepois"/>).</item>
/// <item><b><c>perfis/&lt;chave&gt;.json</c></b>: o perfil de uma situação (<see cref="CalcularPerfil"/>). Uma variável entra
/// quando dois ou mais registros salvos dessa situação, de memórias diferentes, têm a mesma escolha: números a até
/// <see cref="MesmaEscolhaDb"/> um do outro (vale a média) ou o mesmo texto.</item>
/// </list>
/// O Agente só nomeia a situação e diz as variáveis; o registro e a regra do perfil são deste código.
/// </summary>
public static partial class Aprendizado
{
    public const string Pasta = "aprendizado";
    public const string ArquivoAgora = "agora.json";
    public const string PastaRegistros = "registros";
    public const string PastaPerfis = "perfis";

    /// <summary>O nome da situação tem de 1 a 40 caracteres.</summary>
    public const int MaximoNome = 40;

    /// <summary>Dois números são a mesma escolha a até 1 dB um do outro.</summary>
    public const double MesmaEscolhaDb = 1.0;

    /// <summary>Quantas memórias diferentes com a mesma escolha mudam o perfil.</summary>
    public const int MinimoMemorias = 2;

    /// <summary>Um ajuste à mão conta quando o valor muda mais que isto.</summary>
    public const double MinimoAjusteDepois = 0.05;

    // As ações da ferramenta.
    public const string AcaoSituacao = "situacao";
    public const string AcaoVariaveis = "variaveis";
    public const string AcaoOutro = "outro";
    public const string AcaoPerfil = "perfil";

    // Os fins de uma criação.
    public const string FimSalva = "salva";
    public const string FimCancelada = "cancelada";
    public const string FimInterrompida = "interrompida";

    /// <summary>O tipo do evento do fim → o fim no registro.</summary>
    public static IReadOnlyDictionary<string, string> FimDoEvento { get; } = new Dictionary<string, string>
    {
        ["criacao-salva"] = FimSalva,
        ["criacao-cancelada"] = FimCancelada,
        ["criacao-interrompida"] = FimInterrompida,
    };

    public const string MsgAcao = "A \"acao\" é situacao, variaveis, outro ou perfil.";

    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    private static readonly string[] NomesReservados =
    [
        "con", "prn", "aux", "nul",
        "com1", "com2", "com3", "com4", "com5", "com6", "com7", "com8", "com9",
        "lpt1", "lpt2", "lpt3", "lpt4", "lpt5", "lpt6", "lpt7", "lpt8", "lpt9",
    ];

    public static string PastaDe(string pastaLocal) => Path.Combine(pastaLocal, Pasta);

    public static string CaminhoAgora(string pastaLocal) => Path.Combine(PastaDe(pastaLocal), ArquivoAgora);

    public static string PastaDeRegistros(string pastaLocal) => Path.Combine(PastaDe(pastaLocal), PastaRegistros);

    public static string CaminhoPerfil(string pastaLocal, string chave) => Path.Combine(PastaDe(pastaLocal), PastaPerfis, NomeArquivoPerfil(chave));

    // ------------------------------------------------------------------ a chave

    /// <summary>
    /// A chave de um nome: em minúsculas, sem acento, sem espaço nas pontas e com os espaços internos colapsados ("Música ",
    /// "musica" e "MÚSICA" dão "musica").
    /// </summary>
    public static string Chave(string texto)
    {
        var decomposto = texto.Trim().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposto.Length);
        var espaco = false;
        foreach (var c in decomposto)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
                continue;
            if (char.IsWhiteSpace(c))
            {
                espaco = true;
                continue;
            }
            if (espaco && sb.Length > 0)
                sb.Append(' ');
            espaco = false;
            sb.Append(char.ToLowerInvariant(c));
        }
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    [GeneratedRegex("^[a-z0-9-]+$")]
    private static partial Regex NomeSimples();

    /// <summary>
    /// O nome do arquivo do perfil: <c>&lt;chave&gt;.json</c> quando a chave só tem letras sem acento, números e hífen; senão
    /// (espaço, barra, um nome reservado do Windows), a chave com hífens e um resumo dela, para nunca sair da pasta.
    /// </summary>
    public static string NomeArquivoPerfil(string chave)
    {
        if (NomeSimples().IsMatch(chave) && !NomesReservados.Contains(chave))
            return chave + ".json";
        var slug = new string(chave.Select(c => c is >= 'a' and <= 'z' or >= '0' and <= '9' ? c : '-').ToArray()).Trim('-');
        var resumo = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(chave)))[..8];
        return (slug.Length > 0 ? slug + "-" : "") + resumo + ".json";
    }

    // ------------------------------------------------------------------ a ferramenta

    /// <summary>
    /// Uma entrada da ferramenta <c>aprendizado</c>. Nunca lança: um erro sai como recusa, com o motivo.
    /// <list type="bullet">
    /// <item><c>{"acao":"situacao","nome":"música"}</c>: a situação da criação de agora (1 a 40 caracteres).</item>
    /// <item><c>{"acao":"variaveis","valores":{"grave":4.0,"cobertura":"varios"}}</c>: junta as variáveis (número ou texto).</item>
    /// <item><c>{"acao":"outro","texto":"som de show","variavel":"grave"}</c>: liga um "Outro…" a uma variável.</item>
    /// <item><c>{"acao":"perfil","situacao":"música"}</c>: o perfil da situação (vazio, sem perfil).</item>
    /// </list>
    /// </summary>
    public static SaidaAprendizado Executar(string? entrada, string pastaLocal)
    {
        if (string.IsNullOrWhiteSpace(entrada))
            return SaidaAprendizado.Recusa("Entrada vazia: mande um JSON com a \"acao\" (situacao, variaveis, outro ou perfil).");
        JsonObject o;
        try
        {
            if (JsonNode.Parse(entrada) is not JsonObject objeto)
                return SaidaAprendizado.Recusa("A entrada precisa ser um objeto JSON.");
            o = objeto;
        }
        catch (JsonException e)
        {
            return SaidaAprendizado.Recusa($"A entrada não é um JSON válido ({e.Message}).");
        }

        try
        {
            return (Texto(o["acao"])?.Trim().ToLowerInvariant()) switch
            {
                AcaoSituacao => GuardarSituacao(o, pastaLocal),
                AcaoVariaveis => GuardarVariaveis(o, pastaLocal),
                AcaoOutro => GuardarOutro(o, pastaLocal),
                AcaoPerfil => LerPerfil(o, pastaLocal),
                _ => SaidaAprendizado.Recusa(MsgAcao),
            };
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return SaidaAprendizado.Recusa($"Não consegui usar a pasta {PastaDe(pastaLocal)} ({e.GetType().Name}).");
        }
        catch (ArgumentException e)
        {
            // Uma chave repetida no JSON só aparece quando o objeto é lido.
            return SaidaAprendizado.Recusa($"A entrada tem um campo repetido ({e.Message}).");
        }
    }

    private static SaidaAprendizado GuardarSituacao(JsonObject o, string pastaLocal)
    {
        if (o["nome"] is not { } no)
            return SaidaAprendizado.Recusa($"Falta o \"nome\" da situação: um texto de 1 a {MaximoNome} caracteres.");
        if (Texto(no) is not { } bruto)
            return SaidaAprendizado.Recusa("O \"nome\" da situação é um texto, entre aspas.");
        var nome = bruto.Trim();
        var chave = Chave(nome);
        if (chave.Length == 0)
            return SaidaAprendizado.Recusa("O \"nome\" da situação está vazio.");
        if (nome.Length > MaximoNome)
            return SaidaAprendizado.Recusa($"O \"nome\" da situação passa de {MaximoNome} caracteres.");
        var agora = LerAgora(pastaLocal);
        agora["situacao"] = new JsonObject { ["nome"] = nome, ["chave"] = chave };
        GravarObjeto(CaminhoAgora(pastaLocal), agora);
        return SaidaAprendizado.Feito(new JsonObject { ["situacao"] = chave, ["nome"] = nome });
    }

    private static SaidaAprendizado GuardarVariaveis(JsonObject o, string pastaLocal)
    {
        if (o["valores"] is not JsonObject valores || valores.Count == 0)
            return SaidaAprendizado.Recusa("Os \"valores\" são um objeto com pelo menos uma variável: {\"grave\":4.0} (número ou texto).");
        var novas = new List<(string Chave, JsonNode Valor)>();
        foreach (var (nome, valor) in valores)
        {
            var chave = Chave(nome);
            if (chave.Length == 0)
                return SaidaAprendizado.Recusa("Uma variável está sem nome.");
            if (ValorDaVariavel(valor) is not { } limpo)
                return SaidaAprendizado.Recusa($"O valor de \"{nome}\" não é número nem texto.");
            if (novas.Any(n => n.Chave == chave))
                return SaidaAprendizado.Recusa($"A variável \"{chave}\" veio duas vezes nesta chamada (o nome vale sem maiúsculas nem acento).");
            novas.Add((chave, limpo));
        }
        var agora = LerAgora(pastaLocal);
        var todas = agora["variaveis"] as JsonObject ?? [];
        foreach (var (chave, valor) in novas)
            todas[chave] = valor; // uma chamada nova troca o valor da mesma variável
        agora["variaveis"] = todas.DeepClone();
        GravarObjeto(CaminhoAgora(pastaLocal), agora);
        return SaidaAprendizado.Feito(new JsonObject { ["variaveis"] = todas.DeepClone() });
    }

    /// <summary>O valor de uma variável: um número finito ou um texto não vazio; senão null.</summary>
    private static JsonNode? ValorDaVariavel(JsonNode? valor)
    {
        if (valor is not JsonValue v)
            return null;
        switch (v.GetValueKind())
        {
            case JsonValueKind.Number when v.TryGetValue<double>(out var d) && double.IsFinite(d):
                return JsonValue.Create(d);
            case JsonValueKind.String:
                var t = v.GetValue<string>().Trim();
                return t.Length > 0 ? JsonValue.Create(t) : null;
            default:
                return null;
        }
    }

    private static SaidaAprendizado GuardarOutro(JsonObject o, string pastaLocal)
    {
        var texto = Texto(o["texto"])?.Trim();
        if (string.IsNullOrEmpty(texto))
            return SaidaAprendizado.Recusa("Falta o \"texto\" do \"Outro…\" (o que o usuário escreveu).");
        string? variavel = null;
        if (o["variavel"] is { } noVariavel)
        {
            if (Texto(noVariavel) is not { } bruto || Chave(bruto).Length == 0)
                return SaidaAprendizado.Recusa("A \"variavel\" é o nome de uma variável, como texto.");
            variavel = Chave(bruto);
        }
        var agora = LerAgora(pastaLocal);
        var outros = agora["outros"] as JsonArray ?? [];
        var lista = outros.OfType<JsonObject>()
            .Where(x => Chave(Texto(x["texto"]) ?? "") != Chave(texto))
            .Select(x => (JsonNode)x.DeepClone())
            .ToList();
        var novo = new JsonObject { ["texto"] = texto, ["variavel"] = variavel };
        lista.Add(novo.DeepClone());
        agora["outros"] = new JsonArray(lista.ToArray());
        GravarObjeto(CaminhoAgora(pastaLocal), agora);
        return SaidaAprendizado.Feito(new JsonObject { ["outro"] = novo });
    }

    private static SaidaAprendizado LerPerfil(JsonObject o, string pastaLocal)
    {
        var nome = Texto(o["situacao"]);
        var chave = nome is null ? "" : Chave(nome);
        if (chave.Length == 0)
            return SaidaAprendizado.Recusa("Falta a \"situacao\" do perfil (o nome que o usuário deu).");
        return SaidaAprendizado.Feito(Perfil(pastaLocal, chave));
    }

    /// <summary>O perfil guardado da situação, ou o vazio (sem a pasta, sem o arquivo ou com ele estragado).</summary>
    public static JsonObject Perfil(string pastaLocal, string chave)
    {
        var guardado = LerObjeto(CaminhoPerfil(pastaLocal, chave));
        return new JsonObject
        {
            ["situacao"] = chave,
            ["variaveis"] = (guardado?["variaveis"] as JsonObject)?.DeepClone() ?? new JsonObject(),
            ["outros"] = (guardado?["outros"] as JsonArray)?.DeepClone() ?? new JsonArray(),
        };
    }

    // ------------------------------------------------------------------ a criação de agora

    /// <summary>O <c>agora.json</c>, ou um objeto vazio.</summary>
    public static JsonObject LerAgora(string pastaLocal) => LerObjeto(CaminhoAgora(pastaLocal)) ?? [];

    public static void ApagarAgora(string pastaLocal)
    {
        var caminho = CaminhoAgora(pastaLocal);
        if (File.Exists(caminho))
            File.Delete(caminho);
    }

    // ------------------------------------------------------------------ o registro

    /// <summary>
    /// O registro de uma criação que terminou.
    /// </summary>
    /// <param name="fim"><see cref="FimSalva"/>, <see cref="FimCancelada"/> ou <see cref="FimInterrompida"/>.</param>
    /// <param name="eventoFim">O evento do fim (o <c>criacao-salva</c> traz o nome da memória; o cancelado, o <c>por</c>).</param>
    /// <param name="criacao">O último <c>GET /criacao</c> com a criação aberta (o corpo), ou null.</param>
    /// <param name="comoFicou">Os <c>criacao-como-ficou</c> vistos.</param>
    /// <param name="escutas">Os <c>criacao-escuta</c> vistos.</param>
    /// <param name="estado">O <c>GET /estado</c> no fim (o corpo), ou null: o id, o nível e o master da memória em uso.</param>
    /// <param name="agora">O <c>agora.json</c>.</param>
    /// <param name="hora">A hora do fim.</param>
    public static JsonObject MontarRegistro(string fim, JsonObject? eventoFim, JsonObject? criacao, IReadOnlyList<JsonObject> comoFicou,
        IReadOnlyList<JsonObject> escutas, JsonObject? estado, JsonObject agora, DateTimeOffset hora)
    {
        var emUso = MemoriaEmUso(estado);
        JsonObject? memoria = null;
        if (fim == FimSalva)
        {
            var nome = Texto(eventoFim?["memoria"]);
            var memorias = Memorias(estado);
            var achada = memorias.FirstOrDefault(m => Texto(m["nome"]) == nome && Booleano(m["emUso"]))
                         ?? memorias.FirstOrDefault(m => nome is not null && Texto(m["nome"]) == nome)
                         ?? (nome is null ? emUso : null);
            memoria = new JsonObject { ["id"] = Texto(achada?["id"]), ["nome"] = nome ?? Texto(achada?["nome"]) };
        }

        var dialogo = new JsonArray();
        foreach (var p in (criacao?["perguntas"] as JsonArray ?? []).OfType<JsonObject>())
        {
            dialogo.Add(new JsonObject
            {
                ["numero"] = p["numero"]?.DeepClone(),
                ["texto"] = Texto(p["texto"]),
                ["resposta"] = Texto(p["resposta"]),
                ["doOutro"] = Booleano(p["doOutro"]),
            });
        }

        var ajustes = new JsonArray();
        foreach (var a in (criacao?["ajustes"] as JsonArray ?? []).OfType<JsonObject>())
        {
            ajustes.Add(new JsonObject
            {
                ["rodada"] = a["rodada"]?.DeepClone(),
                ["codigo"] = Texto(a["codigo"]),
                ["indice"] = a["indice"]?.DeepClone(),
                ["antes"] = Texto(a["antes"]),
                ["valor"] = Texto(a["novo"]),
                ["relido"] = Texto(a["relido"]),
            });
        }

        var listaEscutas = new JsonArray();
        foreach (var e in escutas)
        {
            listaEscutas.Add(new JsonObject
            {
                ["rodada"] = e["rodada"]?.DeepClone(),
                ["variavel"] = Texto(e["variavel"]),
                ["escolha"] = e["escolha"]?.DeepClone(),
            });
        }

        // O "Como ficou?": o retorno positivo é o salvar:true; o negativo, o texto do refino.
        var respostas = comoFicou.Count > 0
            ? comoFicou.Select(e => (Rodada: e["rodada"], Salvar: Booleano(e["salvar"]), Texto: Texto(e["texto"]), DoOutro: Booleano(e["doOutro"])))
            : (criacao?["comoFicou"] as JsonArray ?? []).OfType<JsonObject>().Where(f => Texto(f["resposta"]) is not null)
            .Select(f => (Rodada: f["rodada"], Salvar: Booleano(f["salvar"]), Texto: Texto(f["resposta"]), DoOutro: Booleano(f["doOutro"])));
        var listaComoFicou = new JsonArray();
        var refinos = 0;
        foreach (var r in respostas)
        {
            var item = new JsonObject { ["rodada"] = r.Rodada?.DeepClone(), ["salvar"] = r.Salvar };
            if (!r.Salvar)
            {
                refinos++;
                item["texto"] = r.Texto;
                item["doOutro"] = r.DoOutro;
            }
            listaComoFicou.Add(item);
        }

        return new JsonObject
        {
            ["fim"] = fim,
            ["por"] = Texto(eventoFim?["por"]),
            ["hora"] = hora.ToString("O", Inv),
            ["memoria"] = memoria,
            ["situacao"] = (agora["situacao"] as JsonObject)?.DeepClone(),
            ["variaveis"] = (agora["variaveis"] as JsonObject)?.DeepClone() ?? new JsonObject(),
            ["outros"] = (agora["outros"] as JsonArray)?.DeepClone() ?? new JsonArray(),
            ["dialogo"] = dialogo,
            ["somAntes"] = Texto(criacao?["somAntes"]),
            ["ajustes"] = ajustes,
            ["escutas"] = listaEscutas,
            ["comoFicou"] = listaComoFicou,
            ["semPalavras"] = new JsonObject { ["rodadasRefino"] = refinos, ["cancelou"] = fim == FimCancelada },
            ["contexto"] = new JsonObject
            {
                ["hora"] = hora.ToString("O", Inv),
                ["nivel"] = emUso?["nivel"]?.DeepClone(),
                ["master"] = emUso?["master"]?.DeepClone(),
            },
            ["ajustesDepois"] = new JsonArray(),
        };
    }

    /// <summary>Grava o registro em <c>registros/&lt;hora UTC&gt;-&lt;n&gt;.json</c> (o primeiro n livre) e devolve o caminho.</summary>
    public static string GravarRegistro(string pastaLocal, JsonObject registro, DateTimeOffset hora)
    {
        var pasta = PastaDeRegistros(pastaLocal);
        Directory.CreateDirectory(pasta);
        var nome = hora.UtcDateTime.ToString("yyyyMMdd'T'HHmmssfff'Z'", Inv);
        var bytes = Encoding.UTF8.GetBytes(registro.ToJsonString(Indentado));
        for (var n = 1; ; n++)
        {
            var caminho = Path.Combine(pasta, $"{nome}-{n.ToString(Inv)}.json");
            try
            {
                using var arquivo = new FileStream(caminho, FileMode.CreateNew, FileAccess.Write);
                arquivo.Write(bytes);
                return caminho;
            }
            catch (IOException) when (File.Exists(caminho))
            {
                // já existe: o próximo n
            }
        }
    }

    /// <summary>Os registros, do mais antigo ao mais novo (pelo nome: a hora UTC e o n). Um arquivo estragado fica de fora.</summary>
    public static IReadOnlyList<(string Caminho, JsonObject Registro)> LerRegistros(string pastaLocal)
    {
        var pasta = PastaDeRegistros(pastaLocal);
        if (!Directory.Exists(pasta))
            return [];
        return Directory.EnumerateFiles(pasta, "*.json")
            .Select(c => (Caminho: c, Nome: Path.GetFileNameWithoutExtension(c)))
            .OrderBy(x => x.Nome.LastIndexOf('-') is var i and > 0 ? x.Nome[..i] : x.Nome, StringComparer.Ordinal)
            .ThenBy(x => x.Nome.LastIndexOf('-') is var i and > 0 && int.TryParse(x.Nome[(i + 1)..], NumberStyles.None, Inv, out var n) ? n : 0)
            .Select(x => (x.Caminho, Registro: LerObjeto(x.Caminho)))
            .Where(x => x.Registro is not null)
            .Select(x => (x.Caminho, x.Registro!))
            .ToArray();
    }

    // ------------------------------------------------------------------ o ajuste à mão depois

    /// <summary>
    /// Ao abrir uma criação nova: se a memória em uso tem um registro salvo, compara o que ele salvou (o último relido de
    /// cada ajuste) com o som de agora, e cada diferença maior que <see cref="MinimoAjusteDepois"/> entra no registro como
    /// <c>ajustesDepois</c> (o código, o índice, o valor salvo e o de agora). O som de agora é o <c>/estado</c>, menos o que
    /// esta criação já mexeu (aí vale o antes do primeiro ajuste dela). Devolve quantas diferenças o registro tem agora.
    /// </summary>
    /// <param name="criacao">O <c>GET /criacao</c> da criação que abriu (o corpo): o <c>somAntes</c> e os ajustes dela.</param>
    /// <param name="estado">O <c>GET /estado</c> (o corpo): a memória em uso e os parâmetros.</param>
    public static int ConferirAjustesDepois(string pastaLocal, JsonObject? criacao, JsonObject? estado)
    {
        // Só pelo id da memória em uso: o nome pode ser o de uma memória de outro Setup.
        var id = Texto(MemoriaEmUso(estado)?["id"]);
        if (id is null)
            return 0;
        var (caminho, registro) = LerRegistros(pastaLocal)
            .LastOrDefault(r => Texto(r.Registro["fim"]) == FimSalva && r.Registro["memoria"] is JsonObject m && Texto(m["id"]) == id);
        if (registro is null)
            return 0;

        // O que o registro salvou: o último valor relido (ou pedido) de cada endereço.
        var salvos = new Dictionary<string, (string Codigo, JsonNode? Indice, string Valor)>(StringComparer.Ordinal);
        foreach (var a in (registro["ajustes"] as JsonArray ?? []).OfType<JsonObject>())
        {
            if (Texto(a["codigo"]) is { } codigo && (Texto(a["relido"]) ?? Texto(a["valor"])) is { } valor)
                salvos[ChaveEndereco(codigo, a["indice"])] = (codigo, a["indice"], valor);
        }
        if (salvos.Count == 0)
            return 0;

        var agora = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var p in (estado?["parametros"] as JsonArray ?? []).OfType<JsonObject>())
        {
            if (Texto(p["codigo"]) is { } codigo && Texto(p["noFio"]) is { } valor)
                agora[ChaveEndereco(codigo, p["indice"])] = valor;
        }
        // O que esta criação já mexeu: o antes do primeiro ajuste dela.
        var vistos = new HashSet<string>(StringComparer.Ordinal);
        foreach (var a in (criacao?["ajustes"] as JsonArray ?? []).OfType<JsonObject>())
        {
            if (Texto(a["codigo"]) is not { } codigo)
                continue;
            var chave = ChaveEndereco(codigo, a["indice"]);
            if (vistos.Add(chave) && Texto(a["antes"]) is { } antes)
                agora[chave] = antes;
        }

        var depois = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        foreach (var d in (registro["ajustesDepois"] as JsonArray ?? []).OfType<JsonObject>())
        {
            if (Texto(d["codigo"]) is { } codigo)
                depois[ChaveEndereco(codigo, d["indice"])] = (JsonObject)d.DeepClone(); // um repetido: vale o último
        }
        var antesDaConta = new JsonArray(depois.Values.Select(d => (JsonNode)d.DeepClone()).ToArray());
        foreach (var (chave, salvo) in salvos)
        {
            if (!agora.TryGetValue(chave, out var valorAgora))
                continue;
            if (Diferente(salvo.Valor, valorAgora))
            {
                depois[chave] = new JsonObject
                {
                    ["codigo"] = salvo.Codigo,
                    ["indice"] = salvo.Indice?.DeepClone(),
                    ["salvo"] = ValorJson(salvo.Valor),
                    ["agora"] = ValorJson(valorAgora),
                };
            }
            else
            {
                depois.Remove(chave); // voltou ao salvo
            }
        }
        var novo = new JsonArray(depois.Values.Select(d => (JsonNode)d).ToArray());
        if (!JsonNode.DeepEquals(antesDaConta, novo))
        {
            registro["ajustesDepois"] = novo;
            GravarObjeto(caminho, registro);
        }
        return novo.Count;
    }

    private static string ChaveEndereco(string codigo, JsonNode? indice) =>
        $"{codigo}|{(indice is JsonValue v && v.GetValueKind() == JsonValueKind.Number ? v.ToJsonString() : "")}";

    private static decimal? Decimal(string texto) =>
        decimal.TryParse(texto.Trim(), NumberStyles.Float, Inv, out var d) ? d : null;

    /// <summary>Dois valores do fio diferem: números a mais de <see cref="MinimoAjusteDepois"/>; textos, se diferentes.</summary>
    private static bool Diferente(string a, string b) =>
        Decimal(a) is { } x && Decimal(b) is { } y
            ? Math.Abs(x - y) > (decimal)MinimoAjusteDepois
            : !string.Equals(a.Trim(), b.Trim(), StringComparison.Ordinal);

    /// <summary>Um valor do fio no JSON: o número, se for número; senão o texto.</summary>
    private static JsonNode? ValorJson(string valor) =>
        double.TryParse(valor.Trim(), NumberStyles.Float, Inv, out var d) && double.IsFinite(d) ? JsonValue.Create(d) : JsonValue.Create(valor);

    // ------------------------------------------------------------------ o perfil

    /// <summary>
    /// Refaz o perfil da situação pelos registros salvos dela (<see cref="CalcularPerfil"/>). Grava só quando muda; um perfil
    /// vazio sem arquivo continua sem arquivo. True se o arquivo mudou.
    /// </summary>
    public static bool RefazerPerfil(string pastaLocal, string chave)
    {
        var perfil = CalcularPerfil(chave, LerRegistros(pastaLocal).Select(r => r.Registro).ToArray());
        var caminho = CaminhoPerfil(pastaLocal, chave);
        var guardado = LerObjeto(caminho);
        var vazio = perfil["variaveis"]!.AsObject().Count == 0 && perfil["outros"]!.AsArray().Count == 0;
        if (guardado is null && vazio)
            return false;
        if (guardado is not null && JsonNode.DeepEquals(guardado, perfil))
            return false;
        GravarObjeto(caminho, perfil);
        return true;
    }

    /// <summary>
    /// O perfil de uma situação pelos registros (do mais antigo ao mais novo). Só contam os salvos dessa situação. Para cada
    /// variável, os grupos da mesma escolha: números a até <see cref="MesmaEscolhaDb"/> um do outro (o valor é a média, uma
    /// por memória) ou o mesmo texto. Vale o grupo com mais memórias diferentes (no empate, o mais recente), e só com
    /// <see cref="MinimoMemorias"/> ou mais. Por variável, só o registro salvo mais recente de cada memória vota (uma memória
    /// salva de novo com outra escolha deixa a antiga). Um registro sem o id da memória não conta. Os "Outro…" entram só
    /// com a variável, e só quando o mesmo (texto e variável) aparece em <see cref="MinimoMemorias"/> memórias ou mais.
    /// </summary>
    public static JsonObject CalcularPerfil(string chave, IReadOnlyList<JsonObject> registros)
    {
        // A variável → a memória → a escolha mais recente dela (os registros vêm do mais antigo ao mais novo).
        var porVariavel = new SortedDictionary<string, Dictionary<string, Escolha>>(StringComparer.Ordinal);
        var outros = new List<(string Texto, string Variavel, string Memoria, int Ordem)>();
        for (var ordem = 0; ordem < registros.Count; ordem++)
        {
            var r = registros[ordem];
            if (Texto(r["fim"]) != FimSalva || Texto((r["situacao"] as JsonObject)?["chave"]) != chave)
                continue;
            // Sem o id (a leitura do /estado falhou), a mesma memória poderia contar duas vezes: o registro fica de fora.
            if (Texto((r["memoria"] as JsonObject)?["id"]) is not { Length: > 0 } memoria)
                continue;
            foreach (var (variavel, valor) in r["variaveis"] as JsonObject ?? [])
            {
                if (valor is not JsonValue v)
                    continue;
                Escolha? escolha = v.GetValueKind() switch
                {
                    JsonValueKind.Number when v.TryGetValue<double>(out var d) && double.IsFinite(d) => new Escolha(memoria, d, null, ordem),
                    JsonValueKind.String when v.GetValue<string>().Trim() is { Length: > 0 } t => new Escolha(memoria, null, t, ordem),
                    _ => null,
                };
                if (escolha is null)
                    continue;
                if (!porVariavel.TryGetValue(variavel, out var daVariavel))
                    porVariavel[variavel] = daVariavel = new Dictionary<string, Escolha>(StringComparer.Ordinal);
                daVariavel[memoria] = escolha; // o mais recente da memória troca o anterior
            }
            foreach (var o in (r["outros"] as JsonArray ?? []).OfType<JsonObject>())
            {
                if (Texto(o["texto"]) is { Length: > 0 } texto && Texto(o["variavel"]) is { Length: > 0 } variavel)
                    outros.Add((texto, variavel, memoria, ordem));
            }
        }

        var variaveis = new JsonObject();
        foreach (var (variavel, escolhas) in porVariavel)
        {
            if (MelhorGrupo(escolhas.Values.ToArray()) is { } grupo && grupo.Memorias >= MinimoMemorias)
                variaveis[variavel] = grupo.Valor;
        }
        var listaOutros = new JsonArray();
        foreach (var g in outros.GroupBy(o => (Chave(o.Texto), o.Variavel))
                     .Where(g => g.Select(o => o.Memoria).Distinct().Count() >= MinimoMemorias)
                     .Select(g => g.MaxBy(o => o.Ordem))
                     .OrderByDescending(o => o.Ordem))
            listaOutros.Add(new JsonObject { ["texto"] = g.Texto, ["variavel"] = g.Variavel });
        return new JsonObject { ["situacao"] = chave, ["variaveis"] = variaveis, ["outros"] = listaOutros };
    }

    private sealed record Escolha(string Memoria, double? Numero, string? Texto, int Ordem);

    private sealed record Grupo(int Memorias, int Recente, JsonNode Valor);

    /// <summary>O grupo com mais memórias diferentes (no empate, o mais recente), ou null.</summary>
    private static Grupo? MelhorGrupo(IReadOnlyList<Escolha> escolhas)
    {
        var candidatos = new List<Grupo>();
        var numeros = escolhas.Where(e => e.Numero is not null).OrderBy(e => e.Numero).ToArray();
        for (var i = 0; i < numeros.Length; i++)
        {
            var j = i;
            while (j + 1 < numeros.Length && numeros[j + 1].Numero!.Value - numeros[i].Numero!.Value <= MesmaEscolhaDb + 1e-9)
                j++;
            // Uma memória conta uma vez: o valor dela é o mais recente dentro do grupo.
            var porMemoria = numeros[i..(j + 1)].GroupBy(e => e.Memoria).Select(g => g.MaxBy(e => e.Ordem)!).ToArray();
            var media = Math.Round(porMemoria.Average(e => e.Numero!.Value), 2, MidpointRounding.AwayFromZero);
            candidatos.Add(new Grupo(porMemoria.Length, numeros[i..(j + 1)].Max(e => e.Ordem), JsonValue.Create(media)));
        }
        foreach (var g in escolhas.Where(e => e.Texto is not null).GroupBy(e => Chave(e.Texto!)))
        {
            var recente = g.MaxBy(e => e.Ordem)!;
            candidatos.Add(new Grupo(g.Select(e => e.Memoria).Distinct().Count(), recente.Ordem, JsonValue.Create(recente.Texto!)));
        }
        return candidatos.OrderByDescending(c => c.Memorias).ThenByDescending(c => c.Recente).FirstOrDefault();
    }

    // ------------------------------------------------------------------ o /estado

    private static IReadOnlyList<JsonObject> Memorias(JsonObject? estado) =>
        ((estado?["setup"] as JsonObject)?["memorias"] as JsonArray ?? []).OfType<JsonObject>().ToArray();

    /// <summary>A memória em uso no <c>/estado</c> (id, nome, nivel, master), ou null.</summary>
    public static JsonObject? MemoriaEmUso(JsonObject? estado) => Memorias(estado).FirstOrDefault(m => Booleano(m["emUso"]));

    // ------------------------------------------------------------------ os arquivos

    private static readonly JsonSerializerOptions Indentado = new(RespostaFerramenta.Formato) { WriteIndented = true };

    private static JsonObject? LerObjeto(string caminho)
    {
        try
        {
            return File.Exists(caminho) ? JsonNode.Parse(File.ReadAllText(caminho)) as JsonObject : null;
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>Grava num arquivo temporário e depois no lugar, para nunca ficar pela metade.</summary>
    private static void GravarObjeto(string caminho, JsonObject o)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(caminho)!);
        var temporario = $"{caminho}.{Guid.NewGuid():N}.tmp";
        File.WriteAllText(temporario, o.ToJsonString(Indentado), new UTF8Encoding(false));
        File.Move(temporario, caminho, overwrite: true);
    }

    private static string? Texto(JsonNode? no) => no is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;

    private static bool Booleano(JsonNode? no) => no is JsonValue v && v.TryGetValue<bool>(out var b) && b;
}

/// <summary>
/// O que o serviço guarda de uma criação em curso para o registro (história 14e): o último <c>GET /criacao</c> com ela aberta,
/// os <c>criacao-como-ficou</c> e os <c>criacao-escuta</c> vistos, e se o ajuste à mão já foi conferido.
/// </summary>
public sealed class AcompanhamentoCriacao
{
    private readonly object _trava = new();
    private readonly List<JsonObject> _comoFicou = [];
    private readonly List<JsonObject> _escutas = [];
    private JsonObject? _ultima;

    /// <summary>A criação já foi vista aberta.</summary>
    public bool VistaAberta
    {
        get
        {
            lock (_trava)
                return _ultima is not null;
        }
    }

    /// <summary>O ajuste à mão da criação anterior já foi conferido nesta.</summary>
    public bool Conferida { get; set; }

    public JsonObject? Ultima
    {
        get
        {
            lock (_trava)
                return (JsonObject?)_ultima?.DeepClone();
        }
    }

    public IReadOnlyList<JsonObject> ComoFicou
    {
        get
        {
            lock (_trava)
                return _comoFicou.Select(e => (JsonObject)e.DeepClone()).ToArray();
        }
    }

    public IReadOnlyList<JsonObject> Escutas
    {
        get
        {
            lock (_trava)
                return _escutas.Select(e => (JsonObject)e.DeepClone()).ToArray();
        }
    }

    /// <summary>Guarda o corpo do <c>GET /criacao</c>, se a criação está aberta (uma leitura fechada não apaga a última).</summary>
    /// <returns>True se guardou.</returns>
    public bool Guardar(JsonObject? criacao)
    {
        var etapa = criacao?["etapa"] is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;
        if (etapa is null || etapa == "fechada")
            return false;
        lock (_trava)
            _ultima = (JsonObject)criacao!.DeepClone();
        return true;
    }

    /// <summary>Anota um <c>criacao-como-ficou</c> ou um <c>criacao-escuta</c>.</summary>
    public void Anotar(string tipo, JsonObject evento)
    {
        lock (_trava)
        {
            if (tipo == "criacao-como-ficou")
                _comoFicou.Add((JsonObject)evento.DeepClone());
            else if (tipo == "criacao-escuta")
                _escutas.Add((JsonObject)evento.DeepClone());
        }
    }
}
