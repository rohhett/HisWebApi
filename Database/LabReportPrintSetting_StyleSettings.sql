/* =====================================================================
   Lab report print setting : style / layout settings
   (font, colours, row spacing, bold observation name, show / hide parts)

   - Adds the new columns to LabReportPrintSettingMaster (all NULLable)
   - Fills the default row (BranchId = 0) with the default values
   - Re-creates the 3 SPs so they read / save the new columns
   Branch rows with NULL fall back to the default row; if a column is
   missing everywhere the PDF service uses its own hard-coded default.
   Safe to run more than once.
   ===================================================================== */

/* ---------- 1. columns ---------- */
IF COL_LENGTH('dbo.LabReportPrintSettingMaster','FontFamily') IS NULL
    ALTER TABLE dbo.LabReportPrintSettingMaster ADD
        -- fonts
        FontFamily                NVARCHAR(100) NULL,
        BaseFontSize              INT NULL,
        InvestigationNameFontSize INT NULL,
        TableHeaderFontSize       INT NULL,
        MethodFontSize            INT NULL,
        -- colours
        ThemeColor                NVARCHAR(30) NULL,
        ThemeTextColor            NVARCHAR(30) NULL,
        TableHeaderBgColor        NVARCHAR(30) NULL,
        TableHeaderTextColor      NVARCHAR(30) NULL,
        AltRowColor               NVARCHAR(30) NULL,
        GridLineColor             NVARCHAR(30) NULL,
        AbnormalResultColor       NVARCHAR(30) NULL,
        -- spacing
        RowSpacing                INT NULL,
        InvestigationSpacing      INT NULL,
        -- bold
        ObservationNameBold       INT NULL,
        ResultBoldIfAbnormal      INT NULL,
        -- show / hide
        ShowInvestigationBand     INT NULL,
        ShowSampleType            INT NULL,
        ShowAccessionInfo         INT NULL,
        ShowDepartmentName        INT NULL,
        ShowMethodColumn          INT NULL,
        ShowStripedRows           INT NULL,
        ShowGridLines             INT NULL,
        ShowEndOfReport           INT NULL;
GO

/* ---------- 2. default row values ---------- */
UPDATE dbo.LabReportPrintSettingMaster SET
    FontFamily = ISNULL(FontFamily, 'Arial'),
    BaseFontSize = ISNULL(BaseFontSize, 14),
    InvestigationNameFontSize = ISNULL(InvestigationNameFontSize, 16),
    TableHeaderFontSize = ISNULL(TableHeaderFontSize, 14),
    MethodFontSize = ISNULL(MethodFontSize, 13),
    ThemeColor = ISNULL(ThemeColor, '#B94A52'),
    ThemeTextColor = ISNULL(ThemeTextColor, '#FFFFFF'),
    TableHeaderBgColor = ISNULL(TableHeaderBgColor, '#D9D9D9'),
    TableHeaderTextColor = ISNULL(TableHeaderTextColor, '#000000'),
    AltRowColor = ISNULL(AltRowColor, '#F2F2F2'),
    GridLineColor = ISNULL(GridLineColor, '#DDDDDD'),
    AbnormalResultColor = ISNULL(AbnormalResultColor, '#000000'),
    RowSpacing = ISNULL(RowSpacing, 3),
    InvestigationSpacing = ISNULL(InvestigationSpacing, 14),
    ObservationNameBold = ISNULL(ObservationNameBold, 0),
    ResultBoldIfAbnormal = ISNULL(ResultBoldIfAbnormal, 1),
    ShowInvestigationBand = ISNULL(ShowInvestigationBand, 1),
    ShowSampleType = ISNULL(ShowSampleType, 1),
    ShowAccessionInfo = ISNULL(ShowAccessionInfo, 1),
    ShowDepartmentName = ISNULL(ShowDepartmentName, 0),
    ShowMethodColumn = ISNULL(ShowMethodColumn, 1),
    ShowStripedRows = ISNULL(ShowStripedRows, 1),
    ShowGridLines = ISNULL(ShowGridLines, 1),
    ShowEndOfReport = ISNULL(ShowEndOfReport, 1)
WHERE BranchId = 0;
GO

/* ---------- 3. GET #1 ---------- */
CREATE OR ALTER PROCEDURE dbo.S_GetLabReportPrintSetting
    @BranchId       INT,
    @ApplyDefaults  BIT = 0
AS
BEGIN
    SET NOCOUNT ON;

    IF @ApplyDefaults = 1
        SELECT
            @BranchId AS BranchId,
            COALESCE(b.MarginLeft,d.MarginLeft) AS MarginLeft,
            COALESCE(b.PageWidth,d.PageWidth) AS PageWidth,
            COALESCE(b.BrowserWidth,d.BrowserWidth) AS BrowserWidth,
            COALESCE(b.HeaderHeight,d.HeaderHeight) AS HeaderHeight,
            COALESCE(b.XHeader,d.XHeader) AS XHeader,
            COALESCE(b.YHeader,d.YHeader) AS YHeader,
            COALESCE(b.HeaderBrowserWidth,d.HeaderBrowserWidth) AS HeaderBrowserWidth,
            COALESCE(b.TopLine,d.TopLine) AS TopLine,
            COALESCE(b.BottomLine,d.BottomLine) AS BottomLine,
            COALESCE(b.FooterHeight,d.FooterHeight) AS FooterHeight,
            COALESCE(b.XFooter,d.XFooter) AS XFooter,
            COALESCE(b.YFooter,d.YFooter) AS YFooter,
            COALESCE(b.FooterBrowserWidth,d.FooterBrowserWidth) AS FooterBrowserWidth,
            COALESCE(b.FooterTopLine,d.FooterTopLine) AS FooterTopLine,
            COALESCE(b.ShowDigitalSignature,d.ShowDigitalSignature) AS ShowDigitalSignature,
            COALESCE(b.XSignature,d.XSignature) AS XSignature,
            COALESCE(b.YSignature,d.YSignature) AS YSignature,
            COALESCE(b.IsActiveBarcode,d.IsActiveBarcode) AS IsActiveBarcode,
            COALESCE(b.XBarcode,d.XBarcode) AS XBarcode,
            COALESCE(b.YBarcode,d.YBarcode) AS YBarcode,
            COALESCE(b.BarcodeHeight,d.BarcodeHeight) AS BarcodeHeight,
            COALESCE(b.BarcodeWidth,d.BarcodeWidth) AS BarcodeWidth,
            COALESCE(b.BarcodePosition,d.BarcodePosition) AS BarcodePosition,
            COALESCE(b.RotateBarcode90Degree,d.RotateBarcode90Degree) AS RotateBarcode90Degree,
            COALESCE(b.IsActiveQRCode,d.IsActiveQRCode) AS IsActiveQRCode,
            COALESCE(b.XQRCode,d.XQRCode) AS XQRCode,
            COALESCE(b.YQRCode,d.YQRCode) AS YQRCode,
            COALESCE(b.QRHeight,d.QRHeight) AS QRHeight,
            COALESCE(b.QRWidth,d.QRWidth) AS QRWidth,
            COALESCE(b.QRCodePosition,d.QRCodePosition) AS QRCodePosition,
            COALESCE(b.PrintDateTime,d.PrintDateTime) AS PrintDateTime,
            COALESCE(b.PreparedBy,d.PreparedBy) AS PreparedBy,
            COALESCE(b.PrintedBy,d.PrintedBy) AS PrintedBy,
            COALESCE(b.PageNumbering,d.PageNumbering) AS PageNumbering,
            COALESCE(b.HideTestNameForSingleTest,d.HideTestNameForSingleTest) AS HideTestNameForSingleTest,
            COALESCE(b.PrintDepartmentSeparate,d.PrintDepartmentSeparate) AS PrintDepartmentSeparate,
            COALESCE(b.SendEmail,d.SendEmail) AS SendEmail,
            COALESCE(b.SendWhatsAppMessage,d.SendWhatsAppMessage) AS SendWhatsAppMessage,
            -- style
            COALESCE(b.FontFamily,d.FontFamily) AS FontFamily,
            COALESCE(b.BaseFontSize,d.BaseFontSize) AS BaseFontSize,
            COALESCE(b.InvestigationNameFontSize,d.InvestigationNameFontSize) AS InvestigationNameFontSize,
            COALESCE(b.TableHeaderFontSize,d.TableHeaderFontSize) AS TableHeaderFontSize,
            COALESCE(b.MethodFontSize,d.MethodFontSize) AS MethodFontSize,
            COALESCE(b.ThemeColor,d.ThemeColor) AS ThemeColor,
            COALESCE(b.ThemeTextColor,d.ThemeTextColor) AS ThemeTextColor,
            COALESCE(b.TableHeaderBgColor,d.TableHeaderBgColor) AS TableHeaderBgColor,
            COALESCE(b.TableHeaderTextColor,d.TableHeaderTextColor) AS TableHeaderTextColor,
            COALESCE(b.AltRowColor,d.AltRowColor) AS AltRowColor,
            COALESCE(b.GridLineColor,d.GridLineColor) AS GridLineColor,
            COALESCE(b.AbnormalResultColor,d.AbnormalResultColor) AS AbnormalResultColor,
            COALESCE(b.RowSpacing,d.RowSpacing) AS RowSpacing,
            COALESCE(b.InvestigationSpacing,d.InvestigationSpacing) AS InvestigationSpacing,
            COALESCE(b.ObservationNameBold,d.ObservationNameBold) AS ObservationNameBold,
            COALESCE(b.ResultBoldIfAbnormal,d.ResultBoldIfAbnormal) AS ResultBoldIfAbnormal,
            COALESCE(b.ShowInvestigationBand,d.ShowInvestigationBand) AS ShowInvestigationBand,
            COALESCE(b.ShowSampleType,d.ShowSampleType) AS ShowSampleType,
            COALESCE(b.ShowAccessionInfo,d.ShowAccessionInfo) AS ShowAccessionInfo,
            COALESCE(b.ShowDepartmentName,d.ShowDepartmentName) AS ShowDepartmentName,
            COALESCE(b.ShowMethodColumn,d.ShowMethodColumn) AS ShowMethodColumn,
            COALESCE(b.ShowStripedRows,d.ShowStripedRows) AS ShowStripedRows,
            COALESCE(b.ShowGridLines,d.ShowGridLines) AS ShowGridLines,
            COALESCE(b.ShowEndOfReport,d.ShowEndOfReport) AS ShowEndOfReport
        FROM dbo.LabReportPrintSettingMaster d
        LEFT JOIN dbo.LabReportPrintSettingMaster b ON b.BranchId = @BranchId
        WHERE d.BranchId = 0;
    ELSE
        SELECT
            @BranchId AS BranchId,
            b.MarginLeft, b.PageWidth, b.BrowserWidth,
            b.HeaderHeight, b.XHeader, b.YHeader, b.HeaderBrowserWidth, b.TopLine, b.BottomLine,
            b.FooterHeight, b.XFooter, b.YFooter, b.FooterBrowserWidth, b.FooterTopLine,
            b.ShowDigitalSignature, b.XSignature, b.YSignature,
            b.IsActiveBarcode, b.XBarcode, b.YBarcode, b.BarcodeHeight, b.BarcodeWidth, b.BarcodePosition, b.RotateBarcode90Degree,
            b.IsActiveQRCode, b.XQRCode, b.YQRCode, b.QRHeight, b.QRWidth, b.QRCodePosition,
            b.PrintDateTime, b.PreparedBy, b.PrintedBy, b.PageNumbering,
            b.HideTestNameForSingleTest, b.PrintDepartmentSeparate, b.SendEmail, b.SendWhatsAppMessage,
            b.FontFamily, b.BaseFontSize, b.InvestigationNameFontSize, b.TableHeaderFontSize, b.MethodFontSize,
            b.ThemeColor, b.ThemeTextColor, b.TableHeaderBgColor, b.TableHeaderTextColor, b.AltRowColor, b.GridLineColor, b.AbnormalResultColor,
            b.RowSpacing, b.InvestigationSpacing, b.ObservationNameBold, b.ResultBoldIfAbnormal,
            b.ShowInvestigationBand, b.ShowSampleType, b.ShowAccessionInfo, b.ShowDepartmentName,
            b.ShowMethodColumn, b.ShowStripedRows, b.ShowGridLines, b.ShowEndOfReport
        FROM (SELECT 1 AS x) t
        LEFT JOIN dbo.LabReportPrintSettingMaster b ON b.BranchId = @BranchId;
END
GO

/* ---------- 4. GET #2 (placeholders) ---------- */
CREATE OR ALTER PROCEDURE dbo.S_GetLabReportPrintSettingDefaults
AS
BEGIN
    SET NOCOUNT ON;
    SELECT MarginLeft, PageWidth, BrowserWidth,
           HeaderHeight, XHeader, YHeader, HeaderBrowserWidth, TopLine, BottomLine,
           FooterHeight, XFooter, YFooter, FooterBrowserWidth, FooterTopLine,
           ShowDigitalSignature, XSignature, YSignature,
           IsActiveBarcode, XBarcode, YBarcode, BarcodeHeight, BarcodeWidth, BarcodePosition, RotateBarcode90Degree,
           IsActiveQRCode, XQRCode, YQRCode, QRHeight, QRWidth, QRCodePosition,
           PrintDateTime, PreparedBy, PrintedBy, PageNumbering,
           HideTestNameForSingleTest, PrintDepartmentSeparate, SendEmail, SendWhatsAppMessage,
           FontFamily, BaseFontSize, InvestigationNameFontSize, TableHeaderFontSize, MethodFontSize,
           ThemeColor, ThemeTextColor, TableHeaderBgColor, TableHeaderTextColor, AltRowColor, GridLineColor, AbnormalResultColor,
           RowSpacing, InvestigationSpacing, ObservationNameBold, ResultBoldIfAbnormal,
           ShowInvestigationBand, ShowSampleType, ShowAccessionInfo, ShowDepartmentName,
           ShowMethodColumn, ShowStripedRows, ShowGridLines, ShowEndOfReport
    FROM dbo.LabReportPrintSettingMaster
    WHERE BranchId = 0;
END
GO

/* ---------- 5. POST (upsert) ---------- */
CREATE OR ALTER PROCEDURE dbo.IU_LabReportPrintSetting
    @BranchId INT,
    @MarginLeft INT = NULL, @PageWidth INT = NULL, @BrowserWidth INT = NULL,
    @HeaderHeight INT = NULL, @XHeader INT = NULL, @YHeader INT = NULL, @HeaderBrowserWidth INT = NULL,
    @TopLine INT = NULL, @BottomLine INT = NULL,
    @FooterHeight INT = NULL, @XFooter INT = NULL, @YFooter INT = NULL, @FooterBrowserWidth INT = NULL, @FooterTopLine INT = NULL,
    @ShowDigitalSignature INT = NULL, @XSignature INT = NULL, @YSignature INT = NULL,
    @IsActiveBarcode INT = NULL, @XBarcode INT = NULL, @YBarcode INT = NULL, @BarcodeHeight INT = NULL, @BarcodeWidth INT = NULL,
    @BarcodePosition INT = NULL, @RotateBarcode90Degree INT = NULL,
    @IsActiveQRCode INT = NULL, @XQRCode INT = NULL, @YQRCode INT = NULL, @QRHeight INT = NULL, @QRWidth INT = NULL, @QRCodePosition INT = NULL,
    @PrintDateTime INT = NULL, @PreparedBy INT = NULL, @PrintedBy INT = NULL, @PageNumbering INT = NULL,
    @HideTestNameForSingleTest INT = NULL, @PrintDepartmentSeparate INT = NULL, @SendEmail INT = NULL, @SendWhatsAppMessage INT = NULL,
    -- style
    @FontFamily NVARCHAR(100) = NULL, @BaseFontSize INT = NULL, @InvestigationNameFontSize INT = NULL,
    @TableHeaderFontSize INT = NULL, @MethodFontSize INT = NULL,
    @ThemeColor NVARCHAR(30) = NULL, @ThemeTextColor NVARCHAR(30) = NULL,
    @TableHeaderBgColor NVARCHAR(30) = NULL, @TableHeaderTextColor NVARCHAR(30) = NULL,
    @AltRowColor NVARCHAR(30) = NULL, @GridLineColor NVARCHAR(30) = NULL, @AbnormalResultColor NVARCHAR(30) = NULL,
    @RowSpacing INT = NULL, @InvestigationSpacing INT = NULL,
    @ObservationNameBold INT = NULL, @ResultBoldIfAbnormal INT = NULL,
    @ShowInvestigationBand INT = NULL, @ShowSampleType INT = NULL, @ShowAccessionInfo INT = NULL, @ShowDepartmentName INT = NULL,
    @ShowMethodColumn INT = NULL, @ShowStripedRows INT = NULL, @ShowGridLines INT = NULL, @ShowEndOfReport INT = NULL,
    @UserId INT,
    @IpAddress NVARCHAR(50) = NULL,
    @Result INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    IF @BranchId <= 0 BEGIN SET @Result = -1; RETURN; END   -- default row is not editable via API

    IF EXISTS (SELECT 1 FROM dbo.LabReportPrintSettingMaster WHERE BranchId = @BranchId)
        UPDATE dbo.LabReportPrintSettingMaster SET
            MarginLeft=@MarginLeft, PageWidth=@PageWidth, BrowserWidth=@BrowserWidth,
            HeaderHeight=@HeaderHeight, XHeader=@XHeader, YHeader=@YHeader, HeaderBrowserWidth=@HeaderBrowserWidth,
            TopLine=@TopLine, BottomLine=@BottomLine,
            FooterHeight=@FooterHeight, XFooter=@XFooter, YFooter=@YFooter, FooterBrowserWidth=@FooterBrowserWidth, FooterTopLine=@FooterTopLine,
            ShowDigitalSignature=@ShowDigitalSignature, XSignature=@XSignature, YSignature=@YSignature,
            IsActiveBarcode=@IsActiveBarcode, XBarcode=@XBarcode, YBarcode=@YBarcode, BarcodeHeight=@BarcodeHeight, BarcodeWidth=@BarcodeWidth,
            BarcodePosition=@BarcodePosition, RotateBarcode90Degree=@RotateBarcode90Degree,
            IsActiveQRCode=@IsActiveQRCode, XQRCode=@XQRCode, YQRCode=@YQRCode, QRHeight=@QRHeight, QRWidth=@QRWidth, QRCodePosition=@QRCodePosition,
            PrintDateTime=@PrintDateTime, PreparedBy=@PreparedBy, PrintedBy=@PrintedBy, PageNumbering=@PageNumbering,
            HideTestNameForSingleTest=@HideTestNameForSingleTest, PrintDepartmentSeparate=@PrintDepartmentSeparate,
            SendEmail=@SendEmail, SendWhatsAppMessage=@SendWhatsAppMessage,
            FontFamily=@FontFamily, BaseFontSize=@BaseFontSize, InvestigationNameFontSize=@InvestigationNameFontSize,
            TableHeaderFontSize=@TableHeaderFontSize, MethodFontSize=@MethodFontSize,
            ThemeColor=@ThemeColor, ThemeTextColor=@ThemeTextColor, TableHeaderBgColor=@TableHeaderBgColor,
            TableHeaderTextColor=@TableHeaderTextColor, AltRowColor=@AltRowColor, GridLineColor=@GridLineColor,
            AbnormalResultColor=@AbnormalResultColor,
            RowSpacing=@RowSpacing, InvestigationSpacing=@InvestigationSpacing,
            ObservationNameBold=@ObservationNameBold, ResultBoldIfAbnormal=@ResultBoldIfAbnormal,
            ShowInvestigationBand=@ShowInvestigationBand, ShowSampleType=@ShowSampleType, ShowAccessionInfo=@ShowAccessionInfo,
            ShowDepartmentName=@ShowDepartmentName, ShowMethodColumn=@ShowMethodColumn, ShowStripedRows=@ShowStripedRows,
            ShowGridLines=@ShowGridLines, ShowEndOfReport=@ShowEndOfReport,
            LastModifiedBy=@UserId, LastModifiedOn=GETDATE(), IpAddress=@IpAddress
        WHERE BranchId=@BranchId;
    ELSE
        INSERT INTO dbo.LabReportPrintSettingMaster
        (BranchId, MarginLeft, PageWidth, BrowserWidth, HeaderHeight, XHeader, YHeader, HeaderBrowserWidth, TopLine, BottomLine,
         FooterHeight, XFooter, YFooter, FooterBrowserWidth, FooterTopLine, ShowDigitalSignature, XSignature, YSignature,
         IsActiveBarcode, XBarcode, YBarcode, BarcodeHeight, BarcodeWidth, BarcodePosition, RotateBarcode90Degree,
         IsActiveQRCode, XQRCode, YQRCode, QRHeight, QRWidth, QRCodePosition,
         PrintDateTime, PreparedBy, PrintedBy, PageNumbering,
         HideTestNameForSingleTest, PrintDepartmentSeparate, SendEmail, SendWhatsAppMessage,
         FontFamily, BaseFontSize, InvestigationNameFontSize, TableHeaderFontSize, MethodFontSize,
         ThemeColor, ThemeTextColor, TableHeaderBgColor, TableHeaderTextColor, AltRowColor, GridLineColor, AbnormalResultColor,
         RowSpacing, InvestigationSpacing, ObservationNameBold, ResultBoldIfAbnormal,
         ShowInvestigationBand, ShowSampleType, ShowAccessionInfo, ShowDepartmentName,
         ShowMethodColumn, ShowStripedRows, ShowGridLines, ShowEndOfReport,
         CreatedBy, CreatedOn, IpAddress)
        VALUES
        (@BranchId, @MarginLeft, @PageWidth, @BrowserWidth, @HeaderHeight, @XHeader, @YHeader, @HeaderBrowserWidth, @TopLine, @BottomLine,
         @FooterHeight, @XFooter, @YFooter, @FooterBrowserWidth, @FooterTopLine, @ShowDigitalSignature, @XSignature, @YSignature,
         @IsActiveBarcode, @XBarcode, @YBarcode, @BarcodeHeight, @BarcodeWidth, @BarcodePosition, @RotateBarcode90Degree,
         @IsActiveQRCode, @XQRCode, @YQRCode, @QRHeight, @QRWidth, @QRCodePosition,
         @PrintDateTime, @PreparedBy, @PrintedBy, @PageNumbering,
         @HideTestNameForSingleTest, @PrintDepartmentSeparate, @SendEmail, @SendWhatsAppMessage,
         @FontFamily, @BaseFontSize, @InvestigationNameFontSize, @TableHeaderFontSize, @MethodFontSize,
         @ThemeColor, @ThemeTextColor, @TableHeaderBgColor, @TableHeaderTextColor, @AltRowColor, @GridLineColor, @AbnormalResultColor,
         @RowSpacing, @InvestigationSpacing, @ObservationNameBold, @ResultBoldIfAbnormal,
         @ShowInvestigationBand, @ShowSampleType, @ShowAccessionInfo, @ShowDepartmentName,
         @ShowMethodColumn, @ShowStripedRows, @ShowGridLines, @ShowEndOfReport,
         @UserId, GETDATE(), @IpAddress);

    SET @Result = @BranchId;
END
GO
