namespace DocRAG.Models;

public class DocumentChunk
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Content { get; set; } = string.Empty;
    public int StartPosition { get; set; }
    public int EndPosition { get; set; }
    public string SourceFileName { get; set; } = string.Empty;
}
