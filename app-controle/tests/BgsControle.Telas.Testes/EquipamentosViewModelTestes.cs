using BgsControle.Nucleo.Herdado;
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
        _vm.Mostrar(ConhecimentoHerdado.IdSetupCasa);
        await _b.Tela.EsvaziarAsync();
    }

    [Fact]
    public async Task A_lista_do_Casa_2_1_mostra_o_detalhe_o_modelo_ou_generico_e_a_funcao()
    {
        await MostrarCasa();

        Assert.Equal("Casa 2.1", _vm.Titulo);
        Assert.Null(_vm.TextoVazio);
        Assert.Equal(["Módulo genérico", "JBL L", "JBL R", "Sub"], _vm.Itens.Select(i => i.Nome));
        var jblR = _vm.Item("JBL R")!;
        Assert.Equal("Alto-falante passivo · canal 2 do Módulo genérico · CH2", jblR.Detalhe);
        Assert.Equal("JBL 4TRMS80", jblR.Modelo);
        Assert.Equal("médio-grave", jblR.Funcao);
        var sub = _vm.Item("Sub")!;
        Assert.Equal("Alto-falante passivo · bridge 3+4 do Módulo genérico · CH3 + CH4", sub.Detalhe);
        Assert.Equal("genérico", sub.Modelo);
        Assert.Equal("sub", sub.Funcao);
        Assert.Equal("Amplificador · 4 canais · bridge 3+4 · CH1 + CH2 + CH3 + CH4", _vm.Item("Módulo genérico")!.Detalhe);
        Assert.Equal(["JBL L", "JBL R", "Sub"], _vm.Saidas.Select(s => s.Nome));
    }

    [Fact]
    public async Task Tirar_o_modulo_confirma_avisa_e_os_alto_falantes_ficam_sem_ligacao()
    {
        await MostrarCasa();
        var inicio = _b.Simulador.Escritas.Count;

        _vm.Item("Módulo genérico")!.Editar.Execute(null);
        Assert.True(_vm.FolhaAberta);
        Assert.False(_vm.Nova);
        Assert.Equal("Módulo genérico", _vm.TituloFolha);
        Assert.Equal(GrupoEquipamento.Amplificador, _vm.Grupo);
        Assert.Equal(4, _vm.NumeroCanais);
        Assert.Equal("19,6", _vm.Ganho);
        Assert.Equal(["Bridge 1+2", "Bridge 3+4"], _vm.Bridges.Select(b => b.Rotulo));
        Assert.Equal([false, true], _vm.Bridges.Select(b => b.Ligado));
        Assert.Equal(["CH1", "CH2", "CH3", "CH4"], _vm.Entradas.Select(e => e.Fonte.Rotulo));

        await _vm.Tirar.ExecutarAsync();
        Assert.Equal("Tirar Módulo genérico do Casa 2.1?", _vm.ConfirmacaoTirar);
        Assert.Equal("Os alto-falantes ligados nele ficam sem ligação: JBL L, JBL R e Sub.", _vm.AvisoTirar);
        await _b.Rodar(_vm.ConfirmarTirar.ExecutarAsync());

        Assert.False(_vm.FolhaAberta);
        Assert.Equal("Módulo genérico tirado do Casa 2.1. Ficaram sem ligação: JBL L, JBL R e Sub.", _vm.Mensagem);
        Assert.Equal(["JBL L", "JBL R", "Sub"], _vm.Itens.Select(i => i.Nome));
        Assert.Equal("Alto-falante passivo · sem ligação", _vm.Item("JBL R")!.Detalhe);
        Assert.Equal(["CH1", "CH2", "CH3 + CH4"], _vm.Saidas.Select(s => s.Nome));
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
        var casa = ConhecimentoHerdado.IdSetupCasa;
        Assert.True((await _b.Setups.TirarEquipamentoAsync(casa, ConhecimentoHerdado.IdModuloCasa)).Ok);
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
        _vm.Diametro = "12";
        _vm.PotenciaRms = "250,5";

        await _vm.Confirmar.ExecutarAsync();

        Assert.False(_vm.FolhaAberta);
        Assert.Equal("Sub ativo adicionado ao Casa 2.1.", _vm.Mensagem);
        var item = _vm.Item("Sub ativo")!;
        Assert.Equal("Amplifica e toca · CH1", item.Detalhe);
        Assert.Equal("SW 12", item.Modelo);
        Assert.Equal("JBL L + Sub ativo", _vm.Saidas[0].Nome);
        var guardado = _b.Setups!.Setup(ConhecimentoHerdado.IdSetupCasa)!.Equipamentos.Single(e => e.Nome == "Sub ativo");
        Assert.Equal(250.5m, guardado.Dados.PotenciaRms);
        Assert.Equal(12m, guardado.Dados.Diametro);
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
        Assert.Equal(["Sem ligação", "canal 1 do Módulo genérico", "canal 2 do Módulo genérico", "canal 3 do Módulo genérico",
            "canal 4 do Módulo genérico", "bridge 3+4 do Módulo genérico"], _vm.OpcoesLigado.Select(o => o.Rotulo));
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
        _vm.Impedancia = "quatro";

        await _vm.Confirmar.ExecutarAsync();

        Assert.True(_vm.FolhaAberta);
        Assert.Equal("Impedância: escreva um número maior que zero.", _vm.Erro);
        _vm.Impedancia = "4";
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

        _vm.AbrirEditar(ConhecimentoHerdado.IdJblR);

        Assert.Equal("JBL R", _vm.Nome);
        Assert.Equal("JBL 4TRMS80", _vm.Modelo);
        Assert.Equal("médio-grave", _vm.Categoria);
        Assert.False(_vm.CategoriaLivre);
        Assert.Equal("3,6", _vm.Impedancia);
        Assert.Equal("canal 2 do Módulo genérico", _vm.Ligado!.Rotulo);
        _vm.Nome = "JBL R frente";

        await _vm.Confirmar.ExecutarAsync();

        Assert.Equal("JBL R frente guardado.", _vm.Mensagem);
        Assert.Equal("JBL R frente", _vm.Saidas[1].Nome);
        Assert.Contains("100 Hz", _b.Setups!.Equipamento(ConhecimentoHerdado.IdSetupCasa, ConhecimentoHerdado.IdJblR)!.Nota);
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
        Assert.Equal(4, _b.Setups!.Setup(ConhecimentoHerdado.IdSetupCasa)!.Equipamentos.Count);
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
    public async Task Quem_nao_toca_nao_mostra_funcao_acustica()
    {
        await MostrarCasa();

        Assert.Null(_vm.Item("Módulo genérico")!.Funcao);
        _vm.AbrirEditar(ConhecimentoHerdado.IdModuloCasa);
        Assert.False(_vm.MostraFuncao);
        Assert.Empty(_vm.Funcoes);
        Assert.Null(_vm.FuncaoSugerida);
        Assert.Null(_vm.FuncaoQueVale);
    }
}
