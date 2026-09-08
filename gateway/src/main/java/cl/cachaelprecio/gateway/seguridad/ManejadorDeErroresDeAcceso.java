package cl.cachaelprecio.gateway.seguridad;

import io.micronaut.context.annotation.Replaces;
import io.micronaut.http.HttpRequest;
import io.micronaut.http.HttpResponse;
import io.micronaut.http.HttpStatus;
import io.micronaut.http.MutableHttpResponse;
import io.micronaut.http.server.exceptions.ExceptionHandler;
import io.micronaut.security.authentication.AuthorizationException;
import io.micronaut.security.authentication.DefaultAuthorizationExceptionHandler;
import jakarta.inject.Singleton;

import java.util.LinkedHashMap;
import java.util.Map;

/**
 * Devuelve el 401 y el 403 como <b>JSON</b>, con el mismo formato siempre.
 *
 * <p>Por defecto Micronaut responde estos dos codigos con el cuerpo vacio. Eso
 * es correcto y no rompe nada, pero deja dos problemas practicos:
 *
 * <ul>
 *   <li>El frontend recibe un 401 pelado y no puede distinguir "no mandaste
 *       token" de "tu token vencio", que se resuelven distinto: uno manda al
 *       login, el otro refresca en silencio.</li>
 *   <li>La rubrica del EP2 pide mostrar, ruta por ruta, la llamada con y sin
 *       token <b>con el JSON esperado</b>. Una respuesta vacia no es evidencia
 *       de nada.</li>
 * </ul>
 *
 * <p>Se cuida de no filtrar detalle: se dice que el token no sirve, nunca por
 * que exactamente. Decir "la firma no valida" o "el client_id no esta en la
 * lista" le regala al atacante el mapa de lo que falta ajustar.
 */
@Singleton
@Replaces(DefaultAuthorizationExceptionHandler.class)
public class ManejadorDeErroresDeAcceso
        implements ExceptionHandler<AuthorizationException, MutableHttpResponse<?>> {

    @Override
    public MutableHttpResponse<?> handle(HttpRequest request, AuthorizationException excepcion) {
        // `isForbidden()` es true cuando SI hay usuario autenticado pero le
        // faltan permisos. Esa es exactamente la diferencia entre 401 y 403.
        boolean sinPermiso = excepcion.isForbidden();

        HttpStatus estado = sinPermiso ? HttpStatus.FORBIDDEN : HttpStatus.UNAUTHORIZED;

        Map<String, Object> cuerpo = new LinkedHashMap<>();
        cuerpo.put("estado", estado.getCode());
        cuerpo.put("error", sinPermiso ? "Prohibido" : "No autorizado");
        cuerpo.put("mensaje", sinPermiso
                ? "Tu token es valido, pero no tienes el permiso que esta ruta exige."
                : "Falta el token de acceso, o no es valido.");
        cuerpo.put("ruta", request.getPath());

        MutableHttpResponse<?> respuesta = HttpResponse.status(estado).body(cuerpo);

        if (!sinPermiso) {
            // Lo pide el RFC 6750: un 401 dice como autenticarse.
            respuesta = respuesta.header("WWW-Authenticate", "Bearer realm=\"cacha-el-precio\"");
        }

        return respuesta;
    }
}
