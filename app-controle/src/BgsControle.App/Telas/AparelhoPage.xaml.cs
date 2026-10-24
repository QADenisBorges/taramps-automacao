using BgsControle.Telas;

namespace BgsControle.App.Telas;

/// <summary>Navegação (‹ e Voltar, que fecha a folha antes), as maiúsculas automáticas nas caixas da senha e o esquecer das senhas ao sair.</summary>
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

    // Ao sair da tela, as senhas digitadas não ficam guardadas no ViewModel (que é único no app).
    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _vm.EsquecerSenhasDigitadas();
    }

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
