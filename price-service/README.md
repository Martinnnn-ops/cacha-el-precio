# price-service

Dueño del esquema `price`. Guarda el estado actual de cada oferta y la serie histórica de precios, y calcula el **descuento real** contra el mínimo observado — no contra el precio de referencia que declara la tienda.

## Estado

Módulo recién creado: **arranca y responde `/health`, y nada más**. La lógica se escribe
encima de esta base.

## Levantarlo

```bash
./mvnw -pl price-service mn:run
curl http://localhost:8082/health
```

Puerto por defecto **8082**, configurable con `PRICE_PORT` en el `.env`.

## Versión

`0.1.0`, independiente del resto ([ADR-014](../docs/adr/014-versionado-semantico-por-servicio.md)):
cada servicio avanza a su ritmo.
