# Alto-falantes: a ficha e as contas que a conferem

Regras gerais dos parâmetros de alto-falante (Thiele-Small). Servem à ficha técnica (conferir se a ficha fecha) e à
receita (o corte e o alcance de cada caixa). As unidades são as da ficha do app.

## Regras

- **O Qts fecha com o Qes e o Qms.** Qts = Qes × Qms / (Qes + Qms). Uma ficha cujo Qts não sai dessa conta (além do
  arredondamento dela) tem um dado errado: marque com o aviso, sem corrigir o dado.
  Fonte: Small, R. H., "Closed-Box Loudspeaker Systems, Part I: Analysis", *Journal of the Audio Engineering Society*, vol. 20, nº 10 (1972).
- **A sensibilidade sai dos parâmetros.** Eficiência η0 = (4π² / c³) × Fs³ × Vas / Qes (Vas em m³, c ≈ 343 m/s), e o
  nível a 1 W e 1 m é cerca de 112 + 10 × log10(η0) dB. A conta vale na faixa em que o cone é controlado pela massa, em
  meio espaço; compare com a sensibilidade declarada e diga os dois números quando não fecharem.
  Fonte: Small, R. H., "Direct-Radiator Loudspeaker System Analysis", *Journal of the Audio Engineering Society*, vol. 20, nº 5 (1972); Dickason, V., *Loudspeaker Design Cookbook*, 7ª ed. (Audio Amateur Press, 2006).
- **Sensibilidade a 2,83 V não é a 1 W.** P = V² / Z: 2,83 V dá 1 W em 8 Ω e 2 W em 4 Ω (3 dB a mais). Compare as
  sensibilidades na mesma referência.
  Fonte: Davis, D. e Patronis, E., *Sound System Engineering*, 3ª ed. (Focal Press, 2006).
- **O Qes amarra a massa, o Bl e a resistência.** Qes = 2π × Fs × Mms × Re / Bl². Com Mms, Re, Fs e Bl na ficha,
  o Qes da conta tem de bater com o declarado.
  Fonte: Small, R. H., "Direct-Radiator Loudspeaker System Analysis", *Journal of the Audio Engineering Society*, vol. 20, nº 5 (1972).
- **O Vas e a Fs amarram a suspensão.** Vas = ρ × c² × Sd² × Cms e Fs = 1 / (2π × √(Mms × Cms)). Com a Cms ou a
  Mms na ficha, as duas contas conferem o Vas, a Fs e o Sd.
  Fonte: Beranek, L. e Mellow, T., *Acoustics: Sound Fields and Transducers* (Academic Press, 2012).
- **A unidade errada muda o valor por 10, 100, 1000 ou mais.** Sd em cm² (1 m² = 10 000 cm²), Xmax em mm, Le em mH, Vas em L
  (1 m³ = 1000 L). A porta recusa um valor fora da faixa larga do campo, que é como a unidade errada aparece.
  Fonte: o `GET /ajuda` da porta, seção "Proposta de ficha" (as unidades e as faixas de cada campo, de `PropostaFicha.Faixas` no app).
- **Na caixa dutada, abaixo da Fb o cone fica solto.** Abaixo da sintonia do duto, a carga do ar some e a excursão
  cresce depressa; um passa-alta perto da Fb protege o cone.
  Fonte: Thiele, A. N., "Loudspeakers in Vented Boxes", *Journal of the Audio Engineering Society*, vol. 19, nº 5 e 6 (1971); Dickason, V., *Loudspeaker Design Cookbook*, 7ª ed. (Audio Amateur Press, 2006).
- **Na caixa fechada, o grave cai abaixo da ressonância do conjunto.** Abaixo da Fc (a Fs com o ar da caixa), a
  resposta cai 12 dB por oitava; reforçar o grave abaixo dela gasta excursão e tensão.
  Fonte: Small, R. H., "Closed-Box Loudspeaker Systems, Part I: Analysis", *Journal of the Audio Engineering Society*, vol. 20, nº 10 (1972).
- **Oficial é do fabricante.** Origem "oficial" é o que o fabricante publica (ficha, manual, site); "outra-fonte" é
  loja, fórum ou medida de terceiros. Um dado sem fonte, ou não achado, fica fora: nunca se inventa.
  Fonte: `spec-agente-memoria/lado-do-app.md` ("Proposta de ficha") e `spec-agente-memoria/SPEC.md` (CAP-6).
