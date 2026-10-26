
namespace CachaElPrecio.Gateway.Mensajeria;

// El componente que encapsula crear, modificar y eliminar recursos de mensajeria.
// Es la UNICA pieza del sistema que habla con el SDK de SQS/SNS para administrar.
//
// Dos implementaciones:
//   · AdministradorMensajeriaAws     → SQS y SNS de verdad (MENSAJERIA_MODO=aws)
//   · AdministradorMensajeriaMemoria → en memoria, para programar y probar sin
//                                      AWS (MENSAJERIA_MODO=memoria)
//
// Esta capa NO valida ni aplica reglas: recibe configuraciones ya revisadas por
// ServicioAdministracion y solo ejecuta. Si un recurso no existe devuelve null
// o lanza RecursoNoEncontradoException; los errores de AWS salen como
// ErrorProveedorException.
//
// Los nombres que recibe y devuelve son siempre completos (con el prefijo cep-).
public interface IAdministradorMensajeria
{
    string Modo { get; }

    // ── Colas ──────────────────────────────────────────────────────────────
    Task<IReadOnlyList<ColaInfo>> ListarColasAsync(CancellationToken ct);
    Task<ColaInfo?> ObtenerColaAsync(string nombre, CancellationToken ct);
    Task CrearColaAsync(ConfiguracionCola cola, CancellationToken ct);
    Task ActualizarColaAsync(ConfiguracionCola cola, CancellationToken ct);
    Task EliminarColaAsync(string nombre, CancellationToken ct);

    // ── Topicos ────────────────────────────────────────────────────────────
    Task<IReadOnlyList<TopicoInfo>> ListarTopicosAsync(CancellationToken ct);
    Task CrearTopicoAsync(string nombre, CancellationToken ct);
    Task EliminarTopicoAsync(string nombre, CancellationToken ct);

    // ── Suscripciones (reglas de distribucion) ─────────────────────────────
    Task<IReadOnlyList<SuscripcionInfo>> ListarSuscripcionesAsync(CancellationToken ct);

    // Crea la suscripcion si no existe y deja el filtro y la entrega cruda como
    // vienen. Tambien ajusta la politica de la cola para que acepte al topico:
    // sin eso SNS publica "bien" y el mensaje nunca llega.
    Task SuscribirAsync(ConfiguracionSuscripcion suscripcion, CancellationToken ct);
    Task EliminarSuscripcionAsync(string topico, string cola, CancellationToken ct);

    // Publica un mensaje en un topico con sus atributos (los que miran los
    // filtros de las reglas). Devuelve el MessageId. Lo usa el simulador de
    // avisos del panel; los servicios publican con su propio publicador.
    Task<string> PublicarAsync(string topico, string mensaje, IReadOnlyList<AtributoMensaje> atributos, CancellationToken ct);

    // ── DLQ ────────────────────────────────────────────────────────────────
    // Lee hasta `maximo` mensajes sin borrarlos (vuelven a quedar visibles).
    Task<IReadOnlyList<MensajeDlq>> VerMensajesAsync(string cola, int maximo, CancellationToken ct);

    // Devuelve los mensajes de la DLQ a `destino` (la cola que la usa como DLQ).
    Task<ResultadoReproceso> ReprocesarAsync(string dlq, string destino, CancellationToken ct);
}

// Un atributo de mensaje SNS: Tipo es "String" o "Number".
public sealed record AtributoMensaje(string Nombre, string Tipo, string Valor);
