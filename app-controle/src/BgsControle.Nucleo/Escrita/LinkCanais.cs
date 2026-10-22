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
/// fica nos arquivos privados.
/// <list type="bullet">
/// <item>Ao vincular, os parâmetros de áudio por canal da saída mestre (1 ou 3) são escritos na
/// outra (só os que diferem), pelo <see cref="Escritor"/>, e só então o link liga.</item>
/// <item>Com o link ligado, cada escrita na mestre também vai para a outra, com o valor que o
/// aparelho confirmou. Mexer na outra saída escreve só nela.</item>
/// </list>
/// Os parâmetros do link são os que o app da Taramps copia (APK, <c>BVM:16189</c>): todos os de
/// canal do Marco 1 menos o roteamento, para o par estéreo não virar mono.
/// [SUPOSIÇÃO] O comportamento na tela ainda será conferido na história 10.
/// </summary>
public sealed class LinkCanais
{
    public const int Formato = 1;

    public const string MsgNaoGuardou = "Não consegui guardar o link no celular.";
    public const string MsgDesligadoSemGuardar =
        "Não consegui guardar o link no celular. Ele está desligado agora, mas ao reabrir o app ele pode voltar ligado.";
    public const string MsgOcupado = "O link está copiando os ajustes.";

    /// <summary>"Link 1–2: as saídas estão diferentes".</summary>
    public static string MsgDiferentes(ParCanais par) => $"Link {Rotulo(par)}: as saídas estão diferentes";

    private readonly Escritor _escritor;
    private readonly EstadoAparelho _estado;
    private readonly Sessao _sessao;
    private readonly IRepositorioTexto _repositorio;
    private readonly LogBle _log;

    private readonly object _trava = new();
    private readonly SemaphoreSlim _operacao = new(1, 1);
    private readonly HashSet<ParCanais> _ligados = [];
    private readonly HashSet<ParCanais> _diferentes = [];

    public LinkCanais(Sessao sessao, EstadoAparelho estado, Escritor escritor, IRepositorioTexto repositorio, LogBle log)
    {
        _sessao = sessao;
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
    /// Vincula o par: escreve na outra saída cada parâmetro do link que difere da mestre e só
    /// então liga e guarda. Se algum ajuste não passar, o link continua desligado.
    /// </summary>
    public async Task<ResultadoLink> VincularAsync(ParCanais par)
    {
        await Carregada;
        if (!await _operacao.WaitAsync(0))
            return new ResultadoLink(false, MsgOcupado);
        try
        {
            if (Ligado(par))
                return new ResultadoLink(true, null);
            if (_sessao.Estado != EstadoSessao.Conectado || !_sessao.SegurandoConexao)
                return new ResultadoLink(false, Sessao.MotivoSemConexao);
            if (_estado.Lendo || !_estado.Pronto)
                return new ResultadoLink(false, _estado.Lendo ? Escritor.MsgLendo : Escritor.MsgSemLeitura);
            // Uma operação de memória anda ou ficou pela metade: a cópia não entra no meio.
            if (_escritor.MotivoBloqueio() is { } bloqueio)
                return new ResultadoLink(false, bloqueio);

            var (mestre, outro) = Canais(par);
            var copias = new List<(Parametro Parametro, Valor Valor, string Quadro)>();
            foreach (var p in Parametros)
            {
                // Sem o valor da mestre, o par não pode ficar igual: não liga.
                if (_estado.Valor(p.Codigo, mestre) is not { } valor)
                    return new ResultadoLink(false, Escritor.MsgSemLeitura);
                if (_estado.Valor(p.Codigo, outro) is { } atual && CopiaAntes.Iguais(atual, valor))
                    continue;
                string quadro;
                try
                {
                    quadro = valor.Numero is { } n ? p.CodificarEscrita(outro, n) : p.CodificarEscrita(outro, valor.Texto ?? "");
                }
                catch (Exception e) when (e is ArgumentException or InvalidOperationException)
                {
                    return new ResultadoLink(false, $"O {p.Nome} da saída {mestre + 1} não pode ser copiado ({valor.Exibicao}).");
                }
                copias.Add((p, valor, quadro));
            }

            if (copias.Count > 0 && _sessao.Conferir(copias[0].Quadro) is { } motivo)
            {
                _log.Recusado(copias[0].Quadro, motivo);
                return new ResultadoLink(false, motivo);
            }

            _log.Evento($"link {Rotulo(par)}: copiando {copias.Count} ajustes da saída {mestre + 1} para a {outro + 1}");
            var falhas = 0;
            string? ultimo = null;
            foreach (var (p, valor, _) in copias)
            {
                var r = await _escritor.EscreverAsync(p, outro, valor, espelhar: false);
                if (!r.Ok || r.Diferente)
                {
                    falhas++;
                    ultimo = r.Motivo ?? $"o aparelho ficou em {r.Valor?.NoFio}";
                }
            }
            if (falhas > 0)
            {
                _log.Evento($"link {Rotulo(par)}: {falhas} ajustes não foram copiados ({ultimo}); o link continua desligado");
                return new ResultadoLink(false, falhas == 1
                    ? $"1 ajuste não foi copiado: {ultimo}. O link continua desligado."
                    : $"{falhas} ajustes não foram copiados: {ultimo}. O link continua desligado.");
            }

            return await DefinirAsync(par, true);
        }
        finally
        {
            _operacao.Release();
        }
    }

    /// <summary>Desliga o link. Não mexe no aparelho.</summary>
    public async Task<ResultadoLink> DesvincularAsync(ParCanais par)
    {
        await Carregada;
        await _operacao.WaitAsync();
        try
        {
            if (!Ligado(par))
                return new ResultadoLink(true, null);
            return await DefinirAsync(par, false);
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
        lock (_trava)
        {
            if (ligado)
                _ligados.Add(par);
            else
                _ligados.Remove(par);
            _diferentes.Remove(par); // ligado agora = acabou de copiar; desligado = sem aviso
            json = ParaJson();
        }

        if (!await Gravacao.GravarAsync(_repositorio, json))
        {
            if (ligado)
            {
                // Ligar só vale guardado; desligar vale mesmo sem guardar (o lado seguro).
                lock (_trava)
                    _ligados.Remove(par);
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
