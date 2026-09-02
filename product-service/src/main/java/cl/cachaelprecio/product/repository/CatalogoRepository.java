package cl.cachaelprecio.product.repository;

import cl.cachaelprecio.product.model.Catalogo;
import io.micronaut.data.jdbc.annotation.JdbcRepository;
import io.micronaut.data.model.query.builder.sql.Dialect;
import io.micronaut.data.repository.CrudRepository;

@JdbcRepository(dialect = Dialect.SQLITE)
public interface CatalogoRepository extends CrudRepository<Catalogo, Long> {
}
