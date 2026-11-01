using System.Text.Json.Nodes;

namespace Agente.Porta;

/// <summary>
/// A ferramenta da porta (história 14a): o ponto de troca entre o Agente (o Claude Code ou o modelo falso) e o app.
/// Recebe a entrada em JSON (<see cref="PedidoFerramenta"/>) e devolve a saída em JSON (<see cref="RespostaFerramenta"/>).
/// Todo pedido usa o endereço e a chave guardados em <c>.local/porta.json</c>; o <c>parear</c> é o que os guarda. A
/// chave nunca aparece na saída.
/// </summary>
public static class FerramentaPorta
{
    public const string MsgSemPareamento = "Sem pareamento: rode porta parear.";

    /// <summary>Roda uma entrada da ferramenta. Nunca lança (fora o cancelamento): o erro sai como <c>ok:false</c>.</summary>
    public static async Task<RespostaFerramenta> ExecutarAsync(string? entrada, string pastaLocal, ClientePorta? cliente = null, CancellationToken cancelar = default)
    {
        PedidoFerramenta pedido;
        try
        {
            pedido = PedidoFerramenta.DeJson(entrada);
        }
        catch (EntradaInvalidaException e)
        {
            return RespostaFerramenta.Recusa(e.Message);
        }

        var proprio = cliente is null;
        cliente ??= new ClientePorta();
        try
        {
            if (pedido.Acao == PedidoFerramenta.Parear)
                return await ParearAsync(pedido, pastaLocal, cliente, cancelar);
            return await PedirAsync(pastaLocal, pedido.Metodo, pedido.Alvo!, pedido.Corpo, cliente, cancelar);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return RespostaFerramenta.Recusa($"Não consegui usar a pasta {pastaLocal} ({e.GetType().Name}).");
        }
        finally
        {
            if (proprio)
                cliente.Dispose();
        }
    }

    /// <summary>Um pedido com o pareamento guardado (o mesmo caminho do <c>ler</c> e do <c>pedir</c>; o serviço também usa).</summary>
    public static Task<RespostaFerramenta> PedirAsync(string pastaLocal, string metodo, string alvo, JsonNode? corpo, ClientePorta cliente,
        CancellationToken cancelar = default)
    {
        if (PareamentoLocal.Ler(pastaLocal) is not { } pareado)
            return Task.FromResult(RespostaFerramenta.Recusa(MsgSemPareamento));
        if (!PedidoFerramenta.AlvoValido(alvo))
            return Task.FromResult(RespostaFerramenta.Recusa("O \"alvo\" é um caminho da porta, como /estado."));
        return cliente.PedirAsync(pareado, metodo, alvo, corpo, cancelar);
    }

    private static async Task<RespostaFerramenta> ParearAsync(PedidoFerramenta pedido, string pastaLocal, ClientePorta cliente, CancellationToken cancelar)
    {
        var (resposta, chave) = await cliente.ParearAsync(pedido.Endereco!, pedido.Codigo!, pedido.Nome!, cancelar);
        if (!resposta.Ok || chave is null)
            return resposta;
        PareamentoLocal.Gravar(pastaLocal, new ClientePareado(pedido.Endereco!, chave));
        return RespostaFerramenta.Feito(resposta.Status ?? 200, null);
    }
}
