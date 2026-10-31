using BgsControle.App.Recursos;
using BgsControle.Telas.Esquema;

namespace BgsControle.App.Controles;

/// <summary>
/// O esquema de ligação plano do Aplicando (DESIGN, setup-schematic; história 10d): pinta o
/// <see cref="EsquemaLigacao"/> (viewBox 230 × 215, escalado para caber e centrado). O PRO 2.4BT em
/// {colors.surface-1} com borda {colors.border-sheet}, as portas CH1 a CH4, os fios curvos e os equipamentos com o
/// nome. Cada canal tem um estado: aceso (porta, fio e borda em {colors.brand}, com brilho), gravado (luz fraca
/// {colors.channel-done}) ou ainda não ({colors.border-sheet}); a troca leva 0,3 s. Um equipamento com dois canais
/// fica aceso se qualquer um está aceso. Não é tocável.
/// </summary>
public sealed class EsquemaLigacaoView : GraphicsView, IDrawable
{
    public static readonly BindableProperty EsquemaProperty = BindableProperty.Create(
        nameof(Esquema), typeof(EsquemaLigacao), typeof(EsquemaLigacaoView), EsquemaLigacao.Nenhum,
        propertyChanged: (b, _, _) => ((EsquemaLigacaoView)b).Invalidate());

    public static readonly BindableProperty EstadosProperty = BindableProperty.Create(
        nameof(Estados), typeof(IReadOnlyList<EstadoCanal>), typeof(EsquemaLigacaoView), null,
        propertyChanged: (b, o, n) => ((EsquemaLigacaoView)b).Trocar(o as IReadOnlyList<EstadoCanal>));

    private static readonly EstadoCanal[] Apagados = [EstadoCanal.AindaNao, EstadoCanal.AindaNao, EstadoCanal.AindaNao, EstadoCanal.AindaNao];

    private IReadOnlyList<EstadoCanal> _antes = Apagados;
    private double _progresso = 1;

    public EsquemaLigacaoView()
    {
        Drawable = this;
        InputTransparent = true;
        BackgroundColor = Colors.Transparent;
    }

    public EsquemaLigacao Esquema { get => (EsquemaLigacao)GetValue(EsquemaProperty); set => SetValue(EsquemaProperty, value); }

    public IReadOnlyList<EstadoCanal>? Estados { get => (IReadOnlyList<EstadoCanal>?)GetValue(EstadosProperty); set => SetValue(EstadosProperty, value); }

    private void Trocar(IReadOnlyList<EstadoCanal>? antes)
    {
        _antes = antes ?? Apagados;
        this.AbortAnimation("troca");
        this.Animate("troca", v =>
        {
            _progresso = v;
            Invalidate();
        }, length: 300);
    }

    private static Color CorDe(EstadoCanal e) => e switch
    {
        EstadoCanal.Aceso => Cores.De("brand"),
        EstadoCanal.Gravado => Cores.De("channel-done"),
        _ => Cores.De("border-sheet"),
    };

    private static float BrilhoDe(EstadoCanal e) => e == EstadoCanal.Aceso ? 1f : 0f;

    private static EstadoCanal Junto(IReadOnlyList<EstadoCanal> estados, IEnumerable<int> canais)
    {
        var lista = canais.Where(c => c >= 0 && c < estados.Count).Select(c => estados[c]).ToArray();
        return lista.Contains(EstadoCanal.Aceso) ? EstadoCanal.Aceso
            : lista.Contains(EstadoCanal.Gravado) ? EstadoCanal.Gravado
            : EstadoCanal.AindaNao;
    }

    /// <summary>A cor e o brilho (0 a 1) no meio da troca de 0,3 s.</summary>
    private (Color Cor, float Brilho) Tom(IEnumerable<int> canais)
    {
        var agora = Estados ?? Apagados;
        var lista = canais.ToArray();
        var de = Junto(_antes, lista);
        var para = Junto(agora, lista);
        var t = (float)_progresso;
        var c0 = CorDe(de);
        var c1 = CorDe(para);
        var cor = new Color(c0.Red + (c1.Red - c0.Red) * t, c0.Green + (c1.Green - c0.Green) * t, c0.Blue + (c1.Blue - c0.Blue) * t);
        return (cor, BrilhoDe(de) + (BrilhoDe(para) - BrilhoDe(de)) * t);
    }

    public void Draw(ICanvas canvas, RectF r)
    {
        var e = Esquema;
        var escala = (float)Math.Min(r.Width / EsquemaLigacao.Largura, r.Height / EsquemaLigacao.Altura);
        if (escala <= 0)
            return;
        var dx = r.Left + (r.Width - (float)EsquemaLigacao.Largura * escala) / 2;
        var dy = r.Top + (r.Height - (float)EsquemaLigacao.Altura * escala) / 2;
        float X(double v) => dx + (float)v * escala;
        float Y(double v) => dy + (float)v * escala;
        RectF Caixa(double x, double y, double l, double a) => new(X(x), Y(y), (float)l * escala, (float)a * escala);

        // O processador e o rótulo.
        canvas.FillColor = Cores.De("surface-1");
        canvas.StrokeColor = Cores.De("border-sheet");
        canvas.StrokeSize = 1 * escala;
        var proc = Caixa(EsquemaLigacao.ProcessadorX, EsquemaLigacao.ProcessadorY, EsquemaLigacao.ProcessadorLargura, EsquemaLigacao.ProcessadorAltura);
        canvas.FillRoundedRectangle(proc, 8 * escala);
        canvas.DrawRoundedRectangle(proc, 8 * escala);
        canvas.FontColor = Cores.De("text-secondary");
        canvas.FontSize = 8.5f * escala;
        canvas.DrawString(EsquemaLigacao.NomeProcessador, Caixa(EsquemaLigacao.ProcessadorX, EsquemaLigacao.RotuloProcessadorY - 8, EsquemaLigacao.ProcessadorLargura, 14),
            HorizontalAlignment.Center, VerticalAlignment.Center);

        // Os fios, por baixo das portas e dos equipamentos.
        canvas.StrokeSize = 1.4f * escala;
        foreach (var f in e.Fios)
        {
            var (cor, brilho) = Tom([f.Canal]);
            var caminho = new PathF(X(EsquemaLigacao.FioDeX), Y(f.DeY));
            caminho.CurveTo(X(105), Y(f.DeY), X(110), Y(f.AteY), X(EsquemaLigacao.EquipamentoX), Y(f.AteY));
            Brilhar(canvas, cor, brilho, 3 * escala);
            canvas.StrokeColor = cor;
            canvas.DrawPath(caminho);
        }
        canvas.SetShadow(SizeF.Zero, 0, null);

        // As portas e os rótulos CHn.
        canvas.FontSize = 9 * escala;
        foreach (var p in e.Portas)
        {
            var (cor, brilho) = Tom([p.Canal]);
            var porta = Caixa(EsquemaLigacao.PortaX, p.Y - EsquemaLigacao.PortaAltura / 2, EsquemaLigacao.PortaLargura, EsquemaLigacao.PortaAltura);
            Brilhar(canvas, cor, brilho, 4 * escala);
            canvas.FillColor = brilho > 0.5f || (Estados?[p.Canal] ?? EstadoCanal.AindaNao) == EstadoCanal.Gravado ? cor : Cores.De("surface-2");
            canvas.StrokeColor = cor;
            canvas.StrokeSize = 1 * escala;
            canvas.FillRoundedRectangle(porta, 3 * escala);
            canvas.DrawRoundedRectangle(porta, 3 * escala);
            canvas.SetShadow(SizeF.Zero, 0, null);
            canvas.FontColor = Cores.De("text-chip");
            canvas.DrawString(p.Rotulo, Caixa(EsquemaLigacao.RotuloPortaX - 20, p.Y - 7, 40, 14), HorizontalAlignment.Center, VerticalAlignment.Center);
        }

        // Os equipamentos com o nome.
        foreach (var q in e.Equipamentos)
        {
            var (cor, brilho) = Tom(q.Canais);
            var caixa = Caixa(EsquemaLigacao.EquipamentoX, q.Y, EsquemaLigacao.LarguraEquipamento, EsquemaLigacao.AlturaEquipamento);
            canvas.FillColor = Cores.De("surface-1");
            canvas.FillRoundedRectangle(caixa, 6 * escala);
            Brilhar(canvas, cor, brilho, 5 * escala);
            canvas.StrokeColor = cor;
            canvas.StrokeSize = 1 * escala;
            canvas.DrawRoundedRectangle(caixa, 6 * escala);
            canvas.SetShadow(SizeF.Zero, 0, null);
            canvas.FontColor = Cores.De("text-chip");
            canvas.FontSize = 9 * escala;
            canvas.DrawString(q.Nome, caixa, HorizontalAlignment.Center, VerticalAlignment.Center);
        }
    }

    private static void Brilhar(ICanvas canvas, Color cor, float brilho, float raio)
    {
        if (brilho > 0.01f)
            canvas.SetShadow(SizeF.Zero, raio, cor.WithAlpha(0.85f * brilho));
        else
            canvas.SetShadow(SizeF.Zero, 0, null);
    }
}
