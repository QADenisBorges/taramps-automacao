using System.Diagnostics;
using Agente.Porta;
using Agente.Servico;

namespace Agente.ModeloFalso;

/// <summary>
/// O lançador do modelo falso: em vez do Claude Code, um processo (este executável) roda o roteiro gravado da tarefa,
/// chamando a mesma ferramenta <c>porta</c>. O serviço o trata igual ao de verdade (prazo, fim, código de saída).
/// </summary>
/// <param name="roteiros">A tarefa → o caminho do roteiro.</param>
/// <param name="pastaLocal">A <c>.local</c> com o pareamento (vai aos processos pela <see cref="PastaLocal.Variavel"/>).</param>
public sealed class LancadorModeloFalso(IReadOnlyDictionary<string, string> roteiros, string pastaLocal)
    : LancadorProcesso(Path.Combine(pastaLocal, "agente.log"))
{
    /// <summary>Este executável (o <c>Agente.ModeloFalso.dll</c>).</summary>
    public static string Dll => typeof(LancadorModeloFalso).Assembly.Location;

    protected override ProcessStartInfo Montar(string tarefa)
    {
        if (!roteiros.TryGetValue(tarefa, out var roteiro))
            throw new InvalidOperationException($"O modelo falso não tem roteiro para a tarefa {tarefa}.");
        var inicio = Executavel.De(Dll);
        inicio.ArgumentList.Add("--roteiro");
        inicio.ArgumentList.Add(Path.GetFullPath(roteiro));
        inicio.ArgumentList.Add("--porta");
        inicio.ArgumentList.Add(FerramentaProcesso.PortaAoLado);
        inicio.Environment[PastaLocal.Variavel] = pastaLocal;
        return inicio;
    }
}
