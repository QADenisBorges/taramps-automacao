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
  - ../../planning-artifacts/sprint-change-proposal-2026-10-23-equipamentos.md
  - ../../forge/montagem-do-setup/forged-idea.md
  - ../../planning-artifacts/sprint-change-proposal-2026-10-29.md
  - ../../planning-artifacts/ux-designs/ux-taramps-automacao-2026-10-30/EXPERIENCE.md
  - ../../planning-artifacts/sprint-change-proposal-2026-10-30.md
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
  - **intent:** O usuário vê o estado completo do aparelho: os parâmetros do MAPA §4, o gerador e a monitoração. As memórias do processador não aparecem no app (decisão do Denis, 29/10/2026): o app não lê os nomes delas (231–233).
  - **success:** Uma leitura completa traz todos os parâmetros sem falha (a referência do protótipo foi 84 de 84). Cada valor do estado atual bate com o que o app da Taramps mostra do som atual.
- **CAP-3**
  - **intent:** O usuário regula, pelas telas, todos os parâmetros de áudio do MAPA §4, por canal e globais. O link dos canais 1–2 e 3–4 não é ajuste do usuário: sai sozinho das ligações do Setup (CAP-16). As saídas na tela são as do Setup: um canal, ou um par de canais vizinhos com o link ligado, que vira uma saída só e escreve nos dois.
  - **success:** Cada parâmetro escrito é relido igual ao valor pedido, dentro do passo do MAPA. Com o link ligado, mexer no canal mestre muda o par. No Casa 2.1, o Sub aparece como uma saída só e mexer nele muda o CH3 e o CH4.
- **CAP-4**
  - **intent:** O usuário cria, aplica, renomeia e apaga memórias no celular. Cada memória pertence a um Setup (CAP-16) e guarda toda a regulagem, com o valor final de cada parâmetro (atraso, fase, cortes, rota, EQ de entrada, ganho, limitador, EQ de saída e MASTER; tudo menos as 15 bandas, o link e o que fica fora das camadas), e a playlist (CAP-15), e lembra o último preset usado com ela, que volta junto quando ela é aplicada. Uma memória nova começa do som atual. Salvar é um passo à parte, com um só botão: cada mudança vai para a camada dona dela (regulagem → memória; 15 bandas → preset, CAP-5); o link vem sozinho do Setup. Aplicar é escrever ao vivo, com cópia antes e releitura. O app nasce sem memórias nem presets do celular (decisão do Denis, 29/10/2026). As 3 memórias do processador não aparecem no app (decisão do Denis, 29/10/2026): o app não salva, não renomeia, não carrega e não lê os nomes delas. Para usar uma memória do aparelho, o caminho é o app da Taramps; depois, este app relê. Quando o som atual não bate com nenhuma memória do celular, o app mostra "Som do aparelho".
  - **success:** Depois de aplicar uma memória, a releitura mostra a regulagem da memória, o link tirado do Setup e o preset escolhido. Nenhum `240`, `241` ou `23N` sai do app.
- **CAP-5**
  - **intent:** O preset é só o EQ gráfico de 15 bandas (gênero ou conteúdo: Rock, Sertanejo, Ação, Diálogo…), guardado no celular e aplicado por cima da memória, sem mexer no resto da regulagem. O usuário pode aplicá-los, renomeá-los e apagá-los. Ao salvar 15 bandas mexidas, o usuário escolhe entre atualizar o preset em uso (vale para toda memória que usa ele) e criar um preset novo.
  - **success:** Trocar o preset muda só as 15 bandas na releitura; os demais parâmetros ficam iguais.
- **CAP-6**
  - **intent:** O app nasce zerado, como numa primeira instalação: sem Setup, equipamentos, fichas, base de modelos, memórias, presets, notas das memórias do aparelho ou preferências. Traz só o protocolo; o usuário cadastra tudo (decisão do Denis, 30/10/2026). As memórias do aparelho ficam intocáveis e não são referência.
  - **success:** Na primeira abertura não há Setup, equipamento, ficha, modelo na base, memória nem preset. Nada de `conhecimento-herdado.md` aparece no app.
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
  - **success:** Com a chave desligada, a operação é recusada, tanto pela tela quanto pela porta de comandos. Ao ligar uma chave, o app mostra o aviso do risco antes. Os padrões de fábrica seguem `chaves-de-acesso.md`.
- **CAP-12**
  - **intent:** A IA comanda o app por uma porta de comandos no Wi-Fi da casa, com as travas de `porta-de-comandos.md`, passando pelas mesmas chaves de acesso do usuário. A porta vai nos dois sentidos na criação da memória (CAP-18): a IA manda a pergunta e os ajustes, e o app devolve a resposta. A interface não mostra a IA: não há o nome, o estado, PARAR IA nem Pausar (decisão do Denis, 30/10/2026).
  - **success:** O cliente pareado lê o estado, muda um ajuste, aplica uma memória e conduz uma criação de memória até o Salvar do usuário. Um cliente sem chave, ou vindo de fora da rede local, é recusado. Uma operação de risco pedida pela porta espera a confirmação numa folha do app que não menciona a IA. Fora da criação, uma sequência de ajustes pela porta sem um toque do usuário no meio espera Continuar numa folha do app (`chaves-de-acesso.md`). Nenhuma tela mostra a palavra "IA".
- **CAP-13**
  - **intent:** A tela e a IA veem o mesmo estado ao vivo, e todo comando fica num histórico. O histórico é lido pela porta e não aparece no app (decisão do Denis, 30/10/2026).
  - **success:** Uma mudança feita pela porta aparece nos valores da tela em até 1 s, como qualquer outra, e a IA lê na hora uma mudança feita na tela. O histórico, lido pela porta, traz a origem (tela ou porta), a hora e o valor de cada comando. O uso ao mesmo tempo segue `porta-de-comandos.md`: um toque na tela pausa a tarefa da porta, sem faixa nem aviso na tela.
- **CAP-15**
  - **intent:** O usuário monta uma playlist para cada memória do celular com arquivos de áudio do celular e toca pelo app, com o som indo pelo Bluetooth ao processador (paridade com o Player do app da Taramps).
  - **success:** Ao aplicar uma memória, o Player mostra a playlist dela; tocar, pausar e pular funcionam; uma faixa cujo arquivo sumiu aparece como indisponível. Decisão do Denis em 21/10/2026; playlist presa à memória do celular desde 23/10/2026.
- **CAP-16**
  - **intent:** O usuário cadastra Setups, um por instalação (o 2.1 de casa, o carro…), todos com o mesmo PRO 2.4BT. O Setup é só o físico: nome e equipamentos com as fichas. A regulagem fica na memória (CAP-4; forja de 29/10/2026). Os equipamentos seguem o modelo de ligação da proposta de 23/10/2026 (equipamentos, seção 2): cada canal do processador vai para uma ou mais entradas de linha (cabo direto, Y ou divisor) de equipamentos de quatro grupos (1: amplificam e tocam, como a caixa ativa e o sub ativo; 2: amplificam para alto-falantes passivos, como o módulo, que tem canais e bridge; 3: processam ou distribuem e repassam; 4: só recebem); os alto-falantes passivos ficam nas saídas (ou no bridge) do grupo 2, mais de um por saída se for o caso; categorias abertas e função acústica. **Fichas:** os dados técnicos são opcionais, e cada um guarda a origem: oficial (manual ou ficha do fabricante, com o endereço), outra fonte, medido ou digitado; um dado sem origem confirmada nunca aparece como verificado, e o que não se sabe fica em branco (o modelo desconhecido fica genérico). Além dos dados de hoje, a ficha tem Re medida, Fs, Qes, Qms, Vas, Bl, Sd, Xmax e Le (alto-falante); tipo, volume, Fb e área do duto (caixa); potência e tensão de ceifamento por canal e em ponte, e ganho medido (módulo); tensão e corrente (fonte). **Base de modelos:** uma ficha confirmada fica guardada no celular por fabricante e modelo, é reaproveitada em outros Setups e completa o campo enquanto o usuário digita. **Link automático:** dois canais vizinhos que alimentam o mesmo bridge ficam com o link ligado; senão, sem link; o app tira isso das ligações e escreve no aparelho, e o usuário não mexe no link. No app, o que se ajusta é o canal do processador: o nome vem dos equipamentos que ele alimenta (sem equipamento, CH1 a CH4), e um par com link é uma saída só. Ao conectar, o app compara o que leu com a última memória aplicada naquele Setup (sem nenhum Setup cadastrado, cai direto na escolha abaixo): se bate, entra direto; se só ganho, limitador, EQ ou MASTER diferem, reaplica sozinho a memória e o preset em uso, em tela toda ("REAPLICANDO"), sem mutar (decisão do Denis, 30/10/2026); se corte, atraso, fase ou rota diferem, ou se não há memória para comparar, para o som, muta os 4 canais (o mudo fica no aparelho até a decisão, mesmo se o app fechar; a IA fica bloqueada), mostra o que não bate e pede para escolher um Setup, criar um Setup novo (o som do aparelho vira a primeira memória dele) ou seguir sem Setup. Com o app conectado, tocar no nome do Setup volta a essa escolha e à conferência (decisões do UX da história 3b, 23/10/2026, e do Denis, 29/10/2026). Os limites físicos usam as fichas e estão na spec do Rendimento (`../spec-rendimento/SPEC.md`).
  - **success:** Com corte, atraso, fase ou rota diferentes da última memória aplicada, nada é aplicado nem tocado sem a confirmação na tela. Num Setup com 4 equipamentos em 4 canais, sem bridge em comum, aparecem 4 saídas com os nomes deles; com o CH3 e o CH4 no mesmo bridge, o link liga sozinho e eles viram uma saída só. Um dado de ficha mostra a origem dele, e um modelo já guardado na base completa a ficha sozinho.
- **CAP-17**
  - **intent:** O Setup aparece num esquema interativo em 2D com sensação de 3D (perspectiva isométrica, profundidade e sombra), atualizado sozinho quando um equipamento entra, sai ou muda: arrastar muda o ângulo, os dedos aproximam e um toque num equipamento abre para ver e editar. Cada equipamento é desenhado pela categoria e pela função acústica, em proporção pelas características (o diâmetro, por exemplo); o esquema mostra o processador com os 4 canais, os cabos e os Y, os equipamentos de cada grupo, as saídas e os bridges dos módulos, os alto-falantes e a cor de cada canal. A função acústica pode ser sugerida pela categoria e pelo corte do canal na memória em uso, e o usuário pode trocar. O 3D de verdade fica para uma versão futura (decisão do Denis, 23/10/2026).
  - **success:** Adicionar, mudar ou tirar um equipamento muda o esquema na hora. No Casa 2.1, o esquema mostra a JBL L no CH1, a JBL R no CH2 e o Sub no módulo em bridge alimentado pelo CH3 e pelo CH4.
- **CAP-14**
  - **intent:** O app traz avisos customizados e interações que o app da Taramps não tem.
  - **success:** Há pelo menos um aviso customizado funcionando no teste 1 do sinal de sucesso, por exemplo ao criar uma memória no celular ("Memória MUSICA ORIG criada no Casa 2.1.") ou na folha do Salvar por camadas.
- **CAP-18**
  - **intent:** O usuário cria uma memória do Setup em uso respondendo a perguntas, uma por tela, com 3 ou 4 respostas e "Outro…". Quem faz as perguntas e escolhe os ajustes é a IA, pela porta (CAP-12); o app só mostra, aplica e salva. Responder à última pergunta autoriza aplicar. Cada ajuste aparece na tela como uma frase com o valor embaixo, e o app escreve no aparelho junto com a animação, que tem prioridade sobre a velocidade. Depois vêm o Aplicando com o esquema de ligação do Setup, o resumo do que mudou e por quê, o "Como ficou?" (aprovar ou pedir refino) e o Salvar, que é só do usuário. O ✕ das perguntas e do "Como ficou?" volta ao som de antes, sem salvar. Uma operação de risco abre a folha de risco do app, e a animação pausa até a resposta; Cancelar nela também volta ao som de antes. Uma chave nas Configurações do app desliga a animação. As perguntas adaptativas e o resto do agente são da spec do Agente (decisões do Denis, 30/10/2026; `porta-de-comandos.md`, seção "Criação da memória").
  - **success:** Com um cliente pareado mandando as perguntas e os ajustes, o app mostra uma pergunta por tela, devolve a resposta pela porta e só aceita ajustes depois da resposta à última pergunta. Cada ajuste é relido igual ao pedido antes de o Aplicando terminar. Cancelar (✕ ou folha de risco) devolve a releitura igual à cópia de antes. Nada vira memória sem o toque em Salvar, e a memória salva guarda a regulagem relida. Nenhuma tela da criação mostra a palavra "IA".

## Constraints

- O app é Android e roda no celular. O notebook sai das configurações.
- As telas cobrem tudo o que o app da Taramps faz, menos as 3 memórias do processador (salvar, renomear e carregar), que ficam só no app da Taramps (decisões do Denis, 22–23/10/2026 e 29/10/2026). No lugar delas, o app tem memórias no celular, ligadas a um Setup. Nenhuma função fica só na IA.
- Não há IA dentro do app. A IA fica fora e só entra pela porta de comandos. A interface não mostra a IA (decisão do Denis, 30/10/2026).
- O Setup é só o físico; toda a regulagem é da memória (forja de 29/10/2026).
- Tudo é reescrito do zero, inclusive o protocolo e o guardião. O código dos protótipos serve só de consulta. `MAPA.md` e `APK.md` são a referência do protocolo.
- O app é andaime: nada é otimizado para reuso no BGS Sound Lab.
- Só cabe uma conexão BLE por vez: o app da Taramps e este app não ficam conectados juntos.
- Não existe comando para desligar o processador. O ON/OFF é o remote out (252), e o processador continua ouvindo o BLE.
- O protocolo veio por engenharia reversa do APK 1.0.8, com o firmware R003. Um firmware novo exige uma nova engenharia reversa.
- A proteção nova é provada por testes de várias formas, inclusive ao vivo com o RCA dos módulos desconectado, antes de ser usada com os alto-falantes ligados.

- A entrega é feita sem data, pedaço por pedaço, cada um testável no aparelho: 1) conectar e ler (CAP-1, CAP-2; teste 3); 2) ajustar, Setups, equipamentos e memórias (CAP-3, CAP-4, CAP-6, CAP-11, CAP-14, CAP-16, CAP-17; testes 1 e 4); 3) porta da IA (CAP-12, CAP-13; teste 2); 4) o resto da paridade (CAP-5, CAP-7 a CAP-10); 5) criação da memória por perguntas (CAP-18; teste 2).

## Non-goals

- IA dentro do app: fica para depois; primeiro, o agente pela porta (forja de 29/10/2026).
- O cérebro do agente que cria a memória (perguntas adaptativas, escuta "1 ou 2", diagnóstico da queixa, skills, aprendizado, ouvintes simulados e o serviço que acorda o agente): spec própria, depois do Rendimento, a partir de `../../forge/montagem-do-setup/forged-idea.md`. O lado do app (telas e porta nos dois sentidos) é a CAP-18. Onde a forja e o UX de 30/10 diferem (uma pergunta por tela; aplicar ao responder à última pergunta, não no toque em Ouvir), vale o UX.
- A tela Histórico: o histórico é lido pela porta (decisão do Denis, 30/10/2026).
- Guardião completo, com limites por alto-falante (fica para uma etapa posterior; aqui existem só as chaves de acesso).
- Usar as 3 memórias do processador: salvar (`240`), renomear (`23N`) e carregar (`241`) só pelo app da Taramps. Elas não aparecem neste app, que não lê nem os nomes (decisão do Denis, 29/10/2026).
- Guardar o som no aparelho para tocar sem o celular. Se a história 12 mostrar que o processador perde o estado ao vivo quando é desligado, o app reaplica a memória ativa do celular ao conectar, depois de confirmar o Setup. Sem o celular, o processador toca o que estiver nele, e isso é responsabilidade do app da Taramps (limitação conhecida).
- Rendimento (limite físico, cerca, níveis Baixo a Extra, análise da playlist, teste a seco, teto por preset): spec própria em `../spec-rendimento/SPEC.md` (30/10/2026). Os limites usam os equipamentos cadastrados na CAP-16.
- Medição por microfone e leitura de dados de softwares profissionais (POC 2).
- Medição de tensão em cada saída do módulo (POC 3).
- Atualização de firmware.
- Acesso pela internet.
- iOS.
- Ler ou decifrar os arquivos `.trps` do app da Taramps: eles são cifrados pelo fabricante. Um preset antigo entra carregando-o pelo app da Taramps e salvando a leitura como preset próprio.
- As telas e a marca finais do BGS Sound Lab.

## Success signal

O POC 1 está pronto quando passa nestes quatro testes:

1. O Denis cadastra o Setup "Casa 2.1" do zero e refaz a MUSICA ORIG como memória dele só pelo celular, sem abrir o app da Taramps. Depois de desligar e religar o processador, ao conectar, o app deixa o som igual ao da memória (o aparelho guardou, ou o app reaplicou).
2. A IA, pela porta de comandos, lê tudo, muda um ajuste, aplica uma memória e conduz a criação de uma memória até o Salvar do Denis, com a tela do celular mostrando ao vivo e sem nada da IA na tela.
3. Cada valor lido pelo app é igual ao que o app da Taramps mostra do estado atual do aparelho. Este app não carrega memória: para comparar outro som, o Denis carrega a memória pelo app da Taramps, e este app relê.
4. Com o processador com corte, atraso, fase ou rota diferentes da última memória aplicada, o app não aplica nem toca antes de o usuário confirmar na tela.

## Assumptions

- O alvo é um só: o PRO 2.4BT + AUDIO com firmware R003. Outros processadores Taramps ficam de fora.
- O cliente IA é o Claude Code no notebook, na mesma rede Wi-Fi do celular.
- A tecnologia do app não foi decidida. A recomendação é Kotlin nativo, a confirmar pelo Denis no plano da história 1.
- A senha do processador é digitada uma vez e fica guardada no celular. Ela nunca aparece em log, na tela nem na porta de comandos.
- "Ao vivo" (CAP-13) foi definido aqui como até 1 s.
- A meta mínima da CAP-14, de um aviso no teste 1, foi definida aqui. O Denis não pediu um número.
