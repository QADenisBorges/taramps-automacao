# taramps-automacao

Software para automatizar o uso do app da Taramps no celular.

## Planejamento com BMAD Method

O projeto usa o [BMAD Method](https://github.com/bmad-code-org/BMAD-METHOD) v6.12.2 (módulos core + bmm), configurado para o Claude Code e em português:

- `_bmad/` — núcleo, módulo BMad Method e configuração
- `.claude/skills/bmad-*` — agentes (analista, PM, UX, arquiteto, dev) e workflows
- `_bmad-output/` — documentos gerados (brief, PRD, arquitetura, histórias)

Para começar, invoque a skill `bmad-help` no Claude Code.
