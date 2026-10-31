using Android.Graphics;
using Android.Provider;

namespace BgsControle.App.Plataforma;

/// <summary>
/// O movimento reduzido do Android (EXPERIENCE, Accessibility Floor; história 10d): a escala das animações do
/// sistema em 0 (Opções do desenvolvedor ou "Remover animações" da acessibilidade). Só Android.
/// </summary>
public static class Movimento
{
    /// <summary>As animações do sistema estão desligadas (<c>ANIMATOR_DURATION_SCALE</c> = 0).</summary>
    public static bool Reduzido()
    {
        try
        {
            var resolver = Android.App.Application.Context.ContentResolver;
            return resolver is not null && Settings.Global.GetFloat(resolver, Settings.Global.AnimatorDurationScale, 1f) == 0f;
        }
        catch (Exception)
        {
            return false; // sem a leitura, vale a animação
        }
    }
}

/// <summary>
/// O desfoque do Foco (DESIGN, motion.phrase-in/out e value-new): <c>RenderEffect</c> no Android 12 (API 31) ou mais
/// novo; abaixo disso, sem desfoque (a frase só anda). Só Android.
/// </summary>
public static class Desfoque
{
    /// <summary>Desfoca a vista em <paramref name="raio"/> px (0 tira o desfoque).</summary>
    public static void Aplicar(VisualElement elemento, double raio)
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(31) || elemento.Handler?.PlatformView is not Android.Views.View vista)
            return;
        var px = (float)(raio * (vista.Resources?.DisplayMetrics?.Density ?? 1f));
        vista.SetRenderEffect(px < 0.5f ? null : RenderEffect.CreateBlurEffect(px, px, Shader.TileMode.Decal!));
    }
}
