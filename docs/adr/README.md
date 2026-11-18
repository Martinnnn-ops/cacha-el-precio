# Registro de decisiones (ADR)

> **Qué es.** Una decisión técnica por archivo: el contexto, las alternativas, lo que se decidió y
> sus consecuencias, **incluidas las malas**.
> **Para quién.** El equipo y la defensa: cuando preguntan «¿por qué así?», la respuesta está aquí.
> **Última revisión:** 18-11-2026.

> 📍 **En 10 segundos:** 22 decisiones vigentes o reemplazadas. Las que más se preguntan:
> **006** (Cognito), **019** (API Gateway), **026** (encabezado del borde), **029** (SQS y SNS) y
> **033** (Terraform). Una decisión que cambia **no se borra**: se marca como reemplazada y se
> escribe la nueva.

**Estado:** ✅ vigente · 🟡 vigente con cambios · 🔁 reemplazada · 💡 propuesta sin implementar

| ADR | Decisión | Estado |
|---|---|---|
| [006](006-cognito-como-idaas.md) | Cognito como IDaaS | ✅ |
| [007](007-catalogo-publico-sin-token.md) | El catálogo es público, sin token | ✅ |
| [008](008-ec2-docker-compose.md) | EC2 con Docker Compose | ✅ |
| [013](013-java-25-maven.md) | Java y Maven | 🔁 por el 020 |
| [014](014-versionado-semantico-por-servicio.md) | Versiones por servicio | ✅ |
| [015](015-red-privada-con-vpc-link.md) | Red privada con VPC Link | 🟡 se desplegó la opción B: EC2 pública con encabezado del borde |
| [016](016-product-service-y-modelos-sin-dto.md) | Límite de Product Service | 🔁 en parte |
| [018](018-scraper-en-python.md) | Scraper en Python | ✅ |
| [019](019-api-gateway-como-api-manager.md) | API Gateway como API Manager, Caddy detrás | ✅ |
| [020](020-csharp-y-simplificacion-de-servicios.md) | C# y retiro de los procesos vacíos | ✅ |
| [021](021-contrato-publico-en-el-bff.md) | El contrato público es el del BFF, en español | ✅ |
| [022](022-identidad-de-producto-entre-tiendas.md) | Identidad de producto entre tiendas | 💡 |
| [023](023-cloudflare-como-cdn.md) | Cloudflare como CDN | ✅ |
| [024](024-cors-en-el-api-manager.md) | CORS solo en el API Manager | ✅ |
| [025](025-catalogo-multi-oferta-postgresql.md) | Catálogo multi-oferta en PostgreSQL | ✅ |
| [026](026-encabezado-secreto-del-borde.md) | El borde se identifica con un encabezado secreto | ✅ |
| [028](028-armario-personal-y-politica-catalogo.md) | Armario personal y política del catálogo | ✅ |
| [029](029-mensajeria-sqs-sns.md) | Mensajería asíncrona con SQS y SNS | ✅ |
| [030](030-notification-service.md) | notification-service: seguidores y correo | ✅ |
| [031](031-publicacion-cambios-oferta.md) | Cómo publica product-service los cambios de oferta | ✅ |
| [032](032-administracion-de-mensajeria.md) | Administración de mensajería en el gateway | 🟡 desde el 17-11 el panel solo mira |
| [033](033-terraform-para-la-infraestructura.md) | Terraform para la infraestructura de AWS | ✅ aceptado el 17-11, fases 0 a 7 |

Los números que faltan (001–005, 009–012, 017, 027) nunca tuvieron archivo en este repo. Algunos
se citan en la bitácora o en el archivo (por ejemplo el 010, los esquemas `catalog` y `price` que
se retiraron): se leen como historia, no como decisiones vigentes.

## Cómo escribir uno nuevo

1. El siguiente número libre y un nombre corto: `034-kinesis-para-precios.md`.
2. **Contexto:** qué problema hay y qué lo hace urgente.
3. **Alternativas:** al menos dos, con su costo real.
4. **Decisión:** una frase.
5. **Consecuencias:** las buenas **y las malas**. Una decisión sin consecuencias malas no se pensó.
6. Una fila nueva en esta tabla, y el enlace desde el documento que la usa.

## Ver también

- [ARQUITECTURA](../ARQUITECTURA.md): cómo encajan todas
- [CONTEXTO](../CONTEXTO.md): lo que está abierto hoy
