package cl.cachaelprecio.product.service;

import cl.cachaelprecio.product.model.Producto;
import cl.cachaelprecio.product.repository.ProductoRepository;
import jakarta.inject.Singleton;

import java.util.Comparator;
import java.util.List;
import java.util.Optional;
import java.util.stream.StreamSupport;

/**
 * Lógica de productos. Por ahora es intencionalmente directa; si después aparecen reglas más
 * pesadas, este es el lugar donde deberían vivir y no en el controller.
 */
@Singleton
public class ProductoService {

    private final ProductoRepository productoRepository;

    public ProductoService(ProductoRepository productoRepository) {
        this.productoRepository = productoRepository;
    }

    public List<Producto> listar() {
        return StreamSupport.stream(productoRepository.findAll().spliterator(), false).toList();
    }

    public List<Producto> listarOrdenados() {
        return listar().stream()
                .sorted(Comparator.comparing(Producto::getNombre, String.CASE_INSENSITIVE_ORDER))
                .toList();
    }

    public List<Producto> listarFiltrados(Optional<Long> catalogoId, boolean soloActivos) {
        if (catalogoId.isPresent() && soloActivos) {
            return productoRepository.findByCatalogoIdAndActivo(catalogoId.get(), true);
        }
        if (catalogoId.isPresent()) {
            return productoRepository.findByCatalogoId(catalogoId.get());
        }
        if (soloActivos) {
            return productoRepository.findByActivo(true);
        }
        return listar();
    }

    public Optional<Producto> buscarPorId(Long id) {
        return productoRepository.findById(id);
    }

    public Producto crear(Producto producto) {
        producto.setId(null);
        return productoRepository.save(producto);
    }

    public Optional<Producto> actualizar(Long id, Producto producto) {
        if (!productoRepository.existsById(id)) {
            return Optional.empty();
        }
        producto.setId(id);
        return Optional.of(productoRepository.update(producto));
    }

    public boolean eliminar(Long id) {
        if (!productoRepository.existsById(id)) {
            return false;
        }
        productoRepository.deleteById(id);
        return true;
    }
}
