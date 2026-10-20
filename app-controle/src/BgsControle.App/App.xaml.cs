namespace BgsControle.App;

public partial class App : Application
{
    private readonly MainPage _pagina;

    public App(MainPage pagina)
    {
        InitializeComponent();
        _pagina = pagina;
    }

    protected override Window CreateWindow(IActivationState? activationState) =>
        new(_pagina) { Title = "BGS Controle" };
}
