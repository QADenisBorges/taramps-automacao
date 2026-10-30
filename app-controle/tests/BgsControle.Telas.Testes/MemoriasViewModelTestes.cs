using BgsControle.Nucleo;
using BgsControle.Nucleo.Herdado;
using BgsControle.Nucleo.Seguranca;
using BgsControle.Nucleo.Setups;
using BgsControle.Simulador;

namespace BgsControle.Telas.Testes;

/// <summary>
/// A tela Memórias das histórias 8c e 8d contra o processador simulado: as memórias do celular do
/// Setup em uso (Aplicar, Renomear, Apagar, Criar memória do som atual, o preset lembrado) e o
/// "aplicado pela metade". As memórias do processador não aparecem (decisão do Denis, 29/10/2026).
/// </summary>
public sealed class MemoriasViewModelTestes : IDisposable
{
    private readonly Bancada _b = new(setups: true);
    private readonly SalvarViewModel _salvar;
    private readonly MemoriasViewModel _vm;

    public MemoriasViewModelTestes()
    {
        _salvar = _b.CriarSalvarViewModel();
        _vm = _b.CriarMemoriasViewModel(_salvar);
    }

    public void Dispose()
    {
        _vm.Dispose();
        _salvar.Dispose();
        _b.ConferirSoAudio();
        Assert.Empty(_b.Tela.Erros);
    }

    private GerenteSetups Setups => _b.Setups!;

    private static Parametro Ganho => Catalogo.ParametroDe("081")!;

    private async Task Criar(string nome)
    {
        await _vm.AbrirCriar.ExecutarAsync();
        Assert.True(_vm.CriarAberto);
        _vm.NomeNova = nome;
        await _b.Rodar(_vm.ConfirmarCriar.ExecutarAsync());
    }

    // ----------------------------------------------------------------- memórias do celular

    [Fact]
    public async Task Os_cartoes_sao_as_memorias_do_Setup_em_uso_com_EM_USO_sem_selo_de_herdado()
    {
        await _b.Conectar();

        Assert.Equal("Memórias do Casa 2.1", _vm.TituloCelular);
        var filme = Assert.Single(_vm.Cartoes);
        Assert.Equal("FILME", filme.Nome);
        Assert.True(filme.EmUso);
        Assert.Equal("EM USO", filme.SeloEmUso);
        Assert.Equal("Esta memória está em uso.", filme.MotivoTravaApagar);
        Assert.Null(_vm.TextoVazio);
        Assert.Null(_vm.MotivoTravaAplicar);
        Assert.Null(_vm.AvisoSomDiferente);
    }

    [Fact]
    public async Task Criar_memoria_do_som_atual_vira_a_em_uso_com_o_aviso_e_nada_vai_ao_aparelho()
    {
        await _b.Conectar();
        await _b.Rodar(_b.Escritor.EscreverAsync(Ganho, 0, 4.0m));
        var antes = _b.Simulador.Escritas.Count;

        await Criar("MUSICA ORIG");

        Assert.Equal("Memória MUSICA ORIG criada no Casa 2.1.", _vm.Mensagem);
        Assert.False(_vm.CriarAberto);
        Assert.Equal(["FILME", "MUSICA ORIG"], _vm.Cartoes.Select(c => c.Nome));
        Assert.True(_vm.Cartao("MUSICA ORIG")!.EmUso);
        Assert.False(_vm.Cartao("FILME")!.EmUso);
        Assert.Equal(antes, _b.Simulador.Escritas.Count);
        Assert.Contains("MUSICA ORIG", _b.Arquivos.Conteudo(RepositorioTextoArquivos.ArquivoSetups));
    }

    [Fact]
    public async Task Criar_com_nome_repetido_o_campo_fica_com_o_erro()
    {
        await _b.Conectar();

        await Criar("filme");

        Assert.True(_vm.CriarAberto);
        Assert.Equal("filme", _vm.NomeNova);
        Assert.Equal("Já existe uma memória com esse nome no Casa 2.1.", _vm.ErroNova);
        Assert.Single(_vm.Cartoes);
    }

    [Fact]
    public async Task Aplicar_pelo_cartao_aplica_sem_confirmacao_e_a_memoria_fica_em_uso()
    {
        await _b.Conectar();
        await _b.Rodar(_b.Escritor.EscreverAsync(Ganho, 0, 4.0m));
        await Criar("MUSICA ORIG");
        var antes = _b.Simulador.Escritas.Count;

        await _b.Rodar(_vm.Cartao("FILME")!.Aplicar.ExecutarAsync());

        Assert.Equal("Memória FILME aplicada.", _vm.Mensagem);
        Assert.True(_vm.Cartao("FILME")!.EmUso);
        Assert.Equal(["*081*0*10.0*"], _b.Simulador.Escritas.Skip(antes).Where(e => Catalogo.EscritaDeAudio(e) is not null));
    }

    [Fact]
    public async Task Aplicar_com_a_chave_desligada_mostra_o_motivo_e_nada_sai()
    {
        await _b.Conectar();
        await _b.Chaves.DefinirAsync(Chave.AplicarMemoriaCelular, false, OrigemComando.Tela);
        await _b.Tela.EsvaziarAsync();
        var antes = _b.Simulador.Escritas.Count;

        await _vm.Cartao("FILME")!.Aplicar.ExecutarAsync();

        Assert.Equal("Chave desligada", _vm.Mensagem);
        Assert.Equal("Chave desligada", _vm.MotivoTravaAplicar);
        Assert.Equal(antes, _b.Simulador.Escritas.Count);
    }

    [Fact]
    public async Task Renomear_vale_no_check_e_nao_manda_nada()
    {
        await _b.Conectar();
        var antes = _b.Simulador.Escritas.Count;

        _vm.Cartao("FILME")!.Renomear.Execute(null);
        Assert.True(_vm.RenomearAberto);
        Assert.Equal("Renomear a FILME", _vm.RenomearTitulo);
        Assert.Equal("FILME", _vm.NomeRenomear);
        _vm.NomeRenomear = "CINEMA";
        await _b.Rodar(_vm.ConfirmarRenomear.ExecutarAsync());

        Assert.False(_vm.RenomearAberto);
        Assert.Equal(["CINEMA"], _vm.Cartoes.Select(c => c.Nome));
        Assert.Equal(antes, _b.Simulador.Escritas.Count);
    }

    [Fact]
    public async Task Apagar_abre_a_folha_e_na_memoria_em_uso_mostra_o_motivo()
    {
        await _b.Conectar();
        await Criar("NOITE");
        await _b.Rodar(_vm.Cartao("FILME")!.Aplicar.ExecutarAsync());

        _vm.Cartao("FILME")!.Apagar.Execute(null);
        Assert.False(_vm.ApagarAberto);
        Assert.Equal("Esta memória está em uso.", _vm.Mensagem);

        _vm.Cartao("NOITE")!.Apagar.Execute(null);
        Assert.True(_vm.ApagarAberto);
        Assert.Equal("Apagar a NOITE?", _vm.ApagarTitulo);
        Assert.Equal("A memória e a playlist dela somem.", _vm.ApagarAviso);
        await _vm.ConfirmarApagar.ExecutarAsync();
        await _b.Tela.EsvaziarAsync();

        Assert.False(_vm.ApagarAberto);
        Assert.Equal(["FILME"], _vm.Cartoes.Select(c => c.Nome));
        Assert.Equal("Memória NOITE apagada.", _vm.Mensagem);
    }

    [Fact]
    public async Task Sem_Setup_as_memorias_do_celular_ficam_travadas()
    {
        _b.Simulador.Definir("050", 2, "0.0");
        await _b.Conectar();
        await _b.Rodar(Setups.SeguirSemSetupAsync());
        await _b.Tela.EsvaziarAsync();

        Assert.Equal("Escolha um Setup para usar as memórias do celular.", _vm.MotivoTravaCelular);
        Assert.Equal("Escolha um Setup para usar as memórias do celular.", _vm.MotivoTravaAplicar);
        Assert.Equal("Escolha um Setup para usar as memórias do celular.", _vm.MotivoTravaCriar);
        await _vm.AbrirCriar.ExecutarAsync();
        Assert.False(_vm.CriarAberto);
        Assert.Equal("Escolha um Setup para usar as memórias do celular.", _vm.Mensagem);
    }

    [Fact]
    public async Task Som_diferente_da_FILME_aparece_com_a_chave_desligada_e_Reaplicar_volta_a_FILME()
    {
        _b.Simulador.Definir("081", 0, "+4.0");
        await _b.ConectarSemReaplicar();
        await _b.Tela.EsvaziarAsync();
        Assert.Equal("Som diferente da FILME", _vm.AvisoSomDiferente);
        Assert.False(_vm.Cartao("FILME")!.EmUso);

        await _b.Rodar(_vm.Reaplicar.ExecutarAsync());

        Assert.Null(_vm.AvisoSomDiferente);
        Assert.True(_vm.Cartao("FILME")!.EmUso);
        Assert.Equal("+10.0", _b.Simulador.Valor("081", 0));
    }

    [Fact]
    public async Task Criar_com_o_som_do_aparelho_em_uso_abre_a_folha_do_Salvar()
    {
        _b.Simulador.Definir("081", 0, "+4.0"); // só o ganho difere da FILME: o som do aparelho
        await _b.ConectarSemReaplicar();
        await _b.Tela.EsvaziarAsync();
        Assert.Equal(SomEmUso.SomDoAparelho, Setups.EmUso);

        await _vm.AbrirCriar.ExecutarAsync();

        Assert.False(_vm.CriarAberto);
        Assert.True(_salvar.Aberta);
        Assert.True(_salvar.PedeNome);
    }

    // ----------------------------------------------------------------- aplicado pela metade

    /// <summary>Aplicar a FILME (com dois ganhos diferentes) e cair logo depois do primeiro ajuste; reconecta.</summary>
    private async Task AplicarComQueda()
    {
        await _b.Conectar();
        await _b.Rodar(_b.Escritor.EscreverAsync(Ganho, 0, 4.0m));
        await _b.Rodar(_b.Escritor.EscreverAsync(Ganho, 2, 1.0m));
        _b.Simulador.CairDepoisDaEscrita = _b.Simulador.Escritas.Count + 1;

        await _b.Rodar(_vm.Cartao("FILME")!.Aplicar.ExecutarAsync());
        await _b.Conectar();
        await Apoio.Esperar(() => _b.Copia.PelaMetade is not null);
        await _b.Tela.EsvaziarAsync();
    }

    [Fact]
    public async Task Voltar_a_copia_escreve_so_o_que_mudou_e_fecha_o_pela_metade()
    {
        await AplicarComQueda();
        Assert.True(_vm.PelaMetade);

        await _b.Rodar(_vm.VoltarACopia.ExecutarAsync());

        Assert.Equal("O som voltou à cópia.", _vm.Mensagem);
        Assert.False(_vm.PelaMetade);
        Assert.Equal("+4.0", _b.Simulador.Valor("081", 0));
        Assert.Equal("+1.0", _b.Simulador.Valor("081", 2));
    }

    [Fact]
    public async Task Queda_no_aplicar_da_memoria_do_celular_fica_pela_metade_e_Terminar_aplica()
    {
        await _b.Conectar();
        await _b.Rodar(_b.Escritor.EscreverAsync(Ganho, 0, 4.0m));
        await _b.Rodar(_b.Escritor.EscreverAsync(Ganho, 2, 1.0m));
        _b.Simulador.CairDepoisDaEscrita = _b.Simulador.Escritas.Count + 1;

        await _b.Rodar(_vm.Cartao("FILME")!.Aplicar.ExecutarAsync());
        await _b.Conectar();
        await Apoio.Esperar(() => _b.Copia.PelaMetade is not null);
        await _b.Tela.EsvaziarAsync();

        Assert.Equal("Aplicar a memória FILME caiu no meio.", _vm.TextoPelaMetade);
        await _b.Rodar(_vm.Terminar.ExecutarAsync());

        Assert.False(_vm.PelaMetade);
        Assert.True(_vm.Cartao("FILME")!.EmUso);
        Assert.Equal("+10.0", _b.Simulador.Valor("081", 0));
        Assert.Equal("+6.0", _b.Simulador.Valor("081", 2));
        Assert.Equal(CenarioFilme.IdMemoria, Setups.EmUso.IdMemoria);
    }

    // ----------------------------------------------------------------- 8d: o preset lembrado e a playlist

    [Fact]
    public async Task O_cartao_mostra_o_preset_lembrado_e_a_memoria_nova_lembra_o_preset_em_uso()
    {
        await _b.Conectar();
        Assert.Equal("Preset: Cinema", _vm.Cartao("FILME")!.TextoPreset);

        await Criar("NOITE");
        await _b.Tela.EsvaziarAsync();

        Assert.Equal("Preset: Cinema", _vm.Cartao("NOITE")!.TextoPreset);
        var rock = (await _b.Presets.CriarAsync("Rock", CenarioFilme.Bandas.Select(v => v.Indice == 0 ? v with { NoFio = "+4.0" } : v).ToArray())).Preset!;
        await _b.Rodar(_b.Presets.AplicarAsync(rock.Id)); // a NOITE, em uso, passa a lembrar o Rock
        await _b.Tela.EsvaziarAsync();
        Assert.Equal("Preset: Rock", _vm.Cartao("NOITE")!.TextoPreset);
        Assert.Equal("Preset: Cinema", _vm.Cartao("FILME")!.TextoPreset);
    }

    [Fact]
    public async Task Aplicar_a_FILME_pelo_cartao_traz_as_bandas_do_Cinema_junto()
    {
        await _b.Conectar();
        await _b.Rodar(_b.Escritor.EscreverAsync(Catalogo.ParametroDe("020")!, 4, 3.0m));
        await Criar("NOITE"); // a NOITE nasce lembrando o Cinema, com 160 Hz em +3 no aparelho

        await _b.Rodar(_vm.Cartao("FILME")!.Aplicar.ExecutarAsync());

        Assert.Equal("Memória FILME aplicada.", _vm.Mensagem);
        Assert.Equal("+1.0", _b.Simulador.Valor("020", 4)); // a banda do Cinema voltou
    }

    [Fact]
    public async Task Apagar_a_memoria_apaga_a_playlist_dela()
    {
        await _b.Conectar();
        await Criar("NOITE");
        var noite = _vm.Cartao("NOITE")!.Id;
        Assert.True((await _b.Playlists.AdicionarAsync(noite, [new Nucleo.Player.ArquivoAudio("content://a", "A", "M", TimeSpan.Zero)])).Ok);
        await _b.Rodar(_vm.Cartao("FILME")!.Aplicar.ExecutarAsync());

        _vm.Cartao("NOITE")!.Apagar.Execute(null);
        await _b.Rodar(_vm.ConfirmarApagar.ExecutarAsync());

        Assert.Empty(_b.Playlists.Playlist(noite));
    }
}
