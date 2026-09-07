# tests/unit/conftest.py
"""
Aisla las pruebas unitarias de la base de datos real.

El problema que esto evita, que ya paso: la API construye su
repositorio al importarse (`_repo = construir_repositorio(_cfg)`), asi
que en cuanto existe DATABASE_URL en el .env, importar el modulo de
pruebas de la API abre una conexion a PostgreSQL y las pruebas
escriben sus datos de mentira en la base de verdad.

Poner DATABASE_URL vacia en el entorno tiene prioridad sobre el .env
en pydantic-settings, asi que la API cae al repositorio en memoria.
Hay que hacerlo AQUI porque conftest.py se ejecuta antes de importar
los modulos de prueba, y ademas hay que vaciar la cache de
get_settings por si algo la poblo antes.

Las pruebas de integracion (tests/integration/) no se ven afectadas:
leen DATABASE_URL por su cuenta y se saltan si no esta.
"""

import os

import pytest


@pytest.fixture(scope="session", autouse=True)
def _sin_base_de_datos_real() -> None:
    pass


# Se hace al importar el conftest, no en un fixture: para cuando se
# ejecutase un fixture, el modulo de la API ya estaria importado y la
# conexion ya estaria abierta.
os.environ["DATABASE_URL"] = ""
os.environ["AWS_S3_BUCKET"] = ""

from scraper.config.settings import get_settings  # noqa: E402

get_settings.cache_clear()
