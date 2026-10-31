using BgsControle.Simulador;
using BgsControle.Nucleo.Setups;

namespace BgsControle.Telas.Testes;

/// <summary>
/// A lista e a folha de equipamento de um Setup (história 8e), contra o processador simulado. Nada
/// vai pelo BLE.
/// </summary>
public sealed class EquipamentosViewModelTestes : IDisposable
{
    private readonly Bancada _b = new(setups: true);
    private readonly EquipamentosViewModel _vm;

    public EquipamentosViewModelTestes() => _vm = new EquipamentosViewModel(_b.Setups!, _b.Tela, _b.Relogio);

    public void Dispose()
    {
        _vm.Dispose();
        _b.ConferirSoAudio();
        Assert.Empty(_b.Tela.Erros);
    }

    private async Task MostrarCasa()
    {
        await _b.Conectar();
        _vm.Mostrar(CasaDoDenis.IdSetupCasa);
        await _b.Tela.EsvaziarAsync();
    }

    [Fact]
    public async Task A_lista_do_Casa_2_1_mostra_o_detalhe_o_modelo_ou_generico_e_a_funcao()
    {
        await MostrarCasa();

        Assert.Equal("Casa 2.1", _vm.Titulo);
        Assert.Null(_vm.TextoVazio);
        Assert.Equal(["Módulo SD800.4 EVO", "JBL L", "JBL R", "Sub"], _vm.Itens.Select(i => i.Nome));
        var jblR = _vm.Item("JBL R")!;
        Assert.Equal("Alto-falante passivo · canal 2 do Módulo SD800.4 EVO · CH2", jblR.Detalhe);
        Assert.Equal("JBL 4TRMS80", jblR.Modelo);
        Assert.Equal("médio-grave", jblR.Funcao);
        var sub = _vm.Item("Sub")!;
        Assert.Equal("Alto-falante passivo · bridge 3+4 do Módulo SD800.4 EVO · CH3 + CH4", sub.Detalhe);
        Assert.Equal("Protech Light 8\"", sub.Modelo);
        Assert.Equal("sub", sub.Funcao);
        Assert.Equal("Amplificador · 4 canais · bridge 3+4 · CH1 + CH2 + CH3 + CH4", _vm.Item("Módulo SD800.4 EVO")!.Detalhe);
        Assert.Equal(["JBL L", "JBL R", "Sub"], _vm.Saidas.Select(s => s.Nome));
    }

    [Fact]
    public async Task Tirar_o_modulo_confirma_avisa_e_os_alto_falantes_ficam_sem_ligacao()
    {
        await MostrarCasa();
        var inicio = _b.Simulador.Escritas.Count;

        _vm.Item("Módulo SD800.4 EVO")!.Editar.Execute(null);
        Assert.True(_vm.FolhaAberta);
        Assert.False(_vm.Nova);
        Assert.Equal("Módulo SD800.4 EVO", _vm.TituloFolha);
        Assert.Equal(GrupoEquipamento.Amplificador, _vm.Grupo);
        Assert.Equal(4, _vm.NumeroCanais);
        Assert.Equal("19,5", _vm.Dado("ganho")!.Texto);
        Assert.Equal(["Bridge 1+2", "Bridge 3+4"], _vm.Bridges.Select(b => b.Rotulo));
        Assert.Equal([false, true], _vm.Bridges.Select(b => b.Ligado));
        Assert.Equal(["CH1", "CH2", "CH3", "CH4"], _vm.Entradas.Select(e => e.Fonte.Rotulo));

        await _vm.Tirar.ExecutarAsync();
        Assert.Equal("Tirar Módulo SD800.4 EVO do Casa 2.1?", _vm.ConfirmacaoTirar);
        Assert.Equal("Os alto-falantes ligados nele ficam sem ligação: JBL L, JBL R e Sub.", _vm.AvisoTirar);
        await _b.Rodar(_vm.ConfirmarTirar.ExecutarAsync());

        Assert.False(_vm.FolhaAberta);
        Assert.Equal("Módulo SD800.4 EVO tirado do Casa 2.1. Ficaram sem ligação: JBL L, JBL R e Sub.", _vm.Mensagem);
        Assert.Equal(["JBL L", "JBL R", "Sub"], _vm.Itens.Select(i => i.Nome));
        Assert.Equal("Alto-falante passivo · sem ligação", _vm.Item("JBL R")!.Detalhe);
        Assert.Equal(["CH1", "CH2", "CH3", "CH4"], _vm.Saidas.Select(s => s.Nome)); // sem o bridge, o link sai sozinho
        Assert.Equal(inicio, _b.Simulador.Escritas.Count); // nada pelo BLE

        // Sem módulo no Setup: o "ligado em" de um alto-falante passivo não tem opções.
        await _vm.Adicionar.ExecutarAsync();
        Assert.True(_vm.Nova);
        Assert.Equal(GrupoEquipamento.AltoFalante, _vm.Grupo);
        Assert.True(_vm.MostraLigado);
        Assert.Empty(_vm.OpcoesLigado);
        Assert.Equal("Adicione um módulo para ligar o alto-falante.", _vm.FraseSemModulo);
    }

    [Fact]
    public async Task Adicionar_alto_falante_passivo_sem_modulo_no_Setup_nao_tem_opcoes_e_avisa()
    {
        await _b.Setups!.Carregada;
        var casa = CasaDoDenis.IdSetupCasa;
        Assert.True((await _b.Setups.TirarEquipamentoAsync(casa, CasaDoDenis.IdModuloCasa)).Ok);
        _vm.Mostrar(casa);
        await _b.Tela.EsvaziarAsync();

        await _vm.Adicionar.ExecutarAsync();

        Assert.Equal("Adicionar equipamento", _vm.TituloFolha);
        Assert.Empty(_vm.OpcoesLigado);
        Assert.Null(_vm.Ligado);
        Assert.Equal(EquipamentosViewModel.TextoSemModulo, _vm.FraseSemModulo);

        // Mesmo assim ele entra, sem ligação.
        _vm.EscolherCategoria("tweeter");
        Assert.Equal("Tweeter", _vm.Nome);
        await _vm.Confirmar.ExecutarAsync();
        Assert.Equal("Tweeter adicionado ao Casa 2.1.", _vm.Mensagem);
        Assert.Equal("Alto-falante passivo · sem ligação", _vm.Item("Tweeter")!.Detalhe);
    }

    [Fact]
    public async Task Adicionar_um_sub_ativo_no_CH1_com_a_folha_vira_nome_da_saida()
    {
        await MostrarCasa();
        await _vm.Adicionar.ExecutarAsync();

        _vm.Grupo = GrupoEquipamento.AmplificaETocam;
        Assert.True(_vm.MostraEntradas);
        Assert.True(_vm.MostraVias);
        Assert.Contains("sub ativo", _vm.Categorias);
        Assert.Equal(EquipamentosViewModel.TextoOutra, _vm.Categorias[^1]);
        _vm.EscolherCategoria("sub ativo");
        Assert.Equal("Sub ativo", _vm.Nome);
        Assert.Equal("Sugerida: sub", _vm.Funcoes[0].Rotulo);
        Assert.Equal(["Sem ligação", "CH1", "CH2", "CH3", "CH4"], _vm.Entradas[0].Opcoes.Select(o => o.Rotulo));
        _vm.Entradas[0].Fonte = _vm.Entradas[0].Opcoes[1];
        _vm.Modelo = "SW 12";
        _vm.Dado("diametro")!.Texto = "12";
        _vm.Dado("potenciaRms")!.Texto = "250,5";

        await _vm.Confirmar.ExecutarAsync();

        Assert.False(_vm.FolhaAberta);
        Assert.Equal("Sub ativo adicionado ao Casa 2.1.", _vm.Mensagem);
        var item = _vm.Item("Sub ativo")!;
        Assert.Equal("Amplifica e toca · CH1", item.Detalhe);
        Assert.Equal("SW 12", item.Modelo);
        Assert.Equal("JBL L + Sub ativo", _vm.Saidas[0].Nome);
        var guardado = _b.Setups!.Setup(CasaDoDenis.IdSetupCasa)!.Equipamentos.Single(e => e.Nome == "Sub ativo");
        Assert.Equal(new DadoTecnico(250.5m, OrigemDado.Digitado), guardado.Dados.PotenciaRms); // a ficha monta o dado digitado
        Assert.Equal(new DadoTecnico(12m, OrigemDado.Digitado), guardado.Dados.Diametro);
        Assert.Null(guardado.Funcao); // vale a sugerida
    }

    [Fact]
    public async Task Categoria_Outra_no_bridge_do_sub_sugere_sub_pelo_corte_e_a_escolha_do_usuario_vale()
    {
        await MostrarCasa();
        await _vm.Adicionar.ExecutarAsync();

        _vm.EscolherCategoria(EquipamentosViewModel.TextoOutra);
        Assert.True(_vm.CategoriaLivre);
        _vm.Categoria = "Outra";
        _vm.Nome = "Shaker do sofá";
        Assert.Equal(["Sem ligação", "canal 1 do Módulo SD800.4 EVO", "canal 2 do Módulo SD800.4 EVO", "canal 3 do Módulo SD800.4 EVO",
            "canal 4 do Módulo SD800.4 EVO", "bridge 3+4 do Módulo SD800.4 EVO"], _vm.OpcoesLigado.Select(o => o.Rotulo));
        _vm.Ligado = _vm.OpcoesLigado[^1];
        Assert.Equal("Sugerida: sub", _vm.Funcoes[0].Rotulo); // o passa-baixa do CH3 em 150 Hz
        Assert.Equal("sub", _vm.FuncaoQueVale);
        _vm.Funcao = FuncaoAcustica.Grave;
        Assert.Equal("grave", _vm.FuncaoQueVale);

        await _vm.Confirmar.ExecutarAsync();

        Assert.Equal("grave", _vm.Item("Shaker do sofá")!.Funcao);
        Assert.Equal("Sub + Shaker do sofá", _vm.Saidas[2].Nome);
    }

    [Fact]
    public async Task Dado_tecnico_que_nao_e_numero_e_nome_vazio_recusam_com_o_motivo_e_nada_muda()
    {
        await MostrarCasa();
        await _vm.Adicionar.ExecutarAsync();
        _vm.EscolherCategoria("woofer");
        _vm.Dado("impedancia")!.Texto = "quatro";

        await _vm.Confirmar.ExecutarAsync();

        Assert.True(_vm.FolhaAberta);
        Assert.Equal("Impedância: escreva um número maior que zero.", _vm.Erro);
        _vm.Dado("impedancia")!.Texto = "4";
        _vm.Nome = "";
        await _vm.Confirmar.ExecutarAsync();
        Assert.Equal(Ligacoes.MsgNomeInvalido, _vm.Erro);
        Assert.Equal(4, _vm.Itens.Count);

        await _vm.Cancelar.ExecutarAsync();
        Assert.False(_vm.FolhaAberta);
        Assert.Null(_vm.Erro);
    }

    [Fact]
    public async Task Editar_a_JBL_R_carrega_a_folha_e_guarda_a_mudanca()
    {
        await MostrarCasa();

        _vm.AbrirEditar(CasaDoDenis.IdJblR);

        Assert.Equal("JBL R", _vm.Nome);
        Assert.Equal("JBL 4TRMS80", _vm.Modelo);
        Assert.Equal("médio-grave", _vm.Categoria);
        Assert.False(_vm.CategoriaLivre);
        Assert.Equal("4", _vm.Dado("impedancia")!.Texto); // a nominal; a Re medida (3,6 Ω) fica na ficha
        Assert.Equal("canal 2 do Módulo SD800.4 EVO", _vm.Ligado!.Rotulo);
        _vm.Nome = "JBL R frente";

        await _vm.Confirmar.ExecutarAsync();

        Assert.Equal("JBL R frente guardado.", _vm.Mensagem);
        Assert.Equal("JBL R frente", _vm.Saidas[1].Nome);
        var guardada = _b.Setups!.Equipamento(CasaDoDenis.IdSetupCasa, CasaDoDenis.IdJblR)!;
        Assert.Contains("100 Hz", guardada.Nota);
        // Os dados que a folha não mostra ficam, com a origem; o que não mudou também.
        Assert.Equal(new DadoTecnico(3.6m, OrigemDado.Medido, "Medido em 18/10/2026"), guardada.Dados.Re);
        Assert.Equal(OrigemDado.Oficial, guardada.Dados.Impedancia!.Origem);
        Assert.Equal(OrigemDado.Oficial, guardada.Dados.Fs!.Origem);
    }

    [Fact]
    public async Task Modulo_novo_com_bridge_entre_canais_e_numero_de_canais()
    {
        await MostrarCasa();
        await _vm.Adicionar.ExecutarAsync();
        _vm.Grupo = GrupoEquipamento.Amplificador;
        Assert.True(_vm.MostraCanaisModulo);
        Assert.Equal(2, _vm.NumeroCanais);
        _vm.NumeroCanais = 4;
        Assert.Equal(["Canal 1 do módulo", "Canal 2 do módulo", "Canal 3 do módulo", "Canal 4 do módulo"], _vm.Entradas.Select(e => e.Rotulo));
        _vm.EscolherCategoria("amplificador");
        _vm.Bridges[0].Ligado = true;
        _vm.Entradas[0].Fonte = _vm.Entradas[0].Opcoes[1];
        _vm.Entradas[1].Fonte = _vm.Entradas[1].Opcoes[2];

        await _vm.Confirmar.ExecutarAsync();

        Assert.Equal("Amplificador · 4 canais · bridge 1+2 · CH1 + CH2", _vm.Item("Amplificador")!.Detalhe);
    }

    [Fact]
    public async Task Sem_Setup_a_lista_pede_para_escolher_um()
    {
        await _b.Setups!.Carregada;
        _vm.Mostrar(null);

        await _vm.Adicionar.ExecutarAsync();

        Assert.Equal(EquipamentosViewModel.MsgSemSetup, _vm.TextoVazio);
        Assert.False(_vm.FolhaAberta);
        Assert.Equal(EquipamentosViewModel.MsgSemSetup, _vm.Mensagem);
    }

    [Fact]
    public async Task Se_o_Setup_muda_com_a_folha_aberta_o_confirmar_recusa_e_nada_entra()
    {
        await _b.Conectar();
        _vm.Mostrar(null); // o Setup em uso: o Casa 2.1
        await _b.Tela.EsvaziarAsync();
        await _vm.Adicionar.ExecutarAsync();
        _vm.EscolherCategoria("tweeter");

        await _b.Sessao.DesconectarAsync(); // sem conexão, o Setup em uso sai
        await _b.Tela.EsvaziarAsync();
        await _vm.Confirmar.ExecutarAsync();

        Assert.True(_vm.FolhaAberta);
        Assert.Equal(EquipamentosViewModel.MsgSetupMudou, _vm.Erro);
        Assert.Equal(4, _b.Setups!.Setup(CasaDoDenis.IdSetupCasa)!.Equipamentos.Count);
    }

    [Fact]
    public async Task O_numero_de_canais_do_modulo_fica_de_1_a_16()
    {
        await MostrarCasa();
        await _vm.Adicionar.ExecutarAsync();
        _vm.Grupo = GrupoEquipamento.Amplificador;

        _vm.NumeroCanais = 40;
        Assert.Equal(16, _vm.NumeroCanais);
        Assert.Equal(8, _vm.Bridges.Count);
        _vm.NumeroCanais = 0;
        Assert.Equal(1, _vm.NumeroCanais);
        Assert.Empty(_vm.Bridges);
    }

    [Fact]
    public async Task Trocar_o_modelo_ao_editar_os_dados_que_ficam_viram_digitados_e_nao_vao_como_oficiais()
    {
        await MostrarCasa();
        _vm.AbrirEditar(CasaDoDenis.IdJblR);

        Assert.Equal("oficial ✓", _vm.Dado("fs")!.Selo);
        _vm.Modelo = "Outra JBL";
        Assert.Equal("digitado", _vm.Dado("fs")!.Selo); // o selo já diz o que vai ser guardado
        Assert.Equal("digitado", _vm.Dado("re")!.Selo);
        await _vm.Confirmar.ExecutarAsync();

        var guardada = _b.Setups!.Equipamento(CasaDoDenis.IdSetupCasa, CasaDoDenis.IdJblR)!;
        Assert.Equal("Outra JBL", guardada.Modelo);
        Assert.Equal(new DadoTecnico(4m, OrigemDado.Digitado), guardada.Dados.Impedancia);
        Assert.Equal(new DadoTecnico(98m, OrigemDado.Digitado), guardada.Dados.Fs);
        Assert.Equal(new DadoTecnico(3.6m, OrigemDado.Digitado), guardada.Dados.Re);
        Assert.Equal(new DadoTexto("selada", OrigemDado.Digitado), guardada.Dados.TipoCaixa);
    }

    // ------------------------------------------------------------------ história 10c: a ficha com origem

    private static readonly string[] Equipamentos =
        [CasaDoDenis.IdModuloCasa, CasaDoDenis.IdJblL, CasaDoDenis.IdJblR, CasaDoDenis.IdSubCasa];

    [Fact]
    public async Task Ficha_da_JBL_L_mostra_o_selo_de_origem_de_cada_dado()
    {
        await MostrarCasa();

        _vm.AbrirEditar(CasaDoDenis.IdJblL);

        Assert.Equal([EquipamentosViewModel.SecaoGerais, EquipamentosViewModel.SecaoAltoFalante, EquipamentosViewModel.SecaoCaixa],
            _vm.SecoesDados.Select(s => s.Titulo));
        var fs = _vm.Dado("fs")!;
        Assert.Equal("98", fs.Texto);
        Assert.Equal("oficial ✓", fs.Selo);
        Assert.Equal("Fs (Hz)", fs.RotuloTela);
        var re = _vm.Dado("re")!;
        Assert.Equal("3,6", re.Texto);
        Assert.Equal("medido ✓", re.Selo);
        var volume = _vm.Dado("volumeCaixa")!;
        Assert.Equal("3,5", volume.Texto);
        Assert.Equal("estimado", volume.Selo);
        Assert.False(volume.Verificado);
        Assert.Equal("selada", _vm.Dado(EquipamentosViewModel.CampoTipoCaixa)!.Texto);
        Assert.Equal("0,81", _vm.Dado("qes")!.Texto);
        Assert.Null(_vm.Dado("le")!.Selo); // vazio: sem selo
        Assert.Equal("", _vm.Dado("le")!.Texto);
    }

    [Fact]
    public async Task Cada_dado_dos_4_equipamentos_da_fixture_mostra_a_origem_que_o_nucleo_guarda_e_so_oficial_e_medido_tem_o_visto()
    {
        await MostrarCasa();

        foreach (var id in Equipamentos)
        {
            var e = _b.Setups!.Equipamento(CasaDoDenis.IdSetupCasa, id)!;
            _vm.AbrirEditar(id);
            var naTela = _vm.SecoesDados.SelectMany(s => s.Dados).ToDictionary(d => d.Nome);
            foreach (var c in DadosTecnicos.Campos)
            {
                if (c.Ler(e.Dados) is not { } v)
                    continue;
                Assert.True(naTela.TryGetValue(c.Nome, out var linha), $"{e.Nome}: {c.Nome} fora da folha");
                Assert.Equal(v.Origem, linha!.Origem);
                Assert.Equal(v.Fonte, linha.Fonte);
                Assert.Equal(DadoViewModel.TextoSelo(v.Origem), linha.Selo);
                Assert.Equal(v.Origem is OrigemDado.Oficial or OrigemDado.Medido, linha.Verificado);
                Assert.Equal(linha.Verificado, linha.Selo!.EndsWith('✓'));
            }
            if (e.Dados.TipoCaixa is { } tipo)
                Assert.Equal(DadoViewModel.TextoSelo(tipo.Origem), naTela[EquipamentosViewModel.CampoTipoCaixa].Selo);
            Assert.All(naTela.Values.Where(d => d.Texto.Length == 0), d => Assert.Null(d.Selo));
            await _vm.Cancelar.ExecutarAsync();
        }
    }

    [Fact]
    public async Task Confirmar_a_ficha_sem_mudar_nada_deixa_todas_as_origens_e_fontes_como_estavam()
    {
        await MostrarCasa();

        foreach (var id in Equipamentos)
        {
            var antes = _b.Setups!.Equipamento(CasaDoDenis.IdSetupCasa, id)!.Dados;
            _vm.AbrirEditar(id);
            await _vm.Confirmar.ExecutarAsync();
            Assert.False(_vm.FolhaAberta, _vm.Erro);

            var depois = _b.Setups.Equipamento(CasaDoDenis.IdSetupCasa, id)!.Dados;
            foreach (var c in DadosTecnicos.Campos)
                Assert.Equal(c.Ler(antes), c.Ler(depois));
            Assert.Equal(antes.TipoCaixa, depois.TipoCaixa);
            Assert.Equal(antes.Vias, depois.Vias);
        }
    }

    [Fact]
    public async Task Digitar_um_dado_vazio_vira_digitado_e_um_valor_que_nao_e_numero_recusa_sem_guardar()
    {
        await MostrarCasa();
        await _vm.Adicionar.ExecutarAsync();
        _vm.EscolherCategoria("woofer");
        var fs = _vm.Dado("fs")!;
        Assert.Null(fs.Selo);

        fs.Texto = "abc";
        await _vm.Confirmar.ExecutarAsync();
        Assert.True(_vm.FolhaAberta);
        Assert.Equal("Fs: escreva um número maior que zero.", _vm.Erro);
        fs.Texto = "0";
        await _vm.Confirmar.ExecutarAsync();
        Assert.Equal("Fs: escreva um número maior que zero.", _vm.Erro);
        Assert.Equal(4, _vm.Itens.Count); // nada guardado

        fs.Texto = "80";
        Assert.Equal("digitado", fs.Selo);
        await _vm.Confirmar.ExecutarAsync();

        var guardado = _b.Setups!.Setup(CasaDoDenis.IdSetupCasa)!.Equipamentos.Single(e => e.Nome == "Woofer");
        Assert.Equal(new DadoTecnico(80m, OrigemDado.Digitado), guardado.Dados.Fs);
    }

    [Fact]
    public async Task Mudar_um_dado_vira_digitado_e_voltar_ao_valor_de_antes_volta_a_origem()
    {
        await MostrarCasa();
        _vm.AbrirEditar(CasaDoDenis.IdJblL);
        var fs = _vm.Dado("fs")!;

        fs.Texto = "99";
        Assert.Equal("digitado", fs.Selo);
        Assert.Null(fs.Fonte);
        fs.Texto = "98,0";
        Assert.Equal("oficial ✓", fs.Selo);
        Assert.NotNull(fs.Fonte);
    }

    [Fact]
    public async Task Tocar_no_selo_escolhe_a_origem_e_a_fonte_e_a_fonte_longa_recusa_sem_guardar()
    {
        await MostrarCasa();
        await _vm.Adicionar.ExecutarAsync();
        _vm.EscolherCategoria("woofer");
        var fs = _vm.Dado("fs")!;
        var re = _vm.Dado("re")!;
        fs.Texto = "80";
        re.Texto = "3,2";

        await fs.AlternarOrigem.ExecutarAsync();
        Assert.True(fs.OrigemAberta);
        Assert.Equal(["oficial", "outra fonte", "medido", "estimado", "digitado"], fs.Origens.Select(o => o.Rotulo));
        Assert.Equal("digitado", fs.Origens.Single(o => o.Escolhida).Rotulo);
        await fs.Origens[0].Escolher.ExecutarAsync();
        Assert.Equal("oficial ✓", fs.Selo);
        Assert.Equal(DadoViewModel.DicaFonteOficial, fs.DicaFonte);
        fs.TextoFonte = new string('x', 121);

        // Uma origem aberta por vez.
        await re.AlternarOrigem.ExecutarAsync();
        Assert.True(re.OrigemAberta);
        Assert.False(fs.OrigemAberta);

        await _vm.Confirmar.ExecutarAsync();
        Assert.True(_vm.FolhaAberta);
        Assert.Equal(EquipamentosViewModel.MsgFonte("Fs"), _vm.Erro);
        Assert.Equal(4, _vm.Itens.Count); // nada guardado

        fs.TextoFonte = "https://www.exemplo.com/woofer.pdf";
        fs.Texto = "81"; // a origem escolhida fica
        Assert.Equal("oficial ✓", fs.Selo);
        await _vm.Confirmar.ExecutarAsync();

        var guardado = _b.Setups!.Setup(CasaDoDenis.IdSetupCasa)!.Equipamentos.Single(e => e.Nome == "Woofer");
        Assert.Equal(new DadoTecnico(81m, OrigemDado.Oficial, "https://www.exemplo.com/woofer.pdf"), guardado.Dados.Fs);
        Assert.Equal(new DadoTecnico(3.2m, OrigemDado.Digitado), guardado.Dados.Re);
    }

    [Fact]
    public async Task O_modelo_se_completa_pela_base_e_so_o_toque_preenche_a_ficha_com_as_origens_dela()
    {
        await MostrarCasa();
        await _vm.Adicionar.ExecutarAsync();
        _vm.EscolherCategoria("médio-grave");
        _vm.Dado("le")!.Texto = "0,5"; // a base não tem o Le da JBL: fica como estava
        _vm.Dado("fs")!.Texto = "70";

        _vm.Modelo = "j";
        Assert.Empty(_vm.SugestoesModelo); // menos de 2 letras
        Assert.False(_vm.TemSugestoes);
        _vm.Modelo = "jb";
        var sugestao = Assert.Single(_vm.SugestoesModelo);
        Assert.Equal("JBL 4TRMS80", sugestao.Rotulo);
        Assert.Equal("médio-grave", sugestao.Detalhe);
        Assert.Equal("70", _vm.Dado("fs")!.Texto); // sem toque, nada é preenchido

        await sugestao.Escolher.ExecutarAsync();

        Assert.Equal("JBL 4TRMS80", _vm.Modelo);
        Assert.Empty(_vm.SugestoesModelo);
        Assert.Equal("98", _vm.Dado("fs")!.Texto);
        Assert.Equal("oficial ✓", _vm.Dado("fs")!.Selo);
        Assert.Equal("0,81", _vm.Dado("qes")!.Texto);
        Assert.Equal("oficial ✓", _vm.Dado("qes")!.Selo);
        Assert.Equal("0,5", _vm.Dado("le")!.Texto);
        Assert.Equal("digitado", _vm.Dado("le")!.Selo);
        await _vm.Confirmar.ExecutarAsync();

        var guardado = _b.Setups!.Setup(CasaDoDenis.IdSetupCasa)!.Equipamentos.Single(e => e.Nome == "Médio-grave");
        Assert.Equal(CasaDoDenis.FichaJbl.Fs, guardado.Dados.Fs);
        Assert.Equal(CasaDoDenis.FichaJbl.Qes, guardado.Dados.Qes);
        Assert.Equal(new DadoTecnico(0.5m, OrigemDado.Digitado), guardado.Dados.Le);
    }

    [Fact]
    public async Task Ao_abrir_a_folha_sem_escrever_no_modelo_nao_ha_sugestao_e_o_modulo_da_base_traz_os_canais()
    {
        await MostrarCasa();
        _vm.AbrirEditar(CasaDoDenis.IdJblR);
        Assert.Empty(_vm.SugestoesModelo);
        await _vm.Cancelar.ExecutarAsync();

        await _vm.Adicionar.ExecutarAsync();
        _vm.Grupo = GrupoEquipamento.Amplificador;
        Assert.Equal([EquipamentosViewModel.SecaoModulo, EquipamentosViewModel.SecaoAlimentacao], _vm.SecoesDados.Select(s => s.Titulo));
        Assert.Equal(2, _vm.NumeroCanais);
        Assert.Equal("2", _vm.Dado(EquipamentosViewModel.CampoCanais)!.Texto);
        Assert.True(_vm.Dado(EquipamentosViewModel.CampoCanais)!.SoLeitura);
        _vm.Modelo = "sd8";
        await Assert.Single(_vm.SugestoesModelo).Escolher.ExecutarAsync();

        Assert.Equal("SD800.4 EVO", _vm.Modelo);
        Assert.Equal(4, _vm.NumeroCanais);
        Assert.Equal("oficial ✓", _vm.Dado(EquipamentosViewModel.CampoCanais)!.Selo);
        Assert.Equal("132", _vm.Dado("potenciaCanal")!.Texto);
        Assert.Equal("oficial ✓", _vm.Dado("potenciaCanal")!.Selo);
        _vm.NumeroCanais = 2;
        Assert.Equal("digitado", _vm.Dado(EquipamentosViewModel.CampoCanais)!.Selo);
    }

    // ------------------------------------------------------------------ história 10c, revisão

    private async Task Guardar(string id, Func<DadosTecnicos, DadosTecnicos> mudar)
    {
        var casa = CasaDoDenis.IdSetupCasa;
        var e = _b.Setups!.Equipamento(casa, id)!;
        Assert.True((await _b.Setups.EditarEquipamentoAsync(casa, e with { Dados = mudar(e.Dados) })).Ok);
        await _b.Tela.EsvaziarAsync();
    }

    [Fact]
    public void Todo_campo_da_ficha_tem_uma_linha_na_folha()
    {
        foreach (var c in DadosTecnicos.Campos)
            Assert.NotNull(_vm.Dado(c.Nome));
        Assert.NotNull(_vm.Dado(EquipamentosViewModel.CampoTipoCaixa));
        Assert.Equal(DadosTecnicos.Campos.Count + 1, _vm.Dados.Count);
    }

    [Fact]
    public async Task Um_valor_com_muitas_casas_aparece_inteiro_e_confirmar_sem_mudar_guarda_o_mesmo_valor_e_a_mesma_origem()
    {
        await MostrarCasa();
        var le = new DadoTecnico(0.0453m, OrigemDado.Oficial, "https://www.exemplo.com/jbl.pdf");
        await Guardar(CasaDoDenis.IdJblL, d => d with { Le = le });

        _vm.AbrirEditar(CasaDoDenis.IdJblL);
        Assert.Equal("0,0453", _vm.Dado("le")!.Texto);
        Assert.Equal("oficial ✓", _vm.Dado("le")!.Selo);
        await _vm.Confirmar.ExecutarAsync();

        Assert.Equal(le, _b.Setups!.Equipamento(CasaDoDenis.IdSetupCasa, CasaDoDenis.IdJblL)!.Dados.Le);
    }

    [Fact]
    public async Task Esvaziar_o_campo_esquece_a_origem_e_a_fonte_escolhidas()
    {
        await MostrarCasa();
        await _vm.Adicionar.ExecutarAsync();
        _vm.EscolherCategoria("woofer");
        var fs = _vm.Dado("fs")!;
        fs.Texto = "80";
        fs.EscolherOrigem(OrigemDado.Oficial);
        fs.TextoFonte = "https://www.exemplo.com/woofer.pdf";
        Assert.Equal("oficial ✓", fs.Selo);

        fs.Texto = "";
        Assert.Null(fs.Selo);
        fs.Texto = "81";

        Assert.Equal("digitado", fs.Selo);
        Assert.Null(fs.Fonte);
    }

    [Fact]
    public async Task Escrever_so_a_fonte_nao_trava_a_origem_e_mudar_o_numero_depois_vira_digitado()
    {
        await MostrarCasa();
        _vm.AbrirEditar(CasaDoDenis.IdJblL);
        var fs = _vm.Dado("fs")!;

        fs.TextoFonte = "Ficha impressa, página 2";
        Assert.Equal("oficial ✓", fs.Selo);
        Assert.Equal("Ficha impressa, página 2", fs.Fonte);
        fs.Texto = "99";

        Assert.Equal("digitado", fs.Selo);
    }

    [Fact]
    public async Task Fonte_com_quebra_de_linha_recusa_com_a_mensagem_da_fonte()
    {
        await MostrarCasa();
        await _vm.Adicionar.ExecutarAsync();
        _vm.EscolherCategoria("woofer");
        var fs = _vm.Dado("fs")!;
        fs.Texto = "80";
        fs.TextoFonte = "linha 1\nlinha 2";

        await _vm.Confirmar.ExecutarAsync();

        Assert.True(_vm.FolhaAberta);
        Assert.Equal("Fs: a fonte vai até 120 caracteres, sem quebra de linha.", _vm.Erro);
        Assert.Equal(4, _vm.Itens.Count);
    }

    [Fact]
    public async Task Os_dados_da_base_viram_digitados_se_o_modelo_muda_depois_do_toque()
    {
        await MostrarCasa();
        await _vm.Adicionar.ExecutarAsync();
        _vm.EscolherCategoria("médio-grave");
        _vm.Modelo = "jb";
        await Assert.Single(_vm.SugestoesModelo).Escolher.ExecutarAsync();
        Assert.Equal("oficial ✓", _vm.Dado("fs")!.Selo);

        _vm.Modelo = "XYZ";
        Assert.Equal("digitado", _vm.Dado("fs")!.Selo);
        await _vm.Confirmar.ExecutarAsync();

        var guardado = _b.Setups!.Setup(CasaDoDenis.IdSetupCasa)!.Equipamentos.Single(e => e.Nome == "Médio-grave");
        Assert.Equal(new DadoTecnico(98m, OrigemDado.Digitado), guardado.Dados.Fs);
    }

    [Fact]
    public async Task As_sugestoes_sao_so_do_grupo_da_folha()
    {
        await MostrarCasa();
        await _vm.Adicionar.ExecutarAsync();

        _vm.Modelo = "sd"; // o SD800.4 EVO é um módulo
        Assert.Empty(_vm.SugestoesModelo);
        _vm.Grupo = GrupoEquipamento.Amplificador;
        Assert.Equal(["SD800.4 EVO"], _vm.SugestoesModelo.Select(s => s.Rotulo));
        _vm.Grupo = GrupoEquipamento.AmplificaETocam;
        _vm.Modelo = "jbl";
        Assert.Empty(_vm.SugestoesModelo); // a JBL é alto-falante passivo

        _vm.EscolherModelo("JBL 4TRMS80"); // de outro grupo: só o nome, nada é preenchido
        Assert.Equal("", _vm.Dado("fs")!.Texto);
    }

    [Fact]
    public async Task Num_modulo_que_ja_existe_a_base_com_menos_canais_nao_tira_canais_nem_ligacoes()
    {
        await MostrarCasa();
        var f = "https://www.exemplo.com/mini.pdf";
        Assert.True(await _b.Modelos!.GuardarAsync(new Equipamento("mini", GrupoEquipamento.Amplificador, "módulo", "Mini", "MINI 2")
        {
            Dados = new DadosTecnicos { Canais = new DadoTecnico(2m, OrigemDado.Oficial, f), PotenciaCanal = new DadoTecnico(50m, OrigemDado.Oficial, f) },
        }));
        _vm.AbrirEditar(CasaDoDenis.IdModuloCasa);

        _vm.Modelo = "min";
        await Assert.Single(_vm.SugestoesModelo).Escolher.ExecutarAsync();

        Assert.Equal("MINI 2", _vm.Modelo);
        Assert.Equal(4, _vm.NumeroCanais);
        Assert.Equal(["CH1", "CH2", "CH3", "CH4"], _vm.Entradas.Select(e => e.Fonte.Rotulo));
        Assert.Equal([false, true], _vm.Bridges.Select(b => b.Ligado));
        Assert.Equal("50", _vm.Dado("potenciaCanal")!.Texto);
        Assert.Equal("oficial ✓", _vm.Dado("potenciaCanal")!.Selo);
    }

    [Fact]
    public async Task Os_canais_guardados_num_equipamento_fora_do_grupo_2_ficam_ao_confirmar_sem_mudar()
    {
        await MostrarCasa();
        await Guardar(CasaDoDenis.IdJblL, d => d with { Canais = new DadoTecnico(1m, OrigemDado.Digitado) });

        _vm.AbrirEditar(CasaDoDenis.IdJblL);
        await _vm.Confirmar.ExecutarAsync();

        Assert.Equal(new DadoTecnico(1m, OrigemDado.Digitado),
            _b.Setups!.Equipamento(CasaDoDenis.IdSetupCasa, CasaDoDenis.IdJblL)!.Dados.Canais);
    }

    [Fact]
    public async Task Os_grupos_3_e_4_mostram_so_a_potencia_RMS()
    {
        await MostrarCasa();
        await _vm.Adicionar.ExecutarAsync();

        _vm.Grupo = GrupoEquipamento.Repassa;
        Assert.Equal(["potenciaRms"], _vm.SecoesDados.SelectMany(s => s.Dados).Select(d => d.Nome));
        _vm.Grupo = GrupoEquipamento.SoRecebe;
        Assert.Equal(["potenciaRms"], _vm.SecoesDados.SelectMany(s => s.Dados).Select(d => d.Nome));
        _vm.Grupo = GrupoEquipamento.AmplificaETocam;
        Assert.Contains(_vm.SecoesDados.SelectMany(s => s.Dados), d => d.Nome == "xmax");
    }

    [Fact]
    public async Task Quem_nao_toca_nao_mostra_funcao_acustica()
    {
        await MostrarCasa();

        Assert.Null(_vm.Item("Módulo SD800.4 EVO")!.Funcao);
        _vm.AbrirEditar(CasaDoDenis.IdModuloCasa);
        Assert.False(_vm.MostraFuncao);
        Assert.Empty(_vm.Funcoes);
        Assert.Null(_vm.FuncaoSugerida);
        Assert.Null(_vm.FuncaoQueVale);
    }
}
