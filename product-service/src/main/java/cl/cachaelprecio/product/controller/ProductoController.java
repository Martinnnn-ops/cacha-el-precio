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

/**
 * API REST de productos. Mantiene el controller liviano: recibe HTTP, llama al service y responde.
 */
@ExecuteOn(TaskExecutors.BLOCKING)
@Controller("/productos")
@Tag(name = "Productos")
public class ProductoController {

    private final ProductoService productoService;

    public ProductoController(ProductoService productoService) {
        this.productoService = productoService;
    }

    /**
     * Versión 1 del listado. Entrega todos los productos y nada más, como para partir sin darle
     * tantas vueltas al asunto.
     */
    @Get
    @Version("1")
    @Operation(summary = "Lista los productos", description = "Versión 1: listado básico.")
    public List<Producto> listarVersion1() {
        return productoService.listar();
    }

    /**
     * Versión 2 del mismo endpoint. Conserva la respuesta, pero ahora viene ordenada por nombre
     * para que el frontend no tenga que hacer esa pega.
     */
    @Get
    @Version("2")
    @Operation(summary = "Lista los productos ordenados", description = "Versión 2: orden alfabético.")
    public List<Producto> listarVersion2() {
        return productoService.listarOrdenados();
    }

    /**
     * Versión 3. Permite filtrar por catálogo y decidir si se muestran solamente productos
     * activos. El valor por defecto es true porque normalmente no queremos mostrar cosas retiradas.
     */
    @Get
    @Version("3")
    @Operation(summary = "Lista productos con filtros", description = "Versión 3: filtros por catálogo y estado.")
    public List<Producto> listarVersion3(@QueryValue Optional<Long> catalogoId,
                                         @QueryValue(defaultValue = "true") boolean soloActivos) {
        return productoService.listarFiltrados(catalogoId, soloActivos);
    }

    /** Busca un producto por id. Devuelve 404 si no está, sin inventar una respuesta rara. */
    @Get("/{id}")
    @Version("1")
    @Operation(summary = "Busca un producto por id")
    public HttpResponse<Producto> buscarPorId(Long id) {
        return productoService.buscarPorId(id)
                .map(HttpResponse::ok)
                .orElseGet(HttpResponse::notFound);
    }

    /** Crea un producto usando el modelo como body, tal como acordamos para esta etapa. */
    @Post
    @Version("1")
    @Operation(summary = "Crea un producto")
    public HttpResponse<Producto> crear(@Body Producto producto) {
        return HttpResponse.created(productoService.crear(producto));
    }

    /** Reemplaza los datos editables del producto y conserva el id de la URL. */
    @Put("/{id}")
    @Version("1")
    @Operation(summary = "Actualiza un producto")
    public HttpResponse<Producto> actualizar(Long id, @Body Producto producto) {
        return productoService.actualizar(id, producto)
                .map(HttpResponse::ok)
                .orElseGet(HttpResponse::notFound);
    }

    /** Elimina el producto cuando existe; en caso contrario responde 404. Cortito y claro. */
    @Delete("/{id}")
    @Version("1")
    @Operation(summary = "Elimina un producto")
    public HttpResponse<?> eliminar(Long id) {
        return productoService.eliminar(id) ? HttpResponse.noContent() : HttpResponse.notFound();
    }
}
