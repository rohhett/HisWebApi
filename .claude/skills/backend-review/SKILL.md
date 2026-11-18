---
name: backend-review
description: Checklist to verify a HISWEBAPI change follows the project's design rules before calling it done (envelope, alert codes, cache invalidation, hospId scoping, logging, SQL script, DI). Use after building or changing any endpoint.
---

# Backend review checklist

Run `dotnet build` first. Then check every changed endpoint against this list and fix what fails.

**Placement**
- [ ] Endpoint is in the existing module controller; logic in the module repository (or its partial feature file).
- [ ] Interface method added; new repository/service registered in `Configuration/DependencyInjection.cs`.
- [ ] Request/response classes in `DTO/<Module>DTO.cs`, list models in `Models/<Module>Model.cs`.

**Controller**
- [ ] `[HttpX("lowerCamelRoute")]` + `[Authorize]`; verb matches (GET read, POST save, PATCH status).
- [ ] `_log.Info` on entry; ModelState → `MODEL_VALIDATION_FAILED` 400; value checks → `INVALID_PARAMETER`.
- [ ] `GlobalFunctions.GetGlobalValues(HttpContext)` for user/hospital — nothing identity-related read from the body.
- [ ] Returns `StatusCode(sr.StatusCode, new { result, messageType, message, data })`.

**Repository**
- [ ] Whole body in try/catch → `LogErrors.WriteErrorLog` → `SERVER_ERROR_FOUND` 500 (or `ServerError<T>`).
- [ ] Only stored procedures via `_sqlHelper`; `hospId`, `userId`, `IpAddress` passed to writes.
- [ ] `@Result` mapped: −1 → 409 `RECORD_ALREADY_EXISTS`, 0 on update → 404, >0 → 201 insert / 200 update.
- [ ] Every write removes the related Redis key(s).
- [ ] Multi-table writes in one `getSqlTransaction` with `Rollback` on every failure path; SPs `SELECT @Result`.
- [ ] `row.Field<T>` types match the SP columns (int / bit / decimal / string).

**Contract with the frontend**
- [ ] Existing routes and response shapes unchanged.
- [ ] DTO lengths = table column lengths = yup `max()` in the screen schema.
- [ ] New endpoint added to frontend `src/config/defaults/index.ts` `ENDPOINTS`.

**Database**
- [ ] Re-runnable `Database/YYYY-MM-DD_<Feature>.sql` with header; ran twice locally.
- [ ] Changed existing procs: callers listed, old parameters still work.

**Record**
- [ ] Dated entry in `Claude\HIS_Knowledge_Base\Development_Decisions.md`; "Where work stopped" in the KB README.
- [ ] Reply states what was Verified (built, called, tested), what is Recommended, what is an Assumption.
