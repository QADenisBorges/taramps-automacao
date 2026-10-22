using System.Text;
using System.Text.Json;
using BgsControle.Nucleo.Escrita;
using BgsControle.Nucleo.Registro;

namespace BgsControle.Nucleo.Player;

/// <summary>Como terminou uma edição de playlist. <see cref="Mensagem"/> é a frase para a tela, ou null.</summary>
public sealed record ResultadoPlaylist(bool Ok, string? Mensagem)
{
    public static readonly ResultadoPlaylist Sucesso = new(true, null);

    public static ResultadoPlaylist Falha(string mensagem) => new(false, mensagem);
}

/// <summary>
/// As 3 playlists do Player (CAP-15), uma por posição de memória (0 a 2), em <c>playlists.json</c>
/// nos arquivos privados. Adicionar (vários de uma vez, no fim, na ordem escolhida), remover e mover
/// não mandam nada ao processador e não precisam de chave. Uma edição só muda a lista se o arquivo
/// foi gravado. O app guarda só a URI de cada música; ele não copia o arquivo.
/// </summary>
public sealed class GerentePlaylists
{
    public const int Formato = 1;

    public const string MsgNaoGuardou = "Não consegui guardar a playlist no celular.";
    public const string MsgMemoria = "Memória de 1 a 3.";
    public const string MsgPosicao = "Posição fora da playlist.";
    public const string MsgNaoExiste = "A faixa não está mais na playlist.";

    private readonly IRepositorioTexto _repositorio;
    private readonly LogBle _log;
    private readonly SemaphoreSlim _porta = new(1, 1);
    private readonly object _trava = new();
    private IReadOnlyList<Faixa>[] _listas = Vazias();

    public GerentePlaylists(IRepositorioTexto repositorio, LogBle log)
    {
        _repositorio = repositorio;
        _log = log;
        Carregada = Task.Run(CarregarAsync);
    }

    /// <summary>Completa quando o <c>playlists.json</c> foi lido (ausente ou estragado = 3 vazias).</summary>
    public Task Carregada { get; }

    /// <summary>Uma playlist mudou. Pode chegar em qualquer thread.</summary>
    public event Action? Mudou;

    /// <summary>A playlist da memória n (0 a 2), na ordem; vazia fora da faixa.</summary>
    public IReadOnlyList<Faixa> Playlist(int n)
    {
        if (n is < 0 or >= Catalogo.Memorias)
            return [];
        lock (_trava)
            return _listas[n];
    }

    // ------------------------------------------------------------------ editar

    /// <summary>Acrescenta os arquivos no fim da playlist da memória n, na ordem em que vieram. Cada um ganha um id novo.</summary>
    public async Task<ResultadoPlaylist> AdicionarAsync(int n, IEnumerable<ArquivoAudio> arquivos)
    {
        if (n is < 0 or >= Catalogo.Memorias)
            return ResultadoPlaylist.Falha(MsgMemoria);
        var novas = arquivos
            .Where(a => !string.IsNullOrWhiteSpace(a.Uri))
            .Select(a => new Faixa($"faixa-{Guid.NewGuid():N}", a.Uri, a.Nome ?? "", a.Pasta ?? "",
                a.Duracao < TimeSpan.Zero ? TimeSpan.Zero : a.Duracao))
            .ToArray();
        if (novas.Length == 0)
            return ResultadoPlaylist.Sucesso;
        return await EditarAsync(n, lista => [.. lista, .. novas], $"{novas.Length} faixas adicionadas");
    }

    /// <summary>Tira a faixa da playlist (o arquivo continua no celular).</summary>
    public Task<ResultadoPlaylist> RemoverAsync(int n, string id)
    {
        if (n is < 0 or >= Catalogo.Memorias)
            return Task.FromResult(ResultadoPlaylist.Falha(MsgMemoria));
        return EditarAsync(n, lista =>
        {
            if (lista.All(f => f.Id != id))
                return null;
            return lista.Where(f => f.Id != id).ToArray();
        }, "faixa removida", MsgNaoExiste);
    }

    /// <summary>Move a faixa da posição <paramref name="de"/> para a <paramref name="para"/> (0 = a primeira).</summary>
    public Task<ResultadoPlaylist> MoverAsync(int n, int de, int para)
    {
        if (n is < 0 or >= Catalogo.Memorias)
            return Task.FromResult(ResultadoPlaylist.Falha(MsgMemoria));
        return EditarAsync(n, lista =>
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
    /// Aplica a edição à playlist n e grava as 3; só se gravou a lista muda. A edição devolve null
    /// quando não vale (a frase é <paramref name="recusa"/>).
    /// </summary>
    private async Task<ResultadoPlaylist> EditarAsync(
        int n, Func<IReadOnlyList<Faixa>, IReadOnlyList<Faixa>?> editar, string evento, string? recusa = null)
    {
        await Carregada;
        await _porta.WaitAsync();
        try
        {
            IReadOnlyList<Faixa>[] atuais;
            lock (_trava)
                atuais = _listas;
            if (editar(atuais[n]) is not { } nova)
                return ResultadoPlaylist.Falha(recusa ?? MsgPosicao);

            var novas = (IReadOnlyList<Faixa>[])atuais.Clone();
            novas[n] = nova;
            if (!await Gravacao.GravarAsync(_repositorio, ParaJson(novas)))
            {
                _log.Evento($"playlist da memória {n + 1}: não consegui guardar no celular");
                return ResultadoPlaylist.Falha(MsgNaoGuardou);
            }
            lock (_trava)
                _listas = novas;
            _log.Evento($"playlist da memória {n + 1}: {evento}");
        }
        finally
        {
            _porta.Release();
        }
        Notificar();
        return ResultadoPlaylist.Sucesso;
    }

    // ------------------------------------------------------------------ guardado

    private async Task CarregarAsync()
    {
        var json = await Gravacao.LerAsync(_repositorio);
        var lidas = Vazias();
        if (json is not null)
        {
            try
            {
                lidas = DeJson(json);
            }
            catch (Exception)
            {
                // Arquivo estragado (qualquer erro ao ler): 3 playlists vazias, sem erro.
            }
        }
        lock (_trava)
            _listas = lidas;
        Notificar();
    }

    private static IReadOnlyList<Faixa>[] Vazias() =>
        Enumerable.Range(0, Catalogo.Memorias).Select(_ => (IReadOnlyList<Faixa>)[]).ToArray();

    /// <summary>O JSON do <c>playlists.json</c> (formato 1): uma lista de faixas por posição de memória.</summary>
    public static string ParaJson(IReadOnlyList<IReadOnlyList<Faixa>> listas)
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
            foreach (var lista in listas)
            {
                w.WriteStartArray();
                foreach (var f in lista)
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
            w.WriteEndArray();
            w.WriteEndObject();
        }
        return Encoding.UTF8.GetString(fluxo.ToArray());
    }

    /// <summary>
    /// Lê o <c>playlists.json</c>. Fora do formato 1, ou com outra forma: 3 vazias. Uma faixa sem id,
    /// sem URI ou com id repetido é descartada; as outras ficam, na ordem.
    /// </summary>
    public static IReadOnlyList<Faixa>[] DeJson(string json)
    {
        var saida = Vazias();
        using var doc = JsonDocument.Parse(json);
        var raiz = doc.RootElement;
        if (raiz.ValueKind != JsonValueKind.Object
            || !raiz.TryGetProperty("formato", out var f) || !f.TryGetInt32(out var formato) || formato != Formato
            || !raiz.TryGetProperty("playlists", out var listas) || listas.ValueKind != JsonValueKind.Array)
            return saida;

        var ids = new HashSet<string>();
        var n = 0;
        foreach (var lista in listas.EnumerateArray())
        {
            if (n >= Catalogo.Memorias)
                break;
            var faixas = new List<Faixa>();
            if (lista.ValueKind == JsonValueKind.Array)
            {
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
            }
            saida[n++] = faixas;
        }
        return saida;
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
