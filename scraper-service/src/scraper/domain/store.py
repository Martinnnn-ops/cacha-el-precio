# src/scraper/domain/store.py
from pydantic import BaseModel


class Store(BaseModel):
    id: str
    name: str
    slug: str
    base_url: str


# · Asi se define la tienda de Converse
# id: "converse"
# name: "Converse"
# slug: "converse"
# base_url: "https://www.converse.cl"