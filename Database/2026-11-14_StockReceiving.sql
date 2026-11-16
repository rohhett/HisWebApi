/* =====================================================================
   Stock Receiving (GRN) - direct receiving from a vendor     2026-11-14
   Database : GWT_HIS_API          Re-runnable (IF NOT EXISTS / CREATE OR ALTER)

     1. TABLES   dbo.StockReceiving (GRN header), dbo.Stock (one row per received batch lot)
                 - ported from OLD DEMO_HIS_NEW; OLD forDepartment -> RoleId (store = role),
                   OLD RecevingNo -> ReceivingNo; added StatusId, ReceivingAgainstId, GRNDate,
                   PaymentMode, Remarks, TaxableAmount, FTID, post / cancel audit columns.
                 - item lines are FinancialTransactionDetails rows (FinancialTransactions TnxTypeId 4
                   'StockReceiving', ReceivingId = GRN id); Stock.FTDID links a lot to its line.
     2. PROCS    IU_StockReceiving             header + lines (JSON) - called inside the API transaction
                 U_PostStockReceiving          Draft -> Posted   (Stock.IsActive 0 -> 1)
                 U_UnPostStockReceiving        Posted -> Draft   (refused -1 once stock was issued)
                 U_CancelStockReceiving        Draft -> Cancelled (Stock.IsActive 2, FT/FTD IsCancel 1)
                 S_GetStockReceivingList       saved GRNs (filters)
                 S_GetStockReceivingDetails    header + lines of one GRN
                 S_SearchStoreItemForReceiving item search with last purchase rate / MRP
                 S_GetStockReceivingStores     stores (= roles) of the user in the branch
     3. SEQUENCE GRN numbers (6-digit counter, e.g. GRN/26-27/000001) for SequenceTypeMaster 30 Medical / 31 General / 34 Blood Bank Inventory
     4. MENU     'Stock Receiving' (stock-receiving) under tab 'Purchase', mapped to roles
                 Admin (2), Pharmacy (5), Medical Store (15) in every active branch.

   Status    : StockReceiving.StatusId 1 Draft, 2 Posted, 3 Cancelled.
   Stock     : IsActive 0 not posted, 1 available, 2 GRN cancelled, 3 replaced by a GRN edit (OLD meaning).
   After running: clear Redis keys _RoleWiseMenuMapping_* and the user menu keys (or log in again).
   ===================================================================== */

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

/* ---------- 1a. StockReceiving ---------- */
IF OBJECT_ID('dbo.StockReceiving', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.StockReceiving
    (
        HospId              int             NOT NULL,
        Id                  int             IDENTITY(1,1) NOT NULL,
        BranchId            int             NOT NULL,
        RoleId              int             NOT NULL,          -- store (OLD forDepartment)
        StoreType           nvarchar(50)    NOT NULL,          -- Medical / General / Blood Bank Inventory
        VendorId            int             NOT NULL,
        TypeId              int             NOT NULL CONSTRAINT DF_StockReceiving_TypeId DEFAULT (1),  -- 1 Direct Receiving, 2 PO Receiving
        [Type]              nvarchar(50)    NOT NULL CONSTRAINT DF_StockReceiving_Type DEFAULT ('Direct Receiving'),
        ReceivingNo         nvarchar(100)   NULL,
        ReceivingAgainstId  int             NOT NULL CONSTRAINT DF_StockReceiving_Against DEFAULT (1),  -- 1 Invoice, 2 Challan, 3 Challan & Invoice
        InvoiceNo           nvarchar(100)   NULL,
        InvoiceDate         date            NULL,
        ChallanNo           nvarchar(100)   NULL,
        ChallanDate         date            NULL,
        IsOnChallan         bit             NOT NULL CONSTRAINT DF_StockReceiving_IsOnChallan DEFAULT (0),
        GSTType             nvarchar(50)    NULL,              -- CGST & SGST / IGST
        GRNDate             date            NOT NULL,
        PaymentMode         nvarchar(30)    NULL,
        Remarks             nvarchar(1000)  NULL,
        GrossAmount         decimal(18,4)   NOT NULL CONSTRAINT DF_StockReceiving_Gross DEFAULT (0),
        DiscountPercentage  decimal(18,4)   NOT NULL CONSTRAINT DF_StockReceiving_DiscPer DEFAULT (0),  -- GRN discount %
        DiscountAmount      decimal(18,4)   NOT NULL CONSTRAINT DF_StockReceiving_DiscAmt DEFAULT (0),  -- all line discounts
        TaxableAmount       decimal(18,4)   NOT NULL CONSTRAINT DF_StockReceiving_Taxable DEFAULT (0),
        GSTAmount           decimal(18,4)   NOT NULL CONSTRAINT DF_StockReceiving_GST DEFAULT (0),
        NetAmount           decimal(18,4)   NOT NULL CONSTRAINT DF_StockReceiving_Net DEFAULT (0),     -- rounded
        RoundOff            decimal(18,4)   NOT NULL CONSTRAINT DF_StockReceiving_RoundOff DEFAULT (0),
        StatusId            int             NOT NULL CONSTRAINT DF_StockReceiving_StatusId DEFAULT (1),
        IsCancel            int             NOT NULL CONSTRAINT DF_StockReceiving_IsCancel DEFAULT (0),
        CancelReason        nvarchar(1000)  NULL,
        CancelledBy         int             NULL,
        CancelledOn         datetime        NULL,
        PostedBy            int             NULL,
        PostedOn            datetime        NULL,
        AmendmentRemark     nvarchar(1000)  NULL,
        IsSettled           int             NOT NULL CONSTRAINT DF_StockReceiving_IsSettled DEFAULT (0),
        PONo                int             NULL,
        FTID                int             NULL,
        CreatedBy           int             NULL,
        CreatedOn           datetime        NULL CONSTRAINT DF_StockReceiving_CreatedOn DEFAULT (GETDATE()),
        LastModifiedBy      int             NULL,
        LastModifiedOn      datetime        NULL,
        IpAddress           nvarchar(50)    NULL,
        UniqueId            nvarchar(100)   NOT NULL CONSTRAINT DF_StockReceiving_UniqueId DEFAULT (NEWID()),
        CONSTRAINT PK_StockReceiving PRIMARY KEY CLUSTERED (Id)
    );
    CREATE INDEX IX_StockReceiving_Vendor ON dbo.StockReceiving (HospId, VendorId, InvoiceNo);
    CREATE INDEX IX_StockReceiving_Date   ON dbo.StockReceiving (HospId, BranchId, GRNDate);
END
GO

/* ---------- 1b. Stock ---------- */
IF OBJECT_ID('dbo.Stock', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Stock
    (
        HospId          int             NOT NULL,
        StockId         int             IDENTITY(1,1) NOT NULL,
        ReceivingId     int             NULL,
        FTDID           int             NULL,
        BranchId        int             NULL,
        RoleId          int             NULL,
        ItemId          int             NULL,
        ItemName        nvarchar(400)   NULL,
        CurrentStock    int             NULL,              -- sale units = (QTY + FreeQTY) x Packing
        QTY             decimal(18,4)   NULL,              -- packs billed
        FreeQTY         decimal(18,4)   NULL,              -- packs free
        IssuedQTY       int             NOT NULL CONSTRAINT DF_Stock_IssuedQTY DEFAULT (0),
        ReturnedQTY     int             NOT NULL CONSTRAINT DF_Stock_ReturnedQTY DEFAULT (0),
        UnitCost        decimal(18,6)   NULL,              -- line net / units
        Rate            decimal(18,4)   NULL,              -- purchase rate per pack
        MRP             decimal(18,6)   NULL,              -- per sale unit
        MajorMRP        decimal(18,4)   NULL,              -- per pack
        BatchNo         nvarchar(100)   NULL,
        ExpiryDate      date            NULL,
        HSNCode         nvarchar(50)    NULL,
        Packing         int             NULL,
        GSTPercentage   decimal(18,4)   NULL,
        IsActive        int             NOT NULL CONSTRAINT DF_Stock_IsActive DEFAULT (0),
        FromStockId     int             NULL,
        ReceivedBy      int             NULL,
        ReceivedOn      datetime        NULL,
        CreatedBy       int             NULL,
        CreatedOn       datetime        NULL CONSTRAINT DF_Stock_CreatedOn DEFAULT (GETDATE()),
        LastModifiedBy  int             NULL,
        LastModifiedOn  datetime        NULL,
        IpAddress       nvarchar(50)    NULL,
        UniqueId        nvarchar(100)   NOT NULL CONSTRAINT DF_Stock_UniqueId DEFAULT (NEWID()),
        CONSTRAINT PK_Stock PRIMARY KEY CLUSTERED (StockId)
    );
    CREATE INDEX IX_Stock_Receiving ON dbo.Stock (ReceivingId);
    CREATE INDEX IX_Stock_Item      ON dbo.Stock (HospId, BranchId, RoleId, ItemId, IsActive);
END
GO

/* 2026-11-16 (owner): barcode scanned on the GRN is stored per lot here only - never in ServiceItemMaster */
IF COL_LENGTH('dbo.Stock', 'Barcode') IS NULL
    ALTER TABLE dbo.Stock ADD Barcode nvarchar(100) NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Stock_Barcode' AND object_id = OBJECT_ID('dbo.Stock'))
    CREATE INDEX IX_Stock_Barcode ON dbo.Stock (HospId, Barcode) INCLUDE (ItemId, IsActive) WHERE Barcode IS NOT NULL;
GO

/* 2026-11-16 (owner): Freight Charges (transport / "bhada") - stored on the GRN header ONLY.
   Not added to Net Payable, not spread into unit cost, not written to FinancialTransactions or any other table. */
IF COL_LENGTH('dbo.StockReceiving', 'FreightCharges') IS NULL
    ALTER TABLE dbo.StockReceiving ADD FreightCharges decimal(18,2) NULL;
GO

/* ---------- 2a. IU_StockReceiving ----------
   Header + all lines in one call. The API calls it inside its own SqlTransaction and computes
   every amount (same formulas as the screen); the procedure only stores them.
   @itemsJson: [{ itemId, itemName, subSubCategoryId, qty, freeQty, packing, deal1, deal2, batchNo,
                  expiryDate (yyyy-MM-dd), hsnCode, mrp (per pack), rate (per pack), discPer, discAmt,
                  spclDiscPer, spclDiscAmt, gstPer, gstAmt, grossAmt, netAmt, units, unitCost }]
   Result : >0 GRN id | -1 duplicate invoice / challan for the vendor | -2 GRN not editable
            (not found, posted or cancelled) | -3 no GRN number sequence for this branch / store type
            | -4 vendor not found or inactive */
CREATE OR ALTER PROCEDURE dbo.IU_StockReceiving
    @hospId              int,
    @branchId            int,
    @roleId              int,
    @receivingId         int = 0,
    @storeType           nvarchar(50),
    @vendorId            int,
    @receivingAgainstId  int,
    @invoiceNo           nvarchar(100) = NULL,
    @invoiceDate         date = NULL,
    @challanNo           nvarchar(100) = NULL,
    @challanDate         date = NULL,
    @gstType             nvarchar(50),
    @grnDate             date,
    @paymentMode         nvarchar(30) = NULL,
    @remarks             nvarchar(1000) = NULL,
    @amendmentRemark     nvarchar(1000) = NULL,
    @freightCharges      decimal(18,2) = NULL,     -- 2026-11-16: header only, not part of any amount
    @grossAmount         decimal(18,4),
    @discountPercentage  decimal(18,4),
    @discountAmount      decimal(18,4),
    @taxableAmount       decimal(18,4),
    @gstAmount           decimal(18,4),
    @netAmount           decimal(18,4),
    @roundOff            decimal(18,4),
    @itemsJson           nvarchar(max),
    @userId              int,
    @ipAddress           nvarchar(50) = NULL,
    @Result              int = NULL OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @ftId int, @receivingNo nvarchar(100), @seqType int;

    IF NOT EXISTS (SELECT 1 FROM dbo.VendorMaster WHERE VendorId = @vendorId AND HospId = @hospId AND IsActive = 1)
    BEGIN
        SET @Result = -4; SELECT @Result; RETURN;
    END

    /* duplicate invoice (or challan when there is no invoice) for the same vendor - OLD S_ValidateDuplicateInvoice */
    IF EXISTS (SELECT 1 FROM dbo.StockReceiving
               WHERE HospId = @hospId AND VendorId = @vendorId AND IsCancel = 0 AND Id <> ISNULL(@receivingId, 0)
                 AND ( (NULLIF(@invoiceNo, '') IS NOT NULL AND InvoiceNo = @invoiceNo)
                    OR (NULLIF(@invoiceNo, '') IS NULL AND NULLIF(@challanNo, '') IS NOT NULL AND ChallanNo = @challanNo) ))
    BEGIN
        SET @Result = -1; SELECT @Result; RETURN;
    END

    IF ISNULL(@receivingId, 0) = 0
    BEGIN
        SET @seqType = CASE @storeType WHEN 'Medical' THEN 30 WHEN 'General' THEN 31 WHEN 'Blood Bank Inventory' THEN 34 END;
        EXEC dbo.getBranchSequenceNumber @branchId, @roleId, @seqType, @receivingNo OUTPUT;
        IF NULLIF(LTRIM(RTRIM(@receivingNo)), '') IS NULL
        BEGIN
            SET @Result = -3; SELECT @Result; RETURN;
        END

        INSERT INTO dbo.FinancialTransactions
            (HospId, BranchId, TnxType, TnxTypeId, GrossAmount, DiscountPercentage, DiscountAmount, TotalTaxAmount,
             RoundOff, NetAmount, Remarks, IsCancel, CreatedBy, CreatedOn, IpAddress, UniqueId, GSTType)
        VALUES
            (@hospId, @branchId, 'StockReceiving', 4, @grossAmount, @discountPercentage, @discountAmount, @gstAmount,
             @roundOff, @netAmount, @remarks, 0, @userId, GETDATE(), @ipAddress, CONVERT(nvarchar(200), NEWID()), @gstType);
        SET @ftId = SCOPE_IDENTITY();

        INSERT INTO dbo.StockReceiving
            (HospId, BranchId, RoleId, StoreType, VendorId, TypeId, [Type], ReceivingNo, ReceivingAgainstId,
             InvoiceNo, InvoiceDate, ChallanNo, ChallanDate, IsOnChallan, GSTType, GRNDate, PaymentMode, Remarks,
             GrossAmount, DiscountPercentage, DiscountAmount, TaxableAmount, GSTAmount, NetAmount, RoundOff,
             StatusId, IsCancel, FTID, FreightCharges, CreatedBy, CreatedOn, IpAddress)
        VALUES
            (@hospId, @branchId, @roleId, @storeType, @vendorId, 1, 'Direct Receiving', @receivingNo, @receivingAgainstId,
             NULLIF(@invoiceNo, ''), @invoiceDate, NULLIF(@challanNo, ''), @challanDate,
             CASE WHEN NULLIF(@challanNo, '') IS NULL THEN 0 ELSE 1 END, @gstType, @grnDate, @paymentMode, @remarks,
             @grossAmount, @discountPercentage, @discountAmount, @taxableAmount, @gstAmount, @netAmount, @roundOff,
             1, 0, @ftId, NULLIF(@freightCharges, 0), @userId, GETDATE(), @ipAddress);
        SET @receivingId = SCOPE_IDENTITY();

        UPDATE dbo.FinancialTransactions SET ReceivingId = @receivingId WHERE FTID = @ftId;
    END
    ELSE
    BEGIN
        /* only a Draft GRN of this hospital can be edited (Unpost a posted one first) */
        SELECT @ftId = FTID FROM dbo.StockReceiving
        WHERE Id = @receivingId AND HospId = @hospId AND StatusId = 1 AND IsCancel = 0;
        IF @ftId IS NULL
        BEGIN
            SET @Result = -2; SELECT @Result; RETURN;
        END

        UPDATE dbo.StockReceiving SET
            RoleId = @roleId, StoreType = @storeType, VendorId = @vendorId, ReceivingAgainstId = @receivingAgainstId,
            InvoiceNo = NULLIF(@invoiceNo, ''), InvoiceDate = @invoiceDate, ChallanNo = NULLIF(@challanNo, ''), ChallanDate = @challanDate,
            IsOnChallan = CASE WHEN NULLIF(@challanNo, '') IS NULL THEN 0 ELSE 1 END,
            GSTType = @gstType, GRNDate = @grnDate, PaymentMode = @paymentMode, Remarks = @remarks, AmendmentRemark = @amendmentRemark,
            FreightCharges = NULLIF(@freightCharges, 0),
            GrossAmount = @grossAmount, DiscountPercentage = @discountPercentage, DiscountAmount = @discountAmount,
            TaxableAmount = @taxableAmount, GSTAmount = @gstAmount, NetAmount = @netAmount, RoundOff = @roundOff,
            LastModifiedBy = @userId, LastModifiedOn = GETDATE(), IpAddress = @ipAddress
        WHERE Id = @receivingId;

        UPDATE dbo.FinancialTransactions SET
            GrossAmount = @grossAmount, DiscountPercentage = @discountPercentage, DiscountAmount = @discountAmount,
            TotalTaxAmount = @gstAmount, RoundOff = @roundOff, NetAmount = @netAmount, Remarks = @remarks, GSTType = @gstType,
            LastModifiedBy = @userId, LastModifiedOn = GETDATE(), IpAddress = @ipAddress
        WHERE FTID = @ftId;

        /* OLD U_CancelStockReceivingItems: previous lines are superseded, then re-written */
        UPDATE dbo.Stock SET IsActive = 3, LastModifiedBy = @userId, LastModifiedOn = GETDATE(), IpAddress = @ipAddress
        WHERE ReceivingId = @receivingId AND IsActive = 0;
        UPDATE dbo.FinancialTransactionDetails SET IsCancel = 1, CancelReason = 'GRN Updated',
            LastModifiedBy = @userId, LastModifiedOn = GETDATE(), IpAddress = @ipAddress
        WHERE FTID = @ftId AND ISNULL(IsCancel, 0) = 0;
    END

    /* lines */
    DECLARE @items TABLE
    (
        RowNo int IDENTITY(1,1), ItemId int, ItemName nvarchar(400), SubSubCategoryId int, Qty decimal(18,4), FreeQty decimal(18,4),
        Packing int, Deal1 int, Deal2 int, BatchNo nvarchar(100), ExpiryDate date, HsnCode nvarchar(50),
        Mrp decimal(18,4), Rate decimal(18,4), DiscPer decimal(18,4), DiscAmt decimal(18,4), SpclDiscPer decimal(18,4),
        SpclDiscAmt decimal(18,4), GstPer decimal(18,4), GstAmt decimal(18,4), GrossAmt decimal(18,4), NetAmt decimal(18,4),
        Units int, UnitCost decimal(18,6), Barcode nvarchar(100)
    );
    INSERT INTO @items (ItemId, ItemName, SubSubCategoryId, Qty, FreeQty, Packing, Deal1, Deal2, BatchNo, ExpiryDate, HsnCode,
                        Mrp, Rate, DiscPer, DiscAmt, SpclDiscPer, SpclDiscAmt, GstPer, GstAmt, GrossAmt, NetAmt, Units, UnitCost, Barcode)
    SELECT itemId, itemName, subSubCategoryId, qty, freeQty, packing, NULLIF(deal1, 0), NULLIF(deal2, 0), NULLIF(batchNo, ''),
           expiryDate, NULLIF(hsnCode, ''), mrp, rate, discPer, discAmt, spclDiscPer, spclDiscAmt, gstPer, gstAmt, grossAmt, netAmt,
           units, unitCost, NULLIF(LTRIM(RTRIM(barcode)), '')
    FROM OPENJSON(@itemsJson) WITH
    (
        itemId int, itemName nvarchar(400), subSubCategoryId int, qty decimal(18,4), freeQty decimal(18,4), packing int,
        deal1 int, deal2 int, batchNo nvarchar(100), expiryDate date, hsnCode nvarchar(50), mrp decimal(18,4), rate decimal(18,4),
        discPer decimal(18,4), discAmt decimal(18,4), spclDiscPer decimal(18,4), spclDiscAmt decimal(18,4), gstPer decimal(18,4),
        gstAmt decimal(18,4), grossAmt decimal(18,4), netAmt decimal(18,4), units int, unitCost decimal(18,6), barcode nvarchar(100)
    );

    DECLARE @i int = 1, @n int = (SELECT COUNT(*) FROM @items), @ftdId int;
    WHILE @i <= @n
    BEGIN
        INSERT INTO dbo.FinancialTransactionDetails
            (HospId, BranchId, FTID, ServiceItemId, SubSubCategoryId, ServiceName, Rate, Qty, GrossAmt,
             DiscPer, DiscAmt, SpecialDiscPer, SpecialDiscAmt, GSTPer, GSTAmt, TotalTaxPer, TotalTaxAmt, NetAmt,
             Deal1, Deal2, IsCancel, BillingDate, CreatedBy, CreatedOn, IpAddress, UniqueId)
        SELECT @hospId, @branchId, @ftId, ItemId, SubSubCategoryId, ItemName, Rate, Qty, GrossAmt,
               DiscPer, DiscAmt, SpclDiscPer, SpclDiscAmt, GstPer, GstAmt, GstPer, GstAmt, NetAmt,
               Deal1, Deal2, 0, @grnDate, @userId, GETDATE(), @ipAddress, CONVERT(nvarchar(200), NEWID())
        FROM @items WHERE RowNo = @i;
        SET @ftdId = SCOPE_IDENTITY();

        INSERT INTO dbo.Stock
            (HospId, ReceivingId, FTDID, BranchId, RoleId, ItemId, ItemName, CurrentStock, QTY, FreeQTY, IssuedQTY, ReturnedQTY,
             UnitCost, Rate, MRP, MajorMRP, BatchNo, ExpiryDate, HSNCode, Packing, GSTPercentage, IsActive, Barcode,
             CreatedBy, CreatedOn, IpAddress)
        SELECT @hospId, @receivingId, @ftdId, @branchId, @roleId, ItemId, ItemName, Units, Qty, FreeQty, 0, 0,
               UnitCost, Rate, Mrp / NULLIF(Packing, 0), Mrp, BatchNo, ExpiryDate, HsnCode, Packing, GstPer, 0, Barcode,
               @userId, GETDATE(), @ipAddress
        FROM @items WHERE RowNo = @i;

        UPDATE dbo.FinancialTransactionDetails SET StockId = SCOPE_IDENTITY() WHERE FTDId = @ftdId;
        SET @i += 1;
    END

    SET @Result = @receivingId;
    SELECT @Result;
END
GO

/* ---------- 2b. Post / Unpost / Cancel ----------
   Result: >0 GRN id | -1 stock already issued (unpost) | -2 wrong status or not found */
CREATE OR ALTER PROCEDURE dbo.U_PostStockReceiving
    @hospId int, @receivingId int, @userId int, @ipAddress nvarchar(50) = NULL, @Result int = NULL OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    IF NOT EXISTS (SELECT 1 FROM dbo.StockReceiving WHERE Id = @receivingId AND HospId = @hospId AND StatusId = 1 AND IsCancel = 0)
    BEGIN SET @Result = -2; SELECT @Result; RETURN; END

    UPDATE dbo.Stock SET IsActive = 1, LastModifiedBy = @userId, LastModifiedOn = GETDATE(), IpAddress = @ipAddress
    WHERE ReceivingId = @receivingId AND IsActive = 0;
    UPDATE dbo.StockReceiving SET StatusId = 2, PostedBy = @userId, PostedOn = GETDATE(),
        LastModifiedBy = @userId, LastModifiedOn = GETDATE(), IpAddress = @ipAddress
    WHERE Id = @receivingId;

    SET @Result = @receivingId; SELECT @Result;
END
GO

CREATE OR ALTER PROCEDURE dbo.U_UnPostStockReceiving
    @hospId int, @receivingId int, @userId int, @ipAddress nvarchar(50) = NULL, @Result int = NULL OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    IF NOT EXISTS (SELECT 1 FROM dbo.StockReceiving WHERE Id = @receivingId AND HospId = @hospId AND StatusId = 2 AND IsCancel = 0)
    BEGIN SET @Result = -2; SELECT @Result; RETURN; END

    /* OLD rule: refused once anything was issued (or returned) from this GRN */
    IF EXISTS (SELECT 1 FROM dbo.Stock WHERE ReceivingId = @receivingId AND IsActive = 1
               AND (IssuedQTY > 0 OR ReturnedQTY > 0 OR CurrentStock < (QTY + ISNULL(FreeQTY, 0)) * Packing))
    BEGIN SET @Result = -1; SELECT @Result; RETURN; END

    UPDATE dbo.Stock SET IsActive = 0, LastModifiedBy = @userId, LastModifiedOn = GETDATE(), IpAddress = @ipAddress
    WHERE ReceivingId = @receivingId AND IsActive = 1;
    UPDATE dbo.StockReceiving SET StatusId = 1, PostedBy = NULL, PostedOn = NULL,
        LastModifiedBy = @userId, LastModifiedOn = GETDATE(), IpAddress = @ipAddress
    WHERE Id = @receivingId;

    SET @Result = @receivingId; SELECT @Result;
END
GO

CREATE OR ALTER PROCEDURE dbo.U_CancelStockReceiving
    @hospId int, @receivingId int, @reason nvarchar(1000), @userId int, @ipAddress nvarchar(50) = NULL, @Result int = NULL OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @ftId int;
    SELECT @ftId = FTID FROM dbo.StockReceiving WHERE Id = @receivingId AND HospId = @hospId AND StatusId = 1 AND IsCancel = 0;
    IF @ftId IS NULL
    BEGIN SET @Result = -2; SELECT @Result; RETURN; END

    UPDATE dbo.Stock SET IsActive = 2, LastModifiedBy = @userId, LastModifiedOn = GETDATE(), IpAddress = @ipAddress
    WHERE ReceivingId = @receivingId AND IsActive = 0;
    UPDATE dbo.FinancialTransactions SET IsCancel = 1, Remarks = @reason, LastModifiedBy = @userId, LastModifiedOn = GETDATE(), IpAddress = @ipAddress
    WHERE FTID = @ftId;
    UPDATE dbo.FinancialTransactionDetails SET IsCancel = 1, CancelReason = @reason, LastModifiedBy = @userId, LastModifiedOn = GETDATE(), IpAddress = @ipAddress
    WHERE FTID = @ftId AND ISNULL(IsCancel, 0) = 0;
    UPDATE dbo.StockReceiving SET StatusId = 3, IsCancel = 1, CancelReason = @reason, CancelledBy = @userId, CancelledOn = GETDATE(),
        LastModifiedBy = @userId, LastModifiedOn = GETDATE(), IpAddress = @ipAddress
    WHERE Id = @receivingId;

    SET @Result = @receivingId; SELECT @Result;
END
GO

/* ---------- 2c. Reads ---------- */
CREATE OR ALTER PROCEDURE dbo.S_GetStockReceivingList
    @hospId int, @branchId int, @fromDate date = NULL, @toDate date = NULL,
    @vendorId int = NULL, @statusId int = NULL, @search nvarchar(100) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET @search = NULLIF(LTRIM(RTRIM(@search)), '');
    SELECT TOP 500
        sr.Id AS ReceivingId, sr.ReceivingNo, sr.GRNDate, sr.StoreType, sr.RoleId, LTRIM(RTRIM(rm.RoleName)) AS StoreName,
        sr.VendorId, vm.VendorName, sr.InvoiceNo, sr.InvoiceDate, sr.ChallanNo, sr.ChallanDate,
        sr.NetAmount, sr.StatusId,
        CASE sr.StatusId WHEN 1 THEN 'Draft' WHEN 2 THEN 'Posted' WHEN 3 THEN 'Cancelled' END AS Status,
        (SELECT COUNT(*) FROM dbo.Stock s WHERE s.ReceivingId = sr.Id AND s.IsActive IN (0, 1, 2)) AS ItemCount,
        ISNULL(um.Name, '') AS CreatedByName, sr.CreatedOn
    FROM dbo.StockReceiving sr
    LEFT JOIN dbo.VendorMaster vm ON vm.VendorId = sr.VendorId
    LEFT JOIN dbo.RoleMaster rm   ON rm.RoleId = sr.RoleId
    LEFT JOIN dbo.UserMaster um   ON um.Id = sr.CreatedBy
    WHERE sr.HospId = @hospId AND sr.BranchId = @branchId
      AND (@fromDate IS NULL OR sr.GRNDate >= @fromDate)
      AND (@toDate   IS NULL OR sr.GRNDate <= @toDate)
      AND (@vendorId IS NULL OR sr.VendorId = @vendorId)
      AND (@statusId IS NULL OR sr.StatusId = @statusId)
      AND (@search IS NULL OR sr.ReceivingNo LIKE '%' + @search + '%' OR sr.InvoiceNo LIKE '%' + @search + '%'
           OR sr.ChallanNo LIKE '%' + @search + '%' OR vm.VendorName LIKE '%' + @search + '%')
    ORDER BY sr.Id DESC;
END
GO

CREATE OR ALTER PROCEDURE dbo.S_GetStockReceivingDetails
    @hospId int, @receivingId int
AS
BEGIN
    SET NOCOUNT ON;
    /* 1. header */
    SELECT sr.Id AS ReceivingId, sr.BranchId, sr.RoleId, LTRIM(RTRIM(rm.RoleName)) AS StoreName, sr.StoreType, sr.VendorId, vm.VendorName,
           vm.GSTINNo, vm.DLNO, sr.TypeId, sr.[Type], sr.ReceivingNo, sr.ReceivingAgainstId, sr.InvoiceNo, sr.InvoiceDate,
           sr.ChallanNo, sr.ChallanDate, sr.GSTType, sr.GRNDate, sr.PaymentMode, sr.Remarks, sr.AmendmentRemark,
           ISNULL(sr.FreightCharges, 0) AS FreightCharges,
           sr.GrossAmount, sr.DiscountPercentage, sr.DiscountAmount, sr.TaxableAmount, sr.GSTAmount, sr.NetAmount, sr.RoundOff,
           sr.StatusId, CASE sr.StatusId WHEN 1 THEN 'Draft' WHEN 2 THEN 'Posted' WHEN 3 THEN 'Cancelled' END AS Status,
           sr.CancelReason, sr.CancelledOn, sr.PostedOn,
           CAST(CASE WHEN EXISTS (SELECT 1 FROM dbo.Stock s WHERE s.ReceivingId = sr.Id AND s.IsActive = 1
                                  AND (s.IssuedQTY > 0 OR s.ReturnedQTY > 0)) THEN 1 ELSE 0 END AS int) AS IsIssued
    FROM dbo.StockReceiving sr
    LEFT JOIN dbo.VendorMaster vm ON vm.VendorId = sr.VendorId
    LEFT JOIN dbo.RoleMaster rm   ON rm.RoleId = sr.RoleId
    WHERE sr.Id = @receivingId AND sr.HospId = @hospId;

    /* 2. lines (current lots: not superseded by an edit) */
    SELECT s.StockId, s.ItemId, s.ItemName, ISNULL(sim.Code, '') AS ItemCode, ISNULL(ftd.SubSubCategoryId, sim.SubSubCategoryId) AS SubSubCategoryId,
           ISNULL(sim.Expirable, 0) AS Expirable, s.QTY AS Qty, ISNULL(s.FreeQTY, 0) AS FreeQty, s.Packing,
           ISNULL(ftd.Deal1, 0) AS Deal1, ISNULL(ftd.Deal2, 0) AS Deal2, s.BatchNo, s.ExpiryDate, s.HSNCode,
           s.MajorMRP AS Mrp, s.Rate, ISNULL(ftd.DiscPer, 0) AS DiscPer, ISNULL(ftd.DiscAmt, 0) AS DiscAmt,
           ISNULL(ftd.SpecialDiscPer, 0) AS SpclDiscPer, ISNULL(ftd.SpecialDiscAmt, 0) AS SpclDiscAmt,
           s.GSTPercentage AS GstPer, ISNULL(ftd.GSTAmt, 0) AS GstAmt, ISNULL(ftd.GrossAmt, 0) AS GrossAmt,
           ISNULL(ftd.NetAmt, 0) AS NetAmt, s.CurrentStock AS Units, s.UnitCost, s.IssuedQTY, ISNULL(s.Barcode, '') AS Barcode,
           (SELECT TOP 1 p.Rate FROM dbo.Stock p WHERE p.HospId = s.HospId AND p.ItemId = s.ItemId AND p.IsActive = 1
              AND p.StockId < s.StockId AND ISNULL(p.ReceivingId, 0) <> s.ReceivingId ORDER BY p.StockId DESC) AS OldRate
    FROM dbo.Stock s
    LEFT JOIN dbo.FinancialTransactionDetails ftd ON ftd.FTDId = s.FTDID
    LEFT JOIN dbo.ServiceItemMaster sim ON sim.ServiceItemId = s.ItemId
    WHERE s.ReceivingId = @receivingId AND s.HospId = @hospId AND s.IsActive IN (0, 1, 2)
    ORDER BY s.StockId;
END
GO

/* item search for the Item Entry panel: store items of the store type with the last purchase rate / MRP */
CREATE OR ALTER PROCEDURE dbo.S_SearchStoreItemForReceiving
    @hospId int, @categoryId int, @search nvarchar(100) = NULL, @top int = 30,
    @itemIds nvarchar(max) = NULL          -- comma-separated ids: the API checks the lines of a save with it
AS
BEGIN
    SET NOCOUNT ON;
    SET @search = NULLIF(LTRIM(RTRIM(@search)), '');
    SELECT TOP (@top)
        sim.ServiceItemId AS ItemId, sim.Name AS ItemName, ISNULL(sim.Code, '') AS ItemCode, sim.SubSubCategoryId,
        ISNULL(sim.Packing, 1) AS Packing, ISNULL(sim.HSNCode, '') AS HsnCode, sim.GSTPer AS GstPer,
        ISNULL(sim.Expirable, 0) AS Expirable, ISNULL(sim.PurchaseUnitName, '') AS PurchaseUnitName,
        ISNULL(sim.SaleUnitName, '') AS SaleUnitName, ISNULL(mm.ManufacturerName, '') AS ManufacturerName,
        CASE WHEN ISNULL(sim.StatusId, 0) = 1 THEN ISNULL(NULLIF(sim.StatusType, ''), 'Hold') ELSE '' END AS HoldStatus,
        ISNULL(sim.MedicineHoldRemorks, '') AS HoldRemarks,
        lp.Rate AS LastRate, lp.MajorMRP AS LastMrp
    FROM dbo.ServiceItemMaster sim
    LEFT JOIN dbo.ManufacturerMaster mm ON mm.ManufacturerId = sim.ManufacturerId
    OUTER APPLY (SELECT TOP 1 s.Rate, s.MajorMRP FROM dbo.Stock s
                 WHERE s.HospId = @hospId AND s.ItemId = sim.ServiceItemId AND s.IsActive = 1
                 ORDER BY s.StockId DESC) lp
    WHERE sim.CategoryId = @categoryId AND sim.IsActive = 1
      AND (@itemIds IS NULL OR sim.ServiceItemId IN (SELECT TRY_CAST(value AS int) FROM STRING_SPLIT(@itemIds, ',')))
      AND (@search IS NULL OR sim.Name LIKE '%' + @search + '%' OR sim.Code LIKE @search + '%'
           OR CAST(sim.ServiceItemId AS nvarchar(20)) = @search)
    ORDER BY CASE WHEN sim.Name LIKE @search + '%' THEN 0 ELSE 1 END, sim.Name;
END
GO

/* stores (= roles) the user has in the branch */
CREATE OR ALTER PROCEDURE dbo.S_GetStockReceivingStores
    @hospId int, @branchId int, @userId int
AS
BEGIN
    SET NOCOUNT ON;
    SELECT DISTINCT rm.RoleId, LTRIM(RTRIM(rm.RoleName)) AS RoleName
    FROM dbo.UserRoleMapping urm
    INNER JOIN dbo.RoleMaster rm ON rm.RoleId = urm.RoleId AND rm.IsActive = 1
    WHERE urm.UserId = @userId AND urm.BranchId = @branchId AND urm.IsActive = 1
    ORDER BY RoleName;
END
GO

/* ---------- 3. GRN number sequences (all branches, all roles) ---------- */
DECLARE @seq TABLE (TypeId int, TypeName nvarchar(100), Name nvarchar(50), Prefix nvarchar(10));
INSERT INTO @seq VALUES (30, 'Medical Stock Receiving', 'GRN', 'GRN'),
                        (31, 'General Stock Receiving', 'GGRN', 'GGRN'),
                        (34, 'Blood Bank Inventory', 'BGRN', 'BGRN');
DECLARE @t int, @tn nvarchar(100), @nm nvarchar(50), @pf nvarchar(10), @sid int;
DECLARE c CURSOR LOCAL FAST_FORWARD FOR SELECT TypeId, TypeName, Name, Prefix FROM @seq;
OPEN c; FETCH NEXT FROM c INTO @t, @tn, @nm, @pf;
WHILE @@FETCH_STATUS = 0
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.BranchSequenceMapping WHERE TypeId = @t)
    BEGIN
        SELECT TOP 1 @sid = SequenceId FROM dbo.SequenceMaster WHERE TypeId = @t AND Name = @nm;
        IF @sid IS NULL
        BEGIN
            INSERT INTO dbo.SequenceMaster (HospId, Name, TypeId, TypeName, Prefix, FirstSeprator, FYFormatId, FYFormat,
                                            SecondSeprator, Length, Preview, IsActive, CreatedBy, CreatedOn, IpAddress, UniqueId)
            VALUES (1, @nm, @t, @tn, @pf, '/', 1, '2', '/', 6, @pf + '/26-27/000001', 1, 1, GETDATE(), '127.0.0.1', CONVERT(nvarchar(50), NEWID()));
            SET @sid = SCOPE_IDENTITY();
        END
        INSERT INTO dbo.BranchSequenceMapping (BranchId, RoleId, TypeId, SequenceId, CreatedBy, CreatedOn, IpAddress, UniqueId)
        VALUES (0, 0, @t, @sid, 1, GETDATE(), '127.0.0.1', CONVERT(nvarchar(50), NEWID()));
    END
    SET @sid = NULL;
    FETCH NEXT FROM c INTO @t, @tn, @nm, @pf;
END
CLOSE c; DEALLOCATE c;

/* owner 2026-11-14: GRN number counter is 6 digits (GRN/26-27/000001) - also fixes rows seeded earlier with 4 */
UPDATE dbo.SequenceMaster
SET Length = 6, Preview = Prefix + '/26-27/000001', LastModifiedBy = 1, LastModifiedOn = GETDATE()
WHERE TypeId IN (30, 31, 34) AND Name IN ('GRN', 'GGRN', 'BGRN') AND ISNULL(Length, 0) <> 6;
GO

/* ---------- 4. Menu row under tab 'Purchase' + role mapping ---------- */
DECLARE @tabId int = (SELECT TOP 1 TabId FROM dbo.NavigationTabMaster WHERE LTRIM(RTRIM(TabName)) = 'Purchase' AND IsActive = 1);
IF @tabId IS NULL
    PRINT 'Tab "Purchase" not found - add "Stock Receiving" (stock-receiving) from the Navigation Pane screen.';
ELSE
BEGIN
    IF EXISTS (SELECT 1 FROM dbo.NavigationSubMenuMaster WHERE URL = 'stock-receiving')
        UPDATE dbo.NavigationSubMenuMaster SET TabId = @tabId, SubMenuName = 'Stock Receiving', IsActive = 1
        WHERE URL = 'stock-receiving';
    ELSE
        INSERT INTO dbo.NavigationSubMenuMaster (TabId, SubMenuName, HospId, IsActive, URL, CreatedOn, CreatedBy, IpAddress, SequenceNo)
        VALUES (@tabId, 'Stock Receiving', 1, 1, 'stock-receiving', GETDATE(), 1, '127.0.0.1', 10);

    DECLARE @subMenuId int = (SELECT TOP 1 SubMenuId FROM dbo.NavigationSubMenuMaster WHERE URL = 'stock-receiving');

    INSERT INTO dbo.RoleWiseMenuMappingMaster (HospId, BranchId, RoleId, SubMenuId, CreatedBy, CreatedOn, IpAddress)
    SELECT b.HospId, b.BranchId, r.RoleId, @subMenuId, 1, GETDATE(), '127.0.0.1'
    FROM dbo.BranchMaster b
    CROSS JOIN (VALUES (2), (5), (15)) r(RoleId)
    WHERE b.IsActive = 1
      AND EXISTS (SELECT 1 FROM dbo.RoleMaster rm WHERE rm.RoleId = r.RoleId)
      AND NOT EXISTS (SELECT 1 FROM dbo.RoleWiseMenuMappingMaster m
                      WHERE m.BranchId = b.BranchId AND m.RoleId = r.RoleId AND m.SubMenuId = @subMenuId);
END
GO
