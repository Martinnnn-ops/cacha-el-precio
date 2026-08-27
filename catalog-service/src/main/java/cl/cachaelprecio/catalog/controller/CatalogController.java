package cl.cachaelprecio.catalog.controller;

import io.micronaut.core.version.annotation.Version;
import io.micronaut.http.HttpResponse;
import io.micronaut.http.annotation.Controller;
import io.micronaut.http.annotation.Get;

/**
 * Expone información técnica del microservicio y demuestra el versionado de rutas.
 */
@Controller("/catalog")
public class CatalogController {

    /**
     * Entrega el mensaje informativo correspondiente a la versión 0.1.0 de la API.
     */
    @Version("0.1.0")
    @Get("/info")
    public String infoV1(){
        return "Status: Funcionando correctamente";
    }

    /**
     * Entrega el mensaje informativo correspondiente a la versión 0.2.0 de la API.
     */
    @Version("0.2.0")
    @Get("/info")
    public HttpResponse<String> infoV2(){
        return HttpResponse.ok("Servidor funcionando");
    }
}
