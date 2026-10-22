using BgsControle.Nucleo.Leitura;
using BgsControle.Nucleo.Registro;

namespace BgsControle.Nucleo.Escrita;

/// <summary>Ritmo e prazos da escrita (MAPA §2). Os testes usam o relógio falso.</summary>
/// <param name="Intervalo">Entre dois envios do escritor (~30 ms).</param>
/// <param name="Arrasto">
/// Entre o fim de uma escrita de um parâmetro e a próxima do mesmo parâmetro (300 ms). Contado
/// do fim (eco ou releitura), não do envio: fica no máximo uma escrita a cada 300 ms.
/// </param>
/// <param name="PrazoEco">Sem eco neste prazo, relê o parâmetro (2 s).</param>
/// <param name="PrazoReleitura">O prazo da releitura (5 s, como uma consulta da leitura completa).</param>
public sealed record TemposEscrita(TimeSpan Intervalo, TimeSpan Arrasto, TimeSpan PrazoEco, TimeSpan PrazoReleitura)
{
    public static TemposEscrita Padrao { get; } = new(
        Intervalo: TimeSpan.FromMilliseconds(30),
        Arrasto: TimeSpan.FromMilliseconds(300),
        PrazoEco: TimeSpan.FromSeconds(2),
        PrazoReleitura: TimeSpan.FromSeconds(5));
}

/// <summary>Como terminou uma escrita.</summary>
/// <param name="Valor">O valor que o aparelho confirmou (eco ou releitura). Vale este, não o pedido.</param>
/// <param name="Relido">Sem eco: o valor veio da releitura.</param>
/// <param name="Diferente">O aparelho ficou com um valor diferente do pedido.</param>
/// <param name="Aviso">
/// A escrita valeu, mas algo à volta dela não: com o link ligado, a outra saída não acompanhou
/// ("A saída 2 não acompanhou.").
/// </param>
public sealed record ResultadoEscrita(bool Ok, string? Motivo, Valor? Valor = null, bool Relido = false, bool Diferente = false, string? Aviso = null)
{
    public static ResultadoEscrita Falha(string motivo) => new(false, motivo);
}

/// <summary>
/// Escreve os ajustes de áudio, sempre pela porta da sessão (<see cref="Sessao.EnviarAsync"/>):
/// <list type="bullet">
/// <item>uma escrita por vez, com ~30 ms entre envios;</item>
/// <item>por parâmetro, só o último valor pedido vai, no máximo uma escrita a cada 300 ms; o
/// último valor sempre vai (arrastar e soltar, segurar − e +);</item>
/// <item>o eco no formato de leitura confirma; sem eco em 2 s, relê; vale o que o aparelho
/// devolveu, e o espelho (<see cref="EstadoAparelho"/>) é atualizado com ele;</item>
/// <item>sem eco e sem releitura, a escrita é uma falha com o motivo.</item>
/// </list>
/// Antes da primeira leitura completa da conexão, e durante uma leitura, nada é escrito: a
/// leitura completa e cada escrita com a sua confirmação passam pela mesma exclusão da sessão.
/// </summary>
public sealed class Escritor
{
    public const string MsgSemLeitura = "O app ainda não leu o aparelho.";
    public const string MsgLendo = "O app está lendo o aparelho.";
    public const string MsgCaiu = "A conexão caiu antes da confirmação.";
    public const string MsgSemConfirmacao = "O aparelho não confirmou: sem eco e sem resposta à releitura.";
    public const string MsgIlegivel = "O aparelho respondeu um valor ilegível.";
    public const string MsgLinkFalhou = "O link falhou: a outra saída pode não ter acompanhado.";

    public static string MsgNaoAcompanhou(int canal) => $"A saída {canal + 1} não acompanhou.";

    private readonly Sessao _sessao;
    private readonly EstadoAparelho _estado;
    private readonly LogBle _log;
    private readonly TemposEscrita _tempos;
    private readonly TimeProvider _relogio;

    private readonly object _trava = new();
    private readonly Dictionary<Endereco, Fila> _filas = [];
    private DateTimeOffset? _ultimoEnvio;
    private Aguardo? _aguardo;

    public Escritor(Sessao sessao, EstadoAparelho estado, LogBle log, TemposEscrita? tempos = null, TimeProvider? relogio = null)
    {
        _sessao = sessao;
        _estado = estado;
        _log = log;
        _tempos = tempos ?? TemposEscrita.Padrao;
        _relogio = relogio ?? TimeProvider.System;
        _sessao.Recebeu += AoReceber;
        _sessao.Mudou += AoMudarSessao;
    }

    /// <summary>Uma escrita terminou (com sucesso ou não). Pode chegar em qualquer thread.</summary>
    public event Action<Endereco, ResultadoEscrita>? Escreveu;

    /// <summary>
    /// O link de canais (<see cref="LinkCanais"/>): para um parâmetro e canal, o outro canal que
    /// recebe a mesma escrita, ou null. Uma escrita pedida pela tela que o aparelho confirmou é
    /// repetida, com o valor confirmado, no outro canal, e a tarefa só termina depois das duas.
    /// </summary>
    internal Func<Parametro, int?, int?>? Espelho { get; set; }

    /// <summary>
    /// A cópia antes (<see cref="CopiaAntes"/>): o motivo para não escrever agora (uma operação de
    /// memória anda ou ficou pela metade), ou null. Vale para a tela e para a cópia do link; só o
    /// Voltar à cópia passa (<c>daCopia</c>).
    /// </summary>
    internal Func<string?>? Bloqueio { get; set; }

    /// <summary>O motivo do <see cref="Bloqueio"/> agora, ou null.</summary>
    internal string? MotivoBloqueio()
    {
        try
        {
            return Bloqueio?.Invoke();
        }
        catch (Exception e)
        {
            _log.Evento($"falha ao conferir a operação em andamento ({e.GetType().Name})");
            return CopiaAntes.MsgOutraOperacao;
        }
    }

    /// <summary>
    /// Pede um valor numérico (ganho, frequência, filtro, fase, booleano 0/1, limitador 0.5 =
    /// OFF). Um pedido mais novo do mesmo parâmetro substitui o que ainda não saiu; a tarefa
    /// termina com a escrita que levou o pedido (ou um mais novo). Com o link ligado, a escrita
    /// na saída mestre também vai para a outra.
    /// </summary>
    public Task<ResultadoEscrita> EscreverAsync(Parametro parametro, int? indice, decimal valor) =>
        EscreverAsync(parametro, indice, valor, espelhar: true);

    /// <summary>Pede um texto (o roteamento: <c>a</c>, <c>b</c>, <c>a+b</c>).</summary>
    public Task<ResultadoEscrita> EscreverAsync(Parametro parametro, int? indice, string texto) =>
        EscreverAsync(parametro, indice, texto, espelhar: true);

    /// <summary>
    /// Escreve um valor como o aparelho o devolveu (número ou texto). Sem <paramref name="espelhar"/>,
    /// o link não repete a escrita: a cópia do link e a volta à cópia escrevem cada canal por si.
    /// Com <paramref name="daCopia"/> (só o Voltar à cópia), passa pelo <see cref="Bloqueio"/>.
    /// </summary>
    internal Task<ResultadoEscrita> EscreverAsync(Parametro parametro, int? indice, Valor valor, bool espelhar, bool daCopia = false)
    {
        if (valor.Numero is { } n)
            return EscreverAsync(parametro, indice, n, espelhar, daCopia);
        if (valor.Texto is { } t)
            return EscreverAsync(parametro, indice, t, espelhar, daCopia);
        return Task.FromResult(ResultadoEscrita.Falha(MsgIlegivel));
    }

    internal Task<ResultadoEscrita> EscreverAsync(Parametro parametro, int? indice, decimal valor, bool espelhar, bool daCopia = false)
    {
        string quadro;
        try
        {
            quadro = parametro.CodificarEscrita(indice, valor);
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException)
        {
            return Task.FromResult(ResultadoEscrita.Falha(e.Message));
        }
        return Pedir(parametro, indice, quadro, espelhar, daCopia);
    }

    internal Task<ResultadoEscrita> EscreverAsync(Parametro parametro, int? indice, string texto, bool espelhar, bool daCopia = false)
    {
        string quadro;
        try
        {
            quadro = parametro.CodificarEscrita(indice, texto);
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException)
        {
            return Task.FromResult(ResultadoEscrita.Falha(e.Message));
        }
        return Pedir(parametro, indice, quadro, espelhar, daCopia);
    }

    private async Task<ResultadoEscrita> Pedir(Parametro parametro, int? indice, string quadro, bool espelhar, bool daCopia)
    {
        if (Catalogo.EscritaDeAudio(quadro) is not { } escrita)
        {
            // Fora dos ajustes de áudio: a sessão recusa e registra o motivo.
            var envio = await _sessao.EnviarAsync(quadro);
            return ResultadoEscrita.Falha(envio.Motivo ?? Sessao.MotivoNaoLiberado);
        }

        var esperado = parametro.Decodificar(escrita.NoFio).Valor;
        if (!daCopia && MotivoBloqueio() is { } bloqueio)
            return Recusar(quadro, bloqueio);
        var pedido = new Pedido(parametro, indice, quadro, esperado, espelhar, daCopia);
        var endereco = new Endereco(parametro.Codigo, indice);
        var tcs = new TaskCompletionSource<ResultadoEscrita>(TaskCreationOptions.RunContinuationsAsynchronously);
        bool iniciar;
        Fila fila;
        lock (_trava)
        {
            if (!_filas.TryGetValue(endereco, out fila!))
                _filas[endereco] = fila = new Fila(endereco);
            fila.Pendente = pedido;
            fila.Esperando.Add(tcs);
            iniciar = !fila.Rodando;
            fila.Rodando = true;
        }
        if (iniciar)
            _ = Task.Run(() => RodarAsync(fila));
        return await tcs.Task;
    }

    /// <summary>Envia o pedido pendente do parâmetro, um de cada vez, até não sobrar nenhum.</summary>
    private async Task RodarAsync(Fila fila)
    {
        var acabou = false;
        try
        {
            while (true)
            {
                TimeSpan espera;
                lock (_trava)
                {
                    if (fila.Pendente is null)
                    {
                        fila.Rodando = false;
                        acabou = true;
                        return;
                    }
                    espera = fila.LiberaEm is { } libera ? libera - _relogio.GetUtcNow() : TimeSpan.Zero;
                }
                if (espera > TimeSpan.Zero)
                    await Task.Delay(espera, _relogio);

                Pedido pedido;
                List<TaskCompletionSource<ResultadoEscrita>> esperando;
                lock (_trava)
                {
                    pedido = fila.Pendente!;
                    esperando = fila.Esperando;
                    fila.Pendente = null;
                    fila.Esperando = [];
                }

                ResultadoEscrita r;
                try
                {
                    r = await EnviarEConfirmarAsync(pedido);
                }
                catch (Exception e)
                {
                    _log.Evento($"falha na escrita ({e.GetType().Name})");
                    r = ResultadoEscrita.Falha($"Erro inesperado ({e.GetType().Name}).");
                }
                lock (_trava)
                    fila.LiberaEm = _relogio.GetUtcNow() + _tempos.Arrasto;

                r = await EspelharAsync(pedido, r);

                foreach (var t in esperando)
                    t.TrySetResult(r);
                try
                {
                    Escreveu?.Invoke(fila.Endereco, r);
                }
                catch (Exception e)
                {
                    // Quem ouve não pode parar a fila do parâmetro.
                    _log.Evento($"falha no aviso de escrita ({e.GetType().Name})");
                }
            }
        }
        finally
        {
            if (!acabou)
            {
                // Saída inesperada: a fila volta a aceitar pedidos e ninguém fica esperando.
                List<TaskCompletionSource<ResultadoEscrita>> sobra;
                lock (_trava)
                {
                    fila.Rodando = false;
                    fila.Pendente = null;
                    sobra = fila.Esperando;
                    fila.Esperando = [];
                }
                foreach (var t in sobra)
                    t.TrySetResult(ResultadoEscrita.Falha("Erro inesperado na fila de escrita."));
            }
        }
    }

    /// <summary>
    /// Com o link ligado, repete no outro canal o valor que o aparelho confirmou na mestre. Se a
    /// outra saída não acompanhar (falha ou valor diferente), o resultado da mestre leva o aviso.
    /// Nunca lança: quem espera a mestre não pode ficar pendurado.
    /// </summary>
    private async Task<ResultadoEscrita> EspelharAsync(Pedido pedido, ResultadoEscrita r)
    {
        if (!pedido.Espelhar || !r.Ok || r.Valor is not { } valor)
            return r;
        try
        {
            var outro = Espelho?.Invoke(pedido.Parametro, pedido.Indice);
            if (outro is not { } canal || canal == pedido.Indice)
                return r;
            var espelhada = await EscreverAsync(pedido.Parametro, canal, valor, espelhar: false);
            if (espelhada.Ok && !espelhada.Diferente)
                return r;
            _log.Evento($"{pedido.Quadro}: o link não levou à saída {canal + 1} " +
                        $"({espelhada.Motivo ?? $"ficou em {espelhada.Valor?.NoFio}"})");
            return r with { Aviso = MsgNaoAcompanhou(canal) };
        }
        catch (Exception e)
        {
            _log.Evento($"falha no link ({e.GetType().Name})");
            return r with { Aviso = MsgLinkFalhou };
        }
    }

    private async Task<ResultadoEscrita> EnviarEConfirmarAsync(Pedido pedido)
    {
        if (!SessaoAberta)
            return Recusar(pedido.Quadro, Sessao.MotivoSemConexao);
        if (_estado.Lendo)
            return Recusar(pedido.Quadro, MsgLendo); // não espera a leitura inteira (~13 s)

        // Uma escrita de cada vez, e nunca junto com uma leitura completa.
        var porta = _sessao.ExclusaoLeituraEscrita;
        await porta.WaitAsync();
        try
        {
            if (!SessaoAberta)
                return Recusar(pedido.Quadro, Sessao.MotivoSemConexao);
            if (_estado.Lendo)
                return Recusar(pedido.Quadro, MsgLendo);
            if (!_estado.Pronto)
                return Recusar(pedido.Quadro, MsgSemLeitura);
            // Conferido de novo com a porta: um pedido da fila não entra no meio de uma memória.
            if (!pedido.DaCopia && MotivoBloqueio() is { } bloqueio)
                return Recusar(pedido.Quadro, bloqueio);

            var consulta = new Consulta(pedido.Parametro, pedido.Indice);
            var (envio, resposta) = await EnviarEEsperarAsync(pedido.Quadro, consulta, _tempos.PrazoEco);
            if (!envio.Enviado)
                return ResultadoEscrita.Falha(envio.Motivo ?? "não enviado");

            var relido = false;
            if (resposta is null)
            {
                if (!SessaoAberta)
                    return ResultadoEscrita.Falha(MsgCaiu);
                _log.Evento($"{pedido.Quadro}: sem eco em {_tempos.PrazoEco.TotalSeconds:0.#} s; relendo");
                (envio, resposta) = await EnviarEEsperarAsync(consulta.Texto, consulta, _tempos.PrazoReleitura);
                if (!envio.Enviado)
                    return ResultadoEscrita.Falha(envio.Motivo ?? "não enviado");
                if (resposta is null)
                {
                    var motivo = SessaoAberta ? MsgSemConfirmacao : MsgCaiu;
                    _log.Evento($"{pedido.Quadro}: {motivo}");
                    return ResultadoEscrita.Falha(motivo);
                }
                relido = true;
            }

            if (pedido.Parametro.Decodificar(resposta).Valor is not { } valor)
            {
                _log.Evento($"{pedido.Quadro}: {MsgIlegivel} ({resposta})");
                return ResultadoEscrita.Falha(MsgIlegivel);
            }
            _estado.Atualizar(new Endereco(pedido.Parametro.Codigo, pedido.Indice), valor);
            var diferente = !Igual(pedido.Esperado, valor);
            if (diferente)
                _log.Evento($"{pedido.Quadro}: o aparelho ficou em {valor.NoFio}");
            return new ResultadoEscrita(true, null, valor, relido, diferente);
        }
        finally
        {
            porta.Release();
        }
    }

    /// <summary>Envia pela sessão e espera a resposta que casa com a consulta. Null = sem resposta.</summary>
    private async Task<(ResultadoEnvio Envio, string? Resposta)> EnviarEEsperarAsync(string quadro, Consulta consulta, TimeSpan prazo)
    {
        TimeSpan espera = TimeSpan.Zero;
        lock (_trava)
        {
            if (_ultimoEnvio is { } ultimo)
                espera = ultimo + _tempos.Intervalo - _relogio.GetUtcNow();
        }
        if (espera > TimeSpan.Zero)
            await Task.Delay(espera, _relogio);

        // O aguardo existe antes do envio: o eco pode chegar antes de a escrita voltar.
        var aguardo = new Aguardo(consulta);
        lock (_trava)
            _aguardo = aguardo;
        try
        {
            var envio = await _sessao.EnviarAsync(quadro);
            if (!envio.Enviado)
                return (envio, null);
            lock (_trava)
                _ultimoEnvio = _relogio.GetUtcNow();
            if (!SessaoAberta)
                aguardo.Resposta.TrySetResult(null);

            using var cancelar = new CancellationTokenSource();
            var limite = Task.Delay(prazo, _relogio, cancelar.Token);
            await Task.WhenAny(aguardo.Resposta.Task, limite);
            cancelar.Cancel();
            return (envio, aguardo.Resposta.Task.IsCompleted ? await aguardo.Resposta.Task : null);
        }
        finally
        {
            lock (_trava)
            {
                if (_aguardo == aguardo)
                    _aguardo = null;
            }
        }
    }

    private ResultadoEscrita Recusar(string quadro, string motivo)
    {
        _log.Recusado(quadro, motivo);
        return ResultadoEscrita.Falha(motivo);
    }

    private static bool Igual(Valor? pedido, Valor recebido)
    {
        if (pedido is null)
            return false;
        if (pedido.Numero is { } a && recebido.Numero is { } b)
            return a == b;
        return pedido.Texto is not null && pedido.Texto == recebido.Texto;
    }

    private bool SessaoAberta => _sessao.Estado == EstadoSessao.Conectado && _sessao.SegurandoConexao;

    private void AoReceber(string texto)
    {
        Aguardo? alvo;
        lock (_trava)
            alvo = _aguardo;
        if (alvo is not null && LeitorCompleto.Casar(alvo.Consulta, texto) is { } noFio)
            alvo.Resposta.TrySetResult(noFio);
    }

    private void AoMudarSessao()
    {
        if (SessaoAberta)
            return;
        Aguardo? alvo;
        lock (_trava)
            alvo = _aguardo;
        alvo?.Resposta.TrySetResult(null);
    }

    private sealed record Pedido(Parametro Parametro, int? Indice, string Quadro, Valor? Esperado, bool Espelhar, bool DaCopia);

    private sealed class Fila(Endereco endereco)
    {
        public Endereco Endereco { get; } = endereco;
        public Pedido? Pendente { get; set; }
        public List<TaskCompletionSource<ResultadoEscrita>> Esperando { get; set; } = [];
        public bool Rodando { get; set; }
        public DateTimeOffset? LiberaEm { get; set; }
    }

    private sealed class Aguardo(Consulta consulta)
    {
        public Consulta Consulta { get; } = consulta;

        /// <summary>O valor no fio, ou null se a conexão caiu.</summary>
        public TaskCompletionSource<string?> Resposta { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
