using BgsControle.Nucleo.Protocolo;

namespace BgsControle.Nucleo;

/// <summary>Uma escrita de ajuste de áudio reconhecida pelo catálogo: o parâmetro, o canal ou a banda e o valor no fio.</summary>
public sealed record EscritaAudio(Parametro Parametro, int? Indice, string NoFio);

public enum TipoComandoMemoria
{
    /// <summary><c>*240*n*</c>.</summary>
    Salvar,

    /// <summary><c>*241*n*</c>.</summary>
    Carregar,

    /// <summary><c>*23N*NOME*</c>.</summary>
    Nomear,
}

/// <summary>Um comando de memória reconhecido pela trava: o tipo, a memória (0 a 2) e o nome, no <c>23N</c>.</summary>
public sealed record ComandoMemoria(TipoComandoMemoria Tipo, int Memoria, string? Nome);

/// <summary>Um comando do gerador reconhecido pela trava: o parâmetro (200–202, 210–213) e o valor.</summary>
public sealed record ComandoGerador(Parametro Parametro, decimal Valor)
{
    /// <summary><c>*202*0*</c>: desligar, o lado seguro (passa sem a chave).</summary>
    public bool Desligar => Parametro.Codigo == Catalogo.CodigoGeradorLigado && Valor == 0;
}

/// <summary>Os comandos de aparelho da história 7b (MAPA §4 e §5).</summary>
public enum TipoComandoAparelho
{
    /// <summary><c>*252*0*</c> / <c>*252*1*</c>: o remote out (os módulos).</summary>
    RemoteOut,

    /// <summary><c>*251*0*</c> / <c>*251*1*</c>: o Bluetooth de áudio (Audio Classic).</summary>
    BluetoothAudio,

    /// <summary><c>*002*NOME*</c>.</summary>
    NomeProduto,

    /// <summary><c>*004*</c>.</summary>
    ResetFabrica,

    /// <summary><c>*242*</c>.</summary>
    ResetAudio,

    /// <summary><c>*001*ANTIGA*NOVA*</c>.</summary>
    TrocaSenha,
}

/// <summary>
/// Um comando de aparelho reconhecido pela trava: o tipo e, conforme o tipo, o liga/desliga, o
/// nome do produto ou as duas senhas (nunca vão para o log).
/// </summary>
public sealed record ComandoAparelho(TipoComandoAparelho Tipo, bool? Ligar = null, string? Nome = null, string? SenhaAntiga = null, string? SenhaNova = null);

/// <summary>
/// A fonte única dos parâmetros do PRO 2.4BT + AUDIO (MAPA §4): a trava pergunta a ele se um
/// quadro é uma leitura conhecida, o leitor monta a fila a partir dele, e as escritas futuras
/// usam o mesmo codificador.
/// </summary>
public static class Catalogo
{
    /// <summary>Frequências das 15 bandas do EQ gráfico, banda 0 a 14 (MAPA §4).</summary>
    public static IReadOnlyList<int> BandasHz { get; } =
        [25, 40, 63, 100, 160, 250, 400, 630, 1000, 1600, 2500, 4000, 6300, 10000, 16000];

    /// <summary>Filtros do crossover: 0 OFF, 1–7 LR, 8–15 BT (MAPA §4).</summary>
    public static IReadOnlyDictionary<int, string> Filtros { get; } = new Dictionary<int, string>
    {
        [0] = "OFF",
        [1] = "LR12", [2] = "LR18", [3] = "LR24", [4] = "LR30", [5] = "LR36", [6] = "LR42", [7] = "LR48",
        [8] = "BT6", [9] = "BT12", [10] = "BT18", [11] = "BT24", [12] = "BT30", [13] = "BT36", [14] = "BT42", [15] = "BT48",
    };

    /// <summary>Presets do EQ de 15 bandas: 0–11 e 255 Custom (MAPA §4).</summary>
    public static IReadOnlyDictionary<int, string> PresetsEq { get; } = new Dictionary<int, string>
    {
        [0] = "Flat", [1] = "Loudness", [2] = "Bass Boost", [3] = "Mid-Bass Boost", [4] = "Treble Boost",
        [5] = "Powerful", [6] = "Electronic", [7] = "Rock Style", [8] = "Hip-Hop Style", [9] = "Pop Music",
        [10] = "Vocal", [11] = "Competition", [255] = "Custom",
    };

    private static readonly IReadOnlyDictionary<int, string> Fases = new Dictionary<int, string>
    {
        [0] = "0°",
        [180] = "180°",
    };

    private static readonly IReadOnlyDictionary<int, string> VelocidadesSweep = new Dictionary<int, string>
    {
        [0] = "desligado", [1] = "lento", [2] = "médio", [3] = "rápido",
    };

    /// <summary>Os 22 parâmetros do Marco 1, na ordem da leitura: 5 globais + 16 × 4 canais + 15 bandas = 84.</summary>
    public static IReadOnlyList<Parametro> Marco1 { get; } =
    [
        Db("080", "master", Escopo.Global, -80, 0),
        Db("081", "ganho", Escopo.Canal, -45, 15),
        Bool("100", "mute", Escopo.Canal, "mutado", "com som"),
        new() { Codigo = "010", Nome = "roteamento", Escopo = Escopo.Canal, Tipo = TipoValor.Roteamento },
        new() { Codigo = "060", Nome = "fase", Escopo = Escopo.Canal, Tipo = TipoValor.Fase, Unidade = "graus", Opcoes = Fases },
        Hz("040", "HPF frequência", Escopo.Canal),
        Hz("041", "LPF frequência", Escopo.Canal),
        Indice("042", "HPF filtro", Escopo.Canal, Filtros),
        Indice("043", "LPF filtro", Escopo.Canal, Filtros),
        new() { Codigo = "050", Nome = "delay", Escopo = Escopo.Canal, Tipo = TipoValor.Centimetro, Unidade = "cm", Minimo = 0, Maximo = 275 },
        Bool("070", "limiter auto", Escopo.Canal, "auto", "manual"),
        new()
        {
            Codigo = "071", Nome = "limiter threshold", Escopo = Escopo.Canal, Tipo = TipoValor.Decibel, Unidade = "dB",
            Minimo = -48, Maximo = 0, Especiais = new Dictionary<decimal, string> { [0.5m] = "OFF" },
        },
        new()
        {
            // No modo Auto o app manda 0.01 ou 0.05, com 2 casas (MAPA §8, lacuna 6).
            Codigo = "072", Nome = "limiter attack", Escopo = Escopo.Canal, Tipo = TipoValor.Attack, Unidade = "ms",
            Minimo = 0.1m, Maximo = 100, Especiais = new Dictionary<decimal, string> { [0.01m] = "auto", [0.05m] = "auto" },
        },
        new() { Codigo = "073", Nome = "limiter release", Escopo = Escopo.Canal, Tipo = TipoValor.Release, Unidade = "ms", Minimo = 1, Maximo = 1600 },
        Hz("090", "EQ saída frequência", Escopo.Canal),
        Db("091", "EQ saída ganho", Escopo.Canal, -12, 12),
        Q("092", "EQ saída Q", Escopo.Canal, 0.1m, 10),
        Db("030", "EQ entrada ganho", Escopo.Global, -12, 12),
        Hz("031", "EQ entrada frequência", Escopo.Global),
        Q("032", "EQ entrada Q", Escopo.Global, 0.4m, 10),
        Db("020", "EQ gráfico", Escopo.Banda, -12, 12),
        Indice("220", "preset de EQ", Escopo.Global, PresetsEq),
    ];

    /// <summary>
    /// Fora do Marco 1 (MAPA §4): voltímetro, clip, gerador, nomes das memórias, Audio
    /// Classic e remote out. Perguntados uma vez em cada leitura; o texto cru fica registrado,
    /// seja o que vier. As faixas do gerador que o MAPA não dá (frequências, ganho do sweep)
    /// seguem as do resto do aparelho.
    /// </summary>
    public static IReadOnlyList<Parametro> Extras { get; } =
    [
        Extra(new() { Codigo = "005", Nome = "voltímetro", Escopo = Escopo.Global, Tipo = TipoValor.Decivolt, Unidade = "V × 10", SomenteLeitura = true }),
        Extra(Bool("111", "clip da entrada", Escopo.Global, "clip", "sem clip") with { SomenteLeitura = true }),
        Extra(new() { Codigo = "150", Nome = "clip das saídas", Escopo = Escopo.Global, Tipo = TipoValor.BitsPorSaida, SomenteLeitura = true }),
        Extra(Db("200", "gerador ganho", Escopo.Global, -60, 0)),
        Extra(Hz("201", "gerador frequência", Escopo.Global)),
        Extra(Bool("202", "gerador ligado", Escopo.Global, "ligado", "desligado")),
        Extra(Hz("210", "sweep frequência inicial", Escopo.Global)),
        Extra(Hz("211", "sweep frequência final", Escopo.Global)),
        Extra(Db("212", "sweep ganho", Escopo.Global, -60, 0)),
        Extra(Indice("213", "sweep velocidade", Escopo.Global, VelocidadesSweep)),
        Extra(Nome("231", "nome da memória 1")),
        Extra(Nome("232", "nome da memória 2")),
        Extra(Nome("233", "nome da memória 3")),
        Extra(Bool("251", "Audio Classic", Escopo.Global, "ligado", "desligado")),
        Extra(Bool("252", "remote out", Escopo.Global, "ligado", "desligado")),
    ];

    public static IReadOnlyList<Parametro> Todos { get; } = [.. Marco1, .. Extras];

    /// <summary>As 84 consultas do Marco 1, na ordem da leitura.</summary>
    public static IReadOnlyList<Consulta> ConsultasMarco1 { get; } = Marco1.SelectMany(p => p.Consultas()).ToArray();

    public static IReadOnlyList<Consulta> ConsultasExtras { get; } = Extras.SelectMany(p => p.Consultas()).ToArray();

    /// <summary>A fila inteira: o Marco 1 primeiro, depois os extras.</summary>
    public static IReadOnlyList<Consulta> Consultas { get; } = [.. ConsultasMarco1, .. ConsultasExtras];

    public static int TotalMarco1 => ConsultasMarco1.Count;
    public static int TotalExtras => ConsultasExtras.Count;

    private static readonly Dictionary<string, Parametro> PorCodigo = Todos.ToDictionary(p => p.Codigo);
    private static readonly Dictionary<string, Consulta> PorTexto = Consultas.ToDictionary(c => c.Texto, StringComparer.Ordinal);

    public static Parametro? ParametroDe(string codigo) => PorCodigo.GetValueOrDefault(codigo);

    /// <summary>
    /// O quadro é exatamente uma consulta de leitura do catálogo, no escopo certo
    /// (<c>#080#</c> sim; <c>#004#</c>, <c>#080#0#</c>, <c>#081#</c>, <c>#081#4#</c>, <c>020#15</c> não).
    /// </summary>
    public static bool EhLeitura(string quadro) => PorTexto.ContainsKey(quadro);

    public static Consulta? ConsultaDe(string quadro) => PorTexto.GetValueOrDefault(quadro);

    private static readonly Dictionary<string, Parametro> Marco1PorCodigo = Marco1.ToDictionary(p => p.Codigo);

    /// <summary>
    /// Se o quadro é exatamente o texto que <see cref="Parametro.CodificarEscrita(int?, decimal)"/>
    /// (ou a versão de texto, para o roteamento) produz para um parâmetro do Marco 1, devolve o
    /// parâmetro, o índice e o valor no fio; senão null. É a regra da trava para os ajustes de
    /// áudio (MAPA §4): <c>*081*0*-3.0*</c> sim; <c>*081*0*-3*</c>, <c>*240*0*</c>, <c>*202*1*</c> não.
    /// </summary>
    public static EscritaAudio? EscritaDeAudio(string quadro)
    {
        var q = Quadros.Analisar(quadro);
        if (q is null || !Marco1PorCodigo.TryGetValue(q.Codigo, out var p))
            return null;
        var segmentos = p.Escopo == Escopo.Global ? 1 : 2;
        if (q.Segmentos.Count != segmentos)
            return null;
        int? indice = null;
        if (segmentos == 2)
        {
            if (!int.TryParse(q.Segmentos[0], System.Globalization.NumberStyles.None,
                    System.Globalization.CultureInfo.InvariantCulture, out var i))
                return null;
            indice = i;
        }
        var valor = q.Segmentos[^1];
        try
        {
            string codificado;
            if (p.Tipo == TipoValor.Roteamento)
            {
                codificado = p.CodificarEscrita(indice, valor);
            }
            else
            {
                if (!decimal.TryParse(valor, System.Globalization.NumberStyles.AllowLeadingSign | System.Globalization.NumberStyles.AllowDecimalPoint,
                        System.Globalization.CultureInfo.InvariantCulture, out var numero))
                    return null;
                codificado = p.CodificarEscrita(indice, numero);
            }
            return codificado == quadro ? new EscritaAudio(p, indice, valor) : null;
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException)
        {
            return null;
        }
    }

    // ------------------------------------------------------------------ memórias (MAPA §5)

    /// <summary>As 3 memórias do DSP: <c>n</c> de 0 a 2 no fio; a memória 1 é o <c>n</c> 0 e o nome 231.</summary>
    public const int Memorias = 3;

    public const string CodigoSalvarMemoria = "240";
    public const string CodigoCarregarMemoria = "241";

    // Suposição MEMORIA-N-NOME (história 12): o formato *23N*NOME* e "memória 1 = n 0 = nome 231".
    private static readonly System.Text.RegularExpressions.Regex QuadroSalvarOuCarregar =
        new(@"^\*(240|241)\*([0-2])\*\z", System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    private static readonly System.Text.RegularExpressions.Regex QuadroNomeMemoria =
        new(@"^\*23([1-3])\*([A-Z0-9 _]{1,12})\*\z", System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    private static readonly System.Text.RegularExpressions.Regex NomeMemoriaValido =
        new(@"^[A-Z0-9 _]{1,12}\z", System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    /// <summary>O código do nome da memória <paramref name="n"/> (0 a 2): 231, 232 ou 233.</summary>
    public static string CodigoNomeMemoria(int n)
    {
        ValidarMemoria(n);
        return $"23{n + 1}";
    }

    /// <summary><c>*240*n*</c>: salva o estado atual na memória n (0 a 2).</summary>
    public static string SalvarMemoria(int n)
    {
        ValidarMemoria(n);
        return $"*{CodigoSalvarMemoria}*{n}*";
    }

    /// <summary><c>*241*n*</c>: carrega a memória n (0 a 2).</summary>
    public static string CarregarMemoria(int n)
    {
        ValidarMemoria(n);
        return $"*{CodigoCarregarMemoria}*{n}*";
    }

    /// <summary><c>*23N*NOME*</c>, com o nome já normalizado (<see cref="NormalizarNomeMemoria"/>).</summary>
    public static string NomearMemoria(int n, string nome)
    {
        ValidarMemoria(n);
        if (NormalizarNomeMemoria(nome) is not { } normalizado || normalizado != nome)
            throw new ArgumentException("O nome tem de 1 a 12 caracteres: A–Z, 0–9, espaço ou _.", nameof(nome));
        return $"*{CodigoNomeMemoria(n)}*{nome}*";
    }

    /// <summary>
    /// O nome de memória como vai no fio: sem espaços nas pontas e em maiúsculas, de 1 a 12
    /// caracteres entre A–Z, 0–9, espaço e <c>_</c>. Fora disso, null (nada sai).
    /// Suposição REGRA-NOMES: a regra de nomes da Taramps vale também para as memórias.
    /// </summary>
    public static string? NormalizarNomeMemoria(string? digitado)
    {
        if (digitado is null)
            return null;
        var nome = digitado.Trim().ToUpperInvariant();
        return NomeMemoriaValido.IsMatch(nome) ? nome : null;
    }

    /// <summary>
    /// Se o quadro é exatamente <c>*240*n*</c>, <c>*241*n*</c> (n = 0 a 2) ou <c>*23N*NOME*</c>
    /// (N = 1 a 3, nome normalizado), devolve o comando; senão null. É a regra da trava para as
    /// memórias: <c>*240*5*</c>, <c>*241*0</c>, <c>*231*filme*</c> e <c>*231* FILME*</c> não passam.
    /// </summary>
    public static ComandoMemoria? ComandoDeMemoria(string quadro)
    {
        if (QuadroSalvarOuCarregar.Match(quadro) is { Success: true } m)
        {
            var n = m.Groups[2].Value[0] - '0';
            return new ComandoMemoria(m.Groups[1].Value == CodigoSalvarMemoria ? TipoComandoMemoria.Salvar : TipoComandoMemoria.Carregar, n, null);
        }
        if (QuadroNomeMemoria.Match(quadro) is { Success: true } nome)
        {
            var texto = nome.Groups[2].Value;
            if (NormalizarNomeMemoria(texto) != texto)
                return null; // espaço nas pontas ou só espaços
            return new ComandoMemoria(TipoComandoMemoria.Nomear, nome.Groups[1].Value[0] - '1', texto);
        }
        return null;
    }

    // ------------------------------------------------------------------ gerador (MAPA §4 e §5)

    public const string CodigoGeradorGanho = "200";
    public const string CodigoGeradorFrequencia = "201";
    public const string CodigoGeradorLigado = "202";
    public const string CodigoSweepInicio = "210";
    public const string CodigoSweepFim = "211";
    public const string CodigoSweepGanho = "212";
    public const string CodigoSweepVelocidade = "213";

    /// <summary>Os códigos do gerador: 200–202 e 210–213.</summary>
    public static IReadOnlyList<string> CodigosDoGerador { get; } =
    [
        CodigoGeradorGanho, CodigoGeradorFrequencia, CodigoGeradorLigado,
        CodigoSweepInicio, CodigoSweepFim, CodigoSweepGanho, CodigoSweepVelocidade,
    ];

    /// <summary><c>*202*1*</c>.</summary>
    public static string LigarGerador { get; } = ParametroDe(CodigoGeradorLigado)!.CodificarEscrita(null, true);

    /// <summary><c>*202*0*</c>.</summary>
    public static string DesligarGerador { get; } = ParametroDe(CodigoGeradorLigado)!.CodificarEscrita(null, false);

    /// <summary>
    /// Se o quadro é exatamente o texto que <see cref="Parametro.CodificarEscrita(int?, decimal)"/>
    /// produz para um parâmetro do gerador (200–202, 210–213), na faixa do catálogo, devolve o
    /// parâmetro e o valor; senão null. É a regra da trava para o gerador: <c>*200*-20.0*</c>,
    /// <c>*213*2*</c> e <c>*202*0*</c> sim; <c>*200*-20*</c>, <c>*200*1.0*</c>, <c>*201*5*</c>,
    /// <c>*202*2*</c> e <c>*213*4*</c> não.
    /// </summary>
    public static ComandoGerador? ComandoDeGerador(string quadro)
    {
        var q = Quadros.Analisar(quadro);
        if (q is null || !CodigosDoGerador.Contains(q.Codigo) || q.Segmentos.Count != 1)
            return null;
        var p = ParametroDe(q.Codigo)!;
        if (!decimal.TryParse(q.Segmentos[0], System.Globalization.NumberStyles.AllowLeadingSign | System.Globalization.NumberStyles.AllowDecimalPoint,
                System.Globalization.CultureInfo.InvariantCulture, out var valor))
            return null;
        try
        {
            return p.CodificarEscrita(null, valor) == quadro ? new ComandoGerador(p, valor) : null;
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException)
        {
            return null;
        }
    }

    // ------------------------------------------------------------------ aparelho (MAPA §4 e §5, história 7b)

    public const string CodigoTrocaSenha = "001";
    public const string CodigoNomeProduto = "002";
    public const string CodigoResetFabrica = "004";
    public const string CodigoResetAudio = "242";
    public const string CodigoBluetoothAudio = "251";
    public const string CodigoRemoteOut = "252";

    /// <summary>Os códigos dos comandos de aparelho: 001, 002, 004, 242, 251 e 252.</summary>
    public static IReadOnlyList<string> CodigosDoAparelho { get; } =
    [
        CodigoTrocaSenha, CodigoNomeProduto, CodigoResetFabrica, CodigoResetAudio, CodigoBluetoothAudio, CodigoRemoteOut,
    ];

    /// <summary><c>*004*</c>: o reset de fábrica.</summary>
    public const string ResetFabrica = "*004*";

    /// <summary><c>*242*</c>: o reset das configurações de áudio.</summary>
    public const string ResetAudio = "*242*";

    private static readonly System.Text.RegularExpressions.Regex QuadroLigaDesliga =
        new(@"^\*(251|252)\*([01])\*\z", System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    private static readonly System.Text.RegularExpressions.Regex QuadroNomeProduto =
        new(@"^\*002\*([A-Z0-9 _]{1,12})\*\z", System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    private static readonly System.Text.RegularExpressions.Regex QuadroTrocaSenha =
        new(@"^\*001\*([0-9A-Z]{4})\*([0-9A-Z]{4})\*\z", System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    /// <summary><c>*252*1*</c> ou <c>*252*0*</c>.</summary>
    public static string RemoteOut(bool ligar) => ParametroDe(CodigoRemoteOut)!.CodificarEscrita(null, ligar);

    /// <summary><c>*251*1*</c> ou <c>*251*0*</c>.</summary>
    public static string BluetoothAudio(bool ligar) => ParametroDe(CodigoBluetoothAudio)!.CodificarEscrita(null, ligar);

    /// <summary><c>*002*NOME*</c>, com o nome já normalizado (<see cref="NormalizarNomeProduto"/>).</summary>
    public static string NomearProduto(string nome)
    {
        if (NormalizarNomeProduto(nome) is not { } normalizado || normalizado != nome)
            throw new ArgumentException("O nome tem de 1 a 12 caracteres: A–Z, 0–9, espaço ou _.", nameof(nome));
        return $"*{CodigoNomeProduto}*{nome}*";
    }

    /// <summary><c>*001*ANTIGA*NOVA*</c>: as duas no formato da senha (4 caracteres [0-9A-Z]) e diferentes.</summary>
    public static string TrocarSenha(string antiga, string nova)
    {
        if (Quadros.NormalizarSenha(antiga) != antiga || Quadros.NormalizarSenha(nova) != nova)
            throw new ArgumentException("A senha tem 4 letras ou números.");
        if (antiga == nova)
            throw new ArgumentException("A senha nova é igual à atual.", nameof(nova));
        return $"*{CodigoTrocaSenha}*{antiga}*{nova}*";
    }

    /// <summary>
    /// O nome do produto como vai no fio: a mesma regra do nome da memória (a regra do Demo): sem
    /// espaços nas pontas, em maiúsculas, de 1 a 12 caracteres entre A–Z, 0–9, espaço e <c>_</c>.
    /// Fora disso, null (nada sai).
    /// </summary>
    public static string? NormalizarNomeProduto(string? digitado) => NormalizarNomeMemoria(digitado);

    /// <summary>
    /// Se o quadro é exatamente um comando de aparelho, devolve o comando; senão null. É a regra da
    /// trava para a história 7b: <c>*252*0*</c>, <c>*251*1*</c>, <c>*002*SALA 1*</c>, <c>*004*</c>,
    /// <c>*242*</c> e <c>*001*AB12*CD34*</c> sim; <c>*252*2*</c>, <c>*002*sala*</c>,
    /// <c>*002* SALA*</c>, <c>*004*1*</c>, <c>*001*ab12*CD34*</c> e <c>*001*AB12*AB12*</c> não.
    /// </summary>
    public static ComandoAparelho? ComandoDeAparelho(string quadro)
    {
        if (quadro == ResetFabrica)
            return new ComandoAparelho(TipoComandoAparelho.ResetFabrica);
        if (quadro == ResetAudio)
            return new ComandoAparelho(TipoComandoAparelho.ResetAudio);
        if (QuadroLigaDesliga.Match(quadro) is { Success: true } liga)
        {
            var tipo = liga.Groups[1].Value == CodigoRemoteOut ? TipoComandoAparelho.RemoteOut : TipoComandoAparelho.BluetoothAudio;
            return new ComandoAparelho(tipo, Ligar: liga.Groups[2].Value == "1");
        }
        if (QuadroNomeProduto.Match(quadro) is { Success: true } nome)
        {
            var texto = nome.Groups[1].Value;
            return NormalizarNomeProduto(texto) == texto ? new ComandoAparelho(TipoComandoAparelho.NomeProduto, Nome: texto) : null;
        }
        if (QuadroTrocaSenha.Match(quadro) is { Success: true } troca)
        {
            var antiga = troca.Groups[1].Value;
            var nova = troca.Groups[2].Value;
            return antiga != nova ? new ComandoAparelho(TipoComandoAparelho.TrocaSenha, SenhaAntiga: antiga, SenhaNova: nova) : null;
        }
        return null;
    }

    private static void ValidarMemoria(int n)
    {
        if (n is < 0 or >= Memorias)
            throw new ArgumentOutOfRangeException(nameof(n), "Memória de 0 a 2 no fio (1 a 3 na tela).");
    }

    public static string RotuloBanda(int banda)
    {
        var hz = BandasHz[banda];
        return hz >= 1000 ? $"{(hz / 1000m).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture).Replace('.', ',')} kHz" : $"{hz} Hz";
    }

    /// <summary>
    /// Uma resposta a uma consulta do catálogo que chegou sem <c>\r\n</c> já está inteira?
    /// Nunca para um extra (o número de segmentos não é conhecido). Para o Marco 1, sim
    /// quando termina num separador e tem todos os segmentos do escopo (<c>#080#+0.0#</c>,
    /// <c>#081#0#+10.0#</c>), ou quando é uma banda com o número inteiro (<c>020#3#-1.0</c>).
    /// O eco <c>#081#0#</c> ainda não está: o valor pode estar a caminho.
    /// </summary>
    public static bool RespostaInteira(string texto)
    {
        var q = Quadros.Analisar(texto);
        // Um extra pode ter vários segmentos (#232#0#MUSICA#): só o \r\n diz que acabou.
        if (q is null || ParametroDe(q.Codigo) is not { } p || p.Extra)
            return false;
        if (p.Escopo == Escopo.Banda && !texto.TrimStart().StartsWith('#'))
            return q.Segmentos.Count == 2 && BandaInteira(q.Segmentos[1]);
        if (!Quadros.TerminaEmSeparador(texto))
            return false;
        return q.Segmentos.Count >= (p.Escopo == Escopo.Global ? 1 : 2);
    }

    private static bool BandaInteira(string valor)
    {
        var v = valor.Trim();
        var ponto = v.IndexOf('.');
        return ponto > 0 && ponto == v.Length - 2 && char.IsAsciiDigit(v[^1]);
    }

    // ------------------------------------------------------------------ construtores

    private static Parametro Db(string codigo, string nome, Escopo escopo, decimal min, decimal max) =>
        new() { Codigo = codigo, Nome = nome, Escopo = escopo, Tipo = TipoValor.Decibel, Unidade = "dB", Minimo = min, Maximo = max };

    private static Parametro Hz(string codigo, string nome, Escopo escopo) =>
        new() { Codigo = codigo, Nome = nome, Escopo = escopo, Tipo = TipoValor.Hertz, Unidade = "Hz", Minimo = 10, Maximo = 22000 };

    private static Parametro Q(string codigo, string nome, Escopo escopo, decimal min, decimal max) =>
        new() { Codigo = codigo, Nome = nome, Escopo = escopo, Tipo = TipoValor.Q, Minimo = min, Maximo = max };

    private static Parametro Indice(string codigo, string nome, Escopo escopo, IReadOnlyDictionary<int, string> opcoes) =>
        new() { Codigo = codigo, Nome = nome, Escopo = escopo, Tipo = TipoValor.Indice, Opcoes = opcoes };

    private static Parametro Bool(string codigo, string nome, Escopo escopo, string ligado, string desligado) =>
        new() { Codigo = codigo, Nome = nome, Escopo = escopo, Tipo = TipoValor.Booleano, RotuloLigado = ligado, RotuloDesligado = desligado };

    private static Parametro Nome(string codigo, string nome) =>
        new() { Codigo = codigo, Nome = nome, Escopo = Escopo.Global, Tipo = TipoValor.Nome };

    private static Parametro Extra(Parametro p) => p with { Extra = true };
}
