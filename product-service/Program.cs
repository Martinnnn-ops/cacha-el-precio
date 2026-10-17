using Scalar.AspNetCore;
using Asp.Versioning;
using Amazon;
using Amazon.SimpleNotificationService;
using Product_Service.Data;
using Microsoft.EntityFrameworkCore;
using Product_Service.Mensajeria;
using Product_Service.Repository;
using Product_Service.Service;

var builder = WebApplication.CreateBuilder(args);
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

builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.ReportApiVersions = true;
    options.ApiVersionReader = new HeaderApiVersionReader("Version");
}).AddMvc();

builder.Services.AddControllers();

builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();

var app = builder.Build();

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
app.MapHealthChecks("/health");

app.Run();
