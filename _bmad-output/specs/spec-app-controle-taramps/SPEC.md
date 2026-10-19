---
id: SPEC-app-controle-taramps
companions:
  - chaves-de-acesso.md
  - porta-de-comandos.md
  - conhecimento-herdado.md
  - failure-modes.md
  - architecture-diagrams.md
  - ../../../protocolo/MAPA.md
  - ../../../protocolo/APK.md
sources:
  - ../../forge/app-controle-taramps/forged-idea.md
---

> **Contrato canônico.** Este SPEC e os arquivos em `companions:` formam o contrato completo, já validado quanto à preservação, do que construir, testar e validar. As fontes do frontmatter servem só para rastrear a origem.

# POC 1: app Android de controle do PRO 2.4BT + AUDIO

## Why

Uma visão a realizar, que nasceu de uma dor. Para regular o 2.1, o Denis depende do app da Taramps no celular, e a IA só chega ao processador por caminhos indiretos. Salvar memórias, por exemplo, exige operar o app por adb. O POC 1 é um app próprio que faz tudo o que o app da Taramps faz, fala direto com o processador e abre uma porta segura para a IA comandar. É o primeiro de três POCs: depois vêm a leitura de dados e a medição de tensão por saída. Eles levam ao BGS Sound Lab, um kit acessível e de bom custo-benefício, pensado como pontapé para a Taramps ou outras marcas nacionais. Este app é andaime: dele passa só o conhecimento.

## Capabilities

- **CAP-1**
  - **intent:** O app conecta e autentica no PRO 2.4BT pelo Bluetooth do celular e lê a versão do firmware.
  - **success:** Com a senha certa, conecta e mostra "R003". Com a senha errada, recusa sem enviar nenhuma escrita. Com um firmware simulado diferente de R003, entra em modo só leitura e avisa.
- **CAP-2**
  - **intent:** O usuário vê o estado completo do aparelho: os parâmetros do MAPA §4, o gerador, a monitoração e os nomes das memórias.
  - **success:** Uma leitura completa traz todos os parâmetros sem falha (a referência do protótipo foi 84 de 84). Nas 3 memórias, cada valor bate com o que o app da Taramps mostra.
- **CAP-3**
  - **intent:** O usuário regula, pelas telas, todos os parâmetros de áudio do MAPA §4, por canal e globais, incluindo o link dos canais 1–2 e 3–4.
  - **success:** Cada parâmetro escrito é relido igual ao valor pedido, dentro do passo do MAPA. Com o link ligado, mexer no canal mestre muda o par.
- **CAP-4**
  - **intent:** O usuário salva, carrega e nomeia as 3 memórias do DSP.
  - **success:** Uma memória salva pelo app aparece com o mesmo nome e os mesmos valores no app da Taramps. Ao carregar uma memória, o app relê o estado inteiro.
- **CAP-5**
  - **intent:** O usuário guarda presets próprios no celular, separados dos presets do app da Taramps, e pode aplicá-los, renomeá-los e apagá-los.
  - **success:** Um preset salvo, aplicado depois de outras mudanças, deixa o aparelho com os mesmos valores da hora em que foi salvo (confirmado por releitura).
- **CAP-6**
  - **intent:** O app já nasce com o conhecimento dos protótipos: os valores atuais, as 3 memórias, os presets e as preferências do Denis.
  - **success:** Na primeira abertura, os itens de `conhecimento-herdado.md` estão disponíveis no app, sem precisar digitar nada.
- **CAP-7**
  - **intent:** O usuário acompanha ao vivo o voltímetro e o clip das saídas e da entrada.
  - **success:** O valor do voltímetro na tela bate com o multímetro, dentro da precisão do aparelho, e um clip provocado aparece no canal certo.
- **CAP-8**
  - **intent:** O usuário usa o gerador de sinal: tom fixo e sweep.
  - **success:** O tom pedido toca na frequência e no ganho escolhidos e para sob comando. A releitura confirma o gerador desligado.
- **CAP-9**
  - **intent:** O usuário liga e desliga o módulo (remote out) e o Bluetooth de áudio.
  - **success:** O comando do app liga e desliga o módulo, e a tela reflete o estado relido.
- **CAP-10**
  - **intent:** O usuário pode usar os comandos de aparelho inteiro: reset de fábrica, reset de áudio, troca de senha e nome do produto.
  - **success:** Cada comando só roda com a sua chave de acesso ligada. Depois dele, o app relê o aparelho e mostra o resultado.
- **CAP-11**
  - **intent:** Cada operação de risco tem uma chave de acesso, ligada ou desligada por padrão conforme a segurança. Quem decide é o usuário, orientado sobre o risco.
  - **success:** Com a chave desligada, a operação é recusada, tanto pela tela quanto pela IA. Ao ligar uma chave, o app mostra o aviso do risco antes. Os padrões de fábrica seguem `chaves-de-acesso.md`.
- **CAP-12**
  - **intent:** A IA comanda o app por uma porta de comandos no Wi-Fi da casa, com as travas de `porta-de-comandos.md`, passando pelas mesmas chaves de acesso do usuário.
  - **success:** O cliente IA pareado lê o estado, muda um ajuste e salva uma memória. Um cliente sem chave, ou vindo de fora da rede local, é recusado. Uma operação de risco pedida pela IA espera a confirmação na tela do celular.
- **CAP-13**
  - **intent:** A tela e a IA veem o mesmo estado ao vivo, e todo comando fica num histórico.
  - **success:** Uma mudança feita pela IA aparece na tela em até 1 s, e a IA lê na hora uma mudança feita na tela. O histórico mostra a origem (tela ou IA), a hora e o valor de cada comando. O uso ao mesmo tempo segue `porta-de-comandos.md`: um toque na tela pausa a tarefa da IA, e cada mudança da IA aparece como aviso.
- **CAP-14**
  - **intent:** O app traz avisos customizados e interações que o app da Taramps não tem.
  - **success:** Há pelo menos um aviso customizado funcionando no teste 1 do sinal de sucesso, por exemplo ao sobrescrever uma memória.

## Constraints

- O app é Android e roda no celular. O notebook sai das configurações.
- As telas cobrem tudo o que o app da Taramps faz, para o Denis deixar de usá-lo. Nenhuma função fica só na IA.
- Não há IA dentro do app. A IA fica fora e só entra pela porta de comandos.
- Tudo é reescrito do zero, inclusive o protocolo e o guardião. O código dos protótipos serve só de consulta. `MAPA.md` e `APK.md` são a referência do protocolo.
- O app é andaime: nada é otimizado para reuso no BGS Sound Lab.
- Só cabe uma conexão BLE por vez: o app da Taramps e este app não ficam conectados juntos.
- Não existe comando para desligar o processador. O ON/OFF é o remote out (252), e o processador continua ouvindo o BLE.
- O protocolo veio por engenharia reversa do APK 1.0.8, com o firmware R003. Um firmware novo exige uma nova engenharia reversa.
- A proteção nova é provada por testes de várias formas, inclusive ao vivo com o RCA dos módulos desconectado, antes de ser usada com os alto-falantes ligados.

- A entrega é feita sem data, pedaço por pedaço, cada um testável no aparelho: 1) conectar e ler (CAP-1, CAP-2; teste 3); 2) ajustar e memórias (CAP-3, CAP-4, CAP-6, CAP-11, CAP-14; teste 1); 3) porta da IA (CAP-12, CAP-13; teste 2); 4) o resto da paridade (CAP-5, CAP-7 a CAP-10).

## Non-goals

- IA dentro do app (fica para o BGS Sound Lab).
- Guardião completo, com limites por alto-falante (fica para uma etapa posterior; aqui existem só as chaves de acesso).
- Medição por microfone e leitura de dados de softwares profissionais (POC 2).
- Medição de tensão em cada saída do módulo (POC 3).
- Atualização de firmware.
- Acesso pela internet.
- iOS.
- Ler ou decifrar os arquivos `.trps` do app da Taramps: eles são cifrados pelo fabricante. Um preset antigo entra carregando-o pelo app da Taramps e salvando a leitura como preset próprio.
- As telas e a marca finais do BGS Sound Lab.

## Success signal

O POC 1 está pronto quando passa nestes três testes:

1. O Denis refaz a MUSICA ORIG e salva na memória 2 só pelo celular, sem abrir o app da Taramps.
2. A IA, pela porta de comandos, lê tudo, muda um ajuste e salva uma memória, com a tela do celular mostrando ao vivo.
3. Cada valor lido pelo app é igual ao que o app da Taramps mostra, nas 3 memórias.

## Assumptions

- O alvo é um só: o PRO 2.4BT + AUDIO com firmware R003. Outros processadores Taramps ficam de fora.
- O cliente IA é o Claude Code no notebook, na mesma rede Wi-Fi do celular.
- A tecnologia do app não foi decidida. A recomendação é Kotlin nativo, a confirmar pelo Denis no plano da história 1.
- A senha do processador é digitada uma vez e fica guardada no celular. Ela nunca aparece em log, na tela nem na porta de comandos.
- "Ao vivo" (CAP-13) foi definido aqui como até 1 s.
- A meta mínima da CAP-14, de um aviso no teste 1, foi definida aqui. O Denis não pediu um número.
