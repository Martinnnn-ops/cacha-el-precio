package cl.cachaelprecio.catalog.dto;

import io.micronaut.serde.annotation.Serdeable;

/**
 * Representa los datos resumidos de un modelo que se entregan mediante la API.
 * No corresponde a una entidad de persistencia.
 */
@Serdeable
public record ModelResumeResponse(
    String idCode,
    String name,
    String model,
    String brand,
    String category
) {
}
