- source_spec: `_bmad-output/specs/spec-taramps-automacao/stories/1-mapa-do-protocolo-ble.md`
  summary: A heurística de polling do btsnoop_att.py (escrita idêntica repetida 5 vezes ou mais) pode tirar do cruzamento os passos de prova, se os botões +/− mandarem comandos relativos idênticos.
  evidence: Não verificado (medium). Quem resolve são as capturas C1/C2. Se o mesmo payload se repetir a cada toque de +/−, a detecção precisa olhar a periodicidade ou ignorar as escritas dentro das janelas de passo.

- source_spec: `_bmad-output/implementation-artifacts/spec-leitura-ble-marco-1.md`
  summary: O montador de respostas pode fundir um quadro sem separador final (ex.: `020#b#v`) com um quadro espontâneo (150/111), aceitar valor truncado quando um fragmento atrasa mais de 0,3 s, ou descartar como eco uma resposta sem canal igual à consulta.
  evidence: Não verificado (medium). Depende do formato e do ritmo reais das notificações; a primeira leitura com o processador (log bruto em estado/log-ble/) decide se precisa ajustar `_INICIO_QUADRO_RE`, o `OCIOSO_S` e a regra de eco.

- source_spec: `_bmad-output/implementation-artifacts/spec-escrita-ble-guardiao.md`
  summary: Os caminhos de saída 3 (sem conexão) e 5 (sem resposta à senha) do ajustar_processador.py não têm teste, e o transporte falso não simula aparelho ausente nem senha muda.
  evidence: Não verificado (medium). Esses caminhos terminam antes de qualquer escrita; um interruptor `encontrado`/`resposta_000` no falso resolve.
