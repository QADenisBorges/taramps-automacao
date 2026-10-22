---
title: 'Ideia: teste de playlist'
created: '2026-10-22'
status: 'pendente: forjar a ideia (bmad-forge-idea)'
origem: 'Denis, sessão da história 8b'
---

# Ideia: teste de playlist

> **Pendente:** passar pelo `/bmad-forge-idea` (forjar ideia) antes de virar spec ou história. Pedido do Denis em 22/10/2026.

## O que o Denis pediu

Uma função que avalie, por canal, a voltagem, o clip e outros sinais enquanto a playlist de uma memória toca pelo Bluetooth. A playlist às vezes não chega nem a 50% da capacidade. Com o teste, dá para melhorar a qualidade e a potência da memória de acordo com a playlist que vai tocar nela.

**Regras do pedido:**
- É uma opção de configuração da memória e da playlist.
- Durante o teste, a playlist é a **única fonte de entrada**.

## O que o aparelho permite hoje

Tudo isto vem de `protocolo/MAPA.md` e da história 7b.

| Sinal | Por canal? | Situação |
|---|---|---|
| Clip das saídas (`150`) | Sim, um bit por saída | O aparelho manda sozinho. Ainda é suposição, conferida na história 12 |
| Clip da entrada (`111`) | Não, um só | Mesmo caso |
| Voltímetro (`005`) | **Não**, um valor só | Provavelmente a tensão de alimentação. Ainda não conferido |
| Tensão de cada saída do módulo | **Não existe no protocolo** | Só com o POC 3 (medição de tensão por saída) |

## Caminhos possíveis

1. **Ler a música, sem precisar do aparelho.** O app mede o pico e a média (dBFS) de cada arquivo da playlist. Com isso calcula quanta folga sobra até a música chegar a 100% (0 dBFS), descontando os ~13 dB a menos com que o BT entra em relação ao gerador (`analise-nivel-2026-10-18.md`). Funciona com o que já existe e serve de ponto de partida.
2. **Ouvir o clip do processador.** A playlist toca e o app registra os clips de cada saída e da entrada. O resultado diz se houve clip e em qual canal, durante a playlist inteira. Isso já vale como teste de "passou ou não passou".
3. **Medir a tensão por canal.** Precisa do POC 3, ou de medir com o multímetro (VC830L).

Os três caminhos se somam e não se excluem.

## Cuidados

- A proposta de ganho vem da conta física: a folga da música, a ficha dos alto-falantes e o limiter. Quem decide o valor é o Denis. Os limites do guardião continuam provisórios.
- Qualquer mudança na memória passa pela cópia antes, pelas chaves de acesso e pela confirmação, como o resto do app.
- Ainda falta definir:
  - quanto tempo o teste dura (a playlist inteira ou trechos);
  - o volume do celular durante o teste (precisa ficar fixo e no máximo para a conta valer);
  - se a sugestão muda o ganho ou o limiter.

## Onde poderia entrar

Depois da história 8b (o player) e da 7b (a monitoração), que já dão a base. O mais natural é uma história nova depois da 12, ou junto do POC 2 (leitura de dados). Isso ainda não está decidido.
