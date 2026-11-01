using System.Reflection;
using System.Text.RegularExpressions;
using BgsControle.Nucleo.Escrita;
using BgsControle.Nucleo.Player;
using BgsControle.Nucleo.Presets;
using BgsControle.Nucleo.Rendimento;
using BgsControle.Nucleo.Setups;
using BgsControle.Simulador;

namespace BgsControle.Telas.Testes;

/// <summary>
/// História 13f nas telas (sem XAML), contra o processador simulado: a linha do Extra na folha do nível (acesa com o teste
/// a seco e o modelo térmico, o teto, o selo "estimado", o motivo e o escolher) e o Extra ao vivo (o tempo de Extra, o
/// "Descansando" e o Bluetooth travado). A PLANA em uso no Máximo, como no teste a seco da 13e. Nenhum texto com "IA".
/// </summary>
public sealed class ExtraAoVivoViewModelTestes : IDisposable
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
    private readonly TermicoExtra _termico;
    private readonly NivelViewModel _nivelVm;
    private readonly ExtraAoVivoViewModel _vm;

    public ExtraAoVivoViewModelTestes() : this(Rapido())
    {
    }

    /// <summary>As JBL esquentam rápido (150 °C, 2 + 1,5 K/W, τ 2 s e 6 s): o corte vem em segundos.</summary>
    private static Setup Rapido(OrigemDado origem = OrigemDado.Oficial) => CasaDoDenis.ComAltoFalantes(CasaDoDenis.SetupCasaComTermico,
        e => e.Id == CasaDoDenis.IdSubCasa ? e.Dados : CasaDoDenis.ComTermico(e.Dados, 150m, 2m, 1.5m, 2m, 6m, origem));

    private ExtraAoVivoViewModelTestes(Setup setup)
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
        var dados = new DadosSetups([setup], [memoria], CasaDoDenis.IdSetupCasa,
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
            _b.Chaves, _b.Log, _player, _b.Gerador, _b.Relogio, TimeSpan.FromMilliseconds(200));
        _termico = new TermicoExtra(_b.Sessao, _b.Estado, _b.Escritor, _b.Setups!, _analise, _player, _b.Comandos, _b.Log, _b.Relogio,
            new RepositorioTextoArquivos(arquivos, RepositorioTextoArquivos.ArquivoTermico));
        _teste.Impedimento = () => _termico.TextoDescanso;
        _nivelVm = new NivelViewModel(_b.Setups!, _b.Estado, _b.Tela, _b.Relogio, _analise);
        _vm = new ExtraAoVivoViewModel(_termico, _b.Tela, _b.Relogio);
    }

    public void Dispose()
    {
        _vm.Dispose();
        _nivelVm.Dispose();
        _termico.Dispose();
        _teste.Dispose();
        _player.Dispose();
        _analise.Dispose();
        Assert.Empty(_b.Tela.Erros);
    }

    // ----------------------------------------------------------------- apoio

    private async Task AndarAte(Func<bool> condicao, int passos = 3000, int ms = 10)
    {
        for (var i = 0; i < passos && !condicao(); i++)
        {
            _b.Relogio.Avancar(TimeSpan.FromMilliseconds(ms));
            await Task.Delay(1);
        }
        Assert.True(condicao(), "a condição não ficou verdadeira");
        await _b.Tela.EsvaziarAsync();
    }

    /// <summary>Conecta, põe 3 senos (−3, −6 e −10 dBFS), otimiza a PLANA e faz o teste a seco, que passa.</summary>
    private async Task Validado()
    {
        await _b.Conectar();
        await _analise.Carregada;
        await AndarAte(() => _termico.Carregado.IsCompleted);
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
        Assert.True(_teste.Abrir().Ok);
        var confirmar = _teste.ConfirmarAsync();
        await AndarAte(() => confirmar.IsCompleted);
        Assert.True((await confirmar).Ok);
        await AndarAte(() => !_b.Copia.EmAndamento && !_b.Comandos.Ocupado);
    }

    private LinhaNivel Extra => _nivelVm.Linhas.Single(l => l.Nome == "Extra");

    private static void SemIa(object vm)
    {
        foreach (var p in vm.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.PropertyType == typeof(string)))
        {
            var texto = p.GetValue(vm) as string;
            Assert.False(texto is not null && PalavraIa.IsMatch(texto), $"{p.Name}: {texto}");
        }
    }

    // ----------------------------------------------------------------- a linha do Extra

    [Fact]
    public async Task Com_o_teste_a_seco_e_o_termico_a_linha_do_Extra_se_acende_com_o_teto_e_se_escolhe()
    {
        await Validado();
        await _nivelVm.Abrir.ExecutarAsync();
        await _b.Tela.EsvaziarAsync();

        Assert.True(Extra.Pode, Extra.Motivo);
        Assert.Null(Extra.Motivo);
        Assert.Null(Extra.Selo);
        Assert.Null(Extra.Nivel);
        Assert.Matches(@"^Extra · até \d+(,\d)? V nas? .+ · só a playlist$", Extra.Texto);
        Assert.False(Extra.Escolhida);

        await _b.Rodar(_nivelVm.EscolherExtra.ExecutarAsync());
        await _b.Tela.EsvaziarAsync();

        Assert.Equal(NivelRendimento.Extra, _b.Setups!.NivelEmUso);
        Assert.Equal("Nível Extra na PLANA.", _nivelVm.Mensagem);
        Assert.True(Extra.Escolhida);
        foreach (var texto in new[] { Extra.Nome, Extra.Texto, Extra.Motivo, Extra.Selo })
            Assert.False(texto is not null && PalavraIa.IsMatch(texto), texto);
        SemIa(_nivelVm);
    }

    [Fact]
    public async Task Sem_o_termico_a_linha_fica_apagada_com_o_nome_do_alto_falante_e_o_escolher_mostra_o_motivo()
    {
        using var t = new ExtraAoVivoViewModelTestes(CasaDoDenis.SetupCasaComBluetooth);
        await t.Validado();
        await t._nivelVm.Abrir.ExecutarAsync();
        await t._b.Tela.EsvaziarAsync();

        Assert.False(t.Extra.Pode);
        Assert.Equal("Falta o modelo térmico do JBL L.", t.Extra.Motivo);

        await t._b.Rodar(t._nivelVm.EscolherExtra.ExecutarAsync());
        await t._b.Tela.EsvaziarAsync();
        Assert.Equal("Falta o modelo térmico do JBL L.", t._nivelVm.Mensagem);
        Assert.Equal(NivelRendimento.Maximo, t._b.Setups!.NivelEmUso);
    }

    [Fact]
    public async Task Um_dado_termico_so_digitado_poe_o_selo_estimado_na_linha()
    {
        using var t = new ExtraAoVivoViewModelTestes(Rapido(OrigemDado.Digitado));
        await t.Validado();
        await t._nivelVm.Abrir.ExecutarAsync();
        await t._b.Tela.EsvaziarAsync();

        Assert.True(t.Extra.Pode, t.Extra.Motivo);
        Assert.Equal("estimado", t.Extra.Selo);
    }

    // ----------------------------------------------------------------- o Extra ao vivo

    [Fact]
    public async Task Tocando_no_Extra_mostra_o_tempo_e_no_corte_o_descanso_com_o_Bluetooth_travado()
    {
        await Validado();
        var extra = _b.Setups!.DefinirNivelAsync(IdPlana, NivelRendimento.Extra);
        await AndarAte(() => extra.IsCompleted);
        Assert.True((await extra).Ok);
        Assert.False(_vm.Visivel);

        var tocar = _player.TocarFaixaAsync(_player.Faixas[0].Id);
        await AndarAte(() => tocar.IsCompleted && _player.Estado == EstadoPlayer.Tocando);
        await AndarAte(() => _vm.TextoTempo is not null, ms: 100);
        Assert.Matches(@"^Extra: \d\d:\d\d$", _vm.TextoTempo!);
        Assert.True(_vm.Visivel);
        Assert.False(_vm.Descansando);
        Assert.Null(_vm.TextoDescanso);
        SemIa(_vm);

        // A bobina chega à máxima: o descanso, com o Bluetooth travado e o player pausado.
        await AndarAte(() => _vm.Descansando && _vm.BluetoothTravado && _player.Estado == EstadoPlayer.Pausado, ms: 100);
        Assert.StartsWith("Descansando: ", _vm.TextoDescanso);
        Assert.Matches(@"^Descansando: \d\d:\d\d$", _vm.TextoDescanso!);
        Assert.Equal("Bluetooth travado", _vm.TextoBluetooth);
        Assert.Null(_vm.TextoTempo);
        Assert.Null(_vm.Aviso);
        Assert.Equal("0", _b.Simulador.Valor("251"));
        SemIa(_vm);

        // No fim do descanso, o Bluetooth volta.
        await AndarAte(() => !_vm.Descansando && !_termico.TravaNoAparelho, passos: 1000, ms: 100);
        Assert.False(_vm.BluetoothTravado);
        Assert.Null(_vm.TextoBluetooth);
        Assert.Equal("1", _b.Simulador.Valor("251"));
    }
}
