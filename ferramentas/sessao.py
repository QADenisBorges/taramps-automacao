#!/usr/bin/env python3
"""Progresso, medidas e diário da sessão de regulagem (skill /regulagem). Só stdlib.

Guarda fora da conversa o que a facilitadora precisa para retomar sem repetir nada:

    uv run ferramentas/sessao.py progresso
        Mostra a hora, o último passo registrado, onde retomar, as medidas e as ideias.

    uv run ferramentas/sessao.py passo --etapa 1 --passo 3 --pedido "…" --resposta "…" [--duvida] [--travou]
        Registra um passo no diário e avança o progresso. --duvida = houve dúvida no meio;
        --travou = o passo não foi concluído (a retomada volta nele).

    uv run ferramentas/sessao.py medida jbl_l_ohm 3.4 ohm [--etapa E --passo N] [--refazer MOTIVO]
        Registra uma medida. Uma medida já registrada só é aceita de novo com --refazer e o
        motivo dito na hora (ex.: "mudou o ganho do módulo"). O valor pode ser número ou texto.

    uv run ferramentas/sessao.py ideia "trocar o cabo do sub"
        Anota uma ideia fora de hora na lista "para depois" e confirma em uma linha.

    uv run ferramentas/sessao.py novo
        Arquiva o progresso atual em progresso-AAAA-MM-DD_HHMM.json e começa do zero. Só quando
        o Denis pedir para recomeçar.

    uv run ferramentas/sessao.py memoria 1 "MUSICA HIGH" --por ia
        Registra que a memória N (1 a 3) do DSP foi salva com esse nome, por quem (ia ou denis)
        e quando. O painel lê isso de estado/memorias.json. Quem salva no aparelho é o Denis,
        pelo app; aqui só fica o registro.

    uv run ferramentas/sessao.py preset "Rock" --por denis
        Registra um preset do celular (EQ de 15 bandas salvo no app). Mesmo nome substitui.
        `memoria N --limpar` esvazia o registro da memória N; `preset "NOME" --remover` tira o
        preset do registro (nada disso toca o aparelho).

    uv run ferramentas/sessao.py modo "Filme" [--por ia|denis]
        Registra o modo que a Vitrine do painel mostra ("Som ajustado pela KARINA · Modo: Filme").
        Só "Filme", "Música High" e "Música Original"; "nenhum" apaga o registro. A /regulagem roda isto
        ao salvar ou carregar uma memória (--por ia é o padrão). É declarado, não lido do aparelho.

Arquivos: estado/sessoes/progresso.json (etapa, passo, medidas, ideias; a cópia anterior fica em
progresso.json.bak), estado/sessoes/AAAA-MM-DD.md (diário legível do dia: pedido, resposta,
dúvida, travou) e estado/memorias.json (memórias do DSP, presets do celular e o modo_ativo da
Vitrine: {nome, por, hora}; sem o campo, a Vitrine não mostra modo).

Códigos de saída: 0 ok; 1 erro de uso ou arquivo ilegível; 2 medida repetida sem --refazer.
"""

from __future__ import annotations

import argparse
import datetime as dt
import json
import math
import os
import sys
import tempfile
import time
import unicodedata
from pathlib import Path

RAIZ = Path(__file__).resolve().parents[1]
FORMATO = 1

# Passos de cada etapa do roteiro (references/roteiro.md). None = varia (etapa 6: por gênero).
PASSOS_POR_ETAPA = {0: 3, 1: 11, 2: 4, 3: 19, 4: 4, 5: 9, 6: None, 7: 3, 8: 1}
ULTIMA_ETAPA = max(PASSOS_POR_ETAPA)

SAIDA_OK, SAIDA_USO, SAIDA_REPETIDA = range(3)


class ProgressoIlegivel(ValueError):
    """progresso.json existe, mas não dá para ler."""


# --------------------------------------------------------------------------- arquivos


def _vazio() -> dict:
    return {
        "formato": FORMATO,
        "atualizado": None,
        "etapa": None,
        "passo": None,
        "travou": False,
        "passos_feitos": [],  # "E.N" de cada passo concluído (sem --travou), na ordem
        "medidas": {},  # nome -> {valor, unidade, hora, etapa, passo, anteriores}
        "ideias": [],  # {hora, texto}
    }


def _validar(dados, origem: str) -> dict:
    if not isinstance(dados, dict) or dados.get("formato") != FORMATO:
        raise ProgressoIlegivel(f"{origem}: formato desconhecido")
    base = _vazio()
    base.update(dados)
    for chave, tipo in (("medidas", dict), ("ideias", list), ("passos_feitos", list)):
        if not isinstance(base[chave], tipo):
            raise ProgressoIlegivel(f"{origem}: campo {chave} fora do formato")
    for chave in ("etapa", "passo"):
        if base[chave] is not None and not isinstance(base[chave], int):
            raise ProgressoIlegivel(f"{origem}: campo {chave} fora do formato")
    if base["etapa"] is not None and base["passo"] is None:
        base["passo"] = 0
    base["travou"] = bool(base["travou"])
    return base


def _ler_json(caminho: Path) -> dict:
    try:
        return _validar(json.loads(caminho.read_text(encoding="utf-8")), _relativo(caminho))
    except (OSError, ValueError) as erro:
        raise ProgressoIlegivel(f"{_relativo(caminho)}: {erro}") from None


def carregar(pasta: Path, avisar=print) -> dict:
    """O progresso. Se progresso.json estiver ilegível mas o .bak carregar, usa o .bak e avisa."""
    caminho = pasta / "progresso.json"
    if not caminho.exists():
        return _vazio()
    try:
        return _ler_json(caminho)
    except ProgressoIlegivel as erro:
        reserva = caminho.with_suffix(".json.bak")
        if not reserva.exists():
            raise
        try:
            dados = _ler_json(reserva)
        except ProgressoIlegivel:
            raise erro from None
        avisar(f"Aviso: {erro}. Usei a cópia anterior ({_relativo(reserva)}).")
        return dados


def gravar(pasta: Path, dados: dict, agora: dt.datetime) -> None:
    """Escreve num temporário e troca de uma vez; a versão anterior vira progresso.json.bak."""
    pasta.mkdir(parents=True, exist_ok=True)
    dados["atualizado"] = agora.isoformat(timespec="seconds")
    caminho = pasta / "progresso.json"
    temporario = caminho.with_suffix(".json.tmp")
    temporario.write_text(json.dumps(dados, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    if caminho.exists():
        _substituir(caminho, caminho.with_suffix(".json.bak"))
    _substituir(temporario, caminho)


def _substituir(origem: Path, destino: Path) -> None:
    """os.replace com uma espera curta: no Windows, um leitor (o painel) com o arquivo aberto
    faz a troca falhar por um instante."""
    for tentativa in range(10):
        try:
            os.replace(origem, destino)
            return
        except PermissionError:
            if tentativa == 9:
                raise
            time.sleep(0.05)


def _ler_texto(caminho: Path) -> str:
    """read_text com a mesma espera curta: no Windows, a troca (os.replace) de outro gravador
    faz a leitura falhar por um instante com PermissionError."""
    for tentativa in range(10):
        try:
            return caminho.read_text(encoding="utf-8")
        except PermissionError:
            if tentativa == 9:
                raise
            time.sleep(0.05)
    raise AssertionError("inalcançável")


def _relativo(caminho: Path) -> str:
    try:
        return caminho.resolve().relative_to(RAIZ).as_posix()
    except ValueError:
        return str(caminho)


def anotar_diario(pasta: Path, agora: dt.datetime, titulo: str, linhas: list[str]) -> Path:
    """Acrescenta um bloco ao diário do dia (cria o arquivo com o cabeçalho na 1ª vez)."""
    pasta.mkdir(parents=True, exist_ok=True)
    caminho = pasta / f"{agora:%Y-%m-%d}.md"
    novo = not caminho.exists()
    with open(caminho, "a", encoding="utf-8") as arquivo:
        if novo:
            arquivo.write(f"# Sessão de regulagem · {agora:%d/%m/%Y}\n\n"
                          "Diário gravado por `ferramentas/sessao.py`: cada passo com o pedido, "
                          "a resposta, se houve dúvida e se travou.\n")
        arquivo.write(f"\n## {agora:%H:%M} · {titulo}\n")
        for linha in linhas:
            arquivo.write(f"- {linha}\n")
    return caminho


# --------------------------------------------------------------------------- memórias

SLOTS_DSP = ("1", "2", "3")
ORIGENS = ("ia", "denis")
# Modos que a Vitrine pode mostrar: palavras que o convidado entende, nada técnico.
MODOS = ("Filme", "Música High", "Música Original")
SEM_MODO = "nenhum"


class ModoInvalido(ValueError):
    """Nome de modo fora de MODOS (e diferente de "nenhum")."""


def _chave_modo(texto: str) -> str:
    sem_acento = "".join(c for c in unicodedata.normalize("NFKD", texto.casefold())
                         if not unicodedata.combining(c))
    return " ".join(sem_acento.replace("-", " ").split())


_MODOS_POR_CHAVE = {_chave_modo(m): m for m in MODOS}
# Nomes de antes de 17/10 (os perfis foram renomeados): continuam aceitos.
_MODOS_POR_CHAVE.update({_chave_modo("Música SPL"): "Música High",
                         _chave_modo("Música EQ"): "Música Original"})


def normalizar_modo(nome) -> str | None:
    """O nome canônico do modo ("musica high" vira "Música High"), ou None para "nenhum".
    Qualquer outro nome levanta ModoInvalido."""
    if not isinstance(nome, str) or not nome.strip():
        raise ModoInvalido("modo vazio")
    chave = _chave_modo(nome)
    if chave == SEM_MODO:
        return None
    if chave not in _MODOS_POR_CHAVE:
        raise ModoInvalido(f"modo desconhecido: {nome.strip()}")
    return _MODOS_POR_CHAVE[chave]


def memorias_vazias() -> dict:
    return {"formato": FORMATO, "atualizado": None, "dsp": {n: None for n in SLOTS_DSP},
            "presets": [], "modo_ativo": None, "erro": None}


def _validar_modo(dados) -> dict | None:
    """modo_ativo validado; um registro estranho é ignorado (a Vitrine omite o modo) em vez de
    tornar o arquivo inteiro ilegível."""
    if not isinstance(dados, dict):
        return None
    try:
        nome = normalizar_modo(dados.get("nome"))
    except ModoInvalido:
        return None
    if nome is None:
        return None
    por = dados.get("por")
    return {"nome": nome, "por": por if por in ORIGENS else None,
            "hora": dados.get("hora") if isinstance(dados.get("hora"), str) else None}


def _validar_registro(dados, origem: str) -> dict | None:
    if dados is None:
        return None
    if not isinstance(dados, dict) or not isinstance(dados.get("nome"), str):
        raise ProgressoIlegivel(f"{origem}: registro fora do formato")
    por = dados.get("por")
    return {"nome": dados["nome"], "por": por if por in ORIGENS else None,
            "hora": dados.get("hora") if isinstance(dados.get("hora"), str) else None}


def validar_memorias(dados) -> dict:
    """estado/memorias.json validado e completo (slots que faltam viram None)."""
    if not isinstance(dados, dict) or dados.get("formato") != FORMATO:
        raise ProgressoIlegivel("memorias.json: formato desconhecido")
    base = memorias_vazias()
    atualizado = dados.get("atualizado")
    base["atualizado"] = atualizado if isinstance(atualizado, str) else None
    dsp = dados.get("dsp") if isinstance(dados.get("dsp"), dict) else {}
    for n in SLOTS_DSP:
        base["dsp"][n] = _validar_registro(dsp.get(n), f"memória {n}")
    presets = dados.get("presets")
    if presets is not None and not isinstance(presets, list):
        raise ProgressoIlegivel("memorias.json: presets fora do formato")
    base["presets"] = [r for r in (_validar_registro(x, "preset") for x in presets or [])
                       if r is not None]
    base["modo_ativo"] = _validar_modo(dados.get("modo_ativo"))
    return base


def carregar_memorias(estado: Path) -> dict:
    caminho = estado / "memorias.json"
    if not caminho.exists():
        return memorias_vazias()
    try:
        return validar_memorias(json.loads(_ler_texto(caminho)))
    except (OSError, ValueError) as erro:
        raise ProgressoIlegivel(f"{_relativo(caminho)}: {erro}") from None


def gravar_memorias(estado: Path, dados: dict, agora: dt.datetime) -> Path:
    """Gravação atômica: um temporário com nome único na mesma pasta e troca de uma vez. O nome
    único deixa o CLI (a /regulagem) e o painel (POST /api/modo) gravarem ao mesmo tempo sem um
    apagar o temporário do outro."""
    estado.mkdir(parents=True, exist_ok=True)
    caminho = estado / "memorias.json"
    dados = {k: v for k, v in dados.items()
             if k != "erro" and not (k == "modo_ativo" and v is None)}  # sem modo = sem o campo
    dados["atualizado"] = agora.isoformat(timespec="seconds")
    with tempfile.NamedTemporaryFile("w", encoding="utf-8", dir=estado, prefix="memorias.",
                                     suffix=".tmp", delete=False) as arquivo:
        arquivo.write(json.dumps(dados, ensure_ascii=False, indent=2) + "\n")
        temporario = Path(arquivo.name)
    try:
        _substituir(temporario, caminho)
    except BaseException:
        temporario.unlink(missing_ok=True)
        raise
    return caminho


def _registro(nome: str, por: str, agora: dt.datetime) -> dict:
    return {"nome": nome, "por": por, "hora": agora.isoformat(timespec="seconds")}


def cmd_memoria(args, estado: Path, agora: dt.datetime) -> int:
    nome = " ".join(args.nome).strip()
    if args.limpar and (nome or args.por):
        print("--limpar vai sozinho: memoria N --limpar")
        return SAIDA_USO
    if not args.limpar and not nome:
        print('Nome vazio. Exemplo: memoria 1 "MUSICA HIGH" --por ia')
        return SAIDA_USO
    if not args.limpar and not args.por:
        print("Falta --por ia ou --por denis.")
        return SAIDA_USO
    try:
        dados = carregar_memorias(estado)
    except ProgressoIlegivel as erro:
        print(f"Não consegui ler as memórias ({erro}). Nada foi gravado.")
        return SAIDA_USO
    slot = str(args.numero)
    anterior = dados["dsp"].get(slot)
    if args.limpar:
        if anterior is None:
            print(f"Memória {slot} já estava vazia.")
            return SAIDA_OK
        dados["dsp"][slot] = None
        gravar_memorias(estado, dados, agora)
        anotar_diario(estado / "sessoes", agora, f"Memória {slot} limpa",
                      [f"Antes: {anterior['nome']}"])
        print(f"Memória {slot} limpa (antes: {anterior['nome']}).")
        return SAIDA_OK
    dados["dsp"][slot] = _registro(nome, args.por, agora)
    gravar_memorias(estado, dados, agora)
    quem = "IA" if args.por == "ia" else "Denis"
    linhas = [f"Memória {slot} do DSP: {nome} (por {quem})"]
    if anterior:
        linhas.append(f"Antes: {anterior['nome']}")
    anotar_diario(estado / "sessoes", agora, f"Memória {slot} salva", linhas)
    extra = f" (antes: {anterior['nome']})" if anterior else ""
    print(f"Memória {slot} registrada: {nome}, por {quem}{extra}.")
    return SAIDA_OK


def cmd_preset(args, estado: Path, agora: dt.datetime) -> int:
    nome = " ".join(args.nome).strip()
    if not nome:
        print('Nome vazio. Exemplo: preset "Rock" --por ia')
        return SAIDA_USO
    if not args.remover and not args.por:
        print("Falta --por ia ou --por denis.")
        return SAIDA_USO
    try:
        dados = carregar_memorias(estado)
    except ProgressoIlegivel as erro:
        print(f"Não consegui ler as memórias ({erro}). Nada foi gravado.")
        return SAIDA_USO
    repetido = any(p["nome"].casefold() == nome.casefold() for p in dados["presets"])
    dados["presets"] = [p for p in dados["presets"] if p["nome"].casefold() != nome.casefold()]
    if args.remover:
        if not repetido:
            print(f"Preset {nome} não estava registrado.")
            return SAIDA_OK
        gravar_memorias(estado, dados, agora)
        anotar_diario(estado / "sessoes", agora, "Preset do celular removido", [nome])
        print(f"Preset removido: {nome}.")
        return SAIDA_OK
    quem = "IA" if args.por == "ia" else "Denis"
    dados["presets"].append(_registro(nome, args.por, agora))
    gravar_memorias(estado, dados, agora)
    anotar_diario(estado / "sessoes", agora, "Preset do celular salvo",
                  [f"{nome} (por {quem})" + (" · substituiu o anterior" if repetido else "")])
    print(f"Preset registrado: {nome}, por {quem}{' (substituído)' if repetido else ''}.")
    return SAIDA_OK


def gravar_modo(estado: Path, nome, por: str, agora: dt.datetime) -> tuple[dict | None, dict | None]:
    """Grava estado/memorias.json → modo_ativo = {nome, por, hora} ("nenhum" apaga o campo).
    Devolve (novo, anterior). Usado pelo CLI (por ia) e pelo POST /api/modo do painel (por denis);
    nos dois casos anota "Modo da Vitrine" no diário do dia, com quem gravou. ModoInvalido para
    nome ou origem fora da lista; ProgressoIlegivel se o arquivo não abre (nada é gravado)."""
    modo = normalizar_modo(nome)
    if por not in ORIGENS:
        raise ModoInvalido(f"origem desconhecida: {por}")
    dados = carregar_memorias(estado)
    anterior = dados.get("modo_ativo")
    novo = None if modo is None else {"nome": modo, "por": por,
                                      "hora": agora.isoformat(timespec="seconds")}
    if novo is None and anterior is None:
        return None, None  # já estava sem modo: nada a gravar
    dados["modo_ativo"] = novo
    gravar_memorias(estado, dados, agora)
    quem = "IA" if por == "ia" else "Denis"
    if novo is None:
        anotar_diario(estado / "sessoes", agora, "Modo da Vitrine apagado",
                      [f"Antes: {anterior['nome']}", f"Por {quem}"])
    else:
        anotar_diario(estado / "sessoes", agora, "Modo da Vitrine",
                      [f"{novo['nome']} (por {quem})"]
                      + ([f"Antes: {anterior['nome']}"] if anterior else []))
    return novo, anterior


def cmd_modo(args, estado: Path, agora: dt.datetime) -> int:
    nome = " ".join(args.nome).strip()
    try:
        novo, anterior = gravar_modo(estado, nome, args.por, agora)
    except ProgressoIlegivel as erro:
        print(f"Não consegui ler as memórias ({erro}). Nada foi gravado.")
        return SAIDA_USO
    except ModoInvalido:
        print(f'Modo desconhecido: {nome or "(vazio)"}. Use "Filme", "Música High", "Música Original" '
              'ou "nenhum".')
        return SAIDA_USO
    antes = f" (antes: {anterior['nome']})" if anterior else ""
    if novo is None:
        if anterior is None:
            print("A Vitrine já estava sem modo.")
            return SAIDA_OK
        print(f'Modo da Vitrine apagado{antes}. A Vitrine mostra só "Som ajustado pela KARINA".')
        return SAIDA_OK
    quem = "IA" if args.por == "ia" else "Denis"
    print(f"Modo da Vitrine: {novo['nome']}, por {quem}{antes}.")
    return SAIDA_OK


# --------------------------------------------------------------------------- comandos


def _valor(texto: str):
    """Número finito quando for número (aceita vírgula decimal); senão o texto como veio."""
    try:
        numero = float(texto.replace(",", "."))
    except ValueError:
        return texto.strip()
    return numero if math.isfinite(numero) else texto.strip()


def _mostrar_valor(medida: dict) -> str:
    v = medida["valor"]
    v = f"{v:g}" if isinstance(v, float) else str(v)
    return f"{v} {medida['unidade']}".rstrip()


def retomada(dados: dict) -> tuple[int, int, str]:
    """(etapa, passo, observação) onde a facilitadora retoma."""
    etapa, passo = dados["etapa"], dados["passo"]
    if etapa is None:
        return 0, 1, ""
    if dados["travou"]:
        return etapa, passo, "o mesmo passo: travou"
    total = PASSOS_POR_ETAPA.get(etapa)
    if total is None:
        return etapa, passo + 1, f"ou etapa {etapa + 1}, passo 1, se a etapa {etapa} acabou"
    if passo >= total:
        if etapa >= ULTIMA_ETAPA:
            return etapa, passo, "roteiro concluído"
        return etapa + 1, 1, f"a etapa {etapa} acabou"
    return etapa, passo + 1, ""


def cmd_progresso(pasta: Path, dados: dict, agora: dt.datetime) -> int:
    print(f"Agora: {agora:%H:%M}")
    if dados["etapa"] is None:
        print("Sessão nova: nenhum passo registrado.")
    else:
        estado = "travou" if dados["travou"] else "concluído"
        print(f"Último passo registrado: etapa {dados['etapa']}, passo {dados['passo']} ({estado}), "
              f"em {dados['atualizado']}.")
    etapa, passo, nota = retomada(dados)
    print(f"Retomar: etapa {etapa}, passo {passo}" + (f" ({nota})" if nota else ""))
    if dados["etapa"] is not None:
        print("Passos feitos: " + (", ".join(dados["passos_feitos"]) or "nenhum"))
    print(f"Medidas ({len(dados['medidas'])}):")
    for nome, m in dados["medidas"].items():
        onde = f", etapa {m['etapa']} passo {m['passo']}" if m.get("etapa") is not None else ""
        refeita = f" · refeita {len(m['anteriores'])}×" if m.get("anteriores") else ""
        print(f"  - {nome} = {_mostrar_valor(m)}  ({m['hora'][11:16]}{onde}{refeita})")
    if not dados["medidas"]:
        print("  (nenhuma)")
    print(f"Para depois ({len(dados['ideias'])}):")
    for i in dados["ideias"]:
        print(f"  - {i['texto']}  ({i['hora'][11:16]})")
    if not dados["ideias"]:
        print("  (nenhuma)")
    diario = pasta / f"{agora:%Y-%m-%d}.md"
    if diario.exists():
        print(f"Diário de hoje: {_relativo(diario)}")
    return SAIDA_OK


def cmd_passo(args, pasta: Path, dados: dict, agora: dt.datetime) -> int:
    chave = f"{args.etapa}.{args.passo}"
    dados["etapa"], dados["passo"], dados["travou"] = args.etapa, args.passo, args.travou
    feitos = dados["passos_feitos"]
    if args.travou:
        dados["passos_feitos"] = [f for f in feitos if f != chave]
    elif chave not in feitos:
        feitos.append(chave)
    gravar(pasta, dados, agora)
    linhas = [f"Pedido: {args.pedido}", f"Resposta: {args.resposta}",
              f"Dúvida: {'sim' if args.duvida else 'não'}",
              f"Travou: {'sim' if args.travou else 'não'}"]
    diario = anotar_diario(pasta, agora, f"Etapa {args.etapa}, passo {args.passo}", linhas)
    estado = "travou aqui" if args.travou else "concluído"
    print(f"Registrado: etapa {args.etapa}, passo {args.passo} ({estado}). Diário: {_relativo(diario)}")
    return SAIDA_OK


def cmd_medida(args, pasta: Path, dados: dict, agora: dt.datetime) -> int:
    nome = args.nome.strip()
    existente = dados["medidas"].get(nome)
    if existente and not args.refazer:
        print(f"Já registrada: {nome} = {_mostrar_valor(existente)} ({existente['hora'][11:16]}). "
              "Não meço de novo sem motivo. Para refazer: --refazer MOTIVO")
        return SAIDA_REPETIDA
    nova = {
        "valor": _valor(args.valor),
        "unidade": args.unidade,
        "hora": agora.isoformat(timespec="seconds"),
        "etapa": args.etapa if args.etapa is not None else dados["etapa"],
        "passo": args.passo if args.passo is not None else dados["passo"],
        "anteriores": [],
    }
    linhas = [f"{nome} = {_mostrar_valor(nova)}"]
    if existente:
        anterior = {k: v for k, v in existente.items() if k != "anteriores"}
        anterior["motivo_da_refeita"] = args.refazer
        nova["anteriores"] = existente.get("anteriores", []) + [anterior]
        linhas.append(f"Refeita (antes {_mostrar_valor(existente)}): {args.refazer}")
    dados["medidas"][nome] = nova
    gravar(pasta, dados, agora)
    anotar_diario(pasta, agora, f"Medida: {nome}", linhas)
    extra = f" (refeita: {args.refazer})" if existente else ""
    print(f"Medida guardada: {nome} = {_mostrar_valor(nova)}{extra}")
    return SAIDA_OK


def cmd_ideia(args, pasta: Path, dados: dict, agora: dt.datetime) -> int:
    texto = " ".join(args.texto).strip()
    if not texto:
        print("Ideia vazia. Exemplo: ideia \"trocar o cabo do sub\"")
        return SAIDA_USO
    dados["ideias"].append({"hora": agora.isoformat(timespec="seconds"), "texto": texto})
    gravar(pasta, dados, agora)
    anotar_diario(pasta, agora, "Ideia para depois", [texto])
    print(f"Anotado para depois: {texto}. Seguimos no passo.")
    return SAIDA_OK


def cmd_novo(pasta: Path, agora: dt.datetime) -> int:
    caminho = pasta / "progresso.json"
    if not caminho.exists():
        print("Nada para arquivar: a sessão já está do zero.")
        return SAIDA_OK
    arquivo = pasta / f"progresso-{agora:%Y-%m-%d_%H%M}.json"
    n = 2
    while arquivo.exists():
        arquivo, n = pasta / f"progresso-{agora:%Y-%m-%d_%H%M}-{n}.json", n + 1
    os.replace(caminho, arquivo)
    reserva = caminho.with_suffix(".json.bak")
    if reserva.exists():
        reserva.unlink()
    anotar_diario(pasta, agora, "Sessão recomeçada do zero",
                  [f"Progresso anterior arquivado em {_relativo(arquivo)}"])
    print(f"Progresso arquivado em {_relativo(arquivo)}. Sessão nova: etapa 0, passo 1.")
    return SAIDA_OK


# --------------------------------------------------------------------------- CLI


class _Parser(argparse.ArgumentParser):
    """Erro de uso sai com código 1 (o 2 do argparse é o da medida repetida)."""

    def error(self, message: str) -> None:  # noqa: D401 - assinatura do argparse
        self.print_usage(sys.stderr)
        self.exit(SAIDA_USO, f"{self.prog}: erro: {message}\n")


def main(argv: list[str] | None = None, *, estado: Path | None = None,
         agora: dt.datetime | None = None) -> int:
    for fluxo in (sys.stdout, sys.stderr):
        try:
            fluxo.reconfigure(encoding="utf-8", errors="replace")
        except (AttributeError, ValueError):
            pass
    ap = _Parser(description="Progresso, medidas e diário da sessão de regulagem.")
    sub = ap.add_subparsers(dest="comando", required=True)

    sub.add_parser("progresso", help="mostra a hora, etapa, passo, medidas e ideias")

    p = sub.add_parser("passo", help="registra um passo no diário e avança o progresso")
    p.add_argument("--etapa", type=int, required=True)
    p.add_argument("--passo", type=int, required=True)
    p.add_argument("--pedido", required=True, help="o que a tela pediu")
    p.add_argument("--resposta", required=True, help="o que o Denis respondeu")
    p.add_argument("--duvida", action="store_true", help="houve dúvida no meio do passo")
    p.add_argument("--travou", action="store_true", help="o passo não foi concluído")

    m = sub.add_parser("medida", help="registra uma medida (recusa repetir sem --refazer)")
    m.add_argument("nome", help="ex.: jbl_l_ohm, saida1_vac, p2_100_vac, zumbido")
    m.add_argument("valor", help="número (3.4 ou 3,4) ou texto (sim/não/pular)")
    m.add_argument("unidade", help="ex.: ohm, Vac, Vdc, Hz, - (sem unidade)")
    m.add_argument("--etapa", type=int)
    m.add_argument("--passo", type=int)
    m.add_argument("--refazer", metavar="MOTIVO", help="motivo para medir de novo")

    i = sub.add_parser("ideia", help="anota uma ideia fora de hora na lista para depois")
    i.add_argument("texto", nargs="+")

    sub.add_parser("novo", help="arquiva o progresso atual e começa do zero")

    me = sub.add_parser("memoria", help="registra o nome de uma memória do DSP (1 a 3)")
    me.add_argument("numero", type=int, choices=(1, 2, 3))
    me.add_argument("nome", nargs="*", help='ex.: "MUSICA HIGH"')
    me.add_argument("--por", choices=ORIGENS, help="quem definiu: ia ou denis")
    me.add_argument("--limpar", action="store_true", help="esvazia o registro da memória N")

    pr = sub.add_parser("preset", help="registra um preset do celular (EQ de 15 bandas)")
    pr.add_argument("nome", nargs="+", help='ex.: "Rock"')
    pr.add_argument("--por", choices=ORIGENS, help="quem definiu: ia ou denis")
    pr.add_argument("--remover", action="store_true", help="tira o preset do registro")

    mo = sub.add_parser("modo", help='registra o modo da Vitrine ("Filme", "Música High", '
                                     '"Música Original" ou "nenhum")')
    mo.add_argument("nome", nargs="+", help='ex.: "Filme"')
    mo.add_argument("--por", choices=ORIGENS, default="ia",
                    help="quem definiu: ia (padrão, a /regulagem) ou denis")

    args = ap.parse_args(argv)
    raiz_estado = estado or RAIZ / "estado"
    pasta = raiz_estado / "sessoes"
    agora = agora or dt.datetime.now()
    if args.comando == "novo":
        return cmd_novo(pasta, agora)
    if args.comando == "memoria":
        return cmd_memoria(args, raiz_estado, agora)
    if args.comando == "preset":
        return cmd_preset(args, raiz_estado, agora)
    if args.comando == "modo":
        return cmd_modo(args, raiz_estado, agora)
    try:
        dados = carregar(pasta)
    except ProgressoIlegivel as erro:
        print(f"Não consegui ler o progresso ({erro}). Nada foi gravado.")
        return SAIDA_USO

    if args.comando == "progresso":
        return cmd_progresso(pasta, dados, agora)
    if args.comando == "passo":
        return cmd_passo(args, pasta, dados, agora)
    if args.comando == "medida":
        return cmd_medida(args, pasta, dados, agora)
    return cmd_ideia(args, pasta, dados, agora)


if __name__ == "__main__":
    sys.exit(main())
