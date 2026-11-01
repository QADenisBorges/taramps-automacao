---
name: objetivo
description: Use ao criar uma memória, antes de cada pergunta ao usuário. Monta a ficha de variáveis (fenômenos como objetivo, cobertura, posição de quem ouve, ambiente, tom, voz, dinâmica e cansaço, cada uma só se o Setup permitir), tira o padrão de cada uma da física e do Setup lido na porta, com a dúvida, escolhe a próxima pergunta pela maior dúvida vezes impacto, manda uma pergunta por vez (POST /criacao/pergunta) e diz quando parar, quando nenhuma resposta muda um ajuste em mais de 1 dB nem uma rota.
allowed-tools: Bash(porta:*)
---

# Objetivo: a ficha de variáveis e a próxima pergunta

## Quando

Na criação de uma memória, do começo até a resposta à última pergunta. A primeira pergunta é sempre o objetivo.

## Como

1. Leia o caso pela porta: `GET /estado` (o Setup, o ambiente, as memórias e o som atual), `GET /catalogo` (o que se
   ajusta e as faixas), `GET /limites` (a cerca, o nível e se cada saída está calibrada) e `GET /criacao`.
2. Monte a ficha de variáveis. Elas são fenômenos, não casos, e cada uma só existe se o Setup permitir:

   | Variável | O que ela move | Existe se |
   |---|---|---|
   | objetivo (fidelidade × nível máximo) | rota, ganho, corte, dinâmica | sempre |
   | cobertura (um ouvinte no ponto ideal × vários ou fora do eixo) | rota (separadas × somadas), atraso | há mais de uma saída com alto-falante |
   | posição de quem ouve | atraso, equilíbrio entre as saídas | as caixas ficam em posições diferentes |
   | ambiente (do Setup) | grave, alcance | sempre; sem ambiente no `/estado`, é dúvida |
   | tom | inclinação da curva, nível da saída de grave | sempre |
   | voz | a faixa de 1 a 4 kHz, a compressão | o conteúdo tem voz (pergunte; não suponha) |
   | dinâmica | limitador, compressor | o `/catalogo` tem o ajuste |
   | cansaço | agudo, teto | as sessões são longas (pergunte) |

3. Dê a cada variável um padrão, tirado da física e do Setup, e uma dúvida: o quanto as respostas possíveis mudam os
   ajustes (em dB) ou se mudam uma rota. Nenhum padrão vem de memória do aparelho, de regulagem antiga ou de outro caso.
4. A primeira pergunta é o objetivo, com respostas montadas para aquele Setup e ambiente (um carro, uma caixa ao ar
   livre, uma sala…), nunca uma lista fixa de usos.
5. Depois, pergunte a variável de maior dúvida × impacto: a que mais muda os números entre uma resposta e outra.
6. Uma pergunta por vez: `POST /criacao/pergunta` com `texto`, de 3 a 4 `respostas` e `ultima`. O celular acrescenta
   o "Outro…". Espere o `criacao-resposta` no `/eventos` antes da próxima. Um "Outro…" (`doOutro`) é texto livre: ligue-o
   a uma variável; se não ligar, pergunte de novo, de outro jeito. Um `criacao-resposta` com o `numero` menor que o da
   última é o ‹ do usuário: as perguntas depois dela saíram; refaça a ficha a partir dessa resposta e mande a próxima
   de novo.
7. Pare quando nenhuma resposta possível mudar um ajuste em mais de 1 dB nem uma rota: essa pergunta vai com
   `"ultima":true`, e a resposta a ela autoriza aplicar (skill receita). Passar de 8 perguntas mostra a ficha larga
   demais (o teto do sinal de sucesso da CAP-2, em `spec-agente-memoria/SPEC.md`).
8. Os textos vão para a tela: palavras de quem ouve, sem jargão e sem a palavra "IA".

## Entrega

A pergunta: o texto, 3 ou 4 respostas (o "Outro…" é do app) e se é a última. Ao parar, a ficha de variáveis, com o
valor e a dúvida de cada uma, para a skill receita.

## Fontes

- `conhecimento/percepcao.md` (o 1 dB, o volume que muda o tom, a voz)
- `conhecimento/sala-e-posicao.md` (quando o atraso e a posição existem como variável)
- `conhecimento/ganho-e-limites.md` (a rota: separadas × somadas)
