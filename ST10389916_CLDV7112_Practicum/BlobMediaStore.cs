using Azure.Storage.Blobs;

namespace ST10389916_CLDV7112_Practicum;

public sealed class BlobMediaStore(IConfiguration configuration)
{
    public async Task<Uri> UploadAsync(string fileName, Stream content, CancellationToken cancellationToken)
    {
        var connectionString = configuration["Storage:ConnectionString"] ?? configuration["Storage__ConnectionString"];
        var containerName = configuration["Storage:BlobContainer"] ?? configuration["Storage__BlobContainer"] ?? "drone-images";
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        var container = new BlobContainerClient(connectionString, containerName);
        await container.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

        var blob = container.GetBlobClient($"{Guid.NewGuid()}-{Path.GetFileName(fileName)}");
        await blob.UploadAsync(content, overwrite: false, cancellationToken);
        return blob.Uri;
    }
}
