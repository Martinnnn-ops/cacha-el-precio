# scraper-service

Trae los datos de las tiendas. Corre por horario y no por request, y es el único servicio que sale a internet hacia sitios ajenos; por eso vive aparte.

## Estado

Módulo recién creado: **arranca y responde `/health`, y nada más**. La lógica se escribe
encima de esta base.

## Levantarlo

```bash
./mvnw -pl scraper-service mn:run
curl http://localhost:8083/health
```

Puerto por defecto **8083**, configurable con `SCRAPER_PORT` en el `.env`.

## Versión

`0.1.0`, independiente del resto ([ADR-014](../docs/adr/014-versionado-semantico-por-servicio.md)):
cada servicio avanza a su ritmo.
