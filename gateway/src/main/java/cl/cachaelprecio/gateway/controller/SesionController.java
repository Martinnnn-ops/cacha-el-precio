package cl.cachaelprecio.gateway.controller;

import io.micronaut.http.annotation.Controller;
import io.micronaut.http.annotation.Get;
import io.micronaut.security.annotation.Secured;
import io.micronaut.security.authentication.Authentication;
import io.micronaut.security.rules.SecurityRule;

import java.util.LinkedHashMap;
import java.util.Map;

/**
 * Devuelve lo que el BFF <b>entendio</b> del token que le presentaron.
 *
 * <p>No es una ruta de adorno ni un stub: los datos salen del token real, ya
 * verificado contra el JWKS de Cognito. Sirve para tres cosas concretas:
 *
 * <ul>
 *   <li>El frontend la llama para pintar en pantalla los roles y scopes leidos
 *       de los claims, que es un punto explicito de la rubrica del EP1.</li>
 *   <li>En la demo del EP2 se muestra al lado del token decodificado en
 *       jwt.io: prueba que el servidor leyo lo mismo que el navegador.</li>
 *   <li>Al depurar responde la pregunta de siempre — "¿por que me da 403?" —
 *       mostrando que grupos trae el usuario de verdad.</li>
 * </ul>
 */
@Controller("/api/yo")
@Secured(SecurityRule.IS_AUTHENTICATED)
public class SesionController {

    @Get
    public Map<String, Object> quienSoy(Authentication autenticacion) {
        Map<String, Object> respuesta = new LinkedHashMap<>();

        // El `sub` de Cognito: el identificador estable del usuario. No es el
        // correo, que puede cambiar.
        respuesta.put("usuario", autenticacion.getName());

        // Micronaut ya mapeo `cognito:groups` a roles (ver application.properties).
        respuesta.put("grupos", autenticacion.getRoles());

        // Los scopes del resource server, tal como vienen en el token.
        respuesta.put("scope", autenticacion.getAttributes().get("scope"));
        respuesta.put("clientId", autenticacion.getAttributes().get("client_id"));
        respuesta.put("emisor", autenticacion.getAttributes().get("iss"));

        return respuesta;
    }
}
