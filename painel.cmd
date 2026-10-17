@echo off
rem Painel do prototipo: sobe http://127.0.0.1:8765 e abre o navegador. Feche esta janela para parar.
rem --rede: celulares no mesmo Wi-Fi podem acompanhar (so olhar) pelo link que aparece aqui.
cd /d "%~dp0"
uv run ferramentas/painel.py --rede
if errorlevel 1 pause
