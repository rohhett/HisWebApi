using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.Json;
using HISWEBAPI.Configuration;
using HISWEBAPI.Data.Helpers;
using HISWEBAPI.DTO;
using HISWEBAPI.Exceptions;
using HISWEBAPI.Models;
using HISWEBAPI.Utilities;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Caching.Distributed;

namespace HISWEBAPI.Repositories.Implementations
{
    /// <summary>
    /// Lab Formula Master redesign (owner, 2026-11-27): formulas and result validations per test.
    ///  * Formula = test + target observation + gender (B/M/F); result entry uses the patient's gender row, else B.
    ///    Language and evaluation: Utilities/LabFormulaEngine.cs (same as src/utils/labFormulaEngine.ts).
    ///  * Validation = test + 1..n observations + check (SUM, DIFFERENCE, PRODUCT, RATIO, AVERAGE, MIN, MAX, EACH,
    ///    EXPRESSION) + condition; ActionType 1 blocks the result save (checked again here on save), 2 only warns.
    /// SPs: script Database/2026-11-27_LabFormulaValidationSetup.sql.
    /// </summary>
    public partial class LabRepository
    {
        private static string FormulaSetupCacheKey(int investigationId) => $"_LabFormulaSetup_Inv{investigationId}";
        private const string FormulaSetupCachePattern = "_LabFormulaSetup_Inv*";

        #region Reads

        public ServiceResult<LabFormulaSetupModel> GetInvestigationFormulaSetup(int investigationId)
        {
            try
            {
                _log.Info($"GetInvestigationFormulaSetup called. InvestigationId={investigationId}");
                var model = LoadFormulaSetup(investigationId);
                return ServiceResult<LabFormulaSetupModel>.Success(model, "Info",
                    $"{model.Formulas.Count} formula(s) and {model.Validations.Count} validation(s) retrieved successfully", 200);
            }
            catch (Exception ex)
            {
                return FormulaServerError<LabFormulaSetupModel>(ex);
            }
        }

        public ServiceResult<ResultEntryFormulaSetupModel> GetResultEntryFormulaSetup(int patientInvestigationId)
        {
            try
            {
                _log.Info($"GetResultEntryFormulaSetup called. PatientInvestigationId={patientInvestigationId}");
                var model = LoadResultEntrySetup(patientInvestigationId);
                return ServiceResult<ResultEntryFormulaSetupModel>.Success(model, "Info",
                    $"{model.Formulas.Count} formula(s) and {model.Validations.Count} validation(s) retrieved successfully", 200);
            }
            catch (Exception ex)
            {
                return FormulaServerError<ResultEntryFormulaSetupModel>(ex);
            }
        }

        private LabFormulaSetupModel LoadFormulaSetup(int investigationId)
        {
            string key = FormulaSetupCacheKey(investigationId);
            var cached = _distributedCache.GetString(key);
            if (!string.IsNullOrEmpty(cached))
                return JsonSerializer.Deserialize<LabFormulaSetupModel>(cached) ?? new LabFormulaSetupModel();

            var ds = _sqlHelper.GetDataSet("S_GetLabInvestigationFormulaSetup", CommandType.StoredProcedure,
                new { investigationId });
            var model = new LabFormulaSetupModel
            {
                Formulas = MapFormulas(ds.Tables.Count > 0 ? ds.Tables[0] : null),
                Validations = MapValidations(ds.Tables.Count > 1 ? ds.Tables[1] : null, ds.Tables.Count > 2 ? ds.Tables[2] : null)
            };
            _distributedCache.SetString(key, JsonSerializer.Serialize(model), new DistributedCacheEntryOptions());
            return model;
        }

        private ResultEntryFormulaSetupModel LoadResultEntrySetup(int patientInvestigationId)
        {
            var ds = _sqlHelper.GetDataSet("S_GetResultEntryFormulaSetup", CommandType.StoredProcedure,
                new { patientInvestigationId });
            var model = new ResultEntryFormulaSetupModel();
            if (ds.Tables.Count > 0 && ds.Tables[0].Rows.Count > 0)
            {
                var r = ds.Tables[0].Rows[0];
                model.InvestigationId = r.Field<int>("InvestigationId");
                model.Gender = r.Field<string>("Gender") ?? "B";
                model.AgeYears = r.Field<int?>("AgeYears");
                model.AgeMonths = r.Field<int?>("AgeMonths");
                model.AgeDays = r.Field<int?>("AgeDays");
            }
            model.Formulas = MapFormulas(ds.Tables.Count > 1 ? ds.Tables[1] : null);
            model.Validations = MapValidations(ds.Tables.Count > 2 ? ds.Tables[2] : null, ds.Tables.Count > 3 ? ds.Tables[3] : null);
            return model;
        }

        private static string? OptionalString(DataRow row, string column) =>
            row.Table.Columns.Contains(column) ? row.Field<string>(column) : null;

        private static List<LabInvestigationFormulaModel> MapFormulas(DataTable? dt) =>
            dt?.AsEnumerable().Select(row => new LabInvestigationFormulaModel
            {
                FormulaId = row.Field<int>("FormulaId"),
                InvestigationId = dt.Columns.Contains("InvestigationId") ? row.Field<int>("InvestigationId") : 0,
                ObservationId = row.Field<int>("ObservationId"),
                ObservationName = row.Field<string>("ObservationName") ?? "",
                Gender = row.Field<string>("Gender") ?? "B",
                Expression = row.Field<string>("Expression") ?? "",
                FormulaText = row.Field<string>("FormulaText") ?? "",
                DecimalPlaces = row.Field<int>("DecimalPlaces"),
                IsActive = dt.Columns.Contains("IsActive") ? row.Field<int>("IsActive") : 1,
                CreatedBy = OptionalString(row, "CreatedBy"),
                CreatedOn = OptionalString(row, "CreatedOn"),
                LastModifiedBy = OptionalString(row, "LastModifiedBy"),
                LastModifiedOn = OptionalString(row, "LastModifiedOn")
            }).ToList() ?? new List<LabInvestigationFormulaModel>();

        private static List<LabInvestigationValidationModel> MapValidations(DataTable? dt, DataTable? obs)
        {
            var obsByRule = obs?.AsEnumerable()
                .GroupBy(r => r.Field<int>("ValidationId"))
                .ToDictionary(g => g.Key, g => g.Select(r => new LabValidationObservationModel
                {
                    ObservationId = r.Field<int>("ObservationId"),
                    ObservationName = r.Field<string>("ObservationName") ?? "",
                    SequenceNo = r.Field<int>("SequenceNo")
                }).OrderBy(o => o.SequenceNo).ToList()) ?? new Dictionary<int, List<LabValidationObservationModel>>();

            return dt?.AsEnumerable().Select(row => new LabInvestigationValidationModel
            {
                ValidationId = row.Field<int>("ValidationId"),
                InvestigationId = dt.Columns.Contains("InvestigationId") ? row.Field<int>("InvestigationId") : 0,
                ValidationName = row.Field<string>("ValidationName") ?? "",
                Gender = row.Field<string>("Gender") ?? "B",
                CheckType = row.Field<string>("CheckType") ?? "",
                Expression = row.Field<string>("Expression") ?? "",
                ExpressionText = row.Field<string>("ExpressionText") ?? "",
                Operator = row.Field<string>("Operator") ?? "",
                Value1 = row.Field<decimal>("Value1"),
                Value2 = row.Field<decimal?>("Value2"),
                Tolerance = row.Field<decimal>("Tolerance"),
                ActionType = row.Field<int>("ActionType"),
                SkipIfBlank = row.Field<int>("SkipIfBlank"),
                ErrorMessage = row.Field<string>("ErrorMessage") ?? "",
                IsActive = dt.Columns.Contains("IsActive") ? row.Field<int>("IsActive") : 1,
                CreatedBy = OptionalString(row, "CreatedBy"),
                CreatedOn = OptionalString(row, "CreatedOn"),
                LastModifiedBy = OptionalString(row, "LastModifiedBy"),
                LastModifiedOn = OptionalString(row, "LastModifiedOn"),
                Observations = obsByRule.TryGetValue(row.Field<int>("ValidationId"), out var list) ? list : new()
            }).ToList() ?? new List<LabInvestigationValidationModel>();
        }

        #endregion

        #region Formula writes

        public ServiceResult<LabFormulaSetupSaveResponse> CreateUpdateInvestigationFormula(CreateUpdateInvestigationFormulaRequest request, AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"CreateUpdateInvestigationFormula called. FormulaId={request.FormulaId}, InvestigationId={request.InvestigationId}, ObservationId={request.ObservationId}, Gender={request.Gender}");

                string expression = (request.Expression ?? "").Trim();
                if (!LabFormulaEngine.TryParse(expression, out _, out var usedIds, out var parseError))
                    return FormulaInvalid<LabFormulaSetupSaveResponse>($"Formula is not valid: {parseError}");

                // a formula may use another calculated observation, but never in a circle (A uses B, B uses A)
                var setup = LoadFormulaSetup(request.InvestigationId);
                var edges = setup.Formulas.Where(f => f.FormulaId != request.FormulaId && f.IsActive == 1)
                    .Select(f => (f.ObservationId, Used: LabFormulaEngine.TryParse(f.Expression, out _, out var ids, out _) ? ids : new HashSet<int>()))
                    .ToList();
                if (request.IsActive == 1) edges.Add((request.ObservationId, usedIds));
                if (HasFormulaCycle(edges))
                    return FormulaInvalid<LabFormulaSetupSaveResponse>("This formula makes a circle with another formula of the test (each one uses the other's result)");

                var result = _sqlHelper.DML("IU_LabInvestigationFormula", CommandType.StoredProcedure, new
                {
                    formulaId = request.FormulaId,
                    hospId = globalValues.hospId,
                    investigationId = request.InvestigationId,
                    observationId = request.ObservationId,
                    gender = request.Gender,
                    expression,
                    formulaText = string.IsNullOrWhiteSpace(request.FormulaText) ? null : request.FormulaText.Trim(),
                    decimalPlaces = request.DecimalPlaces,
                    isActive = request.IsActive,
                    usedObservationIdsJson = JsonSerializer.Serialize(usedIds),
                    userId = globalValues.userId,
                    IpAddress = globalValues.ipAddress
                }, new { Result = 0 });

                int resultValue = Convert.ToInt32(result);
                _distributedCache.Remove(FormulaSetupCacheKey(request.InvestigationId));

                return SaveResult(resultValue, request.FormulaId == 0, resultValue switch
                {
                    -1 => "A formula for this observation and gender already exists in this test",
                    -2 => "The target observation is not mapped to this test",
                    -3 => "The formula uses an observation that is not mapped to this test",
                    -4 => "The target observation cannot be used in its own formula",
                    _ => ""
                });
            }
            catch (Exception ex)
            {
                return FormulaServerError<LabFormulaSetupSaveResponse>(ex);
            }
        }

        private static bool HasFormulaCycle(List<(int Target, HashSet<int> Used)> edges)
        {
            var graph = edges.GroupBy(e => e.Target).ToDictionary(g => g.Key, g => g.SelectMany(e => e.Used).ToHashSet());
            var state = new Dictionary<int, int>();   // 1 visiting, 2 done
            bool Visit(int n)
            {
                if (state.TryGetValue(n, out var s)) return s == 1;
                state[n] = 1;
                if (graph.TryGetValue(n, out var next) && next.Any(Visit)) return true;
                state[n] = 2;
                return false;
            }
            return graph.Keys.Any(Visit);
        }

        public ServiceResult<string> DeleteInvestigationFormula(int formulaId, AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"DeleteInvestigationFormula called. FormulaId={formulaId}, UserId={globalValues.userId}");
                var result = _sqlHelper.DML("D_LabInvestigationFormula", CommandType.StoredProcedure, new { formulaId }, new { Result = 0 });
                GlobalFunctions.ClearCacheByPattern(_configuration, FormulaSetupCachePattern);
                return DeleteResult(Convert.ToInt32(result));
            }
            catch (Exception ex)
            {
                return FormulaServerError<string>(ex);
            }
        }

        #endregion

        #region Validation writes

        public ServiceResult<LabFormulaSetupSaveResponse> CreateUpdateInvestigationValidation(CreateUpdateInvestigationValidationRequest request, AllGlobalValues globalValues)
        {
            SqlConnection? con = null;
            SqlTransaction? tnx = null;
            try
            {
                _log.Info($"CreateUpdateInvestigationValidation called. ValidationId={request.ValidationId}, InvestigationId={request.InvestigationId}, CheckType={request.CheckType}");

                string checkType = (request.CheckType ?? "").Trim().ToUpperInvariant();
                string op = (request.Operator ?? "").Trim().ToUpperInvariant();
                if (!LabFormulaEngine.CheckTypes.Contains(checkType))
                    return FormulaInvalid<LabFormulaSetupSaveResponse>("Check type is not valid");
                if (!LabFormulaEngine.Operators.Contains(op))
                    return FormulaInvalid<LabFormulaSetupSaveResponse>("Condition is not valid");
                if ((op == "BETWEEN" || op == "NOTBETWEEN") && request.Value2 == null)
                    return FormulaInvalid<LabFormulaSetupSaveResponse>("Enter the second value for a between condition");

                string? expression = null;
                var observations = (request.Observations ?? new()).Where(o => o.ObservationId > 0)
                    .GroupBy(o => o.ObservationId).Select(g => g.First())
                    .OrderBy(o => o.SequenceNo).ToList();

                if (checkType == "EXPRESSION")
                {
                    expression = (request.Expression ?? "").Trim();
                    if (!LabFormulaEngine.TryParse(expression, out _, out var usedIds, out var parseError))
                        return FormulaInvalid<LabFormulaSetupSaveResponse>($"Expression is not valid: {parseError}");
                    // the observations of an expression check are the ones it uses
                    observations = usedIds.Select((id, i) => new ValidationObservationItem { ObservationId = id, SequenceNo = i + 1 }).ToList();
                }
                if (observations.Count == 0)
                    return FormulaInvalid<LabFormulaSetupSaveResponse>("Select at least one observation");
                if ((checkType == "DIFFERENCE" || checkType == "RATIO") && observations.Count < 2)
                    return FormulaInvalid<LabFormulaSetupSaveResponse>("Select at least two observations for a difference or ratio check");

                var connectionString = _configuration.GetConnectionString("ConnectionString");
                con = new SqlConnection(connectionString);
                con.Open();
                tnx = CustomSqlHelper.getSqlTransaction(con);

                var result = _sqlHelper.DML(tnx, "IU_LabInvestigationValidation", CommandType.StoredProcedure, new
                {
                    validationId = request.ValidationId,
                    hospId = globalValues.hospId,
                    investigationId = request.InvestigationId,
                    validationName = request.ValidationName?.Trim(),
                    gender = request.Gender,
                    checkType,
                    expression,
                    expressionText = checkType == "EXPRESSION" && !string.IsNullOrWhiteSpace(request.ExpressionText) ? request.ExpressionText.Trim() : null,
                    @operator = op,
                    value1 = request.Value1,
                    value2 = request.Value2,
                    tolerance = Math.Abs(request.Tolerance),
                    actionType = request.ActionType,
                    skipIfBlank = request.SkipIfBlank == 0 ? 0 : 1,
                    errorMessage = string.IsNullOrWhiteSpace(request.ErrorMessage) ? null : request.ErrorMessage.Trim(),
                    isActive = request.IsActive,
                    observationsJson = JsonSerializer.Serialize(observations.Select((o, i) => new { observationId = o.ObservationId, sequenceNo = i + 1 })),
                    userId = globalValues.userId,
                    IpAddress = globalValues.ipAddress
                }, new { Result = 0 });

                int resultValue = Convert.ToInt32(result);
                if (resultValue > 0) tnx.Commit(); else tnx.Rollback();
                _distributedCache.Remove(FormulaSetupCacheKey(request.InvestigationId));

                return SaveResult(resultValue, request.ValidationId == 0, resultValue switch
                {
                    -1 => "A validation with this name already exists in this test",
                    -2 => "An observation is not mapped to this test",
                    -3 => "Select at least one observation",
                    _ => ""
                });
            }
            catch (Exception ex)
            {
                try { tnx?.Rollback(); } catch { /* already closed */ }
                return FormulaServerError<LabFormulaSetupSaveResponse>(ex);
            }
            finally
            {
                tnx?.Dispose();
                con?.Dispose();
            }
        }

        public ServiceResult<string> DeleteInvestigationValidation(int validationId, AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"DeleteInvestigationValidation called. ValidationId={validationId}, UserId={globalValues.userId}");
                var result = _sqlHelper.DML("D_LabInvestigationValidation", CommandType.StoredProcedure, new { validationId }, new { Result = 0 });
                GlobalFunctions.ClearCacheByPattern(_configuration, FormulaSetupCachePattern);
                return DeleteResult(Convert.ToInt32(result));
            }
            catch (Exception ex)
            {
                return FormulaServerError<string>(ex);
            }
        }

        #endregion

        #region Result save check

        /// <summary>
        /// Blocking validations of the test that fail for the values being saved (the screen checks the same
        /// rules first; this stops a save that skipped the screen). Returns the failure messages.
        /// </summary>
        private List<string> FailedBlockingValidations(int patientInvestigationId, IEnumerable<(int ObservationId, string? Value)> values)
        {
            var setup = LoadResultEntrySetup(patientInvestigationId);
            var blocking = setup.Validations.Where(v => v.ActionType == 1).ToList();
            if (blocking.Count == 0) return new List<string>();

            var byObs = values.GroupBy(v => v.ObservationId).ToDictionary(g => g.Key, g => LabFormulaEngine.ToNumber(g.First().Value));
            var ctx = new LabFormulaEngine.EvalContext
            {
                Observation = id => byObs.TryGetValue(id, out var d) ? d : null,
                AgeYears = setup.AgeYears,
                AgeMonths = setup.AgeMonths,
                AgeDays = setup.AgeDays
            };

            return blocking.Select(v => LabFormulaEngine.CheckValidation(new LabFormulaEngine.ValidationRule
            {
                ValidationId = v.ValidationId,
                ValidationName = v.ValidationName,
                CheckType = v.CheckType,
                Expression = v.Expression,
                Operator = v.Operator,
                Value1 = v.Value1,
                Value2 = v.Value2,
                Tolerance = v.Tolerance,
                ActionType = v.ActionType,
                SkipIfBlank = v.SkipIfBlank,
                ErrorMessage = v.ErrorMessage,
                Observations = v.Observations.Select(o => (o.ObservationId, o.ObservationName)).ToList()
            }, ctx))
                .Where(o => !o.Skipped && !o.Passed)
                .Select(o => o.Message)
                .ToList();
        }

        #endregion

        #region Helpers

        private ServiceResult<LabFormulaSetupSaveResponse> SaveResult(int resultValue, bool isNew, string rejection)
        {
            if (resultValue > 0)
            {
                var ok = _messageService.GetMessageAndTypeByAlertCode(isNew ? "DATA_SAVED_SUCCESSFULLY" : "DATA_UPDATED_SUCCESSFULLY");
                return ServiceResult<LabFormulaSetupSaveResponse>.Success(new LabFormulaSetupSaveResponse { Id = resultValue }, ok.Type, ok.Message, isNew ? 201 : 200);
            }
            if (resultValue == -1)
            {
                var dup = _messageService.GetMessageAndTypeByAlertCode("RECORD_ALREADY_EXISTS");
                return ServiceResult<LabFormulaSetupSaveResponse>.Failure(dup.Type, rejection, 409);
            }
            if (resultValue == 0 && !isNew)
            {
                var nf = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                return ServiceResult<LabFormulaSetupSaveResponse>.Failure(nf.Type, nf.Message, 404);
            }
            if (!string.IsNullOrEmpty(rejection)) return FormulaInvalid<LabFormulaSetupSaveResponse>(rejection);

            _log.Error($"Formula setup save returned unexpected result {resultValue}");
            var fail = _messageService.GetMessageAndTypeByAlertCode("OPERATION_FAILED");
            return ServiceResult<LabFormulaSetupSaveResponse>.Failure(fail.Type, fail.Message, 500);
        }

        private ServiceResult<string> DeleteResult(int resultValue)
        {
            if (resultValue > 0)
            {
                var ok = _messageService.GetMessageAndTypeByAlertCode("DATA_DELETED_SUCCESSFULLY");
                return ServiceResult<string>.Success("Deleted", ok.Type, ok.Message, 200);
            }
            var nf = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
            return ServiceResult<string>.Failure(nf.Type, nf.Message, 404);
        }

        private ServiceResult<T> FormulaInvalid<T>(string message)
        {
            var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
            return ServiceResult<T>.Failure(alert.Type, message, 400);
        }

        private ServiceResult<T> FormulaServerError<T>(Exception ex, [System.Runtime.CompilerServices.CallerMemberName] string method = "")
        {
            LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{method}");
            var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
            return ServiceResult<T>.Failure(alert.Type, alert.Message, 500);
        }

        #endregion
    }
}
