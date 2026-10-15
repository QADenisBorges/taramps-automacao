# Stack

Direção técnica para a arquitetura. Não é mecanismo de segurança: as travas são sempre do guardião (ver `permission-model.md`).

- **MCP** (confirmado pelo Denis): o software expõe leitura, escrita e as demais operações ao Claude como servidor de ferramentas no notebook. O guardião roda dentro desse servidor.
- **RAG** (direção do Denis, a avaliar): base de conhecimento sobre as funções do processador (manual, conceitos de DSP) para a IA explicar e decidir ajustes — atende à dor de o Denis não dominar cada função.
- **Fine-tuning** (direção do Denis, a avaliar): ajuste do modelo para lidar com as situações de regulagem. Não substitui o guardião: obedecer limites é garantido pelo software, não pelo modelo.
