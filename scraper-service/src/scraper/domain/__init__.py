# src/scraper/domain/__init__.py
"""Modelos del dominio, reexportados para poder hacer `from scraper.domain import Product`."""

from scraper.domain.offer import Offer
from scraper.domain.product import Product
from scraper.domain.store import Store

# Sin __all__, ruff marca estos imports como "no usados" (F401): no puede
# saber que estan aqui para reexportarse.
__all__ = ["Offer", "Product", "Store"]
