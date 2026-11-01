using System.Text.Json.Nodes;
using Agente.Servico;

namespace Agente.Testes;

/// <summary>
/// O aprendizado em arquivos (história 14e): a regra do perfil, o registro, o ajuste à mão e a ferramenta
/// <c>aprendizado</c>. As linhas Duas de música, Uma só, Mesma memória, Escolhas diferentes, Cancelada, Começa vazio,
/// Ferramenta inválida e Ajuste à mão da matriz. Tudo numa pasta local temporária.
/// </summary>
public sealed class AprendizadoTestes
{
    private static readonly DateTimeOffset Inicio = new(2026, 11, 1, 20, 0, 0, TimeSpan.FromHours(-3));

    /// <summary>Uma criação como o serviço a fecha: o Agente diz a situação e as variáveis, e o serviço grava no fim.</summary>
    private sealed class Linha(string local)
    {
        private int _n;

        public string Criar(string fim, string? memoria, string? situacao, JsonObject? variaveis, JsonArray? outros = null, bool semId = false)
        {
            Aprendizado.ApagarAgora(local);
            if (situacao is not null)
                Assert.True(Aprendizado.Executar(new JsonObject { ["acao"] = "situacao", ["nome"] = situacao }.ToJsonString(), local).Ok);
            if (variaveis is not null)
                Assert.True(Aprendizado.Executar(new JsonObject { ["acao"] = "variaveis", ["valores"] = variaveis }.ToJsonString(), local).Ok);
            foreach (var o in (outros ?? []).OfType<JsonObject>())
            {
                var entrada = (JsonObject)o.DeepClone();
                entrada["acao"] = "outro";
                Assert.True(Aprendizado.Executar(entrada.ToJsonString(), local).Ok);
            }
            var hora = Inicio.AddMinutes(++_n);
            var evento = fim == Aprendizado.FimSalva
                ? new JsonObject { ["tipo"] = "criacao-salva", ["memoria"] = memoria }
                : new JsonObject { ["tipo"] = "criacao-" + fim, ["por"] = "usuario" };
            var registro = Aprendizado.MontarRegistro(fim, evento, null, [], [], semId ? null : Estado(memoria), Aprendizado.LerAgora(local), hora);
            var caminho = Aprendizado.GravarRegistro(local, registro, hora);
            Aprendizado.ApagarAgora(local);
            if (fim == Aprendizado.FimSalva && registro["situacao"]?["chave"]?.GetValue<string>() is { } chave)
                Aprendizado.RefazerPerfil(local, chave);
            return caminho;
        }

        public string Salvar(string memoria, string situacao, double grave) =>
            Criar(Aprendizado.FimSalva, memoria, situacao, new JsonObject { ["grave"] = grave });
    }

    /// <summary>O <c>/estado</c> com a memória em uso (o id é o nome com o prefixo "id-").</summary>
    private static JsonObject? Estado(string? memoria, JsonArray? parametros = null) => memoria is null
        ? null
        : new JsonObject
        {
            ["setup"] = new JsonObject
            {
                ["memorias"] = new JsonArray(
                    new JsonObject { ["id"] = "id-outra", ["nome"] = "OUTRA", ["emUso"] = false, ["nivel"] = null, ["master"] = null },
                    new JsonObject { ["id"] = "id-" + memoria, ["nome"] = memoria, ["emUso"] = true, ["nivel"] = "medio", ["master"] = -12.5 }),
            },
            ["parametros"] = parametros ?? [],
        };

    private static JsonObject Perfil(string local, string situacao)
    {
        var r = Aprendizado.Executar(new JsonObject { ["acao"] = "perfil", ["situacao"] = situacao }.ToJsonString(), local);
        Assert.True(r.Ok, r.Motivo);
        return r.ParaObjeto();
    }

    private static int Registros(string local) => Aprendizado.LerRegistros(local).Count;

    // ------------------------------------------------------------------ a chave

    [Theory]
    [InlineData("Música", "musica")]
    [InlineData("  MÚSICA  ", "musica")]
    [InlineData("musica", "musica")]
    [InlineData("Música   ao \t vivo ", "musica ao vivo")]
    [InlineData("Ação à noite", "acao a noite")]
    public void A_chave_e_o_nome_em_minusculas_sem_acento_e_com_os_espacos_colapsados(string nome, string chave) =>
        Assert.Equal(chave, Aprendizado.Chave(nome));

    [Theory]
    [InlineData("musica")]
    [InlineData("musica ao vivo")]
    [InlineData("filme/serie")]
    [InlineData("..")]
    [InlineData("con")]
    [InlineData("a:b")]
    public void O_arquivo_do_perfil_fica_sempre_dentro_da_pasta(string chave)
    {
        var nome = Aprendizado.NomeArquivoPerfil(chave);
        Assert.EndsWith(".json", nome);
        Assert.Matches("^[a-z0-9-]+\\.json$", nome);
        Assert.NotEqual("con.json", nome);
        if (chave == "musica")
            Assert.Equal("musica.json", nome);
        Assert.NotEqual(Aprendizado.NomeArquivoPerfil("filme serie"), Aprendizado.NomeArquivoPerfil("filme/serie"));
    }

    // ------------------------------------------------------------------ a regra do perfil (CAP-7)

    [Fact]
    public void Duas_de_musica_em_memorias_diferentes_com_grave_4_e_4_6_dao_o_perfil_musica_com_4_3_e_o_filme_nao_muda()
    {
        using var t = new LocalTemporario();
        var linha = new Linha(t.Local);
        linha.Salvar("OUTRA C", "Filme", 9.0);
        var filmeAntes = Perfil(t.Local, "filme");

        linha.Salvar("A", "música", 4.0);
        Assert.Empty(Perfil(t.Local, "música")["variaveis"]!.AsObject()); // depois de uma só, nada muda
        linha.Salvar("B", "Música ", 4.6);

        var musica = Perfil(t.Local, "MUSICA");
        Assert.Equal("musica", musica["situacao"]!.GetValue<string>());
        Assert.Equal(4.3, musica["variaveis"]!["grave"]!.GetValue<double>(), 3);
        Assert.True(JsonNode.DeepEquals(filmeAntes, Perfil(t.Local, "filme")));
        Assert.Empty(Perfil(t.Local, "filme")["variaveis"]!.AsObject());
        Assert.True(File.Exists(Path.Combine(t.Local, "aprendizado", "perfis", "musica.json")));
        Assert.False(File.Exists(Path.Combine(t.Local, "aprendizado", "perfis", "filme.json")));
        Assert.Equal(3, Registros(t.Local));
    }

    [Fact]
    public void Uma_so_criacao_salva_grava_o_registro_e_nenhum_perfil_muda()
    {
        using var t = new LocalTemporario();
        new Linha(t.Local).Salvar("A", "música", 4.0);

        var (_, registro) = Assert.Single(Aprendizado.LerRegistros(t.Local));
        Assert.Equal("salva", registro["fim"]!.GetValue<string>());
        Assert.Equal("id-A", registro["memoria"]!["id"]!.GetValue<string>());
        Assert.Equal("A", registro["memoria"]!["nome"]!.GetValue<string>());
        Assert.Equal(4.0, registro["variaveis"]!["grave"]!.GetValue<double>());
        Assert.Empty(Perfil(t.Local, "música")["variaveis"]!.AsObject());
        Assert.False(Directory.Exists(Path.Combine(t.Local, "aprendizado", "perfis")));
        Assert.False(File.Exists(Aprendizado.CaminhoAgora(t.Local))); // o agora sai no fim
    }

    [Fact]
    public void Duas_criacoes_na_mesma_memoria_contam_uma_vez_e_o_perfil_nao_muda()
    {
        using var t = new LocalTemporario();
        var linha = new Linha(t.Local);
        linha.Salvar("A", "música", 4.0);
        linha.Salvar("A", "música", 4.0);

        Assert.Equal(2, Registros(t.Local));
        Assert.Empty(Perfil(t.Local, "música")["variaveis"]!.AsObject());
    }

    [Fact]
    public void Escolhas_a_mais_de_1_dB_nao_mudam_o_perfil()
    {
        using var t = new LocalTemporario();
        var linha = new Linha(t.Local);
        linha.Salvar("A", "música", 4.0);
        linha.Salvar("B", "música", 6.0);

        Assert.Empty(Perfil(t.Local, "música")["variaveis"]!.AsObject());
    }

    [Fact]
    public void A_criacao_cancelada_grava_o_registro_e_nao_conta_para_o_perfil()
    {
        using var t = new LocalTemporario();
        var linha = new Linha(t.Local);
        linha.Salvar("A", "música", 4.0);
        linha.Criar(Aprendizado.FimCancelada, "B", "música", new JsonObject { ["grave"] = 4.0 });
        linha.Criar(Aprendizado.FimInterrompida, "C", "música", new JsonObject { ["grave"] = 4.0 });

        var registros = Aprendizado.LerRegistros(t.Local).Select(r => r.Registro).ToArray();
        Assert.Equal(["salva", "cancelada", "interrompida"], registros.Select(r => r["fim"]!.GetValue<string>()));
        var cancelada = registros[1];
        Assert.Equal("musica", cancelada["situacao"]!["chave"]!.GetValue<string>());
        Assert.Null(cancelada["memoria"]);
        Assert.True(cancelada["semPalavras"]!["cancelou"]!.GetValue<bool>());
        Assert.Equal("usuario", cancelada["por"]!.GetValue<string>());
        // Mesmo refeito à força, o perfil só conta os salvos.
        Aprendizado.RefazerPerfil(t.Local, "musica");
        Assert.Empty(Perfil(t.Local, "música")["variaveis"]!.AsObject());
    }

    [Fact]
    public void O_mesmo_texto_em_memorias_diferentes_entra_e_o_grupo_com_mais_memorias_vence()
    {
        using var t = new LocalTemporario();
        var linha = new Linha(t.Local);
        linha.Criar("salva", "A", "festa", new JsonObject { ["cobertura"] = "varios", ["grave"] = 6.0 });
        linha.Criar("salva", "B", "festa", new JsonObject { ["cobertura"] = "Varios", ["grave"] = 2.0 });
        linha.Criar("salva", "C", "festa", new JsonObject { ["grave"] = 6.5 });
        linha.Criar("salva", "D", "festa", new JsonObject { ["grave"] = 2.4 });
        linha.Criar("salva", "E", "festa", new JsonObject { ["grave"] = 5.8 });

        var perfil = Perfil(t.Local, "festa");
        Assert.Equal("Varios", perfil["variaveis"]!["cobertura"]!.GetValue<string>()); // o texto mais recente do grupo
        // 6,0, 6,5 e 5,8 (três memórias) vencem 2,0 e 2,4 (duas): a média de uma escolha por memória.
        Assert.Equal(6.1, perfil["variaveis"]!["grave"]!.GetValue<double>(), 3);
    }

    [Fact]
    public void No_empate_de_memorias_vale_o_grupo_mais_recente()
    {
        using var t = new LocalTemporario();
        var linha = new Linha(t.Local);
        linha.Salvar("A", "musica", 2.0);
        linha.Salvar("B", "musica", 2.5);
        linha.Salvar("C", "musica", 8.0);
        Assert.Equal(2.25, Perfil(t.Local, "musica")["variaveis"]!["grave"]!.GetValue<double>(), 3);

        linha.Salvar("D", "musica", 8.4);
        Assert.Equal(8.2, Perfil(t.Local, "musica")["variaveis"]!["grave"]!.GetValue<double>(), 3);
    }

    [Fact]
    public void Os_outros_entram_como_opcao_so_com_variavel_e_em_duas_memorias_diferentes()
    {
        using var t = new LocalTemporario();
        var linha = new Linha(t.Local);
        JsonArray Outros() => new(
            new JsonObject { ["texto"] = "som de show", ["variavel"] = "grave" },
            new JsonObject { ["texto"] = "do jeito do meu tio" });
        linha.Criar("salva", "A", "música", new JsonObject { ["grave"] = 4.0 }, Outros());
        Assert.Empty(Perfil(t.Local, "música")["outros"]!.AsArray()); // depois de uma só, nada muda
        linha.Criar("salva", "A", "música", new JsonObject { ["grave"] = 4.0 }, Outros());
        Assert.Empty(Perfil(t.Local, "música")["outros"]!.AsArray()); // a mesma memória conta uma vez

        linha.Criar("salva", "B", "música", new JsonObject { ["grave"] = 9.0 }, Outros());

        var outro = Assert.Single(Perfil(t.Local, "música")["outros"]!.AsArray())!;
        Assert.Equal("som de show", outro["texto"]!.GetValue<string>());
        Assert.Equal("grave", outro["variavel"]!.GetValue<string>());
    }

    [Fact]
    public void Um_registro_sem_o_id_da_memoria_nao_conta_para_o_perfil()
    {
        using var t = new LocalTemporario();
        var linha = new Linha(t.Local);
        linha.Salvar("A", "música", 4.0);
        linha.Criar("salva", "A", "música", new JsonObject { ["grave"] = 4.2 }, semId: true); // o /estado falhou no fim

        var semId = Aprendizado.LerRegistros(t.Local)[1].Registro;
        Assert.Null(semId["memoria"]!["id"]);
        Assert.Equal("A", semId["memoria"]!["nome"]!.GetValue<string>());
        Assert.Empty(Perfil(t.Local, "música")["variaveis"]!.AsObject());
    }

    [Fact]
    public void So_o_registro_mais_recente_de_cada_memoria_vota()
    {
        using var t = new LocalTemporario();
        var linha = new Linha(t.Local);
        linha.Salvar("A", "música", 4.0);
        linha.Salvar("A", "música", 8.0); // a A foi salva de novo com outra escolha
        linha.Salvar("B", "música", 4.0);

        Assert.Empty(Perfil(t.Local, "música")["variaveis"]!.AsObject());
    }

    // ------------------------------------------------------------------ o registro

    [Fact]
    public void O_registro_traz_o_dialogo_o_proposto_e_o_que_ficou_a_escuta_o_como_ficou_e_o_contexto()
    {
        using var t = new LocalTemporario();
        var criacao = JsonNode.Parse("""
            { "etapa": "salvar", "rodada": 2, "somAntes": "ANTIGA",
              "perguntas": [ { "numero": 1, "texto": "Para que é?", "respostas": ["Música","Filme","Jogo"], "ultima": true,
                               "resposta": "som de show", "doOutro": true } ],
              "ajustes": [ { "rodada": 1, "codigo": "081", "indice": 2, "antes": "0.0", "novo": "4.0", "relido": "4.0", "frase": "x", "motivo": "y" },
                           { "rodada": 2, "codigo": "081", "indice": 2, "antes": "4.0", "novo": "5.0", "relido": "5.0", "frase": "x", "motivo": "y" } ],
              "comoFicou": [] }
            """)!.AsObject();
        JsonObject[] comoFicou =
        [
            new() { ["tipo"] = "criacao-como-ficou", ["texto"] = "Quero mais grave", ["doOutro"] = false, ["salvar"] = false, ["rodada"] = 1 },
            new() { ["tipo"] = "criacao-como-ficou", ["texto"] = "Ótimo, pode salvar", ["doOutro"] = false, ["salvar"] = true, ["rodada"] = 2 },
        ];
        JsonObject[] escutas = [new() { ["tipo"] = "criacao-escuta", ["variavel"] = "grave", ["escolha"] = 2, ["rodada"] = 2 }];
        Assert.True(Aprendizado.Executar("""{"acao":"situacao","nome":"Música"}""", t.Local).Ok);
        Assert.True(Aprendizado.Executar("""{"acao":"variaveis","valores":{"grave":5}}""", t.Local).Ok);

        var r = Aprendizado.MontarRegistro("salva", new JsonObject { ["memoria"] = "NOVA" }, criacao, comoFicou, escutas, Estado("NOVA"),
            Aprendizado.LerAgora(t.Local), Inicio);

        Assert.Equal("salva", r["fim"]!.GetValue<string>());
        Assert.Equal("id-NOVA", r["memoria"]!["id"]!.GetValue<string>());
        var dialogo = Assert.Single(r["dialogo"]!.AsArray())!;
        Assert.Equal("som de show", dialogo["resposta"]!.GetValue<string>());
        Assert.True(dialogo["doOutro"]!.GetValue<bool>());
        Assert.Equal("ANTIGA", r["somAntes"]!.GetValue<string>());
        var ajustes = r["ajustes"]!.AsArray();
        Assert.Equal(2, ajustes.Count);
        Assert.Equal("4.0", ajustes[1]!["antes"]!.GetValue<string>());
        Assert.Equal("5.0", ajustes[1]!["valor"]!.GetValue<string>());
        Assert.Equal("5.0", ajustes[1]!["relido"]!.GetValue<string>());
        Assert.Equal("grave", r["escutas"]![0]!["variavel"]!.GetValue<string>());
        var comoFicouNoRegistro = r["comoFicou"]!.AsArray();
        Assert.Equal("Quero mais grave", comoFicouNoRegistro[0]!["texto"]!.GetValue<string>()); // o negativo: o texto do refino
        Assert.True(comoFicouNoRegistro[1]!["salvar"]!.GetValue<bool>()); // o positivo
        Assert.Null(comoFicouNoRegistro[1]!["texto"]);
        Assert.Equal(1, r["semPalavras"]!["rodadasRefino"]!.GetValue<int>());
        Assert.False(r["semPalavras"]!["cancelou"]!.GetValue<bool>());
        Assert.Equal("medio", r["contexto"]!["nivel"]!.GetValue<string>());
        Assert.Equal(-12.5, r["contexto"]!["master"]!.GetValue<double>());
        Assert.StartsWith("2026-11-01T20:00:00", r["contexto"]!["hora"]!.GetValue<string>());
        Assert.Equal("Música", r["situacao"]!["nome"]!.GetValue<string>());
        Assert.Equal(5, r["variaveis"]!["grave"]!.GetValue<double>());
    }

    [Fact]
    public void Sem_o_GET_criacao_e_sem_o_estado_o_registro_sai_com_o_que_tiver()
    {
        using var t = new LocalTemporario();
        var r = Aprendizado.MontarRegistro("interrompida", null, null, [], [], null, [], Inicio);
        var caminho = Aprendizado.GravarRegistro(t.Local, r, Inicio);
        var outro = Aprendizado.GravarRegistro(t.Local, r, Inicio); // a mesma hora: o próximo n

        Assert.EndsWith("20261101T230000000Z-1.json", caminho);
        Assert.EndsWith("20261101T230000000Z-2.json", outro);
        Assert.Empty(r["dialogo"]!.AsArray());
        Assert.Empty(r["ajustes"]!.AsArray());
        Assert.Null(r["situacao"]);
        Assert.Null(r["contexto"]!["nivel"]);
    }

    // ------------------------------------------------------------------ o ajuste à mão depois

    [Fact]
    public void Ao_abrir_a_proxima_o_ajuste_a_mao_entra_no_registro_salvo_da_memoria_em_uso()
    {
        using var t = new LocalTemporario();
        var criacao = JsonNode.Parse("""
            { "etapa": "salvar", "ajustes": [ { "rodada": 1, "codigo": "081", "indice": 2, "antes": "0.0", "novo": "4.0", "relido": "4.0" },
                                              { "rodada": 1, "codigo": "081", "indice": 0, "antes": "0.0", "novo": "-1.0", "relido": "-1.0" } ] }
            """)!.AsObject();
        var registro = Aprendizado.MontarRegistro("salva", new JsonObject { ["memoria"] = "A" }, criacao, [], [], Estado("A"), [], Inicio);
        var caminho = Aprendizado.GravarRegistro(t.Local, registro, Inicio);

        // A próxima abre com a A em uso: o 081[2] está em 5,5 (à mão) e o 081[0] mudou só 0,03.
        var parametros = new JsonArray(
            new JsonObject { ["codigo"] = "081", ["indice"] = 2, ["noFio"] = "5.5" },
            new JsonObject { ["codigo"] = "081", ["indice"] = 0, ["noFio"] = "-0.97" },
            new JsonObject { ["codigo"] = "080", ["indice"] = null, ["noFio"] = "-20.0" });
        var abrindo = new JsonObject { ["etapa"] = "perguntas", ["somAntes"] = "A", ["ajustes"] = new JsonArray() };

        Assert.Equal(1, Aprendizado.ConferirAjustesDepois(t.Local, abrindo, Estado("A", parametros)));

        var depois = Assert.Single(JsonNode.Parse(File.ReadAllText(caminho))!["ajustesDepois"]!.AsArray())!;
        Assert.Equal("081", depois["codigo"]!.GetValue<string>());
        Assert.Equal(2, depois["indice"]!.GetValue<int>());
        Assert.Equal(4.0, depois["salvo"]!.GetValue<double>());
        Assert.Equal(5.5, depois["agora"]!.GetValue<double>());

        // Se a criação nova já mexeu ali, vale o antes do primeiro ajuste dela; de volta ao salvo, a diferença sai.
        var mexida = new JsonObject
        {
            ["etapa"] = "resumo", ["somAntes"] = "A",
            ["ajustes"] = new JsonArray(new JsonObject { ["codigo"] = "081", ["indice"] = 2, ["antes"] = "4.0", ["novo"] = "7.0", ["relido"] = "7.0" }),
        };
        var agora = new JsonArray(new JsonObject { ["codigo"] = "081", ["indice"] = 2, ["noFio"] = "7.0" });
        Assert.Equal(0, Aprendizado.ConferirAjustesDepois(t.Local, mexida, Estado("A", agora)));
        Assert.Empty(JsonNode.Parse(File.ReadAllText(caminho))!["ajustesDepois"]!.AsArray());
    }

    [Fact]
    public void Sem_registro_salvo_da_memoria_em_uso_nada_muda()
    {
        using var t = new LocalTemporario();
        var parametros = new JsonArray(new JsonObject { ["codigo"] = "081", ["indice"] = 2, ["noFio"] = "5.5" });

        Assert.Equal(0, Aprendizado.ConferirAjustesDepois(t.Local, new JsonObject { ["etapa"] = "perguntas" }, Estado("A", parametros)));
        Assert.False(Directory.Exists(Aprendizado.PastaDe(t.Local)));
    }

    [Fact]
    public void Sem_o_id_da_memoria_em_uso_o_nome_nao_basta_e_um_ajustesDepois_repetido_nao_quebra()
    {
        using var t = new LocalTemporario();
        var criacao = JsonNode.Parse("""{ "ajustes": [ { "rodada": 1, "codigo": "081", "indice": 2, "antes": "0.0", "novo": "4.0", "relido": "4.0" } ] }""")!.AsObject();
        var registro = Aprendizado.MontarRegistro("salva", new JsonObject { ["memoria"] = "A" }, criacao, [], [], Estado("A"), [], Inicio);
        var repetido = new JsonObject { ["codigo"] = "081", ["indice"] = 2, ["salvo"] = 4.0, ["agora"] = 4.8 };
        registro["ajustesDepois"] = new JsonArray(repetido.DeepClone(), repetido.DeepClone());
        var caminho = Aprendizado.GravarRegistro(t.Local, registro, Inicio);
        var parametros = new JsonArray(new JsonObject { ["codigo"] = "081", ["indice"] = 2, ["noFio"] = "5.5" });
        var abrindo = new JsonObject { ["etapa"] = "perguntas", ["somAntes"] = "A" };

        // Uma memória "A" de outro Setup, ou o /estado sem a memória em uso: só o somAntes com o mesmo nome não vale.
        var semId = Estado("A", (JsonArray)parametros.DeepClone())!;
        foreach (var m in semId["setup"]!["memorias"]!.AsArray())
            m!.AsObject().Remove("id");
        Assert.Equal(0, Aprendizado.ConferirAjustesDepois(t.Local, abrindo, semId));
        Assert.Equal(0, Aprendizado.ConferirAjustesDepois(t.Local, abrindo, null));
        Assert.Equal(2, JsonNode.Parse(File.ReadAllText(caminho))!["ajustesDepois"]!.AsArray().Count);

        // Com o id, o repetido vira um só (vale o último), com o valor de agora.
        Assert.Equal(1, Aprendizado.ConferirAjustesDepois(t.Local, abrindo, Estado("A", parametros)));
        var depois = Assert.Single(JsonNode.Parse(File.ReadAllText(caminho))!["ajustesDepois"]!.AsArray())!;
        Assert.Equal(5.5, depois["agora"]!.GetValue<double>());
    }

    // ------------------------------------------------------------------ a ferramenta (processo)

    [Fact]
    public async Task Comeca_vazio_o_perfil_sem_a_pasta_volta_com_as_listas_vazias_e_ok()
    {
        using var t = new LocalTemporario();

        var (codigo, saida, json) = await RodarAsync("""{"acao":"perfil","situacao":"Música"}""", t.Local);

        Assert.True(codigo == 0, saida);
        Assert.True(json["ok"]!.GetValue<bool>());
        Assert.Equal("musica", json["situacao"]!.GetValue<string>());
        Assert.Empty(json["variaveis"]!.AsObject());
        Assert.Empty(json["outros"]!.AsArray());
        Assert.False(Directory.Exists(Aprendizado.PastaDe(t.Local))); // ler não cria nada
    }

    [Theory]
    [InlineData("""{"acao":"x"}""")]
    [InlineData("""{"acao":"situacao","nome":""}""")]
    [InlineData("""{"acao":"situacao","nome":"   "}""")]
    [InlineData("""{"acao":"situacao"}""")]
    [InlineData("""{"acao":"situacao","nome":"um nome de situação comprido demais para caber"}""")]
    [InlineData("""{"acao":"situacao","nome":7}""")]
    [InlineData("""{"acao":"variaveis","valores":{"grave":true}}""")]
    [InlineData("""{"acao":"variaveis","valores":{"grave":[4]}}""")]
    [InlineData("""{"acao":"variaveis","valores":{}}""")]
    [InlineData("""{"acao":"outro","variavel":"grave"}""")]
    [InlineData("""{"acao":"perfil"}""")]
    [InlineData("""{"acao":"situacao","nome":"música" """)]
    [InlineData("""[1,2]""")]
    [InlineData("""{"acao":"situacao","nome":"música","nome":"filme"}""")]
    [InlineData("""{"acao":"variaveis","valores":{"Grave":4.0,"grave":5.0}}""")]
    public async Task Uma_entrada_invalida_e_recusada_com_o_motivo_e_o_codigo_1(string entrada)
    {
        using var t = new LocalTemporario();

        var (codigo, saida, json) = await RodarAsync(entrada, t.Local);

        Assert.True(codigo == 1, saida);
        Assert.False(json["ok"]!.GetValue<bool>());
        Assert.False(string.IsNullOrWhiteSpace(json["motivo"]?.GetValue<string>()));
        Assert.False(File.Exists(Aprendizado.CaminhoAgora(t.Local)));
    }

    [Fact]
    public async Task A_ferramenta_guarda_a_situacao_as_variaveis_e_os_outros_no_agora()
    {
        using var t = new LocalTemporario();

        var (c1, s1, situacao) = await RodarAsync("""{"acao":"situacao","nome":"  Música  ao vivo "}""", t.Local);
        Assert.True(c1 == 0, s1);
        Assert.Equal("musica ao vivo", situacao["situacao"]!.GetValue<string>());
        Assert.Equal("Música  ao vivo", situacao["nome"]!.GetValue<string>());
        Assert.Contains("Música", s1); // os acentos saem como texto

        var (c2, s2, _) = await RodarAsync("""{"acao":"variaveis","valores":{"grave":4.0,"cobertura":"varios"}}""", t.Local);
        Assert.True(c2 == 0, s2);
        var (c3, s3, variaveis) = await RodarAsync("""{"acao":"variaveis","valores":{"Grave":5.5}}""", t.Local);
        Assert.True(c3 == 0, s3);
        Assert.Equal(5.5, variaveis["variaveis"]!["grave"]!.GetValue<double>()); // a chamada nova troca o valor
        Assert.Equal("varios", variaveis["variaveis"]!["cobertura"]!.GetValue<string>());
        var (c4, s4, _) = await RodarAsync("""{"acao":"outro","texto":"som de show","variavel":"grave"}""", t.Local);
        Assert.True(c4 == 0, s4);

        var agora = Aprendizado.LerAgora(t.Local);
        Assert.Equal("musica ao vivo", agora["situacao"]!["chave"]!.GetValue<string>());
        Assert.Equal(5.5, agora["variaveis"]!["grave"]!.GetValue<double>());
        Assert.Equal("grave", agora["outros"]![0]!["variavel"]!.GetValue<string>());
        Assert.Equal(["agora.json"], Directory.GetFiles(Aprendizado.PastaDe(t.Local)).Select(Path.GetFileName));

        // A entrada também vale pelo stdin.
        var (c5, s5) = await PortaCli.RodarCruAsync([], """{"acao":"perfil","situacao":"musica ao vivo"}""", t.Local, "aprendizado.dll");
        Assert.True(c5 == 0, s5);
    }

    private static async Task<(int Codigo, string Saida, JsonObject Json)> RodarAsync(string entrada, string local)
    {
        var (codigo, cru) = await PortaCli.RodarCruAsync(["--json", entrada], null, local, "aprendizado.dll");
        var linhas = cru.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        Assert.Single(linhas); // sempre um JSON numa linha
        return (codigo, linhas[0], (JsonObject)JsonNode.Parse(linhas[0])!);
    }
}
