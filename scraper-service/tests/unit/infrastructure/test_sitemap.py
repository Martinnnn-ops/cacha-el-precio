# tests/unit/infrastructure/test_sitemap.py
"""Lector de sitemaps, con cliente falso: sin red."""

import gzip

from scraper.infrastructure.http.sitemap import leer_sitemap

INDICE = b"""<?xml version="1.0"?>
<sitemapindex xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
  <sitemap><loc>https://t.cl/s1.xml</loc></sitemap>
  <sitemap><loc>https://t.cl/s2.xml</loc></sitemap>
</sitemapindex>"""

def urlset(*urls: str) -> bytes:
    filas = "".join(f"<url><loc>{u}</loc></url>" for u in urls)
    return f'<?xml version="1.0"?><urlset>{filas}</urlset>'.encode()


class ClienteFalso:
    def __init__(self, mapa, falla_en=()):
        self.mapa = mapa
        self.falla_en = set(falla_en)
        self.pedidas: list[str] = []

    def get_bytes(self, url: str) -> bytes:
        self.pedidas.append(url)
        if url in self.falla_en:
            raise ConnectionError("caida simulada")
        return self.mapa[url]


def test_urlset_plano():
    c = ClienteFalso({"https://t.cl/s.xml": urlset("https://t.cl/p/1", "https://t.cl/p/2")})
    assert leer_sitemap("https://t.cl/s.xml", c) == ["https://t.cl/p/1", "https://t.cl/p/2"]


def test_sigue_los_indices():
    c = ClienteFalso({
        "https://t.cl/i.xml": INDICE,
        "https://t.cl/s1.xml": urlset("https://t.cl/p/1"),
        "https://t.cl/s2.xml": urlset("https://t.cl/p/2"),
    })
    assert leer_sitemap("https://t.cl/i.xml", c) == ["https://t.cl/p/1", "https://t.cl/p/2"]


def test_descomprime_gzip():
    """Los sitemaps grandes vienen en .gz aunque la URL no lo diga."""
    c = ClienteFalso({"https://t.cl/s.xml": gzip.compress(urlset("https://t.cl/p/1"))})
    assert leer_sitemap("https://t.cl/s.xml", c) == ["https://t.cl/p/1"]


def test_max_urls_corta_y_no_baja_mas_sitemaps():
    """
    Con 1,5 millones de URLs, el corte tiene que evitar ademas seguir
    descargando sub-sitemaps que ya no hacen falta.
    """
    c = ClienteFalso({
        "https://t.cl/i.xml": INDICE,
        "https://t.cl/s1.xml": urlset("https://t.cl/p/1", "https://t.cl/p/2"),
        "https://t.cl/s2.xml": urlset("https://t.cl/p/3"),
    })
    assert leer_sitemap("https://t.cl/i.xml", c, max_urls=2) == ["https://t.cl/p/1", "https://t.cl/p/2"]
    assert "https://t.cl/s2.xml" not in c.pedidas      # no se descargo de mas


def test_un_sub_sitemap_caido_no_pierde_los_demas():
    c = ClienteFalso({
        "https://t.cl/i.xml": INDICE,
        "https://t.cl/s2.xml": urlset("https://t.cl/p/2"),
    }, falla_en={"https://t.cl/s1.xml"})
    assert leer_sitemap("https://t.cl/i.xml", c) == ["https://t.cl/p/2"]


def test_indice_circular_no_cuelga():
    """Un indice que se apunta a si mismo debe cortarse por profundidad."""
    circular = b'<sitemapindex><sitemap><loc>https://t.cl/i.xml</loc></sitemap></sitemapindex>'
    c = ClienteFalso({"https://t.cl/i.xml": circular})
    assert leer_sitemap("https://t.cl/i.xml", c) == []
    assert len(c.pedidas) <= 3


def test_urlset_que_en_realidad_es_un_indice():
    """
    Paris publica su indice como <urlset> con enlaces a archivos .xml.
    Sin detectarlo, se devolvian rutas de sitemaps como si fueran
    fichas de producto y el barrido no encontraba nada.
    """
    c = ClienteFalso({
        "https://t.cl/i.xml": urlset("https://t.cl/s1.xml", "https://t.cl/s2.xml"),
        "https://t.cl/s1.xml": urlset("https://t.cl/p/1"),
        "https://t.cl/s2.xml": urlset("https://t.cl/p/2"),
    })
    assert leer_sitemap("https://t.cl/i.xml", c) == ["https://t.cl/p/1", "https://t.cl/p/2"]


def test_un_urlset_normal_no_se_confunde_con_un_indice():
    """Control: URLs de producto normales no acaban en .xml."""
    c = ClienteFalso({"https://t.cl/s.xml": urlset("https://t.cl/p/1", "https://t.cl/p/2.html")})
    assert leer_sitemap("https://t.cl/s.xml", c) == ["https://t.cl/p/1", "https://t.cl/p/2.html"]
