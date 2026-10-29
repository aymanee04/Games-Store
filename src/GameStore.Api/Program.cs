using GameStore.Api.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplicationServices(builder.Configuration);

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();                       // GET /openapi/v1.json
    await app.InitialiseDatabaseAsync();    // apply migrations + seed sample data
}

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
