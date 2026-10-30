using BgsControle.Nucleo;
using BgsControle.Nucleo.Escrita;
using BgsControle.Nucleo.Seguranca;
using BgsControle.Nucleo.Setups;

namespace BgsControle.Telas.Testes;

/// <summary>
/// As telas da história 10 montadas como no app (os mesmos singletons), sobre a <see cref="Bancada"/>
/// com os Setups e com a navegação falsa.
/// </summary>
internal sealed class Telas10 : IDisposable
{
    private readonly Dictionary<int, AjusteSaidaViewModel> _ajustes = [];

    public Telas10(Bancada b)
    {
        B = b;
        var setups = b.Setups!;
        Conexao = new ConexaoViewModel(b.Sessao, b.Log, b.Leitura, b.Tela, b.ArquivosUso, new PreparoFalso(), b.Relogio);
        Salvar = b.CriarSalvarViewModel();
        Setup = b.CriarSetupViewModel();
        Memorias = b.CriarMemoriasViewModel(Salvar);
        Link = b.CriarLinkViewModel();
        Botoes = new BotoesMemoriaViewModel(setups, Memorias, b.Sessao, b.Tela, b.Relogio);
        Inicio = new InicioViewModel(setups, b.Sessao, b.Estado, b.Link, Conexao, Setup, Memorias, Botoes, Nav, b.Tela, b.Relogio);
        Mesa = new MesaViewModel(setups, b.Sessao, b.Estado, b.Link, b.Fabrica, Botoes, Nav, b.Tela, b.Relogio);
        Audio = new AudioViewModel(b.Fabrica, setups, b.Sessao, b.Estado, b.Link, Link, Salvar, Nav, b.Tela, b.Relogio, b.Chaves, b.Gerador);
        Casca = new CascaViewModel(b.Sessao, Conexao, Setup, Memorias, Inicio, Mesa, Audio, Salvar, b.Fabrica, Nav, b.Tela, b.Relogio);
        Setups = new SetupsViewModel(setups, b.Sessao, Nav, b.Tela, b.Relogio);
        Configuracoes = new ConfiguracoesViewModel(Nav, b.Tela, b.Relogio);
    }

    public Bancada B { get; }
    public NavegacaoFalsa Nav { get; } = new();
    public ConexaoViewModel Conexao { get; }
    public SalvarViewModel Salvar { get; }
    public SetupViewModel Setup { get; }
    public MemoriasViewModel Memorias { get; }
    public LinkViewModel Link { get; }
    public BotoesMemoriaViewModel Botoes { get; }
    public InicioViewModel Inicio { get; }
    public MesaViewModel Mesa { get; }
    public AudioViewModel Audio { get; }
    public CascaViewModel Casca { get; }
    public SetupsViewModel Setups { get; }
    public ConfiguracoesViewModel Configuracoes { get; }

    public AjusteSaidaViewModel Ajuste(int s)
    {
        if (!_ajustes.TryGetValue(s, out var a))
        {
            a = new AjusteSaidaViewModel(s, B.Fabrica, B.Setups!, B.Sessao, B.Chaves, B.Estado, B.Link, Salvar, Nav, B.Tela, B.Relogio);
            _ajustes[s] = a;
        }
        return a;
    }

    public void Dispose()
    {
        foreach (var a in _ajustes.Values)
            a.Dispose();
        Casca.Dispose();
        Audio.Dispose();
        Mesa.Dispose();
        Inicio.Dispose();
        Botoes.Dispose();
        Setups.Dispose();
        Link.Dispose();
        Memorias.Dispose();
        Setup.Dispose();
        Salvar.Dispose();
    }
}

/// <summary>A história 10 contra o processador simulado: Início, botões de memória, Mesa, Áudio, telas de função, Ajuste, Setups e a casca.</summary>
public sealed class TelasParte1Testes : IDisposable
{
    // Os canais mestres das saídas do Casa 2.1: JBL L (CH1), JBL R (CH2) e Sub (CH3 + CH4, com o link).
    private const int L = 0, R = 1, Sub = 2;

    private readonly Bancada _b = new(setups: true);
    private readonly Telas10 _t;

    public TelasParte1Testes() => _t = new Telas10(_b);

    public void Dispose()
    {
        _t.Dispose();
        _b.ConferirSoAudio();
        Assert.Empty(_b.Tela.Erros);
    }

    private GerenteSetups Setups => _b.Setups!;

    private bool Mutados(bool mutado) =>
        Enumerable.Range(0, 4).All(c => _b.Simulador.Valor("100", c) == (mutado ? "1" : "0"));

    private async Task CriarMemoria(string nome)
    {
        await _t.Memorias.AbrirCriar.ExecutarAsync();
        Assert.True(_t.Memorias.CriarAberto, _t.Memorias.Mensagem);
        _t.Memorias.NomeNova = nome;
        await _b.Rodar(_t.Memorias.ConfirmarCriar.ExecutarAsync());
        Assert.False(_t.Memorias.CriarAberto, _t.Memorias.ErroNova);
    }

    // ----------------------------------------------------------------- Início e casca

    [Fact]
    public async Task Aberto_desconectado_o_Inicio_mostra_o_cartao_de_conexao_e_as_saidas_com_traco()
    {
        await _b.Tela.EsvaziarAsync();

        Assert.True(_t.Inicio.MostrarConexao);
        Assert.False(_t.Inicio.MostrarMemoria);
        Assert.Equal("Desconectado", _t.Inicio.EstadoConexao);
        Assert.Equal("Desconectado. Ligue o som e fique perto dele.", _t.Inicio.FraseConexao);
        Assert.All(_t.Inicio.CartoesSaida, c =>
        {
            Assert.Equal("—", c.Ganho);
            Assert.False(c.Lido);
            Assert.Contains("—", c.Resumo1);
        });
        Assert.Equal("Desconectado", _t.Casca.TextoStatus);
        Assert.True(_t.Casca.PontoDesconectado);
        Assert.Equal("—", _t.Mesa.NomeMemoria);
        Assert.Equal(ControleParametroViewModel.MsgSemConexao, _t.Mesa.Canal(Sub)!.Ganho.MotivoTrava);
    }

    [Fact]
    public async Task Conectado_na_FILME_o_Inicio_mostra_o_Setup_a_memoria_o_preset_e_os_ganhos_lidos()
    {
        await _b.Conectar();
        await _b.Tela.EsvaziarAsync();

        Assert.Equal("Conectado · R003", _t.Casca.TextoStatus);
        Assert.True(_t.Casca.PontoConectado);
        Assert.False(_t.Inicio.MostrarConexao);
        Assert.Equal("Setup Casa 2.1 ›", _t.Inicio.Seletor);
        Assert.Equal("Setup", _t.Inicio.SeletorRotulo);
        Assert.Equal("Casa 2.1", _t.Inicio.SeletorNome);
        Assert.True(_t.Inicio.MostrarMemoria);
        Assert.Equal("FILME", _t.Inicio.NomeMemoria);
        Assert.Equal("Preset: Cinema", _t.Inicio.TextoChip);
        Assert.Null(_t.Inicio.SomDiferente);
        Assert.Equal("+10,0", _t.Inicio.Cartao(L)!.Ganho);
        Assert.Equal("+7,0", _t.Inicio.Cartao(R)!.Ganho);
        Assert.Equal("+6,0", _t.Inicio.Cartao(Sub)!.Ganho);
        Assert.All(_t.Inicio.CartoesSaida, c =>
        {
            Assert.True(c.Lido);
            Assert.DoesNotContain("—", c.Resumo1 + c.Resumo2 + c.Subtitulo);
        });
        Assert.Equal(["JBL L", "JBL R", "Sub"], _t.Inicio.CartoesSaida.Select(c => c.Nome));
        Assert.Equal([0, 1, 2], _t.Inicio.CartoesSaida.Select(c => c.Canal));
        Assert.StartsWith("CH1 · rota ", _t.Inicio.Cartao(L)!.Subtitulo);
        Assert.StartsWith("CH3 + CH4 · rota ", _t.Inicio.Cartao(Sub)!.Subtitulo);
        var filme = _t.Botoes.Botao("FILME")!;
        Assert.True(filme.EmUso);
        Assert.Equal(0, filme.Vaga);
        Assert.False(_t.Botoes.Botoes[1].Visivel);
        Assert.Equal("FILME", _t.Mesa.NomeMemoria);
        Assert.Null(_t.Nav.PorCima is { Setup: true } ? "dúvida" : null);
    }

    [Fact]
    public async Task Cartao_de_saida_e_canal_da_mesa_pedem_o_Ajuste_da_saida()
    {
        await _b.Conectar();

        await _t.Inicio.Cartao(R)!.Ajustar.ExecutarAsync();
        await _t.Mesa.Canal(Sub)!.Ajustar.ExecutarAsync();
        await _t.Casca.AbrirConfiguracoes.ExecutarAsync();
        await _t.Casca.TocarPilula.ExecutarAsync();

        Assert.Equal(["ajuste:CH2", "ajuste:CH3", "configuracoes", "conexao"], _t.Nav.Pedidos.Where(p => !p.StartsWith("por-cima")));
    }

    [Fact]
    public async Task Senha_errada_o_cartao_diz_a_tentativa_e_as_caixas_viram_pontos()
    {
        _b.Cofre.Senha = null;
        await _b.Chaves.Carregada;
        await Setups.Carregada;
        await _b.Sessao.ConectarAsync();
        await _b.Tela.EsvaziarAsync();
        Assert.Equal("Senha", _t.Inicio.EstadoConexao);
        Assert.Equal("Conectando · senha", _t.Casca.TextoStatus);
        Assert.True(_t.Casca.PontoEsperando);

        _t.Conexao.SenhaDigitada = "ZZ";
        Assert.Equal(["•", "•", "", ""], _t.Conexao.Caixas.Select(c => c.Texto));
        Assert.True(_t.Conexao.Caixas[2].Atual);
        _t.Conexao.SenhaDigitada = "ZZ99";
        await _t.Conexao.EnviarSenha.ExecutarAsync();
        await Apoio.Esperar(() => _b.Sessao.Estado == EstadoSessao.PedindoSenha && _b.Sessao.Mensagem is not null);
        await _b.Tela.EsvaziarAsync();

        Assert.Equal("Senha errada. Digite de novo (tentativa 2 de 3).", _t.Inicio.FraseConexao);
        Assert.All(_t.Conexao.Caixas, c => Assert.Equal("", c.Texto));
        Assert.DoesNotContain(_b.Simulador.Escritas, e => Catalogo.EscritaDeAudio(e) is not null);
    }

    [Fact]
    public async Task Firmware_diferente_a_pilula_diz_so_leitura_o_Inicio_avisa_e_os_controles_travam()
    {
        _b.Simulador.Firmware = "R004";
        await _b.Conectar();
        await _b.Tela.EsvaziarAsync();

        Assert.Equal("Só leitura · firmware R004", _t.Casca.TextoStatus);
        Assert.Equal("Firmware diferente do R003. O app só lê; nada é enviado.", _t.Inicio.AvisoFixo);
        Assert.Equal(ControleParametroViewModel.MsgSoLeitura, _t.Mesa.Canal(L)!.Ganho.MotivoTrava);
        Assert.Equal(ControleParametroViewModel.MsgSoLeitura, _t.Audio.Tela(FuncaoAudio.Fase).Controle("060")!.MotivoTrava);
    }

    [Fact]
    public async Task So_o_carater_diferente_mostra_som_do_aparelho_com_Reaplicar_que_volta_a_FILME()
    {
        _b.Simulador.Definir("081", 0, "+3.0");
        await _b.Conectar();
        await _b.Tela.EsvaziarAsync();

        Assert.Equal("Som do aparelho", _t.Inicio.NomeMemoria);
        Assert.Equal("O app não sabe qual memória está tocando.", _t.Inicio.LinhaMemoria);
        Assert.Null(_t.Inicio.TextoChip);
        Assert.Equal("Som diferente da FILME", _t.Inicio.SomDiferente);
        Assert.All(_t.Botoes.Botoes, b => Assert.False(b.EmUso));

        await _b.Rodar(_t.Inicio.Reaplicar.ExecutarAsync());

        Assert.Equal("FILME", _t.Inicio.NomeMemoria);
        Assert.Null(_t.Inicio.SomDiferente);
        Assert.Equal("+10.0", _b.Simulador.Valor("081", 0));
    }

    [Fact]
    public async Task Duvida_ao_conectar_a_casca_poe_a_escolha_por_cima_e_escolher_tira()
    {
        _b.Simulador.Definir("050", 2, "0.0");
        await _b.Conectar();
        await _b.Tela.EsvaziarAsync();

        Assert.Equal((true, false), _t.Nav.PorCima);
        Assert.True(_t.Casca.EscolhaSetupPorCima);
        Assert.False(_t.Setup.PodeDesistir); // na dúvida não há ‹ (nem o Voltar do Android)
        var linha = Assert.Single(_t.Setup.LinhasDiferenca);
        Assert.Equal("Atraso · Sub", linha.Rotulo);
        Assert.Equal("80 cm na FILME, 0 cm no aparelho", linha.Detalhe);
        Assert.Equal(Sub, linha.Canal);
        await _b.Rodar(_t.Setup.Desistir.ExecutarAsync());
        Assert.True(_t.Setup.Aberta);

        await _b.Rodar(_t.Setup.Item("Casa 2.1")!.Escolher.ExecutarAsync());
        await _b.Tela.EsvaziarAsync();

        Assert.Equal((false, false), _t.Nav.PorCima);
        Assert.Equal("Setup Casa 2.1 ›", _t.Inicio.Seletor);
        Assert.True(Mutados(false));
    }

    [Fact]
    public async Task Tocar_no_seletor_abre_Trocar_de_Setup_e_desistir_desmuta()
    {
        await _b.Conectar();

        await _b.Rodar(_t.Inicio.TrocarSetup.ExecutarAsync());
        await _b.Tela.EsvaziarAsync();
        Assert.Equal((true, false), _t.Nav.PorCima);
        Assert.Equal("Trocar de Setup", _t.Setup.Titulo);
        Assert.True(_t.Setup.PodeDesistir);
        Assert.False(_t.Setup.TemDiferencas);
        Assert.True(Mutados(true));

        await _b.Rodar(_t.Setup.Desistir.ExecutarAsync());
        await _b.Tela.EsvaziarAsync();

        Assert.Equal((false, false), _t.Nav.PorCima);
        Assert.True(Mutados(false));
        Assert.Equal("FILME", _t.Inicio.NomeMemoria);
    }

    [Fact]
    public async Task Queda_no_meio_de_aplicar_a_casca_poe_a_folha_pela_metade_e_Terminar_tira()
    {
        await _b.Conectar();
        await _b.Rodar(_b.Escritor.EscreverAsync(Catalogo.ParametroDe("081")!, 0, 4.0m));
        await CriarMemoria("ROCK");
        _b.Simulador.CairDepoisDaEscrita = _b.Simulador.Escritas.Count + 1;

        await _b.Rodar(_t.Botoes.Botao("FILME")!.Tocar.ExecutarAsync());
        await _b.Conectar();
        await Apoio.Esperar(() => _b.Copia.PelaMetade is not null);
        await _b.Tela.EsvaziarAsync();

        Assert.Equal((false, true), _t.Nav.PorCima);
        Assert.Equal("Aplicar a memória FILME caiu no meio.", _t.Memorias.TextoPelaMetade);

        await _b.Rodar(_t.Memorias.Terminar.ExecutarAsync());
        await _b.Tela.EsvaziarAsync();

        Assert.Equal((false, false), _t.Nav.PorCima);
        Assert.Equal("FILME", _t.Inicio.NomeMemoria);
    }

    // ----------------------------------------------------------------- botões de memória e lista

    [Fact]
    public async Task Tres_memorias_tocar_na_segunda_aplica_sem_confirmar_e_os_botoes_nao_mudam_de_lugar()
    {
        await _b.Conectar();
        await CriarMemoria("MUSICA ORIG"); // ganho da saída 1: +10
        await _b.Rodar(_b.Escritor.EscreverAsync(Catalogo.ParametroDe("081")!, 0, 4.0m));
        await CriarMemoria("ROCK"); // ganho da saída 1: +4
        await _b.Tela.EsvaziarAsync();
        Assert.Equal(["FILME", "MUSICA ORIG", "ROCK"], _t.Botoes.Botoes.Select(b => b.Nome));
        Assert.True(_t.Botoes.Botoes[2].EmUso);

        await _b.Rodar(_t.Botoes.Botoes[1].Tocar.ExecutarAsync());
        await _b.Tela.EsvaziarAsync();

        Assert.Equal(["FILME", "MUSICA ORIG", "ROCK"], _t.Botoes.Botoes.Select(b => b.Nome));
        Assert.Equal([false, true, false], _t.Botoes.Botoes.Select(b => b.EmUso));
        Assert.Equal("+10.0", _b.Simulador.Valor("081", 0));
        Assert.Equal("MUSICA ORIG", _t.Inicio.NomeMemoria);
        Assert.Equal("Memória MUSICA ORIG aplicada.", _t.Botoes.Mensagem);
        Assert.Null(_t.Botoes.Aplicando);
    }

    [Fact]
    public async Task Com_a_chave_desligada_o_botao_diz_a_trava_e_nada_e_aplicado()
    {
        await _b.Conectar();
        await _b.Rodar(_b.Escritor.EscreverAsync(Catalogo.ParametroDe("081")!, 0, 4.0m));
        await CriarMemoria("ROCK");
        await _b.Chaves.DefinirAsync(Chave.AplicarMemoriaCelular, false, OrigemComando.Tela);
        await _b.Tela.EsvaziarAsync();
        var antes = _b.Simulador.Escritas.Count;

        await _t.Botoes.Botao("FILME")!.Tocar.ExecutarAsync();

        Assert.Equal("Chave desligada", _t.Botoes.Mensagem);
        Assert.Equal(antes, _b.Simulador.Escritas.Count);
        Assert.Equal("ROCK", _t.Inicio.NomeMemoria);
    }

    [Fact]
    public async Task A_lista_do_mais_tem_so_as_memorias_do_Setup_e_tocar_fecha()
    {
        await _b.Conectar();
        await _t.Botoes.AbrirLista.ExecutarAsync();
        Assert.True(_t.Botoes.ListaAberta);

        var filme = Assert.Single(_t.Botoes.LinhasCelular);
        Assert.Equal("FILME", filme.Nome);
        Assert.Equal("Preset: Cinema", filme.Detalhe);
        Assert.True(filme.EmUso);
        // As memórias do processador não aparecem (decisão do Denis, 29/10/2026).
        Assert.Null(typeof(BotoesMemoriaViewModel).GetProperty("LinhasAparelho"));
        var antes = _b.Simulador.Escritas.Count;

        await _b.Rodar(filme.Tocar.ExecutarAsync()); // a FILME já está em uso: só fecha
        await _b.Tela.EsvaziarAsync();

        Assert.False(_t.Botoes.ListaAberta);
        Assert.Equal("FILME", _t.Inicio.NomeMemoria);
        Assert.Equal(antes, _b.Simulador.Escritas.Count);
    }

    [Fact]
    public async Task Sem_Setup_as_memorias_do_celular_ficam_travadas_com_a_frase()
    {
        _b.Simulador.Definir("050", 2, "0.0");
        await _b.Conectar();
        await _b.Rodar(_t.Setup.SeguirSemSetup.ExecutarAsync());
        await _b.Tela.EsvaziarAsync();

        Assert.Equal("Sem Setup ›", _t.Inicio.Seletor);
        Assert.Null(_t.Inicio.SeletorRotulo);
        Assert.Equal("Som do aparelho", _t.Inicio.NomeMemoria);
        Assert.Equal(GerenteSetups.MsgSemSetup, _t.Botoes.MotivoTravaCelular);
        Assert.True(_t.Botoes.Botao("FILME")!.Travado);

        await _t.Botoes.AbrirLista.ExecutarAsync();
        Assert.All(_t.Botoes.LinhasCelular, l => Assert.True(l.Travada));
        await _t.Botoes.LinhasCelular[0].Tocar.ExecutarAsync();

        Assert.True(_t.Botoes.ListaAberta);
        Assert.Equal(GerenteSetups.MsgSemSetup, _t.Botoes.Mensagem);
    }

    // ----------------------------------------------------------------- Mesa

    [Fact]
    public async Task Na_Mesa_o_fader_do_Sub_e_o_MASTER_mudam_o_som_e_mostram_o_relido()
    {
        await _b.Conectar();
        var sub = _t.Mesa.Canal(Sub)!.Ganho;
        Assert.Equal("+6,0 dB", sub.TextoValor);
        Assert.Equal(-45, sub.Minimo);
        Assert.Equal(15, sub.Maximo);

        await _t.Mesa.Canal(Sub)!.Ganho.ArrastarPara.ExecutarAsync(2.0);
        await _b.Rodar(sub.SoltarFader.ExecutarAsync());
        await _t.Mesa.Master.ArrastarPara.ExecutarAsync(-10.04);
        await _b.Rodar(_t.Mesa.Master.SoltarFader.ExecutarAsync());

        Assert.Equal("+2.0", _b.Simulador.Valor("081", 2));
        Assert.Equal("+2,0 dB", sub.TextoValor);
        Assert.Equal("-10.0", _b.Simulador.Valor("080"));
        Assert.Equal("−10,0 dB", _t.Mesa.Master.TextoValor);
        Assert.Equal("JBL L", _t.Mesa.Canais[0].Saida.Nome);
        Assert.Equal(Sub, sub.Canal);
        Assert.Null(_t.Mesa.Master.Canal);
    }

    // ----------------------------------------------------------------- Áudio e telas de função

    [Fact]
    public async Task A_grade_Audio_tem_os_10_blocos_na_ordem_da_Taramps_com_o_Gerador_travado_pela_chave()
    {
        await _b.Conectar();
        await _b.Tela.EsvaziarAsync();

        Assert.Equal(
            ["Roteamento", "EQ 15 bandas", "EQ de entrada", "Crossover", "Alinhamento", "Fase", "Limitador", "Nível das saídas", "EQ de saída", "Gerador"],
            _t.Audio.Blocos.Select(b => b.Titulo));
        var gerador = _t.Audio.Bloco(FuncaoAudio.Gerador);
        Assert.True(gerador.Travado);
        Assert.Equal("Chave desligada", gerador.Trava); // a chave do Gerador vem desligada
        Assert.Equal("Chave desligada", gerador.TextoResumo);
        Assert.Equal("+10,0 · +7,0 · +6,0 dB", _t.Audio.Bloco(FuncaoAudio.NivelSaidas).TextoResumo);
        Assert.Equal("Preset Cinema", _t.Audio.Bloco(FuncaoAudio.Eq15Bandas).TextoResumo);
        Assert.Equal(R, _t.Audio.Bloco(FuncaoAudio.NivelSaidas).Resumo[2].Canal);

        await gerador.Abrir.ExecutarAsync(); // travado pela chave: abre Chaves de acesso
        await _t.Audio.Bloco(FuncaoAudio.Crossover).Abrir.ExecutarAsync();

        Assert.Equal(["funcao:Crossover"], _t.Nav.Pedidos.Where(p => p.StartsWith("funcao")));
        Assert.Contains("chaves", _t.Nav.Pedidos);
    }

    [Fact]
    public async Task Cada_funcao_mostra_a_vista_e_em_Avancado_o_que_a_tabela_do_EXPERIENCE_pede()
    {
        await _b.Conectar();

        string[] Codigos(IEnumerable<ItemControleViewModel> itens) => itens.Select(i => i.Controle.Parametro.Codigo).ToArray();
        FuncaoViewModel F(FuncaoAudio f) => _t.Audio.Tela(f);

        Assert.Equal(["040", "042", "041", "043"], Codigos(F(FuncaoAudio.Crossover).Vista));
        Assert.True(F(FuncaoAudio.Crossover).TemAvancado); // o link
        Assert.Equal(["071"], Codigos(F(FuncaoAudio.Limitador).Vista));
        Assert.Equal(["072", "073", "070"], Codigos(F(FuncaoAudio.Limitador).Avancado));
        Assert.Equal(["081"], Codigos(F(FuncaoAudio.NivelSaidas).Vista));
        Assert.Equal(["100"], Codigos(F(FuncaoAudio.NivelSaidas).Avancado));
        Assert.Equal(15, F(FuncaoAudio.Eq15Bandas).Vista.Count);
        Assert.False(F(FuncaoAudio.Eq15Bandas).TemAbas);
        Assert.Equal(["220"], Codigos(F(FuncaoAudio.Eq15Bandas).Avancado));
        Assert.Equal(["031", "030", "032"], Codigos(F(FuncaoAudio.EqEntrada).Vista));
        Assert.Equal(["010"], Codigos(F(FuncaoAudio.Roteamento).Vista));
        Assert.Equal(["050"], Codigos(F(FuncaoAudio.Alinhamento).Vista));
        Assert.Equal(["060"], Codigos(F(FuncaoAudio.Fase).Vista));
        Assert.Equal(["090", "091", "092"], Codigos(F(FuncaoAudio.EqSaida).Vista));
        Assert.True(F(FuncaoAudio.Fase).Controle("060")!.EhPar);
        Assert.True(F(FuncaoAudio.Roteamento).Controle("010")!.EhLista);

        var nivel = F(FuncaoAudio.NivelSaidas);
        nivel.EscolherSaida(Sub);
        Assert.Equal(2, nivel.Controle("081")!.Indice);
        Assert.True(nivel.Abas[2].Escolhida);
        Assert.False(nivel.Abas[0].Escolhida);
        Assert.Equal("+6,0 dB", nivel.Abas[2].Resumo);
    }

    [Fact]
    public async Task Na_tela_de_funcao_mexer_vai_ao_aparelho_e_mostra_o_relido()
    {
        await _b.Conectar();
        var atraso = _t.Audio.Tela(FuncaoAudio.Alinhamento);
        atraso.EscolherSaida(R);
        var c = atraso.Controle("050")!;
        var antes = c.ValorMostrado!.Value;

        await c.Aumentar.ExecutarAsync();
        await _b.Rodar(c.UltimaEscrita);

        Assert.Equal(antes + 0.5m, _b.Estado.Valor("050", 1)!.Numero);
        Assert.Equal((antes + 0.5m).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture), _b.Simulador.Valor("050", 1));
        Assert.Equal(FormatoValor.ComUnidade(c.Parametro, antes + 0.5m), c.TextoValor);
    }

    [Fact]
    public async Task Filtro_em_OFF_a_frequencia_some_e_volta_com_o_tipo()
    {
        await _b.Conectar();
        var x = _t.Audio.Tela(FuncaoAudio.Crossover);
        var freq = x.Vista[0];
        var tipo = x.Vista[1].Controle;
        Assert.True(tipo.EhLista);
        var escolhido = tipo.OpcaoEscolhida!;

        await _b.Rodar(tipo.EscolherOpcao.ExecutarAsync(tipo.Opcoes.First(o => o.Rotulo == "OFF")));
        await _b.Tela.EsvaziarAsync();
        Assert.False(freq.Visivel);
        Assert.Equal("Desligado", tipo.DescricaoOpcao);
        Assert.False(tipo.Expandido);

        await _b.Rodar(tipo.EscolherOpcao.ExecutarAsync(tipo.Opcoes.First(o => o.Rotulo == "BT24")));
        await _b.Tela.EsvaziarAsync();
        Assert.True(freq.Visivel);
        Assert.Equal("Butterworth · 24 dB por oitava", tipo.DescricaoOpcao);
        Assert.NotNull(escolhido);
    }

    [Fact]
    public async Task Crossover_no_Avancado_mostra_o_Link_3_4_so_de_leitura_e_mexer_no_Sub_muda_o_CH3_e_o_CH4()
    {
        await _b.Conectar();
        var x = _t.Audio.Tela(FuncaoAudio.Crossover);
        await x.AlternarAvancado.ExecutarAsync();
        await _b.Tela.EsvaziarAsync();
        Assert.True(x.AvancadoAberto);
        Assert.True(x.MostrarLink);

        // O link sai das ligações do Casa 2.1: uma linha só de leitura, sem Lig./Desl.
        var linha = Assert.Single(x.Link.Linhas);
        Assert.Equal("CH3 e CH4 ligados: alimentam o mesmo bridge", linha.Frase);
        Assert.Equal(["JBL L", "JBL R", "Sub"], x.Abas.Select(a => a.Nome));

        x.EscolherSaida(Sub);
        Assert.Equal("Link 3–4 ligado: mexer em Sub muda o CH3 e o CH4.", x.AvisoLink);
        var pa = x.Controle("040")!;
        pa.Expandir();
        pa.AbrirCampo();
        pa.TextoCampo = "45";
        await _b.Rodar(pa.Confirmar.ExecutarAsync());
        await _b.Rodar(pa.UltimaEscrita);
        await _b.Andar(TimeSpan.FromMilliseconds(500));

        Assert.Equal("45", _b.Simulador.Valor("040", 2));
        Assert.Equal("45", _b.Simulador.Valor("040", 3));
    }

    // ----------------------------------------------------------------- Ajuste da saída

    [Fact]
    public async Task Ajuste_da_JBL_R_o_ganho_e_o_valor_final_e_o_Salvar_leva_para_a_memoria()
    {
        await _b.Conectar();
        var r = _t.Ajuste(R);
        Assert.Equal("JBL R", r.Titulo);
        Assert.Equal("CH2 · rota " + TextosParametro.Rota(_b.Estado, R) + " · FILME", r.Subtitulo);
        Assert.Equal("+7,0 dB", r.Ganho.TextoValor);
        Assert.Equal("Nada para salvar.", r.Salvar.MotivoTrava);

        r.Ganho.AbrirCampo();
        r.Ganho.TextoCampo = "6";
        await _b.Rodar(r.Ganho.Confirmar.ExecutarAsync());
        await _b.Rodar(r.Ganho.UltimaEscrita);

        Assert.Equal("+6.0", _b.Simulador.Valor("081", 1));
        Assert.Equal("+6,0 dB", r.Ganho.TextoValor);

        await r.Salvar.Abrir.ExecutarAsync();
        var linha = Assert.Single(r.Salvar.Linhas);
        Assert.Equal("Regulagem → memória FILME", linha.Titulo);
        Assert.Contains("Ganho · JBL R: +7,0 dB → +6,0 dB", linha.Mudancas);
        await r.Salvar.Cancelar.ExecutarAsync();
    }

    [Fact]
    public async Task Ajuste_do_Sub_tem_o_par_e_o_Avancado_tem_o_resto_sem_equilibrio()
    {
        await _b.Conectar();
        var sub = _t.Ajuste(Sub);

        Assert.Equal("Sub", sub.Titulo);
        Assert.StartsWith("CH3 + CH4 · rota ", sub.Subtitulo);
        Assert.Equal(2, sub.Ganho.Indice);
        Assert.True(sub.Ganho.Expandido);
        Assert.Equal(["040", "042", "041", "043", "071"], sub.Vista.Select(i => i.Controle.Parametro.Codigo));
        Assert.Equal(["050", "060"], sub.AvancadoInicio.Select(i => i.Controle.Parametro.Codigo));
        Assert.Equal(["090", "091", "092", "072", "073", "070", "100"], sub.AvancadoFim.Select(i => i.Controle.Parametro.Codigo));
    }

    [Fact]
    public async Task No_Ajuste_mexer_no_atraso_vai_ao_aparelho_e_o_Salvar_destrava()
    {
        await _b.Conectar();
        var l = _t.Ajuste(L);
        var atraso = l.Controle("050")!;
        var antes = atraso.ValorMostrado!.Value;

        await atraso.Aumentar.ExecutarAsync();
        await _b.Rodar(atraso.UltimaEscrita);
        await _b.Tela.EsvaziarAsync();

        Assert.Equal(antes + 0.5m, _b.Estado.Valor("050", 0)!.Numero);
        Assert.Null(l.Salvar.MotivoTrava);
        Assert.True(l.Salvar.PodeSalvar);
    }

    // ----------------------------------------------------------------- Configurações › Setups

    [Fact]
    public async Task Configuracoes_abre_Setups_e_Memorias()
    {
        await _b.Setups!.Carregada; // o setups.json lido: a casca já se acertou antes do toque
        await _b.Tela.EsvaziarAsync();
        var antes = _t.Nav.Pedidos.Count;

        await _t.Configuracoes.AbrirSetups.ExecutarAsync();
        await _t.Configuracoes.AbrirMemorias.ExecutarAsync();

        Assert.Equal(["setups", "memorias"], _t.Nav.Pedidos.Skip(antes));
    }

    [Fact]
    public async Task Setups_lista_com_o_selo_e_as_travas_e_apaga_o_que_nao_esta_em_uso()
    {
        await _b.Conectar();
        await _b.Tela.EsvaziarAsync();
        var casa = Assert.Single(_t.Setups.Linhas);
        Assert.Equal("Casa 2.1", casa.Nome);
        Assert.Equal("HERDADO", casa.Selo);
        Assert.True(casa.EmUso);
        Assert.Equal("Em uso · 1 memória", casa.Detalhe);

        await casa.Apagar.ExecutarAsync();
        Assert.Equal("Este Setup está em uso.", _t.Setups.Mensagem);
        Assert.False(_t.Setups.ApagarAberto);

        // Cria o Carro pela troca e volta ao Casa 2.1.
        await _b.Rodar(_t.Inicio.TrocarSetup.ExecutarAsync());
        await _t.Setup.AbrirCriar.ExecutarAsync();
        _t.Setup.NomeNovo = "Carro";
        await _b.Rodar(_t.Setup.ConfirmarCriar.ExecutarAsync());
        await _b.Tela.EsvaziarAsync();
        Assert.Equal("Carro", _t.Inicio.NomeMemoria); // o som do aparelho vira a primeira memória do Carro
        await _b.Rodar(_t.Inicio.TrocarSetup.ExecutarAsync());
        await _b.Rodar(_t.Setup.Item("Casa 2.1")!.Escolher.ExecutarAsync());
        await _b.Tela.EsvaziarAsync();

        var carro = _t.Setups.Linha("Carro")!;
        Assert.False(carro.EmUso);
        Assert.Equal("1 memória", carro.Detalhe);
        await _t.Setups.Linha("Casa 2.1")!.Apagar.ExecutarAsync();
        Assert.Equal("Este Setup está em uso.", _t.Setups.Mensagem);

        await carro.Apagar.ExecutarAsync();
        Assert.True(_t.Setups.ApagarAberto);
        Assert.Equal("Apagar o Setup Carro?", _t.Setups.ApagarTitulo);
        Assert.Equal("O Setup, as memórias e as playlists dele somem.", _t.Setups.ApagarAvisoTexto);
        await _b.Rodar(_t.Setups.ConfirmarApagar.ExecutarAsync());

        Assert.False(_t.Setups.ApagarAberto);
        Assert.Equal("Setup Carro apagado.", _t.Setups.Mensagem);
        Assert.Null(_t.Setups.Linha("Carro"));
    }

    [Fact]
    public async Task Setups_renomear_vale_no_check_e_o_Inicio_mostra_o_nome_novo()
    {
        await _b.Conectar();
        await _b.Tela.EsvaziarAsync();

        await _t.Setups.Linhas[0].Renomear.ExecutarAsync();
        Assert.Equal("Renomear o Casa 2.1", _t.Setups.RenomearTitulo);
        _t.Setups.NomeRenomear = "Sala#";
        await _b.Rodar(_t.Setups.ConfirmarRenomear.ExecutarAsync());
        Assert.True(_t.Setups.RenomearAberto);
        Assert.NotNull(_t.Setups.ErroRenomear);

        _t.Setups.NomeRenomear = "Sala";
        await _b.Rodar(_t.Setups.ConfirmarRenomear.ExecutarAsync());
        await _b.Tela.EsvaziarAsync();

        Assert.False(_t.Setups.RenomearAberto);
        Assert.Equal("Agora se chama Sala.", _t.Setups.Mensagem);
        Assert.Equal("Setup Sala ›", _t.Inicio.Seletor);
        Assert.Equal("Sala", _t.Setups.Linhas[0].Nome);
    }

    [Fact]
    public async Task Barra_inferior_troca_de_destino()
    {
        Assert.True(_t.Casca.NoInicio);
        await _t.Casca.IrMesa.ExecutarAsync();
        Assert.True(_t.Casca.NaMesa);
        Assert.False(_t.Casca.NoInicio);
        await _t.Casca.IrAudio.ExecutarAsync();
        Assert.Equal(Aba.Audio, _t.Casca.Aba);
    }
}
