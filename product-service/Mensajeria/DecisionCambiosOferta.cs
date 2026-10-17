using Product_Service.Models.Eventos;

namespace Product_Service.Mensajeria;

/// <summary>
/// Decide, de forma <b>pura</b> (sin EF ni AWS), qué eventos del flujo 2
/// corresponden al comparar la oferta guardada con la que llega. Separada a
/// propósito para poder probarla sola: baja de precio, subida, agotada,
/// repuesta, baja y agotada a la vez, y oferta nueva (ver docs/MENSAJERIA.md).
/// </summary>
public static class DecisionCambiosOferta
{
    /// <param name="comunes">Campos comunes del <c>data</c> (sin los de precio).</param>
    /// <param name="precioAnterior">Precio guardado antes del upsert.</param>
    /// <param name="activaAntes">Disponibilidad guardada antes del upsert.</param>
    /// <param name="precioNuevo">Precio que llega.</param>
    /// <param name="activaAhora">Disponibilidad que llega.</param>
    public static IReadOnlyList<(string Tipo, DatosCambioOferta Datos)> Evaluar(
        DatosCambioOferta comunes,
        int precioAnterior,
        bool activaAntes,
        int precioNuevo,
        bool activaAhora)
    {
        var cambios = new List<(string, DatosCambioOferta)>();

        if (precioNuevo < precioAnterior)
        {
            int descuento = (int)Math.Round((precioAnterior - precioNuevo) * 100.0 / precioAnterior);
            cambios.Add((TiposEvento.PrecioBajo, comunes with
            {
                PreviousPrice = precioAnterior,
                NewPrice = precioNuevo,
                DiscountPct = descuento
            }));
        }

        if (activaAntes && !activaAhora)
        {
            cambios.Add((TiposEvento.OfertaAgotada, comunes with { LastPrice = precioNuevo }));
        }
        else if (!activaAntes && activaAhora)
        {
            cambios.Add((TiposEvento.OfertaRepuesta, comunes with { Price = precioNuevo }));
        }

        return cambios;
    }
}
