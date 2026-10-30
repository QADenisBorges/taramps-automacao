using System.Text;
using System.Text.Json;

namespace BgsControle.Nucleo.Seguranca;

/// <summary>As 10 chaves de acesso (CAP-11), na ordem de <c>chaves-de-acesso.md</c>.</summary>
public enum Chave
{
    ResetFabrica,
    ControlePelaRede,
    ResetAudio,
    TrocaSenha,
    Gerador,
    AplicarMemoriaCelular,
    RemoteOut,
    BluetoothAudio,
    NomeProduto,
    AjustesAudio,
}

/// <summary>
/// Quem pede um comando: a tela (o Denis), a IA (pela porta da 9b) ou o próprio app (o
/// desligar automático do gerador). A origem de cada comando que chega à porta é a ambiente
/// (<see cref="Uso.Origem"/>); só a tela liga ou desliga uma chave.
/// </summary>
public enum OrigemComando
{
    Tela,
    Ia,
    App,
}

/// <summary>Quando um pedido da IA exige confirmação na tela.</summary>
public enum ConfirmacaoIa
{
    Sempre,
    Nunca,

    /// <summary>A chave da própria porta ("Controle pela rede"): não há pedido da porta para confirmar.</summary>
    NaoSeAplica,
}

/// <summary>Uma linha da tabela de <c>chaves-de-acesso.md</c>.</summary>
/// <param name="Id">Nome estável no JSON guardado. O da "Controle pela rede" continua
/// <c>ia-pela-rede</c>: trocá-lo desligaria em silêncio uma chave ligada no celular.</param>
/// <param name="Risco">O aviso mostrado ao ligar, sem mudar o texto; null = liga sem aviso.</param>
public sealed record DefinicaoChave(
    Chave Chave,
    string Id,
    string Nome,
    string Codigo,
    bool Padrao,
    ConfirmacaoIa ConfirmacaoIa,
    string? Risco);

public sealed record ResultadoChave(bool Ok, string? Motivo);

/// <summary>As chaves de acesso: o estado de cada uma, guardado no celular. A trava pergunta aqui.</summary>
public interface IChaves
{
    IReadOnlyList<DefinicaoChave> Definicoes { get; }

    /// <summary>Completa quando o estado guardado foi lido. Antes disso, toda chave conta como desligada.</summary>
    Task Carregada { get; }

    /// <summary>Thread-safe e sem espera: a trava chama com o lock da sessão.</summary>
    bool Ligada(Chave chave);

    /// <summary>Liga ou desliga e guarda. Uma origem que não é a tela (no parâmetro ou a ambiente) é sempre recusada.</summary>
    Task<ResultadoChave> DefinirAsync(Chave chave, bool ligada, OrigemComando origem);

    /// <summary>Uma chave mudou. Pode chegar em qualquer thread.</summary>
    event Action<Chave>? Mudou;
}

/// <summary>Onde o JSON das chaves fica (no app, os arquivos privados).</summary>
public interface IRepositorioChaves
{
    /// <summary>O JSON guardado, ou null se não há.</summary>
    Task<string?> LerAsync();

    Task GravarAsync(string json);
}

/// <summary>Os padrões confirmados pelo Denis em 19/10/2026 (<c>chaves-de-acesso.md</c>).</summary>
public static class ChavesPadrao
{
    public static IReadOnlyList<DefinicaoChave> Todas { get; } =
    [
        new(Chave.ResetFabrica, "reset-fabrica", "Reset de fábrica", "*004*", false, ConfirmacaoIa.Sempre,
            "Apaga a senha e as 3 memórias."),
        new(Chave.ControlePelaRede, "ia-pela-rede", "Controle pela rede", "—", false, ConfirmacaoIa.NaoSeAplica,
            "Deixa um computador da casa comandar o som pela rede."),
        new(Chave.ResetAudio, "reset-audio", "Reset de áudio", "*242*", false, ConfirmacaoIa.Sempre,
            "O estado resultante é imprevisível."),
        new(Chave.TrocaSenha, "troca-senha", "Troca de senha", "*001*antiga*nova*", false, ConfirmacaoIa.Sempre,
            "Uma senha perdida tranca o aparelho."),
        new(Chave.Gerador, "gerador", "Gerador: tom fixo e sweep", "200–202, 210–213", false, ConfirmacaoIa.Sempre,
            "Um tom forte pode queimar o tweeter ou o sub."),
        new(Chave.AplicarMemoriaCelular, "aplicar-memoria-celular", "Aplicar memória do celular", "vários (MAPA §4)", true, ConfirmacaoIa.Sempre,
            "Troca todo o som de uma vez."),
        new(Chave.RemoteOut, "remote-out", "Remote out (módulo)", "*252*0/1*", true, ConfirmacaoIa.Sempre,
            "Liga ou corta o som de repente."),
        new(Chave.BluetoothAudio, "bluetooth-audio", "Bluetooth de áudio", "251", true, ConfirmacaoIa.Nunca, null),
        new(Chave.NomeProduto, "nome-produto", "Nome do produto", "*002*nome*", true, ConfirmacaoIa.Nunca, null),
        new(Chave.AjustesAudio, "ajustes-audio", "Ajustes de áudio (MAPA §4)", "vários", true, ConfirmacaoIa.Nunca, null),
    ];

    public static DefinicaoChave De(Chave chave) => Todas.First(d => d.Chave == chave);
}

/// <summary>
/// As chaves de acesso com o estado em memória e guardado em JSON pelo repositório. Começa a
/// ler o guardado ao ser criada; até acabar, toda chave conta como desligada (nada passa).
/// </summary>
public sealed class Chaves : IChaves
{
    public const int Formato = 1;
    public const string MsgIaNaoMuda = "A IA nunca liga nem desliga uma chave.";
    public const string MsgNaoGuardou = "Não consegui guardar a chave no celular.";

    public const string MsgDesligadaSemGuardar =
        "Não consegui guardar a chave no celular. Ela está desligada agora, mas ao reabrir o app ela pode voltar ligada.";

    /// <summary>Tentativas de gravar antes de desistir.</summary>
    public const int TentativasDeGravar = 2;

    private readonly IRepositorioChaves _repositorio;
    private readonly object _trava = new();
    private readonly SemaphoreSlim _gravacao = new(1, 1);
    private readonly Dictionary<Chave, bool> _estado = [];
    private bool _carregada;

    public Chaves(IRepositorioChaves repositorio)
    {
        _repositorio = repositorio;
        foreach (var d in ChavesPadrao.Todas)
            _estado[d.Chave] = d.Padrao;
        // Fora da thread de quem cria: no app, quem cria pode ser a thread da tela.
        Carregada = Task.Run(CarregarAsync);
    }

    public IReadOnlyList<DefinicaoChave> Definicoes => ChavesPadrao.Todas;

    public Task Carregada { get; }

    public event Action<Chave>? Mudou;

    public bool Ligada(Chave chave)
    {
        lock (_trava)
            return _carregada && _estado[chave];
    }

    public async Task<ResultadoChave> DefinirAsync(Chave chave, bool ligada, OrigemComando origem)
    {
        // A origem ambiente também conta: num escopo da IA, nem a tela "emprestada" muda uma chave.
        if (origem != OrigemComando.Tela || Uso.Origem.Atual != OrigemComando.Tela)
            return new ResultadoChave(false, MsgIaNaoMuda);
        await Carregada;

        await _gravacao.WaitAsync();
        try
        {
            bool antes;
            string json;
            lock (_trava)
            {
                antes = _estado[chave];
                if (antes == ligada)
                    return new ResultadoChave(true, null);
                _estado[chave] = ligada;
                json = ParaJson();
            }

            if (!await GravarAsync(json))
            {
                if (ligada)
                {
                    // Ligar só vale guardado; desligar vale mesmo sem guardar (o lado seguro).
                    lock (_trava)
                        _estado[chave] = antes;
                    return new ResultadoChave(false, MsgNaoGuardou);
                }
                Mudou?.Invoke(chave);
                return new ResultadoChave(true, MsgDesligadaSemGuardar);
            }
            Mudou?.Invoke(chave);
            return new ResultadoChave(true, null);
        }
        finally
        {
            _gravacao.Release();
        }
    }

    /// <summary>Grava, com uma nova tentativa antes de desistir.</summary>
    private async Task<bool> GravarAsync(string json)
    {
        for (var tentativa = 1; tentativa <= TentativasDeGravar; tentativa++)
        {
            try
            {
                await _repositorio.GravarAsync(json);
                return true;
            }
            catch (Exception)
            {
                // Tenta de novo; depois da última, desiste.
            }
        }
        return false;
    }

    private async Task CarregarAsync()
    {
        string? json = null;
        try
        {
            json = await _repositorio.LerAsync();
        }
        catch (Exception)
        {
            // Sem leitura: ficam os padrões.
        }
        lock (_trava)
        {
            try
            {
                if (json is not null)
                    Aplicar(json);
            }
            catch (Exception)
            {
                // Qualquer surpresa no guardado: ficam os padrões, e as chaves passam a valer.
            }
            _carregada = true;
        }
        foreach (var d in ChavesPadrao.Todas)
            Mudou?.Invoke(d.Chave);
    }

    /// <summary>
    /// Lê o JSON guardado. Ilegível, sem o <c>formato</c> 1, ou uma chave que falta: fica o
    /// padrão. Um id que o app não conhece mais (o <c>salvar-memoria</c>, que saiu na história 8c, e o
    /// <c>carregar-memoria</c>, que saiu com as memórias do processador em 29/10/2026) é ignorado.
    /// Chamar com a trava.
    /// </summary>
    private void Aplicar(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object
                || !doc.RootElement.TryGetProperty("formato", out var formato)
                || formato.ValueKind != JsonValueKind.Number
                || !formato.TryGetInt32(out var f) || f != Formato
                || !doc.RootElement.TryGetProperty("chaves", out var chaves)
                || chaves.ValueKind != JsonValueKind.Object)
                return;
            foreach (var d in ChavesPadrao.Todas)
            {
                if (chaves.TryGetProperty(d.Id, out var v) && v.ValueKind is JsonValueKind.True or JsonValueKind.False)
                    _estado[d.Chave] = v.GetBoolean();
            }
        }
        catch (JsonException)
        {
            // Arquivo estragado: ficam os padrões.
        }
    }

    /// <summary>Chamar com a trava.</summary>
    private string ParaJson()
    {
        using var fluxo = new MemoryStream();
        using (var w = new Utf8JsonWriter(fluxo, new JsonWriterOptions { Indented = true }))
        {
            w.WriteStartObject();
            w.WriteNumber("formato", Formato);
            w.WriteStartObject("chaves");
            foreach (var d in ChavesPadrao.Todas)
                w.WriteBoolean(d.Id, _estado[d.Chave]);
            w.WriteEndObject();
            w.WriteEndObject();
        }
        return Encoding.UTF8.GetString(fluxo.ToArray());
    }
}
