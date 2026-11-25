/* =====================================================================================================
   2026-11-24  Discharge Summary take-home medicines for the pharmacy (Medicine Sale / Issue)
   Target DB : GWT_HIS_API          Re-runnable: yes
   Owner     : "Yes, build it" (plan of 2026-11-24: table from OLD PatientMedicationAdvice, store query,
               discharge type in the request endpoints, demo data)

   OLD: the discharge summary saved take-home medicines in PatientMedicationAdvice (VisitId, DSId, ItemId, ItemName,
        Route, Dose, Times, Duration, Meal, Remarks, FrequencyName) and read them with S_GetPatientDischargeMedication(@DSId).
   NEW: neither exists. PatientDischargeSummary (header only) does.

     1. TABLE      dbo.PatientDischargeMedication       OLD PatientMedicationAdvice columns + Qty (take-home units) + IsActive
     2. PROCEDURE  dbo.S_GetDischargeMedicineRequests   IPD visits of the branch with take-home medicines (one request per visit)
     3. PROCEDURE  dbo.S_GetDischargeMedicineItems      medicines of one visit with stock in the selected store
     4. PROCEDURE  dbo.S_GetPharmacyRequestCounts       DischargeCount now counted (was 0); same text also in 2026-11-23_PatientMedicineIssue.sql

   A discharge request is dispensed when a bill with StoreBills.SourceTypeId = 3 and SourceId = VisitId exists (not cancelled).
   Nothing in the NEW HIS writes this table yet: the Discharge Summary screen must save into it when it is built.
   Run after 2026-11-23_PatientMedicineIssue.sql. Demo rows: 2026-11-24_PatientMedicineIssue_DemoData.sql.
   ===================================================================================================== */
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

/* ---------- 1. Table ---------- */
IF OBJECT_ID('dbo.PatientDischargeMedication', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.PatientDischargeMedication
    (
        DischargeMedicationId int IDENTITY(1,1) NOT NULL CONSTRAINT PK_PatientDischargeMedication PRIMARY KEY CLUSTERED,
        HospId          int             NOT NULL,
        VisitId         int             NOT NULL,
        DSId            int             NULL,              -- PatientDischargeSummary.DSId when the summary exists
        PatientId       int             NOT NULL,
        ItemId          int             NOT NULL,          -- ServiceItemMaster.ServiceItemId
        ItemName        nvarchar(1024)  NOT NULL,
        Qty             int             NULL,              -- take-home units (OLD had none; NULL = pharmacist decides, shown as 1)
        Dose            nvarchar(100)   NULL,
        FrequencyName   nvarchar(100)   NULL,
        Times           nvarchar(100)   NULL,
        Duration        nvarchar(100)   NULL,
        Route           nvarchar(100)   NULL,
        Meal            nvarchar(100)   NULL,
        Remarks         nvarchar(256)   NULL,
        IsActive        int             NOT NULL CONSTRAINT DF_PatientDischargeMedication_IsActive DEFAULT (1),
        CreatedBy       int             NULL,
        CreatedOn       datetime        NOT NULL CONSTRAINT DF_PatientDischargeMedication_CreatedOn DEFAULT (GETDATE()),
        LastModifiedBy  int             NULL,
        LastModifiedOn  datetime        NULL,
        IpAddress       nvarchar(40)    NULL
    );
    CREATE INDEX IX_PatientDischargeMedication_Visit ON dbo.PatientDischargeMedication (VisitId, IsActive);
END
GO

/* ---------- 2. Discharge requests (one per IPD visit) ---------- */
CREATE OR ALTER PROCEDURE dbo.S_GetDischargeMedicineRequests
    @hospId int, @branchId int, @search nvarchar(100) = NULL, @statusId int = NULL   -- NULL all, 1 pending, 2 dispensed
AS
BEGIN
    SET NOCOUNT ON;
    SET @search = NULLIF(LTRIM(RTRIM(@search)), '');
    ;WITH v AS
    (
        SELECT m.VisitId, MAX(m.DSId) AS DSId, MAX(m.CreatedOn) AS WrittenOn, COUNT(*) AS ItemCount
        FROM dbo.PatientDischargeMedication m
        WHERE m.HospId = @hospId AND m.IsActive = 1
        GROUP BY m.VisitId
    )
    SELECT v.VisitId, v.DSId, v.WrittenOn, v.ItemCount, pvd.VisitNo AS IpdNo, pvd.PatientId, pm.UHID, pm.PatientName, pm.Age, pm.Gender,
           CONCAT(wnm.WardName, ' / ', bm.BedNo) AS WardBed, pvd.DoctorId, dm.CompleteName AS DoctorName, ISNULL(dm.Department, '') AS Department,
           ISNULL(pvd.IsDischarged, 0) AS IsDischarged, pvd.DischargedOn,
           ISNULL(ds.IsApproved, 0) AS IsApproved, ad.CompleteName AS ApprovedBy,
           CASE WHEN d.StoreBillId IS NULL THEN 1 ELSE 2 END AS StatusId,
           CASE WHEN d.StoreBillId IS NULL THEN 'Pending' ELSE 'Dispensed' END AS Status,
           d.BillNo
    FROM v
    INNER JOIN dbo.PatientVisitDetails pvd ON pvd.VisitId = v.VisitId AND pvd.TypeId = 2 AND ISNULL(pvd.IsCancel, 0) = 0
    INNER JOIN dbo.PatientMaster pm ON pm.PatientId = pvd.PatientId
    LEFT JOIN dbo.BedMaster bm ON bm.BedId = pvd.BedId
    LEFT JOIN dbo.WardNameMaster wnm ON wnm.WardNameId = bm.WardNameId
    LEFT JOIN dbo.DoctorMaster dm ON dm.DoctorId = pvd.DoctorId
    LEFT JOIN dbo.PatientDischargeSummary ds ON ds.DSId = v.DSId
    LEFT JOIN dbo.DoctorMaster ad ON ad.DoctorId = ds.ApprovedByDoctorId
    OUTER APPLY (SELECT TOP 1 sb.StoreBillId, sb.BillNo FROM dbo.StoreBills sb WHERE sb.SourceTypeId = 3 AND sb.SourceId = v.VisitId AND sb.IsCancel = 0) d
    WHERE pvd.HospId = @hospId AND pvd.BranchId = @branchId
      AND (@statusId IS NULL OR (@statusId = 1 AND d.StoreBillId IS NULL) OR (@statusId = 2 AND d.StoreBillId IS NOT NULL))
      AND (@search IS NULL OR pm.UHID LIKE '%' + @search + '%' OR pm.PatientName LIKE '%' + @search + '%'
           OR pvd.VisitNo LIKE '%' + @search + '%' OR wnm.WardName LIKE '%' + @search + '%' OR dm.CompleteName LIKE '%' + @search + '%')
    ORDER BY v.WrittenOn DESC;
END
GO

/* ---------- 3. Medicines of one discharge request ---------- */
CREATE OR ALTER PROCEDURE dbo.S_GetDischargeMedicineItems
    @hospId int, @branchId int, @roleId int, @visitId int
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @today date = CAST(GETDATE() AS date);
    SELECT m.DischargeMedicationId, m.ItemId, ISNULL(sim.Name, m.ItemName) AS ItemName, ISNULL(sim.ItemDiscription, '') AS SaltName,
           ISNULL(df.DosageFormName, '') AS DosageForm, ISNULL(NULLIF(m.Qty, 0), 1) AS Qty,
           ISNULL(m.Dose, '') AS Dose, ISNULL(m.FrequencyName, ISNULL(m.Times, '')) AS Frequency, ISNULL(m.Duration, '') AS Duration,
           ISNULL(m.Route, '') AS Route, ISNULL(m.Meal, '') AS Meal, ISNULL(m.Remarks, '') AS Remarks,
           ISNULL((SELECT SUM(st.CurrentStock) FROM dbo.Stock st WHERE st.ItemId = m.ItemId AND st.BranchId = @branchId AND st.RoleId = @roleId
                   AND st.IsActive = 1 AND st.CurrentStock > 0 AND st.ExpiryDate > @today), 0) AS InStock
    FROM dbo.PatientDischargeMedication m
    INNER JOIN dbo.PatientVisitDetails pvd ON pvd.VisitId = m.VisitId AND pvd.HospId = @hospId AND pvd.BranchId = @branchId
    LEFT JOIN dbo.ServiceItemMaster sim ON sim.ServiceItemId = m.ItemId
    LEFT JOIN dbo.DosageFormMaster df ON df.DosageFormId = sim.DosageFormId
    WHERE m.HospId = @hospId AND m.VisitId = @visitId AND m.IsActive = 1
    ORDER BY m.DischargeMedicationId;
END
GO

/* ---------- 4. Inbox counts (DischargeCount added) ---------- */
CREATE OR ALTER PROCEDURE dbo.S_GetPharmacyRequestCounts
    @hospId int, @branchId int, @roleId int, @emrFromDate date
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        (SELECT COUNT(DISTINCT h.VisitId)
           FROM dbo.PatientDoctorHeadersData h
           INNER JOIN dbo.PatientVisitDetails pvd ON pvd.VisitId = h.VisitId AND pvd.TypeId = 1 AND pvd.BranchId = @branchId
           WHERE ISJSON(h.HeaderValue) = 1 AND JSON_VALUE(h.HeaderValue, '$[0].medicineName') IS NOT NULL
             AND CAST(h.CreatedOn AS date) >= @emrFromDate
             AND NOT EXISTS (SELECT 1 FROM dbo.StoreBills sb WHERE sb.SourceTypeId = 1 AND sb.SourceId = h.VisitId AND sb.IsCancel = 0)) AS EmrCount,
        (SELECT COUNT(*) FROM dbo.PatientMedicineIndent WHERE ToBranchId = @branchId AND ToDeptId = @roleId AND StatusId = 1 AND IsCancel = 0) AS IndentCount,
        (SELECT COUNT(*) FROM dbo.PatientMedicineIndent WHERE ToBranchId = @branchId AND ToDeptId = @roleId AND StatusId = 1 AND IsCancel = 0 AND Priority = 'Urgent') AS IndentUrgentCount,
        (SELECT COUNT(DISTINCT m.VisitId)
           FROM dbo.PatientDischargeMedication m
           INNER JOIN dbo.PatientVisitDetails pvd ON pvd.VisitId = m.VisitId AND pvd.TypeId = 2 AND pvd.BranchId = @branchId AND ISNULL(pvd.IsCancel, 0) = 0
           WHERE m.HospId = @hospId AND m.IsActive = 1
             AND NOT EXISTS (SELECT 1 FROM dbo.StoreBills sb WHERE sb.SourceTypeId = 3 AND sb.SourceId = m.VisitId AND sb.IsCancel = 0)) AS DischargeCount;
END
GO
