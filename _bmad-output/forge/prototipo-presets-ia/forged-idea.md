# Ideia forjada: prototipo-presets-ia

**Status:** FORJADA · 2026-10-16 · evolução de `taramps-automacao`

**Alvo:** inauguração do 2.1 no sábado, 17/10, até as 17h (músicas, depois filme). A construção é hoje e amanhã.

**Ideia:** uma IA especialista em som, que conhece o 2.1, entende o que o Denis quer *sentir* (ele é exigente e não domina regulagem), audita o setup, calibra junto e cria os presets no PRO 2.4BT.

## Decisões travadas
- **Papel:** a IA atua só na preparação: aplica, o Denis ouve, ela melhora. Na festa ela não escreve, e os presets ficam fixos.
- **Inventário:** auditar, não refazer do zero. Cada item vai numa tela, marcado como fato com fonte (o Denis confirma), decisão do Som (a IA reavalia) ou desconhecido (mede junto ou usa o padrão seguro). Ele só vale depois que o Denis confirma. O inventário vem primeiro, a regulagem depois.
- **Calibração:** a JBL já está em 11 V. A auditoria começa pelo teste dos +2 dB na saída 1 (com tom, sem falantes). Se ficar em 11 V ou menos, o limitador é o teto. Se passar, entra a regra 6: sem subir nível, master nem reforço de EQ acima da linha de base.
- **Guardião mínimo, fixo no código, só para o 2.1.** A IA nunca:
  - baixa nem desliga o passa-alta das JBL;
  - baixa nem desliga o subsônico do sub;
  - desliga o limitador ou sobe o limiar acima dos 11 V;
  - deixa as saídas 3 e 4 diferentes;
  - manda reset, carregar memória ou gerador.

  Backup antes de cada escrita e releitura depois.
- **Memórias do DSP (3):**
  - **Música SPL HIGH:** o máximo dentro da calibração, por 15 min ou mais.
  - **Música SPL-EQ:** equilíbrio focado em SPL.
  - **Filme-EQ:** diálogo à frente, grave sem cobrir a fala.
- **Gêneros:** são uma camada do EQ de 15 bandas da entrada, por cima da SPL-EQ. Corte, limitador e níveis não mudam. Os gêneros saem amanhã.
- **Fonte:**
  - **Filme:** streaming (Netflix, Prime) no notebook, pelo P2 → RCA. O cabo P2→P2 do Edifier serve com um adaptador fêmea→2 RCA. O compressor fica no notebook: Equalização de volume do driver ou Equalizer APO com plugin, porque o PRO só tem limitador. A auditoria mede o nível do P2 a 100 % e testa zumbido.
  - **Música:** Bluetooth.
  - **Último recurso para o filme:** BT, se o atraso não incomodar num teste no streaming.
- **Formato (TDAH, autismo):**
  - Um passo por tela: faça, confira, responda com um número ou "ok".
  - Dúvida é respondida dentro do passo, voltando ao mesmo ponto. Progresso e medidas ficam salvos.
  - Nada é medido de novo sem motivo dito na hora.
  - Ideias fora de hora vão para uma lista "para depois".
- **Perfeccionismo:** a cada pedido de mais, a IA mostra o ganho esperado e avisa com ✅ quando está pronto para sábado. Quem decide é sempre o Denis.
- **Arquitetura:** um agente, uma voz, várias skills (auditoria, calibração, presets, diário) e um servidor que fala com o processador. Outros agentes, só nos bastidores. Base possível: a skill `consultor-som` e o `perfil.md` do projeto Som.
- **Diário:** as conversas ficam registradas fora da sessão (passo, pedido, resposta, onde travou), para revisar o fluxo depois.
- **Prazo:** a regulagem começa sábado às 12h, no máximo, aconteça o que acontecer.
  - Com a automação pronta, a IA escreve.
  - Sem ela, entra o **modo manual**: um ajuste por vez no app, com o guardião como checagem.
  - Se a automação ficar pronta no meio, entra dali em diante.

## Rejeitado
- **IA ajustando ao vivo na festa:** com escrita e guardião não testados, uma queda no meio do filme é o pior caso.
- **Refazer do zero o que o Som definiu:** refazer fato desperdiça a manhã. Só as decisões são reavaliadas.
- **Guardião completo, regras configuráveis e canal de liberação no sábado:** o que for bloqueado fica bloqueado. Forçar, só pelo app oficial.
- **Várias vozes ou agentes falando com o Denis:** é o mesmo problema das instruções em lote.
- **Fine-tuning como próxima chave:** o diário melhora mais reescrevendo o roteiro e a persona. RAG e MCP continuam como evolução depois do sábado.

## Riscos abertos
- O mapa BLE v1 saiu só do APK e nunca foi provado no aparelho.
- A SPL HIGH por 15 min esquenta o SD800.4 em ponte e o sub, e o guardião não mede temperatura.
- O adaptador P2→RCA ainda não foi confirmado.
