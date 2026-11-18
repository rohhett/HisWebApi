---
name: new-transaction-api
description: Build a HISWEBAPI save that writes a header plus lines (GRN, purchase request/order, bills, issues) in one C# transaction, with status changes and approvals, following the Stock Receiving / Purchase Request pattern. Use for any multi-row or multi-table save.
---

# New transaction API (header + lines, one transaction)

Reference builds — read before writing:
- `Repositories/Implementations/StoreRepository.StockReceiving.cs` (`SaveStockReceiving`, `Invalid<T>`, `ServerError<T>`)
- `Repositories/Implementations/StoreRepository.PurchaseRequest.cs` (send / approve / cancel, approval levels)
- `Repositories/Implementations/StoreRepository.PurchaseOrder.cs`
- SQL: `Database/2026-11-14_StockReceiving.sql`, `Database/2026-11-16_PurchaseRequest.sql`

## Shape

1. **Own partial file** `Repositories/Implementations/<Module>Repository.<Feature>.cs`:
   ```csharp
   /// <summary>
   /// <Feature> (yyyy-MM-dd). Rules ported from OLD <Controller>/<Page> (<BL methods>).
   /// SPs: script Database/yyyy-MM-dd_<Feature>.sql.
   /// </summary>
   public partial class <Module>Repository
   {
       private const int Max<Feature>Lines = 500;        // limits from OLD, with a comment naming the OLD source
       #region Reads
       #endregion
       #region Save
       #endregion
   }
   ```
   Interface methods go in `I<Module>Repository.cs` under `// <Feature> (yyyy-MM-dd)`.
   DTO/model classes go in the module DTO/Model files under a `// ===== <Feature> =====` banner.

2. **Validate in C# first** (before opening a connection) and return `Invalid<T>("specific message")` —
   result=false with HTTP 200 so the screen shows the message as a toast. Recalculate amounts on the server;
   never trust totals sent by the client.

3. **Write in one transaction**:
   ```csharp
   using (var con = new SqlConnection(_configuration.GetConnectionString("ConnectionString")))
   {
       con.Open();
       using (SqlTransaction tnx = CustomSqlHelper.getSqlTransaction(con))   // Serializable
       {
           object raw = _sqlHelper.DML(tnx, "IU_<Feature>", CommandType.StoredProcedure, new
           {
               hospId = globalValues.hospId,
               branchId = request.BranchId,
               …,
               itemsJson = System.Text.Json.JsonSerializer.Serialize(lines),   // lines as one JSON parameter
               userId = globalValues.userId,
               ipAddress = globalValues.ipAddress
           }, new { Result = 0 });

           int id = raw == null || raw == DBNull.Value ? 0 : Convert.ToInt32(raw);
           if (id <= 0)
           {
               tnx.Rollback();
               string msg = id switch { -1 => "…already exists…", -2 => "…", _ => "<Feature> could not be saved" };
               var alert = _messageService.GetMessageAndTypeByAlertCode(id == -1 ? "RECORD_ALREADY_EXISTS" : "OPERATION_FAILED");
               return ServiceResult<T>.Failure(alert.Type, msg, 200);
           }
           tnx.Commit();
       }
   }
   ```
   - The transactional `DML(tnx, …)` returns `ExecuteScalar()` → the SP must `SELECT @Result` on **every** exit path.
   - Lines are read in the SP with `OPENJSON`; the SP does header + lines + stock/ledger rows together.
   - Reusing an OLD `Domain/*` entity (FinancialTransactions, Receipts, …) inside the same `tnx` is allowed.

4. **Status flow**: Draft → Posted / Sent → Approved / Cancelled. Each state change is its own PATCH endpoint
   (`update<Feature>Status`, `approve<Feature>`, `cancel<Feature>`) with a required remark where OLD required one.
   The SP checks the current state and the approver's level (`ApprovalAuthorityMaster`) and returns a negative code otherwise.

5. **Reads**: list with date/status/branch filters (not cached), `get<Feature>Details` returning header + lines
   (`GetDataSet` with two tables), search endpoints capped with `TOP n`.

6. Controller endpoints, build, test (happy path, every negative code, rollback leaves no partial rows),
   decision-log entry and the SQL script list for the owner — same as `new-master-api` steps 7–11.
   Frontend: `new-transaction-screen` skill in the frontend `.claude`.
