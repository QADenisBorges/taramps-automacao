using BgsControle.Nucleo;
using BgsControle.Nucleo.Escrita;
using BgsControle.Nucleo.Seguranca;
using BgsControle.Nucleo.Uso;

namespace BgsControle.Telas.Testes;

/// <summary>
/// A história 9a nas telas, contra o processador simulado: o Histórico (hora, origem, frase,
/// motivo), a IA ativa (faixa, travado ou pausado, PARAR IA, "O que a IA fez" e o aviso da última
/// mudança) e o estado ao vivo no controle de parâmetro.
/// </summary>
public sealed class HistoricoIaViewModelTestes : IDisposable
{
    private readonly Bancada _b = new(usoCompartilhado: true);

    private Historico Historico => _b.Historico!;
    private UsoCompartilhado Uso => _b.Uso!;

    public void Dispose()
    {
        Assert.Empty(_b.Tela.Erros);
        // A senha do aparelho nunca vai ao histórico guardado.
        Assert.DoesNotContain("AB12", Historico.ParaJson(Historico.Entradas));
    }

    private async Task Conectar()
    {
        await Historico.Carregado;
        await Uso.Carregado;
        await _b.Conectar();
    }

    /// <summary>O ganho da saída 3 (canal 2) pelo campo, como o Denis faz.</summary>
    private async Task<ControleParametroViewModel> GanhoPelaTela(string texto, ControleParametroViewModel? controle = null)
    {
        var c = controle ?? _b.Fabrica.Criar(Catalogo.ParametroDe("081")!, 2);
        c.Expandir();
        c.AbrirCampo();
        c.TextoCampo = texto;
        await c.Confirmar.ExecutarAsync();
        await _b.Rodar(c.UltimaEscrita);
        return c;
    }

    private Task<ResultadoEscrita> GanhoPelaIa(int canal, decimal valor)
    {
        using (Origem.Como(OrigemComando.Ia))
            return _b.Escritor.EscreverAsync(Catalogo.ParametroDe("081")!, canal, valor);
    }

    // ----------------------------------------------------------------- Histórico

    [Fact]
    public async Task Comando_da_tela_vira_a_linha_Tela_hora_Ganho_saida_3_mais_2_dB()
    {
        await Conectar();
        using var h = _b.CriarHistoricoViewModel();
        Assert.True(h.Vazio);
        Assert.Equal(HistoricoViewModel.MsgVazio, h.TextoVazio);

        await GanhoPelaTela("2");
        await _b.Tela.EsvaziarAsync();

        var linha = Assert.Single(h.Linhas);
        Assert.Equal(LinhaHistorico.TextoTela, linha.Origem);
        Assert.Equal("Ganho · saída 3 · +2,0 dB", linha.Frase);
        Assert.Matches(@"^21/10 12:00:\d\d$", linha.Hora); // o relógio falso começa em 21/10, às 12:00
        Assert.True(linha.Enviado);
        Assert.Null(linha.Motivo);
        Assert.Null(h.TextoVazio);
    }

    [Fact]
    public async Task Recusa_pela_chave_entra_com_o_motivo()
    {
        await Conectar();
        Assert.True((await _b.Chaves.DefinirAsync(Chave.AjustesAudio, false, OrigemComando.Tela)).Ok);
        using var h = _b.CriarHistoricoViewModel();

        // O controle trava antes do envio com a chave desligada; aqui o pedido chega à porta.
        var r = await _b.Escritor.EscreverAsync(Catalogo.ParametroDe("081")!, 2, 2.0m);
        await _b.Tela.EsvaziarAsync();

        Assert.False(r.Ok);
        var linha = Assert.Single(h.Linhas);
        Assert.False(linha.Enviado);
        Assert.Equal(Sessao.MotivoChaveDesligada, linha.Motivo);
        Assert.Equal("Ganho · saída 3 · +2,0 dB", linha.Frase);
    }

    [Fact]
    public async Task Troca_de_senha_pela_tela_entra_como_Troca_de_senha_sem_as_senhas()
    {
        await Conectar();
        Assert.True((await _b.Chaves.DefinirAsync(Chave.TrocaSenha, true, OrigemComando.Tela)).Ok);
        using var h = _b.CriarHistoricoViewModel();

        var troca = _b.Comandos.TrocarSenhaAsync("AB12", "CD34");
        await _b.Rodar(troca); // o ack e a releitura andam no relógio falso
        var r = await troca;
        await Historico.GravarAgoraAsync();
        await _b.Tela.EsvaziarAsync();

        Assert.True(r.Ok, r.Mensagem);
        var linha = Assert.Single(h.Linhas, l => l.Frase == FraseComando.TrocaSenha);
        Assert.True(linha.Enviado);
        var guardado = _b.ArquivosUso.Conteudo(RepositorioTextoArquivos.ArquivoHistorico)!;
        Assert.DoesNotContain("AB12", guardado);
        Assert.DoesNotContain("CD34", guardado);
        Assert.All(h.Linhas, l => Assert.DoesNotContain("CD34", l.Frase));
    }

    [Fact]
    public async Task O_historico_reaberto_mostra_as_linhas_guardadas_da_mais_nova_para_a_mais_antiga()
    {
        await Conectar();
        await GanhoPelaTela("1");
        _b.Relogio.Avancar(TimeSpan.FromSeconds(1));
        await GanhoPelaTela("2");
        _b.Relogio.Avancar(Historico.IntervaloGravacao);
        await Apoio.Esperar(() => _b.ArquivosUso.Conteudo(RepositorioTextoArquivos.ArquivoHistorico) is not null);

        var reaberto = new Historico(new RepositorioTextoArquivos(_b.ArquivosUso, RepositorioTextoArquivos.ArquivoHistorico), _b.Relogio);
        using var h = new HistoricoViewModel(reaberto, _b.Tela, _b.Relogio);
        await reaberto.Carregado;
        await Apoio.Esperar(() => h.Linhas.Count == 2);

        Assert.Equal(["Ganho · saída 3 · +2,0 dB", "Ganho · saída 3 · +1,0 dB"], h.Linhas.Select(l => l.Frase));
    }

    [Theory]
    [InlineData(TipoComando.AjusteAudio, "071", 1, "0.5", "Limiter threshold · saída 2 · OFF")]
    [InlineData(TipoComando.AjusteAudio, "042", 0, "3", "HPF filtro · saída 1 · LR24")]
    [InlineData(TipoComando.AjusteAudio, "020", 10, "1.0", "EQ gráfico · banda 2,5 kHz · +1,0 dB")]
    [InlineData(TipoComando.AjusteAudio, "080", null, "-3.0", "Master · −3,0 dB")]
    [InlineData(TipoComando.AjusteAudio, "100", 3, "1", "Mute · saída 4 · mutado")]
    [InlineData(TipoComando.Gerador, "202", null, "0", "Gerador · desligado")]
    [InlineData(TipoComando.Gerador, "201", null, "1000", "Gerador frequência · 1000 Hz")]
    [InlineData(TipoComando.CarregarMemoria, "241", null, "2", "Carregar a memória 3")]
    [InlineData(TipoComando.SalvarMemoria, "240", null, "0", "Salvar na memória 1")]
    [InlineData(TipoComando.NomearMemoria, "232", null, "MUSICA ORIG", "Nome da memória 2 · MUSICA ORIG")]
    [InlineData(TipoComando.RemoteOut, "252", null, "0", "Remote out · desligado")]
    [InlineData(TipoComando.ResetAudio, "242", null, null, "Reset de áudio")]
    [InlineData(TipoComando.TrocaSenha, "001", null, null, "Troca de senha")]
    [InlineData(TipoComando.Desconhecido, "999", null, null, "Comando desconhecido · 999")]
    public void A_frase_de_cada_tipo_de_comando(TipoComando tipo, string codigo, int? indice, string? valor, string frase)
    {
        var e = new EntradaHistorico(DateTimeOffset.UnixEpoch, OrigemComando.App, tipo, new Endereco(codigo, indice), valor, true, null);

        Assert.Equal(frase, FraseComando.De(e));
        Assert.Equal(LinhaHistorico.TextoApp, LinhaHistorico.De(e).Origem);
    }

    [Theory]
    [InlineData("*241*2*", "Carregar a memória 3")] // o 240 e o 23N não saem do app desde a história 8c
    [InlineData("*252*1*", "Remote out · ligado")]
    [InlineData("*252*0*", "Remote out · desligado")]
    [InlineData("*251*1*", "Bluetooth de áudio · ligado")]
    [InlineData("*251*0*", "Bluetooth de áudio · desligado")]
    [InlineData("*002*SALA 1*", "Nome do produto · SALA 1")]
    [InlineData("*004*", "Reset de fábrica")]
    [InlineData("*242*", "Reset de áudio")]
    [InlineData("*001*AB12*CD34*", "Troca de senha")]
    [InlineData("*200*-20.0*", "Gerador ganho · −20,0 dB")]
    [InlineData("*201*1000*", "Gerador frequência · 1000 Hz")]
    [InlineData("*202*1*", "Gerador · ligado")]
    [InlineData("*202*0*", "Gerador · desligado")]
    [InlineData("*210*20*", "Sweep frequência inicial · 20 Hz")]
    [InlineData("*211*20000*", "Sweep frequência final · 20000 Hz")]
    [InlineData("*212*-30.0*", "Sweep ganho · −30,0 dB")]
    [InlineData("*213*2*", "Sweep velocidade · médio")]
    [InlineData("*081*2*2.0*", "Ganho · saída 3 · +2,0 dB")]
    [InlineData("*010*0*a+b*", "Roteamento · saída 1 · A+B")]
    [InlineData("*060*1*180*", "Fase · saída 2 · 180°")]
    public void Cada_escrita_do_catalogo_vira_a_sua_frase(string quadro, string frase)
    {
        var comando = ComandoNaPorta.Ler(quadro);

        Assert.NotNull(comando);
        Assert.NotEqual(TipoComando.Desconhecido, comando.Tipo);
        var e = new EntradaHistorico(DateTimeOffset.UnixEpoch, OrigemComando.Tela, comando.Tipo, comando.Endereco, comando.Valor, true, null);
        Assert.Equal(frase, FraseComando.De(e));
    }

    [Fact]
    public void Aplicar_memoria_do_celular_vira_a_frase_com_o_nome_e_volta_igual_do_json()
    {
        var comando = ComandoNaPorta.AplicarMemoria("FILME");
        var e = new EntradaHistorico(DateTimeOffset.UnixEpoch, OrigemComando.Ia, comando.Tipo, comando.Endereco, comando.Valor, false, "Chave desligada");

        Assert.Equal("Aplicar a memória FILME", FraseComando.De(e));
        Assert.Equal([e], Historico.DeJson(Historico.ParaJson([e])));
    }

    // ----------------------------------------------------------------- IA ativa

    [Fact]
    public async Task IA_ativa_no_modo_padrao_faixa_controles_travados_e_o_toque_e_recusado()
    {
        await Conectar();
        using var ia = _b.CriarIaAtivaViewModel();
        Assert.False(ia.Ativa);

        Assert.True(Uso.IniciarTarefa("IA ajustando o EQ da FILME").Ok);
        await _b.Tela.EsvaziarAsync();

        Assert.True(ia.Ativa);
        Assert.Equal("IA ajustando o EQ da FILME", ia.Faixa);
        Assert.True(ia.Travado);
        Assert.Equal(IaAtivaViewModel.MsgControlesTravados, ia.AvisoControles);

        var c = await GanhoPelaTela("2");
        Assert.Equal(UsoCompartilhado.MsgTravado, c.Aviso);
        Assert.DoesNotContain("*081*2*2.0*", _b.Simulador.Escritas);
        c.Dispose();
    }

    [Fact]
    public async Task IA_ativa_no_modo_pausa_o_toque_pausa_e_10_s_depois_a_IA_retoma()
    {
        await Conectar();
        Assert.True((await Uso.DefinirModoAsync(ModoToque.PausarIa, OrigemComando.Tela)).Ok);
        using var ia = _b.CriarIaAtivaViewModel();
        Assert.True(Uso.IniciarTarefa("IA ajustando o EQ da FILME").Ok);
        await _b.Tela.EsvaziarAsync();
        Assert.False(ia.Travado);
        Assert.Null(ia.AvisoControles);

        var c = await GanhoPelaTela("2");
        await _b.Tela.EsvaziarAsync();

        Assert.Null(c.Aviso);
        Assert.Contains("*081*2*2.0*", _b.Simulador.Escritas);
        Assert.True(ia.Pausada);
        Assert.Equal(IaAtivaViewModel.FaixaPausada, ia.Faixa);

        await _b.Andar(TimeSpan.FromSeconds(10.5), passoMs: 100);

        Assert.False(ia.Pausada);
        Assert.True(ia.Ativa);
        Assert.Equal("IA ajustando o EQ da FILME", ia.Faixa);
        c.Dispose();
    }

    [Fact]
    public async Task PARAR_IA_fecha_a_IA_ativa_e_o_comando_seguinte_da_IA_e_recusado()
    {
        await Conectar();
        using var ia = _b.CriarIaAtivaViewModel();
        using var h = _b.CriarHistoricoViewModel();
        Assert.True(Uso.IniciarTarefa("IA ajustando o EQ da FILME").Ok);
        await _b.Tela.EsvaziarAsync();

        await ia.PararIa.ExecutarAsync();
        await _b.Tela.EsvaziarAsync();

        Assert.False(ia.Ativa);
        Assert.False(ia.Travado);
        var t = GanhoPelaIa(2, 2.0m);
        await _b.Rodar(t);
        var r = await t;
        Assert.Equal(UsoCompartilhado.MsgParada, r.Motivo);
        Assert.Equal((LinhaHistorico.TextoIa, UsoCompartilhado.MsgParada), (h.Linhas[0].Origem, h.Linhas[0].Motivo));
        Assert.False(ia.Ativa); // um comando recusado não reabre a IA ativa
    }

    [Fact]
    public async Task O_que_a_IA_fez_e_o_aviso_da_ultima_mudanca()
    {
        await Conectar();
        await GanhoPelaTela("1"); // da tela: não entra no "O que a IA fez"
        using var ia = _b.CriarIaAtivaViewModel();
        Assert.True(Uso.IniciarTarefa("IA ajustando o ganho").Ok);

        await _b.Rodar(GanhoPelaIa(0, -1.0m));
        await _b.Rodar(GanhoPelaIa(1, 2.0m));
        await _b.Tela.EsvaziarAsync();

        Assert.Equal(["Ganho · saída 2 · +2,0 dB", "Ganho · saída 1 · −1,0 dB"], ia.OQueAIaFez.Select(l => l.Frase));
        Assert.All(ia.OQueAIaFez, l => Assert.Equal(LinhaHistorico.TextoIa, l.Origem));
        Assert.Equal("Agora: Ganho · saída 2 · +2,0 dB", ia.UltimaMudanca);
    }

    // ----------------------------------------------------------------- ao vivo

    [Fact]
    public async Task Ao_vivo_a_IA_muda_o_ganho_3_e_o_controle_da_saida_3_mostra_em_ate_1_s()
    {
        await Conectar();
        using var c = _b.Fabrica.Criar(Catalogo.ParametroDe("081")!, 2);
        using var h = _b.CriarHistoricoViewModel();
        Assert.NotEqual("+2,0 dB", c.TextoValor);
        var antes = _b.Relogio.GetUtcNow();

        var escrita = GanhoPelaIa(2, 2.0m);
        for (var i = 0; i < 100 && c.TextoValor != "+2,0 dB"; i++)
            await _b.Andar(TimeSpan.FromMilliseconds(10));

        Assert.Equal("+2,0 dB", c.TextoValor);
        Assert.True(_b.Relogio.GetUtcNow() - antes <= TimeSpan.FromSeconds(1), $"levou {_b.Relogio.GetUtcNow() - antes}");
        await _b.Rodar(escrita);
        Assert.Equal((LinhaHistorico.TextoIa, "Ganho · saída 3 · +2,0 dB"), (h.Linhas[0].Origem, h.Linhas[0].Frase));
    }
}
