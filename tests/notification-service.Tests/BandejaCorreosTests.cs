using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Notification_Service.Correo;
using Notification_Service.Data;

namespace Notification_Service.Tests;

/// <summary>
/// Lo que muestra la bandeja del panel: el destinatario enmascarado, los
/// correos de ejemplo, y que la migracion de la tabla calce con el modelo
/// (la migracion se escribio a mano, sin dotnet-ef).
/// </summary>
public class BandejaCorreosTests
{
    [Theory]
    [InlineData("ana.perez@gmail.com", "a•••@gmail.com")]
    [InlineData("x@dominio.cl", "x•••@dominio.cl")]
    [InlineData("sin-arroba", "•••")]
    [InlineData("@raro.cl", "•••")]
    public void El_destinatario_sale_enmascarado(string correo, string esperado) =>
        Assert.Equal(esperado, EndpointsBandeja.Enmascarar(correo));

    [Theory]
    [InlineData("precio.bajo", "Bajó 30%")]
    [InlineData("oferta.agotada", "Se agotó")]
    [InlineData("oferta.repuesta", "Volvió")]
    public void Cada_tipo_tiene_su_correo_de_ejemplo(string tipo, string asunto)
    {
        var correo = PlantillaCorreo.Ejemplo(tipo, "https://sitio.test");

        Assert.StartsWith(asunto, correo.Asunto);
        Assert.Equal("ejemplo@cachaelprecio.cl", correo.Para);
        Assert.Contains("https://sitio.test", correo.Html);
        Assert.False(string.IsNullOrWhiteSpace(correo.Texto));
    }

    [Fact]
    public void El_snapshot_de_migraciones_calza_con_el_modelo()
    {
        var opciones = new DbContextOptionsBuilder<NotificationDbContext>()
            .UseNpgsql("Host=localhost;Database=sin-uso")
            .Options;
        using var db = new NotificationDbContext(opciones);

        var snapshot = db.GetService<IMigrationsAssembly>().ModelSnapshot!.Model;
        var inicializado = db.GetService<IModelRuntimeInitializer>().Initialize(snapshot);
        var diferencias = db.GetService<IMigrationsModelDiffer>().GetDifferences(
            inicializado.GetRelationalModel(),
            db.GetService<IDesignTimeModel>().Model.GetRelationalModel());

        Assert.Empty(diferencias);
    }

    [Fact]
    public void La_ultima_migracion_es_la_de_la_bandeja()
    {
        var opciones = new DbContextOptionsBuilder<NotificationDbContext>()
            .UseNpgsql("Host=localhost;Database=sin-uso")
            .Options;
        using var db = new NotificationDbContext(opciones);

        Assert.Equal("20261026120000_BandejaCorreos", db.Database.GetMigrations().Last());
    }
}
