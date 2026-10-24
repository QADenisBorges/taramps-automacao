using BgsControle.Telas;

namespace BgsControle.App.Telas;

/// <summary>Navegação (‹ e Voltar, que fecha a folha antes) e as maiúsculas automáticas nas caixas da senha.</summary>
public partial class AparelhoPage : ContentPage
{
    private readonly AparelhoViewModel _vm;

    public AparelhoPage(AparelhoViewModel vm)
    {
        InitializeComponent();
        foreach (var campo in new[] { Atual, Nova, Confirmacao })
            campo.Keyboard = Keyboard.Create(KeyboardFlags.CapitalizeCharacter);
        _vm = vm;
        BindingContext = vm;
    }

    private void AoVoltar(object? sender, EventArgs e) => _ = Navigation.PopAsync();

    protected override bool OnBackButtonPressed()
    {
        if (_vm.FolhaAberta)
        {
            _vm.CancelarFolha.Execute(null);
            return true;
        }
        return base.OnBackButtonPressed();
    }
}
