using BgsControle.Nucleo;
using BgsControle.Nucleo.Aparelho;
using BgsControle.Nucleo.Escrita;
using BgsControle.Nucleo.Gerador;
using BgsControle.Nucleo.Memorias;
using BgsControle.Nucleo.Protocolo;
using BgsControle.Nucleo.Seguranca;

namespace BgsControle.Telas;

/// <summary>A folha de confirmação aberta na tela Aparelho.</summary>
public enum FolhaAparelho
{
    Nenhuma,
    ResetAudio,
    ResetFabrica,
    TrocaSenha,
}

/// <summary>
/// O grupo Aparelho de Configurações (sem XAML: é da história 11):
/// <list type="bullet">
/// <item>Remote out e Bluetooth de áudio, Lig./Desl. na própria linha, sem folha, com o valor
/// relido do aparelho;</item>
/// <item>o nome do produto, que vale no ✓ e relê depois;</item>
/// <item>a troca de senha: a atual, a nova digitada duas vezes e a folha "Uma senha perdida tranca
/// o aparelho."; nenhuma senha aparece numa frase;</item>
/// <item>o reset de áudio e o reset de fábrica (em Avançado), cada um com a folha do seu risco.</item>
/// </list>
/// Cada ação fica travada com o motivo ("Chave desligada", "Sem conexão"…). Nada sai antes do botão
/// de confirmar da folha; Cancelar não envia nada.
/// </summary>
public sealed class AparelhoViewModel : TelaBase, IDisposable
{
    public const string SemValor = "—";
    public const string TextoLigado = "Lig.";
    public const string TextoDesligado = "Desl.";
    public const string TextoCancelar = "Cancelar";
    public const string TextoEnviando = "Enviando…";

    private readonly ComandosAparelho _comandos;
    private readonly Sessao _sessao;
    private readonly IChaves _chaves;
    private readonly EstadoAparelho _estado;
    private readonly CopiaAntes _copia;
    private readonly GerenteMemorias? _memorias;
    private readonly GeradorSinal? _gerador;

    private string? _mensagem;
    private FolhaAparelho _folha;
    private bool _agindo;
    private string _nomeProduto = "";
    private string? _erroNome;
    private string _senhaAtual = "";
    private string _senhaNova = "";
    private string _senhaConfirmacao = "";
    private string? _erroSenha;

    public AparelhoViewModel(
        ComandosAparelho comandos,
        Sessao sessao,
        IChaves chaves,
        EstadoAparelho estado,
        CopiaAntes copia,
        IDespachante despachante,
        TimeProvider relogio,
        GerenteMemorias? memorias = null,
        GeradorSinal? gerador = null)
        : base(despachante, relogio)
    {
        _comandos = comandos;
        _sessao = sessao;
        _chaves = chaves;
        _estado = estado;
        _copia = copia;
        _memorias = memorias;
        _gerador = gerador;

        LigarRemoteOut = new Comando(() => DefinirRemoteOutAsync(true));
        DesligarRemoteOut = new Comando(() => DefinirRemoteOutAsync(false));
        LigarBluetoothAudio = new Comando(() => DefinirBluetoothAudioAsync(true));
        DesligarBluetoothAudio = new Comando(() => DefinirBluetoothAudioAsync(false));
        ConfirmarNome = new Comando(ConfirmarNomeAsync);
        TrocarSenha = new Comando(AbrirTrocaSenha);
        ResetAudio = new Comando(() => AbrirReset(FolhaAparelho.ResetAudio));
        ResetFabrica = new Comando(() => AbrirReset(FolhaAparelho.ResetFabrica));
        ConfirmarFolha = new Comando(ConfirmarFolhaAsync);
        CancelarFolha = new Comando(FecharFolha);

        _comandos.Mudou += AoMudarAlgo;
        _sessao.Mudou += AoMudarAlgo;
        _chaves.Mudou += AoMudarChave;
        _copia.Mudou += AoMudarAlgo;
        if (_memorias is not null)
            _memorias.Mudou += AoMudarAlgo;
        if (_gerador is not null)
            _gerador.Mudou += AoMudarAlgo;
        _estado.Recarregou += AoMudarAlgo;
        _estado.Mudou += AoMudarValor;
        Atualizar();
    }

    /// <summary>A última frase: "Remote out desligado.", "Nome do produto: SALA 1", "Chave desligada"…</summary>
    public string? Mensagem
    {
        get => _mensagem;
        private set
        {
            if (_mensagem == value)
                return;
            _mensagem = value;
            Avisar();
        }
    }

    /// <summary>Um comando desta tela está andando.</summary>
    public bool Agindo => _agindo;

    // ------------------------------------------------------------------ remote out e Bluetooth de áudio

    /// <summary>O remote out relido do aparelho (252), ou null sem leitura.</summary>
    public bool? RemoteOutLigado => _estado.Valor(Catalogo.CodigoRemoteOut)?.Ligado;

    /// <summary>"Lig.", "Desl." ou "—".</summary>
    public string TextoRemoteOut => Texto(RemoteOutLigado);

    public string? MotivoTravaRemoteOut => MotivoTrava(Chave.RemoteOut, aparelhoInteiro: false);

    public bool PodeRemoteOut => MotivoTravaRemoteOut is null;

    public Comando LigarRemoteOut { get; }

    public Comando DesligarRemoteOut { get; }

    /// <summary>O Bluetooth de áudio relido do aparelho (251), ou null sem leitura.</summary>
    public bool? BluetoothAudioLigado => _estado.Valor(Catalogo.CodigoBluetoothAudio)?.Ligado;

    public string TextoBluetoothAudio => Texto(BluetoothAudioLigado);

    public string? MotivoTravaBluetoothAudio => MotivoTrava(Chave.BluetoothAudio, aparelhoInteiro: false);

    public bool PodeBluetoothAudio => MotivoTravaBluetoothAudio is null;

    public Comando LigarBluetoothAudio { get; }

    public Comando DesligarBluetoothAudio { get; }

    /// <summary>Remote out na própria linha: aplica na hora, sem folha. Travado, nada sai e o motivo aparece.</summary>
    public Task DefinirRemoteOutAsync(bool ligar)
    {
        if (MotivoTravaRemoteOut is { } motivo)
        {
            Mensagem = motivo;
            return Task.CompletedTask;
        }
        return Agir(async () => (await _comandos.DefinirRemoteOutAsync(ligar)).Mensagem);
    }

    /// <summary>Bluetooth de áudio na própria linha: aplica na hora, sem folha.</summary>
    public Task DefinirBluetoothAudioAsync(bool ligar)
    {
        if (MotivoTravaBluetoothAudio is { } motivo)
        {
            Mensagem = motivo;
            return Task.CompletedTask;
        }
        return Agir(async () => (await _comandos.DefinirBluetoothAudioAsync(ligar)).Mensagem);
    }

    // ------------------------------------------------------------------ nome do produto

    /// <summary>O campo do nome (até 12 caracteres; vai em maiúsculas).</summary>
    public string NomeProduto
    {
        get => _nomeProduto;
        set
        {
            value ??= "";
            if (_nomeProduto == value)
                return;
            _nomeProduto = value;
            ErroNome = null;
            Avisar();
        }
    }

    /// <summary>"Use até 12 letras, números, espaço ou _", ou null.</summary>
    public string? ErroNome
    {
        get => _erroNome;
        private set
        {
            if (_erroNome == value)
                return;
            _erroNome = value;
            Avisar();
        }
    }

    public string? MotivoTravaNome => MotivoTrava(Chave.NomeProduto, aparelhoInteiro: true);

    public bool PodeNome => MotivoTravaNome is null;

    /// <summary>O ✓ do nome: sem folha. "Nome do produto: NOME" com a resposta do aparelho.</summary>
    public Comando ConfirmarNome { get; }

    private async Task ConfirmarNomeAsync()
    {
        if (Catalogo.NormalizarNomeProduto(NomeProduto) is not { } nome)
        {
            ErroNome = ComandosAparelho.MsgNomeInvalido;
            Mensagem = ComandosAparelho.MsgNomeInvalido;
            return;
        }
        if (MotivoTravaNome is { } motivo)
        {
            Mensagem = motivo;
            return;
        }
        ErroNome = null;
        ResultadoAparelho? r = null;
        await Agir(async () =>
        {
            r = await _comandos.NomearProdutoAsync(nome);
            return r.Mensagem;
        }, depois: () =>
        {
            if (r is { Ok: true })
            {
                _nomeProduto = nome;
                Avisar(nameof(NomeProduto));
            }
        });
    }

    // ------------------------------------------------------------------ troca de senha

    /// <summary>A senha atual (4 caracteres, mascarados na tela).</summary>
    public string SenhaAtual
    {
        get => _senhaAtual;
        set => DefinirSenha(ref _senhaAtual, value);
    }

    /// <summary>A senha nova.</summary>
    public string SenhaNova
    {
        get => _senhaNova;
        set => DefinirSenha(ref _senhaNova, value);
    }

    /// <summary>A senha nova outra vez.</summary>
    public string SenhaNovaConfirmacao
    {
        get => _senhaConfirmacao;
        set => DefinirSenha(ref _senhaConfirmacao, value);
    }

    /// <summary>"As duas senhas novas não são iguais.", "O aparelho recusou a senha atual."…, ou null. Nunca com a senha.</summary>
    public string? ErroSenha
    {
        get => _erroSenha;
        private set
        {
            if (_erroSenha == value)
                return;
            _erroSenha = value;
            Avisar();
        }
    }

    public string? MotivoTravaSenha => MotivoTrava(Chave.TrocaSenha, aparelhoInteiro: true);

    public bool PodeTrocarSenha => MotivoTravaSenha is null;

    /// <summary>Confere os campos e abre a folha "Uma senha perdida tranca o aparelho.". Nada sai aqui.</summary>
    public Comando TrocarSenha { get; }

    private void AbrirTrocaSenha()
    {
        if (MotivoTravaSenha is { } motivo)
        {
            Mensagem = motivo;
            return;
        }
        if (ConferirSenhas() is { } erro)
        {
            ErroSenha = erro;
            Mensagem = erro;
            return;
        }
        ErroSenha = null;
        Abrir(FolhaAparelho.TrocaSenha);
    }

    /// <summary>Por que as senhas digitadas não servem, ou null.</summary>
    private string? ConferirSenhas()
    {
        var atual = Quadros.NormalizarSenha(SenhaAtual);
        var nova = Quadros.NormalizarSenha(SenhaNova);
        if (atual is null || nova is null)
            return ComandosAparelho.MsgSenhaInvalida;
        if (Quadros.NormalizarSenha(SenhaNovaConfirmacao) != nova)
            return ComandosAparelho.MsgSenhasDiferentes;
        if (atual == nova)
            return ComandosAparelho.MsgSenhaIgual;
        return null;
    }

    private void DefinirSenha(ref string campo, string? valor, [System.Runtime.CompilerServices.CallerMemberName] string? nome = null)
    {
        valor ??= "";
        if (campo == valor)
            return;
        campo = valor;
        ErroSenha = null;
        Avisar(nome);
    }

    /// <summary>
    /// Sair da tela da senha: as senhas digitadas (e o erro) saem da memória do app. A folha da troca, se
    /// aberta e sem comando andando, fecha junto.
    /// </summary>
    public void EsquecerSenhasDigitadas()
    {
        LimparSenhas();
        ErroSenha = null;
        if (_folha == FolhaAparelho.TrocaSenha && !_agindo)
            Fechar();
    }

    private void LimparSenhas()
    {
        _senhaAtual = "";
        _senhaNova = "";
        _senhaConfirmacao = "";
        Avisar(nameof(SenhaAtual));
        Avisar(nameof(SenhaNova));
        Avisar(nameof(SenhaNovaConfirmacao));
    }

    // ------------------------------------------------------------------ resets

    public string? MotivoTravaResetAudio => MotivoTrava(Chave.ResetAudio, aparelhoInteiro: true);

    public bool PodeResetAudio => MotivoTravaResetAudio is null;

    public string? MotivoTravaResetFabrica => MotivoTrava(Chave.ResetFabrica, aparelhoInteiro: true);

    public bool PodeResetFabrica => MotivoTravaResetFabrica is null;

    /// <summary>Abre a folha "O estado resultante é imprevisível.". Nada sai aqui.</summary>
    public Comando ResetAudio { get; }

    /// <summary>Abre a folha "Apaga a senha e as 3 memórias.". Nada sai aqui.</summary>
    public Comando ResetFabrica { get; }

    private void AbrirReset(FolhaAparelho folha)
    {
        if (MotivoDa(folha) is { } motivo)
        {
            Mensagem = motivo;
            return;
        }
        Abrir(folha);
    }

    // ------------------------------------------------------------------ folha

    public FolhaAparelho Folha => _folha;

    public bool FolhaAberta => _folha != FolhaAparelho.Nenhuma;

    /// <summary>"Fazer o reset de áudio?", "Fazer o reset de fábrica?" ou "Trocar a senha do aparelho?".</summary>
    public string? FolhaTitulo => _folha switch
    {
        FolhaAparelho.ResetAudio => "Fazer o reset de áudio?",
        FolhaAparelho.ResetFabrica => "Fazer o reset de fábrica?",
        FolhaAparelho.TrocaSenha => "Trocar a senha do aparelho?",
        _ => null,
    };

    /// <summary>O risco da chave, sem mudar o texto (<c>chaves-de-acesso.md</c>).</summary>
    public string? FolhaAviso => ChaveDa(_folha) is { } chave ? ChavesPadrao.De(chave).Risco : null;

    /// <summary>O botão branco: diz o que faz; "Enviando…" até o fim.</summary>
    public string? TextoConfirmarFolha => _folha switch
    {
        FolhaAparelho.Nenhuma => null,
        _ when _agindo => TextoEnviando,
        FolhaAparelho.ResetAudio => "Fazer o reset de áudio",
        FolhaAparelho.ResetFabrica => "Fazer o reset de fábrica",
        _ => "Trocar a senha",
    };

    /// <summary>O botão branco: só aqui o comando sai.</summary>
    public Comando ConfirmarFolha { get; }

    /// <summary>O botão contornado, o véu ou Voltar: nada sai.</summary>
    public Comando CancelarFolha { get; }

    private async Task ConfirmarFolhaAsync()
    {
        var folha = _folha;
        if (folha == FolhaAparelho.Nenhuma || _agindo)
            return;
        if (MotivoDa(folha) is { } motivo)
        {
            Mensagem = motivo;
            return;
        }

        Func<Task<ResultadoAparelho>> acao;
        if (folha == FolhaAparelho.TrocaSenha)
        {
            if (ConferirSenhas() is { } erro)
            {
                ErroSenha = erro;
                Mensagem = erro;
                return;
            }
            var atual = SenhaAtual;
            var nova = SenhaNova;
            acao = () => _comandos.TrocarSenhaAsync(atual, nova);
        }
        else if (folha == FolhaAparelho.ResetAudio)
        {
            acao = _comandos.ResetAudioAsync;
        }
        else
        {
            acao = _comandos.ResetFabricaAsync;
        }

        ResultadoAparelho? r = null;
        await Agir(async () =>
        {
            r = await acao();
            return r.Mensagem;
        }, depois: () =>
        {
            if (folha == FolhaAparelho.TrocaSenha)
            {
                // Só com uma resposta clara (OK ou ERRO) os campos limpam: sem ela, a senha nova
                // pode já valer, e o Denis ainda precisa dela.
                if (r is { Ok: true } || r?.Mensagem == ComandosAparelho.MsgSenhaRecusada)
                    LimparSenhas();
                ErroSenha = r is { Ok: true } ? null : r?.Mensagem;
            }
            Fechar();
        });
    }

    private void Abrir(FolhaAparelho folha)
    {
        _folha = folha;
        Mensagem = null;
        AvisarFolha();
    }

    private void FecharFolha()
    {
        if (_agindo)
            return; // o comando já saiu: a folha fecha quando ele termina
        Fechar();
    }

    private void Fechar()
    {
        _folha = FolhaAparelho.Nenhuma;
        AvisarFolha();
    }

    private void AvisarFolha()
    {
        Avisar(nameof(Folha));
        Avisar(nameof(FolhaAberta));
        Avisar(nameof(FolhaTitulo));
        Avisar(nameof(FolhaAviso));
        Avisar(nameof(TextoConfirmarFolha));
    }

    private static Chave? ChaveDa(FolhaAparelho folha) => folha switch
    {
        FolhaAparelho.ResetAudio => Chave.ResetAudio,
        FolhaAparelho.ResetFabrica => Chave.ResetFabrica,
        FolhaAparelho.TrocaSenha => Chave.TrocaSenha,
        _ => null,
    };

    private string? MotivoDa(FolhaAparelho folha) => ChaveDa(folha) is { } chave ? MotivoTrava(chave, aparelhoInteiro: true) : null;

    // ------------------------------------------------------------------ travas

    /// <summary>Por que a ação da chave está travada agora, ou null.</summary>
    private string? MotivoTrava(Chave chave, bool aparelhoInteiro)
    {
        if (_sessao.Estado != EstadoSessao.Conectado || !_sessao.SegurandoConexao)
            return ControleParametroViewModel.MsgSemConexao;
        if (_sessao.SoLeitura)
            return ControleParametroViewModel.MsgSoLeitura;
        if (!_chaves.Ligada(chave))
            return ControleParametroViewModel.MsgChaveDesligada;
        if (_estado.Lendo)
            return ControleParametroViewModel.MsgLendo;
        if (!_estado.Pronto)
            return ControleParametroViewModel.MsgNaoLeu;
        // A cópia, as memórias e o gerador: o mesmo motivo (e o mesmo texto) do núcleo.
        if (aparelhoInteiro && _comandos.MotivoBloqueio() is { } bloqueio)
            return bloqueio;
        if (_agindo || _comandos.Ocupado)
            return ComandosAparelho.MsgOcupado;
        return null;
    }

    // ------------------------------------------------------------------ apoio

    /// <summary>Roda o comando do núcleo e mostra a frase dele; a tela se atualiza na sua thread.</summary>
    private async Task Agir(Func<Task<string?>> acao, Action? depois = null)
    {
        _agindo = true;
        Atualizar();
        string? frase;
        try
        {
            frase = await acao();
        }
        catch (Exception e)
        {
            frase = $"Erro inesperado ({e.GetType().Name}).";
        }
        var feito = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        NaTela(() =>
        {
            try
            {
                _agindo = false;
                depois?.Invoke();
                Mensagem = frase;
                Atualizar();
            }
            finally
            {
                feito.SetResult();
            }
        });
        await feito.Task;
    }

    private static string Texto(bool? ligado) => ligado switch
    {
        true => TextoLigado,
        false => TextoDesligado,
        null => SemValor,
    };

    private void AoMudarAlgo() => NaTela(Atualizar);

    private void AoMudarChave(Chave chave)
    {
        if (chave is Chave.RemoteOut or Chave.BluetoothAudio or Chave.NomeProduto or Chave.TrocaSenha or Chave.ResetAudio or Chave.ResetFabrica)
            NaTela(Atualizar);
    }

    private void AoMudarValor(Endereco e)
    {
        if (e.Indice is null && e.Codigo is Catalogo.CodigoRemoteOut or Catalogo.CodigoBluetoothAudio)
            NaTela(Atualizar);
    }

    private void Atualizar()
    {
        Avisar(nameof(Agindo));
        Avisar(nameof(RemoteOutLigado));
        Avisar(nameof(TextoRemoteOut));
        Avisar(nameof(MotivoTravaRemoteOut));
        Avisar(nameof(PodeRemoteOut));
        Avisar(nameof(BluetoothAudioLigado));
        Avisar(nameof(TextoBluetoothAudio));
        Avisar(nameof(MotivoTravaBluetoothAudio));
        Avisar(nameof(PodeBluetoothAudio));
        Avisar(nameof(MotivoTravaNome));
        Avisar(nameof(PodeNome));
        Avisar(nameof(MotivoTravaSenha));
        Avisar(nameof(PodeTrocarSenha));
        Avisar(nameof(MotivoTravaResetAudio));
        Avisar(nameof(PodeResetAudio));
        Avisar(nameof(MotivoTravaResetFabrica));
        Avisar(nameof(PodeResetFabrica));
        AvisarFolha();
    }

    public void Dispose()
    {
        _comandos.Mudou -= AoMudarAlgo;
        _sessao.Mudou -= AoMudarAlgo;
        _chaves.Mudou -= AoMudarChave;
        _copia.Mudou -= AoMudarAlgo;
        if (_memorias is not null)
            _memorias.Mudou -= AoMudarAlgo;
        if (_gerador is not null)
            _gerador.Mudou -= AoMudarAlgo;
        _estado.Recarregou -= AoMudarAlgo;
        _estado.Mudou -= AoMudarValor;
    }
}
