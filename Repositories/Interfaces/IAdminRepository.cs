using HISWEBAPI.DTO;
using HISWEBAPI.Models;
using System.Collections.Generic;

namespace HISWEBAPI.Repositories.Interfaces
{
    public interface IAdminRepository
    {
        ServiceResult<string> CreateUpdateRoleMaster(RoleMasterRequest request, AllGlobalValues globalValues);
        ServiceResult<string> UpdateRoleMasterStatus(int roleId, int isActive, AllGlobalValues globalValues);
        ServiceResult<IEnumerable<RoleMasterModel>> RoleMasterList(int? roleId = null);
        ServiceResult<IEnumerable<FaIconModel>> getFaIconMaster();
        ServiceResult<UserMasterResponse> CreateUpdateUserMaster(UserMasterRequest request);
        ServiceResult<string> UpdateUserMasterStatus(int userId, int isActive, AllGlobalValues globalValues);
        ServiceResult<IEnumerable<UserMasterModel>> UserMasterList(int? userId = null);
        ServiceResult<string> CreateUpdateUserDepartment(UserDepartmentRequest request, AllGlobalValues globalValues);
        ServiceResult<string> UpdateUserDepartmentStatus(int id, int isActive, AllGlobalValues globalValues);
        ServiceResult<IEnumerable<UserDepartmentMasterModel>> UserDepartmentList(int? id = null);
        ServiceResult<string> CreateUpdateUserGroupMaster(UserGroupRequest request, AllGlobalValues globalValues);
        ServiceResult<string> UpdateUserGroupStatus(int id, int isActive, AllGlobalValues globalValues);
        ServiceResult<IEnumerable<UserGroupMasterModel>> UserGroupList(int? id = null);
        ServiceResult<string> CreateUpdateUserGroupMembers(UserGroupMembersRequest request, AllGlobalValues globalValues);
        ServiceResult<IEnumerable<UserGroupMembersModel>> UserGroupMembersList(int? groupId);
        ServiceResult<IEnumerable<UserRoleMappingModel>> GetAssignRoleForUserAuthorization(int branchId, int typeId, int userId);
        ServiceResult<string> SaveUpdateRoleMapping(int userId, int branchId, int typeId, List<UserRoleMappingRequest> request, AllGlobalValues globalValues);
        ServiceResult<IEnumerable<UserRightMappingModel>> GetAssignUserRightMapping(int branchId,int typeId,int userId,int roleId);
        ServiceResult<string> SaveUpdateUserRightMapping(SaveUserRightMappingRequest request, AllGlobalValues globalValues);
        ServiceResult<IEnumerable<DashboardUserRightMappingModel>> GetAssignDashBoardUserRight(int branchId,int typeId,int userId,int roleId);
        ServiceResult<string> SaveUpdateDashBoardUserRightMapping(SaveDashboardUserRightMappingRequest request, AllGlobalValues globalValues);
        ServiceResult<NavigationTabMasterResponse> CreateUpdateNavigationTabMaster(NavigationTabMasterRequest request, AllGlobalValues globalValues);
        ServiceResult<IEnumerable<NavigationTabMasterModel>> GetNavigationTabMaster();
        ServiceResult<NavigationSubMenuMasterResponse> CreateUpdateNavigationSubMenuMaster(NavigationSubMenuMasterRequest request,AllGlobalValues globalValues);
        ServiceResult<IEnumerable<NavigationSubMenuMasterModel>> GetNavigationSubMenuMaster();
        ServiceResult<string> SaveUpdateRoleWiseMenuMapping(SaveRoleWiseMenuMappingRequest request, AllGlobalValues globalValues);
        ServiceResult<IEnumerable<RoleWiseMenuMappingModel>> GetRoleWiseMenuMapping(int branchId, int roleId);
        ServiceResult<string> SaveUpdateUserMenuMaster(SaveUserMenuMasterRequest request, AllGlobalValues globalValues);
        ServiceResult<IEnumerable<UserWiseMenuMasterModel>> GetUserWiseMenuMaster(int branchId, int typeId, int userId, int roleId);
        ServiceResult<string> SaveUpdateUserCorporateMapping(SaveUserCorporateMappingRequest request, AllGlobalValues globalValues);
        ServiceResult<IEnumerable<UserWiseCorporateMappingModel>> GetUserWiseCorporateMapping(int branchId, int typeId, int userId);
        ServiceResult<string> SaveUpdateUserBedMapping(SaveUserBedMappingRequest request, AllGlobalValues globalValues);
        ServiceResult<IEnumerable<UserWiseBedMappingModel>> GetUserWiseBedMapping(int branchId, int typeId, int userId);
        ServiceResult<BranchMasterResponse> CreateUpdateBranchMaster(BranchMasterRequest request, AllGlobalValues globalValues);
        ServiceResult<IEnumerable<BranchMasterModel>> GetBranchDetails(int? branchId = null);
        ServiceResult<int> CreateUpdateStateMaster(CreateUpdateStateMasterRequest request, AllGlobalValues globalValues);
        ServiceResult<int> CreateUpdateDistrictMaster(CreateUpdateDistrictMasterRequest request, AllGlobalValues globalValues);
        ServiceResult<int> CreateUpdateCityMaster(CreateUpdateCityMasterRequest request, AllGlobalValues globalValues);
        ServiceResult<int> CreateUpdatePincodeMaster(CreateUpdatePincodeMasterRequest request, AllGlobalValues globalValues);
        ServiceResult<HeaderMasterResponse> CreateUpdateHeaderMaster(HeaderMasterRequest request, AllGlobalValues globalValues);
        ServiceResult<IEnumerable<HeaderMasterModel>> GetHeaderMaster(int branchId, int roleId, int typeId, int isHeader);
        ServiceResult<IEnumerable<SequenceTypeMasterModel>> GetSequenceTypeList();
        ServiceResult<CreateUpdateSequenceMasterResponse> CreateUpdateSequenceMaster(CreateUpdateSequenceMasterRequest request, AllGlobalValues globalValues);
        ServiceResult<IEnumerable<SequenceMasterModel>> GetSequenceMaster(int sequenceTypeId);
        ServiceResult<CreateUpdateBranchSequenceMappingResponse> CreateUpdateBranchSequenceMapping(CreateUpdateBranchSequenceMappingRequest request,AllGlobalValues globalValues);
        ServiceResult<IEnumerable<BranchSequenceMappingModel>> GetBranchSequenceMapping();
        ServiceResult<LabReportLetterHeadResponse> CreateUpdateLabReportLetterHead(LabReportLetterHeadRequest request,AllGlobalValues globalValues);
        ServiceResult<IEnumerable<LabReportLetterHeadMaster>> GetLabReportLetterHeadList();
        ServiceResult<string> DeleteLetterHeadMaster(int id, AllGlobalValues globalValues);
        ServiceResult<DoctorSignatureMasterResponse> CreateUpdateDoctorSignatureMaster(DoctorSignatureMasterRequest request, AllGlobalValues globalValues);
        ServiceResult<IEnumerable<DoctorSignatureMaster>> GetDoctorSignatureMasterList();
        ServiceResult<string> DeleteDoctorSignatureMaster(int id, AllGlobalValues globalValues);
        ServiceResult<BankMasterResponse> CreateUpdateBankMaster(BankMasterRequest request, AllGlobalValues globalValues);
        ServiceResult<IEnumerable<BankMasterModel>> GetBankList(int? bankId = null, int? isActive = null);
        ServiceResult<BankDetailMasterResponse> CreateUpdateBankDetailMaster(BankDetailMasterRequest request, AllGlobalValues globalValues);
        ServiceResult<IEnumerable<BankDetailMasterModel>> GetBankDetailList(int? bankId = null, int? isActive = null);
        ServiceResult<MRDRoomMasterResponse> CreateUpdateMRDRoomMaster(MRDRoomMasterRequest request, AllGlobalValues globalValues);
        ServiceResult<IEnumerable<MRDRoomMasterModel>> GetMRDRoomMaster(int? roomId = 0, int? activeFlag = 0);
        ServiceResult<MRDRackMasterResponse> CreateUpdateMRDRackMaster(MRDRackMasterRequest request, AllGlobalValues globalValues);
        ServiceResult<IEnumerable<MRDRackMasterModel>> GetMRDRackMaster(int roomId, int? rackId = 0, int? activeFlag = 0);
        ServiceResult<MRDShelfMasterResponse> CreateUpdateMRDShelfMaster(MRDShelfMasterRequest request, AllGlobalValues globalValues);
        ServiceResult<IEnumerable<MRDShelfMasterModel>> GetMRDShelfMaster(int roomId, int rackId, int? shelfId = 0, int? activeFlag = 0);
        ServiceResult<PatientDocumentMasterResponse> CreateUpdatePatientDocumentMaster(PatientDocumentMasterRequest request, AllGlobalValues globalValues);
        ServiceResult<IEnumerable<PatientDocumentMasterModel>> GetPatientDocumentMaster(int? isActive = null);
        ServiceResult<IEnumerable<OutSourceLabMasterModel>> GetOutSourceLabMasterList( int? isActive = null);
        ServiceResult<SaveOutSourceLabMasterResponse> SaveOutSourceLabMaster( SaveOutSourceLabMasterRequest request, AllGlobalValues globalValues);
        ServiceResult<IEnumerable<RateListMasterModel>> GetRateListMaster(string? rateListName, int? isActive);
        ServiceResult<string> CreateUpdateRateListMaster(CreateUpdateRateListMasterRequest request, AllGlobalValues globalValues);
        ServiceResult<IEnumerable<object>> GetTariffMaster(
        string rateListId, string patientType, string bedTypeId,
        string doctorId, string categoryId, string subCategoryId,
        string subSubCategoryId, string serviceItemId, string serviceName);
        ServiceResult<string> CreateUpdateTariffMaster( CreateUpdateTariffMasterRequest request, AllGlobalValues globalValues);
        ServiceResult<InsuranceCompanyMasterResponse> CreateUpdateInsuranceCompanyMaster(InsuranceCompanyMasterRequest request, AllGlobalValues globalValues);
        ServiceResult<IEnumerable<InsuranceCompanyMasterModel>> GetInsuranceCompanyMasterList();
        ServiceResult<CorporateTypeMasterResponse> CreateUpdateCorporateTypeMaster(CorporateTypeMasterRequest request, AllGlobalValues globalValues);
        ServiceResult<IEnumerable<CorporateTypeMasterModel>> GetCorporateTypeMasterList();
        ServiceResult<CorporateMasterResponse> CreateUpdateCorporateMaster(CorporateMasterRequest request, AllGlobalValues globalValues);
        ServiceResult<IEnumerable<CorporateMasterDetailModel>> GetCorporateMasterList(int? corporateId = null, string corporateName = null, int? insuranceCompanyId = null, string insuranceCompanyName = null, int? isActive = null);
        ServiceResult<string> UpdateCorporateMasterStatus(int corporateId, int isActive, AllGlobalValues globalValues);
        ServiceResult<DiscountApprovalMasterResponse> CreateUpdateDiscountApprovalMaster(DiscountApprovalMasterRequest request, AllGlobalValues globalValues);
        ServiceResult<IEnumerable<DiscountApprovalMasterModel>> GetDiscountApprovalMasterList(string name = null, int? isActive = null);
        ServiceResult<object> SaveUserwiseDiscountMaster(List<UserwiseDiscountMasterRequest> request, AllGlobalValues globalValues);
        ServiceResult<object> GetUserwiseDiscountMaster();
        ServiceResult<CreateUpdateDoctorHeaderResponse> CreateUpdateDoctorHeader( CreateUpdateDoctorHeaderRequest request, AllGlobalValues globalValues);
        ServiceResult<IEnumerable<DoctorHeaderMasterModel>> GetAllDoctorHeaderMaster(int? headerId = null);
        ServiceResult<IEnumerable<DoctorHeaderLOVModel>> GetDoctorHeaderLOVs(int headerId);
        ServiceResult<IEnumerable<DoctorHeaderMappingModel>> GetDoctorHeaderMappingForMaster( int typeId, int relatedToId);
        ServiceResult<string> SaveDoctorHeaderDepartmentMapping(SaveDoctorHeaderMappingRequest request, AllGlobalValues globalValues);
        ServiceResult<object> CreateUpdateServiceItemMaster(CreateUpdateServiceItemMasterRequest request, AllGlobalValues globalValues);
        ServiceResult<object> CreateUpdatePrintGroupMaster(CreateUpdatePrintGroupMasterRequest request, AllGlobalValues globalValues);
        ServiceResult<object> GetPrintGroupMaster(int? printGroupId);
        ServiceResult<object> CreateUpdateWardNameMaster(CreateUpdateWardNameMasterRequest request, AllGlobalValues globalValues);
        ServiceResult<object> GetWardNameMaster(int? wardNameId);
        ServiceResult<CreateUpdateBlockMasterResponse> CreateUpdateBlockMaster(CreateUpdateBlockMasterRequest request, AllGlobalValues globalValues);
        ServiceResult<IEnumerable<BlockMasterModel>> GetBlockList(int? BlockId = null);
        ServiceResult<CreateUpdateFloorMasterResponse> CreateUpdateFloorMaster(CreateUpdateFloorMasterRequest request, AllGlobalValues globalValues);
        ServiceResult<IEnumerable<FloorMasterModel>> GetFloorList(int? floorId = null);
        ServiceResult<CreateUpdateBedMasterResponse> CreateUpdateBedMaster(CreateUpdateBedMasterRequest request, AllGlobalValues globalValues);
        ServiceResult<object> GetAllBedList(int? bedId = null, int? isActive = null, int? blockId=null,int? floorId = null, int? wardNameId = null, int? branchId = null, int? typeId = null);
        ServiceResult<object> CreateUpdateTabGroupTypeMaster(CreateUpdateTabGroupTypeMasterRequest request, AllGlobalValues globalValues);
        ServiceResult<object> GetTabGroupTypeMaster(int? groupTypeId, int? isActive);
        ServiceResult<object> CreateUpdateIPDTabMaster(CreateUpdateIPDTabMasterRequest request, AllGlobalValues globalValues);
        ServiceResult<object> GetIPDTabMaster(int? tabId, int? groupTypeId, int? tabTypeId, int? roomTypeId,string tabName, int? isActive);
        ServiceResult<string> SaveUpdateRoleWiseIPDTabMapping(SaveRoleWiseIPDTabMappingRequest request, AllGlobalValues globalValues);
        ServiceResult<IEnumerable<Dictionary<string, object>>> GetRoleWiseIPDTabListMaster(int roleId);
        ServiceResult<string> SaveUpdateUserIPDTabMapping(SaveUserIPDTabMappingRequest request, AllGlobalValues globalValues);
        ServiceResult<IEnumerable<Dictionary<string, object>>> GetUserGrantedRemainingTabMaster(int branchId, int typeId, int userId, int roleId);
        ServiceResult<CreateUpdateApprovalAuthorityMasterResponse> CreateUpdateApprovalAuthorityMaster(CreateUpdateApprovalAuthorityMasterRequest request,AllGlobalValues globalValues);
        ServiceResult<object> GetApprovalAuthorityMasterList(int approvalTypeId);
        ServiceResult<string> UpdateApprovalAuthorityMasterStatus(int id, AllGlobalValues globalValues);
        // Branch Corporate Ratelist Mapping
        ServiceResult<object> SaveBranchCorporateRatelistMapping(SaveBranchCorporateRatelistMappingRequest request, AllGlobalValues globalValues);
        ServiceResult<object> GetBranchCorporateRatelistMapping(int? branchId = null, int? corporateId = null);
        // Branch Corporate Service Exclusion Mapping
        ServiceResult<object> SaveBranchCorporateServiceExclusionMapping(SaveBranchCorporateServiceExclusionRequest request, AllGlobalValues globalValues);
        ServiceResult<object> GetBranchCorporateServiceExclusionMapping(int? branchId = null, int? corporateId = null);
        // Branch Right Mapping
        ServiceResult<object> SaveBranchRightMapping(SaveBranchRightMappingRequest request, AllGlobalValues globalValues);
        ServiceResult<object> GetBranchRightMapping(int branchId);
        ServiceResult<string> UpdateDefaultBranchSetting(UpdateDefaultBranchSettingRequest request, AllGlobalValues globalValues);
        ServiceResult<object> GetVitalMasterList(int? isActive);
        ServiceResult<object> CreateUpdateVitalMaster(CreateUpdateVitalMasterRequest request, AllGlobalValues globalValues);
        ServiceResult<object> GetVitalUnitMasterList();
        ServiceResult<object> CreateUpdateVitalUnitMaster(CreateUpdateVitalUnitMasterRequest request, AllGlobalValues globalValues);

        ServiceResult<object> GetVitalDepartmentMapping(int typeId, int relatedToId);
        ServiceResult<string> SaveVitalDepartmentMapping(SaveVitalDepartmentMappingRequest request, AllGlobalValues globalValues);
        ServiceResult<CreateUpdatePackageMasterResponse> CreateUpdatePackageMaster(
    CreateUpdatePackageMasterRequest request,
    AllGlobalValues globalValues);

        ServiceResult<CreateUpdateIPDPackageMasterResponse> CreateUpdateIPDPackageMaster(CreateUpdateIPDPackageMasterRequest request, AllGlobalValues globalValues);
        ServiceResult<object> GetIPDPackageSetupMapping(int packageId);

        ServiceResult<string> UpdateNavigationSubMenuSequenceNo(UpdateNavigationSubMenuSequenceRequest request, AllGlobalValues globalValues);
        ServiceResult<string> UpdateNavigationTabSequenceNo(UpdateNavigationTabSequenceRequest request, AllGlobalValues globalValues);

        ServiceResult<CreateUpdateSurgeryComponentMasterResponse> CreateUpdateSurgeryComponentMaster(CreateUpdateSurgeryComponentMasterRequest request, AllGlobalValues globalValues);
        ServiceResult<object> GetSurgeryComponentsList(int? isActive);

        ServiceResult<object> GetDischargeProcessMaster(int? isActive);
        ServiceResult<object> GetDischargeProcessMasterById(int dischargeProcessId);
        ServiceResult<CreateUpdateDischargeProcessMasterResponse> CreateUpdateDischargeProcessMaster( CreateUpdateDischargeProcessMasterRequest request, AllGlobalValues globalValues);
        ServiceResult<string> UpdateDischargeProcessSequence( UpdateDischargeProcessSequenceRequest request, AllGlobalValues globalValues);

        ServiceResult<string> SaveDischargeProcessCorporateMapping(  SaveDischargeProcessCorporateMappingRequest request, AllGlobalValues globalValues);
        ServiceResult<object> GetDischargeProcessCorporateMapping(int? dischargeProcessId);
        ServiceResult<string> SaveUpdateUserDischargeProcessMapping(SaveUserDischargeProcessMappingRequest request, AllGlobalValues globalValues);
        ServiceResult<object> GetUserWiseDischargeProcessMapping(int branchId, int typeId, int userId);

        ServiceResult<CreateUpdateOTMasterResponse> CreateUpdateOTMaster(CreateUpdateOTMasterRequest request, AllGlobalValues globalValues);
        ServiceResult<object> GetOTMasterList(int? isActive);

        // ─── OT Process ────────────────────────────────────────────────────────────
        ServiceResult<object> GetOTProcessMaster(int? isActive);
        ServiceResult<CreateUpdateOTProcessMasterResponse> CreateUpdateOTProcessMaster(CreateUpdateOTProcessMasterRequest request, AllGlobalValues globalValues);
        ServiceResult<string> UpdateOTProcessSequence(UpdateOTProcessSequenceRequest request, AllGlobalValues globalValues);

        ServiceResult<string> SaveUpdateUserOTProcessMapping(SaveUserOTProcessMappingRequest request, AllGlobalValues globalValues);
        ServiceResult<object> GetUserWiseOTProcessMapping(int branchId, int typeId, int userId);
    }
}