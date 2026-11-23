namespace GameStore.Api.AI.McpTools;

public static class AssistantMcpExtensions
{
    /// <summary>Registers the assistant's MCP client. It does nothing until AssistantMcp:Enabled is true.</summary>
    public static IServiceCollection AddAssistantMcpClient(this IServiceCollection services)
    {
        services.AddOptions<AssistantMcpOptions>().BindConfiguration("AssistantMcp");
        services.AddSingleton<IMcpToolProvider, McpToolProvider>();

        return services;
    }
}