# Cérebro do Agente da memória

Complementa `../../forge/montagem-do-setup/estrategia-mesa.md` (variáveis de decisão, próxima pergunta, parada, três tipos de pergunta, ordem da física, skills e ouvintes simulados). Onde os dois diferem, vale este arquivo, e onde a mesa e o UX de 30/10 diferem, vale o UX.

## Mudanças sobre a mesa

- **Uma pergunta por tela**, não modal com várias (UX de 30/10). Responder à última pergunta autoriza aplicar.
- **A escuta "1 ou 2" só no refino**, depois do primeiro Aplicando, no lugar de uma resposta do "Como ficou?".
- **Padrão das variáveis:** sai da física e do Setup em uso, lido pela porta. Nunca das memórias do aparelho nem de regulagens antigas: o Agente começa zerado.
- **Variáveis são fenômenos, não casos** (decisão do Denis, 30/10/2026). A tabela de variáveis da mesa (uso música/filme, onde senta) vale só como exemplo do caso de uma sala 2.1. O catálogo geral: objetivo (fidelidade × nível máximo), cobertura (um ouvinte no ponto ideal × vários ou fora do eixo), posição de quem ouve, ambiente (do Setup), tom, voz, dinâmica e cansaço. Cada variável só existe se o Setup permitir; por exemplo, atraso entre caixas só com caixas em posições diferentes.
- **As perguntas nascem do Setup e do objetivo.** A primeira é sempre o objetivo do som, com respostas montadas pelo Agente para aquele Setup e ambiente (carro, caixa bob, sala, ao ar livre…), nunca uma lista fixa de usos.
- **Nenhuma direção fixa.** O Agente não supõe esquerda, direita nem que cada entrada vai para uma caixa. A distribuição das entradas entre as saídas (separadas ou somadas) é decisão da receita, com motivo: separadas dão imagem; somadas dão nível, carga dividida e a música inteira para quem está fora do eixo, ao custo da imagem, de cancelamentos do que foi gravado com fase invertida e da folga interna da soma.
- **Todo padrão carrega dúvida.** Se trocar um padrão mexe mais de 1 dB num ajuste ou muda uma rota, o Agente pergunta ou faz a escuta "1 ou 2"; nunca supõe. Caso real (Denis, SPL para sentir o máximo do kit): a IA antiga supôs as entradas como esquerda e direita, e a soma não foi considerada.
- **A situação do perfil** é o nome que o usuário dá à memória ou ao uso, não uma lista fixa.

## Fluxo de uma criação

1. O usuário toca em "+ Nova memória". O app manda o pedido pela porta, e o serviço acorda o Agente.
2. O Agente lê o Setup, as fichas, a cerca (limites e nível) e o som atual, e abre a criação.
3. Perguntas uma a uma, pela maior dúvida × impacto, até nenhuma resposta mudar um parâmetro em mais de 1 dB.
4. A receita vira ajustes na ordem da física, cada um com frase, valor de antes, valor novo e motivo, dentro da cerca.
5. "Como ficou?": aprovar leva ao Salvar do usuário. Pedir refino traz o diagnóstico, se for queixa, ou a escuta "1 ou 2", se for gosto. Só a mudança é aplicada.
6. Fim (salvo ou cancelado): os registros são gravados.

## Skills

| Skill | Faz | Entrega |
|---|---|---|
| objetivo | ficha de variáveis, próxima pergunta, parada | a pergunta (texto, 3 ou 4 respostas, "Outro…", se é a última) |
| escuta | comparação de uma variável, com o volume igualado pelo app | as versões 1 e 2 |
| diagnóstico | queixa → hipóteses → a pergunta que separa | a pergunta, ou "é posição da caixa" |
| receita | variáveis → ajustes na ordem da física | ajustes com frase, valores e motivo |
| ficha-tecnica | busca o modelo, confere se a ficha fecha (ex.: Qts pelo Qes e Qms; sensibilidade pelos parâmetros) | proposta com a origem e o endereço |
| medicao-assistida | lê a calibração guiada do app e compara o esperado com o medido | a explicação da diferença |
| cerca-fisica | código do app (spec-rendimento), lido pela porta | os tetos; o Agente não passa deles |

## Instruções em camadas

1. **Base curta** com as regras invioláveis: cerca, nenhum valor sem motivo, ordem da física, a IA não salva, nada da IA na tela. Quando o usuário pede um valor (uma potência, por exemplo), o Agente mostra o pedido, o calculado (a tensão) e o recomendado, cada um com o motivo. Não há regra universal do tipo "14 V soa melhor"; o que não se sabe aparece como estimativa (proposta do Denis de 29/10).
2. **Uma skill por função** (tabela acima).
3. **Conhecimento com fonte:** cada regra de áudio cita de onde veio.
4. **Dados do caso** lidos pela porta, nunca embutidos.
5. **Ferramentas com formato fixo:** a ferramenta da porta, em JSON.

Tudo versionado no git. Uma mudança só entra se a avaliação real passar.

## Aprendizado (arquivos)

- **Registros por criação:** o que foi proposto × o que o usuário deixou; o retorno positivo e o negativo; o retorno sem palavras (refino, cancelar, ajuste à mão depois); as escolhas do "1 ou 2"; o diálogo resumido; o contexto (volume, conteúdo, hora).
- **Perfil por situação** (o nome que o usuário dá): é o ponto de partida das variáveis. A memória manda. O perfil só muda quando a mesma escolha se repete em memórias diferentes.
- **"Outro…" vira opção** só quando se liga a um parâmetro conhecido.
- **Cada erro real vira caso de teste** dos ouvintes simulados.
- O aprendizado começa vazio.

## Ouvintes simulados e avaliação

- **Ouvinte simulado:** código, com um Setup, um ambiente e um gosto escondido numérico (ex.: "grave +4 dB, ouve a 75 dB, filme à noite"). Responde às perguntas de palavras pelo perfil e ao "1 ou 2" pela versão mais perto do gosto. Os 8 perfis cobrem Setups e ambientes variados (carro, caixa bob ao ar livre, sala 2.1, 2 vias ativo) e incluem o caso da SPL: nível máximo do kit, posição das caixas indiferente, gosto escondido com as entradas somadas; um Agente que supõe entradas separadas reprova.
- **Modelo falso** (build-auto): respostas gravadas no lugar do modelo, para testar serviço, ferramenta, porta, cerca e registros sem custo e sempre com o mesmo resultado.
- **Avaliação real:** um comando roda o Agente de verdade contra 8 perfis e mostra o custo estimado antes (~US$ 12). Passa se, em cada perfil, todo parâmetro final fica a até 1,5 dB do gosto escondido, com até 8 perguntas e nenhuma violação da cerca ou da ordem da física. O relatório mostra, por perfil, as perguntas, a distância e as violações.
- Um Agente genérico (mesmas perguntas para todos) tem de falhar.
