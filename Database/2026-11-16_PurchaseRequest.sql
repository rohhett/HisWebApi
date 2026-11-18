/* =====================================================================================
   Purchase Request + Approve Purchase Request                          2026-11-16
   Database : GWT_HIS_API          Re-runnable (IF NOT EXISTS / CREATE OR ALTER)

   OLD reference: Store/PurchaseRequest + Store/ViewPurchaseRequest pages,
                  IU_PurchaseRequest, I_PurchaseRequestItemDetails, S_GetItemDetailsForPurchaseRequest,
                  S_GetItemDetailsByROL, S_ViewPurchaseRequestDetails, S_ViewPurchaseRequestItemDetails,
                  U_updatePurchaseRequestStatus, U_UpdatePurchaseRequestItemDetails, U_CancelPurchaseRequest,
                  S_ViewAllDepartmentCurrentStockDetailofItem.
   Approval   : NEW approval engine (same rules as U_ApproveOPDRefundRequest / write-off):
                ApprovalAuthorityMaster ApprovalTypeId 2 'Purchase Request', rule of the From Department
                role, else RoleId 0; Sequential / Parallel, several users per level (fn_IsUserInList).
                The approver is checked here, not only in the screen.
   Status     : 0 Draft, 2 Approval Pending, 3 Approved, 1 Cancelled (OLD StatusId values).
   Levels     : Min / Reorder / Max from ServiceItemMaster (NEW has no ReorderLevelMaster yet), sale units.
   Number     : NEW sequence type 9 'Purchase-Request' -> PR/26-27/000001.

   Contents
     1. Tables  PurchaseRequest, PurchaseRequestItemDetails
     2. Procedures
     3. Sequence for type 9
     4. Menu rows (tab Purchase) + role mapping (Admin 2, Pharmacy 5, Medical Store 15)

   AFTER THIS SCRIPT run 2026-11-16_PurchaseRequest_AmountWise.sql (amount-wise approval rule, rates and amounts).
   It replaces several objects created here; re-running only this file would bring back the old versions.
   ===================================================================================== */

/* ---------- 1. Tables ---------- */
IF OBJECT_ID('dbo.PurchaseRequest', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.PurchaseRequest
    (
        HospId              int             NOT NULL,
        PurchaseRequestId   int IDENTITY(1,1) NOT NULL CONSTRAINT PK_PurchaseRequest PRIMARY KEY,
        PurchaseRequestNo   nvarchar(50)    NULL,
        BranchId            int             NOT NULL,
        ToBranchId          int             NOT NULL,
        FromDeptId          int             NOT NULL,       -- role raising the request (the store that needs stock)
        ToDeptId            int             NOT NULL,       -- role that will purchase
        CategoryId          int             NOT NULL,       -- 6 Medical Store Items, 7 General Store Items
        Remarks             nvarchar(1000)  NULL,
        Status              nvarchar(50)    NOT NULL CONSTRAINT DF_PurchaseRequest_Status DEFAULT ('Draft'),
        StatusId            int             NOT NULL CONSTRAINT DF_PurchaseRequest_StatusId DEFAULT (0),
        IsCancel            int             NOT NULL CONSTRAINT DF_PurchaseRequest_IsCancel DEFAULT (0),
        ApprovalRemarks     nvarchar(500)   NULL,
        SentForApprovalBy   int             NULL,
        SentForApprovalOn   datetime        NULL,
        IsLevel1Approve     int             NULL,
        Level1ApproveId     int             NULL,
        Level1ApproveOn     datetime        NULL,
        IsLevel2Approve     int             NULL,
        Level2ApproveId     int             NULL,
        Level2ApproveOn     datetime        NULL,
        IsLevel3Approve     int             NULL,
        Level3ApproveId     int             NULL,
        Level3ApproveOn     datetime        NULL,
        IsLevel4Approve     int             NULL,
        Level4ApproveId     int             NULL,
        Level4ApproveOn     datetime        NULL,
        CancelBy            int             NULL,
        CancelOn            datetime        NULL,
        CancelReason        nvarchar(500)   NULL,
        CreatedBy           int             NOT NULL,
        CreatedOn           datetime        NOT NULL CONSTRAINT DF_PurchaseRequest_CreatedOn DEFAULT (GETDATE()),
        LastModifiedBy      int             NULL,
        LastModifiedOn      datetime        NULL,
        IpAddress           nvarchar(50)    NULL,
        UniqueId            nvarchar(50)    NULL
    );
    CREATE INDEX IX_PurchaseRequest_Branch_Created ON dbo.PurchaseRequest (HospId, BranchId, CreatedOn);
END
GO

IF OBJECT_ID('dbo.PurchaseRequestItemDetails', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.PurchaseRequestItemDetails
    (
        PurchaseRequestItemId int IDENTITY(1,1) NOT NULL CONSTRAINT PK_PurchaseRequestItemDetails PRIMARY KEY,
        PurchaseRequestId     int           NOT NULL CONSTRAINT FK_PurchaseRequestItemDetails_PR
                                                     REFERENCES dbo.PurchaseRequest (PurchaseRequestId),
        ItemId                int           NOT NULL,
        Packing               int           NOT NULL CONSTRAINT DF_PRItem_Packing DEFAULT (1),
        RequestQTY            int           NOT NULL,       -- purchase units (packs)
        ApprovedQTY           int           NOT NULL,       -- = RequestQTY until an approver changes it
        CreatedBy             int           NOT NULL,
        CreatedOn             datetime      NOT NULL CONSTRAINT DF_PRItem_CreatedOn DEFAULT (GETDATE()),
        LastModifiedBy        int           NULL,
        LastModifiedOn        datetime      NULL,
        IpAddress             nvarchar(50)  NULL
    );
    CREATE INDEX IX_PurchaseRequestItemDetails_PR ON dbo.PurchaseRequestItemDetails (PurchaseRequestId);
END
GO

/* ---------- 2. Procedures ---------- */

/* approval rule of a request: rule of the From Department role first, else RoleId 0 */
CREATE OR ALTER FUNCTION dbo.fn_PurchaseRequestApprovalRule (@purchaseRequestId int)
RETURNS TABLE
AS
RETURN
(
    SELECT TOP 1 AAM.ApprovalFlowId, AAM.ApprovalFlow, AAM.IsAllApprovalRequired, AAM.ApprovalLevelId, AAM.ApprovalLevel,
           AAM.Level1UserId, AAM.Level2UserId, AAM.Level3UserId, AAM.Level4UserId
    FROM dbo.PurchaseRequest PR
    INNER JOIN dbo.ApprovalAuthorityMaster AAM
            ON AAM.ApprovalTypeId = 2 AND AAM.IsActive = 1 AND AAM.HospId = PR.HospId
           AND AAM.RoleId IN (PR.FromDeptId, 0)
           AND AAM.BranchId IN (PR.BranchId, 0)
    WHERE PR.PurchaseRequestId = @purchaseRequestId
    ORDER BY CASE WHEN AAM.RoleId = PR.FromDeptId THEN 0 ELSE 1 END,
             CASE WHEN AAM.BranchId = PR.BranchId THEN 0 ELSE 1 END,
             AAM.Id DESC
);
GO

/* level the given user may approve now (0 = none) - same rules as U_ApproveOPDRefundRequest */
CREATE OR ALTER FUNCTION dbo.fn_PurchaseRequestApproveFlag (@purchaseRequestId int, @userId int)
RETURNS int
AS
BEGIN
    DECLARE @flag int = 0, @cur int = 0, @statusId int,
            @l1 int, @l2 int, @l3 int, @l4 int,
            @levels int, @flow int, @all int, @u1 nvarchar(512), @u2 nvarchar(512), @u3 nvarchar(512), @u4 nvarchar(512);

    SELECT @statusId = StatusId, @l1 = Level1ApproveId, @l2 = Level2ApproveId, @l3 = Level3ApproveId, @l4 = Level4ApproveId
    FROM dbo.PurchaseRequest WHERE PurchaseRequestId = @purchaseRequestId;
    IF @statusId IS NULL OR @statusId <> 2 RETURN 0;

    SELECT @levels = ApprovalLevelId, @flow = ApprovalFlowId, @all = IsAllApprovalRequired,
           @u1 = Level1UserId, @u2 = Level2UserId, @u3 = Level3UserId, @u4 = Level4UserId
    FROM dbo.fn_PurchaseRequestApprovalRule(@purchaseRequestId);
    IF @levels IS NULL RETURN 0;

    SET @cur = CASE WHEN @levels >= 1 AND @l1 IS NULL THEN 1
                    WHEN @levels >= 2 AND @l2 IS NULL THEN 2
                    WHEN @levels >= 3 AND @l3 IS NULL THEN 3
                    WHEN @levels >= 4 AND @l4 IS NULL THEN 4 ELSE 0 END;
    IF @cur = 0 RETURN 0;

    IF @flow = 1 OR (@flow = 2 AND @all = 1)
        SET @flag = CASE WHEN @cur = 1 AND dbo.fn_IsUserInList(@u1, @userId) = 1 THEN 1
                         WHEN @cur = 2 AND dbo.fn_IsUserInList(@u2, @userId) = 1 THEN 2
                         WHEN @cur = 3 AND dbo.fn_IsUserInList(@u3, @userId) = 1 THEN 3
                         WHEN @cur = 4 AND dbo.fn_IsUserInList(@u4, @userId) = 1 THEN 4 ELSE 0 END;
    ELSE IF @flow = 2 AND @all = 0
        SET @flag = CASE WHEN @levels >= 1 AND @l1 IS NULL AND dbo.fn_IsUserInList(@u1, @userId) = 1 THEN 1
                         WHEN @levels >= 2 AND @l2 IS NULL AND dbo.fn_IsUserInList(@u2, @userId) = 1 THEN 2
                         WHEN @levels >= 3 AND @l3 IS NULL AND dbo.fn_IsUserInList(@u3, @userId) = 1 THEN 3
                         WHEN @levels >= 4 AND @l4 IS NULL AND dbo.fn_IsUserInList(@u4, @userId) = 1 THEN 4 ELSE 0 END;
    RETURN @flag;
END
GO

/* To Department list (OLD getIndentDepartments): active roles of the hospital */
CREATE OR ALTER PROCEDURE dbo.S_GetPurchaseRequestDepartments
    @hospId int
AS
BEGIN
    SET NOCOUNT ON;
    SELECT RoleId, LTRIM(RTRIM(RoleName)) AS RoleName
    FROM dbo.RoleMaster
    WHERE HospId = @hospId AND IsActive = 1
    ORDER BY RoleName;
END
GO

/* items for the request grid (OLD S_GetItemDetailsForPurchaseRequest + item search):
   current stock of the From Department (posted, not expired) and the item's min / reorder / max levels */
CREATE OR ALTER PROCEDURE dbo.S_SearchItemForPurchaseRequest
    @hospId     int,
    @branchId   int,
    @fromDeptId int,
    @categoryId int,
    @search     nvarchar(100) = NULL,
    @itemIds    nvarchar(max) = NULL,     -- comma-separated ids: edit load / save check
    @belowRol   bit = 0,                  -- 1 = OLD "Get Item by ROL": only items whose stock is below the reorder level
    @top        int = 30
AS
BEGIN
    SET NOCOUNT ON;
    SET @search = NULLIF(LTRIM(RTRIM(@search)), '');

    SELECT TOP (@top)
        sim.ServiceItemId AS ItemId, sim.Name AS ItemName, ISNULL(sim.Code, '') AS ItemCode,
        ISNULL(NULLIF(sim.Packing, 0), 1) AS Packing, ISNULL(sim.PurchaseUnitName, '') AS PurchaseUnitName,
        ISNULL(sim.SaleUnitName, '') AS SaleUnitName,
        ISNULL(st.CurrentStock, 0) AS CurrentStock,
        lv.MinLevel, lv.ReorderLevel, lv.MaxLevel
    FROM dbo.ServiceItemMaster sim
    CROSS APPLY (SELECT ISNULL(TRY_CAST(sim.MinLevel AS int), 0) AS MinLevel,      -- levels are nvarchar in ServiceItemMaster
                        ISNULL(TRY_CAST(sim.ReorderLevel AS int), 0) AS ReorderLevel,
                        ISNULL(TRY_CAST(sim.MaxLevel AS int), 0) AS MaxLevel) lv
    OUTER APPLY (SELECT SUM(s.CurrentStock) AS CurrentStock
                 FROM dbo.Stock s
                 WHERE s.HospId = @hospId AND s.BranchId = @branchId AND s.RoleId = @fromDeptId
                   AND s.ItemId = sim.ServiceItemId AND s.IsActive = 1 AND s.CurrentStock > 0
                   AND CAST(s.ExpiryDate AS date) > CAST(GETDATE() AS date)) st
    WHERE sim.CategoryId = @categoryId AND sim.IsActive = 1
      AND (@itemIds IS NULL OR sim.ServiceItemId IN (SELECT TRY_CAST(value AS int) FROM STRING_SPLIT(@itemIds, ',')))
      AND (@search IS NULL OR sim.Name LIKE '%' + @search + '%' OR sim.Code LIKE @search + '%'
           OR CAST(sim.ServiceItemId AS nvarchar(20)) = @search)
      AND (@belowRol = 0 OR (lv.ReorderLevel > 0 AND lv.ReorderLevel > ISNULL(st.CurrentStock, 0)))
    ORDER BY CASE WHEN @search IS NOT NULL AND sim.Name LIKE @search + '%' THEN 0 ELSE 1 END, sim.Name;
END
GO

/* save a new request or update a Draft (OLD IU_PurchaseRequest + D_/I_PurchaseRequestItemDetails)
   @itemsJson = [{"ItemId":1,"Packing":10,"Qty":5}, ...]
   -2 not Draft, -3 no PR number sequence */
CREATE OR ALTER PROCEDURE dbo.IU_PurchaseRequest
    @hospId            int,
    @branchId          int,
    @purchaseRequestId int = 0,
    @fromDeptId        int,
    @toDeptId          int,
    @categoryId        int,
    @remarks           nvarchar(1000) = NULL,
    @itemsJson         nvarchar(max),
    @userId            int,
    @ipAddress         nvarchar(50) = NULL,
    @Result            int = NULL OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @prNo nvarchar(100);

    IF ISNULL(@purchaseRequestId, 0) = 0
    BEGIN
        EXEC dbo.getBranchSequenceNumber @branchId, @fromDeptId, 9, @prNo OUTPUT;
        IF NULLIF(LTRIM(RTRIM(@prNo)), '') IS NULL
        BEGIN
            SET @Result = -3; SELECT @Result; RETURN;
        END

        INSERT INTO dbo.PurchaseRequest
            (HospId, PurchaseRequestNo, BranchId, ToBranchId, FromDeptId, ToDeptId, CategoryId, Remarks,
             Status, StatusId, IsCancel, CreatedBy, CreatedOn, IpAddress, UniqueId)
        VALUES
            (@hospId, @prNo, @branchId, @branchId, @fromDeptId, @toDeptId, @categoryId, @remarks,
             'Draft', 0, 0, @userId, GETDATE(), @ipAddress, CONVERT(nvarchar(50), NEWID()));
        SET @Result = SCOPE_IDENTITY();
    END
    ELSE
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM dbo.PurchaseRequest
                       WHERE PurchaseRequestId = @purchaseRequestId AND HospId = @hospId AND StatusId = 0 AND IsCancel = 0)
        BEGIN
            SET @Result = -2; SELECT @Result; RETURN;
        END

        /* OLD: only remarks change on the header; department and store type stay as created */
        UPDATE dbo.PurchaseRequest
        SET Remarks = @remarks, ToDeptId = @toDeptId, LastModifiedBy = @userId, LastModifiedOn = GETDATE(), IpAddress = @ipAddress
        WHERE PurchaseRequestId = @purchaseRequestId;

        DELETE FROM dbo.PurchaseRequestItemDetails WHERE PurchaseRequestId = @purchaseRequestId;
        SET @Result = @purchaseRequestId;
    END

    INSERT INTO dbo.PurchaseRequestItemDetails (PurchaseRequestId, ItemId, Packing, RequestQTY, ApprovedQTY, CreatedBy, CreatedOn, IpAddress)
    SELECT @Result, j.ItemId, j.Packing, j.Qty, j.Qty, @userId, GETDATE(), @ipAddress
    FROM OPENJSON(@itemsJson) WITH (ItemId int '$.ItemId', Packing int '$.Packing', Qty int '$.Qty') j;

    SELECT @Result;
END
GO

/* Draft -> Approval Pending (OLD flag 0). -2 not Draft, -5 no approval rule */
CREATE OR ALTER PROCEDURE dbo.U_SendPurchaseRequestForApproval
    @hospId int, @purchaseRequestId int, @userId int, @ipAddress nvarchar(50) = NULL,
    @Result int = NULL OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    IF NOT EXISTS (SELECT 1 FROM dbo.PurchaseRequest
                   WHERE PurchaseRequestId = @purchaseRequestId AND HospId = @hospId AND StatusId = 0 AND IsCancel = 0)
    BEGIN
        SET @Result = -2; SELECT @Result; RETURN;
    END
    IF NOT EXISTS (SELECT 1 FROM dbo.fn_PurchaseRequestApprovalRule(@purchaseRequestId))
    BEGIN
        SET @Result = -5; SELECT @Result; RETURN;
    END

    UPDATE dbo.PurchaseRequest
    SET Status = 'Approval Pending', StatusId = 2, SentForApprovalBy = @userId, SentForApprovalOn = GETDATE(),
        LastModifiedBy = @userId, LastModifiedOn = GETDATE(), IpAddress = @ipAddress
    WHERE PurchaseRequestId = @purchaseRequestId;
    SET @Result = @purchaseRequestId;
    SELECT @Result;
END
GO

/* approve the current level (OLD U_updatePurchaseRequestStatus flag 1-4 + U_UpdatePurchaseRequestItemDetails)
   @itemsJson = [{"PurchaseRequestItemId":1,"ApprovedQty":4}, ...]  (approved qty 0..requested)
   -1 user may not approve this level now, -2 not pending, -6 approved qty out of range */
CREATE OR ALTER PROCEDURE dbo.U_ApprovePurchaseRequest
    @hospId int, @purchaseRequestId int, @flag int, @approvalRemarks nvarchar(500) = NULL,
    @itemsJson nvarchar(max) = NULL, @userId int, @ipAddress nvarchar(50) = NULL,
    @Result int = NULL OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @levels int, @flow int, @all int;

    IF NOT EXISTS (SELECT 1 FROM dbo.PurchaseRequest
                   WHERE PurchaseRequestId = @purchaseRequestId AND HospId = @hospId AND StatusId = 2 AND IsCancel = 0)
    BEGIN
        SET @Result = -2; SELECT @Result; RETURN;
    END
    IF @flag NOT BETWEEN 1 AND 4 OR dbo.fn_PurchaseRequestApproveFlag(@purchaseRequestId, @userId) <> @flag
    BEGIN
        SET @Result = -1; SELECT @Result; RETURN;
    END

    IF NULLIF(@itemsJson, '') IS NOT NULL
    BEGIN
        IF EXISTS (SELECT 1 FROM OPENJSON(@itemsJson) WITH (PurchaseRequestItemId int '$.PurchaseRequestItemId', ApprovedQty int '$.ApprovedQty') j
                   LEFT JOIN dbo.PurchaseRequestItemDetails d
                          ON d.PurchaseRequestItemId = j.PurchaseRequestItemId AND d.PurchaseRequestId = @purchaseRequestId
                   WHERE d.PurchaseRequestItemId IS NULL OR j.ApprovedQty IS NULL OR j.ApprovedQty < 0 OR j.ApprovedQty > d.RequestQTY)
        BEGIN
            SET @Result = -6; SELECT @Result; RETURN;
        END

        UPDATE d
        SET ApprovedQTY = j.ApprovedQty, LastModifiedBy = @userId, LastModifiedOn = GETDATE(), IpAddress = @ipAddress
        FROM dbo.PurchaseRequestItemDetails d
        INNER JOIN OPENJSON(@itemsJson) WITH (PurchaseRequestItemId int '$.PurchaseRequestItemId', ApprovedQty int '$.ApprovedQty') j
                ON j.PurchaseRequestItemId = d.PurchaseRequestItemId
        WHERE d.PurchaseRequestId = @purchaseRequestId;
    END

    UPDATE dbo.PurchaseRequest
    SET IsLevel1Approve = CASE WHEN @flag = 1 THEN 1 ELSE IsLevel1Approve END,
        Level1ApproveId = CASE WHEN @flag = 1 THEN @userId ELSE Level1ApproveId END,
        Level1ApproveOn = CASE WHEN @flag = 1 THEN GETDATE() ELSE Level1ApproveOn END,
        IsLevel2Approve = CASE WHEN @flag = 2 THEN 1 ELSE IsLevel2Approve END,
        Level2ApproveId = CASE WHEN @flag = 2 THEN @userId ELSE Level2ApproveId END,
        Level2ApproveOn = CASE WHEN @flag = 2 THEN GETDATE() ELSE Level2ApproveOn END,
        IsLevel3Approve = CASE WHEN @flag = 3 THEN 1 ELSE IsLevel3Approve END,
        Level3ApproveId = CASE WHEN @flag = 3 THEN @userId ELSE Level3ApproveId END,
        Level3ApproveOn = CASE WHEN @flag = 3 THEN GETDATE() ELSE Level3ApproveOn END,
        IsLevel4Approve = CASE WHEN @flag = 4 THEN 1 ELSE IsLevel4Approve END,
        Level4ApproveId = CASE WHEN @flag = 4 THEN @userId ELSE Level4ApproveId END,
        Level4ApproveOn = CASE WHEN @flag = 4 THEN GETDATE() ELSE Level4ApproveOn END,
        ApprovalRemarks = @approvalRemarks,
        LastModifiedBy = @userId, LastModifiedOn = GETDATE(), IpAddress = @ipAddress
    WHERE PurchaseRequestId = @purchaseRequestId;

    /* fully approved: last level done, or parallel flow where any one approval is enough */
    SELECT @levels = ApprovalLevelId, @flow = ApprovalFlowId, @all = IsAllApprovalRequired
    FROM dbo.fn_PurchaseRequestApprovalRule(@purchaseRequestId);

    IF (@flow = 2 AND @all = 0)
       OR NOT EXISTS (SELECT 1 FROM dbo.PurchaseRequest
                      WHERE PurchaseRequestId = @purchaseRequestId
                        AND ((@levels >= 1 AND Level1ApproveId IS NULL) OR (@levels >= 2 AND Level2ApproveId IS NULL)
                          OR (@levels >= 3 AND Level3ApproveId IS NULL) OR (@levels >= 4 AND Level4ApproveId IS NULL)))
        UPDATE dbo.PurchaseRequest SET Status = 'Approved', StatusId = 3 WHERE PurchaseRequestId = @purchaseRequestId;

    SET @Result = @purchaseRequestId;
    SELECT @Result;
END
GO

/* cancel a Draft or Approval Pending request (OLD U_CancelPurchaseRequest). -2 not Draft / Pending */
CREATE OR ALTER PROCEDURE dbo.U_CancelPurchaseRequest
    @hospId int, @purchaseRequestId int, @reason nvarchar(500), @userId int, @ipAddress nvarchar(50) = NULL,
    @Result int = NULL OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    IF NOT EXISTS (SELECT 1 FROM dbo.PurchaseRequest
                   WHERE PurchaseRequestId = @purchaseRequestId AND HospId = @hospId AND StatusId IN (0, 2) AND IsCancel = 0)
    BEGIN
        SET @Result = -2; SELECT @Result; RETURN;
    END

    UPDATE dbo.PurchaseRequest
    SET IsCancel = 1, Status = 'Cancelled', StatusId = 1, CancelBy = @userId, CancelReason = @reason, CancelOn = GETDATE(),
        LastModifiedBy = @userId, LastModifiedOn = GETDATE(), IpAddress = @ipAddress
    WHERE PurchaseRequestId = @purchaseRequestId;
    SET @Result = @purchaseRequestId;
    SELECT @Result;
END
GO

/* list for both pages (OLD S_ViewPurchaseRequestDetails, parameterised): FlagId / CanApprove for the user */
CREATE OR ALTER PROCEDURE dbo.S_GetPurchaseRequestList
    @hospId     int,
    @branchId   int,
    @userId     int,
    @fromDate   date,
    @toDate     date,
    @fromDeptId int = NULL,
    @toDeptId   int = NULL,
    @categoryId int = NULL,
    @statusId   int = NULL,
    @prNo       nvarchar(50) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET @prNo = NULLIF(LTRIM(RTRIM(@prNo)), '');

    SELECT
        PR.PurchaseRequestId, PR.PurchaseRequestNo, PR.BranchId, PR.FromDeptId, PR.ToDeptId, PR.CategoryId,
        LTRIM(RTRIM(fr.RoleName)) AS FromDeptName, LTRIM(RTRIM(tr.RoleName)) AS ToDeptName,
        CASE PR.CategoryId WHEN 6 THEN 'Medical' ELSE 'General' END AS StoreType,
        FORMAT(PR.CreatedOn, 'dd-MM-yyyy') AS PRDate,
        PR.Remarks, PR.StatusId, PR.IsCancel,
        CASE WHEN PR.StatusId = 2 AND LV.CurLevel > 0 THEN 'Pending Level ' + CAST(LV.CurLevel AS varchar(1)) + ' Approval'
             WHEN PR.StatusId = 2 AND AM.ApprovalLevelId IS NULL THEN 'Approval Authority Not Found'
             ELSE PR.Status END AS Status,
        CASE WHEN PR.StatusId = 2 AND LV.CurLevel = 1 THEN dbo.fn_GetUserNamesFromCSV(AM.Level1UserId)
             WHEN PR.StatusId = 2 AND LV.CurLevel = 2 THEN dbo.fn_GetUserNamesFromCSV(AM.Level2UserId)
             WHEN PR.StatusId = 2 AND LV.CurLevel = 3 THEN dbo.fn_GetUserNamesFromCSV(AM.Level3UserId)
             WHEN PR.StatusId = 2 AND LV.CurLevel = 4 THEN dbo.fn_GetUserNamesFromCSV(AM.Level4UserId)
             ELSE '' END AS CurrentApprover,
        ISNULL(it.ItemCount, 0) AS ItemCount, ISNULL(it.RequestedQty, 0) AS RequestedQty, ISNULL(it.ApprovedQty, 0) AS ApprovedQty,
        ISNULL(PR.ApprovalRemarks, '') AS ApprovalRemarks,
        PR.IsLevel1Approve, FORMAT(PR.Level1ApproveOn, 'dd-MM-yyyy hh:mm tt') AS Level1ApproveOn,
        PR.IsLevel2Approve, FORMAT(PR.Level2ApproveOn, 'dd-MM-yyyy hh:mm tt') AS Level2ApproveOn,
        PR.IsLevel3Approve, FORMAT(PR.Level3ApproveOn, 'dd-MM-yyyy hh:mm tt') AS Level3ApproveOn,
        PR.IsLevel4Approve, FORMAT(PR.Level4ApproveOn, 'dd-MM-yyyy hh:mm tt') AS Level4ApproveOn,
        (SELECT TOP 1 name FROM dbo.UserMaster WHERE id = PR.CancelBy) AS CancelBy,
        FORMAT(PR.CancelOn, 'dd-MM-yyyy hh:mm tt') AS CancelOn, PR.CancelReason,
        (SELECT TOP 1 name FROM dbo.UserMaster WHERE id = PR.CreatedBy) AS CreatedBy,
        FORMAT(PR.CreatedOn, 'dd-MM-yyyy hh:mm tt') AS CreatedOn,
        dbo.fn_PurchaseRequestApproveFlag(PR.PurchaseRequestId, @userId) AS FlagId,
        CASE WHEN dbo.fn_PurchaseRequestApproveFlag(PR.PurchaseRequestId, @userId) > 0 THEN 1 ELSE 0 END AS CanApprove
    FROM dbo.PurchaseRequest PR
    LEFT JOIN dbo.RoleMaster fr ON fr.RoleId = PR.FromDeptId
    LEFT JOIN dbo.RoleMaster tr ON tr.RoleId = PR.ToDeptId
    OUTER APPLY dbo.fn_PurchaseRequestApprovalRule(PR.PurchaseRequestId) AM
    OUTER APPLY (SELECT CurLevel = CASE WHEN AM.ApprovalLevelId >= 1 AND PR.Level1ApproveId IS NULL THEN 1
                                        WHEN AM.ApprovalLevelId >= 2 AND PR.Level2ApproveId IS NULL THEN 2
                                        WHEN AM.ApprovalLevelId >= 3 AND PR.Level3ApproveId IS NULL THEN 3
                                        WHEN AM.ApprovalLevelId >= 4 AND PR.Level4ApproveId IS NULL THEN 4 ELSE 0 END) LV
    OUTER APPLY (SELECT COUNT(1) AS ItemCount, SUM(d.RequestQTY) AS RequestedQty, SUM(d.ApprovedQTY) AS ApprovedQty
                 FROM dbo.PurchaseRequestItemDetails d WHERE d.PurchaseRequestId = PR.PurchaseRequestId) it
    WHERE PR.HospId = @hospId AND PR.BranchId = @branchId
      AND PR.CreatedOn >= @fromDate AND PR.CreatedOn < DATEADD(DAY, 1, @toDate)
      AND (@fromDeptId IS NULL OR PR.FromDeptId = @fromDeptId)
      AND (@toDeptId IS NULL OR PR.ToDeptId = @toDeptId)
      AND (@categoryId IS NULL OR PR.CategoryId = @categoryId)
      AND (@statusId IS NULL OR PR.StatusId = @statusId)
      AND (@prNo IS NULL OR PR.PurchaseRequestNo LIKE '%' + @prNo + '%')
    ORDER BY PR.CreatedOn DESC;
END
GO

/* one request: header + approval details (set 1), items with stock (set 2)
   OLD S_GetPurchaseRequestItemDetailsForEdit + S_ViewPurchaseRequestItemDetails */
CREATE OR ALTER PROCEDURE dbo.S_GetPurchaseRequestDetails
    @hospId int, @purchaseRequestId int, @userId int
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        PR.PurchaseRequestId, PR.PurchaseRequestNo, PR.BranchId, PR.FromDeptId, PR.ToDeptId, PR.CategoryId,
        LTRIM(RTRIM(fr.RoleName)) AS FromDeptName, LTRIM(RTRIM(tr.RoleName)) AS ToDeptName,
        CASE PR.CategoryId WHEN 6 THEN 'Medical' ELSE 'General' END AS StoreType,
        FORMAT(PR.CreatedOn, 'dd-MM-yyyy hh:mm tt') AS CreatedOn,
        (SELECT TOP 1 name FROM dbo.UserMaster WHERE id = PR.CreatedBy) AS CreatedBy,
        PR.Remarks, PR.Status, PR.StatusId, PR.IsCancel, ISNULL(PR.ApprovalRemarks, '') AS ApprovalRemarks,
        AM.ApprovalFlowId, AM.ApprovalFlow, AM.IsAllApprovalRequired, AM.ApprovalLevelId, AM.ApprovalLevel,
        dbo.fn_GetUserNamesFromCSV(AM.Level1UserId) AS Level1ApproverNames,
        dbo.fn_GetUserNamesFromCSV(AM.Level2UserId) AS Level2ApproverNames,
        dbo.fn_GetUserNamesFromCSV(AM.Level3UserId) AS Level3ApproverNames,
        dbo.fn_GetUserNamesFromCSV(AM.Level4UserId) AS Level4ApproverNames,
        PR.IsLevel1Approve, (SELECT TOP 1 name FROM dbo.UserMaster WHERE id = PR.Level1ApproveId) AS Level1ApprovedByName,
        FORMAT(PR.Level1ApproveOn, 'dd-MM-yyyy hh:mm tt') AS Level1ApproveOn,
        PR.IsLevel2Approve, (SELECT TOP 1 name FROM dbo.UserMaster WHERE id = PR.Level2ApproveId) AS Level2ApprovedByName,
        FORMAT(PR.Level2ApproveOn, 'dd-MM-yyyy hh:mm tt') AS Level2ApproveOn,
        PR.IsLevel3Approve, (SELECT TOP 1 name FROM dbo.UserMaster WHERE id = PR.Level3ApproveId) AS Level3ApprovedByName,
        FORMAT(PR.Level3ApproveOn, 'dd-MM-yyyy hh:mm tt') AS Level3ApproveOn,
        PR.IsLevel4Approve, (SELECT TOP 1 name FROM dbo.UserMaster WHERE id = PR.Level4ApproveId) AS Level4ApprovedByName,
        FORMAT(PR.Level4ApproveOn, 'dd-MM-yyyy hh:mm tt') AS Level4ApproveOn,
        (SELECT TOP 1 name FROM dbo.UserMaster WHERE id = PR.CancelBy) AS CancelBy,
        FORMAT(PR.CancelOn, 'dd-MM-yyyy hh:mm tt') AS CancelOn, PR.CancelReason,
        dbo.fn_PurchaseRequestApproveFlag(PR.PurchaseRequestId, @userId) AS FlagId
    FROM dbo.PurchaseRequest PR
    LEFT JOIN dbo.RoleMaster fr ON fr.RoleId = PR.FromDeptId
    LEFT JOIN dbo.RoleMaster tr ON tr.RoleId = PR.ToDeptId
    OUTER APPLY dbo.fn_PurchaseRequestApprovalRule(PR.PurchaseRequestId) AM
    WHERE PR.PurchaseRequestId = @purchaseRequestId AND PR.HospId = @hospId;

    SELECT
        d.PurchaseRequestItemId, d.ItemId, sim.Name AS ItemName, d.Packing, ISNULL(sim.PurchaseUnitName, '') AS PurchaseUnitName,
        d.RequestQTY AS RequestedQty, d.ApprovedQTY AS ApprovedQty,
        ISNULL(sf.Qty, 0) AS CurrentStock,            -- From Department (OLD StQty)
        ISNULL(sa.Qty, 0) AS CurrentStockAllDept,     -- whole branch (OLD StQtyAll)
        ISNULL(TRY_CAST(sim.MinLevel AS int), 0) AS MinLevel, ISNULL(TRY_CAST(sim.ReorderLevel AS int), 0) AS ReorderLevel,
        ISNULL(TRY_CAST(sim.MaxLevel AS int), 0) AS MaxLevel
    FROM dbo.PurchaseRequestItemDetails d
    INNER JOIN dbo.PurchaseRequest PR ON PR.PurchaseRequestId = d.PurchaseRequestId
    INNER JOIN dbo.ServiceItemMaster sim ON sim.ServiceItemId = d.ItemId
    OUTER APPLY (SELECT SUM(s.CurrentStock) AS Qty FROM dbo.Stock s
                 WHERE s.HospId = PR.HospId AND s.BranchId = PR.BranchId AND s.RoleId = PR.FromDeptId AND s.ItemId = d.ItemId
                   AND s.IsActive = 1 AND s.CurrentStock > 0 AND CAST(s.ExpiryDate AS date) > CAST(GETDATE() AS date)) sf
    OUTER APPLY (SELECT SUM(s.CurrentStock) AS Qty FROM dbo.Stock s
                 WHERE s.HospId = PR.HospId AND s.BranchId = PR.BranchId AND s.ItemId = d.ItemId
                   AND s.IsActive = 1 AND s.CurrentStock > 0 AND CAST(s.ExpiryDate AS date) > CAST(GETDATE() AS date)) sa
    WHERE d.PurchaseRequestId = @purchaseRequestId AND PR.HospId = @hospId
    ORDER BY d.PurchaseRequestItemId;
END
GO

/* current stock of one item in every department of the branch (OLD S_ViewAllDepartmentCurrentStockDetailofItem) */
CREATE OR ALTER PROCEDURE dbo.S_GetItemStockByDepartment
    @hospId int, @branchId int, @itemId int
AS
BEGIN
    SET NOCOUNT ON;
    SELECT s.RoleId, LTRIM(RTRIM(rm.RoleName)) AS RoleName, SUM(s.CurrentStock) AS CurrentStock
    FROM dbo.Stock s
    INNER JOIN dbo.RoleMaster rm ON rm.RoleId = s.RoleId
    WHERE s.HospId = @hospId AND s.BranchId = @branchId AND s.ItemId = @itemId
      AND s.IsActive = 1 AND s.CurrentStock > 0 AND CAST(s.ExpiryDate AS date) > CAST(GETDATE() AS date)
    GROUP BY s.RoleId, rm.RoleName
    ORDER BY RoleName;
END
GO

/* ---------- 3. PR number sequence (type 9, all branches, all roles) ---------- */
IF NOT EXISTS (SELECT 1 FROM dbo.BranchSequenceMapping WHERE TypeId = 9)
BEGIN
    DECLARE @sid int = (SELECT TOP 1 SequenceId FROM dbo.SequenceMaster WHERE TypeId = 9 AND Name = 'PR');
    IF @sid IS NULL
    BEGIN
        INSERT INTO dbo.SequenceMaster (HospId, Name, TypeId, TypeName, Prefix, FirstSeprator, FYFormatId, FYFormat,
                                        SecondSeprator, Length, Preview, IsActive, CreatedBy, CreatedOn, IpAddress, UniqueId)
        VALUES (1, 'PR', 9, 'Purchase-Request', 'PR', '/', 1, '2', '/', 6, 'PR/26-27/000001', 1, 1, GETDATE(), '127.0.0.1', CONVERT(nvarchar(50), NEWID()));
        SET @sid = SCOPE_IDENTITY();
    END
    INSERT INTO dbo.BranchSequenceMapping (BranchId, RoleId, TypeId, SequenceId, CreatedBy, CreatedOn, IpAddress, UniqueId)
    VALUES (0, 0, 9, @sid, 1, GETDATE(), '127.0.0.1', CONVERT(nvarchar(50), NEWID()));
END
GO

/* ---------- 4. Menu rows under tab 'Purchase' + role mapping ---------- */
DECLARE @tabId int = (SELECT TOP 1 TabId FROM dbo.NavigationTabMaster WHERE LTRIM(RTRIM(TabName)) = 'Purchase' AND IsActive = 1);
IF @tabId IS NULL
    PRINT 'Tab "Purchase" not found - add "Purchase Request" (purchase-request) and "Approve Purchase Request" (purchase-request-approval) from the Navigation Pane screen.';
ELSE
BEGIN
    DECLARE @menu TABLE (Url nvarchar(200), Name nvarchar(200), Seq int);
    INSERT INTO @menu VALUES ('purchase-request', 'Purchase Request', 1), ('purchase-request-approval', 'Approve Purchase Request', 2);

    UPDATE s SET TabId = @tabId, SubMenuName = m.Name, IsActive = 1
    FROM dbo.NavigationSubMenuMaster s INNER JOIN @menu m ON m.Url = s.URL;

    INSERT INTO dbo.NavigationSubMenuMaster (TabId, SubMenuName, HospId, IsActive, URL, CreatedOn, CreatedBy, IpAddress, SequenceNo)
    SELECT @tabId, m.Name, 1, 1, m.Url, GETDATE(), 1, '127.0.0.1', m.Seq
    FROM @menu m
    WHERE NOT EXISTS (SELECT 1 FROM dbo.NavigationSubMenuMaster s WHERE s.URL = m.Url);

    INSERT INTO dbo.RoleWiseMenuMappingMaster (HospId, BranchId, RoleId, SubMenuId, CreatedBy, CreatedOn, IpAddress)
    SELECT b.HospId, b.BranchId, r.RoleId, s.SubMenuId, 1, GETDATE(), '127.0.0.1'
    FROM dbo.BranchMaster b
    CROSS JOIN (VALUES (2), (5), (15)) r(RoleId)
    CROSS JOIN (SELECT SubMenuId FROM dbo.NavigationSubMenuMaster WHERE URL IN ('purchase-request', 'purchase-request-approval')) s
    WHERE b.IsActive = 1
      AND EXISTS (SELECT 1 FROM dbo.RoleMaster rm WHERE rm.RoleId = r.RoleId)
      AND NOT EXISTS (SELECT 1 FROM dbo.RoleWiseMenuMappingMaster x
                      WHERE x.BranchId = b.BranchId AND x.RoleId = r.RoleId AND x.SubMenuId = s.SubMenuId);
END
GO
