using System.Text;
using System.Text.Json;
using BgsControle.Nucleo.Escrita;
using BgsControle.Nucleo.Herdado;
using BgsControle.Nucleo.Registro;

namespace BgsControle.Nucleo.Player;

/// <summary>Como terminou uma edição de playlist. <see cref="Mensagem"/> é a frase para a tela, ou null.</summary>
public sealed record ResultadoPlaylist(bool Ok, string? Mensagem)
{
    public static readonly ResultadoPlaylist Sucesso = new(true, null);

    public static ResultadoPlaylist Falha(string mensagem) => new(false, mensagem);
}

/// <summary>
/// Uma playlist que espera a sua memória do celular: veio do <c>playlists.json</c> antigo (por
/// posição da memória do aparelho) com o nome que o protótipo dava àquela posição. A primeira memória
/// do celular com o mesmo nome a adota.
/// </summary>
public sealed record PlaylistPendente(string Nome, IReadOnlyList<Faixa> Faixas);

/// <summary>O que fica em <c>playlists.json</c>: as playlists por id de memória do celular e as pendentes por nome.</summary>
public sealed record DadosPlaylists(IReadOnlyDictionary<string, IReadOnlyList<Faixa>> PorMemoria, IReadOnlyList<PlaylistPendente> Pendentes)
{
    public static DadosPlaylists Vazio { get; } = new(new Dictionary<string, IReadOnlyList<Faixa>>(), []);
}

/// <summary>
/// As playlists do Player (CAP-15): uma por memória do celular (pelo id), em <c>playlists.json</c> nos
/// arquivos privados (formato 2). Adicionar (vários de uma vez, no fim, na ordem escolhida), remover
/// e mover não mandam nada ao processador e não precisam de chave. Uma edição só muda a lista se o
/// arquivo foi gravado. O app guarda só a URI de cada música; ele não copia o arquivo.
/// <list type="bullet">
/// <item><b>Migração</b> do formato 1 (3 listas pela posição da memória do aparelho): cada playlist não
/// vazia vira uma <see cref="PlaylistPendente"/> com o nome do protótipo daquela posição (SPL,
/// MUSICA ORIG, FILME); as vazias somem.</item>
/// <item><b>Adoção</b> (<see cref="AdotarAsync"/>): quem conhece as memórias (o <c>GerenteSetups</c>)
/// passa a lista delas na ordem de busca; cada pendente vai para a primeira com o mesmo nome (sem
/// diferença de caixa). Sem memória correspondente, a pendente espera, sem prazo.</item>
/// <item>Só <see cref="ApagarAsync"/> (a memória ou o Setup dela apagados) tira uma playlist.</item>
/// </list>
/// </summary>
public sealed class GerentePlaylists
{
    public const int Formato = 2;

    /// <summary>O formato antigo (3 listas por posição), lido e convertido em pendentes.</summary>
    public const int FormatoAntigo = 1;

    public const string MsgNaoGuardou = "Não consegui guardar a playlist no celular.";
    public const string MsgSemMemoria = "Sem memória do celular, não há playlist.";
    public const string MsgPosicao = "Posição fora da playlist.";
    public const string MsgNaoExiste = "A faixa não está mais na playlist.";

    private readonly IRepositorioTexto _repositorio;
    private readonly LogBle _log;
    private readonly SemaphoreSlim _porta = new(1, 1);
    private readonly object _trava = new();
    private DadosPlaylists _dados = DadosPlaylists.Vazio;

    public GerentePlaylists(IRepositorioTexto repositorio, LogBle log)
    {
        _repositorio = repositorio;
        _log = log;
        Carregada = Task.Run(CarregarAsync);
    }

    /// <summary>Completa quando o <c>playlists.json</c> foi lido (ausente ou estragado = nenhuma playlist).</summary>
    public Task Carregada { get; }

    /// <summary>Uma playlist mudou. Pode chegar em qualquer thread.</summary>
    public event Action? Mudou;

    /// <summary>
    /// Estas URIs saíram de todas as playlists (e das pendentes): o app pode soltar a permissão
    /// persistente delas no Android (história 11). Pode chegar em qualquer thread.
    /// </summary>
    public event Action<IReadOnlyList<string>>? Soltas;

    public DadosPlaylists Dados
    {
        get
        {
            lock (_trava)
                return _dados;
        }
    }

    /// <summary>A playlist da memória do celular, na ordem; vazia sem memória ou sem playlist.</summary>
    public IReadOnlyList<Faixa> Playlist(string? idMemoria)
    {
        if (string.IsNullOrEmpty(idMemoria))
            return [];
        lock (_trava)
            return _dados.PorMemoria.GetValueOrDefault(idMemoria) ?? [];
    }

    /// <summary>As playlists que esperam a memória do celular com o nome delas.</summary>
    public IReadOnlyList<PlaylistPendente> Pendentes => Dados.Pendentes;

    // ------------------------------------------------------------------ editar

    /// <summary>Acrescenta os arquivos no fim da playlist da memória, na ordem em que vieram. Cada um ganha um id novo.</summary>
    public async Task<ResultadoPlaylist> AdicionarAsync(string? idMemoria, IEnumerable<ArquivoAudio> arquivos)
    {
        if (string.IsNullOrEmpty(idMemoria))
            return ResultadoPlaylist.Falha(MsgSemMemoria);
        var novas = arquivos
            .Where(a => !string.IsNullOrWhiteSpace(a.Uri))
            .Select(a => new Faixa($"faixa-{Guid.NewGuid():N}", a.Uri, a.Nome ?? "", a.Pasta ?? "",
                a.Duracao < TimeSpan.Zero ? TimeSpan.Zero : a.Duracao))
            .ToArray();
        if (novas.Length == 0)
            return ResultadoPlaylist.Sucesso;
        return await EditarAsync(idMemoria, lista => [.. lista, .. novas], $"{novas.Length} faixas adicionadas");
    }

    /// <summary>Tira a faixa da playlist (o arquivo continua no celular).</summary>
    public Task<ResultadoPlaylist> RemoverAsync(string? idMemoria, string id)
    {
        if (string.IsNullOrEmpty(idMemoria))
            return Task.FromResult(ResultadoPlaylist.Falha(MsgSemMemoria));
        return EditarAsync(idMemoria, lista =>
        {
            if (lista.All(f => f.Id != id))
                return null;
            return lista.Where(f => f.Id != id).ToArray();
        }, "faixa removida", MsgNaoExiste);
    }

    /// <summary>Move a faixa da posição <paramref name="de"/> para a <paramref name="para"/> (0 = a primeira).</summary>
    public Task<ResultadoPlaylist> MoverAsync(string? idMemoria, int de, int para)
    {
        if (string.IsNullOrEmpty(idMemoria))
            return Task.FromResult(ResultadoPlaylist.Falha(MsgSemMemoria));
        return EditarAsync(idMemoria, lista =>
        {
            if (de < 0 || de >= lista.Count || para < 0 || para >= lista.Count)
                return null;
            var nova = lista.ToList();
            var faixa = nova[de];
            nova.RemoveAt(de);
            nova.Insert(para, faixa);
            return nova;
        }, $"faixa movida de {de + 1} para {para + 1}", MsgPosicao);
    }

    /// <summary>
    /// Aplica a edição à playlist da memória e grava tudo; só se gravou a lista muda. A edição devolve
    /// null quando não vale (a frase é <paramref name="recusa"/>).
    /// </summary>
    private async Task<ResultadoPlaylist> EditarAsync(
        string idMemoria, Func<IReadOnlyList<Faixa>, IReadOnlyList<Faixa>?> editar, string evento, string? recusa = null)
    {
        await Carregada;
        await _porta.WaitAsync();
        try
        {
            var atuais = Dados;
            if (editar(atuais.PorMemoria.GetValueOrDefault(idMemoria) ?? []) is not { } nova)
                return ResultadoPlaylist.Falha(recusa ?? MsgPosicao);

            var porMemoria = new Dictionary<string, IReadOnlyList<Faixa>>(atuais.PorMemoria);
            if (nova.Count == 0)
                porMemoria.Remove(idMemoria);
            else
                porMemoria[idMemoria] = nova;
            if (!await GravarAsync(atuais with { PorMemoria = porMemoria }))
            {
                _log.Evento("playlist: não consegui guardar no celular");
                return ResultadoPlaylist.Falha(MsgNaoGuardou);
            }
            _log.Evento($"playlist de uma memória do celular: {evento}");
        }
        finally
        {
            _porta.Release();
        }
        Notificar();
        return ResultadoPlaylist.Sucesso;
    }

    // ------------------------------------------------------------------ memórias apagadas e adoção

    /// <summary>
    /// Apaga as playlists das memórias (a memória, ou o Setup dela, foi apagada). Só isto tira uma
    /// playlist. Devolve false se não conseguiu gravar (a playlist fica).
    /// </summary>
    public async Task<bool> ApagarAsync(IEnumerable<string> idsMemorias)
    {
        var ids = idsMemorias.ToHashSet();
        await Carregada;
        await _porta.WaitAsync();
        try
        {
            var atuais = Dados;
            if (!ids.Any(atuais.PorMemoria.ContainsKey))
                return true;
            var porMemoria = atuais.PorMemoria.Where(kv => !ids.Contains(kv.Key)).ToDictionary(kv => kv.Key, kv => kv.Value);
            if (!await GravarAsync(atuais with { PorMemoria = porMemoria }))
            {
                _log.Evento("playlist: não consegui apagar a playlist da memória apagada");
                return false;
            }
            _log.Evento("playlist de uma memória apagada: apagada");
        }
        finally
        {
            _porta.Release();
        }
        Notificar();
        return true;
    }

    /// <summary>
    /// Cada pendente vai para a primeira memória da lista (na ordem dada) com o mesmo nome, sem
    /// diferença de caixa; as faixas entram no fim da playlist dela. Sem correspondente, a pendente
    /// espera. Devolve quantas foram adotadas (0 se nada mudou ou não gravou).
    /// </summary>
    public async Task<int> AdotarAsync(IReadOnlyList<(string Id, string Nome)> memoriasEmOrdem)
    {
        await Carregada;
        await _porta.WaitAsync();
        int adotadas;
        try
        {
            var atuais = Dados;
            if (atuais.Pendentes.Count == 0)
                return 0;
            var porMemoria = new Dictionary<string, IReadOnlyList<Faixa>>(atuais.PorMemoria);
            var ficam = new List<PlaylistPendente>();
            adotadas = 0;
            foreach (var p in atuais.Pendentes)
            {
                var dona = memoriasEmOrdem.FirstOrDefault(m => string.Equals(m.Nome, p.Nome, StringComparison.OrdinalIgnoreCase));
                if (dona.Id is null)
                {
                    ficam.Add(p);
                    continue;
                }
                porMemoria[dona.Id] = [.. porMemoria.GetValueOrDefault(dona.Id) ?? [], .. p.Faixas];
                adotadas++;
                _log.Evento($"playlist {p.Nome}: adotada pela memória {dona.Nome}");
            }
            if (adotadas == 0)
                return 0;
            if (!await GravarAsync(new DadosPlaylists(porMemoria, ficam)))
            {
                _log.Evento("playlist: não consegui guardar a adoção no celular; as pendentes esperam");
                return 0;
            }
        }
        finally
        {
            _porta.Release();
        }
        Notificar();
        return adotadas;
    }

    // ------------------------------------------------------------------ guardado

    /// <summary>Grava (com duas tentativas); só se gravou os dados passam a valer. Chamar com a porta.</summary>
    private async Task<bool> GravarAsync(DadosPlaylists novos)
    {
        if (!await Gravacao.GravarAsync(_repositorio, ParaJson(novos)))
            return false;
        DadosPlaylists antes;
        lock (_trava)
        {
            antes = _dados;
            _dados = novos;
        }
        var ficam = Uris(novos);
        var soltas = Uris(antes).Where(u => !ficam.Contains(u)).ToArray();
        if (soltas.Length > 0)
        {
            try
            {
                Soltas?.Invoke(soltas);
            }
            catch (Exception e)
            {
                _log.Evento($"playlist: falha ao soltar as músicas ({e.GetType().Name})");
            }
        }
        return true;
    }

    /// <summary>Todas as URIs das playlists e das pendentes.</summary>
    private static HashSet<string> Uris(DadosPlaylists dados) =>
        dados.PorMemoria.Values.SelectMany(l => l).Concat(dados.Pendentes.SelectMany(p => p.Faixas))
            .Select(f => f.Uri).ToHashSet(StringComparer.Ordinal);

    private async Task CarregarAsync()
    {
        var json = await Gravacao.LerAsync(_repositorio);
        var lidas = DadosPlaylists.Vazio;
        if (json is not null)
        {
            try
            {
                lidas = DeJson(json);
            }
            catch (Exception)
            {
                // Arquivo estragado (qualquer erro ao ler): nenhuma playlist, sem erro.
            }
        }
        lock (_trava)
            _dados = lidas;
        Notificar();
    }

    /// <summary>O JSON do <c>playlists.json</c> (formato 2): as playlists por memória e as pendentes.</summary>
    public static string ParaJson(DadosPlaylists dados)
    {
        using var fluxo = new MemoryStream();
        using (var w = new Utf8JsonWriter(fluxo, new JsonWriterOptions
        {
            Indented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        }))
        {
            w.WriteStartObject();
            w.WriteNumber("formato", Formato);
            w.WriteStartArray("playlists");
            foreach (var (memoria, faixas) in dados.PorMemoria)
            {
                w.WriteStartObject();
                w.WriteString("memoria", memoria);
                EscreverFaixas(w, faixas);
                w.WriteEndObject();
            }
            w.WriteEndArray();
            w.WriteStartArray("pendentes");
            foreach (var p in dados.Pendentes)
            {
                w.WriteStartObject();
                w.WriteString("nome", p.Nome);
                EscreverFaixas(w, p.Faixas);
                w.WriteEndObject();
            }
            w.WriteEndArray();
            w.WriteEndObject();
        }
        return Encoding.UTF8.GetString(fluxo.ToArray());
    }

    private static void EscreverFaixas(Utf8JsonWriter w, IEnumerable<Faixa> faixas)
    {
        w.WriteStartArray("faixas");
        foreach (var f in faixas)
        {
            w.WriteStartObject();
            w.WriteString("id", f.Id);
            w.WriteString("uri", f.Uri);
            w.WriteString("nome", f.Nome);
            w.WriteString("pasta", f.Pasta);
            w.WriteNumber("duracaoMs", (long)f.Duracao.TotalMilliseconds);
            w.WriteEndObject();
        }
        w.WriteEndArray();
    }

    /// <summary>
    /// Lê o <c>playlists.json</c>. Formato 2: as playlists por memória e as pendentes. Formato 1: cada
    /// lista não vazia vira pendente com o nome do protótipo daquela posição. Outro formato, ou outra
    /// forma: nenhuma. Uma faixa sem id, sem URI ou com id repetido é descartada; as outras ficam, na
    /// ordem. Uma playlist sem memória (ou repetida), ou uma pendente sem nome, é descartada.
    /// </summary>
    public static DadosPlaylists DeJson(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var raiz = doc.RootElement;
        if (raiz.ValueKind != JsonValueKind.Object
            || !raiz.TryGetProperty("formato", out var f) || !f.TryGetInt32(out var formato)
            || !raiz.TryGetProperty("playlists", out var listas) || listas.ValueKind != JsonValueKind.Array)
            return DadosPlaylists.Vazio;

        var ids = new HashSet<string>();
        if (formato == FormatoAntigo)
        {
            var pendentes = new List<PlaylistPendente>();
            var n = 0;
            foreach (var lista in listas.EnumerateArray())
            {
                if (n >= Catalogo.Memorias)
                    break;
                var faixas = LerFaixas(lista, ids);
                var nota = ConhecimentoHerdado.NotaMemoria(++n);
                if (faixas.Count > 0 && nota is not null)
                    pendentes.Add(new PlaylistPendente(nota.NomeNoPrototipo, faixas));
            }
            return new DadosPlaylists(new Dictionary<string, IReadOnlyList<Faixa>>(), pendentes);
        }
        if (formato != Formato)
            return DadosPlaylists.Vazio;

        var porMemoria = new Dictionary<string, IReadOnlyList<Faixa>>();
        foreach (var item in listas.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object || Texto(item, "memoria") is not { Length: > 0 } memoria
                || porMemoria.ContainsKey(memoria) || !item.TryGetProperty("faixas", out var faixasJson))
                continue;
            var faixas = LerFaixas(faixasJson, ids);
            if (faixas.Count > 0)
                porMemoria[memoria] = faixas;
        }
        var lidas = new List<PlaylistPendente>();
        if (raiz.TryGetProperty("pendentes", out var pendentesJson) && pendentesJson.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in pendentesJson.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object || Texto(item, "nome") is not { } nome || string.IsNullOrWhiteSpace(nome)
                    || !item.TryGetProperty("faixas", out var faixasJson))
                    continue;
                var faixas = LerFaixas(faixasJson, ids);
                if (faixas.Count > 0)
                    lidas.Add(new PlaylistPendente(nome, faixas));
            }
        }
        return new DadosPlaylists(porMemoria, lidas);
    }

    private static List<Faixa> LerFaixas(JsonElement lista, HashSet<string> ids)
    {
        var faixas = new List<Faixa>();
        if (lista.ValueKind != JsonValueKind.Array)
            return faixas;
        foreach (var item in lista.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
                continue;
            var id = Texto(item, "id");
            var uri = Texto(item, "uri");
            if (string.IsNullOrEmpty(id) || string.IsNullOrWhiteSpace(uri) || !ids.Add(id))
                continue;
            faixas.Add(new Faixa(id, uri, Texto(item, "nome") ?? "", Texto(item, "pasta") ?? "", Duracao(item)));
        }
        return faixas;
    }

    /// <summary>A duração da faixa: só um número inteiro de ms dentro da faixa do <see cref="TimeSpan"/>; senão 0 (a faixa fica).</summary>
    private static TimeSpan Duracao(JsonElement item) =>
        item.TryGetProperty("duracaoMs", out var d) && d.ValueKind == JsonValueKind.Number
        && d.TryGetInt64(out var ms) && ms >= 0 && ms <= TimeSpan.MaxValue.Ticks / TimeSpan.TicksPerMillisecond
            ? TimeSpan.FromTicks(ms * TimeSpan.TicksPerMillisecond)
            : TimeSpan.Zero;

    private static string? Texto(JsonElement o, string nome) =>
        o.TryGetProperty(nome, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;

    private void Notificar()
    {
        try
        {
            Mudou?.Invoke();
        }
        catch (Exception e)
        {
            _log.Evento($"falha no aviso das playlists ({e.GetType().Name})");
        }
    }
}
