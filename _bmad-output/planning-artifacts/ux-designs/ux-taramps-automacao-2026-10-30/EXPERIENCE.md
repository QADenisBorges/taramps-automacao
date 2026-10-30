---
name: BGS Sound
status: draft
updated: 2026-10-30
sources:
  - ../../../specs/spec-app-controle-taramps/SPEC.md
  - ../../sprint-change-proposal-2026-10-29.md
  - DESIGN.md
---

# BGS Sound — Experience Spine

> **Referências visuais** (escolhidas pelo Denis em 30/10/2026):
> - [.working/logo-final.html](.working/logo-final.html) — logo, ícone e barra do topo.
> - [.working/abertura-escolhida.html](.working/abertura-escolhida.html) — abertura.
> - [.working/spinner-opcoes-1.html](.working/spinner-opcoes-1.html) — só a **opção 8, Anel duplo**.
> - [.working/conexao-radar.html](.working/conexao-radar.html) — só as **telas 2 e 3** (procurar e listar; conectando).
> - [.working/tela-inicial-roda-estados.html](.working/tela-inicial-roda-estados.html) — Roda, estados A a F.
> - [.working/tela-inicial-opcoes-1.html](.working/tela-inicial-opcoes-1.html) — só a **opção 2, Roda**.
>
> Os spines vencem em caso de conflito com qualquer mock.

O escopo do redesenho é visual e de organização. O app continua fazendo o que a SPEC define (CAP-1 a CAP-17); telas, navegação e fluxos foram repensados do zero. Os IDs CAP-n seguem a SPEC.

Decisões marcadas *(delegada)* foram recomendadas pelo facilitador e aceitas porque o Denis delegou: "o resto siga escolhendo recomendado".

## Foundation

- **Plataforma:** só celular Android, em retrato. Um único usuário, o Denis.
- **Sistema de UI:** nenhum foi nomeado. A spine não herda de Material/Compose nem de outra biblioteca. As convenções da plataforma (voltar do Android, TalkBack, movimento reduzido) valem onde esta spine não diz nada.
- **Visual:** `DESIGN.md` é a referência visual. Tema só escuro *(delegada)*.
- **Onde o app é usado** (Denis): no sofá, ouvindo; ao lado do notebook, com a IA regulando; e com o computador ligado no projetor para ver filme, na sala escura.
- **Usos principais, nesta ordem:** 1º trocar o som (escolher a memória do momento e ouvir); 2º regular com a IA. O ajuste manual e o Medir não estão entre os usos principais e ficam fora da aba principal.
- **A IA não está dentro do app.** É o Claude Code no notebook, que fala com o app pela porta de comandos no Wi-Fi da casa (CAP-12). Na interface ela se chama só **"IA"** *(delegada)*.

## Information Architecture

**Navegação** *(delegada)*: 4 abas fixas embaixo, **Som · IA · Ajustes · Setup**. A barra do topo tem a logo de uma linha, o chip do Setup em uso ("Casa 2.1 ›") e a **engrenagem**, que leva a **Aparelho** e **Histórico**. Folhas sobem por cima da aba atual. A aplicação de memória cobre a tela toda.

| Tela | Para que serve | O que mostra | Como se chega | CAP |
|---|---|---|---|---|
| **Abertura** | Marcar a entrada no app | A animação Neon + Glitch B até a logo parada ({motion.opening-total}) | Toda abertura a frio. Não toca ao voltar do segundo plano *(delegada)* | — |
| **Busca / Conexão** | Achar o processador e conectar | Tela 2: "Procurando aparelhos", com anéis saindo da logo, pontos e a lista de encontrados. Tela 3: anel duplo com "Conectando ao" + nome e Cancelar | Depois da Abertura; "Procurar aparelhos" (conexão perdida) | CAP-1, CAP-2, CAP-16 |
| **Som (Roda)** | Trocar o som | A Roda com as memórias do Setup em uso. Embaixo: o chip do preset em uso, o mini player, o chip da IA e "+ Nova memória" *(delegada)* | Aba Som; destino depois da conexão | CAP-4, CAP-5, CAP-15, CAP-13 |
| **Memórias** | Cuidar das memórias | As memórias do celular (renomear, apagar) e uma seção à parte, **"No aparelho (app da Taramps)"**, com as 3 memórias internas e as notas herdadas, sem tom de recomendação *(delegada)* | **Segurar o centro da Roda** | CAP-4, CAP-6 |
| **Player** | Ouvir a memória com a playlist dela | A playlist da memória em uso, a faixa atual, tocar/pausar/pular e a edição da playlist | Toque no mini player | CAP-15 |
| **Presets** | Trocar o "gosto" (15 bandas) | A lista de presets. Um toque aplica | Chip "Preset: Ação ›" | CAP-5 |
| **Ajustes** | Regular à mão | As saídas do Setup (nome do equipamento e cor do canal; um par com link conta como uma saída), os globais (EQ de entrada, rota, MASTER), as 15 bandas do preset numa seção própria e o botão **Salvar** fixo no pé *(delegada)* | Aba Ajustes | CAP-3, CAP-4, CAP-5 |
| ↳ **Editor de saída** | Ajustar uma saída | Ganho, limitador, atraso, fase, corte e EQ de saída | Toque numa saída | CAP-3 |
| ↳ **Medir** | Ver voltímetro e clip ao vivo | A monitoração por saída e da entrada | Área Medir, dentro de Ajustes | CAP-7 |
| ↳ **Gerador** | Tocar tom fixo ou sweep | Frequência, ganho, tocar/parar e contagem de 30 s; atrás da chave do gerador | Dentro de Medir | CAP-8, CAP-11 |
| **IA** | Ligar e acompanhar a IA | A chave "IA pela rede", o pareamento (código de 6 dígitos), a sessão ao vivo (o que a IA mudou, Pausar, PARAR IA) e a opção de uso ao mesmo tempo *(delegada)* | Aba IA | CAP-12, CAP-13, CAP-11 |
| **Setup** | Montar e ver a instalação | O Setup em uso no esquema isométrico, a lista de equipamentos com fichas e selo de origem, a lista de Setups e "criar Setup" *(delegada)* | Aba Setup | CAP-16, CAP-17, CAP-6 |
| **Aparelho** | Comandos do aparelho inteiro | As chaves de acesso, o módulo (remote out), o Bluetooth de áudio, o firmware, o nome do produto, a senha e os resets *(delegada)* | Engrenagem | CAP-9, CAP-10, CAP-11, CAP-1 |
| **Histórico** | Saber quem mexeu | Cada comando com origem (tela/IA), hora e valor | Engrenagem | CAP-13 |
| **Folha da dúvida** | Decidir o Setup com o som mudo | O que não bate e as 3 escolhas | Automática ao conectar; toque no chip do Setup | CAP-16 |

Estas telas ainda não têm mock e serão construídas só a partir desta spine: Memórias, Player, Presets, Ajustes (com o editor, o Medir e o Gerador), IA, Setup, Aparelho e Histórico. Ver Questões em aberto.

## Voice and Tone

Microcopy *(delegada)*: pt-BR, frases curtas e diretas, botões começando pelo verbo. **Nunca afirmar o que o app não sabe.** Números com vírgula decimal e unidade (−1,5 dB, 2,1 ms). A voz da marca está em `DESIGN.md › Brand & Style`.

| Fazer | Não fazer |
|---|---|
| "Conectando ao PRO 2.4BT" (com o nome, sem números) | "Lendo 37 de 84…" |
| "Som mudo até você escolher" | "Erro de configuração detectado" |
| "O aparelho segue tocando o que já tinha" | Mostrar a última memória como se fosse o som atual sem conexão |
| "Escolher um Setup" · "Criar um Setup novo" · "Seguir sem Setup" | "OK" / "Continuar?" genéricos |
| Dado de ficha sem origem: deixar em branco | Marcar como "verificado" sem origem |
| Notas das memórias do aparelho como notas | "Recomendado" para uma memória do aparelho |

**Textos já decididos**, dos mocks escolhidos e das fontes:

| Lugar | Texto |
|---|---|
| Busca | "Procurando aparelhos" · "Deixe o processador ligado e o Bluetooth do celular ativo." · "2 encontrados · toque para conectar" · "Conectar ›" · "Taramps · Casa 2.1 da última vez" · "Não é um processador" |
| Conectando | "Conectando ao" + nome do aparelho · "Cancelar" |
| Roda | "Gire para trocar o som" · "tocando · aplicada agora" · "toque para aplicar" · "Tocando: FILME" · "+ Nova memória" · "Preset: Ação ›" · "IA pareada · parada" |
| Aplicando | "Aplicando" + nome da memória · "Mantenha o celular perto do processador" |
| Dúvida | Chip "Setup em dúvida" · centro "MUDO / até você escolher" · "Som mudo até você escolher" · "O aparelho não bate com a FILME do Casa 2.1:" · cada diferença no formato "Atraso do Sub: 80 cm na FILME, 0 cm no aparelho" (P29 §5.5) · "A IA fica bloqueada até você escolher" |
| IA | "IA ajustando o EQ da FILME" · "a IA está ajustando" · "JBL L · 63 Hz −1,5 dB" + "agora" · "Pausar" · "PARAR IA" · "Continuar" · "IA pela rede" |
| Sem conexão | Chip "Sem conexão" · "sem conexão com o PRO 2.4BT" · "O aparelho segue tocando o que já tinha" · "Procurar aparelhos" |
| Aplicado pela metade | "Terminar de aplicar" · "Voltar à cópia" |
| Gerador sem conexão | "DESLIGUE O MÓDULO AGORA" |
| Memórias | "No aparelho (app da Taramps)" · "Memória N do aparelho (app da Taramps)" |
| Aviso (CAP-14) | "Memória MUSICA ORIG criada no Casa 2.1." |
| Link | "CH3 e CH4 ligados: alimentam o mesmo bridge" (P29 §5.5) |
| Riscos das chaves (CHA) | Gerador: "Um tom forte pode queimar o tweeter ou o sub" · Remote out: "Liga ou corta o som de repente" · Reset de fábrica: "Apaga a senha e as 3 memórias" · Reset de áudio: "O estado resultante é imprevisível" · Senha: "Uma senha perdida tranca o aparelho" |

Termo: o botão se chama só **"Salvar"**. "Salvar por camadas" era o nome do modelo antigo (A13). A divisão entre memória e preset acontece por dentro.

## Component Patterns

Comportamento. As specs visuais estão em `DESIGN.md › Components`.

| Componente | Onde | Regras de comportamento |
|---|---|---|
| **Barra do topo** | Global (menos Abertura e aplicação em tela toda) | Logo de uma linha, chip do Setup e engrenagem. A engrenagem abre Aparelho e Histórico *(delegada)*. [ASSUMPTION] A logo não é botão. |
| **Roda** | Som | Mostra só as memórias do celular que existem no Setup em uso; as memórias do aparelho nunca entram nela *(delegada)*. **Girar só escolhe; o som não muda.** A memória que para no topo, sob o arco, é a escolhida. Se o Denis girar e não tocar no centro, a Roda volta sozinha para a memória que está tocando depois de {motion.roda-auto-return} *(delegada)*. Fica travada (apagada, com cadeado) na dúvida, com a IA trabalhando no padrão "travado", sem conexão, sem Setup e no só leitura. |
| **Botão do centro** | Som | Aparece quando a escolhida é diferente da que está tocando ("toque para aplicar"). **Um toque aplica** a escolhida, com cópia antes e releitura (CAP-4, FM 3). Aplicar também traz de volta o último preset da memória. **Segurar** o centro abre Memórias *(delegada)*. |
| **Aplicando em tela toda** | Global | Entra no toque do centro e em "carregar memória do aparelho". Cobre o app com o anel duplo e "Aplicando <memória>" até o aparelho confirmar pela releitura. Não tem botão de cancelar. Depois some, e a Roda mostra a nova memória com "tocando · aplicada agora". |
| **Spinner Anel duplo** | Global | É o indicador de **todo** carregamento e nunca mostra números. Tem três tamanhos: grande ao conectar, grande aplicando e pequeno (18 px) dentro do botão que disparou a ação ("Salvando"). |
| **Anéis da busca + lista** | Busca | Os anéis seguem saindo enquanto procura, **mesmo depois do primeiro aparelho**. Cada aparelho encontrado vira um ponto e um cartão, e a lista cresce. **Nada conecta sozinho**, nem o aparelho já conhecido *(Denis)*. Outros Bluetooth ficam apagados e não conectam. |
| **Cartão de aparelho** | Busca | Um toque em "Conectar ›" troca os anéis pelo anel duplo com o nome do aparelho. "Cancelar" interrompe a conexão. [ASSUMPTION] Cancelar volta para a busca. |
| **Chip do Setup** | Barra do topo | Mostra o Setup em uso, "Setup em dúvida" ou "Sem conexão". [ASSUMPTION] É o "nome do Setup" da SPEC CAP-16: tocar nele volta à escolha de Setup (folha da dúvida, com as 3 escolhas). |
| **Chip do preset** | Som | "Preset: <nome> ›" abre a lista de presets. Um toque num preset **aplica sem confirmação** (com cópia antes; muda só as 15 bandas) *(delegada; CAP-5)*. |
| **Mini player** | Som | Play/pausa no botão. Um toque no bloco abre o Player. Mostra a playlist da memória em uso ("Playlist da FILME · 2 de 8"). Continua tocando se o BLE cair (CAP-15). |
| **Chip da IA** | Som | Mostra o estado da IA ("IA pareada · parada"). [ASSUMPTION] Um toque leva à aba IA. |
| **+ Nova memória** | Som, botão fora da Roda | Cria uma memória do Setup em uso a partir do som atual (CAP-4). Fica fora da Roda para que um giro nunca caia em "criar" *(Denis aceitou a recomendação)*. Fica travado sem Setup. [ASSUMPTION] O nome é pedido ao criar; a forma não foi desenhada. |
| **Faixa da IA** | Som (e onde a IA estiver ativa) | Aparece com a IA ativa: estado, última mudança (em até 1 s, CAP-13), **Pausar** e **PARAR IA**. PARAR IA revoga o acesso na hora e destrava os controles. |
| **Folha** | Global | Sobe do pé por cima da aba atual. É usada em: dúvida do Setup, pedidos da IA (risco, Continuar), aplicado pela metade, Salvar, carregar memória do aparelho (confirmação). **Pedidos da IA sobem por cima de qualquer aba, com PARAR IA sempre visível** *(delegada)*. A folha da dúvida não fecha sem uma escolha. [ASSUMPTION] As outras fecham com voltar ou arrastando para baixo. |
| **Salvar** | Ajustes, fixo no pé | **É um passo à parte, com um botão só.** Ele reparte por camada: a regulagem vai para a memória em uso e as 15 bandas para o preset. Se as 15 bandas mudaram, a folha pergunta: atualizar o preset em uso (vale para toda memória que o usa) ou criar um novo (CAP-4, CAP-5). Nada é salvo sem esse toque. **A IA nunca salva.** |
| **Saída (linha)** | Ajustes | Mostra o nome do equipamento alimentado e a cor do canal (sem equipamento: CH1 a CH4). Um par com link é uma saída e escreve nos dois canais. O link não é controle: aparece só como a linha "CH3 e CH4 ligados: alimentam o mesmo bridge". |
| **Controle de parâmetro** | Editor de saída, globais, 15 bandas | Cada mudança é escrita ao vivo e relida. A tela mostra o valor **relido**. Se a releitura der diferente do pedido, é erro (CAP-3). |
| **Chave de acesso** | Aparelho, IA, Gerador | Ao ligar, o aviso do risco aparece **antes** (texto da CHA). Com a chave desligada, o pedido é recusado antes de qualquer envio, pela tela e pela IA. A IA nunca liga nem desliga chave (CAP-11). |
| **Gerador** | Medir | Fica atrás da chave (desligada por padrão). Toca no máximo 30 s, com contagem. Ao fechar a tela, perder a conexão ou dar erro, manda desligar. A releitura confirma o desligado (CAP-8, FM 1). |
| **Medidores** | Medir | Voltímetro por saída e da entrada; clip no canal certo (CAP-7). O voltímetro pode responder "ERRO 0", e a tela não inventa um valor no lugar. |
| **Esquema isométrico** | Setup | Arrastar gira, pinça aproxima e um toque num equipamento abre a ficha. Ele se atualiza sozinho quando um equipamento entra, muda ou sai (CAP-17). |
| **Ficha / selo de origem** | Setup | Cada dado mostra a origem: oficial (com endereço), outra fonte, medido ou digitado. Um dado sem origem confirmada nunca aparece como verificado; o desconhecido fica em branco; um modelo da base completa a ficha sozinho (CAP-16). |
| **Abas** | Global | Trocam de tela sem perder o estado da Roda. Ficam visíveis em tudo, menos na Abertura, na Busca/Conexão e na aplicação em tela toda. [ASSUMPTION] Na Busca, os mocks não mostram as abas. |

## State Patterns

Os estados A a F da Roda estão em [.working/tela-inicial-roda-estados.html](.working/tela-inicial-roda-estados.html). A busca e a conexão estão em [.working/conexao-radar.html](.working/conexao-radar.html), telas 2 e 3.

### Conexão

| Estado | Tela | Tratamento |
|---|---|---|
| Bluetooth do celular desligado / sem permissão | Busca | O app pede para ligar o BT e as permissões. [ASSUMPTION] O texto e o visual não foram desenhados. |
| Procurando | Busca | Os anéis saem da logo; dica "Deixe o processador ligado e o Bluetooth do celular ativo." |
| Encontrados | Busca | Os pontos ficam nos anéis e os cartões na lista; "N encontrados · toque para conectar"; a busca continua. |
| Nenhum encontrado | Busca | [ASSUMPTION] Os anéis continuam. Não há estado de "desistir" decidido. |
| BLE ocupado (app da Taramps conectado) | Busca/Conectando | Só existe uma conexão BLE por vez. [ASSUMPTION] A mensagem não foi desenhada. |
| Conectando / autenticando / lendo | Conectando | O anel duplo e "Conectando ao <nome>" ficam até a leitura completa (~13 s) terminar. Não há contador. Na primeira vez, a senha é pedida e depois guardada; ela nunca aparece na tela (CAP-1). [ASSUMPTION] O pedido de senha não foi desenhado. |
| Senha errada | Conectando | A conexão é recusada sem nenhuma escrita (CAP-1). [ASSUMPTION] O texto não foi desenhado. |
| Firmware ≠ R003 → **só leitura** | Global | Uma faixa de aviso aparece. Nada é escrito: a Roda, o Salvar, os controles e as chaves ficam travados, e a porta só responde leituras (CAP-1). |
| Conexão perdida | Som (estado F) | A Roda apaga e mostra "—" e "sem conexão com o PRO 2.4BT". O app **não mostra o que não sabe**: "O aparelho segue tocando o que já tinha". O chip mostra "Sem conexão". O botão é "Procurar aparelhos". O Player segue tocando (CAP-15). |
| Reconectando | Conectando | O app **relê tudo primeiro**. Se o gerador ficou ligado, ele é desligado antes de qualquer outra ação (FM 2). |

### Conferência ao conectar (CAP-16)

| Resultado | Tratamento |
|---|---|
| **Bate** com a última memória aplicada | Entra direto na aba Som, com essa memória tocando. |
| **Diferença leve** (só ganho, limitador, EQ ou MASTER) | O app avisa e oferece **Reaplicar**, com o som tocando e **sem mutar** (P29 D3). [ASSUMPTION] A forma do aviso não foi desenhada. |
| **Setup em dúvida** (corte, atraso, fase ou rota diferentes) ou **sem memória para comparar** | Os 4 canais ficam **mudos no aparelho** até a decisão, mesmo se o app fechar ou a conexão cair. A Roda e a IA ficam travadas, o chip mostra "Setup em dúvida" e o centro da Roda mostra "MUDO / até você escolher" em `{colors.warning}`. A folha lista o que não bate e pede uma escolha: **Escolher um Setup** (principal), **Criar um Setup novo** (o som do aparelho vira a 1ª memória dele) ou **Seguir sem Setup**. A legenda diz "A IA fica bloqueada até você escolher". |
| **Sem Setup** (escolheu seguir sem) | As memórias do celular ficam travadas até escolher um Setup. Os links ficam desligados e as saídas aparecem como CH1 a CH4. |
| Processador perdeu o estado | O app reaplica a memória ativa só depois de o Setup ser confirmado (FM 5). |

### Aplicar

| Estado | Tratamento |
|---|---|
| Escolhida, esperando o toque (estado B) | O arco e o topo ficam brancos, o centro vira botão pulsando ("toque para aplicar") e a dica mostra "Tocando: <atual>". O som não muda. |
| **Aplicando** (estado C) | Tela toda com o anel duplo e "Aplicando <memória>" até o aparelho confirmar *(Denis)*. Vale para memória do celular e para memória do aparelho. |
| Aplicado | A camada some e a Roda mostra "<memória> · tocando · aplicada agora". O player troca para a playlist dela e o chip do preset mostra o último preset dela. |
| **Aplicado pela metade** (a conexão caiu no meio) | Ao voltar a conexão, uma folha oferece **"Terminar de aplicar"** ou **"Voltar à cópia"** *(delegada; FM 3)*. |
| Aplicar preset | Muda só as 15 bandas, com cópia antes e sem confirmação. [ASSUMPTION] Ainda não está decidido se usa a tela toda ou o spinner pequeno. |
| Salvando | O spinner pequeno aparece no botão Salvar ("Salvando"). |

### IA (CAP-12, CAP-13)

| Estado | Tratamento |
|---|---|
| Porta desligada | A chave "IA pela rede" vem desligada por padrão. Ao ligar, o aviso do risco aparece antes. |
| Pareando | O código de 6 dígitos aparece na aba IA. A IA o envia uma vez. |
| Pareada · parada | Chip "IA pareada · parada" na aba Som. |
| **Ativa** (estado E) | A faixa mostra "IA ajustando <o quê>" com a última mudança. No padrão "controles travados até PARAR IA", a Roda fica travada com "a IA está ajustando". Cada mudança da IA vira aviso e entra no Histórico. |
| Uso ao mesmo tempo, alternativa | Se o Denis escolher "o toque pausa a IA" nas opções da IA: um toque pausa a IA por uma janela de 10 s, e a IA recebe "o usuário está mexendo nisso". |
| Pausada | [ASSUMPTION] O que o Pausar faz no padrão "travado" não foi definido (A4). |
| Esperando confirmação de risco | Uma folha sobe por cima de qualquer aba, com PARAR IA visível. Vale para aplicar memória, carregar memória do aparelho, remote out, gerador, resets e senha. |
| Esperando **Continuar** | Depois de 3 ajustes seguidos da IA sem um toque do Denis, uma folha pede "Continuar", com PARAR IA visível. O número 3 é provisório (P29 D4). |
| Bloqueada | Na dúvida do Setup, a IA não age. |
| Parada | PARAR IA revoga o acesso na hora e os controles voltam. |
| Pedido recusado | Chave desligada, fora da rede local ou só leitura: a recusa acontece antes do envio e fica no Histórico. |

### Player (CAP-15)

| Estado | Tratamento |
|---|---|
| Tocando / pausado | Mini player com a faixa e "Playlist da <memória> · n de N". |
| Faixa indisponível (o arquivo sumiu) | A faixa aparece como **indisponível**. |
| Playlist vazia | [ASSUMPTION] Não foi desenhado. |
| Sem playlist (memória do aparelho) | A memória do aparelho não tem playlist. [ASSUMPTION] O que o mini player mostra nesse caso não foi desenhado. |
| BLE caiu | O Player continua tocando a playlist da memória em uso. |

### Outros

| Estado | Tratamento |
|---|---|
| **Nenhuma memória** (1ª abertura do Casa 2.1) | O app nasce sem memórias e sem presets do celular (P29 D2). [ASSUMPTION] A Roda vazia não foi desenhada. "+ Nova memória" é o caminho. |
| Nenhum preset | [ASSUMPTION] Não foi desenhado. |
| Gerador tocando | A contagem vai até 30 s. Ao fim, a releitura confirma o desligado. |
| **Gerador ligado + conexão perdida** | São 3 tentativas de reconectar. Depois, a tela toda mostra em destaque **"DESLIGUE O MÓDULO AGORA"** *(delegada; FM 1)*. |
| Módulo / BT de áudio | A tela mostra sempre o valor **relido** (CAP-9). |
| Memória do aparelho carregada | A origem do som aparece como "Memória N do aparelho (app da Taramps)", sem Setup e sem playlist. |

## Interaction Primitives

- **Girar** (arrastar em volta da Roda): escolhe a memória. O som não muda.
- **Tocar no centro da Roda:** aplica a escolhida. É o único gesto que troca o som pela Roda.
- **Segurar o centro da Roda:** abre Memórias.
- **Tocar** em chip, cartão, linha ou botão: age. Um toque na Abertura pula a animação.
- **Arrastar e pinçar no esquema:** arrastar gira, pinçar aproxima e um toque abre o equipamento.
- **Folhas:** sobem do pé. A da dúvida só fecha com uma escolha.
- **Voltar do Android:** [ASSUMPTION] segue a plataforma; o comportamento nas folhas obrigatórias e na aplicação em tela toda não foi decidido.
- **Proibido:** conectar sozinho, aplicar ao soltar a Roda, aplicar sem toque, a IA salvar, a IA ligar chave, mostrar contador de leituras.

## Accessibility Floor

O contraste visual está em `DESIGN.md › Colors`.

- **Alvos de toque:** [ASSUMPTION] mínimo de 48 dp, que é o piso do Android (não foi discutido com o Denis). Os chips e o Cancelar podem ter a aparência menor, mas a área de toque segue esse mínimo.
- **TalkBack:**
  - O spinner tem papel de status, com rótulo do que acontece ("Conectando ao PRO 2.4BT", "Aplicando FILME"), como no mock.
  - A Roda anuncia a memória escolhida e a que está tocando.
  - O botão do centro anuncia "toque para aplicar <memória>".
  - A Roda travada anuncia o motivo ("Som mudo até você escolher", "a IA está ajustando", "sem conexão").
  - [ASSUMPTION] Ainda não foi decidido como escolher a memória sem o gesto de girar.
- **Movimento reduzido** *(delegada)*: a abertura vira um fade de 0,5 s da logo. Os anéis da busca e o anel duplo giram mais devagar, sem pulsos. Nada pisca. O botão do centro continua mostrando que é um botão, mas sem pulsar.
- **Sala escura:** o tema é só escuro e o fundo é preto. O ciano só aparece no que está ativo. Os textos de estado ficam em ≥ 4,7:1 no preto. A exceção é o PARAR IA (3,27:1), que está em Questões em aberto.
- **Honestidade é acessibilidade:** o estado mostrado é o estado relido, e o app nunca mostra um valor que não sabe.

## Responsive & Platform

- Só celular Android, em retrato. Não há tablet, paisagem, iOS ou web (iOS está fora da SPEC).
- Uma conexão BLE por vez: este app e o app da Taramps não ficam conectados juntos.
- A porta da IA funciona só na rede local (Wi-Fi da casa).
- O app não desliga o processador. O ON/OFF é o remote out do módulo, e o processador continua ouvindo o BLE.

## Inspiration & Anti-patterns

**Ficou:**

- **Logo:** o símbolo Onda (opção 2) com o nome Empilhado (formato 1).
- **Abertura:** a animação do nome é a da opção 6, Mergulho (o nome sai do desfoque). O símbolo junta o Neon (10) com a temática do Glitch (6), na versão B: a aura fica e o rosa vira ciano no fim.
- **Spinner:** opção 8, Anel duplo.
- **Tela inicial:** opção 2, Roda.

**Rejeitado:**

- **Radar de feixe girando com rastro** na busca. Foi trocado por anéis saindo do centro.
- **Anéis ciano e rosa alternados** na busca (variação 4).
- **Contador de leituras (1 a X)** ao conectar. Também está fora qualquer número no carregamento.
- **Conectar sozinho**, mesmo o aparelho conhecido. O Denis sempre escolhe.
- **Soltar a Roda para aplicar** (estava na opção 2 original). Agora aplicar é um toque no centro.
- **"+ NOVA" dentro da Roda.** Agora é um botão fora dela.
- **Anel duplo no lugar da logo, dentro da Roda, ao aplicar.** Agora a aplicação ocupa a tela toda.
- **Memórias do aparelho na Roda.** Elas ficam só em Memórias.
- **Modo claro** e **som na abertura**.
- **Logo Cone** (laranja #FF7A1A), escolhido por engano e corrigido para a Onda. Também as logos 1 e 3 a 13 e os formatos de nome 2 a 10.
- **Aberturas 1 a 5** e as animações de símbolo 1 a 9. Do Neon + Glitch, ficaram de fora as versões A (termina limpo) e C (glitch forte).
- **Spinners 1 a 7.**
- **Telas iniciais 1** (Grade), **3** (Agora tocando), **4** (Cinema), **5** (Som e IA juntos), **6** (Tela cheia), **7** (Controle remoto) e **8** (Painel ao vivo).
- **Arquivos só de histórico:** `logo-opcoes-1/2`, `logo-cone-nomes`, `logo-onda-nomes`, `abertura-opcoes-1`, `abertura-simbolo-10`, `abertura-neon-glitch` e `tela-inicial-opcoes-2`.

## Key Flows

### Fluxo 1 — Noite de filme (Denis, no sofá, sala escura, projetor ligado no computador)

1. O Denis pega o celular e abre o BGS Sound. A abertura roda ({motion.opening-total}), e ele toca para pular.
2. A Busca começa: anéis ciano saem da logo. O PRO 2.4BT aparece como ponto e como cartão "Taramps · Casa 2.1 da última vez". Um JBL Flip aparece apagado ("Não é um processador").
3. Ele toca em "Conectar ›". O anel duplo gira com "Conectando ao PRO 2.4BT" até a leitura terminar.
4. A conferência bate com a última memória (MÚSICA HIGH). Entra a aba Som com a MÚSICA HIGH "tocando · aplicada agora".
5. Ele gira a Roda até a FILME. O arco fica branco, o centro vira botão pulsando ("toque para aplicar") e a dica mostra "Tocando: MÚSICA HIGH". O som não muda.
6. Ele toca no centro.
7. **Clímax:** a tela toda escurece. O anel duplo gira grande, com "APLICANDO / FILME" e "Mantenha o celular perto do processador", até o aparelho confirmar. A camada some, e a Roda mostra "FILME · tocando · aplicada agora". O player mostra a playlist da FILME e o chip mostra o último preset dela. O Denis larga o celular e volta para o filme.

Falhas e desvios:
- Se ele girar e não tocar, a Roda volta para a MÚSICA HIGH em 5 s.
- Se a conexão cair durante a aplicação, ao voltar sobe a folha "Terminar de aplicar" / "Voltar à cópia".
- Se a conexão cair depois, entra o estado F ("O aparelho segue tocando o que já tinha").

### Fluxo 2 — Sessão com a IA (Denis no notebook, a IA regulando a FILME pela porta)

1. Na aba IA, o Denis liga a chave "IA pela rede". O aviso do risco aparece antes de ligar.
2. O app mostra o código de 6 dígitos. A IA, no notebook, envia o código uma vez e fica pareada.
3. A IA começa a ajustar. Na aba Som, a Roda trava ("a IA está ajustando"), e a faixa mostra "IA ajustando o EQ da FILME" com a última mudança ("JBL L · 63 Hz −1,5 dB · agora") em até 1 s. Cada mudança vai para o Histórico com a origem "IA".
4. Depois de 3 ajustes seguidos sem um toque dele, uma folha pede "Continuar", com PARAR IA visível. Ele ouve e toca em Continuar.
5. A IA pede para aplicar uma memória. É uma operação de risco, então sobe uma folha de confirmação por cima da aba atual, e ele confirma.
6. Ele gosta do que ouve e toca em **PARAR IA**. O acesso é revogado na hora e os controles voltam.
7. **Clímax:** a IA não salvou nada. O Denis ouve mais uma vez, vai em Ajustes e toca em **Salvar**. A regulagem vai para a FILME. Como as 15 bandas mudaram, a folha pergunta se ele quer atualizar o preset em uso ou criar um novo. Ele escolhe, e o spinner pequeno gira em "Salvando".

Falhas e desvios:
- Se ele não gostar, PARAR IA e não salva. [ASSUMPTION] Desfazer o que a IA mudou sem salvar não está definido; reaplicar a memória pela Roda seria o caminho existente.
- Uma chave desligada recusa o pedido da IA antes do envio.
- Na dúvida do Setup, a IA fica bloqueada.

### Fluxo 3 — Primeira conexão / Setup em dúvida (Denis, primeira abertura, Casa 2.1 herdado)

1. O Denis abre o app pela primeira vez. O Setup Casa 2.1 já está lá, com equipamentos e fichas herdados (CAP-6), mas sem memórias e sem presets.
2. Ele conecta ao PRO 2.4BT pela Busca (Fluxo 1, passos 2 e 3). Na primeira vez, digita a senha, que fica guardada.
3. A conferência não tem memória para comparar, então cai na dúvida. Os 4 canais ficam mudos no aparelho. O chip mostra "Setup em dúvida", a Roda trava com "MUDO / até você escolher" e a IA fica bloqueada.
4. A folha "Som mudo até você escolher" mostra o que não bate e as 3 escolhas. [ASSUMPTION] O texto para "sem memória para comparar" não foi decidido.
5. Ele toca em **Escolher um Setup** e escolhe o Casa 2.1.
6. **Clímax:** o som volta. Como o Casa 2.1 não tem memória, o app desmuta sem aplicar nada (FM 5; ver A11). O chip mostra "Casa 2.1 ›", e a aba Som mostra a Roda sem memórias, com "+ Nova memória".

Desvios:
- Com **Criar um Setup novo**, o som do aparelho vira a 1ª memória do Setup novo.
- Com **Seguir sem Setup**, as memórias ficam travadas, as saídas aparecem como CH1 a CH4 e os links ficam desligados.
- Tocar no chip do Setup depois volta a essas escolhas.

### Fluxo 4 — Levar o processador para o carro (Denis, PRO 2.4BT instalado no carro)

1. No carro, o Denis abre o app e conecta ao PRO 2.4BT.
2. A conferência compara o aparelho com a última memória aplicada do Casa 2.1. O corte do Sub e o atraso diferem, então cai na dúvida: o som fica mudo no aparelho e a IA bloqueada.
3. A folha lista o que não bate ("Atraso do Sub: 80 cm na FILME, 0 cm no aparelho", …).
4. Ele toca em **Criar um Setup novo** e dá o nome "Carro". [ASSUMPTION] A tela de nome não foi desenhada.
5. **Clímax:** o som do aparelho vira a 1ª memória do Carro. O som volta, o chip mostra "Carro ›" e a Roda mostra essa memória tocando.
6. Depois, na aba Setup, ele cadastra os equipamentos do carro, e o esquema isométrico se atualiza sozinho.
7. De volta em casa, a conferência cai na dúvida de novo. Ele escolhe o Casa 2.1, e o app reaplica a memória ativa só depois dessa confirmação.

Em aberto: A10 (a 1ª memória fica fiel ao link do aparelho ou ao link do Setup novo).

### Fluxo 5 — Refazer a MUSICA ORIG (Denis, teste 1 da SPEC)

1. Com o Casa 2.1 em uso, o Denis toca em "+ Nova memória". A memória nasce do som atual e ele dá o nome MUSICA ORIG.
2. O aviso confirma: "Memória MUSICA ORIG criada no Casa 2.1."
3. Em Ajustes, ele regula as saídas e os globais; cada mudança é escrita e relida. Depois toca em **Salvar**.
4. Ele desliga e religa o processador, abre o app e conecta.
5. **Clímax:** a conferência bate (ou o app reaplica depois de confirmar o Setup). A Roda mostra "MUSICA ORIG · tocando", com o som igual ao da memória.

Falha: se o aparelho voltar com corte, atraso, fase ou rota diferentes, cai na dúvida (Fluxo 3) e nada toca sem a escolha dele.

### Fluxo 6 — Conferir contra o app da Taramps (Denis, teste 3 da SPEC)

1. O Denis segura o centro da Roda e abre Memórias.
2. Na seção "No aparelho (app da Taramps)", ele escolhe a FILME. Uma folha pede confirmação, e a aplicação em tela toda mostra "Aplicando" + o nome da memória.
3. A origem do som passa a "Memória N do aparelho (app da Taramps)", sem playlist.
4. Ele sai do BGS Sound (uma conexão BLE por vez), abre o app da Taramps e compara os valores com os de Ajustes. [ASSUMPTION] Como desconectar pelo BGS Sound não foi desenhado.
5. **Clímax:** todos os valores batem. Ao voltar ao BGS Sound, o app relê tudo antes de qualquer ação.

### Fluxo 7 — Medir com o gerador (Denis, conferindo o voltímetro contra o multímetro VC830L)

1. Em Ajustes › Medir, o Denis vê o voltímetro de cada saída e da entrada, e o clip por canal, ao vivo.
2. Para um tom de teste, ele abre o Gerador. A chave do gerador está desligada. Ao ligar, aparece antes o aviso "Um tom forte pode queimar o tweeter ou o sub".
3. Ele escolhe a frequência e o ganho e toca. A contagem corre até 30 s.
4. **Clímax:** ao fim da contagem, ou quando ele toca em parar, o gerador desliga, e a releitura confirma o desligado.

Falha: se a conexão cair com o gerador ligado, o app tenta reconectar 3 vezes. Sem sucesso, a tela toda mostra "DESLIGUE O MÓDULO AGORA". Ao reconectar, o gerador é desligado antes de qualquer outra ação.

Ainda sem fluxo próprio: "Montar um Setup" (só o passo 6 do Fluxo 4), "Ouvir uma memória com a sua playlist" (Player) e "Trazer um preset antigo da Taramps" (depende de A5).

## Questões em aberto

**Da spec (ambiguidades da extração):**

1. **A1, nome da IA:** a interface diz só "IA". O nome **KARINA** fica em aberto para o Denis.
2. **A3, referência da conferência:** "última memória aplicada no último Setup usado" × "naquele Setup" × "+ o link do Setup". Não está dito se um link diferente muta ou só avisa, nem como "memória ativa" difere de "última memória aplicada".
3. **A4, Continuar × controles travados:** com o padrão "travado até PARAR IA", o único toque possível é o Continuar ou o PARAR IA. Também não está dito se aplicar preset ou memória contam entre os 3 ajustes. Falta definir também o que o **Pausar** da faixa faz nesse padrão.
4. **A5, preset antigo da Taramps:** salvar como preset (15 bandas) ou como memória? Um preset da Taramps traz mais que 15 bandas.
5. **A7, rótulos:** separar "EQ de entrada" (memória), "EQ global" das notas e as 15 bandas (preset); e listar o que fica fora das camadas.
6. **A8, limites:** a interface mostra algum alerta (ex.: gerador acima de −13 dB)? A POC não tem teto.
7. **A9, memória do aparelho carregada × Setup:** dá para salvar esse som como memória do celular do Setup atual? E como isso convive com "seguir sem Setup trava as memórias"?
8. **A10, Setup criado do aparelho:** a 1ª memória fica fiel ao som (link do aparelho) ou ao Setup novo (link desligado)?
9. **A11, Setup sem memória:** a 1ª conexão sempre muta, e o desmute toca o que estiver no aparelho, sem referência. É essa a experiência desejada?
10. **A14, avisos customizados (CAP-14):** quantos e quais o redesign deve ter? A meta mínima de 1 aviso foi da spec, não do Denis.

A2 (vale o padrão "travado"), A6 (a IA não salva), A12 (notas sem tom de recomendação) e A13 (botão "Salvar") estão resolvidas acima. A15 é só histórico.

**Do diário:**

11. "Todas opções" (resposta do Denis sobre a tela inicial) ficou ambíguo: as telas iniciais estariam todas disponíveis no app, ou o spinner vale para todos os carregamentos? O spinner em todo carregamento já é decisão. A primeira leitura (várias telas iniciais) não foi confirmada.

**[ASSUMPTION] usados nesta spine (confirmar):**

12. A fonte é a do sistema. Os tamanhos dos mocks valem como sp, e os pequenos (9,5 a 11) ainda precisam ser conferidos na sala escura.
13. A posição exata do chip "Preset: … ›" na aba Som. As abas sem ícones.
14. Cancelar na conexão volta para a Busca. O chip do Setup é o "nome do Setup" que reabre a escolha. O chip da IA leva à aba IA.
15. Telas e estados sem desenho:
    - Busca: BT desligado/permissões, nenhum aparelho encontrado, BLE ocupado, pedido de senha e senha errada.
    - Som: Roda sem memórias, nenhum preset, playlist vazia e mini player de memória do aparelho.
    - Aplicação e conferência: aviso de diferença leve com Reaplicar, folha da dúvida sem memória para comparar, feedback ao aplicar preset (tela toda ou spinner pequeno) e nome ao criar memória ou Setup.
    - Faixa de só leitura.
16. Componentes sem visual: interruptor de chave, selo de origem, esquema isométrico, medidores e clip ("ERRO 0"), gerador com contagem, alerta "DESLIGUE O MÓDULO AGORA", avisos (CAP-14), e as telas Memórias, Player, Presets, Ajustes, IA, Setup, Aparelho e Histórico.
17. Tocar no centro quando a escolhida já é a que está tocando: comportamento não decidido.
18. Alvos de toque ≥ 48 dp; alternativa à rotação da Roda no TalkBack; velocidade exata "mais devagar" no movimento reduzido; voltar do Android nas folhas obrigatórias e na aplicação em tela toda; como desconectar para usar o app da Taramps; como desfazer as mudanças da IA sem salvar.
19. **Mudanças não salvas:** o que acontece se o Denis mexer em Ajustes e trocar de memória ou sair sem Salvar não foi decidido. Também não foi decidido se apagar uma memória ou um preset pede confirmação.
20. ~~**Contraste do PARAR IA**~~ resolvido em 30/10: o fundo passou a #D92D20 (`{colors.stop}`), branco sobre ele dá 4,8:1.
