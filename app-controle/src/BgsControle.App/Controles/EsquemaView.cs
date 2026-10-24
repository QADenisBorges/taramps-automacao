using System.ComponentModel;
using BgsControle.App.Recursos;
using BgsControle.Telas;
using BgsControle.Telas.Esquema;

namespace BgsControle.App.Controles;

/// <summary>
/// O esquema do Setup (DESIGN, esquema-do-setup; história 10b): pinta a cena projetada do
/// <see cref="EsquemaViewModel"/> no fundo {colors.palco} com a grade isométrica fraca em {colors.sup-3};
/// cada peça em três tons (topo {colors.sup-2}, lados {colors.sup-1} e {colors.registro-fundo}) com a sombra
/// elíptica a 40 %; os cabos em traço de 2 px na cor do canal, com o ponto do Y; os bridges como a chave que
/// junta as duas saídas; os rótulos com o nome em {colors.texto} e "genérico" em {colors.apagado}. A cor do
/// canal fica só no cabo, na borda da entrada e no rótulo CHn. A peça tocada ganha o contorno branco de
/// 2 px. Arrastar, pinçar e tocar vão ao ViewModel; as posições, as formas e o toque são dele.
/// </summary>
public sealed class EsquemaView : GraphicsView, IDrawable
{
    public const double AlturaMinima = 280;

    private readonly EsquemaViewModel _vm;
    private double _ultimoX;
    private bool? _horizontal;
    private bool _pincando;

    public EsquemaView(EsquemaViewModel vm)
    {
        _vm = vm;
        Drawable = this;
        MinimumHeightRequest = AlturaMinima;
        HeightRequest = AlturaMinima;
        BackgroundColor = Cores.De("palco");

        var pan = new PanGestureRecognizer();
        pan.PanUpdated += AoArrastar;
        GestureRecognizers.Add(pan);
        var pinca = new PinchGestureRecognizer();
        pinca.PinchUpdated += (_, e) =>
        {
            // Em volta dos dedos (a origem vem de 0 a 1 na área).
            _pincando = e.Status is GestureStatus.Started or GestureStatus.Running;
            if (e.Status == GestureStatus.Running)
                _vm.Pincar(e.Scale, e.ScaleOrigin.X * Width, e.ScaleOrigin.Y * Height);
        };
        GestureRecognizers.Add(pinca);
        var toque = new TapGestureRecognizer();
        toque.Tapped += (_, e) =>
        {
            if (e.GetPosition(this) is { } p)
                _vm.Tocar(p.X, p.Y);
        };
        GestureRecognizers.Add(toque);

        _vm.PropertyChanged += AoMudar;
        SizeChanged += (_, _) => AjustarAltura();
    }

    private void AoMudar(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(EsquemaViewModel.Cena))
            AjustarAltura();
        if (e.PropertyName == nameof(EsquemaViewModel.Versao))
            Invalidate();
    }

    /// <summary>280 no mínimo; um Setup com muitas fileiras ganha altura (a página rola).</summary>
    private void AjustarAltura()
    {
        if (Width <= 0)
            return;
        var altura = _vm.AlturaDesejada(Width);
        if (Math.Abs(altura - HeightRequest) > 1)
            HeightRequest = altura;
    }

    private void AoArrastar(object? sender, PanUpdatedEventArgs e)
    {
        // Só o arrasto mais horizontal que vertical gira, e nunca durante o pinçar; o vertical fica para a
        // rolagem da página. Soltar não volta o ângulo.
        switch (e.StatusType)
        {
            case GestureStatus.Started:
                _ultimoX = 0;
                _horizontal = null;
                break;
            case GestureStatus.Running:
                if (_pincando)
                {
                    _horizontal = false;
                    return;
                }
                if (_horizontal is null && Math.Max(Math.Abs(e.TotalX), Math.Abs(e.TotalY)) >= 8)
                    _horizontal = Math.Abs(e.TotalX) > Math.Abs(e.TotalY);
                if (_horizontal != true)
                    return;
                var dx = e.TotalX - _ultimoX;
                _ultimoX = e.TotalX;
                _vm.Arrastar(dx);
                break;
            default:
                _horizontal = null;
                break;
        }
    }

    // ------------------------------------------------------------------ pintura

    private static PointF P(Ponto2 p) => new((float)p.X, (float)p.Y);

    public void Draw(ICanvas canvas, RectF r)
    {
        // O tamanho da view (o r pode ser só o pedaço sujo).
        _vm.Tamanho(Width > 0 ? Width : r.Width, Height > 0 ? Height : r.Height);
        var pr = _vm.Projecao;
        var cena = _vm.Cena;

        canvas.FillColor = Cores.De("palco");
        canvas.FillRectangle(r);
        Grade(canvas, pr, cena);

        var ordem = pr.OrdemDePintura();
        foreach (var peca in ordem)
            Sombra(canvas, pr, peca, _vm.Opacidade(peca));

        foreach (var cabo in cena.Cabos)
            Cabo(canvas, pr, cabo);
        foreach (var y in cena.Juncoes)
        {
            canvas.Alpha = (float)_vm.Opacidade(y);
            canvas.FillColor = CorCanal(y.Canal);
            var c = pr.Projetar(y.Posicao);
            canvas.FillCircle((float)c.X, (float)c.Y, 3.5f);
        }

        foreach (var peca in ordem)
        {
            var alpha = (float)_vm.Opacidade(peca);
            canvas.Alpha = alpha;
            Peca(canvas, pr, cena, peca, alpha);
            if (peca.Id == _vm.Selecionada)
                Contorno(canvas, pr.Silhueta(peca), Cores.De("texto"), 2);
        }

        foreach (var peca in ordem)
            Rotulo(canvas, pr, peca);

        canvas.Alpha = 1;
        if (_vm.TextoVazio is { } vazio)
        {
            var fundo = pr.Silhueta(cena.Processador).Max(p => p.Y);
            canvas.Font = Microsoft.Maui.Graphics.Font.Default;
            canvas.FontSize = 13;
            canvas.FontColor = Cores.De("apoio");
            canvas.DrawString(vazio, 16, (float)fundo + 34, r.Width - 32, 44, HorizontalAlignment.Center, VerticalAlignment.Top);
        }
    }

    private static Color CorCanal(int? canal) => canal is >= 0 and <= 3 ? Cores.DoCanal(canal) : Cores.De("apagado");

    /// <summary>A grade do chão ao longo dos eixos das caixas (o isométrico), a cada 40 unidades.</summary>
    private static void Grade(ICanvas canvas, ProjecaoIsometrica pr, CenaSetup cena)
    {
        const double passo = 40, folga = 80;
        var r2 = Math.Sqrt(0.5);
        double[] us = [cena.MinU, cena.MaxU];
        double[] vs = [cena.MinV, cena.MaxV];
        var cantos = us.SelectMany(u => vs.Select(v => (A: (u - v) * r2, B: (u + v) * r2))).ToArray();
        var (minA, maxA) = (Math.Floor((cantos.Min(c => c.A) - folga) / passo) * passo, cantos.Max(c => c.A) + folga);
        var (minB, maxB) = (Math.Floor((cantos.Min(c => c.B) - folga) / passo) * passo, cantos.Max(c => c.B) + folga);
        Ponto3 Chao(double a, double b) => new((a + b) * r2, (b - a) * r2);

        canvas.Alpha = 0.7f;
        canvas.StrokeColor = Cores.De("sup-3");
        canvas.StrokeSize = 1;
        for (var a = minA; a <= maxA; a += passo)
            canvas.DrawLine(P(pr.Projetar(Chao(a, minB))), P(pr.Projetar(Chao(a, maxB))));
        for (var b = minB; b <= maxB; b += passo)
            canvas.DrawLine(P(pr.Projetar(Chao(minA, b))), P(pr.Projetar(Chao(maxA, b))));
        canvas.Alpha = 1;
    }

    /// <summary>A sombra elíptica a 40 %, no chão, embaixo da peça.</summary>
    private static void Sombra(ICanvas canvas, ProjecaoIsometrica pr, PecaCena peca, double opacidade)
    {
        var raio = peca.Cilindro ? peca.Raio * 1.15 : Math.Sqrt(peca.MeiaA * peca.MeiaA + peca.MeiaB * peca.MeiaB) * 0.95;
        var (rx, ry) = pr.Elipse(raio);
        var c = pr.Projetar(peca.Base + new Ponto3(2, 4));
        canvas.Alpha = (float)opacidade;
        // Em sup-1 a 40 %: no fundo palco (preto) uma sombra preta não apareceria.
        canvas.FillColor = Cores.De("sup-1").WithAlpha(0.4f);
        canvas.FillEllipse((float)(c.X - rx), (float)(c.Y - ry), (float)(2 * rx), (float)(2 * ry));
        canvas.Alpha = 1;
    }

    private void Cabo(ICanvas canvas, ProjecaoIsometrica pr, CaboCena cabo)
    {
        canvas.Alpha = (float)_vm.Opacidade(cabo);
        canvas.StrokeColor = CorCanal(cabo.Canal);
        canvas.StrokeSize = 2;
        canvas.StrokeLineCap = LineCap.Round;
        canvas.StrokeLineJoin = LineJoin.Round;
        var path = new PathF();
        path.MoveTo(P(pr.Projetar(cabo.Pontos[0])));
        foreach (var p in cabo.Pontos.Skip(1))
            path.LineTo(P(pr.Projetar(p)));
        canvas.DrawPath(path);
    }

    private static void Contorno(ICanvas canvas, IReadOnlyList<Ponto2> pontos, Color cor, float largura)
    {
        if (pontos.Count < 3)
            return;
        canvas.StrokeColor = cor;
        canvas.StrokeSize = largura;
        canvas.StrokeLineJoin = LineJoin.Round;
        canvas.DrawPath(Poligono(pontos));
    }

    private static PathF Poligono(IEnumerable<Ponto2> pontos)
    {
        var path = new PathF();
        var primeiro = true;
        foreach (var p in pontos)
        {
            if (primeiro)
                path.MoveTo(P(p));
            else
                path.LineTo(P(p));
            primeiro = false;
        }
        path.Close();
        return path;
    }

    // ------------------------------------------------------------------ as peças

    private void Peca(ICanvas canvas, ProjecaoIsometrica pr, CenaSetup cena, PecaCena p, float alpha)
    {
        switch (p.Forma)
        {
            case FormaPeca.Domo:
                Cilindro(canvas, pr, p.Base, p.Raio, 0, p.Raio, p.Altura);
                Elipse(canvas, pr, p.Base.NaAltura(p.Altura), p.Raio, "sup-2");
                Domo(canvas, pr, p.Base.NaAltura(p.BaseDoDomo), p.RaioDomo);
                break;
            case FormaPeca.Corneta:
                Cilindro(canvas, pr, p.Base, PecaCena.RaioCorpoCorneta, 0, PecaCena.RaioCorpoCorneta, p.Altura / 2);
                Cilindro(canvas, pr, p.Base, PecaCena.RaioCorpoCorneta - 1, p.Altura / 2, p.Raio, p.Altura);
                Elipse(canvas, pr, p.Base.NaAltura(p.Altura), p.Raio, "sup-2");
                Elipse(canvas, pr, p.Base.NaAltura(p.Altura - 2), p.Raio * 0.75, "registro-fundo");
                Elipse(canvas, pr, p.Base.NaAltura(p.Altura - 6), p.Raio * 0.25, "palco");
                break;
            case FormaPeca.Cone:
            case FormaPeca.Coaxial:
                Cilindro(canvas, pr, p.Base, p.Raio, 0, p.Raio, p.Altura);
                Cone(canvas, pr, p.Base.NaAltura(p.Altura), p.Raio, moldura: true);
                if (p.Forma == FormaPeca.Coaxial)
                    Domo(canvas, pr, p.Base.NaAltura(p.BaseDoDomo), p.RaioDomo);
                break;
            case FormaPeca.CaixaComCone:
                Caixa(canvas, pr, p);
                Cone(canvas, pr, p.Base.NaAltura(p.Altura), p.Raio, moldura: false);
                break;
            case FormaPeca.CaixaComVias:
                Caixa(canvas, pr, p);
                foreach (var via in p.Vias)
                    Cone(canvas, pr, via.Centro, via.Raio, moldura: false);
                break;
            case FormaPeca.BlocoComCanais:
                Caixa(canvas, pr, p);
                Canais(canvas, pr, cena, p, alpha);
                break;
            case FormaPeca.BlocoBaixo:
                Caixa(canvas, pr, p);
                if (p.TextoBloco is { } texto)
                {
                    var c = pr.Projetar(p.Base.NaAltura(p.Altura));
                    canvas.Font = Microsoft.Maui.Graphics.Font.Default;
                    canvas.FontSize = 9;
                    canvas.FontColor = Cores.De("apoio");
                    canvas.DrawString(texto.Length > 18 ? texto[..17] + "…" : texto, (float)c.X, (float)c.Y + 3, HorizontalAlignment.Center);
                }
                break;
            case FormaPeca.Processador:
                Caixa(canvas, pr, p);
                break;
            default:
                Caixa(canvas, pr, p);
                break;
        }

        // As portas da peça: CHn do processador e as entradas com a borda na cor do canal que chega.
        foreach (var porta in cena.PortasDe(p.Id))
        {
            if (!pr.PortaAparece(p, porta))
                continue;
            var c = pr.Projetar(porta.Posicao);
            canvas.Alpha = alpha * (float)_vm.Opacidade(porta);
            var raio = (float)Math.Max(2.5, 3.2 * pr.Escala);
            canvas.FillColor = Cores.De("registro-fundo");
            canvas.FillCircle((float)c.X, (float)c.Y, raio);
            canvas.StrokeSize = porta.Tipo == TipoPorta.Saida ? 1 : 1.5f;
            canvas.StrokeColor = porta.Tipo == TipoPorta.Saida ? Cores.De("apoio") : porta.Canal is { } canal ? Cores.DoCanal(canal) : Cores.De("sup-2");
            canvas.DrawCircle((float)c.X, (float)c.Y, raio);
            if (porta.Tipo == TipoPorta.Processador)
            {
                var rotulo = pr.Projetar((porta.Posicao - CenaSetup.EixoB * 10).NaAltura(p.Altura));
                canvas.Font = Microsoft.Maui.Graphics.Font.DefaultBold;
                canvas.FontSize = 9;
                canvas.FontColor = Cores.DoCanal(porta.Numero);
                canvas.DrawString($"CH{porta.Numero + 1}", (float)rotulo.X, (float)rotulo.Y + 3, HorizontalAlignment.Center);
            }
        }
        canvas.Alpha = alpha;
    }

    /// <summary>Uma caixa ou um bloco: as faces que aparecem (a da esquerda em sup-1, a da direita em registro-fundo) e o topo em sup-2.</summary>
    private static void Caixa(ICanvas canvas, ProjecaoIsometrica pr, PecaCena p)
    {
        var baixo = p.Cantos(0);
        var cima = p.Cantos(p.Altura);
        canvas.StrokeColor = Cores.De("sup-3");
        canvas.StrokeSize = 1;
        canvas.StrokeLineJoin = LineJoin.Round;
        for (var i = 0; i < 4; i++)
        {
            var j = (i + 1) % 4;
            var meio = (baixo[i] + baixo[j]) * 0.5 - p.Base;
            if (!pr.Aparece(meio))
                continue;
            canvas.FillColor = pr.Direcao(meio).Direita < 0 ? Cores.De("sup-1") : Cores.De("registro-fundo");
            var face = Poligono([pr.Projetar(baixo[i]), pr.Projetar(baixo[j]), pr.Projetar(cima[j]), pr.Projetar(cima[i])]);
            canvas.FillPath(face);
            canvas.DrawPath(face);
        }
        var topo = Poligono(cima.Select(pr.Projetar));
        canvas.FillColor = Cores.De("sup-2");
        canvas.FillPath(topo);
        canvas.DrawPath(topo);
    }

    /// <summary>
    /// Um cilindro (ou tronco de cone) de pé: o lado visível com o degradê sup-1 → registro-fundo, sem o
    /// topo (quem chama pinta o topo por cima).
    /// </summary>
    private static void Cilindro(ICanvas canvas, ProjecaoIsometrica pr, Ponto3 base_, double raioBaixo, double hBaixo, double raioCima, double hCima)
    {
        var b = pr.Projetar(base_.NaAltura(hBaixo));
        var t = pr.Projetar(base_.NaAltura(hCima));
        var (bx, by) = pr.Elipse(raioBaixo);
        var (tx, _) = pr.Elipse(raioCima);
        var path = new PathF();
        path.MoveTo((float)(b.X - bx), (float)b.Y);
        const int passos = 16;
        for (var i = 1; i <= passos; i++)
        {
            var a = Math.PI * i / passos;
            path.LineTo((float)(b.X - bx * Math.Cos(a)), (float)(b.Y + by * Math.Sin(a)));
        }
        path.LineTo((float)(t.X + tx), (float)t.Y);
        path.LineTo((float)(t.X - tx), (float)t.Y);
        path.Close();
        var caixa = new RectF((float)Math.Min(b.X - bx, t.X - tx), (float)t.Y, (float)(2 * Math.Max(bx, tx)), (float)Math.Max(1, b.Y + by - t.Y));
        canvas.SetFillPaint(new LinearGradientPaint(
            [new PaintGradientStop(0, Cores.De("sup-1")), new PaintGradientStop(1, Cores.De("registro-fundo"))],
            new Point(0, 0.5), new Point(1, 0.5)), caixa);
        canvas.FillPath(path);
        canvas.StrokeColor = Cores.De("sup-3");
        canvas.StrokeSize = 1;
        canvas.DrawPath(path);
    }

    private static void Elipse(ICanvas canvas, ProjecaoIsometrica pr, Ponto3 centro, double raio, string cor)
    {
        var c = pr.Projetar(centro);
        var (rx, ry) = pr.Elipse(raio);
        canvas.FillColor = Cores.De(cor);
        canvas.FillEllipse((float)(c.X - rx), (float)(c.Y - ry), (float)(2 * rx), (float)(2 * ry));
    }

    /// <summary>Um cone visto de cima: a moldura (sup-2), o cone afundado (registro-fundo) e a calota (sup-1).</summary>
    private static void Cone(ICanvas canvas, ProjecaoIsometrica pr, Ponto3 topo, double raio, bool moldura)
    {
        if (moldura)
            Elipse(canvas, pr, topo, raio, "sup-2");
        Elipse(canvas, pr, topo.NaAltura(topo.H - raio * 0.05), raio * (moldura ? 0.8 : 1), "registro-fundo");
        var c = pr.Projetar(topo);
        var (rx, ry) = pr.Elipse(raio * (moldura ? 0.8 : 1));
        canvas.StrokeColor = Cores.De("sup-3");
        canvas.StrokeSize = 1;
        canvas.DrawEllipse((float)(c.X - rx), (float)(c.Y - ry), (float)(2 * rx), (float)(2 * ry));
        Elipse(canvas, pr, topo.NaAltura(topo.H - raio * 0.25), raio * 0.28, "sup-1");
    }

    /// <summary>Um domo: a meia esfera sobe do topo (registro-fundo, com a borda sup-2).</summary>
    private static void Domo(ICanvas canvas, ProjecaoIsometrica pr, Ponto3 topo, double raio)
    {
        var c = pr.Projetar(topo);
        var rx = raio * pr.Escala;
        var subida = raio * ProjecaoIsometrica.Altura * pr.Escala;
        var ry = raio * ProjecaoIsometrica.Chao * pr.Escala;
        var path = new PathF();
        path.MoveTo((float)(c.X - rx), (float)c.Y);
        const int passos = 16;
        for (var i = 1; i <= passos; i++)
        {
            var a = Math.PI * i / passos;
            path.LineTo((float)(c.X - rx * Math.Cos(a)), (float)(c.Y - subida * Math.Sin(a)));
        }
        for (var i = 1; i <= passos; i++)
        {
            var a = Math.PI * i / passos;
            path.LineTo((float)(c.X + rx * Math.Cos(a)), (float)(c.Y + ry * Math.Sin(a)));
        }
        path.Close();
        canvas.FillColor = Cores.De("sup-1");
        canvas.FillPath(path);
        canvas.StrokeColor = Cores.De("sup-2");
        canvas.StrokeSize = 1;
        canvas.DrawPath(path);
    }

    /// <summary>O módulo: os números dos canais no topo, junto das saídas, e a chave de cada bridge.</summary>
    private void Canais(ICanvas canvas, ProjecaoIsometrica pr, CenaSetup cena, PecaCena p, float alpha)
    {
        canvas.Font = Microsoft.Maui.Graphics.Font.Default;
        canvas.FontSize = 9;
        canvas.FontColor = Cores.De("apoio");
        foreach (var saida in cena.PortasDe(p.Id).Where(x => x.Tipo == TipoPorta.Saida))
        {
            var c = pr.Projetar((saida.Posicao - CenaSetup.EixoB * 7).NaAltura(p.Altura));
            canvas.DrawString(saida.Numero.ToString(System.Globalization.CultureInfo.InvariantCulture), (float)c.X, (float)c.Y + 3, HorizontalAlignment.Center);
        }
        foreach (var bridge in cena.Bridges.Where(b => b.IdModulo == p.Id))
        {
            canvas.Alpha = alpha * (float)_vm.Opacidade(bridge);
            canvas.StrokeColor = Cores.De("texto");
            canvas.StrokeSize = 1.5f;
            canvas.StrokeLineJoin = LineJoin.Round;
            var path = new PathF();
            path.MoveTo(P(pr.Projetar(bridge.Chave[0])));
            foreach (var ponto in bridge.Chave.Skip(1))
                path.LineTo(P(pr.Projetar(ponto)));
            canvas.DrawPath(path);
            var s = pr.Projetar(bridge.Saida);
            canvas.FillColor = Cores.De("texto");
            canvas.FillCircle((float)s.X, (float)s.Y, 2.5f);
        }
        canvas.Alpha = alpha;
    }

    /// <summary>O rótulo embaixo da peça: o nome em texto e, embaixo, "genérico" ou "sem ligação" em apagado.</summary>
    private void Rotulo(ICanvas canvas, ProjecaoIsometrica pr, PecaCena p)
    {
        var silhueta = pr.Silhueta(p);
        var base_ = pr.Projetar(p.Base);
        // Uma peça fora da área (aproximada ou girada) fica sem rótulo, para ele não ir parar longe dela.
        if (silhueta.Count == 0 || base_.X < 0 || base_.X > pr.Largura || base_.Y < 0 || base_.Y > pr.AlturaArea)
            return;
        // O rótulo fica dentro da área (a largura do texto é estimada pelo número de letras).
        var meia = Math.Max(p.Nome.Length * 11, (p.Detalhe?.Length ?? 0) * 10) * 0.3;
        var x = (float)Math.Clamp(base_.X, meia + 2, Math.Max(meia + 2, pr.Largura - meia - 2));
        var y = (float)silhueta.Max(s => s.Y);
        canvas.Alpha = (float)_vm.Opacidade(p);
        canvas.Font = Microsoft.Maui.Graphics.Font.DefaultBold;
        canvas.FontSize = 11;
        canvas.FontColor = Cores.De("texto");
        canvas.DrawString(p.Nome, x, y + 13, HorizontalAlignment.Center);
        if (p.Detalhe is { } detalhe)
        {
            canvas.Font = Microsoft.Maui.Graphics.Font.Default;
            canvas.FontSize = 10;
            canvas.FontColor = Cores.De("apagado");
            canvas.DrawString(detalhe, x, y + 25, HorizontalAlignment.Center);
        }
        canvas.Alpha = 1;
    }
}
