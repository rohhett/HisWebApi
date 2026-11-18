---
name: new-master-api
description: Build the backend for a new lookup/master screen (list + create/update) in HISWEBAPI exactly like Manufacturer Master - SQL script, DTO, model, repository, interface, controller, Redis cache. Use when the owner asks for a new master, lookup list, or "same as manufacturer master".
---

# New master API (list + create/update)

Reference build: **Manufacturer Master** (2026-11-13). Open these before writing anything and copy their shape:
- `Database/2026-11-13_ManufacturerMaster.sql`
- `Controllers/StoreController.cs` → `CreateUpdateManufacturerMaster`, `GetManufacturerList` (Grep the names)
- `Repositories/Implementations/StoreRepository.cs` → same two methods + `ManufacturerCacheKey`
- `Repositories/Interfaces/IStoreRepository.cs`, `DTO/StoreDTO.cs` (`CreateUpdateManufacturerMasterRequest/Response`), `Models/StoreModel.cs` (`ManufacturerMasterModel`)

Rules that apply: `.claude/rules/*.md` (architecture, api-coding-rules, caching-logging-security, sql-rules).

## Steps

1. **Collect the field list first** (never guess). From the OLD page (if migrating: `Claude\HIS_Knowledge_Base`
   → OLD view, BL, procedure) or the owner's design: field name, type, max length, mandatory, default,
   unique rule, dropdown source. Check `GWT_HIS_API` for an existing table/proc with the same name and diff it.

2. **SQL script** `Database/YYYY-MM-DD_<X>Master.sql` (use the `sql-change-script` skill):
   - table `IF NOT EXISTS` with standard audit columns;
   - `IU_<X>Master` (`CREATE OR ALTER`): trims, unique-name check → `@Result = -1`, insert → `SCOPE_IDENTITY()`,
     unknown id → `0`, update → the id;
   - `S_Get<X>List` without parameters: all rows + `IsActive` + audit columns (`dd-MM-yyyy`, user names), ordered by name;
   - menu row in `NavigationSubMenuMaster` (URL = frontend route key) if it is its own page
     (not needed when it becomes a tab of an existing page such as Medical Store Master).
   - OLD rows to copy → separate `…_OldDataCopy.sql`.

3. **DTO** (`DTO/<Module>DTO.cs`): `CreateUpdate<X>MasterRequest` (`<X>Id = 0`, annotated fields with lengths
   equal to the columns, `int IsActive = 1`) + `CreateUpdate<X>MasterResponse { <X>Id }`.

4. **Model** (`Models/<Module>Model.cs`): `<X>MasterModel` with every column the list SP returns (audit as strings).

5. **Interface** (`I<Module>Repository.cs`):
   ```csharp
   ServiceResult<CreateUpdate<X>MasterResponse> CreateUpdate<X>Master(CreateUpdate<X>MasterRequest request, AllGlobalValues globalValues);
   ServiceResult<IEnumerable<<X>MasterModel>> Get<X>List(int? <x>Id = null, int? isActive = null);
   ```

6. **Repository**: `private const string <X>CacheKey = "_<X>Master_All";` next to the other keys;
   `CreateUpdate<X>Master` = `DML("IU_<X>Master", …, new { Result = 0 })` → `Remove(cacheKey)` → map −1 / 0 / >0;
   `Get<X>List` = cache get → else `GetDataTable("S_Get<X>List")` + map + cache → filter → 404 when empty.

7. **Controller**: `[HttpPost("createUpdate<X>Master")] [Authorize]` and `[HttpGet("get<X>List")] [Authorize]`
   with the ModelState / IsActive checks and the standard envelope.

8. **Build**: `dotnet build` must pass with no new warnings in the touched files.

9. **Test** (API running on :5257, SQL script applied locally): insert, update, duplicate name (409),
   unknown id (404), list with `isActive=1`, list after a save shows the change (cache cleared).

10. **Frontend**: continue with the `new-master-screen` skill in `D:\@HISWEBAPI\Frontend\HIS-main\.claude\skills\`.

11. **Record**: dated entry in `Claude\HIS_Knowledge_Base\Development_Decisions.md` (what was built, script names,
    OLD rules kept / differences), and tell the owner which scripts to run on the server, in order.

## Done means
- Script re-runs cleanly; procs return −1 / 0 / id as documented in the header.
- Lengths identical in table, DTO annotations and frontend yup schema.
- Cache key removed on every write path.
- Nothing else in the module changed.
