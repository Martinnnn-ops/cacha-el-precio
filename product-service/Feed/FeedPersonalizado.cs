using System.Globalization;
using System.Text;
using Product_Service.Models.DTOs;

namespace Product_Service.Feed;

/// <summary>
/// Ordena el catálogo para una persona a partir de lo que ya guardó (deseados y
/// prendas de sus outfits). Es una función pura: no toca EF ni AWS, para poder
/// probarla sola.
///
/// La idea del feed «Para ti»: si alguien guarda ropa de hombre, que deje de
/// recomendarle vestidos; si guarda zapatillas, que suban las zapatillas; y
/// siempre con las que están en varias tiendas arriba, que es el punto de un
/// comparador de precios. Sin señales (cuenta nueva) cae a un orden general
/// razonable, no a una lista vacía.
/// </summary>
public static class FeedPersonalizado
{
    public static IReadOnlyList<ProductResponse> Rankear(
        IReadOnlyCollection<ProductResponse> catalogo,
        IReadOnlyCollection<int> guardados,
        int limite = 60)
    {
        HashSet<int> guardadosSet = guardados as HashSet<int> ?? [.. guardados];
        Perfil perfil = Perfil.Construir(catalogo.Where(p => guardadosSet.Contains(p.Id)));

        return catalogo
            .Where(p => !guardadosSet.Contains(p.Id))
            .Select(p => (producto: p, puntaje: Puntuar(p, perfil)))
            .OrderByDescending(x => x.puntaje)
            .ThenByDescending(x => x.producto.Visits)
            .ThenBy(x => PrecioDesde(x.producto))
            .Take(Math.Max(0, limite))
            .Select(x => x.producto)
            .ToList();
    }

    private static double Puntuar(ProductResponse p, Perfil perfil)
    {
        double s = 0;

        // Género: la señal más fuerte de un feed «diferente».
        if (!string.IsNullOrEmpty(perfil.GeneroDominante))
        {
            string g = Norm(p.Gender);
            if (g == perfil.GeneroDominante) s += 4;
            else if (g == "unisex") s += 1;
            else if (EsBinarioOpuesto(g, perfil.GeneroDominante)) s -= 5;
        }

        // Categoría: sube lo que la persona suele guardar.
        if (perfil.PesoCategoria.TryGetValue(Norm(p.Category), out double w)) s += 2 * w;

        // Marca que ya le gustó.
        if (perfil.Marcas.Contains(Norm(p.Brand))) s += 2;

        // Precio cercano a lo que suele mirar.
        if (perfil.PrecioMediana is int mediana && mediana > 0)
        {
            int precio = PrecioDesde(p);
            if (precio > 0)
            {
                double rel = Math.Abs(precio - mediana) / (double)mediana;
                s += Math.Max(0, 1.5 - rel); // misma banda => +1.5; se desvanece al alejarse
            }
        }

        // Comparabilidad: es un comparador, estar en varias tiendas vale.
        int tiendasActivas = p.Offers.Count(o => o.Active);
        if (tiendasActivas >= 2) s += 1.5;
        else if (tiendasActivas == 1) s += 0.75;

        // Popularidad, como desempate suave (log para que no aplaste al resto).
        s += Math.Min(2, Math.Log2(1 + Math.Max(0, p.Visits)) * 0.3);

        return s;
    }

    private static int PrecioDesde(ProductResponse p)
    {
        IEnumerable<int> activos = p.Offers.Where(o => o.Active).Select(o => o.Price);
        IEnumerable<int> fuente = activos.Any() ? activos : p.Offers.Select(o => o.Price);
        return fuente.Any() ? fuente.Min() : 0;
    }

    private static bool EsBinarioOpuesto(string genero, string dominante)
    {
        bool binario(string g) => g is "hombre" or "mujer";
        return binario(genero) && binario(dominante) && genero != dominante;
    }

    private static string Norm(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return "";
        string plano = texto.Trim().ToLower(CultureInfo.InvariantCulture)
            .Normalize(NormalizationForm.FormD);
        StringBuilder sb = new(plano.Length);
        foreach (char c in plano)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        return sb.ToString();
    }

    private sealed record Perfil(
        string? GeneroDominante,
        IReadOnlyDictionary<string, double> PesoCategoria,
        IReadOnlySet<string> Marcas,
        int? PrecioMediana)
    {
        public static Perfil Construir(IEnumerable<ProductResponse> guardados)
        {
            List<ProductResponse> lista = guardados.ToList();
            if (lista.Count == 0)
                return new Perfil(null, new Dictionary<string, double>(), new HashSet<string>(), null);

            // Género dominante entre los binarios (unisex no define preferencia).
            string? genero = lista
                .Select(p => Norm(p.Gender))
                .Where(g => g is "hombre" or "mujer")
                .GroupBy(g => g)
                .OrderByDescending(g => g.Count())
                .Select(g => g.Key)
                .FirstOrDefault();

            // Categorías con peso = frecuencia relativa (0..1).
            Dictionary<string, double> cuentas = lista
                .Select(p => Norm(p.Category))
                .Where(c => c.Length > 0)
                .GroupBy(c => c)
                .ToDictionary(g => g.Key, g => (double)g.Count());
            double max = cuentas.Values.DefaultIfEmpty(1).Max();
            Dictionary<string, double> pesos = cuentas.ToDictionary(kv => kv.Key, kv => kv.Value / max);

            HashSet<string> marcas = lista.Select(p => Norm(p.Brand)).Where(b => b.Length > 0).ToHashSet();

            List<int> precios = lista.Select(PrecioDesde).Where(x => x > 0).OrderBy(x => x).ToList();
            int? mediana = precios.Count > 0 ? precios[precios.Count / 2] : null;

            return new Perfil(genero, pesos, marcas, mediana);
        }
    }
}
