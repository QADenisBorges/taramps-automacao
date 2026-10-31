using BgsControle.Simulador;
using BgsControle.Nucleo.Setups;

namespace BgsControle.Nucleo.Testes;

/// <summary>
/// História 8e, a lógica pura do modelo de ligação (<see cref="Ligacoes"/>): as saídas do app, a
/// cadeia de alimentação, a validação e a sugestão de função. Uma linha da matriz de
/// <c>8e-equipamentos-ligacoes-e-saidas-do-setup.md</c> por teste (ou mais).
/// </summary>
public sealed class EquipamentosTestes
{
    private static Equipamento Modulo(string id, string nome, int canais, params Bridge[] bridges) =>
        new(id, GrupoEquipamento.Amplificador, "módulo", nome)
        {
            Entradas = Enumerable.Range(1, canais).Select(n => new Entrada(n, null)).ToArray(),
            Bridges = bridges,
        };

    private static Equipamento ComFontes(Equipamento e, params FonteSinal?[] fontes) =>
        e with { Entradas = fontes.Select((f, i) => new Entrada(i + 1, f)).ToArray() };

    private static Equipamento AltoFalante(string id, string nome, string modulo, int canal, string categoria = "woofer") =>
        new(id, GrupoEquipamento.AltoFalante, categoria, nome) { Ligado = LigacaoAltoFalante.NoCanal(modulo, canal) };

    private static Equipamento Ativo(string id, string nome, string categoria, params FonteSinal?[] fontes) =>
        ComFontes(new Equipamento(id, GrupoEquipamento.AmplificaETocam, categoria, nome), fontes);

    private static Equipamento Repasse(string id, string nome, params FonteSinal?[] fontes) =>
        ComFontes(new Equipamento(id, GrupoEquipamento.Repassa, "crossover ativo", nome), fontes);

    private static FonteSinal Ch(int n) => FonteSinal.DoCanal(n - 1);

    private static FonteSinal De(string id) => FonteSinal.DoEquipamento(id);

    private static IEnumerable<(string Nome, string Rotulo, int Cor, string? Aviso)> Resumo(IEnumerable<SaidaApp> saidas) =>
        saidas.Select(s => (s.Nome, s.Rotulo, s.Cor, s.Aviso));

    // ----------------------------------------------------------------- Casa 2.1 da fixture CasaDoDenis

    [Fact]
    public void Casa_2_1_da_fixture_as_saidas_sao_JBL_L_JBL_R_e_Sub_no_par_CH3_CH4_com_a_cor_do_CH3()
    {
        var casa = CasaDoDenis.SetupCasa;

        Assert.Null(Ligacoes.Validar(casa.Equipamentos));
        Assert.Equal((false, true), Ligacoes.Links(casa.Equipamentos)); // o link sai do bridge 3+4
        Assert.Equal<(string, string, int, string?)>(
            [("JBL L", "CH1", 1, null), ("JBL R", "CH2", 2, null), ("Sub", "CH3 + CH4", 3, null)],
            Resumo(Ligacoes.Saidas(casa.Equipamentos)));
        Assert.Equal([2, 3], Ligacoes.Saidas(casa.Equipamentos, false, true)[2].Canais);
    }

    [Fact]
    public void Os_equipamentos_da_fixture_tem_o_SD800_4_EVO_as_JBL_e_o_Protech_no_bridge()
    {
        var e = CasaDoDenis.EquipamentosCasa;

        Assert.Equal(["Módulo SD800.4 EVO", "JBL L", "JBL R", "Sub"], e.Select(x => x.Nome));
        var modulo = e[0];
        Assert.Equal(GrupoEquipamento.Amplificador, modulo.Grupo);
        Assert.Equal("SD800.4 EVO", modulo.ModeloExibido);
        Assert.Equal(new DadoTecnico(19.5m, OrigemDado.Medido, "Medido em 17 e 18/10/2026"), modulo.Dados.Ganho);
        Assert.Equal<FonteSinal?>([Ch(1), Ch(2), Ch(3), Ch(4)], modulo.Entradas.Select(x => x.Fonte));
        Assert.Equal([new Bridge(3, 4)], modulo.Bridges);
        Assert.All(e.Skip(1).Take(2), j =>
        {
            Assert.Equal("JBL 4TRMS80", j.Modelo);
            Assert.Equal(4m, j.Dados.Impedancia!.Valor);
            Assert.Equal(3.6m, j.Dados.Re!.Valor);
            Assert.Contains("100 Hz a 24 dB/oit", j.Nota);
        });
        Assert.Equal(LigacaoAltoFalante.NoCanal(CasaDoDenis.IdModuloCasa, 1), e[1].Ligado);
        Assert.Equal(LigacaoAltoFalante.NoCanal(CasaDoDenis.IdModuloCasa, 2), e[2].Ligado);
        Assert.Equal(LigacaoAltoFalante.NoBridge(CasaDoDenis.IdModuloCasa, new Bridge(3, 4)), e[3].Ligado);
        Assert.Equal("Protech Light 8\"", e[3].ModeloExibido);
        Assert.Equal([2, 3], Ligacoes.CanaisQueAlimentam(e, CasaDoDenis.IdSubCasa));
    }

    // ----------------------------------------------------------------- saídas do app

    [Fact]
    public void Setup_de_4_canais_sem_link_da_4_saidas_com_os_nomes_dos_que_tocam()
    {
        var lista = new[]
        {
            ComFontes(Modulo("m", "Módulo", 4), Ch(1), Ch(2), Ch(3), Ch(4)),
            AltoFalante("t1", "Tweeter 1", "m", 1, "tweeter"),
            AltoFalante("t2", "Tweeter 2", "m", 1, "tweeter"),
            AltoFalante("sl", "Snake L", "m", 2),
            AltoFalante("sr", "Snake R", "m", 3),
            AltoFalante("jl", "JBL L traseira", "m", 4),
            AltoFalante("jr", "JBL R traseira", "m", 4),
        };

        Assert.Null(Ligacoes.Validar(lista));
        Assert.Equal(
            ["Tweeter 1 + Tweeter 2", "Snake L", "Snake R", "JBL L traseira + JBL R traseira"],
            Ligacoes.Saidas(lista, false, false).Select(s => s.Nome));
        Assert.Equal([1, 2, 3, 4], Ligacoes.Saidas(lista, false, false).Select(s => s.Cor));
        Assert.All(Ligacoes.Saidas(lista, false, false), s => Assert.Null(s.Aviso));
    }

    [Fact]
    public void Cabo_Y_o_CH1_alimenta_uma_caixa_ativa_e_um_sub_ativo_e_a_saida_tem_os_dois_nomes()
    {
        var lista = new[]
        {
            Ativo("cx", "Caixa", "caixa ativa 2 vias", Ch(1)),
            Ativo("sb", "Sub ativo", "sub ativo", Ch(1)),
        };

        var saidas = Ligacoes.Saidas(lista, false, false);

        Assert.Equal(["Caixa + Sub ativo", "CH2", "CH3", "CH4"], saidas.Select(s => s.Nome));
    }

    [Fact]
    public void Repasse_CH2_crossover_dois_modulos_alto_falantes_a_saida_tem_os_nomes_dos_alto_falantes()
    {
        var lista = new[]
        {
            Repasse("x", "Crossover", Ch(2)),
            ComFontes(Modulo("m1", "Módulo A", 2), De("x"), De("x")),
            ComFontes(Modulo("m2", "Módulo B", 1), De("x")),
            AltoFalante("w", "Woofer", "m1", 1),
            AltoFalante("d", "Driver", "m1", 2, "driver"),
            AltoFalante("t", "Tweeter", "m2", 1, "tweeter"),
        };

        Assert.Null(Ligacoes.Validar(lista));
        var saidas = Ligacoes.Saidas(lista, false, false);
        Assert.Equal(["CH1", "Woofer + Driver + Tweeter", "CH3", "CH4"], saidas.Select(s => s.Nome));
        Assert.Equal([1], Ligacoes.CanaisQueAlimentam(lista, "t"));
    }

    [Fact]
    public void Saida_sem_quem_toque_leva_o_nome_dos_outros_que_ela_alimenta()
    {
        var lista = new[] { ComFontes(Modulo("m", "Módulo", 2), Ch(1), null) };

        Assert.Equal(["Módulo", "CH2", "CH3", "CH4"], Ligacoes.Saidas(lista, false, false).Select(s => s.Nome));
    }

    [Fact]
    public void Nomes_repetidos_aparecem_uma_vez_na_ordem_de_criacao()
    {
        var lista = new[]
        {
            ComFontes(Modulo("m", "Módulo", 2), Ch(1), Ch(1)),
            AltoFalante("b", "Porta", "m", 2),
            AltoFalante("a", "Porta", "m", 1),
            AltoFalante("c", "Teto", "m", 1),
        };

        Assert.Equal("Porta + Teto", Ligacoes.Saidas(lista, false, false)[0].Nome);
    }

    [Theory]
    [InlineData(false, false, new[] { "CH1", "CH2", "CH3", "CH4" })]
    [InlineData(false, true, new[] { "CH1", "CH2", "CH3 + CH4" })]
    [InlineData(true, true, new[] { "CH1 + CH2", "CH3 + CH4" })]
    public void Sem_equipamento_as_saidas_sao_CH1_a_CH4_com_os_pares_do_link(bool link12, bool link34, string[] nomes)
    {
        var saidas = Ligacoes.Saidas([], link12, link34);

        Assert.Equal(nomes, saidas.Select(s => s.Nome));
        Assert.Equal(nomes, saidas.Select(s => s.Rotulo));
    }

    [Fact]
    public void Bridge_com_o_link_forcado_desligado_avisa_que_os_dois_canais_precisam_ser_iguais_sem_pedir_o_link()
    {
        var casa = CasaDoDenis.EquipamentosCasa;

        var saidas = Ligacoes.Saidas(casa, false, false);

        Assert.Equal(["JBL L", "JBL R", "Sub", "Sub"], saidas.Select(s => s.Nome));
        Assert.Null(saidas[0].Aviso);
        Assert.Null(saidas[1].Aviso);
        // O usuário não mexe no link (decisão D1, 29/10/2026): o aviso não pede para ligá-lo.
        Assert.Equal("O bridge 3+4 do Módulo SD800.4 EVO recebe o CH3 e o CH4: os dois canais precisam ser iguais.", saidas[2].Aviso);
        Assert.Equal(saidas[2].Aviso, saidas[3].Aviso);
        Assert.Equal(3, saidas[2].Cor);
        Assert.Equal(4, saidas[3].Cor);
    }

    // ----------------------------------------------------------------- validação

    [Theory]
    [InlineData(2, 3)]
    [InlineData(1, 3)]
    [InlineData(3, 4)] // o módulo tem só 2 canais
    [InlineData(0, 1)]
    public void Bridge_que_nao_junta_dois_canais_vizinhos_e_recusado(int a, int b) =>
        Assert.Equal(Ligacoes.MsgBridgeVizinho, Ligacoes.Validar([Modulo("m", "Módulo", a == 3 ? 2 : 4, new Bridge(a, b))]));

    [Fact]
    public void Um_canal_em_dois_bridges_e_recusado() =>
        Assert.Equal(Ligacoes.MsgBridgeRepetido, Ligacoes.Validar([Modulo("m", "Módulo", 4, new Bridge(1, 2), new Bridge(1, 2))]));

    [Fact]
    public void Ciclo_no_grupo_3_e_recusado()
    {
        var lista = new[] { Repasse("a", "A", De("b")), Repasse("b", "B", De("a")) };

        Assert.Equal(Ligacoes.MsgCiclo, Ligacoes.Validar(lista));
        Assert.Equal(Ligacoes.MsgCiclo, Ligacoes.Validar([Repasse("a", "A", De("a"))]));
        Assert.Null(Ligacoes.Validar([Repasse("a", "A", Ch(1)), Repasse("b", "B", De("a"), De("a"))]));
    }

    [Fact]
    public void Ligacao_para_equipamento_que_nao_existe_ou_do_grupo_errado_e_recusada()
    {
        var modulo = ComFontes(Modulo("m", "Módulo", 2), Ch(1), Ch(2));

        Assert.Equal(Ligacoes.MsgModuloNaoExiste, Ligacoes.Validar([modulo, AltoFalante("w", "W", "outro", 1)]));
        Assert.Equal(Ligacoes.MsgModuloGrupoErrado,
            Ligacoes.Validar([modulo, Ativo("cx", "Caixa", "caixa ativa 2 vias", Ch(1)), AltoFalante("w", "W", "cx", 1)]));
        Assert.Equal(Ligacoes.MsgCanalModuloNaoExiste, Ligacoes.Validar([modulo, AltoFalante("w", "W", "m", 3)]));
        Assert.Equal(Ligacoes.MsgBridgeNaoExiste,
            Ligacoes.Validar([modulo, new Equipamento("w", GrupoEquipamento.AltoFalante, "woofer", "W") { Ligado = LigacaoAltoFalante.NoBridge("m", new Bridge(1, 2)) }]));
        Assert.Equal(Ligacoes.MsgFonteNaoExiste, Ligacoes.Validar([ComFontes(Modulo("m", "Módulo", 1), De("nada"))]));
        Assert.Equal(Ligacoes.MsgFonteGrupoErrado, Ligacoes.Validar([modulo, ComFontes(Modulo("m2", "Outro", 1), De("m"))]));
        Assert.Equal(Ligacoes.MsgCanalProcessador, Ligacoes.Validar([ComFontes(Modulo("m", "Módulo", 1), FonteSinal.DoCanal(4))]));
        Assert.Equal(Ligacoes.MsgIdRepetido, Ligacoes.Validar([modulo, modulo]));
        Assert.Equal(Ligacoes.MsgSemEntrada, Ligacoes.Validar([new Equipamento("cx", GrupoEquipamento.AmplificaETocam, "soundbar", "Barra")]));
        Assert.Equal(Ligacoes.MsgNumeroInvalido,
            Ligacoes.Validar([AltoFalante("w", "W", "m", 1) with { Ligado = null, Dados = new DadosTecnicos { Diametro = new DadoTecnico(0m, OrigemDado.Digitado) } }]));
    }

    [Fact]
    public void Mais_de_um_alto_falante_no_mesmo_canal_e_sem_ligacao_valem()
    {
        var lista = new[]
        {
            ComFontes(Modulo("m", "Módulo", 1), Ch(1)),
            AltoFalante("a", "A", "m", 1),
            AltoFalante("b", "B", "m", 1),
            AltoFalante("c", "C", "m", 1) with { Ligado = null },
        };

        Assert.Null(Ligacoes.Validar(lista));
        Assert.Equal("A + B", Ligacoes.Saidas(lista, false, false)[0].Nome);
    }

    // ----------------------------------------------------------------- sugestão de função

    [Theory]
    [InlineData("tweeter", null, null, FuncaoAcustica.Agudo)]
    [InlineData("driver", null, null, FuncaoAcustica.Medio)]
    [InlineData("driver", 2500, null, FuncaoAcustica.Agudo)]
    [InlineData("médio", null, null, FuncaoAcustica.Medio)]
    [InlineData("Médio-grave", null, 150, FuncaoAcustica.MedioGrave)]
    [InlineData("woofer", null, null, FuncaoAcustica.Grave)]
    [InlineData("subwoofer", null, null, FuncaoAcustica.Sub)]
    [InlineData("sub ativo", null, null, FuncaoAcustica.Sub)]
    [InlineData("coaxial", null, null, FuncaoAcustica.FullRange)]
    [InlineData("caixa ativa full-range", null, 120, FuncaoAcustica.FullRange)]
    [InlineData("Outra", null, 120, FuncaoAcustica.Sub)]
    [InlineData("Outra", null, 200, FuncaoAcustica.Sub)]
    [InlineData("Outra", null, 201, FuncaoAcustica.FullRange)]
    [InlineData("Outra", 2000, null, FuncaoAcustica.Agudo)]
    [InlineData("Outra", 1999, null, FuncaoAcustica.FullRange)]
    [InlineData("shaker", null, null, FuncaoAcustica.FullRange)]
    public void Sugestao_de_funcao_pela_categoria_e_senao_pelo_corte(string categoria, int? passaAlta, int? passaBaixa, FuncaoAcustica esperada) =>
        Assert.Equal(esperada, Ligacoes.SugerirFuncao(categoria, new CorteCanal(passaAlta, passaBaixa)));

    // ----------------------------------------------------------------- tirar

    [Fact]
    public void Tirar_o_modulo_deixa_os_3_alto_falantes_sem_ligacao_e_diz_os_nomes()
    {
        var lista = new[]
        {
            ComFontes(Modulo("m", "Módulo", 2), Ch(1), Ch(2)),
            AltoFalante("a", "Woofer", "m", 1),
            AltoFalante("b", "Médio", "m", 1),
            AltoFalante("c", "Tweeter", "m", 2),
        };

        var r = Ligacoes.Tirar(lista, "m");

        Assert.Equal(["Woofer", "Médio", "Tweeter"], r.SemLigacao);
        Assert.Empty(r.SemFonte);
        Assert.All(r.Lista, e => Assert.Null(e.Ligado));
        Assert.Null(Ligacoes.Validar(r.Lista));
        Assert.Equal(["CH1", "CH2", "CH3", "CH4"], Ligacoes.Saidas(r.Lista, false, false).Select(s => s.Nome));
    }

    [Fact]
    public void Tirar_um_equipamento_do_grupo_3_deixa_sem_fonte_quem_ele_alimentava()
    {
        var lista = new[]
        {
            Repasse("x", "Crossover", Ch(2)),
            ComFontes(Modulo("m", "Módulo A", 2), De("x"), Ch(1)),
        };

        var r = Ligacoes.Tirar(lista, "x");

        Assert.Equal(["Módulo A"], r.SemFonte);
        Assert.Equal<FonteSinal?>([null, Ch(1)], Assert.Single(r.Lista).Entradas.Select(e => e.Fonte));
    }

    [Fact]
    public void Sanear_desfaz_as_ligacoes_quebradas_e_o_ciclo_sem_tirar_ninguem()
    {
        var lista = new[]
        {
            Repasse("a", "A", De("b")),
            Repasse("b", "B", De("a")),
            AltoFalante("w", "W", "sumiu", 1),
            ComFontes(Modulo("m", "Módulo", 1), De("sumiu")),
        };

        var saneada = Ligacoes.Sanear(lista);

        Assert.Equal(4, saneada.Count);
        Assert.Null(Ligacoes.Validar(saneada));
        Assert.Null(saneada[2].Ligado);
        Assert.Null(saneada[3].Entradas[0].Fonte);
    }

    // ----------------------------------------------------------------- revisão da 8e

    [Fact]
    public void Id_vazio_tem_o_seu_proprio_motivo() =>
        Assert.Equal(Ligacoes.MsgIdVazio, Ligacoes.ValidarUm(AltoFalante(" ", "W", "m", 1) with { Ligado = null }));

    [Fact]
    public void Editar_o_modulo_tirando_o_canal_ou_o_bridge_de_um_alto_falante_diz_quem_bloqueia()
    {
        var casa = CasaDoDenis.EquipamentosCasa;
        var modulo = casa[0];
        IReadOnlyList<Equipamento> Com(Equipamento m) => [m, .. casa.Skip(1)];

        Assert.Equal("O Sub está ligado no bridge 3+4 deste módulo: religue ou tire o Sub antes.",
            Ligacoes.Validar(Com(modulo with { Bridges = [] }), modulo.Id));
        Assert.Equal("JBL R e Sub estão ligados neste módulo: religue ou tire eles antes.",
            Ligacoes.Validar(Com(modulo with { Bridges = [], Entradas = modulo.Entradas.Take(1).ToArray() }), modulo.Id));
        Assert.Equal("JBL L, JBL R e Sub estão ligados neste módulo: religue ou tire eles antes.",
            Ligacoes.Validar(Com(modulo with { Grupo = GrupoEquipamento.AmplificaETocam, Bridges = [] }), modulo.Id));
        Assert.Null(Ligacoes.Validar(casa, modulo.Id));
    }

    [Fact]
    public void Sanear_tira_so_os_bridges_que_nao_valem_e_fica_o_primeiro_de_cada_id()
    {
        var casa = CasaDoDenis.EquipamentosCasa;
        var lista = new List<Equipamento>(casa);
        lista[0] = casa[0] with { Bridges = [new Bridge(3, 4), new Bridge(2, 3), new Bridge(3, 4)] };
        lista.Add(casa[1] with { Nome = "Repetida" });

        var saneada = Ligacoes.Sanear(lista);

        Assert.Equal(["Módulo SD800.4 EVO", "JBL L", "JBL R", "Sub"], saneada.Select(e => e.Nome));
        Assert.Equal([new Bridge(3, 4)], saneada[0].Bridges);
        Assert.NotNull(saneada[3].Ligado);
        Assert.Null(Ligacoes.Validar(saneada));
    }

    [Fact]
    public void Bridge_alimentado_por_canais_de_pares_diferentes_avisa_as_duas_saidas()
    {
        var lista = new[]
        {
            ComFontes(Modulo("m", "Módulo", 2, new Bridge(1, 2)), Ch(2), Ch(3)),
            new Equipamento("s", GrupoEquipamento.AltoFalante, "subwoofer", "Sub") { Ligado = LigacaoAltoFalante.NoBridge("m", new Bridge(1, 2)) },
        };

        var saidas = Ligacoes.Saidas(lista, false, false);

        const string aviso = "O bridge 1+2 do Módulo recebe o CH2 e o CH3: os dois canais precisam ser iguais.";
        Assert.Equal<string?>([null, aviso, aviso, null], saidas.Select(s => s.Aviso));
        // Mesmo com os links ligados, o CH2 e o CH3 ficam em saídas diferentes.
        Assert.Equal<string?>([aviso, aviso], Ligacoes.Saidas(lista, true, true).Select(s => s.Aviso));
    }

    [Fact]
    public void Bridge_com_sinal_num_canal_so_avisa_e_par_com_link_no_bridge_nao_avisa()
    {
        var umCanal = new[] { ComFontes(Modulo("m", "Módulo", 2, new Bridge(1, 2)), Ch(1), null) };

        Assert.Equal<string?>(["O bridge 1+2 do Módulo só recebe sinal no canal 1: ligue os dois canais do bridge.", null, null, null],
            Ligacoes.Saidas(umCanal, false, false).Select(s => s.Aviso));

        var comLink = new[] { ComFontes(Modulo("m", "Módulo", 2, new Bridge(1, 2)), Ch(1), Ch(2)) };
        Assert.Equal((true, false), Ligacoes.Links(comLink));
        Assert.All(Ligacoes.Saidas(comLink), s => Assert.Null(s.Aviso));
        Assert.Equal("CH1 + CH2", Ligacoes.Saidas(comLink)[0].Rotulo);
    }

    [Fact]
    public void Nota_e_via_longas_e_faixa_invertida_sao_recusadas()
    {
        var w = AltoFalante("w", "W", "m", 1) with { Ligado = null };
        var longo = new string('a', 121);

        Assert.Equal(Ligacoes.MsgNotaInvalida, Ligacoes.ValidarUm(w with { Nota = longo }));
        Assert.Equal(Ligacoes.MsgNotaInvalida, Ligacoes.Limpar(w with { Nota = longo }).Motivo);
        Assert.Null(Ligacoes.ValidarUm(w with { Nota = new string('a', 120) }));
        var caixa = Ativo("cx", "Caixa", "caixa ativa 2 vias", Ch(1));
        var viaLonga = caixa with { Dados = new DadosTecnicos { Vias = [new Via(longo, null)] } };
        Assert.Equal(Ligacoes.MsgViaInvalida, Ligacoes.ValidarUm(viaLonga));
        Assert.Equal(Ligacoes.MsgViaInvalida, Ligacoes.Limpar(viaLonga).Motivo);
        Assert.Equal(Ligacoes.MsgFaixaInvertida, Ligacoes.ValidarUm(w with { Dados = new DadosTecnicos { FaixaMinima = DadoTecnico.Digitado(200m), FaixaMaxima = DadoTecnico.Digitado(100m) } }));
        Assert.Equal(Ligacoes.MsgFaixaInvertida, Ligacoes.ValidarUm(w with { Dados = new DadosTecnicos { FaixaMinima = DadoTecnico.Digitado(100m), FaixaMaxima = DadoTecnico.Digitado(100m) } }));
        Assert.Null(Ligacoes.ValidarUm(w with { Dados = new DadosTecnicos { FaixaMinima = DadoTecnico.Digitado(45m), FaixaMaxima = DadoTecnico.Digitado(20000m) } }));
    }
}
