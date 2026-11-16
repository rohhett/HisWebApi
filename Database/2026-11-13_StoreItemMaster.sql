/* =====================================================================
   Store Item Master (Medicine / General / Blood Bank Inventory items)  —  2026-11-13
   Target DB : GWT_HIS_API (NEW HIS)
   Re-runnable: yes (IF NOT EXISTS / CREATE OR ALTER)
   Run AFTER 2026-11-13_StoreItemMaster_Lookups.sql (and the Manufacturer, Salt, GST Slab, Drug Category and Rack master scripts)

   Design basis: Store_Item_Master_Build_Spec.md section 6 (MediStock design screens + every OLD field and rule).

   Objects
     1. TABLE  ServiceItemMaster    ADD Strength, PackSize, DosageFormId, Barcode, DrugSchedule, RackId, IsColdChain (all NULL-able;
                                    existing callers of the table are not affected)
               (1b, added later the same day) ADD DefaultStoreRoleId, ExpiryAlertDays, IsFEFOIssue, IsBatchMandatory,
                                    AllowExpiredIssue, ItemClass, StorageCondition, IsSterile, IsSingleUse, IsRecallItem,
                                    IsRestrictedItem, IsTrackBatch, IsBarcodeApplicable, IsStockable, IsCapitalItem, IsHighRisk
                                    -> re-run this whole script on the server (it is re-runnable)
     2. DATA   UserRightsMaster     right "CanEditStoreItemMaster" (copied from OLD Id 10041; assign it in User Authorization)
     3. PROC   IU_StoreItemMaster   (new - separate from IU_ServiceItemMaster, which Admin service master and Lab master use)
                 @Result  >0 = ServiceItemId saved
                          -1 = item name already exists in the same Sub-Sub Group            (OLD rule)
                          -2 = item code already exists                                      (OLD rule; empty code allowed)
                          -3 = Medical item without Salt / Dosage Form / HSN / GST (owner decision 2026-11-13;
                               Pack Size dropped from this rule later the same day, owner: screen shows only Packing)
                 Sale Unit is optional (owner removed it from the screen); NULL / 0 -> NULL.
                          -4 = Store Type / Sub Group / Sub-Sub Group changed without right CanEditStoreItemMaster (OLD rule, now also on the server)
                          -5 = Store Type is not 6 Medical / 7 General / 15 Blood Bank Inventory
                          -6 = Sub Group does not belong to the Store Type, or Sub-Sub Group does not belong to the Sub Group
                           0 = ServiceItemId not found
                 Bag Type is hidden on the screen for now (owner decision): IsBagTypeItem / IsBagTypeId are kept on update, 0 on insert.
     4. PROC   S_GetStoreItemList   (new; parameterised search, no dynamic SQL; categories 6, 7, 15)
     5. PROC   S_GetStoreItemDetails(new; one item with every field for edit)
   ===================================================================== */

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

/* ---------- 1. New item columns (design fields) ---------- */
IF COL_LENGTH('dbo.ServiceItemMaster', 'Strength') IS NULL      ALTER TABLE dbo.ServiceItemMaster ADD Strength     nvarchar(100) NULL;
GO
IF COL_LENGTH('dbo.ServiceItemMaster', 'PackSize') IS NULL      ALTER TABLE dbo.ServiceItemMaster ADD PackSize     nvarchar(50)  NULL;
GO
IF COL_LENGTH('dbo.ServiceItemMaster', 'DosageFormId') IS NULL  ALTER TABLE dbo.ServiceItemMaster ADD DosageFormId int           NULL;
GO
IF COL_LENGTH('dbo.ServiceItemMaster', 'Barcode') IS NULL       ALTER TABLE dbo.ServiceItemMaster ADD Barcode      nvarchar(50)  NULL;
GO
IF COL_LENGTH('dbo.ServiceItemMaster', 'DrugSchedule') IS NULL  ALTER TABLE dbo.ServiceItemMaster ADD DrugSchedule nvarchar(20)  NULL;
GO
IF COL_LENGTH('dbo.ServiceItemMaster', 'RackId') IS NULL        ALTER TABLE dbo.ServiceItemMaster ADD RackId       int           NULL;
GO
IF COL_LENGTH('dbo.ServiceItemMaster', 'IsColdChain') IS NULL   ALTER TABLE dbo.ServiceItemMaster ADD IsColdChain  int           NULL;
GO

/* ---------- 1b. Stock location, batch / expiry, NABH / quality and item flags (owner request 2026-11-13,
              from the owner's prototype UI; NULL on old rows = the screen's default is shown) ---------- */
IF COL_LENGTH('dbo.ServiceItemMaster', 'DefaultStoreRoleId') IS NULL  ALTER TABLE dbo.ServiceItemMaster ADD DefaultStoreRoleId int          NULL;  -- store = role (RoleMaster)
GO
IF COL_LENGTH('dbo.ServiceItemMaster', 'ExpiryAlertDays') IS NULL     ALTER TABLE dbo.ServiceItemMaster ADD ExpiryAlertDays    int          NULL;
GO
IF COL_LENGTH('dbo.ServiceItemMaster', 'IsFEFOIssue') IS NULL         ALTER TABLE dbo.ServiceItemMaster ADD IsFEFOIssue        int          NULL;
GO
IF COL_LENGTH('dbo.ServiceItemMaster', 'IsBatchMandatory') IS NULL    ALTER TABLE dbo.ServiceItemMaster ADD IsBatchMandatory   int          NULL;
GO
IF COL_LENGTH('dbo.ServiceItemMaster', 'AllowExpiredIssue') IS NULL   ALTER TABLE dbo.ServiceItemMaster ADD AllowExpiredIssue  int          NULL;
GO
IF COL_LENGTH('dbo.ServiceItemMaster', 'ItemClass') IS NULL           ALTER TABLE dbo.ServiceItemMaster ADD ItemClass          nvarchar(20) NULL;  -- A / B / C
GO
IF COL_LENGTH('dbo.ServiceItemMaster', 'StorageCondition') IS NULL    ALTER TABLE dbo.ServiceItemMaster ADD StorageCondition   nvarchar(50) NULL;
GO
IF COL_LENGTH('dbo.ServiceItemMaster', 'IsSterile') IS NULL           ALTER TABLE dbo.ServiceItemMaster ADD IsSterile          int          NULL;
GO
IF COL_LENGTH('dbo.ServiceItemMaster', 'IsSingleUse') IS NULL         ALTER TABLE dbo.ServiceItemMaster ADD IsSingleUse        int          NULL;
GO
IF COL_LENGTH('dbo.ServiceItemMaster', 'IsRecallItem') IS NULL        ALTER TABLE dbo.ServiceItemMaster ADD IsRecallItem       int          NULL;
GO
IF COL_LENGTH('dbo.ServiceItemMaster', 'IsRestrictedItem') IS NULL    ALTER TABLE dbo.ServiceItemMaster ADD IsRestrictedItem   int          NULL;
GO
IF COL_LENGTH('dbo.ServiceItemMaster', 'IsTrackBatch') IS NULL        ALTER TABLE dbo.ServiceItemMaster ADD IsTrackBatch       int          NULL;
GO
IF COL_LENGTH('dbo.ServiceItemMaster', 'IsBarcodeApplicable') IS NULL ALTER TABLE dbo.ServiceItemMaster ADD IsBarcodeApplicable int         NULL;
GO
IF COL_LENGTH('dbo.ServiceItemMaster', 'IsStockable') IS NULL         ALTER TABLE dbo.ServiceItemMaster ADD IsStockable        int          NULL;
GO
IF COL_LENGTH('dbo.ServiceItemMaster', 'IsCapitalItem') IS NULL       ALTER TABLE dbo.ServiceItemMaster ADD IsCapitalItem      int          NULL;
GO
IF COL_LENGTH('dbo.ServiceItemMaster', 'IsHighRisk') IS NULL          ALTER TABLE dbo.ServiceItemMaster ADD IsHighRisk         int          NULL;
GO

/* ---------- 2. Right CanEditStoreItemMaster ---------- */
IF NOT EXISTS (SELECT 1 FROM dbo.UserRightsMaster WHERE Name = 'CanEditStoreItemMaster')
    INSERT INTO dbo.UserRightsMaster (HospId, Name, DisplayName, Description, IsActive, CreatedOn)
    VALUES (1, N'CanEditStoreItemMaster', N'Edit Store Item Master',
            N'Allows the user to add, update, Store Items in the Store Master module.', 1, GETDATE());
GO

/* ---------- 3. Insert / Update ---------- */
CREATE OR ALTER PROCEDURE dbo.IU_StoreItemMaster
(
    @hospId            int,
    @serviceItemId     int,
    @categoryId        int,
    @subCategoryId     int,
    @subSubCategoryId  int,
    @name              nvarchar(256),
    @code              nvarchar(20)   = NULL,
    @descriptionId     int            = NULL,   -- salt / generic (SaltNameMaster)
    @strength          nvarchar(100)  = NULL,
    @snomedCode        nvarchar(100)  = NULL,
    @manufacturerId    int,
    @dosageFormId      int            = NULL,
    @drugCategoryId    int            = NULL,
    @barcode           nvarchar(50)   = NULL,
    @packSize          nvarchar(50)   = NULL,
    @purchaseUnitId    int,
    @saleUnitId        int            = NULL,
    @packing           int,
    @hsnCode           nvarchar(20)   = NULL,
    @commodityCode     nvarchar(20)   = NULL,
    @gstPer            decimal(18,2)  = NULL,
    @drugSchedule      nvarchar(20)   = NULL,
    @rackId            int            = NULL,
    @isColdChain       int            = 0,
    @expirable         int            = 1,
    @vedType           nvarchar(20)   = NULL,
    @minLevel          int            = NULL,
    @reorderLevel      int            = NULL,
    @maxLevel          int            = NULL,
    @doctorId          int            = NULL,
    @isKitTypeItem     int            = 0,
    @isActive          int            = 1,
    -- stock location, batch / expiry, NABH / quality, item flags
    @defaultStoreRoleId  int          = NULL,
    @expiryAlertDays     int          = NULL,
    @isFEFOIssue         int          = 1,
    @isBatchMandatory    int          = 1,
    @allowExpiredIssue   int          = 0,
    @itemClass           nvarchar(20) = NULL,
    @storageCondition    nvarchar(50) = NULL,
    @isSterile           int          = 0,
    @isSingleUse         int          = 0,
    @isRecallItem        int          = 0,
    @isRestrictedItem    int          = 0,
    @isTrackBatch        int          = 1,
    @isBarcodeApplicable int          = 1,
    @isStockable         int          = 1,
    @isCapitalItem       int          = 0,
    @isHighRisk          int          = 0,
    @userId            int,
    @IpAddress         nvarchar(20),
    @Result            int = NULL OUTPUT
)
AS
BEGIN
    SET NOCOUNT ON;

    SET @name          = LTRIM(RTRIM(@name));
    SET @code          = NULLIF(LTRIM(RTRIM(@code)), '');
    SET @strength      = NULLIF(LTRIM(RTRIM(@strength)), '');
    SET @snomedCode    = NULLIF(LTRIM(RTRIM(@snomedCode)), '');
    SET @barcode       = NULLIF(LTRIM(RTRIM(@barcode)), '');
    SET @packSize      = NULLIF(LTRIM(RTRIM(@packSize)), '');
    SET @hsnCode       = NULLIF(LTRIM(RTRIM(@hsnCode)), '');
    SET @commodityCode = NULLIF(LTRIM(RTRIM(@commodityCode)), '');
    SET @drugSchedule  = NULLIF(LTRIM(RTRIM(@drugSchedule)), '');
    SET @vedType       = NULLIF(LTRIM(RTRIM(@vedType)), '');
    IF @descriptionId  <= 0 SET @descriptionId  = NULL;
    IF @dosageFormId   <= 0 SET @dosageFormId   = NULL;
    IF @drugCategoryId <= 0 SET @drugCategoryId = NULL;
    IF @rackId         <= 0 SET @rackId         = NULL;
    IF @doctorId       <= 0 SET @doctorId       = NULL;
    IF @defaultStoreRoleId <= 0 SET @defaultStoreRoleId = NULL;
    SET @itemClass        = NULLIF(LTRIM(RTRIM(@itemClass)), '');
    SET @storageCondition = NULLIF(LTRIM(RTRIM(@storageCondition)), '');

    -- store type
    IF @categoryId NOT IN (6, 7, 15)
    BEGIN SET @Result = -5; RETURN; END

    -- group hierarchy
    IF NOT EXISTS (SELECT 1 FROM dbo.SubCategoryMaster WHERE SubCategoryId = @subCategoryId AND CategoryId = @categoryId)
       OR NOT EXISTS (SELECT 1 FROM dbo.SubSubCategoryMaster WHERE SubSubCategoryId = @subSubCategoryId AND SubCategoryId = @subCategoryId)
    BEGIN SET @Result = -6; RETURN; END

    -- Medical items: salt, dosage form, pack size, HSN and GST are mandatory
    IF @saleUnitId <= 0 SET @saleUnitId = NULL;

    IF @categoryId = 6 AND (@descriptionId IS NULL OR @dosageFormId IS NULL OR @hsnCode IS NULL OR @gstPer IS NULL)
    BEGIN SET @Result = -3; RETURN; END

    -- OLD rule: name unique within the Sub-Sub Group
    IF EXISTS (SELECT 1 FROM dbo.ServiceItemMaster
               WHERE LTRIM(RTRIM(Name)) = @name AND SubSubCategoryId = @subSubCategoryId AND ServiceItemId <> @serviceItemId)
    BEGIN SET @Result = -1; RETURN; END

    -- OLD rule: a non-empty code is unique on every row
    IF @code IS NOT NULL
       AND EXISTS (SELECT 1 FROM dbo.ServiceItemMaster WHERE LTRIM(RTRIM(Code)) = @code AND ServiceItemId <> @serviceItemId)
    BEGIN SET @Result = -2; RETURN; END

    DECLARE @saltName         nvarchar(512) = (SELECT TOP 1 LTRIM(RTRIM(SaltName)) FROM dbo.SaltNameMaster WHERE SaltNameId = @descriptionId);
    DECLARE @purchaseUnitName nvarchar(256) = (SELECT TOP 1 PurchaseUnitName FROM dbo.PurchaseUnitMaster WHERE PurchaseUnitId = @purchaseUnitId);
    DECLARE @saleUnitName     nvarchar(256) = (SELECT TOP 1 SaleUnitName FROM dbo.SaleUnitMaster WHERE SaleUnitId = @saleUnitId);

    IF (@serviceItemId = 0)
    BEGIN
        INSERT INTO dbo.ServiceItemMaster
        (
            HospId, CategoryId, SubCategoryId, SubSubCategoryId, Name, Code, IsActive, CreatedBy, IpAddress,
            DescriptionId, ItemDiscription, Strength, SNOMEDCode, ManufacturerId, DosageFormId, DrugCategoryId, Barcode,
            PackSize, PurchaseUnitId, PurchaseUnitName, SaleUnitId, SaleUnitName, Packing, HSNCode, CommodityCode, GSTPer,
            DrugSchedule, RackId, IsColdChain, Expirable, VEDType, MinLevel, ReorderLevel, MaxLevel, DoctorId,
            IsKitTypeItem, IsBagTypeItem, IsBagTypeId,
            DefaultStoreRoleId, ExpiryAlertDays, IsFEFOIssue, IsBatchMandatory, AllowExpiredIssue, ItemClass, StorageCondition,
            IsSterile, IsSingleUse, IsRecallItem, IsRestrictedItem,
            IsTrackBatch, IsBarcodeApplicable, IsStockable, IsCapitalItem, IsHighRisk
        )
        VALUES
        (
            @hospId, @categoryId, @subCategoryId, @subSubCategoryId, @name, @code, @isActive, @userId, @IpAddress,
            @descriptionId, @saltName, @strength, @snomedCode, @manufacturerId, @dosageFormId, @drugCategoryId, @barcode,
            @packSize, @purchaseUnitId, @purchaseUnitName, @saleUnitId, @saleUnitName, @packing, @hsnCode, @commodityCode, @gstPer,
            @drugSchedule, @rackId, @isColdChain, @expirable, @vedType,
            CAST(@minLevel AS nvarchar(100)), CAST(@reorderLevel AS nvarchar(100)), CAST(@maxLevel AS nvarchar(100)), @doctorId,
            @isKitTypeItem, 0, 0,
            @defaultStoreRoleId, @expiryAlertDays, @isFEFOIssue, @isBatchMandatory, @allowExpiredIssue, @itemClass, @storageCondition,
            @isSterile, @isSingleUse, @isRecallItem, @isRestrictedItem,
            @isTrackBatch, @isBarcodeApplicable, @isStockable, @isCapitalItem, @isHighRisk
        );

        SET @Result = SCOPE_IDENTITY();
        RETURN;
    END

    DECLARE @oldCategoryId int, @oldSubCategoryId int, @oldSubSubCategoryId int;
    SELECT @oldCategoryId = CategoryId, @oldSubCategoryId = SubCategoryId, @oldSubSubCategoryId = SubSubCategoryId
    FROM dbo.ServiceItemMaster WHERE ServiceItemId = @serviceItemId AND CategoryId IN (6, 7, 15);

    IF @oldCategoryId IS NULL
    BEGIN SET @Result = 0; RETURN; END

    -- OLD rule (was browser-only): changing the groups of a saved item needs right CanEditStoreItemMaster
    IF (@oldCategoryId <> @categoryId OR @oldSubCategoryId <> @subCategoryId OR @oldSubSubCategoryId <> @subSubCategoryId)
       AND NOT EXISTS (SELECT 1
                       FROM dbo.UserRightMapping urm
                       JOIN dbo.UserRightsMaster ur ON ur.Id = urm.UserRightId
                       WHERE ur.Name = 'CanEditStoreItemMaster' AND urm.UserId = @userId AND urm.IsActive = 1)
    BEGIN SET @Result = -4; RETURN; END

    UPDATE dbo.ServiceItemMaster
    SET    HospId = @hospId, CategoryId = @categoryId, SubCategoryId = @subCategoryId, SubSubCategoryId = @subSubCategoryId,
           Name = @name, Code = @code, IsActive = @isActive,
           DescriptionId = @descriptionId, ItemDiscription = @saltName, Strength = @strength, SNOMEDCode = @snomedCode,
           ManufacturerId = @manufacturerId, DosageFormId = @dosageFormId, DrugCategoryId = @drugCategoryId, Barcode = @barcode,
           PackSize = @packSize, PurchaseUnitId = @purchaseUnitId, PurchaseUnitName = @purchaseUnitName,
           SaleUnitId = @saleUnitId, SaleUnitName = @saleUnitName, Packing = @packing,
           HSNCode = @hsnCode, CommodityCode = @commodityCode, GSTPer = @gstPer,
           DrugSchedule = @drugSchedule, RackId = @rackId, IsColdChain = @isColdChain, Expirable = @expirable, VEDType = @vedType,
           MinLevel = CAST(@minLevel AS nvarchar(100)), ReorderLevel = CAST(@reorderLevel AS nvarchar(100)),
           MaxLevel = CAST(@maxLevel AS nvarchar(100)), DoctorId = @doctorId, IsKitTypeItem = @isKitTypeItem,
           DefaultStoreRoleId = @defaultStoreRoleId, ExpiryAlertDays = @expiryAlertDays, IsFEFOIssue = @isFEFOIssue,
           IsBatchMandatory = @isBatchMandatory, AllowExpiredIssue = @allowExpiredIssue, ItemClass = @itemClass,
           StorageCondition = @storageCondition, IsSterile = @isSterile, IsSingleUse = @isSingleUse,
           IsRecallItem = @isRecallItem, IsRestrictedItem = @isRestrictedItem, IsTrackBatch = @isTrackBatch,
           IsBarcodeApplicable = @isBarcodeApplicable, IsStockable = @isStockable, IsCapitalItem = @isCapitalItem,
           IsHighRisk = @isHighRisk,
           -- IsBagTypeItem / IsBagTypeId deliberately not touched (Bag Type hidden until Blood Bank is migrated)
           LastModifiedBy = @userId, LastModifiedOn = GETDATE(), IpAddress = @IpAddress
    WHERE  ServiceItemId = @serviceItemId;

    SET @Result = @serviceItemId;
END
GO

/* ---------- 4. List (parameterised search) ---------- */
CREATE OR ALTER PROCEDURE dbo.S_GetStoreItemList
(
    @search      nvarchar(100) = NULL,   -- name, code, salt or barcode contains
    @categoryId  int           = NULL,   -- 6 / 7 / 15, NULL = all three
    @isActive    int           = NULL,   -- NULL = active and inactive (as OLD)
    @top         int           = 500
)
AS
BEGIN
    SET NOCOUNT ON;
    SET @search = NULLIF(LTRIM(RTRIM(@search)), '');

    SELECT TOP (@top)
            sim.ServiceItemId,
            sim.CategoryId,
            cm.CategoryName                         AS StoreType,
            ISNULL(sc.SubCategoryName, '')          AS SubGroup,
            ISNULL(ssc.SubSubCategoryName, '')      AS SubSubGroup,
            ISNULL(sim.Code, '')                    AS Code,
            sim.Name,
            ISNULL(sim.ItemDiscription, '')         AS SaltName,
            ISNULL(sim.Strength, '')                AS Strength,
            ISNULL(sim.SNOMEDCode, '')              AS SnomedCode,
            ISNULL(mm.ManufacturerName, '')         AS ManufacturerName,
            ISNULL(df.DosageFormName, '')           AS DosageFormName,
            ISNULL(sim.PackSize, '')                AS PackSize,
            ISNULL(sim.SaleUnitName, '')            AS SaleUnitName,
            ISNULL(sim.Packing, 0)                  AS Packing,
            ISNULL(sim.HSNCode, '')                 AS HsnCode,
            sim.GSTPer                              AS GstPer,
            ISNULL(sim.DrugSchedule, '')            AS DrugSchedule,
            ISNULL(rm.RackCode, '')                 AS RackCode,
            ISNULL(sim.ReorderLevel, '')            AS ReorderLevel,
            ISNULL(sim.IsColdChain, 0)              AS IsColdChain,
            ISNULL(sim.IsActive, 0)                 AS IsActive
    FROM    dbo.ServiceItemMaster sim
    JOIN    dbo.CategoryMaster cm            ON cm.CategoryId = sim.CategoryId
    LEFT JOIN dbo.SubCategoryMaster sc       ON sc.SubCategoryId = sim.SubCategoryId
    LEFT JOIN dbo.SubSubCategoryMaster ssc   ON ssc.SubSubCategoryId = sim.SubSubCategoryId
    LEFT JOIN dbo.ManufacturerMaster mm      ON mm.ManufacturerId = sim.ManufacturerId
    LEFT JOIN dbo.DosageFormMaster df        ON df.DosageFormId = sim.DosageFormId
    LEFT JOIN dbo.RackMaster rm              ON rm.RackId = sim.RackId
    WHERE   sim.CategoryId IN (6, 7, 15)
      AND   (@categoryId IS NULL OR sim.CategoryId = @categoryId)
      AND   (@isActive IS NULL OR sim.IsActive = @isActive)
      AND   (@search IS NULL
             OR sim.Name            LIKE N'%' + @search + N'%'
             OR sim.Code            LIKE N'%' + @search + N'%'
             OR sim.ItemDiscription LIKE N'%' + @search + N'%'
             OR sim.Barcode         LIKE N'%' + @search + N'%')
    ORDER BY sim.Name;
END
GO

/* ---------- 5. Details (edit) ---------- */
CREATE OR ALTER PROCEDURE dbo.S_GetStoreItemDetails
(
    @serviceItemId int
)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT  sim.ServiceItemId,
            sim.CategoryId, sim.SubCategoryId, sim.SubSubCategoryId,
            sim.Name, ISNULL(sim.Code, '') AS Code,
            ISNULL(sim.DescriptionId, 0)   AS DescriptionId,  ISNULL(sim.ItemDiscription, '') AS SaltName,
            ISNULL(sim.Strength, '')       AS Strength,       ISNULL(sim.SNOMEDCode, '')      AS SnomedCode,
            ISNULL(sim.ManufacturerId, 0)  AS ManufacturerId, ISNULL(sim.DosageFormId, 0)     AS DosageFormId,
            ISNULL(sim.DrugCategoryId, 0)  AS DrugCategoryId, ISNULL(sim.Barcode, '')         AS Barcode,
            ISNULL(sim.PackSize, '')       AS PackSize,
            ISNULL(sim.PurchaseUnitId, 0)  AS PurchaseUnitId, ISNULL(sim.SaleUnitId, 0)       AS SaleUnitId,
            ISNULL(sim.Packing, 0)         AS Packing,
            ISNULL(sim.HSNCode, '')        AS HsnCode,        ISNULL(sim.CommodityCode, '')   AS CommodityCode,
            sim.GSTPer                     AS GstPer,
            ISNULL(sim.DrugSchedule, '')   AS DrugSchedule,   ISNULL(sim.RackId, 0)           AS RackId,
            ISNULL(sim.IsColdChain, 0)     AS IsColdChain,    ISNULL(sim.Expirable, 0)        AS Expirable,
            ISNULL(sim.VEDType, '')        AS VedType,
            TRY_CAST(sim.MinLevel AS int)     AS MinLevel,
            TRY_CAST(sim.ReorderLevel AS int) AS ReorderLevel,
            TRY_CAST(sim.MaxLevel AS int)     AS MaxLevel,
            ISNULL(sim.DoctorId, 0)        AS DoctorId,
            ISNULL(sim.IsKitTypeItem, 0)   AS IsKitTypeItem,  ISNULL(sim.IsBagTypeItem, 0)    AS IsBagTypeItem,
            ISNULL(sim.IsActive, 0)        AS IsActive,
            -- NULL (rows saved before these fields existed) -> the screen's default
            ISNULL(sim.DefaultStoreRoleId, 0)   AS DefaultStoreRoleId,
            ISNULL(sim.ExpiryAlertDays, 90)     AS ExpiryAlertDays,
            ISNULL(sim.IsFEFOIssue, 1)          AS IsFEFOIssue,
            ISNULL(sim.IsBatchMandatory, 1)     AS IsBatchMandatory,
            ISNULL(sim.AllowExpiredIssue, 0)    AS AllowExpiredIssue,
            ISNULL(sim.ItemClass, '')           AS ItemClass,
            ISNULL(sim.StorageCondition, '')    AS StorageCondition,
            ISNULL(sim.IsSterile, 0)            AS IsSterile,
            ISNULL(sim.IsSingleUse, 0)          AS IsSingleUse,
            ISNULL(sim.IsRecallItem, 0)         AS IsRecallItem,
            ISNULL(sim.IsRestrictedItem, 0)     AS IsRestrictedItem,
            ISNULL(sim.IsTrackBatch, 1)         AS IsTrackBatch,
            ISNULL(sim.IsBarcodeApplicable, 1)  AS IsBarcodeApplicable,
            ISNULL(sim.IsStockable, 1)          AS IsStockable,
            ISNULL(sim.IsCapitalItem, 0)        AS IsCapitalItem,
            ISNULL(sim.IsHighRisk, 0)           AS IsHighRisk,
            ISNULL((SELECT TOP 1 name FROM dbo.UserMaster umm WHERE umm.id = sim.CreatedBy), '')      AS CreatedBy,
            ISNULL(FORMAT(sim.CreatedOn, 'dd-MM-yyyy'), '')                                            AS CreatedOn,
            ISNULL((SELECT TOP 1 name FROM dbo.UserMaster umm WHERE umm.id = sim.LastModifiedBy), '') AS LastModifiedBy,
            ISNULL(FORMAT(sim.LastModifiedOn, 'dd-MM-yyyy'), '')                                       AS LastModifiedOn
    FROM    dbo.ServiceItemMaster sim
    WHERE   sim.ServiceItemId = @serviceItemId AND sim.CategoryId IN (6, 7, 15);
END
GO
