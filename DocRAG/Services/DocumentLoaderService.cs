using System.Text;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Listener;
using DocRAG.Models;

namespace DocRAG.Services;

public class DocumentLoaderService
{
    private readonly int _chunkSize;
    private readonly int _chunkOverlap;
    private readonly AzureStorageService _storageService;
    private readonly AzureSearchService _searchService;

    public DocumentLoaderService(
        AzureStorageService storageService,
        AzureSearchService searchService,
        int chunkSize = 500,
        int chunkOverlap = 20)
    {
        _storageService = storageService;
        _searchService = searchService;
        _chunkSize = chunkSize;
        _chunkOverlap = chunkOverlap;
    }

    public async Task<List<DocumentChunk>> LoadAndChunkDocument(string filePath)
    {
        string text = await LoadDocument(filePath);
        var chunks = ChunkText(text, Path.GetFileName(filePath));
        
        // Store chunks in Azure and index them
        foreach (var chunk in chunks)
        {
            var blobUri = await _storageService.UploadDocumentChunk(chunk);
            await _searchService.IndexDocumentChunk(chunk, blobUri);
        }
        
        return chunks;
    }

    private async Task<string> LoadDocument(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
            throw new ArgumentException("File path cannot be empty", nameof(filePath));

        string extension = Path.GetExtension(filePath).ToLower();
        return extension switch
        {
            ".pdf" => await LoadPdfDocument(filePath),
            ".txt" => await File.ReadAllTextAsync(filePath),
            _ => throw new NotSupportedException($"File type {extension} is not supported")
        };
    }

    private async Task<string> LoadPdfDocument(string filePath)
    {
        using var pdfReader = new PdfReader(filePath);
        using var pdfDoc = new PdfDocument(pdfReader);
        var text = new StringBuilder();

        for (int i = 1; i <= pdfDoc.GetNumberOfPages(); i++)
        {
            var page = pdfDoc.GetPage(i);
            text.AppendLine(PdfTextExtractor.GetTextFromPage(page));
        }

        return text.ToString();
    }

    private List<DocumentChunk> ChunkText(string text, string sourceFileName)
    {
        var words = text.Split(new[] { ' ', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        var chunks = new List<DocumentChunk>();
        
        for (int i = 0; i < words.Length; i += _chunkSize - _chunkOverlap)
        {
            var chunkWords = words.Skip(i).Take(_chunkSize).ToList();
            if (chunkWords.Count == 0) continue;

            var chunk = new DocumentChunk
            {
                Content = string.Join(" ", chunkWords),
                StartPosition = i,
                EndPosition = i + chunkWords.Count,
                SourceFileName = sourceFileName
            };

            chunks.Add(chunk);
        }

        return chunks;
    }

    public async Task<List<DocumentChunk>> SearchDocuments(string query)
    {
        return await _searchService.SearchDocuments(query);
    }
}
