package cl.cachaelprecio.gateway.seguimiento;

import io.micronaut.http.HttpRequest;
import io.micronaut.http.HttpStatus;
import io.micronaut.http.client.HttpClient;
import io.micronaut.http.client.annotation.Client;
import io.micronaut.http.client.exceptions.HttpClientResponseException;
import io.micronaut.test.extensions.junit5.annotation.MicronautTest;
import jakarta.inject.Inject;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;

import java.util.Set;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertFalse;
import static org.junit.jupiter.api.Assertions.assertThrows;
import static org.junit.jupiter.api.Assertions.assertTrue;

/**
 * La lista de seguimiento es la ruta que hace honesta la demo del 401: el
 * catalogo es publico a proposito, y esto es privado por su naturaleza.
 */
@MicronautTest
class SeguimientoTest {

    @Inject
    @Client("/")
    HttpClient cliente;

    @Inject
    RepositorioDeSeguimiento repositorio;

    @Test
    @DisplayName("sin token da 401")
    void sinTokenDa401() {
        HttpClientResponseException error = assertThrows(HttpClientResponseException.class,
                () -> cliente.toBlocking().exchange(HttpRequest.GET("/seguimiento")));

        assertEquals(HttpStatus.UNAUTHORIZED, error.getStatus());
    }

    @Test
    @DisplayName("seguir tampoco se puede sin token")
    void seguirSinTokenDa401() {
        HttpClientResponseException error = assertThrows(HttpClientResponseException.class,
                () -> cliente.toBlocking().exchange(HttpRequest.POST("/seguimiento/1", "")));

        assertEquals(HttpStatus.UNAUTHORIZED, error.getStatus());
    }

    @Test
    @DisplayName("cada persona ve su propia lista, no la del resto")
    void lasListasNoSeMezclan() {
        repositorio.seguir("usuario-a", 10L);
        repositorio.seguir("usuario-a", 20L);
        repositorio.seguir("usuario-b", 30L);

        assertEquals(Set.of(10L, 20L), repositorio.listar("usuario-a"));
        assertEquals(Set.of(30L), repositorio.listar("usuario-b"));
        assertTrue(repositorio.listar("usuario-c").isEmpty(),
                "quien no sigue nada tiene lista vacia, no la de otro");
    }

    @Test
    @DisplayName("seguir dos veces lo mismo no duplica ni falla")
    void seguirEsIdempotente() {
        assertTrue(repositorio.seguir("usuario-d", 7L), "la primera vez se agrega");
        assertFalse(repositorio.seguir("usuario-d", 7L), "la segunda dice que ya estaba");
        assertEquals(1, repositorio.listar("usuario-d").size());
    }

    @Test
    @DisplayName("la lista que se devuelve es una copia: nadie la modifica desde afuera")
    void laListaDevueltaEsCopia() {
        repositorio.seguir("usuario-e", 1L);
        Set<Long> devuelta = repositorio.listar("usuario-e");

        assertThrows(UnsupportedOperationException.class, () -> devuelta.add(99L));
    }
}
