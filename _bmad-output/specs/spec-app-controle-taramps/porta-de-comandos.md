# Porta de comandos da IA (CAP-12)

As cinco travas foram aceitas pelo Denis na forja de 19/10/2026 e revistas em 30/10/2026 (a interface não mostra a IA; proposta de mudança de 30/10):

1. **Desligada por padrão.** A porta só abre quando o usuário liga a chave "Controle pela rede", na tela Aparelho.
2. **Pareamento único.** O app mostra um código de 6 dígitos. O cliente IA envia esse código uma vez e recebe uma chave própria. Um pedido sem chave válida é recusado. O código aparece embaixo da chave, depois de ligada.
3. **Só a rede local.** Um pedido vindo de fora da rede da casa é recusado.
4. **Tudo registrado.** Cada comando da porta fica no histórico, lido pela porta e fora do app, e o efeito dele aparece nos valores da tela. Desligar a chave "Controle pela rede" revoga o acesso na hora.
5. **Risco pede confirmação.** Uma operação marcada em `chaves-de-acesso.md` como "exige confirmação" só roda depois do toque do usuário na folha de risco do app, que diz o que muda e o risco, sem mencionar a IA.

Também valem:

- A IA passa pelas mesmas chaves de acesso que o usuário, inclusive a regra do **Continuar** a cada 3 ajustes seguidos sem um toque do usuário, fora da criação da memória (`chaves-de-acesso.md`).
- A senha do processador nunca trafega pela porta.
- Com firmware diferente de R003, a porta responde só às leituras.

## Uso ao mesmo tempo (Denis e IA)

**Decisão do Denis (30/10/2026, proposta de mudança de 30/10):** sem PARAR IA, sai o padrão "controles travados até PARAR IA" (decisão de 21/10) e a opção nas configurações da IA. Vale sempre a regra do toque que pausa.

1. **O usuário nunca fica bloqueado.** A tela não tem faixa nem aviso da IA: as mudanças da porta aparecem só nos valores, como qualquer outra.
2. **O toque do usuário pausa a tarefa da porta.** O app avisa a IA da pausa e manda cada mudança feita na tela.
3. **A IA retoma só depois de 10 s sem toques**, e depois de avaliar o conjunto: ela continua, adapta a tarefa ou desiste. Quem avalia é a IA, fora do app. O app só entrega a pausa, as mudanças e a janela de 10 s.
4. **Um ajuste que o usuário acabou de mexer** só pode ser mudado pela porta depois de 10 s sem toques nele. Antes disso, a porta recusa com "o usuário está mexendo nisso".
5. **Na criação da memória**, o usuário não mexe nos controles (é uma tela toda, sem abas). O ✕ é a saída.

## Criação da memória (CAP-18)

Decisões do Denis de 30/10/2026 (UX do BGS Sound e proposta de mudança de 30/10). A porta vai nos dois sentidos: a IA manda, o app mostra e devolve a resposta.

1. **Abrir.** A IA abre a criação para o Setup em uso. O app guarda a cópia de antes (`failure-modes.md`, regra 3) e entra na tela das perguntas.
2. **Perguntas.** Cada pergunta chega com o texto, 3 ou 4 respostas e "Outro…", e diz se é a última. O app mostra uma por tela e devolve a resposta (ou o texto do "Outro…") pela porta.
3. **Aplicar.** Só depois da resposta à última pergunta a porta aceita os ajustes da criação, cada um com a frase, o valor de antes, o valor novo e o motivo. No refino, a resposta do "Como ficou?" autoriza o passo seguinte. Os ajustes passam pelas mesmas chaves; o Continuar não vale aqui, porque a última resposta já é o toque.
4. **Risco.** Um ajuste que exige confirmação abre a folha de risco do app, e a animação pausa. Confirmar segue. Cancelar volta à cópia de antes e fecha a criação.
5. **Salvar.** A IA não salva. Salvar e dar o nome são toques do usuário.
6. **Cancelar.** O ✕ das perguntas e do "Como ficou?" volta à cópia de antes e fecha a criação, sem salvar. A porta recebe o aviso.
