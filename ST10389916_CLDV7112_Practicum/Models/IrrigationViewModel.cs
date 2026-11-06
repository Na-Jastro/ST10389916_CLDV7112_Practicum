namespace ST10389916_CLDV7112_Practicum.Models;

public sealed class IrrigationViewModel
{
    public string DeviceId { get; set; } = string.Empty;
    public string FieldId { get; set; } = string.Empty;
    public string Action { get; set; } = "TriggerIrrigation";
}
