using System.Text.Json;
using System.Text.RegularExpressions;
using BgsControle.Nucleo.Escrita;
using BgsControle.Nucleo.Herdado;
using BgsControle.Nucleo.Leitura;
using BgsControle.Nucleo.Presets;
using BgsControle.Nucleo.Registro;
using BgsControle.Nucleo.Seguranca;
using BgsControle.Simulador;

namespace BgsControle.Nucleo.Testes;

/// <summary>
/// As histórias 8, 8d e 8f no núcleo, contra o processador simulado: o conhecimento herdado (CAP-6) e os
/// presets de 15 bandas do celular (CAP-5): o app nasce sem presets (29/10/2026); criar, atualizar,
/// renomear, apagar (com a trava do preset lembrado), o <c>presets.json</c> no formato 3 (os formatos 1
/// e 2 começam do zero), e o aplicar só das bandas.
/// </summary>
public sealed class PresetsTestes : IDisposable
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
    private readonly LeitorCompleto _leitor;
    private readonly LeituraAutomatica _leitura;
    private readonly EstadoAparelho _estado;
    private readonly Escritor _escritor;
    private readonly TextoMemoria _guardadoCopia = new();
    private readonly TextoMemoria _guardadoLink = new();
    private readonly TextoMemoria _guardadoPresets = new();
    private readonly CopiaAntes _copia;
    private readonly LinkCanais _link;
    private GerentePresets _presets;

    private static Parametro Ganho => Catalogo.ParametroDe("081")!;
    private static Parametro Eq => Catalogo.ParametroDe("020")!;

    public PresetsTestes()
    {
        // A sessão e o leitor esperam em tempo real e o relógio falso não anda enquanto conectam:
        // a senha e as leituras respondem na hora; os ecos e os acks seguem no relógio falso.
        _simulador = new ProcessadorSimulado(_relogio) { AtrasoSenha = TimeSpan.Zero, AtrasoLeitura = TimeSpan.Zero };
        _chaves = new Chaves(new RepositorioMemoria());
        _sessao = new Sessao(_simulador, new CofreFalso { Senha = "AB12" }, _log, Tempos, _chaves);
        _leitor = new LeitorCompleto(_sessao, _log, LeituraRapida);
        _leitura = new LeituraAutomatica(_sessao, _leitor);
        _estado = new EstadoAparelho(_sessao, _leitura);
        _escritor = new Escritor(_sessao, _estado, _log, relogio: _relogio);
        _copia = new CopiaAntes(_sessao, _estado, _escritor, _leitura, _guardadoCopia, _log, _relogio);
        _link = new LinkCanais(_sessao, _estado, _escritor, _guardadoLink, _log);
        _presets = NovoGerente(_guardadoPresets);
    }

    private GerentePresets NovoGerente(IRepositorioTexto repositorio) =>
        new(_sessao, _estado, _escritor, _leitura, _copia, repositorio, _log, _relogio);

    /// <summary>
    /// Critério de aceite: em qualquer teste, só chegaram ao simulador a senha, as leituras do
    /// catálogo e as escritas de áudio. Nenhum comando de memória do aparelho (240, 241, 23N).
    /// </summary>
    public void Dispose() =>
        Assert.All(_simulador.Escritas, e =>
            Assert.True(Senha.IsMatch(e) || Catalogo.EhLeitura(e) || Catalogo.EscritaDeAudio(e) is not null, e));

    private async Task Conectar()
    {
        await _chaves.Carregada;
        await _copia.Carregada;
        await _link.Carregada;
        await _presets.Carregada;
        await _sessao.ConectarAsync();
        Assert.Equal(EstadoSessao.Conectado, _sessao.Estado);
        await Esperar(() => _estado.Pronto && !_estado.Lendo && !_leitura.Lendo && !_copia.EmAndamento);
    }

    private static async Task Esperar(Func<bool> condicao)
    {
        for (var i = 0; i < 1000 && !condicao(); i++)
            await Task.Delay(10);
        Assert.True(condicao());
    }

    /// <summary>Anda o relógio falso até a tarefa acabar.</summary>
    private async Task<T> Rodar<T>(Task<T> tarefa)
    {
        for (var i = 0; i < 4000 && !tarefa.IsCompleted; i++)
        {
            _relogio.Avancar(TimeSpan.FromMilliseconds(10));
            await Task.Delay(1);
        }
        Assert.True(tarefa.IsCompleted, "a operação não terminou");
        return await tarefa;
    }

    private async Task AndarAte(Func<bool> condicao)
    {
        for (var i = 0; i < 2000 && !condicao(); i++)
        {
            _relogio.Avancar(TimeSpan.FromMilliseconds(10));
            await Task.Delay(1);
        }
        Assert.True(condicao());
    }

    private List<string> EscritasDesde(int inicio) => _simulador.Escritas.Skip(inicio).ToList();

    private List<string> AudioDesde(int inicio) => EscritasDesde(inicio).Where(e => Catalogo.EscritaDeAudio(e) is not null).ToList();

    private static string Banda(int indice, decimal valor) => Eq.CodificarEscrita(indice, valor);

    /// <summary>Uma linha por valor, em ordem, para comparar dois estados com uma diferença legível.</summary>
    private static string Linhas(IEnumerable<KeyValuePair<(string Codigo, int? Indice), string>> valores) =>
        string.Join("\n", valores.Select(kv => $"{kv.Key.Codigo}/{kv.Key.Indice}={kv.Value}").Order(StringComparer.Ordinal));

    private static string Linhas(IEnumerable<ValorCopiado> valores) =>
        Linhas(valores.Select(v => new KeyValuePair<(string, int?), string>((v.Codigo, v.Indice), v.NoFio)));

    private static string Linhas(Preset preset) => Linhas(preset.Valores);

    /// <summary>Só as 15 bandas de um estado do Marco 1.</summary>
    private static IReadOnlyDictionary<(string Codigo, int? Indice), string> SoBandas(IReadOnlyDictionary<(string Codigo, int? Indice), string> valores) =>
        valores.Where(kv => kv.Key.Codigo == "020").ToDictionary(kv => kv.Key, kv => kv.Value);

    /// <summary>Tudo menos as 15 bandas (a calibração, o caráter, o 220 e o mute).</summary>
    private static IReadOnlyDictionary<(string Codigo, int? Indice), string> SemBandas(IReadOnlyDictionary<(string Codigo, int? Indice), string> valores) =>
        valores.Where(kv => kv.Key.Codigo != "020").ToDictionary(kv => kv.Key, kv => kv.Value);

    /// <summary>Só os valores do Marco 1 de uma lista de valores (a cópia antes guarda também os extras).</summary>
    private static IReadOnlyDictionary<(string Codigo, int? Indice), string> SoMarco1(IEnumerable<ValorCopiado> valores) =>
        valores
            .Where(v => Catalogo.ConsultasMarco1.Any(c => c.Parametro.Codigo == v.Codigo && c.Indice == v.Indice))
            .ToDictionary(v => (v.Codigo, v.Indice), v => v.NoFio);

    /// <summary>O "Cinema" do cenário de teste: as 15 bandas de 18/10, guardado como preset próprio.</summary>
    private async Task<Preset> CriarCinema()
    {
        var r = await _presets.CriarAsync(CenarioFilme.NomePreset, CenarioFilme.Bandas);
        Assert.True(r.Ok, r.Mensagem);
        return r.Preset!;
    }

    private async Task<Preset> Criar(string nome)
    {
        var r = await _presets.CriarAsync(nome);
        Assert.True(r.Ok, r.Mensagem);
        return r.Preset!;
    }

    /// <summary>Muda 3 das 15 bandas (25 Hz, 160 Hz e 2,5 kHz) e devolve o estado do aparelho depois disso.</summary>
    private async Task<IReadOnlyDictionary<(string Codigo, int? Indice), string>> Mudar3()
    {
        Assert.True((await Rodar(_escritor.EscreverAsync(Eq, 0, 3.0m))).Ok);
        Assert.True((await Rodar(_escritor.EscreverAsync(Eq, 4, -2.0m))).Ok);
        Assert.True((await Rodar(_escritor.EscreverAsync(Eq, 10, 5.0m))).Ok);
        return _simulador.EstadoMarco1();
    }

    // ----------------------------------------------------------------- conhecimento herdado

    [Fact]
    public void Primeira_abertura_nenhum_preset_sem_arquivo_nenhum()
    {
        Assert.Empty(_presets.Presets);
        Assert.Empty(ConhecimentoHerdado.Presets);
        Assert.Empty(_presets.Proprios);
        Assert.Equal(0, _guardadoPresets.Gravacoes);
        Assert.Empty(_simulador.Escritas);
    }

    [Fact]
    public async Task O_Cinema_do_cenario_tem_as_15_bandas_da_leitura_de_18_10_na_ordem_do_catalogo()
    {
        var cinema = await CriarCinema();

        Assert.False(cinema.Herdado);
        Assert.Equal(15, cinema.Valores.Count);
        Assert.All(cinema.Valores, v => Assert.Equal("020", v.Codigo)); // sem o 220
        Assert.Equal(Linhas(SoBandas(_simulador.EstadoMarco1())), Linhas(cinema));
        Assert.Equal("+1.0", cinema.Valores.Single(v => v.Indice == 4).NoFio);   // 160 Hz
        Assert.Equal("-2.0", cinema.Valores.Single(v => v.Indice == 12).NoFio);  // 6,3 kHz
        Assert.Equal(Enumerable.Range(0, 15).Select(i => (int?)i), cinema.Valores.Select(v => v.Indice));
    }

    [Fact]
    public void A_leitura_de_18_10_inteira_fica_com_os_84_valores_para_o_simulador_e_a_paridade()
    {
        Assert.Equal(Catalogo.TotalMarco1, ConhecimentoHerdado.LeituraFilme.Count);
        Assert.Equal(Linhas(_simulador.EstadoMarco1()), Linhas(ConhecimentoHerdado.LeituraFilme));
    }

    [Fact]
    public async Task So_os_proprios_vao_para_o_arquivo()
    {
        await Conectar();
        await Mudar3();
        await Criar("NOITE");

        Assert.Equal(["NOITE"], _presets.Presets.Select(p => p.Nome));
        var guardados = GerentePresets.DeJson(_guardadoPresets.Texto!);
        Assert.Equal(["NOITE"], guardados.Select(p => p.Nome));
    }

    [Theory]
    [InlineData(1, "SPL", "máximo seguro")]
    [InlineData(2, "MUSICA ORIG", "precisa ser refeita")]
    [InlineData(3, "FILME", "definitiva")]
    public void As_notas_das_3_memorias_vem_pela_posicao(int numero, string nomeNoPrototipo, string trecho)
    {
        var nota = ConhecimentoHerdado.NotaMemoria(numero)!;

        Assert.Equal(numero, nota.Numero);
        Assert.Equal(nomeNoPrototipo, nota.NomeNoPrototipo);
        Assert.Contains(trecho, nota.Texto);
        Assert.DoesNotContain("preset herdado FILME", nota.Texto);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    [InlineData(-1)]
    public void Fora_das_3_memorias_nao_ha_nota(int numero) => Assert.Null(ConhecimentoHerdado.NotaMemoria(numero));

    [Fact]
    public void As_secoes_de_texto_estao_todas_em_portugues_e_os_limites_do_guardiao_sao_provisorios()
    {
        var secoes = ConhecimentoHerdado.Secoes;

        Assert.Equal(
            ["Protocolo", "O sistema do Denis", "Os equipamentos do Casa 2.1", "As 3 memórias do aparelho", "Memórias e presets do celular", "Presets", "Preferências e método", "Cuidados com o app da Taramps", "Situação registrada"],
            secoes.Select(s => s.Titulo));
        Assert.All(secoes, s => Assert.NotEmpty(s.Linhas));
        var presets = string.Join(' ', secoes.Single(s => s.Titulo == "Presets").Linhas);
        Assert.Contains("15 bandas", presets);
        Assert.Contains("Não há preset herdado", presets);
        Assert.DoesNotContain("Cinema", presets);
        Assert.Contains("DIALOGO", presets);
        Assert.Contains("NOITE", presets);
        Assert.Contains("cifrados", presets);
        Assert.DoesNotContain("preset FILME", string.Join(' ', secoes.SelectMany(s => s.Linhas)));
        var sistema = string.Join(' ', secoes.Single(s => s.Titulo == "O sistema do Denis").Linhas);
        Assert.Contains("provisórios", sistema);
        Assert.Contains("quem decide os valores é o Denis", sistema);
        var cuidados = string.Join(' ', secoes.Single(s => s.Titulo == "Cuidados com o app da Taramps").Linhas);
        Assert.Contains("Deslizar uma memória", cuidados);
        Assert.Contains("Limiter", cuidados);
    }

    // ----------------------------------------------------------------- criar

    [Fact]
    public async Task Criar_NOITE_guarda_so_as_15_bandas_atuais_em_presets_json_formato_3_e_nada_vai_ao_aparelho()
    {
        await Conectar();
        await Mudar3();
        var inicio = _simulador.Escritas.Count;

        var r = await _presets.CriarAsync("NOITE");

        Assert.True(r.Ok, r.Mensagem);
        Assert.Equal("Preset NOITE criado.", r.Mensagem);
        var preset = r.Preset!;
        Assert.Equal("NOITE", preset.Nome);
        Assert.False(preset.Herdado);
        Assert.Equal(15, preset.Valores.Count);
        Assert.Equal(Linhas(SoBandas(_simulador.EstadoMarco1())), Linhas(preset));
        Assert.Equal(_relogio.GetLocalNow(), preset.Quando);
        Assert.Equal(inicio, _simulador.Escritas.Count); // nem escrita, nem leitura nova
        Assert.Equal(1, _guardadoPresets.Gravacoes);

        // O arquivo: formato 3, só o preset próprio, com as 15 bandas.
        using var doc = JsonDocument.Parse(_guardadoPresets.Texto!);
        Assert.Equal(3, doc.RootElement.GetProperty("formato").GetInt32());
        var item = Assert.Single(doc.RootElement.GetProperty("presets").EnumerateArray());
        Assert.Equal(preset.Id, item.GetProperty("id").GetString());
        Assert.Equal("NOITE", item.GetProperty("nome").GetString());
        Assert.Equal(15, item.GetProperty("valores").GetArrayLength());
        Assert.Equal([preset], _presets.Proprios);
    }

    [Fact]
    public async Task Criar_com_as_bandas_dadas_guarda_so_elas_e_descarta_o_resto()
    {
        await _presets.Carregada;
        IReadOnlyList<ValorCopiado> bandas = [.. GerentePresets.BandasPlanas, new ValorCopiado("220", null, "1"), new ValorCopiado("081", 0, "+1.0")];

        var r = await _presets.CriarAsync("PLANO", bandas);

        Assert.True(r.Ok, r.Mensagem);
        Assert.Equal(GerentePresets.BandasPlanas, r.Preset!.Valores);
    }

    [Fact]
    public async Task Criar_com_bandas_que_faltam_nada_e_guardado()
    {
        await _presets.Carregada;

        var r = await _presets.CriarAsync("POUCO", GerentePresets.BandasPlanas.Take(14).ToArray());

        Assert.False(r.Ok);
        Assert.Equal("A leitura está incompleta (1 banda sem valor).", r.Motivo);
        Assert.Equal(0, _guardadoPresets.Gravacoes);
    }

    [Fact]
    public async Task Criar_sem_precisar_de_chave_e_com_o_nome_do_jeito_que_foi_digitado()
    {
        await Conectar();
        foreach (var chave in Enum.GetValues<Chave>().Where(c => c != Chave.ControlePelaRede))
            await _chaves.DefinirAsync(chave, false, OrigemComando.Tela);

        var r = await _presets.CriarAsync("  Música_1-b  ");

        Assert.True(r.Ok, r.Mensagem);
        Assert.Equal("Música_1-b", r.Preset!.Nome); // sem os espaços das pontas, do jeito que foi digitado
    }

    [Fact]
    public async Task Cada_preset_tem_um_id_unico_e_estavel_que_sobrevive_a_reabrir_o_app()
    {
        await Conectar();
        var a = await Criar("A");
        await Mudar3();
        var b = await Criar("B");
        Assert.NotEqual(a.Id, b.Id);

        var reaberto = NovoGerente(new TextoMemoria(_guardadoPresets.Texto));
        await reaberto.Carregada;

        Assert.Equal([a.Id, b.Id], reaberto.Proprios.Select(p => p.Id));
        Assert.Equal(Linhas(a), Linhas(reaberto.Proprios[0]));
        Assert.Equal(Linhas(b), Linhas(reaberto.Proprios[1]));
        Assert.Equal(a.Quando, reaberto.Proprios[0].Quando);
        Assert.Equal(["A", "B"], reaberto.Presets.Select(p => p.Nome));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ABCDEFGHIJKLMNOPQRSTUVWXY")] // 25 caracteres
    [InlineData("a#b")]
    [InlineData("a*b")]
    [InlineData("nome\tcom tab")]
    [InlineData(null)]
    public async Task Nome_invalido_nada_e_guardado(string? nome)
    {
        await Conectar();

        var r = await _presets.CriarAsync(nome);

        Assert.False(r.Ok);
        Assert.Equal("Use de 1 a 24 letras, números, espaço, _ ou -", r.Motivo);
        Assert.Empty(_presets.Proprios);
        Assert.Equal(0, _guardadoPresets.Gravacoes);
    }

    [Fact]
    public async Task Nome_com_acento_decomposto_vira_FormC_e_e_aceito()
    {
        await Conectar();
        var decomposto = "Café"; // e + acento combinante, como vem de texto colado
        Assert.Equal(5, decomposto.Length);

        var r = await _presets.CriarAsync(decomposto);

        Assert.True(r.Ok, r.Mensagem);
        Assert.Equal("Café", r.Preset!.Nome);
        Assert.Equal(4, r.Preset.Nome.Length); // guardado em FormC
        Assert.Equal("Café", GerentePresets.DeJson(_guardadoPresets.Texto!).Single().Nome);
        Assert.Equal("Já existe um preset com esse nome.", (await _presets.CriarAsync("CAFÉ")).Motivo);
    }

    [Theory]
    [InlineData("ABCDEFGHIJKLMNOPQRSTUVWX")] // 24 caracteres: o limite
    [InlineData("A")]
    [InlineData("Ação 2_x-y")]
    public async Task Nome_dentro_da_regra_vale(string nome)
    {
        await Conectar();

        var r = await _presets.CriarAsync(nome);

        Assert.True(r.Ok, r.Mensagem);
        Assert.Equal(nome, r.Preset!.Nome);
    }

    [Theory]
    [InlineData("noite")]
    [InlineData(" NOITE ")]
    [InlineData("noite ")]
    public async Task Nome_repetido_de_um_proprio_nada_e_guardado(string repetido)
    {
        await Conectar();
        await Criar("NOITE");
        var gravacoes = _guardadoPresets.Gravacoes;

        var r = await _presets.CriarAsync(repetido);

        Assert.False(r.Ok);
        Assert.Equal("Já existe um preset com esse nome.", r.Motivo);
        Assert.Equal(["NOITE"], _presets.Proprios.Select(p => p.Nome));
        Assert.Equal(gravacoes, _guardadoPresets.Gravacoes);
    }

    [Fact]
    public async Task Conferir_nome_novo_diz_o_motivo_sem_guardar()
    {
        await Conectar();
        await Criar("ROCK");

        Assert.Equal(("CINEMA 2", (string?)null), _presets.ConferirNomeNovo(" CINEMA 2 "));
        Assert.Equal(GerentePresets.MsgNomeRepetido, _presets.ConferirNomeNovo("rock").Motivo);
        Assert.Equal(GerentePresets.MsgNomeInvalido, _presets.ConferirNomeNovo("a#b").Motivo);
    }

    [Fact]
    public async Task Leitura_incompleta_com_uma_banda_faltando_nada_e_guardado()
    {
        _simulador.Silenciar.Add(Catalogo.ConsultasMarco1.First(c => c is { Parametro.Codigo: "020", Indice: 4 }).Texto);
        await Conectar();
        Assert.Null(_estado.Valor("020", 4));

        var r = await _presets.CriarAsync("NOITE");

        Assert.False(r.Ok);
        Assert.Equal("A leitura está incompleta (1 banda sem valor).", r.Motivo);
        Assert.Empty(_presets.Proprios);
        Assert.Equal(0, _guardadoPresets.Gravacoes);
    }

    [Fact]
    public async Task Um_ganho_sem_leitura_nao_impede_criar_o_preset_das_bandas()
    {
        _simulador.Silenciar.Add("#081#0#"); // o aparelho não responde a este ajuste, que não é banda
        await Conectar();

        var r = await _presets.CriarAsync("NOITE");

        Assert.True(r.Ok, r.Mensagem);
    }

    [Fact]
    public async Task Criar_sem_leitura_diz_que_o_app_ainda_nao_leu()
    {
        var r = await _presets.CriarAsync("NOITE");

        Assert.False(r.Ok);
        Assert.Equal(Escritor.MsgSemLeitura, r.Motivo);
        Assert.Empty(_presets.Proprios);
        Assert.Empty(_simulador.Escritas);
    }

    [Fact]
    public async Task Criar_durante_a_leitura_diz_que_o_app_esta_lendo()
    {
        await Conectar();
        _simulador.AtrasoLeitura = TimeSpan.FromMilliseconds(10); // a leitura anda no relógio falso
        var lendo = _leitura.LerNovaAsync();
        await AndarAte(() => _estado.Lendo);

        var r = await _presets.CriarAsync("NOITE");
        await Rodar(lendo);

        Assert.False(r.Ok);
        Assert.Equal(Escritor.MsgLendo, r.Motivo);
        Assert.Empty(_presets.Proprios);
    }

    [Fact]
    public async Task Falha_ao_gravar_a_lista_nao_muda()
    {
        await Conectar();
        await Criar("A");
        _guardadoPresets.FalharAoGravar = true;
        var tentativas = _guardadoPresets.Tentativas;

        var r = await _presets.CriarAsync("B");

        Assert.False(r.Ok);
        Assert.Equal("Não consegui guardar o preset no celular.", r.Motivo);
        Assert.Equal(["A"], _presets.Proprios.Select(p => p.Nome));
        Assert.Equal(tentativas + 2, _guardadoPresets.Tentativas); // duas tentativas
        Assert.Equal("Não consegui guardar o preset no celular.", _presets.Aviso);
    }

    [Theory]
    [InlineData("{ isto não é json")]
    [InlineData("")]
    [InlineData("[]")]
    [InlineData("{\"formato\":3,\"presets\":[{\"id\":\"x\",\"nome\":\"X\",\"valores\":[{\"codigo\":\"081\",\"indice\":0,\"noFio\":\"+1.0\"}]}]}")]
    [InlineData("{\"formato\":4,\"presets\":[]}")]
    [InlineData("{\"formato\":\"um\",\"presets\":[]}")]
    [InlineData("{\"formato\":3,\"presets\":{}}")]
    public async Task Arquivo_estragado_ou_de_outro_formato_nenhum_preset_e_criar_funciona(string conteudo)
    {
        var estragado = new TextoMemoria(conteudo);
        _presets = NovoGerente(estragado);
        await _presets.Carregada;

        Assert.Empty(_presets.Proprios);
        Assert.Empty(_presets.Presets);

        await Conectar();
        var r = await _presets.CriarAsync("NOVO");
        Assert.True(r.Ok, r.Mensagem);
        Assert.Equal(["NOVO"], _presets.Presets.Select(p => p.Nome));
        Assert.Equal(["NOVO"], GerentePresets.DeJson(estragado.Texto!).Select(p => p.Nome));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task Os_formatos_1_e_2_comecam_do_zero_e_o_arquivo_e_regravado_no_formato_3(int formato)
    {
        var bandas = string.Join(",", CenarioFilme.Bandas.Select(v => $"{{\"codigo\":\"020\",\"indice\":{v.Indice},\"noFio\":\"{v.NoFio}\"}}"));
        var antigo = $$"""
            {"formato":{{formato}},"presets":[
              {"id":"p1","nome":"NOITE","quando":"2026-10-17T21:00:00-03:00","valores":[{{bandas}}]},
              {"id":"p2","nome":"DIALOGO","valores":[{{bandas}}]}
            ]}
            """;
        var repo = new TextoMemoria(antigo);
        _presets = NovoGerente(repo);
        await _presets.Carregada;

        Assert.Empty(_presets.Proprios);
        Assert.Equal(1, repo.Gravacoes);
        Assert.Equal(GerentePresets.Formato, GerentePresets.FormatoDe(repo.Texto!));
        Assert.Empty(GerentePresets.DeJson(repo.Texto!));
    }

    [Fact]
    public void Entrada_ruim_no_arquivo_e_descartada_e_as_boas_ficam()
    {
        var valores = GerentePresets.ParaJson([new Preset("x", "X", DateTimeOffset.UnixEpoch, GerentePresets.BandasPlanas)]);
        using var doc = JsonDocument.Parse(valores);
        var bandas = doc.RootElement.GetProperty("presets")[0].GetProperty("valores").GetRawText();
        var texto = $$"""
            {"formato":3,"presets":[
              {"id":"boa","nome":"BOA","valores":{{bandas}}},
              {"nome":"SEM ID","valores":{{bandas}}},
              {"id":"p2","nome":"nome#ruim","valores":{{bandas}}},
              {"id":"p3","nome":"SEM VALORES","valores":[]},
              {"id":"boa","nome":"REPETIDA","valores":{{bandas}}},
              {"id":"p4","nome":"boa","valores":{{bandas}}},
              "lixo"
            ]}
            """;

        var lidos = GerentePresets.DeJson(texto);

        Assert.Equal(["BOA"], lidos.Select(p => p.Nome));
    }

    // ----------------------------------------------------------------- atualizar

    [Fact]
    public async Task Atualizar_um_proprio_troca_as_bandas_mantem_o_id_e_o_nome_e_nada_vai_ao_aparelho()
    {
        await Conectar();
        var rock = await Criar("Rock");
        var antes = await Mudar3();
        var inicio = _simulador.Escritas.Count;

        var r = await _presets.AtualizarAsync(rock.Id);

        Assert.True(r.Ok, r.Mensagem);
        Assert.Equal("Preset Rock atualizado.", r.Mensagem);
        var novo = _presets.Proprios.Single();
        Assert.Equal((rock.Id, "Rock"), (novo.Id, novo.Nome));
        Assert.Equal(Linhas(SoBandas(antes)), Linhas(novo));
        Assert.Equal(Linhas(novo), Linhas(GerentePresets.DeJson(_guardadoPresets.Texto!).Single()));
        Assert.Equal(inicio, _simulador.Escritas.Count);
    }

    [Fact]
    public async Task Atualizar_um_que_nao_existe_e_recusado()
    {
        await Conectar();
        await Mudar3();

        Assert.Equal("O preset não existe mais.", (await _presets.AtualizarAsync("nao-existe")).Motivo);
        Assert.Equal(0, _guardadoPresets.Gravacoes);
    }

    // ----------------------------------------------------------------- renomear e apagar

    [Fact]
    public async Task Renomear_um_proprio_muda_o_nome_e_o_id_continua_e_nada_vai_ao_aparelho()
    {
        await Conectar();
        var a = await Criar("A");
        var inicio = _simulador.Escritas.Count;

        var r = await _presets.RenomearAsync(a.Id, "  Sala 2 ");

        Assert.True(r.Ok, r.Mensagem);
        Assert.Equal("Sala 2", _presets.Proprios.Single().Nome);
        Assert.Equal(a.Id, _presets.Proprios.Single().Id);
        Assert.Equal(Linhas(a), Linhas(_presets.Proprios.Single())); // as bandas não mudam
        Assert.Equal("Sala 2", GerentePresets.DeJson(_guardadoPresets.Texto!).Single().Nome);
        Assert.Equal(inicio, _simulador.Escritas.Count);
    }

    [Fact]
    public async Task Renomear_so_a_caixa_do_proprio_nome_vale()
    {
        await Conectar();
        var a = await Criar("noite");

        var r = await _presets.RenomearAsync(a.Id, "NOITE");

        Assert.True(r.Ok, r.Mensagem);
        Assert.Equal("NOITE", _presets.Proprios.Single().Nome);
    }

    [Theory]
    [InlineData("a#b", "Use de 1 a 24 letras, números, espaço, _ ou -")]
    [InlineData("", "Use de 1 a 24 letras, números, espaço, _ ou -")]
    [InlineData("b", "Já existe um preset com esse nome.")]
    [InlineData(" B ", "Já existe um preset com esse nome.")]
    public async Task Renomear_com_nome_ruim_ou_repetido_nada_muda(string novo, string esperado)
    {
        await Conectar();
        var a = await Criar("A");
        await Criar("B");
        var gravacoes = _guardadoPresets.Gravacoes;

        var r = await _presets.RenomearAsync(a.Id, novo);

        Assert.False(r.Ok);
        Assert.Equal(esperado, r.Motivo);
        Assert.Equal(["A", "B"], _presets.Proprios.Select(p => p.Nome));
        Assert.Equal(gravacoes, _guardadoPresets.Gravacoes);
    }

    [Fact]
    public async Task Renomear_ou_apagar_um_que_nao_existe_diz_que_nao_existe_mais()
    {
        await Conectar();

        Assert.Equal("O preset não existe mais.", (await _presets.RenomearAsync("nao-existe", "X")).Motivo);
        Assert.Equal("O preset não existe mais.", (await _presets.ApagarAsync("nao-existe")).Motivo);
    }

    [Fact]
    public async Task Apagar_um_proprio_tira_da_lista_grava_e_nada_vai_ao_aparelho()
    {
        await Conectar();
        var a = await Criar("A");
        await Criar("B");
        var inicio = _simulador.Escritas.Count;

        var r = await _presets.ApagarAsync(a.Id);

        Assert.True(r.Ok, r.Mensagem);
        Assert.Equal(["B"], _presets.Proprios.Select(p => p.Nome));
        Assert.Equal(["B"], GerentePresets.DeJson(_guardadoPresets.Texto!).Select(p => p.Nome));
        Assert.Equal(inicio, _simulador.Escritas.Count);
    }

    [Theory]
    [InlineData(2, "Lembrado por 2 memórias.")]
    [InlineData(1, "Lembrado por 1 memória.")]
    public async Task Apagar_um_preset_lembrado_por_memorias_e_recusado_com_quantas(int quantas, string esperado)
    {
        await Conectar();
        var rock = await Criar("Rock");
        _presets.Lembrados = id => id == rock.Id ? quantas : 0;
        var gravacoes = _guardadoPresets.Gravacoes;

        var r = await _presets.ApagarAsync(rock.Id);

        Assert.False(r.Ok);
        Assert.Equal(esperado, r.Motivo);
        Assert.Equal(esperado, _presets.MotivoTravaApagar(rock.Id));
        Assert.Equal(["Rock"], _presets.Proprios.Select(p => p.Nome));
        Assert.Equal(gravacoes, _guardadoPresets.Gravacoes);

        _presets.Lembrados = _ => 0; // ninguém lembra mais
        Assert.Null(_presets.MotivoTravaApagar(rock.Id));
        Assert.True((await _presets.ApagarAsync(rock.Id)).Ok);
    }

    [Fact]
    public async Task Renomear_e_apagar_com_falha_ao_gravar_a_lista_nao_muda()
    {
        await Conectar();
        var a = await Criar("A");
        _guardadoPresets.FalharAoGravar = true;

        var renomear = await _presets.RenomearAsync(a.Id, "NOVO");
        var apagar = await _presets.ApagarAsync(a.Id);
        var atualizar = await _presets.AtualizarAsync(a.Id);

        Assert.Equal("Não consegui guardar o preset no celular.", renomear.Motivo);
        Assert.Equal("Não consegui guardar o preset no celular.", apagar.Motivo);
        Assert.Equal("Não consegui guardar o preset no celular.", atualizar.Motivo);
        Assert.Equal(["A"], _presets.Proprios.Select(p => p.Nome));
    }

    // ----------------------------------------------------------------- exclusividade

    [Fact]
    public async Task As_operacoes_do_gerente_sao_exclusivas_uma_em_andamento_recusa_as_outras()
    {
        await Conectar();
        var a = await Criar("A");
        Task<ResultadoPreset>? criar = null, renomear = null, apagar = null, aplicar = null, atualizar = null;
        // Enquanto o criar grava, as outras tentam entrar.
        _guardadoPresets.AoGravar = () =>
        {
            criar = _presets.CriarAsync("OUTRO");
            renomear = _presets.RenomearAsync(a.Id, "RENOMEADO");
            apagar = _presets.ApagarAsync(a.Id);
            aplicar = _presets.AplicarAsync(a.Id);
            atualizar = _presets.AtualizarAsync(a.Id);
        };

        var primeiro = await _presets.CriarAsync("B");

        Assert.True(primeiro.Ok, primeiro.Mensagem);
        foreach (var t in new[] { criar!, renomear!, apagar!, aplicar!, atualizar! })
        {
            var r = await t;
            Assert.False(r.Ok);
            Assert.Equal("Uma operação de preset já está em andamento.", r.Motivo);
        }
        Assert.Equal(["A", "B"], _presets.Proprios.Select(p => p.Nome));
        Assert.False(_presets.Ocupado);
    }

    // ----------------------------------------------------------------- exportar

    [Fact]
    public async Task Exportar_devolve_o_json_de_um_proprio_com_o_nome_e_as_15_bandas()
    {
        await Conectar();
        var a = await Criar("A");

        using var proprio = JsonDocument.Parse(_presets.ExportarJson(a.Id)!);

        Assert.Equal(3, proprio.RootElement.GetProperty("formato").GetInt32());
        Assert.Equal("A", proprio.RootElement.GetProperty("nome").GetString());
        Assert.False(proprio.RootElement.GetProperty("herdado").GetBoolean());
        Assert.False(proprio.RootElement.TryGetProperty("nota", out _));
        Assert.Equal(15, proprio.RootElement.GetProperty("valores").GetArrayLength());

        Assert.Null(_presets.ExportarJson("nao-existe"));
    }

    // ----------------------------------------------------------------- aplicar

    [Fact]
    public async Task Aplicar_escreve_so_as_bandas_diferentes_com_a_copia_antes_relê_e_o_resto_fica_igual()
    {
        await Conectar();
        var a = await Criar("A"); // as bandas do Cinema
        var antes = await Mudar3(); // o estado que a cópia antes deve guardar
        Assert.True((await Rodar(_escritor.EscreverAsync(Ganho, 0, -3.0m))).Ok); // e um ganho, que o preset não toca
        antes = _simulador.EstadoMarco1();
        var gravacoesCopia = _guardadoCopia.Gravacoes;
        var inicio = _simulador.Escritas.Count;
        var iniciadas = _leitura.Iniciadas;
        Preset? avisado = null;
        _presets.AoAplicar = p =>
        {
            avisado = p;
            return Task.CompletedTask;
        };

        var r = await Rodar(_presets.AplicarAsync(a.Id));

        Assert.True(r.Ok, r.Mensagem);
        Assert.Equal("Preset A aplicado.", r.Mensagem);
        Assert.Equal("Preset A aplicado.", _presets.Aviso);
        Assert.Same(a, avisado);
        // Só as 3 bandas diferentes foram escritas (cada uma uma vez), e depois a leitura completa nova.
        var audio = AudioDesde(inicio);
        Assert.Equal([Banda(0, 0.0m), Banda(4, 1.0m), Banda(10, 0.0m)], audio);
        Assert.Equal(iniciadas + 1, _leitura.Iniciadas);
        Assert.Equal(Catalogo.Consultas.Select(c => c.Texto), EscritasDesde(inicio).Where(Catalogo.EhLeitura));
        // O aparelho e o espelho: as bandas iguais ao preset; a calibração, o caráter e o resto iguais a antes.
        Assert.Equal(Linhas(a), Linhas(SoBandas(_simulador.EstadoMarco1())));
        Assert.Equal(Linhas(SemBandas(antes)), Linhas(SemBandas(_simulador.EstadoMarco1())));
        Assert.Equal("-3.0", _simulador.Valor("081", 0));
        foreach (var v in a.Valores)
            Assert.Equal(v.NoFio, _estado.Valor(v.Codigo, v.Indice)!.NoFio);
        // A cópia antes foi guardada antes de escrever (o estado de antes), e a marca saiu no fim.
        Assert.True(_guardadoCopia.Gravacoes > gravacoesCopia);
        var copia = CopiaAntes.DeJson(_guardadoCopia.Texto!)!;
        Assert.Null(copia.Operacao);
        Assert.Equal(Linhas(antes), Linhas(SoMarco1(copia.Valores)));
        Assert.Null(_copia.PelaMetade);
        Assert.Null(_copia.MotivoBloqueio());
        Assert.False(_presets.Ocupado);
    }

    [Fact]
    public async Task Aplicar_guarda_a_marca_do_aplicar_antes_da_primeira_escrita()
    {
        await Conectar();
        var a = await Criar("A");
        await Mudar3();
        var escritasNaMarca = -1;
        _guardadoCopia.AoGravar = () =>
        {
            // A cópia antes é gravada com a marca ainda sem nenhuma escrita de áudio do aplicar.
            escritasNaMarca = _simulador.Escritas.Count;
        };
        var inicio = _simulador.Escritas.Count;

        var tarefa = _presets.AplicarAsync(a.Id);
        await AndarAte(() => _copia.Ultima?.Operacao is not null);
        var vista = _copia.Ultima;
        await Rodar(tarefa);

        Assert.Equal(inicio, escritasNaMarca);
        Assert.Equal(new OperacaoPendente("aplicar-preset", a.Id, "Aplicar o preset A", "A"), vista!.Operacao);
    }

    [Fact]
    public async Task Ja_aplicado_nada_sai_nao_ha_copia_e_o_aviso_de_aplicado_vale()
    {
        await Conectar();
        var a = await Criar("NOITE");
        var inicio = _simulador.Escritas.Count;
        var avisos = 0;
        _presets.AoAplicar = _ =>
        {
            avisos++;
            return Task.CompletedTask;
        };

        var r = await _presets.AplicarAsync(a.Id);

        Assert.True(r.Ok, r.Mensagem);
        Assert.Equal("Preset NOITE já está aplicado.", r.Mensagem);
        Assert.Equal(1, avisos); // a memória em uso passa a lembrá-lo mesmo assim
        Assert.Equal(inicio, _simulador.Escritas.Count);
        Assert.Equal(0, _guardadoCopia.Gravacoes);
        Assert.Null(_copia.Ultima);
        Assert.False(_copia.EmAndamento);
    }

    [Fact]
    public async Task Aplicar_com_a_chave_de_ajustes_desligada_nada_sai_nem_a_copia_nem_o_aviso()
    {
        await Conectar();
        var a = await Criar("A");
        await Mudar3();
        await _chaves.DefinirAsync(Chave.AjustesAudio, false, OrigemComando.Tela);
        var inicio = _simulador.Escritas.Count;
        var avisos = 0;
        _presets.AoAplicar = _ =>
        {
            avisos++;
            return Task.CompletedTask;
        };

        var r = await _presets.AplicarAsync(a.Id);

        Assert.False(r.Ok);
        Assert.Equal(Sessao.MotivoChaveDesligada, r.Motivo);
        Assert.Equal(0, avisos);
        Assert.Equal(inicio, _simulador.Escritas.Count);
        Assert.Equal(0, _guardadoCopia.Gravacoes);
        Assert.Contains(_log.Linhas, l => l.Contains($"✗ {Banda(0, 0.0m)}") && l.Contains(Sessao.MotivoChaveDesligada));
        Assert.Null(_copia.MotivoBloqueio());
    }

    [Fact]
    public async Task Aplicar_em_modo_so_leitura_nada_sai()
    {
        _simulador.Firmware = "R004";
        await Conectar();
        Assert.True(_sessao.SoLeitura);
        var a = await Criar("A"); // criar vale: não manda nada
        _estado.Atualizar(new Endereco("020", 0), Eq.Decodificar("+3.0").Valor!); // o espelho difere do preset
        var inicio = _simulador.Escritas.Count;

        var r = await _presets.AplicarAsync(a.Id);

        Assert.False(r.Ok);
        Assert.Equal(Sessao.MotivoSoLeitura, r.Motivo);
        Assert.Equal(inicio, _simulador.Escritas.Count);
        Assert.Equal(0, _guardadoCopia.Gravacoes);
    }

    [Fact]
    public async Task Aplicar_o_Cinema_depois_de_mexer_nas_bandas_volta_as_15_bandas_de_18_10()
    {
        await Conectar();
        var cinema = await CriarCinema();
        await Mudar3();
        Assert.NotEqual(Linhas(CenarioFilme.Bandas), Linhas(SoBandas(_simulador.EstadoMarco1())));

        var r = await Rodar(_presets.AplicarAsync(cinema.Id));

        Assert.True(r.Ok, r.Mensagem);
        Assert.Equal("Preset Cinema aplicado.", r.Mensagem);
        Assert.Equal(Linhas(_simulador.EstadoMarco1()), Linhas(ConhecimentoHerdado.LeituraFilme)); // o aparelho volta à leitura inteira
        Assert.Null(_copia.PelaMetade);
    }

    [Fact]
    public async Task Aplicar_sem_leitura_ou_preset_que_nao_existe_nada_sai()
    {
        var cinema = await CriarCinema();
        var semLeitura = await _presets.AplicarAsync(cinema.Id);
        Assert.Equal(Escritor.MsgSemLeitura, semLeitura.Motivo);

        await Conectar();
        var inexistente = await _presets.AplicarAsync("nao-existe");

        Assert.Equal("O preset não existe mais.", inexistente.Motivo);
        Assert.Equal(0, _guardadoCopia.Gravacoes);
        Assert.Empty(AudioDesde(0));
    }

    [Fact]
    public async Task Aplicar_com_banda_do_preset_ilegivel_ou_ausente_nada_sai()
    {
        await Conectar();
        var valores = GerentePresets.BandasPlanas.ToList();
        valores[0] = new ValorCopiado("020", 0, "lixo");
        valores.RemoveAt(14); // e uma banda que o preset não traz
        var estragado = new Preset("estragado", "ESTRAGADO", DateTimeOffset.UnixEpoch, valores);
        _presets = NovoGerente(new TextoMemoria(GerentePresets.ParaJson([estragado])));
        await _presets.Carregada;
        var inicio = _simulador.Escritas.Count;

        var r = await _presets.AplicarAsync("estragado");

        Assert.False(r.Ok);
        Assert.Equal(CopiaAntes.MsgCopiaIlegivel(2), r.Motivo);
        Assert.Equal(inicio, _simulador.Escritas.Count);
        Assert.Equal(0, _guardadoCopia.Gravacoes);
    }

    [Fact]
    public async Task Aplicar_sem_conseguir_guardar_a_copia_nada_sai()
    {
        await Conectar();
        var a = await Criar("A");
        await Mudar3();
        _guardadoCopia.FalharAoGravar = true;
        var inicio = _simulador.Escritas.Count;

        var r = await _presets.AplicarAsync(a.Id);

        Assert.False(r.Ok);
        Assert.Equal(CopiaAntes.MsgNaoGuardou, r.Motivo);
        Assert.Empty(AudioDesde(inicio));
        Assert.Null(_copia.PelaMetade);
    }

    [Fact]
    public async Task Aplicar_o_aparelho_ajusta_uma_banda_na_releitura_conclui_e_diz_quantas_ficaram_diferentes()
    {
        await Conectar();
        var a = await Criar("A");
        // O aparelho tem uma banda diferente do que o espelho mostra; o app não sabe até reler.
        _simulador.Definir("020", 3, "+1.0");
        Assert.True((await Rodar(_escritor.EscreverAsync(Eq, 0, 3.0m))).Ok);

        var r = await Rodar(_presets.AplicarAsync(a.Id));

        Assert.True(r.Ok, r.Mensagem);
        Assert.Equal("Preset A aplicado, mas 1 banda ficou diferente.", r.Mensagem);
        Assert.Equal("+1.0", _simulador.Valor("020", 3));
        Assert.Equal("+0.0", _simulador.Valor("020", 0));
        Assert.Null(_copia.PelaMetade); // concluiu: a marca saiu
        Assert.Null(CopiaAntes.DeJson(_guardadoCopia.Texto!)!.Operacao);
    }

    // ----------------------------------------------------------------- aplicado pela metade

    /// <summary>
    /// Cria "A", muda 3 bandas, aplica "A" e derruba a conexão depois da 2ª escrita (que o aparelho
    /// aplica sem o app saber); reconecta e espera o "aplicado pela metade". Devolve o preset e o
    /// estado de antes de aplicar.
    /// </summary>
    private async Task<(Preset Preset, IReadOnlyDictionary<(string Codigo, int? Indice), string> Antes)> AplicarComQueda()
    {
        await Conectar();
        var a = await Criar("A");
        var antes = await Mudar3();
        _simulador.CairDepoisDaEscrita = _simulador.Escritas.Count + 2;

        var r = await Rodar(_presets.AplicarAsync(a.Id));

        Assert.False(r.Ok);
        Assert.StartsWith("2 bandas não foram escritas:", r.Motivo);
        Assert.Equal(EstadoSessao.Desconectado, _sessao.Estado);
        Assert.Null(_copia.PelaMetade); // só depois da leitura da conexão nova
        Assert.NotNull(CopiaAntes.DeJson(_guardadoCopia.Texto!)!.Operacao); // a marca fica
        Assert.Equal("+1.0", _simulador.Valor("020", 4)); // o aparelho aplicou a 2ª sem o app saber

        await Conectar();
        await Esperar(() => _copia.PelaMetade is not null);
        return (a, antes);
    }

    [Fact]
    public async Task Queda_ao_aplicar_ao_reconectar_e_ler_fica_aplicado_pela_metade()
    {
        var (a, _) = await AplicarComQueda();

        var op = _copia.PelaMetade!;
        Assert.Equal("aplicar-preset", op.Tipo);
        Assert.Equal(a.Id, op.Alvo);
        Assert.Equal("Aplicar o preset A", op.Descricao);
        Assert.Equal("A", op.Nome);
    }

    [Fact]
    public async Task Pela_metade_o_resto_e_recusado_e_so_o_Terminar_ou_o_Voltar_escrevem()
    {
        var (a, _) = await AplicarComQueda();
        var inicio = _simulador.Escritas.Count;

        var escrita = await Rodar(_escritor.EscreverAsync(Ganho, 0, -1.0m));
        var aplicar = await Rodar(_presets.AplicarAsync(a.Id));

        Assert.Equal(CopiaAntes.MsgPelaMetadePendente, escrita.Motivo);
        Assert.Equal(CopiaAntes.MsgPelaMetadePendente, aplicar.Motivo);
        Assert.Equal(inicio, _simulador.Escritas.Count);
    }

    [Fact]
    public async Task Terminar_escreve_so_o_que_falta_com_a_mesma_copia_relê_e_conclui()
    {
        var (a, antes) = await AplicarComQueda();
        var quandoDaCopia = _copia.Ultima!.Quando;
        var valoresDaCopia = Linhas(_copia.Ultima.Valores);
        var inicio = _simulador.Escritas.Count;

        var r = await Rodar(_copia.TerminarAsync());

        Assert.True(r.Ok, r.Motivo);
        Assert.Equal(Banda(10, 0.0m), Assert.Single(AudioDesde(inicio))); // só o que falta: a 3ª banda
        Assert.Equal("Preset A aplicado.", _presets.Aviso);
        Assert.Equal(Linhas(a), Linhas(SoBandas(_simulador.EstadoMarco1())));
        Assert.Null(_copia.PelaMetade);
        var copia = CopiaAntes.DeJson(_guardadoCopia.Texto!)!;
        Assert.Null(copia.Operacao);
        // A mesma cópia: a de antes de começar a aplicar, não a de depois da queda.
        Assert.Equal(quandoDaCopia, copia.Quando);
        Assert.Equal(valoresDaCopia, Linhas(copia.Valores));
        Assert.Equal(Linhas(antes), Linhas(SoMarco1(copia.Valores)));
    }

    [Fact]
    public async Task Terminar_sem_o_preset_diz_que_nao_existe_mais_e_continua_pela_metade()
    {
        var (a, _) = await AplicarComQueda();
        Assert.True((await _presets.ApagarAsync(a.Id)).Ok);
        var inicio = _simulador.Escritas.Count;

        var r = await Rodar(_copia.TerminarAsync());

        Assert.False(r.Ok);
        Assert.Equal("O preset não existe mais; use Voltar à cópia.", r.Motivo);
        Assert.Equal(inicio, _simulador.Escritas.Count);
        Assert.NotNull(_copia.PelaMetade);
        Assert.NotNull(CopiaAntes.DeJson(_guardadoCopia.Texto!)!.Operacao);
        Assert.Equal("O preset não existe mais; use Voltar à cópia.", _presets.Aviso);
    }

    [Fact]
    public async Task Voltar_a_copia_as_bandas_voltam_ao_retrato_de_antes_de_aplicar()
    {
        var (_, antes) = await AplicarComQueda();
        var inicio = _simulador.Escritas.Count;

        var r = await Rodar(_copia.VoltarAsync());

        Assert.True(r.Ok, r.Motivo);
        Assert.Equal(Linhas(antes), Linhas(_simulador.EstadoMarco1()));
        Assert.Equal([Banda(0, 3.0m), Banda(4, -2.0m)], AudioDesde(inicio)); // só as que o aplicar tinha mudado
        Assert.Null(_copia.PelaMetade);
        Assert.Null(CopiaAntes.DeJson(_guardadoCopia.Texto!)!.Operacao);
    }

    [Fact]
    public async Task Falha_numa_escrita_com_a_sessao_aberta_fica_pela_metade_na_hora_e_o_Terminar_acaba()
    {
        await Conectar();
        var a = await Criar("A");
        await Mudar3();
        // O aparelho ecoa outro valor para a 1ª escrita (e fica com o pedido; o espelho fica com o eco).
        _simulador.EcosDiferentes[Banda(0, 0.0m)] = "020#0#+9.0";
        var inicio = _simulador.Escritas.Count;

        var r = await Rodar(_presets.AplicarAsync(a.Id));

        Assert.False(r.Ok);
        Assert.Equal("1 banda não foi escrita: o aparelho ficou em +9.0", r.Motivo);
        Assert.Equal(EstadoSessao.Conectado, _sessao.Estado);
        Assert.NotNull(_copia.PelaMetade); // já, sem esperar outra conexão
        Assert.Equal(GerentePresets.TipoAplicar, _copia.PelaMetade!.Tipo);
        Assert.Equal(3, AudioDesde(inicio).Count); // as outras duas escritas saíram mesmo assim
        Assert.False(_presets.Ocupado);

        _simulador.EcosDiferentes.Clear();
        var terminar = await Rodar(_copia.TerminarAsync());

        Assert.True(terminar.Ok, terminar.Motivo);
        Assert.Equal(Linhas(a), Linhas(SoBandas(_simulador.EstadoMarco1())));
        Assert.Null(_copia.PelaMetade);
    }

    [Fact]
    public async Task Leitura_que_para_depois_de_escrever_tudo_fica_pela_metade_com_a_sessao_aberta()
    {
        await Conectar();
        var a = await Criar("A");
        await Mudar3();
        foreach (var c in Catalogo.Consultas)
            _simulador.Silenciar.Add(c.Texto); // depois das escritas, o aparelho não responde às leituras

        var r = await Rodar(_presets.AplicarAsync(a.Id));

        Assert.False(r.Ok);
        Assert.Equal(CopiaAntes.MsgLeituraParou, r.Motivo);
        Assert.Equal(EstadoSessao.Conectado, _sessao.Estado);
        Assert.NotNull(_copia.PelaMetade);
        Assert.Equal(GerentePresets.TipoAplicar, _copia.PelaMetade!.Tipo);
    }

    [Fact]
    public async Task Queda_na_releitura_depois_de_escrever_tudo_a_marca_espera_a_proxima_conexao()
    {
        await Conectar();
        var a = await Criar("A");
        await Mudar3();
        // Cai no 1º pedido de leitura depois das 3 escritas de áudio.
        _simulador.CairNaEscrita = _simulador.Escritas.Count + 4;

        var r = await Rodar(_presets.AplicarAsync(a.Id));

        Assert.False(r.Ok);
        Assert.Equal(GerentePresets.MsgCaiuAplicar, r.Motivo);
        Assert.Equal(EstadoSessao.Desconectado, _sessao.Estado);
        Assert.Null(_copia.PelaMetade);
        Assert.NotNull(CopiaAntes.DeJson(_guardadoCopia.Texto!)!.Operacao);

        await Conectar();
        await Esperar(() => _copia.PelaMetade is not null);
        Assert.Equal("Aplicar o preset A", _copia.PelaMetade!.Descricao);
    }

    [Fact]
    public async Task Terminar_com_o_estado_ja_igual_ao_preset_conclui_sem_escrever()
    {
        var (a, _) = await AplicarComQueda();
        // Põe o aparelho (e o espelho) iguais ao preset por fora, na última banda que faltava.
        var banda = a.Valores.Single(v => v is { Codigo: "020", Indice: 10 });
        _simulador.Definir("020", 10, banda.NoFio);
        _estado.Atualizar(new Endereco("020", 10), Eq.Decodificar(banda.NoFio).Valor!);
        var inicio = _simulador.Escritas.Count;

        var r = await Rodar(_copia.TerminarAsync());

        Assert.True(r.Ok, r.Motivo);
        Assert.Empty(AudioDesde(inicio));
        Assert.Null(_copia.PelaMetade);
        Assert.Null(CopiaAntes.DeJson(_guardadoCopia.Texto!)!.Operacao);
    }

    [Fact]
    public async Task Aplicar_durante_outra_operacao_e_recusado_e_nenhuma_escrita_de_audio_sai()
    {
        await Conectar();
        var cinema = await CriarCinema();
        await Mudar3(); // o espelho difere do Cinema: haveria o que escrever
        // Outra operação de vários parâmetros anda (guardou a cópia e a marca e ainda não terminou).
        Assert.True((await _copia.IniciarAsync(new OperacaoPendente("teste", "x", "Operação de teste"))).Ok);
        var inicio = _simulador.Escritas.Count;

        var r = await _presets.AplicarAsync(cinema.Id);

        Assert.False(r.Ok);
        Assert.Equal(CopiaAntes.MsgOutraOperacao, r.Motivo);
        Assert.Empty(AudioDesde(inicio));
        Assert.Equal(inicio, _simulador.Escritas.Count);
        await _copia.ConcluirAsync();
    }

    [Fact]
    public async Task Aplicar_sobre_a_marca_de_um_aplicar_que_nao_terminou_e_recusado_ate_escolher()
    {
        // Uma marca guardada de uma sessão anterior: o app reabre e lê.
        var antigo = new Copia(DateTimeOffset.UnixEpoch, [new ValorCopiado("020", 0, "+0.0")],
            new OperacaoPendente(GerentePresets.TipoAplicar, "id-que-nao-existe", "Aplicar o preset ANTIGO", "ANTIGO"));
        var reaberta = new CopiaAntes(_sessao, _estado, _escritor, _leitura, new TextoMemoria(CopiaAntes.ParaJson(antigo)), _log, _relogio);
        var gerente = new GerentePresets(_sessao, _estado, _escritor, _leitura, reaberta, new TextoMemoria(), _log, _relogio);
        await reaberta.Carregada;
        await _chaves.Carregada;

        await _sessao.ConectarAsync();
        await Esperar(() => reaberta.PelaMetade is not null);

        Assert.Equal("Aplicar o preset ANTIGO", reaberta.PelaMetade!.Descricao);
        var terminar = await Rodar(reaberta.TerminarAsync());
        Assert.Equal("O preset não existe mais; use Voltar à cópia.", terminar.Motivo);
        var cinema = await gerente.CriarAsync("Cinema", CenarioFilme.Bandas);
        Assert.True(cinema.Ok, cinema.Mensagem);
        var aplicar = await gerente.AplicarAsync(cinema.Preset!.Id);
        Assert.Equal(CopiaAntes.MsgPelaMetadePendente, aplicar.Motivo);
    }

    [Fact]
    public void Sem_herdados_um_proprio_chamado_Cinema_e_lido_e_um_nome_repetido_sai()
    {
        var json = GerentePresets.ParaJson([
            new Preset("p1", "Cinema", DateTimeOffset.UnixEpoch, GerentePresets.BandasPlanas),
            new Preset("p2", "CINEMA", DateTimeOffset.UnixEpoch, GerentePresets.BandasPlanas),
            new Preset("p3", "ROCK", DateTimeOffset.UnixEpoch, GerentePresets.BandasPlanas)]);

        Assert.Equal(["Cinema", "ROCK"], GerentePresets.DeJson(json).Select(p => p.Nome));
    }
}
