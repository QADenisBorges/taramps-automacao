---
fonte: mesa do forge de 29/10/2026 (Winston, Sally, Grumbal, Marta)
status: proposta da mesa, para o Denis atacar
---

# Estratégia das perguntas: proposta da mesa

## 1. O agente pergunta para preencher variáveis, não para cumprir roteiro

O agente guarda uma ficha com poucas **variáveis de decisão**. Cada uma move parâmetros conhecidos da memória:

| Variável | Exemplo de resposta | O que ela move |
|---|---|---|
| Uso | música, filme, os dois | memórias separadas; atraso (só a FILME compensa a distância); compressor do diálogo |
| Volume típico e volume máximo | baixo à noite, alto em festa | ganho; teto; reforço de grave e agudo em volume baixo |
| Onde a pessoa senta | distância de cada caixa | atraso; equilíbrio entre as caixas |
| Equilíbrio tonal | mais grave, neutro, mais brilho | inclinação da curva; nível do sub |
| Prioridade da voz | voz na frente, voz natural | 1 a 4 kHz; compressão no filme |
| Dinâmica | explosão forte, volume constante | limitador ou compressor |
| Cansaço | sessões longas | agudo; teto |

Cada variável começa com um valor padrão, tirado da física e do Setup, e com uma **dúvida**.

## 2. Qual pergunta vem a seguir

O agente pergunta a variável em que **a dúvida vezes o impacto** é maior: a que mais muda os números entre uma resposta e outra.

**Parada:** quando nenhuma resposta possível mudar um parâmetro em mais de **1 dB** (o limite do que se percebe, pela escala da skill regulagem), não há mais nada a perguntar. Aí o agente aplica.

## 3. Três tipos de pergunta, cada um no seu momento

1. **Perfil, com palavras** (modal com opções e "Outro"): só o que a pessoa sabe dizer, como uso, volume e onde senta. Perguntas que não dependem umas das outras podem ir juntas no mesmo modal.
2. **Escuta, como no oftalmologista** ("melhor o 1 ou o 2?"): o gosto que ninguém sabe dizer em palavras. O agente toca o mesmo trecho com duas versões que só diferem numa variável. Cada escolha corta pela metade a dúvida daquela variável. **Os dois tocam no mesmo volume**, senão vence o mais alto.
3. **Diagnóstico da queixa** ("as vozes estão baixas"): o agente levanta as hipóteses possíveis e pergunta o que separa uma da outra. Por exemplo, "em todas as cenas ou só na ação?": se for só na ação, é dinâmica (compressor), não EQ.

## 4. A ordem segue a física, não o gosto

Primeiro a cerca, o ganho, o corte, o atraso e a fase. Depois o equilíbrio tonal. Por último a dinâmica. EQ nunca vem antes de o tempo e a fase estarem certos.

Quando a resposta pede algo que o processador não faz (separação, espaço, alcance), o agente diz que é **posição da caixa**, não ajuste.

## 5. Skills

| Skill | Função |
|---|---|
| objetivo | a ficha de variáveis, a escolha da próxima pergunta e a parada |
| escuta | as comparações "1 ou 2" com o volume igualado |
| diagnóstico | queixa → hipóteses → a pergunta que separa uma da outra |
| receita | variáveis → parâmetros, com o motivo de cada valor |
| cerca-fisica | código fixo: o teto de cada canal; a IA não passa dele |
| ficha-tecnica | busca, confere e salva a ficha na base, com a origem |
| medicao-assistida | guia o multímetro e compara o esperado com o medido |

## 6. Como provar que não é genérico (sem usar as memórias atuais)

**Ouvintes simulados:** perfis com um gosto escondido, por exemplo "grave +4 dB, ouve a 75 dB, filme à noite". Um simulador responde às perguntas e às escutas "1 ou 2" seguindo esse gosto. Mede-se:

- quantas perguntas o agente fez;
- a distância entre os parâmetros finais e o gosto escondido;
- se ele respeitou a cerca e a ordem da física.

Um agente genérico faz as mesmas perguntas para todos os perfis e erra o alvo.
