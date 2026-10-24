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
  status: conferido em parte no A52 na história 12a (24/10). Abrir, conectar, ler 84 de 84, carregar as memórias 1 e 3 do aparelho e aplicar a FILME do celular (31 escritas com eco) funcionaram. Falta mexer um ganho pela tela, na 12b.

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
  status: feito na 11. `Platforms/Android/ReprodutorAndroid.cs` (MediaPlayer, wake lock parcial) e `SeletorMusicas.cs` (ACTION_OPEN_DOCUMENT, `audio/*`, vários, TakePersistableUriPermission, nome, pasta e duração); o `Player` e o `PlayerViewModel` registrados no MauiProgram. Sem celular no adb: o áudio real não foi ouvido (história 12).

- source_spec: `_bmad-output/specs/spec-app-controle-taramps/stories/8b-player-playlist-por-memoria.md`
  summary: Música com o app em segundo plano, com tocar/pausar e próxima na notificação do Android (serviço de mídia, FOREGROUND_SERVICE_MEDIA_PLAYBACK, POST_NOTIFICATIONS).
  evidence: Decisão do Denis em 22/10 no plano da 8b: fica para a história 11 ou 12; na 8b a música toca só com o app aberto.
  status (história 11): a música segue com o app em segundo plano enquanto o Android deixar (sem serviço). A notificação de mídia e o serviço em primeiro plano continuam para depois (Never da spec da 11).

- source_spec: `_bmad-output/specs/spec-app-controle-taramps/stories/8b-player-playlist-por-memoria.md`
  summary: O contrato do IReprodutor não diz o que fazer quando o tocar lança exceção nem quando o som cai no meio da faixa (arquivo apagado, perda do foco de áudio, queda do Bluetooth); o Player ficaria Tocando sem som e sem frase na tela.
  evidence: Não verificado (maybe-false; seria medium). Hoje o TocarAsync que lança é engolido pelo Executar do Player (só log), e o IReprodutor só tem o evento Terminou. Quem resolve é o ReprodutorAndroid da história 11: nunca lançar (devolver false) e ganhar um evento de erro ou interrupção, com teste no Player.
  status (história 11): em parte. O `ReprodutorAndroid` nunca lança no TocarAsync (devolve false; a SecurityException vira o aviso "Sem permissão"). O evento de erro ou interrupção no meio da faixa continua aberto.

- source_spec: `_bmad-output/specs/spec-app-controle-taramps/stories/8b-player-playlist-por-memoria.md`
  summary: O título "Playlist da Memória N" (memória em uso sem nome lido) não tem teste.
  evidence: Player.NomeMemoria cai em "Memória N" quando GerenteMemorias.Nome(n) vem nulo ou vazio; todos os testes usam memória com nome. Montar o caso pede preparo no simulador (o carregar relê os nomes); cabe na história 11, com a tela.

- source_spec: `_bmad-output/specs/spec-app-controle-taramps/stories/8b-player-playlist-por-memoria.md`
  summary: A limpeza da entrada no GerentePlaylists.AdicionarAsync (URI em branco descartada, duração negativa vira zero, nada válido não grava) não tem teste.
  evidence: Sem o filtro, uma faixa de URI em branco aparece na sessão e some ao reabrir (o DeJson a descarta). Só importa quando o seletor real do Android existir (história 11); um fato curto em PlaylistsTestes fecha.

- source_spec: `_bmad-output/specs/spec-app-controle-taramps/stories/9a-historico-estado-ao-vivo-uso-simultaneo.md`
  summary: A origem ambiente (AsyncLocal) pode vazar para trabalho em segundo plano iniciado num fluxo da IA (timers, Task.Run, handlers de EstadoAparelho.Mudou e de Sessao), fazendo uma escrita posterior sair como IA (recusada na pausa ou depois do PARAR IA) ou como App (passando sem o uso compartilhado).
  evidence: Não verificado (maybe-false, seria medium). Hoje só o gerador e o Escritor escrevem nesses fluxos, e os dois põem a origem certa. Resolve: na 9b, auditar cada handler que escreve e as entradas da porta HTTP, e pôr `ExecutionContext.SuppressFlow` ou `Origem.Como(...)` onde o trabalho em segundo plano nasce; um teste com um handler que escreve.
  status: feito na 9b (entradas da porta). O `ServidorHttp` aceita cada conexão com `ExecutionContext.SuppressFlow` (`ServidorHttp.SemFluxo`), o `PortaComandos` abre e fecha pela chave do mesmo jeito, e cada comando roda em `Origem.Como(OrigemComando.Ia)`. Testes em `PortaTestes`: um handler que escreve (a origem é IA e não volta para quem chamou) e o HTTP no loopback com a porta aberta dentro de um escopo App (a porta vê a origem padrão). Um handler de terceiros que faz `Task.Run` dentro de um fluxo da IA ainda herda a origem: é do AsyncLocal, e nenhum handler do app faz isso hoje.

- source_spec: `_bmad-output/specs/spec-app-controle-taramps/stories/9b-porta-de-comandos-da-ia.md`
  summary: O MauiProgram ainda não registra a porta da IA: `Pareamento` (porta-ia.json, `RepositorioTextoArquivos.ArquivoPortaIa`), `PedidosConfirmacao`, `RedeLocal` como `IRedeLocal`, `PortaComandos` (porta 8787, criada cedo, depois das chaves, do uso e do histórico), `IaPelaRedeViewModel` e `ConfirmacaoIaViewModel` (a folha sobre qualquer tela).
  evidence: A spec da 9b deixa XAML, navegação e MauiProgram para a história 11 (Never). Até lá, o app real não abre a porta mesmo com a chave "IA pela rede" ligada. O manifesto já tem INTERNET e ACCESS_NETWORK_STATE.
  status: feito na 11. O MauiProgram registra o `Pareamento`, os `PedidosConfirmacao`, a `RedeLocal`, a `PortaComandos` (criada cedo) e os ViewModels da IA; a folha de confirmação e a IA ativa sobem por cima de tudo.

- source_spec: `_bmad-output/specs/spec-app-controle-taramps/stories/9a-historico-estado-ao-vivo-uso-simultaneo.md`
  summary: O MauiProgram ainda não cria o Historico (historico.json) nem o UsoCompartilhado (uso-ia.json), não os passa à Sessao, e ninguém chama Historico.GravarAgoraAsync ao sair do app.
  evidence: A spec da 9a deixa o MauiProgram para a história 11 (Never). Até lá, o app real não tem histórico nem trava, e uma entrada dos últimos 2 s se perde ao fechar o app.
  status: feito na 11. O `Historico` e o `UsoCompartilhado` vão para a `Sessao`; o `App` chama `Historico.GravarAgoraAsync` ao ir para o segundo plano e ao fechar.

- source_spec: `_bmad-output/specs/spec-app-controle-taramps/stories/8c-setup-e-memoria-no-celular.md`
  summary: Com o link 1–2 ligado e um equilíbrio L/R diferente de 0, o link copia o valor final da saída 1 (mestre) para a saída 2 e perde o equilíbrio na outra saída (o ganho e o limiter da JBL R ficam iguais aos da JBL L, sem os −3 dB do Casa 2.1).
  evidence: O `LinkCanais` repete na outra saída o valor que o aparelho confirmou na mestre (`Escritor.EspelharAsync`), sem saber do equilíbrio do Setup; o `GerenteSetups.AjustarEquilibrioAsync` escreve pelo caminho público (com o link). Conferir na história 10, junto do Ajuste da JBL L e da JBL R e do link 1–2 nas abas: somar o equilíbrio da outra saída na cópia do link, ou não copiar o 081 e o 071 com equilíbrio.
  status: em parte na 8e. O equilíbrio passou a ser por canal; um par com link é uma saída só, e mexer no equilíbrio dela põe o mesmo valor nos dois canais (o link repete o final certo). Fica aberto um caso: ligar o link num par que já tem equilíbrios diferentes guardados (o Link 1–2 no Casa 2.1, com a R em −3) copia o final da mestre e o CH2 continua com −3 guardado; a próxima conferência acusa "som diferente". Decidir na história 10 (igualar o equilíbrio do par ao ligar o link, ou avisar).
  status (história 10): a spec da 10 não pediu essa decisão; as telas mostram o par como uma saída só, com o equilíbrio do mestre. O caso do link ligado num par com equilíbrios diferentes continua aberto para o Denis.

- source_spec: `_bmad-output/specs/spec-app-controle-taramps/stories/8d-preset-15-bandas-e-playlist-da-memoria.md`
  summary: Desconectado, o Player fica sem playlist e para, mas o EXPERIENCE.md diz que ele continua tocando, porque a música vai pelo Bluetooth de áudio e não pelo BLE.
  evidence: A queda limpa o som em uso (8c), e o Player seguia o som em uso.
  status: resolvido na história 11, pela decisão do Denis de 24/10/2026. Na queda do BLE o Player continua tocando a playlist da memória do celular que estava em uso (`Player.Retida`, marcada já no aviso da queda). Ele fica com ela durante a conferência, com "som diferente" e na dúvida; uma mudança do som em uso depois de reconectar a solta (a mesma memória segue; outra memória do celular, uma memória do aparelho carregada, Seguir sem Setup ou um Setup sem memórias param e trocam). Testes em `SetupsDecisoes2410Testes`.

- source_spec: `_bmad-output/specs/spec-app-controle-taramps/stories/8e-equipamentos-ligacoes-e-saidas-do-setup.md`
  summary: Na dúvida, em "Seguir sem Setup" e ao criar um Setup novo, o link que vale é o do Setup anterior.
  evidence: Numa instalação desconhecida, o CH3 podia continuar espelhado no CH4.
  status: resolvido na história 11, pela decisão do Denis de 24/10/2026. Sem Setup em uso, os links 1–2 e 3–4 ficam desligados (na dúvida, também a interrompida, ao trocar de Setup e em Seguir sem Setup); desistir da troca devolve os links do Setup em uso; um Setup criado do aparelho nasce com os links desligados; escolher um Setup aplica os links dele. Uma falha ao guardar os links vai para a frase da operação ("Não consegui desligar os links: …"). Testes em `SetupsDecisoes2410Testes` e `TelasParte2Testes`.

- source_spec: `_bmad-output/specs/spec-app-controle-taramps/stories/12a-instalar-ler-paridade-e-suposicoes-do-protocolo.md`
  summary: Telas e aparelho inteiro no aparelho real (história 12b do stories.yaml).
  evidence: A spec da antiga história 12 passava muito do tamanho, e o Denis decidiu dividi-la em 12a, 12b e 12c em 24/10/2026.

- source_spec: `_bmad-output/specs/spec-app-controle-taramps/stories/12a-instalar-ler-paridade-e-suposicoes-do-protocolo.md`
  summary: Porta da IA no Wi-Fi real e os testes 1, 2 e 4 do sinal de sucesso (história 12c do stories.yaml).
  evidence: A spec da antiga história 12 passava muito do tamanho, e o Denis decidiu dividi-la em 12a, 12b e 12c em 24/10/2026.

- source_spec: `_bmad-output/specs/spec-app-controle-taramps/stories/12a-instalar-ler-paridade-e-suposicoes-do-protocolo.md`
  summary: O voltímetro não chega ao app. O `#005#` responde `ERRO 0`, mas o app da Taramps mostra 13,8 V a 13,9 V na mesma hora.
  evidence: Medido em 24/10 (leituras e sonda). O app da Taramps recebe o valor de outro jeito: espontâneo (`#005#v#`, VOLTIMETRO-ESPONTANEO) ou por outra consulta. Conferir na 12b, com o VC830L e o log do BLE.

- source_spec: `_bmad-output/specs/spec-app-controle-taramps/stories/12a-instalar-ler-paridade-e-suposicoes-do-protocolo.md`
  summary: Não se sabe se uma memória do aparelho guarda o 202 (gerador ligado) e o 210–213 (sweep). Se guardar o 202, carregar uma memória salva com o tom tocando ligaria o gerador.
  evidence: Carregar a SPL trocou o 200 e o 201 (CONTEUDO-MEMORIA caiu), mas as duas memórias tinham o 202 em 0 e o 210–213 iguais. Conferir na 12b, com o RCA dos módulos desconectado, e ver se a regra 2 desliga o gerador depois de carregar.

- source_spec: `_bmad-output/specs/spec-app-controle-taramps/stories/12a-instalar-ler-paridade-e-suposicoes-do-protocolo.md`
  summary: A memória 1 do aparelho se chama SPL (salva em 20/10), mas o conhecimento herdado (conhecimento-herdado.md e ConhecimentoHerdado.cs) ainda a descreve como MUSICA HIGH.
  evidence: A leitura de 24/10 deu `231#SPL`, e carregar o `n` 0 trouxe a SPL (JBL com passa-alta em 200 Hz). Atualizar a nota da memória 1 é decisão do Denis.
  status: resolvido em 24/10, por decisão do Denis ("memória 1 é SPL"). A nota herdada da memória 1 passou a ser a SPL, com os valores da seção 12.8 de memoria-spl.md e da leitura de 24/10, em conhecimento-herdado.md e em ConhecimentoHerdado.cs.

- source_spec: `_bmad-output/specs/spec-app-controle-taramps/stories/12a-instalar-ler-paridade-e-suposicoes-do-protocolo.md`
  summary: Antes de conectar, o Início mostra "Sem Setup", e só depois da conexão aparece o Casa 2.1.
  evidence: Visto no A52 em 24/10. Conferir na 12b contra o EXPERIENCE.md, para ver se o chip deveria mostrar o último Setup usado já ao abrir.

- source_spec: `_bmad-output/specs/spec-app-controle-taramps/stories/12a-instalar-ler-paridade-e-suposicoes-do-protocolo.md`
  summary: O "Voltar à cópia" de um carregar de memória que caiu pela metade não volta o 200 e o 201 do gerador, que o carregar troca (CONTEUDO-MEMORIA caiu).
  evidence: `CopiaAntes.Diferencas` (`app-controle/src/BgsControle.Nucleo/Escrita/CopiaAntes.cs:423`) percorre só o `Catalogo.ConsultasMarco1`. O efeito no som é pequeno, porque o gerador escreve o 201 e o 200 antes de cada `*202*1*`, mas a tela do gerador fica com os valores da memória. Decidir se o gerador entra na cópia antes.

- source_spec: `_bmad-output/specs/spec-app-controle-taramps/stories/12a-instalar-ler-paridade-e-suposicoes-do-protocolo.md`
  summary: Carregar uma memória do aparelho com o tom tocando troca o ganho do tom na hora (ex.: de −45 para −25 dB, +20 dB) antes da releitura e da regra 2.
  evidence: Medido que o 241 troca o 200 (CONTEUDO-MEMORIA). Conferir na 12b se o app deixa carregar uma memória com o gerador ligado, e se não deve desligar o gerador antes.

- source_spec: `_bmad-output/specs/spec-app-controle-taramps/stories/12a-instalar-ler-paridade-e-suposicoes-do-protocolo.md`
  summary: Não se sabe se o `ERRO 0` de uma leitura antes da senha arma a queda de ~29 s (QUEDA-APOS-ERRO). O simulador supõe que não.
  evidence: Na sonda de 24/10, a senha certa veio logo depois e cancelaria a queda de qualquer jeito. Para medir: ler antes da senha e esperar 35 s sem autenticar.

- source_spec: `_bmad-output/specs/spec-app-controle-taramps/stories/12a-instalar-ler-paridade-e-suposicoes-do-protocolo.md`
  summary: O eco da fase (`060`) leva ~1,1 s no aparelho, mas o simulador usa ~85 ms para todos os ecos; nenhum teste exercita o `PrazoEco` de 2 s com um eco lento.
  evidence: Medido no A52 em 24/10 (`estado/log-ble/2026-10-24_1712-app-12a.log`). Quando o prazo estoura, o escritor relê, o que é seguro, mas sobra menos de 1 s de folga. Simular o atraso da fase.

- source_spec: `_bmad-output/specs/spec-app-controle-taramps/stories/12a-instalar-ler-paridade-e-suposicoes-do-protocolo.md`
  summary: Na Mesa do A52, o valor "+10,0 dB" da JBL L não cabe na caixa, e o "dB" quebra para uma segunda linha cortada. "+7,0 dB" e "+6,0 dB" cabem.
  evidence: Visto no vídeo da IA de 24/10 (`_bmad-output/videos/2026-10-24-bgs-controle-ia.mp4`, depois da aplicação da FILME). Conferir na 12b, com valores de três dígitos (+10,0, −12,5) em todas as saídas.
