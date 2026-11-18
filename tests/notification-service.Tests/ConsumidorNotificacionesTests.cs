using System.Net.Sockets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Notification_Service.Consumidor;
using Notification_Service.Correo;
using Notification_Service.Mensajeria;
using Notification_Service.Models.Entities;
using Notification_Service.Repository;
using Notification_Service.Seguidores;
using Notification_Service.Service;

namespace Notification_Service.Tests;

/// <summary>
/// El consumidor de cep-notificaciones y sus salidas: exito (se borra), error
/// definitivo (a la DLQ), error recuperable (no se toca: SQS lo reintenta) y el
/// ultimo intento recuperable (a la DLQ con "recuperable-agotado"). Usa el
/// ServicioNotificaciones real con cola, seguidores, directorio, repositorio y
/// enviador falsos, sin SQS, Cognito, Gmail ni base de datos.
/// </summary>
public class ConsumidorNotificacionesTests
{
    private const string PrecioBajo = """
        {
          "eventId": "7c1e2f3a-4b5d-4e6f-8a9b-0c1d2e3f4a5b",
          "type": "precio.bajo",
          "version": 1,
          "source": "product-service",
          "occurredAt": "2026-10-20T18:00:00Z",
          "data": {
            "productId": 42,
            "slug": "polera-basica-negra",
            "name": "Polera basica negra",
            "previousPrice": 19990,
            "newPrice": 14990,
            "discountPct": 25
          }
        }
        """;

    // ── Exito ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Aviso_valido_se_envia_y_el_mensaje_se_borra()
    {
        var p = Armar();

        await p.Consumidor.ManejarAsync(Mensaje(PrecioBajo), CancellationToken.None);

        var correo = Assert.Single(p.Enviador.Enviados);
        Assert.Equal("ana@correo.cl", correo.Para);
        Assert.Equal(["m-1"], p.Cola.Borrados);
        Assert.Empty(p.Cola.EnDlq);
    }

    // ── Bandeja del panel ────────────────────────────────────────────────

    [Fact]
    public async Task El_correo_enviado_queda_en_la_bandeja_con_su_modo()
    {
        var p = Armar();

        await p.Consumidor.ManejarAsync(Mensaje(PrecioBajo), CancellationToken.None);

        var copia = Assert.Single(p.Bandeja.Guardados);
        Assert.Equal("precio.bajo", copia.Tipo);
        Assert.Equal("memoria", copia.Modo);
        Assert.Equal("ana@correo.cl", copia.Correo.Para);
        Assert.Equal("Bajó 25% · Polera basica negra", copia.Correo.Asunto);
    }

    [Fact]
    public async Task Si_la_bandeja_falla_el_aviso_igual_se_da_por_enviado()
    {
        var p = Armar();
        p.Bandeja.Falla = new InvalidOperationException("tabla bloqueada");

        await p.Consumidor.ManejarAsync(Mensaje(PrecioBajo), CancellationToken.None);

        Assert.Single(p.Enviador.Enviados);
        Assert.Equal(["m-1"], p.Cola.Borrados);
        Assert.Empty(p.Cola.EnDlq);
    }

    [Fact]
    public async Task Un_aviso_repetido_no_duplica_la_copia()
    {
        var p = Armar();

        await p.Consumidor.ManejarAsync(Mensaje(PrecioBajo), CancellationToken.None);
        await p.Consumidor.ManejarAsync(Mensaje(PrecioBajo, id: "m-2"), CancellationToken.None);

        Assert.Single(p.Bandeja.Guardados);
    }

    // ── Error definitivo: a la DLQ con diagnostico, sin reintentos ──────

    [Theory]
    [InlineData("esto no es json", "JSON del sobre invalido")]
    [InlineData("""{"eventId":"7c1e2f3a-4b5d-4e6f-8a9b-0c1d2e3f4a5b","type":"oferta.capturada","version":1,"source":"x","occurredAt":"2026-10-20T18:00:00Z","data":{}}""", "type desconocido")]
    public async Task Sobre_invalido_va_a_la_dlq_como_definitivo(string cuerpo, string razon)
    {
        var p = Armar();

        await p.Consumidor.ManejarAsync(Mensaje(cuerpo), CancellationToken.None);

        var (id, diagnostico) = Assert.Single(p.Cola.EnDlq);
        Assert.Equal("m-1", id);
        Assert.Equal("definitivo", diagnostico["error-type"]);
        Assert.Contains(razon, diagnostico["error-reason"]);
        Assert.Equal("notification-service", diagnostico["failed-by"]);
        Assert.Empty(p.Enviador.Enviados);
    }

    // ── Error recuperable: ni se borra ni va a la DLQ ────────────────────

    [Fact]
    public async Task Antes_del_ultimo_intento_sigue_reintentando()
    {
        var p = Armar();
        p.Seguidores.Falla = new HttpRequestException("product-service no responde");

        await p.Consumidor.ManejarAsync(Mensaje(PrecioBajo, intento: 4), CancellationToken.None);

        Assert.Empty(p.Cola.EnDlq);
        Assert.Empty(p.Cola.Borrados);
    }

    // ── El ultimo intento lo aparta el consumidor, con diagnostico ───────

    [Fact]
    public async Task En_el_ultimo_intento_va_a_la_dlq_con_diagnostico_recuperable_agotado()
    {
        var p = Armar();
        p.Enviador.Falla = new TimeoutException("Gmail no respondio");

        await p.Consumidor.ManejarAsync(Mensaje(PrecioBajo, intento: 5), CancellationToken.None);

        var (id, diagnostico) = Assert.Single(p.Cola.EnDlq);
        Assert.Equal("m-1", id);
        Assert.Equal("recuperable-agotado", diagnostico["error-type"]);
        Assert.Equal("5 intentos sin exito; el ultimo: Gmail no respondio", diagnostico["error-reason"]);
        Assert.Equal("TimeoutException", diagnostico["error-exception"]);
        Assert.Equal("notification-service", diagnostico["failed-by"]);
        Assert.Equal("m-1", diagnostico["original-message-id"]);
        Assert.True(DateTimeOffset.TryParse(diagnostico["failed-at"], out _));
    }

    [Fact]
    public async Task El_diagnostico_usa_la_causa_de_fondo_y_no_el_envoltorio()
    {
        var p = Armar();
        // Asi llega un product-service apagado: HttpClient envuelve el error del socket.
        p.Seguidores.Falla = new HttpRequestException("Connection refused (product-service:8080)",
            new SocketException((int)SocketError.ConnectionRefused));

        await p.Consumidor.ManejarAsync(Mensaje(PrecioBajo, intento: 5), CancellationToken.None);

        var (_, diagnostico) = Assert.Single(p.Cola.EnDlq);
        Assert.Equal("SocketException", diagnostico["error-exception"]);
    }

    [Fact]
    public async Task Si_no_se_sabe_el_maximo_lo_deja_a_sqs()
    {
        var p = Armar();
        p.Cola.MaxIntentos = null;     // cola sin DLQ, o RedrivePolicy ilegible
        p.Seguidores.Falla = new HttpRequestException("product-service no responde");

        await p.Consumidor.ManejarAsync(Mensaje(PrecioBajo, intento: 9), CancellationToken.None);

        Assert.Empty(p.Cola.EnDlq);
        Assert.Empty(p.Cola.Borrados);
    }

    [Fact]
    public async Task Apagar_el_servicio_en_el_ultimo_intento_no_lo_manda_a_la_dlq()
    {
        var p = Armar();
        using var apagado = new CancellationTokenSource();
        apagado.Cancel();
        p.Seguidores.Falla = new OperationCanceledException(apagado.Token);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => p.Consumidor.ManejarAsync(Mensaje(PrecioBajo, intento: 5), apagado.Token));

        Assert.Empty(p.Cola.EnDlq);
        Assert.Empty(p.Cola.Borrados);
    }

    // =====================================================================

    private static MensajeCola Mensaje(string cuerpo, string id = "m-1", int intento = 1) =>
        new(id, cuerpo, "recibo-" + id, intento);

    private sealed record Piezas(
        ConsumidorNotificaciones Consumidor, ColaFalsa Cola, SeguidoresFalsos Seguidores, EnviadorMemoria Enviador,
        BandejaMemoria Bandeja);

    private static Piezas Armar()
    {
        var cola = new ColaFalsa();
        var seguidores = new SeguidoresFalsos();
        var enviador = new EnviadorMemoria();
        var bandeja = new BandejaMemoria();
        var servicios = new ServiceCollection()
            .AddLogging()
            .AddSingleton<IConfiguration>(new ConfigurationBuilder().Build())
            .AddSingleton<ISeguidoresClient>(seguidores)
            .AddSingleton<IDirectorioCorreos, DirectorioFalso>()
            .AddSingleton<INotificacionesRepository, RepositorioMemoria>()
            .AddSingleton<IBandejaCorreos>(bandeja)
            .AddSingleton<IEnviadorCorreo>(enviador)
            .AddScoped<ServicioNotificaciones>()
            .BuildServiceProvider();

        var consumidor = new ConsumidorNotificaciones(cola, servicios.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<ConsumidorNotificaciones>.Instance);
        return new Piezas(consumidor, cola, seguidores, enviador, bandeja);
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

    /// <summary>Una persona sigue la prenda, salvo que se le pida fallar.</summary>
    private sealed class SeguidoresFalsos : ISeguidoresClient
    {
        public Exception? Falla { get; set; }

        public Task<IReadOnlyList<string>> ObtenerSeguidoresAsync(int productId, CancellationToken ct) =>
            Falla is null
                ? Task.FromResult<IReadOnlyList<string>>(["sub-ana"])
                : Task.FromException<IReadOnlyList<string>>(Falla);
    }

    private sealed class DirectorioFalso : IDirectorioCorreos
    {
        public Task<string?> ObtenerCorreoAsync(string sub, CancellationToken ct) =>
            Task.FromResult<string?>("ana@correo.cl");
    }

    /// <summary>La marca (event_id, owner) en memoria, como el UNIQUE de la base.</summary>
    private sealed class RepositorioMemoria : INotificacionesRepository
    {
        private readonly HashSet<(Guid, string)> enviados = [];

        public Task<bool> YaEnviadoAsync(Guid eventId, string owner, CancellationToken ct) =>
            Task.FromResult(enviados.Contains((eventId, owner)));

        public Task MarcarEnviadoAsync(Guid eventId, string owner, string tipo, CancellationToken ct)
        {
            enviados.Add((eventId, owner));
            return Task.CompletedTask;
        }
    }

    /// <summary>El enviador de correo en memoria: guarda lo que "mando".</summary>
    private sealed class EnviadorMemoria : IEnviadorCorreo
    {
        public List<CorreoSalida> Enviados { get; } = [];
        public Exception? Falla { get; set; }
        public string Modo => "memoria";

        public Task EnviarAsync(CorreoSalida correo, CancellationToken ct)
        {
            if (Falla is not null) return Task.FromException(Falla);
            Enviados.Add(correo);
            return Task.CompletedTask;
        }
    }

    /// <summary>La bandeja del panel en memoria, con la opcion de fallar.</summary>
    private sealed class BandejaMemoria : IBandejaCorreos
    {
        public List<(string Tipo, string Modo, CorreoSalida Correo)> Guardados { get; } = [];
        public Exception? Falla { get; set; }

        public Task GuardarAsync(Guid eventId, string tipo, string modo, CorreoSalida correo, CancellationToken ct)
        {
            if (Falla is not null) return Task.FromException(Falla);
            Guardados.Add((tipo, modo, correo));
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<CorreoGenerado>> ListarAsync(int limite, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<CorreoGenerado>>([]);

        public Task<CorreoGenerado?> ObtenerAsync(long id, CancellationToken ct) =>
            Task.FromResult<CorreoGenerado?>(null);
    }
}
