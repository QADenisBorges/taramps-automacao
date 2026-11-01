using System.ComponentModel;
using BgsControle.Nucleo.Rendimento;
using BgsControle.Telas;

namespace BgsControle.App.Telas;

/// <summary>
/// O teste a seco (história 13g): a tecla Voltar faz o Cancelar no passo 2 (o preparo e os trechos) e fecha nos passos 1
/// (o Cancelar do aviso) e 3 (o Concluir). Quando o teste fecha (Cancelar no aviso, Concluir), a tela sai sozinha.
/// </summary>
public partial class TesteSecoPage : ContentPage
{
    private readonly TesteSecoViewModel _vm;
    private EtapaTesteSeco _vista;

    public TesteSecoPage(TesteSecoViewModel vm, CascaViewModel? casca = null)
    {
        InitializeComponent();
        _vm = vm;
        _vista = vm.Etapa;
        BindingContext = vm;
        _vm.PropertyChanged += AoMudar;
        // O aviso rápido da casca, também aqui.
        Controles.EspelhoAvisoRapido.Ligar(this, AvisoRapido, casca);
    }

    private void AoVoltar(object? sender, EventArgs e) => Sair();

    private void Sair()
    {
        if (Navigation.NavigationStack.LastOrDefault() == this)
            _ = Navigation.PopAsync();
    }

    private void AoMudar(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(TesteSecoViewModel.Etapa))
            return;
        var antes = _vista;
        _vista = _vm.Etapa;
        if (_vista == EtapaTesteSeco.Fechado && antes != EtapaTesteSeco.Fechado)
            Sair();
    }

    protected override bool OnBackButtonPressed()
    {
        switch (_vm.Etapa)
        {
            case EtapaTesteSeco.Aviso or EtapaTesteSeco.Preparar or EtapaTesteSeco.Tocando:
                _vm.Cancelar.Execute(null);
                return true;
            case EtapaTesteSeco.Resultado:
                _vm.Concluir.Execute(null);
                return true;
            default:
                return base.OnBackButtonPressed();
        }
    }
}
