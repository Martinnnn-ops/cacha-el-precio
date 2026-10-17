using System.Collections.Concurrent;
using Amazon.CognitoIdentityProvider;
using Amazon.CognitoIdentityProvider.Model;

namespace Notification_Service.Seguidores;

// sub -> email con ListUsers de Cognito, usando el rol de la EC2 (cadena de
// credenciales por defecto del SDK). Cachea una hora en memoria para no pegarle
// a Cognito por cada mensaje. Si el correo no esta verificado, devuelve null.
public sealed class DirectorioCorreosCognito(
    IAmazonCognitoIdentityProvider cognito,
    IConfiguration config,
    ILogger<DirectorioCorreosCognito> log) : IDirectorioCorreos
{
    private static readonly TimeSpan Ttl = TimeSpan.FromHours(1);
    private readonly ConcurrentDictionary<string, (string? Correo, DateTimeOffset Expira)> cache = new();
    private readonly string poolId = config["COGNITO_USER_POOL_ID"] ?? "";

    public async Task<string?> ObtenerCorreoAsync(string sub, CancellationToken ct)
    {
        if (cache.TryGetValue(sub, out var guardado) && guardado.Expira > DateTimeOffset.UtcNow)
            return guardado.Correo;

        var correo = await ConsultarAsync(sub, ct);
        cache[sub] = (correo, DateTimeOffset.UtcNow + Ttl);
        return correo;
    }

    private async Task<string?> ConsultarAsync(string sub, CancellationToken ct)
    {
        var respuesta = await cognito.ListUsersAsync(new ListUsersRequest
        {
            UserPoolId = poolId,
            Filter = $"sub = \"{sub}\"",
            Limit = 1
        }, ct);

        var usuario = respuesta.Users.FirstOrDefault();
        if (usuario is null)
        {
            log.LogWarning("Cognito no tiene usuario con sub {Sub}", sub);
            return null;
        }

        string? correo = null;
        bool verificado = false;
        foreach (var atributo in usuario.Attributes)
        {
            if (atributo.Name == "email") correo = atributo.Value;
            else if (atributo.Name == "email_verified") verificado = atributo.Value == "true";
        }

        if (correo is null || !verificado)
        {
            log.LogWarning("Se salta el sub {Sub}: correo ausente o no verificado", sub);
            return null;
        }
        return correo;
    }
}
