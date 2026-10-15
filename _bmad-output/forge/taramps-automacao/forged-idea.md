# Ideia forjada: taramps-automacao

**Status:** FORJADA · 2026-10-15

**Problema:** a cada regulagem, o Denis tira prints de todas as telas e canais do app para a IA conferir. Ele não domina cada função e depende da IA para não danificar falantes nem o módulo.

**Equipamento:** processador Taramps PRO 2.4BT + AUDIO (DSP, 2 entradas, 4 saídas, controle BLE com senha; app Android `com.taramps.pro1`, ainda não confirmado).

## Decisões travadas
- **Produto:** uma ponte entre a IA e o processador. A IA lê e configura direto; não é automação de toques na tela.
- **Aposta:** decifrar o protocolo BLE proprietário (snoop log do Android + análise do APK) para o software atuar como central BLE.
- **Papéis:** a IA configura; o **software é o guardião** e vive dentro do servidor, não no cliente de IA, porque permissões por ferramenta não validam valores.
- **Permissões no estilo do Claude Code:** permitir / perguntar / negar, configuráveis.
  - Dano físico (tweeter/médio sem passa-alta, sub sem subsônico, nível acima do ceifamento): **a IA nunca libera**.
  - Só o Denis libera, por meio de um alerta explícito de risco, com registro.
  - A IA nunca altera o mapa de canais, os limites nem a senha BT.
  - Backup automático antes de qualquer escrita.
- **Marco 1 (leitura):** o notebook conecta com a senha e lê todos os parâmetros, entregando-os ao Claude como ferramenta. Pronto quando bate 100% com o app em 2 ou mais presets.
- **Marco 2 (escrita):** cada escrita é seguida de releitura de confirmação, e o guardião bloqueia as 3 ações de dano físico.
- **Ambiente:** o notebook do home theater é a central BLE. Durante o desenvolvimento, o processador fica desligado do amplificador.
- **Parada:** cerca de 2 dias (um fim de semana) de tokens. Sem bytes legíveis até lá, ou com protocolo protegido, **procurar a Taramps** antes de forçar.

## Rejeitado
- **IA operando o app via ADB/toques:** frágil e lento; não é "definitivo".
- **Trocar de processador:** mataria o pitch para a Taramps.
- **Prints automáticos:** a IA erra ao ler imagem, e o objetivo de segurança exige o valor exato.

## Riscos abertos
- O protocolo pode ser criptografado ou ofuscado; isso ainda não foi testado.
- Um aparelho BLE costuma aceitar uma conexão só, então o celular fica de fora enquanto o software estiver conectado.
- As travas dependem do mapa de canais estar certo; só a fase 2 consegue verificá-lo por medição.

## Método
Capturar um parâmetro alterado por vez, anotando o valor antigo e o novo, e só então a IA analisa a captura pronta.

## Futuro
Fase 2: microfone e equalização automática, além de conferir o mapa de canais. Isso dá força a um pitch para a Taramps.
