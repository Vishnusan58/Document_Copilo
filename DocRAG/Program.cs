using DocRAG.Agents;
using DocRAG.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;

// Set up configuration
var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json")
    .Build();

// Get configuration values
var geminiApiKey = configuration["GeminiApiKey"] 
    ?? throw new Exception("Please set the GeminiApiKey in appsettings.json");
var storageConnectionString = configuration["AzureStorage:ConnectionString"] 
    ?? throw new Exception("Please set the Azure Storage connection string in appsettings.json");
var storageContainerName = configuration["AzureStorage:ContainerName"] 
    ?? throw new Exception("Please set the Azure Storage container name in appsettings.json");
var searchEndpoint = configuration["AzureSearch:Endpoint"] 
    ?? throw new Exception("Please set the Azure Search endpoint in appsettings.json");
var searchApiKey = configuration["AzureSearch:ApiKey"] 
    ?? throw new Exception("Please set the Azure Search API key in appsettings.json");
var searchIndexName = configuration["AzureSearch:IndexName"] 
    ?? throw new Exception("Please set the Azure Search index name in appsettings.json");

// Create services
var geminiService = new GeminiService(geminiApiKey);
var azureStorage = new AzureStorageService(storageConnectionString, storageContainerName);
var azureSearch = new AzureSearchService(searchEndpoint, searchApiKey, searchIndexName);

// Recreate the search index to ensure proper schema
Console.WriteLine("Recreating search index...");
await azureSearch.RecreateIndex();

var documentLoader = new DocumentLoaderService(azureStorage, azureSearch);

// Create kernel and register the summarizer agent
var kernel = Kernel.CreateBuilder().Build();
var summarizerAgent = kernel.CreateFunctionFromMethod(
    new SummarizerAgent(geminiService).Summarize
);

// Process and store document
Console.WriteLine("\nLoading and chunking document...");
var chunks = await documentLoader.LoadAndChunkDocument("C:\\Users\\vishn\\RiderProjects\\DocRAG\\DocRAG\\sample.txt");
Console.WriteLine($"Document split into {chunks.Count} chunks and stored in Azure");

// Demonstrate search capability
Console.WriteLine("\nSearching for content about 'artificial intelligence'...");
var searchResults = await documentLoader.SearchDocuments("artificial intelligence");

foreach (var chunk in searchResults)
{
    Console.WriteLine($"\nFound relevant chunk from {chunk.SourceFileName}:");
    Console.WriteLine("-------------------");
    Console.WriteLine(chunk.Content.Substring(0, Math.Min(100, chunk.Content.Length)) + "...");
    
    Console.WriteLine("\nGenerating summary for this chunk...");
    var summary = await kernel.InvokeAsync(summarizerAgent, new() { ["text"] = chunk.Content });
    Console.WriteLine("Summary: " + summary);
    Console.WriteLine("-------------------");
}
