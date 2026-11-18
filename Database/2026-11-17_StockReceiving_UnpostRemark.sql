/* =====================================================================
   Medical Stock Receiving (GRN) - required remark on Unpost          2026-11-17
   Database : GWT_HIS_API          Re-runnable (IF COL_LENGTH / CREATE OR ALTER)
   Needs    : 2026-11-14_StockReceiving.sql and 2026-11-16_StockReceiving_FreightCharges.sql applied first.

   Owner rule: unposting a GRN needs a remark typed by the user.
   OLD HIS had no remark on unpost (U_UnPostStockReceiving took only ReceivingId).
   The latest unpost is kept on the GRN (an earlier remark is replaced when it is unposted again).

     1. dbo.StockReceiving.UnPostRemark nvarchar(1000) NULL, UnPostedBy int NULL, UnPostedOn datetime NULL
     2. U_UnPostStockReceiving      + @remark nvarchar(1000) (required: blank -> @Result -3)
     3. S_GetStockReceivingDetails  returns UnPostRemark, UnPostedOn, UnPostedByName
   ===================================================================== */

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF COL_LENGTH('dbo.StockReceiving', 'UnPostRemark') IS NULL
    ALTER TABLE dbo.StockReceiving ADD UnPostRemark nvarchar(1000) NULL;
IF COL_LENGTH('dbo.StockReceiving', 'UnPostedBy') IS NULL
    ALTER TABLE dbo.StockReceiving ADD UnPostedBy int NULL;
IF COL_LENGTH('dbo.StockReceiving', 'UnPostedOn') IS NULL
    ALTER TABLE dbo.StockReceiving ADD UnPostedOn datetime NULL;
GO

CREATE OR ALTER PROCEDURE dbo.U_UnPostStockReceiving
    @hospId int, @receivingId int, @remark nvarchar(1000) = NULL, @userId int, @ipAddress nvarchar(50) = NULL, @Result int = NULL OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET @remark = NULLIF(LTRIM(RTRIM(@remark)), '');
    IF @remark IS NULL
    BEGIN SET @Result = -3; SELECT @Result; RETURN; END

    IF NOT EXISTS (SELECT 1 FROM dbo.StockReceiving WHERE Id = @receivingId AND HospId = @hospId AND StatusId = 2 AND IsCancel = 0)
    BEGIN SET @Result = -2; SELECT @Result; RETURN; END

    /* OLD rule: refused once anything was issued (or returned) from this GRN */
    IF EXISTS (SELECT 1 FROM dbo.Stock WHERE ReceivingId = @receivingId AND IsActive = 1
               AND (IssuedQTY > 0 OR ReturnedQTY > 0 OR CurrentStock < (QTY + ISNULL(FreeQTY, 0)) * Packing))
    BEGIN SET @Result = -1; SELECT @Result; RETURN; END

    UPDATE dbo.Stock SET IsActive = 0, LastModifiedBy = @userId, LastModifiedOn = GETDATE(), IpAddress = @ipAddress
    WHERE ReceivingId = @receivingId AND IsActive = 1;
    UPDATE dbo.StockReceiving SET StatusId = 1, PostedBy = NULL, PostedOn = NULL,
        UnPostRemark = @remark, UnPostedBy = @userId, UnPostedOn = GETDATE(),
        LastModifiedBy = @userId, LastModifiedOn = GETDATE(), IpAddress = @ipAddress
    WHERE Id = @receivingId;

    SET @Result = @receivingId; SELECT @Result;
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
           sr.UnPostRemark, sr.UnPostedOn, ISNULL(uu.Name, '') AS UnPostedByName,
           CAST(CASE WHEN EXISTS (SELECT 1 FROM dbo.Stock s WHERE s.ReceivingId = sr.Id AND s.IsActive = 1
                                  AND (s.IssuedQTY > 0 OR s.ReturnedQTY > 0)) THEN 1 ELSE 0 END AS int) AS IsIssued
    FROM dbo.StockReceiving sr
    LEFT JOIN dbo.VendorMaster vm ON vm.VendorId = sr.VendorId
    LEFT JOIN dbo.RoleMaster rm   ON rm.RoleId = sr.RoleId
    LEFT JOIN dbo.UserMaster uu   ON uu.Id = sr.UnPostedBy
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
