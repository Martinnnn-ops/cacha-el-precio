# Evaluaciones: qué se pide y cómo lo cumplimos

> **Qué es.** Cada evaluación del ramo traducida a lo que hay en el sistema, más el vocabulario
> para el informe y el guion de la demo.
> **Para quién.** Los tres, para preparar entregas y la defensa. Los PDF del EP1 y el EP2 están en
> [`rubricas/`](rubricas/).
> **Última revisión:** 18-11-2026.

> 📍 **En 10 segundos:** los seis EP valen el 60% del ramo y la **EFT el 40%**. El EP1 y el EP2
> evaluaron casi solo **identidad, API Manager y frontend** (EP2: nota 7,0). El EP3 evaluó la
> **mensajería** (presentado, sin nota todavía). La **EFT es el 2-dic a las 17:00** e integra todo,
> más **Kinesis, DLQ, logs avanzados** y módulos extra, con **defensa individual**.

**Leyenda:** ✅ cumplido · 🟡 a medias · ⬜ falta · 🔎 por confirmar

---

## 1 · Cómo se compone la nota

| | % de los parciales | % de la nota final | Estado |
|---|---|---|---|
| EP1 · Encargo | 16% | 9,6% | ✅ entregado el 13-09 |
| EP2 · Presentación | 24% | 14,4% | ✅ **7,0** |
| EP3 · Encargo | 12% | 7,2% | ✅ entregado el 18-10, presentado el 21-10 · nota 🔎 |
| EP4 · EP5 · EP6 | 18% · 12% · 18% | 10,8% · 7,2% · 10,8% | 🔎 si la EFT los reemplaza o se suma |
| **Evaluación Final Transversal** | — | **40%** | ⬜ **2-dic, 17:00** |

**La lectura que guió el proyecto:** las rúbricas evalúan sobre todo las capas de plataforma
(identidad, API Manager, mensajería). El dominio de precios es lo que hace que la arquitectura
tenga razón de ser y no sea un CRUD de ejemplo, y es de lo que se habla cuando preguntan *«¿por qué
separaste esto en microservicios?»*. Por eso: **la plataforma se termina bien primero, y el dominio
crece alrededor**.

---

## 2 · EFT · 2-dic-2026, 17:00 (40%)

Producto final, que integra todo lo de los parciales. **La defensa es individual**: el profe tiene
preguntas preparadas para medir la participación de cada uno. La entrega vence un minuto antes de
presentar.

| Entregable | Estado |
|---|---|
| Informe ejecutivo | ⬜ |
| Informe técnico | ⬜ |
| Repos de GitHub | ✅ |
| Diagramas | 🟡 están en Mermaid en los docs; falta pasarlos al informe |
| **Video de 10 a 15 minutos** explicando la aplicación (sin explicar código) | ⬜ |

| Módulo | Cómo se cumple | Estado |
|---|---|---|
| Frontend, backend, IDaaS, API Manager | lo de los parciales | ✅ |
| Mensajería con DLQ | SQS + SNS del EP3 | ✅ |
| **Kinesis** | stream de precios capturados → precio habitual | ⬜ [PLAN-EFT §5](PLAN-EFT.md#5--kinesis-los-precios-capturados) |
| «DLC» 🔎 (creemos que es DLQ) | las de SQS + la del lector de Kinesis | 🟡 |
| **Logs avanzados** | correlationId + logs JSON | ✅ 28-11 · [PLAN-EFT §3](PLAN-EFT.md#3--correlationid-y-logs-en-json) |
| Trazabilidad e idempotencia (clase del 12-11) | lector de auditoría + «Seguir un barrido» | ✅ 28-11 · [PLAN-EFT §4](PLAN-EFT.md#4--la-cola-de-auditoría-que-nadie-leía) |
| Kafka | 🚫 descartado en la clase del 17-11 | — |
| Módulos extra | Terraform, CI/CD, armario digital, bandeja de correos, panel, Grafana | 🟡 |

El plan completo y el orden están en [PLAN-EFT](PLAN-EFT.md).

---

## 3 · EP3 · Mensajería asíncrona (encargo, 12%)

| Indicador | Peso | Con qué se cumplió |
|---|---|---|
| Canales y rutas **centralizados** | 12% | `infra/mensajeria/topologia.json`, el único lugar |
| Destinos y reglas por caso de uso | 13% | dos flujos; el filtro del tópico manda a cada cola lo suyo |
| Negocio separado de la mensajería | 10% | `IColaMensajes`, `IPublicadorEventos`, `IAdministradorMensajeria`: el negocio no toca el SDK |
| Consumidores por responsabilidad | 15% | `ConsumidorOfertas` y `ConsumidorNotificaciones` |
| **Confirmación, reintentos y errores** | **20%** | recuperable / definitivo / recuperable agotado, DLQ con diagnóstico |
| Interfaz de administración | 13% | el panel de mensajería |
| Encapsular crear/modificar/eliminar recursos | 10% | `ServicioAdministracion`. ⚠️ **Desde el 17-11 eso lo hace Terraform** y el panel solo mira ([ADR-032](adr/032-administracion-de-mensajeria.md)) |
| Validar las solicitudes de administración | 7% | `ValidadorSolicitudes`, con casos válidos e inválidos en `Mensajeria.http` |

> **Para la defensa:** el cambio del 17-11 se explica así: crear y borrar recursos desde un panel
> los dejaba fuera del código y sin revisión. Con Terraform el cambio se escribe, se ve el plan y
> recién entonces se aplica, y sirve igual en cualquier cuenta.

---

## 4 · EP1 y EP2 · Identidad y API Manager

**EP1 (encargo):** 60% el **frontend** con el flujo OIDC completo (tokens, guard, interceptor,
roles y scopes de los claims) y 40% el **BFF** validando emisor, audiencia, firma, vigencia y rol,
con los códigos de error correctos. Se confirmó en clase que, además, el sistema tenía que estar
desplegado y llevar un informe ejecutivo de 5 páginas que justificara el IDaaS y el API Manager.

**EP2 (presentación individual, 7,0):**

| Peso | Indicador |
|---|---|
| 20% | El API Manager valida el JWT: 200 / 401 / 403 |
| 15% | Authorization Code + PKCE: `code_verifier`, `code_challenge`, `state`, `nonce` |
| 15% | Evidencia de cada ruta, con y sin token |
| 13% | Rutas del API Manager hacia los microservicios |
| 10% | Tenant: usuarios de prueba, roles y políticas |
| 10% | App registrada: clientId, redirect URIs, roles y scopes |
| 10% | Registro e inicio de sesión desde el frontend |
| 7% | CORS en el API Manager |

### Requisitos del ramo y cómo los cumplimos

| Requisito | Con qué |
|---|---|
| **OpenID Connect** | Cognito user pool: `id_token`, `access_token`, `refresh_token` y JWKS |
| **Login con Google** | Google como proveedor federado en el pool |
| **OAuth2** | Authorization Code + PKCE para el front · `client_credentials` para el scraper |
| **API Manager** | API Gateway HTTP API: JWT Authorizer, stages, CORS explícito, 50 req/s, y **único camino de entrada** ([ADR-026](adr/026-encabezado-secreto-del-borde.md)) |
| **Integración** | SQS + SNS desde el EP3 ([ADR-029](adr/029-mensajeria-sqs-sns.md)) |
| **Validación en los servicios** | el BFF revalida firma, emisor, vigencia, `client_id` y grupos |

### Equivalencias de vocabulario

La pauta usa nombres de Azure. Son los mismos conceptos de OIDC: se nombra la equivalencia una vez
en el informe y listo.

| La rúbrica dice | En Cognito es | Qué se muestra |
|---|---|---|
| «tenant» | **user pool** | el pool con usuarios de prueba, grupos y política de contraseñas |
| «flujo de usuario» | **Hosted UI** con registro y verificación por correo | registro en vivo y después login |
| «MSAL» | **PKCE implementado sobre `fetch`** en Vue 3 | el `code_verifier`, el `code_challenge` SHA-256 y el `state` |
| «guards» y «MsalInterceptor» | guard de Vue Router + interceptor | ruta protegida que redirige; el `Authorization` en DevTools |
| «roles» | **grupos** → claim `cognito:groups` | `admin` contra `usuario`, y el 403 |
| «scopes» | **resource server** con scope propio | `ingesta`, solo para el scraper |
| «valida audience» | `client_id` (el access token no trae `aud`) | [ARQUITECTURA §6](ARQUITECTURA.md#6--seguridad-e-identidad) |

> 📌 **Vue, no React.** Algunos documentos viejos decían React. La pauta pide una librería OIDC, no
> una en concreto.

---

## 5 · Guion para la demo y el video

Sirve para la defensa de la EFT y para el video. Casi todo en vivo, pocos slides.

| Bloque | Qué se muestra |
|---|---|
| El problema | el caso del SERNAC: 65% de descuento que era 30%. Un slide |
| La arquitectura | el diagrama general y por qué está separado así |
| Identidad | login con Google, el token decodificado: claims, scopes y `cognito:groups` |
| El borde | la misma ruta con token (**200**), sin token (**401**), usuario común en ruta admin (**403**), y la EC2 directa sin pasar por el borde (**403**) |
| El producto | buscar una prenda, la ficha con las tiendas, el historial y el precio habitual |
| Mensajería | un cambio de precio que termina en correo; una DLQ con su diagnóstico en el panel |
| Kinesis y trazabilidad | el stream recibiendo precios; un pedido seguido por su `correlationId`; el tablero de Grafana |
| Infraestructura | `tf.sh subir` en una cuenta vacía: todo desde el código |
| Cierre | qué se haría después: emparejamiento por modelo, más tiendas, feed personalizado |

### Antes de presentar

- [ ] AWS prendido (`tf.sh subir`) y la lista del dueño del dominio ([OPERAR-EN-AWS §5](OPERAR-EN-AWS.md#5--lista-del-dueño-del-dominio))
- [ ] Usuarios de prueba y admin con Google listos
- [ ] Las llamadas 200 / 401 / 403 guardadas
- [ ] El catálogo con datos y el historial con varios días
- [ ] Ensayo cronometrado; **los tres tienen que poder responder sobre cualquier pieza**

## Ver también

- [PLAN-EFT](PLAN-EFT.md): lo que falta construir
- [EL-PROYECTO](EL-PROYECTO.md): la idea, para el inicio de la presentación
- [ARQUITECTURA](ARQUITECTURA.md): las respuestas a «¿por qué así?»
