namespace HISWEBAPI.Models
{
    public class PatientMasterModel
    {
        public int PatientId { get; set; }
        public int BranchId { get; set; }
        public string Uhid { get; set; }
        public string Title { get; set; }
        public string FirstName { get; set; }
        public string? MiddleName { get; set; }
        public string? LastName { get; set; }
        public string PatientName { get; set; }
        public int? AgeYears { get; set; }
        public int? AgeMonths { get; set; }
        public int? AgeDays { get; set; }
        public string? Age { get; set; }
        public string? Dob { get; set; }
        public string? Gender { get; set; }
        public string? MaritalStatus { get; set; }
        public string? Relation { get; set; }
        public string? RelativeName { get; set; }
        public string? IdProofName { get; set; }
        public string? IdProofNumber { get; set; }
        public string? ContactNumber { get; set; }
        public string? EmergencyContactNumber { get; set; }
        public string? Email { get; set; }
        public string? PrivilegedCardNumber { get; set; }
        public string? Address { get; set; }
        public int? CountryId { get; set; }
        public string? Country { get; set; }
        public int? StateId { get; set; }
        public string? State { get; set; }
        public int? DistrictId { get; set; }
        public string? District { get; set; }
        public int? CityId { get; set; }
        public string? City { get; set; }
        public int? InsuranceCompanyId { get; set; }
        public int? CorporateId { get; set; }
        public string? CardNo { get; set; }
        public int? IsVaccination { get; set; }
        public int? VIPPatient { get; set; }
        public string? PatientImagePath { get; set; }
        public string? PolicyNo { get; set; }
        public string? PolicyCardNo { get; set; }
        public string? ExpiryDate { get; set; }
        public string? CardHolder { get; set; }
        public string? ReferalNo { get; set; }
        public string? ReferalDate { get; set; }
        public string? LandlineNo { get; set; }
        public string? BirthPlace { get; set; }
        public string? Religion { get; set; }
        public string? RelationPhone { get; set; }
        public int? RelationAge { get; set; }
        public string? RelationGender { get; set; }
        public string? EMG_FirstName { get; set; }
        public string? EMG_LastName { get; set; }
        public string? EMG_Relation { get; set; }
        public string? EMG_MobileNo { get; set; }
        public string? EMG_ResidentNo { get; set; }
        public string? EMG_Address { get; set; }
        public int? IsInternational { get; set; }
        public string? Locality { get; set; }
        public string? PassportNumber { get; set; }
        public string? InternationalNo { get; set; }
        public string? MembershipNo { get; set; }
        public string? PatientType { get; set; }
        public string? IdentityMark { get; set; }
        public string? IdentityMark2 { get; set; }
        public string? ReferenceType { get; set; }
        public string? Remarks { get; set; }
        public int DoctorId { get; set; } = 0;
        public int IPDVisitId { get; set; } = 0;
        public string? IPDNo { get; set; }
        public string? DayCareNo { get; set; }
        public string? DialysisNo { get; set; }
        public string? EmergencyNo { get; set; }
        public int IsRegistrationValid { get; set; }
    }

    public class SearchPatientMasterModel
    {
        public int PatientId { get; set; }
        public int BranchId { get; set; }
        public string Uhid { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string? MiddleName { get; set; }
        public string? LastName { get; set; }
        public string PatientName { get; set; } = string.Empty;
        public int? AgeYears { get; set; }
        public int? AgeMonths { get; set; }
        public int? AgeDays { get; set; }
        public string? Age { get; set; }
        public string? Dob { get; set; }
        public string? Gender { get; set; }
        public string? Relation { get; set; }
        public string? RelativeName { get; set; }
        public string? ContactNumber { get; set; }
        public string? EmergencyContactNumber { get; set; }
        public string? Email { get; set; }
        public string? FullAddress { get; set; }
        public string? RegistrationDate { get; set; }
        public string? IPDNo { get; set; }
    }

    public class ServiceBillingDetailsModel
    {
        public decimal Rate { get; set; }
        public int RateListId { get; set; }
        public int IsRateEditable { get; set; }
        public string ServiceName { get; set; }
        public string Code { get; set; }
        public string CorporateAlias { get; set; }
        public string CorporateCode { get; set; }
        public int ValidityDays { get; set; }
        public decimal DiscountPer { get; set; }
        public string DiscountReason { get; set; }
        public int IsNonPayable { get; set; }
        public int ServiceItemId { get; set; }
        public int CorporateId { get; set; }
        public int CategoryTypeId { get; set; }
        public int CategoryId { get; set; }
        public int SubCategoryId { get; set; }
        public int SubSubCategoryId { get; set; }
        public int IsCorporateDiscount { get; set; }
        public int IsPrivilegedCardDiscount { get; set; }
        public decimal GSTPer { get; set; }
        public int SampleTypeId { get; set; }
        public int ReportTypeId { get; set; }
        public string DoctorDepartmentIds { get; set; }
        public int IsRequiredSeparatePerformingDoctor { get; set; }
        public int LabTypeId { get; set; }
        public int TatTimeInMin { get; set; }
    }

    public class OPDVisitSummaryModel
    {
        public int VisitId { get; set; }
        public string BillNo { get; set; }
        public string BillDate { get; set; }
        public int PatientId { get; set; }
        public string Uhid { get; set; }
        public string Type { get; set; }
        public int TypeId { get; set; }
        public int VisitNo { get; set; }
        public string CurrentAge { get; set; }
        public int DoctorId { get; set; }
        public int CorporateId { get; set; }
        public int InsuranceCompanyId { get; set; }
        public decimal TotalBillAmount { get; set; }
        public decimal TotalDiscountPerOnBill { get; set; }
        public decimal TotalDiscountAmountOnBill { get; set; }
        public decimal RoundOff { get; set; }
        public decimal TotalPayableAmount { get; set; }
        public decimal TotalPaidAmount { get; set; }
        public decimal TotalBalanceAmount { get; set; }
        public string CreatedOn { get; set; }
    }

    public class PackageAllDetailsModel
    {
        public int PackageId { get; set; }
        public string PackageName { get; set; }
        public string PackageCode { get; set; }
        public int IsActive { get; set; }
        public int SubSubCategoryId { get; set; }
        public int SubCategoryId { get; set; }
        public int CategoryId { get; set; }
        public string StartsFrom { get; set; }
        public string ExpiresOn { get; set; }
        public string PackageServiceNameCode { get; set; }
        public string PackageServiceName { get; set; }
        public string PackageServiceCode { get; set; }
        public int PackageServiceId { get; set; }
        public int QTY { get; set; }
        public int PackageServiceCategoryTypeId { get; set; }
        public string PackageServiceCategory { get; set; }
        public int PackageServiceCategoryId { get; set; }
        public int PackageServiceSubCategoryId { get; set; }
        public int PackageServiceSubSubCategoryId { get; set; }
        public int IsMultipleVisitAllow { get; set; }
        public int VisitDuration { get; set; }
        public string VisitDurationType { get; set; }

    }


}