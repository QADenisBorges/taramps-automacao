using BgsControle.Nucleo.Escrita;
using BgsControle.Nucleo.Presets;
using BgsControle.Nucleo.Setups;

namespace BgsControle.Simulador;

/// <summary>
/// O cenário de teste que substitui a FILME e o Cinema herdados de antes de 29/10/2026 (história 8f): o
/// app nasce zerado (história 8g), e os testes que precisam do Casa 2.1 com uma memória guardam o Casa
/// da fixture <see cref="CasaDoDenis"/> e esta memória, criada da leitura de 18/10 com que o simulador nasce. É o mesmo que conectar, escolher o Casa
/// 2.1 e "Criar memória do som atual" com o nome FILME (há um teste para isso), e depois guardar as 15
/// bandas lidas como o preset "Cinema". Só vive nos testes.
/// </summary>
public static class CenarioFilme
{
    public const string IdMemoria = "teste-memoria-filme";
    public const string NomeMemoria = "FILME";
    public const string IdPreset = "teste-preset-cinema";
    public const string NomePreset = "Cinema";

    /// <summary>A regulagem da leitura de 18/10 (os valores finais, como o simulador nasce).</summary>
    public static IReadOnlyList<ValorCopiado> Regulagem { get; } = Da(Camadas.Consultas(Camada.Regulagem).Select(c => (c.Parametro.Codigo, c.Indice)));

    /// <summary>As 15 bandas da leitura de 18/10.</summary>
    public static IReadOnlyList<ValorCopiado> Bandas { get; } = Da(GerentePresets.ConsultasBandas.Select(c => (c.Parametro.Codigo, c.Indice)));

    /// <summary>A memória FILME no Casa 2.1, com a regulagem de 18/10, lembrando o Cinema.</summary>
    public static MemoriaCelular Memoria { get; } =
        new(IdMemoria, CasaDoDenis.IdSetupCasa, NomeMemoria, CasaDoDenis.QuandoFilme, Regulagem, IdPreset);

    /// <summary>O preset "Cinema": as 15 bandas de 18/10.</summary>
    public static Preset Preset { get; } = new(IdPreset, NomePreset, CasaDoDenis.QuandoFilme, Bandas);

    /// <summary>O Casa 2.1 da fixture com a FILME, ativa e no primeiro botão.</summary>
    public static DadosSetups Dados => CasaDoDenis.Dados with
    {
        Memorias = [Memoria],
        Uso = new Dictionary<string, UsoDoSetup>
        {
            [CasaDoDenis.IdSetupCasa] = new([IdMemoria], [IdMemoria, null, null]),
        },
    };

    /// <summary>O <c>setups.json</c> (no formato atual) do cenário.</summary>
    public static string SetupsJson => GerenteSetups.ParaJson(Dados);

    /// <summary>O <c>presets.json</c> (formato 3) do cenário: só o Cinema.</summary>
    public static string PresetsJson => GerentePresets.ParaJson([Preset]);

    /// <summary>O <c>modelos.json</c> do cenário: os 3 modelos da fixture.</summary>
    public static string ModelosJson => CasaDoDenis.ModelosJson;

    private static IReadOnlyList<ValorCopiado> Da(IEnumerable<(string Codigo, int? Indice)> enderecos)
    {
        var quero = enderecos.ToHashSet();
        return CasaDoDenis.LeituraFilme.Where(v => quero.Contains((v.Codigo, v.Indice))).ToArray();
    }
}
