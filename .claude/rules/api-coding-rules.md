# API coding rules — controller, repository, DTO, model

Copy the shapes below exactly. Reference: Manufacturer Master in `StoreController.cs` / `StoreRepository.cs`.

## Naming
| Thing | Rule | Example |
|---|---|---|
| Route | lowerCamelCase method name | `createUpdateManufacturerMaster`, `getManufacturerList`, `updateRoleMasterStatus` |
| C# action / repo method | PascalCase, same words as the route | `CreateUpdateManufacturerMaster` |
| Request / response | `<Verb><Entity>Request` / `...Response` | `CreateUpdateManufacturerMasterRequest` |
| List row model | `<Entity>Model` / `<Entity>MasterModel` | `ManufacturerMasterModel` |
| Verbs | GET = reads with `[FromQuery]` filters · POST = `createUpdate…` / `save…` / searches with a body · PATCH = `update…Status`, approve/cancel, partial updates | no PUT / DELETE in this API |
| Cache key | `private const string <Entity>CacheKey = "_<Entity>Master_All";` | `_ManufacturerMaster_All` |
| SP parameters (anonymous object) | camelCase, `IpAddress` capital I | `hospId, manufacturerId, userId, IpAddress` |
| `AllGlobalValues` members | camelCase (legacy) | `globalValues.hospId` |

## Controller shape
```csharp
[HttpPost("createUpdateXMaster")]
[Authorize]
public IActionResult CreateUpdateXMaster([FromBody] CreateUpdateXMasterRequest request)
{
    _log.Info($"CreateUpdateXMaster called. XId={request.XId}, XName={request.XName}");

    if (!ModelState.IsValid || string.IsNullOrWhiteSpace(request.XName))
    {
        _log.Warn("Invalid model state for X master insert/update.");
        var alert = _messageService.GetMessageAndTypeByAlertCode("MODEL_VALIDATION_FAILED");
        return BadRequest(new { result = false, messageType = alert.Type, message = alert.Message, errors = ModelState });
    }

    if (request.IsActive != 0 && request.IsActive != 1)
    {
        var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
        return BadRequest(new { result = false, messageType = alert.Type, message = "IsActive must be 0 or 1", errors = new { isActive = request.IsActive } });
    }

    var globalValues = GlobalFunctions.GetGlobalValues(HttpContext);
    var serviceResult = _storeRepository.CreateUpdateXMaster(request, globalValues);

    if (serviceResult.Result) _log.Info($"X master operation completed: {serviceResult.Message}");
    else _log.Warn($"X master operation failed: {serviceResult.Message}");

    return StatusCode(serviceResult.StatusCode, new
    {
        result = serviceResult.Result,
        messageType = serviceResult.MessageType,
        message = serviceResult.Message,
        data = serviceResult.Data
    });
}
```
Rules:
- Attribute order: `[HttpX("route")]` then `[Authorize]`.
- Actions are **synchronous** (`IActionResult`), like the rest of the file.
- The controller only validates input, reads `globalValues`, calls the repository, logs, and wraps the result.
  No SQL, no cache, no business rule in the controller.
- Constructor-inject `I<Module>Repository` + `IResponseMessageService`; one static log4net `_log` per class.

## Repository shape
```csharp
public ServiceResult<CreateUpdateXMasterResponse> CreateUpdateXMaster(CreateUpdateXMasterRequest request, AllGlobalValues globalValues)
{
    try
    {
        _log.Info($"CreateUpdateXMaster called. XId={request.XId}");

        var result = _sqlHelper.DML("IU_XMaster", CommandType.StoredProcedure,
            new
            {
                hospId = globalValues.hospId,
                xId = request.XId,
                xName = request.XName?.Trim(),
                isActive = request.IsActive,
                userId = globalValues.userId,
                IpAddress = globalValues.ipAddress
            },
            new { Result = 0 });

        int resultValue = Convert.ToInt32(result);
        _distributedCache.Remove(XCacheKey);                       // ALWAYS after a write

        if (resultValue == -1) { /* RECORD_ALREADY_EXISTS, "X name already exists", 409 */ }
        if (resultValue == 0 && request.XId > 0) { /* DATA_NOT_FOUND, 404 */ }
        if (resultValue > 0)
        {
            bool isNew = request.XId == 0;
            var alert = _messageService.GetMessageAndTypeByAlertCode(isNew ? "DATA_SAVED_SUCCESSFULLY" : "DATA_UPDATED_SUCCESSFULLY");
            return ServiceResult<CreateUpdateXMasterResponse>.Success(new CreateUpdateXMasterResponse { XId = resultValue }, alert.Type, alert.Message, isNew ? 201 : 200);
        }
        /* OPERATION_FAILED, 500 */
    }
    catch (Exception ex)
    {
        LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
        var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
        return ServiceResult<CreateUpdateXMasterResponse>.Failure(alert.Type, alert.Message, 500);
    }
}
```
Rules:
- Signature: `public ServiceResult<T> VerbNoun(<Request> request, AllGlobalValues globalValues)`; list reads take
  their filters as nullable params (`int? xId = null, int? isActive = null`).
- The **whole** body is inside `try/catch`. In partial feature files use the private helpers
  `ServerError<T>(ex)` and `Invalid<T>(message)` (see `StoreRepository.StockReceiving.cs`) instead of repeating the catch block.
- Trim strings before sending; send `null` (not `""`) for empty optional values.
- Map DataTable rows by hand:
  `dataTable?.AsEnumerable().Select(row => new XModel { XId = row.Field<int>("XId"), XName = row.Field<string>("XName") ?? string.Empty }).ToList() ?? new List<XModel>()`.
  `row.Field<T>` must match the SQL type exactly (`int` vs `bit` vs `decimal`) — check the SP's SELECT.
- Business rule numbers from OLD (max qty, max lines, store type ids) are `private const` / `static readonly`
  fields with a comment naming the OLD source (`// OLD numbersOnly(..., 5)`).
- A business rejection from an SP is a **negative** `@Result`; map each code to a specific message with a
  `switch` expression (see `SaveStockReceiving`).

## DTO / model rules
```csharp
public class CreateUpdateXMasterRequest
{
    public int XId { get; set; } = 0;                         // 0 = insert, >0 = update

    [Required(ErrorMessage = "X name is required")]
    [StringLength(256, ErrorMessage = "X name cannot exceed 256 characters")]
    public string XName { get; set; }

    [StringLength(20, ErrorMessage = "Short code cannot exceed 20 characters")]
    public string? ShortCode { get; set; }

    [Required(ErrorMessage = "IsActive is required")]
    public int IsActive { get; set; } = 1;                     // 1 Active / 0 Inactive (int, not bool)
}
```
- PascalCase properties, DataAnnotations with explicit English messages; the lengths **must equal** the
  table column lengths and the frontend yup schema `max()`.
- Optional strings are `string?`; ids default `= 0`; status is `int IsActive = 1`.
- List models: plain properties (no annotations), audit columns as **strings** already formatted by the SP
  (`CreatedBy`, `CreatedOn` `dd-MM-yyyy`, `LastModifiedBy`, `LastModifiedOn`).
- Group a feature's classes under a comment banner: `// ================= Stock Receiving (GRN) =================`.

## Do not
- Do not change an existing endpoint's route, request shape or response shape — screens depend on them.
  Add an optional parameter with a default instead.
- Do not return raw `DataTable` / `DataSet` to the client; map to models.
- Do not hard-code success/error text that already has an alert code; only the specific duplicate/rejection text is written in code.
- Do not catch and swallow exceptions without `LogErrors.WriteErrorLog`.
- Do not make old code "consistent" while doing an unrelated feature (no drive-by refactors).
