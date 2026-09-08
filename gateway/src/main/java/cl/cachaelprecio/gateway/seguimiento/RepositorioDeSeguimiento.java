package cl.cachaelprecio.gateway.seguimiento;

import jakarta.inject.Singleton;

import java.util.LinkedHashSet;
import java.util.Map;
import java.util.Set;
import java.util.concurrent.ConcurrentHashMap;

/**
 * Guarda que productos sigue cada persona.
 *
 * <p>⚠️ <b>En memoria, y se pierde al reiniciar el servicio.</b> Esta dicho acá
 * y no escondido: hoy no hay tabla para esto. Cuando el seguimiento tenga que
 * sobrevivir a un despliegue, la lista pasa a la base y este singleton
 * desaparece — el controlador no se entera, porque habla con esta interfaz y no
 * con un mapa.
 *
 * <p>Se guarda por el {@code sub} de Cognito, que es el identificador estable
 * del usuario. No por el correo: el correo se puede cambiar y entonces la
 * persona perderia su lista.
 */
@Singleton
public class RepositorioDeSeguimiento {

    private final Map<String, Set<Long>> porUsuario = new ConcurrentHashMap<>();

    public Set<Long> listar(String usuario) {
        return Set.copyOf(porUsuario.getOrDefault(usuario, Set.of()));
    }

    /** @return true si se agrego, false si ya estaba */
    public boolean seguir(String usuario, Long producto) {
        return porUsuario
                .computeIfAbsent(usuario, u -> ConcurrentHashMap.newKeySet())
                .add(producto);
    }

    /** @return true si se quito, false si no lo seguia */
    public boolean dejarDeSeguir(String usuario, Long producto) {
        Set<Long> suyos = porUsuario.get(usuario);
        return suyos != null && suyos.remove(producto);
    }
}
