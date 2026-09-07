package cl.cachaelprecio.product.repository;

import cl.cachaelprecio.product.model.Producto;
import io.micronaut.data.annotation.Modifying;
import io.micronaut.data.annotation.Query;
import io.micronaut.data.jdbc.annotation.JdbcRepository;
import io.micronaut.data.model.query.builder.sql.Dialect;
import io.micronaut.data.repository.CrudRepository;

import java.util.List;

@JdbcRepository(dialect = Dialect.SQLITE)
public interface ProductoRepository extends CrudRepository<Producto, Long> {

    List<Producto> findByCatalogoId(Long catalogoId);

    List<Producto> findByActivo(boolean activo);

    List<Producto> findByCatalogoIdAndActivo(Long catalogoId, boolean activo);

    // Sube el contador y guarda cuándo fue la última visita, sin tocar el resto
    // de la fila. SQL nativo porque es un UPDATE sobre una columna, no una
    // entidad completa.
    @Modifying
    @Query("UPDATE productos SET visitas = visitas + 1, visto_en = :ahora WHERE id = :id")
    int registrarVisita(Long id, String ahora);
}
