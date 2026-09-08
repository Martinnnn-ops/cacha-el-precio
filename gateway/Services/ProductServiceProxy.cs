namespace CachaElPrecio.Gateway.Services;

public sealed class ProductServiceProxy(HttpClient client)
{
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
