using BgsControle.App.Plataforma;
using BgsControle.Nucleo;
using BgsControle.Nucleo.Enlace;
using BgsControle.Nucleo.Escrita;
using BgsControle.Nucleo.Leitura;
using BgsControle.Nucleo.Memorias;
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

        // História 6: a cópia antes de mexer, o link de canais e as memórias, com o estado em
        // JSON nos arquivos privados.
        builder.Services.AddSingleton(sp => new CopiaAntes(
            sp.GetRequiredService<Sessao>(),
            sp.GetRequiredService<EstadoAparelho>(),
            sp.GetRequiredService<Escritor>(),
            sp.GetRequiredService<LeituraAutomatica>(),
            new RepositorioTextoArquivos(sp.GetRequiredService<IArquivosApp>(), RepositorioTextoArquivos.ArquivoCopia),
            sp.GetRequiredService<LogBle>()));
        builder.Services.AddSingleton(sp => new LinkCanais(
            sp.GetRequiredService<Sessao>(),
            sp.GetRequiredService<EstadoAparelho>(),
            sp.GetRequiredService<Escritor>(),
            new RepositorioTextoArquivos(sp.GetRequiredService<IArquivosApp>(), RepositorioTextoArquivos.ArquivoLink),
            sp.GetRequiredService<LogBle>()));
        builder.Services.AddSingleton(sp => new GerenteMemorias(
            sp.GetRequiredService<Sessao>(),
            sp.GetRequiredService<EstadoAparelho>(),
            sp.GetRequiredService<LeituraAutomatica>(),
            sp.GetRequiredService<CopiaAntes>(),
            sp.GetRequiredService<LogBle>()));

        builder.Services.AddSingleton<ConexaoViewModel>();
        // Os ViewModels dos ajustes, das chaves, das memórias e do link (sem tela nova: o XAML é
        // das histórias 10 e 11). Singletons: assinam eventos do núcleo; transitórios no
        // contêiner raiz vazariam a cada resolução.
        builder.Services.AddSingleton<FabricaControles>();
        builder.Services.AddSingleton<ChavesViewModel>();
        builder.Services.AddSingleton<MemoriasViewModel>();
        builder.Services.AddSingleton<LinkViewModel>();
        builder.Services.AddSingleton<MainPage>();

        var app = builder.Build();
        // Criados já: o espelho e a cópia antes precisam ver a primeira leitura (a retomada do
        // "aplicado pela metade"), o link se pendura no escritor, e as chaves, a cópia e o link
        // começam a ler o guardado.
        app.Services.GetRequiredService<EstadoAparelho>();
        app.Services.GetRequiredService<Escritor>();
        app.Services.GetRequiredService<LinkCanais>();
        app.Services.GetRequiredService<GerenteMemorias>(); // cria a CopiaAntes e registra as memórias nela
        return app;
    }
}
