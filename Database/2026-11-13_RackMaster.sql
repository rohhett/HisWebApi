/* =====================================================================
   Rack Master (store / pharmacy racks)  —  2026-11-13
   Target DB : GWT_HIS_API (NEW HIS)
   Re-runnable: yes (IF NOT EXISTS / CREATE OR ALTER)

   Design-only master (MediStock design "Rack (R)": Rack Code*, Location*, Status). OLD HIS has no store rack master
   (OLD/NEW MRDRackMaster is the medical-records file rack - not related). No OLD data to copy.
   Planned in Store_Item_Master_Build_Spec 6.x: RackMaster (rack code, location) + ServiceItemMaster.RackId (not added here).

   Objects
     1. TABLE      dbo.RackMaster                (new)
     2. PROCEDURE  dbo.IU_RackMaster             (new)  @Result >0 = RackId saved, -1 = rack code already exists, 0 = RackId not found
     3. PROCEDURE  dbo.S_GetRackList             (new; all rows + audit columns, for Redis cache)
     4. MENU ROW   dbo.NavigationSubMenuMaster   ('Rack Master' / rack-master under tab 'Purchase')

   After running: assign the page to users/roles in User Authorization.
   ===================================================================== */

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

/* ---------- 1. Table ---------- */
IF OBJECT_ID('dbo.RackMaster', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.RackMaster
    (
        HospId          int            NOT NULL,
        RackId          int            IDENTITY(1,1) NOT NULL,
        RackCode        nvarchar(20)   NOT NULL,
        Location        nvarchar(100)  NOT NULL,
        IsActive        int            NOT NULL CONSTRAINT DF_RackMaster_IsActive  DEFAULT (1),
        CreatedBy       int            NULL,
        CreatedOn       datetime       NULL     CONSTRAINT DF_RackMaster_CreatedOn DEFAULT (GETDATE()),
        LastModifiedBy  int            NULL,
        LastModifiedOn  datetime       NULL,
        IpAddress       nvarchar(20)   NULL,
        UniqueId        nvarchar(50)   NOT NULL CONSTRAINT DF_RackMaster_UniqueId  DEFAULT (NEWID()),
        CONSTRAINT PK_RackMaster PRIMARY KEY CLUSTERED (RackId)
    );
END
GO

/* ---------- 2. Insert / Update ---------- */
CREATE OR ALTER PROCEDURE dbo.IU_RackMaster
(
    @hospId     int,
    @rackId     int,
    @rackCode   nvarchar(20),
    @location   nvarchar(100),
    @isActive   int = 1,
    @userId     int,
    @IpAddress  nvarchar(20),
    @Result     int = NULL OUTPUT
)
AS
BEGIN
    SET @rackCode = UPPER(LTRIM(RTRIM(@rackCode)));
    SET @location = LTRIM(RTRIM(@location));

    -- rack code unique across all rows (case-insensitive)
    IF EXISTS (SELECT 1 FROM dbo.RackMaster WHERE RackCode = @rackCode AND RackId <> @rackId)
    BEGIN
        SET @Result = -1;
        RETURN;
    END

    IF (@rackId = 0)
    BEGIN
        INSERT INTO dbo.RackMaster (HospId, RackCode, Location, IsActive, CreatedBy, IpAddress)
        VALUES (@hospId, @rackCode, @location, @isActive, @userId, @IpAddress);

        SET @Result = SCOPE_IDENTITY();
        RETURN;
    END

    IF NOT EXISTS (SELECT 1 FROM dbo.RackMaster WHERE RackId = @rackId)
    BEGIN
        SET @Result = 0;   -- record not found
        RETURN;
    END

    UPDATE dbo.RackMaster
    SET    HospId         = @hospId,
           RackCode       = @rackCode,
           Location       = @location,
           IsActive       = @isActive,
           LastModifiedBy = @userId,
           LastModifiedOn = GETDATE(),
           IpAddress      = @IpAddress
    WHERE  RackId         = @rackId;

    SET @Result = @rackId;
END
GO

/* ---------- 3. List (all rows; API caches and filters) ---------- */
CREATE OR ALTER PROCEDURE dbo.S_GetRackList
AS
BEGIN
    SELECT  rm.RackId,
            rm.RackCode,
            rm.Location,
            rm.IsActive,
            ISNULL((SELECT TOP 1 name FROM dbo.UserMaster umm WHERE umm.id = rm.CreatedBy), '')      AS CreatedBy,
            ISNULL(FORMAT(rm.CreatedOn, 'dd-MM-yyyy'), '')                                            AS CreatedOn,
            ISNULL((SELECT TOP 1 name FROM dbo.UserMaster umm WHERE umm.id = rm.LastModifiedBy), '') AS LastModifiedBy,
            ISNULL(FORMAT(rm.LastModifiedOn, 'dd-MM-yyyy'), '')                                       AS LastModifiedOn
    FROM    dbo.RackMaster rm
    ORDER BY rm.RackCode;
END
GO

/* ---------- 4. Menu row (tab 'Purchase') ---------- */
IF NOT EXISTS (SELECT 1 FROM dbo.NavigationSubMenuMaster WHERE URL = 'rack-master')
BEGIN
    DECLARE @tabId int = (SELECT TOP 1 TabId FROM dbo.NavigationTabMaster
                          WHERE LTRIM(RTRIM(TabName)) = 'Purchase' AND IsActive = 1);
    IF @tabId IS NULL
        PRINT 'Tab "Purchase" not found - add "Rack Master" (rack-master) from the Navigation Pane screen.';
    ELSE
        INSERT INTO dbo.NavigationSubMenuMaster (TabId, SubMenuName, HospId, IsActive, URL, CreatedOn, CreatedBy, IpAddress, SequenceNo)
        VALUES (@tabId, 'Rack Master', 1, 1, 'rack-master', GETDATE(), 1, '127.0.0.1', 84);
END
GO
