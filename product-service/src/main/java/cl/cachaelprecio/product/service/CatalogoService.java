package cl.cachaelprecio.product.service;

import cl.cachaelprecio.product.model.Catalogo;
import cl.cachaelprecio.product.repository.CatalogoRepository;
import jakarta.inject.Singleton;

import java.util.Comparator;
import java.util.List;
import java.util.Locale;
import java.util.Optional;
import java.util.stream.StreamSupport;

/**
 * Junta la lógica relacionada con catálogos para que el controller se dedique solamente a HTTP.
 */
@Singleton
public class CatalogoService {

    private final CatalogoRepository catalogoRepository;

    public CatalogoService(CatalogoRepository catalogoRepository) {
        this.catalogoRepository = catalogoRepository;
    }

    public List<Catalogo> listar() {
        return StreamSupport.stream(catalogoRepository.findAll().spliterator(), false).toList();
    }

    public List<Catalogo> listarOrdenados() {
        return listar().stream()
                .sorted(Comparator.comparing(Catalogo::getNombre, String.CASE_INSENSITIVE_ORDER))
                .toList();
    }

    public List<Catalogo> buscarPorNombre(String nombre) {
        String textoBuscado = nombre.toLowerCase(Locale.ROOT);
        return listarOrdenados().stream()
                .filter(catalogo -> catalogo.getNombre().toLowerCase(Locale.ROOT).contains(textoBuscado))
                .toList();
    }

    public Optional<Catalogo> buscarPorId(Long id) {
        return catalogoRepository.findById(id);
    }

    public Catalogo crear(Catalogo catalogo) {
        catalogo.setId(null);
        return catalogoRepository.save(catalogo);
    }

    public Optional<Catalogo> actualizar(Long id, Catalogo catalogo) {
        if (!catalogoRepository.existsById(id)) {
            return Optional.empty();
        }
        catalogo.setId(id);
        return Optional.of(catalogoRepository.update(catalogo));
    }

    public boolean eliminar(Long id) {
        if (!catalogoRepository.existsById(id)) {
            return false;
        }
        catalogoRepository.deleteById(id);
        return true;
    }
}
