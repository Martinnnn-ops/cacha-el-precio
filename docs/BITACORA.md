# Bitácora del proyecto

Última actualización: 28-11-2026.

## 28-11 · Prometheus, Grafana y Loki, y la EC2 a `t3.medium` — Martín

Rama `feature/monitoreo`, armada en un *worktree* aparte mientras se cerraba la trazabilidad, y
llevada encima de `development` después del merge del PR #64.

- **Métricas:** `prometheus-net` en los 3 servicios .NET, en un puerto aparte (`METRICAS_PUERTO`,
  9464) que Caddy no publica: `/metrics` en el 8080 da **404**. Dos contadores propios:
  `cep_etapas_total` (cada paso de `RegistroTrazas`, con los duplicados descartados) y
  `cep_mensajes_total` (ok / reintento / dlq por cola).
- **Logs:** Loki + Alloy. El `correlationId` se busca como texto, no como etiqueta.
- **Tablero** «Cacha el Precio · salud» (28 paneles), cargado solo desde `infra/monitoreo/`.
- **Probado en local** con el compose real: 5 objetivos `up`; el gateway mostró 200 ×10, 401 ×5
  y 404 ×5; un pedido con `X-Correlation-Id: demo-profe-123` apareció en Loki. CloudWatch queda
  para la EC2.
- **Lo que costó:** el plan estimaba ~0,4 GB y medido fueron **~670 MB** (Grafana ~375). Grafana y
  Alloy rozaban su tope de RAM y se subieron a 512 y 192 MB. Por eso producción pasa a
  `t3.medium` ([ADR-036](adr/036-monitoreo-prometheus-grafana-loki.md)).
- **Una trampa:** `${GRAFANA_CLAVE:?}` habría botado el `up` normal y el CD, porque Compose interpola
  el archivo entero aunque el perfil esté apagado. La clave la genera `desplegar.sh`, como la de la base.

**Pendiente:** `tf.sh subir` con `t3.medium` (leer que diga *in-place*), prender el perfil en la
EC2 y confirmar que el `LabRole` deja leer CloudWatch.

## 28-11 · La trazabilidad destapa un error que llevaba semanas escondido — Martín

Rama `feature/trazabilidad`. Al seguir el barrido `barrido-paris-20261128T002117Z` en la pantalla
nueva «Seguir un barrido», los números no cuadraban: el scraper **envió 217 ofertas** y Kinesis
procesó **217 precios**, pero product-service **guardó solo 206**. Faltaban 11.

Buscando ese número de seguimiento en los logs JSON salió la causa en dos minutos:

```
Npgsql.PostgresException 22001: value too long for type character varying(2000)
```

Algunas fichas de Paris traen una descripción de **más de 2.000 caracteres**, que es el máximo de
`Description` en `ProductDbContext`. Y el error estaba mal clasificado: el consumidor lo toma como
**recuperable** (un `DbUpdateException` cualquiera), así que lo reintenta 5 veces —inútil, la
descripción no se va a acortar sola— y recién ahí lo aparta en la DLQ. Resultado: **76 ofertas
acumuladas en `cep-ofertas-crudas-dlq`** desde barridos anteriores, sin que nadie lo notara.

**Por qué importa para la defensa.** Es el argumento a favor de la trazabilidad con un caso real:
sin el `correlationId` y sin contar cada etapa, «217 enviadas, 206 guardadas» no se ve. Con ellos,
la diferencia salta en la pantalla y el log exacto se encuentra filtrando por un solo número. Y
muestra que la **DLQ hizo su trabajo**: ninguna oferta se perdió, quedaron apartadas con su motivo
y se pueden reprocesar con el redrive cuando el arreglo esté.

**El arreglo:** `ProductService.RecortarDescripcion` guarda los primeros 2.000 caracteres, cortados
en el último punto y con «…» al final. Además, `ProcesadorOfertaCapturada` trata el `22001` como
error **definitivo**: si otro texto no cabe, va a la DLQ al primer intento, sin gastar 5 reintentos.
Pruebas en `DescripcionLargaTests`. Después del despliegue: redrive de las 76 desde el panel.

## 27-11 · El front vuelve a salir solo desde `main` — Martín

Rama `chore/front-desde-main`. Se deshace el atajo del 26-11 (PR #58): `avisar-frontend.yml`
avisa solo desde `main`, y el repo del frontend pasa a copiar desde `main` (`MONO_BRANCH: main`).
Así `main` vuelve a ser producción para todo y `development` vuelve a ser la rama de pruebas: un
PR mergeado sin revisar ya no llega a `www`. Antes se lleva `development` a `main`, que iba 334
commits atrás, para no publicar un front de agosto. Anotado como actualización del 27-11 en el
ADR-034.

## 26-11 (noche) · La portada dice «catálogo en pausa» con la API apagada — Panditax

Rama `feature/portada-catalogo-en-pausa`. Con la EC2 apagada, que es casi siempre, la portada
mostraba una franja roja «Estamos con problemas» que empujaba la página (CLS 0.14 en `www`),
«0 prendas seguidas hoy» y tres secciones vacías: parecía un sitio abandonado, y así lo ven el
evaluador y AdSense. Ahora, si la API falla y no hay **ninguna** prenda, `InicioView` muestra
«Catálogo en pausa · Los precios vuelven pronto» en el hueco de «Lo más reciente», con botón de
reintentar, y oculta «Lo más visto» y «Categorías». Si hay prendas de antes, sigue el aviso de
siempre. El aviso mide 358 px, lo mismo que las siluetas (medido a 375 y 1024 de ancho): al
pasar de una a otro, lo de abajo se movió 0 px. CLS 0.002 con `--mode fallo`.

El humo tenía comprobaciones del diseño anterior («Lo más visto sigue ahí»); se cambiaron por
las del nuevo, conservando la idea: la página no se reemplaza por un error y siempre se puede
reintentar. Queda: el catálogo (`/comparador`) y la ficha siguen con su aviso de error normal.

## 26-11 (noche) · El front sale desde `development` al tiro — Panditax

Rama `feature/aviso-frontend-development`. El PR #57 se mergeó a las 06:49 UTC y `www` no
cambió: el repo del frontend copia desde `development`, pero el monorepo le avisaba solo desde
`main`, así que quedaba esperando la copia programada «cada 15 min», que GitHub corría cada
varias horas (13:28, 22:38, 04:21…). Se lanzó a mano (`gh workflow run`) y quedó publicado a las
08:57. Verificado en `www`: tema claro con el dispositivo en oscuro, CLS 0, `/assets` con
`immutable`, el hash de la CSP calza, canonical y sitemap en `www` y sin `/admin`.

Arreglo: `avisar-frontend.yml` avisa también desde `development`. Cambia una decisión del
ADR-034 (el aviso era «solo desde `main`»), anotada como actualización del 26-11 en el mismo ADR.
Se eligió esto y no pasar el repo del frontend a copiar desde `main` porque `main` va más de 300
commits atrás: publicaría un front viejo, sin el login con Google. Queda: llevar `development` a
`main` antes de la EFT y, después, volver al diseño original (las dos puntas a `main` a la vez).

## 26-11 · Rendimiento del front: menos fuentes y la foto de la ficha primero — Panditax

Rama `feature/rendimiento-front`. Antes se revisó una lista genérica de rendimiento contra el
código. Casi todo ya estaba: esqueletos, `encode zstd gzip` en Caddy, caché del catálogo en el
store, rutas con `import()`, `loading="lazy"`, `AsNoTracking`, índices y timeouts. Lo demás no
aplica (no hay IA, las imágenes son de las tiendas, una sola EC2). Quedaron dos cambios chicos:

- **Google Fonts pasa de 11 archivos a 7.** Se contó qué familia, peso y estilo usa el CSS.
  Fraunces en cursiva no lo usa nadie (el «°» del logo va con `font-style: normal`), y Plex Mono
  500 y Plex Sans 500 tampoco. Se comprobó en el navegador que todo lo que se pinta queda cargado.
  Fraunces 400 y Plex Mono 700 se usan y **nunca se cargaron** (tampoco antes): el navegador los
  simula. Queda anotado por si alguien nota un negrito raro.
- **La foto de la ficha lleva `fetchpriority="high"`**: es lo más grande arriba de la página.

La rama sale del `development` del 26-11 (con el CI/CD del PR #55). `npm run build` pasa y
`npm run humo` da 0 fallas. Falta pasarlo al repo de Panditax con el `subtree` y probarlo en
Slow 4G desde las DevTools.

### Auditoría de `www` y tres arreglos

Se auditó el sitio publicado (con el backend apagado: la API da 503, lo normal fuera de las
sesiones del Academy). Bien: HTTP/3, Brotli, ~100 KB propios, encabezados de seguridad, móvil
sin desbordes. Se arregló en la misma rama:

| Qué | Medido en `www` | Arreglo |
|---|---|---|
| La portada **saltaba al cargar** | CLS 0.84 (0.70 de un solo golpe). Vue montaba antes de que llegara la vista, que se carga aparte: el pie y el anuncio se pintaban arriba y bajaban | `main.js` monta tras `router.isReady()`. En local, CLS 0.011 |
| `/assets/*` **sin caché** | `max-age=0, must-revalidate` en archivos con hash | regla `/assets/*` con `max-age=31536000, immutable` en `_headers` |
| **Canonical** a la raíz, que redirige a `www`, y **el mismo para todas las páginas** | todas decían ser la portada | `www` en `index.html`, sitemap y `robots.txt`; el router actualiza canonical y `og:url` por ruta |
| El sitemap listaba **`/admin/mensajeria`** | — | fuera las rutas con `requiereSesion` o `soloAdmin`; `robots.txt` bloquea `/admin/` |

Quedan abiertos (en el estado «API caída», que es el que más se ve): el aviso de error empuja
la página hacia abajo (CLS 0.14) y la portada dice «Todavía no hay prendas nuevas» y «0 prendas
seguidas hoy» como si estuviera vacía. También: rutas inexistentes con 307 a `/` en vez de la
página 404, sin `og:image`, y la CSP report-only con avisos de Cloudflare Insights y AdSense.
La regla de caché hay que confirmarla con `curl -I` después de desplegar.

### El tema vuelve a arrancar en claro

Decisión de Panditax: el sitio sale **claro siempre**, aunque el dispositivo esté en oscuro.
Revierte el «tema por sistema» del rework visual (`9c12d2c`). «Oscuro» y «Sistema» siguen en el
botón para quien los elija, y se guardan. Cambian `ui.store.js` (`POR_DEFECTO = 'claro'`), el
script inline de `index.html` y sus comprobaciones en `humo.mjs`. Como el script cambió, se
recalculó su hash en la CSP de `_headers` (si no, la CSP lo marcaría al pasar a modo bloqueo).
Probado con el navegador en modo oscuro: sin preferencia sale claro, y con «sistema» sale oscuro.

Al revisar antes del PR: `router.isReady()` se rechaza si la primera navegación falla (se corta
la red bajando la vista), y la app no se montaba nunca: página en blanco en vez de la pantalla de
error. Ahora monta igual. Se confirmó que ningún guard espera a la API, así que montar después de
`isReady()` solo espera el trozo de la vista.

## 23-11 (noche) · Google funcionando en `www` y `tf.sh` más resistente — Martín

Rama `feature/cicd`.

```text
www (bundle publicado)  → VITE_COGNITO_DOMAIN = cacha-el-precio-731705830984 (Panditax lo cambió)
login con Google en www → ✅ entra (2 cuentas de Martín)
hacer-admin.sh          → una de ellas al grupo admin
tf.sh subir martin      → 1ª pasada: apply OK, el despliegue NO partió (llave SSH vieja)
                          2ª pasada: 0 cambios y tampoco desplegó (la EC2 "ya existía")
desplegar.sh + restaurar a mano → respaldo 20261123-191814: 325 del scraper, 224 en el catálogo, /health 200
```

### Hallazgos y arreglos

- **Llave SSH vieja.** Con `conservar_ip = true` la EC2 nueva hereda la IP de la anterior y
  `known_hosts` guarda la llave de aquella: ssh corta con «REMOTE HOST IDENTIFICATION HAS CHANGED».
  → `tf.sh` borra esa llave (`ssh-keygen -R`) cuando la máquina es nueva.
- **Una pasada cortada no se retomaba.** La segunda vez el id de la EC2 no cambió, así que `tf.sh`
  la daba por desplegada y la dejaba vacía. → Si la EC2 existe pero no tiene el `.env` de la app,
  `tf.sh` retoma el despliegue y la restauración.
- **`tf.sh subir` salía con código 1 aunque todo saliera bien** (la última línea era un
  `[[ ]] && {...}` falso). → `if`.
- **`desplegar.sh` seguía de largo si fallaba el paso 2** (pendiente del 23-11). → Se detiene, y el
  script remoto corre con `pipefail` para que un `compose up` fallido no pase por bueno.
- **Google en Cognito salía como «1 to change» en cada plan:** Cognito agrega 6 valores fijos de
  Google que el código no declaraba. → Declarados en `identidad.tf`; el plan queda en 0 cambios.

### Pendiente

- [ ] CSP: revisar la consola cuando `_headers` esté publicado (después del merge) y recién ahí
  pasarla a modo real. Cloudflare inyecta un `<script>` inline propio que hoy quedaría bloqueado.
- [ ] Panditax: `MONO_BRANCH: main` y revisar el bundle antes de publicar ([CICD](CICD.md#pendiente)).
- [ ] PR `feature/cicd → development` y después `development → main`.

## 23-11 · Google en Cognito: `tiene_dominio = true` aplicado — Martín

Rama `feature/cicd`. Cierra la deuda de `tiene_dominio` de la entrada del 19-11.

```text
tf.sh plan (a mano)     → 3 a crear (Google en Cognito, EC2, asociación de la IP),
                          16 cambios, 0 borrados
                          14 integraciones: http://3.227.178.123:8080 → https://api.cacha-el-precio.com
                          pool us-east-1_UQacrbVDn: deletion_protection INACTIVE → ACTIVE
                          client 15mg17g…: se le suma «Google», SIN reemplazo (mismo id)
tf.sh subir martin      → apply OK; el despliegue se cortó (ver hallazgos)
desplegar.sh --solo-backend (RAMA=development)
                        → 6 contenedores arriba, /health 200 dentro de la máquina
                          respaldo de S3 20261119-210012 restaurado: 325 productos
API Gateway             → /health 200 · /productos 200
api.cacha-el-precio.com → /health 200 (https, Caddy)
```

### Hallazgos

- **La primera conexión SSH a la máquina nueva se cortó** («Connection timed out during banner
  exchange») justo después de copiar el `.env`. `desplegar.sh` no se detuvo: siguió al paso 3, que
  esperó un `/health` imposible, y `tf.sh` intentó restaurar el respaldo sin código en la máquina
  (`./tools/respaldar.sh: command not found`). Volver a correr `desplegar.sh --solo-backend` con la
  máquina ya arriba lo resolvió. Pendiente: que el paso 2 corte el script si el SSH falla.
- **`www` manda el login al Cognito equivocado.** El bundle publicado lleva la API y el client id
  de `731705830984`, pero `VITE_COGNITO_DOMAIN` sigue en `cacha-el-precio-276428661451` (el
  Cognito viejo de Panditax): Cognito responde `invalid_request`. Del lado de AWS está todo listo
  (dominio `cacha-el-precio-731705830984` activo, Google habilitado en el cliente, retorno
  `https://www.cacha-el-precio.com/auth/google` registrado). Se le avisó a Panditax para cambiar la
  variable de build en Cloudflare y volver a publicar.

### ⬜ Qué falta

- [ ] Panditax: `VITE_COGNITO_DOMAIN=https://cacha-el-precio-731705830984.auth.us-east-1.amazoncognito.com`
  en Cloudflare y volver a publicar
- [ ] Probar «Entrar con Google» en `www` y `tools/hacer-admin.sh`
- [ ] PR `feature/cicd → development` y después `development → main`

## 19-11 (tarde) · CI/CD cerrado: solo `main` despliega y `www` usa la cuenta `731705830984` — Martín

Rama `feature/cicd`. Cierra la entrada de abajo: **lo de Cloudflare ya no está bloqueado**, porque
cambió el camino del frontend. Cómo quedó todo: [CICD](CICD.md) y [ADR-034](adr/034-cicd-con-github-actions.md).

### Lo que cambió

| Qué | Antes (madrugada del 19-11) | Ahora |
|---|---|---|
| Publicar el front | job `frontend` en `cd.yml` con `wrangler deploy`, bloqueado por el rol en Cloudflare | **Panditax** armó la sincronización: `avisar-frontend.yml` → su repo copia `frontend/` y compila → Cloudflare publica el Worker con cada push a su `main`. El job propio se sacó (`f0de0b4`) |
| Desde qué rama | `cd.yml` con `main` + `feature/cicd`; el aviso con `development` | todo **solo desde `main`** (`7cb1f0e`, `a62df6b`) |
| `www` | S3 de `988704560705` | Worker de Cloudflare (cuenta de Panditax), con las variables de build apuntando al API Gateway y al Cognito de `731705830984` |
| Encabezados de seguridad | ninguno | `frontend/public/_headers`: HSTS, `nosniff`, `X-Frame-Options`, `Referrer-Policy`, `Permissions-Policy` y CSP en *Report-Only* |

### Prueba de punta a punta

```text
tf.sh subir martin      → plan: 2 a crear (EC2 + asociación de la IP), 0 cambios, 0 borrados
                          respaldo restaurado: 224 productos
CD (re-run del job)     → desplegó 7cb1f0e desde GHCR, /health 200
API Gateway             → /health 200 · /productos 200 (224) · CORS desde www: 204
www                     → el bundle lleva mcxw9tpe13 (API) y el client 15mg17g… (Cognito)
                          el login y el catálogo funcionan en el dominio (probado por el equipo)
```

### Hallazgos

- `tiene_dominio = false` en `martin.tfvars`: la cuenta ya es la publicada, pero **sin Google en
  Cognito** y sin `deletion_protection`. Pasarlo a `true` cambia el destino del API Gateway a
  `api.cacha-el-precio.com`, que todavía apunta a `44.215.16.200`: **primero el DNS, después el
  `.tfvars`**. Anotado en la deuda de [CONTEXTO](CONTEXTO.md).
- El repo del frontend todavía copia desde `development` y no revisa el bundle.
- El dominio de Cognito quedó en Cloudflare con `/` al final; funciona, Panditax lo corrige.
- El commit de prueba `7f8c78f` dejó un comentario con la «ó» en Latin-1 en `frontend/README.md`:
  se pasó a UTF-8.

### ⬜ Qué falta

- [ ] Panditax: `MONO_BRANCH: main` en `sync-y-desplegar.yml` y la revisión del bundle
- [x] Mover el registro `api` a `3.227.178.123` (19-11, `infra/terraform/dominio`; Caddy sacó el certificado)
- [x] `google.env` + `tiene_dominio = true` + `tf.sh subir` (23-11, ver la entrada de arriba)
- [ ] Revisar la consola con la CSP en *Report-Only* y pasarla a modo real
- [x] Borrar del environment `produccion` las variables `CLOUDFLARE_ACCOUNT_ID` y `FRONT_*` (19-11)
- [ ] PR `feature/cicd → development` y después `development → main`

AWS apagado al cierre con `tf.sh apagar` (respaldo `respaldos/20261119-211348`); la IP queda reservada.

## 18/19-11 · CI/CD con GitHub Actions: CI verde, el backend se despliega solo — Martín

Rama `feature/cicd` (sin PR todavía). Bloques 2 y 3 del [PLAN-EFT](PLAN-EFT.md#2--cicd-con-github-actions).
La documentación completa (`docs/CICD.md` + ADR-034) va en el PR; esto es lo que **no se puede
olvidar** hasta entonces.

### Lo que quedó funcionando

| Qué | Estado |
|---|---|
| **CI** (`.github/workflows/ci.yml`): .NET (formato, build, 144 pruebas), scraper (195 pruebas + ruff), front (build + humo) y **dependencias vulnerables** (NuGet, `npm audit`, `pip-audit`) | ✅ verde en GitHub |
| **CD backend** (`cd.yml` + `tools/cd-remoto.sh`): CI → 4 imágenes a **GHCR** con el hash del commit → SSH a la EC2 → `pull` + `up --no-build` → `/health` → **rollback** si no queda sana | ✅ desplegó `499d8ff` solo en `731705830984` |
| **CD frontend**: build con el Cognito y la API de la cuenta (variables del environment), revisión del bundle y `wrangler deploy` a un Worker | 🟡 escrito y probado en local; sin token se salta con un aviso |
| `main` sin aprobación obligatoria (sigue exigiendo PR y sin force push) | ✅ |
| Environment **`produccion`**: secrets `EC2_HOST`, `EC2_SSH_KEY`; variables `CLOUDFLARE_ACCOUNT_ID`, `FRONT_*` | ✅ |
| `conservar_ip = true` en `martin.tfvars`: la IP `3.227.178.123` no cambia al apagar | ✅ |

El compose mantiene `build:` **y** suma `image:`: `desplegar.sh` y el desarrollo local siguen
compilando igual. El Dockerfile del scraper ahora instala desde `uv.lock` (antes `pip install .`
elegía versiones que el CI nunca probó).

### Lo que el CI encontró el primer día (y se arregló en la misma rama)

| Problema | Origen | Arreglo |
|---|---|---|
| `dotnet format` fallaba en `FeedPersonalizadoTests.cs` | PR #54 | formato |
| Terracota `#a8482e` sobre el pie: **4.02:1** (mínimo WCAG 4.5) | PR #53 | `#984028` (4.71:1) |
| El store seguía al sistema y el guion de `index.html` partía en claro → **fogonazo claro** con el dispositivo en oscuro | PR #53 | los dos siguen al sistema + prueba nueva que exige que partan igual |
| `follow-redirects` (vía axios) y `source-map-js`: vulnerabilidades *high* | dependencias | `npm audit fix` (solo lockfile) |
| pillow, urllib3, certifi, soupsieve: **51 avisos** de `pip-audit` | dependencias | `uv lock --upgrade-package` (pillow 13, soupsieve 4) |

📌 Avisarle a Orion lo del terracota y el tema (es su PR #53).

### ⛔ Bloqueado: el token de Cloudflare

La cuenta de Cloudflare es de Panditax y mi rol **no permite Workers** («Failed common permission
check… Workers Routes Write»). Hay que pedirle que suba el rol a *Administrator* o *Workers Admin*
(+ DNS de la zona), para crear `github-cd-workers` y cargarlo directo en GitHub sin que el token
pase por un chat. Decidido: el token **sin vencimiento** (a propósito); revocarlo al cerrar el ramo.

### 🔎 Hallazgo: lo que está en vivo NO es de nuestra cuenta

| Pieza | Apunta a | ¿Nuestra (`731705830984`)? |
|---|---|---|
| `www` y la raíz | CNAME a S3 `cacha-el-precio-web-988704560705` | ❌ |
| Login del sitio publicado | Cognito de `276428661451` | ❌ |
| `api` | `44.215.16.200` | ❌ (la nuestra es `3.227.178.123`) |

No hay ningún Worker en Cloudflare: `frontend/wrangler.jsonc` existía pero no se usaba.

### Para la defensa (va al ADR-034)

- **Puerto 22 abierto a `0.0.0.0/0`.** Ya estaba (para `ssh`, `desplegar.sh` y los respaldos);
  con el CD no se puede cerrar a una IP porque los runners de GitHub no tienen IP fija. Entrada
  **solo con llave**: verificado `passwordauthentication no`. Alternativas descartadas **por el
  lab**: SSM (las credenciales vencen cada ~4 h y OIDC + rol IAM está bloqueado en Academy) y un
  runner dentro de la EC2 (repo público: un PR podría correr código en el servidor). **En una
  cuenta real:** GitHub → OIDC → rol IAM → SSM, puerto 22 cerrado.
- **IP fija no es más vulnerable:** ya es pública por el DNS y los bots barren todo IPv4. Al
  revés, una IP que cambia deja el DNS apuntando a una IP que AWS puede darle a otro
  (*subdomain takeover*).
- **El CD no usa credenciales de AWS**: entra por SSH. Terraform sigue siendo manual (`tf.sh`)
  porque el estado vive en el laptop y las credenciales del lab vencen.
- **Front en Worker y no en S3:** subir a S3 exigiría credenciales de AWS en GitHub (vencen); el
  Worker solo necesita un token de Cloudflare y el sitio sigue arriba con AWS apagado. Además
  `not_found_handling: single-page-application` arregla el 404 de `/producto/:slug` que S3 no podía.
- **Mínimo privilegio:** un token por uso (`terraform-dns-martin` solo DNS; `github-cd-workers`
  solo Workers de la zona); el CI no usa secrets, por eso corre seguro en PRs de forks.
- Imágenes en GHCR **públicas** (no llevan secretos); igual la EC2 hace login con el token
  efímero del job y logout al terminar.

### ⬜ Qué falta (en orden)

- [ ] Panditax sube el rol en Cloudflare → crear token → `gh secret set CLOUDFLARE_API_TOKEN --env produccion`
- [ ] Push → Worker en `*.workers.dev` (ahí el catálogo y el login fallan por CORS/Cognito: esperado)
- [ ] Mover el dominio: `tiene_dominio = true` + `tf.sh subir` (Google en Cognito), `www` y raíz → Worker (Custom Domain), `api` → `3.227.178.123` con `infra/terraform/dominio/`. Avisar al grupo antes: el sitio viejo deja de responder
- [ ] Sacar los disparadores temporales de `feature/cicd` en `ci.yml` y `cd.yml`
- [ ] Docs: `docs/CICD.md`, ADR-034, CONTEXTO (deuda: puerto 22, token sin vencimiento), PLAN-EFT §2, README (Estado), DESARROLLO (el CI corre lo mismo)
- [ ] PR `feature/cicd → development` y después `development → main` (primer despliegue real desde `main`)
- [ ] Al cerrar el ramo: revocar `github-cd-workers`

## 17-11 · Terraform fases 6 y 7: el dominio, una cuenta desde cero y adiós a los `crear-*` — Martín

Rama `feature/terraform`. Con esto Terraform queda como la única forma de crear la infraestructura
y el [ADR-033](adr/033-terraform-para-la-infraestructura.md) pasa a **aceptado**.

### Fase 6: el registro `api` en Cloudflare

`infra/terraform/dominio/` (proveedor `cloudflare` 5.29) maneja **un solo registro**, el A de `api`,
adoptado con `import`: el primer `plan` dio 0 cambios. Mover la API a otra IP es
`terraform apply -var api_ip=<IP>`, con un token de Cloudflare que solo puede editar el DNS de esa
zona y vive fuera del repo. `www`, la raíz y el correo siguen a mano: apuntan al bucket de la
cuenta de Panditax y no cambian al prender o apagar.

### Fase 7: montar desde cero en una cuenta vacía

En `081199126041`, una cuenta del Academy sin nada, en una copia aparte del repo (`git worktree`),
porque `tf.sh` usa un estado y un juego de `.env` por carpeta.

```text
tf.sh subir prueba           → 81 creados, desplegar.sh OK, front en el bucket de esa cuenta
restauración del respaldo    → se cortó («Connection closed by remote host») y tf.sh terminó en 0
                               con la base vacía; reintentada a mano: 224 productos
probar-mensajeria.py         → TODO OK
tf.sh apagar prueba          → respaldo bajado al laptop, 3 destruidos (EC2, IP, asociación)
```

- **Lo que costó:** el corte de la restauración. No se encontró la causa (sin reinicio ni errores
  de `sshd`), así que se ataca el síntoma: `tf.sh` reintenta tres veces y, si no puede, **termina
  con error** e imprime cómo hacerlo a mano. `respaldar.sh --restaurar` también sale con error si
  falla. Un «listo» con la base vacía era el peor resultado posible (`3d40a10`).
- **Se retiraron los 4 `crear-*`** (`9e97759`). `desplegar.sh` ya no crea colas: si falta
  `mensajeria.env`, se detiene y pide `tf.sh subir`. `hacer-admin.sh`, `probar-mensajeria.py` y los
  comentarios del compose, el `Caddyfile` y el gateway ya no los nombran.
- **El panel de mensajería solo mira** (`99aba95`). Crear o borrar colas desde el panel chocaba
  con Terraform. Se quitaron las 8 rutas de escritura y la sección «Administrar» del frontend; se
  quedan la consulta, la revisión de DLQ, el redrive, la bandeja y el simulador. Decisión en la
  [actualización del ADR-032](adr/032-administracion-de-mensajeria.md#actualización-del-17-11-2026-el-panel-solo-mira).
- Re-prueba en la misma cuenta con la rama desplegada: `desplegar.sh` se detuvo sin
  `mensajeria.env` (0 colas creadas), y con él `probar-mensajeria.py` dio TODO OK en 29 chequeos,
  con las rutas de escritura en 404/405. Un error del propio script (`sufijo`) se arregló en
  `670d37e`. La cuenta de prueba quedó apagada.

### 18-11 · Los documentos, al día y ordenados

Primero, revisión de todos los documentos contra la rama: el `README` rehecho como presentación
del proyecto, con diagramas y sin comandos; los comandos de desarrollo pasan a
`docs/DESARROLLO.md` (nuevo), y van notas en los ADR 015, 019, 024, 026, 029, 032 y 033.

Después, **`docs/` se ordenó: de 15 documentos a 10**, todos con la misma forma (qué es, para
quién, «📍 En 10 segundos», la leyenda, diagramas y «Ver también»):

| Nuevo | Sale de |
|---|---|
| `CONTEXTO.md` | lo vigente de `INTEGRACION` y `DESPLIEGUE`: el estado de hoy en una página |
| `PLAN-EFT.md` | el plan de implementación de la EFT, versión para el equipo |
| `EL-PROYECTO.md` | `PLAN` + `REQUISITOS` + `CATALOGO-PERSONAL` |
| `ARQUITECTURA.md` | absorbe `IDENTIDAD` y lo vigente de `DESPLIEGUE` |
| `OPERAR-EN-AWS.md` | `MONTAR-EN-TU-CUENTA` + `MIGRACION` |
| `TAREAS.md` | solo lo vigente y **sin dueños**: el que puede, toma la tarea |
| `adr/README.md` | el índice de decisiones, que vivía en `ARQUITECTURA` |
| `HISTORIA.md` | lo que vale la pena de los documentos viejos |

Los diagnósticos viejos (`INTEGRACION`, `INTEGRACION-FRONTEND`, `DESPLIEGUE`, `REQUISITOS`,
`PLAN`, `CATALOGO-PERSONAL`, `IDENTIDAD` y la historia del EP1 de `TAREAS`) se borran y se
resumen en un solo `docs/HISTORIA.md`: lo que cambió desde agosto, la auditoría del 07-09 y lo
aprendido, con el `git show` para leer los originales. Se borraron el `arquitectura.drawio` (dibujaba Micronaut,
RabbitMQ, Kafka y una VPC privada que nunca existieron) y un `.docx` de otra idea (un cotizador de
prendas). Los README de los cuatro servicios se rehicieron con el mismo estilo y diagramas; el del
scraper absorbe `README.arquitectura.md` y `CONVERS.md`. Al rehacer el del gateway apareció que
decía que el seguimiento vivía en memoria: desde el 23-09 se guarda en product-service.

Y un poco de orden fuera de `docs/`: los tres proyectos de pruebas .NET pasan de la raíz a
`tests/` (mismo resultado antes y después: 139 pruebas OK y 3 omitidas, las de AWS real), y se
retira `tools/probar-mensajeria-academy.py`, el sondeo del EP3 que ya cubre
`infra/terraform/sondeo/`. El `docker-compose.yml` propio del scraper **se queda**: es la base
desechable de sus pruebas de integración, y el CLI y una prueba lo nombran.

Los logs de Terraform (los 4 del 14-11 y los del 17-11) **salen del repo**: se guardan fuera, junto
al material para el informe de la EFT, y los del 14-11 siguen en el historial de git. La
evidencia del EP1 al EP3 que citan los informes entregados se queda en `docs/evidencia/`.

**Pendiente:** el PR de `feature/terraform` (lo revisa y mergea Orion o Panditax); sincronizar el
frontend con el repo de Panditax, donde sigue «Administrar»; el código de crear/borrar que quedó
sin rutas; `respaldar.sh` sin el esquema `notification`; y rotar o ponerle fecha al token de
Cloudflare después de la EFT.

## 14-11 · Terraform fases 1 a 5: toda la cuenta y `tools/tf.sh` — Martín

Rama `feature/terraform`. `infra/terraform/cuenta/mensajeria.tf` crea las 2 DLQ, las 3 colas, el
tópico `cep-cambios-oferta`, la política de cada cola y las 2 suscripciones con su filtro. Igual que
`tools/crear-mensajeria.py`, lo lee todo de `infra/mensajeria/topologia.json`, que sigue siendo la
única fuente (ADR-029).

- **Se adoptó en vez de borrar.** En `731705830984` las colas del script se pasaron al estado con
  bloques `import`, sin borrarlas ni cortar el servicio. La receta quedó en `infra/terraform/README.md`.
- **Lo que apareció:** las colas tienen 1 MiB de tamaño máximo (el límite actual de SQS), pero el
  proveedor asume 256 KiB y las iba a achicar. Quedó fijo en 1 MiB.

```text
plan con import         → 10 to import, 0 to add, 0 to destroy (solo cambian etiquetas)
plan después del apply  → No changes
output mensajeria_env   → idéntico al mensajeria.env que dejaba el script
```

- **Prueba de la fase:** `destroy` (10 destruidos, 0 restos) → `apply` (10 creados en 2 min 22 s,
  por la espera de 60 s de SQS para reusar un nombre) → `plan` sin cambios. Con la EC2 apagada se
  publicaron 3 eventos: la baja de 15 % y el agotado llegaron a `notificaciones` y a la auditoría; la
  baja de 5 %, solo a la auditoría.

### Fase 2: la base pasa a `cuenta/base.tf`

Grupo de seguridad (22, 80, 443, 8080), EC2, IP elástica y los dos buckets, con los mismos nombres
que `tools/crear-infra.sh`. También se adoptaron con `import` (14 importados, 0 destruidos).

- **La EC2 no se reemplaza nunca por una AMI nueva.** La de la máquina ya no era la última, y sin
  `ignore_changes = [ami, user_data]` Terraform la habría recreado, y con ella la base de datos.
- **Disco de 8 a 20 GB en caliente.** Al prender, Amazon Linux estiró la partición sola: el disco
  quedó en 20 GB con 32 % de uso (estaba al 96 %).
- **Buckets:** los ajustes usan el nombre del bucket directo. Con `terraform_data.output` el primer
  plan los veía «por conocer» y los iba a reemplazar. Terraform nunca borra un bucket.
- `conservar_ip` (por defecto `false`) deja la IP reservada aunque se apague la máquina, para la
  cuenta con dominio.

```text
plan con import           → 14 to import, 0 to destroy (disco 8 → 20 y etiquetas)
plan después del apply    → No changes
EC2 tras prender          → 20G, 32 % usado, 6 contenedores arriba, datos intactos
API Gateway               → /health, /productos, /catalogos 200 · /seguimiento, /api/yo, /ingesta 401
tools/probar-mensajeria.py → TODO OK (log fuera del repo)
```

### Fase 3: Cognito pasa a `cuenta/identidad.tf`

Pool, dominio del Hosted UI, scope `ingesta`, los clientes `frontend`, `scraper` y `pruebas`, los
grupos y los dos usuarios de prueba, igual que `tools/crear-cognito.sh`. Google se crea solo con
`tiene_dominio = true` y sus credenciales por variable de entorno; en esa cuenta el pool queda con
`deletion_protection`.

- **Lo que costó:** el primer plan recreaba los 3 clientes por `generate_secret`, que Cognito no
  informa al importar (y cada cliente nuevo trae id nuevo). Quedó en `ignore_changes`.
- El `import` de un usuario en un grupo va con comas y el `sub`, no con barras y el correo.

```text
plan con import            → 12 to import, 0 to destroy (etiquetas, la misma clave, unidades de token)
plan después del apply     → No changes
output cognito_env         → idéntico a cognito.env, secreto del scraper incluido
tools/probar-mensajeria.py → TODO OK (401, 403, 200 e ingesta con token de máquina)
```

### Fase 4: el API Gateway pasa a `cuenta/api.tf`

La HTTP API con CORS de orígenes explícitos, el autorizador JWT con los 3 clientes, las 14
integraciones hacia la EC2 con `X-Borde-Secreto`, `POST /ingesta` directo a SQS con `LabRole`, las 24
rutas (la tabla de acceso pública / jwt / scope quedó en un solo `locals`) y los stages `dev` y `prod`
con su límite de 50/s. El destino del borde se decide solo: el dominio en la cuenta con dominio, y si
no `http://<IP>:8080`; si la IP cambia, Terraform actualiza las 14 integraciones.

```text
plan con import            → 43 to import, 0 to destroy (etiquetas; el secreto ya era el de borde.env)
plan después del apply     → No changes
output api_gateway_env     → idéntico a api-gateway.env
rutas                      → públicas 200 · jwt 401 · POST /productos 401 · preflight CORS 204
tools/probar-mensajeria.py → TODO OK
```

### Fase 5: el puente, `tools/tf.sh`

`subir`, `apagar`, `estado` y `plan`. Mete los secretos por variable de entorno (de `borde.env`,
`cognito.env` y `google.env`, o los genera), escribe los 5 `.env` con los mismos nombres que dejaban
los scripts, copia el estado a `~/cacha-cuentas/<cuenta>/estado/` y frena si el plan borra algo
fuera de la EC2 y la IP. Más `tools/hooks/pre-commit`, que frena commits con `.tfstate`, `.tfvars`,
`.env`, `.pem` o algo con forma de secreto (`git config core.hooksPath tools/hooks`).

- **El ciclo completo, probado.** `apagar` respaldó la base, la bajó al laptop y recién entonces
  destruyó la EC2, la IP y la asociación (3 recursos) y re-apuntó las 14 integraciones. `subir` creó
  una EC2 nueva, esperó al cloud-init, corrió `desplegar.sh` con `development` y restauró el
  respaldo del laptop.
- **La barrera funcionó a la primera:** el primer `apagar` no pudo respaldar (`respaldos/` en la EC2
  es de root, lo crea el respaldo horario) y no apagó nada. Ahora respalda con `sudo`, como el timer,
  y restaura desde `/tmp`.
- **Bug encontrado:** con `pipefail`, `terraform state list | grep -q` daba falso por SIGPIPE, y
  `estado` veía cambios pendientes con la máquina apagada. La lista va primero a una variable.
- La EC2 vieja corría código copiado a mano: antes de borrarla se comprobó que sus 21 archivos
  cambiados eran idénticos a `origin/development`. La nueva corre `development` limpio (`736be1e`).

```text
apagar                      → respaldo 260K en respaldos/ · 3 destruidos · /productos 500 (esperado)
subir                       → EC2 nueva (20 GB, 32 %), desplegar.sh OK, respaldo restaurado
filas tras restaurar        → Products 224 · ProductOffers 326 · price_history 325 = el respaldo
borde                       → /health 200 · :8080 directo sin secreto 403
tools/probar-mensajeria.py  → TODO OK en la máquina nueva
.env tras subir sin cambios → los 5 idénticos a los de los scripts
```

**Pendiente:** fase 6 (dominio en Cloudflare, de Panditax) y fase 7 (segunda cuenta desde cero y
retirar los scripts). `respaldar.sh` no vuelca el esquema `notification` (hoy solo tiene los correos
simulados de las pruebas). Las fases 2 y 3 no se destruyeron a propósito: borrarían la base y los
usuarios. La EC2 se probó con `tools/tf.sh apagar`, que respalda antes.

## 10-11 · Terraform: estructura, proveedor y sondeo de permisos — Martín

Rama `feature/terraform`. Arranca el paso de los scripts de `tools/` a Terraform
([ADR-033](adr/033-terraform-para-la-infraestructura.md)). `infra/terraform/cuenta/` todavía no
crea nada: esta sesión dejó la base y despejó la duda que podía tumbar el plan entero.

- **Fase 0.** Estructura, versiones (Terraform 1.x, proveedor `hashicorp/aws` 6.72.0), variables
  por cuenta y el ADR. El `.terraform.lock.hcl` trae las huellas de Linux, Mac y Windows para que
  `init` funcione en la máquina de los tres.
- **Lo que costó:** `terraform init` se cortaba bajando el proveedor (176 MB, `unexpected EOF`) y
  parte de cero en cada intento. Se bajó con `curl -C -`, que retoma, y se usó con `-plugin-dir`.
  La receta quedó en `infra/terraform/README.md`.
- **Fase 0.5, sondeo.** `infra/terraform/sondeo/` creó 31 recursos con prefijo `cep-sondeo-` en
  la cuenta `731705830984` y los destruyó. Pasó todo menos **S3**: el recurso `aws_s3_bucket` lee
  la configuración de *object lock* y una SCP de Academy la niega con *explicit deny*. Arreglo
  probado: el bucket lo crea la CLI desde un `terraform_data` y Terraform maneja sus ajustes. Con
  eso, un segundo `plan` no dio cambios.

```text
POST /ingesta sin token                → 401
POST /ingesta con token client_credentials → 200, mensaje en la cola
Recursos con gestionado-por=terraform  → 14 encontrados por la API de etiquetas
terraform destroy                      → 31 destruidos, 0 restos
```

- También se completó el registro de decisiones de `ARQUITECTURA.md`, que se cortaba en el 025.

**Pendiente:** fase 1 (colas y SNS desde `topologia.json`); antes, `crear-mensajeria.py --borrar`
en la cuenta para que las colas de los scripts no choquen. Decidir si la cuenta con el dominio
conserva la IP al apagar.

## 26-10 · El armador pasa a ser un armario digital — Martín

Rama `feature/armario-outfit`. Solo frontend; la lógica del store (precios, tallas, cuerpo completo,
lo más barato) no se tocó.

**Por qué.** El armador eran 8 cajas iguales en una rejilla de dos columnas, la mitad diciendo
«Todavía no tenemos prendas para esta parte», con el orden del cuerpo mezclado (ropa interior al
lado del abrigo), lo opcional explicado en un párrafo y el total al fondo de la página en el
teléfono. La referencia fueron apps de closet como Fits o Vloset, manteniendo la estética del sitio.

**Cómo queda.** A la izquierda, el **perchero**: pastillas por parte del cuerpo (solo las que
tienen prendas), búsqueda con orden, género y tallas en una línea, y una rejilla de fotos grandes
sobre blanco. Un clic pone o saca la prenda en su lugar. A la derecha, **tu outfit**: de la cabeza
a los pies con una línea que une las zonas, las tres capas del torso apiladas, lo obligatorio
siempre visible y lo opcional como «+ gorro», que lleva al perchero filtrado. Debajo, «Cuánto te
sale». En el teléfono, una barra fija con el total. Se llama «perchero» y no «armario» porque «Mi
armario» ya es lo que la persona guardó.

**Se fueron** `RanuraParte`, `VistaPreviaOutfit` y `SelectorPrenda`. Cuatro comprobaciones de
`humo` describían el diseño viejo; ahora piden perchero y tablero con la misma intención.

**Pendiente.** El catálogo de la cuenta del lab tiene 224 prendas de Falabella (zapatillas y
poleras): sin pantalones, abrigos ni gorros, el tablero muestra «Piernas: todavía no hay prendas».
Falta cargar más tiendas con el scraper.

## 26-10 · Panel de mensajería para la demo: bandeja de correos, simular aviso y orden por flujo — Martín

Rama `feature/bandeja-correos`, desde `development`.

**Frontend de Panditax al día.** El `main` de su repo tenía cinco commits del 21-10 que no estaban
en el monorepo: tema claro por defecto, ficha en móvil, retorno de Google y `wrangler.jsonc`. Se
trajeron con `git subtree pull`. Hubo dos conflictos (`humo.mjs` y `ProductoDetailView.vue`) y en
los dos se quedó su versión, que era la más nueva. `frontend/` quedó idéntico a su `main`.

**El correo solo se veía en los logs.** En la EC2 notification-service corre con
`CORREO_MODO=log`, así que el correo se armaba completo pero solo aparecía en
`docker compose logs`. Ahora cada correo se copia en `notification.correos_generados` (los últimos
50) y el panel lo muestra tal como llega, a través de `/api/admin/mensajeria/correos`. La migración
se escribió a mano, porque `dotnet-ef` no está instalado. Para cuidar eso hay una prueba que compara
el snapshot con el modelo, y se comprobó que falla si se cambia un largo de columna. De paso, el
correo se rediseñó con tablas y estilos en línea, con los colores de la marca.

**Simular un aviso.** `POST /api/admin/mensajeria/simular-aviso` publica en `cep-cambios-oferta`
un cambio inventado con los datos reales de un producto. Usa el mismo sobre que product-service,
con `source = "panel-admin"`, y no toca precios. En el panel son tres botones fijos y una barra de
cuatro pasos que avanza sola. Probado en la EC2: una baja de 30% y un agotado generan correo, y una
baja de 5% la filtra la regla (pide 10% o más).

**El panel, una sola página por flujo.** Tiene un índice que dice para qué sirve cada sección,
tablas de colas y reglas encerradas en su caja (cada DLQ bajo su cola), el plano de los dos flujos
y, al final, administrar. Se probó con pestañas y con cajas sueltas, pero leía mejor como tablas
ordenadas.

**Lo que costó.** El disco de la EC2 (8 GB) estaba al 96% y el build de notification-service falló
con `No space left on device`. Se liberaron ~1,5 GB de caché de builds e imágenes sin usar, sin
tocar volúmenes, y quedó al 80%. Otro tropiezo: `docker compose up --build gateway` intenta
reconstruir product-service, porque es dependencia; para reconstruir uno solo hay que usar
`build <servicio>` + `up -d --no-deps`.

**Pendiente.**
- ⚠️ La EC2 corre esta rama copiada a mano, no `development`. Un `desplegar.sh` la pisa hasta que
  se mergee el PR.
- Las DLQ tienen 5 mensajes de las pruebas del 21-10 (precio −1 y JSON roto). Se dejan a propósito
  para la demo del indicador de errores.
- El admin de prueba sigue el producto 1. El simulador lo hace seguir solo, porque sin seguidores
  no hay a quién avisar.
- En el Learner Lab no hay IdP de Google (`google.env` vacío), así que el panel se probó en
  `localhost:5173` con el token del admin de prueba guardado a mano en `sessionStorage`.

## 21-10 · Catálogo: filtros plegables, presupuesto con piso y techo, y armador con vista previa — Martín

**La rama `fix/frontend-filtros-y-ficha` junta dos tandas.** La primera estaba en el árbol sin
commitear; la segunda salió de la revisión de las capturas del 20-10.

**Filtros.** El panel medía cerca de 2.000 px porque las ~60 tallas iban en una sola lista y el
orden solo conocía `XS…XXL`: `3XL`, `38 CL` y `8.5 US` caían al final o revueltas. Ahora cada
talla se clasifica en su sistema (ropa, calzado, CL, US), se ordena dentro de él y se agrupa en
secciones plegables con tope de diez y «+N». Los atributos parten cerrados salvo que traigan un
valor puesto. Lo elegido se lee en la cabecera de la sección aunque esté plegada.

**Presupuesto.** Había un solo control «hasta», así que pedir «prendas de más de $10.000» no era
posible. Se agregó `precioMinimo` al store, dos barras que se empujan entre sí si se cruzan,
atajos con cifras redondas y un chip de filtro formateado. El rango también acota las facetas, no
solo la lista.

**Catálogo.** Vista cómoda o densa (dos tarjetas en el teléfono, cuatro en pantalla ancha), orden y
vista recordados en el navegador, controles con la forma de las pastillas y una sola barra de
desplazamiento en el panel lateral. En móvil el panel se desplaza por dentro: antes estiraba la
página entera.

**Un bug de la vista densa.** Los resultados eran dos rejillas separadas por el anuncio; la segunda
reiniciaba las filas y la última quedaba con dos tarjetas y dos huecos. Ahora hay una sola rejilla y
el anuncio es un ítem que ocupa todas las columnas.

**Comparación.** Dejó de dibujar la silueta genérica: muestra la foto real, las tallas con stock (en
negrita las que tienen todas), los detalles que el nombre declara (manga, material, cuello, calce) y
cuánto más cuesta cada prenda respecto de la más barata. En el teléfono las columnas se deslizan.

**Armador.** Nueva tira «Así va quedando» con las capas elegidas en orden del cuerpo y huecos
discontinuos para lo que falta. El enlace plano de «Entra con Google» pasó a ser el botón oficial,
con el mismo flujo PKCE.

**Renombres y detalles.** «Comparador» pasa a «Catálogo» en el menú y los enlaces de vuelta, porque
al lado de «Comparar» se leían como el mismo botón. El corazón de deseados tiene color propio
(contraste 5,3:1 en claro y 5,2:1 en oscuro) y late solo al guardar. El carrusel desvanece sus
bordes, salvo en «Lo más reciente». Se corrigió una colisión de nombres: el aviso de datos usaba
`.aviso`, igual que el bloque vacío de las vistas, y heredaba su relleno de 64 px.

Verificación:

```bash
cd frontend
npm run humo
```

Build de producción correcto y **0 comprobaciones fallidas**. Se agregaron comprobaciones para el
piso de presupuesto, el chip formateado, el rango por ambos lados, la rejilla única y el scroll
interno del panel. Queda pendiente mirarlo en un teléfono real: acá solo se pudo comprobar con el
build y las pruebas.

Commits `d31141d` (primera tanda) y los de esta sesión. PR a `development` abierto, sin mergear. No
se desplegó: `--solo-front` queda para cuando se pida.

## 20-10 · Deuda técnica del EP3 antes de montar en la cuenta del dominio — Martín

- **notification-service explica sus fallos.** Lo mismo que product-service del 18-10: en el último
  intento recuperable el consumidor aparta el mensaje en la DLQ con `recuperable-agotado` y la causa
  de fondo, en vez de que SQS lo mueva sin atributos. Además, apagar el servicio ya no cuenta como
  intento agotado. Primer proyecto de pruebas del servicio, `notification-service.Tests` (8 pruebas).
- **El redrive ya no dice «0 movidos» si sí los movió.** El contador de la tarea nativa de SQS es
  aproximado. Ahora se cuenta la DLQ antes de mover y, si al final no queda ninguno de esos, se informa
  ese número. Probado con una SQS falsa que reproduce el caso (falla con el código anterior) y contra
  AWS real. De paso, la prueba real del ping-pong esperaba solo 2 s a que el listado mostrara la cola
  nueva y fallaba siempre; ahora espera como la del ciclo completo.
- **`dotnet format` en cero.** Los 18 avisos de espacios de `PersonalController.cs` y
  `ProductService.cs`, solo forma, sin cambiar lógica.
- **Documentos al día.** El índice de `AGENTS.md` tenía conteos viejos (`TAREAS.md` decía 401 líneas y
  tiene 291) y no listaba `MONTAR-EN-TU-CUENTA.md` ni el README de notification-service; las
  comprobaciones del `README.md` suman `dotnet test`.
- **Pendiente:** avisar a Orion, porque se tocó notification-service y `PersonalController.cs`, que son
  de su carril. El outbox de SNS y el consumidor de auditoría siguen como deuda del informe.

## 18-10 (tarde) · EP3: pruebas de punta a punta en la EC2 (T13), DLQ con diagnóstico y vista del panel (T12) — Martín

**T06 cerrada al pie de la letra.** Con el PR #40 mergeado, `./tools/desplegar.sh --solo-backend`
**sin `RAMA=`** levantó `development` en la EC2 con la mensajería encendida y la base intacta.

**T13 · `tools/probar-mensajeria.py`**: todo por el API Gateway, con tokens reales de Cognito (usuarios
de prueba de `crear-cognito.sh`). Evidencia en `docs/evidencia/mensajeria-ec2-*.log`; la última corrida,
**41/41**:

| Bloque | Qué se comprobó |
|---|---|
| A · panel (T11) | 401 sin token, 403 sin el grupo admin, 200 con el admin; colas, tópicos con filtros y topología sincronizada; 7 casos inválidos con `{ campo, error }`; 5 conflictos 409; crear y borrar una cola con su DLQ y una regla con filtro |
| B · flujo 1 con error | `price: -1` por `/ingesta` → DLQ al primer intento con `definitivo`, visto desde el panel |
| C · flujo 2 en la EC2 | el admin sigue un producto real; baja de 5 % → solo auditoría y **ningún** correo; baja de 30 % → auditoría y el correo «Bajó 30 %» de notification-service **en la EC2**; precio restaurado |
| D · redrive desde el panel | responde en ~2 s y lo inválido vuelve a la DLQ con una falla nueva |
| E · último intento | postgres detenido: 4 reintentos y en el 5.º el consumidor lo aparta con `recuperable-agotado` |

**Lo que encontraron las pruebas, y quedó corregido:**

- **Ping-pong del redrive.** La primera corrida (`mensajeria-ec2-20261018-185842.log`) dio 503: con el
  consumidor corriendo, lo que el redrive devolvía y seguía siendo inválido volvía a la DLQ como mensaje
  nuevo, y el movimiento manual lo volvía a mover: 1000 movimientos con 2 mensajes, 50 s, y el API
  Gateway corta a los 30. Ahora solo se devuelve lo que llegó a la DLQ antes de apretar redrive
  (`SentTimestamp`), con topes de tiempo, y si la tarea nativa sigue en curso no se mueve a mano. Hay
  una regresión contra AWS con un «consumidor que rechaza todo».
- **DLQ sin diagnóstico.** Lo que agotaba los reintentos lo movía SQS, sin atributos. Ahora el
  consumidor lo aparta en el último intento (el máximo se lee de la `RedrivePolicy` de la cola) con
  `recuperable-agotado` y la causa de fondo: EF envuelve el error de Npgsql en un «transient failure»
  genérico que no explica nada. En notification-service queda como antes (lo mueve SQS): se le propone
  a Orion.
- Detalle pendiente: cuando la tarea nativa de redrive termina, su contador aproximado puede decir
  «0 movidos» aunque los mensajes sí volvieron.

**T12 · vista del panel** (rama `feature/vista-admin-mensajeria`): `/admin/mensajeria` en el frontend,
solo para el grupo admin. Build y humo limpios; la capa de servicios se probó contra el gateway real
(listado, 400 por campo, 409 con su mensaje, DLQ con diagnóstico, 403 sin el grupo). El login del sitio
es solo con Google: para verla, la cuenta de Google del admin tiene que estar en el grupo `admin` de
Cognito.

## 18-10 · EP3: consumidor del flujo 1 y administración de mensajería (T05 y T11) — Martín

**T05 · consumidor de ofertas.** product-service ahora vacía `cep-ofertas-crudas`:
`ConsumidorOfertas` lee con long polling y `ProcesadorOfertaCapturada` valida el sobre y guarda con
`UpsertProductAsync`, sin tocar ese método. Tres salidas, igual que el consumidor de Orion: se
borra al guardar; JSON roto, `type`/`version` desconocido o una oferta que no pasa la validación del
catálogo van a la DLQ con los 6 atributos de diagnóstico; una base caída o un timeout dejan el
mensaje en la cola para que SQS lo reintente. Un detalle que salió probando: `TaskCanceledException`
(el timeout de una consulta) hereda de `OperationCanceledException`, y el primer filtro lo dejaba
escapar en vez de tratarlo como recuperable. 13 tests nuevos, incluido el duplicado.

**T11 · administración de mensajería.** Panel en el gateway bajo `/api/admin/mensajeria`, solo para
el grupo `admin`: consultar colas (con mensajes, DLQ e intentos), tópicos con sus filtros y lo que
hay en una DLQ; crear y borrar colas, tópicos y suscripciones; redrive; y aplicar o comparar
`topologia.json` desde la aplicación. El panel también deja mirar `cep-auditoria-precios` (la copia
sin filtro de todos los cambios), pero no una cola con DLQ: para SQS mirar es una recepción y
sumaría intentos hasta mandar mensajes sanos a la DLQ. La primera versión era un servicio aparte; se movió al
gateway para seguir la tarjeta y no sumar un contenedor ([ADR-032](adr/032-administracion-de-mensajeria.md)).
Toda la lógica de AWS queda detrás de `IAdministradorMensajeria`, con una implementación en memoria
para programar la vista (T12) sin cuenta de AWS. 87 tests en `gateway.Tests`: reglas, validación y
25 por HTTP contra el gateway completo (401, 403, 400 con `{ campo, error }`, 409 y la topología
aplicada dos veces sin duplicar nada).

**Probado en local:** la solución compila sin warnings y pasan los 111 tests (87 del gateway y 24
de product-service, incluidos los 11 de Orion). Las imágenes del gateway y de product-service se
construyen; el gateway en Docker responde `/health` 200 y 401 sin token; product-service sin URL
de cola registra «desactivado», y con URL pero sin credenciales sigue arriba reintentando cada 5 s.

**Probado contra AWS (cuenta `731705830984`, mismo día).** Con product-service y
notification-service corriendo en local contra las colas, el tópico y el Cognito reales:

| Qué | Resultado |
|---|---|
| Permisos del `LabRole` (simulación IAM) | `cognito-idp:ListUsers`, `sqs:StartMessageMoveTask` y todo lo de SQS/SNS que usamos: *allowed* |
| T05 · sobre válido por `POST /ingesta` con el token del scraper | en `GET /api/products` a los **4 s**, cola en 0 |
| T05 · `price: -1` | a la DLQ en el **intento 1**, con los 6 atributos de diagnóstico |
| T05 · postgres detenido | 5 reintentos (warning cada uno) y a la DLQ, ~5 min |
| T05 · redrive con la base de vuelta | la oferta se guarda |
| T07 (Orion) · baja 5% / baja 25% / agotada / repuesta | auditoría recibe las **4**, notificaciones **3** (la de 5% no pasa el filtro); contrato v1 crudo con `discountPct` 5 y 25 |
| T08 + T09 (Orion) | `/health` y migración; `ListUsers` resuelve el correo; 3 correos (modo log), 3 filas en `notificaciones_enviadas` |
| T09 · duplicado del mismo `eventId` | «Ya se le había avisado… se salta», sigue en 3 |
| T09 · JSON roto | a `cep-notificaciones-dlq` con `error-type=definitivo` |
| T11 · `MensajeriaAwsRealTests` (`CEP_AWS_REAL=1`) | 2/2: topología real sincronizada y aplicada «sin cambios»; ciclo crear → filtro → mirar DLQ → redrive → borrar |

Lo que solo apareció contra AWS, y quedó corregido en la T11: (1) `ListQueues` tarda en mostrar
una cola recién creada, así que la existencia se pregunta con `GetQueueUrl`; (2) mirar una cola con
`VisibilityTimeout=0` puede devolver **el mismo mensaje varias veces** en una lectura; (3) el
redrive nativo calcula cuánto mover con el contador **aproximado** de la DLQ: si alguien la acaba
de mirar, termina `COMPLETED` con 0 de 0. Ahora se espera la tarea y, si movió 0, se sigue a mano.
También se confirmó que mirar una cola **con** DLQ sube el contador de intentos hasta mandar los
mensajes a la DLQ: por eso el panel no lo permite.

**T04 · el scraper publica en la cola.** Nuevo `IngestaColaClient` con el mismo método que el
cliente de Product Service: el barrido no se tocó y `MODO_PUBLICACION=cola|http` (http por defecto)
decide. Token de Cognito reutilizado, un reintento ante 401, 3 ante 5xx/timeout y un 4xx que solo
hace fallar esa prenda. 20 tests nuevos (186 en el scraper, `ruff` y `mypy` limpios). Probado contra
AWS: con la configuración real publicó por `/ingesta` y el sobre llegó a la cola con el contrato v1.

**Despliegue (avance de la T06).** Revisando cómo llegaría todo a la EC2 apareció que
`desplegar.sh` **no pasaba ninguna variable de la mensajería** al `.env` del servidor: allá el
consumidor de ofertas, la publicación en SNS y el consumidor de correos habrían quedado apagados,
sin un solo error. Ahora lee `mensajeria.env` (si falta, corre `crear-mensajeria.py` o se detiene),
suma `COGNITO_USER_POOL_ID`, deja el scraper en modo cola con su client y el correo en `log` salvo
que se despliegue con `CORREO_MODO=smtp SMTP_USUARIO=... SMTP_CLAVE=...`. Se comprobó también que
las credenciales vacías del compose no rompen la cadena del SDK (cae al rol de la máquina).

**T06 · flujo 1 desplegado y probado en la EC2.** `crear-api-gateway.sh` creó las rutas
`/api/admin/mensajeria` (y se le corrigió un error que repetía en cada corrida al actualizar la
integración de `/ingesta`). `RAMA=feature/admin-mensajeria ./tools/desplegar.sh --solo-backend`
levantó los 6 servicios con la mensajería encendida: ConsumidorOfertas y ConsumidorNotificaciones
escuchando, el panel en modo `aws` y el scraper en modo cola. Las dos pruebas de la tarjeta, con
`tools/probar-flujo1.sh`, que deja la evidencia en `docs/evidencia/`:

| Prueba | Resultado |
|---|---|
| Normal (`flujo1-normal-20261018-050136.log`) | barrido de Falabella: 353 prendas publicadas por `/ingesta` y 353 guardadas por el consumidor, la cola en 0 y el sitio, medido por el API Gateway, pasa de 0 a 224 productos |
| Resiliencia (`flujo1-resiliencia-20261018-050338.log`) | con product-service detenido el barrido publica igual: **352 prendas esperando en la cola**; al encenderlo baja a 0 en ~30 s y vuelven a ser 353 publicadas = 353 guardadas, sin pérdidas |

Las cifras no son iguales y no es un error: 353 es lo que se publicó y se guardó, 325 son las
ofertas distintas en la base (un mismo SKU aparece en varias páginas del listado y la segunda vez
se actualiza) y 224 los productos del sitio (agrupa las ofertas de un mismo modelo). La tarjeta
decía «un scrape de 20»: `limite=20` cuenta páginas del listado, no prendas. En la de resiliencia
una prenda se procesó en su intento 2: SQS entrega «al menos una vez» y guardarla dos veces no
cambia nada (idempotencia, ver MENSAJERIA.md › Duplicados).

**Pendiente:** el `.http` de la T11 con un token de admin contra el API Gateway; el `.http` de la T11 con un
token de admin; el correo por SMTP de verdad (T10). En la plantilla de `oferta.agotada` de
notification-service dice «cuando dejo de estar disponible» (falta la tilde de *dejó*): se le avisa
a Orion.

## 17-10 · EP3: flujo 2 completo — avisos por correo (T07, T08, T09) — Orion

Quedó armado el **flujo 2** de la mensajería (producto que baja de precio, se agota o se repone →
correo a quien lo sigue), en dos PR a `development`:

**notification-service (T08 + T09) — PR #37.** Servicio nuevo en .NET 10 que consume la cola
`cep-notificaciones`: valida el sobre, pregunta a product-service quién sigue la prenda
(`GET /internal/personal/seguidores/{id}`, los «deseados»), traduce cada `sub` a su correo con
Cognito `ListUsers` (caché 1 h, salta los no verificados) y manda un correo por persona con
MailKit, **sin repetir** (tabla `notificaciones_enviadas (event_id, owner)`). Esquema propio
`notification`; en local, sin AWS, va en modo log. Decisiones en
[ADR-030](adr/030-notification-service.md): no toca el esquema `product`, y una dirección
rechazada (SMTP 550/553) se salta esa persona en vez de mandar todo el evento a la DLQ.

**product-service productor (T07) — PR #38.** En `UpsertProductAsync`, **solo cuando la oferta ya
existía**, compara precio y `active` antes de `ApplyOffer` y publica `precio.bajo`,
`oferta.agotada` u `oferta.repuesta` en el tópico SNS `cep-cambios-oferta`, con los atributos
`type` y `discountPct` que el filtro de la cola necesita. El negocio habla con
`IPublicadorEventos` (impl. SNS o log), nunca con el SDK. La decisión de qué eventos corresponden
quedó en una **función pura** (`DecisionCambiosOferta.Evaluar`). La publicación va **después** de
guardar y es best-effort: si SNS falla, el precio queda bien y solo se pierde el aviso; está
anotado como limitación en el [ADR-029](adr/029-mensajeria-sqs-sns.md) y detallado en el nuevo
[ADR-031](adr/031-publicacion-cambios-oferta.md) (el costo: sin outbox, ese aviso no se recupera).

**Dos cosas más.** (1) Al mergear `development` en la rama de la T07, la resolución del conflicto
borró sin querer la sección del notification-service en `.env.example`; se restauró para no
quitarle esas variables a `development`. (2) Entró el **primer proyecto de tests .NET del repo**
(`product-service.Tests`, xUnit): cubre la función pura (baja, subida, agotada, repuesta,
baja+agotada, sin cambios y el redondeo de `discountPct`) y el borde de «oferta nueva no publica»,
todo sin BD ni AWS. 11 casos en verde; la solución compila sin warnings.

Nueva dependencia: `AWSSDK.SimpleNotificationService` en product-service. Pendiente: probar el
flujo punta a punta con `docker compose up` (quedó sin correr acá) y confirmar en la EC2 que
`LabRole` permite `cognito-idp:ListUsers`.

## 14-10 · EP3: sondeo de Academy, puerta /ingesta y cuenta nueva (T02 y T03) — Martín

Learner Lab nuevo: la cuenta pasó a ser **`731705830984`** (la anterior era `116813910999`).
La receta de cuatro comandos de [MONTAR-EN-TU-CUENTA](OPERAR-EN-AWS.md) se corrió entera
sobre la cuenta vacía y dejó el sistema arriba sin pasos a mano: `/health` y `/productos` 200 y
`/seguimiento` 401 a través del API Gateway.

**T02.** El primer sondeo dijo «opción B», y era un **falso negativo**: la aws cli v1 de Academy
devuelve la URL de la cola como `https://queue.amazonaws.com/...`, la cli la acepta, pero la
integración `SQS-SendMessage` responde **400** y el mensaje no llega. Con la URL
`https://sqs.us-east-1.amazonaws.com/...` responde 200 y el mensaje llega: **opción A
confirmada**, LabRole ya confía en `apigateway.amazonaws.com`. Desde la EC2, el 587 de
`smtp.gmail.com` sale y negocia STARTTLS; el 25, bloqueado. Queda en el
[ADR-029](adr/029-mensajeria-sqs-sns.md), que pasa a aceptado.

**T03.** `crear-api-gateway.sh` crea `POST /ingesta` directo a `cep-ofertas-crudas` con el scope
`ingesta`, sin pasar por la EC2. Si las colas no existen, el script corre
`crear-mensajeria.py`: montar una cuenta sigue siendo los mismos cuatro comandos. Medido: sin
token 401, con el token del scraper 200 y el sobre en la cola; una segunda corrida no duplica
nada.

Pendiente: T04 (el scraper publica en la cola) y T05 (el consumidor en product-service). La EC2
de la cuenta nueva quedó encendida para seguir con ellas.

## 23-09 · Catálogo y armario personal — Orion

Trabajo en `feature/catalogo-personal-y-capas`, desde `development` actualizado (incluye el
PR #30). Se conservó la paginación reciente y se agregaron filtros acotados, imágenes sin
recorte, menos reactividad profunda y selector de prendas por tandas. Marcas genéricas
normalizadas sin fusionar automáticamente prendas sin marca entre tiendas. Scraper y backend
rechazan/ocultan prendas infantiles; polerones y chalecos tienen capa intermedia propia.

Deseados y outfits por usuario persisten en PostgreSQL mediante migración, con propietario
derivado del JWT en el BFF. Se añadieron «Mi armario», guardado desde fichas/tarjetas y armador,
rutas protegidas de AWS y ruta SPA de S3. Detalle: [operación](HISTORIA.md) y
[ADR-028](adr/028-armario-personal-y-politica-catalogo.md).

Verificación: compilación de frontend y servicios; pruebas de humo sin fallos, 166 pruebas
Python aprobadas y 7 omitidas; pruebas HTTP sobre PostgreSQL desechable para concurrencia,
aislamiento y rutas sin token, y pruebas de cambios de sesión/capas en el frontend.
Revisión visual en Chromium a 390 y 1440 px. La herramienta de limpieza se probó con registros
sintéticos: conservó la prenda juvenil, eliminó la infantil tras respaldar y permitió restaurarla
en otra base de prueba.

Una consulta **solo de lectura** al catálogo público encontró 7.504 productos, 122 candidatos
infantiles y 18 marcas genéricas. No se eliminó ni desplegó nada en producción. Quedan: revisión
de candidatos y limpieza respaldada en la base correcta, despliegue, prueba real de Google/Cognito
y autorización para commits/PR. El catálogo aún se descarga completo; facetas/paginación de
servidor quedan como trabajo posterior de escalabilidad.

> Tres líneas por persona por semana. No es burocracia: en septiembre **esta bitácora es el
> informe**, ya escrito. Escribir esto al final de cada semana cuesta 5 minutos y ahorra un día.
>
> Formato: qué hice · qué me costó · qué queda pendiente.

---

## Semana 0 · 19–23 ago 2026

**19-08 · Sesión de planificación (los tres)**

Se armó toda la documentación del proyecto y se creó el repositorio. Todavía no hay código.

Decisiones tomadas:

- **El scraper ingesta por el API Manager**, no directo a RabbitMQ. Antes el token de client
  credentials no se validaba en ninguna parte; ahora entra por `POST /ingesta` con su scope.
- **3 capturas diarias** en vez de una, para que el gráfico de historial tenga densidad.
- **`oferta` es estado (upsert) y `precio_historico` es serie (append)**. Estaba ambiguo y
  habría reventado en septiembre.
- **Idempotencia** con hash + `UNIQUE`, porque RabbitMQ entrega at-least-once.
- **Matching con `pg_trgm`** y umbrales 0.85 / 0.60, más tabla de candidatos para revisión manual.
- **Grupos de Cognito** además de scopes: sin roles no hay 403.
- **Code freeze el 10-sep**, tres días de margen antes de la entrega del 13.
- El repo queda en la cuenta `Martinnnn-ops` (la de los runners de Actions).

**Martín —**

**Orion —**

Se configuró el monorepo Maven con Java 25 y se agregó `catalog-service` como primer módulo
Micronaut, todavía sin lógica de negocio. Se verificó el arranque y el endpoint de salud. Queda
pendiente que el integrante implemente el primer endpoint del catálogo con acompañamiento.

Se decidió usar Semantic Versioning independiente por microservicio porque cada uno avanzará a
un ritmo distinto. `catalog-service` parte en `0.1.0`; la versión del agregador Maven no se hereda
como versión del servicio.

Se habilitó el versionado de rutas mediante headers y se crearon los primeros controladores del
catálogo junto con su DTO de respuesta. Queda pendiente incorporar pruebas HTTP antes de conectar
la capa de aplicación y la persistencia.

**Panditax —**

**Del equipo:** (decisiones tomadas, cosas que cambiaron respecto del plan)

---

## Semana 1 · 24–30 ago 2026

**Martín —** (27-08) Levanté el scraper de arranque de Sparta, que estaba pendiente desde
la semana 0. Está en `tools/scraper-rapido/` y ya corre solo: 3 capturas diarias por timer de
systemd (08:00 / 15:00 / 22:00), guardando el JSON crudo comprimido en `capturas/`. Primera
captura real el 27-08 a las 18:40: **2.088 productos, 908 zapatillas, 9.125 filas talla-stock,
142 KB, 110 segundos**. Lo que me costó fue descubrir que la query que teníamos estaba mal
(ver abajo). Queda pendiente migrarlo a Lambda y que el crudo suba a S3.

También levanté el **entorno local con Docker** (Postgres + RabbitMQ) y de paso validé los
umbrales del matching contra datos reales — los dos detalles están más abajo.

(30-08) 🟢 **Cerrado el bloqueador que llevaba una semana abierto: Cognito SÍ se puede en la
cuenta de AWS Academy.** Se sondeó con `tools/verificar-aws-academy.sh`, que crea todo lo que
el EP1 necesita y después lo borra: User Pool, dominio de Hosted UI, resource server con el
scope `ingesta`, app client OIDC con Authorization Code, grupos, HTTP API y **el JWT Authorizer
de API Gateway apuntando al pool**. También se confirmó permiso para lanzar EC2 (`--dry-run`) y
que existe `LabRole`. **El plan B de Keycloak queda descartado.** Razonamiento en el
[ADR-006](adr/006-cognito-como-idaas.md), evidencia cruda en `docs/evidencia/`.

Escribí además [`DESPLIEGUE.md`](HISTORIA.md), que faltaba: los 8 pasos en orden para el
6 de septiembre y las restricciones del Learner Lab que muerden en el despliegue (la sesión
caduca y **apaga las EC2 solas**, la IP pública cambia al reiniciar, no se pueden crear roles
de IAM). El paso 7 —actualizar las redirect URIs de Cognito al dominio de CloudFront— es el que
hunde entregas: el login sigue andando en local y muere en producción sin decir por qué.

(30-08) 🔑 **Identidad levantada de verdad, no en papel.** `tools/crear-cognito.sh` deja el
User Pool completo y es **idempotente**: si algo ya existe lo reutiliza, así que se puede correr
las veces que haga falta. Crea Hosted UI, resource server con el scope `ingesta`, el app client
del frontend (sin secreto, PKCE), el del scraper (con secreto, `client_credentials`), los grupos
`admin` y `usuario`, y dos usuarios de prueba para poder mostrar el 200 y el 403 en la defensa.
Los identificadores quedan en `cognito.env`, que no se versiona porque cambia con cada cuenta.

**Verificado end-to-end, no solo creado:** el JWKS entrega 2 llaves RS256, el Hosted UI responde
200, y pedimos un `access_token` real por `client_credentials` que llegó con el scope correcto.
De paso quedó **medida** la particularidad del `aud`: el `access_token` de Cognito **no trae ese
claim**, trae `client_id`. Si el BFF usa la validación de audiencia por defecto de cualquier
librería, **rechaza todos los tokens buenos** y el mensaje de error manda a buscar al lugar
equivocado. Está escrito en [`IDENTIDAD.md`](HISTORIA.md) §5.

**Hueco de diseño encontrado y cerrado.** El diagrama pone el backend en subred privada, pero
API Gateway vive *fuera* de la VPC: sin **VPC Link** no llega, y el VPC Link necesita un
balanceador detrás. No estaba en ningún documento. Se decidió la arquitectura completa
([ADR-015](adr/015-red-privada-con-vpc-link.md)) con dos ajustes: **ALB en vez de NLB** (sus
health checks dicen qué falla; con NLB es un 503 mudo) y **NAT y ALB se crean y se destruyen por
script**, porque son las únicas piezas que cobran por hora aunque el laboratorio esté cerrado.

Documentos nuevos: [`IDENTIDAD.md`](HISTORIA.md) — cómo funciona el login, qué valida el BFF,
cómo se replica y cómo se defiende en el oral.

⚠️ **Lo que falta y es todo lo que puntúa:** el frontend **no existe** (0 líneas, y es el 60%) y
el `gateway` son 2 `.java` sin nada de seguridad (el 40%). La infraestructura no reparte puntos
por sí sola.

**Orion —**

**Panditax —**

**Del equipo:**

### 🐳 Entorno local levantado (27-08)

Ya existe `docker-compose.yml` con **Postgres 16 + RabbitMQ 3.13**. Antes no había ninguno, así
que cada uno iba a terminar armándose el suyo distinto.

Tres cosas que quedaron decididas al escribirlo:

- **El mismo archivo va a correr en la EC2** ([ADR-008](adr/008-ec2-docker-compose.md)). Todo sale
  del `.env`, así que cambiar de entorno es cambiar ese archivo, no el compose.
- **Los puertos escuchan solo en `127.0.0.1`**, no en toda la red — un Postgres de pruebas
  expuesto en el wifi de la universidad es un problema evitable.
- **Las contraseñas usan `${VAR:?}`**: si faltan, el compose no arranca. Es preferible que falle
  a que levante con una contraseña adivinable.

`infra/postgres/init/` crea solo, al primer arranque, las extensiones `pg_trgm` y `unaccent` y
los esquemas `catalog` y `price` (ADR-010).

**Lo que se decidió NO hacer:** pre-declarar los exchanges y colas con un `definitions.json`.
Habría dejado la DLQ visible en el panel desde el día uno —que es evidencia del EP2— pero
RabbitMQ obliga a declarar también el usuario, con su contraseña hasheada **dentro de un archivo
versionado**, y eso choca con el acuerdo de cero secretos en el repo. La topología la declara
Micronaut con `@RabbitClient` y `@RabbitListener`, como ya decía `ARQUITECTURA.md §6`.
Consecuencia: **las colas aparecen en el panel recién cuando los servicios se conecten.**

### ⚠️ Umbrales de matching: primero los di por buenos, y no lo son

Al levantar el entorno probé los umbrales de `PLAN.md §5` (0,85 acepta / 0,60 rechaza) con un
par de nombres que escribí a mano, dieron 0,87 y 0,57, y los di por validados. **Eso estaba
mal: un par inventado por uno mismo no valida nada.**

Al cargar los 1.496 modelos reales de la captura y consultarlos de verdad:

| Comparación | Similitud | Debería | Pasa |
|---|---|---|---|
| `574 Negra` vs `574 Negro` — **el mismo zapato** | **0,696** | aceptar | revisión manual |
| `574 Negra` vs `515 Negra` — **otro zapato** | **0,660** | rechazar | revisión manual |

El correcto queda **bajo** el 0,85 y el equivocado **sobre** el 0,60. Entre acertar y
equivocarse hay **0,04**, y con nombres reales casi todo cae en la banda gris.

**La causa:** el número del modelo (574 vs 515) es lo único que distingue dos zapatillas
completamente distintas, y el trigrama casi no lo pesa porque comparten todas las demás
palabras.

**Qué hay que hacer:** el matcher no puede ser solo trigrama sobre el nombre completo. Tiene que
**extraer el número/token de modelo y exigir coincidencia exacta**, y usar el trigrama solo para
el resto. Si no, el comparador va a emparejar zapatos distintos con total seguridad — que es
exactamente lo que el producto promete no hacer.

`unaccent` sí funciona: «Básquetbol» vs «Basquetbol» da 1,00.

### 🧱 Los 4 módulos del backend, creados (27-08)

El monorepo tenía **solo `catalog-service`**. Se agregaron los tres que faltaban —`gateway`
(el BFF), `price-service` y `scraper-service`— siguiendo exactamente la misma estructura, para
que nadie tenga que inventar andamiaje en la semana crítica.

Cada uno arranca, responde `/health` y **nada más**: la lógica se escribe encima. Los cuatro
compilan y pasan sus tests con Java 25 (`./mvnw clean test`, exit 0).

| Módulo | Puerto | Qué será |
|---|---|---|
| `gateway` | 8080 | El BFF: valida el JWT y arma las respuestas de la pantalla |
| `catalog-service` | 8081 | Modelos, variantes y matching |
| `price-service` | 8082 | Ofertas, historial y descuento real |
| `scraper-service` | 8083 | Trae los datos; el único que sale a internet |

Los puertos salen del `.env`, así que el mismo build sirve en local y en la EC2. Verificado:
`PRICE_PORT=9099` levanta el servicio en 9099. `/health` responde sin autenticación a propósito
—lo consulta el healthcheck del contenedor, que no tiene token ni debería tenerlo—.

Se dejó a propósito **sin controladores de relleno**: endpoints que no hacen nada son código que
igual hay que mantener. `/health` ya prueba que el servicio está vivo.

### 🗄️ Modelo de datos validado contra datos reales (27-08)

`PLAN.md §4` decía que el modelo era *"propuesta base"* y que había que contrastarlo con datos
reales antes de escribir migraciones. Hecho: se cargaron los **2.088 productos** de la captura
en Postgres (1.496 modelos, 7.355 variantes y ofertas) y se ejecutaron las consultas del
producto. **Cinco cosas del modelo no aguantaron.**

**1. El style code no puede ser el mecanismo de comparación.** Solo Nike lo trae extraíble
(86/89 = 96%); New Balance, Adidas, Joma, Asics y Puma dan **0%**, y Hites no publica ninguno.
Pasa a ser `NULL`able. Donde sí está hace bien su trabajo: agrupa los colores de un modelo
—verificado con las 4 versiones del Nike G.T. Hustle Academy, que comparten `FJ7791`—.

**2. La talla no es un campo simple.** Sparta usa **20 atributos distintos** de talla. Se parte
en `talla_original` + `escala` + `talla_valor`.

**3. La escala no se puede deducir del nombre del atributo.** Sparta llama a un campo
`talla_us_nb_mujer` pero le mete 37.5, 38, 40 — números **europeos**. De 3.993 variantes
rotuladas "US", **3.229 traían numeración europea**. Hay un `CHECK` de rango por escala que
impide que ese error entre en silencio.

**4. Sparta nunca devuelve productos agotados.** Los 2.088 vienen `IN_STOCK` sin excepción:
Magento los filtra. El campo `stock` no sirve. Se reemplaza por `vista_en` + `activa`, porque
**la señal de agotado es la ausencia**. Ya se ve en los datos: entre dos capturas New Balance
pasó de 857 a 856.

**5. Faltaba dónde poner lo que aún no se matchea.** Se agrega `producto_tienda`: sin ella, un
producto que llega de una tienda y todavía no se sabe a qué modelo corresponde habría que
descartarlo.

**Lo que sí resistió:** la clave del upsert. `(variante, tienda)` es única en las 9.125 filas,
sin un solo duplicado. Y la separación `oferta` = estado / `precio_historico` = serie.

Las restricciones se probaron insertando datos malos a propósito: las cuatro rechazan (talla US
con número europeo, talla ALFA con número, precio 0 —hay 3 en la captura— y style code repetido).

Migraciones en `infra/db/`, con su README.

### 👕 Alcance aclarado (27-08)

**Zapatillas es el recorte del EP1, no la ambición del proyecto.** El plan es cubrir ropa además
de calzado. Se parte por calzado porque es donde el problema se resuelve bien —hay style code de
fábrica y el mismo modelo se vende en varias tiendas—; en vestuario los nombres son genéricos y
el matching es el problema difícil. El modelo de datos ya soporta ropa sin cambios.

### 🔴 Hallazgo del 27-08 · la búsqueda de Sparta no filtra por marca

`PLAN.md §3` da por verificado que se consulta Sparta buscando por texto. **No sirve.** La
búsqueda de Magento es difusa y el término "zapatillas" domina al de marca: pedir
`"nike zapatillas"` devuelve **1126 resultados de los cuales solo 24 son Nike**, y las cuatro
marcas devolvían prácticamente los mismos productos. La primera captura trajo 4.000 productos
que en su mayoría eran ruido.

Se corrige filtrando por el atributo `gral_marca`, cuyos ids salen de `aggregations`:

```graphql
products(filter: {gral_marca: {eq: "21"}}, pageSize: 50, currentPage: 1)
```

Con el filtro puesto, el catálogo real de Sparta es este — y **contradice la elección de marcas
del MVP**:

| Marca | id | Productos | Zapatillas |
|---|---|---|---|
| New Balance | 21 | 857 | 408 |
| Adidas | 3 | 752 | 239 |
| Joma | 18644 | 267 | 138 |
| Asics | 447 | 96 | 95 |
| Nike | 23 | 89 | 24 |
| Puma | 30 | 27 | 4 |

**Sparta es una tienda de New Balance y Adidas.** Nike y Puma son marginales, y varios de los
89 productos Nike son balones y accesorios, no calzado.

Esto importa porque `PLAN.md` usa Nike como **ejemplo canónico del style code** (`HV9774`) y
declara a Sparta *fuente canónica del catálogo*. Si el catálogo canónico se arma sobre Nike,
se arma sobre 24 productos. **Decidido el 27-08, pero NO como decía acá.** Mirar solo el volumen de Sparta era el criterio
equivocado para un comparador: una marca sirve solo si está en **las dos** tiendas. La faceta de
marcas de Hites muestra que **Joma y Asics no las vende**, así que aportarían cero pares
comparables pese a tener catálogo en Sparta. Las 4 del MVP vuelven a ser **New Balance, Adidas,
Nike y Puma** — las originales del plan, ahora por un motivo medido.

Dato a favor de la tesis del proyecto: **1.277 de los 2.088 productos tienen descuento activo
declarado**, con casos de 38% — o sea que hay bastante que contrastar contra el mínimo
observado en cuanto el historial tenga fondo.

### 🟠 Hallazgo del 27-08 · el endpoint de Hites que teníamos ya no responde

`PLAN.md §3` registra `Search-UpdateGrid` como la vía para Hites, verificada el 18-08. Hoy ese
endpoint devuelve **500** para cualquier búsqueda. La home sí responde 200.

Alternativa que sí funciona, probada hoy: la página de búsqueda normal
`https://www.hites.com/search?q=<término>`, que trae la grilla con 24 productos por página y
sus `data-pid`. Sigue siendo parseo con Jsoup, así que el plan del Integrante 3 no cambia de
forma — cambia la URL. Conviene confirmarlo antes de escribir el adaptador.

### ⏱️ Sobre el historial

El scraper arrancó el **27-08**, no en la semana 0. Al code freeze del 10-09 el historial va a
tener **14 días**, no las 3 semanas que pide el checklist final de `TAREAS.md`. No es
recuperable; conviene ajustar esa casilla y decirlo en el informe en vez de que lo pregunten.

---

## Semana 2 · 31 ago – 6 sep 2026

**Martín —**

(03-09) 🐛 **El script de Cognito prometía una reproducibilidad que no tenía.** Al ir a sacar un
token de usuario para probar el BFF, el login falló con `NotAuthorizedException: Incorrect
username or password` — con los dos usuarios de prueba, y también usando el UUID en vez del
correo, así que no era un problema de alias.

La causa estaba en `crear_usuario()`: el `admin-set-user-password` vivía **dentro del `else`**,
o sea que solo se ejecutaba al crear el usuario. Pero si no se le pasa `CLAVE_PRUEBA`, el script
**genera una clave nueva en cada corrida y la escribe en `cognito.env`**. Como el script es
idempotente a propósito y se corrió varias veces, el archivo terminó declarando una clave que
nunca se le aplicó a nadie.

Es peor de lo que suena: [`IDENTIDAD.md`](HISTORIA.md) §6 promete que en una cuenta de AWS
nueva basta correr el script y entrar. Eso **no era cierto** desde la segunda corrida, y se
habría descubierto el día que la cuenta del Learner Lab se resetee — probablemente durante la
semana de la entrega. Arreglado: la clave y el grupo se aplican siempre, exista o no el usuario.
Verificado corriendo el script completo sin `CLAVE_PRUEBA` (el escenario que fallaba) y entrando
con la clave recién generada.

(03-09) 🧪 **App client `pruebas`, para poder testear sin navegador.** El client `frontend` solo
acepta `ALLOW_USER_SRP_AUTH`, y el SRP no se puede hacer desde la CLI: habría que calcular el
`SRP_A` a mano. Sin un client aparte, conseguir un token de usuario obliga a pasar por el Hosted
UI, y **un test automatizado no puede hacer eso**.

Se creó uno nuevo en vez de habilitarle el flujo al `frontend` por dos motivos:
`update-user-pool-client` **reemplaza** la configuración entera —todo campo que no se le pase
vuelve al valor por defecto, así que se habrían perdido las callback URLs— y porque el argumento
que se defiende en el EP1 es que el frontend es PKCE puro y sin secreto. El client `pruebas` no
tiene OAuth ni callbacks: solo sirve para pedir un token con usuario y clave.

**Consecuencia de diseño para el BFF, encontrada antes de escribir el código:** el token de ese
client trae **otro `client_id`**. Como Cognito no manda `aud` en el `access_token` y hay que
validar contra `client_id`, validar contra un solo valor haría que los tests con tokens reales
dieran **401 contra nuestra propia validación**. El script ahora emite
`COGNITO_CLIENT_IDS_VALIDOS` con los dos.

Reconfirmado de paso, con un token medido hoy: `aud` no viene, `token_use` es `access` y
`cognito:groups` trae `["admin"]` — o sea que el 403 de la demo se puede construir de verdad.

**Orion —** Reemplazó `catalog-service` por `product-service` porque ahora el módulo será dueño
de catálogos y productos. Dejó una estructura simple (`model`, `repository`, `service` y
`controller`), SQLite con Flyway y tres versiones reales de los listados HTTP. No se agregaron
DTO todavía; el costo de ese acoplamiento quedó anotado en ADR-016 para no olvidarnos después.

**Panditax —**

**Del equipo:**

---

## Semana 3 · 7–13 sep 2026 · Entrega EP1

### 09-09 · Tarjetas acotadas, taxonomía corporal y nuevas tiendas

**Orion —** Se corrigió de extremo a extremo la fila que crecía hasta el alto de todos los SKU
de Falabella. Product Service conserva las variantes en PostgreSQL, pero responde una oferta por
tienda —la disponible más barata, con tallas unificadas—; el adaptador Vue repite la consolidación
como defensa durante despliegues graduales y cada tarjeta resume como máximo cuatro tiendas.

La taxonomía separa ahora `category`, `bodyArea` y `gender`. Quedaron comprobados en vivo un
gorro de mujer (`Cabeza`), un polerón (`Torso`), calcetines (`Pies`), ropa interior femenina y
masculina (`Piernas`) y un conjunto infantil (`Cuerpo completo`). EF Core agregó las migraciones
`AddProductTaxonomy` y `BackfillProductTaxonomy`; esta última clasifica también las 221 filas que
ya existían.

Se encontró por qué Paris e Hites aparentaban no tener productos: el descubridor limitaba sus
sitemaps antes de filtrar hogar/electrónica. Se invirtió ese orden y se añadieron H&M Chile
(JSON-LD + variantes Next.js) y La Polar/ABC (microdatos), con fixtures tomados de fichas reales.
Zara y Ripley siguen rechazando sus sitemaps al User-Agent identificado, por lo que no se falseó
un navegador ni se afirmó cobertura inexistente.

Prueba local completa:

```text
Product Service: 227 productos, 0 productos con tiendas repetidas
Tiendas con datos: falabella, paris, hites, hym, lapolar
Categorías nuevas visibles: Gorros, Calcetines, Conjuntos,
  Ropa interior femenina, Ropa interior masculina
Barrido vivo Paris/Hites/H&M/La Polar: 7/7 guardados y sincronizados, 0 fallos
Scraper: 135 passed, 7 skipped (integraciones opcionales)
Frontend: build correcto; humo sin fallos
```

Además, una instalación nueva del frontend activa todas las tiendas que entrega la API. Antes
heredaba los cinco ids de los mocks y dejaba tres fuentes reales desmarcadas sin intervención.

**Panditax —**

**Del equipo:**

---

**Martín —**

(10-09, madrugada) 🚀 **El sistema completo montado y medido en AWS, y el borde por fin en el
camino.** Se desplegó entero en la cuenta `116813910999`: EC2 `t3.small` con IP fija
`52.200.101.67`, los cinco contenedores arriba, 361 productos servidos y el sitio compilado en
el bucket.

**Lo que resuelve la entrega:** el frontend ya no llama a la EC2 directa sino al API Gateway, y
está verificado *dentro del bundle* —buscando la cadena `qxaa9rl4dj` en `dist/assets/`—, no
mirando la configuración. Con eso los tres indicadores del EP2 pasan de imposibles a medibles.
La evidencia, tomada **a través del borde**, en `docs/evidencia/borde-20260910-045748.log`.

La escalera de autorización queda en tres peldaños: **401** sin token, **403** con token de
usuario sin el scope `ingesta`, y **400** con token de máquina — este último es el buen
resultado, porque significa que atravesó el JWT Authorizer *y* la validación del BFF y lo único
que falló fue el cuerpo, que se mandó vacío a propósito.

🔒 **La puerta de atrás cerrada** ([ADR-026](adr/026-encabezado-secreto-del-borde.md)). El API
Gateway inyecta `X-Borde-Secreto` en sus nueve integraciones y Caddy responde 403 a quien no lo
traiga. Antes, que el tráfico pasara por el borde era una costumbre del frontend; ahora es una
propiedad del sistema.

💾 **El respaldo, después de perder datos de verdad.** Al recrear la infraestructura se
descubrió que los 221 productos y su historial habían desaparecido: la base vivía en un volumen
de Docker sobre el disco de la instancia, y terminar la EC2 lo destruye. La causa de fondo era
de diseño — `respaldar.sh` corre *dentro* de la máquina y `crear-infra.sh --borrar` corre *en el
portátil*: **el que destruye nunca llamaba al que protege**. Ahora hay dos capas: `--borrar`
respalda, se baja la copia al portátil y comprueba que no llegó vacía antes de destruir nada; y
un timer de systemd vuelca la base **cada hora** a un bucket privado aparte, con versionado.

> Por hora y no por día, y la razón es el patrón de uso: el laboratorio no está encendido 24/7.
> Con un timer diario, `Persistent=true` dispara **al arrancar** —o sea que respalda la sesión
> anterior— y no vuelve a correr en las horas que de verdad se trabaja. Todo lo hecho en la
> sesión se perdía igual.

🐛 **Siete defectos que solo aparecieron al desplegarlo**, ninguno visible leyendo el código.
Los tres primeros son el mismo patrón —el script decía «ya existe» y nunca reconciliaba—:

1. El JWT Authorizer no tenía al scraper en su audiencia: **401 con un token perfecto**, y el
   síntoma apuntaba al scope, que no tenía nada que ver.
2. El BFF repetía el fallo una capa más adentro (`COGNITO_CLIENT_IDS_VALIDOS`). Se descubrió
   porque el 401 traía `server: Kestrel`: ya no lo ponía AWS.
3. Los puertos del grupo de seguridad no se revisaban en cada corrida.
4. El swap no sobrevivía al reinicio — `swapon` sin línea en `/etc/fstab`, y desaparecía en
   silencio justo al cambiar el tipo de instancia.
5. La comprobación de salud preguntaba al puerto 80, que Caddy siempre responde con 308:
   **gritaba «no respondió» con los cinco contenedores arriba**.
6. El frontend se compilaba apuntando a la EC2 directa, esquivando el borde.
7. Un `|| npm run build` de respaldo, con las dos salidas a `/dev/null`, compilaba contra el
   user pool de otra cuenta y **subía sin un solo error**.

🖥️ **La máquina pasó a `t3.small`.** Medido con el sistema en marcha: los cinco contenedores
usan ~355 MB, así que en una `t3.micro` quedaban 225 MB libres y ya había 81 MB en swap sin
hacer nada. El que aprieta es compilar las imágenes de .NET. Son 0,0104 USD/hora más.

🌐 **Dos formas de ver el sitio sin tocar el dominio**: `localhost:5173` (el login funciona,
porque Cognito acepta `localhost` como única excepción a su regla de retornos `https`) y la URL
de sitio estático de S3, pública, donde se ve el catálogo pero **no se puede iniciar sesión**.
S3 no puede dar HTTPS por sí solo: haría falta un CDN delante, y para eso hace falta el dominio.

🛡️ **Y tres cosas más de seguridad:** límite de 50 peticiones por segundo en los dos stages del
borde —no protege datos, protege el crédito del laboratorio—; el bucket de respaldos nace
privado y **separado del bucket del sitio**, que es de lectura pública y donde los volcados
quedarían descargables por cualquiera; y versionado activado, para que un respaldo corrupto no
pueda pisar al último bueno.

⚠️ **Lo que queda pendiente y no es código:** el *client secret* de Google. Sin él, `google.env`
no existe y el login con Google no funciona en ninguna cuenta — el script ya sabe crear el
proveedor.

📄 Guía nueva para el equipo: [`MONTAR-EN-TU-CUENTA.md`](OPERAR-EN-AWS.md), que arranca con
dos recetas copiables — una para la cuenta que administra el dominio y otra para las que no.

(10-09, cierre) 🔒 **El borde pasó de costumbre a garantía.** El API Gateway inyecta
`X-Borde-Secreto` en sus nueve integraciones y Caddy responde **403** a quien no lo traiga
([ADR-026](adr/026-encabezado-secreto-del-borde.md)). El puerto 8080 no se puede limitar por grupo
de seguridad —AWS no publica un rango fijo para las integraciones de HTTP API— así que la
alternativa era dejarlo abierto.

> 🧪 **Se probó revirtiendo la máquina al `Caddyfile` viejo a propósito** y volviendo a desplegar:
> se cerró sola. Eso demuestra algo que conviene tener presente: **la EC2 clona desde GitHub**, así
> que un cambio en el `Caddyfile` o el compose no surte efecto hasta estar en `development`. Solo
> el `.env` se copia aparte.

🔁 **Y al remontar, la base se restaura sola.** Faltaba el otro extremo del problema de los datos:
estaban respaldados, pero nadie los volvía a poner, y un sistema recién montado arranca perfecto y
responde 200 con la base vacía. Ahora `desplegar.sh` cuenta las filas y, si son cero, baja el
último respaldo del bucket y lo carga. **La condición de «cero filas» es lo que lo hace seguro**:
si no hay ni una fila no se puede pisar nada. Probado con el pipeline real sobre una base limpia:
481 productos, 481 de historial y 361 del catálogo.

🛡️ **Tres cosas más de seguridad:** límite de **50 req/s** en los dos stages —no protege datos,
protege el crédito del laboratorio—; **versionado** en el bucket de respaldos, para que un volcado
corrupto no pise al último bueno; y `pg_trgm` + `unaccent`, que destraban el emparejamiento entre
tiendas del [ADR-022](adr/022-identidad-de-producto-entre-tiendas.md) (`"Nike Air Max 90"` contra
`"NIKE AIRMAX 90 Hombre"` da **0,52** de similitud).

🔌 **AWS apagado y verificado:** 0 instancias, 0 IPs reservadas. El `--borrar` respaldó solo,
**se bajó la copia al portátil** (1325 filas) y solo entonces destruyó. Sobreviven sin costo el
user pool, la HTTP API y los dos buckets.

📌 **Lo único que sigue bloqueado no es código:** el *client secret* de Google, que tiene Panditax.
Sin `google.env` no hay IdP federado en ninguna cuenta, y la rúbrica lo pide explícitamente.

---


(07-09) 🔀 **Integradas a `development` las dos ramas del equipo** (PR #10 de Orion, PR #11 de
Panditax). Las dos traían el mismo conflicto en esta bitácora —eran anteriores a la entrada del
03-09— y se resolvió conservando las dos partes.

Se mergearon **con deuda conocida y anotada**, no porque estuvieran listas: a tres días del
freeze, integrar y corregir arriba cuesta menos que dejar dos ramas divergiendo, y el BFF
necesita un servicio real detrás que proteger.

Revisando los dos PR aparecieron **ocho decisiones de arquitectura** que se tomaron en el
camino, de las cuales **solo una está en un ADR** (la 016) y **cuatro contradicen algo que ya
está escrito y que vamos a defender oralmente**: SQLite contra el ADR-010, el scraper en Python
contra el ADR-013, Caddy contra el ADR-015, y la ingesta por HTTP directo contra el argumento de
mensajería de `ARQUITECTURA.md` §6.

> 📌 **Nota del 10-09:** de esas cuatro, **la de SQLite ya no aplica**. El PR #20 migró
> `product-service` a PostgreSQL ([ADR-025](adr/025-catalogo-multi-oferta-postgresql.md)) y no
> queda una sola línea de SQLite en el repositorio. **No hay que defenderla ni mencionarla como
> limitación:** hacerlo sería reconocer un techo de escalado que ya no tenemos. Las otras tres
> siguen en pie. Eso último es lo que más pesa: si en la defensa preguntan
«¿por qué mensajería y no llamadas directas?», el documento tiene tres párrafos de respuesta y
el código hace lo contrario.

🔴 **Y apareció un agujero:** `ProductoController` expone `POST`, `PUT` y `DELETE` sin ninguna
anotación de seguridad, y el `Caddyfile` publica ese servicio en internet. No hay riesgo hoy
porque nada está desplegado, pero es el commit que no puede llegar a la EC2 sin arreglarse —y
arreglarlo es exactamente el 40% del EP1 que me toca.

Todo quedó con dueño y fecha en [`INTEGRACION.md`](HISTORIA.md).

(07-09 · noche) 🌐 **Descubierto revisando el dominio: el sistema YA está en internet.** No
estaba anotado en ninguna parte. `www.cacha-el-precio.com` sirve el frontend desde S3 detrás de
Cloudflare, y `api.cacha-el-precio.com` apunta a una EC2 en `us-east-1` con Caddy delante de
`product-service`. Hay 12 productos y 12 catálogos vivos, y el bundle del frontend trae PKCE de
verdad (`code_challenge`, `S256`, `state`, `identity_provider=Google`). **La fecha del 6-sep no se
incumplió del todo**, y eso conviene decirlo en el informe.

🔴 **Pero la escritura está abierta.** `DELETE /productos/999999999` responde **404, no 401** —lo
probé con un id inexistente para no borrar nada—, o sea que la petición llega a la lógica de
negocio sin pasar por ninguna autorización. Cualquiera puede borrar el catálogo con un `curl`.

🔴 **Y hay dos user pools de Cognito.** El frontend desplegado entra a uno distinto del que crea
`tools/crear-cognito.sh`. La causa la encontré en mi propio script: **nunca crea el IdP de
Google**, no hay un solo `create-identity-provider`. `TAREAS.md` lo tiene sin marcar desde la
Semana 1, pero `README.md` y `ARQUITECTURA.md` ya lo daban por hecho, así que quien hizo el
frontend necesitó Google, no lo encontró, y levantó un pool que sí lo tenía. Es un error mío de
documentación, no de nadie más. Se decide en la reunión de mañana.

(07-09 · noche) 🔐 **El BFF ya valida el token — el 40% del EP1 dejó de ser un `Application.java`
vacío.** Verifica la firma contra el JWKS del pool, el emisor, la vigencia, el `client_id` y que
el `token_use` sea `access`; los roles salen de `cognito:groups`.

Dos cosas necesitaron validador propio porque Cognito no se comporta como Micronaut espera. La
primera ya la sabía del 03-09: **no manda `aud` en el `access_token`**, manda `client_id`, así que
la validación estándar de audience quedó apagada a propósito y el equivalente acepta los **dos**
clients nuestros. La segunda apareció escribiendo: los tres tokens del login están firmados por el
mismo pool, así que **un `id_token` pasa firma y emisor sin problema**; se rechaza por
`token_use`, porque el `id_token` dice quién eres y no qué puedes hacer.

Los 401 y 403 salen en JSON con formato parejo — la rúbrica del EP2 pide mostrar el JSON esperado
ruta por ruta, y una respuesta vacía no es evidencia de nada. **7 tests verdes**, y los de
seguridad están escritos para fallar si la protección se apaga: es fácil creer que un servicio
está protegido porque la dependencia está en el `pom`, y que responda 200 a todo.

Falta el test con un token real (necesita credenciales del lab) y que el gateway consulte de
verdad a `product-service`.

**Orion —**

(08-09) ♻️ **Migración del núcleo de aplicación a C# y simplificación de la arquitectura.** Se
actualizó desde `development` y se reemplazó el Product Service Java por el proyecto ASP.NET Core
10. El gateway también se reescribió en ASP.NET Core, conservando validación de Cognito,
autorización por scope/grupo, aliases del frontend y respuestas 401/403 en JSON.

El contrato scraper-producto quedó alineado: `Version: 1.0`, rutas `/api/products`, identidad
`(store, externalId)` y campos de tienda, imagen, URL, descripción y estado. EF Core agregó la
migración `IntegrateScraperContract` y un índice único. Se corrigió además el cliente Python para
aceptar el `204 No Content` de las actualizaciones. Las cinco pruebas unitarias del adaptador
quedaron verdes y la solución C# compila sin advertencias.

Se eliminaron `price-service`, RabbitMQ, el agregador Maven y migraciones `catalog`/`price` sin
consumidor. PostgreSQL **no** se eliminó: el scraper todavía lo necesita para sus productos e
historial. La decisión y sus costos —HTTP sin cola durable, SQLite sin escalado horizontal y
seguimiento en memoria— están en el [ADR-020](adr/020-csharp-y-simplificacion-de-servicios.md).

Comandos principales usados para verificar:

```bash
dotnet restore CachaElPrecio.slnx
dotnet format CachaElPrecio.slnx --no-restore
dotnet build CachaElPrecio.slnx --no-restore
dotnet ef migrations has-pending-model-changes --project product-service/Product-Service.csproj
PYTHONPATH=scraper-service/src uv run --no-project --with pytest --with pydantic \
  --with pydantic-settings --with httpx --with beautifulsoup4 \
  pytest -q scraper-service/tests/unit/services/test_product_service_sync.py
```

(08-09 · catálogo multi-oferta) Product Service pasó de SQLite al esquema PostgreSQL `product`
y separó producto canónico de oferta. `canonicalKey` agrupa el mismo modelo entre tiendas y
`(store, externalId)` hace el POST idempotente; la respuesta entrega `offers[]`, imágenes por
oferta y tallas abiertas como `S`, `38` o `42.5`. La decisión y el riesgo del matching heurístico
quedaron en el ADR-025.

El scraper amplió las categorías de ropa, accesorios y baño, lee JSON-LD anidado,
`ProductGroup`, `AggregateOffer`, precios chilenos y tallas de variantes. El frontend ya traduce
varias ofertas a una sola ficha. Docker Engine rootless quedó instalado para ejecutar Postgres,
scraper y Product Service sin depender de privilegios del sistema.

(08-09 · catálogo visible) Se reemplazó en Falabella el barrido de fichas —bloqueado con `403`—
por la lectura de `__NEXT_DATA__` en listados públicos de Poleras y Zapatillas. Un barrido de
seis páginas guardó y sincronizó 357 observaciones sin fallos; después del agrupamiento canónico,
el API entregó 221 productos reales, todos con imagen. También se retiró de la base el producto
manual de prueba que apuntaba a `images.example`.

Comandos de verificación nuevos:

```bash
dotnet build CachaElPrecio.slnx --configuration Release
dotnet format CachaElPrecio.slnx --verify-no-changes
cd scraper-service && uv run pytest -m "not red"
docker compose up --build -d postgres product-service scraper-api
```

**Panditax —**

**Del equipo:**

---

### 09-09 · El frontend entra al monorepo, y el arnés de pruebas vuelve a servir

**Martín —** sesión larga, en la rama `feature/frontend-en-monorepo`. Diecisiete commits.

**Qué se hizo, en orden:**

1. **Decisión de contrato** ([ADR-021](adr/021-contrato-publico-en-el-bff.md)). El PR #1 del
   frontend lo dejó pidiendo `/api/products`, el mismo path que publica `product-service`. Eso
   convierte al gateway en intermediario transparente y manda el nombre del servicio interno al
   navegador de cada usuario. El contrato público pasa a ser el del BFF, en español.
2. **El frontend entró con `git subtree`**, sin `--squash`: los 18 commits conservan su autor
   (15 de Panditax). En un trabajo evaluado en parte por el aporte individual, `git log` es la
   evidencia de quién hizo qué.
3. **`npm run humo` pasó de 55 fallos a 0.** Ya fallaba antes —el mismo número en el repositorio
   original y en el commit anterior al PR #1—, así que llevaba tiempo sin avisar de nada.
4. **El backend C# se ejecutó por primera vez.** Compila, arranca y aplica sus migraciones solo.
   Nadie lo había corrido nunca: el PR #18 decía que Docker no estaba instalado donde se preparó.
5. **Fase 3:** el frontend conectado al backend, commit a commit, cada uno probado contra el
   sistema corriendo.
6. **Revisión de la rama entera**, que encontró ocho defectos —tres graves, los tres introducidos
   esa misma sesión— y se arreglaron todos.
7. **Fase 4:** vuelven el contador de visitas y la fecha de alta que había borrado el PR #18.

**Lo que costó, que es lo que conviene recordar:**

- **Cuatro archivos que el `.gitignore` desaparecía en silencio** (`.env.fallo`, `.env.anuncios`,
  `.env.humo` y `caddy/Caddyfile.local`). Reglas heredadas de plantillas —el `*.local` de Vite, el
  `.env.*` genérico— que en un monorepo salen más anchas de lo que quien las escribió pensaba. Una
  de esas ausencias tenía una prueba **en verde sin probar nada**: corría con los datos de ejemplo
  creyendo que hablaba con el backend.
- **SQLite no acepta `DEFAULT CURRENT_TIMESTAMP` al añadir una columna.** La migración de
  `CreatedAt` falló con «Cannot add a column with non-constant default» y el contenedor entró en
  bucle. Es el ejemplo concreto que le faltaba a la deuda «SQLite vs PostgreSQL»: allí habría
  funcionado tal cual.
- **Una fecha sin zona horaria vale menos que ninguna.** `createdAt` llegaba sin la `Z`, y un
  navegador lee una marca ISO sin zona como hora local: en Chile un producto creado hace un minuto
  parecía creado tres horas en el futuro.
- **Levantar el proyecto en local peleaba con producción.** El `Caddyfile` del repositorio es el de
  la EC2, así que Caddy pedía certificado a Let's Encrypt para el dominio real desde el portátil.
  Los intentos fallidos consumen la cuota **del dominio de verdad**.

**Lo que queda pendiente y no depende de una sola persona:**

- 🗣️ **La identidad de producto compartida entre tiendas.** Hoy la aplicación lista ofertas
  sueltas y no compara precios, que es su razón de ser. El `externalId` es el código interno de
  cada tienda, así que agrupar por él no junta nada. Es tema de equipo.
- 🗣️ **Retirar las rutas `/api/products*` del gateway.** El ADR-021 dice que no son contrato
  público y el frontend ya no las usa, pero siguen mapeadas: quitarlas toca el carril de Orion.
- 🗣️ **Reconciliar los dos User Pools de Cognito**, y conseguir el *client secret* de Google.

**Panditax —**

**Del equipo:**

### 09-09, segunda parte · revisión, Fase 4 y la arquitectura v1

**Martín —** la sesión siguió después de la entrada de arriba. Lo que pasó, en orden:

**Una revisión de la rama entera** encontró **ocho defectos, y acertó en los ocho**. Tres eran
graves y **los tres los había introducido el trabajo de esa misma sesión**: con la API caída todo
enlace válido acababa en 404; el guard dejaba la página en blanco al abrir un enlace compartido; y
el `sitemap.xml` emitía N veces la misma URL inexistente. Los tres se comprobaron **ejecutando**
—apagando el gateway, entrando en frío, generando el sitemap contra el backend—, no leyendo.

> **Lección para el informe:** ninguno de los tres se veía leyendo el código. Aparecieron al correr
> el sistema en condiciones que no son la feliz.

**Fase 4:** vuelven el contador de visitas y la fecha de alta que había borrado el PR #18. El
incremento va con `ExecuteUpdateAsync` —una sola sentencia `UPDATE … SET Visits = Visits + 1`—
para que dos visitas simultáneas no se pisen. Dos tropiezos que valen para la defensa: **SQLite no
acepta añadir una columna con un valor por defecto no constante** (`CURRENT_TIMESTAMP` falla), que
es el ejemplo concreto que le faltaba a la deuda SQLite-vs-Postgres; y **una fecha sin zona horaria
vale menos que ninguna** — llegaba sin la `Z` y el navegador la leía como hora local, así que en
Chile un producto recién creado parecía creado tres horas en el futuro.

**El PR #19 se mergeó a `development`** el 09-09 a las 04:18 UTC, **sin revisiones**. Lo mergeó su
propio autor. Queda anotado sin más: `AGENTS.md` dice que nadie mergea su propio PR, y la regla
existe para que un segundo par de ojos mire antes. A un día del freeze es entendible; conviene
recordarlo la próxima.

**Auditoría de arquitectura y definición de la v1.** El hallazgo que reordenó todo:
**el API Gateway está creado, probado y fuera del camino.** `frontend/.env.production` apunta a
`api.cacha-el-precio.com`, ese nombre resuelve a la IP de la EC2, y **ningún archivo del repo
menciona `execute-api`**. No es que se *pueda* saltar el API Manager: lo salta todo el mundo,
siempre. De él dependen tres indicadores del EP2 (20% + 13% + 7%).

**Cuatro decisiones cerradas**, dos de ellas con ADR nuevo:

| Decisión | Resultado |
|---|---|
| CDN | **Cloudflare**, no CloudFront ([ADR-023](adr/023-cloudflare-como-cdn.md)) |
| CORS | **solo en el API Manager**; dominio único pospuesto ([ADR-024](adr/024-cors-en-el-api-manager.md)) |
| Persistencia | ~~SQLite se queda hasta después del EP1~~ → **quedó obsoleta el mismo día:** el PR #20 migró a PostgreSQL ([ADR-025](adr/025-catalogo-multi-oferta-postgresql.md)) |
| CI/CD y emparejamiento | fuera del EP1 ([ADR-022](adr/022-identidad-de-producto-entre-tiendas.md)) |

Dato que nadie tenía: **nunca se comprobó si CloudFront está disponible en el Learner Lab.**
`tools/verificar-aws-academy.sh` sondea Cognito, API Gateway, EC2 e IAM, y CloudFront no aparece.
El ADR-023 deja el comando exacto para comprobarlo — y el aviso de que la lectura no prueba nada,
decide `create-distribution`.

**El backlog de la v1** quedó en 9,5 h obligatorias (identidad, meter el borde en el camino,
cerrarlo por detrás, CORS en un solo sitio, la ruta de visitas) + 11,5 h de calidad. **Todo lo
obligatorio cuesta cero dólares:** se arregla con configuración y scripts, no con infraestructura.
Tablero visual actualizado en el mismo enlace de siempre.

⚠️ **Los ADR 022, 023 y 024 quedaron fuera del merge del PR #19** — se commitearon después de subir
la rama. Están en `feature/frontend-en-monorepo`, respaldados en origin, y necesitan su propio PR.

**Lo primero al retomar:** pedirle a Panditax el *client secret* de Google y que declare la URL de
retorno de Cognito en Google Cloud. La fase 1 del plan no arranca sin eso, y es la primera porque
el API Gateway valida contra ese pool.

### 10-09 · La cadena de scripts, probada de verdad contra AWS

**Martín —** primera sesión con credenciales de AWS en la mano. El objetivo no era desplegar:
era que **el sistema se pueda rehacer entero en otra cuenta en minutos**, porque las cuentas de
laboratorio se agotan y tarde o temprano hay que saltar.

**Lo que el sondeo respondió, y llevaba semanas sin respuesta:**

| | |
|---|---|
| 🔴 **CloudFront** | **bloqueado**, ni siquiera deja *leer* (`AccessDenied` en `ListDistributions`) |
| 🟢 Elastic IP | permitida — `allocate-address --dry-run` responde *«would have succeeded»* |
| 🟢 Cognito · API Gateway · S3 · CloudWatch · Parameter Store · RDS · EC2 | permitidos |

Eso **cierra el ADR-023 con evidencia dura**: Cloudflare no era solo lo prudente. Y de paso
responde el ADR-024 — el dominio único con CloudFront no es «mal momento», es **imposible** en
cuenta de laboratorio.

🔑 **El «problema de los dos user pools» no es de pools: es de cuentas.** En la cuenta de Martín
hay un pool y **cero instancias EC2**; el frontend desplegado apunta a un pool que **no existe
ahí**. La máquina, el bucket y el pool que usa el sitio están en otra cuenta. Por eso la API se
cae cuando esa cuenta se apaga, y por eso no era «elegir qué pool gana».

**El API Gateway estaba viejo, no mal.** Tenía 7 rutas y el script declaraba 12. Las horas lo
cuentan solas: la API se creó el 07-09 a las 22:57 y el script se arregló a las **23:51, 54
minutos después**, y nunca se volvió a correr. El arreglo vivía en el código y no en AWS.
Peor: `GET /productos` y `/catalogos` estaban **detrás del token**, que es justo lo que el
ADR-007 prohíbe. El propio ADR decía que se había detectado a tiempo; nunca se corrigió en la
API desplegada. Hoy son 13 rutas, catálogo público y `AllowCredentials=false`.

📏 **Evidencia del EP2 medida a través del borde, con el backend apagado** — y eso la hace más
fuerte, no más débil: el **401** de `/seguimiento` y `/api/yo` solo puede venir del API Gateway,
porque detrás no había nadie que lo generara. Las públicas dan **503**, que prueba que pasaron el
borde sin token. El preflight devuelve cabeceras con el origen permitido y **ninguna** con uno
ajeno.

**Dos scripts nuevos** cierran la cadena: `crear-infra.sh` (máquina, IP fija, bucket) y
`desplegar.sh` (la aplicación dentro). Migrar de cuenta pasa a ser **cuatro comandos y un cambio
de DNS**, que es el único paso fuera de AWS.

**Se desplegó entero y funcionó:** cinco contenedores corriendo y el BFF respondiendo 200, más
53 archivos del sitio en el bucket. Después se apagó todo — la EC2 gasta crédito.

> **Lección, y es la misma de la Fase 3:** probar los scripts encontró **cuatro defectos que no se
> veían leyéndolos**. El perfil de instancia asociado tarde (el agente SSM solo pide credenciales
> al arrancar). `mkswap -q`, que no existe en Amazon Linux 2023 — y el script **decía que había
> funcionado igual**, porque la cadena de comandos se cortaba sin avisar. `buildx` 0.12.1 en la
> AMI cuando compose exige 0.17: comprobar que *exista* no basta, hay que mirar la **versión**. Y
> un heredoc sin comillas que expandía las variables del bloque remoto **en el portátil**.
>
> Un runbook que nunca se ejecutó no es un runbook. Estos cuatro habrían costado la tarde el día
> de la migración de verdad.

**Lo que queda, y no es técnico:** decidir **en qué cuenta vive el sistema**. Hasta entonces el
despliegue depende de que otra persona encienda su laboratorio, y eso no se arregla con código.

---

### 09-09 · Experiencia de catálogo, tallas y outfits

**Orion —** En `feature/frontend-experiencia-catalogo` se alineó la interfaz del monorepo con el
contrato multi-oferta actual sin cambiar su identidad de papel, tinta y tickets.

- La ficha muestra las tallas informadas por **cada tienda**, junto con el número de ofertas y
  tallas disponibles del producto.
- El armador separa talla de ropa y talla de calzado, descarta ofertas incompatibles o agotadas,
  conserva las preferencias en el navegador y puede completar las ranuras con la combinación
  comprable más barata. El resumen usa las mismas tallas para comparar compra repartida y compra
  en una sola tienda.
- El selector permite buscar y ordenar prendas y explica el stock aplicable antes de elegir.
- Los anuncios pasaron a pausas naturales: después de seis resultados, después de elegir tienda y
  después del armador. Inicio de sesión, registro, retorno OAuth y 404 quedan sin anuncios.
- Se reforzó la jerarquía visual con textura de papel, reglas de acento, separación de paneles y
  estados de disponibilidad, conservando los tokens y componentes existentes.

Verificación:

```bash
cd frontend
npm run build
npm run humo
```

Resultado: build de producción correcto y **0 comprobaciones fallidas**. La consulta opcional del
sitemap al backend se omitió porque los contenedores locales estaban detenidos a propósito.

**Panditax —**

**Del equipo:**

---

### 13-09 · URL canónica por slug en Product Service v2

**Orion —** La rama `feature/frontend-experiencia-catalogo` se actualizó por fast-forward desde
`origin/development` (`734ebf1`) antes de continuar. Los cambios locales se protegieron en un
stash y, al restaurarlos, el único conflicto fue esta bitácora; se conservaron ambas historias.

Product Service ahora genera un `slug` único al crear el producto, lo persiste sin modificarlo
cuando cambia el nombre y lo incluye en `ProductResponse`. La migración `AddProductSlug` rellena
las filas existentes desde `canonicalKey` antes de aplicar `NOT NULL` y el índice único.

V1 conserva `GET /api/products/{id}`. En V2, esa misma entrada por ID responde con una redirección
a `/api/products/{slug}` y el nuevo `GetProductBySlug` devuelve el producto desde la URL canónica.
La versión sigue viajando en la cabecera `Version`, de acuerdo con el contrato del servicio.

El gateway conserva V1 como valor predeterminado, pero ahora reenvía la versión pedida por el
cliente y publica `GET /productos/{slug}`. Cuando V2 responde desde la entrada numérica, desactiva
el seguimiento automático de redirecciones y transforma el `Location` interno
`/api/products/{slug}` en la URL pública `/productos/{slug}`. Los patrones excluyen slugs
puramente numéricos para que `/productos/2` nunca compita con la ruta por id.

El frontend conserva el listado y las visitas en V1, pero abre cada ficha directamente con el slug
de V2. El adaptador mantiene ese campo, las tarjetas y el sitemap comparten la misma URL canónica y
los enlaces antiguos `nombre-id` se redirigen sin quedar duplicados en el historial. La ficha deja
de descargar todo el catálogo antes de mostrar una entrada directa. Durante la validación apareció
además un defecto del armador: una talla agotada quitaba ranuras del cálculo y el outfit seguía
marcado como completo; las partes activas ahora dependen del catálogo, no de la talla elegida.

Verificación sin reactivar los contenedores:

```text
dotnet format --verify-no-changes: correcto
dotnet build --no-restore: 0 errores, 0 advertencias
dotnet ef migrations has-pending-model-changes: sin cambios pendientes
dotnet ef migrations script: migración transaccional renderizada correctamente
arranque del gateway y selección de rutas id/slug: correctos
npm run humo: 0 comprobaciones fallidas
npm run build: compilación de producción correcta
pytest -m "not red": 137 aprobadas, 7 integraciones omitidas
ruff y mypy: sin hallazgos
```

La prueba HTTP contra PostgreSQL quedó pendiente porque los contenedores se mantienen detenidos
por petición expresa; `Product-Service.http` deja preparados los tres requests para probar V1,
la redirección V2 y la lectura directa por slug cuando se reactive el entorno.

**Panditax —**

**Del equipo:**

---

### 14-09 · Despliegue reproducible y catálogo de seis tiendas

**Orion —** Se levantó una cuenta nueva del Learner Lab desde cero con los scripts del
repositorio: Cognito, API Gateway, EC2 `t3.small` con Elastic IP, bucket web S3 y bucket privado
de respaldos. La EC2 ejecuta Caddy, Gateway, Product Service, PostgreSQL y Scraper API mediante
Docker Compose. El frontend quedó compilado con el client ID y la URL del API Gateway de esta
misma cuenta; el script comprobó ambos valores dentro del bundle antes de subirlo.

La primera ejecución descubrió dos fallos que no aparecían en la compilación local:

- Product Service abortaba al construir las rutas porque los corchetes de la expresión regular
  del slug se interpretaban como tokens de una ruta por atributo. Se escaparon como `[[...]]` y
  se comprobó el arranque sin reinicios, la lectura v1 y una ficha v2 por slug.
- GitHub respondió 504 al descargar Buildx. Los scripts ahora reintentan errores transitorios y
  descargan a un temporal antes de instalar, para no reemplazar un binario válido con una
  descarga incompleta.

S3 sirve `index.html` como documento de error, pero mantiene el HTTP 404. El despliegue publica
además las once rutas estáticas conocidas como objetos sin extensión, de modo que portada,
comparador, login, outfits y páginas informativas responden 200 al abrirse o recargarse. Las
fichas dinámicas `/producto/:slug` siguen dependiendo del CDN del dominio definitivo para
reescribir cualquier ruta a la SPA con estado 200.

Se ejecutó una carga inicial respetando la pausa por tienda, de forma secuencial. El contrato
público quedó con **325 productos canónicos y 325 ofertas visibles**, distribuidas así:

| Tienda | Ofertas visibles | Con imagen | Con tallas informadas |
|---|---:|---:|---:|
| Falabella | 229 | 229 | 202 |
| Paris | 30 | 30 | 0 |
| Hites | 24 | 24 | 0 |
| H&M | 22 | 22 | 22 |
| La Polar / ABC | 17 | 17 | 16 |
| Sparta | 3 | 3 | 0 |

La diferencia entre fichas sincronizadas y ofertas visibles ocurre porque Product Service
consolida los SKU de una misma tienda antes de responder. El catálogo expone las seis tiendas y
categorías como gorros, jockeys, polerones, ropa interior femenina y masculina, vestidos,
pantalones, zapatillas y zapatos. Se verificaron además: catálogo público 200, ruta protegida sin
token 401, acceso directo a la EC2 sin el encabezado del borde 403, CORS desde el origen S3 y una
imagen real 200.

Después de la ingesta se ejecutó un respaldo inmediato y se confirmó el archivo comprimido en el
bucket privado. El temporizador horario también quedó activo. Pendiente fuera del repositorio:
apuntar Cloudflare a la nueva IP/bucket y configurar el proveedor de Google con credenciales
rotadas y su nueva URL de retorno.

**Panditax —**

**Del equipo:**
