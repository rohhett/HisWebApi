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
    }
}