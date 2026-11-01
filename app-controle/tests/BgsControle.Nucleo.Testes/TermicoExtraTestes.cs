using System.Text.Json;
using BgsControle.Nucleo.Player;
using BgsControle.Nucleo.Porta;
using BgsControle.Nucleo.Rendimento;
using BgsControle.Nucleo.Setups;
using BgsControle.Simulador;

namespace BgsControle.Nucleo.Testes;

/// <summary>
/// História 13f, o modelo térmico ao vivo do Extra contra o processador simulado (<see cref="BancadaExtra"/>, com o relógio
/// falso): a PLANA com o teste a seco feito e o Extra escolhido, tocando os senos da análise. Uma linha da matriz por teste.
/// Os dados térmicos de teste das JBL são curtos (τ de segundos), para o corte vir no tempo de um teste.
/// </summary>
public sealed class TermicoExtraTestes
{
    /// <summary>As JBL esquentam rápido (150 °C, 2 + 1,5 K/W, τ 2 s e 6 s): o corte vem em segundos e o descanso dura ~20 s.</summary>
    private static Setup Rapido() => CasaDoDenis.ComAltoFalantes(CasaDoDenis.SetupCasaComTermico, e => e.Id == CasaDoDenis.IdSubCasa
        ? e.Dados
        : CasaDoDenis.ComTermico(e.Dados, 150m, 2m, 1.5m, 2m, 6m));

    /// <summary>
    /// O ímã das JBL é lento (150 °C, 2,4 + 3 K/W, τ 2 s e 600 s): o corte vem em ~30 s, quase todo pela bobina, e o
    /// descanso dura uns 20 min (o ímã esfria devagar).
    /// </summary>
    private static Setup Lento() => CasaDoDenis.ComAltoFalantes(CasaDoDenis.SetupCasaComTermico, e => e.Id == CasaDoDenis.IdSubCasa
        ? e.Dados
        : CasaDoDenis.ComTermico(e.Dados, 150m, 2.4m, 3m, 2m, 600m));

    /// <summary>Todas as bobinas rápidas (o Sub também): um descanso de precaução acaba em segundos.</summary>
    private static Setup TodosRapidos() => CasaDoDenis.ComAltoFalantes(CasaDoDenis.SetupCasaComTermico,
        e => CasaDoDenis.ComTermico(e.Dados, 150m, 2m, 1.5m, 2m, 6m));

    /// <summary>As JBL com Rth altas e τ curtos: no Máximo a bobina passa da máxima em segundos.</summary>
    private static Setup Quente() => CasaDoDenis.ComAltoFalantes(CasaDoDenis.SetupCasaComTermico, e => e.Id == CasaDoDenis.IdSubCasa
        ? e.Dados
        : CasaDoDenis.ComTermico(e.Dados, 150m, 30m, 20m, 2m, 6m));

    /// <summary>Troca os dados de um alto-falante do Casa 2.1 na bancada (como a ficha na tela).</summary>
    private static async Task Editar(BancadaExtra b, string id, Func<DadosTecnicos, DadosTecnicos> mudar)
    {
        var e = b.Setups.SetupEmUso!.Equipamentos.Single(x => x.Id == id);
        var r = await b.Rodar(b.Setups.EditarEquipamentoAsync(CasaDoDenis.IdSetupCasa, e with { Dados = mudar(e.Dados) }));
        Assert.True(r.Ok, r.Motivo);
    }

    /// <summary>Anda o relógio em passos de 10 s (os passos de 1 s do modelo vêm dentro), até a condição.</summary>
    private static async Task Longo(BancadaExtra b, double segundos, Func<bool> ate)
    {
        for (var i = 0; i < segundos / 10 && !ate(); i++)
        {
            b.Relogio.Avancar(TimeSpan.FromSeconds(10));
            await Task.Delay(1);
        }
        Assert.True(ate(), "a condição não ficou verdadeira no tempo");
    }

    private static IEnumerable<string?> Mutes(BancadaExtra b) => Enumerable.Range(0, 4).Select(c => b.Simulador.Valor("100", c));

    /// <summary>Prepara, valida e escolhe o Extra.</summary>
    private static async Task NoExtra(BancadaExtra b)
    {
        await b.Preparar();
        await b.Validar();
        var r = await b.Rodar(b.Setups.DefinirNivelAsync(BancadaExtra.IdPlana, NivelRendimento.Extra));
        Assert.True(r.Ok, r.Motivo);
    }

    private static async Task<TermicoExtra> Termico(BancadaExtra b)
    {
        var t = b.NovoTermico();
        await b.AndarAte(() => t.Carregado.IsCompleted);
        return t;
    }

    private static async Task Tocar(BancadaExtra b, string uri = "seno-3")
    {
        await b.Rodar(b.Player.TocarFaixaAsync(b.Faixa(uri).Id));
        Assert.Equal(EstadoPlayer.Tocando, b.Player.Estado);
    }

    /// <summary>Anda o relógio até <paramref name="segundos"/> s (em passos de 0,1 s), parando quando a condição vale.</summary>
    private static async Task Segundos(BancadaExtra b, double segundos, Func<bool>? ate = null)
    {
        for (var i = 0; i < segundos * 10 && ate?.Invoke() != true; i++)
        {
            b.Relogio.Avancar(TimeSpan.FromMilliseconds(100));
            await Task.Delay(1);
        }
        if (ate is not null)
            Assert.True(ate(), "a condição não ficou verdadeira no tempo");
    }

    /// <summary>Toca até o corte e a trava confirmada no aparelho.</summary>
    private static async Task AteOCorte(BancadaExtra b, TermicoExtra t, double segundos = 60)
    {
        await Tocar(b);
        await Segundos(b, segundos, () => t.EmDescanso);
        await b.AndarAte(() => t.BluetoothTravado && b.Player.Estado == EstadoPlayer.Pausado);
    }

    [Fact]
    public async Task Corte_no_Extra_pausa_o_player_muta_os_4_canais_e_desliga_o_251()
    {
        using var b = new BancadaExtra(Rapido());
        await NoExtra(b);
        var t = await Termico(b);
        Assert.True(t.Avaliacao.Pode, t.Avaliacao.Motivo);
        Assert.Equal(["0", "0", "0", "0"], Mutes(b));

        await Tocar(b);
        await Segundos(b, 1.5);
        // O tempo de Extra: o da solução fechada com a potência da faixa atual.
        var tempo = t.TempoExtra;
        Assert.NotNull(tempo);
        Assert.InRange(tempo.Value.TotalSeconds, 0.5, 30);
        Assert.Matches(@"^Extra: \d\d:\d\d$", t.TextoTempoExtra!);

        await Segundos(b, 40, () => t.EmDescanso);
        Assert.Equal("JBL L", t.CortadoPor);
        await b.AndarAte(() => t.BluetoothTravado && b.Player.Estado == EstadoPlayer.Pausado);
        Assert.Equal(["1", "1", "1", "1"], Mutes(b));
        Assert.Equal("0", b.Simulador.Valor("251"));
        Assert.Contains("*251*0*", b.Simulador.Escritas);
        Assert.Null(t.Aviso);
        Assert.Null(t.TempoExtra);
        Assert.StartsWith("Descansando: ", t.TextoDescanso);
        // A bobina chegou à máxima, sem folga.
        Assert.True(t.AltoFalantes.Single(a => a.AltoFalante == "JBL L").Temperatura >= 149.9);

        // No descanso o player não toca, e o teste a seco não começa (sem o Bluetooth, nenhum clip acusaria nada).
        await b.Rodar(b.Player.TocarPausarAsync());
        Assert.Equal(EstadoPlayer.Pausado, b.Player.Estado);
        Assert.StartsWith("Descansando: ", b.Player.Aviso);
        Assert.StartsWith("Descansando: ", b.Teste.Abrir().Motivo);

        // A tela e a porta não desfazem a trava: religar o 251 e tirar o mudo são recusados.
        var religar = await b.Rodar(b.Comandos.DefinirBluetoothAudioAsync(true));
        Assert.False(religar.Ok);
        Assert.Matches(@"^Descansando: \d\d:\d\d\.$", religar.Motivo!);
        var desmutar = await b.Rodar(b.Escritor.EscreverAsync(Catalogo.ParametroDe("100")!, 0, 0m));
        Assert.False(desmutar.Ok);
        Assert.StartsWith("Descansando: ", desmutar.Motivo);
        Assert.Equal(["1", "1", "1", "1"], Mutes(b));
        Assert.Equal("0", b.Simulador.Valor("251"));
        Assert.DoesNotContain("*251*1*", b.Simulador.Escritas);

        // O descanso está no termico.json.
        using var doc = JsonDocument.Parse((await b.ArquivoTermico.LerAsync())!);
        Assert.Equal("JBL L", doc.RootElement.GetProperty("descanso").GetProperty("altoFalante").GetString());
        Assert.True(doc.RootElement.GetProperty("trava").GetProperty("bluetooth").GetBoolean());
        Assert.Equal(4, doc.RootElement.GetProperty("trava").GetProperty("mutados").GetArrayLength());
    }

    [Fact]
    public async Task O_mudo_que_nao_confirma_e_repetido_com_o_aviso()
    {
        using var b = new BancadaExtra(Rapido());
        await NoExtra(b);
        var t = await Termico(b);
        b.Simulador.EcosDiferentes["*100*0*1*"] = "#100#0#0#";

        await Tocar(b);
        await Segundos(b, 40, () => t.EmDescanso);
        await b.AndarAte(() => t.Aviso is not null);
        Assert.Equal("Não consegui travar o Bluetooth. Desligue o módulo.", t.Aviso);
        Assert.False(t.BluetoothTravado);
        // O app tenta de novo.
        await Segundos(b, 12, () => b.Simulador.Escritas.Count(e => e == "*100*0*1*") >= 2);

        // O aparelho volta a confirmar: travado, e o aviso sai.
        b.Simulador.EcosDiferentes.Clear();
        await Segundos(b, 12, () => t.BluetoothTravado && t.Aviso is null);
        Assert.Equal(["1", "1", "1", "1"], Mutes(b));
        Assert.Equal("0", b.Simulador.Valor("251"));
    }

    [Fact]
    public async Task Sem_conexao_no_corte_o_descanso_comeca_e_a_trava_vai_ao_reconectar()
    {
        using var b = new BancadaExtra(Rapido());
        await NoExtra(b);
        var t = await Termico(b);
        await Tocar(b);

        // A conexão cai: a música segue pelo Bluetooth de áudio, e o corte vale do mesmo jeito.
        b.Simulador.Derrubar();
        await b.AndarAte(() => b.Player.Retida);
        Assert.Equal(EstadoPlayer.Tocando, b.Player.Estado);
        await Segundos(b, 40, () => t.EmDescanso);
        await b.AndarAte(() => b.Player.Estado == EstadoPlayer.Pausado);
        Assert.DoesNotContain("*251*0*", b.Simulador.Escritas);
        Assert.False(t.BluetoothTravado);

        // Ao reconectar, o travamento é escrito.
        await b.Sessao.ConectarAsync();
        await b.AndarAte(() => b.Estado.Pronto && !b.Estado.Lendo && b.Setups.Conferido);
        await Segundos(b, 15, () => t.BluetoothTravado);
        Assert.Equal(["1", "1", "1", "1"], Mutes(b));
        Assert.Equal("0", b.Simulador.Valor("251"));
    }

    [Fact]
    public async Task No_fim_do_descanso_o_251_volta_o_mudo_sai_e_o_player_toca_de_novo()
    {
        using var b = new BancadaExtra(Rapido());
        await NoExtra(b);
        var t = await Termico(b);
        await AteOCorte(b, t);
        var descanso = t.Descanso!.Value.TotalSeconds;
        Assert.InRange(descanso, 5, 60);

        await Segundos(b, descanso + 10, () => !t.EmDescanso && !t.TravaNoAparelho);
        Assert.Equal("1", b.Simulador.Valor("251"));
        Assert.Equal(["0", "0", "0", "0"], Mutes(b));
        Assert.Contains("*251*1*", b.Simulador.Escritas);
        Assert.Null(t.TextoDescanso);
        // Toda bobina voltou a ambiente + 1 °C.
        Assert.All(t.AltoFalantes, a => Assert.True(a.Temperatura <= 26.05, $"{a.AltoFalante}: {a.Temperatura}"));

        // O player tocava no corte: ele toca de novo sozinho.
        await b.AndarAte(() => b.Player.Estado == EstadoPlayer.Tocando);
        Assert.Null(b.Player.Aviso);
    }

    [Fact]
    public async Task Fechar_no_descanso_e_reabrir_10_min_depois_o_descanso_continua_pelo_relogio_com_o_mudo_no_aparelho()
    {
        using var b = new BancadaExtra(Lento());
        await NoExtra(b);
        var t = await Termico(b);
        await AteOCorte(b, t, segundos: 90);
        var resta = t.Descanso!.Value.TotalSeconds;
        Assert.InRange(resta, 15 * 60, 30 * 60);

        // Fechar o app: o estado vai para o termico.json, e a trava fica no aparelho.
        Assert.True(await t.GravarAgoraAsync());
        t.Dispose();
        var escritas = b.Simulador.Escritas.Count;
        for (var i = 0; i < 60; i++)
        {
            b.Relogio.Avancar(TimeSpan.FromSeconds(10));
            await Task.Delay(1);
        }
        Assert.Equal(["1", "1", "1", "1"], Mutes(b));
        Assert.Equal("0", b.Simulador.Valor("251"));

        // Reabrir: o descanso continua pelo relógio (os 10 min fora contam como esfriamento).
        var reaberto = await Termico(b);
        Assert.True(reaberto.EmDescanso);
        Assert.Equal("JBL L", reaberto.CortadoPor);
        Assert.InRange(reaberto.Descanso!.Value.TotalSeconds, resta - 600 - 5, resta - 600 + 5);
        await Segundos(b, 3);
        Assert.True(reaberto.EmDescanso);
        Assert.True(reaberto.BluetoothTravado);
        Assert.DoesNotContain(b.Simulador.Escritas.Skip(escritas), e => e is "*251*1*" || e.StartsWith("*100*", StringComparison.Ordinal));
        await b.Rodar(b.Player.TocarPausarAsync());
        Assert.StartsWith("Descansando: ", b.Player.Aviso);
    }

    [Fact]
    public async Task Fechar_no_descanso_e_reabrir_depois_do_fim_religa_e_desmuta()
    {
        using var b = new BancadaExtra(Rapido());
        await NoExtra(b);
        var t = await Termico(b);
        await AteOCorte(b, t);
        Assert.True(await t.GravarAgoraAsync());
        t.Dispose();

        for (var i = 0; i < 60; i++)
        {
            b.Relogio.Avancar(TimeSpan.FromSeconds(10));
            await Task.Delay(1);
        }
        Assert.Equal("0", b.Simulador.Valor("251"));

        var reaberto = await Termico(b);
        await Segundos(b, 15, () => !reaberto.EmDescanso && !reaberto.TravaNoAparelho);
        Assert.Equal("1", b.Simulador.Valor("251"));
        Assert.Equal(["0", "0", "0", "0"], Mutes(b));
    }

    [Fact]
    public async Task Sem_o_TauIma_do_Sub_o_Extra_nao_se_escolhe_e_o_motivo_tem_o_nome_dele()
    {
        using var b = new BancadaExtra(CasaDoDenis.ComAltoFalantes(CasaDoDenis.SetupCasaComTermico,
            e => e.Id == CasaDoDenis.IdSubCasa ? e.Dados with { TauIma = null } : e.Dados));
        await b.Preparar();
        await b.Validar();

        Assert.Equal("Falta o modelo térmico do Sub.", b.Setups.MotivoSemExtra(b.Plana));
        var r = await b.Rodar(b.Setups.DefinirNivelAsync(BancadaExtra.IdPlana, NivelRendimento.Extra));
        Assert.False(r.Ok);
        Assert.Equal("Falta o modelo térmico do Sub.", r.Motivo);
        Assert.NotEqual(NivelRendimento.Extra, b.Setups.NivelEmUso);
        var t = await Termico(b);
        Assert.False(t.Avaliacao.Pode);
        Assert.Null(t.AltoFalantes.Single(a => a.AltoFalante == "Sub").Temperatura);
    }

    [Fact]
    public async Task Um_dado_termico_so_digitado_deixa_escolher_o_Extra_com_o_selo_estimado()
    {
        using var b = new BancadaExtra(CasaDoDenis.ComAltoFalantes(CasaDoDenis.SetupCasaComTermico,
            e => e.Id == CasaDoDenis.IdJblR ? e.Dados with { TemperaturaMaximaBobina = DadoTecnico.Digitado(150m) } : e.Dados));
        await NoExtra(b);

        Assert.Equal(NivelRendimento.Extra, b.Setups.NivelEmUso);
        Assert.True(b.Setups.TermicoEmUso.Pode);
        Assert.Equal("estimado", b.Setups.TermicoEmUso.Selo);
        Assert.Null(b.Setups.MotivoSemExtra(b.Plana));
    }

    [Fact]
    public async Task Escolher_o_Extra_com_o_teste_a_seco_e_os_dados_termicos_vale()
    {
        using var b = new BancadaExtra();
        await NoExtra(b);

        Assert.Equal(NivelRendimento.Extra, b.Setups.NivelEmUso);
        Assert.Null(b.Setups.TermicoEmUso.Selo);
        Assert.Equal(b.Plana.ExtraValidado[BancadaExtra.IdPlano].Master, b.MasterNoAparelho);
    }

    [Fact]
    public async Task Parar_no_meio_e_esfriar_5_min_segue_a_solucao_exata()
    {
        using var b = new BancadaExtra(Lento());
        await NoExtra(b);
        var t = await Termico(b);
        await Tocar(b);
        await Segundos(b, 5);
        Assert.True(t.Estado(CasaDoDenis.IdJblL)!.Value.Total > 10);

        await b.Rodar(b.Player.PausarAsync("no teste"));
        b.Relogio.Avancar(TimeSpan.FromSeconds(1));
        var antes = t.Estado(CasaDoDenis.IdJblL)!.Value;
        for (var i = 0; i < 300; i++)
            b.Relogio.Avancar(TimeSpan.FromSeconds(1));
        var depois = t.Estado(CasaDoDenis.IdJblL)!.Value;

        var dados = DadosTermicos.De(b.Setups.SetupEmUso!.Equipamentos.Single(e => e.Id == CasaDoDenis.IdJblL).Dados)!;
        var exato = ModeloTermico.Passo(antes, dados, 0, 300);
        Assert.Equal(exato.Bobina, depois.Bobina, 6);
        Assert.Equal(exato.Ima, depois.Ima, 6);
        Assert.True(depois.Total < antes.Total);
    }

    [Fact]
    public async Task Com_35_graus_no_Setup_o_tempo_de_Extra_encurta()
    {
        using var b = new BancadaExtra(Lento());
        await NoExtra(b);
        var t = await Termico(b);
        await Tocar(b);
        await Segundos(b, 1.5);
        var a25 = t.TempoExtra!.Value.TotalSeconds;

        var r = await b.Rodar(b.Setups.DefinirTemperaturaAmbienteAsync(CasaDoDenis.IdSetupCasa, DadoTecnico.Digitado(35m)));
        Assert.True(r.Ok, r.Motivo);
        Assert.Equal("Casa 2.1: 35 °C.", r.Mensagem);
        var a35 = t.TempoExtra!.Value.TotalSeconds;
        Assert.True(a35 < a25, $"{a35} < {a25}");
        // A temperatura ambiente não põe a memória em "revalidar".
        Assert.False(b.Plana.Revalidar);

        var fora = await b.Rodar(b.Setups.DefinirTemperaturaAmbienteAsync(CasaDoDenis.IdSetupCasa, DadoTecnico.Digitado(80m)));
        Assert.False(fora.Ok);
        Assert.Equal(GerenteSetups.MsgTemperaturaInvalida, fora.Motivo);
    }

    [Fact]
    public async Task A_porta_mostra_o_termico_o_tempo_de_Extra_e_o_descanso_e_a_temperatura_no_estado()
    {
        using var b = new BancadaExtra(Rapido());
        await NoExtra(b);
        var t = await Termico(b);
        var (porta, chave) = await b.PortaPareada(t);
        using var _ = porta;
        await Tocar(b);
        await Segundos(b, 1.5);

        var r = await porta.AtenderAsync(PedidoPorta.De("GET", "/limites", BancadaExtra.Casa, bearer: chave));
        Assert.Equal(200, r.Status);
        var extra = JsonDocument.Parse(r.Json).RootElement.GetProperty("extra");
        Assert.True(extra.GetProperty("termicoPode").GetBoolean());
        Assert.Equal(JsonValueKind.Null, extra.GetProperty("termicoMotivo").ValueKind);
        var termico = extra.GetProperty("termico").EnumerateArray().ToArray();
        Assert.Equal(["JBL L", "JBL R", "Sub"], termico.Select(x => x.GetProperty("altoFalante").GetString()));
        Assert.Equal(150, termico[0].GetProperty("maxima").GetDouble());
        Assert.True(termico[0].GetProperty("temperatura").GetDouble() > 25);
        Assert.Equal("verificado", termico[0].GetProperty("selo").GetString());
        Assert.True(extra.GetProperty("tempoExtra").GetInt64() > 0);
        Assert.Equal(JsonValueKind.Null, extra.GetProperty("descanso").ValueKind);

        await Segundos(b, 40, () => t.EmDescanso);
        await b.AndarAte(() => t.BluetoothTravado);
        r = await porta.AtenderAsync(PedidoPorta.De("GET", "/limites", BancadaExtra.Casa, bearer: chave));
        extra = JsonDocument.Parse(r.Json).RootElement.GetProperty("extra");
        Assert.True(extra.GetProperty("descanso").GetInt64() > 0);
        Assert.Equal(JsonValueKind.Null, extra.GetProperty("tempoExtra").ValueKind);
        Assert.True(extra.GetProperty("bluetoothTravado").GetBoolean());

        // O /estado: sem o dado, a temperatura que vale é 25 °C.
        var estado = await porta.AtenderAsync(PedidoPorta.De("GET", "/estado", BancadaExtra.Casa, bearer: chave));
        var setup = JsonDocument.Parse(estado.Json).RootElement.GetProperty("setup");
        Assert.Equal(JsonValueKind.Null, setup.GetProperty("temperaturaAmbiente").ValueKind);
        Assert.Equal(25, setup.GetProperty("temperaturaAmbienteUsada").GetDouble());
    }

    // ------------------------------------------------------------------ o caminho ao vivo e as guardas

    [Fact]
    public async Task Ao_vivo_com_40_W_constantes_corta_em_832_s_e_descansa_2284_s()
    {
        using var b = new BancadaExtra();
        await NoExtra(b);
        // A potência que o seno de −3 dBFS põe nas JBL no master validado; as Rth das JBL são as do critério (2 e 1,5 K/W a
        // 40 W) passadas para essa potência: a mesma curva de temperatura que 40 W constantes.
        var rms = b.Analise.AnalisesEmUso()[b.Faixa("seno-3").Id].Canais.Single(c => c.Canal == 0).Rms;
        var p = ModeloTermico.Potencia(rms, (double)b.MasterNoAparelho, 19.5, 4);
        Assert.True(p > 1, $"{p} W");
        foreach (var id in new[] { CasaDoDenis.IdJblL, CasaDoDenis.IdJblR })
            await Editar(b, id, d => CasaDoDenis.ComTermico(d, 150m, (decimal)(2 * 40 / p), (decimal)(1.5 * 40 / p), 15m, 600m));
        // O Sub quase não esquenta com o seno de 1 kHz: Rth mínimas, para ele não pesar no descanso.
        await Editar(b, CasaDoDenis.IdSubCasa, d => CasaDoDenis.ComTermico(d, 180m, 0.001m, 0.001m, 20m, 900m));
        Assert.True(b.Setups.TermicoEmUso.Pode, b.Setups.TermicoEmUso.Motivo);
        Assert.Equal(NivelRendimento.Extra, b.Setups.NivelEmUso);
        var t = await Termico(b);

        await Tocar(b);
        var inicio = b.Relogio.GetUtcNow();
        await Longo(b, 1200, () => t.EmDescanso);
        var corte = (t.DescansoDesde!.Value - inicio).TotalSeconds;
        Assert.True(Math.Abs(corte - 832) <= 0.05 * 832, $"corte em {corte:0} s");

        await b.AndarAte(() => t.BluetoothTravado);
        await Longo(b, 3500, () => !t.EmDescanso);
        var descanso = (b.Relogio.GetUtcNow() - inicio).TotalSeconds - corte;
        Assert.True(Math.Abs(descanso - 2284) <= 0.05 * 2284, $"descanso de {descanso:0} s");
    }

    [Fact]
    public async Task Uma_faixa_sem_analise_usa_o_RMS_da_faixa_mais_forte()
    {
        using var b = new BancadaExtra(Lento());
        await NoExtra(b);
        b.Decodificador.Ausente("sumida");
        var r = await b.Playlists.AdicionarAsync(BancadaExtra.IdPlana, [new ArquivoAudio("sumida", "sumida", "Testes", TimeSpan.FromSeconds(2))]);
        Assert.True(r.Ok, r.Mensagem);
        await b.Pronta();
        Assert.False(b.Analise.AnalisesEmUso().ContainsKey(b.Faixa("sumida").Id));
        var t = await Termico(b);

        await Tocar(b, "sumida");
        await Segundos(b, 1.5);
        var semAnalise = t.PotenciaAtual(CasaDoDenis.IdJblL);
        await b.Rodar(b.Player.PausarAsync("no teste"));
        await Tocar(b, "seno-3");
        await Segundos(b, 1.5);
        var forte = t.PotenciaAtual(CasaDoDenis.IdJblL);

        Assert.NotNull(semAnalise);
        Assert.True(semAnalise > 1, $"{semAnalise} W");
        Assert.Equal(forte!.Value, semAnalise!.Value, 9);
        var dados = DadosTermicos.De(b.Setups.SetupEmUso!.Equipamentos.Single(e => e.Id == CasaDoDenis.IdJblL).Dados)!;
        Assert.Equal(ModeloTermico.TempoAteMaxima(default, dados, forte.Value, 25), ModeloTermico.TempoAteMaxima(default, dados, semAnalise.Value, 25));
    }

    [Fact]
    public async Task Reabrir_sem_gravar_ao_fechar_guarda_o_calor_dos_10_s()
    {
        using var b = new BancadaExtra(Lento());
        await NoExtra(b);
        var t = await Termico(b);
        await Tocar(b);
        await Segundos(b, 15);
        Assert.True(t.Estado(CasaDoDenis.IdJblL)!.Value.Total > 10);

        // O app morre sem o GravarAgoraAsync: vale o que foi gravado a cada 10 s.
        t.Dispose();
        var reaberto = await Termico(b);
        Assert.True(reaberto.Estado(CasaDoDenis.IdJblL)!.Value.Total > 1, $"{reaberto.Estado(CasaDoDenis.IdJblL)}");
    }

    [Fact]
    public async Task No_Maximo_a_bobina_na_maxima_nao_corta()
    {
        using var b = new BancadaExtra(Quente());
        await b.Preparar();
        Assert.Equal(NivelRendimento.Maximo, b.Setups.NivelEmUso);
        var t = await Termico(b);

        await Tocar(b);
        await Segundos(b, 40, () => t.AltoFalantes.Single(a => a.AltoFalante == "JBL L").Temperatura >= 150);
        await Segundos(b, 3);
        Assert.False(t.EmDescanso);
        Assert.Null(t.TempoExtra);
        Assert.Null(t.TextoTempoExtra);
        Assert.Equal(EstadoPlayer.Tocando, b.Player.Estado);
        Assert.DoesNotContain("*251*0*", b.Simulador.Escritas);
    }

    [Fact]
    public async Task Tirar_um_dado_termico_no_descanso_nao_acaba_o_descanso()
    {
        using var b = new BancadaExtra(Rapido());
        await NoExtra(b);
        var t = await Termico(b);
        await AteOCorte(b, t);
        var resta = t.Descanso!.Value.TotalSeconds;

        await Editar(b, CasaDoDenis.IdJblL, d => d with { TauIma = null });
        Assert.False(b.Setups.TermicoEmUso.Pode);
        await Segundos(b, 3);

        Assert.True(t.EmDescanso);
        Assert.True(t.BluetoothTravado);
        Assert.InRange(t.Descanso!.Value.TotalSeconds, 1, resta);
        Assert.NotNull(t.Estado(CasaDoDenis.IdJblL));
        // Ele acaba pelo esfriamento com os dados de antes, e a trava sai.
        await Segundos(b, resta + 15, () => !t.EmDescanso && !t.TravaNoAparelho);
        Assert.Equal("1", b.Simulador.Valor("251"));
    }

    [Fact]
    public async Task Sem_o_modelo_o_Extra_em_uso_desce_ao_pior_caso_e_nao_inventa_tempo()
    {
        using var b = new BancadaExtra(Rapido());
        await NoExtra(b);
        var validado = b.Plana.ExtraValidado[BancadaExtra.IdPlano].Master;
        var piorCaso = b.Setups.Conta(NivelRendimento.Maximo)!.Master!.Value;
        var t = await Termico(b);

        await Editar(b, CasaDoDenis.IdSubCasa, d => d with { TauIma = null });
        Assert.Equal(Math.Min(validado, piorCaso), b.Setups.ContaEmUso()!.Master);
        Assert.Equal("Falta o modelo térmico do Sub.", b.Setups.MotivoSemExtra(b.Plana));

        await Tocar(b);
        await Segundos(b, 1.5);
        Assert.Null(t.TempoExtra);
        Assert.False(t.SemLimite);
        Assert.Equal("Falta o modelo térmico do Sub.", t.MotivoSemModelo);
    }

    [Fact]
    public async Task O_termico_json_ilegivel_segura_o_Extra_ate_esfriar()
    {
        using var b = new BancadaExtra(TodosRapidos());
        await NoExtra(b);
        await b.ArquivoTermico.GravarAsync("{ ilegível");
        var t = await Termico(b);

        Assert.True(t.EmDescanso);
        Assert.Null(t.CortadoPor);
        Assert.Equal(150, t.AltoFalantes.Single(a => a.AltoFalante == "JBL L").Temperatura);
        await b.Rodar(b.Player.TocarFaixaAsync(b.Faixa("seno-3").Id));
        Assert.NotEqual(EstadoPlayer.Tocando, b.Player.Estado);
        Assert.StartsWith("Descansando: ", b.Player.Aviso);
        // O aparelho não estava travado: o app não trava nem destrava nada.
        Assert.False(t.BluetoothTravado);
        Assert.DoesNotContain("*251*0*", b.Simulador.Escritas);

        await Segundos(b, 60, () => !t.EmDescanso);
        await Tocar(b);
        Assert.DoesNotContain("*251*1*", b.Simulador.Escritas);
    }

    [Fact]
    public async Task Antes_de_ler_o_termico_json_o_player_espera()
    {
        using var b = new BancadaExtra(Rapido());
        await NoExtra(b);
        var arquivo = new ArquivoQueEspera();
        var t = new TermicoExtra(b.Sessao, b.Estado, b.Escritor, b.Setups, b.Analise, b.Player, b.Comandos, b.Log, b.Relogio, arquivo);
        try
        {
            await b.Rodar(b.Player.TocarFaixaAsync(b.Faixa("seno-3").Id));
            Assert.Equal(TermicoExtra.MsgCarregando, b.Player.Aviso);
            Assert.NotEqual(EstadoPlayer.Tocando, b.Player.Estado);

            arquivo.Soltar();
            await b.AndarAte(() => t.Carregado.IsCompleted);
            await Tocar(b);
        }
        finally
        {
            t.Dispose();
        }
    }

    /// <summary>Um termico.json que só responde quando o teste solta (o app ainda lendo).</summary>
    private sealed class ArquivoQueEspera : Escrita.IRepositorioTexto
    {
        private readonly TaskCompletionSource<string?> _leitura = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void Soltar() => _leitura.TrySetResult(null);

        public Task<string?> LerAsync() => _leitura.Task;

        public Task GravarAsync(string conteudo) => Task.CompletedTask;
    }
}
