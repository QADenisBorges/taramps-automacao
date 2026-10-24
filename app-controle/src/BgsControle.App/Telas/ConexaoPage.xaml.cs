using System.ComponentModel;
using BgsControle.Telas;

namespace BgsControle.App.Telas;

/// <summary>Navegação e teclado: ‹ volta; o campo da senha abre o teclado (maiúsculas) quando esta tela está na frente e fecha ao enviar.</summary>
public partial class ConexaoPage : ContentPage
{
    private readonly ConexaoViewModel _vm;

    public ConexaoPage(ConexaoViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;
        CampoSenha.Keyboard = Keyboard.Create(KeyboardFlags.CapitalizeCharacter);
        vm.PropertyChanged += AoMudar;
    }

    /// <summary>Esta tela é a da frente (a senha cai aqui, e não no Início escondido): na pilha, ou por cima da escolha do Setup.</summary>
    public bool NaFrente => Navigation.ModalStack.Count > 0
        ? Navigation.ModalStack[^1] == this
        : Navigation.NavigationStack.LastOrDefault() == this;

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (_vm.PedindoSenha)
            Dispatcher.Dispatch(() => CampoSenha.Focus());
    }

    private void AoVoltar(object? sender, EventArgs e)
    {
        FecharTeclado();
        _ = Navigation.ModalStack.Count > 0 && Navigation.ModalStack[^1] == this
            ? Navigation.PopModalAsync(false)
            : Navigation.PopAsync();
    }

    private void AoEnviarSenha(object? sender, EventArgs e) => FecharTeclado();

    private void AoMudar(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ConexaoViewModel.PedindoSenha))
            return;
        if (_vm.PedindoSenha && NaFrente)
            Dispatcher.Dispatch(() => CampoSenha.Focus());
        else if (!_vm.PedindoSenha)
            FecharTeclado();
    }

    private void FecharTeclado()
    {
        CampoSenha.Unfocus();
        if (CampoSenha.IsSoftInputShowing())
            _ = CampoSenha.HideSoftInputAsync(CancellationToken.None);
    }
}
