# tests/unit/converse/test_scraper.py
"""
Prueba el scraper con un cliente HTTP falso: sin red, sin esperas.

Es justo lo que habilita inyectar el cliente en el constructor en vez
de crearlo dentro del scraper.
"""

from pathlib import Path

import pytest

from scraper.scrapers.base.scraper_http import DescargaFallida
from scraper.scrapers.converse.scraper import ConverseScraper

FIXTURE = Path(__file__).parents[2] / "fixtures" / "converse" / "product.html"
URL = "https://www.converse.cl/converse-x-dragon-ball-z-chuck-taylor-all-star-a21842c-800-naranja"


class ClienteFalso:
    """Devuelve un HTML fijo y cuenta las llamadas."""

    def __init__(self, html: str = "", fallar_veces: int = 0):
        self.html = html
        self.fallar_veces = fallar_veces
        self.llamadas: list[str] = []

    def get(self, url: str) -> str:
        self.llamadas.append(url)
        if len(self.llamadas) <= self.fallar_veces:
            raise ConnectionError("caida simulada")
        return self.html


@pytest.fixture(scope="module")
def html() -> str:
    return FIXTURE.read_text(encoding="utf-8", errors="replace")


def test_devuelve_el_producto(html):
    cliente = ClienteFalso(html)
    s = ConverseScraper(cliente, delay=0)
    productos = s.scrape(URL)
    assert len(productos) == 1
    assert productos[0].external_id == "A21842C-800"
    assert productos[0].price == 79990
    assert cliente.llamadas == [URL]


def test_reintenta_y_acaba_saliendo_bien(html):
    """Dos caidas seguidas y a la tercera responde."""
    cliente = ClienteFalso(html, fallar_veces=2)
    s = ConverseScraper(cliente, delay=0, reintentos=3)
    assert len(s.scrape(URL)) == 1
    assert len(cliente.llamadas) == 3


def test_si_se_agotan_los_reintentos_lanza_descarga_fallida():
    """
    Lanza en vez de devolver [] para que el servicio pueda distinguir
    "no pude descargar" de "descargue pero no hay producto". El
    servicio lo captura, asi que el barrido no muere.
    """
    cliente = ClienteFalso(fallar_veces=99)
    s = ConverseScraper(cliente, delay=0, reintentos=2)
    with pytest.raises(DescargaFallida):
        s.scrape(URL)
    assert len(cliente.llamadas) == 2


def test_scrape_varios_ignora_las_urls_que_no_bajan():
    """Un producto caido no puede abortar los otros 299."""
    cliente = ClienteFalso(fallar_veces=99)
    s = ConverseScraper(cliente, delay=0, reintentos=1)
    assert s.scrape_varios([URL, URL]) == []


def test_html_que_no_es_ficha_devuelve_vacio():
    s = ConverseScraper(ClienteFalso("<html><body>nada</body></html>"), delay=0)
    assert s.scrape(URL) == []


def test_scrape_varios_no_espera_despues_del_ultimo(html, monkeypatch):
    esperas: list[float] = []
    monkeypatch.setattr("scraper.scrapers.base.scraper_http.time.sleep", esperas.append)
    s = ConverseScraper(ClienteFalso(html), delay=1.5)
    productos = s.scrape_varios([URL, URL, URL])
    assert len(productos) == 3
    # 3 URLs -> 2 esperas, no 3
    assert esperas == [1.5, 1.5]
