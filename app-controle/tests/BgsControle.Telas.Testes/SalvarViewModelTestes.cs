using BgsControle.Nucleo;
using BgsControle.Nucleo.Herdado;
using BgsControle.Nucleo.Presets;
using BgsControle.Nucleo.Setups;
using BgsControle.Simulador;

namespace BgsControle.Telas.Testes;

/// <summary>
/// A folha do Salvar por camadas (histórias 8c, 8d e 8f): a regulagem vai para a memória (em uso, ou
/// uma nova) e as 15 bandas para o preset; não há mais calibração no Setup nem Manter/Atualizar.
/// Contra o processador simulado, com a FILME e o Cinema do cenário de teste.
/// </summary>
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

    private static string? Valor(MemoriaCelular m, string codigo, int indice) =>
        m.Regulagem.SingleOrDefault(v => v.Codigo == codigo && v.Indice == indice)?.NoFio;

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
    public async Task Com_a_memoria_do_celular_em_uso_a_regulagem_vai_para_ela_e_nada_vai_ao_aparelho()
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
        var linha = Assert.Single(_vm.Linhas);
        Assert.Equal("Regulagem → memória FILME", linha.Titulo);
        // O par CH3 + CH4 (o Sub, com o Link 3–4) muda igual: uma linha só, pelo nome da saída.
        Assert.Equal(["Atraso · Sub: 80 cm → 85 cm", "Ganho · JBL L: +10,0 dB → +8,0 dB"], linha.Mudancas.Order());
        Assert.Equal(2, linha.Itens.Single(i => i.Antes.StartsWith("Atraso", StringComparison.Ordinal)).Canal);
        Assert.False(_vm.PedeNome);
        Assert.True(_vm.PodeConfirmar);
        var antes = _b.Simulador.Escritas.Count;

        await _b.Rodar(_vm.Confirmar.ExecutarAsync());

        Assert.False(_vm.Aberta);
        Assert.Equal("Salvo: regulagem na FILME.", _vm.Mensagem);
        Assert.Equal(antes, _b.Simulador.Escritas.Count);
        Assert.Equal("85.0", Valor(Setups.Memoria(CenarioFilme.IdMemoria)!, "050", 2));
        Assert.Equal("+8.0", Valor(Setups.Memoria(CenarioFilme.IdMemoria)!, "081", 0));
    }

    [Fact]
    public async Task Com_o_som_do_aparelho_o_confirmar_trava_ate_dar_o_nome_e_a_regulagem_vai_para_uma_memoria_nova()
    {
        _b.Simulador.Definir("081", 0, "-5.0"); // só o ganho difere da FILME: o som do aparelho
        await _b.Conectar();
        await _b.Tela.EsvaziarAsync();
        Assert.True(_vm.PodeSalvar); // com o som do aparelho, nunca trava por "nada mudou"

        await _vm.Abrir.ExecutarAsync();

        Assert.True(_vm.PedeNome);
        Assert.Equal(["Regulagem → memória nova", "15 bandas → preset Cinema"], _vm.Linhas.Select(l => l.Titulo));
        Assert.False(_vm.PodeConfirmar);
        await _vm.Confirmar.ExecutarAsync();
        Assert.True(_vm.Aberta);
        Assert.Equal(SalvarViewModel.MsgFalteNome, _vm.Erro);

        _vm.NomeMemoria = "MUSICA ORIG";
        Assert.True(_vm.PodeConfirmar);
        var antes = _b.Simulador.Escritas.Count;
        await _b.Rodar(_vm.Confirmar.ExecutarAsync());

        Assert.False(_vm.Aberta);
        Assert.Equal("Salvo: regulagem na MUSICA ORIG.", _vm.Mensagem);
        Assert.Equal(antes, _b.Simulador.Escritas.Count); // o Salvar não manda nada pelo BLE
        var nova = Setups.MemoriasDe(ConhecimentoHerdado.IdSetupCasa).Single(m => m.Nome == "MUSICA ORIG");
        Assert.Equal("-5.0", Valor(nova, "081", 0));
        Assert.Equal(CenarioFilme.IdPreset, nova.IdPreset);
    }

    [Fact]
    public async Task Nome_repetido_a_folha_fica_aberta_com_o_erro()
    {
        _b.Simulador.Definir("081", 0, "+4.0"); // o som do aparelho: a regulagem vai para uma memória nova
        await _b.Conectar();
        await _b.Tela.EsvaziarAsync();
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
    public async Task Cancelar_nao_guarda_nada()
    {
        await _b.Conectar();
        await _b.Rodar(_b.Escritor.EscreverAsync(Ganho, 0, 8.0m));
        var gravacoes = _b.Arquivos.Gravacoes;

        await _vm.Abrir.ExecutarAsync();
        await _vm.Cancelar.ExecutarAsync();

        Assert.False(_vm.Aberta);
        Assert.Equal(gravacoes, _b.Arquivos.Gravacoes);
        Assert.Equal("+10.0", Valor(Setups.Memoria(CenarioFilme.IdMemoria)!, "081", 0));
    }

    // ----------------------------------------------------------------- 8d: a linha das 15 bandas

    [Fact]
    public async Task Bandas_mexidas_na_FILME_com_o_Cinema_pergunta_Atualizar_ou_Criar_e_Criar_pede_o_nome()
    {
        await _b.Conectar();
        await _b.Rodar(_b.Escritor.EscreverAsync(Banda, 4, 3.0m)); // 160 Hz
        await _b.Tela.EsvaziarAsync();
        Assert.True(_vm.PodeSalvar);

        await _vm.Abrir.ExecutarAsync();

        var linha = Assert.Single(_vm.Linhas);
        Assert.Equal("15 bandas → preset Cinema", linha.Titulo);
        Assert.Equal(["Banda 160 Hz: +1,0 dB → +3,0 dB"], linha.Mudancas);
        Assert.True(_vm.PerguntaPreset);
        Assert.Equal("Atualizar o preset Cinema", _vm.TextoAtualizarPreset);
        Assert.Equal("Vale para toda memória que usa ele.", _vm.AvisoAtualizarPreset);
        Assert.Null(_vm.MotivoAtualizarPresetTravado); // não há mais preset herdado
        Assert.True(_vm.PodeAtualizarPreset);
        Assert.Equal(EscolhaPreset.Nenhuma, _vm.EscolhaPreset);
        Assert.False(_vm.PedeNome);
        Assert.False(_vm.PodeConfirmar); // falta escolher

        await _vm.EscolherCriarPreset.ExecutarAsync();
        Assert.True(_vm.PedeNomePreset);
        // Nome repetido: a folha fica, nada guardado.
        _vm.NomePreset = "cinema";
        Assert.True(_vm.PodeConfirmar);
        await _b.Rodar(_vm.Confirmar.ExecutarAsync());
        Assert.True(_vm.Aberta);
        Assert.Equal("Nome do preset: Já existe um preset com esse nome.", _vm.Erro);
        Assert.Single(_b.Presets.Proprios);

        _vm.NomePreset = "CINEMA 2";
        await _b.Rodar(_vm.Confirmar.ExecutarAsync());

        Assert.False(_vm.Aberta);
        Assert.Equal("Salvo: 15 bandas no preset CINEMA 2.", _vm.Mensagem);
        var novo = _b.Presets.Proprios.Single(p => p.Nome == "CINEMA 2");
        Assert.Equal(novo.Id, Setups.Memoria(CenarioFilme.IdMemoria)!.IdPreset);
        Assert.Equal("Nada para salvar.", _vm.MotivoTrava);
    }

    [Fact]
    public async Task Bandas_mexidas_com_um_preset_proprio_nenhuma_escolha_vem_marcada_e_Atualizar_vale_para_ele()
    {
        await _b.Conectar();
        var rock = (await _b.Presets.CriarAsync("Rock", CenarioFilme.Bandas.Select(v => v.Indice == 0 ? v with { NoFio = "+4.0" } : v).ToArray())).Preset!;
        await _b.Rodar(_b.Presets.AplicarAsync(rock.Id)); // a FILME lembra o Rock
        await _b.Rodar(_b.Escritor.EscreverAsync(Banda, 1, -2.0m));
        await _b.Tela.EsvaziarAsync();

        await _vm.Abrir.ExecutarAsync();

        Assert.Equal(["15 bandas → preset Rock"], _vm.Linhas.Select(l => l.Titulo));
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
        _b.Simulador.Definir("020", 0, "+6.0");
        _b.Simulador.Definir("081", 0, "+4.0"); // o ganho também difere da FILME: o som do aparelho
        await _b.Conectar();
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

        Assert.Equal("Salvo: regulagem na MUSICA ORIG, 15 bandas no preset ORIG EQ.", _vm.Mensagem);
        Assert.Equal(_b.Presets.Proprios.Single(p => p.Nome == "ORIG EQ").Id,
            Setups.MemoriasDe(ConhecimentoHerdado.IdSetupCasa).Single(m => m.Nome == "MUSICA ORIG").IdPreset);
    }

    [Fact]
    public async Task Nome_de_preset_invalido_a_frase_diz_que_e_o_nome_do_preset_e_nada_e_guardado()
    {
        await _b.Conectar();
        await _b.Rodar(_b.Escritor.EscreverAsync(Banda, 4, 3.0m));
        await _b.Tela.EsvaziarAsync();
        await _vm.Abrir.ExecutarAsync();
        await _vm.EscolherCriarPreset.ExecutarAsync();

        _vm.NomePreset = "a#b";
        await _b.Rodar(_vm.Confirmar.ExecutarAsync());

        Assert.True(_vm.Aberta);
        Assert.Equal("Nome do preset: Use de 1 a 24 letras, números, espaço, _ ou -", _vm.Erro);
        Assert.Equal("a#b", _vm.NomePreset);
        Assert.Single(_b.Presets.Proprios);
    }
}
