#!/usr/bin/env python3
"""Cliente BLE do PRO 2.4BT + AUDIO, com bleak no Windows.

Não é chamado direto: quem usa são ferramentas/ler_processador.py (leitura) e
ferramentas/ajustar_processador.py (escrita atrás do guardião).

- `TransporteBleak`: scan pelo UUID 0x2055 ou pelo serviço `…e58f`, conexão com
  3 tentativas (sem pareamento), notificação ou indicação na `…1123` e escrita na
  `…1122` com o tipo que a característica permite (MAPA §1 e §3).
- `ClienteTaramps`: autenticação, consulta com espera de resposta (uma pendente por
  vez, ~30 ms entre envios, 5 s de timeout) e log bruto de cada quadro.

Há só três caminhos até o BLE:
- `enviar`: passa por `protocolo_taramps.verificar_envio`. O que não é consulta nem
  autenticação é recusado antes de chegar ao BLE.
- `escrever`: exige a aprovação selada do guardião (guardiao.py), item por item e na
  ordem aprovada. Sem ela, a chamada é recusada antes de qualquer I/O. Cada escrita
  espera a janela de ack (respostas viram órfãos no log) e é relida em seguida.
- `enviar_especial`: tom fixo do gerador e salvar memória, só com a aprovação especial do
  guardião e só os textos exatos dela (`protocolo_taramps.verificar_comando_especial`).
  `desligar_gerador_garantido` manda `*202*0*`, relê e, se a conexão caiu, reconecta.

O bleak só é importado ao conectar, então o cliente é testável com um transporte falso.
"""

from __future__ import annotations

import asyncio
import time
from dataclasses import dataclass
from typing import Callable

import guardiao as gd
import protocolo_taramps as pt

INTERVALO_S = 0.03  # entre envios (MAPA §2)
TIMEOUT_S = 5.0  # por consulta (MAPA §2)
OCIOSO_S = 0.3  # silêncio que fecha uma resposta que chegou sem separador no fim
JANELA_ACK_S = 0.5  # espera depois de uma escrita, antes da releitura
SCAN_S = 10.0
CONEXAO_S = 15.0
TENTATIVAS = 3
MAX_TIMEOUTS_SEGUIDOS = 8  # depois disso o aparelho é dado como mudo e a leitura para


class ConexaoCaiu(RuntimeError):
    """O processador desconectou no meio da conversa."""


class ConexaoFalhou(RuntimeError):
    """Não deu para conectar depois das tentativas."""


# --------------------------------------------------------------------------- transporte


class TransporteBleak:
    """Fala com o processador pelo bleak. Não pede bond nem mexe no Windows."""

    def __init__(self) -> None:
        self.ao_receber: Callable[[bytes], None] = lambda dados: None
        self.ao_cair: Callable[[], None] = lambda: None
        self._cliente = None
        self._escrita = None
        self._conectado = False  # só depois disso uma desconexão conta como queda
        self.dispositivo = None
        self.propriedades: dict[str, list[str]] = {}

    async def procurar(self, timeout: float = SCAN_S):
        from bleak import BleakScanner

        alvos = {pt.UUID_ANUNCIO, pt.UUID_SERVICO}

        def filtro(_dispositivo, anuncio) -> bool:
            return any(u.lower() in alvos for u in (anuncio.service_uuids or []))

        self.dispositivo = await BleakScanner.find_device_by_filter(filtro, timeout=timeout)
        return self.dispositivo

    async def conectar(self, dispositivo, tentativas: int = TENTATIVAS) -> None:
        from bleak import BleakClient

        ultimo_erro: Exception | None = None
        for _ in range(tentativas):
            cliente = BleakClient(
                dispositivo, disconnected_callback=self._desconectou, timeout=CONEXAO_S
            )
            try:
                await cliente.connect()
                servico = cliente.services.get_service(pt.UUID_SERVICO)
                if servico is None:
                    raise ConexaoFalhou("o serviço do processador não apareceu")
                self._escrita = servico.get_characteristic(pt.UUID_ESCRITA)
                resposta = servico.get_characteristic(pt.UUID_RESPOSTA)
                if self._escrita is None or resposta is None:
                    raise ConexaoFalhou("as características 1122/1123 não apareceram")
                self.propriedades = {
                    "1122": list(self._escrita.properties),
                    "1123": list(resposta.properties),
                }
                # O bleak usa notificação quando há e indicação quando só há ela (como o app).
                await cliente.start_notify(resposta, lambda _c, dados: self.ao_receber(bytes(dados)))
                self._cliente = cliente
                self._conectado = True
                return
            except Exception as erro:  # noqa: BLE001 - qualquer falha conta como tentativa
                ultimo_erro = erro
                try:
                    await cliente.disconnect()
                except Exception:  # noqa: BLE001
                    pass
                await asyncio.sleep(0.5)
        raise ConexaoFalhou(str(ultimo_erro) if ultimo_erro else "sem detalhe")

    def _desconectou(self, _cliente) -> None:
        # Uma tentativa de conexão que falha também dispara o callback: ignorar até conectar.
        if self._conectado:
            self.ao_cair()

    async def escrever(self, dados: bytes, preferir_resposta: bool) -> None:
        """Consultas vão sem resposta quando a característica permite; a senha vai com
        resposta quando permite (MAPA §1)."""
        props = set(self._escrita.properties)
        if preferir_resposta:
            com_resposta = "write" in props or "write-without-response" not in props
        else:
            com_resposta = "write-without-response" not in props
        await self._cliente.write_gatt_char(self._escrita, dados, response=com_resposta)

    async def desconectar(self) -> None:
        if self._cliente is not None:
            try:
                await self._cliente.disconnect()
            except Exception:  # noqa: BLE001
                pass


# --------------------------------------------------------------------------- sessão


@dataclass
class Autenticacao:
    ok: bool
    carga: str | None = None  # resposta 000 (informações do produto)
    firmware: str | None = None
    motivo: str | None = None  # "recusada" | "sem resposta"


class ClienteTaramps:
    def __init__(self, transporte, log: pt.LogBruto, *, intervalo: float = INTERVALO_S,
                 timeout: float = TIMEOUT_S, ocioso: float = OCIOSO_S,
                 janela_ack: float = JANELA_ACK_S) -> None:
        self.transporte = transporte
        self.log = log
        self.intervalo = intervalo
        self.timeout = timeout
        self.ocioso = ocioso
        self.janela_ack = janela_ack
        self._aprovacoes: dict[str, int] = {}  # selo -> próximo item; -1 = interrompida
        self._especiais_usados: set[tuple[str, str]] = set()  # (selo, texto) já enviados
        # True do envio de `*202*1*` (ou de uma leitura com o gerador ligado) até uma releitura
        # confirmar 202 = 0. Quem toca o tom desliga enquanto isto for True.
        self.gerador_talvez_ligado = False
        self.caiu = False
        self._encerrando = False
        self.orfaos: list[str] = []
        self._montador = pt.Montador()
        self._chegou = asyncio.Event()
        self._ultimo_dado = 0.0
        self._ultimo_envio = 0.0
        transporte.ao_receber = self._receber
        transporte.ao_cair = self._cair

    # -- eventos do transporte

    def _receber(self, dados: bytes) -> None:
        self.log.registrar(pt.RX, dados)
        self._montador.alimentar(dados)
        self._ultimo_dado = time.monotonic()
        self._chegou.set()

    def _cair(self) -> None:
        if self._encerrando:
            return
        if not self.caiu:
            self.caiu = True
            try:
                self.log.anotar("conexão caiu")
            except ValueError:  # log já fechado (callback atrasado)
                pass
        self._chegou.set()

    def _registrar_orfaos(self, orfaos: list[str], nota: str = "quadro sem consulta") -> None:
        for texto in orfaos:
            self.orfaos.append(self.log.ocultar(texto))
            self.log.registrar("ORFAO", texto.encode("utf-8", "replace"), nota)

    async def encerrar(self) -> None:
        """Desconecta de propósito (sem registrar como queda)."""
        self._encerrando = True
        await self.transporte.desconectar()

    # -- envio

    async def enviar(self, texto: str) -> None:
        """Caminho de leitura até o BLE. Recusa tudo que não é consulta ou autenticação."""
        dados = pt.verificar_envio(texto)  # levanta EnvioRecusado antes de qualquer I/O
        await self._transmitir(dados, preferir_resposta=pt.eh_autenticacao(texto))

    async def _transmitir(self, dados: bytes, preferir_resposta: bool, nota: str = "") -> None:
        """Bytes já conferidos por `enviar` ou `escrever`. Não chamar de fora."""
        if self.caiu:
            raise ConexaoCaiu("sem conexão")
        espera = self._ultimo_envio + self.intervalo - time.monotonic()
        if espera > 0:
            await asyncio.sleep(espera)
        self.log.registrar(pt.TX, dados, nota)
        try:
            await asyncio.wait_for(
                self.transporte.escrever(dados, preferir_resposta=preferir_resposta),
                timeout=self.timeout,
            )
        except asyncio.TimeoutError:
            raise TimeoutError(f"escrita presa por {self.timeout:g} s") from None
        self._ultimo_envio = time.monotonic()

    async def escrever(self, aprovacao, indice: int) -> dict:
        """Escreve o item `indice` de uma aprovação do guardião, espera a janela de ack e
        relê o parâmetro.

        Recusa (gd.EscritaNaoAprovada, antes de qualquer I/O) aprovação ausente, forjada
        ou alterada, item fora da ordem aprovada e aprovação de um lote já interrompido.
        Devolve {codigo, canal, escrito, pedido, relido, cru, status}, com status "igual",
        "divergente" ou "sem resposta". Queda e timeout levantam exceção. Só um item
        "igual" libera o próximo."""
        escrita = gd.item_aprovado(aprovacao, indice)
        proximo = self._aprovacoes.get(aprovacao.selo, 0)
        if proximo < 0:
            raise gd.EscritaNaoAprovada("escrita recusada: o lote foi interrompido")
        if indice != proximo:
            raise gd.EscritaNaoAprovada(
                f"escrita recusada: fora da ordem aprovada (o próximo é o item {proximo + 1})")
        texto = pt.montar_escrita(escrita.codigo, escrita.canal, escrita.valor)
        dados = pt.verificar_escrita(texto)
        self._aprovacoes[aprovacao.selo] = -1  # só volta a valer se a releitura bater
        self._registrar_orfaos(self._montador.descartar())
        await self._transmitir(dados, preferir_resposta=True, nota="escrita")
        await self._janela_ack()
        p = pt.POR_CODIGO[escrita.codigo]
        # Eco atrasado da escrita (`*081*0*-12.0*`) nunca confirma a releitura.
        item = await self.consultar(pt.Consulta(p, escrita.canal), sem_asterisco=True)
        relido = item.get("valor")
        if item.get("cru") is None:
            status = "sem resposta"
        elif pt.mesmo_valor(escrita.codigo, escrita.valor, relido):
            status = "igual"
        else:
            status = "divergente"
        if status == "igual":
            self._aprovacoes[aprovacao.selo] = indice + 1
        self.log.anotar(f"releitura de {texto}: {status}")
        return {"codigo": escrita.codigo, "canal": escrita.canal, "escrito": texto,
                "pedido": escrita.valor, "relido": relido, "cru": item.get("cru"), "status": status}

    # -- comandos especiais (spec-gerador-memoria.md)

    async def enviar_especial(self, aprovacao, texto: str) -> None:
        """Tom fixo (200, 201, 202) e salvar memória (240, 231–233), só com a aprovação
        especial do guardião (gd.avaliar_tom, gd.avaliar_salvar_memoria ou
        gd.aprovar_desligar_gerador). O texto passa por pt.verificar_comando_especial antes de
        qualquer I/O. Cada comando de uma aprovação sai uma vez só; o `*202*0*` (desligar),
        quantas vezes for preciso. Espera a janela de ack (o que chegar vira órfão no log)."""
        dados = pt.verificar_comando_especial(texto, aprovacao)  # recusa antes de qualquer I/O
        chave = (aprovacao.selo, texto)
        if texto != gd.DESLIGAR_GERADOR:
            if chave in self._especiais_usados:
                raise gd.EscritaNaoAprovada(f"comando especial recusado: {texto} já foi enviado "
                                            "com esta aprovação")
            self._especiais_usados.add(chave)
        if texto == "*202*1*":
            self.gerador_talvez_ligado = True  # antes do envio: a queda no meio também conta
        self._registrar_orfaos(self._montador.descartar())
        await self._transmitir(dados, preferir_resposta=True, nota="especial")
        await self._janela_ack()

    async def consultar_especial(self, parametro: pt.Parametro) -> dict:
        """Leitura do tom fixo (200, 201, 202) ou do nome de uma memória (231–233), que a trava
        de leitura não deixa passar: só os seis textos de pt.verificar_consulta_especial. Um
        eco no formato de escrita (`*202*0*`) nunca conta como resposta."""
        consulta = pt.Consulta(parametro)
        dados = pt.verificar_consulta_especial(consulta.texto)  # recusa antes de qualquer I/O
        self._registrar_orfaos(self._montador.descartar())
        await self._transmitir(dados, preferir_resposta=False)
        casamento = await self._esperar(consulta, sem_asterisco=True)
        return pt.leitura(consulta, casamento, None if casamento else "timeout")

    async def ler_gerador(self) -> dict:
        """{"200": item, "201": item, "202": item} do tom fixo (leitura). Gerador lido ligado
        marca `gerador_talvez_ligado`."""
        itens = {}
        for p in (pt.GERADOR_GANHO, pt.GERADOR_FREQ, pt.GERADOR_LIGADO):
            itens[p.codigo] = await self.consultar_especial(p)
        if itens["202"].get("valor") is True:
            self.gerador_talvez_ligado = True
        return itens

    async def desligar_gerador(self, aprovacao) -> bool:
        """`*202*0*` e releitura. True só com o 202 relido igual a 0."""
        await self.enviar_especial(aprovacao, gd.DESLIGAR_GERADOR)
        item = await self.consultar_especial(pt.GERADOR_LIGADO)
        desligado = item.get("cru") is not None and item.get("valor") is False
        if desligado:
            self.gerador_talvez_ligado = False
        self.log.anotar(f"releitura do gerador: {item.get('cru')!r} "
                        f"({'desligado' if desligado else 'NÃO confirmado'})")
        return desligado

    async def reconectar(self, senha: str) -> bool:
        """Procura, conecta e autentica de novo depois de uma queda. True se a senha passou."""
        self.caiu = False
        self._encerrando = False
        self._registrar_orfaos(self._montador.descartar())
        self.log.anotar("reconectando")
        try:
            dispositivo = await self.transporte.procurar()
            if dispositivo is None:
                self.log.anotar("reconexão: processador não encontrado")
                ok = False
            else:
                await self.transporte.conectar(dispositivo)
                auth = await self.autenticar(senha)
                self.log.anotar(f"reconexão: {'senha aceita' if auth.ok else auth.motivo}")
                ok = auth.ok
        except BaseException:
            self.caiu = True  # continua sem conexão: a próxima rodada tenta reconectar de novo
            raise
        if not ok:
            self.caiu = True
        return ok

    async def desligar_gerador_garantido(self, aprovacao, senha: str | None,
                                         tentativas: int = TENTATIVAS) -> bool:
        """Desliga o gerador e confirma pela releitura, em até `tentativas` rodadas. Se a
        conexão caiu, reconecta (com a senha) antes de cada rodada. True = 202 relido 0."""
        for rodada in range(1, tentativas + 1):
            try:
                if self.caiu:
                    if not senha or not await self.reconectar(senha):
                        continue
                if await self.desligar_gerador(aprovacao):
                    return True
            except gd.EscritaNaoAprovada:
                raise
            except Exception as erro:  # noqa: BLE001 - queda, timeout ou erro do BLE: tenta de novo
                try:
                    self.log.anotar(f"desligar o gerador, tentativa {rodada}: {erro!r}")
                except ValueError:  # log já fechado
                    pass
        return False

    async def _janela_ack(self) -> None:
        """Espera o que o aparelho mandar depois da escrita. Tudo vira órfão no log."""
        limite = time.monotonic() + self.janela_ack
        while not self.caiu:
            agora = time.monotonic()
            if agora >= limite:
                break
            if not self._montador.vazio and agora - self._ultimo_dado >= self.ocioso:
                break
            self._chegou.clear()
            try:
                await asyncio.wait_for(self._chegou.wait(),
                                       timeout=max(min(0.05, limite - agora), 0.001))
            except asyncio.TimeoutError:
                pass
        if self.caiu:
            raise ConexaoCaiu("a conexão caiu depois da escrita")
        self._registrar_orfaos(self._montador.descartar(), "depois da escrita")

    async def _esperar(self, consulta: pt.Consulta, enviado: str | None = None,
                       sem_asterisco: bool = False) -> pt.Casamento | None:
        """Espera a resposta da consulta. None = timeout. `sem_asterisco`: ver Montador.casar."""
        limite = time.monotonic() + self.timeout
        while True:
            if self.caiu:
                raise ConexaoCaiu("a conexão caiu durante a consulta")
            casamento = self._montador.casar(consulta, enviado=enviado, sem_asterisco=sem_asterisco)
            if casamento is not None:
                self._registrar_orfaos(casamento.orfaos)
                return casamento
            agora = time.monotonic()
            if agora >= limite:
                casamento = self._montador.casar(consulta, final=True, enviado=enviado,
                                                 sem_asterisco=sem_asterisco)
                if casamento is not None:
                    self._registrar_orfaos(casamento.orfaos)
                    if casamento.cru is not None:
                        return casamento
                return None
            espera = limite - agora
            if not self._montador.vazio:
                silencio = agora - self._ultimo_dado
                if silencio >= self.ocioso:
                    casamento = self._montador.casar(consulta, final=True, enviado=enviado,
                                                 sem_asterisco=sem_asterisco)
                    if casamento is not None:
                        self._registrar_orfaos(casamento.orfaos)
                        if casamento.cru is not None:
                            return casamento
                    continue
                espera = min(espera, self.ocioso - silencio)
            self._chegou.clear()
            try:
                await asyncio.wait_for(self._chegou.wait(), timeout=max(espera, 0.001))
            except asyncio.TimeoutError:
                pass

    async def consultar(self, consulta: pt.Consulta, sem_asterisco: bool = False) -> dict:
        self._registrar_orfaos(self._montador.descartar())  # sobras de respostas atrasadas
        await self.enviar(consulta.texto)
        casamento = await self._esperar(consulta, sem_asterisco=sem_asterisco)
        return pt.leitura(consulta, casamento, None if casamento else "timeout")

    async def autenticar(self, senha: str) -> Autenticacao:
        self._registrar_orfaos(self._montador.descartar())
        texto = pt.montar_autenticacao(senha)
        await self.enviar(texto)
        casamento = await self._esperar(pt.Consulta(pt.AUTENTICACAO), enviado=texto)
        if casamento is None:
            return Autenticacao(False, motivo="sem resposta")
        carga = (casamento.valor or "").strip()
        if not carga or (len(carga) < 4 and "ERRO".startswith(carga.upper())):
            return Autenticacao(False, motivo="sem resposta")  # vazio ou "ERRO" cortado
        if carga.upper() == "ERRO":
            return Autenticacao(False, carga=carga, motivo="recusada")
        return Autenticacao(True, carga=carga, firmware=pt.firmware_de(carga))

    async def ler_tudo(self, consultas: list[pt.Consulta],
                       progresso: Callable[[int, int], None] | None = None) -> tuple[list[dict], bool]:
        """Lê as consultas uma a uma. Devolve (leituras, completo).

        Consulta sem resposta vira valor null com o motivo e a leitura continua. Se a
        conexão cair, ou se o aparelho ficar mudo por MAX_TIMEOUTS_SEGUIDOS consultas,
        o resto vira null com o motivo e o retrato sai incompleto."""
        leituras: list[dict] = []
        seguidos = 0
        motivo_parada: str | None = None
        for i, consulta in enumerate(consultas):
            if motivo_parada is None:
                try:
                    item = await self.consultar(consulta)
                except ConexaoCaiu:
                    motivo_parada = "conexão caiu"
                    item = pt.leitura(consulta, None, motivo_parada)
                except pt.EnvioRecusado:
                    raise
                except Exception as erro:  # noqa: BLE001 - falha do BLE nesta consulta
                    self.log.anotar(f"erro em {consulta.texto}: {erro!r}")
                    item = pt.leitura(consulta, None, f"erro do BLE: {erro}")
                if motivo_parada is None:
                    seguidos = seguidos + 1 if item.get("cru") is None else 0  # sem resposta
                    if self.caiu:
                        motivo_parada = "conexão caiu"
                    elif seguidos >= MAX_TIMEOUTS_SEGUIDOS:
                        motivo_parada = "não consultado: o aparelho parou de responder"
            else:
                item = pt.leitura(consulta, None, motivo_parada)
            leituras.append(item)
            if progresso:
                progresso(i + 1, len(consultas))
        return leituras, motivo_parada is None

    async def bruto(self, texto: str, janela: float | None = None) -> list[bytes]:
        """Manda uma única consulta e devolve o que chegar até a resposta se calar."""
        self._registrar_orfaos(self._montador.descartar())
        recebidos: list[bytes] = []
        anterior = self.transporte.ao_receber

        def guardar(dados: bytes) -> None:
            recebidos.append(bytes(dados))
            anterior(dados)

        self.transporte.ao_receber = guardar
        try:
            await self.enviar(texto)
            limite = time.monotonic() + (janela or self.timeout)
            while time.monotonic() < limite and not self.caiu:
                if recebidos and time.monotonic() - self._ultimo_dado >= self.ocioso:
                    break
                self._chegou.clear()
                try:
                    await asyncio.wait_for(self._chegou.wait(), timeout=0.05)
                except asyncio.TimeoutError:
                    pass
        finally:
            self.transporte.ao_receber = anterior
            self._montador.descartar()
        return recebidos
