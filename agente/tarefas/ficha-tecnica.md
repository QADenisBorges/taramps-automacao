# Tarefa: ficha técnica de um modelo

O usuário salvou um equipamento com um modelo que a base do celular não tem, e o app pediu a ficha (`ficha-pedida`).
O evento vem no fim deste pedido, em "O evento que acordou você": o `modelo`, o `grupo`, a `categoria` e o
`idEquipamento` (e o `idSetup`). Siga as regras do `CLAUDE.md` e use a skill **ficha-tecnica**.

Você fala com o app **só pela ferramenta `porta`**, que recebe um JSON e devolve um JSON numa linha:

```
porta --json '{"acao":"ler","alvo":"/ajuda"}'
porta --json '{"acao":"pedir","metodo":"POST","alvo":"/ficha/proposta","corpo":{"idEquipamento":"<do evento>","dados":[...]}}'
```

1. **Ler a ajuda.** `GET /ajuda`, na parte da proposta de ficha: os campos do `grupo` do evento, as unidades e as
   faixas.
2. **Buscar e conferir** (skill **ficha-tecnica**). Ache a ficha do `modelo` do evento, com o endereço de cada fonte,
   e confira se ela fecha. O que não achou fica fora; o que não fechou vai com o aviso.
3. **Propor.** `POST /ficha/proposta` com o `idEquipamento` do evento e cada dado com a origem (`oficial` ou
   `outra-fonte`), a fonte e o aviso. O pedido espera 60 s desde o evento: mande dentro do prazo.
4. **Fim.** Com a proposta aceita (`ok:true`), termine. Um 409 "O prazo da proposta acabou." também é o fim.

Só o toque do usuário salva a ficha, na folha do celular.
