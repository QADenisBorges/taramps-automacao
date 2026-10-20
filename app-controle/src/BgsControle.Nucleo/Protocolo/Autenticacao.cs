namespace BgsControle.Nucleo.Protocolo;

/// <summary>Resultado da leitura de uma resposta <c>000</c> (MAPA §3, §9).</summary>
public abstract record ResultadoAutenticacao
{
    private ResultadoAutenticacao() { }

    /// <summary>
    /// Senha aceita. <paramref name="Firmware"/> = os 4 últimos caracteres do payload sem
    /// espaços. <paramref name="ProdutoConhecido"/> = o payload começa com <c>PRO2.4MI</c>.
    /// </summary>
    public sealed record Aceita(string Firmware, bool ProdutoConhecido, string Payload) : ResultadoAutenticacao
    {
        /// <summary>Firmware diferente do R003 ou produto desconhecido: modo só leitura.</summary>
        public bool SoLeitura => !ProdutoConhecido || Firmware != Autenticacao.FirmwareSuportado;
    }

    /// <summary>Payload <c>ERRO</c>: senha errada.</summary>
    public sealed record Recusada : ResultadoAutenticacao;

    /// <summary>Payload vazio ou começo de <c>ERRO</c>: o resto ainda pode chegar.</summary>
    public sealed record Incompleta : ResultadoAutenticacao;
}

public static class Autenticacao
{
    /// <summary>O único firmware em que o protocolo foi lido e testado.</summary>
    public const string FirmwareSuportado = "R003";

    /// <summary>Começo do payload da resposta 000 do PRO 2.4BT + AUDIO (<c>#000#PRO2.4MIR003#</c>).</summary>
    public const string PrefixoProduto = "PRO2.4MI";

    private const string Erro = "ERRO";

    /// <summary>
    /// Lê uma resposta à senha. O quadro precisa ter a função <c>000</c>; qualquer outro
    /// código lança, porque a resposta é casada só pela função.
    /// </summary>
    public static ResultadoAutenticacao Interpretar(Quadro quadro)
    {
        if (quadro.Codigo != Quadros.CodigoSenha)
            throw new ArgumentException("A resposta da senha tem a função 000.", nameof(quadro));

        var payload = string.Join("#", quadro.Segmentos).Trim();
        var semEspacos = string.Concat(payload.Where(c => !char.IsWhiteSpace(c)));

        if (semEspacos.Equals(Erro, StringComparison.OrdinalIgnoreCase))
            return new ResultadoAutenticacao.Recusada();
        if (semEspacos.Length == 0 || Erro.StartsWith(semEspacos, StringComparison.OrdinalIgnoreCase))
            return new ResultadoAutenticacao.Incompleta();

        var firmware = semEspacos.Length <= 4 ? semEspacos : semEspacos[^4..];
        var conhecido = semEspacos.StartsWith(PrefixoProduto, StringComparison.Ordinal);
        return new ResultadoAutenticacao.Aceita(firmware, conhecido, payload);
    }
}
