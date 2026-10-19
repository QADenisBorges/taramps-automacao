# Chaves de acesso (CAP-11)

Cada operação de risco tem uma chave. Padrões confirmados pelo Denis em 19/10/2026.

| Operação | Código | Padrão | Pedido da IA exige confirmação na tela | Risco mostrado ao ligar |
|---|---|---|---|---|
| Reset de fábrica | `*004*` | **desligada** | sim | Apaga a senha e as 3 memórias. |
| Porta de comandos da IA | — | **desligada** | — | Deixa a IA comandar o som pela rede. |
| Reset de áudio | `*242*` | desligada | sim | O estado resultante é imprevisível. |
| Troca de senha | `*001*antiga*nova*` | desligada | sim | Uma senha perdida tranca o aparelho. |
| Gerador: tom fixo e sweep | `200–202`, `210–213` | desligada | sim | Um tom forte pode queimar o tweeter ou o sub. |
| Carregar memória | `*241*n*` | ligada | sim | Troca todo o som de uma vez. |
| Salvar memória | `*240*n*` + `23N` | ligada | só ao sobrescrever | Apaga a memória anterior. |
| Remote out (módulo) | `*252*0/1*` | ligada | sim | Liga ou corta o som de repente. |
| Bluetooth de áudio | `251` | ligada | não | — |
| Nome do produto | `*002*nome*` | ligada | não | — |
| Ajustes de áudio (MAPA §4) | vários | ligada | não | — |

Regras que valem para qualquer chave:

- Com a chave desligada, a operação é recusada antes de qualquer envio pelo BLE, tanto pela tela quanto pela IA.
- Ao ligar uma chave, o app mostra o aviso do risco antes de ligar.
- A IA nunca liga nem desliga uma chave. Só o usuário, pela tela.
