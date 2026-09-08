using System.Globalization;
using System.Text;

namespace Product_Service.Models.Domain;

public class Category
{
    public CategoryType Type { get; }

    public string OriginalValue { get; }

    private static readonly Dictionary<string, CategoryType> Aliases =
        new(StringComparer.OrdinalIgnoreCase)
        {
            // Calzado
            ["calzado"] = CategoryType.Calzado,
            ["zapatilla"] = CategoryType.Calzado,
            ["zapatillas"] = CategoryType.Calzado,
            ["zapato"] = CategoryType.Calzado,
            ["zapatos"] = CategoryType.Calzado,
            ["sneaker"] = CategoryType.Calzado,
            ["sneakers"] = CategoryType.Calzado,
            ["tenis"] = CategoryType.Calzado,

            // Poleras
            ["polera"] = CategoryType.Poleras,
            ["poleras"] = CategoryType.Poleras,
            ["camiseta"] = CategoryType.Poleras,
            ["camisetas"] = CategoryType.Poleras,
            ["remera"] = CategoryType.Poleras,
            ["t-shirt"] = CategoryType.Poleras,

            // Pantalones
            ["pantalon"] = CategoryType.Pantalones,
            ["pantalones"] = CategoryType.Pantalones,
            ["jeans"] = CategoryType.Pantalones,
            ["vaqueros"] = CategoryType.Pantalones,

            // Polerones
            ["poleron"] = CategoryType.Polerones,
            ["polerones"] = CategoryType.Polerones,
            ["sudadera"] = CategoryType.Polerones,
            ["campera"] = CategoryType.Polerones,
            ["canguro"] = CategoryType.Polerones,
            ["jersey"] = CategoryType.Polerones,

            // Categorías emitidas por el scraper
            ["botas"] = CategoryType.Botas,
            ["sandalias"] = CategoryType.Sandalias,
            ["zapatos"] = CategoryType.Zapatos,
            ["camisas"] = CategoryType.Camisas,
            ["vestidos"] = CategoryType.Vestidos,
            ["pijamas"] = CategoryType.Pijamas,
            ["ropa interior"] = CategoryType.RopaInterior,
            ["medias"] = CategoryType.Medias,
            ["vestimenta"] = CategoryType.Vestimenta
        };

    private Category(CategoryType type, string originalValue)
    {
        Type = type;
        OriginalValue = originalValue;
    }

    public Category(string value)
    {
        var resultado = Constructor(value);
        Type = resultado.Item1;
        OriginalValue = resultado.Item2;
    }

    public string GetStringValue()
    {
        return $"{Type}";
    }

    public static Category From(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return new Category(CategoryType.Unknown, value);
        }

        string normalized = Normalize(value);

        if (Aliases.TryGetValue(normalized, out CategoryType type))
        {
            return new Category(type, value);
        }

        return new Category(CategoryType.Unknown, value);
    }

    private static (CategoryType, string) Constructor(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return (CategoryType.Unknown, value);
        }

        string normalized = Normalize(value);

        if (Aliases.TryGetValue(normalized, out CategoryType type))
        {
            return (type, value);
        }

        return (CategoryType.Unknown, value);
    }


    private static string Normalize(string value)
    {
        string normalized = value
            .Trim()
            .ToLowerInvariant()
            .Normalize(NormalizationForm.FormD);

        var builder = new StringBuilder();

        foreach (char character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character)
                != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(character);
            }
        }

        return builder
            .ToString()
            .Normalize(NormalizationForm.FormC);
    }
}
