using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using BgsControle.Nucleo;
using BgsControle.Nucleo.Enlace;
using BgsControle.Nucleo.Protocolo;

namespace BgsControle.Simulador;

/// <summary>
/// O PRO 2.4BT + AUDIO simulado, para os testes (nunca no APK). Imita o formato real medido
/// (MAPA §2 e §9): leituras <c>#CCC#[c#]v#\r\n</c>, banda <c>020#b#v</c>, nomes <c>23N#NOME</c>,
/// voltímetro <c>ERRO 0</c>, senha <c>#000#PRO2.4MIR003#</c> ou <c>#000#ERRO#</c>, e a escrita
/// respondida pelo eco no formato de leitura depois de ~85 ms, sem <c>OK</c>.
/// <para>
/// O estado é um dicionário (código, índice) → valor no fio, semeado com as respostas cruas
/// reais de <c>estado/leituras/2026-10-18_2145.json</c> e com os extras reais da história 2.
/// O que não foi medido no aparelho está marcado com <c>SUPOSIÇÃO</c> e listado em
/// <c>SUPOSICOES.md</c> (conferido na história 12).
/// </para>
/// </summary>
public sealed partial class ProcessadorSimulado : ITransporte
{
    /// <summary>O endereço do aparelho real (leitura de 18/10).</summary>
    public const string EnderecoReal = "00:80:E1:22:B6:30";

    public const string ProdutoReal = "PRO2.4MI";
    public const string FirmwareReal = "R003";

    /// <summary>O que chega quando se pergunta o voltímetro <c>#005#</c> (história 2, 20/10).</summary>
    public const string RespostaVoltimetro = "ERRO 0";

    /// <summary>Memórias do DSP: <c>*240*n*</c> e <c>*241*n*</c> com n de 0 a 2.</summary>
    public const int Memorias = 3;

    /// <summary>O processador derruba a conexão uns 29 s depois de um <c>ERRO</c> (medido em 20/10).</summary>
    public static readonly TimeSpan QuedaAposErroReal = TimeSpan.FromSeconds(29);

    /// <summary>O eco de uma escrita chega ~85 ms depois (medido em 16/10, MAPA §9).</summary>
    public static readonly TimeSpan AtrasoEcoReal = TimeSpan.FromMilliseconds(85);

    /// <summary>
    /// O ack <c>#240#n#</c> do salvar chega ~1,17 s depois do <c>*240*n*</c> (medido em 20/10:
    /// <c>estado/log-ble/2026-10-20_1322-memoria.log</c>, linhas 174–181).
    /// </summary>
    public static readonly TimeSpan AtrasoAckSalvarReal = TimeSpan.FromMilliseconds(1170);

    /// <summary>Os extras lidos no aparelho real na história 2 (20/10), como chegaram.</summary>
    public static IReadOnlyList<string> ExtrasReais { get; } =
    [
        "231#SPL", "232#MUSICA ORIG", "233#FILME",
        "#111#0#", "#150#0#",
        "#200#-25.0#", "#201#51#", "#202#0#",
        "#210#10#", "#211#22000#", "#212#-45.0#", "#213#0#",
        "#251#1#", "#252#1#",
    ];

    private const string RecursoSemente = "leitura-2026-10-18_2145.json";
    private const string CodigoVoltimetro = "005";
    private const string FimDeLinha = "\r\n";
    private static readonly CultureInfo Fio = CultureInfo.InvariantCulture;

    private static readonly Lazy<IReadOnlyDictionary<(string Codigo, int? Indice), string>> SementeCarregada = new(LerSemente);

    private readonly object _trava = new();
    private readonly object _emissao = new();
    private readonly TimeProvider _relogio;
    private readonly Dictionary<(string Codigo, int? Indice), string> _estado;
    private readonly Dictionary<(string Codigo, int? Indice), string>[] _memorias = new Dictionary<(string, int?), string>[Memorias];
    private readonly List<string> _escritas = [];
    private readonly List<(string Quadro, string Motivo)> _recusadas = [];
    private readonly List<string> _notificacoes = [];
    private readonly Random _sorteio = new(2026);

    private bool _conectado;
    private bool _autenticado;
    private int _geracao;
    private ITimer? _quedaAposErro;
    private Task _ultimaAtrasada = Task.CompletedTask; // as respostas com atraso saem em fila

    /// <param name="relogio">O relógio dos atrasos e da queda depois do ERRO (o sistema, se null).</param>
    public ProcessadorSimulado(TimeProvider? relogio = null)
    {
        _relogio = relogio ?? TimeProvider.System;
        _estado = new Dictionary<(string, int?), string>(Semente);
        // SUPOSIÇÃO: o conteúdo das 3 memórias não foi lido; começam iguais ao estado semeado.
        for (var n = 0; n < Memorias; n++)
            _memorias[n] = CopiaMarco1(_estado);
    }

    /// <summary>A semente: as 84 respostas reais do Marco 1 e os extras reais.</summary>
    public static IReadOnlyDictionary<(string Codigo, int? Indice), string> Semente => SementeCarregada.Value;

    // ------------------------------------------------------------------ configuração

    /// <summary>A senha do aparelho (4 caracteres [0-9A-Z]).</summary>
    public string Senha { get; set; } = "AB12";

    public string Produto { get; set; } = ProdutoReal;
    public string Firmware { get; set; } = FirmwareReal;

    /// <summary>A senha depois do reset de fábrica <c>*004*</c>.</summary>
    // SUPOSIÇÃO: a senha de fábrica não foi medida.
    public string SenhaDeFabrica { get; set; } = "1234";

    /// <summary>Depois de um <c>ERRO</c>, a conexão cai sozinha neste prazo (29 s medidos).</summary>
    public TimeSpan PrazoQuedaAposErro { get; set; } = QuedaAposErroReal;

    /// <summary>Atraso do eco de uma escrita (~85 ms medidos).</summary>
    public TimeSpan AtrasoEco { get; set; } = AtrasoEcoReal;

    /// <summary>
    /// Atraso do ack de <c>*240*n*</c> (~1,17 s medidos) e de <c>*241*n*</c> (SUPOSIÇÃO: o mesmo).
    /// </summary>
    public TimeSpan AtrasoAckMemoria { get; set; } = AtrasoAckSalvarReal;

    /// <summary>Atraso da resposta a uma leitura e à senha. Zero = responde dentro da escrita.</summary>
    public TimeSpan AtrasoLeitura { get; set; } = TimeSpan.Zero;

    /// <summary>O endereço anunciado. Null = o aparelho não aparece no scan.</summary>
    public string? Endereco { get; set; } = EnderecoReal;

    /// <summary>Exceção lançada pelo scan (Bluetooth desligado, sem permissão).</summary>
    public Exception? ErroAoProcurar { get; set; }

    /// <summary>Quantas das próximas tentativas de conexão falham (status 133).</summary>
    public int FalhasDeConexao { get; set; }

    // ------------------------------------------------------------------ falhas injetáveis

    /// <summary>A conexão cai ao receber a N-ésima escrita (1 = a primeira de <see cref="Escritas"/>, a senha).</summary>
    public int? CairNaEscrita { get; set; }

    /// <summary>
    /// A conexão cai logo depois de aplicar a N-ésima escrita, sem responder a ela (o aparelho
    /// fez, mas o app não soube).
    /// </summary>
    public int? CairDepoisDaEscrita { get; set; }

    /// <summary>Não responde a nada, nem à senha.</summary>
    public bool Mudo { get; set; }

    /// <summary>Quadros sem resposta (a escrita é aplicada, mas o eco não chega).</summary>
    public HashSet<string> Silenciar { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Quadro de escrita → o eco que chega no lugar do eco certo (sem <c>\r\n</c>). O estado
    /// fica com o valor pedido.
    /// </summary>
    public Dictionary<string, string> EcosDiferentes { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Quebra cada mensagem em notificações de 1 a N bytes (sorteio com semente fixa). Null =
    /// uma notificação por mensagem, como no aparelho real.
    /// </summary>
    public int? TamanhoMaximoFragmento { get; set; }

    /// <summary>Um quadro órfão (sem <c>\r\n</c>) enviado antes de cada resposta.</summary>
    public string? OrfaoAntesDeCadaResposta { get; set; }

    // ------------------------------------------------------------------ observação

    /// <summary>Tudo o que chegou pela 1122, na ordem.</summary>
    public IReadOnlyList<string> Escritas
    {
        get
        {
            lock (_trava)
                return _escritas.ToArray();
        }
    }

    /// <summary>O que chegou e não foi aceito, com o motivo.</summary>
    public IReadOnlyList<(string Quadro, string Motivo)> Recusadas
    {
        get
        {
            lock (_trava)
                return _recusadas.ToArray();
        }
    }

    /// <summary>As mensagens enviadas pela 1123 (antes de quebrar em fragmentos).</summary>
    public IReadOnlyList<string> Notificacoes
    {
        get
        {
            lock (_trava)
                return _notificacoes.ToArray();
        }
    }

    public bool Conectado
    {
        get
        {
            lock (_trava)
                return _conectado;
        }
    }

    public bool Autenticado
    {
        get
        {
            lock (_trava)
                return _autenticado;
        }
    }

    public int Procuras { get; private set; }
    public int Conexoes { get; private set; }
    public int Desconexoes { get; private set; }

    /// <summary>Quedas sem pedido (o evento <see cref="Caiu"/>).</summary>
    public int Quedas { get; private set; }

    /// <summary>O valor no fio guardado, ou null.</summary>
    public string? Valor(string codigo, int? indice = null)
    {
        lock (_trava)
            return _estado.GetValueOrDefault((codigo, indice));
    }

    /// <summary>Muda o estado direto (sem eco), para preparar um teste.</summary>
    public void Definir(string codigo, int? indice, string noFio)
    {
        lock (_trava)
            _estado[(codigo, indice)] = noFio;
    }

    /// <summary>Muda um valor guardado na memória n (0 a 2), para preparar um teste.</summary>
    public void DefinirMemoria(int n, string codigo, int? indice, string noFio)
    {
        lock (_trava)
            _memorias[n][(codigo, indice)] = noFio;
    }

    /// <summary>Os 84 valores do Marco 1 do estado atual, como uma memória os guardaria.</summary>
    public IReadOnlyDictionary<(string Codigo, int? Indice), string> EstadoMarco1()
    {
        lock (_trava)
            return CopiaMarco1(_estado);
    }

    /// <summary>Uma cópia do que está guardado na memória n (0 a 2).</summary>
    public IReadOnlyDictionary<(string Codigo, int? Indice), string> Memoria(int n)
    {
        lock (_trava)
            return new Dictionary<(string, int?), string>(_memorias[n]);
    }

    // ------------------------------------------------------------------ ITransporte

    public event Action<byte[]>? Notificacao;
    public event Action? Caiu;

    public Task<string?> ProcurarAsync(TimeSpan duracao, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        lock (_trava)
            Procuras++;
        if (ErroAoProcurar is not null)
            throw ErroAoProcurar;
        return Task.FromResult(Endereco);
    }

    public Task ConectarAsync(string dispositivo, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        lock (_trava)
        {
            Conexoes++;
            if (FalhasDeConexao > 0)
            {
                FalhasDeConexao--;
                throw new FalhaConexaoException("conexão: status 133");
            }
            if (dispositivo != Endereco)
                throw new FalhaConexaoException("endereço desconhecido");
            Soltar();
            _conectado = true;
        }
        return Task.CompletedTask;
    }

    public Task EscreverAsync(byte[] dados, CancellationToken ct)
    {
        var texto = Encoding.ASCII.GetString(dados);
        List<(string Mensagem, TimeSpan Atraso)> respostas;
        bool derrubar;
        int geracao;
        lock (_trava)
        {
            if (!_conectado)
                throw new InvalidOperationException("sem conexão");
            _escritas.Add(texto);
            geracao = _geracao;
            derrubar = CairNaEscrita == _escritas.Count;
            respostas = derrubar ? [] : Processar(texto);
            if (CairDepoisDaEscrita == _escritas.Count)
            {
                derrubar = true;
                respostas = [];
            }
        }

        if (derrubar)
        {
            Derrubar();
            return Task.CompletedTask;
        }
        foreach (var (mensagem, atraso) in respostas)
            Enviar(geracao, mensagem, atraso);
        return Task.CompletedTask;
    }

    public Task DesconectarAsync()
    {
        lock (_trava)
        {
            Desconexoes++;
            Soltar();
        }
        return Task.CompletedTask;
    }

    // ------------------------------------------------------------------ ações dos testes

    /// <summary>Derruba a conexão sem pedido (dispara <see cref="Caiu"/>).</summary>
    public void Derrubar()
    {
        lock (_trava)
        {
            if (!_conectado)
                return;
            Soltar();
            Quedas++;
        }
        Caiu?.Invoke();
    }

    /// <summary>Envia um texto cru pela notificação (inclua o <c>\r\n</c> se quiser).</summary>
    public void Emitir(string texto)
    {
        int geracao;
        lock (_trava)
            geracao = _geracao;
        EmitirAgora(geracao, texto);
    }

    /// <summary>O clip espontâneo das saídas (<c>#150#bits#</c>, um bit por saída), quando o teste quiser.</summary>
    // SUPOSIÇÃO: o formato do clip espontâneo é o da leitura; o período e o gatilho não foram medidos.
    public void EmitirClip(int bitsSaidas)
    {
        string mensagem;
        lock (_trava)
        {
            _estado[("150", null)] = bitsSaidas.ToString(Fio);
            mensagem = $"#150#{bitsSaidas.ToString(Fio)}#";
        }
        Emitir(mensagem + FimDeLinha);
    }

    // ------------------------------------------------------------------ o aparelho

    /// <summary>Interpreta um quadro e devolve as respostas. Chamar com a trava.</summary>
    private List<(string, TimeSpan)> Processar(string texto)
    {
        var respostas = new List<(string, TimeSpan)>();
        var silencio = Mudo || Silenciar.Contains(texto);

        void Responder(string mensagem, TimeSpan atraso)
        {
            if (!silencio)
                respostas.Add((mensagem, atraso));
        }

        // Senha.
        if (QuadroSenha().Match(texto) is { Success: true } senha)
        {
            if (senha.Groups[1].Value == Senha)
            {
                _autenticado = true;
                // SUPOSIÇÃO: a senha certa dentro da janela cancela a queda depois do ERRO.
                CancelarQuedaAposErro();
                Responder($"#000#{Produto}{Firmware}#", AtrasoLeitura);
            }
            else
            {
                _autenticado = false;
                // SUPOSIÇÃO: um segundo ERRO não reinicia o prazo da queda (conta desde o primeiro).
                if (_quedaAposErro is null)
                {
                    var geracao = _geracao;
                    _quedaAposErro = _relogio.CreateTimer(_ => CairAposErro(geracao), null,
                        PrazoQuedaAposErro, Timeout.InfiniteTimeSpan);
                }
                Responder("#000#ERRO#", AtrasoLeitura);
            }
            return respostas;
        }

        // SUPOSIÇÃO: antes da senha aceita, o aparelho ignora tudo, sem resposta.
        if (!_autenticado)
        {
            Recusar(texto, "antes da senha");
            return respostas;
        }

        // Leitura de uma consulta do catálogo.
        if (Catalogo.ConsultaDe(texto) is { } consulta)
        {
            var leitura = consulta.Parametro.Codigo == CodigoVoltimetro
                ? RespostaVoltimetro // SUPOSIÇÃO: o "ERRO 0" chega com \r\n, como as outras respostas.
                : LeituraDe(consulta.Parametro, consulta.Indice);
            if (leitura is null)
                Recusar(texto, "sem valor guardado");
            else
                Responder(leitura, AtrasoLeitura);
            return respostas;
        }

        // Comandos do aparelho inteiro (MAPA §5).
        if (QuadroMemoria().Match(texto) is { Success: true } memoria)
        {
            var n = int.Parse(memoria.Groups[2].Value, Fio);
            if (memoria.Groups[1].Value == "240")
            {
                // SUPOSIÇÃO: a memória guarda os 84 valores do Marco 1 (sem gerador, nomes, 251, 252).
                _memorias[n] = CopiaMarco1(_estado);
            }
            else
            {
                // SUPOSIÇÃO: carregar troca os 84 valores do Marco 1 e não mexe nos nomes 23N.
                foreach (var (chave, valor) in _memorias[n])
                    _estado[chave] = valor;
            }
            // Medido (20/10, 2026-10-20_1322-memoria.log): o 240 responde #240#n#\r\n ~1,17 s depois.
            // SUPOSIÇÃO: o 241 responde igual, #241#n#, no mesmo prazo (não medido).
            Responder($"#{memoria.Groups[1].Value}#{n}#", AtrasoAckMemoria);
            return respostas;
        }

        if (texto == "*242*")
        {
            // SUPOSIÇÃO: o reset de áudio volta o Marco 1 ao estado de fábrica (Fabrica) e
            // mantém as memórias, os nomes, o gerador, o 251 e o 252.
            foreach (var (chave, valor) in Fabrica)
                _estado[chave] = valor;
            // SUPOSIÇÃO: o ack é #242#.
            Responder("#242#", AtrasoEco);
            return respostas;
        }

        if (texto == "*004*")
        {
            // SUPOSIÇÃO: o reset de fábrica volta o Marco 1 e as 3 memórias à fábrica, apaga os
            // nomes e volta a senha à SenhaDeFabrica. A conexão continua.
            foreach (var (chave, valor) in Fabrica)
                _estado[chave] = valor;
            for (var m = 0; m < Memorias; m++)
            {
                _memorias[m] = new Dictionary<(string, int?), string>(Fabrica);
                _estado[($"23{m + 1}", null)] = "";
            }
            Senha = SenhaDeFabrica;
            // SUPOSIÇÃO: o ack é #004#.
            Responder("#004#", AtrasoEco);
            return respostas;
        }

        if (TrocaSenha().Match(texto) is { Success: true } troca)
        {
            var aceita = troca.Groups[1].Value == Senha && SenhaValida().IsMatch(troca.Groups[2].Value);
            if (aceita)
                Senha = troca.Groups[2].Value;
            else
                Recusar("*001*••••*••••*", "senha antiga errada ou nova fora do formato");
            // SUPOSIÇÃO: o ack é #001#OK# ou #001#ERRO#, como a resposta da senha.
            Responder(aceita ? "#001#OK#" : "#001#ERRO#", AtrasoEco);
            return respostas;
        }

        // Escrita de parâmetro do catálogo (inclui gerador, nomes 23N, 251 e 252).
        var (parametro, indice, noFio, motivo) = ValidarEscrita(texto);
        if (motivo is not null)
        {
            Recusar(texto, motivo);
            return respostas;
        }
        _estado[(parametro!.Codigo, indice)] = noFio!;
        if (EcosDiferentes.TryGetValue(texto, out var diferente))
        {
            Responder(diferente, AtrasoEco);
            return respostas;
        }
        // Medido (20/10, 2026-10-20_1322-memoria.log): o nome *23N*NOME* é aceito sem eco; só a
        // releitura responde (23N#NOME).
        if (parametro.Tipo == TipoValor.Nome)
            return respostas;
        // SUPOSIÇÃO: o 251 e o 252 também respondem com o eco no formato de leitura; medido só no 081.
        Responder(LeituraDe(parametro, indice)!, AtrasoEco);
        return respostas;
    }

    /// <summary>
    /// Valida uma escrita pelo <see cref="Catalogo"/>: código conhecido e não só leitura, escopo e
    /// índice certos, valor na faixa e o texto idêntico ao que o codificador do app monta.
    /// </summary>
    private static (Parametro? Parametro, int? Indice, string? NoFio, string? Motivo) ValidarEscrita(string texto)
    {
        var q = Quadros.Analisar(texto);
        if (q is null || Catalogo.ParametroDe(q.Codigo) is not { } p)
            return (null, null, null, "comando desconhecido");
        if (p.SomenteLeitura)
            return (null, null, null, $"o {p.Codigo} só tem leitura");

        var segmentos = p.Escopo == Escopo.Global ? 1 : 2;
        if (q.Segmentos.Count != segmentos)
            return (null, null, null, "número de segmentos errado");
        int? indice = null;
        if (segmentos == 2)
        {
            if (!int.TryParse(q.Segmentos[0], NumberStyles.None, Fio, out var i))
                return (null, null, null, "índice ilegível");
            indice = i;
        }
        var valor = q.Segmentos[^1];

        string codificado;
        string noFio;
        try
        {
            if (p.Tipo is TipoValor.Roteamento or TipoValor.Nome)
            {
                codificado = p.CodificarEscrita(indice, valor);
                noFio = valor;
            }
            else
            {
                if (!decimal.TryParse(valor, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, Fio, out var numero))
                    return (null, null, null, "valor ilegível");
                codificado = p.CodificarEscrita(indice, numero);
                noFio = Formatar(p, numero);
            }
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException)
        {
            return (null, null, null, e.Message);
        }

        // SUPOSIÇÃO: o simulador é estrito e só aceita o texto do codificador do app; o aparelho
        // pode aceitar variações (-3 no lugar de -3.0, espaços), o que não foi medido.
        if (codificado != texto)
            return (null, null, null, $"fora do formato do codificador ({codificado})");
        return (p, indice, noFio, null);
    }

    /// <summary>
    /// O valor como o aparelho devolve na leitura: dB e Q com sinal (<c>+0.0</c>, <c>-3.0</c>,
    /// <c>+0.7</c>), cm e attack sem sinal (<c>100.0</c>), o resto inteiro.
    /// </summary>
    private static string Formatar(Parametro p, decimal v)
    {
        // SUPOSIÇÃO: o valor escrito volta com a formatação das leituras reais (o +, a casa);
        // medido só com -12.0 e -11.0 no 081. Os especiais (0.5, 0.01, 0.05) vão como foram.
        var especial = p.Especiais?.ContainsKey(v) == true;
        return p.Tipo switch
        {
            TipoValor.Decibel or TipoValor.Q => (v >= 0 ? "+" : "") + v.ToString(especial ? "0.0#" : "0.0", Fio),
            TipoValor.Centimetro or TipoValor.Attack => v.ToString(especial ? "0.0#" : "0.0", Fio),
            TipoValor.Booleano => v == 1 ? "1" : "0",
            _ => decimal.Truncate(v).ToString("0", Fio),
        };
    }

    /// <summary>A resposta de leitura no formato real, sem o <c>\r\n</c>. Chamar com a trava.</summary>
    private string? LeituraDe(Parametro p, int? indice)
    {
        if (!_estado.TryGetValue((p.Codigo, indice), out var v))
            return null;
        if (p.Tipo == TipoValor.Nome)
            return $"{p.Codigo}#{v}"; // 231#SPL: sem # nas pontas
        return p.Escopo switch
        {
            Escopo.Global => $"#{p.Codigo}#{v}#",
            Escopo.Canal => $"#{p.Codigo}#{indice}#{v}#",
            _ => $"{p.Codigo}#{indice}#{v}", // 020#b#v: sem # nas pontas
        };
    }

    private void Recusar(string quadro, string motivo) => _recusadas.Add((quadro, motivo));

    // ------------------------------------------------------------------ conexão e envio

    /// <summary>Solta a conexão e cancela a queda pendente. Chamar com a trava.</summary>
    private void Soltar()
    {
        _conectado = false;
        _autenticado = false;
        _geracao++;
        CancelarQuedaAposErro();
    }

    private void CancelarQuedaAposErro()
    {
        _quedaAposErro?.Dispose();
        _quedaAposErro = null;
    }

    private void CairAposErro(int geracao)
    {
        lock (_trava)
        {
            if (geracao != _geracao || !_conectado)
                return;
            Soltar();
            Quedas++;
        }
        Caiu?.Invoke();
    }

    private void Enviar(int geracao, string mensagem, TimeSpan atraso)
    {
        var texto = (OrfaoAntesDeCadaResposta is { } orfao ? orfao + FimDeLinha : "") + mensagem + FimDeLinha;
        if (atraso <= TimeSpan.Zero)
        {
            EmitirAgora(geracao, texto);
            return;
        }
        // Cada resposta espera o seu atraso e a anterior: saem na ordem dos envios, mesmo quando
        // vencem no mesmo Avancar do relógio falso.
        lock (_trava)
        {
            var anterior = _ultimaAtrasada;
            var espera = Task.Delay(atraso, _relogio);
            _ultimaAtrasada = Task.WhenAll(anterior, espera)
                .ContinueWith(_ => EmitirAgora(geracao, texto), TaskScheduler.Default);
        }
    }

    /// <summary>Envia pela notificação, se a conexão ainda é a mesma. Uma mensagem por vez.</summary>
    private void EmitirAgora(int geracao, string texto)
    {
        lock (_emissao)
        {
            List<string> fragmentos;
            lock (_trava)
            {
                if (geracao != _geracao || !_conectado)
                    return;
                _notificacoes.Add(texto);
                fragmentos = Fragmentar(texto);
            }
            foreach (var f in fragmentos)
                Notificacao?.Invoke(Encoding.ASCII.GetBytes(f));
        }
    }

    /// <summary>Chamar com a trava (o sorteio não é thread-safe).</summary>
    private List<string> Fragmentar(string texto)
    {
        if (TamanhoMaximoFragmento is not { } maximo || maximo <= 0)
            return [texto];
        var partes = new List<string>();
        for (var i = 0; i < texto.Length;)
        {
            var n = Math.Min(_sorteio.Next(1, maximo + 1), texto.Length - i);
            partes.Add(texto.Substring(i, n));
            i += n;
        }
        return partes;
    }

    // ------------------------------------------------------------------ semente e fábrica

    private static IReadOnlyDictionary<(string Codigo, int? Indice), string> LerSemente()
    {
        var estado = new Dictionary<(string, int?), string>();
        using var fluxo = typeof(ProcessadorSimulado).Assembly.GetManifestResourceStream(RecursoSemente)
                          ?? throw new InvalidOperationException($"Recurso {RecursoSemente} ausente.");
        using var doc = JsonDocument.Parse(fluxo);
        foreach (var l in doc.RootElement.GetProperty("leituras").EnumerateArray())
            Guardar(estado, l.GetProperty("cru").GetString()!);
        foreach (var cru in ExtrasReais)
            Guardar(estado, cru);
        return estado;
    }

    private static void Guardar(Dictionary<(string, int?), string> estado, string cru)
    {
        var q = Quadros.Analisar(cru) ?? throw new InvalidOperationException($"Resposta ilegível na semente: {cru}");
        var p = Catalogo.ParametroDe(q.Codigo) ?? throw new InvalidOperationException($"Código fora do catálogo: {cru}");
        if (p.Escopo == Escopo.Global)
            estado[(p.Codigo, null)] = string.Join("#", q.Segmentos);
        else
            estado[(p.Codigo, int.Parse(q.Segmentos[0], Fio))] = q.Segmentos[1];
    }

    private static Dictionary<(string, int?), string> CopiaMarco1(Dictionary<(string Codigo, int? Indice), string> estado) =>
        Catalogo.ConsultasMarco1
            .Where(c => estado.ContainsKey((c.Parametro.Codigo, c.Indice)))
            .ToDictionary(c => (c.Parametro.Codigo, c.Indice), c => estado[(c.Parametro.Codigo, c.Indice)]);

    /// <summary>
    /// O estado de fábrica do Marco 1, usado por <c>*242*</c> e <c>*004*</c>. Tudo neutro:
    /// ganhos 0 dB, sem mute, roteamento a/b/a/b, fase 0°, filtros OFF em 10 Hz e 22 kHz,
    /// delay 0, limiter manual em OFF, EQs planos em 1 kHz com Q 1,0, preset Flat.
    /// </summary>
    // SUPOSIÇÃO: o estado de fábrica não foi medido.
    public static IReadOnlyDictionary<(string Codigo, int? Indice), string> Fabrica { get; } =
        Catalogo.ConsultasMarco1.ToDictionary(c => (c.Parametro.Codigo, c.Indice), c => c.Parametro.Codigo switch
        {
            "080" or "081" or "091" or "030" or "020" => "+0.0",
            "100" or "060" or "042" or "043" or "070" or "220" => "0",
            "010" => c.Indice % 2 == 0 ? "a" : "b",
            "040" => "10",
            "041" => "22000",
            "050" => "0.0",
            "071" => "+0.5",
            "072" => "50.0",
            "073" => "200",
            "090" or "031" => "1000",
            "092" or "032" => "+1.0",
            _ => throw new InvalidOperationException($"Sem valor de fábrica para o {c.Parametro.Codigo}."),
        });

    [GeneratedRegex(@"^\*000\*([^*#]*)\*$", RegexOptions.CultureInvariant)]
    private static partial Regex QuadroSenha();

    // SUPOSIÇÃO: memória 1 = n 0 = nome 231 (a correspondência não foi medida).
    [GeneratedRegex(@"^\*(240|241)\*([0-2])\*$", RegexOptions.CultureInvariant)]
    private static partial Regex QuadroMemoria();

    [GeneratedRegex(@"^\*001\*([^*#]*)\*([^*#]*)\*$", RegexOptions.CultureInvariant)]
    private static partial Regex TrocaSenha();

    [GeneratedRegex(@"^[0-9A-Z]{4}\z", RegexOptions.CultureInvariant)]
    private static partial Regex SenhaValida();
}
