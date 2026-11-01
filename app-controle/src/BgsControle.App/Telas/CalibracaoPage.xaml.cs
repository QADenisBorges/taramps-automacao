using System.ComponentModel;
using BgsControle.Telas;

namespace BgsControle.App.Telas;

/// <summary>
/// A calibração guiada (história 13g): o toque numa saída da escolha, o ‹ (só na escolha) e a tecla Voltar do Android,
/// que segue o Cancelar (no fim, o Concluir; na escolha, sai). O campo dos volts abre o teclado quando aparece.
/// </summary>
public partial class CalibracaoPage : ContentPage
{
    private readonly CalibracaoViewModel _vm;

    public CalibracaoPage(CalibracaoViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;
        _vm.PropertyChanged += AoMudar;
    }

    private void AoEscolher(object? sender, TappedEventArgs e)
    {
        if (sender is BindableObject { BindingContext: OpcaoSaidaCalibracao opcao })
            _vm.Escolher.Execute(opcao.Canal);
    }

    private void AoVoltar(object? sender, EventArgs e) => _ = Navigation.PopAsync();

    // O campo dos volts apareceu: o teclado abre; sumiu: fecha.
    private void AoMudar(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(CalibracaoViewModel.PedeVolts))
            return;
        if (_vm.PedeVolts)
            Dispatcher.Dispatch(() => CampoVolts.Focus());
        else if (CampoVolts.IsSoftInputShowing())
        {
            CampoVolts.Unfocus();
            _ = CampoVolts.HideSoftInputAsync(CancellationToken.None);
        }
    }

    protected override bool OnBackButtonPressed()
    {
        if (_vm.PodeCancelar)
        {
            _vm.Cancelar.Execute(null);
            return true;
        }
        if (_vm.NoFim)
        {
            _vm.Concluir.Execute(null);
            return true;
        }
        return base.OnBackButtonPressed();
    }
}
