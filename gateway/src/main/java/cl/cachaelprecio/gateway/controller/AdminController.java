package cl.cachaelprecio.gateway.controller;

import io.micronaut.http.annotation.Controller;
import io.micronaut.http.annotation.Get;
import io.micronaut.security.annotation.Secured;

import java.util.Map;

/**
 * Ruta que exige el grupo <b>admin</b>: es la que produce el <b>403</b>.
 *
 * <p>Que la diferencia entre 401 y 403 se pueda demostrar es justamente el
 * motivo por el que el user pool tiene grupos y no solo scopes: sin roles solo
 * existen el 200 y el 401, y el 403 es un indicador explicito de la rubrica.
 *
 * <p>Un usuario del grupo {@code usuario} presenta un token perfectamente
 * valido —firma buena, emisor nuestro, vigente— y aun asi no pasa. Esa es la
 * diferencia entre autenticar (quien eres) y autorizar (que puedes hacer).
 */
@Controller("/api/admin")
@Secured("admin")
public class AdminController {

    @Get("/diagnostico")
    public Map<String, Object> diagnostico() {
        return Map.of(
                "mensaje", "Si ves esto, tu token trae el grupo admin en cognito:groups",
                "servicio", "gateway");
    }
}
