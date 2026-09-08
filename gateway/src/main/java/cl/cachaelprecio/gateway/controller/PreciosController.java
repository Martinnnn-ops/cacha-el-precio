package cl.cachaelprecio.gateway.controller;

import io.micronaut.http.annotation.Controller;
import io.micronaut.http.annotation.Get;
import io.micronaut.security.annotation.Secured;
import io.micronaut.security.rules.SecurityRule;

import java.util.List;
import java.util.Map;

/**
 * Ruta protegida de lectura: <b>200 con token valido, 401 sin token</b>.
 *
 * <p>⚠️ Los datos que devuelve todavia son de ejemplo. Este controlador existe
 * para probar la capa de seguridad, que es lo que califica el 40% del EP1; el
 * paso siguiente es que consulte de verdad a {@code product-service} y a
 * {@code price-service} y componga la respuesta (eso es lo que convierte al
 * gateway en un BFF y no en un proxy). Mientras eso no exista, esto no se
 * presenta como si trajera precios reales.
 */
@Controller("/api/precios")
@Secured(SecurityRule.IS_AUTHENTICATED)
public class PreciosController {

    @Get
    public List<Map<String, Object>> listar() {
        return List.of(
                Map.of("modelo", "New Balance 574", "tienda", "sparta", "precio", 79990),
                Map.of("modelo", "Adidas Grand Court", "tienda", "sparta", "precio", 49990));
    }
}
