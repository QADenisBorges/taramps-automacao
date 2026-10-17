@echo off
rem Painel do prototipo: sobe http://localhost:8765 e abre o navegador. Feche esta janela para parar.
cd /d "%~dp0"
uv run ferramentas/painel.py
if errorlevel 1 pause
