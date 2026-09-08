package cl.cachaelprecio.gateway.cliente;

import io.micronaut.http.annotation.Get;
import io.micronaut.http.annotation.Header;
import io.micronaut.http.annotation.PathVariable;
import io.micronaut.http.client.annotation.Client;

import java.util.List;
import java.util.Map;

/**
 * Habla con {@code product-service}. Es la unica clase del gateway que sabe
 * donde vive ese servicio y como se le pide.
 *
 * <p>Los metodos devuelven {@code Map} y no clases del dominio a proposito: el
 * BFF hoy <b>reenvia</b> lo que product-service responde, y copiar aca sus
 * entidades significaria tener que tocar el gateway cada vez que a
 * product-service le agregan un campo. Cuando el BFF empiece a <i>componer</i>
 * de verdad —juntar catalogo con precios de {@code price-service}— ahi
 * aparecen tipos propios, que es cuando sirven.
 *
 * <p>La URL sale de configuracion ({@code servicios.product.url}): en el
 * computador es {@code localhost:8081} y dentro del compose es el nombre del
 * servicio. El mismo build sirve en los dos lados.
 */
@Client("${servicios.product.url}")
public interface ClienteDeProductos {

    /**
     * @param version la cabecera que product-service EXIGE. Tiene tres versiones
     *                registradas sobre la misma ruta, y sin este encabezado
     *                responde 400: "More than 1 route matched the incoming
     *                request". No es opcional aunque lo parezca.
     */
    @Get("/productos")
    List<Map<String, Object>> productos(@Header("X-API-VERSION") String version);

    @Get("/productos/{id}")
    Map<String, Object> producto(@PathVariable Long id, @Header("X-API-VERSION") String version);

    @Get("/catalogos")
    List<Map<String, Object>> catalogos(@Header("X-API-VERSION") String version);
}
