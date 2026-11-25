/* =====================================================================
   IPD tab "Consent Form"  -  2026-11-24
   Target DB : GWT_HIS_API (NEW HIS)
   Re-runnable: yes (IF NOT EXISTS / CREATE OR ALTER)
   Run after : 2026-11-24_PatientFormMaster.sql (forms come from PatientFormMaster)

   Objects
     1. TABLE      dbo.PatientConsentForm        (a filled form saved against a visit)
     2. PROCEDURE  dbo.I_PatientConsentForm      (@Result: >0 id, -1 unknown / inactive form, -2 visit not found)
     3. PROCEDURE  dbo.S_GetPatientConsentFormList (@visitId: forms already saved for the visit, newest first)
     4. TAB ROW    dbo.IPDTabMaster              ('Consent Form' / consent-form, IPD Tabs)

   FilePath = path returned by POST Home/uploadDocument (stored as is).
   UploadMode : 'Scanned' = printed, filled by hand, scanned and uploaded
                'Digital' = filled on screen (pen / keyboard) and saved as a PDF
   After running: map the tab to roles / users in Tab Mapping, then restart the API.
   ===================================================================== */
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

/* ---------- 0. Rename from the first "Concern Form" build (local databases only) ----------
   The feature was first built as "Concern Form" and renamed to "Consent Form" the same day.
   A database that ran the old script gets its table, procedures and tab row renamed here;
   a new database skips this section. */
IF OBJECT_ID('dbo.PatientConcernForm', 'U') IS NOT NULL AND OBJECT_ID('dbo.PatientConsentForm', 'U') IS NULL
BEGIN
    EXEC sp_rename 'dbo.PatientConcernForm', 'PatientConsentForm';
    EXEC sp_rename 'dbo.PatientConsentForm.PatientConcernFormId', 'PatientConsentFormId', 'COLUMN';
    EXEC sp_rename 'dbo.PK_PatientConcernForm', 'PK_PatientConsentForm', 'OBJECT';
    EXEC sp_rename 'dbo.DF_PatientConcernForm_UploadMode', 'DF_PatientConsentForm_UploadMode', 'OBJECT';
    EXEC sp_rename 'dbo.DF_PatientConcernForm_IsActive', 'DF_PatientConsentForm_IsActive', 'OBJECT';
    EXEC sp_rename 'dbo.DF_PatientConcernForm_CreatedOn', 'DF_PatientConsentForm_CreatedOn', 'OBJECT';
    EXEC sp_rename N'dbo.PatientConsentForm.IX_PatientConcernForm_VisitId', N'IX_PatientConsentForm_VisitId', N'INDEX';
END
GO
IF OBJECT_ID('dbo.I_PatientConcernForm', 'P') IS NOT NULL DROP PROCEDURE dbo.I_PatientConcernForm;
IF OBJECT_ID('dbo.S_GetPatientConcernFormList', 'P') IS NOT NULL DROP PROCEDURE dbo.S_GetPatientConcernFormList;
IF OBJECT_ID('dbo.U_PatientConcernFormFile', 'P') IS NOT NULL DROP PROCEDURE dbo.U_PatientConcernFormFile;
GO
UPDATE dbo.IPDTabMaster
SET    TabName = 'Consent Form', TabViewURL = 'consent-form'
WHERE  TabViewURL = 'concern-form';
GO

/* ---------- 1. Table ---------- */
IF OBJECT_ID('dbo.PatientConsentForm', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.PatientConsentForm
    (
        HospId                int            NOT NULL,
        BranchId              int            NOT NULL,
        PatientConsentFormId  int            IDENTITY(1,1) NOT NULL,
        VisitId               int            NOT NULL,
        PatientId             int            NOT NULL,
        PatientFormId         int            NOT NULL,
        FormName              nvarchar(200)  NOT NULL,   -- name at the time of saving
        FilePath              nvarchar(500)  NOT NULL,
        UploadMode            nvarchar(20)   NOT NULL CONSTRAINT DF_PatientConsentForm_UploadMode DEFAULT ('Scanned'),
        IsActive              int            NOT NULL CONSTRAINT DF_PatientConsentForm_IsActive   DEFAULT (1),
        CreatedBy             int            NULL,
        CreatedOn             datetime       NULL     CONSTRAINT DF_PatientConsentForm_CreatedOn  DEFAULT (GETDATE()),
        LastModifiedBy        int            NULL,
        LastModifiedOn        datetime       NULL,
        IpAddress             nvarchar(20)   NULL,
        CONSTRAINT PK_PatientConsentForm PRIMARY KEY CLUSTERED (PatientConsentFormId)
    );
    CREATE NONCLUSTERED INDEX IX_PatientConsentForm_VisitId ON dbo.PatientConsentForm (VisitId);
END
GO

/* ---------- 2. Insert ---------- */
CREATE OR ALTER PROCEDURE dbo.I_PatientConsentForm
(
    @hospId         int,
    @branchId       int,
    @visitId        int,
    @patientFormId  int,
    @filePath       nvarchar(500),
    @uploadMode     nvarchar(20) = 'Scanned',
    @userId         int,
    @IpAddress      nvarchar(20),
    @Result         int = NULL OUTPUT
)
AS
BEGIN
    SET NOCOUNT ON;
    SET @filePath   = LTRIM(RTRIM(@filePath));
    SET @uploadMode = CASE WHEN @uploadMode = 'Digital' THEN 'Digital' ELSE 'Scanned' END;

    DECLARE @patientId int = (SELECT TOP 1 PatientId FROM dbo.PatientVisitDetails WHERE VisitId = @visitId);
    IF @patientId IS NULL
    BEGIN
        SET @Result = -2;   -- visit not found
        RETURN;
    END

    DECLARE @formName nvarchar(200) =
        (SELECT TOP 1 FormName FROM dbo.PatientFormMaster WHERE PatientFormId = @patientFormId AND HospId = @hospId AND IsActive = 1);
    IF @formName IS NULL
    BEGIN
        SET @Result = -1;   -- form not found / inactive
        RETURN;
    END

    INSERT INTO dbo.PatientConsentForm
        (HospId, BranchId, VisitId, PatientId, PatientFormId, FormName, FilePath, UploadMode, IsActive, CreatedBy, IpAddress)
    VALUES
        (@hospId, @branchId, @visitId, @patientId, @patientFormId, @formName, @filePath, @uploadMode, 1, @userId, @IpAddress);

    SET @Result = SCOPE_IDENTITY();
END
GO

/* ---------- 3. List for a visit ---------- */
CREATE OR ALTER PROCEDURE dbo.S_GetPatientConsentFormList
(
    @visitId int
)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT  cf.PatientConsentFormId,
            cf.VisitId,
            cf.PatientFormId,
            cf.FormName,
            cf.FilePath,
            cf.UploadMode,
            ISNULL((SELECT TOP 1 name FROM dbo.UserMaster umm WHERE umm.id = cf.CreatedBy), '') AS CreatedBy,
            ISNULL(FORMAT(cf.CreatedOn, 'dd-MM-yyyy hh:mm tt'), '')                              AS CreatedOn
    FROM    dbo.PatientConsentForm cf
    WHERE   cf.VisitId = @visitId AND cf.IsActive = 1
    ORDER BY cf.CreatedOn DESC, cf.PatientConsentFormId DESC;
END
GO

/* ---------- 4. IPD tab row ---------- */
IF NOT EXISTS (SELECT 1 FROM dbo.IPDTabMaster WHERE TabViewURL = 'consent-form')
BEGIN
    INSERT INTO dbo.IPDTabMaster
        (GroupTypeId, TabName, TabViewURL, SequenceNo, TabTypeId, TabType, RoomTypeId, IsActive, CreatedBy, CreatedOn, IpAddress)
    VALUES
        (1, 'Consent Form', 'consent-form', 1, 1, 'IPD Tabs', 120836, 1, 1, GETDATE(), '127.0.0.1');
END
GO
