using System.Globalization;
using Agente.Porta;
using Agente.Servico;

namespace Agente.Avaliacao;

/// <summary>
/// Os comandos do avaliador (<c>dotnet run --project agente/avaliacao/Agente.Avaliacao -- …</c>):
/// <list type="bullet">
/// <item><c>real</c>: mostra o custo estimado e só roda com <c>--confirmo-custo</c> (sem ele, sai com 3 sem lançar nada).
/// Com a confirmação, roda o Agente de verdade (<see cref="LancadorClaude"/>) contra os perfis, um por vez, e grava o
/// relatório em <c>avaliacao/relatorios/&lt;data&gt;/</c>. Sai com 0 se o conjunto passou, 1 se não.</item>
/// <item><c>conferir</c>: sai com 0 quando o último relatório real que passou tem o hash das camadas de agora; senão, 1
/// com o motivo.</item>
/// </list>
/// </summary>
public static class Comandos
{
    /// <summary>O custo estimado de um perfil na avaliação real (SPEC, Assumptions: ~US$ 1,50 por memória).</summary>
    public const decimal CustoPorPerfil = 1.50m;

    public const string OpcaoConfirmo = "--confirmo-custo";

    public const int SaiuSemConfirmar = 3;
    public const int UsoErrado = 2;

    public const string Uso = """
        Uso:
          avaliacao real [--confirmo-custo]   roda o Agente de verdade contra os perfis (mostra o custo antes)
          avaliacao conferir                  confere se as camadas são as da última avaliação real que passou
        """;

    /// <summary>"US$ 12,00".</summary>
    public static string Dolares(decimal valor) => "US$ " + valor.ToString("0.00", CultureInfo.GetCultureInfo("pt-BR"));

    /// <param name="fabrica">
    /// O lançador do <c>real</c>, pela pasta local de cada perfil (null: o <see cref="LancadorClaude"/> da configuração do
    /// serviço). Os testes passam um que só conta.
    /// </param>
    public static async Task<int> RodarAsync(string[] args, TextWriter saida, string? pastaAgente = null,
        Func<string, ILancadorAgente>? fabrica = null, CancellationToken cancelar = default)
    {
        pastaAgente ??= PastaLocal.AcharRaiz(AppContext.BaseDirectory) ?? PastaLocal.AcharRaiz(Directory.GetCurrentDirectory());
        if (pastaAgente is null)
        {
            await saida.WriteLineAsync("Não achei a pasta agente/ (a do servico.config.json).");
            return UsoErrado;
        }
        switch (args.FirstOrDefault())
        {
            case "conferir" when args.Length == 1:
            {
                var (codigo, mensagem) = Conferencia.Conferir(pastaAgente, Conferencia.PastaRelatorios(pastaAgente));
                await saida.WriteLineAsync(mensagem);
                return codigo;
            }
            case "real" when args.Skip(1).All(a => a == OpcaoConfirmo):
                return await RealAsync(args.Contains(OpcaoConfirmo), saida, pastaAgente, fabrica, cancelar);
            default:
                await saida.WriteLineAsync(Uso);
                return UsoErrado;
        }
    }

    private static async Task<int> RealAsync(bool confirmou, TextWriter saida, string pastaAgente, Func<string, ILancadorAgente>? fabrica,
        CancellationToken cancelar)
    {
        var perfis = Perfis.Carregar(Perfis.Pasta(pastaAgente));
        await saida.WriteLineAsync(
            $"Custo estimado: {Dolares(CustoPorPerfil)} por perfil, {Dolares(CustoPorPerfil * perfis.Count)} os {perfis.Count} perfis.");
        if (!confirmou)
        {
            await saida.WriteLineAsync($"Nada foi lançado. Para rodar, repita com {OpcaoConfirmo}.");
            return SaiuSemConfirmar;
        }

        var config = ConfigServico.Carregar(Path.Combine(pastaAgente, ConfigServico.Arquivo));
        if (fabrica is null)
        {
            // A ferramenta porta fica na pasta local de quem usa (o README manda publicá-la lá); o pareamento de cada perfil, na
            // pasta temporária dele.
            var pastaPorta = string.IsNullOrWhiteSpace(config.Lancador.PastaPorta)
                ? null
                : Path.GetFullPath(Path.Combine(PastaLocal.Resolver(), config.Lancador.PastaPorta));
            var lancador = config.Lancador with { PastaPorta = pastaPorta };
            fabrica = local => new LancadorClaude(lancador, pastaAgente, local);
        }
        var opcoes = new OpcoesAvaliacao(config.PrazoAgente, TimeSpan.FromMinutes(2), Paralelo: false);
        var relatorio = await Avaliador.RodarAsync(perfis, fabrica, opcoes, Relatorio.TipoReal, HashCamadas.Calcular(pastaAgente), saida, cancelar);
        var pasta = Path.Combine(Conferencia.PastaRelatorios(pastaAgente), relatorio.Data.ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture));
        relatorio.Gravar(pasta);
        await saida.WriteLineAsync($"{(relatorio.Passou ? "Passou" : "Não passou")}. Relatório em {pasta}.");
        return relatorio.Passou ? 0 : 1;
    }
}
