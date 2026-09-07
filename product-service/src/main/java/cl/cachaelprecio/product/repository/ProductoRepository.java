package cl.cachaelprecio.product.repository;

import cl.cachaelprecio.product.model.Producto;
import io.micronaut.data.jdbc.annotation.JdbcRepository;
import io.micronaut.data.model.query.builder.sql.Dialect;
import io.micronaut.data.repository.CrudRepository;

import java.util.List;

@JdbcRepository(dialect = Dialect.SQLITE)
public interface ProductoRepository extends CrudRepository<Producto, Long> {

    List<Producto> findByCatalogoId(Long catalogoId);

    List<Producto> findByActivo(boolean activo);

    List<Producto> findByCatalogoIdAndActivo(Long catalogoId, boolean activo);
}
