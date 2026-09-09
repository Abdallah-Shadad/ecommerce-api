# E-Commerce Web API — Software Requirements & Architecture Specification (SRS + SAD)

**Version:** 1.0 **Target Stack:** ASP.NET Core 9 (.NET 9) · SQL Server · EF Core 9 (Code-First) · ASP.NET Core Identity · JWT **Audience:** Junior/Mid-level Backend Engineer executing this blueprint independently **Document Type:** Production-Grade Implementation Blueprint

---

## Table of Contents

1. [Executive Summary & Architectural Vision](https://claude.ai/chat/d66180f4-40ab-4531-b9fc-0e9dd95a2f52#1-executive-summary--architectural-vision)
2. [Architectural Design & Project Structure](https://claude.ai/chat/d66180f4-40ab-4531-b9fc-0e9dd95a2f52#2-architectural-design--project-structure)
3. [Domain Model & Database Design (ERD)](https://claude.ai/chat/d66180f4-40ab-4531-b9fc-0e9dd95a2f52#3-domain-model--database-design-erd)
4. [Business Logic & Core Workflows](https://claude.ai/chat/d66180f4-40ab-4531-b9fc-0e9dd95a2f52#4-business-logic--core-workflows)
5. [RESTful API Specification & Endpoints Matrix](https://claude.ai/chat/d66180f4-40ab-4531-b9fc-0e9dd95a2f52#5-restful-api-specification--endpoints-matrix)
6. [Cross-Cutting Concerns & Production Standards](https://claude.ai/chat/d66180f4-40ab-4531-b9fc-0e9dd95a2f52#6-cross-cutting-concerns--production-standards)
7. [Step-by-Step Developer Implementation Roadmap](https://claude.ai/chat/d66180f4-40ab-4531-b9fc-0e9dd95a2f52#7-step-by-step-developer-implementation-roadmap)

---

## 1. Executive Summary & Architectural Vision

### 1.1 System Purpose

This document specifies a production-grade **E-Commerce Web API** that supports customer-facing catalog browsing, persistent cross-device shopping carts, secure checkout with stock-safe order placement, and administrative product/order management. The system is designed to run at **zero external infrastructure cost** during development (local SQL Server, local disk storage, mock payment gateway) while exposing clean abstractions that allow a drop-in swap to cloud services (Azure Blob Storage, Stripe/Paymob, Redis) without touching business logic.

### 1.2 Scope

**In scope:**

- User registration/**authentication** via ASP.NET Core Identity + JWT (access + rotating **refresh tokens**)
- **Role-based** access control (`Customer`, `Admin`)
- **Product catalog with categories, images, filtering, sorting, pagination, search**
- Database-persisted shopping cart (survives across devices/sessions)
- Checkout pipeline with atomic stock deduction and order creation
- Order lifecycle management and status transitions
- Payment abstraction (mock gateway for MVP, interface-ready for real gateways)
- Local file storage abstraction (disk for MVP, interface-ready for cloud blob storage)
- Global exception handling (RFC 7807 ProblemDetails)
- Input validation via FluentValidation

**Out of scope (explicitly deferred):**

- Multi-currency/tax engines, third-party shipping integrations, real payment gateway credentials, multi-tenant support, admin UI (only API endpoints are specified).

### 1.3 Architectural Principles

The system is governed by the following non-negotiable principles:

|Principle|Application in this System|
|---|---|
|**Separation of Concerns**|Presentation (Controllers/DTOs), Business Logic (Services), Data Access (Repositories) are physically separated into distinct projects.|
|**High Cohesion / Low Coupling**|Each service owns one business capability (Auth, Catalog, Cart, Orders, Payments). Services never reach into another service's repository directly — they depend on the other service's public interface.|
|**Dependency Inversion (SOLID-D)**|Controllers depend on Service interfaces; Services depend on Repository interfaces. Concrete implementations are wired only at the Composition Root (`Program.cs`).|
|**Single Responsibility (SOLID-S)**|A repository only performs data access. A service only orchestrates business rules. A validator only validates. No class does two jobs.|
|**Open/Closed (SOLID-O)**|`IFileStorageService` and `IPaymentService` are designed so a new provider (Azure Blob, Stripe) is added via a new class implementing the interface — zero modification to existing consumers.|
|**Interface Segregation (SOLID-I)**|No "fat" repository interface shared across unrelated entities; each aggregate root gets its own repository contract via the generic `IRepository<T>` plus entity-specific extensions only where genuinely needed.|
|**Testability First**|Every service and repository is interface-based and constructor-injected, enabling full unit testing with mocking frameworks (Moq/NSubstitute) without touching a real database.|
|**Fail-Safe Data Integrity**|All multi-step writes (checkout, stock deduction, order + payment creation) are wrapped in explicit database transactions using EF Core's execution strategy to survive transient failures and retries.|

### 1.4 Architectural Style Decision

**Chosen style: Layered (N-Tier) Monolith with Clean Architecture influences**, using:

- **Repository + Unit of Work** for data access abstraction
- **Service Layer** for business orchestration
- **DTO + AutoMapper (or manual mapping)** to decouple domain entities from the wire format

**Why not full Clean Architecture / CQRS+MediatR for this project?** Given the target audience (a junior/mid engineer building this solo, incrementally, without prior large-scale system experience) and the project's genuine complexity (single bounded context, no microservices need), a **pragmatic layered architecture** delivers 90% of Clean Architecture's testability and maintainability benefits with far less ceremony. The design keeps a clear one-way dependency flow (`API → Services → Repositories → Data`) so migrating to a stricter Clean Architecture (with a separate Domain/Application split) later is a refactor, not a rewrite.

---

## 2. Architectural Design & Project Structure

### 2.1 Solution Structure

``` ts
ECommerce.sln
│
├── src/
│   ├── ECommerce.API/          → Presentation Layer (Web API host)
│   ├── ECommerce.Application/  → Business Logic Layer (Services, DTOs, Validators, Interfaces)
│   ├── ECommerce.Domain/  → Core Layer (Entities, Enums, Domain Exceptions — zero dependencies)
│   ├── ECommerce.Infrastructure/ → Data Access Layer (EF Core, Repositories, Identity, File/Payment impl.)
│   └── ECommerce.Shared/   → Cross-cutting Kernel (ProblemDetails models, Result<T>, Constants)
│
└── tests/
    ├── ECommerce.UnitTests/    → Service & validator unit tests (mocked repositories)
    └── ECommerce.IntegrationTests/ → Endpoint tests (WebApplicationFactory + in-memory/test DB)
```

### 2.2 Project Responsibilities & Dependency Rules

| Project                    | Responsibility                                                                                                                                                                                                                 | Depends On                          | Must NOT Depend On               |
| -------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ | ----------------------------------- | -------------------------------- |
| `ECommerce.Domain`         | ==Entities, enums, domain exceptions, domain constants==. Pure C#, zero NuGet packages beyond base .NET.                                                                                                                       | _(nothing)_                         | Application, Infrastructure, API |
| `ECommerce.Application`    | ==Service interfaces + implementations, DTOs, FluentValidation validators, AutoMapper profiles, custom exceptions, repository== _interfaces_ (`IRepository<T>`, `IUnitOfWork`, `IFileStorageService`, `IPaymentService`).      | Domain, Shared                      | Infrastructure, API              |
| `ECommerce.Infrastructure` | `AppDbContext`, EF Core **configurations** (Fluent API), concrete ==Repository/UnitOfWork implementations==, ASP.NET ==Identity== setup, ==JWT== token generation, ==local disk== `FileStorageService`, mock `PaymentService`. | Domain, Application, Shared         | API                              |
| `ECommerce.API`            | ==Controllers, middleware,== `Program.cs` composition root, Swagger config, DI registration, appsettings.                                                                                                                      | Application, Infrastructure, Shared | _(nothing depends on API)_       |
| `ECommerce.Shared`         | `ApiResponse<T>`, `PagedResult<T>`, `ProblemDetails` extensions, shared constants/enums used across layers.                                                                                                                    | _(nothing)_                         | all others                       |

> **Rule enforced at compile time:** `ECommerce.Domain` and `ECommerce.Application` never reference `Microsoft.EntityFrameworkCore` directly in a way that leaks EF-specific types (e.g., `DbSet`) into their public surface. Only `ECommerce.Infrastructure` references EF Core's SQL Server provider.

### 2.3 Folder Hierarchy per Project

```ts
ECommerce.Domain/
├── Entities/
│   ├── Common/
│   │   └── AuditableEntity.cs
│   ├── Identity/
│   │   ├── ApplicationUser.cs
│   │   └── RefreshToken.cs
│   ├── Catalog/
│   │   ├── Category.cs
│   │   ├── Product.cs
│   │   └── ProductImage.cs
│   ├── Cart/
│   │   ├── Cart.cs
│   │   └── CartItem.cs
│   └── Ordering/
│       ├── Order.cs
│       ├── OrderItem.cs
│       └── Payment.cs
├── Enums/
│   ├── OrderStatus.cs
│   └── PaymentStatus.cs
└── Exceptions/
    ├── NotFoundException.cs
    ├── ConflictException.cs
    └── InsufficientStockException.cs

ECommerce.Application/
├── Interfaces/
│   ├── Persistence/
│   │   ├── IRepository.cs
│   │   ├── IUnitOfWork.cs
│   │   └── IProductRepository.cs   (entity-specific query extensions)
│   ├── Services/
│   │   ├── IAuthService.cs
│   │   ├── IProductService.cs
│   │   ├── ICategoryService.cs
│   │   ├── ICartService.cs
│   │   ├── IOrderService.cs
│   │   ├── ITokenService.cs
│   │   ├── IFileStorageService.cs
│   │   └── IPaymentService.cs
├── Services/
│   ├── AuthService.cs
│   ├── ProductService.cs
│   ├── CategoryService.cs
│   ├── CartService.cs
│   └── OrderService.cs
├── DTOs/
│   ├── Auth/  (RegisterRequest, LoginRequest, AuthResponse, RefreshTokenRequest)
│   ├── Product/ (ProductDto, ProductCreateDto, ProductUpdateDto, ProductQueryParameters)
│   ├── Category/ (CategoryDto, CategoryCreateDto)
│   ├── Cart/ (CartDto, CartItemDto, AddCartItemDto, UpdateCartItemDto)
│   └── Order/ (OrderDto, OrderItemDto, CreateOrderDto, OrderStatusUpdateDto)
├── Validators/
│   ├── Auth/ (RegisterRequestValidator, LoginRequestValidator)
│   ├── Product/ (ProductCreateDtoValidator, ProductUpdateDtoValidator)
│   └── Cart/ (AddCartItemDtoValidator)
├── Mappings/
│   └── MappingProfile.cs
└── Common/
    ├── PagedResult.cs
    └── ServiceConstants.cs

ECommerce.Infrastructure/
├── Persistence/
│   ├── AppDbContext.cs
│   ├── Configurations/
│   │   ├── ProductConfiguration.cs
│   │   ├── CategoryConfiguration.cs
│   │   ├── CartConfiguration.cs
│   │   ├── OrderConfiguration.cs
│   │   └── ... (one file per entity)
│   ├── Repositories/
│   │   ├── Repository.cs             (generic implementation)
│   │   ├── ProductRepository.cs
│   │   └── UnitOfWork.cs
│   └── Migrations/                   (EF Core generated)
├── Identity/
│   ├── TokenService.cs
│   └── IdentitySeeder.cs             (seeds Admin role + default admin)
├── Services/
│   ├── LocalFileStorageService.cs
│   └── MockPaymentService.cs
└── DependencyInjection.cs            (AddInfrastructure() extension method)

ECommerce.API/
├── Controllers/
│   ├── AuthController.cs
│   ├── CategoriesController.cs
│   ├── ProductsController.cs
│   ├── CartController.cs
│   └── OrdersController.cs
├── Middleware/
│   └── ExceptionHandlingMiddleware.cs
├── Extensions/
│   └── DependencyInjection.cs (AddApiServices():Swagger,JWT auth,CORS, rate limiting)
├── appsettings.json
│   └── appsettings.Development.json
└── Program.cs
```

### 2.4 Layer Interaction & Data Flow Diagram

```ts
┌───────────────────────────────────────────────────────────────────────────┐
│                              CLIENT (HTTP)                                │
└──────────────────────────────────┬────────────────────────────────────────┘
                                  │  JSON over HTTPS + Bearer JWT
                                  ▼
┌──────────────────────────────────────────────────────────────────────────┐
│  ECommerce.API  (Presentation Layer)                                     │
│  ┌─────────────────────────────────────────────────────────────────────┐ │
│  │ Middleware Pipeline:                                                │ │
│  │  ExceptionHandling → CORS → RateLimiter → Authentication →          │ │
│  │  Authorization → Routing → Controller Action                        │ │
│  └─────────────────────────────────────────────────────────────────────┘ │
│  Controllers: receive DTO, validate ModelState, call IService, return    │
│  ActionResult<T> (never touch DbContext or domain entities directly)     │
└──────────────────────────────────┬───────────────────────────────────────┘
                                  │  calls interface (DI-injected)
                                  ▼
┌────────────────────────────────────────────────────────────────────────┐
│  ECommerce.Application  (Business Logic Layer)                         │
│  Services: orchestrate business rules, call FluentValidation,          │
│  map Entity <-> DTO (AutoMapper), call IUnitOfWork / IRepository<T>,   │
│  call IFileStorageService / IPaymentService, throw domain exceptions   │
└──────────────────────────────────┬─────────────────────────────────────┘
                                  │  calls interface (DI-injected)
                                  ▼
┌───────────────────────────────────────────────────────────────────────┐
│  ECommerce.Infrastructure  (Data Access Layer)                        │
│  Repository<T> / UnitOfWork → AppDbContext (EF Core 9) → SQL Server   │
│  TokenService → generates/validates JWT + refresh tokens              │
│  LocalFileStorageService → writes to wwwroot/uploads                  │
│  MockPaymentService → simulates gateway authorization                 │
└──────────────────────────────────┬────────────────────────────────────┘
                                  │  SQL (parameterized, via EF Core)
                                  ▼
┌───────────────────────────────────────────────────────────────────────┐
│                          SQL Server Database                          │
└───────────────────────────────────────────────────────────────────────┘

  DEPENDENCY DIRECTION (compile-time references):
  API ──────────► Application ──────────► Domain
   │                                          ▲
   └────────► Infrastructure ─────────────────┘
                    (Infrastructure implements Application's interfaces)
```

**Request-to-database walkthrough (example: `POST /api/products`):**

1. `ProductsController.Create(ProductCreateDto dto)` receives the request; `[Authorize(Roles = "Admin")]` gate is evaluated by the Authorization middleware before the action runs.
2. Controller calls `_productService.CreateAsync(dto)` — no business logic in the controller itself.
3. `ProductService` invokes `FluentValidation` (`ProductCreateDtoValidator`) — on failure throws `ValidationException`, caught globally.
4. `ProductService` maps `ProductCreateDto → Product` entity, calls `_unitOfWork.Products.AddAsync(product)`.
5. `ProductService` calls `_unitOfWork.SaveChangesAsync()` — EF Core generates and executes the parameterized `INSERT`.
6. Service maps saved entity back to `ProductDto`, returns to controller.
7. Controller returns `201 Created` with `Location` header and the DTO body.

---

## 3. Domain Model & Database Design (ERD)

### 3.1 Auditable Entity Base Class

```csharp
namespace ECommerce.Domain.Entities.Common;

public abstract class AuditableEntity
{
    public int Id { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }
    public bool IsDeleted { get; set; } = false; // soft delete flag, global query filter applied
}
```

All entities except `ApplicationUser` (which extends `IdentityUser<Guid>`) and pure join-configuration inherit from `AuditableEntity`.

### 3.2 Identity Entities

**`ApplicationUser`** (extends `IdentityUser<Guid>`)

|Property|Type|Constraints|
|---|---|---|
|`Id`|`Guid`|PK (inherited)|
|`FullName`|`string`|`MaxLength(100)`, Required|
|`CreatedAtUtc`|`DateTime`|Required, default `UtcNow`|
|`RefreshTokens`|`ICollection<RefreshToken>`|Navigation, 1:N|
|`Cart`|`Cart?`|Navigation, 1:1|
|`Orders`|`ICollection<Order>`|Navigation, 1:N|

**`RefreshToken`**

|Property|Type|Constraints|
|---|---|---|
|`Id`|`int`|PK, Identity|
|`Token`|`string`|`MaxLength(200)`, Unique Index, Required|
|`ExpiresAtUtc`|`DateTime`|Required|
|`CreatedAtUtc`|`DateTime`|Required|
|`RevokedAtUtc`|`DateTime?`|Nullable|
|`ReplacedByToken`|`string?`|`MaxLength(200)`, Nullable — set when rotated|
|`UserId`|`Guid`|FK → `ApplicationUser.Id`, Required, Indexed|
|_(computed, not mapped)_ `IsActive`|`bool`|`RevokedAtUtc == null && ExpiresAtUtc > UtcNow`|

**Roles seeded:** `Admin`, `Customer` (via `IdentitySeeder` on startup).

### 3.3 Catalog Entities

**`Category`**

|Property|Type|Constraints|
|---|---|---|
|`Id`|`int`|PK, Identity|
|`Name`|`string`|`MaxLength(80)`, Required, Unique Index|
|`Description`|`string?`|`MaxLength(500)`, Nullable|
|`Products`|`ICollection<Product>`|Navigation, 1:N|

**`Product`**

| Property        | Type                        | Constraints                                                                    |
| --------------- | --------------------------- | ------------------------------------------------------------------------------ |
| `Id`            | `int`                       | PK, Identity                                                                   |
| `Name`          | `string`                    | `MaxLength(150)`, Required, Indexed (for search)                               |
| `Slug`          | `string`                    | `MaxLength(180)`, Required, Unique Index                                       |
| `Description`   | `string`                    | `MaxLength(4000)`, Required                                                    |
| `Price`         | `decimal(18,2)`             | Required, `CHECK (Price >= 0)`                                                 |
| `StockQuantity` | `int`                       | Required, `CHECK (StockQuantity >= 0)`, **RowVersion-tracked for concurrency** |
| `RowVersion`    | `byte[]`                    | `[Timestamp]` — optimistic concurrency token                                   |
| `SKU`           | `string`                    | `MaxLength(50)`, Required, Unique Index                                        |
| `IsActive`      | `bool`                      | Required, default `true`                                                       |
| `CategoryId`    | `int`                       | FK → `Category.Id`, Required, Indexed                                          |
| `Images`        | `ICollection<ProductImage>` | Navigation, 1:N                                                                |

**`ProductImage`**

|Property|Type|Constraints|
|---|---|---|
|`Id`|`int`|PK, Identity|
|`ImageUrl`|`string`|`MaxLength(300)`, Required|
|`IsPrimary`|`bool`|Required, default `false`|
|`DisplayOrder`|`int`|Required, default `0`|
|`ProductId`|`int`|FK → `Product.Id`, Required, Indexed|

### 3.4 Cart Entities

**`Cart`**

|Property|Type|Constraints|
|---|---|---|
|`Id`|`int`|PK, Identity|
|`UserId`|`Guid`|FK → `ApplicationUser.Id`, Required, **Unique Index** (enforces 1:1)|
|`Items`|`ICollection<CartItem>`|Navigation, 1:N|

**`CartItem`**

|Property|Type|Constraints|
|---|---|---|
|`Id`|`int`|PK, Identity|
|`CartId`|`int`|FK → `Cart.Id`, Required, Indexed|
|`ProductId`|`int`|FK → `Product.Id`, Required, Indexed|
|`Quantity`|`int`|Required, `CHECK (Quantity > 0)`|
|`UnitPriceSnapshot`|`decimal(18,2)`|Required — price at time of add, re-validated at checkout|
|_Composite Unique Index_|`(CartId, ProductId)`|Prevents duplicate line items for the same product|

### 3.5 Ordering Entities

**`Order`**

| Property          | Type                                                         | Constraints                                                         |
| ----------------- | ------------------------------------------------------------ | ------------------------------------------------------------------- |
| `Id`              | `int`                                                        | PK, Identity                                                        |
| `OrderNumber`     | `string`                                                     | `MaxLength(20)`, Required, Unique Index (e.g., `ORD-20260817-0001`) |
| `UserId`          | `Guid`                                                       | FK → `ApplicationUser.Id`, Required, Indexed                        |
| `Status`          | `OrderStatus` (enum, stored as `string` via `HasConversion`) | Required, default `Pending`                                         |
| `TotalAmount`     | `decimal(18,2)`                                              | Required, `CHECK (TotalAmount >= 0)`                                |
| `ShippingAddress` | `string`                                                     | `MaxLength(300)`, Required                                          |
| `Items`           | `ICollection<OrderItem>`                                     | Navigation, 1:N                                                     |
| `Payment`         | `Payment?`                                                   | Navigation, 1:1                                                     |

**`OrderItem`**

|Property|Type|Constraints|
|---|---|---|
|`Id`|`int`|PK, Identity|
|`OrderId`|`int`|FK → `Order.Id`, Required, Indexed|
|`ProductId`|`int`|FK → `Product.Id`, Required (no cascade delete — see §3.7)|
|`ProductNameSnapshot`|`string`|`MaxLength(150)`, Required — historical record even if product renamed|
|`UnitPrice`|`decimal(18,2)`|Required — price _at time of purchase_, immutable|
|`Quantity`|`int`|Required, `CHECK (Quantity > 0)`|

**`Payment`**

|Property|Type|Constraints|
|---|---|---|
|`Id`|`int`|PK, Identity|
|`OrderId`|`int`|FK → `Order.Id`, Required, **Unique Index** (enforces 1:1)|
|`Status`|`PaymentStatus` (enum, stored as `string`)|Required, default `Pending`|
|`Provider`|`string`|`MaxLength(50)`, Required (e.g., `"Mock"`, `"Stripe"`)|
|`TransactionReference`|`string?`|`MaxLength(100)`, Nullable|
|`Amount`|`decimal(18,2)`|Required|
|`PaidAtUtc`|`DateTime?`|Nullable|

**`OrderStatus` enum:** `Pending`, `Confirmed`, `Processing`, `Shipped`, `Delivered`, `Cancelled`

**`PaymentStatus` enum:** `Pending`, `Authorized`, `Succeeded`, `Failed`, `Refunded`

### 3.6 EF Core Best Practices Applied

- **Fluent API configuration separation:** each entity gets its own `IEntityTypeConfiguration<T>` class under `Infrastructure/Persistence/Configurations/`; `AppDbContext.OnModelCreating` calls `modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly)` — never inline `OnModelCreating` blocks.
- **Global query filter for soft delete:** `modelBuilder.Entity<Product>().HasQueryFilter(p => !p.IsDeleted);` applied to every `AuditableEntity` subtype via a reflection loop in `OnModelCreating`.
- **Decimal precision explicitly configured:** `builder.Property(p => p.Price).HasPrecision(18, 2);` on every monetary field — SQL Server silently truncates unconfigured decimals.
- **Concurrency token:** `Product.RowVersion` mapped as `[Timestamp]` / `.IsRowVersion()` to prevent lost-update anomalies during simultaneous stock deduction.
- **Enum-to-string conversion:** `builder.Property(o => o.Status).HasConversion<string>().HasMaxLength(20);` — keeps the database human-readable and index-friendly instead of opaque integers.
- **No `AppDbContext` injected into Services** — Services depend only on `IUnitOfWork` / `IRepository<T>`, per §1.3.

### 3.7 Entity-Relationship Diagram (ASCII)

``` ts
┌──────────────────────────┐   1:1   ┌──────────────────────────┐
│ ApplicationUser          │─────────│ Cart                     │
│──────────────────────────│         │──────────────────────────│
│ Id (PK, Guid)            │         │ Id (PK)                  │
│ FullName                 │         │ UserId (FK, UQ)          │
│ Email / UserName         │         └────────────┬─────────────┘
└────────────┬─────────────┘                      │ 1:N
             │ 1:N                                ▼
             ▼                       ┌──────────────────────────┐
┌──────────────────────────┐         │ CartItem                 │
│ RefreshToken             │         │──────────────────────────│
│──────────────────────────│         │ Id (PK)                  │
│ Id (PK)                  │         │ CartId (FK)              │
│ Token (UQ)               │         │ ProductId (FK)           │
│ UserId (FK)              │         │ Quantity                 │
│ ExpiresAtUtc             │         │ UnitPriceSnapshot        │
│ RevokedAtUtc             │         └────────────┬─────────────┘
└────────────┬─────────────┘                      │ N:1
             │ 1:N                                ▼
             ▼                       ┌──────────────────────────┐
┌──────────────────────────┐   N:1   │ Product                  │
│ Order                    │────────►│──────────────────────────│
│──────────────────────────│         │ Id (PK)                  │
│ Id (PK)                  │         │ Name / Slug (UQ)         │
│ OrderNumber (UQ)         │         │ SKU (UQ)                 │
│ UserId (FK)              │         │ Price                    │
│ Status                   │         │ StockQuantity            │
│ TotalAmount              │         │ RowVersion               │
│ ShippingAddress          │         │ CategoryId (FK)          │
└────────────┬─────────────┘         └─────┬───────────────▲────┘
             │ 1:N                         │ 1:N           │
             ▼                             ▼               │ N:1
┌──────────────────────────┐   N:1   ┌────────────┐        │
│ OrderItem                │────────►│ProductImage│        │
│──────────────────────────│         │────────────│        │
│ Id (PK)                  │         │ Id (PK)    │        │
│ OrderId (FK)             │         │ ProductId  │        │
│ ProductId (FK, no cascad)│         │ ImageUrl   │        │
│ ProductNameSnapshot      │         │ IsPrimary  │        │
│ UnitPrice                │         └────────────┘        │
│ Quantity                 │                               │
└────────────┬─────────────┘                               │
             │ 1:1                                         │
             ▼                                             │
┌──────────────────────────┐         ┌─────────────────────┴────┐
│ Payment                  │         │ Category                 │
│──────────────────────────│         │──────────────────────────│
│ Id (PK)                  │         │ Id (PK)                  │
│ OrderId (FK, UQ)         │         │ Name (UQ)                │
│ Status                   │         │ Description              │
│ Provider                 │         └──────────────────────────┘
│ TransactionReference     │
│ Amount                   │
└──────────────────────────┘
```

**Cardinality summary:**

|Relationship|Cardinality|Delete Behavior|
|---|---|---|
|`ApplicationUser` → `RefreshToken`|1:N|Cascade|
|`ApplicationUser` → `Cart`|1:1|Cascade|
|`ApplicationUser` → `Order`|1:N|Restrict (never delete a user's order history)|
|`Cart` → `CartItem`|1:N|Cascade|
|`Category` → `Product`|1:N|Restrict (cannot delete category with products)|
|`Product` → `ProductImage`|1:N|Cascade|
|`Product` → `CartItem`|1:N|Cascade (removing a product clears it from carts)|
|`Product` → `OrderItem`|1:N|**Restrict / NoAction** — historical order lines must survive even if the product is later deleted; `ProductNameSnapshot`/`UnitPrice` preserve the record|
|`Order` → `OrderItem`|1:N|Cascade|
|`Order` → `Payment`|1:1|Cascade|

---

## 4. Business Logic & Core Workflows

### 4.1 Authentication Lifecycle

**Registration (`POST /api/auth/register`):**

1. Validate `RegisterRequest` (email format, password policy: min 8 chars, 1 upper, 1 digit, 1 special).
2. `UserManager.CreateAsync()` — Identity hashes password with PBKDF2.
3. Assign default role `Customer` via `UserManager.AddToRoleAsync`.
4. Auto-create an empty `Cart` row for the new user (1:1 invariant established at registration, not lazily).
5. Return `201 Created` (no auto-login token by design — user must explicitly log in).

**Login (`POST /api/auth/login`):**

1. `UserManager.FindByEmailAsync` + `UserManager.CheckPasswordAsync`.
2. On success: `TokenService.GenerateAccessToken(user, roles)` (JWT, 15-minute expiry) + `TokenService.GenerateRefreshToken()` (opaque random 64-byte string, 7-day expiry, persisted to `RefreshToken` table).
3. Return `AuthResponse { AccessToken, RefreshToken, ExpiresAtUtc }`.

**Refresh Token Rotation (`POST /api/auth/refresh`):**

1. Look up the submitted refresh token in the `RefreshToken` table.
2. Validate: exists, not revoked, not expired (`IsActive == true`) → else `401 Unauthorized`.
3. **Rotate:** mark the old token `RevokedAtUtc = UtcNow`, `ReplacedByToken = <new token>`; issue a brand-new refresh token row + a new access token.
4. **Reuse-detection:** if an already-revoked token is presented again, treat as a compromise signal — revoke **all** active refresh tokens for that user and require re-login.

**Logout (`POST /api/auth/logout`):**

1. Revoke the specific refresh token supplied (`RevokedAtUtc = UtcNow`).
2. Access token is stateless JWT — client discards it; no server-side blacklist required for MVP scope (short 15-min expiry bounds the exposure window).

### 4.2 Product Catalog — Filtering, Searching, Sorting, Pagination

`GET /api/products` accepts a `ProductQueryParameters` object bound from the query string:

```csharp
public class ProductQueryParameters
{
    public string? SearchTerm { get; set; }    // matches Name (LIKE '%term%') via indexed column
    public int? CategoryId { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public bool? InStockOnly { get; set; }
    public string SortBy { get; set; } = "name";  // name | price | newest
    public bool Descending { get; set; } = false;
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 20;       // capped server-side at 100
}
```

Service builds a composable `IQueryable<Product>` pipeline: base query (excludes soft-deleted via global filter) → apply `Where` filters conditionally → apply `OrderBy`/`OrderByDescending` switch on `SortBy` → project to `ProductDto` **before** pagination executes `Skip/Take` (avoids over-fetching full entities) → wrap in `PagedResult<ProductDto>` containing `TotalCount`, `PageNumber`, `PageSize`, `TotalPages`, `Items`.

### 4.3 Shopping Cart Operations

- **Add item (`POST /api/cart/items`):** Look up existing `CartItem` for `(CartId, ProductId)`. If found → increment `Quantity`. If not → insert new row with `UnitPriceSnapshot = product.Price`. **Validate `product.StockQuantity >= requestedQuantity`** before persisting; else `409 Conflict`.
- **Update quantity (`PUT /api/cart/items/{productId}`):** Re-validates stock availability against the _new_ total quantity, not just the delta.
- **Remove item (`DELETE /api/cart/items/{productId}`):** Hard delete the `CartItem` row (carts are ephemeral working state, not audit history).
- **Clear cart (`DELETE /api/cart`):** Bulk-delete all `CartItem` rows for the user's cart — used after successful checkout.
- **Get cart (`GET /api/cart`):** Returns live-recalculated line totals and a cart-level `Subtotal`; **flags any item whose live `Product.Price` has drifted from `UnitPriceSnapshot`** so the client can show a price-change warning before checkout.

### 4.4 Order Placement & Checkout Lifecycle

This is the highest-risk workflow in the system — it must be **atomic** and **stock-safe** under concurrent checkouts.

``` ts
┌─────────────────────────────────────────────────────────────────────────┐
│              CHECKOUT PIPELINE (OrderService.PlaceOrderAsync)           │
├─────────────────────────────────────────────────────────────────────────┤
│ 1. Load the user's Cart with Items + related Products (tracked query)   │
│ 2. Guard: Cart.Items.Count == 0  →  throw 400 "Cart is empty"           │
│ 3. BEGIN EF Core Execution Strategy (handles transient SQL failures):   │
│    ┌───────────────────────────────────────────────────────────────┐    │
│    │ BEGIN DATABASE TRANSACTION                                    │    │
│    │  a. For each CartItem:                                        │    │
│    │     - Re-fetch Product WITH its RowVersion (fresh read)       │    │
│    │     - IF Product.StockQuantity < CartItem.Quantity            │    │
│    │            → throw InsufficientStockException(productName)    │    │
│    │     - Product.StockQuantity -= CartItem.Quantity              │    │
│    │       (EF Core checks RowVersion on SaveChanges → optimistic  │    │
│    │        concurrency guard against a simultaneous checkout)     │    │
│    │  b. Create Order { Status = Pending, Items = [...] }          │    │
│    │     - Snapshot ProductNameSnapshot + UnitPrice per line       │    │
│    │     - TotalAmount = Σ(UnitPrice * Quantity)                   │    │
│    │  c. Create Payment { Status = Pending, Amount = TotalAmount } │    │
│    │  d. Call IPaymentService.AuthorizeAsync(payment)              │    │
│    │     - On success → Payment.Status = Succeeded, Order.Status = │    │
│    │       Confirmed                                               │    │
│    │     - On failure → rollback entire transaction (stock is      │    │
│    │       restored automatically since nothing commits)           │    │
│    │  e. Delete all CartItems (cart is cleared)                    │    │
│    │  f. SaveChangesAsync()  →  COMMIT TRANSACTION                 │    │
│    └───────────────────────────────────────────────────────────────┘    │
│    ON DbUpdateConcurrencyException → catch, return 409 Conflict         │
│    with message "Stock changed, please review your cart and retry"      │
│ 4. Return OrderDto to caller (201 Created)                              │
└─────────────────────────────────────────────────────────────────────────┘
```

**Why `CreateExecutionStrategy()` matters:** SQL Server connection retries (e.g., Azure SQL transient faults) must wrap the _entire_ transaction block, not just individual `SaveChanges` calls, or a retried operation can silently double-execute. The canonical pattern:

```csharp
var strategy = _context.Database.CreateExecutionStrategy();
await strategy.ExecuteAsync(async () =>
{
    await using var transaction = await _context.Database.BeginTransactionAsync();
    // ... steps a–f above ...
    await transaction.CommitAsync();
});
```

### 4.5 Order State Machine

```
                ┌─────────────┐
   place        │   Pending   │
   order ──────►│  (default)  │
                └──────┬──────┘
                       │ payment authorized
                       ▼
                ┌─────────────┐   admin cancels     ┌─────────────┐
                │  Confirmed  │────────────────────►│  Cancelled  │
                └──────┬──────┘                     └─────────────┘
                       │ admin marks processing            ▲
                       ▼                                   │ cancel allowed only
                ┌─────────────┐                            │ from Pending/Confirmed
                │ Processing  │────────────────────────────┘
                └──────┬──────┘
                       │ admin marks shipped
                       ▼
                ┌─────────────┐
                │   Shipped   │
                └──────┬──────┘
                       │ delivery confirmed
                       ▼
                ┌─────────────┐
                │  Delivered  │  (terminal state)
                └─────────────┘
```

**Enforced transition rules (in `OrderService.UpdateStatusAsync`):**

- `Cancelled` is reachable only from `Pending` or `Confirmed` — cannot cancel a `Shipped`/`Delivered` order via this endpoint.
- Cancelling an order **restores stock** (`Product.StockQuantity += OrderItem.Quantity` for each line) inside the same transaction.
- All other transitions must follow the linear path above; any out-of-sequence transition request (e.g., `Pending → Shipped`) returns `400 Bad Request` with the current and requested states in the error body.

---

## 5. RESTful API Specification & Endpoints Matrix

**Conventions:** All routes prefixed `/api`. All authenticated routes require header `Authorization: Bearer {accessToken}`. `Customer` role implicitly required for own-resource endpoints (cart, own orders); `Admin` role required for catalog/order-management mutations.

|Module|Method & Route|Request DTO / Params|Response DTO (Status)|Authorization|
|---|---|---|---|---|
|**Auth**|`POST /api/auth/register`|`RegisterRequest {FullName, Email, Password, ConfirmPassword}`|`201` (no body) · `400` ValidationProblem|Anonymous|
|**Auth**|`POST /api/auth/login`|`LoginRequest {Email, Password}`|`AuthResponse {AccessToken, RefreshToken, ExpiresAtUtc}` (`200`) · `401`|Anonymous|
|**Auth**|`POST /api/auth/refresh`|`RefreshTokenRequest {RefreshToken}`|`AuthResponse` (`200`) · `401`|Anonymous|
|**Auth**|`POST /api/auth/logout`|`RefreshTokenRequest {RefreshToken}`|`204` · `401`|Customer, Admin|
|**Categories**|`GET /api/categories`|—|`List<CategoryDto>` (`200`)|Anonymous|
|**Categories**|`GET /api/categories/{id}`|route `id: int`|`CategoryDto` (`200`) · `404`|Anonymous|
|**Categories**|`POST /api/categories`|`CategoryCreateDto {Name, Description}`|`CategoryDto` (`201`) · `400`|Admin|
|**Categories**|`PUT /api/categories/{id}`|`CategoryUpdateDto`|`CategoryDto` (`200`) · `400` · `404`|Admin|
|**Categories**|`DELETE /api/categories/{id}`|route `id: int`|`204` · `404` · `409` (has products)|Admin|
|**Products**|`GET /api/products`|`ProductQueryParameters` (query string)|`PagedResult<ProductDto>` (`200`)|Anonymous|
|**Products**|`GET /api/products/{id}`|route `id: int`|`ProductDto` (`200`) · `404`|Anonymous|
|**Products**|`POST /api/products`|`ProductCreateDto {Name, Description, Price, StockQuantity, SKU, CategoryId}`|`ProductDto` (`201`) · `400`|Admin|
|**Products**|`PUT /api/products/{id}`|`ProductUpdateDto`|`ProductDto` (`200`) · `400` · `404`|Admin|
|**Products**|`DELETE /api/products/{id}`|route `id: int`|`204` · `404`|Admin|
|**Products**|`POST /api/products/{id}/images`|`multipart/form-data` (`IFormFile`)|`ProductImageDto` (`201`) · `400` · `404`|Admin|
|**Products**|`DELETE /api/products/{id}/images/{imageId}`|route params|`204` · `404`|Admin|
|**Cart**|`GET /api/cart`|—|`CartDto {Items[], Subtotal}` (`200`)|Customer|
|**Cart**|`POST /api/cart/items`|`AddCartItemDto {ProductId, Quantity}`|`CartDto` (`200`) · `400` · `404` · `409` (stock)|Customer|
|**Cart**|`PUT /api/cart/items/{productId}`|`UpdateCartItemDto {Quantity}`|`CartDto` (`200`) · `400` · `404` · `409`|Customer|
|**Cart**|`DELETE /api/cart/items/{productId}`|route `productId: int`|`CartDto` (`200`) · `404`|Customer|
|**Cart**|`DELETE /api/cart`|—|`204`|Customer|
|**Orders**|`POST /api/orders/checkout`|`CreateOrderDto {ShippingAddress}`|`OrderDto` (`201`) · `400` (empty cart) · `409` (stock conflict)|Customer|
|**Orders**|`GET /api/orders`|query: `pageNumber, pageSize`|`PagedResult<OrderDto>` (own orders only) (`200`)|Customer|
|**Orders**|`GET /api/orders/{id}`|route `id: int`|`OrderDto` (`200`) · `404` (own orders only; **404 not 403** for cross-user access — see §6)|Customer|
|**Orders**|`GET /api/admin/orders`|`OrderQueryParameters` (status, date range, pagination)|`PagedResult<OrderDto>` (all users) (`200`)|Admin|
|**Orders**|`PUT /api/admin/orders/{id}/status`|`OrderStatusUpdateDto {NewStatus}`|`OrderDto` (`200`) · `400` (invalid transition) · `404`|Admin|
|**Orders**|`POST /api/orders/{id}/cancel`|route `id: int`|`OrderDto` (`200`) · `400` (not cancellable) · `404`|Customer (own order)|

---

## 6. Cross-Cutting Concerns & Production Standards

### 6.1 Global Exception Handling (RFC 7807 ProblemDetails)

Implemented as `ExceptionHandlingMiddleware`, registered first in the pipeline:

```csharp
public class ExceptionHandlingMiddleware
{
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        try { await next(context); }
        catch (Exception ex) { await HandleExceptionAsync(context, ex); }
    }

    private static Task HandleExceptionAsync(HttpContext context, Exception ex)
    {
        var (statusCode, title) = ex switch
        {
            NotFoundException           => (StatusCodes.Status404NotFound, "Resource not found"),
            Application.Exceptions.ValidationException => (StatusCodes.Status400BadRequest, "Validation failed"),
            InsufficientStockException  => (StatusCodes.Status409Conflict, "Stock conflict"),
            ConflictException           => (StatusCodes.Status409Conflict, "Conflict"),
            UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "Unauthorized"),
            DbUpdateConcurrencyException => (StatusCodes.Status409Conflict, "Concurrency conflict"),
            _                            => (StatusCodes.Status500InternalServerError, "An unexpected error occurred")
        };

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = ex.Message,
            Instance = context.Request.Path,
            Type = $"https://httpstatuses.com/{statusCode}"
        };
        if (ex is Application.Exceptions.ValidationException vex)
            problemDetails.Extensions["errors"] = vex.Errors;

        context.Response.ContentType = "application/problem+json";
        context.Response.StatusCode = statusCode;
        return context.Response.WriteAsJsonAsync(problemDetails);
    }
}
```

Unhandled `500` responses **never** leak stack traces to the client in `Production` (`Detail` is replaced with a generic message; the real exception is logged via `ILogger` with full stack trace and a correlation ID).

### 6.2 FluentValidation Pattern

Validators are auto-discovered and run via an MVC filter (`ValidationFilter`) or explicitly injected `IValidator<T>` inside services — **service-level invocation is preferred** so business services remain testable independent of the MVC pipeline:

```csharp
public class ProductCreateDtoValidator : AbstractValidator<ProductCreateDto>
{
    public ProductCreateDtoValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Price).GreaterThanOrEqualTo(0);
        RuleFor(x => x.StockQuantity).GreaterThanOrEqualTo(0);
        RuleFor(x => x.SKU).NotEmpty().MaximumLength(50);
        RuleFor(x => x.CategoryId).GreaterThan(0);
    }
}
```

### 6.3 Generic Repository & Unit of Work Contract

```csharp
public interface IRepository<T> where T : class
{
    Task<T?> GetByIdAsync(int id);
    Task<IReadOnlyList<T>> ListAllAsync();
    IQueryable<T> Query();                 // for composable filtering in services
    Task AddAsync(T entity);
    void Update(T entity);
    void Remove(T entity);
}

public interface IUnitOfWork : IAsyncDisposable
{
    IRepository<Product> Products { get; }
    IRepository<Category> Categories { get; }
    IRepository<Cart> Carts { get; }
    IRepository<Order> Orders { get; }
    Task<int> SaveChangesAsync();
    Task<IDbContextTransaction> BeginTransactionAsync();
    IExecutionStrategy CreateExecutionStrategy();
}
```

> **Critical rule reiterated from §1.3:** No `Service` class ever holds a constructor-injected `AppDbContext`. Every service depends solely on `IUnitOfWork` / `IRepository<T>`. This is what keeps unit tests free of any real database — `IUnitOfWork` mocks entirely.

### 6.4 `IFileStorageService` — Local Disk MVP, Cloud-Ready Interface

```csharp
public interface IFileStorageService
{
    Task<string> SaveFileAsync(Stream fileStream, string fileName, string containerName);
    Task DeleteFileAsync(string fileUrl);
}
```

`LocalFileStorageService` (MVP implementation) writes to `wwwroot/uploads/{containerName}/{guid}-{fileName}` and returns a relative URL served by `app.UseStaticFiles()`. Swapping to `AzureBlobStorageService` later requires **zero changes** to `ProductService` — only a new DI registration in `Program.cs`.

### 6.5 `IPaymentService` — Mock MVP, Real-Gateway-Ready Interface

```csharp
public interface IPaymentService
{
    Task<PaymentResult> AuthorizeAsync(Payment payment);
    Task<PaymentResult> RefundAsync(int paymentId);
}
```

`MockPaymentService` always returns `PaymentResult.Succeeded` after a simulated delay — enabling full checkout-flow testing without any payment provider credentials. A real `StripePaymentService` implementing the same interface is a drop-in swap.

### 6.6 Security Fundamentals

|Concern|Implementation|
|---|---|
|**CORS**|Named policy `"AllowedClients"` — explicit origin allowlist from `appsettings.json` (`AllowedOrigins` array). `AllowAnyOrigin()` is never used in combination with `AllowCredentials()`.|
|**Rate Limiting**|Built-in `Microsoft.AspNetCore.RateLimiting` — fixed-window limiter, 100 requests/minute per IP on anonymous endpoints (`/api/auth/*`), stricter 10/minute on `/api/auth/login` and `/api/auth/register` to blunt credential-stuffing/spam.|
|**Configuration Management**|JWT signing key, connection string, and any secrets loaded via `IConfiguration` + **User Secrets** in Development, **environment variables** in Production — never committed to `appsettings.json`.|
|**Cross-user resource access**|Confirmed pattern: `GET /api/orders/{id}` where the order belongs to another user returns **`404 Not Found`, never `403 Forbidden`** — this avoids confirming to an attacker that the resource ID exists at all (information-disclosure minimization).|
|**Password policy**|Enforced via `IdentityOptions.Password` (min length 8, require digit, require uppercase, require non-alphanumeric) configured in `Program.cs`.|
|**Correct data never leaks in public DTOs**|e.g., `ApplicationUser.PasswordHash` and Identity security stamps are never mapped into any DTO — `MappingProfile` explicitly whitelists only public-safe fields.|
|**HTTPS enforcement**|`app.UseHttpsRedirection()` + `Strict-Transport-Security` header via `app.UseHsts()` in non-Development environments.|

---

## 7. Step-by-Step Developer Implementation Roadmap

### Phase 0 — Solution Scaffolding

1. Create the solution and five projects exactly as structured in §2.1 (`dotnet new sln`, `dotnet new classlib` × 4, `dotnet new webapi` × 1).
2. Wire project references per the dependency table in §2.2 (`dotnet add reference`).
3. Add NuGet packages: `Microsoft.EntityFrameworkCore.SqlServer`, `Microsoft.EntityFrameworkCore.Design` (Infrastructure); `Microsoft.AspNetCore.Identity.EntityFrameworkCore`, `Microsoft.AspNetCore.Authentication.JwtBearer` (Infrastructure/API); `FluentValidation.AspNetCore`, `AutoMapper` (Application); `Swashbuckle.AspNetCore` (API).
4. Verify: solution builds with zero warnings before writing a single entity.

### Phase 1 — Domain & Persistence Foundation

1. Write all entities from §3.2–3.5 in `ECommerce.Domain`.
2. Write all `IEntityTypeConfiguration<T>` classes and `AppDbContext` (extending `IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>`) in `ECommerce.Infrastructure`.
3. Register `AppDbContext` in `Program.cs` with the SQL Server connection string from `appsettings.json`.
4. **Verify migrations:** run `dotnet ef migrations add InitialCreate -p ECommerce.Infrastructure -s ECommerce.API`, inspect the generated migration file line-by-line to confirm every FK, index, and `CHECK` constraint from §3 is present, then `dotnet ef database update`. Open the resulting database in SQL Server Management Studio / Azure Data Studio and manually confirm the ERD in §3.7 matches reality before writing any repository code.

### Phase 2 — Repository, Unit of Work, and Identity Seeding

1. Implement `Repository<T>` and `UnitOfWork` in `ECommerce.Infrastructure`.
2. Implement `IdentitySeeder` to create `Admin`/`Customer` roles and one default admin user on startup (`app.Services.CreateScope()` in `Program.cs`).
3. **Verify:** write a throwaway console test or integration test that adds and retrieves one `Category` through `IUnitOfWork` — confirms the DI graph and DbContext scoping work end-to-end before layering business logic on top.

### Phase 3 — Authentication & JWT

1. Implement `TokenService` (access token generation with claims: `sub`, `email`, `role[]`; refresh token generation and persistence).
2. Implement `AuthService` (register/login/refresh/logout per §4.1).
3. Configure JWT Bearer authentication middleware in `Program.cs` (`TokenValidationParameters` — validate issuer, audience, lifetime, signing key).
4. **Verify via Swagger:** enable the "Authorize" button with a Bearer scheme in `Swashbuckle` config; register a user, log in, copy the access token into Swagger's Authorize dialog, and confirm a `[Authorize]`-protected test endpoint returns `200` instead of `401`.

### Phase 4 — Catalog (Categories & Products)

1. Implement `CategoryService`/`ProductService`, their DTOs, validators, and `MappingProfile` entries.
2. Implement `CategoriesController` and `ProductsController` per the endpoint matrix in §5.
3. Implement `LocalFileStorageService` and the product image upload endpoint.
4. **Verify via Swagger:** create a category, create a product under it, upload an image, then hit `GET /api/products?searchTerm=...&sortBy=price&pageNumber=1` and confirm pagination metadata and filtering behave correctly with 15–20 seeded products (write a small seeding script or use Swagger's "Try it out" repeatedly).

### Phase 5 — Shopping Cart

1. Implement `CartService` and `CartController` per §4.3.
2. **Verify:** as a logged-in `Customer`, add the same product twice (confirm quantity increments, not duplicate rows), attempt to add more than available stock (confirm `409`), remove an item, and clear the cart — all via Swagger with the Bearer token applied.

### Phase 6 — Checkout & Orders (highest-risk phase — build carefully)

1. Implement `OrderService.PlaceOrderAsync` exactly per the transaction pipeline in §4.4, using `CreateExecutionStrategy()` + explicit `BeginTransactionAsync()`.
2. Implement `OrdersController` (customer + admin endpoints) and the state-machine-enforced `UpdateStatusAsync`.
3. Implement `MockPaymentService`.
4. **Verify concurrency safety specifically:** seed a product with `StockQuantity = 1`. Open two separate Swagger/Postman sessions as two different users, both add that product to cart, and fire both checkout requests as close together as possible. Confirm exactly one succeeds with `201` and the other returns `409 Conflict` — this is the single most important test in the entire system, because it proves the `RowVersion` concurrency token and transaction wrapping are actually working, not just present in code.
5. Verify the full state machine: confirm `Pending → Shipped` (skipping `Confirmed`/`Processing`) is rejected with `400`, and that cancelling a `Confirmed` order restores `Product.StockQuantity`.

### Phase 7 — Cross-Cutting Hardening

1. Implement `ExceptionHandlingMiddleware` and register it **first** in the pipeline; retro-test every previously-built endpoint's error paths (invalid input, not-found, unauthorized) to confirm they now return proper `ProblemDetails` JSON instead of raw ASP.NET default error pages.
2. Add CORS policy, rate limiting, and confirm `Admin`-only endpoints correctly return `403` (not `401`) when hit by an authenticated `Customer` token, and `404` (not `403`) when a `Customer` requests another user's order (§6.6).
3. Move all secrets out of `appsettings.json` into User Secrets (`dotnet user-secrets`) and re-verify the app still runs.

### Phase 8 — Test Coverage & Final Verification

1. Write unit tests for `OrderService` (mocked `IUnitOfWork`) covering: successful checkout, insufficient stock, empty cart, invalid status transition.
2. Write integration tests using `WebApplicationFactory<Program>` against a test database (LocalDB or a Dockerized SQL Server instance) for the full register → login → add-to-cart → checkout happy path.
3. Full manual Swagger walkthrough of every row in the §5 endpoint matrix, confirming the documented status code is actually returned in each case.
4. Final review pass against the architectural rules in §1.3 and §2.2 — grep the codebase for any accidental `AppDbContext` injection into a `Service` class; there should be none.

---

_End of Specification. This document is the single source of truth for implementation — deviations from entity names, DTO shapes, or endpoint routes should be treated as a deliberate architectural decision, documented in a changelog, not a silent drift._