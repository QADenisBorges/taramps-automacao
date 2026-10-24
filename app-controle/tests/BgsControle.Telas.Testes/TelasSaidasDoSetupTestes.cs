using BgsControle.Nucleo;
using BgsControle.Nucleo.Escrita;
using BgsControle.Nucleo.Herdado;
using BgsControle.Nucleo.Setups;

namespace BgsControle.Telas.Testes;

/// <summary>
/// História 10, a matriz da spec contra o simulador: as telas com as saídas do Setup em uso
/// (<see cref="GerenteSetups.SaidasEmUso"/>), o equilíbrio de qualquer saída, a tela Setup com a folha de
/// equipamento, os rótulos por saída e as correções da revisão da tentativa 1.
/// </summary>
public sealed class TelasSaidasDoSetupTestes : IDisposable
{
    private const int L = 0, R = 1, Sub = 2;

    private readonly Bancada _b = new(setups: true);
    private readonly Telas10 _t;

    public TelasSaidasDoSetupTestes() => _t = new Telas10(_b);

    public void Dispose()
    {
        _t.Dispose();
        _b.ConferirSoAudio();
        Assert.Empty(_b.Tela.Erros);
    }

    private GerenteSetups Setups => _b.Setups!;

    private async Task DesligarLink34()
    {
        await _b.Rodar(_t.Link.Par(ParCanais.Saidas34).Desligar.ExecutarAsync());
        await _b.Tela.EsvaziarAsync();
        Assert.False(_b.Link.Ligado(ParCanais.Saidas34));
    }

    // ----------------------------------------------------------------- Casa 2.1

    [Fact]
    public async Task Casa_2_1_tres_saidas_com_as_cores_e_o_ganho_do_Sub_no_Ajuste_vai_ao_CH3_e_o_CH4_repete()
    {
        await _b.Conectar();

        Assert.Equal(["JBL L", "JBL R", "Sub"], _t.Inicio.CartoesSaida.Select(c => c.Nome));
        Assert.Equal([0, 1, 2], _t.Inicio.CartoesSaida.Select(c => c.Canal)); // azul, lilás, âmbar
        Assert.Equal(["JBL L", "JBL R", "SUB"], _t.Mesa.Canais.Select(c => c.Nome));
        Assert.Equal(["JBL L", "JBL R", "Sub"], _t.Audio.Tela(FuncaoAudio.NivelSaidas).Abas.Select(a => a.Nome));

        var ganho = _t.Ajuste(Sub).Ganho;
        Assert.Equal(Sub, ganho.Indice);
        ganho.AbrirCampo();
        ganho.TextoCampo = "2";
        await _b.Rodar(ganho.Confirmar.ExecutarAsync());
        await _b.Rodar(ganho.UltimaEscrita);
        await _b.Andar(TimeSpan.FromMilliseconds(500));

        Assert.Equal("+2.0", _b.Simulador.Valor("081", 2));
        Assert.Equal("+2.0", _b.Simulador.Valor("081", 3));
        Assert.Equal("+2,0 dB", ganho.TextoValor);
    }

    [Fact]
    public async Task Link_3_4_desligado_quatro_saidas_com_o_CH4_e_o_aviso_do_bridge_e_as_telas_se_refazem()
    {
        await _b.Conectar();
        var nivel = _t.Audio.Tela(FuncaoAudio.NivelSaidas);
        nivel.EscolherSaida(Sub);
        var ajuste = _t.Ajuste(3); // pedido pelo CH4: com o link, é o Sub
        Assert.Equal("Sub", ajuste.Titulo);
        Assert.Equal(Sub, ajuste.Canal);

        await DesligarLink34();

        Assert.Equal(["JBL L", "JBL R", "Sub · CH3", "Sub · CH4"], _t.Inicio.CartoesSaida.Select(c => c.Nome));
        Assert.Equal([0, 1, 2, 3], _t.Inicio.CartoesSaida.Select(c => c.Canal)); // o CH4 em menta
        Assert.All(_t.Inicio.CartoesSaida.Skip(2), c => Assert.Contains("Link 3–4", c.Aviso));
        Assert.Equal(4, _t.Mesa.Colunas);
        Assert.Equal(3, _t.Mesa.Canal(3)!.Ganho.Indice);
        Assert.Equal(4, nivel.Abas.Count);
        Assert.Equal(Sub, nivel.SaidaEscolhida); // a aba escolhida continua no CH3
        Assert.Contains("Link 3–4", nivel.AvisoLink);
        Assert.Equal("+10,0 · +7,0 · +6,0 · +6,0 dB", _t.Audio.Bloco(FuncaoAudio.NivelSaidas).TextoResumo);
        // O Ajuste aberto pelo CH4 passa a ser o do CH4, com os controles do CH4.
        Assert.Equal("Sub · CH4", ajuste.Titulo);
        Assert.Equal(3, ajuste.Ganho.Indice);
        Assert.Equal(3, ajuste.EquilibrioGanho!.Canal);
    }

    [Fact]
    public async Task Setup_criado_do_aparelho_sem_equipamentos_mostra_CH1_CH2_e_CH3_mais_CH4_e_com_4_equipamentos_sem_link_quatro_saidas()
    {
        await _b.Conectar();
        await _b.Rodar(_t.Inicio.TrocarSetup.ExecutarAsync());
        await _t.Setup.AbrirCriar.ExecutarAsync();
        _t.Setup.NomeNovo = "Bancada";
        await _b.Rodar(_t.Setup.ConfirmarCriar.ExecutarAsync());
        await _b.Tela.EsvaziarAsync();

        Assert.Equal(["CH1", "CH2", "CH3 + CH4"], _t.Inicio.CartoesSaida.Select(c => c.Nome));
        Assert.Equal(["CH1", "CH2", "CH3 + CH4"], _t.Mesa.Canais.Select(c => c.Saida.Nome));

        var id = Setups.SetupEmUso!.Id;
        string[] nomes = ["Caixa 1", "Caixa 2", "Caixa 3", "Caixa 4"];
        for (var c = 0; c < 4; c++)
        {
            var r = await Setups.AdicionarEquipamentoAsync(id,
                new Equipamento("novo", GrupoEquipamento.AmplificaETocam, "caixa ativa", nomes[c])
                {
                    Entradas = [new Entrada(1, FonteSinal.DoCanal(c))],
                });
            Assert.True(r.Ok, r.Motivo);
        }
        await DesligarLink34();

        Assert.Equal(nomes, _t.Inicio.CartoesSaida.Select(c => c.Nome));
        Assert.Equal(nomes.Select(n => n.ToUpperInvariant()), _t.Mesa.Canais.Select(c => c.Nome));
        Assert.Equal(nomes, _t.Audio.Tela(FuncaoAudio.Fase).Abas.Select(a => a.Nome));
        Assert.All(_t.Inicio.CartoesSaida, c => Assert.Null(c.Aviso));
    }

    // ----------------------------------------------------------------- equilíbrio

    [Fact]
    public async Task Tres_toques_seguidos_no_mais_do_equilibrio_da_JBL_R_sobem_0_3_dB_sem_perder_nenhum()
    {
        await _b.Conectar();
        var eq = _t.Ajuste(R).EquilibrioGanho!;
        Assert.Equal("+7.0", _b.Simulador.Valor("081", 1));

        var t1 = eq.Aumentar.ExecutarAsync();
        var t2 = eq.Aumentar.ExecutarAsync();
        var t3 = eq.Aumentar.ExecutarAsync();
        await _b.Rodar(Task.WhenAll(t1, t2, t3));
        await _b.Rodar(eq.UltimoEnvio);

        Assert.Equal("+7.3", _b.Simulador.Valor("081", 1));
        Assert.Equal("−2,7 dB", eq.TextoValor);
        Assert.Equal(-2.7m, Setups.EquilibrioEfetivo.Valor(1, false));
    }

    [Fact]
    public async Task Segurar_o_mais_do_equilibrio_repete_e_soltar_para_sem_perder_o_ultimo()
    {
        await _b.Conectar();
        var eq = _t.Ajuste(L).EquilibrioLimitador!;

        await eq.ApertarMais.ExecutarAsync();
        await _b.Andar(TimeSpan.FromMilliseconds(1000));
        await eq.SoltarPasso.ExecutarAsync();
        await _b.Rodar(eq.UltimoEnvio);
        await _b.Andar(TimeSpan.FromMilliseconds(500));

        var final = Setups.EquilibrioEfetivo.Valor(0, true);
        Assert.True(final > 0.1m, $"segurar repetiu o passo ({final})");
        Assert.Equal(final, eq.Valor);
        Assert.Null(eq.Aviso);
    }

    [Fact]
    public async Task O_equilibrio_do_Sub_vai_aos_dois_canais_e_o_Salvar_diz_o_nome_da_saida()
    {
        await _b.Conectar();
        var eq = _t.Ajuste(Sub).EquilibrioGanho!;

        await eq.TocarNoValor.ExecutarAsync();
        eq.TextoCampo = "-2";
        await _b.Rodar(eq.Confirmar.ExecutarAsync());
        await _b.Rodar(eq.UltimoEnvio);
        await _b.Andar(TimeSpan.FromMilliseconds(500));

        Assert.Equal("+4.0", _b.Simulador.Valor("081", 2));
        Assert.Equal("+4.0", _b.Simulador.Valor("081", 3));
        Assert.Equal(-2.0m, Setups.EquilibrioEfetivo.Valor(2, false));
        Assert.Equal(-2.0m, Setups.EquilibrioEfetivo.Valor(3, false));
        var salvar = _t.Ajuste(Sub).Salvar;
        await salvar.Abrir.ExecutarAsync();
        Assert.Equal(["Equilíbrio do Sub · ganho: 0,0 dB → −2,0 dB"], Assert.Single(salvar.Linhas).Mudancas);
        await salvar.Cancelar.ExecutarAsync();
    }

    // ----------------------------------------------------------------- Salvar e dúvida

    [Fact]
    public async Task Salvar_no_Sub_o_atraso_e_uma_linha_so_com_o_nome_da_saida_e_o_valor_na_cor_do_CH3()
    {
        await _b.Conectar();
        var atraso = _t.Ajuste(Sub).Controle("050")!;
        atraso.Expandir();
        atraso.AbrirCampo();
        atraso.TextoCampo = "85";
        await _b.Rodar(atraso.Confirmar.ExecutarAsync());
        await _b.Rodar(atraso.UltimaEscrita);
        await _b.Andar(TimeSpan.FromMilliseconds(500));

        var salvar = _t.Ajuste(Sub).Salvar;
        await salvar.Abrir.ExecutarAsync();
        var linha = Assert.Single(salvar.Linhas);
        Assert.Equal(["Atraso do Sub: 80 cm → 85 cm"], linha.Mudancas);
        var item = Assert.Single(linha.Itens);
        Assert.Equal("Atraso do Sub: 80 cm → ", item.Antes);
        Assert.Equal("85 cm", item.Novo);
        Assert.Equal(Sub, item.Canal);
        await salvar.Cancelar.ExecutarAsync();
    }

    [Fact]
    public async Task Duvida_com_a_Mesa_aberta_a_casca_poe_a_escolha_por_cima_e_depois_volta_ao_Inicio()
    {
        _b.Simulador.Definir("050", 2, "0.0");
        _t.Casca.Ir(Aba.Mesa);
        await _b.Conectar();
        await _b.Tela.EsvaziarAsync();

        Assert.Equal((true, false), _t.Nav.PorCima);
        Assert.Equal(["Atraso do Sub: 80 cm no Casa 2.1, 0 cm no aparelho"], _t.Setup.Diferencas);

        await _b.Rodar(_t.Setup.Item("Casa 2.1")!.Escolher.ExecutarAsync());
        await _b.Tela.EsvaziarAsync();

        Assert.Equal((false, false), _t.Nav.PorCima);
        Assert.Contains("inicio", _t.Nav.Pedidos);
        Assert.True(_t.Casca.NoInicio);
    }

    // ----------------------------------------------------------------- tela Setup

    [Fact]
    public async Task Configuracoes_Setups_tocar_no_Casa_2_1_abre_a_tela_Setup_com_os_equipamentos()
    {
        await _b.Conectar();
        await _b.Tela.EsvaziarAsync();

        await _t.Setups.Linha("Casa 2.1")!.Abrir.ExecutarAsync();
        Assert.Equal([$"setup:{ConhecimentoHerdado.IdSetupCasa}"], _t.Nav.Pedidos.Where(p => p.StartsWith("setup:")));

        using var tela = new EquipamentosViewModel(Setups, _b.Tela, _b.Relogio);
        tela.Mostrar(ConhecimentoHerdado.IdSetupCasa);
        await _b.Tela.EsvaziarAsync();

        Assert.Equal("Casa 2.1", tela.Titulo);
        Assert.Equal("HERDADO", tela.Selo);
        Assert.Equal(["Módulo genérico", "JBL L", "JBL R", "Sub"], tela.Itens.Select(i => i.Nome));
        Assert.True(tela.Item("Sub")!.Generico);
        Assert.False(tela.Item("JBL R")!.Generico);
        Assert.Equal(1, tela.Item("JBL R")!.Canal);
        Assert.Equal(["JBL L", "JBL R", "Sub"], tela.SaidasTela.Select(s => s.Nome));

        // Tocar abre a folha; tirar o módulo pede confirmação com o aviso dos alto-falantes.
        await tela.Item("Módulo genérico")!.Editar.ExecutarAsync();
        Assert.True(tela.FolhaAberta);
        Assert.Contains(tela.GruposTela, g => g.Escolhida && g.Rotulo == Ligacoes.RotuloGrupo(GrupoEquipamento.Amplificador));
        Assert.Contains(tela.Bridges, b => b.Ligado && b.Rotulo == "Bridge 3+4");
        await tela.Tirar.ExecutarAsync();
        Assert.Equal("Tirar Módulo genérico do Casa 2.1?", tela.ConfirmacaoTirar);
        Assert.Contains("JBL L", tela.AvisoTirar);
        await tela.CancelarTirar.ExecutarAsync();
        await tela.Cancelar.ExecutarAsync();
        Assert.False(tela.FolhaAberta);
    }

    [Fact]
    public async Task Na_folha_as_pilulas_escolhem_e_marcam_a_escolhida()
    {
        await _b.Conectar();
        using var tela = new EquipamentosViewModel(Setups, _b.Tela, _b.Relogio);
        tela.Mostrar(ConhecimentoHerdado.IdSetupCasa);

        await tela.Adicionar.ExecutarAsync();
        var tweeter = tela.CategoriasTela.First(c => c.Rotulo == "tweeter");
        await tweeter.Escolher.ExecutarAsync();
        Assert.True(tela.CategoriasTela.Single(c => c.Rotulo == "tweeter").Escolhida);
        Assert.Equal("Tweeter", tela.Nome);

        var lugar = tela.LigadoTela.First(o => o.Rotulo.StartsWith("canal 1", StringComparison.Ordinal));
        await lugar.Escolher.ExecutarAsync();
        Assert.True(tela.LigadoTela.Single(o => o.Rotulo == lugar.Rotulo).Escolhida);

        await tela.CategoriasTela.Last().Escolher.ExecutarAsync(); // "Outra…"
        Assert.True(tela.CategoriaLivre);
        await tela.Cancelar.ExecutarAsync();
    }

    // ----------------------------------------------------------------- correções da tentativa 1

    [Fact]
    public async Task A_pilula_desconectada_abre_a_tela_de_conexao()
    {
        await _t.Casca.TocarPilula.ExecutarAsync();

        Assert.Equal(["conexao"], _t.Nav.Pedidos);
        Assert.Equal(EstadoSessao.Desconectado, _b.Sessao.Estado); // abrir não conecta sozinho
        Assert.True(_t.Conexao.PodeConectar);
    }

    [Fact]
    public async Task Tocar_na_memoria_em_uso_na_lista_fecha_a_lista()
    {
        await _b.Conectar();
        await _t.Botoes.AbrirLista.ExecutarAsync();
        var filme = Assert.Single(_t.Botoes.LinhasCelular);
        Assert.True(filme.EmUso);
        var antes = _b.Simulador.Escritas.Count;

        await filme.Tocar.ExecutarAsync();

        Assert.False(_t.Botoes.ListaAberta);
        Assert.Equal(antes, _b.Simulador.Escritas.Count);
    }

    [Fact]
    public async Task O_resumo_do_Crossover_mostra_o_passa_alta_e_o_passa_baixa_quando_os_dois_estao_ligados()
    {
        _b.Simulador.Definir("042", 0, "2");
        _b.Simulador.Definir("043", 0, "2");
        await _b.Conectar();
        await _b.Tela.EsvaziarAsync();

        var primeiro = _t.Audio.Bloco(FuncaoAudio.Crossover).Resumo[0].Texto;
        Assert.StartsWith("PA ", primeiro);
        Assert.Contains(" PB ", primeiro);
    }

    [Fact]
    public async Task Sem_leitura_os_resumos_de_filtro_mostram_traco()
    {
        await _b.Tela.EsvaziarAsync();

        Assert.All(_t.Mesa.Canais, c => Assert.Equal("PA — · PB — · Lim —", c.Resumo));
        Assert.Equal("PA — · PB —", _t.Audio.Tela(FuncaoAudio.Crossover).Abas[0].Resumo);
        Assert.Equal("PA — · PB —", _t.Inicio.CartoesSaida[0].Resumo1);
    }

    [Fact]
    public async Task ComandoCom_nao_lanca_para_fora_e_nao_roda_duas_vezes_em_paralelo()
    {
        var vezes = 0;
        var porta = new TaskCompletionSource();
        var comando = new ComandoCom<int>(async _ =>
        {
            vezes++;
            await porta.Task;
            throw new InvalidOperationException("falhou");
        });

        var primeiro = comando.ExecutarAsync(1);
        await comando.ExecutarAsync(2); // chega com o primeiro rodando: ignorado
        Assert.True(comando.Rodando);
        porta.SetResult();
        await primeiro;

        Assert.Equal(1, vezes);
        Assert.IsType<InvalidOperationException>(comando.UltimoErro);
        Assert.False(comando.Rodando);
        comando.Execute("não é número"); // parâmetro que não converte: nada, sem exceção
    }
}
