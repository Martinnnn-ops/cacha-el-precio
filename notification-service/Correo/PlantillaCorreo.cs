using System.Globalization;
using System.Net;
using Notification_Service.Models.Eventos;

namespace Notification_Service.Correo;

// Arma el asunto y los dos cuerpos (HTML y texto) de cada tipo de aviso.
//
// El HTML va con tablas y estilos en linea a proposito: Gmail y Outlook ignoran
// las hojas de estilo y el flex/grid, y una tabla de 560 px es lo que se ve
// igual en todos. Los colores son los de la marca (frontend/src/assets/base.css).
public static class PlantillaCorreo
{
    private static readonly CultureInfo Clp = CultureInfo.GetCultureInfo("es-CL");

    private const string Fondo = "#eae6d9";
    private const string Tinta = "#22271f";
    private const string Suave = "#615e51";
    private const string Linea = "#ddd7c4";
    private const string Fuente = "'IBM Plex Sans',Helvetica,Arial,sans-serif";

    public static readonly string[] Tipos = ["precio.bajo", "oferta.agotada", "oferta.repuesta"];

    // Lo que cambia entre un tipo de aviso y otro.
    private sealed record Aviso(
        string Asunto, string Etiqueta, string Color, string Titular,
        string PrecioHtml, string PrecioTexto, string LlamadaTienda);

    public static CorreoSalida Construir(string tipo, DatosCambioOferta d, string sitioUrl)
    {
        string nombre = d.Name ?? "una prenda que sigues";

        Aviso aviso = tipo switch
        {
            "precio.bajo" => new Aviso(
                Asunto: $"Bajó {d.DiscountPct}% · {nombre}",
                Etiqueta: $"Bajó {d.DiscountPct}%",
                Color: "#2f6b4f",
                Titular: "La prenda que sigues bajó de precio",
                PrecioHtml:
                    $"""
                    <span style="font-size:15px;color:{Suave};text-decoration:line-through">{Pesos(d.PreviousPrice)}</span>
                    <span style="font-size:30px;font-weight:700;color:{Tinta};padding-left:8px">{Pesos(d.NewPrice)}</span>
                    """,
                PrecioTexto: $"Antes {Pesos(d.PreviousPrice)} -> Ahora {Pesos(d.NewPrice)} ({d.DiscountPct}% menos)",
                LlamadaTienda: "Comprar en"),

            "oferta.agotada" => new Aviso(
                Asunto: $"Se agotó · {nombre}",
                Etiqueta: "Se agotó",
                Color: "#b23a2b",
                Titular: "La prenda que sigues se agotó",
                PrecioHtml:
                    $"""
                    <span style="font-size:15px;color:{Suave}">Último precio visto</span>
                    <span style="font-size:24px;font-weight:700;color:{Tinta};padding-left:8px">{Pesos(d.LastPrice)}</span>
                    """,
                PrecioTexto: $"Estaba a {Pesos(d.LastPrice)} cuando dejo de estar disponible.",
                LlamadaTienda: "Revisar en"),

            "oferta.repuesta" => new Aviso(
                Asunto: $"Volvió · {nombre}",
                Etiqueta: "Volvió",
                Color: "#1e4a85",
                Titular: "La prenda que sigues volvió a estar disponible",
                PrecioHtml:
                    $"""
                    <span style="font-size:15px;color:{Suave}">Disponible a</span>
                    <span style="font-size:30px;font-weight:700;color:{Tinta};padding-left:8px">{Pesos(d.Price)}</span>
                    """,
                PrecioTexto: $"Disponible otra vez a {Pesos(d.Price)}.",
                LlamadaTienda: "Comprar en"),

            _ => throw new ArgumentOutOfRangeException(nameof(tipo), tipo, "tipo sin plantilla")
        };

        return Cuerpo(aviso, d, nombre, sitioUrl);
    }

    // El mismo correo con una prenda inventada, para mostrar como se ve cada
    // tipo sin esperar a que cambie una oferta. No se envia a nadie.
    public static CorreoSalida Ejemplo(string tipo, string sitioUrl)
    {
        var datos = new DatosCambioOferta(
            ProductId: 1,
            Slug: null,
            Name: "Polera Básica Hombre Manga Corta 100% Algodón",
            Image: null,
            Store: "Falabella",
            Url: null,
            PreviousPrice: 19990,
            NewPrice: 13990,
            DiscountPct: 30,
            LastPrice: 13990,
            Price: 14990);
        return Construir(tipo, datos, sitioUrl) with { Para = "ejemplo@cachaelprecio.cl" };
    }

    private static CorreoSalida Cuerpo(Aviso a, DatosCambioOferta d, string nombre, string sitioUrl)
    {
        string detalle = d.Slug is null ? sitioUrl : $"{sitioUrl}/producto/{d.Slug}";
        string tienda = Tienda(d.Store);
        string nombreHtml = WebUtility.HtmlEncode(nombre);

        string imagen = d.Image is null
            ? ""
            : $"""
              <tr><td style="padding:0 32px 8px" align="center">
                <img src="{WebUtility.HtmlEncode(d.Image)}" alt="{nombreHtml}" width="240"
                     style="display:block;width:240px;max-width:100%;height:auto;border-radius:10px;border:1px solid {Linea}" />
              </td></tr>
              """;

        string botonTienda = d.Url is null
            ? ""
            : $"""
              <td style="padding:0 4px">
                <a href="{WebUtility.HtmlEncode(d.Url)}" style="display:inline-block;padding:12px 20px;border-radius:8px;
                   background:{Tinta};color:#ffffff;font-weight:600;font-size:14px;text-decoration:none">{a.LlamadaTienda} {tienda}</a>
              </td>
              """;

        string html =
            $"""
            <!doctype html>
            <html lang="es"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width"></head>
            <body style="margin:0;padding:0;background:{Fondo}">
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background:{Fondo};font-family:{Fuente};color:{Tinta}">
              <tr><td align="center" style="padding:24px 12px">
                <table role="presentation" width="560" cellpadding="0" cellspacing="0" style="width:560px;max-width:100%">
                  <tr><td style="padding:0 4px 14px;font-size:15px;font-weight:700;letter-spacing:.2px">
                    Cacha el Precio
                    <span style="font-weight:400;color:{Suave};font-size:13px"> · el descuento real, no el inflado</span>
                  </td></tr>
                  <tr><td style="background:#ffffff;border-radius:14px;border:1px solid {Linea};overflow:hidden">
                    <table role="presentation" width="100%" cellpadding="0" cellspacing="0">
                      <tr><td style="height:6px;background:{a.Color};font-size:0;line-height:0">&nbsp;</td></tr>
                      <tr><td style="padding:28px 32px 6px">
                        <span style="display:inline-block;padding:4px 10px;border-radius:999px;background:{a.Color};
                              color:#ffffff;font-size:12px;font-weight:700;letter-spacing:.3px;text-transform:uppercase">{a.Etiqueta}</span>
                        <p style="margin:14px 0 4px;font-size:14px;color:{Suave}">{a.Titular}</p>
                        <h1 style="margin:0 0 18px;font-size:22px;line-height:1.3;font-weight:700">{nombreHtml}</h1>
                      </td></tr>
                      {imagen}
                      <tr><td style="padding:12px 32px 4px">
                        <table role="presentation" width="100%" cellpadding="0" cellspacing="0"
                               style="background:{Fondo};border-radius:10px">
                          <tr><td style="padding:16px 18px">
                            {a.PrecioHtml}
                            <div style="font-size:13px;color:{Suave};padding-top:4px">en {tienda}</div>
                          </td></tr>
                        </table>
                      </td></tr>
                      <tr><td align="center" style="padding:22px 28px 30px">
                        <table role="presentation" cellpadding="0" cellspacing="0"><tr>
                          {botonTienda}
                          <td style="padding:0 4px">
                            <a href="{WebUtility.HtmlEncode(detalle)}" style="display:inline-block;padding:11px 19px;border-radius:8px;
                               border:1px solid {Tinta};color:{Tinta};font-weight:600;font-size:14px;text-decoration:none">Ver el historial de precios</a>
                          </td>
                        </tr></table>
                      </td></tr>
                    </table>
                  </td></tr>
                  <tr><td style="padding:16px 8px 0;font-size:12px;line-height:1.5;color:{Suave}">
                    Recibes este aviso porque marcaste esta prenda como deseada en Cacha el Precio.
                    Para dejar de recibirlo, quítala de tus deseados en <a href="{WebUtility.HtmlEncode(detalle)}" style="color:{Suave}">su ficha</a>.
                  </td></tr>
                </table>
              </td></tr>
            </table>
            </body></html>
            """;

        string texto =
            $"""
            {a.Titular}: {nombre}

            {a.PrecioTexto}
            Tienda: {tienda}

            Ver el historial de precios: {detalle}
            {(d.Url is null ? "" : $"{a.LlamadaTienda} {tienda}: {d.Url}")}

            Recibes esto porque marcaste esta prenda como deseada en Cacha el Precio.
            """;

        return new CorreoSalida("", a.Asunto, html, texto);
    }

    // "falabella" -> "Falabella"; sin tienda, un generico.
    private static string Tienda(string? store) =>
        string.IsNullOrWhiteSpace(store) ? "la tienda" : Clp.TextInfo.ToTitleCase(store.ToLower(Clp));

    private static string Pesos(int? valor) =>
        valor is null ? "—" : "$" + valor.Value.ToString("N0", Clp);
}
