using BgsControle.Nucleo;
using BgsControle.Nucleo.Herdado;
using BgsControle.Nucleo.Presets;
using BgsControle.Nucleo.Setups;

namespace BgsControle.Telas.Testes;

/// <summary>A folha do Salvar por camadas (histórias 8c e 8d, com a linha das 15 bandas), contra o processador simulado.</summary>
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
    private static Parametro Banda => Catalogo.ParametroDe("020")!;

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
        // O Link 3–4 do Casa 2.1 (história 8e) repete o atraso no CH4.
        Assert.Equal(["Atraso da saída 3: 80 cm → 85 cm", "Atraso da saída 4: 80 cm → 85 cm"], _vm.Linhas[0].Mudancas);
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

    // ----------------------------------------------------------------- 8d: a linha das 15 bandas

    [Fact]
    public async Task Bandas_mexidas_na_FILME_com_o_Cinema_Atualizar_travado_Criar_ja_escolhido_e_o_nome_do_preset()
    {
        await _b.Conectar();
        await _b.Rodar(_b.Escritor.EscreverAsync(Banda, 4, 3.0m)); // 160 Hz
        await _b.Tela.EsvaziarAsync();
        Assert.True(_vm.PodeSalvar);

        await _vm.Abrir.ExecutarAsync();

        var linha = Assert.Single(_vm.Linhas);
        Assert.Equal("15 bandas → preset Cinema", linha.Titulo);
        Assert.Equal("HERDADO", linha.Selo);
        Assert.Equal(["Banda 160 Hz: +1,0 dB → +3,0 dB"], linha.Mudancas);
        Assert.True(_vm.PerguntaPreset);
        Assert.Equal("Atualizar o preset Cinema", _vm.TextoAtualizarPreset);
        Assert.Equal("Vale para toda memória que usa ele.", _vm.AvisoAtualizarPreset);
        Assert.Equal("Preset herdado não é sobrescrito.", _vm.MotivoAtualizarPresetTravado);
        Assert.False(_vm.PodeAtualizarPreset);
        Assert.Equal(EscolhaPreset.Criar, _vm.EscolhaPreset); // já vem escolhido
        Assert.True(_vm.PedeNomePreset);
        Assert.False(_vm.PedeNome);
        Assert.False(_vm.PodeConfirmar); // falta o nome do preset

        await _vm.EscolherAtualizarPreset.ExecutarAsync();
        Assert.Equal("Preset herdado não é sobrescrito.", _vm.Erro);
        Assert.Equal(EscolhaPreset.Criar, _vm.EscolhaPreset);

        // Nome repetido: a folha fica, nada guardado.
        _vm.NomePreset = "cinema";
        Assert.True(_vm.PodeConfirmar);
        await _b.Rodar(_vm.Confirmar.ExecutarAsync());
        Assert.True(_vm.Aberta);
        Assert.Equal("Nome do preset: Já existe um preset com esse nome.", _vm.Erro);
        Assert.Empty(_b.Presets.Proprios);

        _vm.NomePreset = "CINEMA 2";
        await _b.Rodar(_vm.Confirmar.ExecutarAsync());

        Assert.False(_vm.Aberta);
        Assert.Equal("Salvo: 15 bandas no preset CINEMA 2.", _vm.Mensagem);
        var novo = Assert.Single(_b.Presets.Proprios);
        Assert.Equal(novo.Id, Setups.Memoria(ConhecimentoHerdado.IdMemoriaFilme)!.IdPreset);
        Assert.Equal("Nada para salvar.", _vm.MotivoTrava);
    }

    [Fact]
    public async Task Bandas_mexidas_com_um_preset_proprio_nenhuma_escolha_vem_marcada_e_Atualizar_vale_para_ele()
    {
        await _b.Conectar();
        var rock = (await _b.Presets.CriarAsync("Rock", ConhecimentoHerdado.BandasCinema.Select(v => v.Indice == 0 ? v with { NoFio = "+4.0" } : v).ToArray())).Preset!;
        await _b.Rodar(_b.Presets.AplicarAsync(rock.Id)); // a FILME lembra o Rock
        await _b.Rodar(_b.Escritor.EscreverAsync(Banda, 1, -2.0m));
        await _b.Tela.EsvaziarAsync();

        await _vm.Abrir.ExecutarAsync();

        Assert.Equal(["15 bandas → preset Rock"], _vm.Linhas.Select(l => l.Titulo));
        Assert.Null(_vm.Linhas[0].Selo);
        Assert.True(_vm.PodeAtualizarPreset);
        Assert.Equal(EscolhaPreset.Nenhuma, _vm.EscolhaPreset);
        Assert.False(_vm.PedeNomePreset);
        Assert.False(_vm.PodeConfirmar); // travado até escolher

        await _vm.EscolherCriarPreset.ExecutarAsync();
        Assert.True(_vm.PedeNomePreset);
        await _vm.EscolherAtualizarPreset.ExecutarAsync();
        Assert.False(_vm.PedeNomePreset);
        Assert.True(_vm.PodeConfirmar);
        await _b.Rodar(_vm.Confirmar.ExecutarAsync());

        Assert.Equal("Salvo: 15 bandas no preset Rock.", _vm.Mensagem);
        Assert.Equal("-2.0", _b.Presets.Obter(rock.Id)!.Valores.Single(v => v.Indice == 1).NoFio);
    }

    [Fact]
    public async Task Memoria_nova_com_bandas_diferentes_de_todos_pede_os_dois_nomes()
    {
        _b.Simulador.DefinirMemoria(1, "020", 0, "+6.0");
        await _b.Conectar();
        await _b.Rodar(_b.Memorias.CarregarAsync(1));
        await _b.Tela.EsvaziarAsync();

        await _vm.Abrir.ExecutarAsync();

        Assert.Contains("15 bandas → preset novo", _vm.Linhas.Select(l => l.Titulo));
        Assert.True(_vm.PedeNome);
        Assert.True(_vm.PedeNomePreset);
        Assert.False(_vm.PerguntaPreset);
        _vm.NomeMemoria = "MUSICA ORIG";
        Assert.False(_vm.PodeConfirmar);
        _vm.NomePreset = "ORIG EQ";
        Assert.True(_vm.PodeConfirmar);
        await _b.Rodar(_vm.Confirmar.ExecutarAsync());

        Assert.Equal("Salvo: caráter na MUSICA ORIG, 15 bandas no preset ORIG EQ.", _vm.Mensagem);
        Assert.Equal(_b.Presets.Proprios.Single().Id, Setups.MemoriasDe(ConhecimentoHerdado.IdSetupCasa).Single(m => m.Nome == "MUSICA ORIG").IdPreset);
    }

    [Fact]
    public async Task Memoria_nova_com_as_bandas_do_Cinema_so_pede_o_nome_da_memoria()
    {
        await _b.Conectar();
        await _b.Rodar(_b.Memorias.CarregarAsync(1));
        await _b.Tela.EsvaziarAsync();

        await _vm.Abrir.ExecutarAsync();

        var bandas = _vm.Linhas.Single(l => l.Titulo.StartsWith("15 bandas", StringComparison.Ordinal));
        Assert.Equal("15 bandas → preset Cinema", bandas.Titulo);
        Assert.Equal("HERDADO", bandas.Selo);
        Assert.False(_vm.PedeNomePreset);
        _vm.NomeMemoria = "MUSICA ORIG";
        Assert.True(_vm.PodeConfirmar);
        await _b.Rodar(_vm.Confirmar.ExecutarAsync());

        Assert.Equal("Salvo: caráter na MUSICA ORIG.", _vm.Mensagem);
        Assert.Equal(ConhecimentoHerdado.IdCinema, Setups.MemoriasDe(ConhecimentoHerdado.IdSetupCasa).Single(m => m.Nome == "MUSICA ORIG").IdPreset);
    }

    [Fact]
    public async Task Nome_de_preset_invalido_a_frase_diz_que_e_o_nome_do_preset_e_nada_e_guardado()
    {
        await _b.Conectar();
        await _b.Rodar(_b.Escritor.EscreverAsync(Banda, 4, 3.0m));
        await _b.Tela.EsvaziarAsync();
        await _vm.Abrir.ExecutarAsync();

        _vm.NomePreset = "a#b";
        await _b.Rodar(_vm.Confirmar.ExecutarAsync());

        Assert.True(_vm.Aberta);
        Assert.Equal("Nome do preset: Use de 1 a 24 letras, números, espaço, _ ou -", _vm.Erro);
        Assert.Equal("a#b", _vm.NomePreset);
        Assert.Empty(_b.Presets.Proprios);
    }
}
