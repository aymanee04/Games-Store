# GameStore API (ASP.NET 10 / C# / PostgreSQL)

Mini backend: Games and Genres. No security yet (planned as a later step).

## Run

```bash
# 1. PostgreSQL
docker compose up -d

# 2. EF Core CLI (once)
dotnet tool install --global dotnet-ef

# 3. Create the first migration (once)
cd src/GameStore.Api
dotnet ef migrations add InitialCreate -o Data/Migrations

# 4. Run (Development: auto-migrates + seeds sample data)
dotnet run --launch-profile http
```

API: http://localhost:5080/api/games  
OpenAPI JSON: http://localhost:5080/openapi/v1.json  
Try requests with `GameStore.Api.http` (VS Code REST Client, Rider, Visual Studio).

## Structure

| Folder | Role | Spring equivalent |
|---|---|---|
| Controllers | HTTP layer only | @RestController |
| Services | Business logic (interfaces + implementations) | @Service |
| Data | DbContext, entity configurations, seeder | JPA / EntityManager |
| Entities | Database model | @Entity |
| Dtos | API contracts (records) + validation attributes | DTO + Bean Validation |
| Exceptions / Infrastructure | Error types + global handler (ProblemDetails) | @ControllerAdvice |
| Extensions | DI registration kept out of Program.cs | @Configuration |

Production: provide the connection string via env var `ConnectionStrings__GameStoreDb` (never commit real passwords).
