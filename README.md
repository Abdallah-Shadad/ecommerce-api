# E-Commerce Web API (.NET 9)

A modular, production-ready E-Commerce RESTful API developed with ASP.NET Core 9, SQL Server, and Entity Framework Core 9, implementing Clean Layered Architecture, Repository & Unit of Work patterns.

## Tech Stack
- **Framework:** ASP.NET Core 9 Web API
- **Language:** C# 13
- **ORM:** Entity Framework Core 9 (Code-First)
- **Database:** Microsoft SQL Server
- **Authentication:** ASP.NET Core Identity & JWT (with Refresh Token Rotation)
- **Validation:** FluentValidation
- **Mapping:** AutoMapper
- **Documentation:** Swagger / OpenAPI

## Architecture Overview
The solution follows a strict 4-Tier Clean Architecture with unidirectional dependencies:
- `ECommerce.Domain`: Core business entities, value objects, and domain logic.
- `ECommerce.Application`: Application logic, interfaces, DTOs, and validations.
- `ECommerce.Infrastructure`: Data access, EF Core persistence, Identity, and third-party integrations.
- `ECommerce.API`: Controllers, middlewares, routing, and Swagger configuration.
- `ECommerce.Shared`: Common responses, paginated results, and shared kernel models.

## Getting Started

### Prerequisites
- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [SQL Server](https://www.microsoft.com/en-us/sql-server/sql-server-downloads)

### Installation
1. Clone the repository:
   ```bash
   git clone [https://github.com/]https://github.com/Abdallah-Shadad/ecommerce-api.git
   cd <ecommerce-api>
   
Restore NuGet dependencies:
`dotnet restore`

Build the solution:
`dotnet build`
