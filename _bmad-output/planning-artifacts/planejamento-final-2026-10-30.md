# Planejamento do que falta: desenvolver tudo e testar no celular no fim

Pedido do Denis em 30/10/2026: desenvolver tudo o que está pendente e testar com o celular na mão só no fim. Para rodar numa sessão nova: cole este arquivo como primeira mensagem.

## Onde estamos

- A 9c está pronta (commit `b8c8b4b`, no main): nenhuma tela mostra a IA, e a diferença leve ao conectar reaplica a memória sozinha.
- A ordem da proposta de 30/10 era: 9c → 12b → 9d → 10d → 12c → spec do Rendimento → spec do Agente.
- O chat não entra: o UX de 30/10 decidiu uma pergunta por tela.

## O que fazer (só planejar, sem código)

1. **Ordem das histórias** em `_bmad-output/specs/spec-app-controle-taramps/stories.yaml`: mover a `12b` para depois da `10d`, junto da `12c` no fim. Fica: 9d → 10d → histórias do Rendimento → histórias do Agente → 12b → 12c. A 12b e a 12c continuam com `done_checkpoint: true`. Ajustar o comentário do fim do arquivo.
2. **Spec do Rendimento** com a skill `bmad-spec`, junto com o Denis. Base: `_bmad-output/forge/teste-de-playlist/forged-idea.md` (níveis Baixo a Extra, análise da playlist, teste a seco, teto por preset). Quebrar em histórias que o `bmad-build-auto` rode contra o `ProcessadorSimulado`, sem o aparelho. O que só dá para conferir no aparelho entra na 12b/12c ou numa história de teste no fim.
3. **Spec do Agente da memória (o cérebro)**, do mesmo jeito. Base: `_bmad-output/forge/montagem-do-setup/forged-idea.md` e a seção "DEPOIS" (decisão D2) de `sprint-change-proposal-2026-10-30.md`. Conteúdo: perguntas adaptativas, escuta "1 ou 2", diagnóstico da queixa, serviço que acorda o agente, skills, aprendizado, ouvintes simulados. Onde a forja e o UX de 30/10 diferem, vale o UX. A porta nos dois sentidos e as telas são da 9d e da 10d.
4. **Pronto quando** cada história nova estiver no `stories.yaml` nessa ordem e o `bmad-build-auto` conseguir rodar tudo em cadeia até parar na 12b.

## Regras

- Português (Brasil), um passo por vez, opção recomendada primeiro, marcada (Recomendado).
- Commit no `main` ao fim de cada spec, com as linhas de atribuição; sem push e sem PR.
- Não mexer em `app-controle/`.
- As 3 memórias do processador são intocáveis: nunca 240, 241 nem 23N.

## Depois do planejamento

1. `/bmad-build-auto` em cadeia: 9d, 10d, Rendimento, Agente. Para se uma história precisar do Denis.
2. No fim, com o celular na mão: 12b e 12c.
