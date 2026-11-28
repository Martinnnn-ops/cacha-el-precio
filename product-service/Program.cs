using Product_Service.Observabilidad;
using Scalar.AspNetCore;
using Asp.Versioning;
using Amazon;
using Amazon.Kinesis;
using Amazon.SimpleNotificationService;
using Amazon.SQS;
using Product_Service.Auditoria;
using Product_Service.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Product_Service.Mensajeria;
using Product_Service.Repository;
using Product_Service.Service;
using Product_Service.Streaming;

var builder = WebApplication.CreateBuilder(args);
builder.UsarLogsJson();
var config = builder.Configuration;

// Add services to the container.
string connectionString =
    config.GetConnectionString("PostgreSql")
    ?? throw new InvalidOperationException("No existe ConnectionStrings:PostgreSql.");

builder.Services.AddDbContext<ProductDbContext>(options => options.UseNpgsql(
    connectionString,
    postgres => postgres.MigrationsHistoryTable("__EFMigrationsHistory", "product")));

builder.Services.AddScoped<IProductRepository, ProductRepository>();

// ─── Mensajería: publica los cambios de oferta en SNS (flujo 2 del EP3) ──────
// Con tópico configurado habla con SNS; sin él (local, sin AWS) solo lo registra
// en el log y el servicio sigue igual. Ver docs/MENSAJERIA.md y ADR-031.
if (!string.IsNullOrWhiteSpace(config["SNS_CAMBIOS_OFERTA_ARN"]))
{
    string region = config["AWS_REGION"] ?? config["MENSAJERIA_REGION"] ?? "us-east-1";
    builder.Services.AddSingleton<IAmazonSimpleNotificationService>(
        _ => new AmazonSimpleNotificationServiceClient(RegionEndpoint.GetBySystemName(region)));
    builder.Services.AddSingleton<IPublicadorEventos, PublicadorEventosSns>();
}
else
{
    builder.Services.AddSingleton<IPublicadorEventos, PublicadorEventosLog>();
}

builder.Services.AddScoped<ProductService>();

// ─── Mensajería: consume las ofertas del scraper (flujo 1 del EP3) ───────────
// Sin URL de la cola (local, sin AWS) el consumidor no se registra y el POST
// /api/products sigue funcionando igual. Ver docs/MENSAJERIA.md y ADR-029.
builder.Services.AddScoped<ProcesadorOfertaCapturada>();
if (!string.IsNullOrWhiteSpace(config["SQS_OFERTAS_CRUDAS_URL"]))
{
    string region = config["AWS_REGION"] ?? config["MENSAJERIA_REGION"] ?? "us-east-1";
    builder.Services.TryAddSingleton<IAmazonSQS>(_ => new AmazonSQSClient(RegionEndpoint.GetBySystemName(region)));
    builder.Services.AddSingleton<IColaMensajes, ColaMensajesSqs>();
    builder.Services.AddHostedService<ConsumidorOfertas>();
}

// ─── Auditoría: lee cep-auditoria-precios (la copia de todos los cambios) ─────
// Guarda cada cambio una vez por eventId y marca los sospechosos. Sin URL de la
// cola (local) no se registra. Ver docs/PLAN-EFT.md §4.
builder.Services.AddScoped<ProcesadorAuditoria>();
if (!string.IsNullOrWhiteSpace(config["SQS_AUDITORIA_PRECIOS_URL"]))
{
    string region = config["AWS_REGION"] ?? config["MENSAJERIA_REGION"] ?? "us-east-1";
    builder.Services.TryAddSingleton<IAmazonSQS>(_ => new AmazonSQSClient(RegionEndpoint.GetBySystemName(region)));
    builder.Services.AddKeyedSingleton<IColaMensajes>(ConsumidorAuditoria.Cola, (sp, _) => ColaMensajesSqs.Para(
        sp.GetRequiredService<IAmazonSQS>(),
        config["SQS_AUDITORIA_PRECIOS_URL"]!,
        config["SQS_AUDITORIA_PRECIOS_DLQ_URL"] ?? "",
        sp.GetRequiredService<ILogger<ColaMensajesSqs>>()));
    builder.Services.AddHostedService<ConsumidorAuditoria>();
}

// ─── Stream de precios: lee cep-precios de Kinesis y calcula el precio habitual ──
// (EA3/EFT). Sin nombre de stream (local, sin AWS) el lector no se registra y el
// resto del servicio sigue igual. La DLQ cep-precios-dlq usa el mismo SQS; el
// cliente se comparte con el consumidor de ofertas si ambos están activos.
// Ver docs/PLAN-EFT.md §5-6 y ADR-035.
builder.Services.AddScoped<IProcesadorPrecio, ProcesadorPrecioCapturado>();
if (!string.IsNullOrWhiteSpace(config["KINESIS_PRECIOS_STREAM"]))
{
    string region = config["AWS_REGION"] ?? config["KINESIS_REGION"]
        ?? config["MENSAJERIA_REGION"] ?? "us-east-1";
    builder.Services.AddSingleton<IAmazonKinesis>(
        _ => new AmazonKinesisClient(RegionEndpoint.GetBySystemName(region)));
    builder.Services.TryAddSingleton<IAmazonSQS>(
        _ => new AmazonSQSClient(RegionEndpoint.GetBySystemName(region)));
    builder.Services.AddSingleton<ILectorStream, LectorStreamKinesis>();
    builder.Services.AddSingleton<IBuzonErrores, BuzonErroresSqs>();
    builder.Services.AddHostedService<LectorPrecios>();
}

builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.ReportApiVersions = true;
    options.ApiVersionReader = new HeaderApiVersionReader("Version");
}).AddMvc();

builder.Services.AddControllers();

builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();

builder.Services.AddScoped<RegistroTrazas>();

var app = builder.Build();

// Primero de todo: cada pedido lleva su número de seguimiento (PLAN-EFT §3).
app.UsarCorrelacion();

if (string.IsNullOrWhiteSpace(config["SQS_OFERTAS_CRUDAS_URL"]))
    app.Logger.LogWarning("ConsumidorOfertas desactivado: SQS_OFERTAS_CRUDAS_URL viene vacia (la API HTTP sigue igual)");

using (IServiceScope scope = app.Services.CreateScope())
{
    var database = scope.ServiceProvider.GetRequiredService<ProductDbContext>();
    database.Database.Migrate();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseAuthorization();

app.MapControllers();
app.MapAuditoria();
app.MapTrazas();
app.MapHealthChecks("/health");

app.Run();
