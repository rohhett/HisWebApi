/* =====================================================================
   Consent Form - edit a saved form  -  2026-11-24
   Target DB : GWT_HIS_API (NEW HIS)
   Re-runnable: yes (CREATE OR ALTER)
   Run after : 2026-11-24_ConsentForm.sql

   Objects
     1. PROCEDURE  dbo.U_PatientConsentFormFile       (@Result: >0 id, 0 form of this visit not found)
                   replaces the file path of a saved form (the user opened the saved PDF,
                   drew / typed more on it and saved); the old file stays in the DMS folder
     2. PROCEDURE  dbo.S_GetPatientConsentFormList    (+ LastModifiedBy, LastModifiedOn)
   ===================================================================== */
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

CREATE OR ALTER PROCEDURE dbo.U_PatientConsentFormFile
(
    @hospId               int,
    @visitId              int,
    @patientConsentFormId int,
    @filePath             nvarchar(500),
    @userId               int,
    @IpAddress            nvarchar(20),
    @Result               int = NULL OUTPUT
)
AS
BEGIN
    SET NOCOUNT ON;
    SET @filePath = LTRIM(RTRIM(@filePath));

    IF NOT EXISTS (SELECT 1 FROM dbo.PatientConsentForm
                   WHERE PatientConsentFormId = @patientConsentFormId
                     AND VisitId = @visitId AND HospId = @hospId AND IsActive = 1)
    BEGIN
        SET @Result = 0;   -- not found for this visit
        RETURN;
    END

    UPDATE dbo.PatientConsentForm
    SET    FilePath       = @filePath,
           LastModifiedBy = @userId,
           LastModifiedOn = GETDATE(),
           IpAddress      = @IpAddress
    WHERE  PatientConsentFormId = @patientConsentFormId;

    SET @Result = @patientConsentFormId;
END
GO

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
            ISNULL(FORMAT(cf.CreatedOn, 'dd-MM-yyyy hh:mm tt'), '')                              AS CreatedOn,
            ISNULL((SELECT TOP 1 name FROM dbo.UserMaster umm WHERE umm.id = cf.LastModifiedBy), '') AS LastModifiedBy,
            ISNULL(FORMAT(cf.LastModifiedOn, 'dd-MM-yyyy hh:mm tt'), '')                             AS LastModifiedOn
    FROM    dbo.PatientConsentForm cf
    WHERE   cf.VisitId = @visitId AND cf.IsActive = 1
    ORDER BY cf.CreatedOn DESC, cf.PatientConsentFormId DESC;
END
GO
