using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Reflection;
using HISWEBAPI.Data.Helpers;
using HISWEBAPI.DTO;
using HISWEBAPI.Exceptions;
using HISWEBAPI.Models;
using Microsoft.Data.SqlClient;

namespace HISWEBAPI.Repositories.Implementations
{
    /// <summary>
    /// Stock Receiving (GRN) - direct receiving from a vendor (2026-11-14).
    /// Rules ported from OLD StockReceivingNew / Store.saveStockReceivingNew; screen and status flow from the prototype.
    /// Every amount is recomputed here from the line inputs (same formulas as the screen); the client totals are not trusted.
    /// SPs: IU_StockReceiving, U_Post/U_UnPost/U_CancelStockReceiving, S_GetStockReceivingList/Details,
    ///      S_SearchStoreItemForReceiving, S_GetStockReceivingStores (script Database/2026-11-14_StockReceiving.sql).
    /// </summary>
    public partial class StoreRepository
    {
        private static readonly Dictionary<string, int> StoreTypeCategory = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Medical"] = 6,
            ["General"] = 7,
            ["Blood Bank Inventory"] = 15
        };

        private const int MaxGrnLines = 500;

        #region Reads

        public ServiceResult<IEnumerable<ReceivingStoreModel>> GetStockReceivingStores(int branchId, AllGlobalValues globalValues)
        {
            try
            {
                var dt = _sqlHelper.GetDataTable("S_GetStockReceivingStores", CommandType.StoredProcedure,
                    new { hospId = globalValues.hospId, branchId, userId = globalValues.userId });
                var list = dt?.AsEnumerable().Select(r => new ReceivingStoreModel
                {
                    RoleId = r.Field<int>("RoleId"),
                    RoleName = r.Field<string>("RoleName") ?? string.Empty
                }).ToList() ?? new List<ReceivingStoreModel>();

                return ServiceResult<IEnumerable<ReceivingStoreModel>>.Success(list, "Info", $"{list.Count} store(s) retrieved successfully", 200);
            }
            catch (Exception ex)
            {
                return ServerError<IEnumerable<ReceivingStoreModel>>(ex);
            }
        }

        public ServiceResult<IEnumerable<ReceivingItemSearchModel>> SearchReceivingItem(string storeType, string? search, AllGlobalValues globalValues)
        {
            try
            {
                var list = LoadReceivingItems(StoreTypeCategory[storeType], search, null, globalValues.hospId, 30);
                return ServiceResult<IEnumerable<ReceivingItemSearchModel>>.Success(list, "Info", $"{list.Count} item(s) found", 200);
            }
            catch (Exception ex)
            {
                return ServerError<IEnumerable<ReceivingItemSearchModel>>(ex);
            }
        }

        private List<ReceivingItemSearchModel> LoadReceivingItems(int categoryId, string? search, string? itemIds, int hospId, int top)
        {
            var dt = _sqlHelper.GetDataTable("S_SearchStoreItemForReceiving", CommandType.StoredProcedure, new
            {
                hospId,
                categoryId,
                search = string.IsNullOrWhiteSpace(search) ? null : search.Trim(),
                top,
                itemIds
            });
            return dt?.AsEnumerable().Select(r => MapReceivingItem(r, new ReceivingItemSearchModel())).ToList()
                ?? new List<ReceivingItemSearchModel>();
        }

        private static T MapReceivingItem<T>(DataRow r, T m) where T : ReceivingItemSearchModel
        {
            m.ItemId = r.Field<int>("ItemId");
            m.ItemName = r.Field<string>("ItemName") ?? string.Empty;
            m.ItemCode = r.Field<string>("ItemCode") ?? string.Empty;
            m.SubSubCategoryId = r.Field<int?>("SubSubCategoryId");
            m.Packing = r.Field<int>("Packing");
            m.HsnCode = (r.Field<string>("HsnCode") ?? string.Empty).Trim();
            m.GstPer = r.Field<decimal?>("GstPer");
            m.Expirable = r.Field<int>("Expirable");
            m.PurchaseUnitName = r.Field<string>("PurchaseUnitName") ?? string.Empty;
            m.SaleUnitName = r.Field<string>("SaleUnitName") ?? string.Empty;
            m.ManufacturerName = r.Field<string>("ManufacturerName") ?? string.Empty;
            m.HoldStatus = r.Field<string>("HoldStatus") ?? string.Empty;
            m.HoldRemarks = r.Field<string>("HoldRemarks") ?? string.Empty;
            m.LastRate = r.Field<decimal?>("LastRate");
            m.LastMrp = r.Field<decimal?>("LastMrp");
            return m;
        }

        /// <summary>
        /// Scanned barcode -> items (S_GetItemByBarcode): earlier GRN lots first, then ServiceItemMaster.Barcode (read only).
        /// An empty list is a normal answer (barcode not known yet) - the screen then lets the user pick the item by name.
        /// </summary>
        public ServiceResult<IEnumerable<BarcodeItemModel>> GetItemByBarcode(string storeType, string barcode, AllGlobalValues globalValues)
        {
            try
            {
                var dt = _sqlHelper.GetDataTable("S_GetItemByBarcode", CommandType.StoredProcedure, new
                {
                    hospId = globalValues.hospId,
                    categoryId = StoreTypeCategory[storeType],
                    barcode = barcode.Trim()
                });
                var list = dt?.AsEnumerable().Select(r =>
                {
                    var m = MapReceivingItem(r, new BarcodeItemModel());
                    m.Source = r.Field<string>("Source") ?? string.Empty;
                    m.LastBarcodeOn = r.Field<DateTime?>("LastBarcodeOn");
                    return m;
                }).ToList() ?? new List<BarcodeItemModel>();

                return ServiceResult<IEnumerable<BarcodeItemModel>>.Success(list, "Info",
                    list.Count == 0 ? $"Barcode {barcode.Trim()} not found" : $"{list.Count} item(s) found for the barcode", 200);
            }
            catch (Exception ex)
            {
                return ServerError<IEnumerable<BarcodeItemModel>>(ex);
            }
        }

        public ServiceResult<IEnumerable<StockReceivingListModel>> GetStockReceivingList(
            int branchId, DateTime? fromDate, DateTime? toDate, int? vendorId, int? statusId, string? search, AllGlobalValues globalValues)
        {
            try
            {
                var dt = _sqlHelper.GetDataTable("S_GetStockReceivingList", CommandType.StoredProcedure, new
                {
                    hospId = globalValues.hospId,
                    branchId,
                    fromDate = fromDate?.Date,
                    toDate = toDate?.Date,
                    vendorId,
                    statusId,
                    search = string.IsNullOrWhiteSpace(search) ? null : search.Trim()
                });
                var list = dt?.AsEnumerable().Select(r => new StockReceivingListModel
                {
                    ReceivingId = r.Field<int>("ReceivingId"),
                    ReceivingNo = r.Field<string>("ReceivingNo") ?? string.Empty,
                    GrnDate = r.Field<DateTime>("GRNDate"),
                    StoreType = r.Field<string>("StoreType") ?? string.Empty,
                    RoleId = r.Field<int>("RoleId"),
                    StoreName = r.Field<string>("StoreName") ?? string.Empty,
                    VendorId = r.Field<int>("VendorId"),
                    VendorName = r.Field<string>("VendorName") ?? string.Empty,
                    InvoiceNo = r.Field<string>("InvoiceNo") ?? string.Empty,
                    InvoiceDate = r.Field<DateTime?>("InvoiceDate"),
                    ChallanNo = r.Field<string>("ChallanNo") ?? string.Empty,
                    ChallanDate = r.Field<DateTime?>("ChallanDate"),
                    NetAmount = r.Field<decimal>("NetAmount"),
                    StatusId = r.Field<int>("StatusId"),
                    Status = r.Field<string>("Status") ?? string.Empty,
                    ItemCount = r.Field<int>("ItemCount"),
                    CreatedByName = (r.Field<string>("CreatedByName") ?? string.Empty).Trim(),
                    CreatedOn = r.Field<DateTime?>("CreatedOn")
                }).ToList() ?? new List<StockReceivingListModel>();

                return ServiceResult<IEnumerable<StockReceivingListModel>>.Success(list, "Info", $"{list.Count} GRN(s) retrieved successfully", 200);
            }
            catch (Exception ex)
            {
                return ServerError<IEnumerable<StockReceivingListModel>>(ex);
            }
        }

        public ServiceResult<StockReceivingDetailsModel> GetStockReceivingDetails(int receivingId, AllGlobalValues globalValues)
        {
            try
            {
                DataSet ds = _sqlHelper.GetDataSet("S_GetStockReceivingDetails", CommandType.StoredProcedure,
                    new { hospId = globalValues.hospId, receivingId });
                if (ds == null || ds.Tables.Count == 0 || ds.Tables[0].Rows.Count == 0)
                {
                    var nf = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    return ServiceResult<StockReceivingDetailsModel>.Failure(nf.Type, "GRN not found", 404);
                }

                DataRow h = ds.Tables[0].Rows[0];
                var model = new StockReceivingDetailsModel
                {
                    ReceivingId = h.Field<int>("ReceivingId"),
                    BranchId = h.Field<int>("BranchId"),
                    RoleId = h.Field<int>("RoleId"),
                    StoreName = h.Field<string>("StoreName") ?? string.Empty,
                    StoreType = h.Field<string>("StoreType") ?? string.Empty,
                    VendorId = h.Field<int>("VendorId"),
                    VendorName = h.Field<string>("VendorName") ?? string.Empty,
                    GstinNo = h.Field<string>("GSTINNo") ?? string.Empty,
                    DlNo = h.Field<string>("DLNO") ?? string.Empty,
                    TypeId = h.Field<int>("TypeId"),
                    Type = h.Field<string>("Type") ?? string.Empty,
                    ReceivingNo = h.Field<string>("ReceivingNo") ?? string.Empty,
                    ReceivingAgainstId = h.Field<int>("ReceivingAgainstId"),
                    InvoiceNo = h.Field<string>("InvoiceNo") ?? string.Empty,
                    InvoiceDate = h.Field<DateTime?>("InvoiceDate"),
                    ChallanNo = h.Field<string>("ChallanNo") ?? string.Empty,
                    ChallanDate = h.Field<DateTime?>("ChallanDate"),
                    GstType = h.Field<string>("GSTType") ?? string.Empty,
                    GrnDate = h.Field<DateTime>("GRNDate"),
                    PaymentMode = h.Field<string>("PaymentMode") ?? string.Empty,
                    Remarks = h.Field<string>("Remarks") ?? string.Empty,
                    AmendmentRemark = h.Field<string>("AmendmentRemark") ?? string.Empty,
                    FreightCharges = h.Field<decimal>("FreightCharges"),
                    GrossAmount = h.Field<decimal>("GrossAmount"),
                    DiscountPercentage = h.Field<decimal>("DiscountPercentage"),
                    DiscountAmount = h.Field<decimal>("DiscountAmount"),
                    TaxableAmount = h.Field<decimal>("TaxableAmount"),
                    GstAmount = h.Field<decimal>("GSTAmount"),
                    NetAmount = h.Field<decimal>("NetAmount"),
                    RoundOff = h.Field<decimal>("RoundOff"),
                    StatusId = h.Field<int>("StatusId"),
                    Status = h.Field<string>("Status") ?? string.Empty,
                    CancelReason = h.Field<string>("CancelReason") ?? string.Empty,
                    CancelledOn = h.Field<DateTime?>("CancelledOn"),
                    PostedOn = h.Field<DateTime?>("PostedOn"),
                    IsIssued = h.Field<int>("IsIssued") == 1
                };

                if (ds.Tables.Count > 1)
                {
                    model.Items = ds.Tables[1].AsEnumerable().Select(r => new StockReceivingLineModel
                    {
                        StockId = r.Field<int>("StockId"),
                        ItemId = r.Field<int>("ItemId"),
                        ItemName = r.Field<string>("ItemName") ?? string.Empty,
                        ItemCode = r.Field<string>("ItemCode") ?? string.Empty,
                        Expirable = r.Field<int>("Expirable"),
                        Qty = r.Field<decimal>("Qty"),
                        FreeQty = r.Field<decimal>("FreeQty"),
                        Packing = r.Field<int>("Packing"),
                        Deal1 = r.Field<int>("Deal1"),
                        Deal2 = r.Field<int>("Deal2"),
                        BatchNo = r.Field<string>("BatchNo") ?? string.Empty,
                        Expiry = r.Field<DateTime?>("ExpiryDate")?.ToString("yyyy-MM") ?? string.Empty,
                        HsnCode = r.Field<string>("HSNCode") ?? string.Empty,
                        Mrp = r.Field<decimal>("Mrp"),
                        Rate = r.Field<decimal>("Rate"),
                        OldRate = r.Field<decimal?>("OldRate"),
                        DiscPer = r.Field<decimal>("DiscPer"),
                        DiscAmt = r.Field<decimal>("DiscAmt"),
                        SpclDiscPer = r.Field<decimal>("SpclDiscPer"),
                        SpclDiscAmt = r.Field<decimal>("SpclDiscAmt"),
                        GstPer = r.Field<decimal>("GstPer"),
                        GstAmt = r.Field<decimal>("GstAmt"),
                        GrossAmt = r.Field<decimal>("GrossAmt"),
                        NetAmt = r.Field<decimal>("NetAmt"),
                        Units = r.Field<int>("Units"),
                        UnitCost = r.Field<decimal>("UnitCost"),
                        IssuedQty = r.Field<int>("IssuedQTY"),
                        Barcode = r.Field<string>("Barcode") ?? string.Empty
                    }).ToList();
                }

                return ServiceResult<StockReceivingDetailsModel>.Success(model, "Info", "GRN retrieved successfully", 200);
            }
            catch (Exception ex)
            {
                return ServerError<StockReceivingDetailsModel>(ex);
            }
        }

        /// <summary>OLD "Last Purchase History" popup (S_getLastItemPurchaseHistory) - earlier receivings of one item, newest invoice first.</summary>
        public ServiceResult<IEnumerable<ItemPurchaseHistoryModel>> GetItemPurchaseHistory(int itemId, int? excludeReceivingId, AllGlobalValues globalValues)
        {
            try
            {
                var dt = _sqlHelper.GetDataTable("S_GetItemPurchaseHistory", CommandType.StoredProcedure, new
                {
                    hospId = globalValues.hospId,
                    itemId,
                    excludeReceivingId = excludeReceivingId > 0 ? excludeReceivingId : null,
                    top = 100
                });
                var list = dt?.AsEnumerable().Select(r => new ItemPurchaseHistoryModel
                {
                    ReceivingId = r.Field<int>("ReceivingId"),
                    ReceivingNo = r.Field<string>("ReceivingNo") ?? string.Empty,
                    GrnDate = r.Field<DateTime>("GRNDate"),
                    InvoiceDate = r.Field<DateTime?>("InvoiceDate"),
                    InvoiceNo = r.Field<string>("InvoiceNo") ?? string.Empty,
                    ChallanNo = r.Field<string>("ChallanNo") ?? string.Empty,
                    StatusId = r.Field<int>("StatusId"),
                    Status = r.Field<string>("Status") ?? string.Empty,
                    StoreType = r.Field<string>("StoreType") ?? string.Empty,
                    VendorName = r.Field<string>("VendorName") ?? string.Empty,
                    ItemId = r.Field<int>("ItemId"),
                    ItemName = r.Field<string>("ItemName") ?? string.Empty,
                    Packing = r.Field<int?>("Packing") ?? 0,
                    FreeQty = r.Field<decimal>("FreeQty"),
                    BatchNo = r.Field<string>("BatchNo") ?? string.Empty,
                    ExpiryDate = r.Field<DateTime?>("ExpiryDate"),
                    Qty = r.Field<decimal?>("Qty") ?? 0,
                    Mrp = r.Field<decimal?>("MRP") ?? 0,
                    MajorMrp = r.Field<decimal?>("MajorMRP") ?? 0,
                    Rate = r.Field<decimal?>("Rate") ?? 0,
                    UnitCost = r.Field<decimal?>("UnitCost") ?? 0,
                    GstPer = r.Field<decimal?>("GstPer") ?? 0,
                    DiscAmt = r.Field<decimal>("DiscAmt"),
                    DiscPer = r.Field<decimal>("DiscPer"),
                    Barcode = r.Field<string>("Barcode") ?? string.Empty
                }).ToList() ?? new List<ItemPurchaseHistoryModel>();

                return ServiceResult<IEnumerable<ItemPurchaseHistoryModel>>.Success(list, "Info",
                    list.Count == 0 ? "No previous purchase found for this item" : $"{list.Count} previous purchase(s) found", 200);
            }
            catch (Exception ex)
            {
                return ServerError<IEnumerable<ItemPurchaseHistoryModel>>(ex);
            }
        }

        #endregion

        #region Save

        /// <summary>Line amounts - OLD $updateRowAmounts (deal, disc % or amount, special disc, GST added on taxable).</summary>
        private sealed class GrnLineCalc
        {
            public decimal DiscPer, DiscAmt, SpclDiscAmt, Gross, Taxable, Gst, Net, UnitCost;
            public int Units;
        }

        private static GrnLineCalc CalcGrnLine(StockReceivingItemRequest it, decimal grnDiscPer)
        {
            decimal eff = it.Deal1 > 0 && it.Deal2 > 0 ? it.Deal1 * it.Qty / (it.Deal1 + it.Deal2) : it.Qty;
            decimal gross = Math.Round(it.Rate * eff, 2, MidpointRounding.AwayFromZero);
            decimal discPer = it.DiscPer, disc;
            if (it.DiscPer > 0) disc = gross * it.DiscPer / 100m;
            else if (it.DiscAmt > 0) disc = it.DiscAmt;
            else { discPer = grnDiscPer; disc = gross * grnDiscPer / 100m; }   // GRN discount only when no line has its own
            disc = Math.Round(disc, 2, MidpointRounding.AwayFromZero);
            decimal spcl = Math.Round((gross - disc) * it.SpclDiscPer / 100m, 2, MidpointRounding.AwayFromZero);
            decimal taxable = gross - disc - spcl;
            decimal gst = Math.Round(taxable * it.GstPer / 100m, 2, MidpointRounding.AwayFromZero);
            decimal net = taxable + gst;
            int units = (int)((it.Qty + it.FreeQty) * it.Packing);
            return new GrnLineCalc
            {
                DiscPer = discPer, DiscAmt = disc, SpclDiscAmt = spcl, Gross = gross, Taxable = taxable, Gst = gst, Net = net,
                Units = units, UnitCost = units > 0 ? Math.Round(net / units, 6, MidpointRounding.AwayFromZero) : 0
            };
        }

        public ServiceResult<StockReceivingSaveResponse> SaveStockReceiving(StockReceivingSaveRequest request, AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"SaveStockReceiving called. ReceivingId={request.ReceivingId}, VendorId={request.VendorId}, Lines={request.Items?.Count ?? 0}");

                string storeType = StoreTypeCategory.Keys.FirstOrDefault(k => k.Equals(request.StoreType?.Trim(), StringComparison.OrdinalIgnoreCase)) ?? string.Empty;
                bool medical = storeType == "Medical";
                bool onInvoice = request.ReceivingAgainstId != 2, onChallan = request.ReceivingAgainstId != 1;
                string invoiceNo = (request.InvoiceNo ?? string.Empty).Trim();
                string challanNo = (request.ChallanNo ?? string.Empty).Trim();
                var items = request.Items ?? new List<StockReceivingItemRequest>();

                // ---- header (OLD messages) ----
                string? err =
                    storeType == "" ? "Store Type must be Medical, General or Blood Bank Inventory"
                    : request.GstType != "CGST & SGST" && request.GstType != "IGST" ? "GST Type must be CGST & SGST or IGST"
                    : onInvoice && invoiceNo == "" ? "Please Enter Invoice No."
                    : onInvoice && request.InvoiceDate == null ? "Please Select Invoice Date"
                    : onChallan && challanNo == "" ? "Please Enter Challan No."
                    : onChallan && request.ChallanDate == null ? "Please Select Challan Date"
                    : request.GrnDate == default ? "Please Select GRN Date"
                    : (onInvoice && request.InvoiceDate!.Value.Date > DateTime.Today) || (onChallan && request.ChallanDate!.Value.Date > DateTime.Today)
                        ? "Invoice / Challan Date cannot be a future date"
                    : request.GrnDate.Date > DateTime.Today ? "GRN Date cannot be a future date"
                    : request.FreightCharges.HasValue && decimal.Round(request.FreightCharges.Value, 2) != request.FreightCharges.Value
                        ? "Freight Charges can have at most 2 decimal places"
                    : items.Count == 0 ? "Please add at least one item"
                    : items.Count > MaxGrnLines ? $"A GRN can have at most {MaxGrnLines} items"
                    : request.ReceivingId > 0 && string.IsNullOrWhiteSpace(request.AmendmentRemark) ? "Please fill Amendment Remarks before updating."
                    : null;
                if (err != null) return Invalid<StockReceivingSaveResponse>(err);

                // ---- items exist, belong to the store type, are active ----
                string ids = string.Join(",", items.Select(i => i.ItemId).Distinct());
                var master = LoadReceivingItems(StoreTypeCategory[storeType], null, ids, globalValues.hospId, MaxGrnLines)
                    .ToDictionary(m => m.ItemId);

                string thisMonth = DateTime.Today.ToString("yyyy-MM");
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var lines = new List<object>();
                bool lineDiscount = items.Any(i => i.DiscPer > 0 || i.DiscAmt > 0);
                decimal grnDiscPer = lineDiscount ? 0 : request.GrnDiscountPercent;
                decimal sumGross = 0, sumDisc = 0, sumTaxable = 0, sumGst = 0, sumNet = 0;

                for (int n = 0; n < items.Count; n++)
                {
                    var it = items[n];
                    string row = $"Row {n + 1}: ";
                    it.BatchNo = (it.BatchNo ?? string.Empty).Trim().ToUpperInvariant();
                    it.Expiry = (it.Expiry ?? string.Empty).Trim();
                    it.Barcode = string.IsNullOrWhiteSpace(it.Barcode) ? null : it.Barcode.Trim();

                    if (!master.TryGetValue(it.ItemId, out var m))
                        return Invalid<StockReceivingSaveResponse>(row + $"Item {it.ItemId} is not an active {storeType} store item");

                    DateTime? expiry = null;
                    if (it.Expiry != "")
                    {
                        if (!DateTime.TryParseExact(it.Expiry, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var ex))
                            return Invalid<StockReceivingSaveResponse>(row + $"Invalid Expiry Date for Item : {m.ItemName}");
                        expiry = ex;
                    }

                    var c = CalcGrnLine(it, grnDiscPer);
                    err =
                        it.Qty < 1 || it.Qty != Math.Floor(it.Qty) ? "Please Enter a valid QTY"
                        : it.FreeQty < 0 || it.FreeQty != Math.Floor(it.FreeQty) ? "Please Enter a valid Free QTY"
                        : it.Packing < 1 ? "Please Enter a valid Packing"
                        : it.Deal1 < 0 || it.Deal2 < 0 || (it.Deal1 > 0) != (it.Deal2 > 0) ? "Enter both Deal Buy and Deal Free, or neither"
                        : medical && it.BatchNo == "" ? "Please Enter Batch Number"
                        : m.Expirable == 1 && expiry == null ? "Please Enter Expiry"
                        : expiry != null && it.Expiry.CompareTo(thisMonth) < 0 ? "Expiry Date Cannot be less than Current Date"
                        : it.Mrp <= 0 ? "Please Enter a valid MRP"
                        : it.Rate <= 0 ? "Please Enter a valid Rate"
                        : medical && it.Rate > it.Mrp ? "Rate cannot be greater than MRP"
                        : it.DiscPer < 0 || it.DiscPer > 100 || it.SpclDiscPer < 0 || it.SpclDiscPer > 100 ? "Discount % must be 0-100"
                        : it.DiscPer > 0 && it.DiscAmt > 0 ? "Enter either Disc % or Disc Amt, not both"
                        : it.DiscAmt < 0 || it.DiscAmt > c.Gross ? "Discount Amt cannot be greater than Gross Amt"
                        : it.GstPer < 0 || it.GstPer > 100 ? "Please Enter a valid GST %"
                        : !seen.Add($"{it.ItemId}|{it.BatchNo}") ? "This item and batch are already in this GRN"
                        : null;
                    if (err != null) return Invalid<StockReceivingSaveResponse>(row + err + $" ({m.ItemName})");

                    // non-expirable items: today + 5 years (OLD rule)
                    expiry ??= DateTime.Today.AddYears(5);

                    sumGross += c.Gross; sumDisc += c.DiscAmt + c.SpclDiscAmt; sumTaxable += c.Taxable; sumGst += c.Gst; sumNet += c.Net;
                    lines.Add(new
                    {
                        itemId = it.ItemId,
                        itemName = m.ItemName,
                        subSubCategoryId = m.SubSubCategoryId,
                        qty = it.Qty,
                        freeQty = it.FreeQty,
                        packing = it.Packing,
                        deal1 = it.Deal1,
                        deal2 = it.Deal2,
                        batchNo = it.BatchNo,
                        expiryDate = expiry.Value.ToString("yyyy-MM-dd"),
                        hsnCode = string.IsNullOrWhiteSpace(it.HsnCode) ? m.HsnCode : it.HsnCode.Trim(),
                        mrp = it.Mrp,
                        rate = it.Rate,
                        discPer = c.DiscPer,
                        discAmt = c.DiscAmt,
                        spclDiscPer = it.SpclDiscPer,
                        spclDiscAmt = c.SpclDiscAmt,
                        gstPer = it.GstPer,
                        gstAmt = c.Gst,
                        grossAmt = c.Gross,
                        netAmt = c.Net,
                        units = c.Units,
                        unitCost = c.UnitCost,
                        barcode = it.Barcode
                    });
                }

                decimal netPayable = Math.Round(sumNet, 0, MidpointRounding.AwayFromZero);
                if (netPayable == 0) return Invalid<StockReceivingSaveResponse>("Net Bill Amount cannot be 0");

                int id;
                using (var con = new SqlConnection(_configuration.GetConnectionString("ConnectionString")))
                {
                    con.Open();
                    using (SqlTransaction tnx = CustomSqlHelper.getSqlTransaction(con))
                    {
                        object raw = _sqlHelper.DML(tnx, "IU_StockReceiving", CommandType.StoredProcedure, new
                        {
                            hospId = globalValues.hospId,
                            branchId = request.BranchId,
                            roleId = request.RoleId,
                            receivingId = request.ReceivingId,
                            storeType,
                            vendorId = request.VendorId,
                            receivingAgainstId = request.ReceivingAgainstId,
                            invoiceNo = onInvoice ? invoiceNo : null,
                            invoiceDate = onInvoice ? request.InvoiceDate?.Date : null,
                            challanNo = onChallan ? challanNo : null,
                            challanDate = onChallan ? request.ChallanDate?.Date : null,
                            gstType = request.GstType,
                            grnDate = request.GrnDate.Date,
                            paymentMode = string.IsNullOrWhiteSpace(request.PaymentMode) ? null : request.PaymentMode.Trim(),
                            remarks = string.IsNullOrWhiteSpace(request.Remarks) ? null : request.Remarks.Trim(),
                            amendmentRemark = request.ReceivingId > 0 ? request.AmendmentRemark!.Trim() : null,
                            // stored on the header only - deliberately NOT included in any amount below (owner 2026-11-16)
                            freightCharges = request.FreightCharges > 0 ? request.FreightCharges : null,
                            grossAmount = sumGross,
                            discountPercentage = grnDiscPer,
                            discountAmount = sumDisc,
                            taxableAmount = sumTaxable,
                            gstAmount = sumGst,
                            netAmount = netPayable,
                            roundOff = netPayable - sumNet,
                            itemsJson = System.Text.Json.JsonSerializer.Serialize(lines),
                            userId = globalValues.userId,
                            ipAddress = globalValues.ipAddress
                        }, new { Result = 0 });

                        id = raw == null || raw == DBNull.Value ? 0 : Convert.ToInt32(raw);
                        if (id <= 0)
                        {
                            tnx.Rollback();
                            string msg = id switch
                            {
                                -1 => onInvoice ? "Invoice No. already exists for same Vendor" : "Challan No. already exists for same Vendor",
                                -2 => "Only a Draft GRN can be edited - Unpost it first",
                                -3 => $"No GRN number sequence is set up for {storeType} receiving - add it in Sequence Master",
                                -4 => "Vendor not found or inactive",
                                _ => "GRN could not be saved"
                            };
                            var alert = _messageService.GetMessageAndTypeByAlertCode(id == -1 ? "RECORD_ALREADY_EXISTS" : "OPERATION_FAILED");
                            return ServiceResult<StockReceivingSaveResponse>.Failure(alert.Type, msg, 200);
                        }
                        tnx.Commit();
                    }
                }

                _log.Info($"SaveStockReceiving saved GRN {id} ({items.Count} line(s), net {netPayable})");
                var ok = _messageService.GetMessageAndTypeByAlertCode(request.ReceivingId > 0 ? "DATA_UPDATED_SUCCESSFULLY" : "DATA_SAVED_SUCCESSFULLY");
                return ServiceResult<StockReceivingSaveResponse>.Success(new StockReceivingSaveResponse { ReceivingId = id }, ok.Type,
                    request.ReceivingId > 0 ? "GRN updated as Draft" : "GRN saved as Draft. Post it to add the stock.", 200);
            }
            catch (Exception ex)
            {
                return ServerError<StockReceivingSaveResponse>(ex);
            }
        }

        /// <summary>action = post | unpost | cancel</summary>
        public ServiceResult<StockReceivingSaveResponse> ChangeStockReceivingStatus(string action, StockReceivingActionRequest request, AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"ChangeStockReceivingStatus called. Action={action}, ReceivingId={request.ReceivingId}");
                if (action == "cancel" && string.IsNullOrWhiteSpace(request.Reason))
                    return Invalid<StockReceivingSaveResponse>("Cancel reason is required");

                string sp = action switch { "post" => "U_PostStockReceiving", "unpost" => "U_UnPostStockReceiving", _ => "U_CancelStockReceiving" };
                object parameters = action == "cancel"
                    ? new { hospId = globalValues.hospId, receivingId = request.ReceivingId, reason = request.Reason!.Trim(), userId = globalValues.userId, ipAddress = globalValues.ipAddress }
                    : new { hospId = globalValues.hospId, receivingId = request.ReceivingId, userId = globalValues.userId, ipAddress = globalValues.ipAddress };

                int result;
                using (var con = new SqlConnection(_configuration.GetConnectionString("ConnectionString")))
                {
                    con.Open();
                    using (SqlTransaction tnx = CustomSqlHelper.getSqlTransaction(con))
                    {
                        object raw = _sqlHelper.DML(tnx, sp, CommandType.StoredProcedure, parameters, new { Result = 0 });
                        result = raw == null || raw == DBNull.Value ? 0 : Convert.ToInt32(raw);
                        if (result > 0) tnx.Commit(); else tnx.Rollback();
                    }
                }

                if (result <= 0)
                {
                    string msg = result == -1 ? "Cannot unpost - stock from this GRN has already been issued"
                        : action == "post" ? "Only a Draft GRN can be posted"
                        : action == "unpost" ? "Only a Posted GRN can be unposted"
                        : "Only a Draft GRN can be cancelled - Unpost it first";
                    var alert = _messageService.GetMessageAndTypeByAlertCode("OPERATION_FAILED");
                    return ServiceResult<StockReceivingSaveResponse>.Failure(alert.Type, msg, 200);
                }

                var ok = _messageService.GetMessageAndTypeByAlertCode("DATA_UPDATED_SUCCESSFULLY");
                string done = action switch
                {
                    "post" => "GRN posted - stock added and locked",
                    "unpost" => "GRN unposted - stock taken back. You can now Edit or Cancel it.",
                    _ => "GRN cancelled"
                };
                return ServiceResult<StockReceivingSaveResponse>.Success(new StockReceivingSaveResponse { ReceivingId = request.ReceivingId }, ok.Type, done, 200);
            }
            catch (Exception ex)
            {
                return ServerError<StockReceivingSaveResponse>(ex);
            }
        }

        #endregion

        private ServiceResult<T> Invalid<T>(string message)
        {
            var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
            return ServiceResult<T>.Failure(alert.Type, message, 200);
        }

        private ServiceResult<T> ServerError<T>(Exception ex, [System.Runtime.CompilerServices.CallerMemberName] string method = "")
        {
            LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{method}");
            var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
            return ServiceResult<T>.Failure(alert.Type, alert.Message, 500);
        }
    }
}
