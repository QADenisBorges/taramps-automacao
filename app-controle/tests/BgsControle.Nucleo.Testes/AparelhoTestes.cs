using System.Text.RegularExpressions;
using BgsControle.Nucleo.Aparelho;
using BgsControle.Nucleo.Escrita;
using BgsControle.Nucleo.Gerador;
using BgsControle.Nucleo.Leitura;
using BgsControle.Nucleo.Memorias;
using BgsControle.Nucleo.Registro;
using BgsControle.Nucleo.Seguranca;
using BgsControle.Simulador;

namespace BgsControle.Nucleo.Testes;

/// <summary>
/// A história 7b no núcleo, contra o processador simulado: a trava dos comandos de aparelho, o
/// <see cref="ComandosAparelho"/> (remote out, Bluetooth de áudio, resets, troca de senha e nome
/// do produto) e o <see cref="MonitorAparelho"/> (voltímetro e clip). As linhas da matriz de
/// <c>7b-monitoracao-aparelho-inteiro.md</c>.
/// </summary>
public sealed class AparelhoTestes : IDisposable
{
    private static readonly TemposSessao Tempos = new(
        Scan: TimeSpan.FromSeconds(1),
        TentativaConexao: TimeSpan.FromSeconds(1),
        PausaEntreTentativas: TimeSpan.FromMilliseconds(1),
        RespostaSenha: TimeSpan.FromSeconds(2),
        PrazoSenha: TimeSpan.FromSeconds(60));

    private static readonly TemposLeitura LeituraRapida = new(TimeSpan.Zero, TimeSpan.FromMilliseconds(100));

    // O ritmo real (30 ms) e prazos de eco e releitura curtos no relógio falso; o eco do
    // simulador chega em 85 ms, dentro do prazo.
    private static readonly TemposEscrita Escrita = new(
        Intervalo: TimeSpan.FromMilliseconds(30),
        Arrasto: TimeSpan.FromMilliseconds(300),
        PrazoEco: TimeSpan.FromMilliseconds(300),
        PrazoReleitura: TimeSpan.FromMilliseconds(400));

    private static readonly Regex Senha = new(@"^\*000\*[0-9A-Z]{4}\*$");

    private readonly RelogioFalso _relogio = new();
    private readonly ProcessadorSimulado _simulador;
    private readonly Chaves _chaves;
    private readonly CofreFalso _cofre = new() { Senha = "AB12" };
    private readonly LogBle _log = new();
    private readonly Sessao _sessao;
    private readonly LeituraAutomatica _leitura;
    private readonly EstadoAparelho _estado;
    private readonly CopiaAntes _copia;
    private readonly GerenteMemorias _memorias;
    private readonly GeradorSinal _gerador;
    private readonly MonitorAparelho _monitor;
    private readonly ComandosAparelho _comandos;

    /// <summary>Os comandos que o teste pediu (fora a senha e as leituras do catálogo).</summary>
    private readonly HashSet<string> _pedidos = [];

    public AparelhoTestes()
    {
        // A sessão e o leitor esperam em tempo real e o relógio falso não anda enquanto conectam:
        // a senha e as leituras respondem na hora; os ecos e os acks seguem no relógio falso.
        _simulador = new ProcessadorSimulado(_relogio) { AtrasoSenha = TimeSpan.Zero, AtrasoLeitura = TimeSpan.Zero };
        _chaves = new Chaves(new RepositorioMemoria());
        _sessao = new Sessao(_simulador, _cofre, _log, Tempos, _chaves);
        var leitor = new LeitorCompleto(_sessao, _log, LeituraRapida);
        _leitura = new LeituraAutomatica(_sessao, leitor);
        _estado = new EstadoAparelho(_sessao, _leitura);
        var escritor = new Escritor(_sessao, _estado, _log, relogio: _relogio);
        _copia = new CopiaAntes(_sessao, _estado, escritor, _leitura, new TextoMemoria(), _log, _relogio);
        _memorias = new GerenteMemorias(_sessao, _estado, _leitura, _copia, _log, relogio: _relogio);
        _gerador = new GeradorSinal(_sessao, _estado, _leitura, _copia, _log, _memorias, Escrita, relogio: _relogio);
        _monitor = new MonitorAparelho(_sessao, _leitura, _relogio);
        _comandos = new ComandosAparelho(_sessao, _estado, _leitura, _copia, _cofre, _log, _memorias, _gerador, Escrita, _relogio);
    }

    /// <summary>
    /// Critério de aceite: só chegaram a senha, as leituras do catálogo e os comandos da tabela da
    /// trava pedidos pelo teste.
    /// </summary>
    public void Dispose() =>
        Assert.All(_simulador.Escritas, e =>
            Assert.True(Senha.IsMatch(e) || Catalogo.EhLeitura(e) || _pedidos.Contains(e), e));

    // ----------------------------------------------------------------- apoio

    private async Task Conectar(params Chave[] ligar)
    {
        await _chaves.Carregada;
        await _copia.Carregada;
        foreach (var c in ligar)
            Assert.True((await _chaves.DefinirAsync(c, true, OrigemComando.Tela)).Ok);
        await _sessao.ConectarAsync();
        Assert.Equal(EstadoSessao.Conectado, _sessao.Estado);
        await AndarAte(Pronto);
    }

    private bool Pronto() =>
        _sessao.Estado == EstadoSessao.Conectado && _sessao.SegurandoConexao
        && _estado.Pronto && !_estado.Lendo && !_leitura.Lendo && !_copia.EmAndamento;

    /// <summary>Anda o relógio falso em passos de 10 ms até a condição valer.</summary>
    private async Task AndarAte(Func<bool> condicao, int passos = 3000)
    {
        for (var i = 0; i < passos && !condicao(); i++)
        {
            _relogio.Avancar(TimeSpan.FromMilliseconds(10));
            await Task.Delay(1);
        }
        Assert.True(condicao(), "a condição não ficou verdadeira");
    }

    private async Task Andar(TimeSpan quanto)
    {
        for (var t = TimeSpan.Zero; t < quanto; t += TimeSpan.FromMilliseconds(10))
        {
            _relogio.Avancar(TimeSpan.FromMilliseconds(10));
            await Task.Delay(1);
        }
    }

    private async Task<T> Rodar<T>(Task<T> tarefa)
    {
        await AndarAte(() => tarefa.IsCompleted);
        return await tarefa;
    }

    /// <summary>O teste pede estes comandos (o critério de aceite os deixa passar).</summary>
    private void Pedir(params string[] quadros)
    {
        foreach (var q in quadros)
            _pedidos.Add(q);
    }

    private List<string> Escritas(int desde = 0) => _simulador.Escritas.Skip(desde).ToList();

    /// <summary>As escritas que não são leituras do catálogo nem a senha.</summary>
    private List<string> Comandos(int desde = 0) =>
        Escritas(desde).Where(e => !Catalogo.EhLeitura(e) && !Senha.IsMatch(e)).ToList();

    /// <summary>Uma leitura completa nova saiu depois do comando (o Marco 1 inteiro foi perguntado de novo).</summary>
    private void LeuDeNovoDepois(string comando, int desde)
    {
        var escritas = Escritas(desde);
        var depois = escritas.Skip(escritas.IndexOf(comando) + 1).ToList();
        Assert.All(Catalogo.ConsultasMarco1, c => Assert.Contains(c.Texto, depois));
    }

    // ----------------------------------------------------------------- trava

    [Theory]
    [InlineData("*252*0*", null)] // "Remote out", "Bluetooth de áudio" e "Nome do produto" vêm ligadas
    [InlineData("*252*1*", null)]
    [InlineData("*251*0*", null)]
    [InlineData("*251*1*", null)]
    [InlineData("*002*SALA 1*", null)]
    [InlineData("*002*A_B*", null)]
    [InlineData("*004*", Sessao.MotivoChaveDesligada)] // os resets e a senha vêm desligados
    [InlineData("*242*", Sessao.MotivoChaveDesligada)]
    [InlineData("*001*AB12*CD34*", Sessao.MotivoChaveDesligada)]
    [InlineData("*252*2*", Sessao.MotivoAparelhoForaDoFormato)]
    [InlineData("*252*1", Sessao.MotivoAparelhoForaDoFormato)]
    [InlineData("*251*01*", Sessao.MotivoAparelhoForaDoFormato)]
    [InlineData("*002*sala*", Sessao.MotivoAparelhoForaDoFormato)]
    [InlineData("*002* SALA*", Sessao.MotivoAparelhoForaDoFormato)]
    [InlineData("*002*   *", Sessao.MotivoAparelhoForaDoFormato)]
    [InlineData("*002*NOME_COMPRIDO*", Sessao.MotivoAparelhoForaDoFormato)] // 13 caracteres
    [InlineData("*002*SALA#1*", Sessao.MotivoAparelhoForaDoFormato)]
    [InlineData("*004*1*", Sessao.MotivoAparelhoForaDoFormato)]
    [InlineData("*242*0*", Sessao.MotivoAparelhoForaDoFormato)]
    [InlineData("*001*AB12*AB12*", Sessao.MotivoAparelhoForaDoFormato)] // a nova igual à antiga
    [InlineData("*001*ab12*CD34*", Sessao.MotivoAparelhoForaDoFormato)]
    [InlineData("*001*AB12*CD3*", Sessao.MotivoAparelhoForaDoFormato)]
    [InlineData("*001*AB12*", Sessao.MotivoAparelhoForaDoFormato)]
    [InlineData("*005*120*", Sessao.MotivoEscritaForaDoCatalogo)] // só leitura
    [InlineData("*150*1*", Sessao.MotivoEscritaForaDoCatalogo)]
    public async Task A_trava_deixa_passar_so_a_forma_exata_com_a_chave_de_cada_comando(string quadro, string? motivo)
    {
        await Conectar();

        Assert.Equal(motivo, _sessao.Conferir(quadro));
    }

    [Fact]
    public async Task Com_as_chaves_ligadas_os_resets_e_a_senha_passam_e_so_o_240_e_o_23N_ficam_nao_liberados()
    {
        await Conectar(Chave.ResetFabrica, Chave.ResetAudio, Chave.TrocaSenha);

        Assert.Null(_sessao.Conferir("*004*"));
        Assert.Null(_sessao.Conferir("*242*"));
        Assert.Null(_sessao.Conferir("*001*AB12*CD34*"));
        // História 8c: salvar (240) e renomear (23N) as memórias do aparelho só no app da Taramps.
        string[] naoLiberados = ["240", "231", "232", "233"];
        foreach (var codigo in naoLiberados)
            Assert.Equal(Sessao.MotivoNaoLiberado, _sessao.Conferir($"*{codigo}*1*"));
        foreach (var codigo in Catalogo.Todos.Select(p => p.Codigo).Concat(Catalogo.CodigosDoAparelho).Except(naoLiberados))
        {
            foreach (var quadro in new[] { $"*{codigo}*", $"*{codigo}*1*", $"*{codigo}*X*Y*" })
                Assert.NotEqual(Sessao.MotivoNaoLiberado, _sessao.Conferir(quadro));
        }
    }

    [Fact]
    public async Task Sem_conexao_e_em_so_leitura_nada_passa()
    {
        await _chaves.Carregada;
        Assert.Equal(Sessao.MotivoSemConexao, _sessao.Conferir("*252*0*"));
        var r = await _comandos.DefinirRemoteOutAsync(false);
        Assert.False(r.Ok);
        Assert.Equal("Sem conexão autenticada.", r.Motivo);

        _simulador.Firmware = "R004";
        await Conectar();
        Assert.Equal(Sessao.MotivoSoLeitura, _sessao.Conferir("*252*0*"));
        Assert.Equal("Modo só leitura.", (await _comandos.NomearProdutoAsync("SALA")).Motivo);
        Assert.Empty(Comandos());
    }

    // ----------------------------------------------------------------- remote out e Bluetooth de áudio

    [Fact]
    public async Task Remote_out_desligado_com_eco_e_o_espelho_em_0()
    {
        await Conectar();
        Assert.True(_estado.Valor("252")!.Ligado); // semente: 252 = 1
        var antes = _simulador.Escritas.Count;
        Pedir("*252*0*");

        var r = await Rodar(_comandos.DefinirRemoteOutAsync(false));

        Assert.True(r.Ok, r.Mensagem);
        Assert.Equal(ComandosAparelho.MsgRemoteOut(false), r.Mensagem);
        Assert.Equal(["*252*0*"], Escritas(antes)); // confirmado pelo eco, sem releitura
        Assert.False(_estado.Valor("252")!.Ligado);
        Assert.Equal("0", _simulador.Valor("252"));
    }

    [Fact]
    public async Task Sem_eco_no_252_rele_e_vale_o_relido()
    {
        await Conectar();
        _simulador.Silenciar.Add("*252*0*");
        var antes = _simulador.Escritas.Count;
        Pedir("*252*0*");

        var r = await Rodar(_comandos.DefinirRemoteOutAsync(false));

        Assert.True(r.Ok, r.Mensagem);
        Assert.Equal(["*252*0*", "#252#"], Escritas(antes));
        Assert.Equal("0", _estado.Valor("252")!.NoFio);
    }

    [Fact]
    public async Task Eco_diferente_do_pedido_o_espelho_fica_com_o_do_aparelho()
    {
        await Conectar();
        _simulador.EcosDiferentes["*252*0*"] = "#252#1#";
        Pedir("*252*0*");

        var r = await Rodar(_comandos.DefinirRemoteOutAsync(false));

        Assert.False(r.Ok);
        Assert.Contains("ligado", r.Mensagem);
        Assert.True(_estado.Valor("252")!.Ligado);
    }

    [Fact]
    public async Task Bluetooth_de_audio_ligado_e_o_espelho_em_1()
    {
        _simulador.Definir("251", null, "0");
        await Conectar();
        Assert.False(_estado.Valor("251")!.Ligado);
        var antes = _simulador.Escritas.Count;
        Pedir("*251*1*");

        var r = await Rodar(_comandos.DefinirBluetoothAudioAsync(true));

        Assert.True(r.Ok, r.Mensagem);
        Assert.Equal(ComandosAparelho.MsgBluetoothAudio(true), r.Mensagem);
        Assert.Equal(["*251*1*"], Escritas(antes));
        Assert.True(_estado.Valor("251")!.Ligado);
    }

    [Fact]
    public async Task Remote_out_com_a_chave_desligada_nada_sai()
    {
        await _chaves.Carregada;
        Assert.True((await _chaves.DefinirAsync(Chave.RemoteOut, false, OrigemComando.Tela)).Ok);
        await Conectar();

        var r = await _comandos.DefinirRemoteOutAsync(false);

        Assert.False(r.Ok);
        Assert.Equal("Chave desligada.", r.Motivo);
        Assert.Empty(Comandos());
    }

    // ----------------------------------------------------------------- resets

    [Fact]
    public async Task Reset_de_audio_com_a_chave_desligada_nada_sai()
    {
        await Conectar();

        var r = await _comandos.ResetAudioAsync();

        Assert.False(r.Ok);
        Assert.Equal("Chave desligada.", r.Motivo);
        Assert.Empty(Comandos());
    }

    [Fact]
    public async Task Reset_de_audio_espera_o_ack_rele_e_o_espelho_mostra_a_fabrica()
    {
        await Conectar(Chave.ResetAudio);
        Assert.Equal("+10.0", _estado.Valor("081", 0)!.NoFio);
        var leiturasAntes = _leitura.Iniciadas;
        var antes = _simulador.Escritas.Count;
        Pedir("*242*");

        var r = await Rodar(_comandos.ResetAudioAsync());

        Assert.True(r.Ok, r.Mensagem);
        Assert.Equal(ComandosAparelho.MsgResetAudio, r.Mensagem);
        Assert.Equal(["*242*"], Comandos(antes));
        LeuDeNovoDepois("*242*", antes);
        Assert.Equal(leiturasAntes + 1, _leitura.Iniciadas);
        foreach (var ((codigo, indice), valor) in ProcessadorSimulado.Fabrica)
            Assert.Equal(valor, _estado.Valor(codigo, indice)!.NoFio);
        Assert.False(_comandos.Ocupado);
    }

    [Fact]
    public async Task Reset_de_fabrica_rele_esquece_a_senha_e_desconecta()
    {
        await Conectar(Chave.ResetFabrica);
        var antes = _simulador.Escritas.Count;
        Pedir("*004*");

        var r = await Rodar(_comandos.ResetFabricaAsync());

        Assert.True(r.Ok, r.Mensagem);
        Assert.Equal(ComandosAparelho.MsgResetFabrica, r.Mensagem);
        Assert.Equal(["*004*"], Comandos(antes));
        LeuDeNovoDepois("*004*", antes);
        Assert.Null(_cofre.Senha);
        Assert.Equal(EstadoSessao.Desconectado, _sessao.Estado);
        Assert.Equal(_simulador.SenhaDeFabrica, _simulador.Senha);

        // A próxima conexão pede a senha.
        await _sessao.ConectarAsync();
        Assert.Equal(EstadoSessao.PedindoSenha, _sessao.Estado);
    }

    [Fact]
    public async Task Gerador_tocando_o_reset_e_recusado_sem_enviar_nada()
    {
        await Conectar(Chave.Gerador, Chave.ResetAudio);
        Pedir("*213*0*", "*201*1000*", "*200*-20.0*", "*202*1*", "*202*0*");
        var tocar = await Rodar(_gerador.TocarTomAsync(1000, -20.0m));
        Assert.True(tocar.Ok, tocar.Mensagem);
        var antes = _simulador.Escritas.Count;

        var r = await _comandos.ResetAudioAsync();

        Assert.False(r.Ok);
        Assert.Equal(ComandosAparelho.MsgGeradorEmUso, r.Mensagem);
        Assert.Empty(Escritas(antes));

        Assert.True((await Rodar(_gerador.PararAsync())).Ok);
    }

    [Fact]
    public async Task Com_outro_comando_andando_o_segundo_e_recusado_sem_enviar()
    {
        await Conectar(Chave.ResetAudio);
        Pedir("*242*");
        var primeiro = _comandos.ResetAudioAsync();
        Assert.True(_comandos.Ocupado);

        var segundo = await _comandos.NomearProdutoAsync("SALA");

        Assert.False(segundo.Ok);
        Assert.Equal(ComandosAparelho.MsgOcupado, segundo.Motivo);
        Assert.True((await Rodar(primeiro)).Ok);
        Assert.DoesNotContain(_simulador.Escritas, e => e.StartsWith("*002*", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Lendo_os_comandos_de_aparelho_sao_recusados()
    {
        await Conectar(Chave.ResetAudio);
        var leitura = _leitura.LerAsync();
        Assert.True(_estado.Lendo);

        var r = await _comandos.ResetAudioAsync();
        var remote = await _comandos.DefinirRemoteOutAsync(false);

        Assert.Equal(Escritor.MsgLendo, r.Motivo);
        Assert.Equal(Escritor.MsgLendo, remote.Motivo);
        await Rodar(leitura);
        Assert.Empty(Comandos());
    }

    // ----------------------------------------------------------------- troca de senha

    [Fact]
    public async Task Troca_de_senha_guarda_a_nova_e_o_log_nao_mostra_as_senhas()
    {
        await Conectar(Chave.TrocaSenha);
        var antes = _simulador.Escritas.Count;
        Pedir("*001*AB12*CD34*");

        var r = await Rodar(_comandos.TrocarSenhaAsync("ab12", "cd34"));

        Assert.True(r.Ok, r.Mensagem);
        Assert.Equal(ComandosAparelho.MsgSenhaTrocada, r.Mensagem);
        Assert.Equal(["*001*AB12*CD34*"], Comandos(antes));
        Assert.Equal("CD34", _cofre.Senha);
        Assert.Equal("CD34", _simulador.Senha);
        LeuDeNovoDepois("*001*AB12*CD34*", antes);
        // Critério de aceite: nenhuma das duas senhas no log.
        Assert.DoesNotContain(_log.Linhas, l => l.Contains("AB12", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(_log.Linhas, l => l.Contains("CD34", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(_log.Linhas, l => l.Contains("*001*" + LogBle.Mascara));
        Assert.DoesNotContain("AB12", r.Mensagem);
        Assert.DoesNotContain("CD34", r.Mensagem);

        // A próxima conexão usa a senha nova.
        await _sessao.DesconectarAsync();
        await _sessao.ConectarAsync();
        Assert.Equal(EstadoSessao.Conectado, _sessao.Estado);
        await AndarAte(Pronto);
    }

    [Fact]
    public async Task Senha_atual_errada_o_aparelho_recusa_e_a_guardada_nao_muda()
    {
        await Conectar(Chave.TrocaSenha);
        var antes = _simulador.Escritas.Count;
        var leiturasAntes = _leitura.Iniciadas;
        Pedir("*001*XXXX*CD34*");

        var r = await Rodar(_comandos.TrocarSenhaAsync("XXXX", "CD34"));

        Assert.False(r.Ok);
        Assert.Equal(ComandosAparelho.MsgSenhaRecusada, r.Mensagem);
        Assert.Equal("O aparelho recusou a senha atual.", r.Mensagem);
        Assert.Equal(["*001*XXXX*CD34*"], Comandos(antes));
        Assert.Equal("AB12", _cofre.Senha);
        Assert.Equal("AB12", _simulador.Senha);
        Assert.Equal(leiturasAntes, _leitura.Iniciadas); // nada mudou: sem leitura nova
        Assert.DoesNotContain(_log.Linhas, l => l.Contains("XXXX") || l.Contains("CD34"));
    }

    [Theory]
    [InlineData("AB1", "CD34", ComandosAparelho.MsgSenhaInvalida)]
    [InlineData("AB12", "CD3#", ComandosAparelho.MsgSenhaInvalida)]
    [InlineData("AB12", "ab12", ComandosAparelho.MsgSenhaIgual)]
    public async Task Senha_fora_do_formato_nada_sai(string atual, string nova, string erro)
    {
        await Conectar(Chave.TrocaSenha);

        var r = await _comandos.TrocarSenhaAsync(atual, nova);

        Assert.False(r.Ok);
        Assert.Equal(erro, r.Mensagem);
        Assert.Empty(Comandos());
    }

    // ----------------------------------------------------------------- nome do produto

    [Fact]
    public async Task Nome_do_produto_vai_em_maiusculas_espera_o_ack_e_rele()
    {
        await Conectar();
        var antes = _simulador.Escritas.Count;
        Pedir("*002*SALA 1*");

        var r = await Rodar(_comandos.NomearProdutoAsync("sala 1"));

        Assert.True(r.Ok, r.Mensagem);
        Assert.Equal("Nome do produto: SALA 1", r.Mensagem);
        Assert.Equal(["*002*SALA 1*"], Comandos(antes));
        Assert.Equal("SALA 1", _simulador.NomeProduto);
        LeuDeNovoDepois("*002*SALA 1*", antes);
    }

    [Theory]
    [InlineData("sala#1")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("nome comprido")]
    [InlineData("são")]
    public async Task Nome_invalido_nada_sai(string nome)
    {
        await Conectar();

        var r = await _comandos.NomearProdutoAsync(nome);

        Assert.False(r.Ok);
        Assert.Equal("Use até 12 letras, números, espaço ou _", r.Mensagem);
        Assert.Empty(Comandos());
    }

    // ----------------------------------------------------------------- sem resposta e respostas estranhas

    /// <summary>Envia o comando com a resposta do simulador silenciada e manda esta resposta no lugar.</summary>
    private async Task<ResultadoAparelho> ComRespostaTrocada(string quadro, string resposta, Func<Task<ResultadoAparelho>> comando)
    {
        _simulador.Silenciar.Add(quadro);
        Pedir(quadro);
        var tarefa = comando();
        await AndarAte(() => _simulador.Escritas.Contains(quadro));
        _simulador.Emitir(resposta + "\r\n");
        return await Rodar(tarefa);
    }

    [Fact]
    public async Task Reset_de_fabrica_sem_resposta_a_senha_fica_e_a_conexao_continua()
    {
        await Conectar(Chave.ResetFabrica);
        _simulador.Silenciar.Add("*004*");
        var antes = _simulador.Escritas.Count;
        Pedir("*004*");

        var r = await Rodar(_comandos.ResetFabricaAsync());

        Assert.False(r.Ok);
        Assert.Equal(ComandosAparelho.MsgResetFabricaSemResposta, r.Mensagem);
        Assert.Equal("AB12", _cofre.Senha);
        Assert.Equal(0, _cofre.Apagadas);
        Assert.Equal(EstadoSessao.Conectado, _sessao.Estado);
        LeuDeNovoDepois("*004*", antes);
    }

    [Fact]
    public async Task Troca_de_senha_sem_resposta_nada_e_guardado_e_a_frase_pede_para_anotar()
    {
        await Conectar(Chave.TrocaSenha);
        _simulador.Silenciar.Add("*001*AB12*CD34*");
        Pedir("*001*AB12*CD34*");

        var r = await Rodar(_comandos.TrocarSenhaAsync("AB12", "CD34"));

        Assert.False(r.Ok);
        Assert.Equal(ComandosAparelho.MsgSenhaSemResposta, r.Mensagem);
        Assert.Contains("anote", r.Mensagem);
        Assert.DoesNotContain("CD34", r.Mensagem);
        Assert.Equal("AB12", _cofre.Senha);
        Assert.Empty(_cofre.Guardadas.Where(g => g != "AB12"));
    }

    [Fact]
    public async Task Troca_de_senha_com_o_cofre_falhando_avisa_que_nao_guardou()
    {
        await Conectar(Chave.TrocaSenha);
        _cofre.FalharAoGuardar = true; // depois de conectar: a sessão guarda a senha aceita
        Pedir("*001*AB12*CD34*");

        var r = await Rodar(_comandos.TrocarSenhaAsync("AB12", "CD34"));

        Assert.True(r.Ok);
        Assert.Equal(ComandosAparelho.MsgSenhaNaoGuardada, r.Mensagem);
        Assert.Equal("AB12", _cofre.Senha);
    }

    [Fact]
    public async Task Troca_de_senha_com_resposta_que_nao_e_OK_nem_ERRO_fica_incerta()
    {
        await Conectar(Chave.TrocaSenha);

        var r = await ComRespostaTrocada("*001*AB12*CD34*", "#001#TALVEZ#", () => _comandos.TrocarSenhaAsync("AB12", "CD34"));

        Assert.False(r.Ok);
        Assert.Equal(ComandosAparelho.MsgSenhaSemResposta, r.Mensagem);
        Assert.Equal("AB12", _cofre.Senha);
    }

    [Fact]
    public async Task Remote_out_sem_eco_e_sem_releitura_falha_e_o_espelho_nao_muda()
    {
        await Conectar();
        _simulador.Silenciar.Add("*252*0*");
        _simulador.Silenciar.Add("#252#");
        Pedir("*252*0*");

        var r = await Rodar(_comandos.DefinirRemoteOutAsync(false));

        Assert.False(r.Ok);
        Assert.Equal(ComandosAparelho.MsgSemConfirmacao, r.Mensagem);
        Assert.True(_estado.Valor("252")!.Ligado);
    }

    [Fact]
    public async Task Reset_de_audio_sem_resposta_falha_e_rele_mesmo_assim()
    {
        await Conectar(Chave.ResetAudio);
        _simulador.Silenciar.Add("*242*");
        var leiturasAntes = _leitura.Iniciadas;
        var antes = _simulador.Escritas.Count;
        Pedir("*242*");

        var r = await Rodar(_comandos.ResetAudioAsync());

        Assert.False(r.Ok);
        Assert.Equal(ComandosAparelho.MsgResetAudioSemResposta, r.Mensagem);
        Assert.Equal(leiturasAntes + 1, _leitura.Iniciadas);
        LeuDeNovoDepois("*242*", antes);
    }

    [Fact]
    public async Task Com_um_carregar_pela_metade_reset_e_nome_sao_recusados_sem_enviar()
    {
        _simulador.DefinirMemoria(2, "081", 0, "-20.0");
        await Conectar(Chave.ResetAudio);
        Pedir("*241*2*");
        _simulador.CairDepoisDaEscrita = _simulador.Escritas.Count + 1;
        Assert.False((await Rodar(_memorias.CarregarAsync(2))).Ok);
        _simulador.CairDepoisDaEscrita = null;
        await Conectar();
        await AndarAte(() => _copia.PelaMetade is not null);
        var antes = _simulador.Escritas.Count;

        var reset = await _comandos.ResetAudioAsync();
        var nome = await _comandos.NomearProdutoAsync("SALA");

        Assert.Equal(CopiaAntes.MsgPelaMetadePendente, reset.Motivo);
        Assert.Equal(CopiaAntes.MsgPelaMetadePendente, nome.Motivo);
        Assert.Equal(CopiaAntes.MsgPelaMetadePendente, _comandos.MotivoBloqueio());
        Assert.Empty(Escritas(antes));
    }

    [Fact]
    public async Task Nome_do_produto_com_outro_nome_no_ack_falha()
    {
        await Conectar();

        var r = await ComRespostaTrocada("*002*SALA*", "#002#OUTRO#", () => _comandos.NomearProdutoAsync("sala"));

        Assert.False(r.Ok);
        Assert.Equal("O aparelho ficou com o nome OUTRO.", r.Mensagem);
    }

    // ----------------------------------------------------------------- monitoração

    [Fact]
    public async Task A_leitura_semeia_o_clip_e_o_voltimetro_fica_sem_valor_com_o_ERRO()
    {
        _simulador.Definir("150", null, "1");
        _simulador.Definir("111", null, "1");
        Assert.Null(_monitor.Volts);

        await Conectar();

        Assert.Null(_monitor.Volts); // a consulta #005# responde ERRO 0 (medido)
        Assert.True(_monitor.ClipSaida(1));
        Assert.False(_monitor.ClipSaida(2));
        Assert.True(_monitor.ClipEntrada);
    }

    [Fact]
    public async Task Voltimetro_espontaneo()
    {
        await Conectar();
        var avisos = 0;
        _monitor.Mudou += () => Interlocked.Increment(ref avisos);

        _simulador.EmitirVoltimetro(124);

        Assert.Equal(12.4m, _monitor.Volts);
        Assert.Equal(1, avisos);

        // Só ouve: nenhuma consulta periódica ao voltímetro (só a da leitura completa).
        await Andar(TimeSpan.FromSeconds(10));
        Assert.Equal(1, _simulador.Escritas.Count(e => e == "#005#"));
        Assert.Empty(Comandos());
    }

    [Fact]
    public async Task Clip_da_saida_3_acende_e_apaga_3_s_depois_do_ultimo_aviso()
    {
        await Conectar();

        _simulador.EmitirClip(4);

        Assert.True(_monitor.ClipSaida(3));
        Assert.False(_monitor.ClipSaida(1));
        Assert.False(_monitor.ClipSaida(2));
        Assert.False(_monitor.ClipSaida(4));
        Assert.False(_monitor.ClipEntrada);

        await Andar(TimeSpan.FromSeconds(2));
        _simulador.EmitirClip(4); // outro aviso: conta de novo
        await Andar(TimeSpan.FromSeconds(2.5));
        Assert.True(_monitor.ClipSaida(3));
        _simulador.EmitirClip(0); // sem o bit: não renova
        await Andar(TimeSpan.FromMilliseconds(600));
        Assert.False(_monitor.ClipSaida(3));
    }

    [Fact]
    public async Task Clip_da_entrada_acende()
    {
        await Conectar();

        _simulador.EmitirClipEntrada(true);

        Assert.True(_monitor.ClipEntrada);
        Assert.False(_monitor.ClipSaida(1));
        await Andar(MonitorAparelho.ClipAceso + TimeSpan.FromMilliseconds(10));
        Assert.False(_monitor.ClipEntrada);
    }

    [Fact]
    public async Task Ao_desconectar_o_voltimetro_volta_a_sem_valor_e_os_clips_apagam()
    {
        await Conectar();
        _simulador.EmitirVoltimetro(131);
        _simulador.EmitirClip(0b1111);
        _simulador.EmitirClipEntrada(true);
        Assert.Equal(13.1m, _monitor.Volts);

        _simulador.Derrubar();
        await AndarAte(() => _sessao.Estado == EstadoSessao.Desconectado);

        Assert.Null(_monitor.Volts);
        for (var s = 1; s <= MonitorAparelho.Saidas; s++)
            Assert.False(_monitor.ClipSaida(s));
        Assert.False(_monitor.ClipEntrada);
    }
}
