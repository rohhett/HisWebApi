# Caching, logging, messages and security rules

## Redis cache (`IDistributedCache`)
- **Master lists:** the SP returns *all* rows (no parameters); the repository caches the full list as JSON
  (`System.Text.Json`) under `"_<Entity>Master_All"` with `new DistributedCacheEntryOptions()` (no expiry),
  then filters in memory (`xId`, `isActive`, …). Only cache when the list is not empty.
- **Every write that changes cached data removes the key** (`_distributedCache.Remove(XCacheKey)`), also when the
  SP returned a rejection. Wildcard keys (per-user menus) → `GlobalFunctions.ClearCacheByPattern(_configuration, "_Prefix_*")`.
- Per-user scratch data lives only in Redis with the user in the key, e.g. `_GrnUnsaved_{hospId}_{userId}`
  (userId from the JWT, never from the request).
- Transaction lists (GRN, PR, PO, bills) are **not** cached — they change all the time.
- If one table feeds two caches (e.g. Salt: `_SaltNameMaster_AllRows` and `_SaltNameMaster_All`), clear both.

## Response messages
- Text and type come from the DB: `_messageService.GetMessageAndTypeByAlertCode("CODE")` → `(Type, Message)`.
- Use existing codes: `DATA_SAVED_SUCCESSFULLY`, `DATA_UPDATED_SUCCESSFULLY`, `RECORD_ALREADY_EXISTS`,
  `DATA_NOT_FOUND`, `OPERATION_FAILED`, `SERVER_ERROR_FOUND`, `MODEL_VALIDATION_FAILED`, `INVALID_PARAMETER`.
- A new code needs a row in the response-message table, delivered in the feature's SQL script.
- List reads that succeed return type `"Info"` and a count message: `$"{list.Count} manufacturer(s) retrieved successfully"`.

## Logging (log4net)
- One per class: `private static readonly ILog _log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);`
- `_log.Info` on entry with the key ids, `_log.Info` / `_log.Warn` on the outcome, `_log.Error` for an unexpected SP result.
- Exceptions only through `LogErrors.WriteErrorLog(ex, "<Class>.<Method>")`.
- Never log passwords, OTPs, tokens or full patient documents.

## Security
- `[Authorize]` on every endpoint. User / hospital identity only from `GlobalFunctions.GetGlobalValues(HttpContext)`,
  never from the request body.
- Always pass `hospId` to procedures; `branchId` / `roleId` come from the client and the SP must check them
  where it matters (e.g. "only a Draft GRN can be edited").
- Rights such as `CanEditStoreItemMaster` are checked in the screen; when the owner asks for server-side
  enforcement, check them in the repository before the write.
- Parameters always through `ICustomSqlHelper` anonymous objects — never build SQL strings from input.
- Files: only through `Utilities/Helpers/FileUploadHelper.cs` (DMS root, allowed extensions, size limit).
- Passwords: `PasswordHasher` (PBKDF2) + `PasswordPolicyAttribute`. Do not invent another scheme.
- Secrets stay in `appsettings*.json` / environment; do not add new secrets to code.
- New frontend host → add it to `AllowedOrigins`, not a wider CORS policy.
