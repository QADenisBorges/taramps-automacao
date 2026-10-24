using BgsControle.App.Plataforma;
using BgsControle.App.Recursos;
using BgsControle.App.Telas;
using BgsControle.Nucleo;
using BgsControle.Nucleo.Aparelho;
using BgsControle.Nucleo.Enlace;
using BgsControle.Nucleo.Escrita;
using BgsControle.Nucleo.Gerador;
using BgsControle.Nucleo.Leitura;
using BgsControle.Nucleo.Memorias;
using BgsControle.Nucleo.Player;
using BgsControle.Nucleo.Presets;
using BgsControle.Nucleo.Registro;
using BgsControle.Nucleo.Seguranca;
using BgsControle.Nucleo.Setups;
using BgsControle.Telas;
using Microsoft.Extensions.Logging;

namespace BgsControle.App;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>();
        // O peso da letra (200 a 800) e os números tabulares do DESIGN.md.
        Tipo.Registrar();

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

        // História 8 e 8d: os presets de 15 bandas no celular (criar no Salvar, aplicar, renomear, apagar), com
        // o aplicar pela cópia antes, e as playlists por memória do celular (playlists.json). Antes dos
        // Setups, que os recebem. O conhecimento herdado (CAP-6) vem embutido no núcleo.
        builder.Services.AddSingleton(sp => new GerentePresets(
            sp.GetRequiredService<Sessao>(),
            sp.GetRequiredService<EstadoAparelho>(),
            sp.GetRequiredService<Escritor>(),
            sp.GetRequiredService<LeituraAutomatica>(),
            sp.GetRequiredService<CopiaAntes>(),
            new RepositorioTextoArquivos(sp.GetRequiredService<IArquivosApp>(), RepositorioTextoArquivos.ArquivoPresets),
            sp.GetRequiredService<LogBle>()));
        builder.Services.AddSingleton(sp => new GerentePlaylists(
            new RepositorioTextoArquivos(sp.GetRequiredService<IArquivosApp>(), RepositorioTextoArquivos.ArquivoPlaylists),
            sp.GetRequiredService<LogBle>()));

        // História 8c e 8d: os Setups e as memórias do celular (conferência ao conectar, aplicar com o preset
        // lembrado, Salvar por camadas), em setups.json, e a marca do mudo da dúvida em setup-mudo.json.
        builder.Services.AddSingleton(sp => new GerenteSetups(
            sp.GetRequiredService<Sessao>(),
            sp.GetRequiredService<EstadoAparelho>(),
            sp.GetRequiredService<Escritor>(),
            sp.GetRequiredService<LeituraAutomatica>(),
            sp.GetRequiredService<CopiaAntes>(),
            sp.GetRequiredService<LinkCanais>(),
            sp.GetRequiredService<GerenteMemorias>(),
            sp.GetRequiredService<GerentePresets>(),
            sp.GetRequiredService<GerentePlaylists>(),
            sp.GetRequiredService<IChaves>(),
            new RepositorioTextoArquivos(sp.GetRequiredService<IArquivosApp>(), RepositorioTextoArquivos.ArquivoSetups),
            new RepositorioTextoArquivos(sp.GetRequiredService<IArquivosApp>(), RepositorioTextoArquivos.ArquivoSetupMudo),
            sp.GetRequiredService<LogBle>()));

        // História 7a: o gerador de sinal, com as regras de falha 1 e 2 (desliga no prazo, no
        // Parar, antes de soltar a conexão, ao sair do app e no fim de cada leitura).
        builder.Services.AddSingleton(sp => new GeradorSinal(
            sp.GetRequiredService<Sessao>(),
            sp.GetRequiredService<EstadoAparelho>(),
            sp.GetRequiredService<LeituraAutomatica>(),
            sp.GetRequiredService<CopiaAntes>(),
            sp.GetRequiredService<LogBle>(),
            sp.GetRequiredService<GerenteMemorias>()));

        // História 7b: a monitoração (voltímetro e clip, só ouve) e os comandos de aparelho (remote
        // out, Bluetooth de áudio, resets, troca de senha e nome do produto).
        builder.Services.AddSingleton(sp => new MonitorAparelho(
            sp.GetRequiredService<Sessao>(),
            sp.GetRequiredService<LeituraAutomatica>(),
            sp.GetRequiredService<TimeProvider>()));
        builder.Services.AddSingleton(sp => new ComandosAparelho(
            sp.GetRequiredService<Sessao>(),
            sp.GetRequiredService<EstadoAparelho>(),
            sp.GetRequiredService<LeituraAutomatica>(),
            sp.GetRequiredService<CopiaAntes>(),
            sp.GetRequiredService<ICofreSenha>(),
            sp.GetRequiredService<LogBle>(),
            sp.GetRequiredService<GerenteMemorias>(),
            sp.GetRequiredService<GeradorSinal>()));

        builder.Services.AddSingleton<ConexaoViewModel>();
        // Os ViewModels dos ajustes, das chaves, das memórias, do link, dos presets, do conhecimento herdado, do gerador, da monitoração e
        // do aparelho (sem tela nova: o XAML é das histórias 10 e 11). Singletons: assinam eventos do núcleo; transitórios no
        // contêiner raiz vazariam a cada resolução.
        builder.Services.AddSingleton<FabricaControles>();
        builder.Services.AddSingleton<ChavesViewModel>();
        builder.Services.AddSingleton<SalvarViewModel>();
        builder.Services.AddSingleton<SetupViewModel>();
        builder.Services.AddSingleton<EquipamentosViewModel>(); // história 8e (a tela Setup é da história 10)
        builder.Services.AddSingleton<MemoriasViewModel>();
        builder.Services.AddSingleton<LinkViewModel>();
        builder.Services.AddSingleton<PresetsViewModel>();
        builder.Services.AddSingleton<HerdadoViewModel>();
        builder.Services.AddSingleton(sp => new GeradorViewModel(
            sp.GetRequiredService<GeradorSinal>(),
            sp.GetRequiredService<Sessao>(),
            sp.GetRequiredService<IChaves>(),
            sp.GetRequiredService<EstadoAparelho>(),
            sp.GetRequiredService<CopiaAntes>(),
            sp.GetRequiredService<IDespachante>(),
            sp.GetRequiredService<TimeProvider>(),
            sp.GetRequiredService<GerenteMemorias>()));
        builder.Services.AddSingleton(sp => new MonitoracaoViewModel(
            sp.GetRequiredService<MonitorAparelho>(),
            sp.GetRequiredService<IDespachante>(),
            sp.GetRequiredService<TimeProvider>()));
        builder.Services.AddSingleton(sp => new AparelhoViewModel(
            sp.GetRequiredService<ComandosAparelho>(),
            sp.GetRequiredService<Sessao>(),
            sp.GetRequiredService<IChaves>(),
            sp.GetRequiredService<EstadoAparelho>(),
            sp.GetRequiredService<CopiaAntes>(),
            sp.GetRequiredService<IDespachante>(),
            sp.GetRequiredService<TimeProvider>(),
            sp.GetRequiredService<GerenteMemorias>(),
            sp.GetRequiredService<GeradorSinal>()));

        // História 10: a casca e as telas (Início, Mesa, Áudio, telas de função, Ajuste da saída, Setup em dúvida,
        // Configurações › Setups e › Memórias). Singletons: assinam o núcleo; as páginas também, uma de cada.
        builder.Services.AddSingleton<INavegacao, NavegacaoMaui>();
        builder.Services.AddSingleton<BotoesMemoriaViewModel>();
        builder.Services.AddSingleton<InicioViewModel>();
        builder.Services.AddSingleton<MesaViewModel>();
        builder.Services.AddSingleton<AudioViewModel>();
        builder.Services.AddSingleton<CascaViewModel>();
        builder.Services.AddSingleton<SetupsViewModel>();
        builder.Services.AddSingleton<ConfiguracoesViewModel>();
        builder.Services.AddSingleton<AjustesSaida>();
        builder.Services.AddSingleton<CascaPage>();
        builder.Services.AddSingleton<SetupEscolhaPage>();
        builder.Services.AddSingleton<PelaMetadePage>();
        builder.Services.AddSingleton<ConfiguracoesPage>();
        builder.Services.AddSingleton<SetupsPage>();
        builder.Services.AddSingleton<SetupPage>();
        builder.Services.AddSingleton<MemoriasPage>();
        builder.Services.AddSingleton<ConexaoPage>();

        var app = builder.Build();
        // Criados já: o espelho, a cópia antes e o gerador precisam ver a primeira leitura (a
        // retomada do "aplicado pela metade", o 202), o link se pendura no escritor, e as chaves,
        // a cópia e o link começam a ler o guardado.
        app.Services.GetRequiredService<EstadoAparelho>();
        app.Services.GetRequiredService<Escritor>();
        app.Services.GetRequiredService<LinkCanais>();
        app.Services.GetRequiredService<GerenteMemorias>(); // cria a CopiaAntes e registra as memórias nela
        // Os presets cedo: o tipo "aplicar-preset" precisa estar registrado na CopiaAntes antes da primeira leitura.
        app.Services.GetRequiredService<GerentePresets>();
        // Os Setups cedo (com os presets e as playlists): a conferência roda na primeira leitura de cada
        // conexão, os tipos "aplicar-memoria-celular", "aplicar-setup" e "manter-calibracao" ficam
        // registrados na CopiaAntes, e as playlists pendentes procuram a memória delas.
        app.Services.GetRequiredService<GerenteSetups>();
        // O gerador cedo: a regra 2 confere o 202 já na primeira leitura, e o Desconectar desliga antes de soltar.
        app.Services.GetRequiredService<GeradorSinal>();
        // A monitoração cedo: a primeira leitura semeia o voltímetro e o clip.
        app.Services.GetRequiredService<MonitorAparelho>();
        return app;
    }
}
