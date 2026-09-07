# src/scraper/scrapers/base/base_scraper.py

from abc import ABC, abstractmethod

from scraper.domain.product import Product


class BaseScraper(ABC):
    """
    Contrato base para todos los scrapers.
    """

    @abstractmethod
    def scrape(self, url: str) -> list[Product]:
        """
        Extrae productos desde una URL.

        Args:
            url: URL de la página que se desea procesar.

        Returns:
            Lista de productos extraídos.
        """
        ...