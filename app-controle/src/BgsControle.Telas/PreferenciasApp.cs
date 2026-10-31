using System.Text.Json;
using BgsControle.Nucleo.Escrita;

namespace BgsControle.Telas;

/// <summary>
/// As preferências do próprio app (EXPERIENCE, Configurações do app; história 10d), em JSON nos arquivos
/// privados (<see cref="RepositorioTextoArquivos.ArquivoPreferencias"/>), sem o <c>Preferences</c> do MAUI. Hoje
/// só a chave "Animação ao criar o som", que vem ligada: sem arquivo, ou com um arquivo ilegível, vale ligada.
/// </summary>
public sealed class PreferenciasApp
{
    private const string CampoAnimacao = "animacaoCriacao";

    private readonly IRepositorioTexto _repositorio;
    private readonly object _trava = new();
    private bool _animacaoCriacao = true;

    public PreferenciasApp(IRepositorioTexto repositorio)
    {
        _repositorio = repositorio;
        Carregada = Task.Run(CarregarAsync);
    }

    /// <summary>A leitura do arquivo terminou (com ou sem arquivo).</summary>
    public Task Carregada { get; }

    /// <summary>Uma preferência mudou. Pode chegar em qualquer thread.</summary>
    public event Action? Mudou;

    /// <summary>"Animação ao criar o som": cada ajuste aparece na tela, um por vez. Ligada por padrão.</summary>
    public bool AnimacaoCriacao
    {
        get
        {
            lock (_trava)
                return _animacaoCriacao;
        }
    }

    /// <summary>Liga ou desliga a animação e grava. Devolve false se não conseguiu gravar (o valor não muda).</summary>
    public async Task<bool> DefinirAnimacaoCriacaoAsync(bool ligada)
    {
        await Carregada;
        lock (_trava)
        {
            if (_animacaoCriacao == ligada)
                return true;
        }
        try
        {
            await _repositorio.GravarAsync(ParaJson(ligada));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return false;
        }
        lock (_trava)
            _animacaoCriacao = ligada;
        Mudou?.Invoke();
        return true;
    }

    /// <summary>O JSON do arquivo: <c>{"animacaoCriacao":true}</c>.</summary>
    public static string ParaJson(bool animacaoCriacao) =>
        JsonSerializer.Serialize(new Dictionary<string, bool> { [CampoAnimacao] = animacaoCriacao });

    private async Task CarregarAsync()
    {
        string? json;
        try
        {
            json = await _repositorio.LerAsync();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return;
        }
        if (string.IsNullOrWhiteSpace(json))
            return;
        bool? lida = null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind == JsonValueKind.Object
                && doc.RootElement.TryGetProperty(CampoAnimacao, out var v)
                && v.ValueKind is JsonValueKind.True or JsonValueKind.False)
                lida = v.GetBoolean();
        }
        catch (JsonException)
        {
            // Ilegível: fica o padrão (ligada).
        }
        if (lida is not { } valor)
            return;
        lock (_trava)
            _animacaoCriacao = valor;
        Mudou?.Invoke();
    }
}
