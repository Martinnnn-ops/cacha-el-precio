# ADR-024 · El CORS vive en el API Manager, y el dominio único se pospone

**Fecha:** 09-09-2026

**Estado:** aceptada

## Contexto

El sitio y la API viven en **dos direcciones distintas** —`www.cacha-el-precio.com` y
`api.cacha-el-precio.com`—, así que cada petición del navegador **cruza de origen** y el navegador
exige CORS. Hoy el CORS está declarado **en dos sitios a la vez**:

```
tools/crear-api-gateway.sh:126   AllowOrigins=https://www.cacha-el-precio.com,http://localhost:5173
caddy/Caddyfile:24               Access-Control-Allow-Origin "https://www.cacha-el-precio.com"
```

El propio `Caddyfile` avisa en un comentario de que eso es transitorio, y de que
*«tenerlo en los dos lados a la vez manda dos cabeceras y el navegador rechaza la respuesta»*.

Hoy no explota, y el motivo es peor que el problema: **el tráfico real no pasa por el API
Gateway.** Verificado el 09-09 — `frontend/.env.production` apunta a `api.cacha-el-precio.com`,
ese nombre resuelve a la IP de la EC2, y no hay un solo archivo en el repositorio que mencione
`execute-api`. El API Manager está creado y probado, pero **ninguna petición de usuario lo
atraviesa**.

Como la v1 del EP1 mete el API Gateway en el camino, la duplicación deja de ser teórica.

## Alternativas consideradas

- **(a) Dejarlo como está.** Cuesta cero. Pero el día que el tráfico pase por el borde hay dos
  autoridades declarando CORS, y una inconsistencia ya viva: Caddy solo permite el origen **con**
  `www`, mientras el `sitemap.xml` emite URLs **sin** `www`. Es un fallo que solo se ve en un
  navegador y en producción: con `curl` no aparece.

- **(b) CORS solo en el API Manager.** Una sola autoridad decide qué origen entra. Caddy vuelve a
  hacer únicamente lo suyo —TLS y proxy— y el preflight se resuelve en el borde, sin llegar a la
  EC2. Cuesta cerca de una hora.

- **(c) Dominio único: CloudFront reparte por ruta y el CORS desaparece.** `/*` va al sitio y
  `/api/*` a la API, así que el navegador nunca sale del mismo origen. Es **mejor arquitectura**:
  sin preflight, sin cabeceras que sincronizar, y desarrollo y producción se comportan igual
  —el proxy de Vite ya hace exactamente eso—.

## Decisión

**Se adopta (b): el CORS se declara únicamente en el API Manager**, y se retira del `Caddyfile`.
**(c) queda pospuesta a después del EP1.**

### Por qué (b) y no (c), que es mejor arquitectura

Tres razones, y la primera es la que decide:

1. **La rúbrica del EP2 evalúa el CORS como indicador propio** — un 7%, textual:
   *«CORS en el API Manager»*. La opción (c) **elimina lo que hay que demostrar**. Se podría
   defender que se eliminó por diseño, y sería cierto y elegante, pero es apostar un indicador a
   que el argumento convenza. No es el momento de apostar indicadores.
2. **(c) depende de CloudFront, que no está verificado en la cuenta** (ver
   [ADR-023](023-cloudflare-como-cdn.md)). Habría que comprobarlo, montarlo, mover el DNS, el
   certificado y las *redirect URIs* de Cognito — todo junto, a un día del code freeze.
3. **(b) arregla el fallo real por una hora de trabajo.** El riesgo de la cabecera duplicada
   desaparece igual, que es lo que importaba.

> **(c) es mejor arquitectura y peor decisión hoy.** No se descarta: se pospone con fecha y con
> motivo.

### Qué se hace, en concreto

- se retira el bloque `header { Access-Control-… }` y el `respond @cors_preflight 204` del
  `Caddyfile` de producción;
- el API Gateway conserva sus **orígenes explícitos**, nunca `*`;
- se corrige la inconsistencia `www` / apex: el origen permitido y las URLs del `sitemap.xml`
  tienen que ser el mismo;
- **se apaga `AllowCredentials`.** Los tokens de Cognito viajan en la cabecera `Authorization`, no
  en cookies, así que el navegador no necesita mandar credenciales de origen cruzado. Mantenerlo
  encendido es superficie que no se usa — y con `AllowCredentials` activo el comodín `*` queda
  prohibido de todas formas, así que tampoco aporta flexibilidad.

### Lo que esta decisión NO cambia

El CORS **no es un control de seguridad del servidor**. Es una regla que el navegador aplica para
proteger al usuario; quien llame con `curl` no la ve. La autorización real la siguen haciendo el
JWT Authorizer del borde y el BFF, y esta decisión no toca ninguno de los dos.

## Consecuencias

### Positivas

- una sola autoridad de CORS: no puede haber dos configuraciones que discrepen;
- desaparece el riesgo de la cabecera duplicada antes de que el tráfico pase por el borde;
- el preflight se resuelve en el API Gateway y **no llega a la EC2**;
- el indicador del 7% queda limpio y demostrable con DevTools: orígenes explícitos, configurados
  en el API Manager, exactamente lo que la rúbrica pide;
- `AllowCredentials` apagado reduce superficie sin perder nada.

### Costos y riesgos

- **el desarrollo local depende de que `http://localhost:5173` siga en la lista de orígenes.** Si
  alguien lo quita «por limpieza», el equipo se queda sin poder trabajar contra el backend real y
  el error apunta al lugar equivocado;
- mientras el sitio y la API estén en dominios distintos, **cada ruta nueva arrastra un preflight**:
  una petición extra antes de la real;
- se conserva una configuración que el dominio único haría innecesaria — es deuda conocida y con
  destino escrito;
- ⚠️ **queda por verificar** si HTTP API sobrescribe las cabeceras CORS que devuelva la
  integración. La documentación dice que sí; no se ha comprobado contra la cadena real por falta
  de credenciales. **El arreglo no depende de que se cumpla** —quitando el bloque de Caddy no hay
  duplicado en ningún caso—, pero conviene medirlo desde un navegador cuando el lab esté abierto.

## Condiciones para revisar la decisión

- **después del EP1**, evaluar el dominio único (c) con CloudFront ya verificado y sin una entrega
  encima. Ese día este ADR se reemplaza, no se enmienda: la decisión de fondo cambia;
- si aparece un segundo cliente en otro origen (una app, otro dominio), la lista de orígenes
  explícitos deja de escalar y hay que revisar el enfoque;
- si algún día la sesión pasa a viajar en cookie en vez de en cabecera, `AllowCredentials` y
  `SameSite` vuelven al análisis.
