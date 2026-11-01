using System.Reflection;
using System.Text.RegularExpressions;
using BgsControle.Nucleo;
using BgsControle.Nucleo.Escrita;
using BgsControle.Nucleo.Player;
using BgsControle.Nucleo.Presets;
using BgsControle.Nucleo.Rendimento;
using BgsControle.Nucleo.Setups;
using BgsControle.Simulador;

namespace BgsControle.Telas.Testes;

/// <summary>
/// O reprodutor do teste a seco nas telas: toca na hora e, num trecho, manda ao simulador o pico das JBL; segura um
/// trecho até o teste soltar (ou o reprodutor parar).
/// </summary>
internal sealed class ReprodutorDoTeste(ProcessadorSimulado simulador) : IReprodutor
{
    private readonly object _trava = new();
    private readonly Dictionary<string, double> _picos = new(StringComparer.Ordinal);
    private TaskCompletionSource<bool>? _segurado;
    private int _trechos;

    public void Pico(string uri, double jbl)
    {
        lock (_trava)
            _picos[uri] = jbl;
    }

    /// <summary>O trecho n (1 = o primeiro) fica tocando até parar.</summary>
    public int? Segurar { get; set; }

    public bool Segurando
    {
        get
        {
            lock (_trava)
                return _segurado is not null;
        }
    }

    public TimeSpan? Posicao => null;

    public event Action<string>? Terminou;

    public event Action<string>? Pausou;

    public event Action<string>? Falhou;

    public Task<bool> TocarAsync(string uri) => Task.FromResult(true);

    public Task PausarAsync() => Task.CompletedTask;

    public Task<bool> ContinuarAsync() => Task.FromResult(true);

    public Task PararAsync()
    {
        TaskCompletionSource<bool>? segurado;
        lock (_trava)
        {
            segurado = _segurado;
            _segurado = null;
        }
        segurado?.TrySetResult(false);
        return Task.CompletedTask;
    }

    public async Task<bool> TocarTrechoAsync(string uri, TimeSpan inicio, TimeSpan duracao, CancellationToken cancelar = default)
    {
        double? pico;
        TaskCompletionSource<bool>? segurado = null;
        lock (_trava)
        {
            _trechos++;
            pico = _picos.TryGetValue(uri, out var p) ? p : null;
            if (Segurar == _trechos)
                segurado = _segurado = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        }
        if (pico is { } jbl)
        {
            simulador.Sinal(0, jbl);
            simulador.Sinal(1, jbl);
        }
        if (segurado is null)
            return true;
        using (cancelar.Register(() => segurado.TrySetResult(false)))
            return await segurado.Task;
    }

    /// <summary>Só para o compilador: os eventos existem na interface.</summary>
    public void Nada()
    {
        Terminou?.Invoke("");
        Pausou?.Invoke("");
        Falhou?.Invoke("");
    }
}

/// <summary>Outro app do celular nas telas: o teste dispara o "começou".</summary>
internal sealed class OutroPlayerDoTeste : IOutroPlayer
{
    public event Action? Comecou;

    public void Disparar() => Comecou?.Invoke();
}

/// <summary>O volume de mídia do celular nas telas (0 a 15).</summary>
internal sealed class VolumeDoTeste : IVolumeCelular
{
    private int _volume = 15;

    public int Volume => Volatile.Read(ref _volume);

    public int Maximo => 15;

    public event Action? Mudou;

    public Task<bool> DefinirAsync(int volume)
    {
        Volatile.Write(ref _volume, volume);
        Mudou?.Invoke();
        return Task.FromResult(true);
    }

    public void Mudar(int volume)
    {
        Volatile.Write(ref _volume, volume);
        Mudou?.Invoke();
    }
}

/// <summary>
/// História 13e nas telas (sem XAML), contra o processador simulado: o teste a seco, um passo por tela (o aviso, os
/// trechos com o progresso, o resultado e o cancelar), e a linha do Extra e o selo "revalidar" na folha do nível. O
/// Casa 2.1 calibrado e a PLANA em uso no Máximo, com os limitadores das JBL em −5,0 dB. Nenhum texto com "IA".
/// </summary>
public sealed class TesteSecoViewModelTestes : IDisposable
{
    private static readonly Regex PalavraIa = new(@"\bIA\b", RegexOptions.CultureInvariant);
    private const string IdPlana = "memoria-plana";
    private const string IdPlano = "preset-plano";

    private readonly Bancada _b;
    private readonly DecodificadorSintetico _decodificador = new();
    private readonly ReprodutorDoTeste _reprodutor;
    private readonly AnalisePlaylist _analise;
    private readonly Player _player;
    private readonly TesteSeco _teste;
    private readonly TesteSecoViewModel _vm;
    private readonly NivelViewModel _nivelVm;
    private readonly OutroPlayerDoTeste _outro = new();
    private readonly VolumeDoTeste _volume = new();
    private readonly FiscalExtra _fiscal;
    private readonly PlayerViewModel _playerVm;

    public TesteSecoViewModelTestes()
    {
        var plana = CasaDoDenis.LeituraFilme.Select(v => (v.Codigo, v.Indice) switch
        {
            ("081", 0 or 1) => v with { NoFio = "+15.0" },
            ("081", _) => v with { NoFio = "-15.0" },
            ("091", _) or ("030", _) or ("020", _) or ("080", _) => v with { NoFio = "+0.0" },
            ("042", 0 or 1) => v with { NoFio = "3" },
            ("040", 0 or 1) => v with { NoFio = "200" },
            ("071", 0 or 1) => v with { NoFio = "-5.0" },
            _ => v,
        }).ToArray();
        var regulagem = plana.Where(v => Camadas.De(v.Codigo) == Camada.Regulagem).ToArray();
        var plano = new Preset(IdPlano, "Plano", CasaDoDenis.QuandoFilme, GerentePresets.BandasPlanas);
        var memoria = new MemoriaCelular(IdPlana, CasaDoDenis.IdSetupCasa, "PLANA", CasaDoDenis.QuandoFilme, regulagem, IdPlano)
        {
            Nivel = NivelRendimento.Maximo,
        };
        var dados = new DadosSetups([CasaDoDenis.SetupCasaComTermico], [memoria], CasaDoDenis.IdSetupCasa,
            new Dictionary<string, UsoDoSetup> { [CasaDoDenis.IdSetupCasa] = new([IdPlana], [IdPlana, null, null]) });
        var arquivos = new ArquivosFalso();
        arquivos.Semear(RepositorioTextoArquivos.ArquivoSetups, GerenteSetups.ParaJson(dados));
        arquivos.Semear(RepositorioTextoArquivos.ArquivoPresets, GerentePresets.ParaJson([plano]));
        _b = new Bancada(arquivos, setups: true);
        foreach (var v in plana)
            _b.Simulador.Definir(v.Codigo, v.Indice, v.NoFio);
        _reprodutor = new ReprodutorDoTeste(_b.Simulador);
        _analise = new AnalisePlaylist(_b.Setups!, _b.Presets, _b.Playlists, _decodificador,
            new RepositorioTextoArquivos(arquivos, RepositorioTextoArquivos.ArquivoAnalises), _b.Log);
        _player = new Player(_b.Playlists, _b.Setups!, _reprodutor, _b.Log) { EmAnalise = _analise.EmAnalise };
        _teste = new TesteSeco(_b.Sessao, _b.Estado, _b.Escritor, _b.Copia, _b.Setups!, _analise, _b.Comandos, _b.Monitor, _reprodutor,
            _b.Chaves, _b.Log, _player, _b.Gerador, _b.Relogio, TimeSpan.FromMilliseconds(200), _volume);
        _vm = new TesteSecoViewModel(_teste, _b.Setups!, _analise, _b.Sessao, _b.Copia, _b.Estado, _b.Tela, _b.Relogio);
        _nivelVm = new NivelViewModel(_b.Setups!, _b.Estado, _b.Tela, _b.Relogio, _analise);
        _fiscal = new FiscalExtra(_b.Sessao, _b.Estado, _b.Escritor, _b.Copia, _b.Setups!, _analise, _player, _reprodutor, _b.Monitor,
            _outro, _volume, _b.Log, _b.Relogio, _teste);
        _playerVm = new PlayerViewModel(_player, _b.Playlists, _b.Setups!, new SeletorFalso(), _b.Tela, _b.Relogio, analise: _analise,
            fiscal: _fiscal);
    }

    public void Dispose()
    {
        _vm.Dispose();
        _nivelVm.Dispose();
        _playerVm.Dispose();
        _fiscal.Dispose();
        _teste.Dispose();
        _player.Dispose();
        _analise.Dispose();
        Assert.Empty(_b.Tela.Erros);
    }

    // ----------------------------------------------------------------- apoio

    private async Task AndarAte(Func<bool> condicao)
    {
        for (var i = 0; i < 3000 && !condicao(); i++)
        {
            _b.Relogio.Avancar(TimeSpan.FromMilliseconds(10));
            await Task.Delay(1);
        }
        Assert.True(condicao(), "a condição não ficou verdadeira");
        await _b.Tela.EsvaziarAsync();
    }

    /// <summary>Conecta, põe 3 senos (−3, −6 e −10 dBFS) e otimiza a PLANA: a análise pronta.</summary>
    private async Task Pronto()
    {
        await _b.Conectar();
        await _analise.Carregada;
        Assert.Equal(IdPlana, _b.Setups!.MemoriaEmUso?.Id);
        foreach (var (uri, pico) in new[] { ("seno-3", -3.0), ("seno-6", -6.0), ("seno-10", -10.0) })
        {
            _decodificador.Seno(uri, pico);
            _reprodutor.Pico(uri, pico - 13 + 15);
        }
        var r = await _b.Playlists.AdicionarAsync(IdPlana, new[] { ("seno-3", 3), ("seno-6", 6), ("seno-10", 10) }
            .Select(f => new ArquivoAudio(f.Item1, $"menos {f.Item2}", "Testes", TimeSpan.FromSeconds(2))));
        Assert.True(r.Ok, r.Mensagem);
        var otimizar = _analise.OtimizarAsync(IdPlana);
        await AndarAte(() => otimizar.IsCompleted);
        Assert.True((await otimizar).Ok);
        await AndarAte(() => !_analise.Ocupada && !_analise.Analisando(IdPlana) && _analise.TetoEmUso().Estado == EstadoExtra.Pronta);
    }

    private void SemIa(object vm)
    {
        foreach (var p in vm.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
                     .Where(p => p.PropertyType == typeof(string) && p.GetIndexParameters().Length == 0))
        {
            if (p.GetValue(vm) is string texto)
                Assert.False(PalavraIa.IsMatch(texto), $"{p.Name}: {texto}");
        }
    }

    /// <summary>História 13g: só a marca do passo de agora está ligada; a frase igual à instrução não se repete.</summary>
    private void SoNo(string passo)
    {
        var marcas = new (string Nome, bool Valor)[]
        {
            (nameof(_vm.Fechado), _vm.Fechado), (nameof(_vm.NoAviso), _vm.NoAviso), (nameof(_vm.Andando), _vm.Andando),
            (nameof(_vm.NoResultado), _vm.NoResultado),
        };
        Assert.Equal([passo], marcas.Where(m => m.Valor).Select(m => m.Nome));
        if (_vm.Mensagem is not null && _vm.Mensagem == _vm.Instrucao)
            Assert.Null(_vm.MensagemNaTela);
    }

    // ----------------------------------------------------------------- os passos

    [Fact]
    public async Task Sem_a_analise_pronta_a_trava_diz_o_motivo()
    {
        await _b.Conectar();
        await _b.Tela.EsvaziarAsync();

        Assert.False(_vm.Aberto);
        Assert.Equal("Teste a seco", _vm.Titulo);
        Assert.Equal("Otimize a memória na playlist antes do teste a seco.", _vm.MotivoTrava);
        await _vm.Abrir.ExecutarAsync();
        await _b.Tela.EsvaziarAsync();
        Assert.False(_vm.Aberto);
        Assert.Equal("Otimize a memória na playlist antes do teste a seco.", _vm.Mensagem);
        SemIa(_vm);
    }

    [Fact]
    public async Task O_aviso_os_trechos_e_o_resultado_um_passo_por_tela()
    {
        await Pronto();
        var candidato = _analise.TetoEmUso().Master!.Value;
        Assert.Null(_vm.MotivoTrava);
        SoNo(nameof(_vm.Fechado));

        await _vm.Abrir.ExecutarAsync();
        await _b.Tela.EsvaziarAsync();
        Assert.True(_vm.Aberto);
        Assert.Equal(EtapaTesteSeco.Aviso, _vm.Etapa);
        SoNo(nameof(_vm.NoAviso));
        Assert.Equal("O módulo vai ser desligado.", _vm.Instrucao);
        Assert.NotNull(_vm.Detalhe);
        Assert.True(_vm.PodeConfirmar);
        Assert.True(_vm.PodeCancelar);
        Assert.Equal(3, _vm.TotalTrechos);
        SemIa(_vm);

        // O 2º trecho fica tocando: a faixa, o trecho e o progresso.
        _reprodutor.Segurar = 2;
        var confirmar = _vm.Confirmar.ExecutarAsync();
        await AndarAte(() => _reprodutor.Segurando);
        Assert.Equal(EtapaTesteSeco.Tocando, _vm.Etapa);
        SoNo(nameof(_vm.Andando));
        Assert.Equal("Faixa 2 de 3: menos 6, trecho 1", _vm.TextoTrecho);
        Assert.Equal("Faixa 2 de 3: menos 6, trecho 1", _vm.Instrucao);
        Assert.Equal("Trecho 2 de 3", _vm.TextoDoProgresso);
        Assert.Equal(1 / 3.0, _vm.Progresso, 6);
        Assert.Equal($"{LimiteFisico.Db(candidato)} dB", _vm.TextoMaster);
        Assert.False(_vm.PodeConfirmar);
        Assert.True(_vm.PodeCancelar);
        SemIa(_vm);

        // O reprodutor para sozinho no meio do trecho: ele toca de novo, e o teste chega ao teto validado.
        _reprodutor.Segurar = null;
        await _reprodutor.PararAsync();
        await AndarAte(() => confirmar.IsCompleted);
        await confirmar;
        Assert.Equal(EtapaTesteSeco.Resultado, _vm.Etapa);
        SoNo(nameof(_vm.NoResultado));
        Assert.False(_vm.TemClips);
        Assert.True(_vm.Validado);
        Assert.Equal($"Teto validado: {LimiteFisico.Db(candidato)} dB.", _vm.Instrucao);
        Assert.Equal(_vm.Instrucao, _vm.Mensagem);
        Assert.Null(_vm.MensagemNaTela);
        Assert.Equal(1.0, _vm.Progresso, 6);
        Assert.Empty(_vm.LinhasClips);
        Assert.Null(_vm.Detalhe);
        Assert.True(_vm.PodeConcluir);
        SemIa(_vm);

        // A folha do nível: com o teste a seco e o modelo térmico de todo alto-falante (13f), o Extra se acende.
        await _b.Tela.EsvaziarAsync();
        var extra = _nivelVm.Linhas.Single(l => l.Nome == "Extra");
        Assert.True(extra.Pode, extra.Motivo);
        Assert.Null(extra.Motivo);
        Assert.Null(extra.Selo);
        Assert.Matches(@"^Extra · até \d+(,\d)? V nas JBL L e JBL R · só a playlist$", extra.Texto);
        Assert.Null(_nivelVm.SeloRevalidar);
    }

    [Fact]
    public async Task Cancelar_tocando_volta_o_master_e_fecha_com_a_frase()
    {
        await Pronto();
        await _vm.Abrir.ExecutarAsync();
        _reprodutor.Segurar = 1;
        var confirmar = _vm.Confirmar.ExecutarAsync();
        await AndarAte(() => _reprodutor.Segurando);

        var cancelar = _vm.Cancelar.ExecutarAsync();
        await AndarAte(() => cancelar.IsCompleted && confirmar.IsCompleted);
        await cancelar;
        await confirmar;
        await _b.Tela.EsvaziarAsync();

        Assert.False(_vm.Aberto);
        Assert.Equal(TesteSeco.MsgCancelado, _vm.Mensagem);
        Assert.Equal("+0.0", _b.Simulador.Valor("080"));
        Assert.Equal("0", _b.Simulador.Valor("252"));
        SemIa(_vm);
    }

    [Fact]
    public async Task O_selo_revalidar_vem_com_o_motivo()
    {
        await Pronto();
        await _vm.Abrir.ExecutarAsync();
        var confirmar = _vm.Confirmar.ExecutarAsync();
        await AndarAte(() => confirmar.IsCompleted);
        await confirmar;
        Assert.True(_vm.Validado);

        // A ficha da JBL L muda: a memória vai para "revalidar".
        var jbl = _b.Setups!.SetupEmUso!.Equipamentos.Single(e => e.Id == CasaDoDenis.IdJblL);
        var editar = _b.Setups.EditarEquipamentoAsync(CasaDoDenis.IdSetupCasa,
            jbl with { Dados = jbl.Dados with { Fs = new DadoTecnico(99m, OrigemDado.Oficial, "teste") } });
        await AndarAte(() => editar.IsCompleted);
        Assert.True((await editar).Ok);
        await AndarAte(() => _nivelVm.SeloRevalidar is not null);

        Assert.Equal("revalidar", _nivelVm.SeloRevalidar);
        Assert.Equal("A ficha ou a calibração mudou.", _nivelVm.MotivoRevalidar);
        SemIa(_nivelVm);
    }

    [Fact]
    public async Task O_Player_mostra_a_marca_do_clip_a_folha_do_outro_player_e_o_aviso_do_volume_uma_vez()
    {
        // História 13g: a casca com este Player (o aviso do volume no aviso rápido; a folha do outro player como camada).
        using var telas = new Telas11(_b);
        using var casca = new CascaViewModel(_b.Sessao, telas.Conexao, telas.Setup, telas.Memorias, telas.Inicio, telas.Mesa, telas.Audio,
            telas.Salvar, _b.Fabrica, telas.Nav, _b.Tela, _b.Relogio, _playerVm);
        await Pronto();
        await _vm.Abrir.ExecutarAsync();
        var confirmar = _vm.Confirmar.ExecutarAsync();
        await AndarAte(() => confirmar.IsCompleted);
        await confirmar;
        Assert.True(_vm.Validado);
        var extra = _b.Setups!.DefinirNivelAsync(IdPlana, NivelRendimento.Extra);
        await AndarAte(() => extra.IsCompleted);
        Assert.True((await extra).Ok);

        // Tocando no Extra: um clip marca a linha da faixa.
        var tocar = _playerVm.Linha("menos 6")!.Tocar.ExecutarAsync();
        await AndarAte(() => tocar.IsCompleted && _player.Estado == EstadoPlayer.Tocando);
        _b.Simulador.EmitirClip(1);
        await AndarAte(() => _playerVm.Linha("menos 6")!.TextoMarca is not null);
        Assert.Equal("Clip na JBL L", _playerVm.Linha("menos 6")!.TextoMarca);
        Assert.Null(_playerVm.Linha("menos 3")!.TextoMarca);

        // O aviso do volume aparece uma vez.
        _volume.Mudar(5);
        await AndarAte(() => _playerVm.Mensagem == FiscalExtra.MsgVolume && _volume.Volume == 15);
        Assert.Null(_fiscal.Aviso);
        // História 13g: o aviso do volume chega ao aviso rápido da casca e some depois de 2,6 s.
        Assert.Equal(FiscalExtra.MsgVolume, casca.AvisoRapido);
        await _b.Andar(TimeSpan.FromSeconds(2.7), passoMs: 100);
        Assert.Null(casca.AvisoRapido);

        // Outro app começa a tocar: a folha; o Voltar ao player a fecha.
        _outro.Disparar();
        await AndarAte(() => _playerVm.FolhaOutroPlayer && _player.Estado == EstadoPlayer.Pausado);
        Assert.True(casca.FolhaOutroPlayerPorCima);
        Assert.Equal((false, true, false), telas.Nav.Folhas);
        Assert.Equal("Outro app começou a tocar. O som foi mutado.", _playerVm.TextoOutroPlayer);
        Assert.Equal("Voltar ao player do app", _playerVm.TextoVoltar);
        await _playerVm.VoltarAoPlayer.ExecutarAsync();
        await _b.Tela.EsvaziarAsync();
        Assert.False(_playerVm.FolhaOutroPlayer);
        Assert.Equal((false, false, false), telas.Nav.Folhas);
        SemIa(_playerVm);
    }
}
