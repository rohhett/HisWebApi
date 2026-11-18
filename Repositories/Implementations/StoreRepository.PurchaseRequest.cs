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
    /// Purchase Request + Approve Purchase Request (2026-11-16).
    /// Rules ported from OLD Store/PurchaseRequest + Store/ViewPurchaseRequest (Store.savePurchaseRequest,
    /// updatePurchaseRequestStatus, CancelPurchaseRequest). Approval follows the NEW approval engine
    /// (ApprovalAuthorityMaster type 2; the SP checks that the user may approve the current level).
    /// SPs: script Database/2026-11-16_PurchaseRequest.sql.
    /// </summary>
    public partial class StoreRepository
    {
        /// <summary>Purchase requests are raised for Medical and General store items only (OLD ddlStore).</summary>
        private static readonly Dictionary<string, int> PurchaseRequestStoreTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Medical"] = 6,
            ["General"] = 7
        };

        private const int MaxPurchaseRequestLines = 500;
        private const int MaxPurchaseRequestQty = 99999;   // OLD numbersOnly(..., 5)

        #region Reads

        public ServiceResult<IEnumerable<ReceivingStoreModel>> GetPurchaseRequestDepartments(AllGlobalValues globalValues)
        {
            try
            {
                var dt = _sqlHelper.GetDataTable("S_GetPurchaseRequestDepartments", CommandType.StoredProcedure, new { hospId = globalValues.hospId });
                var list = dt?.AsEnumerable().Select(r => new ReceivingStoreModel
                {
                    RoleId = r.Field<int>("RoleId"),
                    RoleName = r.Field<string>("RoleName") ?? string.Empty
                }).ToList() ?? new List<ReceivingStoreModel>();
                return ServiceResult<IEnumerable<ReceivingStoreModel>>.Success(list, "Info", $"{list.Count} department(s) retrieved successfully", 200);
            }
            catch (Exception ex)
            {
                return ServerError<IEnumerable<ReceivingStoreModel>>(ex);
            }
        }

        public ServiceResult<IEnumerable<PurchaseRequestItemSearchModel>> SearchPurchaseRequestItem(
            int branchId, int fromDeptId, string storeType, string? search, bool belowReorderLevel, AllGlobalValues globalValues)
        {
            try
            {
                var list = LoadPurchaseRequestItems(branchId, fromDeptId, PurchaseRequestStoreTypes[storeType], search, null,
                    belowReorderLevel, globalValues.hospId, belowReorderLevel ? MaxPurchaseRequestLines : 30);
                string msg = list.Count == 0
                    ? (belowReorderLevel ? "No item is below its reorder level" : "Item not found")
                    : $"{list.Count} item(s) found";
                return ServiceResult<IEnumerable<PurchaseRequestItemSearchModel>>.Success(list, "Info", msg, 200);
            }
            catch (Exception ex)
            {
                return ServerError<IEnumerable<PurchaseRequestItemSearchModel>>(ex);
            }
        }

        private List<PurchaseRequestItemSearchModel> LoadPurchaseRequestItems(
            int branchId, int fromDeptId, int categoryId, string? search, string? itemIds, bool belowRol, int hospId, int top)
        {
            var dt = _sqlHelper.GetDataTable("S_SearchItemForPurchaseRequest", CommandType.StoredProcedure, new
            {
                hospId,
                branchId,
                fromDeptId,
                categoryId,
                search = string.IsNullOrWhiteSpace(search) ? null : search.Trim(),
                itemIds,
                belowRol,
                top
            });
            return dt?.AsEnumerable().Select(r => new PurchaseRequestItemSearchModel
            {
                ItemId = r.Field<int>("ItemId"),
                ItemName = r.Field<string>("ItemName") ?? string.Empty,
                ItemCode = r.Field<string>("ItemCode") ?? string.Empty,
                Packing = r.Field<int>("Packing"),
                PurchaseUnitName = r.Field<string>("PurchaseUnitName") ?? string.Empty,
                SaleUnitName = r.Field<string>("SaleUnitName") ?? string.Empty,
                CurrentStock = ToInt(r["CurrentStock"]),
                MinLevel = ToInt(r["MinLevel"]),
                ReorderLevel = ToInt(r["ReorderLevel"]),
                MaxLevel = ToInt(r["MaxLevel"]),
                Rate = ToDecimal(r["Rate"])
            }).ToList() ?? new List<PurchaseRequestItemSearchModel>();
        }

        public ServiceResult<IEnumerable<PurchaseRequestListModel>> GetPurchaseRequestList(
            int branchId, DateTime fromDate, DateTime toDate, int? fromDeptId, int? toDeptId, string? storeType, int? statusId,
            string? prNo, AllGlobalValues globalValues)
        {
            try
            {
                var dt = _sqlHelper.GetDataTable("S_GetPurchaseRequestList", CommandType.StoredProcedure, new
                {
                    hospId = globalValues.hospId,
                    branchId,
                    userId = globalValues.userId,
                    fromDate = fromDate.Date,
                    toDate = toDate.Date,
                    fromDeptId = fromDeptId > 0 ? fromDeptId : null,
                    toDeptId = toDeptId > 0 ? toDeptId : null,
                    categoryId = string.IsNullOrWhiteSpace(storeType) ? (int?)null : PurchaseRequestStoreTypes[storeType],
                    statusId,
                    prNo = string.IsNullOrWhiteSpace(prNo) ? null : prNo.Trim()
                });

                var list = dt?.AsEnumerable().Select(r => new PurchaseRequestListModel
                {
                    PurchaseRequestId = r.Field<int>("PurchaseRequestId"),
                    PurchaseRequestNo = r.Field<string>("PurchaseRequestNo") ?? string.Empty,
                    BranchId = r.Field<int>("BranchId"),
                    FromDeptId = r.Field<int>("FromDeptId"),
                    ToDeptId = r.Field<int>("ToDeptId"),
                    CategoryId = r.Field<int>("CategoryId"),
                    FromDeptName = r.Field<string>("FromDeptName") ?? string.Empty,
                    ToDeptName = r.Field<string>("ToDeptName") ?? string.Empty,
                    StoreType = r.Field<string>("StoreType") ?? string.Empty,
                    PrDate = r.Field<string>("PRDate") ?? string.Empty,
                    Remarks = r.Field<string>("Remarks"),
                    StatusId = r.Field<int>("StatusId"),
                    IsCancel = r.Field<int>("IsCancel"),
                    Status = r.Field<string>("Status") ?? string.Empty,
                    CurrentApprover = r.Field<string>("CurrentApprover") ?? string.Empty,
                    ItemCount = ToInt(r["ItemCount"]),
                    RequestedQty = ToInt(r["RequestedQty"]),
                    ApprovedQty = ToInt(r["ApprovedQty"]),
                    TotalAmount = ToDecimal(r["TotalAmount"]),
                    ApprovedAmount = ToDecimal(r["ApprovedAmount"]),
                    ApprovalAmount = ToDecimal(r["ApprovalAmount"]),
                    AmountUpTo = r["AmountUpTo"] == DBNull.Value ? null : ToDecimal(r["AmountUpTo"]),
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
                    CanApprove = ToInt(r["CanApprove"])
                }).ToList() ?? new List<PurchaseRequestListModel>();

                return ServiceResult<IEnumerable<PurchaseRequestListModel>>.Success(list, "Info",
                    list.Count == 0 ? "No Data Found!" : $"{list.Count} purchase request(s) found", 200);
            }
            catch (Exception ex)
            {
                return ServerError<IEnumerable<PurchaseRequestListModel>>(ex);
            }
        }

        public ServiceResult<PurchaseRequestDetailsModel> GetPurchaseRequestDetails(int purchaseRequestId, AllGlobalValues globalValues)
        {
            try
            {
                var ds = _sqlHelper.GetDataSet("S_GetPurchaseRequestDetails", CommandType.StoredProcedure, new
                {
                    hospId = globalValues.hospId,
                    purchaseRequestId,
                    userId = globalValues.userId
                });

                if (ds == null || ds.Tables.Count == 0 || ds.Tables[0].Rows.Count == 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("OPERATION_FAILED");
                    return ServiceResult<PurchaseRequestDetailsModel>.Failure(alert.Type, "Purchase Request not found", 404);
                }

                DataRow h = ds.Tables[0].Rows[0];
                var model = new PurchaseRequestDetailsModel
                {
                    PurchaseRequestId = h.Field<int>("PurchaseRequestId"),
                    PurchaseRequestNo = h.Field<string>("PurchaseRequestNo") ?? string.Empty,
                    BranchId = h.Field<int>("BranchId"),
                    FromDeptId = h.Field<int>("FromDeptId"),
                    ToDeptId = h.Field<int>("ToDeptId"),
                    CategoryId = h.Field<int>("CategoryId"),
                    FromDeptName = h.Field<string>("FromDeptName") ?? string.Empty,
                    ToDeptName = h.Field<string>("ToDeptName") ?? string.Empty,
                    StoreType = h.Field<string>("StoreType") ?? string.Empty,
                    CreatedOn = h.Field<string>("CreatedOn"),
                    CreatedBy = h.Field<string>("CreatedBy"),
                    Remarks = h.Field<string>("Remarks"),
                    Status = h.Field<string>("Status") ?? string.Empty,
                    StatusId = h.Field<int>("StatusId"),
                    IsCancel = h.Field<int>("IsCancel"),
                    ApprovalRemarks = h.Field<string>("ApprovalRemarks") ?? string.Empty,
                    TotalAmount = ToDecimal(h["TotalAmount"]),
                    ApprovedAmount = ToDecimal(h["ApprovedAmount"]),
                    ApprovalAmount = ToDecimal(h["ApprovalAmount"]),
                    AmountUpTo = h["AmountUpTo"] == DBNull.Value ? null : ToDecimal(h["AmountUpTo"]),
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
                {
                    model.Items = ds.Tables[1].AsEnumerable().Select(r => new PurchaseRequestItemModel
                    {
                        PurchaseRequestItemId = r.Field<int>("PurchaseRequestItemId"),
                        ItemId = r.Field<int>("ItemId"),
                        ItemName = r.Field<string>("ItemName") ?? string.Empty,
                        Packing = r.Field<int>("Packing"),
                        PurchaseUnitName = r.Field<string>("PurchaseUnitName") ?? string.Empty,
                        RequestedQty = r.Field<int>("RequestedQty"),
                        ApprovedQty = r.Field<int>("ApprovedQty"),
                        Rate = ToDecimal(r["Rate"]),
                        Amount = ToDecimal(r["Amount"]),
                        ApprovedAmount = ToDecimal(r["ApprovedAmount"]),
                        CurrentStock = ToInt(r["CurrentStock"]),
                        CurrentStockAllDept = ToInt(r["CurrentStockAllDept"]),
                        MinLevel = ToInt(r["MinLevel"]),
                        ReorderLevel = ToInt(r["ReorderLevel"]),
                        MaxLevel = ToInt(r["MaxLevel"])
                    }).ToList();
                }

                return ServiceResult<PurchaseRequestDetailsModel>.Success(model, "Info", "Purchase Request retrieved successfully", 200);
            }
            catch (Exception ex)
            {
                return ServerError<PurchaseRequestDetailsModel>(ex);
            }
        }

        public ServiceResult<IEnumerable<ItemDepartmentStockModel>> GetItemStockByDepartment(int branchId, int itemId, AllGlobalValues globalValues)
        {
            try
            {
                var dt = _sqlHelper.GetDataTable("S_GetItemStockByDepartment", CommandType.StoredProcedure,
                    new { hospId = globalValues.hospId, branchId, itemId });
                var list = dt?.AsEnumerable().Select(r => new ItemDepartmentStockModel
                {
                    RoleId = r.Field<int>("RoleId"),
                    RoleName = r.Field<string>("RoleName") ?? string.Empty,
                    CurrentStock = ToInt(r["CurrentStock"])
                }).ToList() ?? new List<ItemDepartmentStockModel>();
                return ServiceResult<IEnumerable<ItemDepartmentStockModel>>.Success(list, "Info",
                    list.Count == 0 ? "No stock in any department" : $"{list.Count} department(s) hold stock", 200);
            }
            catch (Exception ex)
            {
                return ServerError<IEnumerable<ItemDepartmentStockModel>>(ex);
            }
        }

        #endregion

        #region Writes

        public ServiceResult<PurchaseRequestSaveResponse> SavePurchaseRequest(PurchaseRequestSaveRequest request, AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"SavePurchaseRequest called. PurchaseRequestId={request.PurchaseRequestId}, FromDept={request.FromDeptId}, Lines={request.Items?.Count ?? 0}");

                string storeType = PurchaseRequestStoreTypes.Keys.FirstOrDefault(k => k.Equals(request.StoreType?.Trim(), StringComparison.OrdinalIgnoreCase)) ?? string.Empty;
                var items = request.Items ?? new List<PurchaseRequestItemRequest>();

                string? err =
                    storeType == "" ? "Store Type must be Medical or General"
                    : request.ToDeptId == request.FromDeptId ? "To Department cannot be the same as From Department"
                    : items.Count == 0 ? "Please select at least one item"
                    : items.Count > MaxPurchaseRequestLines ? $"A Purchase Request can have at most {MaxPurchaseRequestLines} items"
                    : null;
                if (err != null) return Invalid<PurchaseRequestSaveResponse>(err);

                // From Department must be one of the user's own roles in this branch (OLD: the session role)
                var myRoles = GetStockReceivingStores(request.BranchId, globalValues).Data ?? Enumerable.Empty<ReceivingStoreModel>();
                if (!myRoles.Any(r => r.RoleId == request.FromDeptId))
                    return Invalid<PurchaseRequestSaveResponse>("You can raise a Purchase Request only for your own department");

                var departments = GetPurchaseRequestDepartments(globalValues).Data ?? Enumerable.Empty<ReceivingStoreModel>();
                if (!departments.Any(d => d.RoleId == request.ToDeptId))
                    return Invalid<PurchaseRequestSaveResponse>("To Department not found or inactive");

                // items exist, belong to the store type, are active; one line per item; qty > 0
                string ids = string.Join(",", items.Select(i => i.ItemId).Distinct());
                var master = LoadPurchaseRequestItems(request.BranchId, request.FromDeptId, PurchaseRequestStoreTypes[storeType], null, ids,
                        false, globalValues.hospId, MaxPurchaseRequestLines)
                    .ToDictionary(m => m.ItemId);

                var seen = new HashSet<int>();
                var lines = new List<object>();
                for (int n = 0; n < items.Count; n++)
                {
                    var it = items[n];
                    if (!master.TryGetValue(it.ItemId, out var m))
                        return Invalid<PurchaseRequestSaveResponse>($"Row {n + 1}: Item {it.ItemId} is not an active {storeType} store item");
                    if (!seen.Add(it.ItemId))
                        return Invalid<PurchaseRequestSaveResponse>($"Item already added : {m.ItemName}");
                    if (it.Qty <= 0)
                        return Invalid<PurchaseRequestSaveResponse>($"Requested QTY can not be Zero for Item : {m.ItemName}");
                    if (it.Qty > MaxPurchaseRequestQty)
                        return Invalid<PurchaseRequestSaveResponse>($"Requested QTY can be at most {MaxPurchaseRequestQty} for Item : {m.ItemName}");
                    lines.Add(new { it.ItemId, m.Packing, it.Qty });
                }

                int id = RunPurchaseRequestSp("IU_PurchaseRequest", new
                {
                    hospId = globalValues.hospId,
                    branchId = request.BranchId,
                    purchaseRequestId = request.PurchaseRequestId,
                    fromDeptId = request.FromDeptId,
                    toDeptId = request.ToDeptId,
                    categoryId = PurchaseRequestStoreTypes[storeType],
                    remarks = string.IsNullOrWhiteSpace(request.Remarks) ? null : request.Remarks.Trim(),
                    itemsJson = System.Text.Json.JsonSerializer.Serialize(lines),
                    userId = globalValues.userId,
                    ipAddress = globalValues.ipAddress
                });

                if (id <= 0)
                {
                    string msg = id switch
                    {
                        -2 => "Only a Draft Purchase Request can be edited",
                        -3 => "No Purchase Request number sequence is set up - add it in Sequence Master",
                        _ => "Purchase Request could not be saved"
                    };
                    var alert = _messageService.GetMessageAndTypeByAlertCode("OPERATION_FAILED");
                    return ServiceResult<PurchaseRequestSaveResponse>.Failure(alert.Type, msg, 200);
                }

                var ok = _messageService.GetMessageAndTypeByAlertCode(request.PurchaseRequestId > 0 ? "DATA_UPDATED_SUCCESSFULLY" : "DATA_SAVED_SUCCESSFULLY");
                return ServiceResult<PurchaseRequestSaveResponse>.Success(new PurchaseRequestSaveResponse { PurchaseRequestId = id }, ok.Type,
                    request.PurchaseRequestId > 0 ? "Purchase Request Updated Successfully" : "Purchase Request Saved Successfully", 200);
            }
            catch (Exception ex)
            {
                return ServerError<PurchaseRequestSaveResponse>(ex);
            }
        }

        public ServiceResult<PurchaseRequestSaveResponse> ChangePurchaseRequestStatus(string action, PurchaseRequestActionRequest request, AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"ChangePurchaseRequestStatus called. Action={action}, PurchaseRequestId={request.PurchaseRequestId}");
                bool cancel = action == "cancel";
                if (cancel && string.IsNullOrWhiteSpace(request.Reason))
                    return Invalid<PurchaseRequestSaveResponse>("Cancel reason is required");

                int result = cancel
                    ? RunPurchaseRequestSp("U_CancelPurchaseRequest", new
                    {
                        hospId = globalValues.hospId,
                        purchaseRequestId = request.PurchaseRequestId,
                        reason = request.Reason!.Trim(),
                        userId = globalValues.userId,
                        ipAddress = globalValues.ipAddress
                    })
                    : RunPurchaseRequestSp("U_SendPurchaseRequestForApproval", new
                    {
                        hospId = globalValues.hospId,
                        purchaseRequestId = request.PurchaseRequestId,
                        userId = globalValues.userId,
                        ipAddress = globalValues.ipAddress
                    });

                if (result <= 0)
                {
                    string msg = result switch
                    {
                        -5 => $"Approval Authority Not Found for amount ₹{(GetPurchaseRequestDetails(request.PurchaseRequestId, globalValues).Data?.TotalAmount ?? 0):N2} - add an \"Amount Up To\" slab for Purchase Request in Approval Authority Master",
                        _ => cancel ? "Only a Draft or Approval Pending Purchase Request can be cancelled"
                                    : "Only a Draft Purchase Request can be sent for approval"
                    };
                    var alert = _messageService.GetMessageAndTypeByAlertCode("OPERATION_FAILED");
                    return ServiceResult<PurchaseRequestSaveResponse>.Failure(alert.Type, msg, 200);
                }

                var ok = _messageService.GetMessageAndTypeByAlertCode("DATA_UPDATED_SUCCESSFULLY");
                return ServiceResult<PurchaseRequestSaveResponse>.Success(new PurchaseRequestSaveResponse { PurchaseRequestId = request.PurchaseRequestId },
                    ok.Type, cancel ? "Purchase Request cancelled successfully" : "Purchase Request sent for approval", 200);
            }
            catch (Exception ex)
            {
                return ServerError<PurchaseRequestSaveResponse>(ex);
            }
        }

        public ServiceResult<PurchaseRequestSaveResponse> ApprovePurchaseRequest(PurchaseRequestApproveRequest request, AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"ApprovePurchaseRequest called. PurchaseRequestId={request.PurchaseRequestId}, Flag={request.Flag}, Lines={request.Items?.Count ?? 0}");

                var items = request.Items ?? new List<PurchaseRequestApproveItem>();
                if (items.Select(i => i.PurchaseRequestItemId).Distinct().Count() != items.Count)
                    return Invalid<PurchaseRequestSaveResponse>("An item is listed more than once");
                if (items.Any(i => i.ApprovedQty < 0))
                    return Invalid<PurchaseRequestSaveResponse>("Approved QTY can not be negative");

                // OLD rule: approved qty <= requested qty - message names the item
                if (items.Count > 0)
                {
                    var details = GetPurchaseRequestDetails(request.PurchaseRequestId, globalValues).Data;
                    if (details == null) return Invalid<PurchaseRequestSaveResponse>("Purchase Request not found");
                    var lines = details.Items.ToDictionary(d => d.PurchaseRequestItemId);
                    foreach (var it in items)
                    {
                        if (!lines.TryGetValue(it.PurchaseRequestItemId, out var line))
                            return Invalid<PurchaseRequestSaveResponse>("An item does not belong to this Purchase Request");
                        if (it.ApprovedQty > line.RequestedQty)
                            return Invalid<PurchaseRequestSaveResponse>($"Approved QTY can not be greater than Requested QTY for Item : {line.ItemName}");
                    }
                }

                int result = RunPurchaseRequestSp("U_ApprovePurchaseRequest", new
                {
                    hospId = globalValues.hospId,
                    purchaseRequestId = request.PurchaseRequestId,
                    flag = request.Flag,
                    approvalRemarks = string.IsNullOrWhiteSpace(request.ApprovalRemarks) ? null : request.ApprovalRemarks.Trim(),
                    itemsJson = items.Count == 0 ? null : System.Text.Json.JsonSerializer.Serialize(items.Select(i => new { i.PurchaseRequestItemId, i.ApprovedQty })),
                    userId = globalValues.userId,
                    ipAddress = globalValues.ipAddress
                });

                if (result <= 0)
                {
                    string msg = result switch
                    {
                        -1 => "You are not an approver for the current approval level of this Purchase Request",
                        -2 => "Only an Approval Pending Purchase Request can be approved",
                        -6 => "Approved QTY must be between 0 and the Requested QTY",
                        _ => "Purchase Request could not be approved"
                    };
                    var alert = _messageService.GetMessageAndTypeByAlertCode("OPERATION_FAILED");
                    return ServiceResult<PurchaseRequestSaveResponse>.Failure(alert.Type, msg, 200);
                }

                var after = GetPurchaseRequestDetails(request.PurchaseRequestId, globalValues).Data;
                var ok = _messageService.GetMessageAndTypeByAlertCode("DATA_UPDATED_SUCCESSFULLY");
                return ServiceResult<PurchaseRequestSaveResponse>.Success(new PurchaseRequestSaveResponse { PurchaseRequestId = request.PurchaseRequestId },
                    ok.Type, after?.StatusId == 3 ? "Purchase Request approved" : $"Level {request.Flag} approval saved - sent to the next level", 200);
            }
            catch (Exception ex)
            {
                return ServerError<PurchaseRequestSaveResponse>(ex);
            }
        }

        /// <summary>Runs a write SP in its own transaction; the SP returns its result with SELECT @Result.</summary>
        private int RunPurchaseRequestSp(string sp, object parameters)
        {
            using var con = new SqlConnection(_configuration.GetConnectionString("ConnectionString"));
            con.Open();
            using SqlTransaction tnx = CustomSqlHelper.getSqlTransaction(con);
            object raw = _sqlHelper.DML(tnx, sp, CommandType.StoredProcedure, parameters, new { Result = 0 });
            int result = raw == null || raw == DBNull.Value ? 0 : Convert.ToInt32(raw);
            if (result > 0) tnx.Commit(); else tnx.Rollback();
            return result;
        }

        #endregion

        private static int ToInt(object value) => value == null || value == DBNull.Value ? 0 : Convert.ToInt32(value);

        private static int? ToNullableInt(object value) => value == null || value == DBNull.Value ? null : Convert.ToInt32(value);

        private static decimal ToDecimal(object value) => value == null || value == DBNull.Value ? 0m : Convert.ToDecimal(value);
    }
}
