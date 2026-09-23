#!/usr/bin/env python3
"""Pruebas HTTP contra servicios LOCALES y PostgreSQL desechable; no usar con producción.

Product Service: 127.0.0.1:5581, gateway: 127.0.0.1:5580.
"""
import json
from hashlib import sha256
from concurrent.futures import ThreadPoolExecutor
from urllib.error import HTTPError
from urllib.request import Request, urlopen
from uuid import uuid4


def request(path, method="GET", data=None, port=5581):
    body = json.dumps(data).encode() if data is not None else None
    req = Request(f"http://127.0.0.1:{port}{path}", data=body, method=method,
                  headers={"Version": "1.0", "Content-Type": "application/json"})
    try:
        response = urlopen(req, timeout=15)
    except HTTPError as error:
        response = error
    with response:
        raw = response.read()
        return response.status, json.loads(raw) if raw else None


def main():
    marker = str(uuid4())
    base = dict(externalId=marker, store="pruebas", name="Polera adulto " + marker,
                brand="GENÉRICO", category="Poleras", gender="Unisex", bodyArea="Torso",
                layer="Base", price=9990, sizes=["M"], url="https://example.com/polera")
    status, product = request("/api/products", "POST", base)
    assert status == 200, (status, product)
    product_id = product["id"]
    assert product["brand"] == "Genéricas"
    assert product["canonicalKey"] == "generica:" + sha256(f"pruebas\0{marker}".encode()).hexdigest()
    owner = "/internal/personal/prueba-" + marker
    other = owner + "-otra"
    outfit_id = str(uuid4())
    try:
        status, same = request("/api/products", "POST", {**base, "brand": "Sin marca"})
        assert status == 200 and same["id"] == product_id, "El cambio de marca duplicó el SKU"
        for name in ["Gorro bebé", "Polera niño", "Zapatilla kids"]:
            assert request("/api/products", "POST", {**base, "name": name})[0] == 400
        assert request(owner + f"/deseados/{product_id}", "PUT")[0] == 204
        with ThreadPoolExecutor(max_workers=4) as pool:
            assert all(s == 204 for s in pool.map(
                lambda _: request(owner + f"/deseados/{product_id}", "PUT")[0], range(8)))
        assert request(owner)[1]["productos"] == [product_id]
        assert request(other)[1]["productos"] == [], "Datos de otra cuenta expuestos"
        assert request(other + f"/deseados/{product_id}", "DELETE")[0] == 204
        assert request(owner)[1]["productos"] == [product_id], "Borrado cruzado entre cuentas"
        datos = dict(nombre="Para la U", seleccion={"torso-base": product_id}, tallaRopa="M", tallaCalzado="")
        assert request(owner + f"/outfits/{outfit_id}", "PUT", datos)[0] == 204
        saved = request(owner)[1]["outfits"][0]
        assert saved["datos"]["seleccion"]["torso-base"] == product_id
        assert saved["datos"]["nombre"] == "Para la U"
        assert request(owner + f"/outfits/{outfit_id}", "PUT", {**datos, "seleccion": {"invalida": 1}})[0] == 400
        for path, method in [("/mi-cuenta", "GET"), (f"/mi-cuenta/deseados/{product_id}", "PUT"),
                             (f"/mi-cuenta/outfits/{outfit_id}", "DELETE"), ("/seguimiento", "GET")]:
            assert request(path, method, port=5580)[0] == 401, "Ruta privada sin JWT"
        assert request(owner, port=5580)[0] == 404, "El gateway publicó una ruta interna"
        print("OK: marcas, edad, SKU estable, concurrencia, propiedad de datos, outfits y rutas protegidas")
    finally:
        request(owner + f"/outfits/{outfit_id}", "DELETE")
        request(owner + f"/deseados/{product_id}", "DELETE")
        request(f"/api/products/{product_id}", "DELETE")


if __name__ == "__main__":
    main()
