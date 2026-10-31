using System.Globalization;
using System.Net;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using BgsControle.Nucleo;
using BgsControle.Nucleo.Escrita;
using BgsControle.Nucleo.Porta;
using BgsControle.Nucleo.Seguranca;
using BgsControle.Nucleo.Setups;
using BgsControle.Nucleo.Uso;

namespace BgsControle.Telas.Testes;

/// <summary>
/// A história 9d nas telas, contra o processador simulado: o lado do usuário da criação da memória (CAP-18), sem
/// XAML. A pergunta com as respostas e o "Outro…", os ajustes da rodada, o resumo, "Avaliar o som", o "Como
/// ficou?" com "Ótimo, pode salvar", o Salvar com o nome, "Ajustar mais" e o ✕ com a folha "Cancelar a nova
/// memória?". As perguntas e os ajustes chegam pela porta, como o cliente pareado manda.
/// </summary>
public sealed class CriacaoMemoriaViewModelTestes : IDisposable
{
    private static readonly IPAddress Casa = IPAddress.Parse("192.168.0.10");
    private static readonly Regex PalavraIa = new(@"\bIA\b", RegexOptions.CultureInvariant);

    private readonly Bancada _b = new(usoCompartilhado: true, setups: true);
    private readonly Pareamento _pareamento;
    private readonly PedidosConfirmacao _confirmacoes;
    private readonly CriacaoMemoria _criacao;
    private readonly PortaComandos _porta;
    private readonly CriacaoMemoriaViewModel _vm;

    public CriacaoMemoriaViewModelTestes()
    {
        _pareamento = new Pareamento(new RepositorioTextoArquivos(_b.ArquivosUso, RepositorioTextoArquivos.ArquivoPortaIa), _b.Relogio, _b.Log);
        _confirmacoes = new PedidosConfirmacao(_b.Relogio, _b.Log);
        _criacao = new CriacaoMemoria(_b.Sessao, _b.Estado, _b.Escritor, _b.Copia, _b.Setups!, _b.Uso!, _b.Log);
        _porta = new PortaComandos(_b.Sessao, _b.Chaves, _b.Uso!, _b.Historico!, _pareamento, _confirmacoes, new RedeFalsa(), _b.Estado,
            _b.Escritor, _b.Setups!, _b.Gerador, _b.Comandos, _b.Monitor, _b.Log, _b.Relogio, 0, _criacao);
        _vm = new CriacaoMemoriaViewModel(_criacao, _b.Tela, _b.Relogio);
    }

    public void Dispose()
    {
        _vm.Dispose();
        _porta.Dispose();
        _criacao.Dispose();
        _b.ConferirSoAudio();
        Assert.Empty(_b.Tela.Erros);
    }

    // ----------------------------------------------------------------- apoio

    private async Task<RespostaPorta> Pedir(string metodo, string alvo, string? corpo = null, string? chave = null)
    {
        var tarefa = _porta.AtenderAsync(PedidoPorta.De(metodo, alvo, Casa, corpo, chave));
        await _b.Rodar(tarefa);
        return await tarefa;
    }

    private static JsonElement Json(RespostaPorta r) => JsonDocument.Parse(r.Json).RootElement;

    /// <summary>Conecta (o Casa 2.1 com a FILME), liga a chave "Controle pela rede" e pareia.</summary>
    private async Task<string> Pronto()
    {
        await _b.Historico!.Carregado;
        await _pareamento.Carregado;
        await _b.Conectar();
        Assert.True((await _b.Chaves.DefinirAsync(Chave.ControlePelaRede, true, OrigemComando.Tela)).Ok);
        await _porta.AtualizarAsync();
        var r = await Pedir("POST", "/parear", $$"""{"codigo":"{{_pareamento.Codigo}}","nome":"Claude"}""");
        Assert.Equal(200, r.Status);
        return Json(r).GetProperty("chave").GetString()!;
    }

    private async Task<string> Aberta()
    {
        var chave = await Pronto();
        Assert.Equal(200, (await Pedir("POST", "/criacao/abrir", null, chave)).Status);
        await _b.Tela.EsvaziarAsync();
        return chave;
    }

    private async Task Pergunta(string chave, bool ultima, string texto = "Para que é esta memória?", string respostas = """["Música","Filme","Jogo"]""")
    {
        var r = await Pedir("POST", "/criacao/pergunta", $$"""{"texto":"{{texto}}","respostas":{{respostas}},"ultima":{{(ultima ? "true" : "false")}}}""", chave);
        Assert.Equal(200, r.Status);
        await _b.Tela.EsvaziarAsync();
    }

    private async Task Ajuste(string chave, string codigo = "081", int? indice = 2, decimal valor = 2.0m, string frase = "Mais corpo no sub")
    {
        var campoIndice = indice is { } i ? $",\"indice\":{i}" : "";
        var r = await Pedir("POST", "/criacao/ajuste",
            $$"""{"codigo":"{{codigo}}"{{campoIndice}},"valor":{{valor.ToString(CultureInfo.InvariantCulture)}},"frase":"{{frase}}","motivo":"A resposta pediu."}""",
            chave);
        Assert.Equal(200, r.Status);
        await _b.Tela.EsvaziarAsync();
    }

    private async Task Resumo(string chave)
    {
        Assert.Equal(200, (await Pedir("POST", "/criacao/resumo", null, chave)).Status);
        await _b.Tela.EsvaziarAsync();
    }

    private async Task ComoFicou(string chave)
    {
        Assert.Equal(200, (await Pedir("POST", "/criacao/como-ficou", """{"respostas":["Quero mais impacto","Os graves embolaram"]}""", chave)).Status);
        await _b.Tela.EsvaziarAsync();
    }

    /// <summary>Abre, a última pergunta e a resposta do usuário pelo ViewModel: os ajustes estão autorizados.</summary>
    private async Task<string> Autorizada()
    {
        var chave = await Aberta();
        await Pergunta(chave, ultima: true);
        await _vm.Responder.ExecutarAsync(0);
        await _b.Tela.EsvaziarAsync();
        Assert.True(_vm.Ajustando);
        return chave;
    }

    private string[] TiposDosEventos() =>
        _porta.Eventos().Where(e => e.Criacao is not null).Select(e => e.Tipo).ToArray();

    /// <summary>Cada parâmetro de áudio do aparelho (relido do simulador) bate com a cópia de antes.</summary>
    private void IgualACopia(Copia copia)
    {
        foreach (var c in Catalogo.ConsultasMarco1)
        {
            var guardado = copia.Valores.FirstOrDefault(v => v.Codigo == c.Parametro.Codigo && v.Indice == c.Indice);
            if (guardado is null)
                continue;
            var agora = _b.Simulador.Valor(c.Parametro.Codigo, c.Indice);
            Assert.True(agora is not null && Camadas.Iguais(c.Parametro.Codigo, agora, guardado.NoFio),
                $"{c.Parametro.Codigo}/{c.Indice}: {agora} no aparelho, {guardado.NoFio} na cópia");
        }
    }

    private void SemIa()
    {
        foreach (var p in typeof(CriacaoMemoriaViewModel).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var valor = p.GetValue(_vm);
            var textos = valor switch
            {
                string s => [s],
                IEnumerable<string> lista => lista.ToArray(),
                IEnumerable<LinhaCriacao> linhas => linhas.SelectMany(l => new[] { l.Rotulo, l.Antes, l.Valor, l.Frase, l.Linha }).ToArray(),
                _ => Array.Empty<string>(),
            };
            foreach (var t in textos)
                Assert.False(PalavraIa.IsMatch(t), $"{p.Name} mostra a IA: \"{t}\"");
        }
    }

    // ----------------------------------------------------------------- matriz

    [Fact]
    public async Task Fechada_nada_aparece()
    {
        await _b.Tela.EsvaziarAsync();

        Assert.False(_vm.Aberta);
        Assert.False(_vm.PodeFechar);
        Assert.Null(_vm.TextoPergunta);
        Assert.Empty(_vm.RespostasPergunta);
        await _vm.Fechar.ExecutarAsync();
        Assert.False(_vm.FolhaCancelarAberta);
    }

    [Fact]
    public async Task A_pergunta_com_3_respostas_aparece_com_o_Outro()
    {
        var chave = await Aberta();
        Assert.True(_vm.Aberta);
        Assert.True(_vm.NasPerguntas);
        Assert.False(_vm.TemPergunta);
        Assert.Equal(CriacaoMemoriaViewModel.TextoEsperandoPergunta, _vm.TextoEspera);
        Assert.Equal(_b.Setups!.SetupEmUso!.Nome, _vm.NomeSetup);

        await Pergunta(chave, ultima: false);

        Assert.True(_vm.TemPergunta);
        Assert.Equal(1, _vm.NumeroPergunta);
        Assert.Equal("Para que é esta memória?", _vm.TextoPergunta);
        Assert.Equal(["Música", "Filme", "Jogo"], _vm.RespostasPergunta);
        Assert.Equal("Outro…", _vm.TextoOutro);
        Assert.True(_vm.PodeFechar);
        Assert.Null(_vm.TextoEspera);
        SemIa();
    }

    [Fact]
    public async Task O_toque_na_2a_resposta_vai_a_porta_e_o_Outro_vazio_nao_responde()
    {
        var chave = await Aberta();
        await Pergunta(chave, ultima: false);

        _vm.TextoDoOutro = "   ";
        await _vm.ResponderOutro.ExecutarAsync();
        Assert.Equal(CriacaoMemoria.MsgOutroVazio, _vm.Aviso);
        Assert.True(_vm.TemPergunta);
        Assert.Empty(TiposDosEventos());

        await _vm.Responder.ExecutarAsync(1);
        await _b.Tela.EsvaziarAsync();

        Assert.Null(_vm.Aviso);
        Assert.False(_vm.TemPergunta);
        var eventos = Json(await Pedir("GET", "/eventos?desde=0", chave: chave)).GetProperty("eventos").EnumerateArray()
            .Where(e => e.GetProperty("tipo").GetString() == CriacaoMemoria.EventoResposta).ToArray();
        var e = Assert.Single(eventos);
        Assert.Equal(1, e.GetProperty("numero").GetInt32());
        Assert.Equal("Filme", e.GetProperty("texto").GetString());

        // O "Outro…" com texto responde a segunda (a última): os ajustes passam a valer.
        await Pergunta(chave, ultima: true, texto: "Onde você senta?");
        Assert.Null(_vm.TextoDoOutro); // a pergunta nova nasce com o "Outro…" vazio
        _vm.TextoDoOutro = "No pufe";
        await _vm.ResponderOutro.ExecutarAsync();
        await _b.Tela.EsvaziarAsync();
        Assert.Null(_vm.TextoDoOutro);
        Assert.True(_vm.Ajustando);
        Assert.False(_vm.PodeFechar); // o ✕ é das perguntas e do "Como ficou?"
        Assert.Equal(CriacaoMemoriaViewModel.TextoAjustando, _vm.TextoEspera);
        Assert.True(_criacao.Perguntas[1].DoOutro);
    }

    [Fact]
    public async Task Os_ajustes_da_rodada_aparecem_com_a_frase_e_o_resumo_conta_os_relidos()
    {
        var chave = await Autorizada();

        await Ajuste(chave, frase: "Mais corpo no sub");
        await Ajuste(chave, codigo: "081", indice: 0, valor: 8.0m, frase: "Frente mais presente");

        Assert.Equal(2, _vm.AjustesDaRodada.Count);
        var primeira = _vm.AjustesDaRodada[0];
        Assert.Equal("Ganho da saída 3", primeira.Rotulo);
        Assert.Equal("+2,0 dB", primeira.Valor);
        Assert.Equal("Mais corpo no sub", primeira.Frase);
        Assert.Equal($"Ganho da saída 3: {primeira.Antes} → +2,0 dB", primeira.Linha);

        await Resumo(chave);

        Assert.True(_vm.NoResumo);
        Assert.Equal("Pronto. Ouça um pouco.", _vm.TituloResumo);
        Assert.Equal("2 ajustes no aparelho, todos relidos.", _vm.TextoResumo);
        Assert.Equal(2, _vm.LinhasResumo.Count);
        Assert.All(_vm.LinhasResumo, l => Assert.False(l.Destaque)); // na primeira rodada, nada em destaque
        Assert.Equal("Avaliar o som", _vm.TextoAvaliar);
        Assert.False(_vm.PodeFechar);
        SemIa();
    }

    [Fact]
    public async Task Refino_Avaliar_o_som_Quero_mais_impacto_e_o_resumo_destaca_so_a_rodada_2()
    {
        var chave = await Autorizada();
        await Ajuste(chave);
        await Resumo(chave);
        await ComoFicou(chave);

        await _vm.Avaliar.ExecutarAsync();
        await _b.Tela.EsvaziarAsync();
        Assert.True(_vm.NoComoFicou);
        Assert.Equal("Como ficou?", _vm.TituloComoFicou);
        Assert.Equal("Ótimo, pode salvar", _vm.TextoPodeSalvar);
        Assert.Equal(["Quero mais impacto", "Os graves embolaram"], _vm.RespostasComoFicou);
        Assert.True(_vm.PodeFechar);
        SemIa();

        await _vm.Responder.ExecutarAsync(0);
        await _b.Tela.EsvaziarAsync();

        Assert.True(_vm.Ajustando);
        var e = _porta.Eventos().Last(x => x.Tipo == CriacaoMemoria.EventoComoFicou).Criacao!;
        Assert.Equal("Quero mais impacto", e.Texto);
        Assert.False(e.Salvar);
        Assert.Empty(_vm.AjustesDaRodada); // rodada nova

        await Ajuste(chave, codigo: "081", indice: 0, valor: 8.0m, frase: "Mais impacto na frente");
        await Resumo(chave);

        Assert.Equal("Mudou 1 ajuste, em destaque. O resto ficou igual.", _vm.TextoResumo);
        Assert.Equal(2, _vm.LinhasResumo.Count);
        Assert.False(_vm.LinhasResumo[0].Destaque);
        Assert.True(_vm.LinhasResumo[1].Destaque);
        Assert.Equal("Mais impacto na frente", _vm.LinhasResumo[1].Frase);
    }

    [Fact]
    public async Task Otimo_pode_salvar_o_nome_repetido_fica_no_Salvar_Ajustar_mais_volta_e_Salvar_cria_em_uso()
    {
        var chave = await Autorizada();
        await Ajuste(chave);
        await Resumo(chave);
        await ComoFicou(chave);
        await _vm.Avaliar.ExecutarAsync();

        await _vm.PodeSalvar.ExecutarAsync();
        await _b.Tela.EsvaziarAsync();
        Assert.True(_vm.NoSalvar);
        Assert.False(_vm.PodeFechar);
        Assert.True(_porta.Eventos().Last(x => x.Tipo == CriacaoMemoria.EventoComoFicou).Criacao!.Salvar);

        _vm.NomeMemoria = "FILME";
        await _vm.Salvar.ExecutarAsync();
        await _b.Tela.EsvaziarAsync();
        Assert.Equal(GerenteSetups.MsgNomeRepetidoMemoria(_b.Setups!.SetupEmUso!.Nome), _vm.ErroNome);
        Assert.True(_vm.NoSalvar);

        await _vm.AjustarMais.ExecutarAsync();
        await _b.Tela.EsvaziarAsync();
        Assert.True(_vm.NoComoFicou);
        Assert.Equal(["Quero mais impacto", "Os graves embolaram"], _vm.RespostasComoFicou);
        await _vm.PodeSalvar.ExecutarAsync();
        await _b.Tela.EsvaziarAsync();

        _vm.NomeMemoria = "MUSICA ORIG";
        Assert.Null(_vm.ErroNome); // o nome novo apaga o erro
        await _vm.Salvar.ExecutarAsync();
        await _b.Tela.EsvaziarAsync();

        Assert.False(_vm.Aberta);
        Assert.Equal("MUSICA ORIG", _b.Setups.MemoriaEmUso!.Nome);
        Assert.Null(_b.Copia.Ultima!.Operacao);
        Assert.Contains(CriacaoMemoria.EventoSalva, TiposDosEventos());
    }

    [Fact]
    public async Task O_X_abre_a_folha_Continuar_criando_fecha_e_Cancelar_e_voltar_volta_a_FILME()
    {
        var chave = await Aberta();
        var copia = _b.Copia.Ultima!;
        await Pergunta(chave, ultima: true);

        await _vm.Fechar.ExecutarAsync();
        Assert.True(_vm.FolhaCancelarAberta);
        Assert.Equal("Cancelar a nova memória?", _vm.TituloFolhaCancelar);
        Assert.Equal("Nada é salvo e o aparelho volta para a FILME, como estava antes.", _vm.TextoFolhaCancelar);
        Assert.Equal("Cancelar e voltar", _vm.TextoCancelarEVoltar);
        Assert.Equal("Continuar criando", _vm.TextoContinuarCriando);
        SemIa();

        await _vm.ContinuarCriando.ExecutarAsync();
        Assert.False(_vm.FolhaCancelarAberta);
        Assert.True(_vm.Aberta);

        // Responde, ajusta, e o ✕ do "Como ficou?" volta tudo.
        await _vm.Responder.ExecutarAsync(0);
        await Ajuste(chave);
        await Ajuste(chave, codigo: "081", indice: 0, valor: 8.0m);
        await Resumo(chave);
        await _vm.Avaliar.ExecutarAsync();
        await _b.Tela.EsvaziarAsync();
        var memorias = _b.Setups!.Dados.Memorias.Count;
        await _vm.Fechar.ExecutarAsync();
        Assert.True(_vm.FolhaCancelarAberta);

        await _b.Rodar(_vm.CancelarEVoltar.ExecutarAsync());

        Assert.False(_vm.Aberta);
        Assert.False(_vm.FolhaCancelarAberta);
        IgualACopia(copia);
        Assert.Equal(memorias, _b.Setups.Dados.Memorias.Count);
        var e = _porta.Eventos().Single(x => x.Tipo == CriacaoMemoria.EventoCancelada).Criacao!;
        Assert.Equal(CriacaoMemoria.PorUsuario, e.Por);
        Assert.True(e.Voltou);
    }

    [Fact]
    public async Task O_X_nao_abre_a_folha_fora_das_perguntas_e_do_Como_ficou()
    {
        var chave = await Autorizada();
        await Ajuste(chave);

        await _vm.Fechar.ExecutarAsync();
        Assert.False(_vm.FolhaCancelarAberta);
        await Resumo(chave);
        await _vm.Fechar.ExecutarAsync();
        Assert.False(_vm.FolhaCancelarAberta);
        Assert.True(_vm.Aberta);
    }

    [Fact]
    public async Task Sem_memoria_em_uso_a_folha_diz_o_som_do_aparelho()
    {
        Assert.Equal("Nada é salvo e o aparelho volta para o som do aparelho, como estava antes.", CriacaoMemoriaViewModel.MsgFolhaCancelar(null));
        Assert.Equal("1 ajuste no aparelho, relido.", CriacaoMemoriaViewModel.MsgResumoPrimeira(1));
        Assert.Equal("Mudou 2 ajustes, em destaque. O resto ficou igual.", CriacaoMemoriaViewModel.MsgResumoRefino(2));
        await Task.CompletedTask;
    }

    [Fact]
    public async Task A_folha_de_risco_pela_porta_fechada_no_Cancelar_fecha_a_criacao_na_tela()
    {
        var chave = await Autorizada();
        Assert.True((await _b.Chaves.DefinirAsync(Chave.Gerador, true, OrigemComando.Tela)).Ok);
        await Ajuste(chave);
        var folha = new FolhaRiscoViewModel(_confirmacoes, _b.Tela, _b.Relogio);
        try
        {
            var tom = _porta.AtenderAsync(PedidoPorta.De("POST", "/gerador/tom", Casa, """{"hz":1000,"db":-30.0}""", chave));
            await Apoio.Esperar(() => _confirmacoes.Atual is not null);
            await _b.Tela.EsvaziarAsync();
            Assert.True(folha.FolhaAberta);
            await folha.Cancelar.ExecutarAsync();
            await _b.Rodar(tom);

            Assert.Equal(409, (await tom).Status);
            Assert.Equal(CriacaoMemoria.MsgCanceladaNaFolha, Json(await tom).GetProperty("motivo").GetString());
            Assert.False(_vm.Aberta);
        }
        finally
        {
            folha.Dispose();
        }
    }

    // ----------------------------------------------------------------- critério de aceite

    [Fact]
    public async Task Criacao_de_ponta_a_ponta_pela_porta_e_pela_tela_ate_salvar_MUSICA_ORIG()
    {
        var chave = await Aberta();
        await Pergunta(chave, ultima: false);
        await _vm.Responder.ExecutarAsync(0);
        await Pergunta(chave, ultima: true, texto: "Que volume?", respostas: """["Baixo","Médio","Alto"]""");
        await _vm.Responder.ExecutarAsync(2);
        await _b.Tela.EsvaziarAsync();

        await Ajuste(chave, codigo: "081", indice: 2, valor: 2.0m);
        await Ajuste(chave, codigo: "081", indice: 0, valor: 8.0m);
        await Ajuste(chave, codigo: "080", indice: null, valor: -3.0m);
        await Resumo(chave);
        Assert.Equal("3 ajustes no aparelho, todos relidos.", _vm.TextoResumo);
        await ComoFicou(chave);
        await _vm.Avaliar.ExecutarAsync();
        await _vm.PodeSalvar.ExecutarAsync();
        await _b.Tela.EsvaziarAsync();
        var espelho = _b.Estado.Copiar();
        _vm.NomeMemoria = "MUSICA ORIG";
        await _vm.Salvar.ExecutarAsync();
        await _b.Tela.EsvaziarAsync();

        Assert.False(_vm.Aberta);
        var nova = _b.Setups!.MemoriaEmUso!;
        Assert.Equal("MUSICA ORIG", nova.Nome);
        Assert.Empty(Camadas.Diferentes(nova.Regulagem, espelho));
        Assert.Null(_b.Copia.Ultima!.Operacao);
        var tipos = Json(await Pedir("GET", "/eventos?desde=0", chave: chave)).GetProperty("eventos").EnumerateArray()
            .Select(e => e.GetProperty("tipo").GetString()!).Where(t => t.StartsWith("criacao-", StringComparison.Ordinal)).ToArray();
        Assert.Equal([CriacaoMemoria.EventoResposta, CriacaoMemoria.EventoResposta, CriacaoMemoria.EventoComoFicou, CriacaoMemoria.EventoSalva], tipos);
    }
}
