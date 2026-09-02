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

/**
 * Endpoints HTTP de los catálogos. Se ejecutan en el pool bloqueante porque SQLite usa JDBC y no
 * conviene dejar una consulta esperando en los hilos de Netty.
 */
@ExecuteOn(TaskExecutors.BLOCKING)
@Controller("/catalogos")
@Tag(name = "Catálogos")
public class CatalogoController {

    private final CatalogoService catalogoService;

    public CatalogoController(CatalogoService catalogoService) {
        this.catalogoService = catalogoService;
    }

    /**
     * Versión inicial: devuelve los catálogos tal como vienen de la base de datos. Es la opción
     * más básica y queda viva para no romper clientes antiguos.
     */
    @Get
    @Version("1")
    @Operation(summary = "Lista los catálogos", description = "Versión 1: listado básico sin ordenar.")
    public List<Catalogo> listarVersion1() {
        return catalogoService.listar();
    }

    /**
     * Versión 2 del mismo GET. La única mejora por ahora es ordenar por nombre, que parece poca
     * cosa, pero sirve para mostrar una evolución real del contrato.
     */
    @Get
    @Version("2")
    @Operation(summary = "Lista los catálogos ordenados", description = "Versión 2: orden alfabético.")
    public List<Catalogo> listarVersion2() {
        return catalogoService.listarOrdenados();
    }

    /**
     * Versión 3: además de ordenar permite buscar por una parte del nombre. Si no llega el filtro,
     * se comporta igual que la versión 2 y listo.
     */
    @Get
    @Version("3")
    @Operation(summary = "Lista o busca catálogos", description = "Versión 3: acepta el filtro opcional nombre.")
    public List<Catalogo> listarVersion3(@QueryValue Optional<String> nombre) {
        return nombre.filter(texto -> !texto.isBlank())
                .map(catalogoService::buscarPorNombre)
                .orElseGet(catalogoService::listarOrdenados);
    }

    /** Busca un catálogo puntual. De momento esta operación parte en la versión 1. */
    @Get("/{id}")
    @Version("1")
    @Operation(summary = "Busca un catálogo por id")
    public HttpResponse<Catalogo> buscarPorId(Long id) {
        return catalogoService.buscarPorId(id)
                .map(HttpResponse::ok)
                .orElseGet(HttpResponse::notFound);
    }

    /** Crea un catálogo usando directamente el modelo, porque por ahora decidimos no usar DTO. */
    @Post
    @Version("1")
    @Operation(summary = "Crea un catálogo")
    public HttpResponse<Catalogo> crear(@Body Catalogo catalogo) {
        return HttpResponse.created(catalogoService.crear(catalogo));
    }

    /** Actualiza todo el catálogo indicado; si el id no existe devuelve 404 y era. */
    @Put("/{id}")
    @Version("1")
    @Operation(summary = "Actualiza un catálogo")
    public HttpResponse<Catalogo> actualizar(Long id, @Body Catalogo catalogo) {
        return catalogoService.actualizar(id, catalogo)
                .map(HttpResponse::ok)
                .orElseGet(HttpResponse::notFound);
    }

    /** Elimina un catálogo si existe. SQLite protege los productos asociados con una FK. */
    @Delete("/{id}")
    @Version("1")
    @Operation(summary = "Elimina un catálogo")
    public HttpResponse<?> eliminar(Long id) {
        return catalogoService.eliminar(id) ? HttpResponse.noContent() : HttpResponse.notFound();
    }
}
