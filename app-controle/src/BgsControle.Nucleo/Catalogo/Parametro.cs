using System.Globalization;
using System.Text.RegularExpressions;

// Os tipos do catálogo ficam no namespace raiz, como a Sessao: um namespace "Catalogo"
// esconderia a classe Catalogo de todo código dentro de BgsControle.Nucleo.
namespace BgsControle.Nucleo;

/// <summary>Como o parâmetro é endereçado no fio (MAPA §2, §4).</summary>
public enum Escopo
{
    /// <summary><c>#CCC#</c>; resposta <c>#CCC#valor#</c>.</summary>
    Global,

    /// <summary><c>#CCC#c#</c> com c = 0 a 3 (saída 1 = 0); resposta <c>#CCC#c#valor#</c>.</summary>
    Canal,

    /// <summary><c>020#b</c> com b = 0 a 14; resposta <c>020#b#v</c>.</summary>
    Banda,
}

/// <summary>O que o valor no fio significa, e com quantas casas ele vai.</summary>
public enum TipoValor
{
    /// <summary>dB, 1 casa.</summary>
    Decibel,

    /// <summary>Hz inteiro.</summary>
    Hertz,

    /// <summary>Q, 1 casa.</summary>
    Q,

    /// <summary>cm, 1 casa (o delay; no código do app se chama "db", mas é cm).</summary>
    Centimetro,

    /// <summary>ms com 1 casa (attack do limiter).</summary>
    Attack,

    /// <summary>ms inteiro (release do limiter).</summary>
    Release,

    /// <summary>Índice inteiro com opções (filtros, preset de EQ, velocidade do sweep).</summary>
    Indice,

    /// <summary>Booleano: na leitura, ≠0 = ligado; na escrita, 1 ou 0.</summary>
    Booleano,

    /// <summary>Texto minúsculo <c>a</c>, <c>b</c> ou <c>a+b</c>; a leitura aceita <c>ab</c>.</summary>
    Roteamento,

    /// <summary>Graus, só 0 ou 180.</summary>
    Fase,

    /// <summary>Volts × 10, inteiro (voltímetro 005).</summary>
    Decivolt,

    /// <summary>Um bit por saída (clip 150).</summary>
    BitsPorSaida,

    /// <summary>Texto livre (nomes das memórias).</summary>
    Nome,
}

/// <summary>Uma consulta de leitura: o parâmetro e, quando há, o canal ou a banda.</summary>
public sealed record Consulta(Parametro Parametro, int? Indice)
{
    /// <summary>O quadro de leitura que vai pelo BLE.</summary>
    public string Texto => Parametro.TextoConsulta(Indice);

    /// <summary>Onde o valor fica: "saída 2", "banda 160 Hz" ou "—" para um global.</summary>
    public string Onde => Parametro.Onde(Indice);

    public bool Extra => Parametro.Extra;
}

/// <summary>Valor decodificado de uma resposta, sempre com o texto do valor como veio.</summary>
public sealed record Valor
{
    /// <summary>O texto do valor dentro do quadro, sem mexer.</summary>
    public required string NoFio { get; init; }

    /// <summary>O número lido (null para roteamento e nomes).</summary>
    public decimal? Numero { get; init; }

    /// <summary>Para booleanos: ≠0 = true (mutado, ligado, clip).</summary>
    public bool? Ligado { get; init; }

    /// <summary>Roteamento normalizado (<c>a</c>, <c>b</c>, <c>a+b</c>) ou o nome.</summary>
    public string? Texto { get; init; }

    /// <summary>O nome da opção ou do valor especial: "LR24", "OFF", "auto", "A+B", "180°", "Custom".</summary>
    public string? Rotulo { get; init; }

    /// <summary>Fora da faixa do MAPA. O valor é guardado como veio, nunca corrigido.</summary>
    public bool ForaDaFaixa { get; init; }

    /// <summary>O valor pronto para a tela, em português.</summary>
    public required string Exibicao { get; init; }
}

/// <summary>Resultado de <see cref="Parametro.Decodificar"/>: o valor, ou o motivo de não ter valor.</summary>
public sealed record Decodificacao(Valor? Valor, string? Motivo)
{
    public bool Ok => Valor is not null;
}

/// <summary>
/// Um parâmetro do aparelho: código, nome, escopo, tipo, faixa, casas, opções e valores
/// especiais. Sabe montar a consulta, decodificar a resposta e codificar uma escrita.
/// </summary>
public sealed partial record Parametro
{
    public const int Canais = 4;
    public const int Bandas = 15;
    public const int TamanhoMaximoNome = 12;

    public const string MotivoIlegivel = "valor ilegível";

    private static readonly CultureInfo Fio = CultureInfo.InvariantCulture;

    public required string Codigo { get; init; }

    /// <summary>Nome curto para a lista (e para o JSON).</summary>
    public required string Nome { get; init; }

    public required Escopo Escopo { get; init; }
    public required TipoValor Tipo { get; init; }
    public string? Unidade { get; init; }
    public decimal? Minimo { get; init; }
    public decimal? Maximo { get; init; }

    /// <summary>Índice → nome (filtros, presets, fase, velocidade).</summary>
    public IReadOnlyDictionary<int, string>? Opcoes { get; init; }

    /// <summary>Valores fora da faixa com significado: threshold 0.5 = OFF, attack 0.01/0.05 = auto.</summary>
    public IReadOnlyDictionary<decimal, string>? Especiais { get; init; }

    /// <summary>Booleano: o que mostrar para ligado (≠0) e desligado (0).</summary>
    public string RotuloLigado { get; init; } = "ligado";
    public string RotuloDesligado { get; init; } = "desligado";

    /// <summary>Fora do Marco 1: perguntado uma vez por leitura, sem nova tentativa.</summary>
    public bool Extra { get; init; }

    /// <summary>Só existe leitura (voltímetro, clip): não há escrita para codificar.</summary>
    public bool SomenteLeitura { get; init; }

    /// <summary>Casas decimais no fio: 1 para dB, Q, cm e attack; 0 para o resto.</summary>
    public int Casas => Tipo is TipoValor.Decibel or TipoValor.Q or TipoValor.Centimetro or TipoValor.Attack ? 1 : 0;

    /// <summary>Quantos canais ou bandas existem (0 para um global).</summary>
    public int Indices => Escopo switch
    {
        Escopo.Canal => Canais,
        Escopo.Banda => Bandas,
        _ => 0,
    };

    /// <summary>As consultas deste parâmetro, na ordem dos canais ou das bandas.</summary>
    public IEnumerable<Consulta> Consultas() =>
        Escopo == Escopo.Global
            ? [new Consulta(this, null)]
            : Enumerable.Range(0, Indices).Select(i => new Consulta(this, i));

    // ------------------------------------------------------------------ consulta

    /// <summary><c>#CCC#</c>, <c>#CCC#c#</c> ou <c>020#b</c>.</summary>
    public string TextoConsulta(int? indice)
    {
        ValidarIndice(indice);
        return Escopo switch
        {
            Escopo.Global => $"#{Codigo}#",
            Escopo.Canal => $"#{Codigo}#{indice}#",
            _ => $"{Codigo}#{indice}",
        };
    }

    public string Onde(int? indice) => Escopo switch
    {
        Escopo.Canal => $"saída {indice + 1}",
        Escopo.Banda => $"banda {Catalogo.RotuloBanda(indice!.Value)}",
        _ => "—",
    };

    private void ValidarIndice(int? indice)
    {
        if (Escopo == Escopo.Global)
        {
            if (indice is not null)
                throw new ArgumentOutOfRangeException(nameof(indice), $"O {Codigo} é global: não tem canal.");
            return;
        }
        if (indice is null || indice < 0 || indice >= Indices)
            throw new ArgumentOutOfRangeException(nameof(indice),
                Escopo == Escopo.Canal ? $"Canal de 0 a {Canais - 1}." : $"Banda de 0 a {Bandas - 1}.");
    }

    // ------------------------------------------------------------------ decodificação

    /// <summary>
    /// Decodifica o texto do valor de uma resposta. Fora da faixa, o valor fica como veio,
    /// com a marca <see cref="Valor.ForaDaFaixa"/>. Só um texto que não é número (num tipo
    /// numérico) não tem valor: <see cref="MotivoIlegivel"/>.
    /// </summary>
    public Decodificacao Decodificar(string noFio)
    {
        var s = noFio.Trim();
        switch (Tipo)
        {
            case TipoValor.Nome:
                return Ok(new Valor { NoFio = noFio, Texto = s, Exibicao = s.Length == 0 ? "(vazio)" : $"\"{s}\"" });

            case TipoValor.Roteamento:
            {
                var v = s.ToLowerInvariant();
                if (v == "ab")
                    v = "a+b"; // a leitura aceita também "ab" (MAPA §4)
                var rotulo = v switch { "a" => "A", "b" => "B", "a+b" => "A+B", _ => null };
                return Ok(new Valor
                {
                    NoFio = noFio,
                    Texto = v,
                    Rotulo = rotulo,
                    ForaDaFaixa = rotulo is null,
                    Exibicao = Marcar(rotulo ?? s, rotulo is null),
                });
            }
        }

        if (!NumeroDoFio().IsMatch(s)
            || !decimal.TryParse(s, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, Fio, out var n))
            return new Decodificacao(null, MotivoIlegivel);

        var inteiro = n == decimal.Truncate(n);

        if (Tipo == TipoValor.Booleano)
        {
            // ≠0 = ligado, mas só 0 e 1 estão na faixa (2, -1, 0.5 ficam marcados).
            var ligado = n != 0;
            var foraBool = n is not (0 or 1);
            return Ok(new Valor
            {
                NoFio = noFio,
                Numero = n,
                Ligado = ligado,
                ForaDaFaixa = foraBool,
                Exibicao = Marcar(ligado ? RotuloLigado : RotuloDesligado, foraBool),
            });
        }

        if (Especiais is not null && Especiais.TryGetValue(n, out var especial))
            return Ok(new Valor { NoFio = noFio, Numero = n, Rotulo = especial, Exibicao = especial });

        if (Opcoes is not null)
        {
            string? opcao = null;
            var achou = inteiro && n is >= int.MinValue and <= int.MaxValue && Opcoes.TryGetValue((int)n, out opcao);
            return Ok(new Valor
            {
                NoFio = noFio,
                Numero = n,
                Rotulo = achou ? opcao : null,
                ForaDaFaixa = !achou,
                Exibicao = Marcar(achou ? opcao! : FormatarNumero(n), !achou),
            });
        }

        var fora = (Casas == 0 && !inteiro)
                   || (Minimo is { } min && n < min)
                   || (Maximo is { } max && n > max);
        return Ok(new Valor { NoFio = noFio, Numero = n, ForaDaFaixa = fora, Exibicao = Marcar(Exibir(n), fora) });
    }

    private static Decodificacao Ok(Valor v) => new(v, null);

    private static string Marcar(string texto, bool fora) => fora ? $"{texto} (fora da faixa)" : texto;

    private string Exibir(decimal n) => Tipo switch
    {
        TipoValor.Decibel => $"{(n > 0 ? "+" : "")}{FormatarNumero(n)} dB",
        TipoValor.Hertz => $"{FormatarNumero(n)} Hz",
        TipoValor.Centimetro => $"{FormatarNumero(n)} cm",
        TipoValor.Attack or TipoValor.Release => $"{FormatarNumero(n)} ms",
        TipoValor.Decivolt => $"{FormatarNumero(n / 10m, casas: 1)} V",
        TipoValor.BitsPorSaida => SaidasComClip(n),
        _ => FormatarNumero(n),
    };

    /// <summary>Número com vírgula, com as casas do parâmetro (ou mais, se o fio trouxe mais).</summary>
    private string FormatarNumero(decimal n, int? casas = null)
    {
        var c = casas ?? Casas;
        var formato = c == 0 ? "0.###" : "0.0##";
        return n.ToString(formato, Fio).Replace('.', ',');
    }

    private static string SaidasComClip(decimal n)
    {
        if (n != decimal.Truncate(n) || n < 0 || n > 15)
            return n.ToString(Fio);
        var bits = (int)n;
        var saidas = Enumerable.Range(0, Canais).Where(c => (bits & (1 << c)) != 0).Select(c => (c + 1).ToString(Fio)).ToList();
        return saidas.Count == 0 ? "nenhuma saída" : $"saídas {string.Join(", ", saidas)}";
    }

    // ------------------------------------------------------------------ codificação de escrita

    /// <summary>
    /// <c>*CCC*v*</c>, <c>*CCC*c*v*</c> ou <c>020*b*v</c> (sem <c>*</c> nas pontas). Números
    /// com ponto: 1 casa para dB, Q, cm e attack (<c>%.1f</c>), inteiro para o resto; um valor
    /// especial vai como é (<c>0.5</c>, <c>0.05</c>). Recusa valor fora da faixa ou com mais
    /// casas do que o fio leva. Só codifica: quem envia é a sessão, pela trava.
    /// </summary>
    public string CodificarEscrita(int? indice, decimal valor)
    {
        ValidarEscrita(indice);
        if (Tipo is TipoValor.Roteamento or TipoValor.Nome)
            throw new ArgumentException($"O {Codigo} leva texto, não número.", nameof(valor));
        if (Tipo == TipoValor.Booleano)
        {
            if (valor is not (0 or 1))
                throw new ArgumentOutOfRangeException(nameof(valor), $"O {Codigo} leva 1 ou 0.");
            return Montar(indice, valor == 1 ? "1" : "0");
        }

        if (Especiais is not null && Especiais.ContainsKey(valor))
            return Montar(indice, valor.ToString("0.0#", Fio));

        if (Opcoes is not null)
        {
            if (valor != decimal.Truncate(valor) || valor is < int.MinValue or > int.MaxValue || !Opcoes.ContainsKey((int)valor))
                throw new ArgumentOutOfRangeException(nameof(valor), $"O {Codigo} só aceita {string.Join(", ", Opcoes.Keys)}.");
            return Montar(indice, ((int)valor).ToString(Fio));
        }

        if ((Minimo is { } min && valor < min) || (Maximo is { } max && valor > max))
            throw new ArgumentOutOfRangeException(nameof(valor), $"O {Codigo} vai de {Minimo} a {Maximo}.");
        if (decimal.Round(valor, Casas) != valor)
            throw new ArgumentException($"O {Codigo} leva {(Casas == 0 ? "um inteiro" : "uma casa decimal")}.", nameof(valor));

        return Montar(indice, Casas == 0 ? decimal.Truncate(valor).ToString("0", Fio) : valor.ToString("0.0", Fio));
    }

    /// <summary>Booleano: <c>1</c> ou <c>0</c>.</summary>
    public string CodificarEscrita(int? indice, bool ligado)
    {
        if (Tipo != TipoValor.Booleano)
            throw new ArgumentException($"O {Codigo} não é liga/desliga.", nameof(ligado));
        return CodificarEscrita(indice, ligado ? 1m : 0m);
    }

    /// <summary>Roteamento (<c>a</c>, <c>b</c>, <c>a+b</c>) ou nome da memória.</summary>
    public string CodificarEscrita(int? indice, string texto)
    {
        ValidarEscrita(indice);
        switch (Tipo)
        {
            case TipoValor.Roteamento:
            {
                var v = texto.Trim().ToLowerInvariant();
                if (v == "ab")
                    v = "a+b";
                if (v is not ("a" or "b" or "a+b"))
                    throw new ArgumentOutOfRangeException(nameof(texto), "O roteamento é a, b ou a+b.");
                return Montar(indice, v);
            }
            case TipoValor.Nome:
                if (!NomeValido().IsMatch(texto))
                    throw new ArgumentException(
                        $"O nome tem de 1 a {TamanhoMaximoNome} caracteres ASCII visíveis, sem # nem *.", nameof(texto));
                return Montar(indice, texto);
            default:
                throw new ArgumentException($"O {Codigo} leva número, não texto.", nameof(texto));
        }
    }

    private void ValidarEscrita(int? indice)
    {
        if (SomenteLeitura)
            throw new InvalidOperationException($"O {Codigo} ({Nome}) só tem leitura.");
        ValidarIndice(indice);
    }

    private string Montar(int? indice, string v) => Escopo switch
    {
        Escopo.Global => $"*{Codigo}*{v}*",
        Escopo.Canal => $"*{Codigo}*{indice}*{v}*",
        _ => $"{Codigo}*{indice}*{v}",
    };

    [GeneratedRegex(@"^[+-]?(\d+(\.\d*)?|\.\d+)$", RegexOptions.CultureInvariant)]
    private static partial Regex NumeroDoFio();

    // ASCII visível, de 1 a 12 caracteres, pelo menos um que não é espaço, sem # nem *.
    // \z e não $: o $ aceita um \n no fim.
    [GeneratedRegex(@"^(?=[\x20-\x7E]{1,12}\z)(?=.*[^ ])[^#*]*\z", RegexOptions.CultureInvariant)]
    private static partial Regex NomeValido();
}
