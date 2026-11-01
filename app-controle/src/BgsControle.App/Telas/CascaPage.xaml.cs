using BgsControle.Telas;

namespace BgsControle.App.Telas;

/// <summary>Navegação e o botão Voltar: a folha aberta fecha antes; fora do Início, volta ao Início.</summary>
public partial class CascaPage : ContentPage
{
    private readonly CascaViewModel _vm;

    public CascaPage(CascaViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        // A dúvida (ou a IA, ou o alarme do gerador) pode ter chegado antes de a casca aparecer.
        _vm.Sincronizar(forcar: true);
        _vm.SincronizarCamadas(forcar: true);
        _vm.SincronizarFolhas(forcar: true);
    }

    protected override bool OnBackButtonPressed()
    {
        if (_vm.Memorias.ListaAberta)
        {
            _vm.Memorias.FecharLista.Execute(null);
            return true;
        }
        if (_vm.Inicio.Presets is { FolhaAberta: true } presets)
        {
            presets.FecharFolha.Execute(null);
            return true;
        }
        if (!_vm.NoInicio)
        {
            _vm.Ir(Aba.Inicio);
            return true;
        }
        return base.OnBackButtonPressed();
    }
}
