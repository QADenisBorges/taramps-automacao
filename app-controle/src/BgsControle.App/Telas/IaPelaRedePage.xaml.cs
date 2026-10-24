using BgsControle.Nucleo.Seguranca;
using BgsControle.Telas;

namespace BgsControle.App.Telas;

/// <summary>A chave "IA pela rede" e a folha do risco vêm das Chaves de acesso; o resto, da IA pela rede.</summary>
public partial class IaPelaRedePage : ContentPage
{
    private readonly ChavesViewModel _chaves;

    public IaPelaRedePage(IaPelaRedeViewModel vm, ChavesViewModel chaves)
    {
        InitializeComponent();
        _chaves = chaves;
        LinhaChave.BindingContext = chaves.Item(Chave.IaPelaRede);
        FolhaChave.BindingContext = chaves;
        BindingContext = vm;
    }

    private void AoVoltar(object? sender, EventArgs e) => _ = Navigation.PopAsync();

    protected override bool OnBackButtonPressed()
    {
        if (_chaves.FolhaAberta)
        {
            _chaves.DesistirFolha.Execute(null);
            return true;
        }
        return base.OnBackButtonPressed();
    }
}
