// Texto de apoyo de cada categoría en la portada. Si falta uno, la tarjeta cae
// en el número de productos, así que añadir una categoría nueva no rompe nada.
export const LEMAS_CATEGORIA = {
  Poleras: 'El básico que más se compara',
  Pantalones: 'Calce y precio, tienda por tienda',
  Chaquetas: 'Abrigo de entretiempo sin pagar de más',
  Vestidos: 'De diario y de salir',
  Camisas: 'Oxford, lino y popelina',
  Abrigos: 'La compra grande de la temporada',
}

export function lemaCategoria(categoria, cantidad) {
  return (
    LEMAS_CATEGORIA[categoria] ??
    `${cantidad} ${cantidad === 1 ? 'producto' : 'productos'}`
  )
}
