/* =====================================================================
   Salt / Generic Master  —  2026-11-13
   Target DB : GWT_HIS_API (NEW HIS)
   Re-runnable: yes (IF NOT EXISTS / CREATE OR ALTER)

   Objects
     1. TABLE      dbo.SaltNameMaster            (exists - ADD columns TherapeuticClass, SNOMEDCode; design-only fields)
     2. PROCEDURE  dbo.IU_SaltNameMaster         (new in NEW; OLD rule: name unique across all rows -> @Result = -1)
     3. PROCEDURE  dbo.S_GetAllSaltNameMaster    (new; all rows + audit columns + medicine count, for Redis cache)
     4. MENU ROW   dbo.NavigationSubMenuMaster   ('Salt Master' / salt-master under tab 'Purchase')

   NOT changed: dbo.S_GetSaltNameMasterList (active salts only) - used by EMR/getSaltNameMasterList.

   After running: assign the page to users/roles in User Authorization.
   OLD data copy (OLD ids kept) is a separate script: 2026-11-13_SaltNameMaster_OldDataCopy.sql
   ===================================================================== */

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

/* ---------- 1. Table (exists in NEW; create only if missing on the target) ---------- */
IF OBJECT_ID('dbo.SaltNameMaster', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.SaltNameMaster
    (
        HospId          int            NOT NULL,
        SaltNameId      int            IDENTITY(1,1) NOT NULL,
        SaltName        nvarchar(256)  NULL,
        IsActive        int            NOT NULL CONSTRAINT DF_SaltNameMaster_IsActive  DEFAULT (1),
        CreatedBy       int            NULL,
        CreatedOn       datetime       NULL     CONSTRAINT DF_SaltNameMaster_CreatedOn DEFAULT (GETDATE()),
        LastModifiedBy  int            NULL,
        LastModifiedOn  datetime       NULL,
        IpAddress       nvarchar(20)   NULL,
        UniqueId        nvarchar(50)   NOT NULL CONSTRAINT DF_SaltNameMaster_UniqueId  DEFAULT (NEWID()),
        CONSTRAINT PK_SaltNameMaster PRIMARY KEY CLUSTERED (SaltNameId)
    );
END
GO

-- design-only fields (MediStock design: Therapeutic Class, SNOMED-CT Code (Substance))
IF COL_LENGTH('dbo.SaltNameMaster', 'TherapeuticClass') IS NULL
    ALTER TABLE dbo.SaltNameMaster ADD TherapeuticClass nvarchar(100) NULL;
GO
IF COL_LENGTH('dbo.SaltNameMaster', 'SNOMEDCode') IS NULL
    ALTER TABLE dbo.SaltNameMaster ADD SNOMEDCode nvarchar(20) NULL;
GO

/* ---------- 2. Insert / Update ---------- */
CREATE OR ALTER PROCEDURE dbo.IU_SaltNameMaster
(
    @hospId            int,
    @saltNameId        int,
    @saltName          nvarchar(256),
    @therapeuticClass  nvarchar(100) = NULL,
    @snomedCode        nvarchar(20)  = NULL,
    @isActive          int           = 1,
    @userId            int,
    @IpAddress         nvarchar(20),
    @Result            int = NULL OUTPUT
)
AS
BEGIN
    SET @saltName         = LTRIM(RTRIM(@saltName));
    SET @therapeuticClass = NULLIF(LTRIM(RTRIM(@therapeuticClass)), '');
    SET @snomedCode       = NULLIF(LTRIM(RTRIM(@snomedCode)), '');

    -- OLD rule: name unique across all rows (case-insensitive by collation)
    IF EXISTS (SELECT 1 FROM dbo.SaltNameMaster
               WHERE LTRIM(RTRIM(SaltName)) = @saltName
                 AND SaltNameId <> @saltNameId)
    BEGIN
        SET @Result = -1;
        RETURN;
    END

    IF (@saltNameId = 0)
    BEGIN
        INSERT INTO dbo.SaltNameMaster
            (HospId, SaltName, TherapeuticClass, SNOMEDCode, IsActive, CreatedBy, IpAddress)
        VALUES
            (@hospId, @saltName, @therapeuticClass, @snomedCode, @isActive, @userId, @IpAddress);

        SET @Result = SCOPE_IDENTITY();
    END
    ELSE
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM dbo.SaltNameMaster WHERE SaltNameId = @saltNameId)
        BEGIN
            SET @Result = 0;   -- record not found
            RETURN;
        END

        UPDATE dbo.SaltNameMaster
        SET    HospId           = @hospId,
               SaltName         = @saltName,
               TherapeuticClass = @therapeuticClass,
               SNOMEDCode       = @snomedCode,
               IsActive         = @isActive,
               LastModifiedBy   = @userId,
               LastModifiedOn   = GETDATE(),
               IpAddress        = @IpAddress
        WHERE  SaltNameId       = @saltNameId;

        SET @Result = @saltNameId;
    END
END
GO

/* ---------- 3. List (all rows; API caches and filters) ---------- */
CREATE OR ALTER PROCEDURE dbo.S_GetAllSaltNameMaster
AS
BEGIN
    SELECT  snm.SaltNameId,
            snm.SaltName,
            ISNULL(snm.TherapeuticClass, '') AS TherapeuticClass,
            ISNULL(snm.SNOMEDCode, '')       AS SNOMEDCode,
            snm.IsActive,
            (SELECT COUNT(1) FROM dbo.ServiceItemMaster sim
              WHERE sim.DescriptionId = snm.SaltNameId) AS MedicineCount,
            ISNULL((SELECT TOP 1 name FROM dbo.UserMaster umm WHERE umm.id = snm.CreatedBy), '')      AS CreatedBy,
            ISNULL(FORMAT(snm.CreatedOn, 'dd-MM-yyyy'), '')                                            AS CreatedOn,
            ISNULL((SELECT TOP 1 name FROM dbo.UserMaster umm WHERE umm.id = snm.LastModifiedBy), '') AS LastModifiedBy,
            ISNULL(FORMAT(snm.LastModifiedOn, 'dd-MM-yyyy'), '')                                       AS LastModifiedOn
    FROM    dbo.SaltNameMaster snm
    ORDER BY snm.SaltName;
END
GO

/* ---------- 4. Menu row (tab 'Purchase') ---------- */
IF NOT EXISTS (SELECT 1 FROM dbo.NavigationSubMenuMaster WHERE URL = 'salt-master')
BEGIN
    DECLARE @tabId int = (SELECT TOP 1 TabId FROM dbo.NavigationTabMaster
                          WHERE LTRIM(RTRIM(TabName)) = 'Purchase' AND IsActive = 1);
    IF @tabId IS NULL
        PRINT 'Tab "Purchase" not found - add "Salt Master" (salt-master) from the Navigation Pane screen.';
    ELSE
        INSERT INTO dbo.NavigationSubMenuMaster (TabId, SubMenuName, HospId, IsActive, URL, CreatedOn, CreatedBy, IpAddress, SequenceNo)
        VALUES (@tabId, 'Salt Master', 1, 1, 'salt-master', GETDATE(), 1, '127.0.0.1', 81);
END
GO
