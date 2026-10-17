---
title: 'Persona facilitadora de regulagem (skill) com progresso e diário da sessão'
type: 'feature'
created: '2026-10-16'
status: 'done'
baseline_commit: 'cefa045f2368fafbeb7b031323808f894121adef'
route: 'dispatch'
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/forge/prototipo-presets-ia/forged-idea.md'
  - '{project-root}/_bmad-output/specs/spec-taramps-automacao/setup-home-theater-2.1.md'
  - '{project-root}/protocolo/MAPA.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Amanhã (17/10), das 12h às 17h, o Denis regula o 2.1 para a inauguração. As ferramentas de ler e ajustar existem, mas falta quem conduza. Antes, ele recebia instruções vagas ou várias de uma vez, perdia o contexto ao tirar dúvidas e acabava refazendo medições. Ele tem TDAH, é autista, é exigente e não domina regulagem.

**Approach:** Uma skill do Claude Code que é a **IA facilitadora**: uma voz só, especialista em som, que conhece o 2.1. Ela conduz auditoria, calibração, memórias e gêneros **um passo por tela**, usando só `ler_processador.py` e `ajustar_processador.py`. Um script pequeno guarda o progresso, as medidas e o diário fora da sessão.

## Boundaries & Constraints

**Always:**
- **Uma tela por passo:** `Passo N de M` + **Faça** (uma ação) + **Confira** (uma coisa) + **Responda** (um número ou `ok`). Nunca dois passos de uma vez nem texto longo.
- **Dúvida no meio:** responde curto ali mesmo e reapresenta o **mesmo** passo.
- **Sem retrabalho:** antes de pedir uma medida, consulta o progresso. Medida registrada não é pedida de novo, a não ser com motivo dito na hora (ex.: "mudou o ganho do módulo").
- **Perfeccionismo:** a cada "quero mais", mostra o ganho esperado em números e diz se a diferença é pequena ou grande. Quando o preset estiver bom para sábado, avisa com ✅ "pronto para sábado, o resto é melhoria". Quem decide é sempre o Denis.
- **Ideia fora de hora:** vai para a lista "para depois", com uma linha de confirmação, e o passo continua.
- **Entender o que ele quer sentir:** traduz sensação em parâmetro, com uma tabela de vocabulário na skill (ex.: "grave no peito" ≈ 50–80 Hz; "embolado" ≈ 200–400 Hz; "voz apagada" ≈ 1–3 kHz). Confirma a tradução antes de aplicar.
- **Ajustes:** sempre `--simular` primeiro e depois aplica. Uma recusa do guardião é explicada em uma frase, com a sugestão segura que ele deu. Depois de o Denis usar o app, nada é assumido: toda execução relê o aparelho.
- **Roteiro de sábado, nesta ordem, retomável:**
  1. auditoria do inventário (fato com fonte, decisão do Som ou desconhecido; um item por tela);
  2. correção das violações lidas (BT24 nas JBL, subsônico ≥ 39 Hz, ponte igual);
  3. teste dos +2 dB e, se passar, o Denis desliga a regra 6 no `estado/guardiao.json`;
  4. medida do P2 a 100 % e teste de zumbido;
  5. memórias SPL HIGH, SPL-EQ e Filme-EQ (o Denis salva cada uma pelo app);
  6. camadas de gênero no EQ de 15 bandas (salvas no celular);
  7. compressor do filme no notebook.
- **Relógio:** a regulagem começa às 12h no máximo. Às 16h30 a facilitadora propõe congelar.
- **Diário:** cada passo grava o pedido, a resposta, se houve dúvida e se travou. O progresso guarda etapa, passo, medidas e ideias.
- **Português, curto, sem jargão sem explicação.**

**Never:**
- adb, toques no celular, scripts próprios de BLE, ou editar `estado/guardiao.json` (o Denis edita, com o passo guiado).
- Pedir, ler ou imprimir a senha. **Decidido (opção A):** a senha fica em `%USERPROFILE%\.taramps\senha`, fora do projeto, e a IA nunca abre esse arquivo. As ferramentas leem na ordem: env `TARAMPS_SENHA`, depois o arquivo, depois `getpass`. A próxima entrega (tela de login única, no painel) grava esse mesmo arquivo; até lá, o Denis cria o arquivo com um comando guiado.
- Várias vozes ou personas falando com o Denis.
- Servidor MCP, RAG ou fine-tuning (fica para depois de sábado).

</frozen-after-approval>

## Code Map

- `.claude/skills/` -- skills do projeto ficam aqui (`bmad-*` já existem). A nova skill entra em `.claude/skills/regulagem/SKILL.md`, invocada por `/regulagem`.
- `C:/Users/denis/Desktop/Som/.claude/skills/consultor-som/SKILL.md` e `references/` -- base de postura e conhecimento de som. Reaproveitar ideias, não copiar inteiro. Não alterar.
- `ferramentas/ler_processador.py` -- `_obter_senha` (env `TARAMPS_SENHA` → `getpass`). Acrescentar o arquivo `%USERPROFILE%\.taramps\senha` entre os dois. O `ajustar_processador.py` usa `lp.obter_senha`.
- `ferramentas/ajustar_processador.py` -- `--simular`, itens `CCC:c=v`, recusas com "Seguro:" e "Para desfazer:". Códigos de saída: 6 = recusado, 7 = parou no meio.
- `estado/guardiao.json` -- limites e `regra6`. Só o Denis edita.
- `estado/leituras/2026-10-16_1747.json` -- estado de referência e violações conhecidas (MAPA §9).
- `_bmad-output/specs/spec-taramps-automacao/setup-home-theater-2.1.md` -- inventário a auditar e etapas pendentes (resistências, P2, Fb).

## Tasks & Acceptance

**Execution:**
- [x] `.claude/skills/regulagem/SKILL.md` -- persona, regras de tela, roteiro de sábado (etapas e passos, com os comandos exatos das ferramentas), vocabulário sensação → parâmetro, como explicar recusas, relógio e o que nunca fazer. Menos de ~300 linhas; detalhes em `references/` se precisar.
- [x] `.claude/skills/regulagem/references/roteiro.md` -- os passos de cada etapa no formato Faça/Confira/Responda, prontos para usar.
- [x] `ferramentas/sessao.py` -- CLI só com stdlib:
  - `progresso` (mostra a etapa, o passo e as medidas);
  - `passo --etapa E --passo N --pedido … --resposta … [--duvida] [--travou]`;
  - `medida NOME VALOR UNIDADE`;
  - `ideia TEXTO`.
  
  Grava `estado/sessoes/AAAA-MM-DD.md` (diário legível) e `estado/sessoes/progresso.json`.
- [x] `ferramentas/ler_processador.py` -- senha também pelo arquivo `%USERPROFILE%\.taramps\senha` (env → arquivo → getpass), sem nunca imprimir; teste com HOME/USERPROFILE temporário.
- [x] `ferramentas/tests/test_sessao.py` -- grava e retoma o progresso, recusa medida repetida sem `--refazer MOTIVO`, gera o diário.

**Acceptance Criteria:**
- Given uma sessão nova, when o Denis chama `/regulagem`, then a IA mostra só o Passo 1 da etapa certa, no formato de tela.
- Given uma sessão interrompida, when ele chama `/regulagem` de novo, then a IA retoma do passo salvo sem repetir medidas.
- Given uma dúvida no meio de um passo, when a IA responde, then ela reapresenta o mesmo passo.
- Given um ajuste pedido, when o guardião recusa, then a IA explica em uma frase e oferece a sugestão segura.

## Implementation Notes

- Novos: `.claude/skills/regulagem/SKILL.md` (persona, tela fixa, vocabulário, roteiro resumido), `references/roteiro.md` (etapas 0 a 8 no formato Faça/Confira/Responda, com os comandos exatos), `ferramentas/sessao.py` (progresso, passo, medida, ideia, novo; gravação atômica com `.bak`) e `ferramentas/tests/test_sessao.py`.
- `ler_processador.py`: senha lida de env → `~/.taramps/senha` → getpass; arquivo inválido dá mensagem própria sem mostrar o conteúdo.
- Comandos do roteiro validados contra o guardião real com o retrato de 16/10: as três correções da etapa 2 passam; com a regra 6 ligada, nenhum reforço de EQ passa, e enquanto as violações lidas não forem corrigidas nenhum ajuste de EQ passa.
- O ensaio manual do `/regulagem` com o Denis ainda não foi feito.

## Review Triage Log

| # | Achado | Veredito | Evidência / rota |
|---|---|---|---|
| 1 | Roteiro 3.9: `--simular 080=0.0` diz "Nada a mudar" com a regra 6 ligada ou desligada (blind, edge, gap) | high | Master já é 0,0; é um no-op. Confirmação falsa de que a trava foi desligada. Patch: `081:0=-9.0`. |
| 2 | Roteiro 0.1: `Read-Host` mostra a senha na tela; Confira só testa se o arquivo existe (blind, edge) | medium | PowerShell 5.1 ecoa. Patch: `-AsSecureString` + conferir 4 caracteres. |
| 3 | SKILL diz "regra N" para as travas 1–3, mas o guardião rotula `trava N` (blind) | medium | `ROTULOS` em guardiao.py. A IA procuraria a regra errada. Patch. |
| 4 | Tabela de ferramentas: "três" com 4 linhas; faltam `Nada a mudar`, `Já estava assim`, `Espelhado` e os códigos 1–5 (blind, edge) | medium | Etapa 2.1 cai em "já estava assim" (saída 2 em 120 Hz). Patch. |
| 5 | Medidas sem nome (fusível, notas, "pular") e ramo indefinido no 3.9 sem número (blind, edge) | medium | Quebra o "nada medido duas vezes" na retomada. Patch. |
| 6 | Congelar sem número de etapa; Etapa 6.1 sem `Confira`; "BW12" (blind, edge) | low | Correções diretas de texto. Patch. |
| 7 | `progresso` deixa o alvo da retomada ambíguo e não imprime a hora (blind) | medium | M por etapa é fixo; o relógio 12h/16h30 depende da hora. Patch. |
| 8 | Sem caminho para recomeçar a sessão (blind) | medium | Após ensaio ou erro, só edição manual. Patch: `novo` com arquivo. |
| 9 | `gravar` não é atômico; arquivo truncado trava todos os comandos (blind, edge) | medium | `write_text` direto. Patch: temp + `os.replace` + `.bak`. |
| 10 | `passo` nulo → TypeError; tipos errados → traceback; `nan`/`inf` no JSON; argparse sai com 2 (colide com repetida); `--travou` em passo feito (edge) | low | Correções diretas. Patch. |
| 11 | Senha inválida no arquivo: mensagem genérica, e `UnicodeDecodeError` sem guarda (blind, edge) | low | Correção direta. Patch. |
| 12 | `formato` desconhecido sem teste (gap) | medium | Confirmado por mutação. Patch (teste). |
| 13 | README/.gitignore não citam `estado/sessoes/` e `sessao.py` (blind) | low | Diário é histórico versionado, como os ajustes. Rejeitado; README fica para o painel. |
| 14 | Nome de medida vazio, unidade "-", quebra de linha, OSError no gravar, `--refazer` sem medida, etapa negativa (edge) | low | Raros no uso guiado; adicionam ramos. Rejeitados. |
| 15 | `TARAMPS_SENHA` só com espaços pula o arquivo (edge) | low | Caso artificial. Rejeitado. |

## Verification

**Commands:**
- `uv run python -m unittest discover -s ferramentas/tests` -- expected: tudo passa.

**Manual checks (if no CLI):**
- Ensaio com o Denis: `/regulagem` mostra o Passo 1 da auditoria em uma tela; uma dúvida volta ao mesmo passo; o diário registra.
