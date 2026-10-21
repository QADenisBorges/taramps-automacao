using BgsControle.App.Plataforma;
using BgsControle.Nucleo;
using BgsControle.Nucleo.Enlace;
using BgsControle.Nucleo.Escrita;
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
        // As chaves de acesso (CAP-11), em JSON nos arquivos privados. A trava da sessão pergunta a elas.
        builder.Services.AddSingleton<IRepositorioChaves, RepositorioChavesArquivos>();
        builder.Services.AddSingleton<IChaves, Chaves>();
        builder.Services.AddSingleton(sp => new Sessao(
            sp.GetRequiredService<ITransporte>(),
            sp.GetRequiredService<ICofreSenha>(),
            sp.GetRequiredService<LogBle>(),
            chaves: sp.GetRequiredService<IChaves>()));
        builder.Services.AddSingleton(sp => new LeitorCompleto(
            sp.GetRequiredService<Sessao>(),
            sp.GetRequiredService<LogBle>()));
        builder.Services.AddSingleton(sp => new LeituraAutomatica(
            sp.GetRequiredService<Sessao>(),
            sp.GetRequiredService<LeitorCompleto>()));

        // A escrita dos ajustes de áudio: o espelho do aparelho e o escritor (eco e releitura).
        builder.Services.AddSingleton(sp => new EstadoAparelho(
            sp.GetRequiredService<Sessao>(),
            sp.GetRequiredService<LeituraAutomatica>()));
        builder.Services.AddSingleton(sp => new Escritor(
            sp.GetRequiredService<Sessao>(),
            sp.GetRequiredService<EstadoAparelho>(),
            sp.GetRequiredService<LogBle>()));

        // As abstrações das telas (BgsControle.Telas), com os adaptadores do MAUI e do Android.
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<IDespachante, DespachanteMaui>();
        builder.Services.AddSingleton<IArquivosApp, ArquivosMaui>();
        builder.Services.AddSingleton<IPreparoBluetooth, PreparoBluetoothAndroid>();
        builder.Services.AddSingleton<ConexaoViewModel>();
        // Os ViewModels dos ajustes e das chaves (sem tela nova: o XAML é das histórias 10 e 11).
        builder.Services.AddSingleton<FabricaControles>();
        // Singleton: assina Chaves.Mudou; transitório no contêiner raiz vazaria a cada resolução.
        builder.Services.AddSingleton<ChavesViewModel>();
        builder.Services.AddSingleton<MainPage>();

        var app = builder.Build();
        // Criados já: o espelho precisa ver a primeira leitura, e as chaves começam a ler o guardado.
        app.Services.GetRequiredService<EstadoAparelho>();
        app.Services.GetRequiredService<Escritor>();
        return app;
    }
}
