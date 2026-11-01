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
using BgsControle.Nucleo.Rendimento;
using BgsControle.Nucleo.Seguranca;
using BgsControle.Nucleo.Setups;
using BgsControle.Nucleo.Uso;
using BgsControle.Simulador;

namespace BgsControle.Nucleo.Testes;

/// <summary>
/// História 13b, a saída sem calibração pela porta: o Casa 2.1 da fixture com o ganho do módulo só digitado. A
/// porta recusa o ganho, o limitador, o EQ e o MASTER sem calibração (409, nada enviado), no avulso e no lote; a
/// tela passa; a volta nunca é recusada. Uma linha da matriz por teste.
/// </summary>
public sealed class SemCalibracaoTestes : IDisposable
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

    private const string IdAlta = "memoria-alta";

    private readonly RelogioFalso _relogio = new();
    private readonly ProcessadorSimulado _simulador;
    private readonly Chaves _chaves;
    private readonly LogBle _log = new();
    private readonly Historico _historico;
    private readonly UsoCompartilhado _uso;
    private readonly Sessao _sessao;
    private readonly LeituraAutomatica _leitura;
    private readonly EstadoAparelho _estado;
    private readonly Escritor _escritor;
    private readonly CopiaAntes _copia;
    private readonly GerenteSetups _setups;
    private readonly GeradorSinal _gerador;
    private readonly Pareamento _pareamento;
    private readonly PedidosConfirmacao _confirmacoes;
    private readonly CriacaoMemoria _criacao;
    private readonly PortaComandos _porta;

    public SemCalibracaoTestes()
    {
        _simulador = new ProcessadorSimulado(_relogio) { AtrasoSenha = TimeSpan.Zero, AtrasoLeitura = TimeSpan.Zero };
        _chaves = new Chaves(new RepositorioMemoria());
        _historico = new Historico(new TextoMemoria(), _relogio, _log);
        _uso = new UsoCompartilhado(_relogio, _log);
        _sessao = new Sessao(_simulador, new CofreFalso { Senha = "AB12" }, _log, Tempos, _chaves, _historico, _uso);
        var leitor = new LeitorCompleto(_sessao, _log, LeituraRapida);
        _leitura = new LeituraAutomatica(_sessao, leitor);
        _estado = new EstadoAparelho(_sessao, _leitura);
        _escritor = new Escritor(_sessao, _estado, _log, Escrita, _relogio);
        _copia = new CopiaAntes(_sessao, _estado, _escritor, _leitura, new TextoMemoria(), _log, _relogio);
        var link = new LinkCanais(_sessao, _estado, _escritor, new TextoMemoria(), _log);
        var presets = new GerentePresets(_sessao, _estado, _escritor, _leitura, _copia, new TextoMemoria(CenarioFilme.PresetsJson), _log, _relogio);
        var playlists = new GerentePlaylists(new TextoMemoria(), _log);
        _setups = new GerenteSetups(_sessao, _estado, _escritor, _leitura, _copia, link, presets, playlists, _chaves,
            new TextoMemoria(GerenteSetups.ParaJson(Dados())), new TextoMemoria(), _log, _relogio);
        _gerador = new GeradorSinal(_sessao, _estado, _leitura, _copia, _log, Escrita, TemposDoGerador, _relogio);
        var monitor = new MonitorAparelho(_sessao, _leitura, _relogio);
        var comandos = new ComandosAparelho(_sessao, _estado, _leitura, _copia, new CofreFalso { Senha = "AB12" }, _log, _gerador, Escrita, _relogio);
        _pareamento = new Pareamento(new TextoMemoria(), _relogio, _log);
        _confirmacoes = new PedidosConfirmacao(_relogio, _log);
        _criacao = new CriacaoMemoria(_sessao, _estado, _escritor, _copia, _setups, _uso, _log);
        _porta = new PortaComandos(_sessao, _chaves, _uso, _historico, _pareamento, _confirmacoes, new RedeFalsa(), _estado, _escritor,
            _setups, _gerador, comandos, monitor, _log, _relogio, 0, _criacao);
    }

    /// <summary>O Casa 2.1 sem calibração, com a FILME (ativa) e a ALTA (o limitador da JBL L em −20 dB).</summary>
    private static DadosSetups Dados()
    {
        var alta = CenarioFilme.Memoria with
        {
            Id = IdAlta,
            Nome = "ALTA",
            Regulagem = CenarioFilme.Regulagem
                .Select(v => v is { Codigo: "071", Indice: 0 } ? v with { NoFio = "-20.0" } : v)
                .ToArray(),
        };
        return CenarioFilme.Dados with
        {
            Setups = [CasaDoDenis.SetupCasa with { Equipamentos = CasaDoDenis.EquipamentosSemCalibracao }],
            Memorias = [CenarioFilme.Memoria, alta],
            Uso = new Dictionary<string, UsoDoSetup>
            {
                [CasaDoDenis.IdSetupCasa] = new([CenarioFilme.IdMemoria], [CenarioFilme.IdMemoria, IdAlta, null]),
            },
        };
    }

    public void Dispose()
    {
        _porta.Dispose();
        _criacao.Dispose();
        Assert.DoesNotContain(_simulador.Escritas, e => e.StartsWith("*24", StringComparison.Ordinal) || e.StartsWith("*23", StringComparison.Ordinal));
        Assert.All(_simulador.Escritas, e =>
            Assert.True(Senha.IsMatch(e) || Catalogo.EhLeitura(e) || Catalogo.EscritaDeAudio(e) is not null
                        || Catalogo.ComandoDeGerador(e) is not null, e));
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

    private async Task<string> Pronto()
    {
        await Conectar();
        Assert.Equal(CasaDoDenis.IdSetupCasa, _setups.SetupEmUso?.Id);
        return await Parear();
    }

    private Task<RespostaPorta> Pedir(string metodo, string alvo, string? corpo = null, string? chave = null) =>
        _porta.AtenderAsync(PedidoPorta.De(metodo, alvo, Casa, corpo, chave));

    private Task<RespostaPorta> Ajuste(string chave, string codigo, int? indice, decimal valor)
    {
        var campoIndice = indice is { } i ? $",\"indice\":{i}" : "";
        return Rodar(Pedir("POST", "/ajuste",
            $$"""{"codigo":"{{codigo}}"{{campoIndice}},"valor":{{valor.ToString(CultureInfo.InvariantCulture)}}}""", chave));
    }

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

    private List<string> AudioDesde(int inicio) =>
        _simulador.Escritas.Skip(inicio).Where(e => Catalogo.EscritaDeAudio(e) is not null).ToList();

    /// <summary>Grava o ganho do módulo como medido (o canal ou a ponte).</summary>
    private async Task Calibrar(bool ponte, decimal ganho)
    {
        var r = await Rodar(_setups.GravarGanhoMedidoAsync(CasaDoDenis.IdSetupCasa, CasaDoDenis.IdModuloCasa, ponte,
            new DadoTecnico(ganho, OrigemDado.Medido, "Calibração guiada em 31/10/2026")));
        Assert.True(r.Ok, r.Motivo);
    }

    // ----------------------------------------------------------------- a matriz

    [Fact]
    public async Task Porta_sem_calibracao_recusa_o_081_da_JBL_L_com_409_e_nada_sai()
    {
        var chave = await Pronto();
        var inicio = _simulador.Escritas.Count;

        var r = await Ajuste(chave, "081", 0, 2.0m);

        Assert.Equal(409, r.Status);
        Assert.Equal("Sem calibração: JBL L. O app não sabe a tensão.", Motivo(r));
        Assert.Empty(AudioDesde(inicio));
        Assert.Equal("+10.0", _simulador.Valor("081", 0));
    }

    [Theory]
    [InlineData("070", 1, 1)]
    [InlineData("071", 0, -20)]
    [InlineData("072", 0, 10)]
    [InlineData("073", 0, 100)]
    [InlineData("090", 0, 1000)]
    [InlineData("091", 1, 1)]
    [InlineData("092", 0, 1)]
    public async Task Porta_sem_calibracao_recusa_o_limitador_e_o_EQ_de_saida_dos_canais(string codigo, int canal, int valor)
    {
        var chave = await Pronto();
        var inicio = _simulador.Escritas.Count;

        var r = await Ajuste(chave, codigo, canal, valor);

        Assert.Equal(409, r.Status);
        Assert.StartsWith("Sem calibração: JBL ", Motivo(r));
        Assert.Empty(AudioDesde(inicio));
    }

    [Fact]
    public async Task Porta_passa_o_que_nao_mexe_no_nivel()
    {
        var chave = await Pronto();

        var atraso = await Ajuste(chave, "050", 0, 10m);

        Assert.Equal(200, atraso.Status);
        Assert.Equal("10.0", _simulador.Valor("050", 0));
    }

    [Fact]
    public async Task Porta_global_recusa_o_MASTER_o_EQ_de_entrada_e_as_bandas_com_o_Sub_sem_calibracao()
    {
        await Conectar();
        await Calibrar(ponte: false, 19.5m); // a JBL L e a JBL R calibradas; o Sub, na ponte, não
        var chave = await Parear();
        var inicio = _simulador.Escritas.Count;

        var jbl = await Ajuste(chave, "081", 0, 9.0m);
        Assert.Equal(200, jbl.Status);

        foreach (var (codigo, indice, valor) in new (string, int?, decimal)[] { ("080", null, -1m), ("030", null, 1m), ("031", null, 100m), ("032", null, 1m), ("020", 3, 1m) })
        {
            var r = await Ajuste(chave, codigo, indice, valor);
            Assert.Equal(409, r.Status);
            Assert.Equal("Sem calibração: Sub. O app não sabe a tensão.", Motivo(r));
        }
        // O preset de EQ (reescreve as 15 bandas) e a rota (a+b soma até +6 dB) também.
        var preset = await Ajuste(chave, "220", null, 1m);
        Assert.Equal(409, preset.Status);
        Assert.Equal("Sem calibração: Sub. O app não sabe a tensão.", Motivo(preset));
        var rota = await Rodar(Pedir("POST", "/ajuste", """{"codigo":"010","indice":0,"valor":"a+b"}""", chave));
        Assert.Equal(409, rota.Status);
        Assert.Equal("Sem calibração: Sub. O app não sabe a tensão.", Motivo(rota));
        var sub = await Ajuste(chave, "081", 2, 5.0m);
        Assert.Equal(409, sub.Status);
        Assert.Equal("Sem calibração: Sub. O app não sabe a tensão.", Motivo(sub));
        Assert.Equal(["*081*0*9.0*"], AudioDesde(inicio));
    }

    [Fact]
    public async Task Calibrado_a_porta_passa_o_MASTER()
    {
        await Conectar();
        await Calibrar(ponte: false, 19.5m);
        await Calibrar(ponte: true, 40.5m);
        var chave = await Parear();

        var r = await Ajuste(chave, "080", null, -1m);

        Assert.Equal(200, r.Status);
    }

    [Fact]
    public async Task Porta_sem_Setup_em_uso_recusa_o_081()
    {
        _simulador.Definir("050", 2, "0.0"); // o atraso do Sub diferente da FILME: dúvida
        await Conectar();
        await AndarAte(() => _setups.Estado == EstadoConferencia.Duvida && !_setups.Ocupado);
        Assert.True((await Rodar(_setups.SeguirSemSetupAsync())).Ok);
        Assert.Null(_setups.SetupEmUso);
        var chave = await Parear();
        var inicio = _simulador.Escritas.Count;

        var r = await Ajuste(chave, "081", 0, 2.0m);

        Assert.Equal(409, r.Status);
        Assert.Equal("Sem Setup em uso: o app não sabe a tensão das saídas.", Motivo(r));
        Assert.Empty(AudioDesde(inicio));
    }

    [Fact]
    public async Task Pela_tela_passa_sem_calibracao()
    {
        await Conectar();

        var r = await Rodar(_escritor.EscreverAsync(Catalogo.ParametroDe("081")!, 0, 2.0m));

        Assert.True(r.Ok, r.Motivo);
        Assert.Equal(2.0m, CamadasValor(_simulador.Valor("081", 0)!));
        Assert.True(SemCalibracao.Sem(_setups.SetupEmUso!.Equipamentos, _setups.SaidasEmUso[0]));
    }

    [Fact]
    public async Task Aplicar_pela_porta_uma_memoria_que_mexe_no_limitador_sem_calibracao_da_409_e_nada_sai()
    {
        var chave = await Pronto();
        var inicio = _simulador.Escritas.Count;

        var aplicar = Pedir("POST", "/memoria/aplicar", """{"memoria":"ALTA"}""", chave);
        await EsperarSemAndar(() => _confirmacoes.Atual is not null);
        _confirmacoes.Confirmar();
        var r = await Rodar(aplicar);

        Assert.Equal(409, r.Status);
        Assert.Equal("Sem calibração: JBL L. O app não sabe a tensão.", Motivo(r));
        Assert.Empty(AudioDesde(inicio));
        Assert.Null(_copia.Ultima?.Operacao);
        Assert.Null(_copia.PelaMetade);

        // Pela tela, o mesmo aplicar passa.
        var tela = await Rodar(_setups.AplicarAsync(IdAlta));
        Assert.True(tela.Ok, tela.Motivo);
        Assert.Equal("-20.0", _simulador.Valor("071", 0));
    }

    [Fact]
    public async Task Criacao_pela_porta_recusa_o_ajuste_sem_calibracao_sem_anotar_a_etapa()
    {
        var chave = await Pronto();
        Assert.Equal(200, (await Pedir("POST", "/criacao/abrir", null, chave)).Status);
        Assert.Equal(200, (await Pedir("POST", "/criacao/pergunta", """{"texto":"Para quê?","respostas":["Música","Filme","Jogo"],"ultima":true}""", chave)).Status);
        Assert.True(_criacao.Responder(0).Ok);
        var inicio = _simulador.Escritas.Count;

        var r = await Rodar(Pedir("POST", "/criacao/ajuste",
            """{"codigo":"081","indice":0,"valor":2.0,"frase":"Mais corpo","motivo":"Pedido."}""", chave));

        Assert.Equal(409, r.Status);
        Assert.Equal("Sem calibração: JBL L. O app não sabe a tensão.", Motivo(r));
        Assert.Empty(AudioDesde(inicio));
        Assert.Null(_copia.Ultima!.Operacao!.Etapa); // nada saiu: a etapa dos ajustes não foi anotada
    }

    [Fact]
    public async Task Volta_pela_porta_cancelar_a_criacao_com_saida_sem_calibracao_volta()
    {
        await Conectar();
        await Calibrar(ponte: false, 19.5m);
        await Calibrar(ponte: true, 40.5m);
        var chave = await Parear();
        Assert.Equal(200, (await Pedir("POST", "/criacao/abrir", null, chave)).Status);
        Assert.Equal(200, (await Pedir("POST", "/criacao/pergunta", """{"texto":"Para quê?","respostas":["Música","Filme","Jogo"],"ultima":true}""", chave)).Status);
        Assert.True(_criacao.Responder(0).Ok);
        var ajuste = await Rodar(Pedir("POST", "/criacao/ajuste",
            """{"codigo":"081","indice":0,"valor":2.0,"frase":"Mais corpo","motivo":"Pedido."}""", chave));
        Assert.Equal(200, ajuste.Status);
        Assert.Equal(2.0m, CamadasValor(_simulador.Valor("081", 0)!));

        // O ganho do módulo deixa de ser medido no meio da criação: a saída fica sem calibração.
        var modulo = _setups.SetupEmUso!.Equipamentos.First(e => e.Id == CasaDoDenis.IdModuloCasa);
        Assert.True((await Rodar(_setups.EditarEquipamentoAsync(CasaDoDenis.IdSetupCasa,
            modulo with { Dados = modulo.Dados with { Ganho = new DadoTecnico(19.5m, OrigemDado.Digitado) } }))).Ok);
        Assert.Equal(409, (await Ajuste(chave, "080", null, -1m)).Status);

        var r = await Rodar(Pedir("POST", "/criacao/cancelar", null, chave));

        Assert.Equal(200, r.Status);
        Assert.Equal(10.0m, CamadasValor(_simulador.Valor("081", 0)!));
        Assert.Null(_copia.Ultima!.Operacao);
        Assert.Null(_copia.PelaMetade);
    }

    [Fact]
    public async Task O_desfazer_como_IA_nunca_e_recusado_pela_regra()
    {
        await Conectar();
        Assert.True((await _copia.IniciarAsync(new OperacaoPendente(CriacaoMemoria.Tipo, CasaDoDenis.IdSetupCasa, "teste"))).Ok);
        using (Origem.Como(OrigemComando.App))
        {
            Assert.True((await Rodar(_escritor.EscreverAsync(Catalogo.ParametroDe("081")!, 0, new Valor { NoFio = "2.0", Numero = 2.0m, Exibicao = "" },
                espelhar: false, daCopia: true))).Ok);
        }

        ResultadoCopia r;
        using (Origem.Como(OrigemComando.Ia))
            r = await Rodar(_copia.DesfazerAsync());

        Assert.True(r.Ok, r.Motivo);
        Assert.Equal(10.0m, CamadasValor(_simulador.Valor("081", 0)!));
    }

    [Fact]
    public async Task O_estado_traz_o_nivel_do_Bluetooth_do_Setup()
    {
        var chave = await Pronto();
        var antes = Json(await Pedir("GET", "/estado", chave: chave)).GetProperty("setup").GetProperty("nivelBluetooth");
        Assert.Equal(JsonValueKind.Null, antes.ValueKind);

        Assert.True((await Rodar(_setups.DefinirNivelBluetoothAsync(CasaDoDenis.IdSetupCasa,
            new DadoTecnico(-13.0m, OrigemDado.Medido, "Calibração guiada em 31/10/2026")))).Ok);
        var nivel = Json(await Pedir("GET", "/estado", chave: chave)).GetProperty("setup").GetProperty("nivelBluetooth");

        Assert.Equal(-13.0m, nivel.GetProperty("valor").GetDecimal());
        Assert.Equal("medido", nivel.GetProperty("origem").GetString());
        Assert.Equal("Calibração guiada em 31/10/2026", nivel.GetProperty("fonte").GetString());
    }

    [Fact]
    public void A_ajuda_documenta_a_regra()
    {
        Assert.Contains("Sem calibração", PortaComandos.TextoAjuda);
        Assert.Contains("nivelBluetooth", PortaComandos.TextoAjuda);
    }

    private static decimal CamadasValor(string noFio) => decimal.Parse(noFio.TrimStart('+'), CultureInfo.InvariantCulture);
}
