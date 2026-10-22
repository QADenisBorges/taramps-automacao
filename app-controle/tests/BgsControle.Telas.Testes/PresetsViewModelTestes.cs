using BgsControle.Nucleo;
using BgsControle.Nucleo.Escrita;
using BgsControle.Nucleo.Herdado;
using BgsControle.Nucleo.Presets;
using BgsControle.Nucleo.Seguranca;
using System.Text.RegularExpressions;

namespace BgsControle.Telas.Testes;

/// <summary>
/// A história 8 nas telas, contra o processador simulado: o <see cref="PresetsViewModel"/>, o
/// <see cref="HerdadoViewModel"/> e o selo herdado nos cartões de memória. Uma linha de teste para
/// cada linha da matriz de <c>8-presets-conhecimento-herdado.md</c>.
/// </summary>
public sealed class PresetsViewModelTestes : IDisposable
{
    private readonly Bancada _b = new();
    private readonly PresetsViewModel _vm;

    public PresetsViewModelTestes() => _vm = _b.CriarPresetsViewModel();

    private static readonly Regex Senha = new(@"^\*000\*[0-9A-Z]{4}\*$");

    /// <summary>Critério de aceite: só a senha, as leituras do catálogo e as escritas de áudio (nenhum comando de memória, nenhum gerador).</summary>
    private static void ConferirSoAudioEstrito(Bancada b) =>
        Assert.All(b.Simulador.Escritas, e =>
            Assert.True(Senha.IsMatch(e) || Catalogo.EhLeitura(e) || Catalogo.EscritaDeAudio(e) is not null, e));

    public void Dispose()
    {
        _vm.Dispose();
        ConferirSoAudioEstrito(_b);
        Assert.Empty(_b.Tela.Erros);
    }

    private static Parametro Ganho => Catalogo.ParametroDe("081")!;
    private static Parametro Eq => Catalogo.ParametroDe("020")!;

    private async Task Guardar(string nome)
    {
        _vm.NomeNovo = nome;
        await _b.Rodar(_vm.Guardar.ExecutarAsync());
        Assert.Null(_vm.ErroNome);
    }

    private async Task Mudar()
    {
        await _b.Rodar(_b.Escritor.EscreverAsync(Ganho, 0, -3.0m));
        await _b.Rodar(_b.Escritor.EscreverAsync(Eq, 4, 3.0m));
    }

    private static string Linhas(IEnumerable<KeyValuePair<(string Codigo, int? Indice), string>> valores) =>
        string.Join("\n", valores.Select(kv => $"{kv.Key.Codigo}/{kv.Key.Indice}={kv.Value}").Order(StringComparer.Ordinal));

    // ----------------------------------------------------------------- primeira abertura

    [Fact]
    public void Primeira_abertura_o_FILME_herdado_as_notas_e_as_secoes_estao_la_sem_arquivo_e_sem_digitar_nada()
    {
        var herdado = new HerdadoViewModel();
        var memorias = _b.CriarMemoriasViewModel();

        // Presets: o FILME herdado primeiro, com selo e nota; nenhum próprio.
        var filme = Assert.Single(_vm.Itens);
        Assert.Equal("FILME", filme.Nome);
        Assert.True(filme.Herdado);
        Assert.Equal("HERDADO", filme.Selo);
        Assert.Contains("definitiva", filme.Nota);
        Assert.Equal("Guarde o som atual como preset.", _vm.TextoVazio);

        // Memórias: a nota e o selo das 3, pela posição.
        Assert.All(memorias.Memorias, m => Assert.Equal("HERDADO", m.Selo));
        Assert.Contains("Aprovada", memorias.Memoria(1).NotaHerdada);
        Assert.Contains("MUSICA ORIG", memorias.Memoria(2).NotaHerdada);
        Assert.Contains("precisa ser refeita", memorias.Memoria(2).NotaHerdada);
        Assert.Contains("definitiva", memorias.Memoria(3).NotaHerdada);

        // As seções de texto.
        Assert.Equal("HERDADO", herdado.Selo);
        Assert.Equal(7, herdado.Secoes.Count);
        Assert.NotNull(herdado.Secao("Cuidados com o app da Taramps"));
        Assert.Equal("MUSICA HIGH", herdado.NotaMemoria(1)!.NomeNoPrototipo);

        Assert.Empty(_b.Simulador.Escritas);
        Assert.Null(_b.Arquivos.Conteudo(RepositorioTextoArquivos.ArquivoPresets));
        memorias.Dispose();
    }

    [Fact]
    public async Task Tocar_no_selo_mostra_a_nota_da_memoria_e_nao_manda_nada()
    {
        var memorias = _b.CriarMemoriasViewModel();
        var cartao = memorias.Memoria(2);
        Assert.False(cartao.NotaVisivel);

        await cartao.AlternarNota.ExecutarAsync();
        Assert.True(cartao.NotaVisivel);
        await cartao.AlternarNota.ExecutarAsync();
        Assert.False(cartao.NotaVisivel);

        Assert.Empty(_b.Simulador.Escritas);
        memorias.Dispose();
    }

    [Fact]
    public async Task O_selo_vale_pela_posicao_mesmo_com_o_nome_de_hoje_diferente()
    {
        var memorias = _b.CriarMemoriasViewModel();
        await _b.Conectar();

        Assert.Equal(["SPL", "MUSICA ORIG", "FILME"], memorias.Memorias.Select(m => m.Nome));
        Assert.Contains("MUSICA HIGH", memorias.Memoria(1).NotaHerdada); // o nome do protótipo, não o de hoje
        Assert.Equal("HERDADO", memorias.Memoria(1).Selo);
        memorias.Dispose();
    }

    // ----------------------------------------------------------------- guardar o som atual

    [Fact]
    public async Task Guardar_o_som_atual_cria_o_preset_proprio_limpa_o_campo_e_nada_vai_ao_aparelho()
    {
        await _b.Conectar();
        var antes = _b.Simulador.Escritas.Count;

        _vm.NomeNovo = "NOITE";
        await _b.Rodar(_vm.Guardar.ExecutarAsync());

        Assert.Equal(["FILME", "NOITE"], _vm.Itens.Select(i => i.Nome));
        Assert.False(_vm.Itens[1].Herdado);
        Assert.Null(_vm.Itens[1].Selo);
        Assert.Equal("", _vm.NomeNovo);
        Assert.Null(_vm.ErroNome);
        Assert.Null(_vm.TextoVazio);
        Assert.Equal("Preset NOITE guardado.", _vm.Mensagem);
        Assert.Equal(antes, _b.Simulador.Escritas.Count);
        Assert.Contains("\"NOITE\"", _b.Arquivos.Conteudo(RepositorioTextoArquivos.ArquivoPresets));
    }

    [Theory]
    [InlineData("")]
    [InlineData("ABCDEFGHIJKLMNOPQRSTUVWXY")]
    [InlineData("a#b")]
    public async Task Nome_invalido_mostra_a_frase_e_nada_e_guardado(string nome)
    {
        await _b.Conectar();

        _vm.NomeNovo = nome;
        await _b.Rodar(_vm.Guardar.ExecutarAsync());

        Assert.Equal("Use de 1 a 24 letras, números, espaço, _ ou -", _vm.ErroNome);
        Assert.Equal(nome, _vm.NomeNovo); // o campo fica como foi digitado
        Assert.Single(_vm.Itens);
        Assert.Null(_b.Arquivos.Conteudo(RepositorioTextoArquivos.ArquivoPresets));
    }

    [Theory]
    [InlineData("noite")]
    [InlineData("filme")]
    public async Task Nome_repetido_mostra_a_frase_e_nada_e_guardado(string repetido)
    {
        await _b.Conectar();
        await Guardar("NOITE");

        _vm.NomeNovo = repetido;
        await _b.Rodar(_vm.Guardar.ExecutarAsync());

        Assert.Equal("Já existe um preset com esse nome.", _vm.ErroNome);
        Assert.Equal(2, _vm.Itens.Count);
    }

    [Fact]
    public async Task Leitura_incompleta_mostra_quantos_ajustes_faltam_e_nada_e_guardado()
    {
        _b.Simulador.Silenciar.Add("#081#0#");
        await _b.Conectar();

        _vm.NomeNovo = "NOITE";
        await _b.Rodar(_vm.Guardar.ExecutarAsync());

        Assert.Equal("A leitura está incompleta (1 ajuste sem valor).", _vm.ErroNome);
        Assert.Single(_vm.Itens);
    }

    [Fact]
    public async Task Guardar_sem_conexao_fica_travado_com_o_motivo()
    {
        await _b.Chaves.Carregada;

        _vm.NomeNovo = "NOITE";
        await _vm.Guardar.ExecutarAsync();

        Assert.Equal("Sem conexão", _vm.MotivoTravaGuardar);
        Assert.False(_vm.PodeGuardar);
        Assert.Equal("Sem conexão", _vm.Mensagem);
        Assert.Single(_vm.Itens);
    }

    [Fact]
    public async Task Falha_ao_gravar_mostra_a_frase_e_a_lista_nao_muda()
    {
        await _b.Conectar();
        _b.Arquivos.Falhar = true;

        _vm.NomeNovo = "NOITE";
        await _b.Rodar(_vm.Guardar.ExecutarAsync());

        Assert.Equal("Não consegui guardar o preset no celular.", _vm.ErroNome);
        Assert.Single(_vm.Itens);
        Assert.Equal("NOITE", _vm.NomeNovo);
    }

    [Fact]
    public async Task Arquivo_estragado_so_os_herdados_na_lista_e_guardar_funciona()
    {
        var arquivos = new ArquivosFalso();
        await arquivos.GravarTextoAsync(RepositorioTextoArquivos.ArquivoPresets, "{ lixo");
        var b = new Bancada(arquivos);
        using var vm = b.CriarPresetsViewModel();
        await b.Conectar();

        Assert.Equal(["FILME"], vm.Itens.Select(i => i.Nome));
        Assert.Equal("Guarde o som atual como preset.", vm.TextoVazio);

        vm.NomeNovo = "NOVO";
        await b.Rodar(vm.Guardar.ExecutarAsync());

        Assert.Equal(["FILME", "NOVO"], vm.Itens.Select(i => i.Nome));
        ConferirSoAudioEstrito(b);
    }

    [Fact]
    public async Task Os_presets_guardados_voltam_quando_o_app_reabre()
    {
        await _b.Conectar();
        await Guardar("NOITE");

        var b = new Bancada(_b.Arquivos);
        using var vm = b.CriarPresetsViewModel();
        await b.Presets.Carregada;
        await b.Tela.EsvaziarAsync();

        Assert.Equal(["FILME", "NOITE"], vm.Itens.Select(i => i.Nome));
        Assert.Equal(_vm.Itens[1].Id, vm.Itens[1].Id);
    }

    // ----------------------------------------------------------------- aplicar

    [Fact]
    public async Task Criterio_de_aceite_guardar_A_mudar_ganho_e_banda_aplicar_A_o_simulador_bate()
    {
        await _b.Conectar();
        await Guardar("A");
        var alvo = _b.Simulador.EstadoMarco1();
        await Mudar();
        Assert.NotEqual(Linhas(alvo), Linhas(_b.Simulador.EstadoMarco1()));

        await _b.Rodar(_vm.Item("A")!.Aplicar.ExecutarAsync());

        Assert.Equal(Linhas(alvo), Linhas(_b.Simulador.EstadoMarco1()));
        foreach (var c in Catalogo.ConsultasMarco1)
            Assert.Equal(alvo[(c.Parametro.Codigo, c.Indice)], _b.Estado.Valor(c.Parametro.Codigo, c.Indice)!.NoFio);
        Assert.StartsWith("Preset A aplicado", _vm.Mensagem);
        Assert.Equal("Preset A aplicado. Para guardar numa memória, use Salvar.", _vm.Mensagem);
        Assert.False(_vm.PelaMetade);
        Assert.False(_vm.Ocupado);
        Assert.NotNull(_b.Arquivos.Conteudo(RepositorioTextoArquivos.ArquivoCopia)); // a cópia antes
    }

    [Fact]
    public async Task Aplicar_marca_ocupado_enquanto_anda()
    {
        await _b.Conectar();
        await Guardar("A");
        await Mudar();

        var tarefa = _vm.Item("A")!.Aplicar.ExecutarAsync();
        Assert.True(_vm.Ocupado);
        Assert.Equal("Uma operação está em andamento", _vm.MotivoTravaAplicar);
        await _b.Rodar(tarefa);

        Assert.False(_vm.Ocupado);
        Assert.Null(_vm.MotivoTravaAplicar);
    }

    [Fact]
    public async Task Aplicar_sem_confirmacao_e_o_herdado_FILME_deixa_o_aparelho_com_a_leitura_de_18_10()
    {
        await _b.Conectar();
        await Mudar();

        await _b.Rodar(_vm.Item("FILME")!.Aplicar.ExecutarAsync());

        Assert.Equal("Preset FILME aplicado. Para guardar numa memória, use Salvar.", _vm.Mensagem);
        Assert.Equal("+10.0", _b.Simulador.Valor("081", 0));
        Assert.Equal(Linhas(ConhecimentoHerdado.PresetFilme.Valores.ToDictionary(v => (v.Codigo, v.Indice), v => v.NoFio)),
            Linhas(_b.Simulador.EstadoMarco1()));
    }

    [Fact]
    public async Task Ja_aplicado_nada_sai_e_diz_que_ja_esta()
    {
        await _b.Conectar();
        await Guardar("NOITE");
        var antes = _b.Simulador.Escritas.Count;

        await _b.Rodar(_vm.Item("NOITE")!.Aplicar.ExecutarAsync());

        Assert.Equal("Preset NOITE já está aplicado.", _vm.Mensagem);
        Assert.Equal(antes, _b.Simulador.Escritas.Count);
        Assert.Null(_b.Arquivos.Conteudo(RepositorioTextoArquivos.ArquivoCopia));
    }

    [Fact]
    public async Task Chave_de_ajustes_desligada_trava_o_aplicar_com_o_motivo_e_nada_sai()
    {
        await _b.Conectar();
        await Guardar("A");
        await Mudar();
        await _b.Chaves.DefinirAsync(Chave.AjustesAudio, false, OrigemComando.Tela);
        await _b.Tela.EsvaziarAsync();
        var antes = _b.Simulador.Escritas.Count;

        await _vm.Item("A")!.Aplicar.ExecutarAsync();

        Assert.Equal("Chave desligada", _vm.MotivoTravaAplicar);
        Assert.False(_vm.PodeAplicar);
        Assert.Equal("Chave desligada", _vm.Mensagem);
        Assert.Equal(antes, _b.Simulador.Escritas.Count);
        Assert.True(_vm.PodeGuardar); // guardar não precisa de chave
    }

    [Fact]
    public async Task Sem_conexao_e_em_modo_so_leitura_o_aplicar_trava_com_o_motivo()
    {
        await _b.Chaves.Carregada;
        await _vm.Item("FILME")!.Aplicar.ExecutarAsync();
        Assert.Equal("Sem conexão", _vm.Mensagem);
        Assert.Empty(_b.Simulador.Escritas);

        _b.Simulador.Firmware = "R004";
        await _b.Conectar();
        Assert.Equal("Modo só leitura", _vm.MotivoTravaAplicar);
    }

    // ----------------------------------------------------------------- renomear

    [Fact]
    public async Task Renomear_abre_o_campo_com_o_nome_e_confirmar_muda_sem_mandar_nada()
    {
        await _b.Conectar();
        await Guardar("NOITE");
        var antes = _b.Simulador.Escritas.Count;

        await _vm.Item("NOITE")!.Renomear.ExecutarAsync();
        Assert.True(_vm.RenomearAberto);
        Assert.Equal("Renomear o preset NOITE", _vm.RenomearTitulo);
        Assert.Equal("NOITE", _vm.NomeRenomear);
        _vm.NomeRenomear = "Sala 2";
        await _b.Rodar(_vm.ConfirmarRenomear.ExecutarAsync());

        Assert.False(_vm.RenomearAberto);
        Assert.Equal(["FILME", "Sala 2"], _vm.Itens.Select(i => i.Nome));
        Assert.Equal(antes, _b.Simulador.Escritas.Count);
    }

    [Fact]
    public async Task Renomear_com_nome_ruim_o_campo_fica_com_o_erro_e_cancelar_fecha()
    {
        await _b.Conectar();
        await Guardar("NOITE");
        await _vm.Item("NOITE")!.Renomear.ExecutarAsync();

        _vm.NomeRenomear = "a#b";
        await _b.Rodar(_vm.ConfirmarRenomear.ExecutarAsync());

        Assert.True(_vm.RenomearAberto);
        Assert.Equal("Use de 1 a 24 letras, números, espaço, _ ou -", _vm.ErroRenomear);
        Assert.Equal("a#b", _vm.NomeRenomear);

        await _vm.CancelarRenomear.ExecutarAsync();
        Assert.False(_vm.RenomearAberto);
        Assert.Equal("NOITE", _vm.Item("NOITE")!.Nome);
    }

    [Fact]
    public async Task Renomear_ou_apagar_o_herdado_e_recusado_e_nenhuma_folha_abre()
    {
        await _b.Conectar();

        await _vm.Item("FILME")!.Renomear.ExecutarAsync();
        Assert.False(_vm.RenomearAberto);
        Assert.Equal("Preset herdado não muda.", _vm.Mensagem);

        await _vm.Item("FILME")!.Apagar.ExecutarAsync();
        Assert.False(_vm.ApagarAberto);
        Assert.Equal("Preset herdado não muda.", _vm.Mensagem);
        Assert.Single(_vm.Itens);
    }

    // ----------------------------------------------------------------- apagar

    [Fact]
    public async Task Apagar_abre_a_folha_e_nada_sai_nem_muda_antes_do_Confirmar()
    {
        await _b.Conectar();
        await Guardar("NOITE");
        var antes = _b.Simulador.Escritas.Count;
        var gravacoes = _b.Arquivos.Gravacoes;

        await _vm.Item("NOITE")!.Apagar.ExecutarAsync();

        Assert.True(_vm.ApagarAberto);
        Assert.Equal("Apagar o preset NOITE?", _vm.ApagarTitulo);
        Assert.Equal("Apagar o preset NOITE", _vm.TextoConfirmarApagar);
        Assert.Equal(["FILME", "NOITE"], _vm.Itens.Select(i => i.Nome));
        Assert.Equal(gravacoes, _b.Arquivos.Gravacoes);
        Assert.Equal(antes, _b.Simulador.Escritas.Count);
    }

    [Fact]
    public async Task Toque_duplo_no_Confirmar_de_apagar_nao_fecha_a_folha_nem_zera_o_andamento()
    {
        await _b.Conectar();
        await Guardar("NOITE");
        await _vm.Item("NOITE")!.Apagar.ExecutarAsync();
        using var trava = new ManualResetEventSlim();
        _b.Tela.Executar(trava.Wait); // segura a thread da tela: o primeiro toque fica andando

        var primeiro = _vm.ConfirmarApagar.ExecutarAsync();
        await _vm.ConfirmarApagar.ExecutarAsync(); // o segundo toque

        Assert.True(_vm.ApagarAberto);
        Assert.True(_vm.Ocupado);
        Assert.Equal("Uma operação está em andamento", _vm.Mensagem);
        Assert.False(primeiro.IsCompleted);

        trava.Set();
        await primeiro;
        await _b.Tela.EsvaziarAsync();
        Assert.False(_vm.ApagarAberto);
        Assert.False(_vm.Ocupado);
        Assert.Equal(["FILME"], _vm.Itens.Select(i => i.Nome));
        Assert.Equal("Preset NOITE apagado.", _vm.Mensagem);
    }

    [Fact]
    public async Task Cancelar_a_folha_de_apagar_o_preset_continua()
    {
        await _b.Conectar();
        await Guardar("NOITE");
        await _vm.Item("NOITE")!.Apagar.ExecutarAsync();

        await _vm.CancelarApagar.ExecutarAsync();

        Assert.False(_vm.ApagarAberto);
        Assert.Equal(["FILME", "NOITE"], _vm.Itens.Select(i => i.Nome));
    }

    [Fact]
    public async Task Confirmar_apaga_o_preset_fecha_a_folha_e_a_lista_volta_a_so_os_herdados()
    {
        await _b.Conectar();
        await Guardar("NOITE");
        await _vm.Item("NOITE")!.Apagar.ExecutarAsync();

        await _b.Rodar(_vm.ConfirmarApagar.ExecutarAsync());

        Assert.False(_vm.ApagarAberto);
        Assert.Equal(["FILME"], _vm.Itens.Select(i => i.Nome));
        Assert.Equal("Guarde o som atual como preset.", _vm.TextoVazio);
        Assert.Equal("Preset NOITE apagado.", _vm.Mensagem);
        Assert.DoesNotContain("NOITE", _b.Arquivos.Conteudo(RepositorioTextoArquivos.ArquivoPresets));
    }

    // ----------------------------------------------------------------- aplicado pela metade

    /// <summary>Guarda A, muda 2 ajustes, aplica com queda depois da 1ª escrita e reconecta.</summary>
    private async Task AplicarComQueda()
    {
        await _b.Conectar();
        await Guardar("A");
        await Mudar();
        _b.Simulador.CairDepoisDaEscrita = _b.Simulador.Escritas.Count + 1;

        await _b.Rodar(_vm.Item("A")!.Aplicar.ExecutarAsync());
        Assert.StartsWith("2 ajustes não foram escritos", _vm.Mensagem);

        await _b.Conectar();
        await Apoio.Esperar(() => _b.Copia.PelaMetade is not null);
        await _b.Tela.EsvaziarAsync();
    }

    [Fact]
    public async Task Queda_ao_aplicar_ao_reconectar_mostra_aplicado_pela_metade_com_os_dois_botoes()
    {
        await AplicarComQueda();

        Assert.True(_vm.PelaMetade);
        Assert.Equal("Aplicar o preset A caiu no meio.", _vm.TextoPelaMetade);
        Assert.NotNull(_vm.Terminar);
        Assert.NotNull(_vm.VoltarACopia);
        Assert.Equal(CopiaAntes.MsgPelaMetadePendente, _vm.MotivoTravaAplicar);
    }

    [Fact]
    public async Task Terminar_escreve_so_o_que_falta_e_conclui()
    {
        await AplicarComQueda();
        var antes = _b.Simulador.Escritas.Count;

        await _b.Rodar(_vm.Terminar.ExecutarAsync());

        Assert.False(_vm.PelaMetade);
        Assert.Equal("Preset A aplicado. Para guardar numa memória, use Salvar.", _vm.Mensagem);
        Assert.Single(_b.Simulador.Escritas.Skip(antes), e => Catalogo.EscritaDeAudio(e) is not null);
    }

    [Fact]
    public async Task Voltar_a_copia_devolve_os_ajustes_do_retrato_de_antes()
    {
        await AplicarComQueda();

        await _b.Rodar(_vm.VoltarACopia.ExecutarAsync());

        Assert.Equal("O som voltou à cópia.", _vm.Mensagem);
        Assert.False(_vm.PelaMetade);
        Assert.Equal("-3.0", _b.Simulador.Valor("081", 0));
    }

    [Fact]
    public async Task Terminar_sem_o_preset_diz_para_usar_Voltar_a_copia_e_continua_pela_metade()
    {
        await AplicarComQueda();
        var id = _vm.Item("A")!.Id;
        await _b.Rodar(_b.Presets.ApagarAsync(id));
        await _b.Tela.EsvaziarAsync();
        var antes = _b.Simulador.Escritas.Count;

        await _b.Rodar(_vm.Terminar.ExecutarAsync());

        Assert.Equal("O preset não existe mais; use Voltar à cópia.", _vm.Mensagem);
        Assert.True(_vm.PelaMetade);
        Assert.Equal(antes, _b.Simulador.Escritas.Count);
    }

    [Fact]
    public async Task Voltar_a_copia_com_a_chave_de_ajustes_desligada_nada_sai()
    {
        await AplicarComQueda();
        await _b.Chaves.DefinirAsync(Chave.AjustesAudio, false, OrigemComando.Tela);
        var antes = _b.Simulador.Escritas.Count;

        await _vm.VoltarACopia.ExecutarAsync();

        Assert.Equal("Chave desligada", _vm.Mensagem);
        Assert.True(_vm.PelaMetade);
        Assert.Equal(antes, _b.Simulador.Escritas.Count);
    }
}
