using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.Resources;

namespace ST10389916_CLDV7112_Practicum;

/// <summary>
/// Handles programmatic sign-in to Azure (for management/automation via the ARM SDK).
///
/// Authentication strategy:
///   - If a service principal is fully configured (Azure:ClientId + Azure:TenantId +
///     Azure:ClientSecret), a <see cref="ClientSecretCredential"/> is used. This matches
///     the RealtimeProcessingApp app registration.
///   - Otherwise it falls back to <see cref="DefaultAzureCredential"/> (managed identity
///     in Azure, Visual Studio / Azure CLI login locally) - no secret required.
///
/// IMPORTANT: Never commit the client secret. Store it in user secrets locally
/// (dotnet user-secrets set "Azure:ClientSecret" "...") or an environment variable /
/// Key Vault in Azure. appsettings.json only holds the non-secret IDs.
/// </summary>
public sealed class AzureManagementService(IConfiguration configuration, ILogger<AzureManagementService> logger)
{
    private readonly TokenCredential _credential = CreateCredential(configuration, logger);

    private static TokenCredential CreateCredential(IConfiguration configuration, ILogger logger)
    {
        var tenantId = configuration["Azure:TenantId"] ?? configuration["AZURE_TENANT_ID"];
        var clientId = configuration["Azure:ClientId"] ?? configuration["AZURE_CLIENT_ID"];
        var clientSecret = configuration["Azure:ClientSecret"] ?? configuration["AZURE_CLIENT_SECRET"];

        if (!string.IsNullOrWhiteSpace(tenantId)
            && !string.IsNullOrWhiteSpace(clientId)
            && !string.IsNullOrWhiteSpace(clientSecret))
        {
            logger.LogInformation("Authenticating to Azure with service principal {ClientId}", clientId);
            return new ClientSecretCredential(tenantId, clientId, clientSecret);
        }

        logger.LogInformation("No service principal secret configured; using DefaultAzureCredential.");
        var options = new DefaultAzureCredentialOptions();
        if (!string.IsNullOrWhiteSpace(tenantId))
        {
            options.TenantId = tenantId;
        }

        return new DefaultAzureCredential(options);
    }

    /// <summary>
    /// Creates an authenticated ARM client. This is the entry point for all resource
    /// management operations (subscriptions, resource groups, Key Vault, Storage, SQL, etc.).
    /// </summary>
    public ArmClient CreateArmClient() => new(_credential);

    /// <summary>
    /// Verifies sign-in works by acquiring a token and listing the subscriptions
    /// the authenticated identity can access.
    /// </summary>
    public async Task<IReadOnlyList<string>> ListAccessibleSubscriptionsAsync(CancellationToken cancellationToken)
    {
        // Force a token request so authentication failures surface clearly.
        var token = await _credential.GetTokenAsync(
            new TokenRequestContext(["https://management.azure.com/.default"]),
            cancellationToken);
        logger.LogInformation("Acquired Azure management token, expires at {Expiry}", token.ExpiresOn);

        var client = CreateArmClient();
        var subscriptions = new List<string>();

        await foreach (SubscriptionResource subscription in client.GetSubscriptions().GetAllAsync(cancellationToken))
        {
            subscriptions.Add($"{subscription.Data.DisplayName} ({subscription.Data.SubscriptionId})");
        }

        return subscriptions;
    }
}
