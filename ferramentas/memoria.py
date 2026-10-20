#!/usr/bin/env python3
"""Salva o estado atual numa memória do DSP do PRO 2.4BT + AUDIO pela IA, atrás do guardião
(spec-gerador-memoria.md).

Uso (na pasta do projeto, com o processador ligado e o celular desconectado dele):

    uv run ferramentas/memoria.py salvar 2 "MUSICA ORIG" [--sobrescrever]
        Salva o estado atual na memória 2 (`*240*1*`) e dá o nome (`*232*MUSICA ORIG*`).

Travas:
- O nome tem de 1 a 12 caracteres: A–Z (sem acento), 0–9, `_` e espaço (MAPA §5). Minúsculas
  viram maiúsculas, como no app. Fora disso, recusa sem conectar.
- Sobrescrever uma memória que estado/memorias.json não marca como vazia ou "por: ia" (salva
  pelo Denis) exige --sobrescrever. Sem ele, recusa sem conectar.
- Antes de salvar: leitura nova completa e o guardião sem nenhuma violação (não se salva estado
  perigoso; gd.avaliar_salvar_memoria). Com violação, recusa e diz qual.
- Depois de nomear, relê o nome (`23N#`). Se divergir, avisa e segue: o salvar em si vale.
- Registra em estado/memorias.json pelo `sessao.py memoria N "NOME" --por ia --retrato SHA`,
  com o SHA-256 do retrato salvo (o backup em estado/backups/).

Carregar memória (241), sweep, reset, senha e remote out continuam proibidos.
Log bruto (senha mascarada) em estado/log-ble/ e registro (tipo "memoria") em estado/ajustes/.

Códigos de saída: 0 salvo; 1 erro de uso ou guardiao.json/memorias.json ilegível; 2 senha
recusada; 3 processador não encontrado ou sem conexão; 4 leitura incompleta (nada salvo);
5 o processador não respondeu à senha; 6 recusado (nada enviado); 7 interrompido depois do
salvar (a memória pode ter sido salva: confira no app).
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
import sessao  # noqa: E402
import tom as tm  # noqa: E402

RAIZ = lp.RAIZ

(SAIDA_OK, SAIDA_USO, SAIDA_SENHA, SAIDA_SEM_APARELHO, SAIDA_LEITURA, SAIDA_SEM_RESPOSTA,
 SAIDA_RECUSADO, SAIDA_INTERROMPIDO) = range(8)


def _sobrescrita(memorias: dict, slot: int) -> dict | None:
    """O registro que só sai com --sobrescrever: memória não vazia e não salva pela IA."""
    anterior = memorias["dsp"].get(str(slot))
    if anterior is None or anterior.get("por") == "ia":
        return None
    return anterior


async def _salvar(cliente, transporte, contexto, slot: int, nome: str, cfg, estado: Path,
                  carimbo: str, reg: tm.RegistroEspecial, senha: str,
                  caminho_log: Path) -> tuple[int, str]:
    print("Senha aceita. Lendo o estado atual…")
    retrato = await tm.ler_completo(cliente, transporte, contexto, senha, caminho_log)
    backup = tm.gravar_backup(estado, carimbo, retrato, reg)
    print(f"Backup: {lp.relativo(backup)}")
    decisao = gd.avaliar_salvar_memoria(retrato, cfg, slot, nome)
    reg.dados["decisao"] = decisao.como_dict()
    if not decisao.aprovado:
        tm.mostrar_recusas(decisao.recusas, "Nada foi salvo.")
        return SAIDA_RECUSADO, "recusado"
    salvar, nomear = decisao.aprovacao.comandos
    print(f"Guardião: APROVADO. Salvando na memória {slot}: {salvar} e {nomear}")

    reg.dados["em_andamento"] = salvar
    reg.gravar("em andamento", fim=False)
    await cliente.enviar_especial(decisao.aprovacao, salvar)
    reg.dados["comandos"].append({"texto": salvar, "status": "enviado"})
    reg.dados["em_andamento"] = nomear
    await cliente.enviar_especial(decisao.aprovacao, nomear)
    reg.dados["comandos"].append({"texto": nomear, "status": "enviado"})
    reg.dados["em_andamento"] = None

    # O formato real do nome não está provado: relê e, se divergir, só avisa.
    item = await cliente.consultar_especial(pt.NOMES_MEMORIA[slot])
    relido = (item.get("valor") or "").strip() if item.get("cru") is not None else None
    reg.dados["nome_relido"] = relido
    if relido != nome:
        aviso = (f"o nome relido ({'sem resposta' if relido is None else repr(relido)}) difere "
                 f"de {nome!r}. O salvar vale; confira o nome no app")
        reg.dados["aviso"] = aviso
        print(f"Aviso: {aviso}.")
    else:
        print(f"Nome relido: {relido}.")

    sha = ap._sha256(backup)
    reg.dados["retrato_sha256"] = sha
    reg.dados["aplicados"] = list(reg.dados["lote"])
    if sessao.main(["memoria", str(slot), nome, "--por", "ia", "--retrato", sha],
                   estado=estado) != sessao.SAIDA_OK:
        reg.dados["aviso_registro"] = "não registrado em estado/memorias.json"
        print("Aviso: a memória foi salva, mas não consegui registrar em estado/memorias.json. "
              f'Registre com: uv run ferramentas/sessao.py memoria {slot} "{nome}" --por ia '
              f"--retrato {sha}")
    return SAIDA_OK, "salvo"


async def _sessao(slot: int, nome: str, cfg, senha: str, transporte, estado: Path, tempos: dict,
                  carimbo: str, reg: tm.RegistroEspecial) -> int:
    pasta_log = estado / "log-ble"
    pasta_log.mkdir(parents=True, exist_ok=True)
    caminho_log = pasta_log / f"{carimbo}-memoria.log"
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
                codigo, status = await _salvar(cliente, transporte, contexto, slot, nome, cfg,
                                               estado, carimbo, reg, senha, caminho_log)
        except tm.Parada as parada:
            if parada.mensagem:
                print(parada.mensagem)
            codigo, status = parada.codigo, parada.status
        except Exception as erro:  # noqa: BLE001 - queda, timeout ou erro do BLE
            log.anotar(f"erro do BLE: {erro!r}")
            enviados = bool(reg.dados["comandos"]) or reg.dados["em_andamento"] is not None
            if isinstance(erro, cb.ConexaoCaiu):
                print("A conexão caiu.")
            else:
                print(f"Erro na conversa com o processador: {erro}")
            if enviados:
                print("A memória pode ter sido salva. Confira no app; o registro em "
                      "estado/memorias.json não foi feito.")
                codigo, status = SAIDA_INTERROMPIDO, "interrompido"
            else:
                print("Nada foi salvo.")
                codigo, status = SAIDA_LEITURA, "queda" if isinstance(erro, cb.ConexaoCaiu) else "erro"
        finally:
            reg.gravar(status)
            await cliente.encerrar()
    return codigo


async def _nomes(senha: str, transporte, estado: Path, tempos: dict, carimbo: str) -> int:
    """Só leitura: os nomes das memórias 1–3 (`231#`–`233#`), direto do processador."""
    pasta_log = estado / "log-ble"
    pasta_log.mkdir(parents=True, exist_ok=True)
    caminho_log = pasta_log / f"{carimbo}-nomes.log"
    with open(caminho_log, "w", encoding="utf-8") as arquivo_log:
        log = pt.LogBruto(arquivo_log, senha)
        cliente = cb.ClienteTaramps(transporte, log, **tempos)
        try:
            erro, _ = await lp.conectar_e_autenticar(cliente, transporte, log, senha)
            if erro is not None:
                return erro
            print("Senha aceita. Lendo os nomes…")
            for slot in pt.SLOTS_MEMORIA:
                item = await cliente.consultar_especial(pt.NOMES_MEMORIA[slot])
                if item.get("cru") is None:
                    print(f"Memória {slot}: sem resposta")
                else:
                    print(f"Memória {slot}: {(item.get('valor') or '').strip() or '(sem nome)'}")
            return SAIDA_OK
        except Exception as erro:  # noqa: BLE001 - queda, timeout ou erro do BLE
            log.anotar(f"erro do BLE: {erro!r}")
            print(f"Erro na conversa com o processador: {erro}")
            return SAIDA_LEITURA
        finally:
            await cliente.encerrar()
            print(f"Log: {lp.relativo(caminho_log)}")


def main(argv: list[str] | None = None, *, transporte=None, estado: Path | None = None,
         tempos: dict | None = None) -> int:
    for fluxo in (sys.stdout, sys.stderr):
        try:
            fluxo.reconfigure(encoding="utf-8", errors="replace")
        except (AttributeError, ValueError):
            pass
    parser = argparse.ArgumentParser(
        description="Salva o estado atual numa memória do DSP, atrás do guardião.")
    sub = parser.add_subparsers(dest="comando", required=True)
    s = sub.add_parser("salvar", help='salva o estado na memória N com o nome, ex.: salvar 2 "MUSICA ORIG"')
    s.add_argument("numero", type=int, choices=pt.SLOTS_MEMORIA, help="memória 1, 2 ou 3")
    s.add_argument("nome", help="até 12 caracteres: A–Z, 0–9, _ e espaço")
    s.add_argument("--sobrescrever", action="store_true",
                   help="sobrescreve uma memória registrada como salva pelo Denis")
    sub.add_parser("nomes", help="só lê os nomes das memórias 1, 2 e 3 no processador")
    args = parser.parse_args(argv)
    estado = estado or RAIZ / "estado"

    if args.comando == "nomes":
        try:
            senha = lp.obter_senha()
        except lp.SenhaInvalida as erro:
            print(erro)
            return SAIDA_USO
        if senha is None:
            print("A senha tem 4 letras ou números (TARAMPS_SENHA, ~/.taramps/senha ou digitada).")
            return SAIDA_USO
        return asyncio.run(_nomes(senha, transporte or cb.TransporteBleak(), estado, tempos or {},
                                  ap._carimbo(estado)))

    slot = args.numero

    try:
        cfg = gd.carregar_config(estado / "guardiao.json")
    except gd.ConfigInvalida as erro:
        print(f"guardiao.json inválido: {erro}. Nada foi feito.")
        return SAIDA_USO
    try:
        memorias = sessao.carregar_memorias(estado)
    except sessao.ProgressoIlegivel as erro:
        print(f"Não consegui ler as memórias ({erro}). Nada foi feito.")
        return SAIDA_USO

    carimbo = ap._carimbo(estado)
    recusas: list[gd.Recusa] = []
    try:
        nome = pt.normalizar_nome_memoria(args.nome)
    except pt.EscritaInvalida as erro:
        nome = args.nome
        recusas.append(gd.Recusa(5, str(erro), "nome da memória"))
    reg = tm.RegistroEspecial(estado, carimbo, "memoria", [f'salvar memória {slot} "{nome}"'], cfg)
    anterior = _sobrescrita(memorias, slot)
    if not recusas and anterior is not None and not args.sobrescrever:
        quem = "o Denis" if anterior.get("por") == "denis" else "alguém fora da IA"
        recusas.append(gd.Recusa(0, f"a memória {slot} está registrada como \"{anterior['nome']}\", "
                                    f"salva por {quem}", f"memória {slot}",
                                 f'memoria.py salvar {slot} "{nome}" --sobrescrever'))
    if recusas:
        reg.dados["decisao"] = gd.DecisaoEspecial(False, recusas=recusas).como_dict()
        tm.mostrar_recusas(recusas, "Nada foi enviado.")
        reg.gravar("recusado")
        print(f"Resultado: {lp.relativo(reg.caminho)}")
        return SAIDA_RECUSADO

    try:
        senha = lp.obter_senha()
    except lp.SenhaInvalida as erro:
        print(erro)
        return SAIDA_USO
    if senha is None:
        print("A senha tem 4 letras ou números (TARAMPS_SENHA, ~/.taramps/senha ou digitada).")
        return SAIDA_USO

    codigo = asyncio.run(_sessao(slot, nome, cfg, senha, transporte or cb.TransporteBleak(),
                                 estado, tempos or {}, carimbo, reg))
    if reg.caminho.exists():
        print(f"Resultado: {lp.relativo(reg.caminho)}")
    return codigo


if __name__ == "__main__":
    sys.exit(main())
