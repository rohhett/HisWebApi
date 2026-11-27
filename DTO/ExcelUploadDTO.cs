using System.ComponentModel.DataAnnotations;

namespace HISWEBAPI.DTO
{
    // ================= Master Excel upload (Doctor, Corporate) =================
    // All uploaded values are text, as typed in the sheet.

    public abstract class ExcelUploadRow
    {
        public int RowNo { get; set; }
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new();
    }

    public class ExcelUploadResponse<TRow> where TRow : ExcelUploadRow
    {
        public bool Saved { get; set; }
        public int TotalRows { get; set; }
        public int ValidRows { get; set; }
        public int ErrorRows { get; set; }
        public int SavedRows { get; set; }
        public List<TRow> Rows { get; set; } = new();
    }

    public class DoctorUploadRow : ExcelUploadRow
    {
        public string? Title { get; set; }
        public string? Name { get; set; }
        public string? Gender { get; set; }
        public string? Dob { get; set; }
        public string? ContactNo { get; set; }
        public string? EmailId { get; set; }
        public string? Address { get; set; }
        public string? Specialization { get; set; }
        public string? Department { get; set; }
        public string? ProfileSummery { get; set; }
        public string? DoctorRole { get; set; }
        public string? RegistrationNo { get; set; }
        public string? RoomNo { get; set; }
        public string? CanApproveLabReport { get; set; }
        public string? CanApproveDischargeSummary { get; set; }
    }

    public class DoctorExcelUploadRequest
    {
        /// <summary>false = only validate the rows, true = validate and save them (all or nothing).</summary>
        public bool SaveData { get; set; }

        /// <summary>Branch ids for every uploaded doctor, comma separated (chosen once in the upload popup).</summary>
        public string? BranchList { get; set; }

        [Required(ErrorMessage = "Rows are required")]
        public List<DoctorUploadRow> Rows { get; set; } = new();
    }

    public class CorporateUploadRow : ExcelUploadRow
    {
        public string? CorporateName { get; set; }
        public string? CorporateType { get; set; }
        public string? InsuranceCompany { get; set; }
        public string? CorporateCode { get; set; }
        public string? CorporateContact1 { get; set; }
        public string? CorporateContact2 { get; set; }
        public string? CorporateEmail { get; set; }
        public string? CorporateAddress1 { get; set; }
        public string? CorporateAddress2 { get; set; }
        public string? ContractStartFrom { get; set; }
        public string? ContractExpiresOn { get; set; }
        public string? CopaymentPer { get; set; }
        public string? DiscountPerOut { get; set; }
        public string? DiscountPerIn { get; set; }
        public string? HikePerOut { get; set; }
        public string? HikePerIn { get; set; }
        public string? PaymentType { get; set; }
        public string? RegistrationCharge { get; set; }
        public string? CaseBilling { get; set; }
    }

    public class CorporateExcelUploadRequest
    {
        public bool SaveData { get; set; }

        /// <summary>Allowed payment mode ids for every uploaded corporate, comma separated (chosen once).</summary>
        public string? ActivePaymentModes { get; set; }

        [Required(ErrorMessage = "Rows are required")]
        public List<CorporateUploadRow> Rows { get; set; } = new();
    }
}
