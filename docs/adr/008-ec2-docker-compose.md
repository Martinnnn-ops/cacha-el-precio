# ADR-008 · EC2 con Docker Compose en vez de ECS Fargate

**Fecha:** 27-08-2026
**Estado:** aceptada

## Contexto

Los cuatro servicios del backend (`api-gateway`/BFF, `product-service`, `price-service` y
RabbitMQ) tienen que estar **corriendo en internet el 6 de septiembre**. Esa es la fecha que
manda: el EP1 no se puede entregar con el sistema en `localhost`, aunque el código esté
impecable.

Los documentos del proyecto asumían **ECS Fargate** desde la semana 0. Al revisar el calendario
real quedaron 10 días para el despliegue, repartidos con otras cuatro evaluaciones del semestre.

Un dato que pesa: **la rúbrica del EP1 no reparte ni un punto por el cómputo.** El 60% es el
flujo OIDC del frontend y el 40% es la validación del token en el BFF. Lo que se evalúa del
despliegue es que **exista y sea accesible**, no cómo está orquestado.

## Alternativas consideradas

- **ECS Fargate.** Es lo correcto a mediano plazo y era el plan original. Pero para llegar a
  "desplegado" hay que resolver antes ECR, task definitions, roles de IAM de ejecución y de
  tarea, security groups, target groups y la configuración de logs en CloudWatch. Cada una es
  un lugar donde quedarse pegado un día, y no quedan días de sobra.
- **EKS (Kubernetes).** Descartado antes por costo: el control plane son ~USD 73/mes del
  crédito de AWS Academy. Orquestar cuatro servicios no necesita Kubernetes.
- **App Runner / Elastic Beanstalk.** Más simples que Fargate, pero igual introducen un modelo
  de despliegue nuevo que nadie del equipo ha usado, y esconden la configuración justo cuando
  hay que poder explicarla en la defensa.
- **Una EC2 con el mismo `docker-compose.yml` de desarrollo.** Es lo que el equipo ya sabe
  usar, porque es el archivo con el que se levanta el proyecto en local desde la semana 0.

## Decisión

**Los cuatro contenedores corren en una instancia EC2, en subred privada, con el mismo
`docker-compose.yml` que se usa en local.** La base de datos queda **fuera** del compose, en
**RDS Postgres**.

El resto de la arquitectura no cambia: API Gateway sigue siendo el API Manager y sigue
validando el JWT en el borde, el frontend sigue en S3 + CloudFront, y los scrapers siguen en
Lambda.

## Consecuencias

**Lo bueno**
- El despliegue es `docker compose up` sobre una instancia. Cabe en el calendario.
- Paridad real entre desarrollo y producción: es literalmente el mismo archivo, así que
  "en mi máquina funciona" deja de ser una fuente de sorpresas.
- Menos superficie de AWS que aprender y explicar en una defensa de 5 a 10 minutos.

**Lo malo — y hay que decirlo antes de que lo pregunten**
- **No hay auto-escalado.** Si la instancia se queda corta, se cambia a mano por una más
  grande. Con dos tiendas y tres capturas diarias, el tráfico no es el problema.
- **Es un único punto de falla.** Si la EC2 se cae, se caen los cuatro servicios. Sobreviven el
  frontend (S3/CloudFront) y los datos (RDS), que es lo que no se puede perder.
- **Contradice el patrón que enseña el ramo.** Es una decisión de calendario, no de
  arquitectura, y conviene presentarla así: *"sabemos que Fargate es lo correcto a mediano
  plazo; elegimos entregar funcionando antes que entregar elegante"*.
- **La migración está acotada.** Como los servicios ya son contenedores sin estado, pasar a
  Fargate más adelante es cambiar dónde se ejecuta la misma imagen, no rediseñar. Si el ramo
  lo pide en el EP3 o EP5, el camino queda abierto.

**Condición de esta decisión:** queda escrita acá porque si no, el repo se contradice solo —
`ARQUITECTURA.md` decía Fargate en nueve lugares distintos.
