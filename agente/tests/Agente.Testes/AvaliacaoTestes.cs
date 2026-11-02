using System.Text.Json.Nodes;
using Agente.Avaliacao;
using Agente.Servico;
using BgsControle.Nucleo.Rendimento;
using BgsControle.Nucleo.Setups;
using Xunit.Abstractions;

namespace Agente.Testes;

/// <summary>
/// As rodadas dos Agentes falsos nos 8 perfis (história 14f), uma vez para a classe: o bom, o genérico e o das entradas
/// separadas. Cada perfil roda no seu núcleo, na sua porta e na sua pasta local, em paralelo.
/// </summary>
public sealed class RodadasFalsas : IAsyncLifetime
{
    public IReadOnlyList<Perfil> Perfis { get; private set; } = [];

    internal Relatorio Bom { get; private set; } = null!;

    internal Relatorio Generico { get; private set; } = null!;

    internal Relatorio Separado { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        Perfis = Avaliacao.Perfis.Carregar(Avaliacao.Perfis.Pasta(Caminhos.Agente));
        var hash = HashCamadas.Calcular(Caminhos.Agente);
        Bom = await Avaliador.RodarAsync(Perfis, local => new AgenteBom(local), OpcoesAvaliacao.Falsos, Relatorio.TipoFalso, hash);
        Generico = await Avaliador.RodarAsync(Perfis, local => new AgenteGenerico(local), OpcoesAvaliacao.Falsos, Relatorio.TipoFalso, hash);
        Separado = await Avaliador.RodarAsync(Perfis, local => new AgenteBom(local, new DesvioFalso(RotaSempreSeparada: true)),
            OpcoesAvaliacao.Falsos, Relatorio.TipoFalso, hash);
    }

    public Task DisposeAsync() => Task.CompletedTask;
}

/// <summary>
/// O avaliador (spec-agente-memoria CAP-8, história 14f): os perfis, o ouvinte simulado, as métricas, o relatório, o custo
/// antes do <c>real</c> e a trava do <c>conferir</c>, com os Agentes falsos em código (nenhum modelo de verdade).
/// </summary>
public sealed class AvaliacaoTestes(RodadasFalsas rodadas, ITestOutputHelper saida) : IClassFixture<RodadasFalsas>
{
    private static ResultadoPerfil Do(Relatorio r, string perfil) => r.Perfis.Single(p => p.Perfil == perfil);

    // ------------------------------------------------------------------ os perfis

    [Fact]
    public void Os_8_perfis_cobrem_os_casos_e_so_tem_dados_genericos()
    {
        var perfis = rodadas.Perfis;
        Assert.Equal(8, perfis.Count);
        Assert.Equal(new[] { "ar-livre", "carro", "outro", "sala" }, perfis.Select(p => p.Setup.Ambiente).Distinct().Order());
        Assert.Contains(perfis, p => p.Setup.Caixas.Any(c => c.Nome.Contains("bob", StringComparison.OrdinalIgnoreCase)));
        Assert.Contains(perfis, p => p.Setup.Caixas.Count(c => c.Categoria == "tweeter") == 2); // 2 vias ativo
        // O caso da SPL: o máximo, a posição indiferente e as entradas somadas em todas as saídas com alto-falante.
        var spl = perfis.Single(p => p.Id == "spl-maximo");
        Assert.All(spl.Gosto.Rota, r => Assert.Equal("a+b", r.Rota));
        Assert.Contains("máximo", spl.Fatos["objetivo"]);
        Assert.Contains("indiferente", spl.Fatos["onde"]);
        // Ao menos dois perfis com o mesmo Setup e gostos diferentes.
        Assert.Contains(perfis.GroupBy(p => Perfis.SetupsJson(p with { Id = "x" })), g => g.Count() >= 2
            && g.Select(p => System.Text.Json.JsonSerializer.Serialize(p.Gosto)).Distinct().Count() == g.Count());
    }

    [Fact]
    public void Um_perfil_mal_formado_e_recusado_com_o_motivo()
    {
        var bom = File.ReadAllText(Path.Combine(Perfis.Pasta(Caminhos.Agente), "sala-fiel.json"));
        var semRota = JsonNode.Parse(bom)!.AsObject();
        semRota["gosto"]!["rota"]!.AsArray().RemoveAt(0);
        var e = Assert.Throws<PerfilInvalidoException>(() => Perfis.Conferir(Perfis.DeJson(semRota.ToJsonString())));
        Assert.Contains("gosto.rota", e.Message);
        var foraDaRegulagem = JsonNode.Parse(bom)!.AsObject();
        foraDaRegulagem["gosto"]!["alvos"]!.AsArray().Add(new JsonObject { ["codigo"] = "020", ["indice"] = 0, ["valor"] = 1 });
        Assert.Throws<PerfilInvalidoException>(() => Perfis.Conferir(Perfis.DeJson(foraDaRegulagem.ToJsonString())));
    }

    // ------------------------------------------------------------------ os Agentes falsos

    [Fact]
    public void O_bom_passa_nos_8_perfis()
    {
        saida.WriteLine(rodadas.Bom.ParaTexto());
        Assert.All(rodadas.Bom.Perfis, p =>
        {
            Assert.True(p.Passou, $"{p.Perfil}: {p.Motivo}");
            Assert.True(p.Distancia <= 1.5m);
            Assert.InRange(p.NumeroPerguntas, 1, 8);
            Assert.Empty(p.Violacoes);
            Assert.True(p.RotaCerta);
            Assert.True(p.Salva);
        });
        Assert.True(rodadas.Bom.Passou);
        Assert.Equal(Relatorio.TipoFalso, rodadas.Bom.Tipo);
        // A escuta "1 ou 2" trabalhou: ao menos um perfil chegou ao gosto escondido pelo refino.
        Assert.Contains(rodadas.Bom.Perfis, p => p.Escutas.Count > 0);
    }

    [Fact]
    public void O_generico_falha_pela_distancia_ou_pela_rota()
    {
        saida.WriteLine(rodadas.Generico.ParaTexto());
        Assert.False(rodadas.Generico.Passou);
        var falhos = rodadas.Generico.Perfis.Where(p => !p.Passou).ToArray();
        Assert.NotEmpty(falhos);
        Assert.All(falhos, p => Assert.True(p.Distancia > 1.5m || !p.RotaCerta, $"{p.Perfil}: {p.Motivo}"));
        // As mesmas perguntas para todos.
        Assert.Single(rodadas.Generico.Perfis.Select(p => string.Join("|", p.Perguntas.Select(q => q.Texto))).Distinct());
    }

    [Fact]
    public void O_das_entradas_separadas_falha_no_perfil_da_SPL_pela_rota()
    {
        saida.WriteLine(rodadas.Separado.ParaTexto());
        var spl = Do(rodadas.Separado, "spl-maximo");
        Assert.False(spl.Passou);
        Assert.False(spl.RotaCerta, spl.Motivo);
        Assert.Contains("Rota errada", spl.Motivo);
        Assert.False(rodadas.Separado.Passou);
    }

    [Fact]
    public void Dois_perfis_do_mesmo_Setup_com_gostos_diferentes_recebem_sequencias_diferentes()
    {
        var fiel = Do(rodadas.Bom, "carro-fiel").Perguntas.Select(q => q.Texto).ToArray();
        var grave = Do(rodadas.Bom, "carro-grave").Perguntas.Select(q => q.Texto).ToArray();
        Assert.NotEqual(fiel, grave);
        var salaFiel = Do(rodadas.Bom, "sala-fiel").Perguntas.Select(q => q.Texto).ToArray();
        var salaFesta = Do(rodadas.Bom, "sala-festa").Perguntas.Select(q => q.Texto).ToArray();
        Assert.NotEqual(salaFiel, salaFesta);
    }

    [Fact]
    public void Um_carro_e_uma_sala_recebem_primeiras_perguntas_com_respostas_diferentes()
    {
        var carro = Do(rodadas.Bom, "carro-fiel").Perguntas[0].Respostas;
        var sala = Do(rodadas.Bom, "sala-fiel").Perguntas[0].Respostas;
        Assert.NotEqual(carro, sala);
    }

    [Fact]
    public async Task O_limitador_acima_do_teto_conta_como_violacao_e_o_perfil_falha()
    {
        var perfil = rodadas.Perfis.Single(p => p.Id == "spl-maximo");
        var r = await Avaliador.RodarPerfilAsync(perfil, local => new AgenteBom(local, new DesvioFalso(LimitadorAcimaDoTeto: 3m)), OpcoesAvaliacao.Falsos);

        saida.WriteLine(r.Motivo);
        Assert.False(r.Passou);
        var cerca = r.Violacoes.Where(v => v.Tipo == "cerca").ToArray();
        Assert.NotEmpty(cerca);
        Assert.All(cerca, v => Assert.StartsWith("Limite: ", v.Detalhe));
    }

    [Fact]
    public async Task Um_EQ_antes_do_ganho_na_mesma_rodada_conta_como_violacao_e_o_perfil_falha()
    {
        var perfil = rodadas.Perfis.Single(p => p.Id == "carro-fiel");
        var r = await Avaliador.RodarPerfilAsync(perfil, local => new AgenteBom(local, new DesvioFalso(TomAntesDoGanho: true)), OpcoesAvaliacao.Falsos);

        saida.WriteLine(r.Motivo);
        Assert.False(r.Passou);
        Assert.Contains(r.Violacoes, v => v.Tipo == "ordem");
    }

    // ------------------------------------------------------------------ o ouvinte

    private static Perfil PerfilCom(params AlvoPerfil[] alvos)
    {
        var p = Perfis.CarregarArquivo(Path.Combine(Perfis.Pasta(Caminhos.Agente), "sala-fiel.json"));
        return p with { Gosto = p.Gosto with { Alvos = alvos } };
    }

    private static AjusteEscuta Versao(decimal valor) => new("081", 2, valor, null, "Grave", "Comparar.");

    [Fact]
    public void No_1_ou_2_o_ouvinte_escolhe_a_versao_mais_perto_do_gosto_e_no_empate_tanto_faz()
    {
        var ouvinte = new OuvinteSimulado(PerfilCom(new AlvoPerfil("081", 2, 4m)), null);

        Assert.Equal(EscolhaEscuta.Dois, ouvinte.Escolher([Versao(2m)], [Versao(5m)]));
        Assert.Equal(EscolhaEscuta.Um, ouvinte.Escolher([Versao(3.5m)], [Versao(5m)]));
        Assert.Equal(EscolhaEscuta.TantoFaz, ouvinte.Escolher([Versao(3m)], [Versao(5m)]));
    }

    [Fact]
    public void O_ouvinte_responde_pelas_palavras_dos_fatos_e_sem_nenhuma_usa_o_fato_mais_perto_da_pergunta()
    {
        var perfil = PerfilCom(new AlvoPerfil("081", 2, 4m));
        var fatos = Palavras.De(string.Join(" ", perfil.Fatos.Values));

        Assert.Equal(1, Palavras.MaisPerto(["Grave forte", "Grave discreto", "Grave na medida"], fatos)); // "grave discreto"
        Assert.Null(Palavras.MaisPerto(["Música", "Jogo", "Notícias"], fatos));
        Assert.Equal(perfil.Fatos["agudo"], new OuvinteSimulado(perfil, null).FatoMaisPerto("Como você gosta do agudo?"));
        Assert.Equal(new[] { "agudo", "neutro" }, Palavras.De("Agudo NEUTRO, é").Order());
    }

    // ------------------------------------------------------------------ o real e o conferir

    /// <summary>Um lançador que só conta: o <c>real</c> sem a confirmação nunca o chama.</summary>
    private sealed class LancadorContado : ILancadorAgente
    {
        public int Criados;

        public Task<int> RodarAsync(string tarefa, JsonObject? evento, CancellationToken cancelar) => Task.FromResult(0);
    }

    [Fact]
    public async Task O_real_sem_a_confirmacao_mostra_o_custo_e_sai_com_3_sem_lancar_nada()
    {
        var contado = new LancadorContado();
        var texto = new StringWriter();

        var codigo = await Comandos.RodarAsync(["real"], texto, Caminhos.Agente, _ =>
        {
            contado.Criados++;
            return contado;
        });

        saida.WriteLine(texto.ToString());
        Assert.Equal(3, codigo);
        Assert.Contains("US$ 12", texto.ToString());
        Assert.Contains("US$ 1,50 por perfil", texto.ToString());
        Assert.Contains(Comandos.OpcaoConfirmo, texto.ToString());
        Assert.Equal(0, contado.Criados);
        Assert.Equal(2, await Comandos.RodarAsync(["real", "--sem-querer"], new StringWriter(), Caminhos.Agente, _ => contado));
        Assert.Equal(0, contado.Criados);
    }

    [Fact]
    public void O_conferir_passa_so_com_o_hash_das_camadas_da_ultima_avaliacao_real_que_passou()
    {
        using var pasta = new PastaTemporaria();
        var agente = pasta.Caminho;
        var relatorios = Path.Combine(agente, "avaliacao", "relatorios");
        foreach (var camada in HashCamadas.Pastas)
            Directory.CreateDirectory(Path.Combine(agente, camada));
        File.WriteAllText(Path.Combine(agente, "instrucoes", "base.md"), "A base.\r\n");
        File.WriteAllText(Path.Combine(agente, "skills", "a.md"), "Uma skill.\n");
        var hash = HashCamadas.Calcular(agente);

        var antes = DateTimeOffset.Now.AddHours(-1);
        // Nenhuma avaliação real ainda (um relatório falso que passou não conta).
        Relatorio.Montar([Resultado(true)], Relatorio.TipoFalso, hash).Gravar(Path.Combine(relatorios, "1-falso"));
        Assert.Equal((1, Conferencia.MsgNenhuma), Conferencia.Conferir(agente, relatorios));

        // A real que passou com o hash de agora.
        var real = Relatorio.Montar([Resultado(true)], Relatorio.TipoReal, hash, antes);
        real.Gravar(Path.Combine(relatorios, "2-real"));
        Assert.Equal(0, Conferencia.Conferir(agente, relatorios).Codigo);
        Assert.True(File.Exists(Path.Combine(relatorios, "2-real", Relatorio.ArquivoTexto)));
        Assert.Equal(hash, Relatorio.DeJson(File.ReadAllText(Path.Combine(relatorios, "2-real", Relatorio.ArquivoJson))).HashCamadas);

        // Uma real mais nova, com as mesmas camadas, que falhou: decide ela.
        Relatorio.Montar([Resultado(false)], Relatorio.TipoReal, hash, antes.AddMinutes(10)).Gravar(Path.Combine(relatorios, "3-real-falhou"));
        Assert.Equal((1, Conferencia.MsgFalhou), Conferencia.Conferir(agente, relatorios));
        Relatorio.Montar([Resultado(true)], Relatorio.TipoReal, hash, antes.AddMinutes(20)).Gravar(Path.Combine(relatorios, "4-real"));
        Assert.Equal(0, Conferencia.Conferir(agente, relatorios).Codigo);

        // Um relatório estragado fica de fora; um arquivo que não é .md nas camadas não muda o hash.
        Directory.CreateDirectory(Path.Combine(relatorios, "5-estragado"));
        File.WriteAllText(Path.Combine(relatorios, "5-estragado", Relatorio.ArquivoJson), "{ não é json");
        File.WriteAllText(Path.Combine(agente, "skills", "Thumbs.db"), "do sistema");
        Assert.Equal(hash, HashCamadas.Calcular(agente));
        Assert.Equal(0, Conferencia.Conferir(agente, relatorios).Codigo);

        // Só o CR do Windows não muda o hash; o conteúdo e o caminho mudam.
        File.WriteAllText(Path.Combine(agente, "instrucoes", "base.md"), "A base.\n");
        Assert.Equal(hash, HashCamadas.Calcular(agente));
        File.WriteAllText(Path.Combine(agente, "skills", "a.md"), "Uma skill mudada.\n");
        Assert.Equal((1, Conferencia.MsgMudaram), Conferencia.Conferir(agente, relatorios));
        File.WriteAllText(Path.Combine(agente, "skills", "a.md"), "Uma skill.\n");
        Assert.Equal(0, Conferencia.Conferir(agente, relatorios).Codigo);
        File.Move(Path.Combine(agente, "skills", "a.md"), Path.Combine(agente, "skills", "b.md"));
        Assert.Equal((1, Conferencia.MsgMudaram), Conferencia.Conferir(agente, relatorios));
    }

    // ------------------------------------------------------------------ as métricas, uma regra por vez

    private static Perfil PerfilMetricas() => PerfilCom(new AlvoPerfil("081", 2, 4m), new AlvoPerfil("041", 2, 80m));

    /// <summary>Os valores finais certos de <see cref="PerfilMetricas"/> (a rota de sala-fiel), com trocas.</summary>
    private static Func<string, int?, string?> Finais(params (string Codigo, int? Indice, string Valor)[] trocas)
    {
        var valores = new Dictionary<(string, int?), string>
        {
            [("081", 2)] = "+4.0", [("041", 2)] = "80", [("010", 0)] = "a", [("010", 1)] = "b", [("010", 2)] = "a+b",
        };
        foreach (var (codigo, indice, valor) in trocas)
            valores[(codigo, indice)] = valor;
        return (codigo, indice) => valores.GetValueOrDefault((codigo, indice));
    }

    private static PerguntaFeita[] Perguntas(int n) =>
        Enumerable.Range(1, n).Select(i => new PerguntaFeita(i, $"Pergunta {i}?", ["A", "B", "C"], "A", false)).ToArray();

    private static ResultadoPerfil Calcular(Func<string, int?, string?> finais, int perguntas = 3) =>
        Metricas.Calcular(PerfilMetricas(), Perguntas(perguntas), [], [], [], true, finais, [], []);

    [Fact]
    public void Com_tudo_certo_o_perfil_passa()
    {
        var r = Calcular(Finais());
        Assert.True(r.Passou, r.Motivo);
        Assert.Equal(0m, r.Distancia);
    }

    [Fact]
    public void Um_alvo_em_dB_a_2_dB_reprova_pela_distancia()
    {
        var r = Calcular(Finais(("081", 2, "+6.0")));
        Assert.False(r.Passou);
        Assert.Equal(2m, r.Distancia);
        Assert.StartsWith("Distância de 2.0 dB (081[2])", r.Motivo);
        Assert.True(Calcular(Finais(("081", 2, "+5.5"))).Passou); // 1,5 dB ainda passa
    }

    [Fact]
    public void Um_alvo_em_Hz_diferente_reprova_mesmo_perto()
    {
        var r = Calcular(Finais(("041", 2, "81")));
        Assert.False(r.Passou);
        Assert.Equal(0m, r.Distancia);
        Assert.Contains("O 041[2] ficou em 81, o alvo é 80 Hz.", r.Motivo);
    }

    [Fact]
    public void Nove_perguntas_reprovam()
    {
        Assert.True(Calcular(Finais(), perguntas: 8).Passou);
        var r = Calcular(Finais(), perguntas: 9);
        Assert.False(r.Passou);
        Assert.Equal("9 perguntas, acima de 8.", r.Motivo);
    }

    private static LimiteSaida Limite(decimal teto) =>
        new(new SaidaApp(0, [0], "Sub", null), "Sub", "Sub", null, null, null, TipoLimite.Excursao, 10, teto, true, null,
            [new LimiteCanal(0, teto, TipoLimite.Excursao, "Sub")]);

    [Theory]
    [InlineData("-6.0", null)]
    [InlineData("-7.0", null)]
    [InlineData("+0.5", "o limitador ficou desligado")]
    [InlineData(null, "o limitador ficou desconhecido")]
    [InlineData("-5.9", "acima do teto de -6.0 dB")]
    public void Um_limitador_final_off_desconhecido_ou_acima_do_teto_fica_fora_da_cerca(string? final, string? motivo)
    {
        var v = Metricas.ForaDaCerca([Limite(-6.0m)], (codigo, indice) => codigo == "071" && indice == 0 ? final : null);
        if (motivo is null)
        {
            Assert.Empty(v);
            return;
        }
        var unica = Assert.Single(v);
        Assert.Equal("fora-da-cerca", unica.Tipo);
        Assert.Contains(motivo, unica.Detalhe);
    }

    [Theory]
    [InlineData("✗ *071*3*-3.0* (recusado: Limite: Sub (CH4), −6,1 dB, pelo curso do cone.)", "Limite: Sub (CH4), −6,1 dB, pelo curso do cone.")]
    [InlineData("✗ *071*3*-3.0* (recusado: Limite: Sub (CH4))", "Limite: Sub (CH4)")]
    [InlineData("· criação: escuta recusada (Limite: Sub, −6,1 dB, pelo térmico.)", "Limite: Sub, −6,1 dB, pelo térmico.")]
    [InlineData("✗ *081*0*+2.0* (recusado: Sem calibração: Sub. O app não sabe a tensão.)", null)]
    public void A_recusa_da_cerca_e_lida_das_duas_linhas_do_log(string linha, string? motivo) =>
        Assert.Equal(motivo, Avaliador.RecusaDaCerca(linha));

    [Theory]
    [InlineData("setup", "caixas")]
    [InlineData("gosto", "alvos")]
    [InlineData("gosto", "rota")]
    public void Um_item_vazio_no_perfil_e_recusado_com_o_campo(string grupo, string lista)
    {
        var json = JsonNode.Parse(File.ReadAllText(Path.Combine(Perfis.Pasta(Caminhos.Agente), "sala-fiel.json")))!.AsObject();
        json[grupo]![lista]!.AsArray().Add(null);
        var e = Assert.Throws<PerfilInvalidoException>(() => Perfis.Conferir(Perfis.DeJson(json.ToJsonString())));
        Assert.Contains($"{grupo}.{lista}", e.Message);
    }

    // ------------------------------------------------------------------ o real confirmado e o conferir, de ponta a ponta

    [Fact]
    public async Task O_real_confirmado_grava_o_relatorio_real_e_o_conferir_passa_ate_as_camadas_mudarem()
    {
        using var pasta = new PastaTemporaria();
        var agente = pasta.Caminho;
        File.WriteAllText(Path.Combine(agente, "servico.config.json"), "{}");
        Directory.CreateDirectory(Perfis.Pasta(agente));
        File.Copy(Path.Combine(Perfis.Pasta(Caminhos.Agente), "carro-grave.json"), Path.Combine(Perfis.Pasta(agente), "carro-grave.json"));
        foreach (var camada in HashCamadas.Pastas)
            Directory.CreateDirectory(Path.Combine(agente, camada));
        File.WriteAllText(Path.Combine(agente, "instrucoes", "base.md"), "A base.\n");
        var texto = new StringWriter();

        var codigo = await Comandos.RodarAsync(["real", Comandos.OpcaoConfirmo], texto, agente, local => new AgenteBom(local));

        saida.WriteLine(texto.ToString());
        Assert.Equal(0, codigo);
        Assert.Contains("US$ 1,50 por perfil, US$ 1,50 os 1 perfis", texto.ToString());
        var arquivo = Assert.Single(Directory.GetFiles(Conferencia.PastaRelatorios(agente), Relatorio.ArquivoJson, SearchOption.AllDirectories));
        var relatorio = Relatorio.DeJson(File.ReadAllText(arquivo));
        Assert.Equal(Relatorio.TipoReal, relatorio.Tipo);
        Assert.True(relatorio.Passou);
        Assert.Equal(HashCamadas.Calcular(agente), relatorio.HashCamadas);
        Assert.True(File.Exists(Path.Combine(Path.GetDirectoryName(arquivo)!, Relatorio.ArquivoTexto)));

        Assert.Equal(0, await Comandos.RodarAsync(["conferir"], new StringWriter(), agente));
        File.WriteAllText(Path.Combine(agente, "instrucoes", "base.md"), "A base mudada.\n");
        var conferir = new StringWriter();
        Assert.Equal(1, await Comandos.RodarAsync(["conferir"], conferir, agente));
        Assert.Contains(Conferencia.MsgMudaram, conferir.ToString());
    }

    private static ResultadoPerfil Resultado(bool passou) =>
        new("teste", [], 0, 0m, [], [], [], true, [], [], true, passou, passou ? "Passou." : "Não.");
}
