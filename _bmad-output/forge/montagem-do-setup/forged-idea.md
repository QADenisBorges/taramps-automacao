---
title: 'Ideia forjada: montagem do Setup e agente que cria a memória'
created: '2026-10-29'
status: hardened
fontes:
  - proposta-ia-setup.md (Denis)
  - proposta-perguntas.md (Denis, só ilustração)
  - estrategia-mesa.md (mesa, travada)
---

# Montagem do Setup e agente que cria a memória

Decisões do Denis no forge de 29/10/2026.

## Camadas

- **Setup = só o físico:** falantes, canais, módulos, ligações e fichas. É montado antes da memória.
- **Memória = toda a regulagem:** atraso, fase, corte, equilíbrio, EQ, limitador. Por isso o atraso sai do Setup (a música fica com atraso 0 e só a FILME compensa a distância).
- **Conferência ao conectar:** compara o que foi lido com a última memória aplicada naquele Setup. Se bate, entra; se não, muta e pede para escolher.
- **As memórias e calibrações de hoje não são referência** para o agente.

## Fichas

- O usuário digita o modelo, a IA busca, **confere se a ficha fecha** e salva numa base com a origem (oficial, outra fonte, digitado). Dado não confirmado nunca aparece como verificado.
- Com a ficha achada, a IA só mostra o valor para confirmar. Os dados de um equipamento ficam numa tela só.

## Agente

- **Onde roda:** Claude Code no notebook, pela porta, por enquanto; depois, dentro do app. Usa a mesma língua de comandos desde o primeiro dia.
- **Porta nos dois sentidos:** a IA manda o modal, o app mostra e a resposta volta pela porta. O usuário só usa o app. Um serviço no notebook acorda o agente.
- **Cerca e escolha:** a conta fixa, em código, protege os equipamentos. A IA escolhe os valores dentro dela.
- **Aplicar:** mostra o resumo do que muda e por quê, e aplica no toque em Ouvir. No refino, a resposta do usuário autoriza o passo seguinte. Fecha a brecha de trocar o som ajuste por ajuste.
- **Melhor resultado:** o usuário aprova na escuta. A IA aplica, ele ouve e ela refina até aprovar.
- **Modelo:** um só no começo (Opus 5.5). Uma tarefa só vai para outro modelo com medida. Sem treino de modelo próprio.
- **Instruções em camadas:** base curta com as regras invioláveis; uma skill por função; conhecimento com fonte; dados do caso lidos pela porta; ferramentas com formato fixo. Versionadas no git; uma mudança só entra se os ouvintes simulados passarem.

## Perguntas (estrategia-mesa.md)

- Ficha de **variáveis de decisão** com valor padrão e dúvida. A próxima pergunta é a de maior dúvida vezes impacto. **Para quando nada muda mais de 1 dB.**
- Três tipos: **perfil** (modal com 3 ou 4 perguntas independentes, opções e Outro), **escuta 1 ou 2** com o volume igualado, e **diagnóstico da queixa**.
- A ordem segue a física: cerca, ganho, corte, atraso e fase, depois equilíbrio tonal, depois dinâmica. Quando a resposta é posição da caixa, o agente diz isso.
- Skills: objetivo, escuta, diagnóstico, receita, cerca-fisica (código), ficha-tecnica, medicao-assistida.

## Perfil e aprendizado

- **O perfil do usuário é ponto de partida, por situação.** A memória manda. O perfil só muda quando a mesma escolha se repete em memórias diferentes. Uma memória nova pode buscar outra coisa.
- **Registros:** o que a IA propôs × o que o usuário deixou; retorno positivo e negativo; retorno sem palavras; escolhas do 1 ou 2; diálogos resumidos; Outro que vira opção só com parâmetro conhecido; contexto (volume, conteúdo, hora). Quem aprende são os arquivos.
- **Garantia:** cerca em código; valor sem motivo é recusado; ouvintes simulados com gosto escondido; cada erro real vira caso de teste; conhecimento com fonte; medida no uso.

## Rejeitado

- **Imitar o Akinator ao pé da letra:** a resposta são números contínuos, não um nome num catálogo.
- **Memórias atuais como catálogo ou gabarito:** as calibrações de hoje estão mal feitas.
- **Calibração no Setup:** o Setup é só o físico.
- **Aplicar sozinha:** contradiz a chave "Aplicar memória do celular".
- **Perguntas no chat do notebook:** o usuário usa só o app.
- **IA dentro do app agora:** custo por uso (~US$ 1,50 por memória no Opus 5.5), chave da API no APK e correct-course. Fica para depois.
- **Modelo local no celular:** fraco para esse raciocínio.

## O que isto muda no que já existe

- **SPEC CAP-16 e história 8e:** a calibração sai do Setup e vai para a memória; a conferência ao conectar muda.
- **CAP-12 (porta):** passa a ir nos dois sentidos, com modal; entra o serviço que acorda o agente.
- **forged-idea de 22/10 (teste de playlist):** cai "uma pergunta por tela" e "Setup: calibração".
- **chaves-de-acesso.md:** fechar a brecha de aplicar ajuste por ajuste.

## Ramos guardados

- Som imersivo no filme: primeiro geometria e tempo (caixas no chão e fora do triângulo hoje).
- Abertura estática do app, mal recebida.

## Próximo passo

`bmad-correct-course` na `spec-app-controle-taramps` com este arquivo, antes da história 12b.
