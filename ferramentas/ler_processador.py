#!/usr/bin/env python3
"""Lê o estado do PRO 2.4BT + AUDIO pelo BLE do notebook (Marco 1). Só leitura.

Uso (na pasta do projeto, com o processador ligado e o celular desconectado dele):

    uv run ferramentas/ler_processador.py
        Faz as 84 leituras do Marco 1 e grava o retrato em
        estado/leituras/AAAA-MM-DD_HHMM.json e o log bruto em estado/log-ble/.

    uv run ferramentas/ler_processador.py --bruto "#081#0#"
        Manda uma única consulta e mostra a resposta crua (para depurar).

    uv run ferramentas/ler_processador.py --conferir estado/leituras/ARQUIVO.json
        Mostra a amostra de conferência com o app, um valor por vez. Enter = igual,
        x = diferente. O resultado fica ao lado do retrato (-conferencia.json).

A senha BT vem da variável TARAMPS_SENHA ou é pedida sem eco. Ela não é gravada em
lugar nenhum: no log aparece como *000*****.

Códigos de saída: 0 tudo lido; 1 alguma consulta falhou ou erro de uso; 2 senha
recusada; 3 processador não encontrado ou sem conexão; 4 leitura interrompida: a
conexão caiu ou o aparelho parou de responder (retrato parcial marcado como
incompleto), ou erro do BLE antes da leitura (sem retrato); 5 o processador não
respondeu à senha (sem retrato).
"""

from __future__ import annotations

import argparse
import asyncio
import datetime as dt
import getpass
import json
import os
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

import cliente_ble as cb  # noqa: E402
import protocolo_taramps as pt  # noqa: E402

RAIZ = Path(__file__).resolve().parents[1]
NAO_ENCONTRADO = "Processador não encontrado. O celular ainda está conectado a ele?"

SAIDA_OK, SAIDA_FALHAS, SAIDA_SENHA, SAIDA_SEM_APARELHO, SAIDA_QUEDA, SAIDA_SEM_RESPOSTA = range(6)


def _obter_senha() -> str | None:
    senha = os.environ.get("TARAMPS_SENHA")
    if not senha:
        try:
            senha = getpass.getpass("Senha BT do processador: ")
        except (EOFError, KeyboardInterrupt):
            return None
    senha = senha.strip()
    try:
        pt.montar_autenticacao(senha)
    except ValueError:
        return None
    return senha


def _carimbo(pasta_leituras: Path, pasta_log: Path) -> str:
    base = dt.datetime.now().strftime("%Y-%m-%d_%H%M")
    carimbo, n = base, 2
    while any(caminho.exists() for caminho in (
            pasta_leituras / f"{carimbo}.json", pasta_log / f"{carimbo}.log",
            pasta_log / f"{carimbo}-bruto.log")):
        carimbo, n = f"{base}-{n}", n + 1
    return carimbo


def _relativo(caminho: Path) -> str:
    try:
        return caminho.resolve().relative_to(RAIZ).as_posix()
    except ValueError:
        return str(caminho)


# Nomes públicos para ajustar_processador.py.
obter_senha = _obter_senha
relativo = _relativo


# --------------------------------------------------------------------------- sessão BLE


async def procurar_e_conectar(transporte, log: pt.LogBruto) -> tuple[int | None, str | None, str | None]:
    """Scan e conexão, com as mensagens do leitor. Devolve (código de saída do erro ou
    None se conectou, nome, endereço). Também usado por ajustar_processador.py."""
    print("Procurando o processador…")
    log.anotar("procurando o processador (UUID 0x2055 ou serviço e58f)")
    try:
        dispositivo = await transporte.procurar()
    except Exception as erro:  # noqa: BLE001 - ex.: Bluetooth do notebook desligado
        log.anotar(f"erro no scan: {erro!r}")
        print("Não consegui procurar. O Bluetooth do notebook está ligado?")
        return SAIDA_SEM_APARELHO, None, None
    if dispositivo is None:
        log.anotar("não encontrado")
        print(NAO_ENCONTRADO)
        return SAIDA_SEM_APARELHO, None, None
    nome = getattr(dispositivo, "name", None)
    endereco = getattr(dispositivo, "address", None)
    log.anotar(f"encontrado: {nome} {endereco}")

    print("Conectando…")
    try:
        await transporte.conectar(dispositivo)
    except cb.ConexaoFalhou as erro:
        log.anotar(f"falha na conexão: {erro}")
        print("Não consegui conectar. Desligue e ligue o processador e tente de novo.")
        return SAIDA_SEM_APARELHO, nome, endereco
    log.anotar(f"propriedades: {getattr(transporte, 'propriedades', {})}")
    return None, nome, endereco


def firmware_seguro(auth: cb.Autenticacao, senha: str) -> None:
    """Nunca mostrar nem gravar um firmware igual à senha."""
    if auth.firmware and senha.upper() in auth.firmware.upper():
        auth.firmware = None


def montar_retrato(*, leituras: list[dict], completo: bool, inicio: dt.datetime,
                   nome: str | None, endereco: str | None, transporte, auth: cb.Autenticacao,
                   senha: str, orfaos: list[str], caminho_log: Path) -> dict:
    """O retrato (formato 1) de uma leitura completa ou parcial."""
    falhas = [x for x in leituras if x.get("valor") is None]
    motivo = None if completo else next(
        (x["motivo"] for x in reversed(leituras) if x.get("motivo")), "incompleto")
    carga = auth.carga or ""
    if senha and senha.upper() in carga.upper():
        carga = "(omitida: continha a senha)"
    return {
        "formato": 1,
        "aparelho": "PRO 2.4BT + AUDIO",
        "status": "completo" if completo else "incompleto",
        "motivo": motivo,
        "inicio": inicio.isoformat(timespec="seconds"),
        "fim": dt.datetime.now().isoformat(timespec="seconds"),
        "dispositivo": {"nome": nome, "endereco": endereco},
        "caracteristicas": getattr(transporte, "propriedades", {}),
        "firmware": auth.firmware,
        "resposta_000": carga,
        "total": len(leituras),
        "lidos": len(leituras) - len(falhas),
        "leituras": leituras,
        "falhas": [
            {"codigo": x["codigo"], "canal": x["canal"], "nome": x["nome"], "motivo": x.get("motivo")}
            for x in falhas
        ],
        "orfaos": orfaos,
        "log": _relativo(caminho_log),
    }


async def _sessao(args, senha: str, transporte, estado: Path, tempos: dict) -> int:
    pasta_leituras = estado / "leituras"
    pasta_log = estado / "log-ble"
    pasta_log.mkdir(parents=True, exist_ok=True)
    carimbo = _carimbo(pasta_leituras, pasta_log)
    caminho_log = pasta_log / (f"{carimbo}-bruto.log" if args.bruto else f"{carimbo}.log")
    inicio = dt.datetime.now()

    with open(caminho_log, "w", encoding="utf-8") as arquivo_log:
        log = pt.LogBruto(arquivo_log, senha)
        cliente = cb.ClienteTaramps(transporte, log, **tempos)

        erro_conexao, nome, endereco = await procurar_e_conectar(transporte, log)
        if erro_conexao is not None:
            return erro_conexao

        try:
            print("Conectado. Enviando a senha…")
            auth = await cliente.autenticar(senha)
            if auth.motivo == "recusada":
                print("Senha recusada.")
                return SAIDA_SENHA
            if not auth.ok and not args.bruto:
                print("O processador não respondeu à senha.")
                return SAIDA_SEM_RESPOSTA

            if args.bruto:
                if not auth.ok:
                    print("Aviso: sem resposta à senha. Mando a consulta mesmo assim.")
                recebidos = await cliente.bruto(args.bruto)
                if not recebidos:
                    print(f"Sem resposta em {cliente.timeout:g} s.")
                for dados in recebidos:
                    hexa, texto = pt.mascarar(dados, senha)
                    print(f"RX |{texto}|  {hexa}")
                print(f"Log: {_relativo(caminho_log)}")
                return SAIDA_OK if recebidos else SAIDA_FALHAS

            firmware_seguro(auth, senha)
            firmware = f" Firmware {auth.firmware}." if auth.firmware else ""
            print(f"Senha aceita.{firmware} Lendo os parâmetros…")
            consultas = pt.consultas_marco1()

            def progresso(feitos: int, total: int) -> None:
                print(f"\r  {feitos} de {total}", end="" if feitos < total else "\n", flush=True)

            leituras, completo = await cliente.ler_tudo(consultas, progresso)
        except cb.ConexaoCaiu:
            print("A conexão caiu.")
            return SAIDA_QUEDA
        except Exception as erro:  # noqa: BLE001 - falha do BLE antes da leitura
            log.anotar(f"erro do BLE: {erro!r}")
            print(f"Erro na conversa com o processador: {erro}")
            return SAIDA_QUEDA
        finally:
            await cliente.encerrar()

        falhas = [x for x in leituras if x.get("valor") is None]
        fora = [x for x in leituras if x.get("fora_da_faixa")]
        retrato = montar_retrato(
            leituras=leituras, completo=completo, inicio=inicio, nome=nome, endereco=endereco,
            transporte=transporte, auth=auth, senha=senha, orfaos=cliente.orfaos,
            caminho_log=caminho_log)
        pasta_leituras.mkdir(parents=True, exist_ok=True)
        caminho_retrato = pasta_leituras / f"{carimbo}.json"
        caminho_retrato.write_text(json.dumps(retrato, ensure_ascii=False, indent=2), encoding="utf-8")

    _resumo(retrato, falhas, fora, caminho_retrato, caminho_log)
    if not completo:
        return SAIDA_QUEDA
    return SAIDA_FALHAS if falhas else SAIDA_OK


def _descrever(item: dict) -> str:
    p = pt.POR_CODIGO[item["codigo"]]
    return pt.Consulta(p, item.get("canal")).descricao()


def _resumo(retrato: dict, falhas: list, fora: list, caminho_retrato: Path, caminho_log: Path) -> None:
    status = "" if retrato["status"] == "completo" else " INCOMPLETO."
    firmware = f" Firmware {retrato['firmware']}." if retrato["firmware"] else ""
    print(f"Pronto.{status} {retrato['lidos']} lidos, {len(falhas)} falhas.{firmware}")
    if falhas:
        print("Falharam:")
        for x in falhas:
            print(f"  - {_descrever(x)}: {x.get('motivo')}")
    if fora:
        print("Fora da faixa do app:")
        for x in fora:
            print(f"  - {_descrever(x)}: {x.get('cru')}")
    print(f"Retrato: {_relativo(caminho_retrato)}")
    print(f"Log: {_relativo(caminho_log)}")


# --------------------------------------------------------------------------- conferência


def _conferir(caminho: Path, entrada) -> int:
    try:
        retrato = json.loads(caminho.read_text(encoding="utf-8"))
    except (OSError, ValueError) as erro:
        print(f"Não consegui abrir o retrato: {erro}")
        return SAIDA_FALHAS
    amostra = pt.amostra_conferencia(retrato)
    if not amostra:
        print("O retrato não tem valores lidos para conferir.")
        return SAIDA_FALHAS

    print(f"Conferência: {len(amostra)} valores. Use a mesma memória da leitura.")
    print("Enter = igual no app. x = diferente.")
    itens = []
    parar = False
    for i, item in enumerate(amostra, 1):
        resultado = dict(item, confere=None, app=None)
        if not parar:
            onde = f" · {item['onde']}" if item["onde"] else ""
            print(f"\n[{i}/{len(amostra)}] Tela {item['tela']}{onde}")
            print(f"{item['rotulo']}: {item['mostrado']}")
            try:
                resposta = entrada("Igual? ").strip().lower()
                while resposta not in ("", "x"):
                    resposta = entrada("Só Enter (igual) ou x (diferente): ").strip().lower()
                if resposta == "x":
                    resultado["confere"] = False
                    resultado["app"] = entrada("O que o app mostra? (Enter pula) ").strip() or None
                else:
                    resultado["confere"] = True
            except (EOFError, KeyboardInterrupt):
                parar = True
                print("\nParei aqui. O que foi conferido está salvo.")
        itens.append(resultado)

    iguais = sum(1 for x in itens if x["confere"] is True)
    diferentes = sum(1 for x in itens if x["confere"] is False)
    saida = caminho.with_name(f"{caminho.stem}-conferencia.json")
    saida.write_text(json.dumps({
        "retrato": caminho.name,
        "data": dt.datetime.now().isoformat(timespec="seconds"),
        "log": retrato.get("log"),
        "iguais": iguais,
        "diferentes": diferentes,
        "sem_resposta": len(itens) - iguais - diferentes,
        "itens": itens,
    }, ensure_ascii=False, indent=2), encoding="utf-8")
    print(f"\nPronto: {iguais} iguais, {diferentes} diferentes.")
    if diferentes:
        print("Cada diferença vira correção no parser ou no MAPA.md, citando o quadro do log.")
    print(f"Resultado: {_relativo(saida)}")
    return SAIDA_OK


# --------------------------------------------------------------------------- CLI


def main(argv: list[str] | None = None, *, transporte=None, estado: Path | None = None,
         tempos: dict | None = None, entrada=input) -> int:
    for fluxo in (sys.stdout, sys.stderr):
        try:
            fluxo.reconfigure(encoding="utf-8", errors="replace")
        except (AttributeError, ValueError):
            pass
    ap = argparse.ArgumentParser(description="Lê o PRO 2.4BT + AUDIO pelo BLE (só leitura).")
    grupo = ap.add_mutually_exclusive_group()
    grupo.add_argument("--bruto", metavar="CONSULTA", help='uma única consulta, ex.: "#081#0#"')
    grupo.add_argument("--conferir", metavar="ARQUIVO", type=Path,
                       help="confere um retrato com o app, um valor por vez")
    args = ap.parse_args(argv)

    if args.conferir:
        return _conferir(args.conferir, entrada)

    if args.bruto is not None:
        if pt.eh_autenticacao(args.bruto):
            print("A senha é enviada sozinha. Use --bruto só com consultas.")
            return SAIDA_FALHAS
        try:
            pt.verificar_envio(args.bruto)
        except pt.EnvioRecusado as erro:
            print(f"{erro}.")
            return SAIDA_FALHAS

    senha = _obter_senha()
    if senha is None:
        print("A senha tem 4 letras ou números (TARAMPS_SENHA ou digitada).")
        return SAIDA_FALHAS

    return asyncio.run(_sessao(
        args, senha, transporte or cb.TransporteBleak(), estado or RAIZ / "estado", tempos or {}
    ))


if __name__ == "__main__":
    sys.exit(main())
