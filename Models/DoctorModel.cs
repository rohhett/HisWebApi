using System.ComponentModel.DataAnnotations;

namespace HISWEBAPI.Models
{
    public class DoctorDepartmentModel
    {
        public int DepartmentId { get; set; }
        public string Department { get; set; }
        public int DepartmentTypeId { get; set; }
        public string DepartmentType { get; set; }
        public int IsActive { get; set; }
    }

    public class DoctorSpecializationModel
    {
        public int SpecializationId { get; set; }
        public string Specialization { get; set; }
        public int IsActive { get; set; }
    }


    public class DoctorMasterModelAll
    {
        public int DoctorId { get; set; }
        public string Title { get; set; }
        public string Name { get; set; }
        public string Dob { get; set; }
        public string Gender { get; set; }
        public string CompleteName { get; set; }
        public string ContactNo { get; set; }
        public string EmailId { get; set; }
        public string Address { get; set; }
        public int SpecializationId { get; set; }
        public string Specialization { get; set; }
        public string UserName { get; set; }
        public string Password { get; set; }
        public int DepartmentId { get; set; }
        public string Department { get; set; }
        public string ProfileSummery { get; set; }
        public string RegistrationNo { get; set; }
        public int IsActive { get; set; }
        public int? UserId { get; set; }
        public int HospId { get; set; }
        public string CreatedBy { get; set; }
        public string CreatedOn { get; set; }
        public string IpAddress { get; set; }
        public string BranchId { get; set; }
        public int CanApproveLabReport { get; set; }
        public int CanApproveDischargeSummary { get; set; }
        public string DoctorPhotoFilePath { get; set; }
        public byte IsDoctorUnit { get; set; }
        public string RoomNo { get; set; }
        public int? DoctorRoleId { get; set; }
        public string DoctorRole { get; set; }
    }

    public class DoctorUnitMappingModel
    {
        public int DoctorId { get; set; }
        public string CompleteName { get; set; }
    }

    public class DoctorTimingDetailsModel
    {
        public int BranchId { get; set; }
        public string Day { get; set; }
        public string StartTiming { get; set; }
        public string EndTiming { get; set; }
        public string BranchName { get; set; }
    }

    public class ProMasterModel
    {
        public int ProId { get; set; }
        public string Name { get; set; }
        public string ContactNo { get; set; }
        public int IsActive { get; set; }
    }

  
    public class ReferDoctorModel
    {
        public int ReferDoctorId { get; set; }
        public string Title { get; set; }
        public string Name { get; set; }
        public string DoctorName { get; set; }
        public string ContactNo { get; set; }
        public string ClinicName { get; set; }
        public string Address { get; set; }
        public int ProId { get; set; }
        public string? PROName { get; set; }
        public int IsActive { get; set; }
        public string? CreatedBy { get; set; }
        public string? CreatedOn { get; set; }
        public string? LastModifiedBy { get; set; }
        public string? LastModifiedOn { get; set; }
    }
}