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
/// História 13d nas telas (sem XAML), contra o processador simulado e o dublê do decodificador: o "Otimizar configuração
/// na playlist" (o anel, o resultado, o cancelar e o motivo), o peso e o "Em análise" em cada linha do Player, a faixa em
/// análise que não toca, e a linha do Extra na folha do nível. O Casa 2.1 calibrado, com o Bluetooth em −13 dB, e a
/// memória PLANA em uso (o ganho +15 nas JBL, LR24 em 200 Hz, lembrando o preset Plano). Nenhum texto com "IA".
/// </summary>
public sealed class OtimizarViewModelTestes : IDisposable
{
    private static readonly Regex PalavraIa = new(@"\bIA\b", RegexOptions.CultureInvariant);
    private const string IdPlana = "memoria-plana";
    private const string IdPlano = "preset-plano";

    private readonly Bancada _b;
    private readonly DecodificadorSintetico _decodificador = new();
    private readonly ReprodutorFalso _reprodutor = new();
    private readonly AnalisePlaylist _analise;
    private readonly Player _player;
    private readonly OtimizarViewModel _vm;
    private readonly PlayerViewModel _playerVm;
    private readonly NivelViewModel _nivelVm;

    public OtimizarViewModelTestes() : this(CasaDoDenis.SetupCasaComBluetooth)
    {
    }

    private OtimizarViewModelTestes(Setup setup)
    {
        var plana = Plana();
        var regulagem = plana.Where(v => Camadas.De(v.Codigo) == Camada.Regulagem).ToArray();
        var plano = new Preset(IdPlano, "Plano", CasaDoDenis.QuandoFilme, GerentePresets.BandasPlanas);
        var memoria = new MemoriaCelular(IdPlana, CasaDoDenis.IdSetupCasa, "PLANA", CasaDoDenis.QuandoFilme, regulagem, IdPlano);
        var dados = new DadosSetups([setup], [memoria], CasaDoDenis.IdSetupCasa,
            new Dictionary<string, UsoDoSetup> { [CasaDoDenis.IdSetupCasa] = new([IdPlana], [IdPlana, null, null]) });
        var arquivos = new ArquivosFalso();
        arquivos.Semear(RepositorioTextoArquivos.ArquivoSetups, GerenteSetups.ParaJson(dados));
        arquivos.Semear(RepositorioTextoArquivos.ArquivoPresets, GerentePresets.ParaJson([plano]));
        _b = new Bancada(arquivos, setups: true);
        // O aparelho já está na PLANA com o Plano: a conferência confere.
        foreach (var v in plana)
            _b.Simulador.Definir(v.Codigo, v.Indice, v.NoFio);
        _analise = new AnalisePlaylist(_b.Setups!, _b.Presets, _b.Playlists, _decodificador,
            new RepositorioTextoArquivos(arquivos, RepositorioTextoArquivos.ArquivoAnalises), _b.Log);
        _player = new Player(_b.Playlists, _b.Setups!, _reprodutor, _b.Log) { EmAnalise = _analise.EmAnalise };
        _vm = new OtimizarViewModel(_analise, _b.Setups!, _player, _b.Tela, _b.Relogio);
        _playerVm = new PlayerViewModel(_player, _b.Playlists, _b.Setups!, new SeletorFalso(), _b.Tela, _b.Relogio, analise: _analise);
        _nivelVm = new NivelViewModel(_b.Setups!, _b.Estado, _b.Tela, _b.Relogio, _analise);
    }

    public void Dispose()
    {
        _vm.Dispose();
        _playerVm.Dispose();
        _nivelVm.Dispose();
        _player.Dispose();
        _analise.Dispose();
        _b.ConferirSoAudio();
        // A análise não muda o som: nada além da senha e das leituras sai para o aparelho.
        Assert.DoesNotContain(_b.Simulador.Escritas, e => Catalogo.EscritaDeAudio(e) is not null);
        Assert.Empty(_b.Tela.Erros);
    }

    /// <summary>A leitura de 18/10 com o ganho +15 nas JBL e −15 no Sub, os EQ e as bandas em 0, LR24 em 200 Hz nas JBL e o master em 0.</summary>
    private static IReadOnlyList<ValorCopiado> Plana() =>
        CasaDoDenis.LeituraFilme.Select(v => (v.Codigo, v.Indice) switch
        {
            ("081", 0 or 1) => v with { NoFio = "+15.0" },
            ("081", _) => v with { NoFio = "-15.0" },
            ("091", _) or ("030", _) or ("020", _) or ("080", _) => v with { NoFio = "+0.0" },
            ("042", 0 or 1) => v with { NoFio = "3" },
            ("040", 0 or 1) => v with { NoFio = "200" },
            _ => v,
        }).ToArray();

    private async Task Pronto()
    {
        await _b.Conectar();
        await _analise.Carregada;
        Assert.Equal(IdPlana, _b.Setups!.MemoriaEmUso?.Id);
        await Apoio.Esperar(() => _player.Memoria == IdPlana);
        await _b.Tela.EsvaziarAsync();
    }

    /// <summary>Senos de 1 kHz com os picos (dBFS) na playlist da PLANA ("menos 3", "menos 6").</summary>
    private async Task Adicionar(params (string Uri, double Pico)[] faixas)
    {
        foreach (var (uri, pico) in faixas)
            _decodificador.Seno(uri, pico);
        var r = await _b.Playlists.AdicionarAsync(IdPlana,
            faixas.Select(f => new ArquivoAudio(f.Uri, $"menos {Math.Abs(f.Pico):0}", "Testes", TimeSpan.FromSeconds(2))));
        Assert.True(r.Ok, r.Mensagem);
        await _b.Tela.EsvaziarAsync();
    }

    private async Task Esperar(Func<bool> condicao)
    {
        await Apoio.Esperar(condicao);
        await _b.Tela.EsvaziarAsync();
    }

    private LinhaPlaylistViewModel Linha(string nome) => _playerVm.Linha(nome)!;

    private string MotivoExtra => _nivelVm.Linhas.Single(l => l.Nivel is null).Motivo!;

    [Fact]
    public async Task Otimizar_mostra_o_anel_e_depois_o_teto_do_Extra_e_o_peso_de_cada_faixa()
    {
        await Pronto();
        await Adicionar(("seno-3", -3), ("seno-6", -6));
        _decodificador.Segurar("seno-6");

        Assert.True(_vm.TemMemoria);
        Assert.False(_vm.Otimizada);
        Assert.Equal("Otimizar configuração na playlist", _vm.TextoOtimizar);
        Assert.Equal("Falta analisar a playlist.", MotivoExtra);

        await _vm.Otimizar.ExecutarAsync();
        await Esperar(() => _decodificador.Lidas.Contains("seno-6"));

        // Durante a análise: o anel, o cancelar, a faixa "Em análise" e a linha do Extra.
        Assert.True(_vm.Otimizada);
        Assert.True(_vm.Analisando);
        Assert.True(_vm.PodeCancelar);
        Assert.Null(_vm.Resultado);
        Assert.Null(_vm.Mensagem);
        Assert.True(Linha("menos 6").EmAnalise);
        Assert.Equal("Em análise", Linha("menos 6").TextoAnalise);
        Assert.Equal("Analisando a playlist.", MotivoExtra);
        Assert.False(_nivelVm.Linhas.Single(l => l.Nivel is null).Pode);

        // A tela e o player seguem livres: tocar a faixa em análise só avisa.
        await _playerVm.TocarFaixaAsync(Linha("menos 6").Id);
        await _b.Tela.EsvaziarAsync();
        Assert.Equal("Esta faixa ainda está em análise.", _playerVm.Mensagem);
        Assert.Null(_reprodutor.Tocando);

        _decodificador.Soltar("seno-6");
        await Esperar(() => !_vm.Analisando && !_analise.Ocupada);

        var master = _analise.Teto(IdPlana, IdPlano).Master!.Value;
        Assert.Equal($"Teto do Extra: {LimiteFisico.Db(master)} dB. Falta o teste a seco.", _vm.Resultado);
        Assert.InRange(master, -5.1m, -4.6m);
        Assert.False(_vm.PodeCancelar);
        Assert.False(Linha("menos 6").EmAnalise);
        Assert.Equal("−3,0 dB no teto", Linha("menos 3").TextoAnalise);
        Assert.Null(Linha("menos 6").TextoAnalise);
        Assert.Equal("Falta o teste a seco.", MotivoExtra);
        Assert.False(_nivelVm.Linhas.Single(l => l.Nivel is null).Pode);
        SemIa();
    }

    [Fact]
    public async Task Cancelar_desotimiza_para_a_analise_e_nada_e_gravado()
    {
        await Pronto();
        await Adicionar(("seno-3", -3));
        _decodificador.Segurar("seno-3");
        await _vm.Otimizar.ExecutarAsync();
        await Esperar(() => _vm.Analisando && _decodificador.Lidas.Contains("seno-3"));

        await _vm.Cancelar.ExecutarAsync();
        await Esperar(() => !_analise.Ocupada);

        Assert.False(_vm.Otimizada);
        Assert.False(_vm.Analisando);
        Assert.Null(_vm.Resultado);
        Assert.Null(_b.Arquivos.Conteudo(RepositorioTextoArquivos.ArquivoAnalises));
        Assert.Null(Linha("menos 3").TextoAnalise);
        Assert.Equal("Falta analisar a playlist.", MotivoExtra);
        SemIa();
    }

    [Fact]
    public async Task Sem_calibracao_o_Otimizar_mostra_o_motivo_e_nada_e_analisado()
    {
        using var t = new OtimizarViewModelTestes(CasaDoDenis.SetupCasaComBluetooth with { Equipamentos = CasaDoDenis.EquipamentosSemCalibracao });
        await t.Pronto();
        await t.Adicionar(("seno-3", -3));

        await t._vm.Otimizar.ExecutarAsync();
        await t._b.Tela.EsvaziarAsync();

        Assert.StartsWith("Calibre a saída ", t._vm.Mensagem);
        Assert.False(t._vm.Otimizada);
        Assert.False(t._vm.Analisando);
        Assert.Empty(t._decodificador.Lidas);
        t.SemIa();
    }

    /// <summary>Nenhum texto público das telas da 13d tem a palavra "IA".</summary>
    private void SemIa()
    {
        object[] telas = [_vm, _playerVm, _nivelVm, .. _playerVm.Linhas];
        foreach (var tela in telas)
        {
            foreach (var p in tela.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.PropertyType == typeof(string)))
            {
                var texto = (string?)p.GetValue(tela);
                Assert.False(texto is not null && PalavraIa.IsMatch(texto), $"{tela.GetType().Name}.{p.Name} mostra a IA: \"{texto}\"");
            }
        }
        foreach (var l in _nivelVm.Linhas)
            Assert.False(l.Motivo is not null && PalavraIa.IsMatch(l.Motivo), l.Motivo);
    }
}
