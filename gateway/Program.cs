using System.Security.Claims;
using CachaElPrecio.Gateway.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

string productUrl = builder.Configuration["PRODUCT_URL"] ?? "http://localhost:8081";
string? cognitoIssuer = builder.Configuration["COGNITO_ISSUER"];
string writeScope = builder.Configuration["COGNITO_WRITE_SCOPE"]
    ?? "https://api.cachaelprecio.cl/ingesta";
var validClientIds = (builder.Configuration["COGNITO_CLIENT_IDS_VALIDOS"] ?? string.Empty)
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    .ToHashSet(StringComparer.Ordinal);

builder.Services.AddHttpClient<ProductServiceProxy>(client =>
{
    client.BaseAddress = new Uri(productUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddSingleton<FollowRepository>();
builder.Services.AddHealthChecks();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        if (!string.IsNullOrWhiteSpace(cognitoIssuer))
        {
            options.Authority = cognitoIssuer;
        }

        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateAudience = false,
            ValidateIssuer = !string.IsNullOrWhiteSpace(cognitoIssuer),
            ValidIssuer = cognitoIssuer,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            NameClaimType = "sub",
            RoleClaimType = "cognito:groups"
        };

        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = context =>
            {
                string? tokenUse = context.Principal?.FindFirstValue("token_use");
                string? clientId = context.Principal?.FindFirstValue("client_id");

                if (tokenUse != "access")
                {
                    context.Fail("El token no es un access_token.");
                }
                else if (clientId is null || !validClientIds.Contains(clientId))
                {
                    context.Fail("El client_id no está autorizado.");
                }

                return Task.CompletedTask;
            },
            OnChallenge = async context =>
            {
                context.HandleResponse();
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.Headers.WWWAuthenticate = "Bearer realm=\"cacha-el-precio\"";
                await context.Response.WriteAsJsonAsync(new
                {
                    estado = 401,
                    error = "No autorizado",
                    mensaje = "Falta el token de acceso, o no es válido.",
                    ruta = context.Request.Path.Value
                });
            },
            OnForbidden = context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return context.Response.WriteAsJsonAsync(new
                {
                    estado = 403,
                    error = "Prohibido",
                    mensaje = "Tu token es válido, pero no tienes el permiso que esta ruta exige.",
                    ruta = context.Request.Path.Value
                });
            }
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("product-write", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireAssertion(context => HasScope(context.User, writeScope));
    });
});

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health").AllowAnonymous();

MapProductRoutes(app);
MapIdentityRoutes(app);
MapFollowRoutes(app);

app.Run();

static void MapProductRoutes(WebApplication app)
{
    app.MapGet("/productos", ProxyTo("/api/products")).AllowAnonymous();
    app.MapGet("/productos/{id:long}", ProxyToDynamic(context => $"/api/products/{context.Request.RouteValues["id"]}"))
        .AllowAnonymous();
    app.MapPost("/productos", ProxyTo("/api/products")).RequireAuthorization("product-write");
    app.MapPut("/productos/{id:long}", ProxyToDynamic(context => $"/api/products/{context.Request.RouteValues["id"]}"))
        .RequireAuthorization("product-write");
    app.MapDelete("/productos/{id:long}", ProxyToDynamic(context => $"/api/products/{context.Request.RouteValues["id"]}"))
        .RequireAuthorization("product-write");

    // Sumar una visita es ANÓNIMO a propósito: el contador vale para todos, con
    // o sin sesión. Pedir login para contar una vista sería cobrar por algo que
    // no se le da a nadie, y dejaría «Lo más visto» midiendo solo a quien entra.
    //
    // Es la única escritura pública del sistema, y puede serlo porque no
    // escribe nada que el usuario controle: solo suma uno a un contador. No
    // acepta cuerpo, no devuelve datos y no puede alterar un precio ni un
    // nombre. El resto de /productos sigue exigiendo el scope de escritura.
    app.MapPost("/productos/{id:long}/visitas",
        ProxyToDynamic(context => $"/api/products/{context.Request.RouteValues["id"]}/visits"))
        .AllowAnonymous();

    // Las categorías salen de los productos que hay de verdad, no de una lista
    // escrita a mano. Hasta el 09-09 esto devolvía seis categorías fijas con
    // ids del 1 al 6 que no correspondían a ningún campo del producto: era
    // contrato público, y mentía.
    //
    // Es también la primera transformación real del BFF. Hasta ahora sus rutas
    // eran passthrough puro —cambiaban el nombre de la URL y nada más—, que es
    // el punto que el ADR-021 dejó abierto: aislaban el nombre, no los datos.
    app.MapGet("/catalogos", async (ProductServiceProxy proxy, CancellationToken ct) =>
    {
        IReadOnlyList<CategorySummary>? categorias = await proxy.GetCategoriesAsync(ct);

        return categorias is null
            ? Results.Json(new
            {
                estado = 502,
                error = "Servicio no disponible",
                mensaje = "Product Service no respondió."
            }, statusCode: StatusCodes.Status502BadGateway)
            : Results.Ok(categorias);
    }).AllowAnonymous();

    app.MapGet("/api/products", ProxyTo("/api/products")).AllowAnonymous();
    app.MapGet("/api/products/{id:int}", ProxyToDynamic(context => $"/api/products/{context.Request.RouteValues["id"]}"))
        .AllowAnonymous();
    app.MapGet("/api/products/by-category/{category}", ProxyToDynamic(context => $"/api/products/by-category/{Uri.EscapeDataString(context.Request.RouteValues["category"]?.ToString() ?? string.Empty)}"))
        .AllowAnonymous();
    app.MapGet("/api/products/by-price/{price:int}", ProxyToDynamic(context => $"/api/products/by-price/{context.Request.RouteValues["price"]}"))
        .AllowAnonymous();
    app.MapGet("/api/products/by-size/{size}", ProxyToDynamic(context => $"/api/products/by-size/{Uri.EscapeDataString(context.Request.RouteValues["size"]?.ToString() ?? string.Empty)}"))
        .AllowAnonymous();
    app.MapPost("/api/products", ProxyTo("/api/products")).RequireAuthorization("product-write");
    app.MapPut("/api/products/{id:int}", ProxyToDynamic(context => $"/api/products/{context.Request.RouteValues["id"]}"))
        .RequireAuthorization("product-write");
    app.MapDelete("/api/products/{id:int}", ProxyToDynamic(context => $"/api/products/{context.Request.RouteValues["id"]}"))
        .RequireAuthorization("product-write");
}

static void MapIdentityRoutes(WebApplication app)
{
    app.MapGet("/api/yo", (ClaimsPrincipal user) => Results.Ok(new
    {
        usuario = user.FindFirstValue("sub"),
        grupos = user.FindAll("cognito:groups").Select(claim => claim.Value),
        scope = user.FindFirstValue("scope"),
        clientId = user.FindFirstValue("client_id"),
        emisor = user.FindFirstValue("iss")
    })).RequireAuthorization();

    app.MapGet("/api/admin/diagnostico", () => Results.Ok(new
    {
        mensaje = "Si ves esto, tu token trae el grupo admin en cognito:groups",
        servicio = "gateway"
    })).RequireAuthorization(policy => policy.RequireRole("admin"));
}

static void MapFollowRoutes(WebApplication app)
{
    app.MapGet("/seguimiento", (ClaimsPrincipal user, FollowRepository repository) =>
    {
        IReadOnlyCollection<long> products = repository.List(user.FindFirstValue("sub")!);
        return Results.Ok(new { productos = products, total = products.Count });
    }).RequireAuthorization();

    app.MapPost("/seguimiento/{productId:long}", (long productId, ClaimsPrincipal user, FollowRepository repository) =>
    {
        bool added = repository.Add(user.FindFirstValue("sub")!, productId);
        return added
            ? Results.Created($"/seguimiento/{productId}", new { siguiendo = productId })
            : Results.Ok(new { siguiendo = productId, yaEstaba = true });
    }).RequireAuthorization();

    app.MapDelete("/seguimiento/{productId:long}", (long productId, ClaimsPrincipal user, FollowRepository repository) =>
        repository.Remove(user.FindFirstValue("sub")!, productId)
            ? Results.NoContent()
            : Results.NotFound()).RequireAuthorization();
}

static RequestDelegate ProxyTo(string path)
{
    return ProxyToDynamic(_ => path);
}

static RequestDelegate ProxyToDynamic(Func<HttpContext, string> pathFactory)
{
    return async context =>
    {
        var proxy = context.RequestServices.GetRequiredService<ProductServiceProxy>();
        await proxy.ForwardAsync(context, pathFactory(context), context.RequestAborted);
    };
}

static bool HasScope(ClaimsPrincipal user, string expectedScope)
{
    return user.FindAll("scope")
        .SelectMany(claim => claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        .Contains(expectedScope, StringComparer.Ordinal);
}

