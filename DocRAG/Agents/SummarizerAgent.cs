using DocRAG.Services;
using Microsoft.SemanticKernel;

namespace DocRAG.Agents;

public class SummarizerAgent
{
    private readonly GeminiService _geminiService;

    public SummarizerAgent(GeminiService geminiService)
    {
        _geminiService = geminiService;
    }

    [KernelFunction]
    public async Task<string> Summarize(string text)
    {
        return await _geminiService.GetSummary(text);
    }
}
