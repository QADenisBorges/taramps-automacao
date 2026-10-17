#!/usr/bin/env python3
"""Ajusta o PRO 2.4BT + AUDIO pelo BLE do notebook, sempre atrás do guardião mínimo.

Uso (na pasta do projeto, com o processador ligado e o celular desconectado dele):

    uv run ferramentas/ajustar_processador.py 081:0=-12.0 042:0=11
        Ajustes no formato CCC:c=v (com canal 0 a 3, ou banda 0 a 14 no 020) ou CCC=v
        (globais). Filtro aceita o índice ou o nome (042:0=BT24).

    uv run ferramentas/ajustar_processador.py --arquivo lote.json
        Lote em JSON: ["081:0=-12.0", {"codigo": "042", "canal": 0, "valor": 11}]
        ou {"ajustes": [...]}.

    uv run ferramentas/ajustar_processador.py --simular 081:0=-12.0
        Lê o aparelho e passa o lote pelo guardião, sem escrever nada.

Fluxo: leitura completa → backup (estado/backups/) → guardião → escrita na ordem aprovada,
com releitura de cada item → resumo. O resultado fica em estado/ajustes/ e o log bruto em
estado/log-ble/. Os limites vêm de estado/guardiao.json, que só o Denis edita.

Fail-safe: releitura diferente do pedido, queda ou timeout param o lote na hora. O resumo
diz o que foi aplicado, o que não foi e o que foi relido. Não salva nem carrega memória:
quem salva é o Denis, pelo app.

A senha BT vem da variável TARAMPS_SENHA, do arquivo %USERPROFILE%\\.taramps\\senha ou é
pedida sem eco (ver ler_processador.py). Ela não é gravada nem mostrada.

Códigos de saída: 0 aplicado (ou aprovado, com --simular); 1 erro de uso, lote ilegível
ou guardiao.json inválido; 2 senha recusada; 3 processador não encontrado ou sem conexão;
4 leitura incompleta ou interrompida (nada escrito); 5 o processador não respondeu à senha;
6 recusado pelo guardião (nada escrito); 7 escrita interrompida (releitura diferente,
queda ou timeout).
"""

from __future__ import annotations

import argparse
import asyncio
import datetime as dt
import hashlib
import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

import cliente_ble as cb  # noqa: E402
import guardiao as gd  # noqa: E402
import ler_processador as lp  # noqa: E402
import protocolo_taramps as pt  # noqa: E402

RAIZ = lp.RAIZ

(SAIDA_OK, SAIDA_USO, SAIDA_SENHA, SAIDA_SEM_APARELHO, SAIDA_LEITURA, SAIDA_SEM_RESPOSTA,
 SAIDA_RECUSADO, SAIDA_INTERROMPIDO) = range(8)


def _carimbo(estado: Path) -> str:
    base = dt.datetime.now().strftime("%Y-%m-%d_%H%M")
    carimbo, n = base, 2
    while any(p.exists() for p in (estado / "backups" / f"{carimbo}.json",
                                   estado / "ajustes" / f"{carimbo}.json",
                                   estado / "log-ble" / f"{carimbo}-ajuste.log")):
        carimbo, n = f"{base}-{n}", n + 1
    return carimbo


def _gravar_json(caminho: Path, dados: dict) -> None:
    caminho.parent.mkdir(parents=True, exist_ok=True)
    caminho.write_text(json.dumps(dados, ensure_ascii=False, indent=2), encoding="utf-8")


def _ler_pedidos(args) -> list[gd.Pedido]:
    pedidos = [gd.interpretar(texto) for texto in args.ajustes]
    if args.arquivo:
        try:
            dados = json.loads(Path(args.arquivo).read_text(encoding="utf-8"))
        except (OSError, ValueError) as erro:
            raise gd.PedidoInvalido(f"não consegui ler {args.arquivo}: {erro}") from None
        pedidos += gd.ler_lote(dados)
    return pedidos


def _mostrar_recusas(recusas: list[gd.Recusa]) -> None:
    print("Guardião: RECUSADO. Nada foi escrito.")
    for r in recusas:
        print(f"  - {r.texto()}")


def _mostrar_alertas(alertas: list[str]) -> None:
    if alertas:
        print("Alerta (já estava assim no aparelho):")
        for a in alertas:
            print(f"  - {a}")


class _Registro:
    """O resultado que vai para estado/ajustes/, montado ao longo da execução."""

    def __init__(self, estado: Path, carimbo: str, pedidos: list[gd.Pedido], simular: bool,
                 cfg: gd.Config | None):
        self.caminho = estado / "ajustes" / f"{carimbo}.json"
        self.dados: dict = {
            "formato": 1,
            "inicio": dt.datetime.now().isoformat(timespec="seconds"),
            "fim": None,
            "simular": simular,
            "lote": [p.original for p in pedidos],
            "guardiao": {"config": lp.relativo(Path(cfg.caminho)) if cfg else None,
                         "sha256": _sha256(Path(cfg.caminho)) if cfg else None,
                         "regra6": cfg.regra6 if cfg else None},
            "status": None,
            "backup": None,
            "log": None,
            "decisao": None,
            "escritas": [],
            "aplicados": [],
            "nao_aplicados": [],
            "em_andamento": None,  # escrita enviada sem resultado (ex.: Ctrl+C no meio)
            "lote_reverso": [],
            "estado_relido": [],
        }
        self.plano: tuple[gd.Escrita, ...] = ()  # escritas aprovadas, na ordem
        self.lido: dict = {}  # estado lido antes da primeira escrita
        self.tentados = 0  # escritas enviadas (aplicadas ou não)

    def completar(self) -> None:
        """O que não foi aplicado e o lote que desfaz o que foi tentado."""
        if not self.plano:
            return
        self.dados["nao_aplicados"] = [e.texto() for e in self.plano[len(self.dados["aplicados"]):]]
        self.dados["lote_reverso"] = _reverso(self.plano[:self.tentados], self.lido)

    def gravar(self, status: str) -> None:
        self.completar()
        self.dados["status"] = status
        self.dados["fim"] = dt.datetime.now().isoformat(timespec="seconds")
        _gravar_json(self.caminho, self.dados)


def _sha256(caminho: Path) -> str | None:
    try:
        return hashlib.sha256(caminho.read_bytes()).hexdigest()
    except OSError:
        return None


def _ponte(e: gd.Escrita) -> bool:
    return pt.POR_CODIGO[e.codigo].escopo == pt.ESCOPO_CANAL and e.canal in gd.PONTE


def _item(codigo: str, canal, valor) -> str:
    onde = f":{canal}" if canal is not None else ""
    return f"{codigo}{onde}={pt.valor_no_fio(codigo, valor)}"


def _reverso(tentados, lido: dict) -> list[str]:
    """`CCC:c=valor_antigo` de cada escrita tentada, da última para a primeira. Da ponte vai
    um item por parâmetro (o guardião espelha na outra saída), de preferência o da saída 3."""
    escolhidos: dict = {}
    for e in tentados:
        chave = (e.codigo, "ponte") if _ponte(e) else e.chave
        if chave not in escolhidos or e.canal == gd.PONTE[0]:
            escolhidos[chave] = e
    ordem = list(dict.fromkeys((e.codigo, "ponte") if _ponte(e) else e.chave for e in tentados))
    return [_item(escolhidos[k].codigo, escolhidos[k].canal, lido[escolhidos[k].chave])
            for k in reversed(ordem)]


COMANDO = "uv run ferramentas/ajustar_processador.py"


# --------------------------------------------------------------------------- sessão BLE


async def _sessao(pedidos: list[gd.Pedido], cfg: gd.Config, simular: bool, senha: str,
                  transporte, estado: Path, tempos: dict, carimbo: str, reg: _Registro) -> int:
    pasta_log = estado / "log-ble"
    pasta_log.mkdir(parents=True, exist_ok=True)
    caminho_log = pasta_log / f"{carimbo}-ajuste.log"
    reg.dados["log"] = lp.relativo(caminho_log)
    inicio = dt.datetime.now()

    with open(caminho_log, "w", encoding="utf-8") as arquivo_log:
        log = pt.LogBruto(arquivo_log, senha)
        cliente = cb.ClienteTaramps(transporte, log, **tempos)
        erro_conexao, nome, endereco = await lp.procurar_e_conectar(transporte, log)
        if erro_conexao is not None:
            reg.gravar("sem conexão")
            return erro_conexao
        status = "interrompido"
        try:
            print("Conectado. Enviando a senha…")
            auth = await cliente.autenticar(senha)
            if auth.motivo == "recusada":
                print("Senha recusada.")
                status = "senha recusada"
                return SAIDA_SENHA
            if not auth.ok:
                print("O processador não respondeu à senha.")
                status = "sem resposta à senha"
                return SAIDA_SEM_RESPOSTA
            lp.firmware_seguro(auth, senha)

            # 1. Leitura completa, sempre nova (o app pode ter mexido no aparelho).
            print("Senha aceita. Lendo o estado atual…")

            def progresso(feitos: int, total: int) -> None:
                print(f"\r  {feitos} de {total}", end="" if feitos < total else "\n", flush=True)

            leituras, completo = await cliente.ler_tudo(pt.consultas_marco1(), progresso)
            retrato = lp.montar_retrato(
                leituras=leituras, completo=completo, inicio=inicio, nome=nome,
                endereco=endereco, transporte=transporte, auth=auth, senha=senha,
                orfaos=list(cliente.orfaos), caminho_log=caminho_log)

            # 2. Backup antes de qualquer escrita.
            caminho_backup = estado / "backups" / f"{carimbo}.json"
            _gravar_json(caminho_backup, retrato)
            reg.dados["backup"] = lp.relativo(caminho_backup)
            print(f"Backup: {lp.relativo(caminho_backup)}")
            if not completo or retrato["falhas"]:
                print(f"Leitura incompleta ({retrato['lidos']} de {retrato['total']}). "
                      "Nada foi escrito. Tente de novo.")
                status = "leitura incompleta"
                return SAIDA_LEITURA

            # 3. Guardião.
            decisao = gd.avaliar_lote(retrato, pedidos, cfg)
            reg.dados["decisao"] = decisao.como_dict()
            _mostrar_alertas(decisao.alertas)
            if not decisao.aprovado:
                _mostrar_recusas(decisao.recusas)
                status = "recusado"
                return SAIDA_RECUSADO
            escritas = decisao.aprovacao.escritas
            if decisao.espelhados:
                print("Espelhado na ponte: " + ", ".join(decisao.espelhados))
            if decisao.sem_mudanca:
                print("Já estava assim: " + ", ".join(decisao.sem_mudanca))
            if not escritas:
                print("Guardião: aprovado. Nada a mudar.")
                status = "sem mudança"
                return SAIDA_OK
            print("Guardião: APROVADO. Ordem: " + ", ".join(decisao.ordem))
            if simular:
                print("Simulação: nada foi escrito.")
                status = "simulado"
                return SAIDA_OK

            # 4. Escrita na ordem aprovada, com releitura de cada item.
            reg.plano, reg.lido = escritas, gd.estado_do_retrato(retrato)
            codigo = await _escrever(cliente, decisao.aprovacao, reg)
            status = "aplicado" if codigo == SAIDA_OK else "interrompido"
            return codigo
        except cb.ConexaoCaiu:
            print("A conexão caiu. Nada foi escrito." if not reg.dados["escritas"] else
                  "A conexão caiu.")
            status = "queda" if not reg.dados["escritas"] else "interrompido"
            return SAIDA_LEITURA if not reg.dados["escritas"] else SAIDA_INTERROMPIDO
        except Exception as erro:  # noqa: BLE001 - falha do BLE fora da escrita
            log.anotar(f"erro do BLE: {erro!r}")
            print(f"Erro na conversa com o processador: {erro}")
            status = "erro" if not reg.dados["escritas"] else "interrompido"
            return SAIDA_LEITURA if not reg.dados["escritas"] else SAIDA_INTERROMPIDO
        finally:
            reg.gravar(status)  # antes de desconectar: o registro sai mesmo com Ctrl+C
            await cliente.encerrar()


async def _escrever(cliente: cb.ClienteTaramps, aprovacao: gd.Aprovacao, reg: _Registro) -> int:
    escritas = aprovacao.escritas
    total = len(escritas)
    falha: str | None = None
    for i, e in enumerate(escritas):
        print(f"  {i + 1}/{total} {e.texto()} … ", end="", flush=True)
        reg.tentados = i + 1
        reg.dados["em_andamento"] = e.texto()  # fica no registro se Ctrl+C cortar aqui
        try:
            resultado = await cliente.escrever(aprovacao, i)
        except cb.ConexaoCaiu:
            resultado, falha = {"escrito": e.texto(), "status": "queda"}, "a conexão caiu"
        except TimeoutError as erro:
            resultado, falha = {"escrito": e.texto(), "status": "timeout"}, str(erro)
        except Exception as erro:  # noqa: BLE001 - falha do BLE nesta escrita
            cliente.log.anotar(f"erro na escrita {e.texto()}: {erro!r}")
            resultado, falha = {"escrito": e.texto(), "status": "erro"}, f"erro do BLE: {erro}"
        reg.dados["em_andamento"] = None
        reg.dados["escritas"].append(resultado)
        if resultado["status"] == "igual":
            print("igual")
            reg.dados["aplicados"].append(e.texto())
            continue
        if falha is None:
            relido = resultado.get("relido")
            falha = ("sem resposta na releitura" if resultado["status"] == "sem resposta"
                     else f"relido {relido}, pedido {pt.valor_no_fio(e.codigo, e.valor)}")
        print(resultado["status"].upper())
        reg.completar()
        print(f"PAREI: {falha}.")
        print("Aplicados: " + (", ".join(reg.dados["aplicados"]) or "nenhum"))
        print("Não aplicados (ou sem confirmação): " + ", ".join(reg.dados["nao_aplicados"]))
        if _ponte(e):
            antigo = _item(e.codigo, gd.PONTE[0], reg.lido[(e.codigo, gd.PONTE[0])])
            print(f"ATENÇÃO: a ponte pode ter ficado desigual (saídas 3 e 4 em "
                  f"{pt.POR_CODIGO[e.codigo].rotulo}). Confira no app.")
            print(f"Para voltar a saída 3 ao valor antigo (espelha na 4): {COMANDO} {antigo}")
        await _reler(cliente, escritas, reg)
        print(f"Para desfazer o que foi tentado: {COMANDO} " + " ".join(reg.dados["lote_reverso"]))
        print(f"Backup completo: {reg.dados['backup']}")
        return SAIDA_INTERROMPIDO
    reg.completar()
    print(f"Pronto: {total} aplicado(s), cada um relido igual.")
    print(f"Para desfazer: {COMANDO} " + " ".join(reg.dados["lote_reverso"]))
    reg.dados["estado_relido"] = [
        {"codigo": r["codigo"], "canal": r["canal"], "valor": r["relido"], "cru": r["cru"]}
        for r in reg.dados["escritas"]]
    return SAIDA_OK


async def _reler(cliente: cb.ClienteTaramps, escritas, reg: _Registro) -> None:
    """Depois de uma parada: relê os parâmetros do lote, se a conexão ainda existe."""
    if cliente.caiu:
        print("Sem conexão para reler. Confira no app.")
        return
    print("Estado relido:")
    vistos = set()
    for e in escritas:
        if e.chave in vistos:
            continue
        vistos.add(e.chave)
        consulta = pt.Consulta(pt.POR_CODIGO[e.codigo], e.canal)
        try:
            item = await cliente.consultar(consulta)
        except Exception as erro:  # noqa: BLE001 - registra e segue
            item = pt.leitura(consulta, None, f"erro: {erro}")
        reg.dados["estado_relido"].append(
            {"codigo": e.codigo, "canal": e.canal, "valor": item.get("valor"),
             "cru": item.get("cru"), "motivo": item.get("motivo")})
        print(f"  - {consulta.descricao()}: {pt.formatar_valor(item)}")
        if cliente.caiu:
            print("A conexão caiu durante a releitura. Confira no app.")
            return


# --------------------------------------------------------------------------- CLI


def main(argv: list[str] | None = None, *, transporte=None, estado: Path | None = None,
         tempos: dict | None = None) -> int:
    for fluxo in (sys.stdout, sys.stderr):
        try:
            fluxo.reconfigure(encoding="utf-8", errors="replace")
        except (AttributeError, ValueError):
            pass
    ap = argparse.ArgumentParser(
        description="Ajusta o PRO 2.4BT + AUDIO pelo BLE, atrás do guardião mínimo.")
    ap.add_argument("ajustes", nargs="*", metavar="CCC:c=v",
                    help="ajuste, ex.: 081:0=-12.0 (canal) ou 080=-3.0 (global)")
    ap.add_argument("--arquivo", metavar="LOTE.json", help="lote em JSON")
    ap.add_argument("--simular", action="store_true",
                    help="lê o aparelho e passa o lote pelo guardião, sem escrever")
    args = ap.parse_args(argv)
    estado = estado or RAIZ / "estado"

    try:
        pedidos = _ler_pedidos(args)
    except gd.PedidoInvalido as erro:
        print(f"Lote ilegível: {erro}.")
        return SAIDA_USO
    if not pedidos:
        print("Nenhum ajuste. Exemplo: 081:0=-12.0")
        return SAIDA_USO
    try:
        cfg = gd.carregar_config(estado / "guardiao.json")
    except gd.ConfigInvalida as erro:
        print(f"guardiao.json inválido: {erro}. Nada foi feito.")
        return SAIDA_USO

    carimbo = _carimbo(estado)
    reg = _Registro(estado, carimbo, pedidos, args.simular, cfg)

    # Regra 5 e conflitos: recusa antes de conectar (nada vai ao BLE).
    recusas = gd.pre_checar(pedidos)
    if recusas:
        reg.dados["decisao"] = gd.Decisao(False, recusas=recusas).como_dict()
        _mostrar_recusas(recusas)
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

    codigo = asyncio.run(_sessao(pedidos, cfg, args.simular, senha,
                                 transporte or cb.TransporteBleak(), estado, tempos or {},
                                 carimbo, reg))
    if reg.caminho.exists():
        print(f"Resultado: {lp.relativo(reg.caminho)}")
    return codigo


if __name__ == "__main__":
    sys.exit(main())
