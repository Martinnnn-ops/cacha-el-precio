using Product_Service.Models.Eventos;

namespace Product_Service.Mensajeria;

/// <summary>
/// Publica los cambios de oferta en el tópico <c>cep-cambios-oferta</c>. El
/// negocio habla con esta interfaz, no con el SDK de AWS (ADR-029): así
/// <c>ProductService</c> no sabe si detrás hay SNS, un log o nada.
/// </summary>
public interface IPublicadorEventos
{
    /// <summary>
    /// <c>true</c> si hay un tópico configurado. Cuando es <c>false</c> (local
    /// sin AWS) el servicio sigue funcionando igual, solo que no avisa a nadie.
    /// </summary>
    bool Habilitado { get; }

    /// <summary>
    /// Publica un cambio de oferta. <paramref name="tipo"/> es uno de
    /// <see cref="TiposEvento"/>. Es <b>best-effort</b>: si el tópico no responde
    /// se registra el error, pero no se propaga — el catálogo ya quedó guardado y
    /// un aviso perdido no debe tumbar el upsert (ver ADR-031).
    /// </summary>
    Task PublicarCambioOfertaAsync(string tipo, DatosCambioOferta datos, CancellationToken ct = default);
}
