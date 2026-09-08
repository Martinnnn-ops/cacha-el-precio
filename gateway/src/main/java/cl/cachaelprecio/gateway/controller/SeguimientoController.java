package cl.cachaelprecio.gateway.controller;

import cl.cachaelprecio.gateway.seguimiento.RepositorioDeSeguimiento;
import io.micronaut.http.HttpResponse;
import io.micronaut.http.annotation.Controller;
import io.micronaut.http.annotation.Delete;
import io.micronaut.http.annotation.Get;
import io.micronaut.http.annotation.Post;
import io.micronaut.security.annotation.Secured;
import io.micronaut.security.authentication.Authentication;
import io.micronaut.security.rules.SecurityRule;

import java.util.Map;
import java.util.Set;

/**
 * La lista de productos que sigue cada persona. <b>Requiere token: 200 con
 * sesion, 401 sin ella.</b>
 *
 * <p>Es la ruta que hace honesta la demo del 401. El catalogo es publico a
 * proposito (ADR-007), asi que cerrarlo solo para poder mostrar un 401 seria
 * romper el producto para ganar una casilla. Esto en cambio <b>tiene</b> que ser
 * privado por su propia naturaleza: son los productos que una persona eligio
 * seguir, y nadie mas deberia verlos.
 *
 * <p>Corresponde al permiso {@code seguimiento:escribir} de
 * `ARQUITECTURA.md` §8, que estaba definido desde el principio y sin
 * implementar.
 *
 * <p>El dueño de la lista sale del <b>token</b>, nunca de un parametro. Si el id
 * del usuario viniera en la URL, cualquiera con sesion podria pedir la lista de
 * otro cambiando un numero — que es el fallo de control de acceso mas comun que
 * existe.
 */
@Controller("/seguimiento")
@Secured(SecurityRule.IS_AUTHENTICATED)
public class SeguimientoController {

    private final RepositorioDeSeguimiento repositorio;

    public SeguimientoController(RepositorioDeSeguimiento repositorio) {
        this.repositorio = repositorio;
    }

    @Get
    public Map<String, Object> mios(Authentication autenticacion) {
        Set<Long> seguidos = repositorio.listar(autenticacion.getName());
        return Map.of("productos", seguidos, "total", seguidos.size());
    }

    @Post("/{productoId}")
    public HttpResponse<?> seguir(Long productoId, Authentication autenticacion) {
        boolean agregado = repositorio.seguir(autenticacion.getName(), productoId);
        // 201 si se agrego, 200 si ya estaba: repetir la llamada no es un error.
        return agregado
                ? HttpResponse.created(Map.of("siguiendo", productoId))
                : HttpResponse.ok(Map.of("siguiendo", productoId, "yaEstaba", true));
    }

    @Delete("/{productoId}")
    public HttpResponse<?> dejarDeSeguir(Long productoId, Authentication autenticacion) {
        return repositorio.dejarDeSeguir(autenticacion.getName(), productoId)
                ? HttpResponse.noContent()
                : HttpResponse.notFound();
    }
}
