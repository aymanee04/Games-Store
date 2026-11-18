using DotNetEnv;
using GameStore.Api.Extensions;
using GameStore.Api.Mcp.Setup;
Env.Load();
var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddEnvironmentVariables();

builder.Services.AddApplicationServices(builder.Configuration);
builder.Services.AddGameStoreMcp();
builder.Services.AddSwaggerGen();
builder.Services.AddCors(o => o.AddPolicy("Client", p => p
    .WithOrigins("http://localhost:5014")
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()));
var app = builder.Build();
app.UseCors("Client");
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    await app.InitialiseDatabaseAsync(); // apply migrations + seed sample data
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "GameStore.Api v1"));
}

app.MapControllers();
app.MapGameStoreMcp();
app.MapHealthChecks("/health");

app.Run();