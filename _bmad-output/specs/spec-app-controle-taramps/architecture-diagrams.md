# Diagramas

## Quem fala com quem

```
 Notebook (Claude Code) ──Wi-Fi da casa, porta de comandos──►  CELULAR (o app)  ──BLE──►  PRO 2.4BT + AUDIO ──RCA──► módulo ──► JBL L, JBL R, sub
                                                                    ▲
                                                       Denis (telas) ┘

 App da Taramps: só quando o app novo está desconectado (uma conexão BLE por vez).
```

## Caminho de um comando

```
 pedido (tela ou IA) → chave de acesso ligada? ─não→ recusa
                                     │sim
                     operação de risco vinda da IA? ─sim→ espera o toque na tela
                                     │
                         envia pelo BLE → relê → atualiza a tela e o histórico
```
