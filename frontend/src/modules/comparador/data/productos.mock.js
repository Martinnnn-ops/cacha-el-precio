// Datos de ejemplo para trabajar sin backend. Los consume comparador.service
// sólo cuando VITE_API_BASE_URL no está definida.

export const PRODUCTOS_MOCK = [
  {
    id: '1',
    descripcion:
      'Una polera lisa de algodón peinado, el tipo de prenda que se compra para no pensar en ella. El tejido es de gramaje medio: abriga poco, así que sirve sola en verano y de primera capa el resto del año. El cuello lleva ribete elástico, que es lo que evita que se dé de sí a los pocos lavados. Está en cuatro de las cinco tiendas que seguimos, con casi $4.000 de diferencia entre la más barata y la más cara.',
    codigo: 'PB-ALG-01',
    specs: [
      {
        'grupo': 'Materiales y confección',
        'filas': {
          'Material': '100 % algodón peinado',
          'Cuello': 'Redondo, ribete elástico',
          'Corte': 'Regular',
          'Origen': 'Confeccionado en Perú'
        }
      },
      {
        'grupo': 'Cuidado',
        'notas': 'El algodón peinado encoge algo en el primer lavado. Si dudas entre dos tallas, la mayor.',
        'filas': {
          'Lavado': 'A máquina, máximo 30°',
          'Secado': 'A la sombra, no en secadora',
          'Plancha': 'Temperatura media, del revés'
        }
      }
    ],
    destacadas: [
      'Algodón peinado: no da picor ni se llena de bolitas',
      'Aguanta lavados sin deformarse el cuello',
      'Está en cuatro tiendas, así que casi siempre hay talla'
    ],
    pros: [
      'El algodón peinado no da picor',
      'Aguanta lavados sin deformarse',
      'Buena relación precio-calidad',
    ],
    contras: [
      'Encoge un poco el primer lavado',
      'Colores claros transparentan',
    ],
    agregadoHace: 2,
    vistas: 4820,
    nombre: 'Polera básica algodón orgánico',
    marca: 'Basement',
    categoria: 'Poleras',
    imagen: null,
    precios: [
      { tienda: 'ripley', precio: 9990, precioLista: 14990, stock: true, url: 'https://simple.ripley.cl/p/1', medioPago: 'Tarjeta Ripley', condicion: 'nueva', tallas: ['XS', 'S', 'M', 'L'] },
      { tienda: 'paris', precio: 12990, precioLista: 12990, stock: true, url: 'https://www.paris.cl/p/1', medioPago: 'Tarjeta Cencosud', condicion: 'nueva', tallas: ['XS', 'XL'] },
      { tienda: 'hym', precio: 8990, precioLista: 12990, stock: true, url: 'https://www2.hm.com/es_cl/p/1', medioPago: 'Con todo medio de pago', condicion: 'nueva', tallas: ['XS', 'M', 'L', 'XL'] },
      { tienda: 'zara', precio: 15990, precioLista: 15990, stock: false, url: 'https://www.zara.com/cl/p/1', medioPago: 'Con todo medio de pago', condicion: 'nueva', tallas: ['XS', 'L'] },
    ],
    historial: [
      { fecha: '2026-07-01', precios: { ripley: 14990, paris: 13990, hym: 12990 } },
      { fecha: '2026-07-15', precios: { ripley: 12990, paris: 13990, hym: 10990 } },
      { fecha: '2026-08-01', precios: { ripley: 11990, paris: 12990, hym: 9990 } },
      { fecha: '2026-08-15', precios: { ripley: 9990, paris: 12990, hym: 8990 } },
    ],
  },
  {
    id: '2',
    descripcion:
      'Jeans de corte slim y tiro medio, con un 2 % de elastano que da movilidad sin que la prenda pierda la forma. El denim es de peso medio, así que no es de los rígidos que tardan semanas en ceder. El tiro medio es el que menos discrimina siluetas. Ojo con la talla: al ser slim queda justo, y la mayoría de la gente acaba pidiendo una más.',
    codigo: 'JN-SLM-02',
    specs: [
      {
        'grupo': 'Tejido y corte',
        'filas': {
          'Material': '98 % algodón, 2 % elastano',
          'Tiro': 'Medio',
          'Corte': 'Slim',
          'Cierre': 'Botón metálico y cremallera'
        }
      },
      {
        'grupo': 'Cuidado',
        'notas': 'Los primeros lavados destiñen. Lávalos solos o con ropa oscura.',
        'filas': {
          'Lavado': 'A máquina, del revés',
          'Secado': 'A la sombra'
        }
      }
    ],
    destacadas: [
      'El elastano da movilidad sin perder la forma',
      'El tiro medio favorece a casi todas las siluetas',
      'Cuatro tiendas lo tienen, con $4.000 de diferencia entre ellas'
    ],
    pros: [
      'El elastano da movilidad',
      'El tiro medio favorece a casi todos',
    ],
    contras: [
      'Talla justa: conviene pedir una más',
      'El azul destiñe al principio',
    ],
    agregadoHace: 9,
    vistas: 12450,
    nombre: 'Jeans slim tiro medio',
    marca: 'Zara',
    categoria: 'Pantalones',
    imagen: null,
    precios: [
      { tienda: 'zara', precio: 29990, precioLista: 39990, stock: true, url: 'https://www.zara.com/cl/p/2', medioPago: 'Con todo medio de pago', condicion: 'nueva', tallas: ['XS', 'S', 'M', 'L', 'XL'] },
      { tienda: 'mango', precio: 34990, precioLista: 34990, stock: true, url: 'https://shop.mango.com/cl/p/2', medioPago: 'Con todo medio de pago', condicion: 'nueva', tallas: ['XS', 'S'] },
      { tienda: 'paris', precio: 27990, precioLista: 35990, stock: true, url: 'https://www.paris.cl/p/2', medioPago: 'Tarjeta Cencosud', condicion: 'nueva', tallas: ['XS', 'S', 'M', 'L', 'XL'] },
      { tienda: 'ripley', precio: 31990, precioLista: 37990, stock: true, url: 'https://simple.ripley.cl/p/2', medioPago: 'Tarjeta Ripley', condicion: 'nueva', tallas: ['XS', 'S', 'M', 'L', 'XL'] },
    ],
    historial: [
      { fecha: '2026-07-01', precios: { zara: 39990, mango: 36990, paris: 35990 } },
      { fecha: '2026-07-15', precios: { zara: 34990, mango: 34990, paris: 32990 } },
      { fecha: '2026-08-01', precios: { zara: 32990, mango: 34990, paris: 29990 } },
      { fecha: '2026-08-15', precios: { zara: 29990, mango: 34990, paris: 27990 } },
    ],
  },
  {
    id: '3',
    descripcion:
      'Blazer entallado en mezcla de lino y viscosa, pensado para entretiempo más que para el frío. El lino transpira y la viscosa le da caída, así que no queda tan acartonado como un blazer de traje. Lleva forro sólo en las mangas, lo que lo hace bastante más ligero. Se arruga, y eso es parte de cómo se ve: si buscas algo que aguante impecable todo el día, este no es.',
    codigo: 'BZ-LIN-03',
    specs: [
      {
        'grupo': 'Tejido y confección',
        'filas': {
          'Material': '55 % lino, 45 % viscosa',
          'Forro': 'Interior en las mangas',
          'Botones': 'Dos, tono sobre tono',
          'Corte': 'Entallado'
        }
      },
      {
        'grupo': 'Cuidado',
        'notas': 'El lino se arruga: es parte de cómo cae. Si buscas algo que no se arrugue, este no es.',
        'filas': {
          'Lavado': 'En seco recomendado',
          'Plancha': 'Con vapor, aún húmedo'
        }
      }
    ],
    destacadas: [
      'El lino transpira de verdad en verano',
      'Cae bien sin necesidad de plancharlo mucho',
      'Sube y baja de precio: vale la pena mirar el historial'
    ],
    pros: [
      'El lino transpira en verano',
      'Cae bien sin planchar mucho',
    ],
    contras: [
      'Se arruga con facilidad',
      'El forro da calor en días muy calurosos',
    ],
    agregadoHace: 1,
    vistas: 2310,
    nombre: 'Blazer entallado de lino',
    marca: 'Mango',
    categoria: 'Chaquetas',
    imagen: null,
    precios: [
      { tienda: 'mango', precio: 59990, precioLista: 79990, stock: true, url: 'https://shop.mango.com/cl/p/3', medioPago: 'Con todo medio de pago', condicion: 'nueva', tallas: ['S', 'XL'] },
      { tienda: 'ripley', precio: 64990, precioLista: 74990, stock: true, url: 'https://simple.ripley.cl/p/3', medioPago: 'Tarjeta Ripley', condicion: 'nueva', tallas: ['XS', 'M', 'L', 'XL'] },
      { tienda: 'zara', precio: 55990, precioLista: 69990, stock: true, url: 'https://www.zara.com/cl/p/3', medioPago: 'Con todo medio de pago', condicion: 'nueva', tallas: ['S', 'L', 'XL'] },
      { tienda: 'paris', precio: 71990, precioLista: 71990, stock: false, url: 'https://www.paris.cl/p/3', medioPago: 'Tarjeta Cencosud', condicion: 'nueva', tallas: ['XS', 'S', 'M', 'L', 'XL'] },
    ],
    historial: [
      { fecha: '2026-07-01', precios: { mango: 79990, ripley: 74990, zara: 69990 } },
      { fecha: '2026-07-15', precios: { mango: 71990, ripley: 69990, zara: 64990 } },
      { fecha: '2026-08-01', precios: { mango: 64990, ripley: 66990, zara: 59990 } },
      { fecha: '2026-08-15', precios: { mango: 59990, ripley: 64990, zara: 55990 } },
    ],
  },
  {
    id: '4',
    codigo: 'VS-MID-04',
    specs: [
      {
        'grupo': 'Tejido y corte',
        'filas': {
          'Material': 'Poliéster',
          'Corte': 'Holgado',
          'Largo': 'Midi',
          'Forro': 'Sí'
        }
      },
      {
        'grupo': 'Cuidado',
        'filas': {
          'Lavado': 'A máquina, del revés',
          'Plancha': 'Temperatura baja'
        }
      }
    ],
    pros: [
      'El plisado aguanta bien el lavado',
      'Cae sin necesidad de plancharlo'
    ],
    contras: [
      'El poliéster transpira poco en verano'
    ],
    agregadoHace: 5,
    vistas: 8790,
    nombre: 'Vestido midi plisado',
    marca: 'H&M',
    categoria: 'Vestidos',
    imagen: null,
    precios: [
      { tienda: 'hym', precio: 24990, precioLista: 32990, stock: true, url: 'https://www2.hm.com/es_cl/p/4', medioPago: 'Con todo medio de pago', condicion: 'nueva', tallas: ['XS', 'S', 'M'] },
      { tienda: 'mango', precio: 39990, precioLista: 39990, stock: true, url: 'https://shop.mango.com/cl/p/4', medioPago: 'Con todo medio de pago', condicion: 'nueva', tallas: ['S', 'M', 'L', 'XL'] },
    ],
    historial: [
      { fecha: '2026-07-01', precios: { hym: 32990, mango: 42990 } },
      { fecha: '2026-07-15', precios: { hym: 29990, mango: 39990 } },
      { fecha: '2026-08-01', precios: { hym: 27990, mango: 39990 } },
      { fecha: '2026-08-15', precios: { hym: 24990, mango: 39990 } },
    ],
  },
  {
    id: '5',
    codigo: 'CM-OXF-05',
    specs: [
      {
        'grupo': 'Tejido y corte',
        'filas': {
          'Material': 'Algodón',
          'Corte': 'Regular',
          'Cuello': 'Button-down',
          'Puño': 'Sencillo'
        }
      },
      {
        'grupo': 'Cuidado',
        'filas': {
          'Lavado': 'A máquina, 40°',
          'Plancha': 'Temperatura media'
        }
      }
    ],
    pros: [
      'El oxford aguanta años',
      'Sirve con corbata y sin ella'
    ],
    contras: [
      'Hay que plancharla siempre'
    ],
    agregadoHace: 14,
    vistas: 15630,
    nombre: 'Camisa oxford manga larga',
    marca: 'Paris',
    categoria: 'Camisas',
    imagen: null,
    precios: [
      { tienda: 'paris', precio: 19990, precioLista: 27990, stock: true, url: 'https://www.paris.cl/p/5', medioPago: 'Tarjeta Cencosud', condicion: 'nueva', tallas: ['XS', 'S', 'L', 'XL'] },
      { tienda: 'ripley', precio: 22990, precioLista: 26990, stock: true, url: 'https://simple.ripley.cl/p/5', medioPago: 'Tarjeta Ripley', condicion: 'nueva', tallas: ['S', 'M', 'L'] },
      { tienda: 'hym', precio: 21990, precioLista: 21990, stock: true, url: 'https://www2.hm.com/es_cl/p/5', medioPago: 'Con todo medio de pago', condicion: 'nueva', tallas: ['XS', 'S', 'M', 'L', 'XL'] },
      { tienda: 'zara', precio: 25990, precioLista: 25990, stock: true, url: 'https://www.zara.com/cl/p/5', medioPago: 'Con todo medio de pago', condicion: 'nueva', tallas: ['S', 'M', 'L', 'XL'] },
      { tienda: 'mango', precio: 28990, precioLista: 28990, stock: false, url: 'https://shop.mango.com/cl/p/5', medioPago: 'Con todo medio de pago', condicion: 'nueva', tallas: ['XS', 'M'] },
    ],
    historial: [
      { fecha: '2026-07-01', precios: { paris: 27990, ripley: 26990, hym: 24990 } },
      { fecha: '2026-07-15', precios: { paris: 24990, ripley: 24990, hym: 22990 } },
      { fecha: '2026-08-01', precios: { paris: 21990, ripley: 23990, hym: 21990 } },
      { fecha: '2026-08-15', precios: { paris: 19990, ripley: 22990, hym: 21990 } },
    ],
  },
  {
    id: '6',
    codigo: 'AB-PAN-06',
    specs: [
      {
        'grupo': 'Tejido y corte',
        'filas': {
          'Material': 'Lana',
          'Corte': 'Recto',
          'Largo': 'Largo',
          'Forro': 'Sí'
        }
      },
      {
        'grupo': 'Cuidado',
        'notas': 'La lana no se lava en casa: se apelmaza y encoge. Tintorería.',
        'filas': {
          'Lavado': 'En seco',
          'Plancha': 'Con vapor'
        }
      }
    ],
    pros: [
      'La lana abriga de verdad, no sólo tapa',
      'El largo cubre hasta media pierna'
    ],
    contras: [
      'Lavarlo en seco sale caro',
      'Pesa bastante'
    ],
    agregadoHace: 3,
    vistas: 6180,
    nombre: 'Abrigo largo de paño',
    marca: 'Zara',
    categoria: 'Abrigos',
    imagen: null,
    precios: [
      { tienda: 'zara', precio: 79990, precioLista: 99990, stock: false, url: 'https://www.zara.com/cl/p/6', medioPago: 'Con todo medio de pago', condicion: 'nueva', tallas: ['XS', 'S', 'M', 'L', 'XL'] },
      { tienda: 'mango', precio: 89990, precioLista: 109990, stock: true, url: 'https://shop.mango.com/cl/p/6', medioPago: 'Con todo medio de pago', condicion: 'nueva', tallas: ['XS', 'S', 'M', 'L'] },
      { tienda: 'ripley', precio: 94990, precioLista: 94990, stock: true, url: 'https://simple.ripley.cl/p/6', medioPago: 'Tarjeta Ripley', condicion: 'nueva', tallas: ['XS', 'S', 'L', 'XL'] },
    ],
    historial: [
      { fecha: '2026-07-01', precios: { zara: 99990, mango: 109990, ripley: 104990 } },
      { fecha: '2026-07-15', precios: { zara: 94990, mango: 99990, ripley: 99990 } },
      { fecha: '2026-08-01', precios: { zara: 84990, mango: 94990, ripley: 94990 } },
      { fecha: '2026-08-15', precios: { zara: 79990, mango: 89990, ripley: 94990 } },
    ],
  },
  // ——— Cabeza ———
  {
    id: '7',
    codigo: 'GR-LAN-07',
    specs: [
      {
        'grupo': 'Tejido y corte',
        'filas': {
          'Material': 'Lana',
          'Corte': 'Ajustado',
          'Forro': 'No'
        }
      },
      {
        'grupo': 'Cuidado',
        'filas': {
          'Lavado': 'A mano',
          'Secado': 'En plano'
        }
      }
    ],
    pros: [
      'Abriga sin dar picor',
      'La vuelta se puede subir o bajar'
    ],
    contras: [
      'A mano o se apelmaza'
    ],
    agregadoHace: 4,
    vistas: 3210,
    nombre: 'Gorro de lana con vuelta',
    marca: 'Basement',
    categoria: 'Gorros',
    imagen: null,
    precios: [
      { tienda: 'ripley', precio: 7990, precioLista: 11990, stock: true, url: 'https://simple.ripley.cl/p/7', medioPago: 'Tarjeta Ripley', condicion: 'nueva', tallas: ['S', 'M', 'XL'] },
      { tienda: 'hym', precio: 6990, precioLista: 9990, stock: true, url: 'https://www2.hm.com/es_cl/p/7', medioPago: 'Con todo medio de pago', condicion: 'nueva', tallas: ['S', 'L', 'XL'] },
      { tienda: 'zara', precio: 9990, precioLista: 9990, stock: true, url: 'https://www.zara.com/cl/p/7', medioPago: 'Con todo medio de pago', condicion: 'nueva', tallas: ['S', 'L'] },
    ],
    historial: [
      { fecha: '2026-07-01', precios: { ripley: 11990, hym: 9990, zara: 12990 } },
      { fecha: '2026-08-15', precios: { ripley: 7990, hym: 6990, zara: 9990 } },
    ],
  },
  {
    id: '8',
    codigo: 'JK-ALG-08',
    specs: [
      {
        'grupo': 'Tejido y corte',
        'filas': {
          'Material': 'Algodón',
          'Corte': 'Regular',
          'Forro': 'No'
        }
      },
      {
        'grupo': 'Cuidado',
        'filas': {
          'Lavado': 'A mano',
          'Secado': 'A la sombra'
        }
      }
    ],
    pros: [
      'Visera rígida, no se dobla en la maleta',
      'Cierre regulable atrás'
    ],
    contras: [
      'El algodón claro se ensucia rápido'
    ],
    agregadoHace: 11,
    vistas: 1890,
    nombre: 'Jockey de algodón liso',
    marca: 'Paris',
    categoria: 'Jockeys',
    imagen: null,
    precios: [
      { tienda: 'paris', precio: 8990, precioLista: 12990, stock: true, url: 'https://www.paris.cl/p/8', medioPago: 'Tarjeta Cencosud', condicion: 'nueva', tallas: ['XS', 'S', 'M', 'L', 'XL'] },
      { tienda: 'ripley', precio: 10990, precioLista: 10990, stock: true, url: 'https://simple.ripley.cl/p/8', medioPago: 'Tarjeta Ripley', condicion: 'nueva', tallas: ['XS', 'S', 'M', 'L', 'XL'] },
      { tienda: 'mango', precio: 14990, precioLista: 14990, stock: false, url: 'https://shop.mango.com/cl/p/8', medioPago: 'Con todo medio de pago', condicion: 'ultima-talla', tallas: ['S', 'M', 'XL'] },
    ],
    historial: [
      { fecha: '2026-07-01', precios: { paris: 12990, ripley: 12990 } },
      { fecha: '2026-08-15', precios: { paris: 8990, ripley: 10990 } },
    ],
  },

  // ——— Pies ———
  {
    id: '9',
    descripcion:
      'Zapatillas de lona con suela de caucho vulcanizado, el modelo de siempre. Ligeras y frescas, van bien para ciudad y días secos. La plantilla sale, así que se puede cambiar por una con amortiguación si vas a caminar mucho — de fábrica traen poca. La lona no es impermeable: con lluvia se calan y tardan en secar.',
    codigo: 'ZP-LON-09',
    specs: [
      {
        'grupo': 'Construcción',
        'filas': {
          'Exterior': 'Lona de algodón',
          'Suela': 'Caucho vulcanizado',
          'Cierre': 'Cordones',
          'Plantilla': 'Extraíble'
        }
      },
      {
        'grupo': 'Uso',
        'notas': 'La lona no es impermeable: se cala con lluvia y tarda en secar.',
        'filas': {
          'Terreno': 'Ciudad, seco',
          'Amortiguación': 'Mínima, no para caminar mucho'
        }
      }
    ],
    destacadas: [
      'Ligeras y frescas para el día a día',
      'La suela agarra bien en seco',
      'La plantilla sale, así que se puede cambiar por una con amortiguación'
    ],
    pros: [
      'Ligeras y frescas',
      'La suela agarra bien en seco',
    ],
    contras: [
      'La lona se moja enseguida',
      'Sin amortiguación para caminar mucho',
    ],
    agregadoHace: 6,
    vistas: 14200,
    nombre: 'Zapatillas urbanas de lona',
    marca: 'Sparta',
    categoria: 'Zapatillas',
    imagen: null,
    precios: [
      { tienda: 'ripley', precio: 24990, precioLista: 34990, stock: true, url: 'https://simple.ripley.cl/p/9', medioPago: 'Tarjeta Ripley', condicion: 'nueva', tallas: ['38', '39', '41', '42'] },
      { tienda: 'paris', precio: 27990, precioLista: 32990, stock: true, url: 'https://www.paris.cl/p/9', medioPago: 'Tarjeta Cencosud', condicion: 'nueva', tallas: ['38', '39', '40', '41', '42'] },
      { tienda: 'hym', precio: 22990, precioLista: 29990, stock: false, url: 'https://www2.hm.com/es_cl/p/9', medioPago: 'Con todo medio de pago', condicion: 'ultima-talla', tallas: ['38', '39', '40', '42', '43'] },
    ],
    historial: [
      { fecha: '2026-07-01', precios: { ripley: 34990, paris: 32990, hym: 29990 } },
      { fecha: '2026-08-15', precios: { ripley: 24990, paris: 27990, hym: 22990 } },
    ],
  },
  {
    id: '10',
    codigo: 'ZP-DEP-10',
    specs: [
      {
        'grupo': 'Construcción',
        'filas': {
          'Exterior': 'Malla técnica',
          'Suela': 'Goma EVA',
          'Cierre': 'Cordones',
          'Plantilla': 'Extraíble'
        }
      },
      {
        'grupo': 'Uso',
        'filas': {
          'Terreno': 'Asfalto y cinta',
          'Amortiguación': 'Alta, con cámara de aire'
        }
      }
    ],
    pros: [
      'La cámara de aire se nota en distancias largas',
      'La malla transpira'
    ],
    contras: [
      'La malla se ensucia y cuesta limpiarla'
    ],
    agregadoHace: 2,
    vistas: 9640,
    nombre: 'Zapatillas deportivas con cámara de aire',
    marca: 'Sparta',
    categoria: 'Zapatillas',
    imagen: null,
    precios: [
      { tienda: 'ripley', precio: 54990, precioLista: 69990, stock: true, url: 'https://simple.ripley.cl/p/10', medioPago: 'Tarjeta Ripley', condicion: 'nueva', tallas: ['38', '39', '40', '41', '42', '43'] },
      { tienda: 'paris', precio: 49990, precioLista: 64990, stock: true, url: 'https://www.paris.cl/p/10', medioPago: 'Tarjeta Cencosud', condicion: 'nueva', tallas: ['38', '39', '41', '43'] },
      { tienda: 'zara', precio: 59990, precioLista: 59990, stock: true, url: 'https://www.zara.com/cl/p/10', medioPago: 'Con todo medio de pago', condicion: 'nueva', tallas: ['38', '40', '42'] },
    ],
    historial: [
      { fecha: '2026-07-01', precios: { ripley: 69990, paris: 64990, zara: 64990 } },
      { fecha: '2026-08-15', precios: { ripley: 54990, paris: 49990, zara: 59990 } },
    ],
  },
  {
    id: '11',
    codigo: 'ZP-CUE-11',
    specs: [
      {
        'grupo': 'Construcción',
        'filas': {
          'Exterior': 'Cuero',
          'Suela': 'Goma',
          'Cierre': 'Cordones',
          'Plantilla': 'Fija'
        }
      },
      {
        'grupo': 'Uso',
        'notas': 'El cuero necesita rodaje: las primeras semanas rozan.',
        'filas': {
          'Terreno': 'Ciudad, oficina',
          'Amortiguación': 'Media'
        }
      }
    ],
    pros: [
      'El cuero mejora con el uso',
      'Se pueden resolar'
    ],
    contras: [
      'Piden rodaje',
      'No valen para lluvia fuerte'
    ],
    agregadoHace: 8,
    vistas: 5120,
    nombre: 'Zapatos de cuero con cordones',
    marca: 'Mango',
    categoria: 'Zapatos',
    imagen: null,
    precios: [
      { tienda: 'mango', precio: 64990, precioLista: 84990, stock: true, url: 'https://shop.mango.com/cl/p/11', medioPago: 'Con todo medio de pago', condicion: 'nueva', tallas: ['38', '39', '42'] },
      { tienda: 'paris', precio: 71990, precioLista: 79990, stock: true, url: 'https://www.paris.cl/p/11', medioPago: 'Tarjeta Cencosud', condicion: 'nueva', tallas: ['38', '40', '42'] },
    ],
    historial: [
      { fecha: '2026-07-01', precios: { mango: 84990, paris: 79990 } },
      { fecha: '2026-08-15', precios: { mango: 64990, paris: 71990 } },
    ],
  },

  // ——— más torso y piernas, para que haya de dónde elegir ———
  {
    id: '12',
    codigo: 'CH-ACO-12',
    specs: [
      {
        'grupo': 'Tejido y corte',
        'filas': {
          'Material': 'Poliéster',
          'Corte': 'Regular',
          'Forro': 'Sí'
        }
      },
      {
        'grupo': 'Cuidado',
        'filas': {
          'Lavado': 'A máquina, 30°',
          'Secado': 'A la sombra'
        }
      }
    ],
    pros: [
      'Abriga el tronco sin estorbar los brazos',
      'Se comprime para la mochila'
    ],
    contras: [
      'Sin mangas, no basta en pleno invierno'
    ],
    agregadoHace: 7,
    vistas: 6730,
    nombre: 'Chaleco acolchado sin mangas',
    marca: 'H&M',
    categoria: 'Chalecos',
    imagen: null,
    precios: [
      { tienda: 'hym', precio: 34990, precioLista: 44990, stock: true, url: 'https://www2.hm.com/es_cl/p/12', medioPago: 'Con todo medio de pago', condicion: 'nueva', tallas: ['S', 'L'] },
      { tienda: 'ripley', precio: 39990, precioLista: 39990, stock: true, url: 'https://simple.ripley.cl/p/12', medioPago: 'Tarjeta Ripley', condicion: 'nueva', tallas: ['M', 'L', 'XL'] },
    ],
    historial: [
      { fecha: '2026-07-01', precios: { hym: 44990, ripley: 42990 } },
      { fecha: '2026-08-15', precios: { hym: 34990, ripley: 39990 } },
    ],
  },
  {
    id: '13',
    codigo: 'FL-MID-13',
    specs: [
      {
        'grupo': 'Tejido y corte',
        'filas': {
          'Material': 'Poliéster',
          'Corte': 'Holgado',
          'Largo': 'Midi',
          'Forro': 'Sí'
        }
      },
      {
        'grupo': 'Cuidado',
        'filas': {
          'Lavado': 'A máquina, del revés',
          'Plancha': 'Temperatura baja'
        }
      }
    ],
    pros: [
      'El plisado no se pierde con el uso',
      'Cintura elástica'
    ],
    contras: [
      'El poliéster da calor en verano'
    ],
    agregadoHace: 13,
    vistas: 4410,
    nombre: 'Falda midi plisada',
    marca: 'Zara',
    categoria: 'Faldas',
    imagen: null,
    precios: [
      { tienda: 'zara', precio: 22990, precioLista: 29990, stock: true, url: 'https://www.zara.com/cl/p/13', medioPago: 'Con todo medio de pago', condicion: 'nueva', tallas: ['XS', 'S', 'L', 'XL'] },
      { tienda: 'mango', precio: 26990, precioLista: 26990, stock: true, url: 'https://shop.mango.com/cl/p/13', medioPago: 'Con todo medio de pago', condicion: 'nueva', tallas: ['XS', 'S', 'M', 'L', 'XL'] },
      { tienda: 'hym', precio: 19990, precioLista: 24990, stock: true, url: 'https://www2.hm.com/es_cl/p/13', medioPago: 'Con todo medio de pago', condicion: 'nueva', tallas: ['M', 'XL'] },
    ],
    historial: [
      { fecha: '2026-07-01', precios: { zara: 29990, mango: 28990, hym: 24990 } },
      { fecha: '2026-08-15', precios: { zara: 22990, mango: 26990, hym: 19990 } },
    ],
  },
]
