using Agente.ModeloFalso;

namespace Agente.Servico.Programa;

/// <summary>
/// O lançador do <c>lancador.tipo</c> da configuração: só "claude" e "modelo-falso". Qualquer outro tipo é recusado, para
/// um erro de digitação nunca rodar o Claude (que custa) sem querer.
/// </summary>
public static class FabricaLancador
{
    public static ILancadorAgente Criar(ConfigServico config, string pastaAgente, string pastaLocal) => config.Lancador.Tipo switch
    {
        ConfigLancador.TipoClaude => new LancadorClaude(config.Lancador, pastaAgente, pastaLocal),
        ConfigLancador.TipoModeloFalso => new LancadorModeloFalso(
            config.Lancador.Roteiros.ToDictionary(r => r.Key, r => Path.GetFullPath(Path.Combine(pastaAgente, r.Value))), pastaLocal),
        var outro => throw new ConfigInvalidaException(
            $"O \"lancador.tipo\" é \"{ConfigLancador.TipoClaude}\" ou \"{ConfigLancador.TipoModeloFalso}\" (veio \"{outro}\")."),
    };
}
