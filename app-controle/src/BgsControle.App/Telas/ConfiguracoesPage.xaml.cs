using BgsControle.Telas;

namespace BgsControle.App.Telas;

/// <summary>Navegação: o Voltar do Android fecha a folha dos resets antes de sair.</summary>
public partial class ConfiguracoesPage : ContentPage
{
    private readonly ConfiguracoesViewModel _vm;

    public ConfiguracoesPage(ConfiguracoesViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        FolhaAparelho.BindingContext = vm.Aparelho;
        CampoNome.Keyboard = Keyboard.Create(KeyboardFlags.CapitalizeCharacter);
        BindingContext = vm;
    }

    protected override bool OnBackButtonPressed()
    {
        if (_vm.Aparelho is { FolhaAberta: true } aparelho)
        {
            aparelho.CancelarFolha.Execute(null);
            return true;
        }
        return base.OnBackButtonPressed();
    }
}
