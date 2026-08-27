package cl.cachaelprecio.catalog.controller;

import java.util.List;

import cl.cachaelprecio.catalog.dto.ModelResumeResponse;
import io.micronaut.core.version.annotation.Version;
import io.micronaut.http.HttpResponse;
import io.micronaut.http.annotation.Controller;
import io.micronaut.http.annotation.Get;

/**
 * Expone las operaciones HTTP relacionadas con los modelos del catálogo.
 */
@Controller("/catalog")
public class ModeloController {

    /**
     * Lista los modelos disponibles para la versión 0.1.0 de la API.
     */
    @Version("0.1.0")
    @Get("/modelos")
    public HttpResponse<List<ModelResumeResponse>> listarModelos() {
        List<ModelResumeResponse> modelos = List.of();

        return HttpResponse.ok(modelos);
    }
}
