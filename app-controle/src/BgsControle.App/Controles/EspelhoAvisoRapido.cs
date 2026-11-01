using System.ComponentModel;
using BgsControle.Telas;

namespace BgsControle.App.Controles;

/// <summary>
/// O aviso rápido da casca espelhado numa tela cheia (história 13g): a página por cima da casca (Ajuste, Função, Memórias,
/// Calibração, Teste a seco) mostra o mesmo aviso de 2,6 s, sem anunciar de novo. Já nasce com o aviso de agora; ouve a
/// casca só enquanto a página está na tela (Loaded/Unloaded), sem deixar o ouvinte pendurado.
/// </summary>
public static class EspelhoAvisoRapido
{
    public static void Ligar(Page pagina, AvisoRapidoView aviso, CascaViewModel? casca)
    {
        if (casca is null)
            return;
        var ligado = false;
        void AoMudar(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(CascaViewModel.AvisoRapido))
                aviso.Texto = casca.AvisoRapido;
        }
        void Ligar()
        {
            if (ligado)
                return;
            ligado = true;
            casca.PropertyChanged += AoMudar;
            aviso.Texto = casca.AvisoRapido;
        }
        void Desligar()
        {
            if (!ligado)
                return;
            ligado = false;
            casca.PropertyChanged -= AoMudar;
        }
        aviso.Texto = casca.AvisoRapido;
        pagina.Loaded += (_, _) => Ligar();
        pagina.Unloaded += (_, _) => Desligar();
    }
}
