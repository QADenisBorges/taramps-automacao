---
title: 'Ideia forjada: Setup, memórias de rendimento e teste de playlist'
created: '2026-10-22'
status: hardened
fontes:
  - _bmad-output/ideias/teste-de-playlist.md
  - Desktop/Documento de referencia.txt (Denis)
---

# Setup, memórias de rendimento e teste de playlist

Todas as decisões abaixo são do Denis, tomadas no forge de 22/10/2026.

## Conceitos

- **Setup:** uma instalação física (2.1 de casa, carro…), sempre com o mesmo PRO 2.4BT. O Denis terá vários. Ao conectar, ele escolhe o Setup. Sinais automáticos ajudam: estado lido do aparelho, localização (sozinha falha: o carro pode estar na garagem de casa) e talvez o voltímetro. Se houver dúvida, o app para o som, muta e pede confirmação.
- **As memórias do processador (3 posições) saem do app.** Elas ficam só no app oficial da Taramps.
- **Três camadas:**
  1. **Setup:** calibração (atraso, fase, corte, equilíbrio L/R). Vale para todas as memórias dele.
  2. **Memória (no celular):** Setup + caráter (limiter/compressor, EQ de saída, nível do sub) + playlist + teto. Carregar = aplicar ao vivo, com cópia antes e releitura.
  3. **Preset:** só o EQ gráfico de 15 bandas (Sertanejo, Rock, Ação, Diálogo…), trocado na hora. Diálogo com mais compressão é memória, não preset.

## Limites

- O **limite físico** de cada canal é **calculado pelo Setup**: o menor entre o térmico (potência RMS na impedância nominal), a excursão (Xmax, com o corte) e o corte do amplificador. Ele vira dB do processador pelo ganho do módulo medido (0 dB = 2,0 V). A referência de método é `_bmad-output/regulagem/memoria-spl.md`, seção 12. As configurações atuais não servem de referência.
- **Dado desconhecido:** o usuário preenche. O que ele não souber entra com um valor conservador, marcado como estimado, sem bloquear o uso. Exemplo: sub do 2.1 em 200 W (28,6 V) até a compra ser confirmada.
- A **calibração da saída** (tom + multímetro) é feita na regulagem de cada memória. Se o ganho do módulo mudar, todas as memórias do Setup vão para "revalidar".
- **O limiter, em todos os níveis, fica preso na excursão e no corte do amplificador.** Ele é a rede para o que a detecção de fonte não pegar.

## Níveis de rendimento

| Nível | Para quê | Fonte | Ajuste |
|---|---|---|---|
| Baixo | Qualidade, uso prolongado | Qualquer | Pior caso (0 dBFS) com margem maior |
| Médio | Qualidade equilibrada | Qualquer | Pior caso com margem menor |
| Alto | SPL máximo validado | **Só a playlist auditada** | Teto da playlist até o limite físico |
| Extra | SPL ampliado | **Só a playlist auditada** | Passa **só do térmico**, por tempo; depois, descanso com o BT travado (A2DP `251` desligado + mute `100`) |

O Extra só aparece com um modelo térmico válido. Sem dados, ele não aparece e o app nunca inventa um tempo seguro. Essa decisão diverge de propósito do §11.2 do documento de referência.

## Teto da playlist (Alto e Extra)

- **Um parâmetro só para todas as músicas:** o master, que fica na menor folga entre músicas e canais, menos a margem. Se uma música passa do limite, desce para todas. Se nenhuma passa, sobe até quase encostar.
- **Um teto por preset.** Ao trocar o preset, o master vai para o teto do novo.
- **"Otimizar configuração na playlist":**
  1. **Análise digital** em segundo plano: simula a cadeia por canal e acha os trechos mais fortes.
  2. **Teste a seco** com o módulo desligado (`252`), tocando só esses trechos (~10 s, 2 por música) e escutando o clip.
- **Música nova:** é analisada em segundo plano e não toca até terminar. Dá para adicionar outras enquanto isso.
- **"Quem está pesando":** cada música mostra quantos dB tira do ajuste.
- **Clip como fiscal no uso:** se ceifar, o master desce um passo e a música é marcada (canal, momento). Clips repetidos que a análise não previa mandam a memória para "revalidar".
- **Exclusividade = detectar e parar:** outro player tocando faz o app mutar e pausar, com mensagem. O volume do celular fica travado no valor da conta.

## Criação da memória

- Fluxo de **uma pergunta por tela**, sempre com "Outro" quando fizer sentido. Os textos do Denis são exemplo, não para copiar.
- **Mapeamento sem retrabalho:**
  - o que é físico sai de **regras fixas**;
  - as perguntas escolhem uma **receita de partida**, com o motivo de cada valor à vista;
  - o gosto é afinado por **escuta A/B** com músicas da playlist, uma coisa por vez;
  - as escolhas ficam num **perfil de ouvinte**, reaproveitado em outras memórias e Setups.
- A IA livre só explica e trata o "Outro". Nenhum valor sem motivo, como aconteceu com os 11 V da JBL.

## Rejeitado

- Setup só do 2.1, sem cadastro: o Denis vai usar o mesmo processador no carro.
- Preset como som completo, e preset mexendo em EQ de saída ou crossover: desfaria a calibração.
- Teto pelo limiter da memória: na FILME ele é compressor de propósito.
- Teto único pelo pior preset: limita todos os presets pelo mais pesado.
- Playlist opcional subindo o volume no Baixo/Médio: esses níveis são de qualidade.
- Tocar a playlist inteira no teste a seco (~80 min): só os trechos fortes bastam.
- IA ajustando livre, como nas sessões de regulagem.

## Suposições a medir (história 12)

- O que o bit `150` (e o `111`) marca de fato.
- O nível do BT com o player do próprio app (os −13 dB foram medidos com o YouTube Music).
- Se o `251` desliga de fato o A2DP.
- O que o voltímetro `005` mede.

## Aberto para a spec

- As regras de revalidação: o que pede reanálise completa, parcial ou só conferência.
- O valor conservador quando faltam Xmax ou Fb.
- O tempo e o descanso do Extra, pela conta térmica.
- A margem de cada nível.

## Próximos passos

1. **Agora, antes da história 10:** `bmad-correct-course` na `spec-app-controle-taramps`. Ele tira as memórias do processador e cria o Setup, a memória no celular e o preset de 15 bandas. Isso mexe nas histórias 6, 8, 8b e 9b (21 arquivos).
2. **Depois da história 12:** uma spec nova, a do **Rendimento** (`bmad-spec` com este arquivo).
