namespace Product_Service.Models.Entities;

// Identidad del propietario suministrada exclusivamente por el BFF autenticado.
public sealed class PersonalItem
{
    public string Owner { get; set; } = "";
    public string Kind { get; set; } = "";
    public string Key { get; set; } = "";
    public string Payload { get; set; } = "{}";
    public DateTimeOffset UpdatedAt { get; set; }
}
