using System.Text.RegularExpressions;

namespace Notification_Service.Observabilidad;

/// <summary>
/// El correlationId: el «número de seguimiento» de un pedido o de un barrido.
/// Entra por el encabezado <c>X-Correlation-Id</c> (pedidos web) o por el campo
/// <c>correlationId</c> del sobre (mensajes de SQS, SNS y Kinesis), y todo log
/// escrito mientras se atiende lo lleva en su scope. Ver docs/PLAN-EFT.md §3.
/// </summary>
public static partial class Correlacion
{
    public const string Encabezado = "X-Correlation-Id";

    private static readonly AsyncLocal<string?> _actual = new();

    /// <summary>El id del pedido o mensaje que se está atendiendo, si hay uno.</summary>
    public static string? Actual => _actual.Value;

    /// <summary>Un id nuevo para un pedido web que llegó sin uno.</summary>
    public static string Nuevo() => $"web-{Guid.NewGuid():N}"[..16];

    /// <summary>
    /// El id solo se acepta si es corto y con caracteres seguros: viene de
    /// afuera y termina escrito en los logs.
    /// </summary>
    public static string? Validar(string? id) =>
        id is { Length: > 0 and <= 128 } && Seguro().IsMatch(id) ? id : null;

    [GeneratedRegex("^[A-Za-z0-9._:-]+$")]
    private static partial Regex Seguro();

    /// <summary>
    /// Marca el inicio del trabajo con este id: queda como <see cref="Actual"/>
    /// y en el scope de los logs. Al soltarlo se restaura el anterior.
    /// </summary>
    public static IDisposable Iniciar(ILogger log, string? id)
    {
        string? anterior = _actual.Value;
        _actual.Value = id;
        IDisposable? scope = id is null
            ? null
            : log.BeginScope(new Dictionary<string, object> { ["correlationId"] = id });
        return new Restaurar(anterior, scope);
    }

    private sealed class Restaurar(string? anterior, IDisposable? scope) : IDisposable
    {
        public void Dispose()
        {
            scope?.Dispose();
            _actual.Value = anterior;
        }
    }

    /// <summary>
    /// Cada pedido web: toma el id que trae (o crea uno), lo pone en los logs y
    /// lo devuelve en la respuesta para poder citarlo («falló el pedido web-3f2a…»).
    /// </summary>
    public static IApplicationBuilder UsarCorrelacion(this IApplicationBuilder app) =>
        app.Use(async (contexto, siguiente) =>
        {
            string id = Validar(contexto.Request.Headers[Encabezado]) ?? Nuevo();
            contexto.Response.Headers[Encabezado] = id;
            var log = contexto.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("Correlacion");
            using (Iniciar(log, id))
            {
                await siguiente(contexto);
            }
        });
}
