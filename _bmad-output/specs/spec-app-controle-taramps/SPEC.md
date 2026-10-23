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
  - ../../forge/teste-de-playlist/forged-idea.md
  - ../../planning-artifacts/sprint-change-proposal-2026-10-23.md
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
  - **intent:** O usuário vê o estado completo do aparelho: os parâmetros do MAPA §4, o gerador, a monitoração e os nomes das memórias do aparelho (só leitura, para escolher qual carregar).
  - **success:** Uma leitura completa traz todos os parâmetros sem falha (a referência do protótipo foi 84 de 84). Cada valor do estado atual bate com o que o app da Taramps mostra, também depois de aplicar a memória herdada FILME.
- **CAP-3**
  - **intent:** O usuário regula, pelas telas, todos os parâmetros de áudio do MAPA §4, por canal e globais, incluindo o link dos canais 1–2 e 3–4.
  - **success:** Cada parâmetro escrito é relido igual ao valor pedido, dentro do passo do MAPA. Com o link ligado, mexer no canal mestre muda o par.
- **CAP-4**
  - **intent:** O usuário cria, aplica, renomeia e apaga memórias no celular. Cada memória pertence a um Setup (CAP-16) e guarda o caráter do som (limiter/compressor, EQ de saída, nível de cada canal e o resto fora da calibração do Setup e das 15 bandas) e a playlist (CAP-15). Aplicar é escrever ao vivo, com cópia antes e releitura. O ganho final e o limiter final de cada canal são o valor da memória mais o equilíbrio do Setup; o app soma os dois ao aplicar. O usuário também pode carregar uma das 3 memórias do processador (`241`), com cópia antes e releitura: o som carregado aparece como "Memória N do aparelho (app da Taramps)", sem Setup e sem playlist, e este app não salva nem renomeia memórias do aparelho.
  - **success:** Depois de aplicar uma memória, a releitura mostra a calibração do Setup, o caráter da memória e o preset escolhido. Nenhum `240` ou `23N` sai do app.
- **CAP-5**
  - **intent:** O preset é só o EQ gráfico de 15 bandas (gênero ou conteúdo: Rock, Sertanejo, Ação, Diálogo…), guardado no celular e aplicado por cima da memória, sem mexer na calibração nem no caráter. O usuário pode aplicá-los, renomeá-los e apagá-los.
  - **success:** Trocar o preset muda só as 15 bandas na releitura; os demais parâmetros ficam iguais.
- **CAP-6**
  - **intent:** O app já nasce com o conhecimento dos protótipos: a leitura da FILME de 18/10 dividida em Setup "Casa 2.1", memória FILME e preset "Cinema", as notas das memórias do aparelho e as preferências do Denis.
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
  - **success:** O cliente IA pareado lê o estado, muda um ajuste e aplica uma memória. Um cliente sem chave, ou vindo de fora da rede local, é recusado. Uma operação de risco pedida pela IA espera a confirmação na tela do celular.
- **CAP-13**
  - **intent:** A tela e a IA veem o mesmo estado ao vivo, e todo comando fica num histórico.
  - **success:** Uma mudança feita pela IA aparece na tela em até 1 s, e a IA lê na hora uma mudança feita na tela. O histórico mostra a origem (tela ou IA), a hora e o valor de cada comando. O uso ao mesmo tempo segue `porta-de-comandos.md`: um toque na tela pausa a tarefa da IA, e cada mudança da IA aparece como aviso.
- **CAP-15**
  - **intent:** O usuário monta uma playlist para cada memória do celular com arquivos de áudio do celular e toca pelo app, com o som indo pelo Bluetooth ao processador (paridade com o Player do app da Taramps).
  - **success:** Ao aplicar uma memória, o Player mostra a playlist dela; tocar, pausar e pular funcionam; uma faixa cujo arquivo sumiu aparece como indisponível. Decisão do Denis em 21/10/2026; playlist presa à memória do celular desde 23/10/2026.
- **CAP-16**
  - **intent:** O usuário cadastra Setups, um por instalação (o 2.1 de casa, o carro…), todos com o mesmo PRO 2.4BT. Cada Setup tem nome e calibração (atraso, fase, corte e equilíbrio L/R de ganho e de limiter). Ao conectar, o usuário escolhe o Setup; o app confere o estado lido com a última aplicação daquele Setup e, na dúvida, para o som, muta e pede confirmação. Componentes e limites físicos ficam para a spec do Rendimento.
  - **success:** Com o aparelho no estado de outro Setup, nada é aplicado nem tocado sem a confirmação na tela.
- **CAP-14**
  - **intent:** O app traz avisos customizados e interações que o app da Taramps não tem.
  - **success:** Há pelo menos um aviso customizado funcionando no teste 1 do sinal de sucesso, por exemplo ao sobrescrever uma memória.

## Constraints

- O app é Android e roda no celular. O notebook sai das configurações.
- As telas cobrem tudo o que o app da Taramps faz, menos a edição das 3 memórias do processador, que fica só no app da Taramps (decisão do Denis, 22–23/10/2026). No lugar delas, o app tem memórias no celular, ligadas a um Setup. Nenhuma função fica só na IA.
- Não há IA dentro do app. A IA fica fora e só entra pela porta de comandos.
- Tudo é reescrito do zero, inclusive o protocolo e o guardião. O código dos protótipos serve só de consulta. `MAPA.md` e `APK.md` são a referência do protocolo.
- O app é andaime: nada é otimizado para reuso no BGS Sound Lab.
- Só cabe uma conexão BLE por vez: o app da Taramps e este app não ficam conectados juntos.
- Não existe comando para desligar o processador. O ON/OFF é o remote out (252), e o processador continua ouvindo o BLE.
- O protocolo veio por engenharia reversa do APK 1.0.8, com o firmware R003. Um firmware novo exige uma nova engenharia reversa.
- A proteção nova é provada por testes de várias formas, inclusive ao vivo com o RCA dos módulos desconectado, antes de ser usada com os alto-falantes ligados.

- A entrega é feita sem data, pedaço por pedaço, cada um testável no aparelho: 1) conectar e ler (CAP-1, CAP-2; teste 3); 2) ajustar, Setups e memórias (CAP-3, CAP-4, CAP-6, CAP-11, CAP-14, CAP-16; testes 1 e 4); 3) porta da IA (CAP-12, CAP-13; teste 2); 4) o resto da paridade (CAP-5, CAP-7 a CAP-10).

## Non-goals

- IA dentro do app (fica para o BGS Sound Lab).
- Guardião completo, com limites por alto-falante (fica para uma etapa posterior; aqui existem só as chaves de acesso).
- Editar as 3 memórias do processador: salvar (`240`) e renomear (`23N`) só pelo app da Taramps. Este app só lê os nomes e carrega (`241`).
- Guardar o som no aparelho para tocar sem o celular. Se a história 12 mostrar que o processador perde o estado ao vivo quando é desligado, o app reaplica a memória ativa do celular ao conectar, depois de confirmar o Setup. Sem o celular, o processador toca o que estiver nele, e isso é responsabilidade do app da Taramps (limitação conhecida).
- Rendimento (níveis Baixo a Extra, análise da playlist, teste a seco, teto por preset): spec própria depois da história 12, a partir de `../../forge/teste-de-playlist/forged-idea.md`.
- Medição por microfone e leitura de dados de softwares profissionais (POC 2).
- Medição de tensão em cada saída do módulo (POC 3).
- Atualização de firmware.
- Acesso pela internet.
- iOS.
- Ler ou decifrar os arquivos `.trps` do app da Taramps: eles são cifrados pelo fabricante. Um preset antigo entra carregando-o pelo app da Taramps e salvando a leitura como preset próprio.
- As telas e a marca finais do BGS Sound Lab.

## Success signal

O POC 1 está pronto quando passa nestes quatro testes:

1. O Denis refaz a MUSICA ORIG como memória do Setup "Casa 2.1" só pelo celular, sem abrir o app da Taramps. Depois de desligar e religar o processador, ao conectar, o app deixa o som igual ao da memória (o aparelho guardou, ou o app reaplicou).
2. A IA, pela porta de comandos, lê tudo, muda um ajuste e aplica uma memória, com a tela do celular mostrando ao vivo.
3. Cada valor lido pelo app é igual ao que o app da Taramps mostra, no estado atual e depois de aplicar a FILME herdada.
4. Com o processador no estado de outro Setup, o app não aplica nem toca antes de o usuário confirmar na tela.

## Assumptions

- O alvo é um só: o PRO 2.4BT + AUDIO com firmware R003. Outros processadores Taramps ficam de fora.
- O cliente IA é o Claude Code no notebook, na mesma rede Wi-Fi do celular.
- A tecnologia do app não foi decidida. A recomendação é Kotlin nativo, a confirmar pelo Denis no plano da história 1.
- A senha do processador é digitada uma vez e fica guardada no celular. Ela nunca aparece em log, na tela nem na porta de comandos.
- "Ao vivo" (CAP-13) foi definido aqui como até 1 s.
- A meta mínima da CAP-14, de um aviso no teste 1, foi definida aqui. O Denis não pediu um número.
