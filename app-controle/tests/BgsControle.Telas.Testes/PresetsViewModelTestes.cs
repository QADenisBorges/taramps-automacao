using BgsControle.Nucleo;
using BgsControle.Nucleo.Escrita;
using BgsControle.Nucleo.Herdado;
using BgsControle.Nucleo.Presets;
using BgsControle.Nucleo.Seguranca;
using System.Text.RegularExpressions;
using BgsControle.Simulador;

namespace BgsControle.Telas.Testes;

/// <summary>
/// As histórias 8 e 8d nas telas, contra o processador simulado: o <see cref="PresetsViewModel"/>
/// (presets de 15 bandas, o herdado Cinema, aplicar por cima da memória, Apagar travado no lembrado),
/// o <see cref="HerdadoViewModel"/> e o selo herdado nos cartões de memória.
/// </summary>
public sealed class PresetsViewModelTestes : IDisposable
{
    private readonly Bancada _b = new(setups: true);
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

    /// <summary>Cria um preset com as 15 bandas que estão no aparelho (o caminho da tela é a folha do Salvar).</summary>
    private async Task Criar(string nome)
    {
        var r = await _b.Presets.CriarAsync(nome);
        Assert.True(r.Ok, r.Mensagem);
        await _b.Tela.EsvaziarAsync();
    }

    /// <summary>Muda um ganho (que o preset não toca) e duas bandas (25 Hz e 160 Hz).</summary>
    private async Task Mudar()
    {
        await _b.Rodar(_b.Escritor.EscreverAsync(Ganho, 0, -3.0m));
        await _b.Rodar(_b.Escritor.EscreverAsync(Eq, 0, 2.0m));
        await _b.Rodar(_b.Escritor.EscreverAsync(Eq, 4, 3.0m));
    }

    private static string Linhas(IEnumerable<KeyValuePair<(string Codigo, int? Indice), string>> valores) =>
        string.Join("\n", valores.Select(kv => $"{kv.Key.Codigo}/{kv.Key.Indice}={kv.Value}").Order(StringComparer.Ordinal));

    private IEnumerable<KeyValuePair<(string Codigo, int? Indice), string>> Bandas() =>
        _b.Simulador.EstadoMarco1().Where(kv => kv.Key.Codigo == "020");

    // ----------------------------------------------------------------- primeira abertura

    [Fact]
    public async Task Primeira_abertura_sem_presets_nem_memorias_as_notas_e_as_secoes_estao_la_sem_arquivo_e_sem_digitar_nada()
    {
        var b = new Bancada(setups: true, filme: false);
        using var vm = b.CriarPresetsViewModel();
        await b.Setups!.Carregada;
        await b.Presets.Carregada;
        await b.Tela.EsvaziarAsync();
        var herdado = new HerdadoViewModel();
        using var memorias = b.CriarMemoriasViewModel();

        // Presets: nenhum (decisão do Denis, 29/10/2026).
        Assert.Empty(vm.Itens);
        Assert.Equal("Mexa nas 15 bandas e toque em Salvar para criar um preset.", vm.TextoVazio);

        // Memórias do celular: nenhuma no Casa 2.1.
        Assert.Empty(memorias.Cartoes);
        Assert.Equal("Memórias do Casa 2.1", memorias.TituloCelular);

        // Do aparelho: a nota das 3, pela posição, sem selo.
        Assert.Contains("máximo seguro", memorias.Aparelho(1).NotaHerdada);
        Assert.Contains("MUSICA ORIG", memorias.Aparelho(2).NotaHerdada);
        Assert.Contains("precisa ser refeita", memorias.Aparelho(2).NotaHerdada);
        Assert.Contains("definitiva", memorias.Aparelho(3).NotaHerdada);

        // As seções de texto.
        Assert.Equal("HERDADO", herdado.Selo);
        Assert.Equal(9, herdado.Secoes.Count);
        Assert.NotNull(herdado.Secao("Cuidados com o app da Taramps"));
        Assert.NotNull(herdado.Secao("Os equipamentos do Casa 2.1")); // história 8e
        Assert.Null(herdado.Secao("A FILME em três camadas"));
        Assert.NotNull(herdado.Secao("Memórias e presets do celular"));
        Assert.Equal("SPL", herdado.NotaMemoria(1)!.NomeNoPrototipo);

        Assert.Empty(b.Simulador.Escritas);
        Assert.Null(b.Arquivos.Conteudo(RepositorioTextoArquivos.ArquivoPresets));
        Assert.Null(b.Arquivos.Conteudo(RepositorioTextoArquivos.ArquivoSetups));
    }

    [Fact]
    public async Task Tocar_na_nota_mostra_a_nota_da_memoria_e_nao_manda_nada()
    {
        using var memorias = _b.CriarMemoriasViewModel();
        var cartao = memorias.Aparelho(2);
        Assert.False(cartao.NotaVisivel);

        await cartao.AlternarNota.ExecutarAsync();
        Assert.True(cartao.NotaVisivel);
        await cartao.AlternarNota.ExecutarAsync();
        Assert.False(cartao.NotaVisivel);

        Assert.Empty(_b.Simulador.Escritas);
    }

    [Fact]
    public async Task A_nota_vale_pela_posicao_mesmo_com_o_nome_de_hoje_diferente()
    {
        using var memorias = _b.CriarMemoriasViewModel();
        _b.Simulador.Definir("231", null, "OUTRA");
        await _b.Conectar();

        Assert.Equal(["OUTRA", "MUSICA ORIG", "FILME"], memorias.DoAparelho.Select(m => m.Nome));
        Assert.Contains("SPL", memorias.Aparelho(1).NotaHerdada); // o nome do protótipo, não o de hoje
    }

    [Fact]
    public async Task Arquivo_estragado_nenhum_preset_na_lista()
    {
        var arquivos = new ArquivosFalso();
        await arquivos.GravarTextoAsync(RepositorioTextoArquivos.ArquivoPresets, "{ lixo");
        var b = new Bancada(arquivos, setups: true);
        using var vm = b.CriarPresetsViewModel();
        await b.Conectar();

        Assert.Empty(vm.Itens);
        Assert.Equal("Mexa nas 15 bandas e toque em Salvar para criar um preset.", vm.TextoVazio);
        ConferirSoAudioEstrito(b);
    }

    [Fact]
    public async Task Os_presets_guardados_voltam_quando_o_app_reabre()
    {
        await _b.Conectar();
        await Criar("NOITE");
        Assert.Equal(["Cinema", "NOITE"], _vm.Itens.Select(i => i.Nome));
        Assert.Null(_vm.TextoVazio);

        var b = new Bancada(_b.Arquivos, setups: true);
        using var vm = b.CriarPresetsViewModel();
        await b.Presets.Carregada;
        await b.Tela.EsvaziarAsync();

        Assert.Equal(["Cinema", "NOITE"], vm.Itens.Select(i => i.Nome));
        Assert.Equal(_vm.Itens[1].Id, vm.Itens[1].Id);
    }

    // ----------------------------------------------------------------- aplicar

    [Fact]
    public async Task Aplicar_A_muda_so_as_15_bandas_a_FILME_passa_a_lembrar_A_e_a_frase_diz_onde()
    {
        await _b.Conectar();
        await Criar("A"); // as bandas do Cinema
        var alvo = Bandas().ToArray();
        await Mudar();
        Assert.NotEqual(Linhas(alvo), Linhas(Bandas()));

        await _b.Rodar(_vm.Item("A")!.Aplicar.ExecutarAsync());

        Assert.Equal(Linhas(alvo), Linhas(Bandas()));
        Assert.Equal("-3.0", _b.Simulador.Valor("081", 0)); // o caráter não muda
        foreach (var (chave, valor) in alvo)
            Assert.Equal(valor, _b.Estado.Valor(chave.Codigo, chave.Indice)!.NoFio);
        Assert.Equal("Preset A aplicado na FILME.", _vm.Mensagem);
        Assert.Equal(_vm.Item("A")!.Id, _b.Setups!.Memoria(CenarioFilme.IdMemoria)!.IdPreset);
        Assert.True(_vm.Item("A")!.EmUso);
        Assert.False(_vm.Item("Cinema")!.EmUso);
        Assert.False(_vm.PelaMetade);
        Assert.False(_vm.Ocupado);
        Assert.NotNull(_b.Arquivos.Conteudo(RepositorioTextoArquivos.ArquivoCopia)); // a cópia antes
    }

    [Fact]
    public async Task Aplicar_marca_ocupado_enquanto_anda()
    {
        await _b.Conectar();
        await Criar("A");
        await Mudar();

        var tarefa = _vm.Item("A")!.Aplicar.ExecutarAsync();
        Assert.True(_vm.Ocupado);
        Assert.Equal("Uma operação está em andamento", _vm.MotivoTravaAplicar);
        await _b.Rodar(tarefa);

        Assert.False(_vm.Ocupado);
        Assert.Null(_vm.MotivoTravaAplicar);
    }

    [Fact]
    public async Task Aplicar_o_Cinema_herdado_sem_confirmacao_volta_as_15_bandas_de_18_10()
    {
        await _b.Conectar();
        await Mudar();

        await _b.Rodar(_vm.Item("Cinema")!.Aplicar.ExecutarAsync());

        Assert.Equal("Preset Cinema aplicado na FILME.", _vm.Mensagem);
        Assert.Equal("-3.0", _b.Simulador.Valor("081", 0));
        Assert.Equal(Linhas(CenarioFilme.Bandas.ToDictionary(v => (v.Codigo, v.Indice), v => v.NoFio)), Linhas(Bandas()));
        Assert.True(_vm.Item("Cinema")!.EmUso);
    }

    [Fact]
    public async Task Ja_aplicado_nada_sai_e_diz_que_ja_esta()
    {
        await _b.Conectar();
        await Criar("NOITE");
        var antes = _b.Simulador.Escritas.Count;

        await _b.Rodar(_vm.Item("NOITE")!.Aplicar.ExecutarAsync());

        Assert.Equal("Preset NOITE já está aplicado.", _vm.Mensagem);
        Assert.Equal(antes, _b.Simulador.Escritas.Count);
        Assert.Null(_b.Arquivos.Conteudo(RepositorioTextoArquivos.ArquivoCopia));
        Assert.True(_vm.Item("NOITE")!.EmUso); // a FILME lembra o NOITE mesmo assim
    }

    [Fact]
    public async Task Chave_de_ajustes_desligada_trava_o_aplicar_com_o_motivo_e_nada_sai()
    {
        await _b.Conectar();
        await Criar("A");
        await Mudar();
        await _b.Chaves.DefinirAsync(Chave.AjustesAudio, false, OrigemComando.Tela);
        await _b.Tela.EsvaziarAsync();
        var antes = _b.Simulador.Escritas.Count;

        await _vm.Item("A")!.Aplicar.ExecutarAsync();

        Assert.Equal("Chave desligada", _vm.MotivoTravaAplicar);
        Assert.False(_vm.PodeAplicar);
        Assert.Equal("Chave desligada", _vm.Mensagem);
        Assert.Equal(antes, _b.Simulador.Escritas.Count);
    }

    [Fact]
    public async Task Sem_conexao_e_em_modo_so_leitura_o_aplicar_trava_com_o_motivo()
    {
        await _b.Chaves.Carregada;
        await _b.Presets.Carregada; // o Cinema do cenário vem do presets.json
        await _b.Tela.EsvaziarAsync();
        await _vm.Item("Cinema")!.Aplicar.ExecutarAsync();
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
        await Criar("NOITE");
        var antes = _b.Simulador.Escritas.Count;

        await _vm.Item("NOITE")!.Renomear.ExecutarAsync();
        Assert.True(_vm.RenomearAberto);
        Assert.Equal("Renomear o preset NOITE", _vm.RenomearTitulo);
        Assert.Equal("NOITE", _vm.NomeRenomear);
        _vm.NomeRenomear = "Sala 2";
        await _b.Rodar(_vm.ConfirmarRenomear.ExecutarAsync());

        Assert.False(_vm.RenomearAberto);
        Assert.Equal(["Cinema", "Sala 2"], _vm.Itens.Select(i => i.Nome));
        Assert.Equal(antes, _b.Simulador.Escritas.Count);
    }

    [Fact]
    public async Task Renomear_com_nome_ruim_o_campo_fica_com_o_erro_e_cancelar_fecha()
    {
        await _b.Conectar();
        await Criar("NOITE");
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
    public async Task Renomear_o_preset_lembrado_muda_o_nome_no_cartao_da_memoria()
    {
        await _b.Conectar();
        await Criar("A");
        await _b.Rodar(_vm.Item("A")!.Aplicar.ExecutarAsync());
        using var memorias = _b.CriarMemoriasViewModel();
        Assert.Equal("Preset: A", memorias.Cartao("FILME")!.TextoPreset);

        await _vm.Item("A")!.Renomear.ExecutarAsync();
        _vm.NomeRenomear = "Rock";
        await _b.Rodar(_vm.ConfirmarRenomear.ExecutarAsync());
        await _b.Tela.EsvaziarAsync();

        Assert.Equal("Preset: Rock", memorias.Cartao("FILME")!.TextoPreset);
    }

    // ----------------------------------------------------------------- apagar

    [Fact]
    public async Task Apagar_um_preset_lembrado_fica_travado_com_quantas_memorias_e_nenhuma_folha_abre()
    {
        await _b.Conectar();
        await Criar("A");
        await _b.Rodar(_vm.Item("A")!.Aplicar.ExecutarAsync()); // a FILME lembra o A
        await _b.Tela.EsvaziarAsync();

        Assert.Equal("Lembrado por 1 memória.", _vm.Item("A")!.MotivoTravaApagar);
        Assert.False(_vm.Item("A")!.PodeApagar);
        await _vm.Item("A")!.Apagar.ExecutarAsync();

        Assert.False(_vm.ApagarAberto);
        Assert.Equal("Lembrado por 1 memória.", _vm.Mensagem);
        Assert.Equal(["Cinema", "A"], _vm.Itens.Select(i => i.Nome));
    }

    [Fact]
    public async Task Apagar_abre_a_folha_e_nada_sai_nem_muda_antes_do_Confirmar()
    {
        await _b.Conectar();
        await Criar("NOITE");
        var antes = _b.Simulador.Escritas.Count;
        var gravacoes = _b.Arquivos.Gravacoes;
        Assert.True(_vm.Item("NOITE")!.PodeApagar);

        await _vm.Item("NOITE")!.Apagar.ExecutarAsync();

        Assert.True(_vm.ApagarAberto);
        Assert.Equal("Apagar o preset NOITE?", _vm.ApagarTitulo);
        Assert.Equal("Apagar o preset NOITE", _vm.TextoConfirmarApagar);
        Assert.Equal(["Cinema", "NOITE"], _vm.Itens.Select(i => i.Nome));
        Assert.Equal(gravacoes, _b.Arquivos.Gravacoes);
        Assert.Equal(antes, _b.Simulador.Escritas.Count);
    }

    [Fact]
    public async Task Toque_duplo_no_Confirmar_de_apagar_nao_fecha_a_folha_nem_zera_o_andamento()
    {
        await _b.Conectar();
        await Criar("NOITE");
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
        Assert.Equal(["Cinema"], _vm.Itens.Select(i => i.Nome));
        Assert.Equal("Preset NOITE apagado.", _vm.Mensagem);
    }

    [Fact]
    public async Task Cancelar_a_folha_de_apagar_o_preset_continua()
    {
        await _b.Conectar();
        await Criar("NOITE");
        await _vm.Item("NOITE")!.Apagar.ExecutarAsync();

        await _vm.CancelarApagar.ExecutarAsync();

        Assert.False(_vm.ApagarAberto);
        Assert.Equal(["Cinema", "NOITE"], _vm.Itens.Select(i => i.Nome));
    }

    [Fact]
    public async Task Confirmar_apaga_o_preset_fecha_a_folha_e_a_lista_volta_a_so_o_Cinema()
    {
        await _b.Conectar();
        await Criar("NOITE");
        await _vm.Item("NOITE")!.Apagar.ExecutarAsync();

        await _b.Rodar(_vm.ConfirmarApagar.ExecutarAsync());

        Assert.False(_vm.ApagarAberto);
        Assert.Equal(["Cinema"], _vm.Itens.Select(i => i.Nome));
        Assert.Null(_vm.TextoVazio); // o Cinema do cenário é um preset do celular
        Assert.Equal("Preset NOITE apagado.", _vm.Mensagem);
        Assert.DoesNotContain("NOITE", _b.Arquivos.Conteudo(RepositorioTextoArquivos.ArquivoPresets));
    }

    // ----------------------------------------------------------------- aplicado pela metade

    /// <summary>Cria A, muda duas bandas, aplica com queda depois da 1ª escrita e reconecta.</summary>
    private async Task AplicarComQueda()
    {
        await _b.Conectar();
        await Criar("A");
        await Mudar();
        _b.Simulador.CairDepoisDaEscrita = _b.Simulador.Escritas.Count + 1;

        await _b.Rodar(_vm.Item("A")!.Aplicar.ExecutarAsync());
        Assert.StartsWith("2 bandas não foram escritas", _vm.Mensagem);

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
        Assert.Equal("Preset A aplicado.", _vm.Mensagem);
        Assert.Single(_b.Simulador.Escritas.Skip(antes), e => Catalogo.EscritaDeAudio(e) is not null);
    }

    [Fact]
    public async Task Voltar_a_copia_devolve_as_bandas_do_retrato_de_antes()
    {
        await AplicarComQueda();

        await _b.Rodar(_vm.VoltarACopia.ExecutarAsync());

        Assert.Equal("O som voltou à cópia.", _vm.Mensagem);
        Assert.False(_vm.PelaMetade);
        Assert.Equal("+2.0", _b.Simulador.Valor("020", 0));
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
