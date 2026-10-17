# Roteiro de sábado, passo a passo

Cada passo já está no formato da tela. Copie a tela como está, troque o que estiver entre `<…>`
pelo valor do momento e nada mais. `M` é o número de passos da etapa. Os comandos marcados
**IA roda** são executados por você, nunca pelo Denis. Os nomes de medida são os que vão no
`sessao.py medida`; antes de pedir uma, confira no `progresso` se já existe.

Referência do estado lido em 16/10 (`estado/leituras/2026-10-16_1747.json`): master 0,0; ganhos
−11,0 / −11,0 / −10,0 / −10,0 dB; roteamento A+B / B / A+B / A+B; saída 2 mutada; JBL BT12 em
150 Hz (saída 1) e 120 Hz (saída 2); sub BT24 em 28 Hz + LR24 em 140 Hz, fase 180°, EQ de saída
+3 dB em 50 Hz (Q 0,8); limiter −11,0 / −11,0 / −6,0 / −6,0 dB, auto OFF, attack 50,0 ms (50,1 na
saída 4), release 500 ms; EQ de 15 bandas e EQ de entrada em 0.

---

## Etapa 0 · Preparação (M = 3)

**0.1 Senha.** Antes de mostrar, rode `Test-Path "$env:USERPROFILE\.taramps\senha"` (PowerShell;
é o único contato da IA com esse arquivo). Se der `True`, a tela vira `Senha já guardada.
Responda: ok`. A digitação não ecoa (`-AsSecureString`); o Confira é o Denis quem roda.

```
Etapa 0 · Passo 1 de 3 — Guardar a senha BT fora do projeto
Faça: no SEU PowerShell (não aqui), cole e rode, e digite a senha quando ele pedir (ela não aparece):
  New-Item -ItemType Directory -Force "$env:USERPROFILE\.taramps" | Out-Null; $s = Read-Host "Senha BT (4 caracteres)" -AsSecureString; [Runtime.InteropServices.Marshal]::PtrToStringBSTR([Runtime.InteropServices.Marshal]::SecureStringToBSTR($s)) | Set-Content -NoNewline "$env:USERPROFILE\.taramps\senha"
Confira: (Get-Content "$env:USERPROFILE\.taramps\senha" -Raw).Trim().Length  responde 4
Responda: ok
```

**0.2 Bluetooth.**

```
Etapa 0 · Passo 2 de 3 — Liberar o processador para o notebook
Faça: ligue o processador, feche o app da Taramps no celular e ligue o Bluetooth do notebook
Confira: o app está fechado (não só em segundo plano)
Responda: ok
```

**0.3 Leitura nova. IA roda** `uv run ferramentas/ler_processador.py`. Compare com a referência
acima e diga em uma linha o que mudou (nada, ou os itens). Se o código for 3, volte à tela 0.2.

```
Etapa 0 · Passo 3 de 3 — Ler o aparelho
Faça: nada; eu leio agora (13 s)
Confira: "Pronto. 84 lidos, 0 falhas." · mudou desde 16/10: <nada | lista curta>
Responda: ok
```

---

## Etapa 1 · Auditoria do inventário (M = 11)

Um item por tela. Marque a classe: **fato com fonte**, **decisão do Som** (você reavalia e propõe) ou
**desconhecido**. O item só vale depois do ok. Se ele responder algo diferente, registre a resposta
no passo e use o valor dele daí em diante.

```
Etapa 1 · Passo 1 de 11 — Cadeia (fato: montagem-2.1.md)
Faça: olhe a montagem
Confira: celular (BT) ou notebook (P2) → PRO 2.4BT (modo BOB) → SD800.4 EVO (fonte BOB 60A, 13,8 V) → JBL L/R + sub em ponte
Responda: ok, ou o que está diferente
```

```
Etapa 1 · Passo 2 de 11 — JBL (fato: montagem-2.1.md)
Faça: olhe os cabos das JBL no módulo
Confira: saída 1 → CH1 → JBL L; saída 2 → CH2 → JBL R; chaves em F; caixas BS-369 seladas
Responda: ok, ou o que está diferente
```

```
Etapa 1 · Passo 3 de 11 — Sub (fato: montagem-2.1.md, foto de 11/10)
Faça: olhe os cabos do sub no módulo
Confira: saídas 3 e 4 → CH3/CH4 em ponte, + no CH4 ⊕ e − no CH3 ⊖ → Protech Light 8" 4 Ω, caixa com duto
Responda: ok, ou o que está diferente
```

```
Etapa 1 · Passo 4 de 11 — Fusíveis (fato: montagem-2.1.md) · medida fusivel_proc_a
Faça: olhe o porta-fusível do processador
Confira: 40 A no + do módulo; o do processador era 5 A provisório (o certo é 1–2 A)
Responda: o valor do fusível do processador hoje (em A)
```

```
Etapa 1 · Passo 5 de 11 — Ficha da JBL 4TRMS80 (fato: ficha da caixa)
Faça: nada, só confirme
Confira: corte mínimo 100 Hz a 24 dB/oit; Re 3,4 Ω; 80 W RMS; Xmax 1,75 mm. É isso que o guardião usa.
Responda: ok
```

```
Etapa 1 · Passo 6 de 11 — Passa-alta das JBL (decisão do Som: BT12 em 120 Hz, excursão de 09/10)
Faça: nada, decida
Confira: lido: saída 1 BT12 em 150 Hz, saída 2 BT12 em 120 Hz (diferentes). Proposta (decidida com o Denis em 17/10): LR24 em 150 Hz nas duas, e o sub cobrindo até 150 Hz (LR24). O sub alivia as JBL onde a 4" mais sofre (100–130 Hz): ~7 dB menos de movimento do cone, o que abre espaço para o teto novo da etapa 3.
Responda: ok, ou a frequência que prefere (120 a 150; se a voz "cair" no sub na etapa 5, descemos)
```

```
Etapa 1 · Passo 7 de 11 — Subsônico do sub (decisão do Som: 30 Hz; Fb do duto desconhecida)
Faça: nada, decida
Confira: lido 28 Hz BT24. Sem a Fb, o mínimo seguro é 35 Hz; com o +3 dB em 50 Hz do sub, o guardião só aceita 39 Hz ou mais. Proposta: 40 Hz BT24.
Responda: ok, ou o número (39 ou mais)
```

```
Etapa 1 · Passo 8 de 11 — Ponte igual (fato: regra 4 do guardião)
Faça: nada, confirme
Confira: lido: attack 50,0 ms na saída 3 e 50,1 ms na saída 4. Tudo o mais igual. Proposta: 50,0 nas duas.
Responda: ok
```

```
Etapa 1 · Passo 9 de 11 — Roteamento e mute (desconhecido: lido em 16/10)
Faça: lembre o que você configurou no app
Confira: saída 1 = A+B, saída 2 = B, saída 2 MUTADA, 3 e 4 = A+B. Para estéreo: saída 1 = A, saída 2 = B, nada mutado.
Responda: "manter" ou "corrigir"
```

```
Etapa 1 · Passo 10 de 11 — Limiter (fato: estado/guardiao.json)
Faça: nada, confirme
Confira: JBL −11,0 dB (0,56 V) e sub −6,0 dB (1,00 V), auto OFF, attack 50 ms, release 500 ms. Esses são os tetos; o guardião não deixa subir.
Responda: ok
```

```
Etapa 1 · Passo 11 de 11 — Tensão da fonte (desconhecido: nunca medida) · medida fonte_vdc
Faça: multímetro em V DC nos bornes + e − do módulo, com tudo ligado
Confira: esperado 13,8 V (12,6 a 14,4 serve)
Responda: o número em V, ou "pular"
```

"pular" também é registrado: `medida fonte_vdc pular -` (o passo não volta; refazer só com
`--refazer`). Vale para toda medida: "pular" ou "sem adaptador" viram `medida NOME pular -`.

Fb do duto: não entra hoje. Registre `ideia "medir a Fb do duto (varredura 25–60 Hz) e soltar o subsônico"`.

---

## Etapa 2 · Correção das violações lidas (M = 4)

Para cada passo: **IA roda** o `--simular`; com `APROVADO`, mostre a tela; com o ok, **IA roda** o
mesmo comando sem `--simular` e diz uma linha: `Pronto: N aplicado(s), relidos iguais.` Com
`RECUSADO`, explique em uma frase e ofereça o `Seguro:`. Troque `150` e `40` pelos valores
decididos nos passos 1.6 e 1.7 (o passa-baixa do sub acompanha o passa-alta das JBL).

```
Etapa 2 · Passo 1 de 4 — Passa-alta das JBL
Faça: nada; eu simulo: ajustar_processador.py --simular 042:0=LR24 040:0=150 042:1=LR24 040:1=150
Confira: Guardião: APROVADO
Responda: ok para aplicar
```

```
Etapa 2 · Passo 2 de 4 — Subsônico e passa-baixa do sub
Faça: nada; eu simulo: ajustar_processador.py --simular 040:2=40 041:2=150 043:2=LR24   (espelha na saída 4)
Confira: Guardião: APROVADO
Responda: ok para aplicar
```

```
Etapa 2 · Passo 3 de 4 — Ponte igual
Faça: nada; eu simulo: ajustar_processador.py --simular 072:2=50.0   (espelha na saída 4)
Confira: Guardião: APROVADO
Responda: ok para aplicar
```

Passo 4 só se o 1.9 foi "corrigir"; senão, `Etapa 2 · Passo 4 de 4 — Roteamento: mantido. Responda: ok`.

```
Etapa 2 · Passo 4 de 4 — Roteamento e mute
Faça: nada; eu simulo: ajustar_processador.py --simular 010:0=a 100:1=0
Confira: Guardião: APROVADO
Responda: ok para aplicar
```

---

## Etapa 3 · Teste dos +2 dB e teto novo das JBL, sem falantes (M = 14)

Precisa: multímetro, celular com tom de **400 Hz** a 0 dBFS (arquivo ou gerador de tom) pareado por
Bluetooth no processador. Depois da etapa 2 as JBL cortam abaixo de 150 Hz: um tom de 60 Hz sairia
~30 dB mais baixo e a medida não valeria. 400 Hz fica dentro da faixa delas, e a maioria dos
multímetros mede bem até ~400 Hz. Passos 2 a 4 são as resistências que nunca foram medidas: já que os
falantes vão ficar soltos, é a hora. Medidas: `jbl_l_ohm`, `jbl_r_ohm`, `sub_ohm`, `saida1_vac`,
`saida1_mais2db_vac`, `teto_jbl_escolhido`, `saida1_teto_novo_vac`, `saida2_teto_novo_vac`.

```
Etapa 3 · Passo 1 de 14 — Soltar os falantes
Faça: tire a fonte da tomada e solte os fios das JBL L, JBL R e do sub nos bornes do módulo
Confira: nenhum fio de falante encostando em borne
Responda: ok
```

```
Etapa 3 · Passo 2 de 14 — Resistência da JBL L · medida jbl_l_ohm
Faça: multímetro em Ω nas pontas do cabo da JBL L
Confira: esperado ≈ 3,4 Ω
Responda: o número
```

```
Etapa 3 · Passo 3 de 14 — Resistência da JBL R · medida jbl_r_ohm
Faça: multímetro em Ω nas pontas do cabo da JBL R
Confira: esperado ≈ 3,4 Ω
Responda: o número
```

```
Etapa 3 · Passo 4 de 14 — Resistência do sub · medida sub_ohm
Faça: multímetro em Ω nas pontas do cabo do sub
Confira: esperado 3 a 3,6 Ω (perto de 7 Ω muda o alvo da ponte: me avise)
Responda: o número
```

```
Etapa 3 · Passo 5 de 14 — Tom pelo Bluetooth · medida saida1_vac
Faça: fonte na tomada; celular a 100 % tocando o tom de 400 Hz; multímetro em V AC nos bornes CH1 do módulo
Confira: esperado ≈ 11 V (o limiter calibrado)
Responda: o número
```

```
Etapa 3 · Passo 6 de 14 — +2 dB pelo app · medida saida1_mais2db_vac
Faça: abra o app, Saída 1, ganho de −11,0 para −9,0 dB; leia o multímetro com o tom tocando
Confira: se ficar em 11,0 V ou menos, o limitador é o teto (passou); se passar de 11,0 V, não passou
Responda: o número
```

```
Etapa 3 · Passo 7 de 14 — Voltar o ganho
Faça: no app, ganho da saída 1 de volta para −11,0 dB; pare o tom; feche o app
Confira: app fechado
Responda: ok
```

**3.8 Releitura. IA roda** `uv run ferramentas/ler_processador.py` (o app escreveu). Confira
`081:0 = −11,0` e que mais nada mudou. Se o ganho não voltou, **IA roda** `ajustar_processador.py
--simular 081:0=-11.0` e aplica com o ok (baixar sempre passa).

```
Etapa 3 · Passo 8 de 14 — Reler o aparelho
Faça: nada; eu leio
Confira: ganho da saída 1 em −11,0 dB; mudou mais alguma coisa: <nada | lista>
Responda: ok
```

Passo 9 só se `saida1_mais2db_vac` é um número ≤ 11,0. Se é maior, ou não é número ("pular",
texto): `Etapa 3 · Passo 9 de 14 — Não passou (ou não medido): a regra 6 fica ligada (nível não
sobe; EQ só por corte). Responda: ok`.

```
Etapa 3 · Passo 9 de 14 — Desligar a regra 6 (só o Denis edita)
Faça: abra estado/guardiao.json e troque  "regra6": true  por  "regra6": false ; salve
Confira: eu rodo ajustar_processador.py --simular 081:0=-9.0 (só simula, nunca aplica): com a regra 6 desligada diz "Guardião: APROVADO"; se disser RECUSADO (regra 6), a edição não pegou
Responda: ok
```

O ganho da saída 1 **não** é aplicado nesse passo: o `--simular` serve só para provar que a regra 6
saiu. O ganho real das JBL sobe só na memória MUSICA HIGH (etapa 5).

### Teto novo das JBL (passos 10 a 14; só se o passo 9 desligou a regra 6)

O teto de 11 V foi validado pelo **movimento do cone com o corte antigo** (BT12 perto de 120 Hz).
Com LR24 em 150 Hz, o limite da JBL passa a ser o **calor** (80 W RMS, 4 Ω). Duas opções:

| Opção | Tensão no módulo | `teto_limiter_db` das saídas 1 e 2 | Ganho sobre hoje |
|---|---|---|---|
| Ficha (recomendada para o dia a dia) | ~12,6 V | `-9.8` | +1,2 dB |
| Demonstração curta (até ~15 min, aceite de risco) | ~15,5 V | `-8.0` | +3 dB |

Conta: o módulo multiplica a saída do processador por ~19,6 (0,56 V → 11 V);
`teto_limiter_db` = 20·log10(V_módulo ÷ 19,6 ÷ 2,0). Se o passo 9 **não** desligou a regra 6, os
passos 10 a 14 viram telas de uma linha: `Etapa 3 · Passo N de 14 — Teto das JBL fica em 11 V
(o +2 dB não passou). Responda: ok`.

```
Etapa 3 · Passo 10 de 14 — Escolher o teto novo das JBL · medida teto_jbl_escolhido
Faça: nada, decida
Confira: 12,6 V = regra da ficha, +1,2 dB, seguro para o dia a dia · 15,5 V = demonstração curta, +3 dB, você aceita o risco (pôr a mão no módulo no fim)
Responda: 12,6 ou 15,5
```

Registre `medida teto_jbl_escolhido <12.6|15.5> V`.

```
Etapa 3 · Passo 11 de 14 — Subir o teto no guardião (só o Denis edita)
Faça: abra estado/guardiao.json e, nas saídas 1 e 2, troque  "teto_limiter_db": -11.0  por  "teto_limiter_db": <-9.8 | -8.0> ; salve
Confira: eu rodo ajustar_processador.py --simular 071:0=<-9.8|-8.0> (só simula): com o teto novo diz "Guardião: APROVADO"; se disser RECUSADO (trava 3), a edição não pegou
Responda: ok
```

```
Etapa 3 · Passo 12 de 14 — Subir o limitador das JBL
Faça: nada; eu simulo: ajustar_processador.py --simular 071:0=<-9.8|-8.0> 071:1=<-9.8|-8.0>
Confira: Guardião: APROVADO
Responda: ok para aplicar
```

```
Etapa 3 · Passo 13 de 14 — Medir o teto novo na saída 1 · medida saida1_teto_novo_vac
Faça: celular a 100 % com o tom de 400 Hz; no app, ganho da saída 1 em −9,0 dB (o tom passa do limitador); multímetro em V AC no CH1; depois volte o ganho para −11,0 e feche o app
Confira: esperado ≈ <12,6 | 15,5> V; se passar mais de 0,5 V, me diga: eu baixo o limitador 0,5 dB por vez
Responda: o número
```

```
Etapa 3 · Passo 14 de 14 — Medir o teto novo na saída 2 · medida saida2_teto_novo_vac
Faça: o mesmo no CH2 (ganho da saída 2 em −9,0 dB, medir, voltar para −11,0); pare o tom; feche o app
Confira: esperado ≈ <12,6 | 15,5> V
Responda: o número
```

Depois do passo 14, **IA roda** uma leitura (o app escreveu) e confere os ganhos em −11,0 e os
limitadores no teto novo. Se alguma medida passou mais de 0,5 V do alvo, baixe o `071` daquela
saída em 0,5 dB (`--simular` antes) e peça a medida de novo com `--refazer "limitador baixou 0,5 dB"`.

---

## Etapa 4 · P2 a 100 % e zumbido (M = 4)

Medidas: `p2_100_vac`, `p2_volume_pct`, `zumbido`. Sem adaptador P2 → 2 RCA: no passo 1 responda
"sem adaptador"; registre `medida p2_100_vac pular -` e `medida zumbido pular -`, os passos 2 e 4
viram uma linha ("pulado: sem adaptador · Responda: ok") e registre
`ideia "filme por BT: testar o atraso no streaming; comprar adaptador P2 fêmea → 2 RCA"`.

```
Etapa 4 · Passo 1 de 4 — Ligar o notebook
Faça: cabo P2 do notebook → adaptador → RCA A (L) e B (R) da entrada do processador; falantes ainda soltos
Confira: os dois RCA firmes
Responda: ok, ou "sem adaptador"
```

```
Etapa 4 · Passo 2 de 4 — Tom pelo P2 · medida p2_100_vac
Faça: notebook a 100 % tocando o tom de 60 Hz a 0 dBFS; multímetro em V AC no CH1
Confira: compare com saida1_vac (<valor>): se der mais, baixe o volume do notebook até marcar o mesmo e anote a porcentagem
Responda: o número em V (e a porcentagem, se baixou)
```

Se baixou: `medida p2_volume_pct <pct> %`. Esse é o volume máximo do notebook no filme.

```
Etapa 4 · Passo 3 de 4 — Religar os falantes
Faça: pare o tom; fonte fora da tomada; + no borne vermelho de cada JBL (CH1 = L, CH2 = R); sub com + no CH4 ⊕; fonte na tomada
Confira: nenhum fio solto; multímetro fora
Responda: ok
```

```
Etapa 4 · Passo 4 de 4 — Zumbido · medida zumbido
Faça: notebook no P2 com o carregador ligado, nada tocando, volume no valor anotado
Confira: ouça a 30 cm das JBL e do sub
Responda: "não", "fraco" ou "forte"
```

Se "fraco" ou "forte": peça uma vez sem o carregador (`medida zumbido_sem_carregador`). Se some, o
filme roda na bateria; se não some, `ideia "isolador de loop de terra no P2"` e o filme vai por BT
se o atraso não incomodar.

---

## Etapa 5 · Memórias (M = 9: três memórias × três passos)

Ordem: **MUSICA HIGH** (memória 1), **MUSICA ORIGINAL** (memória 2; no app o nome cabe só como
`MUSICA ORIG`, até 12 caracteres), **FILME** (memória 3). As três partem do corte da etapa 2
(JBL LR24 em 150 Hz, sub até 150 Hz). Cada memória: **propor e
aplicar**, **ouvir e dar nota**, **salvar pelo app**. Comece cada uma a partir do estado corrigido
(EQ de 15 bandas em 0). Entre memórias, **IA roda** uma leitura (o app foi usado para salvar).
A nota é medida: `nota_musica_high`, `nota_musica_original`, `nota_filme` (número de 1 a 5; uma palavra
também vale como valor). A cada nova rodada de ouvir, registre com `--refazer "<ajuste feito>"`.

Propostas iniciais (ajuste pelo que ele ouvir, uma ou duas bandas por vez, sempre `--simular`):

| Memória | Lote inicial | O que muda |
|---|---|---|
| MUSICA HIGH | Com a regra 6 off e o teto novo: JBL até o limitador novo, `081:0=-9.0 081:1=-9.0` (12,6 V) ou `-8.0` (15,5 V); EQ 0; sub como está (sem folga, regra 7) | volume e impacto para demonstração de até 15 min; sem teto novo, fica o máximo da calibração (11 V) |
| MUSICA ORIGINAL | `081:0=-11.0 081:1=-11.0 071:0=-11.0 071:1=-11.0 020:5=-2.0 020:6=-1.5 020:10=-1.0` | dia a dia: ganho e limitador das JBL de volta aos 11 V (baixar sempre passa); tira o embolado (250/400) e a aspereza (2,5 k) |
| FILME | `081:0=-11.0 081:1=-11.0 071:0=-11.0 071:1=-11.0 081:2=-12.0 020:3=-2.0 020:4=-2.0 020:5=-2.0 020:12=-1.0` | sub −2 dB e corte em 100–250 Hz: a fala sobe em relação; −1 em 6,3 k tira o chiado |

Teste da voz (MUSICA ORIGINAL, primeira rodada de ouvir): uma música com voz masculina. Se a voz
parecer "sair do sub", baixe o corte das JBL e do sub juntos para 130 Hz (`040:0=130 040:1=130
041:2=130`, `--simular` antes).

Avisos: MUSICA HIGH por 15 min esquenta o SD800.4 em ponte e o sub, e ninguém mede temperatura:
no passo "ouvir", peça para encostar a mão no módulo ao fim e responder "morno" ou "quente";
"quente" = parar e anotar. Sem a regra 6, o sub continua sem folga (regra 7): não proponha reforço
em 25–100 Hz.

```
Etapa 5 · Passo <1|4|7> de 9 — <Memória>: propor e aplicar
Faça: nada; eu simulo: ajustar_processador.py --simular <lote>
Confira: Guardião: APROVADO · o que muda: <uma linha, com os dB>
Responda: ok para aplicar
```

```
Etapa 5 · Passo <2|5|8> de 9 — <Memória>: ouvir
Faça: toque <2 min de uma música que você conhece bem | uma cena com diálogo e grave> pelo <Bluetooth | P2>
Confira: <a sensação-alvo da memória: pancada limpa | nada embolado | fala na frente do grave>
Responda: nota de 1 a 5, ou uma palavra do que incomoda
```

Nota ≤ 3 ou palavra: traduza com o vocabulário, mostre os dB e se é pequeno ou grande, confirme,
aplique (`--simular` antes) e **reapresente a mesma tela de ouvir** com o mesmo número de passo.
Nota ≥ 4: avance.

```
Etapa 5 · Passo <3|6|9> de 9 — <Memória>: salvar pelo app
Faça: abra o app → Memórias → salve na memória <n> com o nome "<MUSICA HIGH | MUSICA ORIG | FILME>"; feche o app
Confira: o nome aparece na lista de memórias
Responda: ok
```

Com o ok, **IA roda** `uv run ferramentas/sessao.py memoria <n> "<MUSICA HIGH | MUSICA ORIGINAL | FILME>" --por ia` (a memória veio
da proposta da IA; se o Denis mudou o lote por conta própria, `--por denis`). É só o registro para o
painel (`estado/memorias.json`); quem salvou no aparelho foi ele, pelo app. Registre antes do
`passo`.

Logo depois, **IA roda** `uv run ferramentas/sessao.py modo "<modo>"`: a memória recém-salva é a
que está tocando, e a Vitrine do painel passa a dizer "Som ajustado pela KARINA · Modo: <modo>".
Memória → modo: MUSICA HIGH → `"Música High"`, MUSICA ORIGINAL → `"Música Original"`, FILME → `"Filme"`. Só
esses três nomes (nada técnico vai para a TV); `--por ia` é o padrão.

**Sempre que pedir para carregar uma memória no app** (aqui, na etapa 6 ou quando o Denis
disser que trocou), com o ok dele **IA roda** o mesmo `sessao.py modo "<modo>"` da tabela acima.
Se ele carregou algo fora das três, `sessao.py modo nenhum` (a Vitrine fica só com "Som ajustado
pela KARINA"). O modo é declarado, não lido do aparelho: não registre sem o ok.

Depois do passo 9: `✅ Pronto para sábado. O resto é melhoria.` e proponha congelar se já
passou das 16h30.

---

## Etapa 6 · Camadas de gênero (M = 1 + 2 por gênero; melhoria)

Camada = só o EQ de 15 bandas por cima da MUSICA ORIGINAL. Corte, limiter e níveis não mudam. Salva no
celular (preset de EQ do app), não numa memória do DSP.

```
Etapa 6 · Passo 1 de ? — Quais gêneros
Faça: pense nas músicas de sábado
Confira: nada
Responda: até 3 gêneros, separados por vírgula
```

Com N gêneros, M = 1 + 2N (a partir daqui as telas dizem `de M`). O gênero i (1, 2 ou 3, na
ordem em que ele respondeu) tem passos fixos: **ouvir = 2i, salvar = 2i + 1** (gênero 1 → 2 e 3;
gênero 2 → 4 e 5; gênero 3 → 6 e 7), mesmo que um gênero seja pulado. Nota: `nota_<genero>`
(ex.: `nota_rock`; `--refazer` a cada rodada). Pontos de partida (todos por corte, sobre a
MUSICA ORIGINAL): rock/pop `020:5=-3.0 020:6=-2.0`; eletrônico/funk `020:8=-1.5 020:10=-1.5` (sobram
grave e agudo); sertanejo/pagode/voz `020:4=-1.5 020:12=-1.5` (voz à frente). Antes de cada
gênero, **IA roda** `--simular` a partir do estado lido.

```
Etapa 6 · Passo <2i> de M — <Gênero>: aplicar e ouvir
Faça: nada; eu aplico <lote>; depois toque 1 min de <gênero> pelo Bluetooth
Confira: <sensação-alvo>
Responda: nota de 1 a 5, ou uma palavra do que incomoda
```

```
Etapa 6 · Passo <2i+1> de M — <Gênero>: salvar no celular
Faça: app → EQ 15 bandas → salvar preset no celular com o nome "<Gênero>"; feche o app
Confira: o preset aparece na lista do app
Responda: ok
```

Com o ok, **IA roda** `uv run ferramentas/sessao.py preset "<Gênero>" --por ia` (`--por denis` se
a camada foi ideia dele). Mesmo nome substitui o registro anterior. Registre antes do `passo`.

Ao fim: `Faça: no app, carregue a memória MUSICA ORIG e feche o app` (o DSP ficou com a última
camada); com o ok, **IA roda** `uv run ferramentas/sessao.py modo "Música Original"` e uma leitura
para confirmar o EQ da MUSICA ORIGINAL.

---

## Etapa 7 · Compressor do filme no notebook (M = 3; melhoria)

O PRO só tem limitador; a dinâmica do filme (fala baixa, explosão alta) é tratada no notebook.
Caminho 1: "Equalização de volume" do driver do Windows. Caminho 2: Equalizer APO (ele já usa) com
um VST de compressor (ex.: ReaComp, do pacote gratuito ReaPlugs): threshold −20 dB, ratio 3:1,
attack 10 ms, release 200 ms. O Denis baixa e instala; a IA não baixa nada.

```
Etapa 7 · Passo 1 de 3 — Equalização de volume do Windows
Faça: Configurações → Sistema → Som → saída (fone/P2) → Propriedades → Aprimoramentos → marque "Equalização de volume"
Confira: a opção existe e ficou marcada
Responda: ok, ou "não tem"
```

Passo 2 só se "não tem"; senão `Etapa 7 · Passo 2 de 3 — APO não precisa. Responda: ok`.

```
Etapa 7 · Passo 2 de 3 — Compressor no Equalizer APO
Faça: instale o ReaPlugs; no Configuration Editor do APO, adicione "VST plugin" → ReaComp (threshold −20 dB, ratio 3:1, attack 10 ms, release 200 ms); salve
Confira: o APO mostra o plugin ativo na saída do P2
Responda: ok
```

```
Etapa 7 · Passo 3 de 3 — Testar com uma cena
Faça: memória FILME no app (feche o app depois); toque uma cena com diálogo baixo seguido de explosão
Confira: a fala sobe sozinha e a explosão não estoura
Responda: nota de 1 a 5
```

---

## Etapa 8 · Congelar (M = 1; às 16h30, ou quando ele disser que chega)

```
Etapa 8 · Passo 1 de 1 — Congelar
Faça: no app, confira que as memórias 1, 2 e 3 têm os nomes certos e feche o app
Confira: nada mais vai ser escrito hoje; a festa roda com as memórias fixas
Responda: ok
```

Depois: **IA roda** uma leitura final, registra o passo e mostra uma linha com o caminho do
retrato e do diário (`estado/sessoes/<data>.md`).
