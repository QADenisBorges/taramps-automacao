using BgsControle.Nucleo.Escrita;
using BgsControle.Nucleo.Rendimento;
using BgsControle.Nucleo.Setups;
using BgsControle.Simulador;

namespace BgsControle.Nucleo.Testes;

/// <summary>
/// História 13a, o cálculo: o limite físico de cada saída (térmico, excursão com os três casos e
/// ceifamento), a conta, o selo e a conversão para o limitador. Uma linha da matriz por teste, com os
/// números do exemplo de <c>limites-e-niveis.md</c> (<c>memoria-spl.md</c> §12 e §13.2) dentro de 0,2 dB.
/// </summary>
public sealed class LimiteFisicoTestes
{
    private const int Lr24 = 3;
    private const int Bt24 = 11;

    /// <summary>A JBL do exemplo: a ficha oficial (Re 3,4), caixa selada de 3,5 L.</summary>
    private static DadosTecnicos JblExemplo => CasaDoDenis.FichaJbl with
    {
        TipoCaixa = new DadoTexto("selada", OrigemDado.Medido),
        VolumeCaixa = new DadoTecnico(3.5m, OrigemDado.Estimado),
    };

    /// <summary>O Sub do exemplo: a ficha da Protech, dutada de 28 L (volume estimado), Fb 51 Hz medida.</summary>
    private static DadosTecnicos SubExemplo => CasaDoDenis.FichaSub with
    {
        TipoCaixa = new DadoTexto("dutada", OrigemDado.Medido),
        VolumeCaixa = new DadoTecnico(28m, OrigemDado.Estimado),
        Fb = new DadoTecnico(51m, OrigemDado.Medido),
    };

    /// <summary>O Casa 2.1 da fixture com as fichas dadas para a JBL (L e R), o Sub e o módulo.</summary>
    private static IReadOnlyList<Equipamento> Casa(DadosTecnicos? jbl = null, DadosTecnicos? sub = null, DadosTecnicos? modulo = null) =>
        CasaDoDenis.EquipamentosCasa.Select(e => e.Id switch
        {
            CasaDoDenis.IdJblL or CasaDoDenis.IdJblR => e with { Dados = jbl ?? JblExemplo },
            CasaDoDenis.IdSubCasa => e with { Dados = sub ?? SubExemplo },
            CasaDoDenis.IdModuloCasa when modulo is not null => e with { Dados = modulo },
            _ => e,
        }).ToArray();

    /// <summary>O estado do aparelho: o passa-alta de cada canal (tipo e frequência) e o limitador.</summary>
    private static Func<Endereco, Valor?> Estado(int filtroJbl = Lr24, decimal hzJbl = 200, int filtroSub = Bt24, decimal hzSub = 40)
    {
        var d = new Dictionary<Endereco, Valor>();
        void Por(string codigo, int canal, string noFio) => d[new Endereco(codigo, canal)] = Catalogo.ParametroDe(codigo)!.Decodificar(noFio).Valor!;
        for (var c = 0; c < 4; c++)
        {
            var jbl = c < 2;
            Por("042", c, (jbl ? filtroJbl : filtroSub).ToString(System.Globalization.CultureInfo.InvariantCulture));
            Por("040", c, (jbl ? hzJbl : hzSub).ToString(System.Globalization.CultureInfo.InvariantCulture));
            Por("071", c, "-12.0");
        }
        return e => d.GetValueOrDefault(e);
    }

    private static IReadOnlyList<LimiteSaida> Limites(IReadOnlyList<Equipamento> equipamentos, Func<Endereco, Valor?> estado) =>
        LimiteFisico.Calcular(equipamentos, Ligacoes.Saidas(equipamentos), estado);

    private static LimiteSaida Saida(IReadOnlyList<LimiteSaida> limites, string nome) => limites.Single(l => l.Nome == nome);

    /// <summary>Dois volts dentro de 0,2 dB.</summary>
    private static void PertoDb(double esperado, double? real)
    {
        Assert.NotNull(real);
        Assert.InRange(20 * Math.Log10(real!.Value / esperado), -0.2, 0.2);
    }

    // ----------------------------------------------------------------- a matriz

    [Fact]
    public void JBL_do_exemplo_com_LR24_em_200_Hz_da_o_teto_de_menos_6_8_dB_pelo_termico()
    {
        var jbl = Saida(Limites(Casa(), Estado()), "JBL L");

        PertoDb(17.9, jbl.Termico!.V);
        PertoDb(22.2, jbl.Excursao!.V);
        PertoDb(23, jbl.Ceifamento!.V);
        Assert.Equal(TipoLimite.Termico, jbl.Manda);
        Assert.True(jbl.Calibrada);
        Assert.InRange(jbl.TetoDb!.Value, -7.0m, -6.6m);
        Assert.Equal(-6.8m, jbl.TetoDb);
        PertoDb(17.9, jbl.TetoV);
        Assert.False(jbl.Estimado); // o térmico vem da ficha oficial
        Assert.Equal(LimiteFisico.SeloVerificado, jbl.Termico.Selo);
        Assert.Equal(LimiteFisico.SeloEstimado, jbl.Excursao.Selo); // o volume da caixa é estimado
        Assert.Equal("√(80 W × 4 Ω) = 17,9 V", jbl.Termico.Conta);
        Assert.Contains("LR24 em 200 Hz", jbl.Excursao.Conta);
        Assert.Contains("canal 1", jbl.Ceifamento.Conta);
    }

    [Fact]
    public void JBL_com_LR24_em_150_Hz_o_curso_do_cone_manda()
    {
        var jbl = Saida(Limites(Casa(), Estado(hzJbl: 150)), "JBL R");

        PertoDb(12.9, jbl.Excursao!.V);
        Assert.Equal(TipoLimite.Excursao, jbl.Manda);
        Assert.Equal(LimiteFisico.ParaDb(jbl.Excursao.V!.Value, 19.5m), jbl.TetoDb);
        Assert.InRange((double)jbl.TetoDb!.Value, 20 * Math.Log10(12.9 / 39) - 0.2, 20 * Math.Log10(12.9 / 39) + 0.2);
        Assert.True(jbl.Estimado);
    }

    [Fact]
    public void Sub_do_exemplo_na_ponte_da_o_teto_do_X_estimado()
    {
        var sub = Saida(Limites(Casa(), Estado()), "Sub");

        Assert.Equal([2, 3], sub.Canais);
        PertoDb(42.4, sub.Termico!.V);
        PertoDb(40, sub.Ceifamento!.V);
        PertoDb(33.1, sub.Excursao!.V);
        Assert.Equal(TipoLimite.Excursao, sub.Manda);
        Assert.Equal(-7.8m, sub.TetoDb);
        Assert.True(sub.Estimado); // o volume da caixa é estimado
        Assert.Contains("ponte 3+4", sub.Ceifamento.Conta);
        Assert.Contains("dutada de 28 L com Fb de 51 Hz", sub.Excursao.Conta);
        Assert.Contains("BT24 em 40 Hz", sub.Excursao.Conta);
    }

    [Fact]
    public void Caso_2_dutada_sem_Bl_nem_Qes_com_subsonico_logo_abaixo_da_Fb_da_X_infinito_e_manda_o_menor_de_T_e_C()
    {
        var sub = Saida(Limites(Casa(sub: SubExemplo with { Bl = null, Qes = null }), Estado()), "Sub");

        Assert.True(sub.Excursao!.Infinito);
        Assert.Null(sub.Excursao.V);
        Assert.Equal(LimiteFisico.SeloEstimado, sub.Excursao.Selo);
        Assert.Contains("o duto protege o cone", sub.Excursao.Conta);
        Assert.Equal("Falta o Bl ou o Qes do Sub.", sub.Excursao.Falta);
        Assert.Equal(TipoLimite.Ceifamento, sub.Manda); // min(42,4; 40)
        PertoDb(40, sub.TetoV);
        Assert.Equal(LimiteFisico.ParaDb(40, 40.5m), sub.TetoDb);
    }

    [Theory]
    [InlineData(30)] // abaixo de 0,75 × Fb: o subsônico não protege o cone
    [InlineData(45)] // acima de 0,8 × Fb
    public void Caso_2_so_vale_com_o_corte_entre_0_75_e_0_8_da_Fb(int hz)
    {
        var sub = Saida(Limites(Casa(sub: SubExemplo with { Bl = null, Qes = null }), Estado(hzSub: hz)), "Sub");

        Assert.False(sub.Excursao!.Infinito);
        PertoDb(40 * Math.Pow(10, -3.0 / 20), sub.Excursao.V); // o caso 3
    }

    [Fact]
    public void Caso_2_pede_pelo_menos_24_dB_por_oitava()
    {
        var bt12 = 9;
        var sub = Saida(Limites(Casa(sub: SubExemplo with { Bl = null, Qes = null }), Estado(filtroSub: bt12)), "Sub");

        Assert.False(sub.Excursao!.Infinito);
    }

    [Fact]
    public void Caso_3_dutada_sem_Fb_da_o_menor_de_T_e_C_menos_3_dB_estimado_com_o_aviso()
    {
        var sub = Saida(Limites(Casa(sub: SubExemplo with { Fb = null }), Estado()), "Sub");

        PertoDb(40 * Math.Pow(10, -3.0 / 20), sub.Excursao!.V);
        Assert.Equal(LimiteFisico.SeloEstimado, sub.Excursao.Selo);
        Assert.Equal("Falta a Fb da caixa do Sub.", sub.Excursao.Falta);
        Assert.Equal(TipoLimite.Excursao, sub.Manda);
        Assert.True(sub.Estimado);
    }

    [Fact]
    public void Caso_3_sem_o_tipo_da_caixa_e_sem_volume()
    {
        var jbl = Saida(Limites(Casa(jbl: CasaDoDenis.FichaJbl), Estado()), "JBL L");

        Assert.Equal("Faltam o tipo da caixa (selada ou dutada) e o volume da caixa do JBL L.", jbl.Excursao!.Falta);
        PertoDb(17.9 * Math.Pow(10, -3.0 / 20), jbl.Excursao.V);
        Assert.Equal(TipoLimite.Excursao, jbl.Manda);
    }

    [Fact]
    public void Sem_calibracao_mostra_os_volts_e_o_dB_fica_null()
    {
        var modulo = CasaDoDenis.EquipamentosSemCalibracao.Single(e => e.Id == CasaDoDenis.IdModuloCasa).Dados;
        var limites = Limites(Casa(modulo: modulo), Estado());

        Assert.All(limites, l =>
        {
            Assert.False(l.Calibrada);
            Assert.Null(l.TetoDb);
            Assert.NotNull(l.TetoV);
            Assert.False(l.Cercada);
        });
        PertoDb(17.9, Saida(limites, "JBL L").TetoV);
    }

    [Fact]
    public void O_Re_medido_da_fixture_da_X_de_22_9_V_e_o_teto_continua_pelo_termico()
    {
        var limites = LimiteFisico.Calcular(CasaDoDenis.EquipamentosCasa, Ligacoes.Saidas(CasaDoDenis.EquipamentosCasa), Estado());
        var jbl = Saida(limites, "JBL L");

        PertoDb(22.9, jbl.Excursao!.V);
        Assert.Equal(TipoLimite.Termico, jbl.Manda);
        Assert.Equal(-6.8m, jbl.TetoDb);
    }

    [Fact]
    public void Sem_passa_alta_o_filtro_nao_entra()
    {
        var com = Saida(Limites(Casa(), Estado()), "JBL L");
        var sem = Saida(Limites(Casa(), Estado(filtroJbl: 0)), "JBL L");

        Assert.True(sem.Excursao!.V < com.Excursao!.V);
        Assert.Contains("sem passa-alta", sem.Excursao.Conta);
    }

    [Fact]
    public void Sem_nenhum_dos_tres_a_saida_fica_sem_limite_com_o_motivo()
    {
        var vazio = DadosTecnicos.Nenhum;
        var modulo = CasaDoDenis.FichaModulo with { TensaoCeifamentoCanal = null, TensaoCeifamentoPonte = null };
        var jbl = Saida(Limites(Casa(jbl: vazio, modulo: modulo), Estado()), "JBL L");

        Assert.Null(jbl.TetoV);
        Assert.Null(jbl.Manda);
        Assert.False(jbl.Cercada);
        Assert.Contains("Faltam a potência RMS e a impedância do JBL L.", jbl.SemLimite);
    }

    [Fact]
    public void Um_canal_so_com_caixa_ativa_fica_sem_limite()
    {
        Equipamento[] lista =
        [
            new("ativa", GrupoEquipamento.AmplificaETocam, "caixa ativa 2 vias", "Ativa")
            {
                Entradas = [new Entrada(1, FonteSinal.DoCanal(0))],
            },
        ];
        var limites = LimiteFisico.Calcular(lista, Ligacoes.Saidas(lista), _ => null);

        Assert.Equal(LimiteFisico.MsgSemAltoFalante, limites[0].SemLimite);
        Assert.Null(limites[0].TetoDb);
        Assert.Equal(LimiteFisico.MsgSemAltoFalante, limites[1].SemLimite); // CH2: nada
    }

    [Fact]
    public void Sem_Setup_nenhum_limite()
    {
        Assert.Empty(LimiteFisico.Calcular(null, Ligacoes.Saidas([], false, false), _ => null));
    }

    [Theory]
    [InlineData("selada", TipoCaixaModelo.Selada)]
    [InlineData("  Selada pequena", TipoCaixaModelo.Selada)]
    [InlineData("DUTADA", TipoCaixaModelo.Dutada)]
    [InlineData("duto frontal", TipoCaixaModelo.Dutada)]
    [InlineData("Bass Reflex", TipoCaixaModelo.Dutada)]
    [InlineData("Caixa selada", TipoCaixaModelo.Selada)]
    [InlineData("bass-reflex", TipoCaixaModelo.Dutada)]
    [InlineData("reflex", TipoCaixaModelo.Dutada)]
    [InlineData("Caixa com DUTO", TipoCaixaModelo.Dutada)]
    [InlineData("caixa dutáda", TipoCaixaModelo.Dutada)]
    [InlineData("selado", TipoCaixaModelo.Selada)]
    [InlineData("caixa fechada", TipoCaixaModelo.Selada)]
    [InlineData("sealed box", TipoCaixaModelo.Selada)]
    [InlineData("dutado", TipoCaixaModelo.Dutada)]
    [InlineData("Vented", TipoCaixaModelo.Dutada)]
    [InlineData("ported", TipoCaixaModelo.Dutada)]
    [InlineData("produto importado", null)]
    [InlineData("conduto", null)]
    [InlineData("sem duto", null)]
    [InlineData("não selada", null)]
    [InlineData("nao selada", null)]
    [InlineData("dutada (antes era selada)", null)]
    [InlineData("radiador passivo", null)]
    [InlineData("", null)]
    public void O_tipo_da_caixa_e_texto_livre(string texto, TipoCaixaModelo? esperado)
    {
        Assert.Equal(esperado, LimiteFisico.TipoDaCaixa(texto));
    }

    [Fact]
    public void O_filtro_entra_so_pelo_modulo()
    {
        var lr24 = PassaAlta.DoAparelho(Lr24, 200)!;
        var bt24 = PassaAlta.DoAparelho(Bt24, 40)!;

        Assert.Equal((FormaFiltro.Lr, 24), (lr24.Forma, lr24.Inclinacao));
        Assert.Equal((FormaFiltro.Bt, 24), (bt24.Forma, bt24.Inclinacao));
        Assert.Equal(0.5, lr24.Modulo(200), 6); // LR: −6 dB no corte
        Assert.Equal(1 / Math.Sqrt(2), bt24.Modulo(40), 6); // BT: −3 dB no corte
        Assert.Null(PassaAlta.DoAparelho(0, 200)); // OFF
        Assert.Null(PassaAlta.DoAparelho(null, 200)); // sem leitura
    }

    [Theory]
    [InlineData(17.8885, 19.5, "-6.8")]
    [InlineData(39.0, 19.5, "0.0")]
    [InlineData(33.06, 40.5, "-7.8")]
    public void A_conversao_arredonda_para_baixo_no_passo_de_0_1(double volts, double ganho, string esperado)
    {
        Assert.Equal(decimal.Parse(esperado, System.Globalization.CultureInfo.InvariantCulture), LimiteFisico.ParaDb(volts, (decimal)ganho));
    }

    [Fact]
    public void Um_dado_so_digitado_leva_o_selo_estimado()
    {
        var jbl = Saida(Limites(Casa(jbl: JblExemplo with { PotenciaRms = new DadoTecnico(80m, OrigemDado.Digitado) }), Estado()), "JBL L");

        Assert.Equal(LimiteFisico.SeloEstimado, jbl.Termico!.Selo);
        Assert.True(jbl.Estimado);
    }

    // ----------------------------------------------------------------- a cerca, pura

    /// <summary>O espelho do estado (passa-alta das JBL em LR24 200 Hz, do Sub em BT24 40 Hz, limitador em −12 dB), com mudanças.</summary>
    private static Dictionary<Endereco, Valor> Espelho(params (string Codigo, int Canal, string? NoFio)[] mudancas)
    {
        var estado = Estado();
        var d = new Dictionary<Endereco, Valor>();
        foreach (var codigo in new[] { "040", "042", "071" })
        {
            for (var c = 0; c < 4; c++)
                d[new Endereco(codigo, c)] = estado(new Endereco(codigo, c))!;
        }
        foreach (var (codigo, canal, noFio) in mudancas)
        {
            if (noFio is null)
                d.Remove(new Endereco(codigo, canal));
            else
                d[new Endereco(codigo, canal)] = V(codigo, noFio);
        }
        return d;
    }

    private static Valor V(string codigo, string noFio) => Catalogo.ParametroDe(codigo)!.Decodificar(noFio).Valor!;

    private static (Endereco, Valor?) Escrita(string codigo, int canal, string? noFio) =>
        (new Endereco(codigo, canal), noFio is null ? null : V(codigo, noFio));

    [Fact]
    public void Teto_abaixo_de_menos_48_dB_a_cerca_usa_menos_48_e_avisa()
    {
        // 1 mW em 4 Ω: T = 0,063 V, uns −55,8 dB no limitador.
        var fraca = JblExemplo with { PotenciaRms = new DadoTecnico(0.001m, OrigemDado.Oficial) };
        var lista = Casa(jbl: fraca);
        var jbl = Saida(Limites(lista, Estado()), "JBL L");

        Assert.True(jbl.TetoDb < -48m);
        Assert.Equal(-48m, jbl.TetoCerca);
        Assert.Equal(LimiteSaida.MsgAbaixoDoMinimo, jbl.Aviso);
        Assert.Null(Saida(Limites(Casa(), Estado()), "JBL L").Aviso);

        var saidas = Ligacoes.Saidas(lista);
        Assert.Null(Cerca.Conferir(lista, saidas, Espelho(), [Escrita("071", 0, "-48.0")])); // não trava
        Assert.Equal("Limite: JBL L, −48,0 dB, pelo térmico.", Cerca.Conferir(lista, saidas, Espelho(), [Escrita("071", 0, "-47.9")]));
    }

    [Fact]
    public void Valor_desconhecido_o_corte_sem_o_limitador_no_espelho_e_recusado()
    {
        var lista = Casa();
        var saidas = Ligacoes.Saidas(lista);

        var semLimitador = Cerca.Conferir(lista, saidas, Espelho(("071", 0, null)), [Escrita("040", 0, "250")]);
        var naoDecodifica = Cerca.Conferir(lista, saidas, Espelho(), [Escrita("071", 1, null)]);

        Assert.Equal("Limite: JBL L, o app não sabe o valor do limitador; releia o aparelho.", semLimitador);
        Assert.Equal("Limite: JBL R, o app não sabe o valor do limitador; releia o aparelho.", naoDecodifica);
        // Numa saída sem calibração, o desconhecido passa (a cerca não vale ali).
        var semCalibracao = Casa(modulo: CasaDoDenis.EquipamentosSemCalibracao.Single(e => e.Id == CasaDoDenis.IdModuloCasa).Dados);
        Assert.Null(Cerca.Conferir(semCalibracao, saidas, Espelho(("071", 0, null)), [Escrita("040", 0, "250")]));
    }

    [Fact]
    public void No_par_com_tetos_diferentes_a_mensagem_diz_o_canal_culpado()
    {
        // A JBL R com 40 W: T = 12,6 V, uns −9,8 dB; a JBL L continua em −6,8 dB.
        var lista = CasaDoDenis.EquipamentosCasa.Select(e => e.Id switch
        {
            CasaDoDenis.IdJblL => e with { Dados = JblExemplo },
            CasaDoDenis.IdJblR => e with { Dados = JblExemplo with { PotenciaRms = new DadoTecnico(40m, OrigemDado.Oficial) } },
            _ => e,
        }).ToArray();
        var saidas = Ligacoes.Saidas(lista, link12: true, link34: true);
        var par = Saida(LimiteFisico.Calcular(lista, saidas, Estado()), "JBL L + JBL R");
        Assert.Equal([-6.8m, LimiteFisico.ParaDb(Math.Sqrt(160), 19.5m)], par.PorCanal.Select(c => c.TetoDb));

        // A escrita da tela no par vai aos dois canais (a cópia do link): −8 passa na L e não na R.
        var r = Cerca.Conferir(lista, saidas, Espelho(), [Escrita("071", 0, "-8.0"), Escrita("071", 1, "-8.0")]);

        Assert.Equal($"Limite: JBL L + JBL R (CH2), {LimiteFisico.Db(par.PorCanal[1].TetoDb)} dB, pelo térmico.", r);
        Assert.Null(Cerca.Conferir(lista, saidas, Espelho(), [Escrita("071", 0, "-10.0"), Escrita("071", 1, "-10.0")]));
    }

    [Fact]
    public void A_calibracao_parcial_nao_desliga_a_cerca_do_canal_calibrado()
    {
        // A JBL R num segundo módulo, com o ganho só digitado; a JBL L continua no módulo calibrado.
        var lista = CasaDoDenis.EquipamentosCasa
            .Select(e => e.Id == CasaDoDenis.IdJblR ? e with { Ligado = LigacaoAltoFalante.NoCanal("modulo-2", 1) } : e)
            .Append(new Equipamento("modulo-2", GrupoEquipamento.Amplificador, "módulo", "Módulo 2")
            {
                Dados = CasaDoDenis.FichaModulo with { Ganho = new DadoTecnico(19.5m, OrigemDado.Digitado) },
                Entradas = [new Entrada(1, FonteSinal.DoCanal(1))],
            })
            .ToArray();
        var saidas = Ligacoes.Saidas(lista, link12: true, link34: true);
        var par = Saida(LimiteFisico.Calcular(lista, saidas, Estado()), "JBL L + JBL R");

        Assert.False(par.Calibrada);
        Assert.Null(par.TetoDb);
        Assert.True(par.Cercada);
        Assert.Equal([0], par.PorCanal.Select(c => c.Canal));
        Assert.Equal("Limite: JBL L + JBL R (CH1), −6,8 dB, pelo térmico.",
            Cerca.Conferir(lista, saidas, Espelho(), [Escrita("071", 0, "-6.7"), Escrita("071", 1, "-6.7")]));
    }

    [Fact]
    public void Na_volta_o_limitador_OFF_ou_acima_vai_ao_teto_calculado_com_o_corte_da_copia()
    {
        var lista = Casa();
        var saidas = Ligacoes.Saidas(lista);
        // Agora: JBL L em 200 Hz e −6,8; a cópia tem a JBL L em 150 Hz e OFF, a JBL R em −3 e o Sub em −20.
        var espelho = Espelho(("071", 0, "-6.8"));
        var tetos = Cerca.NaVolta(lista, saidas, espelho,
            [Escrita("040", 0, "150"), Escrita("071", 0, "+0.5"), Escrita("071", 1, "-3.0"), Escrita("071", 2, "-20.0"), Escrita("071", 3, "-20.0")]);

        var jbl150 = Saida(Limites(lista, Estado(hzJbl: 150)), "JBL L");
        Assert.Equal(
            [
                new TetoNaVolta(new Endereco("071", 0), jbl150.TetoCerca!.Value, "JBL L"),
                new TetoNaVolta(new Endereco("071", 1), -6.8m, "JBL R"),
            ],
            tetos);
        Assert.Equal(TipoLimite.Excursao, jbl150.Manda); // o teto da cópia, e não o do corte de agora
        Assert.Empty(Cerca.NaVolta(lista, saidas, espelho, [Escrita("071", 1, "-20.0")]));
        Assert.Empty(Cerca.NaVolta(null, saidas, espelho, [Escrita("071", 0, "+0.5")]));
    }

    [Fact]
    public void Os_alto_falantes_fora_da_conta_aparecem_com_o_motivo()
    {
        Equipamento[] extras =
        [
            new("solto", GrupoEquipamento.AltoFalante, "tweeter", "Tweeter solto"),
            new("ativa", GrupoEquipamento.AmplificaETocam, "caixa ativa", "Ativa") { Entradas = [new Entrada(1, null)] },
            new("na-ativa", GrupoEquipamento.AltoFalante, "tweeter", "Tweeter na ativa") { Ligado = LigacaoAltoFalante.NoCanal("ativa", 1) },
            new("modulo-mudo", GrupoEquipamento.Amplificador, "módulo", "Módulo mudo") { Entradas = [new Entrada(1, null)] },
            new("sem-sinal", GrupoEquipamento.AltoFalante, "woofer", "Woofer sem sinal") { Ligado = LigacaoAltoFalante.NoCanal("modulo-mudo", 1) },
        ];
        var lista = CasaDoDenis.EquipamentosCasa.Concat(extras).ToArray();

        var fora = LimiteFisico.ForaDaConta(lista);

        Assert.Equal(
            [
                new AltoFalanteFora("Tweeter solto", "Desligado: não está em nenhum canal de módulo."),
                new AltoFalanteFora("Tweeter na ativa", "Ligado ao Ativa, que não é amplificador."),
                new AltoFalanteFora("Woofer sem sinal", "O canal 1 do Módulo mudo não recebe sinal do processador."),
            ],
            fora);
        Assert.Empty(LimiteFisico.ForaDaConta(CasaDoDenis.EquipamentosCasa));
        Assert.Empty(LimiteFisico.ForaDaConta(null));
    }
}
