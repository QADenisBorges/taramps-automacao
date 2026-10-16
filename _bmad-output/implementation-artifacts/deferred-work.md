- source_spec: `_bmad-output/specs/spec-taramps-automacao/stories/1-mapa-do-protocolo-ble.md`
  summary: A heurística de polling do btsnoop_att.py (escrita idêntica repetida 5 vezes ou mais) pode tirar do cruzamento os passos de prova, se os botões +/− mandarem comandos relativos idênticos.
  evidence: Não verificado (medium). Quem resolve são as capturas C1/C2. Se o mesmo payload se repetir a cada toque de +/−, a detecção precisa olhar a periodicidade ou ignorar as escritas dentro das janelas de passo.
