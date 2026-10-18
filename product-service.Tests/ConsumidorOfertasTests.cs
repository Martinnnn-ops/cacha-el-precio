using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Product_Service.Mensajeria;
using Product_Service.Models.DTOs;
using Product_Service.Models.Entities;
using Product_Service.Models.Eventos;
using Product_Service.Repository;
using Product_Service.Service;

namespace Product_Service.Tests;

/// <summary>
/// El consumidor de cep-ofertas-crudas (T05) y sus tres salidas: éxito (se
/// borra el mensaje), error definitivo (va a la DLQ con diagnóstico) y error
/// recuperable (no se toca: SQS lo reintenta). Usa una cola, un repositorio y un
/// publicador falsos, sin SQS ni base de datos.
/// </summary>
public class ConsumidorOfertasTests
{
    private const string OfertaValida = """
        {
          "eventId": "3b0f8d4e-6a51-4f7e-9b2a-1c9e0a7d2f11",
          "type": "oferta.capturada",
          "version": 1,
          "source": "scraper-service",
          "occurredAt": "2026-10-14T18:00:00Z",
          "data": {
            "externalId": "SKU-12345",
            "store": "tienda-ejemplo",
            "name": "Polera basica negra",
            "brand": "Marca",
            "category": "poleras",
            "gender": "unisex",
            "price": 14990,
            "sizes": ["S", "M", "L"],
            "url": "https://tienda-ejemplo.cl/polera-basica-negra",
            "image": "https://tienda-ejemplo.cl/img/polera.jpg",
            "active": true
          }
        }
        """;

    // ── Exito ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Oferta_valida_se_guarda_y_el_mensaje_se_borra()
    {
        var (consumidor, cola, repo) = Armar();

        await consumidor.ManejarAsync(Mensaje(OfertaValida), CancellationToken.None);

        var guardado = Assert.Single(repo.Agregados);
        Assert.Equal("Polera basica negra", guardado.ProductName);
        Assert.Equal(14990, Assert.Single(guardado.Offers).Price);
        Assert.Equal(["m-1"], cola.Borrados);
        Assert.Empty(cola.EnDlq);
    }

    // ── Error definitivo: a la DLQ con diagnostico, sin reintentos ──────

    [Theory]
    [InlineData("esto no es json", "JSON del sobre invalido")]
    [InlineData("""{"type":"oferta.capturada","version":1,"data":{}}""", "eventId")]
    [InlineData("""{"eventId":"e1","type":"precio.bajo","version":1,"data":{}}""", "type 'precio.bajo' desconocido")]
    [InlineData("""{"eventId":"e1","type":"oferta.capturada","version":2,"data":{}}""", "version 2")]
    [InlineData("""{"eventId":"e1","type":"oferta.capturada","version":1,"data":"texto"}""", "data debe ser un objeto")]
    public async Task Sobre_invalido_va_a_la_dlq(string cuerpo, string razon)
    {
        var (consumidor, cola, repo) = Armar();

        await consumidor.ManejarAsync(Mensaje(cuerpo), CancellationToken.None);

        var (id, diagnostico) = Assert.Single(cola.EnDlq);
        Assert.Equal("m-1", id);
        Assert.Contains(razon, diagnostico["error-reason"]);
        Assert.Equal("definitivo", diagnostico["error-type"]);
        Assert.Equal("product-service", diagnostico["failed-by"]);
        Assert.Equal("m-1", diagnostico["original-message-id"]);
        Assert.True(DateTimeOffset.TryParse(diagnostico["failed-at"], out _));
        Assert.Empty(repo.Agregados);
    }

    [Theory]
    [InlineData("\"price\": 14990", "\"price\": 0", "validacion: El precio no puede ser cero ni negativo.", "PriceValueException")]
    [InlineData("\"name\": \"Polera basica negra\"", "\"name\": \"Polera bebe\"", "politica", "ErrorDefinitivoException")]
    [InlineData("\"name\": \"Polera basica negra\"", "\"name\": \"\"", "validacion:", "ArgumentException")]
    public async Task Oferta_que_no_pasa_la_validacion_del_catalogo_va_a_la_dlq(
        string original, string reemplazo, string razon, string excepcion)
    {
        var (consumidor, cola, repo) = Armar();

        await consumidor.ManejarAsync(Mensaje(OfertaValida.Replace(original, reemplazo)), CancellationToken.None);

        var (_, diagnostico) = Assert.Single(cola.EnDlq);
        Assert.StartsWith(razon, diagnostico["error-reason"]);
        Assert.Equal(excepcion, diagnostico["error-exception"]);
        Assert.Empty(repo.Agregados);
    }

    // ── Error recuperable: ni se borra ni va a la DLQ ────────────────────

    public static TheoryData<Exception> FallasDeLaBase =>
    [
        new TimeoutException("la base no respondio"),
        // Un timeout de Npgsql/HttpClient llega como TaskCanceledException, que
        // hereda de OperationCanceledException: tambien es recuperable.
        new TaskCanceledException("timeout de la consulta"),
        new InvalidOperationException("conexion cerrada")
    ];

    [Theory]
    [MemberData(nameof(FallasDeLaBase))]
    public async Task Si_la_base_falla_el_mensaje_queda_en_la_cola_para_reintentar(Exception falla)
    {
        var (consumidor, cola, repo) = Armar();
        repo.FallarAlGuardar = falla;

        await consumidor.ManejarAsync(Mensaje(OfertaValida), CancellationToken.None);

        Assert.Empty(cola.Borrados);
        Assert.Empty(cola.EnDlq);
    }

    // ── El ultimo intento lo aparta el consumidor, con diagnostico ───────

    [Fact]
    public async Task En_el_ultimo_intento_va_a_la_dlq_con_diagnostico_recuperable_agotado()
    {
        var (consumidor, cola, repo) = Armar();
        repo.FallarAlGuardar = new TimeoutException("la base no respondio");

        await consumidor.ManejarAsync(Mensaje(OfertaValida, intento: 5), CancellationToken.None);

        var (id, diagnostico) = Assert.Single(cola.EnDlq);
        Assert.Equal("m-1", id);
        Assert.Equal("recuperable-agotado", diagnostico["error-type"]);
        Assert.Equal("5 intentos sin exito; el ultimo: la base no respondio", diagnostico["error-reason"]);
        Assert.Equal("TimeoutException", diagnostico["error-exception"]);
        Assert.Equal("m-1", diagnostico["original-message-id"]);
    }

    [Fact]
    public async Task Antes_del_ultimo_intento_sigue_reintentando()
    {
        var (consumidor, cola, repo) = Armar();
        repo.FallarAlGuardar = new TimeoutException("la base no respondio");

        await consumidor.ManejarAsync(Mensaje(OfertaValida, intento: 4), CancellationToken.None);

        Assert.Empty(cola.EnDlq);
        Assert.Empty(cola.Borrados);
    }

    [Fact]
    public async Task Si_no_se_sabe_el_maximo_lo_deja_a_sqs()
    {
        var (consumidor, cola, repo) = Armar();
        cola.MaxIntentos = null;     // cola sin DLQ, o RedrivePolicy ilegible
        repo.FallarAlGuardar = new TimeoutException("la base no respondio");

        await consumidor.ManejarAsync(Mensaje(OfertaValida, intento: 9), CancellationToken.None);

        Assert.Empty(cola.EnDlq);
        Assert.Empty(cola.Borrados);
    }

    // ── Duplicados ───────────────────────────────────────────────────────

    [Fact]
    public async Task El_mismo_mensaje_dos_veces_no_duplica_la_oferta()
    {
        var (consumidor, cola, repo) = Armar();

        await consumidor.ManejarAsync(Mensaje(OfertaValida), CancellationToken.None);
        await consumidor.ManejarAsync(Mensaje(OfertaValida, "m-2"), CancellationToken.None);

        Assert.Single(repo.Agregados);              // la segunda vez lo encuentra y lo actualiza
        Assert.Equal(["m-1", "m-2"], cola.Borrados);
    }

    // =====================================================================

    private static MensajeCola Mensaje(string cuerpo, string id = "m-1", int intento = 1) =>
        new(id, cuerpo, "recibo-" + id, intento);

    private static (ConsumidorOfertas, ColaFalsa, RepositorioMemoria) Armar()
    {
        var repo = new RepositorioMemoria();
        var servicios = new ServiceCollection()
            .AddLogging()
            .AddSingleton<IProductRepository>(repo)
            .AddSingleton<IPublicadorEventos, PublicadorNulo>()
            .AddScoped<ProductService>()
            .AddScoped<ProcesadorOfertaCapturada>()
            .BuildServiceProvider();

        var cola = new ColaFalsa();
        var consumidor = new ConsumidorOfertas(cola, servicios.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<ConsumidorOfertas>.Instance);
        return (consumidor, cola, repo);
    }

    private sealed class ColaFalsa : IColaMensajes
    {
        public List<string> Borrados { get; } = [];
        public List<(string Id, IReadOnlyDictionary<string, string> Diagnostico)> EnDlq { get; } = [];
        public bool Habilitada => true;
        public int? MaxIntentos { get; set; } = 5;

        public Task<int?> MaxIntentosAsync(CancellationToken ct) => Task.FromResult(MaxIntentos);

        public Task<IReadOnlyList<MensajeCola>> RecibirAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<MensajeCola>>([]);

        public Task BorrarAsync(MensajeCola mensaje, CancellationToken ct)
        {
            Borrados.Add(mensaje.MessageId);
            return Task.CompletedTask;
        }

        public Task EnviarADlqAsync(MensajeCola mensaje, IReadOnlyDictionary<string, string> diagnostico, CancellationToken ct)
        {
            EnDlq.Add((mensaje.MessageId, diagnostico));
            return Task.CompletedTask;
        }
    }

    /// <summary>Guarda en una lista y encuentra por tienda + SKU, como los UNIQUE de la base.</summary>
    private sealed class RepositorioMemoria : IProductRepository
    {
        public List<ProductEntity> Agregados { get; } = [];
        public Exception? FallarAlGuardar { get; set; }

        public Task<ProductEntity?> GetProductByOfferAsync(string store, string externalId) =>
            Task.FromResult(Agregados.FirstOrDefault(p => p.Offers.Any(o =>
                string.Equals(o.Store, store, StringComparison.OrdinalIgnoreCase) && o.ExternalId == externalId)));

        public Task<ProductEntity?> GetProductByCanonicalKeyAsync(string canonicalKey) =>
            Task.FromResult(Agregados.FirstOrDefault(p => p.CanonicalKey == canonicalKey));

        public Task<bool> SlugExistsAsync(string slug) => Task.FromResult(Agregados.Any(p => p.Slug == slug));

        public Task<ProductEntity> AddProductAsync(ProductEntity product)
        {
            // Un producto nuevo se guarda aca (el repositorio real hace SaveChanges adentro).
            if (FallarAlGuardar is not null) return Task.FromException<ProductEntity>(FallarAlGuardar);
            product.ProductId = Agregados.Count + 1;
            Agregados.Add(product);
            return Task.FromResult(product);
        }

        public Task SaveChangesAsync() => FallarAlGuardar is null ? Task.CompletedTask : Task.FromException(FallarAlGuardar);

        public Task<IReadOnlyList<ProductEntity>> GetAllProductsAsync() => throw new NotImplementedException();
        public Task<ProductEntity?> GetProductByIdAsync(int id) => throw new NotImplementedException();
        public Task<ProductEntity?> GetProductBySlugAsync(string slug) => throw new NotImplementedException();
        public Task<bool> DeleteProductByIdAsync(int id) => throw new NotImplementedException();
        public Task<bool> IncrementVisitsAsync(int id) => throw new NotImplementedException();
        public Task<IReadOnlyList<ProductEntity>> GetProductsByCategoryAsync(string category) => throw new NotImplementedException();
        public Task<IReadOnlyList<ProductEntity>> GetProductsByPriceAsync(int price) => throw new NotImplementedException();
        public Task<IReadOnlyList<ProductEntity>> GetProductsBySizeAsync(string size) => throw new NotImplementedException();
    }

    private sealed class PublicadorNulo : IPublicadorEventos
    {
        public bool Habilitado => false;
        public Task PublicarCambioOfertaAsync(string tipo, DatosCambioOferta datos, CancellationToken ct = default) =>
            Task.CompletedTask;
    }
}
