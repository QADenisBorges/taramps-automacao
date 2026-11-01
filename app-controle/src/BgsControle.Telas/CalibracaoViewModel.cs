using BgsControle.Nucleo;
using BgsControle.Nucleo.Escrita;
using BgsControle.Nucleo.Gerador;
using BgsControle.Nucleo.Rendimento;
using BgsControle.Nucleo.Seguranca;
using BgsControle.Nucleo.Setups;

namespace BgsControle.Telas;

/// <summary>
/// Uma saída na escolha da calibração: o nome, os canais, se já tem o ganho medido e o motivo de não se calibrar
/// (sem módulo, corte estreito), ou null.
/// </summary>
/// <param name="Canal">O canal mestre (0 a 3): a cor da saída e o que o Escolher recebe.</param>
public sealed record OpcaoSaidaCalibracao(int Canal, string Nome, string Rotulo, bool Calibrada, string? Motivo)
{
    /// <summary>"Calibrada" ou "Sem calibração".</summary>
    public string Situacao => Calibrada ? CalibracaoViewModel.TextoCalibrada : CalibracaoViewModel.TextoSemCalibracao;

    /// <summary>Dá para calibrar esta saída (a trava geral à parte).</summary>
    public bool Pode => Motivo is null;

    /// <summary>O TalkBack da linha da escolha (história 13g): "JBL L, CH1, Sem calibração" e o motivo.</summary>
    public string Descricao => Motivo is { } m ? $"{Nome}, {Rotulo}, {Situacao}. {m}" : $"{Nome}, {Rotulo}, {Situacao}";
}

/// <summary>
/// A calibração guiada (história 13b; <c>telas-rendimento.md</c>, "Calibração guiada"; sem XAML, que é da 13g):
/// a escolha da saída e um passo por tela.
/// <list type="number">
/// <item>"Desligue os alto-falantes do módulo." e o Já desliguei;</item>
/// <item>"Multímetro em V~ 200 na saída …" e o Seguir (a operação começa: o app prepara a saída e o gerador toca);</item>
/// <item>o tom, com a contagem de 30 s e o "Tocar de novo";</item>
/// <item>o campo dos volts (vírgula ou ponto) e o Confirmar: o gerador para e o ganho vai para a ficha;</item>
/// <item>o celular a 100 % e o som pelo Bluetooth, e o Tocar o tom (só se o Setup não tem o nível medido, ou com o
/// "Medir o Bluetooth de novo");</item>
/// <item>o campo dos volts do tom do celular;</item>
/// </list>
/// e o fim: o som de antes de volta, o que foi gravado e "Religue os alto-falantes.". O Cancelar, depois do passo 2,
/// para o gerador e o tom e volta ao som de antes. A trava do "Tocar de novo" usa o
/// <see cref="CopiaAntes.MotivoBloqueioGerador"/>: a calibração deixa o gerador tocar no meio da operação dela.
/// </summary>
public sealed class CalibracaoViewModel : TelaBase, IDisposable
{
    public const string TextoCalibrada = "Calibrada";
    public const string TextoSemCalibracao = "Sem calibração";
    public const string TituloEscolha = "Calibrar uma saída";
    public const string TextoEscolha = "Escolha a saída. O app mede o ganho do módulo com o gerador e o multímetro, e o nível do Bluetooth.";
    public const string TextoJaDesliguei = "Já desliguei";
    public const string TextoSeguir = "Seguir";
    public const string TextoDigitar = "Digitar a leitura";
    public const string TextoTocarDeNovo = "Tocar de novo";
    public const string TextoConfirmar = "Confirmar";
    public const string TextoTocarTom = "Tocar o tom";
    public const string TextoMedirBluetoothDeNovo = "Medir o Bluetooth de novo";
    public const string TextoCancelar = "Cancelar";
    public const string TextoConcluir = "Concluir";
    public const string TextoUnidade = "V";

    /// <summary>O TalkBack do anel duplo enquanto um passo anda (história 13g).</summary>
    public const string DescricaoOcupado = "Calibrando a saída";

    /// <summary>O TalkBack do ‹ da tela da calibração.</summary>
    public const string DescricaoVoltar = "Voltar";

    /// <summary>A dica do campo dos volts.</summary>
    public const string DicaVolts = "Ex.: 12,4";

    /// <summary>A dica do TalkBack numa saída da escolha.</summary>
    public const string DicaOpcao = "Toque para calibrar esta saída.";

    /// <summary>"Termine a calibração da JBL L primeiro." (outra saída pedida com uma calibração aberta; história 13g).</summary>
    public static string MsgTermineAntes(string saida) => $"Termine a calibração da {saida} primeiro.";

    /// <summary>De quanto em quanto a contagem regressiva se atualiza.</summary>
    public static readonly TimeSpan PassoContagem = TimeSpan.FromMilliseconds(250);

    /// <summary>"Calibrar JBL L".</summary>
    public static string TituloDe(string saida) => $"Calibrar {saida}";

    /// <summary>"Passo 2 de 6".</summary>
    public static string TextoPasso(int passo, int total) => $"Passo {passo} de {total}";

    /// <summary>"O tom vai tocar em 400 Hz, a 0 dB, por até 30 s."</summary>
    public static string TextoTomPrevisto(int hz) => $"O tom vai tocar em {hz} Hz, a 0 dB, por até 30 s.";

    /// <summary>"O gerador toca 400 Hz. Leia a tensão no multímetro."</summary>
    public static string TextoTocando(int hz) => $"O gerador toca {hz} Hz. Leia a tensão no multímetro.";

    /// <summary>"Digite a tensão do tom de 400 Hz, em volts."</summary>
    public static string TextoDigite(int hz) => $"Digite a tensão do tom de {hz} Hz, em volts.";

    /// <summary>"O celular toca 400 Hz. Digite a tensão lida, em volts."</summary>
    public static string TextoCelularTocando(int hz) => $"O celular toca {hz} Hz. Digite a tensão lida, em volts.";

    /// <summary>"O Bluetooth já está medido: −13,0 dB."</summary>
    public static string TextoBluetoothMedido(decimal nivel) => $"O Bluetooth já está medido: {LimiteFisico.Db(nivel)} dB.";

    private readonly Calibracao _calibracao;
    private readonly GeradorSinal _gerador;
    private readonly GerenteSetups _setups;
    private readonly Sessao _sessao;
    private readonly IChaves _chaves;
    private readonly EstadoAparelho _estado;
    private readonly CopiaAntes _copia;
    private readonly IAudioDoCelular _audio;

    private IReadOnlyList<OpcaoSaidaCalibracao> _saidas = [];
    private string _textoVolts = "";
    private string? _erroVolts;
    private string? _mensagem;
    private EtapaCalibracao _etapaVista;
    private ITimer? _contagem;
    private bool _descartado;

    public CalibracaoViewModel(
        Calibracao calibracao,
        GeradorSinal gerador,
        GerenteSetups setups,
        Sessao sessao,
        IChaves chaves,
        EstadoAparelho estado,
        CopiaAntes copia,
        IAudioDoCelular audio,
        IDespachante despachante,
        TimeProvider relogio)
        : base(despachante, relogio)
    {
        _calibracao = calibracao;
        _gerador = gerador;
        _setups = setups;
        _sessao = sessao;
        _chaves = chaves;
        _estado = estado;
        _copia = copia;
        _audio = audio;
        _etapaVista = calibracao.Etapa;

        Escolher = new ComandoCom<int>(EscolherAsync);
        JaDesliguei = new Comando(() => Mostrar(_calibracao.ConfirmarDesligado()));
        Seguir = new Comando(async () => Mostrar(await _calibracao.SeguirAsync()));
        Digitar = new Comando(() => Mostrar(_calibracao.IrParaLeitura()));
        TocarDeNovo = new Comando(async () => Mostrar(await _calibracao.TocarDeNovoAsync()));
        Confirmar = new Comando(ConfirmarAsync);
        TocarTom = new Comando(async () => Mostrar(await _calibracao.TocarBluetoothAsync(_audio.SaindoPeloBluetooth)));
        MedirBluetoothDeNovo = new Comando(() => Mostrar(_calibracao.MedirBluetoothDeNovo()));
        Cancelar = new Comando(async () => Mostrar(await _calibracao.CancelarAsync()));
        Concluir = new Comando(() => Mostrar(_calibracao.Concluir()));

        _calibracao.Mudou += AoMudar;
        _gerador.Mudou += AoMudar;
        _sessao.Mudou += AoMudar;
        _setups.Mudou += AoMudar;
        _copia.Mudou += AoMudar;
        _estado.Recarregou += AoMudar;
        _estado.Mudou += AoMudarValor;
        _chaves.Mudou += AoMudarChave;
        _audio.Mudou += AoMudar;
        Atualizar();
    }

    // ------------------------------------------------------------------ escolha

    /// <summary>As saídas do Setup em uso, com a situação da calibração e o motivo de não calibrar.</summary>
    public IReadOnlyList<OpcaoSaidaCalibracao> Saidas => _saidas;

    /// <summary>Por que nenhuma saída se calibra agora (sem conexão, chave do gerador, módulo desligado…), ou null.</summary>
    public string? MotivoTrava => _calibracao.Etapa == EtapaCalibracao.Fechada ? _calibracao.MotivoParaComecar() : null;

    /// <summary>Abre a calibração da saída (pelo canal mestre).</summary>
    public ComandoCom<int> Escolher { get; }

    // ------------------------------------------------------------------ o passo

    public EtapaCalibracao Etapa => _calibracao.Etapa;

    /// <summary>Uma calibração aberta (um passo por tela).</summary>
    public bool Aberta => Etapa != EtapaCalibracao.Fechada;

    /// <summary>"Calibrar JBL L", ou o título da escolha.</summary>
    public string Titulo => _calibracao.Alvo is { } alvo ? TituloDe(alvo.Nome) : TituloEscolha;

    /// <summary>"Passo 2 de 6" (4 passos quando o Bluetooth já está medido), ou null na escolha e no fim.</summary>
    public string? Passo
    {
        get
        {
            var n = Etapa switch
            {
                EtapaCalibracao.Desligar => 1,
                EtapaCalibracao.Multimetro => 2,
                EtapaCalibracao.Tom => 3,
                EtapaCalibracao.DigitarTom => 4,
                EtapaCalibracao.Bluetooth => 5,
                EtapaCalibracao.DigitarBluetooth => 6,
                _ => 0,
            };
            return n == 0 ? null : TextoPasso(n, MedeBluetooth ? 6 : 4);
        }
    }

    /// <summary>Os passos 5 e 6 entram: o Setup não tem o nível medido, o usuário pediu, ou eles já andaram.</summary>
    private bool MedeBluetooth =>
        _calibracao.BluetoothMedido is null || _calibracao.MedirBluetooth || _calibracao.NivelMedido is not null
        || Etapa is EtapaCalibracao.Bluetooth or EtapaCalibracao.DigitarBluetooth;

    /// <summary>A instrução do passo.</summary>
    public string Instrucao
    {
        get
        {
            var hz = _calibracao.Frequencia ?? 0;
            return Etapa switch
            {
                EtapaCalibracao.Desligar => Calibracao.MsgDesligar,
                EtapaCalibracao.Multimetro when _calibracao.Alvo is { } alvo => Calibracao.MsgMultimetro(alvo),
                EtapaCalibracao.Tom => TextoTocando(hz),
                EtapaCalibracao.DigitarTom => TextoDigite(hz),
                EtapaCalibracao.Bluetooth => Calibracao.MsgPedeBluetooth,
                EtapaCalibracao.DigitarBluetooth => TextoCelularTocando(hz),
                EtapaCalibracao.Fim => _calibracao.Mensagem ?? "",
                _ => TextoEscolha,
            };
        }
    }

    /// <summary>A linha de apoio: o tom previsto (passo 2) e "Religue os alto-falantes." no fim; senão, null.</summary>
    public string? Detalhe => Etapa switch
    {
        EtapaCalibracao.Multimetro when _calibracao.Frequencia is { } hz => TextoTomPrevisto(hz),
        EtapaCalibracao.Fim when !_calibracao.Ocupado => Calibracao.MsgReligue,
        _ => null,
    };

    /// <summary>A frase do núcleo (uma recusa, "Confira a leitura…", o fim) ou do campo, ou null.</summary>
    public string? Mensagem
    {
        get => _mensagem;
        private set
        {
            if (_mensagem == value)
                return;
            _mensagem = value;
            Avisar();
            Avisar(nameof(MensagemNaTela));
        }
    }

    /// <summary>A <see cref="Mensagem"/> sem repetir a instrução (no fim, a frase do núcleo já é a instrução; história 13g).</summary>
    public string? MensagemNaTela => Mensagem == Instrucao ? null : Mensagem;

    /// <summary>Um passo anda (preparando a saída, gravando, voltando ao som de antes): o anel duplo.</summary>
    public bool Ocupado => _calibracao.Ocupado;

    // ------------------------------------------------------------------ o campo dos volts

    /// <summary>Os passos com o campo dos volts (4 e 6).</summary>
    public bool PedeVolts => Etapa is EtapaCalibracao.DigitarTom or EtapaCalibracao.DigitarBluetooth;

    /// <summary>Os volts lidos no multímetro, como escritos (vírgula ou ponto).</summary>
    public string TextoVolts
    {
        get => _textoVolts;
        set
        {
            value ??= "";
            if (_textoVolts == value)
                return;
            _textoVolts = value;
            ErroVolts = null;
            Avisar();
        }
    }

    /// <summary>"Escreva um número." ou a faixa, ou null.</summary>
    public string? ErroVolts
    {
        get => _erroVolts;
        private set
        {
            if (_erroVolts == value)
                return;
            _erroVolts = value;
            Avisar();
        }
    }

    // ------------------------------------------------------------------ o tom e o Bluetooth

    /// <summary>O gerador toca (passos 3 e 4).</summary>
    public bool Tocando => Etapa is EtapaCalibracao.Tom or EtapaCalibracao.DigitarTom && _gerador.Estado == EstadoGerador.Tocando;

    /// <summary>Os segundos que faltam para o gerador desligar sozinho, ou null.</summary>
    public int? Contagem => Tocando && _gerador.Restante is { } falta ? (int)Math.Ceiling(falta.TotalSeconds) : null;

    /// <summary>"30 s", ou null.</summary>
    public string? TextoContagem => Contagem is { } s ? $"{s} s" : null;

    /// <summary>
    /// O "Tocar de novo": nos passos 3 e 4, com o gerador parado (e a cópia deixando o gerador tocar); no passo 6,
    /// o tom do celular.
    /// </summary>
    public bool PodeTocarDeNovo => !Ocupado && Etapa switch
    {
        EtapaCalibracao.Tom or EtapaCalibracao.DigitarTom =>
            _gerador.Estado == EstadoGerador.Desligado && !_gerador.TalvezLigado && _copia.MotivoBloqueioGerador() is null,
        EtapaCalibracao.DigitarBluetooth => true,
        _ => false,
    };

    /// <summary>"O Bluetooth já está medido: −13,0 dB." enquanto o "Medir o Bluetooth de novo" vale, ou null.</summary>
    public string? AvisoBluetoothMedido =>
        PodeMedirBluetoothDeNovo && _calibracao.BluetoothMedido is { } n ? TextoBluetoothMedido(n.Valor) : null;

    /// <summary>O Setup tem o nível medido e o ganho ainda não foi gravado: o usuário pode pedir os passos 5 e 6.</summary>
    public bool PodeMedirBluetoothDeNovo =>
        _calibracao.BluetoothMedido is not null && !_calibracao.MedirBluetooth && _calibracao.GanhoMedido is null
        && Etapa is EtapaCalibracao.Desligar or EtapaCalibracao.Multimetro or EtapaCalibracao.Tom or EtapaCalibracao.DigitarTom;

    /// <summary>No passo 5: "O som do celular não está saindo pelo Bluetooth.", ou null.</summary>
    public string? AvisoSaidaDoSom =>
        Etapa == EtapaCalibracao.Bluetooth && _audio.SaindoPeloBluetooth != true ? Calibracao.MsgForaDoBluetooth : null;

    // ------------------------------------------------------------------ os botões

    public Comando JaDesliguei { get; }

    public Comando Seguir { get; }

    public Comando Digitar { get; }

    public Comando TocarDeNovo { get; }

    /// <summary>Confirma os volts (passos 4 e 6).</summary>
    public Comando Confirmar { get; }

    /// <summary>Passo 5: o tom do celular.</summary>
    public Comando TocarTom { get; }

    public Comando MedirBluetoothDeNovo { get; }

    /// <summary>Volta ao som de antes (depois do passo 2) ou só fecha (antes).</summary>
    public Comando Cancelar { get; }

    /// <summary>No fim: fecha a calibração.</summary>
    public Comando Concluir { get; }

    /// <summary>A escolha da saída (nenhuma calibração aberta): a lista aparece.</summary>
    public bool NaEscolha => !Aberta;

    /// <summary>O passo 1 ("Já desliguei").</summary>
    public bool NoDesligar => Etapa == EtapaCalibracao.Desligar;

    /// <summary>O passo 2 ("Seguir").</summary>
    public bool NoMultimetro => Etapa == EtapaCalibracao.Multimetro;

    /// <summary>O passo 3, com o tom: "Digitar a leitura".</summary>
    public bool NoTom => Etapa == EtapaCalibracao.Tom;

    /// <summary>O passo 5 ("Tocar o tom").</summary>
    public bool NoBluetooth => Etapa == EtapaCalibracao.Bluetooth;

    /// <summary>O fim ("Concluir").</summary>
    public bool NoFim => Etapa == EtapaCalibracao.Fim;

    /// <summary>O Cancelar aparece em todo passo aberto, menos no fim.</summary>
    public bool PodeCancelar => Aberta && Etapa != EtapaCalibracao.Fim;

    // ------------------------------------------------------------------ ações

    /// <summary>
    /// O "Calibrar ›" de uma saída (história 13g): abre a calibração dela no passo 1 e devolve true. Com a calibração dela
    /// já aberta, true sem mexer. Com a de outra saída aberta, não troca: mostra "Termine a calibração da … primeiro." e
    /// devolve false. Uma recusa ao abrir fica na <see cref="Mensagem"/>, e devolve false.
    /// </summary>
    public bool AbrirPara(int canal)
    {
        if (_calibracao.Alvo is { } alvo && Aberta)
        {
            if (alvo.Saida.Canais.Contains(canal))
                return true;
            Mensagem = MsgTermineAntes(alvo.Nome);
            return false;
        }
        TextoVolts = "";
        var r = _calibracao.Abrir(canal);
        Mensagem = r.Ok ? r.Mensagem : r.Mensagem ?? r.Motivo;
        Atualizar();
        return r.Ok && Aberta;
    }

    private Task EscolherAsync(int canal)
    {
        TextoVolts = "";
        Mostrar(_calibracao.Abrir(canal));
        return Task.CompletedTask;
    }

    private async Task ConfirmarAsync()
    {
        if (!PedeVolts)
            return;
        if (FormatoValor.Ler(TextoVolts) is not { } volts)
        {
            ErroVolts = ControleParametroViewModel.MsgNaoNumero;
            Mensagem = ErroVolts;
            return;
        }
        var r = Etapa == EtapaCalibracao.DigitarTom
            ? await _calibracao.InformarTomAsync(volts)
            : await _calibracao.InformarBluetoothAsync(volts);
        Mostrar(r);
    }

    /// <summary>A frase do resultado (a recusa, a leitura a conferir, o fim) vai para a tela.</summary>
    private void Mostrar(ResultadoCalibracao r) => NaTela(() =>
    {
        Mensagem = r.Ok ? r.Mensagem : r.Mensagem ?? r.Motivo;
        Atualizar();
    });

    // ------------------------------------------------------------------ atualização

    private void AoMudar() => NaTela(Atualizar);

    private void AoMudarChave(Chave _) => NaTela(Atualizar);

    private void AoMudarValor(Endereco e)
    {
        if (e.Codigo is "252" or "040" or "041" or "042" or "043")
            NaTela(Atualizar);
    }

    private void Atualizar()
    {
        if (_descartado)
            return;
        var etapa = Etapa;
        if (etapa != _etapaVista)
        {
            // Um passo novo começa limpo: o campo, o erro e a frase do passo anterior saem (a do núcleo fica).
            _etapaVista = etapa;
            if (etapa is EtapaCalibracao.DigitarTom or EtapaCalibracao.DigitarBluetooth or EtapaCalibracao.Bluetooth)
                _textoVolts = "";
            _erroVolts = null;
            Mensagem = _calibracao.Mensagem;
            Avisar(nameof(TextoVolts));
            Avisar(nameof(ErroVolts));
        }
        else if (_calibracao.Mensagem is { } frase)
        {
            Mensagem = frase;
        }
        var saidas = _calibracao.Opcoes()
            .Select(o => new OpcaoSaidaCalibracao(o.Saida.Mestre, o.Nome, o.Saida.Rotulo, o.Calibrada, o.Motivo))
            .ToArray();
        if (!saidas.SequenceEqual(_saidas))
        {
            _saidas = saidas;
            Avisar(nameof(Saidas));
        }
        AjustarContagem();
        foreach (var nome in new[]
                 {
                     nameof(MotivoTrava), nameof(Etapa), nameof(Aberta), nameof(Titulo), nameof(Passo), nameof(Instrucao), nameof(Detalhe),
                     nameof(Ocupado), nameof(PedeVolts), nameof(Tocando), nameof(Contagem), nameof(TextoContagem), nameof(PodeTocarDeNovo),
                     nameof(AvisoBluetoothMedido), nameof(PodeMedirBluetoothDeNovo), nameof(AvisoSaidaDoSom), nameof(PodeCancelar),
                     nameof(NaEscolha), nameof(NoDesligar), nameof(NoMultimetro), nameof(NoTom), nameof(NoBluetooth), nameof(NoFim),
                     nameof(MensagemNaTela),
                 })
            Avisar(nome);
    }

    /// <summary>Tocando, a contagem anda sozinha; parado, o relógio dela para.</summary>
    private void AjustarContagem()
    {
        if (Tocando)
        {
            _contagem ??= Relogio.CreateTimer(_ => NaTela(AvisarContagem), null, PassoContagem, PassoContagem);
            return;
        }
        _contagem?.Dispose();
        _contagem = null;
    }

    private void AvisarContagem()
    {
        if (_descartado)
            return;
        Avisar(nameof(Contagem));
        Avisar(nameof(TextoContagem));
        if (!Tocando)
            Atualizar();
    }

    public void Dispose()
    {
        _descartado = true;
        _contagem?.Dispose();
        _contagem = null;
        _calibracao.Mudou -= AoMudar;
        _gerador.Mudou -= AoMudar;
        _sessao.Mudou -= AoMudar;
        _setups.Mudou -= AoMudar;
        _copia.Mudou -= AoMudar;
        _estado.Recarregou -= AoMudar;
        _estado.Mudou -= AoMudarValor;
        _chaves.Mudou -= AoMudarChave;
        _audio.Mudou -= AoMudar;
    }
}
