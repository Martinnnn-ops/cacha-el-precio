# ADR-022 · Identidad de producto compartida entre tiendas

**Fecha:** 09-09-2026

**Estado:** **propuesta** — diseño acordado, sin implementar. No entra en el EP1.

## Contexto

La aplicación se llama «Cacha el Precio» y su promesa es comparar el precio de **la misma prenda**
entre tiendas. Hoy no lo hace, y no por un descuido de la interfaz: **el modelo de datos no permite
saber qué ofertas son la misma prenda.**

Cada fila de `product-service` es la publicación de una tienda:

```
id=1  store="H&M"   externalId="HM-001"  name="Polera básica de algodón"  price=8990
id=2  store="Zara"  externalId="ZA-114"  name="Polera básica de algodón"  price=12990
```

`externalId` es el código interno **de cada tienda**. Las dos filas de arriba son la misma prenda y
**no comparten ningún identificador**. Agrupar por `(store, externalId)` —que es lo que primero
parece— no junta nada: esa pareja identifica una oferta, no un producto. Sirve para que el scraper
no duplique al reinsertar, que es para lo que existe su índice único, y para nada más.

Las consecuencias son visibles y están medidas:

- **La aplicación lista ofertas sueltas.** El comparador enseña dos tarjetas «Polera básica de
  algodón» a precios distintos, como si fueran productos diferentes. El cálculo del más barato, el
  ahorro entre tiendas y el gráfico de historial **ya están escritos** en el frontend, y no se
  pueden usar porque nunca llega un producto con más de una oferta.
- **Colisionaban las URLs.** Las dos filas generaban el mismo slug y una de las dos ofertas no
  tenía ninguna dirección que llevara a ella. Se tapó añadiéndole la tienda al slug
  (`…-hym`, `…-zara`), que resuelve la alcanzabilidad pero deja dos URLs para una prenda.
- **El BFF no tiene nada que transformar.** Es la deuda que dejó abierta el
  [ADR-021](021-contrato-publico-en-el-bff.md): sus rutas aíslan el nombre y no la carga útil.
  Agrupar ofertas por producto es exactamente el trabajo que lo convertiría en un BFF de verdad.

## Alternativas consideradas

- **Agrupar por nombre normalizado (+ marca).** No cuesta casi nada y con datos de laboratorio
  funciona. Con datos reales del scraper, no: las tiendas no nombran igual la misma prenda
  —`"POLERA BÁSICA M/C ALGODÓN"` frente a `"Polera básica algodón"`—, así que junta de menos. Y
  cuando junta de más, mezcla dos prendas distintas y **muestra un precio que no corresponde**, que
  es el peor fallo posible en un comparador de precios: destruye justo la confianza que el producto
  vende.

- **Que el scraper decida la identidad al capturar.** Es donde más información hay: HTML completo,
  marca, código de fabricante, EAN cuando aparece. Pero mete lógica de dominio en un servicio cuya
  responsabilidad es extraer, y obliga a re-procesar todo el histórico cada vez que la heurística
  cambie.

- **Identidad explícita en el modelo, con emparejamiento asistido.** Un `Producto` con sus
  `Ofertas` colgando, y el emparejamiento como un proceso aparte que **propone** y una persona
  **confirma**. Es más trabajo, y es el único que no arriesga enseñar un precio equivocado.

- **No hacer nada y decirlo.** Presentar la aplicación como lo que es hoy: un catálogo unificado de
  ofertas, con el multi-tienda preparado y esperando la identidad.

## Decisión

Se adopta el **modelo explícito**, y **no se implementa para el EP1**.

### El modelo

```
Producto  { id, nombre, marca, categoria, descripcion, imagen }
              │
              └──< Oferta { id, productoId, tienda, externalId, precio,
                            precioLista, stock, url, tallas, capturadoEn }

  · (tienda, externalId) sigue siendo único   → idempotencia del scraper
  · una Oferta pertenece a un Producto        → identidad compartida
  · una Oferta sin Producto asignado es válida → «pendiente de emparejar»
```

Lo importante es la última línea. **Una oferta sin producto asignado es un estado legítimo**, no un
error: el catálogo sigue funcionando mientras el emparejamiento avanza, y cada oferta suelta se
comporta exactamente como hoy. Eso permite migrar sin apagar nada.

### Cómo se empareja

En dos pasos separados, y esta separación es la decisión de fondo:

1. **Proponer** — automático. Una heurística agrupa candidatos por marca + nombre normalizado
   (sin tildes, sin gramaje, sin color), y en cuanto haya EAN o código de fabricante, por ese
   campo, que es exacto. La propuesta **nunca se aplica sola**.
2. **Confirmar** — una persona. Una pantalla de administración enseña los candidatos y alguien
   dice sí o no. Requiere el grupo `admin`, que ya existe en Cognito.

**Por qué la confirmación humana y no automatizarlo entero:** el costo de los dos errores no es
simétrico. Si el sistema *no junta* dos ofertas que eran la misma prenda, el usuario ve dos
tarjetas parecidas: molesto, y él mismo lo resuelve mirando. Si el sistema *junta* dos prendas
distintas, la ficha enseña «desde $8.990» para algo que cuesta $12.990, el usuario hace clic, llega
a la tienda y descubre que le mentimos. Un comparador de precios que miente sobre un precio no
tiene nada más que ofrecer.

### Qué hace el BFF con esto

`GET /productos` pasa a devolver la prenda con sus ofertas dentro, ya ordenadas:

```json
{
  "id": 12,
  "nombre": "Polera básica de algodón",
  "marca": "Basement",
  "ofertas": [
    { "tienda": "H&M",  "precio": 8990,  "url": "…", "stock": true },
    { "tienda": "Zara", "precio": 12990, "url": "…", "stock": true }
  ]
}
```

Ahí el gateway deja de ser passthrough y **transforma de verdad**, que es lo que el ADR-021 dejó
pendiente. Y de paso resuelve tres cosas a la vez: el frontend puede comparar precios, el slug
vuelve a salir solo del nombre porque ya identifica una prenda, y el `sitemap.xml` deja de emitir
una URL por oferta.

### Por qué NO entra en el EP1

- **La entrega es el 13-09 y el code freeze el 10.** Esto es un cambio de modelo, una migración, un
  proceso de emparejamiento y una pantalla de administración.
- **Sin resolver la identidad, la maquinaria no sirve.** Programar la agrupación antes de decidir
  cómo se emparejan las ofertas es construir un embudo sin saber qué se le va a echar.
- **Lo que hay hoy se defiende mejor.** «Sabemos exactamente qué falta, por qué, y cuál es el
  diseño» es una respuesta de ingeniería. Una agrupación improvisada que junte mal es una demo que
  se cae en la primera pregunta.

## Consecuencias

### Positivas

- la aplicación cumpliría por fin lo que su nombre promete;
- el cálculo del más barato, el ahorro y el gráfico de historial **ya están escritos** en el
  frontend y empezarían a funcionar sin tocarlos;
- una URL por prenda, no por oferta: mejor para el usuario y para el buscador;
- le da al BFF una transformación real que defender;
- `price-service` recupera un motivo para existir, si algún día vuelve: el historial cuelga de la
  oferta, no del producto.

### Costos y riesgos

- **El emparejamiento nunca será perfecto**, y hay que diseñar la interfaz asumiendo que a veces se
  equivoca: poder deshacer, y que una prenda mal juntada se pueda separar;
- pedir confirmación humana **no escala** a miles de productos. Es aceptable mientras el catálogo
  sea de cientos; más allá hay que subir la confianza de la heurística y confirmar solo lo dudoso;
- migrar el modelo obliga a tocar `product-service`, el gateway y el frontend a la vez, que es
  justo lo que este equipo ya ha sufrido dos veces esta semana;
- mientras existan ofertas sin emparejar, **conviven dos formas** de mostrar un resultado, y la
  interfaz tiene que aguantar las dos sin parecer rota.

## Condiciones para revisar la decisión

- **se implementa después del 13-09**, con el scraper cargando datos reales de varias tiendas: sin
  esos datos no se puede medir si la heurística acierta;
- si aparece un EAN o un código de fabricante en el HTML de dos o más tiendas, la parte automática
  deja de ser una heurística y pasa a ser una clave: se revisa este ADR y se simplifica;
- si el catálogo crece hasta que confirmar a mano deje de ser viable, hay que decidir un umbral de
  confianza por encima del cual se empareja solo.
