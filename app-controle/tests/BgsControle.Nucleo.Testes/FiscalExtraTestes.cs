using System.Text.Json;
using BgsControle.Nucleo.Escrita;
using BgsControle.Nucleo.Player;
using BgsControle.Nucleo.Porta;
using BgsControle.Nucleo.Rendimento;
using BgsControle.Nucleo.Setups;
using BgsControle.Simulador;

namespace BgsControle.Nucleo.Testes;

/// <summary>
/// História 13e, o fiscal do Extra e a revalidação contra o processador simulado (<see cref="BancadaExtra"/>): a PLANA
/// com o teste a seco feito e o nível Extra escolhido. Uma linha da matriz por teste.
/// </summary>
public sealed class FiscalExtraTestes : IDisposable
{
    private readonly BancadaExtra _b = new();

    public void Dispose() => _b.Dispose();

    /// <summary>Prepara, valida e escolhe o Extra: devolve o master validado.</summary>
    private async Task<decimal> NoExtra()
    {
        await _b.Preparar();
        await _b.Validar();
        var r = await _b.Rodar(_b.Setups.DefinirNivelAsync(BancadaExtra.IdPlana, NivelRendimento.Extra));
        Assert.True(r.Ok, r.Motivo);
        return _b.Plana.ExtraValidado[BancadaExtra.IdPlano].Master;
    }

    private async Task Tocar(string uri)
    {
        await _b.Rodar(_b.Player.TocarFaixaAsync(_b.Faixa(uri).Id));
        Assert.Equal(EstadoPlayer.Tocando, _b.Player.Estado);
    }

    private IEnumerable<string?> Mutes => Enumerable.Range(0, 4).Select(c => _b.Simulador.Valor("100", c));

    [Fact]
    public async Task Clip_no_uso_baixa_o_master_um_passo_e_marca_a_faixa_e_o_segundo_na_mesma_faixa_pede_revalidar()
    {
        var validado = await NoExtra();
        await Tocar("seno-6");
        _b.Reprodutor.Posicao = TimeSpan.FromSeconds(1.27);
        var sub = TesteSeco.NomeSaida(_b.Setups.SaidasEmUso, 2);

        _b.Simulador.EmitirClip(1 << 2);
        await _b.AndarAte(() => _b.MasterNoAparelho == validado - 1m && _b.Plana.Marcas.Count == 1);

        var marca = Assert.Single(_b.Plana.Marcas);
        Assert.Equal(_b.Faixa("seno-6").Id, marca.IdFaixa);
        Assert.Equal("menos 6", marca.Faixa);
        Assert.Equal(2, marca.Canal);
        Assert.Equal(sub, marca.Saida);
        Assert.Equal(1.3, marca.Segundos);
        Assert.Equal(validado - 1m, _b.Plana.ExtraValidado[BancadaExtra.IdPlano].Master);
        Assert.False(_b.Plana.Revalidar);

        // O mesmo clip aceso (dentro de 3 s) não desce de novo.
        _b.Simulador.EmitirClip(1 << 2);
        await _b.AndarAte(() => false, passos: 20, ms: 10).ContinueWith(_ => { });
        Assert.Single(_b.Plana.Marcas);

        // O 2º clip não previsto na mesma faixa: "revalidar".
        _b.Relogio.Avancar(TimeSpan.FromSeconds(4));
        _b.Simulador.EmitirClip(1 << 2);
        await _b.AndarAte(() => _b.Plana.Revalidar && _b.Plana.ExtraValidado[BancadaExtra.IdPlano].Master == validado - 2m);
        Assert.Equal("Clip duas vezes na menos 6.", _b.Plana.MotivoRevalidar);
        Assert.Equal(2, _b.Plana.Marcas.Count);
        // Em "revalidar", o master do Extra é min(validado, pior caso da cadeia de agora): desce, nunca sobe.
        await _b.AndarAte(() => _b.MasterNoAparelho == _b.Setups.ContaEmUso()!.Master);
        Assert.True(_b.MasterNoAparelho <= validado - 2m);
        Assert.False(_b.Setups.ExtraEmVigorEmUso);
        // As marcas e o "revalidar" ficam no setups.json.
        var lida = GerenteSetups.DeJson(GerenteSetups.ParaJson(_b.Setups.Dados))!.Memorias.Single();
        Assert.True(lida.Revalidar);
        Assert.Equal("Clip duas vezes na menos 6.", lida.MotivoRevalidar);
        Assert.Equal(2, lida.Marcas.Count);
    }

    [Fact]
    public async Task Clip_fora_do_Extra_ou_com_o_player_parado_nao_mexe_em_nada()
    {
        await _b.Preparar();
        await Tocar("seno-6");
        var antes = _b.MasterNoAparelho;

        _b.Simulador.EmitirClip(1);
        for (var i = 0; i < 30; i++)
        {
            _b.Relogio.Avancar(TimeSpan.FromMilliseconds(10));
            await Task.Delay(1);
        }

        Assert.Equal(antes, _b.MasterNoAparelho);
        Assert.Empty(_b.Plana.Marcas);
    }

    [Fact]
    public async Task Outro_player_no_Extra_muta_os_4_canais_pausa_e_o_mudo_sai_ao_tocar_no_player_do_app()
    {
        await NoExtra();
        await Tocar("seno-6");
        Assert.All(Mutes, m => Assert.Equal("0", m));

        _b.Outro.Disparar();
        await _b.AndarAte(() => _b.Fiscal.FolhaOutroPlayer && _b.Player.Estado == EstadoPlayer.Pausado && Mutes.All(m => m == "1"));
        Assert.Equal("Outro app começou a tocar. O som foi mutado.", FiscalExtra.MsgOutroPlayer);
        Assert.True(_b.Fiscal.Mutado);

        await _b.Rodar(_b.Player.TocarPausarAsync());
        await _b.AndarAte(() => Mutes.All(m => m == "0"));
        Assert.Equal(EstadoPlayer.Tocando, _b.Player.Estado);
        Assert.False(_b.Fiscal.FolhaOutroPlayer);
        Assert.False(_b.Fiscal.Mutado);
    }

    [Fact]
    public async Task Outro_player_fora_do_Extra_nao_faz_nada()
    {
        await _b.Preparar();
        await Tocar("seno-6");

        _b.Outro.Disparar();
        for (var i = 0; i < 30; i++)
        {
            _b.Relogio.Avancar(TimeSpan.FromMilliseconds(10));
            await Task.Delay(1);
        }

        Assert.All(Mutes, m => Assert.Equal("0", m));
        Assert.False(_b.Fiscal.FolhaOutroPlayer);
        Assert.Equal(EstadoPlayer.Tocando, _b.Player.Estado);
    }

    [Fact]
    public async Task Volume_no_Extra_vai_ao_maximo_e_volta_quando_muda()
    {
        await NoExtra();
        _b.Volume.Mudar(9);
        await Tocar("seno-6");
        Assert.Equal(15, _b.Volume.Volume);

        _b.Volume.Mudar(9);
        await _b.AndarAte(() => _b.Volume.Volume == 15);

        Assert.Equal("No Extra o volume fica no máximo; use o master.", _b.Fiscal.Aviso);
    }

    [Fact]
    public async Task Candidato_desce_e_o_master_desce_na_hora_e_o_que_sobe_fica_no_validado()
    {
        var validado = await NoExtra();

        // Uma faixa nova 2 dB mais forte: a análise refeita dá o candidato 2 dB abaixo.
        await _b.Adicionar(("seno-1", -1));
        await _b.Pronta();
        await _b.AndarAte(() => _b.Plana.ExtraValidado[BancadaExtra.IdPlano].Master < validado && _b.MasterNoAparelho < validado);
        var baixo = _b.Plana.ExtraValidado[BancadaExtra.IdPlano].Master;
        Assert.Equal(_b.Analise.TetoEmUso().Master, baixo);
        Assert.InRange(validado - baixo, 1.9m, 2.1m);
        Assert.Equal(baixo, _b.MasterNoAparelho);

        // Sem as faixas fortes, o candidato sobe: fica o validado até um teste a seco novo.
        Assert.True((await _b.Playlists.RemoverAsync(BancadaExtra.IdPlana, _b.Faixa("seno-1").Id)).Ok);
        Assert.True((await _b.Playlists.RemoverAsync(BancadaExtra.IdPlana, _b.Faixa("seno-3").Id)).Ok);
        await _b.Pronta();
        await _b.AndarAte(() => _b.Analise.TetoEmUso().Master > baixo);
        for (var i = 0; i < 30; i++)
        {
            _b.Relogio.Avancar(TimeSpan.FromMilliseconds(10));
            await Task.Delay(1);
        }
        Assert.Equal(baixo, _b.Plana.ExtraValidado[BancadaExtra.IdPlano].Master);
        Assert.Equal(baixo, _b.MasterNoAparelho);
    }

    [Fact]
    public async Task Ficha_mudou_poe_a_memoria_em_revalidar_e_o_master_nao_sobe()
    {
        var validado = await NoExtra();
        var jbl = _b.Setups.SetupEmUso!.Equipamentos.Single(e => e.Id == CasaDoDenis.IdJblL);
        var editado = jbl with { Dados = jbl.Dados with { Xmax = new DadoTecnico(1.2m, OrigemDado.Oficial, "teste") } };

        Assert.True((await _b.Rodar(_b.Setups.EditarEquipamentoAsync(CasaDoDenis.IdSetupCasa, editado))).Ok);

        Assert.True(_b.Plana.Revalidar);
        Assert.Equal("A ficha ou a calibração mudou.", _b.Plana.MotivoRevalidar);
        await _b.Pronta();
        // Em "revalidar", o master do Extra é min(validado, pior caso da cadeia de agora): desce, nunca sobe.
        await _b.AndarAte(() => _b.MasterNoAparelho < validado && _b.MasterNoAparelho == _b.Setups.ContaEmUso()!.Master);
        Assert.True(_b.Plana.ExtraValidado[BancadaExtra.IdPlano].Master <= validado);
        Assert.False(_b.Setups.ExtraEmVigorEmUso);
        Assert.Equal(NivelRendimento.Extra, _b.Setups.NivelEmUso);
    }

    [Fact]
    public async Task A_porta_traz_o_validado_o_revalidar_e_as_marcas()
    {
        var validado = await NoExtra();
        var (porta, chave) = await _b.PortaPareada();
        using var _ = porta;
        await Tocar("seno-3");
        _b.Reprodutor.Posicao = TimeSpan.FromSeconds(0.5);
        _b.Simulador.EmitirClip(1);
        await _b.AndarAte(() => _b.Plana.Marcas.Count == 1);

        var r = await porta.AtenderAsync(PedidoPorta.De("GET", "/limites", BancadaExtra.Casa, bearer: chave));

        Assert.Equal(200, r.Status);
        var raiz = JsonDocument.Parse(r.Json).RootElement;
        Assert.Equal("extra", raiz.GetProperty("nivel").GetString());
        var extra = raiz.GetProperty("extra");
        Assert.Equal(validado - 1m, extra.GetProperty("validado").GetDecimal());
        Assert.True(extra.GetProperty("validadoVigente").GetBoolean());
        Assert.False(extra.GetProperty("revalidar").GetBoolean());
        Assert.Equal(JsonValueKind.Null, extra.GetProperty("motivoRevalidar").ValueKind);
        var marca = Assert.Single(extra.GetProperty("marcas").EnumerateArray());
        Assert.Equal("menos 3", marca.GetProperty("faixa").GetString());
        Assert.Equal(_b.Faixa("seno-3").Id, marca.GetProperty("id").GetString());
        Assert.Equal("JBL L", marca.GetProperty("saida").GetString());
        Assert.Equal(0.5, marca.GetProperty("segundos").GetDouble());
    }

    [Fact]
    public async Task Sair_do_Extra_com_o_limitador_acima_do_termico_e_recusado()
    {
        await NoExtra();

        // O limitador das JBL (−5,0 dB) está acima de min(T, X, C): o Alto e o "sem nível" teriam a cerca do térmico.
        var alto = await _b.Rodar(_b.Setups.DefinirNivelAsync(BancadaExtra.IdPlana, NivelRendimento.Alto));
        var semNivel = await _b.Rodar(_b.Setups.DefinirNivelAsync(BancadaExtra.IdPlana, null));

        Assert.False(alto.Ok);
        Assert.Contains("antes de sair do Extra.", alto.Motivo);
        Assert.False(semNivel.Ok);
        Assert.Contains("antes de sair do Extra.", semNivel.Motivo);
        Assert.Equal(NivelRendimento.Extra, _b.Setups.NivelEmUso);
    }

    [Fact]
    public async Task Aplicar_de_novo_poe_o_validado_e_outro_preset_o_dele_ou_o_pior_caso()
    {
        var validado = await NoExtra();
        // O master baixado à mão (abaixo da conta, passa).
        Assert.True((await _b.Rodar(_b.Escritor.EscreverAsync(Catalogo.ParametroDe("080")!, null, validado - 5m, espelhar: false))).Ok);
        Assert.Equal(validado - 5m, _b.MasterNoAparelho);

        // Aplicar a PLANA de novo: o master é o validado do preset dela.
        var aplicar = await _b.Rodar(_b.Setups.AplicarAsync(BancadaExtra.IdPlana));
        Assert.True(aplicar.Ok, aplicar.Motivo);
        Assert.Equal(validado, _b.MasterNoAparelho);

        // O Mil não tem validado: o master é o do pior caso da cadeia com o Mil, nunca o validado do Plano.
        var mil = await _b.Rodar(_b.Presets.AplicarAsync(BancadaExtra.IdMil));
        Assert.True(mil.Ok, mil.Motivo);
        await _b.AndarAte(() => !_b.Copia.EmAndamento && _b.Setups.IdPresetEmUso == BancadaExtra.IdMil);
        var setup = _b.Setups.SetupEmUso!;
        var piorCaso = PiorCaso.Conta(setup.Equipamentos, _b.Setups.SaidasEmUso, _b.Estado.Valor, setup.NivelBluetooth!.Valor,
            NivelRendimento.Extra).Master;
        Assert.Equal(piorCaso, _b.MasterNoAparelho);
        Assert.NotEqual(validado, _b.MasterNoAparelho);
        Assert.Null(_b.Setups.ExtraValidadoEmUso);

        // De volta ao Plano: o validado dele.
        var plano = await _b.Rodar(_b.Presets.AplicarAsync(BancadaExtra.IdPlano));
        Assert.True(plano.Ok, plano.Motivo);
        await _b.AndarAte(() => !_b.Copia.EmAndamento && _b.MasterNoAparelho == validado);
    }

    [Fact]
    public async Task Proxima_depois_do_mudo_do_outro_player_tira_o_mudo_e_poe_o_volume_no_maximo()
    {
        await NoExtra();
        await Tocar("seno-3");
        _b.Outro.Disparar();
        await _b.AndarAte(() => _b.Player.Estado == EstadoPlayer.Pausado && Mutes.All(m => m == "1"));
        _b.Volume.Mudar(5);

        await _b.Rodar(_b.Player.ProximaAsync());
        await _b.AndarAte(() => Mutes.All(m => m == "0"));

        Assert.Equal(EstadoPlayer.Tocando, _b.Player.Estado);
        Assert.Equal("seno-6", _b.Reprodutor.Tocando);
        Assert.Equal(15, _b.Volume.Volume);
        Assert.False(_b.Fiscal.Mutado);
    }

    [Fact]
    public async Task Outro_player_com_o_player_do_app_parado_nao_faz_nada()
    {
        await NoExtra();
        Assert.Equal(EstadoPlayer.Parado, _b.Player.Estado);

        _b.Outro.Disparar();
        for (var i = 0; i < 30; i++)
        {
            _b.Relogio.Avancar(TimeSpan.FromMilliseconds(10));
            await Task.Delay(1);
        }

        Assert.All(Mutes, m => Assert.Equal("0", m));
        Assert.False(_b.Fiscal.FolhaOutroPlayer);
        Assert.False(_b.Fiscal.Mutado);
    }

    [Fact]
    public async Task O_mudo_do_outro_player_fica_guardado_e_sai_ao_tocar_depois_de_reabrir_o_app()
    {
        await NoExtra();
        await Tocar("seno-3");
        _b.Outro.Disparar();
        await _b.AndarAte(() => _b.Player.Estado == EstadoPlayer.Pausado && Mutes.All(m => m == "1"));
        Assert.Contains("\"mute\":[0,0,0,0]", _b.ArquivoFiscal.Texto);

        // O app reabre: o fiscal novo lê o mudo guardado; o tocar o tira.
        _b.Fiscal.Dispose();
        using var reaberto = _b.NovoFiscal();
        await reaberto.Carregado;
        Assert.True(reaberto.Mutado);
        Assert.All(Mutes, m => Assert.Equal("1", m));

        await _b.Rodar(_b.Player.TocarPausarAsync());
        await _b.AndarAte(() => Mutes.All(m => m == "0"));
        Assert.False(reaberto.Mutado);
        Assert.Contains("\"mute\":null", _b.ArquivoFiscal.Texto);
    }
}
