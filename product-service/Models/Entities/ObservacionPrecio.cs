namespace Product_Service.Models.Entities;

/// <summary>
/// Cada precio capturado de una oferta, tal como llegó por Kinesis (o por el
/// relleno inicial con el historial que ya existía). Es lo que dibuja el
/// gráfico de la ficha.
///
/// Por qué no basta con <c>scraper.price_history</c>: esa tabla solo guarda los
/// CAMBIOS de precio, así que una prenda que costó lo mismo un mes entero tiene
/// una sola fila y el gráfico no sabría que hubo 30 días de precio estable.
///
/// La llave (tienda, id, fecha) hace que repetir una captura no la duplique.
/// </summary>
public sealed class ObservacionPrecio
{
    public required string Store { get; set; }
    public required string ExternalId { get; set; }
    public DateTimeOffset CapturadoEn { get; set; }
    public int Precio { get; set; }
}
