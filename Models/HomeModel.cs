namespace HISWEBAPI.Models
{

    public class CountryMasterModel
    {
        public int CountryId { get; set; }
        public string CountryName { get; set; }
        public string Currency { get; set; }
        public decimal? ConversionFactor { get; set; }
        public int IsActive { get; set; }
    }

    public class StateMasterModel
    {
        public int CountryId { get; set; }
        public int StateId { get; set; }
        public string StateName { get; set; }
        public int IsActive { get; set; }
    }

    public class DistrictMasterModel
    {
        public int CountryId { get; set; }
        public int StateId { get; set; }
        public int DistrictId { get; set; }
        public string DistrictName { get; set; }
        public int IsActive { get; set; }
    }

    public class CityMasterModel
    {
        public int CountryId { get; set; }
        public int StateId { get; set; }
        public int DistrictId { get; set; }
        public int CityId { get; set; }
        public string CityName { get; set; }
        public int IsActive { get; set; }
    }

    public class PincodeMasterModel
    {
        public int CityId { get; set; }
        public int PincodeId { get; set; }
        public int Pincode { get; set; }
        public int IsActive { get; set; }
    }
    public class DoctorMasterModel
    {
        public int DoctorId { get; set; }
        public string Name { get; set; }
        public int SpecializationId { get; set; }
        public int DepartmentId { get; set; }
        public int DepartmentTypeId { get; set; }
        public int CanApproveLabReport { get; set; }
        public byte IsDoctorUnit { get; set; }
        public int DoctorRoleId { get; set; }
        public string DoctorRole { get; set; }
    }

    public class LocationByPincodeModel
    {
        public int CountryId { get; set; }
        public string CountryName { get; set; }
        public int StateId { get; set; }
        public string StateName { get; set; }
        public int DistrictId { get; set; }
        public string DistrictName { get; set; }
        public int CityId { get; set; }
        public string CityName { get; set; }
        public int Pincode { get; set; }
    }

    public class CategoryTypeModel
    {
        public int CategoryTypeId { get; set; }
        public string CategoryTypeName { get; set; }
    }

    public class CategoryModel
    {
        public int CategoryId { get; set; }
        public string CategoryName { get; set; }
        public int CategoryTypeId { get; set; }
        public string CategoryTypeName { get; set; }
        public string? CreatedBy { get; set; }
        public string? CreatedOn { get; set; }
        public string? LastModifiedBy { get; set; }
        public string? LastModifiedOn { get; set; }
    }

    public class SubCategoryModel
    {
        public int CategoryId { get; set; }
        public int SubCategoryId { get; set; }
        public string SubCategoryName { get; set; }
        public int LabTypeId { get; set; }
    }

    public class SubSubCategoryModel
    {
        public int SubCategoryId { get; set; }
        public int SubSubCategoryId { get; set; }
        public string SubSubCategoryName { get; set; }
        public int? PrintGroupId { get; set; }
        public int? DepartmentId { get; set; }
    }

    public class PaymentModeMasterModel
    {
        public int PaymentModeId { get; set; }
        public string PaymentModeName { get; set; }
        public string PayModeType { get; set; }
        public int PayModeTypeId { get; set; }
        public int IsRefundAllowed { get; set; }
        public int IsActive { get; set; }
    }

    public class CorporatePaymentModeModel
    {
        public int PaymentModeId { get; set; }
        public string PaymentModeName { get; set; }
        public string PayModeType { get; set; }
        public int PayModeTypeId { get; set; }
        public int ShowBankField { get; set; }
        public int ShowReferenceNumberField { get; set; }
        public int IsExcludedFromPaymentList { get; set; }
    }

    public class DiscountApprovalModel
    {
        public int Id { get; set; }
        public string Name { get; set; }
       
    }

    public class PatientInvestigationReportPdfResult
    {
        public byte[] Content { get; set; } = [];
        public string FileName { get; set; } = "PatientReport.pdf";
    }

    public class PredefineQueryConfig
    {
        public int QueryId { get; set; }
        public string QueryName { get; set; }
        public string SPName { get; set; }
        public string Filter1 { get; set; }
        public string Filter2 { get; set; }
        public string Filter3 { get; set; }
        public string Filter4 { get; set; }
        public string Filter5 { get; set; }
    }
}
