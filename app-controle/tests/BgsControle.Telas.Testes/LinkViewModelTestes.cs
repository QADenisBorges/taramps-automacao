using BgsControle.Nucleo.Escrita;
using BgsControle.Nucleo.Herdado;
using BgsControle.Nucleo.Setups;

namespace BgsControle.Telas.Testes;

/// <summary>
/// O link 1–2 e 3–4 na tela (histórias 6 e 8f) contra o processador simulado: só de leitura, uma linha por
/// par ligado ("CH3 e CH4 ligados: alimentam o mesmo bridge"); o link sai das ligações do Setup em uso.
/// </summary>
public sealed class LinkViewModelTestes : IDisposable
{
    private readonly Bancada _b = new(setups: true);
    private readonly LinkViewModel _vm;

    public LinkViewModelTestes() => _vm = _b.CriarLinkViewModel();

    private GerenteSetups Setups => _b.Setups!;

    public void Dispose()
    {
        _vm.Dispose();
        _b.ConferirSoAudio();
        Assert.Empty(_b.Tela.Erros);
    }

    [Fact]
    public async Task Sem_Setup_em_uso_nenhuma_linha()
    {
        await _b.Link.Carregada;
        await _b.Tela.EsvaziarAsync();

        Assert.Empty(_vm.Linhas);
        Assert.False(_vm.TemLink);
    }

    [Fact]
    public async Task O_Casa_2_1_conferido_mostra_so_a_linha_do_CH3_e_do_CH4_sem_controle_nenhum()
    {
        await _b.Conectar();
        await Apoio.Esperar(() => Setups.Conferido);
        await _b.Tela.EsvaziarAsync();

        var linha = Assert.Single(_vm.Linhas);
        Assert.Equal(ParCanais.Saidas34, linha.Par);
        Assert.Equal("CH3 e CH4 ligados: alimentam o mesmo bridge", linha.Frase);
        Assert.Null(linha.AvisoDiferenca);
        Assert.True(_vm.TemLink);
    }

    [Fact]
    public async Task Tirar_o_CH4_do_bridge_tira_a_linha()
    {
        await _b.Conectar();
        await Apoio.Esperar(() => Setups.Conferido);
        var modulo = Setups.Equipamento(ConhecimentoHerdado.IdSetupCasa, ConhecimentoHerdado.IdModuloCasa)!;

        var r = await Setups.EditarEquipamentoAsync(ConhecimentoHerdado.IdSetupCasa,
            modulo with { Entradas = modulo.Entradas.Select(x => x.Numero == 4 ? x with { Fonte = null } : x).ToArray() });
        await _b.Tela.EsvaziarAsync();

        Assert.True(r.Ok, r.Mensagem);
        Assert.Empty(_vm.Linhas);
    }

    [Fact]
    public async Task Ligado_com_as_saidas_diferentes_depois_da_leitura_so_avisa()
    {
        _b.Simulador.Definir("081", 3, "-7.0"); // o CH4 diferente do CH3, mudado fora do app
        await _b.ConectarSemReaplicar(); // sem o reaplicar sozinho da 9c, que desfaria a diferença
        await Apoio.Esperar(() => Setups.Conferido && _b.Link.Ligado(ParCanais.Saidas34));
        // A próxima leitura completa, com o link ligado, acha o par diferente.
        await _b.Sessao.DesconectarAsync();
        await _b.ConectarSemReaplicar();
        await Apoio.Esperar(() => _b.Link.Diferentes(ParCanais.Saidas34));
        await _b.Tela.EsvaziarAsync();

        Assert.Equal("Link 3–4: as saídas estão diferentes", Assert.Single(_vm.Linhas).AvisoDiferenca);
        Assert.Equal("-7.0", _b.Simulador.Valor("081", 3)); // nada escrito sozinho
    }
}
