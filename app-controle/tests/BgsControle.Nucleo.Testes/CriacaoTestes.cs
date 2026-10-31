using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using BgsControle.Nucleo.Aparelho;
using BgsControle.Nucleo.Escrita;
using BgsControle.Nucleo.Gerador;
using BgsControle.Nucleo.Leitura;
using BgsControle.Nucleo.Player;
using BgsControle.Nucleo.Porta;
using BgsControle.Nucleo.Presets;
using BgsControle.Nucleo.Registro;
using BgsControle.Nucleo.Seguranca;
using BgsControle.Nucleo.Setups;
using BgsControle.Nucleo.Uso;
using BgsControle.Simulador;

namespace BgsControle.Nucleo.Testes;

/// <summary>
/// A história 9d no núcleo, contra o processador simulado: a porta nos dois sentidos para a criação da memória
/// (CAP-18). Uma linha da matriz de <c>9d-porta-nos-dois-sentidos-para-a-criacao-da-memoria.md</c> por teste, mais
/// uma criação de ponta a ponta pela porta até o Salvar. O lado do usuário (responder, avaliar, salvar, cancelar)
/// vai direto no <see cref="CriacaoMemoria"/>; o ViewModel tem os testes dele.
/// </summary>
public sealed class CriacaoTestes : IDisposable
{
    private static readonly TemposSessao Tempos = new(
        Scan: TimeSpan.FromSeconds(1),
        TentativaConexao: TimeSpan.FromSeconds(1),
        PausaEntreTentativas: TimeSpan.FromMilliseconds(1),
        RespostaSenha: TimeSpan.FromSeconds(2),
        PrazoSenha: TimeSpan.FromSeconds(60));

    private static readonly TemposLeitura LeituraRapida = new(TimeSpan.Zero, TimeSpan.FromMilliseconds(100));

    private static readonly TemposEscrita Escrita = new(
        Intervalo: TimeSpan.FromMilliseconds(30),
        Arrasto: TimeSpan.FromMilliseconds(300),
        PrazoEco: TimeSpan.FromMilliseconds(300),
        PrazoReleitura: TimeSpan.FromMilliseconds(400));

    private static readonly TemposGerador TemposDoGerador = new(TimeSpan.FromSeconds(30), TimeSpan.FromMilliseconds(100));

    private static readonly Regex Senha = new(@"^\*000\*[0-9A-Z]{4}\*$");
    private static readonly IPAddress Casa = IPAddress.Parse("192.168.0.10");

    private readonly RelogioFalso _relogio = new();
    private readonly ProcessadorSimulado _simulador;
    private readonly CofreFalso _cofre = new() { Senha = "AB12" };
    private readonly Chaves _chaves;
    private readonly LogBle _log = new();
    private readonly Historico _historico;
    private readonly UsoCompartilhado _uso;
    private readonly Sessao _sessao;
    private readonly LeituraAutomatica _leitura;
    private readonly EstadoAparelho _estado;
    private readonly Escritor _escritor;
    private readonly TextoMemoria _guardadoCopia = new();
    private readonly TextoMemoria _guardadoSetups = new(CenarioFilme.SetupsJson);
    private readonly CopiaAntes _copia;
    private readonly GerentePresets _presets;
    private readonly GerenteSetups _setups;
    private readonly GeradorSinal _gerador;
    private readonly Pareamento _pareamento;
    private readonly PedidosConfirmacao _confirmacoes;
    private readonly CriacaoMemoria _criacao;
    private readonly PortaComandos _porta;

    public CriacaoTestes()
    {
        _simulador = new ProcessadorSimulado(_relogio) { AtrasoSenha = TimeSpan.Zero, AtrasoLeitura = TimeSpan.Zero };
        _chaves = new Chaves(new RepositorioMemoria());
        _historico = new Historico(new TextoMemoria(), _relogio, _log);
        _uso = new UsoCompartilhado(_relogio, _log);
        _sessao = new Sessao(_simulador, _cofre, _log, Tempos, _chaves, _historico, _uso);
        var leitor = new LeitorCompleto(_sessao, _log, LeituraRapida);
        _leitura = new LeituraAutomatica(_sessao, leitor);
        _estado = new EstadoAparelho(_sessao, _leitura);
        _escritor = new Escritor(_sessao, _estado, _log, Escrita, _relogio);
        _copia = new CopiaAntes(_sessao, _estado, _escritor, _leitura, _guardadoCopia, _log, _relogio);
        var link = new LinkCanais(_sessao, _estado, _escritor, new TextoMemoria(), _log);
        _presets = new GerentePresets(_sessao, _estado, _escritor, _leitura, _copia, new TextoMemoria(CenarioFilme.PresetsJson), _log, _relogio);
        var playlists = new GerentePlaylists(new TextoMemoria(), _log);
        _setups = new GerenteSetups(_sessao, _estado, _escritor, _leitura, _copia, link, _presets, playlists, _chaves,
            _guardadoSetups, new TextoMemoria(), _log, _relogio);
        _gerador = new GeradorSinal(_sessao, _estado, _leitura, _copia, _log, Escrita, TemposDoGerador, _relogio);
        var monitor = new MonitorAparelho(_sessao, _leitura, _relogio);
        var comandos = new ComandosAparelho(_sessao, _estado, _leitura, _copia, _cofre, _log, _gerador, Escrita, _relogio);
        _pareamento = new Pareamento(new TextoMemoria(), _relogio, _log);
        _confirmacoes = new PedidosConfirmacao(_relogio, _log);
        _criacao = new CriacaoMemoria(_sessao, _estado, _escritor, _copia, _setups, _uso, _log);
        _porta = new PortaComandos(_sessao, _chaves, _uso, _historico, _pareamento, _confirmacoes, new RedeFalsa(), _estado, _escritor,
            _setups, _gerador, comandos, monitor, _log, _relogio, 0, _criacao);
    }

    /// <summary>Nada além do catálogo chega ao simulador (nenhum 240, 241 ou 23N); a senha nunca vai ao histórico.</summary>
    public void Dispose()
    {
        _porta.Dispose();
        _criacao.Dispose();
        Assert.DoesNotContain(_simulador.Escritas, e => e.StartsWith("*24", StringComparison.Ordinal) || e.StartsWith("*23", StringComparison.Ordinal));
        Assert.All(_simulador.Escritas, e =>
            Assert.True(Senha.IsMatch(e) || Catalogo.EhLeitura(e) || Catalogo.EscritaDeAudio(e) is not null
                        || Catalogo.ComandoDeGerador(e) is not null, e));
        Assert.DoesNotContain("AB12", Historico.ParaJson(_historico.Entradas));
    }

    // ----------------------------------------------------------------- apoio

    private async Task Conectar()
    {
        await _chaves.Carregada;
        await _copia.Carregada;
        await _historico.Carregado;
        await _pareamento.Carregado;
        await _sessao.ConectarAsync();
        Assert.Equal(EstadoSessao.Conectado, _sessao.Estado);
        await AndarAte(() => _estado.Pronto && !_estado.Lendo && !_leitura.Lendo && !_copia.EmAndamento
                             && _gerador.Estado == EstadoGerador.Desligado && _setups.Conferido);
    }

    private async Task<string> Parear()
    {
        Assert.True((await _chaves.DefinirAsync(Chave.ControlePelaRede, true, OrigemComando.Tela)).Ok);
        await _porta.AtualizarAsync();
        Assert.True(_porta.Aberta);
        var r = await Pedir("POST", "/parear", $$"""{"codigo":"{{_pareamento.Codigo}}","nome":"Claude no notebook"}""");
        Assert.Equal(200, r.Status);
        return Json(r).GetProperty("chave").GetString()!;
    }

    /// <summary>Conecta (o Casa 2.1 com a FILME), abre a porta e pareia.</summary>
    private async Task<string> Pronto()
    {
        await Conectar();
        return await Parear();
    }

    private Task<RespostaPorta> Pedir(string metodo, string alvo, string? corpo = null, string? chave = null) =>
        _porta.AtenderAsync(PedidoPorta.De(metodo, alvo, Casa, corpo, chave));

    private static JsonElement Json(RespostaPorta r) => JsonDocument.Parse(r.Json).RootElement;

    private static string? Motivo(RespostaPorta r) => Json(r).TryGetProperty("motivo", out var m) ? m.GetString() : null;

    private async Task AndarAte(Func<bool> condicao, int passos = 3000)
    {
        for (var i = 0; i < passos && !condicao(); i++)
        {
            _relogio.Avancar(TimeSpan.FromMilliseconds(10));
            await Task.Delay(1);
        }
        Assert.True(condicao(), "a condição não ficou verdadeira");
    }

    private static async Task EsperarSemAndar(Func<bool> condicao)
    {
        for (var i = 0; i < 500 && !condicao(); i++)
            await Task.Delay(10);
        Assert.True(condicao(), "a condição não ficou verdadeira");
    }

    private async Task<T> Rodar<T>(Task<T> tarefa)
    {
        await AndarAte(() => tarefa.IsCompleted);
        return await tarefa;
    }

    /// <summary>Abre a criação pela porta.</summary>
    private async Task<string> Aberta()
    {
        var chave = await Pronto();
        var r = await Pedir("POST", "/criacao/abrir", null, chave);
        Assert.Equal(200, r.Status);
        return chave;
    }

    private Task<RespostaPorta> Pergunta(string chave, bool ultima, string texto = "Para que é esta memória?", string respostas = """["Música","Filme","Jogo"]""") =>
        Pedir("POST", "/criacao/pergunta", $$"""{"texto":"{{texto}}","respostas":{{respostas}},"ultima":{{(ultima ? "true" : "false")}}}""", chave);

    /// <summary>Abre, manda a última pergunta e o usuário responde: os ajustes estão autorizados.</summary>
    private async Task<string> Autorizada()
    {
        var chave = await Aberta();
        Assert.Equal(200, (await Pergunta(chave, ultima: true)).Status);
        Assert.True(_criacao.Responder(0).Ok);
        Assert.True(_criacao.Autorizado);
        return chave;
    }

    private Task<RespostaPorta> Ajuste(string chave, string codigo = "081", int? indice = 2, decimal valor = 2.0m, string frase = "Mais corpo",
        string motivo = "A resposta pediu mais impacto.")
    {
        var campoIndice = indice is { } i ? $",\"indice\":{i}" : "";
        return Rodar(Pedir("POST", "/criacao/ajuste",
            $$"""{"codigo":"{{codigo}}"{{campoIndice}},"valor":{{valor.ToString(CultureInfo.InvariantCulture)}},"frase":"{{frase}}","motivo":"{{motivo}}"}""", chave));
    }

    private IReadOnlyList<EventoPorta> EventosDaCriacao(string? tipo = null) =>
        _porta.Eventos().Where(e => e.Criacao is not null && (tipo is null || e.Tipo == tipo)).ToArray();

    /// <summary>Os eventos do tipo, como o cliente lê no <c>GET /eventos</c>.</summary>
    private async Task<JsonElement[]> EventosJson(string chave, string tipo) =>
        Json(await Pedir("GET", "/eventos?desde=0", chave: chave)).GetProperty("eventos").EnumerateArray()
            .Where(e => e.GetProperty("tipo").GetString() == tipo).ToArray();

    /// <summary>Até o Salvar: um ajuste, o resumo, "Avaliar o som" e "Ótimo, pode salvar".</summary>
    private async Task<string> NoSalvar()
    {
        var chave = await Autorizada();
        Assert.Equal(200, (await Ajuste(chave)).Status);
        Assert.Equal(200, (await Pedir("POST", "/criacao/resumo", null, chave)).Status);
        Assert.True(_criacao.AvaliarSom().Ok);
        Assert.True(_criacao.PodeSalvar().Ok);
        return chave;
    }

    /// <summary>Cada parâmetro de áudio do aparelho (relido do simulador) bate com a cópia de antes.</summary>
    private void IgualACopia(Copia copia)
    {
        foreach (var c in Catalogo.ConsultasMarco1)
        {
            var guardado = copia.Valores.FirstOrDefault(v => v.Codigo == c.Parametro.Codigo && v.Indice == c.Indice);
            if (guardado is null)
                continue;
            var agora = _simulador.Valor(c.Parametro.Codigo, c.Indice);
            Assert.True(agora is not null && Camadas.Iguais(c.Parametro.Codigo, agora, guardado.NoFio),
                $"{c.Parametro.Codigo}/{c.Indice}: {agora} no aparelho, {guardado.NoFio} na cópia");
        }
    }

    // ----------------------------------------------------------------- matriz

    [Fact]
    public async Task Abrir_no_Casa_21_com_a_FILME_guarda_a_copia_e_vai_as_perguntas()
    {
        var chave = await Pronto();
        Assert.Equal("FILME", _setups.MemoriaEmUso?.Nome);

        var r = await Pedir("POST", "/criacao/abrir", null, chave);

        Assert.Equal(200, r.Status);
        Assert.Equal("perguntas", Json(r).GetProperty("etapa").GetString());
        Assert.Equal(EtapaCriacao.Perguntas, _criacao.Etapa);
        var copia = _copia.Ultima!;
        Assert.Equal(CriacaoMemoria.Tipo, copia.Operacao!.Tipo);
        Assert.Equal(CasaDoDenis.IdSetupCasa, copia.Operacao.Alvo);
        Assert.Null(copia.Operacao.Etapa);
        Assert.Contains(copia.Valores, v => v.Codigo == "081" && v.Indice == 2 && v.NoFio == _simulador.Valor("081", 2));
        Assert.Equal(CriacaoMemoria.Tipo, CopiaAntes.DeJson(_guardadoCopia.Texto!)!.Operacao!.Tipo); // guardada no celular
        Assert.True(_uso.IaAtiva); // a tarefa começou: o vigia de 30 s vale

        var estado = Json(await Pedir("GET", "/criacao", chave: chave));
        Assert.Equal("perguntas", estado.GetProperty("etapa").GetString());
        Assert.False(estado.GetProperty("autorizado").GetBoolean());
        Assert.Equal(_setups.SetupEmUso!.Nome, estado.GetProperty("setup").GetProperty("nome").GetString());
        Assert.Equal("FILME", estado.GetProperty("somAntes").GetString());
        Assert.Empty(estado.GetProperty("perguntas").EnumerateArray());
    }

    [Fact]
    public async Task Abrir_com_o_Setup_em_duvida_da_409_e_nada_e_guardado()
    {
        _simulador.Definir("050", 2, "0.0");
        await Conectar();
        await AndarAte(() => _setups.Estado == EstadoConferencia.Duvida && !_setups.Ocupado);
        var chave = await Parear();

        var r = await Pedir("POST", "/criacao/abrir", null, chave);

        Assert.Equal(409, r.Status);
        Assert.Equal(GerenteSetups.MsgDuvida, Motivo(r));
        Assert.Null(_copia.Ultima?.Operacao);
        Assert.False(_criacao.Aberta);
    }

    [Fact]
    public async Task Abrir_sem_Setup_em_uso_da_409_e_nada_e_guardado()
    {
        _simulador.Definir("050", 2, "0.0");
        await Conectar();
        await AndarAte(() => _setups.Estado == EstadoConferencia.Duvida && !_setups.Ocupado);
        Assert.True((await Rodar(_setups.SeguirSemSetupAsync())).Ok);
        var chave = await Parear();

        var r = await Pedir("POST", "/criacao/abrir", null, chave);

        Assert.Equal(409, r.Status);
        Assert.Equal(CriacaoMemoria.MsgSemSetup, Motivo(r));
        Assert.Null(_copia.Ultima?.Operacao);
        Assert.False(_criacao.Aberta);
    }

    [Fact]
    public async Task Abrir_com_outra_criacao_aberta_da_409()
    {
        var chave = await Aberta();
        var copia = _copia.Ultima;

        var r = await Pedir("POST", "/criacao/abrir", null, chave);

        Assert.Equal(409, r.Status);
        Assert.Equal(CriacaoMemoria.MsgJaAberta, Motivo(r));
        Assert.Same(copia, _copia.Ultima);
    }

    [Fact]
    public async Task Pergunta_com_3_respostas_aparece_e_com_2_ou_5_da_400_e_outra_pendente_409()
    {
        var chave = await Aberta();

        Assert.Equal(400, (await Pergunta(chave, false, respostas: """["Música","Filme"]""")).Status);
        Assert.Equal(400, (await Pergunta(chave, false, respostas: """["a","b","c","d","e"]""")).Status);
        Assert.Equal(400, (await Pergunta(chave, false, respostas: """["a","","c"]""")).Status);
        Assert.Empty(_criacao.Perguntas);

        var r = await Pergunta(chave, ultima: false);
        Assert.Equal(200, r.Status);
        var p = Assert.Single(_criacao.Perguntas);
        Assert.Equal(1, p.Numero);
        Assert.Equal("Para que é esta memória?", p.Texto);
        Assert.Equal(["Música", "Filme", "Jogo"], p.Respostas);
        Assert.False(p.Ultima);
        Assert.Same(p, _criacao.PerguntaAtual);

        var outra = await Pergunta(chave, ultima: true, texto: "Onde você senta?");
        Assert.Equal(409, outra.Status);
        Assert.Equal(CriacaoMemoria.MsgPerguntaPendente, Motivo(outra));

        // Com 4 respostas também vale (depois da resposta à anterior).
        Assert.True(_criacao.Responder(1).Ok);
        Assert.Equal(200, (await Pergunta(chave, ultima: true, respostas: """["Sofá","Chão","Mesa","Cama"]""")).Status);
    }

    [Fact]
    public async Task O_toque_na_2a_resposta_chega_no_eventos_e_o_Outro_vazio_nao_responde()
    {
        var chave = await Aberta();
        Assert.Equal(200, (await Pergunta(chave, ultima: false)).Status);

        var vazio = _criacao.ResponderOutro("   ");
        Assert.False(vazio.Ok);
        Assert.Equal(CriacaoMemoria.MsgOutroVazio, vazio.Motivo);
        Assert.NotNull(_criacao.PerguntaAtual);
        Assert.Empty(EventosDaCriacao());

        Assert.True(_criacao.Responder(1).Ok);

        var eventos = Json(await Pedir("GET", "/eventos?desde=0", chave: chave)).GetProperty("eventos").EnumerateArray()
            .Where(e => e.GetProperty("tipo").GetString() == CriacaoMemoria.EventoResposta).ToArray();
        var e = Assert.Single(eventos);
        Assert.Equal(1, e.GetProperty("numero").GetInt32());
        Assert.Equal("Filme", e.GetProperty("texto").GetString());
        Assert.False(e.GetProperty("doOutro").GetBoolean());
        Assert.Null(_criacao.PerguntaAtual);
        Assert.Equal(EtapaCriacao.Perguntas, _criacao.Etapa); // não era a última

        // O "Outro…" com texto responde e diz que veio dele.
        Assert.Equal(200, (await Pergunta(chave, ultima: true, texto: "Onde você senta?")).Status);
        Assert.True(_criacao.ResponderOutro(" No pufe ").Ok);
        var outro = EventosDaCriacao(CriacaoMemoria.EventoResposta)[^1].Criacao!;
        Assert.Equal(2, outro.Numero);
        Assert.Equal("No pufe", outro.Texto);
        Assert.True(outro.DoOutro);
        Assert.Equal(EtapaCriacao.Ajustando, _criacao.Etapa);

        var estado = Json(await Pedir("GET", "/criacao", chave: chave));
        Assert.True(estado.GetProperty("autorizado").GetBoolean());
        Assert.Equal(1, estado.GetProperty("rodada").GetInt32());
        var perguntas = estado.GetProperty("perguntas").EnumerateArray().ToArray();
        Assert.Equal("Filme", perguntas[0].GetProperty("resposta").GetString());
        Assert.True(perguntas[1].GetProperty("doOutro").GetBoolean());
    }

    [Fact]
    public async Task Ajuste_antes_da_resposta_a_ultima_pergunta_da_409_e_nada_sai()
    {
        var chave = await Aberta();
        Assert.Equal(200, (await Pergunta(chave, ultima: true)).Status);

        var r = await Ajuste(chave);

        Assert.Equal(409, r.Status);
        Assert.Equal("Os ajustes só vêm depois da resposta à última pergunta.", Motivo(r));
        Assert.DoesNotContain("*081*2*2.0*", _simulador.Escritas);
        Assert.Null(_copia.Ultima!.Operacao!.Etapa); // nenhum ajuste escrito
    }

    [Fact]
    public async Task Ajuste_do_ganho_da_saida_3_devolve_antes_novo_e_relido_vai_como_IA_e_o_espelho_mostra()
    {
        var chave = await Autorizada();
        var antes = _simulador.Valor("081", 2);

        var r = await Ajuste(chave, frase: "Mais corpo no sub", motivo: "Os graves estavam magros.");

        Assert.Equal(200, r.Status);
        var j = Json(r);
        Assert.Equal(antes, j.GetProperty("antes").GetString());
        Assert.Equal("+2,0 dB", j.GetProperty("novoExibicao").GetString());
        Assert.True(Camadas.Iguais("081", "+2.0", j.GetProperty("novo").GetString()!));
        Assert.True(Camadas.Iguais("081", "+2.0", j.GetProperty("relido").GetString()!));
        Assert.Equal("+2,0 dB", j.GetProperty("relidoExibicao").GetString());
        Assert.False(j.GetProperty("diferente").GetBoolean());
        Assert.Equal("Mais corpo no sub", j.GetProperty("frase").GetString());
        Assert.Equal("Os graves estavam magros.", j.GetProperty("motivo").GetString());
        Assert.Equal(1, j.GetProperty("rodada").GetInt32());
        Assert.Contains("*081*2*2.0*", _simulador.Escritas);
        Assert.Equal("+2.0", _simulador.Valor("081", 2));
        Assert.Equal("+2,0 dB", Camadas.Exibir("081", _estado.Valor("081", 2)!.NoFio));
        Assert.True(Camadas.Iguais("081", "+2.0", _simulador.Valor("081", 3)!)); // o link das saídas 3 e 4 repetiu
        Assert.False(j.TryGetProperty("mensagem", out _)); // sem o aviso do link
        Assert.Contains(_historico.Entradas, e => e.Origem == OrigemComando.Ia && e.Tipo == TipoComando.AjusteAudio && e.Enviado
                                                  && e.Endereco == new Endereco("081", 2));
        Assert.Equal(CriacaoMemoria.EtapaAjustes, _copia.Ultima!.Operacao!.Etapa); // a marca sabe que algo foi escrito
        var a = Assert.Single(_criacao.Ajustes);
        Assert.Equal(antes, a.Antes);

        // Fora da regulagem: as 15 bandas e o mute dão 400, sem enviar nada.
        var escritas = _simulador.Escritas.Count;
        var banda = await Ajuste(chave, codigo: "020", indice: 3, valor: 1.0m);
        Assert.Equal(400, banda.Status);
        Assert.Equal(CriacaoMemoria.MsgSoRegulagem, Motivo(banda));
        Assert.Equal(400, (await Ajuste(chave, codigo: "100", indice: 0, valor: 1m)).Status);
        Assert.Equal(400, (await Rodar(Pedir("POST", "/criacao/ajuste", """{"codigo":"081","indice":2,"valor":1.0,"frase":"","motivo":"x"}""", chave))).Status);
        Assert.Equal(escritas, _simulador.Escritas.Count);
    }

    [Fact]
    public async Task Quatro_ajustes_seguidos_da_criacao_nao_pedem_o_Continuar_e_o_ajuste_comum_da_409()
    {
        var chave = await Autorizada();

        for (var i = 0; i < 4; i++)
            Assert.Equal(200, (await Ajuste(chave, valor: 1.0m + i)).Status);

        Assert.Null(_confirmacoes.Atual);
        Assert.Equal(0, _porta.AjustesSeguidos);
        Assert.Equal(4, _criacao.Ajustes.Count);

        var comum = await Rodar(Pedir("POST", "/ajuste", """{"codigo":"081","indice":2,"valor":9.0}""", chave));
        Assert.Equal(409, comum.Status);
        Assert.Equal(PortaComandos.MsgCriacaoAberta, Motivo(comum));
        var aplicar = await Pedir("POST", "/memoria/aplicar", """{"memoria":"FILME"}""", chave);
        Assert.Equal(409, aplicar.Status);
        Assert.Equal(PortaComandos.MsgCriacaoAberta, Motivo(aplicar));
        Assert.Null(_confirmacoes.Atual);
        Assert.DoesNotContain("*081*2*9.0*", _simulador.Escritas);

        // A tela também não escreve: a marca da cópia trava.
        var tela = await Rodar(_escritor.EscreverAsync(Catalogo.ParametroDe("081")!, 0, 3.0m));
        Assert.False(tela.Ok);
        Assert.Equal(CopiaAntes.MsgOutraOperacao, tela.Motivo);
    }

    [Fact]
    public async Task Releitura_diferente_responde_diferente_e_o_resumo_da_409_ate_corrigir()
    {
        var chave = await Autorizada();
        _simulador.EcosDiferentes["*081*2*2.0*"] = "#081#2#1.5#";

        var r = await Ajuste(chave);

        Assert.Equal(200, r.Status);
        Assert.True(Json(r).GetProperty("diferente").GetBoolean());
        Assert.Equal("+1,5 dB", Json(r).GetProperty("relidoExibicao").GetString());
        var resumo = await Pedir("POST", "/criacao/resumo", null, chave);
        Assert.Equal(409, resumo.Status);
        Assert.Equal(CriacaoMemoria.MsgResumoDiferente(["Ganho da saída 3"]), Motivo(resumo));
        Assert.Equal(EtapaCriacao.Ajustando, _criacao.Etapa);

        _simulador.EcosDiferentes.Clear();
        Assert.Equal(200, (await Ajuste(chave)).Status);
        resumo = await Pedir("POST", "/criacao/resumo", null, chave);
        Assert.Equal(200, resumo.Status);
        Assert.Equal(EtapaCriacao.Resumo, _criacao.Etapa);
    }

    [Fact]
    public async Task Refino_o_Como_ficou_volta_aos_ajustes_e_o_resumo_destaca_so_a_rodada_2()
    {
        var chave = await Autorizada();
        Assert.Equal(200, (await Ajuste(chave)).Status);
        Assert.Equal(200, (await Pedir("POST", "/criacao/resumo", null, chave)).Status);
        Assert.Equal(400, (await Pedir("POST", "/criacao/como-ficou", """{"respostas":["só uma"]}""", chave)).Status);
        Assert.Equal(200, (await Pedir("POST", "/criacao/como-ficou", """{"respostas":["Quero mais impacto","Os graves embolaram"]}""", chave)).Status);
        Assert.Equal(409, (await Ajuste(chave)).Status); // no resumo, nada de ajustes
        Assert.True(_criacao.AvaliarSom().Ok);
        Assert.Equal(EtapaCriacao.ComoFicou, _criacao.Etapa);
        Assert.Equal(["Quero mais impacto", "Os graves embolaram"], _criacao.ComoFicouAtual!.Respostas);

        Assert.True(_criacao.ResponderComoFicou(0).Ok);

        var e = Assert.Single(EventosDaCriacao(CriacaoMemoria.EventoComoFicou)).Criacao!;
        Assert.Equal("Quero mais impacto", e.Texto);
        Assert.False(e.Salvar);
        Assert.Equal(EtapaCriacao.Ajustando, _criacao.Etapa);
        Assert.Equal(2, _criacao.Rodada);

        Assert.Equal(200, (await Ajuste(chave, codigo: "081", indice: 0, valor: 8.0m)).Status);
        var r = await Pedir("POST", "/criacao/resumo", null, chave);
        Assert.Equal(200, r.Status);
        var linhas = Json(r).GetProperty("resumo").EnumerateArray().ToArray();
        Assert.Equal(2, linhas.Length);
        Assert.Equal("Ganho da saída 3", linhas[0].GetProperty("rotulo").GetString());
        Assert.False(linhas[0].GetProperty("daRodada").GetBoolean());
        Assert.Equal("Ganho da saída 1", linhas[1].GetProperty("rotulo").GetString());
        Assert.True(linhas[1].GetProperty("daRodada").GetBoolean());
        Assert.Equal("+8,0 dB", linhas[1].GetProperty("valor").GetString());
    }

    [Fact]
    public async Task Salvar_guarda_a_regulagem_relida_em_uso_a_marca_sai_e_o_nome_repetido_fica_no_Salvar()
    {
        var chave = await Autorizada();
        Assert.Equal(200, (await Ajuste(chave)).Status);
        Assert.Equal(200, (await Pedir("POST", "/criacao/resumo", null, chave)).Status);
        Assert.True(_criacao.AvaliarSom().Ok);
        Assert.False((await _criacao.SalvarAsync("MUSICA ORIG")).Ok); // antes do "Ótimo, pode salvar", não

        Assert.True(_criacao.PodeSalvar().Ok);
        var otimo = EventosDaCriacao(CriacaoMemoria.EventoComoFicou)[^1].Criacao!;
        Assert.True(otimo.Salvar);
        Assert.Equal(CriacaoMemoria.OtimoPodeSalvar, otimo.Texto);
        Assert.Equal(EtapaCriacao.Salvar, _criacao.Etapa);

        var repetido = await _criacao.SalvarAsync("filme");
        Assert.False(repetido.Ok);
        Assert.Equal(GerenteSetups.MsgNomeRepetidoMemoria(_setups.SetupEmUso!.Nome), repetido.Motivo);
        Assert.Equal(EtapaCriacao.Salvar, _criacao.Etapa);
        Assert.NotNull(_copia.Ultima!.Operacao);

        var r = await _criacao.SalvarAsync("MUSICA ORIG");

        Assert.True(r.Ok, r.Motivo);
        Assert.Equal(EtapaCriacao.Fechada, _criacao.Etapa);
        var nova = _setups.MemoriaEmUso!;
        Assert.Equal("MUSICA ORIG", nova.Nome);
        Assert.Equal(CasaDoDenis.IdSetupCasa, nova.IdSetup);
        Assert.Equal(nova.Id, _setups.Ativa(nova.IdSetup)!.Id);
        Assert.Equal(CenarioFilme.IdPreset, nova.IdPreset); // lembra o preset em uso
        Assert.Contains(nova.Regulagem, v => v.Codigo == "081" && v.Indice == 2 && v.NoFio == "+2.0");
        Assert.Empty(Camadas.Diferentes(nova.Regulagem, _estado.Copiar()));
        Assert.Null(_copia.Ultima!.Operacao);
        Assert.Null(_copia.PelaMetade);
        var salva = Assert.Single(EventosDaCriacao(CriacaoMemoria.EventoSalva)).Criacao!;
        Assert.Equal("MUSICA ORIG", salva.Memoria);
        Assert.Equal(200, (await Rodar(Pedir("POST", "/ajuste", """{"codigo":"081","indice":2,"valor":3.0}""", chave))).Status); // a porta volta ao normal
    }

    [Fact]
    public async Task O_X_depois_de_2_ajustes_volta_a_copia_fecha_e_nenhuma_memoria_nasce()
    {
        var chave = await Autorizada();
        var copia = _copia.Ultima!;
        Assert.Equal(200, (await Ajuste(chave)).Status);
        Assert.Equal(200, (await Ajuste(chave, codigo: "050", indice: 3, valor: 30m)).Status);
        var memorias = _setups.Dados.Memorias.Count;

        var r = await Rodar(_criacao.CancelarAsync(CriacaoMemoria.PorUsuario));

        Assert.True(r.Ok, r.Motivo);
        Assert.Equal(EtapaCriacao.Fechada, _criacao.Etapa);
        IgualACopia(copia);
        Assert.Equal(memorias, _setups.Dados.Memorias.Count);
        Assert.Null(_copia.Ultima!.Operacao);
        Assert.Null(_copia.PelaMetade);
        var e = Assert.Single(EventosDaCriacao(CriacaoMemoria.EventoCancelada)).Criacao!;
        Assert.Equal(CriacaoMemoria.PorUsuario, e.Por);
        Assert.True(e.Voltou);
        Assert.Equal("fechada", Json(await Pedir("GET", "/criacao", chave: chave)).GetProperty("etapa").GetString());
    }

    [Fact]
    public async Task Cancelar_pela_porta_volta_a_copia_e_avisa_por_porta()
    {
        var chave = await Autorizada();
        var copia = _copia.Ultima!;
        Assert.Equal(200, (await Ajuste(chave)).Status);

        var r = await Rodar(Pedir("POST", "/criacao/cancelar", null, chave));

        Assert.Equal(200, r.Status);
        Assert.True(Json(r).GetProperty("voltou").GetBoolean());
        IgualACopia(copia);
        var e = Assert.Single(await EventosJson(chave, CriacaoMemoria.EventoCancelada));
        Assert.Equal(CriacaoMemoria.PorPorta, e.GetProperty("por").GetString());
        Assert.True(e.GetProperty("voltou").GetBoolean());
        Assert.Equal(409, (await Pedir("POST", "/criacao/cancelar", null, chave)).Status); // nada aberto
    }

    [Fact]
    public async Task Cancelar_com_a_volta_falhando_fecha_responde_voltou_false_e_fica_pela_metade()
    {
        var chave = await Autorizada();
        Assert.Equal(200, (await Ajuste(chave)).Status); // 081 da saída 3: +6,0 → +2,0
        _simulador.EcosDiferentes["*081*2*6.0*"] = "#081#2#5.0#"; // a volta não confere

        var r = await Rodar(Pedir("POST", "/criacao/cancelar", null, chave));

        Assert.Equal(200, r.Status);
        Assert.False(Json(r).GetProperty("voltou").GetBoolean());
        Assert.False(_criacao.Aberta);
        Assert.Equal(CriacaoMemoria.Tipo, _copia.PelaMetade?.Tipo);
        var e = Assert.Single(await EventosJson(chave, CriacaoMemoria.EventoCancelada));
        Assert.Equal(CriacaoMemoria.PorPorta, e.GetProperty("por").GetString());
        Assert.False(e.GetProperty("voltou").GetBoolean());
        Assert.False(string.IsNullOrEmpty(e.GetProperty("motivo").GetString()));
    }

    [Fact]
    public async Task Escrita_sem_confirmacao_deixa_o_resumo_em_409_ate_um_ajuste_confirmado_ali()
    {
        var chave = await Autorizada();
        _simulador.Silenciar.Add("*081*2*2.0*");
        _simulador.Silenciar.Add(new Consulta(Catalogo.ParametroDe("081")!, 2).Texto); // nem a releitura responde

        var r = await Ajuste(chave);

        Assert.Equal(409, r.Status);
        Assert.Equal(Escritor.MsgSemConfirmacao, Motivo(r));
        var resumo = await Pedir("POST", "/criacao/resumo", null, chave);
        Assert.Equal(409, resumo.Status);
        Assert.Equal(CriacaoMemoria.MsgResumoDiferente(["Ganho da saída 3"]), Motivo(resumo));

        _simulador.Silenciar.Clear();
        Assert.Equal(200, (await Ajuste(chave)).Status);
        Assert.Equal(200, (await Pedir("POST", "/criacao/resumo", null, chave)).Status);
    }

    [Fact]
    public async Task Link_que_nao_acompanha_deixa_o_resumo_em_409_ate_acompanhar()
    {
        var chave = await Autorizada();
        _simulador.EcosDiferentes["*081*3*2.0*"] = "#081#3#1.5#"; // a saída 4 (o link) não acompanha

        var r = await Ajuste(chave);

        Assert.Equal(200, r.Status);
        Assert.Equal(Escritor.MsgNaoAcompanhou(3), Json(r).GetProperty("mensagem").GetString());
        Assert.Equal(409, (await Pedir("POST", "/criacao/resumo", null, chave)).Status);

        _simulador.EcosDiferentes.Clear();
        Assert.Equal(200, (await Ajuste(chave)).Status);
        Assert.Equal(200, (await Pedir("POST", "/criacao/resumo", null, chave)).Status);
    }

    [Fact]
    public async Task Cancelar_durante_o_Salvar_e_recusado_e_o_Salvar_fecha()
    {
        await NoSalvar();
        Task<ResultadoCriacao>? cancelar = null;
        _guardadoSetups.AoGravar = () => cancelar = _criacao.CancelarAsync(CriacaoMemoria.PorPorta);

        var r = await Rodar(_criacao.SalvarAsync("MUSICA ORIG"));

        Assert.True(r.Ok, r.Motivo);
        var c = await cancelar!;
        Assert.False(c.Ok);
        Assert.Equal(CriacaoMemoria.MsgSalvando, c.Motivo);
        Assert.False(_criacao.Aberta);
        Assert.Equal("MUSICA ORIG", _setups.MemoriaEmUso?.Nome);
        Assert.Null(_copia.Ultima!.Operacao);
        Assert.Null(_copia.PelaMetade);
        Assert.Empty(EventosDaCriacao(CriacaoMemoria.EventoCancelada));
        Assert.Single(EventosDaCriacao(CriacaoMemoria.EventoSalva));
    }

    [Fact]
    public async Task BLE_cai_durante_o_Salvar_o_Salvar_ganha_e_nada_fica_pela_metade()
    {
        await NoSalvar();
        _guardadoSetups.AoGravar = _simulador.Derrubar;

        var r = await Rodar(_criacao.SalvarAsync("MUSICA ORIG"));
        await AndarAte(() => _sessao.Estado != EstadoSessao.Conectado);

        Assert.True(r.Ok, r.Motivo);
        Assert.False(_criacao.Aberta);
        Assert.Null(_copia.Ultima!.Operacao);
        Assert.Null(_copia.PelaMetade);
        Assert.Empty(EventosDaCriacao(CriacaoMemoria.EventoInterrompida));
        Assert.Single(EventosDaCriacao(CriacaoMemoria.EventoSalva));
    }

    [Fact]
    public async Task BLE_cai_durante_um_Salvar_que_falha_interrompe_depois()
    {
        await NoSalvar();
        _guardadoSetups.AoGravar = _simulador.Derrubar;
        _guardadoSetups.FalharAoGravar = true;

        var r = await Rodar(_criacao.SalvarAsync("MUSICA ORIG"));
        await AndarAte(() => _sessao.Estado != EstadoSessao.Conectado);

        Assert.False(r.Ok);
        Assert.False(_criacao.Aberta);
        Assert.Single(EventosDaCriacao(CriacaoMemoria.EventoInterrompida));
        Assert.Empty(EventosDaCriacao(CriacaoMemoria.EventoSalva));
        Assert.Equal(CriacaoMemoria.EtapaAjustes, _copia.Ultima!.Operacao!.Etapa); // fica para a próxima conexão
    }

    [Fact]
    public async Task Cancelar_numa_folha_de_risco_durante_a_criacao_cancela_e_volta_a_copia()
    {
        var chave = await Autorizada();
        Assert.True((await _chaves.DefinirAsync(Chave.Gerador, true, OrigemComando.Tela)).Ok);
        var copia = _copia.Ultima!;
        Assert.Equal(200, (await Ajuste(chave)).Status);

        var tom = Pedir("POST", "/gerador/tom", """{"hz":1000,"db":-30.0}""", chave);
        await EsperarSemAndar(() => _confirmacoes.Atual is not null);
        _confirmacoes.Recusar();
        var r = await Rodar(tom);

        Assert.Equal(409, r.Status);
        Assert.Equal("O usuário cancelou a nova memória.", Motivo(r));
        Assert.Equal(EtapaCriacao.Fechada, _criacao.Etapa);
        IgualACopia(copia);
        Assert.Equal(CriacaoMemoria.PorFolha, Assert.Single(EventosDaCriacao(CriacaoMemoria.EventoCancelada)).Criacao!.Por);
        Assert.Equal(EstadoGerador.Desligado, _gerador.Estado);
        Assert.Contains(_historico.Entradas, e => e.Origem == OrigemComando.Ia && !e.Enviado && e.Motivo == CriacaoMemoria.MsgCanceladaNaFolha);
    }

    [Fact]
    public async Task Confirmar_a_folha_de_risco_toca_o_tom_e_a_criacao_segue()
    {
        var chave = await Autorizada();
        Assert.True((await _chaves.DefinirAsync(Chave.Gerador, true, OrigemComando.Tela)).Ok);
        Assert.Equal(200, (await Ajuste(chave)).Status);

        var tom = Pedir("POST", "/gerador/tom", """{"hz":1000,"db":-30.0}""", chave);
        await EsperarSemAndar(() => _confirmacoes.Atual is not null);
        _confirmacoes.Confirmar();
        var r = await Rodar(tom);

        Assert.Equal(200, r.Status);
        Assert.Equal(EstadoGerador.Tocando, _gerador.Estado);
        Assert.Equal(EtapaCriacao.Ajustando, _criacao.Etapa);
        Assert.Equal(200, (await Rodar(Pedir("POST", "/gerador/parar", null, chave))).Status);
        Assert.Equal(200, (await Ajuste(chave, valor: 3.0m)).Status); // a criação segue
        Assert.Empty(EventosDaCriacao(CriacaoMemoria.EventoCancelada));
    }

    [Fact]
    public async Task BLE_cai_depois_de_1_ajuste_interrompe_e_ao_reconectar_fica_pela_metade_sem_Terminar()
    {
        var chave = await Autorizada();
        var copia = _copia.Ultima!;
        Assert.Equal(200, (await Ajuste(chave)).Status);

        _simulador.Derrubar();
        await AndarAte(() => _sessao.Estado != EstadoSessao.Conectado && !_criacao.Aberta);

        var e = Assert.Single(EventosDaCriacao(CriacaoMemoria.EventoInterrompida)).Criacao!;
        Assert.Equal(CriacaoMemoria.MsgCaiu, e.Motivo);
        Assert.Null(_copia.PelaMetade); // sem a sessão, fica para a próxima conexão
        Assert.Equal(CriacaoMemoria.EtapaAjustes, CopiaAntes.DeJson(_guardadoCopia.Texto!)!.Operacao!.Etapa);
        Assert.Empty(EventosDaCriacao(CriacaoMemoria.EventoSalva));

        await Conectar();
        await AndarAte(() => _copia.PelaMetade is not null);
        Assert.Equal(CriacaoMemoria.Tipo, _copia.PelaMetade!.Tipo);

        var terminar = await Rodar(_copia.TerminarAsync());
        Assert.False(terminar.Ok);
        Assert.Equal("O app não sabe refazer esta operação.", terminar.Motivo);
        Assert.NotNull(_copia.PelaMetade);

        var voltar = await Rodar(_copia.VoltarAsync());
        Assert.True(voltar.Ok, voltar.Motivo);
        Assert.Null(_copia.PelaMetade);
        IgualACopia(copia);
        Assert.Single(_setups.Dados.Memorias); // nada salvo
    }

    [Fact]
    public async Task BLE_cai_sem_ajuste_escrito_a_marca_sai_e_nada_fica_pela_metade()
    {
        var chave = await Autorizada();

        _simulador.Derrubar();
        await AndarAte(() => _sessao.Estado != EstadoSessao.Conectado && !_criacao.Aberta && _copia.Ultima?.Operacao is null);

        Assert.Single(EventosDaCriacao(CriacaoMemoria.EventoInterrompida));
        await Conectar();
        await AndarAte(() => !_copia.EmAndamento);
        Assert.Null(_copia.PelaMetade);
        Assert.Null(_copia.Ultima?.Operacao);
    }

    [Fact]
    public async Task Porta_some_por_30_s_depois_de_ajustes_fica_pela_metade_na_hora()
    {
        var chave = await Autorizada();
        Assert.Equal(200, (await Ajuste(chave)).Status);

        _relogio.Avancar(TimeSpan.FromSeconds(29));
        Assert.True(_criacao.Aberta);
        _relogio.Avancar(TimeSpan.FromSeconds(1));

        Assert.False(_uso.IaAtiva);
        Assert.False(_criacao.Aberta);
        Assert.Equal(PortaComandos.MsgPerdeuConexao, _porta.AvisoConexao);
        Assert.Equal(CriacaoMemoria.Tipo, _copia.PelaMetade?.Tipo); // na hora: a sessão está aberta
        Assert.Equal(CriacaoMemoria.MsgTarefaAcabou, Assert.Single(EventosDaCriacao(CriacaoMemoria.EventoInterrompida)).Criacao!.Motivo);
        Assert.Equal(200, (await Pedir("GET", "/estado", chave: chave)).Status);
        Assert.Equal(CriacaoMemoria.MsgTarefaAcabou,
            Assert.Single(await EventosJson(chave, CriacaoMemoria.EventoInterrompida)).GetProperty("motivo").GetString());
        var ajuste = await Rodar(Pedir("POST", "/ajuste", """{"codigo":"081","indice":2,"valor":3.0}""", chave));
        Assert.Equal(409, ajuste.Status); // pela metade: só Voltar à cópia
    }

    [Fact]
    public async Task DELETE_tarefa_no_meio_interrompe_e_sem_ajuste_nada_fica_pela_metade()
    {
        var chave = await Aberta();

        Assert.Equal(200, (await Pedir("DELETE", "/tarefa", null, chave)).Status);

        Assert.False(_criacao.Aberta);
        await AndarAte(() => _copia.Ultima?.Operacao is null);
        Assert.Null(_copia.PelaMetade);
        Assert.Single(EventosDaCriacao(CriacaoMemoria.EventoInterrompida));
    }

    [Fact]
    public async Task A_ajuda_documenta_as_rotas_da_criacao()
    {
        await Conectar();
        await Parear();

        var texto = Json(await Pedir("GET", "/ajuda")).GetProperty("texto").GetString()!;

        foreach (var caminho in new[] { "GET  /criacao", "/criacao/abrir", "/criacao/pergunta", "/criacao/ajuste", "/criacao/resumo",
                     "/criacao/como-ficou", "/criacao/cancelar", "criacao-resposta", "criacao-como-ficou", "criacao-salva",
                     "criacao-cancelada", "criacao-interrompida" })
            Assert.Contains(caminho, texto);
        Assert.DoesNotContain("senha", texto, StringComparison.OrdinalIgnoreCase);
    }

    // ----------------------------------------------------------------- história 10d: o ‹ e o pedido

    [Fact]
    public async Task O_voltar_com_outra_resposta_tira_as_perguntas_seguintes_e_a_porta_le_so_ate_ela()
    {
        var chave = await Aberta();
        Assert.Equal(200, (await Pergunta(chave, ultima: false)).Status);
        Assert.True(_criacao.Responder(0).Ok);
        Assert.Equal(200, (await Pergunta(chave, ultima: false, texto: "Que volume?", respostas: """["Baixo","Médio","Alto"]""")).Status);
        Assert.True(_criacao.Responder(1).Ok);
        Assert.Equal(2, EventosDaCriacao(CriacaoMemoria.EventoResposta).Count);

        // A mesma resposta: nada muda e nada vai à porta.
        Assert.True(_criacao.ResponderDeNovo(1, 0).Ok);
        Assert.Equal(2, _criacao.Perguntas.Count);
        Assert.Equal(2, EventosDaCriacao(CriacaoMemoria.EventoResposta).Count);

        // Outra resposta na 1ª: a 2ª sai e a porta recebe a resposta nº 1 de novo.
        Assert.True(_criacao.ResponderDeNovo(1, 2).Ok);
        var e = EventosDaCriacao(CriacaoMemoria.EventoResposta)[^1].Criacao!;
        Assert.Equal((1, "Jogo", false), (e.Numero, e.Texto, e.DoOutro));
        var criacao = Json(await Pedir("GET", "/criacao", chave: chave));
        var perguntas = criacao.GetProperty("perguntas").EnumerateArray().ToArray();
        var p = Assert.Single(perguntas);
        Assert.Equal(1, p.GetProperty("numero").GetInt32());
        Assert.Equal("Jogo", p.GetProperty("resposta").GetString());
        Assert.Equal("perguntas", criacao.GetProperty("etapa").GetString());
        Assert.Null(_criacao.PerguntaAtual); // a tela espera a próxima

        // A próxima volta a ser a nº 2; o "Outro…" também responde de novo.
        Assert.Equal(200, (await Pergunta(chave, ultima: true, texto: "Onde você senta?")).Status);
        Assert.Equal(2, _criacao.PerguntaAtual!.Numero);
        Assert.True(_criacao.ResponderDeNovo(1, "No pufe").Ok);
        Assert.Single(_criacao.Perguntas);
        Assert.True(_criacao.Perguntas[0].DoOutro);
        Assert.Equal(EtapaCriacao.Perguntas, _criacao.Etapa);
    }

    [Fact]
    public async Task O_voltar_so_vale_numa_pergunta_ja_respondida_e_nas_perguntas()
    {
        var chave = await Aberta();
        Assert.Equal(200, (await Pergunta(chave, ultima: false)).Status);

        Assert.Equal(CriacaoMemoria.MsgSemPergunta, _criacao.ResponderDeNovo(1, 0).Motivo); // a da vez é com Responder
        Assert.Equal(CriacaoMemoria.MsgSemPergunta, _criacao.ResponderDeNovo(5, 0).Motivo);
        Assert.True(_criacao.Responder(0).Ok);
        Assert.Equal(CriacaoMemoria.MsgSemPergunta, _criacao.ResponderDeNovo(1, 9).Motivo);
        Assert.Equal(CriacaoMemoria.MsgOutroVazio, _criacao.ResponderDeNovo(1, "  ").Motivo);

        Assert.Equal(200, (await Pergunta(chave, ultima: true, texto: "Que volume?")).Status);
        Assert.True(_criacao.Responder(0).Ok);
        Assert.Equal(EtapaCriacao.Ajustando, _criacao.Etapa);
        Assert.Equal(CriacaoMemoria.MsgSemPergunta, _criacao.ResponderDeNovo(1, 1).Motivo); // depois da última, não volta
        Assert.Equal(2, _criacao.Perguntas.Count);
    }

    [Fact]
    public async Task Pedir_com_a_criacao_fechada_vai_ao_eventos_como_criacao_pedida_e_aberta_recusa()
    {
        var chave = await Pronto();

        Assert.True(_criacao.Pedir().Ok);

        var pedida = Assert.Single(await EventosJson(chave, CriacaoMemoria.EventoPedida));
        Assert.Equal(CriacaoMemoria.EventoPedida, pedida.GetProperty("tipo").GetString());
        Assert.False(_criacao.Aberta); // a tela não abre: quem abre é a porta

        Assert.Equal(200, (await Pedir("POST", "/criacao/abrir", null, chave)).Status);
        var r = _criacao.Pedir();
        Assert.False(r.Ok);
        Assert.Equal(CriacaoMemoria.MsgJaAberta, r.Motivo);
        Assert.Single(EventosDaCriacao(CriacaoMemoria.EventoPedida));
    }

    [Fact]
    public async Task A_ajuda_explica_o_pedido_e_o_voltar()
    {
        await Conectar();
        await Parear();

        var texto = Json(await Pedir("GET", "/ajuda")).GetProperty("texto").GetString()!;

        Assert.Contains("criacao-pedida", texto);
        Assert.Contains("O ‹ do celular volta a uma pergunta já respondida", texto);
        Assert.Contains("as perguntas depois dela saem", texto);
    }

    // ----------------------------------------------------------------- critério de aceite

    [Fact]
    public async Task Criacao_de_ponta_a_ponta_pela_porta_ate_salvar_MUSICA_ORIG()
    {
        var chave = await Pronto();
        Assert.Equal(200, (await Pedir("POST", "/criacao/abrir", null, chave)).Status);

        Assert.Equal(200, (await Pergunta(chave, ultima: false)).Status);
        Assert.True(_criacao.Responder(0).Ok);
        Assert.Equal(200, (await Pergunta(chave, ultima: true, texto: "Que volume?", respostas: """["Baixo","Médio","Alto"]""")).Status);
        Assert.True(_criacao.Responder(2).Ok);

        Assert.Equal(200, (await Ajuste(chave, codigo: "081", indice: 2, valor: 2.0m)).Status);
        Assert.Equal(200, (await Ajuste(chave, codigo: "081", indice: 0, valor: 8.0m)).Status);
        Assert.Equal(200, (await Ajuste(chave, codigo: "080", indice: null, valor: -3.0m)).Status);
        var resumo = await Pedir("POST", "/criacao/resumo", null, chave);
        Assert.Equal(200, resumo.Status);
        Assert.Equal(3, Json(resumo).GetProperty("resumo").GetArrayLength());
        Assert.Equal(200, (await Pedir("POST", "/criacao/como-ficou", """{"respostas":["Mais grave","Mais claro"]}""", chave)).Status);
        Assert.True(_criacao.AvaliarSom().Ok);
        Assert.True(_criacao.PodeSalvar().Ok);
        var espelho = _estado.Copiar();
        var r = await _criacao.SalvarAsync("MUSICA ORIG");
        Assert.True(r.Ok, r.Motivo);

        var nova = _setups.MemoriaEmUso!;
        Assert.Equal("MUSICA ORIG", nova.Nome);
        Assert.Empty(Camadas.Diferentes(nova.Regulagem, espelho)); // a regulagem relida
        Assert.Contains(nova.Regulagem, v => v.Codigo == "080" && v.NoFio == _simulador.Valor("080"));
        Assert.Null(_copia.Ultima!.Operacao);

        var tipos = Json(await Pedir("GET", "/eventos?desde=0", chave: chave)).GetProperty("eventos").EnumerateArray()
            .Select(e => e.GetProperty("tipo").GetString()!).Where(t => t.StartsWith("criacao-", StringComparison.Ordinal)).ToArray();
        Assert.Equal([CriacaoMemoria.EventoResposta, CriacaoMemoria.EventoResposta, CriacaoMemoria.EventoComoFicou, CriacaoMemoria.EventoSalva], tipos);
        var comoFicou = Assert.Single(await EventosJson(chave, CriacaoMemoria.EventoComoFicou));
        Assert.True(comoFicou.GetProperty("salvar").GetBoolean());
        Assert.Equal(1, comoFicou.GetProperty("rodada").GetInt32());
        Assert.Equal(CriacaoMemoria.OtimoPodeSalvar, comoFicou.GetProperty("texto").GetString());
        Assert.Equal("MUSICA ORIG", Assert.Single(await EventosJson(chave, CriacaoMemoria.EventoSalva)).GetProperty("memoria").GetString());
    }
}
