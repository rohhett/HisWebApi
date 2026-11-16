/* =====================================================================
   Lab report print setting - print the barcode in the HEADER  —  2026-11-14
   Target DB : GWT_HIS_API (NEW HIS)
   Re-runnable: yes

   The barcode (Code 128 of the Lab No.) is now printed by PatientInvestigationReportPdfService.
   BarcodePosition 1 = header, anything else = footer. The stored X 155 / Y 128 place it inside the
   header (height 210); in the footer (height 110) Y 128 is outside the area and nothing is visible.

   Changes BarcodePosition to 1 for branch 0 (defaults) and branch 1 (owner, 2026-11-14).
   Other branches keep their own value; branches with no row use the branch-0 default.

   After running: clear the Redis keys _LabReportPrintSetting_* (cached settings), or restart Redis,
   so the normal lab report picks up the change (the preview endpoint always reads fresh values).
   ===================================================================== */

SET NOCOUNT ON;

UPDATE dbo.LabReportPrintSettingMaster
SET    BarcodePosition = 1,
       LastModifiedOn  = GETDATE()
WHERE  BranchId IN (0, 1)
  AND  ISNULL(BarcodePosition, -1) <> 1;

PRINT CAST(@@ROWCOUNT AS varchar) + ' row(s) updated.';

SELECT BranchId, IsActiveBarcode, BarcodePosition, XBarcode, YBarcode, BarcodeWidth, BarcodeHeight, HeaderHeight
FROM   dbo.LabReportPrintSettingMaster
WHERE  BranchId IN (0, 1);
GO
