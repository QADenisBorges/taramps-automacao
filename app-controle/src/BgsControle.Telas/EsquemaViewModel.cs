using System.ComponentModel;
using BgsControle.Nucleo.Setups;
using BgsControle.Telas.Esquema;

namespace BgsControle.Telas;

/// <summary>
/// O esquema do Setup (CAP-17, história 10b): a cena do Setup mostrado pela lista de equipamentos
/// (<see cref="EquipamentosViewModel"/>), refeita sozinha sempre que o Setup mostrado, os equipamentos ou
/// os links mudam; o ângulo de visão, a aproximação e o destaque de um canal. A view só pinta a
/// <see cref="Projecao"/> e repassa os gestos: arrastar gira, pinçar aproxima, tocar numa peça abre a
/// folha dela (a mesma da lista), tocar num canal do processador destaca tudo o que ele alimenta. Nada
/// vai pelo BLE.
/// </summary>
public sealed class EsquemaViewModel : TelaBase, IDisposable
{
    /// <summary>Arrastar na horizontal: graus por pixel (200 px passam do fim).</summary>
    public const double GrausPorPixel = 0.4;

    /// <summary>O que não está destacado fica a 35 %.</summary>
    public const double OpacidadeApagada = 0.35;

    /// <summary>Uma peça sem ligação fica apagada.</summary>
    public const double OpacidadeSemLigacao = 0.45;

    private readonly EquipamentosViewModel _equipamentos;
    private CenaSetup _cena = CenaSetup.Montar([]);
    private ProjecaoIsometrica? _projecao;
    private double _largura = ProjecaoIsometrica.LarguraPadrao;
    private double _altura = ProjecaoIsometrica.AlturaPadrao;
    private double _angulo;
    private double _aproximacao = 1;
    private double _desvioX;
    private double _desvioY;
    private string? _idSetup;
    private int? _destaque;
    private string? _selecionada;
    private int _versao;

    public EsquemaViewModel(EquipamentosViewModel equipamentos, IDespachante despachante, TimeProvider relogio)
        : base(despachante, relogio)
    {
        _equipamentos = equipamentos;
        _equipamentos.PropertyChanged += AoMudarLista;
        _idSetup = _equipamentos.Setup?.Id;
        Refazer();
    }

    public void Dispose() => _equipamentos.PropertyChanged -= AoMudarLista;

    /// <summary>A cena do Setup mostrado.</summary>
    public CenaSetup Cena => _cena;

    /// <summary>Muda a cada vez que algo do desenho muda (a view pinta de novo).</summary>
    public int Versao => _versao;

    /// <summary>O ângulo de visão, em graus: −45° a +45° (0° é o isométrico padrão).</summary>
    public double Angulo => _angulo;

    /// <summary>A aproximação: 0,6× a 2,5×.</summary>
    public double Aproximacao => _aproximacao;

    /// <summary>O canal destacado (0 a 3), ou null.</summary>
    public int? Destaque => _destaque;

    /// <summary>O equipamento tocado por último (contorno branco), enquanto ele existir.</summary>
    public string? Selecionada => _selecionada;

    /// <summary>
    /// Sem equipamentos, a frase embaixo do processador ("Adicione os equipamentos ligados em cada canal.");
    /// sem Setup, o pedido para escolher um; senão, null.
    /// </summary>
    public string? TextoVazio => !_cena.Vazia ? null
        : _equipamentos.Setup is null ? EquipamentosViewModel.MsgSemSetup : EquipamentosViewModel.TextoVazioSetup;

    /// <summary>A projeção com o tamanho da área, o ângulo, a aproximação e o desvio de agora.</summary>
    public ProjecaoIsometrica Projecao => _projecao ??= new ProjecaoIsometrica(_cena, _largura, _altura, _angulo, _aproximacao, _desvioX, _desvioY);

    /// <summary>
    /// A altura da área para a largura dada: 280 no mínimo; um Setup com muitas fileiras ganha altura (até
    /// 640) para as peças e os rótulos não ficarem miúdos.
    /// </summary>
    public double AlturaDesejada(double largura) => ProjecaoIsometrica.AlturaDesejada(_cena, largura);

    /// <summary>O tamanho da área do esquema, em pixels (a view informa ao pintar).</summary>
    public void Tamanho(double largura, double altura)
    {
        if (largura == _largura && altura == _altura)
            return;
        _largura = largura;
        _altura = altura;
        _projecao = null;
    }

    /// <summary>Arrastar na horizontal gira o ângulo (para a direita, positivo), preso em −45° e +45°; soltar não volta.</summary>
    public void Arrastar(double dx)
    {
        if (double.IsNaN(dx) || double.IsInfinity(dx))
            return;
        var novo = Math.Clamp(_angulo + dx * GrausPorPixel, ProjecaoIsometrica.AnguloMinimo, ProjecaoIsometrica.AnguloMaximo);
        if (novo == _angulo)
            return;
        _angulo = novo;
        Mudou(nameof(Angulo));
    }

    /// <summary>
    /// Pinçar aproxima pela escala do gesto (2 = o dobro), presa entre 0,6× e 2,5×, em volta do ponto dos
    /// dedos (x, y em pixels da área; sem ponto, o meio): o que está embaixo dos dedos fica no lugar, e a
    /// vista se desloca só até a borda da cena, para tudo continuar alcançável.
    /// </summary>
    public void Pincar(double escala, double x = double.NaN, double y = double.NaN)
    {
        if (!(escala > 0) || double.IsInfinity(escala))
            return;
        var nova = Projecao.Pincar(escala, x, y);
        if (nova.Aproximacao == _aproximacao && nova.DesvioX == _desvioX && nova.DesvioY == _desvioY)
            return;
        _aproximacao = nova.Aproximacao;
        _desvioX = nova.DesvioX;
        _desvioY = nova.DesvioY;
        Mudou(nameof(Aproximacao));
    }

    /// <summary>
    /// Um toque em (x, y), em pixels da área: numa peça, abre a folha dela; num canal do processador,
    /// destaca o que ele alimenta (de novo no mesmo canal, tira); no vazio, tira o destaque.
    /// </summary>
    public void Tocar(double x, double y)
    {
        var alvo = Projecao.Tocar(x, y);
        if (alvo.Canal is { } canal)
        {
            _destaque = _destaque == canal ? null : canal;
            Mudou(nameof(Destaque));
            return;
        }
        if (alvo.IdPeca is { } id)
        {
            _selecionada = id;
            Mudou(nameof(Selecionada));
            _equipamentos.AbrirEditar(id);
            return;
        }
        if (_destaque is null && _selecionada is null)
            return;
        _destaque = null;
        _selecionada = null;
        Mudou(nameof(Destaque));
        Avisar(nameof(Selecionada));
    }

    // ------------------------------------------------------------------ o destaque

    /// <summary>A peça está no destaque (sem destaque, todas estão). O processador sempre está.</summary>
    public bool Destacada(PecaCena peca) => _destaque is not { } c || !peca.Equipamento || peca.Canais.Contains(c);

    /// <summary>A opacidade da peça: sem ligação, apagada; fora do destaque, 35 %.</summary>
    public double Opacidade(PecaCena peca)
    {
        var base_ = peca.SemLigacao ? OpacidadeSemLigacao : 1;
        return Destacada(peca) ? base_ : Math.Min(base_, OpacidadeApagada);
    }

    /// <summary>A opacidade de um cabo (o que está fora do destaque fica a 35 %).</summary>
    public double Opacidade(CaboCena cabo) => Opacidade(cabo.Canais);

    public double Opacidade(JuncaoCena juncao) => Opacidade(juncao.Canais);

    public double Opacidade(BridgeCena bridge) => Opacidade(bridge.Canais);

    /// <summary>Uma porta (CHn do processador, a borda de uma entrada, uma saída): só as do canal destacado ficam acesas.</summary>
    public double Opacidade(PortaCena porta) => Opacidade(porta.Canais);

    private double Opacidade(IReadOnlySet<int> canais) => _destaque is not { } c || canais.Contains(c) ? 1 : OpacidadeApagada;

    // ------------------------------------------------------------------ ao vivo

    private void AoMudarLista(object? sender, PropertyChangedEventArgs e)
    {
        // A lista avisa os Itens a cada mudança do Setup mostrado (Mostrar, equipamentos, links).
        if (e.PropertyName == nameof(EquipamentosViewModel.Itens))
        {
            Refazer();
        }
        else if (e.PropertyName == nameof(EquipamentosViewModel.FolhaAberta) && !_equipamentos.FolhaAberta && _selecionada is not null)
        {
            // A folha fechou: o contorno da peça tocada sai.
            _selecionada = null;
            Mudou(nameof(Selecionada));
        }
    }

    private void Refazer()
    {
        var setup = _equipamentos.Setup;
        if (setup?.Id != _idSetup)
        {
            // Outro Setup: a vista volta ao começo (ângulo 0°, 1×, sem destaque nem contorno).
            _idSetup = setup?.Id;
            _angulo = 0;
            _aproximacao = 1;
            _desvioX = _desvioY = 0;
            _destaque = null;
            _selecionada = null;
        }
        _cena = setup is null
            ? CenaSetup.Montar([])
            : CenaSetup.Montar(setup.Equipamentos, e => e.Funcao ?? GerenteSetups.FuncaoSugerida(setup, e));
        if (_selecionada is not null && _cena.Peca(_selecionada) is null)
            _selecionada = null;
        _projecao = null;
        Avisar(nameof(Cena));
        Avisar(nameof(TextoVazio));
        Avisar(nameof(Angulo));
        Avisar(nameof(Aproximacao));
        Avisar(nameof(Destaque));
        Avisar(nameof(Selecionada));
        Mudou(null);
    }

    private void Mudou(string? nome)
    {
        _projecao = null;
        _versao++;
        if (nome is not null)
            Avisar(nome);
        Avisar(nameof(Versao));
    }
}
