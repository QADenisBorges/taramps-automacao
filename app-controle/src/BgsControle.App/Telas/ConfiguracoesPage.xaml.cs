using BgsControle.Telas;

namespace BgsControle.App.Telas;

/// <summary>
/// Navegação: o Voltar do Android fecha a folha dos resets (ou a do risco da chave "Controle pela
/// rede") antes de sair. A folha da chave é a das Chaves de acesso.
/// </summary>
public partial class ConfiguracoesPage : ContentPage
{
    private readonly ConfiguracoesViewModel _vm;

    public ConfiguracoesPage(ConfiguracoesViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        FolhaAparelho.BindingContext = vm.Aparelho;
        FolhaChave.BindingContext = vm.ControlePelaRede?.Chaves;
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
        if (_vm.ControlePelaRede?.Chaves is { FolhaAberta: true } chaves)
        {
            chaves.DesistirFolha.Execute(null);
            return true;
        }
        return base.OnBackButtonPressed();
    }
}
