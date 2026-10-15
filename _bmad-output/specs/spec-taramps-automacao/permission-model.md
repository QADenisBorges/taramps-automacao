# Modelo de permissões

Regras no estilo do Claude Code: **permitir / perguntar / negar**, configuráveis por categoria. O guardião aplica as regras no software, validando o valor de cada parâmetro, não só a ferramenta chamada. Citado por CAP-4 a CAP-11 e CAP-13.

| Categoria | Exemplos | Regra para a IA | Quem libera |
|---|---|---|---|
| Dano físico | tweeter/médio sem passa-alta · sub sem subsônico · nível acima do ceifamento (limiares em `damage-thresholds.md`) · ações de estado imprevisível: reset de áudio, carregar memória interna, gerador de sinal, modo de calibração | Negar, fixo | Só o Denis, por alerta explícito de risco, com registro |
| Perda ou bloqueio | reset de fábrica · trocar a senha BT | Negar, fixo; backup automático | Não definido (ver Open Questions do SPEC) |
| Sobrescrever preset | gravar sobre preset salvo | Negar por padrão; com salvamento automático ligado: permitir nos presets da IA, perguntar nos demais | O Denis responde o "perguntar" |
| Restaurar backup | voltar a um backup anterior | Perguntar (a IA propõe); passa pelo guardião | O Denis confirma |
| Áreas protegidas | mapa de canais e inventário do setup confirmado · limites · senha BT | Negar, fixo | Só o Denis |
| Só soa mal | EQ · delay · fase · roteamento | Permitir (configurável) | — |

## Regras fixas

- "Sem exceção" vale para a IA: nenhuma configuração de regras permite que a IA libere dano físico ou altere áreas protegidas.
- O humano mantém a palavra final: o Denis pode aceitar o risco de forma consciente, e o aceite fica registrado.
- Backup completo do estado antes de qualquer escrita, em todas as categorias.
- Queda de conexão ou releitura divergente: o software bloqueia novas escritas até reler o estado (CAP-12).

## Limiares das travas

Os valores saem do inventário do setup (CAP-11), por falante. Os do setup de teste estão em `setup-home-theater-2.1.md`.
- **Passa-alta (tweeter/médio):** frequência no mínimo igual ao corte mínimo da ficha do falante, na inclinação da ficha. Com inclinação menor, o corte sobe, a menos que a caixa complete a inclinação. Passa-alta desligado nunca é permitido.
- **Subsônico (sub):** caixa com duto → 0,75–0,8 × Fb, a 24 dB/oit; caixa selada → 25 Hz; Fb desconhecida → padrão conservador de 30 Hz, a 24 dB/oit.
- **Teto de nível:** limitador sempre ligado, com o limiar no ponto calibrado com multímetro (tensão AC na saída do módulo, fonte a 100 % e tom a 0 dBFS). Margem zero acima do calibrado: a IA só move limiar, nível e EQ para baixo. Sem a calibração guiada concluída (CAP-13), toda subida é negada.
- **Ponte:** saídas em ponte têm todos os ajustes idênticos.

## Dependência crítica

As travas de dano físico (`damage-thresholds.md`) dependem do mapa de canais (o que está ligado em cada saída), montado na configuração do setup (CAP-11). Se o canal do tweeter for declarado como "sub", as travas somem: o mapa de canais é a chave mestra. Por isso ele é área protegida, e só a fase 2 (microfone) consegue verificá-lo por medição.
