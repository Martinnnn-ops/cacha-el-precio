namespace Product_Service.Models.Entities;

/// <summary>
/// Resumen del precio de UNA oferta (tienda + id externo) a lo largo del tiempo,
/// alimentado por el lector del stream de precios (Kinesis). De aquí sale el
/// "precio habitual" que la ficha muestra como «bajo / dentro / sobre su rango».
///
/// Se guardan agregados acumulables (cuántas muestras, su suma y su suma de
/// cuadrados) en vez de la serie completa: con eso se calcula promedio y
/// desviación sin conservar un historial gigante, y el lector solo suma la
/// muestra nueva. La idempotencia la da la tabla de eventos procesados: cada
/// precio se suma una sola vez aunque Kinesis lo entregue dos veces.
/// </summary>
public sealed class EstadisticaPrecio
{
    public required string Store { get; set; }
    public required string ExternalId { get; set; }

    /// <summary>Cuántos precios se han sumado.</summary>
    public long Muestras { get; set; }

    /// <summary>Suma de los precios. Promedio = Suma / Muestras.</summary>
    public long Suma { get; set; }

    /// <summary>Suma de los precios al cuadrado. Sirve para la desviación.</summary>
    public decimal SumaCuadrados { get; set; }

    public int Minimo { get; set; }
    public int Maximo { get; set; }

    /// <summary>El precio de la captura más reciente.</summary>
    public int UltimoPrecio { get; set; }

    /// <summary>Cuándo se capturó el precio más reciente.</summary>
    public DateTimeOffset UltimaCaptura { get; set; }
}
