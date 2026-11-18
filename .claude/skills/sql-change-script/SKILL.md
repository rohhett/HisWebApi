---
name: sql-change-script
description: Write a re-runnable SQL change script for GWT_HIS_API in HISWEBAPI/Database (tables, columns, procedures, seed rows, menu rows, OLD data copy). Use whenever a table, procedure or data row has to change.
---

# SQL change script

Every database change is delivered as `Database/YYYY-MM-DD_<Feature>.sql` (today's date). The owner runs it
on the server by hand, so it must be safe to run more than once and must contain everything the feature needs.

## Template
```sql
/* =====================================================================
   <Feature>  —  YYYY-MM-DD
   Target DB : GWT_HIS_API (NEW HIS)
   Re-runnable: yes (IF NOT EXISTS / CREATE OR ALTER)

   Objects
     1. TABLE      dbo.<X>            (new | + columns A, B)
     2. PROCEDURE  dbo.IU_<X>         (@Result: >0 id, -1 duplicate <rule>, 0 unknown id)
     3. PROCEDURE  dbo.S_Get<X>List   (all rows + audit columns, for the Redis cache)
     4. MENU ROW   dbo.NavigationSubMenuMaster ('<Name>' / <route-key> under tab '<Tab>')

   After running: <e.g. assign the page in User Authorization; restart the API to clear nothing (Redis keys are cleared on save)>
   ===================================================================== */
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF OBJECT_ID('dbo.<X>', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.<X> ( … CONSTRAINT PK_<X> PRIMARY KEY CLUSTERED (<X>Id) );
END
GO

IF COL_LENGTH('dbo.<X>', '<NewCol>') IS NULL
    ALTER TABLE dbo.<X> ADD <NewCol> nvarchar(20) NULL;
GO

CREATE OR ALTER PROCEDURE dbo.IU_<X> ( … , @Result int = NULL OUTPUT ) AS BEGIN … END
GO
```

## Checklist
- One script per feature; a later change to the same feature = a new dated script (`…_<Feature>_<Change>.sql`),
  never an edit to a script the owner already ran (unless the owner says it was not run yet).
- New optional SP parameters get defaults so existing callers keep working.
- Before `CREATE OR ALTER` of an existing proc: read the current text from the DB and keep its behaviour for
  existing callers (Grep the SP name in `Repositories/`).
- New alert codes / rights / page-config rows are inserted here with `IF NOT EXISTS`.
- No dynamic SQL built by concatenation; no `sp_` prefix; no FK constraints unless asked.
- Run it twice on the local DB; test write procs in `BEGIN TRAN … ROLLBACK TRAN`.
- In the reply to the owner list the scripts to run **in order**, and note any OLD data copy script separately.
