/* =====================================================================
   Drug Category Master (table DrugCategoryMasters)  —  2026-11-13
   Target DB : GWT_HIS_API (NEW HIS)
   Re-runnable: yes (IF NOT EXISTS / CREATE OR ALTER)

   Uses OLD table DrugCategoryMasters (DrugId, DrugName, isNarcoticDrug, ColorPicker) - the table store items point to
   (owner decision 2026-11-13). OLD DrugCategoryMaster (therapeutic classes) is NOT created.

   Objects
     1. TABLE      dbo.DrugCategoryMasters          (new; OLD column set)
     2. DATA       12 OLD rows with OLD DrugIds (inserted only if missing)
     3. PROCEDURE  dbo.IU_DrugCategoryMasters       (new in NEW; OLD parameters)
                     @Result >0 = DrugId saved, -1 = name already exists, 0 = DrugId not found
                     Change vs OLD: the duplicate-name check now runs on INSERT too
                     (OLD checked only on update - that is how "Narcotics" 1/1009 and "Hight" 1010/1011 were duplicated).
     4. PROCEDURE  dbo.S_GetAllDrugCategoryMasters  (new; all rows + audit columns + medicine count, for Redis cache;
                                                     replaces OLD dynamic-SQL getAllDrugCategoryMasterList)
     5. MENU ROW   dbo.NavigationSubMenuMaster      ('Drug Category Master' / drug-category-master under tab 'Purchase')

   After running: assign the page to users/roles in User Authorization.
   ===================================================================== */

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

/* ---------- 1. Table ---------- */
IF OBJECT_ID('dbo.DrugCategoryMasters', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.DrugCategoryMasters
    (
        HospId          int            NOT NULL,
        DrugId          int            IDENTITY(1,1) NOT NULL,
        DrugName        nvarchar(200)  NULL,
        isNarcoticDrug  int            NOT NULL,
        ColorPicker     nvarchar(200)  NULL,
        IsActive        int            NOT NULL CONSTRAINT DF_DrugCategoryMasters_IsActive  DEFAULT (1),
        CreatedBy       int            NULL,
        CreatedOn       datetime       NULL     CONSTRAINT DF_DrugCategoryMasters_CreatedOn DEFAULT (GETDATE()),
        LastModifiedBy  int            NULL,
        LastModifiedOn  datetime       NULL,
        IpAddress       nvarchar(20)   NULL,
        UniqueId        nvarchar(50)   NOT NULL CONSTRAINT DF_DrugCategoryMasters_UniqueId  DEFAULT (NEWID()),
        CONSTRAINT PK_DrugCategoryMasters PRIMARY KEY CLUSTERED (DrugId)
    );
END
GO

/* ---------- 2. OLD data (DEMO_HIS_NEW.dbo.DrugCategoryMasters, 12 rows, OLD ids kept) ---------- */
SET IDENTITY_INSERT dbo.DrugCategoryMasters ON;
INSERT INTO dbo.DrugCategoryMasters (HospId, DrugId, DrugName, isNarcoticDrug, ColorPicker, IsActive, CreatedBy, CreatedOn, LastModifiedBy, LastModifiedOn, IpAddress, UniqueId)
SELECT s.HospId, s.DrugId, s.DrugName, s.isNarcoticDrug, s.ColorPicker, s.IsActive, s.CreatedBy, s.CreatedOn, s.LastModifiedBy, s.LastModifiedOn, s.IpAddress, s.UniqueId
FROM (VALUES
             (1,1,N'Narcotics',1,N'#ce8012',1,1,'2025-03-04 15:12:23.870',1,'2025-03-30 17:32:21.730',N'152.59.190.48',N'8C809F66-0449-4BD0-B989-3D13A9D2DD19'),
             (1,2,N'Sedation',1,N'#e53472',1,1,'2025-03-24 10:59:12.730',1,'2025-03-30 17:32:39.923',N'152.59.190.48',N'D2D071F8-A5E4-4D46-977E-06D0D34040DD'),
             (1,3,N'Electrolytes',1,N'#0540f0',1,1,'2025-03-30 17:33:04.560',NULL,NULL,N'152.59.190.48',N'7DB0BDDD-324D-413F-8919-DFDB6A42D886'),
             (1,4,N'Inotropics',1,N'#2ef545',1,1,'2025-03-30 17:33:25.227',1,'2025-04-01 17:33:46.080',N'152.59.182.61',N'04A274A5-D539-4C31-B6BF-D3ADC5990BC8'),
             (1,5,N'Vasodilators',1,N'#05a9f0',1,1,'2025-03-30 17:33:50.117',NULL,NULL,N'152.59.190.48',N'F4773F20-50F2-46E4-980D-C5F0C591EF30'),
             (1,6,N'Psychotropic Drugs',1,N'#f4f07c',1,1,'2025-03-31 12:55:33.660',NULL,NULL,N'152.58.153.27',N'31511732-7CC1-4C7F-BA3D-F108D5DF88CA'),
             (1,7,N'Cannabis',1,N'#000000',1,1,'2025-05-12 13:40:17.860',NULL,NULL,N'192.168.1.1',N'E6E8F468-C4CF-486B-A0B0-1DC17FADC3F5'),
             (1,8,N'HighRisk',1,N'#19e67c',1,1,'2025-07-22 13:09:47.280',36172,'2026-08-08 11:32:10.300',N'49.36.168.53',N'15163342-04FC-4BD9-B9D7-FED3CED897AA'),
             (1,1008,N'avomin',1,N'#e61919',0,36172,'2026-05-21 13:59:38.667',36172,'2026-05-21 14:00:28.110',N'152.59.172.251',N'CB6B93B1-6FC1-4867-ACBE-280B9B1777AB'),
             (1,1009,N'Narcotics',1,N'#d5c8c8',1,36166,'2026-05-29 13:16:55.073',NULL,NULL,N'152.59.179.15',N'4B1303B0-0A40-4319-B672-6B81260DCFB8'),
             (1,1010,N'Hight',1,N'#000000',1,36166,'2026-06-01 11:45:18.873',NULL,NULL,N'152.59.172.194',N'9A79F400-775B-4669-9B6C-D2DA719201A2'),
             (1,1011,N'Hight',1,N'#4f8d4e',1,36166,'2026-06-01 11:46:04.097',NULL,NULL,N'152.59.172.194',N'71C64E18-DA8D-4CDC-9835-E8A3E48E7553')
     ) s (HospId, DrugId, DrugName, isNarcoticDrug, ColorPicker, IsActive, CreatedBy, CreatedOn, LastModifiedBy, LastModifiedOn, IpAddress, UniqueId)
WHERE NOT EXISTS (SELECT 1 FROM dbo.DrugCategoryMasters t WHERE t.DrugId = s.DrugId);
SET IDENTITY_INSERT dbo.DrugCategoryMasters OFF;

DECLARE @maxId int = (SELECT ISNULL(MAX(DrugId), 0) FROM dbo.DrugCategoryMasters);
DBCC CHECKIDENT ('dbo.DrugCategoryMasters', RESEED, @maxId) WITH NO_INFOMSGS;
GO

/* ---------- 3. Insert / Update ---------- */
CREATE OR ALTER PROCEDURE dbo.IU_DrugCategoryMasters
(
    @hospId          int,
    @drugId          int,
    @drugName        nvarchar(200),
    @isNarcoticDrug  int,
    @colorPicker     nvarchar(200) = NULL,
    @isActive        int = 1,
    @userId          int,
    @IpAddress       nvarchar(20),
    @Result          int = NULL OUTPUT
)
AS
BEGIN
    SET @drugName    = LTRIM(RTRIM(@drugName));
    SET @colorPicker = NULLIF(LTRIM(RTRIM(@colorPicker)), '');

    -- unique name on insert AND update (OLD checked update only)
    IF EXISTS (SELECT 1 FROM dbo.DrugCategoryMasters
               WHERE LTRIM(RTRIM(DrugName)) = @drugName AND DrugId <> @drugId)
    BEGIN
        SET @Result = -1;
        RETURN;
    END

    IF (@drugId = 0)
    BEGIN
        INSERT INTO dbo.DrugCategoryMasters (HospId, DrugName, isNarcoticDrug, ColorPicker, IsActive, CreatedBy, IpAddress)
        VALUES (@hospId, @drugName, @isNarcoticDrug, @colorPicker, @isActive, @userId, @IpAddress);

        SET @Result = SCOPE_IDENTITY();
        RETURN;
    END

    IF NOT EXISTS (SELECT 1 FROM dbo.DrugCategoryMasters WHERE DrugId = @drugId)
    BEGIN
        SET @Result = 0;   -- record not found
        RETURN;
    END

    UPDATE dbo.DrugCategoryMasters
    SET    HospId         = @hospId,
           DrugName       = @drugName,
           isNarcoticDrug = @isNarcoticDrug,
           ColorPicker    = @colorPicker,
           IsActive       = @isActive,
           LastModifiedBy = @userId,
           LastModifiedOn = GETDATE(),
           IpAddress      = @IpAddress
    WHERE  DrugId         = @drugId;

    SET @Result = @drugId;
END
GO

/* ---------- 4. List (all rows; API caches and filters) ---------- */
CREATE OR ALTER PROCEDURE dbo.S_GetAllDrugCategoryMasters
AS
BEGIN
    SELECT  dcm.DrugId,
            dcm.DrugName,
            dcm.isNarcoticDrug               AS IsNarcoticDrug,
            ISNULL(dcm.ColorPicker, '')      AS ColorPicker,
            dcm.IsActive,
            (SELECT COUNT(1) FROM dbo.ServiceItemMaster sim
              WHERE sim.DrugCategoryId = dcm.DrugId) AS MedicineCount,
            ISNULL((SELECT TOP 1 name FROM dbo.UserMaster umm WHERE umm.id = dcm.CreatedBy), '')      AS CreatedBy,
            ISNULL(FORMAT(dcm.CreatedOn, 'dd-MM-yyyy'), '')                                            AS CreatedOn,
            ISNULL((SELECT TOP 1 name FROM dbo.UserMaster umm WHERE umm.id = dcm.LastModifiedBy), '') AS LastModifiedBy,
            ISNULL(FORMAT(dcm.LastModifiedOn, 'dd-MM-yyyy'), '')                                       AS LastModifiedOn
    FROM    dbo.DrugCategoryMasters dcm
    ORDER BY dcm.DrugName;
END
GO

/* ---------- 5. Menu row (tab 'Purchase') ---------- */
IF NOT EXISTS (SELECT 1 FROM dbo.NavigationSubMenuMaster WHERE URL = 'drug-category-master')
BEGIN
    DECLARE @tabId int = (SELECT TOP 1 TabId FROM dbo.NavigationTabMaster
                          WHERE LTRIM(RTRIM(TabName)) = 'Purchase' AND IsActive = 1);
    IF @tabId IS NULL
        PRINT 'Tab "Purchase" not found - add "Drug Category Master" (drug-category-master) from the Navigation Pane screen.';
    ELSE
        INSERT INTO dbo.NavigationSubMenuMaster (TabId, SubMenuName, HospId, IsActive, URL, CreatedOn, CreatedBy, IpAddress, SequenceNo)
        VALUES (@tabId, 'Drug Category Master', 1, 1, 'drug-category-master', GETDATE(), 1, '127.0.0.1', 83);
END
GO
