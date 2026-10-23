# Relatório de validação — BGS Controle (taramps-automacao)

- **DESIGN.md:** `C:\Users\denis\Desktop\Automação Taramps\_bmad-output\planning-artifacts\ux-designs\ux-taramps-automacao-2026-10-21\DESIGN.md`
- **EXPERIENCE.md:** `C:\Users\denis\Desktop\Automação Taramps\_bmad-output\planning-artifacts\ux-designs\ux-taramps-automacao-2026-10-21\EXPERIENCE.md`
- **Gerado em:** 2026-10-23T13:03

## Veredito geral

A 3b entrou inteira: as 33 entradas do .memlog.md desde “Início da revisão da história 3b” e os 13 itens da seção 4.6 da proposta de 23/10 estão nos dois documentos, sem contradizer nenhuma decisão anterior que continua valendo, e os tokens, os componentes novos e o mock da 3b batem entre si.

O ponto fraco é o modelo da conferência do Setup, que é o coração da 8c: não está dito contra o quê o app confere (a “última aplicação”), como o som é mutado e o que acontece se a conexão cai mutada, nem o que o Início mostra quando o app não sabe qual som está no aparelho; e a camada da rota contradiz o conhecimento-herdado.md. Vale fechar esses cinco pontos altos antes da 8c; o resto pode ser resolvido junto com as histórias 10 e 11.

## Vereditos por categoria

- Cobertura dos fluxos — adequado
- Completude dos tokens — adequado
- Cobertura dos componentes — adequado
- Cobertura dos estados — fraco
- Cobertura das referências visuais — forte
- Excesso e sobre-especificação — adequado
- Disciplina de herança — fraco
- Encaixe na forma — forte
- Cobertura da 3b — adequado

## Achados por severidade

### Critical (0)

Nenhum.

### High (5)

**[Cobertura dos fluxos]** — A IA “pede para salvar” no Fluxo 2, mas a porta não tem esse comando (§ EXPERIENCE.md › Key Flows › Fluxo 2, passo 7)
Nem a porta-de-comandos.md, nem a CAP-12, nem a 8d têm “salvar” (a 8d acrescenta só “aplicar memória”), e criar/renomear/apagar no celular não têm chave. Não está dito como a folha abre com os controles travados pela IA.
Fix: Decidir com o Denis: a IA não salva (o passo vira PARAR IA + Salvar pelo Denis), ou a porta ganha “pedir para salvar”, que só abre a folha na tela, registrado na porta-de-comandos.md e na 8d.

**[Cobertura dos estados]** — A referência da conferência (“última aplicação”) não está definida (§ EXPERIENCE.md › Conectar e Setup; State Patterns › Setup confere, Primeira abertura; Fluxo 1 passo 8; Fluxo 3 passo 8; Quem vem, última linha)
Não se diz o que é guardado, quando atualiza nem que parâmetros entram. O Fluxo 3 (sobe o sub e não salva) abriria a dúvida mutada na próxima conexão se a referência for a última memória aplicada; o Fluxo 1 cria a memória sem “aplicação” e espera “bate”; a decisão 83 sugere comparar só a calibração. Na primeira conexão não há referência (o Casa 2.1 herdado nunca foi aplicado).
Fix: Regra curta decidida com o Denis: referência = último estado que o app deixou no aparelho, ou só calibração do Setup (+ caráter da memória ativa); que parâmetros entram; na primeira conexão, abrir sempre a escolha do Setup.

**[Cobertura dos estados]** — Mutar e a tela de dúvida interrompida não estão definidos (§ EXPERIENCE.md › Conectar e Setup; State Patterns › Setup em dúvida, Trocando de Setup; Interaction Primitives; Uso junto com a IA)
Não se diz como o app muta (MASTER, remote out 252…), o que acontece se a conexão cai ou o app fecha mutado (o processador pode ficar mudo sem o celular), nem se a porta da IA fica fechada durante a dúvida.
Fix: Nomear o mecanismo (decisão para a 8c), acrescentar o estado “Dúvida interrompida” e dizer que a IA só lê durante a dúvida.

**[Cobertura dos estados]** — O app não sabe qual memória do aparelho toca quando não foi ele que carregou (§ EXPERIENCE.md › Fluxo 5 passo 3; State Patterns › Seguir sem Setup; Cartão da memória em uso)
O MAPA só tem 241 (carregar) e os nomes; não há leitura de “memória atual”. Mesmo assim, o Fluxo 5 diz que o cartão mostra “Memória 1 do aparelho”. O Início nesse caso (cartão, chip, botões, Player) não está definido.
Fix: Estado “Som do aparelho, sem memória” (cartão “Som do aparelho”, sem chip, nenhum botão escolhido, Player em “Sem playlist”) e corrigir o Fluxo 5.

**[Disciplina de herança]** — A camada da rota contradiz o conhecimento-herdado.md (§ EXPERIENCE.md › Foundation, Folha do Salvar; conhecimento-herdado.md, linha 39)
O herdado põe “rotas 1 = B e 2 = A” na calibração do Casa 2.1; o SPEC (CAP-16) e o EXPERIENCE.md dizem calibração = atraso, fase, corte, equilíbrio, e o resto vai para o caráter. Link, MASTER e EQ de entrada também não têm camada dita. A 8c divide a FILME herdada com essa regra.
Fix: Tabela parâmetro → camada para todo o MAPA §4, decidida com o Denis, e alinhar o SPEC ou o conhecimento-herdado.md.

### Medium (12)

**[Cobertura dos fluxos]** — Nenhum fluxo marcado como teste 4 (§ EXPERIENCE.md › Key Flows (Fluxos 5 e 7))
O Fluxo 7 é a troca pedida pelo usuário; o caminho de dúvida aparece só no Fluxo 5, passo 3, cujo clímax é o teste 3.
Fix: Marcar um fluxo como teste 4 com o clímax “nada tocou antes da escolha” no caminho Setup em dúvida.

**[Cobertura dos fluxos]** — Fluxo 7: Setup novo sem memórias deixa os passos 4 e 5 sem caminho (§ EXPERIENCE.md › Fluxo 7; State Patterns › Setup sem memórias)
Não diz onde a ESTRADA é criada, como o preset Rock é aplicado sem memória em uso (o chip fica no cartão, que diz “Nenhuma memória”), nem para onde vai o caráter se ele tocar em Salvar antes de criar a memória.
Fix: Escrever os toques do passo 5 e dizer na Folha do Salvar o destino do caráter num Setup sem memórias.

**[Cobertura dos fluxos]** — O aviso customizado do teste 1 (CAP-14) não está mais no Fluxo 1 (§ EXPERIENCE.md › Fluxo 1)
O exemplo antigo (sobrescrever memória) saiu; nenhum aviso que apareça sempre é nomeado.
Fix: Nomear no Fluxo 1 o aviso que cumpre a CAP-14 e avisar o SPEC de que o exemplo ficou velho.

**[Cobertura dos componentes]** — As listas de escolha da 3b não têm peça de linha (§ DESIGN.md › Telas sem mock; EXPERIENCE.md › Chip de preset, Lista de memórias, IA › Escolha do Setup, Setups)
Lista do “…”, lista de Setups, lista de presets do chip e tela Setups são “montadas com as peças de Components”, mas nenhuma peça é linha de escolha. Também não está dito se o chip abre folha ou a tela Presets.
Fix: Uma peça “linha de escolha” usada pelas três listas e dizer que o chip abre uma folha com ela.

**[Cobertura dos estados]** — Salvar sem estado “nada mudou” e sem base do “antes → depois” (§ EXPERIENCE.md › Botão principal “Salvar”, Folha do Salvar)
Não há sinal de mudança não salva; aplicar outra memória descarta sem aviso; não se diz contra o quê a folha compara.
Fix: Salvar travado sem diferença; a folha compara o estado atual com Setup, memória ativa e preset lembrado.

**[Cobertura dos estados]** — Faltam regras de apagar (preset lembrado, herdado, vaga dos 3 botões) (§ EXPERIENCE.md › Apagar preset, Apagar memória, Botões de memória)
Apagar um preset que outras memórias lembram, apagar o Cinema herdado, apagar uma memória que ocupa um dos 3 botões.
Fix: Três linhas: o que as memórias passam a lembrar; se o herdado se apaga; quem ocupa a vaga.

**[Cobertura dos estados]** — Nome longo e limites de nome (§ DESIGN.md › Cartão da memória em uso; EXPERIENCE.md › Cartão de memória)
“MUSICA ORIG” em memoria-hero (60 px) passa da largura útil do cartão (~324 px). Sem regra de quebra nem limite de caracteres para memória, Setup e preset.
Fix: Limite de caracteres por nome e regra do hero (reduz ou quebra; nunca corta).

**[Excesso e sobre-especificação]** — A regra “memória do aparelho → memória do celular + pergunta da calibração” está em cinco lugares (§ EXPERIENCE.md › Folha do Salvar, Cartão de memória, Carregando memória do aparelho, Salvando, Interaction Primitives, Quem vem; DESIGN.md › Folha do Salvar)
Redações um pouco diferentes; a célula da Folha do Salvar tem ~250 palavras e oito suposições.
Fix: Subseção “Salvar por camadas” com uma tabela caso → camadas → perguntas; as outras passagens apontam para ela.

**[Disciplina de herança]** — Valor mostrado × valor guardado com o equilíbrio (§ EXPERIENCE.md › Ajuste da saída › Equilíbrio L/R; Folha do Salvar)
O giratório mostra o valor final; a folha não diz se o “antes → depois” é o final ou o da memória; a soma fora da faixa não tem regra.
Fix: A folha mostra o valor final; a soma é limitada à faixa do MAPA, com aviso no Ajuste.

**[Cobertura da 3b]** — A variante “Trocando de Setup” não está no DESIGN.md (§ DESIGN.md › Layout › Setup em dúvida; Components › Topo do app; EXPERIENCE.md › Trocando de Setup)
O EXPERIENCE.md dá ‹ à tela e a frase “O aparelho está no Casa 2.1.”; o DESIGN.md só tem a tela sem ‹ e com o título “Setup em dúvida”, errado quando não há dúvida.
Fix: Variante com cabeçalho de tela cheia (‹ + “Trocar de Setup”), sem a lista quando não há diferença.

**[Cobertura da 3b]** — A Mesa não tem lugar para os botões de memória (§ DESIGN.md › Layout › Mesa; EXPERIENCE.md › IA › Mesa)
As abas saíram (decisão 76), mas o layout não diz onde ficam os botões nem o que a Mesa mostra com memória do aparelho (ela não tem o cartão).
Fix: Botões no topo da Mesa, com o rótulo MEMÓRIA EM USO e o nome lido quando nenhum está escolhido.

**[Cobertura da 3b]** — “Atualizar o Setup com a do aparelho” sem aviso de alcance (§ EXPERIENCE.md › Folha do Salvar)
Muda a calibração de todas as memórias do Setup, mas não avisa, ao contrário do preset (decisão 66).
Fix: Explicação da opção: “Vale para todas as memórias do Casa 2.1.”

### Low (13)

**[Cobertura dos fluxos]** — Chip de preset e decisão 83 fora dos Key Flows (§ EXPERIENCE.md › Key Flows)
O componente novo e o caminho “som do aparelho → memória do celular” só aparecem nos componentes e em “Quem vem”.
Fix: Uma linha no Fluxo 3 com o chip; a decisão 83 como variante do Fluxo 5.

**[Cobertura dos fluxos]** — Fluxo 3 sem linha “Falha:” (§ EXPERIENCE.md › Fluxo 3)
A falha está dentro do passo 1.
Fix: Mover para uma linha “Falha:”.

**[Completude dos tokens]** — Botões de memória sem token de tipografia (§ DESIGN.md › components.botoes-de-memoria)
O texto (13 px, 400; ativo 600) e o “…” (20 px, 800) só existem no mock.
Fix: Acrescentar typography e ativo-typography ao bloco e o tamanho do “…”.

**[Completude dos tokens]** — Pares de contraste novos fora da tabela (§ DESIGN.md › Colors › tabela)
sobre-escolhido-apoio sobre texto (6,8:1), apoio sobre palco (8,2:1), cores das saídas sobre palco (9,4 / 9,1 / 14,4). Todos passam.
Fix: Acrescentar as três linhas.

**[Completude dos tokens]** — Comentário de token desatualizado e token sem uso (§ DESIGN.md › frontmatter, linhas 21–22)
sobre-escolhido-apoio cita “MEMÓRIA 1 na aba ativa” (a aba saiu); colors.nota não é usado.
Fix: Trocar o exemplo; usar ou tirar nota.

**[Cobertura dos componentes]** — Faixa do som mutado e lista do que não bate sem linha em Component Patterns (§ EXPERIENCE.md › Component Patterns)
O comportamento está espalhado em Conectar e Setup e no DESIGN.md.
Fix: Duas linhas na tabela Conexão.

**[Cobertura das referências visuais]** — Comentário do mock da 3b desatualizado (§ mockups/key-screens-3b.html, linhas 13–14)
Diz que “o spine ainda é o de antes da 3b”.
Fix: Atualizar a frase.

**[Excesso e sobre-especificação]** — Setup em dúvida em quatro passagens; listas de conflito duplicadas (§ EXPERIENCE.md › Conectar e Setup, State Patterns; DESIGN.md › Layout, Components)
Mesmo texto repetido.
Fix: Texto completo em Conectar e Setup; ponteiros no resto.

**[Disciplina de herança]** — Fontes sem a proposta de 23/10 (§ EXPERIENCE.md › frontmatter sources)
A lista revisada na 3b vem de sprint-change-proposal-2026-10-23.md.
Fix: Acrescentar.

**[Disciplina de herança]** — “Memória ativa” × “memória em uso” sem definição (§ EXPERIENCE.md › Conectar e Setup; Reaplicando a memória ativa)
Dois termos para a mesma ideia.
Fix: Usar um só ou definir.

**[Encaixe na forma]** — Regras de comportamento dentro da Information Architecture (§ EXPERIENCE.md › IA › Ajuste da saída, Conectar e Setup, Player)
Quem procura comportamento olha em Component ou State Patterns.
Fix: Deixar ou subir para seções próprias.

**[Cobertura da 3b]** — Cartão com memória do aparelho descrito diferente nos dois documentos (§ DESIGN.md › Cartão da memória em uso; EXPERIENCE.md › Cartão da memória em uso)
DESIGN.md: nome lido no hero + linha; EXPERIENCE.md e Fluxo 5: só a linha.
Fix: Mesma anatomia nos dois.

**[Cobertura da 3b]** — “Tocar no selo mostra a nota herdada” sem forma (§ EXPERIENCE.md › Selo herdado)
Folha? Linha que abre?
Fix: Folha só leitura com a nota de conhecimento-herdado.md.

## Notas mecânicas

- Referências de token: 95 no DESIGN.md e 7 no EXPERIENCE.md, todas resolvem; chaves de componente do corpo iguais às do frontmatter.
- Tokens sem uso no corpo: colors.nota, rounded.xs, spacing.1 e spacing.3 a spacing.9 (o texto usa os nomes gap-*, alvo-*).
- Frontmatter completo nos dois (name, status: final, updated: 2026-10-23, sources); sem Mermaid; sources resolvem.
- Comentários desatualizados: topo do mock da 3b e token sobre-escolhido-apoio.

## Arquivos do revisor
- `review-rubric.md` (com as tabelas de cobertura da 3b na seção 9)
