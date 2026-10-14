using System.ComponentModel.DataAnnotations;

namespace HISWEBAPI.Models
{
    public class AllGlobalValues
    {
        public int hospId { get; set; }
        public int userId { get; set; }
        public string? userName { get; set; }
        public string? name { get; set; }
        public string? ipAddress { get; set; }
    }
    public class BranchModel
    {
        public required int branchId { get; set; }
        public required string branchName { get; set; }
    }

    public class MobileAppSettingsResponse
    {
        public bool IsMobileAppActive { get; set; }
        public string ApiBaseUrl { get; set; }
    }

    public class PickListModel
    {
       
        public required string value { get; set; }
        public required string key { get; set; }
    }


    public class RoleMasterModel
    {
        public int RoleId { get; set; }
        public required string RoleName { get; set; }
        public int FaIconId { get; set; }
        public int IsActive { get; set; }
        public string? IconClass { get; set; }
        public string? IconName { get; set; }
        public string? ImagePath { get; set; }
        public string? CreatedBy { get; set; }
        public string? CreatedOn { get; set; }
        public string? LastModifiedBy { get; set; }
        public string? LastModifiedOn { get; set; }
    }

    public class FaIconModel
    {
        public int Id { get; set; }
        public string? IconClass { get; set; }
        public string? IconName { get; set; }
      
    }

  

    public class UserMasterModel
    {
        public int Id { get; set; }
        public string? FirstName { get; set; }
        public string? MidelName { get; set; }
        public string? LastName { get; set; }
        public string? DOB { get; set; }
        public string? Gender { get; set; }
        public required string UserName { get; set; }
        public string? Password { get; set; }
        public string? Address { get; set; }
        public string? Contact { get; set; }
        public string? Email { get; set; }
        public int IsActive { get; set; }
        public string? EmployeeID { get; set; }
        public string? CreatedBy { get; set; }
        public string? CreatedOn { get; set; }
        public string? LastModifiedBy { get; set; }
        public string? LastModifiedOn { get; set; }
        public int? ReportToUserId { get; set; }
        public int? UserDepartmentId { get; set; }
        public int? UserRoleId { get; set; }
        public string? UserRole { get; set; }
    }


    public class UserDepartmentMasterModel
    {
        public int Id { get; set; }
        public required string DepartmentName { get; set; }
        public int IsActive { get; set; }
        public string? CreatedBy { get; set; }
        public string? CreatedOn { get; set; }
        public string? LastModifiedBy { get; set; }
        public string? LastModifiedOn { get; set; }
        public string? IPAddress { get; set; }
    }

    public class UserGroupMasterModel
    {
        public int Id { get; set; }
        public required string GroupName { get; set; }
        public int IsActive { get; set; }
        public string? CreatedBy { get; set; }
        public string? CreatedOn { get; set; }
        public string? LastModifiedBy { get; set; }
        public string? LastModifiedOn { get; set; }
        public string? IPAddress { get; set; }
    }

    public class UserGroupMembersModel
    {
        public int isGranted { get; set; }
        public int GroupId { get; set; }
        public int UserId { get; set; }
        public string? GroupName { get; set; }
        public string? UserName { get; set; }
       
    }

    public class UserRoleMappingModel
    {
        public int isGranted { get; set; }
        public required string RoleName { get; set; }
        public int RoleId { get; set; }
    }


    public class UserRightMappingModel
    {
        public int IsGranted { get; set; }
        public required string UserRightName { get; set; }
        public string? Description { get; set; }
        public int UserRightId { get; set; }
    }

    public class DashboardUserRightMappingModel
    {
        public int IsGranted { get; set; }
        public required string UserRightName { get; set; }
        public string? Details { get; set; }
        public int UserRightId { get; set; }
    }


    public class NavigationTabMasterModel
    {
        public int TabId { get; set; }
        public required string TabName { get; set; }
        public int FaIconId { get; set; }
        public int IsActive { get; set; }

    }

    public class NavigationSubMenuMasterModel
    {
        public int SubMenuId { get; set; }
        public int TabId { get; set; }
        public string TabName { get; set; }
        public string SubMenuName { get; set; }
        public string URL { get; set; }
        public int IsActive { get; set; }
        public string CreatedBy { get; set; }
        public string CreatedOn { get; set; }
        public string LastModifiedBy { get; set; }
        public string LastModifiedOn { get; set; }
        public string IpAddress { get; set; }
    }

    public class RoleWiseMenuMappingModel
    {
        public int IsGranted { get; set; }
        public int SubMenuId { get; set; }
        public int TabId { get; set; }
        public required string SubMenuName { get; set; }
        public required string TabName { get; set; }
        public int IsActive { get; set; }
    }


    public class UserWiseMenuMasterModel
    {
        public int IsGranted { get; set; }
        public int SubMenuId { get; set; }
        public int TabId { get; set; }
        public required string SubMenuName { get; set; }
        public required string TabName { get; set; }
        public int IsActive { get; set; }
    }
    public class UserWiseCorporateMappingModel
    {
        public int IsGranted { get; set; }
        public int CorporateId { get; set; }
        public required string CorporateName { get; set; }
        public int IsActive { get; set; }
    }

    public class UserWiseBedMappingModel
    {
        public int IsGranted { get; set; }
        public int ServiceItemId { get; set; }
        public required string Name { get; set; }

    }

    public class BranchMasterModel
    {
        public int BranchId { get; set; }
        public string BranchName { get; set; }
        public string BranchCode { get; set; }
        public string Email { get; set; }
        public string ContactNo1 { get; set; }
        public string ContactNo2 { get; set; }
        public string Address { get; set; }
        public int IsActive { get; set; }
        public string FYStartMonth { get; set; }
        public int DefaultCountryId { get; set; }
        public int DefaultStateId { get; set; }
        public int DefaultDistrictId { get; set; }
        public int DefaultCityId { get; set; }
        public int DefaultInsuranceCompanyId { get; set; }
        public int DefaultCorporateId { get; set; }
        public int ApplyDiscountApproval { get; set; }
        public int SeparateCollectionCounter { get; set; }
    }

    public class HeaderMasterModel
    {
        public int HeaderId { get; set; }
        public string HeaderBody { get; set; }
        public int IsActive { get; set; }
   
    }

    public class SequenceTypeMasterModel
    {
        public int TypeId { get; set; }
        public string TypeName { get; set; }
    }

    public class SequenceMasterModel
    {
        public int SequenceId { get; set; }
        public string Name { get; set; }
        public int TypeId { get; set; }
        public string TypeName { get; set; }
        public string Prefix { get; set; }
        public string FirstSeprator { get; set; }
        public int FYFormatId { get; set; }
        public string FYFormat { get; set; }
        public string SecondSeprator { get; set; }
        public int Length { get; set; }
        public string Preview { get; set; }
    }

    public class BranchSequenceMappingModel
    {
        public int MappingId { get; set; }
        public int BranchId { get; set; }
        public string BranchName { get; set; }
        public int RoleId { get; set; }
        public string RoleName { get; set; }
        public int TypeId { get; set; }
        public string TypeName { get; set; }
        public int SequenceId { get; set; }
        public string SequencePreview { get; set; }
        public string CreatedBy { get; set; }
        public string CreatedOn { get; set; }
        public string LastModifiedBy { get; set; }
        public string LastModifiedOn { get; set; }
    }

    public class LabReportLetterHeadMaster
    {
        public int Id { get; set; }
        public int BranchId { get; set; }
        public string BranchName { get; set; }
        public int TypeId { get; set; }
        public string TypeName { get; set; }
        public int PaddingLeft { get; set; }
        public int PaddingRight { get; set; }
        public int PaddingTop { get; set; }
        public int PaddingBottom { get; set; }
        public string LetterHeadFilePath { get; set; }
        public int IsActive { get; set; }

    }

    public class DoctorSignatureMaster
    {
        public int Id { get; set; }
        public int BranchId { get; set; }
        public string BranchName { get; set; }
        public int DoctorId { get; set; }
        public string DoctorName { get; set; }
        public int XSign { get; set; }
        public int YSign { get; set; }
        public string DocSignPath { get; set; }
    }

    public class BankMasterModel
    {
        public int BankId { get; set; }
        public string BankName { get; set; }
        public int IsActive { get; set; }
        public string CreatedBy { get; set; }
        public string CreatedOn { get; set; }
        public string LastModifiedBy { get; set; }
        public string LastModifiedOn { get; set; }
    }

    public class BankDetailMasterModel
    {
        public int Id { get; set; }
        public string PayeeName { get; set; }
        public string PANNumber { get; set; }
        public string BankName { get; set; }
        public string BankAccountNumber { get; set; }
        public string BankAddress { get; set; }
        public string IFSCCode { get; set; }
        public string PINCode { get; set; }
        public string TINNumber { get; set; }
        public int IsActive { get; set; }
        public string CreatedBy { get; set; }
        public string CreatedOn { get; set; }
        public string LastModifiedBy { get; set; }
        public string LastModifiedOn { get; set; }
    }

    // MRD Room Master Models
    public class MRDRoomMasterModel
    {
        public int RoomId { get; set; }
        public string Name { get; set; }
        public int IsActive { get; set; }
    }

    // MRD Rack Master Models
    public class MRDRackMasterModel
    {
        public int RackId { get; set; }
        public int RoomId { get; set; }
        public string Name { get; set; }
        public int IsActive { get; set; }
    }

    // MRD Shelf Master Models
    public class MRDShelfMasterModel
    {
        public int ShelfId { get; set; }
        public int RoomId { get; set; }
        public int RackId { get; set; }
        public string Name { get; set; }
        public int IsActive { get; set; }
    }

    public class PatientDocumentMasterModel
    {
        public int DocumentId { get; set; }
        public string DocumentName { get; set; }
        public string DocumentCode { get; set; }
        public int IsActive { get; set; }
        public string CreatedBy { get; set; }
        public string CreatedOn { get; set; }
        public string LastModifiedBy { get; set; }
        public string LastModifiedOn { get; set; }
        public int DocumentCategoryId { get; set; }
        public string DocumentCategory { get; set; }
        public int IsMandatory { get; set; } = 0;
    }

    public class OutSourceLabMasterModel
    {
        public int OutSourceLabId { get; set; }
        public string OutSourceLab { get; set; }
        public int BranchId { get; set; }
        public string BranchName { get; set; }
        public string ContactPerson { get; set; }
        public string ContactNumber { get; set; }
        public string Address { get; set; }
        public int IsActive { get; set; }
    }

    public class RateListMasterModel
    {
        public int RateListId { get; set; }
        public string RateListName { get; set; }
        public string ApplicableDate { get; set; }
        public string ExpiryDate { get; set; }
        public int IsActive { get; set; }
    }
    public class InsuranceCompanyMasterModel
    {
        public int InsuranceCompanyId { get; set; }
        public string InsuranceCompanyName { get; set; }
    }

    public class CorporateTypeMasterModel
    {
        public int CorporateTypeId { get; set; }
        public string CorporateTypeName { get; set; }
    }

    public class CorporateMasterDetailModel
    {
        public int CorporateId { get; set; }
        public string CorporateName { get; set; }
        public string InsuranceCompanyName { get; set; }
        public int InsuranceCompanyId { get; set; }
        public int CorporateTypeId { get; set; }
        public int PaymentTypeId { get; set; }
        public string CorporateCode { get; set; }
        public string CorporateContact1 { get; set; }
        public string CorporateContact2 { get; set; }
        public string CorporateEmail { get; set; }
        public string CorporateAddress1 { get; set; }
        public string CorporateAddress2 { get; set; }
        public int IsActive { get; set; }
        public string ContractStartFrom { get; set; }
        public string ContractExpiresOn { get; set; }
        public decimal CopaymentPer { get; set; }
        public decimal DiscountPerOut { get; set; }
        public decimal DiscountPerIn { get; set; }
        public decimal HikePerOut { get; set; }
        public decimal HikePerIn { get; set; }
        public string ActivePaymentModes { get; set; }
        public int IsRegistrationChargeApplicable { get; set; }
        public int IsCaseBillingApplicable { get; set; }


    }

    public class DiscountApprovalMasterModel
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int IsActive { get; set; }
        public string DiscountType { get; set; }
        public string BranchName { get; set; }
        public string FirstName { get; set; }
    }


    // ─── Doctor Header Master ─────────────────────────────────────────────────────

    public class DoctorHeaderMasterModel
    {
        public int HeaderId { get; set; }
        public string HeaderName { get; set; }
        public string DisplayName { get; set; }
        public string ControlType { get; set; }
        public int? ControlTypeId { get; set; }
        public int IsPrint { get; set; }
        public int IsShowInTempRoom { get; set; }
        public int UsedForPatientType { get; set; }
        public int IsMandatory { get; set; }
        public string UsedForPatientTypeName { get; set; }
        public int IsActive { get; set; }
        public string Queries { get; set; }

    }

    // ─── Doctor Header LOV ────────────────────────────────────────────────────────

    public class DoctorHeaderLOVModel
    {
        public string Value { get; set; }
        public int DataTypeId { get; set; }
        public string HeaderName { get; set; }
        public string Options { get; set; }
        public int Score { get; set; }
        public string Base64Data { get; set; }
        public string Description { get; set; }

    }

    // ─── Doctor Header Mapping (for master screen) ────────────────────────────────

    public class DoctorHeaderMappingModel
    {
        public int HeaderId { get; set; }
        public string HeaderName { get; set; }
        public string DisplayName { get; set; }
        public string ControlType { get; set; }
        public long MappingId { get; set; }
        public int SequenceNo { get; set; }
    }

    public class BlockMasterModel
    {
        public int BlockId { get; set; }
        public string BlockName { get; set; }

    }

    public class FloorMasterModel
    {
        public int FloorId { get; set; }
        public string FloorName { get; set; }

    }


}
