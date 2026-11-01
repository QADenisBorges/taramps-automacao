---
name: receita
description: Use depois da resposta à última pergunta, ou de uma mudança pedida no refino, para transformar as variáveis em ajustes na ordem da física (cerca, rota, ganho, corte, atraso e fase, depois tom, por último dinâmica). Cada ajuste vai com frase, valor de antes, valor novo e motivo (POST /criacao/ajuste), dentro da cerca; a rota é decisão com motivo, nunca padrão. Fecha com o resumo e o Como ficou.
allowed-tools: Bash(porta:*) Bash(aprendizado:*)
---

# Receita: das variáveis aos ajustes

## Quando

Depois da resposta à última pergunta (skill objetivo), ou no refino, para a mudança que a escuta ou o diagnóstico
pediram (só o que muda).

## Como

1. Releia a porta: `GET /limites` (a `cerca` de cada saída, `calibrada`, o nível e o master), `GET /catalogo` (os
   códigos, os índices e as faixas) e o `/estado` (o valor de antes de cada ajuste).
2. Traduza as variáveis em ajustes, nesta ordem:
   1. **cerca:** o limitador de cada saída dentro do teto; para baixar o passa-alta, baixe o limitador antes;
   2. **rota:** entradas separadas ou somadas em cada saída. É decisão com motivo, nunca padrão: separadas dão imagem;
      somadas dão nível, carga dividida e a música inteira para quem está fora do eixo, ao custo da imagem, da folga
      da soma e do que foi gravado com fase invertida;
   3. **ganho**, 4. **corte** (passa-alta e passa-baixa, pelo alcance de cada caixa), 5. **atraso e fase**;
   6. **tom** (EQ), só depois do tempo e da fase; corte o que sobra antes de reforçar o que falta. Só a regulagem
      passa pelo `/criacao/ajuste`: as 15 bandas, o preset de EQ, o link e o mute dão 400. Com o link ligado, a
      outra saída do par tem de acompanhar, senão o resumo dá 409;
   7. **dinâmica** (limitador como teto, compressão como forma).
3. Cada ajuste: `POST /criacao/ajuste` com `codigo`, `indice`, `valor`, `frase` (o que muda no som, em palavras de quem
   ouve, sem a palavra "IA") e `motivo` (a variável e a regra de `conhecimento/`). A resposta traz o antes, o novo e o
   relido: um relido diferente do pedido é refeito antes do resumo.
4. Uma saída sem calibração (`calibrada:false`) não recebe ganho, limitador nem EQ; e, com qualquer saída sem
   calibração, nada global (master, EQ de entrada, rota) passa: não mande; a skill
   medicao-assistida explica o que falta.
5. Uma recusa traz o motivo. A da cerca diz o teto: refaça abaixo dele, com o teto no motivo. Não insista no mesmo.
6. Um valor pedido pelo usuário (uma potência, por exemplo) vai com o pedido, o calculado (a tensão) e o recomendado,
   cada um com o motivo; o que não se sabe vai como estimativa.
7. Antes do resumo, diga as variáveis da criação (o valor final de cada uma na ficha, número ou texto) pela ferramenta
   `aprendizado`, que só grava na pasta local: `aprendizado --json '{"acao":"variaveis","valores":{"grave":4.0,"cobertura":"varios"}}'`.
   No refino, diga de novo só as que mudaram (a chamada nova troca o valor). Um valor de texto com palavras do usuário
   vai pelo stdin, num heredoc com o `'FIM'` entre aspas (`aprendizado <<'FIM'`, o JSON, `FIM`), porque um apóstrofo
   quebraria o `--json '...'`. Uma recusa (`ok:false`) traz o motivo (um valor que não é número nem texto, um nome
   repetido): corrija o que ele diz e mande de novo; não insista com a mesma chamada. O registro e o perfil são do serviço.
8. No fim, `POST /criacao/resumo` e o "Como ficou?" (`POST /criacao/como-ficou`) com 2 ou 3 respostas que já pensem no
   refino: uma de gosto e uma de queixa provável para aquele Setup.

## Entrega

Os ajustes, cada um com a frase, o valor de antes, o valor novo e o motivo, dentro da cerca e na ordem da física; o
resumo e o "Como ficou?".

## Fontes

- `conhecimento/ganho-e-limites.md` (a ordem da física, a rota, a folga, a tensão)
- `conhecimento/alto-falantes.md` (o corte pelo alcance da caixa)
- `conhecimento/sala-e-posicao.md` (o atraso, o corte entre caixas)
- `conhecimento/dinamica-e-tom.md` (o tom e a dinâmica)
