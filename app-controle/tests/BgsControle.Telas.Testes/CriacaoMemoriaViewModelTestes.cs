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
using BgsControle.Telas.Esquema;

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

    private void SemIa(CriacaoMemoriaViewModel? vm = null)
    {
        vm ??= _vm;
        foreach (var p in typeof(CriacaoMemoriaViewModel).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var valor = p.GetValue(vm);
            var textos = valor switch
            {
                string s => [s],
                IEnumerable<string> lista => lista.ToArray(),
                IEnumerable<LinhaCriacao> linhas => linhas.SelectMany(l => new[] { l.Rotulo, l.Antes, l.Valor, l.Frase, l.Linha, l.Motivo }).ToArray(),
                IEnumerable<OpcaoCriacao> opcoes => opcoes.Select(o => o.Texto).ToArray(),
                AjustePalco a => [a.Frase, a.Antes, a.Valor, a.Anuncio, a.Nota ?? ""],
                BgsControle.Telas.Esquema.EsquemaLigacao e => [e.Descricao, .. e.Equipamentos.Select(x => x.Nome)],
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

    // ----------------------------------------------------------------- história 10d: apoio das fases

    /// <summary>Roda na thread da tela (como a página chama os sinais) e devolve o resultado.</summary>
    private async Task<T> NaTela<T>(Func<T> f)
    {
        var feito = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        _b.Tela.Executar(() =>
        {
            try
            {
                feito.SetResult(f());
            }
            catch (Exception e)
            {
                feito.SetException(e);
            }
        });
        return await feito.Task;
    }

    /// <summary>Um ViewModel com as preferências (a animação) e a folha de risco, como no app.</summary>
    private async Task<CriacaoMemoriaViewModel> Outro(bool animacao = true, FolhaRiscoViewModel? folha = null, bool reduzido = false)
    {
        var arquivos = new ArquivosFalso();
        if (!animacao)
            arquivos.Semear(RepositorioTextoArquivos.ArquivoPreferencias, PreferenciasApp.ParaJson(false));
        var preferencias = new PreferenciasApp(new RepositorioTextoArquivos(arquivos, RepositorioTextoArquivos.ArquivoPreferencias));
        await preferencias.Carregada;
        return new CriacaoMemoriaViewModel(_criacao, _b.Tela, _b.Relogio, _b.Setups, folha, preferencias) { MovimentoReduzido = reduzido };
    }

    /// <summary>A página no palco, na passagem e no Aplicando: cada sinal até o resumo. Devolve as frases mostradas.</summary>
    private async Task<List<string>> AteOResumo(CriacaoMemoriaViewModel vm)
    {
        var frases = new List<string>();
        while (vm.Fase == FaseCriacao.Palco && vm.AjusteDoPalco is { } a)
        {
            frases.Add(a.Frase);
            Assert.True(await NaTela(vm.PalcoMostrou));
        }
        Assert.Equal(FaseCriacao.Passagem, vm.Fase);
        Assert.True(await NaTela(vm.PassagemTerminou));
        Assert.Equal(FaseCriacao.Aplicando, vm.Fase);
        while (await NaTela(vm.AvancarCanal))
        {
        }
        Assert.True(await NaTela(vm.AplicandoTerminou));
        return frases;
    }

    private EventoPorta[] EventosDoTipo(string tipo) => _porta.Eventos().Where(e => e.Tipo == tipo).ToArray();

    // ----------------------------------------------------------------- história 10d: matriz

    [Fact]
    public async Task Abrir_a_camada_entra_nas_perguntas_com_Nova_memoria_e_Casa_21()
    {
        Assert.Equal(FaseCriacao.Fechada, _vm.Fase);

        await Aberta();

        Assert.True(_vm.Aberta);
        Assert.Equal(FaseCriacao.Perguntas, _vm.Fase);
        Assert.Equal("Nova memória", _vm.TituloBarra);
        Assert.Equal("Casa 2.1", _vm.SubtituloBarra);
        Assert.False(_vm.PodeVoltar); // o ‹ só da 2ª pergunta em diante
        Assert.True(_vm.PodeFechar);
        SemIa();
    }

    [Fact]
    public async Task Voltar_com_outra_resposta_tira_a_pergunta_2_e_a_tela_espera_a_proxima()
    {
        var chave = await Aberta();
        await Pergunta(chave, ultima: false);
        await _vm.Opcoes[0].Tocar.ExecutarAsync();
        await Pergunta(chave, ultima: false, texto: "Que volume?", respostas: """["Baixo","Médio","Alto"]""");
        Assert.True(_vm.PodeVoltar);
        await _vm.Opcoes[1].Tocar.ExecutarAsync();
        await _b.Tela.EsvaziarAsync();
        Assert.False(_vm.TemPergunta); // esperando a 3ª
        Assert.True(_vm.PodeVoltar);

        // ‹ duas vezes: a 2ª e a 1ª, cada uma com a resposta dada marcada.
        await _vm.Anterior.ExecutarAsync();
        Assert.Equal(2, _vm.NumeroPergunta);
        Assert.True(_vm.Voltando);
        Assert.Equal([false, true, false], _vm.Opcoes.Select(o => o.Marcada));
        await _vm.Anterior.ExecutarAsync();
        Assert.Equal(1, _vm.NumeroPergunta);
        Assert.False(_vm.PodeVoltar);
        Assert.Equal([true, false, false], _vm.Opcoes.Select(o => o.Marcada));

        // A mesma resposta só avança na tela, sem evento.
        await _vm.Opcoes[0].Tocar.ExecutarAsync();
        Assert.Equal(2, _vm.NumeroPergunta);
        Assert.False(_vm.Voltando);
        Assert.Equal(2, EventosDoTipo(CriacaoMemoria.EventoResposta).Length);

        // Outra resposta na 1ª: a 2ª sai, a porta recebe a nº 1 de novo e a tela espera a próxima.
        await _vm.Anterior.ExecutarAsync();
        await _vm.Opcoes[2].Tocar.ExecutarAsync();
        await _b.Tela.EsvaziarAsync();
        Assert.False(_vm.TemPergunta);
        Assert.Equal(CriacaoMemoriaViewModel.TextoEsperandoPergunta, _vm.TextoEspera);
        var e = EventosDoTipo(CriacaoMemoria.EventoResposta)[^1].Criacao!;
        Assert.Equal((1, "Jogo"), (e.Numero, e.Texto));
        var perguntas = Json(await Pedir("GET", "/criacao", chave: chave)).GetProperty("perguntas").EnumerateArray().ToArray();
        Assert.Equal("Jogo", Assert.Single(perguntas).GetProperty("resposta").GetString());

        // O "Outro…" escrito aparece no lugar dele ao voltar.
        await Pergunta(chave, ultima: false, texto: "Onde você senta?");
        await _vm.AbrirOutro.ExecutarAsync();
        Assert.True(_vm.CampoOutroAberto);
        _vm.TextoDoOutro = "No pufe";
        await _vm.ResponderOutro.ExecutarAsync();
        await _b.Tela.EsvaziarAsync();
        await _vm.Anterior.ExecutarAsync();
        Assert.Equal(2, _vm.NumeroPergunta);
        Assert.Equal("No pufe", _vm.TextoBotaoOutro);
        Assert.True(_vm.OutroMarcado);
        Assert.All(_vm.Opcoes, o => Assert.False(o.Marcada));
        await _vm.AbrirOutro.ExecutarAsync();
        Assert.Equal("No pufe", _vm.TextoDoOutro);
    }

    [Fact]
    public async Task Palco_a_frente_3_ajustes_e_o_resumo_a_fase_espera_os_sinais_da_pagina()
    {
        var chave = await Autorizada();
        Assert.Equal(FaseCriacao.Palco, _vm.Fase);
        Assert.Null(_vm.AjusteDoPalco); // o anel gira: a fila está vazia e a etapa ainda é Ajustando
        Assert.False(await NaTela(_vm.PalcoMostrou));

        await Ajuste(chave, frase: "Definindo o corte das JBL");
        Assert.Equal("Definindo o corte das JBL", _vm.AjusteDoPalco!.Frase);
        Assert.True(await NaTela(_vm.PalcoMostrou));
        Assert.Equal(FaseCriacao.Palco, _vm.Fase); // nenhum na fila e a etapa ainda é Ajustando: espera
        Assert.Null(_vm.AjusteDoPalco);

        await Ajuste(chave, codigo: "081", indice: 0, valor: 8.0m, frase: "Ajustando o grave");
        await Ajuste(chave, codigo: "080", indice: null, valor: -3.0m, frase: "Protegendo os canais");
        await Ajuste(chave, codigo: "081", indice: 1, valor: 7.0m, frase: "Encaixando o Sub");
        await Resumo(chave);
        Assert.Equal(EtapaCriacao.Resumo, _vm.Etapa);

        // O núcleo está no resumo, mas a tela mostra cada ajuste antes.
        var a = _vm.AjusteDoPalco!;
        Assert.Equal(("Ajustando o", "grave"), (a.Inicio, a.Chave));
        Assert.Equal("+8,0 dB", a.Valor);
        Assert.Equal($"Ajustando o grave: de {a.Antes} para +8,0 dB", _vm.AnuncioPalco);
        Assert.Null(a.Nota);
        Assert.InRange(a.PosicaoValor, 0, 1);
        Assert.False(await NaTela(_vm.PassagemTerminou)); // fora de hora: nada muda
        Assert.False(await NaTela(_vm.AplicandoTerminou));

        Assert.True(await NaTela(_vm.PalcoMostrou));
        Assert.Equal("Protegendo os canais", _vm.AjusteDoPalco!.Frase);
        Assert.True(await NaTela(_vm.PalcoMostrou));
        Assert.Equal(FaseCriacao.Palco, _vm.Fase);
        Assert.Equal("Encaixando o Sub", _vm.AjusteDoPalco!.Frase);
        Assert.True(await NaTela(_vm.PalcoMostrou));
        Assert.Equal(FaseCriacao.Passagem, _vm.Fase);
        Assert.False(_vm.PodeFechar);

        Assert.True(await NaTela(_vm.PassagemTerminou));
        Assert.Equal(FaseCriacao.Aplicando, _vm.Fase);
        Assert.All(_vm.EstadosCanais, c => Assert.Equal(EstadoCanal.AindaNao, c));
        Assert.True(await NaTela(_vm.AvancarCanal));
        Assert.Equal([EstadoCanal.Aceso, EstadoCanal.AindaNao, EstadoCanal.AindaNao, EstadoCanal.AindaNao], _vm.EstadosCanais);
        Assert.True(await NaTela(_vm.AvancarCanal));
        Assert.Equal([EstadoCanal.Gravado, EstadoCanal.Aceso, EstadoCanal.AindaNao, EstadoCanal.AindaNao], _vm.EstadosCanais);
        Assert.True(await NaTela(_vm.AvancarCanal));
        Assert.True(await NaTela(_vm.AvancarCanal));
        Assert.Equal([EstadoCanal.Gravado, EstadoCanal.Gravado, EstadoCanal.Gravado, EstadoCanal.Aceso], _vm.EstadosCanais);
        Assert.False(await NaTela(_vm.AvancarCanal));
        Assert.All(_vm.EstadosCanais, c => Assert.Equal(EstadoCanal.Gravado, c));
        Assert.Equal(FaseCriacao.Aplicando, _vm.Fase);

        Assert.True(await NaTela(_vm.AplicandoTerminou));
        Assert.Equal(FaseCriacao.Resumo, _vm.Fase);
        Assert.Equal("4 ajustes no aparelho, todos relidos.", _vm.TextoResumo);
        Assert.Contains(_vm.LinhasResumo, l => l.Motivo == "A resposta pediu.");
        SemIa();
    }

    [Fact]
    public async Task Sem_animacao_o_Aplicando_vem_desde_o_comeco_cada_canal_acende_com_o_ajuste_e_o_resumo_entra_na_hora()
    {
        using var vm = await Outro(animacao: false);
        var chave = await Aberta();
        await Pergunta(chave, ultima: true);
        Assert.True(_criacao.Responder(0).Ok);
        await _b.Tela.EsvaziarAsync();

        Assert.False(vm.AnimacaoLigada);
        Assert.False(vm.ComAnimacao);
        Assert.Equal(FaseCriacao.Aplicando, vm.Fase);
        Assert.All(vm.EstadosCanais, c => Assert.Equal(EstadoCanal.AindaNao, c));

        await Ajuste(chave, codigo: "081", indice: 2, valor: 2.0m); // o Sub: CH3 e CH4, com o link
        Assert.Null(vm.AjusteDoPalco);
        Assert.Equal([EstadoCanal.AindaNao, EstadoCanal.AindaNao, EstadoCanal.Aceso, EstadoCanal.Aceso], vm.EstadosCanais);
        await Ajuste(chave, codigo: "081", indice: 0, valor: 8.0m);
        Assert.Equal([EstadoCanal.Aceso, EstadoCanal.AindaNao, EstadoCanal.Gravado, EstadoCanal.Gravado], vm.EstadosCanais);
        await Ajuste(chave, codigo: "080", indice: null, valor: -3.0m); // global: todos
        Assert.All(vm.EstadosCanais, c => Assert.Equal(EstadoCanal.Aceso, c));

        await Resumo(chave);
        Assert.Equal(FaseCriacao.Resumo, vm.Fase); // sem mínimo de 0,7 s por canal
        Assert.Equal("3 ajustes no aparelho, todos relidos.", vm.TextoResumo);
    }

    [Fact]
    public async Task Com_o_movimento_reduzido_tambem_nao_ha_palco_nem_passagem()
    {
        using var vm = await Outro(reduzido: true);
        var chave = await Aberta();
        await Pergunta(chave, ultima: true);
        Assert.True(_criacao.Responder(0).Ok);
        await _b.Tela.EsvaziarAsync();

        Assert.True(vm.AnimacaoLigada);
        Assert.False(vm.ComAnimacao);
        Assert.Equal(FaseCriacao.Aplicando, vm.Fase);
        await Ajuste(chave);
        await Resumo(chave);
        Assert.Equal(FaseCriacao.Resumo, vm.Fase);
    }

    [Fact]
    public async Task Refino_o_palco_mostra_so_a_rodada_2_e_o_resumo_destaca_a_linha_da_rodada()
    {
        var chave = await Autorizada();
        await Ajuste(chave, frase: "Ajustando o grave");
        await Ajuste(chave, codigo: "081", indice: 0, valor: 8.0m, frase: "Definindo o corte das JBL");
        await Resumo(chave);
        Assert.Equal(["Ajustando o grave", "Definindo o corte das JBL"], await AteOResumo(_vm));
        await ComoFicou(chave);
        await _vm.Avaliar.ExecutarAsync();
        await _b.Tela.EsvaziarAsync();

        Assert.Equal(FaseCriacao.ComoFicou, _vm.Fase);
        Assert.Equal("tocando agora", _vm.SubtituloBarra);
        Assert.Equal(["Ótimo, pode salvar", "Quero mais impacto", "Os graves embolaram"], _vm.Opcoes.Select(o => o.Texto));
        Assert.True(_vm.PodeFechar);

        await _vm.Opcoes[1].Tocar.ExecutarAsync(); // "Quero mais impacto"
        await _b.Tela.EsvaziarAsync();
        Assert.Equal(FaseCriacao.Palco, _vm.Fase);
        Assert.Null(_vm.AjusteDoPalco);
        await Ajuste(chave, valor: 3.5m, frase: "Reforçando o grave");
        await Resumo(chave);

        Assert.Equal(["Reforçando o grave"], await AteOResumo(_vm));
        Assert.Equal(FaseCriacao.Resumo, _vm.Fase);
        Assert.Equal("Mudou 1 ajuste, em destaque. O resto ficou igual.", _vm.TextoResumo);
        Assert.Equal([true, false], _vm.LinhasResumo.Select(l => l.Destaque));
        Assert.Equal("Reforçando o grave", _vm.LinhasResumo[0].Frase);
    }

    [Fact]
    public async Task Releitura_diferente_o_palco_mostra_o_valor_relido_e_O_aparelho_ficou_com_outro_valor()
    {
        var chave = await Autorizada();
        _b.Simulador.EcosDiferentes["*081*2*2.0*"] = "#081#2#1.5#";

        await Ajuste(chave);

        var a = _vm.AjusteDoPalco!;
        Assert.Equal("+1,5 dB", a.Valor);
        Assert.True(a.OutroValor);
        Assert.Equal("O aparelho ficou com outro valor.", a.Nota);
        var linha = Assert.Single(_vm.AjustesDaRodada);
        Assert.True(linha.Diferente);
        Assert.Equal("+2,0 dB", linha.Pedido);
        Assert.Equal("+1,5 dB", linha.Valor);
        Assert.Equal(409, (await Pedir("POST", "/criacao/resumo", null, chave)).Status); // a regra da 9d
        _b.Simulador.EcosDiferentes.Clear();
    }

    [Fact]
    public async Task Folha_de_risco_no_palco_pausa_e_Confirmar_retoma()
    {
        using var folha = new FolhaRiscoViewModel(_confirmacoes, _b.Tela, _b.Relogio);
        using var vm = await Outro(folha: folha);
        var chave = await Aberta();
        await Pergunta(chave, ultima: true);
        Assert.True(_criacao.Responder(0).Ok);
        await Ajuste(chave);
        Assert.NotNull(vm.AjusteDoPalco);

        var pedido = _confirmacoes.PedirAsync(new PedidoConfirmacao("Ligar o gerador?", "Um tom forte pode queimar o tweeter ou o sub"));
        await Apoio.Esperar(() => _confirmacoes.Atual is not null);
        await _b.Tela.EsvaziarAsync();

        Assert.True(vm.Pausado);
        Assert.False(await NaTela(vm.PalcoMostrou)); // o palco não anda
        Assert.Equal(FaseCriacao.Palco, vm.Fase);
        Assert.NotNull(vm.AjusteDoPalco);

        await folha.Confirmar.ExecutarAsync();
        Assert.Equal(DecisaoConfirmacao.Confirmou, await pedido);
        await _b.Tela.EsvaziarAsync();

        Assert.False(vm.Pausado);
        Assert.True(await NaTela(vm.PalcoMostrou));
        Assert.True(vm.Aberta);
    }

    [Fact]
    public async Task Queda_do_BLE_no_palco_fecha_a_camada_e_fica_aplicado_pela_metade()
    {
        var chave = await Autorizada();
        await Ajuste(chave);
        Assert.Equal(FaseCriacao.Palco, _vm.Fase);

        _b.Simulador.Derrubar();
        await Apoio.Esperar(() => !_criacao.Aberta);
        await _b.Tela.EsvaziarAsync();

        Assert.False(_vm.Aberta);
        Assert.Equal(FaseCriacao.Fechada, _vm.Fase);
        Assert.Null(_vm.AjusteDoPalco);
        Assert.Null(_vm.AvisoFinal); // a folha "Aplicado pela metade" é quem fala
        Assert.Equal(CriacaoMemoria.EtapaAjustes, _b.Copia.Ultima?.Operacao?.Etapa);
    }

    [Fact]
    public async Task Salvar_fecha_a_camada_e_a_casca_mostra_Memoria_criada_por_2_6_s()
    {
        using var t = new Telas11(_b);
        using var casca = new CascaViewModel(_b.Sessao, t.Conexao, t.Setup, t.Memorias, t.Inicio, t.Mesa, t.Audio, t.Salvar, _b.Fabrica, t.Nav,
            _b.Tela, _b.Relogio, t.PlayerTela, t.FolhaRisco, t.Gerador, t.Reaplicando, _vm);
        var chave = await Autorizada();
        Assert.True(casca.CriacaoPorCima);
        Assert.Equal((true, false, false, false), t.Nav.Camadas);
        await Ajuste(chave);
        await Resumo(chave);
        await AteOResumo(_vm);
        await ComoFicou(chave);
        await _vm.Avaliar.ExecutarAsync();
        await _vm.Opcoes[0].Tocar.ExecutarAsync(); // "Ótimo, pode salvar"
        await _b.Tela.EsvaziarAsync();
        Assert.Equal(FaseCriacao.Salvar, _vm.Fase);
        Assert.Equal("Salvar no Casa 2.1?", _vm.TituloSalvar);
        Assert.Equal("Guarda toda a regulagem que está tocando agora.", _vm.ExplicaSalvar);
        Assert.Equal("Nome da memória", _vm.RotuloNome);
        Assert.Equal("Salvar", _vm.TextoSalvar);
        Assert.False(_vm.PodeFechar);

        _vm.NomeMemoria = "";
        await _vm.Salvar.ExecutarAsync();
        await _b.Tela.EsvaziarAsync();
        Assert.NotNull(_vm.ErroNome);
        Assert.Equal(FaseCriacao.Salvar, _vm.Fase);
        Assert.Null(casca.AvisoRapido);

        _vm.NomeMemoria = "MUSICA ORIG";
        await _vm.Salvar.ExecutarAsync();
        await _b.Tela.EsvaziarAsync();

        Assert.False(_vm.Aberta);
        Assert.False(casca.CriacaoPorCima);
        Assert.Equal((false, false, false, false), t.Nav.Camadas);
        Assert.Equal("Memória MUSICA ORIG criada no Casa 2.1.", casca.AvisoRapido);
        await _b.Andar(TimeSpan.FromSeconds(2.5), passoMs: 100);
        Assert.NotNull(casca.AvisoRapido);
        await _b.Andar(TimeSpan.FromSeconds(0.2), passoMs: 100);
        Assert.Null(casca.AvisoRapido);
    }

    [Fact]
    public async Task Cancelar_e_voltar_avisa_Nova_memoria_cancelada()
    {
        var chave = await Aberta();
        var avisos = new List<string?>();
        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(CriacaoMemoriaViewModel.AvisoFinal))
                avisos.Add(_vm.AvisoFinal);
        };
        await Pergunta(chave, ultima: false);

        await _vm.Fechar.ExecutarAsync();
        await _b.Rodar(_vm.CancelarEVoltar.ExecutarAsync());

        Assert.False(_vm.Aberta);
        Assert.Equal([CriacaoMemoria.MsgCancelada], avisos);
    }

    [Fact]
    public async Task Tecla_Voltar_do_Android_em_cada_fase()
    {
        Assert.False(_vm.Voltar()); // fechada: o Android segue
        var chave = await Aberta();
        await Pergunta(chave, ultima: false);

        // Na 1ª pergunta, o ✕; com a folha aberta, "Continuar criando".
        Assert.True(_vm.Voltar());
        Assert.True(_vm.FolhaCancelarAberta);
        Assert.True(_vm.Voltar());
        Assert.False(_vm.FolhaCancelarAberta);
        Assert.True(_vm.Aberta);

        // Da 2ª em diante, o ‹.
        await _vm.Opcoes[0].Tocar.ExecutarAsync();
        await Pergunta(chave, ultima: true, texto: "Que volume?");
        Assert.True(_vm.Voltar());
        Assert.Equal(1, _vm.NumeroPergunta);
        Assert.False(_vm.FolhaCancelarAberta);
        await _vm.Opcoes[0].Tocar.ExecutarAsync(); // a mesma: volta à 2ª
        Assert.Equal(2, _vm.NumeroPergunta);
        await _vm.Opcoes[0].Tocar.ExecutarAsync();
        await _b.Tela.EsvaziarAsync();

        // Palco, passagem, Aplicando e resumo: nada acontece.
        await Ajuste(chave);
        Assert.True(_vm.Voltar());
        Assert.Equal(FaseCriacao.Palco, _vm.Fase);
        Assert.False(_vm.FolhaCancelarAberta);
        await Resumo(chave);
        Assert.True(await NaTela(_vm.PalcoMostrou));
        Assert.Equal(FaseCriacao.Passagem, _vm.Fase);
        Assert.True(_vm.Voltar());
        Assert.Equal(FaseCriacao.Passagem, _vm.Fase);
        Assert.True(await NaTela(_vm.PassagemTerminou));
        Assert.True(_vm.Voltar());
        Assert.Equal(FaseCriacao.Aplicando, _vm.Fase);
        while (await NaTela(_vm.AvancarCanal))
        {
        }
        Assert.True(await NaTela(_vm.AplicandoTerminou));
        Assert.True(_vm.Voltar());
        Assert.Equal(FaseCriacao.Resumo, _vm.Fase);
        Assert.False(_vm.FolhaCancelarAberta);

        // "Como ficou?": o ✕. Salvar: "Ajustar mais".
        await ComoFicou(chave);
        await _vm.Avaliar.ExecutarAsync();
        Assert.True(_vm.Voltar());
        Assert.True(_vm.FolhaCancelarAberta);
        Assert.True(_vm.Voltar());
        Assert.False(_vm.FolhaCancelarAberta);
        await _vm.PodeSalvar.ExecutarAsync();
        await _b.Tela.EsvaziarAsync();
        Assert.Equal(FaseCriacao.Salvar, _vm.Fase);
        Assert.True(_vm.Voltar());
        await _b.Tela.EsvaziarAsync();
        Assert.Equal(FaseCriacao.ComoFicou, _vm.Fase);
    }

    // ----------------------------------------------------------------- história 10d: "+ Nova memória"

    private MemoriasViewModel Memorias() =>
        new(_b.Setups!, _b.Copia, _b.Sessao, _b.Chaves, _b.Estado, _b.CriarSalvarViewModel(), _b.Tela, _b.Relogio, _criacao);

    [Fact]
    public async Task Nova_memoria_com_Setup_em_uso_vai_ao_eventos_e_trava_com_a_criacao_aberta()
    {
        using var memorias = Memorias();
        var chave = await Pronto();
        await _b.Tela.EsvaziarAsync();
        Assert.Equal("+ Nova memória", MemoriasViewModel.TextoNovaMemoria);
        Assert.True(memorias.PodeNovaMemoria);
        Assert.Null(memorias.MotivoTravaNovaMemoria);

        await memorias.NovaMemoria.ExecutarAsync();

        Assert.Null(memorias.Mensagem);
        var eventos = Json(await Pedir("GET", "/eventos?desde=0", chave: chave)).GetProperty("eventos").EnumerateArray()
            .Where(e => e.GetProperty("tipo").GetString() == CriacaoMemoria.EventoPedida).ToArray();
        Assert.Single(eventos);
        Assert.False(_vm.Aberta); // a tela não abre a criação

        Assert.Equal(200, (await Pedir("POST", "/criacao/abrir", null, chave)).Status);
        await _b.Tela.EsvaziarAsync();
        Assert.False(memorias.PodeNovaMemoria);
        Assert.Equal(CriacaoMemoria.MsgJaAberta, memorias.MotivoTravaNovaMemoria);
        await memorias.NovaMemoria.ExecutarAsync();
        Assert.Equal(CriacaoMemoria.MsgJaAberta, memorias.Mensagem);
        Assert.Single(EventosDoTipo(CriacaoMemoria.EventoPedida));
    }

    [Fact]
    public async Task Nova_memoria_travada_sem_Setup_em_uso()
    {
        using var memorias = Memorias();
        _b.Simulador.Definir("050", 2, "0.0");
        await _b.Conectar();
        await _b.Rodar(_b.Setups!.SeguirSemSetupAsync());
        await _b.Tela.EsvaziarAsync();

        Assert.False(memorias.PodeNovaMemoria);
        Assert.Equal(GerenteSetups.MsgSemSetup, memorias.MotivoTravaNovaMemoria);
        await memorias.NovaMemoria.ExecutarAsync();
        Assert.Equal(GerenteSetups.MsgSemSetup, memorias.Mensagem);
        Assert.Empty(EventosDoTipo(CriacaoMemoria.EventoPedida));
    }

    [Fact]
    public async Task Nova_memoria_travada_na_duvida()
    {
        using var memorias = Memorias();
        _b.Simulador.Definir("050", 2, "0.0"); // o atraso do Sub difere da FILME: dúvida
        await _b.Conectar();
        await _b.Tela.EsvaziarAsync();

        Assert.False(memorias.PodeNovaMemoria);
        Assert.Equal(GerenteSetups.MsgDuvida, memorias.MotivoTravaNovaMemoria);
        await memorias.NovaMemoria.ExecutarAsync();
        Assert.Empty(EventosDoTipo(CriacaoMemoria.EventoPedida));
    }

    [Fact]
    public async Task Nova_memoria_travada_no_so_leitura()
    {
        using var memorias = Memorias();
        _b.Simulador.Firmware = "R004";
        await _b.Conectar();
        await _b.Tela.EsvaziarAsync();

        Assert.False(memorias.PodeNovaMemoria);
        Assert.Equal(ControleParametroViewModel.MsgSoLeitura, memorias.MotivoTravaNovaMemoria);
        await memorias.NovaMemoria.ExecutarAsync();
        Assert.Equal(ControleParametroViewModel.MsgSoLeitura, memorias.Mensagem);
        Assert.Empty(EventosDoTipo(CriacaoMemoria.EventoPedida));
    }

    // ----------------------------------------------------------------- história 10d: critério de aceite

    [Fact]
    public async Task Criacao_de_ponta_a_ponta_as_fases_seguem_a_ordem_e_o_Salvar_fecha_com_o_aviso()
    {
        using var t = new Telas11(_b);
        using var casca = new CascaViewModel(_b.Sessao, t.Conexao, t.Setup, t.Memorias, t.Inicio, t.Mesa, t.Audio, t.Salvar, _b.Fabrica, t.Nav,
            _b.Tela, _b.Relogio, t.PlayerTela, t.FolhaRisco, t.Gerador, t.Reaplicando, _vm);
        var fases = new List<FaseCriacao>();
        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(CriacaoMemoriaViewModel.Fase) && (fases.Count == 0 || fases[^1] != _vm.Fase))
                fases.Add(_vm.Fase);
        };

        var chave = await Aberta();
        await Pergunta(chave, ultima: false);
        await _vm.Opcoes[0].Tocar.ExecutarAsync();
        await Pergunta(chave, ultima: true, texto: "Que volume?", respostas: """["Baixo","Médio","Alto"]""");
        await _vm.Opcoes[2].Tocar.ExecutarAsync();
        await _b.Tela.EsvaziarAsync();
        await Ajuste(chave, codigo: "081", indice: 2, valor: 2.0m, frase: "Encaixando o Sub");
        await Ajuste(chave, codigo: "081", indice: 0, valor: 8.0m, frase: "Ajustando o grave");
        await Ajuste(chave, codigo: "080", indice: null, valor: -3.0m, frase: "Protegendo os canais");
        await Resumo(chave);

        var palco = await AteOResumo(_vm);
        Assert.Equal(["Encaixando o Sub", "Ajustando o grave", "Protegendo os canais"], palco); // um por ajuste
        await ComoFicou(chave);
        await _vm.Avaliar.ExecutarAsync();
        await _vm.PodeSalvar.ExecutarAsync();
        await _b.Tela.EsvaziarAsync();
        _vm.NomeMemoria = "MUSICA ORIG";
        await _vm.Salvar.ExecutarAsync();
        await _b.Tela.EsvaziarAsync();

        Assert.Equal([FaseCriacao.Perguntas, FaseCriacao.Palco, FaseCriacao.Passagem, FaseCriacao.Aplicando, FaseCriacao.Resumo,
            FaseCriacao.ComoFicou, FaseCriacao.Salvar, FaseCriacao.Fechada], fases);
        Assert.False(casca.CriacaoPorCima);
        Assert.Equal(GerenteSetups.MsgCriada("MUSICA ORIG", "Casa 2.1"), casca.AvisoRapido);
        Assert.Equal("MUSICA ORIG", _b.Setups!.MemoriaEmUso!.Nome);
    }

    // ----------------------------------------------------------------- história 10d: revisão

    [Theory]
    [InlineData("081", "-45.0", 0.0)] // ganho no mínimo
    [InlineData("081", "-15.0", 0.5)] // no meio da faixa (−45 a +15)
    [InlineData("081", "+15.0", 1.0)] // no máximo
    [InlineData("040", "80", 0.270190)] // corte em 80 Hz, na escala logarítmica de 10 a 22 000 Hz
    [InlineData("042", "8", 0.533333)] // a opção 8 de 0 a 15
    [InlineData("042", "0", 0.0)] // o filtro OFF (a primeira opção)
    [InlineData("071", "0.5", 0.0)] // o limitador OFF (valor especial)
    [InlineData("071", "-24.0", 0.5)]
    public void Posicao_na_barra_pela_faixa_do_parametro(string codigo, string noFio, double esperado)
    {
        Assert.Equal(esperado, CriacaoMemoriaViewModel.PosicaoNaBarra(codigo, noFio), 5);
    }

    [Fact]
    public async Task Ajuste_confirmado_so_pela_releitura_mostra_O_aparelho_ficou_com_outro_valor()
    {
        var chave = await Autorizada();
        _b.Simulador.Silenciar.Add("*081*2*2.0*"); // sem eco: o valor vem da releitura

        await Ajuste(chave, frase: "Ajustando o grave.");

        var a = _vm.AjusteDoPalco!;
        Assert.True(a.OutroValor);
        Assert.Equal("O aparelho ficou com outro valor.", a.Nota);
        Assert.Equal("+2,0 dB", a.Valor);
        Assert.Equal(CriacaoMemoriaViewModel.PosicaoNaBarra("081", "+6.0"), a.PosicaoAntes, 5); // a saída 3 nasce em +6,0 dB
        Assert.Equal(51.0 / 60, a.PosicaoAntes, 5);
        Assert.Equal(47.0 / 60, a.PosicaoValor, 5);
        // A pontuação final não vai para a palavra-chave nem para o anúncio.
        Assert.Equal(("Ajustando o", "grave"), (a.Inicio, a.Chave));
        Assert.StartsWith("Ajustando o grave: de ", a.Anuncio);
        _b.Simulador.Silenciar.Clear();
    }

    [Fact]
    public async Task Cancelar_com_a_volta_falhando_avisa_que_o_som_nao_voltou_todo()
    {
        using var t = new Telas11(_b);
        using var casca = new CascaViewModel(_b.Sessao, t.Conexao, t.Setup, t.Memorias, t.Inicio, t.Mesa, t.Audio, t.Salvar, _b.Fabrica, t.Nav,
            _b.Tela, _b.Relogio, t.PlayerTela, t.FolhaRisco, t.Gerador, t.Reaplicando, _vm);
        var chave = await Autorizada();
        await Ajuste(chave); // 081 da saída 3: +6,0 → +2,0
        await Resumo(chave);
        await AteOResumo(_vm);
        await ComoFicou(chave);
        await _vm.Avaliar.ExecutarAsync();
        await _b.Tela.EsvaziarAsync();
        _b.Simulador.EcosDiferentes["*081*2*6.0*"] = "#081#2#5.0#"; // a volta não confere

        await _vm.Fechar.ExecutarAsync();
        await _b.Rodar(_vm.CancelarEVoltar.ExecutarAsync());

        var e = EventosDoTipo(CriacaoMemoria.EventoCancelada).Single().Criacao!;
        Assert.False(e.Voltou);
        Assert.Equal(CriacaoMemoria.MsgNaoVoltou(e.Motivo!), _vm.AvisoFinal);
        Assert.Equal(_vm.AvisoFinal, casca.AvisoRapido);
        Assert.False(_vm.Aberta);
        _b.Simulador.EcosDiferentes.Clear();
    }

    [Fact]
    public async Task Tecla_Voltar_com_o_Outro_aberto_fecha_o_campo_e_nao_abre_a_folha()
    {
        var chave = await Aberta();
        await Pergunta(chave, ultima: false);

        await _vm.AbrirOutro.ExecutarAsync();
        Assert.True(_vm.Voltar());
        Assert.False(_vm.CampoOutroAberto);
        Assert.False(_vm.FolhaCancelarAberta); // na 1ª pergunta também não
        Assert.Equal(1, _vm.NumeroPergunta);

        await _vm.Opcoes[0].Tocar.ExecutarAsync();
        await Pergunta(chave, ultima: false, texto: "Que volume?");
        await _vm.AbrirOutro.ExecutarAsync();
        Assert.True(_vm.Voltar());
        Assert.False(_vm.CampoOutroAberto);
        Assert.Equal(2, _vm.NumeroPergunta); // continua na 2ª
        Assert.False(_vm.FolhaCancelarAberta);
    }

    [Fact]
    public async Task Toque_atrasado_numa_resposta_de_outra_pergunta_nao_responde_e_a_marca_apaga()
    {
        var chave = await Aberta();
        await Pergunta(chave, ultima: false);
        await _vm.Opcoes[0].Tocar.ExecutarAsync();
        await Pergunta(chave, ultima: false, texto: "Que volume?");
        var daSegunda = _vm.Opcoes[2];
        daSegunda.Acender();

        await _vm.Anterior.ExecutarAsync(); // o ‹ dentro dos 0,18 s
        Assert.Equal(1, _vm.NumeroPergunta);
        await daSegunda.Tocar.ExecutarAsync();
        await _b.Tela.EsvaziarAsync();

        Assert.False(daSegunda.Marcada);
        Assert.Equal(1, _vm.NumeroPergunta);
        Assert.Single(EventosDoTipo(CriacaoMemoria.EventoResposta)); // só a 1ª resposta foi à porta
        Assert.Equal("Música", _criacao.Perguntas[0].Resposta);
        Assert.Equal(2, _criacao.Perguntas.Count);
    }

    [Fact]
    public async Task Salvando_o_botao_mostra_Salvando_e_Voltar_e_Ajustar_mais_nao_mexem()
    {
        var chave = await Autorizada();
        await Ajuste(chave);
        await Resumo(chave);
        await AteOResumo(_vm);
        await ComoFicou(chave);
        await _vm.Avaliar.ExecutarAsync();
        await _vm.PodeSalvar.ExecutarAsync();
        await _b.Tela.EsvaziarAsync();
        var textos = new List<string>();
        var voltou = new List<bool>();
        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != nameof(CriacaoMemoriaViewModel.TextoSalvar))
                return;
            textos.Add(_vm.TextoSalvar);
            if (_vm.Salvando)
            {
                voltou.Add(_vm.Voltar());
                _vm.AjustarMais.ExecutarAsync().GetAwaiter().GetResult();
                Assert.Equal(EtapaCriacao.Salvar, _criacao.Etapa); // o núcleo não volta ao "Como ficou?"
            }
        };

        _vm.NomeMemoria = "MUSICA ORIG";
        await _vm.Salvar.ExecutarAsync();
        await _b.Tela.EsvaziarAsync();

        Assert.Equal(["Salvando", "Salvar"], textos);
        Assert.Equal([true], voltou);
        Assert.False(_vm.Aberta);
        Assert.Equal("MUSICA ORIG", _b.Setups!.MemoriaEmUso!.Nome);
    }

    [Fact]
    public async Task Cancelada_pela_folha_de_risco_avisa_Nova_memoria_cancelada()
    {
        var chave = await Autorizada();
        Assert.True((await _b.Chaves.DefinirAsync(Chave.Gerador, true, OrigemComando.Tela)).Ok);
        await Ajuste(chave);
        using var folha = new FolhaRiscoViewModel(_confirmacoes, _b.Tela, _b.Relogio);
        var tom = _porta.AtenderAsync(PedidoPorta.De("POST", "/gerador/tom", Casa, """{"hz":1000,"db":-30.0}""", chave));
        await Apoio.Esperar(() => _confirmacoes.Atual is not null);
        await _b.Tela.EsvaziarAsync();

        await folha.Cancelar.ExecutarAsync();
        await _b.Rodar(tom);

        Assert.False(_vm.Aberta);
        Assert.Equal(CriacaoMemoria.PorFolha, EventosDoTipo(CriacaoMemoria.EventoCancelada).Single().Criacao!.Por);
        Assert.Equal(CriacaoMemoria.MsgCancelada, _vm.AvisoFinal);
    }

    [Fact]
    public async Task Cancelada_pela_porta_avisa_Nova_memoria_cancelada()
    {
        var chave = await Aberta();
        await Pergunta(chave, ultima: false);

        Assert.Equal(200, (await Pedir("POST", "/criacao/cancelar", null, chave)).Status);
        await _b.Tela.EsvaziarAsync();

        Assert.False(_vm.Aberta);
        Assert.Equal(CriacaoMemoria.PorPorta, EventosDoTipo(CriacaoMemoria.EventoCancelada).Single().Criacao!.Por);
        Assert.Equal(CriacaoMemoria.MsgCancelada, _vm.AvisoFinal);
    }

    // ----------------------------------------------------------------- história 14b: a escuta "1 ou 2"

    /// <summary>Até o refino pelo ViewModel: um ajuste, o resumo, "Avaliar o som" e "Quero mais impacto" (rodada 2).</summary>
    private async Task<string> NoRefino()
    {
        var chave = await Autorizada();
        await Ajuste(chave);
        await Resumo(chave);
        await ComoFicou(chave);
        await _vm.Avaliar.ExecutarAsync();
        await _b.Tela.EsvaziarAsync();
        await _vm.Responder.ExecutarAsync(0);
        await _b.Tela.EsvaziarAsync();
        Assert.True(_vm.Ajustando);
        Assert.Equal(2, _criacao.Rodada);
        return chave;
    }

    private static string GanhoJbl(decimal valor, string frase) =>
        $$"""{"codigo":"081","indice":0,"valor":{{valor.ToString(CultureInfo.InvariantCulture)}},"frase":"{{frase}}","motivo":"Comparar o grave."}""";

    private async Task<string> NaEscuta()
    {
        var chave = await NoRefino();
        await Escutar(chave);
        return chave;
    }

    private async Task Escutar(string chave)
    {
        var r = await Pedir("POST", "/criacao/escuta",
            $$"""{"texto":"Qual grave você prefere?","variavel":"grave","versao1":[{{GanhoJbl(2.0m, "Grave como está")}}],"versao2":[{{GanhoJbl(5.0m, "Grave mais cheio")}}]}""",
            chave);
        Assert.Equal(200, r.Status);
        await _b.Tela.EsvaziarAsync();
        Assert.Equal(FaseCriacao.Escuta, _vm.Fase);
    }

    [Fact]
    public async Task A_escuta_mostra_o_texto_os_botoes_1_e_2_com_o_1_marcado_e_troca_o_som_ao_vivo()
    {
        await NaEscuta();

        Assert.Equal(FaseCriacao.Escuta, _vm.Fase);
        Assert.True(_vm.FaseEscuta);
        Assert.Equal(EtapaCriacao.Escuta, _vm.Etapa);
        Assert.Equal("Qual grave você prefere?", _vm.TextoEscuta);
        Assert.Equal(("1", "2"), (_vm.TextoBotao1, _vm.TextoBotao2));
        Assert.Equal(("Prefiro o 1", "Prefiro o 2", "Tanto faz"), (_vm.TextoPrefiro1, _vm.TextoPrefiro2, _vm.TextoTantoFaz));
        Assert.True(_vm.Marcado1);
        Assert.False(_vm.Marcado2);
        Assert.Equal("Ouvir a versão 1, tocando agora", _vm.DescricaoBotao1);
        Assert.Null(_vm.AvisoEscuta);
        Assert.True(_vm.PodeFechar);
        Assert.Equal("+2.0", _b.Simulador.Valor("081", 0));
        SemIa();

        await _b.Rodar(_vm.Ouvir2.ExecutarAsync());

        Assert.True(_vm.Marcado2);
        Assert.False(_vm.Marcado1);
        Assert.Equal("+5.0", _b.Simulador.Valor("081", 0));
        Assert.Equal(_criacao.Escuta!.Master2, _b.Estado.Valor("080")!.Numero);

        await _b.Rodar(_vm.Ouvir1.ExecutarAsync());
        Assert.True(_vm.Marcado1);
        Assert.Equal("+2.0", _b.Simulador.Valor("081", 0));

        // Uma escrita que não confere: o aviso, e a escuta fica.
        _b.Simulador.EcosDiferentes["*081*0*5.0*"] = "#081#0#4.5#";
        await _b.Rodar(_vm.Ouvir2.ExecutarAsync());
        Assert.Equal("O aparelho ficou com outro valor.", _vm.AvisoEscuta);
        Assert.Equal(FaseCriacao.Escuta, _vm.Fase);
        SemIa();
    }

    [Fact]
    public async Task Tanto_faz_volta_a_1_devolve_a_escolha_e_a_rodada_segue_com_o_ajuste_no_palco()
    {
        await NaEscuta();
        await _b.Rodar(_vm.Ouvir2.ExecutarAsync());

        await _b.Rodar(_vm.TantoFaz.ExecutarAsync());

        Assert.Equal("+2.0", _b.Simulador.Valor("081", 0));
        Assert.True(_vm.Ajustando);
        Assert.Equal(FaseCriacao.Palco, _vm.Fase); // com a animação, os ajustes da escolha entram no palco
        Assert.Equal("Grave como está", _vm.AjusteDoPalco!.Frase);
        Assert.Contains(_vm.AjustesDaRodada, l => l.Frase == "Grave como está");
        Assert.Null(_vm.TextoEscuta);
        var e = _porta.Eventos().Single(x => x.Tipo == CriacaoMemoria.EventoEscuta).Criacao!;
        Assert.Equal(("grave", "tanto-faz"), (e.Variavel, e.Escolha));
    }

    [Fact]
    public async Task Prefiro_o_2_fica_na_versao_2()
    {
        await NaEscuta();

        await _b.Rodar(_vm.Prefiro2.ExecutarAsync());

        Assert.Equal("+5.0", _b.Simulador.Valor("081", 0));
        Assert.True(_vm.Ajustando);
        Assert.Equal("2", _porta.Eventos().Single(x => x.Tipo == CriacaoMemoria.EventoEscuta).Criacao!.Escolha);
    }

    [Fact]
    public async Task O_X_na_escuta_abre_a_folha_e_Cancelar_e_voltar_volta_a_copia_de_antes_da_criacao()
    {
        var chave = await NoRefino();
        var copia = _b.Copia.Ultima!;
        await Escutar(chave);
        await _b.Rodar(_vm.Ouvir2.ExecutarAsync());

        Assert.True(_vm.Voltar()); // a tecla Voltar, como o ✕
        Assert.True(_vm.FolhaCancelarAberta);
        await _b.Rodar(_vm.CancelarEVoltar.ExecutarAsync());

        Assert.Equal(FaseCriacao.Fechada, _vm.Fase);
        IgualACopia(copia);
        Assert.Contains(CriacaoMemoria.EventoCancelada, TiposDosEventos());
        Assert.DoesNotContain(CriacaoMemoria.EventoEscuta, TiposDosEventos());
    }

    [Fact]
    public async Task Sem_animacao_a_escolha_da_escuta_volta_ao_Aplicando()
    {
        using var vm = await Outro(animacao: false);
        await NaEscuta();
        Assert.Equal(FaseCriacao.Escuta, vm.Fase);

        await _b.Rodar(vm.Prefiro1.ExecutarAsync());

        Assert.Equal(FaseCriacao.Aplicando, vm.Fase);
    }

    [Fact]
    public async Task Na_troca_os_botoes_da_escuta_ficam_travados()
    {
        await NaEscuta();
        Assert.True(_vm.PodeTocarEscuta);
        Assert.False(_vm.Escrevendo);

        var troca = _vm.Ouvir2.ExecutarAsync();

        Assert.True(_vm.Escrevendo);
        Assert.False(_vm.PodeTocarEscuta);
        await _vm.Prefiro1.ExecutarAsync(); // travado: nada
        await _b.Rodar(troca);
        Assert.False(_vm.Escrevendo);
        Assert.True(_vm.PodeTocarEscuta);
        Assert.True(_vm.Marcado2);
        Assert.Equal(EtapaCriacao.Escuta, _criacao.Etapa);
    }

    [Fact]
    public async Task Um_toque_que_chega_com_a_escuta_ja_fechada_nao_avisa_nada()
    {
        await NaEscuta();
        var escolha = _criacao.EscolherAsync(EscolhaEscuta.TantoFaz); // a escolha anda e fecha a escuta

        var toque = _vm.Ouvir2.ExecutarAsync(); // o toque chega antes da tela saber, e espera a vez
        await _b.Rodar(escolha);
        await _b.Rodar(toque);

        Assert.True((await escolha).Ok);
        Assert.Null(_vm.Aviso);
        Assert.True(_vm.Ajustando);
    }

    [Fact]
    public async Task A_escuta_guarda_o_que_o_palco_ainda_nao_mostrou_da_rodada()
    {
        var chave = await NoRefino();
        await Ajuste(chave, valor: 3.5m, frase: "Reforçando o grave");
        await Ajuste(chave, codigo: "081", indice: 1, valor: 6.0m, frase: "Abrindo a direita");
        Assert.Equal("Reforçando o grave", _vm.AjusteDoPalco!.Frase);
        await Escutar(chave);

        await _b.Rodar(_vm.TantoFaz.ExecutarAsync());

        Assert.Equal(FaseCriacao.Palco, _vm.Fase);
        var frases = new List<string> { _vm.AjusteDoPalco!.Frase };
        while (_vm.PalcoMostrou() && _vm.AjusteDoPalco is { } a)
            frases.Add(a.Frase);
        Assert.Equal(["Reforçando o grave", "Abrindo a direita", "Grave como está"], frases);
    }
}
