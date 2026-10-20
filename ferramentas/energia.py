#!/usr/bin/env python3
"""Liga e desliga os amplificadores (remote out, `*252*1*` / `*252*0*`) do PRO 2.4BT + AUDIO
pelo notebook, atrás do guardião. É o botão ON/OFF da Home do app (MAPA §4); a energia do
processador em si não tem comando. Liberado pelo Denis em 18/10/2026.

Uso (na pasta do projeto, com o processador ligado e o celular desconectado dele):

    uv run ferramentas/energia.py desliga
        Manda `*252*0*`. Sempre aprovado, sem leitura antes.
    uv run ferramentas/energia.py liga
        Lê o estado completo e só manda `*252*1*` com o guardião sem nenhuma violação
        (gd.avaliar_energia). Com violação, recusa e diz qual.

O aparelho não tem leitura do 252: confira pelo som ou pelo LED do módulo.
Log bruto (senha mascarada) em estado/log-ble/ e registro (tipo "energia") em estado/ajustes/.

Códigos de saída: 0 enviado; 1 erro de uso ou guardiao.json ilegível; 2 senha recusada;
3 processador não encontrado ou sem conexão; 4 leitura incompleta (nada enviado); 5 o
processador não respondeu à senha; 6 recusado (nada enviado); 7 interrompido no envio.
"""

from __future__ import annotations

import argparse
import asyncio
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

import ajustar_processador as ap  # noqa: E402
import cliente_ble as cb  # noqa: E402
import guardiao as gd  # noqa: E402
import ler_processador as lp  # noqa: E402
import protocolo_taramps as pt  # noqa: E402
import tom as tm  # noqa: E402

RAIZ = lp.RAIZ

(SAIDA_OK, SAIDA_USO, SAIDA_SENHA, SAIDA_SEM_APARELHO, SAIDA_LEITURA, SAIDA_SEM_RESPOSTA,
 SAIDA_RECUSADO, SAIDA_INTERROMPIDO) = range(8)


async def _enviar(cliente, transporte, contexto, ligar: bool, cfg, estado: Path, carimbo: str,
                  reg: tm.RegistroEspecial, senha: str, caminho_log: Path) -> tuple[int, str]:
    retrato = None
    if ligar:
        print("Senha aceita. Lendo o estado antes de ligar…")
        retrato = await lp.leitura_completa(cliente, transporte, contexto, senha, caminho_log)
        if not lp.retrato_completo(retrato):
            print(f"Leitura incompleta ({retrato['lidos']} de {retrato['total']}). Nada foi enviado.")
            return SAIDA_LEITURA, "leitura incompleta"
        backup = tm.gravar_backup(estado, carimbo, retrato, reg)
        print(f"Backup: {lp.relativo(backup)}")
    decisao = gd.avaliar_energia(ligar, retrato, cfg)
    reg.dados["decisao"] = decisao.como_dict()
    if not decisao.aprovado:
        tm.mostrar_recusas(decisao.recusas, "Nada foi enviado.")
        return SAIDA_RECUSADO, "recusado"
    (texto,) = decisao.aprovacao.comandos
    print(f"Guardião: APROVADO. Enviando {texto}")
    reg.dados["em_andamento"] = texto
    await cliente.enviar_especial(decisao.aprovacao, texto)
    reg.dados["comandos"].append({"texto": texto, "status": "enviado"})
    reg.dados["em_andamento"] = None
    reg.dados["aplicados"] = list(reg.dados["lote"])
    print("Amplificadores " + ("LIGADOS." if ligar else "DESLIGADOS."))
    return SAIDA_OK, "enviado"


async def _sessao(ligar: bool, cfg, senha: str, transporte, estado: Path, tempos: dict,
                  carimbo: str, reg: tm.RegistroEspecial) -> int:
    pasta_log = estado / "log-ble"
    pasta_log.mkdir(parents=True, exist_ok=True)
    caminho_log = pasta_log / f"{carimbo}-energia.log"
    reg.dados["log"] = lp.relativo(caminho_log)
    codigo, status = SAIDA_INTERROMPIDO, "interrompido"
    with open(caminho_log, "w", encoding="utf-8") as arquivo_log:
        log = pt.LogBruto(arquivo_log, senha)
        cliente = cb.ClienteTaramps(transporte, log, **tempos)
        reg.gravar("em andamento", fim=False)
        try:
            erro, contexto = await lp.conectar_e_autenticar(cliente, transporte, log, senha)
            if erro is not None:
                codigo, status = erro, tm._STATUS_CONEXAO.get(erro, "sem conexão")
            else:
                codigo, status = await _enviar(cliente, transporte, contexto, ligar, cfg, estado,
                                               carimbo, reg, senha, caminho_log)
        except Exception as erro:  # noqa: BLE001 - queda, timeout ou erro do BLE
            log.anotar(f"erro do BLE: {erro!r}")
            print("A conexão caiu." if isinstance(erro, cb.ConexaoCaiu)
                  else f"Erro na conversa com o processador: {erro}")
            if reg.dados["em_andamento"] is not None:
                print("O comando pode ter chegado. Confira pelo som ou pelo LED do módulo.")
                codigo, status = SAIDA_INTERROMPIDO, "interrompido"
            else:
                print("Nada foi enviado.")
                codigo, status = SAIDA_LEITURA, "queda" if isinstance(erro, cb.ConexaoCaiu) else "erro"
        finally:
            reg.gravar(status)
            await cliente.encerrar()
    return codigo


def main(argv: list[str] | None = None, *, transporte=None, estado: Path | None = None,
         tempos: dict | None = None) -> int:
    for fluxo in (sys.stdout, sys.stderr):
        try:
            fluxo.reconfigure(encoding="utf-8", errors="replace")
        except (AttributeError, ValueError):
            pass
    parser = argparse.ArgumentParser(
        description="Liga ou desliga os amplificadores (remote out), atrás do guardião.")
    parser.add_argument("acao", choices=("liga", "desliga"))
    args = parser.parse_args(argv)
    estado = estado or RAIZ / "estado"
    ligar = args.acao == "liga"

    try:
        cfg = gd.carregar_config(estado / "guardiao.json")
    except gd.ConfigInvalida as erro:
        print(f"guardiao.json inválido: {erro}. Nada foi feito.")
        return SAIDA_USO
    try:
        senha = lp.obter_senha()
    except lp.SenhaInvalida as erro:
        print(erro)
        return SAIDA_USO
    if senha is None:
        print("A senha tem 4 letras ou números (TARAMPS_SENHA, ~/.taramps/senha ou digitada).")
        return SAIDA_USO

    carimbo = ap._carimbo(estado)
    reg = tm.RegistroEspecial(estado, carimbo, "energia", [f"remote out {args.acao}"], cfg)
    codigo = asyncio.run(_sessao(ligar, cfg, senha, transporte or cb.TransporteBleak(), estado,
                                 tempos or {}, carimbo, reg))
    if reg.caminho.exists():
        print(f"Resultado: {lp.relativo(reg.caminho)}")
    return codigo


if __name__ == "__main__":
    sys.exit(main())
