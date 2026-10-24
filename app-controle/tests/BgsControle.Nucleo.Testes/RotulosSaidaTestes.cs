using BgsControle.Nucleo.Setups;

namespace BgsControle.Nucleo.Testes;

/// <summary>
/// História 10: os rótulos da folha do Salvar e da lista "O que não bate" pelo nome da saída do Setup
/// (<see cref="RotulosSaida"/>) e os nomes da tela sem repetir (<see cref="Ligacoes.NomesNaTela"/>).
/// </summary>
public sealed class RotulosSaidaTestes
{
    private static SaidaApp Saida(string nome, params int[] canais) => new(canais[0], canais, nome, null);

    private static readonly IReadOnlyList<SaidaApp> Casa = [Saida("JBL L", 0), Saida("JBL R", 1), Saida("Sub", 2, 3)];

    [Fact]
    public void O_par_que_muda_igual_e_uma_linha_so_com_o_nome_da_saida_e_a_cor_do_mestre()
    {
        var linhas = RotulosSaida.Juntar(
            [DiferencaPorCanal.De("050", 2, "80 cm", "85 cm"), DiferencaPorCanal.De("050", 3, "80 cm", "85 cm")], Casa);

        var l = Assert.Single(linhas);
        Assert.Equal("Atraso do Sub", l.Rotulo);
        Assert.Equal(2, l.Canal);
    }

    [Fact]
    public void Os_dois_canais_do_par_diferentes_viram_a_linha_do_mestre_e_a_do_outro_com_o_canal()
    {
        var linhas = RotulosSaida.Juntar(
            [DiferencaPorCanal.De("050", 2, "80 cm", "85 cm"), DiferencaPorCanal.De("050", 3, "80 cm", "90 cm")], Casa);

        Assert.Equal(["Atraso do Sub: 80 cm → 85 cm", "Atraso do Sub · CH4: 80 cm → 90 cm"],
            linhas.Select(l => $"{l.Rotulo}: {l.Antes} → {l.Depois}"));
        Assert.All(linhas, l => Assert.Equal(2, l.Canal));
    }

    [Fact]
    public void So_o_mestre_diferente_e_a_linha_da_saida_e_so_o_outro_leva_o_canal()
    {
        Assert.Equal("Atraso do Sub", Assert.Single(RotulosSaida.Juntar([DiferencaPorCanal.De("050", 2, "80 cm", "0 cm")], Casa)).Rotulo);
        Assert.Equal("Atraso do Sub · CH4", Assert.Single(RotulosSaida.Juntar([DiferencaPorCanal.De("050", 3, "80 cm", "0 cm")], Casa)).Rotulo);
    }

    [Fact]
    public void Globais_e_bandas_ficam_como_estao_sem_cor()
    {
        var linhas = RotulosSaida.Juntar(
            [DiferencaPorCanal.De("080", null, "0,0 dB", "−3,0 dB"), DiferencaPorCanal.De("030", null, "0,0 dB", "+1,0 dB"),
             DiferencaPorCanal.De("020", 2, "0,0 dB", "+1,0 dB")], Casa);

        Assert.Equal(["MASTER", "Ganho do EQ de entrada", $"Banda {Catalogo.RotuloBanda(2)}"], linhas.Select(l => l.Rotulo));
        Assert.All(linhas, l => Assert.Null(l.Canal));
    }

    [Fact]
    public void O_equilibrio_usa_o_sufixo_depois_do_nome()
    {
        var linhas = RotulosSaida.Juntar(
            [new DiferencaPorCanal("eq", 2, 2, "Equilíbrio", "0,0 dB", "−2,0 dB", " · ganho"),
             new DiferencaPorCanal("eq", 3, 3, "Equilíbrio", "0,0 dB", "−2,0 dB", " · ganho")], Casa);

        Assert.Equal("Equilíbrio do Sub · ganho", Assert.Single(linhas).Rotulo);
    }

    [Fact]
    public void Sem_link_cada_canal_e_uma_saida_e_os_nomes_repetidos_levam_o_canal()
    {
        IReadOnlyList<SaidaApp> semLink = [Saida("JBL L", 0), Saida("JBL R", 1), Saida("Sub", 2), Saida("Sub", 3)];

        Assert.Equal(["JBL L", "JBL R", "Sub · CH3", "Sub · CH4"], Ligacoes.NomesNaTela(semLink));
        var linhas = RotulosSaida.Juntar(
            [DiferencaPorCanal.De("050", 2, "80 cm", "85 cm"), DiferencaPorCanal.De("050", 3, "80 cm", "85 cm")], semLink);
        Assert.Equal(["Atraso do Sub · CH3", "Atraso do Sub · CH4"], linhas.Select(l => l.Rotulo));
        Assert.Equal([2, 3], linhas.Select(l => l.Canal!.Value));
    }

    [Fact]
    public void Sem_equipamentos_as_saidas_sao_os_canais()
    {
        var saidas = Ligacoes.Saidas([], link12: false, link34: true);

        Assert.Equal(["CH1", "CH2", "CH3 + CH4"], Ligacoes.NomesNaTela(saidas));
        Assert.Equal("Atraso do CH3 + CH4",
            Assert.Single(RotulosSaida.Juntar([DiferencaPorCanal.De("050", 2, "80 cm", "85 cm")], saidas)).Rotulo);
    }
}
