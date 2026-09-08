package cl.cachaelprecio.gateway.seguridad;

import io.micronaut.http.HttpRequest;
import io.micronaut.http.HttpStatus;
import io.micronaut.http.client.HttpClient;
import io.micronaut.http.client.annotation.Client;
import io.micronaut.http.client.exceptions.HttpClientResponseException;
import io.micronaut.test.extensions.junit5.annotation.MicronautTest;
import jakarta.inject.Inject;
import java.util.Map;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;

import static org.junit.jupiter.api.Assertions.assertDoesNotThrow;
import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertThrows;
import static org.junit.jupiter.api.Assertions.assertTrue;

/**
 * Comprueba que la capa de seguridad esta <b>encendida de verdad</b>.
 *
 * <p>Es facil creer que un servicio esta protegido porque la dependencia esta en
 * el pom y las anotaciones estan escritas. Si la configuracion no calza, el
 * servicio arranca igual, responde 200 a todo, y nadie se entera hasta que se
 * despliega. Estos tests fallan justo en ese caso.
 *
 * <p>No verifican firmas: un token sin presentar se rechaza antes de que
 * Micronaut vaya a buscar el JWKS, asi que corren sin red y sin AWS. La prueba
 * con un token real de Cognito es otra y necesita credenciales del laboratorio.
 */
@MicronautTest
class RechazoSinTokenTest {

    @Inject
    @Client("/")
    HttpClient cliente;

    @Test
    @DisplayName("/health responde sin token: lo consulta el healthcheck del contenedor")
    void healthEsAnonimo() {
        assertDoesNotThrow(() -> cliente.toBlocking().exchange(HttpRequest.GET("/health")));
    }

    @Test
    @DisplayName("una ruta protegida sin token da 401, no 200")
    void rutaProtegidaSinTokenDa401() {
        HttpClientResponseException error = assertThrows(HttpClientResponseException.class,
                () -> cliente.toBlocking().exchange(HttpRequest.GET("/api/yo")));

        assertEquals(HttpStatus.UNAUTHORIZED, error.getStatus());
    }

    @Test
    @DisplayName("la ruta de admin sin token tambien da 401")
    void rutaDeAdminSinTokenDa401() {
        HttpClientResponseException error = assertThrows(HttpClientResponseException.class,
                () -> cliente.toBlocking().exchange(HttpRequest.GET("/api/admin/diagnostico")));

        assertEquals(HttpStatus.UNAUTHORIZED, error.getStatus());
    }

    @Test
    @DisplayName("un token inventado da 401: no basta con mandar cualquier cosa en el header")
    void tokenInventadoDa401() {
        HttpClientResponseException error = assertThrows(HttpClientResponseException.class,
                () -> cliente.toBlocking().exchange(
                        HttpRequest.GET("/api/yo").bearerAuth("esto.no.es-un-jwt")));

        assertEquals(HttpStatus.UNAUTHORIZED, error.getStatus());
    }

    @Test
    @DisplayName("el 401 viene con cuerpo JSON, no vacio: es la evidencia que pide la rubrica")
    void elErrorTraeCuerpoJson() {
        HttpClientResponseException error = assertThrows(HttpClientResponseException.class,
                () -> cliente.toBlocking().exchange(HttpRequest.GET("/api/yo"), Map.class));

        Map<?, ?> cuerpo = error.getResponse().getBody(Map.class).orElseThrow();

        assertEquals(401, cuerpo.get("estado"));
        assertEquals("No autorizado", cuerpo.get("error"));
        assertEquals("/api/yo", cuerpo.get("ruta"));
        assertTrue(error.getResponse().getHeaders().contains("WWW-Authenticate"),
                "un 401 debe decir como autenticarse (RFC 6750)");
    }

    @Test
    @DisplayName("el catalogo publico NO se ve afectado por la seguridad de las otras rutas")
    void elCatalogoSigueSiendoPublico() {
        assertDoesNotThrow(() -> cliente.toBlocking().exchange(HttpRequest.GET("/catalogos")));
    }
}
