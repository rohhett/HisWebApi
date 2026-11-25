namespace HISWEBAPI.Models
{
    public class VendorMasterModel
    {
        public int VendorId { get; set; }
        public int TypeId { get; set; }
        public string Type { get; set; }
        public string VendorName { get; set; }
        public string ContactNo { get; set; }
        public string Email { get; set; }
        public string DLNO { get; set; }
        public string GSTINNo { get; set; }
        public string Address { get; set; }
        public string FullAddress { get; set; }
        public int? CountryId { get; set; }
        public int? StateId { get; set; }
        public int? DistrictId { get; set; }
        public int? CityId { get; set; }
        public int? Pincode { get; set; }
        public int? CreditDays { get; set; }
        /// <summary>1 when the current contact number / email was verified by OTP (Home/verify*VerificationOtp)</summary>
        public int IsContactVerified { get; set; }
        public int IsEmailVerified { get; set; }
        public string MappingBranch { get; set; }
        public int IsActive { get; set; }
        public string CreatedBy { get; set; }
        public string CreatedOn { get; set; }
        public string LastModifiedBy { get; set; }
        public string LastModifiedOn { get; set; }
    }

    public class PurchaseUnitModel
    {
        public int PurchaseUnitId { get; set; }
        public string PurchaseUnitName { get; set; }
        public int IsActive { get; set; }
        public int ItemCount { get; set; }
        public string CreatedBy { get; set; }
        public string CreatedOn { get; set; }
        public string LastModifiedBy { get; set; }
        public string LastModifiedOn { get; set; }
    }

    public class SaleUnitModel
    {
        public int SaleUnitId { get; set; }
        public string SaleUnitName { get; set; }
        public int IsActive { get; set; }
        public int ItemCount { get; set; }
        public string CreatedBy { get; set; }
        public string CreatedOn { get; set; }
        public string LastModifiedBy { get; set; }
        public string LastModifiedOn { get; set; }
    }

    public class DosageFormModel
    {
        public int DosageFormId { get; set; }
        public string DosageFormName { get; set; }
        public string Description { get; set; }
        public int IsActive { get; set; }
        public string CreatedBy { get; set; }
        public string CreatedOn { get; set; }
        public string LastModifiedBy { get; set; }
        public string LastModifiedOn { get; set; }
    }

    public class StoreItemListModel
    {
        public int ServiceItemId { get; set; }
        public int CategoryId { get; set; }
        public string StoreType { get; set; }
        public string SubGroup { get; set; }
        public string SubSubGroup { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        public string SaltName { get; set; }
        public string Strength { get; set; }
        public string SnomedCode { get; set; }
        public string ManufacturerName { get; set; }
        public string DosageFormName { get; set; }
        public string PackSize { get; set; }
        public string SaleUnitName { get; set; }
        public int Packing { get; set; }
        public string HsnCode { get; set; }
        public decimal? GstPer { get; set; }
        public string DrugSchedule { get; set; }
        public string RackCode { get; set; }
        public string ReorderLevel { get; set; }
        public int IsColdChain { get; set; }
        public int IsActive { get; set; }
    }

    public class StoreItemDetailsModel
    {
        public int ServiceItemId { get; set; }
        public int CategoryId { get; set; }
        public int SubCategoryId { get; set; }
        public int SubSubCategoryId { get; set; }
        public string Name { get; set; }
        public string Code { get; set; }
        public int DescriptionId { get; set; }
        public string SaltName { get; set; }
        public string Strength { get; set; }
        public string SnomedCode { get; set; }
        public int ManufacturerId { get; set; }
        public int DosageFormId { get; set; }
        public int DrugCategoryId { get; set; }
        public string Barcode { get; set; }
        public string PackSize { get; set; }
        public int PurchaseUnitId { get; set; }
        public int SaleUnitId { get; set; }
        public int Packing { get; set; }
        public string HsnCode { get; set; }
        public string CommodityCode { get; set; }
        public decimal? GstPer { get; set; }
        public string DrugSchedule { get; set; }
        public int RackId { get; set; }
        public int IsColdChain { get; set; }
        public int Expirable { get; set; }
        public string VedType { get; set; }
        public int? MinLevel { get; set; }
        public int? ReorderLevel { get; set; }
        public int? MaxLevel { get; set; }
        public int DoctorId { get; set; }
        public int IsKitTypeItem { get; set; }
        public int IsBagTypeItem { get; set; }
        public int IsActive { get; set; }
        public int DefaultStoreRoleId { get; set; }
        public int ExpiryAlertDays { get; set; }
        public int IsFEFOIssue { get; set; }
        public int IsBatchMandatory { get; set; }
        public int AllowExpiredIssue { get; set; }
        public string ItemClass { get; set; }
        public string StorageCondition { get; set; }
        public int IsSterile { get; set; }
        public int IsSingleUse { get; set; }
        public int IsRecallItem { get; set; }
        public int IsRestrictedItem { get; set; }
        public int IsTrackBatch { get; set; }
        public int IsBarcodeApplicable { get; set; }
        public int IsStockable { get; set; }
        public int IsCapitalItem { get; set; }
        public int IsHighRisk { get; set; }
        public string CreatedBy { get; set; }
        public string CreatedOn { get; set; }
        public string LastModifiedBy { get; set; }
        public string LastModifiedOn { get; set; }
    }

    public class RackMasterModel
    {
        public int RackId { get; set; }
        public string RackCode { get; set; }
        public string Location { get; set; }
        public int IsActive { get; set; }
        public string CreatedBy { get; set; }
        public string CreatedOn { get; set; }
        public string LastModifiedBy { get; set; }
        public string LastModifiedOn { get; set; }
    }

    public class DrugCategoryModel
    {
        public int DrugId { get; set; }
        public string DrugName { get; set; }
        public int IsNarcoticDrug { get; set; }
        public string ColorPicker { get; set; }
        public int IsActive { get; set; }
        public int MedicineCount { get; set; }
        public string CreatedBy { get; set; }
        public string CreatedOn { get; set; }
        public string LastModifiedBy { get; set; }
        public string LastModifiedOn { get; set; }
    }

    public class TaxPercentageModel
    {
        public int TaxId { get; set; }
        public string Description { get; set; }
        public decimal TaxValue { get; set; }
        public decimal CGST { get; set; }
        public decimal SGST { get; set; }
        public int IsActive { get; set; }
        public int MedicineCount { get; set; }
        public string CreatedBy { get; set; }
        public string CreatedOn { get; set; }
        public string LastModifiedBy { get; set; }
        public string LastModifiedOn { get; set; }
    }

    public class SaltNameMasterModel
    {
        public int SaltNameId { get; set; }
        public string SaltName { get; set; }
        public string TherapeuticClass { get; set; }
        public string SNOMEDCode { get; set; }
        public int IsActive { get; set; }
        public int MedicineCount { get; set; }
        public string CreatedBy { get; set; }
        public string CreatedOn { get; set; }
        public string LastModifiedBy { get; set; }
        public string LastModifiedOn { get; set; }
    }

    public class ManufacturerMasterModel
    {
        public int ManufacturerId { get; set; }
        public string ManufacturerName { get; set; }
        public string ShortCode { get; set; }
        public string CityCountry { get; set; }
        public int IsActive { get; set; }
        public int MedicineCount { get; set; }
        public string CreatedBy { get; set; }
        public string CreatedOn { get; set; }
        public string LastModifiedBy { get; set; }
        public string LastModifiedOn { get; set; }
    }

    // ================= Stock Receiving (GRN) =================

    public class StockReceivingListModel
    {
        public int ReceivingId { get; set; }
        public string ReceivingNo { get; set; } = string.Empty;
        public DateTime GrnDate { get; set; }
        public string StoreType { get; set; } = string.Empty;
        public int RoleId { get; set; }
        public string StoreName { get; set; } = string.Empty;
        public int VendorId { get; set; }
        public string VendorName { get; set; } = string.Empty;
        public string InvoiceNo { get; set; } = string.Empty;
        public DateTime? InvoiceDate { get; set; }
        public string ChallanNo { get; set; } = string.Empty;
        public DateTime? ChallanDate { get; set; }
        public decimal NetAmount { get; set; }
        public int StatusId { get; set; }
        public string Status { get; set; } = string.Empty;
        public int ItemCount { get; set; }
        public string CreatedByName { get; set; } = string.Empty;
        public DateTime? CreatedOn { get; set; }
    }

    public class StockReceivingDetailsModel
    {
        public int ReceivingId { get; set; }
        public int BranchId { get; set; }
        public int RoleId { get; set; }
        public string StoreName { get; set; } = string.Empty;
        public string StoreType { get; set; } = string.Empty;
        public int VendorId { get; set; }
        public string VendorName { get; set; } = string.Empty;
        public string GstinNo { get; set; } = string.Empty;
        public string DlNo { get; set; } = string.Empty;
        public int TypeId { get; set; }
        public string Type { get; set; } = string.Empty;
        public string ReceivingNo { get; set; } = string.Empty;
        public int ReceivingAgainstId { get; set; }
        public string InvoiceNo { get; set; } = string.Empty;
        public DateTime? InvoiceDate { get; set; }
        public string ChallanNo { get; set; } = string.Empty;
        public DateTime? ChallanDate { get; set; }
        public string GstType { get; set; } = string.Empty;
        public DateTime GrnDate { get; set; }
        public string PaymentMode { get; set; } = string.Empty;
        public string Remarks { get; set; } = string.Empty;
        public string AmendmentRemark { get; set; } = string.Empty;
        public decimal FreightCharges { get; set; }
        public decimal GrossAmount { get; set; }
        public decimal DiscountPercentage { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal TaxableAmount { get; set; }
        public decimal GstAmount { get; set; }
        public decimal NetAmount { get; set; }
        public decimal RoundOff { get; set; }
        public int StatusId { get; set; }
        public string Status { get; set; } = string.Empty;
        public string CancelReason { get; set; } = string.Empty;
        public DateTime? CancelledOn { get; set; }
        /// <summary>Latest unpost (2026-11-17): remark typed by the user, who and when</summary>
        public string UnPostRemark { get; set; } = string.Empty;
        public DateTime? UnPostedOn { get; set; }
        public string UnPostedByName { get; set; } = string.Empty;
        public DateTime? PostedOn { get; set; }
        public bool IsIssued { get; set; }
        public List<StockReceivingLineModel> Items { get; set; } = new();
    }

    public class StockReceivingLineModel
    {
        public int StockId { get; set; }
        public int ItemId { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public string ItemCode { get; set; } = string.Empty;
        public int Expirable { get; set; }
        public decimal Qty { get; set; }
        public decimal FreeQty { get; set; }
        public int Packing { get; set; }
        public int Deal1 { get; set; }
        public int Deal2 { get; set; }
        public string BatchNo { get; set; } = string.Empty;
        /// <summary>yyyy-MM</summary>
        public string Expiry { get; set; } = string.Empty;
        public string HsnCode { get; set; } = string.Empty;
        public decimal Mrp { get; set; }
        public decimal Rate { get; set; }
        public decimal? OldRate { get; set; }
        public decimal DiscPer { get; set; }
        public decimal DiscAmt { get; set; }
        public decimal SpclDiscPer { get; set; }
        public decimal SpclDiscAmt { get; set; }
        public decimal GstPer { get; set; }
        public decimal GstAmt { get; set; }
        public decimal GrossAmt { get; set; }
        public decimal NetAmt { get; set; }
        public int Units { get; set; }
        public decimal UnitCost { get; set; }
        public int IssuedQty { get; set; }
        public string Barcode { get; set; } = string.Empty;
    }

    public class ReceivingItemSearchModel
    {
        public int ItemId { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public string ItemCode { get; set; } = string.Empty;
        public int? SubSubCategoryId { get; set; }
        public int Packing { get; set; }
        public string HsnCode { get; set; } = string.Empty;
        public decimal? GstPer { get; set; }
        public int Expirable { get; set; }
        public string PurchaseUnitName { get; set; } = string.Empty;
        public string SaleUnitName { get; set; } = string.Empty;
        public string ManufacturerName { get; set; } = string.Empty;
        public string HoldStatus { get; set; } = string.Empty;
        public string HoldRemarks { get; set; } = string.Empty;
        public decimal? LastRate { get; set; }
        public decimal? LastMrp { get; set; }
    }

    /// <summary>one earlier receiving of an item (S_GetItemPurchaseHistory, OLD "Last Purchase History")</summary>
    public class ItemPurchaseHistoryModel
    {
        public int ReceivingId { get; set; }
        public string ReceivingNo { get; set; } = string.Empty;
        public DateTime GrnDate { get; set; }
        public DateTime? InvoiceDate { get; set; }
        public string InvoiceNo { get; set; } = string.Empty;
        public string ChallanNo { get; set; } = string.Empty;
        public int StatusId { get; set; }
        public string Status { get; set; } = string.Empty;
        public string StoreType { get; set; } = string.Empty;
        public string VendorName { get; set; } = string.Empty;
        public int ItemId { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public int Packing { get; set; }
        public decimal FreeQty { get; set; }
        public string BatchNo { get; set; } = string.Empty;
        public DateTime? ExpiryDate { get; set; }
        public decimal Qty { get; set; }
        public decimal Mrp { get; set; }
        public decimal MajorMrp { get; set; }
        public decimal Rate { get; set; }
        public decimal UnitCost { get; set; }
        public decimal GstPer { get; set; }
        public decimal DiscAmt { get; set; }
        public decimal DiscPer { get; set; }
        public string Barcode { get; set; } = string.Empty;
    }

    /// <summary>item found by a scanned barcode (S_GetItemByBarcode)</summary>
    public class BarcodeItemModel : ReceivingItemSearchModel
    {
        /// <summary>GRN = an earlier lot carries this barcode; Item Master = ServiceItemMaster.Barcode</summary>
        public string Source { get; set; } = string.Empty;
        public DateTime? LastBarcodeOn { get; set; }
    }

    /// <summary>one unsaved GRN kept in Redis for the user (key _GrnUnsaved_{hospId}_{userId})</summary>
    public class GrnUnsavedDraftModel
    {
        public string DraftId { get; set; } = string.Empty;
        public int BranchId { get; set; }
        public string VendorName { get; set; } = string.Empty;
        public string DocNo { get; set; } = string.Empty;
        public int ItemCount { get; set; }
        public DateTime CreatedOn { get; set; }
        public DateTime ModifiedOn { get; set; }
        public string Data { get; set; } = string.Empty;
    }

    public class ReceivingStoreModel
    {
        public int RoleId { get; set; }
        public string RoleName { get; set; } = string.Empty;
    }

    #region Purchase Request (2026-11-16)

    public class PurchaseRequestItemSearchModel
    {
        public int ItemId { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public string ItemCode { get; set; } = string.Empty;
        public int Packing { get; set; }
        public string PurchaseUnitName { get; set; } = string.Empty;
        public string SaleUnitName { get; set; } = string.Empty;
        /// <summary>From Department stock, sale units</summary>
        public int CurrentStock { get; set; }
        public int MinLevel { get; set; }
        public int ReorderLevel { get; set; }
        public int MaxLevel { get; set; }
        /// <summary>Last landed cost per purchase unit in the branch (0 = never received)</summary>
        public decimal Rate { get; set; }
    }

    public class PurchaseRequestListModel
    {
        public int PurchaseRequestId { get; set; }
        public string PurchaseRequestNo { get; set; } = string.Empty;
        public int BranchId { get; set; }
        public int FromDeptId { get; set; }
        public int ToDeptId { get; set; }
        public int CategoryId { get; set; }
        public string FromDeptName { get; set; } = string.Empty;
        public string ToDeptName { get; set; } = string.Empty;
        public string StoreType { get; set; } = string.Empty;
        public string PrDate { get; set; } = string.Empty;
        public string? Remarks { get; set; }
        public int StatusId { get; set; }
        public int IsCancel { get; set; }
        public string Status { get; set; } = string.Empty;
        public string CurrentApprover { get; set; } = string.Empty;
        public int ItemCount { get; set; }
        public int RequestedQty { get; set; }
        public int ApprovedQty { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal ApprovedAmount { get; set; }
        /// <summary>Amount the approval rule is chosen by (fixed when sent)</summary>
        public decimal ApprovalAmount { get; set; }
        /// <summary>AmountUpTo of the matched approval rule (null = no rule)</summary>
        public decimal? AmountUpTo { get; set; }
        public string ApprovalRemarks { get; set; } = string.Empty;
        public int? IsLevel1Approve { get; set; }
        public string? Level1ApproveOn { get; set; }
        public int? IsLevel2Approve { get; set; }
        public string? Level2ApproveOn { get; set; }
        public int? IsLevel3Approve { get; set; }
        public string? Level3ApproveOn { get; set; }
        public int? IsLevel4Approve { get; set; }
        public string? Level4ApproveOn { get; set; }
        public string? CancelBy { get; set; }
        public string? CancelOn { get; set; }
        public string? CancelReason { get; set; }
        public string? CreatedBy { get; set; }
        public string? CreatedOn { get; set; }
        /// <summary>Approval level this user may approve now (0 = none)</summary>
        public int FlagId { get; set; }
        public int CanApprove { get; set; }
    }

    public class PurchaseRequestItemModel
    {
        public int PurchaseRequestItemId { get; set; }
        public int ItemId { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public int Packing { get; set; }
        public string PurchaseUnitName { get; set; } = string.Empty;
        public int RequestedQty { get; set; }
        public int ApprovedQty { get; set; }
        public decimal Rate { get; set; }
        public decimal Amount { get; set; }
        public decimal ApprovedAmount { get; set; }
        public int CurrentStock { get; set; }
        public int CurrentStockAllDept { get; set; }
        public int MinLevel { get; set; }
        public int ReorderLevel { get; set; }
        public int MaxLevel { get; set; }
    }

    public class PurchaseRequestDetailsModel
    {
        public int PurchaseRequestId { get; set; }
        public string PurchaseRequestNo { get; set; } = string.Empty;
        public int BranchId { get; set; }
        public int FromDeptId { get; set; }
        public int ToDeptId { get; set; }
        public int CategoryId { get; set; }
        public string FromDeptName { get; set; } = string.Empty;
        public string ToDeptName { get; set; } = string.Empty;
        public string StoreType { get; set; } = string.Empty;
        public string? CreatedOn { get; set; }
        public string? CreatedBy { get; set; }
        public string? Remarks { get; set; }
        public string Status { get; set; } = string.Empty;
        public int StatusId { get; set; }
        public int IsCancel { get; set; }
        public string ApprovalRemarks { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public decimal ApprovedAmount { get; set; }
        public decimal ApprovalAmount { get; set; }
        public decimal? AmountUpTo { get; set; }
        public int? ApprovalFlowId { get; set; }
        public string? ApprovalFlow { get; set; }
        public int? IsAllApprovalRequired { get; set; }
        public int? ApprovalLevelId { get; set; }
        public string? ApprovalLevel { get; set; }
        public string? Level1ApproverNames { get; set; }
        public string? Level2ApproverNames { get; set; }
        public string? Level3ApproverNames { get; set; }
        public string? Level4ApproverNames { get; set; }
        public int? IsLevel1Approve { get; set; }
        public string? Level1ApprovedByName { get; set; }
        public string? Level1ApproveOn { get; set; }
        public int? IsLevel2Approve { get; set; }
        public string? Level2ApprovedByName { get; set; }
        public string? Level2ApproveOn { get; set; }
        public int? IsLevel3Approve { get; set; }
        public string? Level3ApprovedByName { get; set; }
        public string? Level3ApproveOn { get; set; }
        public int? IsLevel4Approve { get; set; }
        public string? Level4ApprovedByName { get; set; }
        public string? Level4ApproveOn { get; set; }
        public string? CancelBy { get; set; }
        public string? CancelOn { get; set; }
        public string? CancelReason { get; set; }
        public int FlagId { get; set; }
        public List<PurchaseRequestItemModel> Items { get; set; } = new();
    }

    public class ItemDepartmentStockModel
    {
        public int RoleId { get; set; }
        public string RoleName { get; set; } = string.Empty;
        public int CurrentStock { get; set; }
    }

    public class PurchaseRequestSaveResponse
    {
        public int PurchaseRequestId { get; set; }
    }

    #endregion

    #region Purchase Order (2026-11-16)

    public class POTermConditionModel
    {
        public int Id { get; set; }
        public string PoTermCondition { get; set; } = string.Empty;
        public int SequenceOrder { get; set; }
        public int IsDefault { get; set; }
    }

    /// <summary>line defaults: last GRN lot of the item in the branch (rate per pack, MRP, GST, HSN, vendor)</summary>
    public class PurchaseOrderItemDefaultModel
    {
        public int ItemId { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public string ItemCode { get; set; } = string.Empty;
        public int Packing { get; set; }
        public string PurchaseUnitName { get; set; } = string.Empty;
        public string HsnCode { get; set; } = string.Empty;
        public decimal GstPer { get; set; }
        public decimal Rate { get; set; }
        public decimal Mrp { get; set; }
        public int VendorId { get; set; }
        public string VendorName { get; set; } = string.Empty;
        /// <summary>ROL / PR: suggested or approved quantity in packs</summary>
        public decimal Qty { get; set; }
        public int BranchStock { get; set; }
        public int ReorderLevel { get; set; }
        public int MaxLevel { get; set; }
        public string PurchaseRequestNos { get; set; } = string.Empty;
    }

    public class ApprovedPurchaseRequestForPoModel
    {
        public int PurchaseRequestId { get; set; }
        public string PurchaseRequestNo { get; set; } = string.Empty;
        public string PrDate { get; set; } = string.Empty;
        public string FromDeptName { get; set; } = string.Empty;
        public string ToDeptName { get; set; } = string.Empty;
        public string StoreType { get; set; } = string.Empty;
        public int ItemCount { get; set; }
        public int ApprovedQty { get; set; }
        public decimal ApprovedAmount { get; set; }
    }

    public class PurchaseOrderListModel
    {
        public int PoId { get; set; }
        public string PoNo { get; set; } = string.Empty;
        public int BranchId { get; set; }
        public int RoleId { get; set; }
        public string FromDeptName { get; set; } = string.Empty;
        public string StoreType { get; set; } = string.Empty;
        public string PoType { get; set; } = string.Empty;
        public int TypeId { get; set; }
        public string PoTypes { get; set; } = string.Empty;
        public int VendorId { get; set; }
        public string VendorName { get; set; } = string.Empty;
        public string PoDate { get; set; } = string.Empty;
        public string Priority { get; set; } = string.Empty;
        public decimal GrossAmount { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal GstAmount { get; set; }
        public decimal NetAmount { get; set; }
        public decimal ApprovalAmount { get; set; }
        public decimal? AmountUpTo { get; set; }
        public int StatusId { get; set; }
        public int IsCancel { get; set; }
        public string Status { get; set; } = string.Empty;
        public string CurrentApprover { get; set; } = string.Empty;
        public int ItemCount { get; set; }
        public decimal TotalQty { get; set; }
        public string Remarks { get; set; } = string.Empty;
        public string ApprovalRemarks { get; set; } = string.Empty;
        public int? IsLevel1Approve { get; set; }
        public string? Level1ApproveOn { get; set; }
        public int? IsLevel2Approve { get; set; }
        public string? Level2ApproveOn { get; set; }
        public int? IsLevel3Approve { get; set; }
        public string? Level3ApproveOn { get; set; }
        public int? IsLevel4Approve { get; set; }
        public string? Level4ApproveOn { get; set; }
        public string? CancelBy { get; set; }
        public string? CancelOn { get; set; }
        public string? CancelReason { get; set; }
        public string? CreatedBy { get; set; }
        public string? CreatedOn { get; set; }
        public int FlagId { get; set; }
        public int CanApprove { get; set; }
        public int CanEdit { get; set; }
    }

    public class PurchaseOrderItemModel
    {
        public int PoItemId { get; set; }
        public int ItemId { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public string ItemCode { get; set; } = string.Empty;
        public string PurchaseUnitName { get; set; } = string.Empty;
        public decimal Qty { get; set; }
        public decimal FreeQty { get; set; }
        public int Packing { get; set; }
        public int Deal1 { get; set; }
        public int Deal2 { get; set; }
        public string HsnCode { get; set; } = string.Empty;
        public decimal Mrp { get; set; }
        public decimal Rate { get; set; }
        public decimal DiscPer { get; set; }
        public decimal DiscAmt { get; set; }
        public decimal SpclDiscPer { get; set; }
        public decimal SpclDiscAmt { get; set; }
        public decimal GstPer { get; set; }
        public decimal GstAmt { get; set; }
        public decimal GrossAmt { get; set; }
        public decimal NetAmt { get; set; }
        public decimal UnitCost { get; set; }
    }

    public class PurchaseOrderDetailsModel
    {
        public int PoId { get; set; }
        public string PoNo { get; set; } = string.Empty;
        public int BranchId { get; set; }
        public int RoleId { get; set; }
        public string FromDeptName { get; set; } = string.Empty;
        public string StoreType { get; set; } = string.Empty;
        public int CategoryId { get; set; }
        public string PoType { get; set; } = string.Empty;
        public int TypeId { get; set; }
        public string PoTypes { get; set; } = string.Empty;
        public int VendorId { get; set; }
        public string VendorName { get; set; } = string.Empty;
        public decimal GrossAmount { get; set; }
        public decimal DiscountPercentage { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal TaxableAmount { get; set; }
        public decimal GstAmount { get; set; }
        public decimal NetAmount { get; set; }
        public decimal RoundOff { get; set; }
        public decimal ApprovalAmount { get; set; }
        public decimal? AmountUpTo { get; set; }
        public string Priority { get; set; } = string.Empty;
        /// <summary>yyyy-MM-dd</summary>
        public string PoDate { get; set; } = string.Empty;
        public string? DeliveryDate { get; set; }
        public string? ValidDate { get; set; }
        public string PoRemarks { get; set; } = string.Empty;
        public string DeliveryAddress { get; set; } = string.Empty;
        public string DocumentPath { get; set; } = string.Empty;
        public string OtherTermCondition { get; set; } = string.Empty;
        public List<int> PurchaseRequestIds { get; set; } = new();
        public string Status { get; set; } = string.Empty;
        public int StatusId { get; set; }
        public int IsCancel { get; set; }
        public string ApprovalRemarks { get; set; } = string.Empty;
        public string? CreatedOn { get; set; }
        public string? CreatedBy { get; set; }
        public int? ApprovalFlowId { get; set; }
        public string? ApprovalFlow { get; set; }
        public int? IsAllApprovalRequired { get; set; }
        public int? ApprovalLevelId { get; set; }
        public string? ApprovalLevel { get; set; }
        public string? Level1ApproverNames { get; set; }
        public string? Level2ApproverNames { get; set; }
        public string? Level3ApproverNames { get; set; }
        public string? Level4ApproverNames { get; set; }
        public int? IsLevel1Approve { get; set; }
        public string? Level1ApprovedByName { get; set; }
        public string? Level1ApproveOn { get; set; }
        public int? IsLevel2Approve { get; set; }
        public string? Level2ApprovedByName { get; set; }
        public string? Level2ApproveOn { get; set; }
        public int? IsLevel3Approve { get; set; }
        public string? Level3ApprovedByName { get; set; }
        public string? Level3ApproveOn { get; set; }
        public int? IsLevel4Approve { get; set; }
        public string? Level4ApprovedByName { get; set; }
        public string? Level4ApproveOn { get; set; }
        public string? CancelBy { get; set; }
        public string? CancelOn { get; set; }
        public string? CancelReason { get; set; }
        public int FlagId { get; set; }
        public List<PurchaseOrderItemModel> Items { get; set; } = new();
        public List<POTermConditionModel> Terms { get; set; } = new();
    }

    public class PurchaseOrderSavedModel
    {
        public int PoId { get; set; }
        public string PoNo { get; set; } = string.Empty;
        public string VendorName { get; set; } = string.Empty;
        public decimal NetAmount { get; set; }
        /// <summary>Save &amp; Send: false when this PO saved but could not be sent (message says why)</summary>
        public bool Sent { get; set; }
        public string? SendMessage { get; set; }
    }

    public class PurchaseOrderDocumentModel
    {
        public string DocumentPath { get; set; } = string.Empty;
    }

    #endregion

    // ================= Patient Medicine Issue (pharmacy sale / IPD issue, 2026-11-23) =================

    public class SalePatientModel
    {
        public int PatientId { get; set; }
        public string Uhid { get; set; } = string.Empty;
        public string PatientName { get; set; } = string.Empty;
        public string Age { get; set; } = string.Empty;
        public string Gender { get; set; } = string.Empty;
        public string ContactNumber { get; set; } = string.Empty;
        public int? VisitId { get; set; }
        public string IpdNo { get; set; } = string.Empty;
        public DateTime? AdmissionDate { get; set; }
        public int CorporateId { get; set; }
        public string CorporateName { get; set; } = string.Empty;
        public int? DoctorId { get; set; }
        public string DoctorName { get; set; } = string.Empty;
        public string WardBed { get; set; } = string.Empty;
        public int IsGeneralPatient { get; set; }
    }

    public class SaleItemSearchModel
    {
        public int ItemId { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public string SaltName { get; set; } = string.Empty;
        public string ManufacturerName { get; set; } = string.Empty;
        public string SaleUnitName { get; set; } = string.Empty;
        public string DrugCategory { get; set; } = string.Empty;
        public int IsNarcotic { get; set; }
        public string ColorPicker { get; set; } = string.Empty;
        public int HoldStatusId { get; set; }
        public string HoldStatus { get; set; } = string.Empty;
        public string HoldRemarks { get; set; } = string.Empty;
        public string CorporateIds { get; set; } = string.Empty;
        public int TotalStock { get; set; }
        public int BatchCount { get; set; }
        public int StockId { get; set; }
        public string BatchNo { get; set; } = string.Empty;
        public DateTime? ExpiryDate { get; set; }
        public decimal Mrp { get; set; }
        public decimal UnitCost { get; set; }
        public decimal GstPer { get; set; }
    }

    public class SaleBatchModel
    {
        public int StockId { get; set; }
        public int ItemId { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public string ItemCode { get; set; } = string.Empty;
        public int? SubSubCategoryId { get; set; }
        public string BatchNo { get; set; } = string.Empty;
        public DateTime? ExpiryDate { get; set; }
        public int CurrentStock { get; set; }
        public decimal Mrp { get; set; }
        public decimal UnitCost { get; set; }
        public decimal GstPer { get; set; }
        public int IsActive { get; set; }
        public int IsNarcotic { get; set; }
        public string CorporateIds { get; set; } = string.Empty;
        public int HoldStatusId { get; set; }
        public string HoldStatus { get; set; } = string.Empty;
        public int IsExpired { get; set; }
    }

    public class PharmacySaleListModel
    {
        public int StoreBillId { get; set; }
        public string BillNo { get; set; } = string.Empty;
        public DateTime BillDate { get; set; }
        public int PatientTypeId { get; set; }
        public string PatientType { get; set; } = string.Empty;
        public int IsWalkIn { get; set; }
        public string Source { get; set; } = string.Empty;
        public int PatientId { get; set; }
        public string Uhid { get; set; } = string.Empty;
        public string PatientName { get; set; } = string.Empty;
        public string VisitNo { get; set; } = string.Empty;
        public string StoreName { get; set; } = string.Empty;
        public int TotalItems { get; set; }
        public int TotalUnits { get; set; }
        public decimal NetAmount { get; set; }
        public decimal PaidAmount { get; set; }
        public decimal BalanceAmount { get; set; }
        public int IsCancel { get; set; }
        public string ReceiptNo { get; set; } = string.Empty;
        public string CreatedByName { get; set; } = string.Empty;
        public int PrintCount { get; set; }
    }

    public class PharmacySaleLineModel
    {
        public int ItemId { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public string BatchNo { get; set; } = string.Empty;
        public DateTime? ExpiryDate { get; set; }
        public decimal Qty { get; set; }
        public decimal Rate { get; set; }
        public decimal GrossAmt { get; set; }
        public decimal DiscPer { get; set; }
        public decimal DiscAmt { get; set; }
        public decimal GstPer { get; set; }
        public decimal TaxAmt { get; set; }
        public decimal NetAmt { get; set; }
        public int IsNarcotic { get; set; }
    }

    public class PharmacySalePaymentModel
    {
        public int PaymentModeId { get; set; }
        public string PaymentMode { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string ReferenceNo { get; set; } = string.Empty;
    }

    public class PharmacySaleDetailsModel : PharmacySaleListModel
    {
        public string Age { get; set; } = string.Empty;
        public string Gender { get; set; } = string.Empty;
        public string ContactNumber { get; set; } = string.Empty;
        public string CorporateName { get; set; } = string.Empty;
        public string DoctorName { get; set; } = string.Empty;
        public string DoctorRef { get; set; } = string.Empty;
        public string Witness { get; set; } = string.Empty;
        public int SaleTypeId { get; set; }
        public decimal GrossAmount { get; set; }
        public decimal ItemDiscountAmount { get; set; }
        public decimal BillDiscountPer { get; set; }
        public decimal BillDiscountAmount { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal RoundOff { get; set; }
        // bill slip (2026-11-23)
        public string WardBed { get; set; } = string.Empty;
        public string BranchName { get; set; } = string.Empty;
        public string BranchAddress { get; set; } = string.Empty;
        public string BranchPhone { get; set; } = string.Empty;
        public string BranchEmail { get; set; } = string.Empty;
        public string LastPrintedByName { get; set; } = string.Empty;
        public DateTime? LastPrintedOn { get; set; }
        public List<PharmacySaleLineModel> Lines { get; set; } = new();
        public List<PharmacySalePaymentModel> Payments { get; set; } = new();
    }

    public class PharmacyRequestCountsModel
    {
        public int EmrCount { get; set; }
        public int IndentCount { get; set; }
        public int IndentUrgentCount { get; set; }
        public int DischargeCount { get; set; }
    }

    public class PharmacyUserRightsModel
    {
        public int CanSellNarcoDrugs { get; set; }
        public decimal MaxDiscountPer { get; set; }
    }

    /// <summary>One row of the EMR / Ward-indent request drawer list.</summary>
    public class PharmacyRequestModel
    {
        public int RequestId { get; set; }          // EMR: OPD visit id, Indent: IndentId, Discharge: IPD visit id
        public string RefNo { get; set; } = string.Empty;
        public string Priority { get; set; } = string.Empty;
        public int StatusId { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime? RequestedOn { get; set; }
        public int PatientId { get; set; }
        public string Uhid { get; set; } = string.Empty;
        public string PatientName { get; set; } = string.Empty;
        public string Age { get; set; } = string.Empty;
        public string Gender { get; set; } = string.Empty;
        public int? VisitId { get; set; }           // IPD admission visit (indent)
        public string IpdNo { get; set; } = string.Empty;
        public string WardBed { get; set; } = string.Empty;
        public int? DoctorId { get; set; }
        public string DoctorName { get; set; } = string.Empty;
        public string RaisedBy { get; set; } = string.Empty;
        public string Remarks { get; set; } = string.Empty;
        public string BillNo { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public int ItemCount { get; set; }
        /// <summary>indent still open but partly issued / rejected (design "Partial" chip)</summary>
        public int IsPartial { get; set; }
        /// <summary>discharge request: the patient is already discharged (an IPD issue is no longer possible)</summary>
        public int IsDischarged { get; set; }
    }

    public class PharmacyRequestItemModel
    {
        public int? IndentItemId { get; set; }
        public int? ItemId { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public string SaltName { get; set; } = string.Empty;
        public string DosageForm { get; set; } = string.Empty;
        public string Instruction { get; set; } = string.Empty;
        public int RequestedQty { get; set; }
        public int IssuedQty { get; set; }
        public int RejectedQty { get; set; }
        public int PendingQty { get; set; }
        public int InStock { get; set; }
    }
}
