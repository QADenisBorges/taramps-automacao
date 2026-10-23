using System.Globalization;
using BgsControle.Nucleo;
using BgsControle.Nucleo.Seguranca;
using BgsControle.Nucleo.Uso;

namespace BgsControle.Telas;

/// <summary>Uma linha do histórico na tela: a hora, a origem, a frase e, se recusado, o motivo.</summary>
/// <param name="Hora">"22/10 21:04:37" (o histórico guardado passa de um dia).</param>
/// <param name="Origem">"Tela", "IA" ou "App".</param>
/// <param name="Frase">"Ganho · saída 3 · +2,0 dB".</param>
/// <param name="Motivo">O motivo da recusa, ou null se foi enviado.</param>
public sealed record LinhaHistorico(string Hora, string Origem, string Frase, bool Enviado, string? Motivo)
{
    public const string TextoTela = "Tela";
    public const string TextoIa = "IA";
    public const string TextoApp = "App";

    /// <summary>A linha de uma entrada do histórico.</summary>
    public static LinhaHistorico De(EntradaHistorico e) =>
        new(e.Hora.ToString("dd/MM HH:mm:ss", CultureInfo.InvariantCulture), TextoDe(e.Origem), FraseComando.De(e), e.Enviado, e.Motivo);

    public static string TextoDe(OrigemComando origem) => origem switch
    {
        OrigemComando.Ia => TextoIa,
        OrigemComando.App => TextoApp,
        _ => TextoTela,
    };
}

/// <summary>
/// A frase curta de um comando, em português (EXPERIENCE, Voice and Tone): "Ganho · saída 3 ·
/// +2,0 dB", "Carregar a memória 3", "Troca de senha". Nunca mostra uma senha.
/// </summary>
public static class FraseComando
{
    public const string Separador = " · ";
    public const string TrocaSenha = "Troca de senha";
    public const string Desconhecido = "Comando desconhecido";

    public static string De(EntradaHistorico e)
    {
        var codigo = e.Endereco?.Codigo;
        var indice = e.Endereco?.Indice;
        return e.Tipo switch
        {
            TipoComando.AjusteAudio or TipoComando.Gerador => Parametro(codigo, indice, e.Valor),
            TipoComando.SalvarMemoria => $"Salvar na memória {Memoria(e.Valor)}",
            TipoComando.CarregarMemoria => $"Carregar a memória {Memoria(e.Valor)}",
            TipoComando.NomearMemoria => Juntar($"Nome da memória {MemoriaDoNome(codigo)}", e.Valor),
            TipoComando.RemoteOut => Juntar("Remote out", LigaDesliga(e.Valor)),
            TipoComando.BluetoothAudio => Juntar("Bluetooth de áudio", LigaDesliga(e.Valor)),
            TipoComando.NomeProduto => Juntar("Nome do produto", e.Valor),
            TipoComando.ResetFabrica => "Reset de fábrica",
            TipoComando.ResetAudio => "Reset de áudio",
            TipoComando.TrocaSenha => TrocaSenha,
            TipoComando.AplicarMemoriaCelular => $"Aplicar a memória {e.Valor}",
            _ => Juntar(Desconhecido, codigo),
        };
    }

    /// <summary>"Ganho · saída 3 · +2,0 dB"; "Gerador · desligado"; "EQ gráfico · banda 2,5 kHz · +1,0 dB".</summary>
    private static string Parametro(string? codigo, int? indice, string? noFio)
    {
        if (codigo is null || Catalogo.ParametroDe(codigo) is not { } p)
            return Juntar(Desconhecido, codigo);
        // O 202 liga e desliga o gerador: "Gerador · ligado", não "Gerador ligado · ligado".
        var nome = codigo == Catalogo.CodigoGeradorLigado ? "Gerador" : Maiuscula(p.Nome);
        var partes = new List<string> { nome };
        if (p.Escopo != Escopo.Global && indice is not null)
            partes.Add(p.Onde(indice));
        if (noFio is not null)
            partes.Add(Valor(p, noFio));
        return string.Join(Separador, partes);
    }

    /// <summary>O valor como a tela mostra: o rótulo (LR24, OFF, 180°, A+B), ligado/desligado, ou o número com a unidade.</summary>
    private static string Valor(Parametro p, string noFio)
    {
        if (p.Decodificar(noFio).Valor is not { } v)
            return noFio;
        if (v.Rotulo is { } rotulo)
            return rotulo;
        if (p.Tipo == TipoValor.Booleano)
            return v.Exibicao;
        return v.Numero is { } n ? FormatoValor.ComUnidade(p, n) : v.Exibicao;
    }

    private static string Memoria(string? n) =>
        int.TryParse(n, NumberStyles.None, CultureInfo.InvariantCulture, out var i) ? (i + 1).ToString(CultureInfo.InvariantCulture) : "?";

    /// <summary>231 → 1, 232 → 2, 233 → 3.</summary>
    private static string MemoriaDoNome(string? codigo) => codigo is { Length: 3 } ? codigo[2..] : "?";

    private static string? LigaDesliga(string? v) => v switch
    {
        "1" => "ligado",
        "0" => "desligado",
        _ => v,
    };

    private static string Juntar(string texto, string? detalhe) =>
        string.IsNullOrEmpty(detalhe) ? texto : texto + Separador + detalhe;

    private static string Maiuscula(string s) =>
        string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0], CultureInfo.InvariantCulture) + s[1..];
}

/// <summary>
/// A tela Histórico (Configurações › Histórico): cada comando que chegou à porta, da tela, da IA
/// e do app, com a hora, a origem, a frase e, nas recusas, o motivo. A mais nova fica em cima. A
/// lista é trocada inteira a cada entrada nova.
/// </summary>
public sealed class HistoricoViewModel : TelaBase, IDisposable
{
    public const string MsgVazio = "Nenhum comando ainda.";

    private readonly Historico _historico;
    private IReadOnlyList<LinhaHistorico> _linhas = [];

    // As entradas que a última montagem inteira já mostra: uma entrada nova que chegou antes dela
    // não entra duas vezes.
    private HashSet<EntradaHistorico> _montadas = new(ReferenceEqualityComparer.Instance);

    public HistoricoViewModel(Historico historico, IDespachante despachante, TimeProvider relogio)
        : base(despachante, relogio)
    {
        _historico = historico;
        _historico.Registrou += AoRegistrar;
        Montar();
        // O guardado pode chegar depois de a tela abrir.
        _ = _historico.Carregado.ContinueWith(_ => NaTela(Montar), TaskScheduler.Default);
    }

    /// <summary>As linhas, da mais nova à mais antiga.</summary>
    public IReadOnlyList<LinhaHistorico> Linhas
    {
        get => _linhas;
        private set
        {
            _linhas = value;
            Avisar();
            Avisar(nameof(Vazio));
            Avisar(nameof(TextoVazio));
        }
    }

    public bool Vazio => _linhas.Count == 0;

    /// <summary>"Nenhum comando ainda." com a lista vazia; senão, null.</summary>
    public string? TextoVazio => Vazio ? MsgVazio : null;

    /// <summary>Uma entrada nova: só a linha dela entra em cima (a lista não é remontada).</summary>
    private void AoRegistrar(EntradaHistorico e) => NaTela(() =>
    {
        if (_montadas.Contains(e))
            return;
        Linhas = [LinhaHistorico.De(e), .. _linhas.Take(Historico.Maximo - 1)];
    });

    /// <summary>A lista inteira, da mais nova à mais antiga (ao abrir e quando o guardado chega).</summary>
    private void Montar()
    {
        var entradas = _historico.Entradas;
        _montadas = new HashSet<EntradaHistorico>(entradas, ReferenceEqualityComparer.Instance);
        Linhas = entradas.Select(LinhaHistorico.De).ToArray();
    }

    public void Dispose() => _historico.Registrou -= AoRegistrar;
}
