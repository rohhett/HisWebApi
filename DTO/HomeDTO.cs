using System.ComponentModel.DataAnnotations;

namespace HISWEBAPI.DTO
{
    public class ResponseMessageRequest
    {
        public int Id { get; set; }
        [Required(ErrorMessage = "TypeId is required")]
        public int TypeId { get; set; }
        [Required(ErrorMessage = "Type is required")]
        public string Type { get; set; }
        [Required(ErrorMessage = "AlertCode is required")]
        public string AlertCode { get; set; }
        [Required(ErrorMessage = "Message is required")]
        public string Message { get; set; }
        public bool IsActive { get; set; }
    }

    // Country Master Request
    public class GetCountryMasterRequest
    {
        public int? IsActive { get; set; } // null = all, 0 = inactive, 1 = active
    }

    // State Master Request
    public class GetStateMasterRequest
    {
        public int? IsActive { get; set; }

        [Required(ErrorMessage = "CountryId is required")]
        public int CountryId { get; set; }
    }

    // District Master Request
    public class GetDistrictMasterRequest
    {
        public int? IsActive { get; set; }

        [Required(ErrorMessage = "StateId is required")]
        public int StateId { get; set; }
    }

    // City Master Request
    public class GetCityMasterRequest
    {
        public int? IsActive { get; set; }

        [Required(ErrorMessage = "DistrictId is required")]
        public int DistrictId { get; set; }
    }

    public class GetPincodeMasterRequest
    {
        public int? IsActive { get; set; }

        [Required(ErrorMessage = "CityId is required")]
        public int CityId { get; set; }
    }

    public class InsuranceCompanyModel
    {
        public int InsuranceCompanyId { get; set; }
        public string InsuranceCompanyName { get; set; }
    }

    public class CorporateModel
    {
        public int CorporateId { get; set; }
        public string CorporateName { get; set; }
        public int InsuranceCompanyId { get; set; }
        public int IsActive { get; set; }
    }

    public class CorporateBranchMappingModel
    {
        public int BranchId { get; set; }
        public int InsuranceCompanyId { get; set; }
        public int CorporateId { get; set; }
        public string CorporateName { get; set; }
        public string PaymentType { get; set; }
        public int PaymentTypeId { get; set; }
        public int IsRegistrationChargeApplicable { get; set; }
        public int IsCaseBillingApplicable { get; set; }

    }

    public class GetCorporateListRequest
    {
       
        public int? InsuranceCompanyId { get; set; }

     
        public int? IsActive { get; set; }
    }

    public class FileStreamResult
    {
        public FileStream FileStream { get; set; }
        public string ContentType { get; set; }
        public string FileName { get; set; }
    }

    public class FileBase64Result
    {
        public string FileName { get; set; }
        public string FileExtension { get; set; }
        public string ContentType { get; set; }
        public long FileSize { get; set; }
        public double FileSizeMB { get; set; }
        public string Base64Data { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime LastModified { get; set; }
    }

    public class FileExistsResult
    {
        public bool Exists { get; set; }
        public string FilePath { get; set; }
    }

    public class GetDoctorMasterRequest
    {
        [Required(ErrorMessage = "BranchId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "BranchId must be greater than 0")]
        public int BranchId { get; set; }

        public int? DepartmentId { get; set; }

        public int? SpecializationId { get; set; }

        public int? IsDoctorUnit { get; set; }
    }

    public class GetCategoryListRequest
    {
        public string CategoryIds { get; set; } // e.g. "3,4,5,6" — optional, null = return all
    }

    public class CreateUpdateSubCategoryRequest
    {
        public int SubCategoryId { get; set; } = 0;

        [Required(ErrorMessage = "SubCategoryName is required")]
        [StringLength(256, ErrorMessage = "SubCategoryName cannot exceed 256 characters")]
        public string SubCategoryName { get; set; }

        [Required(ErrorMessage = "CategoryId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "CategoryId must be greater than 0")]
        public int CategoryId { get; set; }

        public int LabTypeId { get; set; } = 0;
        public string LabType { get; set; }
    }

    public class CreateUpdateSubCategoryResponse
    {
        public int SubCategoryId { get; set; }
    }

    public class CreateUpdateSubSubCategoryRequest
    {
        public int SubSubCategoryId { get; set; } = 0;

        [Required(ErrorMessage = "SubSubCategoryName is required")]
        [StringLength(256, ErrorMessage = "SubSubCategoryName cannot exceed 256 characters")]
        public string SubSubCategoryName { get; set; }

        [Required(ErrorMessage = "SubCategoryId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "SubCategoryId must be greater than 0")]
        public int SubCategoryId { get; set; }
        public int PrintGroupId { get; set; }
        public int DepartmentId { get; set; }
    }

    public class CreateUpdateSubSubCategoryResponse
    {
        public int SubSubCategoryId { get; set; }
    }

    public class CreateUpdateCategoryRequest
    {
        public int CategoryId { get; set; } = 0;

        [Required(ErrorMessage = "Category name is required")]
        [StringLength(256, ErrorMessage = "Category name cannot exceed 256 characters")]
        public string CategoryName { get; set; }

        [Required(ErrorMessage = "CategoryTypeId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "CategoryTypeId must be greater than 0")]
        public int CategoryTypeId { get; set; }

        [Required(ErrorMessage = "Category type name is required")]
        [StringLength(256, ErrorMessage = "Category type name cannot exceed 256 characters")]
        public string CategoryTypeName { get; set; }
    }

    public class CreateUpdateCategoryResponse
    {
        public int CategoryId { get; set; }
    }

    public class PatientInvestigationReportRequest
    {
        [Required]
        public string PatientInvestigationIds { get; set; } = string.Empty;

        [Required]
        public int BranchId { get; set; }
        public int IsHeaderPng { get; set; }
        public bool Download { get; set; } = true;
        public int DummyMode { get; set; }
        public string Contacts { get; set; } = string.Empty;
        public string EmailIds { get; set; } = string.Empty;
    }
    public class GetPatientLedgerBillRequest
    {
        [Required(ErrorMessage = "PatientId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "PatientId must be greater than 0")]
        public int PatientId { get; set; }
    }
    public class UpdateFavoriteBillingTabRequest
    {
        [Required(ErrorMessage = "BranchId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "BranchId must be greater than 0")]
        public int BranchId { get; set; }

        [Required(ErrorMessage = "RoleId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "RoleId must be greater than 0")]
        public int RoleId { get; set; }

        [Required(ErrorMessage = "TabId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "TabId must be greater than 0")]
        public int TabId { get; set; }
    }

    public class UploadDocumentFromFileManagerRequest
    {
        [Required(ErrorMessage = "File is required")]
        public IFormFile File { get; set; }
    }

    public class UploadDocumentFromFileManagerResponse
    {
        public string FileName { get; set; }
        public string FilePath { get; set; }
        public string FileExtension { get; set; }
        public double FileSizeMB { get; set; }
    }


    public class SendMobileVerificationOtpRequest
    {
        [Required(ErrorMessage = "MobileNumber is required")]
        [RegularExpression(@"^\d{10}$", ErrorMessage = "MobileNumber must be exactly 10 digits")]
        public string MobileNumber { get; set; }
    }

    public class MobileVerificationOtpResponseData
    {
        public string MobileHint { get; set; }
    }

    public class VerifyMobileVerificationOtpRequest
    {
        [Required(ErrorMessage = "MobileNumber is required")]
        [RegularExpression(@"^\d{10}$", ErrorMessage = "MobileNumber must be exactly 10 digits")]
        public string MobileNumber { get; set; }

        [Required(ErrorMessage = "OTP is required")]
        [StringLength(6, MinimumLength = 6, ErrorMessage = "OTP must be 6 digits")]
        public string Otp { get; set; }
    }

    public class SendEmailVerificationOtpRequest
    {
        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email format")]
        public string Email { get; set; }
    }

    public class EmailVerificationOtpResponseData
    {
        public string EmailHint { get; set; }
    }

    public class VerifyEmailVerificationOtpRequest
    {
        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email format")]
        public string Email { get; set; }

        [Required(ErrorMessage = "OTP is required")]
        [StringLength(6, MinimumLength = 6, ErrorMessage = "OTP must be 6 digits")]
        public string Otp { get; set; }
    }

}
