# Unused tables and procedures in GWT_HIS_API (local DB, checked 2026-11-26)

Method: a procedure or function is "used" when its name appears in the API source (HISWEBAPI, comments removed)
or in a trigger, or is called by another used one. A table is "used" when a used procedure/function/trigger
mentions it. Nothing was changed or dropped.

Not covered: SQL Agent jobs, reports or other applications reading the database directly, the live server,
and endpoints that exist but that no screen calls.

## Procedures not called (9)
| Procedure | Last changed | Note |
|---|---|---|
| IU_UserRefreshToken | 2025-11-04 | refresh-token pair, API does not call it |
| S_ValidateRefreshToken | 2025-11-04 | refresh-token pair, API does not call it |
| PRE_GetBloodGroupList | 2026-10-08 | PRE_ prefix, no caller here |
| PRE_GetIPDBillNoListByVisitId | 2026-10-01 | PRE_ prefix, no caller here |
| PRE_GetPatientIPDPackagesByVisitId | 2026-10-03 | PRE_ prefix, no caller here |
| PRE_GetReceiptListByFTID | 2026-10-01 | PRE_ prefix, no caller here |
| PRE_GetReceiptListByVisitId | 2026-10-01 | PRE_ prefix, no caller here |
| U_FinancialTransactionDetails | 2026-03-26 | no caller |
| U_FinancialTransactions | 2026-03-26 | no caller |

## Functions not used (2)
- fnIntegerToWords
- GetCorporateWiseIPDDiscount

## Tables
| Table | Rows | Note |
|---|---|---|
| SMSLogs | 0 | nothing reads or writes it |
| SMStypeMaster | 37 | nothing reads or writes it |
| BloodGroupMaster | 10 | only the uncalled PRE_GetBloodGroupList reads it |
| RoleMaster_Deleted_20261126 | 7 | backup of today's role delete; keep until confirmed |
| UserRoleMapping_Deleted_20261126 | 97 | backup of today's role delete; keep until confirmed |
