using System.Reflection;
using System.Text.RegularExpressions;
using BgsControle.Nucleo;
using BgsControle.Nucleo.Escrita;
using BgsControle.Nucleo.Rendimento;
using BgsControle.Nucleo.Setups;
using BgsControle.Simulador;

namespace BgsControle.Telas.Testes;

/// <summary>
/// História 13g, o limite da saída no Ajuste da saída (o <see cref="LimiteSaidaViewModel"/>) contra o processador simulado:
/// o Casa 2.1 calibrado com o corte da JBL L em 200 Hz ("17,9 V · −6,8 dB · pelo térmico · verificado" e a conta com T, X e C
/// e as origens), o Sub no bridge, a faixa de sem calibração com o "Calibrar ›", a linha do nível em uso e a folha "A
/// conta". E a navegação nova das telas do Rendimento (o "Calibrar saídas", o "Calibrar ›" da folha do nível) e a recusa
/// da cerca no aviso rápido da casca. Nenhum texto com "IA".
/// </summary>
public sealed class LimiteSaidaViewModelTestes
{
    private static readonly Regex PalavraIa = new(@"\bIA\b", RegexOptions.CultureInvariant);
    private const string IdPlana = "memoria-plana";

    private static Bancada SemCalibracao()
    {
        var arquivos = new ArquivosFalso();
        var dados = CenarioFilme.Dados with
        {
            Setups = [CasaDoDenis.SetupCasa with { Equipamentos = CasaDoDenis.EquipamentosSemCalibracao }],
        };
        arquivos.Semear(RepositorioTextoArquivos.ArquivoSetups, GerenteSetups.ParaJson(dados));
        return new Bancada(arquivos, setups: true);
    }

    /// <summary>O passa-alta da JBL L em 200 Hz (o tipo é o da leitura da FILME), como no teste da cerca na tela.</summary>
    private static async Task CortarEm200(Bancada b)
    {
        var corte = b.Escritor.EscreverAsync(Catalogo.ParametroDe("040")!, 0, 200m);
        await b.Rodar(corte);
        Assert.True((await corte).Ok);
        await b.Tela.EsvaziarAsync();
    }

    private static void SemIa(LimiteSaidaViewModel vm)
    {
        foreach (var p in typeof(LimiteSaidaViewModel).GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.PropertyType == typeof(string)))
        {
            var texto = (string?)p.GetValue(vm);
            Assert.False(texto is not null && PalavraIa.IsMatch(texto), $"{p.Name} mostra a IA: \"{texto}\"");
        }
        foreach (var l in vm.Limites)
        {
            foreach (var texto in new[] { l.Nome, l.Valor, l.Selo, l.Conta, l.Falta }.Concat(l.Dados))
                Assert.False(texto is not null && PalavraIa.IsMatch(texto), texto);
        }
    }

    [Fact]
    public async Task JBL_L_calibrada_com_o_corte_em_200_Hz_mostra_o_teto_o_que_manda_e_o_selo_e_a_conta_com_as_origens()
    {
        var b = new Bancada(setups: true);
        var nav = new NavegacaoFalsa();
        using var vm = new LimiteSaidaViewModel(0, b.Setups!, b.Estado, b.Link, nav, b.Tela, b.Relogio);
        await b.Conectar();
        await CortarEm200(b);

        Assert.Null(b.Setups!.NivelEmUso);
        Assert.True(vm.Visivel);
        Assert.Equal("LIMITE DA SAÍDA", vm.Rotulo);
        Assert.Equal("17,9 V · −6,8 dB · pelo térmico · verificado", vm.Linha);
        Assert.Null(vm.FaixaCalibracao);
        Assert.Null(vm.MotivoSemLimite);
        Assert.Null(vm.TextoNivel);
        Assert.True(vm.PodeAbrirConta);
        Assert.Equal("Toque para ver a conta.", vm.Dica);
        Assert.Equal("Limite da saída: 17,9 V · −6,8 dB · pelo térmico · verificado", vm.Descricao);

        // A conta: os três limites, com o valor, a conta, os dados com a origem e o que manda.
        Assert.Equal("Alto-falante que manda: JBL L", vm.AltoFalante);
        Assert.Equal("Ganho do módulo: 19,5 × · medido ✓", vm.Ganho);
        Assert.Equal(["Térmico", "Curso do cone", "Ceifamento do módulo"], vm.Limites.Select(l => l.Nome));
        var t = vm.Limites[0];
        Assert.Equal(TipoLimite.Termico, t.Tipo);
        Assert.Equal("17,9 V", t.Valor);
        Assert.Equal("verificado", t.Selo);
        Assert.Equal("√(80 W × 4 Ω) = 17,9 V", t.Conta);
        Assert.Equal(["Potência RMS: 80 W · oficial ✓", "Impedância: 4 Ω · oficial ✓"], t.Dados);
        Assert.True(t.Manda);
        Assert.Equal("manda", t.TextoManda);
        Assert.Null(t.Falta);
        var x = vm.Limites[1];
        Assert.False(x.Manda);
        Assert.Null(x.TextoManda);
        Assert.Equal("estimado", x.Selo); // o volume da caixa é estimado
        Assert.StartsWith("Xmax de 1,75 mm na pior frequência", x.Conta);
        Assert.Contains("Re: 3,6 Ω · medido ✓", x.Dados);
        Assert.Contains("Tipo da caixa: selada · medido ✓", x.Dados);
        Assert.Contains("Volume da caixa: 3,5 L · estimado", x.Dados);
        Assert.Contains(x.Dados, d => d.StartsWith("Passa-alta: ", StringComparison.Ordinal) && d.EndsWith(" em 200 Hz · lido do aparelho", StringComparison.Ordinal));
        var c = vm.Limites[2];
        Assert.Equal("23,0 V", c.Valor);
        Assert.Equal("verificado", c.Selo);
        Assert.Equal([$"Ceifamento por canal do {CasaDoDenis.NomeModuloCasa}: 23 V · oficial ✓"], c.Dados);

        // Um toque abre a folha "A conta"; Fechar fecha.
        Assert.False(vm.ContaAberta);
        await vm.AbrirConta.ExecutarAsync();
        Assert.True(vm.ContaAberta);
        Assert.Equal("A conta", vm.Titulo);
        Assert.Equal("Fechar", vm.TextoBotaoFechar);
        await vm.FecharConta.ExecutarAsync();
        Assert.False(vm.ContaAberta);
        Assert.Empty(nav.Pedidos);
        SemIa(vm);
        b.ConferirSoAudio();
        Assert.Empty(b.Tela.Erros);
    }

    [Fact]
    public async Task O_Sub_no_bridge_usa_o_ganho_e_o_ceifamento_da_ponte()
    {
        var b = new Bancada(setups: true);
        using var vm = new LimiteSaidaViewModel(3, b.Setups!, b.Estado, b.Link, new NavegacaoFalsa(), b.Tela, b.Relogio);
        await b.Conectar();

        Assert.True(vm.Visivel);
        Assert.Equal("Alto-falante que manda: Sub", vm.AltoFalante);
        Assert.Equal("Ganho do módulo em ponte: 40,5 × · medido ✓", vm.Ganho);
        Assert.Equal([$"Ceifamento em ponte do {CasaDoDenis.NomeModuloCasa}: 40 V · oficial ✓"], vm.Limites[2].Dados);
        Assert.Matches(@"^\d+,\d V · −?\d+,\d dB · pelo .+ · (verificado|estimado)$", vm.Linha);
        SemIa(vm);
    }

    [Fact]
    public async Task Sem_calibracao_mostra_a_faixa_os_volts_sem_o_dB_e_o_Calibrar_abre_a_calibracao_da_saida()
    {
        var b = SemCalibracao();
        var nav = new NavegacaoFalsa();
        using var vm = new LimiteSaidaViewModel(1, b.Setups!, b.Estado, b.Link, nav, b.Tela, b.Relogio);
        await b.Conectar();

        Assert.True(vm.Visivel);
        Assert.Equal("Sem calibração: o app não sabe a tensão.", vm.FaixaCalibracao);
        Assert.Equal("Calibrar ›", vm.TextoBotaoCalibrar);
        Assert.NotNull(vm.Linha);
        Assert.DoesNotContain(" dB", vm.Linha);
        Assert.Matches(@"^\d+,\d V · pelo .+ · (verificado|estimado)$", vm.Linha);
        Assert.Equal("Ganho do módulo: 19,5 × · digitado", vm.Ganho);

        await vm.Calibrar.ExecutarAsync();

        Assert.Equal(["calibracao:CH2"], nav.Pedidos);
        SemIa(vm);
    }

    /// <summary>História 13g: o Setup em uso sem equipamentos não tem a saída nos limites: o bloco diz o motivo, sem a conta.</summary>
    [Fact]
    public async Task Sem_a_saida_nos_limites_o_bloco_mostra_o_motivo_sem_a_conta()
    {
        var arquivos = new ArquivosFalso();
        arquivos.Semear(RepositorioTextoArquivos.ArquivoSetups,
            GerenteSetups.ParaJson(CenarioFilme.Dados with { Setups = [CasaDoDenis.SetupCasa with { Equipamentos = [] }] }));
        var b = new Bancada(arquivos, setups: true);
        using var vm = new LimiteSaidaViewModel(0, b.Setups!, b.Estado, b.Link, new NavegacaoFalsa(), b.Tela, b.Relogio);
        await b.Conectar();
        Assert.NotNull(b.Setups!.SetupEmUso);
        Assert.Empty(b.Setups.LimitesEmUso);

        Assert.True(vm.Visivel);
        Assert.Null(vm.Linha);
        Assert.Equal(LimiteFisico.MsgSemAltoFalante, vm.MotivoSemLimite);
        Assert.Equal($"Limite da saída: {LimiteFisico.MsgSemAltoFalante}", vm.Descricao);
        Assert.False(vm.PodeAbrirConta);
        Assert.Null(vm.Dica);
        await vm.AbrirConta.ExecutarAsync();
        Assert.False(vm.ContaAberta);
    }

    [Fact]
    public async Task Sem_Setup_em_uso_o_bloco_some_e_a_conta_nao_abre()
    {
        var b = new Bancada(setups: true, zerado: true);
        using var vm = new LimiteSaidaViewModel(0, b.Setups!, b.Estado, b.Link, new NavegacaoFalsa(), b.Tela, b.Relogio);
        await b.Conectar();

        Assert.False(vm.Visivel);
        Assert.Null(vm.Linha);
        Assert.False(vm.PodeAbrirConta);
        await vm.AbrirConta.ExecutarAsync();
        Assert.False(vm.ContaAberta);
    }

    /// <summary>
    /// Com o nível Alto na memória em uso, o teto da linha continua o da cerca (o físico, min(T, X, C)), e a linha de apoio
    /// diz até onde o nível leva o pico (o teto − 1 dB): "Nível Alto: até 16 V.".
    /// </summary>
    [Fact]
    public async Task Com_um_nivel_a_linha_de_apoio_diz_ate_onde_o_nivel_leva_o_pico()
    {
        var plana = CasaDoDenis.LeituraFilme.Select(v => (v.Codigo, v.Indice) switch
        {
            ("081", 0 or 1) => v with { NoFio = "+9.0" },
            ("081", _) => v with { NoFio = "-15.0" },
            ("091", _) or ("030", _) or ("020", _) or ("080", _) => v with { NoFio = "+0.0" },
            ("042", 0 or 1) => v with { NoFio = "3" },
            ("040", 0 or 1) => v with { NoFio = "200" },
            _ => v,
        }).ToArray();
        var regulagem = plana.Where(v => Camadas.De(v.Codigo) == Camada.Regulagem).ToArray();
        var memoria = new MemoriaCelular(IdPlana, CasaDoDenis.IdSetupCasa, "PLANA", CasaDoDenis.QuandoFilme, regulagem);
        var dados = new DadosSetups([CasaDoDenis.SetupCasaComBluetooth], [memoria], CasaDoDenis.IdSetupCasa,
            new Dictionary<string, UsoDoSetup> { [CasaDoDenis.IdSetupCasa] = new([IdPlana], [IdPlana, null, null]) });
        var arquivos = new ArquivosFalso();
        arquivos.Semear(RepositorioTextoArquivos.ArquivoSetups, GerenteSetups.ParaJson(dados));
        var b = new Bancada(arquivos, setups: true);
        foreach (var v in plana)
            b.Simulador.Definir(v.Codigo, v.Indice, v.NoFio);
        using var vm = new LimiteSaidaViewModel(0, b.Setups!, b.Estado, b.Link, new NavegacaoFalsa(), b.Tela, b.Relogio);
        await b.Conectar();
        Assert.Equal(IdPlana, b.Setups!.MemoriaEmUso?.Id);

        await b.Rodar(b.Setups.DefinirNivelAsync(IdPlana, NivelRendimento.Alto));
        await b.Tela.EsvaziarAsync();

        Assert.Equal(NivelRendimento.Alto, b.Setups.NivelEmUso);
        Assert.Equal("17,9 V · −6,8 dB · pelo térmico · verificado", vm.Linha);
        Assert.Equal("Nível Alto: até 16 V.", vm.TextoNivel);
        SemIa(vm);
    }

    /// <summary>O Ajuste da saída leva o limite da saída dele (o mesmo canal).</summary>
    [Fact]
    public async Task O_Ajuste_da_saida_leva_o_limite_da_mesma_saida()
    {
        var b = new Bancada(setups: true);
        var nav = new NavegacaoFalsa();
        using var ajuste = new AjusteSaidaViewModel(0, b.Fabrica, b.Setups!, b.Sessao, b.Chaves, b.Estado, b.Link, b.CriarSalvarViewModel(), nav,
            b.Tela, b.Relogio);
        await b.Conectar();
        await CortarEm200(b);

        Assert.Equal("17,9 V · −6,8 dB · pelo térmico · verificado", ajuste.Limite.Linha);
    }

    // ------------------------------------------------------------------ a navegação nova (história 13g)

    [Fact]
    public void Calibrar_saidas_nas_Configuracoes_abre_a_calibracao_na_escolha()
    {
        var nav = new NavegacaoFalsa();
        var vm = new ConfiguracoesViewModel(nav, new DespachanteFila(), TimeProvider.System);

        Assert.Equal("Calibrar saídas", vm.TituloCalibrar);
        vm.AbrirCalibracao.Execute(null);

        Assert.Equal(["calibracao"], nav.Pedidos);
    }

    [Fact]
    public async Task Na_folha_do_nivel_sem_calibracao_o_Calibrar_fecha_a_folha_e_abre_a_calibracao()
    {
        var b = SemCalibracao();
        var nav = new NavegacaoFalsa();
        using var vm = new NivelViewModel(b.Setups!, b.Estado, b.Tela, b.Relogio, nav: nav);
        await b.Conectar();
        await vm.Abrir.ExecutarAsync();
        await b.Tela.EsvaziarAsync();

        Assert.Equal("Sem calibração: o app não sabe a tensão.", vm.FaixaCalibracao);
        Assert.Equal("Calibrar ›", vm.TextoCalibrar);
        Assert.True(vm.MostrarChip);
        Assert.Equal("Sem nível ›", vm.TextoChip);
        Assert.Equal(vm.Linhas.Count, vm.Itens.Count);
        Assert.All(vm.Itens, i => Assert.True(i.Apagada));

        await vm.Calibrar.ExecutarAsync();

        Assert.False(vm.FolhaAberta);
        Assert.Equal(["calibracao:CH1"], nav.Pedidos);
    }

    [Fact]
    public async Task Com_calibracao_a_folha_do_nivel_nao_tem_a_faixa_e_o_chip_mostra_o_nivel()
    {
        var b = new Bancada(setups: true);
        var nav = new NavegacaoFalsa();
        using var vm = new NivelViewModel(b.Setups!, b.Estado, b.Tela, b.Relogio, nav: nav);
        await b.Conectar();

        Assert.Null(vm.FaixaCalibracao);
        await vm.Calibrar.ExecutarAsync();
        Assert.Empty(nav.Pedidos);
        Assert.Equal("Nível: Alto ›", NivelViewModel.TextoChipDe(NivelRendimento.Alto));
    }

    // ------------------------------------------------------------------ a recusa da cerca no aviso rápido (história 13g)

    [Fact]
    public void As_recusas_da_cerca_comecam_pelo_prefixo_que_a_tela_reconhece()
    {
        Assert.True(ControleParametroViewModel.DaCerca(Cerca.MsgLimite("JBL L", -6.8m, TipoLimite.Termico)));
        Assert.True(ControleParametroViewModel.DaCerca(Cerca.MsgOff("JBL L")));
        Assert.True(ControleParametroViewModel.DaCerca(Cerca.MsgDesconhecido("JBL L")));
        Assert.True(ControleParametroViewModel.DaCerca(Cerca.MsgCorteDesconhecido("JBL L")));
        Assert.True(ControleParametroViewModel.DaCerca(Cerca.MsgFalhou));
        Assert.False(ControleParametroViewModel.DaCerca(ControleParametroViewModel.MsgChaveDesligada));
        Assert.False(ControleParametroViewModel.DaCerca(null));
    }

    [Fact]
    public async Task A_recusa_da_cerca_aparece_no_controle_e_no_aviso_rapido_da_casca_e_nada_sai()
    {
        var b = new Bancada(setups: true);
        using var t = new Telas11(b);
        await b.Conectar();
        await CortarEm200(b);
        var c = b.Fabrica.Criar(Catalogo.ParametroDe("071")!, 0, null);
        c.Expandir();
        c.AbrirCampo();
        c.TextoCampo = "-6,7";

        await c.Confirmar.ExecutarAsync();
        await b.Rodar(c.UltimaEscrita);
        await b.Tela.EsvaziarAsync();

        Assert.Equal("Limite: JBL L, −6,8 dB, pelo térmico.", c.Aviso);
        Assert.Equal("Limite: JBL L, −6,8 dB, pelo térmico.", t.Casca.AvisoRapido);
        Assert.Equal(0, b.EscritasDe("*071*0*"));

        // O aviso rápido some sozinho depois de 2,6 s.
        await b.Andar(TimeSpan.FromSeconds(2.7), passoMs: 100);
        Assert.Null(t.Casca.AvisoRapido);
        c.Dispose();
        b.ConferirSoAudio();
        Assert.Empty(b.Tela.Erros);
    }
}
