using Prometheus;

namespace Product_Service.Observabilidad;

// Contadores de negocio para Prometheus (PLAN-EFT §8). Se ven en el tablero de
// Grafana junto a los de HTTP. Viven en memoria del proceso: se reinician con el
// contenedor, y Prometheus ya sabe leer esos reinicios con rate()/increase().
public static class MetricasNegocio
{
    /// <summary>
    /// Cada paso que anota RegistroTrazas (oferta-guardada, cambio-auditado,
    /// duplicado-ignorado, aviso-enviado...). Los «duplicado-ignorado» y
    /// «aviso-repetido-saltado» son la idempotencia trabajando.
    /// </summary>
    public static readonly Counter Etapas = Metrics.CreateCounter(
        "cep_etapas_total",
        "Pasos de negocio procesados, por etapa (los mismos de RegistroTrazas).",
        new CounterConfiguration { LabelNames = ["etapa"] });

    /// <summary>Cómo terminó cada mensaje de una cola SQS: ok, reintento, dlq o dlq-agotado.</summary>
    public static readonly Counter Mensajes = Metrics.CreateCounter(
        "cep_mensajes_total",
        "Mensajes de SQS procesados por product-service, por cola y resultado.",
        new CounterConfiguration { LabelNames = ["cola", "resultado"] });
}
