using CachaElPrecio.Gateway.Mensajeria;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging.Abstractions;

namespace CachaElPrecio.Gateway.Tests;

/// <summary>
/// Las reglas de la administración sobre la mensajería en memoria, con la
/// topología real del repositorio. Cubre crear, consultar, eliminar, la
/// protección de lo declarado, la comparación con topologia.json y el
/// reproceso de una DLQ.
/// </summary>
public class ServicioAdministracionTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;
    private readonly AdministradorMensajeriaMemoria memoria = new();
    private readonly ServicioAdministracion servicio;

    public ServicioAdministracionTests()
    {
        servicio = new ServicioAdministracion(memoria, CargarTopologia(), NullLogger<ServicioAdministracion>.Instance);
    }

    // La misma topologia que se despliega: si alguien la rompe, fallan estos tests.
    private static TopologiaDeclarada CargarTopologia()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "infra", "mensajeria", "topologia.json")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return TopologiaDeclarada.Cargar(Path.Combine(dir!.FullName, "infra", "mensajeria", "topologia.json"));
    }

    // ── Topologia ────────────────────────────────────────────────────────

    [Fact]
    public async Task Sin_nada_creado_la_topologia_no_esta_sincronizada()
    {
        var estado = await servicio.CompararTopologiaAsync(Ct);

        Assert.False(estado.Sincronizada);
        Assert.All(estado.Diferencias, d => Assert.Equal("falta", d.Estado));
        Assert.Contains(estado.Diferencias, d => d.Nombre == "cep-ofertas-crudas");
    }

    [Fact]
    public async Task Aplicar_la_topologia_crea_todo_y_queda_sincronizada()
    {
        var acciones = await servicio.AplicarTopologiaAsync(Ct);

        Assert.Contains(acciones, a => a is { Nombre: "cep-ofertas-crudas-dlq", Resultado: "creada" });
        Assert.Contains(acciones, a => a is { Recurso: "topico", Nombre: "cep-cambios-oferta", Resultado: "creado" });
        Assert.Contains(acciones, a => a is { Nombre: "cep-cambios-oferta -> cep-notificaciones", Resultado: "creada" });

        var estado = await servicio.CompararTopologiaAsync(Ct);
        Assert.True(estado.Sincronizada);
        Assert.Empty(estado.Diferencias);

        var cola = await servicio.ObtenerColaAsync("cep-ofertas-crudas", Ct);
        Assert.Equal(new ReglaDlq("cep-ofertas-crudas-dlq", 5), cola.Dlq);
        Assert.True(cola.DeclaradaEnTopologia);
        Assert.Equal(new[] { "cep-ofertas-crudas" }, (await servicio.ObtenerColaAsync("cep-ofertas-crudas-dlq", Ct)).EsDlqDe);
    }

    [Fact]
    public async Task Aplicar_dos_veces_no_cambia_nada_la_segunda()
    {
        await servicio.AplicarTopologiaAsync(Ct);
        var segunda = await servicio.AplicarTopologiaAsync(Ct);
        Assert.All(segunda, a => Assert.Equal("sin cambios", a.Resultado));
    }

    [Fact]
    public async Task Un_cambio_a_mano_aparece_como_diferencia_y_aplicar_lo_corrige()
    {
        await servicio.AplicarTopologiaAsync(Ct);
        await memoria.ActualizarColaAsync(new ConfiguracionCola("cep-notificaciones", 5, 345600, 20,
            new ReglaDlq("cep-notificaciones-dlq", 5)), Ct);

        var estado = await servicio.CompararTopologiaAsync(Ct);
        var diferencia = Assert.Single(estado.Diferencias);
        Assert.Equal("distinta", diferencia.Estado);
        Assert.Contains("visibilidad 5 s", diferencia.Detalle);

        var acciones = await servicio.AplicarTopologiaAsync(Ct);
        Assert.Contains(acciones, a => a is { Nombre: "cep-notificaciones", Resultado: "actualizada" });
        Assert.True((await servicio.CompararTopologiaAsync(Ct)).Sincronizada);
    }

    // ── Colas ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Crear_una_cola_con_su_dlq_y_consultarla()
    {
        await servicio.CrearColaAsync(new CrearColaSolicitud("cep-prueba-dlq", null, 1209600, 0, null, null), Ct);
        var cola = await servicio.CrearColaAsync(new CrearColaSolicitud("cep-prueba", 45, null, null, "cep-prueba-dlq", 3), Ct);

        Assert.Equal(45, cola.VisibilidadSegundos);
        Assert.Equal(ServicioAdministracion.RetencionPorDefecto, cola.RetencionSegundos);
        Assert.Equal(new ReglaDlq("cep-prueba-dlq", 3), cola.Dlq);
        Assert.False(cola.DeclaradaEnTopologia);
        Assert.Contains(await servicio.ListarColasAsync(Ct), c => c.Nombre == "cep-prueba");
    }

    [Fact]
    public async Task Crear_una_cola_repetida_da_conflicto()
    {
        await servicio.CrearColaAsync(new CrearColaSolicitud("cep-prueba", null, null, null, null, null), Ct);
        await Assert.ThrowsAsync<ConflictoException>(() =>
            servicio.CrearColaAsync(new CrearColaSolicitud("cep-prueba", null, null, null, null, null), Ct));
    }

    [Fact]
    public async Task Crear_una_cola_con_una_dlq_que_no_existe_marca_el_campo()
    {
        var e = await Assert.ThrowsAsync<SolicitudInvalidaException>(() =>
            servicio.CrearColaAsync(new CrearColaSolicitud("cep-prueba", null, null, null, "cep-no-existe", 3), Ct));
        Assert.Contains("no existe", Assert.Single(e.Errores["dlq"]));
    }

    [Fact]
    public async Task Crear_una_cola_invalida_no_toca_la_mensajeria()
    {
        await Assert.ThrowsAsync<SolicitudInvalidaException>(() =>
            servicio.CrearColaAsync(new CrearColaSolicitud("Mala", null, null, null, null, null), Ct));
        Assert.Empty(await memoria.ListarColasAsync(Ct));
    }

    [Fact]
    public async Task Una_cola_declarada_solo_se_borra_forzando()
    {
        await servicio.AplicarTopologiaAsync(Ct);

        var e = await Assert.ThrowsAsync<ConflictoException>(() =>
            servicio.EliminarColaAsync("cep-auditoria-precios", forzar: false, Ct));
        Assert.Contains("recibe de", e.Message);   // primero hay que quitarle la suscripcion

        await servicio.EliminarSuscripcionAsync("cep-cambios-oferta", "cep-auditoria-precios", forzar: true, Ct);
        e = await Assert.ThrowsAsync<ConflictoException>(() =>
            servicio.EliminarColaAsync("cep-auditoria-precios", forzar: false, Ct));
        Assert.Contains("forzar=true", e.Message);

        await servicio.EliminarColaAsync("cep-auditoria-precios", forzar: true, Ct);
        Assert.DoesNotContain(await servicio.ListarColasAsync(Ct), c => c.Nombre == "cep-auditoria-precios");
    }

    [Fact]
    public async Task Una_dlq_en_uso_no_se_borra_ni_forzando()
    {
        await servicio.AplicarTopologiaAsync(Ct);
        var e = await Assert.ThrowsAsync<ConflictoException>(() =>
            servicio.EliminarColaAsync("cep-ofertas-crudas-dlq", forzar: true, Ct));
        Assert.Contains("es la DLQ de cep-ofertas-crudas", e.Message);
    }

    [Fact]
    public async Task Borrar_una_cola_que_no_existe_da_404() =>
        await Assert.ThrowsAsync<RecursoNoEncontradoException>(() => servicio.EliminarColaAsync("cep-fantasma", false, Ct));

    [Fact]
    public async Task Una_cola_propia_se_borra_sin_forzar()
    {
        await servicio.CrearColaAsync(new CrearColaSolicitud("cep-prueba", null, null, null, null, null), Ct);
        await servicio.EliminarColaAsync("cep-prueba", forzar: false, Ct);
        Assert.Empty(await servicio.ListarColasAsync(Ct));
    }

    // ── Topicos y suscripciones ──────────────────────────────────────────

    [Fact]
    public async Task Crear_topico_suscribir_con_filtro_y_cambiar_el_filtro()
    {
        await servicio.CrearTopicoAsync(new CrearTopicoSolicitud("cep-prueba-topico"), Ct);
        await servicio.CrearColaAsync(new CrearColaSolicitud("cep-prueba", null, null, null, null, null), Ct);

        var filtro = JsonNode.Parse("""{"type": ["oferta.agotada"]}""");
        var sub = await servicio.CrearSuscripcionAsync(
            new CrearSuscripcionSolicitud("cep-prueba-topico", "cep-prueba", filtro, null), Ct);
        Assert.True(sub.EntregaCruda);
        Assert.True(JsonNode.DeepEquals(filtro, sub.Filtro));

        var cambiada = await servicio.ActualizarSuscripcionAsync("cep-prueba-topico", "cep-prueba",
            new CrearSuscripcionSolicitud(null, null, null, false), Ct);
        Assert.Null(cambiada.Filtro);
        Assert.False(cambiada.EntregaCruda);

        Assert.Single((await servicio.ListarTopicosAsync(Ct)).Single().Suscripciones);
    }

    [Fact]
    public async Task Suscribir_a_un_topico_y_cola_que_no_existen_marca_los_dos_campos()
    {
        var e = await Assert.ThrowsAsync<SolicitudInvalidaException>(() => servicio.CrearSuscripcionAsync(
            new CrearSuscripcionSolicitud("cep-nada", "cep-tampoco", null, null), Ct));
        Assert.True(e.Errores.ContainsKey("topico"));
        Assert.True(e.Errores.ContainsKey("cola"));
    }

    [Fact]
    public async Task Una_dlq_no_se_puede_suscribir_a_un_topico()
    {
        await servicio.AplicarTopologiaAsync(Ct);
        var e = await Assert.ThrowsAsync<SolicitudInvalidaException>(() => servicio.CrearSuscripcionAsync(
            new CrearSuscripcionSolicitud("cep-cambios-oferta", "cep-notificaciones-dlq", null, null), Ct));
        Assert.Contains("es una DLQ", Assert.Single(e.Errores["cola"]));
    }

    [Fact]
    public async Task Suscripcion_repetida_da_conflicto()
    {
        await servicio.AplicarTopologiaAsync(Ct);
        await Assert.ThrowsAsync<ConflictoException>(() => servicio.CrearSuscripcionAsync(
            new CrearSuscripcionSolicitud("cep-cambios-oferta", "cep-notificaciones", null, null), Ct));
    }

    [Fact]
    public async Task Un_topico_con_suscripciones_no_se_borra()
    {
        await servicio.AplicarTopologiaAsync(Ct);
        var e = await Assert.ThrowsAsync<ConflictoException>(() =>
            servicio.EliminarTopicoAsync("cep-cambios-oferta", forzar: true, Ct));
        Assert.Contains("suscripcion", e.Message);
    }

    // ── DLQ ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Ver_y_reprocesar_los_mensajes_de_una_dlq()
    {
        await servicio.AplicarTopologiaAsync(Ct);
        memoria.Sembrar("cep-ofertas-crudas-dlq", "{}", new Dictionary<string, string> { ["error-type"] = "definitivo" });

        var mensaje = Assert.Single(await servicio.VerMensajesAsync("cep-ofertas-crudas-dlq", 10, Ct));
        Assert.Equal("definitivo", mensaje.Diagnostico["error-type"]);

        var resultado = await servicio.ReprocesarDlqAsync("cep-ofertas-crudas-dlq", destino: null, Ct);
        Assert.Equal("cep-ofertas-crudas", resultado.Destino);   // la unica cola que la usa
        Assert.Equal(1, resultado.MensajesMovidos);
        Assert.Empty(await servicio.VerMensajesAsync("cep-ofertas-crudas-dlq", 10, Ct));
        Assert.Equal(1, (await servicio.ObtenerColaAsync("cep-ofertas-crudas", Ct)).MensajesVisibles);
    }

    [Fact]
    public async Task Reprocesar_hacia_una_cola_que_no_usa_esa_dlq_se_rechaza()
    {
        await servicio.AplicarTopologiaAsync(Ct);
        var e = await Assert.ThrowsAsync<SolicitudInvalidaException>(() =>
            servicio.ReprocesarDlqAsync("cep-ofertas-crudas-dlq", "cep-notificaciones", Ct));
        Assert.True(e.Errores.ContainsKey("destino"));
    }

    [Fact]
    public async Task Una_cola_con_dlq_no_se_mira_porque_sumaria_intentos()
    {
        await servicio.AplicarTopologiaAsync(Ct);
        var e = await Assert.ThrowsAsync<ConflictoException>(() => servicio.VerMensajesAsync("cep-ofertas-crudas", 10, Ct));
        Assert.Contains("cuenta como una recepcion", e.Message);
    }

    [Fact]
    public async Task La_auditoria_se_puede_mirar_porque_no_tiene_dlq()
    {
        await servicio.AplicarTopologiaAsync(Ct);
        memoria.Sembrar("cep-auditoria-precios", """{"type":"precio.bajo"}""", new Dictionary<string, string>());
        Assert.Single(await servicio.VerMensajesAsync("cep-auditoria-precios", 10, Ct));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(11)]
    public async Task Leer_de_a_mas_de_10_o_menos_de_1_se_rechaza(int maximo) =>
        await Assert.ThrowsAsync<SolicitudInvalidaException>(() =>
            servicio.VerMensajesAsync("cep-ofertas-crudas-dlq", maximo, Ct));
}
