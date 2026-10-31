using BgsControle.Nucleo;
using BgsControle.Nucleo.Setups;
using BgsControle.Simulador;

namespace BgsControle.Telas.Testes;

/// <summary>
/// Histórias 10c e 8g: um Setup sem memórias nas telas, contra o simulador. Com o Casa 2.1 da fixture
/// (sem memórias e sem presets), conectar cai na escolha do Setup, Escolher só desmuta, e o Início
/// oferece "Criar memória do som atual". O app zerado de verdade (nenhum Setup) está em
/// <see cref="AppZeradoTelasTestes"/>.
/// </summary>
public sealed class PrimeiraAberturaTelasTestes : IDisposable
{
    private readonly Bancada _b = new(usoCompartilhado: true, setups: true, filme: false);
    private readonly Telas11 _t;

    public PrimeiraAberturaTelasTestes() => _t = new Telas11(_b);

    public void Dispose()
    {
        _t.Dispose();
        Assert.Empty(_b.Tela.Erros);
    }

    private bool Mutados(bool mutado) =>
        Enumerable.Range(0, 4).All(c => _b.Simulador.Valor("100", c) == (mutado ? "1" : "0"));

    [Fact]
    public async Task Conectar_sem_memoria_cai_na_escolha_com_a_frase_sem_memoria_e_sem_lista()
    {
        await _b.Conectar();

        Assert.True(_t.Setup.Aberta);
        Assert.Equal(SetupViewModel.TituloDuvida, _t.Setup.Titulo);
        Assert.Equal("O Casa 2.1 ainda não tem memória.", _t.Setup.Frase);
        Assert.False(_t.Setup.TemDiferencas);
        Assert.Empty(_t.Setup.LinhasDiferenca);
        Assert.Equal(SetupViewModel.TextoMudo, _t.Setup.FaixaMudo);
        Assert.True(Mutados(true));
    }

    [Fact]
    public async Task Escolher_o_Casa_2_1_so_desmuta_e_o_Inicio_oferece_criar_a_memoria_e_Presets_mostra_o_vazio()
    {
        await _b.Conectar();
        Assert.Equal("Aplica a memória ativa dele, relê e desmuta. Sem memória, só desmuta.", SetupViewModel.FraseEscolher);
        var antes = _b.Simulador.Escritas.Count;

        await _t.Setup.AbrirLista.ExecutarAsync();
        await _b.Rodar(_t.Setup.Item(CasaDoDenis.NomeSetupCasa)!.Escolher.ExecutarAsync());

        Assert.False(_t.Setup.Aberta);
        Assert.True(Mutados(false));
        // Só o desmutar dos 4 canais: nenhuma outra escrita de áudio.
        var audio = _b.Simulador.Escritas.Skip(antes).Where(e => Catalogo.EscritaDeAudio(e) is not null).ToArray();
        Assert.Equal(4, audio.Count(e => e.StartsWith("*100*", StringComparison.Ordinal)));
        Assert.Equal(0, audio.Count(e => !e.StartsWith("*100*", StringComparison.Ordinal)));

        Assert.True(_t.Inicio.MostrarMemoria);
        Assert.Equal("Nenhuma memória", _t.Inicio.NomeMemoria);
        Assert.True(_t.Inicio.MostrarCriarMemoria);

        // A lista "…": sem memórias do celular, vazia (as memórias do processador não aparecem).
        Assert.Empty(_t.Botoes.LinhasCelular);
        Assert.Equal("Criar memória do som atual", InicioViewModel.TextoCriarMemoria);
        Assert.Null(_t.Inicio.NomePreset); // sem chip

        Assert.Empty(_t.Presets.Itens);
        Assert.Equal(PresetsViewModel.TextoSemProprios, _t.Presets.TextoVazio);
    }
}

/// <summary>
/// História 8g: o app nasce zerado (CAP-6, decisão do Denis de 30/10/2026). Sem nenhum arquivo, não há
/// Setup, modelo, memória nem preset, e nada do conhecimento herdado aparece em tela: a primeira conexão
/// muta e abre a escolha com a frase de nenhum Setup e sem "Escolher o Setup".
/// </summary>
public sealed class AppZeradoTelasTestes : IDisposable
{
    private readonly Bancada _b = new(usoCompartilhado: true, setups: true, zerado: true);
    private readonly Telas11 _t;
    private readonly SetupsViewModel _setups;

    public AppZeradoTelasTestes()
    {
        _t = new Telas11(_b);
        _setups = new SetupsViewModel(_b.Setups!, _b.Sessao, _t.Nav, _b.Tela, _b.Relogio);
    }

    public void Dispose()
    {
        _setups.Dispose();
        _t.Dispose();
        Assert.Empty(_b.Tela.Erros);
    }

    private bool Mutados(bool mutado) =>
        Enumerable.Range(0, 4).All(c => _b.Simulador.Valor("100", c) == (mutado ? "1" : "0"));

    /// <summary>Os textos das telas abertas, para conferir que nada do herdado aparece.</summary>
    private string Textos() => string.Join(" | ", new[]
        {
            _t.Inicio.Seletor, _t.Inicio.SeletorNome, _t.Inicio.NomeMemoria, _t.Setup.Frase, _t.Setup.Titulo, _t.Setup.Mensagem,
            _setups.Vazio, _t.Memorias.TituloCelular,
        }
        .Concat(_t.Setup.Setups.Select(s => s.Nome))
        .Concat(_setups.Linhas.SelectMany(l => new[] { l.Nome, l.Detalhe }))
        .Concat(_t.Memorias.Cartoes.Select(c => c.Nome))
        .Concat(_t.Presets.Itens.Select(i => i.Nome))
        .OfType<string>());

    private void SemNadaHerdado()
    {
        var textos = Textos();
        foreach (var herdado in new[] { "Casa 2.1", "JBL", "Protech", "SD800", "HERDADO", "SPL", "MUSICA ORIG", "FILME" })
            Assert.DoesNotContain(herdado, textos, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Primeira_abertura_nenhum_Setup_e_o_Inicio_mostra_Sem_Setup()
    {
        await _b.Setups!.Carregada;
        await _b.Modelos!.Carregada;
        await _b.Tela.EsvaziarAsync();

        Assert.Equal("Sem Setup ›", _t.Inicio.Seletor);
        Assert.Empty(_t.Setup.Setups);
        Assert.False(_t.Setup.PodeEscolher);
        Assert.Empty(_setups.Linhas);
        Assert.Equal(SetupsViewModel.TextoVazio, _setups.Vazio);
        Assert.Equal("Nenhum Setup. Conecte o aparelho e toque em \"Sem Setup ›\" no Início para criar.", _setups.Vazio);
        Assert.False(_setups.MostraLembrete);
        Assert.Empty(_t.Presets.Itens);
        Assert.Empty(_b.Modelos.Modelos);
        SemNadaHerdado();
    }

    [Fact]
    public async Task Primeira_conexao_muta_e_abre_a_escolha_com_a_frase_de_nenhum_Setup_e_sem_Escolher_o_Setup()
    {
        await _b.Conectar();

        Assert.True(_t.Setup.Aberta);
        Assert.Equal(SetupViewModel.TituloDuvida, _t.Setup.Titulo);
        Assert.Equal("Nenhum Setup cadastrado. Crie um Setup do aparelho ou siga sem Setup.", _t.Setup.Frase);
        Assert.Equal(SetupViewModel.FraseNenhumSetup, _t.Setup.Frase);
        Assert.False(_t.Setup.PodeEscolher);
        Assert.False(_t.Setup.TemDiferencas);
        Assert.Equal(SetupViewModel.TextoMudo, _t.Setup.FaixaMudo);
        Assert.True(Mutados(true));
        Assert.Equal(GerenteSetups.MsgDuvida, _b.Setups!.MotivoBloqueioIa);
        SemNadaHerdado();
    }

    [Fact]
    public async Task Criar_o_Setup_Casa_com_o_ambiente_Sala_e_a_memoria_SOM_1_o_Inicio_mostra_a_memoria_em_uso()
    {
        await _b.Conectar();

        await _t.Setup.AbrirCriar.ExecutarAsync();
        Assert.Equal(["Sala", "Carro", "Ao ar livre", "Outro"], _t.Setup.AmbientesTela.Select(a => a.Rotulo));
        Assert.DoesNotContain(_t.Setup.AmbientesTela, a => a.Escolhida); // sem padrão
        await _t.Setup.AmbientesTela.Single(a => a.Rotulo == "Sala").Escolher.ExecutarAsync();
        Assert.Equal(AmbienteSetup.Sala, _t.Setup.AmbienteNovo);
        Assert.True(_t.Setup.AmbientesTela.Single(a => a.Rotulo == "Sala").Escolhida);
        Assert.Equal(SetupViewModel.DescricaoAmbiente(AmbienteSetup.Sala, true), _t.Setup.AmbientesTela.Single(a => a.Escolhida).Descricao);
        _t.Setup.NomeNovo = "Casa";
        _t.Setup.NomeMemoriaNova = "SOM 1";
        await _b.Rodar(_t.Setup.ConfirmarCriar.ExecutarAsync());
        await _b.Tela.EsvaziarAsync();

        Assert.False(_t.Setup.Aberta);
        Assert.True(Mutados(false));
        var casa = Assert.Single(_b.Setups!.Setups);
        Assert.Equal(AmbienteSetup.Sala, casa.Ambiente);
        Assert.Equal("Setup Casa ›", _t.Inicio.Seletor);
        Assert.Equal("SOM 1", _t.Inicio.NomeMemoria);
        Assert.Equal(SomEmUso.DoCelular(_b.Setups.MemoriasDe(casa.Id).Single().Id), _b.Setups.EmUso);
        Assert.Null(_setups.Vazio);
        SemNadaHerdado();
    }

    [Fact]
    public async Task Tocar_de_novo_no_ambiente_escolhido_deixa_sem_informar_e_o_Setup_nasce_com_null()
    {
        await _b.Conectar();
        await _t.Setup.AbrirCriar.ExecutarAsync();
        await _t.Setup.AmbientesTela.Single(a => a.Rotulo == "Carro").Escolher.ExecutarAsync();
        await _t.Setup.AmbientesTela.Single(a => a.Rotulo == "Carro").Escolher.ExecutarAsync();
        Assert.Null(_t.Setup.AmbienteNovo);
        _t.Setup.NomeNovo = "Casa";

        await _b.Rodar(_t.Setup.ConfirmarCriar.ExecutarAsync());
        await _b.Tela.EsvaziarAsync();

        Assert.Null(Assert.Single(_b.Setups!.Setups).Ambiente);
    }

    [Fact]
    public async Task Reconectar_sem_escolher_continua_na_duvida_sem_escrever_nada_e_com_a_mesma_marca()
    {
        await _b.Conectar();
        var marca = _b.Setups!.Marca!;

        await _b.Sessao.DesconectarAsync();
        var inicio = _b.Simulador.Escritas.Count;
        await _b.Conectar();

        Assert.Equal(EstadoConferencia.Duvida, _b.Setups.Estado);
        Assert.Null(_b.Setups.Referencia);
        Assert.Empty(_b.Setups.Diferencas);
        Assert.Empty(_b.Simulador.Escritas.Skip(inicio).Where(e => Catalogo.EscritaDeAudio(e) is not null));
        Assert.True(Mutados(true));
        Assert.Equal(marca, _b.Setups.Marca);
        Assert.Equal(marca.MuteAnterior, _b.Setups.Marca!.MuteAnterior);
        Assert.Equal(GerenteSetups.MsgDuvida, _b.Setups.MotivoBloqueioIa);
        Assert.True(_t.Setup.Aberta);
        Assert.Equal(SetupViewModel.FraseNenhumSetup, _t.Setup.Frase);
    }

    [Fact]
    public async Task Seguir_sem_Setup_e_tocar_em_Sem_Setup_abre_a_troca_sem_Escolher_e_Criar_Setup_desmuta()
    {
        await _b.Conectar();
        await _b.Rodar(_t.Setup.SeguirSemSetup.ExecutarAsync());
        await _b.Tela.EsvaziarAsync();
        Assert.True(Mutados(false));

        await _b.Rodar(_t.Setup.Trocar.ExecutarAsync());
        await _b.Tela.EsvaziarAsync();

        Assert.True(_t.Setup.Aberta);
        Assert.Equal(SetupViewModel.TituloTroca, _t.Setup.Titulo);
        Assert.Equal(SetupViewModel.FraseNenhumSetup, _t.Setup.Frase);
        Assert.False(_t.Setup.PodeEscolher);
        Assert.True(Mutados(true));

        await _t.Setup.AbrirCriar.ExecutarAsync();
        _t.Setup.NomeNovo = "Casa";
        await _b.Rodar(_t.Setup.ConfirmarCriar.ExecutarAsync());
        await _b.Tela.EsvaziarAsync();

        var casa = Assert.Single(_b.Setups!.Setups);
        Assert.Equal(casa.Id, _b.Setups.SetupEmUso?.Id);
        Assert.False(_t.Setup.Aberta);
        Assert.True(Mutados(false));
        Assert.Equal("Setup Casa ›", _t.Inicio.Seletor);
    }

    [Fact]
    public async Task Seguir_sem_Setup_desmuta_e_a_proxima_conexao_volta_a_escolha()
    {
        await _b.Conectar();

        await _b.Rodar(_t.Setup.SeguirSemSetup.ExecutarAsync());
        await _b.Tela.EsvaziarAsync();

        Assert.False(_t.Setup.Aberta);
        Assert.True(Mutados(false));
        Assert.Equal("Sem Setup ›", _t.Inicio.Seletor);

        await _b.Sessao.DesconectarAsync();
        await _b.Conectar();

        Assert.True(_t.Setup.Aberta);
        Assert.Equal(SetupViewModel.FraseNenhumSetup, _t.Setup.Frase);
        Assert.True(Mutados(true));
    }
}
