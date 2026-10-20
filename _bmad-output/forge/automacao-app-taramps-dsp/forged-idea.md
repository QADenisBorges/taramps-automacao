# Ideia forjada — controle do Taramps PRO 2.4BT + AUDIO pelo PC

**Resultado:** endurecida (14/10/2026). Objetivo da sessão: esclarecer.

## Decidido
- **Protótipo 1:** o notebook do Denis controla o processador **ao vivo por BLE**, sem o celular. Roda no notebook (provável Windows); esta nuvem não tem Bluetooth.
- **Etapa 1 — descoberta:** registro HCI do Bluetooth no Android, um parâmetro por vez no app, e leitura do APK se precisar. Marco: o PC muda o volume master.
- **Etapa 2 — "deu certo" (libera o protótipo 2):** o PC **lê** a configuração atual e **muda** crossover, nível por saída, atraso, fase, EQ paramétrico e EQ gráfico, e troca a memória. A leitura é obrigatória.
- **Fora do escopo:** limitador, gerador, senha, voltímetro e Audio BT.
- **Autonomia:** na descoberta, toda escrita passa pela aprovação do Denis. Na ferramenta pronta, a IA baixa nível e EQ sozinha dentro de uma faixa definida; o resto precisa de OK.
- **Sempre valem, no código e não no prompt:** passa-alta das saídas 1/2 nunca removido nem baixado; saídas 3 e 4 sempre idênticas; níveis e EQ só para baixo depois do ganho calibrado; cópia da configuração antes de cada mudança; desfazer.
- **Regra da descoberta:** só reenviar comandos vistos saindo do app, com volume baixo ou falantes desligados. Nunca enviar bytes desconhecidos às cegas.

## Rejeitado
- **Gerar arquivo de preset para importar:** não atende o controle ao vivo, e o compartilhamento de presets é criptografado.
- **Pilotar a tela do app:** frágil, e não dá o ciclo medir → ajustar do protótipo 2.
- **IA livre / aprovar cada ajuste na ferramenta pronta:** a primeira é arriscada; a segunda inviabiliza a equalização iterativa.

## Riscos em aberto
- Protocolo fechado, possivelmente criptografado ou atrelado à senha de 4 dígitos. Sem ele, o roteiro muda.
- Provavelmente só uma conexão de controle por vez: PC e app se revezam.
- Escrita interrompida deixa o aparelho em estado desconhecido. A ferramenta precisa reler o estado e restaurar a cópia.

## Depois
Protótipo 2: medição no PC com IA ajustando. O software vendável é um sonho distante, fora do escopo.
Fontes de verdade: `Sound/projetos/home-theater-2.1/montagem-2.1.md`, `Sound/componentes/processadores.md`.
