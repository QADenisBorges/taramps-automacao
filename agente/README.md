# Agente da memória (notebook)

O Agente roda no notebook e fala com o app **só pela porta de comandos** (spec-agente-memoria, CAP-1).
Esta pasta tem três peças, em .NET 10 (solução `BgsAgente.slnx`, fora de `app-controle/`):

- **`porta`** (`src/Porta.Cli`): a ferramenta da porta. JSON na entrada, JSON numa linha na saída.
- **`servico`** (`src/Servico`): escuta o `/eventos` e acorda um Agente por vez.
- **modelo falso** (`src/Agente.ModeloFalso`): roteiros gravados no lugar do modelo, pela mesma `porta`.
- **`aprendizado`** (`src/Aprendizado.Cli`, história 14e): a ferramenta do aprendizado, só na pasta local (veja
  "O aprendizado").

E as **instruções em camadas** do Agente (história 14d), versionadas aqui (veja "As instruções em camadas"), e o
**avaliador** (`avaliacao/Agente.Avaliacao`, história 14f), que prova o Agente com ouvintes simulados (veja "A avaliação").

Nada do sistema de quem usa fica aqui. O endereço, a chave, o estado, os logs e a pasta de trabalho do Agente ficam
na **pasta local**, fora do repositório: `%LOCALAPPDATA%\BgsAgente` (no Linux e no macOS, a pasta de dados locais do
usuário). Para usar outra, passe `--local <pasta>` ou defina `AGENTE_LOCAL`.

## Preparar

```
cd agente
dotnet build BgsAgente.slnx
dotnet publish src/Porta.Cli -c Release -o "%LOCALAPPDATA%\BgsAgente\bin"
dotnet publish src/Aprendizado.Cli -c Release -o "%LOCALAPPDATA%\BgsAgente\bin"
```

O `publish` põe o `porta` e o `aprendizado` em `bin`, dentro da pasta local. O serviço põe essa pasta no PATH do Agente
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
- O evento que acordou o Agente vai junto para o lançador (`ILancadorAgente.RodarAsync(tarefa, evento, …)`): o
  Claude o recebe no prompt, e o modelo falso, em `--evento`. A tarefa da ficha precisa dele (o modelo, o grupo, a
  categoria e o `idEquipamento`): quando o Agente acorda, o `/eventos` "desde o último" já não traz esse evento.
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
  `<pasta local>\trabalho`, fora do repositório. Antes de cada lançamento, o serviço recria lá as camadas (veja
  abaixo). Nos `argumentos`, o `{prompt}` vira o texto de `tarefas/<tarefa>.md`, com a seção "O evento que acordou
  você" (o JSON do evento) no fim; o `{tarefa}` vira o nome dela; e o `{ferramentas}`, as ferramentas da tarefa,
  separadas por espaço. O exemplo é o Claude Code em modo não interativo
  (`claude -p "<prompt>" --output-format json --allowedTools "{ferramentas}"`).
- **Ferramentas por tarefa** (`lancador.ferramentas`): a tarefa → a lista. A chave `"*"` é o padrão, a porta e o
  aprendizado (`["Bash(porta:*)", "Bash(aprendizado:*)"]`; o `aprendizado` só toca a pasta local); a `ficha-tecnica` tem `Bash(porta:*) WebSearch WebFetch`, porque a ficha precisa da fonte
  com o endereço, e sem ler a web o modelo teria de inventá-lo. A web só lê: nada nela escreve no app. Sem o `"*"`,
  ou com uma lista vazia ou um item vazio, o serviço para (código 2), como nas outras configurações inválidas.
  No Windows, o `comando` precisa ser um executável (`claude.exe`); se ele não estiver no PATH, use o caminho
  completo. Um `claude.cmd` (instalação pelo npm) não serve: o prompt tem várias linhas e o `cmd` as corta.
- **`"tipo": "modelo-falso"`**: roda o roteiro de `roteiros` da tarefa (por exemplo,
  `testes/roteiros/criacao-basica.json` e `testes/roteiros/ficha-basica.json`), sem modelo nenhum e sem custo.

Para outro lançador em código, implemente `ILancadorAgente` (`src/Agente.Servico/Lancadores.cs`). O
`LancadorProcesso` já cuida do prazo, do fim do processo e da saída.

Só `"claude"` e `"modelo-falso"` valem: outro tipo para o serviço (código 2), nunca roda o Claude sem querer.

### O que o Agente ainda carrega (para a história 12e conferir)

O Agente começa zerado: rodando fora do repositório, ele não carrega o `CLAUDE.md` do projeto nem a memória automática
do projeto, e a chave fica fora da pasta de trabalho (em `porta.json`, uma pasta acima). O `CLAUDE.md` e as skills da
pasta de trabalho são as camadas de `agente/` (história 14d), recriadas a cada lançamento. Ainda carrega o que é do
usuário no notebook: o `~/.claude/CLAUDE.md`, o `~/.claude/settings.json` (e as permissões, os plugins, as skills e os
servidores MCP do usuário), um `CLAUDE.md` que exista numa pasta acima da pasta local (por exemplo, na pasta do
usuário) e a memória automática da própria pasta de trabalho (que nasce vazia e cresce com o uso). A pasta de
trabalho dentro do repositório é recusada.

## As instruções em camadas

O que o Agente sabe fica em `agente/`, versionado (spec-agente-memoria, `cerebro-do-agente.md`, "Instruções em
camadas"). A cada lançamento, o serviço recria na pasta de trabalho as camadas abaixo (o que sobrou de antes nelas sai; o resto
da pasta de trabalho fica):

| Camada | Em `agente/` | Na pasta de trabalho |
|---|---|---|
| A base curta: as regras invioláveis (a cerca, nenhum valor sem motivo, a ordem da física, o Agente não salva, nada de "IA" na tela, os dados só pela porta, todo padrão é dúvida, o pedido, o calculado e o recomendado) | `instrucoes/base.md` (até 40 linhas) | `CLAUDE.md`, que o Claude Code carrega sozinho |
| Uma skill por função: objetivo, escuta, diagnostico, receita, ficha-tecnica e medicao-assistida (a cerca é código do app, não skill) | `skills/<nome>/SKILL.md` | `.claude/skills/<nome>/SKILL.md`, que carregam pela descrição |
| O conhecimento com fonte: as regras gerais de áudio, cada uma com a linha `Fonte:` (livro, norma, artigo ou documento do projeto) | `conhecimento/*.md` | `conhecimento/` |
| As tarefas: qual skill usar em cada passo | `tarefas/<tarefa>.md` | `tarefas/`, e o texto vai no prompt |

- Cada `SKILL.md` tem o frontmatter `name` (igual à pasta), `description` (quando usar, até 1024 caracteres) e
  `allowed-tools`, e o corpo com `## Entrega` (o que a skill devolve) e `## Fontes` (os arquivos de `conhecimento/`).
- **Só a porta:** toda skill fala com o app só pela ferramenta `porta` (`allowed-tools: Bash(porta:*)`; a
  ficha-tecnica acrescenta `WebSearch` e `WebFetch`, e a objetivo e a receita, `Bash(aprendizado:*)`, que só toca a
  pasta local). Nenhuma camada manda usar outro caminho até o app, e todo alvo citado existe no `GET /ajuda`.
- Uma regra de áudio sem fonte conferível fica fora. Nada do sistema de quem usa (Setup, fichas, memórias, regulagens
  antigas) entra nas camadas: o caso é lido pela porta.
- Os testes (`InstrucoesTestes`) conferem a estrutura, sem modelo: as skills, as fontes, só a porta, as ferramentas e
  a base curta.

## Modelo falso e roteiros

Um roteiro (`testes/roteiros/*.json`) é uma lista de passos, só com dados genéricos:

- `pedir`: um pedido à porta (`metodo`, `alvo`, `corpo`). No `corpo`, um valor de texto inteiro
  `"{evento.<campo>}"` vira o valor do campo do evento que acordou o Agente (o `--evento`), com o tipo dele; sem o
  evento ou sem o campo, o pedido não vai e conta como recusado.
- `esperar`: um evento do `/eventos` de um dos `tipos`, com o prazo em `prazoS`.
- `aprender`: o JSON vai à ferramenta `aprendizado` (`{"aprender":{"acao":"situacao","nome":"música"}}`), com os
  `"{evento.<campo>}"` trocados como no `pedir`. A saída dela é o resultado; um `ok:false` conta como recusado.
- `fim`: termina o roteiro.

Depois de cada passo, o `se` ramifica pelo resultado (`{"campo":"texto","igual":"...","ir":"<id>"}`). O `ir` muda o
próximo passo. O `seFalhar` e o `seVencer` tratam a recusa e o prazo vencido. Se a criação for cancelada ou
interrompida durante uma espera, o roteiro termina.

```
dotnet run --project src/Agente.ModeloFalso -- --roteiro testes/roteiros/criacao-basica.json
dotnet run --project src/Agente.ModeloFalso -- --roteiro testes/roteiros/ficha-basica.json --evento '{"tipo":"ficha-pedida","idEquipamento":"<id>"}'
```

## O aprendizado

O Agente aprende por arquivos (spec-agente-memoria CAP-7, história 14e). Tudo fica em `aprendizado/`, dentro da pasta
local, fora do repositório, e começa vazio: sem a pasta, tudo funciona e o perfil volta vazio. **O Agente só diz a
situação e as variáveis; o registro e a regra do perfil são do serviço, em código** (`src/Agente.Servico/Aprendizado.cs`).

### A ferramenta `aprendizado`

Como a `porta`: JSON na entrada (`--json`, o primeiro argumento ou o stdin), um JSON numa linha na saída, código 0 no ok
e 1 na recusa (`{"ok":false,"motivo":"..."}`). A pasta é a `--local <pasta>`, a `AGENTE_LOCAL` ou
`%LOCALAPPDATA%\BgsAgente`. Ela nunca fala com o app.

```
aprendizado --json '{"acao":"situacao","nome":"música"}'
aprendizado --json '{"acao":"variaveis","valores":{"grave":4.0,"cobertura":"varios"}}'
aprendizado --json '{"acao":"outro","texto":"som de show","variavel":"grave"}'
aprendizado --json '{"acao":"perfil","situacao":"música"}'
```

- `situacao`: a situação da criação de agora, com as palavras do usuário (texto livre, de 1 a 40 caracteres). A chave é
  o nome em minúsculas, sem acento, sem espaço nas pontas e com os espaços internos colapsados ("Música " e "musica"
  são a mesma situação). Os nomes das variáveis passam pela mesma chave.
- `variaveis`: junta as variáveis da criação de agora (número ou texto); uma chamada nova troca o valor da mesma variável.
- `outro`: liga um "Outro…" (o texto livre do usuário) a uma variável conhecida.
- `perfil`: `{"ok":true,"situacao":"musica","variaveis":{...},"outros":[...]}`; sem perfil, as listas vazias.
- Tudo da criação de agora vai para `aprendizado/agora.json`. Um JSON inválido, uma ação desconhecida, um nome vazio ou
  um valor que não é número nem texto dão a recusa com o motivo.

### Os registros

No fim de cada criação (`criacao-salva`, `criacao-cancelada` ou `criacao-interrompida`, vistos no `/eventos`), o
serviço grava `aprendizado/registros/<hora UTC>-<n>.json` e apaga o `agora.json`. Do `criacao-pedida` ao fim, ele lê o
`GET /criacao` a cada volta e guarda o último com a criação aberta. O registro traz:

- o `fim` (salva, cancelada ou interrompida; o `por` do cancelamento) e, se salva, a `memoria` (o nome vem do evento; o
  id, da memória com esse nome no `/estado`);
- o `dialogo` resumido: cada pergunta, com a resposta e o `doOutro`;
- o proposto × o que ficou: os `ajustes` de cada rodada (código, índice, antes, valor e relido) e o `somAntes`;
- as `escutas` (o "1 ou 2") e o `comoFicou` (o positivo é o `salvar:true`; o negativo, o texto do refino);
- o retorno sem palavras (`semPalavras`): o número de rodadas de refino e se cancelou;
- o `contexto`: a hora do fim, e o `nivel` e o `master` da memória em uso no `/estado`;
- a `situacao`, as `variaveis` e os `outros` do `agora.json`.

Uma criação sem o `GET /criacao` lido grava o registro com o que tiver.

**O ajuste à mão depois:** quando uma criação nova abre, se a memória em uso tem um registro salvo, o serviço compara o
que aquele registro salvou (o último relido de cada ajuste) com o som de agora (o `/estado`, ou o antes do primeiro
ajuste da criação nova, se ela já mexeu ali). Cada diferença maior que 0,05 entra no registro antigo em `ajustesDepois`
(o código, o índice, o valor salvo e o de agora).

### O perfil

Depois de gravar um registro salvo, o serviço refaz `aprendizado/perfis/<chave>.json` (uma chave com espaço ou símbolo
vira hífens e um resumo dela no nome do arquivo):

- só contam os registros salvos da mesma situação; o perfil de uma situação nunca muda com registros de outra;
- uma variável entra quando dois ou mais registros, de memórias diferentes (ids diferentes), têm a mesma escolha:
  números a até 1 dB um do outro (o valor é a média) ou o mesmo texto;
- com mais de um grupo, vale o grupo com mais memórias e, no empate, o mais recente; uma memória que repete a mesma
  escolha conta uma vez só;
- os "Outro…" entram como opção só quando têm a `variavel`.

A skill objetivo lê o perfil depois da resposta que diz a situação e o usa como ponto de partida, com dúvida: a memória
manda. A receita diz as variáveis antes do resumo.

## A avaliação

O Agente é provado por **ouvintes simulados** com gosto escondido (spec-agente-memoria CAP-8, história 14f). O avaliador
(`avaliacao/Agente.Avaliacao`) sobe, para cada perfil, o núcleo do app e a porta sobre o `ProcessadorSimulado`, numa pasta
local temporária; pareia; toca em "+ Nova memória"; lança o Agente com a tarefa `criar-memoria` e o evento; e deixa o
ouvinte simulado fazer o lado do usuário. Ele roda o lançador que recebe: os testes usam Agentes falsos em código; o
comando `real`, o `LancadorClaude`.

### Os perfis

Os 8 perfis ficam em `avaliacao/perfis/*.json`, só com dados genéricos (nenhum dado de um sistema real). Cada um traz:

- o **Setup** (`setup`): o módulo (a potência e o ceifamento por canal, e o ganho, que entra como medido: as saídas ficam
  calibradas e a cerca vale), as caixas (o canal do processador, de 0 a 3, o nome, a categoria, a potência RMS e a
  impedância) e o ambiente (`sala`, `carro`, `ar-livre` ou `outro`);
- os **fatos** (`fatos`) que o ouvinte sabe dizer em palavras (`{"objetivo":"o máximo que o kit aguenta","onde":"..."}`);
- o **gosto escondido** (`gosto`): os alvos `{codigo, indice, valor}` e a rota esperada por saída com caixa (`a`, `b` ou
  `a+b`);
- as **frases de refino** (`refino`): uma queixa e um gosto.

Eles cobrem um carro (dois gostos no mesmo Setup), uma caixa bob ao ar livre, uma sala 2.1 (dois gostos no mesmo Setup), um
sistema de 2 vias ativo, um quintal com conversa e o caso da SPL: o máximo do kit, a posição indiferente e as entradas
somadas em todas as saídas. Todo perfil parte do mesmo som: o de fábrica do simulador (neutro, rota a/b/a/b) com o
limitador em −30 dB, guardado como a memória `PARTIDA`, ativa.

### O ouvinte simulado

Código, sem modelo, direto na `CriacaoMemoria` do núcleo:

- **Pergunta:** toca na resposta que mais tem palavras em comum com os fatos (sem acento, sem maiúsculas, palavras de 3
  letras ou mais, fora as ligações como "que", "com" e "para"). Sem nenhuma em comum, usa o "Outro…" com o fato mais perto
  do texto da pergunta.
- **"1 ou 2":** escolhe a versão de menor distância aos alvos; em distâncias iguais, "Tanto faz".
- **"Como ficou?":** salva quando todo alvo está a até 1,5 dB (um alvo fora de dB, igual). Senão, responde a opção mais
  perto da frase de refino (a queixa, depois o gosto), ou o "Outro…" com ela. Depois de 3 refinos, salva assim mesmo.
- **O nome** da memória é `AVAL <id do perfil>`.

### As métricas

Por perfil:

- **Perguntas:** quantas o Agente fez (contando as refeitas pelo ‹).
- **Distância:** a maior diferença entre o valor final relido e o alvo, entre os alvos em dB (080, 081, 030, 091, 071).
  Um alvo em Hz, cm ou graus conta como acerto só se for igual.
- **Violações:** cada recusa da cerca (o 409 "Limite: …" no `/criacao/ajuste`, no `/criacao/escuta` ou no `/ajuste`); cada
  ajuste de tom (030 a 032, 090 a 092) seguido, na mesma rodada, de um de rota (010), ganho (080, 081), corte (040 a 043),
  atraso (050) ou fase (060); e cada limitador final fora da cerca.
- **Rota:** a final de cada saída com caixa igual à esperada.
- **Passa** com distância ≤ 1,5 dB, até 8 perguntas, nenhuma violação e a rota certa. O conjunto passa se todos passam.

O relatório (`relatorio.json` e `relatorio.txt`) traz, por perfil, as perguntas (o texto e a resposta), a distância, as
violações, a rota, o passou e o motivo; no topo, o passou geral, a data, o tipo (`falso` ou `real`) e o hash das camadas
(o SHA-256 dos arquivos `.md` de `instrucoes/`, `skills/`, `conhecimento/` e `tarefas/`, os que o serviço copia, pelos
caminhos e pelo conteúdo, em ordem; CRLF conta como LF).

### A avaliação real e o custo

```
dotnet run --project agente/avaliacao/Agente.Avaliacao -- real
dotnet run --project agente/avaliacao/Agente.Avaliacao -- real --confirmo-custo
```

- Sem `--confirmo-custo`, mostra o custo estimado (US$ 1,50 por perfil, US$ 12,00 os 8) e sai com o código 3, sem lançar
  nada.
- Com a confirmação, roda o Claude de verdade (o `lancador` do `servico.config.json`, com a `porta` da sua pasta local,
  `pastaPorta`), um perfil por vez, e grava o relatório em `avaliacao/relatorios/<data>/`, versionado. Sai com 0 se o
  conjunto passou, 1 se não.

### A trava: `conferir`

```
dotnet run --project agente/avaliacao/Agente.Avaliacao -- conferir
```

Decide pelo relatório real mais recente com o hash das camadas de agora: sai com 0 se ele passou. Senão, sai com 1 e o
motivo ("A última avaliação real com estas camadas não passou.", "As camadas mudaram desde a última avaliação real que
passou." ou "Nenhuma avaliação real passou ainda."). Um relatório ilegível fica de fora.

**Antes de levar uma mudança de `instrucoes/`, `skills/`, `conhecimento/` ou `tarefas/` ao `main`, rode o `conferir`.** Se
ele der 1, rode a avaliação real e versione o relatório junto com a mudança. Instalar um gancho do git que rode o
`conferir` sozinho é decisão de quem mantém o repositório; nada aqui o instala.

### Os Agentes falsos

Em `tests/Agente.Testes/AgentesFalsos.cs`, como `ILancadorAgente` em código, falando só pela porta:

- **o bom** lê o `/estado` e o `/limites`, pergunta o que depende do Setup, do ambiente e do objetivo, escolhe a rota pelo
  objetivo e por onde estão os ouvintes, aplica na ordem da física, dentro da cerca, e refina pela escuta "1 ou 2",
  cortando a dúvida pela metade: passa nos 8;
- **o genérico** faz as mesmas perguntas e a mesma receita para todos, sem escuta: o conjunto falha;
- **o das entradas separadas** é o bom com a rota sempre separada: falha no perfil da SPL pela rota.

## Testes

```
dotnet test tests/Agente.Testes
```

Os testes sobem a `PortaComandos` do núcleo do app sobre o `ProcessadorSimulado`, numa porta livre de localhost. O
serviço roda no próprio processo dos testes; a ferramenta `porta` e o modelo falso rodam como processos de verdade.
Cada teste usa uma pasta local temporária (o aprendizado também). Nenhum teste chama o modelo de verdade: os testes da
avaliação (`AvaliacaoTestes`) rodam os Agentes falsos nos 8 perfis.
