namespace BgsControle.Nucleo.Testes;

/// <summary>O catálogo: forma do Marco 1, decodificação de respostas reais e codificação de escritas.</summary>
public sealed class CatalogoTestes
{
    [Fact]
    public void Marco1_tem_22_parametros_e_84_consultas()
    {
        Assert.Equal(22, Catalogo.Marco1.Count);
        Assert.Equal(5, Catalogo.Marco1.Count(p => p.Escopo == Escopo.Global));
        Assert.Equal(16, Catalogo.Marco1.Count(p => p.Escopo == Escopo.Canal));
        Assert.Equal(["020"], Catalogo.Marco1.Where(p => p.Escopo == Escopo.Banda).Select(p => p.Codigo));
        Assert.Equal(84, Catalogo.TotalMarco1);
        Assert.All(Catalogo.Marco1, p => Assert.False(p.Extra));
    }

    [Fact]
    public void Extras_sao_os_que_o_app_da_Taramps_nao_pede()
    {
        Assert.Equal(
            ["005", "111", "150", "200", "201", "202", "210", "211", "212", "213", "251", "252"],
            Catalogo.Extras.Select(p => p.Codigo));
        Assert.All(Catalogo.Extras, p => Assert.True(p.Extra));
        Assert.All(Catalogo.Extras, p => Assert.Equal(Escopo.Global, p.Escopo));
        // Os nomes das memórias (231–233) não são lidos: as memórias do processador não aparecem (29/10/2026).
        Assert.Equal(12, Catalogo.TotalExtras);
        // Fila: o Marco 1 primeiro, depois os extras.
        Assert.Equal(Catalogo.ConsultasMarco1.Concat(Catalogo.ConsultasExtras), Catalogo.Consultas);
    }

    [Fact]
    public void Codigos_sao_unicos()
    {
        var codigos = Catalogo.Todos.Select(p => p.Codigo).ToList();
        Assert.Equal(codigos.Count, codigos.Distinct().Count());
    }

    [Theory]
    [InlineData("080", null, "#080#")]
    [InlineData("081", 0, "#081#0#")]
    [InlineData("092", 3, "#092#3#")]
    [InlineData("020", 0, "020#0")]
    [InlineData("020", 14, "020#14")]
    public void Monta_a_consulta_pelo_escopo(string codigo, int? indice, string esperado) =>
        Assert.Equal(esperado, Catalogo.ParametroDe(codigo)!.TextoConsulta(indice));

    [Theory]
    [InlineData("080", 0)]
    [InlineData("081", null)]
    [InlineData("081", 4)]
    [InlineData("081", -1)]
    [InlineData("020", 15)]
    public void Recusa_consulta_fora_do_escopo(string codigo, int? indice) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Catalogo.ParametroDe(codigo)!.TextoConsulta(indice));

    [Theory]
    [InlineData("#080#", true)]
    [InlineData("#081#3#", true)]
    [InlineData("020#14", true)]
    [InlineData("#252#", true)]
    [InlineData("#004#", false)]
    [InlineData("#000#", false)]
    [InlineData("#080#0#", false)]
    [InlineData("#081#", false)]
    [InlineData("#081#4#", false)]
    [InlineData("#081#00#", false)]
    [InlineData("020#15", false)]
    [InlineData("#020#3#", false)]
    [InlineData("*081*0*-3.0*", false)]
    public void Sabe_se_um_quadro_e_leitura_do_catalogo(string quadro, bool esperado) =>
        Assert.Equal(esperado, Catalogo.EhLeitura(quadro));

    // ------------------------------------------------------------------ cada parâmetro: decodificar e codificar

    /// <summary>
    /// Para cada parâmetro: uma resposta (as do Marco 1 são reais, de 2026-10-18_2145.json), o
    /// valor esperado na tela, e uma escrita com o quadro esperado.
    /// </summary>
    public static TheoryData<string, int?, string, string, string, string> CadaParametro => new()
    {
        // código, índice, resposta, exibição esperada, valor da escrita, quadro da escrita
        { "080", null, "#080#+0.0#", "0,0 dB", "-10.0", "*080*-10.0*" },
        { "081", 0, "#081#0#+10.0#", "+10,0 dB", "-3.0", "*081*0*-3.0*" },
        { "100", 0, "#100#0#0#", "com som", "1", "*100*0*1*" },
        { "010", 2, "#010#2#a+b#", "A+B", "a+b", "*010*2*a+b*" },
        { "060", 2, "#060#2#180#", "180°", "180", "*060*2*180*" },
        { "040", 0, "#040#0#150#", "150 Hz", "80", "*040*0*80*" },
        { "041", 2, "#041#2#150#", "150 Hz", "22000", "*041*2*22000*" },
        { "042", 2, "#042#2#11#", "BT24", "3", "*042*2*3*" },
        { "043", 0, "#043#0#0#", "OFF", "0", "*043*0*0*" },
        { "050", 1, "#050#1#100.0#", "100,0 cm", "80.5", "*050*1*80.5*" },
        { "070", 0, "#070#0#0#", "manual", "1", "*070*0*1*" },
        { "071", 0, "#071#0#-15.0#", "-15,0 dB", "0.5", "*071*0*0.5*" },
        { "072", 0, "#072#0#50.0#", "50,0 ms", "0.05", "*072*0*0.05*" },
        { "073", 0, "#073#0#200#", "200 ms", "1600", "*073*0*1600*" },
        { "090", 2, "#090#2#45#", "45 Hz", "45", "*090*2*45*" },
        { "091", 0, "#091#0#+2.5#", "+2,5 dB", "-12", "*091*0*-12.0*" },
        { "092", 0, "#092#0#+0.7#", "0,7", "0.1", "*092*0*0.1*" },
        { "030", null, "#030#+0.0#", "0,0 dB", "12", "*030*12.0*" },
        { "031", null, "#031#1000#", "1000 Hz", "10", "*031*10*" },
        { "032", null, "#032#+1.0#", "1,0", "0.4", "*032*0.4*" },
        { "020", 6, "020#6#-1.0", "-1,0 dB", "2.5", "020*6*2.5" },
        { "220", null, "#220#255#", "Custom", "0", "*220*0*" },
        // Extras (respostas plausíveis: o aparelho nunca foi perguntado sobre eles).
        { "200", null, "#200#-20.0#", "-20,0 dB", "-60", "*200*-60.0*" },
        { "201", null, "#201#1000#", "1000 Hz", "440", "*201*440*" },
        { "202", null, "#202#0#", "desligado", "0", "*202*0*" },
        { "210", null, "#210#20#", "20 Hz", "20", "*210*20*" },
        { "211", null, "#211#20000#", "20000 Hz", "20000", "*211*20000*" },
        { "212", null, "#212#-30.0#", "-30,0 dB", "-30", "*212*-30.0*" },
        { "213", null, "#213#2#", "médio", "3", "*213*3*" },
        { "251", null, "#251#1#", "ligado", "0", "*251*0*" },
        { "252", null, "#252#1#", "ligado", "0", "*252*0*" },
    };

    [Theory]
    [MemberData(nameof(CadaParametro))]
    public void Cada_parametro_decodifica_a_resposta_e_codifica_a_escrita(
        string codigo, int? indice, string resposta, string exibicao, string escrita, string quadro)
    {
        var p = Catalogo.ParametroDe(codigo)!;
        var consulta = new Consulta(p, indice);

        var noFio = Leitura.LeitorCompleto.Casar(consulta, resposta);
        Assert.NotNull(noFio);
        var d = p.Decodificar(noFio);
        Assert.True(d.Ok, d.Motivo);
        Assert.Equal(exibicao, d.Valor!.Exibicao);
        Assert.False(d.Valor.ForaDaFaixa);
        Assert.Equal(noFio, d.Valor.NoFio);

        var montado = p.Tipo is TipoValor.Roteamento or TipoValor.Nome
            ? p.CodificarEscrita(indice, escrita)
            : p.CodificarEscrita(indice, decimal.Parse(escrita, System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(quadro, montado);
    }

    [Fact]
    public void Todo_parametro_que_escreve_esta_na_tabela()
    {
        var naTabela = CadaParametro.Select(l => (string)l[0]).ToHashSet();
        Assert.All(Catalogo.Todos.Where(p => !p.SomenteLeitura), p => Assert.Contains(p.Codigo, naTabela));
    }

    [Theory]
    [InlineData("005", "120", "12,0 V")]
    [InlineData("111", "1", "clip")]
    [InlineData("111", "0", "sem clip")]
    [InlineData("150", "3", "saídas 1, 2")]
    [InlineData("150", "0", "nenhuma saída")]
    public void Monitoracao_decodifica_e_nao_tem_escrita(string codigo, string noFio, string exibicao)
    {
        var p = Catalogo.ParametroDe(codigo)!;
        Assert.True(p.SomenteLeitura);
        Assert.Equal(exibicao, p.Decodificar(noFio).Valor!.Exibicao);
        Assert.Throws<InvalidOperationException>(() => p.CodificarEscrita(null, 1m));
    }

    // ------------------------------------------------------------------ regras de decodificação

    [Fact]
    public void Banda_3_e_100_Hz_e_guarda_o_numero()
    {
        var p = Catalogo.ParametroDe("020")!;
        Assert.Equal("banda 100 Hz", p.Onde(3));
        Assert.Equal("banda 160 Hz", p.Onde(4));
        Assert.Equal("banda 1,6 kHz", p.Onde(9));
        Assert.Equal("banda 10 kHz", p.Onde(13));
        var v = p.Decodificar("-1.0").Valor!;
        Assert.Equal(-1.0m, v.Numero);
        Assert.Equal("-1,0 dB", v.Exibicao);
    }

    [Theory]
    [InlineData("a", "a", "A")]
    [InlineData("B", "b", "B")]
    [InlineData("ab", "a+b", "A+B")]
    [InlineData("a+b", "a+b", "A+B")]
    public void Roteamento_aceita_ab(string noFio, string texto, string rotulo)
    {
        var v = Catalogo.ParametroDe("010")!.Decodificar(noFio).Valor!;
        Assert.Equal(texto, v.Texto);
        Assert.Equal(rotulo, v.Rotulo);
        Assert.False(v.ForaDaFaixa);
    }

    [Theory]
    [InlineData("100", "1", true, false)]
    [InlineData("100", "0", false, false)]
    [InlineData("100", "2", true, true)]
    [InlineData("100", "-1", true, true)]
    [InlineData("070", "1", true, false)]
    [InlineData("070", "0", false, false)]
    [InlineData("070", "0.5", true, true)]
    public void Booleano_diferente_de_zero_e_ligado_e_so_0_e_1_estao_na_faixa(string codigo, string noFio, bool ligado, bool fora)
    {
        var v = Catalogo.ParametroDe(codigo)!.Decodificar(noFio).Valor!;
        Assert.Equal(ligado, v.Ligado);
        Assert.Equal(fora, v.ForaDaFaixa);
        Assert.Equal(fora, v.Exibicao.EndsWith("(fora da faixa)"));
    }

    [Theory]
    [InlineData("071", "0.5", "OFF")]
    [InlineData("072", "0.01", "auto")]
    [InlineData("072", "0.05", "auto")]
    [InlineData("042", "0", "OFF")]
    [InlineData("042", "7", "LR48")]
    [InlineData("042", "8", "BT6")]
    [InlineData("043", "15", "BT48")]
    [InlineData("220", "11", "Competition")]
    [InlineData("220", "255", "Custom")]
    [InlineData("060", "0", "0°")]
    public void Especiais_e_opcoes_tem_rotulo(string codigo, string noFio, string rotulo)
    {
        var v = Catalogo.ParametroDe(codigo)!.Decodificar(noFio).Valor!;
        Assert.Equal(rotulo, v.Rotulo);
        Assert.False(v.ForaDaFaixa);
    }

    [Theory]
    [InlineData("060", "90")]
    [InlineData("060", "1")]
    [InlineData("042", "16")]
    [InlineData("220", "12")]
    [InlineData("081", "+15.5")]
    [InlineData("081", "-45.1")]
    [InlineData("040", "9")]
    [InlineData("040", "150.5")]
    [InlineData("073", "1601")]
    [InlineData("071", "0.4")]
    [InlineData("072", "0.0")]
    [InlineData("092", "0.0")]
    [InlineData("032", "0.3")]
    [InlineData("050", "275.5")]
    [InlineData("010", "c")]
    public void Fora_da_faixa_e_guardado_como_veio(string codigo, string noFio)
    {
        var d = Catalogo.ParametroDe(codigo)!.Decodificar(noFio);
        Assert.True(d.Ok);
        Assert.True(d.Valor!.ForaDaFaixa);
        Assert.Equal(noFio, d.Valor.NoFio);
        Assert.EndsWith("(fora da faixa)", d.Valor.Exibicao);
        if (codigo != "010")
            Assert.Equal(decimal.Parse(noFio, System.Globalization.CultureInfo.InvariantCulture), d.Valor.Numero);
    }

    [Theory]
    [InlineData("081", "")]
    [InlineData("081", "abc")]
    [InlineData("081", "1,5")]
    [InlineData("081", "1e3")]
    [InlineData("040", "NaN")]
    [InlineData("100", "sim")]
    public void Texto_que_nao_e_numero_e_ilegivel(string codigo, string noFio)
    {
        var d = Catalogo.ParametroDe(codigo)!.Decodificar(noFio);
        Assert.False(d.Ok);
        Assert.Equal(Parametro.MotivoIlegivel, d.Motivo);
    }

    [Fact]
    public void Casas_por_tipo()
    {
        foreach (var codigo in new[] { "080", "081", "091", "030", "071", "092", "032", "050", "072", "020", "200", "212" })
            Assert.Equal(1, Catalogo.ParametroDe(codigo)!.Casas);
        foreach (var codigo in new[] { "040", "041", "090", "031", "073", "042", "043", "220", "060", "100", "070", "213", "201" })
            Assert.Equal(0, Catalogo.ParametroDe(codigo)!.Casas);
    }

    // ------------------------------------------------------------------ regras de codificação

    [Theory]
    [InlineData("081", 0, "15.1")]
    [InlineData("081", 0, "-45.1")]
    [InlineData("081", 0, "-3.25")]
    [InlineData("040", 0, "9")]
    [InlineData("040", 0, "150.5")]
    [InlineData("060", 0, "90")]
    [InlineData("042", 0, "16")]
    [InlineData("220", null, "12")]
    [InlineData("100", 0, "2")]
    [InlineData("071", 0, "0.4")]
    [InlineData("072", 0, "0.02")]
    [InlineData("020", 0, "12.5")]
    public void Escrita_fora_da_faixa_e_recusada(string codigo, int? indice, string valor) =>
        Assert.ThrowsAny<ArgumentException>(() =>
            Catalogo.ParametroDe(codigo)!.CodificarEscrita(indice, decimal.Parse(valor, System.Globalization.CultureInfo.InvariantCulture)));

    [Theory]
    [InlineData("081", null)]
    [InlineData("081", 4)]
    [InlineData("080", 0)]
    [InlineData("020", 15)]
    public void Escrita_fora_do_escopo_e_recusada(string codigo, int? indice) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Catalogo.ParametroDe(codigo)!.CodificarEscrita(indice, 0m));

    [Fact]
    public void Escrita_usa_ponto_e_uma_casa()
    {
        var ganho = Catalogo.ParametroDe("081")!;
        Assert.Equal("*081*3*0.0*", ganho.CodificarEscrita(3, 0m));
        Assert.Equal("*081*0*-12.0*", ganho.CodificarEscrita(0, -12m));
        Assert.Equal("*081*0*15.0*", ganho.CodificarEscrita(0, 15m));
        Assert.Equal("*100*2*0*", Catalogo.ParametroDe("100")!.CodificarEscrita(2, false));
        Assert.Equal("*010*0*a+b*", Catalogo.ParametroDe("010")!.CodificarEscrita(0, "AB"));
        Assert.Equal("*220*255*", Catalogo.ParametroDe("220")!.CodificarEscrita(null, 255m));
        Assert.Equal("*072*1*0.01*", Catalogo.ParametroDe("072")!.CodificarEscrita(1, 0.01m));
    }

    [Theory]
    [InlineData("")]
    [InlineData("NOME*COM")]
    [InlineData("NOME#COM")]
    [InlineData("TREZE LETRAS")] // 12: passa
    [InlineData("TREZE LETRAS!")]
    [InlineData("ÇÃO")]
    [InlineData("FILME\n")]
    [InlineData("\n")]
    [InlineData("   ")]
    [InlineData(" ")]
    public void Um_parametro_de_nome_vai_sem_separador_e_ate_12(string nome)
    {
        // Nenhum parâmetro do catálogo é de nome desde 29/10/2026 (os nomes 231–233 saíram); o tipo fica
        // para o simulador, que imita os nomes das memórias do aparelho.
        var p = new Parametro { Codigo = "231", Nome = "nome da memória 1", Escopo = Escopo.Global, Tipo = TipoValor.Nome, Extra = true };
        if (nome == "TREZE LETRAS")
            Assert.Equal("*231*TREZE LETRAS*", p.CodificarEscrita(null, nome));
        else
            Assert.Throws<ArgumentException>(() => p.CodificarEscrita(null, nome));
    }

    [Fact]
    public void Tipo_errado_na_escrita_e_recusado()
    {
        Assert.Throws<ArgumentException>(() => Catalogo.ParametroDe("081")!.CodificarEscrita(0, "a"));
        Assert.Throws<ArgumentException>(() => Catalogo.ParametroDe("010")!.CodificarEscrita(0, 1m));
        Assert.Throws<ArgumentException>(() => Catalogo.ParametroDe("081")!.CodificarEscrita(0, true));
        Assert.Throws<ArgumentOutOfRangeException>(() => Catalogo.ParametroDe("010")!.CodificarEscrita(0, "c"));
    }

    // ------------------------------------------------------------------ resposta sem \r\n

    [Theory]
    [InlineData("#080#+0.0#", true)]
    [InlineData("#081#0#+10.0#", true)]
    [InlineData("020#3#-1.0", true)]
    [InlineData("020#10#+2.0", true)]
    [InlineData("#081#0#", false)] // eco: o valor pode estar a caminho
    [InlineData("#080#", false)]
    [InlineData("#081#0#+10", false)]
    [InlineData("020#3#-1", false)]
    [InlineData("020#3#-1.", false)]
    [InlineData("020#3", false)]
    [InlineData("#004#1#", false)]
    [InlineData("#213#0#", false)] // extra: o resto pode estar a caminho (só o \r\n diz que acabou)
    [InlineData("#150#3#", false)]
    public void Reconhece_resposta_inteira_sem_fim_de_linha(string texto, bool inteira) =>
        Assert.Equal(inteira, Catalogo.RespostaInteira(texto));
}
