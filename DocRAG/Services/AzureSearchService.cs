using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using Azure.Search.Documents.Models;
using DocRAG.Models;

namespace DocRAG.Services;

public class AzureSearchService
{
    private readonly SearchIndexClient _indexClient;
    private readonly SearchClient _searchClient;
    private readonly string _indexName;

    public AzureSearchService(string endpoint, string apiKey, string indexName)
    {
        _indexName = indexName;
        var credential = new AzureKeyCredential(apiKey);
        var endpointUri = new Uri(endpoint);
        _indexClient = new SearchIndexClient(endpointUri, credential);
        _searchClient = new SearchClient(endpointUri, indexName, credential);

        EnsureIndexExists().GetAwaiter().GetResult();
    }

    private async Task EnsureIndexExists()
    {
        try
        {
            var indexExists = false;
            await foreach (var page in _indexClient.GetIndexesAsync().AsPages())
            {
                if (page.Values.Any(i => i.Name == _indexName))
                {
                    indexExists = true;
                    break;
                }
            }

            if (!indexExists)
            {
                var index = new SearchIndex(_indexName)
                {
                    Fields = new List<SearchField>
                    {
                        new SimpleField("id", SearchFieldDataType.String) { IsKey = true },
                        new SearchableField("sourceFile") { IsFilterable = true },
                        new SearchableField("content") { IsFilterable = true, AnalyzerName = LexicalAnalyzerName.StandardLucene },
                        new SimpleField("blobUri", SearchFieldDataType.String)
                    }
                };
                await _indexClient.CreateIndexAsync(index);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error creating index: {ex.Message}");
            throw;
        }
    }

    public async Task IndexDocumentChunk(DocumentChunk chunk, string blobUri)
    {
        var searchableChunk = new SearchableDocumentChunk
        {
            Id = chunk.Id,
            Content = chunk.Content,
            SourceFile = chunk.SourceFileName,
            BlobUri = blobUri
        };

        await _searchClient.IndexDocumentsAsync(IndexDocumentsBatch.Upload(new[] { searchableChunk }));
    }

    public async Task<List<DocumentChunk>> SearchDocuments(string searchQuery, int top = 3)
    {
        var options = new SearchOptions
        {
            Size = top,
            IncludeTotalCount = true,
            OrderBy = { "search.score() desc" }
        };

        // SearchResults<T> can be awaited and enumerated directly.
        var results = await _searchClient.SearchAsync<SearchableDocumentChunk>(searchQuery, options);
        var chunks = new List<DocumentChunk>();

        // Simplified loop
        await foreach (var result in results.Value.GetResultsAsync())
        {
            chunks.Add(new DocumentChunk
            {
                Id = result.Document.Id,
                Content = result.Document.Content,
                SourceFileName = result.Document.SourceFile
            });
        }

        return chunks;
    }

    public async Task RecreateIndex()
    {
        try
        {
            // Delete existing index if it exists
            await _indexClient.DeleteIndexAsync(_indexName);
        }
        catch
        {
            // Index might not exist, that's okay
        }

        var searchFields = new List<SearchField>
        {
            new SimpleField("id", SearchFieldDataType.String) { IsKey = true },
            new SearchableField("sourceFile") { IsFilterable = true },
            new SearchableField("content") { IsFilterable = true, AnalyzerName = LexicalAnalyzerName.StandardLucene },
            new SimpleField("blobUri", SearchFieldDataType.String)
        };

        var index = new SearchIndex(_indexName, searchFields);
        await _indexClient.CreateIndexAsync(index);
    }
}

[Serializable]
public class SearchableDocumentChunk
{
    [SimpleField(IsKey = true, IsSortable = true)]
    public string Id { get; set; } = "";

    [SearchableField]
    public string SourceFile { get; set; } = "";

    [SearchableField(AnalyzerName = "standard.lucene")]
    public string Content { get; set; } = "";

    [SimpleField]
    public string BlobUri { get; set; } = "";
}