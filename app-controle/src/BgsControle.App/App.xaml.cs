namespace BgsControle.App;

public partial class App : Application
{
    private readonly IServiceProvider _servicos;

    public App(IServiceProvider servicos)
    {
        InitializeComponent();
        _servicos = servicos;
    }

    // A página é criada aqui, depois do InitializeComponent: as cores do App.xaml já existem.
    protected override Window CreateWindow(IActivationState? activationState) =>
        new(_servicos.GetRequiredService<MainPage>()) { Title = "BGS Controle" };
}
