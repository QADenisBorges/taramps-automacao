using System.Globalization;
using System.Text;
using System.Text.Json;
using BgsControle.Nucleo.Escrita;
using BgsControle.Nucleo.Registro;

namespace BgsControle.Nucleo.Setups;

/// <summary>
/// Um modelo guardado na base de modelos: o nome do modelo como foi confirmado, o grupo, a categoria e
/// só os dados oficiais e de outra fonte (o medido e o estimado são da instalação, não do modelo).
/// </summary>
public sealed record ModeloGuardado(string Modelo, GrupoEquipamento Grupo, string Categoria, DadosTecnicos Dados, DateTimeOffset Quando);

/// <summary>
/// A base de modelos no celular (CAP-16; forja de 29/10/2026), em <c>modelos.json</c> nos arquivos
/// privados. Uma ficha confirmada fica guardada pelo modelo (o texto aparado, sem diferença de
/// maiúsculas), é reaproveitada em outros Setups e completa o campo enquanto o usuário digita
/// (<see cref="Sugerir"/>, <see cref="Ficha"/>). Sem arquivo (ou com ele estragado), nasce vazia (CAP-6,
/// o app nasce zerado). Não manda nada ao aparelho.
/// </summary>
public sealed class BaseModelos
{
    public const int Formato = 1;

    private readonly IRepositorioTexto _repositorio;
    private readonly LogBle _log;
    private readonly TimeProvider _relogio;
    private readonly object _trava = new();
    private readonly SemaphoreSlim _gravacao = new(1, 1);
    private IReadOnlyList<ModeloGuardado> _modelos = [];

    public BaseModelos(IRepositorioTexto repositorio, LogBle log, TimeProvider? relogio = null)
    {
        _repositorio = repositorio;
        _log = log;
        _relogio = relogio ?? TimeProvider.System;
        Carregada = Task.Run(CarregarAsync);
    }

    /// <summary>Completa quando o <c>modelos.json</c> foi lido.</summary>
    public Task Carregada { get; }

    /// <summary>A base mudou. Pode chegar em qualquer thread.</summary>
    public event Action? Mudou;

    /// <summary>Os modelos guardados, na ordem em que entraram.</summary>
    public IReadOnlyList<ModeloGuardado> Modelos
    {
        get
        {
            lock (_trava)
                return _modelos;
        }
    }

    /// <summary>A chave de um modelo: o texto aparado, em FormC e em maiúsculas; null se vazio.</summary>
    public static string? Chave(string? modelo)
    {
        var texto = modelo?.Normalize(NormalizationForm.FormC).Trim();
        return string.IsNullOrEmpty(texto) ? null : texto.ToUpperInvariant();
    }

    /// <summary>Os modelos que começam com o texto (aparado, sem diferença de maiúsculas), em ordem alfabética. Sem texto, nenhum.</summary>
    public IReadOnlyList<string> Sugerir(string? prefixo)
    {
        if (Chave(prefixo) is not { } chave)
            return [];
        return Modelos.Where(m => Chave(m.Modelo)!.StartsWith(chave, StringComparison.Ordinal))
            .Select(m => m.Modelo)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <summary>A ficha guardada do modelo, com as origens, ou null.</summary>
    public ModeloGuardado? Ficha(string? modelo) =>
        Chave(modelo) is { } chave ? Modelos.FirstOrDefault(m => Chave(m.Modelo) == chave) : null;

    /// <summary>
    /// O que a base guarda de uma ficha: só os dados oficiais e de outra fonte, sem os da alimentação
    /// (a fonte e a bateria são da instalação, não do modelo).
    /// </summary>
    public static DadosTecnicos DoModelo(DadosTecnicos dados) =>
        dados.So(OrigemDado.Oficial, OrigemDado.OutraFonte) with { TensaoAlimentacao = null, CorrenteAlimentacao = null };

    /// <summary>
    /// Guarda a ficha de um equipamento confirmado com modelo: a categoria, o grupo e só os dados
    /// do modelo (<see cref="DoModelo"/>). O modelo fica uma vez só, com a ficha mais nova e o nome
    /// com que entrou na base. Um modelo já guardado confirmado sem nenhum dado do modelo (só
    /// digitados ou medidos) fica como estava. Sem modelo, nada. False = não gravou (a base fica como
    /// estava).
    /// </summary>
    public async Task<bool> GuardarAsync(Equipamento equipamento)
    {
        await Carregada;
        if (Chave(equipamento.Modelo) is not { } chave)
            return true;
        var dados = DoModelo(equipamento.Dados);
        await _gravacao.WaitAsync();
        try
        {
            var atuais = Modelos;
            var existente = atuais.FirstOrDefault(m => Chave(m.Modelo) == chave);
            if (existente is not null && dados.Vazia)
                return true;
            var novo = new ModeloGuardado(existente?.Modelo ?? equipamento.Modelo!.Trim(), equipamento.Grupo, equipamento.Categoria,
                dados, _relogio.GetLocalNow());
            var lista = existente is not null
                ? atuais.Select(m => ReferenceEquals(m, existente) ? novo : m).ToArray()
                : [.. atuais, novo];
            if (!await Gravacao.GravarAsync(_repositorio, ParaJson(lista)))
            {
                _log.Evento("não consegui guardar a base de modelos no celular");
                return false;
            }
            lock (_trava)
                _modelos = lista;
            _log.Evento($"base de modelos: {novo.Modelo} guardado");
        }
        finally
        {
            _gravacao.Release();
        }
        Notificar();
        return true;
    }

    private async Task CarregarAsync()
    {
        if (await Gravacao.LerAsync(_repositorio) is not { } json)
            return;
        IReadOnlyList<ModeloGuardado>? lidos = null;
        try
        {
            lidos = DeJson(json);
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException or FormatException)
        {
            // Arquivo estragado: fica vazia, sem erro.
        }
        if (lidos is null)
        {
            _log.Evento("modelos.json ilegível: a base ficou vazia");
            return;
        }
        lock (_trava)
            _modelos = lidos;
        Notificar();
    }

    /// <summary>O JSON do <c>modelos.json</c>.</summary>
    public static string ParaJson(IEnumerable<ModeloGuardado> modelos)
    {
        using var fluxo = new MemoryStream();
        using (var w = FichaJson.NovoEscritor(fluxo))
        {
            w.WriteStartObject();
            w.WriteNumber("formato", Formato);
            w.WriteStartArray("modelos");
            foreach (var m in modelos)
            {
                w.WriteStartObject();
                w.WriteString("modelo", m.Modelo);
                w.WriteString("grupo", FichaJson.NomeGrupo(m.Grupo));
                w.WriteString("categoria", m.Categoria);
                w.WriteString("quando", m.Quando.ToString("O", CultureInfo.InvariantCulture));
                FichaJson.EscreverDados(w, m.Dados);
                w.WriteEndObject();
            }
            w.WriteEndArray();
            w.WriteEndObject();
        }
        return Encoding.UTF8.GetString(fluxo.ToArray());
    }

    /// <summary>
    /// Lê o <c>modelos.json</c>; null fora do formato. Um modelo sem nome, sem grupo, sem categoria ou
    /// repetido (pela chave) é descartado; um dado de outra origem que não oficial ou outra fonte sai.
    /// </summary>
    public static IReadOnlyList<ModeloGuardado>? DeJson(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var raiz = doc.RootElement;
        if (raiz.ValueKind != JsonValueKind.Object
            || !raiz.TryGetProperty("formato", out var f) || !f.TryGetInt32(out var formato) || formato != Formato
            || !raiz.TryGetProperty("modelos", out var lista) || lista.ValueKind != JsonValueKind.Array)
            return null;
        var saida = new List<ModeloGuardado>();
        foreach (var item in lista.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object
                || Ligacoes.TextoValido(FichaJson.Texto(item, "modelo")) is not { } modelo
                || FichaJson.GrupoDe(FichaJson.Texto(item, "grupo")) is not { } grupo
                || Ligacoes.TextoValido(FichaJson.Texto(item, "categoria")) is not { } categoria
                || saida.Any(m => Chave(m.Modelo) == Chave(modelo)))
                continue;
            var dados = DoModelo(FichaJson.LerDados(item, legado: false));
            saida.Add(new ModeloGuardado(modelo, grupo, categoria, dados, FichaJson.Quando(item)));
        }
        return saida;
    }

    private void Notificar()
    {
        try
        {
            Mudou?.Invoke();
        }
        catch (Exception e)
        {
            _log.Evento($"falha no aviso da base de modelos ({e.GetType().Name})");
        }
    }
}

/// <summary>A ficha no JSON (o <c>setups.json</c> formato 3 e o <c>modelos.json</c>).</summary>
internal static class FichaJson
{
    private static readonly CultureInfo Fio = CultureInfo.InvariantCulture;

    public static Utf8JsonWriter NovoEscritor(Stream fluxo) => new(fluxo, new JsonWriterOptions
    {
        Indented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    });

    public static string NomeOrigem(OrigemDado o) => o switch
    {
        OrigemDado.Oficial => "oficial",
        OrigemDado.OutraFonte => "outra-fonte",
        OrigemDado.Medido => "medido",
        OrigemDado.Estimado => "estimado",
        _ => "digitado",
    };

    public static OrigemDado? OrigemDe(string? nome) => nome switch
    {
        "oficial" => OrigemDado.Oficial,
        "outra-fonte" => OrigemDado.OutraFonte,
        "medido" => OrigemDado.Medido,
        "estimado" => OrigemDado.Estimado,
        "digitado" => OrigemDado.Digitado,
        _ => null,
    };

    public static string NomeGrupo(GrupoEquipamento g) => g switch
    {
        GrupoEquipamento.AmplificaETocam => "ativo",
        GrupoEquipamento.Amplificador => "amplificador",
        GrupoEquipamento.Repassa => "repasse",
        GrupoEquipamento.SoRecebe => "receptor",
        _ => "alto-falante",
    };

    public static GrupoEquipamento? GrupoDe(string? nome) => nome switch
    {
        "ativo" => GrupoEquipamento.AmplificaETocam,
        "amplificador" => GrupoEquipamento.Amplificador,
        "repasse" => GrupoEquipamento.Repassa,
        "receptor" => GrupoEquipamento.SoRecebe,
        "alto-falante" => GrupoEquipamento.AltoFalante,
        _ => null,
    };

    /// <summary>
    /// Os dados (formato 3): cada um como <c>{"valor": 3.6, "origem": "medido", "fonte": "…"}</c>; o tipo
    /// da caixa com o valor em texto; as vias como antes.
    /// </summary>
    public static void EscreverDados(Utf8JsonWriter w, DadosTecnicos d)
    {
        w.WriteStartObject("dados");
        foreach (var c in DadosTecnicos.Campos)
        {
            if (c.Ler(d) is not { } v)
                continue;
            w.WriteStartObject(c.Nome);
            w.WriteNumber("valor", v.Valor);
            w.WriteString("origem", NomeOrigem(v.Origem));
            if (v.Fonte is { } fonte)
                w.WriteString("fonte", fonte);
            w.WriteEndObject();
        }
        if (d.TipoCaixa is { } tipo)
        {
            w.WriteStartObject("tipoCaixa");
            w.WriteString("valor", tipo.Valor);
            w.WriteString("origem", NomeOrigem(tipo.Origem));
            if (tipo.Fonte is { } fonte)
                w.WriteString("fonte", fonte);
            w.WriteEndObject();
        }
        if (d.Vias.Count > 0)
        {
            w.WriteStartArray("vias");
            foreach (var v in d.Vias)
            {
                w.WriteStartObject();
                w.WriteString("descricao", v.Descricao);
                if (v.Diametro is { } diametro)
                    w.WriteNumber("diametro", diametro);
                w.WriteEndObject();
            }
            w.WriteEndArray();
        }
        w.WriteEndObject();
    }

    /// <summary>
    /// Os dados de um item. <paramref name="legado"/> (os formatos 1 e 2 do <c>setups.json</c>): cada
    /// número vira um dado digitado. Formato 3: um dado sem valor maior que zero, sem origem conhecida
    /// ou com a fonte inválida sai; os outros ficam.
    /// </summary>
    public static DadosTecnicos LerDados(JsonElement item, bool legado)
    {
        if (!item.TryGetProperty("dados", out var d) || d.ValueKind != JsonValueKind.Object)
            return DadosTecnicos.Nenhum;
        var dados = DadosTecnicos.Nenhum;
        foreach (var c in DadosTecnicos.Campos)
        {
            if (!d.TryGetProperty(c.Nome, out var p))
                continue;
            DadoTecnico? v = null;
            if (legado)
            {
                if (p.ValueKind == JsonValueKind.Number && p.TryGetDecimal(out var n) && n > 0m)
                    v = new DadoTecnico(n, OrigemDado.Digitado);
            }
            else if (p.ValueKind == JsonValueKind.Object
                     && p.TryGetProperty("valor", out var valor) && valor.ValueKind == JsonValueKind.Number
                     && valor.TryGetDecimal(out var n) && n > 0m
                     && OrigemDe(Texto(p, "origem")) is { } origem)
            {
                var fonte = Texto(p, "fonte");
                if (fonte is null || Ligacoes.TextoLongoValido(fonte) is not null)
                    v = new DadoTecnico(n, origem, fonte is null ? null : Ligacoes.TextoLongoValido(fonte));
            }
            if (v is not null && !(c.Nome == "canais" && decimal.Truncate(v.Valor) != v.Valor))
                dados = c.Com(dados, v);
        }
        if (!legado && d.TryGetProperty("tipoCaixa", out var t) && t.ValueKind == JsonValueKind.Object
            && Ligacoes.TextoValido(Texto(t, "valor")) is { } tipo && OrigemDe(Texto(t, "origem")) is { } origemTipo)
        {
            var fonte = Texto(t, "fonte");
            if (fonte is null || Ligacoes.TextoLongoValido(fonte) is not null)
                dados = dados with { TipoCaixa = new DadoTexto(tipo, origemTipo, fonte is null ? null : Ligacoes.TextoLongoValido(fonte)) };
        }
        var vias = new List<Via>();
        if (d.TryGetProperty("vias", out var vs) && vs.ValueKind == JsonValueKind.Array)
        {
            foreach (var v in vs.EnumerateArray())
            {
                if (v.ValueKind == JsonValueKind.Object && Texto(v, "descricao") is { Length: > 0 } descricao)
                {
                    decimal? diametro = v.TryGetProperty("diametro", out var x) && x.ValueKind == JsonValueKind.Number
                                        && x.TryGetDecimal(out var dn) ? dn : null;
                    vias.Add(new Via(descricao, diametro));
                }
            }
        }
        return dados with { Vias = vias };
    }

    public static DateTimeOffset Quando(JsonElement o) =>
        Texto(o, "quando") is { } q && DateTimeOffset.TryParse(q, Fio, DateTimeStyles.RoundtripKind, out var d) ? d : DateTimeOffset.MinValue;

    public static string? Texto(JsonElement o, string nome) =>
        o.TryGetProperty(nome, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;
}
