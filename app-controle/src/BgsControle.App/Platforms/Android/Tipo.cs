using Android.Graphics;
using Microsoft.Maui.Handlers;

namespace BgsControle.App.Recursos;

/// <summary>
/// O peso da letra (DESIGN, typography): 200 para ler, 700–800 para mexer. O MAUI só tem normal e
/// negrito; aqui o peso vai direto à família que o próprio controle já tem (a do <c>FontFamily</c>, como
/// o monospace do registro, ou o Roboto do sistema). A partir do Android 9 (API 28) o peso é o pedido
/// (100 a 900, até onde a família tiver); antes, normal ou negrito. Todo texto ganha também os números
/// tabulares (<c>tnum</c>), para os valores não pularem ao mudar. Só Android (fica em Platforms/Android).
/// </summary>
public static class Tipo
{
    public static readonly BindableProperty PesoProperty = BindableProperty.CreateAttached(
        "Peso", typeof(int), typeof(Tipo), 400,
        propertyChanged: (b, _, _) => (b as IView)?.Handler?.UpdateValue(nameof(ITextStyle.Font)));

    public static int GetPeso(BindableObject b) => (int)b.GetValue(PesoProperty);

    public static void SetPeso(BindableObject b, int valor) => b.SetValue(PesoProperty, valor);

    /// <summary>Pendura o peso e os números tabulares nos textos, botões e campos (uma vez, ao abrir o app).</summary>
    public static void Registrar()
    {
        LabelHandler.Mapper.AppendToMapping(nameof(ITextStyle.Font), (h, v) => Aplicar(h.PlatformView, v as BindableObject));
        ButtonHandler.Mapper.AppendToMapping(nameof(ITextStyle.Font), (h, v) => Aplicar(h.PlatformView, v as BindableObject));
        EntryHandler.Mapper.AppendToMapping(nameof(ITextStyle.Font), (h, v) => Aplicar(h.PlatformView, v as BindableObject));
    }

    private static void Aplicar(Android.Widget.TextView texto, BindableObject? origem)
    {
        if (origem is null)
            return;
        var peso = Math.Clamp(GetPeso(origem), 100, 900);
        texto.FontFeatureSettings = "tnum";
        // O MAUI acabou de pôr a família do FontFamily (o mapeamento do Font roda antes deste): o peso
        // parte dela, e não da família padrão, para o monospace do registro continuar monospace.
        var familia = texto.Typeface ?? Typeface.Default;
        texto.Typeface = OperatingSystem.IsAndroidVersionAtLeast(28)
            ? Typeface.Create(familia, peso, false)
            : Typeface.Create(familia, peso >= 600 ? TypefaceStyle.Bold : TypefaceStyle.Normal);
    }
}
