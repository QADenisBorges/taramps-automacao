using System.Text.RegularExpressions;
using BgsControle.Nucleo.Escrita;
using BgsControle.Nucleo.Gerador;
using BgsControle.Nucleo.Leitura;
using BgsControle.Nucleo.Player;
using BgsControle.Nucleo.Presets;
using BgsControle.Nucleo.Registro;
using BgsControle.Nucleo.Rendimento;
using BgsControle.Nucleo.Seguranca;
using BgsControle.Nucleo.Setups;
using BgsControle.Nucleo.Uso;
using BgsControle.Simulador;

namespace BgsControle.Nucleo.Testes;

/// <summary>O tom do celular nos testes: registra os pedidos e as paradas.</summary>
internal sealed class TomFalso : ITomCelular
{
    private readonly object _trava = new();
    private readonly List<(int Hz, TimeSpan Duracao)> _pedidos = [];
    private bool _tocando;
    private int _paradas;

    /// <summary>O próximo pedido falha (o áudio do celular não abriu).</summary>
    public bool Falhar { get; set; }

    public IReadOnlyList<(int Hz, TimeSpan Duracao)> Pedidos
    {
        get
        {
            lock (_trava)
                return _pedidos.ToArray();
        }
    }

    public bool Tocando
    {
        get
        {
            lock (_trava)
                return _tocando;
        }
    }

    public int Paradas
    {
        get
        {
            lock (_trava)
                return _paradas;
        }
    }

    public Task<bool> TocarAsync(int hz, TimeSpan duracao)
    {
        lock (_trava)
        {
            _pedidos.Add((hz, duracao));
            _tocando = !Falhar;
            return Task.FromResult(!Falhar);
        }
    }

    public Task PararAsync()
    {
        lock (_trava)
        {
            _tocando = false;
            _paradas++;
        }
        return Task.CompletedTask;
    }
}

/// <summary>
/// História 13b, a calibração guiada contra o processador simulado: o Casa 2.1 da fixture sem calibração (o ganho do
/// módulo só digitado), a FILME em uso. Uma linha da matriz por teste.
/// </summary>
public sealed class CalibracaoTestes : IDisposable
{
    private static readonly TemposSessao Tempos = new(
        Scan: TimeSpan.FromSeconds(1),
        TentativaConexao: TimeSpan.FromSeconds(1),
        PausaEntreTentativas: TimeSpan.FromMilliseconds(1),
        RespostaSenha: TimeSpan.FromSeconds(2),
        PrazoSenha: TimeSpan.FromSeconds(60));

    private static readonly TemposLeitura LeituraRapida = new(TimeSpan.Zero, TimeSpan.FromMilliseconds(100));

    private static readonly TemposEscrita Escrita = new(
        Intervalo: TimeSpan.FromMilliseconds(30),
        Arrasto: TimeSpan.FromMilliseconds(300),
        PrazoEco: TimeSpan.FromMilliseconds(300),
        PrazoReleitura: TimeSpan.FromMilliseconds(400));

    private static readonly TemposGerador TemposDoGerador = new(TimeSpan.FromSeconds(30), TimeSpan.FromMilliseconds(100));
    private static readonly Regex Senha = new(@"^\*000\*[0-9A-Z]{4}\*$");
    private static readonly TimeSpan Prazo = TimeSpan.FromSeconds(30);

    private readonly RelogioFalso _relogio = new();
    private readonly ProcessadorSimulado _simulador;
    private readonly Chaves _chaves;
    private readonly LogBle _log = new();
    private readonly Sessao _sessao;
    private readonly LeituraAutomatica _leitura;
    private readonly EstadoAparelho _estado;
    private readonly Escritor _escritor;
    private readonly TextoMemoria _guardadoCopia = new();
    private readonly CopiaAntes _copia;
    private readonly GerentePlaylists _playlists;
    private readonly GerenteSetups _setups;
    private readonly GeradorSinal _gerador;
    private readonly ReprodutorTeste _reprodutor = new();
    private readonly Player.Player _player;
    private readonly TomFalso _tom = new();
    private readonly Calibracao _calibracao;

    public CalibracaoTestes()
    {
        _simulador = new ProcessadorSimulado(_relogio) { AtrasoSenha = TimeSpan.Zero, AtrasoLeitura = TimeSpan.Zero };
        _chaves = new Chaves(new RepositorioMemoria());
        _sessao = new Sessao(_simulador, new CofreFalso { Senha = "AB12" }, _log, Tempos, _chaves);
        _leitura = new LeituraAutomatica(_sessao, new LeitorCompleto(_sessao, _log, LeituraRapida));
        _estado = new EstadoAparelho(_sessao, _leitura);
        _escritor = new Escritor(_sessao, _estado, _log, Escrita, _relogio);
        _copia = new CopiaAntes(_sessao, _estado, _escritor, _leitura, _guardadoCopia, _log, _relogio);
        var link = new LinkCanais(_sessao, _estado, _escritor, new TextoMemoria(), _log);
        var presets = new GerentePresets(_sessao, _estado, _escritor, _leitura, _copia, new TextoMemoria(CenarioFilme.PresetsJson), _log, _relogio);
        _playlists = new GerentePlaylists(new TextoMemoria(), _log);
        var dados = CenarioFilme.Dados with
        {
            Setups = [CasaDoDenis.SetupCasa with { Equipamentos = CasaDoDenis.EquipamentosSemCalibracao }],
        };
        _setups = new GerenteSetups(_sessao, _estado, _escritor, _leitura, _copia, link, presets, _playlists, _chaves,
            new TextoMemoria(GerenteSetups.ParaJson(dados)), new TextoMemoria(), _log, _relogio);
        _gerador = new GeradorSinal(_sessao, _estado, _leitura, _copia, _log, Escrita, TemposDoGerador, _relogio);
        _player = new Player.Player(_playlists, _setups, _reprodutor, _log);
        _calibracao = new Calibracao(_sessao, _estado, _escritor, _copia, _setups, _gerador, _chaves, _tom, _log, _player, _relogio);
    }

    /// <summary>
    /// Critério de aceite: só a senha, as leituras, o áudio e o gerador do catálogo; nenhum 240, 241 ou 23N; cada
    /// <c>*202*1*</c> com um <c>*202*0*</c> até 30 s depois.
    /// </summary>
    public void Dispose()
    {
        _calibracao.Dispose();
        _player.Dispose();
        Assert.DoesNotContain(_simulador.Escritas, e => e.StartsWith("*24", StringComparison.Ordinal) || e.StartsWith("*23", StringComparison.Ordinal));
        Assert.All(_simulador.Escritas, e =>
            Assert.True(Senha.IsMatch(e) || Catalogo.EhLeitura(e) || Catalogo.EscritaDeAudio(e) is not null
                        || Catalogo.ComandoDeGerador(e) is not null, e));
        var marcas = _simulador.LinhaDoTempoGerador;
        for (var i = 0; i < marcas.Count; i++)
        {
            if (!marcas[i].Ligado)
                continue;
            var desligou = marcas.Skip(i + 1).FirstOrDefault(m => !m.Ligado);
            Assert.True(desligou is not null && desligou.Quando - marcas[i].Quando <= Prazo, "um *202*1* ficou sem *202*0* em 30 s");
        }
    }

    // ----------------------------------------------------------------- apoio

    /// <summary>O reprodutor do Player: toca na hora e registra a pausa.</summary>
    private sealed class ReprodutorTeste : IReprodutor
    {
        public bool Pausado { get; private set; }

        public event Action<string>? Terminou;

        public event Action<string>? Pausou;

        public event Action<string>? Falhou;

        public Task<bool> TocarAsync(string uri)
        {
            Pausado = false;
            return Task.FromResult(true);
        }

        public Task PausarAsync()
        {
            Pausado = true;
            return Task.CompletedTask;
        }

        public Task<bool> ContinuarAsync()
        {
            Pausado = false;
            return Task.FromResult(true);
        }

        public Task PararAsync() => Task.CompletedTask;

        /// <summary>Só para o compilador: os eventos existem na interface.</summary>
        public void Nada()
        {
            Terminou?.Invoke("");
            Pausou?.Invoke("");
            Falhou?.Invoke("");
        }
    }

    private async Task Conectar(bool chaveGerador = true)
    {
        await _chaves.Carregada;
        await _copia.Carregada;
        await _setups.Carregada;
        if (chaveGerador)
            Assert.True((await _chaves.DefinirAsync(Chave.Gerador, true, OrigemComando.Tela)).Ok);
        await _sessao.ConectarAsync();
        Assert.Equal(EstadoSessao.Conectado, _sessao.Estado);
        await AndarAte(() => _estado.Pronto && !_estado.Lendo && !_leitura.Lendo && !_copia.EmAndamento
                             && _gerador.Estado == EstadoGerador.Desligado && _setups.Conferido);
        Assert.Equal(CasaDoDenis.IdSetupCasa, _setups.SetupEmUso?.Id);
    }

    private async Task AndarAte(Func<bool> condicao, int passos = 3000)
    {
        for (var i = 0; i < passos && !condicao(); i++)
        {
            _relogio.Avancar(TimeSpan.FromMilliseconds(10));
            await Task.Delay(1);
        }
        Assert.True(condicao(), "a condição não ficou verdadeira");
    }

    private async Task<T> Rodar<T>(Task<T> tarefa)
    {
        await AndarAte(() => tarefa.IsCompleted);
        return await tarefa;
    }

    /// <summary>Uma escrita como o app (sem o "usuário mexendo").</summary>
    private async Task EscreverComoApp(string codigo, int? canal, decimal valor)
    {
        using (Origem.Como(OrigemComando.App))
            Assert.True((await Rodar(_escritor.EscreverAsync(Catalogo.ParametroDe(codigo)!, canal, valor))).Ok);
    }

    private Equipamento Modulo => _setups.SetupEmUso!.Equipamentos.First(e => e.Id == CasaDoDenis.IdModuloCasa);

    private List<string> AudioDesde(int inicio) =>
        _simulador.Escritas.Skip(inicio).Where(e => Catalogo.EscritaDeAudio(e) is not null).ToList();

    /// <summary>O aparelho voltou ao retrato (os 84 do Marco 1), menos os endereços dados.</summary>
    private void IgualAo(IReadOnlyDictionary<(string Codigo, int? Indice), string> antes, params (string, int?)[] menos)
    {
        foreach (var (endereco, noFio) in antes)
        {
            if (menos.Contains(endereco))
                continue;
            var agora = _simulador.Valor(endereco.Codigo, endereco.Indice);
            Assert.True(agora is not null && Camadas.Iguais(endereco.Codigo, agora, noFio),
                $"{endereco.Codigo}/{endereco.Indice}: {agora} no aparelho, {noFio} antes");
        }
    }

    /// <summary>Abre, confirma o passo 1 e segue do multímetro: a saída preparada e o tom tocando.</summary>
    private async Task AteOTom(int mestre)
    {
        Assert.True(_calibracao.Abrir(mestre).Ok, _calibracao.Mensagem);
        Assert.Equal(EtapaCalibracao.Desligar, _calibracao.Etapa);
        Assert.True(_calibracao.ConfirmarDesligado().Ok);
        Assert.Equal(EtapaCalibracao.Multimetro, _calibracao.Etapa);
        var r = await Rodar(_calibracao.SeguirAsync());
        Assert.True(r.Ok, r.Motivo);
        Assert.Equal(EtapaCalibracao.Tom, _calibracao.Etapa);
        Assert.Equal(EstadoGerador.Tocando, _gerador.Estado);
        Assert.True(_calibracao.IrParaLeitura().Ok);
    }

    /// <summary>Uma faixa tocando no Player (a playlist da FILME).</summary>
    private async Task TocarMusica()
    {
        Assert.True((await _playlists.AdicionarAsync(CenarioFilme.IdMemoria,
            [new ArquivoAudio("content://musica/1", "Faixa 1", "Musicas", TimeSpan.FromMinutes(3))])).Ok);
        await _player.TocarPausarAsync();
        Assert.Equal(EstadoPlayer.Tocando, _player.Estado);
    }

    // ----------------------------------------------------------------- a matriz

    [Fact]
    public async Task Calibrar_a_JBL_L_prepara_a_saida_toca_360_Hz_e_grava_o_ganho_19_5_medido()
    {
        await Conectar();
        var antes = _simulador.EstadoMarco1();

        await AteOTom(0);

        // 081 = 0, 080 = 0, 071 = −11, EQ de saída plano e CH2–CH4 mutados.
        Assert.Equal(0m, _estado.Valor("081", 0)!.Numero);
        Assert.Equal(0m, _estado.Valor("080")!.Numero);
        Assert.Equal(-11.0m, _estado.Valor("071", 0)!.Numero);
        Assert.Equal(0m, _estado.Valor("091", 0)!.Numero);
        Assert.Equal(0m, _estado.Valor("100", 0)!.Numero);
        foreach (var c in new[] { 1, 2, 3 })
            Assert.Equal(1m, _estado.Valor("100", c)!.Numero);
        // O passa-alta LR24 em 150 Hz: a menor atenuação é a de 400 Hz (0,17 dB); a mais baixa até 0,1 dB acima é 360 Hz.
        Assert.Equal(360, _calibracao.Frequencia);
        Assert.Equal("360", _simulador.Valor("201"));
        // O EQ de entrada e as 15 bandas planos.
        Assert.Equal(0m, _estado.Valor("030")!.Numero);
        Assert.All(Enumerable.Range(0, Catalogo.BandasHz.Count), b => Assert.Equal(0m, _estado.Valor("020", b)!.Numero));
        Assert.Equal(0m, _estado.Valor("200")!.Numero);
        Assert.Equal(Calibracao.Tipo, _copia.Ultima!.Operacao!.Tipo);

        var r = await Rodar(_calibracao.InformarTomAsync(11.0m));

        Assert.True(r.Ok, r.Motivo);
        Assert.Equal(EstadoGerador.Desligado, _gerador.Estado); // o gerador para quando o usuário digita
        var ganho = Modulo.Dados.Ganho!;
        Assert.Equal(OrigemDado.Medido, ganho.Origem);
        Assert.Equal("19,5", Calibracao.Ganho(ganho.Valor));
        Assert.StartsWith("Calibração guiada em ", ganho.Fonte);
        Assert.Equal(OrigemDado.Digitado, Modulo.Dados.GanhoPonte!.Origem); // o canal não mexe na ponte
        Assert.Equal(EtapaCalibracao.Bluetooth, _calibracao.Etapa); // o Setup não tem o nível medido

        var cancelar = await Rodar(_calibracao.CancelarAsync());
        Assert.True(cancelar.Ok, cancelar.Motivo);
        IgualAo(antes);
        Assert.Null(_copia.Ultima!.Operacao);
        Assert.Equal(OrigemDado.Medido, Modulo.Dados.Ganho!.Origem); // o gravado fica
    }

    [Fact]
    public async Task Ponte_do_Sub_toca_no_passa_banda_e_grava_o_ganho_da_ponte_40_5()
    {
        await Conectar();

        await AteOTom(2);

        Assert.Equal("ponte CH3+CH4", _calibracao.Alvo!.Onde);
        Assert.InRange(_calibracao.Frequencia!.Value, 50, 100); // entre o BT24 em 40 Hz e o LR24 em 150 Hz
        Assert.Equal(-11.0m, _estado.Valor("071", 2)!.Numero);
        Assert.Equal(-11.0m, _estado.Valor("071", 3)!.Numero);
        Assert.Equal(0m, _estado.Valor("081", 2)!.Numero);
        Assert.Equal(0m, _estado.Valor("081", 3)!.Numero);
        Assert.Equal(1m, _estado.Valor("100", 0)!.Numero);
        Assert.Equal(1m, _estado.Valor("100", 1)!.Numero);

        var r = await Rodar(_calibracao.InformarTomAsync(22.8m));

        Assert.True(r.Ok, r.Motivo);
        var ponte = Modulo.Dados.GanhoPonte!;
        Assert.Equal(OrigemDado.Medido, ponte.Origem);
        Assert.Equal("40,5", Calibracao.Ganho(ponte.Valor));
        Assert.Equal(OrigemDado.Digitado, Modulo.Dados.Ganho!.Origem);
        Assert.True((await Rodar(_calibracao.CancelarAsync())).Ok);
    }

    [Fact]
    public async Task Bluetooth_com_8_73_V_grava_menos_13_dB_medido_pausa_o_Player_e_volta_ao_som_de_antes()
    {
        await Conectar();
        await TocarMusica();
        var antes = _simulador.EstadoMarco1();
        await AteOTom(0);
        Assert.True((await Rodar(_calibracao.InformarTomAsync(11.0m))).Ok);

        var fora = await Rodar(_calibracao.TocarBluetoothAsync(false));
        Assert.False(fora.Ok);
        Assert.Equal("O som do celular não está saindo pelo Bluetooth.", fora.Motivo);
        Assert.Empty(_tom.Pedidos);
        Assert.Equal(EtapaCalibracao.Bluetooth, _calibracao.Etapa);

        var tocar = await Rodar(_calibracao.TocarBluetoothAsync(true));

        Assert.True(tocar.Ok, tocar.Motivo);
        Assert.Equal(EtapaCalibracao.DigitarBluetooth, _calibracao.Etapa);
        Assert.Equal((360, TimeSpan.FromSeconds(30)), Assert.Single(_tom.Pedidos));
        Assert.Equal(EstadoPlayer.Pausado, _player.Estado);
        Assert.True(_reprodutor.Pausado);
        // Com o ganho medido, a JBL L ficou cercada: o limitador vai a min(0; teto).
        var teto = _setups.LimitesEmUso.First(l => l.Saida.Mestre == 0).TetoCerca!.Value;
        Assert.Equal(Math.Min(0m, teto), _estado.Valor("071", 0)!.Numero);

        var r = await Rodar(_calibracao.InformarBluetoothAsync(8.73m));

        Assert.True(r.Ok, r.Motivo);
        Assert.False(_tom.Tocando);
        Assert.Equal(EtapaCalibracao.Fim, _calibracao.Etapa);
        // 8,73 V dão −13,0 dB na saída; o Setup guarda o da entrada, com o passa-alta em 360 Hz (0,26 dB) somado.
        Assert.Equal("Ganho ×19,5 e Bluetooth −12,7 dB gravados.", r.Mensagem);
        var nivel = _setups.SetupEmUso!.NivelBluetooth!;
        Assert.Equal(-12.7m, nivel.Valor);
        Assert.Equal(OrigemDado.Medido, nivel.Origem);
        Assert.Null(_copia.Ultima!.Operacao);
        IgualAo(antes);
        Assert.Equal(EstadoPlayer.Tocando, _player.Estado); // o Player que tocava volta a tocar
        Assert.False(_reprodutor.Pausado);
        Assert.True(_calibracao.Concluir().Ok);
        Assert.Equal(EtapaCalibracao.Fechada, _calibracao.Etapa);
    }

    [Fact]
    public async Task Bluetooth_sem_corte_com_8_73_V_grava_menos_13_0_dB_medido()
    {
        await Conectar();
        // A JBL L sem passa-alta e sem passa-baixa (042 e 043 em OFF), escritos antes de calibrar, no Casa sem
        // calibração (a cerca não vale ainda): atenuação zero em f.
        await EscreverComoApp("042", 0, 0m);
        await EscreverComoApp("043", 0, 0m);
        // Sem passa-alta, o curso do cone pela conta daria um teto de ~−18 dB, e o limitador seguraria o tom do
        // celular. Sem o Xmax na ficha, o X vai ao caso 3 (min(T, C) − 3 dB, ~−9,8 dB) e não segura.
        var jbl = _setups.SetupEmUso!.Equipamentos.First(e => e.Id == CasaDoDenis.IdJblL);
        Assert.True((await Rodar(_setups.EditarEquipamentoAsync(CasaDoDenis.IdSetupCasa, jbl with { Dados = jbl.Dados with { Xmax = null } }))).Ok);

        await AteOTom(0);
        Assert.Equal(50, _calibracao.Frequencia); // sem corte, a mais baixa da grade
        Assert.Equal(0.0, Calibracao.AtenuacaoDosCanais(_estado.Valor, [0], 50));
        Assert.True((await Rodar(_calibracao.InformarTomAsync(11.0m))).Ok);
        Assert.True((await Rodar(_calibracao.TocarBluetoothAsync(true))).Ok);

        var r = await Rodar(_calibracao.InformarBluetoothAsync(8.73m));

        Assert.True(r.Ok, r.Motivo);
        Assert.Equal("Ganho ×19,5 e Bluetooth −13,0 dB gravados.", r.Mensagem);
        var nivel = _setups.SetupEmUso!.NivelBluetooth!;
        Assert.Equal(-13.0m, nivel.Valor);
        Assert.Equal(OrigemDado.Medido, nivel.Origem);
    }

    [Fact]
    public async Task Uma_banda_de_menos_6_dB_em_f_nao_muda_o_nivel_gravado()
    {
        await Conectar();
        // A banda de 400 Hz em −6 dB antes da calibração: a preparação a põe em 0 e a cópia a devolve.
        await EscreverComoApp("020", 6, -6.0m);
        await EscreverComoApp("030", null, -3.0m);
        var antes = _simulador.EstadoMarco1();
        await AteOTom(0);
        Assert.Equal(0m, _estado.Valor("020", 6)!.Numero);
        Assert.Equal(0m, _estado.Valor("030")!.Numero);
        Assert.True((await Rodar(_calibracao.InformarTomAsync(11.0m))).Ok);
        Assert.True((await Rodar(_calibracao.TocarBluetoothAsync(true))).Ok);
        Assert.Equal(0m, _estado.Valor("020", 6)!.Numero);

        var r = await Rodar(_calibracao.InformarBluetoothAsync(8.73m));

        Assert.True(r.Ok, r.Motivo);
        Assert.Equal(-12.7m, _setups.SetupEmUso!.NivelBluetooth!.Valor); // o mesmo da calibração sem a banda
        Assert.Equal(-6.0m, _estado.Valor("020", 6)!.Numero);
        IgualAo(antes);
    }

    [Fact]
    public async Task Recalibrar_com_o_teto_abaixo_de_menos_11_usa_o_teto_como_limitador()
    {
        await Conectar();
        // O ganho medido ×30: o teto da JBL L (pelo curso do cone, em 150 Hz) fica abaixo de −11 dB.
        Assert.True((await Rodar(_setups.GravarGanhoMedidoAsync(CasaDoDenis.IdSetupCasa, CasaDoDenis.IdModuloCasa, false,
            new DadoTecnico(30m, OrigemDado.Medido, "Calibração guiada em 30/10/2026")))).Ok);
        var teto = _setups.LimitesEmUso.First(l => l.Saida.Mestre == 0).TetoCerca!.Value;
        Assert.True(teto < Calibracao.LimitadorReferencia, $"teto {teto}");

        await AteOTom(0);

        Assert.Equal(teto, _calibracao.Limitador);
        Assert.Equal(teto, _estado.Valor("071", 0)!.Numero);
        Assert.True((await Rodar(_calibracao.InformarTomAsync(11.0m))).Ok);
        Assert.Equal(Calibracao.GanhoDe(11.0m, teto), _calibracao.GanhoMedido);
        Assert.Equal(Calibracao.GanhoDe(11.0m, teto), Modulo.Dados.Ganho!.Valor);
        Assert.True((await Rodar(_calibracao.CancelarAsync())).Ok);
    }

    [Fact]
    public async Task Uma_escrita_da_preparacao_que_falha_volta_ao_som_de_antes_e_fecha()
    {
        await Conectar();
        var antes = _simulador.EstadoMarco1();
        _simulador.EcosDiferentes["*081*0*0.0*"] = "#081#0#+1.0#"; // o aparelho diz que ficou em +1,0 dB
        Assert.True(_calibracao.Abrir(0).Ok);
        Assert.True(_calibracao.ConfirmarDesligado().Ok);

        var r = await Rodar(_calibracao.SeguirAsync());

        Assert.False(r.Ok);
        Assert.StartsWith("Não consegui preparar a saída:", r.Motivo);
        Assert.Equal(EtapaCalibracao.Fechada, _calibracao.Etapa);
        Assert.Null(_copia.Ultima!.Operacao);
        Assert.Null(_copia.PelaMetade);
        Assert.Equal(EstadoGerador.Desligado, _gerador.Estado);
        _simulador.EcosDiferentes.Clear();
        await Rodar(_leitura.LerNovaAsync());
        IgualAo(antes);
    }

    [Fact]
    public async Task O_tom_do_celular_que_nao_toca_recusa_e_o_passo_fica()
    {
        await Conectar();
        await AteOTom(0);
        Assert.True((await Rodar(_calibracao.InformarTomAsync(11.0m))).Ok);
        _tom.Falhar = true;

        var r = await Rodar(_calibracao.TocarBluetoothAsync(true));

        Assert.False(r.Ok);
        Assert.Equal(Calibracao.MsgTomCelular, r.Motivo);
        Assert.Equal(EtapaCalibracao.Bluetooth, _calibracao.Etapa);
        Assert.True((await Rodar(_calibracao.CancelarAsync())).Ok);
    }

    [Fact]
    public async Task Cancelar_enquanto_a_copia_e_guardada_fecha_como_cancelada_sem_marca()
    {
        await Conectar();
        var inicio = _simulador.Escritas.Count;
        Assert.True(_calibracao.Abrir(0).Ok);
        Assert.True(_calibracao.ConfirmarDesligado().Ok);
        Task<ResultadoCalibracao>? cancelar = null;
        _guardadoCopia.AoGravar = () => cancelar ??= _calibracao.CancelarAsync();

        var r = await Rodar(_calibracao.SeguirAsync());
        _guardadoCopia.AoGravar = null;

        Assert.False(r.Ok);
        Assert.Equal(Calibracao.MsgCancelada, r.Motivo);
        Assert.True((await cancelar!).Ok);
        Assert.Equal(EtapaCalibracao.Fechada, _calibracao.Etapa);
        Assert.Null(_copia.Ultima!.Operacao);
        Assert.Null(_copia.PelaMetade);
        Assert.False(_copia.EmAndamento);
        Assert.Empty(AudioDesde(inicio));
    }

    [Fact]
    public async Task Ja_medido_pula_os_passos_5_e_6_e_Medir_o_Bluetooth_de_novo_volta_a_eles()
    {
        await Conectar();
        var nivel = new DadoTecnico(-13.0m, OrigemDado.Medido, "Calibração guiada em 30/10/2026");
        Assert.True((await Rodar(_setups.DefinirNivelBluetoothAsync(CasaDoDenis.IdSetupCasa, nivel))).Ok);
        Assert.Equal(nivel, _calibracao.BluetoothMedido);

        await AteOTom(2);
        var r = await Rodar(_calibracao.InformarTomAsync(22.8m));

        Assert.True(r.Ok, r.Motivo);
        Assert.Equal(EtapaCalibracao.Fim, _calibracao.Etapa);
        Assert.Equal("Ganho ×40,5 gravado.", r.Mensagem);
        Assert.Empty(_tom.Pedidos);
        Assert.Null(_copia.Ultima!.Operacao);
        Assert.True(_calibracao.Concluir().Ok);

        // De novo, pedindo o Bluetooth: os passos 5 e 6 voltam.
        Assert.True(_calibracao.Abrir(0).Ok);
        Assert.True(_calibracao.MedirBluetoothDeNovo().Ok);
        Assert.True(_calibracao.ConfirmarDesligado().Ok);
        Assert.True((await Rodar(_calibracao.SeguirAsync())).Ok);
        Assert.True((await Rodar(_calibracao.InformarTomAsync(11.0m))).Ok);
        Assert.Equal(EtapaCalibracao.Bluetooth, _calibracao.Etapa);
        Assert.True((await Rodar(_calibracao.CancelarAsync())).Ok);
    }

    [Fact]
    public async Task Leitura_absurda_pede_para_conferir_e_o_passo_se_repete()
    {
        await Conectar();
        await AteOTom(0);

        var r = await Rodar(_calibracao.InformarTomAsync(0.3m));

        Assert.False(r.Ok);
        Assert.Equal("Confira a leitura: o ganho daria ×0,5.", r.Motivo);
        Assert.Equal(EtapaCalibracao.DigitarTom, _calibracao.Etapa);
        Assert.Equal(OrigemDado.Digitado, Modulo.Dados.Ganho!.Origem);
        Assert.Equal(EstadoGerador.Desligado, _gerador.Estado);

        var foraDaFaixa = await Rodar(_calibracao.InformarTomAsync(250m));
        Assert.Equal(Calibracao.MsgVolts, foraDaFaixa.Motivo);

        // "Tocar de novo" vale enquanto não houver resposta.
        Assert.True((await Rodar(_calibracao.TocarDeNovoAsync())).Ok);
        Assert.Equal(EstadoGerador.Tocando, _gerador.Estado);
        Assert.True((await Rodar(_calibracao.InformarTomAsync(11.0m))).Ok);
        Assert.Equal(OrigemDado.Medido, Modulo.Dados.Ganho!.Origem);
        Assert.True((await Rodar(_calibracao.CancelarAsync())).Ok);
    }

    [Fact]
    public async Task Limitador_que_segurou_o_tom_do_celular_pede_para_conferir_o_volume()
    {
        await Conectar();
        await AteOTom(0);
        Assert.True((await Rodar(_calibracao.InformarTomAsync(11.0m))).Ok);
        Assert.True((await Rodar(_calibracao.TocarBluetoothAsync(true))).Ok);

        // 13,0 V com o ganho ×19,5: −9,5 dB, a menos de 0,5 dB do limitador (o teto da JBL L pelo curso do cone).
        var r = await Rodar(_calibracao.InformarBluetoothAsync(13.0m));

        Assert.False(r.Ok);
        Assert.Equal("O limitador segurou o tom; confira o volume.", r.Motivo);
        Assert.Equal(EtapaCalibracao.DigitarBluetooth, _calibracao.Etapa);
        Assert.Null(_setups.SetupEmUso!.NivelBluetooth);
        Assert.True((await Rodar(_calibracao.CancelarAsync())).Ok);
    }

    [Fact]
    public async Task Chave_do_gerador_desligada_nao_comeca()
    {
        await Conectar(chaveGerador: false);
        var inicio = _simulador.Escritas.Count;

        var r = _calibracao.Abrir(0);

        Assert.False(r.Ok);
        Assert.Equal("Ligue a chave do Gerador antes de calibrar.", r.Motivo);
        Assert.Equal(EtapaCalibracao.Fechada, _calibracao.Etapa);
        Assert.Empty(AudioDesde(inicio));
    }

    [Fact]
    public async Task Modulo_desligado_nao_comeca()
    {
        _simulador.Definir("252", null, "0");
        await Conectar();

        var r = _calibracao.Abrir(0);

        Assert.False(r.Ok);
        Assert.Equal("Ligue o módulo (remote out) antes de calibrar.", r.Motivo);
    }

    [Fact]
    public async Task Corte_estreito_nao_comeca()
    {
        // A JBL L com o passa-alta em 1 kHz (LR24): nenhum tom de 50 a 400 Hz passa com até 3 dB.
        await Conectar();
        await EscreverComoApp("040", 0, 1000m);

        var r = _calibracao.Abrir(0);

        Assert.False(r.Ok);
        Assert.Equal("O corte desta saída não deixa passar um tom entre 50 e 400 Hz.", r.Motivo);
        Assert.Equal(Calibracao.MsgCorte, _calibracao.Opcoes().First(o => o.Saida.Mestre == 0).Motivo);
    }

    [Fact]
    public async Task Saida_sem_modulo_nao_se_calibra()
    {
        await Conectar();
        // Um subwoofer ativo (grupo 1) no CH2 no lugar da JBL R.
        var equipamentos = _setups.SetupEmUso!.Equipamentos.Where(e => e.Id != CasaDoDenis.IdJblR)
            .Select(e => e.Id == CasaDoDenis.IdModuloCasa
                ? e with { Entradas = e.Entradas.Select(x => x.Numero == 2 ? x with { Fonte = null } : x).ToArray() }
                : e)
            .Append(new Equipamento("ativo", GrupoEquipamento.AmplificaETocam, "caixa ativa", "Caixa ativa")
            {
                Entradas = [new Entrada(1, FonteSinal.DoCanal(1))],
            })
            .ToArray();
        foreach (var e in equipamentos.Where(e => e.Id is "ativo" or CasaDoDenis.IdModuloCasa))
        {
            var r0 = e.Id == "ativo"
                ? await Rodar(_setups.AdicionarEquipamentoAsync(CasaDoDenis.IdSetupCasa, e))
                : await Rodar(_setups.EditarEquipamentoAsync(CasaDoDenis.IdSetupCasa, e));
            Assert.True(r0.Ok, r0.Motivo);
        }
        Assert.True((await Rodar(_setups.TirarEquipamentoAsync(CasaDoDenis.IdSetupCasa, CasaDoDenis.IdJblR))).Ok);

        var r = _calibracao.Abrir(1);

        Assert.False(r.Ok);
        Assert.Equal("Esta saída não passa por um módulo.", r.Motivo);
    }

    [Fact]
    public async Task Cancelar_no_tom_para_o_gerador_e_volta_a_copia()
    {
        await Conectar();
        await EscreverComoApp("070", 0, 1m); // o limitador em Auto: a preparação o põe em manual
        var antes = _simulador.EstadoMarco1();
        await AteOTom(0);
        Assert.Equal(EstadoGerador.Tocando, _gerador.Estado);
        Assert.Equal(0m, _estado.Valor("070", 0)!.Numero);

        var r = await Rodar(_calibracao.CancelarAsync());

        Assert.True(r.Ok, r.Motivo);
        Assert.Equal(Calibracao.MsgCancelada, r.Mensagem);
        Assert.Equal(EstadoGerador.Desligado, _gerador.Estado);
        Assert.Equal("0", _simulador.Valor("202"));
        Assert.Equal(EtapaCalibracao.Fechada, _calibracao.Etapa);
        Assert.Null(_copia.Ultima!.Operacao);
        await Rodar(_leitura.LerNovaAsync());
        IgualAo(antes);
    }

    [Fact]
    public async Task Cancelar_depois_do_ganho_volta_com_o_limitador_no_teto_quando_a_copia_passa_dele()
    {
        // A JBL L com o limitador em −3 dB, acima do teto que o ganho medido vai dar: a volta vai ao teto (13a).
        await Conectar();
        await EscreverComoApp("071", 0, -3.0m);
        var antes = _simulador.EstadoMarco1();
        await AteOTom(0);
        Assert.True((await Rodar(_calibracao.InformarTomAsync(11.0m))).Ok);
        var teto = _setups.LimitesEmUso.First(l => l.Saida.Mestre == 0).TetoCerca!.Value;

        Assert.True((await Rodar(_calibracao.CancelarAsync())).Ok);

        Assert.Equal(teto, _estado.Valor("071", 0)!.Numero);
        IgualAo(antes, ("071", 0));
    }

    [Fact]
    public async Task Antes_da_operacao_Cancelar_so_fecha()
    {
        await Conectar();
        var inicio = _simulador.Escritas.Count;
        Assert.True(_calibracao.Abrir(0).Ok);
        Assert.True(_calibracao.ConfirmarDesligado().Ok);

        var r = await Rodar(_calibracao.CancelarAsync());

        Assert.True(r.Ok);
        Assert.Equal(EtapaCalibracao.Fechada, _calibracao.Etapa);
        Assert.Empty(AudioDesde(inicio));
        Assert.Null(_copia.Ultima?.Operacao);
    }

    [Fact]
    public async Task Queda_no_tom_segue_a_regra_1_e_ao_voltar_fica_pela_metade_e_o_Terminar_recusa()
    {
        await Conectar();
        await AteOTom(0);

        _simulador.Derrubar();
        await AndarAte(() => _calibracao.Etapa == EtapaCalibracao.Fechada);

        Assert.Equal(Calibracao.MsgCaiu, _calibracao.Mensagem);
        Assert.Equal(Calibracao.Tipo, CopiaAntes.DeJson(_guardadoCopia.Texto!)!.Operacao!.Tipo);
        Assert.False(_tom.Tocando);

        // A regra 1 reconecta para desligar o gerador; a leitura nova acha a marca: pela metade.
        await AndarAte(() => _copia.PelaMetade is not null && !_copia.EmAndamento, passos: 6000);
        Assert.Equal("0", _simulador.Valor("202"));
        Assert.Equal(Calibracao.Tipo, _copia.PelaMetade!.Tipo);

        var terminar = await Rodar(_copia.TerminarAsync());
        Assert.False(terminar.Ok);
        Assert.Equal("O app não sabe refazer esta operação.", terminar.Motivo);

        var voltar = await Rodar(_copia.VoltarAsync());
        Assert.True(voltar.Ok, voltar.Motivo);
        Assert.Null(_copia.PelaMetade);
    }

    [Fact]
    public async Task Aceite_JBL_L_e_Bluetooth_depois_o_Sub_e_o_limites_da_menos_6_8_na_JBL_em_200_Hz()
    {
        await Conectar();
        var antes = _simulador.EstadoMarco1();

        await AteOTom(0);
        Assert.True((await Rodar(_calibracao.InformarTomAsync(11.0m))).Ok);
        Assert.True((await Rodar(_calibracao.TocarBluetoothAsync(true))).Ok);
        Assert.True((await Rodar(_calibracao.InformarBluetoothAsync(8.73m))).Ok);
        Assert.True(_calibracao.Concluir().Ok);

        await AteOTom(2);
        var sub = await Rodar(_calibracao.InformarTomAsync(22.8m));
        Assert.True(sub.Ok, sub.Motivo);
        Assert.Equal(EtapaCalibracao.Fim, _calibracao.Etapa);
        Assert.True(_calibracao.Concluir().Ok);

        Assert.Equal(OrigemDado.Medido, Modulo.Dados.Ganho!.Origem);
        Assert.Equal("19,5", Calibracao.Ganho(Modulo.Dados.Ganho.Valor));
        Assert.Equal(OrigemDado.Medido, Modulo.Dados.GanhoPonte!.Origem);
        Assert.Equal("40,5", Calibracao.Ganho(Modulo.Dados.GanhoPonte.Valor));
        Assert.Equal(-12.7m, _setups.SetupEmUso!.NivelBluetooth!.Valor);
        Assert.Equal(OrigemDado.Medido, _setups.SetupEmUso.NivelBluetooth.Origem);
        await Rodar(_leitura.LerNovaAsync());
        IgualAo(antes);

        // O /limites passa a dar −6,8 dB na JBL com o corte em 200 Hz.
        using (Origem.Como(OrigemComando.App))
            Assert.True((await Rodar(_escritor.EscreverAsync(Catalogo.ParametroDe("040")!, 0, 200m))).Ok);
        var jbl = _setups.LimitesEmUso.First(l => l.Saida.Mestre == 0);
        Assert.True(jbl.Calibrada);
        Assert.Equal(-6.8m, jbl.TetoDb);
        Assert.Equal(TipoLimite.Termico, jbl.Manda);
        Assert.True(_setups.LimitesEmUso.All(l => l.Calibrada));
    }

    [Fact]
    public async Task O_nivel_do_Bluetooth_vai_e_volta_no_setups_json_com_valor_negativo_e_o_formato_4()
    {
        await Conectar();
        var nivel = new DadoTecnico(-13.0m, OrigemDado.Medido, "Calibração guiada em 31/10/2026");
        Assert.True((await Rodar(_setups.DefinirNivelBluetoothAsync(CasaDoDenis.IdSetupCasa, nivel))).Ok);

        var json = GerenteSetups.ParaJson(_setups.Dados);

        Assert.Contains("\"formato\": 4", json);
        Assert.Contains("\"nivelBluetooth\"", json);
        Assert.Contains("-13.0", json);
        var lido = GerenteSetups.DeJson(json)!;
        Assert.Equal(nivel, lido.Setups.Single().NivelBluetooth);

        // Com a origem desconhecida, o campo é descartado; o resto do Setup fica.
        var raiz = System.Text.Json.Nodes.JsonNode.Parse(json)!;
        raiz["setups"]![0]!["nivelBluetooth"]!["origem"] = "chute";
        var estragado = GerenteSetups.DeJson(raiz.ToJsonString())!;
        Assert.Null(estragado.Setups.Single().NivelBluetooth);
        Assert.Equal(CasaDoDenis.IdSetupCasa, estragado.Setups.Single().Id);
    }

    [Fact]
    public void Contas_da_calibracao()
    {
        Assert.Equal(19.51m, Calibracao.GanhoDe(11.0m, -11.0m));
        Assert.Equal(40.45m, Calibracao.GanhoDe(22.8m, -11.0m));
        Assert.Equal("40,5", Calibracao.Ganho(40.45m));
        Assert.Equal(-13.0m, Calibracao.NivelDe(8.73m, 19.51m));
        Assert.Equal(-12.7m, Calibracao.NivelDe(8.73m, 19.51m, 0.258));
        Assert.Equal("Multímetro em V~ 200 na saída CH1 do Módulo SD800.4 EVO.",
            Calibracao.MsgMultimetro(new AlvoCalibracao(new SaidaApp(0, [0], "JBL L", null), "JBL L",
                CasaDoDenis.EquipamentosCasa[0], 1, null)));
        Assert.Equal("Multímetro em V~ 200 na saída ponte CH3+CH4 do Módulo SD800.4 EVO.",
            Calibracao.MsgMultimetro(new AlvoCalibracao(new SaidaApp(2, [2, 3], "Sub", null), "Sub",
                CasaDoDenis.EquipamentosCasa[0], null, new Bridge(3, 4))));
    }
}
