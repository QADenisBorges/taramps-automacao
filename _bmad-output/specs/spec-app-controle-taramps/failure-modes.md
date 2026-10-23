# Falhas no meio de uma operação

Regras confirmadas pelo Denis em 19/10/2026. Elas valem quando o BLE cai, o app fecha ou a IA some.

1. **Gerador com tempo máximo de 30 s.** Ao fechar o app, ao perder a conexão ou num erro, o app manda `*202*0*`. Se não conseguir, tenta reconectar até 3 vezes e então mostra em destaque "DESLIGUE O MÓDULO AGORA".
2. **Ao reconectar, o app relê tudo primeiro.** Se o gerador estiver ligado (`202 = 1`), ele desliga antes de qualquer outra ação.
3. **Cópia antes de mexer em vários parâmetros.** Antes de aplicar uma memória do celular, aplicar um preset ou carregar uma memória do aparelho, o app guarda o estado atual. Se a operação cair no meio, ao voltar ele mostra "aplicado pela metade" e oferece **terminar** ou **voltar à cópia**.
4. **A IA não deixa nada pendente.** Cada comando dela é avulso. O que ela ligou com prazo, como o gerador, desliga sozinho pela regra 1.
5. **Ao conectar, o app confere o Setup (CAP-16).** Depois da releitura da regra 2, ele compara o estado com a última aplicação do Setup escolhido. Na dúvida, muta e pede confirmação antes de tocar. Se o processador perdeu o estado, reaplica a memória ativa depois da confirmação. Regra acrescentada em 23/10/2026 (proposta de mudança da mesma data).
