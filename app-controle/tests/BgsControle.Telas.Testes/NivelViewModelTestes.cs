using System.Reflection;
using System.Text.RegularExpressions;
using BgsControle.Nucleo;
using BgsControle.Nucleo.Escrita;
using BgsControle.Nucleo.Presets;
using BgsControle.Nucleo.Rendimento;
using BgsControle.Nucleo.Setups;
using BgsControle.Simulador;

namespace BgsControle.Telas.Testes;

/// <summary>
/// História 13c, a folha "Nível da memória" (sem XAML) contra o processador simulado: o Casa 2.1 calibrado com o
/// Bluetooth em −13 dB e a memória PLANA em uso (o ganho +9 nas JBL, LR24 em 200 Hz). As linhas, o Extra apagado,
/// a folha de risco do Máximo, o "Sem nível", as linhas apagadas sem calibração ou sem o Bluetooth, e nenhum "IA".
/// </summary>
public sealed class NivelViewModelTestes : IDisposable
{
    private static readonly Regex PalavraIa = new(@"\bIA\b", RegexOptions.CultureInvariant);
    private const string IdPlana = "memoria-plana";

    private readonly Bancada _b;
    private readonly NivelViewModel _vm;

    public NivelViewModelTestes() : this(CasaDoDenis.SetupCasaComBluetooth)
    {
    }

    private NivelViewModelTestes(Setup setup)
    {
        var plana = Plana();
        var regulagem = plana.Where(v => Camadas.De(v.Codigo) == Camada.Regulagem).ToArray();
        var memoria = new MemoriaCelular(IdPlana, CasaDoDenis.IdSetupCasa, "PLANA", CasaDoDenis.QuandoFilme, regulagem);
        var dados = new DadosSetups([setup], [memoria], CasaDoDenis.IdSetupCasa,
            new Dictionary<string, UsoDoSetup> { [CasaDoDenis.IdSetupCasa] = new([IdPlana], [IdPlana, null, null]) });
        var arquivos = new ArquivosFalso();
        arquivos.Semear(RepositorioTextoArquivos.ArquivoSetups, GerenteSetups.ParaJson(dados));
        _b = new Bancada(arquivos, setups: true);
        // O aparelho já está na PLANA: a conferência confere.
        foreach (var v in plana)
            _b.Simulador.Definir(v.Codigo, v.Indice, v.NoFio);
        _vm = new NivelViewModel(_b.Setups!, _b.Estado, _b.Tela, _b.Relogio);
    }

    public void Dispose()
    {
        _vm.Dispose();
        _b.ConferirSoAudio();
        Assert.Empty(_b.Tela.Erros);
    }

    /// <summary>A leitura de 18/10 com o ganho +9 nas JBL e −15 no Sub, os EQ e as bandas em 0, LR24 em 200 Hz nas JBL e o master em 0.</summary>
    private static IReadOnlyList<ValorCopiado> Plana() =>
        CasaDoDenis.LeituraFilme.Select(v => (v.Codigo, v.Indice) switch
        {
            ("081", 0 or 1) => v with { NoFio = "+9.0" },
            ("081", _) => v with { NoFio = "-15.0" },
            ("091", _) or ("030", _) or ("020", _) or ("080", _) => v with { NoFio = "+0.0" },
            ("042", 0 or 1) => v with { NoFio = "3" },
            ("040", 0 or 1) => v with { NoFio = "200" },
            _ => v,
        }).ToArray();

    private async Task Pronto()
    {
        await _b.Conectar();
        Assert.Equal(IdPlana, _b.Setups!.MemoriaEmUso?.Id);
        await _vm.Abrir.ExecutarAsync();
        await _b.Tela.EsvaziarAsync();
    }

    private LinhaNivel Linha(NivelRendimento nivel) => _vm.Linhas.Single(l => l.Nivel == nivel);

    [Fact]
    public async Task As_linhas_vao_do_Baixo_ao_Maximo_com_o_teto_em_volts_e_o_Extra_vem_apagado()
    {
        await Pronto();

        Assert.True(_vm.FolhaAberta);
        Assert.Equal("Nível da memória", _vm.Titulo);
        Assert.Equal(["Baixo", "Médio", "Alto", "Máximo", "Extra"], _vm.Linhas.Select(l => l.Nome));
        Assert.Equal("Alto · até 16 V nas JBL L e JBL R · qualquer fonte", Linha(NivelRendimento.Alto).Texto);
        Assert.Equal("Baixo · até 9,0 V nas JBL L e JBL R · qualquer fonte", Linha(NivelRendimento.Baixo).Texto);
        Assert.Equal("Médio · até 13 V nas JBL L e JBL R · qualquer fonte", Linha(NivelRendimento.Medio).Texto);
        var maximo = Linha(NivelRendimento.Maximo);
        Assert.Matches(@"^Máximo · até 2\d V nas JBL L e JBL R · qualquer fonte$", maximo.Texto);
        Assert.Equal(Niveis.MsgMaximo, maximo.AvisoCalor);
        Assert.All(_vm.Linhas.Take(4), l => Assert.True(l.Pode, l.Motivo));
        Assert.All(_vm.Linhas.Take(3), l => Assert.Null(l.AvisoCalor));
        var extra = _vm.Linhas[4];
        Assert.Null(extra.Nivel);
        Assert.False(extra.Pode);
        Assert.Equal("Falta analisar a playlist.", extra.Motivo);
        Assert.True(_vm.SemNivelEscolhido);
        Assert.Null(_vm.NivelEscolhido);
    }

    [Fact]
    public async Task Escolher_o_Alto_grava_o_nivel_e_poe_o_master_da_conta()
    {
        await Pronto();

        await _b.Rodar(_vm.Escolher.ExecutarAsync(NivelRendimento.Alto));

        Assert.Equal(NivelRendimento.Alto, _b.Setups!.NivelEmUso);
        Assert.Equal("Nível Alto na PLANA.", _vm.Mensagem);
        Assert.Equal("-3.8", _b.Simulador.Valor("080"));
        Assert.True(Linha(NivelRendimento.Alto).Escolhida);
        Assert.False(_vm.SemNivelEscolhido);
        Assert.False(_vm.RiscoAberto);
    }

    [Fact]
    public async Task O_Maximo_pede_a_folha_de_risco_Cancelar_deixa_igual_e_Confirmar_escolhe()
    {
        await Pronto();
        var master = _b.Simulador.Valor("080");

        await _b.Rodar(_vm.Escolher.ExecutarAsync(NivelRendimento.Maximo));

        Assert.True(_vm.RiscoAberto);
        Assert.Equal("Usar o nível Máximo?", _vm.TituloDoRisco);
        Assert.Equal("Passa da potência da ficha. Seguro com música; um tom contínuo ou música muito comprimida por muito tempo podem aquecer a bobina.",
            _vm.Risco);
        Assert.Equal("Confirmar", _vm.TextoConfirmar);
        Assert.Equal("Cancelar", _vm.TextoCancelar);
        Assert.Null(_b.Setups!.NivelEmUso);

        await _vm.Cancelar.ExecutarAsync();

        Assert.False(_vm.RiscoAberto);
        Assert.Null(_b.Setups.NivelEmUso);
        Assert.Equal(master, _b.Simulador.Valor("080"));

        await _b.Rodar(_vm.Escolher.ExecutarAsync(NivelRendimento.Maximo));
        await _b.Rodar(_vm.Confirmar.ExecutarAsync());

        Assert.False(_vm.RiscoAberto);
        Assert.Equal(NivelRendimento.Maximo, _b.Setups.NivelEmUso);
        Assert.Equal("Nível Máximo na PLANA.", _vm.Mensagem);
        Assert.Equal(_b.Setups.Conta(NivelRendimento.Maximo)!.Master,
            decimal.Parse(_b.Simulador.Valor("080")!, System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task Sem_nivel_volta_a_null_sem_mexer_no_master()
    {
        await Pronto();
        await _b.Rodar(_vm.Escolher.ExecutarAsync(NivelRendimento.Baixo));
        Assert.Equal("-8.8", _b.Simulador.Valor("080"));
        var inicio = _b.Simulador.Escritas.Count;

        await _b.Rodar(_vm.SemNivel.ExecutarAsync());

        Assert.Null(_b.Setups!.NivelEmUso);
        Assert.True(_vm.SemNivelEscolhido);
        Assert.Equal("A PLANA ficou sem nível.", _vm.Mensagem);
        Assert.Equal("-8.8", _b.Simulador.Valor("080"));
        Assert.DoesNotContain(_b.Simulador.Escritas.Skip(inicio), e => Catalogo.EscritaDeAudio(e) is not null);
    }

    [Fact]
    public async Task Sem_calibracao_as_linhas_ficam_apagadas_com_o_motivo_e_nada_sai()
    {
        using var t = new NivelViewModelTestes(CasaDoDenis.SetupCasaComBluetooth with { Equipamentos = CasaDoDenis.EquipamentosSemCalibracao });
        await t.Pronto();
        var inicio = t._b.Simulador.Escritas.Count;

        Assert.All(t._vm.Linhas.Take(4), l =>
        {
            Assert.False(l.Pode);
            Assert.Equal("Calibre a saída JBL L.", l.Motivo);
        });
        await t._b.Rodar(t._vm.Escolher.ExecutarAsync(NivelRendimento.Baixo));

        Assert.Equal("Calibre a saída JBL L.", t._vm.Mensagem);
        Assert.Null(t._b.Setups!.NivelEmUso);
        Assert.DoesNotContain(t._b.Simulador.Escritas.Skip(inicio), e => Catalogo.EscritaDeAudio(e) is not null);
    }

    [Fact]
    public async Task Sem_o_Bluetooth_medido_as_linhas_ficam_apagadas_com_o_motivo()
    {
        using var t = new NivelViewModelTestes(CasaDoDenis.SetupCasa);
        await t.Pronto();

        Assert.All(t._vm.Linhas.Take(4), l =>
        {
            Assert.False(l.Pode);
            Assert.Equal("Meça o nível do Bluetooth.", l.Motivo);
            Assert.EndsWith("· qualquer fonte", l.Texto);
        });
    }

    [Fact]
    public async Task Medir_o_Bluetooth_com_a_folha_aberta_acende_as_linhas()
    {
        using var t = new NivelViewModelTestes(CasaDoDenis.SetupCasa);
        await t.Pronto();
        Assert.All(t._vm.Linhas.Take(4), l => Assert.Equal("Meça o nível do Bluetooth.", l.Motivo));

        var r = await t._b.Setups!.DefinirNivelBluetoothAsync(CasaDoDenis.IdSetupCasa, CasaDoDenis.NivelBluetoothCasa);
        Assert.True(r.Ok, r.Motivo);
        await t._b.Tela.EsvaziarAsync();

        Assert.True(t._vm.FolhaAberta);
        Assert.All(t._vm.Linhas.Take(4), l => Assert.True(l.Pode, l.Motivo));
        Assert.Equal("Alto · até 16 V nas JBL L e JBL R · qualquer fonte", t.Linha(NivelRendimento.Alto).Texto);
    }

    [Fact]
    public async Task Nenhum_texto_da_folha_contem_IA()
    {
        await Pronto();
        await _b.Rodar(_vm.Escolher.ExecutarAsync(NivelRendimento.Maximo));

        foreach (var p in typeof(NivelViewModel).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                     .Where(p => p.PropertyType == typeof(string)))
        {
            var texto = p.GetValue(_vm) as string;
            Assert.False(texto is not null && PalavraIa.IsMatch(texto), $"{p.Name}: {texto}");
        }
        foreach (var l in _vm.Linhas)
        {
            foreach (var texto in new[] { l.Nome, l.Texto, l.Motivo, l.AvisoCalor, l.AvisoGanho })
                Assert.False(texto is not null && PalavraIa.IsMatch(texto), texto);
        }
    }
}
