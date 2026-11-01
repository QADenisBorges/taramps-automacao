# Tarefa: criar uma memória

O usuário tocou em "+ Nova memória" no celular. Siga as regras do `CLAUDE.md` e use, em cada passo, a skill dita
abaixo. Você fala com o app **só pela ferramenta `porta`**, que recebe um JSON e devolve um JSON numa linha
(`{"ok":true,"status":200,"corpo":{...}}` ou `{"ok":false,"motivo":"..."}`):

```
porta --json '{"acao":"ler","alvo":"/ajuda"}'
porta --json '{"acao":"ler","alvo":"/estado"}'
porta --json '{"acao":"pedir","metodo":"POST","alvo":"/criacao/abrir"}'
porta --json '{"acao":"ler","alvo":"/eventos?desde=<ultimo>"}'
```

**Eventos:** antes de abrir, leia o `/eventos` uma vez e guarde só o `ultimo` do corpo (os eventos dessa leitura são
antigos). Depois, consulte sempre com `desde=<ultimo>` e atualize o `ultimo` a cada leitura. Nunca trate o que veio
antes do começo da tarefa (`desde=0` traz respostas de criações antigas). Entre duas consultas, espere de 1 a 10 s: com 30 s sem nenhum pedido à porta, a tarefa termina e a criação é
interrompida.

1. **Ler o caso.** `GET /ajuda`, `GET /estado`, `GET /catalogo` e `GET /limites`: o Setup, a cerca e o som atual vêm
   da porta, nunca de outro lugar. Uma saída sem calibração ou um nível que não chega ao teto: skill
   **medicao-assistida**.
2. **Perguntar** (skill **objetivo**). Abra a criação (`POST /criacao/abrir`) e mande uma pergunta por vez
   (`POST /criacao/pergunta`), a primeira sempre o objetivo. A resposta chega no `/eventos` como `criacao-resposta`.
   Quando uma resposta disser a situação, guarde-a e leia o perfil dela pela ferramenta `aprendizado`
   (`{"acao":"situacao",...}` e `{"acao":"perfil",...}`): é o ponto de partida das variáveis, com dúvida.
   Pare quando nenhuma resposta mudar um ajuste em mais de 1 dB nem uma rota: essa vai como a última.
3. **Aplicar** (skill **receita**). Depois da resposta à última pergunta, os ajustes na ordem da física
   (`POST /criacao/ajuste`, cada um com a frase e o motivo), as variáveis pela ferramenta `aprendizado`
   (`{"acao":"variaveis",...}`), o resumo (`POST /criacao/resumo`) e o "Como ficou?" (`POST /criacao/como-ficou`).
4. **Refinar.** A resposta chega como `criacao-como-ficou`. Com `salvar:true`, só espere. Uma queixa vai à skill
   **diagnostico**; um gosto, à skill **escuta** (`POST /criacao/escuta`, com a escolha em `criacao-escuta`). A mudança
   volta pela skill **receita**, só o que muda, com um novo resumo e um novo "Como ficou?".
5. **Fim.** Siga lendo o `/eventos` (com `desde=<ultimo>` e pelo menos 1 s entre as consultas) até `criacao-salva`,
   `criacao-cancelada` ou `criacao-interrompida`. Você não salva: salvar é do usuário.

Uma recusa da porta (`ok:false`) é uma regra do app: leia o motivo e não insista no mesmo pedido.

**A ferramenta `aprendizado`** só toca a pasta local, nunca o app; o mesmo formato (JSON na entrada, um JSON numa linha
na saída, com o `ok`):

```
aprendizado --json '{"acao":"variaveis","valores":{"grave":4.0,"cobertura":"varios"}}'
aprendizado <<'FIM'
{"acao":"situacao","nome":"<as palavras do usuário>"}
FIM
aprendizado <<'FIM'
{"acao":"perfil","situacao":"<as palavras do usuário>"}
FIM
aprendizado <<'FIM'
{"acao":"outro","texto":"<o Outro… dele>","variavel":"<a variável>"}
FIM
```

Quando o JSON leva texto do usuário, mande-o pelo stdin, num heredoc com o `'FIM'` entre aspas: um apóstrofo nas
palavras dele quebraria o `--json '...'`. Uma recusa (`ok:false`) traz o motivo (por exemplo, um nome de mais de 40
caracteres: encurte e mande de novo); não insista com a mesma chamada.

Você só diz a situação e as variáveis: o registro da criação e o perfil são gravados pelo serviço, no fim.
