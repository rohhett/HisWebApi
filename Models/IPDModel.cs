namespace HISWEBAPI.Models
{
    public interface IPDModel
    {
    }

    // ================= Consent Form (IPD tab, 2026-11-24) =================
    public class PatientConsentFormModel
    {
        public int PatientConsentFormId { get; set; }
        public int VisitId { get; set; }
        public int PatientFormId { get; set; }
        public string FormName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string UploadMode { get; set; } = string.Empty;
        public string CreatedBy { get; set; } = string.Empty;
        public string CreatedOn { get; set; } = string.Empty;
        public string LastModifiedBy { get; set; } = string.Empty;
        public string LastModifiedOn { get; set; } = string.Empty;
    }
}
