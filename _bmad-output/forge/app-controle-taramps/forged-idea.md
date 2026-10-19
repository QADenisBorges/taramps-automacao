# App de controle do PRO 2.4BT — ideia forjada (19/10/2026)

**O que é:** app Android, sem IA dentro, que faz tudo o que o app da Taramps faz, fala direto com o processador pelo Bluetooth e pode ser comandado por IA. Primeiro POC do caminho para o BGS Sound Lab.

## Decisões
- **Andaime:** o BGS Sound Lab será reescrito; deste POC só passa o conhecimento.
- **Do zero:** protocolo e guardião também são reescritos; o código dos protótipos é só consulta. Entram os valores atuais, as 3 memórias, os presets e as preferências.
- **Paridade total** com o app da Taramps (regular, memórias/presets, monitorar, gerador, comandos de aparelho inteiro), mais remote out (252) e BT de áudio (251).
- **Chaves de acesso:** cada operação de risco ligada ou desligada por padrão conforme a segurança (reset de fábrica desligado); o usuário decide, orientado sobre o risco. A proteção completa vem depois.
- **Telas completas** para o Denis largar o app da Taramps, com avisos customizados e ideias de interação desde o início.
- **Android no celular;** o notebook sai das configurações.
- **Memórias e presets:** o app tem presets próprios (separados dos do app da Taramps); as 3 memórias do DSP são do aparelho e os dois apps acessam.
- **IA pela porta de comandos no Wi-Fi**, com 5 travas: desligada por padrão; pareamento único por código de 6 dígitos; só rede local; tudo visível e em histórico com botão "Parar IA"; risco só com confirmação na tela.
- **Firmware ≠ R003** na conexão → modo só leitura e aviso; se mudar, nova engenharia reversa.
- **Proteção nova** provada por testes de várias formas, inclusive ao vivo com o RCA dos módulos desconectado.

## Pronto quando
1. O Denis refaz a MUSICA ORIG e salva na memória 2 só pelo celular, sem abrir o app da Taramps.
2. A IA, pela porta, lê tudo, muda um ajuste e salva uma memória, com a tela mostrando ao vivo.
3. Paridade: todo valor lido é igual ao do app da Taramps nas 3 memórias.

## Rejeitado
- Levar o protocolo e o guardião antigos como base → reescrever tudo.
- Só comandos ou tela simples → ele quer deixar o app da Taramps.
- Notebook + navegador → o celular é mais prático e combina com um kit portátil.
- USB/adb ou IA dentro do app agora → IA dentro só no BGS Sound Lab.
- Deixar os comandos perigosos de fora → viram chaves de acesso.

## Fatos que limitam
- Não existe comando para desligar o processador; o ON/OFF do app da Taramps é só o remote out (252).
- Uma conexão Bluetooth por vez: o app novo e o da Taramps não ficam conectados juntos.
- O app da Taramps só lê o firmware; não há atualização pelo Bluetooth no APK.

## Pontos fracos que sobreviveram
- Telas completas são o maior trabalho, e o app é andaime.
- Protocolo por engenharia reversa: firmware novo quebra; a Taramps pode ver como ameaça ou vitrine.
- Tudo provado num único sistema (o 2.1 do Denis).
- Grandes (DSP Concepts + LG) já levam IA de áudio às montadoras; a janela fecha.

## Rumo
POCs: 1) este app; 2) leitor de dados como os softwares profissionais, para a IA decidir; 3) aparelho 4 entradas / 4 saídas que mede a tensão de cada saída do módulo. Posição: acessível, custo-benefício, pontapé para a Taramps ou outras marcas nacionais.
