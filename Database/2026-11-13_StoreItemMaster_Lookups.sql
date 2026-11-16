/* =====================================================================
   Store Item Master - lookup masters  —  2026-11-13
   Target DB : GWT_HIS_API (NEW HIS)
   Re-runnable: yes (IF NOT EXISTS / CREATE OR ALTER; data rows inserted only if the id is missing)
   Run BEFORE 2026-11-13_StoreItemMaster.sql

   Objects
     1. TABLE PurchaseUnitMaster  + 50 OLD rows (OLD ids kept)  + IU_PurchaseUnitMaster + S_GetPurchaseUnitList
     2. TABLE SaleUnitMaster      +  5 OLD rows (OLD ids kept)  + IU_SaleUnitMaster     + S_GetSaleUnitList
     3. TABLE DosageFormMaster    (new, design "Category / Dosage Form") + 6 starter rows + IU_DosageFormMaster + S_GetDosageFormList

   IU_* procedures: @Result >0 = id saved, -1 = name already exists (checked on insert and update), 0 = id not found.
   S_Get*List: all rows + audit columns (+ number of store items using each unit); the API caches and filters.

   OLD data notes (copied as is, for the owner to clean up later):
     PurchaseUnitMaster holds manufacturer names (LUPIN LABORATORIES, NOVARTIS, CIPLA, ...), "xyz", "INC""",
     near-duplicates (PRE-FILLED SYRINGE / PREFILLED SYRINGE, AMPOULE / AMPULE / AMP, KIT / KITS, BAG / BAGS);
     id 16 had no name but 130 OLD items use it -> copied as "(blank)".
     SaleUnitMaster: "pc" (3) and "pcs" (5) look like the same unit.
   ===================================================================== */

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

/* =========================== 1. Purchase Unit =========================== */
IF OBJECT_ID('dbo.PurchaseUnitMaster', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.PurchaseUnitMaster
    (
        HospId            int            NOT NULL,
        PurchaseUnitId    int            IDENTITY(1,1) NOT NULL,
        PurchaseUnitName  nvarchar(256)  NULL,
        IsActive          int            NOT NULL CONSTRAINT DF_PurchaseUnitMaster_IsActive  DEFAULT (1),
        CreatedBy         int            NULL,
        CreatedOn         datetime       NULL     CONSTRAINT DF_PurchaseUnitMaster_CreatedOn DEFAULT (GETDATE()),
        LastModifiedBy    int            NULL,
        LastModifiedOn    datetime       NULL,
        IpAddress         nvarchar(20)   NULL,
        UniqueId          nvarchar(50)   NOT NULL CONSTRAINT DF_PurchaseUnitMaster_UniqueId  DEFAULT (NEWID()),
        CONSTRAINT PK_PurchaseUnitMaster PRIMARY KEY CLUSTERED (PurchaseUnitId)
    );
END
GO

SET IDENTITY_INSERT dbo.PurchaseUnitMaster ON;
INSERT INTO dbo.PurchaseUnitMaster (HospId, PurchaseUnitId, PurchaseUnitName, IsActive, CreatedBy, CreatedOn, LastModifiedBy, LastModifiedOn, IpAddress, UniqueId)
SELECT s.HospId, s.Id, s.Name, s.IsActive, s.CreatedBy, s.CreatedOn, s.LastModifiedBy, s.LastModifiedOn, s.IpAddress, s.UniqueId
FROM (VALUES
             (1,1,N'RESPULES',1,1,'2025-05-06 11:33:02.307',NULL,NULL,NULL,N'38A88EC1-B4DD-4E92-98E0-AA924286E46A'),
             (1,2,N'AMP',1,1,'2025-05-06 11:33:02.307',NULL,NULL,NULL,N'FB2C3501-2EAF-48F7-963A-644F69766D15'),
             (1,3,N'BOTTLES',1,1,'2025-05-06 11:33:02.307',NULL,NULL,NULL,N'DC01B209-A4F5-4231-A4CA-23AD7FD05D83'),
             (1,4,N'NUMBERS',1,1,'2025-05-06 11:33:02.307',NULL,NULL,NULL,N'0EB1AF78-84D4-45F2-8179-2D795BAD2BB0'),
             (1,5,N'PLASTIC JAR',1,1,'2025-05-06 11:33:02.307',NULL,NULL,NULL,N'4CA954C4-FE35-4733-84CC-4FC5EC93DBBE'),
             (1,6,N'CARTRIDGE',1,1,'2025-05-06 11:33:02.307',NULL,NULL,NULL,N'E202C13F-EF50-49B9-938A-4231364E85C7'),
             (1,7,N'CARTON',1,1,'2025-05-06 11:33:02.307',NULL,NULL,NULL,N'84FBA868-B1F9-439E-BC62-C5BB9BD2F96B'),
             (1,8,N'JAR',1,1,'2025-05-06 11:33:02.307',NULL,NULL,NULL,N'2059FAB7-1EDA-4978-BFD0-B600AF4F097D'),
             (1,9,N'LUPIN LABORATORIES',1,1,'2025-05-06 11:33:02.307',NULL,NULL,NULL,N'5B3A8EC5-33E1-46BB-B9D8-76100BE69DA6'),
             (1,10,N'INTEGRACE',1,1,'2025-05-06 11:33:02.307',NULL,NULL,NULL,N'601E6616-5E0E-4867-8E33-F0EDDBC2FD8D'),
             (1,11,N'CATRIGES',1,1,'2025-05-06 11:33:02.307',NULL,NULL,NULL,N'73090989-1CBD-456E-A230-31A31AE0FA47'),
             (1,12,N'NOVARTIS',1,1,'2025-05-06 11:33:02.307',NULL,NULL,NULL,N'2AC24544-1643-40C8-87FE-B84C8DD425C9'),
             (1,13,N'EMCURE PHARMA',1,1,'2025-05-06 11:33:02.307',NULL,NULL,NULL,N'3297C36F-261E-4EB8-9864-A7218A3D44F6'),
             (1,14,N'NOT SPECIFIED',1,1,'2025-05-06 11:33:02.307',NULL,NULL,NULL,N'085B4537-D337-4BCC-ABD1-E32AFF441D82'),
             (1,15,N'CAN',1,1,'2025-05-06 11:33:02.307',NULL,NULL,NULL,N'3A28F1D3-E5F2-42CB-9A99-161D902874B2'),
             (1,16,N'(blank)',1,1,'2025-05-06 11:33:02.307',NULL,NULL,NULL,N'F9650E0F-54AA-4F87-8D47-925BBF791732'),
             (1,17,N'BUNDLES',1,1,'2025-05-06 11:33:02.307',NULL,NULL,NULL,N'9F202EA0-EC35-4CA5-91ED-DAA906E4A9DE'),
             (1,18,N'WOCKHARDT LIMITED',1,1,'2025-05-06 11:33:02.307',NULL,NULL,NULL,N'6BD80459-3C32-453F-AAE5-38DA1E7751B9'),
             (1,19,N'AMPOULE',1,1,'2025-05-06 11:33:02.307',NULL,NULL,NULL,N'F01152CF-A30D-4BBA-A72B-60568E74B7DD'),
             (1,20,N'VIAFLEX',1,1,'2025-05-06 11:33:02.307',NULL,NULL,NULL,N'93F9A79A-0FCB-4763-B281-B284968D2CB4'),
             (1,21,N'SUN PHARMACEUTICALS',1,1,'2025-05-06 11:33:02.307',NULL,NULL,NULL,N'3838A5B9-17A4-4A35-A968-E87A0A05E54F'),
             (1,22,N'VIALS',1,1,'2025-05-06 11:33:02.307',NULL,NULL,NULL,N'04EC693B-FCCA-4726-A17B-391126044DDF'),
             (1,23,N'AKUMS DRUGS & PHARMACEUTICALS LTD.',1,1,'2025-05-06 11:33:02.307',NULL,NULL,NULL,N'05666026-3633-46F3-9AD9-3BBF0F3CF8B7'),
             (1,24,N'PRE-FILLED PEN',1,1,'2025-05-06 11:33:02.307',NULL,NULL,NULL,N'48067FFB-0AF5-4604-9611-BC7CA3186C57'),
             (1,25,N'PACKETS',1,1,'2025-05-06 11:33:02.307',NULL,NULL,NULL,N'9AC8F8C4-7EBB-4AF2-B40E-A3E3B9ED09B4'),
             (1,26,N'BAGS',1,1,'2025-05-06 11:33:02.307',NULL,NULL,NULL,N'7A4BDF54-9429-403E-9EFC-38813C0133BD'),
             (1,27,N'KIT',1,1,'2025-05-06 11:33:02.307',NULL,NULL,NULL,N'C05EE078-31D7-47E0-A9D6-4FA2E4E64E27'),
             (1,28,N'PRE-FILLED SYRINGE',1,1,'2025-05-06 11:33:02.307',NULL,NULL,NULL,N'BAF5F10C-7725-4FC3-B904-D2042F4B8221'),
             (1,29,N'PREFILLED SYRINGE',1,1,'2025-05-06 11:33:02.307',NULL,NULL,NULL,N'0DFF623C-25AE-4AB9-8CC7-EB76EBC6CD2E'),
             (1,30,N'PLASTIC',1,1,'2025-05-06 11:33:02.307',NULL,NULL,NULL,N'EEB40C15-8DB6-44ED-B46B-C21041B2B845'),
             (1,31,N'BOX',1,1,'2025-05-06 11:33:02.307',NULL,NULL,NULL,N'6492B932-DF1E-4645-A678-CAE768C5EC4E'),
             (1,32,N'AMPULE',1,1,'2025-05-06 11:33:02.307',NULL,NULL,NULL,N'DB2CA6FE-F7A7-444E-856F-ACE44BC7ECC4'),
             (1,33,N'BAG',1,1,'2025-05-06 11:33:02.307',NULL,NULL,NULL,N'B04F324C-4FD8-416B-8FD1-465A2DA6B354'),
             (1,34,N'CUBIC METER',1,1,'2025-05-06 11:33:02.307',NULL,NULL,NULL,N'E86AF94E-B4E3-49AC-AF0E-8736875FD2B7'),
             (1,35,N'CONTAINER',1,1,'2025-05-06 11:33:02.307',NULL,NULL,NULL,N'D011547D-FD85-467E-B72E-603F6DDADB3E'),
             (1,36,N'TUBE',1,1,'2025-05-06 11:33:02.307',NULL,NULL,NULL,N'ABB47A32-6B07-4626-ADC2-39E0655CEDDE'),
             (1,37,N'TIN',1,1,'2025-05-06 11:33:02.307',NULL,NULL,NULL,N'9EA4AD5E-A9CC-4839-9948-FC4556F145A6'),
             (1,38,N'IVF',1,1,'2025-05-06 11:33:02.307',NULL,NULL,NULL,N'45E6D095-6FA0-42B9-B3A1-224ED468B86A'),
             (1,39,N'LITRES',1,1,'2025-05-06 11:33:02.307',NULL,NULL,NULL,N'5496088B-82ED-4D9A-83F5-0DF6ED947C77'),
             (1,40,N'INC"',1,1,'2025-05-06 11:33:02.307',NULL,NULL,NULL,N'02BB41FC-5034-4293-81B9-C3E0FF3D1A7F'),
             (1,41,N'CIPLA',1,1,'2025-05-06 11:33:02.307',NULL,NULL,NULL,N'A0AC3AE7-BF3C-41FE-955E-4D5F303F8150'),
             (1,42,N'INJECTION',1,1,'2025-05-06 11:33:02.307',NULL,NULL,NULL,N'E2F0940A-EADC-4C8F-A1F8-C577CE93AEF1'),
             (1,43,N'KITS',1,1,'2025-05-06 11:33:02.307',NULL,NULL,NULL,N'0AE5AEFD-6877-40A1-81CD-D62F0316B583'),
             (1,44,N'MEATERS',1,1,'2025-05-06 11:33:02.307',NULL,NULL,NULL,N'CBDE2611-D077-4AD2-8476-BCEA2C6E4F06'),
             (1,45,N'NOUVEAU MEDICAMENT P LTD',1,1,'2025-05-06 11:33:02.307',NULL,NULL,NULL,N'E9E299B2-31E8-422A-8A7E-2E0AD584F215'),
             (1,46,N'BOOKS',1,1,'2025-05-06 11:33:02.307',NULL,NULL,NULL,N'7A0FB0CB-FB76-41DF-BDBA-6F7FE411EDAB'),
             (1,47,N'STRIPS',1,1,'2025-05-06 11:33:02.307',NULL,NULL,NULL,N'405B6A31-9F0C-48D6-8EB1-A2B76D327797'),
             (1,48,N'PCS',1,1,'2025-05-06 16:30:28.043',NULL,NULL,N'112.79.255.0',N'D78E181E-4A09-48E1-8871-193F891E834B'),
             (1,49,N'UNIT',1,1,'2025-06-09 12:58:01.433',NULL,NULL,N'192.168.1.23',N'2A099D4B-381F-4873-90B0-88D8C21C70CF'),
             (1,50,N'xyz',1,1,'2025-10-30 13:38:56.163',NULL,NULL,N'42.107.140.226',N'1F17EFE9-27B4-4EDE-9D29-BE30A49A33D8')
     ) s (HospId, Id, Name, IsActive, CreatedBy, CreatedOn, LastModifiedBy, LastModifiedOn, IpAddress, UniqueId)
WHERE NOT EXISTS (SELECT 1 FROM dbo.PurchaseUnitMaster t WHERE t.PurchaseUnitId = s.Id);
SET IDENTITY_INSERT dbo.PurchaseUnitMaster OFF;
DECLARE @maxPU int = (SELECT ISNULL(MAX(PurchaseUnitId), 0) FROM dbo.PurchaseUnitMaster);
DBCC CHECKIDENT ('dbo.PurchaseUnitMaster', RESEED, @maxPU) WITH NO_INFOMSGS;
GO

CREATE OR ALTER PROCEDURE dbo.IU_PurchaseUnitMaster
(
    @hospId            int,
    @purchaseUnitId    int,
    @purchaseUnitName  nvarchar(256),
    @isActive          int = 1,
    @userId            int,
    @IpAddress         nvarchar(20),
    @Result            int = NULL OUTPUT
)
AS
BEGIN
    SET @purchaseUnitName = LTRIM(RTRIM(@purchaseUnitName));

    IF EXISTS (SELECT 1 FROM dbo.PurchaseUnitMaster WHERE LTRIM(RTRIM(PurchaseUnitName)) = @purchaseUnitName AND PurchaseUnitId <> @purchaseUnitId)
    BEGIN SET @Result = -1; RETURN; END

    IF (@purchaseUnitId = 0)
    BEGIN
        INSERT INTO dbo.PurchaseUnitMaster (HospId, PurchaseUnitName, IsActive, CreatedBy, IpAddress)
        VALUES (@hospId, @purchaseUnitName, @isActive, @userId, @IpAddress);
        SET @Result = SCOPE_IDENTITY();
        RETURN;
    END

    IF NOT EXISTS (SELECT 1 FROM dbo.PurchaseUnitMaster WHERE PurchaseUnitId = @purchaseUnitId)
    BEGIN SET @Result = 0; RETURN; END

    UPDATE dbo.PurchaseUnitMaster
    SET HospId = @hospId, PurchaseUnitName = @purchaseUnitName, IsActive = @isActive,
        LastModifiedBy = @userId, LastModifiedOn = GETDATE(), IpAddress = @IpAddress
    WHERE PurchaseUnitId = @purchaseUnitId;

    SET @Result = @purchaseUnitId;
END
GO

CREATE OR ALTER PROCEDURE dbo.S_GetPurchaseUnitList
AS
BEGIN
    SELECT  pu.PurchaseUnitId,
            ISNULL(pu.PurchaseUnitName, '') AS PurchaseUnitName,
            pu.IsActive,
            (SELECT COUNT(1) FROM dbo.ServiceItemMaster sim WHERE sim.PurchaseUnitId = pu.PurchaseUnitId) AS ItemCount,
            ISNULL((SELECT TOP 1 name FROM dbo.UserMaster umm WHERE umm.id = pu.CreatedBy), '')      AS CreatedBy,
            ISNULL(FORMAT(pu.CreatedOn, 'dd-MM-yyyy'), '')                                            AS CreatedOn,
            ISNULL((SELECT TOP 1 name FROM dbo.UserMaster umm WHERE umm.id = pu.LastModifiedBy), '') AS LastModifiedBy,
            ISNULL(FORMAT(pu.LastModifiedOn, 'dd-MM-yyyy'), '')                                       AS LastModifiedOn
    FROM dbo.PurchaseUnitMaster pu
    ORDER BY pu.PurchaseUnitName;
END
GO

/* =========================== 2. Sale Unit =========================== */
IF OBJECT_ID('dbo.SaleUnitMaster', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.SaleUnitMaster
    (
        HospId          int            NOT NULL,
        SaleUnitId      int            IDENTITY(1,1) NOT NULL,
        SaleUnitName    nvarchar(256)  NULL,
        IsActive        int            NOT NULL CONSTRAINT DF_SaleUnitMaster_IsActive  DEFAULT (1),
        CreatedBy       int            NULL,
        CreatedOn       datetime       NULL     CONSTRAINT DF_SaleUnitMaster_CreatedOn DEFAULT (GETDATE()),
        LastModifiedBy  int            NULL,
        LastModifiedOn  datetime       NULL,
        IpAddress       nvarchar(20)   NULL,
        UniqueId        nvarchar(50)   NOT NULL CONSTRAINT DF_SaleUnitMaster_UniqueId  DEFAULT (NEWID()),
        CONSTRAINT PK_SaleUnitMaster PRIMARY KEY CLUSTERED (SaleUnitId)
    );
END
GO

SET IDENTITY_INSERT dbo.SaleUnitMaster ON;
INSERT INTO dbo.SaleUnitMaster (HospId, SaleUnitId, SaleUnitName, IsActive, CreatedBy, CreatedOn, LastModifiedBy, LastModifiedOn, IpAddress, UniqueId)
SELECT s.HospId, s.Id, s.Name, s.IsActive, s.CreatedBy, s.CreatedOn, s.LastModifiedBy, s.LastModifiedOn, s.IpAddress, s.UniqueId
FROM (VALUES
             (1,1,N'UNIT',1,1,'2024-03-21 11:45:59.377',NULL,NULL,N'203.81.240.132',N'3EF94F45-4F2C-4FF8-956A-3D54743F963C'),
             (1,2,N'UNIT/gm',1,1,'2024-05-16 17:06:10.660',NULL,NULL,NULL,N'4B9C38DB-FD97-48C3-B132-24F12A4F7BA4'),
             (1,3,N'pc',1,1,'2025-03-11 18:23:10.350',NULL,NULL,N'192.168.1.1',N'AAD8F2E8-BFFD-481B-A70D-416E2EC36F5F'),
             (1,4,N'ML',1,1,'2026-03-26 16:12:59.257',NULL,NULL,N'106.214.8.109',N'3310EA6A-DC1F-4F1B-9441-2877EB0ABEF5'),
             (1,5,N'pcs',1,1,'2026-07-18 14:09:22.513',NULL,NULL,N'49.36.168.53',N'91B87AF7-71D5-4754-852E-5A43132BC5D8')
     ) s (HospId, Id, Name, IsActive, CreatedBy, CreatedOn, LastModifiedBy, LastModifiedOn, IpAddress, UniqueId)
WHERE NOT EXISTS (SELECT 1 FROM dbo.SaleUnitMaster t WHERE t.SaleUnitId = s.Id);
SET IDENTITY_INSERT dbo.SaleUnitMaster OFF;
DECLARE @maxSU int = (SELECT ISNULL(MAX(SaleUnitId), 0) FROM dbo.SaleUnitMaster);
DBCC CHECKIDENT ('dbo.SaleUnitMaster', RESEED, @maxSU) WITH NO_INFOMSGS;
GO

CREATE OR ALTER PROCEDURE dbo.IU_SaleUnitMaster
(
    @hospId        int,
    @saleUnitId    int,
    @saleUnitName  nvarchar(256),
    @isActive      int = 1,
    @userId        int,
    @IpAddress     nvarchar(20),
    @Result        int = NULL OUTPUT
)
AS
BEGIN
    SET @saleUnitName = LTRIM(RTRIM(@saleUnitName));

    IF EXISTS (SELECT 1 FROM dbo.SaleUnitMaster WHERE LTRIM(RTRIM(SaleUnitName)) = @saleUnitName AND SaleUnitId <> @saleUnitId)
    BEGIN SET @Result = -1; RETURN; END

    IF (@saleUnitId = 0)
    BEGIN
        INSERT INTO dbo.SaleUnitMaster (HospId, SaleUnitName, IsActive, CreatedBy, IpAddress)
        VALUES (@hospId, @saleUnitName, @isActive, @userId, @IpAddress);
        SET @Result = SCOPE_IDENTITY();
        RETURN;
    END

    IF NOT EXISTS (SELECT 1 FROM dbo.SaleUnitMaster WHERE SaleUnitId = @saleUnitId)
    BEGIN SET @Result = 0; RETURN; END

    UPDATE dbo.SaleUnitMaster
    SET HospId = @hospId, SaleUnitName = @saleUnitName, IsActive = @isActive,
        LastModifiedBy = @userId, LastModifiedOn = GETDATE(), IpAddress = @IpAddress
    WHERE SaleUnitId = @saleUnitId;

    SET @Result = @saleUnitId;
END
GO

CREATE OR ALTER PROCEDURE dbo.S_GetSaleUnitList
AS
BEGIN
    SELECT  su.SaleUnitId,
            ISNULL(su.SaleUnitName, '') AS SaleUnitName,
            su.IsActive,
            (SELECT COUNT(1) FROM dbo.ServiceItemMaster sim WHERE sim.SaleUnitId = su.SaleUnitId) AS ItemCount,
            ISNULL((SELECT TOP 1 name FROM dbo.UserMaster umm WHERE umm.id = su.CreatedBy), '')      AS CreatedBy,
            ISNULL(FORMAT(su.CreatedOn, 'dd-MM-yyyy'), '')                                            AS CreatedOn,
            ISNULL((SELECT TOP 1 name FROM dbo.UserMaster umm WHERE umm.id = su.LastModifiedBy), '') AS LastModifiedBy,
            ISNULL(FORMAT(su.LastModifiedOn, 'dd-MM-yyyy'), '')                                       AS LastModifiedOn
    FROM dbo.SaleUnitMaster su
    ORDER BY su.SaleUnitName;
END
GO

/* =========================== 3. Dosage Form (design "Category") =========================== */
IF OBJECT_ID('dbo.DosageFormMaster', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.DosageFormMaster
    (
        HospId          int            NOT NULL,
        DosageFormId    int            IDENTITY(1,1) NOT NULL,
        DosageFormName  nvarchar(100)  NOT NULL,
        Description     nvarchar(256)  NULL,
        IsActive        int            NOT NULL CONSTRAINT DF_DosageFormMaster_IsActive  DEFAULT (1),
        CreatedBy       int            NULL,
        CreatedOn       datetime       NULL     CONSTRAINT DF_DosageFormMaster_CreatedOn DEFAULT (GETDATE()),
        LastModifiedBy  int            NULL,
        LastModifiedOn  datetime       NULL,
        IpAddress       nvarchar(20)   NULL,
        UniqueId        nvarchar(50)   NOT NULL CONSTRAINT DF_DosageFormMaster_UniqueId  DEFAULT (NEWID()),
        CONSTRAINT PK_DosageFormMaster PRIMARY KEY CLUSTERED (DosageFormId)
    );
END
GO

-- starter rows (the design's dosage forms); added only when missing by name
INSERT INTO dbo.DosageFormMaster (HospId, DosageFormName, IsActive, CreatedBy, IpAddress)
SELECT 1, s.Name, 1, 1, N'127.0.0.1'
FROM (VALUES (N'Tablet'), (N'Capsule'), (N'Syrup'), (N'Injection'), (N'Ointment / Cream'), (N'Drops')) s (Name)
WHERE NOT EXISTS (SELECT 1 FROM dbo.DosageFormMaster d WHERE d.DosageFormName = s.Name);
GO

CREATE OR ALTER PROCEDURE dbo.IU_DosageFormMaster
(
    @hospId          int,
    @dosageFormId    int,
    @dosageFormName  nvarchar(100),
    @description     nvarchar(256) = NULL,
    @isActive        int = 1,
    @userId          int,
    @IpAddress       nvarchar(20),
    @Result          int = NULL OUTPUT
)
AS
BEGIN
    SET @dosageFormName = LTRIM(RTRIM(@dosageFormName));
    SET @description    = NULLIF(LTRIM(RTRIM(@description)), '');

    IF EXISTS (SELECT 1 FROM dbo.DosageFormMaster WHERE DosageFormName = @dosageFormName AND DosageFormId <> @dosageFormId)
    BEGIN SET @Result = -1; RETURN; END

    IF (@dosageFormId = 0)
    BEGIN
        INSERT INTO dbo.DosageFormMaster (HospId, DosageFormName, Description, IsActive, CreatedBy, IpAddress)
        VALUES (@hospId, @dosageFormName, @description, @isActive, @userId, @IpAddress);
        SET @Result = SCOPE_IDENTITY();
        RETURN;
    END

    IF NOT EXISTS (SELECT 1 FROM dbo.DosageFormMaster WHERE DosageFormId = @dosageFormId)
    BEGIN SET @Result = 0; RETURN; END

    UPDATE dbo.DosageFormMaster
    SET HospId = @hospId, DosageFormName = @dosageFormName, Description = @description, IsActive = @isActive,
        LastModifiedBy = @userId, LastModifiedOn = GETDATE(), IpAddress = @IpAddress
    WHERE DosageFormId = @dosageFormId;

    SET @Result = @dosageFormId;
END
GO

CREATE OR ALTER PROCEDURE dbo.S_GetDosageFormList
AS
BEGIN
    SELECT  df.DosageFormId,
            df.DosageFormName,
            ISNULL(df.Description, '') AS Description,
            df.IsActive,
            ISNULL((SELECT TOP 1 name FROM dbo.UserMaster umm WHERE umm.id = df.CreatedBy), '')      AS CreatedBy,
            ISNULL(FORMAT(df.CreatedOn, 'dd-MM-yyyy'), '')                                            AS CreatedOn,
            ISNULL((SELECT TOP 1 name FROM dbo.UserMaster umm WHERE umm.id = df.LastModifiedBy), '') AS LastModifiedBy,
            ISNULL(FORMAT(df.LastModifiedOn, 'dd-MM-yyyy'), '')                                       AS LastModifiedOn
    FROM dbo.DosageFormMaster df
    ORDER BY df.DosageFormName;
END
GO
