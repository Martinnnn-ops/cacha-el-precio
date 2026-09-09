using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Product_Service.Models.Domain;

public static partial class ProductIdentity
{
    private static readonly HashSet<string> IgnoredTokens = new(StringComparer.Ordinal)
    {
        "zapatilla", "zapatillas", "zapato", "zapatos", "calzado",
        "hombre", "mujer", "nino", "nina", "unisex", "adulto", "infantil",
        "urbana", "urbano", "color", "modelo", "talla",
        "blanco", "blanca", "negro", "negra", "azul", "rojo", "roja",
        "verde", "gris", "beige", "cafe", "rosado", "rosada",
        "the", "de", "del", "para", "con", "y"
    };

    public static string CreateKey(string brand, string name)
    {
        string normalizedBrand = Normalize(brand);
        var brandTokens = normalizedBrand.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
        string normalizedName = ExplicitSize().Replace(Normalize(name), " ");
        var modelTokens = normalizedName
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(token => !IgnoredTokens.Contains(token))
            .Where(token => !brandTokens.Contains(token))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        string model = modelTokens.Length > 0
            ? string.Join('-', modelTokens)
            : Normalize(name).Replace(' ', '-');

        return $"{normalizedBrand.Replace(' ', '-')}:{model}";
    }

    public static string NormalizeKey(string key)
    {
        return string.Join(
            ':',
            Required(key, "canonicalKey")
                .Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(part => Normalize(part).Replace(' ', '-'))
        );
    }

    public static string Required(string? value, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{field} no puede estar vacío.");
        }

        return value.Trim();
    }

    public static string[] NormalizeSizes(IEnumerable<string>? sizes)
    {
        return (sizes ?? [])
            .Where(size => !string.IsNullOrWhiteSpace(size))
            .Select(size => size.Trim().ToUpperInvariant().Replace(',', '.'))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string Normalize(string value)
    {
        string decomposed = (value ?? string.Empty)
            .Trim()
            .ToLowerInvariant()
            .Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder();

        foreach (char character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(char.IsLetterOrDigit(character) ? character : ' ');
            }
        }

        return Whitespace().Replace(builder.ToString(), " ").Trim();
    }

    [GeneratedRegex(@"\btalla\s+\d+(?:[.]\d+)?\b")]
    private static partial Regex ExplicitSize();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
