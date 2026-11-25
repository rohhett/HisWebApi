/* =====================================================================
   Patient Form Master  -  2026-11-24
   Target DB : GWT_HIS_API (NEW HIS)
   Re-runnable: yes (IF NOT EXISTS / CREATE OR ALTER)

   Objects
     1. TABLE      dbo.PatientFormMaster      (new: form name + path of the uploaded PDF + IsActive)
     2. PROCEDURE  dbo.IU_PatientFormMaster   (@Result: >0 id, -1 duplicate form name, 0 unknown id)
     3. PROCEDURE  dbo.S_GetPatientFormList   (all rows + audit columns, for the Redis cache)
     4. MENU ROW   dbo.NavigationSubMenuMaster ('Patient Form Master' / patient-form-master under tab 'Admin')

   FilePath = the path returned by POST Home/uploadDocument (stored as is).
   After running: assign the page in User Authorization.
   ===================================================================== */
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

/* ---------- 1. Table ---------- */
IF OBJECT_ID('dbo.PatientFormMaster', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.PatientFormMaster
    (
        HospId          int            NOT NULL,
        PatientFormId   int            IDENTITY(1,1) NOT NULL,
        FormName        nvarchar(200)  NOT NULL,
        FilePath        nvarchar(500)  NOT NULL,
        IsActive        int            NOT NULL CONSTRAINT DF_PatientFormMaster_IsActive  DEFAULT (1),
        CreatedBy       int            NULL,
        CreatedOn       datetime       NULL     CONSTRAINT DF_PatientFormMaster_CreatedOn DEFAULT (GETDATE()),
        LastModifiedBy  int            NULL,
        LastModifiedOn  datetime       NULL,
        IpAddress       nvarchar(20)   NULL,
        CONSTRAINT PK_PatientFormMaster PRIMARY KEY CLUSTERED (PatientFormId)
    );
END
GO

/* ---------- 2. Insert / Update ---------- */
CREATE OR ALTER PROCEDURE dbo.IU_PatientFormMaster
(
    @hospId         int,
    @patientFormId  int,
    @formName       nvarchar(200),
    @filePath       nvarchar(500),
    @isActive       int = 1,
    @userId         int,
    @IpAddress      nvarchar(20),
    @Result         int = NULL OUTPUT
)
AS
BEGIN
    SET NOCOUNT ON;
    SET @formName = LTRIM(RTRIM(@formName));
    SET @filePath = LTRIM(RTRIM(@filePath));

    -- form name unique per hospital (case-insensitive by collation)
    IF EXISTS (SELECT 1 FROM dbo.PatientFormMaster
               WHERE HospId = @hospId AND LTRIM(RTRIM(FormName)) = @formName
                 AND PatientFormId <> @patientFormId)
    BEGIN
        SET @Result = -1;
        RETURN;
    END

    IF (@patientFormId = 0)
    BEGIN
        INSERT INTO dbo.PatientFormMaster (HospId, FormName, FilePath, IsActive, CreatedBy, IpAddress)
        VALUES (@hospId, @formName, @filePath, @isActive, @userId, @IpAddress);

        SET @Result = SCOPE_IDENTITY();
    END
    ELSE
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM dbo.PatientFormMaster WHERE PatientFormId = @patientFormId)
        BEGIN
            SET @Result = 0;   -- record not found
            RETURN;
        END

        UPDATE dbo.PatientFormMaster
        SET    FormName       = @formName,
               FilePath       = @filePath,
               IsActive       = @isActive,
               LastModifiedBy = @userId,
               LastModifiedOn = GETDATE(),
               IpAddress      = @IpAddress
        WHERE  PatientFormId  = @patientFormId;

        SET @Result = @patientFormId;
    END
END
GO

/* ---------- 3. List (all rows; API caches and filters) ---------- */
CREATE OR ALTER PROCEDURE dbo.S_GetPatientFormList
AS
BEGIN
    SET NOCOUNT ON;

    SELECT  pf.PatientFormId,
            pf.FormName,
            pf.FilePath,
            pf.IsActive,
            ISNULL((SELECT TOP 1 name FROM dbo.UserMaster umm WHERE umm.id = pf.CreatedBy), '')      AS CreatedBy,
            ISNULL(FORMAT(pf.CreatedOn, 'dd-MM-yyyy'), '')                                            AS CreatedOn,
            ISNULL((SELECT TOP 1 name FROM dbo.UserMaster umm WHERE umm.id = pf.LastModifiedBy), '') AS LastModifiedBy,
            ISNULL(FORMAT(pf.LastModifiedOn, 'dd-MM-yyyy'), '')                                       AS LastModifiedOn
    FROM    dbo.PatientFormMaster pf
    ORDER BY pf.FormName;
END
GO

/* ---------- 4. Menu row (tab 'Admin') ---------- */
IF NOT EXISTS (SELECT 1 FROM dbo.NavigationSubMenuMaster WHERE URL = 'patient-form-master')
BEGIN
    DECLARE @tabId int = (SELECT TOP 1 TabId FROM dbo.NavigationTabMaster
                          WHERE LTRIM(RTRIM(TabName)) = 'Admin' AND IsActive = 1);
    IF @tabId IS NULL
        PRINT 'Tab "Admin" not found - add "Patient Form Master" (patient-form-master) from the Navigation Pane screen.';
    ELSE
        INSERT INTO dbo.NavigationSubMenuMaster (TabId, SubMenuName, HospId, IsActive, URL, CreatedOn, CreatedBy, IpAddress, SequenceNo)
        VALUES (@tabId, 'Patient Form Master', 1, 1, 'patient-form-master', GETDATE(), 1, '127.0.0.1', 46);
END
GO
