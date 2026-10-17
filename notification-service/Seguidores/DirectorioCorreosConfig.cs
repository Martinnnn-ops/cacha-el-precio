namespace Notification_Service.Seguidores;

// Directorio para desarrollo local y pruebas, sin cuenta de AWS. Lee un mapa
// sub -> correo de la configuracion `CorreosPrueba` (por ejemplo en
// appsettings.Development.json o la variable CorreosPrueba__<sub>). Asi el
// flujo completo se puede demostrar en modo log sin Cognito.
public sealed class DirectorioCorreosConfig(IConfiguration config) : IDirectorioCorreos
{
    public Task<string?> ObtenerCorreoAsync(string sub, CancellationToken ct) =>
        Task.FromResult(config.GetSection("CorreosPrueba")[sub]);
}
