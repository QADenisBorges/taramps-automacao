---
title: 'História 13a: limite físico e cerca'
type: 'feature'
created: '2026-10-31'
baseline_revision: '3234a4db58ba2f2d19605bc5c4194a933335c520'
status: 'done'
review_loop_iteration: 1
followup_review_recommended: true
context:
  - '{project-root}/_bmad-output/specs/spec-rendimento/SPEC.md'
  - '{project-root}/_bmad-output/specs/spec-rendimento/limites-e-niveis.md'
warnings:
  - oversized
deferred:
  - summary: >-
      Mudanças que movem o teto sem escrever 071/040/042 (trocar de Setup, ficha, ganho medido, link) não conferem o limitador que já está no aparelho.
    evidence: |-
      Cerca.Conferir só olha as saídas que o lote toca; escolher um Setup cujos 071/040/042 já batem não confere nada. Pergunta ao Denis na 1ª pausa, ainda sem resposta.
    location: >-
      app-controle/src/BgsControle.Nucleo/Setups/GerenteSetups.cs (AplicarAlvoAsync)
    severity: medium
  - summary: >-
      Uma exceção no cálculo ainda faz a volta à cópia falhar com MsgFalhou e deixa a operação pela metade.
    evidence: |-
      CopiaAntes.EscreverCopiaAsync devolve Falha(MsgFalhou) quando TetosNaVolta lança; uma saída segura (limitador em −48 em todos os canais) pede decisão.
    location: >-
      app-controle/src/BgsControle.Nucleo/Escrita/CopiaAntes.cs
    severity: medium
  - summary: >-
      A reaplicação na conferência recusada pela cerca só fica no log; o usuário não sabe que a memória ativa passa do teto.
    evidence: |-
      Teste A_reaplicacao_na_conferencia_acima_do_teto_nao_envia_nada; a superfície é da 13g e o revalidar da 13c.
    location: >-
      app-controle/src/BgsControle.Nucleo/Setups/GerenteSetups.cs (ReaplicarNaConferenciaAsync)
    severity: medium
  - summary: >-
      Uma escrita da tela ou da porta entre o Conferir do lote e as escritas dele (loteConferido) pode passar sem ver o lote.
    evidence: |-
      Não verificado: falta conferir se a vez da operação e o Bloqueio da marca (IniciarAsync) já fecham a janela.
    location: >-
      app-controle/src/BgsControle.Nucleo/Setups/GerenteSetups.cs, Escrita/Escritor.cs
    severity: medium (unverified)
---

<intent-contract>

## Intent

**Problem:** O app não sabe o limite físico de nenhuma saída. O limitador aceita qualquer valor, inclusive OFF, venha da tela, da porta ou da criação. Por isso o Rendimento (spec-rendimento CAP-1 e CAP-2) não tem base.

**Approach:**
- Um cálculo no núcleo (`Rendimento/`) tira, das fichas do Setup em uso e do corte de cada saída, três limites e a conversão para o limitador:
  - o térmico (T);
  - a excursão (X), com o modelo eletromecânico em caixa selada ou dutada e os três casos de dado faltando;
  - o ceifamento do módulo (C).
- Cada limite vem com a conta e o selo.
- Uma cerca no `Escritor`, conferida antes de qualquer envio, recusa o limitador OFF ou acima do teto.
- A porta lê os limites em `GET /limites`.

## Boundaries & Constraints

**Always:**
- **Caminho da saída:** para cada canal do processador (0–3), o app segue as ligações do Setup em uso até os alto-falantes passivos. Cada um vem com o módulo e o canal ou o bridge que o alimenta.
  - O limite da saída é o menor entre os alto-falantes dela.
  - Um canal sem alto-falante passivo (só uma caixa ativa, ou nada) fica "sem limite", com o motivo, e a cerca não vale ali.
- **T** = √(PotenciaRms × Impedancia), com os dois dados do alto-falante.
- **C** = `TensaoCeifamentoCanal` do módulo; no bridge, `TensaoCeifamentoPonte`.
- **X**, a tensão RMS de um tom em que o cone chega ao Xmax na pior frequência (de 10 Hz a 20 kHz, com uma grade logarítmica de pelo menos 4000 pontos), com o passa-alta da saída:
  - Os dados vêm do alto-falante e da caixa: Re, Fs, Qes, Qms, Vas, Sd, Xmax, `TipoCaixa` e `VolumeCaixa`; Bl e Le são opcionais; na dutada, também Fb.
  - Cms = Vas/(ρc²Sd²) e Mms = 1/((2πFs)²·Cms), com ρ = 1,204 e c = 343. Sem Bl, ele sai do Qes: √(2πFs·Mms·Re/Qes). Sem Le, Le = 0.
  - **Selada:** x = (Bl·V·√2/Re) / (Mms·s² + (Rms + Bl²/Re)·s + 1/Cms + ρc²Sd²/Vb). É o `calculo_excursao_jbl.py`.
  - **Dutada:** o modelo de Small com Ze = Re + s·Le, perda Ql = 7 e o duto sintonizado na Fb, como no `calculo_excursao_sub.py`.
  - O filtro entra só como módulo: |BTn| = 1/√(1+(fc/f)^(2n)) e |LRn| = 1/(1+(fc/f)^n), com n = inclinação/6. Sem passa-alta (OFF), |H| = 1.
  - `TipoCaixa` é texto livre: começa com "selad" → selada; começa com "dutad", "duto" ou "bass reflex" → dutada (sem diferença de caixa nem de acento); o resto conta como dado que falta.
- **Os três casos de X** (`limites-e-niveis.md`):
  1. Os dados completos dão X pela conta.
  2. Senão, uma caixa dutada com Fb e passa-alta de pelo menos 24 dB/oit com 0,75·Fb ≤ fc ≤ 0,8·Fb dá X = ∞, com o selo "estimado".
  3. Senão, X = min(T, C) − 3 dB, com o selo "estimado" e o aviso do dado que falta (ex.: "Falta a Fb da caixa do Sub.").
  
  Nunca bloqueia o uso.
- **Selo:** cada limite é "verificado" só se todos os dados dele são verificados (oficial ou medido). Senão, leva "estimado".
- **O que manda:** o menor de T, X e C. Um limite que não dá para calcular (sem o dado) fica de fora do mínimo e aparece com o motivo. Sem nenhum dos três, a saída fica sem limite.
- **Conversão:** dB = 20·log10(V / (2,0 × ganho)). O ganho é o `Ganho` do módulo (no bridge, `GanhoPonte`) com a origem "medido".
  - Sem esse ganho, a saída fica "sem calibração": mostra os volts, dB = null e a cerca não vale.
  - O dB é arredondado para baixo, no passo do limitador (0,1).
- **A cerca:**
  - **Onde:** fica num único ponto do `Escritor`, antes da fila e do envio, e vale também com `daCopia`. Assim pega a tela, o `/ajuste`, a criação, a cópia do link, o aplicar da memória, a reaplicação, a volta à cópia e o desfazer.
  - **O que confere:** o estado do aparelho como ficaria depois da escrita, com o espelho e a escrita por cima. Uma escrita é recusada se, depois dela, alguma saída calibrada e com limite que ela toca fica com o limitador OFF (`071` = 0,5) ou com o limiar acima do teto.
  - **Que escritas tocam a saída:** as do `071`, e as do `040`/`042`, que mudam o X.
  - **Teto:** sem nível em uso, é min(T, X, C) (os níveis são da 13c).
  - **Lotes que aplicam** (aplicar memória, reaplicar, escolher Setup): o lote inteiro é conferido antes da primeira escrita. Com uma recusa, nada sai, e o resultado falha com o motivo.
  - **Lotes que voltam** (voltar à cópia e desfazer, inclusive o cancelar da criação; decisão do Denis de 31/10/2026): voltam ao som de antes com o limitador de cada saída calibrada e com limite em min(valor da cópia, teto). Um OFF vira o teto, e o resto da cópia fica igual. O teto é calculado com o corte da própria cópia. A volta nunca é recusada pela cerca, e a escrita no teto é registrada no log.
  - **Teto abaixo de −48 dB** (o mínimo do limitador): o teto da cerca fica em −48 dB, e `GET /limites` traz o aviso "O limitador não chega ao limite físico desta saída.". A saída não trava.
  - **Valor desconhecido:** um `071` ausente do espelho, ou uma escrita que não decodifica, numa saída calibrada e com limite é recusado com "Limite da {saída}: o app não sabe o valor do limitador; releia o aparelho.". Uma exceção no cálculo recusa, com `MsgFalhou`, em todos os caminhos, inclusive no lote e no `/limites`, que devolve 500 com a mensagem.
  - **Mensagem:** "Limite: {saída}, {teto} dB, pelo {térmico|curso do cone|ceifamento do módulo}.", com o dB em vírgula e sinal; o canal culpado vem entre parênteses quando a saída tem dois canais. O OFF dá "Limite: {saída}, o limitador não pode ficar desligado.". A recusa de um corte (`040`/`042`) acrescenta "Baixe o limitador antes de mudar o corte.".
  - A cerca não tem chave nem exceção.
- **Porta:** `GET /limites` traz, por saída do Setup em uso:
  - `saida`, `canais`, `calibrada`, `teto` (dB ou null), `tetoV`, `manda` (`termico`, `excursao` ou `ceifamento`) e `estimado`;
  - cada limite com `v` (null, ou o texto "infinito" no caso 2), `selo`, `conta` (texto) e `falta`.
  
  Sem Setup em uso, devolve uma lista vazia. A recusa do `/ajuste` e do `/criacao/ajuste` é 409 com a mensagem. A ajuda documenta a rota.
- Os números conferem com o exemplo, dentro de 0,2 dB:
  - JBL 4TRMS80 (Re 3,4 da ficha), caixa selada de 3,5 L, LR24 em 200 Hz, ganho ×19,5: T 17,9 V; X 22,2 V; C 23 V; teto −6,8 dB pelo térmico. Com LR24 em 150 Hz, X = 12,9 V.
  - Protech Light 8" na ponte ×40,5, dutada de 28 L, Fb 51 Hz, BT24 em 40 Hz: T 42,4 V; C 40 V; X 33,1 V; teto −7,8 dB pelo curso.

**Never:**
- Telas e XAML (13g).
- Níveis e master (13c).
- Calibração guiada (13b).
- Recusar escritas que não são `071`/`040`/`042`.
- Vigiar o EQ.
- Usar o guardião antigo ou as memórias do processador como referência.
- Nenhum `240`, `241` ou `23N`.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| JBL do exemplo | Fichas do exemplo, LR24 200 Hz, ganho ×19,5 medido | T 17,9; X 22,2; C 23; teto −6,8 dB pelo térmico (±0,2 dB) | — |
| JBL com LR24 150 | O mesmo, com passa-alta em 150 Hz | X 12,9 V manda; teto pelo curso | — |
| Sub do exemplo | Ponte ×40,5, dutada 28 L, Fb 51, BT24 40 Hz | X 33,1 V manda; teto −7,8 dB; "estimado" (volume estimado) | — |
| Caso 2 | Dutada, sem Bl/Qes, Fb 51, BT24 em 40 Hz | X = ∞, "estimado"; manda min(T, C) | — |
| Caso 3 | Dutada sem Fb | X = min(T, C) − 3 dB, "estimado", "Falta a Fb da caixa do …" | — |
| Sem calibração | Ganho digitado | Volts sim, dB null; a cerca deixa passar | — |
| Cerca na tela | Limitador da JBL L em −6,7 dB (teto −6,8) | Recusado, nada enviado, a releitura fica igual, a mensagem com a conta | −6,8 passa |
| Cerca OFF | Porta `/ajuste` 071 = OFF | 409 com a mensagem; nada enviado | — |
| Corte que baixa o X | `040` da JBL de 200 para 150 Hz, com o limitador em −6,8 | Recusado (o teto passa a ser o do X) | Baixar o limitador antes, e depois o corte passa |
| Lote | Aplicar uma memória com o limitador acima do teto | Nada enviado; falha com a mensagem; nada pela metade | — |
| Voltar à cópia | A cópia tem o limitador da JBL L em OFF; voltar | Volta com a JBL L no teto, e o resto igual à cópia; a marca sai | — |
| Cancelar a criação | O som de antes tinha o limitador do Sub acima do teto; ✕ | Volta com o Sub no teto; a criação fecha | — |
| Teto abaixo do mínimo | Saída com o teto de −52 dB | A cerca usa −48; `/limites` traz o aviso | — |
| Valor desconhecido | `040` numa saída sem o `071` no espelho | Recusado com "releia o aparelho" | — |
| Escolher Setup | Setup em dúvida com a memória ativa acima do teto | Nada enviado; falha com a mensagem | — |
| Criação | `/criacao/ajuste` 071 acima do teto | 409; nada enviado | — |
| Porta lê | `GET /limites` com o Casa da fixture | 3 saídas com os campos; o Sub na ponte | Sem Setup: `[]` |

</intent-contract>

## Code Map

Os caminhos são relativos a `app-controle/`.

- `src/BgsControle.Nucleo/Setups/Equipamentos.cs`:
  - `OrigemDado` (75–91) e `DadoTecnico.Verificado` (100–107);
  - `DadosTecnicos` (123–220): `PotenciaRms`, `Impedancia`, `Ganho`, `Re`, `Fs`, `Qes`, `Qms`, `Vas`, `Bl`, `Sd`, `Xmax`, `Le`, `TipoCaixa` (texto), `VolumeCaixa`, `Fb`, `AreaDuto`, `TensaoCeifamentoCanal`/`Ponte` e `GanhoPonte`. As unidades são L, cm², mm e mH (converter para SI);
  - `LigacaoAltoFalante` (67), `Bridge` (60) e `Entrada`/`FonteSinal` (46–57);
  - `Ligacoes.Seguir` (804–851, private, devolve `Alcance` com `CanaisModulo` e `Bridges`), `Alimentados` (854), `Links` (873) e `Saidas` (896/910 → `SaidaApp` 331). O caminho por alto-falante sai de uma versão pública ou interna do `Seguir`.
- `src/BgsControle.Nucleo/Setups/GerenteSetups.cs`: `SetupEmUso`, `SaidasEmUso`/`SaidaDe` (516–539) e `Corte` (575–583, só a frequência).
- Os lotes a conferir inteiros antes do envio:
  - `AplicarAlvoAsync` (2018–2079; confere só `escritas[0]` em 2036);
  - `ReaplicarNaConferenciaAsync` (798–831);
  - `EscolherInternoAsync` (983).
- `src/BgsControle.Nucleo/Catalogo/Catalogo.cs`:
  - `Filtros` (55–60): 0 = OFF; 1–7 = LR12…LR48; 8–15 = BT6…BT48;
  - `040`/`042` = passa-alta, `071` = limiar (−48..0, passo 0,1, OFF = 0,5);
  - `ParametroDe(c).Decodificar(noFio).Valor`.
- `src/BgsControle.Nucleo/Escrita/Escritor.cs`:
  - `Pedir` (171–224) é o ponto da cerca, junto do `Bloqueio` (99, 181);
  - `ResultadoEscrita.Falha` (33–36), `Recusar` (443) e a cópia do link `EspelharAsync` (315–337).
- `src/BgsControle.Nucleo/Escrita/CopiaAntes.cs`: `EscreverCopiaAsync` (449–480; confere só o primeiro quadro) e `ValorCopiado` (23).
- `src/BgsControle.Nucleo/Escrita/EstadoAparelho.cs`: o espelho e `Endereco` (6).
- `src/BgsControle.Nucleo/Setups/CriacaoMemoria.cs:568-592`: o ajuste da criação, que vira 409 na recusa.
- `src/BgsControle.Nucleo/Porta/PortaComandos.cs`:
  - `AtenderPareadoAsync` (545–558): as rotas GET;
  - `Ajuste` (707–746);
  - `EscreverEstado` (1086–1193), modelo do JSON;
  - `TextoAjuda` (1354–1396).
- `src/BgsControle.Telas/ControleParametroViewModel.cs`: `Pedir` (733–745), `AlternarLimitadorOff` (710–726) e `Frase` (787). A recusa já aparece na mensagem.
- Testes:
  - a fixture `tests/BgsControle.Simulador/CasaDoDenis.cs`: JBL com Re medido 3,6 e caixa 3,5 L estimada; Sub dutado com Fb e duto medidos; módulo com o ganho ×19,5 e ×40,5 medidos. Para o exemplo de 0,2 dB, a JBL usa o Re 3,4 da ficha;
  - as bancadas de `PortaTestes` e de `ControleParametroViewModelTestes`;
  - as tolerâncias com `Assert.InRange`.
- Consulta (não entram no app): `_bmad-output/regulagem/calculo_excursao_jbl.py`, `calculo_excursao_sub.py` e `memoria-spl.md` §12.2 e §13.2.

## Tasks & Acceptance

**Execution:**
- `src/BgsControle.Nucleo/Rendimento/ModeloExcursao.cs` (novo): selada e dutada, o filtro em módulo e a pior frequência. Em `double` e `System.Numerics.Complex`.
- `src/BgsControle.Nucleo/Rendimento/LimiteFisico.cs` (novo):
  - `LimiteSaida`, com T, X e C, a conta, o selo, a falta, o que manda, `TetoV`, `TetoDb` e `Calibrada`;
  - o cálculo por Setup e por estado (um leitor de `Endereco` → valor), para servir ao espelho agora e à memória na 13c;
  - os três casos;
  - o caminho por alto-falante, com um `Ligacoes.Caminhos` interno no `Equipamentos.cs`.
- `src/BgsControle.Nucleo/Rendimento/Cerca.cs` (novo) e `Escritor.cs`: a conferência antes da fila e as mensagens. É ligada no `GerenteSetups` ou no `MauiProgram`, como o `Bloqueio`.
- `GerenteSetups.cs` e `CopiaAntes.cs`: conferem o lote inteiro na cerca antes de começar.
- `PortaComandos.cs`: `GET /limites`, a ajuda e o 409 da cerca no `/ajuste` e no `/criacao/ajuste`.
- `src/BgsControle.App/MauiProgram.cs`: liga a cerca no app real.
- `Equipamentos.cs`/`LimiteFisico.cs`: o `TipoCaixa` é reconhecido em qualquer parte do texto ("Caixa selada", "bass-reflex", "reflex", "duto"), sem diferença de caixa nem de acento. Os alto-falantes desligados, ou ligados a um equipamento que não é amplificador, entram como "fora da conta", com o motivo no `/limites`.
- Testes:
  - `tests/BgsControle.Nucleo.Testes/LimiteFisicoTestes.cs` e `CercaTestes.cs`: uma linha da matriz por teste, com os números do exemplo dentro de 0,2 dB;
  - um teste da tela em `ControleParametroViewModelTestes` com a mensagem e nada enviado.

**Acceptance Criteria:**
- Given o Casa da fixture calibrado, when a porta lê `/limites`, then a JBL L e a JBL R dão −6,8 dB pelo térmico, e o Sub dá o teto do X com o selo "estimado".
- Given qualquer origem (tela, porta, criação, lote), when uma escrita deixa o limitador de uma saída calibrada OFF ou acima do teto, then nada é enviado ao simulador e a releitura fica igual.
- Given os testes que já existem, when a 13a entra, then todos passam, e o build Android sai sem erro nem aviso.

## Spec Change Log

- **31/10, depois da revisão (lacuna de intenção, decisão do Denis: a opção 1 recomendada).**
  - **O achado:** a volta à cópia e o desfazer recusados pela cerca deixavam a operação "pela metade" sem saída. O `Bloqueio` recusa todo áudio enquanto a marca existe, e na criação o Terminar não refaz.
  - **A emenda:**
    - os lotes que voltam escrevem o limitador em min(cópia, teto);
    - acrescentados o teto abaixo de −48 dB, o valor desconhecido (conservador) e as exceções;
    - a mensagem nomeia a saída sem artigo e diz o que fazer no corte;
    - o `TipoCaixa` é reconhecido em qualquer parte do texto, e os alto-falantes fora da conta aparecem.
  - **O estado ruim evitado:** uma marca que nunca sai, e uma saída travada com o teto abaixo do mínimo.
  - **As decisões tomadas sem o Denis:** −48 dB como teto mínimo e a recusa do valor desconhecido. Ficam para ele conferir.
  - **KEEP:**
    - o modelo de excursão (os números conferidos: 22,19, 12,90, 22,94 e 33,06 V);
    - o `Ligacoes.Caminhos`;
    - a cerca no `Pedir` e de novo no envio, também com `daCopia`;
    - a conferência do lote inteiro antes do `IniciarAsync`;
    - o `GET /limites`;
    - os testes de `LimiteFisicoTestes`, `CercaTestes` e da porta;
    - a fixture `EquipamentosSemCalibracao`.
  
  O ponto de partida é a tentativa em `_bmad-output/implementation-artifacts/13a-limite-fisico-e-cerca-tentativa-2026-10-31.patch`.

## Review Triage Log

### 2026-10-31 — Review pass
- verdicts: 33 findings — high 1, medium 9, low 15, false 3, maybe-false 5
- findings:
  - `[low]` `[reject]` Intent: a cerca não conhece o nível (o Máximo da CAP-2) — o contexto da história diz "sem nível, usa o teto do Baixo ao Alto", e os níveis são da 13c.
  - `[low]` `[reject]` Intent: nenhum ViewModel mostra o limite — "Sem telas"; o limite fica no núcleo e na porta, e a tela é da 13g.
  - `[low]` `[reject]` Intent: o selo é só verificado/estimado, sem a origem de cada dado — cada dado já leva a origem na ficha (8f), e o selo do limite segue `limites-e-niveis.md`.
  - `[low]` `[reject]` Intent: o caso 2 só entre 0,75 e 0,8·Fb — decisão explicada nas Design Notes.
  - `[false]` `[reject]` Intent: o X do Sub é conferido contra o próprio modelo — os 33,1 V batem com a §13.2 (86 % do Xmax a 28,6 V → 33,1 V) e com o script.
  - `[medium]` `[intent_gap]` Intent/Verification gap: a volta à cópia e o desfazer recusados não têm teste — moot pela lacuna de intenção abaixo (a mesma regra dos lotes de volta).
  - `[false]` `[reject]` Intent: o corte vem do espelho, não da memória — o espelho é o corte em uso, e a 13c passa o da memória pelo mesmo cálculo.
  - `[low]` `[reject]` Intent: o corte também passa pela cerca — é o que a spec decidiu (Design Notes).
  - `[maybe-false]` `[defer→moot]` Blind: escritas ao mesmo tempo em filas diferentes (071 e 040) passam juntas — falta confirmar se a vez da operação e a trava do envio já impedem; moot pela lacuna.
  - `[high]` `[intent_gap]` Blind/Edge: a cópia ou a marca com o limitador OFF ou acima do teto deixam a volta à cópia e o desfazer sempre recusados — a operação fica "pela metade" sem saída (o `Bloqueio` recusa todo áudio enquanto a marca existe, e na criação o Terminar não refaz). A regra "com uma recusa, nada sai" está no intent-contract. Leituras possíveis: escrever o limitador no teto na volta; deixar a volta passar sem a cerca; manter a recusa.
  - `[medium]` `[intent_gap]` Blind: a escrita num canal do par é recusada por causa do outro canal, e a mensagem não diz qual — moot pela lacuna.
  - `[medium]` `[intent_gap]` Blind: um teto abaixo de −48 dB trava a saída para sempre — a spec não diz o que fazer quando a saída não tem proteção possível; moot pela lacuna.
  - `[maybe-false]` `[defer→moot]` Blind: o ganho 081 depois do limitador contornaria a cerca — depende da ordem da cadeia, medida na 12d (suposição já registrada).
  - `[medium]` `[intent_gap]` Blind: mudanças que movem o teto sem escrever 071/040/042 (escolher Setup, ficha, ganho, link) não são conferidas — moot pela lacuna.
  - `[medium]` `[patch→moot]` Blind/Edge: o `Cerca.Conferir` do `AplicarAlvoAsync`, o `LimitesEmUso` e o `/limites` não têm try/catch.
  - `[medium]` `[patch→moot]` Blind/Edge: valor desconhecido deixa passar (071 fora do espelho num 040; pedido sem decodificar), ao contrário do filtro.
  - `[low]` `[patch→moot]` Blind: alto-falantes somem da saída sem aviso (desligados ou noutro grupo).
  - `[low]` `[patch→moot]` Blind: a calibração parcial desliga a cerca da saída inteira.
  - `[low]` `[patch→moot]` Blind: a recusa do corte não diz para baixar o limitador antes.
  - `[low]` `[patch→moot]` Blind: o `TipoCaixa` só reconhece o começo do texto ("Caixa selada", "bass-reflex").
  - `[low]` `[patch→moot]` Blind: o código difere da spec sem registro (`altoFalante`, `semLimite`, o link do lote, o `MauiProgram` só com comentário).
  - `[low]` `[patch→moot]` Blind: faltam testes (volta, reaplicação na conferência, escolher Setup, 042 OFF, `/memoria/aplicar` 409, a exceção, o par com tetos diferentes).
  - `[low]` `[patch→moot]` Blind: o `Escritor.Cerca` é uma propriedade só, e o último `GerenteSetups` vence.
  - `[maybe-false]` `[defer→moot]` Edge: o lote que falha no meio deixa o corte baixo com o limitador acima — depende da ordem das escritas no lote.
  - `[maybe-false]` `[defer→moot]` Edge: uma escrita em voo antes do `Conferir` do lote — igual ao achado da concorrência.
  - `[high]`→ ver acima `[intent_gap]` Edge: a cópia com OFF ou acima do teto trava a volta e o desfazer — mesmo grupo da lacuna.
  - `[medium]` `[patch→moot]` Edge: o 071 fora do espelho num 040 passa — igual ao achado do Blind.
  - `[medium]` `[patch→moot]` Edge: a exceção escapa do `AplicarAlvoAsync` — igual ao achado do Blind.
  - `[low]` `[patch→moot]` Edge (alegação): o canal com link que só alimenta caixa ativa recebe o teto do outro.
  - `[false]` `[reject]` Edge (alegação): a cerca "num único ponto" — o lote confere inteiro antes por desenho (a spec pede), e as escritas avulsas passam pelo `Escritor`.
  - `[medium]` `[intent_gap]` Verification gap: o escolher Setup sem teste da cerca — moot pela lacuna (a mesma regra dos lotes).
  - `[maybe-false]` `[defer→moot]` Verification gap (outro): a escrita da tela junto de um lote — igual ao achado da concorrência.
  - `[medium]` `[intent_gap]` Verification gap: a volta à cópia e o desfazer recusados não têm teste — o mesmo grupo da lacuna.

### 2026-10-31 — Review pass (2ª, depois da decisão do Denis)
- verdicts: 35 findings — high 0, medium 8, low 23, false 3, maybe-false 1
- findings:
  - `[low]` `[reject]` Intent: o teto não depende do nível — os níveis são da 13c ("sem nível, usa o teto do Baixo ao Alto").
  - `[low]` `[reject]` Intent: o usuário não vê o limite numa tela — "Sem telas"; a tela é da 13g.
  - `[low]` `[reject]` Intent: o `/limites` é um objeto (`saidas`, `foraDaConta`), não uma lista — o `foraDaConta` que as Tasks pedem exige o objeto; o conserto seria editar a spec.
  - `[false]` `[reject]` Intent: o corte vem do espelho, não da memória — o espelho é o corte em uso; a 13c passa o da memória ao mesmo cálculo.
  - `[medium]` `[defer]` Intent: trocar de Setup, a ficha, o ganho ou o link sem escrever 071/040/042 não confere o limitador do aparelho — a pergunta ao Denis segue aberta.
  - `[low]` `[patch]` Intent: a decisão do Denis não tem teste no desfazer fora da criação nem na falha da volta — testes (d) e (e).
  - `[low]` `[reject]` Intent: o −48 dB e a recusa do valor desconhecido não foram respondidos pelo Denis — estão na spec como provisórios, para ele conferir.
  - `[false]` `[reject]` Intent: o aceite pede −6,8 no Casa da fixture, que está em 150 Hz — os testes escrevem 200 Hz antes, como o critério supõe (LR24 em 200 Hz).
  - `[low]` `[reject]` Intent: só os `CercaTestes` releem — os outros conferem o valor no simulador, que é o que a releitura devolve.
  - `[low]` `[reject]` Blind: o Auto Run Result ainda diz "blocked" — é reescrito no fim desta rodada.
  - `[low]` `[reject]` Blind: o "Always" diz "começa com" e as Tasks dizem "qualquer parte" do `TipoCaixa` — o conserto é editar a spec; o código segue as Tasks.
  - `[medium]` `[patch]` Blind: o `TipoCaixa` por trecho classifica errado ("produto", "sem duto", "fechada", "sealed") — palavras inteiras, negação e ambos dão null.
  - `[false]` `[reject]` Blind: um dado zero ou negativo derruba o cálculo — o `DadosValidos` só aceita valor maior que zero (8f).
  - `[medium]` `[defer]` Blind: uma exceção no cálculo ainda recusa a volta e deixa pela metade — caminho de defeito, raro; uma saída segura (limitador em −48 em tudo) pede decisão.
  - `[low]` `[patch]` Blind: o log da volta diz "no teto" também onde nada foi escrito — só registra as escritas do teto.
  - `[low]` `[reject]` Blind: a calibração parcial deixa o `/limites` com `teto: null` e a cerca valendo num canal — a cerca por canal veio da emenda; o `cerca[]` por canal mostra o teto que vale.
  - `[low]` `[reject]` Blind: o `MauiProgram` só ganhou um comentário — o `GerenteSetups` liga a cerca no construtor, que já roda cedo no app.
  - `[low]` `[patch]` Blind: a selada usa o Le, ao contrário da fórmula da spec — a selada usa só o Re.
  - `[maybe-false]` `[defer]` Blind: uma escrita da tela entre o `Conferir` do lote e as escritas dele — falta ver se a vez da operação e o `Bloqueio` da marca já fecham essa janela.
  - `[low]` `[reject]` Blind: o `Escritor.Cerca` é uma propriedade só — o app tem um `GerenteSetups` só.
  - `[medium]` `[defer]` Blind: a reaplicação na conferência recusada pela cerca só fica no log, sem aviso ao usuário — a superfície é da 13g (telas) e da 13c (revalidar).
  - `[low]` `[patch]` Blind: faltam testes (desfazer, `/limites` especiais) — testes (e) e (f).
  - `[medium]` `[patch]` Edge: um teto acima de 0 dB não codifica no 071 e a volta falha — o teto da cerca e da volta fica entre −48 e 0.
  - `[low]` `[patch]` Edge: um teto de 0,5 dB codificaria OFF — o mesmo conserto.
  - `[medium]` `[patch]` Edge: o lote que baixa o corte e falha no 071 deixa o aparelho acima do teto — o 071 vai primeiro e o 040/042 por último.
  - `[medium]` `[defer]` Edge: escolher Setup sem diferença no 071/040/042 com o aparelho acima do teto novo — mesmo grupo das mudanças que movem o teto sem escrita.
  - `[low]` `[reject]` Edge: um canal com um alto-falante calibrado e outro sem calibração usa o teto do calibrado — o sem calibração fica de fora da cerca pela regra da spec; a 13b trata a calibração.
  - `[low]` `[patch]` Edge: o corte desconhecido recebe a mensagem do limitador — "o app não sabe o valor do corte".
  - `[medium]` `[patch]` Edge: o `TipoCaixa` com "produto", "conduto", "sem duto" — o mesmo grupo do Blind.
  - `[low]` `[patch]` Edge (alegação): a selada com Le — o mesmo grupo do Blind.
  - `[low]` `[reject]` Edge (alegação): o `/limites` sem Setup não é `[]` — igual à linha do Intent.
  - `[low]` `[patch]` Verification gap: duas escritas pendentes ao mesmo tempo — teste (a).
  - `[low]` `[patch]` Verification gap: o lote que passa do teto no meio e termina dentro — teste (b).
  - `[low]` `[patch]` Verification gap: a etapa da criação depois de um ajuste recusado — teste (c).
  - `[low]` `[patch]` Verification gap: a falha do cálculo na volta (a do lote e a do `/limites` não têm onde injetar) — teste (d).
  - `[low]` `[patch]` Verification gap: o `/limites` com "infinito", o `aviso` e o `foraDaConta` — teste (f).

## Design Notes

- **Por que o corte também passa pela cerca:** baixar o passa-alta baixa o X. Um corte em 150 Hz com o limitador ajustado para 200 Hz deixaria o cone passar do Xmax. A spec do Rendimento diz que a cerca recusa toda escrita que *deixe* o limitador acima do teto.
- **Por que o filtro entra só em módulo:** só o pico de |x| importa, e o módulo das formas LR e BT de qualquer ordem é exato.
- **Caso 2 (0,75 a 0,8·Fb):** o "em até 0,8 × Fb" de `limites-e-niveis.md`, junto com a faixa de 0,75 a 0,8 × Fb da `memoria-spl.md` §12.3. Um subsônico muito abaixo da Fb não protege o cone.
- **Re medido ou da ficha:** o modelo usa o Re que a ficha do Setup tem. Na fixture, o Re medido de 3,6 dá X 22,9 V (0,3 dB acima), e o teto continua pelo térmico.
- **Ordem do limitador e do ganho do processador:** a conversão supõe a escala do limitador fixa (MAPA §7). Se a 12d mostrar que o ganho entra depois, a conversão muda num lugar só.

## Verification

**Commands:**
- `dotnet test app-controle/tests/BgsControle.Nucleo.Testes` -- expected: todos passam.
- `dotnet test app-controle/tests/BgsControle.Telas.Testes` -- expected: todos passam.
- `dotnet build app-controle/src/BgsControle.App -f net10.0-android -c Debug` -- expected: 0 erros e 0 avisos.

## Auto Run Result

Status: done

**Histórico.** A 1ª rodada parou numa lacuna de intenção: a volta à cópia era recusada e ficava pela metade sem saída. O Denis escolheu a opção 1, voltar com o limitador no teto. A spec foi emendada (Spec Change Log), a tentativa guardada foi reaplicada e completada, e passou por uma 2ª revisão.

**O que entrou.** O limite físico e a cerca do Rendimento (spec-rendimento CAP-1 e CAP-2), no núcleo:
- **Os três limites:**
  - T, o térmico;
  - X, o curso do cone: na selada só com o Re, na dutada pelo modelo de Small com Le e Ql 7, com o filtro em módulo e os três casos de dado faltando;
  - C, o ceifamento do canal ou da ponte.
  
  Cada um vem com a conta e o selo, e a conversão para o limitador usa o ganho medido, por canal.
- **Os números do exemplo conferem:** JBL 22,19 V (LR24 em 200 Hz) e 12,90 V (em 150 Hz); teto −6,8 dB pelo térmico; Sub 33,06 V, teto −7,8 dB pelo curso.
- **A cerca** fica no `Escritor`, antes da fila e de novo no envio, também com `daCopia`:
  - recusa o 071 em OFF ou acima do teto, e o 040/042 que baixa o teto abaixo do limitador;
  - o valor desconhecido é recusado;
  - o teto fica entre −48 e 0 dB, com o aviso abaixo do mínimo.
- **Os lotes:** os que aplicam são conferidos inteiros antes de começar, com o 071 primeiro e o corte por último. Os que voltam (volta à cópia, desfazer e cancelar da criação) vão com o limitador em min(cópia, teto) e não são recusados pela cerca.
- **A porta:** `GET /limites` (saídas, cerca por canal, aviso e `foraDaConta`) e o 409 da cerca no `/ajuste`, no `/criacao/ajuste` e no `/memoria/aplicar`.
- **O `TipoCaixa`** é lido por palavra inteira, com a negação.

**Arquivos** (em `app-controle/`):
- Núcleo:
  - `Rendimento/ModeloExcursao.cs`, `LimiteFisico.cs` e `Cerca.cs` (novos);
  - `Escrita/Escritor.cs` e `CopiaAntes.cs`;
  - `Setups/GerenteSetups.cs`, `Equipamentos.cs` (`Ligacoes.Caminhos` e `ForaDosCaminhos`) e `CriacaoMemoria.cs`;
  - `Porta/PortaComandos.cs`;
  - `MauiProgram.cs` (só um comentário: o `GerenteSetups` liga a cerca).
- Testes:
  - `LimiteFisicoTestes` e `CercaTestes` (novos);
  - `PortaTestes`, `CriacaoTestes`, `SetupsTestes`, `ControleParametroViewModelTestes` e `TelasParte1Testes`;
  - a fixture `CasaDoDenis.EquipamentosSemCalibracao`.

A tentativa da 1ª rodada fica em `_bmad-output/implementation-artifacts/13a-limite-fisico-e-cerca-tentativa-2026-10-31.patch`.

**Revisão:**
- **1ª passada:** 33 achados. Ela parou na lacuna, e os `patch→moot` entraram na emenda.
- **2ª passada:** 35 achados (8 medium, 23 low, 3 false, 1 maybe-false).
  - **Patches:**
    - o teto entre −48 e 0 dB;
    - o `TipoCaixa` por palavra inteira;
    - a selada só com o Re;
    - a ordem do lote;
    - a mensagem do corte desconhecido;
    - o log da volta;
    - os testes (a) a (f).
  - **Diferidos (4, no frontmatter):**
    - as mudanças que movem o teto sem escrita;
    - a exceção que ainda recusa a volta;
    - a reaplicação recusada sem aviso;
    - a janela de concorrência no lote (não verificada).
  - **Rejeitados**, com o motivo no Review Triage Log.

**Revisão de acompanhamento: recomendada.** A 2ª passada corrigiu 3 entradas medium (o teto acima de 0 dB, o `TipoCaixa` e a ordem do lote). O risco que ninguém conferiu é a cerca no aparelho real: a ordem do EQ, do limitador e do ganho do processador, e as escritas da tela e da porta ao mesmo tempo. A 12d confere isso.

**Verificação:**
- `dotnet test` Núcleo: 1150 de 1150.
- `dotnet test` Telas: 477 de 477.
- `dotnet build` do App para `net10.0-android` (Debug): 0 erros e 0 avisos.
- A matriz tem pelo menos um teste por linha.

**Riscos que ficam:**
- Um lote que sobe o limitador e o corte juntos e falha no 040 deixa o limitador acima do teto do corte antigo. A volta à cópia corrige isso.
- **Decisões provisórias para o Denis conferir:**
  - o teto mínimo de −48 dB, com o aviso;
  - a recusa do valor desconhecido;
  - o caso 2 só entre 0,75 e 0,8·Fb;
  - a pergunta ainda aberta: trocar de Setup, a ficha, o ganho ou o link deve conferir o limitador que já está no aparelho?
