using System.Collections.Generic;
using HISWEBAPI.DTO;
using HISWEBAPI.Models;

namespace HISWEBAPI.Repositories.Interfaces
{
    public interface IStoreRepository
    {
        ServiceResult<CreateUpdateVendorMasterResponse> CreateUpdateVendorMaster(CreateUpdateVendorMasterRequest request, AllGlobalValues globalValues);

        ServiceResult<IEnumerable<VendorMasterModel>> GetVendorMasterList(int? vendorId = null,int? isActive = null);
        ServiceResult<VendorExcelUploadResponse> VendorExcelUpload(VendorExcelUploadRequest request, AllGlobalValues globalValues);

        ServiceResult<CreateUpdateManufacturerMasterResponse> CreateUpdateManufacturerMaster(CreateUpdateManufacturerMasterRequest request, AllGlobalValues globalValues);

        ServiceResult<IEnumerable<ManufacturerMasterModel>> GetManufacturerList(int? manufacturerId = null, int? isActive = null);

        ServiceResult<CreateUpdateSaltNameMasterResponse> CreateUpdateSaltNameMaster(CreateUpdateSaltNameMasterRequest request, AllGlobalValues globalValues);

        ServiceResult<IEnumerable<SaltNameMasterModel>> GetSaltNameList(int? saltNameId = null, int? isActive = null);

        ServiceResult<CreateUpdateTaxPercentageResponse> CreateUpdateTaxPercentage(CreateUpdateTaxPercentageRequest request, AllGlobalValues globalValues);

        ServiceResult<IEnumerable<TaxPercentageModel>> GetTaxPercentageList(int? taxId = null, int? isActive = null);

        ServiceResult<CreateUpdateDrugCategoryResponse> CreateUpdateDrugCategory(CreateUpdateDrugCategoryRequest request, AllGlobalValues globalValues);

        ServiceResult<IEnumerable<DrugCategoryModel>> GetDrugCategoryList(int? drugId = null, int? isActive = null, int? isNarcoticDrug = null);

        ServiceResult<CreateUpdateRackMasterResponse> CreateUpdateRackMaster(CreateUpdateRackMasterRequest request, AllGlobalValues globalValues);

        ServiceResult<IEnumerable<RackMasterModel>> GetRackList(int? rackId = null, int? isActive = null);

        ServiceResult<LookupSaveResponse> CreateUpdatePurchaseUnit(CreateUpdatePurchaseUnitRequest request, AllGlobalValues globalValues);

        ServiceResult<IEnumerable<PurchaseUnitModel>> GetPurchaseUnitList(int? isActive = null);

        ServiceResult<LookupSaveResponse> CreateUpdateSaleUnit(CreateUpdateSaleUnitRequest request, AllGlobalValues globalValues);

        ServiceResult<IEnumerable<SaleUnitModel>> GetSaleUnitList(int? isActive = null);

        ServiceResult<LookupSaveResponse> CreateUpdateDosageForm(CreateUpdateDosageFormRequest request, AllGlobalValues globalValues);

        ServiceResult<IEnumerable<DosageFormModel>> GetDosageFormList(int? isActive = null);

        ServiceResult<CreateUpdateStoreItemMasterResponse> CreateUpdateStoreItemMaster(CreateUpdateStoreItemMasterRequest request, AllGlobalValues globalValues);

        ServiceResult<IEnumerable<StoreItemListModel>> GetStoreItemList(string? search = null, int? categoryId = null, int? isActive = null);

        ServiceResult<StoreItemDetailsModel> GetStoreItemDetails(int serviceItemId);

        // Stock Receiving (GRN)
        ServiceResult<IEnumerable<ReceivingStoreModel>> GetStockReceivingStores(int branchId, AllGlobalValues globalValues);

        ServiceResult<IEnumerable<ReceivingItemSearchModel>> SearchReceivingItem(string storeType, string? search, AllGlobalValues globalValues);

        ServiceResult<IEnumerable<StockReceivingListModel>> GetStockReceivingList(int branchId, DateTime? fromDate, DateTime? toDate, int? vendorId, int? statusId, string? search, AllGlobalValues globalValues);

        ServiceResult<StockReceivingDetailsModel> GetStockReceivingDetails(int receivingId, AllGlobalValues globalValues);

        ServiceResult<GrnUnsavedDraftModel> SaveGrnUnsavedDraft(GrnUnsavedDraftSaveRequest request, AllGlobalValues globalValues);

        ServiceResult<IEnumerable<GrnUnsavedDraftModel>> GetGrnUnsavedDrafts(int branchId, AllGlobalValues globalValues);

        ServiceResult<bool> DeleteGrnUnsavedDraft(string draftId, AllGlobalValues globalValues);

        ServiceResult<IEnumerable<BarcodeItemModel>> GetItemByBarcode(string storeType, string barcode, AllGlobalValues globalValues);

        ServiceResult<IEnumerable<ItemPurchaseHistoryModel>> GetItemPurchaseHistory(int itemId, int? excludeReceivingId, AllGlobalValues globalValues);

        ServiceResult<StockReceivingSaveResponse> SaveStockReceiving(StockReceivingSaveRequest request, AllGlobalValues globalValues);

        ServiceResult<StockReceivingSaveResponse> ChangeStockReceivingStatus(string action, StockReceivingActionRequest request, AllGlobalValues globalValues);

        // Purchase Request (2026-11-16)
        ServiceResult<IEnumerable<ReceivingStoreModel>> GetPurchaseRequestDepartments(AllGlobalValues globalValues);

        ServiceResult<IEnumerable<PurchaseRequestItemSearchModel>> SearchPurchaseRequestItem(int branchId, int fromDeptId, string storeType, string? search, bool belowReorderLevel, AllGlobalValues globalValues);

        ServiceResult<IEnumerable<PurchaseRequestListModel>> GetPurchaseRequestList(int branchId, DateTime fromDate, DateTime toDate, int? fromDeptId, int? toDeptId, string? storeType, int? statusId, string? prNo, AllGlobalValues globalValues);

        ServiceResult<PurchaseRequestDetailsModel> GetPurchaseRequestDetails(int purchaseRequestId, AllGlobalValues globalValues);

        ServiceResult<IEnumerable<ItemDepartmentStockModel>> GetItemStockByDepartment(int branchId, int itemId, AllGlobalValues globalValues);

        ServiceResult<PurchaseRequestSaveResponse> SavePurchaseRequest(PurchaseRequestSaveRequest request, AllGlobalValues globalValues);

        /// <summary>action = send | cancel</summary>
        ServiceResult<PurchaseRequestSaveResponse> ChangePurchaseRequestStatus(string action, PurchaseRequestActionRequest request, AllGlobalValues globalValues);

        ServiceResult<PurchaseRequestSaveResponse> ApprovePurchaseRequest(PurchaseRequestApproveRequest request, AllGlobalValues globalValues);

        // Purchase Order + Purchase Order Approval (2026-11-16)
        ServiceResult<IEnumerable<POTermConditionModel>> GetPOTermConditions(AllGlobalValues globalValues);
        ServiceResult<IEnumerable<PurchaseOrderItemDefaultModel>> GetPurchaseOrderItemDefaults(int branchId, string storeType, string? itemIds, bool belowReorderLevel, AllGlobalValues globalValues);
        ServiceResult<IEnumerable<ApprovedPurchaseRequestForPoModel>> GetApprovedPurchaseRequestsForPo(int branchId, string storeType, DateTime fromDate, DateTime toDate, int? fromDeptId, AllGlobalValues globalValues);
        ServiceResult<IEnumerable<PurchaseOrderItemDefaultModel>> GetPurchaseRequestItemsForPo(int branchId, string purchaseRequestIds, AllGlobalValues globalValues);
        ServiceResult<IEnumerable<PurchaseOrderListModel>> GetPurchaseOrderList(int branchId, DateTime fromDate, DateTime toDate, string? storeType, int? vendorId, int? statusId, string? poNo, AllGlobalValues globalValues);
        ServiceResult<PurchaseOrderDetailsModel> GetPurchaseOrderDetails(int poId, AllGlobalValues globalValues);
        ServiceResult<IEnumerable<PurchaseOrderSavedModel>> SavePurchaseOrder(PurchaseOrderSaveRequest request, AllGlobalValues globalValues);
        ServiceResult<PurchaseOrderSavedModel> ChangePurchaseOrderStatus(string action, PurchaseOrderActionRequest request, AllGlobalValues globalValues);
        ServiceResult<PurchaseOrderSavedModel> ApprovePurchaseOrder(PurchaseOrderApproveRequest request, AllGlobalValues globalValues);

        // Patient Medicine Issue - pharmacy sale / IPD issue (2026-11-23)
        ServiceResult<IEnumerable<SalePatientModel>> SearchPatientForSale(int branchId, int patientTypeId, string? search, AllGlobalValues globalValues);
        ServiceResult<IEnumerable<SaleItemSearchModel>> SearchSaleItem(int branchId, int roleId, string search, int searchType, AllGlobalValues globalValues);
        ServiceResult<IEnumerable<SaleBatchModel>> GetSaleItemBatches(int branchId, int roleId, string itemIds, AllGlobalValues globalValues);
        ServiceResult<PharmacyUserRightsModel> GetPharmacyUserRights(int branchId, int roleId, AllGlobalValues globalValues);
        ServiceResult<IEnumerable<PharmacySaleListModel>> GetPharmacySaleList(int branchId, int? roleId, DateTime fromDate, DateTime toDate, int? patientTypeId, string? search, AllGlobalValues globalValues);
        ServiceResult<PharmacySaleDetailsModel> GetPharmacySaleDetails(int storeBillId, AllGlobalValues globalValues);
        ServiceResult<PharmacyRequestCountsModel> GetPharmacyRequestCounts(int branchId, int roleId, AllGlobalValues globalValues);
        /// <summary>type = emr | indent</summary>
        ServiceResult<IEnumerable<PharmacyRequestModel>> GetPharmacyRequests(string type, int branchId, int roleId, int? statusId, string? search, AllGlobalValues globalValues);
        ServiceResult<IEnumerable<PharmacyRequestItemModel>> GetPharmacyRequestItems(string type, int branchId, int roleId, int requestId, AllGlobalValues globalValues);
        ServiceResult<PharmacySaleSaveResponse> SavePharmacySale(PharmacySaleSaveRequest request, AllGlobalValues globalValues);
        ServiceResult<int> RejectPatientIndent(RejectPatientIndentRequest request, AllGlobalValues globalValues);
        ServiceResult<int> SavePatientMedicineIndent(SavePatientMedicineIndentRequest request, AllGlobalValues globalValues);
        ServiceResult<int> PrintPharmacyBill(PharmacyBillPrintRequest request, AllGlobalValues globalValues);
    }
}