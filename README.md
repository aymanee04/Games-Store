# GameStore API — ASP.NET Core 10 / C# / PostgreSQL

> **Learning project only.** This project is intended for educational purposes and is not designed or audited for production use.

GameStore API is a RESTful backend application for managing video games, genres, and studios. It is built with **ASP.NET Core 10**, **C#**, **Entity Framework Core**, and **PostgreSQL**, with authentication and authorization features implemented using ASP.NET Core Identity and JWT.

The project focuses on learning modern .NET backend development, REST API design, database management, authentication, security fundamentals, and clean separation of responsibilities.

## Features
[docker-compose.yml](docker-compose.yml)
### Game management
- Create, retrieve, update, and delete games.
- Retrieve games using pagination.
- Filter and search games using supported query parameters.
- Associate games with genres and studios.
- Return DTOs instead of exposing database entities directly.

### Genre management
- Manage game genres.
- Validate genre data.
- Enforce unique genre names at the database level.

### Studio management
- Manage game studios.
- Associate studios with games.
- Enforce database constraints on studio names.

### Authentication and security
- User registration and login.
- Password hashing and account management through ASP.NET Core Identity.
- JWT-based access tokens.
- Refresh-token support and token rotation.
- Hashed refresh tokens stored in the database.
- Role-based authorization.
- Centralized API error handling with Problem Details.

### Backend engineering
- RESTful API conventions and appropriate HTTP status codes.
- Dependency injection.
- Asynchronous database operations.
- Entity Framework Core configurations and migrations.
- Service-layer business logic.
- Data Transfer Objects (DTOs).
- Centralized exception handling.
- PostgreSQL integration.

## Tech Stack

| Technology | Purpose |
|---|---|
| C# | Programming language |
| .NET 10 | Development platform |
| ASP.NET Core Web API | HTTP API |
| Entity Framework Core | ORM and database access |
| PostgreSQL | Relational database |
| ASP.NET Core Identity | User and password management |
| JWT | Access-token authentication |
| Docker Compose | Local database environment |

## Project Structure

```text
GameStore/
├── src/
│   └── GameStore.Api/
│       ├── Controllers/       # HTTP endpoints
│       ├── Data/              # DbContext, migrations, seeding
│       │   └── Configurations/
│       ├── Dtos/              # Request and response models
│       ├── Entities/          # Database entities
│       ├── Exceptions/        # Application exceptions
│       ├── Extensions/        # Service and application setup
│       ├── Infrastructure/    # Global exception handling
│       ├── Services/          # Business logic
│       ├── Program.cs         # Application entry point
│       ├── appsettings.json   # Base configuration
│       └── appsettings.Development.json
├── docker-compose.yml
└── README.md
```

## API Endpoints

The API is organized around games, genres, studios, and authentication. Exact routes and request schemas are defined by the controllers and DTOs in the source code.

| Resource | Operations |
|---|---|
| Authentication | Register, login, and refresh tokens |
| Games | Create, list, retrieve, update, and delete |
| Genres | Create, list, retrieve, update, and delete |
| Studios | Create, list, retrieve, update, and delete |

For protected endpoints, authenticate using the access token returned by the login endpoint.

```http
Authorization: Bearer YOUR_ACCESS_TOKEN
```

## Error Handling

The API uses centralized exception handling to produce consistent error responses using the Problem Details format.

Depending on the operation, responses may include standard HTTP status codes such as:

- `200 OK` — successful request.
- `201 Created` — resource created.
- `204 No Content` — successful operation without a response body.
- `400 Bad Request` — invalid request.
- `401 Unauthorized` — missing or invalid authentication.
- `403 Forbidden` — insufficient permissions.
- `404 Not Found` — resource not found.
- `409 Conflict` — conflicting resource or business rule.

The exact status code depends on the endpoint and the exception-handling implementation.

## Development Roadmap

Potential improvements for continued learning include:

- Add automated unit and integration tests.
- Validate configuration at application startup.
- Improve refresh-token rotation with database transactions and concurrency tests.
- Add login and registration rate limiting.
- Improve validation and database constraint error handling.
- Add CI checks for restore, build, and tests.
- Add structured logging and health checks.
- Document endpoint examples and authentication flows.
- Review production security requirements before any real deployment.

## Learning Objectives

This project helps practice:

- Building REST APIs with ASP.NET Core.
- Structuring an application into controllers, services, entities, and DTOs.
- Using dependency injection and asynchronous programming.
- Designing relational schemas with Entity Framework Core.
- Managing database migrations.
- Implementing authentication and role-based authorization.
- Working with JWT access tokens and refresh tokens.
- Handling exceptions consistently.
- Running a PostgreSQL development environment with Docker Compose.

## Disclaimer

This is a personal learning project. It is **not intended for production use**, and its security, reliability.
**All credentials, tokens, and sensitive data should be treated as examples only.** Do not use this code in a production environment without proper security reviews and testing.

## Author

**Aymane Jemmaa**

- GitHub: [@aymanee04](https://github.com/aymanee04)
- Gitlab: [@aymanee04](https://gitlab.com/aymanee04)
- LinkedIn: [Aymane Jemmaa](https://www.linkedin.com/in/aymane-jemmaa)
- Portfolio: [aymane-jemmaa.vercel.app](https://aymane-jemmaa.vercel.app)

---

Built as a hands-on project to learn modern C# and ASP.NET Core backend development.
