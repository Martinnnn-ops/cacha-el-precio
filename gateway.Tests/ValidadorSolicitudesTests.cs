using CachaElPrecio.Gateway.Mensajeria;
using System.Text.Json.Nodes;

namespace CachaElPrecio.Gateway.Tests;

/// <summary>
/// Casos válidos e inválidos de las solicitudes de administración. Es la
/// evidencia del indicador «validar las solicitudes» (7%): cada caso inválido
/// devuelve el campo y un mensaje que dice qué arreglar.
/// </summary>
public class ValidadorSolicitudesTests
{
    private const string Prefijo = "cep-";

    // ── Nombres ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData("cep-ofertas-crudas")]
    [InlineData("cep-notificaciones-dlq")]
    [InlineData("cep-prueba1")]
    public void Nombre_valido_no_tiene_errores(string nombre) =>
        Assert.Empty(ValidadorSolicitudes.ValidarNombre(nombre, Prefijo));

    [Theory]
    [InlineData(null, "obligatorio")]
    [InlineData("", "obligatorio")]
    [InlineData("ofertas", "Debe empezar con 'cep-'")]
    [InlineData("cep-Ofertas", "minusculas")]
    [InlineData("cep-ofertas_crudas", "minusculas")]
    [InlineData("cep--doble", "minusculas")]
    [InlineData("cep-ofertas-", "minusculas")]
    public void Nombre_invalido_dice_que_arreglar(string? nombre, string pista)
    {
        var errores = ValidadorSolicitudes.ValidarNombre(nombre, Prefijo);

        var mensajes = Assert.Contains("nombre", (IDictionary<string, string[]>)errores);
        Assert.Contains(mensajes, m => m.Contains(pista, StringComparison.Ordinal));
    }

    [Fact]
    public void Nombre_de_mas_de_74_caracteres_se_rechaza()
    {
        var errores = ValidadorSolicitudes.ValidarNombre("cep-" + new string('a', 71), Prefijo);
        Assert.Contains(errores["nombre"], m => m.Contains("74"));
    }

    // ── Colas ────────────────────────────────────────────────────────────

    [Fact]
    public void Cola_con_dlq_y_valores_en_rango_es_valida()
    {
        var s = new CrearColaSolicitud("cep-prueba", 60, 86400, 20, "cep-prueba-dlq", 5);
        Assert.Empty(ValidadorSolicitudes.ValidarCola(s, Prefijo));
    }

    [Fact]
    public void Cola_solo_con_nombre_es_valida_porque_usa_los_valores_por_defecto() =>
        Assert.Empty(ValidadorSolicitudes.ValidarCola(new CrearColaSolicitud("cep-prueba", null, null, null, null, null), Prefijo));

    [Theory]
    [InlineData(-1, null, null, "visibilidadSegundos")]
    [InlineData(43201, null, null, "visibilidadSegundos")]
    [InlineData(null, 59, null, "retencionSegundos")]
    [InlineData(null, 1209601, null, "retencionSegundos")]
    [InlineData(null, null, 21, "esperaSegundos")]
    public void Cola_con_valor_fuera_de_rango_marca_el_campo(int? visibilidad, int? retencion, int? espera, string campo)
    {
        var s = new CrearColaSolicitud("cep-prueba", visibilidad, retencion, espera, null, null);
        Assert.True(ValidadorSolicitudes.ValidarCola(s, Prefijo).ContainsKey(campo));
    }

    [Fact]
    public void Dlq_sin_max_intentos_se_rechaza()
    {
        var s = new CrearColaSolicitud("cep-prueba", null, null, null, "cep-prueba-dlq", null);
        Assert.True(ValidadorSolicitudes.ValidarCola(s, Prefijo).ContainsKey("maxIntentos"));
    }

    [Fact]
    public void Max_intentos_sin_dlq_se_rechaza()
    {
        var s = new CrearColaSolicitud("cep-prueba", null, null, null, null, 5);
        Assert.True(ValidadorSolicitudes.ValidarCola(s, Prefijo).ContainsKey("maxIntentos"));
    }

    [Fact]
    public void Una_cola_no_puede_ser_su_propia_dlq()
    {
        var s = new CrearColaSolicitud("cep-prueba", null, null, null, "cep-prueba", 3);
        Assert.Contains(ValidadorSolicitudes.ValidarCola(s, Prefijo)["dlq"], m => m.Contains("propia DLQ"));
    }

    [Fact]
    public void Varios_errores_se_informan_juntos()
    {
        var s = new CrearColaSolicitud("Mala", -5, 10, 99, null, 0);
        var errores = ValidadorSolicitudes.ValidarCola(s, Prefijo);
        Assert.Equal(
            new[] { "esperaSegundos", "maxIntentos", "nombre", "retencionSegundos", "visibilidadSegundos" },
            errores.Keys.Order());
    }

    // ── Filtros de suscripción ───────────────────────────────────────────

    // El filtro real de topologia.json para cep-notificaciones.
    private const string FiltroNotificaciones = """
        {"$or": [
            {"type": ["precio.bajo"], "discountPct": [{"numeric": [">=", 10]}]},
            {"type": ["oferta.agotada", "oferta.repuesta"]}
        ]}
        """;

    [Theory]
    [InlineData(FiltroNotificaciones)]
    [InlineData("""{"type": ["precio.bajo"]}""")]
    [InlineData("""{"discountPct": [{"numeric": [">", 0, "<=", 100]}]}""")]
    [InlineData("""{"type": [{"anything-but": ["oferta.repuesta"]}]}""")]
    public void Filtro_valido_no_tiene_errores(string filtro) =>
        Assert.Empty(ValidadorSolicitudes.ErroresFiltro(JsonNode.Parse(filtro)!));

    [Theory]
    [InlineData("""["precio.bajo"]""", "objeto JSON")]
    [InlineData("""{}""", "vacio")]
    [InlineData("""{"tienda": ["paris"]}""", "no es un atributo")]
    [InlineData("""{"type": ["precio.subio"]}""", "no existe")]
    [InlineData("""{"type": []}""", "lista no vacia")]
    [InlineData("""{"type": "precio.bajo"}""", "lista no vacia")]
    [InlineData("""{"discountPct": ["10"]}""", "numerico")]
    [InlineData("""{"discountPct": [{"numeric": ["mas de", 10]}]}""", "operador")]
    [InlineData("""{"discountPct": [{"numeric": [">="]}]}""", "rango")]
    [InlineData("""{"type": [{"numeric": [">", 1]}]}""", "solo aplica a discountPct")]
    [InlineData("""{"$or": [{"type": ["precio.bajo"]}]}""", "al menos 2")]
    [InlineData("""{"$or": [{"$or": [{"type": ["precio.bajo"]}, {"type": ["oferta.agotada"]}]}, {"type": ["oferta.repuesta"]}]}""", "anidar")]
    public void Filtro_invalido_dice_que_arreglar(string filtro, string pista)
    {
        var errores = ValidadorSolicitudes.ErroresFiltro(JsonNode.Parse(filtro)!);
        Assert.Contains(errores, m => m.Contains(pista, StringComparison.Ordinal));
    }

    [Fact]
    public void Suscripcion_sin_filtro_es_valida_porque_recibe_todo()
    {
        var s = new CrearSuscripcionSolicitud("cep-cambios-oferta", "cep-auditoria-precios", null, true);
        Assert.Empty(ValidadorSolicitudes.ValidarSuscripcion(s, Prefijo));
    }

    [Fact]
    public void Suscripcion_sin_topico_ni_cola_marca_ambos_campos()
    {
        var errores = ValidadorSolicitudes.ValidarSuscripcion(new CrearSuscripcionSolicitud(null, "", null, null), Prefijo);
        Assert.True(errores.ContainsKey("topico"));
        Assert.True(errores.ContainsKey("cola"));
    }
}
