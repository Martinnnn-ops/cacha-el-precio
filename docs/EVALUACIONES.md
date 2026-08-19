# Evaluaciones: qué se pide y cómo lo cumplimos

> Las dos rúbricas del ramo traducidas a acciones concretas. Los PDF originales están en
> [rubricas/](rubricas/).
> El checklist operativo de tareas está en [TAREAS.md](TAREAS.md).
>
> Documento vivo · Última revisión: 19-08-2026

---

## Índice

1. [Cómo se califica, y cómo usamos esa información](#1-cómo-se-califica-y-cómo-usamos-esa-información)
2. [Requisitos del ramo y cómo los cumplimos](#2-requisitos-del-ramo-y-cómo-los-cumplimos)
3. [Guion de la demo de EP2](#3-guion-de-la-demo-de-ep2)

---

## 1. Cómo se califica, y cómo usamos esa información


Leímos las dos rúbricas indicador por indicador. Sirven para **priorizar el orden de trabajo**,
no para dictar el proyecto: la docente dio libertad de nube, de lenguaje y de idea, y lo que se
pide es un sistema **funcional, escalable y defendible**. Con eso en mente, el dato útil es este:

> Ambas rúbricas evalúan casi por completo **la capa de identidad, el API Manager y el frontend**.
> El scraping, el matching, RabbitMQ y el historial de precios no tienen indicador propio.

La lectura correcta **no** es "el dominio no importa". El dominio de precios es lo que hace que
esta sea una arquitectura con razón de ser y no un CRUD de ejemplo, y es de lo que se habla
cuando preguntan *"¿por qué separaste esto en microservicios?"*. La lectura correcta es:

> **La capa de seguridad se construye primero y se termina bien; el dominio crece alrededor
> y no se deja para el final.**

### Mapa de puntaje

**EP1 · Encargo (16% de la nota final) — código por GitHub, 13-sep**

| Peso | Indicador | Dueño |
|---|---|---|
| **60%** | Frontend: flujo de usuario OIDC completo, obtiene los tokens necesarios, guard de ruta, interceptor que adjunta el token, **lee roles y scopes desde los claims** | Integrante 1 |
| **40%** | BFF: valida **issuer** y **audience**, verifica **firma** y **vigencia**, **autorización por rol**, responde **códigos de error adecuados** | Integrante 2 |

**EP2 · Presentación (24% de la nota final) — nota individual, 5–10 minutos**

| Peso | Indicador | Presenta |
|---|---|---|
| 20% | API Manager valida JWT en las rutas: issuer, audience, pruebas con **200 / 401 / 403** | Integrante 2 |
| 15% | **Authorization Code + PKCE**: code_verifier, code_challenge, `state` y `nonce` | Integrante 1 |
| 15% | **Evidencia** de cada ruta: llamadas **con y sin token**, JSON esperado | Integrante 2 |
| 13% | Rutas del API Manager hacia los microservicios | Integrante 3 |
| 10% | **Tenant** en el IDaaS: usuarios de prueba, **roles**, políticas | Integrante 1 |
| 10% | App registrada en el tenant: clientId, redirect URIs, roles y scopes | Integrante 1 |
| 10% | Flujo de usuario: **registro** e inicio de sesión desde el frontend | Integrante 1 |
| 7% | **CORS** en el API Manager | Integrante 3 |

### Cuatro consecuencias prácticas

1. **EP2 es nota individual.** Dice: *"cada estudiante fundamenta la propuesta desarrollada,
   justificando el diseño y tecnologías utilizadas"*. No es que haya que memorizar la pauta —
   es que los tres tenemos que **entender de verdad** el flujo de autenticación, porque la
   pregunta le puede tocar a cualquiera. Por eso la Semana 0 los tres levantan su propio login.

2. **Hay que mostrar el sistema andando en la nube.** Aparece en la mayoría de los indicadores
   de EP2. El despliegue no es el último paso: es requisito para poder presentar. Va en la Semana 2.

3. **Cuatro cosas que valen puntos y no estaban en el plan**, ya incorporadas: **CORS**,
   **roles** (no solo scopes, porque sin roles no hay 403), `state` y `nonce`, y el **registro**
   de usuarios además del login.

4. **La presentación dura 5–10 minutos.** Pocos slides, casi todo demo en vivo.

### Sobre la libertad que nos dieron

La pauta está redactada sobre un caso de ejemplo (Pedidos360, Angular, Azure AD, Spring Boot),
pero la docente aclaró que **la idea, el lenguaje y la nube son libres mientras se justifiquen**.
Nosotros vamos con **Micronaut + AWS + Cognito**, y esa justificación está escrita en
[ARQUITECTURA.md](ARQUITECTURA.md).

Lo único que conviene tener presente es que la pauta usa vocabulario de Azure ("tenant",
"user flow", "MSAL"). No es un problema — son conceptos estándar de OIDC con otro nombre
comercial. Basta nombrar la equivalencia una vez en el informe y en la presentación
(tabla en §2) y seguir adelante.

---

## 2. Requisitos del ramo y cómo los cumplimos

| Requisito | Con qué se cumple |
|---|---|
| **OpenID Connect** | **Cognito User Pool**: emite `id_token`, `access_token` y `refresh_token`, publica su JWKS |
| **Login con Google** | Google como **IdP federado** en el User Pool (adicional al registro propio) |
| **OAuth2** | **Authorization Code + PKCE** para el frontend · **Client Credentials** para el scraper |
| **API Manager** | **API Gateway HTTP API** con JWT Authorizer: stages dev/prod, CORS, throttling, OpenAPI |
| **Mensajería** | **RabbitMQ** entre ingesta y procesamiento (ver [ARQUITECTURA.md §6](ARQUITECTURA.md#6-por-qué-mensajería-y-no-llamadas-directas)) |
| **Validación en los servicios** | `micronaut-security-jwt` valida firma, issuer, audience, vigencia y roles contra el JWKS |

### Equivalencias de vocabulario (una tabla en el informe y listo)

La pauta usa nombres comerciales de Azure. Son los mismos conceptos de OIDC. Se menciona una vez
y no se vuelve a tocar:

| La rúbrica dice | En Cognito es | Qué mostramos en la presentación |
|---|---|---|
| "crea un **tenant**" | **User Pool** — la unidad de aislamiento de identidad de Cognito | El User Pool con usuarios de prueba, grupos y política de contraseñas |
| "crea el **flujo de usuario**" | **Hosted UI** con sign-up habilitado + verificación por email | Registro de un usuario nuevo en vivo, y después login |
| "la librería **MSAL**" | **`oidc-client-ts`** vía `react-oidc-context` | Es la librería certificada OIDC equivalente; hace PKCE, `state` y `nonce` |
| "**guards** y **MsalInterceptor**" | Route guard de React Router + interceptor de `fetch`/Axios | Ruta protegida que redirige si no hay sesión; el header `Authorization` en DevTools |
| "**roles** y políticas" | **Grupos de Cognito** → claim `cognito:groups` | Usuario en grupo `admin` vs `usuario`, y el 403 que resulta |
| "**scopes**" | **Resource Server** con custom scopes | `precios:leer`, `seguimiento:escribir`, `ingesta:escribir` |
| "valida **audience**" | Ver la nota de abajo | La configuración del authorizer + el código de validación en el BFF |

El detalle técnico del `audience` en Cognito está en
[ARQUITECTURA.md §9](ARQUITECTURA.md#9-el-detalle-del-audience-en-cognito).

---

## 3. Guion de la demo de EP2

Son **5 a 10 minutos**. Casi todo demo en vivo, pocos slides. Orden sugerido:

| Min | Qué se muestra |
|---|---|
| 0–1 | **El problema**: el caso del SERNAC, 65% de descuento que era 30%. Un slide, nada más |
| 1–2 | Diagrama de arquitectura y por qué está separado así |
| 2–4 | **Registro de un usuario en vivo** → login → DevTools mostrando `code_challenge`, `state` y `nonce` |
| 4–5 | El token decodificado en pantalla: claims, scopes y `cognito:groups` |
| 5–7 | **Postman**: la misma ruta con token (**200**), sin token (**401**), y con usuario común en ruta admin (**403**) |
| 7–8 | Consola de AWS: API Gateway con las rutas y CORS, tareas de Fargate corriendo |
| 8–9 | El producto: buscar un modelo, ver el descuento real y el gráfico de historial |
| 9–10 | Cierre: *"agregar alertas es suscribir un servicio nuevo al evento `precio.cambiado`, sin tocar el código existente"* |

### Antes de presentar

- [ ] Grabar un **video de respaldo** de la demo completa el día anterior
- [ ] Tener los usuarios de prueba creados y las contraseñas a mano
- [ ] Postman con la colección lista y las tres llamadas guardadas
- [ ] Verificar que el ambiente de AWS está prendido (no apagado por ahorro de crédito)
- [ ] Ensayar cronometrado: si pasa de 10 minutos, se corta contenido

> Cada indicador tiene un presentador asignado en [TAREAS.md](TAREAS.md), pero **los tres deben
> poder responder cualquier pregunta**. En EP2 la nota es individual.
