using System.ComponentModel;
using BgsControle.Telas;

namespace BgsControle.App.Telas;

/// <summary>Só teclado: o campo da senha (maiúsculas automáticas) e o teclado que fecha ao enviar.</summary>
public partial class InicioView : ContentView
{
    private ConexaoViewModel? _conexao;

    public InicioView()
    {
        InitializeComponent();
        CampoSenha.Keyboard = Keyboard.Create(KeyboardFlags.CapitalizeCharacter);
    }

    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();
        if (_conexao is not null)
            _conexao.PropertyChanged -= AoMudar;
        _conexao = (BindingContext as InicioViewModel)?.Conexao;
        if (_conexao is not null)
            _conexao.PropertyChanged += AoMudar;
    }

    // Enviou a senha: o teclado fecha.
    private void AoEnviarSenha(object? sender, EventArgs e) => FecharTeclado();

    // O campo da senha apareceu: o teclado abre; sumiu (conectou, desistiu, prazo): fecha junto.
    private void AoMudar(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ConexaoViewModel.PedindoSenha) || _conexao is null)
            return;
        // Só com a casca na frente: com a tela de conexão aberta, a senha é digitada lá.
        if (_conexao.PedindoSenha && Navigation is { NavigationStack.Count: <= 1, ModalStack.Count: 0 })
            Dispatcher.Dispatch(() => CampoSenha.Focus());
        else
            FecharTeclado();
    }

    private void FecharTeclado()
    {
        CampoSenha.Unfocus();
        if (CampoSenha.IsSoftInputShowing())
            _ = CampoSenha.HideSoftInputAsync(CancellationToken.None);
    }
}
