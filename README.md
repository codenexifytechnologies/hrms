# Codenexify HRMS

Multi-tenant HRMS & Employee Experience SaaS platform by **Codenexify Technologies Private Limited**.
ASP.NET Core MVC + Web API · C# · EF Core + Dapper · SQL Server · Bootstrap 5 / jQuery / Chart.js.

| Document | |
|---|---|
| [docs/01-Architecture.md](docs/01-Architecture.md) | System architecture, multi-tenancy, subscription engine, security |
| [docs/02-Database-Design.md](docs/02-Database-Design.md) | ER diagram and conventions |
| [docs/03-Development-Plan.md](docs/03-Development-Plan.md) | Phase-by-phase plan and status |
| [docs/04-Setup-and-Deployment.md](docs/04-Setup-and-Deployment.md) | Local setup, configuration, IIS/Plesk deployment |

```
src/Codenexify.HRMS.Domain          entities, enums
src/Codenexify.HRMS.Application     services, catalogs (features, permissions), interfaces
src/Codenexify.HRMS.Infrastructure  EF Core DbContext (tenant filters), migrations, Dapper, jobs
src/Codenexify.HRMS.Web             Super Admin / Org Admin / Employee areas, public site, API
tests/Codenexify.HRMS.Tests         tenant isolation, subscription engine, auth, portal separation
database/                           generated SQL scripts
```

Quick start: `dotnet tool restore && dotnet run --project src/Codenexify.HRMS.Web --urls http://localhost:5080`
