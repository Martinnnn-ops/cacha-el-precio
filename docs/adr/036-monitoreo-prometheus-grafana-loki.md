# ADR-036 · Monitoreo con Prometheus, Grafana y Loki (y la EC2 a t3.medium)

Fecha: 28-11-2026 · Estado: aceptado (EFT). Cambia la decisión del 17-11 de quedarse en `t3.small`.

## Contexto

La EFT pide «módulos extra» y logs avanzados, y la demo tiene que mostrar el tablero de Grafana y
un pedido seguido por su `correlationId` ([EVALUACIONES](../EVALUACIONES.md)). Con
[§3 de PLAN-EFT](../PLAN-EFT.md#3--correlationid-y-logs-en-json) los 4 servicios ya escriben logs
JSON con el `correlationId`, pero solo se podían leer con `docker compose logs | grep`, y no había
ningún número en el tiempo: ni cuántas peticiones, ni cuántos 401/403, ni cuánta RAM.

El plan del 17-11 estimaba ~0,4 GB para el monitoreo y por eso dejaba la EC2 en `t3.small` (2 GB).
Medido el 28-11 con todo arriba, la pila gasta **~670 MB** (Grafana sola ~375 MB) y los servicios
~0,8 GB: la `t3.small` quedaba sin margen para un barrido o un despliegue.

## Alternativas

- **Solo CloudWatch.** Kinesis y SQS ya publican ahí, pero los servicios .NET no, y CloudWatch Logs
  depende de permisos del Academy que no están confirmados. Además se paga por métrica y por GB.
- **Prometheus + Grafana sin logs.** Más liviano, pero el `correlationId` se seguiría buscando con
  `grep` en la consola, que es justo lo que la demo tiene que mostrar bien.
- **cAdvisor para la RAM por contenedor.** ~80 MB más; la RAM de cada proceso .NET ya la entrega
  prometheus-net y la del resto se mide con `docker stats`. Descartado.
- **Prometheus + Grafana + Loki + Alloy + node-exporter, en un perfil aparte.** Un solo tablero con
  métricas propias, la EC2, CloudWatch (Kinesis/SQS) y los logs.

## Decisión

La última, toda en `docker-compose.yml` con el **perfil `monitoreo`** (no se prende con el `up -d`
normal ni con el CD) y la configuración en [`infra/monitoreo/`](../../infra/monitoreo/README.md):

- **Métricas de los servicios .NET:** `prometheus-net.AspNetCore` en gateway, product-service y
  notification-service. Cuentan peticiones por ruta y código, latencia, llamadas del BFF hacia atrás,
  RAM y GC; más dos contadores propios: `cep_etapas_total{etapa}` (los pasos de `RegistroTrazas`,
  incluidos los duplicados descartados) y `cep_mensajes_total{cola,resultado}` (ok, reintento, dlq).
- **`/metrics` en un puerto aparte (`METRICAS_PUERTO`, 9464) que no se publica.** Caddy solo
  reenvía el 8080, así que las métricas nunca quedan en `api.cacha-el-precio.com/metrics` (404).
  Sin la variable, el servidor de métricas no se abre: las pruebas y el `dotnet run` siguen igual.
- **Logs:** Alloy lee los logs de los contenedores por el socket de Docker (solo lectura) y los
  manda a Loki con las etiquetas `servicio` y `nivel`. El `correlationId` **no** es etiqueta (hay uno
  por pedido y Loki se pone lento con etiquetas infinitas): se busca como texto.
- **Grafana solo en `127.0.0.1:3000`**, por túnel SSH. El security group no cambia. Fuentes y tablero
  se cargan solos desde el repo; la clave la genera `tools/desplegar.sh` una vez y la conserva.
- **Retención de 3 días** en Prometheus y Loki, y tope de RAM por contenedor.
- **La EC2 de producción pasa a `t3.medium`** (4 GB) con `tipo_instancia` en `martin.tfvars`. El
  valor por defecto sigue en `t3.small`: las cuentas de prueba no gastan más créditos.

## Consecuencias

- **Más créditos:** `t3.medium` cuesta el doble por hora que `t3.small` (~US$0,04/h contra ~0,02/h).
  Mitigado: la EC2 solo está prendida en las sesiones de trabajo y la demo.
- **Más disco:** las imágenes del monitoreo pesan ~3,6 GB (Grafana sola 2 GB). El disco es de 20 GB.
- **Alloy lee el socket de Docker.** En solo lectura, pero quien controle Alloy ve los contenedores.
  Mitigado: no publica puertos y solo vive en la red interna.
- **Sin la clave en el `.env`, Grafana en local entra con `admin/admin`.** Aceptable porque solo
  escucha en `127.0.0.1`; en la EC2 siempre hay clave generada.
- **Los contadores propios viven en memoria:** se reinician con el contenedor (Prometheus lo
  maneja con `rate`/`increase`). La fuente de verdad de la trazabilidad sigue siendo la tabla `Trazas`.
- **El scraper no expone métricas:** corre en barridos cada 3 h y sus números se ven en Kinesis
  (CloudWatch) y en las etapas de product-service. Si hiciera falta, es un instrumentador más.
- **Cambiar de tipo de instancia la detiene unos minutos** (*update in-place*, no *replace*):
  respaldo antes y leer el plan de `tf.sh subir`.
