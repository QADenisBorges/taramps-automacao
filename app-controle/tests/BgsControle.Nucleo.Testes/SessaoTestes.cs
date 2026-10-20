using BgsControle.Nucleo.Enlace;
using BgsControle.Nucleo.Registro;

namespace BgsControle.Nucleo.Testes;

/// <summary>A matriz de cenários da história 1, com transporte e cofre falsos.</summary>
public sealed class SessaoTestes
{
    private static readonly TemposSessao Rapidos = new(
        Scan: TimeSpan.FromSeconds(1),
        TentativaConexao: TimeSpan.FromSeconds(1),
        PausaEntreTentativas: TimeSpan.FromMilliseconds(1),
        RespostaSenha: TimeSpan.FromMilliseconds(300),
        PrazoSenha: TimeSpan.FromMilliseconds(300));

    private readonly TransporteFalso _transporte = new();
    private readonly CofreFalso _cofre = new();
    private readonly LogBle _log = new();
    private readonly List<EstadoSessao> _estados = [];
    private readonly Sessao _sessao;

    public SessaoTestes()
    {
        _sessao = new Sessao(_transporte, _cofre, _log, Rapidos);
        _sessao.Mudou += () =>
        {
            lock (_estados)
            {
                if (_estados.Count == 0 || _estados[^1] != _sessao.Estado)
                    _estados.Add(_sessao.Estado);
            }
        };
    }

    private void SoQuadrosDeSenha() =>
        Assert.All(_transporte.Escritas, e => Assert.Matches(@"^\*000\*[0-9A-Z]{4}\*$", e));

    private void LogSemSenha(params string[] senhas)
    {
        foreach (var linha in _log.Linhas)
            foreach (var s in senhas)
                Assert.DoesNotContain(s, linha, StringComparison.OrdinalIgnoreCase);
    }

    private async Task Esperar(Func<bool> condicao)
    {
        for (var i = 0; i < 200 && !condicao(); i++)
            await Task.Delay(10);
        Assert.True(condicao(), $"estado {_sessao.Estado}, mensagem {_sessao.Mensagem}");
    }

    // ----------------------------------------------------------------- caminho feliz

    [Fact]
    public async Task Caminho_feliz_mostra_R003_e_guarda_a_senha()
    {
        await _sessao.ConectarAsync();
        Assert.Equal(EstadoSessao.PedindoSenha, _sessao.Estado);
        Assert.Equal(0, _transporte.Procuras); // pede a senha antes de conectar
        Assert.False(_sessao.SegurandoConexao);

        await _sessao.InformarSenhaAsync(" ab12 ");

        Assert.Equal(EstadoSessao.Conectado, _sessao.Estado);
        Assert.Equal("R003", _sessao.Firmware);
        Assert.False(_sessao.SoLeitura);
        Assert.Null(_sessao.Aviso);
        Assert.Equal("Conectado · firmware R003", _sessao.Resumo);
        Assert.Equal("AB12", _cofre.Senha);
        Assert.Equal(["*000*AB12*"], _transporte.Escritas);
        Assert.False(_transporte.EscreveuSemNotificacao);
        LogSemSenha("AB12");
        Assert.Contains(_log.Linhas, l => l.Contains("*000*••••*"));
    }

    [Fact]
    public async Task Senha_guardada_conecta_sem_pedir()
    {
        _cofre.Senha = "AB12";

        await _sessao.ConectarAsync();

        Assert.Equal(EstadoSessao.Conectado, _sessao.Estado);
        Assert.Equal("R003", _sessao.Firmware);
        Assert.DoesNotContain(EstadoSessao.PedindoSenha, _estados);
        Assert.Equal(
            [EstadoSessao.Procurando, EstadoSessao.Conectando, EstadoSessao.Autenticando, EstadoSessao.Conectado],
            _estados);
        LogSemSenha("AB12");
    }

    [Fact]
    public async Task Erro_do_keystore_conta_como_sem_senha()
    {
        _cofre.FalharAoLer = true;

        await _sessao.ConectarAsync();

        Assert.Equal(EstadoSessao.PedindoSenha, _sessao.Estado);
        Assert.Equal(0, _transporte.Procuras);
        Assert.True(_cofre.Apagadas >= 1);
    }

    [Fact]
    public async Task Senha_guardada_fora_do_formato_e_apagada_e_pedida()
    {
        _cofre.Senha = "12-4";

        await _sessao.ConectarAsync();

        Assert.Equal(EstadoSessao.PedindoSenha, _sessao.Estado);
        Assert.Null(_cofre.Senha);
        Assert.Empty(_transporte.Escritas);
    }

    // ----------------------------------------------------------------- senha errada

    [Fact]
    public async Task Senha_errada_apaga_a_guardada_pede_de_novo_e_so_envia_a_senha()
    {
        _cofre.Senha = "ZZ99";

        await _sessao.ConectarAsync();

        Assert.Equal(EstadoSessao.PedindoSenha, _sessao.Estado);
        Assert.True(_sessao.SegurandoConexao); // a conexão continua para tentar de novo
        Assert.StartsWith("Senha errada", _sessao.Mensagem);
        Assert.Null(_cofre.Senha);
        Assert.Empty(_cofre.Guardadas);
        SoQuadrosDeSenha();

        await _sessao.InformarSenhaAsync("ab12");

        Assert.Equal(EstadoSessao.Conectado, _sessao.Estado);
        Assert.Equal(1, _transporte.Procuras); // não procurou de novo
        Assert.Equal(["*000*ZZ99*", "*000*AB12*"], _transporte.Escritas);
        Assert.Equal("AB12", _cofre.Senha);
        LogSemSenha("ZZ99", "AB12");
    }

    [Fact]
    public async Task Tres_senhas_erradas_desconectam()
    {
        _cofre.Senha = "ZZ99";

        await _sessao.ConectarAsync();
        await _sessao.InformarSenhaAsync("ZZ98");
        Assert.Equal(EstadoSessao.PedindoSenha, _sessao.Estado);
        await _sessao.InformarSenhaAsync("ZZ97");

        Assert.Equal(EstadoSessao.Desconectado, _sessao.Estado);
        Assert.StartsWith("Senha recusada 3 vezes", _sessao.Mensagem);
        Assert.Equal(3, _transporte.Escritas.Count);
        SoQuadrosDeSenha();
        Assert.Equal(1, _transporte.Desconexoes);
        Assert.Null(_cofre.Senha);
        LogSemSenha("ZZ99", "ZZ98", "ZZ97");
    }

    [Fact]
    public async Task Prazo_para_digitar_a_senha_desconecta()
    {
        _cofre.Senha = "ZZ99";

        await _sessao.ConectarAsync();
        Assert.Equal(EstadoSessao.PedindoSenha, _sessao.Estado);

        await Esperar(() => _sessao.Estado == EstadoSessao.Desconectado);
        Assert.Equal(Sessao.MsgPrazoSenha, _sessao.Mensagem);
        Assert.Equal(1, _transporte.Desconexoes);
        SoQuadrosDeSenha();
    }

    [Fact]
    public async Task Eco_da_senha_nao_conta_como_resposta()
    {
        _cofre.Senha = "ZZ99";
        _transporte.AntesDaResposta = ["*000*ZZ99*"];

        await _sessao.ConectarAsync();

        Assert.Equal(EstadoSessao.PedindoSenha, _sessao.Estado);
        Assert.StartsWith("Senha errada", _sessao.Mensagem);
        LogSemSenha("ZZ99");
    }

    // ----------------------------------------------------------------- firmware e produto

    [Fact]
    public async Task Firmware_diferente_entra_em_so_leitura_e_recusa_escrita_sem_envio()
    {
        _cofre.Senha = "AB12";
        _transporte.RespostaCerta = ["#000#PRO2.4MIR004#\r\n"];

        await _sessao.ConectarAsync();

        Assert.Equal(EstadoSessao.Conectado, _sessao.Estado);
        Assert.Equal("R004", _sessao.Firmware);
        Assert.True(_sessao.SoLeitura);
        Assert.Equal("R004 · modo só leitura", _sessao.Resumo);
        Assert.NotNull(_sessao.Aviso);
        Assert.Contains("R004", _sessao.Aviso);

        var escritasAntes = _transporte.Escritas.Count;
        var r = await _sessao.EnviarAsync("*081*0*-12.0*");

        Assert.False(r.Enviado);
        Assert.Equal("modo só leitura", r.Motivo);
        Assert.Equal(escritasAntes, _transporte.Escritas.Count);
        Assert.Equal(["*000*AB12*"], _transporte.Escritas);
    }

    [Fact]
    public async Task Outro_produto_entra_em_so_leitura_com_aviso()
    {
        _cofre.Senha = "AB12";
        _transporte.RespostaCerta = ["#000#XYZR003#\r\n"];

        await _sessao.ConectarAsync();

        Assert.Equal(EstadoSessao.Conectado, _sessao.Estado);
        Assert.Equal("R003", _sessao.Firmware);
        Assert.True(_sessao.SoLeitura);
        Assert.Contains("Produto desconhecido", _sessao.Aviso);
        var r = await _sessao.EnviarAsync("*080*-10.0*");
        Assert.False(r.Enviado);
        Assert.Equal(["*000*AB12*"], _transporte.Escritas);
    }

    // ----------------------------------------------------------------- trava de escrita

    [Theory]
    [InlineData("*081*0*-12.0*")]
    [InlineData("*080*-10.0*")]
    [InlineData("020*3*2.5")]
    [InlineData("*020*3*2.5*")]
    [InlineData("*240*1*")]
    [InlineData("*241*0*")]
    [InlineData("*004*")]
    [InlineData("*242*")]
    [InlineData("*001*AB12*CD34*")]
    [InlineData("*252*1*")]
    [InlineData("*202*1*")]
    [InlineData("231*NOME")]
    [InlineData("*000*AB12*")] // a senha fora do estado Autenticando
    [InlineData("#081#0#\r\n")]
    [InlineData("#081#0")]
    [InlineData("020#15")]
    [InlineData("")]
    public async Task Com_R003_nenhuma_escrita_e_liberada_nesta_historia(string quadro)
    {
        _cofre.Senha = "AB12";
        await _sessao.ConectarAsync();
        Assert.Equal(EstadoSessao.Conectado, _sessao.Estado);
        Assert.False(_sessao.SoLeitura);

        var r = await _sessao.EnviarAsync(quadro);

        Assert.False(r.Enviado);
        Assert.Equal(["*000*AB12*"], _transporte.Escritas);
        LogSemSenha("AB12", "CD34");
    }

    [Theory]
    [InlineData("#080#")]
    [InlineData("#081#0#")]
    [InlineData("#092#3#")]
    [InlineData("020#0")]
    [InlineData("020#14")]
    public async Task Leituras_da_lista_passam_so_com_a_sessao_conectada(string quadro)
    {
        var antes = await _sessao.EnviarAsync(quadro);
        Assert.False(antes.Enviado);
        Assert.Empty(_transporte.Escritas);

        _cofre.Senha = "AB12";
        await _sessao.ConectarAsync();
        var depois = await _sessao.EnviarAsync(quadro);

        Assert.True(depois.Enviado);
        Assert.Equal(["*000*AB12*", quadro], _transporte.Escritas);
    }

    [Fact]
    public async Task Leitura_e_recusada_enquanto_pede_a_senha_de_novo()
    {
        _cofre.Senha = "ZZ99";
        await _sessao.ConectarAsync();
        Assert.Equal(EstadoSessao.PedindoSenha, _sessao.Estado);

        var r = await _sessao.EnviarAsync("#080#");

        Assert.False(r.Enviado);
        SoQuadrosDeSenha();
    }

    // ----------------------------------------------------------------- órfão e fragmentos

    [Fact]
    public async Task Orfao_antes_da_senha_e_ignorado()
    {
        _cofre.Senha = "AB12";
        _transporte.AoConectar = ["#005#120#"];
        _transporte.AntesDaResposta = ["#005#120#\r\n"];

        await _sessao.ConectarAsync();

        Assert.Equal(EstadoSessao.Conectado, _sessao.Estado);
        Assert.Equal("R003", _sessao.Firmware);
        Assert.Contains(_log.Linhas, l => l.Contains("#005#120#") && l.Contains("órfão"));
    }

    [Fact]
    public async Task Orfao_000_antes_da_senha_nunca_e_resposta()
    {
        _cofre.Senha = "AB12";
        _transporte.AoConectar = ["#000#ERRO#\r\n"];

        await _sessao.ConectarAsync();

        Assert.Equal(EstadoSessao.Conectado, _sessao.Estado);
        Assert.Equal("AB12", _cofre.Senha);
    }

    [Fact]
    public async Task Resposta_fragmentada_e_juntada()
    {
        _cofre.Senha = "AB12";
        _transporte.RespostaCerta = ["#000#PRO2.4", "MIR003#"];

        await _sessao.ConectarAsync();

        Assert.Equal(EstadoSessao.Conectado, _sessao.Estado);
        Assert.Equal("R003", _sessao.Firmware);
        Assert.False(_sessao.SoLeitura);
    }

    [Theory]
    [InlineData("#000#ER", "RO#")]
    [InlineData("#000#", "ERRO#\r\n")]
    [InlineData("#000#E", "RRO\r\n")]
    public async Task Erro_cortado_espera_o_resto_e_nao_conta_como_aceito(string a, string b)
    {
        _cofre.Senha = "ZZ99";
        _transporte.RespostaErrada = [a, b];

        await _sessao.ConectarAsync();

        Assert.Equal(EstadoSessao.PedindoSenha, _sessao.Estado);
        Assert.StartsWith("Senha errada", _sessao.Mensagem);
        Assert.Empty(_cofre.Guardadas);
    }

    [Fact]
    public async Task Erro_cortado_sem_o_resto_nao_e_aceito()
    {
        _cofre.Senha = "ZZ99";
        _transporte.RespostaErrada = ["#000#ER"];

        await _sessao.ConectarAsync();

        Assert.Equal(EstadoSessao.Desconectado, _sessao.Estado);
        Assert.Equal(Sessao.MsgSemResposta, _sessao.Mensagem);
        Assert.Empty(_cofre.Guardadas);
    }

    // ----------------------------------------------------------------- formato da senha

    [Theory]
    [InlineData("12a")]
    [InlineData("12345")]
    [InlineData("12-4")]
    [InlineData("")]
    [InlineData("çã12")]
    public async Task Formato_invalido_nao_envia_nada(string digitada)
    {
        await _sessao.ConectarAsync();

        await _sessao.InformarSenhaAsync(digitada);

        Assert.Equal(EstadoSessao.PedindoSenha, _sessao.Estado);
        Assert.Equal(Sessao.MsgSenhaInvalida, _sessao.Mensagem);
        Assert.Equal(0, _transporte.Procuras);
        Assert.Empty(_transporte.Escritas);
    }

    [Fact]
    public async Task Formato_invalido_com_a_conexao_aberta_nao_envia_nada()
    {
        _cofre.Senha = "ZZ99";
        await _sessao.ConectarAsync();

        await _sessao.InformarSenhaAsync("12345");

        Assert.Equal(EstadoSessao.PedindoSenha, _sessao.Estado);
        Assert.Equal(Sessao.MsgSenhaInvalida, _sessao.Mensagem);
        Assert.Equal(["*000*ZZ99*"], _transporte.Escritas);
    }

    // ----------------------------------------------------------------- falhas do enlace

    [Fact]
    public async Task Sem_resposta_em_5_s_desconecta()
    {
        _cofre.Senha = "AB12";
        _transporte.Mudo = true;

        await _sessao.ConectarAsync();

        Assert.Equal(EstadoSessao.Desconectado, _sessao.Estado);
        Assert.Equal(Sessao.MsgSemResposta, _sessao.Mensagem);
        Assert.Equal(1, _transporte.Desconexoes);
        Assert.Equal("AB12", _cofre.Senha); // não apaga: a senha não foi recusada
        Assert.Empty(_cofre.Guardadas);
    }

    [Fact]
    public async Task Processador_nao_encontrado()
    {
        _cofre.Senha = "AB12";
        _transporte.Dispositivo = null;

        await _sessao.ConectarAsync();

        Assert.Equal(EstadoSessao.Desconectado, _sessao.Estado);
        Assert.Equal("Não achei o processador. O app da Taramps está fechado?", _sessao.Mensagem);
        Assert.Equal(0, _transporte.TentativasDeConexao);
        Assert.Empty(_transporte.Escritas);
    }

    [Fact]
    public async Task Conexao_tenta_3_vezes()
    {
        _cofre.Senha = "AB12";
        _transporte.FalhasDeConexao = 2;

        await _sessao.ConectarAsync();

        Assert.Equal(EstadoSessao.Conectado, _sessao.Estado);
        Assert.Equal(3, _transporte.TentativasDeConexao);
    }

    [Fact]
    public async Task Tres_falhas_de_conexao_desistem()
    {
        _cofre.Senha = "AB12";
        _transporte.FalhasDeConexao = 3;

        await _sessao.ConectarAsync();

        Assert.Equal(EstadoSessao.Desconectado, _sessao.Estado);
        Assert.Equal(Sessao.MsgNaoConectou, _sessao.Mensagem);
        Assert.Equal(3, _transporte.TentativasDeConexao);
        Assert.Empty(_transporte.Escritas);
    }

    [Fact]
    public async Task Queda_volta_para_desconectado_e_permite_conectar_de_novo()
    {
        _cofre.Senha = "AB12";
        await _sessao.ConectarAsync();
        Assert.Equal(EstadoSessao.Conectado, _sessao.Estado);

        _transporte.Derrubar();
        await Esperar(() => _sessao.Estado == EstadoSessao.Desconectado);

        Assert.Equal(Sessao.MsgCaiu, _sessao.Mensagem);
        Assert.Null(_sessao.Firmware);
        Assert.False((await _sessao.EnviarAsync("#080#")).Enviado);

        await _sessao.ConectarAsync();
        Assert.Equal(EstadoSessao.Conectado, _sessao.Estado);
    }

    [Fact]
    public async Task Queda_enquanto_pede_a_senha()
    {
        _cofre.Senha = "ZZ99";
        await _sessao.ConectarAsync();
        Assert.Equal(EstadoSessao.PedindoSenha, _sessao.Estado);

        _transporte.Derrubar();
        await Esperar(() => _sessao.Estado == EstadoSessao.Desconectado);

        Assert.Equal(Sessao.MsgCaiu, _sessao.Mensagem);
    }

    [Theory]
    [InlineData(MotivoBluetooth.Desligado, Sessao.MsgBluetoothDesligado)]
    [InlineData(MotivoBluetooth.SemPermissao, Sessao.MsgSemPermissao)]
    [InlineData(MotivoBluetooth.SemSuporte, Sessao.MsgSemBle)]
    public async Task Bluetooth_indisponivel_explica_o_que_liberar(MotivoBluetooth motivo, string mensagem)
    {
        _cofre.Senha = "AB12";
        _transporte.ErroAoProcurar = new BluetoothIndisponivelException(motivo);

        await _sessao.ConectarAsync();

        Assert.Equal(EstadoSessao.Desconectado, _sessao.Estado);
        Assert.Equal(mensagem, _sessao.Mensagem);
        Assert.Empty(_transporte.Escritas);
    }

    // ----------------------------------------------------------------- comandos da tela

    [Fact]
    public async Task Esquecer_a_senha_apaga_e_desconecta()
    {
        _cofre.Senha = "AB12";
        await _sessao.ConectarAsync();

        await _sessao.EsquecerSenhaAsync();

        Assert.Null(_cofre.Senha);
        Assert.Equal(EstadoSessao.Desconectado, _sessao.Estado);
        Assert.Equal(1, _transporte.Desconexoes);

        await _sessao.ConectarAsync();
        Assert.Equal(EstadoSessao.PedindoSenha, _sessao.Estado);
        Assert.False(_sessao.SegurandoConexao);
    }

    [Fact]
    public async Task Desconectar_pela_tela()
    {
        _cofre.Senha = "AB12";
        await _sessao.ConectarAsync();

        await _sessao.DesconectarAsync();

        Assert.Equal(EstadoSessao.Desconectado, _sessao.Estado);
        Assert.Null(_sessao.Mensagem);
        Assert.Equal(1, _transporte.Desconexoes);
    }

    [Fact]
    public async Task Conectar_duas_vezes_nao_abre_duas_conexoes()
    {
        _cofre.Senha = "AB12";
        await Task.WhenAll(_sessao.ConectarAsync(), _sessao.ConectarAsync());

        Assert.Equal(1, _transporte.Procuras);
        Assert.Equal(["*000*AB12*"], _transporte.Escritas);
    }
}
