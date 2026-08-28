# Plan del proyecto

> La idea completa, el alcance, las fuentes de datos y las decisiones de producto.
> Para el **cómo está construido** ver [`ARQUITECTURA.md`](ARQUITECTURA.md).
> Para el **quién hace qué y cuándo** ver [`TAREAS.md`](TAREAS.md).
>
> Documento vivo · Última revisión: 19-08-2026

---

## Índice

1. [La idea en corto](#1-la-idea-en-corto)
2. [Alcance](#2-alcance)
3. [Tiendas: estado verificado](#3-tiendas-estado-verificado-18-08-2026)
4. [Modelo de datos](#4-modelo-de-datos)
5. [Matching: el método concreto](#5-matching-el-método-concreto)
6. [El descuento real: los tres números](#6-el-descuento-real-los-tres-números)
7. [Costos: capturar 24/7 sin quemar el crédito](#7-costos-cómo-capturar-247-sin-quemar-el-crédito)
8. [Consideraciones legales y éticas](#8-consideraciones-legales-y-éticas)
9. [Riesgos](#9-riesgos)
10. [Glosario](#10-glosario-rápido)
11. [Decisiones pendientes](#11-decisiones-pendientes)

---

## 1. La idea en corto

Una web que **compara el precio del mismo modelo de zapatilla entre varias tiendas chilenas**,
guarda el historial de precios y muestra el **descuento real** — medido contra el mínimo que
nosotros observamos, no contra el precio inflado que la tienda usa como referencia.

**El problema.** El SERNAC ya persiguió a Falabella y Paris por inflar precios antes del Cyber:
un producto se promocionaba con 65% de descuento usando como referencia $198.900, cuando días
antes esa referencia rondaba los $100.000 — el descuento real era 30%. Hoy el comprador no tiene
forma de saberlo.

**Por qué es posible.** Las zapatillas de marcas terceras se venden en varias tiendas a la vez y
muchas traen un **style code** de fábrica (ej. `HV9774`, de Nike) que funciona como identificador
único compartido entre tiendas.

> 🔴 **Medido el 27-08 sobre 2.088 productos: el style code NO sirve para comparar entre
> tiendas.** Solo Nike lo trae de forma extraíble (86 de 89, 96%); New Balance, Adidas, Joma,
> Asics y Puma dan **0%**, porque cada marca esconde su código con su propio prefijo y sin
> formato común. Y sobre todo: **Hites no publica ninguno**.
>
> Donde sí está, el style code hace bien su trabajo: en Sparta el SKU se descompone en
> `174000` + `FJ7791` (modelo) + `-50096` (color), así que agrupa los colores de un mismo modelo
> — se verificó con las 4 versiones del Nike G.T. Hustle Academy.
>
> **Conclusión: el style code es identidad interna, no mecanismo de comparación.** Ese rol lo
> toma el matching por nombre con `pg_trgm` (§5), que pasa a ser el mecanismo **principal** y no
> el respaldo.

**Para quién.** Alguien que está por gastar $80.000–$150.000 en zapatillas y quiere saber dónde
está más barato, si el descuento es real, y si su talla está disponible.

---

## 2. Alcance

### Entra
- Calzado deportivo y urbano de **adulto**
- Marcas terceras: **New Balance, Adidas, Nike y Puma** (cerrado el 27-08, ver aviso abajo)
- Productos **nuevos**, vendidos online, con precio y stock visibles
- Precios en CLP, tallas de adulto

### No entra (y por qué)
| Fuera | Razón |
|---|---|
| Marcas propias del retail (Basement, Sybilla, H&M, Zara) | Existen en una sola tienda: no hay nada que comparar |
| Vestuario de marca | **Fuera del MVP, no fuera del proyecto** — ver el aviso de abajo |
| Segunda mano, réplicas, ventas por Instagram | Sin catálogo estructurado |
| Carrito, pagos, checkout | La app informa y deriva a la tienda. No vendemos |
| App móvil | Web responsive |

> 👕 **Zapatillas es el recorte de la primera entrega, no la ambición del proyecto.**
> El plan a futuro es cubrir **ropa además de calzado**. Se parte por zapatillas porque es donde
> el problema se puede resolver bien: traen *style code* de fábrica, se venden en varias tiendas
> a la vez y el mismo modelo es comparable sin ambigüedad. En vestuario los nombres son
> genéricos y sin código, así que el matching es el problema difícil — y meterlo antes de tener
> el flujo completo andando sería cambiar un MVP que funciona por uno que casi funciona.
>
> Concretamente: **el EP1 va solo con calzado.** La ropa entra cuando el matching esté medido y
> el sistema desplegado, y el modelo de datos ya la soporta sin cambios (`modelo_canonico` tiene
> `categoria`, y `variante` tiene `talla` y `color`, que sirven igual para una polera).

### Límites duros del semestre
- **2 tiendas en el MVP** (Sparta + Hites), no más
- 4 marcas, y dentro de ellas los modelos con más presencia
- **3 capturas diarias** por tienda (08:00 / 15:00 / 22:00)
- Historial **desde el 27-08-2026**, que es cuando arrancó el scraper

> 🎯 **Las 4 marcas del MVP, cerradas el 27-08 con el criterio correcto.**
>
> Hubo dos vueltas acá y la primera fue un error, así que conviene dejar escrito el porqué.
>
> **Primera medición:** al filtrar el catálogo de Sparta por el atributo `gral_marca` apareció
> que Sparta casi no vende Nike ni Puma, y se propuso cambiar las marcas a New Balance, Adidas,
> **Joma y Asics** por volumen.
>
> **El error:** el volumen de una tienda es el criterio equivocado para un **comparador**. Una
> marca sirve solo si está en **las dos** tiendas. Consultando la faceta de marcas de Hites:
>
> | Marca | Zapatillas en Sparta | ¿En Hites? | ¿Comparable? |
> |---|---|---|---|
> | **New Balance** | 408 | ✅ | **Sí — el núcleo** |
> | **Adidas** | 239 | ✅ | **Sí — el núcleo** |
> | **Nike** | 24 | ✅ | Sí, poco volumen |
> | **Puma** | 4 | ✅ | Sí, muy poco |
> | Joma | 138 | ❌ **no la vende** | **No: no hay contra qué compararla** |
> | Asics | 95 | ❌ **no la vende** | **No** |
>
> **Joma y Asics quedan fuera**: tienen catálogo en Sparta pero Hites no las vende, así que
> aportarían cero pares comparables. Las 4 del MVP vuelven a ser las originales del plan, ahora
> por un motivo medido: **son las que existen en ambas tiendas**. El volumen real lo ponen New
> Balance y Adidas; Nike y Puma entran porque son comparables, no porque muevan la aguja.
>
> El scraper captura **las seis** de todas formas: capturar de más es barato, capturar de menos
> es irrecuperable.

---

## 3. Tiendas: estado verificado (revisado el 27-08-2026)

Probado con `curl` contra los endpoints reales, no supuesto.

| Tienda | Plataforma | Cómo se obtienen los datos | Nivel | ¿MVP? |
|---|---|---|---|---|
| **Sparta** | Magento | **GraphQL público sin autenticación** ✅ En producción desde el 27-08. Trae SKU, precio, precio de lista, tallas y stock por talla | 1 · trivial | **Sí, andando** |
| **Hites** | Salesforce Commerce | ⚠️ `Search-UpdateGrid` **da 500 desde el 27-08**. Sirve `/search?q=` (24 productos por página con `data-pid`) → Jsoup | 2 · medio | **Sí, por escribir** |
| Tricot | Salesforce Commerce | Mismo patrón que Hites (se reutiliza el scraper), pero casi no vende marca tercera | 2 · bajo valor | No |
| Nike.cl | VTEX | Es VTEX (tiene API) pero está tras **Cloudflare con challenge JS** → Playwright | 3 · caro | Post-MVP |
| Falabella | Next.js | Vía `curl` devuelve la home; hay `__NEXT_DATA__` con `skuId` y `brandName` → Playwright | 3 · caro | Post-MVP |
| Ripley | Next.js | `__NEXT_DATA__` de la home solo trae marketing | 3 · caro | Post-MVP |
| Paris | sin identificar | 2,2 MB de HTML sin firma reconocible | 3 | Post-MVP |
| adidas.cl | — | 403 directo | 3 | Post-MVP |
| Mercado Libre | API oficial | **Requiere OAuth con app registrada** (ya no responde sin token) | 2 | Post-MVP |

**Ejemplo real de la respuesta de Sparta:**

```json
{"sku":"174000HV9774-00121",
 "name":"Zapatillas basquetbol hombre Nike Air Zoom GT Cut Academy 2 Negro",
 "stock_status":"IN_STOCK",
 "variants":[{"product":{"sku":"...0012104"},"attributes":[{"label":"7.5","code":"zapatillas_tallaus"}]}]}
```

Dentro del SKU `174000**HV9774**-00121` está el style code de Nike. **Sparta es nuestra fuente
canónica**: de ahí sale el catálogo de modelos, y contra ese catálogo se matchean las demás tiendas.

> 🔴 **Corrección del 27-08 · la búsqueda por texto no filtra por marca.**
> Pedir `search: "nike zapatillas"` devuelve **1126 resultados de los que solo 24 son Nike**: el
> término "zapatillas" domina y las cuatro marcas devolvían casi los mismos productos. Hay que
> usar el atributo `gral_marca`, cuyos ids salen de `aggregations`:
>
> ```graphql
> products(filter: {gral_marca: {eq: "21"}}, pageSize: 50, currentPage: 1)
> ```
>
> Ids: New Balance `21` · Adidas `3` · Joma `18644` · Asics `447` · Nike `23` · Puma `30`.
> Con el filtro puesto, lo capturado calza exacto con lo que declara la tienda.

⚠️ **Hites no publica el style code**, solo un SKU interno (`956894`). El matching con Hites será
por marca + nombre normalizado (ver §5).

---

## 4. Modelo de datos

> ⚠️ Este modelo es la **propuesta base**, no la versión final. Antes de escribir las migraciones,
> el Integrante 2 hace una pasada de análisis con datos reales de Sparta y Hites (tarea de la
> Semana 1). Lo que sí está decidido es la separación estado / serie temporal.

### Decisión clave: `oferta` es estado, `precio_historico` es serie

Esto estaba ambiguo antes y habría causado un problema serio en septiembre:

- **`oferta` = el estado actual.** Una fila por `(variante_id, tienda_id)`. En cada captura se
  hace **UPSERT**, nunca INSERT. No crece con el tiempo.
- **`precio_historico` = la serie temporal.** Solo se agrega (append), nunca se actualiza.
  Es la tabla que crece y la que alimenta el gráfico.

```
marca              (id, nombre)

modelo_canonico    (id, style_code, nombre, marca_id, categoria)

variante           (id, modelo_id, talla, color)

tienda             (id, nombre, plataforma, nivel_dificultad)

oferta             (id, variante_id, tienda_id, url, precio, precio_lista,
                    stock, moneda, actualizado_en)
                   UNIQUE (variante_id, tienda_id)          -- upsert por esta clave

precio_historico   (id, variante_id, tienda_id, capturado_en, precio, precio_lista,
                    stock, clave_idempotencia)
                   UNIQUE (clave_idempotencia)              -- ver ARQUITECTURA.md §6
                   INDEX  (variante_id, capturado_en DESC)  -- para el gráfico

ejecucion_captura  (id, tienda_id, inicio, fin, productos_ok, productos_error, estado)

match_candidato    (id, modelo_id, tienda_id, sku_externo, nombre_externo,
                    similitud, estado)   -- estado: pendiente | aceptado | rechazado

seguimiento        (id, usuario_sub, modelo_id, talla, creado_en)
                   -- usuario_sub = claim "sub" del JWT
```

**Por qué se agregó cada tabla nueva:**

- **`ejecucion_captura`** — sirve para tres cosas: el test de contrato ("esta corrida trajo más de
  N productos válidos"), detectar cuándo una tienda cambió su HTML, y ser **evidencia del informe**.
- **`match_candidato`** — convierte un posible fracaso ("el matching por nombre falla") en un
  hallazgo gestionado ("el 20% requiere revisión humana, y así lo manejamos").
- **`precio_lista`** — el precio tachado que muestra la tienda. Es **la evidencia del descuento
  inflado**, y hay que guardarlo desde el día 1 porque no se puede recuperar después.
- **`seguimiento` sobre `modelo_id`** (no `variante_id`) — uno sigue un modelo, y la talla es
  opcional. Sobre variante obligaría a seguir cada talla por separado.

---

## 5. Matching: el método concreto

1. **Normalizar** el nombre: minúsculas, sin tildes, y quitar palabras de ruido
   (`zapatillas`, `hombre`, `mujer`, `urbano`, `running`, `deportivo`).
> 🔴 **Los umbrales 0,85 / 0,60 NO resisten el catálogo real. Medido el 27-08** contra los
> 1.496 modelos cargados de la captura de Sparta:
>
> | Comparación | Similitud | Qué debería pasar | Qué pasa |
> |---|---|---|---|
> | `574 Negra` vs `574 Negro` — **mismo zapato** | **0,696** | aceptar | cae a revisión manual |
> | `574 Negra` vs `515 Negra` — **otro zapato** | **0,660** | rechazar | cae a revisión manual |
>
> El correcto queda **bajo** el umbral que acepta y el equivocado queda **sobre** el que
> rechaza: entre acertar y equivocarse hay **0,04**. Con nombres reales casi todo cae en la
> banda gris.
>
> **La causa:** el número del modelo (574 vs 515) es lo único que distingue dos zapatillas
> completamente distintas, y el trigrama casi no lo pesa porque comparten todas las demás
> palabras.
>
> **Consecuencia para el matcher:** no puede ser solo trigrama sobre el nombre completo. Hay que
> **extraer el número/token de modelo y exigir coincidencia exacta**, dejando el trigrama para
> el resto. `unaccent` sí funciona: «Básquetbol» contra «Basquetbol» da 1,00.

2. **Comparar** con **`pg_trgm`**, extensión que ya viene en Postgres de RDS y aporta
   `similarity(a, b)` → número entre 0 y 1.
3. **Umbrales:**

| Similitud | Qué pasa |
|---|---|
| `> 0.85` | Match automático |
| `0.60 – 0.85` | Va a `match_candidato` para revisión humana |
| `< 0.60` | Se descarta |

Cuando existe **style code** (Sparta), el match es directo y no pasa por similitud.

**Métrica para el informe:** se revisan 30 productos a mano y se reporta
*"24 de 30 con match correcto = 80% de precisión, 3 falsos positivos"*. Es un **resultado medido**,
no una opinión, y es lo que separa un informe bueno de uno promedio.

---

## 6. El descuento real: los tres números

Con **dos semanas** de historial, nuestro "mínimo histórico" es en realidad *"el mínimo que
hemos observado desde el **27 de agosto**"* — que es cuando arrancó el scraper. Presentarlo como
verdad absoluta es indefendible. La ficha muestra los tres números y deja que el usuario saque
la conclusión:

```
Precio hoy                        $89.990
Referencia de la tienda          $149.990   (–40% según ellos)
Mínimo observado por nosotros     $84.990   (desde el 22-08-2026)
→ Descuento real vs. mínimo observado: 5,9%
```

Esa transparencia **es** el argumento del proyecto, y además nos blinda: no estamos afirmando
más de lo que podemos probar.

**Descarga del historial:** botón "Descargar CSV" en la ficha de producto. Sirve al usuario y,
de paso, es nuestro respaldo si el crédito de AWS se agota (ver §7).

---

## 7. Costos: cómo capturar 24/7 sin quemar el crédito

**La pregunta:** ¿tiene que haber un PC prendido todo el rato? ¿O la nube corriendo todo el rato?

**Respuesta corta: no, y cuesta prácticamente cero.**

| Pieza | Cómo funciona | Costo real |
|---|---|---|
| **Lambda + EventBridge** | Se "despierta" 3 veces al día, corre 30 segundos, se apaga. **90 invocaciones al mes** | **$0** — el free tier es 1.000.000 de requests/mes |
| **S3 (crudo)** | ~90 archivos JSON de 1–2 MB al mes ≈ 150 MB | **~$0,003/mes** |
| **RDS Postgres** | `db.t4g.micro` corriendo 24/7 | ⚠️ **~$15–25/mes** — el único costo real del proyecto |
| **EC2** | Los 4 servicios en un `docker compose` | `t3.small` ≈ **~$15/mes**; se apaga fuera de horario |

**Estrategia de crédito:**

1. **S3 es la fuente de verdad, no la base de datos.** Si el crédito se agota y hay que apagar
   RDS, los datos siguen intactos en S3 y se recargan después. El historial no se pierde nunca.
2. **RDS recién desde la Semana 2.** Hasta entonces, Postgres local en Docker.
3. **`aws s3 sync` a local una vez por semana** — respaldo del historial en el PC, gratis.
4. **Apagar la EC2 fuera de horario de trabajo**: el crédito se consume solo si está prendida.
5. ❌ **No usar EKS**: el control plane cuesta ~USD 73/mes y se come el crédito.
6. ❌ **No usar Amazon MQ**: ~USD 22/mes. RabbitMQ es un contenedor más del compose.

### ⚠️ Sobre la persistencia de RabbitMQ

"RabbitMQ con volumen persistente" suena bien, pero amarra la cola al disco de una instancia
concreta y agrega complejidad. **Decisión consciente: la cola es efímera.** Si RabbitMQ se reinicia y
pierde mensajes, se reprocesan desde el crudo en S3.

Escrito así, deja de ser una debilidad y pasa a ser una decisión con fundamento — que es
exactamente lo que se evalúa en la defensa.

---

## 8. Consideraciones legales y éticas

Estamos consultando sitios comerciales de forma automatizada. Media página en el informe cierra
esta pregunta antes de que la hagan:

- **Qué recolectamos:** únicamente **datos públicos de precio, stock y nombre de producto**,
  los mismos que cualquier persona ve al entrar al sitio. Ningún dato personal, ninguna cuenta.
- **Cómo:** un request cada 1–2 segundos, **User-Agent identificable** con contacto,
  y respeto a `robots.txt`.
- **Volumen:** 3 capturas diarias. Es una fracción despreciable del tráfico de estos sitios.
- **Qué no hacemos:** no revendemos el dato, no clonamos catálogos, no hay checkout.
  **La app deriva el tráfico a la tienda**, no lo captura.
- **Interés público:** el precedente del SERNAC contra Falabella y Paris por precios de referencia
  inflados respalda que exista transparencia de precios para el consumidor.

Si alguna tienda solicita que dejemos de consultarla, se retira del sistema.

---

## 9. Riesgos

| Riesgo | Qué hacemos |
|---|---|
| **Alguien llega a EP2 sin dominar OIDC** | Semana 0: los tres construyen su propio flujo. Sincronización los sábados. Es nota individual |
| **El historial queda vacío** | Scraper corriendo desde el 19 de agosto, aunque sea con cron local. **Riesgo número uno del proyecto** |
| **Nada desplegado el día de EP2** | Hay que mostrar el sistema andando en la nube. Despliegue en la Semana 2, no en la 3 |
| **La cuenta de AWS Academy bloquea Cognito** | 🔴 **Sigue sin probarse.** Plan B: **Keycloak como un contenedor más del compose** |
| **Confusión con el `aud` de Cognito** | Documentado en [ARQUITECTURA.md §9](ARQUITECTURA.md#9-el-detalle-del-audience-en-cognito) |
| **Sparta cambia o cierra su GraphQL** | Hites queda como segunda fuente; el crudo en S3 permite reprocesar lo capturado |
| **El matching por nombre falla mucho** | Se mide con 30 productos en la Semana 2. Si es malo, el MVP muestra solo los matches confirmados y se reporta el número honesto |
| **El crédito de AWS se agota** | S3 como fuente de verdad; sin EKS, sin Amazon MQ, sin Fargate; apagar la EC2 fuera de horario |
| **Nos bloquean por scrapear** | 1 request cada 1–2 s, User-Agent identificable, respetar `robots.txt` |
| **La demo en vivo se cae** | Video de respaldo grabado el día anterior |

---

## 10. Glosario rápido

| Término | En simple |
|---|---|
| **OAuth2** | El protocolo para dar permisos sin entregar la contraseña. Responde "¿qué puede hacer?" |
| **OpenID Connect (OIDC)** | Capa encima de OAuth2 que agrega identidad. Responde "¿quién es?" |
| **IDaaS** | Identidad como servicio: un tercero (Cognito) se encarga de usuarios y tokens |
| **JWT** | El token: un texto firmado que dice quién eres y qué puedes hacer |
| **JWKS** | La lista pública de llaves de Cognito. Verifica la firma del JWT sin llamar a nadie |
| **`iss` (issuer)** | Quién emitió el token. Debe ser nuestro User Pool y no otro |
| **`aud` (audience)** | Para quién es el token. Evita que un token de otra app sirva en la nuestra |
| **PKCE** | Protección extra para apps sin secreto (un frontend), para que nadie robe el código de autorización |
| **`state`** | Valor aleatorio que protege contra CSRF en el redirect del login |
| **`nonce`** | Valor aleatorio que evita que reutilicen un `id_token` viejo |
| **IdP federado** | Un tercero (Google) que autentica por nosotros; Cognito confía en él y emite nuestros tokens |
| **Client credentials** | Flujo OAuth2 para máquinas: el scraper obtiene token sin humano detrás |
| **DLQ** | *Dead Letter Queue*: cola donde caen los mensajes que fallaron mucho, para revisarlos aparte |
| **Idempotencia** | Que procesar el mismo mensaje dos veces dé el mismo resultado que procesarlo una |
| **BFF** | *Backend for Frontend*: junta datos de varios servicios para que el frontend pida una sola vez |
| **Style code** | Código de fábrica del modelo (ej. `HV9774`), el mismo en todas las tiendas del mundo |
| **CORS** | Regla del navegador que decide qué sitios pueden llamar a nuestra API |

---

## 11. Decisiones pendientes

- [x] ~~Nombre del proyecto~~ → **Cacha el Precio**
- [x] ~~IDaaS~~ → **AWS Cognito** (19-08-2026), con la tabla de equivalencias en [EVALUACIONES.md](EVALUACIONES.md#2-requisitos-del-ramo-y-cómo-los-cumplimos)
- [x] ~~El scraper publica directo a Rabbit o por el API Manager~~ → **por el API Manager**
- [x] ~~Equipo de 2 o 3~~ → **3**, autorizado por la docente
- [ ] Confirmar la fecha exacta de EP2 (presentación individual)
- [ ] Confirmar quién es el Integrante 1, 2 y 3
- [ ] Validar en la Semana 0 que AWS Academy permite Cognito y API Gateway
- [ ] Definir la paleta y tipografía del frontend (Int. 1, Semana 2)
- [ ] ~~Registrar el dominio~~ → **descartado para el MVP**: se usa la URL de CloudFront
