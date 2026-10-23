using System.Text.RegularExpressions;
using BgsControle.Nucleo.Escrita;
using BgsControle.Nucleo.Herdado;
using BgsControle.Nucleo.Leitura;
using BgsControle.Nucleo.Memorias;
using BgsControle.Nucleo.Registro;
using BgsControle.Nucleo.Seguranca;
using BgsControle.Nucleo.Setups;
using BgsControle.Simulador;

namespace BgsControle.Nucleo.Testes;

/// <summary>
/// A história 8c no núcleo, contra o processador simulado: os Setups, as memórias do celular, a
/// conferência ao conectar com o mudo da dúvida, o Salvar por camadas e a FILME herdada dividida
/// em três. Uma linha da matriz de <c>8c-setup-e-memoria-no-celular.md</c> por teste (ou mais).
/// </summary>
public sealed class SetupsTestes : IDisposable
{
    private static readonly TemposSessao Tempos = new(
        Scan: TimeSpan.FromSeconds(1),
        TentativaConexao: TimeSpan.FromSeconds(1),
        PausaEntreTentativas: TimeSpan.FromMilliseconds(1),
        RespostaSenha: TimeSpan.FromSeconds(2),
        PrazoSenha: TimeSpan.FromSeconds(60));

    private static readonly TemposLeitura LeituraRapida = new(TimeSpan.Zero, TimeSpan.FromMilliseconds(100));
    private static readonly Regex Senha = new(@"^\*000\*[0-9A-Z]{4}\*$");

    private readonly RelogioFalso _relogio = new();
    private readonly ProcessadorSimulado _simulador;
    private readonly Chaves _chaves;
    private readonly LogBle _log = new();
    private readonly Sessao _sessao;
    private readonly LeituraAutomatica _leitura;
    private readonly EstadoAparelho _estado;
    private readonly Escritor _escritor;
    private readonly TextoMemoria _guardadoCopia = new();
    private readonly TextoMemoria _guardadoLink = new();
    private readonly TextoMemoria _guardadoSetups;
    private readonly TextoMemoria _guardadoMudo;
    private readonly CopiaAntes _copia;
    private readonly LinkCanais _link;
    private readonly GerenteMemorias _memorias;
    private readonly GerenteSetups _setups;

    private static Parametro Ganho => Catalogo.ParametroDe("081")!;
    private static Parametro Limiter => Catalogo.ParametroDe("071")!;
    private static Parametro Atraso => Catalogo.ParametroDe("050")!;
    private static Parametro Banda => Catalogo.ParametroDe("020")!;

    private static string Casa => ConhecimentoHerdado.IdSetupCasa;
    private static string Filme => ConhecimentoHerdado.IdMemoriaFilme;

    public SetupsTestes() : this(null, null)
    {
    }

    private SetupsTestes(string? setupsGuardado, string? mudoGuardado)
    {
        _guardadoSetups = new TextoMemoria(setupsGuardado);
        _guardadoMudo = new TextoMemoria(mudoGuardado);
        _simulador = new ProcessadorSimulado(_relogio) { AtrasoSenha = TimeSpan.Zero, AtrasoLeitura = TimeSpan.Zero };
        _chaves = new Chaves(new RepositorioMemoria());
        _sessao = new Sessao(_simulador, new CofreFalso { Senha = "AB12" }, _log, Tempos, _chaves);
        var leitor = new LeitorCompleto(_sessao, _log, LeituraRapida);
        _leitura = new LeituraAutomatica(_sessao, leitor);
        _estado = new EstadoAparelho(_sessao, _leitura);
        _escritor = new Escritor(_sessao, _estado, _log, relogio: _relogio);
        _copia = new CopiaAntes(_sessao, _estado, _escritor, _leitura, _guardadoCopia, _log, _relogio);
        _link = new LinkCanais(_sessao, _estado, _escritor, _guardadoLink, _log);
        _memorias = new GerenteMemorias(_sessao, _estado, _leitura, _copia, _log, relogio: _relogio);
        _setups = new GerenteSetups(_sessao, _estado, _escritor, _leitura, _copia, _link, _memorias, _chaves,
            _guardadoSetups, _guardadoMudo, _log, _relogio);
    }

    /// <summary>Critério de aceite: nenhum quadro <c>*240*</c> ou <c>*23N*</c> chega ao simulador, em nenhum teste.</summary>
    public void Dispose()
    {
        Assert.DoesNotContain(_simulador.Escritas, e => e.StartsWith("*240*", StringComparison.Ordinal) || e.StartsWith("*23", StringComparison.Ordinal));
        Assert.All(_simulador.Escritas, e =>
            Assert.True(Senha.IsMatch(e) || Catalogo.EhLeitura(e) || Catalogo.EscritaDeAudio(e) is not null
                        || Catalogo.ComandoDeMemoria(e) is not null, e));
    }

    // ----------------------------------------------------------------- apoio

    private async Task Conectar()
    {
        await _chaves.Carregada;
        await _copia.Carregada;
        await _link.Carregada;
        await _setups.Carregada;
        await _sessao.ConectarAsync();
        Assert.Equal(EstadoSessao.Conectado, _sessao.Estado);
        await AndarAte(() => _estado.Pronto && !_estado.Lendo && !_leitura.Lendo && !_copia.EmAndamento && _setups.Conferido);
    }

    private async Task Reconectar()
    {
        await _sessao.DesconectarAsync();
        await Conectar();
    }

    private async Task AndarAte(Func<bool> condicao)
    {
        for (var i = 0; i < 3000 && !condicao(); i++)
        {
            _relogio.Avancar(TimeSpan.FromMilliseconds(10));
            await Task.Delay(1);
        }
        Assert.True(condicao(), "a condição não ficou verdadeira");
    }

    private async Task<T> Rodar<T>(Task<T> tarefa)
    {
        await AndarAte(() => tarefa.IsCompleted);
        return await tarefa;
    }

    private List<string> EscritasDesde(int inicio) => _simulador.Escritas.Skip(inicio).ToList();

    private List<string> AudioDesde(int inicio) =>
        EscritasDesde(inicio).Where(e => Catalogo.EscritaDeAudio(e) is not null).ToList();

    private bool Mutados(bool mutado) =>
        Enumerable.Range(0, 4).All(c => _simulador.Valor("100", c) == (mutado ? "1" : "0"));

    /// <summary>A calibração do aparelho fica diferente da do Casa 2.1: o atraso da saída 3 em 0 (80 no Casa).</summary>
    private void MexerNaCalibracao() => _simulador.Definir("050", 2, "0.0");

    private async Task<string> EntrarNaDuvida()
    {
        MexerNaCalibracao();
        await Conectar();
        Assert.Equal(EstadoConferencia.Duvida, _setups.Estado);
        return _setups.Referencia!.Id;
    }

    private static string? Valor(IReadOnlyList<ValorCopiado> valores, string codigo, int? indice) =>
        valores.FirstOrDefault(v => v.Codigo == codigo && v.Indice == indice)?.NoFio;

    // ----------------------------------------------------------------- a FILME herdada em três camadas (CAP-6)

    [Fact]
    public void A_FILME_herdada_composta_Casa_mais_FILME_mais_Cinema_e_a_leitura_de_18_10()
    {
        var setup = ConhecimentoHerdado.SetupCasa;
        var memoria = ConhecimentoHerdado.MemoriaFilme;
        var composto = new Dictionary<(string, int?), string>();
        foreach (var v in setup.Calibracao.Concat(Camadas.Compor(memoria.Carater, setup.Equilibrio)).Concat(ConhecimentoHerdado.BandasCinema))
            Assert.True(composto.TryAdd((v.Codigo, v.Indice), v.NoFio), $"{v.Codigo} {v.Indice} repetido");

        var leitura = ConhecimentoHerdado.PresetFilme.Valores.Where(v => Camadas.De(v.Codigo) != Camada.Fora).ToArray();
        Assert.Equal(leitura.Length, composto.Count);
        foreach (var v in leitura)
            Assert.True(Camadas.Iguais(v.Codigo, v.NoFio, composto[(v.Codigo, v.Indice)]), $"{v.Codigo} {v.Indice}: {v.NoFio} × {composto[(v.Codigo, v.Indice)]}");

        // As três camadas cobrem o Marco 1 inteiro, menos o mute.
        Assert.Equal(Catalogo.ConsultasMarco1.Count - 4, composto.Count);
        Assert.Equal(15 + 1, ConhecimentoHerdado.BandasCinema.Count); // as 15 bandas e o preset de EQ do aparelho
    }

    [Fact]
    public void A_memoria_FILME_guarda_a_R_sem_o_equilibrio_e_o_Casa_2_1_tem_R_menos_3()
    {
        var memoria = ConhecimentoHerdado.MemoriaFilme;
        var setup = ConhecimentoHerdado.SetupCasa;

        Assert.Equal("+10.0", Valor(memoria.Carater, "081", 1));
        Assert.Equal("-15.0", Valor(memoria.Carater, "071", 1));
        Assert.Equal("+10.0", Valor(memoria.Carater, "081", 0));
        Assert.Equal(new Equilibrio(0m, 0m, -3.0m, -3.0m), setup.Equilibrio);
        Assert.False(setup.Link12);
        Assert.False(setup.Link34);
        Assert.True(setup.Herdado);
        Assert.True(memoria.Herdado);
        Assert.Equal("Casa 2.1", setup.Nome);
        Assert.Equal("FILME", memoria.Nome);
        Assert.All(setup.Calibracao, v => Assert.Equal(Camada.Calibracao, Camadas.De(v.Codigo)));
        Assert.All(memoria.Carater, v => Assert.Equal(Camada.Carater, Camadas.De(v.Codigo)));
        Assert.Contains(ConhecimentoHerdado.Secoes, s => s.Titulo == "A FILME em três camadas");
    }

    [Theory]
    [InlineData("081", 1, "+10.0", "+7.0")] // a R da FILME: +10 − 3
    [InlineData("071", 1, "-15.0", "-18.0")]
    [InlineData("071", 1, "0.5", "0.5")] // OFF fica OFF, sem soma
    [InlineData("071", 1, "+0.5", "+0.5")]
    [InlineData("081", 1, "-44.0", "-45.0")] // preso à faixa do MAPA
    [InlineData("071", 1, "-47.0", "-48.0")]
    [InlineData("081", 0, "+10.0", "+10.0")] // a saída 1 sem equilíbrio no Casa
    [InlineData("081", 2, "+6.0", "+6.0")] // o sub não tem equilíbrio
    [InlineData("091", 1, "+2.5", "+2.5")] // só o ganho e o limiter levam o equilíbrio
    public void Somar_o_equilibrio_do_Casa(string codigo, int indice, string memoria, string final) =>
        Assert.Equal(final, Camadas.Somar(codigo, indice, memoria, ConhecimentoHerdado.EquilibrioCasa));

    [Fact]
    public void Somar_preso_ao_teto_e_subtrair_volta_a_parte()
    {
        var eq = new Equilibrio(5.0m, 3.0m, 0m, 0m);

        Assert.Equal("+15.0", Camadas.Somar("081", 0, "+14.0", eq));
        Assert.Equal("+0.0", Camadas.Somar("071", 0, "-1.0", eq));
        Assert.Equal("+9.0", Camadas.Subtrair("081", 0, "+14.0", eq));
        Assert.Equal("0.5", Camadas.Subtrair("071", 0, "0.5", eq));
        // A parte que daria +0.5 (OFF no fio) fica em +0.4.
        Assert.Equal("+0.4", Camadas.Subtrair("071", 0, "+3.5", eq));
    }

    // ----------------------------------------------------------------- conferência

    [Fact]
    public async Task Primeira_conexao_com_o_aparelho_na_FILME_Setup_confere_Casa_em_uso_e_FILME_em_uso_nada_escrito()
    {
        await Conectar();

        Assert.Equal(EstadoConferencia.Confere, _setups.Estado);
        Assert.Equal(Casa, _setups.SetupEmUso!.Id);
        Assert.Equal(SomEmUso.DoCelular(Filme), _setups.EmUso);
        Assert.False(_setups.Mutado);
        Assert.Null(_setups.MotivoBloqueioIa);
        Assert.All(_simulador.Escritas, e => Assert.True(Catalogo.EhLeitura(e) || Senha.IsMatch(e), e));
        Assert.Equal(0, _guardadoSetups.Gravacoes);
        Assert.Equal(0, _guardadoMudo.Gravacoes);
    }

    [Fact]
    public async Task So_o_carater_difere_som_diferente_sem_mutar_e_Reaplicar_escreve_so_o_que_difere()
    {
        _simulador.Definir("081", 0, "+4.0");
        await Conectar();

        Assert.Equal(EstadoConferencia.SomDiferente, _setups.Estado);
        Assert.Equal(SomEmUso.SomDoAparelho, _setups.EmUso);
        Assert.Equal(Casa, _setups.SetupEmUso!.Id);
        Assert.False(_setups.Mutado);
        Assert.True(Mutados(false));
        Assert.Equal("Som diferente da FILME", GerenteSetups.MsgSomDiferente(_setups.Ativa(Casa)!.Nome));
        var inicio = _simulador.Escritas.Count;

        var r = await Rodar(_setups.ReaplicarAsync());

        Assert.True(r.Ok, r.Mensagem);
        Assert.Equal(["*081*0*10.0*"], AudioDesde(inicio));
        Assert.Equal(EstadoConferencia.Confere, _setups.Estado);
        Assert.Equal(SomEmUso.DoCelular(Filme), _setups.EmUso);
        Assert.Null(CopiaAntes.DeJson(_guardadoCopia.Texto!)!.Operacao);
    }

    [Fact]
    public async Task Calibracao_difere_muta_os_4_canais_guarda_a_marca_e_lista_a_diferenca()
    {
        MexerNaCalibracao();
        await Conectar();

        Assert.Equal(EstadoConferencia.Duvida, _setups.Estado);
        Assert.Null(_setups.SetupEmUso);
        Assert.Equal(Casa, _setups.Referencia!.Id);
        Assert.True(Mutados(true));
        Assert.True(_setups.Mutado);
        var d = Assert.Single(_setups.Diferencas);
        Assert.Equal("Atraso da saída 3: 80 cm no Casa 2.1, 0 cm no aparelho", d.Linha("Casa 2.1"));
        Assert.Equal(GerenteSetups.MsgDuvida, _setups.MotivoBloqueioIa);
        var marca = GerenteSetups.MarcaDeJson(_guardadoMudo.Texto!)!;
        Assert.Equal(Casa, marca.IdSetup);
        Assert.Equal(["0", "0", "0", "0"], marca.MuteAnterior);
        // Na dúvida, carregar uma memória do aparelho também é recusado.
        var carregar = await _memorias.CarregarAsync(0);
        Assert.Equal(GerenteSetups.MsgDuvida, carregar.Motivo);
    }

    [Fact]
    public async Task Mutar_recusado_pela_chave_a_duvida_abre_mesmo_assim_com_o_motivo()
    {
        await _chaves.Carregada;
        Assert.True((await _chaves.DefinirAsync(Chave.AjustesAudio, false, OrigemComando.Tela)).Ok);
        MexerNaCalibracao();

        await Conectar();

        Assert.Equal(EstadoConferencia.Duvida, _setups.Estado);
        Assert.False(_setups.Mutado);
        Assert.True(Mutados(false));
        Assert.Equal(GerenteSetups.MsgNaoMutou(Sessao.MotivoChaveDesligada), _setups.MotivoMudo);
        Assert.Equal(0, _guardadoMudo.Gravacoes);
        Assert.Equal(GerenteSetups.MsgDuvida, _setups.MotivoBloqueioIa);
    }

    [Fact]
    public async Task Duvida_interrompida_volta_a_duvida_sem_mutar_de_novo()
    {
        await EntrarNaDuvida();
        var marca = _setups.Marca;
        _simulador.Definir("100", 3, "1"); // continua mutado no aparelho

        await Reconectar();
        var inicio = _simulador.Escritas.Count;
        await AndarAte(() => _setups.Conferido);

        Assert.Equal(EstadoConferencia.Duvida, _setups.Estado);
        Assert.Equal(marca, _setups.Marca);
        Assert.Single(_setups.Diferencas);
        Assert.Empty(AudioDesde(inicio));
        Assert.True(Mutados(true));
    }

    [Fact]
    public async Task Marca_guardada_com_o_aparelho_desmutado_por_fora_apaga_a_marca_e_confere_normal()
    {
        await EntrarNaDuvida();
        await _sessao.DesconectarAsync();
        // O app da Taramps desmutou e voltou o atraso.
        for (var c = 0; c < 4; c++)
            _simulador.Definir("100", c, "0");
        _simulador.Definir("050", 2, "80.0");

        await Conectar();

        Assert.Equal(EstadoConferencia.Confere, _setups.Estado);
        Assert.Null(_setups.Marca);
        Assert.Null(GerenteSetups.MarcaDeJson(_guardadoMudo.Texto!));
        Assert.True(Mutados(false));
    }

    // ----------------------------------------------------------------- escolher, criar, seguir, trocar

    [Fact]
    public async Task Escolher_o_Casa_2_1_na_duvida_aplica_a_calibracao_e_a_FILME_relê_desmuta_e_confere()
    {
        await EntrarNaDuvida();
        var inicio = _simulador.Escritas.Count;
        var leituras = _leitura.Iniciadas;

        var r = await Rodar(_setups.EscolherAsync(Casa));

        Assert.True(r.Ok, r.Mensagem);
        Assert.Equal(["*050*2*80.0*", "*100*0*0*", "*100*1*0*", "*100*2*0*", "*100*3*0*"], AudioDesde(inicio));
        Assert.Equal(leituras + 1, _leitura.Iniciadas); // a releitura
        Assert.Equal(EstadoConferencia.Confere, _setups.Estado);
        Assert.Equal(Casa, _setups.SetupEmUso!.Id);
        Assert.Equal(SomEmUso.DoCelular(Filme), _setups.EmUso);
        Assert.Null(_setups.Marca);
        Assert.True(Mutados(false));
        Assert.Null(_setups.MotivoBloqueioIa);
        Assert.Null(CopiaAntes.DeJson(_guardadoCopia.Texto!)!.Operacao);
        Assert.Contains(CopiaAntes.DeJson(_guardadoCopia.Texto!)!.Valores, v => v is { Codigo: "100", Indice: 0, NoFio: "1" }); // a cópia, mutada
    }

    [Fact]
    public async Task Escolher_com_a_chave_Aplicar_memoria_do_celular_desligada_recusa_antes_do_BLE()
    {
        await EntrarNaDuvida();
        Assert.True((await _chaves.DefinirAsync(Chave.AplicarMemoriaCelular, false, OrigemComando.Tela)).Ok);
        var inicio = _simulador.Escritas.Count;

        var r = await Rodar(_setups.EscolherAsync(Casa));

        Assert.False(r.Ok);
        Assert.Equal(Sessao.MotivoChaveDesligada, r.Motivo);
        Assert.Equal(inicio, _simulador.Escritas.Count);
        Assert.Equal(EstadoConferencia.Duvida, _setups.Estado);
    }

    [Fact]
    public async Task Queda_no_meio_do_escolher_fica_pela_metade_mutado_e_Terminar_refaz_e_desmuta()
    {
        _simulador.Definir("040", 2, "45"); // duas diferenças: o passa-alta e o atraso da saída 3
        await EntrarNaDuvida();
        _simulador.CairDepoisDaEscrita = _simulador.Escritas.Count + 1;

        var r = await Rodar(_setups.EscolherAsync(Casa));

        Assert.False(r.Ok);
        Assert.Equal(EstadoSessao.Desconectado, _sessao.Estado);
        Assert.True(Mutados(true));
        Assert.Equal(GerenteSetups.TipoAplicarSetup, CopiaAntes.DeJson(_guardadoCopia.Texto!)!.Operacao!.Tipo);

        await Conectar();
        await AndarAte(() => _copia.PelaMetade is not null);
        Assert.Equal(EstadoConferencia.Duvida, _setups.Estado); // dúvida interrompida, sem mutar de novo
        Assert.Equal(GerenteSetups.TipoAplicarSetup, _copia.PelaMetade!.Tipo);

        var terminar = await Rodar(_copia.TerminarAsync());

        Assert.True(terminar.Ok, terminar.Motivo);
        Assert.Equal(EstadoConferencia.Confere, _setups.Estado);
        Assert.True(Mutados(false));
        Assert.Null(_setups.Marca);
        Assert.Null(_copia.PelaMetade);
        Assert.Equal("80.0", _simulador.Valor("050", 2));
    }

    [Fact]
    public async Task Criar_Setup_novo_na_duvida_nasce_com_a_calibracao_lida_e_desmuta()
    {
        await EntrarNaDuvida();
        var inicio = _simulador.Escritas.Count;

        var r = await Rodar(_setups.CriarSetupAsync("Carro"));

        Assert.True(r.Ok, r.Mensagem);
        var carro = _setups.SetupEmUso!;
        Assert.Equal("Carro", carro.Nome);
        Assert.Equal("0.0", Valor(carro.Calibracao, "050", 2));
        Assert.Equal(Equilibrio.Zero, carro.Equilibrio);
        Assert.False(carro.Link12);
        Assert.Equal(Camadas.Consultas(Camada.Calibracao).Count, carro.Calibracao.Count);
        Assert.Equal(SomEmUso.Nenhum, _setups.EmUso);
        Assert.Empty(_setups.MemoriasDe(carro.Id));
        Assert.Equal(carro.Id, _setups.UltimoSetup);
        Assert.Equal(["*100*0*0*", "*100*1*0*", "*100*2*0*", "*100*3*0*"], AudioDesde(inicio)); // só o desmutar
        Assert.Null(_setups.Marca);
        Assert.Equal(EstadoConferencia.Confere, _setups.Estado);

        // A próxima conexão confere com o Carro, sem dúvida.
        await Reconectar();
        Assert.Equal(EstadoConferencia.Confere, _setups.Estado);
        Assert.Equal(carro.Id, _setups.SetupEmUso!.Id);
    }

    [Theory]
    [InlineData("casa 2.1", "Use de 1 a 24 letras, números, espaço, _ ou -")] // o ponto não vale (a regra dos presets)
    [InlineData("", "Use de 1 a 24 letras, números, espaço, _ ou -")]
    [InlineData("nome#ruim", "Use de 1 a 24 letras, números, espaço, _ ou -")]
    [InlineData("vinte e cinco caracteres!", "Use de 1 a 24 letras, números, espaço, _ ou -")]
    public async Task Criar_Setup_com_nome_invalido_ou_repetido_recusa_e_nada_muda(string nome, string motivo)
    {
        await EntrarNaDuvida();
        var inicio = _simulador.Escritas.Count;

        var r = await _setups.CriarSetupAsync(nome);

        Assert.False(r.Ok);
        Assert.Equal(motivo, r.Motivo);
        Assert.Single(_setups.Setups);
        Assert.Equal(EstadoConferencia.Duvida, _setups.Estado);
        Assert.Equal(inicio, _simulador.Escritas.Count);
        Assert.True(_setups.Mutado);
    }

    [Fact]
    public async Task Criar_Setup_com_nome_repetido_sem_diferenca_de_caixa_recusa()
    {
        await EntrarNaDuvida();
        Assert.True((await Rodar(_setups.CriarSetupAsync("Carro"))).Ok);
        Assert.True((await Rodar(_setups.TrocarAsync())).Ok);

        var r = await _setups.CriarSetupAsync("CARRO");

        Assert.False(r.Ok);
        Assert.Equal(GerenteSetups.MsgNomeRepetidoSetup, r.Motivo);
        Assert.Equal(2, _setups.Setups.Count);
        Assert.Equal(EstadoConferencia.Trocando, _setups.Estado);
        Assert.True((await Rodar(_setups.DesistirAsync())).Ok);
    }

    [Fact]
    public async Task Seguir_sem_Setup_desmuta_sem_aplicar_e_as_memorias_do_celular_ficam_travadas()
    {
        await EntrarNaDuvida();
        var inicio = _simulador.Escritas.Count;

        var r = await Rodar(_setups.SeguirSemSetupAsync());

        Assert.True(r.Ok, r.Mensagem);
        Assert.Equal(["*100*0*0*", "*100*1*0*", "*100*2*0*", "*100*3*0*"], AudioDesde(inicio));
        Assert.Null(_setups.SetupEmUso);
        Assert.Equal(SomEmUso.SomDoAparelho, _setups.EmUso);
        Assert.Null(_setups.MotivoBloqueioIa);
        Assert.Equal("0.0", _simulador.Valor("050", 2)); // nada aplicado
        Assert.Equal(GerenteSetups.MsgSemSetup, (await _setups.AplicarAsync(Filme)).Motivo);
        Assert.Equal(GerenteSetups.MsgSemSetup, (await _setups.CriarMemoriaAsync("NOVA")).Motivo);
        Assert.Equal(GerenteSetups.MsgSemSetupSalvar, _setups.PrepararSalvar().Trava);
    }

    [Fact]
    public async Task Trocar_de_Setup_muta_e_desistir_desmuta_e_volta_ao_estado_de_antes()
    {
        await Conectar();
        var inicio = _simulador.Escritas.Count;

        var trocar = await Rodar(_setups.TrocarAsync());

        Assert.True(trocar.Ok, trocar.Mensagem);
        Assert.Equal(EstadoConferencia.Trocando, _setups.Estado);
        Assert.True(Mutados(true));
        Assert.Empty(_setups.Diferencas); // "O aparelho está no Casa 2.1."
        Assert.Equal(GerenteSetups.MsgDuvida, _setups.MotivoBloqueioIa);

        var desistir = await Rodar(_setups.DesistirAsync());

        Assert.True(desistir.Ok, desistir.Mensagem);
        Assert.Equal(EstadoConferencia.Confere, _setups.Estado);
        Assert.Equal(Casa, _setups.SetupEmUso!.Id);
        Assert.Equal(SomEmUso.DoCelular(Filme), _setups.EmUso);
        Assert.True(Mutados(false));
        Assert.Null(_setups.Marca);
        Assert.Equal(["*100*0*1*", "*100*1*1*", "*100*2*1*", "*100*3*1*", "*100*0*0*", "*100*1*0*", "*100*2*0*", "*100*3*0*"],
            AudioDesde(inicio));
    }

    [Fact]
    public async Task O_desmutar_volta_ao_mute_anterior_de_cada_canal()
    {
        _simulador.Definir("100", 3, "1"); // a saída 4 já estava mutada
        await EntrarNaDuvida();
        Assert.Equal(["0", "0", "0", "1"], _setups.Marca!.MuteAnterior);

        await Rodar(_setups.SeguirSemSetupAsync());

        Assert.Equal("0", _simulador.Valor("100", 0));
        Assert.Equal("1", _simulador.Valor("100", 3));
    }

    // ----------------------------------------------------------------- memórias do celular

    [Fact]
    public async Task Criar_memoria_do_som_atual_guarda_o_ganho_da_R_sem_o_equilibrio_e_vira_a_em_uso()
    {
        await Conectar();
        Assert.Equal("+7.0", _simulador.Valor("081", 1)); // a R lida
        Assert.True((await Rodar(_escritor.EscreverAsync(Ganho, 0, 4.0m))).Ok);
        var inicio = _simulador.Escritas.Count;

        var r = await Rodar(_setups.CriarMemoriaAsync("MUSICA ORIG"));

        Assert.True(r.Ok, r.Mensagem);
        Assert.Equal("Memória MUSICA ORIG criada no Casa 2.1.", r.Mensagem);
        var nova = _setups.MemoriasDe(Casa).Single(m => m.Nome == "MUSICA ORIG");
        Assert.Equal("+10.0", Valor(nova.Carater, "081", 1));
        Assert.Equal("-15.0", Valor(nova.Carater, "071", 1));
        Assert.Equal("+4.0", Valor(nova.Carater, "081", 0));
        Assert.Equal(Camadas.Consultas(Camada.Carater).Count, nova.Carater.Count);
        Assert.Equal(SomEmUso.DoCelular(nova.Id), _setups.EmUso);
        Assert.Equal(nova.Id, _setups.Ativa(Casa)!.Id);
        Assert.Equal([Filme, nova.Id, null], _setups.Botoes(Casa).Select(m => m?.Id));
        Assert.Equal(inicio, _simulador.Escritas.Count); // criar não manda nada
    }

    [Theory]
    [InlineData("filme", "Já existe uma memória com esse nome no Casa 2.1.")]
    [InlineData("  ", "Use de 1 a 24 letras, números, espaço, _ ou -")]
    public async Task Criar_memoria_com_nome_repetido_no_Setup_ou_invalido_recusa(string nome, string motivo)
    {
        await Conectar();

        var r = await _setups.CriarMemoriaAsync(nome);

        Assert.False(r.Ok);
        Assert.Equal(motivo, r.Motivo);
        Assert.Single(_setups.MemoriasDe(Casa));
    }

    [Fact]
    public async Task Criar_memoria_com_uma_memoria_do_aparelho_em_uso_pede_a_folha_do_Salvar()
    {
        await Conectar();
        Assert.True((await Rodar(_memorias.CarregarAsync(1))).Ok);

        var r = await _setups.CriarMemoriaAsync("NOVA");

        Assert.False(r.Ok);
        Assert.True(r.PedeFolhaSalvar);
        Assert.Single(_setups.MemoriasDe(Casa));
    }

    [Fact]
    public async Task Aplicar_memoria_com_limiter_OFF_escreve_0_5_sem_soma_e_as_bandas_e_o_mute_nao_mudam()
    {
        await Conectar();
        // Uma memória "SEM LIMITE" com o limiter da saída 2 em OFF, criada do som atual.
        Assert.True((await Rodar(_escritor.EscreverAsync(Limiter, 1, 0.5m))).Ok);
        Assert.True((await Rodar(_setups.CriarMemoriaAsync("SEM LIMITE"))).Ok);
        var off = Valor(_setups.MemoriasDe(Casa).Single(m => m.Nome == "SEM LIMITE").Carater, "071", 1)!;
        Assert.Equal("OFF", Limiter.Decodificar(off).Valor!.Rotulo); // guardado como o aparelho devolveu, sem o equilíbrio
        Assert.True((await Rodar(_setups.AplicarAsync(Filme))).Ok);
        Assert.Equal("-18.0", _simulador.Valor("071", 1));
        // Uma banda e o mute da saída 4 mudam fora da memória.
        Assert.True((await Rodar(_escritor.EscreverAsync(Banda, 3, -4.0m))).Ok);
        _simulador.Definir("100", 3, "1");
        var inicio = _simulador.Escritas.Count;

        var r = await Rodar(_setups.AplicarAsync(_setups.MemoriasDe(Casa).Single(m => m.Nome == "SEM LIMITE").Id));

        Assert.True(r.Ok, r.Mensagem);
        Assert.Equal(["*071*1*0.5*"], AudioDesde(inicio));
        var copia = CopiaAntes.DeJson(_guardadoCopia.Texto!)!;
        Assert.Null(copia.Operacao); // a cópia antes foi guardada e a marca saiu no fim
        Assert.Contains(copia.Valores, v => v is { Codigo: "071", Indice: 1, NoFio: "-18.0" }); // o estado antes
        // O espelho relido: a calibração do Setup, o caráter da memória com o equilíbrio; bandas e mute como estavam.
        var espelho = _estado.Copiar();
        var memoria = _setups.Memoria(_setups.EmUso.IdMemoria)!;
        Assert.Empty(Camadas.Diferentes(ConhecimentoHerdado.SetupCasa.Calibracao, espelho));
        Assert.Empty(Camadas.Diferentes(Camadas.Compor(memoria.Carater, ConhecimentoHerdado.EquilibrioCasa), espelho));
        Assert.Equal(-4.0m, _estado.Valor("020", 3)!.Numero);
        Assert.Equal(1m, _estado.Valor("100", 3)!.Numero);
        Assert.Equal("SEM LIMITE", memoria.Nome);
    }

    [Fact]
    public async Task Aplicar_memoria_com_a_chave_desligada_recusa_antes_do_BLE()
    {
        await Conectar();
        Assert.True((await _chaves.DefinirAsync(Chave.AplicarMemoriaCelular, false, OrigemComando.Tela)).Ok);
        _simulador.Definir("081", 0, "+1.0");
        var inicio = _simulador.Escritas.Count;

        var r = await _setups.AplicarAsync(Filme);

        Assert.False(r.Ok);
        Assert.Equal(Sessao.MotivoChaveDesligada, r.Motivo);
        Assert.Equal(inicio, _simulador.Escritas.Count);
        Assert.Equal(0, _guardadoCopia.Gravacoes);
    }

    [Fact]
    public async Task Aplicar_uma_memoria_na_duvida_e_recusado()
    {
        await EntrarNaDuvida();

        var r = await _setups.AplicarAsync(Filme);

        Assert.False(r.Ok);
        Assert.Equal(GerenteSetups.MsgDuvida, r.Motivo);
    }

    [Fact]
    public void Botoes_fixos_a_memoria_nova_toma_o_lugar_da_usada_ha_mais_tempo()
    {
        var uso = new UsoDoSetup(["b", "a", "c"], ["a", "b", "c"]); // c usada há mais tempo

        var depois = GerenteSetups.Usar(uso, "d");

        Assert.Equal(["a", "b", "d"], depois.Botoes);
        Assert.Equal(["d", "b", "a", "c"], depois.Recentes);
        // Usar uma que já está nos botões não muda nenhum de lugar.
        Assert.Equal(["a", "b", "d"], GerenteSetups.Usar(depois, "a").Botoes);
        // Uma vaga vazia é ocupada antes de tirar alguém.
        Assert.Equal(["x", "y", null], GerenteSetups.Usar(new UsoDoSetup(["x"], ["x", null, null]), "y").Botoes);
    }

    [Fact]
    public void Apagar_a_memoria_de_um_botao_a_vaga_fica_com_a_usada_por_ultimo_fora_dos_botoes_ou_vazia()
    {
        var uso = new UsoDoSetup(["d", "x", "a", "b"], ["a", "b", "d"]);

        var depois = GerenteSetups.Remover(uso, "b");

        Assert.Equal(["a", "x", "d"], depois.Botoes);
        Assert.Equal(["a", null, "d"], GerenteSetups.Remover(depois, "x").Botoes);
    }

    [Fact]
    public async Task Apagar_a_memoria_em_uso_o_Setup_em_uso_e_o_Casa_herdado_e_recusado()
    {
        await Conectar();

        Assert.Equal(GerenteSetups.MsgMemoriaEmUso, (await _setups.ApagarMemoriaAsync(Filme)).Motivo);
        Assert.Equal(GerenteSetups.MsgSetupHerdado, (await _setups.ApagarSetupAsync(Casa)).Motivo);
        Assert.Single(_setups.MemoriasDe(Casa));
        Assert.Single(_setups.Setups);
    }

    [Fact]
    public async Task Apagar_e_renomear_memoria_nao_manda_nada_e_o_botao_vazio_fica_vazio()
    {
        await Conectar();
        Assert.True((await Rodar(_setups.CriarMemoriaAsync("NOITE"))).Ok);
        var noite = _setups.Ativa(Casa)!;
        Assert.True((await Rodar(_setups.AplicarAsync(Filme))).Ok);
        var inicio = _simulador.Escritas.Count;

        Assert.Equal(GerenteSetups.MsgNomeRepetidoMemoria("Casa 2.1"), (await _setups.RenomearMemoriaAsync(noite.Id, "FILME")).Motivo);
        Assert.True((await _setups.RenomearMemoriaAsync(noite.Id, "NOITE 2")).Ok);
        Assert.True((await _setups.ApagarMemoriaAsync(noite.Id)).Ok);

        Assert.Equal(inicio, _simulador.Escritas.Count);
        Assert.Equal([Filme, null, null], _setups.Botoes(Casa).Select(m => m?.Id));
        Assert.Equal([Filme], _setups.Dados.UsoDe(Casa).Recentes);
    }

    [Fact]
    public async Task Apagar_um_Setup_que_nao_esta_em_uso_leva_as_memorias_dele()
    {
        await EntrarNaDuvida();
        Assert.True((await Rodar(_setups.CriarSetupAsync("Carro"))).Ok);
        var carro = _setups.SetupEmUso!.Id;
        Assert.True((await Rodar(_setups.CriarMemoriaAsync("ESTRADA"))).Ok);
        Assert.True((await Rodar(_setups.TrocarAsync())).Ok);
        Assert.True((await Rodar(_setups.EscolherAsync(Casa))).Ok);

        var r = await _setups.ApagarSetupAsync(carro);

        Assert.True(r.Ok, r.Mensagem);
        Assert.Single(_setups.Setups);
        Assert.Empty(_setups.MemoriasDe(carro));
        Assert.DoesNotContain(_setups.Dados.Memorias, m => m.Nome == "ESTRADA");
    }

    // ----------------------------------------------------------------- memória do aparelho

    [Fact]
    public async Task Carregar_memoria_do_aparelho_fica_em_uso_sem_mudar_o_Setup_e_ao_reconectar_vira_som_do_aparelho()
    {
        _simulador.DefinirMemoria(1, "081", 0, "-5.0");
        await Conectar();

        var r = await Rodar(_memorias.CarregarAsync(1));

        Assert.True(r.Ok, r.Mensagem);
        Assert.Equal(SomEmUso.DoAparelho(1), _setups.EmUso);
        Assert.Equal(Casa, _setups.SetupEmUso!.Id);

        await Reconectar();

        Assert.Equal(EstadoConferencia.SomDiferente, _setups.Estado);
        Assert.Equal(SomEmUso.SomDoAparelho, _setups.EmUso);
    }

    // ----------------------------------------------------------------- Salvar por camadas

    [Fact]
    public async Task Salvar_com_a_memoria_do_celular_em_uso_leva_a_calibracao_ao_Setup_e_o_carater_a_memoria()
    {
        await Conectar();
        Assert.Equal(GerenteSetups.MsgNadaParaSalvar, _setups.PrepararSalvar().Trava);
        Assert.True((await Rodar(_escritor.EscreverAsync(Atraso, 2, 85.0m))).Ok);
        Assert.True((await Rodar(_escritor.EscreverAsync(Ganho, 0, 8.0m))).Ok);

        var folha = _setups.PrepararSalvar();

        Assert.Null(folha.Trava);
        Assert.False(folha.PedeNome);
        Assert.False(folha.PerguntaCalibracao);
        Assert.Equal(2, folha.Linhas.Count);
        Assert.Equal("Calibração → Setup Casa 2.1", folha.Linhas[0].Titulo);
        Assert.Equal("Atraso da saída 3: 80 cm → 85 cm", Assert.Single(folha.Linhas[0].Mudancas).Linha);
        Assert.Equal("Caráter → memória FILME", folha.Linhas[1].Titulo);
        Assert.Equal("Ganho da saída 1: +10,0 dB → +8,0 dB", Assert.Single(folha.Linhas[1].Mudancas).Linha);
        var inicio = _simulador.Escritas.Count;

        var r = await _setups.SalvarAsync(EscolhaCalibracao.Nenhuma, null);

        Assert.True(r.Ok, r.Mensagem);
        Assert.Equal("Salvo: calibração no Casa 2.1, caráter na FILME.", r.Mensagem);
        Assert.Equal("85.0", Valor(_setups.Setup(Casa)!.Calibracao, "050", 2));
        Assert.Equal("+8.0", Valor(_setups.Memoria(Filme)!.Carater, "081", 0));
        Assert.Equal("+10.0", Valor(_setups.Memoria(Filme)!.Carater, "081", 1)); // a R continua sem o equilíbrio
        Assert.Equal(inicio, _simulador.Escritas.Count); // salvar não manda nada
        Assert.Equal(GerenteSetups.MsgNadaParaSalvar, _setups.PrepararSalvar().Trava);
    }

    [Fact]
    public async Task Salvar_o_equilibrio_mexido_vai_para_o_Setup_e_a_memoria_nao_muda()
    {
        await Conectar();

        var ajuste = await Rodar(_setups.AjustarEquilibrioAsync(1, limiter: false, -2.0m));

        Assert.True(ajuste.Ok, ajuste.Mensagem);
        Assert.Equal("+8.0", _simulador.Valor("081", 1)); // +10 − 2
        var folha = _setups.PrepararSalvar();
        var linha = Assert.Single(folha.Linhas);
        Assert.Equal("Calibração → Setup Casa 2.1", linha.Titulo);
        Assert.Equal("Equilíbrio da saída 2 · ganho: −3,0 dB → −2,0 dB", Assert.Single(linha.Mudancas).Linha);

        var r = await _setups.SalvarAsync(EscolhaCalibracao.Nenhuma, null);

        Assert.True(r.Ok, r.Mensagem);
        Assert.Equal(new Equilibrio(0m, 0m, -2.0m, -3.0m), _setups.Setup(Casa)!.Equilibrio);
        Assert.Equal("+10.0", Valor(_setups.Memoria(Filme)!.Carater, "081", 1));
        Assert.Null(_setups.EquilibrioPendente);
        Assert.Equal(GerenteSetups.MsgNadaParaSalvar, _setups.PrepararSalvar().Trava);
    }

    [Fact]
    public async Task Equilibrio_fora_da_faixa_e_recusado()
    {
        await Conectar();

        var r = await _setups.AjustarEquilibrioAsync(0, limiter: true, -12.1m);

        Assert.False(r.Ok);
        Assert.Equal(GerenteSetups.MsgEquilibrioFaixa, r.Motivo);
        Assert.Equal(GerenteSetups.MsgEquilibrioSaida, (await _setups.AjustarEquilibrioAsync(2, false, 1.0m)).Motivo);
    }

    [Fact]
    public async Task Salvar_com_memoria_do_aparelho_e_calibracao_diferente_pede_nome_e_Manter_reaplica_a_do_Setup()
    {
        _simulador.DefinirMemoria(1, "050", 2, "60.0");
        _simulador.DefinirMemoria(1, "081", 0, "-5.0");
        await Conectar();
        Assert.True((await Rodar(_memorias.CarregarAsync(1))).Ok);

        var folha = _setups.PrepararSalvar();

        Assert.Null(folha.Trava);
        Assert.True(folha.PedeNome);
        Assert.True(folha.PerguntaCalibracao);
        Assert.Equal("Atraso da saída 3: 80 cm no Casa 2.1, 60 cm no aparelho", Assert.Single(folha.Diferencas).Linha("Casa 2.1"));
        Assert.Equal(GerenteSetups.MsgEscolha, (await _setups.SalvarAsync(EscolhaCalibracao.Nenhuma, "MUSICA ORIG")).Motivo);
        var inicio = _simulador.Escritas.Count;

        var r = await Rodar(_setups.SalvarAsync(EscolhaCalibracao.Manter, "MUSICA ORIG"));

        Assert.True(r.Ok, r.Mensagem);
        Assert.Equal(["*050*2*80.0*"], AudioDesde(inicio)); // a calibração do Casa volta; o caráter já é o da memória nova
        var nova = _setups.MemoriasDe(Casa).Single(m => m.Nome == "MUSICA ORIG");
        Assert.Equal("-5.0", Valor(nova.Carater, "081", 0));
        Assert.Equal("80.0", Valor(_setups.Setup(Casa)!.Calibracao, "050", 2)); // o Setup não mudou
        Assert.Equal(SomEmUso.DoCelular(nova.Id), _setups.EmUso);
        Assert.Equal(EstadoConferencia.Confere, _setups.Estado);
        Assert.Null(CopiaAntes.DeJson(_guardadoCopia.Texto!)!.Operacao);
    }

    [Fact]
    public async Task Salvar_com_memoria_do_aparelho_e_Atualizar_muda_o_Setup_sem_mandar_nada()
    {
        _simulador.DefinirMemoria(1, "050", 2, "60.0");
        await Conectar();
        Assert.True((await Rodar(_memorias.CarregarAsync(1))).Ok);
        var inicio = _simulador.Escritas.Count;

        var r = await _setups.SalvarAsync(EscolhaCalibracao.Atualizar, "MUSICA ORIG");

        Assert.True(r.Ok, r.Mensagem);
        Assert.Equal("Salvo: calibração no Casa 2.1, caráter na MUSICA ORIG.", r.Mensagem);
        Assert.Equal("60.0", Valor(_setups.Setup(Casa)!.Calibracao, "050", 2));
        Assert.Equal(inicio, _simulador.Escritas.Count);
    }

    [Fact]
    public async Task Salvar_com_memoria_do_aparelho_sem_diferenca_de_calibracao_so_pede_o_nome()
    {
        _simulador.DefinirMemoria(0, "081", 0, "-5.0");
        await Conectar();
        Assert.True((await Rodar(_memorias.CarregarAsync(0))).Ok);

        var folha = _setups.PrepararSalvar();

        Assert.Null(folha.Trava); // com memória do aparelho, o Salvar nunca trava por "nada mudou"
        Assert.True(folha.PedeNome);
        Assert.False(folha.PerguntaCalibracao);
        var r = await _setups.SalvarAsync(EscolhaCalibracao.Nenhuma, "SPL");
        Assert.True(r.Ok, r.Mensagem);
        Assert.Equal("Salvo: caráter na SPL.", r.Mensagem);
    }

    // ----------------------------------------------------------------- guardado

    [Fact]
    public async Task Reabrir_o_app_traz_Setups_memorias_ultimo_Setup_botoes_e_a_marca_iguais()
    {
        await EntrarNaDuvida();
        Assert.True((await Rodar(_setups.CriarSetupAsync("Carro"))).Ok);
        Assert.True((await Rodar(_setups.CriarMemoriaAsync("ESTRADA"))).Ok);
        Assert.True((await Rodar(_setups.TrocarAsync())).Ok);
        var dados = _setups.Dados;
        var marca = _setups.Marca;
        Assert.NotNull(marca);

        using var reaberto = new SetupsTestes(_guardadoSetups.Texto, _guardadoMudo.Texto);
        await reaberto._setups.Carregada;

        var outro = reaberto._setups.Dados;
        Assert.Equal(dados.UltimoSetup, outro.UltimoSetup);
        Assert.Equal(dados.Setups.Select(s => (s.Id, s.Nome, s.Equilibrio, s.Link12, s.Link34, s.Herdado)),
            outro.Setups.Select(s => (s.Id, s.Nome, s.Equilibrio, s.Link12, s.Link34, s.Herdado)));
        Assert.Equal(dados.Setups.SelectMany(s => s.Calibracao), outro.Setups.SelectMany(s => s.Calibracao));
        Assert.Equal(dados.Memorias.Select(m => (m.Id, m.IdSetup, m.Nome, m.Herdado)), outro.Memorias.Select(m => (m.Id, m.IdSetup, m.Nome, m.Herdado)));
        Assert.Equal(dados.Memorias.SelectMany(m => m.Carater), outro.Memorias.SelectMany(m => m.Carater));
        foreach (var s in dados.Setups)
        {
            Assert.Equal(dados.UsoDe(s.Id).Recentes, outro.UsoDe(s.Id).Recentes);
            Assert.Equal(dados.UsoDe(s.Id).Botoes, outro.UsoDe(s.Id).Botoes);
        }
        Assert.Equal(marca!.IdSetup, reaberto._setups.Marca!.IdSetup);
        Assert.Equal(marca.MuteAnterior, reaberto._setups.Marca.MuteAnterior);
        Assert.Equal(ConhecimentoHerdado.NotaSetupCasa, outro.Setups.Single(s => s.Id == Casa).Nota);
    }

    [Theory]
    [InlineData("{ isto não é json")]
    [InlineData("{\"formato\": 2, \"setups\": []}")]
    [InlineData("{\"formato\": 1, \"setups\": []}")]
    [InlineData("{\"formato\": \"um\"}")]
    [InlineData("[]")]
    public async Task Um_setups_json_estragado_volta_ao_herdado_sem_erro(string json)
    {
        using var reaberto = new SetupsTestes(json, "lixo");
        await reaberto._setups.Carregada;

        var dados = reaberto._setups.Dados;
        Assert.Equal([Casa], dados.Setups.Select(s => s.Id));
        Assert.Equal([Filme], dados.Memorias.Select(m => m.Id));
        Assert.Equal(Casa, dados.UltimoSetup);
        Assert.Null(reaberto._setups.Marca);
    }

    // ----------------------------------------------------------------- revisão da 8c

    [Fact]
    public async Task Ao_cair_a_conexao_o_Setup_em_uso_sai_ate_a_conferencia_nova()
    {
        await Conectar();
        Assert.NotNull(_setups.SetupEmUso);

        await _sessao.DesconectarAsync();

        Assert.Null(_setups.SetupEmUso);
        Assert.Equal(EstadoConferencia.Nenhuma, _setups.Estado);
        Assert.Equal(GerenteSetups.MsgSemSetup, (await _setups.CriarMemoriaAsync("NOVA")).Motivo);
        Assert.Equal(GerenteSetups.MsgSemConexao, (await _setups.AjustarEquilibrioAsync(1, false, -2.0m)).Motivo);
        await Conectar();
        Assert.Equal(Casa, _setups.SetupEmUso!.Id);
    }

    [Fact]
    public async Task Escolher_o_Casa_2_1_poe_o_link_dele_desligado()
    {
        await Conectar();
        Assert.True((await Rodar(_link.VincularAsync(ParCanais.Saidas12))).Ok);
        Assert.True(_link.Ligado(ParCanais.Saidas12));

        Assert.True((await Rodar(_setups.TrocarAsync())).Ok);
        Assert.True((await Rodar(_setups.EscolherAsync(Casa))).Ok);

        Assert.False(_link.Ligado(ParCanais.Saidas12));
    }

    [Fact]
    public async Task Criar_Setup_com_o_link_1_2_ligado_guarda_o_link_e_ele_sobrevive_ao_JSON()
    {
        await Conectar();
        Assert.True((await Rodar(_link.VincularAsync(ParCanais.Saidas12))).Ok);
        Assert.True((await Rodar(_setups.TrocarAsync())).Ok);

        Assert.True((await Rodar(_setups.CriarSetupAsync("Carro"))).Ok);

        var carro = _setups.SetupEmUso!;
        Assert.True(carro.Link12);
        Assert.False(carro.Link34);
        var relido = GerenteSetups.DeJson(GerenteSetups.ParaJson(_setups.Dados))!.Setups.Single(s => s.Id == carro.Id);
        Assert.True(relido.Link12);
        Assert.False(relido.Link34);
    }

    [Fact]
    public async Task O_mudo_da_duvida_passa_mesmo_com_uma_operacao_pela_metade()
    {
        _simulador.DefinirMemoria(1, "050", 2, "60.0");
        await Conectar();
        _simulador.CairDepoisDaEscrita = _simulador.Escritas.Count + 1; // cai logo depois do *241*1*

        var r = await Rodar(_memorias.CarregarAsync(1));
        Assert.False(r.Ok);
        Assert.Equal("*241*1*", _simulador.Escritas[^1]);

        await Conectar();
        await AndarAte(() => _copia.PelaMetade is not null);

        Assert.Equal(EstadoConferencia.Duvida, _setups.Estado);
        Assert.True(Mutados(true));
        Assert.True(_setups.Mutado);
        Assert.NotNull(_copia.PelaMetade);
    }

    [Fact]
    public async Task Salvar_num_Setup_sem_memorias_leva_a_calibracao_ao_Setup_e_cria_a_memoria()
    {
        await EntrarNaDuvida();
        Assert.True((await Rodar(_setups.CriarSetupAsync("Carro"))).Ok);
        var carro = _setups.SetupEmUso!.Id;
        Assert.True((await Rodar(_escritor.EscreverAsync(Atraso, 2, 30.0m))).Ok);

        var folha = _setups.PrepararSalvar();

        Assert.Null(folha.Trava);
        Assert.True(folha.PedeNome);
        Assert.False(folha.PerguntaCalibracao);
        Assert.Equal(["Calibração → Setup Carro", "Caráter → memória nova"], folha.Linhas.Select(l => l.Titulo));

        var r = await _setups.SalvarAsync(EscolhaCalibracao.Nenhuma, "PRIMEIRA");

        Assert.True(r.Ok, r.Mensagem);
        Assert.Equal("30.0", Valor(_setups.Setup(carro)!.Calibracao, "050", 2));
        var primeira = Assert.Single(_setups.MemoriasDe(carro));
        Assert.Equal("PRIMEIRA", primeira.Nome);
        Assert.Equal(SomEmUso.DoCelular(primeira.Id), _setups.EmUso);
    }

    [Fact]
    public async Task Equilibrio_do_limiter_escreve_o_final_e_fica_pendente()
    {
        await Conectar();
        var inicio = _simulador.Escritas.Count;

        var r = await Rodar(_setups.AjustarEquilibrioAsync(1, limiter: true, -5.0m));

        Assert.True(r.Ok, r.Mensagem);
        Assert.Equal(["*071*1*-20.0*"], AudioDesde(inicio)); // a parte da memória (−15) mais −5
        Assert.Equal(-5.0m, _setups.EquilibrioPendente!.Limiter2);
    }

    [Fact]
    public async Task Equilibrio_com_o_limiter_em_OFF_nao_escreve_nada()
    {
        _simulador.Definir("071", 1, "0.5");
        await Conectar();
        var inicio = _simulador.Escritas.Count;

        var r = await Rodar(_setups.AjustarEquilibrioAsync(1, limiter: true, -5.0m));

        Assert.True(r.Ok, r.Mensagem);
        Assert.Empty(AudioDesde(inicio));
        Assert.Equal(-5.0m, _setups.EquilibrioPendente!.Limiter2);
    }

    [Fact]
    public async Task Equilibrio_que_bate_no_teto_avisa_o_limite()
    {
        await Conectar();

        var r = await Rodar(_setups.AjustarEquilibrioAsync(0, limiter: false, 12.0m)); // +10 + 12 > +15

        Assert.True(r.Ok, r.Mensagem);
        Assert.Equal("No limite da faixa com o equilíbrio do Casa 2.1.", r.Mensagem);
        Assert.Equal("+15.0", _simulador.Valor("081", 0));
    }

    [Fact]
    public async Task Um_Manter_velho_sem_a_pergunta_nao_impede_de_salvar_a_calibracao()
    {
        await Conectar();
        Assert.True((await Rodar(_escritor.EscreverAsync(Atraso, 2, 85.0m))).Ok);

        var r = await _setups.SalvarAsync(EscolhaCalibracao.Manter, null);

        Assert.True(r.Ok, r.Mensagem);
        Assert.Equal("85.0", Valor(_setups.Setup(Casa)!.Calibracao, "050", 2));
    }

    [Fact]
    public async Task Manter_com_a_reaplicacao_falhando_salva_a_memoria_e_avisa_que_a_calibracao_nao_voltou()
    {
        _simulador.DefinirMemoria(1, "050", 2, "60.0");
        await Conectar();
        Assert.True((await Rodar(_memorias.CarregarAsync(1))).Ok);
        _simulador.CairNaEscrita = _simulador.Escritas.Count + 1; // a conexão cai ao receber o *050*2*80.0*

        var r = await Rodar(_setups.SalvarAsync(EscolhaCalibracao.Manter, "MUSICA ORIG"));

        Assert.True(r.Ok, r.Mensagem);
        Assert.StartsWith("Salvo: caráter na MUSICA ORIG. A calibração do Casa 2.1 não voltou:", r.Mensagem);
        Assert.Contains(_setups.MemoriasDe(Casa), m => m.Nome == "MUSICA ORIG");
        Assert.Equal("80.0", Valor(_setups.Setup(Casa)!.Calibracao, "050", 2));
    }

    [Fact]
    public async Task Manter_com_a_chave_Aplicar_memoria_desligada_recusa_antes_de_guardar()
    {
        _simulador.DefinirMemoria(1, "050", 2, "60.0");
        await Conectar();
        Assert.True((await Rodar(_memorias.CarregarAsync(1))).Ok);
        Assert.True((await _chaves.DefinirAsync(Chave.AplicarMemoriaCelular, false, OrigemComando.Tela)).Ok);
        var gravacoes = _guardadoSetups.Gravacoes;
        var inicio = _simulador.Escritas.Count;

        var r = await _setups.SalvarAsync(EscolhaCalibracao.Manter, "MUSICA ORIG");

        Assert.False(r.Ok);
        Assert.Equal(Sessao.MotivoChaveDesligada, r.Motivo);
        Assert.Equal(gravacoes, _guardadoSetups.Gravacoes);
        Assert.Equal(inicio, _simulador.Escritas.Count);
        Assert.DoesNotContain(_setups.MemoriasDe(Casa), m => m.Nome == "MUSICA ORIG");
    }

    [Fact]
    public void A_marca_nao_guardada_le_uma_vez_so_dentro_do_motivo_do_mudo() =>
        Assert.Equal("O som não foi mutado: não consegui guardar a marca do mudo no celular",
            GerenteSetups.MsgNaoMutou(GerenteSetups.MsgNaoGuardouMarca));

    // ----------------------------------------------------------------- o cenário inteiro

    [Fact]
    public async Task Conectar_duvida_escolher_criar_aplicar_salvar_e_carregar_sem_nenhum_240_nem_23N()
    {
        await EntrarNaDuvida();
        Assert.True((await Rodar(_setups.EscolherAsync(Casa))).Ok);
        Assert.True((await Rodar(_escritor.EscreverAsync(Ganho, 0, 5.0m))).Ok);
        Assert.True((await Rodar(_setups.CriarMemoriaAsync("MUSICA ORIG"))).Ok);
        Assert.True((await Rodar(_setups.AplicarAsync(Filme))).Ok);
        Assert.True((await Rodar(_escritor.EscreverAsync(Atraso, 0, 1.0m))).Ok);
        Assert.True((await _setups.SalvarAsync(EscolhaCalibracao.Nenhuma, null)).Ok);
        Assert.True((await Rodar(_memorias.CarregarAsync(2))).Ok);
        Assert.True((await Rodar(_setups.SalvarAsync(EscolhaCalibracao.Atualizar, "DO APARELHO"))).Ok);
        Assert.True((await Rodar(_setups.TrocarAsync())).Ok);
        Assert.True((await Rodar(_setups.DesistirAsync())).Ok);

        Assert.DoesNotContain(_simulador.Escritas, e => e.StartsWith("*240*", StringComparison.Ordinal) || e.StartsWith("*23", StringComparison.Ordinal));
        Assert.Contains("*241*2*", _simulador.Escritas);
        Assert.Equal(3, _setups.MemoriasDe(Casa).Count);
    }
}
