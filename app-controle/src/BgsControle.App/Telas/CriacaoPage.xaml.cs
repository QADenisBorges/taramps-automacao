using System.ComponentModel;
using BgsControle.App.Controles;
using BgsControle.App.Plataforma;
using BgsControle.App.Recursos;
using BgsControle.Telas;

namespace BgsControle.App.Telas;

/// <summary>
/// As animações da criação da memória (DESIGN, Movimento; EXPERIENCE, "Criação da memória (30/10)"). A ordem das
/// fases é do <see cref="CriacaoMemoriaViewModel"/>; aqui só os tempos do UX e os sinais de volta:
/// <list type="bullet">
/// <item>a pergunta entra em 0,32 s, de 28 px (da direita ao avançar, da esquerda com ‹); a resposta acende 0,18 s
/// antes de avançar;</item>
/// <item>o palco: 0,3 s parado; cada ajuste com o Foco (0,8 s; o valor 0,9 s), a barrinha (0,6 s), o risco, a seta e
/// o valor novo, 1,3 s parado (nunca menos; a folha de risco pausa), a saída (0,65 s) e 0,12 s até o próximo; depois
/// <see cref="CriacaoMemoriaViewModel.PalcoMostrou"/>;</item>
/// <item>a passagem Pião e cambalhota em 1,6 s, e <see cref="CriacaoMemoriaViewModel.PassagemTerminou"/>;</item>
/// <item>o Aplicando: 0,7 s por canal, 1 s parado e 0,3 s de saída, e
/// <see cref="CriacaoMemoriaViewModel.AplicandoTerminou"/>. Sem a animação (ou com o movimento reduzido), o Aplicando
/// entra num fade de 0,3 s e os canais seguem os ajustes relidos;</item>
/// <item>o resumo: os itens sobem um depois do outro, 0,07 s entre eles.</item>
/// </list>
/// O desfoque usa o RenderEffect do Android 12 ou mais novo; abaixo, a frase só anda. O Voltar do Android vai ao
/// <see cref="CriacaoMemoriaViewModel.Voltar"/>.
/// </summary>
public partial class CriacaoPage : ContentPage
{
    private const double LarguraBarra = 54;

    private readonly CriacaoMemoriaViewModel _vm;
    private FaseCriacao _faseVista = FaseCriacao.Fechada;
    private int _versao;
    private int? _perguntaVista;
    private bool _tocando;

    public CriacaoPage(CriacaoMemoriaViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        CampoNome.Keyboard = Keyboard.Create(KeyboardFlags.CapitalizeCharacter);
        BindingContext = vm;
        vm.PropertyChanged += AoMudar;
        SizeChanged += (_, _) => Posicionar();
        Frase.SizeChanged += (_, _) => MoverHorizonte(animado: true);
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        var reduzido = Movimento.Reduzido();
        _vm.MovimentoReduzido = reduzido;
        AnelChao.Lento = reduzido;
        AnelAplicando.Lento = reduzido;
        // O OnAppearing volta quando uma camada de cima (a folha de risco, o REAPLICANDO, o alarme) sai: a fase que
        // já anda não recomeça; só sincroniza se a tela ficou para trás.
        AplicarFase(_vm.Fase);
        // A entrada da pergunta pode ter rodado com a página fora da tela: ela fica no lugar final.
        if (!this.AnimationIsRunning("pergunta"))
        {
            BlocoPergunta.Opacity = 1;
            BlocoPergunta.TranslationX = 0;
        }
    }

    protected override bool OnBackButtonPressed() => _vm.Voltar() || base.OnBackButtonPressed();

    private void AoMudar(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(CriacaoMemoriaViewModel.Fase):
                AplicarFase(_vm.Fase);
                break;
            case nameof(CriacaoMemoriaViewModel.NumeroPergunta) or nameof(CriacaoMemoriaViewModel.Opcoes):
                if (_vm.Fase == FaseCriacao.Perguntas && _vm.NumeroPergunta != _perguntaVista)
                {
                    _perguntaVista = _vm.NumeroPergunta;
                    if (_perguntaVista is not null)
                        _ = EntrarPerguntaAsync(_vm.Voltando);
                }
                break;
            case nameof(CriacaoMemoriaViewModel.FolhaCancelarAberta):
                _ = MostrarFolhaCancelarAsync(_vm.FolhaCancelarAberta);
                break;
            case nameof(CriacaoMemoriaViewModel.CampoOutroAberto):
                if (_vm.CampoOutroAberto)
                    Dispatcher.Dispatch(() => CampoOutro.Focus());
                break;
        }
    }

    // ------------------------------------------------------------------ as fases

    private void AplicarFase(FaseCriacao fase)
    {
        if (fase == _faseVista)
            return;
        var antes = _faseVista;
        _faseVista = fase;
        var v = ++_versao;
        switch (fase)
        {
            case FaseCriacao.Fechada:
                Palco.IsVisible = false;
                Aplicando.IsVisible = false;
                _perguntaVista = null;
                break;
            case FaseCriacao.Perguntas or FaseCriacao.ComoFicou:
                Palco.IsVisible = false;
                Aplicando.IsVisible = false;
                _perguntaVista = _vm.NumeroPergunta;
                _ = EntrarPerguntaAsync(voltando: false);
                break;
            case FaseCriacao.Palco:
                Aplicando.IsVisible = false;
                _ = RodarPalcoAsync(v);
                break;
            case FaseCriacao.Passagem:
                _ = RodarPassagemAsync(v);
                break;
            case FaseCriacao.Aplicando:
                if (!_vm.ComAnimacao)
                {
                    _ = EntrarAplicandoAsync();
                }
                else
                {
                    // Vindo da passagem, o Aplicando já está no lugar; senão (a tela ficou para trás), monta já.
                    if (antes != FaseCriacao.Passagem)
                    {
                        PrepararAplicando();
                        Aplicando.Opacity = 1;
                    }
                    _ = RodarAplicandoAsync(v);
                }
                break;
            case FaseCriacao.Resumo:
                _ = EntrarResumoAsync(v, antes);
                break;
            case FaseCriacao.Salvar:
                Palco.IsVisible = false;
                Aplicando.IsVisible = false;
                break;
        }
    }

    private async Task EntrarPerguntaAsync(bool voltando)
    {
        this.AbortAnimation("pergunta");
        if (_vm.MovimentoReduzido)
        {
            BlocoPergunta.TranslationX = 0;
            BlocoPergunta.Opacity = 1;
            return;
        }
        var de = voltando ? -28 : 28;
        BlocoPergunta.TranslationX = de;
        BlocoPergunta.Opacity = 0;
        await Animar("pergunta", t =>
        {
            BlocoPergunta.TranslationX = de * (1 - t);
            BlocoPergunta.Opacity = t;
        }, 320, Curvas.Entrada);
    }

    /// <summary>A resposta acende (0,18 s) e então responde e avança. Um toque de cada vez.</summary>
    private async void AoTocarResposta(object? sender, TappedEventArgs e)
    {
        if (_tocando || (sender as BindableObject)?.BindingContext is not OpcaoCriacao opcao)
            return;
        _tocando = true;
        try
        {
            opcao.Acender();
            await Task.Delay(180);
            await opcao.Tocar.ExecutarAsync(); // se não passou, a opção apaga sozinha
        }
        finally
        {
            _tocando = false;
        }
    }

    // ------------------------------------------------------------------ o palco

    private async Task RodarPalcoAsync(int v)
    {
        Palco.IsVisible = true;
        Palco.Opacity = 1;
        AnelChao.Opacity = 0.35;
        Frase.Opacity = 0;
        Posicionar();
        await Task.Delay(300); // o palco parado antes da 1ª frase
        while (v == _versao && _vm.Fase == FaseCriacao.Palco)
        {
            if (_vm.AjusteDoPalco is not { } ajuste)
            {
                await Task.Delay(60); // o anel gira e o palco espera o próximo ajuste
                continue;
            }
            await MostrarAjusteAsync(ajuste, v);
            if (v != _versao)
                return;
            await EsperarSemPausaAsync(v);
            if (v != _versao || !_vm.PalcoMostrou())
            {
                await Task.Delay(60);
                continue;
            }
            await Task.Delay(120); // o intervalo entre as frases
        }
    }

    private async Task MostrarAjusteAsync(AjustePalco a, int v)
    {
        if (!string.IsNullOrEmpty(a.Anuncio))
            SemanticScreenReader.Default.Announce(a.Anuncio);
        var texto = new FormattedString();
        if (a.Inicio.Length > 0)
            texto.Spans.Add(new Span { Text = a.Inicio + " " });
        texto.Spans.Add(new Span { Text = a.Chave, TextColor = Cores.De("brand") });
        TextoFrase.FormattedText = texto;
        Antes.Text = a.Antes;
        Antes.TextDecorations = TextDecorations.None;
        Novo.Text = a.Valor;
        Novo.Opacity = 0;
        Seta.Opacity = 0;
        NotaValor.Text = a.Nota;
        NotaValor.IsVisible = a.Nota is not null;
        NotaValor.Opacity = 0;
        Preenchimento.WidthRequest = LarguraBarra * a.PosicaoAntes;
        TextoFrase.TranslationX = -Width;
        LinhaValor.TranslationX = -Width;
        Frase.Opacity = 1;
        await Task.Yield(); // mede a frase antes de partir da borda

        // Foco: parte da borda esquerda, já visível e desfocada, e fica nítida ao parar no centro.
        var largura = Math.Max(Width, 1);
        var deFrase = -(largura + TextoFrase.Width) / 2;
        var deValor = -(largura + LinhaValor.Width) / 2;
        await Task.WhenAll(
            Focar("frase", TextoFrase, deFrase, 0, 12, 0, 800, Curvas.FocoEntrada),
            Focar("valor", LinhaValor, deValor, 0, 12, 0, 900, Curvas.FocoEntrada));
        if (v != _versao)
            return;

        // Parou: a barrinha anda, o antes é riscado, a seta entra e o valor novo sai do desfoque.
        var barraDe = LarguraBarra * a.PosicaoAntes;
        var barraAte = LarguraBarra * a.PosicaoValor;
        var barra = Animar("barra", t => Preenchimento.WidthRequest = barraDe + (barraAte - barraDe) * t, 600, Curvas.Suave);
        await Task.Delay(300);
        Antes.TextDecorations = TextDecorations.Strikethrough;
        var seta = Animar("seta", t =>
        {
            Seta.Opacity = t;
            Seta.TranslationX = -8 * (1 - t);
        }, 240, Easing.CubicOut);
        await Task.Delay(100);
        var novo = Animar("novo", t =>
        {
            Novo.Opacity = t;
            Novo.TranslationX = -8 * (1 - t);
            NotaValor.Opacity = t;
            Desfoque.Aplicar(Novo, 6 * (1 - t));
        }, 400, Easing.CubicOut);
        await Task.WhenAll(barra, seta, novo);
        if (v != _versao)
            return;

        // A parada nunca encurta; com a folha de risco por cima, espera a resposta.
        await Task.Delay(1300);
        await EsperarSemPausaAsync(v);
        if (v != _versao)
            return;

        // Foco de saída: do centro à borda direita, desfocando; a linha do valor sai logo depois.
        var paraFrase = (largura + TextoFrase.Width) / 2;
        var paraValor = (largura + LinhaValor.Width) / 2;
        var saiFrase = Focar("frase", TextoFrase, 0, paraFrase, 0, 12, 650, Curvas.FocoSaida);
        await Task.Delay(60);
        var saiNota = Animar("nota", t => NotaValor.Opacity = 1 - t, 620, Curvas.FocoSaida);
        await Task.WhenAll(saiFrase, Focar("valor", LinhaValor, 0, paraValor, 0, 12, 620, Curvas.FocoSaida), saiNota);
        Frase.Opacity = 0;
        Desfoque.Aplicar(TextoFrase, 0);
        Desfoque.Aplicar(LinhaValor, 0);
    }

    // ------------------------------------------------------------------ a passagem e o Aplicando

    private (Rect Grande, Rect Medio, Rect Rotulo, Rect Nome, Rect Esquema) Geometria()
    {
        var w = Math.Max(Width, 1);
        var h = Math.Max(Height, 1);
        double grande = w * 0.56, medio = w * 0.3, alturaRotulo = 21, alturaNome = 25, espaco = 14;
        var topo = h * 0.9 - (alturaRotulo + espaco + medio + espaco + alturaNome);
        return (
            new Rect((w - grande) / 2, h * 0.58, grande, grande),
            new Rect((w - medio) / 2, topo + alturaRotulo + espaco, medio, medio),
            new Rect(0, topo, w, alturaRotulo + 4),
            new Rect(0, topo + alturaRotulo + espaco + medio + espaco, w, alturaNome + 4),
            new Rect(16, h * 0.12, w - 32, Math.Max(0, topo - h * 0.12 - 10)));
    }

    private static Rect Entre(Rect a, Rect b, double t) =>
        new(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t, a.Width + (b.Width - a.Width) * t, a.Height + (b.Height - a.Height) * t);

    /// <summary>Pião e cambalhota: o anel do chão abre nos dois anéis de neon, que dão uma volta ao contrário e se fecham no anel do Aplicando.</summary>
    private async Task RodarPassagemAsync(int v)
    {
        var g = Geometria();
        Aplicando.IsVisible = true;
        Aplicando.Opacity = 1;
        VeuAplicando.Opacity = 0;
        EsquemaVista.Opacity = 0;
        RotuloAplicando.Opacity = 0;
        NomeAplicando.Opacity = 0;
        AnelAplicando.Opacity = 0;
        AnelAplicando.Traco = 2.5;
        PosicionarAplicando(g);
        AbsoluteLayout.SetLayoutBounds(AnelAplicando, g.Grande);
        AbsoluteLayout.SetLayoutBounds(NeonCiano, g.Grande);
        AbsoluteLayout.SetLayoutBounds(NeonRosa, g.Grande);
        NeonCiano.Opacity = NeonRosa.Opacity = 0.35;

        _ = Animar("veu", t => VeuAplicando.Opacity = t, 800, Easing.SinInOut);
        var esquemaEntrou = false;
        var textoEntrou = false;
        await Animar("passagem", t =>
        {
            var e = Curvas.Suave.Ease(t);
            var caixa = Entre(g.Grande, g.Medio, e);
            AbsoluteLayout.SetLayoutBounds(NeonCiano, caixa);
            AbsoluteLayout.SetLayoutBounds(NeonRosa, caixa);
            AbsoluteLayout.SetLayoutBounds(AnelAplicando, caixa);
            var volta = 360 * Curvas.Seno(t);
            var deitado = Math.Min(Curvas.Degrau(0, 0.25, t), 1 - Curvas.Degrau(0.7, 1, t));
            NeonCiano.RotationY = volta;
            NeonRosa.RotationX = -volta + 64 * deitado;
            NeonRosa.Scale = 1 - 0.23 * Curvas.Degrau(0.7, 1, t);
            var brilho = t < 0.12 ? 0.35 + 0.65 * t / 0.12 : t < 0.88 ? 1 : Math.Max(0, 1 - (t - 0.88) / 0.12);
            NeonCiano.Opacity = NeonRosa.Opacity = brilho;
            AnelChao.Opacity = t < 0.12 ? 0.35 * (1 - t / 0.12) : 0;
            AnelAplicando.Opacity = t < 0.87 ? 0 : (t - 0.87) / 0.13;
            AnelAplicando.Traco = 2.5 + 3.5 * e;
            if (!esquemaEntrou && t >= 1.0 / 1.6)
            {
                esquemaEntrou = true;
                _ = Animar("esquema", x => EsquemaVista.Opacity = x, 500, Easing.SinInOut);
            }
            if (!textoEntrou && t >= 1.2 / 1.6)
            {
                textoEntrou = true;
                _ = EntrarTextosAplicandoAsync();
            }
        }, 1600, Easing.Linear);
        NeonCiano.Opacity = NeonRosa.Opacity = 0;
        AnelAplicando.Opacity = 1;
        AnelAplicando.Traco = 6;
        AbsoluteLayout.SetLayoutBounds(AnelAplicando, g.Medio);
        if (v != _versao)
            return;
        await EsperarSemPausaAsync(v);
        if (v == _versao)
            _vm.PassagemTerminou();
    }

    private async Task EntrarTextosAplicandoAsync()
    {
        var rotulo = Animar("rotulo", t =>
        {
            RotuloAplicando.Opacity = t;
            RotuloAplicando.TranslationY = -6 * (1 - t);
        }, 350, Easing.CubicOut);
        await Task.Delay(80);
        await Task.WhenAll(rotulo, Animar("nome", t =>
        {
            NomeAplicando.Opacity = t;
            NomeAplicando.TranslationY = 6 * (1 - t);
        }, 350, Easing.CubicOut));
    }

    /// <summary>Com a animação: cada canal acende 0,7 s, de CH1 a CH4; tudo gravado, 1 s parado e a saída em 0,3 s.</summary>
    private async Task RodarAplicandoAsync(int v)
    {
        while (v == _versao)
        {
            await EsperarSemPausaAsync(v);
            if (v != _versao || !_vm.AvancarCanal())
                break;
            await Task.Delay(700);
        }
        if (v != _versao)
            return;
        await Task.Delay(1000);
        await EsperarSemPausaAsync(v);
        if (v != _versao)
            return;
        await Task.WhenAll(Aplicando.FadeToAsync(0, 300, Easing.SinInOut), Palco.FadeToAsync(0, 300, Easing.SinInOut));
        // A folha de risco pode subir no meio da saída: espera a resposta e tenta de novo.
        while (v == _versao && !_vm.AplicandoTerminou())
        {
            await Task.Delay(100);
            await EsperarSemPausaAsync(v);
        }
    }

    /// <summary>Sem a animação (ou com o movimento reduzido): o Aplicando entra num fade de 0,3 s, já no lugar; os canais seguem os ajustes relidos.</summary>
    private async Task EntrarAplicandoAsync()
    {
        PrepararAplicando();
        Aplicando.Opacity = 0;
        await Aplicando.FadeToAsync(1, 300, Easing.SinInOut);
    }

    /// <summary>O Aplicando no lugar final: o véu, o esquema, APLICANDO, o anel (traço 6) e o nome; sem os anéis de neon.</summary>
    private void PrepararAplicando()
    {
        var g = Geometria();
        Palco.IsVisible = true;
        Palco.Opacity = 1;
        Frase.Opacity = 0;
        AnelChao.Opacity = 0;
        Aplicando.IsVisible = true;
        VeuAplicando.Opacity = 1;
        NeonCiano.Opacity = NeonRosa.Opacity = 0;
        EsquemaVista.Opacity = 1;
        RotuloAplicando.Opacity = 1;
        RotuloAplicando.TranslationY = 0;
        NomeAplicando.Opacity = 1;
        NomeAplicando.TranslationY = 0;
        AnelAplicando.Opacity = 1;
        AnelAplicando.Traco = 6;
        PosicionarAplicando(g);
        AbsoluteLayout.SetLayoutBounds(AnelAplicando, g.Medio);
    }

    private async Task EntrarResumoAsync(int v, FaseCriacao antes)
    {
        if (antes == FaseCriacao.Aplicando && Aplicando.IsVisible && Aplicando.Opacity > 0)
            await Task.WhenAll(Aplicando.FadeToAsync(0, 300, Easing.SinInOut), Palco.FadeToAsync(0, 300, Easing.SinInOut));
        if (v != _versao)
            return;
        Palco.IsVisible = false;
        Aplicando.IsVisible = false;
        await Task.Yield();
        var itens = ListaResumo.Children.OfType<VisualElement>().ToArray();
        if (_vm.MovimentoReduzido)
        {
            foreach (var item in itens)
            {
                item.Opacity = 1;
                item.TranslationY = 0;
            }
            return;
        }
        foreach (var item in itens)
        {
            item.Opacity = 0;
            item.TranslationY = 10;
        }
        for (var i = 0; i < itens.Length && v == _versao; i++)
        {
            var item = itens[i];
            _ = Animar($"item{i}", t =>
            {
                item.Opacity = t;
                item.TranslationY = 10 * (1 - t);
            }, 350, Curvas.Entrada);
            await Task.Delay(70);
        }
    }

    // ------------------------------------------------------------------ lugares

    /// <summary>O texto a cerca de 27 % da altura; o anel grande e apagado no chão, com o topo a 58 %.</summary>
    private void Posicionar()
    {
        if (Width <= 0 || Height <= 0)
            return;
        var g = Geometria();
        AbsoluteLayout.SetLayoutBounds(AnelChao, g.Grande);
        AbsoluteLayout.SetLayoutBounds(Frase, new Rect(18, Height * 0.27, Width - 36, AbsoluteLayout.AutoSize));
        MoverHorizonte(animado: false);
        PosicionarAplicando(g);
        if (_faseVista == FaseCriacao.Aplicando)
            AbsoluteLayout.SetLayoutBounds(AnelAplicando, g.Medio);
    }

    private void PosicionarAplicando((Rect Grande, Rect Medio, Rect Rotulo, Rect Nome, Rect Esquema) g)
    {
        AbsoluteLayout.SetLayoutBounds(RotuloAplicando, g.Rotulo);
        AbsoluteLayout.SetLayoutBounds(NomeAplicando, g.Nome);
        AbsoluteLayout.SetLayoutBounds(EsquemaVista, g.Esquema);
    }

    /// <summary>O horizonte fica 12 px abaixo do texto (uma ou duas linhas) e o chão vai dele ao pé, em 0,5 s.</summary>
    private void MoverHorizonte(bool animado)
    {
        if (Width <= 0 || Height <= 0)
            return;
        var y = Frase.Height > 0 ? Height * 0.27 + Frase.Height + 12 : Height * 0.44;
        var atual = AbsoluteLayout.GetLayoutBounds(Horizonte).Y;
        void Por(double topo)
        {
            AbsoluteLayout.SetLayoutBounds(Horizonte, new Rect(0, topo, Width, 1));
            AbsoluteLayout.SetLayoutBounds(Chao, new Rect(0, topo, Width, Math.Max(0, Height - topo)));
        }
        if (!animado || atual <= 0 || Math.Abs(atual - y) < 1)
        {
            Por(y);
            return;
        }
        _ = Animar("horizonte", t => Por(atual + (y - atual) * t), 500, Curvas.Suave);
    }

    // ------------------------------------------------------------------ a folha de cancelar

    /// <summary>A folha sobe do pé (0,18 s) sobre o véu; fecha sem movimento.</summary>
    private async Task MostrarFolhaCancelarAsync(bool aberta)
    {
        FolhaCancelar.IsVisible = aberta;
        if (!aberta)
            return;
        CorpoFolhaCancelar.TranslationY = 400;
        await CorpoFolhaCancelar.TranslateToAsync(0, 0, 180, Easing.CubicOut);
    }

    // ------------------------------------------------------------------ apoio

    private async Task EsperarSemPausaAsync(int v)
    {
        while (_vm.Pausado && v == _versao)
            await Task.Delay(100);
    }

    private Task Animar(string nome, Action<double> passo, uint ms, Easing curva)
    {
        var feito = new TaskCompletionSource();
        this.AbortAnimation(nome);
        new Animation(passo, 0, 1, curva).Commit(this, nome, 16, ms, finished: (_, _) => feito.TrySetResult());
        return feito.Task;
    }

    /// <summary>O Foco: a posição e o desfoque andam juntos, na mesma curva.</summary>
    private Task Focar(string nome, VisualElement elemento, double de, double ate, double desfoqueDe, double desfoqueAte, uint ms, Easing curva) =>
        Animar(nome, t =>
        {
            elemento.TranslationX = de + (ate - de) * t;
            Desfoque.Aplicar(elemento, desfoqueDe + (desfoqueAte - desfoqueDe) * t);
        }, ms, curva);
}
