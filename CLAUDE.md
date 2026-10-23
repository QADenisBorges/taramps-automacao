# Regras do projeto (valem para qualquer sessão)

## Git (regra do Denis, 23/10/2026)

- O trabalho é sempre no `main`. Não criar branch nem worktree.
- Cada história fecha com o seu próprio commit no `main`, com as linhas de atribuição.
- Sem push e sem PR, a menos que o Denis peça na hora.

## bmad-build-auto

Uma chamada roda várias histórias seguidas, no mesmo contexto, na ordem do `stories.yaml`. Ela para numa história que precise do Denis ou do aparelho real. As regras estão em `_bmad/custom/bmad-build-auto.toml`.
