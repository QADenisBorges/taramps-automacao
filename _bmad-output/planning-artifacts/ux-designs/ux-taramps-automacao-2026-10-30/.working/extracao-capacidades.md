# Extração de capacidades: BGS Sound (app de controle do PRO 2.4BT + AUDIO)

Extração, não desenho. Fontes: SPEC.md e companheiros em `_bmad-output/specs/spec-app-controle-taramps/`, proposta de mudança de 29/10/2026 (`planning-artifacts/sprint-change-proposal-2026-10-29.md`) e `stories.yaml` (só para o status). O UX antigo não foi lido.

Siglas das fontes: **SPEC** = SPEC.md · **CHA** = chaves-de-acesso.md · **POR** = porta-de-comandos.md · **FM** = failure-modes.md · **HER** = conhecimento-herdado.md · **PLA** = plano-resto-poc.md · **DIA** = architecture-diagrams.md · **P29** = proposta de 29/10 · **ST** = stories.yaml.

Sobre a proposta de 29/10: ela já foi aplicada na SPEC (frontmatter da SPEC a cita; P29 status "aprovada e aplicada"). Onde um documento ainda carrega o modelo antigo, vale a P29; os casos estão na seção 6.

## 1. Glossário

- **Setup:** uma instalação física (o 2.1 de casa, o carro…), sempre com o mesmo PRO 2.4BT. **Só o físico**: nome e equipamentos com as fichas e as ligações; não guarda regulagem (SPEC CAP-16; P29 §1, D1).
- **Equipamento / grupos:** o que um canal alimenta por cabo direto, Y ou divisor. Grupo 1 amplifica e toca (caixa/sub ativos); 2 amplifica para passivos (módulo, com canais e bridge); 3 processa ou distribui e repassa; 4 só recebe. Alto-falantes passivos ficam nas saídas ou no bridge do grupo 2. Categorias abertas e **função acústica** (SPEC CAP-16).
- **Ficha:** dados técnicos opcionais do equipamento, cada um com **origem**: oficial (com endereço), outra fonte, medido ou digitado (SPEC CAP-16; P29 §5.1).
- **Base de modelos:** fichas confirmadas guardadas no celular por fabricante e modelo; reaproveitadas e autocompletadas ao digitar (SPEC CAP-16).
- **Memória (do celular):** pertence a um Setup e guarda **toda a regulagem com o valor final** (atraso, fase, cortes, rota, EQ de entrada, ganho, limitador, EQ de saída, MASTER), a playlist e o último preset usado. Quantidade livre (SPEC CAP-4; P29 D2).
- **Memória do aparelho:** uma das 3 memórias internas do processador (hoje SPL, MUSICA ORIG, FILME). Este app só lê os nomes e carrega (`241`); intocável e "não é referência" (SPEC CAP-4, Non-goals; HER §3 memórias).
- **Preset:** só o EQ gráfico de **15 bandas** (gênero ou conteúdo: Rock, Sertanejo, Ação, Diálogo…), guardado no celular, aplicado por cima da memória (SPEC CAP-5).
- **Camadas:** as três camadas do som: Setup (físico, de onde sai o link) → memória (regulagem) → preset (15 bandas). "Salvar por camadas" manda cada mudança à camada dona dela (SPEC CAP-4, CAP-14; P29 §5.5).
- **Regulagem:** tudo o que a memória guarda; termo que substitui "calibração + caráter" (P29 §5.1).
- **Calibração (obsoleto):** antes de 29/10, a parte do Setup (atraso, fase, corte, equilíbrio…). Não existe mais (P29 §5.1, §6).
- **Caráter (obsoleto):** antes de 29/10, a parte da memória (limiter, EQ de saída, nível). Absorvido pela "regulagem" (P29 §5.1).
- **Equilíbrio (obsoleto):** ajuste por saída do Setup somado ao ganho/limiter da memória. Saiu (P29 D2).
- **Saída:** o que se ajusta na tela: um canal do processador, ou um par vizinho com link, que vira uma saída só e escreve nos dois. Nome vem dos equipamentos alimentados (sem equipamento: CH1 a CH4); cada canal tem cor (SPEC CAP-3, CAP-16, CAP-17).
- **Link (1–2, 3–4):** automático, tirado das ligações: dois canais vizinhos no mesmo bridge ficam com link. O usuário não mexe (SPEC CAP-3, CAP-16; P29 D1).
- **Conferência ao conectar / Setup em dúvida:** comparação do que foi lido com a última memória aplicada; na diferença grave, muta e pede escolha (SPEC CAP-16; FM 5).
- **Memória ativa / última memória aplicada:** a referência da conferência e da reaplicação (FM 5; SPEC CAP-16). Ver ambiguidade A3.
- **Chave de acesso:** liga/desliga por operação de risco, com padrão de fábrica e aviso do risco ao ligar (SPEC CAP-11; CHA).
- **Porta de comandos:** entrada HTTP no Wi-Fi da casa pela qual a IA externa comanda o app (SPEC CAP-12; POR).
- **IA / cliente IA:** o Claude Code no notebook, na mesma rede; não há IA dentro do app (SPEC Assumptions, Constraints). **KARINA não aparece na spec**: é o nome da IA no painel/vitrine (fora destes documentos). Ver A1.
- **Pareamento:** código de 6 dígitos mostrado no app; o cliente o envia uma vez e recebe chave própria (POR trava 2).
- **Parar IA:** botão que revoga o acesso da IA na hora (POR trava 4).
- **Continuar:** confirmação na tela exigida após 3 ajustes seguidos da IA sem toque do usuário (CHA; P29 D4).
- **Cópia antes:** estado guardado antes de operação de muitos parâmetros; base do "aplicado pela metade" (FM 3).
- **Modo só leitura:** firmware diferente de R003; nada é escrito e a porta só responde leituras (SPEC CAP-1; POR).
- **Remote out:** o ON/OFF do módulo (`252`); não existe comando para desligar o processador (SPEC Constraints).
- **Gerador:** tom fixo e sweep do processador (SPEC CAP-8).
- **Player / playlist:** arquivos de áudio do celular, uma playlist por memória, tocados pelo Bluetooth de áudio (SPEC CAP-15).
- **Casa 2.1:** o Setup herdado: módulo SD800.4 EVO; JBL L (CH1), JBL R (CH2), "Sub" Protech Light 8" no bridge CH3+CH4 (HER Equipamentos).
- **Rendimento / Agente da memória / guardião completo:** specs futuras, fora da POC (SPEC Non-goals; P29 §3).

## 2. Capacidades (CAP-n)

Status: **feita (aparelho)** = conferida no processador real; **feita (simulador)** = no código e testada contra o processador simulado, falta a conferência no aparelho (ST 12b/12c); **planejada**; **fora da POC**. Todas as histórias até a 10c estão no `main` (git log); faltam a 12b e a 12c (ST).

- **CAP-1 Conectar e autenticar.** Conectar pelo Bluetooth do celular, autenticar com a senha e ver o firmware ("R003"). Necessidade: falar direto com o processador sem o app da Taramps. **Feita (aparelho)** (ST 1, 12a). Regras: senha errada recusa sem nenhuma escrita; firmware ≠ R003 → só leitura com aviso; a senha é digitada uma vez, guardada, e nunca aparece na tela, em log ou na porta (SPEC CAP-1, Assumptions); uma conexão BLE por vez (SPEC Constraints).
- **CAP-2 Ver o estado completo.** Ver todos os parâmetros do MAPA §4, gerador, monitoração e os nomes das 3 memórias do aparelho (só para escolher qual carregar). Necessidade: confiar que o que o app mostra é o que está no aparelho. **Feita (aparelho)**: 84 de 84 em ~13 s, paridade com o app da Taramps (SPEC CAP-2; HER Protocolo; ST 12a). Regra: cada valor bate com o app da Taramps, inclusive após carregar a FILME do aparelho.
- **CAP-3 Regular os parâmetros de áudio.** Mexer em tudo do MAPA §4, por saída e globais. Necessidade: regular o 2.1 só pelo celular. **Feita (simulador)** (ST 5, 10, 10c). Regras: cada escrita é relida e confere dentro do passo; as saídas são as do Setup; o link não é controle do usuário; no Casa 2.1 o Sub é uma saída e escreve no CH3 e no CH4 (SPEC CAP-3); sem Setup em uso, links 1–2 e 3–4 desligados (ST 11, decisão de 24/10).
- **CAP-4 Memórias do celular.** Criar, aplicar, renomear e apagar memórias de um Setup; carregar uma das 3 memórias do aparelho. Necessidade: guardar e trocar "sons" completos sem depender do app da Taramps. **Feita (simulador)** (ST 6, 8c, 8f, 10c). Regras: memória nova começa do som atual; **Salvar é um passo à parte, um só botão**, que reparte por camada (regulagem → memória; 15 bandas → preset); aplicar = escrita ao vivo com cópia antes e releitura; aplicar traz de volta o último preset da memória; o app nasce **sem memórias e sem presets do celular** (P29 D2); a memória do aparelho carregada aparece como **"Memória N do aparelho (app da Taramps)"**, sem Setup e sem playlist; **nunca** sai `240` (salvar) nem `23N` (renomear) do app (SPEC CAP-4). Chaves: "Aplicar memória do celular" e "Carregar memória do aparelho" ligadas, com confirmação na tela se pedidas pela IA (CHA).
- **CAP-5 Presets de 15 bandas.** Aplicar, renomear e apagar presets; ao salvar 15 bandas mexidas, escolher entre atualizar o preset em uso (vale para toda memória que o usa) ou criar um novo. Necessidade: trocar o "gosto" por gênero/conteúdo sem mexer na regulagem. **Feita (simulador)** (ST 8, 8d, 11). Regras: trocar preset muda só as 15 bandas; sem preset herdado (P29 §5.1 "CAP-5"); aplicar preset tem cópia antes (FM 3) mas não pede confirmação (CHA).
- **CAP-6 Conhecimento herdado.** Abrir o app pela primeira vez e já ter o Setup "Casa 2.1" (equipamentos e fichas com origem), as notas das memórias do aparelho e as preferências do Denis. Necessidade: não digitar o que já se sabe. **Feita (simulador)** (ST 8, 8f). Regras: nada de memórias/presets herdados; memórias do aparelho intocáveis e não são referência (SPEC CAP-6; HER; P29 D2).
- **CAP-7 Monitoração ao vivo.** Ver voltímetro e clip das saídas e da entrada. Necessidade: saber se algo ceifa e quanta tensão chega. **Feita (simulador)**; conferência contra o multímetro VC830L com foto na 12b (ST 7b, 11, 12b). Regra: o clip aparece no canal certo (SPEC CAP-7). O voltímetro pode responder "ERRO 0" (PLA §2).
- **CAP-8 Gerador de sinal.** Tocar tom fixo e sweep, na frequência e ganho escolhidos, e parar. Necessidade: testes e medições. **Feita (simulador)**; no aparelho só com o RCA dos módulos desconectado (ST 7a, 12b). Regras: chave **desligada** por padrão ("Um tom forte pode queimar o tweeter ou o sub"); prazo máximo de 30 s; releitura confirma desligado (CHA; FM 1–2).
- **CAP-9 Módulo e Bluetooth de áudio.** Ligar/desligar o módulo (remote out) e o BT de áudio. Necessidade: cortar ou religar o som. **Feita (simulador)** (ST 7b, 11). Regras: a tela reflete o estado **relido**; remote out ligado por padrão mas com confirmação se vier da IA ("Liga ou corta o som de repente"); BT de áudio sem confirmação (CHA).
- **CAP-10 Comandos de aparelho inteiro.** Reset de fábrica, reset de áudio, troca de senha e nome do produto. Necessidade: paridade com o app da Taramps. **Feita (simulador)**; no aparelho, reset de fábrica, reset de áudio e troca de senha testados **só na recusa** (ST 7b, 12b). Regras: cada um só roda com a sua chave; depois relê e mostra o resultado (SPEC CAP-10). Riscos: "Apaga a senha e as 3 memórias" / "O estado resultante é imprevisível" / "Uma senha perdida tranca o aparelho" (CHA).
- **CAP-11 Chaves de acesso.** Ligar ou desligar cada operação de risco, orientado pelo aviso do risco. Necessidade: proteger o som e o aparelho de erro humano ou da IA. **Feita (simulador)** (ST 5, 11). Regras: chave desligada recusa **antes** de qualquer envio BLE, pela tela e pela IA; aviso do risco **antes** de ligar; a IA nunca liga/desliga chave; padrões na tabela de CHA (desligadas: reset de fábrica, IA pela rede, reset de áudio, troca de senha, gerador).
- **CAP-12 Porta de comandos da IA.** Deixar a IA (Claude Code no notebook) ler, ajustar e aplicar memória pelo Wi-Fi. Necessidade: a IA regular junto sem caminhos indiretos (adb). **Feita (simulador)**; Wi-Fi real na 12c (ST 9b, 12c). Regras: chave "IA pela rede" desligada por padrão; pareamento de 6 dígitos; só rede local; tudo visível e no histórico; risco pede toque na tela; **Continuar** a cada 3 ajustes seguidos sem toque (P29 D4, provisório); senha nunca trafega; firmware ≠ R003 → só leituras (POR; CHA).
- **CAP-13 Estado compartilhado e histórico.** Ver na tela, em até 1 s, o que a IA muda (e vice-versa) e consultar o histórico de todo comando. Necessidade: confiança e rastreio de quem mexeu. **Feita (simulador)** (ST 9a, 11). Regras: histórico com origem (tela/IA), hora e valor; faixa "IA ajustando…"; cada mudança da IA vira aviso; uso simultâneo é **opção nas configurações da IA**, com padrão "controles travados até PARAR IA" e alternativa "o toque pausa a IA" (janela de 10 s; recusa "o usuário está mexendo nisso") (SPEC CAP-13; POR §Uso ao mesmo tempo).
- **CAP-14 Avisos e interações próprias.** Receber avisos customizados que o app da Taramps não tem. Necessidade: entender o que aconteceu. **Feita (simulador)** (ST 6, 10). Exemplos verbatim: "Memória MUSICA ORIG criada no Casa 2.1."; a folha do "Salvar por camadas" (SPEC CAP-14). Meta mínima (1 aviso) foi fixada pela spec, não pelo Denis (SPEC Assumptions).
- **CAP-15 Player por memória.** Montar uma playlist por memória do celular e tocar pelo app (tocar, pausar, pular), com o som indo por Bluetooth ao processador. Necessidade: paridade com o Player da Taramps; ouvir a memória com a música certa. **Feita (simulador)** (ST 8b, 8d, 11). Regras: aplicar a memória mostra a playlist dela; faixa cujo arquivo sumiu aparece **indisponível**; se o BLE cai, o Player continua tocando a playlist da memória em uso (ST 11, decisão de 24/10); memória do aparelho não tem playlist (SPEC CAP-4).
- **CAP-16 Setups, equipamentos e conferência.** Cadastrar Setups (nome, equipamentos, ligações, fichas com origem), reaproveitar modelos da base e, ao conectar, conferir o aparelho contra a última memória aplicada. Necessidade: o mesmo processador viaja entre instalações (casa, carro) sem tocar som errado. **Feita (simulador)** (ST 8c, 8e, 8f, 10, 10c). Regras: dado sem origem confirmada **nunca** aparece como verificado; desconhecido fica em branco (modelo desconhecido = genérico); link automático; conferência em 3 resultados (bate → entra; só ganho/limitador/EQ/MASTER → avisa e oferece Reaplicar, sem mutar; corte/atraso/fase/rota ou sem memória → para o som, muta os 4 canais no aparelho, IA bloqueada, lista "o que não bate" e pede: **escolher um Setup**, **criar um Setup novo** (o som do aparelho vira a 1ª memória dele) ou **seguir sem Setup**); tocar no nome do Setup volta a essa escolha (SPEC CAP-16; FM 5; P29 D3). Limites físicos pelas fichas: fora (Rendimento).
- **CAP-17 Esquema do Setup.** Ver o Setup num esquema 2D isométrico (profundidade, sombra) que se atualiza sozinho; arrastar gira, pinça aproxima, toque num equipamento abre para ver/editar. Necessidade: entender e montar as ligações sem planilha. **Feita (simulador)** (ST 10b). Regras: desenho pela categoria e função acústica, em proporção pelas características; mostra processador com 4 canais, cabos e Y, grupos, saídas e bridges, alto-falantes e cor de cada canal; função acústica sugerida pela categoria e pelo corte do canal **na memória em uso** (P29 §5.1), trocável; 3D de verdade fora (SPEC CAP-17).
- **Fora da POC:** IA dentro do app; o agente que cria a memória (perguntas adaptativas, escuta "1 ou 2", porta nos dois sentidos com modal, serviço que acorda o agente, 7 skills, aprendizado, ouvintes simulados); guardião completo com limites por alto-falante; editar/salvar/renomear memórias do aparelho; tocar sem o celular; Rendimento (níveis Baixo a Extra, análise da playlist, teste a seco, teto por preset); microfone e dados profissionais (POC 2); tensão por saída (POC 3); firmware; internet; iOS; ler `.trps`; marca e telas finais do BGS Sound Lab (SPEC Non-goals; P29 §3, §5.7).

## 3. Regras de segurança e de honestidade para a interface

- **Recusa antes do envio:** chave desligada = nada sai pelo BLE, pela tela ou pela IA (CHA; DIA "Caminho de um comando").
- **Aviso do risco antes de ligar uma chave**, com o texto do risco da tabela (CHA).
- **Risco pedido pela IA espera toque na tela** (aplicar memória, carregar memória do aparelho, remote out, gerador, resets, senha) (CHA; POR trava 5).
- **Continuar a cada 3 ajustes seguidos da IA** sem toque do usuário; ajuste avulso segue sem confirmação; o 3 é provisório (CHA; P29 D4).
- **A IA nunca liga nem desliga chave** (CHA). **A IA não salva** memória nem preset; no futuro o modal mostraria o Salvar do usuário (P29 §5.7). Ver A6.
- **PARAR IA sempre à mão**; o usuário nunca fica sem saída (POR).
- **Gerador:** prazo máximo de 30 s; ao fechar, perder conexão ou errar, manda desligar; sem sucesso, 3 tentativas de reconectar e então em destaque **"DESLIGUE O MÓDULO AGORA"** (FM 1).
- **Ao reconectar, relê tudo primeiro**; gerador ligado é desligado antes de qualquer outra ação (FM 2).
- **Cópia antes** de aplicar memória, preset ou carregar memória do aparelho; se cair no meio, ao voltar mostra **"aplicado pela metade"** com **terminar** ou **voltar à cópia** (FM 3).
- **Mudo da dúvida:** com corte, atraso, fase ou rota diferentes (ou sem memória para comparar), os 4 canais ficam mudos **no aparelho** até a decisão, mesmo se o app fechar ou a conexão cair; IA bloqueada; nada toca sem confirmação (FM 5; SPEC CAP-16, teste 4).
- **Só avisa sem mutar** quando difere apenas ganho, limitador, EQ ou MASTER (P29 D3).
- **Senha nunca na tela, em log ou na porta** (SPEC Assumptions; POR).
- **Nunca mostrar o que o app não sabe:** dado de ficha sem origem confirmada nunca aparece como verificado; o desconhecido fica em branco; modelo desconhecido fica genérico (SPEC CAP-16; HER Equipamentos).
- **Estado mostrado = estado relido:** cada escrita é relida (SPEC CAP-3, CAP-9, CAP-10); depois de usar o app da Taramps, reler (HER Cuidados).
- **Firmware ≠ R003 → só leitura**, com aviso; a porta só responde leituras (SPEC CAP-1; POR).
- **Memórias do aparelho intocáveis:** nunca `240` nem `23N`; carregar troca todo o som de uma vez (SPEC CAP-4; CHA).
- **Limites de volume/ganho:** a POC **não** define teto em código. Só existem as chaves; o guardião completo e o Rendimento ficam fora; os limites de `estado/guardiao.json` são provisórios e "não são regra nem referência" (SPEC Non-goals; HER Sistema). Conhecimento de operação, não regra do app: gerador só em −13 dB, ou 0 dB **sem os alto-falantes**; o BT entra ~13 dB abaixo do gerador (HER Sistema). Ver A8.
- **Uma conexão BLE por vez:** o app da Taramps e este não ficam juntos (SPEC Constraints; DIA).
- **Não há "desligar o processador":** o ON/OFF é o remote out e o processador continua ouvindo o BLE (SPEC Constraints).
- **Sem o celular, o processador toca o que estiver nele** (limitação conhecida); se ele perdeu o estado, o app reaplica a memória ativa só depois de confirmar o Setup (SPEC Non-goals; FM 5).
- **Seguir sem Setup trava as memórias do celular** até escolher um Setup (FM 5); sem Setup, links desligados (ST 11).

## 4. Estados de conexão e de dados a representar

- **Bluetooth do celular:** desligado / sem permissão (pedido de ligar o BT e de permissões) (PLA §1 `IPreparoBluetooth`).
- **Desconectado** · **procurando/conectando** · **BLE ocupado** pelo app da Taramps (uma conexão por vez) (SPEC Constraints).
- **Autenticando:** senha pedida na 1ª vez, depois guardada; **senha errada** ("ERRO") (SPEC CAP-1; HER Protocolo).
- **Firmware desconhecido → modo só leitura** (faixa de aviso) (SPEC CAP-1).
- **Lendo:** leitura completa (~13 s, 84 parâmetros) após conectar e ao reconectar (HER Protocolo; FM 2).
- **Conferência:** bate (entra direto) / diferença leve (aviso + Reaplicar, som tocando) / **Setup em dúvida** (mudo no aparelho, IA bloqueada, lista do que não bate, 3 saídas) / sem memória para comparar (cai na dúvida; 1ª conexão do Casa 2.1) (SPEC CAP-16; FM 5; P29 nota da aplicação).
- **Sem Setup em uso** (seguiu sem Setup): memórias do celular travadas, links desligados, saídas CH1–CH4 (FM 5; ST 11).
- **Processador perdeu o estado** → reaplicar a memória ativa após confirmação (FM 5).
- **Conectado ao vivo / ocioso.** Origem do som atual: memória do celular (nome + Setup + preset), "Memória N do aparelho (app da Taramps)", ou som sem memória (SPEC CAP-4).
- **Escrevendo:** escrita com eco e releitura (30 ms entre escritas, 300 ms por parâmetro); releitura diferente do pedido = erro (PLA §3 história 5; SPEC CAP-3).
- **Aplicando** memória/preset/memória do aparelho (com cópia antes) · **aplicado pela metade** (terminar / voltar à cópia) (FM 3).
- **Conexão caiu / reconectando** (até 3 tentativas quando o gerador ficou ligado) · **"DESLIGUE O MÓDULO AGORA"** (FM 1).
- **Gerador tocando** com contagem até 30 s / desligado confirmado (FM 1; SPEC CAP-8).
- **Módulo ligado/desligado** (remote out) e **BT de áudio ligado/desligado**, sempre pelo valor relido (SPEC CAP-9).
- **Monitoração:** voltímetro por saída e entrada, **clip** por canal; voltímetro pode vir "ERRO 0" (SPEC CAP-7; PLA §2).
- **Chave:** ligada / desligada / pedido recusado por chave desligada (CHA).
- **IA:** porta desligada / pareando (código de 6 dígitos na tela) / pareada / **ativa** (faixa "IA ajustando o EQ da FILME") / pausada pelo toque (janela de 10 s) / esperando **confirmação de risco** / esperando **Continuar** / bloqueada (dúvida) / parada (PARAR IA) / pedido recusado (sem chave, fora da rede, "o usuário está mexendo nisso") (POR; CHA; FM 5).
- **Histórico:** cada comando com origem, hora e valor (SPEC CAP-13).
- **Dados de memória/preset:** nenhuma memória (estado inicial do Casa 2.1) / nenhum preset / memória com último preset lembrado / preset compartilhado por várias memórias (SPEC CAP-4, CAP-5).
- **Player:** tocando / pausado / faixa **indisponível** (arquivo sumiu) / playlist vazia / sem playlist (memória do aparelho) / tocando com BLE caído (SPEC CAP-15; ST 11).
- **Ficha:** dado com selo de origem (oficial, outra fonte, medido, digitado) / sem origem confirmada (nunca "verificado") / em branco / modelo genérico / autocompletado pela base (SPEC CAP-16; P29 §5.5).
- **Setup:** com ou sem equipamentos (sem equipamento, saídas CH1–CH4); par com link = uma saída (SPEC CAP-16).

## 5. Jornadas e momentos de uso

Quem: **Denis** (único usuário, dono do 2.1); **a IA** (Claude Code no notebook, Wi-Fi da casa); o **app da Taramps** disputa a mesma conexão (SPEC Assumptions; DIA). Onde: a sala do 2.1 (L a 3,20 m, sub a 2,40 m, R a 2,20 m; ouvinte 0,9 m à direita da R) e o carro (HER Sistema; SPEC CAP-16).

- **Teste 1, refazer a MUSICA ORIG:** o Denis recria a MUSICA ORIG como memória do Casa 2.1 só pelo celular; desliga e religa o processador; ao conectar, o som fica igual (SPEC Success 1).
- **Teste 2, a IA pela porta:** a IA lê tudo, muda um ajuste e aplica uma memória, com a tela ao vivo (SPEC Success 2).
- **Teste 3, paridade com o app da Taramps:** cada valor igual ao app da Taramps, no estado atual e depois de carregar a FILME do aparelho (SPEC Success 3).
- **Teste 4, aparelho diferente da memória:** com corte, atraso, fase ou rota diferentes, nada aplica nem toca sem confirmação (SPEC Success 4).
- **Primeira abertura:** Casa 2.1 pronto, sem memórias nem presets; 1ª conexão cai na dúvida (sem memória) (SPEC CAP-6; P29 nota).
- **Levar o processador para outro Setup (o carro):** conectar, cair na conferência, escolher ou criar Setup; "Criar Setup novo" guarda o som do aparelho como 1ª memória (SPEC CAP-16; FM 5).
- **Montar um Setup:** cadastrar equipamentos e ligações antes da memória; a ficha que a IA não achar o usuário digita (P29 §1, D1).
- **Regular junto com a IA:** sessão de regulagem com a IA comandando e o Denis acompanhando, pausando ou parando (POR §Uso ao mesmo tempo).
- **Ouvir uma memória com a sua playlist** (SPEC CAP-15).
- **Medir:** voltímetro contra o VC830L, com foto marcada; gerador só com o RCA dos módulos desconectado (HER Preferências; ST 12b).
- **Trazer um preset antigo** da Taramps: carregá-lo pelo app da Taramps e salvar a leitura no celular (SPEC Non-goals; HER Presets). Ver A5.
- **Método do Denis:** avalia voz, grave, detalhes e envolvimento juntos e dá notas; confere posição, tempo, fase e LFE antes do EQ; um passo por vez, cada um com o que conferir e o que responder (HER Preferências).
- **Nomes de fluxos do UX antigo citados pela P29** (só os nomes, não o conteúdo): Fluxo 2, Fluxo 3 "noite de filme", Fluxo 5 "conferir contra o app da Taramps", Fluxo 7 "carro" (P29 §5.5).
- **Futuro (fora):** o agente que cria a memória por perguntas e escuta "1 ou 2" (P29 §3).

## 6. Ambiguidades e conflitos

- **A1, nome da IA e do app.** A spec só diz "a IA" / "cliente IA"; "KARINA" não aparece em nenhum destes documentos (só no painel/vitrine). O app não tem nome na spec ("BGS Sound" não aparece; a história 1 o chamou de "BGS Controle"; a SPEC diz que marca e telas do BGS Sound Lab estão fora). Decidir se a interface nomeia a IA e o app.
- **A2, uso ao mesmo tempo: SPEC × POR.** SPEC CAP-13 dá como regra "um toque na tela pausa a tarefa da IA"; POR diz que isso é a **alternativa**, e o **padrão** é "controles travados até PARAR IA". Vale POR (decisão de 21/10, mais específica).
- **A3, referência da conferência.** FM 5: "última memória aplicada no último Setup usado"; SPEC CAP-16: "última memória aplicada naquele Setup"; P29 §5.1 acrescenta "e o link do Setup". Não está dito se um link diferente muta ou só avisa, nem como "memória ativa" (FM, reaplicar) difere de "última memória aplicada".
- **A4, Continuar × controles travados.** O Continuar exige "um toque do usuário no meio"; com o padrão "controles travados até PARAR IA", o único toque possível seria o próprio Continuar ou PARAR IA. Também não está dito se aplicar preset ou aplicar memória contam entre os 3 ajustes.
- **A5, preset antigo da Taramps: preset ou memória?** SPEC Non-goals: salvar a leitura "como preset próprio"; HER Presets: "como memória do celular (CAP-4)". Com o preset = 15 bandas (P29), o certo parece ser preset, mas um preset da Taramps traz mais que 15 bandas.
- **A6, a IA salva?** PLA Verificação fala em "a IA lendo, ajustando e **salvando** pela porta"; P29 §5.7: "Hoje vale 'a IA não salva'"; SPEC CAP-12 só cita ler, ajustar e aplicar. Vale a P29 (mais nova).
- **A7, "o que fica fora das camadas".** CAP-4 exclui da memória "o que fica fora das camadas" sem listar (gerador, remote out, BT, nome do produto?). Também falta separar, para rótulos, "EQ de entrada" (memória) × "EQ global" das notas da FILME × as 15 bandas (preset); o MAPA §4 não estava no escopo desta leitura.
- **A8, "proteção nova" e guardião.** Constraints: "tudo é reescrito, inclusive o guardião" e "a proteção nova é provada… antes de ser usada com os alto-falantes"; Non-goals: guardião completo fora, "só as chaves de acesso". Não há teto de volume/ganho no app; resta saber se a interface deve mostrar algum limite ou alerta (ex.: gerador acima de −13 dB).
- **A9, memória do aparelho carregada × Setup.** Ela aparece "sem Setup e sem playlist", mas o usuário pode estar num Setup (saídas nomeadas, link). Não está dito se dá para salvar esse som como memória do celular do Setup atual (HER sugere que sim) e como isso convive com "seguir sem Setup trava as memórias".
- **A10, Setup criado do aparelho.** ST 11 (24/10): nasce com links desligados; P29: o Setup é só físico e o som do aparelho vira a 1ª memória. Um Setup novo sem equipamentos tem link desligado, mas o som do aparelho pode estar com link ligado: a 1ª memória fica fiel ao som ou ao Setup?
- **A11, Setup sem memória.** FM 5: escolher um Setup sem memórias "desmuta sem aplicar nada" (P29 nota da aplicação); então a 1ª conexão sempre muta e o desmute toca o que estiver no aparelho, ainda sem referência. Confirmar se é a experiência desejada na primeira abertura.
- **A12, conhecimento-herdado diz "não são referência" e também "notas para escolher qual carregar"**: mostrar as notas das memórias do aparelho na tela é desejado, mas sem tom de recomendação (HER §memórias; SPEC CAP-6).
- **A13, termos velhos ainda vivos.** CAP-14 fala em "folha do Salvar por camadas", nome que veio do modelo calibração/caráter; hoje só há duas camadas salváveis (memória e preset; o Setup não recebe nada do Salvar). Histórias antigas (ST 8c, 8e) ainda descrevem calibração e equilíbrio; vale a P29.
- **A14, CAP-14 meta mínima** (1 aviso) foi definida pela spec, não pelo Denis (SPEC Assumptions): quanto aviso customizado o redesign deve ter fica em aberto.
- **A15, a ordem da SPEC Constraints** ("pedaço 2" inclui CAP-16/17) e a do PLA (histórias 3 a 12 de 21/10) estão superadas pelas propostas de 23/10 e 29/10 em ST; só serve para histórico.
