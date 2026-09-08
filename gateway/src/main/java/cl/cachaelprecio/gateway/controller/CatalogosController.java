package cl.cachaelprecio.gateway.controller;

import cl.cachaelprecio.gateway.cliente.ClienteDeProductos;
import io.micronaut.core.annotation.Nullable;
import io.micronaut.http.annotation.Controller;
import io.micronaut.http.annotation.Get;
import io.micronaut.http.annotation.Header;
import io.micronaut.security.annotation.Secured;
import io.micronaut.security.rules.SecurityRule;

import java.util.List;
import java.util.Map;

/**
 * Las categorias de prenda. Publico por el mismo motivo que
 * {@link ProductosController}: es lo que se ve al entrar al sitio.
 */
@Controller("/catalogos")
@Secured(SecurityRule.IS_ANONYMOUS)
public class CatalogosController {

    private static final String VERSION_POR_DEFECTO = "0.1.0";

    private final ClienteDeProductos productos;

    public CatalogosController(ClienteDeProductos productos) {
        this.productos = productos;
    }

    @Get
    public List<Map<String, Object>> listar(
            @Nullable @Header("X-API-VERSION") String version) {
        return productos.catalogos(
                (version == null || version.isBlank()) ? VERSION_POR_DEFECTO : version);
    }
}
