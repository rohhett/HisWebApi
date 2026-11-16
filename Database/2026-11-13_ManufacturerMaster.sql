/* =====================================================================
   Manufacturer Master  —  2026-11-13
   Target DB : GWT_HIS_API (NEW HIS)
   Re-runnable: yes (IF NOT EXISTS / CREATE OR ALTER)

   Objects
     1. TABLE      dbo.ManufacturerMaster          (new; OLD column set + ShortCode, CityCountry)
     2. PROCEDURE  dbo.IU_ManufacturerMaster       (new; OLD rule: name unique across all rows -> @Result = -1)
     3. PROCEDURE  dbo.S_GetManufacturerList       (new; all rows + audit columns + medicine count, for Redis cache)
     4. MENU ROW   dbo.NavigationSubMenuMaster     ('Manufacturer Master' / manufacturer-master under tab 'Purchase')

   After running: assign the page to users/roles in User Authorization.
   OLD data copy (1,011 manufacturers, OLD ids kept) is a separate script:
     2026-11-13_ManufacturerMaster_OldDataCopy.sql
   ===================================================================== */

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

/* ---------- 1. Table ---------- */
IF OBJECT_ID('dbo.ManufacturerMaster', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.ManufacturerMaster
    (
        HospId            int            NOT NULL,
        ManufacturerId    int            IDENTITY(1,1) NOT NULL,
        ManufacturerName  nvarchar(256)  NULL,
        IsActive          int            NOT NULL CONSTRAINT DF_ManufacturerMaster_IsActive  DEFAULT (1),
        CreatedBy         int            NULL,
        CreatedOn         datetime       NULL     CONSTRAINT DF_ManufacturerMaster_CreatedOn DEFAULT (GETDATE()),
        LastModifiedBy    int            NULL,
        LastModifiedOn    datetime       NULL,
        IpAddress         nvarchar(20)   NULL,
        UniqueId          nvarchar(50)   NOT NULL CONSTRAINT DF_ManufacturerMaster_UniqueId  DEFAULT (NEWID()),
        CONSTRAINT PK_ManufacturerMaster PRIMARY KEY CLUSTERED (ManufacturerId)
    );
END
GO

-- design-only fields (MediStock design: Short Code, City / Country)
IF COL_LENGTH('dbo.ManufacturerMaster', 'ShortCode') IS NULL
    ALTER TABLE dbo.ManufacturerMaster ADD ShortCode nvarchar(20) NULL;
GO
IF COL_LENGTH('dbo.ManufacturerMaster', 'CityCountry') IS NULL
    ALTER TABLE dbo.ManufacturerMaster ADD CityCountry nvarchar(100) NULL;
GO

/* ---------- 2. Insert / Update ---------- */
CREATE OR ALTER PROCEDURE dbo.IU_ManufacturerMaster
(
    @hospId            int,
    @manufacturerId    int,
    @manufacturerName  nvarchar(256),
    @shortCode         nvarchar(20)  = NULL,
    @cityCountry       nvarchar(100) = NULL,
    @isActive          int           = 1,
    @userId            int,
    @IpAddress         nvarchar(20),
    @Result            int = NULL OUTPUT
)
AS
BEGIN
    SET @manufacturerName = LTRIM(RTRIM(@manufacturerName));
    SET @shortCode        = NULLIF(LTRIM(RTRIM(@shortCode)), '');
    SET @cityCountry      = NULLIF(LTRIM(RTRIM(@cityCountry)), '');

    -- OLD rule: name unique across all rows (case-insensitive by collation)
    IF EXISTS (SELECT 1 FROM dbo.ManufacturerMaster
               WHERE LTRIM(RTRIM(ManufacturerName)) = @manufacturerName
                 AND ManufacturerId <> @manufacturerId)
    BEGIN
        SET @Result = -1;
        RETURN;
    END

    IF (@manufacturerId = 0)
    BEGIN
        INSERT INTO dbo.ManufacturerMaster
            (HospId, ManufacturerName, ShortCode, CityCountry, IsActive, CreatedBy, IpAddress)
        VALUES
            (@hospId, @manufacturerName, @shortCode, @cityCountry, @isActive, @userId, @IpAddress);

        SET @Result = SCOPE_IDENTITY();
    END
    ELSE
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM dbo.ManufacturerMaster WHERE ManufacturerId = @manufacturerId)
        BEGIN
            SET @Result = 0;   -- record not found
            RETURN;
        END

        UPDATE dbo.ManufacturerMaster
        SET    HospId           = @hospId,
               ManufacturerName = @manufacturerName,
               ShortCode        = @shortCode,
               CityCountry      = @cityCountry,
               IsActive         = @isActive,
               LastModifiedBy   = @userId,
               LastModifiedOn   = GETDATE(),
               IpAddress        = @IpAddress
        WHERE  ManufacturerId   = @manufacturerId;

        SET @Result = @manufacturerId;
    END
END
GO

/* ---------- 3. List (all rows; API caches and filters) ---------- */
CREATE OR ALTER PROCEDURE dbo.S_GetManufacturerList
AS
BEGIN
    SELECT  mm.ManufacturerId,
            mm.ManufacturerName,
            ISNULL(mm.ShortCode, '')   AS ShortCode,
            ISNULL(mm.CityCountry, '') AS CityCountry,
            mm.IsActive,
            (SELECT COUNT(1) FROM dbo.ServiceItemMaster sim
              WHERE sim.ManufacturerId = mm.ManufacturerId) AS MedicineCount,
            ISNULL((SELECT TOP 1 name FROM dbo.UserMaster umm WHERE umm.id = mm.CreatedBy), '')      AS CreatedBy,
            ISNULL(FORMAT(mm.CreatedOn, 'dd-MM-yyyy'), '')                                            AS CreatedOn,
            ISNULL((SELECT TOP 1 name FROM dbo.UserMaster umm WHERE umm.id = mm.LastModifiedBy), '') AS LastModifiedBy,
            ISNULL(FORMAT(mm.LastModifiedOn, 'dd-MM-yyyy'), '')                                       AS LastModifiedOn
    FROM    dbo.ManufacturerMaster mm
    ORDER BY mm.ManufacturerName;
END
GO

/* ---------- 4. Menu row (tab 'Purchase') ---------- */
IF NOT EXISTS (SELECT 1 FROM dbo.NavigationSubMenuMaster WHERE URL = 'manufacturer-master')
BEGIN
    DECLARE @tabId int = (SELECT TOP 1 TabId FROM dbo.NavigationTabMaster
                          WHERE LTRIM(RTRIM(TabName)) = 'Purchase' AND IsActive = 1);
    IF @tabId IS NULL
        PRINT 'Tab "Purchase" not found - add "Manufacturer Master" (manufacturer-master) from the Navigation Pane screen.';
    ELSE
        INSERT INTO dbo.NavigationSubMenuMaster (TabId, SubMenuName, HospId, IsActive, URL, CreatedOn, CreatedBy, IpAddress, SequenceNo)
        VALUES (@tabId, 'Manufacturer Master', 1, 1, 'manufacturer-master', GETDATE(), 1, '127.0.0.1', 80);
END
GO
