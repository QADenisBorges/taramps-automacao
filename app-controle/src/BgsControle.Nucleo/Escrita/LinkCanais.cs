using System.Text;
using System.Text.Json;
using BgsControle.Nucleo.Registro;

namespace BgsControle.Nucleo.Escrita;

/// <summary>Os pares de saídas que podem ser vinculados (MAPA §6). O valor é o canal mestre (0 ou 2).</summary>
public enum ParCanais
{
    /// <summary>Saídas 1–2: a saída 1 manda.</summary>
    Saidas12 = 0,

    /// <summary>Saídas 3–4: a saída 3 manda.</summary>
    Saidas34 = 2,
}

public sealed record ResultadoLink(bool Ok, string? Motivo);

/// <summary>
/// O link 1–2 e 3–4. É só do app (MAPA §6): o processador não tem comando de link, e o estado
/// fica nos arquivos privados. Ele sai sozinho das ligações do Setup em uso (o par vizinho que
/// alimenta os dois canais de um bridge); o usuário não mexe nele (decisão D1 do Denis, 29/10/2026).
/// <list type="bullet">
/// <item>Só o <c>GerenteSetups</c> muda o estado (<see cref="DefinirEstadoAsync"/>), sem copiar a
/// saída mestre.</item>
/// <item>Com o link ligado, cada escrita na mestre também vai para a outra, com o valor que o
/// aparelho confirmou. Mexer na outra saída escreve só nela.</item>
/// </list>
/// Os parâmetros do link são os que o app da Taramps copia (APK, <c>BVM:16189</c>): todos os de
/// canal do Marco 1 menos o roteamento, para o par estéreo não virar mono.
/// Suposição LINK-PARAMETROS: o comportamento na tela ainda será conferido na história 10.
/// </summary>
public sealed class LinkCanais
{
    public const int Formato = 1;

    public const string MsgNaoGuardou = "Não consegui guardar o link no celular.";
    public const string MsgDesligadoSemGuardar =
        "Não consegui guardar o link no celular. Ele está desligado agora, mas ao reabrir o app ele pode voltar ligado.";

    /// <summary>"Link 1–2: as saídas estão diferentes".</summary>
    public static string MsgDiferentes(ParCanais par) => $"Link {Rotulo(par)}: as saídas estão diferentes";

    private readonly Escritor _escritor;
    private readonly EstadoAparelho _estado;
    private readonly IRepositorioTexto _repositorio;
    private readonly LogBle _log;

    private readonly object _trava = new();
    private readonly SemaphoreSlim _operacao = new(1, 1);
    private readonly HashSet<ParCanais> _ligados = [];
    private readonly HashSet<ParCanais> _diferentes = [];

    public LinkCanais(Sessao sessao, EstadoAparelho estado, Escritor escritor, IRepositorioTexto repositorio, LogBle log)
    {
        _estado = estado;
        _escritor = escritor;
        _repositorio = repositorio;
        _log = log;
        _escritor.Espelho = Espelho;
        _estado.Recarregou += Conferir;
        Carregada = Task.Run(CarregarAsync);
    }

    /// <summary>Os parâmetros que o link copia e repete: os de canal do Marco 1, menos o roteamento.</summary>
    public static IReadOnlyList<Parametro> Parametros { get; } =
        Catalogo.Marco1.Where(p => p.Escopo == Escopo.Canal && p.Tipo != TipoValor.Roteamento).ToArray();

    private static readonly HashSet<string> CodigosDoLink = [.. Parametros.Select(p => p.Codigo)];

    public static IReadOnlyList<ParCanais> Pares { get; } = [ParCanais.Saidas12, ParCanais.Saidas34];

    /// <summary>O canal mestre do par (0 ou 2) e o outro (1 ou 3).</summary>
    public static (int Mestre, int Outro) Canais(ParCanais par) => ((int)par, (int)par + 1);

    /// <summary>"1–2" ou "3–4".</summary>
    public static string Rotulo(ParCanais par) => $"{(int)par + 1}–{(int)par + 2}";

    /// <summary>Completa quando o estado guardado foi lido. Antes, todo link conta como desligado.</summary>
    public Task Carregada { get; }

    /// <summary>Um link mudou. Pode chegar em qualquer thread.</summary>
    public event Action<ParCanais>? Mudou;

    public bool Ligado(ParCanais par)
    {
        lock (_trava)
            return _ligados.Contains(par);
    }

    /// <summary>
    /// O par está ligado, mas a última leitura completa (também a de depois de carregar uma
    /// memória) achou as duas saídas diferentes nos parâmetros do link. Só avisa: nada é escrito.
    /// </summary>
    public bool Diferentes(ParCanais par)
    {
        lock (_trava)
            return _diferentes.Contains(par);
    }

    /// <summary>O aviso para a tela ("Link 1–2: as saídas estão diferentes"), ou null.</summary>
    public string? AvisoDiferenca(ParCanais par) => Diferentes(par) ? MsgDiferentes(par) : null;

    /// <summary>
    /// Põe o estado do link, o tirado das ligações do Setup em uso (decisão D1 do Denis, 29/10/2026):
    /// é o caminho único, e só o <c>GerenteSetups</c> o usa; o usuário não mexe no link. Não copia a
    /// saída mestre nem manda nada ao aparelho (quem aplica a memória escreve as duas saídas). Devolve
    /// o motivo, se não conseguiu guardar algum par, ou null.
    /// </summary>
    internal async Task<string?> DefinirEstadoAsync(bool link12, bool link34)
    {
        await Carregada;
        await _operacao.WaitAsync();
        try
        {
            string? motivo = null;
            foreach (var (par, ligado) in new[] { (ParCanais.Saidas12, link12), (ParCanais.Saidas34, link34) })
            {
                if (Ligado(par) == ligado)
                    continue;
                var r = await DefinirAsync(par, ligado);
                if (!r.Ok || r.Motivo is not null)
                    motivo ??= r.Motivo;
            }
            return motivo;
        }
        finally
        {
            _operacao.Release();
        }
    }

    /// <summary>O canal que repete a escrita deste, ou null (o escritor pergunta a cada escrita confirmada).</summary>
    internal int? Espelho(Parametro parametro, int? indice)
    {
        if (indice is not { } canal || !CodigosDoLink.Contains(parametro.Codigo))
            return null;
        foreach (var par in Pares)
        {
            var (mestre, outro) = Canais(par);
            if (canal == mestre && Ligado(par))
                return outro;
        }
        return null;
    }

    private async Task<ResultadoLink> DefinirAsync(ParCanais par, bool ligado)
    {
        string json;
        bool diferente;
        lock (_trava)
        {
            if (ligado)
                _ligados.Add(par);
            else
                _ligados.Remove(par);
            // Nada é copiado ao ligar (o link sai das ligações): o aviso vem do espelho de agora.
            diferente = ligado && _estado.Pronto && !_estado.Lendo && ParDiferente(par);
            if (diferente)
                _diferentes.Add(par);
            else
                _diferentes.Remove(par);
            json = ParaJson();
        }
        if (diferente)
            _log.Evento(MsgDiferentes(par));

        if (!await Gravacao.GravarAsync(_repositorio, json))
        {
            if (ligado)
            {
                // Ligar só vale guardado; desligar vale mesmo sem guardar (o lado seguro).
                lock (_trava)
                {
                    _ligados.Remove(par);
                    _diferentes.Remove(par);
                }
                return new ResultadoLink(false, MsgNaoGuardou);
            }
            _log.Evento($"link {Rotulo(par)} desligado sem guardar");
            Notificar(par);
            return new ResultadoLink(true, MsgDesligadoSemGuardar);
        }
        _log.Evento($"link {Rotulo(par)} {(ligado ? "ligado" : "desligado")}");
        Notificar(par);
        return new ResultadoLink(true, null);
    }

    private async Task CarregarAsync()
    {
        var json = await Gravacao.LerAsync(_repositorio);
        if (json is null)
            return;
        var lidos = new List<ParCanais>();
        try
        {
            using var doc = JsonDocument.Parse(json);
            var raiz = doc.RootElement;
            if (raiz.ValueKind == JsonValueKind.Object
                && raiz.TryGetProperty("formato", out var f) && f.TryGetInt32(out var formato) && formato == Formato
                && raiz.TryGetProperty("pares", out var pares) && pares.ValueKind == JsonValueKind.Object)
            {
                foreach (var par in Pares)
                {
                    if (pares.TryGetProperty(Rotulo(par).Replace('–', '-'), out var v) && v.ValueKind == JsonValueKind.True)
                        lidos.Add(par);
                }
            }
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException)
        {
            // Arquivo estragado (JSON ruim, "formato" que não é número): os links ficam desligados.
        }
        lock (_trava)
        {
            foreach (var par in lidos)
                _ligados.Add(par);
        }
        foreach (var par in lidos)
            Notificar(par);
        Conferir(); // a leitura pode ter acabado antes de o guardado ser lido
    }

    /// <summary>
    /// Depois de uma leitura completa (o espelho foi recarregado): confere se cada par ligado
    /// tem as duas saídas iguais nos parâmetros do link. Sem leitura, nenhum aviso.
    /// </summary>
    private void Conferir()
    {
        var pronto = _estado.Pronto && !_estado.Lendo;
        var mudaram = new List<ParCanais>();
        lock (_trava)
        {
            foreach (var par in Pares)
            {
                var diferente = pronto && _ligados.Contains(par) && ParDiferente(par);
                if (diferente ? _diferentes.Add(par) : _diferentes.Remove(par))
                    mudaram.Add(par);
            }
        }
        foreach (var par in mudaram)
        {
            if (Diferentes(par))
                _log.Evento(MsgDiferentes(par));
            Notificar(par);
        }
    }

    private bool ParDiferente(ParCanais par)
    {
        var (mestre, outro) = Canais(par);
        foreach (var p in Parametros)
        {
            var a = _estado.Valor(p.Codigo, mestre);
            var b = _estado.Valor(p.Codigo, outro);
            if (a is null && b is null)
                continue;
            if (a is null || b is null || !CopiaAntes.Iguais(a, b))
                return true;
        }
        return false;
    }

    /// <summary>Chamar com a trava.</summary>
    private string ParaJson()
    {
        using var fluxo = new MemoryStream();
        using (var w = new Utf8JsonWriter(fluxo, new JsonWriterOptions { Indented = true }))
        {
            w.WriteStartObject();
            w.WriteNumber("formato", Formato);
            w.WriteStartObject("pares");
            foreach (var par in Pares)
                w.WriteBoolean(Rotulo(par).Replace('–', '-'), _ligados.Contains(par));
            w.WriteEndObject();
            w.WriteEndObject();
        }
        return Encoding.UTF8.GetString(fluxo.ToArray());
    }

    private void Notificar(ParCanais par)
    {
        try
        {
            Mudou?.Invoke(par);
        }
        catch (Exception e)
        {
            _log.Evento($"falha no aviso do link ({e.GetType().Name})");
        }
    }
}
