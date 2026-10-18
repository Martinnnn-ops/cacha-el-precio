namespace Product_Service.Mensajeria;

// Un mensaje recibido de la cola, con lo justo para procesarlo y confirmarlo.
public sealed record MensajeCola(
    string MessageId,
    string Cuerpo,
    string ReceiptHandle,
    int ApproximateReceiveCount);

// El negocio habla con esta interfaz, no con el SDK de SQS (misma forma que en
// notification-service). Migrar a otra cola seria otra implementacion, sin
// tocar el consumidor ni el procesamiento de la oferta.
public interface IColaMensajes
{
    // false cuando la URL de la cola viene vacia: el consumidor no arranca y el
    // resto del servicio sigue funcionando (se puede programar sin AWS).
    bool Habilitada { get; }

    // Long polling: espera hasta 20 s por mensajes.
    Task<IReadOnlyList<MensajeCola>> RecibirAsync(CancellationToken ct);

    // El ACK: saca el mensaje de la cola. Solo despues de terminar el trabajo.
    Task BorrarAsync(MensajeCola mensaje, CancellationToken ct);

    // Error definitivo: copia el mensaje a la DLQ con atributos de diagnostico y
    // lo borra de la cola original, sin gastar los 5 reintentos.
    Task EnviarADlqAsync(MensajeCola mensaje, IReadOnlyDictionary<string, string> diagnostico,
        CancellationToken ct);
}
