using Product_Service.Models.Entities;

namespace Product_Service.Streaming;

/// <summary>Dónde cae un precio respecto de lo habitual para esa oferta.</summary>
public enum UbicacionPrecio
{
    /// <summary>Todavía no hay muestras suficientes para opinar.</summary>
    Insuficiente,
    Bajo,
    Dentro,
    Sobre
}

/// <summary>El rango habitual de una oferta: [piso, techo] y si es confiable.</summary>
public readonly record struct RangoHabitual(int Piso, int Techo, bool Suficiente);

/// <summary>
/// El cálculo del "precio habitual", en funciones PURAS: no tocan la base ni
/// Kinesis, así que se prueban solas. El lector guarda el resultado en
/// <see cref="EstadisticaPrecio"/> y la ficha lo lee (docs/PLAN-EFT.md §5).
///
/// El rango habitual es promedio ± una desviación estándar, recortado al mínimo
/// y máximo vistos. Un precio por debajo del piso es una buena oferta ("bajo");
/// por encima del techo, caro ("sobre"); en medio, "dentro de lo habitual".
/// </summary>
public static class EstadisticasPrecioHabitual
{
    /// <summary>
    /// Mínimo de muestras para dar un rango. Con menos, la ficha muestra
    /// "historial insuficiente" en vez de un rango que miente con 2 datos.
    /// </summary>
    public const int MuestrasMinimas = 5;

    /// <summary>
    /// Suma una muestra a las estadísticas y devuelve el objeto actualizado.
    /// Función pura: no guarda nada, solo modifica la entidad que recibe (la
    /// misma que el lector persiste después).
    /// </summary>
    public static EstadisticaPrecio Acumular(EstadisticaPrecio est, int precio, DateTimeOffset capturadoEn)
    {
        if (est.Muestras == 0)
        {
            est.Minimo = precio;
            est.Maximo = precio;
            est.UltimoPrecio = precio;
            est.UltimaCaptura = capturadoEn;
        }
        else
        {
            if (precio < est.Minimo) est.Minimo = precio;
            if (precio > est.Maximo) est.Maximo = precio;
            // El "último precio" es el de la captura más reciente por fecha, no el
            // último en llegar: los registros pueden desordenarse entre shards.
            if (capturadoEn >= est.UltimaCaptura)
            {
                est.UltimoPrecio = precio;
                est.UltimaCaptura = capturadoEn;
            }
        }

        est.Muestras += 1;
        est.Suma += precio;
        est.SumaCuadrados += (decimal)precio * precio;
        return est;
    }

    public static RangoHabitual Rango(EstadisticaPrecio est)
    {
        if (est.Muestras < MuestrasMinimas)
            return new RangoHabitual(est.Minimo, est.Maximo, Suficiente: false);

        double promedio = (double)est.Suma / est.Muestras;
        double mediaCuadrados = (double)est.SumaCuadrados / est.Muestras;
        double varianza = Math.Max(0, mediaCuadrados - promedio * promedio);
        double desviacion = Math.Sqrt(varianza);

        // El rango nunca se sale de lo realmente visto: sin datos raros, piso y
        // techo quedan dentro de [mínimo, máximo].
        int piso = Math.Max(est.Minimo, (int)Math.Round(promedio - desviacion));
        int techo = Math.Min(est.Maximo, (int)Math.Round(promedio + desviacion));
        if (techo < piso) (piso, techo) = (techo, piso);
        return new RangoHabitual(piso, techo, Suficiente: true);
    }

    public static UbicacionPrecio Clasificar(EstadisticaPrecio est, int precio)
    {
        RangoHabitual rango = Rango(est);
        if (!rango.Suficiente) return UbicacionPrecio.Insuficiente;
        if (precio < rango.Piso) return UbicacionPrecio.Bajo;
        if (precio > rango.Techo) return UbicacionPrecio.Sobre;
        return UbicacionPrecio.Dentro;
    }
}
