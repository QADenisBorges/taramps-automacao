using System.Text;
using System.Text.RegularExpressions;

namespace BgsControle.Nucleo.Protocolo;

/// <summary>UUIDs do GATT do PRO 2.4BT + AUDIO (MAPA §1).</summary>
public static class Gatt
{
    /// <summary>UUID de 16 bits 0x2055 no anúncio (filtro do scan).</summary>
    public const string UuidAnuncio = "00002055-0000-1000-8000-00805f9b34fb";
    public const string UuidServico = "00000000-cc7a-482a-984a-7f2ed5b3e58f";
    /// <summary>Característica de escrita de comandos (só write, com resposta).</summary>
    public const string UuidEscrita = "00001122-8e22-4541-9d4c-21edae82ed19";
    /// <summary>Característica das respostas (notify).</summary>
    public const string UuidResposta = "00001123-8e22-4541-9d4c-21edae82ed19";
    /// <summary>Descritor Client Characteristic Configuration (0x2902).</summary>
    public const string UuidCccd = "00002902-0000-1000-8000-00805f9b34fb";
}

/// <summary>Um quadro de resposta quebrado em código e segmentos (MAPA §2).</summary>
public sealed record Quadro(string Codigo, IReadOnlyList<string> Segmentos, string Cru);

/// <summary>Montagem e análise dos quadros de texto do protocolo (MAPA §2, §3).</summary>
public static class Quadros
{
    public const string CodigoSenha = "000";

    /// <summary>
    /// Tira os espaços e converte para maiúsculas. Devolve null se o resultado não tiver
    /// exatamente 4 caracteres [0-9A-Z]. Só letras e números ASCII contam.
    /// </summary>
    public static string? NormalizarSenha(string? digitada)
    {
        if (digitada is null)
            return null;
        var sb = new StringBuilder(4);
        foreach (var c in digitada)
        {
            if (char.IsWhiteSpace(c))
                continue;
            if (!char.IsAsciiLetterOrDigit(c))
                return null;
            sb.Append(char.ToUpperInvariant(c));
        }
        return sb.Length == 4 ? sb.ToString() : null;
    }

    /// <summary>Monta <c>*000*SENHA*</c>. Recusa uma senha fora do formato.</summary>
    public static string MontarSenha(string senha)
    {
        var normalizada = NormalizarSenha(senha);
        if (normalizada is null || normalizada != senha)
            throw new ArgumentException("A senha tem 4 letras ou números.", nameof(senha));
        return $"*{CodigoSenha}*{normalizada}*";
    }

    /// <summary>
    /// Quebra uma resposta como o app da Taramps: troca '*' por '#', tira os separadores das
    /// pontas, divide em segmentos e exige 3 dígitos no primeiro. Devolve null se não for quadro.
    /// </summary>
    public static Quadro? Analisar(string texto)
    {
        var t = texto.Trim().Replace('*', '#').Trim('#');
        var segs = t.Split('#');
        if (segs.Length == 0 || !Regex.IsMatch(segs[0], @"^\d{3}$"))
            return null;
        return new Quadro(segs[0], segs.Skip(1).Select(s => s.Trim()).ToArray(), texto);
    }

    /// <summary>O texto termina num separador ('#' ou '*'), depois de tirar os espaços.</summary>
    public static bool TerminaEmSeparador(string texto)
    {
        var t = texto.TrimEnd();
        return t.Length > 0 && (t[^1] == '#' || t[^1] == '*');
    }
}

/// <summary>
/// Junta os fragmentos das notificações e recorta os quadros por <c>\r</c>/<c>\n</c> ou
/// pelo começo de um novo quadro (<c>#CCC#</c> logo depois de um separador). O último
/// pedaço, sem terminador, fica "aberto": pode ainda receber o resto.
/// Não é thread-safe; quem usa protege com lock.
/// </summary>
public sealed class MontadorQuadros
{
    private static readonly Regex InicioQuadro = new(@"(?<=[#*])(?=[#*]\d{3}[#*])", RegexOptions.CultureInvariant);
    private static readonly char[] FimDeLinha = ['\r', '\n'];

    private string _aberto = "";

    /// <summary>O pedaço sem terminador que ainda pode crescer.</summary>
    public string Aberto => _aberto;

    public void Alimentar(byte[] dados) => Alimentar(Encoding.UTF8.GetString(dados));

    public void Alimentar(string texto) => _aberto += texto;

    /// <summary>
    /// Tira do buffer e devolve os quadros fechados. O pedaço aberto continua em
    /// <see cref="Aberto"/>.
    /// </summary>
    public IReadOnlyList<string> RetirarFechados()
    {
        var fechados = new List<string>();
        var buffer = _aberto;
        var ultimoFim = buffer.LastIndexOfAny(FimDeLinha);
        string cauda;
        if (ultimoFim >= 0)
        {
            foreach (var linha in buffer[..ultimoFim].Split(FimDeLinha))
                fechados.AddRange(Recortar(linha));
            cauda = buffer[(ultimoFim + 1)..];
        }
        else
        {
            cauda = buffer;
        }

        var partes = Recortar(cauda);
        if (partes.Count == 0)
        {
            _aberto = "";
        }
        else
        {
            fechados.AddRange(partes.Take(partes.Count - 1));
            _aberto = partes[^1];
        }
        return fechados;
    }

    /// <summary>Esvazia o pedaço aberto.</summary>
    public void DescartarAberto() => _aberto = "";

    /// <summary>Esvazia tudo e devolve o que havia (fechado ou não) como pedaços.</summary>
    public IReadOnlyList<string> Esvaziar()
    {
        var pedacos = RetirarFechados().ToList();
        if (!string.IsNullOrWhiteSpace(_aberto))
            pedacos.Add(_aberto);
        _aberto = "";
        return pedacos;
    }

    private static List<string> Recortar(string linha) =>
        InicioQuadro.Split(linha).Where(p => !string.IsNullOrWhiteSpace(p)).ToList();
}
