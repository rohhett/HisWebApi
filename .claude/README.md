# .claude — HISWEBAPI backend rules and skills

This folder holds the design rules and step-by-step skills for the HISWEBAPI backend
(ASP.NET Core 8 Web API + SQL Server stored procedures). Every new API must follow them,
so that new code looks exactly like the existing code.

| Path | What it is | Loaded |
|---|---|---|
| `rules/architecture.md` | Layers, folders, request flow, response contract | always |
| `rules/api-coding-rules.md` | Controller / repository / DTO / model rules with code shapes | always |
| `rules/caching-logging-security.md` | Redis cache keys, log4net, JWT / hospId, alert codes | always |
| `rules/sql-rules.md` | Table, procedure and change-script rules | when a `Database/*.sql` file is touched |
| `skills/new-master-api/` | Build a lookup master API (list + create/update) end to end | `/new-master-api` |
| `skills/new-transaction-api/` | Build a multi-line save (header + lines, approvals) in one transaction | `/new-transaction-api` |
| `skills/sql-change-script/` | Write the re-runnable `Database/YYYY-MM-DD_<Feature>.sql` script | `/sql-change-script` |
| `skills/backend-review/` | Checklist to run before calling a backend change done | `/backend-review` |

Reference builds (copy these, they are the cleanest examples):
- **Master:** Manufacturer Master — `StoreController.CreateUpdateManufacturerMaster` / `GetManufacturerList`,
  `StoreRepository.cs`, `StoreDTO.cs`, `StoreModel.cs`, `Database/2026-11-13_ManufacturerMaster.sql`.
- **Transaction:** Stock Receiving (GRN) — `Repositories/Implementations/StoreRepository.StockReceiving.cs`,
  `Database/2026-11-14_StockReceiving.sql`. Purchase Request / Purchase Order for approval flows.

Frontend counterpart: `D:\@HISWEBAPI\Frontend\HIS-main\.claude\`.
Migration knowledge base (OLD → NEW rules, decisions): `D:\@HISWEBAPI\Claude\HIS_Knowledge_Base\`.

When the owner corrects a rule, update the matching file here **and** add a dated entry to
`Claude\HIS_Knowledge_Base\Development_Decisions.md`.
