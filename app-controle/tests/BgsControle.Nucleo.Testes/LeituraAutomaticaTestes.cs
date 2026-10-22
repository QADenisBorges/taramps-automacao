using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using BgsControle.Nucleo.Leitura;
using BgsControle.Nucleo.Registro;

namespace BgsControle.Nucleo.Testes;

/// <summary>A regra "ler uma vez ao entrar em Conectado", o "Ler de novo" e a leitura nova, com o transporte falso.</summary>
public sealed class LeituraAutomaticaTestes
{
    private static readonly TemposSessao Rapidos = new(
        Scan: TimeSpan.FromSeconds(1),
        TentativaConexao: TimeSpan.FromSeconds(1),
        PausaEntreTentativas: TimeSpan.FromMilliseconds(1),
        RespostaSenha: TimeSpan.FromSeconds(1),
        PrazoSenha: TimeSpan.FromSeconds(1));

    // Os extras ficam mudos no transporte falso: cada um espera o prazo.
    private static readonly TemposLeitura LeituraRapida = new(TimeSpan.Zero, TimeSpan.FromMilliseconds(20));

    /// <summary>Como uma leitura acabou: pelo <c>Terminou</c> (retrato) ou pelo <c>Falhou</c> (erro).</summary>
    private sealed record Fim(Retrato? Retrato, Exception? Erro);

    private readonly TransporteFalso _transporte = new();
    private readonly CofreFalso _cofre = new() { Senha = "AB12" };
    private readonly LogBle _log = new();
    private readonly Sessao _sessao;
    private readonly LeituraAutomatica _auto;
    private readonly ConcurrentQueue<Fim> _fins = new();
    private int _comecos;

    public LeituraAutomaticaTestes()
    {
        _sessao = new Sessao(_transporte, _cofre, _log, Rapidos);
        var leitor = new LeitorCompleto(_sessao, _log, LeituraRapida);
        _auto = new LeituraAutomatica(_sessao, leitor);
        _auto.Comecou += () => Interlocked.Increment(ref _comecos);
        _auto.Terminou += r => _fins.Enqueue(new Fim(r, null));
        _auto.Falhou += e => _fins.Enqueue(new Fim(null, e));

        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Dados/leitura-2026-10-18_2145.json")));
        foreach (var l in doc.RootElement.GetProperty("leituras").EnumerateArray())
        {
            int? canal = l.GetProperty("canal").ValueKind == JsonValueKind.Null ? null : l.GetProperty("canal").GetInt32();
            var consulta = Catalogo.ParametroDe(l.GetProperty("codigo").GetString()!)!.TextoConsulta(canal);
            _transporte.Respostas[consulta] = [l.GetProperty("cru").GetString() + "\r\n"];
        }
    }

    /// <summary>Quantas leituras passaram pelo transporte (cada uma pergunta o master uma vez).</summary>
    private int Leituras => _transporte.Escritas.Count(e => e == "#080#");

    private async Task EsperarFins(int n)
    {
        var limite = DateTime.UtcNow.AddSeconds(10);
        while (_fins.Count < n || _auto.Lendo)
        {
            if (DateTime.UtcNow > limite)
                throw new TimeoutException($"Esperava {n} leituras terminadas; vieram {_fins.Count}.");
            await Task.Delay(5);
        }
    }

    private static void DispararMudou(Sessao sessao)
    {
        var campo = typeof(Sessao).GetField(nameof(Sessao.Mudou), BindingFlags.Instance | BindingFlags.NonPublic)!;
        ((Action?)campo.GetValue(sessao))?.Invoke();
    }

    [Fact]
    public async Task Entrar_em_Conectado_le_sozinho_uma_vez()
    {
        await _sessao.ConectarAsync();
        Assert.Equal(EstadoSessao.Conectado, _sessao.Estado);
        await EsperarFins(1);

        var fim = Assert.Single(_fins);
        Assert.Null(fim.Erro);
        Assert.True(fim.Retrato!.Completo);
        Assert.Equal("84 de 84", fim.Retrato.PlacarMarco1.Texto);
        Assert.Equal(1, _comecos);
        Assert.Equal(1, _auto.Iniciadas);
        foreach (var c in Catalogo.Consultas)
            Assert.Single(_transporte.Escritas, c.Texto);
    }

    [Fact]
    public async Task Sem_conectar_nao_le()
    {
        await _sessao.ConectarAsync();
        await EsperarFins(1);
        _cofre.Senha = null;
        await _sessao.DesconectarAsync();
        var escritas = _transporte.Escritas.Count;

        await _sessao.ConectarAsync(); // sem senha guardada: pede a senha
        Assert.Equal(EstadoSessao.PedindoSenha, _sessao.Estado);

        Assert.False(_auto.Lendo);
        Assert.Null(await _auto.LerAsync());
        Assert.Null(await _auto.LerNovaAsync());
        Assert.Equal(1, _comecos);
        Assert.Equal(escritas, _transporte.Escritas.Count);
    }

    [Fact]
    public async Task Mudou_repetido_em_Conectado_nao_le_de_novo()
    {
        var durante = 0;
        _auto.Avancou += r =>
        {
            if (r.Feitas == 10 && durante++ == 0)
            {
                DispararMudou(_sessao);
                DispararMudou(_sessao);
            }
        };
        await _sessao.ConectarAsync();
        await EsperarFins(1);

        for (var i = 0; i < 3; i++)
        {
            DispararMudou(_sessao);
            Assert.False(_auto.Lendo); // uma leitura nova começaria aqui mesmo, na hora
        }

        Assert.Equal(1, durante);
        Assert.Equal(1, _comecos);
        Assert.Equal(1, Leituras);
        Assert.Single(_fins);
    }

    [Fact]
    public async Task Cair_e_conectar_de_novo_le_de_novo()
    {
        await _sessao.ConectarAsync();
        await EsperarFins(1);

        _transporte.Derrubar();
        Assert.Equal(EstadoSessao.Desconectado, _sessao.Estado);
        await _sessao.ConectarAsync();
        await EsperarFins(2);

        Assert.Equal(2, _comecos);
        Assert.Equal(2, Leituras);
        Assert.All(_fins, f => Assert.True(f.Retrato!.Completo));
    }

    [Fact]
    public async Task Cair_no_meio_e_reconectar_na_hora_tambem_le_de_novo()
    {
        _transporte.DerrubarNaConsulta = 40;
        var reconectou = 0;
        var conectadoAntesDoFim = false;
        _log.Registrou += linha =>
        {
            // A leitura velha está parando (ainda "lendo"): reconecta e só a solta depois.
            if (linha.Contains("leitura incompleta") && Interlocked.Exchange(ref reconectou, 1) == 0)
            {
                _ = Task.Run(_sessao.ConectarAsync);
                conectadoAntesDoFim = SpinWait.SpinUntil(() => _sessao.Estado == EstadoSessao.Conectado, TimeSpan.FromSeconds(5));
            }
        };

        await _sessao.ConectarAsync();
        await EsperarFins(2);

        Assert.True(conectadoAntesDoFim);
        Assert.Equal(2, _comecos);
        var fins = _fins.ToArray();
        Assert.False(fins[0].Retrato!.Completo);
        Assert.Equal(Sessao.MsgCaiu, fins[0].Retrato!.Motivo);
        Assert.True(fins[1].Retrato!.Completo);
        Assert.Equal("84 de 84", fins[1].Retrato!.PlacarMarco1.Texto);
    }

    [Fact]
    public async Task Ler_de_novo_com_a_leitura_parada_le_outra_vez()
    {
        await _sessao.ConectarAsync();
        await EsperarFins(1);

        var r = await _auto.LerAsync();

        Assert.NotNull(r);
        Assert.True(r.Completo);
        Assert.Equal(2, _comecos);
        Assert.Equal(2, Leituras);
        Assert.Equal(2, _fins.Count);
    }

    [Fact]
    public async Task Ler_de_novo_durante_a_leitura_e_ignorado()
    {
        Task<Retrato?>? ignorado = null;
        _auto.Avancou += r =>
        {
            if (r.Feitas == 5 && ignorado is null)
                ignorado = _auto.LerAsync();
        };

        await _sessao.ConectarAsync();
        await EsperarFins(1);

        Assert.NotNull(ignorado);
        Assert.True(ignorado.IsCompletedSuccessfully);
        Assert.Null(await ignorado);
        Assert.Equal(1, _comecos);
        Assert.Equal(1, Leituras);
    }

    [Fact]
    public async Task Erro_na_leitura_chega_no_Falhou_e_libera_a_proxima()
    {
        var falhar = true;
        _auto.Avancou += _ =>
        {
            if (falhar)
                throw new InvalidOperationException("quebrou");
        };

        await _sessao.ConectarAsync();
        await EsperarFins(1);

        var fim = Assert.Single(_fins);
        Assert.Null(fim.Retrato);
        Assert.IsType<InvalidOperationException>(fim.Erro);
        Assert.False(_auto.Lendo);

        falhar = false;
        var r = await _auto.LerAsync();
        Assert.True(r!.Completo);
    }

    [Fact]
    public async Task Assinante_que_lanca_no_Comecou_ou_no_Terminou_nao_prende_a_leitura()
    {
        await _sessao.ConectarAsync();
        await EsperarFins(1);

        var lancarNoComeco = true;
        var lancarNoFim = false;
        _auto.Comecou += () =>
        {
            if (lancarNoComeco)
                throw new InvalidOperationException("começo");
        };
        _auto.Terminou += _ =>
        {
            if (lancarNoFim)
                throw new InvalidOperationException("fim");
        };

        Assert.Null(await _auto.LerAsync()); // o Comecou lançou: a leitura falhou, o erro veio no Falhou
        Assert.Equal("começo", _fins.Last().Erro!.Message);
        Assert.False(_auto.Lendo);
        Assert.Equal(1, Leituras); // não chegou a perguntar nada

        lancarNoComeco = false;
        lancarNoFim = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() => _auto.LerAsync());
        Assert.False(_auto.Lendo);

        lancarNoFim = false;
        var r = await _auto.LerAsync();
        Assert.True(r!.Completo);
        Assert.Equal(3, Leituras);
    }

    [Fact]
    public async Task Assinante_que_lanca_no_Comecou_e_no_Falhou_tambem_nao_prende_a_leitura()
    {
        await _sessao.ConectarAsync();
        await EsperarFins(1);

        var lancar = true;
        _auto.Comecou += () =>
        {
            if (lancar)
                throw new InvalidOperationException("começo");
        };
        _auto.Falhou += _ =>
        {
            if (lancar)
                throw new InvalidOperationException("falha");
        };

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(() => _auto.LerAsync());
        Assert.Equal("falha", erro.Message);
        Assert.False(_auto.Lendo);

        lancar = false;
        var r = await _auto.LerAsync();
        Assert.True(r!.Completo);
    }

    [Fact]
    public async Task Avancou_repassa_os_parciais_da_leitura()
    {
        var parciais = new ConcurrentQueue<int>();
        _auto.Avancou += r => parciais.Enqueue(r.Feitas);

        await _sessao.ConectarAsync();
        await EsperarFins(1);

        Assert.Equal(Enumerable.Range(1, Catalogo.Consultas.Count), parciais);
    }

    // ----------------------------------------------------------------- leitura nova (LerNovaAsync)

    [Fact]
    public async Task Leitura_nova_com_a_leitura_parada_le_agora()
    {
        await _sessao.ConectarAsync();
        await EsperarFins(1);

        var r = await _auto.LerNovaAsync();

        Assert.True(r!.Completo);
        Assert.Equal(2, _auto.Iniciadas);
        Assert.Equal(2, Leituras);
    }

    [Fact]
    public async Task Leitura_nova_durante_outra_espera_e_le_depois_dela()
    {
        Task<Retrato?>? nova = null;
        Task<Retrato?>? outraNova = null;
        var leiturasAoPedir = -1;
        _auto.Avancou += r =>
        {
            if (r.Feitas == 5 && nova is null)
            {
                leiturasAoPedir = Leituras;
                nova = _auto.LerNovaAsync();
                outraNova = _auto.LerNovaAsync(); // as duas esperam a mesma leitura seguinte
            }
        };

        await _sessao.ConectarAsync();
        await EsperarFins(2);
        var r = await nova!;

        Assert.Equal(1, leiturasAoPedir);
        Assert.True(r!.Completo);
        Assert.Same(r, await outraNova!);
        Assert.Equal(2, _auto.Iniciadas);
        Assert.Equal(2, Leituras);
        Assert.Same(r, _fins.Last().Retrato); // o retrato é o da segunda, começada depois do pedido
        Assert.NotSame(r, _fins.First().Retrato);
    }

    [Fact]
    public async Task Leitura_nova_pendente_devolve_null_se_a_sessao_cair()
    {
        Task<Retrato?>? nova = null;
        _auto.Avancou += r =>
        {
            if (r.Feitas == 5 && nova is null)
            {
                nova = _auto.LerNovaAsync();
                _transporte.Derrubar();
            }
        };

        await _sessao.ConectarAsync();
        await EsperarFins(1);

        Assert.Null(await nova!);
        Assert.Equal(1, _auto.Iniciadas);
        Assert.False(_auto.Lendo);
        Assert.False(_fins.Single().Retrato!.Completo);
    }

    [Fact]
    public async Task Leitura_nova_espera_mesmo_se_o_Comecou_da_seguinte_lancar()
    {
        Task<Retrato?>? nova = null;
        var lancar = false;
        _auto.Comecou += () =>
        {
            if (lancar)
                throw new InvalidOperationException("começo");
        };
        _auto.Avancou += r =>
        {
            if (r.Feitas == 5 && nova is null)
            {
                nova = _auto.LerNovaAsync();
                lancar = true; // a leitura seguinte vai falhar ao começar
            }
        };

        await _sessao.ConectarAsync();
        await EsperarFins(2);

        Assert.Null(await nova!);
        Assert.False(_auto.Lendo);
        Assert.Equal("começo", _fins.Last().Erro!.Message);

        lancar = false;
        Assert.True((await _auto.LerAsync())!.Completo);
    }
}
