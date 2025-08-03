using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using DocRAG.Models;

namespace DocRAG.Services;

public class AzureStorageService
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly string _containerName;

    public AzureStorageService(string connectionString, string containerName)
    {
        _blobServiceClient = new BlobServiceClient(connectionString);
        _containerName = containerName;
        EnsureContainerExists().GetAwaiter().GetResult();
    }

    private async Task EnsureContainerExists()
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
        await containerClient.CreateIfNotExistsAsync();
    }

    public async Task<string> UploadDocumentChunk(DocumentChunk chunk)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
        var blobName = $"{chunk.SourceFileName}/{chunk.Id}.txt";
        var blobClient = containerClient.GetBlobClient(blobName);

        using var stream = new MemoryStream();
        using var writer = new StreamWriter(stream);
        await writer.WriteAsync(chunk.Content);
        await writer.FlushAsync();
        stream.Position = 0;

        await blobClient.UploadAsync(stream, new BlobHttpHeaders { ContentType = "text/plain" });
        return blobClient.Uri.ToString();
    }

    public async Task<DocumentChunk> DownloadDocumentChunk(string fileName, string chunkId)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
        var blobName = $"{fileName}/{chunkId}.txt";
        var blobClient = containerClient.GetBlobClient(blobName);

        if (!await blobClient.ExistsAsync())
            throw new FileNotFoundException($"Chunk {chunkId} not found for document {fileName}");

        BlobDownloadInfo download = await blobClient.DownloadAsync();
        using var reader = new StreamReader(download.Content);
        string content = await reader.ReadToEndAsync();

        return new DocumentChunk
        {
            Id = chunkId,
            Content = content,
            SourceFileName = fileName
        };
    }

    public async Task<List<string>> ListDocuments()
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
        var documents = new HashSet<string>();

        await foreach (var blob in containerClient.GetBlobsAsync())
        {
            var parts = blob.Name.Split('/');
            if (parts.Length > 0)
            {
                documents.Add(parts[0]);
            }
        }

        return documents.ToList();
    }
}
