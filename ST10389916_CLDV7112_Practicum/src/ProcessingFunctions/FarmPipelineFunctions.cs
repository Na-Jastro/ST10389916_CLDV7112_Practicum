using System.Data;
using System.Text.Json;
using Azure;
using Azure.Data.Tables;
using Azure.Storage.Queues;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ProcessingFunctions;

internal static class JsonDefaults
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true
    };
}

public sealed class BlobUploadAuditFunction(IConfiguration configuration, ILogger<BlobUploadAuditFunction> logger)
{
    [Function("BlobUploadAudit")]
    public async Task RunAsync(
        [BlobTrigger("drone-images/{name}", Connection = "StorageConnection")] Stream blob,
        string name,
        CancellationToken cancellationToken)
    {
        var connectionString = configuration["StorageConnection"];
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            logger.LogWarning("StorageConnection is not configured.");
            return;
        }

        var tableName = configuration["AuditTableName"] ?? "UploadAudit";
        var tableClient = new TableClient(connectionString, tableName);
        await tableClient.CreateIfNotExistsAsync(cancellationToken);

        var entity = new TableEntity("BlobUpload", Guid.NewGuid().ToString("N"))
        {
            ["BlobName"] = name,
            ["BlobSizeBytes"] = blob.Length,
            ["Status"] = "Uploaded",
            ["LoggedAtUtc"] = DateTimeOffset.UtcNow
        };

        await tableClient.AddEntityAsync(entity, cancellationToken);
        logger.LogInformation("Blob upload audit written for {BlobName}", name);
    }
}

public sealed class TelemetryGetFunction(IConfiguration configuration)
{
    [Function("TelemetryGet")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "telemetry/latest")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var response = request.CreateResponse();
        var connectionString = configuration["StorageConnection"];
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            response.StatusCode = System.Net.HttpStatusCode.InternalServerError;
            await response.WriteStringAsync("StorageConnection not configured.", cancellationToken);
            return response;
        }

        var tableName = configuration["TelemetryTableName"] ?? "TelemetryData";
        var tableClient = new TableClient(connectionString, tableName);
        await tableClient.CreateIfNotExistsAsync(cancellationToken);

        var list = new List<TelemetryDto>();
        await foreach (var row in tableClient.QueryAsync<TableEntity>(cancellationToken: cancellationToken))
        {
            list.Add(new TelemetryDto(
                row.PartitionKey,
                row.RowKey,
                row.TryGetValue("DeviceId", out var deviceId) ? deviceId?.ToString() ?? string.Empty : string.Empty,
                row.TryGetValue("Metric", out var metric) ? metric?.ToString() ?? string.Empty : string.Empty,
                row.TryGetValue("Value", out var value) ? value?.ToString() ?? string.Empty : string.Empty,
                row.Timestamp ?? DateTimeOffset.UtcNow));
        }

        var latest = list
            .OrderByDescending(x => x.Timestamp)
            .Take(50)
            .ToArray();

        response.StatusCode = System.Net.HttpStatusCode.OK;
        await response.WriteAsJsonAsync(latest, cancellationToken);
        return response;
    }
}

public sealed class IrrigationCommandPostFunction(IConfiguration configuration)
{
    [Function("IrrigationCommandPost")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "irrigation/command")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var response = request.CreateResponse();
        var body = await JsonSerializer.DeserializeAsync<IrrigationCommand>(request.Body, JsonDefaults.Options, cancellationToken);
        if (body is null || string.IsNullOrWhiteSpace(body.DeviceId) || string.IsNullOrWhiteSpace(body.FieldId))
        {
            response.StatusCode = System.Net.HttpStatusCode.BadRequest;
            await response.WriteStringAsync("DeviceId and FieldId are required.", cancellationToken);
            return response;
        }

        var storageConnection = configuration["IrrigationQueueConnection"] ?? configuration["StorageConnection"];
        if (string.IsNullOrWhiteSpace(storageConnection))
        {
            response.StatusCode = System.Net.HttpStatusCode.InternalServerError;
            await response.WriteStringAsync("IrrigationQueueConnection not configured.", cancellationToken);
            return response;
        }

        var queueName = configuration["IrrigationQueueName"] ?? "irrigation-commands";
        var queue = new QueueClient(storageConnection, queueName);
        await queue.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

        var payload = JsonSerializer.Serialize(new IrrigationCommand(
            body.DeviceId,
            body.FieldId,
            string.IsNullOrWhiteSpace(body.Action) ? "TriggerIrrigation" : body.Action));

        await queue.SendMessageAsync(payload, cancellationToken);

        var telemetryTableName = configuration["TelemetryTableName"] ?? "TelemetryData";
        var telemetryTable = new TableClient(storageConnection, telemetryTableName);
        await telemetryTable.CreateIfNotExistsAsync(cancellationToken);
        var telemetryEntity = new TableEntity("Irrigation", DateTimeOffset.UtcNow.Ticks.ToString())
        {
            ["DeviceId"] = body.DeviceId,
            ["Metric"] = "IrrigationCommand",
            ["Value"] = body.Action ?? "TriggerIrrigation"
        };
        await telemetryTable.UpsertEntityAsync(telemetryEntity, TableUpdateMode.Replace, cancellationToken);

        response.StatusCode = System.Net.HttpStatusCode.Accepted;
        await response.WriteAsJsonAsync(new { queued = true, body.DeviceId, body.FieldId }, cancellationToken);
        return response;
    }
}

public sealed class IrrigationCommandQueueFunction(IConfiguration configuration, ILogger<IrrigationCommandQueueFunction> logger)
{
    [Function("IrrigationCommandQueue")]
    public async Task RunAsync(
        [QueueTrigger("irrigation-commands", Connection = "IrrigationQueueConnection")] string queueMessage,
        CancellationToken cancellationToken)
    {
        var command = JsonSerializer.Deserialize<IrrigationCommand>(queueMessage, JsonDefaults.Options);
        if (command is null)
        {
            logger.LogWarning("Queue message could not be deserialized.");
            return;
        }

        var sqlConnectionString = configuration["SqlConnectionString"];
        if (string.IsNullOrWhiteSpace(sqlConnectionString))
        {
            logger.LogWarning("SqlConnectionString is not configured.");
            return;
        }

        await using var connection = new SqlConnection(sqlConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using var commandSql = connection.CreateCommand();
        commandSql.CommandText = @"
MERGE DeviceRegistry AS target
USING (SELECT @deviceId AS DeviceId) AS source
ON target.DeviceId = source.DeviceId
WHEN MATCHED THEN
    UPDATE SET FieldId = @fieldId,
               OperationalStatus = @status,
               LastCommand = @lastCommand,
               UpdatedAt = SYSUTCDATETIME()
WHEN NOT MATCHED THEN
    INSERT (DeviceId, FieldId, OperationalStatus, LastCommand, UpdatedAt)
    VALUES (@deviceId, @fieldId, @status, @lastCommand, SYSUTCDATETIME());";

        commandSql.Parameters.Add(new SqlParameter("@deviceId", SqlDbType.NVarChar, 100) { Value = command.DeviceId });
        commandSql.Parameters.Add(new SqlParameter("@fieldId", SqlDbType.NVarChar, 100) { Value = command.FieldId });
        commandSql.Parameters.Add(new SqlParameter("@status", SqlDbType.NVarChar, 50) { Value = "IrrigationTriggered" });
        commandSql.Parameters.Add(new SqlParameter("@lastCommand", SqlDbType.NVarChar, 200) { Value = command.Action ?? "TriggerIrrigation" });

        await commandSql.ExecuteNonQueryAsync(cancellationToken);
        logger.LogInformation("Device registry updated for DeviceId={DeviceId}", command.DeviceId);
    }
}

public sealed record IrrigationCommand(string DeviceId, string FieldId, string? Action);
public sealed record TelemetryDto(string PartitionKey, string RowKey, string DeviceId, string Metric, string Value, DateTimeOffset Timestamp);
