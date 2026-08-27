# Requisitos del sistema

> Roles, historias de usuario y requisitos funcionales y no funcionales.
> Para la **idea y el alcance** ver [`PLAN.md`](PLAN.md).
> Para el **cómo está construido** ver [`ARQUITECTURA.md`](ARQUITECTURA.md).
> Para **qué pide el ramo** ver [`EVALUACIONES.md`](EVALUACIONES.md).
>
> Documento vivo · Última revisión: 26-08-2026

---

## Índice

1. [Introducción](#1-introducción)
2. [Objetivo del sistema](#2-objetivo-del-sistema)
3. [Roles de usuario](#3-roles-de-usuario)
4. [Historias de usuario](#4-historias-de-usuario)
5. [Requisitos funcionales](#5-requisitos-funcionales)
6. [Requisitos no funcionales](#6-requisitos-no-funcionales)
7. [Trazabilidad con la rúbrica del EP1](#7-trazabilidad-con-la-rúbrica-del-ep1)

---

## 1. Introducción

Este documento define **qué tiene que hacer el sistema**, en el lenguaje de quien lo va a usar.
No describe tecnologías: esas están justificadas en [`ARQUITECTURA.md`](ARQUITECTURA.md).

El alcance está acotado al **MVP**, que es lo que se entrega en el EP1 y se presenta en el EP2.
Las historias marcadas como *post-MVP* están escritas para que se vea hacia dónde va el sistema,
pero no se construyen antes del code freeze del 10 de septiembre.

---

## 2. Objetivo del sistema

**Cacha el Precio permite saber si el descuento que anuncia una tienda es real.**

Cuando una tienda publica «65% de descuento», ese porcentaje se calcula contra un *precio de
referencia* que define la propia tienda. El SERNAC ya persiguió a Falabella y a Paris por inflar
esa referencia antes del Cyber: en un caso, el precio de referencia de $198.900 rondaba los
$100.000 días antes, y el descuento real era 30%, no 65%.

El sistema resuelve eso de tres maneras:

1. **Compara el mismo modelo entre varias tiendas** usando el *style code* de fábrica, que es el
   mismo identificador en todas partes, así que la comparación es exacta y no aproximada.
2. **Guarda el historial de precios** desde que el sistema arrancó, con varias capturas diarias.
3. **Calcula el descuento contra el mínimo observado por nosotros**, no contra el precio de
   referencia que declara la tienda.

El sistema **no vende nada**. Informa y deriva a la tienda.

### Qué NO es

- No es una tienda: no hay carrito, ni pago, ni despacho.
- No es un cotizador: no se arman pedidos ni se solicita precio a un vendedor.
- No es un catálogo propio: el catálogo son los productos que las tiendas publican.

---

## 3. Roles de usuario

| Rol | Descripción | Cómo se determina |
|---|---|---|
| **Visitante** | Usuario no autenticado. Puede buscar modelos, ver la comparación entre tiendas, el descuento real y el historial. No puede seguir productos ni recibir avisos. | Sin token |
| **Usuario registrado** | Se autenticó con correo o con Google. Puede seguir modelos en una talla, ver su lista y recibir avisos por correo cuando el precio baja de verdad. | Token válido, grupo `usuario` |
| **Administrador** | Integrante del equipo. Resuelve los emparejamientos dudosos entre tiendas y vigila el estado de las capturas. | Token válido, grupo `admin` |

> **Por qué existe el rol de administrador, y por qué no es decorativo.** El emparejamiento
> automático entre tiendas no siempre es concluyente: cuando la similitud queda en zona gris, la
> pareja se guarda como **candidato pendiente** en vez de darse por buena. Alguien tiene que
> aceptarla o rechazarla, y esa persona es el administrador. La tabla de candidatos con estado
> `pendiente / aceptado / rechazado` ya está en el modelo de datos de [`PLAN.md §4`](PLAN.md#4-modelo-de-datos).
>
> Esto importa para la nota: el **403** que pide la rúbrica no es un caso inventado para la demo.
> Es un usuario común intentando entrar al panel de candidatos.

### La frontera entre los tres roles, en una línea

> **Los precios son públicos. Tu lista es tuya. Los emparejamientos los decide el equipo.**

---

## 4. Historias de usuario

### 4.1 Visitante

**HU-00 · Explorar sin registrarse**

> Como visitante
> quiero ver la comparación de precios sin crear una cuenta
> para decidir si me sirve antes de registrarme.

Criterios de aceptación:

1. Se accede al buscador y a la ficha de producto desde la portada, sin iniciar sesión.
2. Se ve el precio de cada tienda, el descuento real y el gráfico de historial.
3. Se puede cambiar el tema de la página entre claro y oscuro.
4. Se puede filtrar por tienda.
5. Al intentar seguir un producto, el sistema pide iniciar sesión y **vuelve a la misma ficha**
   después del login.

### 4.2 Autenticación y cuenta

**HU-01 · Registro e inicio de sesión**

> Como visitante
> quiero registrarme o entrar con mi cuenta de Google
> para no tener que crear ni recordar otra contraseña.

Criterios de aceptación:

1. Se ofrecen dos caminos: **registro con correo y contraseña**, y **entrar con Google**.
2. El registro con correo exige **verificación por correo** antes del primer ingreso.
3. Si el correo ya existe con otro método, el sistema lo informa en vez de crear una cuenta
   duplicada.
4. El flujo usa **Authorization Code + PKCE**, con `state` y `nonce`.
5. Al volver, la aplicación guarda la sesión y muestra el nombre del usuario.

> ⚠️ **GitHub no entra como proveedor.** La versión anterior de este documento ofrecía
> «iniciar sesión con GitHub». GitHub implementa OAuth2 pero **no es un proveedor OIDC**: no
> publica el `id_token` ni el documento de descubrimiento que Cognito necesita para federar.
> Agregarlo obligaría a montar un intermediario propio, que es trabajo que no puntúa y una
> superficie de seguridad extra. **Google sí es proveedor federado nativo**, y con eso se cumple
> el requisito de login social.

**HU-02 · Cerrar sesión**

> Como usuario autenticado
> quiero cerrar mi sesión
> para proteger mi cuenta en un equipo compartido.

Criterios de aceptación:

1. El cierre invalida la sesión local **y** cierra la sesión en el proveedor de identidad.
2. Después de cerrar, una ruta protegida redirige al login en vez de mostrar datos.

**HU-03 · Ver y editar mi perfil**

> Como usuario registrado
> quiero ver mis datos y mis preferencias
> para que los avisos me lleguen bien.

Criterios de aceptación:

1. Se muestran nombre y correo tal como vienen del proveedor de identidad.
2. Se pueden editar las preferencias propias del producto: **talla habitual** y **tiendas de
   interés**.
3. Se puede activar o desactivar la recepción de avisos por correo.

### 4.3 Buscar y comparar

**HU-04 · Buscar un modelo**

> Como usuario
> quiero buscar por nombre o por style code
> para llegar al modelo que me interesa.

Criterios de aceptación:

1. La búsqueda por **style code** (por ejemplo `HV9774`) devuelve el modelo exacto.
2. La búsqueda por texto tolera diferencias de escritura entre tiendas.
3. Si no hay resultados, se explica qué se puede intentar en vez de mostrar una pantalla vacía.

**HU-05 · Filtrar resultados**

> Como usuario
> quiero filtrar por marca, talla, tienda y rango de precio
> para no revisar todo el listado.

**HU-06 · Ver la ficha de un modelo**

> Como usuario
> quiero ver el mismo modelo en todas las tiendas, lado a lado
> para saber dónde conviene comprarlo.

Criterios de aceptación:

1. Se listan las tiendas que tienen el modelo, con su precio actual.
2. Se muestra el **descuento real** junto al descuento que anuncia la tienda, cuando difieren.
3. Se indica la **disponibilidad por talla** en cada tienda.
4. Cada tienda tiene un enlace que lleva al producto en su sitio.
5. Se muestra **cuándo fue la última captura**, para que el dato tenga fecha.

**HU-07 · Ver el historial de precios**

> Como usuario
> quiero ver cómo se movió el precio en el tiempo
> para saber si esta oferta es realmente buena.

Criterios de aceptación:

1. Un gráfico muestra la serie de precios por tienda desde que hay datos.
2. Se marcan el **mínimo observado** y el **precio actual**.
3. Si hay pocos días de historial, el sistema lo dice en vez de dar a entender que es una serie
   larga.

### 4.4 Seguimiento y avisos

**HU-08 · Seguir un modelo** *(ruta protegida)*

> Como usuario registrado
> quiero guardar un modelo en una talla
> para que me avisen si baja de precio.

Criterios de aceptación:

1. Requiere sesión iniciada; sin token la petición se rechaza con **401**.
2. Se guarda modelo, talla y, opcionalmente, un **precio objetivo**.
3. Un usuario **solo puede ver y modificar sus propios seguimientos** — intentar tocar los de
   otro responde **403**.

**HU-09 · Ver mi lista de seguimiento**

> Como usuario registrado
> quiero ver todo lo que sigo, con su precio de hoy
> para revisarlo de una vez.

**HU-10 · Recibir un aviso por correo**

> Como usuario registrado
> quiero que me avisen cuando el precio baje de verdad
> para no tener que revisar todos los días.

Criterios de aceptación:

1. El aviso se dispara cuando el precio **baja del mínimo observado** o del precio objetivo que
   fijó el usuario, no cuando la tienda anuncia una oferta.
2. El correo dice el precio anterior, el nuevo, la tienda y la fecha.
3. No se manda más de un aviso por modelo y talla al día.
4. El usuario puede desactivar los avisos desde su perfil.

**HU-11 · Dejar de seguir**

> Como usuario registrado
> quiero quitar algo de mi lista
> para dejar de recibir avisos que ya no me sirven.

**HU-12 · Presupuesto** *(post-MVP)*

> Como usuario registrado
> quiero indicar cuánto tengo disponible
> para que el sistema me muestre qué de mi lista me alcanza hoy.

### 4.5 Administración

**HU-13 · Resolver emparejamientos dudosos** *(ruta protegida, solo `admin`)*

> Como administrador
> quiero revisar las parejas de productos que el sistema no pudo confirmar
> para que la comparación no muestre dos productos distintos como si fueran el mismo.

Criterios de aceptación:

1. Se listan los candidatos en estado `pendiente`, con ambos productos y su grado de similitud.
2. Se puede **aceptar** o **rechazar** cada uno, y la decisión queda registrada.
3. Un usuario del grupo `usuario` que entre a esta ruta recibe **403**.

**HU-14 · Vigilar el estado de las capturas** *(solo `admin`)*

> Como administrador
> quiero ver cuándo corrió cada tienda por última vez y qué falló
> para darme cuenta de que un scraper se rompió antes de que se note en la web.

Criterios de aceptación:

1. Se ve la última captura exitosa por tienda y cuántos productos trajo.
2. Se ven los mensajes que quedaron en la cola de errores.

**HU-15 · Activar o desactivar una tienda** *(solo `admin`)*

> Como administrador
> quiero desactivar temporalmente una tienda
> para que un scraper roto no ensucie los datos mientras se arregla.

---

## 5. Requisitos funcionales

| ID | Requisito | HU | ¿MVP? |
|---|---|---|---|
| **RF-01** | Capturar precio, stock por talla y style code de las tiendas activas, varias veces al día | — | Sí |
| **RF-02** | Guardar cada captura cruda antes de procesarla, para poder reprocesar | — | Sí |
| **RF-03** | Emparejar productos entre tiendas por style code y, si no lo hay, por similitud de nombre | — | Sí |
| **RF-04** | Dejar en estado `pendiente` los emparejamientos que no superen el umbral de confianza | HU-13 | Sí |
| **RF-05** | Registrar el precio de cada captura como serie temporal, sin sobrescribir | HU-07 | Sí |
| **RF-06** | Calcular el descuento real contra el mínimo observado | HU-06 | Sí |
| **RF-07** | Buscar por style code y por texto | HU-04 | Sí |
| **RF-08** | Filtrar por marca, talla, tienda y rango de precio | HU-05 | Sí |
| **RF-09** | Mostrar la comparación entre tiendas de un mismo modelo | HU-06 | Sí |
| **RF-10** | Mostrar el historial de precios en un gráfico | HU-07 | Sí |
| **RF-11** | Registrar e iniciar sesión con correo verificado o con Google | HU-01 | Sí |
| **RF-12** | Cerrar sesión localmente y en el proveedor de identidad | HU-02 | Sí |
| **RF-13** | Guardar, listar y eliminar seguimientos, aislados por usuario | HU-08, 09, 11 | Sí |
| **RF-14** | Restringir el panel de administración al grupo `admin` | HU-13, 14, 15 | Sí |
| **RF-15** | Notificar por correo cuando un seguimiento baja del mínimo o del objetivo | HU-10 | Post-MVP |
| **RF-16** | Mostrar el estado de las capturas y los errores acumulados | HU-14 | Post-MVP |
| **RF-17** | Filtrar la lista por presupuesto disponible | HU-12 | Post-MVP |

---

## 6. Requisitos no funcionales

### 6.1 Seguridad e identidad

| ID | Requisito |
|---|---|
| **RNF-01** | La autenticación usa **OIDC con Authorization Code + PKCE**. Nunca flujo implícito |
| **RNF-02** | Cada petición a una ruta protegida lleva el token en la cabecera `Authorization` |
| **RNF-03** | El API Manager valida **issuer**, **audience**, **firma** y **vigencia** antes de enrutar |
| **RNF-04** | El backend **vuelve a validar** el token: no confía en que el borde ya lo hizo |
| **RNF-05** | La autorización se resuelve por **grupo**, no por una lista de correos en el código |
| **RNF-06** | Los tokens no se guardan en `localStorage`, para reducir el daño de un XSS |
| **RNF-07** | CORS declara **orígenes explícitos**. Nunca `*` |
| **RNF-08** | Ningún secreto vive en el repositorio: van en el gestor de secretos |
| **RNF-09** | La base de datos no es accesible desde internet |
| **RNF-10** | Los errores de autorización responden el código correcto — **401** sin token, **403** con token pero sin permiso — y no filtran detalles internos |

### 6.2 Datos personales

| ID | Requisito |
|---|---|
| **RNF-11** | Solo se piden los datos necesarios: correo, nombre y las preferencias del producto |
| **RNF-12** | El usuario puede editar sus preferencias y desactivar los avisos en cualquier momento |
| **RNF-13** | Los avisos por correo solo salen a quien los activó |

> Aplica la **Ley 21.719** de protección de datos personales. El sistema no trata datos
> sensibles, pero el correo del usuario sí es dato personal y se maneja bajo el principio de
> minimización.

### 6.3 Uso responsable de las fuentes

| ID | Requisito |
|---|---|
| **RNF-14** | Se consultan **únicamente datos públicos** de precio y stock |
| **RNF-15** | Máximo **un request cada 1 a 2 segundos** por tienda |
| **RNF-16** | Se respeta `robots.txt` |
| **RNF-17** | El sistema deriva a la tienda: no intermedia la venta ni reproduce el catálogo completo |

> Detalle en [`PLAN.md §8`](PLAN.md#8-consideraciones-legales-y-éticas).

### 6.4 Disponibilidad y resiliencia

| ID | Requisito |
|---|---|
| **RNF-18** | Si un consumidor está caído, los mensajes esperan en la cola en vez de perderse |
| **RNF-19** | Un mensaje que falla se reintenta con espera creciente, y tras tres intentos se aparta |
| **RNF-20** | El procesamiento es **idempotente**: un mensaje repetido no duplica el historial |
| **RNF-21** | Si una tienda falla, las demás siguen capturando |
| **RNF-22** | La base se puede reconstruir desde las capturas crudas guardadas |

### 6.5 Rendimiento

| ID | Requisito |
|---|---|
| **RNF-23** | La búsqueda responde en menos de 2 segundos con el volumen del MVP |
| **RNF-24** | La ficha de producto carga la comparación y el historial en una sola petición |
| **RNF-25** | Un token inválido se rechaza en el borde, sin gastar cómputo del backend |

### 6.6 Usabilidad

| ID | Requisito |
|---|---|
| **RNF-26** | La web funciona en teléfono: es donde se mira un precio estando en la tienda |
| **RNF-27** | Tema claro y oscuro |
| **RNF-28** | Todo precio en pantalla dice de cuándo es |
| **RNF-29** | Después de iniciar sesión, el usuario vuelve a donde estaba |

### 6.7 Operación

| ID | Requisito |
|---|---|
| **RNF-30** | La misma imagen corre en `dev` y en `prod`, con la configuración por entorno |
| **RNF-31** | El costo mensual se mantiene dentro del crédito disponible |
| **RNF-32** | Los servicios son sin estado: se pueden reiniciar o replicar sin perder datos |

---

## 7. Trazabilidad con la rúbrica del EP1

Lo que el EP1 califica, y qué historia lo demuestra:

| Indicador de la rúbrica | Se demuestra con |
|---|---|
| Flujo de usuario OIDC completo | HU-01 |
| Obtiene los tokens necesarios | HU-01 |
| Guard de ruta | HU-08 — intentar seguir un modelo sin sesión |
| Interceptor que adjunta el token | HU-08, HU-09 |
| Lee roles y scopes desde los claims | HU-13 — el panel solo aparece para `admin` |
| Valida issuer, audience, firma y vigencia | RNF-03, RNF-04 |
| Autorización por rol | HU-13 |
| Códigos de error adecuados | RNF-10 — 401 sin token, 403 sin permiso |

> Las tres historias que sostienen la nota del EP1 son **HU-01**, **HU-08** y **HU-13**. Si algo
> se cae del alcance, no puede ser ninguna de esas tres.
