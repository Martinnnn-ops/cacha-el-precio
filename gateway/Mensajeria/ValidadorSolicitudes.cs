using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace CachaElPrecio.Gateway.Mensajeria;

// Reglas de forma de las solicitudes de administracion. Es una funcion pura: no
// consulta AWS ni la base, asi que se prueba sin infraestructura (igual que
// DecisionCambiosOferta en product-service). Las reglas que dependen del estado
// (que la DLQ exista, que el recurso no este repetido) viven en
// ServicioAdministracion, que es quien conoce lo que hay.
//
// Devuelve los errores por campo, en el formato de ValidationProblem de ASP.NET:
// { "nombre": ["..."], "filtro": ["..."] }. Vacio = valido.
public static partial class ValidadorSolicitudes
{
    // Limites de AWS (no inventados): SQS los documenta para cada atributo.
    public const int VisibilidadMax = 43200;        // 12 h
    public const int RetencionMin = 60;             // 1 min
    public const int RetencionMax = 1209600;        // 14 dias
    public const int EsperaMax = 20;                // long polling
    // Mas de 10 intentos solo demora en apartar un mensaje que no se va a arreglar.
    public const int MaxIntentosMax = 10;
    public const int LargoNombreMax = 74;           // cep- + 70 (tarjeta T11)

    // Los atributos que product-service publica en cada mensaje SNS. Un filtro
    // sobre otro atributo no calzaria nunca: el mensaje no lo trae.
    public static readonly IReadOnlySet<string> AtributosFiltrables =
        new HashSet<string>(StringComparer.Ordinal) { "type", "discountPct" };

    public static readonly IReadOnlySet<string> TiposConocidos =
        new HashSet<string>(StringComparer.Ordinal) { "precio.bajo", "oferta.agotada", "oferta.repuesta" };

    private static readonly IReadOnlySet<string> OperadoresNumericos =
        new HashSet<string>(StringComparer.Ordinal) { "=", "<", "<=", ">", ">=" };

    // cep- + palabras en minusculas separadas por un guion: cep-ofertas-crudas-dlq.
    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex FormatoNombre();

    public static Dictionary<string, string[]> ValidarNombre(string? nombre, string prefijo, string campo = "nombre")
    {
        var errores = new Dictionary<string, string[]>();
        var e = ErroresNombre(nombre, prefijo);
        if (e.Count > 0) errores[campo] = [.. e];
        return errores;
    }

    public static Dictionary<string, string[]> ValidarCola(CrearColaSolicitud s, string prefijo)
    {
        var errores = new Errores();
        errores.Agregar("nombre", ErroresNombre(s.Nombre, prefijo));

        if (s.VisibilidadSegundos is < 0 or > VisibilidadMax)
            errores.Agregar("visibilidadSegundos", $"Debe estar entre 0 y {VisibilidadMax} segundos (12 horas).");

        if (s.RetencionSegundos is < RetencionMin or > RetencionMax)
            errores.Agregar("retencionSegundos", $"Debe estar entre {RetencionMin} y {RetencionMax} segundos (14 dias).");

        if (s.EsperaSegundos is < 0 or > EsperaMax)
            errores.Agregar("esperaSegundos", $"Debe estar entre 0 y {EsperaMax} segundos.");

        bool hayDlq = !string.IsNullOrWhiteSpace(s.Dlq);
        if (hayDlq)
        {
            errores.Agregar("dlq", ErroresNombre(s.Dlq, prefijo));
            if (s.Dlq == s.Nombre)
                errores.Agregar("dlq", "Una cola no puede ser su propia DLQ.");
            if (s.MaxIntentos is null)
                errores.Agregar("maxIntentos", "Es obligatorio cuando se indica una DLQ: cuantas veces se reintenta antes de apartar el mensaje.");
        }
        else if (s.MaxIntentos is not null)
        {
            errores.Agregar("maxIntentos", "Solo tiene sentido junto con una DLQ.");
        }

        if (s.MaxIntentos is < 1 or > MaxIntentosMax)
            errores.Agregar("maxIntentos", $"Debe estar entre 1 y {MaxIntentosMax}.");

        return errores.Diccionario;
    }

    public static Dictionary<string, string[]> ValidarTopico(CrearTopicoSolicitud s, string prefijo) =>
        ValidarNombre(s.Nombre, prefijo);

    public static Dictionary<string, string[]> ValidarSuscripcion(CrearSuscripcionSolicitud s, string prefijo)
    {
        var errores = new Errores();
        errores.Agregar("topico", ErroresNombre(s.Topico, prefijo));
        errores.Agregar("cola", ErroresNombre(s.Cola, prefijo));
        if (s.Filtro is not null)
            errores.Agregar("filtro", ErroresFiltro(s.Filtro));
        return errores.Diccionario;
    }

    // ---------------------------------------------------------------------
    // Nombre
    // ---------------------------------------------------------------------
    private static List<string> ErroresNombre(string? nombre, string prefijo)
    {
        var e = new List<string>();
        if (string.IsNullOrWhiteSpace(nombre))
        {
            e.Add("Es obligatorio.");
            return e;
        }

        // El prefijo separa lo nuestro del resto de la cuenta: este componente no
        // puede ver ni borrar colas de otros proyectos del mismo laboratorio.
        if (!nombre.StartsWith(prefijo, StringComparison.Ordinal))
            e.Add($"Debe empezar con '{prefijo}'. Solo se administran los recursos de Cacha el Precio.");

        if (nombre.Length > LargoNombreMax)
            e.Add($"Puede tener como maximo {LargoNombreMax} caracteres.");

        if (!FormatoNombre().IsMatch(nombre))
            e.Add("Solo minusculas, numeros y guiones simples entre palabras (ejemplo: cep-ofertas-crudas).");

        if (nombre.EndsWith(".fifo", StringComparison.Ordinal))
            e.Add("Las colas FIFO no se usan en este proyecto.");

        return e;
    }

    // ---------------------------------------------------------------------
    // Filtro de SNS (FilterPolicy sobre los atributos del mensaje)
    // ---------------------------------------------------------------------
    public static List<string> ErroresFiltro(JsonNode filtro)
    {
        var e = new List<string>();
        if (filtro is not JsonObject objeto)
        {
            e.Add("Debe ser un objeto JSON, por ejemplo {\"type\": [\"precio.bajo\"]}.");
            return e;
        }

        if (objeto.Count == 0)
        {
            e.Add("Un filtro vacio no filtra nada: si la cola debe recibir todo, manda filtro null.");
            return e;
        }

        RevisarCondicion(objeto, "filtro", e, profundidad: 0);
        return e;
    }

    private static void RevisarCondicion(JsonObject objeto, string ruta, List<string> e, int profundidad)
    {
        // SNS permite anidar $or, pero mas de un nivel no lo necesita ningun caso
        // de uso nuestro y vuelve el filtro dificil de leer.
        if (profundidad > 1)
        {
            e.Add($"{ruta}: no se permite anidar '$or' dentro de otro '$or'.");
            return;
        }

        foreach (var (clave, valor) in objeto)
        {
            string aqui = $"{ruta}.{clave}";

            if (clave == "$or")
            {
                if (valor is not JsonArray alternativas || alternativas.Count < 2)
                {
                    e.Add($"{aqui}: debe ser una lista con al menos 2 alternativas.");
                    continue;
                }
                for (int i = 0; i < alternativas.Count; i++)
                {
                    if (alternativas[i] is JsonObject alternativa && alternativa.Count > 0)
                        RevisarCondicion(alternativa, $"{aqui}[{i}]", e, profundidad + 1);
                    else
                        e.Add($"{aqui}[{i}]: cada alternativa debe ser un objeto con condiciones.");
                }
                continue;
            }

            if (!AtributosFiltrables.Contains(clave))
            {
                e.Add($"{aqui}: '{clave}' no es un atributo que publiquen los productores. " +
                      $"Se puede filtrar por: {string.Join(", ", AtributosFiltrables)}.");
                continue;
            }

            if (valor is not JsonArray condiciones || condiciones.Count == 0)
            {
                e.Add($"{aqui}: debe ser una lista no vacia de valores o condiciones.");
                continue;
            }

            foreach (var condicion in condiciones)
                RevisarValor(clave, condicion, aqui, e);
        }
    }

    private static void RevisarValor(string atributo, JsonNode? condicion, string ruta, List<string> e)
    {
        switch (condicion)
        {
            case JsonValue v when v.GetValueKind() == JsonValueKind.String:
                string texto = v.GetValue<string>();
                if (atributo == "type" && !TiposConocidos.Contains(texto))
                    e.Add($"{ruta}: el tipo '{texto}' no existe. Tipos publicados: {string.Join(", ", TiposConocidos)}.");
                if (atributo == "discountPct")
                    e.Add($"{ruta}: discountPct es numerico; usa {{\"numeric\": [\">=\", 10]}}.");
                break;

            case JsonValue v when v.GetValueKind() == JsonValueKind.Number:
                if (atributo == "type")
                    e.Add($"{ruta}: type es texto, no numero.");
                break;

            case JsonObject operador when operador.Count == 1 && operador.ContainsKey("numeric"):
                if (atributo != "discountPct")
                    e.Add($"{ruta}: 'numeric' solo aplica a discountPct.");
                RevisarNumerico(operador["numeric"], ruta, e);
                break;

            case JsonObject operador when operador.Count == 1 && operador.ContainsKey("exists"):
                if (operador["exists"] is not JsonValue ex || ex.GetValueKind() is not (JsonValueKind.True or JsonValueKind.False))
                    e.Add($"{ruta}: 'exists' debe ser true o false.");
                break;

            case JsonObject operador when operador.Count == 1 && operador.ContainsKey("anything-but"):
                break;

            default:
                e.Add($"{ruta}: condicion no soportada. Usa un texto, un numero o un objeto " +
                      "'numeric', 'exists' o 'anything-but'.");
                break;
        }
    }

    private static void RevisarNumerico(JsonNode? nodo, string ruta, List<string> e)
    {
        // ["op", n] o ["op", n, "op", n] (rango).
        if (nodo is not JsonArray partes || partes.Count is not (2 or 4))
        {
            e.Add($"{ruta}: 'numeric' debe ser [\">=\", 10] o un rango [\">\", 0, \"<=\", 100].");
            return;
        }

        for (int i = 0; i < partes.Count; i += 2)
        {
            bool operadorOk = partes[i] is JsonValue op && op.GetValueKind() == JsonValueKind.String
                && OperadoresNumericos.Contains(op.GetValue<string>());
            bool numeroOk = partes[i + 1] is JsonValue n && n.GetValueKind() == JsonValueKind.Number;
            if (!operadorOk || !numeroOk)
            {
                e.Add($"{ruta}: cada par de 'numeric' es un operador ({string.Join(" ", OperadoresNumericos)}) seguido de un numero.");
                return;
            }
        }
    }

    // Acumula mensajes por campo sin repetir el mismo campo.
    private sealed class Errores
    {
        private readonly Dictionary<string, List<string>> porCampo = new();

        public void Agregar(string campo, string mensaje) => Agregar(campo, [mensaje]);

        public void Agregar(string campo, IEnumerable<string> mensajes)
        {
            foreach (var m in mensajes)
            {
                if (!porCampo.TryGetValue(campo, out var lista))
                    porCampo[campo] = lista = [];
                lista.Add(m);
            }
        }

        public Dictionary<string, string[]> Diccionario =>
            porCampo.ToDictionary(kv => kv.Key, kv => kv.Value.ToArray());
    }
}
