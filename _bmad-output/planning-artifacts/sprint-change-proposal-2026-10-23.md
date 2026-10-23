---
title: 'Proposta de mudança: Setup, memórias no celular e preset de 15 bandas'
created: '2026-10-23'
status: 'aprovada pelo Denis em 23/10/2026; mudanças aplicadas no SPEC, nos documentos e no stories.yaml'
escopo: moderado
spec_afetada: _bmad-output/specs/spec-app-controle-taramps
origem: _bmad-output/forge/teste-de-playlist/forged-idea.md
modo: incremental (6 propostas, todas aprovadas pelo Denis)
---

# Proposta de mudança: Setup, memórias no celular e preset de 15 bandas

## 1. O problema

O forge de 22/10/2026 tinha como base a ideia do teste de playlist e o documento de referência do Denis. Dele saíram três decisões do dono do produto:

- **O mesmo PRO 2.4BT vai servir a vários sistemas** (o 2.1 de casa, o carro…). Cada sistema é um **Setup**.
- **O app não edita as memórias 1, 2 e 3 do processador.** Ele só lê os nomes e carrega (`241`). Salvar e renomear ficam no app da Taramps (decisão de 23/10).
- **O som passa a ter três camadas:**
  - Setup = calibração;
  - memória no celular = caráter + playlist;
  - preset = só o EQ de 15 bandas.

Tipo: **requisito novo** do dono. Isso apareceu antes da história 10, e é por isso que as telas ainda não existem.

**Evidência do conflito.** O SPEC de hoje diz que o app existe "para o Denis deixar de usar o app da Taramps". As regras dele dependem das memórias do aparelho: a CAP-4 (salvar, carregar e nomear as 3 memórias), a CAP-5 (preset de som completo) e os 3 testes de sucesso ("salvar na memória 2", "salva uma memória", "nas 3 memórias"). Além disso, a FILME de 18/10 mostra que o caráter de cinema veio de atraso, compressão, EQ de saída **e** das 15 bandas juntos. Um preset de som completo misturava essas camadas.

## 2. Impacto

| Área | Impacto |
|---|---|
| SPEC | Mudam a regra da linha 74, o "fora do escopo", as CAP-2, 4, 5, 6, 12 e 15 e o sinal de sucesso; entra a CAP-16 (Setup) |
| Histórias feitas (6, 8, 8b, 9b) | Mudam de papel (21 arquivos do código). **Nenhuma é desfeita**: a cópia antes, o escritor e a releitura das histórias 5 e 6 servem do mesmo jeito |
| Histórias por fazer | Entram 3b, 8c e 8d antes da 10; mudam a 10 e a 12 |
| Documentos | Mudam chaves de acesso, modos de falha e conhecimento herdado; porta de comandos e diagramas ficam iguais |
| UX | Muda o EXPERIENCE.md (arquitetura, conectar, componente Memória, Player, selo herdado, fluxos 1 e 4, fluxo novo do carro, estados); mudam alguns componentes do DESIGN.md |
| Código | `GerenteMemorias`, `GerentePresets`, `GerentePlaylists`/`Player`, `PortaComandos`, `ConhecimentoHerdado`, chaves e ViewModels relacionados; o `playlists.json` por posição precisa de migração |

## 3. Caminho escolhido

**Ajuste direto.** Muda o SPEC e entram três histórias antes da 10.
- **Desfazer código foi descartado**: o código das histórias feitas continua útil.
- **Revisar o MVP também foi descartado**: o POC 1 continua com o mesmo escopo, só com outro modelo de memória.
- **Esforço:** médio.
- **Risco:** baixo a médio. O maior é migrar as playlists e o preset herdado.
- **Prazo:** o POC 1 não tem data. A troca evita refazer as telas 10 e 11.

## 4. Mudanças aprovadas

### 4.1 SPEC: regra e "fora do escopo" (proposta 1)

Regra (linha 74):
- ANTES: "As telas cobrem tudo o que o app da Taramps faz, para o Denis deixar de usá-lo. Nenhuma função fica só na IA."
- DEPOIS: "As telas cobrem tudo o que o app da Taramps faz, menos a edição das 3 memórias do processador, que fica só no app da Taramps (decisão do Denis, 22–23/10/2026). No lugar delas, o app tem memórias no celular, ligadas a um Setup. Nenhuma função fica só na IA."

"Fora do escopo", linhas novas:
- **Editar as 3 memórias do processador:** salvar (`240`) e renomear (`23N`) só pelo app da Taramps. O app só lê os nomes e carrega (`241`).
- **Se a história 12 mostrar que o processador perde o estado ao vivo quando é desligado,** o app reaplica a memória ativa do celular ao conectar, depois de confirmar o Setup. Sem o celular, o processador toca o que estiver nele, e isso é responsabilidade do app da Taramps (limitação conhecida).
- **Rendimento** (níveis Baixo a Extra, análise da playlist, teste a seco, teto por preset): spec própria depois da história 12, a partir de `forge/teste-de-playlist/forged-idea.md`.

### 4.2 SPEC: capacidades (proposta 2)

- **CAP-2:** continua lendo os nomes das memórias, só para carregar. Teste: "cada valor do estado atual bate com o app da Taramps, também depois de aplicar a memória herdada FILME".
- **CAP-4:** o usuário cria, aplica, renomeia e apaga memórias no celular. Cada uma pertence a um Setup e guarda o caráter do som e a playlist.
  - O **caráter** é tudo fora da calibração e das 15 bandas: limiter/compressor, EQ de saída, nível de cada canal…
  - **Aplicar** = escrever ao vivo, com cópia antes e releitura.
  - O usuário também pode carregar uma das 3 memórias do processador, com cópia antes e releitura. Ela aparece como "Memória N do aparelho (app da Taramps)", sem Setup e sem playlist, e não pode ser salva por este app.
  - **Teste:** a releitura mostra calibração do Setup + caráter da memória + preset escolhido.
- **CAP-5:** o preset é só o EQ de 15 bandas (Rock, Sertanejo, Ação, Diálogo…), aplicado por cima da memória. Teste: "trocar o preset muda só as 15 bandas na releitura".
- **CAP-16 (nova):** o Setup é um por instalação, com nome e calibração (atraso, fase, corte, equilíbrio L/R).
  - Ao conectar, o usuário escolhe o Setup. O app confere o estado lido com a última aplicação daquele Setup. Se houver dúvida, para o som, muta e pede confirmação.
  - Componentes e limites físicos ficam para a spec do Rendimento.
  - **Teste:** com o aparelho no estado de outro Setup, nada é aplicado nem tocado sem confirmar na tela.
- **CAP-6:** a leitura da FILME de 18/10 é dividida em Setup "Casa 2.1", memória FILME e preset "Cinema". As notas da MUSICA HIGH e da ORIG continuam como notas.
- **CAP-12:** o teste passa a ser "…muda um ajuste e aplica uma memória".
- **CAP-15:** a playlist pertence a cada memória do celular. Ao aplicar uma memória, o Player mostra a playlist dela.
- **Regra de composição:** ganho final e limiter final = valor da memória + equilíbrio do Setup. O app soma os dois ao aplicar.

### 4.3 SPEC: sinal de sucesso (proposta 3)

1. O Denis refaz a MUSICA ORIG como memória do Setup "Casa 2.1" só pelo celular, sem abrir o app da Taramps. Depois de desligar e religar o processador, ao conectar, o app deixa o som igual ao da memória (o aparelho guardou ou o app reaplicou).
2. A IA, pela porta de comandos, lê tudo, muda um ajuste e aplica uma memória, com a tela do celular mostrando ao vivo.
3. Cada valor lido pelo app é igual ao que o app da Taramps mostra, no estado atual e depois de aplicar a FILME herdada.
4. Com o processador no estado de outro Setup, o app não aplica nem toca antes de o usuário confirmar na tela.

### 4.4 Documentos que acompanham o SPEC (proposta 4)

- **chaves-de-acesso.md:**
  - sai "Salvar memória" e entra a nota "salvar e renomear as memórias do aparelho não existem neste app";
  - "Carregar memória" vira "Carregar memória do aparelho" (`241`, ligada, confirmação para a IA);
  - entra "Aplicar memória do celular" (ligada, confirmação para a IA, "Troca todo o som de uma vez");
  - o preset fica em "Ajustes de áudio".
- **failure-modes.md:**
  - a regra 3 cobre aplicar memória do celular, aplicar preset e carregar memória do aparelho;
  - regra 5 (nova): ao conectar, relê e confere o Setup; se houver dúvida, muta e pede confirmação antes de tocar; se o processador perdeu o estado, reaplica a memória ativa depois da confirmação.
- **conhecimento-herdado.md:**
  - a FILME é dividida em três:
    - **Setup "Casa 2.1":** atrasos 0/100/80 cm; fase do sub 180°; cortes LR24 150 Hz e sub BT24 40 Hz; rotas 1 = B e 2 = A; equilíbrio R −3 dB;
    - **memória FILME:** ganhos L +10, sub +6; limiter JBL −15, sub −12; ataque 50 ms, liberação 200 ms; EQ de saída 2 kHz +2,5 e 45 Hz +3;
    - **preset "Cinema":** 160 Hz +1, 400 Hz −1, 4 kHz −1, 6,3 kHz −2, 10 kHz +2, 16 kHz +2;
  - os limites apontam para o recálculo de 20/10 (`regulagem/memoria-spl.md` §12), ainda provisórios;
  - "As 3 memórias do aparelho": o app só carrega;
  - o preset antigo entra como memória do celular.

### 4.5 Histórias (proposta 5)

Nova ordem: **3b → 8c → 8d → 10 → 11 → 12**.

| id | Título | Conteúdo | CAPs |
|---|---|---|---|
| 3b | UX: Setup e memórias no celular | `bmad-ux` com o Denis, revisando o que está em 4.6 | 4, 5, 15, 16 |
| 8c | Setup e memória no celular | Setup e conferência ao conectar; memórias no celular com a soma do equilíbrio; carregar do aparelho; tirar o salvar e o renomear no aparelho do código; dividir a FILME herdada; chaves e regra 5 | 4, 6, 11, 16 |
| 8d | Preset de 15 bandas e playlist da memória do celular | Preset só de 15 bandas; playlist presa ao id da memória, com migração do `playlists.json`; "aplicar memória" na porta da IA | 5, 12, 15 |

- **10:** passa a ser "leitura, ajustes, link, Setups e memórias".
- **12** ganha dois itens: medir se o processador guarda o estado ao vivo ao desligar (e conferir a reaplicação), e conferir o teste 4.
- **Comentário no fim:** depois da 12, spec própria do Rendimento.

### 4.6 UX (proposta 6, revisada na história 3b)

- **EXPERIENCE.md:**
  - arquitetura de informação: Setups novo; Memórias = celular + "Do aparelho"; Presets = 15 bandas;
  - Conectar com escolha e conferência do Setup;
  - componente Memória, em quantidade livre e salvando no celular;
  - Player preso à memória do celular;
  - selo herdado (Setup, FILME, Cinema);
  - fluxos 1 e 4 refeitos;
  - fluxo novo "levar o processador para o carro";
  - estados (aplicando, aplicado pela metade, Setup em dúvida);
  - "Quem vem do app da Taramps".
- **DESIGN.md:**
  - revisar botões e abas de memória, o cartão de memória e o cartão da memória em uso;
  - componentes novos: seletor de Setup e chip de preset.
- **Primeira pergunta da 3b:** com memórias em quantidade livre, os 3 botões do Início deixam de caber.

## 5. Entrega

**Escopo moderado:** o backlog e o contrato mudam, sem replanejar o POC.

| Quem | O quê |
|---|---|
| Dev (esta sessão) | Aplicar as mudanças de 4.1 a 4.5 no SPEC, nos documentos e no `stories.yaml` |
| UX (`bmad-ux`, história 3b) | Revisar o EXPERIENCE.md e o DESIGN.md com o Denis |
| Dev (`bmad-build`) | Histórias 8c e 8d, depois 10, 11 e 12 |

**Pronto quando:**
- o SPEC, os documentos e o `stories.yaml` mostram as mudanças acima;
- a 3b fecha a UX;
- a 8c e a 8d passam nos testes contra o `ProcessadorSimulado`, sem nenhum `240` ou `23N` saindo do app;
- a 12 confere os testes 1 a 4 no aparelho.
