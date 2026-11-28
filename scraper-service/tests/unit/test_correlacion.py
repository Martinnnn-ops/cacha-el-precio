"""El correlationId del barrido viaja en los sobres (docs/PLAN-EFT.md §3)."""

from datetime import UTC, datetime

from scraper.infrastructure.http.ingesta_cola_client import sobre
from scraper.observabilidad import con_correlacion, correlacion_actual, id_barrido


def test_id_barrido_es_legible():
    momento = datetime(2026, 11, 28, 0, 0, 12, tzinfo=UTC)
    assert id_barrido("paris", momento) == "barrido-paris-20261128T000012Z"


def test_sin_barrido_el_sobre_no_lleva_correlation_id():
    assert "correlationId" not in sobre({"store": "paris"})


def test_dentro_de_un_barrido_el_sobre_lleva_su_id():
    with con_correlacion("barrido-paris-1"):
        s = sobre({"store": "paris"})
    assert s["correlationId"] == "barrido-paris-1"


def test_el_id_se_suelta_al_salir_del_bloque():
    with con_correlacion("barrido-hites-1"):
        assert correlacion_actual() == "barrido-hites-1"
    assert correlacion_actual() is None
