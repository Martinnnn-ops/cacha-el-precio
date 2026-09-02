# gateway

El **BFF**. Es la única puerta de entrada de los servicios: recibe las llamadas del frontend ya filtradas por el API Gateway, valida el JWT y arma las respuestas que la pantalla necesita, juntando lo que haga falta de `catalog` y `price`.

## Estado

Módulo recién creado: **arranca y responde `/health`, y nada más**. La lógica se escribe
encima de esta base.

## Levantarlo

```bash
./mvnw -pl gateway mn:run
curl http://localhost:8080/health
```

Puerto por defecto **8080**, configurable con `GATEWAY_PORT` en el `.env`.

## Versión

`0.1.0`, independiente del resto ([ADR-014](../docs/adr/014-versionado-semantico-por-servicio.md)):
cada servicio avanza a su ritmo.
