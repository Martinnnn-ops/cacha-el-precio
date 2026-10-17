using Amazon;
using Amazon.CognitoIdentityProvider;
using Amazon.SQS;
using Microsoft.EntityFrameworkCore;
using Notification_Service.Consumidor;
using Notification_Service.Correo;
using Notification_Service.Data;
using Notification_Service.Mensajeria;
using Notification_Service.Repository;
using Notification_Service.Seguidores;
using Notification_Service.Service;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;

// ─── Base de datos: esquema propio `notification` ───────────────────────────
string connectionString =
    config.GetConnectionString("PostgreSql")
    ?? throw new InvalidOperationException("No existe ConnectionStrings:PostgreSql.");

builder.Services.AddDbContext<NotificationDbContext>(options => options.UseNpgsql(
    connectionString,
    postgres => postgres.MigrationsHistoryTable("__EFMigrationsHistory", "notification")));

// ─── AWS: region de la cadena de credenciales por defecto (rol de la EC2) ────
string region = config["AWS_REGION"] ?? config["MENSAJERIA_REGION"] ?? "us-east-1";
var regionEndpoint = RegionEndpoint.GetBySystemName(region);

builder.Services.AddSingleton<IAmazonSQS>(_ => new AmazonSQSClient(regionEndpoint));
builder.Services.AddSingleton<IColaMensajes, ColaMensajesSqs>();

// ─── Seguidores (HTTP interno a product-service) ────────────────────────────
string productUrl = config["PRODUCT_URL"] ?? "http://product-service:8081";
builder.Services.AddHttpClient<ISeguidoresClient, SeguidoresClient>(http =>
{
    http.BaseAddress = new Uri(productUrl.TrimEnd('/') + "/");
    http.Timeout = TimeSpan.FromSeconds(10);
});

// ─── sub -> email: Cognito si hay user pool, si no un mapa de configuracion ──
if (!string.IsNullOrWhiteSpace(config["COGNITO_USER_POOL_ID"]))
{
    builder.Services.AddSingleton<IAmazonCognitoIdentityProvider>(
        _ => new AmazonCognitoIdentityProviderClient(regionEndpoint));
    builder.Services.AddScoped<IDirectorioCorreos, DirectorioCorreosCognito>();
}
else
{
    builder.Services.AddScoped<IDirectorioCorreos, DirectorioCorreosConfig>();
}

// ─── Envio de correo segun CORREO_MODO ──────────────────────────────────────
string correoModo = (config["CORREO_MODO"] ?? "log").ToLowerInvariant();
if (correoModo == "smtp")
{
    if (string.IsNullOrWhiteSpace(config["SMTP_USUARIO"]) || string.IsNullOrWhiteSpace(config["SMTP_CLAVE"]))
        throw new InvalidOperationException("CORREO_MODO=smtp exige SMTP_USUARIO y SMTP_CLAVE.");
    builder.Services.AddSingleton<IEnviadorCorreo, EnviadorCorreoSmtp>();
}
else
{
    builder.Services.AddSingleton<IEnviadorCorreo, EnviadorCorreoLog>();
}

builder.Services.AddScoped<INotificacionesRepository, NotificacionesRepository>();
builder.Services.AddScoped<ServicioNotificaciones>();
builder.Services.AddHostedService<ConsumidorNotificaciones>();

builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();

var app = builder.Build();

using (IServiceScope scope = app.Services.CreateScope())
{
    var database = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
    database.Database.Migrate();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();

    // Diagnostico solo en desarrollo (expone correos: no se enruta en el gateway):
    // comprueba la cadena productId -> seguidores -> correos de la T08.
    app.MapGet("/internal/diagnostico/seguidores/{productId:int:min(1)}",
        async (int productId, ISeguidoresClient seguidores, IDirectorioCorreos directorio,
            CancellationToken ct) =>
        {
            var subs = await seguidores.ObtenerSeguidoresAsync(productId, ct);
            var correos = new List<object>();
            foreach (var sub in subs)
                correos.Add(new { sub, correo = await directorio.ObtenerCorreoAsync(sub, ct) });
            return Results.Ok(new { productId, seguidores = correos });
        });
}

app.MapHealthChecks("/health");

app.Run();
