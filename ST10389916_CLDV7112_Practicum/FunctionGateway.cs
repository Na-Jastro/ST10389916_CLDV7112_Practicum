using System.Text.Json;
using System.Net.Http.Json;

namespace ST10389916_CLDV7112_Practicum;

public sealed class FunctionGateway(HttpClient httpClient, IConfiguration configuration)
{
    private readonly string _baseUrl = configuration["Functions:BaseUrl"] ?? configuration["Functions__BaseUrl"] ?? string.Empty;
    private readonly string? _defaultKey = configuration["Functions:Key"] ?? configuration["Functions__Key"];
    private readonly string? _telemetryKey = configuration["Functions:TelemetryKey"] ?? configuration["Functions__TelemetryKey"];
    private readonly string? _irrigationKey = configuration["Functions:IrrigationKey"] ?? configuration["Functions__IrrigationKey"];

    public async Task<JsonElement> GetTelemetryAsync(CancellationToken cancellationToken)
    {
        var uri = BuildUri("/telemetry/latest", _telemetryKey ?? _defaultKey);
        var response = await httpClient.GetAsync(uri, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"TelemetryGet failed: {(int)response.StatusCode} {response.ReasonPhrase}. {body}");
        }
        return await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
    }

    public async Task<JsonElement> TriggerIrrigationAsync(IrrigationCommand command, CancellationToken cancellationToken)
    {
        var uri = BuildUri("/irrigation/command", _irrigationKey ?? _defaultKey);
        var response = await httpClient.PostAsJsonAsync(uri, command, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"IrrigationCommandPost failed: {(int)response.StatusCode} {response.ReasonPhrase}. {body}");
        }
        return await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
    }

    private string BuildUri(string route, string? key)
    {
        var baseUrl = _baseUrl.TrimEnd('/');
        var path = route.TrimStart('/');
        var uri = $"{baseUrl}/{path}";
        return string.IsNullOrWhiteSpace(key) ? uri : $"{uri}?code={key}";
    }
}

public sealed record IrrigationCommand(string DeviceId, string FieldId, string Action);
