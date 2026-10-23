---
title: 'História 3b: UX — Setup e memórias no celular'
type: 'ux'
created: '2026-10-23'
status: 'done'
route: 'bmad-ux (interativa com o Denis)'
context:
  - '{project-root}/_bmad-output/planning-artifacts/sprint-change-proposal-2026-10-23.md'
  - '{project-root}/_bmad-output/planning-artifacts/ux-designs/ux-taramps-automacao-2026-10-21/EXPERIENCE.md'
  - '{project-root}/_bmad-output/planning-artifacts/ux-designs/ux-taramps-automacao-2026-10-21/DESIGN.md'
---

# História 3b: UX — Setup e memórias no celular

Revisão do EXPERIENCE.md e do DESIGN.md (pasta `ux-taramps-automacao-2026-10-21`) feita com a skill `bmad-ux`, em modo Update, com o Denis, em 23/10/2026. A lista do que revisar era a seção 4.6 da proposta de mudança de 23/10. As decisões estão no `.memlog.md` da pasta do UX, da entrada "Início da revisão da história 3b" em diante.

## O que ficou decidido

- **Início:** 3 botões com as memórias do celular usadas por último naquele Setup, fixas no lugar, mais "…" para a lista completa (com as 3 do aparelho numa seção à parte). Setup no cabeçalho ("Setup Casa 2.1 ›"); preset como chip dentro do cartão da memória em uso. Selo HERDADO só nas listas.
- **Setup:** ao conectar, entra direto se o estado bate com o último Setup; na dúvida, muta e mostra o que não bate, com Escolher o Setup, Criar Setup novo e Seguir sem Setup. Com o app conectado, tocar no nome do Setup volta a essa escolha.
- **Salvar:** passo à parte, um só botão; cada mudança vai para a camada dona dela (calibração → Setup; caráter → memória; 15 bandas → atualizar o preset ou criar um novo; herdado só cria novo). Som carregado do aparelho: a folha pergunta se mantém a calibração do Setup ou atualiza o Setup.
- **Memórias e presets:** memória nova parte do som atual; cada memória lembra o último preset e o traz ao ser aplicada. Equilíbrio L/R no Ajuste, em Avançado.
- **Player:** só a playlist da memória em uso.
- **Fluxos:** 1 e 4 refeitos; fluxo novo "Levar o processador para o carro".

## O que mudou fora do UX

- `SPEC.md`: CAP-4, CAP-5 e CAP-16.
- `failure-modes.md`: regra 5.
- `stories.yaml`: `invoke_dev_with` das histórias 8c e 8d.

## Auto Run Result

Status: done (feita de forma interativa, fora do build-auto).
Desenho: `mockups/key-screens-3b.html` (Início, Setup em dúvida, folha do Salvar).
