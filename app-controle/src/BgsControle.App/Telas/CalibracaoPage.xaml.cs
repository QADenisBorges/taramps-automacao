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

    private bool _voltarAoFechar;
    private bool _abertaVista;

    public CalibracaoPage(CalibracaoViewModel vm, CascaViewModel? casca = null)
    {
        InitializeComponent();
        _vm = vm;
        _abertaVista = vm.Aberta;
        BindingContext = vm;
        _vm.PropertyChanged += AoMudar;
        // O aviso rápido da casca, também aqui.
        Controles.EspelhoAvisoRapido.Ligar(this, AvisoRapido, casca);
    }

    /// <summary>
    /// A navegação abre a tela (história 13g). Sem saída (Configurações › "Calibrar saídas"): a escolha, ou a calibração já
    /// aberta. Com a saída (o "Calibrar ›"): a calibração dela no passo 1 (<see cref="CalibracaoViewModel.AbrirPara"/>, que
    /// não troca uma calibração de outra saída aberta); quando ela fecha (Concluir ou Cancelar), a tela sai sozinha.
    /// </summary>
    public void Abrir(int? canal)
    {
        if (canal is { } c)
            _voltarAoFechar = _vm.AbrirPara(c);
    }

    private void Sair()
    {
        if (Navigation.NavigationStack.LastOrDefault() == this)
            _ = Navigation.PopAsync();
    }

    private void AoEscolher(object? sender, TappedEventArgs e)
    {
        if (sender is BindableObject { BindingContext: OpcaoSaidaCalibracao opcao })
            _vm.Escolher.Execute(opcao.Canal);
    }

    private void AoVoltar(object? sender, EventArgs e) => Sair();

    // O campo dos volts apareceu: o teclado abre; sumiu: fecha. A calibração aberta pelo "Calibrar ›" fechou: a tela sai.
    private void AoMudar(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CalibracaoViewModel.Aberta))
        {
            var antes = _abertaVista;
            _abertaVista = _vm.Aberta;
            if (antes && !_abertaVista && _voltarAoFechar)
            {
                _voltarAoFechar = false;
                Sair();
            }
            return;
        }
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
