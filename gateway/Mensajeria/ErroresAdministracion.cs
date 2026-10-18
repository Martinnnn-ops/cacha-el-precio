namespace CachaElPrecio.Gateway.Mensajeria;

// Las reglas de negocio lanzan estas excepciones y EndpointsAdministracion las
// traduce a una respuesta HTTP clara (ProblemDetails). Asi el negocio no sabe de
// codigos HTTP y los endpoints no repiten la logica de errores.

// 400: la solicitud tiene campos invalidos. Trae el detalle por campo.
public sealed class SolicitudInvalidaException(IDictionary<string, string[]> errores)
    : Exception("La solicitud no es valida.")
{
    public IDictionary<string, string[]> Errores { get; } = errores;

    public static SolicitudInvalidaException Campo(string campo, string mensaje) =>
        new(new Dictionary<string, string[]> { [campo] = [mensaje] });
}

// 404: el recurso pedido no existe.
public sealed class RecursoNoEncontradoException(string tipo, string nombre)
    : Exception($"No existe {tipo} '{nombre}'.");

// 409: la operacion choca con el estado actual (ya existe, esta protegido, esta en uso).
public sealed class ConflictoException(string mensaje) : Exception(mensaje);

// 502: AWS rechazo o fallo la operacion. Se distingue el permiso denegado porque
// en el Learner Lab es lo mas comun y tiene una causa concreta (LabRole).
public sealed class ErrorProveedorException(string mensaje, bool permisoDenegado, Exception? interna = null)
    : Exception(mensaje, interna)
{
    public bool PermisoDenegado { get; } = permisoDenegado;
}
