---
name: diagnostico
description: Use no refino de uma memória quando a resposta ao Como ficou é uma queixa (as vozes estão baixas, o grave embola, falta espaço). Transforma a queixa em hipóteses físicas e escolhe a pergunta que separa uma da outra; o que o processador não faz (separação, espaço, alcance) é posição da caixa, sem ajuste.
allowed-tools: Bash(porta:*)
---

# Diagnóstico da queixa

## Quando

No refino, quando o `criacao-como-ficou` traz uma queixa, não um gosto (gosto é da skill escuta).

## Como

1. Liste as hipóteses físicas que dão a mesma queixa. Por exemplo, para "a voz está baixa":
   - tom: falta a faixa de 1 a 4 kHz;
   - dinâmica: a voz some só quando o resto cresce;
   - posição: quem ouve está fora do eixo da caixa que leva a voz;
   - rota: entradas somadas cancelando o que foi gravado com fase invertida.
2. Escolha a pergunta cuja resposta é diferente para cada hipótese. Exemplo: "em tudo ou só nas partes mais fortes?".
   Só nas fortes é dinâmica (compressor), não EQ.
3. No refino, a pergunta vai pelo "Como ficou?", que só vale depois do resumo: feche a rodada com
   `POST /criacao/resumo` e mande `POST /criacao/como-ficou` com 2 ou 3 `respostas`, uma por
   hipótese, em palavras de quem ouve e sem a palavra "IA". A escolha volta como `criacao-como-ficou`.
4. Com a hipótese separada, a mudança vai pela skill receita (só o que muda), com o motivo dizendo a hipótese.
5. Separação, espaço e alcance não são ajuste do processador: são posição da caixa. Não mande ajuste para isso; diga a
   orientação de posição numa resposta do "Como ficou?" (por exemplo, afastar a caixa da parede ou abrir o ângulo).

## Entrega

A pergunta que separa as hipóteses, ou a orientação "é posição da caixa", sem ajuste.

## Fontes

- `conhecimento/dinamica-e-tom.md` (a queixa só nas partes fortes)
- `conhecimento/sala-e-posicao.md` (o que é posição da caixa)
- `conhecimento/percepcao.md` (a faixa da voz)
- `conhecimento/ganho-e-limites.md` (a soma que cancela)
