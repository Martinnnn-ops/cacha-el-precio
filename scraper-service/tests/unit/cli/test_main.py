# tests/unit/cli/test_main.py
"""
Comportamiento del CLI cuando la base no esta disponible.

Importa en un job por cron: si la base se cae, el log debe decir que
pasa y que hacer, no escupir una traza de psycopg de quince lineas. Y
los comandos que no tocan la base tienen que seguir funcionando.
"""

import pytest

from scraper.config.settings import get_settings
from scraper.main import main


@pytest.fixture
def sin_base(monkeypatch):
    """Apunta a un puerto donde no hay nadie escuchando."""
    monkeypatch.setenv("DATABASE_URL", "postgresql://x:x@127.0.0.1:59999/nada")
    get_settings.cache_clear()
    yield
    get_settings.cache_clear()


def test_tiendas_funciona_sin_base(sin_base, capsys):
    assert main(["tiendas"]) == 0
    salida = capsys.readouterr().out
    for t in ("falabella", "paris", "ripley", "hites", "sparta", "converse"):
        assert t in salida


def test_tiendas_dice_cual_tiene_sitemap(sin_base, capsys):
    main(["tiendas"])
    lineas = {
        linea.split("\t")[0]: linea
        for linea in capsys.readouterr().out.splitlines()
        if "\t" in linea
    }
    assert "fuente automatica disponible" in lineas["falabella"]
    assert "URLs autorizadas" in lineas["converse"]


def test_barrer_sin_base_sale_con_codigo_3(sin_base, caplog):
    """
    Codigo 3 y no una excepcion: asi el timer de systemd marca el fallo
    y el log queda legible.
    """
    with pytest.raises(SystemExit) as e:
        main(["barrer", "paris", "--url", "https://ejemplo.cl/x"])
    assert e.value.code == 3
    mensajes = " ".join(r.getMessage() for r in caplog.records)
    assert "no se pudo conectar" in mensajes
    assert "docker compose up -d db" in mensajes  # dice como arreglarlo


def test_tienda_sin_sitemap_no_revienta(sin_base):
    """Converse no tiene sitemap: debe avisar, no lanzar excepcion."""
    assert main(["descubrir", "converse"]) == 2


def test_falabella_descubre_listados_en_lugar_de_fichas(sin_base, capsys):
    assert main(["descubrir", "falabella", "--limite", "2"]) == 0
    urls = capsys.readouterr().out.splitlines()
    assert len(urls) == 2
    assert all("/category/" in url for url in urls)
    assert urls[1].endswith("?page=2")
