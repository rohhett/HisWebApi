using System.ComponentModel.DataAnnotations;

namespace HISWEBAPI.DTO
{
    /// <summary>
    /// POST body. Property names MUST match the IU_LabReportPrintSetting parameter names
    /// (they are mapped by reflection). null = clear override / use default.
    /// Flag fields (TopLine, BottomLine, ...) are 0 / 1.
    /// </summary>
    public class SaveLabReportPrintSettingRequest
    {
        [Required(ErrorMessage = "BranchId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "BranchId must be greater than 0")]
        public int BranchId { get; set; }

        // Page / body
        public int? MarginLeft { get; set; }
        public int? PageWidth { get; set; }
        public int? BrowserWidth { get; set; }

        // Header
        public int? HeaderHeight { get; set; }
        public int? XHeader { get; set; }
        public int? YHeader { get; set; }
        public int? HeaderBrowserWidth { get; set; }
        public int? TopLine { get; set; }
        public int? BottomLine { get; set; }

        // Footer
        public int? FooterHeight { get; set; }
        public int? XFooter { get; set; }
        public int? YFooter { get; set; }
        public int? FooterBrowserWidth { get; set; }
        public int? FooterTopLine { get; set; }

        // Digital signature
        public int? ShowDigitalSignature { get; set; }
        public int? XSignature { get; set; }
        public int? YSignature { get; set; }

        // Barcode
        public int? IsActiveBarcode { get; set; }
        public int? XBarcode { get; set; }
        public int? YBarcode { get; set; }
        public int? BarcodeHeight { get; set; }
        public int? BarcodeWidth { get; set; }
        public int? BarcodePosition { get; set; }
        public int? RotateBarcode90Degree { get; set; }

        // QR code
        public int? IsActiveQRCode { get; set; }
        public int? XQRCode { get; set; }
        public int? YQRCode { get; set; }
        public int? QRHeight { get; set; }
        public int? QRWidth { get; set; }
        public int? QRCodePosition { get; set; }

        // Footer texts
        public int? PrintDateTime { get; set; }
        public int? PreparedBy { get; set; }
        public int? PrintedBy { get; set; }
        public int? PageNumbering { get; set; }

        // Behaviour
        public int? HideTestNameForSingleTest { get; set; }
        public int? PrintDepartmentSeparate { get; set; }
        public int? SendEmail { get; set; }
        public int? SendWhatsAppMessage { get; set; }

        // ── Report style ───────────────────────────────────────────────
        // Fonts (sizes in px)
        [RegularExpression(@"^[A-Za-z0-9 ,'\-]{1,100}$", ErrorMessage = "FontFamily contains invalid characters")]
        public string? FontFamily { get; set; }
        [Range(6, 40)] public int? BaseFontSize { get; set; }
        [Range(6, 40)] public int? InvestigationNameFontSize { get; set; }
        [Range(6, 40)] public int? TableHeaderFontSize { get; set; }
        [Range(6, 40)] public int? MethodFontSize { get; set; }

        // Colours: #RGB / #RRGGBB or a CSS colour name
        [RegularExpression(ColorPattern, ErrorMessage = "ThemeColor must be #RRGGBB or a colour name")]
        public string? ThemeColor { get; set; }
        [RegularExpression(ColorPattern, ErrorMessage = "ThemeTextColor must be #RRGGBB or a colour name")]
        public string? ThemeTextColor { get; set; }
        [RegularExpression(ColorPattern, ErrorMessage = "TableHeaderBgColor must be #RRGGBB or a colour name")]
        public string? TableHeaderBgColor { get; set; }
        [RegularExpression(ColorPattern, ErrorMessage = "TableHeaderTextColor must be #RRGGBB or a colour name")]
        public string? TableHeaderTextColor { get; set; }
        [RegularExpression(ColorPattern, ErrorMessage = "AltRowColor must be #RRGGBB or a colour name")]
        public string? AltRowColor { get; set; }
        [RegularExpression(ColorPattern, ErrorMessage = "GridLineColor must be #RRGGBB or a colour name")]
        public string? GridLineColor { get; set; }
        [RegularExpression(ColorPattern, ErrorMessage = "AbnormalResultColor must be #RRGGBB or a colour name")]
        public string? AbnormalResultColor { get; set; }

        // Spacing (px): padding inside each result row / gap between investigations
        [Range(0, 30)] public int? RowSpacing { get; set; }
        [Range(0, 100)] public int? InvestigationSpacing { get; set; }

        // Bold (0 / 1)
        public int? ObservationNameBold { get; set; }
        public int? ResultBoldIfAbnormal { get; set; }

        // Show / hide (0 / 1)
        public int? ShowInvestigationBand { get; set; }   // coloured bar with test name (new layout); 0 = old plain layout
        public int? ShowSampleType { get; set; }          // "EDTA Whole Blood Sample" on the right of the bar
        public int? ShowAccessionInfo { get; set; }       // Accession No / Collected / Received / Approved line
        public int? ShowDepartmentName { get; set; }
        public int? ShowMethodColumn { get; set; }
        public int? ShowStripedRows { get; set; }
        public int? ShowGridLines { get; set; }
        public int? ShowEndOfReport { get; set; }

        private const string ColorPattern = @"^(#[0-9A-Fa-f]{3}|#[0-9A-Fa-f]{6}|[A-Za-z]{3,20})$";
    }
}