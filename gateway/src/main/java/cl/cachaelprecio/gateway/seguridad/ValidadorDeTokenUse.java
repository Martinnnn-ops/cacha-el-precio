package cl.cachaelprecio.gateway.seguridad;

import io.micronaut.core.annotation.Nullable;
import io.micronaut.http.HttpRequest;
import io.micronaut.security.token.Claims;
import io.micronaut.security.token.jwt.validator.GenericJwtClaimsValidator;
import jakarta.inject.Singleton;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

/**
 * Exige que el token presentado sea un <b>access_token</b> y no un id_token.
 *
 * <p>Cognito emite tres tokens en el mismo login y los tres estan firmados por
 * el mismo pool, asi que los tres pasan la verificacion de firma y de emisor.
 * Lo unico que los distingue es el claim {@code token_use}.
 *
 * <p>Sin esta comprobacion, un frontend que se equivoque de variable y mande el
 * id_token en la cabecera {@code Authorization} entraria igual. Y el id_token
 * dice <i>quien</i> es el usuario, no <i>que</i> puede hacer: no trae scopes.
 * Aceptarlo seria autorizar con un documento que no habla de permisos.
 */
@Singleton
public class ValidadorDeTokenUse implements GenericJwtClaimsValidator<HttpRequest<?>> {

    private static final Logger LOG = LoggerFactory.getLogger(ValidadorDeTokenUse.class);

    private static final String CLAIM_TOKEN_USE = "token_use";
    private static final String TOKEN_DE_ACCESO = "access";

    @Override
    public boolean validate(Claims claims, @Nullable HttpRequest<?> request) {
        Object uso = claims.get(CLAIM_TOKEN_USE);

        if (uso == null) {
            LOG.debug("Token rechazado: no trae el claim token_use");
            return false;
        }

        boolean esDeAcceso = TOKEN_DE_ACCESO.equals(uso.toString());
        if (!esDeAcceso) {
            LOG.debug("Token rechazado: token_use es '{}' y se esperaba 'access'", uso);
        }
        return esDeAcceso;
    }
}
