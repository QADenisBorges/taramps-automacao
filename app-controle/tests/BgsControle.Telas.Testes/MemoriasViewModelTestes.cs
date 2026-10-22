using BgsControle.Nucleo;
using BgsControle.Nucleo.Escrita;
using BgsControle.Nucleo.Memorias;
using BgsControle.Nucleo.Seguranca;

namespace BgsControle.Telas.Testes;

/// <summary>A tela Memórias (história 6) contra o processador simulado.</summary>
public sealed class MemoriasViewModelTestes : IDisposable
{
    private readonly Bancada _b = new();
    private readonly MemoriasViewModel _vm;

    public MemoriasViewModelTestes() => _vm = _b.CriarMemoriasViewModel();

    public void Dispose()
    {
        _vm.Dispose();
        _b.ConferirSoAudio();
        Assert.Empty(_b.Tela.Erros);
    }

    private static Parametro Ganho => Catalogo.ParametroDe("081")!;

    // ----------------------------------------------------------------- critério de aceite

    [Fact]
    public async Task Salvar_na_memoria_2_e_carregar_a_3_o_simulador_bate_e_a_tela_mostra_os_nomes_relidos()
    {
        _b.Simulador.DefinirMemoria(2, "081", 0, "-20.0");
        _b.Simulador.DefinirMemoria(2, "091", 3, "+4.0");
        await _b.Conectar();
        Assert.Equal(["SPL", "MUSICA ORIG", "FILME"], _vm.Memorias.Select(m => m.Nome));
        await _b.Rodar(_b.Escritor.EscreverAsync(Ganho, 0, -3.0m));

        // Salvar na memória 2.
        _vm.Memoria(2).SalvarAqui.Execute(null);
        Assert.True(_vm.FolhaAberta);
        _vm.NomeFolha = "musica orig2";
        await _b.Rodar(_vm.ConfirmarFolha.ExecutarAsync());
        var salvo = _b.Simulador.EstadoMarco1();

        Assert.False(_vm.FolhaAberta);
        Assert.Equal("Memória 2 salva", _vm.Mensagem);
        Assert.Equal(salvo, _b.Simulador.Memoria(1));
        Assert.Equal("-3.0", _b.Simulador.Memoria(1)[("081", 0)]);
        Assert.Equal("MUSICA ORIG2", _vm.Memoria(2).Nome);
        Assert.True(_vm.Memoria(2).EmUso);

        // Carregar a memória 3.
        await _b.Rodar(_vm.Memoria(3).Carregar.ExecutarAsync());

        Assert.Equal(_b.Simulador.Memoria(2), _b.Simulador.EstadoMarco1());
        Assert.Equal("-20.0", _b.Simulador.Valor("081", 0));
        Assert.Equal(salvo, _b.Simulador.Memoria(1)); // a 2 continua como foi salva
        Assert.Equal(["SPL", "MUSICA ORIG2", "FILME"], _vm.Memorias.Select(m => m.Nome));
        Assert.True(_vm.Memoria(3).EmUso);
        Assert.False(_vm.Memoria(2).EmUso);
        Assert.Equal("Memória 3 FILME em uso.", _vm.Mensagem);
    }

    // ----------------------------------------------------------------- folha de sobrescrever

    [Fact]
    public async Task Salvar_aqui_abre_a_folha_de_sobrescrever_com_o_nome_editavel()
    {
        await _b.Conectar();
        var antes = _b.Simulador.Escritas.Count;

        await _vm.Memoria(2).SalvarAqui.ExecutarAsync();

        Assert.True(_vm.FolhaAberta);
        Assert.Equal("Salvar sobre a MUSICA ORIG?", _vm.FolhaTitulo);
        Assert.Equal("A versão atual some.", _vm.FolhaAviso);
        Assert.Equal("MUSICA ORIG", _vm.NomeFolha);
        Assert.Equal("Salvar sobre a MUSICA ORIG", _vm.TextoConfirmarFolha);
        Assert.Equal(antes, _b.Simulador.Escritas.Count); // nada antes do botão branco
    }

    [Fact]
    public async Task Cancelar_a_folha_nada_sai()
    {
        await _b.Conectar();
        var antes = _b.Simulador.Escritas.Count;

        await _vm.Memoria(2).SalvarAqui.ExecutarAsync();
        _vm.NomeFolha = "OUTRO";
        await _vm.CancelarFolha.ExecutarAsync();
        await _b.Andar(TimeSpan.FromMilliseconds(300));

        Assert.False(_vm.FolhaAberta);
        Assert.Equal(antes, _b.Simulador.Escritas.Count);
        Assert.Null(_b.Arquivos.Conteudo(RepositorioTextoArquivos.ArquivoCopia));
    }

    [Theory]
    [InlineData("música#1")]
    [InlineData("MUSICA ORIG 2")] // 13 caracteres
    [InlineData("")]
    public async Task Nome_invalido_na_folha_e_recusado_e_nada_sai(string nome)
    {
        await _b.Conectar();
        var antes = _b.Simulador.Escritas.Count;
        await _vm.Memoria(2).SalvarAqui.ExecutarAsync();
        _vm.NomeFolha = nome;

        await _vm.ConfirmarFolha.ExecutarAsync();

        Assert.True(_vm.FolhaAberta);
        Assert.Equal("Use até 12 letras, números, espaço ou _", _vm.ErroFolha);
        Assert.Equal(antes, _b.Simulador.Escritas.Count);
    }

    [Fact]
    public async Task Confirmar_com_o_mesmo_nome_salva_e_relê()
    {
        await _b.Conectar();
        var antes = _b.Simulador.Escritas.Count;
        await _vm.Memoria(2).SalvarAqui.ExecutarAsync();

        await _b.Rodar(_vm.ConfirmarFolha.ExecutarAsync());

        Assert.Equal(["*240*1*", "*232*MUSICA ORIG*", "#232#"], _b.Simulador.Escritas.Skip(antes));
        Assert.Equal("Memória 2 salva", _vm.Mensagem);
        Assert.False(_vm.Salvando);
    }

    // ----------------------------------------------------------------- carregar e travas

    [Fact]
    public async Task Carregar_age_direto_sem_folha()
    {
        await _b.Conectar();
        var antes = _b.Simulador.Escritas.Count;

        await _b.Rodar(_vm.Memoria(3).Carregar.ExecutarAsync());

        Assert.False(_vm.FolhaAberta);
        Assert.Equal("*241*2*", _b.Simulador.Escritas[antes]);
        Assert.True(_vm.Memoria(3).EmUso);
        Assert.NotNull(_b.Arquivos.Conteudo(RepositorioTextoArquivos.ArquivoCopia)); // a cópia antes
    }

    [Fact]
    public async Task Chave_de_carregar_desligada_trava_com_o_motivo_e_nada_sai()
    {
        await _b.Conectar();
        await _b.Chaves.DefinirAsync(Chave.CarregarMemoria, false, OrigemComando.Tela);
        await _b.Tela.EsvaziarAsync();
        var antes = _b.Simulador.Escritas.Count;

        await _vm.Memoria(3).Carregar.ExecutarAsync();

        Assert.Equal("Chave desligada", _vm.MotivoTravaCarregar);
        Assert.False(_vm.PodeCarregar);
        Assert.True(_vm.PodeSalvar); // a outra chave continua ligada
        Assert.Equal("Chave desligada", _vm.Mensagem);
        Assert.Equal(antes, _b.Simulador.Escritas.Count);
    }

    [Fact]
    public async Task Chave_de_salvar_desligada_salvar_aqui_e_renomear_ficam_travados()
    {
        await _b.Conectar();
        await _b.Chaves.DefinirAsync(Chave.SalvarMemoria, false, OrigemComando.Tela);
        await _b.Tela.EsvaziarAsync();

        await _vm.Memoria(1).SalvarAqui.ExecutarAsync();
        Assert.False(_vm.FolhaAberta);
        await _vm.Memoria(1).Renomear.ExecutarAsync();
        Assert.False(_vm.RenomearAberto);

        Assert.Equal("Chave desligada", _vm.MotivoTravaSalvar);
        Assert.Equal("Chave desligada", _vm.Mensagem);
    }

    [Fact]
    public async Task Sem_conexao_tudo_travado_com_o_motivo()
    {
        await _b.Chaves.Carregada;

        await _vm.Memoria(1).Carregar.ExecutarAsync();

        Assert.Equal("Sem conexão", _vm.MotivoTravaCarregar);
        Assert.Equal("Sem conexão", _vm.MotivoTravaSalvar);
        Assert.Equal("Sem conexão", _vm.Mensagem);
        Assert.Equal(SemNomes, _vm.Memorias.Select(m => m.Nome));
        Assert.Empty(_b.Simulador.Escritas);
    }

    private static readonly string[] SemNomes = [MemoriasViewModel.SemNome, MemoriasViewModel.SemNome, MemoriasViewModel.SemNome];

    [Fact]
    public async Task Modo_so_leitura_trava_com_o_motivo()
    {
        _b.Simulador.Firmware = "R004";
        await _b.Conectar();

        Assert.Equal("Modo só leitura", _vm.MotivoTravaCarregar);
        Assert.Equal("Modo só leitura", _vm.MotivoTravaSalvar);
    }

    // ----------------------------------------------------------------- renomear

    [Fact]
    public async Task Renomear_envia_so_o_nome()
    {
        await _b.Conectar();
        var antes = _b.Simulador.Escritas.Count;

        await _vm.Memoria(1).Renomear.ExecutarAsync();
        Assert.True(_vm.RenomearAberto);
        Assert.Equal("SPL", _vm.NomeRenomear);
        _vm.NomeRenomear = "cinema";
        await _b.Rodar(_vm.ConfirmarRenomear.ExecutarAsync());

        Assert.Equal(["*231*CINEMA*", "#231#"], _b.Simulador.Escritas.Skip(antes));
        Assert.Equal("CINEMA", _vm.Memoria(1).Nome);
        Assert.False(_vm.RenomearAberto);
        Assert.Equal(0, _b.EscritasDe("*240*"));
    }

    // ----------------------------------------------------------------- quedas

    [Fact]
    public async Task Queda_no_salvar_a_folha_fica_com_o_nome_e_ao_reconectar_diz_que_pode_nao_ter_sido_salva()
    {
        await _b.Conectar();
        _b.Simulador.CairDepoisDaEscrita = _b.Simulador.Escritas.Count + 1; // cai depois do *240*1*
        await _vm.Memoria(2).SalvarAqui.ExecutarAsync();
        _vm.NomeFolha = "MUSICA ORIG2";

        await _b.Rodar(_vm.ConfirmarFolha.ExecutarAsync());
        Assert.Equal(GerenteMemorias.MsgCaiuSalvar, _vm.Mensagem);
        Assert.True(_vm.FolhaAberta); // a folha fica, com o nome digitado e o erro
        Assert.Equal("MUSICA ORIG2", _vm.NomeFolha);
        Assert.Equal(GerenteMemorias.MsgCaiuSalvar, _vm.ErroFolha);
        Assert.False(_vm.Salvando);

        await _b.Conectar();
        await Apoio.Esperar(() => _b.Memorias.Aviso is not null && _b.Memorias.Aviso.StartsWith("A memória 2", StringComparison.Ordinal));
        await _b.Tela.EsvaziarAsync();

        Assert.Equal("A memória 2 pode não ter sido salva; carregue-a para conferir", _vm.Mensagem);
        Assert.False(_vm.PelaMetade);
    }

    [Fact]
    public async Task Salvar_sem_resposta_ao_240_a_tela_diz_enviada_e_fecha_a_folha()
    {
        await _b.Conectar();
        _b.Simulador.Silenciar.Add("*240*1*");
        await _vm.Memoria(2).SalvarAqui.ExecutarAsync();

        await _b.Rodar(_vm.ConfirmarFolha.ExecutarAsync());

        Assert.Equal("Memória 2 enviada (o aparelho não confirma o salvar)", _vm.Mensagem);
        Assert.False(_vm.FolhaAberta);
        Assert.False(_vm.Memoria(2).EmUso);
    }

    [Fact]
    public async Task Renomear_sem_confirmacao_o_campo_fica_com_o_nome_e_o_erro()
    {
        await _b.Conectar();
        _b.Simulador.Silenciar.Add("#231#");
        await _vm.Memoria(1).Renomear.ExecutarAsync();
        _vm.NomeRenomear = "cinema";

        await _b.Rodar(_vm.ConfirmarRenomear.ExecutarAsync());

        Assert.True(_vm.RenomearAberto);
        Assert.Equal("cinema", _vm.NomeRenomear);
        Assert.Equal(GerenteMemorias.MsgNomeNaoConfirmado, _vm.ErroRenomear);
    }

    private async Task CarregarComQueda()
    {
        _b.Simulador.DefinirMemoria(2, "081", 0, "-20.0");
        await _b.Conectar();
        _b.Simulador.CairDepoisDaEscrita = _b.Simulador.Escritas.Count + 1; // cai depois do *241*2*
        await _b.Rodar(_vm.Memoria(3).Carregar.ExecutarAsync());
        Assert.Equal(GerenteMemorias.MsgCaiuCarregar, _vm.Mensagem);

        await _b.Conectar();
        await Apoio.Esperar(() => _b.Copia.PelaMetade is not null);
        await _b.Tela.EsvaziarAsync();
    }

    [Fact]
    public async Task Queda_no_carregar_ao_reconectar_mostra_aplicado_pela_metade_com_os_dois_botoes()
    {
        await CarregarComQueda();

        Assert.True(_vm.PelaMetade);
        Assert.Equal("Carregar a memória 3 (FILME) caiu no meio.", _vm.TextoPelaMetade);
        Assert.NotNull(_vm.Terminar);
        Assert.NotNull(_vm.VoltarACopia);
        Assert.False(_vm.Memoria(3).EmUso);
    }

    [Fact]
    public async Task Voltar_a_copia_escreve_so_o_que_mudou_e_fecha_o_pela_metade()
    {
        await CarregarComQueda();
        var antes = _b.Simulador.Escritas.Count;

        await _b.Rodar(_vm.VoltarACopia.ExecutarAsync());

        Assert.Equal("O som voltou à cópia.", _vm.Mensagem);
        Assert.False(_vm.PelaMetade);
        Assert.Equal(["*081*0*10.0*"], _b.Simulador.Escritas.Skip(antes).Where(e => Catalogo.EscritaDeAudio(e) is not null));
        Assert.Equal("+10.0", _b.Simulador.Valor("081", 0));
    }

    [Fact]
    public async Task Terminar_refaz_o_carregar()
    {
        await CarregarComQueda();

        await _b.Rodar(_vm.Terminar.ExecutarAsync());

        Assert.False(_vm.PelaMetade);
        Assert.True(_vm.Memoria(3).EmUso);
        Assert.Equal("Memória 3 FILME em uso.", _vm.Mensagem);
        Assert.Equal("-20.0", _b.Simulador.Valor("081", 0));
    }

    [Fact]
    public async Task Voltar_a_copia_com_a_chave_de_ajustes_desligada_nada_sai()
    {
        await CarregarComQueda();
        await _b.Chaves.DefinirAsync(Chave.AjustesAudio, false, OrigemComando.Tela);
        var antes = _b.Simulador.Escritas.Count;

        await _vm.VoltarACopia.ExecutarAsync();

        Assert.Equal("Chave desligada", _vm.Mensagem);
        Assert.True(_vm.PelaMetade);
        Assert.Equal(antes, _b.Simulador.Escritas.Count);
    }
}
