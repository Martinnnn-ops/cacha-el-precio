# src/scraper/config/settings.py
"""
Configuracion del servicio, leida del entorno o del .env.

Se usa pydantic-settings en vez de os.getenv suelto por dos motivos:
valida los tipos al arrancar (un HTTP_TIMEOUT="abc" revienta al inicio
y no a mitad de un scrapeo), y deja la configuracion en un solo sitio
en lugar de repartida en llamadas a getenv por todo el codigo.

Las claves son las mismas que ya estan en .env.example.
"""

from __future__ import annotations

from functools import lru_cache

from pydantic import Field
from pydantic_settings import BaseSettings, SettingsConfigDict


class Settings(BaseSettings):
    model_config = SettingsConfigDict(
        env_file=".env",
        env_file_encoding="utf-8",
        extra="ignore",          # no reventar si el .env trae claves de mas
    )

    # --- aplicacion ---
    app_name: str = "cacha-el-precio-scrapper"
    app_env: str = "development"
    log_level: str = "INFO"

    # --- base de datos ---
    database_url: str | None = None

    # --- cliente HTTP ---
    http_timeout: float = 30.0
    http_max_retries: int = 3
    # Identificarse es lo correcto donde el sitio lo permite, y ademas
    # funciona: Falabella, Paris y Ripley sirven con este User-Agent.
    # Ojo: converse.cl devuelve 403 a cualquier UA que no sea de
    # navegador, y su robots.txt bloquea a todos los bots menos Google,
    # Bing, WhatsApp y Facebook. Ver la nota en el README.
    http_user_agent: str = "CachaElPrecioBot/1.0 (+contacto@ejemplo.cl)"

    # --- imagenes ---
    image_max_width: int = 800
    image_max_height: int = 800
    image_card_size: int = Field(default=400, ge=1)
    image_max_download_bytes: int = Field(default=8 * 1024 * 1024, ge=1024)
    image_max_pixels: int = Field(default=40_000_000, ge=1)
    image_webp_quality: int = Field(default=82, ge=1, le=100)

    # --- almacenamiento S3 ---
    aws_region: str = "us-east-1"
    aws_s3_bucket: str | None = None
    aws_s3_public_base_url: str | None = None
    aws_access_key_id: str | None = None
    aws_secret_access_key: str | None = None

    # --- scraper ---
    scraper_request_delay: float = Field(default=1.0, ge=0)
    scraper_max_concurrent_requests: int = Field(default=5, ge=1)

    # --- tiendas ---
    converse_base_url: str = "https://www.converse.cl"
    falabella_base_url: str = "https://www.falabella.com"

    # Sitemap de fichas de producto de cada tienda. Es de donde salen
    # las URLs a barrer, en vez de adivinar patrones o pasear el
    # catalogo pagina a pagina.
    falabella_sitemap: str = (
        "https://www.falabella.com/static/site/sitemaps/pdp/pdp_cl_FA_COM-index.xml"
    )
    # Paris publica 43 sitemaps mezclando marcas, categorias y productos.
    # Se apunta directo a uno de productos (50.000 URLs) en vez de al
    # indice, para no bajar los de categorias que no sirven aqui.
    paris_sitemap: str = "https://www.paris.cl/sitemaps/sitemap_products_es_cl_0.xml"
    # 1P = productos propios de Ripley. Hay tambien _3P (marketplace).
    ripley_sitemap: str = "https://simple.ripley.cl/sitemap_ripley_productos_1P.xml"
    hites_sitemap: str = "https://www.hites.com/sitemap_0-product.xml"
    # Sparta mezcla fichas y paginas normales en el mismo sitemap; las
    # que no son producto se reportan como "sin producto", no como fallo.
    # De los tres sub-sitemaps de Sparta, el -1-2 es 99% fichas; el
    # -1-1 es mitad categorias. Medido el 03/09/2026.
    sparta_sitemap: str = "https://sparta.cl/media/sparta/sitemap-1-2.xml"

    @property
    def es_produccion(self) -> bool:
        return self.app_env.lower() in {"production", "prod"}

    @property
    def s3_public_base_url(self) -> str | None:
        if self.aws_s3_public_base_url:
            return self.aws_s3_public_base_url.rstrip("/")
        if self.aws_s3_bucket:
            return f"https://{self.aws_s3_bucket}.s3.{self.aws_region}.amazonaws.com"
        return None


@lru_cache
def get_settings() -> Settings:
    """
    Instancia unica. El lru_cache evita releer el .env en cada llamada
    y garantiza que todo el proceso ve la misma configuracion.
    """
    return Settings()
