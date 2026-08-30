# ADR-006 · Cognito como IDaaS

**Fecha:** 30-08-2026
**Estado:** aceptada

## Contexto

El EP1 exige un **IDaaS** y el informe de 5 páginas tiene que **justificar cuál se eligió**.
Junto con el API Manager, es la decisión que más peso tiene en la nota: la rúbrica reparte
60% al frontend con el flujo OIDC completo y 40% al BFF validando el token.

La restricción real no era técnica sino de cuenta: el proyecto se despliega en una cuenta de
**AWS Academy Learner Lab**, que bloquea servicios sin avisar cuáles. Durante cuatro días la
decisión quedó abierta con la nota *«sin confirmar si AWS Academy deja levantar Cognito»*, y
el 100% del EP1 dependía de esa respuesta.

## Alternativas consideradas

- **Auth0.** Es el IDaaS de referencia y su capa gratuita alcanza de sobra para el MVP. Se
  descarta porque queda fuera de AWS: suma una consola más, una cuenta más y un proveedor más
  que explicar, y no aporta nada que Cognito no dé en este alcance.
- **Keycloak como un contenedor más del `docker-compose.yml`.** Era el plan B si Cognito
  estaba bloqueado. Funciona y es defendible, pero **deja de ser un IDaaS**: pasa a ser un
  servidor de identidad que nosotros administramos, parchamos y mantenemos arriba. Cambia el
  argumento del informe y agrega una pieza que puede caerse la semana de la entrega.
- **Entra ID + MSAL**, que es lo que enseña el material de la EA1 del ramo. Se descarta porque
  el stack propio del equipo ya fue aprobado por el profesor el 25-08, y federar Entra desde
  fuera de un tenant institucional agrega un trámite sin beneficio.

## Decisión

**Amazon Cognito**, confirmado con evidencia el 30-08-2026.

Se sondeó la cuenta del Learner Lab con `tools/verificar-aws-academy.sh` y **todo lo que el
EP1 necesita se puede crear**:

| Pieza | Resultado |
|---|---|
| User Pool | ✅ |
| Dominio de **Hosted UI** (sin él no hay pantalla de login) | ✅ |
| **Resource server** con el scope propio `ingesta`, el que usa el scraper | ✅ |
| **App client OIDC** con Authorization Code, sin secreto (para el SPA) | ✅ |
| Grupos de usuarios (`admin` / `usuario`) | ✅ |
| **JWT Authorizer de API Gateway apuntando al User Pool** | ✅ |
| Lanzar EC2 (comprobado con `--dry-run`) | ✅ |
| `LabRole` disponible | ✅ |

La evidencia cruda está en `docs/evidencia/sondeo-aws-20260830-013059.log`.

Las razones para elegirlo, ahora que se sabe que es viable:

1. **Está dentro de AWS.** El JWT Authorizer de API Gateway se integra con un User Pool
   escogiéndolo de una lista; con cualquier otro proveedor hay que configurar el issuer y el
   JWKS a mano. La integración IDaaS ↔ API Manager es literalmente lo que evalúa el EP1.
2. **Cubre OIDC completo**: Authorization Code + PKCE, Hosted UI, federación con Google,
   grupos y scopes personalizados. No falta nada de lo que el proyecto necesita.
3. **No cuesta crédito** en el volumen del MVP, y el crédito del Learner Lab es limitado.

## Consecuencias

- El `audience` de los tokens de Cognito tiene una particularidad que ya está documentada en
  [`ARQUITECTURA.md` §9](../ARQUITECTURA.md#9-el-detalle-del-audience-en-cognito): el
  `access_token` no trae `aud`, así que la validación en el BFF se hace contra `client_id`.
  Hay que tenerlo resuelto antes de la demo, porque es una pregunta típica de defensa.
- **GitHub no se puede federar** (es OAuth2, no OIDC). Google sí. Ya estaba decidido y esto
  lo confirma.
- Quedamos atados a AWS para la identidad. Es aceptable: el proyecto ya está atado a AWS por
  el despliegue completo, y migrar a otro IDaaS con OIDC estándar es cambiar el issuer.
- ⚠️ **El User Pool vive en la cuenta del Learner Lab.** Si el laboratorio se resetea, se
  pierde y hay que recrearlo. Por eso la configuración del pool tiene que quedar **escrita
  como comandos reproducibles**, no hecha a mano en la consola y olvidada.

## Nota de herramienta (no es de AWS)

`aws-cli v1` interpreta cualquier parámetro que empiece con `http://` o `https://` como una
URL de la que hay que **descargar** el valor, y falla con
`Could not connect to the endpoint URL`. Afecta a `--identifier` y a `--callback-urls`.
La solución que usa el script es pasar esos comandos con `--cli-input-json`, que se comporta
igual en la v1 y en la v2. Si aparece ese error, **no es que Academy lo bloquee**.
