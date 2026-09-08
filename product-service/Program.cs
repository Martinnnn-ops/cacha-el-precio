using Scalar.AspNetCore;
using Asp.Versioning;
using Product_Service.Data;
using Microsoft.EntityFrameworkCore;
using Product_Service.Repository;
using Product_Service.Service;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
string connectionString =
    builder.Configuration.GetConnectionString("Sqlite") ?? throw new InvalidOperationException("No existe ConnectionStrings:Sqlite.");

builder.Services.AddDbContext<ProductDbContext>(options => options.UseSqlite(connectionString));

builder.Services.AddScoped<IProductRepository, SqliteProductRepository>();

builder.Services.AddScoped<ProductService>();

builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
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
