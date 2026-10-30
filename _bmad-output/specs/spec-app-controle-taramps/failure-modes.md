# Falhas no meio de uma operação

Regras confirmadas pelo Denis em 19/10/2026. Elas valem quando o BLE cai, o app fecha ou a IA some.

1. **Gerador com tempo máximo de 30 s.** Ao fechar o app, ao perder a conexão ou num erro, o app manda `*202*0*`. Se não conseguir, tenta reconectar até 3 vezes e então mostra em destaque "DESLIGUE O MÓDULO AGORA".
2. **Ao reconectar, o app relê tudo primeiro.** Se o gerador estiver ligado (`202 = 1`), ele desliga antes de qualquer outra ação.
3. **Cópia antes de mexer em vários parâmetros.** Antes de aplicar uma memória do celular ou aplicar um preset, o app guarda o estado atual. Se a operação cair no meio, ao voltar ele mostra "aplicado pela metade" e oferece **terminar** ou **voltar à cópia**. O app não carrega mais memória do aparelho (decisão do Denis, 29/10/2026). Uma marca `carregar-memoria` guardada de antes ainda aparece como "aplicado pela metade", mas só **voltar à cópia** funciona; **terminar** diz "O app não sabe refazer esta operação." (como a marca `salvar-memoria` da história 8c).
4. **A IA não deixa nada pendente.** Cada comando dela é avulso. O que ela ligou com prazo, como o gerador, desliga sozinho pela regra 1.
5. **Ao conectar, o app confere o Setup (CAP-16).** Depois da releitura da regra 2, ele compara o que leu com a última memória aplicada no último Setup usado. Se bate, segue direto. Se só ganho, limitador, EQ ou MASTER diferem, avisa e oferece reaplicar, sem mutar. Se corte, atraso, fase ou rota diferem, ou se o Setup ainda não tem memória para comparar, muta os 4 canais (o mudo fica no aparelho até a decisão, mesmo se o app fechar ou a conexão cair; a IA fica bloqueada) e pede confirmação antes de tocar:
   - **escolher um Setup:** aplica a memória ativa dele, relê e desmuta; num Setup sem memórias, desmuta sem aplicar nada;
   - **criar um Setup novo:** o som do aparelho vira a primeira memória dele;
   - **seguir sem Setup:** desmuta sem aplicar nada; as memórias do celular ficam travadas até escolher um Setup.

   Se o processador perdeu o estado, reaplica a memória ativa depois da confirmação. Regra acrescentada em 23/10/2026 e refeita em 29/10/2026 (propostas de mudança das mesmas datas).
