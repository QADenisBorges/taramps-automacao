using BgsControle.Nucleo.Escrita;
using BgsControle.Nucleo.Leitura;
using BgsControle.Nucleo.Registro;
using BgsControle.Nucleo.Seguranca;
using BgsControle.Nucleo.Uso;

namespace BgsControle.Nucleo.Gerador;

/// <summary>Em que pé está o gerador, para a tela.</summary>
public enum EstadoGerador
{
    /// <summary>Desligado (ou sem notícia de que esteja ligado).</summary>
    Desligado,

    /// <summary>Enviando os parâmetros e o <c>*202*1*</c>.</summary>
    Ligando,

    /// <summary>Ligado e confirmado; o prazo de 30 s corre.</summary>
    Tocando,

    /// <summary>O <c>*202*0*</c> saiu e o app espera a confirmação.</summary>
    Desligando,

    /// <summary>A conexão caiu (ou o desligar não foi confirmado) com o gerador talvez ligado: o app reconecta para desligar.</summary>
    Reconectando,

    /// <summary>Não deu para desligar: alarme "DESLIGUE O MÓDULO AGORA" até uma leitura mostrar 202 = 0.</summary>
    SemResposta,
}

/// <summary>Como terminou um pedido ao gerador. <see cref="Mensagem"/> é a frase para a tela.</summary>
public sealed record ResultadoGerador(bool Ok, string? Motivo, string? Mensagem = null)
{
    public static ResultadoGerador Falha(string motivo, string? mensagem = null) => new(false, motivo, mensagem ?? motivo);
}

/// <summary>Os prazos do gerador. Os testes usam o relógio falso.</summary>
/// <param name="Prazo">O tempo máximo do tom (30 s, failure-modes, regra 1).</param>
/// <param name="PausaReconexao">Entre duas tentativas de reconectar para desligar.</param>
public sealed record TemposGerador(TimeSpan Prazo, TimeSpan PausaReconexao)
{
    public static TemposGerador Padrao { get; } = new(Prazo: TimeSpan.FromSeconds(30), PausaReconexao: TimeSpan.FromSeconds(1));

    /// <summary>
    /// Quanto uma reconexão espera a leitura chegar ao fim (e a regra 2 conferir o 202) antes de
    /// contar como falha. A leitura completa leva ~13 s; 60 s dão folga.
    /// </summary>
    public TimeSpan LimiteTentativa { get; init; } = TimeSpan.FromSeconds(60);
}

/// <summary>
/// O gerador de sinal do processador (CAP-8): tom fixo e sweep, sempre pela porta da sessão
/// (<see cref="Sessao.EnviarAsync"/>) e com a chave "Gerador", com as regras 1 e 2 de
/// <c>failure-modes.md</c>:
/// <list type="bullet">
/// <item><b>Tocar:</b> envia os parâmetros, cada um confirmado pelo eco ou pela releitura, e por
/// último o <c>*202*1*</c>. Se um parâmetro falha, o <c>*202*1*</c> não sai. Recusado, sem
/// enviar nada, sem leitura, lendo, com uma operação de memória andando ou pela metade, ou com o
/// gerador já em uso.</item>
/// <item><b>Prazo:</b> desde o envio do <c>*202*1*</c> o gerador conta como "talvez ligado", e
/// só deixa de contar quando o app lê 202 = 0. O <c>*202*0*</c> sai no máximo 30 s depois do
/// <c>*202*1*</c> (a contagem mostrada começa na confirmação), sem esperar uma leitura em
/// andamento, e é confirmado pelo eco ou pela releitura de <c>#202#</c>.</item>
/// <item><b>Regra 1:</b> desliga também no Parar, num erro ao ligar, antes de a tela soltar a
/// conexão (Desconectar, Esquecer a senha) e ao sair do app. Se a conexão cai, ou se o
/// desligar não é confirmado, com o gerador talvez ligado, reconecta até 3 vezes (cada uma é um
/// <see cref="Sessao.ConectarAsync"/> completo, e a regra 2 desliga); se as 3 falham, fica
/// <see cref="EstadoGerador.SemResposta"/>, com o alarme, até uma leitura mostrar 202 = 0.</item>
/// <item><b>Regra 2:</b> no fim de cada leitura, antes do aviso de fim (e, portanto, antes do
/// espelho, da retomada da cópia e de qualquer escrita), confere o 202: ligado sem o app estar
/// tocando, desliga e avisa "O gerador estava ligado e foi desligado." No modo só leitura nada
/// sai e vale o alarme. O espelho passa a mostrar o 202 confirmado.</item>
/// </list>
/// Crie cedo (antes da primeira conexão): ele se pendura na sessão e na leitura automática.
/// </summary>
public sealed class GeradorSinal
{
    public const int TentativasDeReconexao = 3;

    public const string MsgTocando = "Tocando. O gerador desliga sozinho em 30 s.";
    public const string MsgPrazo = "O prazo de 30 s acabou: o gerador foi desligado.";
    public const string MsgParado = "O gerador foi desligado.";
    public const string MsgEstavaLigado = "O gerador estava ligado e foi desligado.";
    public const string MsgDesligadoAoReconectar = "Reconectado: o gerador está desligado.";
    public const string MsgAlarme = "DESLIGUE O MÓDULO AGORA";
    public const string MsgEmUso = "O gerador já está em uso.";
    public const string MsgCancelado = "Parado antes de ligar: o gerador não foi ligado.";
    public const string MsgNaoLigou = "O aparelho não confirmou que ligou o gerador. O app mandou desligar.";
    public const string MsgNaoConfirmouDesligar = "O aparelho não confirmou que desligou o gerador.";
    public const string MsgSemConexaoParaDesligar = "Sem conexão: o app não conseguiu mandar desligar o gerador.";
    public const string MsgSemConfirmacao = "O aparelho não confirmou: sem eco e sem resposta à releitura.";
    public const string MsgCaiu = "A conexão caiu antes da confirmação.";
    public const string MsgVelocidade = "A velocidade do sweep vai de 1 (lento) a 3 (rápido).";

    public static string MsgReconectando(int tentativa) =>
        $"Reconectando para desligar o gerador (tentativa {tentativa} de {TentativasDeReconexao})…";

    public static string MsgParametroFalhou(Parametro p, string motivo) => $"{p.Nome}: {motivo} O gerador não foi ligado.";

    private static readonly Parametro Ganho = Catalogo.ParametroDe(Catalogo.CodigoGeradorGanho)!;
    private static readonly Parametro Frequencia = Catalogo.ParametroDe(Catalogo.CodigoGeradorFrequencia)!;
    private static readonly Parametro Ligado = Catalogo.ParametroDe(Catalogo.CodigoGeradorLigado)!;
    private static readonly Parametro SweepInicio = Catalogo.ParametroDe(Catalogo.CodigoSweepInicio)!;
    private static readonly Parametro SweepFim = Catalogo.ParametroDe(Catalogo.CodigoSweepFim)!;
    private static readonly Parametro SweepGanho = Catalogo.ParametroDe(Catalogo.CodigoSweepGanho)!;
    private static readonly Parametro Velocidade = Catalogo.ParametroDe(Catalogo.CodigoSweepVelocidade)!;
    private static readonly Consulta Consulta202 = Catalogo.ConsultaDe(Ligado.TextoConsulta(null))!;
    private static readonly Endereco Endereco202 = new(Catalogo.CodigoGeradorLigado, null);

    private readonly Sessao _sessao;
    private readonly EstadoAparelho _estado;
    private readonly CopiaAntes _copia;
    private readonly LogBle _log;
    private readonly EnvioComResposta _envio;
    private readonly TemposGerador _tempos;
    private readonly TimeProvider _relogio;

    private readonly object _trava = new();
    private readonly SemaphoreSlim _fio = new(1, 1); // a ordem entre o *202*1* e o *202*0*

    private bool _ligando;
    private bool _tocando;
    private bool _cancelarLigar;
    private bool _talvezLigado;
    private bool _alarme;
    private bool _reconectando;
    private TaskCompletionSource<(Valor? Confirmado, string? Motivo)>? _desligando;
    private TaskCompletionSource<bool>? _tentativa;
    private ITimer? _prazo;
    private DateTimeOffset? _fimDoPrazo;
    private string? _mensagem;
    private int _confirmacoes;
    private int _confirmacoesNoInicio;
    private Valor? _ultimoConfirmado;

    public GeradorSinal(
        Sessao sessao,
        EstadoAparelho estado,
        LeituraAutomatica leitura,
        CopiaAntes copia,
        LogBle log,
        TemposEscrita? tempos = null,
        TemposGerador? temposGerador = null,
        TimeProvider? relogio = null)
    {
        _sessao = sessao;
        _estado = estado;
        _copia = copia;
        _log = log;
        _relogio = relogio ?? TimeProvider.System;
        _tempos = temposGerador ?? TemposGerador.Padrao;
        _envio = new EnvioComResposta(sessao, tempos, _relogio);

        _sessao.AntesDeSoltar = AntesDeSoltarAsync;
        _sessao.Mudou += AoMudarSessao;
        leitura.AntesDoFim = AoFimDaLeituraAsync;
        leitura.Comecou += AoComecarLeitura;
    }

    /// <summary>Algo do gerador mudou (estado, mensagem, alarme, prazo). Pode chegar em qualquer thread.</summary>
    public event Action? Mudou;

    public EstadoGerador Estado
    {
        get
        {
            lock (_trava)
            {
                if (_alarme)
                    return EstadoGerador.SemResposta;
                if (_reconectando)
                    return EstadoGerador.Reconectando;
                if (_desligando is not null)
                    return EstadoGerador.Desligando;
                if (_tocando)
                    return EstadoGerador.Tocando;
                return _ligando ? EstadoGerador.Ligando : EstadoGerador.Desligado;
            }
        }
    }

    /// <summary>Desde o envio de um <c>*202*1*</c> (ou de uma leitura com 202 = 1) até o app ler 202 = 0.</summary>
    public bool TalvezLigado
    {
        get
        {
            lock (_trava)
                return _talvezLigado;
        }
    }

    /// <summary>O alarme "DESLIGUE O MÓDULO AGORA": fica até uma leitura mostrar 202 = 0.</summary>
    public bool Alarme
    {
        get
        {
            lock (_trava)
                return _alarme;
        }
    }

    /// <summary>A última frase para a tela, ou null.</summary>
    public string? Mensagem
    {
        get
        {
            lock (_trava)
                return _mensagem;
        }
    }

    /// <summary>Quando o prazo vence (tocando), ou null.</summary>
    public DateTimeOffset? FimDoPrazo
    {
        get
        {
            lock (_trava)
                return _tocando ? _fimDoPrazo : null;
        }
    }

    /// <summary>Quanto falta para o prazo (tocando), ou null.</summary>
    public TimeSpan? Restante
    {
        get
        {
            if (FimDoPrazo is not { } fim)
                return null;
            var falta = fim - _relogio.GetUtcNow();
            return falta > TimeSpan.Zero ? falta : TimeSpan.Zero;
        }
    }

    private bool SessaoAberta => _sessao.Estado == EstadoSessao.Conectado && _sessao.SegurandoConexao;

    // ------------------------------------------------------------------ tocar

    /// <summary>
    /// Tom fixo: <c>*213*0*</c>, <c>*201*hz*</c>, <c>*200*dB*</c> e, por último, <c>*202*1*</c>.
    /// Frequência de 10 a 22000 Hz; ganho de −60,0 a 0,0 dB, com uma casa.
    /// </summary>
    public Task<ResultadoGerador> TocarTomAsync(int hz, decimal db)
    {
        string[] parametros;
        try
        {
            // Suposição GERADOR-MODO: o tom fixo é o 213 em 0; o sweep é o 213 de 1 a 3; o 202 liga e desliga os dois.
            parametros =
            [
                Velocidade.CodificarEscrita(null, 0m),
                Frequencia.CodificarEscrita(null, hz),
                Ganho.CodificarEscrita(null, db),
            ];
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException)
        {
            return Task.FromResult(ResultadoGerador.Falha(e.Message));
        }
        return TocarAsync(parametros, $"tom fixo {hz} Hz, {db.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)} dB");
    }

    /// <summary>
    /// Sweep: <c>*210*</c>, <c>*211*</c>, <c>*212*</c>, <c>*213*v*</c> (v de 1 a 3) e, por
    /// último, <c>*202*1*</c>.
    /// </summary>
    public Task<ResultadoGerador> TocarSweepAsync(int inicioHz, int fimHz, decimal db, int velocidade)
    {
        if (velocidade is < 1 or > 3)
            return Task.FromResult(ResultadoGerador.Falha(MsgVelocidade));
        string[] parametros;
        try
        {
            parametros =
            [
                SweepInicio.CodificarEscrita(null, inicioHz),
                SweepFim.CodificarEscrita(null, fimHz),
                SweepGanho.CodificarEscrita(null, db),
                Velocidade.CodificarEscrita(null, velocidade),
            ];
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException)
        {
            return Task.FromResult(ResultadoGerador.Falha(e.Message));
        }
        return TocarAsync(parametros, $"sweep {inicioHz} → {fimHz} Hz");
    }

    private async Task<ResultadoGerador> TocarAsync(IReadOnlyList<string> parametros, string descricao)
    {
        string[] todos = [.. parametros, Catalogo.LigarGerador];
        if (MotivoParaNaoTocar(todos) is { } motivo)
            return ResultadoGerador.Falha(motivo);
        lock (_trava)
        {
            if (_ligando || _tocando || _desligando is not null || _reconectando || _talvezLigado || _alarme)
                return ResultadoGerador.Falha(MsgEmUso);
            _ligando = true;
            _cancelarLigar = false;
            _mensagem = null;
        }
        Notificar();
        _log.Evento($"gerador: tocar {descricao}");

        ResultadoGerador r;
        bool desligar;
        var porta = _sessao.ExclusaoLeituraEscrita;
        await porta.WaitAsync();
        try
        {
            (r, desligar) = await LigarAsync(parametros, todos);
        }
        catch (Exception e)
        {
            _log.Evento($"gerador: erro ao ligar ({e.GetType().Name})");
            (r, desligar) = (ResultadoGerador.Falha($"Erro inesperado ({e.GetType().Name})."), true);
        }
        finally
        {
            lock (_trava)
                _ligando = false;
            porta.Release();
        }

        if (!r.Ok)
            _log.Evento($"gerador: {r.Mensagem ?? r.Motivo}");
        Avisar(r.Mensagem);
        if (desligar)
        {
            // Erro ao ligar (regra 1): o app desliga sozinho e confirma; sem confirmação, reconecta.
            var (confirmado, _) = await DesligarSozinhoAsync();
            if (confirmado is null)
                IniciarReconexaoSePreciso();
        }
        return r;
    }

    /// <summary>Os parâmetros e o 202, com a exclusão da sessão. Desligar = o 202 1 pode ter saído e algo falhou.</summary>
    private async Task<(ResultadoGerador Resultado, bool Desligar)> LigarAsync(IReadOnlyList<string> parametros, string[] todos)
    {
        if (MotivoParaNaoTocar(todos) is { } motivo)
            return (ResultadoGerador.Falha(motivo), false);

        foreach (var quadro in parametros)
        {
            if (Cancelado)
                return (ResultadoGerador.Falha(MsgCancelado), false);
            var (ok, falha) = await EnviarParametroAsync(quadro);
            if (!ok)
                return (ResultadoGerador.Falha(falha!), false); // o *202*1* não sai
        }

        // O *202*1*, por último. A ordem com um *202*0* do Parar é garantida pelo _fio.
        using var eco = _envio.Esperar(Casar202(ligado: true));
        DateTimeOffset enviado;
        ResultadoEnvio envio;
        await _fio.WaitAsync();
        try
        {
            lock (_trava)
            {
                if (_cancelarLigar)
                    return (ResultadoGerador.Falha(MsgCancelado), false);
                _talvezLigado = true;
                enviado = _relogio.GetUtcNow();
            }
            envio = await _envio.EnviarAsync(Catalogo.LigarGerador);
        }
        finally
        {
            _fio.Release();
        }
        Notificar();
        if (!envio.Enviado)
            return (ResultadoGerador.Falha(envio.Motivo ?? MsgNaoLigou, MsgNaoLigou), true);

        var resposta = await eco.AguardarAsync(_envio.Tempos.PrazoEco);
        if (Cancelado)
            return (ResultadoGerador.Falha(MsgCancelado), false); // o Parar já mandou desligar: nada de reler
        if (resposta is null && SessaoAberta)
        {
            _log.Evento($"{Catalogo.LigarGerador}: sem eco em {_envio.Tempos.PrazoEco.TotalSeconds:0.#} s; relendo");
            (_, resposta) = await _envio.EnviarEEsperarAsync(Consulta202.Texto, Casar202(ligado: null), _envio.Tempos.PrazoReleitura);
        }
        if (Cancelado)
            return (ResultadoGerador.Falha(MsgCancelado), false);
        if (Decodificar202(resposta) is not { Ligado: true } valor)
            return (ResultadoGerador.Falha(SessaoAberta ? MsgNaoLigou : MsgCaiu, MsgNaoLigou), true);

        lock (_trava)
        {
            if (_cancelarLigar)
                return (ResultadoGerador.Falha(MsgCancelado), false); // o Parar já mandou desligar
            _tocando = true;
            // O prazo vale desde o envio do *202*1*: o tom nunca passa de 30 s, mesmo que a
            // confirmação demore. A contagem na tela começa agora, com o que falta.
            _fimDoPrazo = enviado + _tempos.Prazo;
            var falta = _fimDoPrazo.Value - _relogio.GetUtcNow();
            _prazo = _relogio.CreateTimer(_ => AoVencerPrazo(), null, falta > TimeSpan.Zero ? falta : TimeSpan.Zero, Timeout.InfiniteTimeSpan);
        }
        // Só depois de conferir o cancelamento: o 202 = 0 confirmado pelo Parar não é trocado por 1.
        _estado.Atualizar(Endereco202, valor);
        _log.Evento("gerador: tocando; desliga sozinho no prazo");
        return (new ResultadoGerador(true, null, MsgTocando), false);
    }

    private bool Cancelado
    {
        get
        {
            lock (_trava)
                return _cancelarLigar;
        }
    }

    /// <summary>Envia um parâmetro do gerador e confirma pelo eco ou pela releitura. Um valor diferente do pedido é falha.</summary>
    private async Task<(bool Ok, string? Motivo)> EnviarParametroAsync(string quadro)
    {
        var comando = Catalogo.ComandoDeGerador(quadro)!;
        var p = comando.Parametro;
        var consulta = Catalogo.ConsultaDe(p.TextoConsulta(null))!;
        // Suposição ECO-GERADOR: os códigos 200–202 e 210–213 ecoam no formato de leitura, como o 081.
        Func<string, string?> casar = texto => texto.TrimStart().StartsWith('*') ? null : LeitorCompleto.Casar(consulta, texto);

        var (envio, resposta) = await _envio.EnviarEEsperarAsync(quadro, casar, _envio.Tempos.PrazoEco);
        if (!envio.Enviado)
            return (false, MsgParametroFalhou(p, Frase(envio.Motivo ?? "não enviado")));
        if (resposta is null)
        {
            if (!SessaoAberta)
                return (false, MsgParametroFalhou(p, MsgCaiu));
            _log.Evento($"{quadro}: sem eco em {_envio.Tempos.PrazoEco.TotalSeconds:0.#} s; relendo");
            (envio, resposta) = await _envio.EnviarEEsperarAsync(consulta.Texto, casar, _envio.Tempos.PrazoReleitura);
            if (!envio.Enviado)
                return (false, MsgParametroFalhou(p, Frase(envio.Motivo ?? "não enviado")));
            if (resposta is null)
                return (false, MsgParametroFalhou(p, SessaoAberta ? MsgSemConfirmacao : MsgCaiu));
        }

        if (p.Decodificar(resposta).Valor is not { } valor)
            return (false, MsgParametroFalhou(p, "O aparelho respondeu um valor ilegível."));
        _estado.Atualizar(new Endereco(p.Codigo, null), valor);
        if (valor.Numero != comando.Valor)
        {
            _log.Evento($"{quadro}: o aparelho ficou em {valor.NoFio}");
            return (false, MsgParametroFalhou(p, $"O aparelho ficou em {valor.Exibicao}."));
        }
        return (true, null);
    }

    /// <summary>Por que não dá para tocar agora, ou null. Não envia nada.</summary>
    private string? MotivoParaNaoTocar(IEnumerable<string> quadros)
    {
        if (!SessaoAberta)
            return Sessao.MotivoSemConexao;
        foreach (var q in quadros)
        {
            if (_sessao.Conferir(q) is { } motivo)
            {
                _log.Recusado(q, motivo);
                return motivo;
            }
        }
        if (_estado.Lendo)
            return Escritor.MsgLendo;
        if (!_estado.Pronto)
            return Escritor.MsgSemLeitura;
        if (_copia.MotivoBloqueio() is { } bloqueio)
            return bloqueio;
        return null;
    }

    // ------------------------------------------------------------------ desligar

    /// <summary>Parar: o <c>*202*0*</c> sai na hora (sem a chave, sem esperar leitura) e é confirmado. Nunca fica travado.</summary>
    public async Task<ResultadoGerador> PararAsync()
    {
        _log.Evento("gerador: parar");
        var (confirmado, motivo) = await DesligarAsync(MsgParado);
        if (confirmado is not null)
        {
            Avisar(MsgParado);
            return new ResultadoGerador(true, null, MsgParado);
        }
        var frase = SessaoAberta ? MsgNaoConfirmouDesligar : MsgSemConexaoParaDesligar;
        if (!IniciarReconexaoSePreciso())
            AvisarSemAlarme(frase);
        return ResultadoGerador.Falha(motivo ?? frase, frase);
    }

    /// <summary>Ao sair do app ou fechá-lo: desliga se o gerador pode estar ligado.</summary>
    public async Task DesligarAoSairAsync()
    {
        if (!PodeEstarLigado())
            return;
        _log.Evento("gerador: o app vai sair; desligando");
        using (Origem.Como(OrigemComando.App)) // o desligar automático vai como App no histórico
            await PararAsync();
    }

    /// <summary>O gancho da sessão: antes de a tela soltar a conexão, desliga.</summary>
    private async Task AntesDeSoltarAsync()
    {
        lock (_trava)
        {
            if (_reconectando)
                return; // é o próprio gerador soltando para reconectar
        }
        if (!PodeEstarLigado())
            return;
        _log.Evento("gerador: desligando antes de soltar a conexão");
        var (confirmado, _) = await DesligarSozinhoAsync();
        // Sem confirmação, a conexão sai mesmo assim; a queda com o gerador talvez ligado
        // faz reconectar (regra 1).
        if (confirmado is not null)
            Avisar(MsgParado);
        else
            AvisarSemAlarme(MsgNaoConfirmouDesligar);
    }

    private bool PodeEstarLigado()
    {
        lock (_trava)
            return _talvezLigado || _ligando || _tocando || _desligando is not null;
    }

    private void AoVencerPrazo()
    {
        lock (_trava)
        {
            if (!_tocando)
                return;
        }
        _ = VencerPrazoAsync();
    }

    private async Task VencerPrazoAsync()
    {
        try
        {
            _log.Evento("gerador: o prazo acabou; desligando");
            // Sem esperar nada: o *202*0* sai já, mesmo com uma leitura completa em andamento.
            var (confirmado, _) = await DesligarSozinhoAsync(MsgPrazo);
            if (confirmado is not null)
            {
                Avisar(MsgPrazo);
                return;
            }
            if (!IniciarReconexaoSePreciso())
                AvisarSemAlarme(SessaoAberta ? MsgNaoConfirmouDesligar : MsgSemConexaoParaDesligar);
        }
        catch (Exception e)
        {
            _log.Evento($"gerador: erro no prazo ({e.GetType().Name})");
        }
    }

    /// <summary>
    /// Envia o <c>*202*0*</c> e confirma pelo eco ou pela releitura de <c>#202#</c>. Não pega a
    /// exclusão da sessão: não espera leitura nem escrita em andamento. Pedidos ao mesmo tempo
    /// esperam o mesmo desligar. Confirmado = o valor lido (202 = 0); null = não confirmado.
    /// A <paramref name="frase"/> do dono do desligar entra junto com a confirmação: a tela nunca
    /// vê o gerador desligado com a frase antiga.
    /// </summary>
    private async Task<(Valor? Confirmado, string? Motivo)> DesligarAsync(string? frase = null)
    {
        TaskCompletionSource<(Valor?, string?)> tcs;
        bool dono;
        lock (_trava)
        {
            _cancelarLigar = true;
            _tocando = false;
            CancelarPrazo();
            if (_desligando is { } emCurso)
            {
                tcs = emCurso;
                dono = false;
            }
            else
            {
                tcs = new TaskCompletionSource<(Valor?, string?)>(TaskCreationOptions.RunContinuationsAsynchronously);
                _desligando = tcs;
                dono = true;
            }
        }
        if (!dono)
            return await tcs.Task;

        (Valor? Confirmado, string? Motivo) r;
        try
        {
            r = await DesligarNoFioAsync();
        }
        catch (Exception e)
        {
            _log.Evento($"gerador: erro ao desligar ({e.GetType().Name})");
            r = (null, $"Erro inesperado ({e.GetType().Name}).");
        }
        lock (_trava)
            _desligando = null;
        if (r.Confirmado is { } valor)
            ConfirmarDesligado(valor, frase);
        else
            Notificar();
        tcs.TrySetResult(r);
        return r;
    }

    /// <summary>
    /// O desligar automático (prazo, erro ao ligar, antes de soltar, ao sair, regra 2 ao reconectar):
    /// o mesmo <see cref="DesligarAsync"/>, com a origem App no histórico.
    /// </summary>
    private async Task<(Valor? Confirmado, string? Motivo)> DesligarSozinhoAsync(string? frase = null)
    {
        using (Origem.Como(OrigemComando.App))
            return await DesligarAsync(frase);
    }

    private async Task<(Valor? Confirmado, string? Motivo)> DesligarNoFioAsync()
    {
        using var eco = _envio.Esperar(Casar202(ligado: false));
        ResultadoEnvio envio;
        await _fio.WaitAsync();
        try
        {
            envio = await _envio.EnviarAsync(Catalogo.DesligarGerador);
        }
        finally
        {
            _fio.Release();
        }
        Notificar();
        if (!envio.Enviado)
            return (null, envio.Motivo ?? Sessao.MotivoSemConexao);

        var resposta = await eco.AguardarAsync(_envio.Tempos.PrazoEco);
        if (resposta is null)
        {
            if (!SessaoAberta)
                return (null, MsgCaiu);
            _log.Evento($"{Catalogo.DesligarGerador}: sem eco em {_envio.Tempos.PrazoEco.TotalSeconds:0.#} s; relendo");
            (envio, resposta) = await _envio.EnviarEEsperarAsync(Consulta202.Texto, Casar202(ligado: null), _envio.Tempos.PrazoReleitura);
            if (!envio.Enviado)
                return (null, envio.Motivo ?? Sessao.MotivoSemConexao);
        }
        var valor = Decodificar202(resposta);
        if (valor is { Ligado: false })
            return (valor, null);
        var motivo = valor is null ? (SessaoAberta ? MsgSemConfirmacao : MsgCaiu) : MsgNaoConfirmouDesligar;
        _log.Evento($"{Catalogo.DesligarGerador}: {motivo}");
        return (null, motivo);
    }

    /// <summary>O app leu 202 = 0: o gerador deixa de contar como talvez ligado, e o alarme sai.</summary>
    private void ConfirmarDesligado(Valor valor, string? frase = null)
    {
        bool tinhaAlarme;
        lock (_trava)
        {
            tinhaAlarme = _alarme;
            if (frase is not null)
                _mensagem = frase;
            _talvezLigado = false;
            _alarme = false;
            _tocando = false;
            _confirmacoes++;
            _ultimoConfirmado = valor;
            CancelarPrazo();
        }
        _estado.Atualizar(Endereco202, valor);
        if (tinhaAlarme)
            _log.Evento("gerador: 202 = 0 lido; o alarme saiu");
        Notificar();
    }

    /// <summary>Chamar com a trava.</summary>
    private void CancelarPrazo()
    {
        _prazo?.Dispose();
        _prazo = null;
        _fimDoPrazo = null;
    }

    // ------------------------------------------------------------------ regra 1: reconectar

    private void AoMudarSessao()
    {
        if (SessaoAberta)
            return;
        TaskCompletionSource<bool>? tentativa = null;
        bool reconectar;
        lock (_trava)
        {
            // Sem conexão, o prazo não tem como desligar: quem desliga agora é a reconexão.
            var tocava = _tocando;
            _tocando = false;
            CancelarPrazo();
            var desconectado = _sessao.Estado == EstadoSessao.Desconectado;
            if (desconectado)
                tentativa = _tentativa;
            reconectar = desconectado && _talvezLigado && !_reconectando && !_alarme;
            if (!tocava && !reconectar && tentativa is null)
                return;
        }
        tentativa?.TrySetResult(false);
        if (reconectar)
        {
            _log.Evento("gerador: a conexão caiu com o gerador talvez ligado");
            IniciarReconexaoSePreciso();
        }
        else
        {
            Notificar();
        }
    }

    /// <summary>Com o gerador talvez ligado, começa a reconexão (se ainda não anda e sem o alarme). True = começou ou já andava.</summary>
    private bool IniciarReconexaoSePreciso()
    {
        lock (_trava)
        {
            if (_reconectando)
                return true;
            if (!_talvezLigado || _alarme)
                return false;
            _reconectando = true;
        }
        Notificar();
        _ = Task.Run(ReconectarAsync);
        return true;
    }

    /// <summary>
    /// Até 3 reconexões, cada uma um <see cref="Sessao.ConectarAsync"/> completo; a regra 2, no
    /// fim da leitura, desliga. Se as 3 falham: alarme até uma leitura mostrar 202 = 0.
    /// </summary>
    private async Task ReconectarAsync()
    {
        var desligou = false;
        var feitas = 0;
        try
        {
            for (var t = 1; t <= TentativasDeReconexao; t++)
            {
                feitas = t;
                Avisar(MsgReconectando(t));
                _log.Evento($"gerador: reconectando para desligar (tentativa {t} de {TentativasDeReconexao})");
                if (t > 1)
                    await Task.Delay(_tempos.PausaReconexao, _relogio);
                if (_sessao.Estado != EstadoSessao.Desconectado)
                    await _sessao.DesconectarAsync(); // conectada sem confirmar o desligar: solta e conecta de novo
                await _sessao.Encerramento; // o GATT da conexão anterior foi solto

                var tentativa = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                lock (_trava)
                    _tentativa = tentativa;
                await _sessao.ConectarAsync();
                if (_sessao.Estado != EstadoSessao.Conectado)
                {
                    lock (_trava)
                        _tentativa = null;
                    if (_sessao.Estado == EstadoSessao.PedindoSenha)
                        break; // sem senha guardada, só o Denis conecta
                    continue;
                }

                // Com limite: se a leitura depois de reconectar nunca chega ao fim, a tentativa falhou.
                var ok = await EsperarTentativaAsync(tentativa.Task);
                lock (_trava)
                    _tentativa = null;
                if (ok || !TalvezLigado)
                {
                    desligou = true;
                    return;
                }
                if (_sessao.SoLeitura)
                    break; // no modo só leitura nada sai: não adianta tentar de novo
            }

            lock (_trava)
                _alarme = true;
            _log.Evento($"gerador: sem resposta depois de {feitas} {(feitas == 1 ? "reconexão" : "reconexões")}; alarme");
            Avisar(MsgAlarme);
        }
        catch (Exception e)
        {
            _log.Evento($"gerador: erro ao reconectar ({e.GetType().Name})");
            lock (_trava)
                _alarme = _talvezLigado;
            if (Alarme)
                Avisar(MsgAlarme);
        }
        finally
        {
            lock (_trava)
            {
                _reconectando = false;
                _tentativa = null;
            }
            if (desligou && Mensagem is { } m && m.StartsWith("Reconectando", StringComparison.Ordinal))
                Avisar(MsgDesligadoAoReconectar);
            else
                Notificar();
        }
    }

    /// <summary>O resultado da tentativa, ou false se não vier em <see cref="TemposGerador.LimiteTentativa"/>.</summary>
    private async Task<bool> EsperarTentativaAsync(Task<bool> tentativa)
    {
        using var cancelar = new CancellationTokenSource();
        var limite = Task.Delay(_tempos.LimiteTentativa, _relogio, cancelar.Token);
        if (await Task.WhenAny(tentativa, limite) != tentativa)
        {
            _log.Evento($"gerador: a leitura depois de reconectar não terminou em {_tempos.LimiteTentativa.TotalSeconds:0} s");
            return false;
        }
        cancelar.Cancel();
        return await tentativa;
    }

    // ------------------------------------------------------------------ regra 2: no fim da leitura

    private void AoComecarLeitura()
    {
        lock (_trava)
            _confirmacoesNoInicio = _confirmacoes;
    }

    /// <summary>
    /// No fim de cada leitura, antes do aviso de fim: confere o 202 e desliga se o app não está
    /// tocando. Devolve o retrato com o 202 confirmado.
    /// </summary>
    private async Task<Retrato> AoFimDaLeituraAsync(Retrato retrato)
    {
        TaskCompletionSource<bool>? tentativa;
        lock (_trava)
            tentativa = _tentativa;
        try
        {
            var (saida, desligado) = await ConferirLeituraAsync(retrato, tentativa is not null);
            tentativa?.TrySetResult(desligado);
            return saida;
        }
        catch (Exception e)
        {
            _log.Evento($"gerador: erro ao conferir o 202 ({e.GetType().Name})");
            tentativa?.TrySetResult(false);
            return retrato;
        }
    }

    private async Task<(Retrato Retrato, bool Desligado)> ConferirLeituraAsync(Retrato retrato, bool reconectando)
    {
        if (!SessaoAberta)
            return (retrato, false); // caiu: quem cuida é a queda (regra 1)

        var lido = retrato.Item(Catalogo.CodigoGeradorLigado)?.Valor;
        bool tocando, confirmadoNaLeitura, emCurso;
        Valor? confirmado;
        lock (_trava)
        {
            tocando = _tocando || _ligando;
            emCurso = _desligando is not null;
            confirmadoNaLeitura = _confirmacoes != _confirmacoesNoInicio && !_talvezLigado;
            confirmado = _ultimoConfirmado;
        }

        if (tocando)
            return (retrato, false); // "Ler de novo" tocando: não desliga; o prazo segue
        if (lido is { Ligado: false })
        {
            if (TalvezLigado || Alarme)
                ConfirmarDesligado(lido);
            return (retrato, true);
        }
        if (confirmadoNaLeitura && confirmado is not null)
        {
            // O app desligou e confirmou durante esta leitura: o 202 lido antes ficou velho.
            return (Substituir202(retrato, confirmado), true);
        }

        var ligado = lido is { Ligado: true };
        if (_sessao.SoLeitura)
        {
            if (ligado)
            {
                lock (_trava)
                    _alarme = true;
                _log.Evento("gerador: 202 = 1 no modo só leitura; nada sai; alarme");
                Avisar(MsgAlarme);
            }
            return (retrato, false);
        }

        if (ligado)
        {
            lock (_trava)
                _talvezLigado = true;
            _log.Evento("gerador: 202 = 1 na leitura; desligando antes de qualquer outra ação");
        }
        else
        {
            // O 202 não veio na leitura: o estado é desconhecido e desligar é o lado seguro. Conta
            // como talvez ligado até confirmar 202 = 0 (sem confirmação, reconecta e, sem volta, alarme).
            lock (_trava)
                _talvezLigado = true;
            _log.Evento("gerador: 202 sem leitura; desligando por segurança");
        }

        var (valor, _) = await DesligarSozinhoAsync(ligado && !emCurso ? MsgEstavaLigado : null);
        if (valor is null)
        {
            if (!reconectando && !IniciarReconexaoSePreciso() && ligado)
                AvisarSemAlarme(MsgNaoConfirmouDesligar);
            return (retrato, false);
        }
        if (ligado && !emCurso)
            Avisar(MsgEstavaLigado);
        return (Substituir202(retrato, valor), true);
    }

    /// <summary>O retrato com o 202 confirmado no lugar do lido (o espelho passa a mostrar este).</summary>
    private static Retrato Substituir202(Retrato retrato, Valor valor)
    {
        var item = new ItemLido(Consulta202.Parametro.Codigo, Consulta202.Parametro.Nome, null, Consulta202.Onde, true,
            Consulta202.Texto, $"#{Consulta202.Parametro.Codigo}#{valor.NoFio}#", valor,
            retrato.Item(Catalogo.CodigoGeradorLigado)?.Tentativas ?? 1);
        var lidos = retrato.Lidos.ToList();
        var i = lidos.FindIndex(l => l.Codigo == Catalogo.CodigoGeradorLigado && l.Indice is null);
        if (i >= 0)
            lidos[i] = item;
        else
            lidos.Add(item);
        return retrato with
        {
            Lidos = lidos,
            Falhas = retrato.Falhas.Where(f => !(f.Codigo == Catalogo.CodigoGeradorLigado && f.Indice is null)).ToArray(),
        };
    }

    // ------------------------------------------------------------------ apoio

    /// <summary>A resposta ao 202 (eco ou leitura). Com <paramref name="ligado"/>, só a que diz esse valor.</summary>
    private static Func<string, string?> Casar202(bool? ligado) => texto =>
    {
        if (texto.TrimStart().StartsWith('*') || LeitorCompleto.Casar(Consulta202, texto) is not { } noFio)
            return null;
        if (ligado is { } esperado && Ligado.Decodificar(noFio).Valor?.Ligado != esperado)
            return null;
        return noFio;
    };

    private static Valor? Decodificar202(string? noFio) => noFio is null ? null : Ligado.Decodificar(noFio).Valor;

    private static string Frase(string motivo) =>
        string.IsNullOrEmpty(motivo) ? motivo : char.ToUpperInvariant(motivo[0]) + motivo[1..] + (motivo.EndsWith('.') ? "" : ".");

    /// <summary>Como <see cref="Avisar"/>, mas não troca o "DESLIGUE O MÓDULO AGORA" do alarme.</summary>
    private void AvisarSemAlarme(string mensagem)
    {
        lock (_trava)
        {
            if (_alarme)
                return;
            _mensagem = mensagem;
        }
        Notificar();
    }

    private void Avisar(string? mensagem)
    {
        lock (_trava)
            _mensagem = mensagem;
        Notificar();
    }

    private void Notificar()
    {
        try
        {
            Mudou?.Invoke();
        }
        catch (Exception e)
        {
            _log.Evento($"falha no aviso do gerador ({e.GetType().Name})");
        }
    }
}
