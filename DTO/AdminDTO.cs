using System.ComponentModel.DataAnnotations;
using HISWEBAPI.Attributes;

namespace HISWEBAPI.DTO
{
    public class PageConfigRequest
    {
       
        public int Id { get; set; } = 0;

        [Required(ErrorMessage = "ConfigKey is required")]
        [StringLength(256, ErrorMessage = "ConfigKey cannot exceed 256 characters")]
        public string ConfigKey { get; set; }

        [Required(ErrorMessage = "ConfigJson is required")]
        public string ConfigJson { get; set; }

        public bool IsActive { get; set; } = true;
    }

    public class PageConfigResponse
    {
        public int Id { get; set; }
        public string ConfigKey { get; set; }
        public string ConfigJson { get; set; }
       
    }

    
    public class GetPageConfigRequest
    {
        [StringLength(256, ErrorMessage = "ConfigKey cannot exceed 256 characters")]
        public string ConfigKey { get; set; }
    }

    public class RoleMasterRequest
    {
        public int RoleId { get; set; } = 0;

        [Required(ErrorMessage = "Role name is required")]
        [StringLength(256, ErrorMessage = "Role name cannot exceed 256 characters")]
        public string RoleName { get; set; }

        [Required(ErrorMessage = "IsActive status is required")]
        public int IsActive { get; set; }

        [Required(ErrorMessage = "FaIconId is required")]
        public int FaIconId { get; set; } = 0;

        [Required(ErrorMessage = "Image Path is required")]
        [StringLength(256, ErrorMessage = "Image Path cannot exceed 256 characters")]
        public string ImagePath { get; set; }
    }

    public class UserMasterRequest
    {
        public int userId { get; set; } = 0;

        [Required(ErrorMessage = "First name is required")]
        [StringLength(100)]
        public string FirstName { get; set; }

        [StringLength(100)]
        public string MiddleName { get; set; }

        [StringLength(100)]
        public string LastName { get; set; }

        [Required(ErrorMessage = "Username is required")]
        [StringLength(50)]
        public string UserName { get; set; }

        [Required(ErrorMessage = "Password is required")]
        [PasswordPolicy]
        public string Password { get; set; }

        [Required(ErrorMessage = "Confirm password is required")]
        [Compare("Password", ErrorMessage = "Password and confirm password do not match")]
        public string ConfirmPassword { get; set; }

        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email format")]
        [StringLength(100)]
        public string Email { get; set; }

        [Required(ErrorMessage = "Contact is required")]
        [RegularExpression(@"^\d{10}$", ErrorMessage = "Contact must be exactly 10 digits")]
        public string Contact { get; set; }

        [StringLength(500)]
        public string Address { get; set; }

        [Required(ErrorMessage = "Date of birth is required")]
        public DateTime DOB { get; set; }

        [Required(ErrorMessage = "Gender is required")]
        [RegularExpression("^(Male|Female|Other)$", ErrorMessage = "Gender must be Male, Female, or Other")]
        public string Gender { get; set; }
        public int IsActive { get; set; }
        public string EmployeeID { get; set; }
        public int ReportToUserId { get; set; }
        public int UserDepartmentId { get; set; }

    }

    public class UserMasterResponse
    {
        public long userId { get; set; }
    }

    public class UserDepartmentRequest
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Department name is required")]
        [StringLength(200, ErrorMessage = "Department name cannot exceed 200 characters")]
        public required string DepartmentName { get; set; }

        [Required(ErrorMessage = "IsActive is required")]
        public int IsActive { get; set; }
    }

    public class UserGroupRequest
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Group name is required")]
        [StringLength(200, ErrorMessage = "Group name cannot exceed 200 characters")]
        public required string GroupName { get; set; }

        [Required(ErrorMessage = "IsActive is required")]
        public int IsActive { get; set; }
    }

    public class UserGroupMembersRequest
    {
        [Required(ErrorMessage = "GroupId is required")]
        public int GroupId { get; set; }

        [Required(ErrorMessage = "UserIds are required")]
        public required List<int> UserIds { get; set; }
    }

    public class UserRoleMappingRequest
    {
        [Required(ErrorMessage = "UserId is required")]
        public int userId { get; set; }

        [Required(ErrorMessage = "BranchId is required")]
        public int branchId { get; set; }

        [Required(ErrorMessage = "TypeId is required")]
        public int typeId { get; set; }

        [Required(ErrorMessage = "RoleId is required")]
        public int roleId { get; set; }
    }

    public class UserRoleMappingListRequest
    {
        [Required(ErrorMessage = "UserId is required")]
        public int userId { get; set; }

        [Required(ErrorMessage = "BranchId is required")]
        public int branchId { get; set; }

        [Required(ErrorMessage = "TypeId is required")]
        public int typeId { get; set; }

        public List<UserRoleMappingRequest>? userRoleMappings { get; set; }
    }


    public class UserRightsRequest
    {
        [Required(ErrorMessage = "TypeId is required")]
        public int TypeId { get; set; }

        [Required(ErrorMessage = "UserId is required")]
        public int UserId { get; set; }

        [Required(ErrorMessage = "BranchId is required")]
        public int BranchId { get; set; }

        [Required(ErrorMessage = "RoleId is required")]
        public int RoleId { get; set; }

        [Required(ErrorMessage = "UserRightId is required")]
        public int UserRightId { get; set; }
    }

    public class SaveUserRightMappingRequest
    {
        [Required(ErrorMessage = "TypeId is required")]
        public int TypeId { get; set; }

        [Required(ErrorMessage = "UserId is required")]
        public int UserId { get; set; }

        [Required(ErrorMessage = "BranchId is required")]
        public int BranchId { get; set; }

        [Required(ErrorMessage = "RoleId is required")]
        public int RoleId { get; set; }

        public List<UserRightsRequest> UserRights { get; set; } = new List<UserRightsRequest>();
    }


    public class DashboardUserRightsRequest
    {
        [Required(ErrorMessage = "TypeId is required")]
        public int TypeId { get; set; }

        [Required(ErrorMessage = "UserId is required")]
        public int UserId { get; set; }

        [Required(ErrorMessage = "BranchId is required")]
        public int BranchId { get; set; }

        [Required(ErrorMessage = "RoleId is required")]
        public int RoleId { get; set; }

        [Required(ErrorMessage = "UserRightId is required")]
        public int UserRightId { get; set; }
    }

    public class SaveDashboardUserRightMappingRequest
    {
        [Required(ErrorMessage = "TypeId is required")]
        public int TypeId { get; set; }

        [Required(ErrorMessage = "UserId is required")]
        public int UserId { get; set; }

        [Required(ErrorMessage = "BranchId is required")]
        public int BranchId { get; set; }

        [Required(ErrorMessage = "RoleId is required")]
        public int RoleId { get; set; }

        public List<DashboardUserRightsRequest> DashboardUserRights { get; set; } = new List<DashboardUserRightsRequest>();
    }

    public class NavigationTabMasterRequest
    {
        public int TabId { get; set; } = 0;

        [Required(ErrorMessage = "Tab name is required")]
        [StringLength(100, ErrorMessage = "Tab name cannot exceed 100 characters")]
        public string TabName { get; set; }

        [Required(ErrorMessage = "FaIconId is required")]
        public int FaIconId { get; set; }
    }
    public class NavigationTabMasterResponse
    {
        public int TabId { get; set; }
    }



    public class NavigationSubMenuMasterRequest
    {
        public int SubMenuId { get; set; } = 0;

        [Required(ErrorMessage = "TabId is required")]
        public int TabId { get; set; }

        [Required(ErrorMessage = "Sub menu name is required")]
        [StringLength(512, ErrorMessage = "Sub menu name cannot exceed 512 characters")]
        public string SubMenuName { get; set; }

        [Required(ErrorMessage = "URL is required")]
        public string URL { get; set; }

        public bool IsActive { get; set; } = true;
    }

    public class NavigationSubMenuMasterResponse
    {
        public int SubMenuId { get; set; }
    }



    public class RoleWiseMenuMappingRequest
    {
        [Required(ErrorMessage = "RoleId is required")]
        public int RoleId { get; set; }

        [Required(ErrorMessage = "BranchId is required")]
        public int BranchId { get; set; }

        [Required(ErrorMessage = "SubMenuId is required")]
        public int SubMenuId { get; set; }
    }

    public class SaveRoleWiseMenuMappingRequest
    {
        [Required(ErrorMessage = "BranchId is required")]
        public int BranchId { get; set; }

        [Required(ErrorMessage = "RoleId is required")]
        public int RoleId { get; set; }

        [Required(ErrorMessage = "IsFirst is required")]
        public int IsFirst { get; set; }

        public List<RoleWiseMenuMappingRequest> MenuMappings { get; set; } = new List<RoleWiseMenuMappingRequest>();
    }

    public class GetRoleWiseMenuMappingRequest
    {
        [Required(ErrorMessage = "BranchId is required")]
        public int BranchId { get; set; }
       
        [Required(ErrorMessage = "RoleId is required")]
        public int RoleId { get; set; }
    }


    public class UserMenuMasterRequest
    {
        [Required(ErrorMessage = "TypeId is required")]
        public int TypeId { get; set; }

        [Required(ErrorMessage = "UserId is required")]
        public int UserId { get; set; }

        [Required(ErrorMessage = "RoleId is required")]
        public int RoleId { get; set; }

        [Required(ErrorMessage = "BranchId is required")]
        public int BranchId { get; set; }

        [Required(ErrorMessage = "SubMenuId is required")]
        public int SubMenuId { get; set; }
    }

    public class SaveUserMenuMasterRequest
    {
        [Required(ErrorMessage = "TypeId is required")]
        public int TypeId { get; set; }

        [Required(ErrorMessage = "UserId is required")]
        public int UserId { get; set; }

        [Required(ErrorMessage = "BranchId is required")]
        public int BranchId { get; set; }

        [Required(ErrorMessage = "RoleId is required")]
        public int RoleId { get; set; }

        [Required(ErrorMessage = "IsFirst is required")]
        public int IsFirst { get; set; }

        public List<UserMenuMasterRequest> UserMenus { get; set; } = new List<UserMenuMasterRequest>();
    }

    public class GetUserWiseMenuMasterRequest
    {
        [Required(ErrorMessage = "TypeId is required")]
        public int TypeId { get; set; }

        [Required(ErrorMessage = "BranchId is required")]
        public int BranchId { get; set; }

        [Required(ErrorMessage = "UserId is required")]
        public int UserId { get; set; }

        [Required(ErrorMessage = "RoleId is required")]
        public int RoleId { get; set; }
    }

    public class UserCorporateMappingRequest
    {
        [Required(ErrorMessage = "TypeId is required")]
        public int TypeId { get; set; }

        [Required(ErrorMessage = "UserId is required")]
        public int UserId { get; set; }

        [Required(ErrorMessage = "BranchId is required")]
        public int BranchId { get; set; }

        [Required(ErrorMessage = "CorporateId is required")]
        public int CorporateId { get; set; }
    }

    public class SaveUserCorporateMappingRequest
    {
        [Required(ErrorMessage = "TypeId is required")]
        public int TypeId { get; set; }

        [Required(ErrorMessage = "UserId is required")]
        public int UserId { get; set; }

        [Required(ErrorMessage = "BranchId is required")]
        public int BranchId { get; set; }

        [Required(ErrorMessage = "IsFirst is required")]
        public int IsFirst { get; set; }

        public List<UserCorporateMappingRequest> UserCorporates { get; set; } = new List<UserCorporateMappingRequest>();
    }

    public class GetUserWiseCorporateMappingRequest
    {
        [Required(ErrorMessage = "TypeId is required")]
        public int TypeId { get; set; }

        [Required(ErrorMessage = "BranchId is required")]
        public int BranchId { get; set; }

        [Required(ErrorMessage = "UserId is required")]
        public int UserId { get; set; }
    }

    public class UserBedMappingRequest
    {
        [Required(ErrorMessage = "TypeId is required")]
        public int TypeId { get; set; }

        [Required(ErrorMessage = "UserId is required")]
        public int UserId { get; set; }

        [Required(ErrorMessage = "BranchId is required")]
        public int BranchId { get; set; }

        [Required(ErrorMessage = "ServiceItemId is required")]
        public int ServiceItemId { get; set; }
    }

    public class SaveUserBedMappingRequest
    {
        [Required(ErrorMessage = "TypeId is required")]
        public int TypeId { get; set; }

        [Required(ErrorMessage = "UserId is required")]
        public int UserId { get; set; }

        [Required(ErrorMessage = "BranchId is required")]
        public int BranchId { get; set; }

        [Required(ErrorMessage = "IsFirst is required")]
        public int IsFirst { get; set; }

        public List<UserBedMappingRequest> UserBeds { get; set; } = new List<UserBedMappingRequest>();
    }

    public class GetUserWiseBedMappingRequest
    {
        [Required(ErrorMessage = "TypeId is required")]
        public int TypeId { get; set; }

        [Required(ErrorMessage = "BranchId is required")]
        public int BranchId { get; set; }

        [Required(ErrorMessage = "UserId is required")]
        public int UserId { get; set; }
    }

    public class BranchMasterRequest
    {
        public int BranchId { get; set; } = 0;

        [Required(ErrorMessage = "Branch name is required")]
        [StringLength(256, ErrorMessage = "Branch name cannot exceed 256 characters")]
        public string BranchName { get; set; }

        [Required(ErrorMessage = "Branch code is required")]
        [StringLength(10, ErrorMessage = "Branch code cannot exceed 10 characters")]
        public string BranchCode { get; set; }

        [EmailAddress(ErrorMessage = "Invalid email format")]
        [StringLength(100)]
        public string Email { get; set; }

        [Required(ErrorMessage = "Contact number 1 is required")]
        [StringLength(15)]
        [RegularExpression(@"^\d{10}$", ErrorMessage = "Contact must be exactly 10 digits")]
        public string ContactNo1 { get; set; }

        [StringLength(15)]
        public string ContactNo2 { get; set; }

        public string Address { get; set; }

        [Required(ErrorMessage = "IsActive is required")]
        public int IsActive { get; set; }

        [Required(ErrorMessage = "Financial year start is required")]
        [StringLength(20)]
        public string FYStartFrom { get; set; }

      
    }

    public class BranchMasterResponse
    {
        public int BranchId { get; set; }
    }

    public class CreateUpdateStateMasterRequest
    {
        public int StateId { get; set; } = 0; // 0 or empty = create, >0 = update

        [Required(ErrorMessage = "CountryId is required")]
        public int CountryId { get; set; }

        [Required(ErrorMessage = "StateName is required")]
        [StringLength(100, ErrorMessage = "StateName cannot exceed 100 characters")]
        public string StateName { get; set; }

        [Required(ErrorMessage = "IsActive is required")]
        public int IsActive { get; set; }
    }

    // Create/Update District Master Request
    public class CreateUpdateDistrictMasterRequest
    {
        public int DistrictId { get; set; } = 0; // 0 or empty = create, >0 = update

        [Required(ErrorMessage = "StateId is required")]
        public int StateId { get; set; }

        [Required(ErrorMessage = "CountryId is required")]
        public int CountryId { get; set; }

        [Required(ErrorMessage = "DistrictName is required")]
        [StringLength(100, ErrorMessage = "DistrictName cannot exceed 100 characters")]
        public string DistrictName { get; set; }

        [Required(ErrorMessage = "IsActive is required")]
        public int IsActive { get; set; }
    }

    // Create/Update City Master Request
    public class CreateUpdateCityMasterRequest
    {
        public int CityId { get; set; } = 0; // 0 or empty = create, >0 = update

        [Required(ErrorMessage = "DistrictId is required")]
        public int DistrictId { get; set; }

        [Required(ErrorMessage = "StateId is required")]
        public int StateId { get; set; }

        [Required(ErrorMessage = "CountryId is required")]
        public int CountryId { get; set; }

        [Required(ErrorMessage = "CityName is required")]
        [StringLength(100, ErrorMessage = "CityName cannot exceed 100 characters")]
        public string CityName { get; set; }


        [Required(ErrorMessage = "IsActive is required")]
        public int IsActive { get; set; }
    }

    public class CreateUpdatePincodeMasterRequest
    {
        public int PincodeId { get; set; } = 0; // 0 = create, >0 = update

        [Required(ErrorMessage = "CityId is required")]
        public int CityId { get; set; }

        [Required(ErrorMessage = "Pincode is required")]
        [Range(100000, 999999, ErrorMessage = "Pincode must be exactly 6 digits")]
        public int Pincode { get; set; }

        [Required(ErrorMessage = "IsActive is required")]
        public int IsActive { get; set; }
    }

    public class HeaderMasterRequest
    {
        public int HeaderId { get; set; } = 0;

        [Required(ErrorMessage = "RoleId is required")]
        public int RoleId { get; set; }

        [Required(ErrorMessage = "BranchId is required")]
        public int BranchId { get; set; }

        [Required(ErrorMessage = "Type is required")]
        [StringLength(256, ErrorMessage = "Type cannot exceed 256 characters")]
        public string Type { get; set; }

        [Required(ErrorMessage = "TypeId is required")]
        public int TypeId { get; set; }

        [Required(ErrorMessage = "IsHeader is required")]
        public int IsHeader { get; set; }

        public string HeaderBody { get; set; }

        [Required(ErrorMessage = "IsActive is required")]
        public int IsActive { get; set; }
    }

    public class HeaderMasterResponse
    {
        public int HeaderId { get; set; }
    }

    public class GetHeaderMasterRequest
    {
        [Required(ErrorMessage = "BranchId is required")]
        public int BranchId { get; set; }

        [Required(ErrorMessage = "RoleId is required")]
        public int RoleId { get; set; }

        [Required(ErrorMessage = "TypeId is required")]
        public int TypeId { get; set; }

        [Required(ErrorMessage = "IsHeader is required")]
        public int IsHeader { get; set; }
    }

    public class CreateUpdateSequenceMasterRequest
    {
        [Required(ErrorMessage = "SequenceId is required")]
        public int SequenceId { get; set; }

        [Required(ErrorMessage = "Name is required")]
        [StringLength(256, ErrorMessage = "Name cannot exceed 256 characters")]
        public string Name { get; set; }

        [Required(ErrorMessage = "TypeId is required")]
        public int TypeId { get; set; }

        [Required(ErrorMessage = "TypeName is required")]
        [StringLength(100, ErrorMessage = "TypeName cannot exceed 100 characters")]
        public string TypeName { get; set; }

        // Prefix can be blank (empty string), not required
        [StringLength(10, ErrorMessage = "Prefix cannot exceed 10 characters")]
        public string Prefix { get; set; } = string.Empty;

        // FirstSeprator can be blank (empty string), not required
        [StringLength(2, ErrorMessage = "FirstSeprator cannot exceed 2 characters")]
        public string FirstSeprator { get; set; } = string.Empty;

        // FYFormatId can be 0 (no format selected), not required to be > 0
        public int FYFormatId { get; set; } = 0;

        // FYFormat can be blank (empty string), not required
        [StringLength(20, ErrorMessage = "FYFormat cannot exceed 20 characters")]
        public string FYFormat { get; set; } = string.Empty;

        // SecondSeprator can be blank (empty string), not required
        [StringLength(2, ErrorMessage = "SecondSeprator cannot exceed 2 characters")]
        public string SecondSeprator { get; set; } = string.Empty;

        // Length must be greater than 0
        [Required(ErrorMessage = "Length is required")]
        [Range(1, int.MaxValue, ErrorMessage = "Length must be greater than 0")]
        public int Length { get; set; }

        [Required(ErrorMessage = "Preview is required")]
        [StringLength(50, ErrorMessage = "Preview cannot exceed 50 characters")]
        public string Preview { get; set; }
    }

    public class CreateUpdateSequenceMasterResponse
    {
        public int SequenceId { get; set; }
    }

    public class CreateUpdateBranchSequenceMappingRequest
    {
        public int MappingId { get; set; } = 0;

        [Required(ErrorMessage = "BranchId is required")]
        [Range(0, int.MaxValue, ErrorMessage = "BranchId must be greater than or equal to 0")]
        public int BranchId { get; set; }

        [Required(ErrorMessage = "RoleId is required")]
        [Range(0, int.MaxValue, ErrorMessage = "RoleId must be greater than or equal to 0")]
        public int RoleId { get; set; }

        [Required(ErrorMessage = "TypeId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "TypeId must be greater than 0")]
        public int TypeId { get; set; }

        [Required(ErrorMessage = "SequenceId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "SequenceId must be greater than 0")]
        public int SequenceId { get; set; }
    }

    public class CreateUpdateBranchSequenceMappingResponse
    {
        public int MappingId { get; set; }
    }

    public class LabReportLetterHeadRequest
    {
        public int Id { get; set; } = 0;

        [Required(ErrorMessage = "BranchId is required")]
        public int BranchId { get; set; }

        [Required(ErrorMessage = "TypeId is required")]
        public int TypeId { get; set; }

        [Required(ErrorMessage = "TypeName is required")]
        [StringLength(100, ErrorMessage = "TypeName cannot exceed 100 characters")]
        public string TypeName { get; set; }

        [Range(0, 500, ErrorMessage = "PaddingLeft must be between 0 and 500")]
        public int PaddingLeft { get; set; } = 0;

        [Range(0, 500, ErrorMessage = "PaddingRight must be between 0 and 500")]
        public int PaddingRight { get; set; } = 0;

        [Range(0, 500, ErrorMessage = "PaddingTop must be between 0 and 500")]
        public int PaddingTop { get; set; } = 0;

        [Range(0, 500, ErrorMessage = "PaddingBottom must be between 0 and 500")]
        public int PaddingBottom { get; set; } = 0;

        public IFormFile? LetterHeadFile { get; set; }

      
    }

   
    public class LabReportLetterHeadResponse
    {
        public int Id { get; set; }
        public string LetterHeadFilePath { get; set; }
    }
    public class DeleteLetterHeadRequest
    {
        [Required(ErrorMessage = "Id is required")]
        public int Id { get; set; }
    }

    public class DoctorSignatureMasterRequest
    {
        public int Id { get; set; } = 0;

        [Required(ErrorMessage = "BranchId is required")]
        [Range(0, int.MaxValue, ErrorMessage = "BranchId must be greater than or equal to 0")]
        public int BranchId { get; set; }

        [Required(ErrorMessage = "DoctorId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "DoctorId must be greater than 0")]
        public int DoctorId { get; set; }

        [Range(0, 1000, ErrorMessage = "XSign must be between 0 and 1000")]
        public int XSign { get; set; } = 0;

        [Range(0, 1000, ErrorMessage = "YSign must be between 0 and 1000")]
        public int YSign { get; set; } = 0;

        public IFormFile? DocSignFile { get; set; }
    }

    public class DoctorSignatureMasterResponse
    {
        public int Id { get; set; }
        public string DocSignPath { get; set; }
    }

    public class DeleteDoctorSignatureRequest
    {
        [Required(ErrorMessage = "Id is required")]
        public int Id { get; set; }
    }

    public class BankMasterRequest
    {
        public int BankId { get; set; } = 0;

        [Required(ErrorMessage = "Bank name is required")]
        [StringLength(256, ErrorMessage = "Bank name cannot exceed 256 characters")]
        public string BankName { get; set; }

        [Required(ErrorMessage = "IsActive is required")]
        public int IsActive { get; set; }
    }

    public class BankMasterResponse
    {
        public int BankId { get; set; }
    }

    public class BankDetailMasterRequest
    {
        public int BankId { get; set; } = 0;

        [Required(ErrorMessage = "Payee name is required")]
        [StringLength(256, ErrorMessage = "Payee name cannot exceed 256 characters")]
        public string PayeeName { get; set; }

        [Required(ErrorMessage = "PAN number is required")]
        [StringLength(20, ErrorMessage = "PAN number cannot exceed 20 characters")]
        [RegularExpression(@"^[A-Z]{5}[0-9]{4}[A-Z]{1}$", ErrorMessage = "Invalid PAN number format")]
        public string PANNumber { get; set; }

        [Required(ErrorMessage = "Bank name is required")]
        [StringLength(256, ErrorMessage = "Bank name cannot exceed 256 characters")]
        public string BankName { get; set; }

        [Required(ErrorMessage = "Bank account number is required")]
        [StringLength(20, ErrorMessage = "Bank account number cannot exceed 20 characters")]
        public string BankAccountNumber { get; set; }

        [Required(ErrorMessage = "Bank address is required")]
        [StringLength(256, ErrorMessage = "Bank address cannot exceed 256 characters")]
        public string BankAddress { get; set; }

        [Required(ErrorMessage = "IFSC code is required")]
        [StringLength(100, ErrorMessage = "IFSC code cannot exceed 100 characters")]
        [RegularExpression(@"^[A-Z]{4}0[A-Z0-9]{6}$", ErrorMessage = "Invalid IFSC code format")]
        public string IFSCCode { get; set; }

        [Required(ErrorMessage = "PIN code is required")]
        [StringLength(10, ErrorMessage = "PIN code cannot exceed 10 characters")]
        [RegularExpression(@"^\d{6}$", ErrorMessage = "PIN code must be 6 digits")]
        public string PINCode { get; set; }

        [Required(ErrorMessage = "TIN number is required")]
        [StringLength(20, ErrorMessage = "TIN number cannot exceed 20 characters")]
        public string TINNumber { get; set; }

        [Required(ErrorMessage = "IsActive is required")]
        public int IsActive { get; set; }
    }

    public class BankDetailMasterResponse
    {
        public int BankId { get; set; }
    }

    // MRD Room Master DTOs
    public class MRDRoomMasterRequest
    {
        public int RoomId { get; set; } = 0;

        [Required(ErrorMessage = "Room name is required")]
        [StringLength(256, ErrorMessage = "Room name cannot exceed 256 characters")]
        public string Name { get; set; }

        [Required(ErrorMessage = "IsActive is required")]
        public int IsActive { get; set; }
    }

    public class MRDRoomMasterResponse
    {
        public int RoomId { get; set; }
    }

    // MRD Rack Master DTOs
    public class MRDRackMasterRequest
    {
        public int RackId { get; set; } = 0;

        [Required(ErrorMessage = "RoomId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "RoomId must be greater than 0")]
        public int RoomId { get; set; }

        [Required(ErrorMessage = "Rack name is required")]
        [StringLength(256, ErrorMessage = "Rack name cannot exceed 256 characters")]
        public string Name { get; set; }

        [Required(ErrorMessage = "IsActive is required")]
        public int IsActive { get; set; }

        [Range(0, 100, ErrorMessage = "AutoCreateShelfs must be between 0 and 100")]
        public int AutoCreateShelfs { get; set; } = 0;
    }

    public class MRDRackMasterResponse
    {
        public int RackId { get; set; }
    }

    // MRD Shelf Master DTOs
    public class MRDShelfMasterRequest
    {
        public int ShelfId { get; set; } = 0;

        [Required(ErrorMessage = "RoomId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "RoomId must be greater than 0")]
        public int RoomId { get; set; }

        [Required(ErrorMessage = "RackId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "RackId must be greater than 0")]
        public int RackId { get; set; }

        [Required(ErrorMessage = "Shelf name is required")]
        [StringLength(256, ErrorMessage = "Shelf name cannot exceed 256 characters")]
        public string Name { get; set; }

        [Required(ErrorMessage = "IsActive is required")]
        public int IsActive { get; set; }
    }

    public class MRDShelfMasterResponse
    {
        public int ShelfId { get; set; }
    }

    public class PatientDocumentMasterRequest
    {
        public int DocumentId { get; set; } = 0;

        [Required(ErrorMessage = "Document name is required")]
        [StringLength(256, ErrorMessage = "Document name cannot exceed 256 characters")]
        public string DocumentName { get; set; }

        [Required(ErrorMessage = "Document code is required")]
        [StringLength(20, ErrorMessage = "Document code cannot exceed 20 characters")]
        public string DocumentCode { get; set; }

        [Required(ErrorMessage = "IsActive is required")]
        public int IsActive { get; set; }

        [Required(ErrorMessage = "DocumentCategoryId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "DocumentCategoryId must be greater than 0")]
        public int DocumentCategoryId { get; set; }

        [Required(ErrorMessage = "Document Category is required")]
        [StringLength(256, ErrorMessage = "Document Category cannot exceed 256 characters")]
        public string DocumentCategory { get; set; }
        public int IsMandatory { get; set; } = 0;

    }

    public class PatientDocumentMasterResponse
    {
        public int DocumentId { get; set; }
    }

    public class SaveOutSourceLabMasterRequest
    {
        public int OutSourceLabId { get; set; } = 0;

        [Required(ErrorMessage = "OutSourceLab is required")]
        [StringLength(256, ErrorMessage = "OutSourceLab cannot exceed 256 characters")]
        public string OutSourceLab { get; set; }

        [StringLength(256)]
        public string ContactPerson { get; set; }

        [StringLength(50)]
        public string ContactNumber { get; set; }

        public string Address { get; set; }

        public int IsActive { get; set; } = 1;
        public int branchId { get; set; }

       
    }

 

    public class SaveOutSourceLabMasterResponse
    {
        public int OutSourceLabId { get; set; }
    }

    public class GetRateListMasterRequest
    {
        /// <summary>Filter by name (partial match). Null / empty = return all.</summary>
        public string? RateListName { get; set; }

        /// <summary>Filter by active status. Null = return all, 0 = inactive, 1 = active.</summary>
        public int? IsActive { get; set; }
    }

    public class CreateUpdateRateListMasterRequest
    {
        public int RateListId { get; set; } = 0;

        [Required(ErrorMessage = "RateListName is required")]
        [StringLength(256, ErrorMessage = "RateListName cannot exceed 256 characters")]
        public string RateListName { get; set; }

        [Required(ErrorMessage = "ApplicableDate is required")]
        public string ApplicableDate { get; set; }

        [Required(ErrorMessage = "ExpiryDate is required")]
        public string ExpiryDate { get; set; }   // expected format: dd-MM-yyyy from client

        [Required(ErrorMessage = "IsActive is required")]
        [Range(0, 1, ErrorMessage = "IsActive must be 0 or 1")]
        public int IsActive { get; set; }
        public int ImportFromRateListId { get; set; } = 0;
    }

    public class TariffMasterRequest
    {
        public int TariffId { get; set; }
        public int RateListId { get; set; }
        public int ServiceItemId { get; set; }
        public int BedTypeId { get; set; }
        public string? Alias { get; set; }
        public string? ServiceCode { get; set; }
        public int DoctorId { get; set; }
        public int ValidityDays { get; set; }
        public decimal EmergencyCharges { get; set; }
        public decimal Rate { get; set; }
        public int IsRateEditable { get; set; }
        public int IsActive { get; set; }
    }

    public class CreateUpdateTariffMasterRequest
    {
        public int IsCopyRateForIPD { get; set; } = 0;
        public List<TariffMasterRequest> TariffMasterData { get; set; } = new();
    }

    public class InsuranceCompanyMasterRequest
    {
        public int InsuranceCompanyId { get; set; } = 0;

        [Required(ErrorMessage = "Insurance company name is required")]
        [StringLength(256, ErrorMessage = "Insurance company name cannot exceed 256 characters")]
        public string InsuranceCompanyName { get; set; }
    }

    public class InsuranceCompanyMasterResponse
    {
        public int InsuranceCompanyId { get; set; }
    }


    public class CorporateTypeMasterRequest
    {
        public int CorporateTypeId { get; set; } = 0;

        [Required(ErrorMessage = "Corporate type name is required")]
        [StringLength(256, ErrorMessage = "Corporate type name cannot exceed 256 characters")]
        public string CorporateTypeName { get; set; }
    }

    public class CorporateTypeMasterResponse
    {
        public int CorporateTypeId { get; set; }
    }

    public class CorporateMasterRequest
    {
        public int CorporateId { get; set; } = 0;

        [Required(ErrorMessage = "Corporate name is required")]
        [StringLength(256, ErrorMessage = "Corporate name cannot exceed 256 characters")]
        public string CorporateName { get; set; }

        [Required(ErrorMessage = "InsuranceCompanyName name is required")]
        [StringLength(256, ErrorMessage = "Insurance company name cannot exceed 256 characters")]
        public string InsuranceCompanyName { get; set; }

        public int InsuranceCompanyId { get; set; }

        [Required(ErrorMessage = "CorporateTypeName is required")]
        [StringLength(256, ErrorMessage = "Corporate type name cannot exceed 256 characters")]
        public string CorporateTypeName { get; set; }

        [Required(ErrorMessage = "CorporateTypeId is required")]
        public int CorporateTypeId { get; set; }

        public int PaymentTypeId { get; set; }

        [StringLength(50, ErrorMessage = "Corporate code cannot exceed 50 characters")]
        public string CorporateCode { get; set; }

        [Required(ErrorMessage = "Corporate Contact 1 is required")]
        [StringLength(20, ErrorMessage = "Corporate contact 1 cannot exceed 20 characters")]
        public string CorporateContact1 { get; set; }

        [StringLength(20, ErrorMessage = "Corporate contact 2 cannot exceed 20 characters")]
        public string CorporateContact2 { get; set; }

        [Required(ErrorMessage = "Corporate Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email format")]
        [StringLength(100, ErrorMessage = "Corporate email cannot exceed 100 characters")]
        public string CorporateEmail { get; set; }

        [StringLength(500, ErrorMessage = "Corporate address 1 cannot exceed 500 characters")]
        public string CorporateAddress1 { get; set; }

        [StringLength(500, ErrorMessage = "Corporate address 2 cannot exceed 500 characters")]
        public string CorporateAddress2 { get; set; }

        [Required(ErrorMessage = "IsActive is required")]
        public int IsActive { get; set; }

        [Required(ErrorMessage = "Contract start date is required")]
        [RegularExpression(@"^\d{2}-\d{2}-\d{4}$",
            ErrorMessage = "ContractStartFrom must be in dd-MM-yyyy format (e.g. 20-04-2026)")]
        public string ContractStartFrom { get; set; }

        [Required(ErrorMessage = "Contract expiry date is required")]
        [RegularExpression(@"^\d{2}-\d{2}-\d{4}$",
            ErrorMessage = "ContractExpiresOn must be in dd-MM-yyyy format (e.g. 31-12-2028)")]
        public string ContractExpiresOn { get; set; }

        public decimal CopaymentPer { get; set; } = 0;
        public decimal DiscountPerOut { get; set; } = 0;
        public decimal DiscountPerIn { get; set; } = 0;
        public decimal HikePerOut { get; set; } = 0;
        public decimal HikePerIn { get; set; } = 0;

        [Required(ErrorMessage = "Active Payment Modes is required")]
        [StringLength(100, ErrorMessage = "Active payment modes cannot exceed 100 characters")]
        public string ActivePaymentModes { get; set; }
        public int IsRegistrationChargeApplicable { get; set; } = 0;
        public int IsCaseBillingApplicable { get; set; } = 0;





    }

    public class CorporateMasterResponse
    {
        public int CorporateId { get; set; }
    }

    public class DiscountApprovalMasterRequest
    {
        public int DiscountApprovalId { get; set; } = 0;

        [Required(ErrorMessage = "Discount approval name is required")]
        [StringLength(256, ErrorMessage = "Name cannot exceed 256 characters")]
        public string DiscountApprovalName { get; set; }

        [Required(ErrorMessage = "HmsUserId is required")]
        public int HmsUserId { get; set; }

        [Required(ErrorMessage = "IsActive is required")]
        public int IsActive { get; set; }

        [Required(ErrorMessage = "MappingBranch is required")]
        [StringLength(100, ErrorMessage = "MappingBranch cannot exceed 100 characters")]
        public string MappingBranch { get; set; }

        [Required(ErrorMessage = "MappingDiscountType is required")]
        [StringLength(100, ErrorMessage = "MappingDiscountType cannot exceed 100 characters")]
        public string MappingDiscountType { get; set; }
    }

    public class DiscountApprovalMasterResponse
    {
        public int Id { get; set; }
    }

    public class UserwiseDiscountMasterRequest
    {
        [Required(ErrorMessage = "UserId is required")]
        public int userId { get; set; }

        public decimal discPerOPD { get; set; } = 0;
        public decimal discPerIPD { get; set; } = 0;
        public decimal discPerPharmacy { get; set; } = 0;
        public decimal discPerDayCare { get; set; } = 0;
        public decimal discPerDialysis { get; set; } = 0;
        public decimal discPerEmergency { get; set; } = 0;
    }

    public class CreateUpdateDoctorHeaderRequest
    {
        public int HeaderId { get; set; } = 0;

        [Required(ErrorMessage = "HeaderName is required")]
        [StringLength(256, ErrorMessage = "HeaderName cannot exceed 256 characters")]
        public string HeaderName { get; set; }

        [StringLength(256, ErrorMessage = "DisplayName cannot exceed 256 characters")]
        public string DisplayName { get; set; }

        [StringLength(256, ErrorMessage = "ControlType cannot exceed 256 characters")]
        public string ControlType { get; set; }

        [Required(ErrorMessage = "ControlTypeId is required")]
        public int ControlTypeId { get; set; }

        public int IsPrint { get; set; } = 1;

        public int IsShowInTempRoom { get; set; } = 0;

        public int UsedForPatientType { get; set; } = 1;

        [Required(ErrorMessage = "IsActive is required")]
        public int IsActive { get; set; }
        public int IsMandatory { get; set; } = 0;
        public string Queries { get; set; }



        public List<DoctorHeaderLOVRequest> ListOfValues { get; set; }
    }

    public class DoctorHeaderLOVRequest
    {
       
        public string Value { get; set; }

        public int DataTypeId { get; set; } = 0;
        public int Score { get; set; } = 0;
        public string Base64Data { get; set; }
        public string Description { get; set; }
        public string HeaderName { get; set; }
        public List<string> Options { get; set; } = new List<string>();
    }

    public class CreateUpdateDoctorHeaderResponse
    {
        public int HeaderId { get; set; }
    }

    // ─── Save Doctor Header Department Mapping ────────────────────────────────────

    public class SaveDoctorHeaderMappingRequest
    {
        [Required(ErrorMessage = "TypeId is required")]
        public int TypeId { get; set; }

        [Required(ErrorMessage = "RelatedToId is required")]
        public int RelatedToId { get; set; }

        public List<DoctorHeaderMappingItemRequest> HeaderMappingData { get; set; }
    }

    public class DoctorHeaderMappingItemRequest
    {
        [Required(ErrorMessage = "TypeId is required")]
        public int TypeId { get; set; }

        [StringLength(100)]
        public string TypeName { get; set; }

        [Required(ErrorMessage = "HeaderId is required")]
        public int HeaderId { get; set; }

        [Required(ErrorMessage = "RelatedToId is required")]
        public int RelatedToId { get; set; }

        public int SequenceNo { get; set; } = 0;
    }

    // ─── Get Doctor Header LOVs ───────────────────────────────────────────────────

    public class GetDoctorHeaderLOVsRequest
    {
        [Required(ErrorMessage = "HeaderId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "HeaderId must be greater than 0")]
        public int HeaderId { get; set; }
    }

    // ─── Get Doctor Header Mapping For Master ────────────────────────────────────

    public class GetDoctorHeaderMappingRequest
    {
        [Required(ErrorMessage = "TypeId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "TypeId must be greater than 0")]
        public int TypeId { get; set; }

        [Required(ErrorMessage = "RelatedToId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "RelatedToId must be greater than 0")]
        public int RelatedToId { get; set; }
    }

    public class CreateUpdateServiceItemMasterRequest
    {
        public int ServiceItemId { get; set; } = 0;

        [Required(ErrorMessage = "CategoryId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "CategoryId must be greater than 0")]
        public int CategoryId { get; set; }

        [Required(ErrorMessage = "SubCategoryId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "SubCategoryId must be greater than 0")]
        public int SubCategoryId { get; set; }

        [Required(ErrorMessage = "SubSubCategoryId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "SubSubCategoryId must be greater than 0")]
        public int SubSubCategoryId { get; set; }

        [Required(ErrorMessage = "Name is required")]
        [StringLength(256, ErrorMessage = "Name cannot exceed 256 characters")]
        public string Name { get; set; }

        [StringLength(50, ErrorMessage = "Code cannot exceed 50 characters")]
        public string? Code { get; set; }

        public int? RoomTypeId { get; set; }
        public string? RoomType { get; set; }
        public int? IsICU { get; set; }
        public decimal GstPer { get; set; } = 0;

        [StringLength(50, ErrorMessage = "SNOMED Code cannot exceed 50 characters")]
        public string? SNOMEDCode { get; set; }
        public int IsRequiredSeparatePerformingDoctor { get; set; } = 0;
        public string? DoctorDepartmentIds { get; set; }
        public int? OPDConsultationTypeId  { get; set; }
        public string? OPDConsultationType { get; set; }
        public int? IsOnlineConsultationAllow  { get; set; }
        public int? IsTeleConsultationService  { get; set; }
        public int? IsRegistrationCharge { get; set; }
        public int? RegistrationChargeValidityDays { get; set; }

        [Required(ErrorMessage = "IsActive is required")]
        public int IsActive { get; set; }
    }

    public class RegistrationChargeDuplicateResponse
    {
        public int DuplicateCount { get; set; }
        public string ServiceName { get; set; }
    }

    public class CreateUpdateServiceItemMasterResponse
    {
        public int ServiceItemId { get; set; }
    }

    public class CreateUpdatePrintGroupMasterRequest
    {
        public int PrintGroupId { get; set; } = 0;

        [Required(ErrorMessage = "PrintGroupName is required")]
        [StringLength(100, ErrorMessage = "PrintGroupName cannot exceed 100 characters")]
        public string PrintGroupName { get; set; }

        public int? PrintOrder { get; set; }
    }

    public class CreateUpdateWardNameMasterRequest
    {
        public int WardNameId { get; set; } = 0;

        [Required(ErrorMessage = "WardName is required")]
        [StringLength(100, ErrorMessage = "WardName cannot exceed 100 characters")]
        public string WardName { get; set; }
    }

    public class CreateUpdateBlockMasterRequest
    {
        public int BlockId { get; set; } = 0;

        [Required(ErrorMessage = "Block name is required")]
        [StringLength(256, ErrorMessage = "Block name cannot exceed 256 characters")]
        public string BlockName { get; set; }
    }

    public class CreateUpdateBlockMasterResponse
    {
        public int BlockId { get; set; }
    }

    public class CreateUpdateFloorMasterRequest
    {
        public int FloorId { get; set; } = 0;

        [Required(ErrorMessage = "Floor name is required")]
        [StringLength(256, ErrorMessage = "Floor name cannot exceed 256 characters")]
        public string FloorName { get; set; }
    }

    public class CreateUpdateFloorMasterResponse
    {
        public int FloorId { get; set; }
    }

    public class CreateUpdateBedMasterRequest
    {
        public int BedId { get; set; } = 0;

       

        [Required(ErrorMessage = "BranchId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "BranchId must be greater than 0")]
        public int BranchId { get; set; }

        [Required(ErrorMessage = "TypeId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "TypeId must be greater than 0")]
        public int TypeId { get; set; }

        [Required(ErrorMessage = "BlockId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "BlockId must be greater than 0")]
        public int BlockId { get; set; }

        [Required(ErrorMessage = "FloorId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "FloorId must be greater than 0")]
        public int FloorId { get; set; }

        [Required(ErrorMessage = "WardNameId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "WardNameId must be greater than 0")]
        public int WardNameId { get; set; }


        [StringLength(256, ErrorMessage = "RoomName cannot exceed 256 characters")]
        public string RoomName { get; set; }

        [Required(ErrorMessage = "Gender is required")]
        public string Gender { get; set; }

        [Required(ErrorMessage = "BedNo is required")]
        [Range(1, 25, ErrorMessage = "Enter BedNo between 1 to 25")]
        public int BedNo { get; set; }

        [Required(ErrorMessage = "IsActive is required")]
        public int IsActive { get; set; }
    }

    public class CreateUpdateBedMasterResponse
    {
        public int BedId { get; set; }
    }



    public class CreateUpdateTabGroupTypeMasterRequest
    {
        public int GroupTypeId { get; set; } = 0;

        [Required(ErrorMessage = "GroupTypeName is required")]
        [StringLength(100, ErrorMessage = "GroupTypeName cannot exceed 100 characters")]
        public string GroupTypeName { get; set; }

    
    }

    public class CreateUpdateIPDTabMasterRequest
    {
        public int TabId { get; set; } = 0;

        [Required(ErrorMessage = "GroupTypeId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "GroupTypeId must be greater than 0")]
        public int GroupTypeId { get; set; }

        [Required(ErrorMessage = "TabName is required")]
        [StringLength(100, ErrorMessage = "TabName cannot exceed 100 characters")]
        public string TabName { get; set; }

        [StringLength(1000, ErrorMessage = "TabViewURL cannot exceed 1000 characters")]
        public string TabViewURL { get; set; }

        public int SequenceNo { get; set; } = 0;

        [Required(ErrorMessage = "TabTypeId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "TabTypeId must be greater than 0")]
        public int TabTypeId { get; set; }

        [Required(ErrorMessage = "TabType is required")]
        [StringLength(100, ErrorMessage = "TabType cannot exceed 100 characters")]
        public string TabType { get; set; }

        public int? RoomTypeId { get; set; }

        [Required(ErrorMessage = "FaIconId is required")]
        public int FaIconId { get; set; } = 0;

        [Required(ErrorMessage = "IsActive is required")]
        public int IsActive { get; set; }
    }

   

    public class SaveUserIPDTabMappingRequest
    {
        [Required(ErrorMessage = "TypeId is required")]
        public int TypeId { get; set; }

        [Required(ErrorMessage = "UserId is required")]
        public int UserId { get; set; }

        [Required(ErrorMessage = "BranchId is required")]
        public int BranchId { get; set; }

        [Required(ErrorMessage = "RoleId is required")]
        public int RoleId { get; set; }

        public List<UserIPDTabMappingItem> TabMappings { get; set; } = new List<UserIPDTabMappingItem>();
    }

    public class UserIPDTabMappingItem
    {
        [Required(ErrorMessage = "TabId is required")]
        public int TabId { get; set; }
    }


    // Role Wise IPD Tab Mapping DTOs
    public class SaveRoleWiseIPDTabMappingRequest
    {
        [Required(ErrorMessage = "RoleId is required")]
        public int RoleId { get; set; }

        public List<RoleWiseIPDTabMappingItem> TabMappings { get; set; } = new List<RoleWiseIPDTabMappingItem>();
    }

    public class RoleWiseIPDTabMappingItem
    {
        [Required(ErrorMessage = "TabId is required")]
        public int TabId { get; set; }
    }


    public class CreateUpdateApprovalAuthorityMasterRequest
    {
        public int Id { get; set; } = 0;

        [Required(ErrorMessage = "BranchId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "BranchId must be greater than 0")]
        public int BranchId { get; set; }

        [Required(ErrorMessage = "ApprovalFlowId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "ApprovalFlowId must be greater than 0")]
        public int ApprovalFlowId { get; set; }

        [Required(ErrorMessage = "ApprovalFlow is required")]
        [StringLength(100, ErrorMessage = "ApprovalFlow cannot exceed 100 characters")]
        public string ApprovalFlow { get; set; }

        [Required(ErrorMessage = "IsAllApprovalRequired is required")]
        [Range(0, 1, ErrorMessage = "IsAllApprovalRequired must be 0 or 1")]
        public int IsAllApprovalRequired { get; set; }

        [Required(ErrorMessage = "ApprovalTypeId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "ApprovalTypeId must be greater than 0")]
        public int ApprovalTypeId { get; set; }

        [Required(ErrorMessage = "ApprovalType is required")]
        [StringLength(100, ErrorMessage = "ApprovalType cannot exceed 100 characters")]
        public string ApprovalType { get; set; }

        public int RoleId { get; set; } = 0;

        [Required(ErrorMessage = "ApprovalLevelId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "ApprovalLevelId must be greater than 0")]
        public int ApprovalLevelId { get; set; }

        [Required(ErrorMessage = "ApprovalLevel is required")]
        [StringLength(100, ErrorMessage = "ApprovalLevel cannot exceed 100 characters")]
        public string ApprovalLevel { get; set; }

        [Required(ErrorMessage = "Level1UserId is required")]
        public string Level1UserId { get; set; }
        public string Level2UserId { get; set; }
        public string Level3UserId { get; set; }
        public string Level4UserId { get; set; }

        public decimal AmountUpTo { get; set; } = 0;

        [Required(ErrorMessage = "IsActive is required")]
        [Range(0, 1, ErrorMessage = "IsActive must be 0 or 1")]
        public int IsActive { get; set; }
    }

    public class CreateUpdateApprovalAuthorityMasterResponse
    {
        public long Id { get; set; }
    }



    // ─── Branch Corporate Ratelist Mapping ───────────────────────────────────────

    public class BranchCorporateRatelistMappingItem
    {
        [Required(ErrorMessage = "RateListIdOPD is required")]
        public string RateListIdOPD { get; set; }

        [Required(ErrorMessage = "RateListIdIPD is required")]
        public string RateListIdIPD { get; set; }
    }

    public class SaveBranchCorporateRatelistMappingRequest
    {
        [Required(ErrorMessage = "BranchId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "BranchId must be greater than 0")]
        public int BranchId { get; set; }

        [Required(ErrorMessage = "CorporateId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "CorporateId must be greater than 0")]
        public int CorporateId { get; set; }

        /// <summary>
        /// List of ratelist mappings to insert after deactivating existing ones.
        /// Pass empty list to just deactivate existing mappings.
        /// </summary>
        [Required(ErrorMessage = "Mappings list is required")]
        public List<BranchCorporateRatelistMappingItem> Mappings { get; set; } = new();
    }

    // ─── Branch Corporate Wise Service Exclusion Mapping ─────────────────────────

    public class SaveBranchCorporateServiceExclusionRequest
    {
        [Required(ErrorMessage = "BranchId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "BranchId must be greater than 0")]
        public int BranchId { get; set; }

        [Required(ErrorMessage = "CorporateId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "CorporateId must be greater than 0")]
        public int CorporateId { get; set; }

        /// <summary>
        /// List of ServiceItemIds to exclude. Pass empty list to just deactivate existing exclusions.
        /// </summary>
        [Required(ErrorMessage = "ServiceItemIds list is required")]
        public List<int> ServiceItemIds { get; set; } = new();
    }

    // ─── Branch Right Mapping ─────────────────────────────────────────────────────

    public class SaveBranchRightMappingRequest
    {
        [Required(ErrorMessage = "BranchId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "BranchId must be greater than 0")]
        public int BranchId { get; set; }

        /// <summary>
        /// List of BranchRightIds to map. Pass empty list to just delete existing mappings.
        /// </summary>
        [Required(ErrorMessage = "BranchRightIds list is required")]
        public List<int> BranchRightIds { get; set; } = new();
    }


    public class UpdateDefaultBranchSettingRequest
    {
        [Required(ErrorMessage = "BranchId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "BranchId must be greater than 0")]
        public int BranchId { get; set; }

        [Required(ErrorMessage = "DefaultCountryId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "DefaultCountryId must be greater than 0")]
        public int DefaultCountryId { get; set; }

        [Required(ErrorMessage = "DefaultStateId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "DefaultStateId must be greater than 0")]
        public int DefaultStateId { get; set; }

        [Required(ErrorMessage = "DefaultDistrictId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "DefaultDistrictId must be greater than 0")]
        public int DefaultDistrictId { get; set; }

        [Required(ErrorMessage = "DefaultCityId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "DefaultCityId must be greater than 0")]
        public int DefaultCityId { get; set; }

        [Required(ErrorMessage = "DefaultInsuranceCompanyId is required")]
        [Range(0, int.MaxValue, ErrorMessage = "DefaultInsuranceCompanyId must be greater than or equal to 0")]
        public int DefaultInsuranceCompanyId { get; set; }

        [Required(ErrorMessage = "DefaultCorporateId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "DefaultCorporateId must be greater than 0")]
        public int DefaultCorporateId { get; set; }
    }

    public class CreateUpdateVitalMasterRequest
    {
        public int VitalId { get; set; } = 0;

        [Required(ErrorMessage = "VitalName is required")]
        [StringLength(256, ErrorMessage = "VitalName cannot exceed 256 characters")]
        public string VitalName { get; set; }

        [Required(ErrorMessage = "UnitId is required")]
        public int UnitId { get; set; }

        [StringLength(256, ErrorMessage = "UnitName cannot exceed 256 characters")]
        public string UnitName { get; set; }

        [StringLength(256, ErrorMessage = "MinValue cannot exceed 256 characters")]
        public string MinValue { get; set; }

        [StringLength(256, ErrorMessage = "MaxValue cannot exceed 256 characters")]
        public string MaxValue { get; set; }

        public string snomedCode { get; set; }

        [Required(ErrorMessage = "Active is required")]
        [Range(0, 1, ErrorMessage = "Active must be 0 or 1")]
        public int Active { get; set; }
        public int IsMandatory { get; set; } = 0;
        public int IsBodyMeasurement { get; set; } = 0;

    }

    public class CreateUpdateVitalUnitMasterRequest
    {
        public int Id { get; set; } = 0;

        [Required(ErrorMessage = "UnitName is required")]
        [StringLength(100, ErrorMessage = "UnitName cannot exceed 100 characters")]
        public string UnitName { get; set; }
    }

    public class SaveVitalDepartmentMappingRequest
    {
        [Required(ErrorMessage = "TypeId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "TypeId must be greater than 0")]
        public int TypeId { get; set; }

        [StringLength(100)]
        public string TypeName { get; set; }

        [Required(ErrorMessage = "RelatedToId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "RelatedToId must be greater than 0")]
        public int RelatedToId { get; set; }

        public List<VitalDepartmentMappingItemRequest> HeaderMappingData { get; set; }
    }

    public class VitalDepartmentMappingItemRequest
    {
      
       

        [Required(ErrorMessage = "VitalId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "VitalId must be greater than 0")]
        public int vitalId { get; set; }
        public int SequenceNo { get; set; } = 0;
    }

    public class CreateUpdatePackageMasterRequest
    {
        public int PackageId { get; set; } = 0;

        [Required(ErrorMessage = "CategoryId is required")]
        public int CategoryId { get; set; }

        [Required(ErrorMessage = "SubCategoryId is required")]
        public int SubCategoryId { get; set; }

        [Required(ErrorMessage = "SubSubCategoryId is required")]
        public int SubSubCategoryId { get; set; }

        [Required(ErrorMessage = "Name is required")]
        [StringLength(256, ErrorMessage = "Name cannot exceed 256 characters")]
        public string Name { get; set; }

        [StringLength(50, ErrorMessage = "Code cannot exceed 50 characters")]
        public string? Code { get; set; }

        public int? IsMultipleVisitAllow { get; set; }
        public int? VisitDuration { get; set; }

        [StringLength(20, ErrorMessage = "VisitDurationType cannot exceed 20 characters")]
        public string? VisitDurationType { get; set; }

        [Required(ErrorMessage = "ValidityStartsFrom is required")]
        public string ValidityStartsFrom { get; set; }   // e.g. dd-MM-yyyy or yyyy-MM-dd

        [Required(ErrorMessage = "ValidityEndsOn is required")]
        public string ValidityEndsOn { get; set; }

        [Required(ErrorMessage = "IsActive is required")]
        public int IsActive { get; set; }

        [Required(ErrorMessage = "PackageServices is required")]
        [MinLength(1, ErrorMessage = "At least one package service is required")]
        public List<PackageServiceRequest> PackageServices { get; set; } = new List<PackageServiceRequest>();
    }

    public class PackageServiceRequest
    {
        [Required(ErrorMessage = "ServiceItemId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "ServiceItemId must be greater than 0")]
        public int ServiceItemId { get; set; }

        [Required(ErrorMessage = "Qty is required")]
        [Range(1, int.MaxValue, ErrorMessage = "Qty must be greater than 0")]
        public int Qty { get; set; }
    }

    public class CreateUpdatePackageMasterResponse
    {
        public int PackageId { get; set; }
    }

    public class UpdateNavigationSubMenuSequenceItem
    {
        [Required(ErrorMessage = "SubMenuId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "SubMenuId must be greater than 0")]
        public int SubMenuId { get; set; }

        [Required(ErrorMessage = "SequenceNo is required")]
        public int SequenceNo { get; set; }
    }

    public class UpdateNavigationSubMenuSequenceRequest
    {
        [Required(ErrorMessage = "SubMenus list is required")]
        [MinLength(1, ErrorMessage = "At least one submenu sequence is required")]
        public List<UpdateNavigationSubMenuSequenceItem> SubMenus { get; set; } = new();
    }

    public class UpdateNavigationTabSequenceItem
    {
        [Required(ErrorMessage = "TabId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "TabId must be greater than 0")]
        public int TabId { get; set; }

        [Required(ErrorMessage = "SequenceNo is required")]
        public int SequenceNo { get; set; }
    }

    public class UpdateNavigationTabSequenceRequest
    {
        [Required(ErrorMessage = "Tabs list is required")]
        [MinLength(1, ErrorMessage = "At least one tab sequence is required")]
        public List<UpdateNavigationTabSequenceItem> Tabs { get; set; } = new();
    }

    public class CreateUpdateSurgeryComponentMasterRequest
    {
        public int ComponentId { get; set; } = 0;

        [Required(ErrorMessage = "ComponentName is required")]
        [StringLength(256, ErrorMessage = "ComponentName cannot exceed 256 characters")]
        public string ComponentName { get; set; }

        [Required(ErrorMessage = "HasDoctor is required")]
        [Range(0, 1, ErrorMessage = "HasDoctor must be 0 or 1")]
        public int HasDoctor { get; set; }

        [Required(ErrorMessage = "IsBaseComponent is required")]
        [Range(0, 1, ErrorMessage = "IsBaseComponent must be 0 or 1")]
        public int IsBaseComponent { get; set; }

        [Range(0, 100, ErrorMessage = "SharePercentage must be between 0 and 100")]
        public decimal SharePercentage { get; set; } = 0;

        [Required(ErrorMessage = "IsActive is required")]
        [Range(0, 1, ErrorMessage = "IsActive must be 0 or 1")]
        public int IsActive { get; set; }
    }

    public class CreateUpdateSurgeryComponentMasterResponse
    {
        public int ComponentId { get; set; }
    }
    public class CreateUpdateDischargeProcessMasterRequest
    {
        public int DischargeProcessId { get; set; } = 0;

        [Required(ErrorMessage = "ProcessKey is required")]
        [StringLength(100, ErrorMessage = "ProcessKey cannot exceed 100 characters")]
        [RegularExpression(@"^[A-Z0-9_]+$", ErrorMessage = "ProcessKey must be uppercase letters, digits, and underscores only")]
        public string ProcessKey { get; set; }

        [Required(ErrorMessage = "ProcessName is required")]
        [StringLength(200, ErrorMessage = "ProcessName cannot exceed 200 characters")]
        public string ProcessName { get; set; }

        [Required(ErrorMessage = "FaIconId is required")]
        [Range(0, int.MaxValue, ErrorMessage = "FaIconId must be greater than 0")]
        public int FaIconId { get; set; }

        [Required(ErrorMessage = "IsMandatory is required")]
        [Range(0, 1, ErrorMessage = "IsMandatory must be 0 or 1")]
        public int IsMandatory { get; set; } = 0;

      

        [Required(ErrorMessage = "IsActive is required")]
        [Range(0, 1, ErrorMessage = "IsActive must be 0 or 1")]
        public int IsActive { get; set; } = 1;

        [Range(0, 1, ErrorMessage = "IsSystemProcess must be 0 or 1")]
        public int IsSystemProcess { get; set; } = 0;
    }

    public class CreateUpdateDischargeProcessMasterResponse
    {
        public int DischargeProcessId { get; set; }
    }

    public class UpdateDischargeProcessSequenceItemRequest
    {
        [Required(ErrorMessage = "DischargeProcessId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "DischargeProcessId must be greater than 0")]
        public int DischargeProcessId { get; set; }

        [Required(ErrorMessage = "SequenceNo is required")]
        [Range(1, int.MaxValue, ErrorMessage = "SequenceNo must be greater than 0")]
        public int SequenceNo { get; set; }
    }

    public class UpdateDischargeProcessSequenceRequest
    {
        [Required(ErrorMessage = "Sequences is required")]
        [MinLength(1, ErrorMessage = "At least one sequence item is required")]
        public List<UpdateDischargeProcessSequenceItemRequest> Sequences { get; set; } = new();
    }

    public class SaveDischargeProcessCorporateMappingRequest
    {
        [Required(ErrorMessage = "DischargeProcessId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "DischargeProcessId must be greater than 0")]
        public int DischargeProcessId { get; set; }

        /// <summary>Empty list = process becomes Global (no corporate restriction).</summary>
        [Required(ErrorMessage = "CorporateIds is required")]
        public List<int> CorporateIds { get; set; } = new();
    }

    public class UserDischargeProcessMappingRequest
    {
        [Required(ErrorMessage = "TypeId is required")]
        public int TypeId { get; set; }

        [Required(ErrorMessage = "UserId is required")]
        public int UserId { get; set; }

        [Required(ErrorMessage = "BranchId is required")]
        public int BranchId { get; set; }

        [Required(ErrorMessage = "DischargeProcessId is required")]
        public int DischargeProcessId { get; set; }
    }

    public class SaveUserDischargeProcessMappingRequest
    {
        [Required(ErrorMessage = "TypeId is required")]
        public int TypeId { get; set; }

        [Required(ErrorMessage = "UserId is required")]
        public int UserId { get; set; }

        [Required(ErrorMessage = "BranchId is required")]
        public int BranchId { get; set; }

        [Required(ErrorMessage = "IsFirst is required")]
        public int IsFirst { get; set; }

        public List<UserDischargeProcessMappingRequest>? UserDischargeProcessMappings { get; set; }
    }

}
