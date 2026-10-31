using BgsControle.Nucleo;
using BgsControle.Nucleo.Setups;
using BgsControle.Simulador;

namespace BgsControle.Telas.Testes;

/// <summary>
/// A tela Setup em dúvida / Trocar de Setup e o seletor do cabeçalho (história 8c), contra o
/// processador simulado.
/// </summary>
public sealed class SetupViewModelTestes : IDisposable
{
    private readonly Bancada _b = new(setups: true);
    private readonly SetupViewModel _vm;

    public SetupViewModelTestes() => _vm = _b.CriarSetupViewModel();

    public void Dispose()
    {
        _vm.Dispose();
        _b.ConferirSoAudio();
        Assert.Empty(_b.Tela.Erros);
    }

    private bool Mutados(bool mutado) =>
        Enumerable.Range(0, 4).All(c => _b.Simulador.Valor("100", c) == (mutado ? "1" : "0"));

    private async Task EntrarNaDuvida()
    {
        _b.Simulador.Definir("050", 2, "0.0"); // o atraso da saída 3: 80 cm no Casa 2.1
        await _b.Conectar();
        await _b.Tela.EsvaziarAsync();
        Assert.True(_vm.Aberta);
    }

    [Fact]
    public async Task Conectado_com_o_Setup_conferido_o_seletor_diz_o_Setup_e_a_tela_fica_fechada()
    {
        Assert.Equal("Sem Setup ›", _vm.Seletor);

        await _b.Conectar();
        await _b.Tela.EsvaziarAsync();

        Assert.Equal("Setup Casa 2.1 ›", _vm.Seletor);
        Assert.False(_vm.Aberta);
        Assert.Null(_vm.Titulo);
        Assert.Empty(_vm.Diferencas);
    }

    [Fact]
    public async Task Setup_em_duvida_mostra_o_titulo_a_frase_a_faixa_do_mudo_e_o_que_nao_bate()
    {
        await EntrarNaDuvida();

        Assert.Equal("Setup em dúvida", _vm.Titulo);
        Assert.Equal("O aparelho não está como o Casa 2.1 deixou da última vez.", _vm.Frase);
        Assert.Equal("Som mutado até você escolher.", _vm.FaixaMudo);
        Assert.Equal(["Atraso · Sub: 80 cm na FILME, 0 cm no aparelho"], _vm.Diferencas);
        Assert.False(_vm.PodeDesistir);
        Assert.Equal("Sem Setup ›", _vm.Seletor);
        Assert.Equal("O QUE NÃO BATE", SetupViewModel.RotuloDiferencas);
        var casa = Assert.Single(_vm.Setups);
        Assert.Equal("Casa 2.1", casa.Nome);
        Assert.True(casa.EmUso);
        Assert.True(_vm.PodeEscolher);
        Assert.True(Mutados(true));
    }

    [Fact]
    public async Task Escolher_o_Setup_pela_lista_aplica_desmuta_e_fecha_a_tela()
    {
        await EntrarNaDuvida();

        await _vm.AbrirLista.ExecutarAsync();
        Assert.True(_vm.ListaAberta);
        await _b.Rodar(_vm.Item("Casa 2.1")!.Escolher.ExecutarAsync());

        Assert.False(_vm.Aberta);
        Assert.False(_vm.ListaAberta);
        Assert.Equal("Setup Casa 2.1 em uso.", _vm.Mensagem);
        Assert.Equal("Setup Casa 2.1 ›", _vm.Seletor);
        Assert.True(Mutados(false));
        Assert.Equal("80.0", _b.Simulador.Valor("050", 2));
    }

    [Fact]
    public async Task Criar_Setup_novo_com_nome_invalido_o_campo_fica_com_o_erro_e_com_nome_bom_cria()
    {
        await EntrarNaDuvida();
        await _vm.AbrirCriar.ExecutarAsync();
        Assert.True(_vm.CriarAberto);

        _vm.NomeNovo = "Carro!";
        await _b.Rodar(_vm.ConfirmarCriar.ExecutarAsync());
        Assert.True(_vm.CriarAberto);
        Assert.Equal("Use de 1 a 24 letras, números, espaço, ponto, _ ou -", _vm.ErroNome); // o nome do Setup aceita o ponto
        Assert.Null(_vm.ErroNomeMemoria);
        Assert.True(_vm.Aberta);

        _vm.NomeNovo = "Carro";
        await _b.Rodar(_vm.ConfirmarCriar.ExecutarAsync());

        Assert.False(_vm.CriarAberto);
        Assert.False(_vm.Aberta);
        Assert.Equal("Setup Carro ›", _vm.Seletor);
        Assert.Equal("Setup Carro criado, com a memória Carro.", _vm.Mensagem);
        Assert.True(Mutados(false));
    }

    // ------------------------------------------------------------------ história 10c

    [Fact]
    public async Task Criar_Setup_novo_com_os_dois_nomes_cria_o_Setup_e_a_primeira_memoria()
    {
        await EntrarNaDuvida();
        await _vm.AbrirCriar.ExecutarAsync();
        Assert.Equal("Carro", _vm.DicaNomeMemoria); // vazio = o nome do Setup

        _vm.NomeNovo = "Carro";
        Assert.Equal("Carro", _vm.DicaNomeMemoria);
        _vm.NomeMemoriaNova = "ESTRADA";
        await _b.Rodar(_vm.ConfirmarCriar.ExecutarAsync());

        Assert.False(_vm.CriarAberto);
        Assert.Equal("Setup Carro criado, com a memória ESTRADA.", _vm.Mensagem);
        Assert.Equal("Setup Carro ›", _vm.Seletor);
        var memoria = _b.Setups!.Memoria(_b.Setups.EmUso.IdMemoria)!;
        Assert.Equal("ESTRADA", memoria.Nome);
        Assert.Equal(_b.Setups.SetupEmUso!.Id, memoria.IdSetup);
        Assert.True(Mutados(false));
    }

    [Fact]
    public async Task Nome_da_memoria_invalido_o_erro_fica_embaixo_do_campo_da_memoria_e_o_do_Setup_fica_sem_erro()
    {
        await EntrarNaDuvida();
        await _vm.AbrirCriar.ExecutarAsync();
        _vm.NomeNovo = "Carro";
        _vm.NomeMemoriaNova = "ESTRADA!";

        await _b.Rodar(_vm.ConfirmarCriar.ExecutarAsync());

        Assert.True(_vm.CriarAberto);
        Assert.Null(_vm.ErroNome);
        Assert.Equal(GerenteSetups.MsgNomeMemoria(Nucleo.Presets.GerentePresets.MsgNomeInvalido), _vm.ErroNomeMemoria);
        Assert.Equal("Carro", _vm.NomeNovo); // os campos ficam
        Assert.Equal("ESTRADA!", _vm.NomeMemoriaNova);
        Assert.Single(_b.Setups!.Setups);
        Assert.True(Mutados(true));

        // O nome do Setup errado depois: o erro passa para o campo do Setup.
        _vm.NomeNovo = "Carro#";
        _vm.NomeMemoriaNova = "ESTRADA";
        await _b.Rodar(_vm.ConfirmarCriar.ExecutarAsync());
        Assert.NotNull(_vm.ErroNome);
        Assert.Null(_vm.ErroNomeMemoria);

        await _vm.CancelarCriar.ExecutarAsync();
        Assert.Equal("", _vm.NomeMemoriaNova);
        Assert.Null(_vm.ErroNome);
    }

    [Fact]
    public async Task Uma_recusa_que_nao_e_de_um_nome_vai_para_a_mensagem_e_nao_para_o_campo()
    {
        await EntrarNaDuvida();
        await _vm.AbrirCriar.ExecutarAsync();
        _vm.NomeNovo = "Carro";
        var seguir = _b.Setups!.SeguirSemSetupAsync(); // a escolha fecha por fora
        await _b.Rodar(seguir);
        Assert.True(seguir.Result.Ok);

        await _b.Rodar(_vm.ConfirmarCriar.ExecutarAsync());

        Assert.True(_vm.CriarAberto);
        Assert.Null(_vm.ErroNome);
        Assert.Null(_vm.ErroNomeMemoria);
        Assert.Equal(GerenteSetups.MsgSoNaEscolha, _vm.Mensagem);
    }

    [Fact]
    public async Task A_dica_do_nome_da_memoria_e_avisada_ao_fechar_e_abrir_a_folha()
    {
        await EntrarNaDuvida();
        await _vm.AbrirCriar.ExecutarAsync();
        _vm.NomeNovo = "Barco";
        Assert.Equal("Barco", _vm.DicaNomeMemoria);
        var avisados = new List<string?>();
        _vm.PropertyChanged += (_, e) => avisados.Add(e.PropertyName);

        await _vm.CancelarCriar.ExecutarAsync();
        Assert.Contains(nameof(SetupViewModel.DicaNomeMemoria), avisados);
        Assert.Equal(SetupViewModel.DicaNomeSetup, _vm.DicaNomeMemoria);

        avisados.Clear();
        await _vm.AbrirCriar.ExecutarAsync();
        Assert.Contains(nameof(SetupViewModel.DicaNomeMemoria), avisados);
        Assert.Equal(SetupViewModel.DicaNomeSetup, _vm.DicaNomeMemoria);
    }

    [Fact]
    public void Os_textos_da_escolha_do_Setup_vem_do_ViewModel()
    {
        Assert.Equal("Aplica a memória ativa dele, relê e desmuta. Sem memória, só desmuta.", SetupViewModel.FraseEscolher);
        Assert.Equal("O som do aparelho vira a primeira memória dele.", SetupViewModel.FraseCriar);
        Assert.Equal("O QUE NÃO BATE", _vm.TituloDiferencas);
        Assert.Equal("lido agora no aparelho", _vm.SubtituloDiferencas);
        Assert.DoesNotContain("calibração", SetupViewModel.LinhaFolhaCriar, StringComparison.Ordinal);
        Assert.Equal("Nome do Setup", SetupViewModel.RotuloNomeSetup);
        Assert.Equal("Carro", SetupViewModel.DicaNomeSetup);
        Assert.Equal("Nome da primeira memória", SetupViewModel.RotuloNomeMemoria);
    }

    [Fact]
    public async Task Seguir_sem_Setup_desmuta_e_o_seletor_diz_sem_Setup()
    {
        await EntrarNaDuvida();

        await _b.Rodar(_vm.SeguirSemSetup.ExecutarAsync());

        Assert.False(_vm.Aberta);
        Assert.Equal("Sem Setup ›", _vm.Seletor);
        Assert.True(Mutados(false));
        Assert.Equal("0.0", _b.Simulador.Valor("050", 2));
    }

    [Fact]
    public async Task Tocar_no_seletor_abre_Trocar_de_Setup_mutado_e_desistir_volta()
    {
        await _b.Conectar();

        await _b.Rodar(_vm.Trocar.ExecutarAsync());

        Assert.True(_vm.Aberta);
        Assert.Equal("Trocar de Setup", _vm.Titulo);
        Assert.Equal("O aparelho está no Casa 2.1.", _vm.Frase);
        Assert.Empty(_vm.Diferencas);
        Assert.True(_vm.PodeDesistir);
        Assert.Equal("Som mutado até você escolher.", _vm.FaixaMudo);
        Assert.True(Mutados(true));

        await _b.Rodar(_vm.Desistir.ExecutarAsync());

        Assert.False(_vm.Aberta);
        Assert.Equal("Setup Casa 2.1 ›", _vm.Seletor);
        Assert.True(Mutados(false));
        Assert.Equal(SomEmUso.DoCelular(CenarioFilme.IdMemoria), _b.Setups!.EmUso);
    }

    [Fact]
    public async Task Na_duvida_o_Desistir_nao_faz_nada()
    {
        await EntrarNaDuvida();
        var antes = _b.Simulador.Escritas.Count;

        await _vm.Desistir.ExecutarAsync();
        await _b.Tela.EsvaziarAsync();

        Assert.True(_vm.Aberta);
        Assert.Equal(antes, _b.Simulador.Escritas.Count);
    }

    [Fact]
    public async Task Mutar_recusado_a_tela_abre_com_o_motivo_e_sem_a_faixa()
    {
        await _b.Chaves.Carregada;
        await _b.Chaves.DefinirAsync(Nucleo.Seguranca.Chave.AjustesAudio, false, Nucleo.Seguranca.OrigemComando.Tela);

        await EntrarNaDuvida();

        Assert.Null(_vm.FaixaMudo);
        Assert.Equal("O som não foi mutado: chave desligada", _vm.MotivoMudo);
    }
}
