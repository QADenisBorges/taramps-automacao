using System.Text.Json;
using BgsControle.Nucleo;
using BgsControle.Nucleo.Leitura;
using BgsControle.Nucleo.Registro;
using BgsControle.Simulador;

namespace BgsControle.Telas.Testes;

/// <summary>
/// A tela de conexão contra o processador simulado, sem MAUI: a matriz da história 4 e a
/// leitura automática ao entrar em Conectado.
/// </summary>
public sealed class ConexaoViewModelTestes
{
    private static readonly TemposSessao Tempos = new(
        Scan: TimeSpan.FromSeconds(1),
        TentativaConexao: TimeSpan.FromSeconds(1),
        PausaEntreTentativas: TimeSpan.FromMilliseconds(1),
        RespostaSenha: TimeSpan.FromSeconds(2),
        PrazoSenha: TimeSpan.FromSeconds(60));

    // O voltímetro nunca responde no formato: o prazo curto deixa o teste rápido.
    private static readonly TemposLeitura LeituraRapida = new(TimeSpan.Zero, TimeSpan.FromMilliseconds(100));

    private readonly RelogioFalso _relogio = new();
    private readonly ProcessadorSimulado _simulador;
    private readonly CofreMemoria _cofre = new();
    private readonly LogBle _log = new();
    private readonly Sessao _sessao;
    private readonly LeitorCompleto _leitor;
    private readonly LeituraAutomatica _leitura;
    private readonly DespachanteFila _tela = new();
    private readonly ArquivosFalso _arquivos = new();
    private readonly PreparoFalso _preparo = new();
    private readonly ConexaoViewModel _vm;

    public ConexaoViewModelTestes()
    {
        // A sessão e o leitor esperam em tempo real e o relógio falso não anda enquanto conectam:
        // a senha e as leituras respondem na hora (o ritmo real tem um teste no SimuladorTestes).
        _simulador = new ProcessadorSimulado(_relogio) { AtrasoSenha = TimeSpan.Zero, AtrasoLeitura = TimeSpan.Zero };
        _sessao = new Sessao(_simulador, _cofre, _log, Tempos);
        _leitor = new LeitorCompleto(_sessao, _log, LeituraRapida);
        _leitura = new LeituraAutomatica(_sessao, _leitor);
        _vm = new ConexaoViewModel(_sessao, _log, _leitura, _tela, _arquivos, _preparo, _relogio);
    }

    /// <summary>Quantas leituras completas passaram pelo simulador (cada uma pergunta o master uma vez).</summary>
    private int LeiturasNoSimulador => _simulador.Escritas.Count(e => e == "#080#");

    private async Task PedirEEnviarSenha(string senha)
    {
        await _vm.Conectar.ExecutarAsync();
        await _tela.EsvaziarAsync();
        Assert.True(_vm.PedindoSenha);
        Assert.Equal(Sessao.MsgPedeSenha, _vm.Mensagem);
        Assert.Equal(0, _simulador.Procuras); // sem senha guardada, pede antes de procurar

        _vm.SenhaDigitada = senha;
        await _vm.EnviarSenha.ExecutarAsync();
        Assert.Equal("", _vm.SenhaDigitada);
    }

    private async Task EsperarLeituraSalva(int gravacoes)
    {
        await Apoio.Esperar(() => _arquivos.Gravacoes >= gravacoes, () => $"gravações {_arquivos.Gravacoes}; {_vm.ResumoLeitura}");
        await Apoio.Esperar(() => !_vm.LendoAgora);
        await _tela.EsvaziarAsync();
        Assert.Equal(gravacoes, _arquivos.Gravacoes);
    }

    /// <summary>
    /// Prende a leitura de número <paramref name="leitura"/> logo depois da primeira consulta: o
    /// leitor fica parado no Avancou (a tela já recebeu o avanço) até <c>Soltar</c>.
    /// </summary>
    private (Task Presa, Action Soltar) PrenderNaPrimeiraConsulta(int leitura = 1)
    {
        var solta = new ManualResetEventSlim();
        var presa = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var prendeu = 0;
        _leitura.Avancou += _ =>
        {
            if (_leitura.Iniciadas != leitura || Interlocked.Exchange(ref prendeu, 1) == 1)
                return;
            presa.SetResult();
            solta.Wait(TimeSpan.FromSeconds(10));
        };
        return (presa.Task.WaitAsync(TimeSpan.FromSeconds(10)), solta.Set);
    }

    /// <summary>Prende a 1ª leitura no aviso de fim: ela acabou, mas ainda não soltou.</summary>
    private (Task Presa, Action Soltar) PrenderNoFimDaPrimeiraLeitura()
    {
        var solta = new ManualResetEventSlim();
        var presa = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var fins = 0;
        _leitura.Terminou += _ =>
        {
            if (Interlocked.Increment(ref fins) != 1)
                return;
            presa.SetResult();
            solta.Wait(TimeSpan.FromSeconds(10));
        };
        return (presa.Task.WaitAsync(TimeSpan.FromSeconds(10)), solta.Set);
    }

    // ----------------------------------------------------------------- caminho feliz

    [Fact]
    public async Task Caminho_feliz_conecta_le_84_de_84_e_salva_o_retrato()
    {
        await PedirEEnviarSenha("ab12");
        await EsperarLeituraSalva(1);

        Assert.Equal(EstadoSessao.Conectado, _sessao.Estado);
        Assert.Equal("Conectado · firmware R003", _vm.Resumo);
        Assert.Equal("R003", _vm.Firmware);
        Assert.False(_vm.SoLeitura);
        Assert.Equal("AB12", _cofre.Senha);

        Assert.Equal("84 de 84", _vm.PlacarMarco1);
        Assert.Equal("Extras: 14 de 15", _vm.PlacarExtras); // o voltímetro responde "ERRO 0"
        Assert.StartsWith("Leitura completa.", _vm.ResumoLeitura);
        Assert.True(_vm.MostrarLeitura);
        Assert.True(_vm.PodeLerDeNovo);
        Assert.Contains("MARCO 1 · 84 de 84", _vm.ListaLeitura);
        Assert.Contains("231 nome da memória 1 · — · \"SPL\" · 231#SPL", _vm.ListaLeitura);
        Assert.Contains("ÓRFÃOS · 1", _vm.ListaLeitura);

        // O retrato salvo pelo IArquivosApp.
        using var json = JsonDocument.Parse(_arquivos.Conteudo(ConexaoViewModel.ArquivoRetrato)!);
        var raiz = json.RootElement;
        Assert.Equal("completo", raiz.GetProperty("status").GetString());
        Assert.Equal("R003", raiz.GetProperty("firmware").GetString());
        Assert.Equal(84 + 14, raiz.GetProperty("lidos").GetArrayLength());
        Assert.Equal("ERRO 0", raiz.GetProperty("orfaos")[0].GetString());

        // Só a senha e as consultas do catálogo, na ordem: nenhuma escrita.
        Assert.Equal(["*000*AB12*", .. Catalogo.Consultas.Select(c => c.Texto)], _simulador.Escritas);
        Assert.Empty(_tela.Erros);
    }

    [Fact]
    public async Task Ler_de_novo_faz_outra_leitura_e_salva_de_novo()
    {
        await PedirEEnviarSenha("AB12");
        await EsperarLeituraSalva(1);

        await _vm.LerDeNovo.ExecutarAsync();
        await EsperarLeituraSalva(2);

        Assert.Equal(2, LeiturasNoSimulador);
        Assert.Equal(2, _leitura.Iniciadas);
        Assert.Equal("84 de 84", _vm.PlacarMarco1);
    }

    // ----------------------------------------------------------------- uma leitura por entrada

    [Fact]
    public async Task Varios_Mudou_em_Conectado_dao_uma_leitura_so()
    {
        await PedirEEnviarSenha("AB12");
        // Durante a leitura automática...
        for (var i = 0; i < 5; i++)
            Apoio.DispararMudou(_sessao);
        await EsperarLeituraSalva(1);

        // ...e depois dela, ainda em Conectado.
        for (var i = 0; i < 5; i++)
            Apoio.DispararMudou(_sessao);
        await Task.Delay(200);
        await _tela.EsvaziarAsync();

        Assert.Equal(EstadoSessao.Conectado, _sessao.Estado);
        Assert.Equal(1, _leitura.Iniciadas);
        Assert.Equal(1, LeiturasNoSimulador);
        Assert.Equal(1, _arquivos.Gravacoes);
    }

    [Fact]
    public async Task Nao_comeca_outra_leitura_enquanto_uma_roda()
    {
        var (presa, soltar) = PrenderNaPrimeiraConsulta();
        await PedirEEnviarSenha("AB12");
        await presa;

        Assert.True(_leitura.Lendo);
        Assert.Null(await _leitura.LerAsync());
        await _vm.LerDeNovo.ExecutarAsync();
        await _tela.EsvaziarAsync();
        await _vm.LerDeNovo.ExecutarAsync();
        Assert.Equal(1, _leitura.Iniciadas);

        soltar();
        await EsperarLeituraSalva(1);
        Assert.Equal(1, _leitura.Iniciadas);
        Assert.Equal(1, LeiturasNoSimulador);
    }

    [Fact]
    public async Task Entrada_em_Conectado_antes_de_a_leitura_anterior_soltar_le_quando_ela_solta()
    {
        var (presa, soltar) = PrenderNoFimDaPrimeiraLeitura();
        await PedirEEnviarSenha("AB12");
        await presa;
        Assert.True(_leitura.Lendo);

        // Desconecta e conecta de novo enquanto isso: a leitura desta entrada fica pendente.
        await _vm.Desconectar.ExecutarAsync();
        await _vm.Conectar.ExecutarAsync();
        Assert.Equal(EstadoSessao.Conectado, _sessao.Estado);
        Assert.Equal(1, _leitura.Iniciadas);

        soltar();
        await EsperarLeituraSalva(2);
        Assert.Equal(2, _leitura.Iniciadas);
        Assert.Equal(2, LeiturasNoSimulador);
        Assert.Equal("84 de 84", _vm.PlacarMarco1);
    }

    [Fact]
    public async Task Leitura_pendente_e_esquecida_se_a_sessao_desconecta_antes()
    {
        var (presa, soltar) = PrenderNoFimDaPrimeiraLeitura();
        await PedirEEnviarSenha("AB12");
        await presa;

        await _vm.Desconectar.ExecutarAsync();
        await _vm.Conectar.ExecutarAsync(); // fica pendente
        await _vm.Desconectar.ExecutarAsync(); // e desconecta de novo

        soltar();
        await Apoio.Esperar(() => !_leitura.Lendo);
        await Task.Delay(200);
        Assert.Equal(1, _leitura.Iniciadas);
        Assert.Equal(1, LeiturasNoSimulador);
    }

    [Fact]
    public async Task Desconectar_e_conectar_de_novo_faz_uma_leitura_nova()
    {
        await PedirEEnviarSenha("AB12");
        await EsperarLeituraSalva(1);

        await _vm.Desconectar.ExecutarAsync();
        Assert.Equal(EstadoSessao.Desconectado, _sessao.Estado);
        Assert.False(_simulador.Conectado);

        // A senha ficou guardada: conecta sem pedir.
        await _vm.Conectar.ExecutarAsync();
        Assert.Equal(EstadoSessao.Conectado, _sessao.Estado);
        await EsperarLeituraSalva(2);

        Assert.Equal(2, _leitura.Iniciadas);
        Assert.Equal(2, LeiturasNoSimulador);
        Assert.Equal("84 de 84", _vm.PlacarMarco1);
    }

    // ----------------------------------------------------------------- andamento e falhas da leitura

    [Fact]
    public async Task Durante_a_leitura_o_placar_mostra_o_parcial()
    {
        var (presa, soltar) = PrenderNaPrimeiraConsulta();
        await PedirEEnviarSenha("AB12");
        await presa;
        await _tela.EsvaziarAsync();

        Assert.True(_vm.LendoAgora);
        Assert.True(_vm.MostrarLeitura);
        Assert.False(_vm.PodeLerDeNovo);
        Assert.Equal("1 de 84", _vm.PlacarMarco1);
        Assert.Equal($"Lendo… 1 de {Catalogo.Consultas.Count} consultas", _vm.ResumoLeitura);

        soltar();
        await EsperarLeituraSalva(1);
        Assert.Equal("84 de 84", _vm.PlacarMarco1);
    }

    [Fact]
    public async Task Ler_de_novo_limpa_o_aviso_de_falha_ao_salvar()
    {
        _arquivos.Falhar = true;
        await PedirEEnviarSenha("AB12");
        await Apoio.Esperar(() => _vm.ResumoLeitura.Contains("Não consegui salvar"), () => _vm.ResumoLeitura);
        _arquivos.Falhar = false;

        var (presa, soltar) = PrenderNaPrimeiraConsulta(leitura: 2);
        var lerDeNovo = _vm.LerDeNovo.ExecutarAsync();
        await presa;
        await _tela.EsvaziarAsync();

        // No começo da leitura nova, o aviso antigo some.
        Assert.True(_vm.LendoAgora);
        Assert.StartsWith("Lendo… 1 de", _vm.ResumoLeitura);
        Assert.DoesNotContain("Não consegui salvar", _vm.ResumoLeitura);

        soltar();
        await lerDeNovo;
        await EsperarLeituraSalva(1);
        Assert.StartsWith("Leitura completa.", _vm.ResumoLeitura);
        Assert.DoesNotContain("Não consegui salvar", _vm.ResumoLeitura);
    }

    [Fact]
    public async Task Leitura_que_lanca_avisa_que_falhou_e_Ler_de_novo_volta_a_ler()
    {
        var falhar = 1;
        _leitor.Avancou += _ =>
        {
            if (Interlocked.Exchange(ref falhar, 0) == 1)
                throw new InvalidOperationException("falha de teste");
        };

        await PedirEEnviarSenha("AB12");
        await Apoio.Esperar(() => _vm.ResumoLeitura.Contains("A leitura falhou (InvalidOperationException)."),
            () => _vm.ResumoLeitura);
        await Apoio.Esperar(() => !_leitura.Lendo);
        await _tela.EsvaziarAsync();

        Assert.False(_vm.LendoAgora);
        Assert.True(_vm.PodeLerDeNovo);
        Assert.Equal(0, _arquivos.Gravacoes);

        await _vm.LerDeNovo.ExecutarAsync();
        await EsperarLeituraSalva(1);
        Assert.Equal(2, _leitura.Iniciadas);
        Assert.Equal("84 de 84", _vm.PlacarMarco1);
        Assert.DoesNotContain("A leitura falhou", _vm.ResumoLeitura);
    }

    // ----------------------------------------------------------------- senha errada

    [Fact]
    public async Task Senha_errada_avisa_solta_a_conexao_e_nao_escreve_mais_nada()
    {
        await PedirEEnviarSenha("ZZZZ");
        await _tela.EsvaziarAsync();

        Assert.StartsWith(Sessao.MsgSenhaErrada, _vm.Mensagem);
        Assert.True(_vm.PedindoSenha);
        Assert.False(_simulador.Conectado); // o app soltou a conexão
        Assert.Equal(["*000*ZZZZ*"], _simulador.Escritas);
        Assert.Equal(0, _leitura.Iniciadas);
        Assert.Null(_cofre.Senha);
        Assert.False(_vm.MostrarLeitura);
    }

    [Fact]
    public async Task Depois_do_ERRO_os_29_s_do_simulador_passam_e_a_sessao_segue_pedindo_a_senha()
    {
        await PedirEEnviarSenha("ZZZZ");

        _relogio.Avancar(ProcessadorSimulado.QuedaAposErroReal);
        await Task.Delay(50);
        await _tela.EsvaziarAsync();

        Assert.Equal(EstadoSessao.PedindoSenha, _sessao.Estado);
        Assert.True(_vm.PedindoSenha);
        Assert.StartsWith(Sessao.MsgSenhaErrada, _vm.Mensagem);
        // O app já soltou a conexão: a queda de 29 s do simulador não chega à sessão. A queda
        // em si é testada direto no simulador (SimuladorTestes).
        Assert.Equal(0, _simulador.Quedas);

        // E a senha certa ainda conecta, numa conexão nova.
        _vm.SenhaDigitada = "AB12";
        await _vm.EnviarSenha.ExecutarAsync();
        Assert.Equal(EstadoSessao.Conectado, _sessao.Estado);
        await EsperarLeituraSalva(1);
        Assert.Equal("84 de 84", _vm.PlacarMarco1);
    }

    // ----------------------------------------------------------------- Bluetooth não está pronto

    [Theory]
    [InlineData(Sessao.MsgSemPermissao)]
    [InlineData(Sessao.MsgBluetoothDesligado)]
    public async Task Sem_permissao_ou_com_o_Bluetooth_desligado_mostra_a_mensagem_e_nao_procura(string falta)
    {
        _cofre.Senha = "AB12"; // mesmo com a senha guardada
        _preparo.Falta = falta;

        await _vm.Conectar.ExecutarAsync();
        await _tela.EsvaziarAsync();

        Assert.Equal(1, _preparo.Pedidos);
        Assert.Equal(falta, _vm.Mensagem);
        Assert.True(_vm.TemMensagem);
        Assert.Equal(0, _simulador.Procuras);
        Assert.Empty(_simulador.Escritas);
        Assert.Equal(EstadoSessao.Desconectado, _sessao.Estado);
        Assert.Equal("Desconectado", _vm.Resumo);
        Assert.True(_vm.PodeConectar);
        Assert.False(_vm.Ocupado);
    }

    // ----------------------------------------------------------------- leitura interrompida e falha ao salvar

    [Fact]
    public async Task Queda_no_meio_da_leitura_salva_o_retrato_incompleto()
    {
        _simulador.CairNaEscrita = 11; // a senha e 9 consultas passam; a 10ª derruba

        await PedirEEnviarSenha("AB12");
        await EsperarLeituraSalva(1);

        Assert.Equal(EstadoSessao.Desconectado, _sessao.Estado);
        Assert.Equal(Sessao.MsgCaiu, _vm.Mensagem);
        Assert.StartsWith("Leitura incompleta:", _vm.ResumoLeitura);
        Assert.Equal("9 de 84", _vm.PlacarMarco1);
        Assert.False(_vm.PodeLerDeNovo);
        using var json = JsonDocument.Parse(_arquivos.Conteudo(ConexaoViewModel.ArquivoRetrato)!);
        Assert.Equal("incompleto", json.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Falha_ao_salvar_o_retrato_aparece_no_resumo()
    {
        _arquivos.Falhar = true;

        await PedirEEnviarSenha("AB12");
        await Apoio.Esperar(() => _vm.ResumoLeitura.Contains("Não consegui salvar o retrato (IOException)."),
            () => _vm.ResumoLeitura);
        await _tela.EsvaziarAsync();

        Assert.False(_vm.LendoAgora);
        Assert.StartsWith("Leitura completa.", _vm.ResumoLeitura);
        Assert.Equal("84 de 84", _vm.PlacarMarco1);
    }
}
