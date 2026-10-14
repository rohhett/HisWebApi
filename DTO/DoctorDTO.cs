using System.ComponentModel.DataAnnotations;
using HISWEBAPI.Attributes;

namespace HISWEBAPI.DTO
{
    // Doctor Department DTOs
    public class CreateUpdateDoctorDepartmentRequest
    {
        public int DepartmentId { get; set; } = 0;

        [Required(ErrorMessage = "Department name is required")]
        [StringLength(100, ErrorMessage = "Department name cannot exceed 100 characters")]
        public string Department { get; set; }

        [Required(ErrorMessage = "DepartmentTypeId is required")]
        public int DepartmentTypeId { get; set; }

        [StringLength(100, ErrorMessage = "DepartmentType cannot exceed 100 characters")]
        public string DepartmentType { get; set; }

        [Required(ErrorMessage = "IsActive is required")]
        public int IsActive { get; set; }
    }

    public class CreateUpdateDoctorDepartmentResponse
    {
        public int DepartmentId { get; set; }
    }

    // Doctor Specialization DTOs
    public class CreateUpdateDoctorSpecializationRequest
    {
        public int SpecializationId { get; set; } = 0;

        [Required(ErrorMessage = "Specialization name is required")]
        [StringLength(100, ErrorMessage = "Specialization name cannot exceed 100 characters")]
        public string Specialization { get; set; }

        [Required(ErrorMessage = "IsActive is required")]
        public int IsActive { get; set; }
    }

    public class CreateUpdateDoctorSpecializationResponse
    {
        public int SpecializationId { get; set; }
    }

    public class CreateUpdateDoctorMasterRequest
    {
        // Doctor Basic Info
        public int DoctorId { get; set; } = 0;

        [Required(ErrorMessage = "Title is required")]
        [StringLength(20, ErrorMessage = "Title cannot exceed 20 characters")]
        public string Title { get; set; }

        [Required(ErrorMessage = "Name is required")]
        [StringLength(100, ErrorMessage = "Name cannot exceed 100 characters")]
        public string Name { get; set; }

        [Required(ErrorMessage = "Gender is required")]
        [StringLength(20)]
        public string Gender { get; set; }

        [Required(ErrorMessage = "Dob is required")]
        public DateTime Dob { get; set; }

        [Required(ErrorMessage = "Contact number is required")]
        [StringLength(15)]
        [RegularExpression(@"^\d{10}$", ErrorMessage = "Contact must be exactly 10 digits")]
        public string ContactNo { get; set; }

        [StringLength(100)]
        public string? EmailId { get; set; }

        public string? Address { get; set; }

        [Required(ErrorMessage = "SpecializationId is required")]
        public int SpecializationId { get; set; }

        [Required(ErrorMessage = "Specialization is required")]
        [StringLength(100)]
        public string Specialization { get; set; }

        [Required(ErrorMessage = "DepartmentId is required")]
        public int DepartmentId { get; set; }

        [Required(ErrorMessage = "Department is required")]
        [StringLength(50)]
        public string Department { get; set; }

        [Required(ErrorMessage = "ProfileSummery is required")]
        public string? ProfileSummery { get; set; }

        [StringLength(50)]
        public string? RegistrationNo { get; set; }

        [Required(ErrorMessage = "IsActive is required")]
        public int IsActive { get; set; }

        [Required(ErrorMessage = "BranchList is required")]
        public string BranchList { get; set; }

        [StringLength(20)]
        public string? RoomNo { get; set; }

        public int CanApproveLabReport { get; set; } = 0;

        public int CanApproveDischargeSummary { get; set; } = 0;

        // File upload for doctor photo
        public IFormFile? DoctorPhotoFile { get; set; }

        public int IsLogin { get; set; } = 0;

        [StringLength(50)]
        public string? UserName { get; set; }

        public string? Password { get; set; }
        public int DoctorRoleId { get; set; } = 0;
        [StringLength(100)]
        public string? DoctorRole { get; set; }
    }

    public class CreateUpdateDoctorMasterResponse
    {
        public long DoctorId { get; set; }
        public string? DoctorPhotoFilePath { get; set; }
    }

    public class CreateUpdateDoctorTimingDetailsRequest
    {
        [Required(ErrorMessage = "DoctorId is required")]
        [Range(0, int.MaxValue, ErrorMessage = "DoctorId must be greater than or equal to 0")]
        public int DoctorId { get; set; }

        [Required(ErrorMessage = "DoctorTimings is required")]
        [MinLength(1, ErrorMessage = "At least one timing detail is required")]
        public List<DoctorTimingDetailRequest> DoctorTimings { get; set; } = new List<DoctorTimingDetailRequest>();
    }
    public class DoctorTimingDetailRequest
    {
        [Required(ErrorMessage = "BranchId is required")]
        [Range(1, int.MaxValue, ErrorMessage = "BranchId must be greater than 0")]
        public int BranchId { get; set; }

        [Required(ErrorMessage = "Day is required")]
        [StringLength(20, ErrorMessage = "Day cannot exceed 20 characters")]
        [RegularExpression(@"^(Monday|Tuesday|Wednesday|Thursday|Friday|Saturday|Sunday)$",
            ErrorMessage = "Day must be a valid day of the week")]
        public string Day { get; set; }

        [Required(ErrorMessage = "StartTiming is required")]
        [StringLength(10, ErrorMessage = "StartTiming cannot exceed 10 characters")]
        [RegularExpression(@"^([0-1]?[0-9]|2[0-3]):[0-5][0-9]$",
            ErrorMessage = "StartTiming must be in HH:mm format (e.g., 09:00)")]
        public string StartTiming { get; set; }

        [Required(ErrorMessage = "EndTiming is required")]
        [StringLength(10, ErrorMessage = "EndTiming cannot exceed 10 characters")]
        [RegularExpression(@"^([0-1]?[0-9]|2[0-3]):[0-5][0-9]$",
            ErrorMessage = "EndTiming must be in HH:mm format (e.g., 17:00)")]
        public string EndTiming { get; set; }
    }

   

    public class CreateUpdateDoctorTimingDetailsResponse
    {
        public int DoctorId { get; set; }
        public int TotalTimingsCreated { get; set; }
        public List<int> DoctorTimingIds { get; set; } = new List<int>();
    }

    public class DoctorUnitMappingRequest
    {
        [Required(ErrorMessage = "DoctorId is required")]
        public int DoctorId { get; set; }
    }

    public class CreateUpdateDoctorUnitMasterRequest
    {
        public int DoctorId { get; set; } = 0; // 0 = create, >0 = update

        [Required(ErrorMessage = "Name is required")]
        [StringLength(150, ErrorMessage = "Name cannot exceed 150 characters")]
        public string Name { get; set; }

        [Required(ErrorMessage = "SpecializationId is required")]
        public int SpecializationId { get; set; }

        [Required(ErrorMessage = "Specialization is required")]
        [StringLength(100, ErrorMessage = "Specialization cannot exceed 100 characters")]
        public string Specialization { get; set; }

        [Required(ErrorMessage = "DepartmentId is required")]
        public int DepartmentId { get; set; }

        [Required(ErrorMessage = "Department is required")]
        [StringLength(50, ErrorMessage = "Department cannot exceed 50 characters")]
        public string Department { get; set; }

        [Required(ErrorMessage = "IsActive is required")]
        public int IsActive { get; set; }

        [Required(ErrorMessage = "BranchId is required")]
        public string BranchId { get; set; }

       
    }

    public class CreateUpdateDoctorUnitMappingRequest
    {
       
        [Required(ErrorMessage = "UnitId is required")]
        public int UnitId { get; set; }

        [Required(ErrorMessage = "DoctorMappings is required")]
        public List<DoctorUnitMappingRequest> DoctorMappings { get; set; } = new List<DoctorUnitMappingRequest>();
    }

    public class DoctorUnitMappingResponse
    {
        public int UnitId { get; set; }
    }

    public class CreateUpdateProMasterRequest
    {
        public int ProId { get; set; } = 0;

        [Required(ErrorMessage = "PRO Name is required")]
        [StringLength(256, ErrorMessage = "PRO Name cannot exceed 256 characters")]
        public string ProName { get; set; }

        [Required(ErrorMessage = "Contact No is required")]
        [StringLength(15, ErrorMessage = "Contact No cannot exceed 15 characters")]
        public string ContactNo { get; set; }

        [Required(ErrorMessage = "IsActive is required")]
        [Range(0, 1, ErrorMessage = "IsActive must be 0 or 1")]
        public int IsActive { get; set; }
    }

  
    public class CreateUpdateProMasterResponse
    {
        public int ProId { get; set; }
    }


    public class CreateUpdateReferDoctorRequest
    {
        public int ReferDoctorId { get; set; } = 0;

        [Required(ErrorMessage = "Title is required")]
        [StringLength(20, ErrorMessage = "Title cannot exceed 20 characters")]
        public string Title { get; set; }

        [Required(ErrorMessage = "Name is required")]
        [StringLength(100, ErrorMessage = "Name cannot exceed 100 characters")]
        public string Name { get; set; }

        [Required(ErrorMessage = "Doctor Contact No is required")]
        [StringLength(15, ErrorMessage = "Contact No cannot exceed 15 characters")]
        public string DoctorContacNo { get; set; }

        [StringLength(256, ErrorMessage = "Clinic Name cannot exceed 256 characters")]
        public string ClinicName { get; set; }

        [StringLength(2048, ErrorMessage = "Address cannot exceed 2048 characters")]
        public string Address { get; set; }

        [Required(ErrorMessage = "ProId is required")]
        [Range(0, int.MaxValue, ErrorMessage = "ProId must be greater than or equal to 0")]
        public int ProId { get; set; }

        [Required(ErrorMessage = "Active status is required")]
        [Range(0, 1, ErrorMessage = "Active must be 0 or 1")]
        public int Active { get; set; }
    }

  
    public class CreateUpdateReferDoctorResponse
    {
        public int ReferDoctorId { get; set; }
    }
}