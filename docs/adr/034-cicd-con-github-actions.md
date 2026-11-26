# ADR-034 · CI/CD con GitHub Actions

Fecha: 19-11-2026 · Estado: **aceptado**. Probado el 19-11 en la cuenta `731705830984`: el CD
desplegó `7cb1f0e` y `www` quedó llamando al API Gateway y al Cognito de esa cuenta. Cómo se usa
día a día: [CICD](../CICD.md). **Modificado el 26-11-2026**: el aviso al front sale también desde
`development` ([ver al final](#actualización-del-26-11-2026-el-front-sale-desde-development)).


## Contexto

Hasta el 18-11 no había ningún workflow. Las pruebas se corrían a mano antes de cada PR y el
despliegue era `tools/desplegar.sh` desde un laptop, que **compilaba las imágenes dentro de la
EC2** (`t3.small`, 2 GB). Para la EFT entran muchos PRs de tres personas en dos semanas, y la
rúbrica cuenta el CI/CD como módulo extra.

Tres condiciones del Learner Lab mandan sobre todo lo demás:

- Las credenciales de AWS **vencen cada ~4 h** y no se pueden renovar desde GitHub.
- **OIDC con un rol IAM está bloqueado** en Academy, igual que crear roles nuevos.
- La EC2 **no está prendida 24/7**: el CD tiene que convivir con el lab apagado.

## Alternativas

**Dónde corre:**

- **Jenkins en la EC2.** Es lo que se vio en clases. Pide ~1 GB de RAM en una máquina de 2 GB y
  hay que mantenerlo. Descartado.
- **GitHub Actions.** Viene con el repo, los minutos de un repo público no se cobran y no ocupa
  la EC2. **Elegido.**

**Cómo entra a la EC2:**

- **SSM con OIDC.** Lo correcto en una cuenta real: sin puerto 22 ni llaves guardadas. No se puede
  en el lab (OIDC bloqueado; las credenciales vencen).
- **Un runner propio dentro de la EC2.** No necesita puerto abierto, pero en un repo público un PR
  podría correr código en el servidor de producción.
- **SSH con la llave del lab guardada como secret.** **Elegido**, con las consecuencias de abajo.

**Cómo se publica el frontend:**

- **S3, como hacía `desplegar.sh`.** Exige credenciales de AWS en GitHub (vencen) y S3 devuelve
  404 en las rutas `/producto/:slug`.
- **Un job propio en `cd.yml` con `wrangler deploy`.** Se escribió y se probó en local el 19-11,
  pero necesitaba un token de Cloudflare y el rol de Martín en esa cuenta no permite Workers.
- **El repo de Panditax con la integración Git de Cloudflare.** Panditax armó la sincronización el
  18-11: el monorepo avisa, su repo copia `frontend/` y Cloudflare publica cada push a su `main`.
  No pide ningún token de Cloudflare en GitHub. **Elegido**; el job propio se sacó (`f0de0b4`).

## Decisión

GitHub Actions con tres workflows en el monorepo: **CI** en cada PR y cada push a `development`;
**CD** solo con push a **`main`**, que arma las imágenes en GitHub, las sube a **GHCR** y las
despliega **por SSH** con *rollback*; y un **aviso** al repo del frontend, también solo desde
`main`. El frontend lo publica Cloudflare desde el repo de Panditax.

## Consecuencias

- La EC2 deja de compilar: baja imágenes ya probadas, etiquetadas con el hash del commit. Lo que
  corre en producción se puede rastrear hasta un commit exacto.
- Un despliegue que no levanta **vuelve solo a la versión anterior**.
- Con AWS apagado el CD termina en verde con un aviso, y el sitio sigue arriba porque el Worker no
  depende de AWS.
- El CI no usa secretos, así que un PR de un fork no puede sacar nada.
- 🔴 **El puerto 22 queda abierto a todo internet.** Ya lo estaba (para `ssh`, `desplegar.sh` y los
  respaldos), y los runners de GitHub no tienen IP fija para cerrarlo a una. Se entra **solo con
  llave** (`PasswordAuthentication no`, verificado). En una cuenta real: GitHub → OIDC → rol IAM →
  SSM, con el 22 cerrado.
- 🔴 **El frontend depende de otro repo y otra cuenta.** Si Panditax pierde acceso o cambia su
  workflow, el sitio deja de actualizarse. Su repo copia desde `development` hasta que cambie
  `MONO_BRANCH` a `main`, y no revisa que el bundle lleve el Cognito correcto.
- 🟠 **La configuración del front vive en Cloudflare**, no en el repo. Al cambiar el sistema de
  cuenta de AWS hay que cambiar a mano las cuatro variables de build y volver a desplegar.
- 🟠 **La llave SSH del lab queda guardada en GitHub.** Es del lab y muere con él, pero mientras
  exista da acceso a la EC2.
- Las imágenes de GHCR son públicas. No llevan secretos, y así la EC2 las podría bajar sin token.
- Terraform sigue siendo manual (`tools/tf.sh`), porque el estado vive en el laptop.

## Actualización del 26-11-2026: el front sale desde `development`

**Contexto.** El repo de Panditax copia desde `development` (`MONO_BRANCH`), pero el aviso salía
solo desde `main`. Cada merge a `development` quedaba esperando la copia programada «cada 15 min»,
que GitHub corría cada varias horas: el PR #57 esperó más de dos horas y hubo que lanzar la copia a
mano. Pasar a `main` como estaba previsto no era opción todavía: `main` va más de 300 commits
atrás, y copiar desde ahí publicaría un front viejo, sin el login con Google.

**Decisión.** Hasta la EFT, `avisar-frontend.yml` avisa también con cada push a `development` que
toca `frontend/`. El backend no cambia: el CD sigue saliendo solo de `main`.

**Consecuencias.**
- Lo que entra a `development` llega a `www` en 1–2 minutos, sin esperar el cron.
- 🔴 **Para el front, `development` es producción.** Un PR mergeado sin revisar sale al sitio al
  tiro. Por eso importa más que nunca que nadie mergee su propio PR.
- Front y backend salen de ramas distintas: `www` puede llevar un front que espera algo que el
  backend de `main` todavía no tiene. Se corrige al llevar `development` a `main` antes de la EFT.
- **Para volver al diseño original:** `MONO_BRANCH: main` en el repo del frontend **y** sacar
  `development` de `avisar-frontend.yml`, en el mismo momento.

## Ver también

- [CICD](../CICD.md): los workflows, los secretos y qué hacer cuando algo falla
- [ADR-033](033-terraform-para-la-infraestructura.md): Terraform, que el CD no reemplaza
- [ADR-023](023-cloudflare-como-cdn.md): Cloudflare como CDN
