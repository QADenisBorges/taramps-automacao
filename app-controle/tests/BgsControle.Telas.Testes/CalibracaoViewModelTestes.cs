using System.Reflection;
using System.Text.RegularExpressions;
using BgsControle.Nucleo;
using BgsControle.Nucleo.Gerador;
using BgsControle.Nucleo.Player;
using BgsControle.Nucleo.Rendimento;
using BgsControle.Nucleo.Seguranca;
using BgsControle.Nucleo.Setups;
using BgsControle.Simulador;

namespace BgsControle.Telas.Testes;

/// <summary>O tom do celular nos testes da tela: registra os pedidos.</summary>
internal sealed class TomCelularFalso : ITomCelular
{
    private int _pedidos;
    private int _tocando;

    public int Pedidos => Volatile.Read(ref _pedidos);

    public bool Tocando => Volatile.Read(ref _tocando) == 1;

    public Task<bool> TocarAsync(int hz, TimeSpan duracao)
    {
        Interlocked.Increment(ref _pedidos);
        Volatile.Write(ref _tocando, 1);
        return Task.FromResult(true);
    }

    public Task PararAsync()
    {
        Volatile.Write(ref _tocando, 0);
        return Task.CompletedTask;
    }
}

/// <summary>
/// História 13b, a calibração guiada pela tela (sem XAML) contra o processador simulado: o Casa 2.1 sem calibração,
/// o caminho inteiro da JBL L (com o Bluetooth) e do Sub, o aviso da tela e nenhum "IA".
/// </summary>
public sealed class CalibracaoViewModelTestes : IDisposable
{
    private static readonly Regex Senha = new(@"^\*000\*[0-9A-Z]{4}\*$");
    private static readonly Regex PalavraIa = new(@"\bIA\b", RegexOptions.CultureInvariant);
    private static readonly TimeSpan Prazo = TimeSpan.FromSeconds(30);

    private readonly Bancada _b;
    private readonly TomCelularFalso _tom = new();
    private readonly AudioDoCelularFalso _audio = new();
    private readonly Calibracao _calibracao;
    private readonly CalibracaoViewModel _vm;

    public CalibracaoViewModelTestes()
    {
        var arquivos = new ArquivosFalso();
        var dados = CenarioFilme.Dados with
        {
            Setups = [CasaDoDenis.SetupCasa with { Equipamentos = CasaDoDenis.EquipamentosSemCalibracao }],
        };
        arquivos.Semear(RepositorioTextoArquivos.ArquivoSetups, GerenteSetups.ParaJson(dados));
        _b = new Bancada(arquivos, setups: true);
        _calibracao = new Calibracao(_b.Sessao, _b.Estado, _b.Escritor, _b.Copia, _b.Setups!, _b.Gerador, _b.Chaves, _tom, _b.Log,
            relogio: _b.Relogio);
        _vm = new CalibracaoViewModel(_calibracao, _b.Gerador, _b.Setups!, _b.Sessao, _b.Chaves, _b.Estado, _b.Copia, _audio, _b.Tela, _b.Relogio);
    }

    /// <summary>Critério de aceite: só a senha, as leituras, o áudio e o gerador; cada <c>*202*1*</c> com o <c>*202*0*</c> em 30 s.</summary>
    public void Dispose()
    {
        _vm.Dispose();
        _calibracao.Dispose();
        Assert.DoesNotContain(_b.Simulador.Escritas, e => e.StartsWith("*24", StringComparison.Ordinal) || e.StartsWith("*23", StringComparison.Ordinal));
        Assert.All(_b.Simulador.Escritas, e =>
            Assert.True(Senha.IsMatch(e) || Catalogo.EhLeitura(e) || Catalogo.EscritaDeAudio(e) is not null
                        || Catalogo.ComandoDeGerador(e) is not null, e));
        var marcas = _b.Simulador.LinhaDoTempoGerador;
        for (var i = 0; i < marcas.Count; i++)
        {
            if (!marcas[i].Ligado)
                continue;
            var desligou = marcas.Skip(i + 1).FirstOrDefault(m => !m.Ligado);
            Assert.True(desligou is not null && desligou.Quando - marcas[i].Quando <= Prazo, "um *202*1* ficou sem *202*0* em 30 s");
        }
        Assert.Empty(_b.Tela.Erros);
    }

    private async Task Pronto()
    {
        await _b.Conectar();
        Assert.True((await _b.Chaves.DefinirAsync(Chave.Gerador, true, OrigemComando.Tela)).Ok);
        await _b.Tela.EsvaziarAsync();
    }

    private Equipamento Modulo => _b.Setups!.SetupEmUso!.Equipamentos.First(e => e.Id == CasaDoDenis.IdModuloCasa);

    /// <summary>Nenhuma propriedade de texto da tela mostra a palavra "IA".</summary>
    private void SemIa()
    {
        foreach (var p in typeof(CalibracaoViewModel).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                     .Where(p => p.PropertyType == typeof(string) && p.GetIndexParameters().Length == 0))
        {
            var texto = p.GetValue(_vm) as string;
            Assert.False(texto is not null && PalavraIa.IsMatch(texto), $"{p.Name} mostra a IA: \"{texto}\"");
        }
        foreach (var o in _vm.Saidas)
            Assert.False(PalavraIa.IsMatch(o.Nome + o.Situacao + o.Motivo), o.Nome);
    }

    /// <summary>Do passo 1 até o campo dos volts do tom.</summary>
    private async Task AteDigitarTom(int canal, string passo1)
    {
        await _vm.Escolher.ExecutarAsync(canal);
        await _b.Tela.EsvaziarAsync();
        Assert.Equal(EtapaCalibracao.Desligar, _vm.Etapa);
        Assert.Equal("Desligue os alto-falantes do módulo.", _vm.Instrucao);
        Assert.Equal(passo1, _vm.Passo);
        Assert.True(_vm.PodeCancelar);
        SemIa();

        await _vm.JaDesliguei.ExecutarAsync();
        await _b.Tela.EsvaziarAsync();
        Assert.Equal(EtapaCalibracao.Multimetro, _vm.Etapa);
        SemIa();

        await _b.Rodar(_vm.Seguir.ExecutarAsync());
        Assert.Equal(EtapaCalibracao.Tom, _vm.Etapa);
        Assert.True(_vm.Tocando);
        Assert.InRange(_vm.Contagem!.Value, 25, 30);
        Assert.EndsWith(" s", _vm.TextoContagem);
        Assert.False(_vm.PodeTocarDeNovo); // o gerador toca
        SemIa();

        await _vm.Digitar.ExecutarAsync();
        await _b.Tela.EsvaziarAsync();
        Assert.Equal(EtapaCalibracao.DigitarTom, _vm.Etapa);
        Assert.True(_vm.PedeVolts);
    }

    private async Task Confirmar(string volts)
    {
        _vm.TextoVolts = volts;
        await _b.Rodar(_vm.Confirmar.ExecutarAsync());
    }

    // ----------------------------------------------------------------- o caminho inteiro

    [Fact]
    public async Task O_caminho_inteiro_da_JBL_L_com_o_Bluetooth_e_depois_o_Sub()
    {
        await Pronto();
        Assert.Null(_vm.MotivoTrava);
        Assert.Equal(CalibracaoViewModel.TituloEscolha, _vm.Titulo);
        Assert.Equal(["JBL L", "JBL R", "Sub"], _vm.Saidas.Select(s => s.Nome));
        Assert.All(_vm.Saidas, s => Assert.Equal("Sem calibração", s.Situacao));
        Assert.All(_vm.Saidas, s => Assert.True(s.Pode));
        SemIa();

        // A JBL L: os 6 passos.
        await AteDigitarTom(0, "Passo 1 de 6");
        Assert.Equal("Calibrar JBL L", _vm.Titulo);
        Assert.Equal("Digite a tensão do tom de 360 Hz, em volts.", _vm.Instrucao);

        await Confirmar("11,0");
        Assert.Equal(EtapaCalibracao.Bluetooth, _vm.Etapa);
        Assert.Equal("Passo 5 de 6", _vm.Passo);
        Assert.Equal(Calibracao.MsgPedeBluetooth, _vm.Instrucao);
        Assert.Null(_vm.AvisoSaidaDoSom);
        Assert.Equal(OrigemDado.Medido, Modulo.Dados.Ganho!.Origem);
        Assert.Equal(EstadoGerador.Desligado, _b.Gerador.Estado);
        SemIa();

        await _b.Rodar(_vm.TocarTom.ExecutarAsync());
        Assert.Equal(EtapaCalibracao.DigitarBluetooth, _vm.Etapa);
        Assert.Equal("Passo 6 de 6", _vm.Passo);
        Assert.Equal("O celular toca 360 Hz. Digite a tensão lida, em volts.", _vm.Instrucao);
        Assert.Equal("", _vm.TextoVolts); // o campo do passo novo começa limpo
        Assert.True(_tom.Tocando);
        Assert.True(_vm.PodeTocarDeNovo);

        await Confirmar("8,73");
        Assert.Equal(EtapaCalibracao.Fim, _vm.Etapa);
        Assert.Equal("Ganho ×19,5 e Bluetooth −12,7 dB gravados.", _vm.Instrucao);
        Assert.Equal("Religue os alto-falantes.", _vm.Detalhe);
        Assert.False(_vm.PodeCancelar);
        Assert.False(_tom.Tocando);
        Assert.Equal(-12.7m, _b.Setups!.SetupEmUso!.NivelBluetooth!.Valor);
        SemIa();

        await _vm.Concluir.ExecutarAsync();
        await _b.Tela.EsvaziarAsync();
        Assert.Equal(EtapaCalibracao.Fechada, _vm.Etapa);
        Assert.Equal(["Calibrada", "Calibrada", "Sem calibração"], _vm.Saidas.Select(s => s.Situacao));

        // O Sub: o Bluetooth já está medido, 4 passos.
        await AteDigitarTom(2, "Passo 1 de 4");
        Assert.Equal("Calibrar Sub", _vm.Titulo);
        await Confirmar("22,8");
        Assert.Equal(EtapaCalibracao.Fim, _vm.Etapa);
        Assert.Equal("Ganho ×40,5 gravado.", _vm.Instrucao);
        Assert.Equal("40,5", Calibracao.Ganho(Modulo.Dados.GanhoPonte!.Valor));
        Assert.Equal(1, _tom.Pedidos);
        await _vm.Concluir.ExecutarAsync();
        await _b.Tela.EsvaziarAsync();
        Assert.All(_vm.Saidas, s => Assert.Equal("Calibrada", s.Situacao));
        Assert.Null(_b.Copia.Ultima!.Operacao);
    }

    [Fact]
    public async Task Medir_o_Bluetooth_de_novo_aparece_com_o_nivel_ja_medido_e_volta_aos_passos_5_e_6()
    {
        await Pronto();
        Assert.True((await _b.Setups!.DefinirNivelBluetoothAsync(CasaDoDenis.IdSetupCasa,
            new DadoTecnico(-13.0m, OrigemDado.Medido, "Calibração guiada em 30/10/2026"))).Ok);
        await _b.Tela.EsvaziarAsync();
        await _vm.Escolher.ExecutarAsync(0);
        await _b.Tela.EsvaziarAsync();
        Assert.Equal("Passo 1 de 4", _vm.Passo);
        Assert.True(_vm.PodeMedirBluetoothDeNovo);
        Assert.Equal("O Bluetooth já está medido: −13,0 dB.", _vm.AvisoBluetoothMedido);

        await _vm.MedirBluetoothDeNovo.ExecutarAsync();
        await _b.Tela.EsvaziarAsync();

        Assert.Equal("Passo 1 de 6", _vm.Passo);
        Assert.False(_vm.PodeMedirBluetoothDeNovo);
        Assert.Null(_vm.AvisoBluetoothMedido);
        await _b.Rodar(_vm.Cancelar.ExecutarAsync());
        Assert.Equal(EtapaCalibracao.Fechada, _vm.Etapa);
    }

    [Fact]
    public async Task O_campo_aceita_a_virgula_e_a_leitura_absurda_pede_para_conferir()
    {
        await Pronto();
        await AteDigitarTom(0, "Passo 1 de 6");

        await Confirmar("onze");
        Assert.Equal(ControleParametroViewModel.MsgNaoNumero, _vm.ErroVolts);
        Assert.Equal(EtapaCalibracao.DigitarTom, _vm.Etapa);

        await Confirmar("0,3");
        Assert.Equal("Confira a leitura: o ganho daria ×0,5.", _vm.Mensagem);
        Assert.Equal(EtapaCalibracao.DigitarTom, _vm.Etapa);
        Assert.True(_vm.PodeTocarDeNovo); // o gerador parou: "Tocar de novo" vale

        await _b.Rodar(_vm.TocarDeNovo.ExecutarAsync());
        Assert.True(_vm.Tocando);
        await _b.Rodar(_vm.Cancelar.ExecutarAsync());
        Assert.Equal(EtapaCalibracao.Fechada, _vm.Etapa);
        Assert.Equal(Calibracao.MsgCancelada, _vm.Mensagem);
        Assert.Equal(EstadoGerador.Desligado, _b.Gerador.Estado);
    }

    [Fact]
    public async Task Fora_do_Bluetooth_o_passo_5_avisa_e_nao_toca()
    {
        await Pronto();
        await AteDigitarTom(0, "Passo 1 de 6");
        await Confirmar("11,0");
        _audio.SaindoPeloBluetooth = false;
        await _b.Tela.EsvaziarAsync();

        Assert.Equal(Calibracao.MsgForaDoBluetooth, _vm.AvisoSaidaDoSom);
        await _b.Rodar(_vm.TocarTom.ExecutarAsync());

        Assert.Equal(EtapaCalibracao.Bluetooth, _vm.Etapa);
        Assert.Equal("O som do celular não está saindo pelo Bluetooth.", _vm.Mensagem);
        Assert.Equal(0, _tom.Pedidos);
        await _b.Rodar(_vm.Cancelar.ExecutarAsync());
    }

    [Fact]
    public async Task Com_a_chave_do_gerador_desligada_a_escolha_fica_travada_com_o_motivo()
    {
        await _b.Conectar();

        Assert.Equal("Ligue a chave do Gerador antes de calibrar.", _vm.MotivoTrava);
        await _vm.Escolher.ExecutarAsync(0);
        await _b.Tela.EsvaziarAsync();

        Assert.Equal(EtapaCalibracao.Fechada, _vm.Etapa);
        Assert.Equal("Ligue a chave do Gerador antes de calibrar.", _vm.Mensagem);
        SemIa();
    }

    // ----------------------------------------------------------------- o aviso da tela

    [Fact]
    public async Task O_ajuste_da_saida_avisa_sem_calibracao_e_deixa_mexer()
    {
        await Pronto();
        using var jbl = new AjusteSaidaViewModel(0, _b.Fabrica, _b.Setups!, _b.Sessao, _b.Chaves, _b.Estado, _b.Link, _b.CriarSalvarViewModel(),
            new NavegacaoFalsa(), _b.Tela, _b.Relogio);
        using var sub = new AjusteSaidaViewModel(2, _b.Fabrica, _b.Setups!, _b.Sessao, _b.Chaves, _b.Estado, _b.Link, _b.CriarSalvarViewModel(),
            new NavegacaoFalsa(), _b.Tela, _b.Relogio);
        await _b.Tela.EsvaziarAsync();

        Assert.Equal("Sem calibração: o app não sabe a tensão.", jbl.AvisoCalibracao);
        Assert.Equal("Sem calibração: o app não sabe a tensão.", sub.AvisoCalibracao);
        Assert.Equal("Sem calibração: o app não sabe a tensão.", SaidaTela.EmUso(_b.Setups!)[0].AvisoCalibracao);

        // A tela passa.
        var ganho = jbl.Ganho;
        ganho.AbrirCampo();
        ganho.TextoCampo = "2,0";
        await ganho.Confirmar.ExecutarAsync();
        await _b.Rodar(ganho.UltimaEscrita);
        Assert.Contains("*081*0*2.0*", _b.Simulador.Escritas);

        // O ganho do canal medido tira o aviso da JBL; o Sub, na ponte, continua.
        Assert.True((await _b.Setups!.GravarGanhoMedidoAsync(CasaDoDenis.IdSetupCasa, CasaDoDenis.IdModuloCasa, ponte: false,
            new DadoTecnico(19.5m, OrigemDado.Medido, "Calibração guiada em 31/10/2026"))).Ok);
        await _b.Tela.EsvaziarAsync();
        Assert.Null(jbl.AvisoCalibracao);
        Assert.Equal("Sem calibração: o app não sabe a tensão.", sub.AvisoCalibracao);
    }
}
