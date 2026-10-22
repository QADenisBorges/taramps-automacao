# Porta de comandos da IA (CAP-12)

As cinco travas foram aceitas pelo Denis na forja de 19/10/2026:

1. **Desligada por padrão.** A porta só abre quando o usuário liga a chave "IA pela rede".
2. **Pareamento único.** O app mostra um código de 6 dígitos. O cliente IA envia esse código uma vez e recebe uma chave própria. Um pedido sem chave válida é recusado.
3. **Só a rede local.** Um pedido vindo de fora da rede da casa é recusado.
4. **Tudo visível.** Cada comando da IA aparece na tela e fica no histórico. O botão "Parar IA" revoga o acesso na hora.
5. **Risco pede confirmação.** Uma operação marcada em `chaves-de-acesso.md` como "exige confirmação" só roda depois do toque do usuário na tela do celular.

Também valem:

- A IA passa pelas mesmas chaves de acesso que o usuário.
- A senha do processador nunca trafega pela porta.
- Com firmware diferente de R003, a porta responde só às leituras.

## Uso ao mesmo tempo (Denis e IA)

**Decisão do Denis (21/10/2026, UX):** o que o toque do usuário faz enquanto a IA trabalha é uma **opção nas configurações da IA**. O **padrão** é "controles travados até PARAR IA": a tela mostra ao vivo os parâmetros que a IA está mexendo, e os controles só destravam com PARAR IA. A alternativa, que o usuário pode ligar, é a regra abaixo (o toque pausa a IA). Com o padrão, a regra 1 vale na forma "o usuário nunca fica sem saída": PARAR IA está sempre à mão.

1. **O usuário nunca fica bloqueado.** Enquanto a IA está ativa, uma faixa na tela mostra o que ela está fazendo (por exemplo, "IA ajustando o EQ da FILME").
2. **O toque do usuário pausa a tarefa da IA.** O app avisa a IA da pausa e manda cada mudança feita na tela.
3. **A IA retoma só depois de 10 s sem toques**, e depois de avaliar o conjunto: ela continua, adapta a tarefa ou avisa o usuário de um conflito (por exemplo, "você subiu o sub enquanto eu mexia no atraso; quer que eu confira?"). Quem avalia é a IA, fora do app. O app só entrega a pausa, as mudanças e a janela de 10 s.
4. **Um ajuste que o usuário acabou de mexer** só pode ser mudado pela IA depois de 10 s sem toques nele. Antes disso, o app recusa com "o usuário está mexendo nisso".
5. **Toda mudança da IA aparece na tela como aviso**, além do histórico.
