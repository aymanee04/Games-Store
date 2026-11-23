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
    /// <summary>Registers the MCP server (tools, resources, prompts), API-key login and the audit log.</summary>
    public static IServiceCollection AddGameStoreMcp(this IServiceCollection services)
    {
        // The assistant's own MCP client (it reads its catalog tools from the server below).
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
            .AddMcpAudit()              // before the authorization filters, so denied resource/prompt requests are logged
            .AddAuthorizationFilters()  // turns on [Authorize] attributes on tools, resources and prompts
            .WithTools<GameCatalogMcpTools>()       // read: search_games, get_game
            .WithTools<GameManagementMcpTools>()    // write: create_game, update_game, delete_game ([Authorize(Roles = "Admin")])
            .WithResources<GameCatalogResources>()  // data: games://genres, games://studios, games://game/{id}
            .WithPrompts<GamePrompts>();            // templates: price_review

        return services;
    }

    /// <summary>
    /// Exposes /mcp. Accepts a normal admin JWT or an MCP API key. Needs the Admin role (full
    /// keys and admin logins) or McpReader (read-only keys); tools that change data additionally
    /// require Admin through their own [Authorize] attribute.
    /// </summary>
    public static IEndpointConventionBuilder MapGameStoreMcp(this WebApplication app) =>
        app.MapMcp("/mcp").RequireAuthorization(new AuthorizeAttribute
        {
            Roles = $"Admin,{McpApiKeyDefaults.ReaderRole}",
            AuthenticationSchemes = $"{JwtBearerDefaults.AuthenticationScheme},{McpApiKeyDefaults.Scheme}"
        });
}
