using Microsoft.AspNetCore.Mvc;
using ST10389916_CLDV7112_Practicum.Models;

namespace ST10389916_CLDV7112_Practicum.Controllers;

public sealed class DashboardController(FunctionGateway gateway) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        try
        {
            ViewBag.TelemetryJson = (await gateway.GetTelemetryAsync(cancellationToken)).ToString();
        }
        catch (Exception ex)
        {
            ViewBag.Error = ex.Message;
        }

        return View(new IrrigationViewModel());
    }

    [HttpPost]
    public async Task<IActionResult> TriggerIrrigation(IrrigationViewModel model, CancellationToken cancellationToken)
    {
        try
        {
            var result = await gateway.TriggerIrrigationAsync(new IrrigationCommand(model.DeviceId, model.FieldId, model.Action), cancellationToken);
            ViewBag.Message = result.ToString();
        }
        catch (Exception ex)
        {
            ViewBag.Error = ex.Message;
        }

        try
        {
            ViewBag.TelemetryJson = (await gateway.GetTelemetryAsync(cancellationToken)).ToString();
        }
        catch (Exception ex)
        {
            ViewBag.Error = string.IsNullOrWhiteSpace(ViewBag.Error) ? ex.Message : $"{ViewBag.Error} | {ex.Message}";
            ViewBag.TelemetryJson = "[]";
        }

        return View("Index", model);
    }
}
