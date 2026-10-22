using BgsControle.Nucleo;
using BgsControle.Nucleo.Escrita;
using BgsControle.Nucleo.Seguranca;

namespace BgsControle.Telas.Testes;

/// <summary>O link 1–2 e 3–4 na tela (história 6) contra o processador simulado.</summary>
public sealed class LinkViewModelTestes : IDisposable
{
    private readonly Bancada _b = new();
    private readonly LinkViewModel _vm;

    public LinkViewModelTestes() => _vm = _b.CriarLinkViewModel();

    public void Dispose()
    {
        _vm.Dispose();
        _b.ConferirSoAudio();
        Assert.Empty(_b.Tela.Erros);
    }

    [Fact]
    public async Task Ligar_mostra_o_aviso_e_so_copia_depois_do_Confirmar()
    {
        await _b.Conectar();
        var antes = _b.Simulador.Escritas.Count;

        await _vm.Par(ParCanais.Saidas12).Ligar.ExecutarAsync();

        Assert.True(_vm.FolhaAberta);
        Assert.Equal("Os ajustes da saída 1 vão para a saída 2", _vm.FolhaAviso);
        Assert.False(_vm.Par(ParCanais.Saidas12).Ligado);
        Assert.Equal(antes, _b.Simulador.Escritas.Count);

        await _b.Rodar(_vm.ConfirmarFolha.ExecutarAsync());

        Assert.False(_vm.FolhaAberta);
        Assert.True(_vm.Par(ParCanais.Saidas12).Ligado);
        Assert.Equal("Lig.", _vm.Par(ParCanais.Saidas12).TextoEstado);
        Assert.Equal("Link 1–2 ligado.", _vm.Mensagem);
        foreach (var p in LinkCanais.Parametros)
            Assert.True(CopiaAntes.Iguais(_b.Estado.Valor(p.Codigo, 0)!, _b.Estado.Valor(p.Codigo, 1)!), p.Codigo);
        Assert.Contains("\"1-2\": true", _b.Arquivos.Conteudo(RepositorioTextoArquivos.ArquivoLink));
    }

    [Fact]
    public async Task O_aviso_do_3_4_fala_das_saidas_3_e_4()
    {
        await _b.Conectar();

        await _vm.Par(ParCanais.Saidas34).Ligar.ExecutarAsync();

        Assert.Equal("Os ajustes da saída 3 vão para a saída 4", _vm.FolhaAviso);
    }

    [Fact]
    public async Task Cancelar_nada_sai_e_o_link_fica_desligado()
    {
        await _b.Conectar();
        var antes = _b.Simulador.Escritas.Count;

        await _vm.Par(ParCanais.Saidas12).Ligar.ExecutarAsync();
        await _vm.CancelarFolha.ExecutarAsync();
        await _b.Andar(TimeSpan.FromMilliseconds(300));

        Assert.False(_vm.FolhaAberta);
        Assert.False(_b.Link.Ligado(ParCanais.Saidas12));
        Assert.Equal(antes, _b.Simulador.Escritas.Count);
    }

    [Fact]
    public async Task Link_ligado_ganho_da_saida_1_em_menos_2_pelo_controle_as_duas_saidas_ficam_em_menos_2()
    {
        await _b.Conectar();
        await _vm.Par(ParCanais.Saidas12).Ligar.ExecutarAsync();
        await _b.Rodar(_vm.ConfirmarFolha.ExecutarAsync());
        using var saida1 = _b.Fabrica.Criar(Catalogo.ParametroDe("081")!, 0);
        using var saida2 = _b.Fabrica.Criar(Catalogo.ParametroDe("081")!, 1);

        saida1.Expandir();
        saida1.AbrirCampo();
        saida1.TextoCampo = "-2,0";
        await saida1.Confirmar.ExecutarAsync();
        await _b.Rodar(saida1.UltimaEscrita);
        await _b.Andar(TimeSpan.FromMilliseconds(100));

        Assert.Equal("-2.0", _b.Simulador.Valor("081", 0));
        Assert.Equal("-2.0", _b.Simulador.Valor("081", 1));
        Assert.Equal("−2,0 dB", saida2.TextoValor);
    }

    [Fact]
    public async Task Desligar_age_direto_sem_folha()
    {
        await _b.Conectar();
        await _vm.Par(ParCanais.Saidas34).Ligar.ExecutarAsync();
        await _b.Rodar(_vm.ConfirmarFolha.ExecutarAsync());
        var antes = _b.Simulador.Escritas.Count;

        await _vm.Par(ParCanais.Saidas34).Desligar.ExecutarAsync();
        await _b.Tela.EsvaziarAsync();

        Assert.False(_vm.FolhaAberta);
        Assert.False(_vm.Par(ParCanais.Saidas34).Ligado);
        Assert.Equal(antes, _b.Simulador.Escritas.Count);
    }

    [Fact]
    public async Task Chave_de_ajustes_desligada_ligar_fica_travado_com_o_motivo()
    {
        await _b.Conectar();
        await _b.Chaves.DefinirAsync(Chave.AjustesAudio, false, OrigemComando.Tela);
        await _b.Tela.EsvaziarAsync();

        await _vm.Par(ParCanais.Saidas12).Ligar.ExecutarAsync();

        Assert.False(_vm.FolhaAberta);
        Assert.Equal("Chave desligada", _vm.MotivoTrava);
        Assert.Equal("Chave desligada", _vm.Mensagem);
    }

    [Fact]
    public async Task Ligar_com_uma_copia_andando_mostra_que_o_link_esta_ocupado()
    {
        _b.Simulador.Definir("081", 1, "-7.0"); // há o que copiar
        _b.Simulador.Definir("081", 3, "-7.0");
        await _b.Conectar();
        await _vm.Par(ParCanais.Saidas12).Ligar.ExecutarAsync();
        var copia = _vm.ConfirmarFolha.ExecutarAsync(); // o relógio parado: a cópia espera o eco
        await Apoio.Esperar(() => _vm.Copiando);

        await _vm.Par(ParCanais.Saidas34).Ligar.ExecutarAsync();

        Assert.False(_vm.FolhaAberta);
        Assert.Equal(LinkCanais.MsgOcupado, _vm.Mensagem);
        await _b.Rodar(copia);
        Assert.True(_vm.Par(ParCanais.Saidas12).Ligado);
        Assert.False(_vm.Par(ParCanais.Saidas34).Ligado);
    }

    [Fact]
    public async Task Link_ligado_com_as_saidas_diferentes_depois_da_leitura_o_par_mostra_o_aviso()
    {
        await _b.Conectar();
        await _vm.Par(ParCanais.Saidas12).Ligar.ExecutarAsync();
        await _b.Rodar(_vm.ConfirmarFolha.ExecutarAsync());
        Assert.Null(_vm.Par(ParCanais.Saidas12).AvisoDiferenca);
        await _b.Sessao.DesconectarAsync();
        _b.Simulador.Definir("081", 1, "-7.0"); // mudou fora do app

        await _b.Conectar();
        await Apoio.Esperar(() => _b.Link.Diferentes(ParCanais.Saidas12));
        await _b.Tela.EsvaziarAsync();

        Assert.Equal("Link 1–2: as saídas estão diferentes", _vm.Par(ParCanais.Saidas12).AvisoDiferenca);
        Assert.Equal("-7.0", _b.Simulador.Valor("081", 1)); // nada escrito sozinho
    }

    [Fact]
    public async Task Sem_conexao_ligar_fica_travado()
    {
        await _b.Chaves.Carregada;

        await _vm.Par(ParCanais.Saidas12).Ligar.ExecutarAsync();

        Assert.False(_vm.FolhaAberta);
        Assert.Equal("Sem conexão", _vm.Mensagem);
        Assert.Empty(_b.Simulador.Escritas);
    }
}
