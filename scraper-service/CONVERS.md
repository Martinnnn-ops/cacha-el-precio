                    CONVERSE
                       │
                       ▼
                convers/scraper.py
                       │
                       ▼
                  HTML / JSON
                       │
                       ▼
                convers/parser.py
                       │
                       ▼
              Producto normalizado
                       │
             ┌─────────┴─────────┐
             ▼                   ▼
        Información          image_url
        del producto             │
             │                   ▼
             │              ImageService
             │                   │
             │              ┌────┴────┐
             │              ▼         ▼
             │          WebP       Storage
             │                       │
             └───────────┬───────────┘
                         ▼
                    Repository
                         │
                         ▼
                    PostgreSQL
