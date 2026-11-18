# Backend architecture (HISWEBAPI) — follow for every new API

## Request flow
```
React screen ──HTTP──> Controller ──> Repository (logic + data access) ──> ICustomSqlHelper ──> Stored procedure (GWT_HIS_API)
                          │                 │
                          │                 ├── IResponseMessageService  (alert code -> Type + Message, Redis 6 h)
                          │                 └── IDistributedCache        (Redis master-list cache)
                          └── GlobalFunctions.GetGlobalValues(HttpContext) -> AllGlobalValues { hospId, userId, userName, name, ipAddress }
```
- **No business-service layer.** The repository holds the business rules *and* the data access
  (the job `PMS.BLL` did in the OLD HIS). `Services/` is only for cross-cutting services
  (JWT, SMS, Email, response messages, lab-report PDF, token revocation). Never add `XService` for domain logic.
- **No EF Core, no inline SQL.** Every database call is a stored procedure through `ICustomSqlHelper`.
  (EF packages are referenced but unused — do not start using them.)
- **No AutoMapper, no MediatR, no new NuGet package** without the owner's approval.

## Folder placement (one file per layer per module)
| Layer | File | Notes |
|---|---|---|
| Controller | `Controllers/<Module>Controller.cs` | Modules: Admin, User, Home, PageConfig, Patient, Doctor, Lab, LabReport, EMR, IPD, Store |
| Repository interface | `Repositories/Interfaces/I<Module>Repository.cs` | group methods under a `// <Feature> (yyyy-MM-dd)` comment |
| Repository | `Repositories/Implementations/<Module>Repository.cs` | a big feature goes in a **partial** file `<Module>Repository.<Feature>.cs` (see `StoreRepository.StockReceiving.cs`, `.PurchaseRequest.cs`, `.PurchaseOrder.cs`) |
| Request / response classes | `DTO/<Module>DTO.cs` | `<Verb><Entity>Request`, `<Verb><Entity>Response` |
| Row models (lists) | `Models/<Module>Model.cs` | `<Entity>Model` / `<Entity>MasterModel` |
| Transactional entities | `Domain/*.cs` | ported OLD `PMS.DAL` classes that take a `SqlTransaction` |
| SQL change scripts | `Database/YYYY-MM-DD_<Feature>.sql` | re-runnable; the owner runs them on the server by hand |
| DI | `Configuration/DependencyInjection.cs` | a new repository/service must be `AddScoped` here |

A new endpoint goes into the **existing** module controller/repository. Only a genuinely new module gets a new
set of four files (+ DI registration). Files are large (AdminRepository ≈ 12k lines): find things with Grep
on the method or SP name, never read the whole file.

## Response contract (the frontend depends on it — never change it)
Every action returns
```csharp
return StatusCode(serviceResult.StatusCode, new
{
    result = serviceResult.Result,
    messageType = serviceResult.MessageType,
    message = serviceResult.Message,
    data = serviceResult.Data
});
```
plus `errors` on a 400. JSON is camelCase. The repository returns `ServiceResult<T>`
(`Models/Configuration/ServiceResult.cs`) built only with `ServiceResult<T>.Success(data, type, message, status)`
or `ServiceResult<T>.Failure(type, message, status)`.

| Situation | Alert code | HTTP |
|---|---|---|
| ModelState invalid | `MODEL_VALIDATION_FAILED` (+ `errors = ModelState`) | 400 |
| Bad query/body value | `INVALID_PARAMETER` (specific message) | 400 (inside a transaction save: 200 with `result=false`, see `Invalid<T>`) |
| Insert OK | `DATA_SAVED_SUCCESSFULLY` | 201 |
| Update OK | `DATA_UPDATED_SUCCESSFULLY` | 200 |
| Duplicate (SP `@Result = -1`) | `RECORD_ALREADY_EXISTS` + specific text ("Manufacturer name already exists") | 409 |
| Unknown id on update (SP `@Result = 0`) / empty list | `DATA_NOT_FOUND` | 404 |
| Any other non-positive result | `OPERATION_FAILED` | 500 |
| Exception | `SERVER_ERROR_FOUND` | 500 |

## Identity and scope
- Claims: `userId`, `hospId`, `userName`, `name`, `sessionId`. `branchId` and `roleId` are **not** in the token:
  they come from the client as query/body parameters.
- Every read and write is scoped by `globalValues.hospId`; every `IU_`/`I_`/`U_` call also passes
  `userId` and `IpAddress` for the audit columns.
- `[Authorize]` on every endpoint. `[AllowAnonymous]` only for login / OTP / public lookups, and only with the owner's OK.

## Pipeline (`Program.cs`, order matters)
Swagger → HttpsRedirection → CORS `_myAllowSpecificOrigins` (from `AllowedOrigins`) → `ClientTypeMiddleware`
→ Authentication → Authorization → MapControllers. There is no global exception middleware: each repository
method catches its own exceptions.

## Build / run / check
`dotnet build` (no test project, no linter) · `dotnet run --launch-profile HISWEBAPI` → http://localhost:5257 ·
Swagger for manual calls. The local API usually runs in a Terminal tab — restart it after backend changes.
Needs SQL Server `GWT_HIS_API` (Windows auth) and Redis on `localhost:6379`.
