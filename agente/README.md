# Agente da memória (notebook)

O Agente roda no notebook e fala com o app **só pela porta de comandos** (spec-agente-memoria, CAP-1).
Esta pasta tem três peças, em .NET 10 (solução `BgsAgente.slnx`, fora de `app-controle/`):

- **`porta`** (`src/Porta.Cli`): a ferramenta da porta. JSON na entrada, JSON numa linha na saída.
- **`servico`** (`src/Servico`): escuta o `/eventos` e acorda um Agente por vez.
- **modelo falso** (`src/Agente.ModeloFalso`): roteiros gravados no lugar do modelo, pela mesma `porta`.

Nada do sistema de quem usa fica aqui. O endereço, a chave, o estado, os logs e a pasta de trabalho do Agente ficam
na **pasta local**, fora do repositório: `%LOCALAPPDATA%\BgsAgente` (no Linux e no macOS, a pasta de dados locais do
usuário). Para usar outra, passe `--local <pasta>` ou defina `AGENTE_LOCAL`.

## Preparar

```
cd agente
dotnet build BgsAgente.slnx
dotnet publish src/Porta.Cli -c Release -o "%LOCALAPPDATA%\BgsAgente\bin"
```

O `publish` põe o `porta` em `bin`, dentro da pasta local. O serviço põe essa pasta no PATH do Agente
(`pastaPorta` no `servico.config.json`, relativa à pasta local).

## Parear

1. No celular, ligue a chave **Controle pela rede** (Configurações, grupo APARELHO). O endereço e o código de
   6 dígitos aparecem embaixo da chave.
2. No notebook:

```
"%LOCALAPPDATA%\BgsAgente\bin\porta" --json '{"acao":"parear","endereco":"<ip-do-celular>:8787","codigo":"<código>"}'
```

A saída é `{"ok":true,"status":200}`. O endereço e a chave ficam em `porta.json`, na pasta local. A chave nunca aparece na
saída. Com o código errado, a saída traz o motivo da porta e o código de saída é 1.

## Usar a ferramenta

```
porta --json '{"acao":"ler","alvo":"/estado"}'
porta --json '{"acao":"pedir","metodo":"POST","alvo":"/criacao/abrir"}'
porta --json '{"acao":"pedir","metodo":"DELETE","alvo":"/tarefa"}'
echo '{"acao":"ler","alvo":"/eventos?desde=0"}' | porta
```

- A saída é `{"ok":true,"status":200,"corpo":{...}}` (o `corpo` é a resposta do app) ou
  `{"ok":false,"status":409,"motivo":"..."}`. O código de saída é 0 no sucesso e 1 na recusa ou erro.
- O `ler` é um GET. O `pedir` leva o `metodo` (GET, POST ou DELETE) e o `corpo`.
- Sem pareamento, a saída é `{"ok":false,"motivo":"Sem pareamento: rode porta parear."}`.
- Cada pedido espera até 70 s (cabe a folha de risco de 60 s do celular).
- A pasta do pareamento é a `--local <pasta>`, ou a da variável `AGENTE_LOCAL`, ou `%LOCALAPPDATA%\BgsAgente`.
- O `codigo` vai entre aspas (os zeros à esquerda contam). O endereço guardado é conferido a cada pedido: a chave só
  vai para aquele ip:porta.
- O `GET /ajuda` da porta lista todos os caminhos.

## Rodar o serviço

```
cd agente
dotnet run --project src/Servico
```

- Lê o `/eventos` a cada 1 s e guarda o último evento em `servico.json` (na pasta local). Ao subir, começa do evento
  atual. Depois de uma queda, relê tudo e trata só o que é mais novo que o último evento visto (se o app reabriu, a
  contagem recomeçou).
- Os gatilhos ficam no `servico.config.json`: `criacao-pedida` acorda a tarefa `criar-memoria`, e
  `ficha-pedida` (da história 14b) acorda a `ficha-tecnica`. O texto de cada tarefa é `tarefas/<tarefa>.md`.
- Um Agente por vez. Um pedido com um Agente rodando é ignorado, com uma linha no log.
- Prazo do Agente: 30 min (`prazoAgenteMin`, até 24 h). Depois disso, o processo é encerrado.
- No fim do Agente (por qualquer motivo: saiu sozinho, prazo, erro, serviço parando), se a criação ficou aberta nas
  perguntas, nos ajustes ou no resumo, o serviço manda `POST /criacao/cancelar`. No "Como ficou?" e no Salvar, não:
  o usuário ainda pode salvar. O `DELETE /tarefa` só vai se não havia tarefa antes deste Agente. O código de saída e a
  duração vão ao log.
- Se a porta cair, o serviço tenta de novo a cada 5 s, com uma linha no log a cada mudança de estado.
- Log: `servico.log`; a saída do Agente vai para `agente.log` (os dois na pasta local; passados 5 MB, o atual vira
  `.1`). Ctrl+C para o serviço.
- Uma configuração inválida (JSON estragado, tempo fora da faixa, `lancador.tipo` desconhecido) para o serviço com o
  código 2 e a mensagem.

## Trocar o lançador

O lançador fica em `lancador`, no `servico.config.json`:

- **`"tipo": "claude"`** (o padrão): roda o `comando` com os `argumentos` numa **pasta de trabalho isolada**,
  `<pasta local>\trabalho`, fora do repositório. Antes de cada lançamento, o serviço copia `agente/tarefas/` para lá.
  O `{prompt}` vira o texto de `tarefas/<tarefa>.md` e o `{tarefa}`, o nome dela. O exemplo é o Claude Code em modo não interativo
  (`claude -p "<prompt>" --output-format json`), com a ferramenta `porta` liberada (`--allowedTools "Bash(porta:*)"`).
  No Windows, o `comando` precisa ser um executável (`claude.exe`); se ele não estiver no PATH, use o caminho
  completo. Um `claude.cmd` (instalação pelo npm) não serve: o prompt tem várias linhas e o `cmd` as corta.
- **`"tipo": "modelo-falso"`**: roda o roteiro de `roteiros` da tarefa (por exemplo,
  `testes/roteiros/criacao-basica.json`), sem modelo nenhum e sem custo.

Para outro lançador em código, implemente `ILancadorAgente` (`src/Agente.Servico/Lancadores.cs`). O
`LancadorProcesso` já cuida do prazo, do fim do processo e da saída.

Só `"claude"` e `"modelo-falso"` valem: outro tipo para o serviço (código 2), nunca roda o Claude sem querer.

### O que o Agente ainda carrega (para a história 12e conferir)

O Agente começa zerado: rodando fora do repositório, ele não carrega o `CLAUDE.md` do projeto nem a memória automática
do projeto, e a chave fica fora da pasta de trabalho (em `porta.json`, uma pasta acima). Ainda carrega o que é do
usuário no notebook: o `~/.claude/CLAUDE.md`, o `~/.claude/settings.json` (e as permissões, os plugins, as skills e os
servidores MCP do usuário), um `CLAUDE.md` que exista numa pasta acima da pasta local (por exemplo, na pasta do
usuário) e a memória automática da própria pasta de trabalho (que nasce vazia e cresce com o uso). A pasta de
trabalho dentro do repositório é recusada.

## Modelo falso e roteiros

Um roteiro (`testes/roteiros/*.json`) é uma lista de passos, só com dados genéricos:

- `pedir`: um pedido à porta (`metodo`, `alvo`, `corpo`).
- `esperar`: um evento do `/eventos` de um dos `tipos`, com o prazo em `prazoS`.
- `fim`: termina o roteiro.

Depois de cada passo, o `se` ramifica pelo resultado (`{"campo":"texto","igual":"...","ir":"<id>"}`). O `ir` muda o
próximo passo. O `seFalhar` e o `seVencer` tratam a recusa e o prazo vencido. Se a criação for cancelada ou
interrompida durante uma espera, o roteiro termina.

```
dotnet run --project src/Agente.ModeloFalso -- --roteiro testes/roteiros/criacao-basica.json
```

## Testes

```
dotnet test tests/Agente.Testes
```

Os testes sobem a `PortaComandos` do núcleo do app sobre o `ProcessadorSimulado`, numa porta livre de localhost. O
serviço roda no próprio processo dos testes; a ferramenta `porta` e o modelo falso rodam como processos de verdade.
Cada teste usa uma pasta local temporária. Nenhum teste chama o modelo de verdade.
