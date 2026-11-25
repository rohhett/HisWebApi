using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using HISWEBAPI.Data.Helpers;
using HISWEBAPI.Domain;
using HISWEBAPI.DTO;
using HISWEBAPI.Models;
using Microsoft.Data.SqlClient;

namespace HISWEBAPI.Repositories.Implementations
{
    /// <summary>
    /// Patient Medicine Issue - OPD / walk-in counter sale and IPD issue on one screen (2026-11-23).
    /// Rules ported from OLD Store/PatientMedicineIssue (Store.saveStoreBilling) and the IPD indent procs
    /// (U_UpdateIndentItem, U_rejectIndentItem, U_UpdateIndent, U_rejectIndent); screen / workflow from the MediStock design.
    /// Every amount is recomputed here from the stock lots; client totals are not trusted.
    /// SPs: script Database/2026-11-23_PatientMedicineIssue.sql (+ existing I_PatientVisitDetails, I_PatientBillDetails,
    /// I_FinancialTransactions, I_Receipts, I_ReceiptsPaymentModeDetails, S_GetCorporatePaymentModes).
    /// </summary>
    public partial class StoreRepository
    {
        private const int MaxSaleLines = 200;              // one bill; OLD sent 25-line chunks of one bill (no hard limit)
        private const int PayModeTypeBank = 2;             // OLD: bank mandatory for type 2
        private const int PayModeTypeCredit = 4;           // OLD: credit modes are not written to ReceiptsPaymentModeDetails

        #region Reads

        public ServiceResult<IEnumerable<SalePatientModel>> SearchPatientForSale(int branchId, int patientTypeId, string? search, AllGlobalValues globalValues)
        {
            try
            {
                var dt = _sqlHelper.GetDataTable("S_SearchPatientForSale", CommandType.StoredProcedure, new
                {
                    hospId = globalValues.hospId,
                    branchId,
                    patientTypeId,
                    search = string.IsNullOrWhiteSpace(search) ? null : search.Trim(),
                    top = 30
                });
                var list = dt?.AsEnumerable().Where(r => dt.Columns.Contains("UHID")).Select(r => new SalePatientModel
                {
                    PatientId = r.Field<int>("PatientId"),
                    Uhid = r.Field<string>("UHID") ?? string.Empty,
                    PatientName = r.Field<string>("PatientName") ?? string.Empty,
                    Age = r.Field<string>("Age") ?? string.Empty,
                    Gender = r.Field<string>("Gender") ?? string.Empty,
                    ContactNumber = r.Field<string>("ContactNumber") ?? string.Empty,
                    VisitId = r.Field<int?>("VisitId"),
                    IpdNo = r.Field<string>("IpdNo") ?? string.Empty,
                    AdmissionDate = r.Field<DateTime?>("AdmissionDate"),
                    CorporateId = r.Field<int?>("CorporateId") ?? 1,
                    CorporateName = r.Field<string>("CorporateName") ?? string.Empty,
                    DoctorId = r.Field<int?>("DoctorId"),
                    DoctorName = r.Field<string>("DoctorName") ?? string.Empty,
                    WardBed = r.Field<string>("WardBed") ?? string.Empty,
                    IsGeneralPatient = r.Field<int>("IsGeneralPatient")
                }).ToList() ?? new List<SalePatientModel>();
                return ServiceResult<IEnumerable<SalePatientModel>>.Success(list, "Info", $"{list.Count} patient(s) found", 200);
            }
            catch (Exception ex)
            {
                return ServerError<IEnumerable<SalePatientModel>>(ex);
            }
        }

        public ServiceResult<IEnumerable<SaleItemSearchModel>> SearchSaleItem(int branchId, int roleId, string search, int searchType, AllGlobalValues globalValues)
        {
            try
            {
                var dt = _sqlHelper.GetDataTable("S_SearchItemForSale", CommandType.StoredProcedure, new
                {
                    hospId = globalValues.hospId,
                    branchId,
                    roleId,
                    search = search.Trim(),
                    searchType,
                    top = 30
                });
                var list = dt?.AsEnumerable().Select(r => new SaleItemSearchModel
                {
                    ItemId = r.Field<int>("ItemId"),
                    ItemName = r.Field<string>("ItemName") ?? string.Empty,
                    SaltName = r.Field<string>("SaltName") ?? string.Empty,
                    ManufacturerName = r.Field<string>("ManufacturerName") ?? string.Empty,
                    SaleUnitName = r.Field<string>("SaleUnitName") ?? string.Empty,
                    DrugCategory = r.Field<string>("DrugCategory") ?? string.Empty,
                    IsNarcotic = ToInt(r["IsNarcotic"]),
                    ColorPicker = r.Field<string>("ColorPicker") ?? string.Empty,
                    HoldStatusId = ToInt(r["HoldStatusId"]),
                    HoldStatus = r.Field<string>("HoldStatus") ?? string.Empty,
                    HoldRemarks = r.Field<string>("HoldRemarks") ?? string.Empty,
                    CorporateIds = r.Field<string>("CorporateIds") ?? string.Empty,
                    TotalStock = ToInt(r["TotalStock"]),
                    BatchCount = ToInt(r["BatchCount"]),
                    StockId = r.Field<int>("StockId"),
                    BatchNo = r.Field<string>("BatchNo") ?? string.Empty,
                    ExpiryDate = r.Field<DateTime?>("ExpiryDate"),
                    Mrp = r.Field<decimal?>("MRP") ?? 0,
                    UnitCost = r.Field<decimal?>("UnitCost") ?? 0,
                    GstPer = r.Field<decimal?>("GSTPercentage") ?? 0
                }).ToList() ?? new List<SaleItemSearchModel>();
                return ServiceResult<IEnumerable<SaleItemSearchModel>>.Success(list, "Info", $"{list.Count} item(s) found", 200);
            }
            catch (Exception ex)
            {
                return ServerError<IEnumerable<SaleItemSearchModel>>(ex);
            }
        }

        public ServiceResult<IEnumerable<SaleBatchModel>> GetSaleItemBatches(int branchId, int roleId, string itemIds, AllGlobalValues globalValues)
        {
            try
            {
                var list = LoadSaleBatches(branchId, roleId, itemIds, null, globalValues.hospId);
                return ServiceResult<IEnumerable<SaleBatchModel>>.Success(list, "Info", $"{list.Count} batch(es) found", 200);
            }
            catch (Exception ex)
            {
                return ServerError<IEnumerable<SaleBatchModel>>(ex);
            }
        }

        private List<SaleBatchModel> LoadSaleBatches(int branchId, int roleId, string? itemIds, string? stockIds, int hospId)
        {
            var dt = _sqlHelper.GetDataTable("S_GetSaleItemBatches", CommandType.StoredProcedure,
                new { hospId, branchId, roleId, itemIds, stockIds });
            return dt?.AsEnumerable().Select(r => new SaleBatchModel
            {
                StockId = r.Field<int>("StockId"),
                ItemId = r.Field<int>("ItemId"),
                ItemName = r.Field<string>("ItemName") ?? string.Empty,
                ItemCode = r.Field<string>("ItemCode") ?? string.Empty,
                SubSubCategoryId = r.Field<int?>("SubSubCategoryId"),
                BatchNo = r.Field<string>("BatchNo") ?? string.Empty,
                ExpiryDate = r.Field<DateTime?>("ExpiryDate"),
                CurrentStock = r.Field<int?>("CurrentStock") ?? 0,
                Mrp = r.Field<decimal?>("MRP") ?? 0,
                UnitCost = r.Field<decimal?>("UnitCost") ?? 0,
                GstPer = r.Field<decimal?>("GSTPercentage") ?? 0,
                IsActive = r.Field<int?>("IsActive") ?? 0,
                IsNarcotic = ToInt(r["IsNarcotic"]),
                CorporateIds = r.Field<string>("CorporateIds") ?? string.Empty,
                HoldStatusId = ToInt(r["HoldStatusId"]),
                HoldStatus = r.Field<string>("HoldStatus") ?? string.Empty,
                IsExpired = r.Field<int>("IsExpired")
            }).ToList() ?? new List<SaleBatchModel>();
        }

        public ServiceResult<PharmacyUserRightsModel> GetPharmacyUserRights(int branchId, int roleId, AllGlobalValues globalValues)
        {
            try
            {
                return ServiceResult<PharmacyUserRightsModel>.Success(LoadPharmacyRights(branchId, roleId, globalValues.userId), "Info", "Rights retrieved", 200);
            }
            catch (Exception ex)
            {
                return ServerError<PharmacyUserRightsModel>(ex);
            }
        }

        private PharmacyUserRightsModel LoadPharmacyRights(int branchId, int roleId, int userId)
        {
            var dt = _sqlHelper.GetDataTable("S_GetPharmacyUserRights", CommandType.StoredProcedure, new { branchId, userId, roleId });
            var r = dt?.Rows.Count > 0 ? dt.Rows[0] : null;
            return new PharmacyUserRightsModel
            {
                CanSellNarcoDrugs = r == null ? 0 : ToInt(r["CanSellNarcoDrugs"]),
                MaxDiscountPer = r == null ? 100m : Convert.ToDecimal(r["MaxDiscountPer"], CultureInfo.InvariantCulture)
            };
        }

        public ServiceResult<IEnumerable<PharmacySaleListModel>> GetPharmacySaleList(
            int branchId, int? roleId, DateTime fromDate, DateTime toDate, int? patientTypeId, string? search, AllGlobalValues globalValues)
        {
            try
            {
                var dt = _sqlHelper.GetDataTable("S_GetPharmacySaleList", CommandType.StoredProcedure, new
                {
                    hospId = globalValues.hospId,
                    branchId,
                    roleId = roleId > 0 ? roleId : null,
                    fromDate = fromDate.Date,
                    toDate = toDate.Date,
                    patientTypeId = patientTypeId > 0 ? patientTypeId : null,
                    search = string.IsNullOrWhiteSpace(search) ? null : search.Trim()
                });
                var list = dt?.AsEnumerable().Select(r => MapSaleListRow(r, new PharmacySaleListModel())).ToList()
                    ?? new List<PharmacySaleListModel>();
                return ServiceResult<IEnumerable<PharmacySaleListModel>>.Success(list, "Info", $"{list.Count} bill(s) retrieved successfully", 200);
            }
            catch (Exception ex)
            {
                return ServerError<IEnumerable<PharmacySaleListModel>>(ex);
            }
        }

        private static T MapSaleListRow<T>(DataRow r, T m) where T : PharmacySaleListModel
        {
            m.StoreBillId = r.Field<int>("StoreBillId");
            m.BillNo = r.Field<string>("BillNo") ?? string.Empty;
            m.PrintCount = ToInt(r["PrintCount"]);
            m.BillDate = r.Field<DateTime>("BillDate");
            m.PatientTypeId = r.Field<int>("PatientTypeId");
            m.PatientType = r.Field<string>("PatientType") ?? string.Empty;
            m.IsWalkIn = r.Field<int>("IsWalkIn");
            m.Source = r.Table.Columns.Contains("Source") ? r.Field<string>("Source") ?? string.Empty : string.Empty;
            m.PatientId = r.Field<int>("PatientId");
            m.Uhid = r.Field<string>("UHID") ?? string.Empty;
            m.PatientName = r.Field<string>("PatientName") ?? string.Empty;
            m.VisitNo = r.Field<string>("VisitNo") ?? string.Empty;
            m.StoreName = r.Field<string>("StoreName") ?? string.Empty;
            m.TotalItems = r.Field<int>("TotalItems");
            m.TotalUnits = r.Field<int>("TotalUnits");
            m.NetAmount = r.Field<decimal>("NetAmount");
            m.PaidAmount = r.Field<decimal>("PaidAmount");
            m.BalanceAmount = r.Field<decimal>("BalanceAmount");
            m.IsCancel = r.Field<int>("IsCancel");
            m.ReceiptNo = r.Field<string>("ReceiptNo") ?? string.Empty;
            m.CreatedByName = r.Field<string>("CreatedByName") ?? string.Empty;
            return m;
        }

        public ServiceResult<PharmacySaleDetailsModel> GetPharmacySaleDetails(int storeBillId, AllGlobalValues globalValues)
        {
            try
            {
                var ds = _sqlHelper.GetDataSet("S_GetPharmacySaleDetails", CommandType.StoredProcedure,
                    new { hospId = globalValues.hospId, storeBillId });
                if (ds == null || ds.Tables.Count == 0 || ds.Tables[0].Rows.Count == 0)
                {
                    var nf = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    return ServiceResult<PharmacySaleDetailsModel>.Failure(nf.Type, nf.Message, 404);
                }
                var h = ds.Tables[0].Rows[0];
                var m = MapSaleListRow(h, new PharmacySaleDetailsModel());
                int src = h.Field<int>("SourceTypeId");
                m.Source = src switch { 1 => "EMR", 2 => "Indent", 3 => "Discharge", _ => "Counter" };
                m.Age = h.Field<string>("Age") ?? string.Empty;
                m.Gender = h.Field<string>("Gender") ?? string.Empty;
                m.ContactNumber = h.Field<string>("ContactNumber") ?? string.Empty;
                m.CorporateName = h.Field<string>("CorporateName") ?? string.Empty;
                m.DoctorName = h.Field<string>("DoctorName") ?? string.Empty;
                m.DoctorRef = h.Field<string>("DoctorRef") ?? string.Empty;
                m.Witness = h.Field<string>("Witness") ?? string.Empty;
                m.SaleTypeId = h.Field<int>("SaleTypeId");
                m.GrossAmount = h.Field<decimal>("GrossAmount");
                m.ItemDiscountAmount = h.Field<decimal>("ItemDiscountAmount");
                m.BillDiscountPer = h.Field<decimal>("BillDiscountPer");
                m.BillDiscountAmount = h.Field<decimal>("BillDiscountAmount");
                m.TaxAmount = h.Field<decimal>("TaxAmount");
                m.RoundOff = h.Field<decimal>("RoundOff");
                m.WardBed = h.Field<string>("WardBed") ?? string.Empty;
                m.BranchName = h.Field<string>("BranchName") ?? string.Empty;
                m.BranchAddress = h.Field<string>("BranchAddress") ?? string.Empty;
                m.BranchPhone = h.Field<string>("BranchPhone") ?? string.Empty;
                m.BranchEmail = h.Field<string>("BranchEmail") ?? string.Empty;
                m.LastPrintedByName = h.Field<string>("LastPrintedByName") ?? string.Empty;
                m.LastPrintedOn = h.Field<DateTime?>("LastPrintedOn");
                if (ds.Tables.Count > 1)
                    m.Lines = ds.Tables[1].AsEnumerable().Select(r => new PharmacySaleLineModel
                    {
                        ItemId = r.Field<int>("ItemId"),
                        ItemName = r.Field<string>("ItemName") ?? string.Empty,
                        BatchNo = r.Field<string>("BatchNo") ?? string.Empty,
                        ExpiryDate = r.Field<DateTime?>("ExpiryDate"),
                        Qty = r.Field<decimal?>("Qty") ?? 0,
                        Rate = r.Field<decimal?>("Rate") ?? 0,
                        GrossAmt = r.Field<decimal?>("GrossAmt") ?? 0,
                        DiscPer = r.Field<decimal?>("DiscPer") ?? 0,
                        DiscAmt = r.Field<decimal?>("DiscAmt") ?? 0,
                        GstPer = r.Field<decimal?>("GstPer") ?? 0,
                        TaxAmt = r.Field<decimal?>("TaxAmt") ?? 0,
                        NetAmt = r.Field<decimal?>("NetAmt") ?? 0,
                        IsNarcotic = ToInt(r["IsNarcotic"])
                    }).ToList();
                if (ds.Tables.Count > 2)
                    m.Payments = ds.Tables[2].AsEnumerable().Select(r => new PharmacySalePaymentModel
                    {
                        PaymentModeId = r.Field<int>("PaymentModeId"),
                        PaymentMode = r.Field<string>("PaymentMode") ?? string.Empty,
                        Amount = r.Field<decimal?>("Amount") ?? 0,
                        ReferenceNo = r.Field<string>("ReferenceNo") ?? string.Empty
                    }).ToList();

                return ServiceResult<PharmacySaleDetailsModel>.Success(m, "Info", "Bill retrieved successfully", 200);
            }
            catch (Exception ex)
            {
                return ServerError<PharmacySaleDetailsModel>(ex);
            }
        }

        public ServiceResult<PharmacyRequestCountsModel> GetPharmacyRequestCounts(int branchId, int roleId, AllGlobalValues globalValues)
        {
            try
            {
                var dt = _sqlHelper.GetDataTable("S_GetPharmacyRequestCounts", CommandType.StoredProcedure, new
                {
                    hospId = globalValues.hospId,
                    branchId,
                    roleId,
                    emrFromDate = DateTime.Today.AddDays(-EmrRequestDays)
                });
                var r = dt?.Rows.Count > 0 ? dt.Rows[0] : null;
                var m = new PharmacyRequestCountsModel
                {
                    EmrCount = r == null ? 0 : ToInt(r["EmrCount"]),
                    IndentCount = r == null ? 0 : ToInt(r["IndentCount"]),
                    IndentUrgentCount = r == null ? 0 : ToInt(r["IndentUrgentCount"]),
                    DischargeCount = r == null ? 0 : ToInt(r["DischargeCount"])
                };
                return ServiceResult<PharmacyRequestCountsModel>.Success(m, "Info", "Request counts retrieved", 200);
            }
            catch (Exception ex)
            {
                return ServerError<PharmacyRequestCountsModel>(ex);
            }
        }

        /// <summary>EMR prescriptions of the last N days are shown as requests (Recommended; OLD searched a chosen date range).</summary>
        private const int EmrRequestDays = 7;

        /// <summary>type = emr | indent | discharge ; statusId 1 pending / open, 2 dispensed / closed, null all.</summary>
        public ServiceResult<IEnumerable<PharmacyRequestModel>> GetPharmacyRequests(string type, int branchId, int roleId, int? statusId, string? search, AllGlobalValues globalValues)
        {
            try
            {
                string? q = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
                List<PharmacyRequestModel> list;
                if (type == "indent")
                {
                    var dt = _sqlHelper.GetDataTable("S_GetPendingPatientIndents", CommandType.StoredProcedure,
                        new { hospId = globalValues.hospId, branchId, roleId, statusId, search = q });
                    list = dt?.AsEnumerable().Select(r => new PharmacyRequestModel
                    {
                        RequestId = r.Field<int>("IndentId"),
                        RefNo = "IND-" + r.Field<int>("IndentNo"),
                        Priority = r.Field<string>("Priority") ?? string.Empty,
                        StatusId = r.Field<int>("StatusId"),
                        Status = r.Field<string>("Status") ?? string.Empty,
                        RequestedOn = r.Field<DateTime>("CreatedOn"),
                        PatientId = r.Field<int>("PatientId"),
                        Uhid = r.Field<string>("UHID") ?? string.Empty,
                        PatientName = r.Field<string>("PatientName") ?? string.Empty,
                        Age = r.Field<string>("Age") ?? string.Empty,
                        Gender = r.Field<string>("Gender") ?? string.Empty,
                        VisitId = r.Field<int>("VisitId"),
                        IpdNo = r.Field<string>("IpdNo") ?? string.Empty,
                        WardBed = r.Field<string>("WardBed") ?? string.Empty,
                        DoctorName = r.Field<string>("DoctorName") ?? string.Empty,
                        RaisedBy = r.Field<string>("RaisedBy") ?? string.Empty,
                        Department = r.Field<string>("Department") ?? string.Empty,
                        ItemCount = ToInt(r["ItemCount"]),
                        IsPartial = ToInt(r["IsPartial"]),
                        Remarks = r.Field<string>("Remarks") ?? string.Empty
                    }).ToList() ?? new List<PharmacyRequestModel>();
                }
                else if (type == "discharge")
                {
                    // one request per IPD visit: take-home medicines of its discharge summary (2026-11-24)
                    var dt = _sqlHelper.GetDataTable("S_GetDischargeMedicineRequests", CommandType.StoredProcedure,
                        new { hospId = globalValues.hospId, branchId, search = q, statusId });
                    list = dt?.AsEnumerable().Select(r =>
                    {
                        int? dsId = r.Field<int?>("DSId");
                        string ipdNo = r.Field<string>("IpdNo") ?? string.Empty;
                        return new PharmacyRequestModel
                        {
                            RequestId = r.Field<int>("VisitId"),
                            RefNo = dsId > 0 ? $"DS-{dsId}" : ipdNo,
                            StatusId = r.Field<int>("StatusId"),
                            Status = r.Field<string>("Status") ?? string.Empty,
                            RequestedOn = r.Field<DateTime?>("WrittenOn"),
                            PatientId = r.Field<int>("PatientId"),
                            Uhid = r.Field<string>("UHID") ?? string.Empty,
                            PatientName = r.Field<string>("PatientName") ?? string.Empty,
                            Age = r.Field<string>("Age") ?? string.Empty,
                            Gender = r.Field<string>("Gender") ?? string.Empty,
                            VisitId = r.Field<int>("VisitId"),
                            IpdNo = ipdNo,
                            WardBed = r.Field<string>("WardBed") ?? string.Empty,
                            DoctorId = r.Field<int?>("DoctorId"),
                            DoctorName = r.Field<string>("DoctorName") ?? string.Empty,
                            RaisedBy = r.Field<string>("ApprovedBy") ?? string.Empty,
                            Department = r.Field<string>("Department") ?? string.Empty,
                            BillNo = r.Field<string>("BillNo") ?? string.Empty,
                            ItemCount = ToInt(r["ItemCount"]),
                            IsDischarged = ToInt(r["IsDischarged"])
                        };
                    }).ToList() ?? new List<PharmacyRequestModel>();
                }
                else
                {
                    var dt = _sqlHelper.GetDataTable("S_GetEmrMedicineRequests", CommandType.StoredProcedure, new
                    {
                        hospId = globalValues.hospId,
                        branchId,
                        fromDate = DateTime.Today.AddDays(-EmrRequestDays),
                        toDate = DateTime.Today,
                        search = q,
                        statusId
                    });
                    list = dt?.AsEnumerable().Select(r => new PharmacyRequestModel
                    {
                        RequestId = r.Field<int>("VisitId"),
                        RefNo = r.Field<string>("VisitNo") ?? string.Empty,
                        StatusId = r.Field<int>("StatusId"),
                        Status = r.Field<string>("Status") ?? string.Empty,
                        RequestedOn = r.Field<DateTime?>("PrescribedOn"),
                        PatientId = r.Field<int>("PatientId"),
                        Uhid = r.Field<string>("UHID") ?? string.Empty,
                        PatientName = r.Field<string>("PatientName") ?? string.Empty,
                        Age = r.Field<string>("Age") ?? string.Empty,
                        Gender = r.Field<string>("Gender") ?? string.Empty,
                        DoctorId = r.Field<int?>("DoctorId"),
                        DoctorName = r.Field<string>("DoctorName") ?? string.Empty,
                        BillNo = r.Field<string>("BillNo") ?? string.Empty,
                        Department = r.Field<string>("Department") ?? string.Empty,
                        ItemCount = ToInt(r["ItemCount"])
                    }).ToList() ?? new List<PharmacyRequestModel>();
                }
                return ServiceResult<IEnumerable<PharmacyRequestModel>>.Success(list, "Info", $"{list.Count} request(s) found", 200);
            }
            catch (Exception ex)
            {
                return ServerError<IEnumerable<PharmacyRequestModel>>(ex);
            }
        }

        public ServiceResult<IEnumerable<PharmacyRequestItemModel>> GetPharmacyRequestItems(string type, int branchId, int roleId, int requestId, AllGlobalValues globalValues)
        {
            try
            {
                List<PharmacyRequestItemModel> list;
                if (type == "indent")
                {
                    var dt = _sqlHelper.GetDataTable("S_GetPatientIndentItems", CommandType.StoredProcedure,
                        new { hospId = globalValues.hospId, branchId, roleId, indentId = requestId });
                    list = dt?.AsEnumerable().Select(r => new PharmacyRequestItemModel
                    {
                        IndentItemId = r.Field<int>("IndentItemId"),
                        ItemId = r.Field<int>("ItemId"),
                        ItemName = r.Field<string>("ItemName") ?? string.Empty,
                        SaltName = r.Field<string>("SaltName") ?? string.Empty,
                        DosageForm = r.Field<string>("DosageForm") ?? string.Empty,
                        Instruction = string.Join(" · ", new[] { r.Field<string>("Dose"), r.Field<string>("Times"), r.Field<string>("Duration"), r.Field<string>("Route") }
                                          .Where(s => !string.IsNullOrWhiteSpace(s))),
                        RequestedQty = r.Field<int>("RequestedQTY"),
                        IssuedQty = r.Field<int>("IssuedQTY"),
                        RejectedQty = r.Field<int>("RejectedQTY"),
                        PendingQty = r.Field<int>("PendingQty"),
                        InStock = ToInt(r["InStock"])
                    }).ToList() ?? new List<PharmacyRequestItemModel>();
                }
                else if (type == "discharge")
                {
                    var dt = _sqlHelper.GetDataTable("S_GetDischargeMedicineItems", CommandType.StoredProcedure,
                        new { hospId = globalValues.hospId, branchId, roleId, visitId = requestId });
                    list = dt?.AsEnumerable().Select(r =>
                    {
                        int qty = ToInt(r["Qty"]);
                        return new PharmacyRequestItemModel
                        {
                            ItemId = r.Field<int>("ItemId"),
                            ItemName = r.Field<string>("ItemName") ?? string.Empty,
                            SaltName = r.Field<string>("SaltName") ?? string.Empty,
                            DosageForm = r.Field<string>("DosageForm") ?? string.Empty,
                            Instruction = string.Join(" · ", new[] { r.Field<string>("Dose"), r.Field<string>("Frequency"), r.Field<string>("Duration"),
                                                                    r.Field<string>("Route"), r.Field<string>("Meal"), r.Field<string>("Remarks") }
                                              .Where(s => !string.IsNullOrWhiteSpace(s))),
                            RequestedQty = qty,
                            PendingQty = qty,
                            InStock = ToInt(r["InStock"])
                        };
                    }).ToList() ?? new List<PharmacyRequestItemModel>();
                }
                else
                {
                    var dt = _sqlHelper.GetDataTable("S_GetEmrMedicineRequestItems", CommandType.StoredProcedure,
                        new { hospId = globalValues.hospId, branchId, roleId, visitId = requestId });
                    list = dt?.AsEnumerable().Select(r =>
                    {
                        string dose = r.Field<string>("DoseQty") ?? "", unit = r.Field<string>("DoseUnit") ?? "",
                               freq = r.Field<string>("Frequency") ?? "", dur = r.Field<string>("DurationValue") ?? "",
                               durUnit = r.Field<string>("DurationUnit") ?? "", route = r.Field<string>("Route") ?? "";
                        int req = EmrRequiredQty(dose, freq, dur, durUnit);
                        return new PharmacyRequestItemModel
                        {
                            ItemId = r.Field<int?>("ItemId"),
                            ItemName = r.Field<string>("MedicineName") ?? string.Empty,
                            SaltName = r.Field<string>("SaltName") ?? string.Empty,
                            DosageForm = r.Field<string>("DosageForm") ?? string.Empty,
                            Instruction = string.Join(" · ", new[] { $"{dose} {unit}".Trim(), freq, $"{dur} {durUnit}".Trim(), route }
                                              .Where(s => !string.IsNullOrWhiteSpace(s))),
                            RequestedQty = req,
                            PendingQty = req,
                            InStock = ToInt(r["InStock"])
                        };
                    }).ToList() ?? new List<PharmacyRequestItemModel>();
                }
                return ServiceResult<IEnumerable<PharmacyRequestItemModel>>.Success(list, "Info", $"{list.Count} item(s) found", 200);
            }
            catch (Exception ex)
            {
                return ServerError<IEnumerable<PharmacyRequestItemModel>>(ex);
            }
        }

        /// <summary>
        /// Required units from the EMR dose (Recommended - OLD left the quantity to the pharmacist):
        /// dose qty × doses per day (sum of a "1-0-1" pattern, else 1) × days (weeks ×7, months ×30); minimum 1.
        /// </summary>
        private static int EmrRequiredQty(string dose, string freq, string duration, string durationUnit)
        {
            decimal d = decimal.TryParse(dose, NumberStyles.Number, CultureInfo.InvariantCulture, out var x) && x > 0 ? x : 1;
            var parts = freq.Split('-', StringSplitOptions.TrimEntries);
            decimal perDay = parts.Length > 1 && parts.All(p => decimal.TryParse(p, NumberStyles.Number, CultureInfo.InvariantCulture, out _))
                ? parts.Sum(p => decimal.Parse(p, CultureInfo.InvariantCulture)) : 1;
            decimal days = decimal.TryParse(duration, NumberStyles.Number, CultureInfo.InvariantCulture, out var y) && y > 0 ? y : 1;
            string u = durationUnit.Trim().ToLowerInvariant();
            if (u.StartsWith("week")) days *= 7; else if (u.StartsWith("month")) days *= 30;
            int qty = (int)Math.Ceiling(d * Math.Max(perDay, 1) * days);
            return Math.Clamp(qty, 1, 9999);
        }

        #endregion

        #region Save

        /// <summary>Line amounts - OLD addNewRow / $updateAmount: GST inclusive, line disc % else bill disc %.</summary>
        private sealed class SaleLineCalc
        {
            public decimal Rate, Gross, DiscPer, DiscAmt, Net, Tax;
            public bool BillDiscount;
        }

        private static SaleLineCalc CalcSaleLine(decimal rate, int qty, decimal lineDiscPer, decimal billDiscPer, decimal gstPer)
        {
            rate = Math.Round(rate, 2, MidpointRounding.AwayFromZero);
            decimal gross = Math.Round(rate * qty, 2, MidpointRounding.AwayFromZero);
            bool billDisc = lineDiscPer <= 0 && billDiscPer > 0;
            decimal discPer = lineDiscPer > 0 ? lineDiscPer : billDiscPer;
            decimal disc = Math.Round(gross * discPer / 100m, 2, MidpointRounding.AwayFromZero);
            decimal net = gross - disc;
            decimal tax = Math.Round(net - net * 100m / (100m + gstPer), 2, MidpointRounding.AwayFromZero);   // OLD: tax inclusive
            return new SaleLineCalc { Rate = rate, Gross = gross, DiscPer = discPer, DiscAmt = disc, Net = net, Tax = tax, BillDiscount = billDisc };
        }

        public ServiceResult<PharmacySaleSaveResponse> SavePharmacySale(PharmacySaleSaveRequest request, AllGlobalValues globalValues)
        {
            try
            {
                var items = request.Items ?? new List<PharmacySaleItemRequest>();
                var payments = (request.Payments ?? new List<PharmacySalePaymentRequest>()).Where(p => p.Amount > 0).ToList();
                var rejects = request.Rejects ?? new List<PharmacySaleRejectRequest>();
                bool isIpd = request.PatientTypeId == 2;
                bool isWalkIn = !isIpd && request.PatientId <= 0;
                _log.Info($"SavePharmacySale called. Type={request.PatientTypeId}, PatientId={request.PatientId}, VisitId={request.VisitId}, Lines={items.Count}, Source={request.SourceTypeId}");

                // ---- header (OLD client rules + design rules) ----
                string? err =
                    items.Count == 0 ? "Please add atleast one service to be billed"   // to refuse a whole indent use rejectPatientIndent
                    : items.Count > MaxSaleLines ? $"A bill can have at most {MaxSaleLines} items"
                    : isIpd && (request.PatientId <= 0 || request.VisitId <= 0) ? "Please select an admitted (IPD) patient"
                    : isWalkIn && string.IsNullOrWhiteSpace(request.WalkInName) ? "Patient Name is required"
                    : isWalkIn && request.AgeYears == 0 && request.AgeMonths == 0 && request.AgeDays == 0 ? "Please Enter a Valid Age"
                    : isWalkIn && string.IsNullOrWhiteSpace(request.Gender) ? "Gender is required"
                    : isWalkIn && !string.IsNullOrWhiteSpace(request.Mobile) && !System.Text.RegularExpressions.Regex.IsMatch(request.Mobile.Trim(), "^[0-9]{10}$")
                        ? "Mobile must be 10 digits"
                    : request.SourceTypeId == 2 && (!isIpd || !(request.SourceId > 0)) ? "A ward indent can only be issued to its IPD patient"
                    : request.SourceTypeId == 1 && !(request.SourceId > 0) ? "EMR request is missing"
                    : request.SourceTypeId == 3 && (!isIpd || request.SourceId != request.VisitId) ? "Discharge medicines can only be issued to their IPD patient"
                    : request.SourceTypeId != 2 && rejects.Count > 0 ? "Items can be rejected only against a ward indent"
                    : isIpd && payments.Count > 0 ? "IPD issue is posted to the IP bill; no payment is collected here"
                    : items.GroupBy(i => i.StockId).Any(g => g.Count() > 1) ? "Item Already Added"
                    : rejects.Any(r => string.IsNullOrWhiteSpace(r.Reason)) ? "Rejected Reason is required"
                    : null;
                if (err != null) return Invalid<PharmacySaleSaveResponse>(err);

                // ---- patient / visit ----
                int corporateId = 1, insuranceCompanyId = 0, patientId = request.PatientId, visitId = 0;
                int? doctorId = request.DoctorId > 0 ? request.DoctorId : null;
                string uhid = string.Empty, currentAge = string.Empty;
                if (!isWalkIn)
                {
                    var pdt = _sqlHelper.GetDataTable("S_GetPatientForSale", CommandType.StoredProcedure,
                        new { hospId = globalValues.hospId, branchId = request.BranchId, patientId = request.PatientId, visitId = isIpd ? request.VisitId : 0 });
                    if (pdt == null || pdt.Rows.Count == 0) return Invalid<PharmacySaleSaveResponse>("Patient information not found");
                    var p = pdt.Rows[0];
                    if (isIpd && ToInt(p["IsAdmitted"]) != 1) return Invalid<PharmacySaleSaveResponse>("The patient is not admitted (or already discharged) in this branch");
                    uhid = p.Field<string>("UHID") ?? string.Empty;
                    currentAge = p.Field<string>("Age") ?? string.Empty;
                    insuranceCompanyId = ToInt(p["InsuranceCompanyId"]);
                    corporateId = isIpd ? (p.Field<int?>("VisitCorporateId") ?? 1) : ToInt(p["PatientCorporateId"]);
                    if (isIpd) { visitId = request.VisitId; doctorId ??= p.Field<int?>("VisitDoctorId"); }
                }
                else
                {
                    currentAge = $"{request.AgeYears}Y {request.AgeMonths}M {request.AgeDays}D";
                }

                // ---- lots, rights, rates (server is the source of truth) ----
                var rights = LoadPharmacyRights(request.BranchId, request.RoleId, globalValues.userId);
                decimal maxDisc = Math.Min(100m, rights.MaxDiscountPer);
                if (request.BillDiscountPer > maxDisc || items.Any(i => i.DiscPer > maxDisc))
                    return Invalid<PharmacySaleSaveResponse>($"You are allowed to give Discount upto {maxDisc:0.##}%");

                var lots = items.Count == 0 ? new Dictionary<int, SaleBatchModel>()
                    : LoadSaleBatches(request.BranchId, request.RoleId, null, string.Join(",", items.Select(i => i.StockId)), globalValues.hospId)
                        .ToDictionary(b => b.StockId);

                var lines = new List<object>();
                decimal sumGross = 0, sumItemDisc = 0, sumBillDisc = 0, sumNet = 0, sumTax = 0;
                int units = 0;
                bool hasNarcotic = false;
                for (int n = 0; n < items.Count; n++)
                {
                    var it = items[n];
                    if (!lots.TryGetValue(it.StockId, out var lot) || lot.IsActive != 1 || lot.IsExpired == 1)
                        return Invalid<PharmacySaleSaveResponse>($"Row {n + 1}: Stock not available for this batch");
                    string name = lot.ItemName;
                    if (it.Qty > lot.CurrentStock)
                        return Invalid<PharmacySaleSaveResponse>($"Issue quantity can not be more then current Stock For Item : {name} (available {lot.CurrentStock})");
                    if (lot.IsNarcotic == 1)
                    {
                        hasNarcotic = true;
                        if (rights.CanSellNarcoDrugs != 1) return Invalid<PharmacySaleSaveResponse>("You do not have permission to sell narcotic drugs.");
                    }
                    if (corporateId > 0 && lot.CorporateIds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                            .Contains(corporateId.ToString(CultureInfo.InvariantCulture)))
                        return Invalid<PharmacySaleSaveResponse>($"This medicine ({name}) is restricted for the patient's corporate / panel");
                    if (it.IndentItemId > 0 && request.SourceTypeId != 2)
                        return Invalid<PharmacySaleSaveResponse>("Indent lines can be issued only against their ward indent");

                    decimal rate = request.SaleTypeId == 2 ? lot.UnitCost : lot.Mrp;
                    if (rate <= 0) return Invalid<PharmacySaleSaveResponse>($"Rate cannot be zero for {name}");
                    var c = CalcSaleLine(rate, it.Qty, it.DiscPer, request.BillDiscountPer, lot.GstPer);

                    sumGross += c.Gross; sumNet += c.Net; sumTax += c.Tax; units += it.Qty;
                    if (c.BillDiscount) sumBillDisc += c.DiscAmt; else sumItemDisc += c.DiscAmt;
                    lines.Add(new
                    {
                        lineNo = n + 1,
                        stockId = lot.StockId,
                        itemId = lot.ItemId,
                        itemName = name,
                        subSubCategoryId = lot.SubSubCategoryId,
                        itemCode = lot.ItemCode,
                        qty = it.Qty,
                        rate = c.Rate,
                        grossAmt = c.Gross,
                        discPer = c.DiscPer,
                        discAmt = c.DiscAmt,
                        gstPer = lot.GstPer,
                        taxAmt = c.Tax,
                        netAmt = c.Net,
                        indentItemId = it.IndentItemId > 0 ? it.IndentItemId : null
                    });
                }
                if (hasNarcotic && string.IsNullOrWhiteSpace(request.Witness))
                    return Invalid<PharmacySaleSaveResponse>("Witness is required for narcotic medicines");

                decimal netAmount = Math.Round(sumNet, 0, MidpointRounding.AwayFromZero);
                if (items.Count > 0 && netAmount == 0) return Invalid<PharmacySaleSaveResponse>("Net Amount cannot be zero.");

                // ---- payments (OPD): modes mapped to the corporate (OLD), bank / reference where the mode needs it ----
                decimal paid = 0, credit = 0;
                var payRows = new List<(PharmacySalePaymentRequest P, int TypeId)>();
                if (!isIpd)
                {
                    var modes = _sqlHelper.GetDataTable("S_GetCorporatePaymentModes", CommandType.StoredProcedure,
                        new { corporateId, isRefundPaymentModes = 0 })?.AsEnumerable()
                        .ToDictionary(r => r.Field<int>("PaymentModeId"), r => r) ?? new Dictionary<int, DataRow>();
                    foreach (var p in payments)
                    {
                        if (!modes.TryGetValue(p.PaymentModeId, out var mode))
                            return Invalid<PharmacySaleSaveResponse>("No Payment Mode is mapped with Corporate. Please contact to Administrator");
                        int typeId = ToInt(mode["PayModeTypeId"]);
                        string modeName = mode.Field<string>("PaymentModeName") ?? "";
                        if (typeId == PayModeTypeBank && !(p.BankId > 0)) return Invalid<PharmacySaleSaveResponse>($"Select Bank for Payment Mode {modeName}");
                        if (ToInt(mode["ShowReferenceNumberField"]) == 1 && string.IsNullOrWhiteSpace(p.RefNo))
                            return Invalid<PharmacySaleSaveResponse>($"Enter Reference No. for Payment Mode {modeName}");
                        if (typeId == PayModeTypeCredit) credit += p.Amount; else paid += p.Amount;
                        payRows.Add((p, typeId));
                    }
                    if (paid + credit > netAmount) return Invalid<PharmacySaleSaveResponse>("Paid amount cannot be more than the bill amount");
                    // design: payment must be complete unless a Credit mode covers the rest
                    if (paid + credit < netAmount) return Invalid<PharmacySaleSaveResponse>($"Balance ₹ {netAmount - paid - credit:0.00} is due - collect it or add a Credit mode");
                }

                // ---- write (one Serializable transaction) ----
                int storeBillId;
                string billNo = string.Empty;
                using (var con = new SqlConnection(_configuration.GetConnectionString("ConnectionString")))
                {
                    con.Open();
                    using (SqlTransaction tnx = CustomSqlHelper.getSqlTransaction(con))
                    {
                        // 1. walk-in patient -> UHID from the walk-in sequence (owner 2026-11-23)
                        if (isWalkIn)
                        {
                            object rawPid = _sqlHelper.DML(tnx, "I_PharmacyWalkInPatient", CommandType.StoredProcedure, new
                            {
                                hospId = globalValues.hospId,
                                branchId = request.BranchId,
                                title = string.IsNullOrWhiteSpace(request.Title) ? "MR." : request.Title.Trim(),
                                firstName = request.WalkInName!.Trim(),
                                ageYears = request.AgeYears,
                                ageMonths = request.AgeMonths,
                                ageDays = request.AgeDays,
                                gender = request.Gender?.Trim(),
                                contactNumber = string.IsNullOrWhiteSpace(request.Mobile) ? null : request.Mobile.Trim(),
                                address = string.IsNullOrWhiteSpace(request.Address) ? null : request.Address.Trim(),
                                userId = globalValues.userId,
                                ipAddress = globalValues.ipAddress
                            }, new { Result = 0 });
                            patientId = rawPid == null || rawPid == DBNull.Value ? 0 : Convert.ToInt32(rawPid);
                            if (patientId <= 0)
                            {
                                tnx.Rollback();
                                return Invalid<PharmacySaleSaveResponse>("Walk-in UHID sequence is not set up (Sequence type 26)");
                            }
                            var u = _sqlHelper.GetDataTable(tnx, "S_GetPatientForSale", CommandType.StoredProcedure,
                                new { hospId = globalValues.hospId, branchId = request.BranchId, patientId, visitId = 0 });
                            uhid = u?.Rows.Count > 0 ? u.Rows[0].Field<string>("UHID") ?? string.Empty : string.Empty;
                            var bc = u?.Rows.Count > 0 ? ToInt(u.Rows[0]["PatientCorporateId"]) : 1;
                            corporateId = bc > 0 ? bc : 1;
                        }

                        // 2. visit: OPD / walk-in = new Store visit (OLD), IPD = the admission
                        if (!isIpd)
                        {
                            var pvd = new PatientVisitDetails
                            {
                                HospId = globalValues.hospId,
                                BranchId = request.BranchId,
                                PatientId = patientId,
                                Uhid = uhid,
                                visitType = VisitType.Store,
                                CurrentAge = currentAge,
                                DoctorId = doctorId ?? 0,
                                CorporateId = corporateId,
                                InsuranceCompanyId = insuranceCompanyId,
                                TotalBillAmount = sumGross,
                                TotalDiscountPerOnBill = request.BillDiscountPer,
                                TotalDiscountAmountOnBill = sumItemDisc + sumBillDisc,
                                RoundOff = netAmount - sumNet,
                                TotalPayableAmount = netAmount,
                                TotalPatientPayableAmount = netAmount,
                                TotalPaidAmount = paid,
                                TotalPatientPaidAmount = paid,
                                TotalBalanceAmount = netAmount - paid,
                                UserId = globalValues.userId,
                                IpAddress = globalValues.ipAddress,
                                UniqueId = request.UniqueId ?? Guid.NewGuid().ToString()
                            };
                            visitId = Convert.ToInt32(pvd.Create(_sqlHelper, tnx));
                        }

                        // 3. bill row (IPD: I_PatientBillDetails adds the amount into the admission's IP bill = OLD updateIPDBillingByVisitId)
                        var pbd = new PatientBillDetails
                        {
                            HospId = globalValues.hospId,
                            BranchId = request.BranchId,
                            RoleId = request.RoleId,
                            PatientId = patientId,
                            VisitId = visitId,
                            TypeId = isIpd ? 2 : 3,
                            TotalBillAmount = sumGross,
                            TotalDiscountPerOnBill = request.BillDiscountPer,
                            TotalDiscountAmountOnBill = sumItemDisc + sumBillDisc,
                            RoundOff = netAmount - sumNet,
                            TotalPayableAmount = netAmount,
                            TotalPaidAmount = paid,
                            TotalBalanceAmount = netAmount - paid,
                            TotalPatientPayableAmount = netAmount,
                            TotalPatientPaidAmount = paid,
                            UserId = globalValues.userId,
                            IpAddress = globalValues.ipAddress
                        };
                        int billId = items.Count > 0 ? Convert.ToInt32(pbd.Create(_sqlHelper, tnx)) : 0;

                        // 4. financial transaction (OPDSell / IPDIssue)
                        var ft = new FinancialTransactions
                        {
                            HospId = globalValues.hospId,
                            BranchId = request.BranchId,
                            VisitId = visitId,
                            BillId = billId > 0 ? billId : null,
                            PatientId = patientId,
                            tnxType = isIpd ? TnxType.IPDIssue : TnxType.OPDSell,
                            GrossAmount = sumGross,
                            DiscountPercentage = request.BillDiscountPer,
                            DiscountAmount = sumItemDisc + sumBillDisc,
                            TotalTaxAmount = sumTax,
                            RoundOff = netAmount - sumNet,
                            NetAmount = netAmount,
                            Remarks = string.IsNullOrWhiteSpace(request.Remarks) ? null : request.Remarks.Trim(),
                            GstType = "CGST & SGST",      // OLD hard-coded
                            UserId = globalValues.userId,
                            IpAddress = globalValues.ipAddress,
                            UniqueId = request.UniqueId ?? Guid.NewGuid().ToString()
                        };
                        int ftid = Convert.ToInt32(ft.Create(_sqlHelper, tnx));

                        // 5. receipt (OPD, paid > 0): Receipts isStore = 1 + one row per non-credit mode
                        int receiptId = 0;
                        if (paid > 0)
                        {
                            var receipt = new Receipts
                            {
                                HospId = globalValues.hospId,
                                BranchId = request.BranchId,
                                RoleId = request.RoleId,
                                BillId = billId,
                                VisitId = visitId,
                                PatientId = patientId,
                                Amount = paid,
                                IsStore = 1,
                                UserId = globalValues.userId,
                                IpAddress = globalValues.ipAddress,
                                UniqueId = Guid.NewGuid().ToString()
                            };
                            receiptId = Convert.ToInt32(receipt.Create(_sqlHelper, tnx));
                            foreach (var (p, typeId) in payRows.Where(x => x.TypeId != PayModeTypeCredit))
                            {
                                new ReceiptsPaymentModeDetails
                                {
                                    HospId = globalValues.hospId,
                                    BranchId = request.BranchId,
                                    ReceiptID = receiptId,
                                    Amount = p.Amount,
                                    PaymentModeId = p.PaymentModeId,
                                    BankId = p.BankId > 0 ? p.BankId : null,
                                    ReferenceNo = string.IsNullOrWhiteSpace(p.RefNo) ? null : p.RefNo.Trim(),
                                    UserId = globalValues.userId,
                                    IpAddress = globalValues.ipAddress
                                }.Create(_sqlHelper, tnx);
                            }
                        }

                        // 6. store bill + lines + stock issue + indent bookkeeping
                        object raw = _sqlHelper.DML(tnx, "I_PharmacySaleLines", CommandType.StoredProcedure, new
                        {
                            hospId = globalValues.hospId,
                            branchId = request.BranchId,
                            roleId = request.RoleId,
                            visitId,
                            patientId,
                            billId = billId > 0 ? billId : (int?)null,
                            ftid,
                            receiptId = receiptId > 0 ? receiptId : (int?)null,
                            corporateId,
                            doctorId,
                            patientTypeId = request.PatientTypeId,
                            isWalkIn = isWalkIn ? 1 : 0,
                            saleTypeId = request.SaleTypeId,
                            sourceTypeId = request.SourceTypeId,
                            sourceId = request.SourceTypeId > 0 ? request.SourceId : null,
                            doctorRef = request.DoctorRef,
                            witness = request.Witness,
                            remarks = request.Remarks,
                            totalItems = items.Count,
                            totalUnits = units,
                            grossAmount = sumGross,
                            itemDiscountAmount = sumItemDisc,
                            billDiscountPer = request.BillDiscountPer,
                            billDiscountAmount = sumBillDisc,
                            taxAmount = sumTax,
                            roundOff = netAmount - sumNet,
                            netAmount,
                            paidAmount = paid,
                            itemsJson = System.Text.Json.JsonSerializer.Serialize(lines),
                            rejectsJson = rejects.Count == 0 ? null : System.Text.Json.JsonSerializer.Serialize(
                                rejects.Select(r => new { indentItemId = r.IndentItemId, qty = r.Qty, reason = r.Reason!.Trim() })),
                            userId = globalValues.userId,
                            ipAddress = globalValues.ipAddress
                        }, new { Result = 0 });

                        storeBillId = raw == null || raw == DBNull.Value ? 0 : Convert.ToInt32(raw);
                        if (storeBillId <= 0)
                        {
                            tnx.Rollback();
                            string ItemAt(int i) => i >= 1 && i <= items.Count && lots.TryGetValue(items[i - 1].StockId, out var l) ? l.ItemName : "";
                            string msg = storeBillId switch
                            {
                                -3 => "Store bill number sequence (type 5) is not set up",
                                -9000 => "This indent is no longer open for this store",
                                -9001 => "issue and rejected quantity can not be more then pending quantity",
                                < -10000 => $"Issue and reject quantity can not more than pending quantity ({ItemAt(-storeBillId - 10000)})",
                                < 0 => $"Stock not available for item : {ItemAt(-storeBillId)}",
                                _ => "Bill could not be saved"
                            };
                            _log.Warn($"SavePharmacySale rejected: {storeBillId} {msg}");
                            return Invalid<PharmacySaleSaveResponse>(msg);
                        }
                        tnx.Commit();
                    }
                }

                var saved = _sqlHelper.GetDataTable("S_GetPharmacySaleList", CommandType.StoredProcedure, new
                {
                    hospId = globalValues.hospId, branchId = request.BranchId, roleId = request.RoleId,
                    fromDate = DateTime.Today.AddDays(-1), toDate = DateTime.Today.AddDays(1), patientTypeId = (int?)null, search = (string?)null
                })?.AsEnumerable().FirstOrDefault(r => r.Field<int>("StoreBillId") == storeBillId);
                billNo = saved?.Field<string>("BillNo") ?? string.Empty;
                uhid = saved?.Field<string>("UHID") ?? uhid;

                _log.Info($"SavePharmacySale committed. StoreBillId={storeBillId}, BillNo={billNo}, Net={netAmount}");
                var ok = _messageService.GetMessageAndTypeByAlertCode("DATA_SAVED_SUCCESSFULLY");
                return ServiceResult<PharmacySaleSaveResponse>.Success(new PharmacySaleSaveResponse
                {
                    StoreBillId = storeBillId,
                    BillNo = billNo,
                    Uhid = uhid,
                    NetAmount = netAmount,
                    PaidAmount = paid
                }, ok.Type, isIpd ? $"Issued {billNo} - posted to the IP bill" : $"Billing saved successfully - {billNo}", 201);
            }
            catch (Exception ex)
            {
                return ServerError<PharmacySaleSaveResponse>(ex);
            }
        }

        public ServiceResult<int> RejectPatientIndent(RejectPatientIndentRequest request, AllGlobalValues globalValues)
        {
            try
            {
                var raw = _sqlHelper.DML("U_RejectPatientIndent", CommandType.StoredProcedure, new
                {
                    hospId = globalValues.hospId,
                    branchId = request.BranchId,
                    roleId = request.RoleId,
                    indentId = request.IndentId,
                    reason = request.Reason.Trim(),
                    userId = globalValues.userId,
                    ipAddress = globalValues.ipAddress
                }, new { Result = 0 });
                int r = Convert.ToInt32(raw);
                if (r > 0)
                {
                    var ok = _messageService.GetMessageAndTypeByAlertCode("DATA_UPDATED_SUCCESSFULLY");
                    return ServiceResult<int>.Success(r, ok.Type, "Indent rejected", 200);
                }
                return Invalid<int>(r == -2 ? "Only an open indent can be rejected" : "Indent not found for this store");
            }
            catch (Exception ex)
            {
                return ServerError<int>(ex);
            }
        }

        /// <summary>Counts one print of the bill slip; returns the total prints (1 = original, more = duplicate).</summary>
        public ServiceResult<int> PrintPharmacyBill(PharmacyBillPrintRequest request, AllGlobalValues globalValues)
        {
            try
            {
                var raw = _sqlHelper.DML("U_PharmacyBillPrint", CommandType.StoredProcedure, new
                {
                    hospId = globalValues.hospId,
                    storeBillId = request.StoreBillId,
                    userId = globalValues.userId,
                    IpAddress = globalValues.ipAddress
                }, new { Result = 0 });
                int prints = Convert.ToInt32(raw);
                if (prints <= 0)
                {
                    var nf = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    return ServiceResult<int>.Failure(nf.Type, nf.Message, 404);
                }
                return ServiceResult<int>.Success(prints, "Info", prints == 1 ? "Original printed" : $"Reprint #{prints - 1} (duplicate)", 200);
            }
            catch (Exception ex)
            {
                return ServerError<int>(ex);
            }
        }

        /// <summary>Raise a ward indent (OLD IPD tab _MedicineIndent). Used by the IPD medicine-indent screen (to be built) and for testing.</summary>
        public ServiceResult<int> SavePatientMedicineIndent(SavePatientMedicineIndentRequest request, AllGlobalValues globalValues)
        {
            try
            {
                var items = request.Items ?? new List<PatientMedicineIndentItemRequest>();
                if (items.Count == 0) return Invalid<int>("Please add at least one medicine");
                if (items.GroupBy(i => i.ItemId).Any(g => g.Count() > 1)) return Invalid<int>("Item Already Added");
                var raw = _sqlHelper.DML("IU_PatientMedicineIndent", CommandType.StoredProcedure, new
                {
                    hospId = globalValues.hospId,
                    branchId = request.BranchId,
                    patientId = request.PatientId,
                    visitId = request.VisitId,
                    toBranchId = request.BranchId,
                    toDeptId = request.ToDeptId,
                    priority = request.Priority == "Urgent" ? "Urgent" : "Routine",
                    remarks = string.IsNullOrWhiteSpace(request.Remarks) ? null : request.Remarks.Trim(),
                    itemsJson = System.Text.Json.JsonSerializer.Serialize(items.Select(i => new
                    {
                        itemId = i.ItemId, qty = i.Qty, dose = i.Dose, route = i.Route, times = i.Times,
                        duration = i.Duration, meal = i.Meal, remarks = i.Remarks
                    })),
                    userId = globalValues.userId,
                    ipAddress = globalValues.ipAddress
                }, new { Result = 0 });
                int r = Convert.ToInt32(raw);
                if (r > 0)
                {
                    var ok = _messageService.GetMessageAndTypeByAlertCode("DATA_SAVED_SUCCESSFULLY");
                    return ServiceResult<int>.Success(r, ok.Type, ok.Message, 201);
                }
                return Invalid<int>(r == -2 ? "The patient is not admitted" : "Indent could not be saved");
            }
            catch (Exception ex)
            {
                return ServerError<int>(ex);
            }
        }

        #endregion
        // ToInt(object) is shared from StoreRepository.PurchaseRequest.cs
    }
}
