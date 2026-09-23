using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Product_Service.Models.Domain;

public static class CatalogPolicy
{
    public static string GenericKey(string store, string externalId) => "generica:" +
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            $"{ProductIdentity.Required(store, "store").ToLowerInvariant()}\0{ProductIdentity.Required(externalId, "externalId")}")))
        .ToLowerInvariant();

    private static string Normalize(string value) => Regex.Replace(string.Concat(
        value.ToLowerInvariant().Normalize(NormalizationForm.FormD)
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)),
        "[^a-z0-9]+", " ").Trim();

    public static string Brand(string? value) => Normalize(value ?? "") switch
    {
        "" or "generica" or "generico" or "genericas" or "genericos" or "generic"
        or "sin marca" or "no brand" or "unbranded" or "s m" or "n a" or "marca generica"
            => "Genéricas",
        _ => value!.Trim()
    };

    public static bool IsChild(string name, string? context = null)
    {
        string text = Regex.Replace(Normalize($"{name} {context}"), @"\bbaby (tee|doll)\b", "");
        return Regex.IsMatch(text, @"\b(bebe|bebes|baby|babies|infantil|infantiles|infant|infants|nino|ninos|nina|ninas|kids|kid|toddler|toddlers|newborn|recien nacido|recien nacida|preescolar|children)\b");
    }
}
