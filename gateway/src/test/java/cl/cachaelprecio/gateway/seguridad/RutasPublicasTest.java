package cl.cachaelprecio.gateway.seguridad;

import cl.cachaelprecio.gateway.cliente.ClienteDeProductos;
import io.micronaut.context.annotation.Replaces;
import io.micronaut.http.HttpRequest;
import io.micronaut.http.HttpStatus;
import io.micronaut.http.client.HttpClient;
import io.micronaut.http.client.annotation.Client;
import io.micronaut.test.extensions.junit5.annotation.MicronautTest;
import jakarta.inject.Inject;
import jakarta.inject.Singleton;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;

import java.util.ArrayList;
import java.util.List;
import java.util.Map;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertTrue;

/**
 * El catalogo tiene que verse <b>sin iniciar sesion</b>.
 *
 * <p>No es un detalle: `ARQUITECTURA.md` §8 define {@code precios:leer} como
 * publico, y este proyecto es un comparador de precios. Si alguien "asegura"
 * estas rutas creyendo que mas candados es mejor, el sitio deja de mostrar
 * productos a quien llega de Google y nadie se entera hasta que un visitante
 * reclama. Estos tests fallan primero.
 *
 * <p>No necesitan product-service levantado: el cliente se reemplaza por uno de
 * mentira que ademas anota que version le pidieron, para poder comprobar que la
 * cabecera viaja.
 */
@MicronautTest
class RutasPublicasTest {

    @Inject
    @Client("/")
    HttpClient cliente;

    @Inject
    ClienteDeProductosFalso falso;

    @Test
    @DisplayName("/productos se ve sin token: es un comparador publico (ADR-007)")
    void productosEsPublico() {
        var respuesta = cliente.toBlocking().exchange(HttpRequest.GET("/productos"), String.class);

        assertEquals(HttpStatus.OK, respuesta.getStatus());
        assertTrue(respuesta.body().contains("New Balance 574"),
                "el BFF tiene que devolver lo que product-service le dio");
    }

    @Test
    @DisplayName("/catalogos tambien se ve sin token")
    void catalogosEsPublico() {
        var respuesta = cliente.toBlocking().exchange(HttpRequest.GET("/catalogos"), String.class);
        assertEquals(HttpStatus.OK, respuesta.getStatus());
    }

    @Test
    @DisplayName("sin cabecera de version, el BFF pone la que sabe hablar")
    void poneLaVersionPorDefecto() {
        falso.versionesPedidas.clear();
        cliente.toBlocking().exchange(HttpRequest.GET("/productos"), String.class);

        assertEquals(List.of("0.1.0"), falso.versionesPedidas,
                "sin esta cabecera product-service responde 400, no 200");
    }

    @Test
    @DisplayName("si quien llama pide una version, se respeta")
    void respetaLaVersionPedida() {
        falso.versionesPedidas.clear();
        cliente.toBlocking().exchange(
                HttpRequest.GET("/productos").header("X-API-VERSION", "0.3.0"), String.class);

        assertEquals(List.of("0.3.0"), falso.versionesPedidas);
    }

    @Test
    @DisplayName("un producto que no existe da 404, no 500")
    void productoInexistenteDa404() {
        var error = org.junit.jupiter.api.Assertions.assertThrows(
                io.micronaut.http.client.exceptions.HttpClientResponseException.class,
                () -> cliente.toBlocking().exchange(HttpRequest.GET("/productos/999999"), String.class));

        assertTrue(error.getStatus() == HttpStatus.NOT_FOUND,
                "se esperaba 404 y llego " + error.getStatus());
    }

    /** Reemplaza a product-service. Anota que version le pidieron. */
    @Singleton
    @Replaces(ClienteDeProductos.class)
    static class ClienteDeProductosFalso implements ClienteDeProductos {

        final List<String> versionesPedidas = new ArrayList<>();

        @Override
        public List<Map<String, Object>> productos(String version) {
            versionesPedidas.add(version);
            return List.of(Map.of("id", 1, "nombre", "New Balance 574"));
        }

        @Override
        public Map<String, Object> producto(Long id, String version) {
            versionesPedidas.add(version);
            return id == 1L ? Map.of("id", 1, "nombre", "New Balance 574") : null;
        }

        @Override
        public List<Map<String, Object>> catalogos(String version) {
            versionesPedidas.add(version);
            return List.of(Map.of("id", 1, "nombre", "Zapatillas"));
        }
    }
}
