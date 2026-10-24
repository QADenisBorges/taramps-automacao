namespace BgsControle.Telas.Esquema;

/// <summary>Um ponto na tela, em pixels (x para a direita, y para baixo).</summary>
public readonly record struct Ponto2(double X, double Y);

/// <summary>O que um toque acertou: uma saída CHn do processador (<see cref="Canal"/>), uma peça, ou nada.</summary>
public sealed record AlvoToque(string? IdPeca, int? Canal)
{
    public static AlvoToque Vazio { get; } = new(null, null);

    public bool Nada => IdPeca is null && Canal is null;
}

/// <summary>
/// A projeção isométrica da cena do esquema (história 10b): gira a cena em volta do eixo vertical pelo
/// ângulo de visão (−45° a +45°; 0° é o isométrico padrão, com as caixas a 45°), olha de cima no ângulo do
/// isométrico (o chão encolhe para tan 30° e a altura para cos 35,26°) e aproxima de 0,6× a 2,5×. A 1× a
/// cena inteira cabe na área, no ângulo em que está (girar não esconde peça). Também faz o teste de toque:
/// a peça que fica na frente ganha.
/// </summary>
public sealed class ProjecaoIsometrica
{
    public const double AnguloMinimo = -45;
    public const double AnguloMaximo = 45;
    public const double AproximacaoMinima = 0.6;
    public const double AproximacaoMaxima = 2.5;

    /// <summary>A área padrão (o celular em pé, 280 de altura), quando a view ainda não tem tamanho.</summary>
    public const double LarguraPadrao = 360;
    public const double AlturaPadrao = 280;

    /// <summary>As margens da área, em pixels: dos lados e embaixo sobra lugar para os rótulos.</summary>
    public const double MargemLado = 24;
    public const double MargemTopo = 10;
    public const double MargemBaixo = 36;

    /// <summary>Embaixo do processador sozinho fica a frase do Setup vazio.</summary>
    public const double MargemBaixoVazia = 64;

    /// <summary>O toque numa saída CHn vale até esta distância, em pixels (a mais perto ganha).</summary>
    public const double RaioToqueCanal = 20;

    /// <summary>O chão encolhe para tan 30° (o seno da elevação do isométrico, 35,26°).</summary>
    public static readonly double Chao = Math.Tan(Math.PI / 6);

    /// <summary>A altura encolhe para o cosseno da elevação do isométrico.</summary>
    public static readonly double Altura = Math.Sqrt(1 - Chao * Chao);

    private const double EscalaMaxima = 1.3;
    private const double EscalaMinima = 0.05;

    private readonly CenaSetup _cena;
    private readonly double _cos;
    private readonly double _sin;
    private readonly double _cx;
    private readonly double _cy;
    private readonly double _meioX;
    private readonly double _meioY;

    /// <param name="desvioX">O deslocamento da vista, em pixels (vem do pinçar fora do meio); preso para a cena continuar alcançável.</param>
    /// <param name="desvioY">Idem, na vertical.</param>
    public ProjecaoIsometrica(CenaSetup cena, double largura, double altura, double angulo, double aproximacao,
        double desvioX = 0, double desvioY = 0)
    {
        _cena = cena;
        if (!(largura > 1) || !(altura > 1) || double.IsInfinity(largura) || double.IsInfinity(altura))
            (largura, altura) = (LarguraPadrao, AlturaPadrao);
        Largura = largura;
        AlturaArea = altura;
        Angulo = Math.Clamp(double.IsNaN(angulo) ? 0 : angulo, AnguloMinimo, AnguloMaximo);
        Aproximacao = Math.Clamp(double.IsNaN(aproximacao) ? 1 : aproximacao, AproximacaoMinima, AproximacaoMaxima);
        var rad = Angulo * Math.PI / 180;
        _cos = Math.Cos(rad);
        _sin = Math.Sin(rad);

        // A escala que faz a cena caber no ângulo de agora, com as margens dos rótulos.
        var baixo = cena.Vazia ? MargemBaixoVazia : MargemBaixo;
        var uteisL = Math.Max(1, largura - 2 * MargemLado);
        var uteisA = Math.Max(1, altura - MargemTopo - baixo);
        var (minX, maxX, minY, maxY) = Limites(cena, _cos, _sin);
        var ajuste = Math.Min(uteisL / Math.Max(1, maxX - minX), uteisA / Math.Max(1, maxY - minY));
        EscalaBase = Math.Clamp(ajuste, EscalaMinima, EscalaMaxima);
        Escala = EscalaBase * Aproximacao;
        _meioX = (minX + maxX) / 2;
        _meioY = (minY + maxY) / 2;
        _cx = MargemLado + uteisL / 2;
        _cy = MargemTopo + uteisA / 2;

        // O desvio só vai até a borda da cena chegar à borda da área: a 1× é zero; aproximada, dá para
        // trazer qualquer ponta da cena para dentro.
        var limiteX = Math.Max(0, ((maxX - minX) * Escala - uteisL) / 2);
        var limiteY = Math.Max(0, ((maxY - minY) * Escala - uteisA) / 2);
        DesvioX = Math.Clamp(double.IsFinite(desvioX) ? desvioX : 0, -limiteX, limiteX);
        DesvioY = Math.Clamp(double.IsFinite(desvioY) ? desvioY : 0, -limiteY, limiteY);
    }

    /// <summary>O deslocamento da vista, em pixels, já preso.</summary>
    public double DesvioX { get; }

    public double DesvioY { get; }

    /// <summary>
    /// A projeção depois de pinçar pela escala com os dedos em (x, y): a aproximação muda (presa entre
    /// 0,6× e 2,5×) e o ponto da cena embaixo dos dedos fica no lugar (o desvio acompanha, preso).
    /// </summary>
    public ProjecaoIsometrica Pincar(double escala, double x, double y)
    {
        if (!(escala > 0) || double.IsInfinity(escala))
            return this;
        var nova = Math.Clamp(Aproximacao * escala, AproximacaoMinima, AproximacaoMaxima);
        var f = nova / Aproximacao;
        if (!double.IsFinite(x) || !double.IsFinite(y))
            (x, y) = (_cx + DesvioX, _cy + DesvioY);
        return new ProjecaoIsometrica(_cena, Largura, AlturaArea, Angulo, nova,
            (x - _cx) * (1 - f) + f * DesvioX, (y - _cy) * (1 - f) + f * DesvioY);
    }

    public double Largura { get; }

    public double AlturaArea { get; }

    /// <summary>O ângulo de visão, em graus, preso entre −45° e +45°.</summary>
    public double Angulo { get; }

    /// <summary>A aproximação, presa entre 0,6× e 2,5×.</summary>
    public double Aproximacao { get; }

    /// <summary>Os pixels por unidade da cena a 1× (a cena inteira cabe no ângulo de agora).</summary>
    public double EscalaBase { get; }

    /// <summary>Os pixels por unidade da cena agora (a base vezes a aproximação).</summary>
    public double Escala { get; }

    /// <summary>
    /// A altura que a área precisa para a cena caber pela largura no ângulo 0° (sem passar de 1 pixel por
    /// unidade): nunca menos que <see cref="AlturaPadrao"/> (a altura mínima) e no máximo
    /// <see cref="AlturaMaxima"/>. Um Setup com muitas fileiras ganha altura em vez de ficar miúdo.
    /// </summary>
    public static double AlturaDesejada(CenaSetup cena, double largura)
    {
        if (!(largura > 1) || double.IsInfinity(largura))
            largura = LarguraPadrao;
        var (minX, maxX, minY, maxY) = Limites(cena, 1, 0);
        var escala = Math.Min(1, Math.Max(1, largura - 2 * MargemLado) / Math.Max(1, maxX - minX));
        var baixo = cena.Vazia ? MargemBaixoVazia : MargemBaixo;
        return Math.Clamp(Math.Ceiling((maxY - minY) * escala + MargemTopo + baixo), AlturaPadrao, AlturaMaxima);
    }

    /// <summary>A área não cresce além disto (a página rola).</summary>
    public const double AlturaMaxima = 640;

    /// <summary>Os limites da cena girada (sem escala), em volta do meio dela.</summary>
    private static (double MinX, double MaxX, double MinY, double MaxY) Limites(CenaSetup cena, double cos, double sin)
    {
        double minX = double.MaxValue, maxX = double.MinValue, minY = double.MaxValue, maxY = double.MinValue;
        foreach (var p in Pontos(cena))
        {
            var du = p.U - cena.Centro.U;
            var dv = p.V - cena.Centro.V;
            var x = du * cos - dv * sin;
            var y = (du * sin + dv * cos) * Chao - p.H * Altura;
            minX = Math.Min(minX, x);
            maxX = Math.Max(maxX, x);
            minY = Math.Min(minY, y);
            maxY = Math.Max(maxY, y);
        }
        return (minX, maxX, minY, maxY);
    }

    private static IEnumerable<Ponto3> Pontos(CenaSetup cena) =>
        cena.Pecas.SelectMany(p => p.Contorno()).Concat(cena.Portas.Select(p => p.Posicao)).Concat(cena.Cabos.SelectMany(c => c.Pontos));

    /// <summary>O ponto no chão girado pelo ângulo, em volta do meio da cena: (para a direita, para a frente).</summary>
    private (double X, double Z) Girar(double u, double v)
    {
        var du = u - _cena.Centro.U;
        var dv = v - _cena.Centro.V;
        return (du * _cos - dv * _sin, du * _sin + dv * _cos);
    }

    /// <summary>O ponto da cena na tela, em pixels.</summary>
    public Ponto2 Projetar(Ponto3 p)
    {
        var (x, z) = Girar(p.U, p.V);
        var y = z * Chao - p.H * Altura;
        return new Ponto2(_cx + DesvioX + (x - _meioX) * Escala, _cy + DesvioY + (y - _meioY) * Escala);
    }

    /// <summary>O quanto o ponto fica na frente (maior = mais perto de quem olha).</summary>
    public double Profundidade(Ponto3 p) => Girar(p.U, p.V).Z;

    /// <summary>A direção (no chão) gira junto com a cena; devolve o quanto ela aponta para quem olha (&gt; 0 aparece) e para a direita.</summary>
    public (double Direita, double Frente) Direcao(Ponto3 d) => (d.U * _cos - d.V * _sin, d.U * _sin + d.V * _cos);

    /// <summary>Uma face com essa normal (no chão) aparece.</summary>
    public bool Aparece(Ponto3 normal) => Direcao(normal).Frente > 1e-6;

    /// <summary>
    /// A porta (e o rótulo CHn dela) aparece: num cilindro sempre; numa caixa, quando a face dela aparece ou
    /// fica de perfil (a ±45° as faces das portas ficam de perfil e as portas continuam tocáveis).
    /// </summary>
    public bool PortaAparece(PecaCena peca, PortaCena porta) => peca.Cilindro || Direcao(porta.Face).Frente > -1e-6;

    /// <summary>O círculo no chão (ou em qualquer altura) vira uma elipse: os raios na tela.</summary>
    public (double Rx, double Ry) Elipse(double raio) => (raio * Escala, raio * Chao * Escala);

    /// <summary>As peças de trás para a frente (a ordem da pintura); na mesma profundidade, a mais à direita por cima.</summary>
    public IReadOnlyList<PecaCena> OrdemDePintura() =>
        _cena.Pecas.OrderBy(p => Profundidade(p.Base)).ThenBy(p => Girar(p.Base.U, p.Base.V).X).ThenBy(p => p.Base.U).ToArray();

    /// <summary>A silhueta da peça na tela: o contorno convexo dos pontos projetados.</summary>
    public IReadOnlyList<Ponto2> Silhueta(PecaCena peca) => Envoltoria(peca.Contorno().Select(Projetar).ToArray());

    /// <summary>
    /// O que o toque em (x, y) acerta: a peça cuja silhueta tem o ponto, a da frente ganhando; senão a
    /// saída CH1 a CH4 do processador mais perto, até <see cref="RaioToqueCanal"/> pixels; senão, nada. O
    /// corpo do processador não abre folha: conta como vazio.
    /// </summary>
    public AlvoToque Tocar(double x, double y)
    {
        var ponto = new Ponto2(x, y);
        // Na ordem contrária à da pintura: a que fica por cima na tela ganha.
        foreach (var peca in OrdemDePintura().Where(p => p.Equipamento).Reverse())
        {
            if (Dentro(Silhueta(peca), ponto))
                return new AlvoToque(peca.Id, null);
        }

        var raio = Math.Max(RaioToqueCanal, 14 * Escala);
        PortaCena? perto = null;
        var melhor = double.MaxValue;
        foreach (var porta in _cena.Portas.Where(p => p.Tipo == TipoPorta.Processador))
        {
            var t = Projetar(porta.Posicao);
            var d = Math.Sqrt((t.X - x) * (t.X - x) + (t.Y - y) * (t.Y - y));
            if (d <= raio && d < melhor)
            {
                melhor = d;
                perto = porta;
            }
        }
        return perto is not null ? new AlvoToque(null, perto.Numero) : AlvoToque.Vazio;
    }

    /// <summary>O contorno convexo (cadeia monótona), no sentido dos ponteiros na tela.</summary>
    public static IReadOnlyList<Ponto2> Envoltoria(IReadOnlyList<Ponto2> pontos)
    {
        var p = pontos.Distinct().OrderBy(a => a.X).ThenBy(a => a.Y).ToArray();
        if (p.Length < 3)
            return p;
        var h = new Ponto2[p.Length * 2];
        var k = 0;
        static double Cruz(Ponto2 o, Ponto2 a, Ponto2 b) => (a.X - o.X) * (b.Y - o.Y) - (a.Y - o.Y) * (b.X - o.X);
        foreach (var a in p)
        {
            while (k >= 2 && Cruz(h[k - 2], h[k - 1], a) <= 0)
                k--;
            h[k++] = a;
        }
        for (int i = p.Length - 2, baixo = k + 1; i >= 0; i--)
        {
            while (k >= baixo && Cruz(h[k - 2], h[k - 1], p[i]) <= 0)
                k--;
            h[k++] = p[i];
        }
        return h.Take(k - 1).ToArray();
    }

    /// <summary>O ponto está dentro do polígono convexo (ou na borda).</summary>
    public static bool Dentro(IReadOnlyList<Ponto2> poligono, Ponto2 ponto)
    {
        if (poligono.Count < 3)
            return false;
        var sinal = 0;
        for (var i = 0; i < poligono.Count; i++)
        {
            var a = poligono[i];
            var b = poligono[(i + 1) % poligono.Count];
            var cruz = (b.X - a.X) * (ponto.Y - a.Y) - (b.Y - a.Y) * (ponto.X - a.X);
            if (Math.Abs(cruz) < 1e-9)
                continue;
            var s = Math.Sign(cruz);
            if (sinal == 0)
                sinal = s;
            else if (s != sinal)
                return false;
        }
        return true;
    }
}
