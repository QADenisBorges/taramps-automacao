# Tarefa: criar uma memória

Esqueleto da história 14a. As skills (objetivo, receita, escuta, diagnóstico) entram na história 14d.

O usuário tocou em "+ Nova memória" no celular. Você fala com o app **só pela ferramenta `porta`**, que recebe
um JSON e devolve um JSON numa linha (`{"ok":true,"status":200,"corpo":{...}}` ou `{"ok":false,"motivo":"..."}`):

```
porta --json '{"acao":"ler","alvo":"/ajuda"}'
porta --json '{"acao":"ler","alvo":"/estado"}'
porta --json '{"acao":"pedir","metodo":"POST","alvo":"/criacao/abrir"}'
porta --json '{"acao":"ler","alvo":"/eventos?desde=<ultimo>"}'
```

**Eventos:** antes de abrir, leia o `/eventos` uma vez e guarde só o `ultimo` do corpo (os eventos dessa leitura são antigos). Depois, consulte sempre com
`desde=<ultimo>` e atualize o `ultimo` a cada leitura. Nunca trate o que veio antes do começo da tarefa (`desde=0`
traz respostas de criações antigas). Entre duas consultas, espere pelo menos 1 s.

1. Leia `/ajuda`, `/estado`, `/catalogo` e `/limites`: o Setup, a cerca e o som atual vêm da porta, nunca de outro lugar.
2. Abra a criação (`POST /criacao/abrir`) e mande uma pergunta por vez (`POST /criacao/pergunta`); a resposta chega no
   `/eventos` como `criacao-resposta` (consulte com `desde=<ultimo>`, como acima).
3. Depois da resposta à última pergunta, mande os ajustes (`POST /criacao/ajuste`), cada um com a frase e o motivo,
   e o resumo (`POST /criacao/resumo`). Depois, o "Como ficou?" (`POST /criacao/como-ficou`).
4. Siga lendo o `/eventos` (com `desde=<ultimo>` e pelo menos 1 s entre as consultas) até `criacao-salva` ou
   `criacao-cancelada`. Você não salva: salvar é do usuário.

Uma recusa da porta (`ok:false`) é uma regra do app: leia o motivo e não insista no mesmo pedido.
