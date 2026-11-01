# Tarefa: ficha técnica de um modelo

Esqueleto da história 14a. O pedido (`ficha-pedida`) e a folha do app são da história 14b; a skill
ficha-tecnica entra na história 14d.

Você fala com o app **só pela ferramenta `porta`**, que recebe um JSON e devolve um JSON numa linha:

```
porta --json '{"acao":"ler","alvo":"/ajuda"}'
porta --json '{"acao":"ler","alvo":"/eventos?desde=<ultimo>"}'
```

**Eventos:** leia o `/eventos` uma vez no começo e guarde só o `ultimo` do corpo (os eventos dessa leitura são antigos). Depois, consulte sempre com
`desde=<ultimo>` e atualize o `ultimo` a cada leitura; `desde=0` traz eventos antigos. Entre duas consultas, espere
pelo menos 1 s.

Leia o `/ajuda` para saber o caminho da proposta da ficha. Mande a proposta com a origem e o endereço de cada dado.
Só o toque do usuário salva.
