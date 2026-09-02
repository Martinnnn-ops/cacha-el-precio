# ADR-015 · Red privada de verdad, con VPC Link

**Fecha:** 30-08-2026
**Estado:** aceptada

## Contexto

`ARQUITECTURA.md` §11 y el diagrama del equipo dicen que el backend vive en **subredes
privadas** y que **nada del backend es alcanzable desde internet**. Es la frase que ordena todo
el diseño y la que se dice en la defensa.

Al ir a ejecutarlo apareció un hueco que ningún documento cubría:

> **API Gateway es un servicio administrado que vive FUERA de nuestra VPC.** Para alcanzar algo
> que está en una subred privada necesita un **VPC Link**, y el VPC Link de una HTTP API se
> conecta a través de un **balanceador** (ALB o NLB). Sin esa pieza, "backend privado + API
> Gateway" simplemente no se conecta.

O sea: el diseño escrito era correcto pero estaba **incompleto**, y el faltante no es gratis.

### Lo que cuesta la plomería

Tarifa pública de `us-east-1`, aproximada. Lo importante: **estas dos piezas cobran por hora
aunque el laboratorio esté cerrado**, a diferencia de las EC2, que el Learner Lab apaga solas.

| Pieza | ≈ por hora | ≈ por mes |
|---|---|---|
| NAT Gateway | USD 0,045 + 0,045/GB | ~USD 32 |
| ALB para el VPC Link | USD 0,0225 + LCU | ~USD 16 |

## Alternativas consideradas

- **B · Todo en la VPC por defecto.** La cuenta ya trae una VPC `172.31.0.0/16` con seis
  subredes, todas públicas. La EC2 con IP pública y API Gateway integrando por HTTP directo.
  Cuesta ~0 y se levanta en medio día. Se descarta porque **no hay diseño de red que defender**:
  el informe tendría que explicar por qué el backend está expuesto, y la respuesta sería
  "porque era más barato".
- **C · Híbrida sin NAT ni balanceador.** VPC propia con subredes públicas y privadas en dos
  zonas, RDS en la privada y la EC2 en la pública con un security group cerrado. Cuesta ~0 y
  permite decir con verdad que *la base de datos* no es alcanzable desde internet. Era la
  opción de menor riesgo, y se descarta por lo que dice la decisión.

## Decisión

**Opción A: la arquitectura completa.** VPC propia, subredes públicas y privadas en dos zonas
de disponibilidad, NAT Gateway, el backend en la privada y **API Gateway llegando por VPC Link
a través de un ALB**.

Las razones, en orden:

1. **El ramo es Cloud Native y lo que se evalúa es justificar decisiones de arquitectura.**
   Construir el diseño correcto y explicar su economía es una defensa más fuerte que construir
   el atajo y explicar por qué se tomó.
2. **En un proyecto real esta es la opción correcta y USD 50/mes es ruido.** Es lo que
   recomienda el Well-Architected Framework y lo que tiene cualquier cuenta seria: nadie deja
   la base de datos y el backend con IP pública para ahorrarse eso. El costo solo es un
   problema por el tope del crédito de Academy, que no es un presupuesto real.
3. **El crédito dejó de ser el riesgo principal.** Si se agota, se puede pedir otra cuenta. Lo
   que no se puede improvisar es **volver a levantar todo en la cuenta nueva**, y de ahí sale
   la condición de abajo.

### Dos ajustes sobre la opción A

- **ALB en vez de NLB** para el VPC Link. Cuesta prácticamente lo mismo y sus health checks son
  HTTP: cuando algo falla, dice qué falla. Con un NLB, API Gateway devuelve 503 sin explicar
  por qué, y eso es un día perdido en la semana de la entrega.
- **NAT Gateway y ALB se crean y se destruyen por script**, no viven encendidos 24/7. Se
  levantan los días que se trabaja y para la demo. De aquí a la entrega son ~10 días de uso
  real: **≈ USD 17 en vez de 50**, sin arriesgar el crédito de EP3 a EP6 y la EFT.
  Apagar lo que no se usa es un argumento de FinOps, y suma en la defensa.

### La condición que hace válida esta decisión

🔴 **Todo se crea por script, nada a mano en la consola.** Es lo que vuelve reversible el
apagado, lo que permite migrar a otra cuenta si esta se agota, y lo que hace que un reset del
laboratorio cueste un comando y no una tarde. Un recurso creado a mano es un recurso que no se
puede replicar: rompe la decisión completa.

## Consecuencias

- **Hay más piezas que pueden fallar**, y la que más falla es el health check del ALB: si el
  target queda `unhealthy`, API Gateway responde 503. Antes de conectar el VPC Link hay que ver
  el target `healthy` en la consola.
- **Levantar y bajar tiene un costo de tiempo**: crear un NAT Gateway toma un par de minutos y
  el ALB otros tantos. No se apaga cinco minutos antes de una demo.
- **Al recrear el ALB cambia su DNS**, así que la integración de API Gateway se vuelve a apuntar.
  Va dentro del mismo script para que no se olvide.
- **El costo hay que vigilarlo igual.** Si se olvida un NAT encendido un fin de semana largo,
  son ~USD 3. Si se olvida dos semanas, son ~USD 15. El script de apagado no es opcional.
- Las dos zonas de disponibilidad no son adorno: **RDS las exige** para crear el subnet group.

## Cómo se replica en otra cuenta

Si esta cuenta de Academy se agota o la resetean:

```bash
# 1. Credenciales nuevas en ~/.aws/credentials (Learner Lab -> AWS Details -> AWS CLI)
./tools/verificar-aws-academy.sh   # confirma que la cuenta nueva deja hacer todo
./tools/crear-cognito.sh           # identidad
./tools/crear-red.sh               # VPC, subredes, NAT, security groups
```

Ninguno de los tres pide editar nada: el nombre del dominio de Cognito se deriva del número de
cuenta y los IDs quedan en `cognito.env`, que no se versiona porque cambia con cada cuenta.
