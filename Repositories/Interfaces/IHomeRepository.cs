using System.Collections.Generic;
using HISWEBAPI.DTO;
using HISWEBAPI.Models;
using Microsoft.AspNetCore.Mvc;

namespace HISWEBAPI.Repositories.Interfaces
{
    public interface IHomeRepository
    {
        ServiceResult<MobileAppSettingsResponse> GetMobileAppSettings();
        ServiceResult<IEnumerable<BranchModel>> GetActiveBranchList();
        ServiceResult<IEnumerable<PickListModel>> GetPickListMaster(string fieldName);
        ServiceResult<object> GetPredefineQueryResult(string queryName, string filter1, string filter2);
        ServiceResult<AllGlobalValues> GetAllGlobalValues();
        ServiceResult<string> ClearAllCache();

        ServiceResult<IEnumerable<Dictionary<string, object>>> GetDashBoardStates(int branchId, int userId, int roleId);
        ServiceResult<MobileVerificationOtpResponseData> SendMobileVerificationOtp(SendMobileVerificationOtpRequest request);
        ServiceResult<string> VerifyMobileVerificationOtp(VerifyMobileVerificationOtpRequest request);
        ServiceResult<EmailVerificationOtpResponseData> SendEmailVerificationOtp(SendEmailVerificationOtpRequest request);
        ServiceResult<string> VerifyEmailVerificationOtp(VerifyEmailVerificationOtpRequest request);
        ServiceResult<IEnumerable<CountryMasterModel>> GetCountryMaster(int? isActive);
        ServiceResult<IEnumerable<StateMasterModel>> GetStateMaster(int countryId, int? isActive);
        ServiceResult<IEnumerable<DistrictMasterModel>> GetDistrictMaster(int stateId, int? isActive);
        ServiceResult<IEnumerable<CityMasterModel>> GetCityMaster(int districtId, int? isActive);
        ServiceResult<IEnumerable<PincodeMasterModel>> GetPincodeMaster(int cityId, int? isActive);
        ServiceResult<LocationByPincodeModel> GetLocationByPincode(int pincode);
        ServiceResult<IEnumerable<InsuranceCompanyModel>> GetAllInsuranceCompanyList();
        ServiceResult<IEnumerable<CorporateModel>> GetCorporateListByInsuranceCompanyId(int? insuranceCompanyId, int? isActive);
        ServiceResult<IEnumerable<CorporateBranchMappingModel>> GetCorporateListByBranchIdAndInsuranceCompanyId(int? branchId, int? insuranceCompanyId);
        ServiceResult<UploadDocumentFromFileManagerResponse> UploadDocument( UploadDocumentFromFileManagerRequest request,AllGlobalValues globalValues);
        ServiceResult<DTO.FileStreamResult> GetFile(string filePath);
        ServiceResult<FileBase64Result> GetFileAsBase64(string filePath);
        ServiceResult<FileExistsResult> CheckFileExists(string filePath);
        ServiceResult<IEnumerable<DoctorMasterModel>> GetDoctorMasterListByBranchId(
            int branchId,
            string departmentId = null,
            string specializationId = null,
            int? canApproveLabReport = null,
            byte? isDoctorUnit = null);
        ServiceResult<IEnumerable<CategoryTypeModel>> GetCategoryTypeList(string categoryTypeIds);
        ServiceResult<IEnumerable<CategoryModel>> GetCategoryList(string categoryIds, string categoryTypeIds);
        ServiceResult<CreateUpdateCategoryResponse> CreateUpdateCategory(CreateUpdateCategoryRequest request, AllGlobalValues globalValues);
        ServiceResult<IEnumerable<SubCategoryModel>> GetSubCategoryList(string categoryIds);
        ServiceResult<IEnumerable<SubSubCategoryModel>> GetSubSubCategoryList(string subCategoryIds);
        ServiceResult<CreateUpdateSubCategoryResponse> CreateUpdateSubCategory(CreateUpdateSubCategoryRequest request, AllGlobalValues globalValues);
        ServiceResult<CreateUpdateSubSubCategoryResponse> CreateUpdateSubSubCategory(CreateUpdateSubSubCategoryRequest request, AllGlobalValues globalValues);
        ServiceResult<IEnumerable<ServiceItemMasterModel>> GetServiceItemList(int? serviceItemId, int? isActive,string categoryTypeId, string categoryId, int? subCategoryId, int? subSubCategoryId, int? labTypeId, int? reportTypeId, string serviceName, int? isRegistrationCharge);
        ServiceResult<IEnumerable<PaymentModeMasterModel>> GetPaymentModeMasterList(string paymentModeName = null, int? isActive = null);
        ServiceResult<string> UpdateServiceItemMasterStatus(int serviceItemId, int isActive, AllGlobalValues globalValues);
        ServiceResult<IEnumerable<CorporatePaymentModeModel>> GetCorporatePaymentModes(int corporateId, int isRefundPaymentModes);
        ServiceResult<IEnumerable<DiscountApprovalModel>> GetDiscountApprovalForBilling(string discountType, int branchId);
        ServiceResult<object> CheckBedStatus(int bedId);
        ServiceResult<object> CheckPatientAdmitted(int patientId);
        ServiceResult<object> GetBedTypes(int branchId, int roomTypeId);
        ServiceResult<object> GetAvailableBeds(int branchId, int typeId);
        ServiceResult<object> GetBillingTabs(int branchId, int roleId, int tabTypeId, int roomServiceItemId, AllGlobalValues globalValues);
        ServiceResult<string> UpdateFavoriteBillingTab(UpdateFavoriteBillingTabRequest request, AllGlobalValues globalValues);
        ServiceResult<object> GetAssignBranchRight(int branchId);
        ServiceResult<IEnumerable<Dictionary<string, object>>> GetPatientLedgerBill(int patientId);
        ServiceResult<IEnumerable<Dictionary<string, object>>> GetReceiptPaymentDetails(int receiptId);

    }
}