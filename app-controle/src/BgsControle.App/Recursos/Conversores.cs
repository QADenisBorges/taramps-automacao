using System.Globalization;
using BgsControle.Telas;

namespace BgsControle.App.Recursos;

/// <summary>Uma cor do dicionário do app pelo nome do token ("canal-1", "texto").</summary>
public static class Cores
{
    public static Color De(string token) =>
        Application.Current?.Resources.TryGetValue(token, out var v) == true && v is Color c ? c : Colors.Transparent;

    /// <summary>
    /// A luz de um canal do processador (0 a 3; numa saída, o canal mestre): CH1 azul, CH2 lilás, CH3
    /// âmbar, CH4 menta (tokens <c>canal-1</c> a <c>canal-4</c>); sem canal, o branco do texto.
    /// </summary>
    public static Color DoCanal(int? canal) => De(canal is >= 0 and <= 3 ? $"canal-{canal + 1}" : "texto");
}

/// <summary>
/// O canal mestre de uma saída (0 a 3, ou null) → a cor dela. O parâmetro "apoio" troca o null por
/// {colors.apoio} (os trechos de texto dos resumos).
/// </summary>
public sealed class CorDaSaida : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var cor = value is int canal
            ? Cores.DoCanal(canal)
            : parameter as string is { Length: > 0 } token ? Cores.De(token) : Cores.DoCanal(null);
        return typeof(Brush).IsAssignableFrom(targetType) ? new SolidColorBrush(cor) : cor;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>bool → o contrário.</summary>
public sealed class Inverso : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value is not true;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => value is not true;
}

/// <summary>Texto (ou qualquer coisa) → visível quando não é nulo nem vazio.</summary>
public sealed class TemTexto : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        null => false,
        string s => s.Length > 0,
        System.Collections.ICollection c => c.Count > 0,
        _ => true,
    };

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>
/// O nome da memória em uso (DESIGN, cartão da memória em uso): 60 px; um nome longo diminui até os
/// 36 px da lista e depois quebra em duas linhas; nunca corta.
/// </summary>
public sealed class TamanhoDoNome : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        (value as string)?.Length switch
        {
            null or <= 8 => 60d,
            <= 10 => 46d,
            _ => 36d,
        };

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}
