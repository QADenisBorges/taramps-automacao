using BgsControle.Nucleo.Rendimento;
using BgsControle.Nucleo.Setups;
using BgsControle.Simulador;

namespace BgsControle.Nucleo.Testes;

/// <summary>
/// História 13f, o modelo térmico de duas constantes de tempo (puro): o passo exato, o tempo até a máxima e o descanso
/// contra a solução analítica (dentro de 5 %), a temperatura ambiente, a potência na bobina e o "pode o Extra".
/// </summary>
public sealed class ModeloTermicoTestes
{
    /// <summary>Os dados do critério de aceite: Rv 2 K/W, Rm 1,5 K/W, τv 15 s, τm 600 s, Tmax 150 °C.</summary>
    private static readonly DadosTermicos Aceite = new(150, 2, 1.5, 15, 600);

    private const double Ambiente = 25;
    private const double P = 40;

    /// <summary>A solução analítica do corte: a bobina rápida já no regime (80 K), o ímã sobe 45 K: 600·ln 4.</summary>
    private static readonly double CorteAnalitico = 600 * Math.Log(4);

    /// <summary>O descanso analítico: do corte (80 K + 45 K) até 1 K, com a parte do ímã mandando: 600·ln 45.</summary>
    private static readonly double DescansoAnalitico = 600 * Math.Log(45);

    private static void DentroDe5(double esperado, double real) =>
        Assert.True(Math.Abs(real - esperado) <= 0.05 * esperado, $"esperado {esperado:0.0}, veio {real:0.0}");

    [Fact]
    public void Os_numeros_de_conferencia_da_solucao_analitica()
    {
        // Os da spec: 832 s e 2284 s.
        Assert.Equal(832, CorteAnalitico, 0);
        Assert.Equal(2284, DescansoAnalitico, 0);
    }

    [Fact]
    public void Com_40_W_constantes_o_passo_de_1_s_corta_em_832_s_e_o_descanso_dura_2284_s()
    {
        // O passo de 1 s até a bobina chegar à máxima.
        var x = default(EstadoTermico);
        var segundos = 0;
        while (Ambiente + x.Total < Aceite.TMax)
        {
            x = ModeloTermico.Passo(x, Aceite, P, 1);
            segundos++;
            Assert.True(segundos < 100_000);
        }
        DentroDe5(832, segundos);
        DentroDe5(CorteAnalitico, segundos);
        // O passo de 1 s passa no máximo 1 s do instante exato.
        Assert.InRange(segundos, CorteAnalitico, CorteAnalitico + 1);

        // A solução fechada resolvida numericamente dá o mesmo instante.
        var tempo = ModeloTermico.TempoAteMaxima(default, Aceite, P, Ambiente);
        Assert.NotNull(tempo);
        DentroDe5(832, tempo.Value);
        Assert.InRange(tempo.Value, segundos - 1, segundos);

        // O descanso: sem potência, até ambiente + 1 °C.
        var descanso = ModeloTermico.TempoDescanso(x, Aceite);
        DentroDe5(2284, descanso);
        var parado = 0;
        while (x.Total > ModeloTermico.FolgaDescanso)
        {
            x = ModeloTermico.Passo(x, Aceite, 0, 1);
            parado++;
            Assert.True(parado < 100_000);
        }
        DentroDe5(2284, parado);
        DentroDe5(DescansoAnalitico, parado);
        Assert.InRange(descanso, parado - 1, parado);
    }

    [Fact]
    public void O_passo_e_exato_dois_meios_passos_sao_um_passo_inteiro()
    {
        var x = new EstadoTermico(30, 12);
        var inteiro = ModeloTermico.Passo(x, Aceite, P, 2);
        var meios = ModeloTermico.Passo(ModeloTermico.Passo(x, Aceite, P, 1), Aceite, P, 1);
        Assert.Equal(inteiro.Bobina, meios.Bobina, 9);
        Assert.Equal(inteiro.Ima, meios.Ima, 9);
        // Cada parte vai ao regime P·R.
        var regime = ModeloTermico.Passo(default, Aceite, P, 1e6);
        Assert.Equal(P * Aceite.RthBobinaIma, regime.Bobina, 6);
        Assert.Equal(P * Aceite.RthImaAr, regime.Ima, 6);
    }

    [Fact]
    public void Esfriar_parado_5_min_cai_pela_solucao_exata()
    {
        var x = new EstadoTermico(70, 40);
        var depois = ModeloTermico.Passo(x, Aceite, 0, 300);
        Assert.Equal(70 * Math.Exp(-300 / 15.0), depois.Bobina, 9);
        Assert.Equal(40 * Math.Exp(-300 / 600.0), depois.Ima, 9);
        // Em 300 passos de 1 s, o mesmo.
        var passo = x;
        for (var i = 0; i < 300; i++)
            passo = ModeloTermico.Passo(passo, Aceite, 0, 1);
        Assert.Equal(depois.Total, passo.Total, 9);
    }

    [Fact]
    public void Com_35_graus_o_corte_vem_mais_cedo_e_sem_o_dado_vale_25_graus()
    {
        var a25 = ModeloTermico.TempoAteMaxima(default, Aceite, P, 25)!.Value;
        var a35 = ModeloTermico.TempoAteMaxima(default, Aceite, P, 35)!.Value;
        Assert.True(a35 < a25);
        // Tmax − 35 = 115 K: o ímã sobe 35 K → 600·ln(60/25).
        DentroDe5(600 * Math.Log(60.0 / 25), a35);

        Assert.Equal(25, ModeloTermico.Ambiente(CasaDoDenis.SetupCasa));
        Assert.Equal("25 °C (padrão)", ModeloTermico.TextoAmbiente(null));
        var quente = CasaDoDenis.SetupCasa with { TemperaturaAmbiente = DadoTecnico.Digitado(35m) };
        Assert.Equal(35, ModeloTermico.Ambiente(quente));
        Assert.Equal("35 °C", ModeloTermico.TextoAmbiente(quente.TemperaturaAmbiente));
    }

    [Fact]
    public void Abaixo_do_regime_a_bobina_nunca_chega_e_ja_na_maxima_o_tempo_e_zero()
    {
        // 30 W: o regime é 105 K, abaixo dos 125 K que faltam.
        Assert.Null(ModeloTermico.TempoAteMaxima(default, Aceite, 30, Ambiente));
        Assert.Equal(0, ModeloTermico.TempoAteMaxima(new EstadoTermico(80, 45), Aceite, 0, Ambiente));
        Assert.Equal(0, ModeloTermico.TempoDescanso(new EstadoTermico(0.5, 0.4), Aceite));
    }

    [Fact]
    public void Com_a_soma_que_sobe_e_desce_o_tempo_e_a_primeira_passagem()
    {
        // Uma ficha esquisita (a bobina mais lenta que o ímã): o ímã quente esfria rápido enquanto a bobina sobe devagar.
        var d = new DadosTermicos(100, 3, 0.5, 400, 5);
        var x = new EstadoTermico(10, 60);
        foreach (var potencia in new[] { 5.0, 15.0, 30.0 })
        {
            var t = ModeloTermico.TempoAteMaxima(x, d, potencia, 20);
            double? varredura = null;
            for (var s = 0.0; s < 20_000; s += 0.01)
            {
                if (20 + ModeloTermico.DeltaEm(x, d, potencia, s) >= d.TMax)
                {
                    varredura = s;
                    break;
                }
            }
            if (varredura is null)
                Assert.Null(t);
            else
                Assert.InRange(t!.Value, varredura.Value - 0.02, varredura.Value + 0.02);
        }
    }

    [Fact]
    public void A_potencia_e_V2_sobre_Z_com_V_pela_escala_do_limitador()
    {
        // RMS de −4 dB no master de −4,9 dB, ganho ×19,5 em 4 Ω: V = 2,0 × 10^(−8,9/20) × 19,5.
        var v = 2.0 * Math.Pow(10, -8.9 / 20) * 19.5;
        Assert.Equal(v * v / 4, ModeloTermico.Potencia(-4, -4.9, 19.5, 4), 9);
        Assert.Equal(0, ModeloTermico.Potencia(MedidorFaixa.PisoDb, 0, 19.5, 4));
    }

    // ------------------------------------------------------------------ o "pode o Extra"

    private static Setup ComTermico(Func<Equipamento, DadosTecnicos>? mudar = null) =>
        CasaDoDenis.ComAltoFalantes(CasaDoDenis.SetupCasaComTermico, e => mudar?.Invoke(e) ?? e.Dados);

    private static AvaliacaoTermica Avaliar(Setup s) => ModeloTermico.Avaliar(s, Ligacoes.Saidas(s.Equipamentos));

    [Fact]
    public void Com_os_cinco_dados_de_todo_alto_falante_o_Extra_pode_sem_selo()
    {
        var a = Avaliar(CasaDoDenis.SetupCasaComTermico);
        Assert.True(a.Pode, a.Motivo);
        Assert.Null(a.Motivo);
        Assert.False(a.Estimado);
        Assert.Null(a.Selo);
        Assert.Equal(["JBL L", "JBL R", "Sub"], a.AltoFalantes.Select(x => x.AltoFalante.Nome));
        // O Sub na ponte: os canais 3 e 4 com o ganho em ponte.
        var sub = a.AltoFalantes.Single(x => x.AltoFalante.Nome == "Sub");
        Assert.Equal(new (int, double)[] { (2, 40.5), (3, 40.5) }, sub.Canais);
        Assert.Equal(new (int, double)[] { (0, 19.5) }, a.AltoFalantes[0].Canais);
    }

    [Fact]
    public void Sem_o_TauIma_do_Sub_o_Extra_fica_apagado_com_o_nome_dele()
    {
        var a = Avaliar(ComTermico(e => e.Id == CasaDoDenis.IdSubCasa ? e.Dados with { TauIma = null } : e.Dados));
        Assert.False(a.Pode);
        Assert.Equal("Falta o modelo térmico do Sub.", a.Motivo);
        Assert.Null(a.Selo);

        // Sem dado nenhum: o primeiro alto-falante.
        Assert.Equal("Falta o modelo térmico do JBL L.", Avaliar(CasaDoDenis.SetupCasaComBluetooth).Motivo);
        // Sem a impedância, o modelo também não roda, com o motivo de verdade.
        Assert.Equal("Falta a impedância do JBL R.",
            Avaliar(ComTermico(e => e.Id == CasaDoDenis.IdJblR ? e.Dados with { Impedancia = null } : e.Dados)).Motivo);
    }

    [Fact]
    public void Um_caminho_sem_o_ganho_medido_deixa_o_alto_falante_de_fora_com_a_calibracao()
    {
        // O ganho em ponte só digitado: o Sub (nos canais 3 e 4) ficaria com a potência de parte dos caminhos.
        var setup = CasaDoDenis.SetupCasaComTermico with
        {
            Equipamentos = CasaDoDenis.SetupCasaComTermico.Equipamentos.Select(e => e.Id == CasaDoDenis.IdModuloCasa
                ? e with { Dados = e.Dados with { GanhoPonte = DadoTecnico.Digitado(40.5m) } }
                : e).ToArray(),
        };
        var a = Avaliar(setup);
        Assert.False(a.Pode);
        Assert.Equal("Calibre a saída Sub.", a.Motivo);
        Assert.False(a.AltoFalantes.Single(x => x.AltoFalante.Nome == "Sub").Completo);
        Assert.True(a.AltoFalantes.Single(x => x.AltoFalante.Nome == "JBL L").Completo);
    }

    [Fact]
    public void A_maxima_ate_ambiente_mais_1_grau_e_recusada()
    {
        var baixa = ComTermico(e => e.Id == CasaDoDenis.IdSubCasa ? e.Dados with { TemperaturaMaximaBobina = new DadoTecnico(26m, OrigemDado.Oficial) } : e.Dados);
        Assert.Equal("A temperatura máxima do Sub precisa passar de 26 °C.", Avaliar(baixa).Motivo);
        var quente = baixa with { TemperaturaAmbiente = DadoTecnico.Digitado(22.5m) };
        Assert.True(Avaliar(quente).Pode, Avaliar(quente).Motivo);
    }

    [Fact]
    public void Sem_alto_falante_passivo_o_Extra_pede_um()
    {
        var setup = CasaDoDenis.SetupCasaComTermico with
        {
            Equipamentos = CasaDoDenis.SetupCasaComTermico.Equipamentos.Where(e => e.Grupo != GrupoEquipamento.AltoFalante).ToArray(),
        };
        Assert.Equal("O Extra precisa de um alto-falante passivo com o modelo térmico.", Avaliar(setup).Motivo);
    }

    [Fact]
    public void Um_dado_termico_de_outra_fonte_leva_o_selo_estimado()
    {
        var a = Avaliar(ComTermico(e => e.Id == CasaDoDenis.IdSubCasa
            ? e.Dados with { TauBobina = new DadoTecnico(20m, OrigemDado.OutraFonte, "fórum") }
            : e.Dados));
        Assert.True(a.Pode, a.Motivo);
        Assert.Equal("estimado", a.Selo);
    }

    [Fact]
    public void O_relogio_passa_a_horas_acima_de_59_59()
    {
        Assert.Equal("13:52", TermicoExtra.Relogio(TimeSpan.FromSeconds(13 * 60 + 52)));
        Assert.Equal("59:59", TermicoExtra.Relogio(TimeSpan.FromSeconds(3599)));
        Assert.Equal("1:00:00", TermicoExtra.Relogio(TimeSpan.FromSeconds(3600)));
        Assert.Equal("2:05:07", TermicoExtra.Relogio(TimeSpan.FromSeconds(2 * 3600 + 5 * 60 + 7)));
    }

    [Fact]
    public void Um_dado_termico_so_digitado_leva_o_selo_estimado()
    {
        var a = Avaliar(ComTermico(e => e.Id == CasaDoDenis.IdJblL ? e.Dados with { RthImaAr = DadoTecnico.Digitado(2.5m) } : e.Dados));
        Assert.True(a.Pode, a.Motivo);
        Assert.True(a.Estimado);
        Assert.Equal("estimado", a.Selo);
        Assert.Equal("estimado", a.AltoFalantes[0].Selo);
        Assert.Equal("verificado", a.AltoFalantes[1].Selo);
    }

    [Fact]
    public void Os_cinco_dados_vao_e_voltam_no_JSON_da_ficha_e_na_base_de_modelos()
    {
        var dados = new DadosSetups([CasaDoDenis.SetupCasaComTermico with { TemperaturaAmbiente = DadoTecnico.Digitado(31.5m) }], [],
            CasaDoDenis.IdSetupCasa, new Dictionary<string, UsoDoSetup> { [CasaDoDenis.IdSetupCasa] = UsoDoSetup.Vazio });
        var json = GerenteSetups.ParaJson(dados);
        foreach (var nome in new[] { "tMaxBobina", "rthBobinaIma", "rthImaAr", "tauBobina", "tauIma", "temperaturaAmbiente" })
            Assert.Contains($"\"{nome}\"", json);
        var lidos = GerenteSetups.DeJson(json)!;
        var setup = Assert.Single(lidos.Setups);
        Assert.Equal(DadoTecnico.Digitado(31.5m), setup.TemperaturaAmbiente);
        var sub = setup.Equipamentos.Single(e => e.Id == CasaDoDenis.IdSubCasa);
        Assert.Equal(new DadoTecnico(900m, OrigemDado.Oficial, CasaDoDenis.FonteTermicoTeste), sub.Dados.TauIma);
        Assert.True(Avaliar(setup).Pode);

        // A base de modelos guarda só o oficial e outra fonte.
        var modelo = BaseModelos.DoModelo(sub.Dados with { TauBobina = DadoTecnico.Digitado(20m) });
        Assert.Null(modelo.TauBobina);
        Assert.Equal(sub.Dados.TauIma, modelo.TauIma);

        // Um dado térmico de zero ou menos é recusado como os outros.
        var ruim = sub with { Dados = sub.Dados with { TemperaturaMaximaBobina = DadoTecnico.Digitado(0m) } };
        Assert.Equal(Ligacoes.MsgNumeroInvalido, Ligacoes.ValidarUm(ruim));
    }
}
