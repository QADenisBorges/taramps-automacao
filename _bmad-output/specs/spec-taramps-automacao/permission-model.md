# Modelo de permissões

Regras no estilo do Claude Code: **permitir / perguntar / negar**, configuráveis por categoria. O guardião aplica as regras no software, validando o valor de cada parâmetro, não só a ferramenta chamada. Citado por CAP-4 a CAP-11 e CAP-13.

| Categoria | Exemplos | Regra para a IA | Quem libera |
|---|---|---|---|
| Dano físico | tweeter/médio sem passa-alta · sub sem subsônico · nível acima do ceifamento (limiares em `damage-thresholds.md`) · ações de estado imprevisível: reset de áudio, carregar memória interna, gerador de sinal, modo de calibração | Negar, fixo | Só o Denis, por alerta explícito de risco, com registro |
| Perda ou bloqueio | reset de fábrica · trocar a senha BT | Negar, fixo; backup automático | Só o Denis, por alerta explícito de risco, com registro; depois do reset, a configuração do setup é refeita |
| Sobrescrever preset | gravar sobre preset salvo | Negar por padrão; com salvamento automático ligado: permitir nos presets da IA, perguntar nos demais | O Denis responde o "perguntar" |
| Restaurar backup | voltar a um backup anterior | Perguntar (a IA propõe); passa pelo guardião | O Denis confirma |
| Áreas protegidas | mapa de canais e inventário do setup confirmado · limites · senha BT | Negar, fixo | Só o Denis |
| Só soa mal | EQ · delay · fase · roteamento | Permitir (configurável) | — |

## Regras fixas

- "Sem exceção" vale para a IA: nenhuma configuração de regras permite que a IA libere dano físico ou altere áreas protegidas.
- Toda resposta humana ("perguntar", alerta, liberação) vem de um canal do software que a IA não alcança — tela ou terminal do notebook, nunca o chat.
- O humano mantém a palavra final: o Denis pode aceitar o risco de forma consciente, e o aceite fica registrado.
- Backup completo do estado antes de qualquer escrita, em todas as categorias.
- Queda de conexão ou releitura divergente: o software bloqueia novas escritas até reler o estado (CAP-12).

## Limiares das travas

As regras estão em `damage-thresholds.md`, que é a fonte única delas. Os valores saem do inventário do setup (CAP-11), por falante, com um padrão conservador quando falta dado. A aplicação ao setup de teste está em `setup-home-theater-2.1.md`.

## Dependência crítica

As travas de dano físico (`damage-thresholds.md`) dependem do mapa de canais (o que está ligado em cada saída), montado na configuração do setup (CAP-11). Se o canal do tweeter for declarado como "sub", as travas somem: o mapa de canais é a chave mestra. Por isso ele é área protegida, e só a fase 2 (microfone) consegue verificá-lo por medição.
