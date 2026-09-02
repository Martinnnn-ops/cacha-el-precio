package cl.cachaelprecio.product.model;

import io.micronaut.data.annotation.GeneratedValue;
import io.micronaut.data.annotation.Id;
import io.micronaut.data.annotation.MappedEntity;
import io.micronaut.data.annotation.MappedProperty;
import io.micronaut.serde.annotation.Serdeable;

import java.math.BigDecimal;

/**
 * Producto canónico del sistema. Todavía es un modelo chico porque primero queremos que funcione
 * el flujo completo y después agregar variantes, marcas y el resto de cosas entretenidas.
 */
@Serdeable
@MappedEntity("productos")
public class Producto {

    @Id
    @GeneratedValue
    private Long id;

    private String nombre;

    private String descripcion;

    private BigDecimal precio;

    @MappedProperty("catalogo_id")
    private Long catalogoId;

    private boolean activo;

    public Producto() {
    }

    public Producto(Long id, String nombre, String descripcion, BigDecimal precio,
                    Long catalogoId, boolean activo) {
        this.id = id;
        this.nombre = nombre;
        this.descripcion = descripcion;
        this.precio = precio;
        this.catalogoId = catalogoId;
        this.activo = activo;
    }

    public Long getId() {
        return id;
    }

    public void setId(Long id) {
        this.id = id;
    }

    public String getNombre() {
        return nombre;
    }

    public void setNombre(String nombre) {
        this.nombre = nombre;
    }

    public String getDescripcion() {
        return descripcion;
    }

    public void setDescripcion(String descripcion) {
        this.descripcion = descripcion;
    }

    public BigDecimal getPrecio() {
        return precio;
    }

    public void setPrecio(BigDecimal precio) {
        this.precio = precio;
    }

    public Long getCatalogoId() {
        return catalogoId;
    }

    public void setCatalogoId(Long catalogoId) {
        this.catalogoId = catalogoId;
    }

    public boolean isActivo() {
        return activo;
    }

    public void setActivo(boolean activo) {
        this.activo = activo;
    }
}
