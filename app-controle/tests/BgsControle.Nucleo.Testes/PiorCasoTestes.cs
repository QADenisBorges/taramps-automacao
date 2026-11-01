using BgsControle.Nucleo.Escrita;
using BgsControle.Nucleo.Rendimento;
using BgsControle.Nucleo.Setups;
using BgsControle.Simulador;

namespace BgsControle.Nucleo.Testes;

/// <summary>
/// História 13c, a conta pura do pior caso e do master do nível: o Casa 2.1 da fixture calibrado (ganho ×19,5 por
/// canal, ×40,5 na ponte), o Bluetooth em −13 dB e uma memória plana com o ganho +9 nas JBL e o passa-alta LR24 em
/// 200 Hz. Os números da JBL (17,9 V térmico, 22,2 V curso do cone, 23 V ceifamento) dentro de 0,2 dB.
/// </summary>
public sealed class PiorCasoTestes
{
    /// <summary>O térmico da JBL 4TRMS80 em 4 Ω (limites-e-niveis.md).</summary>
    private const double TermicoJbl = 17.9;

    /// <summary>O curso do cone da JBL com o LR24 em 200 Hz (limites-e-niveis.md).</summary>
    private const double ExcursaoJbl = 22.2;

    /// <summary>O ceifamento do canal do SD800.4 EVO.</summary>
    private const double CeifamentoJbl = 23.0;

    private const decimal Bluetooth = -13m;
    private const decimal GanhoModulo = 19.5m;

    private static IReadOnlyList<Equipamento> Equipamentos => CasaDoDenis.SetupCasaComBluetooth.Equipamentos;

    private static IReadOnlyList<SaidaApp> Saidas => Ligacoes.Saidas(Equipamentos);

    /// <summary>
    /// A memória plana sobre a leitura de 18/10: o ganho +9 nas JBL e −15 no Sub (que fica longe do teto), o EQ de
    /// entrada, o de saída e as 15 bandas em 0, as JBL com LR24 em 200 Hz e o master em 0.
    /// </summary>
    internal static IReadOnlyList<ValorCopiado> Plana(decimal ganhoJbl = 9m) =>
        CasaDoDenis.LeituraFilme.Select(v => (v.Codigo, v.Indice) switch
        {
            ("081", 0 or 1) => v with { NoFio = Db(ganhoJbl) },
            ("081", _) => v with { NoFio = "-15.0" },
            ("091", _) or ("030", _) or ("020", _) or ("080", _) => v with { NoFio = "+0.0" },
            ("042", 0 or 1) => v with { NoFio = "3" },
            ("040", 0 or 1) => v with { NoFio = "200" },
            _ => v,
        }).ToArray();

    private static string Db(decimal v) => (v >= 0 ? "+" : "-") + Math.Abs(v).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Um leitor dos valores, com mudanças por cima.</summary>
    private static Func<Endereco, Valor?> Leitor(IEnumerable<ValorCopiado> valores, params (string Codigo, int? Indice, string NoFio)[] mudancas)
    {
        var d = new Dictionary<Endereco, Valor>();
        foreach (var v in valores)
            d[new Endereco(v.Codigo, v.Indice)] = Catalogo.ParametroDe(v.Codigo)!.Decodificar(v.NoFio).Valor!;
        foreach (var (codigo, indice, noFio) in mudancas)
            d[new Endereco(codigo, indice)] = Catalogo.ParametroDe(codigo)!.Decodificar(noFio).Valor!;
        return e => d.GetValueOrDefault(e);
    }

    /// <summary>O pior caso da JBL L (canal 0) com o master da conta, em dB relativo a <paramref name="volts"/> − margem.</summary>
    private static double ErroDaJbl(Func<Endereco, Valor?> ler, decimal master, double volts, double margem)
    {
        var pico = PiorCaso.Pico(ler, 0, Bluetooth).Db!.Value;
        var v = PiorCaso.Volts(pico + (double)master, GanhoModulo);
        return 20 * Math.Log10(v / (volts * Math.Pow(10, -margem / 20)));
    }

    [Fact]
    public void O_sino_da_o_ganho_no_centro_e_nada_longe_dele()
    {
        Assert.Equal(3.0, PiorCaso.Sino(1000, 1000, 3, 0.7), 6);
        Assert.Equal(-6.0, PiorCaso.Sino(250, 250, -6, 2.1), 6);
        Assert.InRange(PiorCaso.Sino(20000, 250, 4, 2.1), -0.01, 0.01);
        Assert.Equal(0, PiorCaso.Sino(500, 250, 0, 2.1));
    }

    [Fact]
    public void O_pico_da_JBL_na_memoria_plana_e_o_Bluetooth_mais_o_ganho()
    {
        var pico = PiorCaso.Pico(Leitor(Plana()), 0, Bluetooth);

        Assert.Null(pico.Falta);
        Assert.Equal(-4.0, pico.Db!.Value, 2);
        Assert.True(pico.Frequencia > 2000);
    }

    [Fact]
    public void A_rota_a_mais_b_soma_6_dB()
    {
        var a = PiorCaso.Pico(Leitor(Plana()), 1, Bluetooth).Db!.Value;
        var ab = PiorCaso.Pico(Leitor(Plana(), ("010", 1, "a+b")), 1, Bluetooth).Db!.Value;

        Assert.Equal(6.0, ab - a, 6);
    }

    [Fact]
    public void Um_valor_da_cadeia_que_falta_deixa_a_conta_sem_master_com_o_motivo()
    {
        var valores = Plana().Where(v => !(v.Codigo == "081" && v.Indice == 0)).ToArray();

        var conta = PiorCaso.Conta(Equipamentos, Saidas, Leitor(valores), Bluetooth, NivelRendimento.Alto);

        Assert.Null(conta.Master);
        Assert.Equal("O app não sabe o valor de Ganho da saída 1.", conta.Falta);
    }

    [Theory]
    [InlineData(NivelRendimento.Baixo, 6.0)]
    [InlineData(NivelRendimento.Medio, 3.0)]
    [InlineData(NivelRendimento.Alto, 1.0)]
    public void JBL_do_Baixo_ao_Alto_fica_no_termico_menos_a_margem(NivelRendimento nivel, double margem)
    {
        var ler = Leitor(Plana());

        var conta = PiorCaso.Conta(Equipamentos, Saidas, ler, Bluetooth, nivel);

        Assert.Null(conta.Falta);
        Assert.Null(conta.FaltaGanho);
        var master = conta.Master!.Value;
        // O teto da JBL é −6,8 dB (o térmico), e o pico sem o master é −4 dB: −6,8 − margem + 4.
        Assert.Equal(-6.8m - (decimal)margem + 4m, master);
        Assert.InRange(ErroDaJbl(ler, master, TermicoJbl, margem), -0.2, 0.2);
        Assert.Equal(["JBL L", "JBL R"], conta.SaidasQueMandam);
        Assert.Equal(TipoLimite.Termico, conta.QueManda!.Manda);
        Assert.InRange(conta.TetoNivelV!.Value, TermicoJbl * Math.Pow(10, -(margem + 0.2) / 20), TermicoJbl * Math.Pow(10, -(margem - 0.2) / 20));
    }

    [Fact]
    public void JBL_no_Maximo_fica_no_menor_de_curso_e_ceifamento_menos_1_dB()
    {
        var ler = Leitor(Plana());

        var conta = PiorCaso.Conta(Equipamentos, Saidas, ler, Bluetooth, NivelRendimento.Maximo);

        var master = conta.Master!.Value;
        Assert.Equal(TipoLimite.Excursao, conta.QueManda!.Manda);
        Assert.InRange(ErroDaJbl(ler, master, Math.Min(ExcursaoJbl, CeifamentoJbl), 1.0), -0.2, 0.2);
        // Passa do térmico (o aviso do calor): o pior caso fica acima de 17,9 V − 1 dB.
        Assert.True(ErroDaJbl(ler, master, TermicoJbl, 1.0) > 1.0);
    }

    [Fact]
    public void Reforco_de_3_dB_no_EQ_de_saida_da_JBL_baixa_o_master_3_dB()
    {
        var antes = PiorCaso.Conta(Equipamentos, Saidas, Leitor(Plana()), Bluetooth, NivelRendimento.Alto).Master!.Value;

        var depois = PiorCaso.Conta(Equipamentos, Saidas, Leitor(Plana(), ("091", 0, "+3.0")), Bluetooth, NivelRendimento.Alto);

        Assert.InRange(antes - depois.Master!.Value, 2.9m, 3.1m);
        Assert.Equal(["JBL L"], depois.SaidasQueMandam);
    }

    [Fact]
    public void Quatro_dB_em_250_Hz_nas_bandas_baixam_o_master_pela_conta_nova()
    {
        var plano = PiorCaso.Conta(Equipamentos, Saidas, Leitor(Plana()), Bluetooth, NivelRendimento.Alto).Master!.Value;
        var ler = Leitor(Plana(), ("020", 5, "+4.0"));

        var grave = PiorCaso.Conta(Equipamentos, Saidas, ler, Bluetooth, NivelRendimento.Alto).Master!.Value;

        Assert.True(grave < plano);
        // O pior caso da JBL com o master novo ainda fica no teto do Alto.
        Assert.InRange(ErroDaJbl(ler, grave, TermicoJbl, 1.0), -0.2, 0.2);
    }

    [Fact]
    public void Sem_ganho_na_memoria_o_master_fica_em_0_com_o_aviso()
    {
        var conta = PiorCaso.Conta(Equipamentos, Saidas, Leitor(Plana(ganhoJbl: 0m)), Bluetooth, NivelRendimento.Alto);

        Assert.Equal(0m, conta.Master);
        Assert.Equal("JBL L", conta.FaltaGanho);
        Assert.Equal("O nível não chega ao teto: falta ganho na memória. Saída: JBL L.", conta.Aviso);
    }

    [Fact]
    public void O_master_nunca_passa_de_menos_80()
    {
        var conta = PiorCaso.Conta(Equipamentos, Saidas, Leitor(Plana(), ("081", 0, "+15.0"), ("030", null, "+12.0")), 60m,
            NivelRendimento.Baixo);

        Assert.Equal(PiorCaso.MinimoMaster, conta.Master);
        Assert.Equal("O nível não chega ao teto: o master não desce o bastante. Saída: JBL L.", conta.Aviso);
    }

    [Fact]
    public void No_Maximo_um_alto_falante_so_com_o_termico_fica_no_termico_e_nao_sai_da_cerca()
    {
        // O Sub com o duto protegendo o cone (sem Bl nem Qes: X infinito) e a ponte sem a tensão de ceifamento.
        var equipamentos = Equipamentos.Select(e => e.Id switch
        {
            CasaDoDenis.IdSubCasa => e with { Dados = e.Dados with { Bl = null, Qes = null } },
            CasaDoDenis.IdModuloCasa => e with { Dados = e.Dados with { TensaoCeifamentoPonte = null } },
            _ => e,
        }).ToArray();
        var saidas = Ligacoes.Saidas(equipamentos);

        var sub = LimiteFisico.Calcular(equipamentos, saidas, Leitor(Plana()), semTermico: true).Single(l => l.Nome == "Sub");

        Assert.True(sub.Excursao!.Infinito);
        Assert.Equal(TipoLimite.Termico, sub.Manda);
        Assert.Equal([2, 3], sub.PorCanal.Select(c => c.Canal));
    }

    [Fact]
    public void Os_niveis_so_sao_oferecidos_com_o_Setup_calibrado_e_o_Bluetooth_medido()
    {
        var saidas = Saidas;

        Assert.Null(Niveis.Indisponivel(CasaDoDenis.SetupCasaComBluetooth, saidas));
        Assert.Equal(Niveis.MsgSemSetup, Niveis.Indisponivel(null, saidas));
        Assert.Equal("Meça o nível do Bluetooth.", Niveis.Indisponivel(CasaDoDenis.SetupCasa, saidas));
        Assert.Equal("Meça o nível do Bluetooth.", Niveis.Indisponivel(
            CasaDoDenis.SetupCasa with { NivelBluetooth = new DadoTecnico(-13m, OrigemDado.Digitado) }, saidas));
        var semCalibracao = CasaDoDenis.SetupCasaComBluetooth with { Equipamentos = CasaDoDenis.EquipamentosSemCalibracao };
        Assert.Equal("Calibre a saída JBL L.", Niveis.Indisponivel(semCalibracao, Ligacoes.Saidas(CasaDoDenis.EquipamentosSemCalibracao)));
    }

    [Fact]
    public void Os_nomes_e_as_margens_dos_niveis()
    {
        Assert.Equal(["Baixo", "Médio", "Alto", "Máximo"], Niveis.Todos.Select(Niveis.Nome));
        Assert.Equal(["baixo", "medio", "alto", "maximo"], Niveis.Todos.Select(Niveis.Chave));
        Assert.Equal([6m, 3m, 1m, 1m], Niveis.Todos.Select(Niveis.Margem));
        Assert.All(Niveis.Todos, n => Assert.Equal(n, Niveis.De(Niveis.Chave(n))));
        Assert.Null(Niveis.De("extra"));
        Assert.Null(Niveis.De(null));
        Assert.Equal("Nível Alto: o master vai até −3,8 dB.", Niveis.MsgMasterAcima(NivelRendimento.Alto, -3.8m));
        Assert.Equal("16 V", Niveis.Volts(15.94));
        Assert.Equal("8,9 V", Niveis.Volts(8.94));
        Assert.Equal("10 V", Niveis.Volts(9.96));
        Assert.Equal("9,9 V", Niveis.Volts(9.94));
    }
}
