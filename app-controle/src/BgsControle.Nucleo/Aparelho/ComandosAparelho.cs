using BgsControle.Nucleo.Escrita;
using BgsControle.Nucleo.Gerador;
using BgsControle.Nucleo.Leitura;
using BgsControle.Nucleo.Memorias;
using BgsControle.Nucleo.Protocolo;
using BgsControle.Nucleo.Registro;
using BgsControle.Nucleo.Seguranca;

namespace BgsControle.Nucleo.Aparelho;

/// <summary>Como terminou um comando de aparelho. <see cref="Mensagem"/> é a frase para a tela (nunca com uma senha).</summary>
public sealed record ResultadoAparelho(bool Ok, string? Motivo, string? Mensagem = null)
{
    public static ResultadoAparelho Falha(string motivo, string? mensagem = null) => new(false, motivo, mensagem ?? motivo);
}

/// <summary>
/// Os comandos de aparelho da história 7b (CAP-9 e CAP-10), sempre pela porta da sessão
/// (<see cref="Sessao.EnviarAsync"/>), com a chave de cada um (a trava da sessão confere):
/// <list type="bullet">
/// <item><b>Remote out</b> (<c>*252*0/1*</c>) e <b>Bluetooth de áudio</b> (<c>*251*0/1*</c>):
/// envia e confirma pelo eco ou pela releitura de <c>#252#</c> / <c>#251#</c>
/// (Suposição ECO-251-252); o espelho fica com o valor confirmado.</item>
/// <item><b>Aparelho inteiro</b>: reset de áudio (<c>*242*</c>), reset de fábrica (<c>*004*</c>),
/// troca de senha (<c>*001*ANTIGA*NOVA*</c>) e nome do produto (<c>*002*NOME*</c>). Cada um
/// espera a sua resposta e depois faz uma leitura completa nova. Depois do reset de fábrica, o
/// app esquece a senha guardada e desconecta; com <c>#001#OK#</c>, guarda a senha nova. Ficam
/// recusados, sem enviar nada, sem leitura ou lendo, com uma operação de memória andando ou pela
/// metade, com o gerador talvez ligado e com outro comando de aparelho andando.</item>
/// </list>
/// Um comando de aparelho por vez. Nenhuma senha vai para o log: as duas da troca entram na
/// máscara do <see cref="LogBle"/> antes de qualquer envio.
/// </summary>
public sealed class ComandosAparelho
{
    /// <summary>
    /// Quanto esperar a resposta de um comando de aparelho inteiro. Nenhuma foi medida
    /// (Suposição RESET-004, Suposição RESET-242, Suposição ACK-001, Suposição ACK-002); 5 s dão folga.
    /// </summary>
    public static readonly TimeSpan PrazoRespostaPadrao = TimeSpan.FromSeconds(5);

    public const string MsgOcupado = "Outro comando do aparelho está em andamento.";
    public const string MsgGeradorEmUso = "O gerador está em uso.";
    public const string MsgCaiu = "A conexão caiu antes da resposta do aparelho.";
    public const string MsgSemConfirmacao = "O aparelho não confirmou: sem eco e sem resposta à releitura.";
    public const string MsgIlegivel = "O aparelho respondeu um valor ilegível.";
    public const string MsgNomeInvalido = GerenteMemorias.MsgNomeInvalido;
    public const string MsgSenhaInvalida = Sessao.MsgSenhaInvalida;
    public const string MsgSenhaIgual = "A senha nova é igual à atual.";
    public const string MsgSenhasDiferentes = "As duas senhas novas não são iguais.";
    public const string MsgSenhaRecusada = "O aparelho recusou a senha atual.";
    public const string MsgSenhaTrocada = "Senha trocada. O celular guardou a senha nova.";
    public const string MsgSenhaNaoGuardada = "Senha trocada, mas não consegui guardar a senha nova no celular. Na próxima conexão, digite a senha nova.";
    public const string MsgSenhaSemResposta = "O aparelho não confirmou a troca de senha. A senha nova pode já valer: anote-a. A senha guardada no celular não mudou.";
    public const string MsgResetAudio = "Reset de áudio feito. O app releu o aparelho.";
    public const string MsgResetAudioSemResposta = "O aparelho não confirmou o reset de áudio. O app releu o aparelho.";
    public const string MsgResetFabrica = "Reset de fábrica feito. A senha foi apagada do celular: a próxima conexão pede a senha.";
    public const string MsgResetFabricaSemResposta = "O aparelho não confirmou o reset de fábrica. O app releu o aparelho.";
    public const string MsgNomeSemResposta = "O aparelho não confirmou o nome do produto.";
    public const string MsgLeituraParou = " A leitura depois parou no meio.";

    /// <summary>O aparelho respondeu outro nome (ou nenhum).</summary>
    public static string MsgNomeDiferente(string nome) =>
        string.IsNullOrEmpty(nome) ? "O aparelho ficou sem nome." : $"O aparelho ficou com o nome {nome}.";

    /// <summary>"Nome do produto: SALA 1".</summary>
    public static string MsgNomeProduto(string nome) => $"Nome do produto: {nome}";

    public static string MsgRemoteOut(bool ligado) => ligado ? "Remote out ligado." : "Remote out desligado.";

    public static string MsgBluetoothAudio(bool ligado) => ligado ? "Bluetooth de áudio ligado." : "Bluetooth de áudio desligado.";

    private static readonly Consulta Consulta252 = Catalogo.ConsultaDe("#252#")!;
    private static readonly Consulta Consulta251 = Catalogo.ConsultaDe("#251#")!;

    private readonly Sessao _sessao;
    private readonly EstadoAparelho _estado;
    private readonly LeituraAutomatica _leitura;
    private readonly CopiaAntes _copia;
    private readonly ICofreSenha _cofre;
    private readonly LogBle _log;
    private readonly GerenteMemorias? _memorias;
    private readonly GeradorSinal? _gerador;
    private readonly EnvioComResposta _envio;
    private readonly TimeSpan _prazoResposta;
    private int _ocupado;

    public ComandosAparelho(
        Sessao sessao,
        EstadoAparelho estado,
        LeituraAutomatica leitura,
        CopiaAntes copia,
        ICofreSenha cofre,
        LogBle log,
        GerenteMemorias? memorias = null,
        GeradorSinal? gerador = null,
        TemposEscrita? tempos = null,
        TimeProvider? relogio = null,
        TimeSpan? prazoResposta = null)
    {
        _sessao = sessao;
        _estado = estado;
        _leitura = leitura;
        _copia = copia;
        _cofre = cofre;
        _log = log;
        _memorias = memorias;
        _gerador = gerador;
        _envio = new EnvioComResposta(sessao, tempos, relogio);
        _prazoResposta = prazoResposta ?? PrazoRespostaPadrao;
    }

    /// <summary>Começou ou acabou um comando. Pode chegar em qualquer thread.</summary>
    public event Action? Mudou;

    /// <summary>Um comando de aparelho está andando (do envio até o fim da leitura nova).</summary>
    public bool Ocupado => Volatile.Read(ref _ocupado) == 1;

    private bool SessaoAberta => _sessao.Estado == EstadoSessao.Conectado && _sessao.SegurandoConexao;

    /// <summary>
    /// Por que um comando de aparelho inteiro (resets, senha, nome) não pode sair agora, fora a
    /// trava da sessão e a leitura: uma operação de memória anda ou ficou pela metade, o gerador
    /// pode estar ligado, outro comando de aparelho anda. Null = pode. Não envia nada.
    /// </summary>
    public string? MotivoBloqueio() => BloqueioDeFora() ?? (Ocupado ? MsgOcupado : null);

    /// <summary>O <see cref="MotivoBloqueio"/> sem contar o próprio comando que anda.</summary>
    private string? BloqueioDeFora()
    {
        if (_copia.MotivoBloqueio() is { } copia)
            return copia;
        if (_memorias?.Ocupado == true)
            return GerenteMemorias.MsgOcupado;
        if (_gerador is { } g && (g.TalvezLigado || g.Estado != EstadoGerador.Desligado))
            return MsgGeradorEmUso;
        return null;
    }

    // ------------------------------------------------------------------ remote out e Bluetooth de áudio

    /// <summary>Liga ou desliga o remote out (<c>*252*1*</c> / <c>*252*0*</c>), confirmado pelo eco ou pela releitura.</summary>
    public Task<ResultadoAparelho> DefinirRemoteOutAsync(bool ligar) =>
        LigarDesligarAsync(Catalogo.RemoteOut(ligar), Consulta252, ligar, MsgRemoteOut(ligar), "Remote out");

    /// <summary>Liga ou desliga o Bluetooth de áudio (<c>*251*1*</c> / <c>*251*0*</c>), confirmado pelo eco ou pela releitura.</summary>
    public Task<ResultadoAparelho> DefinirBluetoothAudioAsync(bool ligar) =>
        LigarDesligarAsync(Catalogo.BluetoothAudio(ligar), Consulta251, ligar, MsgBluetoothAudio(ligar), "Bluetooth de áudio");

    private async Task<ResultadoAparelho> LigarDesligarAsync(string quadro, Consulta consulta, bool ligar, string frase, string nome)
    {
        if (Recusa(quadro, aparelhoInteiro: false) is { } recusa)
            return recusa;
        if (!Ocupar())
            return ResultadoAparelho.Falha(MsgOcupado);
        try
        {
            var p = consulta.Parametro;
            // Suposição ECO-251-252: o 251 e o 252 respondem com o eco no formato de leitura.
            Func<string, string?> casar = texto => texto.TrimStart().StartsWith('*') ? null : LeitorCompleto.Casar(consulta, texto);
            ResultadoEnvio envio;
            string? resposta;
            var porta = _sessao.ExclusaoLeituraEscrita;
            await porta.WaitAsync();
            try
            {
                // O estado pode ter mudado na fila da porta (uma leitura, por exemplo): nada sai.
                if (Impedimento(quadro, aparelhoInteiro: false) is { } impedimento)
                    return Terminar(impedimento);
                (envio, resposta) = await _envio.EnviarEEsperarAsync(quadro, casar, _envio.Tempos.PrazoEco);
                if (envio.Enviado && resposta is null && SessaoAberta)
                {
                    _log.Evento($"{quadro}: sem eco em {_envio.Tempos.PrazoEco.TotalSeconds:0.#} s; relendo");
                    // Vale o primeiro envio: o comando saiu, mesmo que a releitura não saia.
                    (_, resposta) = await _envio.EnviarEEsperarAsync(consulta.Texto, casar, _envio.Tempos.PrazoReleitura);
                }
            }
            finally
            {
                porta.Release();
            }

            if (!envio.Enviado)
                return Terminar(ResultadoAparelho.Falha(Frase(envio.Motivo ?? Sessao.MotivoSemConexao)));
            if (resposta is null)
                return Terminar(ResultadoAparelho.Falha(SessaoAberta ? MsgSemConfirmacao : MsgCaiu));
            if (p.Decodificar(resposta).Valor is not { } valor)
                return Terminar(ResultadoAparelho.Falha(MsgIlegivel));

            // O espelho fica com o valor confirmado, mesmo que seja outro.
            _estado.Atualizar(new Endereco(p.Codigo, null), valor);
            if (valor.Ligado != ligar)
            {
                _log.Evento($"{quadro}: o aparelho ficou em {valor.NoFio}");
                return Terminar(ResultadoAparelho.Falha($"{nome}: o aparelho ficou {valor.Exibicao}."));
            }
            return Terminar(new ResultadoAparelho(true, null, frase));
        }
        finally
        {
            Desocupar();
        }
    }

    // ------------------------------------------------------------------ aparelho inteiro

    /// <summary>Reset de áudio (<c>*242*</c>): espera <c>#242#</c> e relê o aparelho. O estado resultante é imprevisível.</summary>
    public async Task<ResultadoAparelho> ResetAudioAsync()
    {
        var quadro = Catalogo.ResetAudio;
        if (Recusa(quadro, aparelhoInteiro: true) is { } recusa)
            return recusa;
        if (!Ocupar())
            return ResultadoAparelho.Falha(MsgOcupado);
        try
        {
            _log.Evento("aparelho: reset de áudio");
            // Suposição RESET-242: o ack é #242#.
            var (envio, ack) = await EnviarEEsperarAckAsync(quadro, CasarAck(Catalogo.CodigoResetAudio));
            if (!envio.Enviado)
                return Terminar(ResultadoAparelho.Falha(Frase(envio.Motivo ?? Sessao.MotivoSemConexao)));
            if (ack is null && !SessaoAberta)
                return Terminar(ResultadoAparelho.Falha(MsgCaiu));
            var lida = await LerDepoisAsync();
            var frase = (ack is null ? MsgResetAudioSemResposta : MsgResetAudio) + (lida ? "" : MsgLeituraParou);
            return Terminar(ack is null ? ResultadoAparelho.Falha(frase) : new ResultadoAparelho(true, null, frase));
        }
        finally
        {
            Desocupar();
        }
    }

    /// <summary>
    /// Reset de fábrica (<c>*004*</c>): espera <c>#004#</c>, relê o aparelho e, com a resposta,
    /// esquece a senha guardada e desconecta (a próxima conexão pede a senha). Apaga a senha e
    /// as 3 memórias do aparelho.
    /// </summary>
    public async Task<ResultadoAparelho> ResetFabricaAsync()
    {
        var quadro = Catalogo.ResetFabrica;
        if (Recusa(quadro, aparelhoInteiro: true) is { } recusa)
            return recusa;
        if (!Ocupar())
            return ResultadoAparelho.Falha(MsgOcupado);
        try
        {
            _log.Evento("aparelho: reset de fábrica");
            // Suposição RESET-004: o ack é #004#, e a conexão continua depois do reset.
            var (envio, ack) = await EnviarEEsperarAckAsync(quadro, CasarAck(Catalogo.CodigoResetFabrica));
            if (!envio.Enviado)
                return Terminar(ResultadoAparelho.Falha(Frase(envio.Motivo ?? Sessao.MotivoSemConexao)));
            if (ack is null)
            {
                // Sem a resposta, o app não sabe se a senha mudou: fica a guardada. Se ela não
                // valer mais, a próxima conexão recebe ERRO, apaga a senha e pede a nova.
                if (!SessaoAberta)
                    return Terminar(ResultadoAparelho.Falha(MsgCaiu));
                var relida = await LerDepoisAsync();
                return Terminar(ResultadoAparelho.Falha(MsgResetFabricaSemResposta + (relida ? "" : MsgLeituraParou)));
            }

            var lida = await LerDepoisAsync();
            // Depois da leitura: a senha guardada não vale mais. Esquecer desconecta.
            await _sessao.EsquecerSenhaAsync();
            _log.Evento("aparelho: reset de fábrica feito; senha esquecida e desconectado");
            return Terminar(new ResultadoAparelho(true, null, MsgResetFabrica + (lida ? "" : MsgLeituraParou)));
        }
        finally
        {
            Desocupar();
        }
    }

    /// <summary>
    /// Troca de senha (<c>*001*ANTIGA*NOVA*</c>): as duas com 4 caracteres [0-9A-Z] e diferentes.
    /// Com <c>#001#OK#</c>, o celular guarda a nova e o app relê; com <c>#001#ERRO#</c>, nada muda e
    /// a frase é "O aparelho recusou a senha atual." Nenhuma senha vai para o log nem para a frase.
    /// </summary>
    public async Task<ResultadoAparelho> TrocarSenhaAsync(string? atual, string? nova)
    {
        var antiga = Quadros.NormalizarSenha(atual);
        var nova4 = Quadros.NormalizarSenha(nova);
        // Na máscara antes de qualquer linha do log (a recusa da trava também é registrada).
        if (antiga is not null)
            _log.ProtegerSegredo(antiga);
        if (nova4 is not null)
            _log.ProtegerSegredo(nova4);
        if (antiga is null || nova4 is null)
            return ResultadoAparelho.Falha(MsgSenhaInvalida);
        if (antiga == nova4)
            return ResultadoAparelho.Falha(MsgSenhaIgual);

        var quadro = Catalogo.TrocarSenha(antiga, nova4);
        if (Recusa(quadro, aparelhoInteiro: true) is { } recusa)
            return recusa;
        if (!Ocupar())
            return ResultadoAparelho.Falha(MsgOcupado);
        try
        {
            _log.Evento("aparelho: troca de senha");
            // Suposição ACK-001: o ack é #001#OK# ou #001#ERRO#.
            var (envio, ack) = await EnviarEEsperarAckAsync(quadro, CasarAck(Catalogo.CodigoTrocaSenha));
            if (!envio.Enviado)
                return Terminar(ResultadoAparelho.Falha(Frase(envio.Motivo ?? Sessao.MotivoSemConexao)));
            if (ack is null)
                return Terminar(ResultadoAparelho.Falha(SessaoAberta ? MsgSenhaSemResposta : MsgCaiu,
                    SessaoAberta ? MsgSenhaSemResposta : $"{MsgCaiu} {MsgSenhaSemResposta}"));
            if (string.Equals(ack, "ERRO", StringComparison.OrdinalIgnoreCase))
                return Terminar(ResultadoAparelho.Falha(MsgSenhaRecusada)); // ERRO: nada muda
            if (!string.Equals(ack, "OK", StringComparison.OrdinalIgnoreCase))
            {
                // Nem OK nem ERRO: o app não sabe se a senha mudou.
                _log.Evento("aparelho: resposta da troca de senha sem OK nem ERRO");
                return Terminar(ResultadoAparelho.Falha(MsgSenhaSemResposta));
            }

            var guardou = await GuardarSenhaAsync(nova4);
            var lida = await LerDepoisAsync();
            var frase = (guardou ? MsgSenhaTrocada : MsgSenhaNaoGuardada) + (lida ? "" : MsgLeituraParou);
            return Terminar(new ResultadoAparelho(true, null, frase));
        }
        finally
        {
            Desocupar();
        }
    }

    /// <summary>
    /// Nome do produto (<c>*002*NOME*</c>): de 1 a 12 caracteres A–Z, 0–9, espaço e <c>_</c>, em
    /// maiúsculas. Espera <c>#002#NOME#</c> e relê. Suposição NOME-PRODUTO: o 002 não muda o
    /// começo <c>PRO2.4MI</c> da resposta 000; se mudar, a próxima conexão entra em modo só
    /// leitura, que é o lado seguro.
    /// </summary>
    public async Task<ResultadoAparelho> NomearProdutoAsync(string? nome)
    {
        if (Catalogo.NormalizarNomeProduto(nome) is not { } normalizado)
            return ResultadoAparelho.Falha(MsgNomeInvalido);
        var quadro = Catalogo.NomearProduto(normalizado);
        if (Recusa(quadro, aparelhoInteiro: true) is { } recusa)
            return recusa;
        if (!Ocupar())
            return ResultadoAparelho.Falha(MsgOcupado);
        try
        {
            _log.Evento($"aparelho: nome do produto {normalizado}");
            // Suposição ACK-002: o ack é #002#NOME#.
            var (envio, ack) = await EnviarEEsperarAckAsync(quadro, CasarAck(Catalogo.CodigoNomeProduto));
            if (!envio.Enviado)
                return Terminar(ResultadoAparelho.Falha(Frase(envio.Motivo ?? Sessao.MotivoSemConexao)));
            if (ack is null && !SessaoAberta)
                return Terminar(ResultadoAparelho.Falha(MsgCaiu));
            var lida = await LerDepoisAsync();
            if (ack is null)
                return Terminar(ResultadoAparelho.Falha(MsgNomeSemResposta + (lida ? "" : MsgLeituraParou)));
            if (ack != normalizado)
                return Terminar(ResultadoAparelho.Falha(MsgNomeDiferente(ack) + (lida ? "" : MsgLeituraParou)));
            return Terminar(new ResultadoAparelho(true, null, MsgNomeProduto(ack) + (lida ? "" : MsgLeituraParou)));
        }
        finally
        {
            Desocupar();
        }
    }

    // ------------------------------------------------------------------ apoio

    /// <summary>Por que o quadro não pode sair agora, ou null. Não envia nada.</summary>
    private ResultadoAparelho? Recusa(string quadro, bool aparelhoInteiro)
    {
        if (Impedimento(quadro, aparelhoInteiro) is { } impedimento)
            return impedimento;
        if (Ocupado)
            return ResultadoAparelho.Falha(MsgOcupado);
        return null;
    }

    /// <summary>
    /// As condições do envio, fora o próprio comando que anda: conexão, trava, leitura e (no
    /// aparelho inteiro) a cópia, as memórias e o gerador. Conferidas antes e de novo com a porta.
    /// </summary>
    private ResultadoAparelho? Impedimento(string quadro, bool aparelhoInteiro)
    {
        if (!SessaoAberta)
            return ResultadoAparelho.Falha(Frase(Sessao.MotivoSemConexao));
        if (_sessao.Conferir(quadro) is { } motivo)
        {
            _log.Recusado(quadro, motivo);
            return ResultadoAparelho.Falha(Frase(motivo));
        }
        if (_estado.Lendo)
            return ResultadoAparelho.Falha(Escritor.MsgLendo);
        if (!_estado.Pronto)
            return ResultadoAparelho.Falha(Escritor.MsgSemLeitura);
        if (aparelhoInteiro && BloqueioDeFora() is { } bloqueio)
            return ResultadoAparelho.Falha(bloqueio);
        return null;
    }

    /// <summary>
    /// Envia com a exclusão da sessão e espera a resposta no prazo. Resposta null = não veio ou
    /// caiu. Com a porta, confere de novo as condições: se algo mudou na fila, nada sai.
    /// </summary>
    private async Task<(ResultadoEnvio Envio, string? Resposta)> EnviarEEsperarAckAsync(string quadro, Func<string, string?> casar)
    {
        var porta = _sessao.ExclusaoLeituraEscrita;
        await porta.WaitAsync();
        try
        {
            if (Impedimento(quadro, aparelhoInteiro: true) is { } impedimento)
                return (new ResultadoEnvio(false, impedimento.Motivo), null);
            return await _envio.EnviarEEsperarAsync(quadro, casar, _prazoResposta);
        }
        finally
        {
            porta.Release();
        }
    }

    /// <summary>A resposta <c>#CCC#…#</c> do comando (os segmentos depois do código, juntos), ou null.</summary>
    private static Func<string, string?> CasarAck(string codigo) => texto =>
        !texto.TrimStart().StartsWith('*') && Quadros.Analisar(texto) is { } q && q.Codigo == codigo
            ? string.Join("#", q.Segmentos)
            : null;

    /// <summary>A leitura completa nova (CAP-10). True = completa.</summary>
    private async Task<bool> LerDepoisAsync()
    {
        if (!SessaoAberta)
            return false;
        var retrato = await _leitura.LerNovaAsync();
        return retrato is { Completo: true };
    }

    private async Task<bool> GuardarSenhaAsync(string senha)
    {
        try
        {
            await _cofre.GuardarAsync(senha);
            return true;
        }
        catch (Exception e)
        {
            _log.Evento($"não consegui guardar a senha nova ({e.GetType().Name})");
            return false;
        }
    }

    private ResultadoAparelho Terminar(ResultadoAparelho r)
    {
        if (!r.Ok)
            _log.Evento($"aparelho: {r.Mensagem ?? r.Motivo}");
        return r;
    }

    private bool Ocupar()
    {
        if (Interlocked.CompareExchange(ref _ocupado, 1, 0) != 0)
            return false;
        Notificar();
        return true;
    }

    private void Desocupar()
    {
        Volatile.Write(ref _ocupado, 0);
        Notificar();
    }

    private static string Frase(string motivo) =>
        string.IsNullOrEmpty(motivo) ? motivo : char.ToUpperInvariant(motivo[0]) + motivo[1..] + (motivo.EndsWith('.') ? "" : ".");

    private void Notificar()
    {
        try
        {
            Mudou?.Invoke();
        }
        catch (Exception e)
        {
            _log.Evento($"falha no aviso dos comandos do aparelho ({e.GetType().Name})");
        }
    }
}
