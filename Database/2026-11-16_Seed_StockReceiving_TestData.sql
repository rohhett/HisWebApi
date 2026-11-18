/* =====================================================================
   TEST DATA ONLY - 5 Medical Stock Receiving (GRN) transactions          2026-11-16
   Database : GWT_HIS_API          Re-runnable (skips a GRN whose test invoice already exists)
   Needs    : 2026-11-14_StockReceiving.sql + 2026-11-16_StockReceiving_FreightCharges.sql

   Do NOT run on a live / customer database: it creates real GRNs, stock and financial rows.

   Saves through dbo.IU_StockReceiving (same path as the screen), amounts computed like grnCalc.ts:
     gross = qty * rate, disc = gross * discPer%, gst = (gross - disc) * gstPer%, net = taxable + gst,
     units = (qty + free) * packing, unitCost = net / units, NetAmount = ROUND(sum net, 0), RoundOff = difference

     #  Invoice        Vendor  Items  Status
     1  TEST-INV-1001  1       20     Posted
     2  TEST-INV-1002  2       10     Posted
     3  TEST-INV-1003  3        5     Posted
     4  TEST-INV-1004  4       10     Draft
     5  TEST-INV-1005  5        5     Draft

   Store: HospId 1, BranchId 1, RoleId 5 (Pharmacy), StoreType Medical (CategoryId 6), UserId 2.
   Change the variables below for another server.
   ===================================================================== */

SET NOCOUNT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;

DECLARE @hospId int = 1, @branchId int = 1, @roleId int = 5, @userId int = 2, @categoryId int = 6;

DECLARE @grns TABLE (No int, InvoiceNo nvarchar(100), VendorId int, ItemCount int, ItemOffset int, Post bit, FreightCharges decimal(18,2));
INSERT INTO @grns VALUES
    (1, 'TEST-INV-1001', 1, 20,  0, 1, 250),
    (2, 'TEST-INV-1002', 2, 10, 20, 1,   0),
    (3, 'TEST-INV-1003', 3,  5, 30, 1, 120),
    (4, 'TEST-INV-1004', 4, 10, 35, 0,   0),
    (5, 'TEST-INV-1005', 5,  5, 45, 0,  80);

/* active, not-on-hold medical items in a fixed order */
DECLARE @pool TABLE (Rn int, ItemId int, ItemName nvarchar(400), SubSubCategoryId int, Packing int, HsnCode nvarchar(50), GstPer decimal(18,4));
INSERT INTO @pool
SELECT ROW_NUMBER() OVER (ORDER BY ServiceItemId), ServiceItemId, Name, SubSubCategoryId,
       CASE WHEN ISNULL(Packing, 0) > 0 THEN Packing ELSE 1 END, ISNULL(HSNCode, ''), ISNULL(GSTPer, 12)
FROM dbo.ServiceItemMaster
WHERE CategoryId = @categoryId AND IsActive = 1 AND ISNULL(StatusId, 0) <> 1;

DECLARE @poolCount int = (SELECT COUNT(*) FROM @pool);
IF @poolCount = 0
BEGIN
    RAISERROR('No active Medical items found in ServiceItemMaster - nothing created.', 16, 1);
    RETURN;
END

DECLARE @no int = 1;
WHILE @no <= 5
BEGIN
    DECLARE @invoiceNo nvarchar(100), @vendorId int, @itemCount int, @offset int, @post bit, @freight decimal(18,2);
    SELECT @invoiceNo = InvoiceNo, @vendorId = VendorId, @itemCount = ItemCount, @offset = ItemOffset, @post = Post, @freight = FreightCharges
    FROM @grns WHERE No = @no;

    IF EXISTS (SELECT 1 FROM dbo.StockReceiving WHERE HospId = @hospId AND VendorId = @vendorId AND InvoiceNo = @invoiceNo AND IsCancel = 0)
    BEGIN
        PRINT CONCAT('GRN ', @no, ' (', @invoiceNo, ') already exists - skipped');
        SET @no += 1;
        CONTINUE;
    END

    /* lines: deterministic qty / rate / discount per row so the numbers look varied but repeat on every run */
    DECLARE @lines TABLE
    (
        Ln int, ItemId int, ItemName nvarchar(400), SubSubCategoryId int, Qty decimal(18,4), FreeQty decimal(18,4), Packing int,
        BatchNo nvarchar(100), ExpiryDate date, HsnCode nvarchar(50), Mrp decimal(18,4), Rate decimal(18,4), DiscPer decimal(18,4),
        DiscAmt decimal(18,4), GstPer decimal(18,4), GstAmt decimal(18,4), GrossAmt decimal(18,4), NetAmt decimal(18,4),
        Units int, UnitCost decimal(18,6)
    );
    DELETE FROM @lines;

    INSERT INTO @lines (Ln, ItemId, ItemName, SubSubCategoryId, Qty, FreeQty, Packing, BatchNo, ExpiryDate, HsnCode, Mrp, Rate, DiscPer, GstPer)
    SELECT k.Ln, p.ItemId, p.ItemName, p.SubSubCategoryId,
           5 + (k.Ln * 7 + @no) % 20,                                   -- qty 5..24
           CASE WHEN k.Ln % 4 = 0 THEN 1 ELSE 0 END,                    -- every 4th line has 1 free
           p.Packing,
           CONCAT('TB', @no, RIGHT('00' + CAST(k.Ln AS varchar(3)), 3)),
           DATEADD(MONTH, 12 + (k.Ln % 12), CAST(GETDATE() AS date)),
           p.HsnCode,
           ROUND((20 + (p.ItemId % 50) * 3.5) * 1.4, 0),                   -- MRP per pack
           20 + (p.ItemId % 50) * 3.5,                                       -- purchase rate per pack
           CASE WHEN k.Ln % 3 = 0 THEN 5 ELSE 0 END,                     -- every 3rd line 5% discount
           p.GstPer
    FROM (SELECT TOP (@itemCount) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS Ln FROM sys.all_objects) k
    JOIN @pool p ON p.Rn = ((@offset + k.Ln - 1) % @poolCount) + 1;

    UPDATE @lines SET GrossAmt = ROUND(Qty * Rate, 2);
    UPDATE @lines SET DiscAmt = ROUND(GrossAmt * DiscPer / 100, 2);
    UPDATE @lines SET GstAmt = ROUND((GrossAmt - DiscAmt) * GstPer / 100, 2);
    UPDATE @lines SET NetAmt = GrossAmt - DiscAmt + GstAmt, Units = (Qty + FreeQty) * Packing;
    UPDATE @lines SET UnitCost = NetAmt / NULLIF(Units, 0);

    DECLARE @gross decimal(18,4), @disc decimal(18,4), @gst decimal(18,4), @net decimal(18,4), @pay decimal(18,4);
    SELECT @gross = SUM(GrossAmt), @disc = SUM(DiscAmt), @gst = SUM(GstAmt), @net = SUM(NetAmt) FROM @lines;
    SET @pay = ROUND(@net, 0);

    DECLARE @itemsJson nvarchar(max) =
    (
        SELECT ItemId AS itemId, ItemName AS itemName, SubSubCategoryId AS subSubCategoryId, Qty AS qty, FreeQty AS freeQty,
               Packing AS packing, 0 AS deal1, 0 AS deal2, BatchNo AS batchNo, CONVERT(varchar(10), ExpiryDate, 23) AS expiryDate,
               HsnCode AS hsnCode, Mrp AS mrp, Rate AS rate, DiscPer AS discPer, DiscAmt AS discAmt, 0 AS spclDiscPer,
               0 AS spclDiscAmt, GstPer AS gstPer, GstAmt AS gstAmt, GrossAmt AS grossAmt, NetAmt AS netAmt,
               Units AS units, UnitCost AS unitCost, '' AS barcode
        FROM @lines ORDER BY Ln
        FOR JSON PATH
    );

    DECLARE @grnDate date = DATEADD(DAY, @no - 5, CAST(GETDATE() AS date));   -- last 5 days, one per GRN
    DECLARE @remark nvarchar(1000) = CONCAT('Test GRN ', @no, ' - ', @itemCount, ' items (seed script)');
    DECLARE @taxable decimal(18,4) = @gross - @disc, @roundOff decimal(18,4) = @pay - @net;
    DECLARE @receivingId int;
    DECLARE @out TABLE (Result int);
    DELETE FROM @out;

    INSERT INTO @out
    EXEC dbo.IU_StockReceiving
        @hospId = @hospId, @branchId = @branchId, @roleId = @roleId, @receivingId = 0, @storeType = 'Medical',
        @vendorId = @vendorId, @receivingAgainstId = 1, @invoiceNo = @invoiceNo, @invoiceDate = @grnDate,
        @challanNo = NULL, @challanDate = NULL, @gstType = 'CGST & SGST', @grnDate = @grnDate, @paymentMode = 'Credit',
        @remarks = @remark, @amendmentRemark = NULL, @freightCharges = @freight,
        @grossAmount = @gross, @discountPercentage = 0, @discountAmount = @disc, @taxableAmount = @taxable,
        @gstAmount = @gst, @netAmount = @pay, @roundOff = @roundOff,
        @itemsJson = @itemsJson, @userId = @userId, @ipAddress = 'seed-script';
    SELECT @receivingId = Result FROM @out;

    IF ISNULL(@receivingId, 0) <= 0
    BEGIN
        PRINT CONCAT('GRN ', @no, ' (', @invoiceNo, ') failed, IU_StockReceiving returned ', @receivingId);
        SET @no += 1;
        CONTINUE;
    END

    IF @post = 1
    BEGIN
        DELETE FROM @out;
        INSERT INTO @out EXEC dbo.U_PostStockReceiving @hospId = @hospId, @receivingId = @receivingId, @userId = @userId, @ipAddress = 'seed-script';
    END

    PRINT CONCAT('GRN ', @no, ' created: ReceivingId ', @receivingId, ', ', @itemCount, ' items, net ', @pay,
                 CASE WHEN @post = 1 THEN ', Posted' ELSE ', Draft' END);
    SET @no += 1;
END

SELECT sr.Id AS ReceivingId, sr.ReceivingNo, sr.InvoiceNo, vm.VendorName, sr.GRNDate,
       (SELECT COUNT(*) FROM dbo.Stock s WHERE s.ReceivingId = sr.Id AND s.IsActive IN (0, 1)) AS Items,
       sr.NetAmount, sr.FreightCharges, CASE sr.StatusId WHEN 1 THEN 'Draft' WHEN 2 THEN 'Posted' ELSE 'Cancelled' END AS Status
FROM dbo.StockReceiving sr
LEFT JOIN dbo.VendorMaster vm ON vm.VendorId = sr.VendorId
WHERE sr.HospId = @hospId AND sr.InvoiceNo LIKE 'TEST-INV-10%'
ORDER BY sr.Id;
