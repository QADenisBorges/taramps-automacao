using BgsControle.Nucleo.Seguranca;

namespace BgsControle.Telas.Testes;

/// <summary>A tela Chaves de acesso (história 5): lista, folha de risco e persistência.</summary>
public sealed class ChavesViewModelTestes : IDisposable
{
    private readonly Bancada _b = new();
    private readonly ChavesViewModel _vm;

    public ChavesViewModelTestes()
    {
        _vm = new ChavesViewModel(_b.Chaves, _b.Tela, _b.Relogio);
    }

    public void Dispose()
    {
        _vm.Dispose();
        _b.ConferirSoAudio();
        Assert.Empty(_b.Tela.Erros);
    }

    private async Task Pronto()
    {
        await _b.Chaves.Carregada;
        await Task.Delay(20);
        await _b.Tela.EsvaziarAsync();
    }

    [Fact]
    public async Task Lista_as_10_chaves_com_o_estado_padrao()
    {
        await Pronto();

        Assert.Equal(10, _vm.Itens.Count);
        Assert.Equal(ChavesPadrao.Todas.Select(d => d.Nome), _vm.Itens.Select(i => i.Nome));
        // História 8c: o "Salvar memória" saiu; em 29/10/2026, o "Carregar memória do aparelho" (as memórias
        // do processador não aparecem no app). Fica o "Aplicar memória do celular".
        Assert.DoesNotContain("Carregar memória do aparelho", _vm.Itens.Select(i => i.Nome));
        Assert.Contains("Aplicar memória do celular", _vm.Itens.Select(i => i.Nome));
        Assert.DoesNotContain(_vm.Itens, i => i.Nome.Contains("Salvar", StringComparison.Ordinal));
        Assert.True(_vm.Item(Chave.AplicarMemoriaCelular).Ligada);
        Assert.False(_vm.Item(Chave.Gerador).Ligada);
        Assert.Equal("Desligada", _vm.Item(Chave.Gerador).TextoEstado);
        Assert.True(_vm.Item(Chave.AjustesAudio).Ligada);
        Assert.Equal("Ligada", _vm.Item(Chave.AjustesAudio).TextoEstado);
        Assert.False(_vm.FolhaAberta);
    }

    [Fact]
    public async Task A_chave_IA_pela_rede_aparece_desligada_com_a_nota()
    {
        await Pronto();
        var ia = _vm.Item(Chave.IaPelaRede);

        Assert.Equal("IA pela rede", ia.Nome);
        Assert.False(ia.Ligada);
        Assert.Equal("Pareia por código de 6 dígitos.", ia.Observacao);
        Assert.Null(_vm.Item(Chave.Gerador).Observacao);
    }

    [Fact]
    public async Task Ligar_o_Gerador_abre_a_folha_com_o_risco_e_so_liga_no_confirmar()
    {
        await Pronto();
        var gerador = _vm.Item(Chave.Gerador);

        await gerador.Ligar.ExecutarAsync();

        Assert.True(_vm.FolhaAberta);
        Assert.Equal("Um tom forte pode queimar o tweeter ou o sub.", _vm.FolhaRisco);
        Assert.Contains("Gerador", _vm.FolhaTitulo);
        Assert.False(_b.Chaves.Ligada(Chave.Gerador));

        await _vm.ConfirmarFolha.ExecutarAsync();
        await _b.Tela.EsvaziarAsync();

        Assert.False(_vm.FolhaAberta);
        Assert.True(_b.Chaves.Ligada(Chave.Gerador));
        Assert.True(gerador.Ligada);
    }

    [Fact]
    public async Task Ligar_uma_chave_ja_ligada_nao_abre_a_folha()
    {
        await Pronto();
        Assert.True(_vm.Item(Chave.RemoteOut).Ligada);

        await _vm.Item(Chave.RemoteOut).Ligar.ExecutarAsync();

        Assert.False(_vm.FolhaAberta);
        Assert.Null(_vm.FolhaRisco);
        Assert.Equal(0, _b.Arquivos.Gravacoes);
    }

    [Fact]
    public async Task Desistir_da_folha_deixa_desligada()
    {
        await Pronto();

        await _vm.Item(Chave.ResetFabrica).Ligar.ExecutarAsync();
        Assert.Equal("Apaga a senha e as 3 memórias.", _vm.FolhaRisco);
        await _vm.DesistirFolha.ExecutarAsync();
        await _b.Tela.EsvaziarAsync();

        Assert.False(_vm.FolhaAberta);
        Assert.False(_b.Chaves.Ligada(Chave.ResetFabrica));
        Assert.False(_vm.Item(Chave.ResetFabrica).Ligada);
        Assert.Equal(0, _b.Arquivos.Gravacoes);
    }

    [Fact]
    public async Task Desligar_age_direto_sem_folha()
    {
        await Pronto();

        await _vm.Item(Chave.RemoteOut).Desligar.ExecutarAsync();
        await _b.Tela.EsvaziarAsync();

        Assert.False(_vm.FolhaAberta);
        Assert.False(_b.Chaves.Ligada(Chave.RemoteOut));
        Assert.False(_vm.Item(Chave.RemoteOut).Ligada);
    }

    [Fact]
    public async Task Chave_sem_risco_liga_sem_folha()
    {
        await Pronto();
        await _vm.Item(Chave.BluetoothAudio).Desligar.ExecutarAsync();

        await _vm.Item(Chave.BluetoothAudio).Ligar.ExecutarAsync();
        await _b.Tela.EsvaziarAsync();

        Assert.False(_vm.FolhaAberta);
        Assert.True(_vm.Item(Chave.BluetoothAudio).Ligada);
    }

    [Fact]
    public async Task Ligar_e_recriar_o_estado_continua()
    {
        await Pronto();
        await _vm.Item(Chave.Gerador).Ligar.ExecutarAsync();
        await _vm.ConfirmarFolha.ExecutarAsync();
        await _vm.Item(Chave.AjustesAudio).Desligar.ExecutarAsync();
        Assert.NotNull(_b.Arquivos.Conteudo(RepositorioChavesArquivos.Arquivo));

        var outra = new Bancada(_b.Arquivos); // o app reabre com os mesmos arquivos privados
        using var vm = new ChavesViewModel(outra.Chaves, outra.Tela, outra.Relogio);
        await outra.Chaves.Carregada;
        await Task.Delay(20);
        await outra.Tela.EsvaziarAsync();

        Assert.True(vm.Item(Chave.Gerador).Ligada);
        Assert.False(vm.Item(Chave.AjustesAudio).Ligada);
        Assert.True(vm.Item(Chave.RemoteOut).Ligada);
    }

    [Fact]
    public async Task Falha_ao_guardar_ao_ligar_avisa_e_nao_liga()
    {
        await Pronto();
        _b.Arquivos.Falhar = true;

        await _vm.Item(Chave.Gerador).Ligar.ExecutarAsync();
        await _vm.ConfirmarFolha.ExecutarAsync();
        await _b.Tela.EsvaziarAsync();

        Assert.False(_vm.Item(Chave.Gerador).Ligada);
        Assert.Equal(Chaves.MsgNaoGuardou, _vm.Mensagem);
    }

    [Fact]
    public async Task Mudanca_feita_fora_da_tela_aparece_na_lista()
    {
        await Pronto();

        await _b.Chaves.DefinirAsync(Chave.NomeProduto, false, OrigemComando.Tela);
        await _b.Tela.EsvaziarAsync();

        Assert.False(_vm.Item(Chave.NomeProduto).Ligada);
    }
}
