using System.Globalization;
using System.Text.Json.Nodes;
using Agente.Avaliacao;
using Agente.Porta;
using Agente.Servico;

namespace Agente.Testes;

/// <summary>A criação acabou (salva, cancelada ou interrompida) enquanto o Agente falso esperava.</summary>
internal sealed class CriacaoAcabouException(string tipo) : Exception($"A criação acabou ({tipo}).")
{
    public string Tipo { get; } = tipo;
}

/// <summary>
/// A porta como um Agente falso a vê: só a ferramenta da porta (<see cref="FerramentaPorta"/>), com o pareamento da pasta
/// local, como o modelo. Os eventos são lidos a partir do momento em que o Agente começa.
/// </summary>
internal sealed class PortaDoAgente(string pastaLocal, CancellationToken cancelar) : IDisposable
{
    private static readonly TimeSpan Prazo = TimeSpan.FromSeconds(60);
    private readonly ClientePorta _cliente = new();
    private long _desde;

    public async Task IniciarAsync() => _desde = (await LerAsync("/eventos?desde=0"))["ultimo"]!.GetValue<long>();

    public async Task<JsonObject> LerAsync(string alvo)
    {
        var r = await FerramentaPorta.PedirAsync(pastaLocal, "GET", alvo, null, _cliente, cancelar);
        return r.Ok && r.Corpo is JsonObject o ? o : throw new InvalidOperationException($"GET {alvo}: {r.Motivo}");
    }

    public Task<RespostaFerramenta> PedirAsync(string alvo, JsonObject? corpo = null, string metodo = "POST") =>
        FerramentaPorta.PedirAsync(pastaLocal, metodo, alvo, corpo, _cliente, cancelar);

    /// <summary>Um pedido que precisa dar certo.</summary>
    public async Task<JsonObject> ExigirAsync(string alvo, JsonObject? corpo = null)
    {
        var r = await PedirAsync(alvo, corpo);
        return r.Ok && r.Corpo is JsonObject o ? o : throw new InvalidOperationException($"POST {alvo}: {r.Motivo}");
    }

    /// <summary>O próximo evento de um dos tipos; a criação que acaba antes lança <see cref="CriacaoAcabouException"/>.</summary>
    public async Task<JsonObject> EsperarAsync(params string[] tipos)
    {
        var limite = DateTime.UtcNow + Prazo;
        while (DateTime.UtcNow < limite)
        {
            var corpo = await LerAsync($"/eventos?desde={_desde}");
            foreach (var e in (corpo["eventos"] as JsonArray ?? []).OfType<JsonObject>())
            {
                _desde = Math.Max(_desde, e["n"]!.GetValue<long>());
                var tipo = e["tipo"]?.GetValue<string>();
                if (tipo is not null && tipos.Contains(tipo))
                    return e;
                if (tipo is "criacao-salva" or "criacao-cancelada" or "criacao-interrompida")
                    throw new CriacaoAcabouException(tipo);
            }
            await Task.Delay(40, cancelar);
        }
        throw new TimeoutException($"Nenhum evento {string.Join(" ou ", tipos)} em {Prazo.TotalSeconds} s.");
    }

    public void Dispose() => _cliente.Dispose();
}

/// <summary>A base dos Agentes falsos: a pergunta e a resposta, o ajuste, o resumo e o "Como ficou?" pela porta.</summary>
internal abstract class AgenteFalsoBase(string pastaLocal) : ILancadorAgente
{
    protected PortaDoAgente Porta { get; private set; } = null!;

    private int _perguntas;

    public async Task<int> RodarAsync(string tarefa, JsonObject? evento, CancellationToken cancelar)
    {
        using var porta = new PortaDoAgente(pastaLocal, cancelar);
        Porta = porta;
        await porta.IniciarAsync();
        try
        {
            await CriarAsync();
            return 0;
        }
        catch (CriacaoAcabouException e)
        {
            return e.Tipo == "criacao-salva" ? 0 : 1;
        }
    }

    protected abstract Task CriarAsync();

    /// <summary>Manda a pergunta e devolve o texto da resposta do usuário.</summary>
    protected async Task<string> PerguntarAsync(string texto, string[] respostas, bool ultima)
    {
        var numero = ++_perguntas;
        await Porta.ExigirAsync("/criacao/pergunta", new JsonObject
        {
            ["texto"] = texto,
            ["respostas"] = new JsonArray(respostas.Select(r => (JsonNode)r!).ToArray()),
            ["ultima"] = ultima,
        });
        while (true)
        {
            var e = await Porta.EsperarAsync("criacao-resposta");
            if (e["numero"]?.GetValue<int>() == numero)
                return e["texto"]!.GetValue<string>();
        }
    }

    protected static JsonObject Ajuste(string codigo, int? indice, decimal valor, string frase, string motivo)
    {
        var o = new JsonObject { ["codigo"] = codigo };
        if (indice is { } i)
            o["indice"] = i;
        o["valor"] = valor;
        o["frase"] = frase;
        o["motivo"] = motivo;
        return o;
    }

    protected static JsonObject Rota(int canal, string rota, string motivo) => new()
    {
        ["codigo"] = "010", ["indice"] = canal, ["valor"] = rota, ["frase"] = $"Entradas {rota} no canal {canal + 1}", ["motivo"] = motivo,
    };

    /// <summary>Um ajuste da criação; a recusa (a cerca, por exemplo) não para o Agente.</summary>
    protected Task<RespostaFerramenta> AjustarAsync(JsonObject ajuste) => Porta.PedirAsync("/criacao/ajuste", ajuste);

    /// <summary>Fecha a rodada, manda o "Como ficou?" e devolve true se o usuário pediu para salvar.</summary>
    protected async Task<bool> AvaliarAsync(string[] respostas)
    {
        await Porta.ExigirAsync("/criacao/resumo");
        await Porta.ExigirAsync("/criacao/como-ficou", new JsonObject { ["respostas"] = new JsonArray(respostas.Select(r => (JsonNode)r!).ToArray()) });
        var e = await Porta.EsperarAsync("criacao-como-ficou");
        return e["salvar"]?.GetValue<bool>() == true;
    }

    /// <summary>Depois do "Ótimo, pode salvar": espera o Salvar do usuário (a criação acaba).</summary>
    protected async Task EsperarSalvarAsync() => await Porta.EsperarAsync("criacao-salva");

    protected static decimal Arredondar(decimal v) => Math.Round(v, 1, MidpointRounding.AwayFromZero);
}

/// <summary>
/// O Agente falso genérico: as mesmas perguntas e a mesma receita para todos, sem ler o Setup e sem escuta. Num refino,
/// só fecha a rodada de novo.
/// </summary>
internal sealed class AgenteGenerico(string pastaLocal) : AgenteFalsoBase(pastaLocal)
{
    protected override async Task CriarAsync()
    {
        await Porta.ExigirAsync("/criacao/abrir");
        await PerguntarAsync("Para que é esta memória?", ["Música", "Filme", "Jogo"], false);
        await PerguntarAsync("Em que volume você costuma ouvir?", ["Baixo", "Médio", "Alto"], true);
        for (var canal = 0; canal < 4; canal++)
            await AjustarAsync(Rota(canal, canal % 2 == 0 ? "a" : "b", "Uma entrada para cada caixa."));
        for (var canal = 0; canal < 4; canal++)
            await AjustarAsync(Ajuste("081", canal, 2.0m, "Um pouco mais de ganho", "A receita de sempre."));
        for (var canal = 0; canal < 4; canal++)
            await AjustarAsync(Ajuste("091", canal, -1.0m, "Um toque de EQ", "A receita de sempre."));
        while (!await AvaliarAsync(["Quero mais grave", "Quero menos grave"]))
        {
            // sem escuta: a mesma receita, a rodada fecha de novo
        }
        await EsperarSalvarAsync();
    }
}

/// <summary>Como o Agente falso bom erra de propósito (os testes das violações e da rota).</summary>
internal sealed record DesvioFalso(bool RotaSempreSeparada = false, decimal LimitadorAcimaDoTeto = 0m, bool TomAntesDoGanho = false);

/// <summary>
/// O Agente falso bom: lê o <c>/estado</c> e o <c>/limites</c>, pergunta o que depende do Setup, do ambiente e do objetivo,
/// escolhe a rota pelo objetivo e pela cobertura, aplica na ordem da física (cerca, rota, ganho, corte, tom) dentro da
/// cerca e refina pela escuta "1 ou 2", cortando a dúvida pela metade até ela caber em 2,5 dB (o meio fica a até 1,25 dB
/// do gosto). Só pela porta.
/// </summary>
internal sealed class AgenteBom(string pastaLocal, DesvioFalso? desvio = null) : AgenteFalsoBase(pastaLocal)
{
    private readonly DesvioFalso _desvio = desvio ?? new DesvioFalso();

    /// <summary>A dúvida que basta: o meio do intervalo fica a até 1,25 dB de qualquer ponto dele.</summary>
    private const decimal DuvidaQueBasta = 2.5m;

    private enum Funcao { Sub, Grave, Agudo, Cheia }

    private enum Objetivo { Fiel, Festa, Maximo, Conversa }

    private sealed record Saida(string Nome, int[] Canais, Funcao Funcao, IReadOnlyList<(int Canal, decimal Teto)> Cerca);

    /// <summary>Uma variável com dúvida: os endereços (o mesmo valor em todos), o intervalo e a frase da escuta.</summary>
    private sealed class Variavel(string nome, string codigo, int?[] indices, decimal estimativa, decimal duvida, string pergunta, string frase)
    {
        public string Nome { get; } = nome;
        public string Codigo { get; } = codigo;
        public int?[] Indices { get; } = indices;
        public decimal Baixo { get; set; } = estimativa - duvida;
        public decimal Alto { get; set; } = estimativa + duvida;
        public decimal Valor { get; set; } = estimativa;
        public string Pergunta { get; } = pergunta;
        public string Frase { get; } = frase;
        public decimal Largura => Alto - Baixo;
        public decimal Meio => (Baixo + Alto) / 2;
    }

    protected override async Task CriarAsync()
    {
        var estado = await Porta.LerAsync("/estado");
        var limites = await Porta.LerAsync("/limites");
        var ambiente = estado["setup"]?["ambiente"]?.GetValue<string>() ?? "outro";
        var saidas = (limites["saidas"] as JsonArray ?? []).OfType<JsonObject>()
            .Where(s => s["altoFalante"] is not null)
            .Select(s => new Saida(
                s["saida"]!.GetValue<string>(),
                (s["canais"] as JsonArray)!.Select(c => c!.GetValue<int>()).ToArray(),
                FuncaoDe(s["saida"]!.GetValue<string>() + " " + s["altoFalante"]!.GetValue<string>()),
                (s["cerca"] as JsonArray ?? []).OfType<JsonObject>().Select(c => (c["canal"]!.GetValue<int>(), c["teto"]!.GetValue<decimal>())).ToArray()))
            .OrderBy(s => s.Canais[0])
            .ToArray();
        var temSub = saidas.Any(s => s.Funcao == Funcao.Sub);
        var temGrave = saidas.Any(s => s.Funcao == Funcao.Grave);
        var temAgudo = saidas.Any(s => s.Funcao == Funcao.Agudo);
        var temCheia = saidas.Any(s => s.Funcao == Funcao.Cheia);

        await Porta.ExigirAsync("/criacao/abrir");

        // 1. O objetivo, com respostas montadas para o ambiente do Setup.
        var objetivo = ObjetivoDe(await PerguntarAsync("Qual é o objetivo do som desta memória?", RespostasObjetivo(ambiente), false));

        // O plano das próximas perguntas sai do Setup e do objetivo.
        var plano = new List<(string Texto, string[] Respostas, string Chave)>();
        if (objetivo == Objetivo.Maximo)
            plano.Add(("Para o máximo, a posição das caixas importa?", ["Tanto faz a posição", "Quero imagem de palco", "Só importa quem dirige"], "onde"));
        else if (objetivo is Objetivo.Festa)
            plano.Add(("Quem vai curtir o som?", RespostasOnde(ambiente), "onde"));
        else
            plano.Add(("Onde fica quem ouve?", RespostasOnde(ambiente), "onde"));
        if (temSub && objetivo != Objetivo.Maximo)
            plano.Add(("Quanto grave você quer no sub?", ["Grave discreto", "Grave na medida", "Grave forte"], "grave"));
        if (temGrave && temAgudo)
            plano.Add(("Que corte o fabricante indica para o agudo?", ["1500 Hz", "2500 Hz", "3500 Hz"], "corte"));
        if (objetivo != Objetivo.Maximo && (temAgudo || (temCheia && objetivo is Objetivo.Fiel or Objetivo.Conversa)))
            plano.Add(("Como você gosta do agudo?", ["Agudo suave", "Agudo neutro", "Agudo brilhante"], "agudo"));
        if (objetivo == Objetivo.Conversa)
            plano.Add(("Como a voz deve soar?", ["Voz natural", "Voz mais à frente", "Voz mais macia"], "voz"));
        var respostas = new Dictionary<string, string>();
        for (var i = 0; i < plano.Count; i++)
            respostas[plano[i].Chave] = await PerguntarAsync(plano[i].Texto, plano[i].Respostas, i == plano.Count - 1);

        // A rota: separadas dão imagem a quem está no ponto; somadas dão a música inteira a quem está fora do eixo e o nível.
        var onde = Palavras.Normalizar(respostas["onde"]);
        var pontoUnico = !(onde.Contains("tanto faz") || onde.Contains("indiferente") || onde.Contains("espalhad") || onde.Contains("todos")
                           || onde.Contains("galera") || onde.Contains("varias") || onde.Contains("andando") || onde.Contains("volta"));
        var separadas = _desvio.RotaSempreSeparada || pontoUnico;

        var variaveis = new List<Variavel>();
        int?[] Canais(Funcao f) => saidas.Where(s => s.Funcao == f).SelectMany(s => s.Canais).Select(c => (int?)c).ToArray();
        if (temSub && objetivo != Objetivo.Maximo)
            variaveis.Add(new Variavel("grave", "081", Canais(Funcao.Sub), Grave(respostas["grave"]), 4m, "Qual grave você prefere?", "Ganho do sub"));
        if (temAgudo && objetivo != Objetivo.Maximo)
            variaveis.Add(new Variavel("agudo", "081", Canais(Funcao.Agudo), Agudo(respostas["agudo"]), 3m, "Qual agudo você prefere?", "Ganho do agudo"));
        if (!temAgudo && respostas.TryGetValue("agudo", out var agudo))
            variaveis.Add(new Variavel("brilho", "091", Canais(Funcao.Cheia), Agudo(agudo), 3m, "Qual brilho você prefere?", "EQ do agudo"));
        if (respostas.TryGetValue("voz", out var voz))
            variaveis.Add(new Variavel("voz", "030", [null], Voz(voz), 3m, "Qual voz você prefere?", "EQ da voz"));

        // 2. A receita, na ordem da física: a cerca, a rota, o ganho, o corte; depois o tom.
        var ganhos = variaveis.Where(v => v.Codigo == "081").ToArray();
        var tons = variaveis.Where(v => v.Codigo != "081").ToArray();
        if (_desvio.TomAntesDoGanho)
            await AplicarTomAsync(tons);
        foreach (var s in saidas)
        {
            foreach (var (canal, teto) in s.Cerca)
                await AjustarAsync(Ajuste("071", canal, teto + _desvio.LimitadorAcimaDoTeto, $"Limitador de {s.Nome} no teto",
                    "O teto da cerca: o limite físico da saída, em dB do limitador."));
        }
        var graves = 0;
        var agudos = 0;
        var cheias = 0;
        foreach (var s in saidas)
        {
            var rota = !separadas || s.Funcao == Funcao.Sub ? "a+b"
                : s.Funcao switch
                {
                    Funcao.Grave => graves++ % 2 == 0 ? "a" : "b",
                    Funcao.Agudo => agudos++ % 2 == 0 ? "a" : "b",
                    _ => cheias++ % 2 == 0 ? "a" : "b",
                };
            foreach (var c in s.Canais)
                await AjustarAsync(Rota(c, rota, separadas ? "Entradas separadas: imagem para quem está no ponto." : "Entradas somadas: a música inteira para todos e o nível."));
        }
        foreach (var v in ganhos)
            await AplicarAsync(v, v.Valor);
        if (temSub)
        {
            foreach (var s in saidas)
            {
                foreach (var c in s.Canais)
                {
                    if (s.Funcao == Funcao.Sub)
                    {
                        await AjustarAsync(Ajuste("041", c, 80, "Corte do sub em 80 Hz", "O sub fica só com o grave, que não dá direção."));
                        await AjustarAsync(Ajuste("043", c, 3, "Corte do sub a 24 dB/oitava", "LR24: a soma com as caixas fica plana."));
                    }
                    else
                    {
                        await AjustarAsync(Ajuste("040", c, 80, $"Passa-alta de {s.Nome} em 80 Hz", "O grave vai para o sub; a caixa folga."));
                        await AjustarAsync(Ajuste("042", c, 3, $"Passa-alta de {s.Nome} a 24 dB/oitava", "LR24: a soma com o sub fica plana."));
                    }
                }
            }
        }
        if (temGrave && temAgudo)
        {
            var corte = Corte(respostas["corte"]);
            foreach (var s in saidas)
            {
                foreach (var c in s.Canais)
                {
                    if (s.Funcao == Funcao.Grave)
                    {
                        await AjustarAsync(Ajuste("041", c, corte, $"Corte de {s.Nome} em {corte} Hz", "O corte que o fabricante indica."));
                        await AjustarAsync(Ajuste("043", c, 3, $"Corte de {s.Nome} a 24 dB/oitava", "LR24 nas duas vias."));
                    }
                    else if (s.Funcao == Funcao.Agudo)
                    {
                        await AjustarAsync(Ajuste("040", c, corte, $"Passa-alta de {s.Nome} em {corte} Hz", "O corte que o fabricante indica."));
                        await AjustarAsync(Ajuste("042", c, 3, $"Passa-alta de {s.Nome} a 24 dB/oitava", "LR24 nas duas vias."));
                    }
                }
            }
        }
        if (!_desvio.TomAntesDoGanho)
            await AplicarTomAsync(tons);

        // 3. "Como ficou?" e o refino pela escuta, na ordem da física (o ganho antes do tom).
        string[] comoFicou = temSub ? ["Quero mais grave", "Quero menos grave", "O agudo incomoda"]
            : temAgudo ? ["Quero o agudo mais suave", "Quero mais agudo", "Falta grave"]
            : ["Quero a voz mais à frente", "O agudo incomoda", "Falta grave"];
        while (!await AvaliarAsync(comoFicou))
        {
            foreach (var v in ganhos.Concat(tons))
            {
                if (v.Largura <= DuvidaQueBasta)
                    continue;
                while (v.Largura > DuvidaQueBasta)
                    await EscutarAsync(v);
                await AplicarAsync(v, Arredondar(v.Meio));
            }
        }
        await EsperarSalvarAsync();
    }

    private async Task AplicarTomAsync(IEnumerable<Variavel> tons)
    {
        foreach (var v in tons)
        {
            if (v.Codigo == "091")
            {
                foreach (var i in v.Indices)
                {
                    await AjustarAsync(Ajuste("090", i, 8000, "EQ do agudo em 8 kHz", "O brilho fica acima de 6 kHz."));
                    await AjustarAsync(Ajuste("092", i, 0.7m, "EQ do agudo largo", "Q 0,7: uma prateleira suave."));
                }
            }
            else if (v.Codigo == "030")
            {
                await AjustarAsync(Ajuste("031", null, 2000, "EQ da voz em 2 kHz", "A inteligibilidade da voz fica entre 1 e 4 kHz."));
            }
            await AplicarAsync(v, v.Valor);
        }
    }

    private async Task AplicarAsync(Variavel v, decimal valor)
    {
        v.Valor = valor;
        foreach (var i in v.Indices)
            await AjustarAsync(Ajuste(v.Codigo, i, valor, v.Frase, $"{v.Nome}: {valor.ToString("0.0", CultureInfo.InvariantCulture)} dB, pela resposta e pela escuta."));
    }

    /// <summary>Uma escuta: o meio menos e mais um quarto da dúvida; a escolha corta a dúvida pela metade.</summary>
    private async Task EscutarAsync(Variavel v)
    {
        var meio = v.Meio;
        var quarto = v.Largura / 4;
        var v1 = Arredondar(meio - quarto);
        var v2 = Arredondar(meio + quarto);
        JsonArray Versao(decimal valor, string nome) =>
            new(v.Indices.Select(i => (JsonNode)Ajuste(v.Codigo, i, valor, $"{v.Frase}: {nome}", $"Comparar {v.Nome}.")).ToArray());
        var abriu = await Porta.PedirAsync("/criacao/escuta", new JsonObject
        {
            ["texto"] = v.Pergunta, ["variavel"] = v.Nome, ["versao1"] = Versao(v1, "menos"), ["versao2"] = Versao(v2, "mais"),
        });
        if (!abriu.Ok)
        {
            v.Baixo = v.Alto = meio; // sem escuta, fica a estimativa
            return;
        }
        var e = await Porta.EsperarAsync("criacao-escuta");
        var escolha = e["escolha"] is JsonValue j && j.TryGetValue<int>(out var n) ? n.ToString(CultureInfo.InvariantCulture) : "tanto-faz";
        switch (escolha)
        {
            case "1":
                v.Alto = meio;
                break;
            case "2":
                v.Baixo = meio;
                break;
            default:
                v.Baixo = meio - quarto;
                v.Alto = meio + quarto;
                break;
        }
    }

    private static Funcao FuncaoDe(string nome)
    {
        var p = Palavras.Normalizar(nome);
        if (p.Contains("sub"))
            return Funcao.Sub;
        if (p.Contains("woofer") || p.Contains("grave"))
            return Funcao.Grave;
        if (p.Contains("tweeter") || p.Contains("driver") || p.Contains("agudo") || p.Contains("corneta"))
            return Funcao.Agudo;
        return Funcao.Cheia;
    }

    private static string[] RespostasObjetivo(string ambiente) => ambiente switch
    {
        "carro" => ["Som fiel e equilibrado", "Grave forte para curtir", "O máximo que o kit aguenta"],
        "sala" => ["Ouvir com fidelidade", "Festa com o pessoal", "O máximo que o kit aguenta"],
        "ar-livre" => ["Som limpo para conversa", "Festa com volume", "O máximo que o kit aguenta"],
        _ => ["Ouvir com fidelidade", "Som alto para festa", "O máximo que o kit aguenta"],
    };

    private static string[] RespostasOnde(string ambiente) => ambiente switch
    {
        "carro" => ["Só quem dirige", "Todos no carro", "Galera do lado de fora"],
        "ar-livre" => ["Pessoas espalhadas em volta", "Todos de um lado só", "Uma pessoa perto da caixa"],
        "sala" => ["Uma pessoa no ponto ideal", "Várias pessoas espalhadas", "Gente andando pela sala"],
        _ => ["Uma pessoa no ponto ideal", "Várias pessoas espalhadas", "Gente andando pelo ambiente"],
    };

    private static Objetivo ObjetivoDe(string texto)
    {
        var t = Palavras.Normalizar(texto);
        if (t.Contains("maximo") || t.Contains("aguenta"))
            return Objetivo.Maximo;
        if (t.Contains("conversa") || t.Contains("voz"))
            return Objetivo.Conversa;
        if (t.Contains("festa") || t.Contains("curtir") || t.Contains("forte") || t.Contains("alto"))
            return Objetivo.Festa;
        return Objetivo.Fiel;
    }

    private static decimal Grave(string texto)
    {
        var t = Palavras.Normalizar(texto);
        return t.Contains("discreto") ? 0m : t.Contains("forte") ? 6m : 3m;
    }

    private static decimal Agudo(string texto)
    {
        var t = Palavras.Normalizar(texto);
        return t.Contains("suave") || t.Contains("macio") ? -2m : t.Contains("brilhante") ? 2m : 0m;
    }

    private static decimal Voz(string texto)
    {
        var t = Palavras.Normalizar(texto);
        return t.Contains("frente") ? 3m : t.Contains("macia") ? -2m : 0m;
    }

    private static int Corte(string texto)
    {
        var digitos = new string(texto.SkipWhile(c => !char.IsAsciiDigit(c)).TakeWhile(char.IsAsciiDigit).ToArray());
        return int.TryParse(digitos, CultureInfo.InvariantCulture, out var hz) && hz is >= 20 and <= 20000 ? hz : 2000;
    }
}
