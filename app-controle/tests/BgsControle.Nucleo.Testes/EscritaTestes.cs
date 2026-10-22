using System.Text.RegularExpressions;
using BgsControle.Nucleo.Escrita;
using BgsControle.Nucleo.Leitura;
using BgsControle.Nucleo.Registro;
using BgsControle.Nucleo.Seguranca;
using BgsControle.Simulador;

namespace BgsControle.Nucleo.Testes;

/// <summary>Chaves de acesso guardadas em memória.</summary>
internal sealed class RepositorioMemoria : IRepositorioChaves
{
    public string? Json { get; set; }
    public bool FalharAoGravar { get; set; }
    public bool FalharAoLer { get; set; }

    /// <summary>Quantas das próximas gravações falham.</summary>
    public int FalhasAoGravar { get; set; }

    public int Gravacoes { get; private set; }
    public int TentativasDeGravar { get; private set; }
    public Task<string?>? Leitura { get; set; }

    public Task<string?> LerAsync()
    {
        if (FalharAoLer)
            throw new IOException("arquivo ilegível");
        return Leitura ?? Task.FromResult(Json);
    }

    public Task GravarAsync(string json)
    {
        TentativasDeGravar++;
        if (FalharAoGravar)
            throw new IOException("disco cheio");
        if (FalhasAoGravar > 0)
        {
            FalhasAoGravar--;
            throw new IOException("falha passageira");
        }
        Json = json;
        Gravacoes++;
        return Task.CompletedTask;
    }
}

/// <summary>
/// A história 5 no núcleo, contra o processador simulado: a trava nova, o escritor (eco,
/// releitura, ritmo do arrasto), o espelho e as chaves de acesso.
/// </summary>
public sealed class EscritaTestes : IDisposable
{
    private static readonly TemposSessao Tempos = new(
        Scan: TimeSpan.FromSeconds(1),
        TentativaConexao: TimeSpan.FromSeconds(1),
        PausaEntreTentativas: TimeSpan.FromMilliseconds(1),
        RespostaSenha: TimeSpan.FromSeconds(2),
        PrazoSenha: TimeSpan.FromSeconds(60));

    // As leituras respondem na hora; só o voltímetro espera o prazo (tempo real).
    private static readonly TemposLeitura LeituraRapida = new(TimeSpan.Zero, TimeSpan.FromMilliseconds(100));

    private static readonly Regex Senha = new(@"^\*000\*[0-9A-Z]{4}\*$");

    private readonly RelogioFalso _relogio = new();
    private readonly ProcessadorSimulado _simulador;
    private readonly RepositorioMemoria _repositorio = new();
    private readonly Chaves _chaves;
    private readonly LogBle _log = new();
    private readonly Sessao _sessao;
    private readonly LeitorCompleto _leitor;
    private readonly LeituraAutomatica _leitura;
    private readonly EstadoAparelho _estado;
    private readonly Escritor _escritor;

    private static Parametro Ganho => Catalogo.ParametroDe("081")!;

    public EscritaTestes()
    {
        // A sessão e o leitor esperam em tempo real e o relógio falso não anda enquanto conectam:
        // a senha e as leituras respondem na hora; os ecos seguem no relógio falso.
        _simulador = new ProcessadorSimulado(_relogio) { AtrasoSenha = TimeSpan.Zero, AtrasoLeitura = TimeSpan.Zero };
        _chaves = new Chaves(_repositorio);
        _sessao = new Sessao(_simulador, new CofreFalso { Senha = "AB12" }, _log, Tempos, _chaves);
        _leitor = new LeitorCompleto(_sessao, _log, LeituraRapida);
        _leitura = new LeituraAutomatica(_sessao, _leitor);
        _estado = new EstadoAparelho(_sessao, _leitura);
        _escritor = new Escritor(_sessao, _estado, _log, relogio: _relogio);
    }

    /// <summary>
    /// Critério de aceite: em qualquer teste, só chegaram ao simulador a senha, as leituras do
    /// catálogo, as escritas de áudio do catálogo e os comandos de memória permitidos.
    /// </summary>
    public void Dispose() =>
        Assert.All(_simulador.Escritas, e =>
            Assert.True(Senha.IsMatch(e) || Catalogo.EhLeitura(e) || Catalogo.EscritaDeAudio(e) is not null
                        || Catalogo.ComandoDeMemoria(e) is not null, e));

    private async Task Conectar()
    {
        await _chaves.Carregada;
        await _sessao.ConectarAsync();
        Assert.Equal(EstadoSessao.Conectado, _sessao.Estado);
        await Esperar(() => _estado.Pronto && !_estado.Lendo);
    }

    private static async Task Esperar(Func<bool> condicao)
    {
        for (var i = 0; i < 1000 && !condicao(); i++)
            await Task.Delay(10);
        Assert.True(condicao());
    }

    /// <summary>Anda o relógio falso em passos curtos, dando tempo às continuações.</summary>
    private async Task Andar(TimeSpan quanto, TimeSpan? passo = null)
    {
        var p = passo ?? TimeSpan.FromMilliseconds(10);
        for (var t = TimeSpan.Zero; t < quanto; t += p)
        {
            _relogio.Avancar(p);
            await Task.Delay(1);
        }
    }

    /// <summary>Anda o relógio até a tarefa acabar (no máximo 20 s de tempo falso).</summary>
    private async Task<T> Rodar<T>(Task<T> tarefa)
    {
        for (var i = 0; i < 2000 && !tarefa.IsCompleted; i++)
        {
            _relogio.Avancar(TimeSpan.FromMilliseconds(10));
            await Task.Delay(1);
        }
        Assert.True(tarefa.IsCompleted, "a escrita não terminou");
        return await tarefa;
    }

    private int EscritasDe(string prefixo) => _simulador.Escritas.Count(e => e.StartsWith(prefixo, StringComparison.Ordinal));

    // ----------------------------------------------------------------- escrita simples

    [Fact]
    public async Task Escrita_simples_sai_o_quadro_o_eco_confirma_e_o_espelho_mostra()
    {
        await Conectar();
        Assert.Equal(10.0m, _estado.Valor("081", 0)!.Numero); // semeado pela leitura

        var r = await Rodar(_escritor.EscreverAsync(Ganho, 0, -3.0m));

        Assert.True(r.Ok, r.Motivo);
        Assert.False(r.Relido);
        Assert.False(r.Diferente);
        Assert.Contains("*081*0*-3.0*", _simulador.Escritas);
        Assert.Equal("-3.0", _simulador.Valor("081", 0));
        Assert.Equal(-3.0m, _estado.Valor("081", 0)!.Numero);
        Assert.DoesNotContain("#081#0#", _simulador.Escritas.SkipWhile(e => e != "*081*0*-3.0*")); // sem releitura
    }

    [Fact]
    public async Task O_espelho_avisa_a_mudanca_do_parametro()
    {
        await Conectar();
        var mudancas = new List<Endereco>();
        _estado.Mudou += e =>
        {
            lock (mudancas)
                mudancas.Add(e);
        };

        await Rodar(_escritor.EscreverAsync(Ganho, 2, 1.5m));

        lock (mudancas)
            Assert.Equal([new Endereco("081", 2)], mudancas);
    }

    // ----------------------------------------------------------------- arrasto

    [Fact]
    public async Task Arrasto_de_20_valores_em_1_s_faz_no_maximo_4_escritas_e_o_ultimo_vai()
    {
        await Conectar();
        Task<ResultadoEscrita> ultima = Task.FromResult(new ResultadoEscrita(true, null));
        var valores = Enumerable.Range(0, 20).Select(i => -10.0m - i * 0.5m).ToList();

        foreach (var v in valores)
        {
            ultima = _escritor.EscreverAsync(Ganho, 0, v);
            await Andar(TimeSpan.FromMilliseconds(50));
        }
        var r = await Rodar(ultima); // soltou: o último valor sempre vai

        Assert.True(r.Ok, r.Motivo);
        var escritas = _simulador.Escritas.Where(e => e.StartsWith("*081*0*", StringComparison.Ordinal)).ToList();
        Assert.InRange(escritas.Count, 2, 4);
        Assert.Equal("*081*0*-19.5*", escritas[^1]);
        Assert.Equal("-19.5", _simulador.Valor("081", 0));
        Assert.Equal(-19.5m, _estado.Valor("081", 0)!.Numero);
    }

    [Fact]
    public async Task Duas_saidas_arrastadas_juntas_seguem_uma_escrita_por_vez()
    {
        await Conectar();

        var inicio = _relogio.GetUtcNow();
        var a = _escritor.EscreverAsync(Ganho, 0, -1.0m);
        await Esperar(() => _simulador.Escritas.Contains("*081*0*-1.0*")); // a ordem dos pedidos
        var b = _escritor.EscreverAsync(Ganho, 1, -2.0m);
        await Rodar(Task.WhenAll(a, b).ContinueWith(_ => true));
        var duracao = _relogio.GetUtcNow() - inicio;

        var ra = await a;
        var rb = await b;
        Assert.True(ra.Ok);
        Assert.True(rb.Ok);
        Assert.False(ra.Relido); // cada uma confirmada pelo próprio eco
        Assert.False(rb.Relido);
        var escritas = _simulador.Escritas.ToList();
        Assert.True(escritas.IndexOf("*081*1*-2.0*") > escritas.IndexOf("*081*0*-1.0*"));
        Assert.True(duracao < TemposEscrita.Padrao.PrazoEco / 2, $"{duracao}");
        Assert.Equal("-1.0", _simulador.Valor("081", 0));
        Assert.Equal("-2.0", _simulador.Valor("081", 1));
    }

    [Fact]
    public async Task Ouvinte_que_lanca_nao_para_a_fila_do_parametro()
    {
        await Conectar();
        _escritor.Escreveu += (_, _) => throw new InvalidOperationException("ouvinte com defeito");

        var r1 = await Rodar(_escritor.EscreverAsync(Ganho, 0, -1.0m));
        var r2 = await Rodar(_escritor.EscreverAsync(Ganho, 0, -2.0m));

        Assert.True(r1.Ok, r1.Motivo);
        Assert.True(r2.Ok, r2.Motivo);
        Assert.Equal("-2.0", _simulador.Valor("081", 0));
        Assert.Contains(_log.Linhas, l => l.Contains("falha no aviso de escrita"));
    }

    // ----------------------------------------------------------------- leitura e escrita não se misturam

    /// <summary>Prende a próxima leitura completa logo depois da primeira consulta, até <c>Soltar</c>.</summary>
    private (Task Presa, Action Soltar) PrenderProximaLeitura()
    {
        var solta = new ManualResetEventSlim();
        var presa = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var prendeu = 0;
        _leitor.Avancou += _ =>
        {
            if (Interlocked.Exchange(ref prendeu, 1) == 1)
                return;
            presa.SetResult();
            solta.Wait(TimeSpan.FromSeconds(10));
        };
        return (presa.Task.WaitAsync(TimeSpan.FromSeconds(10)), solta.Set);
    }

    [Fact]
    public async Task Ler_de_novo_durante_um_eco_pendente_espera_a_escrita_e_o_espelho_fica_com_o_valor_escrito()
    {
        await Conectar();
        var escrita = _escritor.EscreverAsync(Ganho, 0, -3.0m);
        await Esperar(() => _simulador.Escritas.Contains("*081*0*-3.0*")); // enviada; o eco ainda não veio

        var ler = _leitura.LerAsync(); // "Ler de novo"
        await Task.Delay(100);
        Assert.Equal(1, _simulador.Escritas.Count(e => e == "#080#")); // a leitura espera a escrita

        var r = await Rodar(escrita);
        Assert.True(r.Ok, r.Motivo);
        Assert.False(r.Relido);
        Assert.NotNull(await ler.WaitAsync(TimeSpan.FromSeconds(10)));
        await Esperar(() => !_estado.Lendo);

        Assert.Equal(2, _simulador.Escritas.Count(e => e == "#080#"));
        Assert.Equal(-3.0m, _estado.Valor("081", 0)!.Numero);
    }

    [Fact]
    public async Task Durante_uma_leitura_o_escritor_nao_escreve()
    {
        await Conectar();
        var (presa, soltar) = PrenderProximaLeitura();
        var ler = _leitura.LerAsync();
        await presa;
        Assert.True(_estado.Lendo);

        var r = await Rodar(_escritor.EscreverAsync(Ganho, 0, -3.0m));

        Assert.False(r.Ok);
        Assert.Equal(Escritor.MsgLendo, r.Motivo);
        Assert.Equal(0, EscritasDe("*081*"));

        soltar();
        await ler.WaitAsync(TimeSpan.FromSeconds(10));
        await Esperar(() => !_estado.Lendo);
        var depois = await Rodar(_escritor.EscreverAsync(Ganho, 0, -3.0m));
        Assert.True(depois.Ok, depois.Motivo);
    }

    [Fact]
    public async Task Leitura_que_falha_libera_a_escrita()
    {
        await Conectar();
        var falhar = 1;
        _leitor.Avancou += _ =>
        {
            if (Interlocked.Exchange(ref falhar, 0) == 1)
                throw new InvalidOperationException("falha de teste");
        };

        Assert.Null(await _leitura.LerAsync()); // a leitura lançou
        await Esperar(() => !_estado.Lendo);

        var r = await Rodar(_escritor.EscreverAsync(Ganho, 0, -3.0m));
        Assert.True(r.Ok, r.Motivo);
        Assert.Equal(-3.0m, _estado.Valor("081", 0)!.Numero);
    }

    [Fact]
    public async Task Desconectar_durante_uma_leitura_tira_o_espelho_de_lendo()
    {
        await Conectar();
        var (presa, soltar) = PrenderProximaLeitura();
        var ler = _leitura.LerAsync();
        await presa;
        Assert.True(_estado.Lendo);

        await _sessao.DesconectarAsync();

        Assert.False(_estado.Lendo);
        Assert.False(_estado.Pronto);
        soltar();
        await ler.WaitAsync(TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task Queda_durante_a_escrita_falha_com_MsgCaiu()
    {
        _simulador.Silenciar.Add("*081*0*-3.0*");
        await Conectar();
        var inicio = _relogio.GetUtcNow();
        var tarefa = _escritor.EscreverAsync(Ganho, 0, -3.0m);
        await Andar(TimeSpan.FromMilliseconds(500));
        Assert.False(tarefa.IsCompleted); // esperando o eco

        _simulador.Derrubar();
        var r = await Rodar(tarefa);

        Assert.False(r.Ok);
        Assert.Equal(Escritor.MsgCaiu, r.Motivo);
        Assert.True(_relogio.GetUtcNow() - inicio < TemposEscrita.Padrao.PrazoEco);
        Assert.Equal(["*081*0*-3.0*"], _simulador.Escritas.SkipWhile(e => e != "*081*0*-3.0*")); // sem releitura
        Assert.Null(_estado.Valor("081", 0));
    }

    // ----------------------------------------------------------------- eco e releitura

    [Fact]
    public async Task Eco_diferente_vale_o_eco()
    {
        _simulador.EcosDiferentes["*081*0*-3.0*"] = "#081#0#-2.9#";
        await Conectar();

        var r = await Rodar(_escritor.EscreverAsync(Ganho, 0, -3.0m));

        Assert.True(r.Ok);
        Assert.True(r.Diferente);
        Assert.Equal(-2.9m, r.Valor!.Numero);
        Assert.Equal(-2.9m, _estado.Valor("081", 0)!.Numero);
    }

    [Fact]
    public async Task Sem_eco_rele_e_vale_o_relido()
    {
        _simulador.Silenciar.Add("*081*0*-3.0*");
        await Conectar();

        var tarefa = _escritor.EscreverAsync(Ganho, 0, -3.0m);
        await Andar(TimeSpan.FromMilliseconds(500));
        Assert.False(tarefa.IsCompleted); // ainda esperando o eco (2 s)
        _simulador.Definir("081", 0, "-2.5"); // o aparelho ficou noutro valor
        var r = await Rodar(tarefa);

        Assert.True(r.Ok, r.Motivo);
        Assert.True(r.Relido);
        Assert.True(r.Diferente);
        Assert.Equal(-2.5m, _estado.Valor("081", 0)!.Numero);
        var depois = _simulador.Escritas.SkipWhile(e => e != "*081*0*-3.0*").ToList();
        Assert.Equal(["*081*0*-3.0*", "#081#0#"], depois);
        Assert.Contains(_log.Linhas, l => l.Contains("sem eco") && l.Contains("relendo"));
    }

    [Fact]
    public async Task Sem_eco_e_sem_releitura_e_falha_com_o_motivo()
    {
        _simulador.Silenciar.Add("*081*0*-3.0*");
        await Conectar();
        _simulador.Silenciar.Add("#081#0#"); // depois da leitura completa

        var r = await Rodar(_escritor.EscreverAsync(Ganho, 0, -3.0m));

        Assert.False(r.Ok);
        Assert.Equal(Escritor.MsgSemConfirmacao, r.Motivo);
        Assert.Equal(10.0m, _estado.Valor("081", 0)!.Numero); // o espelho não inventa
        Assert.Contains(_log.Linhas, l => l.Contains(Escritor.MsgSemConfirmacao));
    }

    // ----------------------------------------------------------------- trava

    [Fact]
    public async Task Chave_desligada_nada_sai_e_o_motivo_e_chave_desligada()
    {
        await Conectar();
        Assert.True((await _chaves.DefinirAsync(Chave.AjustesAudio, false, OrigemComando.Tela)).Ok);
        var antes = _simulador.Escritas.Count;

        var r = await Rodar(_escritor.EscreverAsync(Ganho, 0, -3.0m));

        Assert.False(r.Ok);
        Assert.Equal(Sessao.MotivoChaveDesligada, r.Motivo);
        Assert.Equal(antes, _simulador.Escritas.Count);
        Assert.Contains(_log.Linhas, l => l.Contains("✗ *081*0*-3.0*") && l.Contains(Sessao.MotivoChaveDesligada));
    }

    [Fact]
    public async Task Firmware_R004_modo_so_leitura_nada_sai()
    {
        _simulador.Firmware = "R004";
        await Conectar();
        Assert.True(_sessao.SoLeitura);

        var r = await Rodar(_escritor.EscreverAsync(Ganho, 0, -3.0m));

        Assert.False(r.Ok);
        Assert.Equal(Sessao.MotivoSoLeitura, r.Motivo);
        Assert.Equal(0, EscritasDe("*081*"));
    }

    [Theory]
    [InlineData("*240*5*", Sessao.MotivoMemoriaForaDoFormato)]
    [InlineData("*241*3*", Sessao.MotivoMemoriaForaDoFormato)]
    [InlineData("*241*0", Sessao.MotivoMemoriaForaDoFormato)]
    [InlineData("*231*filme*", Sessao.MotivoMemoriaForaDoFormato)]
    [InlineData("*231* FILME*", Sessao.MotivoMemoriaForaDoFormato)]
    [InlineData("*234*FILME*", Sessao.MotivoEscritaForaDoCatalogo)]
    [InlineData("*202*1*", Sessao.MotivoNaoLiberado)]
    [InlineData("*200*-20.0*", Sessao.MotivoNaoLiberado)]
    [InlineData("*004*", Sessao.MotivoNaoLiberado)]
    [InlineData("*242*", Sessao.MotivoNaoLiberado)]
    [InlineData("*001*AB12*CD34*", Sessao.MotivoNaoLiberado)]
    [InlineData("*002*NOVO*", Sessao.MotivoNaoLiberado)]
    [InlineData("*252*0*", Sessao.MotivoNaoLiberado)]
    [InlineData("*251*0*", Sessao.MotivoNaoLiberado)]
    [InlineData("*999*1*", Sessao.MotivoEscritaForaDoCatalogo)]
    [InlineData("*081*0*-3*", Sessao.MotivoEscritaForaDoCatalogo)] // fora do formato do codificador
    [InlineData("*081*4*-3.0*", Sessao.MotivoEscritaForaDoCatalogo)] // canal 4
    [InlineData("*081*0*-46.0*", Sessao.MotivoEscritaForaDoCatalogo)] // fora da faixa
    [InlineData("*020*3*2.5*", Sessao.MotivoEscritaForaDoCatalogo)] // a banda vai sem * nas pontas
    [InlineData("*060*0*90*", Sessao.MotivoEscritaForaDoCatalogo)]
    [InlineData("*081*0*-3.0**", Sessao.MotivoEscritaForaDoCatalogo)]
    [InlineData("texto qualquer", Sessao.MotivoEscritaForaDoCatalogo)]
    public async Task Com_a_chave_ligada_o_que_nao_e_ajuste_de_audio_nem_memoria_e_recusado_antes_do_BLE(string quadro, string motivo)
    {
        await Conectar();
        Assert.True(_chaves.Ligada(Chave.AjustesAudio));
        var antes = _simulador.Escritas.Count;

        var r = await _sessao.EnviarAsync(quadro);

        Assert.False(r.Enviado);
        Assert.Equal(motivo, r.Motivo);
        Assert.Equal(antes, _simulador.Escritas.Count);
        Assert.Contains(_log.Linhas, l => l.Contains("✗") && l.Contains(motivo));
    }

    [Fact]
    public async Task Com_a_chave_ligada_toda_escrita_do_Marco_1_passa_e_nenhuma_dos_extras()
    {
        await Conectar();
        foreach (var p in Catalogo.Todos.Where(p => !p.SomenteLeitura))
        {
            int? indice = p.Escopo == Escopo.Global ? null : 0;
            var quadro = p.Tipo switch
            {
                TipoValor.Roteamento => p.CodificarEscrita(indice, "a+b"),
                TipoValor.Nome => p.CodificarEscrita(indice, "X"),
                TipoValor.Booleano => p.CodificarEscrita(indice, false),
                _ when p.Opcoes is not null => p.CodificarEscrita(indice, p.Opcoes.Keys.First()),
                _ => p.CodificarEscrita(indice, p.Maximo ?? 0m),
            };

            var r = await _sessao.EnviarAsync(quadro);

            // Os nomes das memórias (23N) passam com a chave "Salvar memória" (história 6).
            var passa = !p.Extra || Catalogo.ComandoDeMemoria(quadro) is not null;
            Assert.True(r.Enviado == passa, $"{quadro}: {r.Motivo}");
            Assert.Equal(passa, _simulador.Escritas.Contains(quadro));
        }
        await Andar(TimeSpan.FromMilliseconds(200)); // os ecos
    }

    [Fact]
    public async Task Sem_conexao_a_escrita_de_audio_e_recusada()
    {
        await _chaves.Carregada;

        var r = await _sessao.EnviarAsync("*081*0*-3.0*");

        Assert.False(r.Enviado);
        Assert.Equal(Sessao.MotivoSemConexao, r.Motivo);
        Assert.Empty(_simulador.Escritas);
    }

    // ----------------------------------------------------------------- espelho

    [Fact]
    public async Task Antes_da_primeira_leitura_o_escritor_nao_escreve()
    {
        var estado = new EstadoAparelho(_sessao); // sem a leitura automática: nunca semeado
        var escritor = new Escritor(_sessao, estado, _log, relogio: _relogio);
        await Conectar();
        var antes = _simulador.Escritas.Count;

        var r = await Rodar(escritor.EscreverAsync(Ganho, 0, -3.0m));

        Assert.False(r.Ok);
        Assert.Equal(Escritor.MsgSemLeitura, r.Motivo);
        Assert.Equal(antes, _simulador.Escritas.Count);
    }

    [Fact]
    public async Task Ao_desconectar_o_espelho_esvazia()
    {
        await Conectar();
        Assert.NotNull(_estado.Valor("081", 0));

        await _sessao.DesconectarAsync();

        Assert.False(_estado.Pronto);
        Assert.Null(_estado.Valor("081", 0));
        var r = await Rodar(_escritor.EscreverAsync(Ganho, 0, -3.0m));
        Assert.False(r.Ok);
        Assert.Equal(Sessao.MotivoSemConexao, r.Motivo); // a conexão, não "ainda não leu"
    }

    // ----------------------------------------------------------------- chaves de acesso

    [Fact]
    public async Task As_11_chaves_com_os_padroes_e_os_riscos_da_spec()
    {
        await _chaves.Carregada;

        Assert.Equal(11, _chaves.Definicoes.Count);
        Assert.Equal(Enum.GetValues<Chave>(), _chaves.Definicoes.Select(d => d.Chave));
        foreach (var c in new[] { Chave.ResetFabrica, Chave.IaPelaRede, Chave.ResetAudio, Chave.TrocaSenha, Chave.Gerador })
            Assert.False(_chaves.Ligada(c), c.ToString());
        foreach (var c in new[] { Chave.CarregarMemoria, Chave.SalvarMemoria, Chave.RemoteOut, Chave.BluetoothAudio, Chave.NomeProduto, Chave.AjustesAudio })
            Assert.True(_chaves.Ligada(c), c.ToString());
        Assert.Equal("Um tom forte pode queimar o tweeter ou o sub.", ChavesPadrao.De(Chave.Gerador).Risco);
        Assert.Equal("Apaga a senha e as 3 memórias.", ChavesPadrao.De(Chave.ResetFabrica).Risco);
        Assert.Equal("Deixa a IA comandar o som pela rede.", ChavesPadrao.De(Chave.IaPelaRede).Risco);
        Assert.Null(ChavesPadrao.De(Chave.AjustesAudio).Risco);
        Assert.Equal(ConfirmacaoIa.AoSobrescrever, ChavesPadrao.De(Chave.SalvarMemoria).ConfirmacaoIa);
    }

    [Theory]
    [InlineData(Chave.Gerador, true)]
    [InlineData(Chave.AjustesAudio, false)]
    [InlineData(Chave.IaPelaRede, true)]
    public async Task A_IA_nunca_liga_nem_desliga_uma_chave(Chave chave, bool ligar)
    {
        await _chaves.Carregada;
        var antes = _chaves.Ligada(chave);

        var r = await _chaves.DefinirAsync(chave, ligar, OrigemComando.Ia);

        Assert.False(r.Ok);
        Assert.Equal("A IA nunca liga nem desliga uma chave.", r.Motivo);
        Assert.Equal(antes, _chaves.Ligada(chave));
        Assert.Equal(0, _repositorio.Gravacoes);
    }

    [Fact]
    public async Task Ligar_uma_chave_e_recriar_mantem_o_estado()
    {
        await _chaves.Carregada;
        Assert.True((await _chaves.DefinirAsync(Chave.Gerador, true, OrigemComando.Tela)).Ok);
        Assert.True((await _chaves.DefinirAsync(Chave.AjustesAudio, false, OrigemComando.Tela)).Ok);

        var recriadas = new Chaves(_repositorio);
        await recriadas.Carregada;

        Assert.True(recriadas.Ligada(Chave.Gerador));
        Assert.False(recriadas.Ligada(Chave.AjustesAudio));
        Assert.True(recriadas.Ligada(Chave.RemoteOut)); // o resto segue o padrão
        Assert.Contains("\"gerador\": true", _repositorio.Json);
    }

    [Fact]
    public async Task Antes_de_ler_o_guardado_toda_chave_conta_como_desligada()
    {
        var leitura = new TaskCompletionSource<string?>();
        var repositorio = new RepositorioMemoria { Leitura = leitura.Task };
        var chaves = new Chaves(repositorio);

        Assert.False(chaves.Ligada(Chave.AjustesAudio));
        leitura.SetResult(null);
        await chaves.Carregada;
        Assert.True(chaves.Ligada(Chave.AjustesAudio));
    }

    [Theory]
    [InlineData("isto não é json")]
    [InlineData("{\"chaves\": {\"gerador\": \"sim\"}}")]
    [InlineData("[]")]
    public async Task Guardado_estragado_volta_aos_padroes(string json)
    {
        var chaves = new Chaves(new RepositorioMemoria { Json = json });
        await chaves.Carregada;

        Assert.False(chaves.Ligada(Chave.Gerador));
        Assert.True(chaves.Ligada(Chave.AjustesAudio));
    }

    [Fact]
    public async Task Falha_ao_guardar_nao_liga_mas_desliga()
    {
        await _chaves.Carregada;
        _repositorio.FalharAoGravar = true;

        var ligar = await _chaves.DefinirAsync(Chave.Gerador, true, OrigemComando.Tela);
        var desligar = await _chaves.DefinirAsync(Chave.AjustesAudio, false, OrigemComando.Tela);

        Assert.False(ligar.Ok);
        Assert.False(_chaves.Ligada(Chave.Gerador));
        Assert.True(desligar.Ok); // desligar é o lado seguro: vale mesmo sem guardar
        Assert.Equal(Chaves.MsgDesligadaSemGuardar, desligar.Motivo);
        Assert.Contains("ao reabrir o app ela pode voltar ligada", desligar.Motivo);
        Assert.False(_chaves.Ligada(Chave.AjustesAudio));
        Assert.Equal(2 * Chaves.TentativasDeGravar, _repositorio.TentativasDeGravar); // tentou de novo
    }

    [Fact]
    public async Task Uma_falha_passageira_ao_guardar_e_vencida_pela_nova_tentativa()
    {
        await _chaves.Carregada;
        _repositorio.FalhasAoGravar = 1;

        var r = await _chaves.DefinirAsync(Chave.Gerador, true, OrigemComando.Tela);

        Assert.True(r.Ok);
        Assert.Null(r.Motivo);
        Assert.True(_chaves.Ligada(Chave.Gerador));
        Assert.Equal(1, _repositorio.Gravacoes);
        Assert.Contains("\"gerador\": true", _repositorio.Json);
    }

    [Fact]
    public async Task Falha_ao_ler_o_guardado_fica_com_os_padroes()
    {
        var repositorio = new RepositorioMemoria { FalharAoLer = true };
        var chaves = new Chaves(repositorio);

        await chaves.Carregada.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(chaves.Ligada(Chave.AjustesAudio));
        Assert.False(chaves.Ligada(Chave.Gerador));
        Assert.True((await chaves.DefinirAsync(Chave.Gerador, true, OrigemComando.Tela)).Ok);
        Assert.True(chaves.Ligada(Chave.Gerador));
    }

    [Theory]
    [InlineData("{\"chaves\": {\"gerador\": true, \"ajustes-audio\": false}}")] // sem formato
    [InlineData("{\"formato\": 2, \"chaves\": {\"gerador\": true, \"ajustes-audio\": false}}")]
    [InlineData("{\"formato\": \"1\", \"chaves\": {\"gerador\": true, \"ajustes-audio\": false}}")]
    public async Task Guardado_sem_o_formato_1_fica_com_os_padroes(string json)
    {
        var chaves = new Chaves(new RepositorioMemoria { Json = json });
        await chaves.Carregada;

        Assert.False(chaves.Ligada(Chave.Gerador));
        Assert.True(chaves.Ligada(Chave.AjustesAudio));
    }

    [Fact]
    public async Task Guardado_com_o_formato_1_vale()
    {
        var chaves = new Chaves(new RepositorioMemoria { Json = "{\"formato\": 1, \"chaves\": {\"gerador\": true, \"ajustes-audio\": false}}" });
        await chaves.Carregada;

        Assert.True(chaves.Ligada(Chave.Gerador));
        Assert.False(chaves.Ligada(Chave.AjustesAudio));
    }

    // ----------------------------------------------------------------- booleanos e preset

    [Theory]
    [InlineData("100", 1, "*100*0*1*", true)] // mute
    [InlineData("100", 0, "*100*0*0*", false)]
    [InlineData("070", 1, "*070*0*1*", true)] // limiter auto
    [InlineData("070", 0, "*070*0*0*", false)]
    public async Task Booleano_e_codificado_enviado_e_confirmado(string codigo, int valor, string quadro, bool ligado)
    {
        await Conectar();

        var r = await Rodar(_escritor.EscreverAsync(Catalogo.ParametroDe(codigo)!, 0, valor));

        Assert.True(r.Ok, r.Motivo);
        Assert.False(r.Relido);
        Assert.Contains(quadro, _simulador.Escritas);
        Assert.Equal(valor.ToString(), _simulador.Valor(codigo, 0));
        Assert.Equal(ligado, _estado.Valor(codigo, 0)!.Ligado);
    }

    [Fact]
    public async Task Preset_de_EQ_e_codificado_enviado_e_confirmado()
    {
        await Conectar();

        var r = await Rodar(_escritor.EscreverAsync(Catalogo.ParametroDe("220")!, null, 7m));

        Assert.True(r.Ok, r.Motivo);
        Assert.False(r.Relido);
        Assert.Contains("*220*7*", _simulador.Escritas);
        Assert.Equal("7", _simulador.Valor("220"));
        Assert.Equal("Rock Style", _estado.Valor("220")!.Rotulo);
    }
}
