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
import java.util.Map;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertNotNull;

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

    @Test
    void persisteYDevuelveUrlImagenYMarca() {
        Catalogo catalogo = catalogoRepository.save(new Catalogo(null, "Poleras", "Prendas"));
        Producto nuevo = new Producto();
        nuevo.setNombre("Polera CK");
        nuevo.setDescripcion("Algodón");
        nuevo.setPrecio(new BigDecimal("19990"));
        nuevo.setCatalogoId(catalogo.getId());
        nuevo.setActivo(true);
        nuevo.setUrl("https://paris.cl/polera-ck");
        nuevo.setImagen("https://img.example/polera.webp");
        nuevo.setMarca("CK");

        HttpRequest<?> solicitud = HttpRequest.POST("/productos", nuevo).header("X-API-VERSION", "0.1.0");
        Producto guardado = cliente.toBlocking().retrieve(solicitud, Producto.class);

        assertNotNull(guardado.getId());
        assertEquals("https://paris.cl/polera-ck", guardado.getUrl());
        assertEquals("https://img.example/polera.webp", guardado.getImagen());
        assertEquals("CK", guardado.getMarca());

        Producto leido = productoRepository.findById(guardado.getId()).orElseThrow();
        assertEquals("https://paris.cl/polera-ck", leido.getUrl());
        assertEquals("https://img.example/polera.webp", leido.getImagen());
        assertEquals("CK", leido.getMarca());
    }

    @Test
    void registraVisitasSinTocarlasAlActualizar() {
        Catalogo catalogo = catalogoRepository.save(new Catalogo(null, "Poleras", "Prendas"));
        Producto nuevo = new Producto();
        nuevo.setNombre("Polera CK");
        nuevo.setPrecio(new BigDecimal("19990"));
        nuevo.setCatalogoId(catalogo.getId());
        nuevo.setActivo(true);

        HttpRequest<?> alta = HttpRequest.POST("/productos", nuevo).header("X-API-VERSION", "0.1.0");
        Producto creado = cliente.toBlocking().retrieve(alta, Producto.class);

        assertNotNull(creado.getCreadoEn(), "el servicio debería poner la fecha de creación");
        assertEquals(0, creado.getVisitas());

        for (int i = 1; i <= 3; i++) {
            HttpRequest<?> visita = HttpRequest.POST(
                    "/productos/" + creado.getId() + "/visitas",
                    null).header("X-API-VERSION", "0.1.0");
            Map<?, ?> respuesta = cliente.toBlocking().retrieve(visita, Map.class);
            assertEquals(i, ((Number) respuesta.get("visitas")).intValue());
        }

        Producto conVisitas = productoRepository.findById(creado.getId()).orElseThrow();
        assertEquals(3, conVisitas.getVisitas());
        assertNotNull(conVisitas.getVistoEn(), "la última vista debería quedar registrada");

        // El PUT del scraper envía el producto sin visitas y no debe perder el contador.
        Producto parchado = new Producto();
        parchado.setNombre("Polera CK");
        parchado.setPrecio(new BigDecimal("17990"));
        parchado.setCatalogoId(catalogo.getId());
        parchado.setActivo(true);

        HttpRequest<?> actualizacion = HttpRequest.PUT("/productos/" + creado.getId(), parchado)
                .header("X-API-VERSION", "0.1.0");
        cliente.toBlocking().exchange(actualizacion, Producto.class);

        Producto trasActualizar = productoRepository.findById(creado.getId()).orElseThrow();
        assertEquals(3, trasActualizar.getVisitas(), "el update no debería pisar el contador");
        assertEquals(new BigDecimal("17990"), trasActualizar.getPrecio());
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
