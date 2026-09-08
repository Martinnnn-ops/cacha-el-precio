# cacha-el-precio — frontend modular

Comparador de precios de ropa entre Ripley, Paris, Zara, H&M y Mango.
Vue 3 (`<script setup>`) + Vite + Pinia + Vue Router 4 + axios.

```bash
npm install
npm run dev     # http://localhost:5173
npm run build
npm run humo    # contraste, adaptador, stores, render de vistas, seguridad de
                # rutas, y la capa de servicios contra :8080 si está levantado
```

Sin `VITE_API_BASE_URL` definida la capa de servicios responde con los datos de
`src/modules/comparador/data/`, así que la aplicación funciona entera sin
backend. Copia `.env.example` a `.env` para apuntar a uno real.

## Conexión con el Product Service

```bash
cp .env.example .env     # ya viene apuntando al proxy
npm run dev
```

En desarrollo el frontend habla con el gateway ASP.NET Core de
`cacha-el-precio` en `localhost:8080`. El navegador pide `/api` al servidor de
Vite y el proxy conserva ese prefijo al reenviar.

| Qué pasa | Dónde se resuelve |
|---|---|
| El contrato vigente es `1.0` | `core/api/http.js` manda `Version: 1.0` en cada petición |
| Las rutas públicas son `GET /api/products` y `GET /api/products/{id}` | `VITE_API_BASE_URL=/api` y el servicio llama a `/products` |
| El frontend y el gateway ocupan puertos distintos en desarrollo | el proxy de Vite reenvía `/api` a `BACKEND_URL=http://localhost:8080` |
| ASP.NET y el gateway pueden devolver Problem Details o `{ error, mensaje }` | el interceptor traduce ambos formatos a mensajes mostrables |

> **Para producción el proxy no sirve.** O el front y la API van bajo el mismo
> dominio, o hay que habilitar CORS en el gateway ASP.NET Core.

### El modelo no coincide, y hay un adaptador

```
API   Product { id, externalId, store, name, brand, category, price,
                sizes, description, url, image, active }
App   Producto { id, nombre, categoria, precios[], historial[] }
```

`services/producto.adapter.js` traduce una cosa en la otra. `category` alimenta
los filtros, `store` identifica la fuente de precio y `sizes` se transforma en
la lista de tallas disponibles.

#### Lo que falta para que esto sea un comparador

La API guarda una oferta por fila, pero todavía no relaciona automáticamente
el mismo artículo entre varias tiendas. Para comparar una prenda equivalente
hace falta una identidad de producto compartida y ofertas separadas:

```
Oferta { id, productoId, tiendaId, precio, precioLista, stock, fecha }
Tienda { id, nombre }
```

Con eso el adaptador agrupa por `productoId` y **todo lo demás ya está escrito**:
el cálculo del más barato, el ahorro entre tiendas, el gráfico de historial y
los filtros por tienda.

Mientras tanto la interfaz se degrada sola en vez de mentir:

| Falta en la API | Qué hace la app hoy |
|---|---|
| agrupación del mismo producto entre tiendas | cada oferta se muestra como un producto independiente |
| precio de lista | no pinta el porcentaje de descuento |
| stock por talla | usa `active` para la oferta y `sizes` para cada talla |
| historial de precios | la ficha muestra «todavía no tenemos historial» |
| contador de visitas | no se envía ninguna llamada porque el backend no ofrece ese endpoint |

---

## Publicidad (Google AdSense)

`public/ads.txt` declara ante Google que somos el editor legítimo del
inventario. Va en `public/` porque Vite copia esa carpeta tal cual a la raíz
del build, y Google lo busca exactamente en `/ads.txt`.

### Activar los anuncios

```bash
VITE_ADSENSE_CLIENT=ca-pub-2105662597936673
VITE_ADSENSE_SLOT_PORTADA=   # data-ad-slot del bloque de arriba
VITE_ADSENSE_SLOT_CIERRE=    # data-ad-slot del bloque de abajo
```

> El id va como `pub-…` en `ads.txt` y como `ca-pub-…` en la variable. Es el
> mismo número; si no coinciden la cuenta queda sin verificar y nada avisa, así
> que `npm run humo` lo comprueba.

**Mientras los ids de bloque estén vacíos, `AdSlot` no renderiza nada.** Se
puede desplegar sin ellos: la portada se ve limpia, sin cajas vacías.

### Detalles que importan

- **El script de AdSense se inyecta desde `shared/services/adsense.js`**, no
  desde `index.html`. Así no se descarga nada de terceros cuando no hay
  anuncios que mostrar — en desarrollo, en las pruebas o antes de terminar la
  configuración.
- **Una sola inserción por bloque montado.** AdSense lanza `TagError` si se
  empuja dos veces sobre el mismo `<ins>`.
- **El alto se reserva desde el primer pintado.** El anuncio llega tarde y sin
  reserva empujaría el contenido; ese salto lo mide Core Web Vitals (CLS).
- **Los bloques van etiquetados «Publicidad».** Las políticas de AdSense
  permiten etiquetas neutras, y en un comparador de precios es lo honesto.

### Pendiente

- **Los ids de bloque.** Hay que crearlos en el panel de AdSense y pegarlos en
  `.env`. Hasta entonces la portada no muestra huecos, que es lo buscado.
- **Consentimiento (CMP).** Sólo si el sitio empieza a recibir visitas del EEE
  o Reino Unido: AdSense exige entonces una plataforma certificada o deja de
  servir anuncios a ese tráfico. Para audiencia chilena no aplica.

---

## Publicar (AWS S3 + CloudFront)

```bash
BUCKET=mi-bucket DISTRIBUCION=E123ABC npm run desplegar
```

La configuración de la infraestructura está en **[`deploy/README.md`](deploy/README.md)**,
incluida la parte que más fácil se hace mal: con S3 «sitio web estático» y
`index.html` como documento de error, **todas las rutas profundas devuelven
HTTP 404** aunque la página se vea bien. Los rastreadores lo notan, y eso
hunde el posicionamiento y puede tumbar la revisión de AdSense. La solución
son las respuestas de error de CloudFront mapeando 403/404 → `/index.html`
con **200**.

`sitemap.xml` se genera al compilar a partir del mapa de rutas
(`scripts/sitemap.mjs`), así que añadir o quitar una página lo actualiza sola.
`npm run humo` comprueba que sus URLs existen de verdad y que no incluyen nada
que `robots.txt` bloquee.

---

## Marca

El símbolo es el mismo dibujo en los tres sitios donde aparece —cabecera, pie y
pantallas de sesión— y en el favicon: `shared/components/BrandMark.vue` y
`public/favicon.svg` comparten los mismos trazos, y `npm run humo` falla si
alguien cambia uno y no el otro.

Va todo en `currentColor`, así que hereda el color del contexto y funciona en
claro y en oscuro sin una segunda versión.

> **Nota de diseño:** el visto bueno dentro de la lupa ocupa unos 6px cuando el
> símbolo va a 30px, y a ese tamaño se empasta. La silueta —etiqueta, lupa y
> mango— sí se reconoce, que es lo que importa en un logo pequeño. Si quieres
> que el detalle interior también se lea, hay que retocar el dibujo original:
> agrandar la lupa respecto de la etiqueta, o engrosar el tic.

---

## Qué se ve cuando el backend no responde

La estructura se pinta **siempre**. Un fallo nunca sustituye la página: aparece
una franja arriba con el aviso y un botón de reintentar, y debajo se sigue
viendo el sitio entero.

| Situación | Qué se ve |
|---|---|
| Cargando | Siluetas con la forma real de las tarjetas (`ProductoCardSkeleton`), así el contenido cae en el mismo sitio y no hay salto |
| Falla y no hay datos | Titular, títulos de sección, categorías, pie… y la franja de aviso |
| Falla y **sí** hay datos | Los precios siguen visibles, con «no pudimos actualizar, son de hace 10 minutos» |
| Sin conexión | Aviso propio: reintentar no sirve de nada hasta que vuelva la red |

Piezas: `shared/components/BaseSkeleton.vue` (primitiva),
`shared/components/AvisoDatos.vue` (la franja) y los esqueletos con forma de
contenido en `modules/comparador/components/`.

**El detalle que más importa:** el store **no borra lo que ya tenías** cuando
una recarga falla. Perder datos que ya estaban en pantalla es el peor momento
para quitárselos a alguien; es mejor enseñarlos diciendo de cuándo son.
`npm run humo` lo comprueba contra un backend inexistente de verdad
(`.env.fallo`), no simulando el error a mano.

---

## Layouts

El layout de cada ruta se declara en su `meta.layout` y lo resuelve `App.vue`.

| Layout | Rutas | Qué tiene |
|---|---|---|
| `default` | catálogo, ficha, legales | cabecera completa + pie completo. Con `meta.ancho: 'lectura'` estrecha el contenido a 820px |
| `auth` | entrar, registro, retorno de Google | barra mínima con la marca y pie sólo con enlaces legales. Sin buscador ni filtro de tiendas: en una pantalla de sesión, cada elemento de más es una salida por la que abandonar el proceso |

Viven en `src/layouts/` y **no** en `shared/`: son parte del punto de
composición, como `App.vue` y `core/router`. Son el único sitio donde el marco
se junta con el estado de los módulos.

`App.vue` además envuelve todo en un `onErrorCaptured`: si un componente lanza
durante el render, el usuario ve una pantalla con salida en vez de un blanco.

---

## Pantallas

| Ruta | Vista | Qué hace |
|---|---|---|
| `/` | `InicioView` | Portada: destacado, Lo más reciente, Lo más visto, Categorías populares, Ofertas del día y dos huecos de anuncio. |
| `/comparador` | `ComparadorView` | Búsqueda, filtro por tienda y rejilla de resultados. Acepta `?categoria=Poleras`. |
| `/producto/:id` | `ProductoDetailView` | Precio por tienda e historial. |
| `/entrar`, `/registro` | módulo `cuenta` | Sesión. |
| `/terminos`, `/privacidad`, `/preguntas` | módulo `legal` | Páginas de texto. |

---

## Lenguaje visual

"Papel y tinta": fondo crema, tarjetas que imitan un **ticket de compra** con
sus muescas troqueladas, tipografía Space Grotesk para titulares e IBM Plex
Mono para las cifras. La idea es que comparar precios se parezca a revisar
boletas.

Todo sale de los tokens de `src/assets/base.css` — escala tipográfica en `rem`,
espaciado base 4, radios, sombras y duraciones. Un componente que escriba un
hex o un píxel a mano se sale del sistema.

Dos piezas que sostienen el aspecto:

- **`BaseTicket`** pone las muescas por su cuenta (arriba y abajo). Van dentro
  del componente porque acordarse de ponerlas en cada tarjeta es justo lo que
  se termina olvidando.
- **`PrendaArt`** es el relleno de las tarjetas sin fotografía: una camiseta en
  una percha. El proyecto del que viene el diseño dibujaba una zapatilla porque
  comparaba calzado.

---

### Contrato consumido

| Endpoint | Acceso | Versión |
|---|---|---|
| `GET /api/products` | público | `Version: 1.0` |
| `GET /api/products/{id}` | público | `Version: 1.0` |
| `POST/PUT/DELETE /api/products` | requiere token y scope de escritura | `Version: 1.0` |

El frontend sólo consume las operaciones públicas. Las categorías y tiendas
se derivan de la respuesta del listado; no se consulta el alias legado
`/catalogos`.

---

## Inicio de sesión con Google

Flujo **OAuth 2.0 Authorization Code + PKCE**, no el implícito: el implícito
deja el token en el fragmento de la URL, donde queda en el historial del
navegador y a la vista de cualquier extensión.

### Configurar

El frontend usa Cognito Hosted UI y fuerza el proveedor Google. Configura en el
App Client público de Cognito (sin secret) los callbacks
`http://localhost:5173/auth/google` y `https://TU-DOMINIO/auth/google`:

```bash
VITE_COGNITO_CLIENT_ID=xxxxxxxxxxxxxxxxxxxxxxxxxx
VITE_COGNITO_DOMAIN=https://tu-dominio.auth.tu-region.amazoncognito.com
```

El id y el secreto OAuth de Google se guardan en la configuración del proveedor
federado dentro de Cognito. **No pongas ningún secreto en `.env`, en una
variable `VITE_` ni en Git**: Vite lo publicaría en el bundle. `npm run humo`
comprueba que no aparezca el prefijo habitual de un secreto de Google.

### Qué protege qué

| Riesgo | Defensa |
|---|---|
| CSRF en el retorno | `state` aleatorio en sessionStorage, verificado y **consumido** al volver: no vale dos veces |
| Código interceptado | PKCE con `S256`; el verificador nunca sale del navegador |
| Token robado por XSS | vive en `sessionStorage` (muere con la pestaña), no en `localStorage` |
| Código en el historial | la vuelta usa `router.replace`, no `push` |
| `id_token` falsificado | no se leen sus claims; el perfil se pide al endpoint de Google con el token |

> **Siguiente paso de endurecimiento:** que el canje del código lo haga el
> backend y devuelva una cookie `HttpOnly + Secure + SameSite`. Así el token no
> pasa por JavaScript y un XSS no puede leerlo. Sólo cambia `canjearCodigo`.

---

## Legal

`/terminos` y `/privacidad` describen con precisión cómo funciona el servicio,
pero **no los ha revisado nadie con competencia legal**. Antes de tratarlos
como definitivos deben pasar por revisión y ajustarse a la normativa aplicable
(en Chile, Ley 19.496 de protección al consumidor y Ley 19.628 sobre datos
personales). La fecha que se muestra sale de `modules/legal/vigencia.js`.

---

## Seguridad del frontend

Primero lo importante: **nada de esto es autorización**. Todo corre en el
navegador del usuario y se salta con las herramientas de desarrollo. Quien tiene
que decidir si alguien puede ver algo es el backend, en cada petición. Lo de
aquí evita que un enlace preparado haga daño desde el propio frontend.

`shared/utils/rutas.js` concentra el saneo de todo lo que llega por la URL:

| Riesgo | Qué se hace |
|---|---|
| **Open redirect** — `/entrar?volver=https://sitio-falso.cl` te devolvería a un sitio ajeno con la confianza de venir del tuyo | `rutaInternaSegura()` sólo acepta rutas que empiezan por una barra: descarta `//host`, `\`, cualquier esquema (`javascript:`, `data:`) y los caracteres de control |
| **Parámetro de ruta arbitrario** — `/producto/../algo` saldría a la red con lo que sea | la forma del id se declara en la ruta (`:id(\d{1,12})`), así que ni llega a la vista; la vista lo revalida igualmente |
| **Texto de la URL sin límite** | `textoDeUrl()` quita los caracteres de control y recorta a 120 |
| **Título de pestaña manipulable** | `afterEach` compone el título sólo con textos del mapa de rutas, nunca con algo de la URL |
| **Vistas de sesión** | guard `soloInvitados` / `requiereSesion` en `core/router/index.js` |

Lo que **no** cubre y hay que hacer en el servidor: cabeceras de seguridad
(CSP, `X-Content-Type-Options`, HSTS), CORS, límite de peticiones y la
autorización real de cada endpoint.

---

## Cómo está organizado

Por **dominio**, no por tipo de archivo. La pregunta que responde esta
estructura es "¿dónde está todo lo del comparador?" y no "¿dónde están todos
los componentes?". Un módulo se puede leer, mover o borrar entero.

| Carpeta | Qué va aquí | Qué NO va aquí |
|---|---|---|
| `core/` | Lo que arranca la aplicación: cliente HTTP, router, Pinia. Una sola vez. | Nada de un dominio concreto. |
| `modules/<x>/` | Todo lo de esa funcionalidad: vistas, componentes, store, servicios, rutas, datos. | Nada que use otro módulo. |
| `shared/` | Lo genérico y reutilizable: `BaseButton`, formateo, reglas de precio. | Nada que sepa de un módulo. |

### La regla que sostiene el resto

**Las dependencias apuntan hacia dentro.** `modules/` puede importar de `core/`
y de `shared/`; `shared/` y `core/` **nunca** importan de `modules/`. Un módulo
no debería importar de otro módulo: si dos lo necesitan, eso que comparten
pertenece a `shared/`.

### Las capas dentro de un módulo

```
vista  →  store  →  servicio  →  core/api/http  →  backend
```

- **Servicio** — conoce las URLs y la forma de la respuesta. Es el único que
  toca `http`. Devuelve objetos de dominio.
- **Store** — guarda el estado y orquesta. Llama al servicio y nunca ve una
  respuesta HTTP. Aquí van los filtros, el orden y el `cargando`.
- **Vista/componente** — pinta. Los componentes reciben props y emiten eventos;
  la vista es la que habla con el store.

Se separan para que cambiar de backend sea tocar un archivo, y para poder
probar la lógica de filtros sin levantar nada.

---

## Añadir un módulo nuevo — ejemplo: alertas de precio

### 1. Estructura

```bash
mkdir -p src/modules/alertas/{components,views,store,services}
```

```
src/modules/alertas/
├── components/AlertaForm.vue
├── views/AlertasView.vue
├── store/alertas.store.js
├── services/alertas.service.js
└── routes.js
```

### 2. Servicio — `services/alertas.service.js`

```js
import http, { USAR_MOCK } from '@/core/api/http'

export async function obtenerAlertas() {
  if (USAR_MOCK) return []

  return http.get('/alertas')
}

export async function crearAlerta({ productoId, precioObjetivo }) {
  if (USAR_MOCK) return { id: crypto.randomUUID(), productoId, precioObjetivo }

  return http.post('/alertas', { productoId, precioObjetivo })
}
```

Sin `fetch` ni `axios` sueltos: siempre a través de `http`, que ya lleva la URL
base, el token y la traducción de errores.

### 3. Store — `store/alertas.store.js`

```js
import { computed, ref } from 'vue'
import { defineStore } from 'pinia'

import { crearAlerta, obtenerAlertas } from '@/modules/alertas/services/alertas.service'

export const useAlertasStore = defineStore('alertas', () => {
  const alertas = ref([])
  const cargando = ref(false)
  const error = ref(null)

  const total = computed(() => alertas.value.length)

  async function cargar() {
    cargando.value = true
    error.value = null

    try {
      alertas.value = await obtenerAlertas()
    } catch (e) {
      error.value = e.message
    } finally {
      cargando.value = false
    }
  }

  return { alertas, cargando, error, total, cargar }
})
```

El `id` del store (`'alertas'`) tiene que ser único en toda la aplicación.
No hay que registrarlo en ningún sitio: se registra solo al llamar
`useAlertasStore()` por primera vez.

### 4. Rutas — `routes.js`

```js
export default [
  {
    path: '/alertas',
    name: 'alertas',
    component: () => import('@/modules/alertas/views/AlertasView.vue'),
    meta: { titulo: 'Mis alertas' },
  },
]
```

Siempre con `() => import(...)`: cada vista viaja en su propio trozo y no se
descarga hasta que alguien entra en esa ruta.

### 5. Engancharlo — `src/core/router/routes.js`

Dos líneas, y es lo **único** que se toca fuera del módulo:

```js
import comparadorRoutes from '@/modules/comparador/routes'
import alertasRoutes from '@/modules/alertas/routes'      // ← nueva

export const routes = [
  ...comparadorRoutes,
  ...alertasRoutes,                                        // ← nueva
  { path: '/:pathMatch(.*)*', name: 'no-encontrado', /* … */ },
]
```

El catch-all del 404 va siempre al final.

### 6. Antes de darlo por hecho

- ¿El módulo importa algo de `modules/comparador/`? Si es así, eso va a
  `shared/`. Las reglas de precio ya están en `shared/utils/precios.js`
  precisamente por esto: las alertas necesitan el mismo "cuál es el más barato".
- ¿Algún componente llama a `http` o a `axios`? Debe pasar por su servicio.
- ¿La vista tiene estado de carga, de error **con reintento**, y de vacío?
- ¿Añade un enlace en la barra de `App.vue` si es una sección de primer nivel?
- Añade el módulo a `scripts/humo.mjs` y pasa `npm run humo`.

---

## Notas de implementación

- **`@` → `src/`** (alias en `vite.config.js`). Con módulos anidados los
  imports relativos se vuelven `../../../core/api/http` y atan el archivo a su
  carpeta.
- **`core/router/routes.js` está separado de `core/router/index.js`** porque
  `index.js` llama a `createWebHistory()`, que toca `window` al importarse. Con
  el mapa aparte se puede montar el mismo router en un test o en Node.
- **Interceptores en `core/api/http.js`**: el de petición engancha el token en
  cada llamada (al arrancar todavía no hay sesión); el de respuesta convierte
  cualquier fallo en un `Error` con un mensaje que se puede mostrar tal cual,
  en vez de dejar salir un `AxiosError` hasta la vista.
- **Los tokens de color y espaciado están en `src/assets/base.css`.** Un
  componente que escriba un hex a mano se sale del sistema.
