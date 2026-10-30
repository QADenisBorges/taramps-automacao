---
name: BGS Sound
status: final
updated: 2026-10-30
sources:
  - ../../../specs/spec-app-controle-taramps/SPEC.md
  - ../../sprint-change-proposal-2026-10-29.md
  - ../../../forge/montagem-do-setup
  - DESIGN.md
---

# BGS Sound — Experience Spine

> **Referências visuais** (escolhidas pelo Denis em 30/10/2026):
> - [mockups/prototipo.html](mockups/prototipo.html) — protótipo navegável (abertura → busca → conexão → Roda), anterior a 30/10: os traços da IA e o Aplicando antigo não valem.
> - [mockups/criacao-memoria-ia.html](mockups/criacao-memoria-ia.html) — **criar a memória com a IA** (rodada de 30/10): do "+ Nova memória" ao aviso de memória criada, com o palco, a passagem, o Aplicando novo, o refino e as Configurações do app. É a referência mais atual para o Aplicando. Perguntas, frases e valores são de exemplo.
> - [mockups/logo-final.html](mockups/logo-final.html) — logo, ícone e barra do topo.
> - [mockups/abertura-escolhida.html](mockups/abertura-escolhida.html) — abertura.
> - [mockups/conexao-radar.html](mockups/conexao-radar.html) — só as **telas 2 e 3** (procurar e listar; conectando).
> - [mockups/tela-inicial-roda-estados.html](mockups/tela-inicial-roda-estados.html) — a Roda nos estados A a F, anterior a 30/10: os traços da IA e o Aplicando antigo não valem.
> - [.working/spinner-opcoes-1.html](.working/spinner-opcoes-1.html) — histórico: só vale a **opção 8, Anel duplo**.
> - [.working/tela-inicial-opcoes-1.html](.working/tela-inicial-opcoes-1.html) — histórico: só vale a **opção 2, Roda**.
> - [reconcile-referencia-esfera-neon.md](reconcile-referencia-esfera-neon.md) — o que ficou da imagem de referência da esfera de neon.
>
> **Em conflito com qualquer mock, as spines vencem.**

O redesenho muda o visual e a organização. O app continua fazendo o que a SPEC define (CAP-1 a CAP-17), com as mudanças de 30/10 que ainda precisam de correct-course (ver Questões em aberto, item 1). Telas, navegação e fluxos foram repensados do zero. Os IDs CAP-n seguem a SPEC.

Decisões marcadas *(delegada)* foram recomendadas pelo facilitador e aceitas porque o Denis delegou: "o resto siga escolhendo recomendado". Decisões marcadas *(Denis, recomendado)* foram recomendadas pelo facilitador e escolhidas pelo Denis.

## Foundation

- **Plataforma:** só celular Android, em retrato. Um usuário só, o Denis.
- **Sistema de UI:** nenhum foi escolhido. A spine não herda de Material/Compose nem de outra biblioteca. Onde ela não diz nada, valem as convenções do Android (voltar, TalkBack, movimento reduzido).
- **Visual:** `DESIGN.md` é a referência. Tema só escuro *(delegada)*.
- **Onde o app é usado** (Denis): no sofá, ouvindo e criando o som; e na sala escura, com o computador no projetor, vendo filme.
- **Usos principais, nesta ordem:** 1º trocar o som (escolher a memória do momento e ouvir); 2º **criar a memória respondendo a perguntas**, uma por tela, ouvir e aprovar. Ajuste manual e Medir não são usos principais e ficam fora da aba principal.
- **A IA não está dentro do app e não aparece nele** *(Denis, 30/10)*. É o Claude Code no notebook, que fala com o app pela porta de comandos, no Wi-Fi da casa (CAP-12). A porta e o pareamento continuam por baixo. Na interface não há a palavra "IA", estado da IA, PARAR IA nem Pausar. O histórico das mudanças fica fora do app.
- **Criar o som é imersivo** *(Denis, 30/10)*: cada ajuste aparece na tela, um por vez, como uma frase com o valor embaixo. A animação tem prioridade sobre a velocidade da operação real.

## Information Architecture

**Navegação** *(Denis, 30/10)*: 3 abas fixas embaixo, **Som · Ajustes · Setup**. A aba IA saiu. A barra do topo tem a logo de uma linha, o chip do Setup em uso ("Casa 2.1 ›") e a **engrenagem**, que leva a **Aparelho** e às **Configurações do app**. Folhas sobem por cima da aba atual. Aplicar uma memória cobre a tela toda. A criação da memória é um fluxo em tela toda, sem abas.

| Tela | Para que serve | O que mostra | Como se chega | CAP |
|---|---|---|---|---|
| **Abertura** | Marcar a entrada no app | A animação Neon + Glitch B até a logo parada ({motion.opening-total}) | Toda abertura a frio; não toca ao voltar do segundo plano *(delegada)* | — |
| **Busca / Conexão** | Achar o processador e conectar | Tela 2: "Procurando aparelhos", anéis saindo da logo, pontos e a lista de encontrados. Tela 3: anel duplo, "Conectando ao" + nome e Cancelar | Depois da Abertura; "Procurar aparelhos" (conexão perdida) | CAP-1, CAP-2, CAP-16 |
| **Som (Roda)** | Trocar o som | A Roda com as memórias do Setup em uso. No centro, o nome da memória e, logo abaixo, o preset em uso ("Ação ›") *(Denis)*. Embaixo: mini player e o chip "+ Nova memória" | Aba Som; destino depois da conexão | CAP-4, CAP-5, CAP-15 |
| **Nova memória (perguntas)** | Dizer como quer o som | **Uma pergunta por tela**, com 3 ou 4 respostas e "Outro…". Barra do fluxo: ‹, "Nova memória / Casa 2.1", ✕ *(Denis, 30/10)* | Chip "+ Nova memória" | CAP-4, CAP-12 |
| ↳ **Palco da criação** | Ver o som sendo montado | Cada ajuste como frase com verbo no gerúndio e o valor numa linha, acima do horizonte, com o anel grande e apagado no chão ([mock](mockups/criacao-memoria-ia.html)) | Sozinho, depois da última resposta | CAP-3, CAP-4 |
| ↳ **Aplicando** | Gravar e reler no aparelho | APLICANDO, anel e nome embaixo; o esquema de ligação do Setup no alto, com os canais acendendo | Sozinho, depois da última frase | CAP-3, CAP-4 |
| ↳ **Resumo** | Saber o que mudou e por quê | "Pronto. Ouça um pouco.", a lista com ✓ (ajuste, valor, motivo) e "Avaliar o som" | Sozinho, depois do Aplicando | CAP-4 |
| ↳ **Como ficou?** | Aprovar ou pedir refino | Pergunta com respostas e "Outro…" | "Avaliar o som" | CAP-4 |
| ↳ **Salvar a memória** | Guardar a memória | "Salvar a MUSICA ORIG no Casa 2.1?", Salvar e "Ajustar mais" | "Ótimo, pode salvar" | CAP-4, CAP-14 |
| **Memórias** | Cuidar das memórias | As memórias do celular (renomear, apagar) e uma seção à parte, **"No aparelho (app da Taramps)"**, com as 3 memórias internas e as notas herdadas, sem tom de recomendação *(delegada)* | **Segurar o centro da Roda** | CAP-4, CAP-6 |
| **Player** | Ouvir a memória com a playlist dela | A playlist da memória em uso, a faixa atual, tocar/pausar/pular e a edição da playlist | Toque no mini player | CAP-15 |
| **Presets** | Trocar o "gosto" (15 bandas) | A lista de presets. Um toque aplica | Toque no preset do centro da Roda | CAP-5 |
| **Ajustes** | Regular à mão | As saídas do Setup (nome do equipamento e cor do canal; um par com link conta como uma saída), os globais (EQ de entrada, rota, MASTER), as 15 bandas do preset numa seção própria e o **Salvar** fixo no pé *(delegada)* | Aba Ajustes | CAP-3, CAP-4, CAP-5 |
| ↳ **Editor de saída** | Ajustar uma saída | Ganho, limitador, atraso, fase, corte e EQ de saída | Toque numa saída | CAP-3 |
| ↳ **Medir** | Ver voltímetro e clip ao vivo | A monitoração por saída e da entrada | Área Medir, em Ajustes | CAP-7 |
| ↳ **Gerador** | Tocar tom fixo ou sweep | Frequência, ganho, tocar/parar e contagem de 30 s; atrás da chave do gerador | Dentro de Medir | CAP-8, CAP-11 |
| **Setup** | Montar e ver a instalação | O Setup em uso no esquema isométrico, os equipamentos com fichas e selo de origem, a lista de Setups e "criar Setup" *(delegada)* | Aba Setup | CAP-6, CAP-16, CAP-17 |
| **Aparelho** | Comandos do aparelho inteiro | Chaves de acesso, módulo (remote out), Bluetooth de áudio, firmware, nome do produto, senha e resets *(delegada)* | Engrenagem | CAP-1, CAP-9, CAP-10, CAP-11 |
| **Configurações do app** (folha) | Preferências do próprio app | A chave **"Animação ao criar o som"** (ligada por padrão) e "Fechar" *(facilitador; aprovado no protótipo 3)* | Engrenagem | — |
| **Folha da dúvida** | Decidir o Setup com o som mudo | O que não bate e as 3 escolhas | Sozinha ao conectar; toque no chip do Setup | CAP-16 |

Memórias, Player, Presets, Ajustes (com editor, Medir e Gerador), Setup e Aparelho ainda não têm mock. Serão construídas só a partir desta spine. A regulagem à mão da memória tem 6 formas renderizadas em [.working/regular-memoria-opcoes-1.html](.working/regular-memoria-opcoes-1.html), sem escolha. Ver Questões em aberto.

## Voice and Tone

Microcopy *(delegada)*: pt-BR, frases curtas e diretas, botões começando pelo verbo. **Nunca afirmar o que o app não sabe.** **Nunca falar da IA** *(Denis, 30/10)*. Números com vírgula decimal e unidade (−1,5 dB, 2,1 ms). A voz da marca está em `DESIGN.md › Brand & Style`.

| Fazer | Não fazer |
|---|---|
| "Conectando ao PRO 2.4BT" (com o nome, sem números) | "Lendo 37 de 84…" |
| "Som mudo até você escolher" | "Erro de configuração detectado" |
| "O aparelho segue tocando o que já tinha" | Mostrar a última memória como se fosse o som atual, sem conexão |
| "Escolher um Setup" · "Criar um Setup novo" · "Seguir sem Setup" | "OK" / "Continuar?" genéricos |
| Dado de ficha sem origem: deixar em branco | Marcar "verificado" sem origem |
| Notas das memórias do aparelho como notas | "Recomendado" numa memória do aparelho |
| Preset só pelo nome: "Ação ›" | "Preset: Ação ›" |
| "Definindo o corte das JBL" (a ação, no gerúndio) | "A IA está ajustando o corte" |
| Linha do valor só com os valores: "desligado → 80 Hz" | "HPF em todas: desligado → 80 Hz" |
| "Pronto. Ouça um pouco." | "A IA terminou" |

**Textos já decididos** (dos mocks escolhidos e das fontes):

| Lugar | Texto |
|---|---|
| Busca | "Procurando aparelhos" · "Deixe o processador ligado e o Bluetooth do celular ativo." · "2 encontrados · toque para conectar" · "Conectar ›" · "Taramps · Casa 2.1 da última vez" · "Não é um processador" |
| Conectando | "Conectando ao" + nome do aparelho · "Cancelar" |
| Roda | "Gire para trocar o som" · "tocando · aplicada agora" · "toque para aplicar" · "Tocando: FILME" · "+ Nova memória" |
| Preset no centro | "Ação ›" (só o nome) · sem preset: "Sem preset ›" · memória escolhida e não aplicada: o último preset dela, sem "›" |
| Aplicando | "APLICANDO" em cima do anel e o nome da memória embaixo · ao reaplicar: "REAPLICANDO" · nome do preset e nota "Mantenha o celular perto do processador" (ver Questões em aberto, nº 16) |
| Dúvida | Chip "Setup em dúvida" · centro "MUDO / até você escolher" · "Som mudo até você escolher" · "O aparelho não bate com a FILME do Casa 2.1:" · cada diferença no formato "Atraso do Sub: 80 cm na FILME, 0 cm no aparelho" (P29 §5.5). A legenda "A IA fica bloqueada até você escolher" saiu (30/10) |
| Nova memória | Barra "Nova memória / Casa 2.1" · "Outro…" · campo "Escreva do seu jeito" + "OK" · perguntas de exemplo: "Para que é esta memória?", "Em que volume você costuma ouvir?", "Onde você senta?", "Como você prefere o grave?" |
| Cancelar a criação | "Cancelar a nova memória?" · "Nada é salvo e o aparelho volta para a FILME, como estava antes." · "Cancelar e voltar" · "Continuar criando" |
| Palco (frases) | Verbo no gerúndio + o que muda, com a palavra-chave em destaque: "Definindo o corte das **JBL**" · "Encaixando o **Sub** nas JBL" · "Alinhando o tempo das **caixas**" · "Ajustando o **grave**" · "Protegendo os **canais**". No refino: "Reforçando o **grave**" · "Devolvendo o **detalhe**" · "Suavizando o **grave**" · "Dando mais **folga**" (a escolha das palavras é do facilitador, pela coesão do texto) |
| Palco (valor) | Só os valores: "desligado → 80 Hz" · "150 Hz → 80 Hz" · "+6 dB → +7,5 dB" |
| Resumo | "Pronto. Ouça um pouco." · "5 ajustes no aparelho, todos relidos." · no refino: "Mudou 1 ajuste, em destaque. O resto ficou igual." · "Avaliar o som" |
| Como ficou? | Barra "MUSICA ORIG / tocando agora" · "Como ficou?" · respostas de exemplo: "Ótimo, pode salvar" · "Quero mais impacto" · "Perdeu detalhe" · "Não gostei" · "Outro…" |
| Salvar | "Salvar a MUSICA ORIG no Casa 2.1?" · "Guarda toda a regulagem que está tocando agora." · "Salvar" · "Ajustar mais" |
| Configurações do app | "Configurações do app" · "Animação ao criar o som" · "Mostra cada ajuste na tela, um por vez, enquanto a memória é montada. Desligada, aparece só o 'Aplicando'." · "Fechar" |
| Sem conexão | Chip "Sem conexão" · "sem conexão com o PRO 2.4BT" · "O aparelho segue tocando o que já tinha" · "Procurar aparelhos" |
| Aplicado pela metade | "Terminar de aplicar" · "Voltar à cópia" |
| Gerador sem conexão | "DESLIGUE O MÓDULO AGORA" |
| Memórias | "No aparelho (app da Taramps)" · "Memória N do aparelho (app da Taramps)" |
| Aviso (CAP-14) | "Memória MUSICA ORIG criada no Casa 2.1." |
| Link | "CH3 e CH4 ligados: alimentam o mesmo bridge" (P29 §5.5) |
| Riscos das chaves (CHA) | Gerador: "Um tom forte pode queimar o tweeter ou o sub" · Remote out: "Liga ou corta o som de repente" · Reset de fábrica: "Apaga a senha e as 3 memórias" · Reset de áudio: "O estado resultante é imprevisível" · Senha: "Uma senha perdida tranca o aparelho" |

O botão se chama só **"Salvar"**. "Salvar por camadas" era o nome do modelo antigo (A13). A divisão entre memória e preset acontece por dentro.

## Component Patterns

Aqui fica o comportamento. O visual está em `DESIGN.md › Components`.

| Componente | Onde | Regras de comportamento |
|---|---|---|
| **Barra do topo** | Global, menos Abertura, criação da memória e Aplicando | Logo de uma linha, chip do Setup e engrenagem. A engrenagem abre Aparelho e as Configurações do app. [ASSUMPTION] A logo não é botão. [ASSUMPTION] Como a engrenagem oferece as duas coisas (menu, folha ou tela) não foi desenhado; o mock abre direto a folha das Configurações do app. |
| **Roda** | Som | Mostra só as memórias do celular do Setup em uso. As memórias do aparelho nunca entram nela *(delegada)*. **Girar só escolhe; o som não muda.** A memória que para no topo, sob o arco, é a escolhida. Sem toque no centro, a Roda volta sozinha para a memória que está tocando depois de {motion.roda-auto-return} *(delegada)*. Fica travada (apagada, com cadeado) na dúvida, sem conexão, sem Setup e no só leitura. |
| **Botão do centro** | Som | Aparece quando a escolhida é diferente da que está tocando ("toque para aplicar"). **Um toque aplica** a escolhida e o último preset dela, com cópia antes e releitura (CAP-4, FM 3). **Segurar** o centro abre Memórias *(delegada)*. |
| **Preset no centro** | Som | Fica no centro da Roda, abaixo do nome da memória, em todas as memórias *(Denis)*. Mostra só o nome do preset ("Ação ›"; sem preset, "Sem preset ›") *(Denis)*. Tem área de toque própria de 160 × 48 px, separada do giro e do toque no centro *(Denis)*. Um toque abre a lista de presets. Um toque num preset da lista **aplica na hora, sem confirmação** (com cópia antes; muda só as 15 bandas) *(delegada; CAP-5)*. A memória em uso passa a lembrar esse preset como o último dela, sem Salvar *(Denis; CAP-4)*. Com outra memória escolhida e ainda não aplicada, mostra o último preset dela, sem "›"; o toque no centro aplica memória e preset juntos. |
| **Aplicando** | Global | Entra no toque do centro, em "carregar memória do aparelho", na reaplicação sozinha ao entrar no app ("REAPLICANDO") e no fim da criação. Cobre o app até o aparelho confirmar pela releitura. **Layout novo** *(Denis, 30/10)*: embaixo, "APLICANDO" em evidência, o anel duplo e o nome da memória; no alto, o esquema de ligação do Setup, com cada canal acendendo enquanto é gravado e relido e ficando com luz fraca depois. Não tem Cancelar nem ✕. Ao terminar, a camada some e a Roda dá um brilho rápido e mostra a nova memória com "tocando · aplicada agora"; na criação, entra o resumo. [ASSUMPTION] O esquema também vale fora da criação (toque no centro, reaplicar), já que o Aplicando é o padrão do app; o mock só mostra a criação (ver Questões em aberto, nº 16). |
| **Esquema de ligação no Aplicando** | Aplicando | Desenhado com os dados que o Setup já tem: o PRO 2.4BT com CH1 a CH4, os fios e os equipamentos. Serve para qualquer Setup (casa, carro, 5.1) *(Denis, 30/10)*. Os canais acendem na ordem CH1 a CH4, cada um enquanto é gravado e relido (0,7 s na animação). Um equipamento com dois canais fica aceso nos dois. Não é tocável. |
| **Spinner Anel duplo** | Global | É o indicador de **todo** carregamento e nunca mostra números. Grande ao conectar e aplicando; pequeno (18 px) no botão que disparou a ação ("Salvando"); grande e apagado no chão do palco da criação. Todo carregamento termina com a transição suave de `DESIGN.md › Movimento` *(Denis)*. |
| **Anéis da busca + lista** | Busca | Os anéis seguem saindo enquanto procura, **mesmo depois do primeiro aparelho**. Cada aparelho encontrado vira um ponto e um cartão, e a lista cresce. **Nada conecta sozinho**, nem o aparelho já conhecido *(Denis)*. Outros Bluetooth ficam apagados e não conectam. |
| **Cartão de aparelho** | Busca | Um toque em "Conectar ›" troca os anéis pelo anel duplo com o nome do aparelho. "Cancelar" interrompe a conexão. [ASSUMPTION] Cancelar volta para a Busca. |
| **Chip do Setup** | Barra do topo | Mostra o Setup em uso, "Setup em dúvida" ou "Sem conexão". [ASSUMPTION] É o "nome do Setup" da CAP-16: um toque volta à escolha de Setup (folha da dúvida, com as 3 escolhas). |
| **Mini player** | Som | O botão toca e pausa. Um toque no bloco abre o Player. Mostra a playlist da memória em uso ("Playlist da FILME · 2 de 8"). Segue tocando se o BLE cair (CAP-15). |
| **+ Nova memória** | Som, chip embaixo da Roda | Abre a criação da memória com perguntas, no Setup em uso *(Denis, 30/10)*. Fica fora da Roda para que um giro nunca caia em "criar" *(Denis aceitou a recomendação)*. Travado sem Setup. [ASSUMPTION] Quando o nome da memória é pedido não foi desenhado (o mock já usa "MUSICA ORIG"; ver Questões em aberto, nº 12). |
| **Barra do fluxo** | Criação da memória | ‹ volta à pergunta anterior, sem perder as respostas; só aparece da 2ª pergunta em diante. ✕ abre a folha "Cancelar a nova memória?". O ✕ fica nas perguntas e no "Como ficou?" e **some no palco e no Aplicando** *(Denis, recomendado)*. |
| **Pergunta** | Criação da memória | **Uma pergunta por tela** *(Denis, 30/10)*. **Tocar numa resposta já responde e avança**: a resposta acende e a próxima pergunta entra (sem botão "Próximo"). Responder à última pergunta **autoriza aplicar** e leva ao palco *(Denis, recomendado)*. A resposta dada fica marcada ao voltar com ‹. |
| **Outro…** | Pergunta e "Como ficou?" | Vira um campo ("Escreva do seu jeito") com **OK** ao lado. OK ou Enter responde e avança; campo vazio não avança. A resposta escrita aparece no lugar de "Outro…" ao voltar. |
| **Palco da criação** | Criação da memória | Mostra **uma ação por vez**, como frase com verbo no gerúndio e o valor numa linha (antes riscado → novo, só os valores) *(Denis, 30/10)*. A escrita real roda junto e cada ajuste é relido; **a animação tem prioridade**: nenhuma frase sai antes do tempo dela (1,3 s parada), mesmo que o aparelho responda rápido *(Denis, 30/10)*. Não tem ‹ nem ✕. Depois da última frase, o anel do chão faz a passagem até o Aplicando. Nada na tela responde ao toque. |
| **Resumo** | Criação da memória | Lista cada ajuste com ✓, o valor e o motivo. No refino, mostra todos e destaca só o que mudou. "Avaliar o som" leva ao "Como ficou?". O som já está tocando com os ajustes; **nada foi salvo**. |
| **Como ficou?** | Criação da memória | "Ótimo, pode salvar" leva ao Salvar. Qualquer outra resposta (ou "Outro…") **refina**: volta ao palco só com o que muda, depois ao Aplicando e ao resumo. O ✕ cancela e volta ao som de antes, sem salvar. |
| **Salvar a memória** | Criação da memória | "Salvar" guarda toda a regulagem que está tocando como a nova memória do Setup em uso; volta à Roda com ela tocando e o aviso "Memória <nome> criada no <Setup>." "Ajustar mais" volta ao "Como ficou?". **Nada é salvo sem esse toque** *(Denis, recomendado)*. |
| **Configurações do app** | Folha, pela engrenagem | A chave **"Animação ao criar o som"** vem ligada. Desligada, a criação pula as frases e a passagem e mostra só o Aplicando pelo tempo real da operação, e depois o resumo *(facilitador; aprovado no protótipo 3)*. |
| **Folha** | Global | Sobe do pé por cima da aba atual. Usada em: dúvida do Setup, confirmação de risco, cancelar a criação, aplicado pela metade, a pergunta do preset no Salvar dos Ajustes, confirmação de carregar memória do aparelho e Configurações do app. O Salvar da criação é uma tela, não uma folha. A folha da dúvida não fecha sem uma escolha. [ASSUMPTION] As outras fecham com voltar ou arrastando para baixo. |
| **Confirmação de risco** | Folha, global | Folha **do próprio app, sem mencionar a IA**: diz o que muda e o risco, com **Confirmar** e **Cancelar** *(Denis, recomendado)*. Na criação, a animação **pausa** até a resposta. Mantém a regra da spec de confirmar na tela do celular. [ASSUMPTION] O que Cancelar faz na folha de risco (voltar ao som de antes ou só pular esse ajuste) não foi desenhado (ver Questões em aberto, nº 13). |
| **Aviso rápido** | Global | Aparece no pé por 2,6 s e some sozinho. Ex.: "Memória MUSICA ORIG criada no Casa 2.1." (CAP-14). |
| **Salvar** | Ajustes, fixo no pé | **Um passo à parte, com um botão só.** Reparte por camada: a regulagem vai para a memória em uso e as 15 bandas para o preset. Se as 15 bandas mudaram, a folha pergunta: atualizar o preset em uso (vale para toda memória que o usa) ou criar um novo (CAP-4, CAP-5). Fora a troca de preset pela Roda e o Salvar da criação, nada é salvo sem um toque em Salvar. **A IA nunca salva.** |
| **Saída (linha)** | Ajustes | Mostra o nome do equipamento alimentado e a cor do canal (sem equipamento: CH1 a CH4). Um par com link é uma saída e escreve nos dois canais. O link não é controle: aparece só como a linha "CH3 e CH4 ligados: alimentam o mesmo bridge". |
| **Controle de parâmetro** | Editor de saída, globais, 15 bandas | Cada mudança é escrita ao vivo e relida. A tela mostra o valor **relido**. Releitura diferente do pedido é erro (CAP-3). |
| **Chave de acesso** | Aparelho, Gerador | Ao ligar, o aviso do risco aparece **antes** (texto da CHA). Com a chave desligada, o pedido é recusado antes de qualquer envio, pela tela e pela porta. A IA nunca liga nem desliga chave (CAP-11). |
| **Gerador** | Medir | Fica atrás da chave, desligada por padrão. Toca no máximo 30 s, com contagem. Ao fechar a tela, perder a conexão ou dar erro, manda desligar. A releitura confirma o desligado (CAP-8, FM 1). |
| **Medidores** | Medir | Voltímetro por saída e da entrada; clip no canal certo (CAP-7). O voltímetro pode responder "ERRO 0"; a tela não inventa um valor no lugar. |
| **Esquema isométrico** | Setup | Arrastar gira, pinçar aproxima e um toque num equipamento abre a ficha. Atualiza sozinho quando um equipamento entra, muda ou sai (CAP-17). |
| **Ficha / selo de origem** | Setup | Cada dado mostra a origem: oficial (com endereço), outra fonte, medido ou digitado. Dado sem origem confirmada nunca aparece como verificado. O desconhecido fica em branco. Um modelo da base completa a ficha sozinho (CAP-16). |
| **Abas** | Global | Som, Ajustes e Setup. Trocam de tela sem perder o estado da Roda. Visíveis em tudo, menos na Abertura, na Busca/Conexão, na criação da memória e no Aplicando. [ASSUMPTION] Na Busca, os mocks não mostram as abas. |

## State Patterns

Os estados A a F da Roda estão em [mockups/tela-inicial-roda-estados.html](mockups/tela-inicial-roda-estados.html). Busca e conexão estão em [mockups/conexao-radar.html](mockups/conexao-radar.html), telas 2 e 3. A criação da memória está em [mockups/criacao-memoria-ia.html](mockups/criacao-memoria-ia.html).

### Conexão

| Estado | Tela | Tratamento |
|---|---|---|
| Bluetooth do celular desligado / sem permissão | Busca | O app pede para ligar o BT e dar as permissões. [ASSUMPTION] Texto e visual não foram desenhados. |
| Procurando | Busca | Os anéis saem da logo. Dica: "Deixe o processador ligado e o Bluetooth do celular ativo." |
| Encontrados | Busca | Pontos nos anéis e cartões na lista; "N encontrados · toque para conectar". A busca continua. |
| Nenhum encontrado | Busca | [ASSUMPTION] Os anéis continuam. Não há estado de "desistir" decidido. |
| BLE ocupado (app da Taramps conectado) | Busca/Conectando | Só uma conexão BLE por vez. [ASSUMPTION] A mensagem não foi desenhada. |
| Conectando / autenticando / lendo | Conectando | Anel duplo e "Conectando ao <nome>" até a leitura completa (~13 s). Sem contador. Na primeira vez, a senha é pedida e guardada; nunca aparece na tela (CAP-1). Ao terminar, a transição do fim do carregamento leva à aba Som. [ASSUMPTION] O pedido de senha não foi desenhado. |
| Senha errada | Conectando | A conexão é recusada sem nenhuma escrita (CAP-1). [ASSUMPTION] O texto não foi desenhado. |
| Firmware ≠ R003 → **só leitura** | Global | Uma faixa de aviso aparece. Nada é escrito: Roda, Salvar, controles, chaves e "+ Nova memória" ficam travados, e a porta só responde leituras (CAP-1). |
| Conexão perdida | Som (estado F) | A Roda apaga e mostra "—" e "sem conexão com o PRO 2.4BT". O app **não mostra o que não sabe**: "O aparelho segue tocando o que já tinha". Chip "Sem conexão". Botão "Procurar aparelhos". O Player segue tocando (CAP-15). |
| Reconectando | Conectando | O app **relê tudo primeiro**. Se o gerador ficou ligado, desliga antes de qualquer outra ação (FM 2). |

### Conferência ao conectar (CAP-16)

Ao entrar no app, a última memória aplicada e o preset dela continuam em uso: o app garante que o aparelho esteja com os dois *(Denis, 30/10)*.

| Resultado | Tratamento |
|---|---|
| **Bate** com a última memória aplicada | Entra direto na aba Som, com essa memória tocando. |
| **Diferença leve** (só ganho, limitador, EQ ou MASTER) | O app **reaplica memória e preset sozinho**, em tela toda ("REAPLICANDO" + memória), sem mutar *(Denis, 30/10)*. A spec foi corrigida (CAP-16 e failure-modes, regra 5; proposta de mudança de 30/10). |
| **Setup em dúvida** (corte, atraso, fase ou rota diferentes) ou **sem memória para comparar** | Os 4 canais ficam **mudos no aparelho** até a decisão, mesmo se o app fechar ou a conexão cair. A Roda e o "+ Nova memória" travam; a IA fica bloqueada por baixo. O chip mostra "Setup em dúvida" e o centro da Roda, "MUDO / até você escolher" em `{colors.warning}`. A folha lista o que não bate e pede uma escolha: **Escolher um Setup** (principal), **Criar um Setup novo** (o som do aparelho vira a 1ª memória dele) ou **Seguir sem Setup**. A legenda que falava da IA saiu. |
| **Sem Setup** (escolheu seguir sem) | As memórias do celular ficam travadas até escolher um Setup. Os links ficam desligados e as saídas aparecem como CH1 a CH4. |
| Processador perdeu o estado | O app reaplica a memória ativa só depois de o Setup ser confirmado (FM 5). |

### Aplicar

| Estado | Tratamento |
|---|---|
| Escolhida, esperando o toque (estado B) | Arco e topo brancos. O centro vira botão pulsando ("toque para aplicar") e mostra o último preset da escolhida, sem "›". A dica mostra "Tocando: <atual>". O som não muda. |
| **Aplicando** (estado C) | Tela toda com o layout novo: APLICANDO, o anel e o nome da memória embaixo; o esquema de ligação no alto, com os canais acendendo até o aparelho confirmar *(Denis, 30/10)*. Vale para memória do celular e do aparelho. [ASSUMPTION] O esquema fora da criação, o nome do preset e a nota "Mantenha o celular perto do processador" não foram desenhados (ver Questões em aberto, nº 16). |
| Aplicado | A camada some e a Roda dá um brilho rápido. A Roda mostra "<memória> · tocando · aplicada agora" e o último preset dela abaixo do nome. O player troca para a playlist dela. |
| **Aplicado pela metade** (a conexão caiu no meio) | Ao voltar a conexão, uma folha oferece **"Terminar de aplicar"** ou **"Voltar à cópia"** *(delegada; FM 3)*. |
| Aplicar preset | Muda só as 15 bandas, com cópia antes e sem confirmação. A memória em uso passa a lembrar esse preset, sem Salvar *(Denis)*. [ASSUMPTION] Ainda não foi decidido se usa a tela toda ou o spinner pequeno. |
| Salvando | Spinner pequeno no botão Salvar ("Salvando"). |

### Criação da memória (30/10)

| Estado | Tratamento |
|---|---|
| **Perguntando** | Uma pergunta por tela. Tocar na resposta avança. ‹ volta, ✕ cancela. As abas e a barra do topo somem. |
| **Escrevendo em "Outro…"** | O campo abre com o teclado; OK ou Enter responde e avança. |
| **Cancelando** | A folha "Cancelar a nova memória?" sobe. "Cancelar e voltar" volta ao som de antes, sem salvar, e leva à Roda. "Continuar criando" fecha a folha. Vale nas perguntas e no "Como ficou?" *(Denis, recomendado)*. |
| **Montando (palco)** | Depois da última resposta, que autoriza aplicar *(Denis, recomendado)*. Cada ajuste vai ao aparelho e é relido enquanto a frase dele passa na tela. Sem ‹ e sem ✕ (poucos segundos; a cerca em código (os limites fixos de cada canal, que a IA não passa) protege os equipamentos) *(Denis, recomendado)*. |
| **Esperando confirmação de risco** | A folha do app sobe por cima do palco; a animação pausa até Confirmar ou Cancelar *(Denis, recomendado)*. |
| **Passagem** | O palco escurece e o anel do chão se abre nos dois anéis de neon, que giram em sentidos contrários e se fecham no anel do Aplicando (1,6 s). |
| **Aplicando** | Os canais do esquema acendem um por um enquanto são gravados e relidos. Tudo gravado: mais 1 s e a tela sai em 0,3 s. Sem ✕. |
| **Pronto (resumo)** | "Pronto. Ouça um pouco." O som novo já está tocando. Nada foi salvo. |
| **Avaliando** | "Como ficou?" com ✕ (cancela e volta ao som de antes). |
| **Refinando** | Volta ao palco só com o que muda (ex.: "Reforçando o grave", +6 dB → +7,5 dB), depois Aplicando e resumo com o que mudou em destaque. |
| **Salvar** | "Salvar a MUSICA ORIG no Casa 2.1?" com Salvar e "Ajustar mais". |
| **Salva** | Volta à Roda com a nova memória tocando e o aviso "Memória MUSICA ORIG criada no Casa 2.1." |
| **Animação desligada** | Sem frases e sem passagem: só o Aplicando pelo tempo real e depois o resumo *(facilitador; aprovado no protótipo 3)*. |
| **Movimento reduzido** | As frases trocam por fade (0,3 s) e o Aplicando entra num fade, sem passagem *(facilitador; aprovado no protótipo 3)*. |
| Erro na releitura ou conexão perdida no meio | [ASSUMPTION] Não foi desenhado. A regra geral vale: releitura diferente é erro (CAP-3) e conexão perdida no meio cai em "Aplicado pela metade" (FM 3). |

### IA por baixo (CAP-12, CAP-13)

A IA não aparece na interface *(Denis, 30/10)*. As regras de segurança da spec continuam valendo por dentro. A porta, o pareamento e a IA fora da criação estão nas Questões em aberto (nº 1 e nº 22).

| Estado | Tratamento |
|---|---|
| Bloqueada | Na dúvida do Setup, a IA não age. |
| Pedido recusado | Chave desligada, fora da rede local ou só leitura: a recusa acontece antes do envio e fica no histórico, fora do app. |

### Player (CAP-15)

| Estado | Tratamento |
|---|---|
| Tocando / pausado | Mini player com a faixa e "Playlist da <memória> · n de N". |
| Faixa indisponível (o arquivo sumiu) | A faixa aparece como **indisponível**. |
| Playlist vazia | [ASSUMPTION] Não foi desenhado. |
| Sem playlist (memória do aparelho) | A memória do aparelho não tem playlist. [ASSUMPTION] O que o mini player mostra nesse caso não foi desenhado. |
| BLE caiu | O Player segue tocando a playlist da memória em uso. |

### Outros

| Estado | Tratamento |
|---|---|
| **Nenhuma memória** (1ª abertura do Casa 2.1) | O app nasce sem memórias e sem presets do celular (P29 D2). "+ Nova memória" é o caminho. [ASSUMPTION] A Roda vazia não foi desenhada. |
| Nenhum preset | O centro mostra "Sem preset ›". Uma memória recém-criada aparece assim no mock. [ASSUMPTION] A lista de presets vazia não foi desenhada. |
| Gerador tocando | A contagem vai até 30 s. No fim, a releitura confirma o desligado. |
| **Gerador ligado + conexão perdida** | 3 tentativas de reconectar. Depois, a tela toda mostra em destaque **"DESLIGUE O MÓDULO AGORA"** *(delegada; FM 1)*. |
| Módulo / BT de áudio | A tela mostra sempre o valor **relido** (CAP-9). |
| Memória do aparelho carregada | A origem do som aparece como "Memória N do aparelho (app da Taramps)", sem Setup e sem playlist. |

## Interaction Primitives

- **Girar** (arrastar em volta da Roda): escolhe a memória. O som não muda.
- **Tocar no centro da Roda:** aplica a escolhida, com o último preset dela. É o único gesto que troca a memória pela Roda.
- **Tocar no preset do centro** (área de 160 × 48 px): abre a lista de presets.
- **Segurar o centro da Roda:** abre Memórias.
- **Tocar numa resposta:** responde e avança. Responder à última pergunta autoriza aplicar.
- **‹ no fluxo:** volta à pergunta anterior. **✕ no fluxo:** pergunta se cancela; cancelar volta ao som de antes, sem salvar.
- **Tocar** em chip, cartão, linha ou botão: age. Na Abertura, um toque pula a animação.
- **Arrastar e pinçar no esquema isométrico:** arrastar gira, pinçar aproxima, um toque abre o equipamento.
- **Folhas:** sobem do pé. A da dúvida só fecha com uma escolha.
- **Voltar do Android:** [ASSUMPTION] segue a plataforma. O comportamento nas folhas obrigatórias, no palco, no Aplicando e nas perguntas (igual ao ‹ ou ao ✕) não foi decidido.
- **Proibido:** conectar sozinho, aplicar ao soltar a Roda, aplicar sem toque, salvar sem o toque em Salvar, a IA salvar, a IA ligar chave, mostrar contador de leituras, mostrar qualquer traço da IA.

## Accessibility Floor

O contraste está em `DESIGN.md › Colors`.

- **Alvos de toque:** [ASSUMPTION] mínimo de 48 dp, o piso do Android (não foi discutido com o Denis). Chips e Cancelar podem parecer menores, mas a área de toque segue esse mínimo. No mock da criação: ‹ e ✕ com 48 × 48 px, respostas com 56 px de altura, botões com 52 px.
- **TalkBack:**
  - o spinner tem papel de status, com o rótulo do que acontece ("Conectando ao PRO 2.4BT", "Aplicando FILME"), como no mock;
  - a Roda anuncia a memória escolhida e a que está tocando;
  - o botão do centro anuncia "toque para aplicar <memória>";
  - a Roda travada anuncia o motivo ("Som mudo até você escolher", "sem conexão");
  - [ASSUMPTION] Ainda não foi decidido como escolher a memória sem o gesto de girar.
  - [ASSUMPTION] Ainda não foi decidido como o palco (cada frase e valor) e o esquema do Aplicando são anunciados.
- **Movimento reduzido:** segue `DESIGN.md › Movimento`. A abertura vira um fade de 0,5 s, o fim do carregamento um fade de 0,2 s, as frases da criação trocam por fade de 0,3 s, a passagem some, os anéis e o anel duplo giram mais devagar, sem pulsos, e nada pisca. O botão do centro continua parecendo botão, sem pulsar.
- **Animação ao criar o som:** além do movimento reduzido do Android, o Denis pode desligar a animação nas Configurações do app.
- **Sala escura:** tema só escuro, fundo preto, ciano só no que está ativo. Textos de estado com ≥ 4,7:1 no preto.
- **Honestidade é acessibilidade:** o estado mostrado é o relido. O app nunca mostra um valor que não sabe.

## Responsive & Platform

- Só celular Android, em retrato. Sem tablet, paisagem, iOS ou web (iOS está fora da SPEC).
- Uma conexão BLE por vez: este app e o app da Taramps não ficam conectados juntos.
- A porta da IA só funciona na rede local (Wi-Fi da casa). Ela não aparece na interface.
- O app não desliga o processador. O ON/OFF é o remote out do módulo, e o processador segue ouvindo o BLE.
- O esquema de ligação do Aplicando usa só os dados que o Setup já guarda. O Setup não guarda posições na sala ou no carro, nem o tipo de ambiente.

## Inspiration & Anti-patterns

**Ficou:**

- **Logo:** símbolo Onda (opção 2) com o nome Empilhado (formato 1).
- **Abertura:** o nome usa a animação da opção 6, Mergulho (sai do desfoque). O símbolo junta o Neon (10) com a temática do Glitch (6), na versão B: a aura fica durante a abertura e o rosa vira ciano no fim.
- **Spinner:** opção 8, Anel duplo.
- **Tela inicial:** opção 2, Roda.
- **Criação da memória** (30/10, [mock](mockups/criacao-memoria-ia.html)):
  - uma pergunta por tela, em tela própria;
  - frase com a animação **Foco** (opção 9 da 3ª rodada, mudada pelo Denis: foco e posição juntos, 0,8 s e 0,65 s), sobre o movimento lateral aprovado na 2ª rodada (sentido fixo, da esquerda para a direita);
  - valor na opção 5 das combinações, **Tudo numa linha** (o valor entra com o mesmo Foco da frase + antes → depois + medidor);
  - posição na opção 3, **Acima do horizonte**;
  - anel durante as frases na opção 3, **Grande e apagado**;
  - passagem na opção 1 da esfera de 1 linha, **Pião e cambalhota** (ver [reconcile](reconcile-referencia-esfera-neon.md));
  - alto do Aplicando: ideia 1, **A sala acendendo**, pelo caminho 2, **esquema de ligação**.

**Rejeitado (só histórico):**

- **Radar de feixe girando com rastro** na busca. Trocado por anéis saindo do centro.
- **Anéis ciano e rosa alternados** na busca (variação 4).
- **Contador de leituras (1 a X)** ao conectar, e qualquer número no carregamento.
- **Conectar sozinho**, mesmo o aparelho conhecido. O Denis sempre escolhe.
- **Soltar a Roda para aplicar** (estava na opção 2 original e no estado B do mock: "solte para aplicar"). Aplicar é um toque no centro.
- **"+ NOVA" dentro da Roda.** É um chip fora dela.
- **Anel duplo no lugar da logo, dentro da Roda, ao aplicar.** A aplicação ocupa a tela toda.
- **Chip de preset embaixo da Roda** ("Preset: Ação ›") e a palavra "Preset" no centro. O preset fica no centro, só com o nome.
- **Escala nas telas inteiras** no fim do carregamento (o cabeçalho duplicava). As telas trocam só pela opacidade.
- **Memórias do aparelho na Roda.** Ficam só em Memórias.
- **Modo claro** e **som na abertura**.
- **Logo Cone** (laranja #FF7A1A), escolhida por engano e corrigida para a Onda. Também as logos 1 e 3 a 13 e os formatos de nome 2 a 10.
- **Aberturas 1 a 5** e as animações de símbolo 1 a 9. Do Neon + Glitch, as versões A (termina limpo) e C (glitch forte).
- **Spinners 1 a 7.**
- **Telas iniciais 1** (Grade), **3** (Agora tocando), **4** (Cinema), **5** (Som e IA juntos), **6** (Tela cheia), **7** (Controle remoto) e **8** (Painel ao vivo).
- **Da rodada de 30/10 (criação):**
  - **Traços da IA na interface** *(Denis)*: a aba IA (chave "IA pela rede", pareamento, sessão ao vivo), o chip da IA, a faixa da IA (estado, última mudança, Pausar e PARAR IA), os pedidos da IA "com PARAR IA sempre visível" e o botão **PARAR IA**, com o vermelho dele (#D92D20; antes, o #FF4D4D de 3,27:1). O app não tem mais vermelho.
  - **A tela Histórico.** O histórico fica fora do app; a porta e o pareamento continuam por baixo.
  - **O antigo Fluxo 2, "Sessão com a IA"** (aba IA, faixa com Pausar e PARAR IA, Continuar a cada 3 ajustes), com a decisão de não ter traço da IA na interface.
  - **Modal com 3 ou 4 perguntas juntas** (forja de 29/10). Trocado por uma pergunta por tela.
  - **Aplicar só no toque em Ouvir** (forja de 29/10). Responder à última pergunta autoriza aplicar.
  - As outras formas de mostrar a conversa: folha por cima da Roda, conversa em balões com campo de texto, ficha do que a IA já sabe e dentro da aba IA.
  - A frase subindo com fade e desfoque (1ª versão do protótipo 3); as 10 transições da 1ª rodada; as 10 laterais da 2ª rodada (só o movimento ficou); o sentido alternado; as 10 animações de texto ligando (só o Foco ficou).
  - Os outros jeitos do valor (contador, odômetro, cápsula, terminal, "gravado no aparelho" etc.) e as combinações 1 a 4; os prefixos "em todas", HPF, LPF e Sub.
  - As posições 1, 2, 4 e 5 (terço de cima, abaixo do título, holofote, anamórfico); os anéis do chão 1, 2 e 4 a 10.
  - **Aplicando antigo** (anel no centro, "Aplicando" e o nome embaixo); o Aplicando na altura das frases com o anel médio no chão.
  - As passagens "Arcos que migram", "Foco que se contrai", "Carga pelas etapas" e 3D1 a 3D15; da esfera, as opções de 2 linhas, de 2+2 e 3+3 anéis, o neon forte, as setas e as duas voltas rápidas; as animações 2 a 10 da esfera de 1 linha.
  - As ideias 2 a 7 para o alto do Aplicando (o jeito da memória, a curva, medidores, contexto do aparelho, a marca, nada); os caminhos 1 (arranjo pela função acústica) e 3 (ambiente de verdade, que exige dado novo no Setup).

## Key Flows

### Fluxo 1 — Noite de filme (Denis no sofá, sala escura, projetor ligado no computador)

1. O Denis pega o celular e abre o BGS Sound. A abertura roda ({motion.opening-total}); ele toca para pular.
2. A Busca começa: anéis ciano saem da logo. O PRO 2.4BT aparece como ponto e como cartão "Taramps · Casa 2.1 da última vez". Um JBL Flip aparece apagado ("Não é um processador").
3. Ele toca em "Conectar ›". O anel duplo gira com "Conectando ao PRO 2.4BT" até a leitura terminar.
4. A conferência bate com a última memória (MUSICA HIGH). O anel fecha num anel ciano e a aba Som entra, com a Roda crescendo e a MUSICA HIGH "tocando · aplicada agora".
5. Ele gira a Roda até a FILME. O arco fica branco, o centro vira botão pulsando ("toque para aplicar") e mostra o último preset da FILME. A dica mostra "Tocando: MUSICA HIGH". O som não muda.
6. Ele toca no centro.
7. **Clímax:** a tela toda escurece. Embaixo, "APLICANDO" sobre o anel duplo e "FILME" abaixo dele; no alto, o esquema do Casa 2.1 acende canal por canal até o aparelho confirmar ([ASSUMPTION] o esquema fora da criação: ver Questões em aberto, nº 16). A camada some e a Roda dá um brilho: "FILME · tocando · aplicada agora", com o preset dela abaixo do nome. O player mostra a playlist da FILME. O Denis larga o celular e volta para o filme.

Falhas e desvios:
- Se o aparelho só diferir em ganho, limitador, EQ ou MASTER, no passo 4 o app reaplica sozinho, em tela toda ("REAPLICANDO MUSICA HIGH").
- Se ele girar e não tocar, a Roda volta para a MUSICA HIGH em 5 s.
- Para trocar só o preset, ele toca em "Ação ›" e escolhe outro na lista. Aplica na hora, e a FILME passa a lembrar esse preset.
- Se a conexão cair durante a aplicação, ao voltar sobe a folha "Terminar de aplicar" / "Voltar à cópia".
- Se a conexão cair depois, entra o estado F ("O aparelho segue tocando o que já tinha").

### Fluxo 2 — Denis refaz a MUSICA ORIG no sofá, criando com a IA

Protagonista: o Denis, no sofá, com a FILME tocando no Casa 2.1. A IA trabalha por baixo; ele nunca a vê. Referência: [mockups/criacao-memoria-ia.html](mockups/criacao-memoria-ia.html). Perguntas, frases e valores abaixo são os de exemplo do mock.

1. Na aba Som, ele toca em **"+ Nova memória"**. A barra do topo e as abas somem; a barra do fluxo mostra "Nova memória / Casa 2.1" e o ✕.
2. "Para que é esta memória?" Ele toca em **Música**. A resposta acende e a próxima pergunta entra pela direita.
3. "Em que volume você costuma ouvir?" Ele toca em **Moderado**.
4. "Onde você senta?" Ele toca em **No meio do sofá**. (Se quisesse mudar o volume, o **‹** voltaria à pergunta anterior com a resposta marcada.)
5. "Como você prefere o grave?" Ele toca em **Forte, com pressão**. Essa última resposta autoriza aplicar. (Se nenhuma opção servisse, **Outro…** abriria o campo para escrever.)
6. O palco entra: tela escura, horizonte fino, o anel grande e apagado girando no chão. "Definindo o corte das **JBL**" chega da borda esquerda desfocada e fica nítida no centro; atrás vem "desligado → 80 Hz", a barrinha anda e o novo valor acende em ciano. A frase fica 1,3 s parada e sai pela direita, desfocando. Seguem "Encaixando o **Sub** nas JBL", "Alinhando o tempo das **caixas**", "Ajustando o **grave**" e "Protegendo os **canais**". Cada ajuste vai ao aparelho e é relido enquanto a frase passa.
7. Se algum ajuste tiver risco, a animação pausa e sobe a folha do app com o que muda, **Confirmar** e **Cancelar**. Ele confirma e o palco segue.
8. Depois da última frase, o palco escurece. O anel do chão se abre num anel ciano em pé e num rosa deitado, que dão uma volta em sentidos contrários e se fecham no anel do Aplicando.
9. **Clímax:** embaixo, "APLICANDO" brilha sobre o anel e "MUSICA ORIG" aparece abaixo. No alto, o esquema do Casa 2.1: CH1 e o fio até a JBL L acendem em ciano, depois CH2 e a JBL R, depois CH3 e CH4 com o Sub. Cada canal que termina fica com luz fraca. Com tudo gravado, a tela fica mais um segundo e some.
10. "Pronto. Ouça um pouco." A lista mostra os 5 ajustes com ✓, o valor e o motivo. O som novo já toca; nada foi salvo. Ele ouve uma música inteira e toca em **Avaliar o som**.
11. "Como ficou?" Ele toca em **Quero mais impacto**. O palco volta só com "Reforçando o **grave**" (+6 dB → +7,5 dB), o Aplicando acende de novo e o resumo mostra o ganho do Sub em destaque: "Mudou 1 ajuste, em destaque. O resto ficou igual."
12. Ele ouve, avalia de novo e toca em **Ótimo, pode salvar**.
13. "Salvar a MUSICA ORIG no Casa 2.1?" Ele toca em **Salvar**. A Roda volta com a MUSICA ORIG tocando e o aviso "Memória MUSICA ORIG criada no Casa 2.1."

Falhas e desvios:
- Em qualquer pergunta ou no "Como ficou?", o ✕ abre "Cancelar a nova memória?". "Cancelar e voltar" devolve o som de antes (a FILME), sem salvar.
- No palco e no Aplicando não há ✕; são poucos segundos e a cerca em código protege os equipamentos.
- Com "Animação ao criar o som" desligada (engrenagem › Configurações do app), os passos 6 e 8 somem: entra só o Aplicando pelo tempo real e depois o resumo.
- Com o movimento reduzido do Android, as frases trocam por fade e não há passagem.
- Em "Salvar", "Ajustar mais" volta ao "Como ficou?".
- [ASSUMPTION] Quando o nome "MUSICA ORIG" é dado não foi desenhado (ver Questões em aberto, nº 12). O que Cancelar faz na folha de risco (voltar ao som de antes ou só pular esse ajuste) não foi desenhado (nº 13).

### Fluxo 3 — Primeira conexão / Setup em dúvida (Denis, primeira abertura, Casa 2.1 herdado)

1. O Denis abre o app pela primeira vez. O Setup Casa 2.1 já está lá, com equipamentos e fichas herdados (CAP-6), mas sem memórias e sem presets.
2. Ele conecta ao PRO 2.4BT pela Busca (Fluxo 1, passos 2 e 3). Na primeira vez, digita a senha, que fica guardada.
3. Não há memória para comparar, então a conferência cai na dúvida. Os 4 canais ficam mudos no aparelho. O chip mostra "Setup em dúvida" e a Roda trava com "MUDO / até você escolher".
4. A folha "Som mudo até você escolher" mostra o que não bate e as 3 escolhas. [ASSUMPTION] O texto para "sem memória para comparar" não foi decidido.
5. Ele toca em **Escolher um Setup** e escolhe o Casa 2.1.
6. **Clímax:** o som volta. Como o Casa 2.1 não tem memória, o app desmuta sem aplicar nada (FM 5; ver A11). O chip mostra "Casa 2.1 ›" e a aba Som mostra a Roda sem memórias, com "+ Nova memória".

Desvios:
- Com **Criar um Setup novo**, o som do aparelho vira a 1ª memória do Setup novo.
- Com **Seguir sem Setup**, as memórias ficam travadas, as saídas aparecem como CH1 a CH4 e os links ficam desligados.
- Depois, um toque no chip do Setup volta a essas escolhas.

### Fluxo 4 — Levar o processador para o carro (Denis, PRO 2.4BT instalado no carro)

1. No carro, o Denis abre o app e conecta ao PRO 2.4BT.
2. A conferência compara o aparelho com a última memória aplicada do Casa 2.1. O corte do Sub e o atraso diferem, então cai na dúvida: som mudo no aparelho e Roda travada.
3. A folha lista o que não bate ("Atraso do Sub: 80 cm na FILME, 0 cm no aparelho", …).
4. Ele toca em **Criar um Setup novo** e dá o nome "Carro". [ASSUMPTION] A tela de nome não foi desenhada.
5. **Clímax:** o som do aparelho vira a 1ª memória do Carro. O som volta, o chip mostra "Carro ›" e a Roda mostra essa memória tocando.
6. Depois, na aba Setup, ele cadastra os equipamentos do carro, e o esquema isométrico se atualiza sozinho. O esquema de ligação do Aplicando passa a mostrar as portas, os tweeters e o sub do carro. [ASSUMPTION] O esquema fora da criação não foi desenhado (ver Questões em aberto, nº 16).
7. De volta em casa, a conferência cai na dúvida de novo. Ele escolhe o Casa 2.1, e o app reaplica a memória ativa só depois dessa confirmação.

Em aberto: A10 (a 1ª memória fica fiel ao link do aparelho ou ao link do Setup novo).

### Fluxo 5 — Refazer a MUSICA ORIG à mão (Denis, teste 1 da SPEC)

**Bloqueado** até decidir como criar uma memória à mão, sem perguntas (ver Questões em aberto, nº 23).

1. Com o Casa 2.1 em uso, o Denis cria a MUSICA ORIG.
2. O aviso confirma: "Memória MUSICA ORIG criada no Casa 2.1."
3. Em Ajustes, ele regula as saídas e os globais. Cada mudança é escrita e relida. Depois toca em **Salvar**.
4. Ele desliga e religa o processador, abre o app e conecta.
5. **Clímax:** a conferência bate (ou o app reaplica depois de confirmar o Setup). A Roda mostra "MUSICA ORIG · tocando", com o som igual ao da memória.

Falha: se o aparelho voltar com corte, atraso, fase ou rota diferentes, cai na dúvida (Fluxo 3) e nada toca sem a escolha dele.

### Fluxo 6 — Conferir contra o app da Taramps (Denis, teste 3 da SPEC)

1. O Denis segura o centro da Roda e abre Memórias.
2. Na seção "No aparelho (app da Taramps)", ele escolhe a FILME. Uma folha pede confirmação, e o Aplicando mostra "APLICANDO" + o nome da memória.
3. A origem do som passa a "Memória N do aparelho (app da Taramps)", sem playlist.
4. Ele sai do BGS Sound (uma conexão BLE por vez), abre o app da Taramps e compara os valores com os de Ajustes. [ASSUMPTION] Como desconectar pelo BGS Sound não foi desenhado.
5. **Clímax:** todos os valores batem. Ao voltar ao BGS Sound, o app relê tudo antes de qualquer ação.

### Fluxo 7 — Medir com o gerador (Denis, conferindo o voltímetro contra o multímetro VC830L)

1. Em Ajustes › Medir, o Denis vê ao vivo o voltímetro de cada saída e da entrada, e o clip por canal.
2. Para um tom de teste, ele abre o Gerador. A chave do gerador está desligada. Ao ligar, aparece antes o aviso "Um tom forte pode queimar o tweeter ou o sub".
3. Ele escolhe frequência e ganho e toca. A contagem corre até 30 s.
4. **Clímax:** no fim da contagem, ou quando ele toca em parar, o gerador desliga e a releitura confirma o desligado.

Falha: se a conexão cair com o gerador ligado, o app tenta reconectar 3 vezes. Sem sucesso, a tela toda mostra "DESLIGUE O MÓDULO AGORA". Ao reconectar, o gerador é desligado antes de qualquer outra ação.

Ainda sem fluxo próprio: "Montar um Setup" (só o passo 6 do Fluxo 4), "Ouvir uma memória com a sua playlist" (Player) e "Trazer um preset antigo da Taramps" (depende de A5).

## Questões em aberto

**Resolvido pela proposta de mudança de 30/10** (`../../sprint-change-proposal-2026-10-30.md`, aplicada na spec):

1. **Mudanças de 30/10 contra a spec** — *resolvido.* A chave da porta e o código de pareamento ficam em **Aparelho**, como "Controle pela rede" (D3). Fora da criação, a IA mexe pela porta e o Continuar a cada 3 ajustes é uma folha do app, sem falar em IA (D4). O toque sempre pausa a tarefa da porta. A criação entra no POC como CAP-18 (só o lado do app). Itens originais:
   - **Reaplicar sozinho ao entrar:** memória e preset em uso ao entrar; diferença leve em ganho, limitador, EQ ou MASTER reaplica sozinha, em tela toda. Troca a regra da CAP-16 / P29 D3 (avisar e oferecer Reaplicar, sem mutar). Se a spec mantiver a regra antiga, o aviso com Reaplicar precisa de desenho.
   - **Interface sem traço da IA:** saem a aba IA, o PARAR IA, o Pausar, o chip e a faixa da IA, e a tela Histórico; a porta e o pareamento continuam por baixo; o histórico fica fora do app. Afeta CAP-11, CAP-12 e CAP-13 (chave "IA pela rede", código de pareamento, faixa em até 1 s, PARAR IA que revoga o acesso, Continuar a cada 3 ajustes, Histórico com origem). Falta decidir onde ficam a chave da porta (que vem desligada por padrão) e o código de pareamento de 6 dígitos, como o aviso do risco da porta aparece e se o Continuar ainda existe. A A4 (Continuar × controles travados) passa para cá; o Pausar saiu.
   - **Uma pergunta por tela** (a forja de 29/10 dizia 3 ou 4 perguntas juntas).
   - **Aplicar ao responder à última pergunta** (a forja dizia aplicar só no toque em Ouvir). Nada é salvo sem o toque em Salvar.
   - **Aplicando com o esquema de ligação** do Setup no alto e o bloco embaixo.
   - **Parar a criação:** ✕ nas perguntas e no "Como ficou?", sem ✕ no palco e no Aplicando (sem PARAR IA).
   - **Confirmação de risco** pela folha do app, sem mencionar a IA, com a animação pausada.

**Da spec (ambiguidades da extração):**

2. **A3, referência da conferência:** "última memória aplicada no último Setup usado" × "naquele Setup" × "+ o link do Setup". Não está dito se um link diferente muta ou só avisa, nem como "memória ativa" difere de "última memória aplicada".
3. **A5, preset antigo da Taramps:** salvar como preset (15 bandas) ou como memória? Um preset da Taramps traz mais que 15 bandas.
4. **A7, rótulos:** separar "EQ de entrada" (memória), "EQ global" das notas e as 15 bandas (preset); listar o que fica fora das camadas.
5. **A8, limites:** a interface mostra algum alerta (ex.: gerador acima de −13 dB)? A POC não tem teto.
6. **A9, memória do aparelho carregada × Setup:** dá para salvar esse som como memória do celular do Setup atual? Como isso convive com "seguir sem Setup trava as memórias"?
7. **A10, Setup criado do aparelho:** a 1ª memória fica fiel ao som (link do aparelho) ou ao Setup novo (link desligado)?
8. **A11, Setup sem memória:** a 1ª conexão sempre muta, e o desmute toca o que estiver no aparelho, sem referência. É essa a experiência desejada?
9. **A14, avisos customizados (CAP-14):** quantos e quais o redesenho deve ter? A meta mínima de 1 aviso veio da spec, não do Denis.

Resolvidas acima: A1 (a interface não dá nome à IA; KARINA não aparece), A2 (vale o padrão "travado"), A6 (a IA não salva), A12 (notas sem tom de recomendação) e A13 (botão "Salvar"). A15 é só histórico.

**Do diário:**

10. "Todas opções" (resposta do Denis sobre a tela inicial) ficou ambíguo: todas as telas iniciais disponíveis no app, ou o spinner em todos os carregamentos? O spinner em todo carregamento já é decisão. A primeira leitura (várias telas iniciais) não foi confirmada.
11. **Regular a memória à mão:** as 6 formas de [.working/regular-memoria-opcoes-1.html](.working/regular-memoria-opcoes-1.html) (lista por saída, esquema da sala, mesa de som, por etapa, curva, abas internas) seguem sem escolha.

**[ASSUMPTION] da criação da memória (30/10, confirmar):**

12. **Nome da memória:** quando é pedido (antes das perguntas, no Salvar ou sugerido) não foi desenhado; o mock usa "MUSICA ORIG" desde o início.
13. **Folha de risco na criação:** o texto e quais ajustes contam como risco. *Resolvido em 30/10 (D5):* Cancelar na folha de risco volta ao som de antes, como o ✕.
14. **✕ no resumo e no Salvar:** a decisão põe o ✕ só nas perguntas e no "Como ficou?", e a spine segue isso. O mock ainda mostra o ✕ no resumo e no Salvar e precisa acompanhar.
15. **Animação desligada:** a decisão diz "só o Aplicando pelo tempo real"; o mock ainda segura cada canal por no mínimo 0,7 s. Falta decidir se o esquema anda no tempo real ou no mínimo de 0,7 s.
16. **Aplicando fora da criação:** se o esquema de ligação também aparece ao aplicar pela Roda e ao reaplicar, e o que aparece no alto quando não há dados para desenhar o esquema (memória do aparelho sem Setup, "Seguir sem Setup"). Também falta o lugar do nome do preset (antes, abaixo do nome da memória) e da nota "Mantenha o celular perto do processador" no layout novo; o mock da criação não tem nenhum dos dois. O fundo do Aplicando fora da criação continua sendo o degradê azul-escuro com desfoque.
17. **Perguntas e respostas:** o mock tem 4 perguntas de perfil e 4 respostas no "Como ficou?", todas de exemplo. A forja de 29/10 previa também a escuta "1 ou 2", o diagnóstico da queixa e a janela de ajuda para medir, que não foram desenhados nesta rodada.
18. **Erro no meio da criação:** releitura diferente do pedido ou conexão perdida durante o palco ou o Aplicando não foi desenhado.
19. **Engrenagem:** como ela oferece Aparelho e Configurações do app (menu, folha ou tela).
20. **TalkBack:** como escolher a memória sem o gesto de girar a Roda, e como o palco, os valores e o esquema do Aplicando são anunciados.
21. **Voltar do Android:** nas perguntas, no palco, no Aplicando e nas folhas obrigatórias.
22. **A IA fora da criação** — *resolvido em 30/10 (D4):* ela mexe no som pela porta; a Roda mostra só os valores, sem travar e sem texto da IA; a cada 3 ajustes seguidos, uma folha do app pede Continuar.
23. **Criar uma memória à mão, sem perguntas:** desde 30/10, "+ Nova memória" abre a criação com perguntas (Fluxo 2). A entrada para criar uma memória a partir do som atual, sem perguntas, não foi decidida. O Fluxo 5 depende disso.

**[ASSUMPTION] usados nesta spine (confirmar):**

24. A fonte é a do sistema. Os tamanhos dos mocks valem como sp; os pequenos (8,5 a 11) ainda precisam ser conferidos na sala escura.
25. As abas não têm ícones.
26. Cancelar na conexão volta para a Busca. O chip do Setup é o "nome do Setup" e reabre a escolha.
27. Telas e estados sem desenho:
    - Busca: BT desligado/permissões, nenhum aparelho encontrado, BLE ocupado, pedido de senha e senha errada.
    - Som: Roda sem memórias, lista de presets vazia, playlist vazia e mini player de memória do aparelho.
    - Aplicação e conferência: folha da dúvida sem memória para comparar, retorno ao aplicar preset (tela toda ou spinner pequeno) e nome ao criar Setup.
    - Faixa de só leitura.
28. Componentes sem visual: interruptor de chave, selo de origem, esquema isométrico, medidores e clip ("ERRO 0"), gerador com contagem, alerta "DESLIGUE O MÓDULO AGORA", avisos (CAP-14), e as telas Memórias, Player, Presets, Ajustes, Setup e Aparelho.
29. Tocar no centro quando a escolhida já é a que está tocando: comportamento não decidido.
30. Alvos de toque ≥ 48 dp (a área do preset passou a 160 × 48 px em 30/10); valor exato de "mais devagar" nos anéis da busca; como desconectar para usar o app da Taramps. (TalkBack e Voltar do Android: nº 20 e nº 21.)
31. **Mudanças não salvas:** não foi decidido o que acontece se o Denis mexer em Ajustes e trocar de memória ou sair sem Salvar, nem se apagar uma memória ou um preset pede confirmação.

As questões resolvidas em 30/10 estão no corpo desta spine e no `.memlog.md`.
