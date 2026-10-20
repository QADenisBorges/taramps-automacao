#!/usr/bin/env python3
"""Tom fixo do gerador do PRO 2.4BT + AUDIO pela IA, atrás do guardião (spec-gerador-memoria.md).

Uso (na pasta do projeto, com o processador ligado e o celular desconectado dele):

    uv run ferramentas/tom.py 400 [--db -20.0] [--s 20]
        Toca um tom fixo de 400 Hz (10 Hz a 20 kHz), no ganho do gerador em dB (−60 a 0) e
        pela duração em segundos (padrão 20, máximo 30). Ctrl+C desliga antes.

    uv run ferramentas/tom.py --caminho [--hz 400] [--db -10.0] [--s 20]
        Teste de caminho, com o módulo desligado do processador (RCA fora). Em 4 telas, o
        Denis mede no RCA de saída do processador: o tom ligado; com a saída 1 mutada; com o
        passa-alta da saída 1 em 5 kHz; com o limiter da saída 1 em −30 dB. Cada mudança só
        protege (mute, corte mais alto, limiter mais baixo) e é desfeita logo depois da medida;
        no fim, o estado é relido inteiro e comparado com o original. O resultado fica em
        estado/auditorias/AAAA-MM-DD_caminho-gerador.json.

Travas:
- O tom só toca com `"gerador": {"liberado": true, "caminho_provado": "<arquivo>"}` em
  estado/guardiao.json (só o Denis edita) e com esse arquivo aprovado e restaurado. Sem isso,
  recusa antes de conectar. O teste de caminho é o único modo que roda sem a liberação.
- Antes de cada tom: leitura nova completa e o guardião sem violação nas travas 1–4 e na
  regra 7 (gd.avaliar_tom). Com violação, recusa e diz qual.
- O desligamento (`*202*0*`) sai sempre: no fim, no Ctrl+C, em exceção e na saída do processo
  (try/finally + atexit), com releitura confirmando 202 = 0. Se a conexão cair com o tom
  ligado, reconecta e tenta desligar até 3 vezes; se não conseguir, imprime em destaque
  "DESLIGUE O MÓDULO AGORA" e sai com o código 8.

Log bruto (senha mascarada) em estado/log-ble/, retrato em estado/backups/ e registro (tipo
"tom") em estado/ajustes/, como nos ajustes.

Códigos de saída: 0 tocado (ou teste de caminho concluído); 1 erro de uso ou guardiao.json
inválido; 2 senha recusada; 3 processador não encontrado ou sem conexão; 4 leitura incompleta
ou queda antes do tom; 5 o processador não respondeu à senha; 6 recusado (nada tocado);
7 interrompido (gerador desligado e relido); 8 o gerador pode ter ficado ligado.
"""

from __future__ import annotations

import argparse
import asyncio
import atexit
import datetime as dt
import json
import math
import os
import sys
from dataclasses import dataclass, field
from pathlib import Path
from typing import Callable

sys.path.insert(0, str(Path(__file__).resolve().parent))

import ajustar_processador as ap  # noqa: E402
import cliente_ble as cb  # noqa: E402
import guardiao as gd  # noqa: E402
import ler_processador as lp  # noqa: E402
import protocolo_taramps as pt  # noqa: E402

RAIZ = lp.RAIZ

(SAIDA_OK, SAIDA_USO, SAIDA_SENHA, SAIDA_SEM_APARELHO, SAIDA_LEITURA, SAIDA_SEM_RESPOSTA,
 SAIDA_RECUSADO, SAIDA_INTERROMPIDO, SAIDA_GERADOR_LIGADO) = range(9)

TOM_PADRAO_S = 20.0
TOM_PADRAO_DB = -20.0
CAMINHO_HZ = 400
CAMINHO_DB = -10.0
TENTATIVAS_DESLIGAR = 3

AVISO_DESLIGUE = "DESLIGUE O MÓDULO AGORA: o gerador pode ter ficado ligado"
BANNER = "\n" + "!" * 64 + f"\n!!! {AVISO_DESLIGUE}\n" + "!" * 64 + "\n"

_STATUS_CONEXAO = {SAIDA_SENHA: "senha recusada", SAIDA_SEM_APARELHO: "sem conexão",
                   SAIDA_SEM_RESPOSTA: "sem resposta à senha"}


# --------------------------------------------------------------------------- registro


class RegistroEspecial:
    """O resultado que vai para estado/ajustes/ (tipo "tom" ou "memoria"), no formato que o
    painel já lê (lote, status, decisao, aplicados…), mais os comandos especiais enviados."""

    def __init__(self, estado: Path, carimbo: str, tipo: str, lote: list[str],
                 cfg: gd.Config | None):
        self.caminho = estado / "ajustes" / f"{carimbo}.json"
        self.dados: dict = {
            "formato": 1,
            "tipo": tipo,
            "inicio": dt.datetime.now().isoformat(timespec="seconds"),
            "fim": None,
            "simular": False,
            "lote": lote,
            "guardiao": {"config": lp.relativo(Path(cfg.caminho)) if cfg else None,
                         "sha256": ap._sha256(Path(cfg.caminho)) if cfg else None},
            "status": None,
            "backup": None,
            "log": None,
            "decisao": None,
            "comandos": [],  # {texto, status, relido?} de cada comando especial, na ordem
            "escritas": [],  # ajustes do catálogo (só no teste de caminho)
            "aplicados": [],
            "nao_aplicados": [],
            "em_andamento": None,
        }

    def gravar(self, status: str, fim: bool = True) -> None:
        self.dados["status"] = status
        self.dados["fim"] = dt.datetime.now().isoformat(timespec="seconds") if fim else None
        ap._gravar_json(self.caminho, self.dados)


def gravar_backup(estado: Path, carimbo: str, retrato: dict, reg: RegistroEspecial,
                  sufixo: str = "") -> Path:
    caminho = estado / "backups" / f"{carimbo}{sufixo}.json"
    ap._gravar_json(caminho, retrato)
    if reg.dados["backup"] is None:
        reg.dados["backup"] = lp.relativo(caminho)
    return caminho


def mostrar_recusas(recusas: list[gd.Recusa], nada: str) -> None:
    print(f"Guardião: RECUSADO. {nada}")
    for r in recusas:
        print(f"  - {r.texto()}")


# --------------------------------------------------------------------------- caminho provado


def motivo_sem_caminho(cfg: gd.Config, estado: Path) -> str | None:
    """None se o gerador está liberado com o caminho provado; senão o motivo da recusa."""
    g = cfg.gerador
    if not g.liberado:
        return ("o gerador não está liberado (estado/guardiao.json: gerador.liberado = false). "
                "Primeiro o teste de caminho: uv run ferramentas/tom.py --caminho")
    if not g.caminho_provado:
        return "falta gerador.caminho_provado no guardiao.json (o arquivo do teste de caminho)"
    caminho = Path(g.caminho_provado)
    if not caminho.is_absolute():
        caminho = estado.parent / caminho
    try:
        dados = json.loads(caminho.read_text(encoding="utf-8"))
    except OSError:
        return f"o arquivo do teste de caminho não existe: {g.caminho_provado}"
    except ValueError:
        return f"o arquivo do teste de caminho está ilegível: {g.caminho_provado}"
    if not isinstance(dados, dict) or dados.get("tipo") != "caminho-gerador" \
            or dados.get("aprovado") is not True or dados.get("restaurado") is not True:
        return f"o teste de caminho em {g.caminho_provado} não está aprovado e restaurado"
    return None


# --------------------------------------------------------------------------- desligamento


@dataclass
class _Controle:
    """O que o desligamento garantido (finally e atexit) precisa."""

    senha: str
    tempos: dict
    fabrica: Callable[[], object]  # transporte novo, para a emergência na saída do processo
    aprovacao_desligar: gd.AprovacaoEspecial = field(default_factory=gd.aprovar_desligar_gerador)
    cliente: cb.ClienteTaramps | None = None
    caminho_log: Path | None = None
    tratado: bool = False  # o finally já tentou desligar (com ou sem sucesso)


_CONTROLE: _Controle | None = None
_ATEXIT_REGISTRADO = False


async def _desligar(cliente: cb.ClienteTaramps, controle: _Controle,
                    reg: RegistroEspecial) -> bool:
    """`*202*0*` com releitura, reconectando se preciso, até 3 vezes. Falhou: o aviso."""
    print("Desligando o gerador…")
    ok = await cliente.desligar_gerador_garantido(controle.aprovacao_desligar, controle.senha,
                                                  TENTATIVAS_DESLIGAR)
    reg.dados["gerador_desligado"] = ok
    reg.dados["comandos"].append({"texto": gd.DESLIGAR_GERADOR,
                                  "status": "relido 0" if ok else "não confirmado"})
    if ok:
        print("Gerador desligado (relido 202 = 0).")
    else:
        print(BANNER, flush=True)
    return ok


def _ao_sair(sair: Callable[[int], None] = os._exit) -> None:
    """atexit: se o processo sai com o gerador talvez ligado e o finally não rodou, tenta
    desligar com uma conexão nova; se não der, o aviso em destaque e código 8."""
    c = _CONTROLE
    if c is None or c.tratado or c.cliente is None or not c.cliente.gerador_talvez_ligado:
        return
    c.tratado = True
    try:
        ok = asyncio.run(_desligar_de_emergencia(c))
    except BaseException:  # noqa: BLE001 - na saída do processo, qualquer falha vira o aviso
        ok = False
    if ok:
        print("Gerador desligado na saída do processo (relido 202 = 0).", flush=True)
        return
    print(BANNER, flush=True)
    sair(SAIDA_GERADOR_LIGADO)


async def _desligar_de_emergencia(c: _Controle) -> bool:
    destino = open(c.caminho_log, "a", encoding="utf-8") if c.caminho_log else open(os.devnull, "w")
    with destino as arquivo:
        log = pt.LogBruto(arquivo, c.senha)
        log.anotar("saída do processo com o gerador talvez ligado: desligar")
        cliente = cb.ClienteTaramps(c.fabrica(), log, **c.tempos)
        cliente.caiu = True  # conexão nova: reconecta antes de desligar
        cliente.gerador_talvez_ligado = True
        try:
            return await cliente.desligar_gerador_garantido(c.aprovacao_desligar, c.senha,
                                                            TENTATIVAS_DESLIGAR)
        finally:
            await cliente.encerrar()


# --------------------------------------------------------------------------- tom


def _mesmo(parametro: pt.Parametro, pedido, relido) -> bool:
    if relido is None or isinstance(relido, bool):
        return False
    try:
        if parametro.casas == 1:
            return round(float(pedido), 1) == round(float(relido), 1)
        return float(pedido) == float(relido)
    except (TypeError, ValueError):
        return False


async def tocar_tom(cliente: cb.ClienteTaramps, aprovacao: gd.AprovacaoEspecial, hz: int,
                    db: float, segundos: float, dormir, controle: _Controle,
                    reg: RegistroEspecial) -> bool:
    """Ganho e frequência (relidos), liga, espera e desliga com releitura. O desligamento sai
    em qualquer saída daqui (try/finally). True só com tudo confirmado e o 202 relido 0."""
    c200, c201, ligar, _ = aprovacao.comandos
    for texto, parametro, pedido in ((c200, pt.GERADOR_GANHO, db), (c201, pt.GERADOR_FREQ, hz)):
        await cliente.enviar_especial(aprovacao, texto)
        item = await cliente.consultar_especial(parametro)
        if item.get("cru") is None:
            status = "sem resposta"
        else:
            status = "igual" if _mesmo(parametro, pedido, item.get("valor")) else "divergente"
        reg.dados["comandos"].append({"texto": texto, "status": status, "relido": item.get("valor")})
        if status != "igual":
            print(f"PAREI: {parametro.rotulo} {status} na releitura (relido {item.get('valor')}). "
                  "O tom não foi ligado.")
            return False
    desligado = False
    try:
        reg.dados["em_andamento"] = ligar
        reg.gravar("em andamento", fim=False)
        print(f"Tom ligado: {hz} Hz, {db:.1f} dB, por {segundos:g} s. Ctrl+C desliga antes.",
              flush=True)
        await cliente.enviar_especial(aprovacao, ligar)
        reg.dados["comandos"].append({"texto": ligar, "status": "enviado"})
        await dormir(segundos)
    finally:
        reg.dados["em_andamento"] = None
        desligado = await _desligar(cliente, controle, reg)
    return desligado


async def ler_completo(cliente, transporte, contexto, senha, caminho_log) -> dict:
    print("Lendo o estado completo…")
    retrato = await lp.leitura_completa(cliente, transporte, contexto, senha, caminho_log)
    if not lp.retrato_completo(retrato):
        raise Parada(SAIDA_LEITURA, "leitura incompleta",
                      f"Leitura incompleta ({retrato['lidos']} de {retrato['total']}). "
                      "Nenhum tom foi tocado depois dela.")
    return retrato


class Parada(Exception):
    def __init__(self, codigo: int, status: str, mensagem: str | None = None):
        super().__init__(mensagem or status)
        self.codigo, self.status, self.mensagem = codigo, status, mensagem


async def _gerador_antes(cliente: cb.ClienteTaramps, reg: RegistroEspecial) -> None:
    """Lê o gerador antes de tudo. Já ligado: para aqui (o finally desliga)."""
    itens = await cliente.ler_gerador()
    reg.dados["gerador_antes"] = {codigo: item.get("valor") for codigo, item in itens.items()}
    if cliente.gerador_talvez_ligado:
        raise Parada(SAIDA_INTERROMPIDO, "gerador já ligado",
                      "O gerador já estava ligado. Vou desligar e não toco nada; rode de novo.")


async def _tom(cliente, transporte, contexto, args, cfg, estado, carimbo, reg, dormir, controle,
               caminho_log) -> tuple[int, str]:
    retrato = await ler_completo(cliente, transporte, contexto, controle.senha, caminho_log)
    gravar_backup(estado, carimbo, retrato, reg)
    await _gerador_antes(cliente, reg)
    decisao = gd.avaliar_tom(retrato, cfg, args.hz, args.db, args.s)
    reg.dados["decisao"] = decisao.como_dict()
    ap._mostrar_alertas(decisao.alertas)
    if not decisao.aprovado:
        mostrar_recusas(decisao.recusas, "Nenhum tom foi tocado.")
        return SAIDA_RECUSADO, "recusado"
    print("Guardião: APROVADO.")
    ok = await tocar_tom(cliente, decisao.aprovacao, args.hz, args.db, args.s, dormir, controle, reg)
    if not ok:
        return SAIDA_INTERROMPIDO, "interrompido"
    reg.dados["aplicados"] = list(reg.dados["lote"])
    return SAIDA_OK, "tocado"


# --------------------------------------------------------------------------- teste de caminho

# (chave, título da tela, ajuste que só protege, máximo da medida / tom ligado)
PASSOS_CAMINHO = (
    ("ligado", "tom ligado", None, None),
    ("mute", "saída 1 mutada (deve cair a ~0)", "100:0=1", 0.05),
    ("passa_alta_5k", "passa-alta da saída 1 em 5 kHz (deve cair muito)", "040:0=5000", 0.25),
    ("limiter_30", "limiter da saída 1 em −30 dB (deve cair muito)", "071:0=-30.0", 0.5),
)


def _pedir_medida(entrada) -> float | None:
    """A leitura do multímetro em volts AC (aceita vírgula e "mV"). None = cancelou."""
    while True:
        try:
            r = entrada("Quanto o multímetro mostrou, em V AC? (ex.: 0,18; x cancela) ")
        except (EOFError, KeyboardInterrupt):
            return None
        r = (r or "").strip().lower().replace(" ", "")
        if r in ("x", "cancelar"):
            return None
        fator = 1.0
        if r.endswith("mv"):
            fator, r = 0.001, r[:-2]
        elif r.endswith("v"):
            r = r[:-1]
        try:
            valor = float(r.replace(",", "."))
        except ValueError:
            valor = math.nan
        if math.isfinite(valor) and valor >= 0:
            return round(valor * fator, 6)
        print("Só o número, ex.: 0,18 (ou 180mV).")


def _desfazer(ajuste: str, lido: dict) -> str:
    pedido = gd.interpretar(ajuste)
    chave = (pedido.codigo, pedido.canal)
    return f"{pedido.codigo}:{pedido.canal}={pt.valor_no_fio(pedido.codigo, lido[chave])}"


async def _ajustar(cliente, retrato: dict, ajuste: str, cfg, reg) -> None:
    """Um ajuste do catálogo pelo caminho normal (guardião + escrita com releitura)."""
    decisao = gd.avaliar_lote(retrato, [gd.interpretar(ajuste)], cfg)
    if not decisao.aprovado:
        mostrar_recusas(decisao.recusas, "Nada foi escrito.")
        raise Parada(SAIDA_RECUSADO, "recusado")
    for i, e in enumerate(decisao.aprovacao.escritas):
        print(f"  {e.texto()} … ", end="", flush=True)
        r = await cliente.escrever(decisao.aprovacao, i)
        reg.dados["escritas"].append(r)
        print(r["status"])
        if r["status"] != "igual":
            raise Parada(SAIDA_INTERROMPIDO, "interrompido",
                          f"PAREI: {e.texto()} {r['status']} na releitura.")


def _caminho_auditoria(estado: Path) -> Path:
    base = f"{dt.date.today():%Y-%m-%d}_caminho-gerador"
    caminho, n = estado / "auditorias" / f"{base}.json", 2
    while caminho.exists():
        caminho, n = estado / "auditorias" / f"{base}-{n}.json", n + 1
    return caminho


def _avaliar_medidas(medidas: dict) -> dict:
    """Queda de cada passo em relação ao tom ligado e se ela passou no critério."""
    ligado = medidas.get("ligado")
    saida = {}
    for chave, _, _, maximo in PASSOS_CAMINHO[1:]:
        v = medidas.get(chave)
        if v is None or not ligado:
            saida[chave] = {"razao": None, "queda_db": None, "maximo": maximo, "ok": False}
            continue
        razao = v / ligado
        queda = None if razao == 0 else round(20 * math.log10(razao), 1)
        saida[chave] = {"razao": round(razao, 4), "queda_db": queda, "maximo": maximo,
                        "ok": razao <= maximo}
    return saida


async def _caminho(cliente, transporte, contexto, args, cfg, estado, carimbo, reg, dormir,
                   controle, caminho_log, entrada) -> tuple[int, str]:
    senha = controle.senha
    original = await ler_completo(cliente, transporte, contexto, senha, caminho_log)
    gravar_backup(estado, carimbo, original, reg)
    lido0 = gd.estado_do_retrato(original)
    if lido0[("100", 0)]:
        print("A saída 1 está mutada. Tire o mute pelo app e rode de novo. Nada foi tocado.")
        return SAIDA_RECUSADO, "recusado"
    await _gerador_antes(cliente, reg)

    resultado: dict = {
        "formato": 1, "tipo": "caminho-gerador",
        "inicio": dt.datetime.now().isoformat(timespec="seconds"), "fim": None,
        "tom": {"hz": args.hz, "db": args.db, "s": args.s},
        "onde": "RCA de saída 1 do processador, módulo desligado (RCA fora)",
        "backup": reg.dados["backup"], "log": lp.relativo(caminho_log),
        "medidas": {}, "quedas": {}, "completo": False, "restaurado": None, "diferencas": [],
        "aprovado": False,
    }
    atual, pendente = original, None
    codigo, status = SAIDA_OK, "caminho concluído"
    try:
        for i, (chave, titulo, ajuste, _) in enumerate(PASSOS_CAMINHO, 1):
            print(f"\nTela {i} de {len(PASSOS_CAMINHO)} · {titulo}")
            if ajuste:
                pendente = _desfazer(ajuste, lido0)
                await _ajustar(cliente, atual, ajuste, cfg, reg)
                atual = await ler_completo(cliente, transporte, contexto, senha, caminho_log)
            decisao = gd.avaliar_tom(atual, cfg, args.hz, args.db, args.s, teste_de_caminho=True)
            reg.dados["decisao"] = decisao.como_dict()
            if not decisao.aprovado:
                mostrar_recusas(decisao.recusas, "Nenhum tom foi tocado.")
                raise Parada(SAIDA_RECUSADO, "recusado")
            print("Ponha o multímetro (V AC) no RCA da saída 1 do processador. O tom toca e "
                  "desliga sozinho; leia o valor enquanto ele toca.")
            if not await tocar_tom(cliente, decisao.aprovacao, args.hz, args.db, args.s, dormir,
                                   controle, reg):
                raise Parada(SAIDA_INTERROMPIDO, "interrompido")
            valor = _pedir_medida(entrada)
            if valor is None:
                raise Parada(SAIDA_INTERROMPIDO, "cancelado", "Teste cancelado.")
            resultado["medidas"][chave] = valor
            if pendente:
                print("Desfazendo:")
                await _ajustar(cliente, atual, pendente, cfg, reg)
                pendente, atual = None, original  # relido igual: de volta ao original
        resultado["completo"] = True
    except Parada as parada:
        if parada.mensagem:
            print(parada.mensagem)
        codigo, status = parada.codigo, parada.status
    finally:
        if cliente.gerador_talvez_ligado:  # antes de qualquer outra coisa
            await _desligar(cliente, controle, reg)
        if not cliente.caiu and not cliente.gerador_talvez_ligado:
            resultado["restaurado"], resultado["diferencas"] = await _restaurar(
                cliente, transporte, contexto, cfg, reg, pendente, lido0, senha, caminho_log)
        else:
            print("Sem conexão para restaurar e reler. Confira no app: mute, passa-alta e "
                  f"limiter da saída 1. Backup: {reg.dados['backup']}")
        resultado["quedas"] = _avaliar_medidas(resultado["medidas"])
        resultado["aprovado"] = bool(
            resultado["completo"] and resultado["restaurado"] and resultado["medidas"].get("ligado")
            and all(q["ok"] for q in resultado["quedas"].values()))
        resultado["fim"] = dt.datetime.now().isoformat(timespec="seconds")
        arquivo = _caminho_auditoria(estado)
        ap._gravar_json(arquivo, resultado)
        reg.dados["auditoria"] = lp.relativo(arquivo)
        _resumo_caminho(resultado, arquivo)
    if not resultado["restaurado"] and codigo == SAIDA_OK:
        codigo, status = SAIDA_INTERROMPIDO, "não restaurado"
    return codigo, status


async def _restaurar(cliente, transporte, contexto, cfg, reg, pendente, lido0, senha,
                     caminho_log) -> tuple[bool, list[str]]:
    """Desfaz o ajuste pendente (com leitura nova) e compara a leitura final com o original."""
    try:
        if pendente:
            print(f"Restaurando {pendente}…")
            atual = await ler_completo(cliente, transporte, contexto, senha, caminho_log)
            await _ajustar(cliente, atual, pendente, cfg, reg)
        print("Conferindo o estado final contra o original…")
        final = await ler_completo(cliente, transporte, contexto, senha, caminho_log)
        lido = gd.estado_do_retrato(final)
    except Exception as erro:  # noqa: BLE001 - qualquer falha: não restaurado, com o comando
        motivo = getattr(erro, "mensagem", None) or str(erro)
        print(f"Não consegui restaurar e reler ({motivo}).")
        if pendente:
            print(f"Para restaurar: {ap.COMANDO} {pendente}")
        return False, []
    diferencas = [f"{codigo}{'' if canal is None else f':{canal}'}: {lido0[(codigo, canal)]} → "
                  f"{lido.get((codigo, canal))}"
                  for (codigo, canal) in lido0 if lido.get((codigo, canal)) != lido0[(codigo, canal)]]
    if diferencas:
        print("O estado final NÃO bate com o original:")
        for d in diferencas:
            print(f"  - {d}")
    else:
        print("Estado restaurado: as 84 leituras batem com o original.")
    return not diferencas, diferencas


def _resumo_caminho(resultado: dict, arquivo: Path) -> None:
    print("\nTeste de caminho:")
    ligado = resultado["medidas"].get("ligado")
    print(f"  - tom ligado: {ligado if ligado is not None else 'sem medida'} V")
    for chave, titulo, _, _ in PASSOS_CAMINHO[1:]:
        q = resultado["quedas"].get(chave, {})
        v = resultado["medidas"].get(chave)
        if v is None:
            print(f"  - {titulo}: sem medida")
            continue
        queda = "a zero" if q.get("queda_db") is None else f"{q['queda_db']:g} dB"
        print(f"  - {titulo}: {v} V ({queda}) {'ok' if q.get('ok') else 'NÃO caiu o bastante'}")
    print(f"Resultado: {lp.relativo(arquivo)}")
    if resultado["aprovado"]:
        print("Aprovado. Para liberar o tom, o Denis edita estado/guardiao.json: "
              f'"gerador": {{"liberado": true, "caminho_provado": "{lp.relativo(arquivo)}"}}.')
    else:
        print("Não aprovado: o tom continua bloqueado.")


# --------------------------------------------------------------------------- sessão BLE


async def _sessao(args, cfg, transporte, estado: Path, tempos: dict, carimbo: str,
                  reg: RegistroEspecial, entrada, dormir, controle: _Controle) -> int:
    pasta_log = estado / "log-ble"
    pasta_log.mkdir(parents=True, exist_ok=True)
    caminho_log = pasta_log / f"{carimbo}-tom.log"
    reg.dados["log"] = lp.relativo(caminho_log)
    controle.caminho_log = caminho_log
    codigo, status = SAIDA_INTERROMPIDO, "interrompido"
    with open(caminho_log, "w", encoding="utf-8") as arquivo_log:
        log = pt.LogBruto(arquivo_log, controle.senha)
        cliente = cb.ClienteTaramps(transporte, log, **tempos)
        controle.cliente = cliente
        reg.gravar("em andamento", fim=False)
        try:
            erro, contexto = await lp.conectar_e_autenticar(cliente, transporte, log, controle.senha)
            if erro is not None:
                codigo, status = erro, _STATUS_CONEXAO.get(erro, "sem conexão")
            elif args.caminho:
                codigo, status = await _caminho(cliente, transporte, contexto, args, cfg, estado,
                                                carimbo, reg, dormir, controle, caminho_log, entrada)
            else:
                codigo, status = await _tom(cliente, transporte, contexto, args, cfg, estado,
                                            carimbo, reg, dormir, controle, caminho_log)
        except Parada as parada:
            if parada.mensagem:
                print(parada.mensagem)
            codigo, status = parada.codigo, parada.status
        except cb.ConexaoCaiu:
            print("A conexão caiu.")
            codigo, status = SAIDA_INTERROMPIDO, "queda"
        except Exception as erro:  # noqa: BLE001 - falha do BLE
            log.anotar(f"erro do BLE: {erro!r}")
            print(f"Erro na conversa com o processador: {erro}")
            codigo, status = SAIDA_INTERROMPIDO, "erro"
        finally:
            if cliente.gerador_talvez_ligado and reg.dados.get("gerador_desligado") is not False:
                await _desligar(cliente, controle, reg)
            if cliente.gerador_talvez_ligado:
                codigo, status = SAIDA_GERADOR_LIGADO, "gerador talvez ligado"
            controle.tratado = True
            reg.gravar(status)
            await cliente.encerrar()
    return codigo


# --------------------------------------------------------------------------- CLI


def main(argv: list[str] | None = None, *, transporte=None, estado: Path | None = None,
         tempos: dict | None = None, entrada=input, dormir=None, fabrica_transporte=None) -> int:
    global _CONTROLE, _ATEXIT_REGISTRADO
    for fluxo in (sys.stdout, sys.stderr):
        try:
            fluxo.reconfigure(encoding="utf-8", errors="replace")
        except (AttributeError, ValueError):
            pass
    ap_ = argparse.ArgumentParser(
        description="Tom fixo do gerador do PRO 2.4BT + AUDIO, atrás do guardião.")
    ap_.add_argument("frequencia", nargs="?", type=float, metavar="HZ",
                     help="frequência do tom, de 10 a 20000 Hz")
    ap_.add_argument("--hz", type=float, help="frequência (alternativa ao HZ; no --caminho, "
                                              f"padrão {CAMINHO_HZ})")
    ap_.add_argument("--db", type=float, help=f"ganho do gerador, de −60 a 0 dB (padrão "
                                              f"{TOM_PADRAO_DB:g}; no --caminho, {CAMINHO_DB:g})")
    ap_.add_argument("--s", type=float, default=TOM_PADRAO_S,
                     help=f"duração em segundos (padrão {TOM_PADRAO_S:g}, máximo {gd.TOM_MAX_S:g})")
    ap_.add_argument("--caminho", action="store_true",
                     help="teste de caminho, com o módulo desligado do processador (RCA fora)")
    args = ap_.parse_args(argv)
    estado = estado or RAIZ / "estado"

    if args.frequencia is not None and args.hz is not None:
        print("Use HZ ou --hz, não os dois.")
        return SAIDA_USO
    hz = args.frequencia if args.frequencia is not None else args.hz
    if hz is None and not args.caminho:
        print("Falta a frequência. Exemplo: uv run ferramentas/tom.py 400 --db -20 --s 20")
        return SAIDA_USO
    args.hz = int(hz) if hz is not None and float(hz).is_integer() else hz
    if args.hz is None:
        args.hz = CAMINHO_HZ
    if args.db is None:
        args.db = CAMINHO_DB if args.caminho else TOM_PADRAO_DB

    try:
        cfg = gd.carregar_config(estado / "guardiao.json")
    except gd.ConfigInvalida as erro:
        print(f"guardiao.json inválido: {erro}. Nada foi feito.")
        return SAIDA_USO

    # Limites e liberação: recusa antes de conectar (nada vai ao BLE).
    _, recusas = gd.pre_checar_tom(cfg, args.hz, args.db, args.s, teste_de_caminho=args.caminho)
    if not recusas and not args.caminho:
        motivo = motivo_sem_caminho(cfg, estado)
        if motivo:
            recusas = [gd.Recusa(5, motivo, "tom")]
    lote = (["teste de caminho do gerador"] if args.caminho
            else [f"tom {args.hz} Hz, {args.db:.1f} dB, {args.s:g} s"])
    carimbo = ap._carimbo(estado)
    reg = RegistroEspecial(estado, carimbo, "tom", lote, cfg)
    if recusas:
        reg.dados["decisao"] = gd.DecisaoEspecial(False, recusas=recusas).como_dict()
        mostrar_recusas(recusas, "Nada foi enviado.")
        reg.gravar("recusado")
        print(f"Resultado: {lp.relativo(reg.caminho)}")
        return SAIDA_RECUSADO

    if args.caminho:
        print("Teste de caminho do gerador. O módulo precisa estar DESLIGADO do processador "
              "(RCA fora): a medida é no RCA de saída do processador.")
        try:
            resposta = entrada("O módulo está desligado do processador (RCA fora)? Digite sim: ")
        except (EOFError, KeyboardInterrupt):
            resposta = ""
        if (resposta or "").strip().lower() != "sim":
            print("Teste cancelado. Nada foi enviado.")
            return SAIDA_USO

    try:
        senha = lp.obter_senha()
    except lp.SenhaInvalida as erro:
        print(erro)
        return SAIDA_USO
    if senha is None:
        print("A senha tem 4 letras ou números (TARAMPS_SENHA, ~/.taramps/senha ou digitada).")
        return SAIDA_USO

    tempos = tempos or {}
    controle = _Controle(senha=senha, tempos=tempos,
                         fabrica=fabrica_transporte or cb.TransporteBleak)
    _CONTROLE = controle
    if not _ATEXIT_REGISTRADO:
        atexit.register(_ao_sair)
        _ATEXIT_REGISTRADO = True

    codigo = asyncio.run(_sessao(args, cfg, transporte or cb.TransporteBleak(), estado, tempos,
                                 carimbo, reg, entrada, dormir or asyncio.sleep, controle))
    if reg.caminho.exists():
        print(f"Resultado: {lp.relativo(reg.caminho)}")
    return codigo


if __name__ == "__main__":
    sys.exit(main())
