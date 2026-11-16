/* =====================================================================
   Stock Receiving (GRN) - item-wise previous purchase history        2026-11-16
   Database : GWT_HIS_API          Re-runnable (CREATE OR ALTER)
   Needs    : 2026-11-14_StockReceiving.sql (tables StockReceiving, Stock)

   S_GetItemPurchaseHistory  - port of OLD S_getLastItemPurchaseHistory (popup "Last Purchase History"
                               opened from the icon beside the item search on the GRN page).
   Differences from OLD (Recommended):
     * scoped by hospital; optional @excludeReceivingId (the GRN being entered is left out)
     * only current lots: rows replaced by a GRN edit (Stock.IsActive 3) and cancelled GRNs are left out
       (OLD showed the replaced rows too, so an edited GRN appeared twice)
     * also returns GRN Id / status / GRN date / disc % so the screen can open the GRN; dates stay date-typed
   ===================================================================== */

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- barcode column (also in 2026-11-14_StockReceiving.sql / 2026-11-16_StockReceiving_Barcode.sql) - needed by the history below
IF COL_LENGTH('dbo.Stock', 'Barcode') IS NULL
    ALTER TABLE dbo.Stock ADD Barcode nvarchar(100) NULL;
GO

CREATE OR ALTER PROCEDURE dbo.S_GetItemPurchaseHistory
    @hospId              int,
    @itemId              int,
    @excludeReceivingId  int = NULL,
    @top                 int = 100
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (@top)
        sr.Id AS ReceivingId, sr.ReceivingNo, sr.GRNDate, sr.InvoiceDate, sr.InvoiceNo, sr.ChallanNo,
        sr.StatusId, CASE sr.StatusId WHEN 1 THEN 'Draft' WHEN 2 THEN 'Posted' WHEN 3 THEN 'Cancelled' END AS Status,
        sr.StoreType, ISNULL(vm.VendorName, '') AS VendorName,
        st.ItemId, st.ItemName, st.Packing, ISNULL(st.FreeQTY, 0) AS FreeQty, st.BatchNo, st.ExpiryDate, st.QTY AS Qty,
        st.MRP, st.MajorMRP, st.Rate, st.UnitCost, st.GSTPercentage AS GstPer,
        ISNULL(ftd.DiscAmt, 0) AS DiscAmt, ISNULL(ftd.DiscPer, 0) AS DiscPer, ISNULL(st.Barcode, '') AS Barcode
    FROM dbo.Stock st
    INNER JOIN dbo.StockReceiving sr ON sr.Id = st.ReceivingId AND sr.TypeId = 1 AND sr.IsCancel = 0
    LEFT  JOIN dbo.FinancialTransactionDetails ftd ON ftd.FTDId = st.FTDID
    LEFT  JOIN dbo.VendorMaster vm ON vm.VendorId = sr.VendorId
    WHERE st.HospId = @hospId AND st.ItemId = @itemId AND st.IsActive IN (0, 1)
      AND (@excludeReceivingId IS NULL OR sr.Id <> @excludeReceivingId)
    ORDER BY ISNULL(sr.InvoiceDate, sr.GRNDate) DESC, sr.Id DESC, st.StockId DESC;
END
GO
