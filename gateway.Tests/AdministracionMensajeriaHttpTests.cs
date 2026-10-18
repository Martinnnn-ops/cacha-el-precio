using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace CachaElPrecio.Gateway.Tests;

/// <summary>
/// El «Listo cuando» de la T11, por HTTP y contra el gateway completo: el
/// pipeline real de autenticación y autorización, las rutas
/// /api/admin/mensajeria y la mensajería en memoria. El token lo firma el test
/// con una clave propia en vez de Cognito; el resto es el mismo código que corre
/// en la EC2.
///
///   · sin token → 401; con token pero sin el grupo admin → 403
///   · casos válidos → 200/201/204
///   · casos inválidos → 400 con { campo, error }, o 409 si choca con lo que hay
///   · aplicar la topología dos veces no duplica nada
/// </summary>
public class AdministracionMensajeriaHttpTests : IDisposable
{
    private const string Emisor = "https://cognito.test/us-east-1_PRUEBA";
    private const string ClienteValido = "cliente-de-prueba";
    private static readonly SymmetricSecurityKey Clave =
        new(Encoding.UTF8.GetBytes("clave-solo-para-los-tests-del-gateway-0123456789"));

    private readonly WebApplicationFactory<Program> fabrica;
    private readonly HttpClient admin;

    public AdministracionMensajeriaHttpTests()
    {
        fabrica = new WebApplicationFactory<Program>().WithWebHostBuilder(web =>
        {
            web.UseSetting("COGNITO_ISSUER", Emisor);
            web.UseSetting("COGNITO_CLIENT_IDS_VALIDOS", ClienteValido);
            web.UseSetting("MENSAJERIA_MODO", "memoria");
            web.UseSetting("TOPOLOGIA_RUTA", RutaTopologia());
            web.ConfigureTestServices(services =>
                services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, o =>
                {
                    // Las claves de firma salen de acá y no del JWKS de Cognito.
                    var configuracion = new OpenIdConnectConfiguration { Issuer = Emisor };
                    configuracion.SigningKeys.Add(Clave);
                    o.Configuration = configuracion;
                    o.TokenValidationParameters.IssuerSigningKey = Clave;
                }));
        });
        admin = Cliente(Token(grupos: ["admin"]));
    }

    public void Dispose()
    {
        admin.Dispose();
        fabrica.Dispose();
        GC.SuppressFinalize(this);
    }

    // ── Quién puede entrar ───────────────────────────────────────────────

    [Fact]
    public async Task Sin_token_responde_401()
    {
        using var anonimo = fabrica.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonimo.GetAsync("/api/admin/mensajeria/colas")).StatusCode);
    }

    [Fact]
    public async Task Con_token_pero_sin_el_grupo_admin_responde_403()
    {
        using var usuario = Cliente(Token(grupos: ["usuario"]));
        var r = await usuario.GetAsync("/api/admin/mensajeria/colas");
        Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode);

        var crear = await usuario.PostAsJsonAsync("/api/admin/mensajeria/colas", new { nombre = "cep-intruso" });
        Assert.Equal(HttpStatusCode.Forbidden, crear.StatusCode);
    }

    // ── Consultar ────────────────────────────────────────────────────────

    [Fact]
    public async Task Consultar_colas_topicos_con_sus_filtros_y_una_dlq()
    {
        var colas = await Json(await admin.GetAsync("/api/admin/mensajeria/colas"), HttpStatusCode.OK);
        var crudas = colas.EnumerateArray().Single(c => c.GetProperty("nombre").GetString() == "cep-ofertas-crudas");
        Assert.Equal("cep-ofertas-crudas-dlq", crudas.GetProperty("dlq").GetProperty("cola").GetString());
        Assert.Equal(5, crudas.GetProperty("dlq").GetProperty("maxIntentos").GetInt32());

        var topicos = await Json(await admin.GetAsync("/api/admin/mensajeria/topicos"), HttpStatusCode.OK);
        var subs = topicos[0].GetProperty("suscripciones");
        Assert.Equal(2, subs.GetArrayLength());
        Assert.Contains(subs.EnumerateArray(), s => s.GetProperty("filtro").ValueKind == JsonValueKind.Object);

        // La semilla deja 2 fallos en la DLQ del flujo 1, con su diagnóstico.
        var mensajes = await Json(await admin.GetAsync("/api/admin/mensajeria/colas/cep-ofertas-crudas-dlq/mensajes"), HttpStatusCode.OK);
        Assert.Equal(2, mensajes.GetArrayLength());
        Assert.Equal("definitivo", mensajes[0].GetProperty("diagnostico").GetProperty("error-type").GetString());
    }

    // ── Gestionar (casos válidos) ────────────────────────────────────────

    [Fact]
    public async Task Crear_suscribir_con_filtro_y_borrar_todo()
    {
        await Esperar(admin.PostAsJsonAsync("/api/admin/mensajeria/colas",
            new { nombre = "cep-prueba-dlq" }), HttpStatusCode.Created);
        var cola = await Json(await admin.PostAsJsonAsync("/api/admin/mensajeria/colas",
            new { nombre = "cep-prueba", visibilidadSegundos = 45, dlq = "cep-prueba-dlq", maxIntentos = 3 }), HttpStatusCode.Created);
        Assert.Equal(45, cola.GetProperty("visibilidadSegundos").GetInt32());

        await Esperar(admin.PostAsJsonAsync("/api/admin/mensajeria/suscripciones", new
        {
            topico = "cep-cambios-oferta",
            cola = "cep-prueba",
            filtro = new { type = new[] { "oferta.agotada" } }
        }), HttpStatusCode.Created);

        await Esperar(admin.DeleteAsync("/api/admin/mensajeria/suscripciones/cep-cambios-oferta/cep-prueba"), HttpStatusCode.NoContent);
        await Esperar(admin.DeleteAsync("/api/admin/mensajeria/colas/cep-prueba"), HttpStatusCode.NoContent);
        await Esperar(admin.DeleteAsync("/api/admin/mensajeria/colas/cep-prueba-dlq"), HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Redrive_devuelve_los_mensajes_de_la_dlq_a_su_cola()
    {
        var r = await Json(await admin.PostAsync("/api/admin/mensajeria/colas/cep-ofertas-crudas-dlq/redrive", null), HttpStatusCode.OK);
        Assert.Equal("cep-ofertas-crudas", r.GetProperty("destino").GetString());
        Assert.Equal(2, r.GetProperty("mensajesMovidos").GetInt32());
    }

    [Fact]
    public async Task Aplicar_la_topologia_dos_veces_no_duplica_nada()
    {
        int colasAntes = (await Json(await admin.GetAsync("/api/admin/mensajeria/colas"), HttpStatusCode.OK)).GetArrayLength();

        var primera = await Json(await admin.PostAsync("/api/admin/mensajeria/topologia/aplicar", null), HttpStatusCode.OK);
        var segunda = await Json(await admin.PostAsync("/api/admin/mensajeria/topologia/aplicar", null), HttpStatusCode.OK);

        Assert.All(segunda.GetProperty("acciones").EnumerateArray(),
            a => Assert.Equal("sin cambios", a.GetProperty("resultado").GetString()));
        Assert.True(segunda.GetProperty("estado").GetProperty("sincronizada").GetBoolean());
        Assert.Equal(primera.GetProperty("acciones").GetArrayLength(), segunda.GetProperty("acciones").GetArrayLength());
        Assert.Equal(colasAntes, (await Json(await admin.GetAsync("/api/admin/mensajeria/colas"), HttpStatusCode.OK)).GetArrayLength());
    }

    // ── Validación: 400 con { campo, error } ─────────────────────────────

    public static TheoryData<string, object, string> SolicitudesInvalidas => new()
    {
        { "/api/admin/mensajeria/colas", new { nombre = "ofertas" }, "nombre" },                        // sin prefijo cep-
        { "/api/admin/mensajeria/colas", new { nombre = "cep-Ofertas_Crudas" }, "nombre" },             // mayúsculas y _
        { "/api/admin/mensajeria/colas", new { nombre = "cep-x", dlq = "cep-no-existe", maxIntentos = 3 }, "dlq" },
        { "/api/admin/mensajeria/colas", new { nombre = "cep-x", dlq = "cep-ofertas-crudas-dlq", maxIntentos = 11 }, "maxIntentos" },
        { "/api/admin/mensajeria/colas", new { nombre = "cep-x", visibilidadSegundos = 50000 }, "visibilidadSegundos" },
        { "/api/admin/mensajeria/colas", new { nombre = "cep-x", retencionSegundos = 30 }, "retencionSegundos" },
        { "/api/admin/mensajeria/topicos", new { nombre = "" }, "nombre" },
        { "/api/admin/mensajeria/suscripciones",
            new { topico = "cep-cambios-oferta", cola = "cep-auditoria-precios", filtro = new { tienda = new[] { "paris" } } }, "filtro" },
        { "/api/admin/mensajeria/suscripciones",
            new { topico = "cep-cambios-oferta", cola = "cep-auditoria-precios", filtro = new { type = new[] { "precio.subio" } } }, "filtro" },
        { "/api/admin/mensajeria/suscripciones", new { topico = "cep-no-existe", cola = "cep-auditoria-precios" }, "topico" },
    };

    [Theory]
    [MemberData(nameof(SolicitudesInvalidas))]
    public async Task Solicitud_invalida_responde_400_con_campo_y_error(string ruta, object cuerpo, string campo)
    {
        var r = await Json(await admin.PostAsJsonAsync(ruta, cuerpo), HttpStatusCode.BadRequest);

        var errores = r.GetProperty("errores").EnumerateArray().ToList();
        Assert.Contains(errores, e => e.GetProperty("campo").GetString() == campo);
        Assert.All(errores, e => Assert.False(string.IsNullOrWhiteSpace(e.GetProperty("error").GetString())));
    }

    // ── Conflictos: 409 ──────────────────────────────────────────────────

    [Fact]
    public async Task Crear_una_cola_que_ya_existe_responde_409() =>
        await Esperar(admin.PostAsJsonAsync("/api/admin/mensajeria/colas", new { nombre = "cep-ofertas-crudas" }), HttpStatusCode.Conflict);

    [Fact]
    public async Task Borrar_una_dlq_en_uso_responde_409_aunque_se_fuerce() =>
        await Esperar(admin.DeleteAsync("/api/admin/mensajeria/colas/cep-ofertas-crudas-dlq?forzar=true"), HttpStatusCode.Conflict);

    [Fact]
    public async Task Borrar_una_cola_con_suscripciones_responde_409() =>
        await Esperar(admin.DeleteAsync("/api/admin/mensajeria/colas/cep-auditoria-precios?forzar=true"), HttpStatusCode.Conflict);

    [Fact]
    public async Task Borrar_un_topico_con_suscripciones_responde_409() =>
        await Esperar(admin.DeleteAsync("/api/admin/mensajeria/topicos/cep-cambios-oferta?forzar=true"), HttpStatusCode.Conflict);

    [Fact]
    public async Task Suscribir_dos_veces_lo_mismo_responde_409() =>
        await Esperar(admin.PostAsJsonAsync("/api/admin/mensajeria/suscripciones",
            new { topico = "cep-cambios-oferta", cola = "cep-notificaciones" }), HttpStatusCode.Conflict);

    [Fact]
    public async Task Mirar_una_cola_con_dlq_responde_409_porque_sumaria_intentos() =>
        await Esperar(admin.GetAsync("/api/admin/mensajeria/colas/cep-ofertas-crudas/mensajes"), HttpStatusCode.Conflict);

    [Fact]
    public async Task La_auditoria_se_puede_mirar() =>
        await Esperar(admin.GetAsync("/api/admin/mensajeria/colas/cep-auditoria-precios/mensajes"), HttpStatusCode.OK);

    [Fact]
    public async Task Una_cola_que_no_existe_responde_404() =>
        await Esperar(admin.GetAsync("/api/admin/mensajeria/colas/cep-fantasma"), HttpStatusCode.NotFound);

    // =====================================================================

    private HttpClient Cliente(string token)
    {
        var cliente = fabrica.CreateClient();
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return cliente;
    }

    // Un access_token con la forma de los de Cognito: token_use, client_id y cognito:groups.
    private static string Token(string[] grupos) => new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
    {
        Issuer = Emisor,
        Expires = DateTime.UtcNow.AddMinutes(10),
        Claims = new Dictionary<string, object>
        {
            ["sub"] = "usuario-de-prueba",
            ["token_use"] = "access",
            ["client_id"] = ClienteValido,
            ["cognito:groups"] = grupos
        },
        SigningCredentials = new SigningCredentials(Clave, SecurityAlgorithms.HmacSha256)
    });

    private static async Task<JsonElement> Json(HttpResponseMessage r, HttpStatusCode esperado)
    {
        string cuerpo = await r.Content.ReadAsStringAsync();
        Assert.True(r.StatusCode == esperado, $"Se esperaba {(int)esperado} y llegó {(int)r.StatusCode}: {cuerpo}");
        return JsonDocument.Parse(cuerpo).RootElement.Clone();
    }

    private static async Task Esperar(Task<HttpResponseMessage> llamada, HttpStatusCode esperado)
    {
        var r = await llamada;
        Assert.True(r.StatusCode == esperado,
            $"Se esperaba {(int)esperado} y llegó {(int)r.StatusCode}: {await r.Content.ReadAsStringAsync()}");
    }

    private static string RutaTopologia()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "infra", "mensajeria", "topologia.json")))
            dir = dir.Parent;
        return Path.Combine(dir!.FullName, "infra", "mensajeria", "topologia.json");
    }
}
