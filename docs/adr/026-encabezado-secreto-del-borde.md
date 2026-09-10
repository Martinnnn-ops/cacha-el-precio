# ADR-026 · El borde se identifica con un encabezado secreto

- **Fecha:** 2026-09-10
- **Estado:** aceptado
- **Contexto de:** [ADR-019](019-api-gateway-como-api-manager.md) (API Gateway como API Manager)

## Contexto

Hasta hoy, que el tráfico pasara por el API Gateway era **una costumbre del
frontend, no una garantía**. La EC2 acepta peticiones directas, así que
cualquiera con la dirección podía saltarse el borde entero.

El 10-09 apareció además un caso que lo vuelve inevitable. El registro DNS de
`api.cacha-el-precio.com` admite **una sola IP** y lo administra una sola
persona. En cualquier cuenta que no sea la dueña del dominio, Caddy no puede
sacar certificado —Let's Encrypt comprueba el dominio conectándose a lo que diga
el DNS, y llega a otra máquina—, así que el borde tiene que entrar por
`http://<IP>:8080`, el bloque sin dominio del `Caddyfile`.

Ese puerto **no se puede limitar por grupo de seguridad**: AWS no publica un
rango de direcciones fijo para las integraciones de HTTP API. O queda abierto a
internet, o el borde no puede entrar.

## Decisión

El API Gateway **inyecta un encabezado `X-Borde-Secreto`** en cada una de sus
integraciones, mediante *parameter mapping*. El bloque `:8080` del `Caddyfile`
responde **403** a toda petición que no lo traiga con el valor correcto.

El secreto lo genera `tools/crear-api-gateway.sh`, queda en `borde.env` (fuera
del repositorio) y `tools/desplegar.sh` lo lleva al `.env` del servidor, desde
donde llega al contenedor de Caddy como variable de entorno.

Se **reutiliza** entre corridas: si se regenerara cada vez, dejaría de coincidir
con el que tiene la máquina y todo daría 403 hasta volver a desplegar.

## Por qué así

**Lo pone el API Gateway, no el cliente.** El navegador nunca ve el encabezado
ni lo puede falsear: se añade en el salto de AWS a la EC2, después de que el
JWT ya fue validado.

**Falla hacia el lado seguro.** El valor por defecto en el `Caddyfile` es
imposible de acertar (`sin-configurar`), así que una máquina sin la variable
responde 403 a todo en vez de quedarse abierta. Un error de configuración tiene
que cerrar, no abrir.

**Lo que gana y lo que no.** El token se valida igualmente más adentro, en el
BFF, así que la puerta abierta **no filtraba datos privados**. Lo que esto
arregla es que el borde deje de ser opcional: hoy el API Gateway es el único que
conoce el secreto, y por lo tanto el único camino posible. Eso convierte «todo
el tráfico pasa por el borde» en una propiedad del sistema y no en una
costumbre — que es justo lo que evalúa el EP2.

## Consecuencias

- El puerto 8080 sigue abierto en el grupo de seguridad, y **está bien**: quien
  lo toque sin el encabezado recibe 403 sin llegar al gateway.
- La comprobación de salud de `desplegar.sh` tiene que mandar el encabezado. Si
  no, informaría de un sistema caído cuando en realidad está bien cerrado.
- **El `Caddyfile` vive en el repositorio y la máquina lo clona desde GitHub**,
  así que este cierre solo surte efecto cuando el cambio llega a `development`.
  Verificado el 10-09: un `desplegar.sh` con el `Caddyfile` viejo reabre el
  puerto sin avisar.
- Si se pierde `borde.env`, se borra, se vuelve a correr `crear-api-gateway.sh`
  y después `desplegar.sh`. Se genera otro y se sincroniza solo.

## Alternativas descartadas

| | Por qué no |
|---|---|
| Limitar el 8080 por grupo de seguridad | AWS no publica un rango fijo para las integraciones de HTTP API |
| VPC Link con subred privada ([ADR-015](015-red-privada-con-vpc-link.md)) | Es el diseño correcto, pero el NAT cobra por hora y no cabe en el crédito del laboratorio |
| Cerrar el 8080 y entrar solo por el dominio | Solo funciona en la cuenta que administra el DNS. Deja a las otras dos sin poder montar el sistema |
| mTLS entre el borde y la EC2 | HTTP API no soporta certificados de cliente hacia la integración |
