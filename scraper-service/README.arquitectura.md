Arquitectura modular por capas (Layered Architecture) con separación de responsabilidades, aplicada a un microservicio de scraping.
	
	- Clean Architecture
	- Hexagonal Architecture / Ports & Adapters
	- Repository Pattern
	- Strategy/Adapter Pattern para los distintos sitios
	- Domain-Driven Design (DDD) a pequeña escala

----------------------------------------------------------------------------------------------------------------------------------

                 ┌─────────────────────────┐
                 │       SCHEDULER         │
                 │   / API / Job externo    │
                 └────────────┬────────────┘
                              │
                              ▼
                 ┌─────────────────────────┐
                 │        SERVICES         │
                 │   Orquestan procesos    │
                 └────────────┬────────────┘
                              │
             ┌────────────────┼────────────────┐
             ▼                ▼                ▼
      ┌─────────────┐  ┌─────────────┐  ┌─────────────┐
      │   CONVERSE  │  │    NIKE     │  │   ADIDAS    │
      │   Scraper   │  │   Scraper   │  │   Scraper   │
      └──────┬──────┘  └──────┬──────┘  └──────┬──────┘
             │                │                │
             └────────────────┼────────────────┘
                              ▼
                     ┌─────────────────┐
                     │     DOMAIN      │
                     │ Product / Offer │
                     │ ProductImage    │
                     └────────┬────────┘
                              │
              ┌───────────────┼────────────────┐
              ▼               ▼                ▼
        ┌──────────┐   ┌─────────────┐   ┌──────────┐
        │   HTTP   │   │    IMAGE    │   │ Storage  │
        │  Client  │   │  Processor  │   │   S3     │
        └──────────┘   └─────────────┘   └──────────┘
                                               │
                                               ▼
                                        ┌─────────────┐
                                        │ PostgreSQL  │
                                        └─────────────┘



---------------------------------------------------------------------------------------------------------------------------------.



src/scraper/config
	-> configuraciones
	-> Logging
src/scraper/domain
	-> Sirve como Modelo

src/scraper/image
	-> Descarga
	-> Convierte
	-> Optimiza la imagen del scrapeo
	-> Valida la imagen

src/scraper/infrastructure
	-> Conexion a la base de datos
		-> Persistencia de datos
	-> URLs del cliente servidor
	-> Almacenamiento en S3

src/scraper/scrapers
	-> Funcion del scrapeo y Inicializador
	-> Aqui ira las paginas que usaremos 
		-> Convers, Nike, Adidas, Falabella, Paris...

src/scraper/services
	-> Servicio de Scraping, producto y imagen. LO que es importante para eso

--------------------------------------------------------------------------------------------------



# Ejemplo de diagrama

                         INTERNET
                            │
             ┌──────────────┼──────────────┐
             ▼              ▼              ▼
           Nike           Adidas        Falabella
             │              │              │
             └──────────────┼──────────────┘
                            │
                            ▼
                 ┌────────────────────┐
                 │  SCRAPER SERVICE    │
                 │      Python        │
                 └─────────┬──────────┘
                           │
                 ┌─────────┴──────────┐
                 │                    │
                 ▼                    ▼
             PostgreSQL              S3
             productos              imágenes
                 │                    │
                 └─────────┬──────────┘
                           │
                           ▼
                  ┌─────────────────┐
                  │ Backend API     │
                  │    Micronaut    │
                  └────────┬────────┘
                           │
                           ▼
                    API Gateway
                           │
                           ▼
                     Vue Frontend

