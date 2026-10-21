using BgsControle.App.Plataforma;
using BgsControle.Nucleo;
using BgsControle.Nucleo.Enlace;
using BgsControle.Nucleo.Leitura;
using BgsControle.Nucleo.Registro;
using BgsControle.Nucleo.Seguranca;
using BgsControle.Telas;
using Microsoft.Extensions.Logging;

namespace BgsControle.App;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>();

        // Sem provedores de log: nada da sessão vai ao logcat.
        builder.Logging.ClearProviders();

        builder.Services.AddSingleton<LogBle>();
        builder.Services.AddSingleton<ITransporte>(_ => new TransporteAndroid(Android.App.Application.Context));
        builder.Services.AddSingleton<ICofreSenha, CofreSecureStorage>();
        builder.Services.AddSingleton(sp => new Sessao(
            sp.GetRequiredService<ITransporte>(),
            sp.GetRequiredService<ICofreSenha>(),
            sp.GetRequiredService<LogBle>()));
        builder.Services.AddSingleton(sp => new LeitorCompleto(
            sp.GetRequiredService<Sessao>(),
            sp.GetRequiredService<LogBle>()));
        builder.Services.AddSingleton(sp => new LeituraAutomatica(
            sp.GetRequiredService<Sessao>(),
            sp.GetRequiredService<LeitorCompleto>()));

        // As abstrações das telas (BgsControle.Telas), com os adaptadores do MAUI e do Android.
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<IDespachante, DespachanteMaui>();
        builder.Services.AddSingleton<IArquivosApp, ArquivosMaui>();
        builder.Services.AddSingleton<IPreparoBluetooth, PreparoBluetoothAndroid>();
        builder.Services.AddSingleton<ConexaoViewModel>();
        builder.Services.AddSingleton<MainPage>();

        return builder.Build();
    }
}
