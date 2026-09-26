using HISWEBAPI.Data.Helpers;
using HISWEBAPI.Domain;
using HISWEBAPI.DTO;
using HISWEBAPI.Exceptions;
using HISWEBAPI.Models;
using HISWEBAPI.Repositories.Interfaces;
using HISWEBAPI.Services;
using HISWEBAPI.Utilities;
using log4net;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Reflection;

namespace HISWEBAPI.Repositories.Implementations
{
    public class IPDRepository : IIPDRepository
    {
        private readonly ICustomSqlHelper _sqlHelper;
        private readonly IResponseMessageService _messageService;
        private readonly IConfiguration _configuration;
        private static readonly ILog _log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        // Bed status constants (matches U_UpdateBedStatus contract)
        private const int BED_STATUS_AVAILABLE = 0;
        private const int BED_STATUS_PATIENT_ADMITTED = 1;

        public IPDRepository(
            ICustomSqlHelper sqlHelper,
            IResponseMessageService messageService,
            IConfiguration configuration)
        {
            _sqlHelper = sqlHelper;
            _messageService = messageService;
            _configuration = configuration;
        }

        public ServiceResult<object> GetIPDPatientBedHistory(int visitId)
        {
            try
            {
                _log.Info($"GetIPDPatientBedHistory called. VisitId={visitId}");

                var dataTable = _sqlHelper.GetDataTable(
                    "S_GetIPDPatientBedHistory",
                    CommandType.StoredProcedure,
                    new { @visitId = visitId }
                );

                if (dataTable == null || dataTable.Rows.Count == 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info($"No bed history found for VisitId={visitId}");
                    return ServiceResult<object>.Failure(
                        alert.Type,
                        "No bed history found for the given visit",
                        404
                    );
                }

                // Return raw DataTable as list of dictionaries without model mapping
                var result = dataTable.AsEnumerable().Select(row =>
                    dataTable.Columns.Cast<DataColumn>().ToDictionary(
                        col => col.ColumnName,
                        col => row[col] == DBNull.Value ? null : row[col]
                    )
                ).ToList();

                _log.Info($"IPD bed history retrieved successfully for VisitId={visitId}. Rows={result.Count}");

                return ServiceResult<object>.Success(
                    result,
                    "Info",
                    "Bed history retrieved successfully",
                    200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<object>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }

        public ServiceResult<string> TransferIPDPatientBed(
            TransferIPDPatientBedRequest request,
            AllGlobalValues globalValues)
        {
            var connectionString = _configuration.GetConnectionString("ConnectionString");
            SqlConnection con = new SqlConnection(connectionString);
            con.Open();
            var tnx = CustomSqlHelper.getSqlTransaction(con);

            try
            {
                _log.Info($"TransferIPDPatientBed called. VisitId={request.VisitId}, CurrentBedId={request.CurrentBedId}, NewBedId={request.NewBedId}");

                // 1. Update PatientVisitDetails with new billing/room/bed
                _sqlHelper.DML(
                    tnx,
                    "U_TransferIPDPatientBed",
                    CommandType.StoredProcedure,
                    new
                    {
                        @billingTypeId = request.BillingTypeId,
                        @roomTypeId = request.RoomTypeId,
                        @bedId = request.NewBedId,
                        @visitId = request.VisitId,
                        @userId = globalValues.userId,
                        @ipAddress = globalValues.ipAddress
                    }
                );

                // 2. Insert new bed mapping row / close previous current mapping
                _sqlHelper.DML(
                    tnx,
                    "IU_IPDVisitBedMapping",
                    CommandType.StoredProcedure,
                    new
                    {
                        @visitId = request.VisitId,
                        @bedId = request.NewBedId,
                        @isTransfer=1,
                        @userId = globalValues.userId,
                        @ipAddress = globalValues.ipAddress
                    }
                );

                // 3. Free up old bed
                _sqlHelper.DML(
                    tnx,
                    "U_UpdateBedStatus",
                    CommandType.StoredProcedure,
                    new
                    {
                        @bedId = request.CurrentBedId,
                        @currentStatus = BED_STATUS_AVAILABLE
                    }
                );

                // 4. Occupy new bed
                _sqlHelper.DML(
                    tnx,
                    "U_UpdateBedStatus",
                    CommandType.StoredProcedure,
                    new
                    {
                        @bedId = request.NewBedId,
                        @currentStatus = BED_STATUS_PATIENT_ADMITTED
                    }
                );

                tnx.Commit();
                _log.Info($"TransferIPDPatientBed committed. VisitId={request.VisitId}, NewBedId={request.NewBedId}");

                var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_UPDATED_SUCCESSFULLY");
                return ServiceResult<string>.Success(
                    "Patient bed transferred successfully",
                    alert.Type,
                    alert.Message,
                    200
                );
            }
            catch (Exception ex)
            {
                try { tnx.Rollback(); } catch { /* swallow rollback exception */ }
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<string>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
            finally
            {
                tnx.Dispose();
                if (con.State == ConnectionState.Open)
                    con.Close();
            }
        }

        public ServiceResult<object> GetIPDPatientDoctorHistory(int visitId)
        {
            try
            {
                _log.Info($"GetIPDPatientDoctorHistory called. VisitId={visitId}");

                var dataTable = _sqlHelper.GetDataTable(
                    "S_GetIPDPatientDoctorHistory",
                    CommandType.StoredProcedure,
                    new { @visitId = visitId }
                );

                if (dataTable == null || dataTable.Rows.Count == 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info($"No doctor history found for VisitId={visitId}");
                    return ServiceResult<object>.Failure(
                        alert.Type,
                        "No doctor history found for the given visit",
                        404
                    );
                }

                // Return raw DataTable as list of dictionaries without model mapping
                var result = dataTable.AsEnumerable().Select(row =>
                    dataTable.Columns.Cast<DataColumn>().ToDictionary(
                        col => col.ColumnName,
                        col => row[col] == DBNull.Value ? null : row[col]
                    )
                ).ToList();

                _log.Info($"IPD doctor history retrieved successfully for VisitId={visitId}. Rows={result.Count}");

                return ServiceResult<object>.Success(
                    result,
                    "Info",
                    "Doctor history retrieved successfully",
                    200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<object>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }

        public ServiceResult<string> TransferIPDPatientDoctor(
            TransferIPDPatientDoctorRequest request,
            AllGlobalValues globalValues)
        {
            var connectionString = _configuration.GetConnectionString("ConnectionString");
            SqlConnection con = new SqlConnection(connectionString);
            con.Open();
            var tnx = CustomSqlHelper.getSqlTransaction(con);

            try
            {
                _log.Info($"TransferIPDPatientDoctor called. VisitId={request.VisitId}, PrimaryDoctorId={request.PrimaryDoctorId}, SecondaryCount={request.SecondaryDoctorIds?.Count ?? 0}");

                // 1. Close out the currently active doctor mapping(s) for this visit
                _sqlHelper.DML(
                    tnx,
                    "U_DisableIPDVisitDoctorMapping",
                    CommandType.StoredProcedure,
                    new
                    {
                        @visitId = request.VisitId,
                        @userId = globalValues.userId,
                        @ipAddress = globalValues.ipAddress
                    }
                );

                // 2. Insert secondary (non-primary) doctor mappings
                if (request.SecondaryDoctorIds != null)
                {
                    foreach (var secondaryDoctorId in request.SecondaryDoctorIds)
                    {
                        _sqlHelper.DML(
                            tnx,
                            "I_IPDVisitDoctorMapping",
                            CommandType.StoredProcedure,
                            new
                            {
                                @visitId = request.VisitId,
                                @doctorId = secondaryDoctorId,
                                @isPrimaryDoctor = 0,
                                @userId = globalValues.userId,
                                @ipAddress = globalValues.ipAddress
                            }
                        );
                    }
                }

                // 3. Insert primary doctor mapping
                _sqlHelper.DML(
                    tnx,
                    "I_IPDVisitDoctorMapping",
                    CommandType.StoredProcedure,
                    new
                    {
                        @visitId = request.VisitId,
                        @doctorId = request.PrimaryDoctorId,
                        @isPrimaryDoctor = 1,
                        @userId = globalValues.userId,
                        @ipAddress = globalValues.ipAddress
                    }
                );

                // 4. Doctor-wise IPD sequence number — only create if it doesn't already exist
                int isSeqExists = _sqlHelper.ExecuteScalar(
                    tnx,
                    "S_CheckDoctorVisitSeqExists",
                    CommandType.StoredProcedure,
                    new
                    {
                        @visitId = request.VisitId,
                        @doctorId = request.PrimaryDoctorId
                    }
                );

                

                if (isSeqExists == 0)
                {
                    _sqlHelper.DML(
                        tnx,
                        "I_IPDVisitDoctorSequence",
                        CommandType.StoredProcedure,
                        new
                        {
                            @branchId = request.BranchId,
                            @doctorId = request.PrimaryDoctorId,
                            @visitId = request.VisitId
                        }
                    );
                }

                tnx.Commit();
                _log.Info($"TransferIPDPatientDoctor committed. VisitId={request.VisitId}, PrimaryDoctorId={request.PrimaryDoctorId}");

                var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_UPDATED_SUCCESSFULLY");
                return ServiceResult<string>.Success(
                    "Patient doctor transferred successfully",
                    alert.Type,
                    alert.Message,
                    200
                );
            }
            catch (Exception ex)
            {
                try { tnx.Rollback(); } catch { /* swallow rollback exception */ }
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<string>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
            finally
            {
                tnx.Dispose();
                if (con.State == ConnectionState.Open)
                    con.Close();
            }
        }

        public ServiceResult<object> GetIPDPatientCorporateHistory(int visitId)
        {
            try
            {
                _log.Info($"GetIPDPatientCorporateHistory called. VisitId={visitId}");

                var dataTable = _sqlHelper.GetDataTable(
                    "S_GetIPDPatientCorporateHistory",
                    CommandType.StoredProcedure,
                    new { @visitId = visitId }
                );

                if (dataTable == null || dataTable.Rows.Count == 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info($"No corporate history found for VisitId={visitId}");
                    return ServiceResult<object>.Failure(
                        alert.Type,
                        "No corporate history found for the given visit",
                        404
                    );
                }

                // Return raw DataTable as list of dictionaries without model mapping
                var result = dataTable.AsEnumerable().Select(row =>
                    dataTable.Columns.Cast<DataColumn>().ToDictionary(
                        col => col.ColumnName,
                        col => row[col] == DBNull.Value ? null : row[col]
                    )
                ).ToList();

                _log.Info($"IPD corporate history retrieved successfully for VisitId={visitId}. Rows={result.Count}");

                return ServiceResult<object>.Success(
                    result,
                    "Info",
                    "Corporate history retrieved successfully",
                    200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<object>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }

        public ServiceResult<string> UpdateIPDPatientTariffDetails(
            UpdateIPDPatientTariffDetailsRequest request,
            AllGlobalValues globalValues)
        {
            var connectionString = _configuration.GetConnectionString("ConnectionString");
            SqlConnection con = new SqlConnection(connectionString);
            con.Open();
            var tnx = CustomSqlHelper.getSqlTransaction(con);

            try
            {
                _log.Info($"UpdateIPDPatientTariffDetails called. VisitId={request.VisitId}, PatientId={request.PatientId}, CorporateId={request.CorporateId}, IsChangeTariff={request.IsChangeTariff}");

                // 1. Update corporate/insurance/relation/card details + push new corporate mapping row
                DateTime TransferDate = Convert.ToDateTime(request.TransferDate);


                _sqlHelper.DML(
                    tnx,
                    "U_UpdateIPDTariffDetails",
                    CommandType.StoredProcedure,
                    new
                    {
                        @visitId = request.VisitId,
                        @patientId = request.PatientId,
                        @insuranceCompanyId = request.InsuranceCompanyId,
                        @corporateId = request.CorporateId,
                        @relation = (object)request.Relation ?? DBNull.Value,
                        @relativeName = (object)request.RelativeName ?? DBNull.Value,
                        @cardNo = (object)request.CardNo ?? DBNull.Value,
                        @transferDate = TransferDate,
                        @remarks = (object)request.Remarks ?? DBNull.Value,
                        @reasonForTransfer = (object)request.ReasonForTransfer ?? DBNull.Value,
                        @authorizationNumber = (object)request.AuthorizationNumber ?? DBNull.Value,
                        @billingTypeId = request.BillingTypeId,
                        @userId = globalValues.userId,
                        @ipAddress = globalValues.ipAddress
                    }
                );

                // 2. Optionally recalculate tariff rates + roll up billing totals for the visit
                if (request.IsChangeTariff == 1)
                {
                    DateTime fromDate = Convert.ToDateTime(request.ChangeTariffFromDate);
                    DateTime toDate = Convert.ToDateTime(request.ChangeTariffToDate);

                    _sqlHelper.DML(
                        tnx,
                        "U_UpdateIPDTariffAfterCorporateChange",
                        CommandType.StoredProcedure,
                        new
                        {
                            @branchId = request.BranchId,
                            @visitId = request.VisitId,
                            @newCorporateId = request.CorporateId,
                            @changeFromDate = fromDate.ToString("yyyy-MM-dd"),
                            @changeToDate = toDate.ToString("yyyy-MM-dd"),
                            @userId = globalValues.userId,
                            @ipAddress = globalValues.ipAddress
                        }
                    );

                    _sqlHelper.DML(
                        tnx,
                        "U_UpdateIPDBillingByVisitDetails",
                        CommandType.StoredProcedure,
                        new
                        {
                            @visitId = request.VisitId,
                            @userId = globalValues.userId,
                            @ipAddress = globalValues.ipAddress
                        }
                    );
                }

                tnx.Commit();
                _log.Info($"UpdateIPDPatientTariffDetails committed. VisitId={request.VisitId}");

                var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_UPDATED_SUCCESSFULLY");
                return ServiceResult<string>.Success(
                    "Patient tariff details updated successfully",
                    alert.Type,
                    alert.Message,
                    200
                );
            }
            catch (Exception ex)
            {
                try { tnx.Rollback(); } catch { /* swallow rollback exception */ }
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<string>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
            finally
            {
                tnx.Dispose();
                if (con.State == ConnectionState.Open)
                    con.Close();
            }
        }

        public ServiceResult<SaveCorporateTransferRequestApprovalResponse> SaveCorporateTransferRequestApproval(
    SaveCorporateTransferRequestApprovalRequest request,
    AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"SaveCorporateTransferRequestApproval called. PatientId={request.PatientId}, BranchId={request.BranchId}, VisitId={request.VisitId}");

                // Parse optional tariff-change dates (SP columns are DATE)
                DateTime? changeFromDate = null;
                DateTime? changeToDate = null;

                if (request.IsChangeTariff == 1)
                {
                    if (DateTime.TryParse(request.ChangeFromDate, out var cfd))
                        changeFromDate = cfd;

                    if (DateTime.TryParse(request.ChangeToDate, out var ctd))
                        changeToDate = ctd;
                }

                DateTime TransferDate = Convert.ToDateTime(request.TransferDate);


                // I_CorporateTransferRequestDetails uses a true OUTPUT parameter (no trailing SELECT @Result;),
                // so RunProcedureInsert is required here. No item table for CorporateTransfer — header only.
                long corporateTransferIdResult = _sqlHelper.RunProcedureInsert(
                    "I_CorporateTransferRequestDetails",
                    new IDataParameter[]
                    {
                        new SqlParameter("@BranchId", request.BranchId),
                        new SqlParameter("@RoleId", request.RoleId),
                        new SqlParameter("@PatientId", request.PatientId),
                        new SqlParameter("@VisitId", request.VisitId),
                        new SqlParameter("@TypeId", request.TypeId),
                        new SqlParameter("@InsuranceCompanyId", request.InsuranceCompanyId),
                        new SqlParameter("@CorporateId", request.CorporateId),
                        new SqlParameter("@BillingTypeId", request.BillingTypeId),
                        new SqlParameter("@IsChangeTariff", request.IsChangeTariff),
                        new SqlParameter("@ChangeFromDate", (object)changeFromDate ?? DBNull.Value),
                        new SqlParameter("@ChangeToDate", (object)changeToDate ?? DBNull.Value),
                        new SqlParameter("@Relation", (object)request.Relation ?? DBNull.Value),
                        new SqlParameter("@RelativeName", (object)request.RelativeName ?? DBNull.Value),
                        new SqlParameter("@CardNo", (object)request.CardNo ?? DBNull.Value),

                        new SqlParameter("@TransferDate", TransferDate),
                        new SqlParameter("@Remarks", (object)request.Remarks ?? DBNull.Value),
                        new SqlParameter("@ReasonForTransfer", (object)request.ReasonForTransfer ?? DBNull.Value),
                        new SqlParameter("@AuthorizationNumber", (object)request.AuthorizationNumber ?? DBNull.Value),

                        new SqlParameter("@UserId", globalValues.userId),
                        new SqlParameter("@IpAddress", (object)globalValues.ipAddress ?? DBNull.Value),
                        new SqlParameter("@Result", SqlDbType.Int) { Direction = ParameterDirection.Output }
                    });



                int corporateTransferId = Convert.ToInt32(corporateTransferIdResult);
                _log.Info($"CorporateTransferRequestDetails created. CorporateTransferId={corporateTransferId}");

                var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_SAVED_SUCCESSFULLY");
                return ServiceResult<SaveCorporateTransferRequestApprovalResponse>.Success(
                    new SaveCorporateTransferRequestApprovalResponse { CorporateTransferId = corporateTransferId },
                    alert.Type,
                    "Corporate transfer request saved successfully",
                    201
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<SaveCorporateTransferRequestApprovalResponse>.Failure(alert.Type, alert.Message, 500);
            }
        }

        public ServiceResult<string> ApproveCorporateTransferRequest(ApproveCorporateTransferRequestRequest request, AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"ApproveCorporateTransferRequest called. CorporateTransferId={request.CorporateTransferId}, Flag={request.Flag}");

                _sqlHelper.DML(
                    "U_ApproveCorporateTransferRequest",
                    CommandType.StoredProcedure,
                    new
                    {
                        @CorporateTransferId = request.CorporateTransferId,
                        @flag = request.Flag,
                        @ApprovalRemarks = (object)request.ApprovalRemarks ?? DBNull.Value,
                        @UserId = globalValues.userId,
                        @IpAddress = globalValues.ipAddress
                    });

                _log.Info($"ApproveCorporateTransferRequest completed. CorporateTransferId={request.CorporateTransferId}");

                var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_UPDATED_SUCCESSFULLY");
                return ServiceResult<string>.Success(
                    "Corporate transfer request approval updated successfully",
                    alert.Type,
                    alert.Message,
                    200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<string>.Failure(alert.Type, alert.Message, 500);
            }
        }

        public ServiceResult<string> CancelCorporateTransferRequest(CancelCorporateTransferRequestRequest request, AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"CancelCorporateTransferRequest called. CorporateTransferId={request.CorporateTransferId}");

                _sqlHelper.DML(
                    "U_CancelCorporateTransferRequest",
                    CommandType.StoredProcedure,
                    new
                    {
                        @CorporateTransferId = request.CorporateTransferId,
                        @CancelReason = (object)request.CancelReason ?? DBNull.Value,
                        @UserId = globalValues.userId,
                        @IpAddress = globalValues.ipAddress
                    });

                _log.Info($"CancelCorporateTransferRequest completed. CorporateTransferId={request.CorporateTransferId}");

                var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_UPDATED_SUCCESSFULLY");
                return ServiceResult<string>.Success(
                    "Corporate transfer request cancelled successfully",
                    alert.Type,
                    alert.Message,
                    200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<string>.Failure(alert.Type, alert.Message, 500);
            }
        }

        public ServiceResult<string> ConfirmCorporateTransferRequest(ConfirmCorporateTransferRequestRequest request, AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"ConfirmCorporateTransferRequest called. CorporateTransferId={request.CorporateTransferId}");

                _sqlHelper.DML(
                    "U_ConfirmCorporateTransferRequest",
                    CommandType.StoredProcedure,
                    new
                    {
                        @CorporateTransferId = request.CorporateTransferId,
                        @UserId = globalValues.userId,
                        @IpAddress = globalValues.ipAddress
                    });

                _log.Info($"ConfirmCorporateTransferRequest completed. CorporateTransferId={request.CorporateTransferId}");

                var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_UPDATED_SUCCESSFULLY");
                return ServiceResult<string>.Success(
                    "Corporate transfer marked as created successfully",
                    alert.Type,
                    alert.Message,
                    200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<string>.Failure(alert.Type, alert.Message, 500);
            }
        }

        public ServiceResult<object> GetCorporateTransferRequestListForApproval(string fromDate, string toDate, int branchId, AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"GetCorporateTransferRequestListForApproval called. FromDate={fromDate}, ToDate={toDate}, BranchId={branchId}");

                if (!DateTime.TryParse(fromDate, out DateTime parsedFromDate))
                {
                    var alertDate = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                    return ServiceResult<object>.Failure(alertDate.Type, "Invalid FromDate format", 400);
                }

                if (!DateTime.TryParse(toDate, out DateTime parsedToDate))
                {
                    var alertDate = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                    return ServiceResult<object>.Failure(alertDate.Type, "Invalid ToDate format", 400);
                }

                var dataTable = _sqlHelper.GetDataTable(
                    "S_CorporateTransferRequestDetailsForApproval",
                    CommandType.StoredProcedure,
                    new
                    {
                        @fromDate = parsedFromDate.ToString("yyyy-MM-dd"),
                        @toDate = parsedToDate.ToString("yyyy-MM-dd"),
                        @branchId = branchId,
                        @userId = globalValues.userId
                    });

                if (dataTable == null || dataTable.Rows.Count == 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info("GetCorporateTransferRequestListForApproval: no records found");
                    return ServiceResult<object>.Failure(alert.Type, "No corporate transfer requests found", 404);
                }

                var rows = dataTable.AsEnumerable().Select(row =>
                    dataTable.Columns.Cast<DataColumn>().ToDictionary(
                        col => col.ColumnName,
                        col => row[col] == DBNull.Value ? null : row[col]
                    )
                ).ToList();

                _log.Info($"GetCorporateTransferRequestListForApproval retrieved {rows.Count} record(s)");

                var alert1 = _messageService.GetMessageAndTypeByAlertCode("OPERATION_COMPLETED_SUCCESSFULLY");
                return ServiceResult<object>.Success(rows, alert1.Type, $"{rows.Count} corporate transfer request(s) retrieved successfully", 200);
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<object>.Failure(alert.Type, alert.Message, 500);
            }
        }

        public ServiceResult<object> GetCorporateTransferRequestDetailsByCorporateTransferId(int corporateTransferId)
        {
            try
            {
                _log.Info($"GetCorporateTransferRequestDetailsByCorporateTransferId called. CorporateTransferId={corporateTransferId}");

                var dataTable = _sqlHelper.GetDataTable(
                    "S_CorporateTransferRequestDetailsByCorporateTransferId",
                    CommandType.StoredProcedure,
                    new { @CorporateTransferId = corporateTransferId });

                if (dataTable == null || dataTable.Rows.Count == 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info($"No corporate transfer details found for CorporateTransferId={corporateTransferId}");
                    return ServiceResult<object>.Failure(alert.Type, "No corporate transfer details found", 404);
                }

                var result = dataTable.AsEnumerable().Select(row =>
                    dataTable.Columns.Cast<DataColumn>().ToDictionary(
                        col => col.ColumnName,
                        col => row[col] == DBNull.Value ? null : row[col]
                    )
                ).ToList();

                _log.Info($"Corporate transfer details retrieved successfully for CorporateTransferId={corporateTransferId}. Rows={result.Count}");

                return ServiceResult<object>.Success(result, "Info", "Corporate transfer details retrieved successfully", 200);
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<object>.Failure(alert.Type, alert.Message, 500);
            }
        }

        public ServiceResult<object> GetCorporateTransferRequestApprovalDetails(int corporateTransferId)
        {
            try
            {
                _log.Info($"GetCorporateTransferRequestApprovalDetails called. CorporateTransferId={corporateTransferId}");

                var dataTable = _sqlHelper.GetDataTable(
                    "S_GetCorporateTransferRequestApprovalDetails",
                    CommandType.StoredProcedure,
                    new { @CorporateTransferId = corporateTransferId });

                if (dataTable == null || dataTable.Rows.Count == 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info($"No approval details found for CorporateTransferId={corporateTransferId}");
                    return ServiceResult<object>.Failure(alert.Type, "No approval details found", 404);
                }

                var result = dataTable.AsEnumerable().Select(row =>
                    dataTable.Columns.Cast<DataColumn>().ToDictionary(
                        col => col.ColumnName,
                        col => row[col] == DBNull.Value ? null : row[col]
                    )
                ).ToList();

                _log.Info($"Approval details retrieved successfully for CorporateTransferId={corporateTransferId}");

                return ServiceResult<object>.Success(result, "Info", "Approval details retrieved successfully", 200);
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<object>.Failure(alert.Type, alert.Message, 500);
            }
        }

        public ServiceResult<object> GetCorporateTransferRequestDetailsByVisitId(int visitId)
        {
            try
            {
                _log.Info($"GetCorporateTransferRequestDetailsByVisitId called. VisitId={visitId}");

                var dataTable = _sqlHelper.GetDataTable(
                    "S_CorporateTransferRequestDetailsByVisitId",
                    CommandType.StoredProcedure,
                    new { @visitId = visitId }
                );

                if (dataTable == null || dataTable.Rows.Count == 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info($"No corporate transfer request details found for VisitId={visitId}");
                    return ServiceResult<object>.Failure(
                        alert.Type,
                        "No corporate transfer request details found for the given visit",
                        404
                    );
                }

                // Raw DataTable -> List<Dictionary<string,object>> (no model mapping),
                // so any new columns added to the SP surface automatically.
                var result = dataTable.AsEnumerable().Select(row =>
                    dataTable.Columns.Cast<DataColumn>().ToDictionary(
                        col => col.ColumnName,
                        col => row[col] == DBNull.Value ? null : row[col]
                    )
                ).ToList();

                _log.Info($"GetCorporateTransferRequestDetailsByVisitId retrieved {result.Count} record(s) for VisitId={visitId}");

                var alert1 = _messageService.GetMessageAndTypeByAlertCode("OPERATION_COMPLETED_SUCCESSFULLY");
                return ServiceResult<object>.Success(
                    result,
                    alert1.Type,
                    $"{result.Count} record(s) retrieved successfully",
                    200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<object>.Failure(alert.Type, alert.Message, 500);
            }
        }


        public ServiceResult<SaveIPDBillingResponse> SaveIPDBilling(
    SaveIPDBillingRequest request,
    AllGlobalValues globalValues)
        {
            var connectionString = _configuration.GetConnectionString("ConnectionString");
            SqlConnection con = new SqlConnection(connectionString);
            con.Open();
            var tnx = CustomSqlHelper.getSqlTransaction(con);

            try
            {
                _log.Info($"SaveIPDBilling called. PatientId={request.VisitDetails.PatientId}, VisitId={request.VisitDetails.VisitId}, BranchId={request.VisitDetails.BranchId}");

                var v = request.VisitDetails;
                int visitId = v.VisitId;

                decimal totalPaidAmount = 0;
                if (request.PaymentDetails?.Count > 0)
                {
                    totalPaidAmount = request.PaymentDetails.Sum(p => p.Amount);
                }

                // ── 1. PatientBillDetails ────────────────────────────────────────────
                var pbd = new PatientBillDetails
                {
                    HospId = globalValues.hospId,
                    BranchId = v.BranchId,
                    RoleId = v.RoleId,
                    PatientId = v.PatientId,
                    VisitId = visitId,
                    TypeId = 2,                                // 2 = IPD
                    TotalBillAmount = v.GrossBillAmount,
                    TotalDiscountPerOnBill = v.TotalDiscPerOnBill,
                    TotalDiscountAmountOnBill = v.TotalDiscAmtOnBill,
                    DiscountApprovedById = v.DiscApprovedById > 0 ? v.DiscApprovedById : (int?)null,
                    DiscountReason = v.DiscountReason,
                    RoundOff = v.RoundOff,
                    TotalPayableAmount = v.NetAmount,
                    TotalPaidAmount = totalPaidAmount,
                    TotalBalanceAmount = v.NetAmount - totalPaidAmount,
                    TotalPatientPayableAmount = v.NetAmount,
                    TotalCorporatePayableAmount = 0,
                    TotalPatientPaidAmount = totalPaidAmount,
                    TotalCorporatePaidAmount = 0,
                    IsSupplementaryBill = v.IsSupplementaryBill,
                    UserId = globalValues.userId,
                    IpAddress = globalValues.ipAddress
                };

                int billId = Convert.ToInt32(pbd.Create(_sqlHelper, tnx));
                _log.Info($"PatientBillDetails created. BillId={billId}");

                // ── 2. FinancialTransactions ─────────────────────────────────────────
                var ft = new FinancialTransactions
                {
                    HospId = globalValues.hospId,
                    BranchId = v.BranchId,
                    VisitId = visitId,
                    BillId = billId,
                    PatientId = v.PatientId,
                    tnxType = TnxType.IPDBilling,
                    GrossAmount = v.GrossBillAmount,
                    DiscountPercentage = v.TotalDiscPerOnBill,
                    DiscountAmount = v.TotalDiscAmtOnBill,
                    RoundOff = v.RoundOff,
                    NetAmount = v.NetAmount,
                    Remarks = v.Remarks,
                    UserId = globalValues.userId,
                    IpAddress = globalValues.ipAddress,
                    UniqueId = v.UniqueId
                };

                int ftid = Convert.ToInt32(ft.Create(_sqlHelper, tnx));
                _log.Info($"FinancialTransactions created. FTID={ftid}");

                // ── 3. Process billing items ─────────────────────────────────────────
                bool isLabInvestigations = false;

                int labNo = 0;
                var sampleTypeBarcodeMap = new Dictionary<int, int>();
                int pathologyTokenNo = 0;
                int radiologyTokenNo = 0;
                int cardiologyTokenNo = 0;

                foreach (var item in request.BillingItems)
                {
                    // ── 3a. FinancialTransactionDetails ──────────────────────────────
                    decimal itemDiscPer, itemDiscAmt, itemNetAmt;
                    string itemDiscReason;

                    if (request.IsBillDiscount == 1)
                    {
                        itemDiscPer = v.TotalDiscPerOnBill;
                        itemDiscAmt = (item.GrossAmt * v.TotalDiscPerOnBill) / 100;
                        itemNetAmt = item.GrossAmt - itemDiscAmt;
                        itemDiscReason = v.DiscountReason;
                    }
                    else
                    {
                        itemDiscPer = item.DiscPer;
                        itemDiscAmt = item.DiscAmt;
                        itemNetAmt = item.NetAmt;
                        itemDiscReason = item.DiscountReason;
                    }

                    if (!DateTime.TryParse(item.BillingDate, out DateTime parsedBillingDate))
                    {
                        var alertDate = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                        return ServiceResult<SaveIPDBillingResponse>.Failure(alertDate.Type, "Invalid Billing Date format", 400);
                    }

                    var ftd = new FinancialTransactionDetails
                    {
                        HospId = globalValues.hospId,
                        BranchId = v.BranchId,
                        FTID = ftid,
                        VisitId = visitId,
                        BillId = billId,
                        PatientId = v.PatientId,
                        ServiceItemId = item.ServiceItemId,
                        SubSubCategoryId = item.SubSubCategoryId,
                        ServiceName = item.ServiceName,
                        ServiceCode = item.Code,
                        Remarks = item.Remarks,
                        CorporateAlias = item.CorporateAlias,
                        CorporateCode = item.CorporateCode,
                        DoctorId = item.DoctorId > 0 ? item.DoctorId : (int?)null,
                        PerformingDoctorId = item.PerformingDoctorId > 0 ? item.PerformingDoctorId : (int?)null,
                        CorporateId = v.CorporateId > 0 ? v.CorporateId : (int?)null,
                        Rate = item.Rate,
                        Qty = item.Qty,
                        GrossAmt = item.GrossAmt,
                        DiscPer = itemDiscPer,
                        DiscAmt = itemDiscAmt,
                        NetAmt = itemNetAmt,
                        IsCorporateNonPayable = item.IsNonPayable,
                        DiscountReason = itemDiscReason,
                        RateListId = item.RateListId,
                        IsAutoAddToPackage = item.IsAutoAddToPackage,

                        BillingDate = parsedBillingDate.ToString("yyyy-MM-dd"),
                        UserId = globalValues.userId,
                        IpAddress = globalValues.ipAddress
                    };

                    int ftdId = Convert.ToInt32(ftd.Create(_sqlHelper, tnx));
                    _log.Info($"FinancialTransactionDetails created. FTDId={ftdId}, ServiceItemId={item.ServiceItemId}");

                    // ── 3b. Investigation (CategoryTypeId == 3) ──────────────────────
                    if (item.CategoryTypeId == 3)
                    {
                        // Barcode – one per unique SampleTypeId (Pathology only)
                        int barCode = 0;
                        if (item.LabTypeId == 1 && item.SampleTypeId > 0)
                        {
                            if (!sampleTypeBarcodeMap.ContainsKey(item.SampleTypeId))
                            {
                                int newBarcode = Convert.ToInt32(_sqlHelper.ExecuteScalar(
                                    tnx,
                                    "S_GetNextGlobalBarcode",
                                    CommandType.StoredProcedure,
                                    new { @branchId = v.BranchId }));
                                sampleTypeBarcodeMap[item.SampleTypeId] = newBarcode;
                            }
                            barCode = sampleTypeBarcodeMap[item.SampleTypeId];
                        }

                        // Lab number – shared for all investigations in this visit
                        if (labNo == 0)
                        {
                            labNo = Convert.ToInt32(_sqlHelper.ExecuteScalar(
                                tnx,
                                "getLabNo",
                                CommandType.StoredProcedure,
                                new { @branchId = v.BranchId },
                                new { result = 0 }));
                        }

                        // Token number per sub-category
                        int tokenNo = 0;
                        if (item.LabTypeId == 1 || item.LabTypeId == 2 || item.LabTypeId == 3)
                        {
                            tokenNo = Convert.ToInt32(_sqlHelper.ExecuteScalar(
                                tnx,
                                "S_GetLabTokenNo",
                                CommandType.StoredProcedure,
                                new { @branchId = v.BranchId, @SubCategoryId = item.LabTypeId },
                                new { result = 0 }));
                        }

                        if (pathologyTokenNo == 0 && item.LabTypeId == 1) pathologyTokenNo = tokenNo;
                        if (radiologyTokenNo == 0 && item.LabTypeId == 2) radiologyTokenNo = tokenNo;
                        if (cardiologyTokenNo == 0 && item.LabTypeId == 3) cardiologyTokenNo = tokenNo;

                        var pid = new PatientInvestigationDetails
                        {
                            HospId = globalValues.hospId,
                            BranchId = v.BranchId,
                            VisitId = visitId,
                            FTDID = ftdId,
                            InvestigationId = item.ServiceItemId,
                            DoctorId = item.DoctorId,
                            PatientId = v.PatientId,
                            LabNo = labNo,
                            TokenNo = item.LabTypeId == 1 ? pathologyTokenNo
                                    : item.LabTypeId == 2 ? radiologyTokenNo
                                    : cardiologyTokenNo,
                            IsUrgent = item.IsUrgent,
                            BarCode = barCode,
                            UserId = globalValues.userId,
                            IpAddress = globalValues.ipAddress
                        };

                        pid.Create(_sqlHelper, tnx);
                        isLabInvestigations = true;
                        _log.Info($"PatientInvestigationDetails created for InvestigationId={item.ServiceItemId}");
                    }
                }

                // ── 4. Receipt ───────────────────────────────────────────────────────
                int receiptId = 0;
                bool isReceipt = false;
                if (totalPaidAmount > 0)
                {
                    var receipt = new Receipts
                    {
                        HospId = globalValues.hospId,
                        BranchId = v.BranchId,
                        RoleId = v.RoleId,
                        BillId = v.IsSupplementaryBill == 0 ? 0 : billId,
                        VisitId = visitId,
                        PatientId = v.PatientId,
                        Amount = totalPaidAmount,
                        IsCopaymentReceipt = request.PaymentDetails[0].IsCopaymentReceipt,
                        PlutusTransactionReferenceID = request.PaymentDetails[0].PlutusTransactionReferenceID,
                        TransactionLogId = request.PaymentDetails[0].TransactionLogId,
                        UserId = globalValues.userId,
                        IpAddress = globalValues.ipAddress,
                        UniqueId = v.UniqueId
                    };

                    receiptId = Convert.ToInt32(receipt.Create(_sqlHelper, tnx));
                    _log.Info($"Receipt created. ReceiptId={receiptId}");

                    foreach (var p in request.PaymentDetails)
                    {
                        // PaymentModeTypeId 4 = Credit → skip
                        if (p.PaymentModeTypeId == 4)
                            continue;

                        if (p.IsPatientAdvanceAmount == 1)
                            continue;

                        var rpmd = new ReceiptsPaymentModeDetails
                        {
                            HospId = globalValues.hospId,
                            BranchId = v.BranchId,
                            ReceiptID = receiptId,
                            Amount = p.Amount,
                            PaymentModeId = p.PaymentModeId,
                            BankId = p.BankId > 0 ? p.BankId : (int?)null,
                            ReferenceNo = p.RefNo,
                            UserId = globalValues.userId,
                            IpAddress = globalValues.ipAddress
                        };

                        rpmd.Create(_sqlHelper, tnx);
                    }

                    isReceipt = true;
                }



                var packageIdList = request.BillingItems
                    .Where(x => x.CategoryTypeId == 12 && x.IsAutoAddExistingServices == 1)
                    .Select(x => x.ServiceItemId)
                    .Distinct()
                    .ToList();

                _log.Info("BillingItems check: " + string.Join(" | ",
                    request.BillingItems.Select(x =>
                        $"ServiceItemId={x.ServiceItemId}, CategoryTypeId={x.CategoryTypeId}, IsAutoAddExistingServices={x.IsAutoAddExistingServices}")));

                _log.Info($"packageIdList=[{string.Join(",", packageIdList)}], Count={packageIdList.Count}");

                if (packageIdList.Any())
                {
                    foreach (var packageId in packageIdList)
                    {
                        _sqlHelper.DML(
                            tnx,
                            "U_AddExistingServicesInPackage",
                            CommandType.StoredProcedure,
                            new
                            {
                                @PackageId = packageId,
                                @VisitId = visitId,
                                @billId = billId,
                                @UserId = globalValues.userId,
                                @IpAddress = globalValues.ipAddress
                            });
                        _log.Info($"U_AddExistingServicesInPackage called. packageId={packageId}, billId={billId}, VisitId={visitId}");

                    }
                }


                UpdateIPDBillingByVisitId(tnx, v.VisitId, globalValues);

                tnx.Commit();
                _log.Info($"SaveIPDBilling committed. VisitId={visitId}, FTID={ftid}, ReceiptId={receiptId}");

                var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_SAVED_SUCCESSFULLY");
                return ServiceResult<SaveIPDBillingResponse>.Success(
                    new SaveIPDBillingResponse
                    {
                        VisitId = visitId,
                        FTID = ftid,
                        ReceiptId = receiptId,
                        IsReceipt = isReceipt,
                        IsLabInvestigations = isLabInvestigations
                    },
                    alert.Type,
                    "IPD Billing saved successfully",
                    201
                );
            }
            catch (Exception ex)
            {
                try { tnx.Rollback(); } catch { /* swallow rollback exception */ }
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<SaveIPDBillingResponse>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
            finally
            {
                tnx.Dispose();
                if (con.State == ConnectionState.Open)
                    con.Close();
            }
        }

        public ServiceResult<object> GetIPDBillingSummary(int branchId, int visitId)
        {
            try
            {
                _log.Info($"GetIPDBillingSummary called. BranchId={branchId}, VisitId={visitId}");

                var dataTable = _sqlHelper.GetDataTable(
                    "S_GetIPDBillingDetails",
                    CommandType.StoredProcedure,
                    new
                    {
                        @branchId = branchId,
                        @visitId = visitId
                    }
                );

                if (dataTable == null || dataTable.Rows.Count == 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info($"No IPD billing details found for VisitId={visitId}, BranchId={branchId}");
                    return ServiceResult<object>.Failure(
                        alert.Type,
                        "No IPD billing details found",
                        404
                    );
                }

                var result = dataTable.AsEnumerable().Select(row =>
                    dataTable.Columns.Cast<DataColumn>().ToDictionary(
                        col => col.ColumnName,
                        col => row[col] == DBNull.Value ? null : row[col]
                    )
                ).ToList();

                _log.Info($"GetIPDBillingSummary retrieved {result.Count} record(s) for VisitId={visitId}");

                var alert1 = _messageService.GetMessageAndTypeByAlertCode("OPERATION_COMPLETED_SUCCESSFULLY");
                return ServiceResult<object>.Success(
                    result,
                    alert1.Type,
                    $"{result.Count} billing item(s) retrieved successfully",
                    200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<object>.Failure(alert.Type, alert.Message, 500);
            }
        }

        public ServiceResult<object> GetIPDPatientBillAmounts(int visitId, int patientId)
        {
            try
            {
                _log.Info($"GetIPDPatientBillAmounts called. VisitId={visitId}, PatientId={patientId}");

                var dataTable = _sqlHelper.GetDataTable(
                    "S_GetIPDPatientBillAmounts",
                    CommandType.StoredProcedure,
                    new
                    {
                        @visitId = visitId,
                        @patientId = patientId
                    }
                );

                if (dataTable == null || dataTable.Rows.Count == 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info($"No IPD patient bill amounts found for VisitId={visitId}, PatientId={patientId}");
                    return ServiceResult<object>.Failure(
                        alert.Type,
                        "No IPD patient bill amounts found",
                        404
                    );
                }

                var result = dataTable.AsEnumerable().Select(row =>
                    dataTable.Columns.Cast<DataColumn>().ToDictionary(
                        col => col.ColumnName,
                        col => row[col] == DBNull.Value ? null : row[col]
                    )
                ).ToList();

                _log.Info($"GetIPDPatientBillAmounts retrieved {result.Count} record(s) for VisitId={visitId}");

                var alert1 = _messageService.GetMessageAndTypeByAlertCode("OPERATION_COMPLETED_SUCCESSFULLY");
                return ServiceResult<object>.Success(
                    result,
                    alert1.Type,
                    "IPD patient bill amounts retrieved successfully",
                    200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<object>.Failure(alert.Type, alert.Message, 500);
            }
        }
        public ServiceResult<object> GetIPDPatientOrderDetails(int ftid, AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"GetIPDPatientOrderDetails called. FTID={ftid}");

                var dataTable = _sqlHelper.GetDataTable(
                    "S_GetIPDPatientOrderDetails",
                    CommandType.StoredProcedure,
                    new
                    {
                        @FTID = ftid,
                        @printUserId = globalValues.userId
                    }
                );

                if (dataTable == null || dataTable.Rows.Count == 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info($"No IPD patient order details found for FTID={ftid}");
                    return ServiceResult<object>.Failure(
                        alert.Type,
                        "No IPD patient order details found",
                        404
                    );
                }

                var result = dataTable.AsEnumerable().Select(row =>
                    dataTable.Columns.Cast<DataColumn>().ToDictionary(
                        col => col.ColumnName,
                        col => row[col] == DBNull.Value ? null : row[col]
                    )
                ).ToList();

                _log.Info($"GetIPDPatientOrderDetails retrieved {result.Count} record(s) for FTID={ftid}");

                var alert1 = _messageService.GetMessageAndTypeByAlertCode("OPERATION_COMPLETED_SUCCESSFULLY");
                return ServiceResult<object>.Success(
                    result,
                    alert1.Type,
                    $"{result.Count} order detail(s) retrieved successfully",
                    200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<object>.Failure(alert.Type, alert.Message, 500);
            }
        }


        private ServiceResult<string> UpdateFTDServices(
    int visitId,
    string updateColumns,
    string ftdIdList,
    AllGlobalValues globalValues,
    string filter = null)
        {
            var connectionString = _configuration.GetConnectionString("ConnectionString");
            SqlConnection con = new SqlConnection(connectionString);
            con.Open();
            var tnx = CustomSqlHelper.getSqlTransaction(con);

            try
            {
                _log.Info($"UpdateFTDServices called. VisitId={visitId}, FTDIdList={ftdIdList}");

                _sqlHelper.DML(tnx, "U_CommonUpdateFTDServices", CommandType.StoredProcedure, new
                {
                    @updateColumns = updateColumns,
                    @FTDList = ftdIdList,
                    @filter = (object)filter ?? DBNull.Value,
                    @userId = globalValues.userId,
                    @ipAddress = globalValues.ipAddress
                });

                UpdateIPDBillingByVisitId(tnx, visitId, globalValues);

                tnx.Commit();
                _log.Info($"UpdateFTDServices committed. VisitId={visitId}");

                var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_UPDATED_SUCCESSFULLY");
                return ServiceResult<string>.Success(
                    "IPD billing updated successfully",
                    alert.Type,
                    alert.Message,
                    200
                );
            }
            catch (Exception ex)
            {
                try { tnx.Rollback(); } catch { /* swallow rollback exception */ }
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<string>.Failure(alert.Type, alert.Message, 500);
            }
            finally
            {
                tnx.Dispose();
                if (con.State == ConnectionState.Open)
                    con.Close();
            }
        }

        /// <summary>
        /// Re-syncs PatientBillDetails / FinancialTransactions totals for a visit
        /// against the current FinancialTransactionDetails rows. Kept as a separate,
        /// reusable method (called from UpdateFTDServices and can be called standalone
        /// wherever a visit's billing totals need re-syncing after an FTD change).
        /// </summary>
        private void UpdateIPDBillingByVisitId(SqlTransaction tnx, int visitId, AllGlobalValues globalValues)
        {
            _sqlHelper.DML(tnx, "U_UpdateIPDBillingByVisitDetails", CommandType.StoredProcedure, new
            {
                @visitId = visitId,
                @userId = globalValues.userId,
                @ipAddress = globalValues.ipAddress
            });
        }

        private static string EscapeSql(string value)
        {
            return string.IsNullOrEmpty(value) ? string.Empty : value.Replace("'", "''");
        }

        public ServiceResult<string> RemoveIPDServiceItem(RemoveIPDServiceItemRequest request, AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"RemoveIPDServiceItem called. VisitId={request.VisitId}, FTDIdList={request.FTDIdList}");

                string updateColumns = $"IsCancel=1,CancelReason='{EscapeSql(request.CancelReason)}'";

                return UpdateFTDServices(request.VisitId, updateColumns, request.FTDIdList, globalValues);
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<string>.Failure(alert.Type, alert.Message, 500);
            }
        }

        public ServiceResult<string> UpdateIPDServicePackage(UpdateIPDServicePackageRequest request, AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"UpdateIPDServicePackage called. VisitId={request.VisitId}, FTDIdList={request.FTDIdList}, PackageId={request.PackageId}");

                string updateColumns = request.PackageId == 0
                    ? "IsUnderPackage=0,PackageId=0,GrossAmt=S_GrossAmt,DiscAmt=S_DiscAmt,NetAmt=S_NetAmt"
                    : $"IsUnderPackage=1,PackageId={request.PackageId},GrossAmt=0,DiscAmt=0,NetAmt=0";

                return UpdateFTDServices(request.VisitId, updateColumns, request.FTDIdList, globalValues);
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<string>.Failure(alert.Type, alert.Message, 500);
            }
        }

        public ServiceResult<string> UpdateIPDServiceCorporateNonPayable(UpdateIPDServiceCorporateNonPayableRequest request, AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"UpdateIPDServiceCorporateNonPayable called. VisitId={request.VisitId}, FTDIdList={request.FTDIdList}, IsNonPayable={request.IsNonPayable}");

                string updateColumns = $"IsCorporateNonPayable={request.IsNonPayable}";

                return UpdateFTDServices(request.VisitId, updateColumns, request.FTDIdList, globalValues);
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<string>.Failure(alert.Type, alert.Message, 500);
            }
        }

        public ServiceResult<string> UpdateIPDServiceDiscAmt(UpdateIPDServiceDiscAmtRequest request, AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"UpdateIPDServiceDiscAmt called. VisitId={request.VisitId}, FTDIdList={request.FTDIdList}, DiscAmt={request.DiscAmt}");

                string discAmt = request.DiscAmt.ToString(CultureInfo.InvariantCulture);
                string updateColumns =
                    $"DiscPer=({discAmt}/GrossAmt)*100,DiscAmt={discAmt},NetAmt=(GrossAmt-{discAmt}),DiscountReason='{EscapeSql(request.DiscReason)}'";

                return UpdateFTDServices(request.VisitId, updateColumns, request.FTDIdList, globalValues, "IsUnderPackage=0");
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<string>.Failure(alert.Type, alert.Message, 500);
            }
        }

        public ServiceResult<string> UpdateIPDServiceDiscPer(UpdateIPDServiceDiscPerRequest request, AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"UpdateIPDServiceDiscPer called. VisitId={request.VisitId}, FTDIdList={request.FTDIdList}, DiscPer={request.DiscPer}");

                string discPer = request.DiscPer.ToString(CultureInfo.InvariantCulture);
                string updateColumns =
                    $"DiscPer={discPer},DiscAmt=(GrossAmt*{discPer}/100),NetAmt=(GrossAmt-(GrossAmt*{discPer}/100)),DiscountReason='{EscapeSql(request.DiscReason)}'";

                return UpdateFTDServices(request.VisitId, updateColumns, request.FTDIdList, globalValues, "IsUnderPackage=0");
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<string>.Failure(alert.Type, alert.Message, 500);
            }
        }

        public ServiceResult<string> UpdateIPDServiceRate(UpdateIPDServiceRateRequest request, AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"UpdateIPDServiceRate called. VisitId={request.VisitId}, FTDIdList={request.FTDIdList}, Rate={request.Rate}");

                string rate = request.Rate.ToString(CultureInfo.InvariantCulture);
                string updateColumns =
                    $"Rate={rate},GrossAmt=({rate}*Qty),DiscAmt=({rate}*Qty*DiscPer/100),NetAmt=(({rate}*Qty)-({rate}*Qty*DiscPer/100))";

                return UpdateFTDServices(request.VisitId, updateColumns, request.FTDIdList, globalValues, "IsUnderPackage=0");
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<string>.Failure(alert.Type, alert.Message, 500);
            }
        }

        public ServiceResult<string> UpdateIPDServiceQty(UpdateIPDServiceQtyRequest request, AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"UpdateIPDServiceQty called. VisitId={request.VisitId}, FTDIdList={request.FTDIdList}, Qty={request.Qty}");

                string qty = request.Qty.ToString(CultureInfo.InvariantCulture);
                string updateColumns =
                    $"Qty={qty},GrossAmt=(Rate*{qty}),DiscAmt=(Rate*{qty}*DiscPer/100),NetAmt=((Rate*{qty})-(Rate*{qty}*DiscPer/100))";

                return UpdateFTDServices(request.VisitId, updateColumns, request.FTDIdList, globalValues);
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<string>.Failure(alert.Type, alert.Message, 500);
            }
        }

        public ServiceResult<SaveIPDPatientAdvanceResponse> SaveIPDPatientAdvance(
    SaveIPDPatientAdvanceRequest request,
    AllGlobalValues globalValues)
        {
            var connectionString = _configuration.GetConnectionString("ConnectionString");
            SqlConnection con = new SqlConnection(connectionString);
            con.Open();
            var tnx = CustomSqlHelper.getSqlTransaction(con);

            try
            {
                _log.Info($"SaveIPDPatientAdvance called. Type={request.Type}, PatientId={request.PatientId}, VisitId={request.VisitId}");

                decimal totalPaidAmount = request.PaymentDetails.Sum(p => p.Amount);

                // Refund => negative amount, same as legacy behavior
                if (request.Type.Trim().ToUpper() == "R")
                    totalPaidAmount = (-1) * totalPaidAmount;

                // ── 1. Receipts ──────────────────────────────────────────────────────
                var receipt = new Receipts
                {
                    HospId = globalValues.hospId,
                    BranchId = request.BranchId,
                    RoleId = request.RoleId,
                    BillId = 0,
                    VisitId = request.VisitId,
                    PatientId = request.PatientId,
                    Amount = totalPaidAmount,
                    PlutusTransactionReferenceID = request.PaymentDetails[0].PlutusTransactionReferenceID,
                    TransactionLogId = request.PaymentDetails[0].TransactionLogId,
                    UserId = globalValues.userId,
                    IpAddress = globalValues.ipAddress,
                    UniqueId = request.UniqueId,
                    Remarks = request.Remarks,
                    GuardianName = request.GuardianName
                };

                int receiptId = Convert.ToInt32(receipt.Create(_sqlHelper, tnx));
                _log.Info($"Receipt created for IPD patient advance. ReceiptId={receiptId}, VisitId={request.VisitId}");

                // ── 2. Receipt Payment Mode Details (skip Credit, PaymentModeTypeId == 4) ──
                foreach (var p in request.PaymentDetails)
                {
                    if (p.PaymentModeTypeId == 4)
                        continue;

                    var rpmd = new ReceiptsPaymentModeDetails
                    {
                        HospId = globalValues.hospId,
                        BranchId = request.BranchId,
                        ReceiptID = receiptId,
                        Amount = p.Amount,
                        PaymentModeId = p.PaymentModeId,
                        BankId = p.BankId > 0 ? p.BankId : (int?)null,
                        ReferenceNo = p.RefNo,
                        UserId = globalValues.userId,
                        IpAddress = globalValues.ipAddress
                    };

                    rpmd.Create(_sqlHelper, tnx);
                }

                // ── 3. Recalculate IPD billing totals for the visit ─────────────────
                // Reuses the shared private helper already extracted for IPD billing mutations
                UpdateIPDBillingByVisitId(tnx, request.VisitId, globalValues);


                tnx.Commit();
                _log.Info($"SaveIPDPatientAdvance committed. VisitId={request.VisitId}, ReceiptId={receiptId}");

                var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_SAVED_SUCCESSFULLY");
                return ServiceResult<SaveIPDPatientAdvanceResponse>.Success(
                    new SaveIPDPatientAdvanceResponse
                    {
                        PatientId = request.PatientId,
                        VisitId = request.VisitId,
                        ReceiptId = receiptId
                    },
                    alert.Type,
                    "IPD patient advance saved successfully",
                    201
                );
            }
            catch (Exception ex)
            {
                try { tnx.Rollback(); } catch { /* swallow rollback exception */ }
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<SaveIPDPatientAdvanceResponse>.Failure(alert.Type, alert.Message, 500);
            }
            finally
            {
                tnx.Dispose();
                if (con.State == ConnectionState.Open)
                    con.Close();
            }
        }

        public ServiceResult<IEnumerable<Dictionary<string, object>>> GetIPDReceiptDetails(int receiptId)
        {
            try
            {
                _log.Info($"GetIPDReceiptDetails called. ReceiptId={receiptId}");

                var dataTable = _sqlHelper.GetDataTable(
                    "S_GetIPDReceiptDetails",
                    CommandType.StoredProcedure,
                    new { receiptId = receiptId }
                );

                var result = dataTable.ToRawList();

                if (!result.Any())
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    _log.Info($"No IPD receipt details found for ReceiptId={receiptId}");
                    return ServiceResult<IEnumerable<Dictionary<string, object>>>.Failure(
                        alert.Type,
                        $"No IPD receipt details found for ReceiptId: {receiptId}",
                        404
                    );
                }

                _log.Info($"Retrieved {result.Count} IPD receipt detail row(s) for ReceiptId={receiptId}");

                return ServiceResult<IEnumerable<Dictionary<string, object>>>.Success(
                    result,
                    "Info",
                    "IPD receipt details retrieved successfully",
                    200
                );
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<IEnumerable<Dictionary<string, object>>>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
        }

        // ─── Patient Workflow (visit-specific — never cached) ────────────────

        public ServiceResult<object> InitializePatientDischargeProcess(
            InitializePatientDischargeProcessRequest request, AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"InitializePatientDischargeProcess called. VisitId={request.VisitId}, CorporateId={request.CorporateId}");

                long result = _sqlHelper.RunProcedureInsert(
                    "I_InitializePatientDischargeProcess",
                    new IDataParameter[]
                    {
                        new SqlParameter("@VisitId", request.VisitId),
                        new SqlParameter("@CorporateId", request.CorporateId),
                        new SqlParameter("@UserId", globalValues.userId),
                        new SqlParameter("@IpAddress", (object)globalValues.ipAddress ?? DBNull.Value),
                        new SqlParameter("@Result", SqlDbType.Int) { Direction = ParameterDirection.Output }
                    });

                int resultValue = Convert.ToInt32(result);

                if (resultValue == -2)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    return ServiceResult<object>.Failure(alert.Type, "No applicable active discharge processes are configured", 404);
                }

                if (resultValue == -3)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                    return ServiceResult<object>.Failure(alert.Type, alert.Message, 500);
                }

                // -1 = already initialized (idempotent), >0 = newly initialized — either way return the snapshot
                var workflow = GetPatientDischargeProcess(request.VisitId,request.BranchId, globalValues);
                string message = resultValue == -1
                    ? "Discharge workflow already initialized for this visit"
                    : "Discharge workflow initialized successfully";

                if (!workflow.Result)
                    return workflow;

                var successAlert = _messageService.GetMessageAndTypeByAlertCode(
                    resultValue == -1 ? "OPERATION_COMPLETED_SUCCESSFULLY" : "DATA_SAVED_SUCCESSFULLY");

                return ServiceResult<object>.Success(workflow.Data, successAlert.Type, message, resultValue == -1 ? 200 : 201);
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<object>.Failure(alert.Type, alert.Message, 500);
            }
        }

        public ServiceResult<object> GetPatientDischargeProcess(int visitId,int branchid, AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"GetPatientDischargeProcess called. VisitId={visitId}");

                var dataTable = _sqlHelper.GetDataTable(
                    "S_GetPatientDischargeProcess",
                    CommandType.StoredProcedure,
                    new { 
                        @VisitId = visitId,
                        @UserId= globalValues.userId,
                        @BranchId=branchid

                    });

                if (dataTable == null || dataTable.Rows.Count == 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    return ServiceResult<object>.Failure(alert.Type, "Discharge workflow not initialized for this visit", 404);
                }

                var result = dataTable.AsEnumerable().Select(row =>
                    dataTable.Columns.Cast<DataColumn>().ToDictionary(
                        col => col.ColumnName,
                        col => row[col] == DBNull.Value ? null : row[col]
                    )
                ).ToList();

                var success = _messageService.GetMessageAndTypeByAlertCode("OPERATION_COMPLETED_SUCCESSFULLY");
                return ServiceResult<object>.Success(result, success.Type, $"{result.Count} process(es) retrieved successfully", 200);
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<object>.Failure(alert.Type, alert.Message, 500);
            }
        }

        public ServiceResult<object> GetCurrentDischargeProcess(int visitId)
        {
            try
            {
                _log.Info($"GetCurrentDischargeProcess called. VisitId={visitId}");

                var dataTable = _sqlHelper.GetDataTable(
                    "S_GetCurrentDischargeProcess",
                    CommandType.StoredProcedure,
                    new { @VisitId = visitId });

                if (dataTable == null || dataTable.Rows.Count == 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    return ServiceResult<object>.Failure(alert.Type, "No current process found", 404);
                }

                var row = dataTable.Rows[0];
                var result = dataTable.Columns.Cast<DataColumn>().ToDictionary(
                    col => col.ColumnName,
                    col => row[col] == DBNull.Value ? null : row[col]
                );

                var success = _messageService.GetMessageAndTypeByAlertCode("OPERATION_COMPLETED_SUCCESSFULLY");
                return ServiceResult<object>.Success(result, success.Type, "Current process retrieved successfully", 200);
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<object>.Failure(alert.Type, alert.Message, 500);
            }
        }

        public ServiceResult<object> StartPatientDischargeProcess(
            StartPatientDischargeProcessRequest request, AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"StartPatientDischargeProcess called. VisitId={request.VisitId}, DischargeProcessId={request.DischargeProcessId}");

                long result = _sqlHelper.RunProcedureInsert(
                    "U_StartPatientDischargeProcess",
                    new IDataParameter[]
                    {
                        new SqlParameter("@VisitId", request.VisitId),
                        new SqlParameter("@DischargeProcessId", request.DischargeProcessId),
                        new SqlParameter("@UserId", globalValues.userId),
                        new SqlParameter("@IpAddress", (object)globalValues.ipAddress ?? DBNull.Value),
                        new SqlParameter("@Result", SqlDbType.Int) { Direction = ParameterDirection.Output }
                    });

                int resultValue = Convert.ToInt32(result);
                var (failType, failMessage, failCode) = MapWorkflowErrorCode(resultValue);
                if (failMessage != null)
                    return ServiceResult<object>.Failure(failType, failMessage, failCode);

                var current = GetCurrentDischargeProcess(request.VisitId);
                var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_UPDATED_SUCCESSFULLY");
                return ServiceResult<object>.Success(current.Data, alert.Type, "Process started successfully", 200);
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<object>.Failure(alert.Type, alert.Message, 500);
            }
        }

        public ServiceResult<object> CompletePatientDischargeProcess(
            CompletePatientDischargeProcessRequest request, AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"CompletePatientDischargeProcess called. VisitId={request.VisitId}, DischargeProcessId={request.DischargeProcessId}");

                long result = _sqlHelper.RunProcedureInsert(
                    "U_PatientDischargeProcess",
                    new IDataParameter[]
                    {
                        new SqlParameter("@VisitId", request.VisitId),
                        new SqlParameter("@DischargeProcessId", request.DischargeProcessId),
                        new SqlParameter("@UserId", globalValues.userId),
                        new SqlParameter("@IpAddress", (object)globalValues.ipAddress ?? DBNull.Value),
                        new SqlParameter("@Remarks", (object)request.Remarks ?? DBNull.Value),
                        new SqlParameter("@Result", SqlDbType.Int) { Direction = ParameterDirection.Output }
                    });

                int resultValue = Convert.ToInt32(result);
                var (failType, failMessage, failCode) = MapWorkflowErrorCode(resultValue);
                if (failMessage != null)
                    return ServiceResult<object>.Failure(failType, failMessage, failCode);

                // NOTE: If this ProcessKey requires an existing business operation
                // (e.g. CLOSE_BILLING -> billing-closed check, NURSING_CLEARANCE -> nursing sign-off),
                // call the relevant existing repository/service here (or before this call)
                // as the process-specific handler. The workflow engine itself stays generic.

                var next = GetCurrentDischargeProcess(request.VisitId);
                var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_UPDATED_SUCCESSFULLY");
                return ServiceResult<object>.Success(next.Data, alert.Type, "Process completed successfully", 200);
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<object>.Failure(alert.Type, alert.Message, 500);
            }
        }

        public ServiceResult<object> ValidatePatientDischargeProcess(int visitId)
        {
            try
            {
                _log.Info($"ValidatePatientDischargeProcess called. VisitId={visitId}");

                var dataTable = _sqlHelper.GetDataTable(
                    "S_ValidatePatientDischargeProcess",
                    CommandType.StoredProcedure,
                    new { @VisitId = visitId });

                if (dataTable == null || dataTable.Rows.Count == 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    return ServiceResult<object>.Failure(alert.Type, "Discharge workflow not initialized for this visit", 404);
                }

                var row = dataTable.Rows[0];
                var result = dataTable.Columns.Cast<DataColumn>().ToDictionary(
                    col => col.ColumnName,
                    col => row[col] == DBNull.Value ? null : row[col]
                );

                var success = _messageService.GetMessageAndTypeByAlertCode("OPERATION_COMPLETED_SUCCESSFULLY");
                return ServiceResult<object>.Success(result, success.Type, "Discharge validation completed", 200);
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<object>.Failure(alert.Type, alert.Message, 500);
            }
        }

        /// <summary>
        /// Shared mapping for U_StartPatientDischargeProcess / U_PatientDischargeProcess result codes.
        /// Returns (null,null,0) when the code indicates success (>0).
        /// </summary>
        private (string Type, string Message, int StatusCode) MapWorkflowErrorCode(int resultValue)
        {
            switch (resultValue)
            {
                case -1:
                    return (_messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER").Type,
                        "This process is not part of the patient's discharge workflow", 400);
                case -2:
                    return (_messageService.GetMessageAndTypeByAlertCode("OPERATION_FAILED").Type,
                        "This discharge process has already been completed", 409);
                case -3:
                    return (_messageService.GetMessageAndTypeByAlertCode("OPERATION_FAILED").Type,
                        "Previous discharge process is not completed. Only the current process can be executed", 409);
                case -99:
                    return (_messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND").Type,
                        "Server error while processing discharge workflow step", 500);
                default:
                    return (null, null, 0);
            }
        }


      

        public ServiceResult<string> SaveIPDDischarge(SaveIPDDischargeRequest request, AllGlobalValues globalValues)
        {
            var connectionString = _configuration.GetConnectionString("ConnectionString");
            SqlConnection con = new SqlConnection(connectionString);
            con.Open();
            var tnx = CustomSqlHelper.getSqlTransaction(con);

            try
            {
                _log.Info($"SaveIPDDischarge called. VisitId={request.VisitId}, BedId={request.BedId}, DischargeType={request.DischargeType}");

                // Parse discharge date/time
                if (!DateTime.TryParse(request.DischargeDate, out DateTime dischargeDate))
                {
                    tnx.Rollback();
                    var alertDate = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                    return ServiceResult<string>.Failure(alertDate.Type, "Invalid DischargeDate format", 400);
                }

                if (!DateTime.TryParse(request.DischargeTime, out DateTime dischargeTime))
                {
                    tnx.Rollback();
                    var alertTime = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                    return ServiceResult<string>.Failure(alertTime.Type, "Invalid DischargeTime format", 400);
                }

                // Parse optional death date/time (legacy fields for U_UpdateIPDPatientDischarge)
                object deathDateParam = DBNull.Value;
                object deathTimeParam = DBNull.Value;

              

                // ── 1. Update PatientVisitDetails discharge info (common header — unchanged SP) ──
                _sqlHelper.DML(tnx, "U_UpdateIPDPatientDischarge", CommandType.StoredProcedure, new
                {
                    @dischargeDate = dischargeDate.Date,
                    @dischargeTime = dischargeTime.TimeOfDay,
                    @dischargeType = request.DischargeType,
                    @visitId = request.VisitId,
                    @userId = globalValues.userId,
                    @IpAddress = globalValues.ipAddress
                });

                // ── 2. Insert/Update exactly the ONE detail table matching the discharge type ──
                //     Each IU_*DischargeDetails SP upserts by VisitId (update if exists, else insert)

                if (request.NormalDischargeDetails != null)
                {
                    var d = request.NormalDischargeDetails;
                    DateTime? followUpDate = null;
                    if (!string.IsNullOrWhiteSpace(d.FollowUpDate) && DateTime.TryParse(d.FollowUpDate, out DateTime fd))
                        followUpDate = fd.Date;

                    _sqlHelper.DML(tnx, "IU_NormalDischargeDetails", CommandType.StoredProcedure, new
                    {
                        @visitId = request.VisitId,
                        @conditionAtDischarge = d.ConditionAtDischarge ?? (object)DBNull.Value,
                        @dischargeAdvice = d.DischargeAdvice ?? (object)DBNull.Value,
                        @followUpDate = (object)followUpDate ?? DBNull.Value,
                        @followUpDepartmentId = (object)d.FollowUpDepartmentId ?? DBNull.Value,
                        @followUpDoctorId = (object)d.FollowUpDoctorId ?? DBNull.Value,
                        @userId = globalValues.userId,
                        @IpAddress = globalValues.ipAddress
                    });
                    _log.Info($"NormalDischargeDetails saved for VisitId={request.VisitId}");
                }
                else if (request.LAMADischargeDetails != null)
                {
                    var d = request.LAMADischargeDetails;
                    DateTime? otpVerifiedOn = null;
                    if (!string.IsNullOrWhiteSpace(d.OtpVerifiedOn) && DateTime.TryParse(d.OtpVerifiedOn, out DateTime ov))
                        otpVerifiedOn = ov;

                    _sqlHelper.DML(tnx, "IU_LAMADischargeDetails", CommandType.StoredProcedure, new
                    {
                        @visitId = request.VisitId,
                        @reason = d.Reason ?? (object)DBNull.Value,
                        @isRiskExplained = (object)d.IsRiskExplained ?? DBNull.Value,
                        @declarationText = d.DeclarationText ?? (object)DBNull.Value,
                        @counsellingByDoctorId = (object)d.CounsellingByDoctorId ?? DBNull.Value,
                        @relativeName = d.RelativeName ?? (object)DBNull.Value,
                        @relationship = d.Relationship ?? (object)DBNull.Value,
                        @signatureFilePath = d.SignatureFilePath ?? (object)DBNull.Value,
                        @isOtpVerified = (object)d.IsOtpVerified ?? DBNull.Value,
                        @otpVerifiedOn = (object)otpVerifiedOn ?? DBNull.Value,
                        @userId = globalValues.userId,
                        @IpAddress = globalValues.ipAddress
                    });
                    _log.Info($"LAMADischargeDetails saved for VisitId={request.VisitId}");
                }
                else if (request.TransferDischargeDetails != null)
                {
                    var d = request.TransferDischargeDetails;

                    _sqlHelper.DML(tnx, "IU_TransferDischargeDetails", CommandType.StoredProcedure, new
                    {
                        @visitId = request.VisitId,
                        @transferHospitalName = d.TransferHospitalName ?? (object)DBNull.Value,
                        @transferReason = d.TransferReason ?? (object)DBNull.Value,
                        @conditionAtTransfer = d.ConditionAtTransfer ?? (object)DBNull.Value,
                        @isAmbulanceRequired = (object)d.IsAmbulanceRequired ?? DBNull.Value,
                        @accompanyingStaffUserId = (object)d.AccompanyingStaffUserId ?? DBNull.Value,
                        @referralLetterFilePath = d.ReferralLetterFilePath ?? (object)DBNull.Value,
                        @userId = globalValues.userId,
                        @IpAddress = globalValues.ipAddress
                    });
                    _log.Info($"TransferDischargeDetails saved for VisitId={request.VisitId}");
                }
                else if (request.DeathDischargeDetails != null)
                {
                    var d = request.DeathDischargeDetails;
                    DateTime? dateOfDeath = null;
                    if (!string.IsNullOrWhiteSpace(d.DateOfDeath) && DateTime.TryParse(d.DateOfDeath, out DateTime dod))
                        dateOfDeath = dod.Date;

                    TimeSpan? timeOfDeath = null;
                    if (!string.IsNullOrWhiteSpace(d.TimeOfDeath) && DateTime.TryParse(d.TimeOfDeath, out DateTime tod))
                        timeOfDeath = tod.TimeOfDay;

                    _sqlHelper.DML(tnx, "IU_DeathDischargeDetails", CommandType.StoredProcedure, new
                    {
                        @visitId = request.VisitId,
                        @dateOfDeath = (object)dateOfDeath ?? DBNull.Value,
                        @timeOfDeath = (object)timeOfDeath ?? DBNull.Value,
                        @causeOfDeath = d.CauseOfDeath ?? (object)DBNull.Value,
                        @deathSummary = d.DeathSummary ?? (object)DBNull.Value,
                        @certificateStatus = d.CertificateStatus ?? (object)DBNull.Value,
                        @bodyHandoverDetails = d.BodyHandoverDetails ?? (object)DBNull.Value,
                        @relativeName = d.RelativeName ?? (object)DBNull.Value,
                        @relationship = d.Relationship ?? (object)DBNull.Value,
                        @contactNumber = d.ContactNumber ?? (object)DBNull.Value,
                        @userId = globalValues.userId,
                        @IpAddress = globalValues.ipAddress
                    });
                    _log.Info($"DeathDischargeDetails saved for VisitId={request.VisitId}");
                }
                else if (request.AbscondedDischargeDetails != null)
                {
                    var d = request.AbscondedDischargeDetails;
                    DateTime? lastSeenDate = null;
                    if (!string.IsNullOrWhiteSpace(d.LastSeenDate) && DateTime.TryParse(d.LastSeenDate, out DateTime lsd))
                        lastSeenDate = lsd.Date;

                    TimeSpan? lastSeenTime = null;
                    if (!string.IsNullOrWhiteSpace(d.LastSeenTime) && DateTime.TryParse(d.LastSeenTime, out DateTime lst))
                        lastSeenTime = lst.TimeOfDay;

                    _sqlHelper.DML(tnx, "IU_AbscondedDischargeDetails", CommandType.StoredProcedure, new
                    {
                        @visitId = request.VisitId,
                        @lastSeenDate = (object)lastSeenDate ?? DBNull.Value,
                        @lastSeenTime = (object)lastSeenTime ?? DBNull.Value,
                        @circumstances = d.Circumstances ?? (object)DBNull.Value,
                        @isStaffInformed = (object)d.IsStaffInformed ?? DBNull.Value,
                        @isPoliceInformed = (object)d.IsPoliceInformed ?? DBNull.Value,
                        @remarks = d.Remarks ?? (object)DBNull.Value,
                        @firNo = d.FIRNo ?? (object)DBNull.Value,
                        @userId = globalValues.userId,
                        @IpAddress = globalValues.ipAddress
                    });
                    _log.Info($"AbscondedDischargeDetails saved for VisitId={request.VisitId}");
                }

                // ── 3. Free up the bed ──────────────────────────────────────────────
                _sqlHelper.DML(tnx, "U_UpdateBedStatus", CommandType.StoredProcedure, new
                {
                    @bedId = request.BedId,
                    @currentStatus = 0 //BedStatus_Available
                });

                tnx.Commit();
                _log.Info($"SaveIPDDischarge committed. VisitId={request.VisitId}, BedId={request.BedId}");

                var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_UPDATED_SUCCESSFULLY");
                return ServiceResult<string>.Success(
                    "Patient discharged successfully",
                    alert.Type,
                    alert.Message,
                    200
                );
            }
            catch (Exception ex)
            {
                try { tnx.Rollback(); } catch { /* swallow rollback exception */ }
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<string>.Failure(alert.Type, alert.Message, 500);
            }
            finally
            {
                tnx.Dispose();
                if (con.State == ConnectionState.Open)
                    con.Close();
            }
        }


        public ServiceResult<CreateSupplementaryBillFromMainBillResponse> CreateSupplementaryBillFromMainBill(
 CreateSupplementaryBillFromMainBillRequest request,
 AllGlobalValues globalValues)
        {
            var connectionString = _configuration.GetConnectionString("ConnectionString");
            SqlConnection con = new SqlConnection(connectionString);
            con.Open();
            var tnx = CustomSqlHelper.getSqlTransaction(con);

            try
            {
                _log.Info($"CreateSupplementaryBillFromMainBill called. PatientId={request.VisitDetails.PatientId}, VisitId={request.VisitDetails.VisitId}, BranchId={request.VisitDetails.BranchId}");

                var v = request.VisitDetails;
                int visitId = v.VisitId;

                decimal totalPaidAmount = 0;
                if (request.PaymentDetails?.Count > 0)
                {
                    totalPaidAmount = request.PaymentDetails.Sum(p => p.Amount);
                }

                // ── 1. PatientBillDetails ────────────────────────────────────────────
                var pbd = new PatientBillDetails
                {
                    HospId = globalValues.hospId,
                    BranchId = v.BranchId,
                    RoleId = v.RoleId,
                    PatientId = v.PatientId,
                    VisitId = visitId,
                    TypeId = 2,                                // 2 = IPD
                    TotalBillAmount = v.GrossBillAmount,
                    TotalDiscountPerOnBill = v.TotalDiscPerOnBill,
                    TotalDiscountAmountOnBill = v.TotalDiscAmtOnBill,
                    DiscountApprovedById = v.DiscApprovedById > 0 ? v.DiscApprovedById : (int?)null,
                    DiscountReason = v.DiscountReason,
                    RoundOff = v.RoundOff,
                    TotalPayableAmount = v.NetAmount,
                    TotalPaidAmount = totalPaidAmount,
                    TotalBalanceAmount = v.NetAmount - totalPaidAmount,
                    TotalPatientPayableAmount = v.NetAmount,
                    TotalCorporatePayableAmount = 0,
                    TotalPatientPaidAmount = totalPaidAmount,
                    TotalCorporatePaidAmount = 0,
                    IsSupplementaryBill = 1,
                    UserId = globalValues.userId,
                    IpAddress = globalValues.ipAddress
                };

                int billId = Convert.ToInt32(pbd.Create(_sqlHelper, tnx));
                _log.Info($"PatientBillDetails created. BillId={billId}");

                // ── 2. Split billing items by FTId, decide "full move" vs "partial move" ────
                var ftIdGroups = request.BillingItems
                    .GroupBy(i => i.FTId)
                    .ToList();

                var fullyCoveredFtIds = new List<int>();
                var partiallyAffectedFtIds = new HashSet<int>(); // old FTIds that lose only some FTDs
                var partialFtdIds = new List<int>();

                foreach (var group in ftIdGroups)
                {
                    int ftId = group.Key;
                    var payloadFtdIds = group.Select(i => i.FTDId).Distinct().ToList();

                    var dbFtdIdsTable = _sqlHelper.GetDataTable(
                        tnx,
                        "S_GetActiveFTDIdsByFTId",
                        CommandType.StoredProcedure,
                        new { @FTId = ftId }
                    );

                    var dbFtdIds = dbFtdIdsTable.AsEnumerable()
                        .Select(r => Convert.ToInt32(r["FTDID"]))
                        .ToList();

                    bool allCovered = dbFtdIds.Count > 0 && dbFtdIds.All(id => payloadFtdIds.Contains(id));

                    if (allCovered)
                    {
                        fullyCoveredFtIds.Add(ftId);
                    }
                    else
                    {
                        _log.Warn($"FTId={ftId} not fully covered by payload. DbCount={dbFtdIds.Count}, PayloadCount={payloadFtdIds.Count}. Will split.");
                        partialFtdIds.AddRange(payloadFtdIds);
                        partiallyAffectedFtIds.Add(ftId); // the OLD ftid loses these FTDs, its totals shrink
                    }
                }

                // 2a. Fully covered FTIds -> just repoint FinancialTransactions.BillId (totals unchanged)
                foreach (var ftIdToUpdate in fullyCoveredFtIds)
                {
                    _sqlHelper.DML(
                        tnx,
                        "U_UpdateFinancialTransactionsBillId",
                        CommandType.StoredProcedure,
                        new
                        {
                            @FTId = ftIdToUpdate,
                            @billId = billId,
                            @UserId = globalValues.userId,
                            @IpAddress = globalValues.ipAddress
                        }
                    );
                    _log.Info($"FinancialTransactions.BillId updated for fully covered FTID={ftIdToUpdate}, BillId={billId}");
                }

                // 2b. Partially covered FTDIds -> new FinancialTransactions row + repoint those FTDIds
                int? newFtId = null;
                if (partialFtdIds.Any())
                {
                    var distinctPartialFtdIds = partialFtdIds.Distinct().ToList();

                    var ft = new FinancialTransactions
                    {
                        HospId = globalValues.hospId,
                        BranchId = v.BranchId,
                        VisitId = visitId,
                        BillId = billId,
                        PatientId = v.PatientId,
                        tnxType = TnxType.IPDBilling,
                        GrossAmount = v.GrossBillAmount,
                        DiscountPercentage = v.TotalDiscPerOnBill,
                        DiscountAmount = v.TotalDiscAmtOnBill,
                        RoundOff = v.RoundOff,
                        NetAmount = v.NetAmount,
                        Remarks = v.Remarks,
                        UserId = globalValues.userId,
                        IpAddress = globalValues.ipAddress,
                        UniqueId = v.UniqueId
                    };

                    newFtId = Convert.ToInt32(ft.Create(_sqlHelper, tnx));
                    _log.Info($"New FinancialTransactions created for partial FTD split. NewFTID={newFtId}, FTDCount={distinctPartialFtdIds.Count}");

                    string ftdIdList = string.Join(",", distinctPartialFtdIds);

                    _sqlHelper.DML(
                        tnx,
                        "U_UpdateFinancialTransactionDetailsFTID",
                        CommandType.StoredProcedure,
                        new
                        {
                            @FTDIdList = ftdIdList,
                            @FTID = newFtId.Value,
                            @UserId = globalValues.userId,
                            @IpAddress = globalValues.ipAddress
                        }
                    );

                    _log.Info($"FinancialTransactionDetails re-pointed to NewFTID={newFtId} for FTDIds={ftdIdList}");
                }

                // ── 4. Receipt ───────────────────────────────────────────────────────
                int receiptId = 0;
                bool isReceipt = false;
                if (totalPaidAmount > 0)
                {
                    var receipt = new Receipts
                    {
                        HospId = globalValues.hospId,
                        BranchId = v.BranchId,
                        RoleId = v.RoleId,
                        BillId = billId,
                        VisitId = visitId,
                        PatientId = v.PatientId,
                        Amount = totalPaidAmount,
                        IsCopaymentReceipt = 0,
                        PlutusTransactionReferenceID = request.PaymentDetails[0].PlutusTransactionReferenceID,
                        TransactionLogId = request.PaymentDetails[0].TransactionLogId,
                        UserId = globalValues.userId,
                        IpAddress = globalValues.ipAddress,
                        UniqueId = v.UniqueId
                    };

                    receiptId = Convert.ToInt32(receipt.Create(_sqlHelper, tnx));
                    _log.Info($"Receipt created. ReceiptId={receiptId}");

                    foreach (var p in request.PaymentDetails)
                    {
                        // PaymentModeTypeId 4 = Credit → skip
                        if (p.PaymentModeTypeId == 4)
                            continue;

                        if (p.IsPatientAdvanceAmount == 1)
                            continue;

                        var rpmd = new ReceiptsPaymentModeDetails
                        {
                            HospId = globalValues.hospId,
                            BranchId = v.BranchId,
                            ReceiptID = receiptId,
                            Amount = p.Amount,
                            PaymentModeId = p.PaymentModeId,
                            BankId = p.BankId > 0 ? p.BankId : (int?)null,
                            ReferenceNo = p.RefNo,
                            UserId = globalValues.userId,
                            IpAddress = globalValues.ipAddress
                        };

                        rpmd.Create(_sqlHelper, tnx);
                    }

                    isReceipt = true;
                }


                foreach (var oldFtId in partiallyAffectedFtIds)
                {
                    UpdateFinancialTransactionByFtId(tnx, oldFtId, globalValues);
                    _log.Info($"Recalculated FinancialTransactions totals for old FTID={oldFtId} after FTD split");
                }

                if (newFtId.HasValue)
                {
                    UpdateFinancialTransactionByFtId(tnx, newFtId.Value, globalValues);
                    _log.Info($"Recalculated FinancialTransactions totals for new FTID={newFtId.Value} after FTD split");
                }


                UpdateIPDBillingByVisitId(tnx, v.VisitId, globalValues);


                tnx.Commit();

                var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_SAVED_SUCCESSFULLY");
                return ServiceResult<CreateSupplementaryBillFromMainBillResponse>.Success(
                    new CreateSupplementaryBillFromMainBillResponse
                    {
                        VisitId = visitId,
                        FTID = 0,
                        ReceiptId = receiptId,
                        IsReceipt = isReceipt
                    },
                    alert.Type,
                    "IPD Billing saved successfully",
                    201
                );
            }
            catch (Exception ex)
            {
                try { tnx.Rollback(); } catch { /* swallow rollback exception */ }
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<CreateSupplementaryBillFromMainBillResponse>.Failure(
                    alert.Type,
                    alert.Message,
                    500
                );
            }
            finally
            {
                tnx.Dispose();
                if (con.State == ConnectionState.Open)
                    con.Close();
            }
        }


        private void UpdateFinancialTransactionByFtId(SqlTransaction tnx, int ftId, AllGlobalValues globalValues)
        {
            _sqlHelper.DML(tnx, "U_UpdateFinancialTransactionByFTId", CommandType.StoredProcedure, new
            {
                @FTId = ftId,
                @UserId = globalValues.userId,
                @IpAddress = globalValues.ipAddress
            });
        }

        // ─── Patient OT Workflow (visit-specific — never cached) ───────────────────

        public ServiceResult<object> InitializePatientOTProcess(InitializePatientOTProcessRequest request, AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"InitializePatientOTProcess called. VisitId={request.VisitId}");

                long result = _sqlHelper.RunProcedureInsert(
                    "I_InitializePatientOTProcess",
                    new IDataParameter[]
                    {
                new SqlParameter("@VisitId", request.VisitId),
                new SqlParameter("@UserId", globalValues.userId),
                new SqlParameter("@IpAddress", (object)globalValues.ipAddress ?? DBNull.Value),
                new SqlParameter("@Result", SqlDbType.Int) { Direction = ParameterDirection.Output }
                    });

                int resultValue = Convert.ToInt32(result);

                if (resultValue == -2)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    return ServiceResult<object>.Failure(alert.Type, "No active OT processes are configured", 404);
                }

                if (resultValue == -3)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                    return ServiceResult<object>.Failure(alert.Type, alert.Message, 500);
                }

                var workflow = GetPatientOTProcess(request.VisitId);
                if (!workflow.Result)
                    return workflow;

                string message = resultValue == -1
                    ? "OT workflow already initialized for this visit"
                    : "OT workflow initialized successfully";

                var successAlert = _messageService.GetMessageAndTypeByAlertCode(
                    resultValue == -1 ? "OPERATION_COMPLETED_SUCCESSFULLY" : "DATA_SAVED_SUCCESSFULLY");

                return ServiceResult<object>.Success(workflow.Data, successAlert.Type, message, resultValue == -1 ? 200 : 201);
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<object>.Failure(alert.Type, alert.Message, 500);
            }
        }

        public ServiceResult<object> GetPatientOTProcess(int visitId)
        {
            try
            {
                _log.Info($"GetPatientOTProcess called. VisitId={visitId}");

                var dataTable = _sqlHelper.GetDataTable(
                    "S_GetPatientOTProcess", CommandType.StoredProcedure, new { @VisitId = visitId });

                if (dataTable == null || dataTable.Rows.Count == 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    return ServiceResult<object>.Failure(alert.Type, "OT workflow not initialized for this visit", 404);
                }

                var result = dataTable.AsEnumerable().Select(row =>
                    dataTable.Columns.Cast<DataColumn>().ToDictionary(
                        col => col.ColumnName,
                        col => row[col] == DBNull.Value ? null : row[col]
                    )
                ).ToList();

                var success = _messageService.GetMessageAndTypeByAlertCode("OPERATION_COMPLETED_SUCCESSFULLY");
                return ServiceResult<object>.Success(result, success.Type, $"{result.Count} process(es) retrieved successfully", 200);
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<object>.Failure(alert.Type, alert.Message, 500);
            }
        }

        public ServiceResult<object> GetCurrentOTProcess(int visitId)
        {
            try
            {
                _log.Info($"GetCurrentOTProcess called. VisitId={visitId}");

                var dataTable = _sqlHelper.GetDataTable(
                    "S_GetCurrentOTProcess", CommandType.StoredProcedure, new { @VisitId = visitId });

                if (dataTable == null || dataTable.Rows.Count == 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    return ServiceResult<object>.Failure(alert.Type, "No current OT process found", 404);
                }

                var row = dataTable.Rows[0];
                var result = dataTable.Columns.Cast<DataColumn>().ToDictionary(
                    col => col.ColumnName,
                    col => row[col] == DBNull.Value ? null : row[col]
                );

                var success = _messageService.GetMessageAndTypeByAlertCode("OPERATION_COMPLETED_SUCCESSFULLY");
                return ServiceResult<object>.Success(result, success.Type, "Current OT process retrieved successfully", 200);
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<object>.Failure(alert.Type, alert.Message, 500);
            }
        }

        public ServiceResult<object> CompletePatientOTProcess(CompletePatientOTProcessRequest request, AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"CompletePatientOTProcess called. VisitId={request.VisitId}, OTProcessId={request.OTProcessId}");

                long result = _sqlHelper.RunProcedureInsert(
                    "U_PatientOTProcess",
                    new IDataParameter[]
                    {
                new SqlParameter("@VisitId", request.VisitId),
                new SqlParameter("@OTProcessId", request.OTProcessId),
                new SqlParameter("@UserId", globalValues.userId),
                new SqlParameter("@IpAddress", (object)globalValues.ipAddress ?? DBNull.Value),
                new SqlParameter("@Remarks", (object)request.Remarks ?? DBNull.Value),
                new SqlParameter("@Result", SqlDbType.Int) { Direction = ParameterDirection.Output }
                    });

                int resultValue = Convert.ToInt32(result);
                var (failType, failMessage, failCode) = MapOTWorkflowErrorCode(resultValue);
                if (failMessage != null)
                    return ServiceResult<object>.Failure(failType, failMessage, failCode);

                // NOTE: dispatch on request-resolved ProcessKey here if a step needs an
                // existing business operation (e.g. OT_NOTES -> save OT notes SP), same
                // pattern as discharge process handlers. Workflow engine stays generic.

                var next = GetCurrentOTProcess(request.VisitId);
                var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_UPDATED_SUCCESSFULLY");
                return ServiceResult<object>.Success(next.Data, alert.Type, "OT process completed successfully", 200);
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<object>.Failure(alert.Type, alert.Message, 500);
            }
        }

        public ServiceResult<object> ValidatePatientOTProcess(int visitId)
        {
            try
            {
                _log.Info($"ValidatePatientOTProcess called. VisitId={visitId}");

                var dataTable = _sqlHelper.GetDataTable(
                    "S_ValidatePatientOTProcess", CommandType.StoredProcedure, new { @VisitId = visitId });

                if (dataTable == null || dataTable.Rows.Count == 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    return ServiceResult<object>.Failure(alert.Type, "OT workflow not initialized for this visit", 404);
                }

                var row = dataTable.Rows[0];
                var result = dataTable.Columns.Cast<DataColumn>().ToDictionary(
                    col => col.ColumnName,
                    col => row[col] == DBNull.Value ? null : row[col]
                );

                var success = _messageService.GetMessageAndTypeByAlertCode("OPERATION_COMPLETED_SUCCESSFULLY");
                return ServiceResult<object>.Success(result, success.Type, "OT validation completed", 200);
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<object>.Failure(alert.Type, alert.Message, 500);
            }
        }

        private (string Type, string Message, int StatusCode) MapOTWorkflowErrorCode(int resultValue)
        {
            switch (resultValue)
            {
                case -1:
                    return (_messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER").Type,
                        "This process is not part of the patient's OT workflow", 400);
                case -2:
                    return (_messageService.GetMessageAndTypeByAlertCode("OPERATION_FAILED").Type,
                        "This OT process has already been completed", 409);
                case -3:
                    return (_messageService.GetMessageAndTypeByAlertCode("OPERATION_FAILED").Type,
                        "Previous OT process is not completed. Only the current process can be executed", 409);
                case -99:
                    return (_messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND").Type,
                        "Server error while processing OT workflow step", 500);
                default:
                    return (null, null, 0);
            }
        }
    }
}