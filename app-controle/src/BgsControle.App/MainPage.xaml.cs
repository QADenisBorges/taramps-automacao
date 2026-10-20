using System.ComponentModel;

namespace BgsControle.App;

public partial class MainPage : ContentPage
{
    public MainPage(ConexaoViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        viewModel.PropertyChanged += AoMudar;
    }

    // Enviou a senha: o teclado fecha.
    private void AoEnviarSenha(object? sender, EventArgs e) => FecharTeclado();

    // O campo de senha sumiu da tela (conectou, desistiu, prazo): o teclado fecha junto.
    private void AoMudar(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ConexaoViewModel.PedindoSenha) && sender is ConexaoViewModel { PedindoSenha: false })
            FecharTeclado();
    }

    private void FecharTeclado()
    {
        CampoSenha.Unfocus();
        if (CampoSenha.IsSoftInputShowing())
            _ = CampoSenha.HideSoftInputAsync(CancellationToken.None);
    }
}
