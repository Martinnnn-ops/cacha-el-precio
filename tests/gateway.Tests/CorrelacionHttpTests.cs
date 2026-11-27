using System.Net;
using CachaElPrecio.Gateway.Observabilidad;
using CachaElPrecio.Gateway.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace CachaElPrecio.Gateway.Tests;

/// <summary>
/// El número de seguimiento de cada pedido web (PLAN-EFT §3): el gateway lo
/// crea o respeta el que llega, lo devuelve y se lo pasa a product-service.
/// </summary>
public class CorrelacionHttpTests : IDisposable
{
    private readonly Capturador productos = new();
    private readonly WebApplicationFactory<Program> fabrica;

    public CorrelacionHttpTests()
    {
        fabrica = new WebApplicationFactory<Program>().WithWebHostBuilder(web =>
        {
            web.UseSetting("COGNITO_ISSUER", "https://cognito.test/pool");
            web.UseSetting("COGNITO_CLIENT_IDS_VALIDOS", "cliente-test");
            web.UseSetting("MENSAJERIA_MODO", "memoria");
            web.UseSetting("TOPOLOGIA_RUTA", RutaTopologia());
            web.ConfigureTestServices(services =>
                services.AddHttpClient<ProductServiceProxy>()
                    .ConfigurePrimaryHttpMessageHandler(() => productos));
        });
    }

    public void Dispose()
    {
        fabrica.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task Un_pedido_sin_id_recibe_uno_nuevo_y_viaja_a_product_service()
    {
        using var cliente = fabrica.CreateClient();

        var respuesta = await cliente.GetAsync("/productos");

        string id = Assert.Single(respuesta.Headers.GetValues(Correlacion.Encabezado));
        Assert.StartsWith("web-", id);
        Assert.Equal(id, productos.UltimoId);
    }

    [Fact]
    public async Task Un_id_valido_se_respeta()
    {
        using var cliente = fabrica.CreateClient();
        cliente.DefaultRequestHeaders.Add(Correlacion.Encabezado, "demo-profe-001");

        var respuesta = await cliente.GetAsync("/productos");

        Assert.Equal("demo-profe-001", Assert.Single(respuesta.Headers.GetValues(Correlacion.Encabezado)));
        Assert.Equal("demo-profe-001", productos.UltimoId);
    }

    [Theory]
    [InlineData("hola mundo")]
    [InlineData("<script>")]
    [InlineData("id\"inyectado")]
    public async Task Un_id_con_caracteres_raros_se_reemplaza(string malo)
    {
        using var cliente = fabrica.CreateClient();
        cliente.DefaultRequestHeaders.TryAddWithoutValidation(Correlacion.Encabezado, malo);

        var respuesta = await cliente.GetAsync("/productos");

        string id = Assert.Single(respuesta.Headers.GetValues(Correlacion.Encabezado));
        Assert.StartsWith("web-", id);
        Assert.Equal(id, productos.UltimoId);
    }

    [Fact]
    public void Un_id_demasiado_largo_no_se_acepta() =>
        Assert.Null(Correlacion.Validar(new string('a', 129)));

    private static string RutaTopologia()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "infra", "mensajeria", "topologia.json")))
            dir = dir.Parent;
        return Path.Combine(dir!.FullName, "infra", "mensajeria", "topologia.json");
    }

    // product-service falso que anota el id que le llegó.
    private sealed class Capturador : HttpMessageHandler
    {
        public string? UltimoId { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            UltimoId = request.Headers.TryGetValues(Correlacion.Encabezado, out var v) ? v.Single() : null;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("[]", System.Text.Encoding.UTF8, "application/json")
            });
        }
    }
}
