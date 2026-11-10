using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using GameStore.Client;
using GameStore.Client.Services;
using Microsoft.AspNetCore.Components.Authorization;
var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddAuthorizationCore();
builder.Services.AddCascadingAuthenticationState();
var apiBase = builder.Configuration["ApiBaseUrl"] ?? "http://localhost:5080/";
builder.Services.AddSingleton(new ApiSettings(apiBase));
builder.Services.AddScoped<TokenStore>();
builder.Services.AddScoped<ClientAuthStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(
    sp => sp.GetRequiredService<ClientAuthStateProvider>());
builder.Services.AddScoped<AuthClient>();
builder.Services.AddScoped<AuthTokenHandler>();

// The HttpClient every page uses: base address + bearer token + a long timeout for the AI.
builder.Services.AddScoped(sp => new HttpClient(
    sp.GetRequiredService<AuthTokenHandler>(), disposeHandler: false)
{
    BaseAddress = new Uri(apiBase),
    Timeout = TimeSpan.FromMinutes(6)
});

builder.Services.AddScoped<StoreApi>();
builder.Services.AddScoped<ImportApi>();

await builder.Build().RunAsync();
