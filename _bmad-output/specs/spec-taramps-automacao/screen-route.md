# Via pela tela

Como a CAP-14 opera o app oficial no celular e o que o guardião exige nela. O mapa de telas, controles, faixas e passos do app 1.0.8 está em `../../../protocolo/capturas/demo-telas.md`.

## Como funciona

- O notebook comanda o celular Android por `adb`. O celular é a central BLE e o app oficial fica conectado ao módulo.
- **Leitura:** pela árvore de interface (`uiautomator dump`). Master, ganho por canal, delay e threshold em volts não aparecem na árvore. Esses valores são lidos por OCR determinístico do software sobre recortes de posição e fonte fixas, nunca pela visão da IA. Um valor ilegível ou fora da faixa volta como desconhecido.
- **Escrita:** por toques em +/−, seletores e campos. O passo de nível é 0,1 dB (10 toques = 1 dB), e segurar o botão repete o passo cerca de 3 vezes por segundo.

## Guardião na tela

- A IA chama as mesmas ferramentas de parâmetro e valor da via BLE. O servidor só converte em toques depois do guardião. A IA nunca recebe `adb`, coordenadas nem toques livres.
- O guardião simula o estado resultante e também cada estado intermediário da sequência de toques: cada +/−, cada opção percorrida num seletor e cada tecla, se o app aplicar por tecla. Uma violação em qualquer ponto nega a sequência inteira. Exemplo: o seletor de filtro vai de OFF a BT48 e para nas pontas, então chegar a OFF é passa-alta desligado (trava 1).
- Só são tocados controles do mapa de telas da versão validada do app. Os controles das categorias negadas para a IA nunca são tocados: "Resetar configurações de Áudio", "Resetar aos Padrões de Fábrica", Gerador de Áudio, senha em Configurações e carregar memória (ver `permission-model.md`).
- Valor desconhecido numa saída: o guardião nega qualquer subida de nível, limiar ou EQ nela.
- **Backup:** leitura completa das telas antes da primeira escrita, mais um preset salvo no celular ("Smartphone") como cópia que o app consegue restaurar. O `.trps` não é decifrado.
- **Confirmação:** releitura da tela depois de cada escrita.

## Fail-safe

Os toques param e novas escritas ficam bloqueadas até uma releitura completa quando:
- a tela não é reconhecida ou aparece um diálogo inesperado;
- o celular está bloqueado ou girado;
- o app perde a conexão com o processador;
- a versão do app difere da validada.

## Riscos

- Cada atualização do app pode mudar o layout e exige revalidar o mapa de telas.
- O celular precisa ficar desbloqueado, em retrato e sem bloqueio automático durante a sessão.
- A via é mais lenta que a BLE, porque os toques são incrementais.
- Só Android.

## Ordem

Esta via é construída primeiro, no Modo Demo e sem o processador (o Demo não gera tráfego BLE). Com o processador, ele fica sempre desligado do amplificador.
