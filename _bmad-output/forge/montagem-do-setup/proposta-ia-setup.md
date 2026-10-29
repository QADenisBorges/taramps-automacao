---
fonte: Denis, colado no forge de 29/10/2026
---

# Interação com a IA durante a criação do setup

## 1. Objetivo da interação

A IA do aplicativo deve auxiliar o usuário na criação do setup de áudio de maneira prática, intuitiva e orientada por perguntas objetivas.

A interação deve ocorrer principalmente por meio de caixas de diálogo (modais), apresentando de 3 a 4 perguntas por etapa, sempre que possível. Cada pergunta deve oferecer opções predefinidas e uma alternativa aberta, permitindo que o usuário informe outra resposta quando nenhuma das opções disponíveis atender à sua necessidade.

O objetivo é reduzir a complexidade das configurações técnicas, permitindo que usuários com diferentes níveis de conhecimento configurem o sistema com segurança e alcancem os resultados sonoros desejados.

## 2. Identificação dos alto-falantes e dos canais

Antes de iniciar a configuração do setup, o usuário deverá informar quais alto-falantes estão instalados e em quais canais do módulo ou amplificador estão conectados.

Para cada alto-falante, a IA deverá buscar automaticamente as especificações técnicas com base no modelo informado.

O processo deverá seguir esta ordem:

1. O usuário informa o fabricante e o modelo do alto-falante.
2. A IA pesquisa as especificações técnicas disponíveis.
3. Caso encontre informações confiáveis, apresenta os dados identificados para confirmação do usuário.
4. Caso não encontre informações suficientes, solicita que o usuário forneça os dados manualmente ou disponibilize um site de referência que possa ser consultado.
5. Após a identificação do equipamento, o usuário informa em qual canal ele está conectado.

A identificação deverá considerar, conforme a disponibilidade das informações, características como potência RMS, impedância, sensibilidade, resposta de frequência e demais especificações relevantes para a configuração.

## 3. Base de equipamentos com especificações confiáveis

O aplicativo deverá manter uma base de equipamentos identificados pela IA, priorizando modelos que possuam manuais, fichas técnicas oficiais ou outras fontes concretas e confiáveis.

Quando um modelo for identificado e suas especificações forem verificadas, essas informações poderão ser armazenadas para reutilização em futuros setups.

O objetivo é evitar pesquisas repetidas, aumentar a confiabilidade das configurações e construir progressivamente uma base técnica de alto-falantes, amplificadores, módulos e outros equipamentos compatíveis com o sistema.

A base deverá preservar a origem das informações, permitindo identificar a fonte consultada e distinguir dados oficiais, dados de outras fontes e informações fornecidas manualmente pelo usuário.

Informações ainda não confirmadas não deverão ser apresentadas como especificações verificadas.

## 4. Configuração assistida e validação de informações

Durante a criação do setup, a IA poderá identificar situações em que seja necessário medir, conferir ou confirmar algum parâmetro do sistema.

Nesses casos, deverá solicitar ao usuário a informação necessária e explicar, de maneira objetiva, o que precisa ser verificado.

O usuário poderá informar os valores manualmente ou solicitar ajuda para realizar a conferência.

Por exemplo, se for necessário determinar a tensão de saída correspondente a uma potência desejada, a IA poderá orientar o usuário durante o cálculo e a medição, considerando a impedância, o tipo de sinal e as características do equipamento.

A assistência deverá ser contextualizada: a IA precisa compreender qual equipamento está sendo configurado, qual canal está sendo analisado e qual resultado o usuário pretende alcançar.

## 5. Janela de assistência técnica

Quando o usuário solicitar ajuda, o aplicativo deverá abrir uma janela específica para assistência técnica com a IA.

Essa janela terá como objetivo orientar o usuário durante verificações, cálculos e medições necessárias à configuração.

A IA deverá:

- Explicar o que precisa ser verificado e por quê.
- Solicitar os dados necessários por meio de perguntas objetivas.
- Apresentar cálculos e valores de referência adequados ao equipamento identificado.
- Comparar o resultado obtido pelo usuário com o resultado esperado.
- Sugerir ajustes quando identificar oportunidades de melhorar a configuração.
- Solicitar confirmação quando uma informação importante ainda não estiver validada.

O usuário deverá poder retornar à tela de configuração do setup a qualquer momento, sem perder as informações já preenchidas.

A assistência deverá preservar o contexto da configuração, incluindo o equipamento, o canal, os valores informados e o objetivo sonoro definido pelo usuário.

## 6. Recomendações alinhadas ao objetivo do usuário

A IA não deverá apenas executar os valores solicitados pelo usuário. Ela também deverá compreender o resultado desejado e avaliar se existem alternativas tecnicamente mais adequadas.

Por exemplo, suponha que o usuário queira obter um som alto e com qualidade em um alto-falante JBL de 80 W RMS.

A IA deverá considerar o alto-falante, o amplificador, a impedância, a configuração do canal e as condições de utilização para avaliar os parâmetros recomendados.

Se o usuário solicitar uma potência específica, a IA poderá calcular a tensão correspondente e orientar a conferência da regulagem.

Caso identifique que outra configuração atende melhor ao objetivo, poderá apresentar uma recomendação alternativa, explicando as diferenças e os benefícios esperados.

A recomendação poderá considerar, por exemplo:

- Potência de saída pretendida.
- Tensão correspondente à potência desejada.
- Limites elétricos do alto-falante e do amplificador.
- Margem de segurança para evitar danos.
- Possibilidade de distorção e clipping.
- Qualidade sonora esperada para o uso pretendido.

A IA deverá distinguir entre o valor solicitado pelo usuário, o valor calculado e o valor recomendado. O usuário deverá compreender os motivos da recomendação e poder confirmar ou revisar a escolha.

Não deverá existir uma regra universal que associe determinada tensão, como 14 V, à melhor qualidade sonora. A recomendação dependerá do equipamento, da impedância e do objetivo de regulagem.

## 7. Exemplo de interação

Contexto: o usuário está configurando um canal conectado a um alto-falante JBL de 80 W RMS e deseja obter volume elevado sem comprometer a qualidade sonora.

Pergunta 1 — Qual é o seu objetivo principal para este alto-falante?

- Volume elevado com boa qualidade sonora.
- Máxima fidelidade sonora.
- Maior pressão sonora.
- Outra finalidade.

Pergunta 2 — Qual é a impedância do alto-falante?

- 2 Ω.
- 4 Ω.
- 8 Ω.
- Não sei informar.

Pergunta 3 — Você já definiu a potência que deseja utilizar?

- Sim, quero utilizar uma potência específica.
- Quero uma recomendação da IA.
- Quero conferir a regulagem atual.
- Outro objetivo.

Pergunta 4 — Você consegue medir a tensão de saída do canal?

- Sim, já tenho o valor medido.
- Consigo medir com orientação.
- Preciso de ajuda para entender como realizar a medição.
- Não consigo medir neste momento.

Com base nas respostas, a IA poderá apresentar uma recomendação inicial ou iniciar uma etapa de assistência técnica.

Se o usuário precisar de ajuda para medir a tensão, poderá abrir a janela de assistência, receber as orientações necessárias e retornar à configuração original após concluir a verificação.

Os valores finais deverão ser calculados com base nos dados efetivamente confirmados, e não apenas em estimativas genéricas.

## 8. Continuidade e controle da configuração

A criação do setup deverá funcionar como um processo contínuo, no qual o usuário possa avançar, voltar, revisar respostas e solicitar ajuda a qualquer momento.

A IA deverá manter o contexto durante toda a configuração, evitando que o usuário precise repetir informações já fornecidas.

Quando uma recomendação depender de uma informação ainda desconhecida, a IA deverá solicitar essa informação ou apresentar uma estimativa claramente identificada como tal.

O usuário deverá ter a palavra final sobre as preferências de configuração, desde que os parâmetros escolhidos sejam compatíveis com os limites técnicos e de segurança do equipamento.

## 9. Princípio geral

A IA deverá atuar como uma assistente técnica de áudio, combinando perguntas objetivas, identificação automática de equipamentos, consulta a fontes confiáveis, cálculos técnicos e recomendações personalizadas.

A experiência deve ser simples para o usuário, mesmo quando os cálculos internos forem complexos.

O sistema deverá priorizar três princípios:

1. Simplicidade: poucas perguntas, opções claras e possibilidade de resposta aberta.
2. Confiabilidade: especificações verificadas, cálculos fundamentados e identificação explícita de informações desconhecidas.
3. Orientação por objetivos: recomendações que considerem o resultado sonoro pretendido, e não apenas a execução literal dos valores solicitados.
