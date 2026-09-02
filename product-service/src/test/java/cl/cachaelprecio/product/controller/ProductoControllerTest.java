package cl.cachaelprecio.product.controller;

import cl.cachaelprecio.product.model.Catalogo;
import cl.cachaelprecio.product.model.Producto;
import cl.cachaelprecio.product.repository.CatalogoRepository;
import cl.cachaelprecio.product.repository.ProductoRepository;
import io.micronaut.core.type.Argument;
import io.micronaut.http.HttpRequest;
import io.micronaut.http.client.HttpClient;
import io.micronaut.http.client.annotation.Client;
import io.micronaut.test.extensions.junit5.annotation.MicronautTest;
import jakarta.inject.Inject;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;

import java.math.BigDecimal;
import java.util.List;

import static org.junit.jupiter.api.Assertions.assertEquals;

@MicronautTest(transactional = false)
class ProductoControllerTest {

    @Inject
    @Client("/")
    HttpClient cliente;

    @Inject
    CatalogoRepository catalogoRepository;

    @Inject
    ProductoRepository productoRepository;

    @BeforeEach
    void prepararDatos() {
        productoRepository.deleteAll();
        catalogoRepository.deleteAll();

        Catalogo catalogo = catalogoRepository.save(new Catalogo(null, "Notebooks", "Equipos portátiles"));
        productoRepository.save(new Producto(null, "Notebook activo", "Disponible",
                new BigDecimal("699990"), catalogo.getId(), true));
        productoRepository.save(new Producto(null, "Notebook retirado", "Fuera del catálogo",
                new BigDecimal("499990"), catalogo.getId(), false));
    }

    @Test
    void respondeLasTresVersionesDelListado() {
        assertEquals(2, listarConVersion("0.1.0", "/productos").size());
        assertEquals(2, listarConVersion("0.2.0", "/productos").size());
        assertEquals(1, listarConVersion("0.3.0", "/productos").size());
    }

    @Test
    void respondeLasTresVersionesDeCatalogos() {
        assertEquals(1, listarCatalogosConVersion("0.1.0", "/catalogos").size());
        assertEquals(1, listarCatalogosConVersion("0.2.0", "/catalogos").size());
        assertEquals(1, listarCatalogosConVersion("0.3.0", "/catalogos?nombre=note").size());
    }

    private List<Producto> listarConVersion(String version, String ruta) {
        HttpRequest<?> solicitud = HttpRequest.GET(ruta).header("X-API-VERSION", version);
        return cliente.toBlocking().retrieve(solicitud, Argument.listOf(Producto.class));
    }

    private List<Catalogo> listarCatalogosConVersion(String version, String ruta) {
        HttpRequest<?> solicitud = HttpRequest.GET(ruta).header("X-API-VERSION", version);
        return cliente.toBlocking().retrieve(solicitud, Argument.listOf(Catalogo.class));
    }
}
