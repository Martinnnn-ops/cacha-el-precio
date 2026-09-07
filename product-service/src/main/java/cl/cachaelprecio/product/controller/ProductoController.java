package cl.cachaelprecio.product.controller;

import cl.cachaelprecio.product.model.Producto;
import cl.cachaelprecio.product.service.ProductoService;
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
@Controller("/productos")
@Tag(name = "Productos")
public class ProductoController {

    private final ProductoService productoService;

    public ProductoController(ProductoService productoService) {
        this.productoService = productoService;
    }

    @Get
    @Version("0.1.0")
    @Operation(summary = "Lista los productos", description = "Versión 0.1.0: listado básico.")
    public List<Producto> listar() {
        return productoService.listar();
    }

    @Get
    @Version("0.2.0")
    @Operation(summary = "Lista los productos ordenados", description = "Versión 0.2.0: orden alfabético.")
    public List<Producto> listarOrdenados() {
        return productoService.listarOrdenados();
    }

    @Get
    @Version("0.3.0")
    @Operation(summary = "Lista productos con filtros", description = "Versión 0.3.0: permite filtrar por catálogo y estado.")
    public List<Producto> listarConFiltros(@QueryValue Optional<Long> catalogoId,
                                           @QueryValue(defaultValue = "true") boolean soloActivos) {
        return productoService.listarFiltrados(catalogoId, soloActivos);
    }

    @Get("/{id}")
    @Version("0.1.0")
    @Operation(summary = "Busca un producto por id")
    public HttpResponse<Producto> buscarPorId(Long id) {
        return productoService.buscarPorId(id)
                .map(HttpResponse::ok)
                .orElseGet(HttpResponse::notFound);
    }

    @Post
    @Version("0.1.0")
    @Operation(summary = "Crea un producto")
    public HttpResponse<Producto> crear(@Body Producto producto) {
        return HttpResponse.created(productoService.crear(producto));
    }

    @Put("/{id}")
    @Version("0.1.0")
    @Operation(summary = "Actualiza un producto")
    public HttpResponse<Producto> actualizar(Long id, @Body Producto producto) {
        return productoService.actualizar(id, producto)
                .map(HttpResponse::ok)
                .orElseGet(HttpResponse::notFound);
    }

    @Delete("/{id}")
    @Version("0.1.0")
    @Operation(summary = "Elimina un producto")
    public HttpResponse<?> eliminar(Long id) {
        return productoService.eliminar(id) ? HttpResponse.noContent() : HttpResponse.notFound();
    }
}
