# ADR-033 · Terraform para la infraestructura de AWS

Fecha: 10-11-2026 · Estado: propuesta (fase 0). Pasa a aceptado cuando Terraform monte el
sistema completo en dos cuentas desde cero.

## Contexto

La infraestructura se crea con cuatro scripts: `crear-infra.sh`, `crear-cognito.sh`,
`crear-api-gateway.sh` y `crear-mensajeria.py`, unas 1.900 líneas entre los cuatro. Funcionan y
están probados en dos cuentas, pero tienen tres problemas que ya costaron horas:

1. **Son una receta, no un estado.** Cada uno busca lo que hay y crea lo que falta. Nunca borra
   lo que no conoce, así que una ruta o una cola que se saca del script queda viva en AWS como
   basura. También pasa al revés: el 10-09-2026 la API tenía 7 rutas y el script declaraba 12,
   porque se había corregido el script pero nadie lo volvió a correr.
2. **El orden lo recuerda una persona.** Al volver a prender, la EC2 recibe otra IP y hay que
   correr `crear-api-gateway.sh` para apuntarla. Si se olvida, la API llama a una IP muerta.
3. **No se puede ver un cambio antes de aplicarlo.** No hay nada parecido a un «qué va a pasar».

Además, las cuentas del Learner Lab se agotan. La idea de las tres cuentas
([MIGRACION.md](../MIGRACION.md)) exige poder montar el sistema entero en otra cuenta rápido y
sin sorpresas.

## Alternativas

- **Seguir con los scripts.** No cuesta nada nuevo y ya funcionan. No resuelve ninguno de los tres
  problemas.
- **CloudFormation.** Es la herramienta propia de AWS y guarda el estado en la cuenta. No se
  probó si el Learner Lab deja crear stacks, y el estado vive en la misma cuenta que se puede
  resetear.
- **Terraform.** Declarativo: se escribe cómo debe quedar la cuenta, `plan` muestra la diferencia
  y `apply` la aplica en el orden correcto. Es la herramienta más usada en el mercado. Desde la
  versión 1.6 su licencia es BSL, que permite este uso.
- **OpenTofu.** El fork libre de Terraform, con el mismo lenguaje. Arch trae la 1.13.9, más
  atrasada que el Terraform de los repos. Si algún día conviene cambiar, el código es casi el
  mismo.

## Decisión

Se adopta **Terraform**, con el proveedor `hashicorp/aws` 6.x, para todo lo que **crea**
infraestructura. Lo que **opera** el sistema (desplegar, respaldar, dar permisos de admin,
probar) sigue en `tools/`.

- **Dos raíces.** `infra/terraform/cuenta/` lo corre cada persona en su cuenta.
  `infra/terraform/dominio/` (el registro DNS en Cloudflare) lo corre solo quien administra el
  dominio, para que nadie lo mueva sin querer con un `apply` de su cuenta.
- **Un mismo código, distintas variables.** Cada persona tiene su `cuentas/<nombre>.tfvars`, fuera
  de git. Lo que cambia entre cuentas es poco: el número de cuenta y si tiene el dominio.
- **Estado local, uno por cuenta.** No se usa un bucket S3 para el estado, como sería lo normal,
  porque en Academy viviría en la misma cuenta que se puede resetear. Cada cuenta la maneja una
  sola persona, así que no hay estado que compartir.
- **Mismos archivos de salida.** Terraform escribe `infra.env`, `cognito.env`, `api-gateway.env`,
  `borde.env` y `mensajeria.env` con los mismos nombres de variable que los scripts, así
  `desplegar.sh` no cambia.
- **Apagar no es destruir.** `encendido = false` baja solo la EC2 y su IP, que es lo que cobra por
  hora. El respaldo de la base va antes, y si falla no se apaga nada.
- **Barreras escritas, no recordadas.** `allowed_account_ids` impide trabajar en la cuenta
  equivocada. `prevent_destroy` protege el bucket de respaldos y `deletion_protection` protege el
  user pool de la cuenta con dominio. Todo lleva la etiqueta `gestionado-por = terraform`.
- **Transición por fases.** Los scripts siguen siendo el plan B hasta que Terraform monte el
  sistema completo en dos cuentas. Recién entonces se borran, en un PR aparte. 🚫 En una misma
  cuenta no se mezclan.

## Consecuencias

- Montar, prender y apagar pasan a ser un comando cada uno, y `terraform plan` muestra cada
  cambio antes de aplicarlo.
- Lo que se saca del código **se borra de AWS**. Es lo que se quería, pero un error en el código
  también borra. Por eso `tools/tf.sh` se detendrá si el plan borra o reemplaza algo distinto de
  la EC2 o su IP.
- **El estado guarda secretos en texto plano** (el del borde, el del cliente del scraper y el de
  Google). Nunca va a git, y si se filtra hay que rotar esos secretos.
- **Si el estado se pierde**, Terraform cree que la cuenta está vacía y choca por los nombres. Se
  recupera con bloques `import`, porque todos los recursos tienen nombres fijos, pero es trabajo a
  mano.
- **No se sabe todavía si el Learner Lab da todos los permisos que pide el proveedor.** Terraform
  lee más atributos que los scripts (un bucket S3, por ejemplo, consulta varias configuraciones).
  La fase 0.5 lo prueba antes de escribir el resto.
- **Al apagar, la IP cambia.** En la cuenta con dominio eso deja el registro DNS de `api`
  apuntando a una dirección muerta hasta correr `dominio/`. Mantener la IP solo en esa cuenta
  cuesta unos USD 3,6 al mes de crédito. Decisión pendiente.
- Los tres tienen que instalar Terraform 1.15 o más nuevo.
- Mientras dure la transición hay dos formas de crear lo mismo, y la documentación tiene que dejar
  claro cuál vale en cada cuenta.
