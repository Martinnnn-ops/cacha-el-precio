using System.Text.Json;
using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Product_Service.Data;
using Product_Service.Models.Domain;

namespace Product_Service.Controllers;

// Servicio privado en la red interna. El gateway obtiene owner del JWT, nunca del navegador.
[ApiController, ApiVersionNeutral, Route("internal/personal/{owner}")]
[RequestSizeLimit(16384)]
public sealed class PersonalController(ProductDbContext db) : ControllerBase
{
    private static readonly HashSet<string> Slots = ["cabeza", "torso-base", "torso-intermedia",
        "torso-abrigo", "interior", "piernas", "calcetines", "calzado"];

    [HttpGet]
    public async Task<IActionResult> List(string owner, CancellationToken ct)
    {
        var items = await db.PersonalItems.AsNoTracking().Where(p => p.Owner == owner)
            .OrderByDescending(p => p.UpdatedAt).ToListAsync(ct);
        return Ok(new {
            productos = items.Where(p => p.Kind == "deseado").Select(p => int.Parse(p.Key)),
            total = items.Count(p => p.Kind == "deseado"),
            outfits = items.Where(p => p.Kind == "outfit").Select(p => new {
                id = p.Key, datos = JsonSerializer.Deserialize<OutfitInput>(p.Payload), p.UpdatedAt
            })
        });
    }

    [HttpPut("deseados/{id:int:min(1)}")]
    [HttpPost("deseados/{id:int:min(1)}")]
    public async Task<IActionResult> Wish(string owner, int id, CancellationToken ct)
    {
        var product = await db.Products.AsNoTracking().SingleOrDefaultAsync(p => p.ProductId == id, ct);
        if (product is null || CatalogPolicy.IsChild(product.ProductName, $"{product.ProductCategory} {product.Gender}"))
            return NotFound();
        return await Save(owner, "deseado", id.ToString(), "{}", 200, ct);
    }

    [HttpPut("outfits/{id:guid}")]
    public Task<IActionResult> Outfit(string owner, Guid id, OutfitInput input, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input.Nombre) || input.Nombre.Length > 80 ||
            input.Seleccion is null || input.Seleccion.Count is < 1 or > 8 ||
            input.Seleccion.Any(p => !Slots.Contains(p.Key) || p.Value <= 0) ||
            (input.TallaRopa?.Length ?? 0) > 30 || (input.TallaCalzado?.Length ?? 0) > 30)
            return Task.FromResult<IActionResult>(BadRequest());
        // Guardamos referencias, no precios: al abrir se consulta el stock actual.
        return Save(owner, "outfit", id.ToString(), JsonSerializer.Serialize(input), 50, ct);
    }

    [HttpDelete("{kind}/{key}")]
    public async Task<IActionResult> Delete(string owner, string kind, string key, CancellationToken ct)
    {
        string? type = kind switch { "deseados" => "deseado", "outfits" => "outfit", _ => null };
        if (type is null) return BadRequest();
        await db.PersonalItems.Where(p => p.Owner == owner && p.Kind == type && p.Key == key)
            .ExecuteDeleteAsync(ct);
        return NoContent();
    }

    private async Task<IActionResult> Save(string owner, string kind, string key, string payload,
        int limit, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(owner) || owner.Length > 128) return BadRequest();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        // Serializa las escrituras del mismo usuario, incluso con varias instancias del BFF.
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({owner}, 0))", ct);
        var existing = await db.PersonalItems.FindAsync([owner, kind, key], ct);
        if (existing is null)
        {
            if (await db.PersonalItems.CountAsync(p => p.Owner == owner && p.Kind == kind, ct) >= limit)
                return Conflict(new { mensaje = $"Puedes guardar hasta {limit} elementos." });
            db.PersonalItems.Add(new() { Owner = owner, Kind = kind, Key = key,
                Payload = payload, UpdatedAt = DateTimeOffset.UtcNow });
        }
        else { existing.Payload = payload; existing.UpdatedAt = DateTimeOffset.UtcNow; }
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return NoContent();
    }

    public sealed record OutfitInput(string Nombre, Dictionary<string, int>? Seleccion,
        string? TallaRopa, string? TallaCalzado);
}
