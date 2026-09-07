# tests/conftest.py
"""
Guarda la DATABASE_URL real antes de que nadie la toque.

pytest ejecuta el conftest de la raiz de tests/ antes que los de las
subcarpetas, asi que aqui la URL todavia es la de verdad. Se copia a
DATABASE_URL_REAL para que las pruebas de integracion la encuentren
aunque tests/unit/conftest.py haya vaciado DATABASE_URL despues.

Sin esto, el aislamiento de las unitarias hacia que las de integracion
se SALTARAN en silencio al correr la suite entera, y parecerian estar
pasando cuando en realidad no se ejecutaban. Antes funcionaba solo por
el orden alfabetico (integration < unit), que es demasiado fragil para
depender de el.
"""

import os

os.environ.setdefault("DATABASE_URL_REAL", os.getenv("DATABASE_URL", ""))
