/* =====================================================================
   GST Slab Master (table TaxPercentage)  —  2026-11-13
   Target DB : GWT_HIS_API (NEW HIS)
   Re-runnable: yes (IF NOT EXISTS / CREATE OR ALTER)

   Objects
     1. TABLE      dbo.TaxPercentage            (new; OLD column set + audit columns LastModifiedBy, LastModifiedOn, IpAddress)
     2. DATA       4 OLD slabs 0 / 5 / 12 / 18 % with OLD TaxIDs (inserted only if missing)
     3. PROCEDURE  dbo.IU_TaxPercentage         (new)
                     @Result  >0 = TaxID saved
                              -1 = rate already exists
                              -2 = slab name already exists
                               0 = TaxID not found
                     Note: ServiceItemMaster.GSTPer stores the GST value, not the TaxID - changing a slab's rate
                     does not change items already saved with the old rate (owner decision 2026-11-13: no lock).
     4. PROCEDURE  dbo.S_GetTaxPercentageList   (new; all rows + audit columns + medicine count, for Redis cache)
     5. MENU ROW   dbo.NavigationSubMenuMaster  ('GST Slab Master' / gst-slab-master under tab 'Purchase')

   After running: assign the page to users/roles in User Authorization.
   ===================================================================== */

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

/* ---------- 1. Table ---------- */
IF OBJECT_ID('dbo.TaxPercentage', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.TaxPercentage
    (
        TaxID        int            IDENTITY(1,1) NOT NULL,
        TaxValue     decimal(18,2)  NOT NULL,
        Description  nvarchar(50)   NULL,
        IsActive     bit            NOT NULL CONSTRAINT DF_TaxPercentage_IsActive  DEFAULT (1),
        CreatedOn    datetime       NOT NULL CONSTRAINT DF_TaxPercentage_CreatedOn DEFAULT (GETDATE()),
        CreatedBy    int            NOT NULL,
        CONSTRAINT PK_TaxPercentage PRIMARY KEY CLUSTERED (TaxID)
    );
END
GO

-- audit columns used by every NEW master (OLD table had none)
IF COL_LENGTH('dbo.TaxPercentage', 'LastModifiedBy') IS NULL
    ALTER TABLE dbo.TaxPercentage ADD LastModifiedBy int NULL;
GO
IF COL_LENGTH('dbo.TaxPercentage', 'LastModifiedOn') IS NULL
    ALTER TABLE dbo.TaxPercentage ADD LastModifiedOn datetime NULL;
GO
IF COL_LENGTH('dbo.TaxPercentage', 'IpAddress') IS NULL
    ALTER TABLE dbo.TaxPercentage ADD IpAddress nvarchar(20) NULL;
GO

/* ---------- 2. OLD data (DEMO_HIS_NEW.dbo.TaxPercentage, 4 rows, OLD ids kept) ---------- */
SET IDENTITY_INSERT dbo.TaxPercentage ON;
INSERT INTO dbo.TaxPercentage (TaxID, TaxValue, Description, IsActive, CreatedOn, CreatedBy)
SELECT s.TaxID, s.TaxValue, s.Description, s.IsActive, s.CreatedOn, s.CreatedBy
FROM (VALUES (1,  0.00, N'0% Tax',  1, CAST('2026-04-08 13:18:33.380' AS datetime), 1),
             (2,  5.00, N'5% Tax',  1, CAST('2026-04-08 13:18:33.380' AS datetime), 1),
             (3, 12.00, N'12% Tax', 1, CAST('2026-04-08 13:18:33.380' AS datetime), 1),
             (4, 18.00, N'18% Tax', 1, CAST('2026-04-08 13:18:33.380' AS datetime), 1)
     ) s (TaxID, TaxValue, Description, IsActive, CreatedOn, CreatedBy)
WHERE NOT EXISTS (SELECT 1 FROM dbo.TaxPercentage t WHERE t.TaxID = s.TaxID OR t.TaxValue = s.TaxValue);
SET IDENTITY_INSERT dbo.TaxPercentage OFF;

DECLARE @maxId int = (SELECT ISNULL(MAX(TaxID), 0) FROM dbo.TaxPercentage);
DBCC CHECKIDENT ('dbo.TaxPercentage', RESEED, @maxId) WITH NO_INFOMSGS;
GO

/* ---------- 3. Insert / Update ---------- */
CREATE OR ALTER PROCEDURE dbo.IU_TaxPercentage
(
    @taxId        int,
    @description  nvarchar(50),
    @taxValue     decimal(18,2),
    @isActive     int = 1,
    @userId       int,
    @IpAddress    nvarchar(20),
    @Result       int = NULL OUTPUT
)
AS
BEGIN
    SET @description = LTRIM(RTRIM(@description));

    IF EXISTS (SELECT 1 FROM dbo.TaxPercentage WHERE TaxValue = @taxValue AND TaxID <> @taxId)
    BEGIN
        SET @Result = -1;   -- rate already exists
        RETURN;
    END

    IF EXISTS (SELECT 1 FROM dbo.TaxPercentage WHERE LTRIM(RTRIM(Description)) = @description AND TaxID <> @taxId)
    BEGIN
        SET @Result = -2;   -- slab name already exists
        RETURN;
    END

    IF (@taxId = 0)
    BEGIN
        INSERT INTO dbo.TaxPercentage (TaxValue, Description, IsActive, CreatedBy, IpAddress)
        VALUES (@taxValue, @description, @isActive, @userId, @IpAddress);

        SET @Result = SCOPE_IDENTITY();
        RETURN;
    END

    IF NOT EXISTS (SELECT 1 FROM dbo.TaxPercentage WHERE TaxID = @taxId)
    BEGIN
        SET @Result = 0;    -- record not found
        RETURN;
    END

    UPDATE dbo.TaxPercentage
    SET    TaxValue       = @taxValue,
           Description    = @description,
           IsActive       = @isActive,
           LastModifiedBy = @userId,
           LastModifiedOn = GETDATE(),
           IpAddress      = @IpAddress
    WHERE  TaxID          = @taxId;

    SET @Result = @taxId;
END
GO

/* ---------- 4. List (all rows; API caches and filters) ---------- */
CREATE OR ALTER PROCEDURE dbo.S_GetTaxPercentageList
AS
BEGIN
    SELECT  tp.TaxID,
            ISNULL(tp.Description, '')        AS Description,
            tp.TaxValue,
            CAST(tp.TaxValue / 2 AS decimal(18,2)) AS CGST,
            CAST(tp.TaxValue / 2 AS decimal(18,2)) AS SGST,
            CAST(tp.IsActive AS int)          AS IsActive,
            (SELECT COUNT(1) FROM dbo.ServiceItemMaster sim
              WHERE sim.GSTPer = tp.TaxValue) AS MedicineCount,
            ISNULL((SELECT TOP 1 name FROM dbo.UserMaster umm WHERE umm.id = tp.CreatedBy), '')      AS CreatedBy,
            ISNULL(FORMAT(tp.CreatedOn, 'dd-MM-yyyy'), '')                                            AS CreatedOn,
            ISNULL((SELECT TOP 1 name FROM dbo.UserMaster umm WHERE umm.id = tp.LastModifiedBy), '') AS LastModifiedBy,
            ISNULL(FORMAT(tp.LastModifiedOn, 'dd-MM-yyyy'), '')                                       AS LastModifiedOn
    FROM    dbo.TaxPercentage tp
    ORDER BY tp.TaxValue;
END
GO

/* ---------- 5. Menu row (tab 'Purchase') ---------- */
IF NOT EXISTS (SELECT 1 FROM dbo.NavigationSubMenuMaster WHERE URL = 'gst-slab-master')
BEGIN
    DECLARE @tabId int = (SELECT TOP 1 TabId FROM dbo.NavigationTabMaster
                          WHERE LTRIM(RTRIM(TabName)) = 'Purchase' AND IsActive = 1);
    IF @tabId IS NULL
        PRINT 'Tab "Purchase" not found - add "GST Slab Master" (gst-slab-master) from the Navigation Pane screen.';
    ELSE
        INSERT INTO dbo.NavigationSubMenuMaster (TabId, SubMenuName, HospId, IsActive, URL, CreatedOn, CreatedBy, IpAddress, SequenceNo)
        VALUES (@tabId, 'GST Slab Master', 1, 1, 'gst-slab-master', GETDATE(), 1, '127.0.0.1', 82);
END
GO
