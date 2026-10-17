namespace Notification_Service.Seguidores;

// Traduce un `sub` de Cognito al correo verificado de esa persona. Devuelve null
// si no existe o el correo no esta verificado: a esa persona se la salta.
public interface IDirectorioCorreos
{
    Task<string?> ObtenerCorreoAsync(string sub, CancellationToken ct);
}
