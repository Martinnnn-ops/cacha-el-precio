using System.Globalization;
using Notification_Service.Models.Eventos;

namespace Notification_Service.Correo;

// Arma el asunto y los dos cuerpos (HTML y texto) de cada tipo de aviso.
public static class PlantillaCorreo
{
    private static readonly CultureInfo Clp = CultureInfo.GetCultureInfo("es-CL");

    public static CorreoSalida Construir(string tipo, DatosCambioOferta d, string sitioUrl)
    {
        string nombre = d.Name ?? "una prenda que sigues";
        string detalle = d.Slug is null ? sitioUrl : $"{sitioUrl}/producto/{d.Slug}";

        return tipo switch
        {
            "precio.bajo" => Cuerpo(
                asunto: $"Bajó {d.DiscountPct}% · {nombre}",
                titular: $"¡Bajó de precio! {nombre}",
                detallePrecio: $"Antes {Pesos(d.PreviousPrice)} · Ahora <strong>{Pesos(d.NewPrice)}</strong> " +
                               $"({d.DiscountPct}% menos)",
                detallePrecioTexto: $"Antes {Pesos(d.PreviousPrice)} -> Ahora {Pesos(d.NewPrice)} ({d.DiscountPct}% menos)",
                d, detalle, sitioUrl),

            "oferta.agotada" => Cuerpo(
                asunto: $"Se agotó · {nombre}",
                titular: $"Se agotó {nombre}",
                detallePrecio: $"Estaba a {Pesos(d.LastPrice)} cuando dejó de estar disponible.",
                detallePrecioTexto: $"Estaba a {Pesos(d.LastPrice)} cuando dejo de estar disponible.",
                d, detalle, sitioUrl),

            "oferta.repuesta" => Cuerpo(
                asunto: $"Volvió · {nombre}",
                titular: $"Volvió a estar disponible: {nombre}",
                detallePrecio: $"Disponible otra vez a <strong>{Pesos(d.Price)}</strong>.",
                detallePrecioTexto: $"Disponible otra vez a {Pesos(d.Price)}.",
                d, detalle, sitioUrl),

            _ => throw new ArgumentOutOfRangeException(nameof(tipo), tipo, "tipo sin plantilla")
        };
    }

    private static CorreoSalida Cuerpo(string asunto, string titular, string detallePrecio,
        string detallePrecioTexto, DatosCambioOferta d, string detalle, string sitioUrl)
    {
        string tienda = d.Store ?? "la tienda";
        string imagen = d.Image is null ? "" :
            $"<img src=\"{d.Image}\" alt=\"{d.Name}\" style=\"max-width:320px;width:100%;border-radius:8px\" />";
        string botonTienda = d.Url is null ? "" :
            $"<a href=\"{d.Url}\" style=\"display:inline-block;padding:10px 18px;margin:4px;" +
            $"background:#2b2b2b;color:#fff;text-decoration:none;border-radius:6px\">Ver en {tienda}</a>";

        string html =
            $"""
            <div style="font-family:Georgia,serif;max-width:480px;margin:0 auto;color:#2b2b2b">
              <h2 style="margin-bottom:4px">{titular}</h2>
              <p style="color:#555">{detallePrecio}</p>
              {imagen}
              <p style="margin-top:16px">
                <a href="{detalle}" style="display:inline-block;padding:10px 18px;margin:4px;
                   background:#c0392b;color:#fff;text-decoration:none;border-radius:6px">Ver el detalle</a>
                {botonTienda}
              </p>
              <p style="color:#999;font-size:12px;margin-top:24px">
                Recibes esto porque marcaste esta prenda como deseada en Cacha el Precio.
              </p>
            </div>
            """;

        string texto =
            $"""
            {titular}

            {detallePrecioTexto}

            Ver el detalle: {detalle}
            {(d.Url is null ? "" : $"Ver en {tienda}: {d.Url}")}

            Recibes esto porque marcaste esta prenda como deseada en Cacha el Precio.
            """;

        return new CorreoSalida("", asunto, html, texto);
    }

    private static string Pesos(int? valor) =>
        valor is null ? "—" : "$" + valor.Value.ToString("N0", Clp);
}
