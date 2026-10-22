- source_spec: `_bmad-output/specs/spec-taramps-automacao/stories/1-mapa-do-protocolo-ble.md`
  summary: A heurística de polling do btsnoop_att.py (escrita idêntica repetida 5 vezes ou mais) pode tirar do cruzamento os passos de prova, se os botões +/− mandarem comandos relativos idênticos.
  evidence: Não verificado (medium). Quem resolve são as capturas C1/C2. Se o mesmo payload se repetir a cada toque de +/−, a detecção precisa olhar a periodicidade ou ignorar as escritas dentro das janelas de passo.

- source_spec: `_bmad-output/implementation-artifacts/spec-leitura-ble-marco-1.md`
  summary: O montador de respostas pode fundir um quadro sem separador final (ex.: `020#b#v`) com um quadro espontâneo (150/111), aceitar valor truncado quando um fragmento atrasa mais de 0,3 s, ou descartar como eco uma resposta sem canal igual à consulta.
  evidence: Não verificado (medium). Depende do formato e do ritmo reais das notificações; a primeira leitura com o processador (log bruto em estado/log-ble/) decide se precisa ajustar `_INICIO_QUADRO_RE`, o `OCIOSO_S` e a regra de eco.

- source_spec: `_bmad-output/implementation-artifacts/spec-escrita-ble-guardiao.md`
  summary: Os caminhos de saída 3 (sem conexão) e 5 (sem resposta à senha) do ajustar_processador.py não têm teste, e o transporte falso não simula aparelho ausente nem senha muda.
  evidence: Não verificado (medium). Esses caminhos terminam antes de qualquer escrita; um interruptor `encontrado`/`resposta_000` no falso resolve.

- source_spec: `_bmad-output/implementation-artifacts/spec-vitrine-bgs.md`
  summary: O comportamento da página (Vitrine mostrando o Modo, AudioContext fechado depois da abertura, sequência de fases, seletor) só é verificado por texto da fonte e pela checagem manual; falta um harness de testes de JS (ex.: Playwright).
  evidence: Não verificado (medium). Uma mutação em `modoAtual()` ou a remoção de `som.fechar()` passaria nos testes atuais.

- source_spec: `_bmad-output/specs/spec-app-controle-taramps/stories/2-catalogo-parametros-leitura-completa.md`
  summary: A leitura automática depois da senha, o "Ler de novo" e o JSON salvo (ConexaoViewModel) não têm teste automático.
  evidence: Não existe projeto de teste para o BgsControle.App (MAUI); tirar a linha `_ = LerAsync()` passaria em todos os testes. Mover a regra "ler uma vez ao entrar em Conectado" para o núcleo permitiria testá-la com o TransporteFalso.

- source_spec: `_bmad-output/specs/spec-app-controle-taramps/stories/4-infraestrutura-testes-processador-simulado.md`
  summary: O guarda do contador de leitura no ConexaoViewModel (salvamento antigo terminando durante uma leitura nova) não tem teste.
  evidence: ArquivosFalso grava na hora; cobrir pede um ArquivosFalso com porta. Vale quando a tela de leitura for refeita (histórias 10–11).

- source_spec: `_bmad-output/specs/spec-app-controle-taramps/stories/5-escrita-ajustes-chaves-acesso.md`
  summary: A montagem do DI no MauiProgram (Sessao com IChaves, EstadoAparelho e Escritor criados cedo) não tem teste; um erro ali deixaria o app sem escrever.
  evidence: O projeto do App é Android e nenhum teste o referencia. Fechar movendo os registros para um método de extensão testável, ou conferir no celular (abrir, conectar, mexer um ganho).

- source_spec: `_bmad-output/specs/spec-app-controle-taramps/stories/6-memorias-link-copia-antes.md`
  summary: A retomada "Adiada" do salvar (queda de novo durante a releitura depois de reconectar) não tem teste.
  evidence: Exige uma queda cronometrada depois da leitura completa e antes da releitura; os ganchos do simulador contam escritas. Mais raro que os casos cobertos.

- source_spec: `_bmad-output/specs/spec-app-controle-taramps/stories/4-infraestrutura-testes-processador-simulado.md`
  summary: Os atrasos realistas do simulador (senha ~23 ms, leitura ~25 ms) ficam zerados nos testes das histórias 5 e 6 (EscritaTestes, MemoriasTestes, ConexaoViewModelTestes e a Bancada de ControleParametroViewModelTestes); só o SimuladorTestes roda com eles.
  evidence: A sessão e o leitor esperam em tempo real, mas o relógio falso desses testes não anda durante a conexão e a leitura; com atraso no relógio falso a senha nunca responde. Ativar exige fazer o relógio andar nesses testes, o que deixa as suítes mais lentas e mexe em tempos que as histórias 5 e 6 conferem.

- source_spec: `_bmad-output/specs/spec-app-controle-taramps/stories/7a-gerador-regras-de-falha.md`
  summary: O desligar do gerador ao sair ou fechar o app (Window.Stopped e Destroying no App.xaml.cs) dispara o `*202*0*` sem esperar, e não tem teste no celular.
  evidence: O Android pode matar o processo antes da escrita BLE; esperar na thread da tela arrisca travar. A regra 2 desliga na próxima conexão. Conferir no Galaxy A52 na história 12 (tom tocando, fechar o app, ver o 202 relido).

- source_spec: `_bmad-output/specs/spec-app-controle-taramps/stories/7b-monitoracao-aparelho-inteiro.md`
  summary: Entre a resposta de um comando de aparelho inteiro (reset, senha, nome) e o começo da leitura nova, uma escrita de áudio, uma operação de memória ou o gerador ainda podem sair.
  evidence: O Escritor, o GerenteMemorias e o GeradorSinal não olham o `ComandosAparelho.Ocupado`; a porta é solta entre o ack e o `LerNovaAsync`. A janela é de milissegundos e exige um toque nesse instante. Fechar com um bloqueio comum (por exemplo, o `ComandosAparelho` entrar no `Escritor.Bloqueio` e no `MotivoParaNaoTocar` do gerador) quando a história 9a juntar o uso compartilhado.

- source_spec: `_bmad-output/specs/spec-app-controle-taramps/stories/7b-monitoracao-aparelho-inteiro.md`
  summary: O LogBle esconde cada senha conhecida em qualquer lugar da linha, e uma senha como 1000 ou 1234 apaga do log frequências e valores iguais (`*201*1000*`).
  evidence: `LogBle.Mascarar` troca toda ocorrência, sem olhar o quadro; já valia para a senha da conexão e agora vale também para as senhas da troca. Mascarar só dentro dos quadros 000 e 001, ou só o texto digitado, resolve.

- source_spec: `_bmad-output/specs/spec-app-controle-taramps/stories/8b-player-playlist-por-memoria.md`
  summary: O reprodutor e o seletor de áudio do Android (`ReprodutorAndroid` com MediaPlayer, `SeletorAudioAndroid` com ACTION_OPEN_DOCUMENT e TakePersistableUriPermission, duração pelo MediaMetadataRetriever) e o DI do Player no MauiProgram.
  evidence: Separado da 8b pelo tamanho da spec (~2.900 tokens); o stories.yaml deixa na 8b só o núcleo e o ViewModel. Entra com as telas na história 11. Não usar o FilePicker do MAUI, que copia o arquivo para o cache.

- source_spec: `_bmad-output/specs/spec-app-controle-taramps/stories/8b-player-playlist-por-memoria.md`
  summary: Música com o app em segundo plano, com tocar/pausar e próxima na notificação do Android (serviço de mídia, FOREGROUND_SERVICE_MEDIA_PLAYBACK, POST_NOTIFICATIONS).
  evidence: Decisão do Denis em 22/10 no plano da 8b: fica para a história 11 ou 12; na 8b a música toca só com o app aberto.

- source_spec: `_bmad-output/specs/spec-app-controle-taramps/stories/8b-player-playlist-por-memoria.md`
  summary: O contrato do IReprodutor não diz o que fazer quando o tocar lança exceção nem quando o som cai no meio da faixa (arquivo apagado, perda do foco de áudio, queda do Bluetooth); o Player ficaria Tocando sem som e sem frase na tela.
  evidence: Não verificado (maybe-false; seria medium). Hoje o TocarAsync que lança é engolido pelo Executar do Player (só log), e o IReprodutor só tem o evento Terminou. Quem resolve é o ReprodutorAndroid da história 11: nunca lançar (devolver false) e ganhar um evento de erro ou interrupção, com teste no Player.

- source_spec: `_bmad-output/specs/spec-app-controle-taramps/stories/8b-player-playlist-por-memoria.md`
  summary: O título "Playlist da Memória N" (memória em uso sem nome lido) não tem teste.
  evidence: Player.NomeMemoria cai em "Memória N" quando GerenteMemorias.Nome(n) vem nulo ou vazio; todos os testes usam memória com nome. Montar o caso pede preparo no simulador (o carregar relê os nomes); cabe na história 11, com a tela.

- source_spec: `_bmad-output/specs/spec-app-controle-taramps/stories/8b-player-playlist-por-memoria.md`
  summary: A limpeza da entrada no GerentePlaylists.AdicionarAsync (URI em branco descartada, duração negativa vira zero, nada válido não grava) não tem teste.
  evidence: Sem o filtro, uma faixa de URI em branco aparece na sessão e some ao reabrir (o DeJson a descarta). Só importa quando o seletor real do Android existir (história 11); um fato curto em PlaylistsTestes fecha.
