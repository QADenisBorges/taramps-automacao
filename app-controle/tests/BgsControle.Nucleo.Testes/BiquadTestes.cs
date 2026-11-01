using BgsControle.Nucleo.Rendimento;

namespace BgsControle.Nucleo.Testes;

/// <summary>
/// História 13d, os biquads da cadeia no tempo: o módulo do sino RBJ e dos cortes BT e LR (ordens pares) confere com o
/// do <see cref="PiorCaso"/> e do <see cref="ModeloExcursao"/> dentro de 0,1 dB, nas frequências até 1/8 da taxa e onde o
/// filtro ainda passa sinal (acima de −12 dB); o LR ímpar fica com −6 dB no corte e perto do ideal. Um seno passado
/// amostra por amostra sai com o módulo.
/// <para>
/// A bilinear acerta o módulo em f0 e no corte, e entorta o resto perto de Nyquist: um sino acima de ~2 kHz fica mais
/// estreito que o analógico, e um passa-baixa em 1 kHz cai mais rápido (até ~0,4 dB a −24 dB, em 4 a 6 kHz). Por isso
/// os sinos vão até 1 kHz e os passa-baixas até 200 Hz (o do Sub); os passa-altas, até 1 kHz.
/// </para>
/// </summary>
public sealed class BiquadTestes
{
    private static readonly int[] Taxas = [44100, 48000];

    /// <summary>A grade do teste: 20 Hz até 1/8 da taxa, 200 pontos logarítmicos.</summary>
    private static IEnumerable<double> Grade(double taxa) =>
        Enumerable.Range(0, 200).Select(i => 20 * Math.Pow(taxa / 8 / 20, i / 199.0));

    private static double Db(double modulo) => 20 * Math.Log10(modulo);

    [Fact]
    public void O_sino_RBJ_confere_com_o_sino_do_pior_caso()
    {
        foreach (var taxa in Taxas)
        {
            foreach (var f0 in new[] { 25.0, 100, 250, 630, 1000 })
            {
                foreach (var (g, q) in new[] { (6.0, 2.1), (-6.0, 2.1), (3.0, 0.7), (-12.0, 4.0) })
                {
                    var sino = Biquad.Sino(taxa, f0, g, q)!;
                    foreach (var f in Grade(taxa))
                        Assert.InRange(Db(sino.Modulo(f, taxa)) - PiorCaso.Sino(f, f0, g, q), -0.1, 0.1);
                    Assert.Equal(g, Db(sino.Modulo(f0, taxa)), 6);
                }
            }
        }
    }

    [Fact]
    public void O_sino_em_0_dB_e_acima_de_Nyquist_fica_de_fora()
    {
        Assert.Null(Biquad.Sino(48000, 1000, 0, 2.1));
        Assert.Null(Biquad.Sino(48000, 30000, 6, 2.1));
    }

    [Theory]
    [InlineData(FormaFiltro.Bt, 6)]
    [InlineData(FormaFiltro.Bt, 12)]
    [InlineData(FormaFiltro.Bt, 18)]
    [InlineData(FormaFiltro.Bt, 24)]
    [InlineData(FormaFiltro.Bt, 36)]
    [InlineData(FormaFiltro.Bt, 48)]
    [InlineData(FormaFiltro.Lr, 12)]
    [InlineData(FormaFiltro.Lr, 24)]
    [InlineData(FormaFiltro.Lr, 36)]
    [InlineData(FormaFiltro.Lr, 48)]
    public void O_passa_alta_e_o_passa_baixa_conferem_com_o_modulo_do_modelo(FormaFiltro forma, int inclinacao)
    {
        foreach (var taxa in Taxas)
        {
            foreach (var fc in new[] { 40.0, 80, 200, 1000 })
            {
                var alta = new PassaAlta(forma, inclinacao, fc);
                var cascataAlta = Biquad.De(alta, taxa);
                foreach (var f in Grade(taxa).Where(f => Db(alta.Modulo(f)) > -12))
                    Assert.InRange(Biquad.ModuloDb(cascataAlta, f, taxa) - Db(alta.Modulo(f)), -0.1, 0.1);
                Assert.InRange(Biquad.ModuloDb(cascataAlta, fc, taxa) - Db(alta.Modulo(fc)), -0.001, 0.001);
            }
            foreach (var fc in new[] { 40.0, 80, 120, 200 })
            {
                var baixa = new PassaBaixa(forma, inclinacao, fc);
                var cascataBaixa = Biquad.De(baixa, taxa);
                foreach (var f in Grade(taxa).Where(f => Db(baixa.Modulo(f)) > -12))
                    Assert.InRange(Biquad.ModuloDb(cascataBaixa, f, taxa) - Db(baixa.Modulo(f)), -0.1, 0.1);
            }
        }
    }

    [Theory]
    [InlineData(18)]
    [InlineData(30)]
    [InlineData(42)]
    public void O_LR_impar_cai_6_dB_no_corte_e_fica_perto_do_ideal(int inclinacao)
    {
        const double taxa = 48000;
        var alta = new PassaAlta(FormaFiltro.Lr, inclinacao, 100);
        var cascata = Biquad.De(alta, taxa);

        Assert.InRange(Biquad.ModuloDb(cascata, 100, taxa), -6.03, -5.99);
        foreach (var f in Grade(taxa).Where(f => Db(alta.Modulo(f)) > -24))
            Assert.InRange(Biquad.ModuloDb(cascata, f, taxa) - Db(alta.Modulo(f)), -0.5, 0.5);
    }

    [Fact]
    public void Um_seno_passado_por_amostra_sai_com_o_modulo()
    {
        const int taxa = 48000;
        var cascata = Biquad.De(new PassaAlta(FormaFiltro.Lr, 24, 200), taxa).Append(Biquad.Sino(taxa, 1000, 6, 2.1)!).ToArray();
        var pico = 0.0;
        for (var i = 0; i < taxa; i++)
        {
            var y = cascata.Aggregate(Math.Sin(2 * Math.PI * 300 * i / taxa), (x, b) => b.Processar(x));
            if (i > taxa / 2)
                pico = Math.Max(pico, Math.Abs(y));
        }

        Assert.InRange(Db(pico) - Biquad.ModuloDb(cascata, 300, taxa), -0.05, 0.05);
    }
}
