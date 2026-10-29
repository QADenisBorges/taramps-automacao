---
title: 'Proposta de mudança: Setup só físico, memória com toda a regulagem e o agente que cria a memória'
created: '2026-10-29'
status: 'aprovada pelo Denis em 29/10/2026 e aplicada na spec e no UX'
nota_da_aplicacao: >-
  Consequência de D2 escrita na aplicação: um Setup sem memória (o Casa 2.1 na
  primeira conexão) cai no Setup em dúvida, e Escolher o Setup desmuta sem
  aplicar nada.
escopo: moderado
spec_afetada: _bmad-output/specs/spec-app-controle-taramps
gatilho: _bmad-output/forge/montagem-do-setup/forged-idea.md (forja de 29/10/2026, hardened)
modo: batch
---

# Proposta de mudança: Setup só físico e o agente que cria a memória

## 1. O problema

**Como apareceu.** O Denis não entendia como se monta um Setup. Na forja de 29/10/2026 ficou claro por quê: o Setup pede dados dos equipamentos que nada usa, e mistura o físico com a regulagem (atraso, fase, corte, equilíbrio, rota, EQ de entrada).

**O que o Denis decidiu (forja de 29/10/2026).**
- **Setup = só o físico:** falantes, canais, módulos, ligações e fichas. É montado antes da memória.
- **Memória = toda a regulagem**, inclusive o atraso. Isso fecha uma contradição antiga: a regra dele é atraso 0 na música e compensação pela distância só na FILME, mas o atraso estava no Setup e valia para todas as memórias.
- **Conferência ao conectar** pela última memória aplicada naquele Setup.
- **Ficha conferida e guardada numa base, com a origem** de cada dado.
- **Aplicar com um toque** e fechar a brecha de a IA trocar o som inteiro ajuste por ajuste.
- **Um agente com skills** cria a memória por perguntas adaptativas e escuta. Ele roda pelo Claude Code no notebook, com a porta nos dois sentidos e um serviço que o acorda; mais tarde vai para dentro do app.
- **As memórias e calibrações de hoje não são referência** para o agente.

**Tipo.** Requisito novo do dono do produto, com correção de um modelo que não seguia a regra dele.

## 2. Impacto

| Área | Impacto |
|---|---|
| SPEC | CAP-4 e CAP-16 mudam (camadas, conferência); CAP-6 muda (herdado); CAP-17 muda pouco (sugestão de função); Non-goals e Constraints citam a forja |
| failure-modes.md | Regra 5 (conferência ao conectar) muda |
| chaves-de-acesso.md | Entra a regra que fecha a brecha do ajuste por ajuste |
| conhecimento-herdado.md | Saem a memória FILME e o preset Cinema herdados; o Casa 2.1 fica com os equipamentos e as fichas com origem |
| EXPERIENCE.md | Três camadas, Conectar e Setup, Salvar por camadas, Ajuste da saída (sai o equilíbrio), folha de equipamento, Fluxos 3 e 7, Uso junto com a IA |
| Histórias feitas (8c, 8e, 10, 10b, 11) | Nenhuma é desfeita. O que muda entra em duas histórias novas |
| Histórias novas | **8f** (núcleo) e **10c** (telas), depois da 12a e antes da 12b |
| 12b e 12c | A 12b confere no aparelho o modelo novo; a 12c ganha a brecha fechada |
| Código | `Setups/Camadas.cs` (a regra das camadas), `Setup.cs`, `GerenteSetups.cs`, `Equipamentos.cs`, `Memorias/GerenteMemorias.cs`, `Herdado/ConhecimentoHerdado.cs`, `Porta`, `Seguranca`, `Telas/SalvarViewModel.cs` e as telas do Setup; `setups.json` vai ao formato 3, com migração |
| Spec nova | **Agente da memória**, depois da 12 e da spec do Rendimento (seção 3) |

## 3. Caminho recomendado: ajuste direto + spec nova para o agente

- **Ajuste direto (agora, antes da 12b):** o modelo de dados do app, a conferência, as fichas e a brecha. É o que muda as telas que a 12b vai testar no aparelho; testar antes e mudar depois seria retrabalho. Esforço médio, risco baixo: a regra das camadas está num lugar só (`Camadas.cs`), e o padrão de migração já existe desde a 8e.
- **Spec nova (depois):** o agente e o que só ele usa (porta nos dois sentidos com modal, escuta "1 ou 2", serviço que acorda, as 7 skills, o aprendizado e os ouvintes simulados). Motivos:
  - o agente vive no notebook, não no app, e a SPEC continua valendo: "não há IA dentro do app";
  - ele precisa da **cerca física em código**, que é o limite físico da spec do **Rendimento**;
  - é grande: 7 skills, testes com ouvintes simulados e mudanças na porta.
- **Ordem sugerida:** 8f → 10c → 12b → 12c → spec do Rendimento (cerca) → spec do Agente.
- **Rollback:** não vale. Nada feito precisa ser desfeito; o código muda para frente, com migração.
- **MVP:** os 4 testes do sinal de sucesso continuam iguais em espírito. O teste 3 compara depois de carregar a FILME do aparelho, e o teste 4 passa a falar de "corte, atraso, fase ou rota diferentes da última memória aplicada" em vez de "estado de outro Setup".

## 4. Decisões do Denis (29/10/2026)

| # | Decisão |
|---|---|
| D1 | **O link é automático, tirado das ligações do Setup:** dois canais vizinhos que alimentam o mesmo bridge ficam com o link ligado; senão, sem link. O usuário não mexe nele. Na ficha, o que a IA não achar o usuário digita, e o dado que ele não souber e não for essencial fica em branco. |
| D2 | **O app começa do zero:** saem as memórias e os presets do celular, inclusive a FILME e o Cinema herdados. As 3 memórias do processador ficam intocáveis (o app só lê os nomes e carrega). Cada memória nova guarda o valor final; o equilíbrio do Setup sai. |
| D3 | **Na conferência ao conectar,** muta quando difere corte, atraso, fase ou rota; só avisa (com Reaplicar, sem mutar) quando difere ganho, limitador, EQ ou MASTER. |
| D4 | **A cada 3 ajustes seguidos da IA** sem um toque do usuário no meio, o próximo espera Continuar (valor provisório, do Denis). |
| D5 | **O agente vai para uma spec própria,** depois do Rendimento. |

## 5. Mudanças propostas, por arquivo

### 5.1 SPEC.md

**CAP-4, intent.**

ANTES: "…guarda o caráter do som (limiter/compressor, EQ de saída, nível de cada canal e o resto fora da calibração do Setup e das 15 bandas)… cada mudança vai para a camada dona dela (calibração → Setup; caráter → memória; 15 bandas → preset, CAP-5)… O ganho final e o limiter final de cada canal são o valor da memória mais o equilíbrio do Setup; o app soma os dois ao aplicar…"

DEPOIS: "…guarda **toda a regulagem, com o valor final** (atraso, fase, cortes, rota, EQ de entrada, ganho, limitador, EQ de saída e MASTER; tudo menos as 15 bandas, o link e o que fica fora das camadas) e a playlist (CAP-15)… cada mudança vai para a camada dona dela (regulagem → memória; 15 bandas → preset, CAP-5); o link vem sozinho do Setup (D1)…" Sai a frase da soma com o equilíbrio (D2).

**CAP-4, success.** ANTES: "a releitura mostra a calibração do Setup, o caráter da memória e o preset escolhido." DEPOIS: "a releitura mostra a regulagem da memória, o link tirado do Setup e o preset escolhido."

**CAP-2, success.** "também depois de aplicar a memória herdada FILME" → "também depois de carregar a FILME do aparelho (`241`)" (D2).

**CAP-16, intent.** Reescrita, mantendo o modelo de ligação da proposta de 23/10:
- "Cada Setup tem nome e **equipamentos com as fichas**. A regulagem fica na memória (forja de 29/10/2026)."
- **Link automático (D1):** dois canais vizinhos que alimentam o mesmo bridge ficam com o link ligado; senão, sem link. O app tira isso das ligações e escreve no aparelho; não há controle de link para o usuário.
- **Fichas:** dados técnicos opcionais, cada um com a **origem**: oficial (manual ou ficha do fabricante, com o endereço), outra fonte, medido ou digitado. Um dado sem origem confirmada nunca aparece como verificado. O que não se sabe fica em branco. Campos novos, além dos de hoje: Re medida, Fs, Qes, Qms, Vas, Bl, Sd, Xmax e Le (alto-falante); tipo, volume, Fb e área do duto (caixa); potência e tensão de ceifamento por canal e em ponte, ganho medido (módulo); tensão e corrente (fonte).
- **Base de modelos:** uma ficha confirmada fica guardada no celular por fabricante e modelo, é reaproveitada em outros Setups e completa o campo enquanto o usuário digita.
- **Conferência ao conectar:** compara o que leu com a **última memória aplicada naquele Setup** e o link do Setup. Corte, atraso, fase ou rota diferentes → para o som, muta os 4 canais e pede para escolher; ganho, limitador, EQ ou MASTER diferentes → entra sem mutar, com o aviso e Reaplicar (D3). O resto do texto (mudo no aparelho, IA bloqueada, tocar no nome do Setup) fica.
- Sai "calibração (atraso, fase, corte, equilíbrio…)" e "criar um Setup novo com a calibração do aparelho" vira "criar um Setup novo e guardar o som do aparelho como memória nova dele".
- Sai "Os limites físicos ficam para a spec do Rendimento (…os componentes entram agora)"; entra "Os limites físicos usam as fichas e ficam para a spec do Rendimento".

**CAP-16, success.** ANTES: "Com o aparelho no estado de outro Setup, nada é aplicado nem tocado sem a confirmação na tela." DEPOIS: "Com corte, atraso, fase ou rota diferentes da última memória aplicada, nada é aplicado nem tocado sem a confirmação na tela." Entra: "Um dado de ficha mostra a origem dele; um modelo já guardado na base completa a ficha sozinho."

**CAP-6.** "a leitura da FILME de 18/10 dividida em Setup 'Casa 2.1', memória FILME e preset 'Cinema', as notas das memórias do aparelho e as preferências do Denis" → "o Setup 'Casa 2.1' (equipamentos e fichas com origem), as notas das memórias do aparelho e as preferências do Denis. O app nasce **sem memórias e sem presets do celular** (D2)."

**CAP-5.** Sai "um preset herdado não é sobrescrito" (não há mais preset herdado).

**CAP-17.** "A função acústica pode ser sugerida pela categoria e pelo corte do canal" → "…pelo corte do canal na memória em uso".

**CAP-12, success.** Entra: "Uma sequência de ajustes da IA sem um toque do usuário no meio espera Continuar na tela (chaves-de-acesso.md)."

**Constraints.** Entra: "O Setup é só o físico; a regulagem é da memória (forja de 29/10/2026)."

**Non-goals.** "IA dentro do app (fica para o BGS Sound Lab)" → "IA dentro do app: fica para depois; primeiro, o agente pela porta (forja de 29/10/2026)." Entra: "O agente que cria a memória (perguntas, escuta, aprendizado e testes): spec própria, depois do Rendimento, a partir de `../../forge/montagem-do-setup/forged-idea.md`."

**Success signal, teste 3.** "…e depois de aplicar a FILME herdada" → "…e depois de carregar a FILME do aparelho" (D2).

**Success signal, teste 4.** "Com o processador no estado de outro Setup…" → "Com o processador com corte, atraso, fase ou rota diferentes da última memória aplicada…"

**sources:** entra `../../forge/montagem-do-setup/forged-idea.md` e esta proposta.

### 5.2 failure-modes.md, regra 5

Reescrita com a conferência nova (D3). Ficam: o mudo no aparelho até a decisão, a IA bloqueada, a reaplicação depois da confirmação. As três saídas da dúvida passam a ser: **Escolher o Setup** (aplica a memória ativa dele, relê e desmuta), **Criar Setup novo** (o som do aparelho vira a primeira memória dele) e **Seguir sem Setup**.

### 5.3 chaves-de-acesso.md

Entra uma regra que vale para qualquer chave: "A cada **3** ajustes da IA (D4) sem um toque do usuário no meio, o próximo espera **Continuar** na tela. Um ajuste avulso continua sem confirmação."

### 5.4 conhecimento-herdado.md

- **Setup "Casa 2.1":** os equipamentos de hoje (o Link 3–4 sai sozinho do bridge) e as fichas com origem: JBL 4TRMS80 (ficha impressa, oficial), Protech Light 8" (site do fabricante, 27/10, oficial), SD800.4 EVO (manual, oficial), Re e ganho do módulo (medido). Essas três fichas entram na base de modelos.
- **Saem** a seção "A FILME dividida nas três camadas", o preset "Cinema" e a frase da soma com o equilíbrio (D2).
- **Ficam** as notas das memórias do aparelho (só para escolher qual carregar) e as preferências do Denis.
- Entra uma nota: "As memórias do aparelho são intocáveis e não são referência para o agente (forja e decisão de 29/10/2026)."

### 5.5 EXPERIENCE.md

- **Três camadas do som:** Setup = nome, equipamentos e fichas (o link sai deles); Memória = toda a regulagem com o valor final, a playlist e o preset lembrado; Preset = 15 bandas.
- **Começo do zero:** sem memórias nem presets herdados; o cartão "Nenhuma memória" é o estado inicial do Casa 2.1. Somem os selos HERDADO de memória e preset e a trava do "Atualizar o Cinema".
- **Link:** some o Link 1–2 e 3–4 do Avançado do Crossover; a tela mostra só, em uma linha, "CH3 e CH4 ligados: alimentam o mesmo bridge".
- **Conectar e Setup:** a tabela do que o app lê passa a usar D3; sai o parágrafo "O equilíbrio na conferência"; "O que não bate" lista as diferenças contra a memória ativa ("Atraso do Sub: 80 cm na FILME, 0 cm no aparelho").
- **Salvar por camadas:** a regulagem vai para a memória em uso e as 15 bandas para o preset; somem a pergunta "Manter a calibração do Casa 2.1 ou atualizar" e a soma com o equilíbrio.
- **Ajuste da saída:** sai o equilíbrio do Setup em Avançado.
- **Folha de equipamento:** os campos novos da ficha, a origem de cada dado (selo: oficial, outra fonte, medido, digitado) e o modelo que se completa sozinho pela base.
- **Fluxo 3** (noite de filme) e **Fluxo 7** (carro): reescritos com a conferência nova; no carro, "Criar Setup novo" guarda o som do aparelho como primeira memória do Carro. Nos Fluxos 2 e 3, a FILME passa a ser uma memória criada do zero no celular.
- **Fluxo 5** (conferir contra o app da Taramps): a paridade é feita depois de **carregar a FILME do aparelho**.
- **Uso junto com a IA:** entra a regra do Continuar (D4).

### 5.6 stories.yaml

Entram duas histórias depois da 12a:

```yaml
- id: "8f"
  title: Setup só físico, memória com toda a regulagem e fichas com origem
  description: >-
    A regra das camadas muda: a regulagem vai para a memória com o valor
    final, o Setup fica com equipamentos e fichas, o link sai sozinho das
    ligações e o equilíbrio sai. Conferência ao conectar pela última memória
    aplicada (muta só com corte, atraso, fase ou rota diferentes). Fichas com
    os campos novos e a origem de cada dado, base de modelos no celular,
    herdado sem memórias e sem presets, setups.json no formato 3 (começa do
    zero: apaga as memórias e os presets do celular e mantém os Setups e os
    equipamentos), e a regra do Continuar na porta.
  invoke_dev_with: >-
    Proposta de 29/10/2026 e forged-idea da montagem do Setup. Tudo contra o
    ProcessadorSimulado. Sem telas.
- id: "10c"
  title: Telas do Setup físico e da memória completa
  description: >-
    Salvar por camadas sem calibração, Setup em dúvida contra a memória
    ativa, Ajuste sem equilíbrio, folha de equipamento com os campos novos,
    a origem e o modelo que se completa pela base, e o aviso do Continuar
    da IA.
  invoke_dev_with: >-
    EXPERIENCE.md atualizado em 29/10/2026.
```

**12b, invoke_dev_with:** entra "Conferir no aparelho a conferência nova (muta com corte diferente, só avisa com ganho diferente), o link que sai sozinho do bridge e o app começando sem memórias; as 3 memórias do processador continuam como estavam."

**Comentário do fim:** entra "Depois do Rendimento: spec própria do Agente da memória, a partir de `_bmad-output/forge/montagem-do-setup/forged-idea.md`."

### 5.7 O que fica para a spec do Agente (não muda agora)

Porta nos dois sentidos com modal, escuta "1 ou 2" com volume igualado, serviço no notebook que acorda o agente, as 7 skills, a parada em 1 dB, o aprendizado, os ouvintes simulados e a decisão sobre salvar no fim do fluxo. Hoje vale "a IA não salva"; a proposta para a spec do Agente é que, ao fim, o modal mostre o **Salvar do usuário**, sem a IA ganhar comando de salvar.

## 6. Entrega

- **Escopo:** moderado. Reorganiza o backlog (duas histórias novas) e abre uma spec futura.
- **Quem faz:** eu, ao aprovar, aplico as edições dos itens 5.1 a 5.6 nos arquivos da spec e do UX, num commit no `main`. Depois, a 8f e a 10c pelo `bmad-build-auto` (que encadeia as duas e para na 12b, que precisa do aparelho).
- **Pronto quando:**
  - a regra das camadas não tem mais calibração nem equilíbrio;
  - o link liga sozinho quando dois canais alimentam o mesmo bridge e desliga quando não;
  - depois da migração, o celular não tem memórias nem presets, os Setups e os equipamentos ficam, e nenhum `240` ou `23N` sai do app;
  - a conferência muta com corte diferente e só avisa com ganho diferente, no simulador;
  - uma ficha mostra a origem e a base completa um modelo já guardado;
  - a quarta escrita seguida da IA espera Continuar.
