using System.Collections.Generic;
using HISWEBAPI.DTO;
using HISWEBAPI.Models;

namespace HISWEBAPI.Repositories.Interfaces
{
    public interface IDoctorRepository
    {
        ServiceResult<CreateUpdateDoctorDepartmentResponse> CreateUpdateDoctorDepartment(CreateUpdateDoctorDepartmentRequest request,AllGlobalValues globalValues);
        ServiceResult<IEnumerable<DoctorDepartmentModel>> GetDoctorDepartmentList(int? departmentId = null,int? isActive = null);
        ServiceResult<CreateUpdateDoctorSpecializationResponse> CreateUpdateDoctorSpecialization(CreateUpdateDoctorSpecializationRequest request,AllGlobalValues globalValues);
        ServiceResult<IEnumerable<DoctorSpecializationModel>> GetDoctorSpecializationList(int? specializationId = null,int? isActive = null);
        ServiceResult<ExcelUploadResponse<DoctorUploadRow>> DoctorExcelUpload(DoctorExcelUploadRequest request, AllGlobalValues globalValues);
        ServiceResult<CreateUpdateDoctorMasterResponse> CreateUpdateDoctorMaster(CreateUpdateDoctorMasterRequest request,AllGlobalValues globalValues);
        ServiceResult<CreateUpdateDoctorTimingDetailsResponse> CreateUpdateDoctorTimingDetails(CreateUpdateDoctorTimingDetailsRequest request,AllGlobalValues globalValues);
        ServiceResult<DoctorUnitMappingResponse> CreateUpdateDoctorUnitMaster(CreateUpdateDoctorUnitMasterRequest request, AllGlobalValues globalValues);
        ServiceResult<DoctorUnitMappingResponse> CreateUpdateDoctorUnitMapping(CreateUpdateDoctorUnitMappingRequest request, AllGlobalValues globalValues);
        ServiceResult<IEnumerable<DoctorMasterModelAll>> GetDoctorMaster(int? doctorId = null, int? isDoctorUnit = null,int? doctorDepartmentId=null, int? isActive = null);
        ServiceResult<IEnumerable<DoctorUnitMappingModel>> GetDoctorUnitMapping(int unitId);
        ServiceResult<IEnumerable<DoctorTimingDetailsModel>> GetDoctorTimingDetails(int doctorId);
        ServiceResult<string> UpdateDoctorMasterStatus(int doctorId, int isActive, AllGlobalValues globalValues);
        ServiceResult<CreateUpdateProMasterResponse> CreateUpdateProMaster(CreateUpdateProMasterRequest request, AllGlobalValues globalValues);
        ServiceResult<IEnumerable<ProMasterModel>> GetProList( int? proId = null,int? isActive = null);
        ServiceResult<CreateUpdateReferDoctorResponse> CreateUpdateReferDoctor( CreateUpdateReferDoctorRequest request, AllGlobalValues globalValues);
        ServiceResult<string> UpdateReferDoctorMasterStatus(int referDoctorId, int isActive, AllGlobalValues globalValues);
        ServiceResult<IEnumerable<ReferDoctorModel>> GetReferDoctorList( int? referDoctorId = null, int? isActive = null);

    }
}