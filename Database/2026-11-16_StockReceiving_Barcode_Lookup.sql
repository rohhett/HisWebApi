/* =====================================================================
   Stock Receiving (GRN) - barcode lookup                             2026-11-16
   Database : GWT_HIS_API          Re-runnable (CREATE OR ALTER)
   Run order: 2026-11-14_StockReceiving.sql (adds Stock.Barcode, saves it in IU_StockReceiving,
              returns it in S_GetStockReceivingDetails) -> 2026-11-16_StockReceiving_ItemPurchaseHistory.sql
              -> this script.

   Owner rules (2026-11-16):
     * the barcode scanned on a GRN line is saved in dbo.Stock.Barcode (per lot) only;
       it is NEVER written to ServiceItemMaster.
     * lookup order: 1) earlier GRN lots with that barcode (newest first)
                     2) ServiceItemMaster.Barcode, read only (filled by hand in Store Item Master, if ever)
       Not found -> the screen searches the text as item name / code, then asks to pick the item by name;
       the barcode is saved with that line, so the next scan finds it.
     * a GS1 2D code is parsed on the screen; the GTIN (AI 01) is what is looked up and saved.
       Leading zeros are ignored when matching (EAN-13 8901234567890 = GTIN-14 08901234567890).

   S_GetItemByBarcode: one row per matching active item of the store type, same columns as
   S_SearchStoreItemForReceiving plus Source ('GRN' / 'Item Master') and LastBarcodeOn.
   ===================================================================== */

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF COL_LENGTH('dbo.Stock', 'Barcode') IS NULL
    ALTER TABLE dbo.Stock ADD Barcode nvarchar(100) NULL;
GO

CREATE OR ALTER PROCEDURE dbo.S_GetItemByBarcode
    @hospId     int,
    @categoryId int,
    @barcode    nvarchar(100)
AS
BEGIN
    SET NOCOUNT ON;
    SET @barcode = NULLIF(LTRIM(RTRIM(@barcode)), '');
    IF @barcode IS NULL RETURN;

    /* digits-only codes match with or without leading zeros */
    DECLARE @isNumeric bit = CASE WHEN @barcode NOT LIKE '%[^0-9]%' THEN 1 ELSE 0 END;
    DECLARE @trimmed nvarchar(100) =
        CASE WHEN @isNumeric = 1 THEN ISNULL(NULLIF(SUBSTRING(@barcode, PATINDEX('%[^0]%', @barcode + '.'), 100), ''), '0') ELSE @barcode END;

    DECLARE @hits TABLE (ItemId int PRIMARY KEY, Source nvarchar(20), LastOn datetime, SortNo int);

    /* 1. earlier GRN lots (posted or draft, not cancelled / replaced) */
    INSERT INTO @hits (ItemId, Source, LastOn, SortNo)
    SELECT s.ItemId, 'GRN', MAX(s.CreatedOn), 1
    FROM dbo.Stock s
    WHERE s.HospId = @hospId AND s.IsActive IN (0, 1) AND s.Barcode IS NOT NULL
      AND (s.Barcode = @barcode
           OR (@isNumeric = 1 AND s.Barcode NOT LIKE '%[^0-9]%'
               AND SUBSTRING(s.Barcode, PATINDEX('%[^0]%', s.Barcode + '.'), 100) = @trimmed))
    GROUP BY s.ItemId;

    /* 2. item master barcode - read only */
    INSERT INTO @hits (ItemId, Source, LastOn, SortNo)
    SELECT sim.ServiceItemId, 'Item Master', NULL, 2
    FROM dbo.ServiceItemMaster sim
    WHERE NULLIF(LTRIM(RTRIM(sim.Barcode)), '') IS NOT NULL
      AND (LTRIM(RTRIM(sim.Barcode)) = @barcode
           OR (@isNumeric = 1 AND LTRIM(RTRIM(sim.Barcode)) NOT LIKE '%[^0-9]%'
               AND SUBSTRING(LTRIM(RTRIM(sim.Barcode)), PATINDEX('%[^0]%', LTRIM(RTRIM(sim.Barcode)) + '.'), 100) = @trimmed))
      AND NOT EXISTS (SELECT 1 FROM @hits h WHERE h.ItemId = sim.ServiceItemId);

    SELECT
        sim.ServiceItemId AS ItemId, sim.Name AS ItemName, ISNULL(sim.Code, '') AS ItemCode, sim.SubSubCategoryId,
        ISNULL(sim.Packing, 1) AS Packing, ISNULL(sim.HSNCode, '') AS HsnCode, sim.GSTPer AS GstPer,
        ISNULL(sim.Expirable, 0) AS Expirable, ISNULL(sim.PurchaseUnitName, '') AS PurchaseUnitName,
        ISNULL(sim.SaleUnitName, '') AS SaleUnitName, ISNULL(mm.ManufacturerName, '') AS ManufacturerName,
        CASE WHEN ISNULL(sim.StatusId, 0) = 1 THEN ISNULL(NULLIF(sim.StatusType, ''), 'Hold') ELSE '' END AS HoldStatus,
        ISNULL(sim.MedicineHoldRemorks, '') AS HoldRemarks,
        lp.Rate AS LastRate, lp.MajorMRP AS LastMrp,
        h.Source, h.LastOn AS LastBarcodeOn
    FROM @hits h
    INNER JOIN dbo.ServiceItemMaster sim ON sim.ServiceItemId = h.ItemId AND sim.CategoryId = @categoryId AND sim.IsActive = 1
    LEFT  JOIN dbo.ManufacturerMaster mm ON mm.ManufacturerId = sim.ManufacturerId
    OUTER APPLY (SELECT TOP 1 s.Rate, s.MajorMRP FROM dbo.Stock s
                 WHERE s.HospId = @hospId AND s.ItemId = sim.ServiceItemId AND s.IsActive = 1
                 ORDER BY s.StockId DESC) lp
    ORDER BY h.SortNo, h.LastOn DESC, sim.Name;
END
GO
