using System.Text.Json.Nodes;

namespace CachaElPrecio.Gateway.Mensajeria;

// Lo que llega en el cuerpo de las solicitudes de administracion. Todo es
// opcional a proposito: el validador decide que falta y responde con un error
// claro por campo, en vez de un 400 generico de deserializacion.

public sealed record CrearColaSolicitud(
    string? Nombre,
    int? VisibilidadSegundos,
    int? RetencionSegundos,
    int? EsperaSegundos,
    string? Dlq,
    int? MaxIntentos);

public sealed record CrearTopicoSolicitud(string? Nombre);

public sealed record CrearSuscripcionSolicitud(
    string? Topico,
    string? Cola,
    JsonNode? Filtro,
    bool? EntregaCruda);

// Ya validadas y con los valores por defecto aplicados: es lo que recibe la
// capa de infraestructura (IAdministradorMensajeria), que no valida nada.

public sealed record ConfiguracionCola(
    string Nombre,
    int VisibilidadSegundos,
    int RetencionSegundos,
    int EsperaSegundos,
    ReglaDlq? Dlq);

public sealed record ConfiguracionSuscripcion(
    string Topico,
    string Cola,
    JsonNode? Filtro,
    bool EntregaCruda);
