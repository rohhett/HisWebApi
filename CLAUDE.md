# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

HISWEBAPI is the backend for a Hospital Information System (HIS). It is an ASP.NET Core 8 Web API that talks to SQL Server through **stored procedures**. Its React/Vite frontend lives in a separate folder (`D:\@HISWEBAPI\Frontend\HIS-main`, base path `/GWSNHIS`), which has its own CLAUDE.md.

## Design rules and skills (`.claude/`)
Every new API must follow `.claude/rules/` (architecture, API coding rules, caching/logging/security, SQL) and be
built with the skills in `.claude/skills/`: `new-master-api`, `new-transaction-api`, `sql-change-script`,
`backend-review`. Overview: `.claude/README.md`. Reference build: Manufacturer Master.

## Commands

```bash
dotnet restore
dotnet build
dotnet run                      # launchSettings: http://localhost:5257 (Kestrel profile) / :11350 (IIS Express)
dotnet publish -c Release /p:PublishProfile=FolderProfile   # outputs to D:\@HISWEBAPI\Published Code\HIS_API
```

- Swagger UI is set up in `Configuration/SwaggerConfiguration.cs`.
- There is **no test project** and no linter config. Check changes with `dotnet build` and by calling the endpoint.
- `dotnet-ef` is listed in `.config/dotnet-tools.json`, but EF Core is **not** used for data access, so there are no migrations.

To run locally you need:
- SQL Server: database `GWT_HIS_API` on `ROHIT\SQLEXPRESS` (Windows auth). Backups are in `D:\@HISWEBAPI\`.
- Redis on `localhost:6379` (override with `Redis:Configuration`). Most read endpoints cache through Redis.
- The folder at `LoggingFlags:LogBasePath` (log4net output, configured in `log4net.config`) and the folder at `DMS:RootPath` (uploaded files).

## Architecture

The request flow is **Controller → Repository → `ICustomSqlHelper` → stored procedure**. There is no separate business-service layer for domain logic: repositories hold both the logic and the data access. `Services/` only holds cross-cutting services (JWT, SMS, Email, response messages, lab-report PDF).

All DI registration is in `Configuration/DependencyInjection.cs`. A new repository or service has to be registered there.

### Domain modules
Each module has one file per layer: `Controllers/XController.cs`, `Repositories/{Interfaces,Implementations}/XRepository.cs`, `DTO/XDTO.cs` (request/response classes) and `Models/XModel.cs` (row models). The modules are Admin, User, Home, PageConfig, Patient, Doctor, Lab, LabReport (plus `PatientLabReport`), EMR, IPD and Store. These files are very large (AdminRepository is about 12k lines), so search with Grep for the method or SP name instead of reading the whole file.

### Data access: `Data/Helpers/CustomSqlHelper.cs`
- Parameters are passed as **anonymous objects**. Each property becomes `@PropertyName`, and nulls become `DBNull`.
- Passing any non-null `outParameters` object (by convention `new { Result = 0 }`) adds an output param `@Result VARCHAR(50)`.
- `GetDataTable`/`GetDataSet` are used for reads. Rows are mapped by hand with `row.Field<T>("Col")` or `Utilities/Extensions/DataTableExtensions.cs`.
- Transactions are passed explicitly: `CustomSqlHelper.getSqlTransaction(con)` plus the `DML(tnx, ...)` overloads.
- Return values differ by overload. The non-transactional `DML(sqlCmd, ...)` with out params returns the integer value of `@Result`, falling back to rows affected when `@Result` is NULL. The transactional `DML(tnx, ...)` overloads return `ExecuteScalar()`, so those SPs must `SELECT @Result` at the end. Without out params, both return rows affected.
- SP naming: `S_` = select/read (e.g. `S_GetAllSampleTypeMaster`), `IU_` = insert/update (e.g. `IU_SampleTypeMaster`). The SP's return value encodes the result, e.g. `-1` = duplicate and `>0` = new/updated id.
- The SQL scripts in `Database/` are reference/seed scripts only. SP source code lives in the database, not in this repository.

### Standard endpoint pattern (copy it for new endpoints)
1. Controller: route is `api/[controller]` with camelCase action routes (`[HttpPost("createUpdateSampleTypeMaster")]`). Add `[Authorize]`. Check `ModelState` and return `BadRequest` with an alert code on failure. Get user context with `GlobalFunctions.GetGlobalValues(HttpContext)`, which returns `AllGlobalValues` with `hospId`, `userId`, `userName`, `name` and `ipAddress` from JWT claims. Pass that into the repository.
2. Repository returns `ServiceResult<T>` (`Models/Configuration/ServiceResult.cs`) through `ServiceResult<T>.Success(data, type, message, statusCode)` or `.Failure(type, message, statusCode)`. Wrap the body in try/catch, and in the catch call `LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}")` and return alert `SERVER_ERROR_FOUND` with status 500.
3. Controller returns `StatusCode(serviceResult.StatusCode, new { result, messageType, message, data })`. The frontend expects this exact envelope.

### User-facing messages
Messages come from the DB, not from code: `IResponseMessageService.GetMessageAndTypeByAlertCode("CODE")` returns `(Type, Message)` from SP `S_GetAllResponseMessages` and caches it in Redis for 6 hours. Common codes are `DATA_SAVED_SUCCESSFULLY`, `DATA_UPDATED_SUCCESSFULLY`, `RECORD_ALREADY_EXISTS`, `OPERATION_FAILED`, `SERVER_ERROR_FOUND`, `MODEL_VALIDATION_FAILED` and `INVALID_PARAMETER`. A new code must be added to the DB table.

### Caching (Redis via `IDistributedCache`)
Master/list reads cache the **full** list under a fixed key (e.g. `_SampleTypeMaster_All`), filter it in memory and store it as JSON. Every write that changes that data must invalidate the key, either with `_distributedCache.Remove(key)` or with `GlobalFunctions.ClearCacheByPattern(configuration, "_Prefix_*")` for wildcard keys such as per-user menu mappings. If you forget the invalidation, the UI keeps showing stale data.

### Auth and cross-cutting
- JWT bearer setup is in `Configuration/JwtConfigurationExtensions.cs` and tokens are issued by `Services/Implementations/JwtService.cs`. The claims are `userId`, `hospId`, `userName` and `name`. Expiry and refresh settings are under `JwtSettings`. Login is `POST api/User/userLogin`.
- Every query is scoped by **`hospId`** (multi-hospital). Always pass `globalValues.hospId` and `userId`/`ipAddress` for auditing to `IU_` procedures.
- `Middleware/ClientTypeMiddleware.cs` tags each request as `Web` or `Mobile` (path `/mob/...` or the `X-Client-Type` header) for log4net output. Middleware order in `Program.cs` matters: ClientType → Authentication → Authorization.
- CORS policy `_myAllowSpecificOrigins` reads `AllowedOrigins` from appsettings. A new frontend host must be added there.
- File uploads go through `Utilities/Helpers/FileUploadHelper.cs`, which uses the `DMS` config (root path, subfolders, allowed extensions, size limit).
- Password rules come from the `PasswordPolicy` config section and are enforced by `Validators/Attributes/PasswordPolicyAttribute.cs`.
- PDF, barcode and QR generation (lab reports) use DLLs in `Libs/` (HiQPdf, iTextSharp, BarcodeStandard, MessagingToolkit.QRCode). These are referenced by HintPath, not from NuGet.
- Every class logs with log4net through `LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType)`. Use `_log.Info`/`Warn` for flow and `LogErrors.WriteErrorLog` for exceptions.
