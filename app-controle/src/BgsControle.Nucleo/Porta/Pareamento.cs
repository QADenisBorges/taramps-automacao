using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BgsControle.Nucleo.Escrita;
using BgsControle.Nucleo.Registro;

namespace BgsControle.Nucleo.Porta;

/// <summary>Como terminou um pedido de pareamento. Com <see cref="Ok"/>, a chave do cliente (só agora, nunca mais).</summary>
public sealed record ResultadoPareamento(bool Ok, int Status, string? Chave, string? Motivo);

/// <summary>
/// A trava 2 (<c>porta-de-comandos.md</c>): o pareamento único, um cliente por vez.
/// <list type="bullet">
/// <item>Sem cliente, há um código de 6 dígitos (<see cref="RandomNumberGenerator"/>) para a tela
/// mostrar. A cada <see cref="TentativasPorCodigo"/> códigos errados nasce outro.</item>
/// <item>O código certo devolve uma chave de 32 bytes aleatórios em base64url. O celular guarda só
/// o SHA-256 dela, em <c>porta-ia.json</c> (formato 1, com o nome do cliente e a data). O código
/// some.</item>
/// <item>A chave é conferida pelo hash, em tempo constante.</item>
/// <item><see cref="RevogarAsync"/> apaga o cliente (na hora, em memória) e faz nascer um código novo.</item>
/// <item>Ao abrir, o cliente guardado continua valendo; um arquivo estragado = sem cliente.</item>
/// </list>
/// </summary>
public sealed class Pareamento
{
    public const int Formato = 1;
    public const int TentativasPorCodigo = 5;
    public const int TamanhoMaximoNome = 60;

    public const string MsgJaPareado = "Já existe um cliente pareado. Revogue no celular para parear outro.";
    public const string MsgCodigoErrado = "Código errado. Confira o código na tela IA pela rede do celular.";
    public const string MsgCodigoNovo = "Código errado 5 vezes. A tela mostra um código novo.";
    public const string MsgNaoGuardou = "Não consegui guardar o pareamento no celular. Tente de novo.";
    public const string MsgRevogadoNoMeio = "O pareamento foi revogado no celular. Peça o código novo na tela IA pela rede.";

    /// <summary>Quanto um código errado espera antes do 401 (as tentativas vão uma por vez).</summary>
    public static TimeSpan EsperaCodigoErrado { get; } = TimeSpan.FromSeconds(1);

    private readonly IRepositorioTexto _repositorio;
    private readonly TimeProvider _relogio;
    private readonly LogBle? _log;

    private readonly object _trava = new();
    private readonly SemaphoreSlim _gravacao = new(1, 1);
    private readonly SemaphoreSlim _tentativas = new(1, 1);
    private string? _codigo;
    private int _erros;
    private byte[]? _hash;
    private string? _cliente;
    private DateTimeOffset? _desde;
    private bool _pareando;
    private bool _carregado;

    // Cada revogação conta: um pareamento ou uma leitura que começou antes dela não vale.
    private int _revogacoes;

    public Pareamento(IRepositorioTexto repositorio, TimeProvider? relogio = null, LogBle? log = null)
    {
        _repositorio = repositorio;
        _relogio = relogio ?? TimeProvider.System;
        _log = log;
        _codigo = GerarCodigo();
        Carregado = Task.Run(CarregarAsync);
    }

    /// <summary>Completa quando o <c>porta-ia.json</c> foi lido (ausente ou estragado = sem cliente).</summary>
    public Task Carregado { get; }

    /// <summary>O cliente, o código ou as tentativas mudaram. Pode chegar em qualquer thread.</summary>
    public event Action? Mudou;

    /// <summary>O código de 6 dígitos para a tela, ou null com um cliente pareado.</summary>
    public string? Codigo
    {
        get
        {
            lock (_trava)
                return _hash is null ? _codigo : null;
        }
    }

    public bool Pareado
    {
        get
        {
            lock (_trava)
                return _hash is not null;
        }
    }

    /// <summary>O nome que o cliente deu ao parear, ou null.</summary>
    public string? Cliente
    {
        get
        {
            lock (_trava)
                return _cliente;
        }
    }

    /// <summary>Quando o cliente pareou, ou null.</summary>
    public DateTimeOffset? Desde
    {
        get
        {
            lock (_trava)
                return _desde;
        }
    }

    /// <summary>Um código novo (ao abrir a porta sem cliente). Com cliente, nada muda.</summary>
    public void NovoCodigo()
    {
        lock (_trava)
        {
            if (_hash is not null)
                return;
            _codigo = GerarCodigo();
            _erros = 0;
        }
        Avisar();
    }

    /// <summary>
    /// O pedido de pareamento: com cliente, 409; código errado, 401 (o 5º faz nascer outro); código
    /// certo, a chave nova, guardada só como hash.
    /// </summary>
    public async Task<ResultadoPareamento> ParearAsync(string codigo, string nome)
    {
        await Carregado;
        // Uma tentativa por vez, e cada código errado espera 1 s: adivinhar os 6 dígitos leva dias.
        await _tentativas.WaitAsync();
        try
        {
            return await ParearUmaVezAsync(codigo, nome);
        }
        finally
        {
            _tentativas.Release();
        }
    }

    private async Task<ResultadoPareamento> ParearUmaVezAsync(string codigo, string nome)
    {
        byte[] hash = [];
        int geracao = 0;
        string chave = "";
        DateTimeOffset agora = default;
        bool? trocou = null; // não null = código errado (true: nasceu outro)
        lock (_trava)
        {
            if (_hash is not null || _pareando)
                return new ResultadoPareamento(false, 409, null, MsgJaPareado);
            if (_codigo is null || !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(codigo), Encoding.UTF8.GetBytes(_codigo)))
            {
                _erros++;
                trocou = _erros >= TentativasPorCodigo;
                if (trocou == true)
                {
                    _codigo = GerarCodigo();
                    _erros = 0;
                }
            }
            else
            {
                _pareando = true;
                geracao = _revogacoes;
                chave = System.Buffers.Text.Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));
                hash = SHA256.HashData(Encoding.UTF8.GetBytes(chave));
                agora = _relogio.GetLocalNow();
            }
        }
        if (trocou is { } novo)
        {
            await Task.Delay(EsperaCodigoErrado, _relogio);
            if (novo)
            {
                _log?.Evento("porta: código errado 5 vezes; código novo");
                Avisar();
            }
            return new ResultadoPareamento(false, 401, null, novo ? MsgCodigoNovo : MsgCodigoErrado);
        }

        try
        {
            if (!await GravarAsync(ParaJson(nome, hash, agora)))
            {
                _log?.Evento("porta: não consegui guardar o pareamento");
                return new ResultadoPareamento(false, 409, null, MsgNaoGuardou);
            }
            bool revogado;
            lock (_trava)
            {
                revogado = _revogacoes != geracao;
                if (!revogado)
                {
                    _hash = hash;
                    _cliente = nome;
                    _desde = agora;
                    _codigo = null;
                    _erros = 0;
                }
            }
            if (revogado)
            {
                // Revogado durante a gravação (PARAR IA, a chave desligada): o cliente não fica.
                _log?.Evento("porta: pareamento revogado no meio");
                await GravarAsync(ParaJson(null, null, null));
                return new ResultadoPareamento(false, 409, null, MsgRevogadoNoMeio);
            }
        }
        finally
        {
            lock (_trava)
                _pareando = false;
        }
        _log?.Evento($"porta: cliente pareado ({nome})");
        Avisar();
        return new ResultadoPareamento(true, 200, chave, null);
    }

    /// <summary>A chave é a do cliente pareado (pelo hash, em tempo constante).</summary>
    public bool Confere(string? chave)
    {
        if (string.IsNullOrEmpty(chave))
            return false;
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(chave));
        lock (_trava)
            return _hash is not null && CryptographicOperations.FixedTimeEquals(hash, _hash);
    }

    /// <summary>
    /// Apaga o cliente e faz nascer um código novo. Em memória vale na hora (o próximo pedido já dá
    /// 401); a tarefa termina quando o arquivo foi gravado (false = não gravou).
    /// </summary>
    public Task<bool> RevogarAsync()
    {
        bool tinha;
        lock (_trava)
        {
            // Também um pareamento gravando ou um cliente guardado ainda por ler: o arquivo fica sem cliente.
            tinha = _hash is not null || _pareando || !_carregado;
            _revogacoes++;
            _hash = null;
            _cliente = null;
            _desde = null;
            _codigo = GerarCodigo();
            _erros = 0;
        }
        if (tinha)
            _log?.Evento("porta: cliente revogado");
        Avisar();
        return tinha ? GravarSemClienteAsync() : Task.FromResult(true);
    }

    private async Task<bool> GravarSemClienteAsync()
    {
        await Carregado;
        var ok = await GravarAsync(ParaJson(null, null, null));
        if (!ok)
            _log?.Evento("porta: não consegui apagar o cliente guardado");
        return ok;
    }

    private async Task<bool> GravarAsync(string json)
    {
        await _gravacao.WaitAsync();
        try
        {
            return await Gravacao.GravarAsync(_repositorio, json);
        }
        finally
        {
            _gravacao.Release();
        }
    }

    private async Task CarregarAsync()
    {
        var json = await Gravacao.LerAsync(_repositorio);
        var guardado = json is null ? null : DeJson(json);
        lock (_trava)
        {
            _carregado = true;
            // Uma revogação (ou um pareamento) durante a leitura é mais nova que o guardado.
            if (guardado is null || _hash is not null || _pareando || _revogacoes != 0)
                return;
            _hash = guardado.Value.Hash;
            _cliente = guardado.Value.Nome;
            _desde = guardado.Value.Desde;
        }
        Avisar();
    }

    /// <summary>O JSON do <c>porta-ia.json</c> (formato 1). Sem cliente, só o formato.</summary>
    public static string ParaJson(string? nome, byte[]? hash, DateTimeOffset? desde)
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
            if (hash is not null)
            {
                w.WriteStartObject("cliente");
                w.WriteString("nome", nome ?? "");
                w.WriteString("sha256", Convert.ToBase64String(hash));
                if (desde is { } d)
                    w.WriteString("desde", d.ToString("O", CultureInfo.InvariantCulture));
                w.WriteEndObject();
            }
            w.WriteEndObject();
        }
        return Encoding.UTF8.GetString(fluxo.ToArray());
    }

    /// <summary>O cliente guardado, ou null sem cliente, fora do formato 1 ou estragado.</summary>
    public static (string Nome, byte[] Hash, DateTimeOffset? Desde)? DeJson(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var raiz = doc.RootElement;
            if (raiz.ValueKind != JsonValueKind.Object
                || !raiz.TryGetProperty("formato", out var f) || f.ValueKind != JsonValueKind.Number
                || !f.TryGetInt32(out var formato) || formato != Formato
                || !raiz.TryGetProperty("cliente", out var c) || c.ValueKind != JsonValueKind.Object
                || !c.TryGetProperty("sha256", out var h) || h.ValueKind != JsonValueKind.String)
                return null;
            var hash = Convert.FromBase64String(h.GetString()!);
            if (hash.Length != SHA256.HashSizeInBytes)
                return null;
            var nome = c.TryGetProperty("nome", out var n) && n.ValueKind == JsonValueKind.String ? n.GetString()! : "";
            DateTimeOffset? desde = c.TryGetProperty("desde", out var d) && d.ValueKind == JsonValueKind.String
                                    && DateTimeOffset.TryParse(d.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var quando)
                ? quando
                : null;
            return (nome, hash, desde);
        }
        catch (Exception e) when (e is JsonException or FormatException)
        {
            return null;
        }
    }

    private static string GerarCodigo() =>
        RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6", CultureInfo.InvariantCulture);

    private void Avisar()
    {
        try
        {
            Mudou?.Invoke();
        }
        catch (Exception e)
        {
            _log?.Evento($"falha no aviso do pareamento ({e.GetType().Name})");
        }
    }
}
