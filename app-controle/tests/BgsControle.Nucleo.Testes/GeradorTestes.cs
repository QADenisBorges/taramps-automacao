using System.Text.RegularExpressions;
using BgsControle.Nucleo.Escrita;
using BgsControle.Nucleo.Gerador;
using BgsControle.Nucleo.Leitura;
using BgsControle.Nucleo.Memorias;
using BgsControle.Nucleo.Registro;
using BgsControle.Nucleo.Seguranca;
using BgsControle.Simulador;

namespace BgsControle.Nucleo.Testes;

/// <summary>
/// A história 7a no núcleo, contra o processador simulado: a trava do gerador, o
/// <see cref="GeradorSinal"/> com o prazo de 30 s e as regras de falha 1 e 2. As linhas da
/// matriz de <c>7a-gerador-regras-de-falha.md</c>; a linha do tempo do 202 do simulador prova
/// cada desligamento.
/// </summary>
public sealed class GeradorTestes : IDisposable
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

    /// <summary>O prazo real de 30 s; só a pausa entre as reconexões é curta.</summary>
    private static readonly TemposGerador TemposDoGerador = new(TimeSpan.FromSeconds(30), TimeSpan.FromMilliseconds(100));

    private static readonly TimeSpan Prazo = TimeSpan.FromSeconds(30);
    private static readonly Regex Senha = new(@"^\*000\*[0-9A-Z]{4}\*$");

    private readonly RelogioFalso _relogio = new();
    private readonly ProcessadorSimulado _simulador;
    private readonly RepositorioMemoria _repositorioChaves = new();
    private readonly Chaves _chaves;
    private readonly LogBle _log = new();
    private readonly Sessao _sessao;
    private readonly LeitorCompleto _leitor;
    private readonly LeituraAutomatica _leitura;
    private readonly EstadoAparelho _estado;
    private readonly Escritor _escritor;
    private readonly CopiaAntes _copia;
    private readonly GerenteMemorias _memorias;
    private readonly GeradorSinal _gerador;
    private volatile bool _alarmeVisto;

    public GeradorTestes()
    {
        // A sessão e o leitor esperam em tempo real e o relógio falso não anda enquanto conectam:
        // a senha e as leituras respondem na hora; os ecos e os prazos seguem no relógio falso.
        _simulador = new ProcessadorSimulado(_relogio) { AtrasoSenha = TimeSpan.Zero, AtrasoLeitura = TimeSpan.Zero };
        _chaves = new Chaves(_repositorioChaves);
        _sessao = new Sessao(_simulador, new CofreFalso { Senha = "AB12" }, _log, Tempos, _chaves);
        _leitor = new LeitorCompleto(_sessao, _log, LeituraRapida);
        _leitura = new LeituraAutomatica(_sessao, _leitor);
        _estado = new EstadoAparelho(_sessao, _leitura);
        _escritor = new Escritor(_sessao, _estado, _log, relogio: _relogio);
        _copia = new CopiaAntes(_sessao, _estado, _escritor, _leitura, new TextoMemoria(), _log, _relogio);
        _memorias = new GerenteMemorias(_sessao, _estado, _leitura, _copia, _log, relogio: _relogio);
        _gerador = new GeradorSinal(_sessao, _estado, _leitura, _copia, _log, _memorias, Escrita, TemposDoGerador, _relogio);
        _gerador.Mudou += () =>
        {
            if (_gerador.Alarme)
                _alarmeVisto = true;
        };
    }

    /// <summary>
    /// Critério de aceite: cada <c>*202*1*</c> tem um <c>*202*0*</c> aceito até 30 s depois, ou a
    /// queda e o alarme; fora do gerador, só chegaram a senha e as leituras do catálogo.
    /// </summary>
    public void Dispose()
    {
        Assert.All(_simulador.Escritas, e =>
            Assert.True(Senha.IsMatch(e) || Catalogo.EhLeitura(e) || Catalogo.ComandoDeGerador(e) is not null, e));

        var marcas = _simulador.LinhaDoTempoGerador;
        for (var i = 0; i < marcas.Count; i++)
        {
            if (!marcas[i].Ligado)
                continue;
            var desligou = marcas.Skip(i + 1).FirstOrDefault(m => !m.Ligado);
            if (desligou is not null && desligou.Quando - marcas[i].Quando <= Prazo)
                continue;
            Assert.True(_alarmeVisto && _simulador.Quedas > 0,
                $"o *202*1* de {marcas[i].Quando:HH:mm:ss.fff} ficou sem *202*0* em 30 s, sem a queda e o alarme");
        }
    }

    // ----------------------------------------------------------------- apoio

    private async Task Conectar(bool ligarChave = true)
    {
        await _chaves.Carregada;
        await _copia.Carregada;
        if (ligarChave)
            Assert.True((await _chaves.DefinirAsync(Chave.Gerador, true, OrigemComando.Tela)).Ok);
        await _sessao.ConectarAsync();
        Assert.Equal(EstadoSessao.Conectado, _sessao.Estado);
        await AndarAte(Pronto);
    }

    private bool Pronto() =>
        SessaoAberta && _estado.Pronto && !_estado.Lendo && !_leitura.Lendo && !_copia.EmAndamento
        && _gerador.Estado is not (EstadoGerador.Desligando or EstadoGerador.Reconectando);

    private bool SessaoAberta => _sessao.Estado == EstadoSessao.Conectado && _sessao.SegurandoConexao;

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

    private async Task Rodar(Task tarefa)
    {
        await AndarAte(() => tarefa.IsCompleted);
        await tarefa;
    }

    /// <summary>Tom fixo de 1000 Hz a −20 dB, confirmado.</summary>
    private async Task TocarTom()
    {
        var r = await Rodar(_gerador.TocarTomAsync(1000, -20.0m));
        Assert.True(r.Ok, r.Mensagem);
        Assert.Equal(EstadoGerador.Tocando, _gerador.Estado);
        Assert.True(_simulador.GeradorLigado);
    }

    /// <summary>Pula até perto do fim do prazo e anda em passos curtos até o gerador desligar.</summary>
    private async Task AndarAteOPrazo()
    {
        var falta = _gerador.Restante!.Value;
        _relogio.Avancar(falta - TimeSpan.FromMilliseconds(200));
        await Task.Delay(1);
        Assert.Equal(EstadoGerador.Tocando, _gerador.Estado);
        await AndarAte(() => _gerador.Estado == EstadoGerador.Desligado && !_gerador.TalvezLigado);
    }

    private List<string> Escritas(int desde = 0) => _simulador.Escritas.Skip(desde).ToList();

    /// <summary>As escritas que não são leituras do catálogo nem a senha.</summary>
    private List<string> Comandos(int desde = 0) =>
        Escritas(desde).Where(e => !Catalogo.EhLeitura(e) && !Senha.IsMatch(e)).ToList();

    // ----------------------------------------------------------------- critério de aceite

    [Fact]
    public async Task Tom_fixo_sai_na_ordem_e_desliga_sozinho_no_prazo_de_30_s()
    {
        await Conectar();
        var antes = _simulador.Escritas.Count;

        await TocarTom();

        Assert.Equal(["*213*0*", "*201*1000*", "*200*-20.0*", "*202*1*"], Comandos(antes));
        Assert.True(_gerador.TalvezLigado);
        Assert.Equal(GeradorSinal.MsgTocando, _gerador.Mensagem);
        Assert.InRange(_gerador.Restante!.Value, TimeSpan.FromSeconds(29.5), Prazo);
        Assert.Equal("1", _estado.Valor("202")!.NoFio);
        Assert.Equal("1000", _estado.Valor("201")!.NoFio);

        await AndarAteOPrazo();

        var marcas = _simulador.LinhaDoTempoGerador;
        Assert.Equal([true, false], marcas.Select(m => m.Ligado));
        Assert.True(marcas[1].Quando - marcas[0].Quando <= Prazo);
        Assert.True(marcas[1].Quando - marcas[0].Quando >= Prazo - TimeSpan.FromMilliseconds(10));
        Assert.Equal(["*213*0*", "*201*1000*", "*200*-20.0*", "*202*1*", "*202*0*"], Comandos(antes));
        Assert.DoesNotContain("#202#", Escritas(antes)); // confirmado pelo eco
        Assert.False(_simulador.GeradorLigado);
        Assert.Equal(GeradorSinal.MsgPrazo, _gerador.Mensagem);
        Assert.False(_estado.Valor("202")!.Ligado);
        Assert.Null(_gerador.Restante);
    }

    [Fact]
    public async Task Conexao_nova_com_o_202_ligado_o_202_0_e_a_primeira_escrita_depois_das_leituras()
    {
        _simulador.Definir("202", null, "1");
        bool? ligadoNoFim = null;
        var desligadoAntesDoFim = false;
        _leitura.Terminou += r =>
        {
            ligadoNoFim = r.Item("202")?.Valor.Ligado;
            desligadoAntesDoFim = _simulador.Escritas.Contains("*202*0*");
        };

        await Conectar(ligarChave: false); // desligar não precisa da chave

        var escritas = Escritas();
        var primeiraNaoLeitura = escritas.Skip(1).First(e => !Catalogo.EhLeitura(e));
        Assert.Equal("*202*0*", primeiraNaoLeitura);
        Assert.Equal(["*202*0*"], Comandos());
        Assert.True(escritas.Take(escritas.IndexOf("*202*0*")).Skip(1).All(Catalogo.EhLeitura));
        Assert.Contains("#202#", escritas.Take(escritas.IndexOf("*202*0*"))); // a leitura viu o 202
        Assert.True(desligadoAntesDoFim, "o *202*0* saiu antes do aviso de fim da leitura");
        Assert.False(ligadoNoFim); // o espelho mostra o 202 confirmado
        Assert.False(_estado.Valor("202")!.Ligado);
        Assert.False(_simulador.GeradorLigado);
        Assert.Equal(GeradorSinal.MsgEstavaLigado, _gerador.Mensagem);
        Assert.False(_gerador.TalvezLigado);
    }

    // ----------------------------------------------------------------- matriz

    [Fact]
    public async Task Sweep_sai_na_ordem_e_Parar_desliga_na_hora()
    {
        await Conectar();
        var antes = _simulador.Escritas.Count;

        var r = await Rodar(_gerador.TocarSweepAsync(20, 200, -30.0m, 2));
        Assert.True(r.Ok, r.Mensagem);
        Assert.Equal(["*210*20*", "*211*200*", "*212*-30.0*", "*213*2*", "*202*1*"], Comandos(antes));

        var agora = _relogio.GetUtcNow();
        var parar = _gerador.PararAsync();
        Assert.Equal(agora, _simulador.LinhaDoTempoGerador[^1].Quando); // saiu na hora
        Assert.False(_simulador.LinhaDoTempoGerador[^1].Ligado);
        var p = await Rodar(parar);

        Assert.True(p.Ok);
        Assert.Equal(GeradorSinal.MsgParado, _gerador.Mensagem);
        Assert.Equal(EstadoGerador.Desligado, _gerador.Estado);
        Assert.False(_gerador.TalvezLigado);
        Assert.Null(_gerador.Restante);
        Assert.False(_simulador.GeradorLigado);
    }

    [Fact]
    public async Task Chave_desligada_Tocar_nao_envia_nada()
    {
        await Conectar(ligarChave: false);
        var antes = _simulador.Escritas.Count;

        var r = await _gerador.TocarTomAsync(1000, -20.0m);

        Assert.False(r.Ok);
        Assert.Equal(Sessao.MotivoChaveDesligada, r.Motivo);
        Assert.Equal(antes, _simulador.Escritas.Count);
        Assert.Equal(EstadoGerador.Desligado, _gerador.Estado);
        Assert.False(_gerador.TalvezLigado);
    }

    [Theory]
    [InlineData(1000, 1.0)]
    [InlineData(1000, -60.5)]
    [InlineData(1000, -20.05)]
    [InlineData(5, -20.0)]
    [InlineData(22001, -20.0)]
    public async Task Fora_da_faixa_nada_sai(int hz, double db)
    {
        await Conectar();
        var antes = _simulador.Escritas.Count;

        var r = await _gerador.TocarTomAsync(hz, (decimal)db);

        Assert.False(r.Ok);
        Assert.NotNull(r.Motivo);
        Assert.Equal(antes, _simulador.Escritas.Count);
        Assert.False(_gerador.TalvezLigado);
    }

    [Theory]
    [InlineData(5, 200, -30.0, 2)]
    [InlineData(20, 200, 0.5, 2)]
    [InlineData(20, 200, -30.0, 0)]
    [InlineData(20, 200, -30.0, 4)]
    public async Task Sweep_fora_da_faixa_nada_sai(int inicio, int fim, double db, int velocidade)
    {
        await Conectar();
        var antes = _simulador.Escritas.Count;

        var r = await _gerador.TocarSweepAsync(inicio, fim, (decimal)db, velocidade);

        Assert.False(r.Ok);
        Assert.Equal(antes, _simulador.Escritas.Count);
    }

    [Fact]
    public async Task Sem_eco_no_desligar_rele_o_202_e_confirma()
    {
        _simulador.Silenciar.Add("*202*0*");
        await Conectar();
        await TocarTom();
        var antes = _simulador.Escritas.Count;

        var r = await Rodar(_gerador.PararAsync());

        Assert.True(r.Ok, r.Mensagem);
        Assert.Equal(["*202*0*", "#202#"], Escritas(antes));
        Assert.Equal(EstadoGerador.Desligado, _gerador.Estado);
        Assert.False(_gerador.TalvezLigado);
        Assert.False(_estado.Valor("202")!.Ligado);
    }

    [Fact]
    public async Task Queda_tocando_reconecta_e_o_202_0_e_a_primeira_escrita_da_nova_conexao()
    {
        await Conectar();
        await TocarTom();
        var conexoes = _simulador.Conexoes;
        var antes = _simulador.Escritas.Count;

        _simulador.Derrubar();
        await AndarAte(() => Pronto() && !_gerador.TalvezLigado);

        Assert.Equal(conexoes + 1, _simulador.Conexoes);
        var depois = Escritas(antes);
        Assert.Matches(Senha, depois[0]);
        Assert.Equal("*202*0*", depois.Skip(1).First(e => !Catalogo.EhLeitura(e)));
        Assert.Equal(["*202*0*"], Comandos(antes));
        Assert.False(_simulador.GeradorLigado);
        Assert.Equal(GeradorSinal.MsgEstavaLigado, _gerador.Mensagem);
        Assert.Equal(EstadoGerador.Desligado, _gerador.Estado);
        Assert.False(_gerador.Alarme);
    }

    [Fact]
    public async Task Queda_sem_volta_3_reconexoes_falham_e_o_alarme_fica_ate_ler_202_0()
    {
        await Conectar();
        await TocarTom();
        var procuras = _simulador.Procuras;
        _simulador.Endereco = null; // o aparelho some

        _simulador.Derrubar();
        await AndarAte(() => _gerador.Alarme && _gerador.Estado == EstadoGerador.SemResposta);

        Assert.Equal(procuras + GeradorSinal.TentativasDeReconexao, _simulador.Procuras);
        Assert.Equal(GeradorSinal.MsgAlarme, _gerador.Mensagem);
        Assert.True(_gerador.TalvezLigado);
        Assert.True(_simulador.GeradorLigado);

        // O alarme fica: o tempo passa e nada muda sozinho.
        await Andar(TimeSpan.FromSeconds(2));
        Assert.True(_gerador.Alarme);
        Assert.Equal(EstadoSessao.Desconectado, _sessao.Estado);
        Assert.Equal(procuras + GeradorSinal.TentativasDeReconexao, _simulador.Procuras);

        // O aparelho volta e o Denis conecta: a leitura vê 202 = 1, desliga, e o alarme sai.
        _simulador.Endereco = ProcessadorSimulado.EnderecoReal;
        await _sessao.ConectarAsync();
        await AndarAte(() => !_gerador.Alarme && Pronto());

        Assert.False(_gerador.TalvezLigado);
        Assert.False(_simulador.GeradorLigado);
        Assert.Equal(EstadoGerador.Desligado, _gerador.Estado);
    }

    [Fact]
    public async Task Desligar_sem_confirmacao_reconecta_3_vezes_e_mostra_o_alarme()
    {
        await Conectar();
        await TocarTom();
        _simulador.Silenciar.Add("*202*0*");
        _simulador.Silenciar.Add("#202#");
        var conexoes = _simulador.Conexoes;

        var r = await Rodar(_gerador.PararAsync());
        Assert.False(r.Ok);
        await AndarAte(() => _gerador.Alarme && _gerador.Estado == EstadoGerador.SemResposta, passos: 5000);

        Assert.Equal(conexoes + GeradorSinal.TentativasDeReconexao, _simulador.Conexoes);
        Assert.Equal(GeradorSinal.MsgAlarme, _gerador.Mensagem);
        // O aparelho aplicou o *202*0* (só não respondeu): a linha do tempo mostra o desligado.
        Assert.False(_simulador.GeradorLigado);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Desconectar_ou_esquecer_a_senha_tocando_desliga_antes_de_soltar(bool esquecer)
    {
        await Conectar();
        await TocarTom();
        var antes = _simulador.Escritas.Count;
        var desconexoes = _simulador.Desconexoes;

        await Rodar(esquecer ? _sessao.EsquecerSenhaAsync() : _sessao.DesconectarAsync());

        Assert.Equal(["*202*0*"], Comandos(antes));
        Assert.Equal(desconexoes + 1, _simulador.Desconexoes);
        Assert.Equal(EstadoSessao.Desconectado, _sessao.Estado);
        Assert.False(_simulador.GeradorLigado);
        Assert.False(_gerador.TalvezLigado);
        Assert.Equal(EstadoGerador.Desligado, _gerador.Estado);

        // Desligado e confirmado: nada de reconectar.
        var conexoes = _simulador.Conexoes;
        await Andar(TimeSpan.FromMilliseconds(500));
        Assert.Equal(conexoes, _simulador.Conexoes);
    }

    [Fact]
    public async Task Ler_de_novo_tocando_nao_desliga()
    {
        await Conectar();
        await TocarTom();
        var antes = _simulador.Escritas.Count;

        var retrato = await Rodar(_leitura.LerAsync());

        Assert.NotNull(retrato);
        Assert.True(retrato!.Completo);
        Assert.Empty(Comandos(antes));
        Assert.Equal(EstadoGerador.Tocando, _gerador.Estado);
        Assert.True(_estado.Valor("202")!.Ligado);
        Assert.True(_simulador.GeradorLigado);

        await AndarAteOPrazo(); // o prazo segue valendo
        Assert.Equal(["*202*0*"], Comandos(antes));
    }

    [Fact]
    public async Task So_leitura_com_o_202_ligado_nada_sai_e_vale_o_alarme_ate_ler_202_0()
    {
        _simulador.Firmware = "R004";
        _simulador.Definir("202", null, "1");

        await Conectar();

        Assert.True(_sessao.SoLeitura);
        Assert.Empty(Comandos());
        Assert.True(_gerador.Alarme);
        Assert.Equal(EstadoGerador.SemResposta, _gerador.Estado);
        Assert.Equal(GeradorSinal.MsgAlarme, _gerador.Mensagem);
        Assert.Equal(Sessao.MotivoSoLeitura, _sessao.Conferir("*202*0*"));
        var parar = await Rodar(_gerador.PararAsync());
        Assert.False(parar.Ok);
        Assert.Empty(Comandos());

        // Alguém desligou o módulo: a próxima leitura mostra 202 = 0 e o alarme sai.
        _simulador.Definir("202", null, "0");
        await Rodar(_leitura.LerAsync());

        Assert.False(_gerador.Alarme);
        Assert.Equal(EstadoGerador.Desligado, _gerador.Estado);
        Assert.Empty(Comandos());
    }

    // ----------------------------------------------------------------- recusas e falhas

    [Fact]
    public async Task Um_parametro_sem_confirmacao_e_o_202_1_nao_sai()
    {
        _simulador.Silenciar.Add("*201*1000*");
        _simulador.Silenciar.Add("#201#");
        await Conectar();
        var antes = _simulador.Escritas.Count;

        var r = await Rodar(_gerador.TocarTomAsync(1000, -20.0m));

        Assert.False(r.Ok);
        Assert.DoesNotContain("*202*1*", Escritas(antes));
        Assert.Equal(["*213*0*", "*201*1000*"], Comandos(antes));
        Assert.Equal(EstadoGerador.Desligado, _gerador.Estado);
        Assert.False(_gerador.TalvezLigado);
        Assert.Empty(_simulador.LinhaDoTempoGerador);
    }

    [Fact]
    public async Task Um_parametro_que_o_aparelho_deixa_diferente_e_o_202_1_nao_sai()
    {
        _simulador.EcosDiferentes["*200*-20.0*"] = "#200#-10.0#";
        await Conectar();
        var antes = _simulador.Escritas.Count;

        var r = await Rodar(_gerador.TocarTomAsync(1000, -20.0m));

        Assert.False(r.Ok);
        Assert.Contains("O gerador não foi ligado", r.Mensagem);
        Assert.Equal(["*213*0*", "*201*1000*", "*200*-20.0*"], Comandos(antes));
        Assert.False(_gerador.TalvezLigado);
    }

    [Fact]
    public async Task Sem_eco_no_202_1_rele_e_toca()
    {
        _simulador.Silenciar.Add("*202*1*");
        await Conectar();
        var antes = _simulador.Escritas.Count;

        await TocarTom();

        Assert.Equal(["*202*1*", "#202#"], Escritas(antes).Skip(3).ToList());
        await AndarAteOPrazo();
    }

    [Fact]
    public async Task Tocando_um_segundo_Tocar_e_recusado_sem_enviar()
    {
        await Conectar();
        await TocarTom();
        var antes = _simulador.Escritas.Count;

        var r = await _gerador.TocarSweepAsync(20, 200, -30.0m, 1);

        Assert.False(r.Ok);
        Assert.Equal(GeradorSinal.MsgEmUso, r.Motivo);
        Assert.Equal(antes, _simulador.Escritas.Count);
        await Rodar(_gerador.PararAsync());
    }

    [Fact]
    public async Task Com_uma_operacao_de_memoria_em_andamento_Tocar_e_recusado()
    {
        await Conectar();
        Assert.True((await _copia.IniciarAsync(new OperacaoPendente("teste", "0", "Operação de teste"))).Ok);
        var antes = _simulador.Escritas.Count;

        var r = await _gerador.TocarTomAsync(1000, -20.0m);

        Assert.False(r.Ok);
        Assert.Equal(CopiaAntes.MsgOutraOperacao, r.Motivo);
        Assert.Equal(antes, _simulador.Escritas.Count);
        await _copia.ConcluirAsync();
    }

    [Fact]
    public async Task Lendo_o_aparelho_Tocar_e_recusado()
    {
        await Conectar();
        _simulador.AtrasoLeitura = TimeSpan.FromMilliseconds(25); // a leitura espera o relógio falso
        var leitura = _leitura.LerAsync();
        await AndarAte(() => _estado.Lendo, passos: 1);
        var antes = _simulador.Escritas.Count;

        var r = await _gerador.TocarTomAsync(1000, -20.0m);

        Assert.False(r.Ok);
        Assert.Equal(Escritor.MsgLendo, r.Motivo);
        Assert.Empty(Comandos(antes));
        _simulador.AtrasoLeitura = TimeSpan.Zero;
        await Rodar(leitura);
    }

    [Fact]
    public async Task Sem_conexao_Tocar_e_recusado_e_Parar_nao_trava()
    {
        await _chaves.Carregada;

        var r = await _gerador.TocarTomAsync(1000, -20.0m);
        var p = await Rodar(_gerador.PararAsync());

        Assert.False(r.Ok);
        Assert.Equal(Sessao.MotivoSemConexao, r.Motivo);
        Assert.False(p.Ok);
        Assert.Equal(GeradorSinal.MsgSemConexaoParaDesligar, p.Mensagem);
        Assert.Empty(_simulador.Escritas);
    }

    [Fact]
    public async Task Parar_antes_do_202_1_cancela_e_o_202_1_nao_sai()
    {
        await Conectar();
        var antes = _simulador.Escritas.Count;
        var tocar = _gerador.TocarTomAsync(1000, -20.0m);
        await AndarAte(() => _simulador.Escritas.Contains("*213*0*"), passos: 1);

        var parar = _gerador.PararAsync();
        var r = await Rodar(tocar);
        var p = await Rodar(parar);

        Assert.False(r.Ok);
        Assert.Equal(GeradorSinal.MsgCancelado, r.Mensagem);
        Assert.True(p.Ok);
        Assert.DoesNotContain("*202*1*", Escritas(antes));
        Assert.Contains("*202*0*", Escritas(antes));
        Assert.Equal(EstadoGerador.Desligado, _gerador.Estado);
        Assert.False(_gerador.TalvezLigado);
    }

    [Fact]
    public async Task Desligar_a_chave_com_o_tom_tocando_o_prazo_ainda_desliga()
    {
        await Conectar();
        await TocarTom();

        Assert.True((await _chaves.DefinirAsync(Chave.Gerador, false, OrigemComando.Tela)).Ok);
        Assert.Null(_sessao.Conferir("*202*0*"));
        Assert.Equal(Sessao.MotivoChaveDesligada, _sessao.Conferir("*202*1*"));
        await AndarAteOPrazo();

        Assert.False(_simulador.GeradorLigado);
        Assert.Equal(GeradorSinal.MsgPrazo, _gerador.Mensagem);
    }

    [Fact]
    public async Task Sem_eco_e_sem_releitura_do_202_1_Tocar_falha_e_desliga()
    {
        await Conectar();
        _simulador.Silenciar.Add("*202*1*");
        _simulador.Silenciar.Add("#202#");
        var antes = _simulador.Escritas.Count;

        var r = await Rodar(_gerador.TocarTomAsync(1000, -20.0m));
        await AndarAte(() => _gerador.Estado == EstadoGerador.Desligado && !_gerador.TalvezLigado);

        Assert.False(r.Ok);
        Assert.Equal(GeradorSinal.MsgNaoLigou, r.Mensagem);
        var escritas = Escritas(antes);
        Assert.True(escritas.IndexOf("*202*0*") > escritas.IndexOf("*202*1*"), string.Join(" ", escritas));
        Assert.False(_simulador.GeradorLigado);
        Assert.False(_gerador.TalvezLigado);
    }

    [Fact]
    public async Task Ao_sair_do_app_desliga_se_estiver_tocando_e_nao_envia_nada_desligado()
    {
        await Conectar();
        var antes = _simulador.Escritas.Count;

        await Rodar(_gerador.DesligarAoSairAsync()); // desligado: nada sai
        Assert.Equal(antes, _simulador.Escritas.Count);

        await TocarTom();
        antes = _simulador.Escritas.Count;
        await Rodar(_gerador.DesligarAoSairAsync());

        Assert.Equal(["*202*0*"], Escritas(antes));
        Assert.False(_simulador.GeradorLigado);
        Assert.False(_gerador.TalvezLigado);
        Assert.Equal(EstadoGerador.Desligado, _gerador.Estado);
    }

    [Fact]
    public async Task Prazo_vencendo_durante_um_Ler_de_novo_desliga_uma_vez_e_o_fim_mostra_202_0()
    {
        await Conectar();
        await TocarTom();
        Retrato? fim = null;
        _leitura.Terminou += r => fim = r;
        var antes = _simulador.Escritas.Count;

        // As leituras passam a esperar o relógio falso (25 ms cada, ~2,5 s a leitura inteira); o
        // prazo vence no meio dela.
        _relogio.Avancar(_gerador.Restante!.Value - TimeSpan.FromMilliseconds(2300));
        _simulador.AtrasoLeitura = TimeSpan.FromMilliseconds(25);
        var leitura = _leitura.LerAsync();
        var retrato = await Rodar(leitura);
        _simulador.AtrasoLeitura = TimeSpan.Zero;
        await AndarAte(() => fim is not null && _gerador.Estado == EstadoGerador.Desligado);
        await Andar(TimeSpan.FromMilliseconds(500));

        Assert.NotNull(retrato);
        Assert.Equal(["*202*0*"], Comandos(antes));
        Assert.Equal(GeradorSinal.MsgPrazo, _gerador.Mensagem);
        Assert.False(fim!.Item("202")!.Valor.Ligado);
        Assert.False(_estado.Valor("202")!.Ligado);
        Assert.False(_simulador.GeradorLigado);
    }

    [Fact]
    public async Task O_202_sem_leitura_com_o_aparelho_ligado_desliga_por_seguranca()
    {
        _simulador.Definir("202", null, "1");
        _simulador.Silenciar.Add("#202#"); // só durante a leitura

        await Conectar(ligarChave: false);
        _simulador.Silenciar.Remove("#202#");

        Assert.Contains("*202*0*", Comandos());
        Assert.False(_simulador.GeradorLigado);
        Assert.False(_gerador.TalvezLigado);
        Assert.False(_gerador.Alarme);
        Assert.False(_estado.Valor("202")!.Ligado);
    }

    [Fact]
    public async Task Parar_depois_do_202_1_e_antes_da_confirmacao_cancela_sem_segundo_desligar()
    {
        await Conectar();
        var antes = _simulador.Escritas.Count;
        var tocar = _gerador.TocarTomAsync(1000, -20.0m);
        await AndarAte(() => _simulador.Escritas.Contains("*202*1*"));
        Assert.False(_simulador.Notificacoes.Any(n => n.StartsWith("#202#1#", StringComparison.Ordinal)), "o eco do *202*1* ainda não chegou");

        var parar = _gerador.PararAsync();
        var r = await Rodar(tocar);
        var p = await Rodar(parar);

        Assert.False(r.Ok);
        Assert.Equal(GeradorSinal.MsgCancelado, r.Mensagem);
        Assert.True(p.Ok);
        Assert.Equal(EstadoGerador.Desligado, _gerador.Estado);
        Assert.False(_gerador.TalvezLigado);
        Assert.Null(_gerador.Restante);
        Assert.False(_estado.Valor("202")!.Ligado); // o 1 atrasado não volta ao espelho

        // Nada armado: o prazo não manda outro *202*0*.
        _relogio.Avancar(TimeSpan.FromSeconds(31));
        await Andar(TimeSpan.FromMilliseconds(500));
        Assert.Equal(1, Comandos(antes).Count(c => c == "*202*0*"));
        Assert.DoesNotContain("#202#", Escritas(antes));
        Assert.False(_simulador.GeradorLigado);
    }

    [Fact]
    public async Task A_leitura_que_lanca_ao_reconectar_encerra_a_tentativa_e_depois_de_3_vem_o_alarme()
    {
        await Conectar();
        await TocarTom();
        var falhar = true;
        _leitor.Avancou += _ =>
        {
            if (falhar)
                throw new InvalidOperationException("leitura que lança (teste)");
        };
        // Sem confirmação do desligar: cada tentativa falha e o laço segue.
        _simulador.Silenciar.Add("*202*0*");
        _simulador.Silenciar.Add("#202#");
        var conexoes = _simulador.Conexoes;

        _simulador.Derrubar();
        await AndarAte(() => _gerador.Alarme && _gerador.Estado == EstadoGerador.SemResposta, passos: 5000);
        falhar = false;

        Assert.Equal(conexoes + GeradorSinal.TentativasDeReconexao, _simulador.Conexoes);
        Assert.Equal(GeradorSinal.MsgAlarme, _gerador.Mensagem);
        Assert.Contains("*202*0*", Comandos()); // o desligar por segurança saiu na leitura que lançou
    }

    // ----------------------------------------------------------------- trava

    [Theory]
    [InlineData("*200*-20.0*")]
    [InlineData("*201*1000*")]
    [InlineData("*210*20*")]
    [InlineData("*211*200*")]
    [InlineData("*212*-30.0*")]
    [InlineData("*213*0*")]
    [InlineData("*213*3*")]
    [InlineData("*202*1*")]
    public async Task Com_a_chave_Gerador_cada_comando_do_gerador_passa_na_forma_exata(string quadro)
    {
        await Conectar(ligarChave: false);
        Assert.Equal(Sessao.MotivoChaveDesligada, _sessao.Conferir(quadro));
        Assert.True((await _chaves.DefinirAsync(Chave.Gerador, true, OrigemComando.Tela)).Ok);

        var r = await _sessao.EnviarAsync(quadro);

        Assert.True(r.Enviado, r.Motivo);
        Assert.Contains(quadro, _simulador.Escritas);
        if (quadro == "*202*1*")
            Assert.True((await _sessao.EnviarAsync("*202*0*")).Enviado); // e desliga já
        await Andar(TimeSpan.FromMilliseconds(200)); // os ecos
    }

    [Fact]
    public async Task O_202_0_passa_sem_a_chave_mas_nao_sem_conexao_nem_em_so_leitura()
    {
        await _chaves.Carregada;
        Assert.Equal(Sessao.MotivoSemConexao, _sessao.Conferir("*202*0*"));

        await Conectar(ligarChave: false);
        Assert.False(_chaves.Ligada(Chave.Gerador));
        var r = await _sessao.EnviarAsync("*202*0*");

        Assert.True(r.Enviado, r.Motivo);
        await Andar(TimeSpan.FromMilliseconds(200));
    }

    [Theory]
    [InlineData("*251*0*")]
    [InlineData("*252*1*")]
    [InlineData("*002*NOVO*")]
    [InlineData("*004*")]
    [InlineData("*242*")]
    [InlineData("*001*AB12*CD34*")]
    public async Task Os_comandos_da_7b_continuam_nao_liberados(string quadro)
    {
        await Conectar();

        var r = await _sessao.EnviarAsync(quadro);

        Assert.False(r.Enviado);
        Assert.Equal(Sessao.MotivoNaoLiberado, r.Motivo);
    }

    [Theory]
    [InlineData("*202*0*", true)]
    [InlineData("*202*1*", true)]
    [InlineData("*213*2*", true)]
    [InlineData("*200*0.0*", true)]
    [InlineData("*200*-60.0*", true)]
    [InlineData("*201*22000*", true)]
    [InlineData("*202*2*", false)]
    [InlineData("*202*1", false)]
    [InlineData("*200*-20*", false)]
    [InlineData("*200*-60.1*", false)]
    [InlineData("*200*0.5*", false)]
    [InlineData("*201*9*", false)]
    [InlineData("*201*1000.0*", false)]
    [InlineData("*213*4*", false)]
    [InlineData("*202*0*1*", false)]
    [InlineData("#202#", false)]
    public void O_catalogo_reconhece_so_a_forma_exata_do_gerador(string quadro, bool reconhece)
    {
        Assert.Equal(reconhece, Catalogo.ComandoDeGerador(quadro) is not null);
    }
}
