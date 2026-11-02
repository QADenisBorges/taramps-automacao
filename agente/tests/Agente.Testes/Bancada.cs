using Agente.Avaliacao;
using BgsControle.Nucleo.Porta;
using BgsControle.Nucleo.Setups;
using BgsControle.Simulador;

namespace Agente.Testes;

/// <summary>
/// O núcleo do app sobre o <see cref="ProcessadorSimulado"/> (o <see cref="NucleoSimulado"/> do avaliador, história 14f),
/// com o cenário de teste do app (um Setup com uma memória, só como dado de teste): conectado, lido, com a chave
/// "Controle pela rede" ligada e a <see cref="PortaComandos"/> com a <see cref="CriacaoMemoria"/> numa porta livre de
/// localhost. O lado do usuário vai direto no <see cref="Criacao"/>.
/// </summary>
internal sealed class Bancada : IAsyncDisposable
{
    private readonly NucleoSimulado _nucleo = new(CenarioFilme.SetupsJson, CenarioFilme.PresetsJson, CenarioFilme.ModelosJson);

    public ProcessadorSimulado Simulador => _nucleo.Simulador;

    public CriacaoMemoria Criacao => _nucleo.Criacao;

    /// <summary>Os Setups do cenário (o lado do usuário salva um equipamento por aqui).</summary>
    public GerenteSetups Setups => _nucleo.Setups;

    /// <summary>A base de modelos do celular (só o toque do usuário grava nela).</summary>
    public BaseModelos Modelos => _nucleo.Modelos;

    /// <summary>A proposta de ficha: a folha do celular (<see cref="PropostaFicha.Atual"/>).</summary>
    public PropostaFicha Ficha => _nucleo.Ficha;

    public Pareamento Pareamento => _nucleo.Pareamento;

    public PortaComandos Porta => _nucleo.Porta;

    /// <summary>O endereço que a ferramenta usa para parear.</summary>
    public string Endereco => _nucleo.Endereco;

    /// <summary>Conectada, lida, com o Setup conferido e a porta aberta.</summary>
    public static async Task<Bancada> ProntaAsync()
    {
        var b = new Bancada();
        await b._nucleo.ConectarAsync();
        await b._nucleo.LigarPortaAsync();
        return b;
    }

    /// <summary>A porta cai (o servidor fecha sem revogar o cliente).</summary>
    public void DerrubarPorta() => _nucleo.DerrubarPorta();

    /// <summary>
    /// A porta volta (o app reaberto: a contagem dos eventos recomeça), no mesmo número, com o mesmo cliente pareado (o
    /// guardado no celular). <paramref name="antesDeAbrir"/> roda com a porta nova já ouvindo a criação, antes de ela abrir.
    /// </summary>
    public async Task ReabrirPortaAsync(Action? antesDeAbrir = null)
    {
        await _nucleo.ReabrirPortaAsync(antesDeAbrir);
        Assert.True(Pareamento.Pareado);
    }

    /// <summary>Nenhum 240, 241 ou 23N chegou ao processador.</summary>
    public void NadaDeMemoriaDoAparelho() =>
        Assert.DoesNotContain(Simulador.Escritas, e => e.StartsWith("*24", StringComparison.Ordinal) || e.StartsWith("*23", StringComparison.Ordinal));

    public ValueTask DisposeAsync() => _nucleo.DisposeAsync();
}

internal static class Esperar
{
    /// <summary>Espera, no tempo real, a condição ficar verdadeira.</summary>
    public static async Task Ate(Func<bool> condicao, TimeSpan prazo, string? oQue = null)
    {
        var limite = DateTime.UtcNow + prazo;
        while (!condicao())
        {
            if (DateTime.UtcNow > limite)
                Assert.Fail($"A condição não ficou verdadeira em {prazo.TotalSeconds} s{(oQue is null ? "" : $": {oQue}")}.");
            await Task.Delay(50);
        }
    }
}
