/* =====================================================================
   Medical Stock Receiving (GRN) - Freight Charges                    2026-11-16
   Database : GWT_HIS_API          Re-runnable (IF COL_LENGTH / CREATE OR ALTER)
   Needs    : 2026-11-14_StockReceiving.sql applied first (that script now contains the same change).

   Owner rule: Freight Charges (transport / "bhada") is stored on dbo.StockReceiving ONLY.
     * not added to Net Payable / NetAmount, not spread into item unit cost
     * not written to FinancialTransactions, FinancialTransactionDetails, Stock or any other table
     * 0 or blank is saved as NULL

     1. dbo.StockReceiving.FreightCharges decimal(18,2) NULL
     2. IU_StockReceiving          + @freightCharges decimal(18,2) = NULL (default keeps older calls working)
     3. S_GetStockReceivingDetails  returns FreightCharges (0 when NULL)
   ===================================================================== */

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF COL_LENGTH('dbo.StockReceiving', 'FreightCharges') IS NULL
    ALTER TABLE dbo.StockReceiving ADD FreightCharges decimal(18,2) NULL;
GO

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

