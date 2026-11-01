using BgsControle.Nucleo.Escrita;
using BgsControle.Nucleo.Setups;
using BgsControle.Simulador;

namespace BgsControle.Nucleo.Testes;

/// <summary>
/// Histórias 8e e 8f no gerente, contra o processador simulado: as saídas do Setup em uso (com o link
/// tirado das ligações), as operações de equipamento (sem BLE), a sugestão de função pelo corte da
/// memória ativa e os equipamentos no <c>setups.json</c>.
/// </summary>
public sealed partial class SetupsTestes
{
    private static FonteSinal Ch(int n) => FonteSinal.DoCanal(n - 1);

    private static Equipamento NovoModulo(string nome, int canais, params Bridge[] bridges) =>
        new("x", GrupoEquipamento.Amplificador, "módulo", nome)
        {
            Entradas = Enumerable.Range(1, canais).Select(n => new Entrada(n, Ch(n))).ToArray(),
            Bridges = bridges,
        };

    private static Equipamento NovoAltoFalante(string nome, LigacaoAltoFalante? ligado, string categoria = "woofer") =>
        new("x", GrupoEquipamento.AltoFalante, categoria, nome) { Ligado = ligado };

    private async Task<string> CriarCarro()
    {
        await EntrarNaDuvida();
        Assert.True((await Rodar(_setups.CriarSetupAsync("Carro"))).Ok);
        return _setups.SetupEmUso!.Id;
    }

    // ----------------------------------------------------------------- Casa 2.1 da fixture CasaDoDenis

    [Fact]
    public async Task Casa_2_1_conferido_tem_as_saidas_JBL_L_JBL_R_e_Sub_e_o_Link_3_4_ligado()
    {
        await Conectar();

        Assert.True(_link.Ligado(ParCanais.Saidas34));
        Assert.False(_link.Ligado(ParCanais.Saidas12));
        var saidas = _setups.SaidasEmUso;
        Assert.Equal(["JBL L", "JBL R", "Sub"], saidas.Select(s => s.Nome));
        Assert.Equal([1, 2, 3], saidas.Select(s => s.Cor));
        Assert.Equal([2, 3], saidas[2].Canais);
        Assert.Equal(saidas.Select(s => s.Nome), _setups.Saidas(Casa).Select(s => s.Nome));
        Assert.Equal(4, _setups.Setup(Casa)!.Equipamentos.Count);
    }

    [Fact]
    public async Task O_Sub_muda_o_ganho_por_uma_escrita_no_canal_mestre_e_o_CH3_e_o_CH4_ficam_iguais()
    {
        await Conectar();
        var sub = _setups.SaidaDe(3)!;
        Assert.Equal(2, sub.Mestre);

        var r = await Rodar(_escritor.EscreverAsync(Ganho, sub.Mestre, 3.5m));

        Assert.True(r.Ok, r.Motivo);
        Assert.Equal("+3.5", _simulador.Valor("081", 2));
        Assert.Equal("+3.5", _simulador.Valor("081", 3));
        Assert.Equal(3.5m, _estado.Valor("081", 3)!.Numero);
    }

    [Fact]
    public async Task A_FILME_aplicada_no_Casa_2_1_relida_e_igual_a_leitura_de_18_10_na_regulagem_e_nas_bandas()
    {
        _simulador.Definir("081", 0, "-5.0");
        _simulador.Definir("081", 2, "+1.0");
        _simulador.Definir("081", 3, "+1.0");
        _simulador.Definir("071", 1, "-3.0");
        await ConectarSemReaplicar();
        Assert.Equal(EstadoConferencia.SomDiferente, _setups.Estado);

        var r = await Rodar(_setups.AplicarAsync(Filme));

        Assert.True(r.Ok, r.Mensagem);
        var espelho = _estado.Copiar();
        var camadas = CasaDoDenis.LeituraFilme.Where(v => Camadas.De(v.Codigo) == Camada.Regulagem || v.Codigo == "020").ToArray();
        Assert.Equal(Catalogo.ConsultasMarco1.Count - 4 - 1, camadas.Length);
        Assert.Empty(Camadas.Diferentes(camadas, espelho));
        Assert.Equal("+7.0", _simulador.Valor("081", 1)); // a R: o valor final guardado
        Assert.Equal("-18.0", _simulador.Valor("071", 1));
    }

    // ----------------------------------------------------------------- operações de equipamento

    [Fact]
    public async Task Setup_criado_do_aparelho_sem_equipamento_tem_as_saidas_CH1_a_CH4_e_o_link_sai_do_bridge_quando_entra()
    {
        var carro = await CriarCarro();

        Assert.Empty(_setups.SetupEmUso!.Equipamentos);
        Assert.Equal(["CH1", "CH2", "CH3", "CH4"], _setups.SaidasEmUso.Select(s => s.Nome));
        Assert.True((await _setups.AdicionarEquipamentoAsync(carro, NovoModulo("Módulo", 4, new Bridge(3, 4)))).Ok);
        Assert.True(_link.Ligado(ParCanais.Saidas34));
        Assert.Equal(["Módulo", "Módulo", "Módulo"], _setups.SaidasEmUso.Select(s => s.Nome));
        Assert.Equal("CH3 + CH4", _setups.SaidasEmUso[2].Rotulo);
        Assert.Equal(3, _setups.SaidasEmUso[2].Cor);
    }

    [Fact]
    public async Task Adicionar_editar_e_tirar_equipamento_guarda_no_celular_sem_mandar_nada_pelo_BLE()
    {
        var carro = await CriarCarro();
        var inicio = _simulador.Escritas.Count;
        var gravacoes = _guardadoSetups.Gravacoes;

        var m = await _setups.AdicionarEquipamentoAsync(carro, NovoModulo("Módulo", 4));
        Assert.True(m.Ok, m.Mensagem);
        Assert.Equal("Módulo adicionado ao Carro.", m.Mensagem);
        var idModulo = m.Id!;
        foreach (var (nome, canal) in new[] { ("Woofer L", 1), ("Woofer R", 2), ("Porta", 3) })
            Assert.True((await _setups.AdicionarEquipamentoAsync(carro, NovoAltoFalante(nome, LigacaoAltoFalante.NoCanal(idModulo, canal)))).Ok);
        Assert.True((await _setups.AdicionarEquipamentoAsync(carro, NovoAltoFalante("Solto", null))).Ok);

        Assert.Equal(["Woofer L", "Woofer R", "Porta", "Módulo"], _setups.SaidasEmUso.Select(s => s.Nome));
        var porta = _setups.SetupEmUso!.Equipamentos.Single(e => e.Nome == "Porta");
        var editado = await _setups.EditarEquipamentoAsync(carro, porta with { Nome = "Porta L", Modelo = "  " });
        Assert.True(editado.Ok, editado.Mensagem);
        Assert.Equal("Porta L guardado.", editado.Mensagem);
        Assert.Null(_setups.Equipamento(carro, porta.Id)!.Modelo);
        Assert.Equal("genérico", _setups.Equipamento(carro, porta.Id)!.ModeloExibido);
        Assert.Equal("Porta L", _setups.SaidasEmUso[2].Nome);

        var tirado = await _setups.TirarEquipamentoAsync(carro, idModulo);

        Assert.True(tirado.Ok, tirado.Mensagem);
        Assert.Equal("Módulo tirado do Carro. Ficaram sem ligação: Woofer L, Woofer R e Porta L.", tirado.Mensagem);
        Assert.All(_setups.SetupEmUso!.Equipamentos, e => Assert.Null(e.Ligado));
        Assert.Equal(["CH1", "CH2", "CH3", "CH4"], _setups.SaidasEmUso.Select(s => s.Nome));
        Assert.Equal(inicio, _simulador.Escritas.Count); // nada pelo BLE
        Assert.True(_guardadoSetups.Gravacoes > gravacoes);
    }

    [Fact]
    public async Task Ligacao_quebrada_bridge_invalido_e_ciclo_sao_recusados_sem_mudar_nada()
    {
        var carro = await CriarCarro();
        var idModulo = (await _setups.AdicionarEquipamentoAsync(carro, NovoModulo("Módulo", 4))).Id!;
        var x1 = (await _setups.AdicionarEquipamentoAsync(carro,
            new Equipamento("x", GrupoEquipamento.Repassa, "crossover ativo", "X1") { Entradas = [new Entrada(1, Ch(1))] })).Id!;
        var antes = GerenteSetups.ParaJson(_setups.Dados);
        var gravacoes = _guardadoSetups.Gravacoes;

        Assert.Equal(Ligacoes.MsgModuloNaoExiste,
            (await _setups.AdicionarEquipamentoAsync(carro, NovoAltoFalante("W", LigacaoAltoFalante.NoCanal("nada", 1)))).Motivo);
        Assert.Equal(Ligacoes.MsgModuloGrupoErrado,
            (await _setups.AdicionarEquipamentoAsync(carro, NovoAltoFalante("W", LigacaoAltoFalante.NoCanal(x1, 1)))).Motivo);
        Assert.Equal(Ligacoes.MsgBridgeVizinho, (await _setups.AdicionarEquipamentoAsync(carro, NovoModulo("M2", 4, new Bridge(2, 3)))).Motivo);
        Assert.Equal(Ligacoes.MsgBridgeRepetido,
            (await _setups.AdicionarEquipamentoAsync(carro, NovoModulo("M2", 4, new Bridge(1, 2), new Bridge(1, 2)))).Motivo);
        Assert.Equal(Ligacoes.MsgFonteGrupoErrado,
            (await _setups.AdicionarEquipamentoAsync(carro, NovoModulo("M2", 1) with { Entradas = [new Entrada(1, FonteSinal.DoEquipamento(idModulo))] })).Motivo);
        Assert.Equal(Ligacoes.MsgCanaisInteiro,
            (await _setups.AdicionarEquipamentoAsync(carro, NovoModulo("M2", 1) with { Dados = new DadosTecnicos { Canais = DadoTecnico.Digitado(1.5m) } })).Motivo);
        Assert.Equal(Ligacoes.MsgNumeroInvalido,
            (await _setups.AdicionarEquipamentoAsync(carro, NovoAltoFalante("W", null) with { Dados = new DadosTecnicos { Xmax = DadoTecnico.Digitado(-1m) } })).Motivo);
        Assert.Equal(antes, GerenteSetups.ParaJson(_setups.Dados));
        Assert.Equal(gravacoes, _guardadoSetups.Gravacoes);
        var x2 = new Equipamento("x", GrupoEquipamento.Repassa, "equalizador", "X2") { Entradas = [new Entrada(1, FonteSinal.DoEquipamento(x1))] };
        var idX2 = (await _setups.AdicionarEquipamentoAsync(carro, x2)).Id!;
        antes = GerenteSetups.ParaJson(_setups.Dados);
        gravacoes = _guardadoSetups.Gravacoes;
        var x1Ciclo = _setups.Equipamento(carro, x1)! with { Entradas = [new Entrada(1, FonteSinal.DoEquipamento(idX2))] };
        Assert.Equal(Ligacoes.MsgCiclo, (await _setups.EditarEquipamentoAsync(carro, x1Ciclo)).Motivo);
        Assert.Equal(Ligacoes.MsgNomeInvalido, (await _setups.AdicionarEquipamentoAsync(carro, NovoAltoFalante("  ", null))).Motivo);
        Assert.Equal(GerenteSetups.MsgEquipamentoNaoExiste, (await _setups.TirarEquipamentoAsync(carro, "nada")).Motivo);

        Assert.Equal(antes, GerenteSetups.ParaJson(_setups.Dados));
        Assert.Equal(gravacoes, _guardadoSetups.Gravacoes);
    }

    [Fact]
    public async Task Tirar_um_equipamento_do_grupo_3_avisa_quem_ficou_sem_fonte()
    {
        var carro = await CriarCarro();
        var x = (await _setups.AdicionarEquipamentoAsync(carro,
            new Equipamento("x", GrupoEquipamento.Repassa, "crossover ativo", "Crossover") { Entradas = [new Entrada(1, Ch(2))] })).Id!;
        var modulo = NovoModulo("Módulo A", 2) with { Entradas = [new Entrada(1, FonteSinal.DoEquipamento(x)), new Entrada(2, FonteSinal.DoEquipamento(x))] };
        var idModulo = (await _setups.AdicionarEquipamentoAsync(carro, modulo)).Id!;
        Assert.True((await _setups.AdicionarEquipamentoAsync(carro, NovoAltoFalante("Woofer", LigacaoAltoFalante.NoCanal(idModulo, 1)))).Ok);
        Assert.Equal("Woofer", _setups.SaidasEmUso[1].Nome);

        var r = await _setups.TirarEquipamentoAsync(carro, x);

        Assert.True(r.Ok, r.Mensagem);
        Assert.Equal("Crossover tirado do Carro. Ficaram sem fonte: Módulo A.", r.Mensagem);
        Assert.All(_setups.Equipamento(carro, idModulo)!.Entradas, e => Assert.Null(e.Fonte));
    }

    // ----------------------------------------------------------------- sugestão de função

    [Fact]
    public async Task Sugestao_pelo_corte_da_memoria_ativa_e_a_escolha_do_usuario_vale_mais()
    {
        await Conectar(); // a FILME: o CH3 com passa-baixa LR24 em 150 Hz
        var casa = _setups.Setup(Casa)!;
        var outra = NoBridgeDoSub();

        Assert.Equal(new CorteCanal(40m, 150m), GerenteSetups.Corte(CenarioFilme.Memoria, 2));
        Assert.Equal(FuncaoAcustica.Sub, _setups.FuncaoSugerida(Casa, outra));
        Assert.Equal(FuncaoAcustica.Grave, _setups.Funcao(Casa, outra with { Funcao = FuncaoAcustica.Grave }));
        Assert.Equal(FuncaoAcustica.MedioGrave, _setups.Funcao(Casa, casa.Equipamentos.Single(e => e.Id == CasaDoDenis.IdJblR)));
        Assert.Equal(FuncaoAcustica.Sub, _setups.Funcao(Casa, casa.Equipamentos.Single(e => e.Id == CasaDoDenis.IdSubCasa)));
    }

    private static Equipamento NoBridgeDoSub() => new("novo", GrupoEquipamento.AltoFalante, "Outra", "Coisa")
    {
        Ligado = LigacaoAltoFalante.NoBridge(CasaDoDenis.IdModuloCasa, new Bridge(3, 4)),
    };

    [Fact]
    public async Task A_sugestao_usa_o_corte_guardado_na_memoria_e_nao_o_do_aparelho()
    {
        await Conectar(); // o aparelho na FILME: o CH3 com passa-baixa em 150 Hz (sugeriria sub)
        var casa = _setups.Setup(Casa)!;
        // Uma memória com o CH3 com passa-alta LR24 em 2500 Hz e passa-baixa em OFF.
        var outra = CenarioFilme.Memoria with
        {
            Regulagem = CenarioFilme.Regulagem.Select(v => (v.Codigo, v.Indice) switch
            {
                ("040", 2) => v with { NoFio = "2500" },
                ("042", 2) => v with { NoFio = "3" },
                ("043", 2) => v with { NoFio = "0" },
                _ => v,
            }).ToArray(),
        };

        Assert.Equal(new CorteCanal(2500m, null), GerenteSetups.Corte(outra, 2));
        Assert.Equal(FuncaoAcustica.Agudo, GerenteSetups.FuncaoSugerida(casa, outra, NoBridgeDoSub()));
        Assert.Equal(FuncaoAcustica.Sub, GerenteSetups.FuncaoSugerida(casa, CenarioFilme.Memoria, NoBridgeDoSub()));
    }

    [Fact]
    public void Passa_baixa_em_OFF_com_a_frequencia_guardada_ate_200_Hz_nao_sugere_sub()
    {
        var casa = CasaDoDenis.SetupCasa;
        var semPassaBaixa = CenarioFilme.Memoria with
        {
            Regulagem = CenarioFilme.Regulagem.Select(v => (v.Codigo, v.Indice) == ("043", 2) ? v with { NoFio = "0" } : v).ToArray(),
        };

        Assert.Equal("150", Valor(semPassaBaixa.Regulagem, "041", 2));
        Assert.Null(GerenteSetups.Corte(semPassaBaixa, 2).PassaBaixa);
        Assert.Equal(FuncaoAcustica.FullRange, GerenteSetups.FuncaoSugerida(casa, semPassaBaixa, NoBridgeDoSub()));
        Assert.Equal(FuncaoAcustica.Sub, GerenteSetups.FuncaoSugerida(casa, CenarioFilme.Memoria, NoBridgeDoSub()));
    }

    [Fact]
    public void Quem_nao_toca_nao_tem_funcao_acustica()
    {
        var casa = CasaDoDenis.SetupCasa;
        var modulo = casa.Equipamentos[0];

        Assert.Null(GerenteSetups.FuncaoSugerida(casa, CenarioFilme.Memoria, modulo));
        Assert.Null(GerenteSetups.FuncaoSugerida(casa, CenarioFilme.Memoria, modulo with { Funcao = FuncaoAcustica.Grave }));
    }

    // ----------------------------------------------------------------- guardado

    [Fact]
    public async Task Reabrir_o_app_traz_os_equipamentos_as_ligacoes_e_as_fichas_iguais()
    {
        var carro = await CriarCarro();
        var idModulo = (await _setups.AdicionarEquipamentoAsync(carro, NovoModulo("Módulo", 4, new Bridge(3, 4)) with
        {
            Dados = new DadosTecnicos
            {
                Ganho = new DadoTecnico(19.6m, OrigemDado.Medido, "Multímetro"),
                Canais = DadoTecnico.Digitado(4m),
                PotenciaPonte = new DadoTecnico(400m, OrigemDado.Oficial, "https://fabricante.example/manual.pdf"),
                TensaoAlimentacao = new DadoTecnico(13.8m, OrigemDado.Estimado),
            },
        })).Id!;
        Assert.True((await _setups.AdicionarEquipamentoAsync(carro, NovoAltoFalante("Sub", LigacaoAltoFalante.NoBridge(idModulo, new Bridge(3, 4)), "subwoofer") with
        {
            Funcao = FuncaoAcustica.Sub,
            Dados = new DadosTecnicos
            {
                Diametro = DadoTecnico.Digitado(12m),
                Impedancia = DadoTecnico.Digitado(2m),
                TipoCaixa = new DadoTexto("selada", OrigemDado.OutraFonte, "Loja"),
                Le = new DadoTecnico(1.2m, OrigemDado.OutraFonte, "Fórum"),
            },
        })).Ok);
        Assert.True((await _setups.AdicionarEquipamentoAsync(carro,
            new Equipamento("x", GrupoEquipamento.AmplificaETocam, "caixa ativa 2 vias", "Caixa", "Modelo X")
            {
                Entradas = [new Entrada(1, Ch(1)), new Entrada(2, null)],
                Dados = new DadosTecnicos
                {
                    Sensibilidade = DadoTecnico.Digitado(98m),
                    FaixaMinima = DadoTecnico.Digitado(45m),
                    FaixaMaxima = DadoTecnico.Digitado(20000m),
                    Vias = [new Via("woofer", 12m), new Via("driver", null)],
                },
            })).Ok);
        var dados = _setups.Dados;

        using var reaberto = new SetupsTestes(_guardadoSetups.Texto, _guardadoMudo.Texto);
        await reaberto._setups.Carregada;

        var outro = reaberto._setups.Dados;
        Assert.Equal(GerenteSetups.ParaJson(dados), GerenteSetups.ParaJson(outro));
        Assert.Equal(dados.Setups.Single(s => s.Id == carro).Equipamentos, outro.Setups.Single(s => s.Id == carro).Equipamentos, new EquipamentoIgual());
        Assert.Equal(_setups.Saidas(carro).Select(s => s.Nome), reaberto._setups.Saidas(carro).Select(s => s.Nome));
        // O CH2 só alimenta o módulo (nada toca no canal 2 dele): vale o nome do módulo; o Sub no bridge é um par.
        Assert.Equal(["Caixa", "Módulo", "Sub"], reaberto._setups.Saidas(carro).Select(s => s.Nome));
        var caixa = reaberto._setups.Setup(carro)!.Equipamentos.Single(e => e.Nome == "Caixa");
        Assert.Equal([new Via("woofer", 12m), new Via("driver", null)], caixa.Dados.Vias);
        Assert.Contains("\"formato\": 4", _guardadoSetups.Texto);
        Assert.Contains("\"origem\": \"oficial\"", _guardadoSetups.Texto);
    }

    [Fact]
    public void Um_equipamento_ilegivel_e_descartado_e_os_outros_ficam()
    {
        var json = GerenteSetups.ParaJson(CasaDoDenis.Dados);
        var estragado = json.Replace("\"grupo\": \"amplificador\"", "\"grupo\": \"foguete\"", StringComparison.Ordinal);
        Assert.NotEqual(json, estragado);

        var dados = GerenteSetups.DeJson(estragado)!;

        var casa = dados.Setups.Single(s => s.Id == Casa);
        Assert.Equal(["JBL L", "JBL R", "Sub"], casa.Equipamentos.Select(e => e.Nome));
        Assert.All(casa.Equipamentos, e => Assert.Null(e.Ligado));
        Assert.Null(Ligacoes.Validar(casa.Equipamentos));
        Assert.Equal((false, false), Ligacoes.Links(casa.Equipamentos));
        Assert.Equal("CH3", Ligacoes.Saidas(casa.Equipamentos)[2].Nome);
    }

    [Fact]
    public void Um_dado_da_ficha_com_a_origem_desconhecida_sai_e_o_equipamento_fica()
    {
        var json = GerenteSetups.ParaJson(CasaDoDenis.Dados);
        var estragado = json.Replace("\"origem\": \"estimado\"", "\"origem\": \"chute\"", StringComparison.Ordinal);
        Assert.NotEqual(json, estragado);

        var casa = GerenteSetups.DeJson(estragado)!.Setups.Single(s => s.Id == Casa);

        Assert.Equal(4, casa.Equipamentos.Count);
        Assert.Null(casa.Equipamentos.Single(e => e.Id == CasaDoDenis.IdJblL).Dados.VolumeCaixa);
        Assert.NotNull(casa.Equipamentos.Single(e => e.Id == CasaDoDenis.IdJblL).Dados.Re);
    }

    [Fact]
    public void A_marca_do_mudo_continua_no_formato_1()
    {
        var marca = new MarcaMudo(Casa, ["0", "0", "1", "0"], DateTimeOffset.MinValue);

        var json = GerenteSetups.MarcaParaJson(marca);

        Assert.Contains("\"formato\": 1", json);
        Assert.Equal(marca.MuteAnterior, GerenteSetups.MarcaDeJson(json)!.MuteAnterior);
    }

    // ----------------------------------------------------------------- revisão da 8e

    [Fact]
    public async Task Editar_o_modulo_do_Casa_sem_o_bridge_e_recusado_dizendo_que_o_Sub_bloqueia()
    {
        await _setups.Carregada;
        var modulo = _setups.Equipamento(Casa, CasaDoDenis.IdModuloCasa)!;
        var antes = GerenteSetups.ParaJson(_setups.Dados);

        var r = await _setups.EditarEquipamentoAsync(Casa, modulo with { Bridges = [] });

        Assert.False(r.Ok);
        Assert.Equal("O Sub está ligado no bridge 3+4 deste módulo: religue ou tire o Sub antes.", r.Motivo);
        Assert.Equal(antes, GerenteSetups.ParaJson(_setups.Dados));
    }

    [Fact]
    public async Task Setup_com_2_tweeters_no_CH1_Snake_L_no_CH2_Snake_R_no_CH3_e_as_JBL_traseiras_no_CH4_tem_4_saidas()
    {
        var carro = await CriarCarro();
        Assert.False(_link.Ligado(ParCanais.Saidas12));
        Assert.False(_link.Ligado(ParCanais.Saidas34));
        var idModulo = (await _setups.AdicionarEquipamentoAsync(carro, NovoModulo("Módulo", 4))).Id!;
        foreach (var (nome, canal) in new[] { ("Tweeter 1", 1), ("Tweeter 2", 1), ("Snake L", 2), ("Snake R", 3), ("JBL L traseira", 4), ("JBL R traseira", 4) })
            Assert.True((await _setups.AdicionarEquipamentoAsync(carro, NovoAltoFalante(nome, LigacaoAltoFalante.NoCanal(idModulo, canal)))).Ok);

        Assert.Equal(["Tweeter 1 + Tweeter 2", "Snake L", "Snake R", "JBL L traseira + JBL R traseira"],
            _setups.Saidas(carro).Select(s => s.Nome));
        Assert.Equal(_setups.Saidas(carro).Select(s => s.Nome), _setups.SaidasEmUso.Select(s => s.Nome));
        Assert.False(_link.Ligado(ParCanais.Saidas34)); // sem bridge, sem link
    }

    [Fact]
    public async Task Reabrir_traz_campo_a_campo_a_funcao_o_modelo_a_nota_e_os_dados_tecnicos()
    {
        var carro = await CriarCarro();
        var dados = new DadosTecnicos
        {
            Diametro = DadoTecnico.Digitado(6.5m),
            Impedancia = new DadoTecnico(4m, OrigemDado.Oficial, "Manual"),
            PotenciaRms = DadoTecnico.Digitado(100m),
            Sensibilidade = DadoTecnico.Digitado(91.5m),
            FaixaMinima = DadoTecnico.Digitado(55m),
            FaixaMaxima = DadoTecnico.Digitado(21000m),
            Ganho = DadoTecnico.Digitado(2.5m),
            Canais = DadoTecnico.Digitado(3m),
            Vias = [new Via("woofer", 6.5m), new Via("tweeter", 1m)],
        };
        var id = (await _setups.AdicionarEquipamentoAsync(carro,
            new Equipamento("x", GrupoEquipamento.AmplificaETocam, "caixa ativa 2 vias", "Caixa", "Modelo X", FuncaoAcustica.MedioGrave)
            {
                Entradas = [new Entrada(1, Ch(1))],
                Dados = dados,
                Nota = "Comprada em 2025.",
            })).Id!;

        using var reaberto = new SetupsTestes(_guardadoSetups.Texto, _guardadoMudo.Texto);
        await reaberto._setups.Carregada;

        var e = reaberto._setups.Equipamento(carro, id)!;
        Assert.Equal(FuncaoAcustica.MedioGrave, e.Funcao);
        Assert.Equal("Modelo X", e.Modelo);
        Assert.Equal("Comprada em 2025.", e.Nota);
        Assert.Equal(GrupoEquipamento.AmplificaETocam, e.Grupo);
        Assert.Equal("caixa ativa 2 vias", e.Categoria);
        Assert.Equal(dados with { Vias = [] }, e.Dados with { Vias = [] });
        Assert.Equal([new Via("woofer", 6.5m), new Via("tweeter", 1m)], e.Dados.Vias);
        Assert.Equal<FonteSinal?>([Ch(1)], e.Entradas.Select(x => x.Fonte));
    }

    [Fact]
    public void Um_canal_escrito_como_texto_descarta_so_aquele_equipamento()
    {
        var json = GerenteSetups.ParaJson(CasaDoDenis.Dados);
        var estragado = json.Replace("\"canal\": 1", "\"canal\": \"1\"", StringComparison.Ordinal);
        Assert.NotEqual(json, estragado);

        var casa = GerenteSetups.DeJson(estragado)!.Setups.Single(s => s.Id == Casa);

        Assert.Equal(["Módulo SD800.4 EVO", "JBL R", "Sub"], casa.Equipamentos.Select(e => e.Nome));
        Assert.All(casa.Equipamentos.Skip(1), e => Assert.NotNull(e.Ligado));
    }

    [Fact]
    public void Um_canal_null_no_ligado_e_sem_ligacao_e_o_equipamento_fica()
    {
        var json = GerenteSetups.ParaJson(CasaDoDenis.Dados);
        var comNull = json.Replace("\"canal\": 1", "\"canal\": null", StringComparison.Ordinal);
        Assert.NotEqual(json, comNull);

        var casa = GerenteSetups.DeJson(comNull)!.Setups.Single(s => s.Id == Casa);

        Assert.Equal(["Módulo SD800.4 EVO", "JBL L", "JBL R", "Sub"], casa.Equipamentos.Select(e => e.Nome));
        Assert.Null(casa.Equipamentos[1].Ligado);
    }

    [Fact]
    public void Um_bridge_que_nao_vale_no_arquivo_sai_e_o_modulo_e_os_alto_falantes_ficam()
    {
        var fixture = CasaDoDenis.Dados;
        var casa = fixture.Setups[0];
        var modulo = casa.Equipamentos[0] with { Bridges = [new Bridge(3, 4), new Bridge(2, 3)] };
        var json = GerenteSetups.ParaJson(fixture with
        {
            Setups = [casa with { Equipamentos = [modulo, .. casa.Equipamentos.Skip(1), casa.Equipamentos[1] with { Nome = "Repetida" }] }],
        });

        var lida = GerenteSetups.DeJson(json)!.Setups.Single(s => s.Id == Casa);

        Assert.Equal(["Módulo SD800.4 EVO", "JBL L", "JBL R", "Sub"], lida.Equipamentos.Select(e => e.Nome));
        Assert.Equal([new Bridge(3, 4)], lida.Equipamentos[0].Bridges);
        Assert.Equal(LigacaoAltoFalante.NoBridge(CasaDoDenis.IdModuloCasa, new Bridge(3, 4)), lida.Equipamentos[3].Ligado);
        Assert.Equal(["JBL L", "JBL R", "Sub"], Ligacoes.Saidas(lida.Equipamentos).Select(s => s.Nome));
    }
}
