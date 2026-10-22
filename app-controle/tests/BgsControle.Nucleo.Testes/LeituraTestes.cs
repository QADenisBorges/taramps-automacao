using System.Text.Json;
using BgsControle.Nucleo.Leitura;
using BgsControle.Nucleo.Registro;

namespace BgsControle.Nucleo.Testes;

/// <summary>A leitura completa e a matriz de cenários da história 2, com o transporte falso.</summary>
public sealed class LeituraTestes
{
    private static readonly TemposSessao Rapidos = new(
        Scan: TimeSpan.FromSeconds(1),
        TentativaConexao: TimeSpan.FromSeconds(1),
        PausaEntreTentativas: TimeSpan.FromMilliseconds(1),
        RespostaSenha: TimeSpan.FromMilliseconds(300),
        PrazoSenha: TimeSpan.FromMilliseconds(300));

    private static readonly TemposLeitura LeituraRapida = new(TimeSpan.Zero, TimeSpan.FromMilliseconds(60));

    private const string Arquivo = "Dados/leitura-2026-10-18_2145.json";

    private readonly TransporteFalso _transporte = new();
    private readonly CofreFalso _cofre = new() { Senha = "AB12" };
    private readonly LogBle _log = new();
    private readonly Sessao _sessao;
    private readonly LeitorCompleto _leitor;

    public LeituraTestes()
    {
        _sessao = new Sessao(_transporte, _cofre, _log, Rapidos);
        _leitor = new LeitorCompleto(_sessao, _log, LeituraRapida);
    }

    /// <summary>As leituras do arquivo real: (consulta, cru, valor do JSON).</summary>
    private static List<(string Consulta, string Codigo, int? Canal, string Cru, JsonElement Valor)> LeiturasReais()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, Arquivo)));
        var saida = new List<(string, string, int?, string, JsonElement)>();
        foreach (var l in doc.RootElement.GetProperty("leituras").EnumerateArray())
        {
            var codigo = l.GetProperty("codigo").GetString()!;
            int? canal = l.GetProperty("canal").ValueKind == JsonValueKind.Null ? null : l.GetProperty("canal").GetInt32();
            var cru = l.GetProperty("cru").GetString()!;
            var consulta = Catalogo.ParametroDe(codigo)!.TextoConsulta(canal);
            saida.Add((consulta, codigo, canal, cru, l.GetProperty("valor").Clone()));
        }
        return saida;
    }

    /// <summary>O transporte responde as 84 consultas com as respostas cruas reais.</summary>
    private void ResponderComOArquivoReal()
    {
        foreach (var l in LeiturasReais())
            _transporte.Respostas[l.Consulta] = [l.Cru + "\r\n"];
    }

    private async Task Conectar()
    {
        await _sessao.ConectarAsync();
        Assert.Equal(EstadoSessao.Conectado, _sessao.Estado);
    }

    private void SoSenhaEConsultasDoCatalogo() =>
        Assert.All(_transporte.Escritas, e => Assert.True(e == "*000*AB12*" || Catalogo.EhLeitura(e), e));

    // ----------------------------------------------------------------- critérios de aceite

    [Fact]
    public async Task Com_as_respostas_reais_o_retrato_bate_valor_a_valor()
    {
        ResponderComOArquivoReal();
        await Conectar();

        var r = await _leitor.LerAsync();

        Assert.True(r.Completo);
        Assert.Equal("completo", r.Status);
        Assert.Null(r.Motivo);
        Assert.Equal("84 de 84", r.PlacarMarco1.Texto);
        Assert.DoesNotContain(r.Falhas, f => !f.Extra);
        Assert.Empty(r.Orfaos);
        Assert.Equal("R003", r.Firmware);
        Assert.NotNull(r.Fim);
        Assert.NotNull(r.FimMarco1);

        var reais = LeiturasReais();
        Assert.Equal(84, reais.Count);
        foreach (var (_, codigo, canal, cru, valor) in reais)
        {
            var item = r.Item(codigo, canal);
            Assert.True(item is not null, $"{codigo} canal {canal} não foi lido");
            Assert.Equal(cru, item.Cru);
            Assert.False(item.Valor.ForaDaFaixa, $"{codigo} {canal}");
            switch (valor.ValueKind)
            {
                case JsonValueKind.True or JsonValueKind.False:
                    Assert.Equal(valor.GetBoolean(), item.Valor.Ligado);
                    break;
                case JsonValueKind.String:
                    Assert.Equal(valor.GetString(), item.Valor.Texto);
                    break;
                default:
                    Assert.Equal(valor.GetDecimal(), item.Valor.Numero);
                    break;
            }
        }
    }

    [Fact]
    public async Task O_transporte_so_recebe_a_senha_e_as_consultas_do_catalogo_na_ordem()
    {
        ResponderComOArquivoReal();
        await Conectar();

        await _leitor.LerAsync();

        SoSenhaEConsultasDoCatalogo();
        Assert.Equal(["*000*AB12*", .. Catalogo.Consultas.Select(c => c.Texto)], _transporte.Escritas);
    }

    [Fact]
    public async Task Os_valores_da_FILME_aparecem_na_lista()
    {
        ResponderComOArquivoReal();
        await Conectar();

        var r = await _leitor.LerAsync();

        Assert.Equal("050 delay · saída 1 · 0,0 cm", r.Item("050", 0)!.Linha);
        Assert.Equal("050 delay · saída 2 · 100,0 cm", r.Item("050", 1)!.Linha);
        Assert.Equal("050 delay · saída 3 · 80,0 cm", r.Item("050", 2)!.Linha);
        Assert.Equal("060 fase · saída 3 · 180°", r.Item("060", 2)!.Linha);
        Assert.Equal("042 HPF filtro · saída 3 · BT24", r.Item("042", 2)!.Linha);
        Assert.Equal("220 preset de EQ · — · Custom", r.Item("220")!.Linha);
        Assert.Equal("020 EQ gráfico · banda 400 Hz · -1,0 dB", r.Item("020", 6)!.Linha);
    }

    // ----------------------------------------------------------------- extras

    [Fact]
    public async Task Extras_sao_perguntados_uma_vez_e_o_cru_fica_registrado()
    {
        ResponderComOArquivoReal();
        _transporte.Respostas["#005#"] = ["#005#120#\r\n"];
        _transporte.Respostas["#231#"] = ["#231#FILME#\r\n"];
        _transporte.Respostas["#232#"] = ["#232#0#MUSICA#\r\n"]; // formato inesperado: guarda o que veio
        _transporte.Respostas["#252#"] = ["#252#1#\r\n"];
        _transporte.Respostas["#202#"] = ["#202#x#\r\n"]; // ilegível
        await Conectar();

        var r = await _leitor.LerAsync();

        Assert.Equal("84 de 84", r.PlacarMarco1.Texto);
        Assert.Equal($"4 de {Catalogo.TotalExtras}", r.PlacarExtras.Texto);
        Assert.Equal("12,0 V", r.Item("005")!.Valor.Exibicao);
        Assert.Equal("#005#120#", r.Item("005")!.Cru);
        Assert.Equal("FILME", r.Item("231")!.Valor.Texto);
        Assert.Equal("0#MUSICA", r.Item("232")!.Valor.Texto);
        Assert.Equal("#232#0#MUSICA#", r.Item("232")!.Cru);
        Assert.True(r.Item("252")!.Valor.Ligado);

        var ilegivel = r.FalhaDe("202")!;
        Assert.Equal(Parametro.MotivoIlegivel, ilegivel.Motivo);
        Assert.Equal("#202#x#", ilegivel.Cru);

        var mudo = r.FalhaDe("150")!;
        Assert.True(mudo.Extra);
        Assert.Equal(LeitorCompleto.MotivoSemResposta, mudo.Motivo);
        Assert.Null(mudo.Cru);
        Assert.Equal(1, mudo.Tentativas);

        // Uma vez cada, sem nova tentativa.
        foreach (var c in Catalogo.ConsultasExtras)
            Assert.Single(_transporte.Escritas, c.Texto);
        Assert.Equal(Catalogo.TotalExtras - 4, r.Falhas.Count(f => f.Extra));
        Assert.True(r.Completo);
    }

    [Fact]
    public async Task Extra_dividido_em_duas_notificacoes_espera_o_fim_de_linha()
    {
        ResponderComOArquivoReal();
        _transporte.Respostas["#232#"] = ["#232#0#", "MUSICA#\r\n"];
        await Conectar();

        var r = await _leitor.LerAsync();

        Assert.Equal("0#MUSICA", r.Item("232")!.Valor.Texto);
        Assert.Equal("#232#0#MUSICA#", r.Item("232")!.Cru);
        Assert.Empty(r.Orfaos);
    }

    [Fact]
    public async Task Nome_vazio_de_extra_e_registrado()
    {
        ResponderComOArquivoReal();
        _transporte.Respostas["#231#"] = ["#231##\r\n"];
        await Conectar();

        var r = await _leitor.LerAsync();

        var item = r.Item("231")!;
        Assert.Equal("", item.Valor.Texto);
        Assert.Equal("(vazio)", item.Valor.Exibicao);
        Assert.Equal("#231##", item.Cru);
        Assert.Null(r.FalhaDe("231"));
        Assert.Empty(r.Orfaos);
    }

    [Fact]
    public async Task FimMarco1_fica_entre_a_consulta_84_e_a_85()
    {
        ResponderComOArquivoReal(); // extras mudos
        await Conectar();
        DateTimeOffset? t84 = null, t85 = null;
        _leitor.Avancou += p =>
        {
            if (p.Feitas == 84)
                t84 = DateTimeOffset.Now;
            if (p.Feitas == 85)
                t85 = DateTimeOffset.Now;
        };

        var r = await _leitor.LerAsync();

        Assert.NotNull(r.FimMarco1);
        Assert.True(t84 <= r.FimMarco1, $"t84 {t84:O} FimMarco1 {r.FimMarco1:O}");
        Assert.True(r.FimMarco1 <= t85, $"FimMarco1 {r.FimMarco1:O} t85 {t85:O}");
        Assert.True(r.FimMarco1 < r.Fim);
        Assert.Equal(r.FimMarco1 - r.Inicio, r.DuracaoMarco1);
    }

    // ----------------------------------------------------------------- aparelho mudo

    [Fact]
    public async Task Aparelho_mudo_para_depois_de_8_consultas_sem_resposta()
    {
        await Conectar(); // o transporte não responde a nenhuma consulta

        var r = await _leitor.LerAsync();

        Assert.False(r.Completo);
        Assert.Equal("incompleto", r.Status);
        Assert.Equal(LeitorCompleto.MsgParouDeResponder, r.Motivo);
        Assert.Equal(LeitorCompleto.MaximoMudasSeguidas, r.Falhas.Count);
        Assert.Equal(1 + 2 * LeitorCompleto.MaximoMudasSeguidas, _transporte.Escritas.Count); // com a nova tentativa
        Assert.Equal(EstadoSessao.Conectado, _sessao.Estado);
    }

    [Fact]
    public async Task Sete_mudas_seguidas_nao_param_a_leitura()
    {
        ResponderComOArquivoReal();
        foreach (var q in new[] { "#081#0#", "#081#1#", "#081#2#", "#081#3#", "#100#0#", "#100#1#", "#100#2#" })
            _transporte.Respostas.Remove(q);
        await Conectar();

        var r = await _leitor.LerAsync();

        Assert.True(r.Completo);
        Assert.Equal("77 de 84", r.PlacarMarco1.Texto);
        Assert.Equal(7, r.Falhas.Count(f => !f.Extra));
    }

    // ----------------------------------------------------------------- matriz

    [Fact]
    public async Task Banda_020_3_menos_1()
    {
        ResponderComOArquivoReal();
        _transporte.Respostas["020#3"] = ["020#3#-1.0\r\n"];
        await Conectar();

        var r = await _leitor.LerAsync();

        var banda = r.Item("020", 3)!;
        Assert.Equal(-1.0m, banda.Valor.Numero);
        Assert.Equal("banda 100 Hz", banda.Onde); // MAPA §4: banda 3 = 100 Hz (a 4 é 160 Hz)
        Assert.Equal("020#3#-1.0", banda.Cru);
    }

    [Fact]
    public async Task Banda_sem_fim_de_linha_tambem_casa()
    {
        ResponderComOArquivoReal();
        _transporte.Respostas["020#3"] = ["020#3#-1.0"];
        _transporte.Respostas["020#4"] = ["020#4#", "+1", ".0"];
        await Conectar();

        var r = await _leitor.LerAsync();

        Assert.Equal(-1.0m, r.Item("020", 3)!.Valor.Numero);
        Assert.Equal(1.0m, r.Item("020", 4)!.Valor.Numero);
        Assert.Equal("84 de 84", r.PlacarMarco1.Texto);
        Assert.Empty(r.Orfaos);
    }

    [Fact]
    public async Task Roteamento_ab_na_saida_3_e_A_mais_B()
    {
        ResponderComOArquivoReal();
        _transporte.Respostas["#010#2#"] = ["#010#2#ab#\r\n"];
        await Conectar();

        var r = await _leitor.LerAsync();

        var item = r.Item("010", 2)!;
        Assert.Equal("saída 3", item.Onde);
        Assert.Equal("a+b", item.Valor.Texto);
        Assert.Equal("A+B", item.Valor.Rotulo);
        Assert.Equal("#010#2#ab#", item.Cru);
    }

    [Fact]
    public async Task Threshold_0_5_e_limiter_OFF()
    {
        ResponderComOArquivoReal();
        _transporte.Respostas["#071#1#"] = ["#071#1#0.5#\r\n"];
        await Conectar();

        var r = await _leitor.LerAsync();

        var item = r.Item("071", 1)!;
        Assert.Equal("saída 2", item.Onde);
        Assert.Equal("OFF", item.Valor.Rotulo);
        Assert.Equal(0.5m, item.Valor.Numero);
        Assert.False(item.Valor.ForaDaFaixa);
    }

    [Fact]
    public async Task Fora_da_faixa_conta_como_lido_e_fica_marcado()
    {
        ResponderComOArquivoReal();
        _transporte.Respostas["#060#0#"] = ["#060#0#90#\r\n"];
        await Conectar();

        var r = await _leitor.LerAsync();

        var item = r.Item("060", 0)!;
        Assert.Equal(90m, item.Valor.Numero);
        Assert.True(item.Valor.ForaDaFaixa);
        Assert.Equal("90 (fora da faixa)", item.Valor.Exibicao);
        Assert.Equal("84 de 84", r.PlacarMarco1.Texto);
    }

    [Fact]
    public async Task Sem_resposta_ganha_uma_nova_tentativa()
    {
        ResponderComOArquivoReal();
        _transporte.MudasNaPrimeira.Add("#081#1#");
        await Conectar();

        var r = await _leitor.LerAsync();

        Assert.Equal("84 de 84", r.PlacarMarco1.Texto);
        Assert.Equal(2, r.Item("081", 1)!.Tentativas);
        Assert.Equal(1, r.Item("081", 0)!.Tentativas);
        Assert.Equal(2, _transporte.Escritas.Count(e => e == "#081#1#"));
        Assert.Equal(1, _transporte.Escritas.Count(e => e == "#081#2#"));
    }

    [Fact]
    public async Task Sem_resposta_duas_vezes_vira_falha_e_a_leitura_segue()
    {
        ResponderComOArquivoReal();
        _transporte.Respostas.Remove("#081#1#");
        await Conectar();

        var r = await _leitor.LerAsync();

        Assert.True(r.Completo);
        Assert.Equal("83 de 84", r.PlacarMarco1.Texto);
        var falha = r.FalhaDe("081", 1)!;
        Assert.Equal(LeitorCompleto.MotivoSemResposta, falha.Motivo);
        Assert.Equal(2, falha.Tentativas);
        Assert.Equal(2, _transporte.Escritas.Count(e => e == "#081#1#"));
        Assert.NotNull(r.Item("220")); // a última do Marco 1 foi lida
    }

    [Fact]
    public async Task Resposta_ilegivel_tenta_de_novo_e_guarda_o_cru()
    {
        ResponderComOArquivoReal();
        _transporte.Respostas["#081#2#"] = ["#081#2#lixo#\r\n"];
        await Conectar();

        var r = await _leitor.LerAsync();

        var falha = r.FalhaDe("081", 2)!;
        Assert.Equal(Parametro.MotivoIlegivel, falha.Motivo);
        Assert.Equal("#081#2#lixo#", falha.Cru);
        Assert.Equal(2, _transporte.Escritas.Count(e => e == "#081#2#"));
    }

    [Fact]
    public async Task Queda_na_consulta_40_para_com_39_lidos()
    {
        ResponderComOArquivoReal();
        _transporte.DerrubarNaConsulta = 40;
        await Conectar();

        var r = await _leitor.LerAsync();

        Assert.False(r.Completo);
        Assert.Equal("incompleto", r.Status);
        Assert.Equal(Sessao.MsgCaiu, r.Motivo);
        Assert.Equal("39 de 84", r.PlacarMarco1.Texto);
        Assert.Null(_sessao.Firmware); // a sessão já apagou...
        Assert.Equal("R003", r.Firmware); // ...mas o retrato guardou o do início
        Assert.Contains("\"firmware\": \"R003\"", r.ParaJson());
        Assert.Empty(r.Falhas);
        Assert.Equal(40, _transporte.Escritas.Count - 1); // nada saiu depois da queda
        Assert.Equal(EstadoSessao.Desconectado, _sessao.Estado);
        Assert.NotNull(r.Fim);
        SoSenhaEConsultasDoCatalogo();
    }

    [Fact]
    public async Task Sem_conexao_o_retrato_fica_incompleto_sem_envio()
    {
        var r = await _leitor.LerAsync();

        Assert.False(r.Completo);
        Assert.Equal("0 de 84", r.PlacarMarco1.Texto);
        Assert.Empty(_transporte.Escritas);
    }

    [Fact]
    public async Task Orfao_no_meio_nao_estraga_a_consulta_pendente()
    {
        ResponderComOArquivoReal();
        _transporte.AntesDaConsulta["#081#0#"] = ["#150#3#\r\n"];
        _transporte.AntesDaConsulta["#081#1#"] = ["#150#3#"]; // sem fim de linha, colado na resposta
        await Conectar();

        var r = await _leitor.LerAsync();

        Assert.Equal(10.0m, r.Item("081", 0)!.Valor.Numero);
        Assert.Equal(7.0m, r.Item("081", 1)!.Valor.Numero);
        Assert.Equal(["#150#3#", "#150#3#"], r.Orfaos);
        Assert.Equal("84 de 84", r.PlacarMarco1.Texto);
    }

    [Fact]
    public async Task Eco_do_envio_vai_para_os_orfaos()
    {
        ResponderComOArquivoReal();
        _transporte.AntesDaConsulta["#081#0#"] = ["#081#0#\r\n"];
        _transporte.AntesDaConsulta["#080#"] = ["#080#\r\n"];
        _transporte.AntesDaConsulta["020#0"] = ["020#0\r\n"];
        await Conectar();

        var r = await _leitor.LerAsync();

        Assert.Equal(["#080#", "#081#0#", "020#0"], r.Orfaos);
        Assert.Equal("84 de 84", r.PlacarMarco1.Texto);
    }

    [Fact]
    public async Task Resposta_de_outro_canal_e_orfao()
    {
        ResponderComOArquivoReal();
        _transporte.Respostas["#081#1#"] = ["#081#2#+6.0#\r\n"];
        await Conectar();

        var r = await _leitor.LerAsync();

        Assert.Null(r.Item("081", 1));
        Assert.Equal(LeitorCompleto.MotivoSemResposta, r.FalhaDe("081", 1)!.Motivo);
        Assert.Equal(["#081#2#+6.0#", "#081#2#+6.0#"], r.Orfaos);
    }

    [Fact]
    public async Task Resposta_fragmentada_e_juntada()
    {
        ResponderComOArquivoReal();
        _transporte.Respostas["#081#0#"] = ["#081#", "0#+1", "0.0#", "\r\n"];
        await Conectar();

        var r = await _leitor.LerAsync();

        Assert.Equal(10.0m, r.Item("081", 0)!.Valor.Numero);
        Assert.Empty(r.Orfaos);
    }

    [Fact]
    public async Task O_progresso_chega_a_cada_consulta()
    {
        ResponderComOArquivoReal();
        await Conectar();
        var feitas = new List<int>();
        _leitor.Avancou += r => feitas.Add(r.Feitas);

        await _leitor.LerAsync();

        Assert.Equal(Enumerable.Range(1, Catalogo.Consultas.Count), feitas);
    }

    [Fact]
    public async Task Duas_leituras_ao_mesmo_tempo_nao_rodam()
    {
        ResponderComOArquivoReal();
        await Conectar();

        var primeira = _leitor.LerAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => _leitor.LerAsync());
        var r = await primeira;

        Assert.Equal("84 de 84", r.PlacarMarco1.Texto);
        Assert.False(_leitor.Lendo);
    }

    [Fact]
    public async Task Ler_de_novo_faz_outra_leitura_inteira()
    {
        ResponderComOArquivoReal();
        await Conectar();

        await _leitor.LerAsync();
        var segunda = await _leitor.LerAsync();

        Assert.Equal("84 de 84", segunda.PlacarMarco1.Texto);
        Assert.Empty(segunda.Orfaos);
        Assert.Equal(1 + 2 * Catalogo.Consultas.Count, _transporte.Escritas.Count);
    }

    [Fact]
    public async Task Retrato_vira_json_com_valor_e_cru()
    {
        ResponderComOArquivoReal();
        await Conectar();

        var json = (await _leitor.LerAsync()).ParaJson();

        using var doc = JsonDocument.Parse(json);
        var raiz = doc.RootElement;
        Assert.Equal("completo", raiz.GetProperty("status").GetString());
        Assert.Equal(84, raiz.GetProperty("placarMarco1").GetProperty("lidos").GetInt32());
        var fase = raiz.GetProperty("lidos").EnumerateArray()
            .First(l => l.GetProperty("codigo").GetString() == "060" && l.GetProperty("indice").GetInt32() == 2);
        Assert.Equal("#060#2#180#", fase.GetProperty("cru").GetString());
        Assert.Equal(180m, fase.GetProperty("valor").GetProperty("numero").GetDecimal());
        Assert.Contains("180°", json); // sem °
        Assert.DoesNotContain("AB12", json);
    }

    // ----------------------------------------------------------------- trava

    [Theory]
    [InlineData("#004#")]
    [InlineData("#000#")]
    [InlineData("#080#0#")]
    [InlineData("#081#")]
    [InlineData("#081#4#")]
    [InlineData("#020#3#")]
    [InlineData("#999#")]
    public async Task Leitura_fora_do_catalogo_e_recusada_sem_envio_e_registrada(string quadro)
    {
        await Conectar();

        var r = await _sessao.EnviarAsync(quadro);

        Assert.False(r.Enviado);
        Assert.Equal(Sessao.MotivoForaDoCatalogo, r.Motivo);
        Assert.Equal(["*000*AB12*"], _transporte.Escritas);
        Assert.Contains(_log.Linhas, l => l.Contains($"✗ {quadro}") && l.Contains(Sessao.MotivoForaDoCatalogo));
    }

    [Theory]
    [InlineData("*081*0*-3.0*", Sessao.MotivoChaveDesligada)]
    [InlineData("020*3*2.5", Sessao.MotivoChaveDesligada)]
    [InlineData("*202*1*", Sessao.MotivoChaveDesligada)] // gerador: chave "Gerador" (história 7a)
    [InlineData("*202*2*", Sessao.MotivoGeradorForaDoFormato)]
    [InlineData("*252*1*", Sessao.MotivoChaveDesligada)] // remote out: chave "Remote out" (história 7b)
    [InlineData("*252*2*", Sessao.MotivoAparelhoForaDoFormato)]
    [InlineData("*240*0*", Sessao.MotivoChaveDesligada)]
    [InlineData("*241*0*", Sessao.MotivoChaveDesligada)]
    [InlineData("*231*FILME*", Sessao.MotivoChaveDesligada)]
    [InlineData("*240*5*", Sessao.MotivoMemoriaForaDoFormato)]
    public async Task Escrita_codificada_pelo_catalogo_sem_chaves_e_recusada_com_o_motivo(string quadro, string motivo)
    {
        // Esta sessão não tem IChaves: toda escrita conhecida cai na chave; fora da forma exata, no formato.
        await Conectar();

        var r = await _sessao.EnviarAsync(quadro);

        Assert.False(r.Enviado);
        Assert.Equal(motivo, r.Motivo);
        Assert.Equal(["*000*AB12*"], _transporte.Escritas);
        Assert.Contains(_log.Linhas, l => l.Contains($"✗ {quadro}") && l.Contains(motivo));
    }

    [Fact]
    public async Task Sem_chaves_toda_escrita_do_codificador_e_recusada_pela_trava()
    {
        await Conectar();
        var quadros = new List<string>();
        foreach (var p in Catalogo.Todos.Where(p => !p.SomenteLeitura))
        {
            int? indice = p.Escopo == Escopo.Global ? null : 0;
            quadros.Add(p.Tipo switch
            {
                TipoValor.Roteamento => p.CodificarEscrita(indice, "a"),
                TipoValor.Nome => p.CodificarEscrita(indice, "X"),
                TipoValor.Booleano => p.CodificarEscrita(indice, true),
                _ when p.Opcoes is not null => p.CodificarEscrita(indice, p.Opcoes.Keys.First()),
                _ => p.CodificarEscrita(indice, p.Minimo ?? 0m),
            });
        }

        foreach (var q in quadros)
            Assert.False((await _sessao.EnviarAsync(q)).Enviado, q);
        Assert.Equal(["*000*AB12*"], _transporte.Escritas);
    }

    [Fact]
    public async Task Quadro_recebido_conectado_vai_ao_log_e_ao_evento()
    {
        await Conectar();
        var recebidos = new List<string>();
        _sessao.Recebeu += recebidos.Add;

        _transporte.Emitir("#005#120#\r\n");
        _transporte.Emitir("020#3#-1.0");

        Assert.Equal(["#005#120#", "020#3#-1.0"], recebidos);
        Assert.Contains(_log.Linhas, l => l.Contains("← #005#120#"));
        Assert.Contains(_log.Linhas, l => l.Contains("← 020#3#-1.0"));
    }
}
