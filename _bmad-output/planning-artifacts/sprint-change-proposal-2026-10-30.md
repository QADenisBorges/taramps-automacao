---
title: 'Proposta de mudança: interface sem a IA, reaplicar sozinho e a criação da memória por perguntas no app'
created: '2026-10-30'
status: 'aprovada pelo Denis em 30/10/2026 e aplicada na spec, nas histórias e no EXPERIENCE'
nota_da_aplicacao: >-
  O título da 11 (feita) ficou como estava: é só rótulo, e a história não é
  reaberta. As histórias novas ficaram no stories.yaml na ordem 9c, 12b, 9d,
  10d, 12c.
escopo: moderado
spec_afetada: _bmad-output/specs/spec-app-controle-taramps
gatilho: _bmad-output/planning-artifacts/ux-designs/ux-taramps-automacao-2026-10-30 (EXPERIENCE.md, Questões em aberto, item 1; .memlog.md, a partir de "Update 30/10")
modo: batch
---

# Proposta de mudança: interface sem a IA e a criação da memória no app

## 1. O problema

**Como apareceu.** Na rodada de UX de 30/10 (BGS Sound), o Denis desenhou a criação de uma memória respondendo a perguntas e decidiu que a IA não aparece no app. A SPEC ainda descreve o desenho antigo: aba IA, PARAR IA, faixa da IA, tela Histórico, "aplica no toque em Ouvir" e "avisa e oferece Reaplicar" na diferença leve. Construir pela SPEC de hoje faria o contrário do que foi decidido.

**O que o Denis decidiu (UX de 30/10).**
- **Nenhum traço da IA na interface:** saem a aba IA, o PARAR IA, o Pausar, o chip e a faixa da IA e a tela Histórico. A porta e o pareamento continuam por baixo, e o histórico fica fora do app.
- **Criar a memória por perguntas:** uma pergunta por tela, com 3 ou 4 respostas e "Outro…". Responder à última pergunta autoriza aplicar, e nada é salvo sem o toque em Salvar.
- **Palco da criação:** cada ajuste aparece como uma frase, com o valor embaixo. Depois vem o Aplicando, com o esquema de ligação do Setup no alto e os canais acendendo.
- **Parar a criação:** o ✕ fica nas perguntas e no "Como ficou?" e volta ao som de antes. Não há ✕ no palco nem no Aplicando.
- **Confirmação de risco:** uma folha do app, sem mencionar a IA, com Confirmar e Cancelar. A animação pausa até a resposta.
- **Reaplicar sozinho na diferença leve:** com só ganho, limitador, EQ ou MASTER diferentes, o app reaplica a memória e o preset em tela toda ("REAPLICANDO"), sem mutar.

**O que o Denis decidiu nesta correção (30/10).**

| # | Decisão |
|---|---|
| D1 | A criação da memória por perguntas entra já neste POC, sem esperar a spec do Agente. |
| D2 | Entra só o lado do app: as telas da criação e a porta nos dois sentidos (a IA manda a pergunta, o app mostra e devolve a resposta). O "cérebro" fica na spec do Agente: perguntas adaptativas, escuta "1 ou 2", diagnóstico da queixa, serviço que acorda o agente, skills, aprendizado e ouvintes simulados. |
| D3 | A chave da porta e o código de pareamento ficam na tela **Aparelho**, como a chave **"Controle pela rede"**. Ao ligar, aparece o aviso do risco e, embaixo, o código de 6 dígitos. |
| D4 | Fora da criação, a IA continua mexendo no som pela porta. A cada 3 ajustes seguidos sem um toque, sobe uma folha do app pedindo **Continuar**, sem falar em IA. Dentro da criação, a última resposta já autoriza, e o Continuar não vale. |
| D5 | Na folha de risco da criação, **Cancelar volta ao som de antes** (a cópia feita antes de mexer) e leva à Roda, sem salvar. Faz o mesmo que o ✕. |

**Fica fora desta correção:** construir as telas novas do BGS Sound (Roda, Ajustes, Setup, Aparelho no visual novo). Isso será planejado à parte. Ver seção 6.

**Tipo.** Requisito novo do dono do produto (rodada de UX), com uma parte da spec do Agente trazida para antes.

## 2. Impacto

| Área | Impacto |
|---|---|
| SPEC | CAP-12 e CAP-13 mudam (porta sem rosto, histórico fora do app, uso ao mesmo tempo); CAP-11 muda pouco; CAP-16 muda (reaplicar sozinho); entra a **CAP-18** (criação da memória, lado do app); Constraints, Non-goals, Success signal e fontes mudam |
| porta-de-comandos.md | Travas 1, 4 e 5 mudam; "Uso ao mesmo tempo" é refeito; entra a seção da criação |
| chaves-de-acesso.md | "IA pela rede" vira "Controle pela rede", em Aparelho; o Continuar vira folha do app e não vale na criação |
| failure-modes.md | A regra 5 muda (reaplicar sozinho); entra a regra 6 (criação interrompida) |
| EXPERIENCE.md | A Questão em aberto 1 fica resolvida; as questões 13 (Cancelar da folha de risco) e 22 (a IA fora da criação) também |
| Histórias feitas (9a, 9b, 10c, 11) | Nenhuma é desfeita. O código muda para frente em três histórias novas |
| Histórias novas | **9c** (porta sem rosto e reaplicar sozinho), **9d** (porta nos dois sentidos para a criação) e **10d** (telas da criação) |
| 12b e 12c | A 12b confere o reaplicar sozinho no lugar do "só avisa"; a 12c testa a porta sem rosto e a criação pela porta |
| Código | Saem `IaAtivaPage`, `IaPelaRedePage`, `HistoricoPage`, `ConfirmacaoIaPage` (vira a folha de risco neutra) e os ViewModels deles; `Uso/ModoToque.cs` e `Uso/UsoCompartilhado.cs` (sai o "travado até PARAR IA"); `Porta/PortaComandos.cs` (rotas da criação); `Setups` (conferência); `ChavesViewModel` e `AparelhoPage` (chave nova); telas novas da criação |
| Spec do Agente | Fica menor: só o cérebro (D2) |

## 3. Caminho recomendado: ajuste direto, com 3 histórias novas

- **Ajuste direto.** A SPEC e os anexos passam a dizer o que foi decidido, e o código muda para frente em três histórias novas. Nada feito é desfeito: as páginas da IA saem, mas o núcleo (porta, pareamento, histórico, pausa no toque) fica.
- **Por que 3 histórias.** A **9c** é pequena e mexe no que a 12b vai conferir (a conferência ao conectar), então vem antes de a 12b continuar. A **9d** é só núcleo e porta, testada contra o simulador. A **10d** é só tela e depende da 9d.
- **Ordem sugerida:** 9c → 12b (continua no aparelho) → 9d → 10d → 12c → spec do Rendimento → spec do Agente (só o cérebro).
- **Rollback:** não vale.
- **MVP:** os testes 1, 3 e 4 do sinal de sucesso não mudam. O teste 2 ganha a criação pela porta e passa a pedir a tela sem nada da IA.
- **Esforço e risco:** médio. A porta já tem tarefa, eventos e confirmação (`/tarefa`, `/eventos`, `PedidosConfirmacao`), e a criação se apoia nisso. O maior risco é a animação junto com a escrita real, mas a regra já está decidida: a frase nunca sai antes do tempo dela, e o Aplicando só termina com a releitura.

## 4. Mudanças, uma por uma

### 4.1 SPEC.md

**Frontmatter `sources`:** acrescentar
`../../planning-artifacts/ux-designs/ux-taramps-automacao-2026-10-30/EXPERIENCE.md` e
`../../planning-artifacts/sprint-change-proposal-2026-10-30.md`.

**CAP-11, success.**
- ANTES: "Com a chave desligada, a operação é recusada, tanto pela tela quanto pela IA."
- DEPOIS: "Com a chave desligada, a operação é recusada, tanto pela tela quanto pela porta de comandos."

**CAP-12.**
- ANTES (intent): "A IA comanda o app por uma porta de comandos no Wi-Fi da casa, com as travas de `porta-de-comandos.md`, passando pelas mesmas chaves de acesso do usuário."
- DEPOIS (intent): "A IA comanda o app por uma porta de comandos no Wi-Fi da casa, com as travas de `porta-de-comandos.md`, passando pelas mesmas chaves de acesso do usuário. A porta vai nos dois sentidos na criação da memória (CAP-18): a IA manda a pergunta e os ajustes, e o app devolve a resposta. A interface não mostra a IA: não há o nome, o estado, PARAR IA nem Pausar (decisão do Denis, 30/10/2026)."
- ANTES (success): "… Uma operação de risco pedida pela IA espera a confirmação na tela do celular. Uma sequência de ajustes da IA sem um toque do usuário no meio espera Continuar na tela (`chaves-de-acesso.md`)."
- DEPOIS (success): "O cliente pareado lê o estado, muda um ajuste, aplica uma memória e conduz uma criação de memória até o Salvar do usuário. Um cliente sem chave, ou vindo de fora da rede local, é recusado. Uma operação de risco pedida pela porta espera a confirmação numa folha do app que não menciona a IA. Fora da criação, uma sequência de ajustes pela porta sem um toque do usuário no meio espera Continuar numa folha do app (`chaves-de-acesso.md`). Nenhuma tela mostra a palavra "IA"."

**CAP-13.**
- ANTES (intent): "A tela e a IA veem o mesmo estado ao vivo, e todo comando fica num histórico."
- DEPOIS (intent): "A tela e a IA veem o mesmo estado ao vivo, e todo comando fica num histórico. O histórico é lido pela porta e não aparece no app (decisão do Denis, 30/10/2026)."
- ANTES (success): "Uma mudança feita pela IA aparece na tela em até 1 s, e a IA lê na hora uma mudança feita na tela. O histórico mostra a origem (tela ou IA), a hora e o valor de cada comando. O uso ao mesmo tempo segue `porta-de-comandos.md`: um toque na tela pausa a tarefa da IA, e cada mudança da IA aparece como aviso."
- DEPOIS (success): "Uma mudança feita pela porta aparece nos valores da tela em até 1 s, como qualquer outra, e a IA lê na hora uma mudança feita na tela. O histórico, lido pela porta, traz a origem (tela ou porta), a hora e o valor de cada comando. O uso ao mesmo tempo segue `porta-de-comandos.md`: um toque na tela pausa a tarefa da porta, sem faixa nem aviso na tela."

**CAP-16, conferência ao conectar.**
- ANTES: "… se só ganho, limitador, EQ ou MASTER diferem, avisa e oferece reaplicá-la, sem mutar; …"
- DEPOIS: "… se só ganho, limitador, EQ ou MASTER diferem, reaplica sozinho a memória e o preset em uso, em tela toda ("REAPLICANDO"), sem mutar (decisão do Denis, 30/10/2026); …"

**CAP-18 (nova), depois da CAP-14.**
- **intent:** O usuário cria uma memória do Setup em uso respondendo a perguntas, uma por tela, com 3 ou 4 respostas e "Outro…". Quem faz as perguntas e escolhe os ajustes é a IA, pela porta (CAP-12); o app só mostra, aplica e salva. Responder à última pergunta autoriza aplicar. Cada ajuste aparece na tela como uma frase com o valor embaixo, e o app escreve no aparelho junto com a animação, que tem prioridade sobre a velocidade. Depois vêm o Aplicando com o esquema de ligação do Setup, o resumo do que mudou e por quê, o "Como ficou?" (aprovar ou pedir refino) e o Salvar, que é só do usuário. O ✕ das perguntas e do "Como ficou?" volta ao som de antes, sem salvar. Uma operação de risco abre a folha de risco do app, e a animação pausa até a resposta. Uma chave nas Configurações do app desliga a animação. As perguntas adaptativas e o resto do agente são da spec do Agente (decisões do Denis, 30/10/2026).
- **success:** Com um cliente pareado mandando as perguntas e os ajustes, o app mostra uma pergunta por tela, devolve a resposta pela porta e só aceita ajustes depois da resposta à última pergunta. Cada ajuste é relido igual ao pedido antes de o Aplicando terminar. Cancelar (✕ ou folha de risco) devolve a releitura igual à cópia de antes. Nada vira memória sem o toque em Salvar, e a memória salva guarda a regulagem relida. Nenhuma tela da criação mostra a palavra "IA".

**Constraints.**
- ANTES: "Não há IA dentro do app. A IA fica fora e só entra pela porta de comandos."
- DEPOIS: "Não há IA dentro do app. A IA fica fora e só entra pela porta de comandos. A interface não mostra a IA (decisão do Denis, 30/10/2026)."
- Entrega por pedaços: acrescentar "5) criação da memória por perguntas (CAP-18; teste 2)".

**Non-goals.**
- ANTES: "O agente que cria a memória (perguntas adaptativas, escuta "1 ou 2", aprendizado, ouvintes simulados, porta nos dois sentidos com modal e o serviço que acorda o agente): spec própria, depois do Rendimento, …"
- DEPOIS: "O cérebro do agente que cria a memória (perguntas adaptativas, escuta "1 ou 2", diagnóstico da queixa, skills, aprendizado, ouvintes simulados e o serviço que acorda o agente): spec própria, depois do Rendimento, a partir de `../../forge/montagem-do-setup/forged-idea.md`. O lado do app (telas e porta nos dois sentidos) é a CAP-18. Onde a forja e o UX de 30/10 diferem (uma pergunta por tela; aplicar ao responder à última pergunta, não no toque em Ouvir), vale o UX."
- Novo: "A tela Histórico: o histórico é lido pela porta (decisão do Denis, 30/10/2026)."

**Success signal, teste 2.**
- ANTES: "A IA, pela porta de comandos, lê tudo, muda um ajuste e aplica uma memória, com a tela do celular mostrando ao vivo."
- DEPOIS: "A IA, pela porta de comandos, lê tudo, muda um ajuste, aplica uma memória e conduz a criação de uma memória até o Salvar do Denis, com a tela do celular mostrando ao vivo e sem nada da IA na tela."

### 4.2 porta-de-comandos.md

- **Trava 1.** DEPOIS: "Desligada por padrão. A porta só abre quando o usuário liga a chave "Controle pela rede", na tela Aparelho."
- **Trava 2.** Acrescentar: "O código aparece embaixo da chave, depois de ligada."
- **Trava 4.** ANTES: "Tudo visível. Cada comando da IA aparece na tela e fica no histórico. O botão "Parar IA" revoga o acesso na hora." DEPOIS: "Tudo registrado. Cada comando da porta fica no histórico, lido pela porta e fora do app, e o efeito dele aparece nos valores da tela. Desligar a chave "Controle pela rede" revoga o acesso na hora."
- **Trava 5.** DEPOIS: "Risco pede confirmação. Uma operação marcada em `chaves-de-acesso.md` como "exige confirmação" só roda depois do toque do usuário na folha de risco do app, que diz o que muda e o risco, sem mencionar a IA."
- **Uso ao mesmo tempo**, refeito:
  1. O padrão "controles travados até PARAR IA" sai, com o PARAR IA. Vale sempre a regra "o toque pausa": o toque do usuário pausa a tarefa da porta, e o app avisa a IA da pausa e manda cada mudança feita na tela.
  2. A IA retoma só depois de 10 s sem toques, e quem avalia o conjunto é a IA, fora do app.
  3. Um ajuste que o usuário acabou de mexer só pode ser mudado pela porta depois de 10 s sem toques nele; antes disso, a porta recusa com "o usuário está mexendo nisso".
  4. A tela não tem faixa nem aviso da IA. As mudanças aparecem só nos valores.
  5. Na criação, o usuário não mexe nos controles (a criação é uma tela toda sem abas). O ✕ é a saída.
- **Seção nova "Criação da memória (CAP-18)":**
  - A IA abre a criação para o Setup em uso. O app guarda a cópia de antes (failure-modes, regra 3) e mostra as perguntas.
  - Cada pergunta chega com o texto, 3 ou 4 respostas e "Outro…", e diz se é a última. O app mostra uma por tela e devolve a resposta (ou o texto do "Outro…") pela porta.
  - Só depois da resposta à última pergunta a porta aceita os ajustes da criação, cada um com a frase, o valor de antes, o valor novo e o motivo. O mesmo vale para o refino: a resposta do "Como ficou?" autoriza o passo seguinte.
  - Os ajustes da criação passam pelas mesmas chaves. O Continuar não vale ali: a última resposta já é o toque.
  - A IA não salva. Salvar e dar o nome são toques do usuário.
  - ✕ e Cancelar na folha de risco voltam à cópia de antes e fecham a criação, e a porta recebe o aviso.

### 4.3 chaves-de-acesso.md

- Linha "IA pela rede (porta de comandos da IA)" → **"Controle pela rede (porta de comandos)"**, com o risco "Deixa um computador da casa comandar o som pela rede." A chave fica na tela Aparelho.
- Regra do Continuar. DEPOIS: "**Sequência pela porta pede o toque.** Fora da criação da memória, a cada 3 ajustes pela porta sem um toque do usuário no meio, o próximo espera **Continuar** numa folha do app, que não menciona a IA. Um ajuste avulso continua sem confirmação. Na criação (CAP-18), a resposta à última pergunta autoriza os ajustes, e o Continuar não vale ali. (Decisão do Denis, 29/10/2026; folha e criação, 30/10/2026; o 3 é provisório e do Denis.)"

### 4.4 failure-modes.md

- **Regra 5.** ANTES: "Se só ganho, limitador, EQ ou MASTER diferem, avisa e oferece reaplicar, sem mutar." DEPOIS: "Se só ganho, limitador, EQ ou MASTER diferem, reaplica sozinho a memória e o preset em uso, em tela toda ("REAPLICANDO"), sem mutar (decisão do Denis, 30/10/2026)."
- **Regra 6 (nova): criação interrompida.** Se a conexão BLE cair, o app fechar ou a porta sumir no meio da criação, vale a regra 3: ao voltar, o app mostra "aplicado pela metade" e oferece voltar à cópia ou terminar. "Terminar" diz "O app não sabe refazer esta operação." quando os ajustes vieram da porta. Nada é salvo. [ASSUMPTION do facilitador: a UX não desenhou o erro no meio da criação (Questão em aberto 18). O desenho fica na 10d.]

### 4.5 EXPERIENCE.md (só marcar o que foi resolvido)

- Questão em aberto 1: marcar como **resolvida pela proposta de mudança de 30/10**, com D3 a D5.
- Questão 13: Cancelar na folha de risco = voltar ao som de antes (D5). O texto e quais ajustes contam como risco continuam abertos.
- Questão 22: a IA mexe no som fora da criação pela porta, sem nada na Roda além dos valores, com o Continuar em folha do app (D4).
- A linha 180 (diferença leve) perde o "precisa de correct-course".

### 4.6 stories.yaml

**Novas, com o comentário da proposta de 30/10:**

- **9c — Porta sem rosto e reaplicar sozinho.** Saem a página IA ativa, a página IA pela rede, o Histórico e a faixa da IA; a confirmação da IA vira a folha de risco do app, sem a palavra "IA"; a chave "Controle pela rede" com o código de pareamento vai para Aparelho; o Continuar vira folha do app; sai o modo "travado até PARAR IA" e o toque passa a sempre pausar; desligar a chave revoga as chaves pareadas; o histórico continua no núcleo e na porta. A conferência ao conectar reaplica sozinha a memória e o preset na diferença leve, em tela toda ("REAPLICANDO") (CAP-11, CAP-12, CAP-13, CAP-16). *Invoke:* esta proposta, seções 4.2 a 4.4. Um teste garante que nenhum texto de tela contém "IA". Contra o ProcessadorSimulado. Vem antes de a 12b continuar.
- **9d — Porta nos dois sentidos para a criação da memória.** Rotas da criação na porta (abrir, pergunta, resposta, ajustes com frase e motivo, resumo, "Como ficou?", cancelar); a cópia antes ao abrir; ajustes só depois da última resposta; Continuar fora da criação; volta à cópia no ✕ e no Cancelar da folha de risco; regra 6 de failure-modes (CAP-12, CAP-18). *Invoke:* porta-de-comandos.md, seção "Criação da memória". Núcleo e ViewModels testados contra o ProcessadorSimulado, sem telas. O cérebro não entra (D2): nos testes, as perguntas vêm de um cliente de teste.
- **10d — Telas da criação da memória.** "+ Nova memória", uma pergunta por tela com "Outro…", o palco (frase com o Foco e o valor numa linha, anel grande e apagado no chão), a passagem Pião e cambalhota, o Aplicando com o esquema de ligação, o resumo, o "Como ficou?", o Salvar, as folhas de cancelar e de risco e a chave "Animação ao criar o som" nas Configurações do app (CAP-18). *Invoke:* EXPERIENCE.md e DESIGN.md de 30/10 e `mockups/criacao-memoria-ia.html`. As telas da criação seguem o visual do BGS Sound, em tela toda e sem abas, então servem ao app de hoje e ao redesenho. [ASSUMPTION] Até a Roda existir, "+ Nova memória" fica na tela onde hoje se cria memória. Questões em aberto que a história resolve ou deixa explícitas: 12 (quando o nome é pedido), 14 (✕ no resumo e no Salvar), 15 (animação desligada), 18 (erro no meio), 20 e 21 (TalkBack e Voltar).

**Muda:**

- **12b.** Na lista de conferências, "ganho mexido sem salvar → só avisa" vira "ganho mexido sem salvar → reaplica sozinho ("REAPLICANDO")". Muda o bloco congelado da 12b, com a aprovação desta proposta.
- **12c.** ANTES: "… (parear, ler, ajustar, aplicar memória com confirmação na tela, recusas, PARAR IA, Continuar a cada 3 ajustes seguidos, tela ao vivo em até 1 s) …" DEPOIS: "… (ligar "Controle pela rede" em Aparelho e parear, ler, ajustar, aplicar memória com a folha de risco, recusas, desligar a chave revoga o acesso, Continuar a cada 3 ajustes seguidos, tela ao vivo em até 1 s, nenhuma tela com "IA"); uma criação de memória pela porta com o Claude Code mandando as perguntas à mão, até o Salvar do Denis; …"
- **11** (feita): só o título perde "IA e histórico" no comentário. A história não é reaberta.
- Comentário do fim: "spec própria do Agente da memória (perguntas adaptativas, escuta "1 ou 2", porta nos dois sentidos com modal, …)" → sem "porta nos dois sentidos com modal", que entrou na 9d.

## 5. Entrega

- **Escopo: moderado.** São 3 histórias novas e a ordem do resto muda, mas o plano não é refeito.
- **Quem faz:** o Developer (bmad-build ou bmad-build-auto) pega a 9c, depois a 9d e a 10d, nessa ordem. A 12b e a 12c continuam precisando do Denis e do aparelho.
- **Pronto quando:** a SPEC, os anexos, o EXPERIENCE e o `stories.yaml` dizem o que está na seção 4, e as histórias 9c, 9d e 10d passam com os testes verdes.
- **Commit:** só se o Denis pedir, no `main`, sem push (CLAUDE.md).

## 6. Fica para depois (não entra aqui)

- **Telas novas do BGS Sound** (Roda, Ajustes, Setup, Aparelho, Player e Presets no visual novo). O EXPERIENCE diz que o app continua fazendo o que a SPEC define, então o redesenho não muda a SPEC, mas precisa de histórias próprias. Fica para um plano à parte, depois desta correção.
- As outras Questões em aberto do EXPERIENCE (2 a 11 e 16 a 31) que não são da criação.
