# src/scraper/infrastructure/streaming/kinesis_precios.py
"""
Productor del stream de precios (EA3/EFT, Kinesis).

Despues de que una prenda se acepta y se publica (flujo 1), su precio tambien
se manda al stream cep-precios. Es ANALISIS, no trabajo: product-service lo lee
aparte para calcular el "precio habitual" de cada oferta (docs/PLAN-EFT.md §5,
ADR-035). El flujo 1 ya guardo la prenda, asi que esto es un efecto de lado y de
mejor esfuerzo: si Kinesis falla, se registra un aviso y el barrido sigue; el
precio no se pierde porque quedo en el catalogo.

Igual que las imagenes a S3, el productor usa boto3 con el perfil de la EC2
(LabInstanceProfile). Sin KINESIS_PRECIOS_STREAM no se construye y el barrido
corre igual, como el consumidor de product-service sin la URL de la cola.

`clave_particion` y `registro_precio` son funciones puras (no tocan boto3): se
prueban sin AWS.
"""

from __future__ import annotations

import json
import logging
import uuid
from datetime import UTC, datetime
from typing import Protocol

from scraper.domain.product import Product
from scraper.observabilidad import correlacion_actual

log = logging.getLogger(__name__)

TIPO = "precio.capturado"
VERSION = 1
FUENTE = "scraper-service"


def clave_particion(producto: Product) -> str:
    """
    El carril del stream. Se agrupa por (tienda, id externo) para que todas las
    capturas de una misma oferta caigan en el mismo shard y lleguen en orden al
    lector. Es la misma identidad que usa el catalogo.
    """
    return f"{producto.store}#{producto.external_id}"


def registro_precio(producto: Product, ahora: datetime | None = None) -> dict:
    """
    Envuelve un precio capturado en el sobre precio.capturado v1, con un eventId
    nuevo para que el lector pueda descartar duplicados (Kinesis entrega "al menos
    una vez"). Mismo estilo de sobre que oferta.capturada (docs/MENSAJERIA.md).
    """
    emitido = (ahora or datetime.now(UTC)).isoformat().replace("+00:00", "Z")
    registro = {
        "eventId": str(uuid.uuid4()),
        "type": TIPO,
        "version": VERSION,
        "source": FUENTE,
        "occurredAt": emitido,
        "data": {
            "store": producto.store,
            "externalId": producto.external_id,
            "price": producto.price,
            "currency": producto.currency,
            "capturedAt": producto.scraped_at.isoformat().replace("+00:00", "Z"),
        },
    }
    # Opcional: el numero de seguimiento del barrido, si lo hay (PLAN-EFT §3).
    if cid := correlacion_actual():
        registro["correlationId"] = cid
    return registro


class PublicadorPrecios(Protocol):
    """Lo que ProductServiceSync necesita para mandar un precio al stream."""

    def publicar(self, producto: Product) -> None: ...


class PrecioStreamKinesis:
    """
    Escribe cada precio capturado en el stream de Kinesis con put_record.

    `cliente` se inyecta en las pruebas (un doble); en produccion se crea un
    cliente de boto3 que toma las credenciales del perfil de la EC2.
    """

    def __init__(
        self,
        nombre_stream: str,
        region: str = "us-east-1",
        cliente=None,
    ) -> None:
        self._nombre = nombre_stream
        if cliente is not None:
            self._kinesis = cliente
        else:
            import boto3

            self._kinesis = boto3.client("kinesis", region_name=region)

    def publicar(self, producto: Product) -> None:
        registro = registro_precio(producto)
        datos = (json.dumps(registro, ensure_ascii=False) + "\n").encode("utf-8")
        try:
            self._kinesis.put_record(
                StreamName=self._nombre,
                PartitionKey=clave_particion(producto),
                Data=datos,
            )
            log.debug(
                "Precio al stream %s/%s eventId=%s",
                producto.store,
                producto.external_id,
                registro["eventId"],
            )
        # De mejor esfuerzo: cualquier fallo de Kinesis (sin permiso, sin red,
        # throttling) se registra y no corta el barrido. El precio ya quedo en el
        # catalogo por el flujo 1.
        except Exception as e:
            log.warning(
                "No se pudo mandar el precio de %s/%s al stream: %s",
                producto.store,
                producto.external_id,
                e,
            )
