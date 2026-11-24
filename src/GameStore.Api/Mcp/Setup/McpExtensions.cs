using GameStore.Api.AI.McpTools;
using GameStore.Api.Mcp.Audit;
using GameStore.Api.Mcp.Auth;
using GameStore.Api.Mcp.Prompts;
using GameStore.Api.Mcp.Ressources;
using GameStore.Api.Mcp.Tools;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;

namespace GameStore.Api.Mcp.Setup;

public static class McpExtensions
{
    /// Registers the MCP server (tools, resources, prompts), API-key login and the audit log.
    public static IServiceCollection AddGameStoreMcp(this IServiceCollection services)
    {
        services.AddAssistantMcpClient();

        services.AddScoped<IMcpApiKeyService, McpApiKeyService>();
        services.AddScoped<IMcpAuditService, McpAuditService>();

        // An extra authentication scheme next to JWT. It is NOT the default: only the /mcp
        // endpoint asks for it (see MapGameStoreMcp), so REST endpoints still accept JWT only.
        services.AddAuthentication()
            .AddScheme<AuthenticationSchemeOptions, McpApiKeyAuthHandler>(McpApiKeyDefaults.Scheme, _ => { });

        services.AddMcpServer()
            .WithHttpTransport(options =>
            {
                // Stateless: every HTTP request is self-contained, with no session to keep.
                // Each call gets its own DI scope, which the scoped services need.
                options.Stateless = true;
            })
            .AddMcpAudit()
            .AddAuthorizationFilters()
            .WithTools<GameCatalogMcpTools>()
            .WithTools<GameManagementMcpTools>()
            .WithResources<GameCatalogResources>()
            .WithPrompts<GamePrompts>();

        return services;
    }
    
    public static IEndpointConventionBuilder MapGameStoreMcp(this WebApplication app) =>
        app.MapMcp("/mcp").RequireAuthorization(new AuthorizeAttribute
        {
            Roles = $"Admin,{McpApiKeyDefaults.ReaderRole}",
            AuthenticationSchemes = $"{JwtBearerDefaults.AuthenticationScheme},{McpApiKeyDefaults.Scheme}"
        });
}
