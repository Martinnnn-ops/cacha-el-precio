package cl.cachaelprecio.gateway.seguridad;

import io.micronaut.context.annotation.Value;
import io.micronaut.core.annotation.Nullable;
import io.micronaut.http.HttpRequest;
import io.micronaut.security.token.Claims;
import io.micronaut.security.token.jwt.validator.GenericJwtClaimsValidator;
import jakarta.inject.Singleton;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

import java.util.Arrays;
import java.util.List;

/**
 * Comprueba que el token fue emitido para una aplicacion NUESTRA.
 *
 * <p>Esto es lo que la rubrica llama "validar el audience", y en Cognito hay que
 * hacerlo a mano. El motivo: Cognito <b>no incluye el claim {@code aud} en el
 * access_token</b> — lo pone solo en el id_token. En su lugar publica
 * {@code client_id}. Si se activara la validacion estandar de audience de
 * Micronaut, todo token real de Cognito daria 401 contra nuestra propia
 * validacion.
 *
 * <p>Se aceptan <b>varios</b> client id, no uno solo, y tambien es a proposito:
 * el frontend usa su client de PKCE y los tests usan el client {@code pruebas},
 * que puede pedir un token con usuario y clave sin pasar por el navegador. Son
 * dos client id distintos para tokens igual de legitimos. La lista la emite
 * {@code tools/crear-cognito.sh} en {@code COGNITO_CLIENT_IDS_VALIDOS}.
 *
 * @see <a href="../../../../../../../docs/ARQUITECTURA.md">ARQUITECTURA.md seccion 9</a>
 */
@Singleton
public class ValidadorDeClientId implements GenericJwtClaimsValidator<HttpRequest<?>> {

    private static final Logger LOG = LoggerFactory.getLogger(ValidadorDeClientId.class);

    /** El claim que Cognito usa en lugar de {@code aud}. */
    private static final String CLAIM_CLIENT_ID = "client_id";

    private final List<String> clientIdsValidos;

    public ValidadorDeClientId(@Value("${cognito.client-ids-validos:}") String configurados) {
        this.clientIdsValidos = Arrays.stream(configurados.split(","))
                .map(String::trim)
                .filter(s -> !s.isEmpty())
                .toList();

        if (this.clientIdsValidos.isEmpty()) {
            // No se lanza excepcion para no impedir que el servicio arranque en
            // un entorno sin Cognito (por ejemplo los tests que no lo necesitan),
            // pero se avisa fuerte: sin esta lista, ningun token pasa.
            LOG.warn("cognito.client-ids-validos esta vacio: se rechazaran TODOS los tokens. "
                    + "Revisa que COGNITO_CLIENT_IDS_VALIDOS este en el entorno.");
        }
    }

    @Override
    public boolean validate(Claims claims, @Nullable HttpRequest<?> request) {
        Object clientId = claims.get(CLAIM_CLIENT_ID);

        if (clientId == null) {
            LOG.debug("Token rechazado: no trae el claim client_id");
            return false;
        }

        boolean valido = clientIdsValidos.contains(clientId.toString());
        if (!valido) {
            LOG.debug("Token rechazado: client_id '{}' no esta en la lista de clients validos", clientId);
        }
        return valido;
    }
}
