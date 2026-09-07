package cl.cachaelprecio.product.controller;

import cl.cachaelprecio.product.model.Catalogo;
import cl.cachaelprecio.product.service.CatalogoService;
import io.micronaut.core.version.annotation.Version;
import io.micronaut.http.HttpResponse;
import io.micronaut.http.annotation.Body;
import io.micronaut.http.annotation.Controller;
import io.micronaut.http.annotation.Delete;
import io.micronaut.http.annotation.Get;
import io.micronaut.http.annotation.Post;
import io.micronaut.http.annotation.Put;
import io.micronaut.http.annotation.QueryValue;
import io.micronaut.scheduling.TaskExecutors;
import io.micronaut.scheduling.annotation.ExecuteOn;
import io.swagger.v3.oas.annotations.Operation;
import io.swagger.v3.oas.annotations.tags.Tag;

import java.util.List;
import java.util.Optional;

@ExecuteOn(TaskExecutors.BLOCKING)
@Controller("/catalogos")
@Tag(name = "Catálogos")
public class CatalogoController {

    private final CatalogoService catalogoService;

    public CatalogoController(CatalogoService catalogoService) {
        this.catalogoService = catalogoService;
    }

    @Get
    @Version("0.1.0")
    @Operation(summary = "Lista los catálogos", description = "Versión 0.1.0: listado básico sin ordenar.")
    public List<Catalogo> listar() {
        return catalogoService.listar();
    }

    @Get
    @Version("0.2.0")
    @Operation(summary = "Lista los catálogos ordenados", description = "Versión 0.2.0: orden alfabético.")
    public List<Catalogo> listarOrdenados() {
        return catalogoService.listarOrdenados();
    }

    @Get
    @Version("0.3.0")
    @Operation(summary = "Lista o busca catálogos", description = "Versión 0.3.0: permite filtrar por nombre.")
    public List<Catalogo> listarConFiltro(@QueryValue Optional<String> nombre) {
        return nombre.filter(texto -> !texto.isBlank())
                .map(catalogoService::buscarPorNombre)
                .orElseGet(catalogoService::listarOrdenados);
    }

    @Get("/{id}")
    @Version("0.1.0")
    @Operation(summary = "Busca un catálogo por id")
    public HttpResponse<Catalogo> buscarPorId(Long id) {
        return catalogoService.buscarPorId(id)
                .map(HttpResponse::ok)
                .orElseGet(HttpResponse::notFound);
    }

    @Post
    @Version("0.1.0")
    @Operation(summary = "Crea un catálogo")
    public HttpResponse<Catalogo> crear(@Body Catalogo catalogo) {
        return HttpResponse.created(catalogoService.crear(catalogo));
    }

    @Put("/{id}")
    @Version("0.1.0")
    @Operation(summary = "Actualiza un catálogo")
    public HttpResponse<Catalogo> actualizar(Long id, @Body Catalogo catalogo) {
        return catalogoService.actualizar(id, catalogo)
                .map(HttpResponse::ok)
                .orElseGet(HttpResponse::notFound);
    }

    @Delete("/{id}")
    @Version("0.1.0")
    @Operation(summary = "Elimina un catálogo")
    public HttpResponse<?> eliminar(Long id) {
        return catalogoService.eliminar(id) ? HttpResponse.noContent() : HttpResponse.notFound();
    }
}
