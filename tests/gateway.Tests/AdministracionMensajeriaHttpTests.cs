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
using CachaElPrecio.Gateway.Mensajeria;

namespace CachaElPrecio.Gateway.Tests;

/// <summary>
/// El «Listo cuando» de la T11, por HTTP y contra el gateway completo: el
/// pipeline real de autenticación y autorización, las rutas
/// /api/admin/mensajeria y la mensajería en memoria. El token lo firma el test
/// con una clave propia en vez de Cognito; el resto es el mismo código que corre
/// en la EC2.
///
///   · sin token → 401; con token pero sin el grupo admin → 403
///   · mirar colas, tópicos, mensajes y la topología; redrive de una DLQ
///   · desde el 17-11, crear, cambiar o borrar colas, tópicos y reglas ya no
///     existe (404/405): eso es de Terraform
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
            {
                services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, o =>
                {
                    // Las claves de firma salen de acá y no del JWKS de Cognito.
                    var configuracion = new OpenIdConnectConfiguration { Issuer = Emisor };
                    configuracion.SigningKeys.Add(Clave);
                    o.Configuration = configuracion;
                    o.TokenValidationParameters.IssuerSigningKey = Clave;
                });
                // notification-service no corre en los tests: responde uno falso.
                services.AddHttpClient<BandejaCorreosProxy>()
                    .ConfigurePrimaryHttpMessageHandler(() => new NotificacionesFalso());
                services.AddHttpClient<SimuladorAvisos>()
                    .ConfigurePrimaryHttpMessageHandler(() => new ProductosFalso());
                services.AddHttpClient<AuditoriaProxy>()
                    .ConfigurePrimaryHttpMessageHandler(() => new NotificacionesFalso());
                foreach (var nombre in new[] { TrazasProxy.Scraper, TrazasProxy.Productos })
                    services.AddHttpClient(nombre).ConfigurePrimaryHttpMessageHandler(() => new TrazasFalso());
                // notification-service "apagado": su parte debe venir en «faltan».
                services.AddHttpClient(TrazasProxy.Notificaciones)
                    .ConfigurePrimaryHttpMessageHandler(() => new NotificacionesFalso());
            });
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

        var redrive = await usuario.PostAsync("/api/admin/mensajeria/colas/cep-ofertas-crudas-dlq/redrive", null);
        Assert.Equal(HttpStatusCode.Forbidden, redrive.StatusCode);
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

    // ── El panel no toca la infraestructura (17-11) ──────────────────────
    // Colas, tópicos y reglas son de Terraform. Si estas rutas volvieran a
    // existir, el panel podría crear o borrar recursos por fuera de Terraform.

    public static TheoryData<string, string> RutasQueYaNoExisten => new()
    {
        { "POST", "/api/admin/mensajeria/colas" },
        { "DELETE", "/api/admin/mensajeria/colas/cep-ofertas-crudas" },
        { "POST", "/api/admin/mensajeria/topicos" },
        { "DELETE", "/api/admin/mensajeria/topicos/cep-cambios-oferta" },
        { "POST", "/api/admin/mensajeria/suscripciones" },
        { "PUT", "/api/admin/mensajeria/suscripciones/cep-cambios-oferta/cep-notificaciones" },
        { "DELETE", "/api/admin/mensajeria/suscripciones/cep-cambios-oferta/cep-notificaciones" },
        { "POST", "/api/admin/mensajeria/topologia/aplicar" },
    };

    [Theory]
    [MemberData(nameof(RutasQueYaNoExisten))]
    public async Task El_panel_no_crea_cambia_ni_borra_infraestructura(string metodo, string ruta)
    {
        int colasAntes = (await Json(await admin.GetAsync("/api/admin/mensajeria/colas"), HttpStatusCode.OK)).GetArrayLength();

        using var pedido = new HttpRequestMessage(new HttpMethod(metodo), ruta)
        {
            Content = JsonContent.Create(new { nombre = "cep-prueba", topico = "cep-cambios-oferta", cola = "cep-prueba" })
        };
        var r = await admin.SendAsync(pedido);

        // 404 si la ruta no existe con ningún método; 405 si existe solo con GET.
        Assert.Contains(r.StatusCode, new[] { HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed });
        Assert.Equal(colasAntes, (await Json(await admin.GetAsync("/api/admin/mensajeria/colas"), HttpStatusCode.OK)).GetArrayLength());
    }

    [Fact]
    public async Task Redrive_devuelve_los_mensajes_de_la_dlq_a_su_cola()
    {
        var r = await Json(await admin.PostAsync("/api/admin/mensajeria/colas/cep-ofertas-crudas-dlq/redrive", null), HttpStatusCode.OK);
        Assert.Equal("cep-ofertas-crudas", r.GetProperty("destino").GetString());
        Assert.Equal(2, r.GetProperty("mensajesMovidos").GetInt32());
    }

    [Fact]
    public async Task La_topologia_se_puede_comparar() =>
        Assert.True((await Json(await admin.GetAsync("/api/admin/mensajeria/topologia"), HttpStatusCode.OK))
            .GetProperty("sincronizada").GetBoolean());

    // ── Mirar mensajes ───────────────────────────────────────────────────

    [Fact]
    public async Task Mirar_una_cola_con_dlq_responde_409_porque_sumaria_intentos() =>
        await Esperar(admin.GetAsync("/api/admin/mensajeria/colas/cep-ofertas-crudas/mensajes"), HttpStatusCode.Conflict);

    [Fact]
    // Desde el 28-11 la auditoría tiene lector y DLQ: mirarla contaría como una
    // entrega y podría mandar mensajes sanos a la DLQ, así que el panel lo frena.
    public async Task La_auditoria_ya_no_se_puede_mirar_porque_tiene_lector_y_dlq() =>
        await Esperar(admin.GetAsync("/api/admin/mensajeria/colas/cep-auditoria-precios/mensajes"), HttpStatusCode.Conflict);

    [Fact]
    public async Task Una_cola_que_no_existe_responde_404() =>
        await Esperar(admin.GetAsync("/api/admin/mensajeria/colas/cep-fantasma"), HttpStatusCode.NotFound);

    // ── Bandeja de correos (proxy a notification-service) ────────────────

    [Fact]
    public async Task La_bandeja_tambien_exige_el_grupo_admin()
    {
        using var usuario = Cliente(Token(grupos: ["usuario"]));
        Assert.Equal(HttpStatusCode.Forbidden, (await usuario.GetAsync("/api/admin/mensajeria/correos")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await usuario.GetAsync("/api/admin/mensajeria/correos/7")).StatusCode);
    }

    [Fact]
    public async Task La_bandeja_pasa_lo_que_responde_notification_service()
    {
        var lista = await Json(await admin.GetAsync("/api/admin/mensajeria/correos?limite=5"), HttpStatusCode.OK);
        Assert.Equal("internal/correos?limite=5", lista[0].GetProperty("pedido").GetString());

        var ejemplo = await Json(await admin.GetAsync("/api/admin/mensajeria/correos/ejemplo/precio.bajo"), HttpStatusCode.OK);
        Assert.Equal("internal/correos/ejemplo/precio.bajo", ejemplo.GetProperty("pedido").GetString());

        await Esperar(admin.GetAsync("/api/admin/mensajeria/correos/404"), HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Si_notification_service_no_responde_la_bandeja_da_502() =>
        await Esperar(admin.GetAsync("/api/admin/mensajeria/correos/999"), HttpStatusCode.BadGateway);

    // ── Simular un aviso (publica en el tópico) ──────────────────────────

    [Fact]
    public async Task Simular_un_aviso_exige_el_grupo_admin()
    {
        using var usuario = Cliente(Token(grupos: ["usuario"]));
        var r = await usuario.PostAsJsonAsync("/api/admin/mensajeria/simular-aviso",
            new { productId = 1, tipo = "precio.bajo", descuento = 30 });
        Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode);
    }

    [Fact]
    public async Task Simular_una_baja_publica_el_sobre_y_los_atributos_del_filtro()
    {
        var r = await Json(await admin.PostAsJsonAsync("/api/admin/mensajeria/simular-aviso",
            new { productId = 1, tipo = "precio.bajo", descuento = 30 }), HttpStatusCode.Accepted);

        var memoria = fabrica.Services.GetRequiredService<AdministradorMensajeriaMemoria>();
        var (topico, mensaje, atributos) = Assert.Single(memoria.Publicados);
        Assert.Equal("cep-cambios-oferta", topico);
        Assert.Contains(atributos, a => a is { Nombre: "type", Valor: "precio.bajo" });
        Assert.Contains(atributos, a => a is { Nombre: "discountPct", Tipo: "Number", Valor: "30" });

        var sobre = JsonDocument.Parse(mensaje).RootElement;
        Assert.Equal(r.GetProperty("eventId").GetString(), sobre.GetProperty("eventId").GetString());
        Assert.Equal("panel-admin", sobre.GetProperty("source").GetString());
        var datos = sobre.GetProperty("data");
        Assert.Equal(19990, datos.GetProperty("previousPrice").GetInt32());
        Assert.Equal(13990, datos.GetProperty("newPrice").GetInt32());
        Assert.Equal("falabella", datos.GetProperty("store").GetString());
    }

    [Fact]
    public async Task Simular_un_agotado_no_manda_descuento()
    {
        await Esperar(admin.PostAsJsonAsync("/api/admin/mensajeria/simular-aviso",
            new { productId = 1, tipo = "oferta.agotada" }), HttpStatusCode.Accepted);

        var memoria = fabrica.Services.GetRequiredService<AdministradorMensajeriaMemoria>();
        var (_, mensaje, atributos) = Assert.Single(memoria.Publicados);
        Assert.DoesNotContain(atributos, a => a.Nombre == "discountPct");
        Assert.Equal(19990, JsonDocument.Parse(mensaje).RootElement.GetProperty("data").GetProperty("lastPrice").GetInt32());
    }

    [Fact]
    public async Task Simular_con_datos_invalidos_responde_400_por_campo()
    {
        var r = await Json(await admin.PostAsJsonAsync("/api/admin/mensajeria/simular-aviso",
            new { productId = 0, tipo = "precio.bajo", descuento = 0 }), HttpStatusCode.BadRequest);
        var campos = r.GetProperty("errores").EnumerateArray().Select(e => e.GetProperty("campo").GetString()).ToList();
        Assert.Contains("productId", campos);
        Assert.Contains("descuento", campos);

        var tipo = await Json(await admin.PostAsJsonAsync("/api/admin/mensajeria/simular-aviso",
            new { productId = 1, tipo = "oferta.capturada" }), HttpStatusCode.BadRequest);
        Assert.Equal("tipo", tipo.GetProperty("errores")[0].GetProperty("campo").GetString());
    }

    [Fact]
    public async Task Simular_con_un_producto_que_no_existe_responde_404() =>
        await Esperar(admin.PostAsJsonAsync("/api/admin/mensajeria/simular-aviso",
            new { productId = 999, tipo = "oferta.repuesta" }), HttpStatusCode.NotFound);

    // ── La auditoría (cep-auditoria-precios, en product-service) ────────

    [Fact]
    public async Task La_auditoria_pasa_el_filtro_de_sospechosos_a_product_service()
    {
        var r = await admin.GetAsync("/api/admin/mensajeria/auditoria?limite=5&soloSospechosos=true");

        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        using var json = JsonDocument.Parse(await r.Content.ReadAsStringAsync());
        Assert.Equal("internal/auditoria?limite=5&soloSospechosos=true", json.RootElement.GetProperty("pedido").GetString());
    }

    [Fact]
    public async Task La_auditoria_sin_token_responde_401()
    {
        using var anonimo = fabrica.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonimo.GetAsync("/api/admin/mensajeria/auditoria")).StatusCode);
    }

    // ── Seguir un barrido (trazas) ───────────────────────────────────────

    [Fact]
    public async Task La_linea_de_tiempo_junta_lo_que_sabe_cada_servicio()
    {
        var r = await admin.GetAsync("/api/admin/mensajeria/trazas/barrido-paris-1");
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);

        using var json = JsonDocument.Parse(await r.Content.ReadAsStringAsync());
        var raiz = json.RootElement;
        Assert.Equal("ok", raiz.GetProperty("scraper").GetProperty("estado").GetString());
        Assert.Equal("oferta-guardada", raiz.GetProperty("etapas")[0].GetProperty("etapa").GetString());
        Assert.Equal(1, raiz.GetProperty("auditoria").GetArrayLength());
        // NotificacionesFalso responde otra forma: el gateway no se cae, solo lo omite.
        Assert.Equal(JsonValueKind.Array, raiz.GetProperty("faltan").ValueKind);
    }

    [Fact]
    public async Task La_linea_de_tiempo_exige_el_grupo_admin()
    {
        using var usuario = Cliente(Token(grupos: ["usuario"]));
        Assert.Equal(HttpStatusCode.Forbidden, (await usuario.GetAsync("/api/admin/mensajeria/trazas")).StatusCode);
    }

    // scraper y product-service falsos para las trazas.
    private sealed class TrazasFalso : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            string ruta = request.RequestUri!.PathAndQuery.TrimStart('/');
            string json = ruta switch
            {
                _ when ruta.StartsWith("barridos/") => """{"correlationId":"barrido-paris-1","estado":"ok","guardados":3}""",
                _ when ruta.StartsWith("barridos") => """[{"correlationId":"barrido-paris-1","estado":"ok"}]""",
                _ when ruta.StartsWith("internal/auditoria") => """{"eventos":[{"eventId":"e-1","sospechoso":true}]}""",
                _ when ruta.StartsWith("internal/trazas/") => """[{"servicio":"product-service","etapa":"oferta-guardada","cantidad":3}]""",
                _ => "[]"
            };
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        }
    }

    // product-service falso: el producto 1 con una oferta de Falabella a $19.990.
    private sealed class ProductosFalso : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (!request.RequestUri!.AbsolutePath.EndsWith("/api/products/1"))
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            string json = JsonSerializer.Serialize(new
            {
                id = 1,
                slug = "polera-basica",
                name = "Polera básica",
                image = "https://img.test/p.jpg",
                offers = new[] { new { store = "falabella", price = 19990, url = "https://tienda.test/p", image = (string?)null, active = true } }
            });
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        }
    }

    // Responde lo que se le pidió, para comprobar la ruta; /404 da 404 y /999
    // hace como si el servicio estuviera apagado.
    private sealed class NotificacionesFalso : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            string pedido = request.RequestUri!.PathAndQuery.TrimStart('/');
            if (pedido.EndsWith("/999")) throw new HttpRequestException("Connection refused");

            var codigo = pedido.EndsWith("/404") ? HttpStatusCode.NotFound : HttpStatusCode.OK;
            string json = pedido.StartsWith("internal/correos?")
                ? JsonSerializer.Serialize(new[] { new { pedido } })
                : JsonSerializer.Serialize(new { pedido });
            return Task.FromResult(new HttpResponseMessage(codigo)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        }
    }

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
