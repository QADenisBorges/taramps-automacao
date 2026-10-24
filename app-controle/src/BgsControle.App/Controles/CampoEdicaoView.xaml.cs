using System.ComponentModel;
using BgsControle.Telas;

namespace BgsControle.App.Controles;

/// <summary>Só teclado: abre com o foco no campo, e fechar o teclado sem ✓ descarta.</summary>
public partial class CampoEdicaoView : ContentView
{
    private ICampoEditavel? _vm;

    public CampoEdicaoView()
    {
        InitializeComponent();
    }

    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();
        if (_vm is not null)
            _vm.PropertyChanged -= AoMudar;
        _vm = BindingContext as ICampoEditavel;
        if (_vm is not null)
            _vm.PropertyChanged += AoMudar;
    }

    private void AoMudar(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ICampoEditavel.EditandoCampo) || _vm is null)
            return;
        if (_vm.EditandoCampo)
        {
            Dispatcher.Dispatch(() =>
            {
                Campo.Focus();
                Campo.CursorPosition = Campo.Text?.Length ?? 0;
            });
        }
        else if (Campo.IsSoftInputShowing())
        {
            Campo.Unfocus();
            _ = Campo.HideSoftInputAsync(CancellationToken.None);
        }
    }

    // O teclado fechou sem ✓: descarta. Com um erro na tela, o campo fica (o Denis corrige ou cancela).
    private void AoSair(object? sender, FocusEventArgs e)
    {
        if (_vm is { EditandoCampo: true, ErroCampo: null })
            _vm.FecharCampo.Execute(null);
    }
}
