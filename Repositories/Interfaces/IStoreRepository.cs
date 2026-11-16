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
    }
}