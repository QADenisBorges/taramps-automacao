using System.Text.RegularExpressions;
using BgsControle.Nucleo;
using BgsControle.Nucleo.Gerador;
using BgsControle.Nucleo.Seguranca;

namespace BgsControle.Telas.Testes;

/// <summary>A tela Gerador (história 7a) contra o processador simulado.</summary>
public sealed class GeradorViewModelTestes : IDisposable
{
    private static readonly Regex Senha = new(@"^\*000\*[0-9A-Z]{4}\*$");
    private static readonly TimeSpan Prazo = TimeSpan.FromSeconds(30);

    private readonly Bancada _b = new();
    private readonly GeradorViewModel _vm;

    public GeradorViewModelTestes() => _vm = _b.CriarGeradorViewModel();

    /// <summary>
    /// Critério de aceite: fora do gerador, só a senha e as leituras do catálogo; cada
    /// <c>*202*1*</c> tem um <c>*202*0*</c> aceito até 30 s depois.
    /// </summary>
    public void Dispose()
    {
        _vm.Dispose();
        Assert.All(_b.Simulador.Escritas, e =>
            Assert.True(Senha.IsMatch(e) || Catalogo.EhLeitura(e) || Catalogo.ComandoDeGerador(e) is not null, e));
        var marcas = _b.Simulador.LinhaDoTempoGerador;
        for (var i = 0; i < marcas.Count; i++)
        {
            if (!marcas[i].Ligado)
                continue;
            var desligou = marcas.Skip(i + 1).FirstOrDefault(m => !m.Ligado);
            Assert.True(desligou is not null && desligou.Quando - marcas[i].Quando <= Prazo, "um *202*1* ficou sem *202*0* em 30 s");
        }
        Assert.Empty(_b.Tela.Erros);
    }

    private async Task LigarChave() =>
        Assert.True((await _b.Chaves.DefinirAsync(Chave.Gerador, true, OrigemComando.Tela)).Ok);

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

    private int Comandos() => _b.Simulador.Escritas.Count(e => !Catalogo.EhLeitura(e) && !Senha.IsMatch(e));

    // ----------------------------------------------------------------- campos e travas

    [Fact]
    public async Task Os_campos_vem_semeados_pelo_espelho()
    {
        Assert.Equal("", _vm.FrequenciaTom);

        await _b.Conectar();

        // Os extras reais de 20/10: 200 = −25,0; 201 = 51; 210 = 10; 211 = 22000; 212 = −45,0; 213 = 0.
        Assert.Equal("51", _vm.FrequenciaTom);
        Assert.Equal("−25,0", _vm.GanhoTom);
        Assert.Equal("10", _vm.InicioSweep);
        Assert.Equal("22000", _vm.FimSweep);
        Assert.Equal("−45,0", _vm.GanhoSweep);
        Assert.Equal(GeradorViewModel.VelocidadePadrao, _vm.VelocidadeSweep); // 0 no espelho = tom fixo
        Assert.Equal(["lento", "médio", "rápido"], _vm.Velocidades.Select(v => v.Rotulo));
        Assert.Equal("10 a 22000 Hz", _vm.FaixaFrequencia);
        Assert.Equal("−60,0 a 0,0 dB", _vm.FaixaGanho);
    }

    [Fact]
    public async Task Sem_conexao_e_com_a_chave_desligada_Tocar_fica_travado_e_o_Parar_nao()
    {
        Assert.Equal(ControleParametroViewModel.MsgSemConexao, _vm.MotivoTrava);
        Assert.True(_vm.PodeParar);

        await _b.Conectar();
        Assert.Equal(ControleParametroViewModel.MsgChaveDesligada, _vm.MotivoTrava);
        Assert.False(_vm.PodeTocar);
        Assert.True(_vm.PodeParar);
        var antes = _b.Simulador.Escritas.Count;

        await _vm.Tocar.ExecutarAsync();

        Assert.Equal("Chave desligada", _vm.Mensagem);
        Assert.Equal(antes, _b.Simulador.Escritas.Count);

        await LigarChave();
        await _b.Tela.EsvaziarAsync();
        Assert.Null(_vm.MotivoTrava);
        Assert.True(_vm.PodeTocar);
    }

    [Theory]
    [InlineData("1000", "+1,0", "Fora da faixa: −60,0 a 0,0 dB")]
    [InlineData("5", "−20", "Fora da faixa: 10 a 22000 Hz")]
    [InlineData("1000", "abc", ControleParametroViewModel.MsgNaoNumero)]
    [InlineData("1000,5", "−20", "Use um número inteiro.")]
    public async Task Fora_da_faixa_nada_sai_e_a_faixa_aparece(string hz, string db, string erro)
    {
        await _b.Conectar();
        await LigarChave();
        _vm.FrequenciaTom = hz;
        _vm.GanhoTom = db;

        await _vm.Tocar.ExecutarAsync();

        Assert.Equal(erro, _vm.ErroCampo);
        Assert.Equal(erro, _vm.Mensagem);
        Assert.Equal(0, Comandos());
    }

    // ----------------------------------------------------------------- tocar, contagem e prazo

    [Fact]
    public async Task Tom_fixo_mostra_a_contagem_e_desliga_sozinho_no_prazo()
    {
        await _b.Conectar();
        await LigarChave();
        _vm.FrequenciaTom = "1000";
        _vm.GanhoTom = "−20";

        await _b.Rodar(_vm.Tocar.ExecutarAsync());

        Assert.Equal(["*213*0*", "*201*1000*", "*200*-20.0*", "*202*1*"],
            _b.Simulador.Escritas.Where(e => Catalogo.ComandoDeGerador(e) is not null));
        Assert.True(_vm.Tocando);
        Assert.Equal(30, _vm.Contagem);
        Assert.Equal("30 s", _vm.TextoContagem);
        Assert.Equal(GeradorViewModel.MsgEmUso, _vm.MotivoTrava);
        Assert.True(_vm.PodeParar);

        await _b.Andar(TimeSpan.FromSeconds(5), passoMs: 250);
        Assert.Equal(25, _vm.Contagem);

        _b.Relogio.Avancar(TimeSpan.FromSeconds(24));
        await _b.Tela.EsvaziarAsync();
        // O estado vira Desligado um pouco antes da mensagem chegar à tela: espera as duas.
        await AndarAte(() => !_vm.Tocando && _b.Gerador.Estado == EstadoGerador.Desligado
                             && _vm.Mensagem == GeradorSinal.MsgPrazo);

        Assert.Equal(GeradorSinal.MsgPrazo, _vm.Mensagem);
        Assert.Null(_vm.Contagem);
        Assert.False(_b.Simulador.GeradorLigado);
        Assert.Null(_vm.MotivoTrava);
    }

    [Fact]
    public async Task Sweep_e_Parar()
    {
        await _b.Conectar();
        await LigarChave();
        _vm.ModoSweep = true;
        _vm.InicioSweep = "20";
        _vm.FimSweep = "200";
        _vm.GanhoSweep = "−30,0";
        _vm.VelocidadeSweep = 2;

        await _b.Rodar(_vm.Tocar.ExecutarAsync());
        Assert.True(_vm.Tocando);
        Assert.Equal(["*210*20*", "*211*200*", "*212*-30.0*", "*213*2*", "*202*1*"],
            _b.Simulador.Escritas.Where(e => Catalogo.ComandoDeGerador(e) is not null));

        await _b.Rodar(_vm.Parar.ExecutarAsync());

        Assert.False(_vm.Tocando);
        Assert.Equal(GeradorSinal.MsgParado, _vm.Mensagem);
        Assert.Equal("*202*0*", _b.Simulador.Escritas[^1]);
        Assert.False(_b.Simulador.GeradorLigado);
        Assert.Null(_vm.Contagem);
    }

    // ----------------------------------------------------------------- regras de falha

    [Fact]
    public async Task Ligado_ao_conectar_a_tela_avisa_que_estava_ligado()
    {
        _b.Simulador.Definir("202", null, "1");
        await _b.Chaves.Carregada;
        await _b.Copia.Carregada;
        await _b.Link.Carregada;

        await _b.Sessao.ConectarAsync();
        await AndarAte(() => _b.Estado.Pronto && !_b.Estado.Lendo && !_b.Gerador.TalvezLigado);

        Assert.Equal(GeradorSinal.MsgEstavaLigado, _vm.Mensagem);
        Assert.False(_b.Simulador.GeradorLigado);
        Assert.False(_vm.Alarme);
    }

    [Fact]
    public async Task So_leitura_com_o_202_ligado_mostra_o_alarme()
    {
        _b.Simulador.Firmware = "R004";
        _b.Simulador.Definir("202", null, "1");

        await _b.Conectar();

        Assert.True(_vm.Alarme);
        Assert.Equal(GeradorViewModel.TextoAlarme, _vm.TextoDoAlarme);
        Assert.Equal("DESLIGUE O MÓDULO AGORA", _vm.TextoDoAlarme);
        Assert.Equal(EstadoGerador.SemResposta, _vm.Estado);
        Assert.Equal(ControleParametroViewModel.MsgSoLeitura, _vm.MotivoTrava);
        Assert.True(_vm.PodeParar);
        Assert.Equal(0, Comandos());
    }
}
