using System.Diagnostics;
using System.Text.Json.Nodes;
using Agente.ModeloFalso;
using Agente.Porta;
using Agente.Servico;
using BgsControle.Nucleo.Setups;

namespace Agente.Testes;

/// <summary>
/// O serviço que acorda o Agente (história 14a), de ponta a ponta: a <c>PortaComandos</c> do núcleo sobre o simulador, o
/// serviço com o modelo falso (um processo que roda o roteiro pela ferramenta <c>porta</c>, outro processo), e o usuário
/// direto no <see cref="CriacaoMemoria"/>. As linhas Acordar, Um por vez, Ponta a ponta, Prazo e Porta cai da matriz.
/// </summary>
public sealed class ServicoTestes
{
    private static readonly TimeSpan Longo = TimeSpan.FromSeconds(45);

    private static async Task ParearAsync(Bancada bancada, string local)
    {
        var entrada = new JsonObject { ["acao"] = "parear", ["endereco"] = bancada.Endereco, ["codigo"] = bancada.Pareamento.Codigo }.ToJsonString();
        var r = await FerramentaPorta.ExecutarAsync(entrada, local);
        Assert.True(r.Ok, r.Motivo);
    }

    private static LancadorModeloFalso ModeloFalso(string roteiro, string local) =>
        new(new Dictionary<string, string> { ["criar-memoria"] = Caminhos.Roteiro(roteiro) }, local);

    private static async Task<JsonObject> LerCriacaoAsync(string local)
    {
        var (codigo, saida, json) = await PortaCli.RodarAsync("""{"acao":"ler","alvo":"/criacao"}""", local);
        Assert.True(codigo == 0, saida);
        return json["corpo"]!.AsObject();
    }

    [Fact]
    public async Task Criacao_pedida_acorda_o_Agente_e_a_criacao_abre_em_ate_15_s_e_um_segundo_pedido_nao_lanca_outro()
    {
        await using var bancada = await Bancada.ProntaAsync();
        using var local = new LocalTemporario();
        await ParearAsync(bancada, local.Local);
        await using var servico = new ServicoRodando(Caminhos.Config, ModeloFalso("criacao-basica", local.Local), local.Local);
        await servico.EsperarLinha("Começando do evento");

        var relogio = Stopwatch.StartNew();
        Assert.True(bancada.Criacao.Pedir().Ok); // o toque no "+ Nova memória"
        await Esperar.Ate(() => bancada.Criacao.PerguntaAtual is not null, TimeSpan.FromSeconds(15), "a criação abrir com a primeira pergunta");
        relogio.Stop();

        var criacao = await LerCriacaoAsync(local.Local);
        Assert.Equal("perguntas", criacao["etapa"]!.GetValue<string>());
        Assert.Single(criacao["perguntas"]!.AsArray());
        Assert.True(relogio.Elapsed < TimeSpan.FromSeconds(15), $"abriu em {relogio.Elapsed.TotalSeconds:0.0} s");
        Assert.True(servico.Tem("Agente acordado pelo criacao-pedida: tarefa criar-memoria."));

        // Um 2º toque com a criação aberta não vira pedido, e nenhum Agente novo sai.
        Assert.False(bancada.Criacao.Pedir().Ok);
        await Task.Delay(TimeSpan.FromSeconds(2.5));
        Assert.Equal(1, servico.Servico.Lancamentos);
        Assert.True(servico.Servico.AgenteRodando);
        Assert.True(servico.Servico.Ultimo >= 1);

        await servico.PararAsync(); // o serviço parando encerra o Agente
        await servico.EsperarLinha("Serviço parando: o processo do Agente foi encerrado.");
        await servico.EsperarLinha("Agente terminou: código nenhum");
        bancada.NadaDeMemoriaDoAparelho();
    }

    [Fact]
    public async Task Segundo_pedido_com_o_Agente_rodando_e_ignorado_com_a_linha_no_log()
    {
        await using var bancada = await Bancada.ProntaAsync();
        using var local = new LocalTemporario();
        await ParearAsync(bancada, local.Local);
        await using var servico = new ServicoRodando(Caminhos.Config, ModeloFalso("trava", local.Local), local.Local);
        await servico.EsperarLinha("Começando do evento");

        // Dois toques antes de o Agente abrir a criação: dois criacao-pedida.
        Assert.True(bancada.Criacao.Pedir().Ok);
        Assert.True(bancada.Criacao.Pedir().Ok);

        await servico.EsperarLinha(ServicoAgente.MsgJaRodando);
        Assert.Equal(1, servico.Servico.Lancamentos);
        Assert.Equal(1, servico.Quantas("Agente acordado"));
        Assert.Equal(1, servico.Quantas(ServicoAgente.MsgJaRodando));
        await Esperar.Ate(() => bancada.Criacao.Aberta, Longo, "o Agente abrir a criação");
    }

    [Fact]
    public async Task Roteiro_basico_de_ponta_a_ponta_duas_perguntas_dois_ajustes_relidos_e_o_como_ficou_na_tela()
    {
        await using var bancada = await Bancada.ProntaAsync();
        using var local = new LocalTemporario();
        await ParearAsync(bancada, local.Local);
        await using var servico = new ServicoRodando(Caminhos.Config, ModeloFalso("criacao-basica", local.Local), local.Local);
        await servico.EsperarLinha("Começando do evento");
        var c = bancada.Criacao;

        Assert.True(c.Pedir().Ok);

        // 1ª pergunta: a resposta escolhida decide a 2ª (o roteiro ramifica pela resposta).
        await Esperar.Ate(() => c.PerguntaAtual is { Numero: 1 }, Longo, "a 1ª pergunta");
        Assert.False(c.PerguntaAtual!.Ultima);
        Assert.True(c.Responder(1).Ok); // "O máximo que o sistema aguenta"
        await Esperar.Ate(() => c.PerguntaAtual is { Numero: 2 }, Longo, "a 2ª pergunta");
        Assert.True(c.PerguntaAtual!.Ultima);
        Assert.Equal("Por quanto tempo seguido o som fica alto?", c.PerguntaAtual.Texto);
        Assert.True(c.Responder(0).Ok);

        // Os 2 ajustes, cada um com a frase e o motivo, relidos iguais; o resumo fecha a rodada.
        await Esperar.Ate(() => c.Etapa == EtapaCriacao.Resumo, Longo, "o resumo");
        Assert.Equal(2, c.Ajustes.Count);
        Assert.All(c.Ajustes, a =>
        {
            Assert.False(string.IsNullOrWhiteSpace(a.Frase));
            Assert.False(string.IsNullOrWhiteSpace(a.Motivo));
            Assert.False(a.Diferente);
            Assert.Equal(Numero(a.Novo), Numero(a.Relido));
        });
        Assert.Equal(2.0m, Numero(bancada.Simulador.Valor("081", 2)));
        Assert.Equal(-1.0m, Numero(bancada.Simulador.Valor("081", 0)));

        // O "Como ficou?" chega e vai para a tela.
        await Esperar.Ate(() => c.ComoFicou.Count == 1, Longo, "o \"Como ficou?\" chegar");
        Assert.True(c.AvaliarSom().Ok);
        Assert.Equal(EtapaCriacao.ComoFicou, c.Etapa);
        Assert.Equal(["Quero mais grave", "Está alto demais"], c.ComoFicouAtual!.Respostas);
        var criacao = await LerCriacaoAsync(local.Local);
        Assert.Equal("como-ficou", criacao["etapa"]!.GetValue<string>());
        Assert.Equal(2, criacao["perguntas"]!.AsArray().Count);
        Assert.Equal(2, criacao["ajustes"]!.AsArray().Count);

        // O usuário aprova e salva; o Agente vê a criacao-salva e termina com 0; o serviço fecha a tarefa.
        Assert.True(c.PodeSalvar().Ok);
        Assert.True((await c.SalvarAsync("TESTE A")).Ok);
        await servico.EsperarLinha("Agente terminou: código 0", Longo);
        Assert.True(servico.Tem("Tarefa terminada (DELETE /tarefa)."));
        Assert.False(servico.Servico.AgenteRodando);
        Assert.False(c.Aberta);

        // O último evento lido fica guardado; a chave não vai ao log.
        Assert.Equal(servico.Servico.Ultimo, JsonNode.Parse(local.Ler(ServicoAgente.ArquivoEstado)!)!["ultimo"]!.GetValue<long>());
        var chave = PareamentoLocal.Ler(local.Local)!.Chave;
        Assert.DoesNotContain(chave, local.Ler(ServicoAgente.ArquivoLog));
        Assert.DoesNotContain(chave, local.Ler("agente.log") ?? "");
        // Cada linha do log começa com a data e a hora.
        var linhas = local.Ler(ServicoAgente.ArquivoLog)!.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.NotEmpty(linhas);
        Assert.All(linhas, l => Assert.Matches(@"^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2} ", l));
        bancada.NadaDeMemoriaDoAparelho();
    }

    [Fact]
    public async Task Prazo_vencido_encerra_o_processo_cancela_a_criacao_aberta_e_registra()
    {
        await using var bancada = await Bancada.ProntaAsync();
        using var local = new LocalTemporario();
        await ParearAsync(bancada, local.Local);
        var lancador = ModeloFalso("trava", local.Local);
        var config = Caminhos.Config with { PrazoAgenteMin = 8.0 / 60 }; // o prazo curto do teste: 8 s
        await using var servico = new ServicoRodando(config, lancador, local.Local);
        await servico.EsperarLinha("Começando do evento");

        Assert.True(bancada.Criacao.Pedir().Ok);
        await Esperar.Ate(() => bancada.Criacao.Aberta, TimeSpan.FromSeconds(8), "o Agente abrir a criação antes do prazo");

        await servico.EsperarLinha("Agente terminou:", Longo);
        Assert.True(servico.Tem("Prazo de 8 s vencido: o processo do Agente foi encerrado."));
        Assert.True(servico.Tem("Criação aberta cancelada (POST /criacao/cancelar)."));
        Assert.True(servico.Tem("Agente terminou: código nenhum"));
        Assert.False(bancada.Criacao.Aberta);
        Assert.Contains(bancada.Porta.Eventos(), e => e.Tipo == CriacaoMemoria.EventoCancelada && e.Criacao?.Por == CriacaoMemoria.PorPorta);
        Assert.True(ProcessoTerminou(lancador.UltimoPid!.Value));
        Assert.False(servico.Servico.AgenteRodando);
        bancada.NadaDeMemoriaDoAparelho();
    }

    [Fact]
    public async Task Porta_que_cai_o_servico_tenta_de_novo_e_volta_a_ler_quando_ela_volta()
    {
        await using var bancada = await Bancada.ProntaAsync();
        using var local = new LocalTemporario();
        await ParearAsync(bancada, local.Local);
        var lancador = new LancadorQueConta();
        var config = Caminhos.Config with { EsperaQuedaS = 0.5 };
        await using var servico = new ServicoRodando(config, lancador, local.Local);
        await servico.EsperarLinha(ServicoAgente.MsgConectado);

        bancada.DerrubarPorta();
        await servico.EsperarLinha("Porta sem resposta");
        await Task.Delay(TimeSpan.FromSeconds(1.5)); // várias tentativas: uma linha só por mudança de estado
        Assert.Equal(1, servico.Quantas("Porta sem resposta"));

        await bancada.ReabrirPortaAsync();
        await Esperar.Ate(() => servico.Quantas(ServicoAgente.MsgConectado) == 2, TimeSpan.FromSeconds(10), "a porta voltar");
        Assert.True(bancada.Criacao.Pedir().Ok);
        await Esperar.Ate(() => lancador.Tarefas.Count == 1, TimeSpan.FromSeconds(10), "o pedido depois da volta acordar o Agente");
        Assert.Equal("criar-memoria", lancador.Tarefas[0]);
    }

    [Fact]
    public async Task Ao_subir_o_servico_comeca_do_evento_atual_sem_reprocessar_o_passado()
    {
        await using var bancada = await Bancada.ProntaAsync();
        using var local = new LocalTemporario();
        await ParearAsync(bancada, local.Local);
        Assert.True(bancada.Criacao.Pedir().Ok); // um pedido de antes do serviço
        var lancador = new LancadorQueConta();

        await using var servico = new ServicoRodando(Caminhos.Config, lancador, local.Local);
        await servico.EsperarLinha("Começando do evento");
        await Task.Delay(TimeSpan.FromSeconds(2.5)); // mais duas leituras

        Assert.Empty(lancador.Tarefas);
        Assert.True(servico.Servico.Ultimo >= 1);
    }

    [Fact]
    public async Task Sem_pareamento_o_servico_registra_a_recusa_uma_vez_e_segue_tentando()
    {
        using var local = new LocalTemporario();
        var config = Caminhos.Config with { EsperaQuedaS = 0.2 };

        await using var servico = new ServicoRodando(config, new LancadorQueConta(), local.Local);
        await servico.EsperarLinha(ServicoAgente.MsgRecusada(FerramentaPorta.MsgSemPareamento));
        await Task.Delay(TimeSpan.FromSeconds(1));

        Assert.Equal(1, servico.Quantas("A porta recusou"));
        Assert.Null(servico.Servico.Ultimo);
    }

    [Fact]
    public async Task App_reaberto_com_um_pedido_antes_da_primeira_leitura_depois_da_volta_acorda_o_Agente()
    {
        await using var bancada = await Bancada.ProntaAsync();
        using var local = new LocalTemporario();
        await ParearAsync(bancada, local.Local);
        var lancador = new LancadorQueConta();
        await using var servico = new ServicoRodando(Caminhos.Config with { EsperaQuedaS = 0.5 }, lancador, local.Local);
        await servico.EsperarLinha("Começando do evento");
        Assert.True(bancada.Criacao.Pedir().Ok);
        await Esperar.Ate(() => lancador.Tarefas.Count == 1 && !servico.Servico.AgenteRodando, TimeSpan.FromSeconds(10), "o 1º pedido");
        Assert.True(servico.Servico.Ultimo > 0);

        // O app reabre: a contagem recomeça, e o pedido novo (n = 1) chega antes da primeira leitura depois da volta.
        bancada.DerrubarPorta();
        await servico.EsperarLinha("Porta sem resposta");
        await bancada.ReabrirPortaAsync(() => Assert.True(bancada.Criacao.Pedir().Ok));

        await Esperar.Ate(() => lancador.Tarefas.Count == 2, TimeSpan.FromSeconds(10), "o pedido do app reaberto acordar o Agente");
        await Task.Delay(TimeSpan.FromSeconds(2)); // e só uma vez
        Assert.Equal(2, lancador.Tarefas.Count);
    }

    [Fact]
    public async Task Agente_que_sai_sozinho_com_a_criacao_nas_perguntas_tem_a_criacao_cancelada()
    {
        await using var bancada = await Bancada.ProntaAsync();
        using var local = new LocalTemporario();
        await ParearAsync(bancada, local.Local);
        var roteiro = Path.Combine(local.Raiz, "so-abre.json");
        File.WriteAllText(roteiro, """{ "passos": [ { "pedir": { "metodo": "POST", "alvo": "/criacao/abrir" } } ] }""");
        var lancador = new LancadorModeloFalso(new Dictionary<string, string> { ["criar-memoria"] = roteiro }, local.Local);
        await using var servico = new ServicoRodando(Caminhos.Config, lancador, local.Local);
        await servico.EsperarLinha("Começando do evento");

        Assert.True(bancada.Criacao.Pedir().Ok);

        await servico.EsperarLinha("Agente terminou: código 0", Longo);
        Assert.True(servico.Tem(ServicoAgente.MsgCriacaoCancelada));
        Assert.False(bancada.Criacao.Aberta);
        Assert.Contains(bancada.Porta.Eventos(), e => e.Tipo == CriacaoMemoria.EventoCancelada && e.Criacao?.Por == CriacaoMemoria.PorPorta);
        Assert.True(servico.Tem("Tarefa terminada (DELETE /tarefa).")); // não havia tarefa antes deste Agente
    }

    [Fact]
    public async Task Lancador_que_falha_depois_de_abrir_tem_a_criacao_cancelada_e_o_servico_segue()
    {
        await using var bancada = await Bancada.ProntaAsync();
        using var local = new LocalTemporario();
        await ParearAsync(bancada, local.Local);
        await using var servico = new ServicoRodando(Caminhos.Config, new LancadorQueAbreEFalha(local.Local), local.Local);
        await servico.EsperarLinha("Começando do evento");

        Assert.True(bancada.Criacao.Pedir().Ok);

        await servico.EsperarLinha("Agente terminou: código nenhum", Longo);
        Assert.True(servico.Tem("Não consegui rodar o Agente: falhou de propósito"));
        Assert.True(servico.Tem(ServicoAgente.MsgCriacaoCancelada));
        Assert.False(bancada.Criacao.Aberta);
        Assert.False(servico.Servico.AgenteRodando);

        // O lugar do Agente ficou livre: um pedido novo acorda outro.
        Assert.True(bancada.Criacao.Pedir().Ok);
        await Esperar.Ate(() => servico.Servico.Lancamentos == 2, TimeSpan.FromSeconds(10), "o 2º Agente");
    }

    [Fact]
    public async Task A_tarefa_que_ja_existia_antes_do_Agente_nao_e_apagada_no_fim()
    {
        await using var bancada = await Bancada.ProntaAsync();
        using var local = new LocalTemporario();
        await ParearAsync(bancada, local.Local);
        var tarefa = await FerramentaPorta.ExecutarAsync("""{"acao":"pedir","metodo":"POST","alvo":"/tarefa","corpo":{"frase":"outra tarefa"}}""", local.Local);
        Assert.True(tarefa.Ok, tarefa.Motivo);
        await using var servico = new ServicoRodando(Caminhos.Config, new LancadorQueConta(), local.Local);
        await servico.EsperarLinha("Começando do evento");

        Assert.True(bancada.Criacao.Pedir().Ok);

        await servico.EsperarLinha("Agente terminou: código 0");
        Assert.False(servico.Tem("Tarefa terminada"));
        var estado = await FerramentaPorta.ExecutarAsync("""{"acao":"ler","alvo":"/estado"}""", local.Local);
        Assert.Equal("outra tarefa", estado.Corpo!["ia"]!["tarefa"]!.GetValue<string>());
    }

    private sealed class LancadorQueAbreEFalha(string local) : ILancadorAgente
    {
        public async Task<int> RodarAsync(string tarefa, CancellationToken cancelar)
        {
            using var cliente = new ClientePorta();
            var r = await FerramentaPorta.PedirAsync(local, "POST", "/criacao/abrir", null, cliente, cancelar);
            Assert.True(r.Ok, r.Motivo);
            throw new InvalidOperationException("falhou de propósito");
        }
    }

    private static decimal? Numero(string? texto) =>
        texto is null ? null : decimal.Parse(texto, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture);

    private static bool ProcessoTerminou(int pid)
    {
        try
        {
            using var p = Process.GetProcessById(pid);
            return p.HasExited;
        }
        catch (ArgumentException)
        {
            return true; // não existe mais
        }
    }
}
