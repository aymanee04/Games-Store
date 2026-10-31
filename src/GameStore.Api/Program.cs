using DotNetEnv;
using GameStore.Api.Extensions;
Env.Load();
var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddEnvironmentVariables();

builder.Services.AddApplicationServices(builder.Configuration);
builder.Services.AddOpenApi();
builder.Services.AddSwaggerGen();
var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    await app.InitialiseDatabaseAsync(); // apply migrations + seed sample data
    app.MapOpenApi(); // GET /openapi/v1.json
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "GameStore.Api v1"));
}

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();