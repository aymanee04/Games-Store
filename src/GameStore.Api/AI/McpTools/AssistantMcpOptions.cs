namespace GameStore.Api.AI.McpTools;

/// Settings for the assistant's MCP client (section "AssistantMcp"). When enabled, the assistant
/// reads its catalog tools from an MCP server instead of calling its own plugin.
public sealed class AssistantMcpOptions
{
    public const string SectionName = "AssistantMcp";
    
    public bool Enabled { get; set; }

    /// The MCP endpoint, (this app's own /mcp).
    public string Endpoint { get; set; } = "http://localhost:5080/mcp";

    /// An MCP API key. Use a READ-ONLY key: the assistant must never be able to write over MCP.
    public string ApiKey { get; set; } = "";

    /// Only these tools are ever handed to the model, whatever the server offers. A second layer
    /// next to the read-only key: even if the key were too powerful, the write tools stay unused.
    public string[] AllowedTools { get; set; } = ["search_games", "get_game"];
}