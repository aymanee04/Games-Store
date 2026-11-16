using System.Text;
using GameStore.Api.AI.Filters;
using GameStore.Api.AI.Interfaces;
using GameStore.Api.AI.services;
using GameStore.Api.AI.Services;
using GameStore.Api.Data;
using GameStore.Api.Entities;
using GameStore.Api.Infrastructure;
using GameStore.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.SemanticKernel;

namespace GameStore.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration["CONN_STRING"]
            ?? throw new InvalidOperationException("CONN_STRING environment variable is missing.");

        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));

        services.AddScoped<IGameService, GameService>();
        services.AddScoped<IGenreService, GenreService>();
        services.AddScoped<IStudioService, StudioService>();
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ICsvGameParser, CsvGameParser>();
        services.AddSingleton(TimeProvider.System);
        
        services.AddKernel()
            .AddOpenAIChatCompletion(
                modelId: configuration["GROQ_MODEL"] ?? throw new InvalidOperationException("GROQ_MODEL is missing."),
                apiKey: configuration["GROQ_API_KEY"] ?? throw new InvalidOperationException("GROQ_API_KEY is missing."),
                httpClient: new HttpClient
                {
                    BaseAddress = new Uri("https://api.groq.com/openai/v1")
                });
        
        services.AddSingleton<IFunctionInvocationFilter, AiTimingFilter>();

        services.AddMemoryCache();
        services.AddSingleton<IConversationStore, ConversationStore>();
        services.AddScoped<IAdminAssistantService, AdminAssistantService>();

        services.AddScoped<IDocumentExtractionService, DocumentExtractionService>();
        services.AddScoped<IGameImportService, GameImportService>();

        services.AddIdentityCore<ApplicationUser>(o =>
        {
            o.User.RequireUniqueEmail = true;
            o.Password.RequiredLength = 8;
            o.Lockout.MaxFailedAccessAttempts = 5;
            o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        })
        .AddRoles<IdentityRole>()
        .AddEntityFrameworkStores<AppDbContext>()
        .AddSignInManager();

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = configuration["JWT_ISSUER"] ?? throw new InvalidOperationException("Jwt:Issuer configuration is missing."),
                ValidAudience = configuration["JWT_AUDIENCE"] ?? throw new InvalidOperationException("Jwt:Audience configuration is missing."),
                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(configuration["JWT_KEY"] ?? throw new InvalidOperationException("JWT_KEY configuration is missing.")))
            };
        });

        services.AddAuthorization();
        services.AddControllers();
        services.AddProblemDetails();
        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.AddHealthChecks();

        return services;
    }
}