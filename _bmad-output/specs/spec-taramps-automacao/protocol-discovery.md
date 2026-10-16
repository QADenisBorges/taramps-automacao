# Descoberta do protocolo

Como CAP-1 é perseguido e quando parar.

## Equipamento

- Processador Taramps PRO 2.4BT + AUDIO: DSP 24-bit/48 kHz, 2 entradas, 4 saídas.
- Controle por app via BLE com senha (padrão de fábrica 1234); áudio por A2DP.
- App Android 8.1+ / iOS 17+; Android provável: `com.taramps.pro1` (não confirmado).
- O manual não documenta o protocolo. Não há trabalho público de engenharia reversa deste modelo.
- O app exporta e importa presets em arquivo e pode compartilhá-los por mensageiro. O processador guarda até 3 presets na memória interna, e o celular guarda até 15 (manual, p. 14). O arquivo exportado entra como fonte do mapa.
- O celular do Denis é Android, o que viabiliza o HCI snoop log.

## Caminho

1. **Aprender** pelo HCI snoop log do Android (o app provavelmente baixa a configuração completa ao conectar, e cada ajuste vira um comando gravado) e pela análise do APK.
2. **Atuar** com o software como central BLE no notebook, lendo e escrevendo direto no módulo.

## Método de captura

- Alterar um parâmetro por vez no app, anotando o valor antigo e o novo.
- Só depois a IA analisa a captura pronta. Analisar bytes "no escuro" queima o orçamento de tokens.

## Parada e saída

- Orçamento: cerca de 2 dias (um fim de semana) de tokens.
- Sem bytes legíveis até o fim do orçamento → procurar a Taramps.
- Protocolo criptografado, protegido ou ofuscado → procurar a Taramps (pedir documentação ou acesso) antes de forçar a quebra.

## Riscos abertos

- O protocolo pode ser criptografado ou ofuscado; ainda não testado.
- Periféricos BLE costumam aceitar uma única central: o celular fica de fora enquanto o software estiver conectado.
- As travas dependem do mapa de canais estar certo; ver `permission-model.md`.
