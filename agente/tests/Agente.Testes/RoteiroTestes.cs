using System.Text.Json.Nodes;
using Agente.ModeloFalso;
using Agente.Porta;
using Agente.Servico;

namespace Agente.Testes;

/// <summary>Uma porta de mentira para o executor: os pedidos ficam anotados e o /eventos devolve a fila dada.</summary>
internal sealed class FerramentaFalsa : IFerramenta
{
    private readonly List<JsonObject> _eventos = [];

    public List<JsonObject> Entradas { get; } = [];

    /// <summary>O /eventos não responde.</summary>
    public bool EventosFalham { get; set; }

    /// <summary>A resposta a um pedido (fora o /eventos); null = ok 200 com corpo vazio.</summary>
    public Func<JsonObject, RespostaFerramenta?> Responder { get; set; } = _ => null;

    /// <summary>Um evento como veio (o "n" é posto aqui).</summary>
    public void EventoCru(JsonObject e)
    {
        e["n"] = _eventos.Count + 1;
        _eventos.Add(e);
    }

    public void Evento(string tipo, JsonObject? campos = null)
    {
        var e = campos ?? [];
        e["n"] = _eventos.Count + 1;
        e["tipo"] = tipo;
        _eventos.Add(e);
    }

    public Task<RespostaFerramenta> RodarAsync(JsonObject entrada, CancellationToken cancelar)
    {
        var alvo = entrada["alvo"]!.GetValue<string>();
        if (alvo.StartsWith("/eventos?desde=", StringComparison.Ordinal))
        {
            if (EventosFalham)
                return Task.FromResult(RespostaFerramenta.Recusa("A porta não respondeu."));
            var desde = long.Parse(alvo["/eventos?desde=".Length..]);
            var lista = new JsonArray(_eventos.Where(e => e["n"]!.GetValue<int>() > desde).Select(e => (JsonNode)e.DeepClone()).ToArray());
            return Task.FromResult(RespostaFerramenta.Feito(200, new JsonObject { ["ok"] = true, ["ultimo"] = _eventos.Count, ["eventos"] = lista }));
        }
        Entradas.Add(entrada);
        return Task.FromResult(Responder(entrada) ?? RespostaFerramenta.Feito(200, new JsonObject { ["ok"] = true }));
    }
}

/// <summary>O formato do roteiro, o executor (pedir, esperar, ramificar) e a configuração do serviço.</summary>
public sealed class RoteiroTestes
{
    private static readonly TimeSpan Rapido = TimeSpan.FromMilliseconds(10);

    private static string[] Alvos(FerramentaFalsa f) => f.Entradas.Select(e => e["alvo"]!.GetValue<string>()).ToArray();

    [Fact]
    public void O_roteiro_basico_e_valido_e_so_tem_dados_genericos()
    {
        var r = Roteiro.Carregar(Caminhos.Roteiro("criacao-basica"));

        Assert.Equal("/criacao/abrir", r.Passos[0].Pedir!.Alvo);
        var perguntas = r.Passos.Where(p => p.Pedir?.Alvo == "/criacao/pergunta").ToArray();
        Assert.Equal(3, perguntas.Length); // a 1ª e as duas versões da 2ª (o ramo)
        Assert.False(perguntas[0].Pedir!.Corpo!["ultima"]!.GetValue<bool>());
        Assert.All(perguntas.Skip(1), p => Assert.True(p.Pedir!.Corpo!["ultima"]!.GetValue<bool>()));
        var ajustes = r.Passos.Where(p => p.Pedir?.Alvo == "/criacao/ajuste").ToArray();
        Assert.Equal(2, ajustes.Length);
        Assert.All(ajustes, a =>
        {
            Assert.False(string.IsNullOrWhiteSpace(a.Pedir!.Corpo!["frase"]?.GetValue<string>()));
            Assert.False(string.IsNullOrWhiteSpace(a.Pedir!.Corpo!["motivo"]?.GetValue<string>()));
        });
        Assert.Contains(r.Passos, p => p.Pedir?.Alvo == "/criacao/resumo");
        Assert.Contains(r.Passos, p => p.Pedir?.Alvo == "/criacao/como-ficou");
        Assert.Contains(r.Passos, p => p.Esperar?.Tipos.Contains("criacao-como-ficou") == true);
        Roteiro.Carregar(Caminhos.Roteiro("trava"));
    }

    [Fact]
    public async Task O_executor_ramifica_pela_resposta_recebida()
    {
        var roteiro = Roteiro.DeJson("""
            { "passos": [
              { "esperar": { "tipos": ["criacao-resposta"], "prazoS": 5 }, "se": [ { "campo": "texto", "igual": "B", "ir": "b" } ] },
              { "id": "a", "pedir": { "alvo": "/caminho-a" }, "ir": "fim" },
              { "id": "b", "pedir": { "alvo": "/caminho-b" } },
              { "id": "fim", "fim": true }
            ] }
            """);
        var f = new FerramentaFalsa();
        var executor = new ExecutorRoteiro(f, TextWriter.Null, Rapido);
        var rodando = executor.RodarAsync(roteiro);
        await Task.Delay(50);
        f.Evento("comando");
        f.Evento("criacao-resposta", new JsonObject { ["numero"] = 1, ["texto"] = "B", ["doOutro"] = false });

        Assert.Equal(ExecutorRoteiro.Terminou, await rodando.WaitAsync(TimeSpan.FromSeconds(10)));
        Assert.Equal(["/caminho-b"], Alvos(f));
    }

    [Fact]
    public async Task So_os_eventos_depois_do_inicio_contam()
    {
        var roteiro = Roteiro.DeJson("""
            { "passos": [ { "esperar": { "tipos": ["criacao-resposta"], "prazoS": 0.3 }, "seVencer": "vencido" },
                          { "pedir": { "alvo": "/chegou" }, "ir": "fim" },
                          { "id": "vencido", "pedir": { "alvo": "/vencido" } },
                          { "id": "fim", "fim": true } ] }
            """);
        var f = new FerramentaFalsa();
        f.Evento("criacao-resposta", new JsonObject { ["texto"] = "do passado" });

        Assert.Equal(ExecutorRoteiro.Terminou, await new ExecutorRoteiro(f, TextWriter.Null, Rapido).RodarAsync(roteiro));
        Assert.Equal(["/vencido"], Alvos(f));
    }

    [Fact]
    public async Task Um_pedido_recusado_sem_seFalhar_falha_o_roteiro_e_com_seFalhar_segue()
    {
        var f = new FerramentaFalsa { Responder = e => e["alvo"]!.GetValue<string>() == "/recusa" ? RespostaFerramenta.Recusa("não", 409) : null };
        var semDesvio = Roteiro.DeJson("""{ "passos": [ { "pedir": { "alvo": "/recusa" } }, { "pedir": { "alvo": "/depois" } } ] }""");
        var comDesvio = Roteiro.DeJson("""{ "passos": [ { "pedir": { "alvo": "/recusa" }, "seFalhar": "x" }, { "pedir": { "alvo": "/pulado" } }, { "id": "x", "pedir": { "alvo": "/desvio" } } ] }""");

        Assert.Equal(ExecutorRoteiro.Falhou, await new ExecutorRoteiro(f, TextWriter.Null, Rapido).RodarAsync(semDesvio));
        Assert.Equal(["/recusa"], Alvos(f));
        f.Entradas.Clear();
        Assert.Equal(ExecutorRoteiro.Terminou, await new ExecutorRoteiro(f, TextWriter.Null, Rapido).RodarAsync(comDesvio));
        Assert.Equal(["/recusa", "/desvio"], Alvos(f));
    }

    [Fact]
    public async Task A_criacao_cancelada_no_meio_de_uma_espera_termina_o_roteiro()
    {
        var roteiro = Roteiro.DeJson("""{ "passos": [ { "esperar": { "tipos": ["criacao-resposta"], "prazoS": 5 } }, { "pedir": { "alvo": "/nunca" } } ] }""");
        var f = new FerramentaFalsa();
        var rodando = new ExecutorRoteiro(f, TextWriter.Null, Rapido).RodarAsync(roteiro);
        await Task.Delay(50);
        f.Evento("criacao-cancelada", new JsonObject { ["por"] = "usuario" });

        Assert.Equal(ExecutorRoteiro.Terminou, await rodando.WaitAsync(TimeSpan.FromSeconds(10)));
        Assert.Empty(f.Entradas);
    }

    [Theory]
    [InlineData("""{ "passos": [] }""")]
    [InlineData("""{ "passos": [ { "pedir": { "alvo": "/a" }, "esperar": { "tipos": ["x"] } } ] }""")]
    [InlineData("""{ "passos": [ { "pedir": { "alvo": "sem-barra" } } ] }""")]
    [InlineData("""{ "passos": [ { "pedir": { "alvo": "/a" }, "ir": "nao-existe" } ] }""")]
    [InlineData("""{ "passos": [ { "id": "a", "fim": true }, { "id": "a", "fim": true } ] }""")]
    [InlineData("""{ "passos": [ { "esperar": { "tipos": [] } } ] }""")]
    [InlineData("""{ "passos": [ { "fim": true, "campoDesconhecido": 1 } ] }""")]
    [InlineData("""{ "passos": null }""")]
    [InlineData("""{ "passos": [ { "fim": true, "se": null } ] }""")]
    [InlineData("""{ "passos": [ { "esperar": { "tipos": null } } ] }""")]
    [InlineData("""{ "passos": [ { "pedir": { "alvo": "/a", "metodo": null } } ] }""")]
    [InlineData("""{ "passos": [ { "pedir": { "alvo": "/a", "metodo": "PUT" } } ] }""")]
    [InlineData("""{ "passos": [ { "esperar": { "tipos": ["x"], "prazoS": 86401 } } ] }""")]
    [InlineData("""{ "passos": [ { "id": "a", "fim": true, "se": [ { "campo": "x", "igual": null, "ir": "a" } ] } ] }""")]
    public void Roteiro_mal_formado_e_recusado(string json) =>
        Assert.Throws<RoteiroInvalidoException>(() => Roteiro.DeJson(json));

    [Fact]
    public async Task Sem_resposta_do_eventos_no_comeco_o_roteiro_falha_em_vez_de_partir_do_zero()
    {
        var f = new FerramentaFalsa { EventosFalham = true };
        var roteiro = Roteiro.DeJson("""{ "passos": [ { "pedir": { "alvo": "/a" } } ] }""");

        Assert.Equal(ExecutorRoteiro.Falhou, await new ExecutorRoteiro(f, TextWriter.Null, TimeSpan.FromMilliseconds(1)).RodarAsync(roteiro));
        Assert.Empty(f.Entradas);
    }

    [Fact]
    public async Task Um_ir_em_ciclo_para_no_maximo_de_passos()
    {
        var f = new FerramentaFalsa();
        var roteiro = Roteiro.DeJson("""{ "passos": [ { "id": "a", "pedir": { "alvo": "/x" }, "ir": "a" } ] }""");

        Assert.Equal(ExecutorRoteiro.Falhou, await new ExecutorRoteiro(f, TextWriter.Null, Rapido).RodarAsync(roteiro));
        Assert.Equal(ExecutorRoteiro.MaximoPassos, f.Entradas.Count);
    }

    [Fact]
    public async Task A_criacao_salva_no_meio_de_uma_espera_termina_o_roteiro()
    {
        var roteiro = Roteiro.DeJson("""{ "passos": [ { "esperar": { "tipos": ["criacao-resposta"], "prazoS": 5 } }, { "pedir": { "alvo": "/nunca" } } ] }""");
        var f = new FerramentaFalsa();
        var rodando = new ExecutorRoteiro(f, TextWriter.Null, Rapido).RodarAsync(roteiro);
        await Task.Delay(50);
        f.Evento("criacao-salva", new JsonObject { ["memoria"] = "TESTE" });
        Assert.Equal(ExecutorRoteiro.Terminou, await rodando.WaitAsync(TimeSpan.FromSeconds(10)));
        Assert.Empty(f.Entradas);
    }

    [Fact]
    public async Task Um_evento_com_tipo_que_nao_e_texto_e_ignorado()
    {
        var roteiro = Roteiro.DeJson("""{ "passos": [ { "esperar": { "tipos": ["criacao-resposta"], "prazoS": 5 } }, { "pedir": { "alvo": "/depois" } } ] }""");
        var f = new FerramentaFalsa();
        var rodando = new ExecutorRoteiro(f, TextWriter.Null, Rapido).RodarAsync(roteiro);
        await Task.Delay(50);
        f.Evento("qualquer");
        f.EventoCru(new JsonObject { ["tipo"] = 7 });
        f.Evento("criacao-resposta");

        Assert.Equal(ExecutorRoteiro.Terminou, await rodando.WaitAsync(TimeSpan.FromSeconds(10)));
        Assert.Equal(["/depois"], Alvos(f));
    }

    [Theory]
    [InlineData("texto", "Sim")]
    [InlineData("salvar", "true")]
    [InlineData("numero", "2")]
    [InlineData("corpo.etapa", "perguntas")]
    [InlineData("corpo.lista.1", "b")]
    public void O_campo_do_ramo_e_um_caminho_com_pontos(string campo, string esperado)
    {
        var no = JsonNode.Parse("""{"texto":"Sim","salvar":true,"numero":2,"corpo":{"etapa":"perguntas","lista":["a","b"]}}""");

        Assert.Equal(esperado, ExecutorRoteiro.Valor(no, campo));
        Assert.Null(ExecutorRoteiro.Valor(no, "nao.existe"));
    }

    [Fact]
    public void A_configuracao_do_repositorio_tem_os_gatilhos_os_tempos_e_o_Claude_sem_endereco()
    {
        var config = Caminhos.Config;

        Assert.Equal("criar-memoria", config.Gatilhos["criacao-pedida"]);
        Assert.Equal("ficha-tecnica", config.Gatilhos["ficha-pedida"]);
        Assert.Equal(TimeSpan.FromSeconds(1), config.Intervalo);
        Assert.Equal(TimeSpan.FromSeconds(5), config.EsperaQueda);
        Assert.Equal(TimeSpan.FromMinutes(30), config.PrazoAgente);
        Assert.Equal(ConfigLancador.TipoClaude, config.Lancador.Tipo);
        Assert.Equal("claude", config.Lancador.Comando);
        Assert.Contains("-p", config.Lancador.Argumentos);
        Assert.Contains(LancadorClaude.MarcaPrompt, config.Lancador.Argumentos);
        Assert.Contains("json", config.Lancador.Argumentos);
        Assert.Contains(LancadorClaude.MarcaFerramentas, config.Lancador.Argumentos);
        Assert.All(config.Gatilhos.Values, t => Assert.Contains("Bash(porta:*)", config.Lancador.FerramentasDe(t)));
        // Cada gatilho tem a sua tarefa, e cada uma manda usar a ferramenta porta.
        Assert.All(config.Gatilhos.Values, t => Assert.Contains("porta", LancadorClaude.Prompt(Caminhos.Agente, t)));
        Assert.All(config.Lancador.Roteiros.Values, r => Roteiro.Carregar(Path.Combine(Caminhos.Agente, r)));
    }

    [Fact]
    public async Task O_lancador_do_Claude_roda_o_comando_da_config_com_o_prompt_da_tarefa_e_a_saida_no_agente_log()
    {
        using var local = new LocalTemporario();
        // No lugar do claude, a própria porta: ela recusa o prompt como argumento e o diz na saída.
        var config = new ConfigLancador { Comando = Path.ChangeExtension(Caminhos.PortaDll, OperatingSystem.IsWindows() ? ".exe" : null), Argumentos = ["{prompt}"] };
        var lancador = new LancadorClaude(config, Caminhos.Agente, local.Local);

        var codigo = await lancador.RodarAsync("criar-memoria", null, CancellationToken.None);

        Assert.Equal(1, codigo);
        var saida = local.Ler("agente.log")!;
        Assert.Contains("Argumento desconhecido: # Tarefa: criar uma memória", saida);
    }

    [Fact]
    public async Task O_lancador_encerra_o_processo_no_cancelamento()
    {
        using var local = new LocalTemporario();
        var lancador = new LancadorModeloFalso(new Dictionary<string, string> { ["criar-memoria"] = Caminhos.Roteiro("trava") }, local.Local);
        using var prazo = new CancellationTokenSource(TimeSpan.FromSeconds(1.5));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => lancador.RodarAsync("criar-memoria", null, prazo.Token));

        Assert.True(System.Diagnostics.Process.GetProcesses().All(p => p.Id != lancador.UltimoPid));
    }

    [Fact]
    public async Task Um_texto_inteiro_evento_campo_vira_o_valor_do_campo_do_evento_com_o_tipo_dele()
    {
        var roteiro = Roteiro.DeJson("""
            { "passos": [ { "pedir": { "alvo": "/ficha/proposta", "corpo": {
                "idEquipamento": "{evento.idEquipamento}", "numero": "{evento.n}", "ligado": "{evento.ativo}",
                "grupo": "{evento.ficha.grupo}", "lista": ["{evento.idEquipamento}", "texto {evento.idEquipamento}"], "fixo": "x" } } } ] }
            """);
        var evento = new JsonObject
        {
            ["tipo"] = "ficha-pedida", ["idEquipamento"] = "eq-1", ["n"] = 7, ["ativo"] = true,
            ["ficha"] = new JsonObject { ["grupo"] = "alto-falante" },
        };
        var f = new FerramentaFalsa();

        Assert.Equal(ExecutorRoteiro.Terminou, await new ExecutorRoteiro(f, TextWriter.Null, Rapido, evento).RodarAsync(roteiro));

        var corpo = Assert.Single(f.Entradas)["corpo"]!.AsObject();
        Assert.Equal("eq-1", corpo["idEquipamento"]!.GetValue<string>());
        Assert.Equal(System.Text.Json.JsonValueKind.Number, corpo["numero"]!.GetValueKind());
        Assert.Equal(7, corpo["numero"]!.GetValue<int>());
        Assert.True(corpo["ligado"]!.GetValue<bool>());
        Assert.Equal("alto-falante", corpo["grupo"]!.GetValue<string>());
        Assert.Equal("eq-1", corpo["lista"]![0]!.GetValue<string>());
        Assert.Equal("texto {evento.idEquipamento}", corpo["lista"]![1]!.GetValue<string>()); // só o texto inteiro troca
        Assert.Equal("x", corpo["fixo"]!.GetValue<string>());
        // O roteiro fica como estava (a troca é numa cópia).
        Assert.Equal("{evento.idEquipamento}", roteiro.Passos[0].Pedir!.Corpo!["idEquipamento"]!.GetValue<string>());
    }

    [Fact]
    public async Task Sem_o_evento_ou_sem_o_campo_o_pedido_nao_vai_e_conta_como_recusado()
    {
        var semDesvio = Roteiro.DeJson("""{ "passos": [ { "pedir": { "alvo": "/a", "corpo": { "id": "{evento.idEquipamento}" } } } ] }""");
        var comDesvio = Roteiro.DeJson("""
            { "passos": [ { "pedir": { "alvo": "/a", "corpo": { "id": "{evento.idEquipamento}" } }, "seFalhar": "x" },
                          { "id": "x", "pedir": { "alvo": "/desvio" } } ] }
            """);
        var f = new FerramentaFalsa();

        Assert.Equal(ExecutorRoteiro.Falhou, await new ExecutorRoteiro(f, TextWriter.Null, Rapido).RodarAsync(semDesvio));
        Assert.Equal(ExecutorRoteiro.Falhou,
            await new ExecutorRoteiro(f, TextWriter.Null, Rapido, new JsonObject { ["tipo"] = "ficha-pedida" }).RodarAsync(semDesvio));
        Assert.Empty(f.Entradas);
        Assert.Equal(ExecutorRoteiro.Terminou, await new ExecutorRoteiro(f, TextWriter.Null, Rapido).RodarAsync(comDesvio));
        Assert.Equal(["/desvio"], Alvos(f));
    }

    [Fact]
    public void O_roteiro_da_ficha_manda_a_proposta_para_o_equipamento_do_evento_com_a_origem_e_a_fonte()
    {
        var r = Roteiro.Carregar(Caminhos.Roteiro("ficha-basica"));

        var proposta = Assert.Single(r.Passos, p => p.Pedir?.Alvo == "/ficha/proposta").Pedir!;
        Assert.Equal("{evento.idEquipamento}", proposta.Corpo!["idEquipamento"]!.GetValue<string>());
        var dados = proposta.Corpo!["dados"]!.AsArray();
        Assert.NotEmpty(dados);
        Assert.All(dados, d =>
        {
            Assert.Contains(d!["origem"]!.GetValue<string>(), new[] { "oficial", "outra-fonte" });
            Assert.False(string.IsNullOrWhiteSpace(d["fonte"]?.GetValue<string>()));
        });
        Assert.Equal("testes/roteiros/ficha-basica.json", Caminhos.Config.Lancador.Roteiros["ficha-tecnica"]);
    }

    [Fact]
    public void O_prompt_de_uma_tarefa_fora_da_pasta_tarefas_e_recusado() =>
        Assert.Throws<ArgumentException>(() => LancadorClaude.Prompt(Caminhos.Agente, "../segredo"));
}
