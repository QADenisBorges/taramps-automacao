# Chaves de acesso (CAP-11)

Cada operação de risco tem uma chave. Padrões confirmados pelo Denis em 19/10/2026.

| Operação | Código | Padrão | Pedido da IA exige confirmação na tela | Risco mostrado ao ligar |
|---|---|---|---|---|
| Reset de fábrica | `*004*` | **desligada** | sim | Apaga a senha e as 3 memórias. |
| IA pela rede (porta de comandos da IA) | — | **desligada** | — | Deixa a IA comandar o som pela rede. |
| Reset de áudio | `*242*` | desligada | sim | O estado resultante é imprevisível. |
| Troca de senha | `*001*antiga*nova*` | desligada | sim | Uma senha perdida tranca o aparelho. |
| Gerador: tom fixo e sweep | `200–202`, `210–213` | desligada | sim | Um tom forte pode queimar o tweeter ou o sub. |
| Aplicar memória do celular | vários (MAPA §4) | ligada | sim | Troca todo o som de uma vez. |
| Remote out (módulo) | `*252*0/1*` | ligada | sim | Liga ou corta o som de repente. |
| Bluetooth de áudio | `251` | ligada | não | — |
| Nome do produto | `*002*nome*` | ligada | não | — |
| Ajustes de áudio (MAPA §4), inclusive aplicar um preset de 15 bandas | vários | ligada | não | — |

São 10 chaves. Salvar (`240`) e renomear (`23N`) as memórias do aparelho não existem neste app: só o app da Taramps faz isso (decisão do Denis, 23/10/2026). Carregar (`241`) também não existe: as memórias do processador não aparecem no app (decisão do Denis, 29/10/2026). A trava recusa o `240`, o `241` e o `23N` em qualquer forma, antes de qualquer envio pelo BLE. A chave "Carregar memória do aparelho" saiu; o id `carregar-memoria` guardado num `chaves-de-acesso.json` antigo é ignorado ao ler, como o `salvar-memoria` da história 8c. Criar, renomear e apagar memórias e presets no celular não manda nada pelo BLE e não tem chave.

Regras que valem para qualquer chave:

- Com a chave desligada, a operação é recusada antes de qualquer envio pelo BLE, tanto pela tela quanto pela IA.
- Ao ligar uma chave, o app mostra o aviso do risco antes de ligar.
- A IA nunca liga nem desliga uma chave. Só o usuário, pela tela.
- **Sequência da IA pede o toque.** A cada 3 ajustes da IA sem um toque do usuário no meio, o próximo espera **Continuar** na tela. Um ajuste avulso continua sem confirmação. Isso fecha a brecha de a IA trocar o som inteiro ajuste por ajuste, sem passar pela confirmação de "Aplicar memória do celular" (decisão do Denis, 29/10/2026; o 3 é provisório e do Denis).
