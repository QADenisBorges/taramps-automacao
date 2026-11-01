---
name: ficha-tecnica
description: Use quando o app pede a ficha de um modelo que a base do celular não tem (evento ficha-pedida). Busca a ficha do modelo na web, confere se ela fecha (o Qts pelo Qes e pelo Qms, a sensibilidade pelos parâmetros) e manda a proposta (POST /ficha/proposta) com a origem de cada dado (oficial ou outra-fonte), a fonte (o endereço) e o aviso do que não fechou. Nunca inventa um dado.
allowed-tools: Bash(porta:*), WebSearch, WebFetch
---

# Ficha técnica de um modelo

## Quando

O evento `ficha-pedida` acordou você. Ele vem no pedido, em "O evento que acordou você": `modelo`, `grupo`,
`categoria`, `idEquipamento` e `idSetup`.

## Como

1. Leia o `GET /ajuda`, na parte da proposta de ficha: os campos de cada grupo, as unidades e as faixas.
2. Busque a ficha do `modelo` (WebSearch) e leia a página (WebFetch). Primeiro o fabricante: a ficha, o manual, o
   site. Origem `oficial` é do fabricante; `outra-fonte` é loja, fórum ou medida de terceiros.
3. Confira se a ficha fecha, e diga no `aviso` do dado (até 120 caracteres, numa linha) o que não fechou, com os
   dois números. Num alto-falante:
   - o Qts declarado contra Qes × Qms / (Qes + Qms);
   - a sensibilidade declarada contra a dos parâmetros (Fs, Vas e Qes), na mesma referência (1 W ou 2,83 V);
   - o Qes contra a massa, o Bl e o Re, quando a ficha os traz.
   Nos outros grupos (amplificador, ativo), a potência por canal contra a tensão de ceifamento, por V = √(P × Z).
4. Converta para a unidade da porta (Sd em cm², Xmax em mm, Le em mH, Vas em L). A porta recusa um valor fora da faixa
   do campo: é a unidade errada.
5. Nunca invente: o dado que não achou fica fora, e um valor calculado não entra como dado da ficha.
6. Mande `POST /ficha/proposta` com o `idEquipamento` do evento e os `dados`: cada um com `campo`, `valor`, `origem`,
   `fonte` (o endereço da página, até 120 caracteres, sem parâmetros de rastreio) e, se for o caso, `aviso`. Só os
   campos do grupo do evento.
7. O pedido espera 60 s desde a `hora` do evento (conte dela, não do seu começo): mande o que já conferiu dentro do prazo. Um 400 aponta o dado inválido:
   corrija só ele e mande de novo. Um 409 "O prazo da proposta acabou." é o fim: não insista.
8. Só o toque do usuário salva, na folha do celular. Você não salva nem pede que ele salve.

## Entrega

A proposta (`POST /ficha/proposta`): cada dado com a origem (`oficial` ou `outra-fonte`), a fonte (o endereço) e o
aviso do que não fechou.

## Fontes

- `conhecimento/alto-falantes.md` (as contas que conferem a ficha, as unidades e a origem)
- `conhecimento/ganho-e-limites.md` (potência e tensão, para a ficha do amplificador)
