using System.Reflection;
using System.Text.RegularExpressions;
using BgsControle.Nucleo.Seguranca;
using BgsControle.Nucleo.Setups;
using BgsControle.Simulador;

namespace BgsControle.Telas.Testes;

/// <summary>
/// A folha da proposta de ficha (história 14b), sem XAML: cada dado com o rótulo, o valor, a origem, a fonte e o aviso;
/// "Usar" e "Recusar" por dado e para todos; só o "Salvar" grava, e só os usados. A proposta chega como a porta a
/// entrega (<see cref="PropostaFicha.Receber"/>). Os dados são genéricos.
/// </summary>
public sealed class PropostaFichaViewModelTestes : IDisposable
{
    private const string Modelo = "ABC 123";
    private const string Fonte = "https://exemplo.com/abc-123.pdf";
    private static readonly Regex PalavraIa = new(@"\bIA\b", RegexOptions.CultureInvariant);

    private readonly Bancada _b = new(setups: true);
    private readonly PropostaFicha _ficha;
    private readonly PropostaFichaViewModel _vm;

    public PropostaFichaViewModelTestes()
    {
        _ficha = new PropostaFicha(_b.Setups!, _b.Log, _b.Relogio);
        _vm = new PropostaFichaViewModel(_ficha, _b.Tela, _b.Relogio);
    }

    public void Dispose()
    {
        _vm.Dispose();
        _ficha.Dispose();
        Assert.Empty(_b.Simulador.Escritas); // nada vai ao aparelho
        Assert.Empty(_b.Tela.Erros);
    }

    private static readonly DadoProposto[] TresDados =
    [
        new("fs", 46m, OrigemDado.Oficial, Fonte),
        new("qes", 0.42m, OrigemDado.OutraFonte, "Fórum de áudio, tópico do ABC 123"),
        new("xmax", 6.5m, OrigemDado.Oficial, Fonte, "Qts não fecha com Qes e Qms"),
    ];

    /// <summary>O woofer com o modelo fora da base salvo com a chave ligada, e a proposta dos 3 dados na folha.</summary>
    private async Task<string> ComProposta(DadosTecnicos? dados = null)
    {
        await _b.Chaves.DefinirAsync(Chave.ControlePelaRede, true, OrigemComando.Tela);
        var r = await _b.Setups!.AdicionarEquipamentoAsync(CasaDoDenis.IdSetupCasa,
            new Equipamento("x", GrupoEquipamento.AltoFalante, "woofer", "Woofer extra", Modelo) { Dados = dados ?? DadosTecnicos.Nenhum });
        Assert.True(r.Ok, r.Mensagem);
        Assert.True(_ficha.Receber(r.Id, TresDados).Ok);
        await _b.Tela.EsvaziarAsync();
        Assert.True(_vm.Aberta);
        return r.Id!;
    }

    private Equipamento Equipamento(string id) => _b.Setups!.Setup(CasaDoDenis.IdSetupCasa)!.Equipamentos.Single(e => e.Id == id);

    private void SemIa()
    {
        var textos = typeof(PropostaFichaViewModel).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.PropertyType == typeof(string)).Select(p => p.GetValue(_vm) as string)
            .Concat(_vm.Dados.SelectMany(d => new[] { d.Rotulo, d.Valor, d.Origem, d.Fonte, d.Aviso }));
        foreach (var t in textos.OfType<string>())
            Assert.False(PalavraIa.IsMatch(t), t);
    }

    [Fact]
    public async Task A_folha_mostra_cada_dado_com_o_rotulo_o_valor_a_origem_a_fonte_e_o_aviso()
    {
        Assert.False(_vm.Aberta);

        await ComProposta();

        Assert.Equal("Ficha do ABC 123", _vm.Titulo);
        Assert.Equal("Woofer extra", _vm.Equipamento);
        Assert.Equal(["Fs", "Qes", "Xmax"], _vm.Dados.Select(d => d.Rotulo));
        Assert.Equal(["46 Hz", "0,42", "6,5 mm"], _vm.Dados.Select(d => d.Valor));
        Assert.Equal(["oficial ✓", "outra fonte", "oficial ✓"], _vm.Dados.Select(d => d.Origem));
        Assert.Equal(Fonte, _vm.Dados[0].Fonte);
        Assert.Equal([null, null, "Qts não fecha com Qes e Qms"], _vm.Dados.Select(d => d.Aviso));
        Assert.All(_vm.Dados, d => Assert.Null(d.Escolha));
        Assert.Equal(("Usar", "Recusar", "Usar todos", "Recusar todos", "Salvar"),
            (_vm.TextoUsar, _vm.TextoRecusar, _vm.TextoUsarTodos, _vm.TextoRecusarTodos, _vm.TextoSalvar));
        SemIa();
    }

    [Fact]
    public async Task Usar_em_2_e_Recusar_em_1_o_Salvar_grava_os_2_na_ficha_e_na_base()
    {
        var id = await ComProposta();

        await _vm.Dados[0].Usar.ExecutarAsync();
        await _vm.Dados[1].Recusar.ExecutarAsync();
        await _vm.Dados[2].Usar.ExecutarAsync();
        Assert.Equal(2, _vm.Usados);
        Assert.True(_vm.Dados[1].Recusado);
        Assert.Null(Equipamento(id).Dados.Fs); // nada antes do Salvar

        await _b.Rodar(_vm.Salvar.ExecutarAsync());

        var dados = Equipamento(id).Dados;
        Assert.Equal(new DadoTecnico(46m, OrigemDado.Oficial, Fonte), dados.Fs);
        Assert.Equal(new DadoTecnico(6.5m, OrigemDado.Oficial, Fonte), dados.Xmax);
        Assert.Null(dados.Qes); // o recusado fica em branco
        var naBase = _b.Modelos!.Ficha(Modelo)!.Dados;
        Assert.Equal(46m, naBase.Fs!.Valor);
        Assert.Equal(6.5m, naBase.Xmax!.Valor);
        Assert.Null(naBase.Qes);
        Assert.False(_vm.Aberta);
        Assert.Equal("2 dados salvos na ficha do Woofer extra.", _vm.AvisoFinal);
    }

    [Fact]
    public async Task Usar_todos_e_Recusar_todos_marcam_cada_dado_e_tudo_recusado_nao_grava_nada()
    {
        var id = await ComProposta();

        await _vm.UsarTodos.ExecutarAsync();
        Assert.Equal(3, _vm.Usados);
        Assert.All(_vm.Dados, d => Assert.True(d.Usado));

        await _vm.RecusarTodos.ExecutarAsync();
        Assert.Equal(0, _vm.Usados);
        Assert.All(_vm.Dados, d => Assert.True(d.Recusado));

        await _b.Rodar(_vm.Salvar.ExecutarAsync());
        Assert.False(_vm.Aberta);
        Assert.True(Equipamento(id).Dados.Vazia);
        Assert.True(_b.Modelos!.Ficha(Modelo)!.Dados.Vazia);
    }

    [Fact]
    public async Task Fechar_sem_salvar_nao_grava_nada()
    {
        var id = await ComProposta();
        await _vm.UsarTodos.ExecutarAsync();

        await _vm.Fechar.ExecutarAsync();
        await _b.Tela.EsvaziarAsync();

        Assert.False(_vm.Aberta);
        Assert.Empty(_vm.Dados);
        Assert.True(Equipamento(id).Dados.Vazia);
        Assert.True(_b.Modelos!.Ficha(Modelo)!.Dados.Vazia);
    }

    [Fact]
    public async Task Cada_dado_mostra_o_valor_de_agora_e_Usar_todos_nao_troca_um_medido()
    {
        var id = await ComProposta(DadosTecnicos.Nenhum with
        {
            Fs = new DadoTecnico(44m, OrigemDado.Medido, "Medido com o UMIK"),
            Qes = DadoTecnico.Digitado(0.5m),
        });

        Assert.Equal(["44 Hz", "0,5", null], _vm.Dados.Select(d => d.ValorAtual));
        Assert.Equal(["medido ✓", "digitado", null], _vm.Dados.Select(d => d.OrigemAtual));
        // História 14c: o texto do valor de agora na folha.
        Assert.Equal(["Agora: 44 Hz (medido ✓)", "Agora: 0,5 (digitado)", null], _vm.Dados.Select(d => d.TextoAtual));
        Assert.True(_vm.Dados[0].AtualMedido);

        await _vm.UsarTodos.ExecutarAsync();
        Assert.Null(_vm.Dados[0].Escolha); // o medido pede o "Usar" dele
        Assert.True(_vm.Dados[1].Usado);
        Assert.True(_vm.Dados[2].Usado);
        await _b.Rodar(_vm.Salvar.ExecutarAsync());
        Assert.Equal(new DadoTecnico(44m, OrigemDado.Medido, "Medido com o UMIK"), Equipamento(id).Dados.Fs);
        Assert.Equal(0.42m, Equipamento(id).Dados.Qes!.Valor);
    }

    [Fact]
    public async Task O_Usar_de_um_dado_medido_troca_pelo_toque_do_usuario()
    {
        var id = await ComProposta(DadosTecnicos.Nenhum with { Fs = new DadoTecnico(44m, OrigemDado.Medido, "Medido com o UMIK") });

        await _vm.Dados[0].Usar.ExecutarAsync();
        await _b.Rodar(_vm.Salvar.ExecutarAsync());

        Assert.Equal(new DadoTecnico(46m, OrigemDado.Oficial, Fonte), Equipamento(id).Dados.Fs);
    }

    // ----------------------------------------------------------------- história 14c: a folha global na casca

    /// <summary>A casca do app com a folha da proposta de ficha (as outras telas como no app).</summary>
    private CascaViewModel Casca(Telas11 telas) =>
        new(_b.Sessao, telas.Conexao, telas.Setup, telas.Memorias, telas.Inicio, telas.Mesa, telas.Audio, telas.Salvar, _b.Fabrica, telas.Nav,
            _b.Tela, _b.Relogio, telas.PlayerTela, telas.FolhaRisco, telas.Gerador, telas.Reaplicando, ficha: _vm);

    [Fact]
    public async Task A_folha_da_ficha_sobe_na_casca_e_o_aviso_depois_de_salvar_vai_ao_aviso_rapido()
    {
        using var telas = new Telas11(_b);
        using var casca = Casca(telas);
        casca.SincronizarFolhas(forcar: true);
        Assert.False(casca.FolhaFichaPorCima);
        Assert.Equal((false, false, false), telas.Nav.Folhas);

        await ComProposta();

        Assert.True(casca.FolhaFichaPorCima);
        Assert.Equal((false, false, true), telas.Nav.Folhas);

        await _vm.Dados[0].Usar.ExecutarAsync();
        await _vm.Dados[2].Usar.ExecutarAsync();
        await _b.Rodar(_vm.Salvar.ExecutarAsync());
        await _b.Tela.EsvaziarAsync();

        Assert.False(casca.FolhaFichaPorCima);
        Assert.Equal((false, false, false), telas.Nav.Folhas);
        Assert.Equal("2 dados salvos na ficha do Woofer extra.", casca.AvisoRapido);
        await _b.Andar(TimeSpan.FromSeconds(2.7), passoMs: 100);
        Assert.Null(casca.AvisoRapido);
    }

    [Fact]
    public async Task Fechar_a_folha_da_ficha_a_tira_da_casca_sem_aviso()
    {
        using var telas = new Telas11(_b);
        using var casca = Casca(telas);
        casca.SincronizarFolhas(forcar: true);
        await ComProposta();
        Assert.Equal((false, false, true), telas.Nav.Folhas);

        await _vm.Fechar.ExecutarAsync();
        await _b.Tela.EsvaziarAsync();

        Assert.False(casca.FolhaFichaPorCima);
        Assert.Equal((false, false, false), telas.Nav.Folhas);
        Assert.Null(casca.AvisoRapido);
    }

    [Fact]
    public void O_valor_mostra_todos_os_algarismos_sem_zeros_sobrando()
    {
        Assert.Equal("46 Hz", PropostaFichaViewModel.Texto(46.000m, "Hz"));
        Assert.Equal("0,000012 mH", PropostaFichaViewModel.Texto(0.000012m, "mH"));
        Assert.Equal("3,123456 Ω", PropostaFichaViewModel.Texto(3.123456m, "Ω"));
        Assert.Equal("0,42", PropostaFichaViewModel.Texto(0.42m, null));
    }
}
