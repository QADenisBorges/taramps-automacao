using BgsControle.Nucleo;
using BgsControle.Nucleo.Escrita;
using BgsControle.Nucleo.Presets;
using BgsControle.Nucleo.Seguranca;
using BgsControle.Nucleo.Setups;

namespace BgsControle.Telas;

/// <summary>Uma linha da lista de presets: um herdado (com selo e nota) ou um próprio.</summary>
public sealed class ItemPresetViewModel : TelaBase
{
    private string _nome;
    private bool _emUso;
    private string? _motivoTravaApagar;

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
        Escolher = new Comando(() => tela.EscolherNaFolhaAsync(Id));
        Exportar = new Comando(() => tela.ExportarAsync(Id));
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

    /// <summary>É o preset em uso (o lembrado pela memória do celular em uso, ou o último aplicado): a linha em branco.</summary>
    public bool EmUso
    {
        get => _emUso;
        internal set
        {
            if (_emUso == value)
                return;
            _emUso = value;
            Avisar();
            Avisar(nameof(Detalhe));
        }
    }

    /// <summary>Por que Apagar está travado ("Preset herdado não muda.", "Lembrado por 2 memórias."), ou null.</summary>
    public string? MotivoTravaApagar
    {
        get => _motivoTravaApagar;
        internal set
        {
            if (_motivoTravaApagar == value)
                return;
            _motivoTravaApagar = value;
            Avisar();
            Avisar(nameof(PodeApagar));
        }
    }

    public bool PodeApagar => _motivoTravaApagar is null;

    /// <summary>Veio dos protótipos (CAP-6): não muda.</summary>
    public bool Herdado { get; }

    /// <summary>A nota herdada, ou null.</summary>
    public string? Nota { get; }

    /// <summary>Aplica o preset, sem confirmação (cópia antes, escreve, relê).</summary>
    public Comando Aplicar { get; }

    /// <summary>Abre o campo do nome (só os próprios; um herdado recusa).</summary>
    public Comando Renomear { get; }

    /// <summary>Abre a folha "Apagar o preset X?" (só os próprios que nenhuma memória lembra; senão, o motivo).</summary>
    public Comando Apagar { get; }

    /// <summary>Tocar na linha da folha de presets (o chip): aplica e fecha; o em uso só fecha.</summary>
    public Comando Escolher { get; }

    /// <summary>Exportar: grava o preset como arquivo JSON (não <c>.trps</c>) e abre o compartilhar.</summary>
    public Comando Exportar { get; }

    /// <summary>O detalhe da linha de escolha: "Em uso", ou null.</summary>
    public string? Detalhe => EmUso ? PresetsViewModel.TextoEmUso : null;
}

/// <summary>
/// A tela Presets no celular (Configurações › Presets, CAP-5): os presets de 15 bandas (o herdado
/// Cinema primeiro, com selo e nota, depois os próprios), Aplicar por item (só as 15 bandas, por cima
/// da memória; a memória do celular em uso passa a lembrar o preset), Renomear e Apagar com a folha
/// "Apagar o preset X?" (Confirmar ou Cancelar). Apagar fica travado num herdado e num preset que
/// alguma memória lembra ("Lembrado por 2 memórias."). O preset novo nasce na folha do Salvar. Cada
/// ação fica travada, com o motivo, sem conexão, em modo só leitura ou com a chave desligada. Depois
/// de uma queda no meio de um aplicar, mostra "aplicado pela metade" com Terminar e Voltar à cópia.
/// Renomear e apagar nunca mandam nada ao aparelho.
/// </summary>
public sealed class PresetsViewModel : TelaBase, IDisposable
{
    public const string TextoSemProprios = "Mexa nas 15 bandas e toque em Salvar para criar um preset.";
    public const string TextoCancelar = "Cancelar";
    public const string TextoRenomear = "Renomear";
    public const string TituloPelaMetade = "Aplicado pela metade";
    public const string TextoTerminar = "Terminar";
    public const string TextoVoltar = "Voltar à cópia";
    public const string MsgEmAndamento = "Uma operação está em andamento";
    public const string MsgVoltou = "O som voltou à cópia.";
    public const string TextoEmUso = "Em uso";
    public const string TextoExportar = "Exportar";
    public const string MsgSemExportar = "Exportar não está disponível neste aparelho.";

    /// <summary>"Preset Rock exportado."</summary>
    public static string MsgExportado(string nome) => $"Preset {nome} exportado.";

    /// <summary>
    /// O nome do arquivo do preset exportado: "preset-Rock.json". Espaço e todo caractere que não vale num
    /// nome de arquivo (<c>/ \ : * ? " &lt; &gt; |</c>, controle) viram _.
    /// </summary>
    public static string NomeArquivo(string nome)
    {
        var invalidos = new HashSet<char>(Path.GetInvalidFileNameChars()) { ' ', '/', '\\', ':', '*', '?', '"', '<', '>', '|' };
        var limpo = new string(nome.Select(c => invalidos.Contains(c) || char.IsControl(c) ? '_' : c).ToArray());
        return $"preset-{limpo}.json";
    }

    private readonly GerentePresets _gerente;
    private readonly GerenteSetups _setups;
    private readonly CopiaAntes _copia;
    private readonly Sessao _sessao;
    private readonly IChaves _chaves;
    private readonly EstadoAparelho _estado;
    private readonly IExportarArquivo? _exportar;

    private readonly Dictionary<string, ItemPresetViewModel> _porId = [];
    private IReadOnlyList<ItemPresetViewModel> _itens = [];
    private string? _mensagem;
    private string? _ultimoAvisoGerente;
    private string? _renomear;
    private string _nomeRenomear = "";
    private string? _erroRenomear;
    private string? _apagar;
    private bool _agindo;
    private bool _folha;

    public PresetsViewModel(
        GerentePresets gerente,
        GerenteSetups setups,
        CopiaAntes copia,
        Sessao sessao,
        IChaves chaves,
        EstadoAparelho estado,
        IDespachante despachante,
        TimeProvider relogio,
        IExportarArquivo? exportar = null)
        : base(despachante, relogio)
    {
        _gerente = gerente;
        _exportar = exportar;
        _setups = setups;
        _copia = copia;
        _sessao = sessao;
        _chaves = chaves;
        _estado = estado;

        ConfirmarRenomear = new Comando(ConfirmarRenomearAsync);
        CancelarRenomear = new Comando(FecharRenomear);
        ConfirmarApagar = new Comando(ConfirmarApagarAsync);
        CancelarApagar = new Comando(FecharApagar);
        Terminar = new Comando(TerminarAsync);
        VoltarACopia = new Comando(VoltarAsync);
        AbrirFolha = new Comando(AbrirAFolha);
        FecharFolha = new Comando(FecharAFolha);

        _ultimoAvisoGerente = _gerente.Aviso;
        _gerente.Mudou += AoMudarAlgo;
        _setups.Mudou += AoMudarAlgo;
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

    /// <summary>"Mexa nas 15 bandas e toque em Salvar para criar um preset." quando só há o herdado; senão null.</summary>
    public string? TextoVazio => _gerente.Proprios.Count == 0 ? TextoSemProprios : null;

    /// <summary>A última frase: "Preset Rock aplicado na FILME.", "Chave desligada", "Já existe um preset com esse nome."</summary>
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

    public bool PodeAplicar => MotivoTravaAplicar is null;

    // ------------------------------------------------------------------ aplicar

    /// <summary>
    /// Aplicar: age direto, sem confirmação (a cópia antes é guardada). Com uma memória do celular em
    /// uso, ela passa a lembrar o preset: "Preset Rock aplicado na FILME.". Travado, nada sai e o
    /// motivo aparece.
    /// </summary>
    public async Task AplicarAsync(string id)
    {
        if (MotivoTravaAplicar is { } motivo)
        {
            Mensagem = motivo;
            return;
        }
        await Agir(async () =>
        {
            var r = await _gerente.AplicarAsync(id);
            if (r is { Ok: true, Preset: { } preset } && r.Mensagem == GerentePresets.MsgAplicado(preset.Nome)
                && _setups.MemoriaEmUso is { } memoria && memoria.IdPreset == preset.Id)
                return MsgAplicadoNa(preset.Nome, memoria.Nome);
            return Frase(r);
        });
    }

    /// <summary>"Preset Rock aplicado na FILME."</summary>
    public static string MsgAplicadoNa(string preset, string memoria) => $"Preset {preset} aplicado na {memoria}.";

    // ------------------------------------------------------------------ folha de presets (o chip)

    /// <summary>A folha de presets do chip "Preset: Cinema ▾" (Início) está aberta.</summary>
    public bool FolhaAberta
    {
        get => _folha;
        private set
        {
            if (_folha == value)
                return;
            _folha = value;
            Avisar();
        }
    }

    /// <summary>"Preset da FILME" (a memória do celular em uso), ou "Presets".</summary>
    public string TituloFolha => _setups.MemoriaEmUso is { } m ? $"Preset da {m.Nome}" : "Presets";

    /// <summary>"Só as 15 bandas mudam, por cima da memória."</summary>
    public string ApoioFolha => "Só as 15 bandas mudam, por cima da memória. A memória passa a lembrar o preset.";

    /// <summary>Tocar no chip: abre a folha, com a lista e o preset em uso em branco.</summary>
    public Comando AbrirFolha { get; }

    /// <summary>O véu ou o Voltar: fecha sem mudar nada.</summary>
    public Comando FecharFolha { get; }

    private void AbrirAFolha()
    {
        Mensagem = null;
        FolhaAberta = true;
        Avisar(nameof(TituloFolha));
    }

    private void FecharAFolha() => FolhaAberta = false;

    /// <summary>
    /// Tocar numa linha da folha: o em uso só fecha; outro preset fecha a folha e aplica (sem
    /// confirmação, cópia antes, releitura). Travado, a folha fica aberta com o motivo.
    /// </summary>
    public async Task EscolherNaFolhaAsync(string id)
    {
        if (id == _setups.IdPresetEmUso)
        {
            FolhaAberta = false;
            return;
        }
        if (MotivoTravaAplicar is { } motivo)
        {
            Mensagem = motivo;
            return;
        }
        FolhaAberta = false;
        await AplicarAsync(id);
    }

    // ------------------------------------------------------------------ exportar

    /// <summary>Exportar: o preset em JSON (formato próprio, não <c>.trps</c>) para o compartilhar do Android. Nada vai pelo BLE.</summary>
    public async Task ExportarAsync(string id)
    {
        if (_gerente.Obter(id) is not { } preset || _gerente.ExportarJson(id) is not { } json)
        {
            Mensagem = GerentePresets.MsgNaoExiste;
            return;
        }
        if (_exportar is null)
        {
            Mensagem = MsgSemExportar;
            return;
        }
        string? motivo;
        try
        {
            motivo = await _exportar.ExportarAsync(NomeArquivo(preset.Nome), json);
        }
        catch (Exception e)
        {
            motivo = $"Erro inesperado ({e.GetType().Name}).";
        }
        var frase = motivo ?? MsgExportado(preset.Nome);
        NaTela(() => Mensagem = frase);
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

    /// <summary>
    /// "Apagar": abre a folha de confirmação. Um herdado ou um preset lembrado recusa com o motivo;
    /// ocupado, só o motivo. Nada sai antes do botão.
    /// </summary>
    public void AbrirApagar(string id)
    {
        if (RecusaMudar(id))
            return;
        if (_gerente.MotivoTravaApagar(id) is { } lembrado)
        {
            Mensagem = lembrado;
            return;
        }
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
        Avisar(nameof(TituloFolha));
        Avisar(nameof(MotivoTravaAplicar));
        Avisar(nameof(PodeAplicar));
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
        var emUso = _setups.IdPresetEmUso;
        var novos = new List<ItemPresetViewModel>(presets.Count);
        foreach (var p in presets)
        {
            if (!_porId.TryGetValue(p.Id, out var item))
                _porId[p.Id] = item = new ItemPresetViewModel(p, this, Despachante, Relogio);
            item.Nome = p.Nome;
            item.EmUso = p.Id == emUso;
            item.MotivoTravaApagar = _gerente.MotivoTravaApagar(p.Id);
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
        _setups.Mudou -= AoMudarAlgo;
        _copia.Mudou -= AoMudarAlgo;
        _sessao.Mudou -= AoMudarAlgo;
        _chaves.Mudou -= AoMudarChave;
        _estado.Recarregou -= AoMudarAlgo;
    }
}
