using System.Text.RegularExpressions;
using BgsControle.Nucleo;
using BgsControle.Nucleo.Aparelho;
using BgsControle.Nucleo.Registro;
using BgsControle.Nucleo.Seguranca;
using BgsControle.Simulador;

namespace BgsControle.Telas.Testes;

/// <summary>O grupo Aparelho de Configurações (história 7b) contra o processador simulado.</summary>
public sealed class AparelhoViewModelTestes : IDisposable
{
    private static readonly Regex Senha = new(@"^\*000\*[0-9A-Z]{4}\*$");

    private readonly Bancada _b = new();
    private readonly AparelhoViewModel _vm;
    private readonly HashSet<string> _pedidos = [];

    public AparelhoViewModelTestes() => _vm = _b.CriarAparelhoViewModel();

    /// <summary>
    /// Critério de aceite: só chegaram a senha, as leituras do catálogo e os comandos da tabela da
    /// trava pedidos pelo teste.
    /// </summary>
    public void Dispose()
    {
        _vm.Dispose();
        Assert.All(_b.Simulador.Escritas, e =>
            Assert.True(Senha.IsMatch(e) || Catalogo.EhLeitura(e) || _pedidos.Contains(e), e));
        Assert.Empty(_b.Tela.Erros);
    }

    private void Pedir(params string[] quadros)
    {
        foreach (var q in quadros)
            _pedidos.Add(q);
    }

    private async Task Ligar(params Chave[] chaves)
    {
        foreach (var c in chaves)
            Assert.True((await _b.Chaves.DefinirAsync(c, true, OrigemComando.Tela)).Ok);
        await _b.Tela.EsvaziarAsync();
    }

    private List<string> Comandos() => _b.Simulador.Escritas.Where(e => !Catalogo.EhLeitura(e) && !Senha.IsMatch(e)).ToList();

    /// <summary>Anda o relógio em passos de 10 ms até a condição valer; a thread da tela anda junto.</summary>
    private async Task AndarAte(Func<bool> condicao)
    {
        for (var i = 0; i < 3000 && !condicao(); i++)
        {
            _b.Relogio.Avancar(TimeSpan.FromMilliseconds(10));
            await Task.Delay(1);
            await _b.Tela.EsvaziarAsync();
        }
        Assert.True(condicao(), "a condição não ficou verdadeira");
        await _b.Tela.EsvaziarAsync();
    }

    // ----------------------------------------------------------------- travas

    [Fact]
    public async Task Sem_conexao_tudo_fica_travado_com_o_motivo()
    {
        Assert.Equal(ControleParametroViewModel.MsgSemConexao, _vm.MotivoTravaRemoteOut);
        Assert.Equal(ControleParametroViewModel.MsgSemConexao, _vm.MotivoTravaResetFabrica);
        Assert.Equal(AparelhoViewModel.SemValor, _vm.TextoRemoteOut);
        Assert.Equal(AparelhoViewModel.SemValor, _vm.TextoBluetoothAudio);

        await _vm.DesligarRemoteOut.ExecutarAsync();

        Assert.Equal("Sem conexão", _vm.Mensagem);
        Assert.Empty(_b.Simulador.Escritas);
    }

    [Fact]
    public async Task Conectado_as_linhas_mostram_o_valor_lido_e_as_chaves_desligadas_travam()
    {
        await _b.Conectar();

        Assert.Equal(AparelhoViewModel.TextoLigado, _vm.TextoRemoteOut); // semente: 252 = 1
        Assert.Equal(AparelhoViewModel.TextoLigado, _vm.TextoBluetoothAudio); // 251 = 1
        Assert.Null(_vm.MotivoTravaRemoteOut);
        Assert.Null(_vm.MotivoTravaBluetoothAudio);
        Assert.Null(_vm.MotivoTravaNome);
        Assert.Equal(ControleParametroViewModel.MsgChaveDesligada, _vm.MotivoTravaResetAudio);
        Assert.Equal(ControleParametroViewModel.MsgChaveDesligada, _vm.MotivoTravaResetFabrica);
        Assert.Equal(ControleParametroViewModel.MsgChaveDesligada, _vm.MotivoTravaSenha);
        Assert.False(_vm.PodeResetAudio);

        await Ligar(Chave.ResetAudio);
        Assert.Null(_vm.MotivoTravaResetAudio);
    }

    // ----------------------------------------------------------------- remote out e Bluetooth de áudio

    [Fact]
    public async Task Remote_out_desliga_na_propria_linha_sem_folha()
    {
        await _b.Conectar();
        Pedir("*252*0*");

        await _b.Rodar(_vm.DesligarRemoteOut.ExecutarAsync());

        Assert.False(_vm.FolhaAberta);
        Assert.Equal(["*252*0*"], Comandos());
        Assert.Equal(AparelhoViewModel.TextoDesligado, _vm.TextoRemoteOut);
        Assert.False(_vm.RemoteOutLigado);
        Assert.Equal("Remote out desligado.", _vm.Mensagem);
    }

    [Fact]
    public async Task Sem_eco_no_252_mostra_o_valor_relido()
    {
        await _b.Conectar();
        _b.Simulador.Silenciar.Add("*252*0*");
        Pedir("*252*0*");

        await _b.Rodar(_vm.DesligarRemoteOut.ExecutarAsync());

        Assert.Contains("#252#", _b.Simulador.Escritas.Skip(_b.Simulador.Escritas.ToList().IndexOf("*252*0*")));
        Assert.Equal(AparelhoViewModel.TextoDesligado, _vm.TextoRemoteOut);
    }

    [Fact]
    public async Task Bluetooth_de_audio_liga_e_mostra_Lig()
    {
        _b.Simulador.Definir("251", null, "0");
        await _b.Conectar();
        Assert.Equal(AparelhoViewModel.TextoDesligado, _vm.TextoBluetoothAudio);
        Pedir("*251*1*");

        await _b.Rodar(_vm.LigarBluetoothAudio.ExecutarAsync());

        Assert.Equal(["*251*1*"], Comandos());
        Assert.Equal(AparelhoViewModel.TextoLigado, _vm.TextoBluetoothAudio);
        Assert.Equal("Bluetooth de áudio ligado.", _vm.Mensagem);
    }

    // ----------------------------------------------------------------- resets

    [Fact]
    public async Task Reset_de_audio_com_a_chave_desligada_nada_sai()
    {
        await _b.Conectar();

        await _vm.ResetAudio.ExecutarAsync();

        Assert.False(_vm.FolhaAberta);
        Assert.Equal("Chave desligada", _vm.Mensagem);
        Assert.Empty(Comandos());
    }

    [Fact]
    public async Task Reset_de_audio_so_sai_no_Confirmar_da_folha_e_o_espelho_mostra_a_fabrica()
    {
        await _b.Conectar();
        await Ligar(Chave.ResetAudio);
        Pedir("*242*");

        await _vm.ResetAudio.ExecutarAsync();

        Assert.True(_vm.FolhaAberta);
        Assert.Equal(FolhaAparelho.ResetAudio, _vm.Folha);
        Assert.Equal("O estado resultante é imprevisível.", _vm.FolhaAviso);
        Assert.Equal("Fazer o reset de áudio", _vm.TextoConfirmarFolha);
        Assert.Empty(Comandos()); // nada sai antes do botão de confirmar

        await _b.Rodar(_vm.ConfirmarFolha.ExecutarAsync());

        Assert.False(_vm.FolhaAberta);
        Assert.Equal(["*242*"], Comandos());
        Assert.Equal(ComandosAparelho.MsgResetAudio, _vm.Mensagem);
        foreach (var ((codigo, indice), valor) in ProcessadorSimulado.Fabrica)
            Assert.Equal(valor, _b.Estado.Valor(codigo, indice)!.NoFio);
    }

    [Theory]
    [InlineData(FolhaAparelho.ResetAudio)]
    [InlineData(FolhaAparelho.ResetFabrica)]
    public async Task Cancelar_a_folha_nada_sai(FolhaAparelho folha)
    {
        await _b.Conectar();
        await Ligar(Chave.ResetAudio, Chave.ResetFabrica);

        await (folha == FolhaAparelho.ResetAudio ? _vm.ResetAudio : _vm.ResetFabrica).ExecutarAsync();
        Assert.Equal(folha, _vm.Folha);
        await _vm.CancelarFolha.ExecutarAsync();
        await _b.Andar(TimeSpan.FromSeconds(1));

        Assert.False(_vm.FolhaAberta);
        Assert.Empty(Comandos());
        Assert.Equal("AB12", _b.Cofre.Senha);
    }

    [Fact]
    public async Task Reset_de_fabrica_rele_apaga_a_senha_do_celular_e_desconecta()
    {
        await _b.Conectar();
        await Ligar(Chave.ResetFabrica);
        Pedir("*004*");

        await _vm.ResetFabrica.ExecutarAsync();
        Assert.Equal("Apaga a senha e as 3 memórias.", _vm.FolhaAviso);
        await _b.Rodar(_vm.ConfirmarFolha.ExecutarAsync());

        Assert.Equal(["*004*"], Comandos());
        Assert.Null(_b.Cofre.Senha);
        Assert.Equal(EstadoSessao.Desconectado, _b.Sessao.Estado);
        Assert.Equal(ComandosAparelho.MsgResetFabrica, _vm.Mensagem);
        Assert.Equal(ControleParametroViewModel.MsgSemConexao, _vm.MotivoTravaResetFabrica);
        Assert.Equal(AparelhoViewModel.SemValor, _vm.TextoRemoteOut);
    }

    [Fact]
    public async Task Com_o_gerador_tocando_o_reset_fica_travado()
    {
        await _b.Conectar();
        await Ligar(Chave.Gerador, Chave.ResetAudio);
        Pedir("*213*0*", "*201*1000*", "*200*-20.0*", "*202*1*", "*202*0*");
        var tocar = _b.Gerador.TocarTomAsync(1000, -20.0m);
        await _b.Rodar(tocar);
        Assert.True((await tocar).Ok);
        await _b.Tela.EsvaziarAsync();
        var antes = _b.Simulador.Escritas.Count;

        Assert.Equal("O gerador está em uso.", _vm.MotivoTravaResetAudio);
        await _vm.ResetAudio.ExecutarAsync();

        Assert.False(_vm.FolhaAberta);
        Assert.Equal("O gerador está em uso.", _vm.Mensagem);
        Assert.Equal(antes, _b.Simulador.Escritas.Count);

        await _b.Rodar(_b.Gerador.PararAsync());
    }

    // ----------------------------------------------------------------- troca de senha

    [Fact]
    public async Task Troca_de_senha_pela_folha_guarda_a_nova_e_nenhuma_senha_aparece()
    {
        await _b.Conectar();
        await Ligar(Chave.TrocaSenha);
        Pedir("*001*AB12*CD34*");
        _vm.SenhaAtual = "AB12";
        _vm.SenhaNova = "CD34";
        _vm.SenhaNovaConfirmacao = "cd34";

        await _vm.TrocarSenha.ExecutarAsync();
        Assert.Equal(FolhaAparelho.TrocaSenha, _vm.Folha);
        Assert.Equal("Uma senha perdida tranca o aparelho.", _vm.FolhaAviso);
        Assert.Empty(Comandos());

        await _b.Rodar(_vm.ConfirmarFolha.ExecutarAsync());

        Assert.Equal(["*001*AB12*CD34*"], Comandos());
        Assert.Equal("CD34", _b.Cofre.Senha);
        Assert.Equal(ComandosAparelho.MsgSenhaTrocada, _vm.Mensagem);
        Assert.Null(_vm.ErroSenha);
        Assert.Equal("", _vm.SenhaAtual); // os campos limpam
        Assert.Equal("", _vm.SenhaNova);
        Assert.Equal("", _vm.SenhaNovaConfirmacao);
        // Critério de aceite: o log da sessão não mostra nenhuma das duas senhas.
        Assert.DoesNotContain(_b.Log.Linhas, l => l.Contains("AB12", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(_b.Log.Linhas, l => l.Contains("CD34", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(_b.Log.Linhas, l => l.Contains("*001*" + LogBle.Mascara));
    }

    [Fact]
    public async Task Senha_atual_errada_o_aparelho_recusa_e_a_guardada_nao_muda()
    {
        await _b.Conectar();
        await Ligar(Chave.TrocaSenha);
        Pedir("*001*XXXX*CD34*");
        _vm.SenhaAtual = "XXXX";
        _vm.SenhaNova = "CD34";
        _vm.SenhaNovaConfirmacao = "CD34";

        await _vm.TrocarSenha.ExecutarAsync();
        await _b.Rodar(_vm.ConfirmarFolha.ExecutarAsync());

        Assert.Equal("O aparelho recusou a senha atual.", _vm.Mensagem);
        Assert.Equal("O aparelho recusou a senha atual.", _vm.ErroSenha);
        Assert.Equal("AB12", _b.Cofre.Senha);
        Assert.False(_vm.FolhaAberta);
    }

    [Fact]
    public async Task Nova_que_nao_confere_nada_sai()
    {
        await _b.Conectar();
        await Ligar(Chave.TrocaSenha);
        _vm.SenhaAtual = "AB12";
        _vm.SenhaNova = "CD34";
        _vm.SenhaNovaConfirmacao = "CD35";

        await _vm.TrocarSenha.ExecutarAsync();

        Assert.False(_vm.FolhaAberta);
        Assert.Equal("As duas senhas novas não são iguais.", _vm.ErroSenha);
        Assert.Equal("As duas senhas novas não são iguais.", _vm.Mensagem);
        await _b.Andar(TimeSpan.FromSeconds(1));
        Assert.Empty(Comandos());
    }

    [Fact]
    public async Task Troca_de_senha_com_a_chave_desligada_nada_sai()
    {
        await _b.Conectar();
        _vm.SenhaAtual = "AB12";
        _vm.SenhaNova = "CD34";
        _vm.SenhaNovaConfirmacao = "CD34";

        await _vm.TrocarSenha.ExecutarAsync();

        Assert.False(_vm.FolhaAberta);
        Assert.Equal("Chave desligada", _vm.Mensagem);
        Assert.Empty(Comandos());
    }

    // ----------------------------------------------------------------- nome do produto

    [Fact]
    public async Task Nome_do_produto_vale_no_check_sem_folha()
    {
        await _b.Conectar();
        Pedir("*002*SALA 1*");
        _vm.NomeProduto = "sala 1";
        Assert.Empty(Comandos()); // até o ✓, nada sai

        await _b.Rodar(_vm.ConfirmarNome.ExecutarAsync());

        Assert.False(_vm.FolhaAberta);
        Assert.Equal(["*002*SALA 1*"], Comandos());
        Assert.Equal("Nome do produto: SALA 1", _vm.Mensagem);
        Assert.Equal("SALA 1", _vm.NomeProduto);
        Assert.Null(_vm.ErroNome);
    }

    [Fact]
    public async Task Nome_invalido_nada_sai()
    {
        await _b.Conectar();
        _vm.NomeProduto = "sala#1";

        await _vm.ConfirmarNome.ExecutarAsync();

        Assert.Equal("Use até 12 letras, números, espaço ou _", _vm.ErroNome);
        Assert.Equal("Use até 12 letras, números, espaço ou _", _vm.Mensagem);
        await _b.Andar(TimeSpan.FromSeconds(1));
        Assert.Empty(Comandos());
    }
}
