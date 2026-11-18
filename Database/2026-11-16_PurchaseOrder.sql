/* =====================================================================================
   Purchase Order + Purchase Order Approval                              2026-11-16
   Database : GWT_HIS_API      Re-runnable (IF NOT EXISTS for tables / rows, CREATE OR ALTER for code)
   Run AFTER 2026-11-16_PurchaseRequest.sql and 2026-11-16_PurchaseRequest_AmountWise.sql (PO By PR reads them).

   OLD (DEMO_HIS_NEW): PurchaseOrder, PurchaseOrderItemDetails, POTermConditionMaster/Details,
       I_StockReceivingByPO, U_UpdatePurchaseOrder, U_PostStockReceivingByPO, U_CancelStockReceivingByPO,
       S_ViewPurchaseOrder, S_GetStockReceivingDetailsForEditByPO, S_GetItemDetailsByPurchaseRequestIdList,
       S_GetItemDetailsByROLforPO, S_SearchItemNameForPO, S_GetPOTermCondition.
   Kept from OLD : columns, PO types (PO By Item / PO By Supplier / PO By PR), one PO per vendor,
       Draft (0) -> Approval Pending (2) -> Approved (3), Cancelled (1) with reason; edit while Draft or Pending
       without any approval; approval = ApprovalAuthorityMaster type 3 chosen by PO Net Amount
       (smallest AmountUpTo >= amount, From Department role before RoleId 0).
   Changed (owner-approved plan 2026-11-16):
       - approvers are CSV user lists, sequential / parallel like Purchase Request (OLD compared one user id);
       - ApprovalAmount fixed at send; own branch before branch 0; only rules with AmountUpTo > 0;
       - line defaults come from the item's last GRN in the branch (NEW has no QuotationMaster);
       - an approved Purchase Request used by a PO is marked IsPOCreated = 1 (released again if all its POs are cancelled);
       - PO number = sequence type 10 'Purchase-Order' (OLD type 31 is General Stock Receiving in NEW).
   Return codes: -2 wrong status, -3 no PO number sequence, -5 no approval rule for the amount,
                 -1 user may not approve this level now, -7 a Purchase Request is already used / not approved.

   Contents
     1. Tables  PurchaseOrder, PurchaseOrderItemDetails, POTermConditionMaster (+ OLD terms), POTermConditionDetails;
                PurchaseRequest.IsPOCreated
     2. Functions fn_PurchaseOrderItemDefaults, fn_PurchaseOrderApprovalRule, fn_PurchaseOrderApproveFlag
     3. Procedures
     4. PO number sequence (type 10)
     5. Menus 'Purchase Order' (purchase-order) and 'Purchase Order Approval' (purchase-order-approval)
   ===================================================================================== */
SET NOCOUNT ON;
GO

/* ---------- 1. Tables ---------- */
IF OBJECT_ID('dbo.PurchaseOrder', 'U') IS NULL
CREATE TABLE dbo.PurchaseOrder
(
    PoId               int IDENTITY(1,1) NOT NULL CONSTRAINT PK_PurchaseOrder PRIMARY KEY,
    HospId             int            NOT NULL,
    BranchId           int            NOT NULL,
    RoleId             int            NOT NULL,              -- From Department (creator's role) - picks the approval rule
    StoreType          nvarchar(50)   NOT NULL,              -- Medical / General
    CategoryId         int            NOT NULL,              -- 6 Medical, 7 General
    POType             nvarchar(50)   NOT NULL,              -- No-Service / Service
    TypeId             int            NOT NULL,              -- 1 PO By Item, 2 PO By Supplier, 3 PO By PR
    POTypes            nvarchar(50)   NOT NULL,
    VendorId           int            NOT NULL,
    PONo               nvarchar(100)  NOT NULL,
    GrossAmount        decimal(18,2)  NOT NULL CONSTRAINT DF_PO_Gross DEFAULT (0),
    DiscountPercentage decimal(18,4)  NOT NULL CONSTRAINT DF_PO_DiscPer DEFAULT (0),
    DiscountAmount     decimal(18,2)  NOT NULL CONSTRAINT DF_PO_DiscAmt DEFAULT (0),
    TaxableAmount      decimal(18,2)  NOT NULL CONSTRAINT DF_PO_Taxable DEFAULT (0),
    GSTAmount          decimal(18,2)  NOT NULL CONSTRAINT DF_PO_Gst DEFAULT (0),
    NetAmount          decimal(18,2)  NOT NULL CONSTRAINT DF_PO_Net DEFAULT (0),
    RoundOff           decimal(18,2)  NOT NULL CONSTRAINT DF_PO_RoundOff DEFAULT (0),
    ApprovalAmount     decimal(18,2)  NULL,                  -- NetAmount when sent; picks the approval rule
    Priority           nvarchar(50)   NOT NULL CONSTRAINT DF_PO_Priority DEFAULT ('Normal'),
    PODate             date           NOT NULL,
    DeliveryDate       date           NULL,
    ValidDate          date           NULL,
    PORemarks          nvarchar(500)  NULL,
    DeliveryAddress    nvarchar(500)  NULL,
    DocumentPath       nvarchar(512)  NULL,
    OtherTermCondition nvarchar(1000) NULL,
    PurchaseRequestIds nvarchar(1000) NULL,                  -- PO By PR: comma list of PurchaseRequestId
    Status             nvarchar(100)  NOT NULL CONSTRAINT DF_PO_Status DEFAULT ('Draft'),
    StatusId           int            NOT NULL CONSTRAINT DF_PO_StatusId DEFAULT (0),
    IsCancel           int            NOT NULL CONSTRAINT DF_PO_IsCancel DEFAULT (0),
    CancelBy           int            NULL,
    CancelOn           datetime       NULL,
    CancelReason       nvarchar(500)  NULL,
    ApprovalRemarks    nvarchar(500)  NULL,
    SentForApprovalBy  int            NULL,
    SentForApprovalOn  datetime       NULL,
    IsLevel1Approve    int NULL, Level1ApproveId int NULL, Level1ApproveOn datetime NULL,
    IsLevel2Approve    int NULL, Level2ApproveId int NULL, Level2ApproveOn datetime NULL,
    IsLevel3Approve    int NULL, Level3ApproveId int NULL, Level3ApproveOn datetime NULL,
    IsLevel4Approve    int NULL, Level4ApproveId int NULL, Level4ApproveOn datetime NULL,
    CreatedBy          int            NOT NULL,
    CreatedOn          datetime       NOT NULL CONSTRAINT DF_PO_CreatedOn DEFAULT (GETDATE()),
    LastModifiedBy     int            NULL,
    LastModifiedOn     datetime       NULL,
    IpAddress          nvarchar(50)   NULL,
    UniqueId           nvarchar(50)   NOT NULL CONSTRAINT DF_PO_UniqueId DEFAULT (CONVERT(nvarchar(50), NEWID()))
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_PurchaseOrder_Branch_Created')
    CREATE INDEX IX_PurchaseOrder_Branch_Created ON dbo.PurchaseOrder (HospId, BranchId, CreatedOn) INCLUDE (StatusId, VendorId);
GO

IF OBJECT_ID('dbo.PurchaseOrderItemDetails', 'U') IS NULL
CREATE TABLE dbo.PurchaseOrderItemDetails
(
    PoItemId           int IDENTITY(1,1) NOT NULL CONSTRAINT PK_PurchaseOrderItemDetails PRIMARY KEY,
    PoId               int            NOT NULL CONSTRAINT FK_POItem_PO REFERENCES dbo.PurchaseOrder (PoId),
    ItemId             int            NOT NULL,
    ItemName           nvarchar(400)  NOT NULL,
    QTY                decimal(18,2)  NOT NULL,              -- purchase units (packs)
    FreeQTY            decimal(18,2)  NOT NULL CONSTRAINT DF_POItem_Free DEFAULT (0),
    Packing            int            NOT NULL,
    Deal1              int            NOT NULL CONSTRAINT DF_POItem_Deal1 DEFAULT (0),
    Deal2              int            NOT NULL CONSTRAINT DF_POItem_Deal2 DEFAULT (0),
    HSNCode            nvarchar(50)   NULL,
    MRP                decimal(18,4)  NOT NULL CONSTRAINT DF_POItem_Mrp DEFAULT (0),
    Rate               decimal(18,4)  NOT NULL,              -- per pack before tax
    DiscountPercentage decimal(18,4)  NOT NULL CONSTRAINT DF_POItem_DiscPer DEFAULT (0),
    DiscountAmount     decimal(18,2)  NOT NULL CONSTRAINT DF_POItem_DiscAmt DEFAULT (0),
    SpecialDiscPer     decimal(18,4)  NOT NULL CONSTRAINT DF_POItem_SpclPer DEFAULT (0),
    SpecialDiscAmt     decimal(18,2)  NOT NULL CONSTRAINT DF_POItem_SpclAmt DEFAULT (0),
    GSTPercentage      decimal(18,4)  NOT NULL CONSTRAINT DF_POItem_GstPer DEFAULT (0),
    GSTAmount          decimal(18,2)  NOT NULL CONSTRAINT DF_POItem_GstAmt DEFAULT (0),
    GrossAmount        decimal(18,2)  NOT NULL CONSTRAINT DF_POItem_Gross DEFAULT (0),
    NetAmount          decimal(18,2)  NOT NULL CONSTRAINT DF_POItem_Net DEFAULT (0),
    UnitCost           decimal(18,6)  NOT NULL CONSTRAINT DF_POItem_UnitCost DEFAULT (0),   -- net per sale unit
    CreatedBy          int            NOT NULL,
    CreatedOn          datetime       NOT NULL CONSTRAINT DF_POItem_CreatedOn DEFAULT (GETDATE()),
    IpAddress          nvarchar(50)   NULL
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_POItem_PoId')
    CREATE INDEX IX_POItem_PoId ON dbo.PurchaseOrderItemDetails (PoId);
GO

IF OBJECT_ID('dbo.POTermConditionMaster', 'U') IS NULL
CREATE TABLE dbo.POTermConditionMaster
(
    Id              int IDENTITY(1,1) NOT NULL CONSTRAINT PK_POTermConditionMaster PRIMARY KEY,
    HospId          int            NOT NULL CONSTRAINT DF_POTerm_Hosp DEFAULT (1),
    POTermCondition nvarchar(2000) NOT NULL,
    SequenceOrder   int            NOT NULL CONSTRAINT DF_POTerm_Seq DEFAULT (0),
    IsActive        int            NOT NULL CONSTRAINT DF_POTerm_Active DEFAULT (1),
    IsDefault       int            NOT NULL CONSTRAINT DF_POTerm_Default DEFAULT (0),   -- ticked on a new PO
    CreatedBy       int            NOT NULL CONSTRAINT DF_POTerm_CreatedBy DEFAULT (1),
    CreatedOn       datetime       NOT NULL CONSTRAINT DF_POTerm_CreatedOn DEFAULT (GETDATE()),
    LastModifiedBy  int            NULL,
    LastModifiedOn  datetime       NULL,
    IpAddress       nvarchar(50)   NULL,
    UniqueId        nvarchar(50)   NOT NULL CONSTRAINT DF_POTerm_UniqueId DEFAULT (CONVERT(nvarchar(50), NEWID()))
);
GO

/* OLD terms (DEMO_HIS_NEW.POTermConditionMaster) - only when the table is empty */
IF NOT EXISTS (SELECT 1 FROM dbo.POTermConditionMaster)
INSERT INTO dbo.POTermConditionMaster (POTermCondition, SequenceOrder, IsDefault) VALUES
(N'Medicines must be delivered strictly within the time frame mentioned in the purchase order. Delays beyond the deadline may result in penalties or cancellation of the order. All deliveries should be made during the hospital''s working hours, and prior notice should be given. Medicines requiring cold storage must be transported in temperature-controlled vehicles.', 5, 1),
(N'Payment will be made within the agreed-upon duration (e.g., 30, 45, or 60 days) from the date of receipt of the goods and verification of the invoice. Invoices must clearly mention batch numbers, expiry dates, and PO references. Any delay in delivery, documentation, or product compliance may result in delayed or withheld payments.', 2, 0),
(N'Medicines must meet pharmacopoeial standards and be approved by national drug regulatory authorities. No unapproved substitutions allowed.', 1, 1),
(N'Supplied medicines must have at least 75% shelf life remaining. Short-expiry products will be rejected immediately.', 3, 0),
(N'Each batch must include COA and batch release certificate. Random testing may be conducted; failed batches are rejected.', 4, 0),
(N'Payments made within agreed time post delivery and invoice verification. Incomplete or non-compliant deliveries delay payment.', 6, 0),
(N'Medicines supplied must strictly comply with pharmacopeial standards such as IP (Indian Pharmacopoeia), BP (British Pharmacopoeia), or USP (United States Pharmacopeia). Only products approved by the national drug regulatory authority (such as CDSCO in India or FDA in the U.S.) will be accepted. No substitute brands, dosages, or formulations will be allowed unless formally approved in writing by the hospital''s procurement department.', 10, 0);
GO

/* fix-up: an earlier run stored the curly apostrophe of "hospital's" as mojibake (a-circumflex, euro, trade mark) */
UPDATE dbo.POTermConditionMaster
   SET POTermCondition = REPLACE(POTermCondition, N'hospital' + NCHAR(226) + NCHAR(8364) + NCHAR(8482) + N's', N'hospital''s')
 WHERE POTermCondition LIKE N'%hospital' + NCHAR(226) + NCHAR(8364) + NCHAR(8482) + N's%';
GO

IF OBJECT_ID('dbo.POTermConditionDetails', 'U') IS NULL
CREATE TABLE dbo.POTermConditionDetails
(
    Id              int IDENTITY(1,1) NOT NULL CONSTRAINT PK_POTermConditionDetails PRIMARY KEY,
    PoId            int      NOT NULL CONSTRAINT FK_POTermDet_PO REFERENCES dbo.PurchaseOrder (PoId),
    TermConditionId int      NOT NULL CONSTRAINT FK_POTermDet_Term REFERENCES dbo.POTermConditionMaster (Id),
    CreatedBy       int      NOT NULL,
    CreatedOn       datetime NOT NULL CONSTRAINT DF_POTermDet_CreatedOn DEFAULT (GETDATE())
);
GO

IF COL_LENGTH('dbo.PurchaseRequest', 'IsPOCreated') IS NULL
    ALTER TABLE dbo.PurchaseRequest ADD IsPOCreated int NOT NULL CONSTRAINT DF_PurchaseRequest_IsPOCreated DEFAULT (0);
GO

/* ---------- 2. Functions ---------- */

/* defaults for a PO line: the item's last GRN lot in the branch (rate per pack, MRP, GST, HSN, vendor) */
CREATE OR ALTER FUNCTION dbo.fn_PurchaseOrderItemDefaults (@hospId int, @branchId int, @itemId int)
RETURNS TABLE
AS
RETURN
(
    SELECT TOP 1 s.Rate AS LastRate, s.MRP AS LastMrp, s.GSTPercentage AS LastGstPer, s.HSNCode AS LastHsnCode,
           sr.VendorId AS LastVendorId, LTRIM(RTRIM(vm.VendorName)) AS LastVendorName
    FROM dbo.Stock s
    INNER JOIN dbo.StockReceiving sr ON sr.Id = s.ReceivingId AND sr.IsCancel = 0
    LEFT JOIN dbo.VendorMaster vm ON vm.VendorId = sr.VendorId
    WHERE s.HospId = @hospId AND s.BranchId = @branchId AND s.ItemId = @itemId AND ISNULL(s.Rate, 0) > 0
    ORDER BY s.StockId DESC
);
GO

/* approval rule of a PO by amount: type 3, From Department role before 0, own branch before 0, smallest AmountUpTo */
CREATE OR ALTER FUNCTION dbo.fn_PurchaseOrderApprovalRule (@poId int)
RETURNS TABLE
AS
RETURN
(
    SELECT TOP 1 AAM.Id AS ApprovalAuthorityId, AAM.ApprovalFlowId, AAM.ApprovalFlow, AAM.IsAllApprovalRequired,
           AAM.ApprovalLevelId, AAM.ApprovalLevel, AAM.AmountUpTo,
           AAM.Level1UserId, AAM.Level2UserId, AAM.Level3UserId, AAM.Level4UserId
    FROM dbo.PurchaseOrder PO
    INNER JOIN dbo.ApprovalAuthorityMaster AAM
            ON AAM.ApprovalTypeId = 3 AND AAM.IsActive = 1 AND AAM.HospId = PO.HospId
           AND AAM.RoleId IN (PO.RoleId, 0)
           AND AAM.BranchId IN (PO.BranchId, 0)
           AND ISNULL(AAM.AmountUpTo, 0) > 0
           AND AAM.AmountUpTo >= ISNULL(PO.ApprovalAmount, PO.NetAmount)
    WHERE PO.PoId = @poId
    ORDER BY CASE WHEN AAM.RoleId = PO.RoleId THEN 0 ELSE 1 END,
             CASE WHEN AAM.BranchId = PO.BranchId THEN 0 ELSE 1 END,
             AAM.AmountUpTo ASC,
             AAM.Id DESC
);
GO

/* level the given user may approve now (0 = none) - same rules as Purchase Request */
CREATE OR ALTER FUNCTION dbo.fn_PurchaseOrderApproveFlag (@poId int, @userId int)
RETURNS int
AS
BEGIN
    DECLARE @flag int = 0, @cur int = 0, @statusId int, @isCancel int,
            @l1 int, @l2 int, @l3 int, @l4 int,
            @levels int, @flow int, @all int, @u1 nvarchar(512), @u2 nvarchar(512), @u3 nvarchar(512), @u4 nvarchar(512);

    SELECT @statusId = StatusId, @isCancel = IsCancel,
           @l1 = Level1ApproveId, @l2 = Level2ApproveId, @l3 = Level3ApproveId, @l4 = Level4ApproveId
    FROM dbo.PurchaseOrder WHERE PoId = @poId;
    IF @statusId IS NULL OR @statusId <> 2 OR @isCancel = 1 RETURN 0;

    SELECT @levels = ApprovalLevelId, @flow = ApprovalFlowId, @all = IsAllApprovalRequired,
           @u1 = Level1UserId, @u2 = Level2UserId, @u3 = Level3UserId, @u4 = Level4UserId
    FROM dbo.fn_PurchaseOrderApprovalRule(@poId);
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

/* ---------- 3. Procedures ---------- */

/* OLD S_GetPOTermCondition: active terms, sequence 0 last */
CREATE OR ALTER PROCEDURE dbo.S_GetPOTermConditions
    @hospId int
AS
BEGIN
    SET NOCOUNT ON;
    SELECT Id, POTermCondition, SequenceOrder, IsDefault
    FROM dbo.POTermConditionMaster
    WHERE IsActive = 1 AND HospId = @hospId
    ORDER BY CASE WHEN SequenceOrder = 0 THEN 1000 ELSE SequenceOrder END, Id;
END
GO

/* item defaults for PO lines (search pick, Get Item by ROL, PO By PR)
   @itemIds = comma list; @belowRol = 1 -> OLD S_GetItemDetailsByROLforPO (branch stock below reorder level, all departments);
   SuggestedQty (packs) = (max level, else reorder level) - branch stock, in packs, rounded up */
CREATE OR ALTER PROCEDURE dbo.S_GetPurchaseOrderItemDefaults
    @hospId     int,
    @branchId   int,
    @categoryId int,
    @itemIds    nvarchar(max) = NULL,
    @belowRol   bit = 0
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP 500
        sim.ServiceItemId AS ItemId, sim.Name AS ItemName, ISNULL(sim.Code, '') AS ItemCode,
        pk.Packing, ISNULL(sim.PurchaseUnitName, '') AS PurchaseUnitName,
        LTRIM(RTRIM(ISNULL(NULLIF(d.LastHsnCode, ''), ISNULL(sim.HSNCode, '')))) AS HsnCode,
        ISNULL(d.LastGstPer, ISNULL(sim.GSTPer, 0)) AS GstPer,
        ISNULL(d.LastRate, 0) AS Rate, ISNULL(d.LastMrp, 0) AS Mrp,
        ISNULL(d.LastVendorId, 0) AS VendorId, ISNULL(d.LastVendorName, '') AS VendorName,
        ISNULL(st.Qty, 0) AS BranchStock, lv.ReorderLevel, lv.MaxLevel,
        CASE WHEN (CASE WHEN lv.MaxLevel > 0 THEN lv.MaxLevel ELSE lv.ReorderLevel END) > ISNULL(st.Qty, 0)
             THEN CEILING(((CASE WHEN lv.MaxLevel > 0 THEN lv.MaxLevel ELSE lv.ReorderLevel END) - ISNULL(st.Qty, 0)) * 1.0 / pk.Packing)
             ELSE 0 END AS SuggestedQty
    FROM dbo.ServiceItemMaster sim
    CROSS APPLY (SELECT ISNULL(NULLIF(sim.Packing, 0), 1) AS Packing) pk
    CROSS APPLY (SELECT ISNULL(TRY_CAST(sim.ReorderLevel AS int), 0) AS ReorderLevel,
                        ISNULL(TRY_CAST(sim.MaxLevel AS int), 0) AS MaxLevel) lv
    OUTER APPLY dbo.fn_PurchaseOrderItemDefaults(@hospId, @branchId, sim.ServiceItemId) d
    OUTER APPLY (SELECT SUM(s.CurrentStock) AS Qty FROM dbo.Stock s
                 WHERE s.HospId = @hospId AND s.BranchId = @branchId AND s.ItemId = sim.ServiceItemId
                   AND s.IsActive = 1 AND s.CurrentStock > 0 AND CAST(s.ExpiryDate AS date) > CAST(GETDATE() AS date)) st
    WHERE sim.CategoryId = @categoryId AND sim.IsActive = 1
      AND (@itemIds IS NULL OR sim.ServiceItemId IN (SELECT TRY_CAST(value AS int) FROM STRING_SPLIT(@itemIds, ',')))
      AND (@belowRol = 0 OR (lv.ReorderLevel > 0 AND lv.ReorderLevel > ISNULL(st.Qty, 0)))
    ORDER BY sim.Name;
END
GO

/* OLD "Check Purchase Request" popup: approved, not cancelled requests not yet used by a PO */
CREATE OR ALTER PROCEDURE dbo.S_GetApprovedPurchaseRequestsForPO
    @hospId     int,
    @branchId   int,
    @categoryId int,
    @fromDate   date,
    @toDate     date,
    @fromDeptId int = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT PR.PurchaseRequestId, PR.PurchaseRequestNo, FORMAT(PR.CreatedOn, 'dd-MM-yyyy') AS PRDate,
           LTRIM(RTRIM(fr.RoleName)) AS FromDeptName, LTRIM(RTRIM(tr.RoleName)) AS ToDeptName,
           CASE PR.CategoryId WHEN 6 THEN 'Medical' ELSE 'General' END AS StoreType,
           it.ItemCount, it.ApprovedQty, PR.ApprovedAmount
    FROM dbo.PurchaseRequest PR
    LEFT JOIN dbo.RoleMaster fr ON fr.RoleId = PR.FromDeptId
    LEFT JOIN dbo.RoleMaster tr ON tr.RoleId = PR.ToDeptId
    CROSS APPLY (SELECT COUNT(1) AS ItemCount, SUM(d.ApprovedQTY) AS ApprovedQty
                 FROM dbo.PurchaseRequestItemDetails d WHERE d.PurchaseRequestId = PR.PurchaseRequestId AND d.ApprovedQTY > 0) it
    WHERE PR.HospId = @hospId AND PR.BranchId = @branchId AND PR.CategoryId = @categoryId
      AND PR.StatusId = 3 AND PR.IsCancel = 0 AND PR.IsPOCreated = 0 AND it.ItemCount > 0
      AND PR.CreatedOn >= @fromDate AND PR.CreatedOn < DATEADD(DAY, 1, @toDate)
      AND (@fromDeptId IS NULL OR PR.FromDeptId = @fromDeptId)
    ORDER BY PR.CreatedOn DESC;
END
GO

/* OLD S_GetItemDetailsByPurchaseRequestIdList: approved qty of the chosen requests, one row per item (qty summed) + defaults */
CREATE OR ALTER PROCEDURE dbo.S_GetPurchaseRequestItemsForPO
    @hospId             int,
    @branchId           int,
    @purchaseRequestIds nvarchar(1000)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT q.ItemId, sim.Name AS ItemName, ISNULL(sim.Code, '') AS ItemCode, q.Packing,
           ISNULL(sim.PurchaseUnitName, '') AS PurchaseUnitName,
           LTRIM(RTRIM(ISNULL(NULLIF(d.LastHsnCode, ''), ISNULL(sim.HSNCode, '')))) AS HsnCode,
           ISNULL(d.LastGstPer, ISNULL(sim.GSTPer, 0)) AS GstPer,
           ISNULL(d.LastRate, 0) AS Rate, ISNULL(d.LastMrp, 0) AS Mrp,
           ISNULL(d.LastVendorId, 0) AS VendorId, ISNULL(d.LastVendorName, '') AS VendorName,
           q.Qty, q.PurchaseRequestNos
    FROM (SELECT pd.ItemId, MAX(pd.Packing) AS Packing, SUM(pd.ApprovedQTY) AS Qty,
                 STRING_AGG(PR.PurchaseRequestNo, ', ') AS PurchaseRequestNos
          FROM dbo.PurchaseRequest PR
          INNER JOIN dbo.PurchaseRequestItemDetails pd ON pd.PurchaseRequestId = PR.PurchaseRequestId AND pd.ApprovedQTY > 0
          WHERE PR.HospId = @hospId AND PR.BranchId = @branchId AND PR.StatusId = 3 AND PR.IsCancel = 0
            AND PR.PurchaseRequestId IN (SELECT TRY_CAST(value AS int) FROM STRING_SPLIT(@purchaseRequestIds, ','))
          GROUP BY pd.ItemId) q
    INNER JOIN dbo.ServiceItemMaster sim ON sim.ServiceItemId = q.ItemId
    OUTER APPLY dbo.fn_PurchaseOrderItemDefaults(@hospId, @branchId, q.ItemId) d
    ORDER BY sim.Name;
END
GO

/* save a new PO or update an editable one (Draft, or Pending with no approval yet)
   amounts come computed from the API (same arithmetic as GRN); @itemsJson =
   [{"ItemId":1,"ItemName":"x","Qty":10,"FreeQty":0,"Packing":10,"Deal1":0,"Deal2":0,"HsnCode":"","Mrp":0,"Rate":0,
     "DiscPer":0,"DiscAmt":0,"SpclDiscPer":0,"SpclDiscAmt":0,"GstPer":0,"GstAmt":0,"GrossAmt":0,"NetAmt":0,"UnitCost":0}]
   -2 not editable, -3 no PO number sequence, -7 a Purchase Request is not approved or already used by another PO */
CREATE OR ALTER PROCEDURE dbo.IU_PurchaseOrder
    @hospId             int,
    @branchId           int,
    @poId               int = 0,
    @roleId             int,
    @storeType          nvarchar(50),
    @categoryId         int,
    @poType             nvarchar(50),
    @typeId             int,
    @poTypes            nvarchar(50),
    @vendorId           int,
    @grossAmount        decimal(18,2),
    @discountPercentage decimal(18,4),
    @discountAmount     decimal(18,2),
    @taxableAmount      decimal(18,2),
    @gstAmount          decimal(18,2),
    @netAmount          decimal(18,2),
    @roundOff           decimal(18,2),
    @priority           nvarchar(50),
    @poDate             date,
    @deliveryDate       date = NULL,
    @validDate          date = NULL,
    @poRemarks          nvarchar(500) = NULL,
    @deliveryAddress    nvarchar(500) = NULL,
    @documentPath       nvarchar(512) = NULL,
    @otherTermCondition nvarchar(1000) = NULL,
    @purchaseRequestIds nvarchar(1000) = NULL,
    @termIds            nvarchar(1000) = NULL,
    @itemsJson          nvarchar(max),
    @userId             int,
    @ipAddress          nvarchar(50) = NULL,
    @Result             int = NULL OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @poNo nvarchar(100), @oldPrIds nvarchar(1000);

    /* PO By PR: every request must be approved and not used by another live PO */
    IF NULLIF(@purchaseRequestIds, '') IS NOT NULL
       AND EXISTS (SELECT 1 FROM STRING_SPLIT(@purchaseRequestIds, ',') x
                   LEFT JOIN dbo.PurchaseRequest PR ON PR.PurchaseRequestId = TRY_CAST(x.value AS int)
                   WHERE PR.PurchaseRequestId IS NULL OR PR.StatusId <> 3 OR PR.IsCancel = 1
                      OR EXISTS (SELECT 1 FROM dbo.PurchaseOrder o
                                 WHERE o.IsCancel = 0 AND o.PoId <> ISNULL(@poId, 0)
                                   AND ',' + o.PurchaseRequestIds + ',' LIKE '%,' + CAST(PR.PurchaseRequestId AS varchar(20)) + ',%'
                                   AND o.VendorId = @vendorId))
    BEGIN
        SET @Result = -7; SELECT @Result; RETURN;
    END

    IF ISNULL(@poId, 0) = 0
    BEGIN
        EXEC dbo.getBranchSequenceNumber @branchId, @roleId, 10, @poNo OUTPUT;
        IF NULLIF(LTRIM(RTRIM(@poNo)), '') IS NULL
        BEGIN
            SET @Result = -3; SELECT @Result; RETURN;
        END

        INSERT INTO dbo.PurchaseOrder
            (HospId, BranchId, RoleId, StoreType, CategoryId, POType, TypeId, POTypes, VendorId, PONo,
             GrossAmount, DiscountPercentage, DiscountAmount, TaxableAmount, GSTAmount, NetAmount, RoundOff,
             Priority, PODate, DeliveryDate, ValidDate, PORemarks, DeliveryAddress, DocumentPath, OtherTermCondition,
             PurchaseRequestIds, Status, StatusId, IsCancel, CreatedBy, CreatedOn, IpAddress)
        VALUES
            (@hospId, @branchId, @roleId, @storeType, @categoryId, @poType, @typeId, @poTypes, @vendorId, @poNo,
             @grossAmount, @discountPercentage, @discountAmount, @taxableAmount, @gstAmount, @netAmount, @roundOff,
             @priority, @poDate, @deliveryDate, @validDate, @poRemarks, @deliveryAddress, @documentPath, @otherTermCondition,
             NULLIF(@purchaseRequestIds, ''), 'Draft', 0, 0, @userId, GETDATE(), @ipAddress);
        SET @Result = SCOPE_IDENTITY();
    END
    ELSE
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM dbo.PurchaseOrder
                       WHERE PoId = @poId AND HospId = @hospId AND IsCancel = 0
                         AND (StatusId = 0 OR (StatusId = 2 AND Level1ApproveId IS NULL AND Level2ApproveId IS NULL
                                               AND Level3ApproveId IS NULL AND Level4ApproveId IS NULL)))
        BEGIN
            SET @Result = -2; SELECT @Result; RETURN;
        END
        SELECT @oldPrIds = PurchaseRequestIds FROM dbo.PurchaseOrder WHERE PoId = @poId;

        /* store type / PO type / department stay as created (OLD edit keeps them) */
        UPDATE dbo.PurchaseOrder
        SET VendorId = @vendorId, GrossAmount = @grossAmount, DiscountPercentage = @discountPercentage,
            DiscountAmount = @discountAmount, TaxableAmount = @taxableAmount, GSTAmount = @gstAmount,
            NetAmount = @netAmount, RoundOff = @roundOff,
            ApprovalAmount = CASE WHEN StatusId = 2 THEN @netAmount ELSE ApprovalAmount END,  -- pending: rule follows the new amount
            Priority = @priority, PODate = @poDate, DeliveryDate = @deliveryDate, ValidDate = @validDate,
            PORemarks = @poRemarks, DeliveryAddress = @deliveryAddress,
            DocumentPath = ISNULL(@documentPath, DocumentPath), OtherTermCondition = @otherTermCondition,
            PurchaseRequestIds = NULLIF(@purchaseRequestIds, ''),
            LastModifiedBy = @userId, LastModifiedOn = GETDATE(), IpAddress = @ipAddress
        WHERE PoId = @poId;

        DELETE FROM dbo.PurchaseOrderItemDetails WHERE PoId = @poId;
        DELETE FROM dbo.POTermConditionDetails WHERE PoId = @poId;
        SET @Result = @poId;
    END

    INSERT INTO dbo.PurchaseOrderItemDetails
        (PoId, ItemId, ItemName, QTY, FreeQTY, Packing, Deal1, Deal2, HSNCode, MRP, Rate, DiscountPercentage, DiscountAmount,
         SpecialDiscPer, SpecialDiscAmt, GSTPercentage, GSTAmount, GrossAmount, NetAmount, UnitCost, CreatedBy, CreatedOn, IpAddress)
    SELECT @Result, j.ItemId, j.ItemName, j.Qty, j.FreeQty, j.Packing, j.Deal1, j.Deal2, NULLIF(j.HsnCode, ''), j.Mrp, j.Rate,
           j.DiscPer, j.DiscAmt, j.SpclDiscPer, j.SpclDiscAmt, j.GstPer, j.GstAmt, j.GrossAmt, j.NetAmt, j.UnitCost,
           @userId, GETDATE(), @ipAddress
    FROM OPENJSON(@itemsJson) WITH (
        ItemId int '$.ItemId', ItemName nvarchar(400) '$.ItemName', Qty decimal(18,2) '$.Qty', FreeQty decimal(18,2) '$.FreeQty',
        Packing int '$.Packing', Deal1 int '$.Deal1', Deal2 int '$.Deal2', HsnCode nvarchar(50) '$.HsnCode',
        Mrp decimal(18,4) '$.Mrp', Rate decimal(18,4) '$.Rate', DiscPer decimal(18,4) '$.DiscPer', DiscAmt decimal(18,2) '$.DiscAmt',
        SpclDiscPer decimal(18,4) '$.SpclDiscPer', SpclDiscAmt decimal(18,2) '$.SpclDiscAmt', GstPer decimal(18,4) '$.GstPer',
        GstAmt decimal(18,2) '$.GstAmt', GrossAmt decimal(18,2) '$.GrossAmt', NetAmt decimal(18,2) '$.NetAmt',
        UnitCost decimal(18,6) '$.UnitCost') j;

    INSERT INTO dbo.POTermConditionDetails (PoId, TermConditionId, CreatedBy, CreatedOn)
    SELECT DISTINCT @Result, t.Id, @userId, GETDATE()
    FROM STRING_SPLIT(ISNULL(@termIds, ''), ',') x
    INNER JOIN dbo.POTermConditionMaster t ON t.Id = TRY_CAST(x.value AS int);

    /* requests used now are marked; requests dropped from this PO are released when no other live PO uses them */
    UPDATE dbo.PurchaseRequest SET IsPOCreated = 1
    WHERE PurchaseRequestId IN (SELECT TRY_CAST(value AS int) FROM STRING_SPLIT(ISNULL(@purchaseRequestIds, ''), ','));
    UPDATE PR SET IsPOCreated = 0
    FROM dbo.PurchaseRequest PR
    WHERE PR.PurchaseRequestId IN (SELECT TRY_CAST(value AS int) FROM STRING_SPLIT(ISNULL(@oldPrIds, ''), ','))
      AND NOT EXISTS (SELECT 1 FROM dbo.PurchaseOrder o
                      WHERE o.IsCancel = 0 AND ',' + o.PurchaseRequestIds + ',' LIKE '%,' + CAST(PR.PurchaseRequestId AS varchar(20)) + ',%');

    SELECT @Result;
END
GO

/* Draft -> Approval Pending (OLD flag 0). -2 not Draft, -5 no rule covers the net amount */
CREATE OR ALTER PROCEDURE dbo.U_SendPurchaseOrderForApproval
    @hospId int, @poId int, @userId int, @ipAddress nvarchar(50) = NULL,
    @Result int = NULL OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    IF NOT EXISTS (SELECT 1 FROM dbo.PurchaseOrder WHERE PoId = @poId AND HospId = @hospId AND StatusId = 0 AND IsCancel = 0)
    BEGIN
        SET @Result = -2; SELECT @Result; RETURN;
    END

    UPDATE dbo.PurchaseOrder SET ApprovalAmount = NULL WHERE PoId = @poId;
    IF NOT EXISTS (SELECT 1 FROM dbo.fn_PurchaseOrderApprovalRule(@poId))
    BEGIN
        SET @Result = -5; SELECT @Result; RETURN;
    END

    UPDATE dbo.PurchaseOrder
    SET Status = 'Approval Pending', StatusId = 2, ApprovalAmount = NetAmount,
        SentForApprovalBy = @userId, SentForApprovalOn = GETDATE(),
        LastModifiedBy = @userId, LastModifiedOn = GETDATE(), IpAddress = @ipAddress
    WHERE PoId = @poId;
    SET @Result = @poId;
    SELECT @Result;
END
GO

/* approve the current level (OLD U_PostStockReceivingByPO flag 1-4); approved when the last level is done
   or the flow is parallel with any-one-approval. -1 user may not approve this level now, -2 not pending */
CREATE OR ALTER PROCEDURE dbo.U_ApprovePurchaseOrder
    @hospId int, @poId int, @flag int, @approvalRemarks nvarchar(500) = NULL,
    @userId int, @ipAddress nvarchar(50) = NULL,
    @Result int = NULL OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @levels int, @flow int, @all int;

    IF NOT EXISTS (SELECT 1 FROM dbo.PurchaseOrder WHERE PoId = @poId AND HospId = @hospId AND StatusId = 2 AND IsCancel = 0)
    BEGIN
        SET @Result = -2; SELECT @Result; RETURN;
    END
    IF @flag NOT BETWEEN 1 AND 4 OR dbo.fn_PurchaseOrderApproveFlag(@poId, @userId) <> @flag
    BEGIN
        SET @Result = -1; SELECT @Result; RETURN;
    END

    UPDATE dbo.PurchaseOrder
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
    WHERE PoId = @poId;

    SELECT @levels = ApprovalLevelId, @flow = ApprovalFlowId, @all = IsAllApprovalRequired
    FROM dbo.fn_PurchaseOrderApprovalRule(@poId);

    IF (@flow = 2 AND @all = 0)
       OR NOT EXISTS (SELECT 1 FROM dbo.PurchaseOrder
                      WHERE PoId = @poId
                        AND ((@levels >= 1 AND Level1ApproveId IS NULL) OR (@levels >= 2 AND Level2ApproveId IS NULL)
                          OR (@levels >= 3 AND Level3ApproveId IS NULL) OR (@levels >= 4 AND Level4ApproveId IS NULL)))
        UPDATE dbo.PurchaseOrder SET Status = 'Approved', StatusId = 3 WHERE PoId = @poId;

    SET @Result = @poId;
    SELECT @Result;
END
GO

/* cancel a Draft or Pending PO with a reason (OLD U_CancelStockReceivingByPO); its Purchase Requests are released
   when no other live PO uses them. -2 not Draft / Pending */
CREATE OR ALTER PROCEDURE dbo.U_CancelPurchaseOrder
    @hospId int, @poId int, @reason nvarchar(500), @userId int, @ipAddress nvarchar(50) = NULL,
    @Result int = NULL OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @prIds nvarchar(1000);
    IF NOT EXISTS (SELECT 1 FROM dbo.PurchaseOrder WHERE PoId = @poId AND HospId = @hospId AND StatusId IN (0, 2) AND IsCancel = 0)
    BEGIN
        SET @Result = -2; SELECT @Result; RETURN;
    END

    SELECT @prIds = PurchaseRequestIds FROM dbo.PurchaseOrder WHERE PoId = @poId;
    UPDATE dbo.PurchaseOrder
    SET Status = 'Cancel', StatusId = 1, IsCancel = 1, CancelBy = @userId, CancelOn = GETDATE(), CancelReason = @reason,
        LastModifiedBy = @userId, LastModifiedOn = GETDATE(), IpAddress = @ipAddress
    WHERE PoId = @poId;

    UPDATE PR SET IsPOCreated = 0
    FROM dbo.PurchaseRequest PR
    WHERE PR.PurchaseRequestId IN (SELECT TRY_CAST(value AS int) FROM STRING_SPLIT(ISNULL(@prIds, ''), ','))
      AND NOT EXISTS (SELECT 1 FROM dbo.PurchaseOrder o
                      WHERE o.IsCancel = 0 AND ',' + o.PurchaseRequestIds + ',' LIKE '%,' + CAST(PR.PurchaseRequestId AS varchar(20)) + ',%');

    SET @Result = @poId;
    SELECT @Result;
END
GO

/* list for both pages (OLD S_ViewPurchaseOrder): FlagId / CanApprove / CanEdit for the user, current approver, amounts */
CREATE OR ALTER PROCEDURE dbo.S_GetPurchaseOrderList
    @hospId     int,
    @branchId   int,
    @userId     int,
    @fromDate   date,
    @toDate     date,
    @categoryId int = NULL,
    @vendorId   int = NULL,
    @statusId   int = NULL,
    @poNo       nvarchar(50) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET @poNo = NULLIF(LTRIM(RTRIM(@poNo)), '');

    SELECT
        PO.PoId, PO.PONo, PO.BranchId, PO.RoleId, LTRIM(RTRIM(rm.RoleName)) AS FromDeptName,
        PO.StoreType, PO.POType, PO.TypeId, PO.POTypes, PO.VendorId, LTRIM(RTRIM(vm.VendorName)) AS VendorName,
        FORMAT(PO.PODate, 'dd-MM-yyyy') AS PODate, PO.Priority,
        PO.GrossAmount, PO.DiscountAmount, PO.GSTAmount, PO.NetAmount,
        ISNULL(PO.ApprovalAmount, PO.NetAmount) AS ApprovalAmount, AM.AmountUpTo,
        PO.StatusId, PO.IsCancel,
        CASE WHEN PO.StatusId = 2 AND LV.CurLevel > 0 THEN 'Pending Level ' + CAST(LV.CurLevel AS varchar(1)) + ' Approval'
             WHEN PO.StatusId = 2 AND AM.ApprovalLevelId IS NULL THEN 'Approval Authority Not Found'
             ELSE PO.Status END AS Status,
        CASE WHEN PO.StatusId = 2 AND LV.CurLevel = 1 THEN dbo.fn_GetUserNamesFromCSV(AM.Level1UserId)
             WHEN PO.StatusId = 2 AND LV.CurLevel = 2 THEN dbo.fn_GetUserNamesFromCSV(AM.Level2UserId)
             WHEN PO.StatusId = 2 AND LV.CurLevel = 3 THEN dbo.fn_GetUserNamesFromCSV(AM.Level3UserId)
             WHEN PO.StatusId = 2 AND LV.CurLevel = 4 THEN dbo.fn_GetUserNamesFromCSV(AM.Level4UserId)
             ELSE '' END AS CurrentApprover,
        ISNULL(it.ItemCount, 0) AS ItemCount, ISNULL(it.Qty, 0) AS TotalQty,
        ISNULL(PO.PORemarks, '') AS Remarks, ISNULL(PO.ApprovalRemarks, '') AS ApprovalRemarks,
        PO.IsLevel1Approve, FORMAT(PO.Level1ApproveOn, 'dd-MM-yyyy hh:mm tt') AS Level1ApproveOn,
        PO.IsLevel2Approve, FORMAT(PO.Level2ApproveOn, 'dd-MM-yyyy hh:mm tt') AS Level2ApproveOn,
        PO.IsLevel3Approve, FORMAT(PO.Level3ApproveOn, 'dd-MM-yyyy hh:mm tt') AS Level3ApproveOn,
        PO.IsLevel4Approve, FORMAT(PO.Level4ApproveOn, 'dd-MM-yyyy hh:mm tt') AS Level4ApproveOn,
        (SELECT TOP 1 name FROM dbo.UserMaster WHERE id = PO.CancelBy) AS CancelBy,
        FORMAT(PO.CancelOn, 'dd-MM-yyyy hh:mm tt') AS CancelOn, PO.CancelReason,
        (SELECT TOP 1 name FROM dbo.UserMaster WHERE id = PO.CreatedBy) AS CreatedBy,
        FORMAT(PO.CreatedOn, 'dd-MM-yyyy hh:mm tt') AS CreatedOn,
        dbo.fn_PurchaseOrderApproveFlag(PO.PoId, @userId) AS FlagId,
        CASE WHEN dbo.fn_PurchaseOrderApproveFlag(PO.PoId, @userId) > 0 THEN 1 ELSE 0 END AS CanApprove,
        CASE WHEN PO.IsCancel = 0 AND (PO.StatusId = 0 OR (PO.StatusId = 2 AND PO.Level1ApproveId IS NULL AND PO.Level2ApproveId IS NULL
                                                           AND PO.Level3ApproveId IS NULL AND PO.Level4ApproveId IS NULL))
             THEN 1 ELSE 0 END AS CanEdit
    FROM dbo.PurchaseOrder PO
    LEFT JOIN dbo.RoleMaster rm ON rm.RoleId = PO.RoleId
    LEFT JOIN dbo.VendorMaster vm ON vm.VendorId = PO.VendorId
    OUTER APPLY dbo.fn_PurchaseOrderApprovalRule(PO.PoId) AM
    OUTER APPLY (SELECT CurLevel = CASE WHEN AM.ApprovalLevelId >= 1 AND PO.Level1ApproveId IS NULL THEN 1
                                        WHEN AM.ApprovalLevelId >= 2 AND PO.Level2ApproveId IS NULL THEN 2
                                        WHEN AM.ApprovalLevelId >= 3 AND PO.Level3ApproveId IS NULL THEN 3
                                        WHEN AM.ApprovalLevelId >= 4 AND PO.Level4ApproveId IS NULL THEN 4 ELSE 0 END) LV
    OUTER APPLY (SELECT COUNT(1) AS ItemCount, SUM(d.QTY) AS Qty FROM dbo.PurchaseOrderItemDetails d WHERE d.PoId = PO.PoId) it
    WHERE PO.HospId = @hospId AND PO.BranchId = @branchId
      AND PO.CreatedOn >= @fromDate AND PO.CreatedOn < DATEADD(DAY, 1, @toDate)
      AND (@categoryId IS NULL OR PO.CategoryId = @categoryId)
      AND (@vendorId IS NULL OR PO.VendorId = @vendorId)
      AND (@statusId IS NULL OR PO.StatusId = @statusId)
      AND (@poNo IS NULL OR PO.PONo LIKE '%' + @poNo + '%')
    ORDER BY PO.CreatedOn DESC;
END
GO

/* one PO: header + approval (set 1), items (set 2), ticked term ids (set 3) */
CREATE OR ALTER PROCEDURE dbo.S_GetPurchaseOrderDetails
    @hospId int, @poId int, @userId int
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        PO.PoId, PO.PONo, PO.BranchId, PO.RoleId, LTRIM(RTRIM(rm.RoleName)) AS FromDeptName,
        PO.StoreType, PO.CategoryId, PO.POType, PO.TypeId, PO.POTypes, PO.VendorId, LTRIM(RTRIM(vm.VendorName)) AS VendorName,
        PO.GrossAmount, PO.DiscountPercentage, PO.DiscountAmount, PO.TaxableAmount, PO.GSTAmount, PO.NetAmount, PO.RoundOff,
        ISNULL(PO.ApprovalAmount, PO.NetAmount) AS ApprovalAmount, AM.AmountUpTo,
        PO.Priority, PO.PODate, PO.DeliveryDate, PO.ValidDate,
        ISNULL(PO.PORemarks, '') AS PORemarks, ISNULL(PO.DeliveryAddress, '') AS DeliveryAddress,
        ISNULL(PO.DocumentPath, '') AS DocumentPath, ISNULL(PO.OtherTermCondition, '') AS OtherTermCondition,
        ISNULL(PO.PurchaseRequestIds, '') AS PurchaseRequestIds,
        PO.Status, PO.StatusId, PO.IsCancel, ISNULL(PO.ApprovalRemarks, '') AS ApprovalRemarks,
        FORMAT(PO.CreatedOn, 'dd-MM-yyyy hh:mm tt') AS CreatedOn,
        (SELECT TOP 1 name FROM dbo.UserMaster WHERE id = PO.CreatedBy) AS CreatedBy,
        AM.ApprovalFlowId, AM.ApprovalFlow, AM.IsAllApprovalRequired, AM.ApprovalLevelId, AM.ApprovalLevel,
        dbo.fn_GetUserNamesFromCSV(AM.Level1UserId) AS Level1ApproverNames,
        dbo.fn_GetUserNamesFromCSV(AM.Level2UserId) AS Level2ApproverNames,
        dbo.fn_GetUserNamesFromCSV(AM.Level3UserId) AS Level3ApproverNames,
        dbo.fn_GetUserNamesFromCSV(AM.Level4UserId) AS Level4ApproverNames,
        PO.IsLevel1Approve, (SELECT TOP 1 name FROM dbo.UserMaster WHERE id = PO.Level1ApproveId) AS Level1ApprovedByName,
        FORMAT(PO.Level1ApproveOn, 'dd-MM-yyyy hh:mm tt') AS Level1ApproveOn,
        PO.IsLevel2Approve, (SELECT TOP 1 name FROM dbo.UserMaster WHERE id = PO.Level2ApproveId) AS Level2ApprovedByName,
        FORMAT(PO.Level2ApproveOn, 'dd-MM-yyyy hh:mm tt') AS Level2ApproveOn,
        PO.IsLevel3Approve, (SELECT TOP 1 name FROM dbo.UserMaster WHERE id = PO.Level3ApproveId) AS Level3ApprovedByName,
        FORMAT(PO.Level3ApproveOn, 'dd-MM-yyyy hh:mm tt') AS Level3ApproveOn,
        PO.IsLevel4Approve, (SELECT TOP 1 name FROM dbo.UserMaster WHERE id = PO.Level4ApproveId) AS Level4ApprovedByName,
        FORMAT(PO.Level4ApproveOn, 'dd-MM-yyyy hh:mm tt') AS Level4ApproveOn,
        (SELECT TOP 1 name FROM dbo.UserMaster WHERE id = PO.CancelBy) AS CancelBy,
        FORMAT(PO.CancelOn, 'dd-MM-yyyy hh:mm tt') AS CancelOn, PO.CancelReason,
        dbo.fn_PurchaseOrderApproveFlag(PO.PoId, @userId) AS FlagId
    FROM dbo.PurchaseOrder PO
    LEFT JOIN dbo.RoleMaster rm ON rm.RoleId = PO.RoleId
    LEFT JOIN dbo.VendorMaster vm ON vm.VendorId = PO.VendorId
    OUTER APPLY dbo.fn_PurchaseOrderApprovalRule(PO.PoId) AM
    WHERE PO.PoId = @poId AND PO.HospId = @hospId;

    SELECT d.PoItemId, d.ItemId, d.ItemName, ISNULL(sim.Code, '') AS ItemCode, ISNULL(sim.PurchaseUnitName, '') AS PurchaseUnitName,
           d.QTY AS Qty, d.FreeQTY AS FreeQty, d.Packing, d.Deal1, d.Deal2, ISNULL(d.HSNCode, '') AS HsnCode,
           d.MRP AS Mrp, d.Rate, d.DiscountPercentage AS DiscPer, d.DiscountAmount AS DiscAmt,
           d.SpecialDiscPer AS SpclDiscPer, d.SpecialDiscAmt AS SpclDiscAmt, d.GSTPercentage AS GstPer, d.GSTAmount AS GstAmt,
           d.GrossAmount AS GrossAmt, d.NetAmount AS NetAmt, d.UnitCost
    FROM dbo.PurchaseOrderItemDetails d
    INNER JOIN dbo.PurchaseOrder PO ON PO.PoId = d.PoId
    LEFT JOIN dbo.ServiceItemMaster sim ON sim.ServiceItemId = d.ItemId
    WHERE d.PoId = @poId AND PO.HospId = @hospId
    ORDER BY d.PoItemId;

    SELECT t.Id, t.POTermCondition
    FROM dbo.POTermConditionDetails x
    INNER JOIN dbo.POTermConditionMaster t ON t.Id = x.TermConditionId
    WHERE x.PoId = @poId
    ORDER BY CASE WHEN t.SequenceOrder = 0 THEN 1000 ELSE t.SequenceOrder END, t.Id;
END
GO

/* ---------- 4. PO number sequence (type 10, all branches, all roles) ---------- */
IF NOT EXISTS (SELECT 1 FROM dbo.BranchSequenceMapping WHERE TypeId = 10)
BEGIN
    DECLARE @sid int = (SELECT TOP 1 SequenceId FROM dbo.SequenceMaster WHERE TypeId = 10 AND Name = 'PO');
    IF @sid IS NULL
    BEGIN
        INSERT INTO dbo.SequenceMaster (HospId, Name, TypeId, TypeName, Prefix, FirstSeprator, FYFormatId, FYFormat,
                                        SecondSeprator, Length, Preview, IsActive, CreatedBy, CreatedOn, IpAddress, UniqueId)
        VALUES (1, 'PO', 10, 'Purchase-Order', 'PO', '/', 1, '2', '/', 6, 'PO/26-27/000001', 1, 1, GETDATE(), '127.0.0.1', CONVERT(nvarchar(50), NEWID()));
        SET @sid = SCOPE_IDENTITY();
    END
    INSERT INTO dbo.BranchSequenceMapping (BranchId, RoleId, TypeId, SequenceId, CreatedBy, CreatedOn, IpAddress, UniqueId)
    VALUES (0, 0, 10, @sid, 1, GETDATE(), '127.0.0.1', CONVERT(nvarchar(50), NEWID()));
END
GO

/* ---------- 5. Menu rows under tab 'Purchase' + role mapping (Admin 2, Pharmacy 5, Medical Store 15) ---------- */
DECLARE @tabId int = (SELECT TOP 1 TabId FROM dbo.NavigationTabMaster WHERE LTRIM(RTRIM(TabName)) = 'Purchase' AND IsActive = 1);
IF @tabId IS NULL
    PRINT 'Tab "Purchase" not found - add "Purchase Order" (purchase-order) and "Purchase Order Approval" (purchase-order-approval) from the Navigation Pane screen.';
ELSE
BEGIN
    DECLARE @menu TABLE (Url nvarchar(200), Name nvarchar(200), Seq int);
    INSERT INTO @menu VALUES ('purchase-order', 'Purchase Order', 3), ('purchase-order-approval', 'Purchase Order Approval', 4);

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
    CROSS JOIN (SELECT SubMenuId FROM dbo.NavigationSubMenuMaster WHERE URL IN ('purchase-order', 'purchase-order-approval')) s
    WHERE b.IsActive = 1
      AND EXISTS (SELECT 1 FROM dbo.RoleMaster rm WHERE rm.RoleId = r.RoleId)
      AND NOT EXISTS (SELECT 1 FROM dbo.RoleWiseMenuMappingMaster x
                      WHERE x.BranchId = b.BranchId AND x.RoleId = r.RoleId AND x.SubMenuId = s.SubMenuId);
END
GO

PRINT 'Purchase Order script applied';
GO
