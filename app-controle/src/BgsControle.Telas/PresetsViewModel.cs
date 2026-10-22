using BgsControle.Nucleo;
using BgsControle.Nucleo.Escrita;
using BgsControle.Nucleo.Presets;
using BgsControle.Nucleo.Seguranca;

namespace BgsControle.Telas;

/// <summary>Uma linha da lista de presets: um herdado (com selo e nota) ou um próprio.</summary>
public sealed class ItemPresetViewModel : TelaBase
{
    private string _nome;

    internal ItemPresetViewModel(Preset preset, PresetsViewModel tela, IDespachante despachante, TimeProvider relogio)
        : base(despachante, relogio)
    {
        Id = preset.Id;
        _nome = preset.Nome;
        Herdado = preset.Herdado;
        Nota = preset.Nota;
        Aplicar = new Comando(() => tela.AplicarAsync(Id));
        Renomear = new Comando(() => tela.AbrirRenomear(Id));
        Apagar = new Comando(() => tela.AbrirApagar(Id));
    }

    public string Id { get; }

    public string Nome
    {
        get => _nome;
        internal set
        {
            if (_nome == value)
                return;
            _nome = value;
            Avisar();
        }
    }

    /// <summary>Veio dos protótipos (CAP-6): não muda.</summary>
    public bool Herdado { get; }

    /// <summary>O selo HERDADO, ou null para um preset próprio.</summary>
    public string? Selo => Herdado ? HerdadoViewModel.TextoSelo : null;

    /// <summary>A nota herdada, ou null.</summary>
    public string? Nota { get; }

    /// <summary>Aplica o preset, sem confirmação (cópia antes, escreve, relê).</summary>
    public Comando Aplicar { get; }

    /// <summary>Abre o campo do nome (só os próprios; um herdado recusa).</summary>
    public Comando Renomear { get; }

    /// <summary>Abre a folha "Apagar o preset X?" (só os próprios; um herdado recusa).</summary>
    public Comando Apagar { get; }
}

/// <summary>
/// A tela Presets no celular (Configurações › Presets, CAP-5): a lista (os herdados primeiro, com
/// selo e nota, depois os próprios), "Guardar o som atual" com o nome, Aplicar por item, Renomear
/// e Apagar com a folha "Apagar o preset X?" (Confirmar ou Cancelar). Cada ação fica travada, com o
/// motivo, sem conexão, em modo só leitura ou com a chave desligada. Depois de uma queda no meio de
/// um aplicar, mostra "aplicado pela metade" com Terminar e Voltar à cópia, como a tela Memórias.
/// Nada sai ao aparelho antes do botão de confirmar da folha; guardar, renomear e apagar nunca
/// mandam nada ao aparelho.
/// </summary>
public sealed class PresetsViewModel : TelaBase, IDisposable
{
    public const string TextoSemProprios = "Guarde o som atual como preset.";
    public const string TextoGuardar = "Guardar o som atual";
    public const string TextoCancelar = "Cancelar";
    public const string TextoRenomear = "Renomear";
    public const string TituloPelaMetade = "Aplicado pela metade";
    public const string TextoTerminar = "Terminar";
    public const string TextoVoltar = "Voltar à cópia";
    public const string MsgEmAndamento = "Uma operação está em andamento";
    public const string MsgVoltou = "O som voltou à cópia.";

    private readonly GerentePresets _gerente;
    private readonly CopiaAntes _copia;
    private readonly Sessao _sessao;
    private readonly IChaves _chaves;
    private readonly EstadoAparelho _estado;

    private readonly Dictionary<string, ItemPresetViewModel> _porId = [];
    private IReadOnlyList<ItemPresetViewModel> _itens = [];
    private string? _mensagem;
    private string? _ultimoAvisoGerente;
    private string _nomeNovo = "";
    private string? _erroNome;
    private string? _renomear;
    private string _nomeRenomear = "";
    private string? _erroRenomear;
    private string? _apagar;
    private bool _agindo;

    public PresetsViewModel(
        GerentePresets gerente,
        CopiaAntes copia,
        Sessao sessao,
        IChaves chaves,
        EstadoAparelho estado,
        IDespachante despachante,
        TimeProvider relogio)
        : base(despachante, relogio)
    {
        _gerente = gerente;
        _copia = copia;
        _sessao = sessao;
        _chaves = chaves;
        _estado = estado;

        Guardar = new Comando(GuardarAsync);
        ConfirmarRenomear = new Comando(ConfirmarRenomearAsync);
        CancelarRenomear = new Comando(FecharRenomear);
        ConfirmarApagar = new Comando(ConfirmarApagarAsync);
        CancelarApagar = new Comando(FecharApagar);
        Terminar = new Comando(TerminarAsync);
        VoltarACopia = new Comando(VoltarAsync);

        _ultimoAvisoGerente = _gerente.Aviso;
        _gerente.Mudou += AoMudarAlgo;
        _copia.Mudou += AoMudarAlgo;
        _sessao.Mudou += AoMudarAlgo;
        _chaves.Mudou += AoMudarChave;
        _estado.Recarregou += AoMudarAlgo;
        Atualizar();
    }

    /// <summary>A lista: os herdados (com selo e nota) primeiro, depois os próprios.</summary>
    public IReadOnlyList<ItemPresetViewModel> Itens => _itens;

    /// <summary>O item pelo nome (sem distinguir maiúsculas), ou null.</summary>
    public ItemPresetViewModel? Item(string nome) =>
        _itens.FirstOrDefault(i => string.Equals(i.Nome, nome, StringComparison.OrdinalIgnoreCase));

    /// <summary>"Guarde o som atual como preset." quando não há preset próprio; senão null.</summary>
    public string? TextoVazio => _gerente.Proprios.Count == 0 ? TextoSemProprios : null;

    /// <summary>A última frase: "Preset A aplicado…", "Chave desligada", "Já existe um preset com esse nome."</summary>
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

    /// <summary>Uma operação de preset (ou o Voltar à cópia) está andando: o botão mostra "ocupado".</summary>
    public bool Ocupado => _agindo || _gerente.Ocupado || _copia.EmAndamento;

    // ------------------------------------------------------------------ travas

    /// <summary>Por que "Aplicar" está travado, ou null.</summary>
    public string? MotivoTravaAplicar
    {
        get
        {
            if (_sessao.Estado != EstadoSessao.Conectado || !_sessao.SegurandoConexao)
                return ControleParametroViewModel.MsgSemConexao;
            if (_sessao.SoLeitura)
                return ControleParametroViewModel.MsgSoLeitura;
            if (!_chaves.Ligada(Chave.AjustesAudio))
                return ControleParametroViewModel.MsgChaveDesligada;
            if (_estado.Lendo)
                return ControleParametroViewModel.MsgLendo;
            if (!_estado.Pronto)
                return ControleParametroViewModel.MsgNaoLeu;
            if (Ocupado)
                return MsgEmAndamento;
            if (_copia.PelaMetade is not null)
                return CopiaAntes.MsgPelaMetadePendente;
            return null;
        }
    }

    /// <summary>
    /// Por que "Guardar o som atual" está travado, ou null. Guardar não manda nada ao aparelho e
    /// não precisa de chave; precisa só da leitura completa.
    /// </summary>
    public string? MotivoTravaGuardar
    {
        get
        {
            if (_sessao.Estado != EstadoSessao.Conectado || !_sessao.SegurandoConexao)
                return ControleParametroViewModel.MsgSemConexao;
            if (_estado.Lendo)
                return ControleParametroViewModel.MsgLendo;
            if (!_estado.Pronto)
                return ControleParametroViewModel.MsgNaoLeu;
            if (Ocupado)
                return MsgEmAndamento;
            return null;
        }
    }

    public bool PodeAplicar => MotivoTravaAplicar is null;
    public bool PodeGuardar => MotivoTravaGuardar is null;

    // ------------------------------------------------------------------ guardar o som atual

    /// <summary>O nome do preset novo (como foi digitado; vale sem os espaços das pontas).</summary>
    public string NomeNovo
    {
        get => _nomeNovo;
        set
        {
            if (_nomeNovo == value)
                return;
            _nomeNovo = value;
            Avisar();
        }
    }

    /// <summary>"Use de 1 a 24 letras, números, espaço, _ ou -", "Já existe um preset com esse nome.", ou null.</summary>
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

    /// <summary>Guarda o som atual com o nome. Se não valer, o campo fica com o nome digitado e o erro.</summary>
    public Comando Guardar { get; }

    private async Task GuardarAsync()
    {
        if (MotivoTravaGuardar is { } motivo)
        {
            ErroNome = null;
            Mensagem = motivo;
            return;
        }
        ErroNome = null;
        ResultadoPreset? resultado = null;
        await Agir(async () =>
        {
            resultado = await _gerente.GuardarAsync(NomeNovo);
            return Frase(resultado);
        }, depois: () =>
        {
            if (resultado is { Ok: true })
            {
                _nomeNovo = "";
                Avisar(nameof(NomeNovo));
            }
            else
            {
                _erroNome = Frase(resultado);
                Avisar(nameof(ErroNome));
            }
        });
    }

    // ------------------------------------------------------------------ aplicar

    /// <summary>Aplicar: age direto, sem confirmação (a cópia antes é guardada). Travado, nada sai e o motivo aparece.</summary>
    public async Task AplicarAsync(string id)
    {
        if (MotivoTravaAplicar is { } motivo)
        {
            Mensagem = motivo;
            return;
        }
        await Agir(async () => Frase(await _gerente.AplicarAsync(id)));
    }

    // ------------------------------------------------------------------ renomear

    public bool RenomearAberto => _renomear is not null;

    /// <summary>"Renomear o preset NOITE".</summary>
    public string? RenomearTitulo => _renomear is { } id && _gerente.Obter(id) is { } p ? $"{TextoRenomear} o preset {p.Nome}" : null;

    public string NomeRenomear
    {
        get => _nomeRenomear;
        set
        {
            if (_nomeRenomear == value)
                return;
            _nomeRenomear = value;
            Avisar();
        }
    }

    public string? ErroRenomear
    {
        get => _erroRenomear;
        private set
        {
            if (_erroRenomear == value)
                return;
            _erroRenomear = value;
            Avisar();
        }
    }

    /// <summary>Confirma o novo nome. Não manda nada ao aparelho.</summary>
    public Comando ConfirmarRenomear { get; }

    public Comando CancelarRenomear { get; }

    /// <summary>"Renomear": abre o campo do nome. Um herdado recusa; ocupado, só o motivo.</summary>
    public void AbrirRenomear(string id)
    {
        if (RecusaMudar(id))
            return;
        FecharApagar();
        _renomear = id;
        NomeRenomear = _gerente.Obter(id)?.Nome ?? "";
        ErroRenomear = null;
        Mensagem = null;
        AvisarRenomear();
    }

    private async Task ConfirmarRenomearAsync()
    {
        if (_renomear is not { } id)
            return;
        if (Ocupado)
        {
            ErroRenomear = MsgEmAndamento;
            return;
        }
        ErroRenomear = null;
        ResultadoPreset? resultado = null;
        await Agir(async () =>
        {
            resultado = await _gerente.RenomearAsync(id, NomeRenomear);
            return Frase(resultado);
        }, depois: () =>
        {
            if (resultado is { Ok: true })
                FecharRenomear();
            else
                ErroRenomear = Frase(resultado); // o campo fica, com o nome digitado
        });
    }

    private void FecharRenomear()
    {
        _renomear = null;
        _nomeRenomear = "";
        _erroRenomear = null;
        Avisar(nameof(NomeRenomear));
        Avisar(nameof(ErroRenomear));
        AvisarRenomear();
    }

    private void AvisarRenomear()
    {
        Avisar(nameof(RenomearAberto));
        Avisar(nameof(RenomearTitulo));
    }

    // ------------------------------------------------------------------ apagar

    public bool ApagarAberto => _apagar is not null;

    /// <summary>"Apagar o preset NOITE?"</summary>
    public string? ApagarTitulo => _apagar is { } id && _gerente.Obter(id) is { } p ? $"Apagar o preset {p.Nome}?" : null;

    /// <summary>O botão branco: "Apagar o preset NOITE".</summary>
    public string? TextoConfirmarApagar => _apagar is { } id && _gerente.Obter(id) is { } p ? $"Apagar o preset {p.Nome}" : null;

    /// <summary>O botão branco: apaga o preset. Só aqui, e só aqui, ele sai da lista.</summary>
    public Comando ConfirmarApagar { get; }

    /// <summary>O botão contornado, o véu ou Voltar: nada muda.</summary>
    public Comando CancelarApagar { get; }

    /// <summary>"Apagar": abre a folha de confirmação. Um herdado recusa; ocupado, só o motivo. Nada sai antes do botão.</summary>
    public void AbrirApagar(string id)
    {
        if (RecusaMudar(id))
            return;
        FecharRenomear();
        _apagar = id;
        Mensagem = null;
        AvisarApagar();
    }

    private async Task ConfirmarApagarAsync()
    {
        if (_apagar is not { } id)
            return;
        // Um segundo toque no Confirmar não fecha a folha nem mexe no andamento do primeiro.
        if (Ocupado)
        {
            Mensagem = MsgEmAndamento;
            return;
        }
        await Agir(async () => Frase(await _gerente.ApagarAsync(id)), depois: FecharApagar);
    }

    private void FecharApagar()
    {
        if (_apagar is null)
            return;
        _apagar = null;
        AvisarApagar();
    }

    private void AvisarApagar()
    {
        Avisar(nameof(ApagarAberto));
        Avisar(nameof(ApagarTitulo));
        Avisar(nameof(TextoConfirmarApagar));
    }

    /// <summary>Renomear e apagar não valem para um herdado e não entram no meio de outra operação.</summary>
    private bool RecusaMudar(string id)
    {
        if (_gerente.Obter(id) is { Herdado: true })
        {
            Mensagem = GerentePresets.MsgHerdado;
            return true;
        }
        if (_gerente.Obter(id) is null)
        {
            Mensagem = GerentePresets.MsgNaoExiste;
            return true;
        }
        if (Ocupado)
        {
            Mensagem = MsgEmAndamento;
            return true;
        }
        return false;
    }

    // ------------------------------------------------------------------ aplicado pela metade

    /// <summary>A folha "Aplicado pela metade" está aberta.</summary>
    public bool PelaMetade => _copia.PelaMetade is not null;

    /// <summary>"Aplicar o preset NOITE caiu no meio."</summary>
    public string? TextoPelaMetade => _copia.PelaMetade is { } op ? $"{op.Descricao} caiu no meio." : null;

    /// <summary>O botão branco: refaz a operação (escreve só o que falta, com a mesma cópia).</summary>
    public Comando Terminar { get; }

    /// <summary>O botão contornado: escreve de volta os ajustes que mudaram (chave "Ajustes de áudio").</summary>
    public Comando VoltarACopia { get; }

    private async Task TerminarAsync()
    {
        if (!PelaMetade)
            return;
        await Agir(async () =>
        {
            var r = await _copia.TerminarAsync();
            return r.Ok ? _gerente.Aviso : Frase(r.Motivo);
        });
    }

    private async Task VoltarAsync()
    {
        if (!PelaMetade)
            return;
        if (!_chaves.Ligada(Chave.AjustesAudio))
        {
            Mensagem = ControleParametroViewModel.MsgChaveDesligada;
            return;
        }
        await Agir(async () =>
        {
            var r = await _copia.VoltarAsync();
            return r.Ok ? MsgVoltou : Frase(r.Motivo);
        });
    }

    // ------------------------------------------------------------------ apoio

    /// <summary>Roda a ação do núcleo e mostra a frase dela; a tela se atualiza na sua thread.</summary>
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
                _ultimoAvisoGerente = _gerente.Aviso;
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

    private static string? Frase(ResultadoPreset? r) => r is null ? null : Frase(r.Mensagem ?? r.Motivo);

    private static string? Frase(string? motivo) =>
        string.IsNullOrEmpty(motivo) ? null : char.ToUpperInvariant(motivo[0]) + motivo[1..];

    private void AoMudarAlgo() => NaTela(Atualizar);

    private void AoMudarChave(Chave chave)
    {
        if (chave is Chave.AjustesAudio)
            NaTela(Atualizar);
    }

    private void Atualizar()
    {
        MontarItens();

        // Um aviso novo do núcleo (o resultado de um Terminar, por exemplo) aparece.
        var aviso = _gerente.Aviso;
        if (aviso != _ultimoAvisoGerente)
        {
            _ultimoAvisoGerente = aviso;
            if (aviso is not null)
                Mensagem = aviso;
        }

        // Uma folha de um preset que sumiu fecha sozinha.
        if (_renomear is { } r && _gerente.Obter(r) is null)
            FecharRenomear();
        if (_apagar is { } a && _gerente.Obter(a) is null)
            FecharApagar();

        Avisar(nameof(TextoVazio));
        Avisar(nameof(MotivoTravaAplicar));
        Avisar(nameof(MotivoTravaGuardar));
        Avisar(nameof(PodeAplicar));
        Avisar(nameof(PodeGuardar));
        Avisar(nameof(Ocupado));
        Avisar(nameof(PelaMetade));
        Avisar(nameof(TextoPelaMetade));
        AvisarRenomear();
        AvisarApagar();
    }

    /// <summary>Refaz a lista de itens a partir do gerente, mantendo o mesmo objeto para o mesmo id.</summary>
    private void MontarItens()
    {
        var presets = _gerente.Presets;
        var novos = new List<ItemPresetViewModel>(presets.Count);
        foreach (var p in presets)
        {
            if (!_porId.TryGetValue(p.Id, out var item))
                _porId[p.Id] = item = new ItemPresetViewModel(p, this, Despachante, Relogio);
            item.Nome = p.Nome;
            novos.Add(item);
        }
        foreach (var id in _porId.Keys.Where(id => presets.All(p => p.Id != id)).ToArray())
            _porId.Remove(id);

        if (_itens.Count == novos.Count && _itens.Zip(novos).All(par => ReferenceEquals(par.First, par.Second)))
            return;
        _itens = novos;
        Avisar(nameof(Itens));
    }

    public void Dispose()
    {
        _gerente.Mudou -= AoMudarAlgo;
        _copia.Mudou -= AoMudarAlgo;
        _sessao.Mudou -= AoMudarAlgo;
        _chaves.Mudou -= AoMudarChave;
        _estado.Recarregou -= AoMudarAlgo;
    }
}
