# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

ShopeeMini backend: a mini e-commerce REST API (.NET 8 Web API + MySQL 8 + EF Core Code-First) with two roles, `Admin` (shop owner) and `User` (buyer). The original requirements live in `../ShopeeMiniBackEnd.md`, one level above this repo. The sibling `../front-end/` folder consumes this API. User-facing strings (API messages, exception messages, docs) are written in **Vietnamese**; keep new ones consistent.

## Commands

All commands run from the repo root (`back-end/`). There is no test project and no linter configured.

```bash
docker compose up -d                                   # MySQL 8 container "shopeemini-mysql", db "shopee_mini", port 3306
dotnet build ShopeeMini.slnx                           # .slnx needs a recent SDK (9.0.200+); projects target net8.0
dotnet run --project ShopeeMiniBackEnd/ShopeeMini.API/ShopeeMini.API.csproj
# → http://localhost:5019 / https://localhost:7031, Swagger at /swagger (Development only)

# EF Core migrations (DbContext lives in Infrastructure, startup project is API)
dotnet ef migrations add <Name> \
  --project ShopeeMiniBackEnd/ShopeeMini.Infrastructure/ShopeeMini.Infrastructure.csproj \
  --startup-project ShopeeMiniBackEnd/ShopeeMini.API/ShopeeMini.API.csproj \
  --output-dir Persistence/Migrations
dotnet ef database update --project <same> --startup-project <same>
```

Migrations are also applied automatically at startup: `DbSeeder.SeedAsync` calls `Database.MigrateAsync()`, then seeds the admin `admin@shopeemini.local` / `Admin@123` with its shop, plus 4 root categories. A running API process locks the build output, so stop it before rebuilding.

## Architecture

Clean Architecture in `ShopeeMiniBackEnd/`, with dependencies pointing inward: `API → Application/Infrastructure → Domain`, and `Infrastructure → Application`.

- **Domain**: POCO entities (all inherit `BaseEntity`: `long Id`, UTC `CreatedAt`/`UpdatedAt`) and enums. No EF attributes.
- **Application**: one folder per feature under `Features/<Feature>/` (`I<X>Service`, `<X>Service`, `Dtos/`, `Validators/`). Services take `userId` as a parameter from the controller and never touch `HttpContext`. They talk to the DB through `IAppDbContext`, which exposes `DbSet<T>` and `Database`, so Application *does* depend on EF Core APIs (`Include`, `AsNoTracking`, `ExecuteUpdateAsync`, transactions) but not on the MySQL provider. There are no repositories.
- **Infrastructure**: `AppDbContext` (also implements `IAppDbContext`, and stamps `CreatedAt`/`UpdatedAt` in `SaveChangesAsync`), Fluent API configs in `Persistence/Configurations/*.cs` (lengths, precision, indexes, delete behaviour; enums stored as strings), JWT, BCrypt, and the invoice JSON writer.
- **API**: thin controllers inheriting `ApiControllerBase`. Several files hold two controller classes, e.g. `CategoriesController.cs` contains both the public and the `/api/admin/categories` controllers. Authorization uses `[Authorize(Roles = nameof(UserRole.Admin|User))]`.

### Cross-cutting conventions (spread across several files)

- **Response envelope**: every action returns `SuccessResponse(data, message, statusCode)`, which produces `ApiResponse<T>` `{ success, data, message, errors }`. Never build error responses in controllers or services. Instead **throw** an exception from `Application/Common/Exceptions/AppExceptions.cs` (`BadRequestException` → 400, `UnauthorizedAppException` → 401, `ForbiddenException` → 403, `NotFoundException` → 404, `ConflictException` → 409). `GlobalExceptionMiddleware` maps them; anything else becomes a generic 500.
- **Validation**: the global `ValidationFilter` resolves `IValidator<T>` for each action argument and throws on failure. Adding a validator class in `Features/*/Validators` is enough, because they are auto-registered via `AddValidatorsFromAssembly`. Do not validate inside controllers.
- **Ownership checks** happen in services: admin operations verify `Product.Shop.OwnerUserId == userId` (otherwise 403); user-owned rows are filtered by `UserId` in the query, so other users' rows return 404.
- **Pagination**: list queries take a record inheriting `PaginationQuery` (`page`, `pageSize` capped at 100, `fromDate`, `toDate`) and return `PagedResult<T>`. Pattern: `LongCountAsync` + `OrderByDescending(CreatedAt)` + `Skip/Take` + server-side `Select` projection.
- **Pricing**: `Application/Common/DiscountCalculator` picks the max active discount percent per SKU (`IsActive && StartAt <= now <= EndAt`), never stacked and never applied at order level. List endpoints recompute the same logic inside the LINQ projection, so keep the two in sync if you change it.
- **SPU/SKU**: `Product` is the SPU shell (`IsPublic`, new products start hidden); `ProductSku` carries `Price` and `StockQuantity`. `OrderItem` stores name, price and discount *snapshots* plus a denormalized `ShopId` (no FK).
- **Registration**: new services go in `Application/DependencyInjection.cs` (scoped) or `Infrastructure/DependencyInjection.cs`. Middleware order and filters are set up in `API/Program.cs`.

### Critical flows

- **Checkout** (`OrderService.CheckoutAsync`): inside one transaction, stock is decremented per SKU with `ExecuteUpdateAsync` and the condition `StockQuantity >= qty`; 0 affected rows means rollback everything. On failure it then records a `Failed` order plus a notification and keeps the cart. On success it creates a `Paid` order, deletes the cart, notifies the buyer and each shop owner, commits, and only **after** commit writes `App_Data/invoices/{OrderCode}.json` and saves `InvoiceJsonPath`.
- **Idempotency**: `POST /api/orders/checkout` has `[ServiceFilter(typeof(IdempotencyFilter))]`, which requires the `Idempotency-Key` header and replays the cached 2xx response from `IdempotencyRecords`, keyed by `(UserId, Key, Path)`. `POST /api/orders/checkout-no-idempotency` is an intentional demo endpoint without the filter; do not "fix" or remove it.
- **Hiding a product** (`AdminProductService.SetProductStatusAsync`): in one transaction it removes that product's SKUs from every cart and sends a `ProductHiddenRemovedFromCart` notification to each affected user.

## Coding rules (from the project requirements)

1. SOLID / Clean Code.
2. All I/O (DB, file, external) is fully async/await.
3. Always validate input DTOs with FluentValidation before running logic.
4. Money and prices use `decimal` (DB: `decimal(18,2)`; percent: `decimal(5,2)`).
5. Read-only EF queries always use `.AsNoTracking()`.
6. Stock deduction in checkout must use a transaction plus an atomic conditional update (`StockQuantity >= Quantity`).

All timestamps are UTC (`DateTime.UtcNow`).

## Docs

`docs/` holds detailed Vietnamese documentation. Update the relevant file when you change behaviour:
- `architecture.md`, `api-specification.md` (every endpoint, validation rules and error messages), `database-schema.md`
- `IDEMPOTENCY.md`, `LOG_LEVELS.md` (Serilog levels are hard-coded in `Program.cs`; the `appsettings` log levels have no effect), `MIGRATIONS.md`, `ACCOUNTS.md` (test accounts)

The README still lists Swagger on port 5080; the actual ports are the ones in `launchSettings.json` (5019/7031).
