---
title: 'História 8b: player com uma playlist por memória'
type: 'feature'
created: '2026-10-22'
status: 'done'
route: 'dispatch'
baseline_commit: 'f3974dedad4c12402be7c671f3da5f07455c774f'
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/planning-artifacts/ux-designs/ux-taramps-automacao-2026-10-21/EXPERIENCE.md'
  - '{project-root}/_bmad-output/specs/spec-app-controle-taramps/stories/8-presets-conhecimento-herdado.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Para ouvir uma memória com música, o Denis ainda depende do Player do app da Taramps (CAP-15). O app próprio não tem playlists e não toca nada.

**Approach:** No núcleo:
- um `GerentePlaylists` guarda as 3 playlists em `playlists.json`, uma por posição de memória;
- um `Player` toca a playlist da memória carregada por uma porta `IReprodutor`.

Em `BgsControle.Telas` fica o `PlayerViewModel`, testado com reprodutor e seletor falsos. A parte Android fica para a história 11.

## Boundaries & Constraints

**Always:**
- **Ligado à memória (decisão do Denis, 22/10).** O Player só mostra e toca a playlist da memória carregada (`GerenteMemorias.EmUso`). Não há abas.
  - Para trocar de playlist, carrega-se outra memória.
  - Com `EmUso` nulo, nada de playlist nem de comandos: "Carregue uma memória para usar o Player."
- **Troca com música (decisão do Denis, 22/10).** Se `EmUso` muda, inclusive para nulo, o player para na hora e passa a mostrar a playlist da nova memória, parado.
- **Faixa:** id único estável, a URI do arquivo (o app não copia a música), nome, pasta e duração. Tudo vem do `ISeletorAudio` na hora de adicionar.
- **`playlists.json`:** formato 1, uma lista por posição (0–2), lida no início (`Carregada`), com evento `Mudou`.
  - Grava com `Gravacao.GravarAsync`, e a lista só muda se gravou.
  - Se falhar: "Não consegui guardar a playlist no celular."
  - Arquivo ausente ou estragado vira 3 playlists vazias, sem erro.
- **Editar:** adicionar vários de uma vez, no fim, na ordem escolhida; remover; mover de uma posição para outra. Nada disso manda nada ao processador nem precisa de chave.
- **Tocar:**
  - Tocar/pausar e Próxima. Tocar numa linha toca aquela faixa.
  - No fim de uma faixa, segue para a próxima. Depois da última, o player para e o próximo Tocar recomeça pela primeira.
  - Faixa cujo arquivo sumiu (o reprodutor recusa) aparece como "Arquivo não encontrado" e é pulada. Se nenhuma tocar: "Nenhuma faixa disponível na playlist da NOME." e o player para.
  - Remover a faixa que está tocando para o player.
- **Textos:**
  - "Playlist da NOME" (nome de `GerenteMemorias.Nome(n)`; sem nome, "Memória N")
  - "Playlist da NOME vazia. Toque em Adicionar músicas."
  - Duração em m:ss.

**Never:**
- XAML, navegação, `MediaPlayer`, seletor do Android ou DI no `MauiProgram` (história 11).
- Tocar em segundo plano ou ter notificação (história 11 ou 12).
- Escrever no processador.
- Copiar a música para o app.

## I/O & Edge-Case Matrix

| Cenário | Entrada / estado | Esperado | Erro |
|---|---|---|---|
| Sem memória | `EmUso` nulo | Sem lista e sem comandos | "Carregue uma memória para usar o Player." |
| Vazia | FILME carregada, sem faixas | "Playlist da FILME" | "Playlist da FILME vazia. Toque em Adicionar músicas." |
| Adicionar | 3 arquivos escolhidos | Entram no fim, na ordem; arquivo gravado | — |
| Ordenar | Mover a 3 para a 1 | Nova ordem gravada | — |
| Tocar e seguir | A faixa 1 termina | Toca a 2; a marca de "tocando" anda | — |
| Arquivo sumiu | A 2 não existe mais | A 2 aparece indisponível; toca a 3 | — |
| Nenhuma disponível | Todos sumiram | Parado | "Nenhuma faixa disponível na playlist da FILME." |
| Fim da lista | A última termina | Parado; Tocar volta à primeira | — |
| Troca de memória | Tocando; `EmUso` muda de 2 para 0 | Para na hora; mostra a playlist da 0, parada | — |
| Remover a que toca | Remove a atual | Parado; lista gravada | — |
| Reabrir | Mesmo repositório, novo gerente | As 3 playlists voltam na ordem | — |
| Arquivo estragado | `playlists.json` ilegível | 3 vazias; adicionar funciona | — |
| Falha ao gravar | Repositório que falha | A lista não muda | "Não consegui guardar a playlist no celular." |

</frozen-after-approval>

## Code Map

Os caminhos são relativos a `app-controle/`.

- `src/BgsControle.Nucleo/Presets/GerentePresets.cs`: molde do guardado.
  - `Carregada` (l. 105) e `Mudou` (l. 112).
  - `GravarAsync` privado (l. 403), que só troca a lista se gravou.
  - `CarregarAsync` (l. 416) e `ParaJson`/`DeJson` (l. 437/471), que engolem `JsonException`/`InvalidOperationException`.
- `src/BgsControle.Nucleo/Escrita/IRepositorioTexto.cs`: `Gravacao` é internal. Por isso o gerente e o `Player` ficam no Nucleo.
- `src/BgsControle.Nucleo/Memorias/GerenteMemorias.cs`: `EmUso` (l. 115; nulo depois do 241, `n` ao fim do carregar ou do salvar), `Nome(n)` (l. 141), `Mudou` (em qualquer thread).
- `src/BgsControle.Telas/MemoriasViewModel.cs`: modelo do ViewModel.
  - Assinaturas (l. 161–168), `NaTela(Atualizar)`.
  - `Agir` (l. 502), `Mensagem`, `Ocupado` e `Dispose` (l. 581).
  - Listas em `IReadOnlyList` trocada inteira (`PresetsViewModel.Atualizar`, l. 579).
- `src/BgsControle.Telas/Abstracoes.cs`: entra o `ISeletorAudio` (escolhe vários arquivos e devolve URI, nome, pasta e duração). `RepositorioTextoArquivos.cs`: `ArquivoPlaylists = "playlists.json"`.
- Testes:
  - `tests/BgsControle.Nucleo.Testes/MemoriasTestes.cs:12` (`TextoMemoria`);
  - `tests/BgsControle.Telas.Testes/Falsos.cs` (`DespachanteFila`, `ArquivosFalso`) e a `Bancada` (`ControleParametroViewModelTestes.cs:19`, `CriarMemoriasViewModel` l. 76);
  - `ArquiteturaTestes.cs`: Telas sem Maui.

## Tasks & Acceptance

**Execution:**
- [x] `src/BgsControle.Nucleo/Player/` (`Faixa`, `GerentePlaylists`, `IReprodutor`, `Player`) -- playlists em JSON, a fila, pular o arquivo que sumiu, parar na troca de memória -- CAP-15
- [x] `src/BgsControle.Telas/` (`PlayerViewModel`, `ISeletorAudio`, `ArquivoPlaylists`) -- título, aviso, linhas (nome, m:ss, pasta, tocando, indisponível), Editar/Pronto, Adicionar, Remover, Mover, Tocar/pausar, Próxima, Mensagem
- [x] `tests/` -- um teste por linha da matriz, com um `ReprodutorFalso` (termina faixas sob comando, recusa URIs ausentes) e um `SeletorFalso`

**Acceptance Criteria:**
- Given qualquer teste da 8b com o `ProcessadorSimulado`, when ele é inspecionado, then nenhuma escrita chegou ao aparelho.

## Implementation Notes

- **Critério de aceite e a memória em uso.** O `EmUso` só fica definido depois de um carregar (241) ou salvar (240) de verdade. Os testes do `PlayerViewModel` carregam a memória pelo `GerenteMemorias` e o guarda do `Dispose` confere que, fora a senha e as leituras, as únicas escritas no simulador são os `*241*n*` que o próprio teste pediu. Player e playlists não escrevem nada.
- **Namespace `BgsControle.Nucleo.Player` e a classe `Player`.** Dentro de `BgsControle.Nucleo.*` o nome `Player` resolve para o namespace; por isso os testes do Player ficam em `BgsControle.Telas.Testes` e os do `GerentePlaylists` em `PlaylistsTestes` (Nucleo.Testes), que não usa o tipo `Player`.
- **Próxima com o Player parado** não faz nada. Depois da última faixa, Próxima também para.
- **"Nenhuma faixa disponível…"** aparece quando todas as faixas da playlist ficaram marcadas como indisponíveis. Chegar ao fim da lista depois de pular as últimas só para, sem a frase.
- **Faixa indisponível** vale enquanto o app está aberto (não vai para o `playlists.json`); tocar de novo nela tenta outra vez.

## Spec Change Log

## Review Triage Log

### 2026-10-22 — Revisão

- **Veredictos:** 36 achados. 0 high, 5 medium, 23 low, 4 false e 4 maybe-false. Viraram 5 grupos de patch e 3 adiados.

**Blind Hunter**
- `[low]` `[reject]` **Adicionar pode cair na playlist errada se a memória mudar com o seletor aberto.** Hoje o `EmUso` só muda por um carregar ou salvar feito no app, o que não acontece com o seletor do Android por cima. A porta da IA (9b) reavalia isso.
- `[low]` `[reject]` **Um `Terminou` atrasado da mesma URI (faixa repetida) pula a faixa recém-iniciada.** É uma corrida de milissegundos entre o fim e um toque. A correção mexeria no contrato (um token por execução).
- `[maybe-false]` `[defer]` **O `IReprodutor` não tem evento de erro ou interrupção** (foco de áudio, arquivo apagado no meio). Depende do adaptador Android da história 11. Está no `deferred-work.md`.
- `[low]` `[reject]` **O contrato do `ISeletorAudio` não exige a permissão persistente da URI.** Já está no item adiado do adaptador Android (TakePersistableUriPermission).
- `[low]` `[reject]` **Um `playlists.json` estragado ou de outro formato é sobrescrito na próxima edição.** A spec manda "3 vazias, sem erro", e a história 8 decidiu igual para o `presets.json`.
- `[false]` `[reject]` **Uma leitura que falha quebraria as edições.** Falso: o `Gravacao.LerAsync` engole qualquer erro e devolve null. O caso real, um `DeJson` que lança, está no patch de duração.
- `[maybe-false]` `[defer]` **Os erros do Player não aparecem na tela.** É a mesma raiz do `TocarAsync` que lança. Adiado junto com o contrato do reprodutor.
- `[medium]` `[patch]` **Nada bloqueia o toque duplo.** O seletor abria duas vezes, e o `Ocupado` era zerado pela primeira edição.
- `[low]` `[patch]` **A frase e o modo Editar sobreviviam à troca de memória.** Agrupado com os dois achados de frase velha do Edge Case Hunter.
- `[low]` `[reject]` **As marcas de indisponível nunca são limpas.** A regra está nas Implementation Notes: valem enquanto o app está aberto, e tocar de novo tenta outra vez. O teste novo prova o "tenta de novo".
- `[low]` `[reject]` **Testes que faltam:**
  - **remover a faixa pausada, trocar de memória pausado, Próxima parado:** passam pelos mesmos caminhos já testados (`PararAsync`, `SincronizarAsync`, `Atual` nulo);
  - **`MoverAsync` com `de == para` no núcleo:** o ViewModel não chama.
  
  Os dois casos com risco entraram no patch de testes.
- `[low]` `[reject]` **`Dispose` não para o som, e o `GerentePlaylists` não é `IDisposable`.** Os dois são singletons da vida do app (história 11).
- `[false]` `[reject]` **O diff deixa de fora a spec e o `deferred-work.md`.** É de propósito: o diff da revisão traz só o código.
- `[low]` `[reject]` **"1 faixas" no log.** É só no log, e o Denis não vê.

**Verification Gap**
- `[medium]` `[patch]` **A faixa marcada que volta a existir não tem teste.** Teste novo: `Arquivo_que_volta_e_tentado_de_novo_toca_e_perde_a_marca`.
- `[medium]` `[patch]` **O fim depois de pular as últimas, sem a frase, não tem teste.** Teste novo: `Ultimas_indisponiveis_no_fim_da_faixa_1_para_sem_a_frase_e_tocar_volta_a_1`.
- `[low]` `[defer]` **O título "Playlist da Memória N" não tem teste.** Precisa de preparo no simulador. Está no `deferred-work.md`.
- `[low]` `[defer]` **A limpeza da entrada do `AdicionarAsync` não tem teste.** Só importa com o seletor real. Está no `deferred-work.md`.

**Edge Case Hunter**
- `[low]` `[patch]` **`duracaoMs` enorme estoura o `TimeSpan`, o `Carregada` falha e nenhuma edição funciona mais.** Agrupado com os dois seguintes. Teste novo com 5 valores.
- `[low]` `[patch]` **Exceções fora de Json/InvalidOperation/Format no `DeJson`.** Mesma raiz. Agora o `CarregarAsync` cai em 3 vazias com qualquer exceção.
- `[low]` `[patch]` **`duracaoMs` em texto zera as 3 playlists.** Mesma raiz. Agora só aceita número.
- `[low]` `[reject]` **Uma falha passageira de leitura faz a próxima edição apagar o arquivo.** O comportamento vem do `Gravacao.LerAsync`, que já existia e é o mesmo do `presets.json` e do `link-canais.json`. Uma falha de leitura nos arquivos privados é raríssima.
- `[low]` `[reject]` **Todos os arquivos escolhidos com URI em branco somem sem frase.** O seletor do Android não devolve URI vazia.
- `[low]` `[reject]` **Um `Terminou` depois de um Pausar na fila deixa o Player pausado numa faixa que acabou.** É uma corrida de milissegundos. O próximo Tocar continua, e o reprodutor real devolve o fim de novo.
- `[low]` `[reject]` **Um `Terminou` velho com a mesma URI.** Igual ao segundo achado do Blind Hunter.
- `[low]` `[reject]` **Recarregar a mesma memória (2 → nulo → 2) com a porta ocupada.** A porta só fica ocupada durante um comando curto, e o carregar leva cerca de 13 s nulo. O `Sincronizar` vê o nulo.
- `[low]` `[reject]` **Uma edição entre o `Sincronizar` e o comando faz o `Posicao` dar −1 e recomeçar da primeira.** É uma corrida de milissegundos, e o `Mudou` das playlists já põe outra sincronização na fila.
- `[maybe-false]` `[defer]` **`TocarAsync` que lança deixa o estado velho.** É a mesma raiz do contrato do reprodutor (história 11).
- `[false]` `[reject]` **A memória muda entre ler o `EmUso` e assinar o `Mudou`, no construtor.** Os singletons nascem no arranque, antes de qualquer carregar. Não há caminho que chegue lá.
- `[low]` `[reject]` **Comandos na fila depois do `Dispose`.** Igual ao achado do `Dispose` do Blind Hunter.
- `[low]` `[patch]` **A frase "Carregue uma memória" fica depois de carregar.** Agrupado no patch da troca de memória.
- `[low]` `[patch]` **A frase da playlist anterior fica depois da troca.** Mesmo patch.
- `[medium]` `[patch]` **Duas edições se sobrepõem e o `Ocupado` é zerado cedo.** Agrupado no patch do toque duplo.
- `[medium]` `[patch]` **Adicionar de novo com o seletor aberto.** Mesmo patch.
- `[false]` `[reject]` **O seletor devolve null.** O contrato é `IReadOnlyList` não anulável, com o Nullable ligado. Desistir devolve uma lista vazia.
- `[maybe-false]` `[defer]` **Exceção do reprodutor sem frase.** Contada no grupo do contrato do reprodutor.

**Patches aplicados** (reenvio ao mesmo subagente):
- **Duração:** o `duracaoMs` só vale como número dentro da faixa do `TimeSpan`, e o `CarregarAsync` cai em 3 vazias com qualquer exceção. Teste com 5 valores.
- **Troca de memória:** limpa a frase e sai do modo Editar.
- **Toque duplo:** o `EditarPlaylist` recusa uma edição enquanto outra anda e liga o `Ocupado` antes do seletor. Dois testes.
- **Testes novos:** a faixa que volta a existir, e o fim depois de pular as últimas.

## Verification

**Commands:**
- `dotnet test app-controle/tests/BgsControle.Nucleo.Testes` e `dotnet test app-controle/tests/BgsControle.Telas.Testes` -- expected: todos passam
- `dotnet build app-controle/src/BgsControle.App -f net10.0-android -c Debug` -- expected: 0 erros e 0 avisos
