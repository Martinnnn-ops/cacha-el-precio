# src/scraper/infrastructure/http/client.py
import httpx


class HttpClient:
    """
    Cliente HTTP utilizado por los scrapers para comunicarse
    con las tiendas.
    """

    DEFAULT_USER_AGENT = (
        "Mozilla/5.0 (X11; Linux x86_64) "
        "AppleWebKit/537.36 (KHTML, like Gecko) "
        "Chrome/140.0.0.0 Safari/537.36"
    )

    def __init__(
        self,
        timeout: float = 10.0,
        user_agent: str = DEFAULT_USER_AGENT,
    ):
        self._client = httpx.Client(
            timeout=timeout,
            follow_redirects=True,
            headers={
                "User-Agent": user_agent,
            },
        )

    def get(self, url: str) -> str:
        """
        Realiza una petición GET y devuelve el contenido HTML.
        """

        response = self._client.get(url)
        response.raise_for_status()

        return response.text

    def get_bytes(self, url: str) -> bytes:
        """
        Igual que get() pero sin decodificar.

        Hace falta para los sitemaps: vienen en .gz y decodificarlos
        como texto los corrompe antes de poder descomprimirlos.
        """
        response = self._client.get(url)
        response.raise_for_status()
        return response.content

    def close(self) -> None:
        """Cierra el cliente HTTP y sus conexiones."""
        self._client.close()

    def __enter__(self) -> HttpClient:
        return self

    def __exit__(self, exc_type, exc_value, traceback) -> None:
        self.close()