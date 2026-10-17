namespace Notification_Service.Seguidores;

// Quien sigue una prenda. Lo resuelve product-service por la red interna; este
// servicio nunca lee el esquema `product` directo (ADR-030).
public interface ISeguidoresClient
{
    // Los `sub` de Cognito de quienes marcaron la prenda como deseada.
    Task<IReadOnlyList<string>> ObtenerSeguidoresAsync(int productId, CancellationToken ct);
}
