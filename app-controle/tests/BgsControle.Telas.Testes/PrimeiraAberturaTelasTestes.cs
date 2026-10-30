using BgsControle.Nucleo;
using BgsControle.Nucleo.Herdado;

namespace BgsControle.Telas.Testes;

/// <summary>
/// História 10c: a primeira abertura do modelo de 29/10/2026 nas telas, contra o simulador. O app nasce
/// com o Casa 2.1 sem memórias e sem presets: conectar cai na escolha do Setup, Escolher só desmuta, e o
/// Início oferece "Criar memória do som atual".
/// </summary>
public sealed class PrimeiraAberturaTelasTestes : IDisposable
{
    private readonly Bancada _b = new(usoCompartilhado: true, setups: true, filme: false);
    private readonly Telas11 _t;

    public PrimeiraAberturaTelasTestes() => _t = new Telas11(_b);

    public void Dispose()
    {
        _t.Dispose();
        Assert.Empty(_b.Tela.Erros);
    }

    private bool Mutados(bool mutado) =>
        Enumerable.Range(0, 4).All(c => _b.Simulador.Valor("100", c) == (mutado ? "1" : "0"));

    [Fact]
    public async Task Conectar_sem_memoria_cai_na_escolha_com_a_frase_sem_memoria_e_sem_lista()
    {
        await _b.Conectar();

        Assert.True(_t.Setup.Aberta);
        Assert.Equal(SetupViewModel.TituloDuvida, _t.Setup.Titulo);
        Assert.Equal("O Casa 2.1 ainda não tem memória.", _t.Setup.Frase);
        Assert.False(_t.Setup.TemDiferencas);
        Assert.Empty(_t.Setup.LinhasDiferenca);
        Assert.Equal(SetupViewModel.TextoMudo, _t.Setup.FaixaMudo);
        Assert.True(Mutados(true));
    }

    [Fact]
    public async Task Escolher_o_Casa_2_1_so_desmuta_e_o_Inicio_oferece_criar_a_memoria_e_Presets_mostra_o_vazio()
    {
        await _b.Conectar();
        Assert.Equal("Aplica a memória ativa dele, relê e desmuta. Sem memória, só desmuta.", SetupViewModel.FraseEscolher);
        var antes = _b.Simulador.Escritas.Count;

        await _t.Setup.AbrirLista.ExecutarAsync();
        await _b.Rodar(_t.Setup.Item(ConhecimentoHerdado.NomeSetupCasa)!.Escolher.ExecutarAsync());

        Assert.False(_t.Setup.Aberta);
        Assert.True(Mutados(false));
        // Só o desmutar dos 4 canais: nenhuma outra escrita de áudio.
        var audio = _b.Simulador.Escritas.Skip(antes).Where(e => Catalogo.EscritaDeAudio(e) is not null).ToArray();
        Assert.Equal(4, audio.Count(e => e.StartsWith("*100*", StringComparison.Ordinal)));
        Assert.Equal(0, audio.Count(e => !e.StartsWith("*100*", StringComparison.Ordinal)));

        Assert.True(_t.Inicio.MostrarMemoria);
        Assert.Equal("Nenhuma memória", _t.Inicio.NomeMemoria);
        Assert.True(_t.Inicio.MostrarCriarMemoria);

        // A lista "…": sem memórias do celular, vazia (as memórias do processador não aparecem).
        Assert.Empty(_t.Botoes.LinhasCelular);
        Assert.Equal("Criar memória do som atual", InicioViewModel.TextoCriarMemoria);
        Assert.Null(_t.Inicio.NomePreset); // sem chip

        Assert.Empty(_t.Presets.Itens);
        Assert.Equal(PresetsViewModel.TextoSemProprios, _t.Presets.TextoVazio);
    }
}
