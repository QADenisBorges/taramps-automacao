using System.Diagnostics;
using BgsControle.Nucleo.Herdado;
using BgsControle.Nucleo.Setups;
using BgsControle.Telas.Esquema;

namespace BgsControle.Telas.Testes;

/// <summary>
/// O esquema do Setup (história 10b), sem MAUI: a cena (peças, formas, proporções, cabos, Y, bridges,
/// sem ligação, vazio), a projeção (ângulo e aproximação presos, toque com ângulo) e o ViewModel (toque
/// que abre a folha, destaque do canal, ao vivo) contra o processador simulado.
/// </summary>
public sealed class EsquemaTestes : IDisposable
{
    private readonly Bancada _b = new(setups: true);
    private readonly EquipamentosViewModel _lista;
    private readonly EsquemaViewModel _vm;

    public EsquemaTestes()
    {
        _lista = new EquipamentosViewModel(_b.Setups!, _b.Tela, _b.Relogio);
        _vm = new EsquemaViewModel(_lista, _b.Tela, _b.Relogio);
    }

    public void Dispose()
    {
        _vm.Dispose();
        _lista.Dispose();
        _b.ConferirSoAudio();
        Assert.Empty(_b.Tela.Erros);
    }

    private const string Casa = ConhecimentoHerdado.IdSetupCasa;

    private async Task MostrarCasa()
    {
        await _b.Setups!.Carregada;
        _lista.Mostrar(Casa);
        await _b.Tela.EsvaziarAsync();
    }

    // ----------------------------------------------------------------- montagem de cenas de teste

    private static FonteSinal Ch(int n) => FonteSinal.DoCanal(n - 1);

    private static Equipamento Modulo(string id, string nome, int canais, Func<int, FonteSinal?> fonte, params Bridge[] bridges) =>
        new(id, GrupoEquipamento.Amplificador, "módulo", nome)
        {
            Entradas = Enumerable.Range(1, canais).Select(n => new Entrada(n, fonte(n))).ToArray(),
            Bridges = bridges,
        };

    private static Equipamento AltoFalante(string id, string categoria, string nome, LigacaoAltoFalante? ligado,
        decimal? diametro = null, FuncaoAcustica? funcao = null) =>
        new(id, GrupoEquipamento.AltoFalante, categoria, nome, Funcao: funcao)
        {
            Dados = new DadosTecnicos { Diametro = DadoTecnico.Digitado(diametro) },
            Ligado = ligado,
        };

    private static Equipamento Ativo(string id, string categoria, string nome, FonteSinal? fonte, params Via[] vias) =>
        new(id, GrupoEquipamento.AmplificaETocam, categoria, nome)
        {
            Entradas = [new Entrada(1, fonte)],
            Dados = new DadosTecnicos { Vias = vias },
        };

    /// <summary>CH2 → crossover ativo → 2 módulos → alto-falantes; CH1 → caixa ativa e sub ativo (Y).</summary>
    private static IReadOnlyList<Equipamento> Grande() =>
    [
        new Equipamento("x", GrupoEquipamento.Repassa, "crossover ativo", "Crossover") { Entradas = [new Entrada(1, Ch(2))] },
        Modulo("m1", "Módulo A", 2, _ => FonteSinal.DoEquipamento("x")),
        Modulo("m2", "Módulo B", 4, n => n <= 2 ? FonteSinal.DoEquipamento("x") : Ch(3 + (n - 3)), new Bridge(3, 4)),
        AltoFalante("tw", "tweeter", "Tweeter", LigacaoAltoFalante.NoCanal("m1", 1)),
        AltoFalante("dr", "driver", "Driver", LigacaoAltoFalante.NoCanal("m1", 2)),
        AltoFalante("w12", "woofer", "Woofer 12", LigacaoAltoFalante.NoCanal("m2", 1), diametro: 12m),
        AltoFalante("w6", "woofer", "Woofer 6", LigacaoAltoFalante.NoCanal("m2", 1), diametro: 6.5m),
        AltoFalante("cx", "coaxial", "Coaxial", LigacaoAltoFalante.NoCanal("m2", 2)),
        AltoFalante("sb", "subwoofer", "Subwoofer", LigacaoAltoFalante.NoBridge("m2", new Bridge(3, 4))),
        Ativo("ca", "caixa ativa 2 vias", "Caixa ativa", Ch(1), new Via("woofer de 12\"", 12m), new Via("driver", null)),
        Ativo("sa", "sub ativo", "Sub ativo", Ch(1)),
        AltoFalante("ou", "Outra", "Shaker", LigacaoAltoFalante.NoCanal("m2", 2), funcao: FuncaoAcustica.Grave),
        new Equipamento("rec", GrupoEquipamento.SoRecebe, "gravador", "Gravador") { Entradas = [new Entrada(1, Ch(4))] },
        AltoFalante("solto", "tweeter", "Solto", null),
    ];

    // ----------------------------------------------------------------- a cena

    [Fact]
    public void Casa_2_1_processador_modulo_com_os_quatro_canais_e_as_JBL_e_o_Sub_nas_saidas_e_no_bridge()
    {
        var cena = CenaSetup.Montar(ConhecimentoHerdado.EquipamentosCasa);

        Assert.Equal(["Processador", "Módulo SD800.4 EVO", "JBL L", "JBL R", "Sub"], cena.Pecas.Select(p => p.Nome));
        var processador = cena.Processador;
        var modulo = cena.Peca(ConhecimentoHerdado.IdModuloCasa)!;
        var jblL = cena.PecaPorNome("JBL L")!;
        var jblR = cena.PecaPorNome("JBL R")!;
        var sub = cena.PecaPorNome("Sub")!;

        // Da esquerda para a direita: processador, módulo, alto-falantes (JBL L em cima, o Sub embaixo).
        Assert.True(processador.Base.U < modulo.Base.U && modulo.Base.U < jblL.Base.U);
        Assert.Equal(jblL.Base.U, sub.Base.U);
        Assert.True(jblL.Base.V < jblR.Base.V && jblR.Base.V < sub.Base.V);
        Assert.Equal(FormaPeca.BlocoComCanais, modulo.Forma);
        Assert.Equal(FormaPeca.Cone, jblL.Forma);
        Assert.Equal(FormaPeca.CaixaComCone, sub.Forma);

        // O módulo: 4 entradas na cor do CH1 (azul) ao CH4 (menta), cada uma com o cabo do seu canal.
        Assert.Equal([0, 1, 2, 3], cena.Portas.Where(p => p.IdPeca == modulo.Id && p.Tipo == TipoPorta.Entrada).Select(p => p.Canal));
        Assert.Equal([0, 1, 2, 3], cena.CabosAte(modulo.Id).Select(c => c.Canal));
        Assert.All(cena.CabosDe(CenaSetup.IdProcessador), c => Assert.Equal(c.Pontos[0], cena.SaidaProcessador(c.Canal!.Value).Posicao));
        Assert.Empty(cena.Juncoes);

        // As JBL nos canais 1 e 2; o Sub no bridge 3+4, que é uma chave juntando as saídas 3 e 4.
        Assert.Equal(0, cena.CabosAte(jblL.Id).Single().Canal);
        Assert.Equal(1, cena.CabosAte(jblR.Id).Single().Canal);
        var bridge = Assert.Single(cena.Bridges);
        Assert.Equal(new Bridge(3, 4), bridge.Bridge);
        Assert.Equal(new HashSet<int> { 2, 3 }, bridge.Canais);
        Assert.Equal(2, bridge.Canal);
        var saida3 = cena.Portas.Single(p => p.IdPeca == modulo.Id && p.Tipo == TipoPorta.Saida && p.Numero == 3).Posicao;
        var saida4 = cena.Portas.Single(p => p.IdPeca == modulo.Id && p.Tipo == TipoPorta.Saida && p.Numero == 4).Posicao;
        Assert.Equal(saida3, bridge.Chave[0]);
        Assert.Equal(saida4, bridge.Chave[^1]);
        var caboSub = cena.CabosAte(sub.Id).Single();
        Assert.Equal(bridge.Saida, caboSub.Pontos[0]);
        Assert.Equal(2, caboSub.Canal);

        // Rótulos: só quem não tem modelo é "genérico"; desde a 8f, o módulo e o Sub têm modelo.
        Assert.Null(sub.Detalhe);
        Assert.Null(modulo.Detalhe);
        Assert.Null(jblL.Detalhe);
        Assert.False(cena.Vazia);
    }

    [Fact]
    public void Cabo_Y_um_canal_para_duas_entradas_abre_em_Y_com_um_ponto_na_cor_do_canal()
    {
        var cena = CenaSetup.Montar([Ativo("ca", "caixa ativa full-range", "Caixa ativa", Ch(1)), Ativo("sa", "sub ativo", "Sub ativo", Ch(1))]);

        var cabos = cena.CabosDe(CenaSetup.IdProcessador).ToArray();
        Assert.Equal(["ca", "sa"], cabos.Select(c => c.IdDestino));
        Assert.All(cabos, c => Assert.Equal(0, c.Canal)); // azul
        var y = Assert.Single(cena.Juncoes);
        Assert.Equal(0, y.Canal);
        // O ponto do Y fica no caminho dos dois cabos.
        Assert.All(cabos, c => Assert.Contains(c.Pontos.Zip(c.Pontos.Skip(1)), t => NoTrecho(y.Posicao, t.First, t.Second)));
        Assert.Equal(FormaPeca.CaixaComVias, cena.Peca("ca")!.Forma);
        Assert.Equal(FormaPeca.CaixaComCone, cena.Peca("sa")!.Forma);
    }

    private static bool NoTrecho(Ponto3 p, Ponto3 a, Ponto3 b)
    {
        var dentroU = p.U >= Math.Min(a.U, b.U) - 1e-6 && p.U <= Math.Max(a.U, b.U) + 1e-6;
        var dentroV = p.V >= Math.Min(a.V, b.V) - 1e-6 && p.V <= Math.Max(a.V, b.V) + 1e-6;
        var cruz = (b.U - a.U) * (p.V - a.V) - (b.V - a.V) * (p.U - a.U);
        return dentroU && dentroV && Math.Abs(cruz) < 1e-6;
    }

    [Fact]
    public void Repasse_o_crossover_fica_na_coluna_do_grupo_3_e_os_cabos_dele_aos_modulos_sao_lilas()
    {
        var cena = CenaSetup.Montar(Grande());
        var crossover = cena.Peca("x")!;
        var moduloA = cena.Peca("m1")!;
        var moduloB = cena.Peca("m2")!;

        Assert.Equal(1, crossover.Coluna);
        Assert.Equal(2, moduloA.Coluna);
        Assert.Equal(2, moduloB.Coluna);
        Assert.Equal(3, cena.Peca("tw")!.Coluna);
        Assert.True(cena.Processador.Base.U < crossover.Base.U && crossover.Base.U < moduloA.Base.U);
        Assert.Equal(FormaPeca.BlocoBaixo, crossover.Forma);
        Assert.Equal("crossover ativo", crossover.TextoBloco);

        // Do CH2 ao crossover, e do crossover às 4 entradas dos módulos (2 do A, 2 do B), em lilás, com Y.
        Assert.Equal(1, cena.CabosAte("x").Single().Canal);
        var doCrossover = cena.CabosDe("x").ToArray();
        Assert.Equal(4, doCrossover.Length);
        Assert.All(doCrossover, c => Assert.Equal(1, c.Canal));
        Assert.Contains(cena.Juncoes, j => j.IdOrigem == "x" && j.Canal == 1);
        // O CH3 e o CH4 entram nos canais 3 e 4 do módulo B, que estão em bridge até o Subwoofer.
        Assert.Equal([1, 1, 2, 3], cena.Portas.Where(p => p.IdPeca == "m2" && p.Tipo == TipoPorta.Entrada).Select(p => p.Canal));
        Assert.Equal(2, cena.CabosAte("sb").Single().Canal);
        // O gravador (grupo 4) fica com os grupos 1 e 2.
        Assert.Equal(moduloA.Base.U, cena.Peca("rec")!.Base.U);
        Assert.Equal(FormaPeca.BlocoBaixo, cena.Peca("rec")!.Forma);
    }

    [Fact]
    public void Formas_por_categoria_e_pela_funcao_e_o_cone_pelo_diametro()
    {
        var cena = CenaSetup.Montar(Grande());

        Assert.Equal(FormaPeca.Domo, cena.Peca("tw")!.Forma);
        Assert.Equal(FormaPeca.Corneta, cena.Peca("dr")!.Forma);
        Assert.Equal(FormaPeca.Cone, cena.Peca("w12")!.Forma);
        Assert.Equal(FormaPeca.Coaxial, cena.Peca("cx")!.Forma);
        Assert.Equal(FormaPeca.CaixaComCone, cena.Peca("sb")!.Forma);
        Assert.Equal(FormaPeca.CaixaComVias, cena.Peca("ca")!.Forma);
        Assert.Equal(FormaPeca.Cone, cena.Peca("ou")!.Forma); // "Outra" de função grave
        Assert.Equal(FormaPeca.BlocoComCanais, cena.Peca("m2")!.Forma);
        Assert.Equal(FormaPeca.Processador, cena.Processador.Forma);

        // O woofer de 12" é maior que o de 6,5"; o diâmetro fica numa faixa legível.
        Assert.True(cena.Peca("w12")!.Raio > cena.Peca("w6")!.Raio);
        Assert.Equal(12 * CenaSetup.RaioPorPolegada, cena.Peca("w12")!.Raio, 6);
        var grande = CenaSetup.Montar([AltoFalante("g", "woofer", "W", null, diametro: 40m), AltoFalante("p", "woofer", "P", null, diametro: 0.5m)]);
        Assert.Equal(CenaSetup.RaioConeMaximo, grande.Peca("g")!.Raio);
        Assert.Equal(CenaSetup.RaioConeMinimo, grande.Peca("p")!.Raio);
        // Sem diâmetro, um tamanho médio pela categoria (o woofer maior que o médio).
        var semDiametro = CenaSetup.Montar([AltoFalante("w", "woofer", "W", null), AltoFalante("m", "médio", "M", null)]);
        Assert.True(semDiametro.Peca("w")!.Raio > semDiametro.Peca("m")!.Raio);

        // A caixa ativa com 2 vias: as 2 no topo, a do woofer maior; sem vias, uma só.
        var vias = cena.Peca("ca")!.Vias;
        Assert.Equal(2, vias.Count);
        Assert.True(vias[0].Raio > vias[1].Raio);
        Assert.Single(CenaSetup.Montar([Ativo("c", "monitor ativo", "Monitor", Ch(1))]).Peca("c")!.Vias);

        // Categoria desconhecida: a forma da função (sub → caixa com cone, agudo → domo, full-range → coaxial, outra → caixa).
        Assert.Equal(FormaPeca.CaixaComCone, CenaSetup.FormaDe(AltoFalante("a", "shaker", "S", null), FuncaoAcustica.Sub));
        Assert.Equal(FormaPeca.Domo, CenaSetup.FormaDe(AltoFalante("a", "piezo", "S", null), FuncaoAcustica.Agudo));
        Assert.Equal(FormaPeca.Coaxial, CenaSetup.FormaDe(AltoFalante("a", "x", "S", null), FuncaoAcustica.FullRange));
        Assert.Equal(FormaPeca.Caixa, CenaSetup.FormaDe(AltoFalante("a", "x", "S", null), FuncaoAcustica.Outra));
        Assert.Equal(FormaPeca.Cone, CenaSetup.FormaDe(AltoFalante("a", "x", "S", null), FuncaoAcustica.MedioGrave));
    }

    [Fact]
    public void Sem_ligacao_fica_a_parte_embaixo_com_sem_ligacao_e_a_funcao_so_e_pedida_para_categoria_desconhecida()
    {
        var pedidas = new List<string>();
        var cena = CenaSetup.Montar(Grande(), e =>
        {
            pedidas.Add(e.Id);
            return e.Funcao;
        });

        var solto = cena.Peca("solto")!;
        Assert.True(solto.SemLigacao);
        Assert.Equal("genérico · sem ligação", solto.Detalhe);
        Assert.True(cena.Pecas.Where(p => !p.SemLigacao).All(p => p.Base.V < solto.Base.V));
        Assert.Empty(cena.CabosAte("solto"));
        Assert.Null(cena.Portas.Single(p => p.IdPeca == "solto").Canal);
        Assert.Equal(["ou"], pedidas);
        Assert.DoesNotContain(cena.Pecas, p => p.Id != "solto" && p.SemLigacao);
    }

    [Fact]
    public void Setup_vazio_so_o_processador()
    {
        var cena = CenaSetup.Montar([]);

        Assert.True(cena.Vazia);
        Assert.Equal([CenaSetup.IdProcessador], cena.Pecas.Select(p => p.Id));
        Assert.Empty(cena.Cabos);
        Assert.Equal(4, cena.Portas.Count);
        Assert.Equal(AlvoToque.Vazio, new ProjecaoIsometrica(cena, 360, 280, 0, 1).Tocar(5, 5));
    }

    [Fact]
    public void Montar_com_20_equipamentos_e_linear_e_rapido()
    {
        var lista = new List<Equipamento>();
        for (var m = 0; m < 4; m++)
        {
            lista.Add(Modulo($"m{m}", $"Módulo {m}", 4, n => Ch(n), new Bridge(3, 4)));
            lista.Add(AltoFalante($"a{m}", "woofer", $"W{m}a", LigacaoAltoFalante.NoCanal($"m{m}", 1)));
            lista.Add(AltoFalante($"b{m}", "tweeter", $"T{m}b", LigacaoAltoFalante.NoCanal($"m{m}", 2)));
            lista.Add(AltoFalante($"c{m}", "subwoofer", $"S{m}c", LigacaoAltoFalante.NoBridge($"m{m}", new Bridge(3, 4))));
            lista.Add(Ativo($"d{m}", "monitor ativo", $"Monitor {m}", Ch(1 + m)));
        }
        Assert.Equal(20, lista.Count);

        CenaSetup.Montar(lista); // aquece
        var relogio = Stopwatch.StartNew();
        CenaSetup? cena = null;
        for (var i = 0; i < 20; i++)
            cena = CenaSetup.Montar(lista);
        var projecao = new ProjecaoIsometrica(cena!, 360, 280, 30, 1.4);
        foreach (var p in cena!.Pecas.Where(p => p.Equipamento))
            Assert.Equal(p.Id, projecao.Tocar(projecao.Projetar(p.Centro).X, projecao.Projetar(p.Centro).Y).IdPeca);
        relogio.Stop();

        Assert.Equal(21, cena.Pecas.Count);
        Assert.Equal(16 + 4 + 12, cena.Cabos.Count); // 4 canais × 4 módulos, 4 monitores, 12 alto-falantes
        Assert.True(relogio.ElapsedMilliseconds < 1000, $"{relogio.ElapsedMilliseconds} ms");
    }

    // ----------------------------------------------------------------- projeção e toque

    [Fact]
    public void Em_qualquer_angulo_e_aproximacao_o_centro_projetado_de_cada_peca_acerta_a_peca()
    {
        foreach (var cena in new[] { CenaSetup.Montar(ConhecimentoHerdado.EquipamentosCasa), CenaSetup.Montar(Grande()) })
        {
            for (var angulo = -45.0; angulo <= 45.0; angulo += 5)
            {
                foreach (var aproximacao in new[] { 0.6, 1.0, 1.7, 2.5 })
                {
                    var projecao = new ProjecaoIsometrica(cena, 360, 280, angulo, aproximacao);
                    foreach (var peca in cena.Pecas.Where(p => p.Equipamento))
                    {
                        var c = projecao.Projetar(peca.Centro);
                        Assert.True(projecao.Tocar(c.X, c.Y).IdPeca == peca.Id, $"{peca.Nome} a {angulo}° e {aproximacao}×");
                    }
                    for (var canal = 0; canal < 4; canal++)
                    {
                        var c = projecao.Projetar(cena.SaidaProcessador(canal).Posicao);
                        Assert.True(projecao.Tocar(c.X, c.Y).Canal == canal, $"CH{canal + 1} a {angulo}° e {aproximacao}×");
                    }
                }
            }
        }
    }

    [Fact]
    public void A_1x_a_cena_inteira_cabe_na_area_em_qualquer_angulo()
    {
        var cena = CenaSetup.Montar(Grande());
        foreach (var angulo in new[] { -45.0, -20, 0, 30, 45 })
        {
            var projecao = new ProjecaoIsometrica(cena, 360, 280, angulo, 1);
            foreach (var p in cena.Pecas.SelectMany(p => projecao.Silhueta(p)))
            {
                Assert.InRange(p.X, ProjecaoIsometrica.MargemLado - 0.5, 360 - ProjecaoIsometrica.MargemLado + 0.5);
                Assert.InRange(p.Y, ProjecaoIsometrica.MargemTopo - 0.5, 280 - ProjecaoIsometrica.MargemBaixo + 0.5);
            }
        }
        // Sem tamanho ainda, vale a área padrão.
        Assert.Equal(new ProjecaoIsometrica(cena, 360, 280, 0, 1).Escala, new ProjecaoIsometrica(cena, 0, double.NaN, 0, 1).Escala);
    }

    [Fact]
    public void A_area_tem_280_de_altura_minima_e_um_Setup_com_muitas_fileiras_ganha_altura()
    {
        var casa = CenaSetup.Montar(ConhecimentoHerdado.EquipamentosCasa);
        var grande = CenaSetup.Montar(Grande());

        Assert.Equal(280, ProjecaoIsometrica.AlturaDesejada(casa, 328));
        Assert.Equal(280, ProjecaoIsometrica.AlturaDesejada(CenaSetup.Montar([]), 328));
        var alta = ProjecaoIsometrica.AlturaDesejada(grande, 328);
        Assert.InRange(alta, 300, ProjecaoIsometrica.AlturaMaxima);
        // Com essa altura, a cena cabe pela largura: a escala não fica menor que na área de 280.
        Assert.True(new ProjecaoIsometrica(grande, 328, alta, 0, 1).Escala > new ProjecaoIsometrica(grande, 328, 280, 0, 1).Escala);
    }

    // ----------------------------------------------------------------- o ViewModel

    [Fact]
    public async Task Arrastar_e_pincar_ficam_presos_e_o_toque_continua_acertando()
    {
        await MostrarCasa();

        _vm.Arrastar(200);
        _vm.Pincar(3);

        Assert.Equal(45, _vm.Angulo);
        Assert.Equal(2.5, _vm.Aproximacao);
        Assert.Equal(45, _vm.Projecao.Angulo);
        Assert.Equal(_vm.Projecao.EscalaBase * 2.5, _vm.Projecao.Escala, 9);
        _vm.Arrastar(-1000);
        _vm.Pincar(0.01);
        Assert.Equal(-45, _vm.Angulo);
        Assert.Equal(0.6, _vm.Aproximacao);
        _vm.Pincar(double.NaN);
        _vm.Pincar(-1);
        Assert.Equal(0.6, _vm.Aproximacao);

        _vm.Arrastar(250); // de −45° a +55° → preso em +45°; soltar não volta
        _vm.Pincar(3);
        _vm.Tamanho(392, 280);
        foreach (var peca in _vm.Cena.Pecas.Where(p => p.Equipamento))
        {
            var c = _vm.Projecao.Projetar(peca.Centro);
            Assert.Equal(peca.Id, _vm.Projecao.Tocar(c.X, c.Y).IdPeca);
        }
        Assert.Equal(45, _vm.Angulo);
    }

    [Fact]
    public async Task Toque_no_Sub_abre_a_folha_do_Sub_e_toque_no_vazio_nao_abre_nada()
    {
        await MostrarCasa();
        _vm.Tamanho(360, 280);

        var c = _vm.Projecao.Projetar(_vm.Cena.PecaPorNome("Sub")!.Centro);
        _vm.Tocar(c.X, c.Y);

        Assert.True(_lista.FolhaAberta);
        Assert.Equal("Sub", _lista.TituloFolha);
        Assert.Equal(ConhecimentoHerdado.IdSubCasa, _vm.Selecionada);
        await _lista.Cancelar.ExecutarAsync();

        _vm.Tocar(2, 2);
        Assert.False(_lista.FolhaAberta);
        Assert.Null(_vm.Selecionada);
    }

    [Fact]
    public async Task Toque_no_CH3_destaca_o_modulo_o_bridge_e_o_Sub_e_o_resto_fica_a_35_por_cento()
    {
        await MostrarCasa();
        var cena = _vm.Cena;
        var ch3 = _vm.Projecao.Projetar(cena.SaidaProcessador(2).Posicao);

        _vm.Tocar(ch3.X, ch3.Y);

        Assert.Equal(2, _vm.Destaque);
        Assert.False(_lista.FolhaAberta);
        double Op(string nome) => _vm.Opacidade(cena.PecaPorNome(nome)!);
        Assert.Equal(1, Op("Módulo SD800.4 EVO"));
        Assert.Equal(1, Op("Sub"));
        Assert.Equal(1, Op("Processador"));
        Assert.Equal(EsquemaViewModel.OpacidadeApagada, Op("JBL L"));
        Assert.Equal(EsquemaViewModel.OpacidadeApagada, Op("JBL R"));
        Assert.Equal(1, _vm.Opacidade(cena.Bridges.Single()));
        Assert.Equal(1, _vm.Opacidade(cena.CabosAte(ConhecimentoHerdado.IdSubCasa).Single()));
        Assert.Equal(EsquemaViewModel.OpacidadeApagada, _vm.Opacidade(cena.CabosAte(ConhecimentoHerdado.IdJblL).Single()));
        Assert.Equal([2], cena.CabosAte(ConhecimentoHerdado.IdModuloCasa).Where(c => _vm.Opacidade(c) == 1).Select(c => c.Canal!.Value));
        Assert.Equal(1, _vm.Opacidade(cena.SaidaProcessador(2)));
        Assert.Equal(EsquemaViewModel.OpacidadeApagada, _vm.Opacidade(cena.SaidaProcessador(0)));

        // De novo no CH3: sem destaque.
        _vm.Tocar(ch3.X, ch3.Y);
        Assert.Null(_vm.Destaque);
        Assert.Equal(1, Op("JBL L"));

        // No vazio também tira.
        _vm.Tocar(ch3.X, ch3.Y);
        Assert.Equal(2, _vm.Destaque);
        _vm.Tocar(_vm.Projecao.Largura - 2, 2);
        Assert.Null(_vm.Destaque);
    }

    [Fact]
    public async Task Ao_vivo_tirar_a_JBL_R_pela_folha_refaz_a_cena_e_o_angulo_a_aproximacao_e_o_destaque_ficam()
    {
        await MostrarCasa();
        _vm.Arrastar(50);
        _vm.Pincar(1.5);
        var ch1 = _vm.Projecao.Projetar(_vm.Cena.SaidaProcessador(0).Posicao);
        _vm.Tocar(ch1.X, ch1.Y);
        Assert.Equal(0, _vm.Destaque);
        var versao = _vm.Versao;

        _lista.AbrirEditar(ConhecimentoHerdado.IdJblR);
        await _lista.Tirar.ExecutarAsync();
        await _b.Rodar(_lista.ConfirmarTirar.ExecutarAsync());
        await _b.Tela.EsvaziarAsync();

        Assert.Null(_vm.Cena.PecaPorNome("JBL R"));
        Assert.Equal(["Processador", "Módulo SD800.4 EVO", "JBL L", "Sub"], _vm.Cena.Pecas.Select(p => p.Nome));
        Assert.True(_vm.Versao > versao);
        Assert.Equal(20, _vm.Angulo, 9);
        Assert.Equal(1.5, _vm.Aproximacao, 9);
        Assert.Equal(0, _vm.Destaque);
        Assert.Equal(_vm.Angulo, _vm.Projecao.Angulo);
    }

    [Fact]
    public async Task Ao_vivo_um_equipamento_novo_entra_na_hora_e_a_peca_tocada_que_some_perde_o_contorno()
    {
        await MostrarCasa();
        var r = await _b.Setups!.AdicionarEquipamentoAsync(Casa, AltoFalante("x", "tweeter", "Tweeter solto", null));
        Assert.True(r.Ok, r.Motivo);
        await _b.Tela.EsvaziarAsync();

        var solto = _vm.Cena.PecaPorNome("Tweeter solto")!;
        Assert.True(solto.SemLigacao);
        Assert.Equal(EsquemaViewModel.OpacidadeSemLigacao, _vm.Opacidade(solto));
        var c = _vm.Projecao.Projetar(solto.Centro);
        _vm.Tocar(c.X, c.Y);
        Assert.Equal(solto.Id, _vm.Selecionada);
        Assert.Equal("Tweeter solto", _lista.TituloFolha);
        await _lista.Cancelar.ExecutarAsync();

        Assert.True((await _b.Setups.TirarEquipamentoAsync(Casa, solto.Id)).Ok);
        await _b.Tela.EsvaziarAsync();
        Assert.Null(_vm.Cena.Peca(solto.Id));
        Assert.Null(_vm.Selecionada);
    }

    [Fact]
    public async Task Setup_sem_equipamentos_mostra_so_o_processador_e_a_frase()
    {
        await MostrarCasa();
        foreach (var id in new[] { ConhecimentoHerdado.IdJblL, ConhecimentoHerdado.IdJblR, ConhecimentoHerdado.IdSubCasa, ConhecimentoHerdado.IdModuloCasa })
            Assert.True((await _b.Setups!.TirarEquipamentoAsync(Casa, id)).Ok);
        await _b.Tela.EsvaziarAsync();

        Assert.True(_vm.Cena.Vazia);
        Assert.Equal("Adicione os equipamentos ligados em cada canal.", _vm.TextoVazio);
        Assert.Equal(EquipamentosViewModel.TextoVazioSetup, _lista.TextoVazio);
    }

    [Fact]
    public async Task Sem_Setup_mostrado_o_esquema_e_so_o_processador_com_o_pedido_de_escolher()
    {
        await _b.Setups!.Carregada;
        _lista.Mostrar("nao-existe");
        await _b.Tela.EsvaziarAsync();

        Assert.True(_vm.Cena.Vazia);
        Assert.Equal(EquipamentosViewModel.MsgSemSetup, _vm.TextoVazio);
        _vm.Tocar(100, 100);
        Assert.False(_lista.FolhaAberta);
    }

    // ----------------------------------------------------------------- correções da revisão

    [Fact]
    public void A_mais_e_menos_45_graus_as_portas_e_os_rotulos_CH_continuam_aparecendo()
    {
        var cena = CenaSetup.Montar(ConhecimentoHerdado.EquipamentosCasa);
        foreach (var angulo in new[] { -45.0, 45.0 })
        {
            var projecao = new ProjecaoIsometrica(cena, 360, 280, angulo, 1);
            foreach (var peca in cena.Pecas)
            {
                foreach (var porta in cena.PortasDe(peca.Id).Where(p => p.Tipo is TipoPorta.Processador or TipoPorta.Entrada))
                    Assert.True(projecao.PortaAparece(peca, porta), $"{peca.Nome} {porta.Tipo} {porta.Numero} a {angulo}°");
            }
        }
    }

    [Fact]
    public void Sem_ligacao_e_a_mesma_regra_da_lista_e_alto_falante_sem_canal_nem_bridge_nao_quebra()
    {
        var listas = new List<IReadOnlyList<Equipamento>>
        {
            Grande(),
            ConhecimentoHerdado.EquipamentosCasa,
            new Equipamento[]
            {
                // Módulo sem entrada mas com alto-falante; crossover sem entrada que alimenta um módulo.
                Modulo("m", "Módulo", 2, _ => null),
                AltoFalante("a", "woofer", "W", LigacaoAltoFalante.NoCanal("m", 1)),
                new Equipamento("x", GrupoEquipamento.Repassa, "crossover ativo", "X") { Entradas = [new Entrada(1, null)] },
                Modulo("m2", "Módulo 2", 2, _ => FonteSinal.DoEquipamento("x")),
                AltoFalante("b", "woofer", "B", new LigacaoAltoFalante("m2", null, null)),
                Modulo("m3", "Módulo 3", 2, n => n == 1 ? Ch(1) : null),
                AltoFalante("c", "woofer", "C", LigacaoAltoFalante.NoCanal("m3", 2)),
            },
        };
        foreach (var lista in listas)
        {
            var cena = CenaSetup.Montar(lista);
            foreach (var e in lista)
                Assert.True(Ligacoes.CanaisQueAlimentam(lista, e.Id).Count == 0 == cena.Peca(e.Id)!.SemLigacao, e.Nome);
        }
        var terceira = CenaSetup.Montar(listas[2]);
        Assert.True(terceira.Peca("m")!.SemLigacao);
        Assert.True(terceira.Peca("x")!.SemLigacao);
        Assert.True(terceira.Peca("b")!.SemLigacao);
        Assert.False(terceira.Peca("m3")!.SemLigacao);
        Assert.True(terceira.Peca("c")!.SemLigacao); // no canal 2 do módulo, que não recebe sinal
        Assert.Empty(terceira.CabosAte("c"));
    }

    [Fact]
    public void A_fileira_dos_sem_ligacao_quebra_e_as_vias_cabem_na_caixa_e_os_barramentos_do_grupo_3_nao_se_sobrepoem()
    {
        var soltos = CenaSetup.Montar(Enumerable.Range(0, 8).Select(i => AltoFalante($"t{i}", "tweeter", $"T{i}", null)).ToArray());
        Assert.True(soltos.Pecas.Where(p => p.SemLigacao).Select(p => p.Base.V).Distinct().Count() > 1);
        Assert.True(soltos.Pecas.Where(p => p.SemLigacao).GroupBy(p => p.Base.V).All(g => g.Count() <= 3));

        var caixa = CenaSetup.Montar([Ativo("c", "caixa ativa 3 vias", "C", Ch(1),
            new Via("woofer", 15m), new Via("woofer", 15m), new Via("médio", 15m), new Via("médio", 15m))]).Peca("c")!;
        Assert.Equal(4, caixa.Vias.Count);
        foreach (var via in caixa.Vias)
        {
            var d = via.Centro - caixa.Base;
            var aoLongoDeA = d.U * CenaSetup.EixoA.U + d.V * CenaSetup.EixoA.V;
            Assert.True(Math.Abs(aoLongoDeA) + via.Raio <= caixa.MeiaA + 1e-9, $"{aoLongoDeA} + {via.Raio} > {caixa.MeiaA}");
        }

        var g3 = CenaSetup.Montar(
        [
            new Equipamento("x1", GrupoEquipamento.Repassa, "crossover ativo", "X1") { Entradas = [new Entrada(1, Ch(1))] },
            new Equipamento("x2", GrupoEquipamento.Repassa, "crossover ativo", "X2") { Entradas = [new Entrada(1, Ch(2))] },
            Modulo("m1", "M1", 1, _ => FonteSinal.DoEquipamento("x1")),
            Modulo("m2", "M2", 1, _ => FonteSinal.DoEquipamento("x2")),
            Modulo("m3", "M3", 1, _ => FonteSinal.DoEquipamento("x1")),
        ]);
        Assert.Equal(g3.Peca("x1")!.Coluna, g3.Peca("x2")!.Coluna);
        var barramentos = new[] { "x1", "x2" }.Select(id => g3.CabosDe(id).Where(c => c.Pontos.Count > 2).Select(c => c.Pontos[1].U).Distinct().Single()).ToArray();
        Assert.NotEqual(barramentos[0], barramentos[1]);
    }

    [Fact]
    public void O_alto_do_domo_do_tweeter_e_do_coaxial_acerta_a_peca()
    {
        var cena = CenaSetup.Montar([AltoFalante("tw", "tweeter", "T", null), AltoFalante("cx", "coaxial", "C", null)]);
        foreach (var angulo in new[] { -45.0, 0, 45 })
        {
            var projecao = new ProjecaoIsometrica(cena, 360, 280, angulo, 2.5);
            foreach (var peca in cena.Pecas.Where(p => p.Equipamento))
            {
                var alto = projecao.Projetar(peca.Base.NaAltura(peca.AlturaDoDomo!.Value));
                Assert.True(projecao.Silhueta(peca).Min(p => p.Y) <= alto.Y + 1e-9);
                Assert.Equal(peca.Id, projecao.Tocar(alto.X, alto.Y + 0.5).IdPeca);
            }
        }
    }

    private static PecaCena Bloco(string id, double u, double v) =>
        new(id, id, FormaPeca.Caixa, new Ponto3(u, v), 30, 30, 30, 0) { Grupo = GrupoEquipamento.AmplificaETocam };

    [Fact]
    public void Pecas_sobrepostas_a_da_frente_ganha_o_toque_e_na_mesma_profundidade_vale_a_ordem_da_pintura()
    {
        // A e B na mesma profundidade, lado a lado e sobrepostas.
        var cena = new CenaSetup([Bloco("a", 0, 0), Bloco("b", 40, 0)], [], [], [], []);
        var projecao = new ProjecaoIsometrica(cena, 360, 280, 0, 1);
        var ordem = projecao.OrdemDePintura().Select(p => p.Id).ToArray();
        Assert.True(Array.IndexOf(ordem, "b") > Array.IndexOf(ordem, "a"));

        // No meio de A e B (as duas silhuetas têm o ponto), ganha quem é pintada por cima.
        var meio = projecao.Projetar(new Ponto3(20, 0, 30));
        Assert.True(ProjecaoIsometrica.Dentro(projecao.Silhueta(cena.Peca("a")!), meio));
        Assert.True(ProjecaoIsometrica.Dentro(projecao.Silhueta(cena.Peca("b")!), meio));
        Assert.Equal("b", projecao.Tocar(meio.X, meio.Y).IdPeca);

        // C na frente de A, sobrepostas: a da frente (C) ganha.
        var outra = new CenaSetup([Bloco("a", 0, 0), Bloco("c", 0, 30)], [], [], [], []);
        var vista = new ProjecaoIsometrica(outra, 360, 280, 0, 1);
        var frente = vista.Projetar(new Ponto3(0, 15, 15));
        Assert.True(ProjecaoIsometrica.Dentro(vista.Silhueta(outra.Peca("a")!), frente));
        Assert.True(ProjecaoIsometrica.Dentro(vista.Silhueta(outra.Peca("c")!), frente));
        Assert.Equal("c", vista.Tocar(frente.X, frente.Y).IdPeca);
    }

    [Fact]
    public async Task Pincar_aproxima_em_volta_dos_dedos_a_vista_fica_presa_na_cena_e_o_toque_acerta()
    {
        await MostrarCasa();
        _vm.Tamanho(360, 280);
        var ch1 = _vm.Projecao.Projetar(_vm.Cena.SaidaProcessador(0).Posicao);

        // Pinçando sobre o CH1, o CH1 fica embaixo dos dedos (ou o mais perto que a borda deixa).
        _vm.Pincar(2.5, ch1.X, ch1.Y);
        Assert.Equal(2.5, _vm.Aproximacao);
        Assert.NotEqual(0, _vm.Projecao.DesvioX);
        var depois = _vm.Projecao.Projetar(_vm.Cena.SaidaProcessador(0).Posicao);
        Assert.InRange(depois.X, 0, 360);
        Assert.InRange(depois.Y, 0, 280);
        Assert.Equal(0, _vm.Projecao.Tocar(depois.X, depois.Y).Canal);
        foreach (var peca in _vm.Cena.Pecas.Where(p => p.Equipamento))
        {
            var c = _vm.Projecao.Projetar(peca.Centro);
            Assert.Equal(peca.Id, _vm.Projecao.Tocar(c.X, c.Y).IdPeca);
        }

        // Pinçar num canto longe não leva a cena para fora: o desvio fica preso.
        _vm.Pincar(1.01, -5000, -5000);
        var limites = _vm.Cena.Pecas.SelectMany(p => _vm.Projecao.Silhueta(p)).ToArray();
        Assert.True(limites.Max(p => p.X) >= 360 - ProjecaoIsometrica.MargemLado - 1);
        Assert.True(limites.Min(p => p.X) <= ProjecaoIsometrica.MargemLado + 1);

        // De volta a 1×, a vista volta ao meio.
        _vm.Pincar(0.01, 10, 10);
        Assert.Equal(0.6, _vm.Aproximacao);
        Assert.Equal(0, _vm.Projecao.DesvioX);
        Assert.Equal(0, _vm.Projecao.DesvioY);
    }

    [Fact]
    public async Task Outro_Setup_volta_a_vista_ao_comeco_e_fechar_a_folha_tira_o_contorno()
    {
        await MostrarCasa();
        var sub = _vm.Projecao.Projetar(_vm.Cena.PecaPorNome("Sub")!.Centro);
        _vm.Tocar(sub.X, sub.Y);
        Assert.NotNull(_vm.Selecionada);
        await _lista.Cancelar.ExecutarAsync();
        Assert.Null(_vm.Selecionada);

        _vm.Arrastar(60);
        _vm.Pincar(2);
        var ch3 = _vm.Projecao.Projetar(_vm.Cena.SaidaProcessador(2).Posicao);
        _vm.Tocar(ch3.X, ch3.Y);
        Assert.Equal(2, _vm.Destaque);
        sub = _vm.Projecao.Projetar(_vm.Cena.PecaPorNome("Sub")!.Centro);
        _vm.Tocar(sub.X, sub.Y);
        Assert.NotNull(_vm.Selecionada);

        _lista.Mostrar("outro-setup");
        await _b.Tela.EsvaziarAsync();
        Assert.Equal(0, _vm.Angulo);
        Assert.Equal(1, _vm.Aproximacao);
        Assert.Null(_vm.Destaque);
        Assert.Null(_vm.Selecionada);
        Assert.True(_vm.Cena.Vazia);

        _vm.Arrastar(30);
        _lista.Mostrar(Casa);
        await _b.Tela.EsvaziarAsync();
        Assert.Equal(0, _vm.Angulo);
        Assert.False(_vm.Cena.Vazia);
    }

    [Fact]
    public async Task Com_um_canal_destacado_as_bordas_das_entradas_dos_outros_canais_apagam()
    {
        await MostrarCasa();
        var ch3 = _vm.Projecao.Projetar(_vm.Cena.SaidaProcessador(2).Posicao);
        _vm.Tocar(ch3.X, ch3.Y);

        var entradas = _vm.Cena.PortasDe(ConhecimentoHerdado.IdModuloCasa).Where(p => p.Tipo == TipoPorta.Entrada).ToArray();
        Assert.Equal([EsquemaViewModel.OpacidadeApagada, EsquemaViewModel.OpacidadeApagada, 1, EsquemaViewModel.OpacidadeApagada],
            entradas.Select(_vm.Opacidade));
        Assert.Equal(1, _vm.Opacidade(_vm.Cena.PortasDe(ConhecimentoHerdado.IdSubCasa).Single()));
        Assert.Equal(EsquemaViewModel.OpacidadeApagada, _vm.Opacidade(_vm.Cena.PortasDe(ConhecimentoHerdado.IdJblL).Single()));
    }

    [Fact]
    public async Task Shaker_sem_funcao_escolhida_pelo_ViewModel_tem_a_forma_da_funcao_sugerida()
    {
        await MostrarCasa();
        var r = await _b.Setups!.AdicionarEquipamentoAsync(Casa,
            AltoFalante("x", "shaker", "Shaker", LigacaoAltoFalante.NoBridge(ConhecimentoHerdado.IdModuloCasa, new Bridge(3, 4))));
        Assert.True(r.Ok, r.Motivo);
        await _b.Tela.EsvaziarAsync();

        var setup = _b.Setups.Setup(Casa)!;
        var shaker = setup.Equipamentos.Single(e => e.Nome == "Shaker");
        Assert.Null(shaker.Funcao);
        var sugerida = _b.Setups.FuncaoSugerida(Casa, shaker);
        Assert.Equal(FuncaoAcustica.Sub, sugerida); // o passa-baixa do CH3 em 150 Hz
        var forma = _vm.Cena.PecaPorNome("Shaker")!.Forma;
        Assert.Equal(CenaSetup.FormaDe(shaker, sugerida), forma);
        Assert.Equal(FormaPeca.CaixaComCone, forma);
        Assert.NotEqual(FormaPeca.Caixa, forma);
    }
}
