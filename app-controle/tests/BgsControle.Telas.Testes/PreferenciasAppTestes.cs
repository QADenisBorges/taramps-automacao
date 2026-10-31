namespace BgsControle.Telas.Testes;

/// <summary>
/// História 10d: a chave "Animação ao criar o som" nas Configurações do app, ligada por padrão e guardada em JSON
/// pelos arquivos privados (preferencias-app.json), sem o Preferences do MAUI.
/// </summary>
public sealed class PreferenciasAppTestes
{
    private readonly DespachanteFila _tela = new();
    private readonly NavegacaoFalsa _nav = new();

    private static PreferenciasApp Preferencias(ArquivosFalso arquivos) =>
        new(new RepositorioTextoArquivos(arquivos, RepositorioTextoArquivos.ArquivoPreferencias));

    [Fact]
    public async Task Sem_arquivo_a_animacao_vem_ligada()
    {
        var p = Preferencias(new ArquivosFalso());
        await p.Carregada;

        Assert.True(p.AnimacaoCriacao);
    }

    [Fact]
    public async Task Arquivo_ilegivel_fica_ligada()
    {
        var arquivos = new ArquivosFalso();
        arquivos.Semear(RepositorioTextoArquivos.ArquivoPreferencias, "{ não é json");
        var p = Preferencias(arquivos);
        await p.Carregada;

        Assert.True(p.AnimacaoCriacao);
    }

    [Fact]
    public async Task Desligar_nas_Configuracoes_e_reabrir_o_app_fica_desligada()
    {
        var arquivos = new ArquivosFalso();
        var p = Preferencias(arquivos);
        var vm = new ConfiguracoesViewModel(_nav, _tela, TimeProvider.System, preferencias: p);
        Assert.True(vm.ComPreferencias);
        Assert.Equal("Animação ao criar o som", vm.TituloAnimacao);
        Assert.Equal("Mostra cada ajuste na tela, um por vez, enquanto a memória é montada. Desligada, aparece só o \"Aplicando\".",
            vm.DescricaoAnimacao);
        Assert.True(vm.AnimacaoLigada);
        Assert.Equal("Ligada", vm.TextoEstadoAnimacao);

        await vm.DesligarAnimacao.ExecutarAsync();
        await _tela.EsvaziarAsync();

        Assert.False(vm.AnimacaoLigada);
        Assert.Equal("Desligada", vm.TextoEstadoAnimacao);
        Assert.Equal("""{"animacaoCriacao":false}""", arquivos.Conteudo(RepositorioTextoArquivos.ArquivoPreferencias));

        var reaberta = Preferencias(arquivos); // o app reabre com os mesmos arquivos privados
        await reaberta.Carregada;
        Assert.False(reaberta.AnimacaoCriacao);

        await vm.LigarAnimacao.ExecutarAsync();
        await _tela.EsvaziarAsync();
        Assert.True(vm.AnimacaoLigada);
        Assert.True(p.AnimacaoCriacao);
    }

    [Fact]
    public async Task Falha_ao_guardar_avisa_e_nao_muda()
    {
        var arquivos = new ArquivosFalso();
        var p = Preferencias(arquivos);
        var vm = new ConfiguracoesViewModel(_nav, _tela, TimeProvider.System, preferencias: p);
        arquivos.Falhar = true;

        await vm.DesligarAnimacao.ExecutarAsync();
        await _tela.EsvaziarAsync();

        Assert.True(vm.AnimacaoLigada);
        Assert.Equal(ConfiguracoesViewModel.MsgNaoGuardouPreferencia, vm.ErroAnimacao);
    }

    [Fact]
    public void Sem_preferencias_a_linha_nao_aparece()
    {
        var vm = new ConfiguracoesViewModel(_nav, _tela, TimeProvider.System);

        Assert.False(vm.ComPreferencias);
        Assert.True(vm.AnimacaoLigada);
    }
}
