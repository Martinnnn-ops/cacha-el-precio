package cl.cachaelprecio.gateway.controller;

import cl.cachaelprecio.gateway.cliente.ClienteDeProductos;
import io.micronaut.core.annotation.Nullable;
import io.micronaut.http.HttpResponse;
import io.micronaut.http.annotation.Controller;
import io.micronaut.http.annotation.Get;
import io.micronaut.http.annotation.Header;
import io.micronaut.security.annotation.Secured;
import io.micronaut.security.rules.SecurityRule;

import java.util.List;
import java.util.Map;

/**
 * El catalogo de productos, servido por el BFF.
 *
 * <p><b>Es publico, y es una decision, no un olvido.</b> `ARQUITECTURA.md` §8
 * define el permiso {@code precios:leer} como <i>publico, sin token</i>: esto es
 * un comparador de precios, no vende nada, y obligar a iniciar sesion para ver
 * un precio seria romper el producto. Lo que se protege es escribir y lo que es
 * de cada persona, no lo que cualquiera puede ver entrando al sitio de la
 * tienda.
 *
 * <p>Las rutas son las mismas que el frontend ya llama ({@code /productos},
 * {@code /catalogos}), a proposito: asi Caddy puede pasar a apuntar al gateway
 * sin que haya que tocar el frontend.
 */
@Controller("/productos")
@Secured(SecurityRule.IS_ANONYMOUS)
public class ProductosController {

    /** La version de product-service que este gateway sabe hablar. */
    private static final String VERSION_POR_DEFECTO = "0.1.0";

    private final ClienteDeProductos productos;

    public ProductosController(ClienteDeProductos productos) {
        this.productos = productos;
    }

    @Get
    public List<Map<String, Object>> listar(
            @Nullable @Header("X-API-VERSION") String version) {
        return productos.productos(versionPedida(version));
    }

    @Get("/{id}")
    public HttpResponse<Map<String, Object>> porId(
            Long id, @Nullable @Header("X-API-VERSION") String version) {
        Map<String, Object> encontrado = productos.producto(id, versionPedida(version));
        return encontrado == null ? HttpResponse.notFound() : HttpResponse.ok(encontrado);
    }

    /**
     * Si quien llama pidio una version, se respeta; si no, se usa la que el
     * gateway conoce.
     *
     * <p>Que el frontend pueda elegir es util mientras las tres versiones de
     * product-service convivan. Que no tenga que hacerlo es lo que hace que el
     * BFF sirva de algo: el dia que product-service jubile la 0.1.0, se cambia
     * esta constante y el frontend no se entera.
     */
    private String versionPedida(String version) {
        return (version == null || version.isBlank()) ? VERSION_POR_DEFECTO : version;
    }
}
