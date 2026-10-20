"""Mapeia o processador Taramps PRO 2.4BT + AUDIO pelo Bluetooth (BLE) do notebook.

SO LEITURA: lista servicos e caracteristicas, le o que for legivel e salva um
relatorio JSON em relatorios/. Nao escreve nenhum valor de audio no aparelho.

Antes de rodar: feche o app Pro 2.4BT+Audio no celular (ou desligue o Bluetooth
dele). O processador provavelmente aceita so uma conexao de controle por vez.

Uso:
    py mapear_gatt.py                    # procura e deixa voce escolher o aparelho
    py mapear_gatt.py --nome taramps     # filtra pelo nome anunciado
    py mapear_gatt.py --endereco AA:BB:CC:DD:EE:FF
    py mapear_gatt.py --ouvir 15         # tambem assina notificacoes por 15 s
"""

import argparse
import asyncio
import datetime
import json
import sys
from pathlib import Path

from bleak import BleakClient, BleakScanner

PASTA_RELATORIOS = Path(__file__).resolve().parent / "relatorios"


def hex_de(dados):
    return dados.hex(" ") if dados else ""


def texto_de(dados):
    try:
        texto = dados.decode("utf-8")
    except (UnicodeDecodeError, AttributeError):
        return None
    return texto if texto.isprintable() else None


async def escolher_aparelho(args):
    print(f"Procurando aparelhos BLE por {args.tempo} s...")
    achados = await BleakScanner.discover(timeout=args.tempo, return_adv=True)
    lista = sorted(achados.values(), key=lambda par: par[1].rssi, reverse=True)
    if args.nome:
        filtro = args.nome.lower()
        lista = [(d, a) for d, a in lista if filtro in (a.local_name or d.name or "").lower()]

    if not lista:
        print("Nenhum aparelho encontrado. Processador ligado? App do celular fechado?")
        return None, None

    for i, (d, a) in enumerate(lista):
        nome = a.local_name or d.name or "(sem nome)"
        print(f"[{i}] {nome:<28} {d.address}  RSSI {a.rssi:>4}  servicos {a.service_uuids}")

    if len(lista) == 1 and args.nome:
        return lista[0]
    escolha = input("Numero do processador na lista (Enter cancela): ").strip()
    if not escolha.isdigit() or int(escolha) >= len(lista):
        return None, None
    return lista[int(escolha)]


def resumo_anuncio(anuncio):
    if anuncio is None:
        return None
    return {
        "nome": anuncio.local_name,
        "rssi": anuncio.rssi,
        "servicos": anuncio.service_uuids,
        "fabricante": {str(k): hex_de(v) for k, v in anuncio.manufacturer_data.items()},
        "dados_de_servico": {k: hex_de(v) for k, v in anuncio.service_data.items()},
        "tx_power": anuncio.tx_power,
    }


async def mapear(endereco, args):
    relatorio = {"endereco": endereco, "servicos": [], "notificacoes": []}
    print(f"\nConectando em {endereco}...")
    async with BleakClient(endereco, timeout=20.0) as cliente:
        print("Conectado. Lendo servicos...\n")
        for servico in cliente.services:
            s = {"uuid": servico.uuid, "descricao": servico.description, "caracteristicas": []}
            print(f"SERVICO {servico.uuid}  {servico.description}")
            for car in servico.characteristics:
                c = {
                    "uuid": car.uuid,
                    "handle": car.handle,
                    "descricao": car.description,
                    "propriedades": car.properties,
                    "descritores": [],
                }
                print(f"  CARACT {car.uuid}  handle {car.handle}  {car.properties}  {car.description}")
                if "read" in car.properties:
                    try:
                        valor = await cliente.read_gatt_char(car)
                        c["valor_hex"] = hex_de(valor)
                        c["valor_texto"] = texto_de(valor)
                        print(f"         valor: {hex_de(valor)}  {texto_de(valor) or ''}")
                    except Exception as erro:  # leitura pode exigir pareamento
                        c["erro_leitura"] = repr(erro)
                        print(f"         erro na leitura: {erro!r}")
                for desc in car.descriptors:
                    d = {"uuid": desc.uuid, "handle": desc.handle, "descricao": desc.description}
                    try:
                        d["valor_hex"] = hex_de(await cliente.read_gatt_descriptor(desc.handle))
                    except Exception as erro:
                        d["erro_leitura"] = repr(erro)
                    c["descritores"].append(d)
                s["caracteristicas"].append(c)
            relatorio["servicos"].append(s)

        if args.ouvir > 0:
            # Assinar notificacoes grava so o descritor padrao de assinatura (CCCD),
            # nao mexe em nenhum ajuste de audio.
            inicio = asyncio.get_running_loop().time()

            def ao_receber(caracteristica, dados):
                t = round(asyncio.get_running_loop().time() - inicio, 3)
                relatorio["notificacoes"].append(
                    {"t": t, "uuid": caracteristica.uuid, "valor_hex": hex_de(dados)}
                )
                print(f"  [{t:7.3f}s] {caracteristica.uuid}: {hex_de(dados)}")

            assinadas = []
            for servico in cliente.services:
                for car in servico.characteristics:
                    if {"notify", "indicate"} & set(car.properties):
                        try:
                            await cliente.start_notify(car, ao_receber)
                            assinadas.append(car)
                        except Exception as erro:
                            print(f"  nao assinou {car.uuid}: {erro!r}")
            print(f"\nOuvindo {len(assinadas)} caracteristicas por {args.ouvir} s...")
            await asyncio.sleep(args.ouvir)
            for car in assinadas:
                try:
                    await cliente.stop_notify(car)
                except Exception:
                    pass
    return relatorio


async def principal():
    analisador = argparse.ArgumentParser(description="Mapeia (so leitura) o processador Taramps por BLE.")
    analisador.add_argument("--nome", help="parte do nome anunciado (ex.: taramps)")
    analisador.add_argument("--endereco", help="endereco BLE, pula a busca")
    analisador.add_argument("--tempo", type=float, default=8.0, help="segundos de busca (padrao 8)")
    analisador.add_argument("--ouvir", type=float, default=0, help="segundos ouvindo notificacoes (padrao 0)")
    args = analisador.parse_args()

    anuncio = None
    endereco = args.endereco
    if not endereco:
        aparelho, anuncio = await escolher_aparelho(args)
        if aparelho is None:
            return 1
        endereco = aparelho.address

    relatorio = await mapear(endereco, args)
    relatorio["anuncio"] = resumo_anuncio(anuncio)
    relatorio["data"] = datetime.datetime.now().isoformat(timespec="seconds")

    PASTA_RELATORIOS.mkdir(exist_ok=True)
    arquivo = PASTA_RELATORIOS / f"gatt-{datetime.datetime.now():%Y%m%d-%H%M%S}.json"
    arquivo.write_text(json.dumps(relatorio, indent=2, ensure_ascii=False), encoding="utf-8")
    print(f"\nRelatorio salvo em {arquivo}")
    return 0


if __name__ == "__main__":
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8")
    sys.exit(asyncio.run(principal()))
