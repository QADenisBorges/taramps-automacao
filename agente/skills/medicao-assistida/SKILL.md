---
name: medicao-assistida
description: Use quando um teto, um nível que não chega ou uma recusa Sem calibração precisa de explicação. Lê pela porta o que a calibração guiada do app mediu (GET /limites, com calibrada, teto, tetoV, manda e as contas) e explica a diferença entre o esperado pela ficha e o medido. Não mede nem calibra; a calibração é do usuário no celular.
allowed-tools: Bash(porta:*)
---

# Medição assistida: o esperado e o medido

## Quando

Um ajuste esbarra no teto, o nível da memória não chega ao teto, ou uma saída está sem calibração e a porta recusa o
ganho, o limitador ou o EQ dela.

## Como

1. Leia o `GET /limites`. Por saída: `calibrada`, `teto` (dB do limitador), `tetoV` (volts RMS), `manda` (térmico,
   excursão ou ceifamento), `estimado`, `semLimite`, e cada limite com `v`, `selo`, `conta` e `falta`. No topo:
   `nivel`, `master` e `avisoNivel`.
2. O esperado vem das fichas (a potência e a impedância do alto-falante, a tensão de ceifamento do módulo); o medido é
   o que a calibração guiada do app guardou (o ganho do módulo). Use a `conta` que a porta devolve; não refaça com
   outro número.
3. Explique a diferença: qual limite manda e por quê, o que é estimado e o dado que falta (`falta`). Um teto que
   depende de um dado estimado vai dito como estimativa.
4. Sem calibração, não há número (uma saída sem calibração trava o ganho, o limitador e o EQ dela, e também o master,
   o EQ de entrada e a rota do Setup): a explicação diz que a calibração guiada é feita pelo usuário no celular. Você não
   mede nem calibra, e não pede ajuste de uma saída sem calibração.
5. A explicação vai para a tela no motivo do ajuste que ela afeta, ou numa resposta do "Como ficou?", sem a palavra
   "IA".

## Entrega

A explicação da diferença entre o esperado e o medido: o limite que manda, o que é estimado e o dado que falta.

## Fontes

- `conhecimento/ganho-e-limites.md` (potência e tensão, os três limites, sem calibração)
- `conhecimento/alto-falantes.md` (os dados da ficha que entram na conta)
