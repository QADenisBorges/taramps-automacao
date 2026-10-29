using System.Text.RegularExpressions;
using BgsControle.Nucleo.Enlace;
using BgsControle.Nucleo.Escrita;
using BgsControle.Nucleo.Gerador;
using BgsControle.Nucleo.Leitura;
using BgsControle.Nucleo.Memorias;
using BgsControle.Nucleo.Registro;
using BgsControle.Nucleo.Seguranca;
using BgsControle.Nucleo.Uso;
using BgsControle.Simulador;

namespace BgsControle.Nucleo.Testes;

/// <summary>
/// A história 9a no núcleo, contra o processador simulado: a origem ambiente que atravessa a fila
/// do Escritor, o histórico na porta (com as recusas, o App e o guardado) e o uso compartilhado
/// (travado, pausa, endereço tocado, PARAR IA). Uma linha da matriz de
/// <c>9a-historico-estado-ao-vivo-uso-simultaneo.md</c> por teste.
/// </summary>
public sealed class UsoTestes : IDisposable
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

    private static Parametro Ganho => Catalogo.ParametroDe("081")!;

    private readonly RelogioFalso _relogio = new();
    private readonly ProcessadorSimulado _simulador;
    private readonly TransporteSegurado _transporte;
    private readonly RepositorioMemoria _repositorioChaves = new();
    private readonly TextoMemoria _arquivoHistorico;
    private readonly TextoMemoria _arquivoUso;
    private readonly Chaves _chaves;
    private readonly LogBle _log = new();
    private readonly Historico _historico;
    private readonly UsoCompartilhado _uso;
    private readonly Sessao _sessao;
    private readonly LeituraAutomatica _leitura;
    private readonly EstadoAparelho _estado;
    private readonly Escritor _escritor;
    private readonly CopiaAntes _copia;
    private readonly GeradorSinal _gerador;
    private int _pausou;
    private int _retomou;

    public UsoTestes() : this(null, null)
    {
    }

    private UsoTestes(string? historicoGuardado, string? usoGuardado)
    {
        _simulador = new ProcessadorSimulado(_relogio) { AtrasoSenha = TimeSpan.Zero, AtrasoLeitura = TimeSpan.Zero };
        _arquivoHistorico = new TextoMemoria(historicoGuardado);
        _arquivoUso = new TextoMemoria(usoGuardado);
        _chaves = new Chaves(_repositorioChaves);
        _historico = new Historico(_arquivoHistorico, _relogio, _log);
        _uso = new UsoCompartilhado(_arquivoUso, _relogio, _log);
        _uso.Pausou += () => Interlocked.Increment(ref _pausou);
        _uso.Retomou += () => Interlocked.Increment(ref _retomou);
        _transporte = new TransporteSegurado(_simulador);
        _sessao = new Sessao(_transporte, new CofreFalso { Senha = "AB12" }, _log, Tempos, _chaves, _historico, _uso);
        var leitor = new LeitorCompleto(_sessao, _log, LeituraRapida);
        _leitura = new LeituraAutomatica(_sessao, leitor);
        _estado = new EstadoAparelho(_sessao, _leitura);
        _escritor = new Escritor(_sessao, _estado, _log, Escrita, _relogio);
        _copia = new CopiaAntes(_sessao, _estado, _escritor, _leitura, new TextoMemoria(), _log, _relogio);
        var memorias = new GerenteMemorias(_sessao, _estado, _leitura, _copia, _log, relogio: _relogio);
        _gerador = new GeradorSinal(_sessao, _estado, _leitura, _copia, _log, memorias, Escrita, TemposDoGerador, _relogio);
    }

    /// <summary>Nada além da senha, das leituras e dos comandos do catálogo chega ao simulador; a senha nunca vai ao histórico.</summary>
    public void Dispose()
    {
        Assert.All(_simulador.Escritas, e =>
            Assert.True(Senha.IsMatch(e) || Catalogo.EhLeitura(e) || Catalogo.EscritaDeAudio(e) is not null
                        || Catalogo.ComandoDeGerador(e) is not null || Catalogo.ComandoDeAparelho(e) is not null, e));
        Assert.DoesNotContain("AB12", Historico.ParaJson(_historico.Entradas));
    }

    // ----------------------------------------------------------------- apoio

    private async Task Conectar()
    {
        await _chaves.Carregada;
        await _copia.Carregada;
        await _historico.Carregado;
        await _uso.Carregado;
        await _sessao.ConectarAsync();
        Assert.Equal(EstadoSessao.Conectado, _sessao.Estado);
        await AndarAte(() => _estado.Pronto && !_estado.Lendo && !_leitura.Lendo && !_copia.EmAndamento
                             && _gerador.Estado == EstadoGerador.Desligado);
    }

    private async Task AndarAte(Func<bool> condicao, int passos = 3000)
    {
        for (var i = 0; i < passos && !condicao(); i++)
        {
            _relogio.Avancar(TimeSpan.FromMilliseconds(10));
            await Task.Delay(1);
        }
        Assert.True(condicao(), "a condição não ficou verdadeira");
    }

    /// <summary>Espera em tempo real, com o relógio falso parado (o eco não chega).</summary>
    private static async Task EsperarSemAndar(Func<bool> condicao)
    {
        for (var i = 0; i < 500 && !condicao(); i++)
            await Task.Delay(10);
        Assert.True(condicao(), "a condição não ficou verdadeira");
    }

    /// <summary>Anda o relógio sem nada no ar (entre as escritas), em passos de 100 ms.</summary>
    private async Task Andar(TimeSpan quanto)
    {
        var passo = TimeSpan.FromMilliseconds(100);
        for (var t = TimeSpan.Zero; t < quanto; t += passo)
        {
            _relogio.Avancar(passo);
            await Task.Delay(1);
        }
    }

    private async Task Rodar(Task tarefa)
    {
        await AndarAte(() => tarefa.IsCompleted);
        await tarefa;
    }

    private async Task<T> Rodar<T>(Task<T> tarefa)
    {
        await AndarAte(() => tarefa.IsCompleted);
        return await tarefa;
    }

    private Task<ResultadoEscrita> Escrever(int canal, decimal valor, OrigemComando origem = OrigemComando.Tela)
    {
        using (Origem.Como(origem))
            return _escritor.EscreverAsync(Ganho, canal, valor);
    }

    private int EscritasDe(string quadro) => _simulador.Escritas.Count(e => e == quadro);

    private EntradaHistorico Ultima() => _historico.Entradas[0];

    private static ComandoNaPorta Comando(int canal, string valor) =>
        new(TipoComando.AjusteAudio, new Endereco("081", canal), valor);

    /// <summary>Entra no modo "O toque pausa a IA" e deixa a IA ativa com uma tarefa.</summary>
    private async Task IaAtivaPausando()
    {
        Assert.True((await _uso.DefinirModoAsync(ModoToque.PausarIa, OrigemComando.Tela)).Ok);
        Assert.True(_uso.IniciarTarefa("IA ajustando o ganho").Ok);
        Assert.Equal(EstadoIa.Ativa, _uso.Estado);
    }

    // ----------------------------------------------------------------- origem

    [Fact]
    public async Task A_origem_padrao_e_a_tela_e_o_escopo_vale_so_dentro_do_using()
    {
        Assert.Equal(OrigemComando.Tela, Origem.Atual);
        using (Origem.Como(OrigemComando.Ia))
        {
            Assert.Equal(OrigemComando.Ia, Origem.Atual);
            using (Origem.Como(OrigemComando.App))
                Assert.Equal(OrigemComando.App, Origem.Atual);
            Assert.Equal(OrigemComando.Ia, Origem.Atual);
            await Task.Run(() => Assert.Equal(OrigemComando.Ia, Origem.Atual)); // segue o fluxo
        }
        Assert.Equal(OrigemComando.Tela, Origem.Atual);
    }

    [Fact]
    public async Task Pedidos_da_IA_e_da_tela_juntados_pelo_Escritor_o_historico_mostra_a_origem_do_que_saiu()
    {
        await Conectar();
        // Um pedido do app ocupa a fila do ganho 3; o da tela toma o lugar do pendente da IA.
        var primeiro = Escrever(2, 1.0m, OrigemComando.App);
        await EsperarSemAndar(() => EscritasDe("*081*2*1.0*") == 1);
        var daIa = Escrever(2, 2.0m, OrigemComando.Ia);
        var daTela = Escrever(2, 3.0m, OrigemComando.Tela);

        // A IA termina já (sem andar o relógio), com a sua recusa; a tela, com a escrita que saiu.
        var ia = await daIa.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(ia.Ok);
        Assert.Equal(UsoCompartilhado.MsgMexendo, ia.Motivo);
        await Rodar(primeiro);
        var r = await Rodar(daTela);

        Assert.True(r.Ok, r.Motivo);
        Assert.Equal(0, EscritasDe("*081*2*2.0*"));
        Assert.Equal(1, EscritasDe("*081*2*3.0*"));
        var ganhos = _historico.Entradas.Where(e => e.Endereco == new Endereco("081", 2)).ToList();
        Assert.Equal(2, ganhos.Count);
        Assert.Equal((OrigemComando.Tela, "3.0"), (ganhos[0].Origem, ganhos[0].Valor));
        Assert.Equal((OrigemComando.App, "1.0"), (ganhos[1].Origem, ganhos[1].Valor));
        Assert.DoesNotContain(_historico.Entradas, e => e.Origem == OrigemComando.Ia);
        Assert.Equal(EstadoIa.Inativa, _uso.Estado); // o pedido da IA nunca chegou à porta
    }

    [Fact]
    public async Task Pedido_da_IA_sobre_um_pendente_da_tela_e_recusado_e_o_da_tela_sai_como_Tela()
    {
        await Conectar();
        var primeiro = Escrever(1, 1.0m, OrigemComando.App);
        await EsperarSemAndar(() => EscritasDe("*081*1*1.0*") == 1);
        var daTela = Escrever(1, 2.0m, OrigemComando.Tela);
        var daIa = Escrever(1, 3.0m, OrigemComando.Ia);

        Assert.True(daIa.IsCompleted); // recusado na hora, sem esperar a fila
        var ia = await daIa;
        Assert.False(ia.Ok);
        Assert.Equal(UsoCompartilhado.MsgMexendo, ia.Motivo);
        await Rodar(primeiro);
        var r = await Rodar(daTela);

        Assert.True(r.Ok, r.Motivo);
        Assert.Equal(0, EscritasDe("*081*1*3.0*"));
        Assert.Equal(1, EscritasDe("*081*1*2.0*"));
        Assert.Equal((OrigemComando.Tela, "2.0", true), (Ultima().Origem, Ultima().Valor, Ultima().Enviado));
        Assert.DoesNotContain(_historico.Entradas, e => e.Origem == OrigemComando.Ia); // nunca chegou à porta
        Assert.Equal(EstadoIa.Inativa, _uso.Estado);
    }

    [Fact]
    public async Task Com_o_link_ligado_a_copia_leva_a_origem_do_pedido_que_saiu()
    {
        var link = new LinkCanais(_sessao, _estado, _escritor, new TextoMemoria(), _log);
        await link.Carregada;
        await Conectar();
        Assert.Null(await link.DefinirEstadoAsync(true, false)); // o link sai das ligações: não copia nada
        await Andar(TimeSpan.FromSeconds(11));

        // A fila do ganho 1 começa com um pedido do app; o da IA sai depois, com a cópia na saída 2.
        var primeiro = Escrever(0, 1.0m, OrigemComando.App);
        await EsperarSemAndar(() => EscritasDe("*081*0*1.0*") == 1);
        var daIa = Escrever(0, 3.0m, OrigemComando.Ia);
        await Rodar(primeiro);
        var r = await Rodar(daIa);

        Assert.True(r.Ok, r.Motivo);
        Assert.Equal(1, EscritasDe("*081*1*3.0*"));
        var copia = _historico.Entradas.First(e => e.Endereco == new Endereco("081", 1) && e.Valor == "3.0");
        Assert.Equal(OrigemComando.Ia, copia.Origem);
        Assert.Equal(OrigemComando.App, _historico.Entradas.First(e => e.Endereco == new Endereco("081", 1) && e.Valor == "1.0").Origem);
    }

    // ----------------------------------------------------------------- matriz: histórico

    [Fact]
    public async Task Comando_da_tela_sai_e_entra_com_a_hora_a_origem_o_tipo_o_endereco_e_o_valor()
    {
        await Conectar();
        var antes = _relogio.GetLocalNow();

        var r = await Rodar(Escrever(2, 2.0m));

        Assert.True(r.Ok, r.Motivo);
        Assert.Contains("*081*2*2.0*", _simulador.Escritas);
        var e = Assert.Single(_historico.Entradas);
        Assert.Equal(OrigemComando.Tela, e.Origem);
        Assert.Equal(TipoComando.AjusteAudio, e.Tipo);
        Assert.Equal(new Endereco("081", 2), e.Endereco);
        Assert.Equal("2.0", e.Valor);
        Assert.True(e.Enviado);
        Assert.Null(e.Motivo);
        Assert.InRange(e.Hora, antes, _relogio.GetLocalNow());
    }

    [Fact]
    public async Task As_leituras_e_o_quadro_da_senha_nunca_entram_no_historico()
    {
        await Conectar(); // a senha e as 100 leituras passaram pela porta

        Assert.Empty(_historico.Entradas);
        Assert.Null(ComandoNaPorta.Ler("*000*AB12*"));
        Assert.Null(ComandoNaPorta.Ler("*000*ab*"));
        Assert.Null(ComandoNaPorta.Ler("#081#2#"));
    }

    [Fact]
    public async Task Recusa_pela_chave_entra_recusada_com_o_motivo_da_chave()
    {
        await Conectar();
        Assert.True((await _chaves.DefinirAsync(Chave.AjustesAudio, false, OrigemComando.Tela)).Ok);

        var r = await Rodar(Escrever(2, 2.0m));

        Assert.False(r.Ok);
        Assert.Equal(Sessao.MotivoChaveDesligada, r.Motivo);
        Assert.Equal(0, EscritasDe("*081*2*2.0*"));
        var e = Assert.Single(_historico.Entradas);
        Assert.False(e.Enviado);
        Assert.Equal(Sessao.MotivoChaveDesligada, e.Motivo);
    }

    [Fact]
    public async Task A_chave_e_conferida_antes_do_uso_a_IA_recusada_pela_chave_nao_fica_ativa()
    {
        await Conectar();
        Assert.True((await _chaves.DefinirAsync(Chave.AjustesAudio, false, OrigemComando.Tela)).Ok);

        var r = await Rodar(Escrever(2, 2.0m, OrigemComando.Ia));

        Assert.Equal(Sessao.MotivoChaveDesligada, r.Motivo);
        Assert.Equal(EstadoIa.Inativa, _uso.Estado);
    }

    [Fact]
    public async Task Troca_de_senha_pela_tela_entra_como_Troca_de_senha_sem_as_senhas()
    {
        await Conectar();
        Assert.True((await _chaves.DefinirAsync(Chave.TrocaSenha, true, OrigemComando.Tela)).Ok);

        var envio = await _sessao.EnviarAsync(Catalogo.TrocarSenha("AB12", "CD34"));

        Assert.True(envio.Enviado, envio.Motivo);
        var e = Assert.Single(_historico.Entradas);
        Assert.Equal(TipoComando.TrocaSenha, e.Tipo);
        Assert.Null(e.Valor);
        var json = Historico.ParaJson(_historico.Entradas);
        Assert.DoesNotContain("AB12", json);
        Assert.DoesNotContain("CD34", json);
    }

    [Fact]
    public async Task Um_texto_fora_do_catalogo_entra_recusado_so_com_o_codigo()
    {
        await Conectar();

        var envio = await _sessao.EnviarAsync("*001*AB12*AB12*"); // a nova igual à antiga: fora do formato

        Assert.False(envio.Enviado);
        var e = Assert.Single(_historico.Entradas);
        Assert.Equal(TipoComando.TrocaSenha, e.Tipo);
        Assert.Null(e.Valor);
        Assert.Equal(Sessao.MotivoAparelhoForaDoFormato, e.Motivo);
    }

    // ----------------------------------------------------------------- matriz: uso compartilhado

    [Fact]
    public async Task Travado_IA_ativa_no_modo_padrao_o_comando_da_tela_nao_sai()
    {
        await Conectar();
        Assert.Equal(ModoToque.TravarAtePararIa, _uso.Modo);
        Assert.True(_uso.IniciarTarefa("IA ajustando o EQ da FILME").Ok);
        Assert.True(_uso.Travado);

        var r = await Rodar(Escrever(2, 2.0m));

        Assert.False(r.Ok);
        Assert.Equal(UsoCompartilhado.MsgTravado, r.Motivo);
        Assert.Equal(0, EscritasDe("*081*2*2.0*"));
        Assert.Equal((OrigemComando.Tela, false, UsoCompartilhado.MsgTravado), (Ultima().Origem, Ultima().Enviado, Ultima().Motivo));
        Assert.Equal(UsoCompartilhado.MsgTravado, _sessao.Conferir(Ganho.CodificarEscrita(2, 2.0m)));
    }

    [Fact]
    public async Task Gerador_no_travado_o_prazo_acaba_e_o_desligar_sai_como_App()
    {
        await Conectar();
        Assert.True((await _chaves.DefinirAsync(Chave.Gerador, true, OrigemComando.Tela)).Ok);
        var tocar = await Rodar(_gerador.TocarTomAsync(1000, -20.0m));
        Assert.True(tocar.Ok, tocar.Mensagem);
        Assert.True(_uso.IniciarTarefa("IA ajustando o EQ da FILME").Ok);
        Assert.True(_uso.Travado);

        _relogio.Avancar(_gerador.Restante!.Value - TimeSpan.FromMilliseconds(200));
        await AndarAte(() => _gerador.Estado == EstadoGerador.Desligado && !_gerador.TalvezLigado);

        Assert.Contains(Catalogo.DesligarGerador, _simulador.Escritas);
        Assert.False(_simulador.GeradorLigado);
        var desligar = _historico.Entradas.First(e => e.Tipo == TipoComando.Gerador && e.Endereco?.Codigo == "202");
        Assert.Equal((OrigemComando.App, "0", true), (desligar.Origem, desligar.Valor, desligar.Enviado));
        Assert.Equal(OrigemComando.Tela, _historico.Entradas.First(e => e.Valor == "1").Origem); // o *202*1* foi da tela
    }

    [Fact]
    public async Task Desligar_o_gerador_nunca_fica_travado_nem_para_a_IA_parada()
    {
        await Conectar();
        Assert.True(_uso.IniciarTarefa("IA trabalhando").Ok);

        Assert.True((await _sessao.EnviarAsync(Catalogo.DesligarGerador)).Enviado); // da tela, no travado
        _uso.PararIa();
        using (Origem.Como(OrigemComando.Ia))
            Assert.True((await _sessao.EnviarAsync(Catalogo.DesligarGerador)).Enviado);
    }

    [Fact]
    public async Task Pausa_IA_ativa_no_modo_opcao_o_comando_da_tela_sai_e_pausa_a_IA()
    {
        await Conectar();
        await IaAtivaPausando();

        var r = await Rodar(Escrever(2, 2.0m));

        Assert.True(r.Ok, r.Motivo);
        Assert.Equal(1, EscritasDe("*081*2*2.0*"));
        Assert.Equal(EstadoIa.Pausada, _uso.Estado);
        Assert.Equal(1, Volatile.Read(ref _pausou));
        Assert.False(_uso.Travado);
    }

    [Fact]
    public async Task IA_na_pausa_o_ajuste_da_IA_nao_sai()
    {
        await Conectar();
        await IaAtivaPausando();
        await Rodar(Escrever(2, 2.0m));

        var r = await Rodar(Escrever(0, -1.0m, OrigemComando.Ia)); // outro endereço

        Assert.False(r.Ok);
        Assert.Equal(UsoCompartilhado.MsgPausada, r.Motivo);
        Assert.Equal(0, EscritasDe("*081*0*-1.0*"));
        Assert.Equal((OrigemComando.Ia, UsoCompartilhado.MsgPausada), (Ultima().Origem, Ultima().Motivo));
    }

    [Fact]
    public async Task Retomar_com_10_s_sem_comandos_da_tela_a_IA_volta_a_ativa()
    {
        await Conectar();
        await IaAtivaPausando();
        await Rodar(Escrever(2, 2.0m));
        Assert.Equal(EstadoIa.Pausada, _uso.Estado);

        await Andar(TimeSpan.FromSeconds(9));
        Assert.Equal(EstadoIa.Pausada, _uso.Estado);
        Assert.Equal(0, Volatile.Read(ref _retomou));
        await Andar(TimeSpan.FromSeconds(1.5));

        Assert.Equal(EstadoIa.Ativa, _uso.Estado);
        Assert.Equal(1, Volatile.Read(ref _retomou));
        var r = await Rodar(Escrever(0, -1.0m, OrigemComando.Ia));
        Assert.True(r.Ok, r.Motivo);
    }

    [Fact]
    public async Task Cada_novo_comando_da_tela_reinicia_os_10_s()
    {
        await Conectar();
        await IaAtivaPausando();
        await Rodar(Escrever(2, 2.0m));

        await Andar(TimeSpan.FromSeconds(7));
        await Rodar(Escrever(1, 1.0m)); // reinicia a janela
        await Andar(TimeSpan.FromSeconds(7));
        Assert.Equal(EstadoIa.Pausada, _uso.Estado);
        Assert.Equal(1, Volatile.Read(ref _pausou)); // pausada uma vez só

        await Andar(TimeSpan.FromSeconds(4));
        Assert.Equal(EstadoIa.Ativa, _uso.Estado);
        Assert.Equal(1, Volatile.Read(ref _retomou));
    }

    [Fact]
    public async Task Endereco_tocado_sem_tarefa_a_IA_mexe_5_s_depois_e_e_recusada()
    {
        await Conectar();
        Assert.True((await Rodar(Escrever(2, 2.0m))).Ok);
        Assert.Equal(EstadoIa.Inativa, _uso.Estado);

        await Andar(TimeSpan.FromSeconds(5));
        var r = await Rodar(Escrever(2, 5.0m, OrigemComando.Ia));

        Assert.False(r.Ok);
        Assert.Equal(UsoCompartilhado.MsgMexendo, r.Motivo);
        Assert.Equal(0, EscritasDe("*081*2*5.0*"));
        Assert.Equal(EstadoIa.Inativa, _uso.Estado);

        // Outro endereço passa; o tocado volta à IA depois de 10 s sem toques nele.
        Assert.True((await Rodar(Escrever(1, 1.0m, OrigemComando.Ia))).Ok);
        await Andar(TimeSpan.FromSeconds(5));
        Assert.True((await Rodar(Escrever(2, 5.0m, OrigemComando.Ia))).Ok);
    }

    [Fact]
    public async Task PARAR_IA_a_IA_fica_inativa_e_um_comando_dela_depois_e_recusado()
    {
        await Conectar();
        Assert.True(_uso.IniciarTarefa("IA ajustando o EQ da FILME").Ok);

        _uso.PararIa();

        Assert.Equal(EstadoIa.Inativa, _uso.Estado);
        Assert.Null(_uso.Tarefa);
        var r = await Rodar(Escrever(2, 2.0m, OrigemComando.Ia));
        Assert.Equal(UsoCompartilhado.MsgParada, r.Motivo);
        Assert.Equal(0, EscritasDe("*081*2*2.0*"));
        Assert.False(_uso.IniciarTarefa("de novo").Ok);
        Assert.True((await Rodar(Escrever(2, 3.0m))).Ok); // a tela destravou

        _uso.Liberar(); // a 9b, num pareamento novo
        await Andar(TimeSpan.FromSeconds(11));
        Assert.True((await Rodar(Escrever(2, 2.0m, OrigemComando.Ia))).Ok);
    }

    [Fact]
    public async Task Ao_vivo_a_mudanca_da_IA_chega_ao_espelho_e_entra_como_IA()
    {
        await Conectar();
        var mudou = new List<Endereco>();
        _estado.Mudou += e =>
        {
            lock (mudou)
                mudou.Add(e);
        };
        var antes = _relogio.GetUtcNow();

        var r = await Rodar(Escrever(2, 2.0m, OrigemComando.Ia));

        Assert.True(r.Ok, r.Motivo);
        Assert.True(_relogio.GetUtcNow() - antes <= TimeSpan.FromSeconds(1));
        Assert.Equal(2.0m, _estado.Valor("081", 2)!.Numero);
        lock (mudou)
            Assert.Contains(new Endereco("081", 2), mudou);
        Assert.Equal((OrigemComando.Ia, true), (Ultima().Origem, Ultima().Enviado));
    }

    // ----------------------------------------------------------------- a IA não muda o modo nem as chaves

    [Fact]
    public async Task A_IA_nunca_muda_o_modo_do_toque_nem_uma_chave()
    {
        await _uso.Carregado;
        await _chaves.Carregada;

        Assert.False((await _uso.DefinirModoAsync(ModoToque.PausarIa, OrigemComando.Ia)).Ok);
        using (Origem.Como(OrigemComando.Ia))
        {
            Assert.Equal(UsoCompartilhado.MsgSoATela, (await _uso.DefinirModoAsync(ModoToque.PausarIa, OrigemComando.Tela)).Motivo);
            Assert.Equal(Chaves.MsgIaNaoMuda, (await _chaves.DefinirAsync(Chave.Gerador, true, OrigemComando.Tela)).Motivo);
        }
        Assert.Equal(Chaves.MsgIaNaoMuda, (await _chaves.DefinirAsync(Chave.Gerador, true, OrigemComando.App)).Motivo);

        Assert.Equal(ModoToque.TravarAtePararIa, _uso.Modo);
        Assert.False(_chaves.Ligada(Chave.Gerador));
        Assert.Null(_arquivoUso.Texto);
    }

    // ----------------------------------------------------------------- guardado

    [Fact]
    public async Task O_modo_do_toque_fica_guardado_e_volta_ao_reabrir()
    {
        await _uso.Carregado;
        Assert.True((await _uso.DefinirModoAsync(ModoToque.PausarIa, OrigemComando.Tela)).Ok);
        Assert.Contains("\"formato\": 1", _arquivoUso.Texto);

        var reaberto = new UsoCompartilhado(new TextoMemoria(_arquivoUso.Texto), _relogio);
        await reaberto.Carregado;

        Assert.Equal(ModoToque.PausarIa, reaberto.Modo);
    }

    [Theory]
    [InlineData("{ isto não é json")]
    [InlineData("{\"formato\": 2, \"modoToque\": \"pausar-ia\"}")]
    [InlineData("{\"formato\": 1, \"modoToque\": \"outro\"}")]
    public async Task Modo_guardado_estragado_vale_o_padrao(string json)
    {
        var uso = new UsoCompartilhado(new TextoMemoria(json), _relogio);
        await uso.Carregado;

        Assert.Equal(ModoToque.TravarAtePararIa, uso.Modo);
    }

    [Fact]
    public async Task Modo_que_nao_foi_guardado_volta_ao_de_antes()
    {
        await _uso.Carregado;
        _arquivoUso.FalharAoGravar = true;

        var r = await _uso.DefinirModoAsync(ModoToque.PausarIa, OrigemComando.Tela);

        Assert.False(r.Ok);
        Assert.Equal(UsoCompartilhado.MsgNaoGuardou, r.Motivo);
        Assert.Equal(ModoToque.TravarAtePararIa, _uso.Modo);
    }

    [Fact]
    public async Task As_gravacoes_do_historico_se_juntam_a_cada_2_s()
    {
        await _historico.Carregado;
        for (var i = 0; i < 5; i++)
            _historico.Registrar(Comando(0, $"{i}.0"), OrigemComando.Tela, true, null);
        Assert.Equal(0, _arquivoHistorico.Gravacoes);

        _relogio.Avancar(TimeSpan.FromSeconds(1.9));
        Assert.Equal(0, _arquivoHistorico.Gravacoes);
        _relogio.Avancar(TimeSpan.FromSeconds(0.2));
        await AndarAte(() => _arquivoHistorico.Gravacoes == 1, passos: 10);

        Assert.Equal(5, Historico.DeJson(_arquivoHistorico.Texto!).Count);
    }

    [Fact]
    public async Task Reabrir_3_entradas_gravadas_voltam_na_ordem()
    {
        await _historico.Carregado;
        _historico.Registrar(Comando(0, "1.0"), OrigemComando.Tela, true, null);
        _relogio.Avancar(TimeSpan.FromMilliseconds(500));
        _historico.Registrar(Comando(1, "2.0"), OrigemComando.Ia, false, UsoCompartilhado.MsgPausada);
        _relogio.Avancar(TimeSpan.FromMilliseconds(500));
        _historico.Registrar(new ComandoNaPorta(TipoComando.Gerador, new Endereco("202", null), "0"), OrigemComando.App, true, null);
        Assert.True(await _historico.GravarAgoraAsync());

        var reaberto = new Historico(new TextoMemoria(_arquivoHistorico.Texto), _relogio);
        await reaberto.Carregado;

        Assert.Equal(_historico.Entradas, reaberto.Entradas);
        Assert.Equal([OrigemComando.App, OrigemComando.Ia, OrigemComando.Tela], reaberto.Entradas.Select(e => e.Origem));
        Assert.Equal(UsoCompartilhado.MsgPausada, reaberto.Entradas[1].Motivo);
    }

    [Fact]
    public async Task Limite_com_1001_entradas_fica_com_as_1000_mais_novas()
    {
        await _historico.Carregado;
        for (var i = 0; i < 1001; i++)
            _historico.Registrar(Comando(0, i.ToString(System.Globalization.CultureInfo.InvariantCulture)), OrigemComando.Tela, true, null);

        Assert.Equal(Historico.Maximo, _historico.Entradas.Count);
        Assert.Equal("1000", _historico.Entradas[0].Valor);
        Assert.Equal("1", _historico.Entradas[^1].Valor);
        Assert.True(await _historico.GravarAgoraAsync());
        Assert.Equal(Historico.Maximo, Historico.DeJson(_arquivoHistorico.Texto!).Count);
    }

    [Theory]
    [InlineData("{ estragado")]
    [InlineData("[1, 2, 3]")]
    [InlineData("{\"formato\": 2, \"entradas\": []}")]
    public async Task Arquivo_estragado_comeca_vazio_e_o_registro_funciona(string json)
    {
        var historico = new Historico(new TextoMemoria(json), _relogio);
        await historico.Carregado;

        Assert.Empty(historico.Entradas);
        historico.Registrar(Comando(2, "2.0"), OrigemComando.Tela, true, null);
        Assert.Single(historico.Entradas);
    }

    [Fact]
    public async Task Gravacao_que_falha_a_lista_continua_e_a_proxima_tenta_de_novo()
    {
        await _historico.Carregado;
        _arquivoHistorico.FalharAoGravar = true;
        _historico.Registrar(Comando(0, "1.0"), OrigemComando.Tela, true, null);
        _relogio.Avancar(TimeSpan.FromSeconds(2));
        await AndarAte(() => _arquivoHistorico.Tentativas == Gravacao.Tentativas, passos: 10);
        Assert.Single(_historico.Entradas);
        Assert.Null(_arquivoHistorico.Texto);

        // Sem entrada nova: a gravação seguinte já ficou agendada.
        _arquivoHistorico.FalharAoGravar = false;
        _relogio.Avancar(Historico.IntervaloGravacao);
        await AndarAte(() => _arquivoHistorico.Gravacoes == 1, passos: 10);
        Assert.Single(Historico.DeJson(_arquivoHistorico.Texto!));

        _historico.Registrar(Comando(1, "2.0"), OrigemComando.Tela, true, null);
        _relogio.Avancar(Historico.IntervaloGravacao);
        await AndarAte(() => _arquivoHistorico.Gravacoes == 2, passos: 10);
        Assert.Equal(2, Historico.DeJson(_arquivoHistorico.Texto!).Count);
    }

    [Fact]
    public async Task Leitura_que_lanca_ao_abrir_o_historico_comeca_vazio()
    {
        var historico = new Historico(new RepositorioQueLanca(), _relogio);
        await historico.Carregado;

        Assert.Empty(historico.Entradas);
    }

    // ----------------------------------------------------------------- 9a: revisão

    [Fact]
    public async Task Uma_leitura_fora_do_catalogo_nunca_entra_no_historico()
    {
        await Conectar();

        var envio = await _sessao.EnviarAsync("#004#");

        Assert.False(envio.Enviado);
        Assert.Equal(Sessao.MotivoForaDoCatalogo, envio.Motivo);
        Assert.Empty(_historico.Entradas);
        Assert.Null(ComandoNaPorta.Ler("020#15"));
    }

    [Fact]
    public async Task TerminarTarefa_deixa_a_IA_inativa_e_destrava_a_tela()
    {
        await Conectar();
        Assert.True(_uso.IniciarTarefa("IA ajustando o EQ da FILME").Ok);
        Assert.True(_uso.Travado);

        _uso.TerminarTarefa();

        Assert.Equal(EstadoIa.Inativa, _uso.Estado);
        Assert.False(_uso.Travado);
        Assert.Null(_uso.Tarefa);
        Assert.True((await Rodar(Escrever(2, 2.0m))).Ok);
    }

    [Fact]
    public async Task Conferir_no_modo_pausa_nao_pausa_nem_marca_o_endereco()
    {
        await Conectar();
        await IaAtivaPausando();

        Assert.Null(_sessao.Conferir(Ganho.CodificarEscrita(2, 2.0m)));

        Assert.Equal(EstadoIa.Ativa, _uso.Estado);
        Assert.Equal(0, Volatile.Read(ref _pausou));
        var r = await Rodar(Escrever(2, 5.0m, OrigemComando.Ia)); // o endereço não ficou tocado
        Assert.True(r.Ok, r.Motivo);
    }

    [Fact]
    public async Task PARAR_IA_com_um_comando_da_IA_na_fila_do_GATT_ele_e_recusado()
    {
        await Conectar();
        _transporte.Segurar("*081*0*1.0*");
        var daTela = _sessao.EnviarAsync("*081*0*1.0*");
        await _transporte.Chegou;
        Task<ResultadoEnvio> daIa;
        using (Origem.Como(OrigemComando.Ia))
            daIa = _sessao.EnviarAsync("*081*1*2.0*");
        await Task.Delay(50);
        Assert.False(daIa.IsCompleted); // esperando o GATT

        _uso.PararIa();
        _transporte.Soltar();

        Assert.True((await daTela).Enviado);
        var r = await daIa;
        Assert.False(r.Enviado);
        Assert.Equal(UsoCompartilhado.MsgParada, r.Motivo);
        Assert.DoesNotContain("*081*1*2.0*", _simulador.Escritas);
        Assert.Equal((OrigemComando.Ia, false, UsoCompartilhado.MsgParada), (Ultima().Origem, Ultima().Enviado, Ultima().Motivo));
        Assert.Equal(EstadoIa.Inativa, _uso.Estado);
    }

    [Fact]
    public async Task As_saidas_de_falha_da_porta_entram_recusadas_com_o_motivo()
    {
        await Conectar();
        _transporte.Segurar("*081*0*1.0*");
        var segurado = _sessao.EnviarAsync("*081*0*1.0*");
        await _transporte.Chegou;
        var naFila = _sessao.EnviarAsync("*081*1*1.0*");
        await Task.Delay(50);

        await _sessao.DesconectarAsync(); // a fila do GATT termina
        var r = await naFila;
        _transporte.Soltar(); // o segurado chega ao simulador já desconectado: a escrita lança
        var s = await segurado;

        Assert.Equal(Sessao.MotivoConexaoTerminou, r.Motivo);
        Assert.Equal(Sessao.MotivoFalhaEscrita, s.Motivo);
        var fila = _historico.Entradas.Single(e => e.Endereco == new Endereco("081", 1));
        var falha = _historico.Entradas.Single(e => e.Endereco == new Endereco("081", 0));
        Assert.Equal((false, Sessao.MotivoConexaoTerminou), (fila.Enviado, fila.Motivo));
        Assert.Equal((false, Sessao.MotivoFalhaEscrita), (falha.Enviado, falha.Motivo));
    }

    // ----------------------------------------------------------------- o gerador: App e Tela

    private async Task LigarGerador()
    {
        Assert.True((await _chaves.DefinirAsync(Chave.Gerador, true, OrigemComando.Tela)).Ok);
        var tocar = await Rodar(_gerador.TocarTomAsync(1000, -20.0m));
        Assert.True(tocar.Ok, tocar.Mensagem);
    }

    private EntradaHistorico Desligar202() =>
        _historico.Entradas.First(e => e.Endereco == new Endereco("202", null) && e.Valor == "0");

    [Fact]
    public async Task Gerador_o_Parar_da_tela_entra_como_Tela()
    {
        await Conectar();
        await LigarGerador();

        Assert.True((await Rodar(_gerador.PararAsync())).Ok);

        Assert.Equal((OrigemComando.Tela, true), (Desligar202().Origem, Desligar202().Enviado));
    }

    [Fact]
    public async Task Gerador_ao_sair_do_app_desliga_como_App()
    {
        await Conectar();
        await LigarGerador();

        await Rodar(_gerador.DesligarAoSairAsync());

        Assert.Equal((OrigemComando.App, true), (Desligar202().Origem, Desligar202().Enviado));
    }

    [Fact]
    public async Task Gerador_antes_de_soltar_a_conexao_desliga_como_App()
    {
        await Conectar();
        await LigarGerador();

        await Rodar(_sessao.DesconectarAsync());

        Assert.Equal(EstadoSessao.Desconectado, _sessao.Estado);
        Assert.Equal((OrigemComando.App, true), (Desligar202().Origem, Desligar202().Enviado));
    }

    [Fact]
    public async Task Gerador_erro_ao_ligar_desliga_como_App()
    {
        await Conectar();
        Assert.True((await _chaves.DefinirAsync(Chave.Gerador, true, OrigemComando.Tela)).Ok);
        _simulador.Silenciar.Add("*202*1*");
        _simulador.Silenciar.Add("#202#");

        var r = await Rodar(_gerador.TocarTomAsync(1000, -20.0m));
        await AndarAte(() => _gerador.Estado == EstadoGerador.Desligado && !_gerador.TalvezLigado);

        Assert.False(r.Ok);
        Assert.Equal(OrigemComando.Tela, _historico.Entradas.First(e => e.Endereco == new Endereco("202", null) && e.Valor == "1").Origem);
        Assert.Equal((OrigemComando.App, true), (Desligar202().Origem, Desligar202().Enviado));
    }

    [Fact]
    public async Task Gerador_ligado_ao_conectar_a_regra_2_desliga_como_App()
    {
        _simulador.Definir("202", null, "1");

        await Conectar();

        Assert.False(_simulador.GeradorLigado);
        Assert.Equal((OrigemComando.App, true), (Desligar202().Origem, Desligar202().Enviado));
    }

    [Fact]
    public async Task O_historico_volta_ao_abrir_e_o_que_entrou_depois_fica_em_cima()
    {
        var guardado = Historico.ParaJson([new EntradaHistorico(_relogio.GetLocalNow(), OrigemComando.Ia, TipoComando.AjusteAudio,
            new Endereco("081", 0), "1.0", true, null)]);
        using var t = new UsoTestes(guardado, null);
        await t._historico.Carregado;

        t._historico.Registrar(Comando(1, "2.0"), OrigemComando.Tela, true, null);

        Assert.Equal(["2.0", "1.0"], t._historico.Entradas.Select(e => e.Valor));
    }
}

/// <summary>
/// O processador simulado com uma escrita que pode ficar segurada (o GATT ocupado), para ver o
/// que a porta faz com quem espera a fila.
/// </summary>
internal sealed class TransporteSegurado(ProcessadorSimulado simulador) : ITransporte
{
    private readonly object _trava = new();
    private string? _segurar;
    private TaskCompletionSource _chegou = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private TaskCompletionSource _soltar = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Completa quando o quadro segurado chegou à escrita.</summary>
    public Task Chegou
    {
        get
        {
            lock (_trava)
                return _chegou.Task;
        }
    }

    /// <summary>A próxima escrita deste quadro fica parada até <see cref="Soltar"/>.</summary>
    public void Segurar(string quadro)
    {
        lock (_trava)
        {
            _segurar = quadro;
            _chegou = new(TaskCreationOptions.RunContinuationsAsynchronously);
            _soltar = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }

    public void Soltar()
    {
        lock (_trava)
            _soltar.TrySetResult();
    }

    public event Action<byte[]>? Notificacao
    {
        add => simulador.Notificacao += value;
        remove => simulador.Notificacao -= value;
    }

    public event Action? Caiu
    {
        add => simulador.Caiu += value;
        remove => simulador.Caiu -= value;
    }

    public Task<string?> ProcurarAsync(TimeSpan duracao, CancellationToken ct) => simulador.ProcurarAsync(duracao, ct);

    public Task ConectarAsync(string dispositivo, CancellationToken ct) => simulador.ConectarAsync(dispositivo, ct);

    public Task DesconectarAsync() => simulador.DesconectarAsync();

    public async Task EscreverAsync(byte[] dados, CancellationToken ct)
    {
        Task? esperar = null;
        lock (_trava)
        {
            if (_segurar is not null && System.Text.Encoding.ASCII.GetString(dados) == _segurar)
            {
                _segurar = null;
                _chegou.TrySetResult();
                esperar = _soltar.Task;
            }
        }
        if (esperar is not null)
            await esperar;
        await simulador.EscreverAsync(dados, ct);
    }
}

/// <summary>Um texto guardado cuja leitura lança uma exceção qualquer.</summary>
internal sealed class RepositorioQueLanca : IRepositorioTexto
{
    public Task<string?> LerAsync() => throw new UnauthorizedAccessException("sem acesso");

    public Task GravarAsync(string conteudo) => Task.CompletedTask;
}
