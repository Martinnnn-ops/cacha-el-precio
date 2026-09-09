# Despliegue en AWS (S3 + CloudFront)

## La trampa de S3, y por qué hace falta CloudFront

S3 tiene una opción de «alojamiento de sitio web estático» donde se pone
`index.html` como *documento de error*. Con eso una SPA parece funcionar: entras
a `/comparador`, S3 no encuentra ese objeto, sirve `index.html`, y Vue Router
pinta la página correcta.

**Pero devuelve `HTTP 404`.** El navegador lo disimula; un rastreador no.

Consecuencias reales:

- Google indexa `/comparador`, `/preguntas` y las legales como páginas
  inexistentes, así que no aparecen en resultados.
- El rastreador de AdSense ve 404 en todo el sitio menos en la portada, y la
  revisión de la cuenta puede rechazarse por «contenido insuficiente».
- El endpoint de sitio web de S3 **no sirve HTTPS** en dominio propio, y AdSense
  exige HTTPS.

Por eso el bucket va **privado**, sin alojamiento web, y CloudFront delante.

---

## 1. Bucket

Privado, sin alojamiento web estático activado. CloudFront accede con **OAC**
(Origin Access Control), que sustituye a la antigua OAI.

Política del bucket — sustituye `MI-BUCKET`, `MI-CUENTA` y `MI-DISTRIBUCION`:

```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Sid": "SoloCloudFront",
      "Effect": "Allow",
      "Principal": { "Service": "cloudfront.amazonaws.com" },
      "Action": "s3:GetObject",
      "Resource": "arn:aws:s3:::MI-BUCKET/*",
      "Condition": {
        "StringEquals": {
          "AWS:SourceArn": "arn:aws:cloudfront::MI-CUENTA:distribution/MI-DISTRIBUCION"
        }
      }
    }
  ]
}
```

---

## 2. CloudFront

**Origen:** el endpoint REST del bucket
(`mi-bucket.s3.us-east-1.amazonaws.com`), **no** el de sitio web.

**Documento raíz por defecto:** `index.html`

### Respuestas de error — esto es lo que arregla el 404

Con OAC, S3 responde **403** (no 404) a las claves que no existe, porque el
permiso es por objeto. Hay que mapear las dos:

| Código del origen | Respuesta | Página | TTL |
|---|---|---|---|
| 403 | **200** | `/index.html` | 0 |
| 404 | **200** | `/index.html` | 0 |

El `200` es el punto clave: sin él seguimos con el problema de arriba.

> **Efecto secundario asumido:** una URL que de verdad no existe también
> devuelve 200 con la pantalla de «404» de la aplicación. Es el comportamiento
> normal de cualquier SPA servida así y es preferible al problema contrario.

Por CLI:

```bash
aws cloudfront create-invalidation --distribution-id MI-DISTRIBUCION \
  --paths '/index.html' '/ads.txt' '/robots.txt' '/sitemap.xml'
```

### Comportamiento

- **Redirigir HTTP a HTTPS** — obligatorio para AdSense.
- **Compresión automática:** activada.
- **Política de caché:** `CachingOptimized`. El `Cache-Control` lo pone el
  script de despliegue por archivo, y CloudFront lo respeta.

---

## 3. Certificado y dominio

El certificado de ACM tiene que estar en **us-east-1**, pase lo que pase — es
la única región desde la que CloudFront los lee, aunque el bucket esté en otra.

En Route 53, un registro **A de tipo alias** apuntando a la distribución.

---

## 4. Publicar

```bash
BUCKET=mi-bucket DISTRIBUCION=E123ABC npm run desplegar
```

El script sube `assets/` con caché de un año (llevan hash en el nombre) y el
resto con caché de cinco minutos, fija el tipo de contenido de `ads.txt` e
invalida los archivos sin hash.

---

## 5. Comprobar después de publicar

```bash
# ads.txt: 200, texto plano y la línea exacta
curl -sI https://cacha-el-precio.com/ads.txt | grep -iE 'http/|content-type'
curl -s  https://cacha-el-precio.com/ads.txt

# Las rutas profundas tienen que dar 200, NO 404
for r in / /comparador /preguntas /terminos /privacidad; do
  printf '%-14s %s\n' "$r" "$(curl -s -o /dev/null -w '%{http_code}' "https://cacha-el-precio.com$r")"
done

# HTTP redirige a HTTPS
curl -sI http://cacha-el-precio.com/ | grep -i location
```

Las cinco rutas deben responder `200`. Si alguna da `404`, faltan las
respuestas de error de CloudFront del punto 2.
