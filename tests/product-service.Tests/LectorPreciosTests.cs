using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Product_Service.Streaming;

namespace Product_Service.Tests;

/// <summary>
/// El lector de cep-precios (Kinesis) y cómo resuelve cada registro: éxito
/// (procesado), error definitivo (a la DLQ con diagnóstico) y error recuperable
/// (reintenta y, si no cede, a la DLQ como "recuperable-agotado"). Usa un
/// procesador y una DLQ falsos, sin Kinesis ni base.
/// </summary>
public class LectorPreciosTests
{
    private static RegistroStream Registro(string cuerpo = "{}", string seq = "49-1") =>
        new("shardId-000000000000", seq, "paris#SKU-1", cuerpo);

    [Fact]
    public async Task Un_precio_valido_se_procesa_y_no_va_a_la_dlq()
    {
        var (lector, proc, dlq) = Armar();

        await lector.ManejarAsync(Registro(), CancellationToken.None);

        Assert.Equal(1, proc.Llamadas);
        Assert.Empty(dlq.Enviados);
    }

    [Fact]
    public async Task Un_registro_definitivo_va_a_la_dlq_con_diagnostico_y_sin_reintentar()
    {
        var (lector, proc, dlq) = Armar();
        proc.Lanzar = new Product_Service.Exceptions.ErrorDefinitivoException("precio inválido: -1");

        await lector.ManejarAsync(Registro("""{"roto":true}"""), CancellationToken.None);

        var (cuerpo, diag) = Assert.Single(dlq.Enviados);
        Assert.Equal("""{"roto":true}""", cuerpo);
        Assert.Equal("definitivo", diag["error-type"]);
        Assert.Contains("precio inválido", diag["error-reason"]);
        Assert.StartsWith("product-service", diag["failed-by"]);
        Assert.Equal(1, proc.Llamadas); // no reintenta un definitivo
    }

    [Fact]
    public async Task Un_error_recuperable_se_reintenta_y_termina_en_la_dlq_agotado()
    {
        var (lector, proc, dlq) = Armar();
        proc.Lanzar = new TimeoutException("la base no respondió");

        await lector.ManejarAsync(Registro(), CancellationToken.None);

        // 1 intento + 3 reintentos (EsperasReintento) = 4 llamadas, y luego la DLQ.
        Assert.Equal(4, proc.Llamadas);
        var (_, diag) = Assert.Single(dlq.Enviados);
        Assert.Equal("recuperable-agotado", diag["error-type"]);
        Assert.Contains("la base no respondió", diag["error-reason"]);
        Assert.Equal("TimeoutException", diag["error-exception"]);
    }

    [Fact]
    public async Task El_diagnostico_recuperable_usa_la_causa_de_fondo()
    {
        var (lector, proc, dlq) = Armar();
        proc.Lanzar = new InvalidOperationException(
            "An exception has been raised that is likely due to a transient failure.",
            new System.Net.Sockets.SocketException(111));

        await lector.ManejarAsync(Registro(), CancellationToken.None);

        var (_, diag) = Assert.Single(dlq.Enviados);
        Assert.Equal("SocketException", diag["error-exception"]);
        Assert.DoesNotContain("transient failure", diag["error-reason"]);
    }

    // =====================================================================

    private static (LectorPrecios, ProcesadorFalso, DlqFalsa) Armar()
    {
        var proc = new ProcesadorFalso();
        var servicios = new ServiceCollection()
            .AddLogging()
            .AddScoped<IProcesadorPrecio>(_ => proc)
            .BuildServiceProvider();

        var dlq = new DlqFalsa();
        var lector = new LectorPrecios(
            new LectorInactivo(),
            dlq,
            servicios.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<LectorPrecios>.Instance)
        {
            // No dormir de verdad entre reintentos.
            Dormir = (_, _) => Task.CompletedTask
        };
        return (lector, proc, dlq);
    }

    private sealed class ProcesadorFalso : IProcesadorPrecio
    {
        public int Llamadas { get; private set; }
        public Exception? Lanzar { get; set; }

        public Task<string> ProcesarAsync(string cuerpo, CancellationToken ct)
        {
            Llamadas++;
            return Lanzar is null ? Task.FromResult("evt-1") : Task.FromException<string>(Lanzar);
        }
    }

    private sealed class DlqFalsa : IBuzonErrores
    {
        public List<(string Cuerpo, IReadOnlyDictionary<string, string> Diagnostico)> Enviados { get; } = [];
        public bool Habilitado => true;

        public Task EnviarAsync(string cuerpo, IReadOnlyDictionary<string, string> diagnostico, CancellationToken ct)
        {
            Enviados.Add((cuerpo, diagnostico));
            return Task.CompletedTask;
        }
    }

    private sealed class LectorInactivo : ILectorStream
    {
        public bool Habilitada => false;
        public Task<IReadOnlyList<string>> ShardsAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<string>>([]);
        public Task<IReadOnlyList<RegistroStream>> LeerAsync(string shardId, string? checkpoint, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<RegistroStream>>([]);
    }
}
