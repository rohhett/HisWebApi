/* =====================================================================================
   Purchase Request - amount-wise approval                              2026-11-16
   Database : GWT_HIS_API      Run AFTER 2026-11-16_PurchaseRequest.sql. Re-runnable.

   Rule (same as write-off, ApprovalTypeId 8):
     the active Approval Authority row of ApprovalTypeId 2 with the smallest AmountUpTo >= request amount.
     Among rows that cover the amount: From Department role before RoleId 0, own branch before 0.
     No row covers the amount -> request cannot be sent (-5).
   Amount :
     Rate   = last landed cost of the item in the branch, per purchase unit
              (Stock.UnitCost of the latest active GRN batch x PR packing; discount, free qty and GST included).
              Item never received -> Rate 0.
     Amount = RequestQTY x Rate;  PurchaseRequest.TotalAmount = sum of item amounts.
     ApprovalAmount = TotalAmount when the request is sent; the rule stays fixed by it even if an
     approver lowers quantities. ApprovedAmount = sum(ApprovedQTY x Rate).

   Contents
     1. Columns  PurchaseRequest.TotalAmount / ApprovedAmount / ApprovalAmount,
                 PurchaseRequestItemDetails.Rate / Amount
     2. fn_PurchaseRequestItemRate (new), fn_PurchaseRequestApprovalRule (amount slabs)
     3. S_SearchItemForPurchaseRequest, IU_PurchaseRequest, U_SendPurchaseRequestForApproval,
        U_ApprovePurchaseRequest, S_GetPurchaseRequestList, S_GetPurchaseRequestDetails (amounts added)
     4. Back-fill amounts of requests saved before this script
   NOTE: existing Approval Authority rows of type 2 with AmountUpTo = 0 no longer match any request.
         Edit them in Approval Authority Master and set "Amount Up To".
   ===================================================================================== */
SET NOCOUNT ON;
GO

/* ---------- 1. Columns ---------- */
IF COL_LENGTH('dbo.PurchaseRequest', 'TotalAmount') IS NULL
    ALTER TABLE dbo.PurchaseRequest ADD TotalAmount decimal(18,2) NOT NULL CONSTRAINT DF_PurchaseRequest_TotalAmount DEFAULT (0);
IF COL_LENGTH('dbo.PurchaseRequest', 'ApprovedAmount') IS NULL
    ALTER TABLE dbo.PurchaseRequest ADD ApprovedAmount decimal(18,2) NOT NULL CONSTRAINT DF_PurchaseRequest_ApprovedAmount DEFAULT (0);
IF COL_LENGTH('dbo.PurchaseRequest', 'ApprovalAmount') IS NULL
    ALTER TABLE dbo.PurchaseRequest ADD ApprovalAmount decimal(18,2) NULL;      -- amount the approval rule is chosen by (set on send)
IF COL_LENGTH('dbo.PurchaseRequestItemDetails', 'Rate') IS NULL
    ALTER TABLE dbo.PurchaseRequestItemDetails ADD Rate decimal(18,4) NOT NULL CONSTRAINT DF_PRItem_Rate DEFAULT (0);   -- per purchase unit
IF COL_LENGTH('dbo.PurchaseRequestItemDetails', 'Amount') IS NULL
    ALTER TABLE dbo.PurchaseRequestItemDetails ADD Amount decimal(18,2) NOT NULL CONSTRAINT DF_PRItem_Amount DEFAULT (0); -- RequestQTY x Rate
GO

/* ---------- 2. Functions ---------- */

/* last landed cost per purchase unit of an item in the branch (0 = never received) */
CREATE OR ALTER FUNCTION dbo.fn_PurchaseRequestItemRate (@hospId int, @branchId int, @itemId int, @packing int)
RETURNS decimal(18,4)
AS
BEGIN
    DECLARE @unitCost decimal(18,6);
    SELECT TOP 1 @unitCost = s.UnitCost
    FROM dbo.Stock s
    WHERE s.HospId = @hospId AND s.BranchId = @branchId AND s.ItemId = @itemId AND s.IsActive = 1 AND ISNULL(s.UnitCost, 0) > 0
    ORDER BY s.StockId DESC;
    RETURN ROUND(ISNULL(@unitCost, 0) * CASE WHEN ISNULL(@packing, 0) > 0 THEN @packing ELSE 1 END, 4);
END
GO

/* approval rule of a request by amount (write-off pattern: smallest AmountUpTo >= amount) */
CREATE OR ALTER FUNCTION dbo.fn_PurchaseRequestApprovalRule (@purchaseRequestId int)
RETURNS TABLE
AS
RETURN
(
    SELECT TOP 1 AAM.Id AS ApprovalAuthorityId, AAM.ApprovalFlowId, AAM.ApprovalFlow, AAM.IsAllApprovalRequired,
           AAM.ApprovalLevelId, AAM.ApprovalLevel, AAM.AmountUpTo,
           AAM.Level1UserId, AAM.Level2UserId, AAM.Level3UserId, AAM.Level4UserId
    FROM dbo.PurchaseRequest PR
    INNER JOIN dbo.ApprovalAuthorityMaster AAM
            ON AAM.ApprovalTypeId = 2 AND AAM.IsActive = 1 AND AAM.HospId = PR.HospId
           AND AAM.RoleId IN (PR.FromDeptId, 0)
           AND AAM.BranchId IN (PR.BranchId, 0)
           AND ISNULL(AAM.AmountUpTo, 0) > 0
           AND AAM.AmountUpTo >= ISNULL(PR.ApprovalAmount, PR.TotalAmount)
    WHERE PR.PurchaseRequestId = @purchaseRequestId
    ORDER BY CASE WHEN AAM.RoleId = PR.FromDeptId THEN 0 ELSE 1 END,
             CASE WHEN AAM.BranchId = PR.BranchId THEN 0 ELSE 1 END,
             AAM.AmountUpTo ASC,
             AAM.Id DESC
);
GO

/* ---------- 3. Procedures ---------- */

/* items for the request grid + last landed cost per purchase unit (Rate) */
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
        pk.Packing, ISNULL(sim.PurchaseUnitName, '') AS PurchaseUnitName,
        ISNULL(sim.SaleUnitName, '') AS SaleUnitName,
        ISNULL(st.CurrentStock, 0) AS CurrentStock,
        lv.MinLevel, lv.ReorderLevel, lv.MaxLevel,
        dbo.fn_PurchaseRequestItemRate(@hospId, @branchId, sim.ServiceItemId, pk.Packing) AS Rate
    FROM dbo.ServiceItemMaster sim
    CROSS APPLY (SELECT ISNULL(NULLIF(sim.Packing, 0), 1) AS Packing) pk
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

/* save a new request or update a Draft; rate / amount of each item and request total are calculated here
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

    INSERT INTO dbo.PurchaseRequestItemDetails (PurchaseRequestId, ItemId, Packing, RequestQTY, ApprovedQTY, Rate, Amount, CreatedBy, CreatedOn, IpAddress)
    SELECT @Result, j.ItemId, j.Packing, j.Qty, j.Qty, r.Rate, ROUND(j.Qty * r.Rate, 2), @userId, GETDATE(), @ipAddress
    FROM OPENJSON(@itemsJson) WITH (ItemId int '$.ItemId', Packing int '$.Packing', Qty int '$.Qty') j
    CROSS APPLY (SELECT dbo.fn_PurchaseRequestItemRate(@hospId, @branchId, j.ItemId, j.Packing) AS Rate) r;

    UPDATE PR
    SET TotalAmount = ISNULL(t.Total, 0), ApprovedAmount = ISNULL(t.Total, 0)
    FROM dbo.PurchaseRequest PR
    OUTER APPLY (SELECT SUM(d.Amount) AS Total FROM dbo.PurchaseRequestItemDetails d WHERE d.PurchaseRequestId = PR.PurchaseRequestId) t
    WHERE PR.PurchaseRequestId = @Result;

    SELECT @Result;
END
GO

/* Draft -> Approval Pending. The request amount fixes the approval rule.
   -2 not Draft, -5 no approval rule covers the amount */
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

    UPDATE dbo.PurchaseRequest SET ApprovalAmount = NULL WHERE PurchaseRequestId = @purchaseRequestId;   -- rule by current total
    IF NOT EXISTS (SELECT 1 FROM dbo.fn_PurchaseRequestApprovalRule(@purchaseRequestId))
    BEGIN
        SET @Result = -5; SELECT @Result; RETURN;
    END

    UPDATE dbo.PurchaseRequest
    SET Status = 'Approval Pending', StatusId = 2, ApprovalAmount = TotalAmount,
        SentForApprovalBy = @userId, SentForApprovalOn = GETDATE(),
        LastModifiedBy = @userId, LastModifiedOn = GETDATE(), IpAddress = @ipAddress
    WHERE PurchaseRequestId = @purchaseRequestId;
    SET @Result = @purchaseRequestId;
    SELECT @Result;
END
GO

/* approve the current level; approved qty may be lowered (0..requested), ApprovedAmount follows
   @itemsJson = [{"PurchaseRequestItemId":1,"ApprovedQty":4}, ...]
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

    UPDATE PR
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
        ApprovedAmount = ISNULL(t.Total, 0),
        LastModifiedBy = @userId, LastModifiedOn = GETDATE(), IpAddress = @ipAddress
    FROM dbo.PurchaseRequest PR
    OUTER APPLY (SELECT SUM(ROUND(d.ApprovedQTY * d.Rate, 2)) AS Total
                 FROM dbo.PurchaseRequestItemDetails d WHERE d.PurchaseRequestId = PR.PurchaseRequestId) t
    WHERE PR.PurchaseRequestId = @purchaseRequestId;

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

/* list for both pages: FlagId / CanApprove for the user, amounts and the rule's AmountUpTo */
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
        PR.TotalAmount, PR.ApprovedAmount, ISNULL(PR.ApprovalAmount, PR.TotalAmount) AS ApprovalAmount, AM.AmountUpTo,
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

/* one request: header + approval details (set 1), items with stock, rate and amounts (set 2) */
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
        PR.TotalAmount, PR.ApprovedAmount, ISNULL(PR.ApprovalAmount, PR.TotalAmount) AS ApprovalAmount, AM.AmountUpTo,
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
        d.Rate, d.Amount, ROUND(d.ApprovedQTY * d.Rate, 2) AS ApprovedAmount,
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

/* ---------- 4. Back-fill requests saved before this script (rate still 0) ---------- */
UPDATE d
SET Rate = r.Rate, Amount = ROUND(d.RequestQTY * r.Rate, 2)
FROM dbo.PurchaseRequestItemDetails d
INNER JOIN dbo.PurchaseRequest PR ON PR.PurchaseRequestId = d.PurchaseRequestId
CROSS APPLY (SELECT dbo.fn_PurchaseRequestItemRate(PR.HospId, PR.BranchId, d.ItemId, d.Packing) AS Rate) r
WHERE d.Rate = 0 AND r.Rate > 0;

UPDATE PR
SET TotalAmount = ISNULL(t.Total, 0), ApprovedAmount = ISNULL(t.Approved, 0),
    ApprovalAmount = CASE WHEN PR.StatusId <> 0 AND PR.ApprovalAmount IS NULL THEN ISNULL(t.Total, 0) ELSE PR.ApprovalAmount END
FROM dbo.PurchaseRequest PR
OUTER APPLY (SELECT SUM(d.Amount) AS Total, SUM(ROUND(d.ApprovedQTY * d.Rate, 2)) AS Approved
             FROM dbo.PurchaseRequestItemDetails d WHERE d.PurchaseRequestId = PR.PurchaseRequestId) t
WHERE PR.TotalAmount = 0;
GO

PRINT 'Purchase Request amount-wise approval applied';
GO
