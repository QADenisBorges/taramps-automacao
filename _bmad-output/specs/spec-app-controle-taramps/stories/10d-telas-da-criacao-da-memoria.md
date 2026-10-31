---
title: 'História 10d: telas da criação da memória'
type: 'feature'
created: '2026-10-31'
baseline_revision: 'f93f7763d6f045997ee5e5c9ad0af3ec8c3d9ac1'
status: 'done'
review_loop_iteration: 0
followup_review_recommended: true
context:
  - '{project-root}/_bmad-output/planning-artifacts/ux-designs/ux-taramps-automacao-2026-10-30/EXPERIENCE.md'
  - '{project-root}/_bmad-output/planning-artifacts/ux-designs/ux-taramps-automacao-2026-10-30/DESIGN.md'
  - '{project-root}/_bmad-output/planning-artifacts/ux-designs/ux-taramps-automacao-2026-10-30/mockups/criacao-memoria-ia.html'
warnings:
  - oversized
deferred: []
---

<intent-contract>

## Intent

**Problem:** A criação da memória (CAP-18) existe no núcleo e na porta (9d), mas nenhuma tela a mostra. O `CriacaoMemoriaViewModel` está registrado e ninguém o usa. O usuário não vê as perguntas, os ajustes, o resumo nem o Salvar.

**Approach:** Criar uma camada global em tela toda (`CriacaoPage`), ligada a `CriacaoMemoriaViewModel.Aberta`, com o visual BGS Sound do UX de 30/10:
- perguntas com ‹ e ✕;
- palco;
- passagem Pião e cambalhota;
- Aplicando com o esquema de ligação;
- resumo, "Como ficou?" e Salvar;
- folhas de cancelar e de risco.

A ordem das fases visuais fica num ViewModel testável. A página só anima. Junto entram:
- o chip "+ Nova memória";
- a chave "Animação ao criar o som";
- o ‹ que volta à pergunta anterior, no núcleo.

## Boundaries & Constraints

**Always:**
- **Fases visuais** (no ViewModel, `FaseCriacao`): `Perguntas` → `Palco` → `Passagem` → `Aplicando` → `Resumo` → `ComoFicou` → `Salvar`.
  - O núcleo pode estar à frente da tela. A tela só mostra o resumo depois que:
    - o palco mostrou todos os ajustes da rodada;
    - a etapa do núcleo é `Resumo`;
    - o Aplicando terminou.
  - Enquanto a fila do palco está vazia e a etapa ainda é `Ajustando`, o palco espera com o anel girando.
  - No refino, o palco mostra só os ajustes da rodada nova.
  - Com a animação desligada, ou com o movimento reduzido do sistema, não há palco nem passagem. O Aplicando aparece desde o início da etapa `Ajustando`, e cada canal acende quando um ajuste nele é relido; um ajuste sem saída (global) acende todos. O resumo entra assim que a etapa é `Resumo`, sem mínimo de 0,7 s por canal (questão 15).
- **Tempos do UX** (EXPERIENCE/mock):
  - a resposta acende em 0,18 s; a pergunta seguinte entra em 0,32 s, vinda de 28 px (da direita ao avançar, da esquerda com ‹);
  - no palco: 0,3 s parado antes da 1ª frase; o Foco de entrada leva 0,8 s (valor 0,9 s); depois a barrinha (0,6 s), o risco, a seta e o valor novo; a parada é de 1,3 s; a saída leva 0,65 s; o intervalo entre frases é de 0,12 s;
  - a passagem leva 1,6 s;
  - no Aplicando: 0,7 s por canal, de CH1 a CH4, mais 1 s parado e 0,3 s de saída;
  - os itens do resumo entram com 0,07 s entre eles.
  - A parada de 1,3 s nunca encurta. O desfoque usa `RenderEffect` no Android 31 ou mais novo; abaixo disso fica sem desfoque.
- **Palco:**
  - Mostra a frase do ajuste (24/800; a palavra-chave em ciano é a última palavra da frase) e, numa linha embaixo, a barrinha, o valor de antes riscado, a seta e o valor relido.
  - Com `Diferente` ou `SemEco`, a linha ganha embaixo "O aparelho ficou com outro valor." em `apoio` (questão 18).
  - Embaixo ficam o horizonte, o chão e o anel duplo grande e apagado. Não há ‹ nem ✕.
- **Barra do fluxo:**
  - Título "Nova memória" e o nome do Setup como subtítulo, em todas as fases. No "Como ficou?", o subtítulo é "tocando agora". O nome só existe no Salvar (questão 12).
  - O ‹ aparece da 2ª pergunta em diante. O ✕ aparece só nas perguntas e no "Como ficou?" (questão 14).
- **‹ (núcleo):** volta à pergunta anterior, com a resposta dada marcada e o texto do "Outro…" no lugar dele.
  - A mesma resposta só avança na tela.
  - Uma resposta diferente responde de novo aquela pergunta: as perguntas seguintes saem, e o `/eventos` recebe `criacao-resposta` com o número dela.
- **Aplicando:** usa o esquema de ligação plano do UX: o PRO 2.4BT com CH1–CH4, os fios e os equipamentos que cada canal alimenta, montados só com os dados do Setup em uso. Os estados são aceso #3FD0FF, gravado #16394A e ainda não #2A2A30. O texto é "APLICANDO", com o anel duplo e "Nova memória". Não há ✕ nem Cancelar.
- **Resumo:**
  - "Pronto. Ouça um pouco." e o texto do VM.
  - A lista mostra ✓, o rótulo, o valor e o motivo; no refino, as linhas da rodada ficam em destaque.
  - O botão é "Avaliar o som".
- **Salvar:**
  - Tela com o título "Salvar no {Setup}?", o texto "Guarda toda a regulagem que está tocando agora.", o campo "Nome da memória" com o erro embaixo, "Salvar" (que mostra "Salvando" enquanto salva) e "Ajustar mais".
  - Ao salvar, a criação fecha, e a casca mostra por 2,6 s o aviso `GerenteSetups.MsgCriada`.
  - Ao cancelar, a casca mostra `CriacaoMemoria.MsgCancelada`.
- **Folha de risco:**
  - A camada da criação fica abaixo da `FolhaRiscoPage`, que continua por cima de tudo.
  - Com a folha de risco aberta, o palco pausa e retoma depois da resposta.
- **Folha de cancelar:** usa os textos que já estão no VM.
- **Voltar do Android** (questão 21):
  - nas perguntas, faz o ‹; na 1ª pergunta, faz o ✕;
  - no "Como ficou?", faz o ✕;
  - no Salvar, faz o "Ajustar mais";
  - com a folha de cancelar aberta, faz o "Continuar criando";
  - no palco, na passagem, no Aplicando e no resumo, não faz nada.
- **TalkBack** (questão 20):
  - cada ajuste do palco é anunciado uma vez ("{frase}: de {antes} para {valor}");
  - o anel tem a descrição "Aplicando";
  - ‹ = "Voltar", ✕ = "Cancelar";
  - o esquema descreve cada canal e o que ele alimenta.
- **"+ Nova memória":**
  - Chip na `MemoriasPage`, junto de "Criar memória do som atual" (ASSUMPTION do `stories.yaml`).
  - Fica travado sem Setup em uso, em dúvida, em só leitura ou com uma criação aberta.
  - O toque chama `CriacaoMemoria.Pedir()`, que emite o evento `criacao-pedida` no `/eventos`. A espera, o prazo de 15 s e o aviso da chave desligada são das histórias 14b e 14c.
- **Chave "Animação ao criar o som":**
  - Fica nas Configurações do app, no par Desl./Lig. que o app já usa, com o texto do UX: "Mostra cada ajuste na tela, um por vez, enquanto a memória é montada. Desligada, aparece só o "Aplicando"."
  - Vem ligada e é gravada em JSON pelos `IArquivosApp`.
- Cores só pelos tokens do `App.xaml`: os que faltam do DESIGN.md entram lá. O XAML segue as regras do `ArquiteturaTestes`. Nenhum texto visível contém "IA".

**Never:**
- Mudar as regras da porta e das etapas da 9d, fora o ‹ e o `Pedir`.
- A espera do pedido, a escuta "1 ou 2" e a proposta de ficha (14b e 14c).
- Abrir a criação pela tela.
- A Roda e o redesenho das outras telas.
- `Preferences` do MAUI.
- Nenhum `240`, `241` ou `23N`.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Abrir | A porta abre a criação | A camada entra em `Perguntas`, com "Nova memória" e "Casa 2.1" | — |
| Voltar com ‹ | 2 perguntas respondidas; ‹; outra resposta na 1ª | A pergunta 2 sai; `criacao-resposta` nº 1 de novo; a tela espera a próxima | Mesma resposta: avança sem evento |
| Palco à frente | 3 ajustes chegam juntos e a porta já mandou o resumo | A fase fica `Palco` até mostrar os 3, depois `Passagem`, `Aplicando` e `Resumo` | — |
| Sem animação | Chave desligada | `Ajustando` vai direto ao `Aplicando`; o canal acende quando o ajuste dele é relido; `Resumo` na hora | — |
| Refino | "Quero mais impacto" | O palco mostra só a rodada 2; o resumo destaca as linhas da rodada | — |
| Releitura diferente | O ajuste vem com `Diferente` | O palco mostra o valor relido e "O aparelho ficou com outro valor." | O resumo da porta segue dando 409 (9d) |
| Risco | Folha de risco abre no palco | O palco pausa; a folha fica por cima; Confirmar retoma | Cancelar fecha a criação (9d) |
| Queda | BLE cai no palco | A camada fecha; "aplicado pela metade" aparece | — |
| Salvar | Nome "MUSICA ORIG" | A camada fecha; "Memória MUSICA ORIG criada no Casa 2.1." por 2,6 s | Nome inválido: erro embaixo do campo, fica no Salvar |
| Voltar do Android | No palco | Nada acontece | — |
| + Nova memória | Setup em uso, conectado | Evento `criacao-pedida` no `/eventos` | Travado sem Setup, em dúvida, em só leitura ou com uma criação aberta |
| Chave | Desligar e reabrir o app | Fica desligada | Sem arquivo: ligada |

</intent-contract>

## Code Map

Os caminhos são relativos a `app-controle/`.

- `src/BgsControle.Nucleo/Setups/CriacaoMemoria.cs`:
  - as etapas (l.9), `PerguntaCriacao` (33) e `AjusteCriacao` (47; `Indice` null = global, `Rotulo` e `*Exibicao`);
  - as constantes dos eventos (147–151), `MsgCancelada` (178), `Mudou`/`Aconteceu` (249–252);
  - `Perguntar`/`Responder`/`ResponderPergunta` (441–500) recebem o ‹: só a última pergunta é respondida hoje;
  - `CancelarAsync` (804).
- `src/BgsControle.Nucleo/Porta/PortaComandos.cs`:
  - a assinatura de `Aconteceu` e o mapeamento para o `EventoPorta` (procurar `EventoResposta`), para o `criacao-pedida`;
  - `TextoAjuda` (procurar `/criacao`);
  - o Cancelar da folha na criação (620–628).
- `src/BgsControle.Telas/CriacaoMemoriaViewModel.cs`:
  - `LinhaCriacao` (7–10) não tem `Motivo` nem o valor pedido nem `Diferente`;
  - as constantes de texto (29–56); `PodeFechar` (116); `AjustesDaRodada` (150); `LinhasResumo` (159);
  - `Atualizar` (335–396) refaz as listas a cada `Mudou`, sem evento de ajuste novo;
  - Salvar e Cancelar não expõem a mensagem final.
- `src/BgsControle.Telas/Abstracoes.cs` (57–113, `INavegacao`), `src/BgsControle.App/NavegacaoMaui.cs` (`Reconciliar` e `MostrarCamadas`), `src/BgsControle.Telas/CascaViewModel.cs` (`SincronizarCamadas` 222–244) e `tests/BgsControle.Telas.Testes/Falsos.cs:129` (`NavegacaoFalsa`): entra a camada da criação, abaixo da `FolhaRiscoPage`.
- `src/BgsControle.App/Telas/FolhaRiscoPage.xaml(.cs)`, `PelaMetadePage.xaml` e `ReaplicandoPage.xaml` são o modelo de camada modal (veu, cantos 26,26,0,0 e `OnBackButtonPressed`). `FolhaRiscoViewModel.FolhaAberta` é o sinal da pausa.
- `src/BgsControle.App/Telas/MemoriasPage.xaml:74-93` e `src/BgsControle.Telas/MemoriasViewModel.cs` (143, 223 `MotivoTravaCriar`, 311): o lugar do chip.
- `src/BgsControle.Telas/Esquema/CenaSetup.cs` (`Montar` 306, `PortasDe`, `CabosAte`): de onde sai qual equipamento cada canal alimenta, para o esquema plano. `EquipamentosViewModel.Setup` (371, 415, 424) devolve o Setup em uso.
- `src/BgsControle.App/Controles/EsquemaView.cs` é o modelo de `GraphicsView` + `IDrawable` com o modelo testável em Telas. `ArcoView.cs` e `FolhaView.cs:72` (a única animação de hoje, `TranslateToAsync`).
- `src/BgsControle.Telas/SetupsViewModel.cs:319-389` (`ConfiguracoesViewModel`) e `src/BgsControle.App/Telas/ConfiguracoesPage.xaml:184-201` (o par Desl./Lig. com o estilo `par` e a `SemanticProperties.Description`).
- `src/BgsControle.Telas/RepositorioChavesArquivos.cs` e `RepositorioTextoArquivos.cs` (nomes dos arquivos) são o modelo da preferência em JSON. Teste: `ChavesViewModelTestes.cs:138-150`.
- `src/BgsControle.App/App.xaml` (15–47: tokens; "palco" já é o preto do fundo) e `Recursos/Cores` (`Cores.De`, `Cores.DoCanal`). A fonte é a Roboto com `r:Tipo.Peso`.
- `src/BgsControle.App/MauiProgram.cs`: o `CriacaoMemoria` (243–252), o VM (285–288) e as camadas (228–235, 293–300).
- Testes:
  - `tests/BgsControle.Telas.Testes/CriacaoMemoriaViewModelTestes.cs` (a bancada com a porta, `Pronto`, `Pergunta`, `Ajuste`, `Resumo`, `SemIa` 141–156);
  - `tests/BgsControle.Nucleo.Testes/CriacaoTestes.cs`;
  - `ArquiteturaTestes.cs` (133–217 "IA" no XAML; 247–267 `x:DataType` e nenhuma cor solta).

## Tasks & Acceptance

**Execution:**
- `src/BgsControle.Nucleo/Setups/CriacaoMemoria.cs`:
  - `ResponderDeNovo(numero, indice | textoOutro)` para o ‹: responde de novo uma pergunta já respondida, ainda na etapa `Perguntas`, tira as seguintes e emite `criacao-resposta`; a mesma resposta não muda nada;
  - `Pedir()`, que emite `EventoPedida = "criacao-pedida"` quando a criação está fechada.
- `src/BgsControle.Nucleo/Porta/PortaComandos.cs`: passa o `criacao-pedida` ao `/eventos`; a ajuda explica o ‹ (a resposta pode voltar para um número anterior; as perguntas depois dele saem) e o pedido.
- `src/BgsControle.Telas/CriacaoMemoriaViewModel.cs`:
  - a `FaseCriacao` e a fila do palco (`AjusteDoPalco`, `PalcoMostrou`, `PassagemTerminou`, `AplicandoTerminou`, `Pausado` pela folha de risco);
  - os canais do Aplicando (aceso, gravado, ainda não);
  - a pergunta mostrada (com ‹ e `Voltar`), `PodeVoltar`, a resposta marcada;
  - a `LinhaCriacao` com o motivo, o valor relido e o diferente;
  - o subtítulo; `AnimacaoLigada` e `MovimentoReduzido`;
  - `AvisoFinal` (salva ou cancelada);
  - a tecla Voltar (`Voltar()` devolve se tratou);
  - o anúncio do TalkBack por ajuste;
  - o título e o texto do Salvar.
- `src/BgsControle.Telas/Esquema/EsquemaLigacao.cs` (novo): o modelo plano das portas CH1–CH4, dos equipamentos com os canais deles e das posições do UX, testável. Sai do Setup em uso.
- `src/BgsControle.Telas/PreferenciasApp.cs` (novo) e `RepositorioTextoArquivos.cs`: `AnimacaoCriacao`, ligada por padrão, em `preferencias-app.json`. `ConfiguracoesViewModel` ganha a linha.
- `src/BgsControle.Telas/MemoriasViewModel.cs`: `NovaMemoria`, com a trava e o comando que chama `Pedir()`.
- `src/BgsControle.Telas/Abstracoes.cs`, `CascaViewModel.cs` e `tests/.../Falsos.cs`:
  - a camada da criação em `MostrarCamadas`;
  - o aviso rápido de 2,6 s na casca.
- `src/BgsControle.App/Telas/CriacaoPage.xaml(.cs)` (nova): a barra do fluxo, as perguntas com o "Outro…" (campo "Escreva do seu jeito" e "OK"), o palco, a passagem, o Aplicando, o resumo, o "Como ficou?", o Salvar e a folha de cancelar (`FolhaView`). As animações (`Animation`, `TranslateTo`, `RotationX/Y`) seguem as fases do VM. `OnBackButtonPressed` chama `Voltar()`.
- `src/BgsControle.App/Controles/AnelDuploView.cs` e `EsquemaLigacaoView.cs` (novos), em `GraphicsView`:
  - o anel duplo (ciano em 1,1 s horário e rosa em 1,5 s anti-horário; no movimento reduzido, 2,2 s e 3 s);
  - o esquema plano com os 3 estados.
- `src/BgsControle.App/Platforms/Android/` contém o `RenderEffect` do Foco e a leitura do `AnimatorDurationScale` (0 = movimento reduzido).
- Os tokens do DESIGN que faltam vão para `App.xaml`, `NavegacaoMaui.cs` e `MauiProgram.cs`, junto com a página, o chip, a chave e o aviso rápido na `MemoriasPage`, na `ConfiguracoesPage` e na `CascaPage`.
- Testes:
  - `CriacaoTestes` (o ‹ e o pedido);
  - `CriacaoMemoriaViewModelTestes` (cada linha da matriz que não é de animação, com as fases, a fila e a pausa);
  - `EsquemaLigacaoTestes` (o Casa 2.1: Sub em CH3+CH4; um Setup de 4 canais);
  - `PreferenciasApp`/`Configuracoes`;
  - `MemoriasViewModel` (a trava e o evento);
  - a tecla Voltar por fase.

**Acceptance Criteria:**
- Given um cliente pareado conduzindo uma criação pela porta até o resumo e o usuário tocando as respostas pelo ViewModel, when cada fase avança pelos sinais da página, then a ordem é perguntas, palco (um por ajuste), passagem, Aplicando, resumo, "Como ficou?" e Salvar, e o Salvar fecha a camada com o aviso `MsgCriada`.
- Given o ‹ numa pergunta anterior com outra resposta, when a porta lê `GET /criacao`, then só existem as perguntas até ela, com a resposta nova.
- Given os testes que já existem, when a 10d entra, then todos passam, o teste do "IA" cobre o XAML novo e o build Android sai sem erro nem aviso.

## Spec Change Log

- 31/10 (implementação): decisões tomadas onde a spec não dizia.
  - O "Como ficou?" e o Salvar só chegam por um toque do usuário depois do resumo; neles a fase segue a etapa direto (a fila do palco que sobrar é descartada). Só o palco, a passagem e o Aplicando andam atrás do núcleo.
  - A tecla Voltar com o campo do "Outro…" aberto fecha o campo antes (como as folhas).
  - Sem animação, um ajuste num canal de um par com link (o Sub do Casa 2.1) acende os dois canais (`EsquemaLigacao.Juntos`).
  - O esquema plano mostra os equipamentos que tocam; um canal sem nenhum que toca mostra o que ele alimenta (o amplificador). O TalkBack do esquema é "CH1 alimenta JBL L." por canal.
  - O aviso rápido da casca também aparece na `MemoriasPage`, porque a criação fecha com ela aberta (o chip fica lá até a Roda).
  - A chave fica num grupo novo "DO APP" das Configurações.
  - A folha de cancelar usa o `FolhaView` de hoje (fundo e cantos do visual antigo).

## Review Triage Log

### 2026-10-31 — Review pass
- verdicts: 40 findings — high 2, medium 5, low 30, false 3, maybe-false 0
- findings:
  - `[false]` `[reject]` Intent: o "+ Nova memória" só pede, não abre a criação — a spec decidiu assim, com base em `lado-do-app.md` e na divisão com a 14b/14c ("Abrir a criação pela tela" está no Never).
  - `[low]` `[reject]` Intent: o Aplicando animado acende CH1 a CH4 em sequência, depois da releitura, e CH3/CH4 não acendem juntos — é o tempo que a spec pede (0,7 s por canal, de CH1 a CH4); o Sub fica aceso enquanto qualquer um dos dois canais dele está aceso.
  - `[false]` `[reject]` Intent: "Nova memória" no lugar do nome no Aplicando e no Salvar — decisão da questão 12 na spec (o nome só existe no Salvar).
  - `[low]` `[patch]` Intent: a folha de cancelar da criação usa o visual antigo do `FolhaView`, e a intenção põe as telas da criação no visual BGS Sound — grupo N: a folha ganhou os tokens do DESIGN só na `CriacaoPage`.
  - `[low]` `[reject]` Intent: a regra 6 reaproveita a `PelaMetadePage` sem tela nova — a própria regra 6 manda valer a regra 3 ("aplicado pela metade"), e a spec escolheu isso na matriz (Queda).
  - `[low]` `[reject]` Intent: o TalkBack e a tecla Voltar só são testados no ViewModel — a parte da plataforma só se confere no aparelho (12c), como diz a Verification.
  - `[low]` `[patch]` Verification gap: `PosicaoNaBarra` só é conferida entre 0 e 1 — grupo K: `[Theory]` com as posições exatas de cada ramo.
  - `[low]` `[patch]` Verification gap: a nota "O aparelho ficou com outro valor." com `SemEco` não tem teste — grupo K.
  - `[low]` `[patch]` Verification gap: o aviso `MsgNaoVoltou` do cancelar que não volta não tem teste — grupo K.
  - `[low]` `[patch]` Verification gap: a tecla Voltar com o campo do "Outro…" aberto não tem teste — grupo K.
  - `[low]` `[patch]` Verification gap (outro): o ramo `Ultima` do `ResponderDeNovo` nunca roda — grupo J: apagado.
  - `[high]` `[patch]` Blind: o `OnAppearing` reinicia a fase depois que uma camada modal fecha, e o Aplicando animado fica preso — grupo A: o `OnAppearing` só sincroniza uma fase diferente, e o Aplicando escolhe pelo `ComAnimacao`.
  - `[medium]` `[patch]` Blind: um toque numa resposta seguido de ‹ em menos de 0,18 s responde a pergunta errada — grupo C: o toque fica preso ao número da pergunta em que a resposta foi montada.
  - `[low]` `[patch]` Blind: o Salvar não trava o "Ajustar mais" nem a tecla Voltar — grupo D: nada acontece enquanto salva, com teste do "Salvando".
  - `[low]` `[patch]` Blind: o aviso rápido é anunciado duas vezes no TalkBack — grupo E: só a casca anuncia.
  - `[low]` `[patch]` Blind: o anel duplo redesenha a 60 fps escondido — grupo F: só redesenha com ele e os pais visíveis. O `RenderEffect` por quadro fica (só durante a animação do Foco).
  - `[low]` `[reject]` Blind: as gravações das preferências não são em série — exige dois toques antes de uma gravação local terminar, e o conserto acrescenta uma trava.
  - `[low]` `[reject]` Blind: o "+ Nova memória" não trava desconectado e não dá retorno — a lista de travas é a da spec; a espera e o retorno são da 14b/14c.
  - `[low]` `[patch]` Blind: o esquema sobrepõe as caixas com 10 equipamentos ou mais — grupo G: a altura fica limitada ao passo, com teste.
  - `[low]` `[patch]` Blind: um nome longo sai da caixa do esquema ("Protech Light 8\"", do Setup real) — grupo H: o nome fica preso à caixa.
  - `[low]` `[patch]` Blind: a resposta recusada fica acesa — grupo I: a opção apaga quando a resposta falha.
  - `[low]` `[patch]` Blind: o teste do só leitura não confere a mensagem — grupo K: confere `MsgSoLeitura`.
  - `[low]` `[patch]` Blind: o `AvisoFinal` do cancelar pela folha de risco e pela porta não tem teste — grupo K.
  - `[low]` `[reject]` Blind: o `Pedir` desconectado não tem teste — o comportamento desconectado não está na spec.
  - `[low]` `[patch]` Blind: o "Salvando" e a trava do Salvar não têm teste — grupo D.
  - `[low]` `[reject]` Blind: a volta à página não tem teste e não está nos riscos da spec — o conserto do grupo A fecha o defeito, e o teste só existe no aparelho; mudar a Verification seria editar a spec.
  - `[low]` `[patch]` Blind: o ramo morto do `ResponderDeNovo` — grupo J.
  - `[low]` `[patch]` Blind: a pontuação no fim da frase entra na palavra-chave e no anúncio — grupo L: a pontuação final sai.
  - `[false]` `[reject]` Blind: o `Estados?[p.Canal]` do `EsquemaLigacaoView` não confere o tamanho — os `EstadosCanais` têm sempre 4 itens (`_canais` é `new EstadoCanal[4]`), e `Canal` vai de 0 a 3.
  - `[low]` `[patch]` Blind: `MaxLength="24"` e o placeholder fixo "MUSICA ORIG" no campo do nome (caso fixo) — grupo M: usa a constante do núcleo e um placeholder sem caso.
  - `[high]` `[patch]` Edge: uma camada modal que fecha no Aplicando animado deixa a criação presa — grupo A.
  - `[medium]` `[patch]` Edge: a folha de risco que abre na saída de 0,3 s do Aplicando deixa a tela em branco e presa — grupo B: espera sem pausa e tenta o `AplicandoTerminou` de novo.
  - `[low]` `[patch]` Edge: a tecla Voltar no Salvar enquanto salva — grupo D.
  - `[low]` `[reject]` Edge: Desl. e Lig. rápidos nas preferências — igual à linha do Blind acima.
  - `[low]` `[patch]` Edge: o aviso rápido anunciado duas vezes — grupo E.
  - `[low]` `[patch]` Edge: a resposta recusada fica acesa — grupo I.
  - `[medium]` `[patch]` Edge: as respostas refeitas durante os 180 ms respondem pelo índice velho — grupo C.
  - `[low]` `[patch]` Edge: as caixas se sobrepõem com 10 equipamentos ou mais — grupo G.
  - `[medium]` `[patch]` Edge (alegação): depois da folha de risco, o palco recomeça o ajuste e o anuncia de novo — grupo A.
  - `[medium]` `[patch]` Edge (alegação): a criação fica presa no Aplicando — grupo A (mesma causa do primeiro achado do Edge).

## Design Notes

- **Por que a fase visual é separada da etapa do núcleo:** a porta manda os ajustes no ritmo dela e o núcleo escreve na hora. "A animação tem prioridade sobre a velocidade" quer dizer que a tela nunca encurta: ela anda atrás do núcleo e só mostra o resumo quando o palco e o Aplicando terminaram. Cada ajuste já foi relido quando a frase dele aparece, o que vale a CAP-18 ("relido antes de o Aplicando terminar").
- **A palavra-chave em ciano:** a frase chega da porta sem marcação. A última palavra é a regra simples ("Ajustando o **grave**").
- **O nome só no Salvar:** a porta não nomeia, e o UX não desenhou o nome antes (questão 12). Por isso "Nova memória" fica na barra até lá.
- **O par Desl./Lig.:** a chave nova segue o padrão da tela de Configurações de hoje. O interruptor de 48×28 é do redesenho.

## Verification

**Commands:**
- `dotnet test app-controle/tests/BgsControle.Nucleo.Testes` -- expected: todos passam.
- `dotnet test app-controle/tests/BgsControle.Telas.Testes` -- expected: todos passam.
- `dotnet build app-controle/src/BgsControle.App -f net10.0-android -c Debug` -- expected: 0 erros e 0 avisos.

**Manual checks (if no CLI):**
- As animações, o desfoque, o TalkBack e a tecla Voltar só aparecem no aparelho (história 12c); sem celular, registrar que não foram vistos.

## Auto Run Result

Status: done

**O que entrou.** As telas da criação da memória (CAP-18), numa camada em tela toda ligada à criação da 9d:
- perguntas com ‹ e ✕ e o "Outro…";
- o palco com o Foco, o valor numa linha e o anel apagado no chão;
- a passagem Pião e cambalhota;
- o Aplicando com o esquema de ligação plano do Setup;
- o resumo, o "Como ficou?" e o Salvar com o nome;
- a folha de cancelar no visual BGS e a folha de risco por cima da criação.

A ordem das fases visuais fica no ViewModel (`FaseCriacao`): a tela anda atrás do núcleo e nunca encurta. Junto entraram:
- o ‹ no núcleo (`ResponderDeNovo`);
- o chip "+ Nova memória", que emite `criacao-pedida`;
- a chave "Animação ao criar o som", gravada em `preferencias-app.json`;
- o aviso rápido de 2,6 s na casca.

As questões 12, 14, 15, 18, 20 e 21 foram resolvidas na spec.

**Arquivos** (em `app-controle/`):
- Núcleo:
  - `Setups/CriacaoMemoria.cs`: `ResponderDeNovo` e `Pedir`;
  - `Porta/PortaComandos.cs`: a ajuda.
- Telas:
  - `CriacaoMemoriaViewModel.cs`: as fases, a fila do palco, os canais, o ‹, a tecla Voltar, o TalkBack e o Salvar;
  - `Esquema/EsquemaLigacao.cs` (novo): o esquema plano;
  - `PreferenciasApp.cs` (novo): a chave;
  - `MemoriasViewModel.cs`: o chip e a trava;
  - `SetupsViewModel.cs`: a linha da chave nas Configurações;
  - `CascaViewModel.cs` e `Abstracoes.cs`: a camada e o aviso rápido;
  - `RepositorioTextoArquivos.cs`.
- App:
  - `Telas/CriacaoPage.xaml(.cs)` (nova): as telas e as animações;
  - controles `AnelDuploView`, `AnelNeonView`, `EsquemaLigacaoView`, `AvisoRapidoView` e `Curvas`;
  - `Platforms/Android/EfeitosAndroid.cs`: o desfoque e o movimento reduzido;
  - `App.xaml`: os tokens;
  - `NavegacaoMaui.cs`, `MauiProgram.cs`, `MemoriasPage`, `ConfiguracoesPage` e `CascaPage`.
- Testes:
  - `CriacaoTestes`, `CriacaoMemoriaViewModelTestes`, `EsquemaLigacaoTestes` (novo), `PreferenciasAppTestes` (novo), `ArquiteturaTestes`, `Falsos` e `TelasParte2Testes`.

**Revisão.** 40 achados: 2 high, 5 medium, 30 low e 3 false.
- **Patches (14 grupos):**
  - **A (high):** o `OnAppearing` não reinicia mais a fase quando uma camada fecha. Antes, o Aplicando ficava preso e o palco repetia o anúncio.
  - **B:** a saída do Aplicando com a folha de risco aberta.
  - **C:** o toque fica preso à pergunta em que a resposta foi montada.
  - **D:** o Salvar trava o "Ajustar mais" e a tecla Voltar.
  - **E:** o aviso rápido é anunciado uma vez só.
  - **F:** o anel só redesenha quando está visível.
  - **G e H:** as caixas e os nomes do esquema cabem com muitos equipamentos e com nomes longos.
  - **I:** a resposta recusada apaga.
  - **J:** o ramo morto do `ResponderDeNovo` saiu.
  - **K:** testes da posição na barra, do sem eco, do `MsgNaoVoltou`, do Voltar com o campo aberto, do só leitura e do aviso do cancelar pela folha e pela porta.
  - **L:** a pontuação final da frase sai.
  - **M:** o nome usa o tamanho do núcleo, sem placeholder fixo.
  - **N:** a folha de cancelar segue o visual BGS.
- **Junto com os patches:** o implementador pôs a atualização do ViewModel e os sinais da página sob uma trava. Isso tirou uma corrida que fazia dois testes falharem às vezes.
- **Nada diferido.**
- **Rejeitados, com o motivo no Review Triage Log:**
  - o chip só pede;
  - o tempo do Aplicando;
  - o nome "Nova memória";
  - a regra 6 com a `PelaMetadePage`;
  - o TalkBack e o Voltar só no ViewModel;
  - a gravação das preferências fora de série;
  - o chip desconectado;
  - o `Pedir` desconectado sem teste;
  - a volta à página sem teste;
  - o índice dos estados (false).

**Revisão de acompanhamento: recomendada.** Os patches foram 1 high e 3 medium (grupos B e C, e o grupo A também trazia as alegações medium). O risco que ninguém conferiu é a página real: as animações, o `OnAppearing` depois de uma camada modal, a ordem das camadas no `NavegacaoMaui` e a trava nova no ViewModel. Os testes não rodam nada disso, e só o aparelho mostra.

**Verificação:**
- `dotnet test` Núcleo: 1055 de 1055.
- `dotnet test` Telas: 465 de 465.
- `dotnet build` do App para `net10.0-android` (Debug): 0 erros e 0 avisos.
- A auditoria da matriz deu pelo menos um teste por linha, e todos rodaram.
- Nada foi visto num celular.

**Riscos que ficam:**
- Os tempos, a perspectiva da passagem, o desfoque (Android 12 ou mais novo), o TalkBack e a tecla Voltar só se conferem no aparelho (12c).
- O "+ Nova memória" só manda o evento: sem cliente, o toque não mostra nada até a 14b e a 14c.
