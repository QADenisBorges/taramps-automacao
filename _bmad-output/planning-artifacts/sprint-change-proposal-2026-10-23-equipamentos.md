---
title: 'Proposta de mudança: equipamentos do Setup e esquema do Setup'
created: '2026-10-23'
status: 'aprovada pelo Denis em 23/10/2026'
escopo: moderado
spec_afetada: _bmad-output/specs/spec-app-controle-taramps
gatilho: história 10 (bloqueada por lacuna de intenção) e pedido do Denis de 23/10/2026
modo: batch (o Denis revisou no fim; o modelo de ligação foi refeito com ele)
---

# Proposta de mudança: equipamentos do Setup e esquema do Setup

## 1. O problema

**Como apareceu.** A história 10 (telas, parte 1) parou com o status blocked. Com o Link 3–4 desligado, as telas só alcançavam a saída 3 do Sub, e a saída 4 ficava sem ajuste. O EXPERIENCE.md dizia que esse caso "não está desenhado". As telas também tinham as três saídas fixas no código (JBL L = canal 1, JBL R = canal 2, Sub = canais 3 e 4).

**O que o Denis decidiu (23/10/2026).**
- O processador tem **4 canais de saída de linha**. No **Casa 2.1**: JBL L = CH1, JBL R = CH2, Sub = CH3 e CH4. Outro Setup pode ser bem diferente: 2 tweeters = CH1, Snake frontal L = CH2, Snake frontal R = CH3, JBL L e R traseiras = CH4.
- **Um canal pode ir para vários equipamentos ao mesmo tempo**, com cabo Y ou divisor, e todos recebem o mesmo sinal com a mesma configuração.
- **O bridge é do módulo, não do processador**: junta 2 canais do módulo numa saída de potência.
- Além de módulos, um canal pode ir para **caixas ativas** e outros equipamentos com entrada de linha.
- **Requisito novo, para entrar agora:**
  - ao adicionar um equipamento, definir em quais canais e saídas ele fica ligado;
  - o Setup aparece num esquema visual dinâmico e interativo, atualizado sozinho, que mostra cada equipamento pela categoria, função acústica e características técnicas (os exemplos não são uma lista fechada) e mostra os canais, as saídas e a distribuição das vias;
  - **por agora, o esquema é 2D com sensação de 3D**.
- **Modelo desconhecido:** quando ninguém sabe o modelo ou um dado técnico, o equipamento fica **genérico**.

**Tipo.** É um requisito novo do dono do produto. Ele resolve a lacuna da história 10: as saídas deixam de ser fixas e passam a vir do Setup.

**Conflito.** A CAP-16 do SPEC dizia "Componentes e limites físicos ficam para a spec do Rendimento". Os **componentes** entram agora; os **limites físicos** (potência, tensão, teto) continuam com o Rendimento.

## 2. O modelo de ligação (aprovado)

1. **Processador:** CH1 a CH4, saídas de linha.
2. **Ligação:** cada canal do processador vai para **uma ou mais entradas de linha** (cabo direto, Y ou divisor), de qualquer equipamento dos quatro grupos abaixo. Um equipamento do grupo 3 pode repassar o sinal para outros.
3. **Os quatro grupos de equipamentos com entrada de linha:**
   - **Grupo 1, amplificam e já tocam:** caixa ativa (full-range, de 2 ou 3 vias), subwoofer ativo, monitor de estúdio ativo, line array ativo, soundbar ou receiver com entrada AUX. A caixa ativa é um equipamento só, com as vias de dentro descritas (por exemplo, woofer de 12" + driver).
   - **Grupo 2, amplificam para alto-falantes passivos:** módulo ou amplificador (mono, 2, 4 ou mais canais) e amplificador integrado. O módulo tem os canais dele, e o **bridge** é uma opção dele (2 canais do módulo → 1 saída).
   - **Grupo 3, processam ou distribuem e repassam:** outro processador ou crossover ativo, equalizador externo, distribuidor de linha (line driver), transmissor sem fio.
   - **Grupo 4, só recebem:** interface de áudio, gravador, mesa de som.
4. **Alto-falantes passivos:** tweeter, driver, médio, médio-grave, woofer, coaxial, subwoofer, shaker. A lista é aberta e o usuário pode criar outras categorias. Cada um fica numa saída de um equipamento do grupo 2 (ou num bridge), e pode haver mais de um por saída.
5. **No app, o que se ajusta continua sendo o canal do processador.**
   - O nome do canal vem dos equipamentos que ele alimenta; sem equipamento, aparece "CH1" a "CH4".
   - O **link** junta canais vizinhos que precisam ficar iguais (1–2, 3–4) e eles viram **uma saída só na tela**. No Casa 2.1, o CH3 e o CH4 alimentam o módulo em bridge, então o Link 3–4 fica ligado e o Sub é uma saída que escreve nos dois.
   - Com o link desligado, cada canal é uma saída própria.
6. **Equilíbrio por saída:** cada saída do app (um canal, ou um par com link) pode ter um ganho e um limiter de equilíbrio, somados à memória como na 8c. No Casa 2.1, a JBL R (CH2) fica com −3 nos dois, igual a hoje.
7. **Genérico:** modelo e dados técnicos são opcionais; o que não se sabe fica "genérico" (por exemplo, "Subwoofer genérico", "Módulo genérico").

## 3. Impacto

| Área | Impacto |
|---|---|
| SPEC | Muda a CAP-16 (equipamentos, saídas do Setup, equilíbrio por saída); entra a CAP-17 (esquema); a CAP-3 passa a citar as saídas do Setup; muda a linha do Rendimento em "fora do escopo" |
| Histórias feitas (8c, 8d) | Nenhuma é desfeita. O Setup da 8c ganha equipamentos e ligações; o equilíbrio L/R vira equilíbrio por saída; o Casa 2.1 herdado passa a ter o Link 3–4 ligado. A pendência de "link com equilíbrio" some: um par com link é uma saída só, com um equilíbrio só |
| História 10 (bloqueada) | Volta com o patch salvo; as abas, os cartões e os faders vêm das saídas do Setup; entra a tela do Setup com os equipamentos |
| Histórias novas | **8e** (núcleo dos equipamentos, ligações e saídas) antes da 10; **10b** (o esquema) depois da 10 |
| 11 e 12 | A 11 usa as saídas do Setup; a 12 confere no aparelho o Sub nos dois canais e o esquema do Casa 2.1 |
| UX | EXPERIENCE.md e DESIGN.md: a tela do Setup, a folha de equipamento, as saídas dinâmicas, o esquema e uma cor por canal |
| Código | `Setup`/`GerenteSetups` e `Camadas` (8c), `ConhecimentoHerdado`, e a tentativa da 10 (`Saidas.cs` fixa) |
| Tecnologia | Esquema em 2D com sensação de 3D (perspectiva isométrica, profundidade, sombra) no `GraphicsView` do .NET MAUI, sem biblioteca nova e sem internet. O 3D de verdade fica para depois |

## 4. Caminho escolhido

**Ajuste direto, com duas histórias novas.**
- **Desfazer foi descartado:** a 8c e a 8d continuam valendo.
- **Revisar o MVP também foi descartado:** o POC 1 só ganha uma capacidade.
- **Esforço:** médio. A 8e leva cerca de 1 h, a volta da 10 de 1 h a 1h30 e a 10b de 1 h a 1h30.
- **Risco:** médio. O maior é o esquema no celular (desempenho e toque); depois vem a migração do `setups.json`.
- **Prazo:** o POC 1 não tem data; a 12 continua sendo a última.

## 5. Mudanças aprovadas

### 5.1 SPEC

- **CAP-3 (intent):** acrescentar que as saídas na tela são as do Setup (um canal, ou um par com link) e que, com o link ligado, uma saída escreve nos dois canais.
- **CAP-16 (intent):**
  - ANTES: "Cada Setup tem nome e calibração (atraso, fase, corte, equilíbrio L/R de ganho e de limiter, rota, link e EQ paramétrico de entrada) … Componentes e limites físicos ficam para a spec do Rendimento."
  - DEPOIS: Cada Setup tem nome, **equipamentos** e calibração (atraso, fase, corte, **equilíbrio de cada saída** de ganho e de limiter, rota, link e EQ paramétrico de entrada), mais o modelo de ligação da seção 2 desta proposta. Os limites físicos ficam para a spec do Rendimento.
  - **success (acrescentar):** "No Casa 2.1, o Sub aparece como uma saída só e mexer nele muda o CH3 e o CH4. Num Setup com 4 equipamentos em 4 canais e sem link, aparecem 4 saídas com os nomes deles."
- **CAP-17 (nova), esquema do Setup:**
  - **intent:**
    - O Setup aparece num esquema interativo em 2D com sensação de 3D (perspectiva isométrica, profundidade e sombra): arrastar para mudar o ângulo, aproximar com os dedos, tocar num equipamento para ver e editar.
    - O esquema se atualiza sozinho.
    - Cada equipamento é desenhado pela categoria, pela função acústica e em proporção pelas características (o diâmetro, por exemplo).
    - O esquema mostra o processador com os 4 canais, os cabos e os Y, os equipamentos de cada grupo, as saídas e os bridges dos módulos, os alto-falantes e a cor de cada canal.
    - A função acústica pode ser sugerida pela categoria e pelo corte do canal, e o usuário pode trocar.
    - O 3D de verdade fica para uma versão futura.
  - **success:** "Adicionar, mudar ou tirar um equipamento muda o esquema na hora. No Casa 2.1, o esquema mostra a JBL L no CH1, a JBL R no CH2 e o Sub no módulo em bridge alimentado pelo CH3 e pelo CH4."
- **Fora do escopo, linha do Rendimento:** acrescentar "Os limites físicos por equipamento (potência, tensão, teto) usam os equipamentos cadastrados na CAP-16."
- **Entrega (pedaço 2):** acrescentar a CAP-17.

### 5.2 Documentos que acompanham o SPEC

- **conhecimento-herdado.md:** o Casa 2.1 ganha os equipamentos:
  - JBL L (CH1) e JBL R (CH2): JBL 4TRMS80, 3,6 Ω, passa-alta mínimo de 100 Hz a 24 dB/oit, cada uma num canal de um "Módulo genérico" (×19,6);
  - "Subwoofer genérico" no bridge do mesmo módulo (×40), alimentado pelo CH3 e pelo CH4;
  - Link 3–4 ligado.
- **failure-modes.md** e **chaves-de-acesso.md:** sem mudança. Os equipamentos não vão pelo BLE e não têm chave.

### 5.3 Histórias

Nova ordem: **8e → 10 → 10b → 11 → 12**.

| id | Título | Conteúdo | CAPs |
|---|---|---|---|
| 8e | Equipamentos, ligações e saídas do Setup | Os equipamentos (quatro grupos, categorias abertas, função, dados técnicos opcionais e "genérico"); as ligações (canal → uma ou mais entradas, repasse do grupo 3, canais e bridge do módulo, alto-falantes nas saídas); as saídas do app (canal, ou par com link) com nome e cor; o equilíbrio por saída no lugar do L/R; o Casa 2.1 herdado com os equipamentos e o Link 3–4 ligado; a migração do `setups.json`; o ViewModel da lista e da folha de equipamento; tudo testado contra o simulador | 3, 16 |
| 10 | Telas, parte 1 (muda) | Volta com o patch salvo; as abas, os cartões e os faders vêm das saídas do Setup; entra a tela do Setup com a lista de equipamentos e a folha (o lugar do esquema fica reservado para a 10b) | 2, 3, 4, 14, 16 |
| 10b | Esquema do Setup (2D com sensação de 3D) | Desenho isométrico no `GraphicsView`; a forma de cada categoria gerada pelo código; o esquema ligado ao Setup em tempo real; arrastar, aproximar e tocar para abrir o equipamento | 17 |
| 11 | (muda pouco) | As telas dela usam as saídas do Setup | — |
| 12 | (ganha itens) | Conferir no aparelho o Sub nos dois canais e o esquema do Casa 2.1 | — |

### 5.4 UX

- **EXPERIENCE.md:**
  - entra a tela do **Setup** (Configurações › Setups › tocar num Setup), com o esquema em cima e os equipamentos embaixo, e a **folha de equipamento** (grupo, categoria, nome, modelo, função, dados técnicos, onde está ligado);
  - as abas e os cartões vêm das saídas do Setup, e o equilíbrio vale para qualquer saída;
  - estado novo **"Setup sem equipamentos"**: aparecem CH1 a CH4;
  - o `[NOTE FOR UX]` do link 3–4 sai, resolvido.
- **DESIGN.md:**
  - troca as três cores de saída por **uma cor por canal** (`canal-1` a `canal-4`); um par com link usa a cor do canal mestre;
  - entram os componentes "esquema do Setup" e "folha de equipamento".
- **Sem sessão de UX à parte:** o pedido dá liberdade de desenho; os dois documentos são atualizados nesta sessão.

## 6. Entrega

**Escopo moderado:** o backlog e o contrato mudam, sem replanejar o POC.

| Quem | O quê |
|---|---|
| Dev (esta sessão) | Aplicar 5.1 a 5.4 no SPEC, nos documentos, no UX e no `stories.yaml`, e arquivar a spec bloqueada da 10 (a nova é escrita na cadeia, com o patch salvo) |
| Dev (`bmad-build-auto`, em cadeia) | 8e, a 10 (com o patch), a 10b e a 11; para antes da 12 |
| Denis | A 12, no aparelho |

**Pronto quando:**
- o SPEC, os documentos, o UX e o `stories.yaml` mostram as mudanças;
- a 8e, a 10 e a 10b passam nos testes contra o simulador, com o build sem avisos;
- no Casa 2.1, o Sub escreve no CH3 e no CH4 juntos;
- o esquema muda sozinho ao mexer num equipamento;
- a 12 confere tudo no aparelho.
