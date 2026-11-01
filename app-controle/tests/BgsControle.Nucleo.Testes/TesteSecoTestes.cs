using BgsControle.Nucleo.Escrita;
using BgsControle.Nucleo.Player;
using BgsControle.Nucleo.Rendimento;
using BgsControle.Nucleo.Setups;
using BgsControle.Simulador;

namespace BgsControle.Nucleo.Testes;

/// <summary>
/// História 13e, o teste a seco e o nível Extra contra o processador simulado (<see cref="BancadaExtra"/>): o Casa 2.1
/// calibrado, a PLANA em uso no Máximo com 3 faixas (−3, −6 e −10 dBFS, um trecho cada) e a análise pronta, com o
/// teto candidato ≈ −4,7 dB. Uma linha da matriz por teste.
/// </summary>
public sealed class TesteSecoTestes : IDisposable
{
    private readonly BancadaExtra _b = new();

    public void Dispose() => _b.Dispose();

    /// <summary>A posição da escrita no simulador (a primeira, a partir de <paramref name="desde"/>), ou −1.</summary>
    private int Indice(string escrita, int desde = 0)
    {
        var escritas = _b.Simulador.Escritas;
        for (var i = desde; i < escritas.Count; i++)
        {
            if (escritas[i] == escrita)
                return i;
        }
        return -1;
    }

    private int UltimaDoMaster() => _b.Simulador.Escritas.Select((e, i) => (e, i)).Last(x => x.e.StartsWith("*080*", StringComparison.Ordinal)).i;

    [Fact]
    public async Task Teste_sem_clip_desliga_o_modulo_antes_do_primeiro_trecho_e_religa_so_no_master_validado()
    {
        await _b.Preparar();
        var candidato = _b.Analise.TetoEmUso().Master!.Value;
        Assert.InRange(candidato, -5.1m, -4.6m);

        var abrir = _b.Teste.Abrir();
        Assert.True(abrir.Ok, abrir.Motivo);
        Assert.Equal(EtapaTesteSeco.Aviso, _b.Teste.Etapa);
        Assert.Equal("O módulo vai ser desligado.", TesteSeco.MsgAviso);
        Assert.Equal(3, _b.Teste.Plano.Count);
        Assert.Equal("Faixa 1 de 3: menos 3, trecho 1", _b.Teste.Plano[0].Texto);
        var r = await _b.Rodar(_b.Teste.ConfirmarAsync());

        Assert.True(r.Ok, r.Motivo);
        Assert.Equal(EtapaTesteSeco.Resultado, _b.Teste.Etapa);
        Assert.Equal($"Teto validado: {LimiteFisico.Db(candidato)} dB.", r.Mensagem);
        // Todos os trechos tocados, do início ao fim de cada um.
        var trechos = _b.Reprodutor.Trechos;
        Assert.Equal(["seno-3", "seno-6", "seno-10"], trechos.Select(t => t.Uri));
        Assert.All(trechos, t => Assert.Equal(TimeSpan.FromSeconds(2), t.Duracao));
        // O 252 = 0 antes do 1º trecho; o 252 = 1 depois do último master (o validado).
        var desligou = Indice("*252*0*");
        Assert.InRange(desligou, 0, trechos[0].Escritas - 1);
        var religou = Indice("*252*1*", desligou);
        Assert.True(religou > UltimaDoMaster(), "o módulo religou antes do master validado");
        Assert.Equal("1", _b.Modulo);
        Assert.Equal(candidato, _b.MasterNoAparelho);
        // O validado fica na memória, com a impressão da cadeia; a cópia terminou.
        var validado = _b.Plana.ExtraValidado[BancadaExtra.IdPlano];
        Assert.Equal(candidato, validado.Master);
        Assert.Equal(_b.Analise.ImpressaoAtual(BancadaExtra.IdPlana, BancadaExtra.IdPlano), validado.Impressao);
        Assert.Null(_b.Copia.Ultima?.Operacao);
        Assert.Empty(_b.Teste.Clips);
        Assert.False(_b.Plana.Revalidar);
        // O setups.json guarda o validado (opcional no formato 4).
        var json = GerenteSetups.ParaJson(_b.Setups.Dados);
        Assert.Contains("\"formato\": 4", json);
        Assert.Equal(candidato, GerenteSetups.DeJson(json)!.Memorias.Single().ExtraValidado[BancadaExtra.IdPlano].Master);
    }

    [Fact]
    public async Task Clip_num_trecho_baixa_o_master_um_passo_repete_o_trecho_e_derruba_o_teto()
    {
        await _b.Preparar();
        var candidato = _b.Analise.TetoEmUso().Master!.Value;
        // O 2º trecho (a de −6 dBFS, 3 dB abaixo da que manda) chega 4 dB mais forte na JBL L do que a análise previu.
        _b.Reprodutor.AMais = (n, canal) => n == 2 && canal == 0 ? 4.0 : 0;

        var r = await _b.Validar();

        var esperado = candidato - 1m;
        Assert.Equal(TesteSeco.MsgClipNa("menos 6", "JBL L", esperado), r.Mensagem);
        Assert.Equal(["seno-3", "seno-6", "seno-6", "seno-10"], _b.Reprodutor.Trechos.Select(t => t.Uri));
        var clip = Assert.Single(_b.Teste.Clips);
        Assert.Equal(_b.Faixa("seno-6").Id, clip.IdFaixa);
        Assert.Equal(0, clip.Canal);
        Assert.Equal("JBL L", clip.Saida);
        Assert.Equal(esperado, clip.Master);
        Assert.Equal(esperado, _b.Plana.ExtraValidado[BancadaExtra.IdPlano].Master);
        Assert.Equal(esperado, _b.MasterNoAparelho);
        Assert.Equal("1", _b.Modulo);
        Assert.True(Indice("*252*1*") > UltimaDoMaster());
    }

    [Fact]
    public async Task Clip_que_nao_some_ate_o_minimo_falha_e_deixa_o_modulo_desligado()
    {
        await _b.Preparar();
        _b.Reprodutor.AMais = (_, canal) => canal == 0 ? 100 : 0;

        Assert.True(_b.Teste.Abrir().Ok);
        var r = await _b.Rodar(_b.Teste.ConfirmarAsync(), ms: 250);

        Assert.False(r.Ok);
        Assert.Equal("O clip não sumiu nem com o master no mínimo.", r.Motivo);
        Assert.Equal("0", _b.Modulo);
        Assert.Equal(0m, _b.MasterNoAparelho);
        Assert.Empty(_b.Plana.ExtraValidado);
        Assert.Null(_b.Copia.Ultima?.Operacao);
    }

    [Fact]
    public async Task Queda_no_meio_deixa_o_modulo_desligado_pela_metade_e_nada_validado()
    {
        await _b.Preparar();
        _b.Reprodutor.AoTocar = n =>
        {
            if (n == 3)
                _b.Simulador.Derrubar();
        };

        Assert.True(_b.Teste.Abrir().Ok);
        var r = await _b.Rodar(_b.Teste.ConfirmarAsync());

        Assert.False(r.Ok);
        Assert.Equal(TesteSeco.MsgCaiu, r.Motivo);
        Assert.Equal(EtapaTesteSeco.Fechado, _b.Teste.Etapa);
        Assert.Equal("0", _b.Modulo);
        Assert.Empty(_b.Plana.ExtraValidado);
        Assert.Equal(TesteSeco.Tipo, _b.Copia.Ultima?.Operacao?.Tipo);
        Assert.DoesNotContain("*252*1*", _b.Simulador.Escritas);

        // Ao reconectar: pela metade. O Terminar não sabe refazer; o Voltar à cópia devolve o master, e o módulo fica desligado.
        await _b.Sessao.ConectarAsync();
        await _b.AndarAte(() => _b.Estado.Pronto && !_b.Estado.Lendo && _b.Copia.PelaMetade is not null);
        var terminar = await _b.Rodar(_b.Copia.TerminarAsync());
        Assert.False(terminar.Ok);
        Assert.Equal("O app não sabe refazer esta operação.", terminar.Motivo);
        var voltar = await _b.Rodar(_b.Copia.VoltarAsync());
        Assert.True(voltar.Ok, voltar.Motivo);
        Assert.Equal(0m, _b.MasterNoAparelho);
        Assert.Equal("0", _b.Modulo);
        Assert.Null(_b.Copia.PelaMetade);
    }

    [Fact]
    public async Task Cancelar_tocando_para_o_player_volta_o_master_e_deixa_o_modulo_desligado()
    {
        await _b.Preparar();
        _b.Reprodutor.Segurar = 2;
        Assert.True(_b.Teste.Abrir().Ok);
        var confirmar = _b.Teste.ConfirmarAsync();
        await _b.AndarAte(() => _b.Reprodutor.Segurando);
        Assert.Equal(EtapaTesteSeco.Tocando, _b.Teste.Etapa);
        Assert.Equal("Faixa 2 de 3: menos 6, trecho 1", _b.Teste.TrechoAtual?.Texto);
        Assert.True(_b.Player.Bloqueio?.Invoke() is not null);

        var cancelar = await _b.Rodar(_b.Teste.CancelarAsync());
        var r = await _b.Rodar(confirmar);

        Assert.Equal(TesteSeco.MsgCancelado, cancelar.Motivo);
        Assert.Equal(TesteSeco.MsgCancelado, r.Motivo);
        Assert.Equal(EtapaTesteSeco.Fechado, _b.Teste.Etapa);
        Assert.Null(_b.Reprodutor.Tocando);
        Assert.Equal(EstadoPlayer.Parado, _b.Player.Estado);
        Assert.Equal(0m, _b.MasterNoAparelho);
        Assert.Equal("0", _b.Modulo);
        Assert.Null(_b.Copia.Ultima?.Operacao);
        Assert.Empty(_b.Plana.ExtraValidado);
        Assert.Null(_b.Player.Bloqueio?.Invoke());
    }

    [Fact]
    public async Task Nao_desligou_nada_toca()
    {
        await _b.Preparar();
        // O aparelho responde que o módulo ficou ligado.
        _b.Simulador.EcosDiferentes["*252*0*"] = "#252#1#";

        Assert.True(_b.Teste.Abrir().Ok);
        var r = await _b.Rodar(_b.Teste.ConfirmarAsync());

        Assert.False(r.Ok);
        Assert.Equal("Não consegui desligar o módulo.", r.Motivo);
        Assert.Empty(_b.Reprodutor.Trechos);
        Assert.DoesNotContain(_b.Simulador.Escritas, e => e.StartsWith("*080*", StringComparison.Ordinal));
        Assert.Null(_b.Copia.Ultima?.Operacao);
        Assert.Empty(_b.Plana.ExtraValidado);
    }

    [Fact]
    public async Task Sem_a_analise_pronta_o_teste_nao_abre()
    {
        await _b.Conectar();

        var r = _b.Teste.Abrir();

        Assert.False(r.Ok);
        Assert.Equal(TesteSeco.MsgSemOtimizar, r.Motivo);
        Assert.Equal(EtapaTesteSeco.Fechado, _b.Teste.Etapa);
        Assert.DoesNotContain(_b.Simulador.Escritas, e => e.StartsWith("*252*", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Escolher_o_Extra_poe_o_master_validado_e_a_cerca_em_min_X_C()
    {
        await _b.Preparar();
        var sem = await _b.Rodar(_b.Setups.DefinirNivelAsync(BancadaExtra.IdPlana, NivelRendimento.Extra));
        Assert.False(sem.Ok);
        Assert.Equal("Falta o teste a seco.", sem.Motivo);
        Assert.Equal(NivelRendimento.Maximo, _b.Setups.NivelEmUso);

        await _b.Validar();
        var validado = _b.Plana.ExtraValidado[BancadaExtra.IdPlano].Master;
        var r = await _b.Rodar(_b.Setups.DefinirNivelAsync(BancadaExtra.IdPlana, NivelRendimento.Extra));

        Assert.True(r.Ok, r.Motivo);
        Assert.Equal("Nível Extra na PLANA.", r.Mensagem);
        Assert.Equal(NivelRendimento.Extra, _b.Setups.NivelEmUso);
        Assert.Equal(validado, _b.Setups.ContaEmUso()!.Master);
        Assert.Equal(validado, _b.MasterNoAparelho);
        // A cerca do Extra é a do Máximo: min(X, C).
        var limites = _b.Setups.LimitesEmUso;
        var semTermico = LimiteFisico.Calcular(_b.Setups.SetupEmUso!.Equipamentos, _b.Setups.SaidasEmUso, _b.Estado.Valor, semTermico: true);
        Assert.Equal(semTermico.SelectMany(l => l.PorCanal).Select(c => c.TetoCerca), limites.SelectMany(l => l.PorCanal).Select(c => c.TetoCerca));
        // No Extra, o master à mão acima do validado é recusado.
        Assert.Equal(Niveis.MsgMasterAcima(NivelRendimento.Extra, validado),
            (await _b.Rodar(_b.Escritor.EscreverAsync(Catalogo.ParametroDe("080")!, null, validado + 1m, espelhar: false))).Motivo);
        Assert.Contains("\"nivel\": \"extra\"", GerenteSetups.ParaJson(_b.Setups.Dados));
    }

    [Fact]
    public async Task A_cadeia_mudou_depois_do_teste_e_o_Extra_pede_um_teste_novo()
    {
        await _b.Preparar();
        await _b.Validar();
        // Uma faixa nova muda a análise, não a impressão: o Extra ainda se escolhe. A ficha muda a impressão.
        var jbl = _b.Setups.SetupEmUso!.Equipamentos.Single(e => e.Id == CasaDoDenis.IdJblL);
        var editado = jbl with { Dados = jbl.Dados with { Fs = new DadoTecnico(99m, OrigemDado.Oficial, "teste") } };
        Assert.True((await _b.Rodar(_b.Setups.EditarEquipamentoAsync(CasaDoDenis.IdSetupCasa, editado))).Ok);
        await _b.Pronta();

        var r = await _b.Rodar(_b.Setups.DefinirNivelAsync(BancadaExtra.IdPlana, NivelRendimento.Extra));

        Assert.False(r.Ok);
        Assert.Equal(Niveis.MsgRefazerTesteSeco, r.Motivo);
        Assert.True(_b.Plana.Revalidar);
        Assert.Equal("A ficha ou a calibração mudou.", _b.Plana.MotivoRevalidar);

        // Um teste a seco que passa tira o "revalidar", e o Extra volta a se escolher.
        await _b.Validar();
        Assert.False(_b.Plana.Revalidar);
        Assert.True((await _b.Rodar(_b.Setups.DefinirNivelAsync(BancadaExtra.IdPlana, NivelRendimento.Extra))).Ok);
    }

    [Fact]
    public async Task Um_comando_do_Player_no_meio_do_teste_e_recusado_e_o_trecho_segue()
    {
        await _b.Preparar();
        _b.Reprodutor.Segurar = 2;
        Assert.True(_b.Teste.Abrir().Ok);
        var confirmar = _b.Teste.ConfirmarAsync();
        await _b.AndarAte(() => _b.Reprodutor.Segurando);

        await _b.Rodar(_b.Player.TocarFaixaAsync(_b.Faixa("seno-3").Id));
        Assert.Equal(TesteSeco.MsgPlayerOcupado, _b.Player.Aviso);
        await _b.Rodar(_b.Player.TocarPausarAsync());
        Assert.Equal(TesteSeco.MsgPlayerOcupado, _b.Player.Aviso);

        Assert.True(_b.Reprodutor.Segurando);
        Assert.Equal(EtapaTesteSeco.Tocando, _b.Teste.Etapa);
        _b.Reprodutor.Soltar();
        var r = await _b.Rodar(confirmar);
        Assert.True(r.Ok, r.Motivo);
    }

    [Fact]
    public async Task Fechar_o_app_no_meio_deixa_pela_metade_sem_voltar_o_master_e_sem_religar()
    {
        await _b.Preparar();
        var candidato = _b.Analise.TetoEmUso().Master!.Value;
        _b.Reprodutor.Segurar = 2;
        Assert.True(_b.Teste.Abrir().Ok);
        var confirmar = _b.Teste.ConfirmarAsync();
        await _b.AndarAte(() => _b.Reprodutor.Segurando);

        _b.Teste.InterromperAoFechar();
        var r = await _b.Rodar(confirmar);

        Assert.False(r.Ok);
        Assert.Equal(TesteSeco.MsgFechouApp, r.Motivo);
        Assert.Equal(TesteSeco.Tipo, _b.Copia.PelaMetade?.Tipo);
        Assert.Equal(candidato, _b.MasterNoAparelho);
        Assert.Equal("0", _b.Modulo);
        Assert.DoesNotContain("*252*1*", _b.Simulador.Escritas);
        Assert.Empty(_b.Plana.ExtraValidado);
    }

    [Fact]
    public async Task Cancelar_durante_a_validacao_nunca_religa_o_modulo()
    {
        await _b.Preparar();
        var cancelou = false;
        _b.Teste.Mudou += () =>
        {
            // Os trechos passaram: a validação começou.
            if (!cancelou && _b.Teste.Plano.Count > 0 && _b.Teste.TrechosFeitos == _b.Teste.Plano.Count)
            {
                cancelou = true;
                _ = _b.Teste.CancelarAsync();
            }
        };
        Assert.True(_b.Teste.Abrir().Ok);

        var r = await _b.Rodar(_b.Teste.ConfirmarAsync());
        await _b.AndarAte(() => !_b.Copia.EmAndamento);

        Assert.True(cancelou);
        Assert.False(r.Ok);
        Assert.DoesNotContain("*252*1*", _b.Simulador.Escritas);
        Assert.Equal("0", _b.Modulo);
        Assert.Equal(0m, _b.MasterNoAparelho);
        Assert.Empty(_b.Plana.ExtraValidado);
    }

    [Fact]
    public async Task Sem_o_volume_do_celular_no_maximo_nada_acontece()
    {
        await _b.Preparar();
        _b.Volume.Limite = 12;

        Assert.True(_b.Teste.Abrir().Ok);
        var r = await _b.Rodar(_b.Teste.ConfirmarAsync());

        Assert.False(r.Ok);
        Assert.Equal(TesteSeco.MsgVolume, r.Motivo);
        Assert.Empty(_b.Reprodutor.Trechos);
        Assert.DoesNotContain("*252*0*", _b.Simulador.Escritas);
    }

    [Fact]
    public async Task Com_o_limitador_desligado_o_teste_nao_abre()
    {
        await _b.Preparar();
        _b.Estado.Atualizar(new Endereco("071", 1), Catalogo.ParametroDe("071")!.Decodificar("+0.5").Valor!);

        var r = _b.Teste.Abrir();

        Assert.False(r.Ok);
        Assert.Equal(TesteSeco.MsgLimitadorDesligado("JBL R"), r.Motivo);
    }

    [Fact]
    public async Task Um_trecho_que_nao_toca_inteiro_toca_de_novo()
    {
        await _b.Preparar();
        _b.Reprodutor.Segurar = 2;
        Assert.True(_b.Teste.Abrir().Ok);
        var confirmar = _b.Teste.ConfirmarAsync();
        await _b.AndarAte(() => _b.Reprodutor.Segurando);

        // O reprodutor parou sozinho no meio do 2º trecho: ele toca de novo, e o teste passa.
        await _b.Reprodutor.PararAsync();
        var r = await _b.Rodar(confirmar);

        Assert.True(r.Ok, r.Motivo);
        Assert.Equal(["seno-3", "seno-6", "seno-6", "seno-10"], _b.Reprodutor.Trechos.Select(t => t.Uri));
    }
}
