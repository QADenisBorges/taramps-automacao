using BgsControle.App.Plataforma;
using BgsControle.App.Recursos;
using BgsControle.App.Telas;
using BgsControle.Nucleo;
using BgsControle.Nucleo.Aparelho;
using BgsControle.Nucleo.Enlace;
using BgsControle.Nucleo.Escrita;
using BgsControle.Nucleo.Gerador;
using BgsControle.Nucleo.Leitura;
using BgsControle.Nucleo.Player;
using BgsControle.Nucleo.Porta;
using BgsControle.Nucleo.Presets;
using BgsControle.Nucleo.Registro;
using BgsControle.Nucleo.Rendimento;
using BgsControle.Nucleo.Seguranca;
using BgsControle.Nucleo.Setups;
using BgsControle.Nucleo.Uso;
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
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<IArquivosApp, ArquivosMaui>();
        // História 11 (as peças das histórias 9a e 9b): o histórico de cada comando (em historico.json, nos
        // arquivos privados; lido só pela porta) e o uso ao mesmo tempo com a porta, que a sessão consulta em
        // cada escrita. O toque da tela sempre pausa a porta; não há modo guardado (história 9c).
        builder.Services.AddSingleton(sp => new Historico(
            new RepositorioTextoArquivos(sp.GetRequiredService<IArquivosApp>(), RepositorioTextoArquivos.ArquivoHistorico),
            sp.GetRequiredService<TimeProvider>(),
            sp.GetRequiredService<LogBle>()));
        builder.Services.AddSingleton(sp => new UsoCompartilhado(
            sp.GetRequiredService<TimeProvider>(),
            sp.GetRequiredService<LogBle>()));
        builder.Services.AddSingleton(sp => new Sessao(
            sp.GetRequiredService<ITransporte>(),
            sp.GetRequiredService<ICofreSenha>(),
            sp.GetRequiredService<LogBle>(),
            chaves: sp.GetRequiredService<IChaves>(),
            historico: sp.GetRequiredService<Historico>(),
            uso: sp.GetRequiredService<UsoCompartilhado>()));
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
        builder.Services.AddSingleton<IDespachante, DespachanteMaui>();
        builder.Services.AddSingleton<IPreparoBluetooth, PreparoBluetoothAndroid>();
        // História 11: o som do celular (saída e acesso às músicas), o seletor de músicas e o Exportar dos presets.
        builder.Services.AddSingleton(_ => new AudioDoCelularAndroid(Android.App.Application.Context));
        builder.Services.AddSingleton<IAudioDoCelular>(sp => sp.GetRequiredService<AudioDoCelularAndroid>());
        builder.Services.AddSingleton<ISeletorAudio, SeletorMusicas>();
        builder.Services.AddSingleton<IExportarArquivo, ExportarMaui>();

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

        // História 8 e 8d: os presets de 15 bandas no celular (criar no Salvar, aplicar, renomear, apagar), com
        // o aplicar pela cópia antes, e as playlists por memória do celular (playlists.json). Antes dos
        // Setups, que os recebem. O app nasce zerado (CAP-6): sem Setup, sem modelo, sem memória e sem preset.
        builder.Services.AddSingleton(sp => new GerentePresets(
            sp.GetRequiredService<Sessao>(),
            sp.GetRequiredService<EstadoAparelho>(),
            sp.GetRequiredService<Escritor>(),
            sp.GetRequiredService<LeituraAutomatica>(),
            sp.GetRequiredService<CopiaAntes>(),
            new RepositorioTextoArquivos(sp.GetRequiredService<IArquivosApp>(), RepositorioTextoArquivos.ArquivoPresets),
            sp.GetRequiredService<LogBle>()));
        builder.Services.AddSingleton(sp =>
        {
            var playlists = new GerentePlaylists(
                new RepositorioTextoArquivos(sp.GetRequiredService<IArquivosApp>(), RepositorioTextoArquivos.ArquivoPlaylists),
                sp.GetRequiredService<LogBle>());
            // História 11: a música que sai de todas as playlists solta a permissão da URI no Android.
            playlists.Soltas += SeletorMusicas.Soltar;
            return playlists;
        });

        // História 8f: a base de modelos (a ficha confirmada de cada modelo), em modelos.json.
        builder.Services.AddSingleton(sp => new BaseModelos(
            new RepositorioTextoArquivos(sp.GetRequiredService<IArquivosApp>(), RepositorioTextoArquivos.ArquivoModelos),
            sp.GetRequiredService<LogBle>()));

        // Histórias 8c, 8d e 8f: os Setups (só o físico) e as memórias do celular (toda a regulagem;
        // conferência ao conectar, aplicar com o preset lembrado, Salvar por camadas), em setups.json, e a
        // marca do mudo da dúvida em setup-mudo.json.
        builder.Services.AddSingleton(sp => new GerenteSetups(
            sp.GetRequiredService<Sessao>(),
            sp.GetRequiredService<EstadoAparelho>(),
            sp.GetRequiredService<Escritor>(),
            sp.GetRequiredService<LeituraAutomatica>(),
            sp.GetRequiredService<CopiaAntes>(),
            sp.GetRequiredService<LinkCanais>(),
            sp.GetRequiredService<GerentePresets>(),
            sp.GetRequiredService<GerentePlaylists>(),
            sp.GetRequiredService<IChaves>(),
            new RepositorioTextoArquivos(sp.GetRequiredService<IArquivosApp>(), RepositorioTextoArquivos.ArquivoSetups),
            new RepositorioTextoArquivos(sp.GetRequiredService<IArquivosApp>(), RepositorioTextoArquivos.ArquivoSetupMudo),
            sp.GetRequiredService<LogBle>(),
            modelos: sp.GetRequiredService<BaseModelos>(),
            // História 13c: a cópia do setups.json formato 3, guardada antes da migração ao 4.
            repositorioFormato3: new RepositorioTextoArquivos(sp.GetRequiredService<IArquivosApp>(), RepositorioTextoArquivos.ArquivoSetupsFormato3)));

        // História 13c: a conta do nível refeita na hora (o master da memória em uso num nível acompanha a cadeia, a
        // ficha, a calibração e o preset).
        builder.Services.AddSingleton(sp => new GerenteNiveis(
            sp.GetRequiredService<EstadoAparelho>(),
            sp.GetRequiredService<Escritor>(),
            sp.GetRequiredService<GerenteSetups>(),
            sp.GetRequiredService<GerentePresets>(),
            sp.GetRequiredService<CopiaAntes>(),
            sp.GetRequiredService<LogBle>()));

        // História 7a: o gerador de sinal, com as regras de falha 1 e 2 (desliga no prazo, no
        // Parar, antes de soltar a conexão, ao sair do app e no fim de cada leitura).
        builder.Services.AddSingleton(sp => new GeradorSinal(
            sp.GetRequiredService<Sessao>(),
            sp.GetRequiredService<EstadoAparelho>(),
            sp.GetRequiredService<LeituraAutomatica>(),
            sp.GetRequiredService<CopiaAntes>(),
            sp.GetRequiredService<LogBle>()));

        // História 11: o reprodutor real (o MediaPlayer do Android) e o Player, que toca a playlist da memória do
        // celular em uso (e segue tocando na queda do BLE, decisão do Denis de 24/10/2026).
        builder.Services.AddSingleton(sp => new ReprodutorAndroid(
            Android.App.Application.Context,
            sp.GetRequiredService<AudioDoCelularAndroid>()));
        builder.Services.AddSingleton<IReprodutor>(sp => sp.GetRequiredService<ReprodutorAndroid>());
        // História 13d: a análise da playlist (o PCM pelo MediaExtractor + MediaCodec, em segundo plano) e o teto do
        // Extra, com os resultados em analises.json. Uma faixa em análise não toca.
        builder.Services.AddSingleton<IDecodificadorAudio>(_ => new DecodificadorAndroid(Android.App.Application.Context));
        builder.Services.AddSingleton(sp => new AnalisePlaylist(
            sp.GetRequiredService<GerenteSetups>(),
            sp.GetRequiredService<GerentePresets>(),
            sp.GetRequiredService<GerentePlaylists>(),
            sp.GetRequiredService<IDecodificadorAudio>(),
            new RepositorioTextoArquivos(sp.GetRequiredService<IArquivosApp>(), RepositorioTextoArquivos.ArquivoAnalises),
            sp.GetRequiredService<LogBle>()));
        builder.Services.AddSingleton(sp =>
        {
            var player = new Player(
                sp.GetRequiredService<GerentePlaylists>(),
                sp.GetRequiredService<GerenteSetups>(),
                sp.GetRequiredService<IReprodutor>(),
                sp.GetRequiredService<LogBle>());
            player.EmAnalise = sp.GetRequiredService<AnalisePlaylist>().EmAnalise;
            return player;
        });

        // História 13b: a calibração guiada da saída (o ganho do módulo pelo gerador e o multímetro, o nível do
        // Bluetooth pelo tom de 0 dBFS do celular, num AudioTrack). O tipo "calibrar-saida" fica registrado na
        // CopiaAntes quando ela nasce (cedo, abaixo).
        builder.Services.AddSingleton(_ => new TomCelularAndroid(Android.App.Application.Context));
        builder.Services.AddSingleton<ITomCelular>(sp => sp.GetRequiredService<TomCelularAndroid>());
        builder.Services.AddSingleton(sp => new Calibracao(
            sp.GetRequiredService<Sessao>(),
            sp.GetRequiredService<EstadoAparelho>(),
            sp.GetRequiredService<Escritor>(),
            sp.GetRequiredService<CopiaAntes>(),
            sp.GetRequiredService<GerenteSetups>(),
            sp.GetRequiredService<GeradorSinal>(),
            sp.GetRequiredService<IChaves>(),
            sp.GetRequiredService<ITomCelular>(),
            sp.GetRequiredService<LogBle>(),
            sp.GetRequiredService<Player>(),
            sp.GetRequiredService<TimeProvider>()));

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
            gerador: sp.GetRequiredService<GeradorSinal>()));

        // História 13e: o teste a seco do Extra (o módulo desligado, os trechos fortes e o 150) e o fiscal no uso do Extra
        // (o clip, o outro player e o volume do celular). O tipo "teste-seco" fica registrado na CopiaAntes quando ele
        // nasce (cedo, abaixo).
        builder.Services.AddSingleton<IOutroPlayer>(sp => new OutroPlayerAndroid(
            Android.App.Application.Context,
            sp.GetRequiredService<ReprodutorAndroid>(),
            sp.GetRequiredService<TomCelularAndroid>()));
        builder.Services.AddSingleton<IVolumeCelular>(_ => new VolumeCelularAndroid(Android.App.Application.Context));
        builder.Services.AddSingleton(sp => new TesteSeco(
            sp.GetRequiredService<Sessao>(),
            sp.GetRequiredService<EstadoAparelho>(),
            sp.GetRequiredService<Escritor>(),
            sp.GetRequiredService<CopiaAntes>(),
            sp.GetRequiredService<GerenteSetups>(),
            sp.GetRequiredService<AnalisePlaylist>(),
            sp.GetRequiredService<ComandosAparelho>(),
            sp.GetRequiredService<MonitorAparelho>(),
            sp.GetRequiredService<IReprodutor>(),
            sp.GetRequiredService<IChaves>(),
            sp.GetRequiredService<LogBle>(),
            sp.GetRequiredService<Player>(),
            sp.GetRequiredService<GeradorSinal>(),
            sp.GetRequiredService<TimeProvider>(),
            volume: sp.GetRequiredService<IVolumeCelular>()));
        builder.Services.AddSingleton(sp => new FiscalExtra(
            sp.GetRequiredService<Sessao>(),
            sp.GetRequiredService<EstadoAparelho>(),
            sp.GetRequiredService<Escritor>(),
            sp.GetRequiredService<CopiaAntes>(),
            sp.GetRequiredService<GerenteSetups>(),
            sp.GetRequiredService<AnalisePlaylist>(),
            sp.GetRequiredService<Player>(),
            sp.GetRequiredService<IReprodutor>(),
            sp.GetRequiredService<MonitorAparelho>(),
            sp.GetRequiredService<IOutroPlayer>(),
            sp.GetRequiredService<IVolumeCelular>(),
            sp.GetRequiredService<LogBle>(),
            sp.GetRequiredService<TimeProvider>(),
            sp.GetRequiredService<TesteSeco>(),
            new RepositorioTextoArquivos(sp.GetRequiredService<IArquivosApp>(), RepositorioTextoArquivos.ArquivoFiscalExtra)));
        // História 13f: o modelo térmico do Extra (o tempo de Extra, o corte e o descanso com o Bluetooth travado), em
        // termico.json. O descanso impede o teste a seco (sem o Bluetooth, nenhum clip acusaria nada).
        builder.Services.AddSingleton(sp =>
        {
            var termico = new TermicoExtra(
                sp.GetRequiredService<Sessao>(),
                sp.GetRequiredService<EstadoAparelho>(),
                sp.GetRequiredService<Escritor>(),
                sp.GetRequiredService<GerenteSetups>(),
                sp.GetRequiredService<AnalisePlaylist>(),
                sp.GetRequiredService<Player>(),
                sp.GetRequiredService<ComandosAparelho>(),
                sp.GetRequiredService<LogBle>(),
                sp.GetRequiredService<TimeProvider>(),
                new RepositorioTextoArquivos(sp.GetRequiredService<IArquivosApp>(), RepositorioTextoArquivos.ArquivoTermico));
            sp.GetRequiredService<TesteSeco>().Impedimento = () => termico.TextoDescanso;
            return termico;
        });

        builder.Services.AddSingleton<ConexaoViewModel>();
        // Os ViewModels dos ajustes, das chaves, das memórias, do link, dos presets, do gerador, da monitoração e
        // do aparelho (sem tela nova: o XAML é das histórias 10 e 11). Singletons: assinam eventos do núcleo; transitórios no
        // contêiner raiz vazariam a cada resolução.
        builder.Services.AddSingleton<FabricaControles>();
        builder.Services.AddSingleton<ChavesViewModel>();
        builder.Services.AddSingleton<SalvarViewModel>();
        builder.Services.AddSingleton<SetupViewModel>();
        builder.Services.AddSingleton<EquipamentosViewModel>(); // história 8e (a tela Setup é da história 10)
        builder.Services.AddSingleton<EsquemaViewModel>(); // história 10b: o esquema do Setup mostrado pela lista
        builder.Services.AddSingleton<MemoriasViewModel>();
        builder.Services.AddSingleton<LinkViewModel>();
        builder.Services.AddSingleton<PresetsViewModel>();
        builder.Services.AddSingleton(sp => new GeradorViewModel(
            sp.GetRequiredService<GeradorSinal>(),
            sp.GetRequiredService<Sessao>(),
            sp.GetRequiredService<IChaves>(),
            sp.GetRequiredService<EstadoAparelho>(),
            sp.GetRequiredService<CopiaAntes>(),
            sp.GetRequiredService<IDespachante>(),
            sp.GetRequiredService<TimeProvider>()));
        // História 13b: a calibração guiada, um passo por tela (a CalibracaoPage e a TesteSecoPage, da 13g).
        builder.Services.AddSingleton(sp => new CalibracaoViewModel(
            sp.GetRequiredService<Calibracao>(),
            sp.GetRequiredService<GeradorSinal>(),
            sp.GetRequiredService<GerenteSetups>(),
            sp.GetRequiredService<Sessao>(),
            sp.GetRequiredService<IChaves>(),
            sp.GetRequiredService<EstadoAparelho>(),
            sp.GetRequiredService<CopiaAntes>(),
            sp.GetRequiredService<IAudioDoCelular>(),
            sp.GetRequiredService<IDespachante>(),
            sp.GetRequiredService<TimeProvider>()));
        // História 13c: a folha "Nível da memória" (a NivelFolhaView, da 13g).
        builder.Services.AddSingleton(sp => new NivelViewModel(
            sp.GetRequiredService<GerenteSetups>(),
            sp.GetRequiredService<EstadoAparelho>(),
            sp.GetRequiredService<IDespachante>(),
            sp.GetRequiredService<TimeProvider>(),
            sp.GetRequiredService<AnalisePlaylist>(),
            sp.GetRequiredService<LogBle>(),
            // História 13g: o "Calibrar ›" da faixa de sem calibração.
            sp.GetRequiredService<INavegacao>()));
        // História 13e: o teste a seco, um passo por tela (a CalibracaoPage e a TesteSecoPage, da 13g).
        builder.Services.AddSingleton(sp => new TesteSecoViewModel(
            sp.GetRequiredService<TesteSeco>(),
            sp.GetRequiredService<GerenteSetups>(),
            sp.GetRequiredService<AnalisePlaylist>(),
            sp.GetRequiredService<Sessao>(),
            sp.GetRequiredService<CopiaAntes>(),
            sp.GetRequiredService<EstadoAparelho>(),
            sp.GetRequiredService<IDespachante>(),
            sp.GetRequiredService<TimeProvider>()));
        // História 13f: o Extra ao vivo (o tempo de Extra e o descanso; a ExtraAoVivoView, da 13g).
        builder.Services.AddSingleton(sp => new ExtraAoVivoViewModel(
            sp.GetRequiredService<TermicoExtra>(),
            sp.GetRequiredService<IDespachante>(),
            sp.GetRequiredService<TimeProvider>()));
        // História 13d: "Otimizar configuração na playlist", no Player (o XAML, da 13g).
        builder.Services.AddSingleton(sp => new OtimizarViewModel(
            sp.GetRequiredService<AnalisePlaylist>(),
            sp.GetRequiredService<GerenteSetups>(),
            sp.GetRequiredService<Player>(),
            sp.GetRequiredService<IDespachante>(),
            sp.GetRequiredService<TimeProvider>(),
            // História 13g: o "Fazer o teste a seco ›".
            sp.GetRequiredService<INavegacao>()));
        builder.Services.AddSingleton(sp => new MonitoracaoViewModel(
            sp.GetRequiredService<MonitorAparelho>(),
            sp.GetRequiredService<IDespachante>(),
            sp.GetRequiredService<TimeProvider>(),
            sp.GetRequiredService<GerenteSetups>(),
            sp.GetRequiredService<INavegacao>()));
        builder.Services.AddSingleton(sp => new AparelhoViewModel(
            sp.GetRequiredService<ComandosAparelho>(),
            sp.GetRequiredService<Sessao>(),
            sp.GetRequiredService<IChaves>(),
            sp.GetRequiredService<EstadoAparelho>(),
            sp.GetRequiredService<CopiaAntes>(),
            sp.GetRequiredService<IDespachante>(),
            sp.GetRequiredService<TimeProvider>(),
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

        // História 11: a porta de comandos (CAP-12; escuta na 8787 só com a chave "Controle pela rede"), o
        // pareamento em porta-ia.json e a folha de risco dos pedidos de risco.
        builder.Services.AddSingleton(sp => new Pareamento(
            new RepositorioTextoArquivos(sp.GetRequiredService<IArquivosApp>(), RepositorioTextoArquivos.ArquivoPortaIa),
            sp.GetRequiredService<TimeProvider>(),
            sp.GetRequiredService<LogBle>()));
        // História 9d: a criação da memória (CAP-18), nos dois sentidos pela porta; o tipo "criar-memoria" fica
        // registrado na CopiaAntes quando ela nasce (cedo, abaixo).
        builder.Services.AddSingleton(sp => new CriacaoMemoria(
            sp.GetRequiredService<Sessao>(),
            sp.GetRequiredService<EstadoAparelho>(),
            sp.GetRequiredService<Escritor>(),
            sp.GetRequiredService<CopiaAntes>(),
            sp.GetRequiredService<GerenteSetups>(),
            sp.GetRequiredService<UsoCompartilhado>(),
            sp.GetRequiredService<LogBle>()));
        builder.Services.AddSingleton(sp => new PedidosConfirmacao(sp.GetRequiredService<TimeProvider>(), sp.GetRequiredService<LogBle>()));
        // História 14b: a proposta de ficha (o pedido de um modelo fora da base, com o prazo de 60 s, e a folha que só o toque salva).
        builder.Services.AddSingleton(sp => new PropostaFicha(
            sp.GetRequiredService<GerenteSetups>(),
            sp.GetRequiredService<LogBle>(),
            sp.GetRequiredService<TimeProvider>()));
        builder.Services.AddSingleton(sp => new PortaComandos(
            sp.GetRequiredService<Sessao>(),
            sp.GetRequiredService<IChaves>(),
            sp.GetRequiredService<UsoCompartilhado>(),
            sp.GetRequiredService<Historico>(),
            sp.GetRequiredService<Pareamento>(),
            sp.GetRequiredService<PedidosConfirmacao>(),
            new RedeLocal(),
            sp.GetRequiredService<EstadoAparelho>(),
            sp.GetRequiredService<Escritor>(),
            sp.GetRequiredService<GerenteSetups>(),
            sp.GetRequiredService<GeradorSinal>(),
            sp.GetRequiredService<ComandosAparelho>(),
            sp.GetRequiredService<MonitorAparelho>(),
            sp.GetRequiredService<LogBle>(),
            sp.GetRequiredService<TimeProvider>(),
            criacao: sp.GetRequiredService<CriacaoMemoria>(),
            analise: sp.GetRequiredService<AnalisePlaylist>(),
            termico: sp.GetRequiredService<TermicoExtra>(),
            ficha: sp.GetRequiredService<PropostaFicha>()));

        // História 11: os ViewModels que só faltavam ligar e as páginas novas. Singletons, como os da história 10.
        // História 9c: a chave "Controle pela rede" (no grupo APARELHO das Configurações), a folha de risco do
        // app e a tela toda "REAPLICANDO"; a IA ativa, a tela IA pela rede e o Histórico saíram.
        builder.Services.AddSingleton<PlayerViewModel>();
        builder.Services.AddSingleton(sp => new ControlePelaRedeViewModel(
            sp.GetRequiredService<PortaComandos>(),
            sp.GetRequiredService<ChavesViewModel>(),
            sp.GetRequiredService<IDespachante>(),
            sp.GetRequiredService<TimeProvider>()));
        builder.Services.AddSingleton(sp => new FolhaRiscoViewModel(
            sp.GetRequiredService<PedidosConfirmacao>(),
            sp.GetRequiredService<IDespachante>(),
            sp.GetRequiredService<TimeProvider>()));
        // História 10d: as preferências do app (a chave "Animação ao criar o som", em preferencias-app.json) e a
        // criação da memória em tela toda, com o esquema do Setup, a pausa da folha de risco e a animação.
        builder.Services.AddSingleton(sp => new PreferenciasApp(
            new RepositorioTextoArquivos(sp.GetRequiredService<IArquivosApp>(), RepositorioTextoArquivos.ArquivoPreferencias)));
        builder.Services.AddSingleton(sp => new CriacaoMemoriaViewModel(
            sp.GetRequiredService<CriacaoMemoria>(),
            sp.GetRequiredService<IDespachante>(),
            sp.GetRequiredService<TimeProvider>(),
            sp.GetRequiredService<GerenteSetups>(),
            sp.GetRequiredService<FolhaRiscoViewModel>(),
            sp.GetRequiredService<PreferenciasApp>()));
        // História 14b: a folha da proposta de ficha (a XAML é da 14c).
        builder.Services.AddSingleton(sp => new PropostaFichaViewModel(
            sp.GetRequiredService<PropostaFicha>(),
            sp.GetRequiredService<IDespachante>(),
            sp.GetRequiredService<TimeProvider>()));
        builder.Services.AddSingleton(sp => new ReaplicandoViewModel(
            sp.GetRequiredService<GerenteSetups>(),
            sp.GetRequiredService<IDespachante>(),
            sp.GetRequiredService<TimeProvider>()));
        builder.Services.AddSingleton<GeradorPage>();
        builder.Services.AddSingleton<PresetsPage>();
        builder.Services.AddSingleton<AparelhoPage>();
        builder.Services.AddSingleton<MonitoracaoPage>();
        builder.Services.AddSingleton<ChavesPage>();
        builder.Services.AddSingleton<FolhaRiscoPage>();
        builder.Services.AddSingleton<ReaplicandoPage>();
        builder.Services.AddSingleton<AlarmeGeradorPage>();
        builder.Services.AddSingleton<CriacaoPage>();
        // História 13g: as telas cheias do Rendimento (a calibração guiada e o teste a seco).
        builder.Services.AddSingleton<CalibracaoPage>();
        builder.Services.AddSingleton<TesteSecoPage>();
        // As folhas globais (camadas da navegação): a do nível, a do outro player e (história 14c) a da proposta de ficha.
        builder.Services.AddSingleton<NivelFolhaPage>();
        builder.Services.AddSingleton<OutroPlayerPage>();
        builder.Services.AddSingleton<PropostaFichaPage>();

        var app = builder.Build();
        // Uma falha num comando da tela não derruba o app, mas deixa a linha no registro do BLE.
        var logTela = app.Services.GetRequiredService<LogBle>();
        FalhasDeComando.Destino = e => logTela.Evento($"tela: um comando falhou ({e.GetType().Name}: {e.Message})");
#if DEBUG
        // Só no APK Debug (história 12a): o registro do BLE, já mascarado, também vai para
        // files/registro-ble.txt, para conferir no aparelho os acks e os atrasos pelo run-as.
        // Nunca vai ao logcat. Recomeça a cada abertura do app, para não crescer sem fim.
        var arquivoRegistro = Path.Combine(FileSystem.AppDataDirectory, "registro-ble.txt");
        var travaRegistro = new object();
        try
        {
            File.Delete(arquivoRegistro);
        }
        catch (Exception)
        {
            // Sem apagar, o arquivo só continua o da última vez.
        }
        logTela.Registrou += linha =>
        {
            try
            {
                lock (travaRegistro)
                    File.AppendAllText(arquivoRegistro, linha + "\n");
            }
            catch (Exception)
            {
                // O registro em arquivo é só para a conferência; uma falha nele não atrapalha o BLE.
            }
        };
#endif
        // Criados já: o espelho, a cópia antes e o gerador precisam ver a primeira leitura (a
        // retomada do "aplicado pela metade", o 202), o link se pendura no escritor, e as chaves,
        // a cópia e o link começam a ler o guardado.
        app.Services.GetRequiredService<EstadoAparelho>();
        app.Services.GetRequiredService<Escritor>();
        app.Services.GetRequiredService<LinkCanais>();
        app.Services.GetRequiredService<CopiaAntes>(); // a retomada do "aplicado pela metade" vê a primeira leitura
        // Os presets cedo: o tipo "aplicar-preset" precisa estar registrado na CopiaAntes antes da primeira leitura.
        app.Services.GetRequiredService<GerentePresets>();
        // Os Setups cedo (com os presets e as playlists): a conferência roda na primeira leitura de cada
        // conexão, os tipos "aplicar-memoria-celular", "aplicar-setup" e "manter-calibracao" ficam
        // registrados na CopiaAntes, e as playlists pendentes procuram a memória delas. História 13a: é ele que
        // liga a cerca do limitador no escritor (como a cópia antes liga o bloqueio), antes de qualquer escrita.
        app.Services.GetRequiredService<GerenteSetups>();
        // História 13c: a conta do nível cedo: ela acompanha o espelho desde a primeira leitura.
        app.Services.GetRequiredService<GerenteNiveis>();
        // História 9d: a criação da memória cedo: o tipo "criar-memoria" precisa estar registrado na CopiaAntes antes
        // da primeira leitura (uma criação interrompida volta como "aplicado pela metade").
        app.Services.GetRequiredService<CriacaoMemoria>();
        // O gerador cedo: a regra 2 confere o 202 já na primeira leitura, e o Desconectar desliga antes de soltar.
        app.Services.GetRequiredService<GeradorSinal>();
        // História 13b: a calibração cedo: o tipo "calibrar-saida" precisa estar registrado na CopiaAntes antes da
        // primeira leitura (uma calibração interrompida volta como "aplicado pela metade").
        app.Services.GetRequiredService<Calibracao>();
        // A monitoração cedo: a primeira leitura semeia o voltímetro e o clip.
        app.Services.GetRequiredService<MonitorAparelho>();
        // História 11: o Player cedo (acompanha a memória em uso desde a primeira conferência) e a porta de
        // comandos (abre sozinha se a chave "Controle pela rede" já estiver ligada).
        app.Services.GetRequiredService<Player>();
        app.Services.GetRequiredService<PortaComandos>();
        // História 13e: o teste a seco cedo (o tipo "teste-seco" na CopiaAntes antes da primeira leitura: uma queda no meio
        // volta como "aplicado pela metade") e o fiscal do Extra (ouve o clip, o outro player e o volume desde o começo).
        app.Services.GetRequiredService<TesteSeco>();
        app.Services.GetRequiredService<FiscalExtra>();
        // História 13f: o modelo térmico cedo (o tempo fechado entra como esfriamento, e o descanso continua pelo relógio).
        app.Services.GetRequiredService<TermicoExtra>();
        return app;
    }
}
