---
name: escuta
description: Use só no refino de uma memória, depois de um Como ficou respondido sem salvar, quando o pedido é de gosto (mais grave, mais brilho) e não uma queixa. Compara uma variável por vez pela escuta 1 ou 2 (POST /criacao/escuta), com duas versões que só diferem nela; cada escolha (criacao-escuta) corta a dúvida daquela variável pela metade.
allowed-tools: Bash(porta:*)
---

# Escuta: "1 ou 2"

## Quando

Só no refino: a etapa `ajustando` numa rodada 2 ou mais, depois de um "Como ficou?" respondido sem salvar. Fora dele,
a porta recusa com 409 "A escuta só vale no refino.". Uma queixa ("as vozes estão baixas") é da skill diagnostico.

## Como

1. Uma variável por vez: a de maior dúvida que restou na ficha de variáveis.
2. Monte duas versões que só diferem nela. Cada versão é uma lista de ajustes como no `/criacao/ajuste` (`codigo`,
   `indice`, `valor`, `frase`, `motivo`), dentro da cerca (`GET /limites`) e só da regulagem.
3. Com a dúvida da variável num intervalo, as versões ficam nos pontos que o dividem; a escolha guarda a metade do
   lado escolhido. "Tanto faz" diz que a diferença não importa: o app volta à versão 1; feche a variável nela.
4. `POST /criacao/escuta` com `texto` (a pergunta, em palavras do som, sem a palavra "IA"), `variavel`, `versao1` e
   `versao2`. O celular iguala o volume das duas pelo master: não iguale você, e não mande um master para isso.
5. A escolha chega no `/eventos` como `criacao-escuta` (`variavel`, `escolha`: 1, 2 ou "tanto-faz", `rodada`). O som
   fica na versão escolhida e a etapa volta a `ajustando`.
6. Repita até a dúvida da variável ficar em até 1 dB, ou passe à próxima. Depois, `POST /criacao/resumo` e um novo
   "Como ficou?" (`POST /criacao/como-ficou`).
7. Uma recusa (outra escuta aberta, a cerca, uma saída sem calibração, volume que não dá para igualar) traz o motivo:
   mude o pedido; não mande o mesmo de novo.

## Entrega

As versões 1 e 2 da escuta mandada e, depois da escolha, a nova dúvida daquela variável.

## Fontes

- `conhecimento/percepcao.md` (o nível igualado, o 1 dB, a dúvida cortada pela metade)
- `conhecimento/dinamica-e-tom.md` (o tom como inclinação, o gosto como dúvida)
