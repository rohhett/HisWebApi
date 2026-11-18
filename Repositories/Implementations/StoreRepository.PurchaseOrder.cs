using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using HISWEBAPI.Data.Helpers;
using HISWEBAPI.DTO;
using HISWEBAPI.Models;
using Microsoft.Data.SqlClient;

namespace HISWEBAPI.Repositories.Implementations
{
    /// <summary>
    /// Purchase Order + Purchase Order Approval (2026-11-16).
    /// Ported from OLD Store/PurchaseOrder, Store/PurchaseOrderApproval (Store.saveStockReceivingNewByPO,
    /// PostStockReceivingByPO, CancelStockReceivingByPO). OLD screen saves one PO per vendor - kept here: the lines are
    /// grouped by vendor and saved in one transaction. Amounts are recalculated with the GRN arithmetic (CalcGrnLine).
    /// Approval: ApprovalAuthorityMaster type 3, chosen by PO net amount. SPs: Database/2026-11-16_PurchaseOrder.sql.
    /// </summary>
    public partial class StoreRepository
    {
        private const int MaxPurchaseOrderLines = 500;
        private static readonly string[] PoTypeNames = { "", "PO By Item", "PO By Supplier", "PO By PR" };
        private static readonly string[] PoServiceTypes = { "No-Service", "Service" };
        private static readonly string[] PoPriorities = { "Normal", "Urgent", "Immediate" };

        #region Reads

        public ServiceResult<IEnumerable<POTermConditionModel>> GetPOTermConditions(AllGlobalValues globalValues)
        {
            try
            {
                var dt = _sqlHelper.GetDataTable("S_GetPOTermConditions", CommandType.StoredProcedure, new { hospId = globalValues.hospId });
                var list = dt?.AsEnumerable().Select(r => new POTermConditionModel
                {
                    Id = r.Field<int>("Id"),
                    PoTermCondition = r.Field<string>("POTermCondition") ?? string.Empty,
                    SequenceOrder = r.Field<int>("SequenceOrder"),
                    IsDefault = r.Field<int>("IsDefault")
                }).ToList() ?? new List<POTermConditionModel>();
                return ServiceResult<IEnumerable<POTermConditionModel>>.Success(list, "Info", $"{list.Count} term(s) found", 200);
            }
            catch (Exception ex)
            {
                return ServerError<IEnumerable<POTermConditionModel>>(ex);
            }
        }

        /// <summary>line defaults for picked items (itemIds) or OLD "Get Item by ROL" (belowReorderLevel)</summary>
        public ServiceResult<IEnumerable<PurchaseOrderItemDefaultModel>> GetPurchaseOrderItemDefaults(
            int branchId, string storeType, string? itemIds, bool belowReorderLevel, AllGlobalValues globalValues)
        {
            try
            {
                var list = LoadPoItemDefaults(branchId, PurchaseRequestStoreTypes[storeType], itemIds, belowReorderLevel, globalValues.hospId);
                string msg = list.Count == 0 ? (belowReorderLevel ? "No item is below its reorder level" : "Item not found") : $"{list.Count} item(s) found";
                return ServiceResult<IEnumerable<PurchaseOrderItemDefaultModel>>.Success(list, "Info", msg, 200);
            }
            catch (Exception ex)
            {
                return ServerError<IEnumerable<PurchaseOrderItemDefaultModel>>(ex);
            }
        }

        private List<PurchaseOrderItemDefaultModel> LoadPoItemDefaults(int branchId, int categoryId, string? itemIds, bool belowRol, int hospId)
        {
            var dt = _sqlHelper.GetDataTable("S_GetPurchaseOrderItemDefaults", CommandType.StoredProcedure,
                new { hospId, branchId, categoryId, itemIds, belowRol });
            return dt?.AsEnumerable().Select(r =>
            {
                var m = MapPoItemDefault(r);
                m.Qty = ToDecimal(r["SuggestedQty"]);
                m.BranchStock = ToInt(r["BranchStock"]);
                m.ReorderLevel = ToInt(r["ReorderLevel"]);
                m.MaxLevel = ToInt(r["MaxLevel"]);
                return m;
            }).ToList() ?? new List<PurchaseOrderItemDefaultModel>();
        }

        private static PurchaseOrderItemDefaultModel MapPoItemDefault(DataRow r) => new()
        {
            ItemId = r.Field<int>("ItemId"),
            ItemName = r.Field<string>("ItemName") ?? string.Empty,
            ItemCode = r.Field<string>("ItemCode") ?? string.Empty,
            Packing = ToInt(r["Packing"]),
            PurchaseUnitName = r.Field<string>("PurchaseUnitName") ?? string.Empty,
            HsnCode = r.Field<string>("HsnCode") ?? string.Empty,
            GstPer = ToDecimal(r["GstPer"]),
            Rate = ToDecimal(r["Rate"]),
            Mrp = ToDecimal(r["Mrp"]),
            VendorId = ToInt(r["VendorId"]),
            VendorName = r.Field<string>("VendorName") ?? string.Empty
        };

        /// <summary>OLD "Check Purchase Request" popup</summary>
        public ServiceResult<IEnumerable<ApprovedPurchaseRequestForPoModel>> GetApprovedPurchaseRequestsForPo(
            int branchId, string storeType, DateTime fromDate, DateTime toDate, int? fromDeptId, AllGlobalValues globalValues)
        {
            try
            {
                var dt = _sqlHelper.GetDataTable("S_GetApprovedPurchaseRequestsForPO", CommandType.StoredProcedure, new
                {
                    hospId = globalValues.hospId,
                    branchId,
                    categoryId = PurchaseRequestStoreTypes[storeType],
                    fromDate = fromDate.Date,
                    toDate = toDate.Date,
                    fromDeptId = fromDeptId > 0 ? fromDeptId : null
                });
                var list = dt?.AsEnumerable().Select(r => new ApprovedPurchaseRequestForPoModel
                {
                    PurchaseRequestId = r.Field<int>("PurchaseRequestId"),
                    PurchaseRequestNo = r.Field<string>("PurchaseRequestNo") ?? string.Empty,
                    PrDate = r.Field<string>("PRDate") ?? string.Empty,
                    FromDeptName = r.Field<string>("FromDeptName") ?? string.Empty,
                    ToDeptName = r.Field<string>("ToDeptName") ?? string.Empty,
                    StoreType = r.Field<string>("StoreType") ?? string.Empty,
                    ItemCount = ToInt(r["ItemCount"]),
                    ApprovedQty = ToInt(r["ApprovedQty"]),
                    ApprovedAmount = ToDecimal(r["ApprovedAmount"])
                }).ToList() ?? new List<ApprovedPurchaseRequestForPoModel>();
                return ServiceResult<IEnumerable<ApprovedPurchaseRequestForPoModel>>.Success(list, "Info",
                    list.Count == 0 ? "No Data Found!" : $"{list.Count} approved request(s) found", 200);
            }
            catch (Exception ex)
            {
                return ServerError<IEnumerable<ApprovedPurchaseRequestForPoModel>>(ex);
            }
        }

        /// <summary>OLD "Add Item" of the Check Purchase Request popup: approved qty of the ticked requests, one line per item</summary>
        public ServiceResult<IEnumerable<PurchaseOrderItemDefaultModel>> GetPurchaseRequestItemsForPo(int branchId, string purchaseRequestIds, AllGlobalValues globalValues)
        {
            try
            {
                var dt = _sqlHelper.GetDataTable("S_GetPurchaseRequestItemsForPO", CommandType.StoredProcedure,
                    new { hospId = globalValues.hospId, branchId, purchaseRequestIds });
                var list = dt?.AsEnumerable().Select(r =>
                {
                    var m = MapPoItemDefault(r);
                    m.Qty = ToDecimal(r["Qty"]);
                    m.PurchaseRequestNos = r.Field<string>("PurchaseRequestNos") ?? string.Empty;
                    return m;
                }).ToList() ?? new List<PurchaseOrderItemDefaultModel>();
                return ServiceResult<IEnumerable<PurchaseOrderItemDefaultModel>>.Success(list, "Info",
                    list.Count == 0 ? "No Data Found !" : $"{list.Count} item(s) found", 200);
            }
            catch (Exception ex)
            {
                return ServerError<IEnumerable<PurchaseOrderItemDefaultModel>>(ex);
            }
        }

        public ServiceResult<IEnumerable<PurchaseOrderListModel>> GetPurchaseOrderList(
            int branchId, DateTime fromDate, DateTime toDate, string? storeType, int? vendorId, int? statusId, string? poNo, AllGlobalValues globalValues)
        {
            try
            {
                var dt = _sqlHelper.GetDataTable("S_GetPurchaseOrderList", CommandType.StoredProcedure, new
                {
                    hospId = globalValues.hospId,
                    branchId,
                    userId = globalValues.userId,
                    fromDate = fromDate.Date,
                    toDate = toDate.Date,
                    categoryId = string.IsNullOrWhiteSpace(storeType) ? (int?)null : PurchaseRequestStoreTypes[storeType],
                    vendorId = vendorId > 0 ? vendorId : null,
                    statusId,
                    poNo = string.IsNullOrWhiteSpace(poNo) ? null : poNo.Trim()
                });
                var list = dt?.AsEnumerable().Select(r => new PurchaseOrderListModel
                {
                    PoId = r.Field<int>("PoId"),
                    PoNo = r.Field<string>("PONo") ?? string.Empty,
                    BranchId = r.Field<int>("BranchId"),
                    RoleId = r.Field<int>("RoleId"),
                    FromDeptName = r.Field<string>("FromDeptName") ?? string.Empty,
                    StoreType = r.Field<string>("StoreType") ?? string.Empty,
                    PoType = r.Field<string>("POType") ?? string.Empty,
                    TypeId = r.Field<int>("TypeId"),
                    PoTypes = r.Field<string>("POTypes") ?? string.Empty,
                    VendorId = r.Field<int>("VendorId"),
                    VendorName = r.Field<string>("VendorName") ?? string.Empty,
                    PoDate = r.Field<string>("PODate") ?? string.Empty,
                    Priority = r.Field<string>("Priority") ?? string.Empty,
                    GrossAmount = ToDecimal(r["GrossAmount"]),
                    DiscountAmount = ToDecimal(r["DiscountAmount"]),
                    GstAmount = ToDecimal(r["GSTAmount"]),
                    NetAmount = ToDecimal(r["NetAmount"]),
                    ApprovalAmount = ToDecimal(r["ApprovalAmount"]),
                    AmountUpTo = r["AmountUpTo"] == DBNull.Value ? null : ToDecimal(r["AmountUpTo"]),
                    StatusId = r.Field<int>("StatusId"),
                    IsCancel = r.Field<int>("IsCancel"),
                    Status = r.Field<string>("Status") ?? string.Empty,
                    CurrentApprover = r.Field<string>("CurrentApprover") ?? string.Empty,
                    ItemCount = ToInt(r["ItemCount"]),
                    TotalQty = ToDecimal(r["TotalQty"]),
                    Remarks = r.Field<string>("Remarks") ?? string.Empty,
                    ApprovalRemarks = r.Field<string>("ApprovalRemarks") ?? string.Empty,
                    IsLevel1Approve = r.Field<int?>("IsLevel1Approve"),
                    Level1ApproveOn = r.Field<string>("Level1ApproveOn"),
                    IsLevel2Approve = r.Field<int?>("IsLevel2Approve"),
                    Level2ApproveOn = r.Field<string>("Level2ApproveOn"),
                    IsLevel3Approve = r.Field<int?>("IsLevel3Approve"),
                    Level3ApproveOn = r.Field<string>("Level3ApproveOn"),
                    IsLevel4Approve = r.Field<int?>("IsLevel4Approve"),
                    Level4ApproveOn = r.Field<string>("Level4ApproveOn"),
                    CancelBy = r.Field<string>("CancelBy"),
                    CancelOn = r.Field<string>("CancelOn"),
                    CancelReason = r.Field<string>("CancelReason"),
                    CreatedBy = r.Field<string>("CreatedBy"),
                    CreatedOn = r.Field<string>("CreatedOn"),
                    FlagId = ToInt(r["FlagId"]),
                    CanApprove = ToInt(r["CanApprove"]),
                    CanEdit = ToInt(r["CanEdit"])
                }).ToList() ?? new List<PurchaseOrderListModel>();

                return ServiceResult<IEnumerable<PurchaseOrderListModel>>.Success(list, "Info",
                    list.Count == 0 ? "No Data Found !" : $"{list.Count} purchase order(s) found", 200);
            }
            catch (Exception ex)
            {
                return ServerError<IEnumerable<PurchaseOrderListModel>>(ex);
            }
        }

        public ServiceResult<PurchaseOrderDetailsModel> GetPurchaseOrderDetails(int poId, AllGlobalValues globalValues)
        {
            try
            {
                var ds = _sqlHelper.GetDataSet("S_GetPurchaseOrderDetails", CommandType.StoredProcedure,
                    new { hospId = globalValues.hospId, poId, userId = globalValues.userId });
                if (ds == null || ds.Tables.Count == 0 || ds.Tables[0].Rows.Count == 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("OPERATION_FAILED");
                    return ServiceResult<PurchaseOrderDetailsModel>.Failure(alert.Type, "Purchase Order not found", 404);
                }

                DataRow h = ds.Tables[0].Rows[0];
                string? D(string col) => h[col] == DBNull.Value ? null : Convert.ToDateTime(h[col]).ToString("yyyy-MM-dd");
                var m = new PurchaseOrderDetailsModel
                {
                    PoId = h.Field<int>("PoId"),
                    PoNo = h.Field<string>("PONo") ?? string.Empty,
                    BranchId = h.Field<int>("BranchId"),
                    RoleId = h.Field<int>("RoleId"),
                    FromDeptName = h.Field<string>("FromDeptName") ?? string.Empty,
                    StoreType = h.Field<string>("StoreType") ?? string.Empty,
                    CategoryId = h.Field<int>("CategoryId"),
                    PoType = h.Field<string>("POType") ?? string.Empty,
                    TypeId = h.Field<int>("TypeId"),
                    PoTypes = h.Field<string>("POTypes") ?? string.Empty,
                    VendorId = h.Field<int>("VendorId"),
                    VendorName = h.Field<string>("VendorName") ?? string.Empty,
                    GrossAmount = ToDecimal(h["GrossAmount"]),
                    DiscountPercentage = ToDecimal(h["DiscountPercentage"]),
                    DiscountAmount = ToDecimal(h["DiscountAmount"]),
                    TaxableAmount = ToDecimal(h["TaxableAmount"]),
                    GstAmount = ToDecimal(h["GSTAmount"]),
                    NetAmount = ToDecimal(h["NetAmount"]),
                    RoundOff = ToDecimal(h["RoundOff"]),
                    ApprovalAmount = ToDecimal(h["ApprovalAmount"]),
                    AmountUpTo = h["AmountUpTo"] == DBNull.Value ? null : ToDecimal(h["AmountUpTo"]),
                    Priority = h.Field<string>("Priority") ?? string.Empty,
                    PoDate = D("PODate") ?? string.Empty,
                    DeliveryDate = D("DeliveryDate"),
                    ValidDate = D("ValidDate"),
                    PoRemarks = h.Field<string>("PORemarks") ?? string.Empty,
                    DeliveryAddress = h.Field<string>("DeliveryAddress") ?? string.Empty,
                    DocumentPath = h.Field<string>("DocumentPath") ?? string.Empty,
                    OtherTermCondition = h.Field<string>("OtherTermCondition") ?? string.Empty,
                    PurchaseRequestIds = (h.Field<string>("PurchaseRequestIds") ?? string.Empty)
                        .Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(x => int.TryParse(x, out var v) ? v : 0).Where(v => v > 0).ToList(),
                    Status = h.Field<string>("Status") ?? string.Empty,
                    StatusId = h.Field<int>("StatusId"),
                    IsCancel = h.Field<int>("IsCancel"),
                    ApprovalRemarks = h.Field<string>("ApprovalRemarks") ?? string.Empty,
                    CreatedOn = h.Field<string>("CreatedOn"),
                    CreatedBy = h.Field<string>("CreatedBy"),
                    ApprovalFlowId = ToNullableInt(h["ApprovalFlowId"]),
                    ApprovalFlow = h.Field<string>("ApprovalFlow"),
                    IsAllApprovalRequired = ToNullableInt(h["IsAllApprovalRequired"]),
                    ApprovalLevelId = ToNullableInt(h["ApprovalLevelId"]),
                    ApprovalLevel = h.Field<string>("ApprovalLevel"),
                    Level1ApproverNames = h.Field<string>("Level1ApproverNames"),
                    Level2ApproverNames = h.Field<string>("Level2ApproverNames"),
                    Level3ApproverNames = h.Field<string>("Level3ApproverNames"),
                    Level4ApproverNames = h.Field<string>("Level4ApproverNames"),
                    IsLevel1Approve = h.Field<int?>("IsLevel1Approve"),
                    Level1ApprovedByName = h.Field<string>("Level1ApprovedByName"),
                    Level1ApproveOn = h.Field<string>("Level1ApproveOn"),
                    IsLevel2Approve = h.Field<int?>("IsLevel2Approve"),
                    Level2ApprovedByName = h.Field<string>("Level2ApprovedByName"),
                    Level2ApproveOn = h.Field<string>("Level2ApproveOn"),
                    IsLevel3Approve = h.Field<int?>("IsLevel3Approve"),
                    Level3ApprovedByName = h.Field<string>("Level3ApprovedByName"),
                    Level3ApproveOn = h.Field<string>("Level3ApproveOn"),
                    IsLevel4Approve = h.Field<int?>("IsLevel4Approve"),
                    Level4ApprovedByName = h.Field<string>("Level4ApprovedByName"),
                    Level4ApproveOn = h.Field<string>("Level4ApproveOn"),
                    CancelBy = h.Field<string>("CancelBy"),
                    CancelOn = h.Field<string>("CancelOn"),
                    CancelReason = h.Field<string>("CancelReason"),
                    FlagId = ToInt(h["FlagId"])
                };

                if (ds.Tables.Count > 1)
                    m.Items = ds.Tables[1].AsEnumerable().Select(r => new PurchaseOrderItemModel
                    {
                        PoItemId = r.Field<int>("PoItemId"),
                        ItemId = r.Field<int>("ItemId"),
                        ItemName = r.Field<string>("ItemName") ?? string.Empty,
                        ItemCode = r.Field<string>("ItemCode") ?? string.Empty,
                        PurchaseUnitName = r.Field<string>("PurchaseUnitName") ?? string.Empty,
                        Qty = ToDecimal(r["Qty"]),
                        FreeQty = ToDecimal(r["FreeQty"]),
                        Packing = ToInt(r["Packing"]),
                        Deal1 = ToInt(r["Deal1"]),
                        Deal2 = ToInt(r["Deal2"]),
                        HsnCode = r.Field<string>("HsnCode") ?? string.Empty,
                        Mrp = ToDecimal(r["Mrp"]),
                        Rate = ToDecimal(r["Rate"]),
                        DiscPer = ToDecimal(r["DiscPer"]),
                        DiscAmt = ToDecimal(r["DiscAmt"]),
                        SpclDiscPer = ToDecimal(r["SpclDiscPer"]),
                        SpclDiscAmt = ToDecimal(r["SpclDiscAmt"]),
                        GstPer = ToDecimal(r["GstPer"]),
                        GstAmt = ToDecimal(r["GstAmt"]),
                        GrossAmt = ToDecimal(r["GrossAmt"]),
                        NetAmt = ToDecimal(r["NetAmt"]),
                        UnitCost = ToDecimal(r["UnitCost"])
                    }).ToList();

                if (ds.Tables.Count > 2)
                    m.Terms = ds.Tables[2].AsEnumerable().Select(r => new POTermConditionModel
                    {
                        Id = r.Field<int>("Id"),
                        PoTermCondition = r.Field<string>("POTermCondition") ?? string.Empty
                    }).ToList();

                return ServiceResult<PurchaseOrderDetailsModel>.Success(m, "Info", "Purchase Order retrieved successfully", 200);
            }
            catch (Exception ex)
            {
                return ServerError<PurchaseOrderDetailsModel>(ex);
            }
        }

        #endregion

        #region Writes

        /// <summary>
        /// Save (OLD $savepurchaseorder): validate every line (OLD messages), group lines by vendor, recompute amounts,
        /// save one PO per vendor in one transaction. With SendForApproval each saved PO is then sent; a PO whose amount
        /// has no approval rule stays Draft and the reply says so.
        /// </summary>
        public ServiceResult<IEnumerable<PurchaseOrderSavedModel>> SavePurchaseOrder(PurchaseOrderSaveRequest request, AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"SavePurchaseOrder called. PoId={request.PoId}, Type={request.TypeId}, Lines={request.Items?.Count ?? 0}");

                string storeType = PurchaseRequestStoreTypes.Keys.FirstOrDefault(k => k.Equals(request.StoreType?.Trim(), StringComparison.OrdinalIgnoreCase)) ?? string.Empty;
                string poType = PoServiceTypes.FirstOrDefault(k => k.Equals(request.PoType?.Trim(), StringComparison.OrdinalIgnoreCase)) ?? string.Empty;
                string priority = PoPriorities.FirstOrDefault(k => k.Equals(request.Priority?.Trim(), StringComparison.OrdinalIgnoreCase)) ?? string.Empty;
                var items = request.Items ?? new List<PurchaseOrderItemRequest>();
                DateTime poDate = request.PoDate.Date;

                string? err =
                    storeType == "" ? "Store must be Medical or General"
                    : poType == "" ? "PO Type must be No-Service or Service"
                    : priority == "" ? "Priority must be Normal, Urgent or Immediate"
                    : poDate == DateTime.MinValue ? "Please select PO Date"
                    : request.DeliveryDate.HasValue && request.DeliveryDate.Value.Date < poDate ? "Delivery Date cannot be before PO Date"
                    : request.ValidDate.HasValue && request.ValidDate.Value.Date < poDate ? "Valid Date cannot be before PO Date"
                    : request.TypeId == 3 && request.PoId == 0 && (request.PurchaseRequestIds?.Count ?? 0) == 0 ? "Please Select at least one Purchase Request"
                    : items.Count == 0 ? "Please add at least one item"
                    : items.Count > MaxPurchaseOrderLines ? $"A Purchase Order can have at most {MaxPurchaseOrderLines} items"
                    : null;
                if (err != null) return Invalid<IEnumerable<PurchaseOrderSavedModel>>(err);

                var myRoles = GetStockReceivingStores(request.BranchId, globalValues).Data ?? Enumerable.Empty<ReceivingStoreModel>();
                if (!myRoles.Any(r => r.RoleId == request.RoleId))
                    return Invalid<IEnumerable<PurchaseOrderSavedModel>>("You can create a Purchase Order only for your own department");

                // items: active items of the store type
                int categoryId = PurchaseRequestStoreTypes[storeType];
                var master = LoadPoItemDefaults(request.BranchId, categoryId, string.Join(",", items.Select(i => i.ItemId).Distinct()), false, globalValues.hospId)
                    .ToDictionary(m => m.ItemId);

                // vendors: active and mapped to the branch (blank mapping = all branches)
                var vendorIds = items.Select(i => i.VendorId).Where(v => v > 0).Distinct().ToList();
                var vendors = LoadPoVendors(vendorIds, request.BranchId);

                var seen = new HashSet<(int, int)>();
                for (int n = 0; n < items.Count; n++)
                {
                    var it = items[n];
                    if (!master.TryGetValue(it.ItemId, out var m))
                        return Invalid<IEnumerable<PurchaseOrderSavedModel>>($"Row {n + 1}: Item {it.ItemId} is not an active {storeType} store item");
                    string name = m.ItemName;
                    if (it.VendorId <= 0) return Invalid<IEnumerable<PurchaseOrderSavedModel>>($"Please Map Vendor Name for Item : {name}");
                    if (!vendors.ContainsKey(it.VendorId)) return Invalid<IEnumerable<PurchaseOrderSavedModel>>($"Vendor of Item : {name} is not active for this branch");
                    if (!seen.Add((it.VendorId, it.ItemId))) return Invalid<IEnumerable<PurchaseOrderSavedModel>>($"Item already added : {name}");
                    if (it.Qty < 1 || it.Qty > 99999) return Invalid<IEnumerable<PurchaseOrderSavedModel>>($"Please Enter a valid QTY for Item : {name}");
                    if (it.FreeQty < 0 || it.FreeQty > 99999) return Invalid<IEnumerable<PurchaseOrderSavedModel>>($"Please Enter a valid Free QTY for Item : {name}");
                    if (it.Packing < 1 || it.Packing > 100000) return Invalid<IEnumerable<PurchaseOrderSavedModel>>($"Please Enter a valid Packing for Item : {name}");
                    if (it.Deal1 < 0 || it.Deal2 < 0 || (it.Deal1 > 0) != (it.Deal2 > 0)) return Invalid<IEnumerable<PurchaseOrderSavedModel>>($"Enter both Deal Buy and Deal Free, or neither, for Item : {name}");
                    if (it.Rate <= 0) return Invalid<IEnumerable<PurchaseOrderSavedModel>>($"Please Enter a valid Rate for Item : {name}");
                    if (it.Mrp < 0) return Invalid<IEnumerable<PurchaseOrderSavedModel>>($"Please Enter a valid MRP for Item : {name}");
                    if (it.Mrp > 0 && it.Rate > it.Mrp) return Invalid<IEnumerable<PurchaseOrderSavedModel>>($"Rate can not be greater than MRP for Item : {name}");
                    if (it.DiscPer is < 0 or > 100 || it.SpclDiscPer is < 0 or > 100) return Invalid<IEnumerable<PurchaseOrderSavedModel>>($"Discount % must be 0-100 for Item : {name}");
                    if (it.GstPer is < 0 or > 100) return Invalid<IEnumerable<PurchaseOrderSavedModel>>($"Please Enter a valid GST % for Item : {name}");
                    if (it.DiscAmt < 0) return Invalid<IEnumerable<PurchaseOrderSavedModel>>($"Discount Amt cannot be negative for Item : {name}");
                }

                var groups = items.GroupBy(i => i.VendorId).ToList();
                if (request.PoId > 0 && groups.Count > 1)
                    return Invalid<IEnumerable<PurchaseOrderSavedModel>>("All items of an existing Purchase Order must have the same vendor");

                string? prIds = (request.PurchaseRequestIds?.Count ?? 0) == 0 ? null : string.Join(",", request.PurchaseRequestIds!.Distinct());
                string? termIds = (request.TermIds?.Count ?? 0) == 0 ? null : string.Join(",", request.TermIds!.Distinct());

                var saved = new List<PurchaseOrderSavedModel>();
                using (var con = new SqlConnection(_configuration.GetConnectionString("ConnectionString")))
                {
                    con.Open();
                    using SqlTransaction tnx = CustomSqlHelper.getSqlTransaction(con);
                    foreach (var g in groups)
                    {
                        var lines = g.ToList();
                        bool lineDisc = lines.Any(l => l.DiscPer > 0 || l.DiscAmt > 0);
                        decimal poDisc = lineDisc ? 0 : request.DiscountPercent;
                        var calc = lines.Select(l => (l, c: CalcGrnLine(ToGrnItem(l), poDisc))).ToList();

                        foreach (var (l, c) in calc)
                            if (l.DiscAmt > 0 && l.DiscPer <= 0 && c.DiscAmt > c.Gross)
                                return Invalid<IEnumerable<PurchaseOrderSavedModel>>($"Discount Amt cannot be greater than Gross Amt for Item : {master[l.ItemId].ItemName}");

                        decimal gross = calc.Sum(x => x.c.Gross), disc = calc.Sum(x => x.c.DiscAmt + x.c.SpclDiscAmt),
                                taxable = calc.Sum(x => x.c.Taxable), gst = calc.Sum(x => x.c.Gst), net = calc.Sum(x => x.c.Net);
                        decimal pay = Math.Round(net, 0, MidpointRounding.AwayFromZero);
                        if (pay <= 0) return Invalid<IEnumerable<PurchaseOrderSavedModel>>("Net Bill Amount cannot be 0");

                        string itemsJson = System.Text.Json.JsonSerializer.Serialize(calc.Select(x => new
                        {
                            x.l.ItemId,
                            ItemName = master[x.l.ItemId].ItemName,
                            x.l.Qty,
                            x.l.FreeQty,
                            x.l.Packing,
                            x.l.Deal1,
                            x.l.Deal2,
                            HsnCode = string.IsNullOrWhiteSpace(x.l.HsnCode) ? null : x.l.HsnCode.Trim(),
                            x.l.Mrp,
                            x.l.Rate,
                            DiscPer = x.c.DiscPer,
                            DiscAmt = x.c.DiscAmt,
                            x.l.SpclDiscPer,
                            SpclDiscAmt = x.c.SpclDiscAmt,
                            x.l.GstPer,
                            GstAmt = x.c.Gst,
                            GrossAmt = x.c.Gross,
                            NetAmt = x.c.Net,
                            x.c.UnitCost
                        }));

                        object raw = _sqlHelper.DML(tnx, "IU_PurchaseOrder", CommandType.StoredProcedure, new
                        {
                            hospId = globalValues.hospId,
                            branchId = request.BranchId,
                            poId = request.PoId,
                            roleId = request.RoleId,
                            storeType,
                            categoryId,
                            poType,
                            typeId = request.TypeId,
                            poTypes = PoTypeNames[request.TypeId],
                            vendorId = g.Key,
                            grossAmount = gross,
                            discountPercentage = poDisc,
                            discountAmount = disc,
                            taxableAmount = taxable,
                            gstAmount = gst,
                            netAmount = pay,
                            roundOff = pay - net,
                            priority,
                            poDate,
                            deliveryDate = request.DeliveryDate?.Date,
                            validDate = request.ValidDate?.Date,
                            poRemarks = string.IsNullOrWhiteSpace(request.Remarks) ? null : request.Remarks.Trim(),
                            deliveryAddress = string.IsNullOrWhiteSpace(request.DeliveryAddress) ? null : request.DeliveryAddress.Trim(),
                            documentPath = string.IsNullOrWhiteSpace(request.DocumentPath) ? null : request.DocumentPath.Trim(),
                            otherTermCondition = string.IsNullOrWhiteSpace(request.OtherTermCondition) ? null : request.OtherTermCondition.Trim(),
                            purchaseRequestIds = prIds,
                            termIds,
                            itemsJson,
                            userId = globalValues.userId,
                            ipAddress = globalValues.ipAddress
                        }, new { Result = 0 });
                        int id = raw == null || raw == DBNull.Value ? 0 : Convert.ToInt32(raw);
                        if (id <= 0)
                        {
                            tnx.Rollback();
                            string msg = id switch
                            {
                                -2 => "Only a Draft Purchase Order, or a pending one not yet approved by anyone, can be edited",
                                -3 => "No Purchase Order number sequence is set up - add it in Sequence Master",
                                -7 => "A selected Purchase Request is not approved or is already used by another Purchase Order",
                                _ => "Purchase Order could not be saved"
                            };
                            var alert = _messageService.GetMessageAndTypeByAlertCode("OPERATION_FAILED");
                            return ServiceResult<IEnumerable<PurchaseOrderSavedModel>>.Failure(alert.Type, msg, 200);
                        }
                        saved.Add(new PurchaseOrderSavedModel { PoId = id, VendorName = vendors[g.Key], NetAmount = pay });
                    }
                    tnx.Commit();
                }

                // PO numbers, then optional send
                foreach (var s in saved)
                {
                    s.PoNo = GetPurchaseOrderDetails(s.PoId, globalValues).Data?.PoNo ?? string.Empty;
                    if (request.SendForApproval)
                    {
                        int r = RunPurchaseRequestSp("U_SendPurchaseOrderForApproval",
                            new { hospId = globalValues.hospId, poId = s.PoId, userId = globalValues.userId, ipAddress = globalValues.ipAddress });
                        s.Sent = r > 0;
                        if (r == -5) s.SendMessage = $"Approval Authority Not Found for amount ₹{s.NetAmount:N2} - saved as Draft";
                    }
                }

                string okMsg = request.PoId > 0 ? "Purchase Order Updated Successfully"
                    : saved.Count == 1 ? "Purchase Order Saved Successfully"
                    : $"{saved.Count} Purchase Orders Saved Successfully (one per vendor)";
                if (request.SendForApproval)
                {
                    int notSent = saved.Count(s => !s.Sent);
                    okMsg += notSent == 0 ? " and sent for approval" : $" - {notSent} could not be sent: " + string.Join("; ", saved.Where(s => !s.Sent).Select(s => $"{s.PoNo}: {s.SendMessage ?? "not sent"}"));
                }
                var ok = _messageService.GetMessageAndTypeByAlertCode(request.PoId > 0 ? "DATA_UPDATED_SUCCESSFULLY" : "DATA_SAVED_SUCCESSFULLY");
                return ServiceResult<IEnumerable<PurchaseOrderSavedModel>>.Success(saved, ok.Type, okMsg, 200);
            }
            catch (Exception ex)
            {
                return ServerError<IEnumerable<PurchaseOrderSavedModel>>(ex);
            }
        }

        private static StockReceivingItemRequest ToGrnItem(PurchaseOrderItemRequest l) => new()
        {
            ItemId = l.ItemId,
            Qty = l.Qty,
            FreeQty = l.FreeQty,
            Packing = l.Packing,
            Deal1 = l.Deal1,
            Deal2 = l.Deal2,
            Mrp = l.Mrp,
            Rate = l.Rate,
            DiscPer = l.DiscPer,
            DiscAmt = l.DiscAmt,
            SpclDiscPer = l.SpclDiscPer,
            GstPer = l.GstPer
        };

        /// <summary>active vendors mapped to the branch (blank MappingBranch = every branch): id -> name</summary>
        private Dictionary<int, string> LoadPoVendors(List<int> vendorIds, int branchId)
        {
            if (vendorIds.Count == 0) return new Dictionary<int, string>();
            var dt = _sqlHelper.GetDataTable(
                @"SELECT VendorId, LTRIM(RTRIM(VendorName)) AS VendorName, ISNULL(MappingBranch, '') AS MappingBranch
                  FROM dbo.VendorMaster
                  WHERE IsActive = 1 AND VendorId IN (SELECT TRY_CAST(value AS int) FROM STRING_SPLIT(@ids, ','))",
                CommandType.Text, new { ids = string.Join(",", vendorIds) });
            return dt?.AsEnumerable()
                .Where(r =>
                {
                    var ids = (r.Field<string>("MappingBranch") ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    return ids.Length == 0 || ids.Contains(branchId.ToString());
                })
                .ToDictionary(r => r.Field<int>("VendorId"), r => r.Field<string>("VendorName") ?? string.Empty)
                ?? new Dictionary<int, string>();
        }

        public ServiceResult<PurchaseOrderSavedModel> ChangePurchaseOrderStatus(string action, PurchaseOrderActionRequest request, AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"ChangePurchaseOrderStatus called. Action={action}, PoId={request.PoId}");
                bool cancel = action == "cancel";
                if (cancel && string.IsNullOrWhiteSpace(request.Reason))
                    return Invalid<PurchaseOrderSavedModel>("Cancel reason is required");

                int result = cancel
                    ? RunPurchaseRequestSp("U_CancelPurchaseOrder", new
                    {
                        hospId = globalValues.hospId,
                        poId = request.PoId,
                        reason = request.Reason!.Trim(),
                        userId = globalValues.userId,
                        ipAddress = globalValues.ipAddress
                    })
                    : RunPurchaseRequestSp("U_SendPurchaseOrderForApproval", new
                    {
                        hospId = globalValues.hospId,
                        poId = request.PoId,
                        userId = globalValues.userId,
                        ipAddress = globalValues.ipAddress
                    });

                if (result <= 0)
                {
                    string msg = result switch
                    {
                        -5 => $"Approval Authority Not Found for amount ₹{(GetPurchaseOrderDetails(request.PoId, globalValues).Data?.NetAmount ?? 0):N2} - add an \"Amount Up To\" slab for Purchase Order in Approval Authority Master",
                        _ => cancel ? "Only a Draft or Approval Pending Purchase Order can be cancelled"
                                    : "Only a Draft Purchase Order can be sent for approval"
                    };
                    var alert = _messageService.GetMessageAndTypeByAlertCode("OPERATION_FAILED");
                    return ServiceResult<PurchaseOrderSavedModel>.Failure(alert.Type, msg, 200);
                }

                var ok = _messageService.GetMessageAndTypeByAlertCode("DATA_UPDATED_SUCCESSFULLY");
                return ServiceResult<PurchaseOrderSavedModel>.Success(new PurchaseOrderSavedModel { PoId = request.PoId, Sent = !cancel },
                    ok.Type, cancel ? "PO cancelled successfully" : "Purchase Order sent for approval", 200);
            }
            catch (Exception ex)
            {
                return ServerError<PurchaseOrderSavedModel>(ex);
            }
        }

        public ServiceResult<PurchaseOrderSavedModel> ApprovePurchaseOrder(PurchaseOrderApproveRequest request, AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"ApprovePurchaseOrder called. PoId={request.PoId}, Flag={request.Flag}");
                int result = RunPurchaseRequestSp("U_ApprovePurchaseOrder", new
                {
                    hospId = globalValues.hospId,
                    poId = request.PoId,
                    flag = request.Flag,
                    approvalRemarks = string.IsNullOrWhiteSpace(request.ApprovalRemarks) ? null : request.ApprovalRemarks.Trim(),
                    userId = globalValues.userId,
                    ipAddress = globalValues.ipAddress
                });

                if (result <= 0)
                {
                    string msg = result switch
                    {
                        -1 => "You are not an approver for the current approval level of this Purchase Order",
                        -2 => "Only an Approval Pending Purchase Order can be approved",
                        _ => "Purchase Order could not be approved"
                    };
                    var alert = _messageService.GetMessageAndTypeByAlertCode("OPERATION_FAILED");
                    return ServiceResult<PurchaseOrderSavedModel>.Failure(alert.Type, msg, 200);
                }

                var after = GetPurchaseOrderDetails(request.PoId, globalValues).Data;
                var ok = _messageService.GetMessageAndTypeByAlertCode("DATA_UPDATED_SUCCESSFULLY");
                return ServiceResult<PurchaseOrderSavedModel>.Success(new PurchaseOrderSavedModel { PoId = request.PoId, PoNo = after?.PoNo ?? "" },
                    ok.Type, after?.StatusId == 3 ? "Purchase Order approved" : $"Level {request.Flag} approval saved - sent to the next level", 200);
            }
            catch (Exception ex)
            {
                return ServerError<PurchaseOrderSavedModel>(ex);
            }
        }

        #endregion
    }
}
