namespace GameStore.Api.AI.DTOs;

public class OllamaOptions
{
    public const string SectionName = "Ollama";

    public string BaseUrl { get; set; } = "http://localhost:11434";

    public string Model { get; set; } = "qwen2.5:7b"; // qwen2.5:7b mid, gemma4:e4b mid, qwen3:8b 0, qwen2.5:3b bad

    /// <summary>How long Ollama keeps the model in memory after a request (e.g. "30m").</summary>
    public string KeepAlive { get; set; } = "30m";

    /// <summary>Context window in tokens. Must fit the prompt plus the output;
    /// a large CSV needs a bigger value.</summary>
    public int NumCtx { get; set; } = 4096;

    /// <summary>CPU threads to use. Leave null to let Ollama decide.</summary>
    public int? NumThread { get; set; }
    
    /// <summary>Whether to enable thinking mode.</summary>
    public bool? Think { get; set; }
}