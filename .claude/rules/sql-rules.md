---
paths:
  - "Database/**/*.sql"
---

# SQL rules (database `GWT_HIS_API`)

## Change scripts
- Every table / procedure / seed / menu change is a **re-runnable** script `Database/YYYY-MM-DD_<Feature>.sql`.
  The owner applies it to the server by hand — the script is the only record of the change.
- Header comment: feature, date, target DB, "Re-runnable: yes", numbered list of objects, what to do after running
  (e.g. "assign the page to users/roles in User Authorization"). See `2026-11-13_ManufacturerMaster.sql`.
- Tables: `IF OBJECT_ID('dbo.X','U') IS NULL CREATE TABLE …`; new columns: `IF COL_LENGTH('dbo.X','Col') IS NULL ALTER TABLE … ADD …`.
- Procedures: `CREATE OR ALTER PROCEDURE`. Seed / menu rows: `IF NOT EXISTS (…) INSERT …`.
- OLD-data copies go in a separate `…_OldDataCopy.sql` (OLD ids kept, `SET IDENTITY_INSERT`, idempotent).
- Test locally: run the script twice (must not fail), test the `IU_` proc inside `BEGIN TRAN … ROLLBACK`.

## Tables
- Schema `dbo`; PascalCase singular names with a suffix: `…Master`, `…Details`, `…Mapping`, `…_Log`.
- PK `<Entity>Id int IDENTITY(1,1)` with `CONSTRAINT PK_<Table> PRIMARY KEY CLUSTERED`.
- Standard columns: `HospId`, (`BranchId` when branch-scoped), `IsActive int DEFAULT 1`, `CreatedBy int`,
  `CreatedOn datetime DEFAULT GETDATE()`, `LastModifiedBy`, `LastModifiedOn`, `IpAddress nvarchar(20)`.
- Named default constraints `DF_<Table>_<Column>`. Text `nvarchar(n)`, money/qty `decimal(p,s)`.
- No FK constraints are used in this DB; write joins explicitly.

## Procedures
- Prefixes: `S_` read, `IU_` insert/update (`@<entity>Id = 0` ⇒ insert), `I_`, `U_`, `D_`. Never `sp_`, `MIS_`, `get…`.
- Parameters camelCase (`@hospId`, `@userId`, `@IpAddress`), optional ones with defaults (`= NULL`) so old callers keep working.
- Writes expose `@Result int = NULL OUTPUT`: `> 0` = id, `-1` = duplicate, `0` = unknown id on update,
  `-2, -3 …` = other business rejections (document each in the header).
- When the proc is called through the **transactional** `DML(tnx, …)` overload, end with `SELECT @Result;`
  (that overload returns `ExecuteScalar()`).
- Trim inputs in the proc (`LTRIM(RTRIM())`, `NULLIF(…,'')`).
- List procs for cached masters take **no parameters**, return all rows + audit columns formatted
  `FORMAT(…, 'dd-MM-yyyy')` and user names from `UserMaster`, ordered by name.
- Multi-line saves: lines come as one JSON parameter (`@itemsJson nvarchar(max)` read with `OPENJSON`).
- Transactions are owned by C# (Serializable). Use `BEGIN TRAN/TRY` inside a proc only if it is never called inside a C# transaction.
- Never concatenate parameters into dynamic SQL; use `sp_executesql` with parameters, `STRING_SPLIT` or `OPENJSON`.

## Before changing an existing procedure
Read its current text from the DB, find every caller (`Grep` the SP name in `Repositories/`, and
`Claude\HIS_Knowledge_Base\generated\code\sp_usage.tsv`), keep old parameters working, and state the impact.
A same-named proc in the OLD DB (`DEMO_HIS_NEW`) can have a different contract — diff, do not assume.

## Menu row for a new screen
Insert into `NavigationSubMenuMaster` (`TabId` looked up by tab name, `SubMenuName`, `URL` = the frontend
kebab-case route key, `SequenceNo`), guarded by `IF NOT EXISTS (… WHERE URL = '<key>')`, with a `PRINT` if the tab is missing.
