using System.Windows.Input;

namespace BgsControle.App.Controles;

/// <summary>
/// Os botões − e + (DESIGN, botoes-mais-menos): o dedo descer dá um passo e, segurando, repete; o dedo
/// subir para. Os dois momentos vão aos comandos do ViewModel (que conta o tempo da repetição).
/// </summary>
public sealed class BotaoSegurar : Button
{
    public static readonly BindableProperty ComandoApertarProperty =
        BindableProperty.Create(nameof(ComandoApertar), typeof(ICommand), typeof(BotaoSegurar));

    public static readonly BindableProperty ComandoSoltarProperty =
        BindableProperty.Create(nameof(ComandoSoltar), typeof(ICommand), typeof(BotaoSegurar));

    public BotaoSegurar()
    {
        Pressed += (_, _) =>
        {
            if (ComandoApertar?.CanExecute(null) == true)
                ComandoApertar.Execute(null);
        };
        Released += (_, _) =>
        {
            if (ComandoSoltar?.CanExecute(null) == true)
                ComandoSoltar.Execute(null);
        };
    }

    public ICommand? ComandoApertar { get => (ICommand?)GetValue(ComandoApertarProperty); set => SetValue(ComandoApertarProperty, value); }

    public ICommand? ComandoSoltar { get => (ICommand?)GetValue(ComandoSoltarProperty); set => SetValue(ComandoSoltarProperty, value); }
}
