/* =====================================================================
   Lab report print settings - copy branch 1 settings into branch 0 (defaults)  -  2026-11-14
   Target DB : GWT_HIS_API (NEW HIS)
   Re-runnable: yes (copies the current branch-1 values each time it is run)

   Branch 0 is the default used by "Default Settings" and by every branch that has no row of its own.
   All setting columns are copied; BranchId, CreatedBy and CreatedOn of branch 0 are kept.
   If branch 0 has no row, it is inserted.

   After running: clear the Redis keys _LabReportPrintSetting_* (or restart Redis).
   ===================================================================== */

SET NOCOUNT ON;

IF NOT EXISTS (SELECT 1 FROM dbo.LabReportPrintSettingMaster WHERE BranchId = 1)
BEGIN
    PRINT 'Branch 1 has no print settings - nothing copied.';
    RETURN;
END

IF EXISTS (SELECT 1 FROM dbo.LabReportPrintSettingMaster WHERE BranchId = 0)
BEGIN
    UPDATE d
    SET
       d.MarginLeft = s.MarginLeft,
       d.PageWidth = s.PageWidth,
       d.BrowserWidth = s.BrowserWidth,
       d.HeaderHeight = s.HeaderHeight,
       d.XHeader = s.XHeader,
       d.YHeader = s.YHeader,
       d.HeaderBrowserWidth = s.HeaderBrowserWidth,
       d.TopLine = s.TopLine,
       d.BottomLine = s.BottomLine,
       d.FooterHeight = s.FooterHeight,
       d.XFooter = s.XFooter,
       d.YFooter = s.YFooter,
       d.FooterBrowserWidth = s.FooterBrowserWidth,
       d.FooterTopLine = s.FooterTopLine,
       d.ShowDigitalSignature = s.ShowDigitalSignature,
       d.XSignature = s.XSignature,
       d.YSignature = s.YSignature,
       d.IsActiveBarcode = s.IsActiveBarcode,
       d.XBarcode = s.XBarcode,
       d.YBarcode = s.YBarcode,
       d.BarcodeHeight = s.BarcodeHeight,
       d.BarcodeWidth = s.BarcodeWidth,
       d.BarcodePosition = s.BarcodePosition,
       d.RotateBarcode90Degree = s.RotateBarcode90Degree,
       d.IsActiveQRCode = s.IsActiveQRCode,
       d.XQRCode = s.XQRCode,
       d.YQRCode = s.YQRCode,
       d.QRHeight = s.QRHeight,
       d.QRWidth = s.QRWidth,
       d.QRCodePosition = s.QRCodePosition,
       d.PrintDateTime = s.PrintDateTime,
       d.PreparedBy = s.PreparedBy,
       d.PrintedBy = s.PrintedBy,
       d.PageNumbering = s.PageNumbering,
       d.HideTestNameForSingleTest = s.HideTestNameForSingleTest,
       d.PrintDepartmentSeparate = s.PrintDepartmentSeparate,
       d.SendEmail = s.SendEmail,
       d.SendWhatsAppMessage = s.SendWhatsAppMessage,
       d.FontFamily = s.FontFamily,
       d.BaseFontSize = s.BaseFontSize,
       d.InvestigationNameFontSize = s.InvestigationNameFontSize,
       d.TableHeaderFontSize = s.TableHeaderFontSize,
       d.MethodFontSize = s.MethodFontSize,
       d.ThemeColor = s.ThemeColor,
       d.ThemeTextColor = s.ThemeTextColor,
       d.TableHeaderBgColor = s.TableHeaderBgColor,
       d.TableHeaderTextColor = s.TableHeaderTextColor,
       d.AltRowColor = s.AltRowColor,
       d.GridLineColor = s.GridLineColor,
       d.AbnormalResultColor = s.AbnormalResultColor,
       d.RowSpacing = s.RowSpacing,
       d.InvestigationSpacing = s.InvestigationSpacing,
       d.ObservationNameBold = s.ObservationNameBold,
       d.ResultBoldIfAbnormal = s.ResultBoldIfAbnormal,
       d.ShowInvestigationBand = s.ShowInvestigationBand,
       d.ShowSampleType = s.ShowSampleType,
       d.ShowAccessionInfo = s.ShowAccessionInfo,
       d.ShowDepartmentName = s.ShowDepartmentName,
       d.ShowMethodColumn = s.ShowMethodColumn,
       d.ShowStripedRows = s.ShowStripedRows,
       d.ShowGridLines = s.ShowGridLines,
       d.ShowEndOfReport = s.ShowEndOfReport,
           d.LastModifiedBy = s.LastModifiedBy,
           d.LastModifiedOn = GETDATE(),
           d.IpAddress      = s.IpAddress
    FROM   dbo.LabReportPrintSettingMaster d
    CROSS JOIN (SELECT TOP 1 * FROM dbo.LabReportPrintSettingMaster WHERE BranchId = 1) s
    WHERE  d.BranchId = 0;
    PRINT 'Branch 0 updated from branch 1.';
END
ELSE
BEGIN
    INSERT INTO dbo.LabReportPrintSettingMaster (BranchId, MarginLeft, PageWidth, BrowserWidth, HeaderHeight, XHeader, YHeader, HeaderBrowserWidth, TopLine, BottomLine, FooterHeight, XFooter, YFooter, FooterBrowserWidth, FooterTopLine, ShowDigitalSignature, XSignature, YSignature, IsActiveBarcode, XBarcode, YBarcode, BarcodeHeight, BarcodeWidth, BarcodePosition, RotateBarcode90Degree, IsActiveQRCode, XQRCode, YQRCode, QRHeight, QRWidth, QRCodePosition, PrintDateTime, PreparedBy, PrintedBy, PageNumbering, HideTestNameForSingleTest, PrintDepartmentSeparate, SendEmail, SendWhatsAppMessage, FontFamily, BaseFontSize, InvestigationNameFontSize, TableHeaderFontSize, MethodFontSize, ThemeColor, ThemeTextColor, TableHeaderBgColor, TableHeaderTextColor, AltRowColor, GridLineColor, AbnormalResultColor, RowSpacing, InvestigationSpacing, ObservationNameBold, ResultBoldIfAbnormal, ShowInvestigationBand, ShowSampleType, ShowAccessionInfo, ShowDepartmentName, ShowMethodColumn, ShowStripedRows, ShowGridLines, ShowEndOfReport, CreatedBy, CreatedOn, IpAddress)
    SELECT TOP 1 0, s.MarginLeft, s.PageWidth, s.BrowserWidth, s.HeaderHeight, s.XHeader, s.YHeader, s.HeaderBrowserWidth, s.TopLine, s.BottomLine, s.FooterHeight, s.XFooter, s.YFooter, s.FooterBrowserWidth, s.FooterTopLine, s.ShowDigitalSignature, s.XSignature, s.YSignature, s.IsActiveBarcode, s.XBarcode, s.YBarcode, s.BarcodeHeight, s.BarcodeWidth, s.BarcodePosition, s.RotateBarcode90Degree, s.IsActiveQRCode, s.XQRCode, s.YQRCode, s.QRHeight, s.QRWidth, s.QRCodePosition, s.PrintDateTime, s.PreparedBy, s.PrintedBy, s.PageNumbering, s.HideTestNameForSingleTest, s.PrintDepartmentSeparate, s.SendEmail, s.SendWhatsAppMessage, s.FontFamily, s.BaseFontSize, s.InvestigationNameFontSize, s.TableHeaderFontSize, s.MethodFontSize, s.ThemeColor, s.ThemeTextColor, s.TableHeaderBgColor, s.TableHeaderTextColor, s.AltRowColor, s.GridLineColor, s.AbnormalResultColor, s.RowSpacing, s.InvestigationSpacing, s.ObservationNameBold, s.ResultBoldIfAbnormal, s.ShowInvestigationBand, s.ShowSampleType, s.ShowAccessionInfo, s.ShowDepartmentName, s.ShowMethodColumn, s.ShowStripedRows, s.ShowGridLines, s.ShowEndOfReport, s.CreatedBy, GETDATE(), s.IpAddress
    FROM   dbo.LabReportPrintSettingMaster s
    WHERE  s.BranchId = 1;
    PRINT 'Branch 0 inserted from branch 1.';
END
GO
