using BgsControle.Nucleo.Protocolo;
using BgsControle.Nucleo.Registro;

namespace BgsControle.Nucleo.Testes;

public sealed class QuadrosTestes
{
    [Theory]
    [InlineData("ab12", "AB12")]
    [InlineData(" a b 1 2 ", "AB12")]
    [InlineData("0000", "0000")]
    [InlineData("Zz9\t9", "ZZ99")]
    public void Normaliza_a_senha(string digitada, string esperada) =>
        Assert.Equal(esperada, Quadros.NormalizarSenha(digitada));

    [Theory]
    [InlineData("12a")]
    [InlineData("12345")]
    [InlineData("12-4")]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("ıııı")] // 'ı' vira 'I' em maiúsculas, mas não é ASCII
    [InlineData("ab1²")]
    public void Recusa_senha_fora_do_formato(string? digitada) =>
        Assert.Null(Quadros.NormalizarSenha(digitada));

    [Fact]
    public void Monta_o_quadro_da_senha() => Assert.Equal("*000*AB12*", Quadros.MontarSenha("AB12"));

    [Theory]
    [InlineData("ab12")]
    [InlineData("12a")]
    [InlineData("AB 12")]
    public void Nao_monta_senha_fora_do_formato(string senha) =>
        Assert.Throws<ArgumentException>(() => Quadros.MontarSenha(senha));

    [Fact]
    public void Analisa_codigo_e_segmentos()
    {
        var q = Quadros.Analisar("#081#0#-11.0#\r\n");
        Assert.NotNull(q);
        Assert.Equal("081", q.Codigo);
        Assert.Equal(["0", "-11.0"], q.Segmentos);

        var banda = Quadros.Analisar("020#3#2.5");
        Assert.Equal("020", banda!.Codigo);
        Assert.Equal(["3", "2.5"], banda.Segmentos);

        Assert.Equal(["AB12"], Quadros.Analisar("*000*AB12*")!.Segmentos);
        Assert.Null(Quadros.Analisar("MIR003#"));
        Assert.Null(Quadros.Analisar("#12#"));
    }

    [Fact]
    public void Junta_fragmentos_e_recorta_por_fim_de_linha()
    {
        var m = new MontadorQuadros();
        m.Alimentar("#081#0#-1");
        Assert.Empty(m.RetirarFechados());
        Assert.Equal("#081#0#-1", m.Aberto);

        m.Alimentar("1.0#\r\n#080#");
        Assert.Equal(["#081#0#-11.0#"], m.RetirarFechados());
        Assert.Equal("#080#", m.Aberto);

        m.Alimentar("-3.0#\n");
        Assert.Equal(["#080#-3.0#"], m.RetirarFechados());
        Assert.Equal("", m.Aberto);
    }

    [Fact]
    public void Recorta_pelo_comeco_de_um_novo_quadro()
    {
        var m = new MontadorQuadros();
        m.Alimentar("*000*AB12*#000#ERRO#");
        Assert.Equal(["*000*AB12*"], m.RetirarFechados());
        Assert.Equal("#000#ERRO#", m.Aberto);

        m.Alimentar("#005#120#");
        Assert.Equal(["#000#ERRO#"], m.RetirarFechados());
        Assert.Equal("#005#120#", m.Aberto);
    }

    [Fact]
    public void Esvaziar_devolve_tudo()
    {
        var m = new MontadorQuadros();
        m.Alimentar("#005#120#\r\n#000#PR");
        Assert.Equal(["#005#120#", "#000#PR"], m.Esvaziar());
        Assert.Equal("", m.Aberto);
    }
}

public sealed class AutenticacaoTestes
{
    private static ResultadoAutenticacao Ler(string texto) => Autenticacao.Interpretar(Quadros.Analisar(texto)!);

    [Fact]
    public void Resposta_real_aceita_R003()
    {
        var r = Assert.IsType<ResultadoAutenticacao.Aceita>(Ler("#000#PRO2.4MIR003#"));
        Assert.Equal("R003", r.Firmware);
        Assert.True(r.ProdutoConhecido);
        Assert.False(r.SoLeitura);
    }

    [Fact]
    public void Firmware_sao_os_4_ultimos_sem_espacos()
    {
        var r = Assert.IsType<ResultadoAutenticacao.Aceita>(Ler("#000#PRO2.4MI R 004 #"));
        Assert.Equal("R004", r.Firmware);
        Assert.True(r.SoLeitura);
    }

    [Fact]
    public void Produto_desconhecido_fica_so_leitura()
    {
        var r = Assert.IsType<ResultadoAutenticacao.Aceita>(Ler("#000#XYZR003#"));
        Assert.Equal("R003", r.Firmware);
        Assert.False(r.ProdutoConhecido);
        Assert.True(r.SoLeitura);
    }

    [Theory]
    [InlineData("#000#ERRO#")]
    [InlineData("#000#erro#")]
    [InlineData("*000*ERRO*")]
    public void ERRO_e_senha_errada(string texto) => Assert.IsType<ResultadoAutenticacao.Recusada>(Ler(texto));

    [Theory]
    [InlineData("#000#")]
    [InlineData("#000#E")]
    [InlineData("#000#ER")]
    [InlineData("#000#ERR")]
    [InlineData("#000# #")]
    public void ERRO_cortado_ou_vazio_e_incompleto(string texto) =>
        Assert.IsType<ResultadoAutenticacao.Incompleta>(Ler(texto));

    [Fact]
    public void So_a_funcao_000_e_resposta_da_senha() =>
        Assert.Throws<ArgumentException>(() => Autenticacao.Interpretar(Quadros.Analisar("#005#120#")!));
}

public sealed class LogBleTestes
{
    [Fact]
    public void Quadro_da_senha_sai_mascarado()
    {
        var log = new LogBle();
        log.Enviado("*000*AB12*");
        log.Recusado("*001*AB12*CD34*", "teste");
        Assert.All(log.Linhas, l => Assert.DoesNotContain("AB12", l));
        Assert.Contains("*000*••••*", log.Linhas[0]);
    }

    [Fact]
    public void Senha_protegida_some_de_qualquer_linha()
    {
        var log = new LogBle();
        log.ProtegerSegredo("CD34");
        log.Recebido("#000#cd34#");
        log.Evento("algo com CD34 no meio");
        Assert.All(log.Linhas, l => Assert.DoesNotContain("CD34", l, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Resposta_do_aparelho_continua_legivel()
    {
        var log = new LogBle();
        log.ProtegerSegredo("AB12");
        log.Recebido("#000#PRO2.4MIR003#\r\n");
        Assert.Contains("#000#PRO2.4MIR003#", log.Linhas[0]);
    }
}
