using System.ComponentModel;
using BgsControle.Nucleo;
using BgsControle.Nucleo.Escrita;
using BgsControle.Nucleo.Setups;

namespace BgsControle.Telas;

/// <summary>Um dos 3 botões de memória: a memória do celular daquela vaga (ou vazia), em branco quando é a em uso.</summary>
public sealed class BotaoMemoriaViewModel : TelaBase
{
    private string? _id;
    private string _nome = "";
    private bool _emUso;
    private bool _travado;

    internal BotaoMemoriaViewModel(int vaga, BotoesMemoriaViewModel tela, IDespachante despachante, TimeProvider relogio)
        : base(despachante, relogio)
    {
        Vaga = vaga;
        Tocar = new Comando(() => _id is { } id ? tela.AplicarAsync(id) : Task.CompletedTask);
    }

    /// <summary>A posição (0 a 2). A memória que entra ocupa a vaga da usada há mais tempo; as outras não mudam de lugar.</summary>
    public int Vaga { get; }

    /// <summary>O id da memória na vaga, ou null (vaga vazia: o botão some).</summary>
    public string? Id => _id;

    public bool Visivel => _id is not null;

    public string Nome => _nome;

    /// <summary>A memória em uso: o botão em branco, com o texto preto.</summary>
    public bool EmUso => _emUso;

    /// <summary>Sem Setup (ou na dúvida): o botão travado, com o texto apagado.</summary>
    public bool Travado => _travado;

    /// <summary>Aplica a memória, sem confirmação.</summary>
    public Comando Tocar { get; }

    internal void Definir(MemoriaCelular? memoria, bool emUso, bool travado)
    {
        var id = memoria?.Id;
        var nome = memoria?.Nome ?? "";
        if (id != _id)
        {
            _id = id;
            Avisar(nameof(Id));
            Avisar(nameof(Visivel));
        }
        if (nome != _nome)
        {
            _nome = nome;
            Avisar(nameof(Nome));
        }
        if (emUso != _emUso)
        {
            _emUso = emUso;
            Avisar(nameof(EmUso));
        }
        if (travado != _travado)
        {
            _travado = travado;
            Avisar(nameof(Travado));
        }
    }
}

/// <summary>Uma linha de escolha da lista de memórias ("…"): nome, detalhe, em uso, travada.</summary>
public sealed class LinhaMemoriaViewModel
{
    internal LinhaMemoriaViewModel(string nome, string? detalhe, bool emUso, bool travada, Func<Task> tocar)
    {
        Nome = nome;
        Detalhe = detalhe;
        EmUso = emUso;
        Travada = travada;
        Tocar = new Comando(tocar);
    }

    public string Nome { get; }

    /// <summary>"Preset: Cinema", ou null.</summary>
    public string? Detalhe { get; }

    /// <summary>A que está em uso fica em branco.</summary>
    public bool EmUso { get; }

    /// <summary>Sem Setup, as memórias do celular ficam travadas (nome apagado).</summary>
    public bool Travada { get; }

    /// <summary>Aplica e fecha a lista.</summary>
    public Comando Tocar { get; }
}

/// <summary>
/// Os 3 botões de memória do Início e da Mesa e a lista "…" (EXPERIENCE, Component Patterns › Memória):
/// as 3 memórias do celular usadas por último no Setup em uso, que não mudam de lugar, e o "…", que
/// abre a lista com as memórias do celular do Setup. As memórias do processador não aparecem
/// (decisão do Denis, 29/10/2026). Tocar aplica, sem confirmação, pelas travas da tela Memórias
/// (<see cref="MemoriasViewModel"/>). Sem Setup, as memórias do celular ficam travadas com a frase.
/// </summary>
public sealed class BotoesMemoriaViewModel : TelaBase, IDisposable
{
    private readonly GerenteSetups _setups;
    private readonly MemoriasViewModel _tela;
    private readonly Sessao _sessao;

    private IReadOnlyList<LinhaMemoriaViewModel> _celular = [];
    private bool _listaAberta;
    private string? _mensagem;
    private string? _aplicando;
    private bool _agindo;

    public BotoesMemoriaViewModel(
        GerenteSetups setups,
        MemoriasViewModel tela,
        Sessao sessao,
        IDespachante despachante,
        TimeProvider relogio)
        : base(despachante, relogio)
    {
        _setups = setups;
        _tela = tela;
        _sessao = sessao;
        Botoes = Enumerable.Range(0, GerenteSetups.QuantosBotoes).Select(v => new BotaoMemoriaViewModel(v, this, despachante, relogio)).ToArray();
        AbrirLista = new Comando(() =>
        {
            Mensagem = null;
            ListaAberta = true;
        });
        FecharLista = new Comando(() => ListaAberta = false);

        _setups.Mudou += AoMudar;
        _sessao.Mudou += AoMudar;
        _tela.PropertyChanged += AoMudarTela;
        Atualizar();
    }

    /// <summary>Os 3 botões, sempre na mesma ordem (a vaga não muda).</summary>
    public IReadOnlyList<BotaoMemoriaViewModel> Botoes { get; }

    /// <summary>O botão pelo nome da memória, ou null.</summary>
    public BotaoMemoriaViewModel? Botao(string nome) =>
        Botoes.FirstOrDefault(b => string.Equals(b.Nome, nome, StringComparison.OrdinalIgnoreCase));

    /// <summary>Sem Setup (ou na dúvida): "Escolha um Setup para usar as memórias do celular.", ou null.</summary>
    public string? MotivoTravaCelular => _tela.MotivoTravaCelular;

    public bool TravadoCelular => MotivoTravaCelular is not null;

    /// <summary>A última frase: "Memória FILME aplicada.", "Chave desligada"…</summary>
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

    /// <summary>"Aplicando a FILME" até a releitura; os valores esmaecem.</summary>
    public string? Aplicando
    {
        get => _aplicando;
        private set
        {
            if (_aplicando == value)
                return;
            _aplicando = value;
            Avisar();
            Avisar(nameof(Esmaecido));
        }
    }

    public bool Esmaecido => _aplicando is not null;

    // ------------------------------------------------------------------ a lista "…"

    public bool ListaAberta
    {
        get => _listaAberta;
        private set
        {
            if (_listaAberta == value)
                return;
            _listaAberta = value;
            Avisar();
        }
    }

    public Comando AbrirLista { get; }

    public Comando FecharLista { get; }

    /// <summary>As memórias do celular do Setup em uso, na ordem em que foram criadas.</summary>
    public IReadOnlyList<LinhaMemoriaViewModel> LinhasCelular => _celular;

    /// <summary>"Memórias do Casa 2.1", ou null sem Setup.</summary>
    public string? TituloCelular => _tela.TituloCelular;

    // ------------------------------------------------------------------ aplicar

    /// <summary>Aplica a memória do celular: sem confirmação, pelas travas da tela Memórias.</summary>
    public async Task AplicarAsync(string id)
    {
        if (_agindo)
            return; // um segundo toque com o primeiro rodando não faz nada
        if (_tela.MotivoTravaAplicar is { } motivo)
        {
            Mensagem = motivo;
            return;
        }
        ListaAberta = false; // tocar escolhe e fecha a lista, também na memória em uso
        if (_setups.EmUso is { Tipo: TipoSomEmUso.MemoriaCelular } uso && uso.IdMemoria == id)
            return; // já é a em uso: nada a aplicar
        Aplicando = _setups.Memoria(id) is { } m ? $"Aplicando a {m.Nome}" : null;
        _agindo = true;
        try
        {
            await _tela.AplicarAsync(id);
        }
        finally
        {
            _agindo = false;
            Fim();
        }
    }

    private void Fim()
    {
        var feito = _tela.Mensagem;
        NaTela(() =>
        {
            Aplicando = null;
            Mensagem = feito;
        });
    }

    // ------------------------------------------------------------------ apoio

    private void AoMudar() => NaTela(Atualizar);

    private void AoMudarTela(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MemoriasViewModel.MotivoTravaCelular) or nameof(MemoriasViewModel.TituloCelular))
            Atualizar();
    }

    private void Atualizar()
    {
        var setup = _setups.SetupEmUso ?? (_sessao.Estado == EstadoSessao.Conectado ? _setups.Setup(_setups.UltimoSetup) : null);
        var travado = TravadoCelular;
        var uso = _setups.EmUso;
        string? emUso = uso.Tipo == TipoSomEmUso.MemoriaCelular ? uso.IdMemoria : null;

        var botoes = _setups.Botoes(setup?.Id);
        for (var v = 0; v < Botoes.Count; v++)
        {
            var m = v < botoes.Count ? botoes[v] : null;
            Botoes[v].Definir(m, m is not null && m.Id == emUso, travado);
        }

        _celular = _setups.MemoriasDe(setup?.Id)
            .Select(m => new LinhaMemoriaViewModel(m.Nome,
                _setups.PresetDe(m) is { } p ? MemoriasViewModel.TextoPresetDe(p.Nome) : null,
                m.Id == emUso, travado, () => AplicarAsync(m.Id)))
            .ToArray();

        Avisar(nameof(LinhasCelular));
        Avisar(nameof(TituloCelular));
        Avisar(nameof(MotivoTravaCelular));
        Avisar(nameof(TravadoCelular));
    }

    public void Dispose()
    {
        _setups.Mudou -= AoMudar;
        _sessao.Mudou -= AoMudar;
        _tela.PropertyChanged -= AoMudarTela;
    }
}
