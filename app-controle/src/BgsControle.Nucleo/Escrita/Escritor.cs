using BgsControle.Nucleo.Leitura;
using BgsControle.Nucleo.Registro;
using BgsControle.Nucleo.Rendimento;
using BgsControle.Nucleo.Seguranca;
using BgsControle.Nucleo.Setups;
using BgsControle.Nucleo.Uso;

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

    /// <summary>
    /// A cerca do limitador (<see cref="Rendimento.Cerca"/>, história 13a): para as escritas que vão sair
    /// (a pedida e a cópia do link, ou um lote inteiro), o motivo da recusa, ou null. Um valor null é uma
    /// escrita que não decodifica. Vale para toda origem, também com <c>daCopia</c>: a tela, a porta, a
    /// criação, a cópia do link, o aplicar e a reaplicação. Um lote conferido inteiro antes da primeira
    /// escrita (<c>loteConferido</c>) não é conferido de novo escrita a escrita.
    /// </summary>
    internal Func<IReadOnlyList<(Endereco Endereco, Valor? Valor)>, string?>? Cerca { get; set; }

    /// <summary>
    /// A cerca nos lotes que voltam (a volta à cópia e o desfazer): os limitadores que vão no teto em vez
    /// do valor da cópia (<see cref="Rendimento.Cerca.NaVolta"/>). Ligada com a <see cref="Cerca"/>.
    /// </summary>
    internal Func<IReadOnlyList<(Endereco Endereco, Valor? Valor)>, IReadOnlyList<TetoNaVolta>>? CercaNaVolta { get; set; }

    /// <summary>A recusa da <see cref="Cerca"/> para as escritas, ou null. Uma falha ao conferir recusa.</summary>
    internal string? ConferirCerca(IReadOnlyList<(Endereco Endereco, Valor? Valor)> escritas)
    {
        if (Cerca is not { } cerca || !escritas.Any(e => Rendimento.Cerca.Toca(e.Endereco.Codigo)))
            return null;
        try
        {
            return cerca(escritas);
        }
        catch (Exception e)
        {
            _log.Evento($"falha ao conferir o limite ({e.GetType().Name})");
            return Rendimento.Cerca.MsgFalhou;
        }
    }

    /// <summary>
    /// Os limitadores que um lote que volta escreve no teto (<see cref="CercaNaVolta"/>), ou vazio sem a
    /// cerca ligada. Uma falha ao calcular devolve null: o lote não sai (<see cref="Rendimento.Cerca.MsgFalhou"/>).
    /// </summary>
    internal IReadOnlyList<TetoNaVolta>? TetosNaVolta(IReadOnlyList<(Endereco Endereco, Valor? Valor)> escritas)
    {
        if (CercaNaVolta is not { } naVolta)
            return [];
        try
        {
            return naVolta(escritas);
        }
        catch (Exception e)
        {
            _log.Evento($"falha ao conferir o limite na volta ({e.GetType().Name})");
            return null;
        }
    }

    /// <summary>
    /// A recusa da cerca para um valor (como o aparelho o devolve) e, com <paramref name="espelhar"/>, a
    /// cópia do link, ou null: quem precisa saber antes de mexer em outra coisa (a criação da memória).
    /// </summary>
    internal string? ConferirCerca(Parametro parametro, int? indice, Valor? valor, bool espelhar) =>
        CercaDoPedido(parametro, indice, valor, espelhar, loteConferido: false);

    /// <summary>A recusa da cerca para o pedido e a cópia do link dele, ou null. Um pedido que não decodifica vai como desconhecido.</summary>
    private string? CercaDoPedido(Parametro parametro, int? indice, Valor? esperado, bool espelhar, bool loteConferido)
    {
        if (loteConferido || !Rendimento.Cerca.Toca(parametro.Codigo))
            return null;
        var escritas = new List<(Endereco, Valor?)> { (new Endereco(parametro.Codigo, indice), esperado) };
        if (espelhar)
        {
            int? outro;
            try
            {
                outro = Espelho?.Invoke(parametro, indice);
            }
            catch (Exception e)
            {
                _log.Evento($"falha ao conferir o link ({e.GetType().Name})");
                return Rendimento.Cerca.MsgFalhou;
            }
            if (outro is { } canal && canal != indice)
                escritas.Add((new Endereco(parametro.Codigo, canal), esperado));
        }
        return ConferirCerca(escritas);
    }

    /// <summary>
    /// A regra da saída sem calibração (<see cref="Rendimento.SemCalibracao"/>, história 13b): para os endereços
    /// que vão sair, o motivo da recusa, ou null. Vale só para a origem IA e só nas escritas avulsas (a pedida e
    /// a cópia do link); os lotes conferem inteiros antes (<c>loteConferido</c>), e os que voltam (a volta à cópia
    /// e o desfazer) nunca são recusados por ela. Ligada pelo <c>GerenteSetups</c>.
    /// </summary>
    internal Func<IReadOnlyList<Endereco>, string?>? SemCalibracao { get; set; }

    /// <summary>
    /// A recusa da regra "sem calibração" para o pedido e, com <paramref name="espelhar"/>, a cópia do link dele,
    /// qualquer que seja a origem (a criação da memória, que só tem ajustes da porta, confere antes de anotar a
    /// etapa). Uma falha ao conferir recusa.
    /// </summary>
    internal string? ConferirSemCalibracao(Parametro parametro, int? indice, bool espelhar)
    {
        if (SemCalibracao is not { } regra || !Rendimento.SemCalibracao.Vigia(parametro.Codigo))
            return null;
        try
        {
            var enderecos = new List<Endereco> { new(parametro.Codigo, indice) };
            if (espelhar && Espelho?.Invoke(parametro, indice) is { } outro && outro != indice)
                enderecos.Add(new Endereco(parametro.Codigo, outro));
            return regra(enderecos);
        }
        catch (Exception e)
        {
            _log.Evento($"falha ao conferir a calibração ({e.GetType().Name})");
            return Rendimento.SemCalibracao.MsgFalhou;
        }
    }

    /// <summary>A recusa "sem calibração" de um pedido avulso da IA, ou null (outra origem, ou um lote já conferido).</summary>
    private string? SemCalibracaoDoPedido(Parametro parametro, int? indice, bool espelhar, bool loteConferido) =>
        loteConferido || Origem.Atual != OrigemComando.Ia ? null : ConferirSemCalibracao(parametro, indice, espelhar);

    /// <summary>
    /// O master à mão com um nível em uso (história 13c): para o valor pedido do MASTER (<c>080</c>), o motivo da
    /// recusa ("Nível Alto: o master vai até −8,8 dB."), ou null. Abaixo da conta passa (baixar é seguro). Vale para
    /// a tela e a porta; o app (a conta do nível, origem App) e os lotes da cópia passam. Ligada pelo
    /// <c>GerenteSetups</c>.
    /// </summary>
    internal Func<decimal, string?>? MasterNivel { get; set; }

    /// <summary>A recusa do master à mão pedido acima da conta do nível, ou null. Uma falha ao conferir recusa.</summary>
    internal string? ConferirMaster(Parametro parametro, Valor? esperado, bool daCopia, bool loteConferido)
    {
        if (daCopia || loteConferido || parametro.Codigo != Camadas.CodigoMaster || Origem.Atual == OrigemComando.App
            || MasterNivel is not { } regra || esperado?.Numero is not { } valor)
            return null;
        try
        {
            return regra(valor);
        }
        catch (Exception e)
        {
            _log.Evento($"falha ao conferir o nível ({e.GetType().Name})");
            return Rendimento.Niveis.MsgFalhou;
        }
    }

    /// <summary>
    /// O descanso do Extra (história 13f): para um valor pedido, o motivo da recusa ("Descansando: 12:34."), ou null. Tirar o
    /// mudo (<c>100</c> = 0) no descanso desfaria a trava; vale para toda origem e para os lotes. Ligado pelo
    /// <c>TermicoExtra</c>; o religar dele, no fim do descanso, já passa.
    /// </summary>
    internal Func<Parametro, Valor?, string?>? Descanso { get; set; }

    /// <summary>A recusa do <see cref="Descanso"/>, ou null. Uma falha ao conferir recusa (o lado seguro).</summary>
    private string? ConferirDescanso(Parametro parametro, Valor? esperado)
    {
        if (Descanso is not { } regra)
            return null;
        try
        {
            return regra(parametro, esperado);
        }
        catch (Exception e)
        {
            _log.Evento($"falha ao conferir o descanso do Extra ({e.GetType().Name})");
            return Rendimento.Niveis.MsgFalhou;
        }
    }

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
    /// Com <paramref name="daCopia"/> (o Voltar à cópia, o aplicar de uma memória ou preset e os ajustes da criação
    /// da memória, que andam com a marca da cópia), passa pelo <see cref="Bloqueio"/>.
    /// </summary>
    /// <remarks>Com <paramref name="loteConferido"/>, a escrita é de um lote que a <see cref="Cerca"/> já conferiu inteiro antes da primeira escrita.</remarks>
    internal Task<ResultadoEscrita> EscreverAsync(Parametro parametro, int? indice, Valor valor, bool espelhar, bool daCopia = false,
        bool loteConferido = false)
    {
        if (valor.Numero is { } n)
            return EscreverAsync(parametro, indice, n, espelhar, daCopia, loteConferido);
        if (valor.Texto is { } t)
            return EscreverAsync(parametro, indice, t, espelhar, daCopia, loteConferido);
        return Task.FromResult(ResultadoEscrita.Falha(MsgIlegivel));
    }

    internal Task<ResultadoEscrita> EscreverAsync(Parametro parametro, int? indice, decimal valor, bool espelhar, bool daCopia = false,
        bool loteConferido = false)
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
        return Pedir(parametro, indice, quadro, espelhar, daCopia, loteConferido);
    }

    internal Task<ResultadoEscrita> EscreverAsync(Parametro parametro, int? indice, string texto, bool espelhar, bool daCopia = false,
        bool loteConferido = false)
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
        return Pedir(parametro, indice, quadro, espelhar, daCopia, loteConferido);
    }

    private async Task<ResultadoEscrita> Pedir(Parametro parametro, int? indice, string quadro, bool espelhar, bool daCopia, bool loteConferido)
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
        // Sem calibração (história 13b): a porta não mexe no nível de uma saída cuja tensão o app não sabe.
        if (SemCalibracaoDoPedido(parametro, indice, espelhar, loteConferido) is { } semCalibracao)
            return Recusar(quadro, semCalibracao);
        // A cerca, antes da fila: vale para toda origem, também com daCopia.
        if (CercaDoPedido(parametro, indice, esperado, espelhar, loteConferido) is { } cerca)
            return Recusar(quadro, cerca);
        // O master à mão acima da conta do nível em uso (história 13c), antes da fila, como a cerca.
        if (ConferirMaster(parametro, esperado, daCopia, loteConferido) is { } nivel)
            return Recusar(quadro, nivel);
        // O descanso do Extra (história 13f): o mudo da trava não sai até ele acabar.
        if (ConferirDescanso(parametro, esperado) is { } descanso)
            return Recusar(quadro, descanso);
        // A origem ambiente de quem pediu (tela, IA ou app) vai junto até o envio: a fila roda em
        // outra tarefa, e um pedido mais novo de outra origem pode tomar o lugar deste.
        var pedido = new Pedido(parametro, indice, quadro, esperado, espelhar, daCopia, loteConferido, Origem.Atual);
        var endereco = new Endereco(parametro.Codigo, indice);
        var tcs = new TaskCompletionSource<ResultadoEscrita>(TaskCreationOptions.RunContinuationsAsynchronously);
        bool iniciar;
        bool mexendo;
        Fila fila;
        List<Espera>? trocados = null;
        lock (_trava)
        {
            if (!_filas.TryGetValue(endereco, out fila!))
                _filas[endereco] = fila = new Fila(endereco);
            // A tela e a IA não se juntam no mesmo endereço: um pedido da tela que ainda não saiu
            // ganha do da IA (o usuário está mexendo nisso), e cada um termina com o seu resultado.
            var pendente = fila.Pendente?.Origem;
            mexendo = pendente == OrigemComando.Tela && pedido.Origem == OrigemComando.Ia;
            iniciar = false;
            if (!mexendo)
            {
                if (pendente == OrigemComando.Ia && pedido.Origem == OrigemComando.Tela)
                {
                    trocados = fila.Esperando.FindAll(e => e.Origem == OrigemComando.Ia);
                    fila.Esperando.RemoveAll(e => e.Origem == OrigemComando.Ia);
                }
                fila.Pendente = pedido;
                fila.Esperando.Add(new Espera(tcs, pedido.Origem));
                iniciar = !fila.Rodando;
                fila.Rodando = true;
            }
        }
        if (mexendo)
            return Recusar(quadro, UsoCompartilhado.MsgMexendo);
        if (trocados is not null)
        {
            foreach (var e in trocados)
                e.Tcs.TrySetResult(ResultadoEscrita.Falha(UsoCompartilhado.MsgMexendo));
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
                List<Espera> esperando;
                lock (_trava)
                {
                    pedido = fila.Pendente!;
                    esperando = fila.Esperando;
                    fila.Pendente = null;
                    fila.Esperando = [];
                }

                ResultadoEscrita r;
                // O quadro sai com a origem do pedido que foi enviado (o mais novo), e a cópia do
                // link também.
                using (Origem.Como(pedido.Origem))
                {
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
                }

                foreach (var t in esperando)
                    t.Tcs.TrySetResult(r);
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
                List<Espera> sobra;
                lock (_trava)
                {
                    fila.Rodando = false;
                    fila.Pendente = null;
                    sobra = fila.Esperando;
                    fila.Esperando = [];
                }
                foreach (var t in sobra)
                    t.Tcs.TrySetResult(ResultadoEscrita.Falha("Erro inesperado na fila de escrita."));
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
            // A cópia do link passa pela trava como a mestre: só a escrita que já passava (a da criação da memória) passa.
            var espelhada = await EscreverAsync(pedido.Parametro, canal, valor, espelhar: false, daCopia: pedido.DaCopia);
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
            // A cerca de novo, com a porta: o estado agora já tem as escritas que saíram antes desta.
            if (CercaDoPedido(pedido.Parametro, pedido.Indice, pedido.Esperado, pedido.Espelhar, pedido.LoteConferido) is { } cerca)
                return Recusar(pedido.Quadro, cerca);

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

    private sealed record Pedido(Parametro Parametro, int? Indice, string Quadro, Valor? Esperado, bool Espelhar, bool DaCopia, bool LoteConferido,
        OrigemComando Origem);

    /// <summary>Quem espera uma escrita, com a origem de quem pediu.</summary>
    private sealed record Espera(TaskCompletionSource<ResultadoEscrita> Tcs, OrigemComando Origem);

    private sealed class Fila(Endereco endereco)
    {
        public Endereco Endereco { get; } = endereco;
        public Pedido? Pendente { get; set; }
        public List<Espera> Esperando { get; set; } = [];
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
