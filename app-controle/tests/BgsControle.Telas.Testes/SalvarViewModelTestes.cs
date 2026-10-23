using BgsControle.Nucleo;
using BgsControle.Nucleo.Herdado;
using BgsControle.Nucleo.Setups;

namespace BgsControle.Telas.Testes;

/// <summary>A folha do Salvar por camadas (história 8c), contra o processador simulado.</summary>
public sealed class SalvarViewModelTestes : IDisposable
{
    private readonly Bancada _b = new(setups: true);
    private readonly SalvarViewModel _vm;

    public SalvarViewModelTestes() => _vm = _b.CriarSalvarViewModel();

    public void Dispose()
    {
        _vm.Dispose();
        _b.ConferirSoAudio();
        Assert.Empty(_b.Tela.Erros);
    }

    private GerenteSetups Setups => _b.Setups!;

    private static Parametro Ganho => Catalogo.ParametroDe("081")!;
    private static Parametro Atraso => Catalogo.ParametroDe("050")!;

    [Fact]
    public async Task Nada_mudou_o_Salvar_fica_travado_e_nao_abre()
    {
        await _b.Conectar();

        Assert.Equal("Nada para salvar.", _vm.MotivoTrava);
        Assert.False(_vm.PodeSalvar);
        await _vm.Abrir.ExecutarAsync();

        Assert.False(_vm.Aberta);
        Assert.Equal("Nada para salvar.", _vm.Mensagem);
    }

    [Fact]
    public async Task Com_a_memoria_do_celular_em_uso_a_folha_diz_o_que_vai_para_onde_e_salva_sem_mandar_nada()
    {
        await _b.Conectar();
        await _b.Rodar(_b.Escritor.EscreverAsync(Atraso, 2, 85.0m));
        await _b.Rodar(_b.Escritor.EscreverAsync(Ganho, 0, 8.0m));
        await _b.Tela.EsvaziarAsync();
        Assert.True(_vm.PodeSalvar);

        await _vm.Abrir.ExecutarAsync();

        Assert.True(_vm.Aberta);
        Assert.Equal("Salvar o que mudou?", _vm.Titulo);
        Assert.Equal("Cada mudança vai para o lugar dela.", _vm.Subtitulo);
        Assert.Equal(["Calibração → Setup Casa 2.1", "Caráter → memória FILME"], _vm.Linhas.Select(l => l.Titulo));
        Assert.Equal(["Atraso da saída 3: 80 cm → 85 cm"], _vm.Linhas[0].Mudancas);
        Assert.Equal(["Ganho da saída 1: +10,0 dB → +8,0 dB"], _vm.Linhas[1].Mudancas);
        Assert.False(_vm.Pergunta);
        Assert.False(_vm.PedeNome);
        Assert.True(_vm.PodeConfirmar);
        var antes = _b.Simulador.Escritas.Count;

        await _b.Rodar(_vm.Confirmar.ExecutarAsync());

        Assert.False(_vm.Aberta);
        Assert.Equal("Salvo: calibração no Casa 2.1, caráter na FILME.", _vm.Mensagem);
        Assert.Equal(antes, _b.Simulador.Escritas.Count);
        Assert.Equal("85.0", Setups.Setup(ConhecimentoHerdado.IdSetupCasa)!.Calibracao.Single(v => v.Codigo == "050" && v.Indice == 2).NoFio);
    }

    [Fact]
    public async Task Com_memoria_do_aparelho_e_calibracao_diferente_o_confirmar_trava_ate_escolher_e_dar_nome()
    {
        _b.Simulador.DefinirMemoria(1, "050", 2, "60.0");
        await _b.Conectar();
        await _b.Rodar(_b.Memorias.CarregarAsync(1));
        await _b.Tela.EsvaziarAsync();
        Assert.True(_vm.PodeSalvar); // com memória do aparelho, nunca trava por "nada mudou"

        await _vm.Abrir.ExecutarAsync();

        Assert.True(_vm.Pergunta);
        Assert.True(_vm.PedeNome);
        Assert.Equal("Manter a calibração do Casa 2.1", _vm.TextoManter);
        Assert.Equal("Atualizar o Casa 2.1 com a do aparelho", _vm.TextoAtualizar);
        Assert.Equal("Vale para todas as memórias do Casa 2.1.", _vm.AvisoAtualizar);
        Assert.Contains(_vm.Linhas, l => l.Titulo == "Caráter → memória nova");
        Assert.False(_vm.PodeConfirmar);

        _vm.NomeMemoria = "MUSICA ORIG";
        Assert.False(_vm.PodeConfirmar); // falta escolher
        await _vm.Confirmar.ExecutarAsync();
        Assert.True(_vm.Aberta);
        Assert.Equal(GerenteSetups.MsgEscolha, _vm.Erro);

        await _vm.EscolherAtualizar.ExecutarAsync();
        Assert.Equal(EscolhaCalibracao.Atualizar, _vm.Escolha);
        Assert.True(_vm.PodeConfirmar);
        await _b.Rodar(_vm.Confirmar.ExecutarAsync());

        Assert.False(_vm.Aberta);
        Assert.Equal("Salvo: calibração no Casa 2.1, caráter na MUSICA ORIG.", _vm.Mensagem);
        Assert.Contains(Setups.MemoriasDe(ConhecimentoHerdado.IdSetupCasa), m => m.Nome == "MUSICA ORIG");
    }

    [Fact]
    public async Task Manter_a_calibracao_reaplica_a_do_Setup_pelo_BLE()
    {
        _b.Simulador.DefinirMemoria(1, "050", 2, "60.0");
        await _b.Conectar();
        await _b.Rodar(_b.Memorias.CarregarAsync(1));
        await _vm.Abrir.ExecutarAsync();
        _vm.NomeMemoria = "MUSICA ORIG";
        await _vm.EscolherManter.ExecutarAsync();
        var antes = _b.Simulador.Escritas.Count;

        await _b.Rodar(_vm.Confirmar.ExecutarAsync());

        Assert.False(_vm.Aberta);
        Assert.Equal("Salvo: caráter na MUSICA ORIG. Calibração do Casa 2.1 reaplicada.", _vm.Mensagem);
        Assert.Equal(["*050*2*80.0*"], _b.Simulador.Escritas.Skip(antes).Where(e => Catalogo.EscritaDeAudio(e) is not null));
    }

    [Fact]
    public async Task Mexer_num_valor_no_aparelho_destrava_o_Salvar_e_avisa_a_tela()
    {
        await _b.Conectar();
        await _b.Tela.EsvaziarAsync();
        Assert.Equal("Nada para salvar.", _vm.MotivoTrava);
        var avisos = new List<string?>();
        _vm.PropertyChanged += (_, e) => avisos.Add(e.PropertyName);

        await _b.Rodar(_b.Escritor.EscreverAsync(Ganho, 0, 8.0m));
        await _b.Tela.EsvaziarAsync();

        Assert.Contains(nameof(SalvarViewModel.MotivoTrava), avisos);
        Assert.Contains(nameof(SalvarViewModel.PodeSalvar), avisos);
        Assert.True(_vm.PodeSalvar);
    }

    [Fact]
    public async Task Manter_com_a_reaplicacao_falhando_fecha_a_folha_e_diz_o_que_ficou()
    {
        _b.Simulador.DefinirMemoria(1, "050", 2, "60.0");
        await _b.Conectar();
        await _b.Rodar(_b.Memorias.CarregarAsync(1));
        await _vm.Abrir.ExecutarAsync();
        _vm.NomeMemoria = "MUSICA ORIG";
        await _vm.EscolherManter.ExecutarAsync();
        _b.Simulador.CairNaEscrita = _b.Simulador.Escritas.Count + 1;

        await _b.Rodar(_vm.Confirmar.ExecutarAsync());

        Assert.False(_vm.Aberta);
        Assert.StartsWith("Salvo: caráter na MUSICA ORIG. A calibração do Casa 2.1 não voltou:", _vm.Mensagem);
    }

    [Fact]
    public async Task Nome_repetido_a_folha_fica_aberta_com_o_erro()
    {
        await _b.Conectar();
        await _b.Rodar(_b.Memorias.CarregarAsync(0));
        await _vm.Abrir.ExecutarAsync();
        _vm.NomeMemoria = "filme";

        await _b.Rodar(_vm.Confirmar.ExecutarAsync());

        Assert.True(_vm.Aberta);
        Assert.Equal("Já existe uma memória com esse nome no Casa 2.1.", _vm.Erro);
        Assert.Equal("filme", _vm.NomeMemoria);
    }

    [Fact]
    public async Task Sem_Setup_o_Salvar_fica_travado()
    {
        _b.Simulador.Definir("050", 2, "0.0");
        await _b.Conectar();
        await _b.Rodar(Setups.SeguirSemSetupAsync());
        await _b.Tela.EsvaziarAsync();

        Assert.Equal("Escolha um Setup para salvar.", _vm.MotivoTrava);
    }

    [Fact]
    public async Task Cancelar_nao_guarda_nada()
    {
        await _b.Conectar();
        await _b.Rodar(_b.Escritor.EscreverAsync(Ganho, 0, 8.0m));
        var gravacoes = _b.Arquivos.Gravacoes;

        await _vm.Abrir.ExecutarAsync();
        await _vm.Cancelar.ExecutarAsync();

        Assert.False(_vm.Aberta);
        Assert.Equal(gravacoes, _b.Arquivos.Gravacoes);
        Assert.Equal("+10.0", Setups.Memoria(ConhecimentoHerdado.IdMemoriaFilme)!.Carater.Single(v => v.Codigo == "081" && v.Indice == 0).NoFio);
    }
}
