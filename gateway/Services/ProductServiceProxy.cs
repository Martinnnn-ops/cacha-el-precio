using System.Text.Json;

namespace CachaElPrecio.Gateway.Services;

/// <summary>Una categoría del catálogo, con cuántos productos tiene.</summary>
public sealed record CategorySummary(string Nombre, int Total);

public sealed class ProductServiceProxy(HttpClient client)
{
    /// <summary>
    /// Categorías derivadas de los productos que existen de verdad. Devuelve
    /// null si Product Service no responde, para poder distinguir «no hay
    /// categorías» de «no pude preguntarlo»: lo primero es una lista vacía y
    /// lo segundo un 502.
    /// </summary>
    public async Task<IReadOnlyList<CategorySummary>?> GetCategoriesAsync(CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/products");
        request.Headers.TryAddWithoutValidation("Version", "1.0");

        try
        {
            using HttpResponseMessage response = await client.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            await using Stream cuerpo = await response.Content.ReadAsStreamAsync(cancellationToken);
            using JsonDocument documento = await JsonDocument.ParseAsync(cuerpo, cancellationToken: cancellationToken);

            if (documento.RootElement.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            // Se agrupa por el nombre tal como lo guarda el producto. Ordenado
            // alfabéticamente y no por cantidad: es una lista de navegación, y
            // una lista que se reordena sola cada vez que cambia el catálogo es
            // más difícil de recorrer que una estable.
            return documento.RootElement
                .EnumerateArray()
                .Select(producto => producto.TryGetProperty("category", out JsonElement categoria)
                    ? categoria.GetString()
                    : null)
                .Where(nombre => !string.IsNullOrWhiteSpace(nombre))
                .Select(nombre => nombre!)
                .GroupBy(nombre => nombre, StringComparer.OrdinalIgnoreCase)
                .Select(grupo => new CategorySummary(grupo.Key, grupo.Count()))
                .OrderBy(categoria => categoria.Nombre, StringComparer.CurrentCulture)
                .ToList();
        }
        catch (Exception e) when (e is HttpRequestException or JsonException)
        {
            return null;
        }
    }

    public async Task ForwardAsync(HttpContext context, string path, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(
            new HttpMethod(context.Request.Method),
            path + context.Request.QueryString);

        if (context.Request.ContentLength > 0 || context.Request.Headers.ContainsKey("Transfer-Encoding"))
        {
            request.Content = new StreamContent(context.Request.Body);

            if (!string.IsNullOrWhiteSpace(context.Request.ContentType))
            {
                request.Content.Headers.TryAddWithoutValidation("Content-Type", context.Request.ContentType);
            }
        }

        request.Headers.TryAddWithoutValidation("Version", "1.0");

        try
        {
            using HttpResponseMessage response = await client.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            context.Response.StatusCode = (int)response.StatusCode;

            if (response.Content.Headers.ContentType is not null)
            {
                context.Response.ContentType = response.Content.Headers.ContentType.ToString();
            }

            if (response.Headers.Location is not null)
            {
                context.Response.Headers.Location = response.Headers.Location.ToString();
            }

            await response.Content.CopyToAsync(context.Response.Body, cancellationToken);
        }
        catch (HttpRequestException)
        {
            context.Response.StatusCode = StatusCodes.Status502BadGateway;
            await context.Response.WriteAsJsonAsync(new
            {
                estado = 502,
                error = "Servicio no disponible",
                mensaje = "Product Service no respondió."
            }, cancellationToken);
        }
    }
}
