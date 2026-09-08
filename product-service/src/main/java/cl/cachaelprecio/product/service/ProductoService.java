package cl.cachaelprecio.product.service;

import cl.cachaelprecio.product.model.Producto;
import cl.cachaelprecio.product.repository.ProductoRepository;
import jakarta.inject.Singleton;

import java.time.Instant;
import java.util.Comparator;
import java.util.List;
import java.util.Optional;
import java.util.stream.StreamSupport;

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

        // La fecha de nacimiento la pone el servicio, no quien hace el POST:
        // el scraper no tiene por qué saber que existe el campo.
        if (producto.getCreadoEn() == null) {
            producto.setCreadoEn(Instant.now().toString());
        }

        return productoRepository.save(producto);
    }

    public Optional<Producto> actualizar(Long id, Producto producto) {
        Optional<Producto> actual = productoRepository.findById(id);

        if (actual.isEmpty()) {
            return Optional.empty();
        }

        producto.setId(id);

        // El scraper envía el producto entero (nombre, precio, url, imagen,
        // marca) para que quede al día, pero no conoce las columnas de
        // actividad. Si no se preservara el estado anterior, cada actualización
        // pondría visitas a 0 y se perdería la fecha de nacimiento.
        producto.setVisitas(actual.get().getVisitas());
        producto.setVistoEn(actual.get().getVistoEn());

        if (actual.get().getCreadoEn() != null) {
            producto.setCreadoEn(actual.get().getCreadoEn());
        } else if (producto.getCreadoEn() == null) {
            // Producto viejo de antes de la V3: cobra fecha la primera vez que
            // se actualiza, para que "agregado hace X" no quede vacío eterno.
            producto.setCreadoEn(Instant.now().toString());
        }

        return Optional.of(productoRepository.update(producto));
    }

    public Optional<Integer> registrarVisita(Long id) {
        // Lee, suma y vuelve a guardar: el UPDATE del scraper no debe pisar el
        // contador, así que el incremento atraviesa el mismo repository que todo.
        // Para el caudal de la app un read-modify-write no compite con nadie.
        Optional<Producto> actual = productoRepository.findById(id);

        if (actual.isEmpty()) {
            return Optional.empty();
        }

        Producto producto = actual.get();
        producto.setVisitas(producto.getVisitas() + 1);
        producto.setVistoEn(Instant.now().toString());

        return Optional.of(productoRepository.update(producto).getVisitas());
    }

    public boolean eliminar(Long id) {
        if (!productoRepository.existsById(id)) {
            return false;
        }
        productoRepository.deleteById(id);
        return true;
    }
}
