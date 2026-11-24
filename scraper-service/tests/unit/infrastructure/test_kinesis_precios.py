"""Pruebas del productor del stream de precios (Kinesis). Sin boto3 ni AWS."""

import json
from datetime import UTC, datetime

from scraper.domain.product import Product
from scraper.infrastructure.streaming.kinesis_precios import (
    PrecioStreamKinesis,
    clave_particion,
    registro_precio,
)


class KinesisFalso:
    """Doble del cliente de boto3: guarda lo que se le manda."""

    def __init__(self, revienta=False):
        self.registros = []
        self._revienta = revienta

    def put_record(self, **kwargs):
        if self._revienta:
            raise RuntimeError("sin permiso para kinesis:PutRecord")
        self.registros.append(kwargs)
        return {"ShardId": "shardId-000000000000", "SequenceNumber": "49"}


def producto(store="paris", external_id="SKU-1", precio=19990):
    return Product(
        external_id=external_id,
        store=store,
        name="Poleron",
        brand="CK",
        price=precio,
        product_url="https://paris.cl/x",
        scraped_at=datetime(2026, 11, 24, 12, 0, tzinfo=UTC),
    )


def test_clave_particion_agrupa_por_tienda_y_id():
    assert clave_particion(producto(store="ripley", external_id="A1")) == "ripley#A1"


def test_registro_lleva_sobre_y_datos_del_precio():
    reg = registro_precio(producto(precio=19990), ahora=datetime(2026, 11, 24, 12, 0, tzinfo=UTC))

    assert reg["type"] == "precio.capturado"
    assert reg["version"] == 1
    assert reg["source"] == "scraper-service"
    assert reg["occurredAt"] == "2026-11-24T12:00:00Z"
    assert reg["eventId"]  # un uuid nuevo, no vacio
    assert reg["data"] == {
        "store": "paris",
        "externalId": "SKU-1",
        "price": 19990,
        "currency": "CLP",
        "capturedAt": "2026-11-24T12:00:00Z",
    }


def test_cada_registro_trae_un_event_id_distinto():
    uno = registro_precio(producto())
    otro = registro_precio(producto())
    assert uno["eventId"] != otro["eventId"]


def test_publicar_manda_el_registro_con_su_clave_de_particion():
    kinesis = KinesisFalso()
    stream = PrecioStreamKinesis("cep-precios", cliente=kinesis)

    stream.publicar(producto(store="hites", external_id="Z9", precio=12990))

    assert len(kinesis.registros) == 1
    enviado = kinesis.registros[0]
    assert enviado["StreamName"] == "cep-precios"
    assert enviado["PartitionKey"] == "hites#Z9"
    cuerpo = json.loads(enviado["Data"].decode("utf-8"))
    assert cuerpo["data"]["price"] == 12990


def test_un_fallo_de_kinesis_no_corta_el_barrido():
    # De mejor esfuerzo: si Kinesis niega el permiso, se traga el error.
    stream = PrecioStreamKinesis("cep-precios", cliente=KinesisFalso(revienta=True))
    stream.publicar(producto())  # no debe lanzar
