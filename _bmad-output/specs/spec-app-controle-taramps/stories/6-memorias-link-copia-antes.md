---
title: 'História 6: memórias, link de canais e cópia antes de mexer'
type: 'feature'
created: '2026-10-21'
status: 'ready-for-dev'
route: 'dispatch'
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/specs/spec-app-controle-taramps/failure-modes.md'
  - '{project-root}/_bmad-output/specs/spec-app-controle-taramps/chaves-de-acesso.md'
  - '{project-root}/protocolo/MAPA.md'
  - '{project-root}/_bmad-output/planning-artifacts/ux-designs/ux-taramps-automacao-2026-10-21/EXPERIENCE.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** O app já escreve ajustes, mas o Denis ainda depende do app da Taramps para salvar, carregar e nomear as 3 memórias (CAP-4) e para vincular os canais 1–2 e 3–4 (CAP-3). Também falta proteção quando uma operação cai no meio (regra 3 de `failure-modes.md`) e o aviso customizado ao sobrescrever uma memória (CAP-14).

**Approach:** No núcleo:
- um gerente de memórias, com salvar, carregar e nomear pela porta única e com as chaves de acesso;
- uma "cópia antes de mexer", com retomada "aplicado pela metade";
- o link de canais, que é só do app.

Em `BgsControle.Telas`, os ViewModels de Memórias e de link, testados contra o `ProcessadorSimulado`.

**Decisões já tomadas (UX de 21/10/2026):**
- Carregar uma memória não pede confirmação, mas faz a cópia antes.
- "Salvar aqui" abre a folha "Salvar sobre a MUSICA ORIG? A versão atual some.", com o nome editável e com Confirmar ou Cancelar.
- Ao reconectar depois de uma queda no meio do salvar, o app relê e diz se a memória foi salva ou não.

## Boundaries & Constraints

**Always:**
- **Trava.** Passam também os comandos abaixo, só na forma exata, com a sessão Conectada e fora do modo só leitura. Fora deles, nada muda.

  | Comando | O que faz | Chave que precisa estar ligada |
  |---|---|---|
  | `*240*n*` (n = 0 a 2) | Salva a memória | "Salvar memória" |
  | `*241*n*` | Carrega a memória | "Carregar memória" |
  | `*23N*NOME*` (N = 1 a 3) | Nomeia a memória | "Salvar memória" |

  [SUPOSIÇÃO a conferir na história 12] O formato `*23N*NOME*` e a correspondência "memória 1 = `n` 0 = nome 231".
- **Nome de memória:** de 1 a 12 caracteres, entre A–Z, 0–9, espaço e `_`. O app converte para maiúsculas. Isso segue a regra de nomes da Taramps; [SUPOSIÇÃO] ela vale também para as memórias.
- **Salvar:**
  1. Grava a cópia antes (o retrato atual).
  2. Envia o nome (`*23N*…*`) e depois o `*240*n*`.
  3. Confirma relendo o nome (`#23N#`), porque o ack não foi medido.
  4. Se a conexão cair no meio, ao reconectar o app relê o nome e diz "A memória N foi salva" ou "A memória N não foi salva".
- **Carregar:**
  1. Grava a cópia antes.
  2. Envia o `*241*n*`.
  3. Faz uma **leitura completa nova** (CAP-4).
  4. Só no fim da leitura dá a memória como "em uso".
- **Cópia antes (`CopiaAntes`).** É genérica e vai ser reusada pelos presets da história 8.
  - Guarda nos arquivos privados o retrato do estado e uma marca da operação em andamento: qual é e com que alvo. A marca sai quando a operação termina.
  - Na próxima conexão, depois da leitura completa, se a marca ainda existir, o estado vira **"aplicado pela metade"**, com duas saídas:
    - **Terminar:** refaz a operação.
    - **Voltar à cópia:** escreve, pelo `Escritor`, cada parâmetro de áudio da cópia que for diferente do estado atual. Isso precisa da chave "Ajustes de áudio".
- **Link 1–2 e 3–4.** Só do app (MAPA §6). O estado fica nos arquivos privados.
  - Ao **vincular**, os parâmetros de áudio por canal da saída mestre (1 ou 3) são escritos na outra.
  - Com o link **ligado**, cada escrita na mestre também vai para a outra pelo `Escritor`. Mexer na outra saída escreve só nela. [SUPOSIÇÃO] Esse comportamento ainda tem de ser conferido na tela da história 10.
- **`MemoriasViewModel`** (Telas):
  - mostra as 3 memórias com os nomes lidos e marca a que está em uso;
  - "Carregar" age direto;
  - "Salvar aqui" abre a folha de sobrescrever, com o nome editável;
  - "Renomear" envia só o nome;
  - a ação fica travada, com o motivo, quando a chave está desligada, em modo só leitura ou sem conexão;
  - mostra o estado "aplicado pela metade" com os dois botões.
- **`LinkViewModel`** (Telas): dois pares Lig./Desl. Ao ligar, aparece o aviso "Os ajustes da saída 1 vão para a saída 2" e só copia depois do Confirmar.
- **Testes** contra o `ProcessadorSimulado`: as memórias 240, 241 e 23N, a queda injetável e os nomes.

**Never:**
- Gerador, remote out, resets, troca de senha ou nome do produto (é a história 7).
- Presets no celular (é a história 8).
- XAML (são as histórias 10 e 11).
- Qualquer escrita fora de `Sessao.EnviarAsync`.

## I/O & Edge-Case Matrix

| Cenário | Entrada / estado | Esperado | Erro |
|---|---|---|---|
| Salvar | Salvar na memória 2 como "MUSICA ORIG 2" | Folha → Confirmar → `*232*MUSICA ORIG 2*`, `*240*1*`; o nome relido bate | — |
| Cancelar a folha | "Salvar aqui" e Cancelar | Nada sai | — |
| Carregar | Carregar a memória 3 | Cópia guardada, `*241*2*`, leitura completa nova; FILME em uso | — |
| Chave desligada | "Carregar memória" desligada | Nada sai | "Chave desligada" |
| Nome inválido | "música#1" | Recusado, nada sai | "Use até 12 letras, números, espaço ou _" |
| Queda no salvar | Cai depois do `*232*…*` | Ao reconectar e ler: "A memória 2 não foi salva" | — |
| Queda no carregar | Cai depois do `*241*2*` | Ao reconectar: "aplicado pela metade" | Terminar ou Voltar à cópia |
| Voltar à cópia | Escolha do usuário | Só os parâmetros diferentes são escritos; o estado bate com a cópia | — |
| Vincular 1–2 | Ligar e confirmar | Os parâmetros da saída 1 vão para a saída 2; o link fica guardado | — |
| Link ligado | Ganho da saída 1 = −2,0 | As duas saídas ficam em −2,0 | — |
| Trava | `*240*5*` ou `*241*0*` com a chave desligada | Recusado antes do BLE | Motivo no log |

</frozen-after-approval>

## Code Map

- `app-controle/src/BgsControle.Nucleo/Sessao/Sessao.cs` (`Trava`) e `Catalogo/Catalogo.cs` (`EscritaDeAudio`): aceitar 240, 241 e 23N.
- `app-controle/src/BgsControle.Nucleo/Escrita/Escritor.cs` e `EstadoAparelho.cs`: usar na cópia, no link e no "voltar à cópia". O portão leitura/escrita já existe.
- `app-controle/src/BgsControle.Nucleo/Leitura/LeituraAutomatica.cs`: a leitura nova depois de carregar e a retomada depois da leitura.
- `app-controle/src/BgsControle.Nucleo/Seguranca/Chaves.cs`: as chaves "Carregar memória" e "Salvar memória".
- `app-controle/src/BgsControle.Telas/` (`TelaBase`, `Comando`, `IArquivosApp`, `ControleParametroViewModel`, `ChavesViewModel`): base.
- `app-controle/tests/BgsControle.Simulador/ProcessadorSimulado.cs` e `SUPOSICOES.md`: as memórias e os nomes já simulados. Acrescentar as suposições novas.

## Tasks & Acceptance

**Execution:**
- [ ] `app-controle/src/BgsControle.Nucleo/Memorias/`: o gerente de memórias.
- [ ] `app-controle/src/BgsControle.Nucleo/Escrita/`: `CopiaAntes` e `LinkCanais`.
- [ ] `app-controle/src/BgsControle.Nucleo/Sessao/Sessao.cs` e `Catalogo`: a trava.
- [ ] `app-controle/src/BgsControle.Telas/`: `MemoriasViewModel` e `LinkViewModel`.
- [ ] `app-controle/src/BgsControle.App/MauiProgram.cs`: o DI, sem tela nova.
- [ ] Os testes: as linhas da matriz e `SUPOSICOES.md` atualizado.

**Acceptance Criteria:**
- Given o simulador com as 3 memórias, when o Denis salva na memória 2 e carrega a 3, then o simulador termina com a memória 2 igual ao estado salvo e o estado atual igual à memória 3, e o app mostra os nomes relidos.
- Given qualquer teste, when o simulador é inspecionado, then só chegaram a senha, as leituras do catálogo, as escritas de áudio e os comandos 240, 241 e 23N permitidos.

## Verification

**Commands:**
- `dotnet test app-controle/tests/BgsControle.Nucleo.Testes` e `dotnet test app-controle/tests/BgsControle.Telas.Testes`: todos passam.
- `dotnet build app-controle/src/BgsControle.App -f net10.0-android -c Debug`: 0 erros e 0 avisos.

## Implementation Notes

## Spec Change Log

## Review Triage Log
