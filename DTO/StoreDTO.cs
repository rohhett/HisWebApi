using System.ComponentModel.DataAnnotations;

namespace HISWEBAPI.DTO
{
    public class CreateUpdateVendorMasterRequest
    {
        public int VendorId { get; set; } = 0;

        [Required(ErrorMessage = "TypeId is required")]
        public int TypeId { get; set; }

        [Required(ErrorMessage = "Type is required")]
        [StringLength(15, ErrorMessage = "Type cannot exceed 15 characters")]
        public string Type { get; set; }

        [Required(ErrorMessage = "Vendor name is required")]
        [StringLength(256, ErrorMessage = "Vendor name cannot exceed 256 characters")]
        public string VendorName { get; set; }

        [Required(ErrorMessage = "Contact number is required")]
        [StringLength(15, ErrorMessage = "Contact number cannot exceed 15 characters")]
        public string ContactNo { get; set; }

        [RegularExpression(@"^$|^[^@\s]+@[^@\s]+\.[^@\s]+$",ErrorMessage = "Invalid email format")]
        [StringLength(100, ErrorMessage = "Email cannot exceed 100 characters")]
        public string? Email { get; set; }

        [Required(ErrorMessage = "Drug Licence No. is required")]
        [StringLength(100, ErrorMessage = "Drug Licence No. cannot exceed 100 characters")]
        public string DLNO { get; set; }

        [Required(ErrorMessage = "GSTIN number is required")]
        [StringLength(100, ErrorMessage = "GSTIN number cannot exceed 100 characters")]
        public string GSTINNo { get; set; }

        public string Address { get; set; }

        public int? CountryId { get; set; }

        public int? StateId { get; set; }

        public int? DistrictId { get; set; }

        public int? CityId { get; set; }

        [StringLength(100, ErrorMessage = "Mapping branch cannot exceed 100 characters")]

        [Required(ErrorMessage = "Mapping BranchId is required")]
        public string MappingBranch { get; set; }

        [Required(ErrorMessage = "IsActive is required")]
        public int IsActive { get; set; }

        // optional; when sent it must be 6 digits (checked in StoreController)
        public int? Pincode { get; set; }

        // design field (MediStock supplier master, KB conflict C18); optional
        [Range(0, 365, ErrorMessage = "Credit days must be between 0 and 365")]
        public int? CreditDays { get; set; }

    }

    public class CreateUpdateVendorMasterResponse
    {
        public int VendorId { get; set; }
    }

    /// <summary>One vendor row read from the upload Excel (all values as text, as typed in the sheet).</summary>
    public class VendorUploadRow
    {
        public int RowNo { get; set; }
        public string? VendorName { get; set; }
        public string? ContactNo { get; set; }
        public string? Email { get; set; }
        public string? DLNO { get; set; }
        public string? GSTINNo { get; set; }
        public string? CreditDays { get; set; }
        public string? Address { get; set; }
        /// <summary>Optional. When given, Country/State/District/City are taken from Pincode Master.</summary>
        public string? Pincode { get; set; }
    }

    public class VendorExcelUploadRequest
    {
        /// <summary>false = only validate the rows, true = validate and save them (all or nothing).</summary>
        public bool SaveData { get; set; }

        /// <summary>Branch ids for every uploaded vendor, comma separated (chosen once at the top of the upload popup).</summary>
        public string? MappingBranch { get; set; }

        [Required(ErrorMessage = "Rows are required")]
        public List<VendorUploadRow> Rows { get; set; } = new();
    }

    /// <summary>The uploaded row after validation: cleaned values, location from the pincode and the errors (if any).</summary>
    public class VendorUploadRowResult : VendorUploadRow
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new();
        public int? CountryId { get; set; }
        public int? StateId { get; set; }
        public int? DistrictId { get; set; }
        public int? CityId { get; set; }
        /// <summary>"City, District, State, Country" found for the pincode (shown in the grid), or empty.</summary>
        public string? Location { get; set; }
        public int? VendorId { get; set; }
    }

    public class VendorExcelUploadResponse
    {
        public bool Saved { get; set; }
        public int TotalRows { get; set; }
        public int ValidRows { get; set; }
        public int ErrorRows { get; set; }
        public int SavedRows { get; set; }
        public List<VendorUploadRowResult> Rows { get; set; } = new();
    }

    public class GetVendorMasterRequest
    {
        public int? VendorId { get; set; }
        public int? IsActive { get; set; }
    }

    public class CreateUpdateManufacturerMasterRequest
    {
        public int ManufacturerId { get; set; } = 0;

        [Required(ErrorMessage = "Manufacturer name is required")]
        [StringLength(256, ErrorMessage = "Manufacturer name cannot exceed 256 characters")]
        public string ManufacturerName { get; set; }

        [StringLength(20, ErrorMessage = "Short code cannot exceed 20 characters")]
        public string? ShortCode { get; set; }

        [StringLength(100, ErrorMessage = "City / Country cannot exceed 100 characters")]
        public string? CityCountry { get; set; }

        [Required(ErrorMessage = "IsActive is required")]
        public int IsActive { get; set; } = 1;
    }

    public class CreateUpdateManufacturerMasterResponse
    {
        public int ManufacturerId { get; set; }
    }

    public class CreateUpdateSaltNameMasterRequest
    {
        public int SaltNameId { get; set; } = 0;

        [Required(ErrorMessage = "Salt/Generic name is required")]
        [StringLength(256, ErrorMessage = "Salt/Generic name cannot exceed 256 characters")]
        public string SaltName { get; set; }

        [StringLength(100, ErrorMessage = "Therapeutic class cannot exceed 100 characters")]
        public string? TherapeuticClass { get; set; }

        [StringLength(20, ErrorMessage = "SNOMED-CT code cannot exceed 20 characters")]
        [RegularExpression(@"^$|^\d+$", ErrorMessage = "SNOMED-CT code must contain digits only")]
        public string? SNOMEDCode { get; set; }

        [Required(ErrorMessage = "IsActive is required")]
        public int IsActive { get; set; } = 1;
    }

    public class CreateUpdateSaltNameMasterResponse
    {
        public int SaltNameId { get; set; }
    }

    public class CreateUpdateTaxPercentageRequest
    {
        public int TaxId { get; set; } = 0;

        [Required(ErrorMessage = "Slab name is required")]
        [StringLength(50, ErrorMessage = "Slab name cannot exceed 50 characters")]
        public string Description { get; set; }

        [Required(ErrorMessage = "Rate is required")]
        [Range(typeof(decimal), "0", "100", ErrorMessage = "Rate must be between 0 and 100")]
        public decimal TaxValue { get; set; }

        [Required(ErrorMessage = "IsActive is required")]
        public int IsActive { get; set; } = 1;
    }

    public class CreateUpdateTaxPercentageResponse
    {
        public int TaxId { get; set; }
    }

    public class CreateUpdateDrugCategoryRequest
    {
        public int DrugId { get; set; } = 0;

        [Required(ErrorMessage = "Drug category name is required")]
        [StringLength(200, ErrorMessage = "Drug category name cannot exceed 200 characters")]
        public string DrugName { get; set; }

        [Required(ErrorMessage = "Is narcotic drug is required")]
        [Range(0, 1, ErrorMessage = "IsNarcoticDrug must be 0 or 1")]
        public int IsNarcoticDrug { get; set; }

        [RegularExpression(@"^$|^#[0-9A-Fa-f]{6}$", ErrorMessage = "Color must be a hex code like #1A73E8")]
        public string? ColorPicker { get; set; }

        [Required(ErrorMessage = "IsActive is required")]
        public int IsActive { get; set; } = 1;
    }

    public class CreateUpdateDrugCategoryResponse
    {
        public int DrugId { get; set; }
    }

    public class CreateUpdateRackMasterRequest
    {
        public int RackId { get; set; } = 0;

        [Required(ErrorMessage = "Rack code is required")]
        [StringLength(20, ErrorMessage = "Rack code cannot exceed 20 characters")]
        public string RackCode { get; set; }

        [Required(ErrorMessage = "Location is required")]
        [StringLength(100, ErrorMessage = "Location cannot exceed 100 characters")]
        public string Location { get; set; }

        [Required(ErrorMessage = "IsActive is required")]
        public int IsActive { get; set; } = 1;
    }

    public class CreateUpdateRackMasterResponse
    {
        public int RackId { get; set; }
    }

    // ---------------- Store Item Master lookups ----------------

    public class CreateUpdatePurchaseUnitRequest
    {
        public int PurchaseUnitId { get; set; } = 0;

        [Required(ErrorMessage = "Purchase unit name is required")]
        [StringLength(256, ErrorMessage = "Purchase unit name cannot exceed 256 characters")]
        public string PurchaseUnitName { get; set; }

        [Range(0, 1, ErrorMessage = "IsActive must be 0 or 1")]
        public int IsActive { get; set; } = 1;
    }

    public class CreateUpdateSaleUnitRequest
    {
        public int SaleUnitId { get; set; } = 0;

        [Required(ErrorMessage = "Sale unit name is required")]
        [StringLength(256, ErrorMessage = "Sale unit name cannot exceed 256 characters")]
        public string SaleUnitName { get; set; }

        [Range(0, 1, ErrorMessage = "IsActive must be 0 or 1")]
        public int IsActive { get; set; } = 1;
    }

    public class CreateUpdateDosageFormRequest
    {
        public int DosageFormId { get; set; } = 0;

        [Required(ErrorMessage = "Dosage form name is required")]
        [StringLength(100, ErrorMessage = "Dosage form name cannot exceed 100 characters")]
        public string DosageFormName { get; set; }

        [StringLength(256, ErrorMessage = "Description cannot exceed 256 characters")]
        public string? Description { get; set; }

        [Range(0, 1, ErrorMessage = "IsActive must be 0 or 1")]
        public int IsActive { get; set; } = 1;
    }

    public class LookupSaveResponse
    {
        public int Id { get; set; }
    }

    // ---------------- Store Item Master ----------------

    public class CreateUpdateStoreItemMasterRequest
    {
        public int ServiceItemId { get; set; } = 0;

        [Range(1, int.MaxValue, ErrorMessage = "Store type is required")]
        public int CategoryId { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Sub group is required")]
        public int SubCategoryId { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Sub-sub group is required")]
        public int SubSubCategoryId { get; set; }

        [Required(ErrorMessage = "Item name is required")]
        [StringLength(256, ErrorMessage = "Item name cannot exceed 256 characters")]
        public string Name { get; set; }

        [StringLength(20, ErrorMessage = "Item code cannot exceed 20 characters")]
        public string? Code { get; set; }

        public int? DescriptionId { get; set; }

        [StringLength(100, ErrorMessage = "Strength cannot exceed 100 characters")]
        public string? Strength { get; set; }

        [StringLength(20, ErrorMessage = "SNOMED-CT code cannot exceed 20 characters")]
        [RegularExpression(@"^$|^\d+$", ErrorMessage = "SNOMED-CT code must contain digits only")]
        public string? SnomedCode { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Manufacturer is required")]
        public int ManufacturerId { get; set; }

        public int? DosageFormId { get; set; }

        public int? DrugCategoryId { get; set; }

        [StringLength(50, ErrorMessage = "Barcode cannot exceed 50 characters")]
        public string? Barcode { get; set; }

        [StringLength(50, ErrorMessage = "Pack size cannot exceed 50 characters")]
        public string? PackSize { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Purchase unit is required")]
        public int PurchaseUnitId { get; set; }

        // optional (owner removed Sale Unit from the Medical Store Item Master screen)
        public int? SaleUnitId { get; set; }

        [Range(1, 99999, ErrorMessage = "Packing / conversion factor must be between 1 and 99999")]
        public int Packing { get; set; }

        [StringLength(20, ErrorMessage = "HSN code cannot exceed 20 characters")]
        public string? HsnCode { get; set; }

        [StringLength(20, ErrorMessage = "Commodity code cannot exceed 20 characters")]
        public string? CommodityCode { get; set; }

        [Range(typeof(decimal), "0", "100", ErrorMessage = "GST must be between 0 and 100")]
        public decimal? GstPer { get; set; }

        [RegularExpression(@"^$|^(OTC|G|H|H1|X|Narcotic)$", ErrorMessage = "Drug schedule must be OTC, G, H, H1, X or Narcotic")]
        public string? DrugSchedule { get; set; }

        public int? RackId { get; set; }

        [Range(0, 1, ErrorMessage = "Cold chain must be 0 or 1")]
        public int IsColdChain { get; set; } = 0;

        [Range(0, 1, ErrorMessage = "Expirable must be 0 or 1")]
        public int Expirable { get; set; } = 1;

        [RegularExpression(@"^$|^(Vital|Essential|Desirable)$", ErrorMessage = "VED type must be Vital, Essential or Desirable")]
        public string? VedType { get; set; }

        [Range(0, 999999, ErrorMessage = "Min level must be between 0 and 999999")]
        public int? MinLevel { get; set; }

        [Range(0, 999999, ErrorMessage = "Reorder level must be between 0 and 999999")]
        public int? ReorderLevel { get; set; }

        [Range(0, 999999, ErrorMessage = "Max level must be between 0 and 999999")]
        public int? MaxLevel { get; set; }

        public int? DoctorId { get; set; }

        [Range(0, 1, ErrorMessage = "Kit type must be 0 or 1")]
        public int IsKitTypeItem { get; set; } = 0;

        [Range(0, 1, ErrorMessage = "IsActive must be 0 or 1")]
        public int IsActive { get; set; } = 1;

        // ---- stock location, batch / expiry, NABH / quality, item flags ----
        public int? DefaultStoreRoleId { get; set; }

        [Range(0, 3650, ErrorMessage = "Expiry alert days must be between 0 and 3650")]
        public int? ExpiryAlertDays { get; set; }

        [Range(0, 1)] public int IsFEFOIssue { get; set; } = 1;
        [Range(0, 1)] public int IsBatchMandatory { get; set; } = 1;
        [Range(0, 1)] public int AllowExpiredIssue { get; set; } = 0;

        [RegularExpression(@"^$|^(A|B|C)$", ErrorMessage = "Class must be A, B or C")]
        public string? ItemClass { get; set; }

        [StringLength(50, ErrorMessage = "Storage condition cannot exceed 50 characters")]
        public string? StorageCondition { get; set; }

        [Range(0, 1)] public int IsSterile { get; set; } = 0;
        [Range(0, 1)] public int IsSingleUse { get; set; } = 0;
        [Range(0, 1)] public int IsRecallItem { get; set; } = 0;
        [Range(0, 1)] public int IsRestrictedItem { get; set; } = 0;
        [Range(0, 1)] public int IsTrackBatch { get; set; } = 1;
        [Range(0, 1)] public int IsBarcodeApplicable { get; set; } = 1;
        [Range(0, 1)] public int IsStockable { get; set; } = 1;
        [Range(0, 1)] public int IsCapitalItem { get; set; } = 0;
        [Range(0, 1)] public int IsHighRisk { get; set; } = 0;
    }

    public class CreateUpdateStoreItemMasterResponse
    {
        public int ServiceItemId { get; set; }
    }

    // ================= Stock Receiving (GRN) =================

    public class StockReceivingSaveRequest
    {
        public int ReceivingId { get; set; } = 0;

        [Range(1, int.MaxValue, ErrorMessage = "Branch is required")]
        public int BranchId { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Store is required")]
        public int RoleId { get; set; }

        [Required(ErrorMessage = "Store Type is required")]
        public string StoreType { get; set; } = "Medical";

        [Range(1, int.MaxValue, ErrorMessage = "Please select the Vendor first")]
        public int VendorId { get; set; }

        /// <summary>1 Invoice, 2 Challan, 3 Challan and Invoice</summary>
        [Range(1, 3, ErrorMessage = "Receiving Against must be Invoice, Challan or Challan & Invoice")]
        public int ReceivingAgainstId { get; set; } = 1;

        [MaxLength(50)] public string? InvoiceNo { get; set; }
        public DateTime? InvoiceDate { get; set; }
        [MaxLength(50)] public string? ChallanNo { get; set; }
        public DateTime? ChallanDate { get; set; }

        [Required] public string GstType { get; set; } = "CGST & SGST";
        public DateTime GrnDate { get; set; }
        [MaxLength(30)] public string? PaymentMode { get; set; }
        [MaxLength(1000)] public string? Remarks { get; set; }
        [MaxLength(1000)] public string? AmendmentRemark { get; set; }

        [Range(0, 100, ErrorMessage = "GRN Disc. % must be 0-100")]
        public decimal GrnDiscountPercent { get; set; }

        /// <summary>transport / "bhada" - stored on StockReceiving only; not part of Net Payable or any other table (owner 2026-11-16)</summary>
        [Range(typeof(decimal), "0", "9999999", ErrorMessage = "Freight Charges must be 0 - 99,99,999")]
        public decimal? FreightCharges { get; set; }

        public List<StockReceivingItemRequest> Items { get; set; } = new();
    }

    public class StockReceivingItemRequest
    {
        public int ItemId { get; set; }
        public decimal Qty { get; set; }
        public decimal FreeQty { get; set; }
        public int Packing { get; set; }
        public int Deal1 { get; set; }
        public int Deal2 { get; set; }
        [MaxLength(50)] public string? BatchNo { get; set; }
        /// <summary>yyyy-MM (month of expiry); saved as the first day of that month</summary>
        public string? Expiry { get; set; }
        [MaxLength(50)] public string? HsnCode { get; set; }
        public decimal Mrp { get; set; }
        public decimal Rate { get; set; }
        public decimal DiscPer { get; set; }
        public decimal DiscAmt { get; set; }
        public decimal SpclDiscPer { get; set; }
        public decimal GstPer { get; set; }
        /// <summary>scanned barcode (GTIN for a GS1 code); saved on the Stock lot only, never on the item master</summary>
        [MaxLength(100)] public string? Barcode { get; set; }
    }

    public class StockReceivingSaveResponse
    {
        public int ReceivingId { get; set; }
    }

    /// <summary>
    /// GRN being entered, autosaved to Redis only (never the DB) so it can be resumed after a session expiry,
    /// lost connection or closed tab. Stored per user (userId from the token).
    /// </summary>
    public class GrnUnsavedDraftSaveRequest
    {
        /// <summary>empty = new record; otherwise the id returned by the first save</summary>
        [MaxLength(40)] public string? DraftId { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Branch is required")]
        public int BranchId { get; set; }

        [MaxLength(300)] public string? VendorName { get; set; }
        [MaxLength(120)] public string? DocNo { get; set; }
        [Range(0, 10000)] public int ItemCount { get; set; }

        /// <summary>the screen state as JSON (header, lines, the line being typed)</summary>
        [Required, MaxLength(1_000_000)] public string Data { get; set; } = string.Empty;
    }

    public class GrnUnsavedDraftDeleteRequest
    {
        [Required, MaxLength(40)] public string DraftId { get; set; } = string.Empty;
    }

    public class StockReceivingActionRequest
    {
        [Range(1, int.MaxValue, ErrorMessage = "ReceivingId is required")]
        public int ReceivingId { get; set; }

        /// <summary>Cancel: cancel reason. Unpost: unpost remark (required, 2026-11-17). Post: not used.</summary>
        [MaxLength(1000)] public string? Reason { get; set; }
    }

    #region Purchase Request (2026-11-16)

    public class PurchaseRequestItemRequest
    {
        [Range(1, int.MaxValue, ErrorMessage = "ItemId is required")]
        public int ItemId { get; set; }

        /// <summary>Requested quantity in purchase units (packs), whole number</summary>
        public int Qty { get; set; }
    }

    public class PurchaseRequestSaveRequest
    {
        /// <summary>0 = new request, else the Draft request to update</summary>
        public int PurchaseRequestId { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "BranchId is required")]
        public int BranchId { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Please select From Department")]
        public int FromDeptId { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Please select department first")]
        public int ToDeptId { get; set; }

        /// <summary>Medical or General</summary>
        [Required(ErrorMessage = "Store Type is required")]
        public string StoreType { get; set; } = string.Empty;

        [MaxLength(1000)] public string? Remarks { get; set; }

        public List<PurchaseRequestItemRequest> Items { get; set; } = new();
    }

    public class PurchaseRequestActionRequest
    {
        [Range(1, int.MaxValue, ErrorMessage = "PurchaseRequestId is required")]
        public int PurchaseRequestId { get; set; }

        /// <summary>Cancel only</summary>
        [MaxLength(500)] public string? Reason { get; set; }
    }

    public class PurchaseRequestApproveItem
    {
        [Range(1, int.MaxValue)] public int PurchaseRequestItemId { get; set; }
        public int ApprovedQty { get; set; }
    }

    public class PurchaseRequestApproveRequest
    {
        [Range(1, int.MaxValue, ErrorMessage = "PurchaseRequestId is required")]
        public int PurchaseRequestId { get; set; }

        /// <summary>Approval level the user is approving (FlagId from the list)</summary>
        [Range(1, 4, ErrorMessage = "Flag must be 1-4")]
        public int Flag { get; set; }

        [MaxLength(500)] public string? ApprovalRemarks { get; set; }

        /// <summary>Approved quantity per line (0..requested). Lines left out keep their current approved quantity.</summary>
        public List<PurchaseRequestApproveItem> Items { get; set; } = new();
    }

    #endregion

    #region Purchase Order (2026-11-16)

    /// <summary>one PO line; the vendor is per line (OLD Map Vendor) - lines are saved as one PO per vendor</summary>
    public class PurchaseOrderItemRequest
    {
        [Range(1, int.MaxValue, ErrorMessage = "ItemId is required")]
        public int ItemId { get; set; }
        public int VendorId { get; set; }
        public decimal Qty { get; set; }
        public decimal FreeQty { get; set; }
        public int Packing { get; set; }
        public int Deal1 { get; set; }
        public int Deal2 { get; set; }
        [MaxLength(50)] public string? HsnCode { get; set; }
        public decimal Mrp { get; set; }
        public decimal Rate { get; set; }
        public decimal DiscPer { get; set; }
        public decimal DiscAmt { get; set; }
        public decimal SpclDiscPer { get; set; }
        public decimal GstPer { get; set; }
    }

    public class PurchaseOrderSaveRequest
    {
        /// <summary>0 = new (split into one PO per vendor), else the PO to update (all lines must then be that one vendor)</summary>
        public int PoId { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "BranchId is required")]
        public int BranchId { get; set; }

        /// <summary>From Department (user's role) - picks the approval rule</summary>
        [Range(1, int.MaxValue, ErrorMessage = "Please select department first")]
        public int RoleId { get; set; }

        /// <summary>Medical or General</summary>
        [Required(ErrorMessage = "Store is required")] public string StoreType { get; set; } = string.Empty;

        /// <summary>No-Service / Service</summary>
        [Required, MaxLength(50)] public string PoType { get; set; } = "No-Service";

        /// <summary>1 PO By Item, 2 PO By Supplier, 3 PO By PR</summary>
        [Range(1, 3, ErrorMessage = "Type must be PO By Item, PO By Supplier or PO By PR")]
        public int TypeId { get; set; }

        /// <summary>PO-level discount %, used only when no line has its own discount (GRN rule)</summary>
        [Range(0, 100, ErrorMessage = "Discount % must be 0-100")]
        public decimal DiscountPercent { get; set; }

        [Required, MaxLength(50)] public string Priority { get; set; } = "Normal";
        public DateTime PoDate { get; set; }
        public DateTime? DeliveryDate { get; set; }
        public DateTime? ValidDate { get; set; }
        [MaxLength(500)] public string? Remarks { get; set; }
        [MaxLength(500)] public string? DeliveryAddress { get; set; }
        [MaxLength(512)] public string? DocumentPath { get; set; }
        [MaxLength(1000)] public string? OtherTermCondition { get; set; }
        public List<int> TermIds { get; set; } = new();
        /// <summary>PO By PR: the approved requests the lines came from</summary>
        public List<int> PurchaseRequestIds { get; set; } = new();
        /// <summary>true = also send every saved PO for approval (Save &amp; Send)</summary>
        public bool SendForApproval { get; set; }

        public List<PurchaseOrderItemRequest> Items { get; set; } = new();
    }

    public class PurchaseOrderActionRequest
    {
        [Range(1, int.MaxValue, ErrorMessage = "PoId is required")]
        public int PoId { get; set; }

        /// <summary>Cancel only</summary>
        [MaxLength(500)] public string? Reason { get; set; }
    }

    public class PurchaseOrderApproveRequest
    {
        [Range(1, int.MaxValue, ErrorMessage = "PoId is required")]
        public int PoId { get; set; }

        [Range(1, 4, ErrorMessage = "Flag must be 1-4")]
        public int Flag { get; set; }

        [MaxLength(500)] public string? ApprovalRemarks { get; set; }
    }

    public class PurchaseOrderDocumentRequest
    {
        [Required(ErrorMessage = "Document file is required")]
        public IFormFile? UploadFile { get; set; }
    }

    #endregion
}