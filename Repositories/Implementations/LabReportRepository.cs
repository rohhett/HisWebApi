using System.Data;
using System.Reflection;
using System.Text.Json;
using HISWEBAPI.Configuration;
using HISWEBAPI.Data.Helpers;
using HISWEBAPI.DTO;
using HISWEBAPI.Exceptions;
using HISWEBAPI.Models;
using HISWEBAPI.Repositories.Interfaces;
using HISWEBAPI.Services;
using log4net;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Caching.Distributed;

namespace HISWEBAPI.Repositories.Implementations
{
    public class LabReportRepository : ILabReportRepository
    {
        private readonly ICustomSqlHelper _sqlHelper;
        private readonly IResponseMessageService _messageService;
        private readonly IDistributedCache _distributedCache;
        private readonly IConfiguration _configuration;
        private static readonly ILog _log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private const string CACHE_PREFIX = "_LabReportPrintSetting_";
        private const string CACHE_KEY_DEFAULTS = "_LabReportPrintSetting_Defaults";

        public LabReportRepository(
            ICustomSqlHelper sqlHelper,
            IResponseMessageService messageService,
            IDistributedCache distributedCache,
            IConfiguration configuration)
        {
            _sqlHelper = sqlHelper;
            _messageService = messageService;
            _distributedCache = distributedCache;
            _configuration = configuration;
        }

        // ── shared: raw SP result -> single dictionary (no model mapping), cached ─────────
        // Redis problems never block reading the settings: on any cache error we read from the DB.
        private Dictionary<string, object> LoadRow(string cacheKey, string spName, object parameters)
        {
            try
            {
                var cached = _distributedCache.GetString(cacheKey);
                if (!string.IsNullOrEmpty(cached))
                    return JsonSerializer.Deserialize<Dictionary<string, object>>(cached);
            }
            catch (Exception ex)
            {
                _log.Warn($"Print setting cache read failed for {cacheKey}, reading from DB. {ex.Message}");
            }

            var dt = parameters == null
                ? _sqlHelper.GetDataTable(spName, CommandType.StoredProcedure)
                : _sqlHelper.GetDataTable(spName, CommandType.StoredProcedure, parameters);

            if (dt == null || dt.Rows.Count == 0) return null;

            var row = dt.Rows[0];
            var dict = dt.Columns.Cast<DataColumn>()
                .ToDictionary(c => c.ColumnName, c => row[c] == DBNull.Value ? null : row[c]);

            try
            {
                _distributedCache.SetString(cacheKey, JsonSerializer.Serialize(dict),
                    new DistributedCacheEntryOptions { AbsoluteExpiration = null, SlidingExpiration = null });
            }
            catch (Exception ex)
            {
                _log.Warn($"Print setting cache write failed for {cacheKey}. {ex.Message}");
            }
            return dict;
        }

        // ── GET 1 ────────────────────────────────────────────────────────────────────────
        public ServiceResult<object> GetLabReportPrintSetting(int branchId, bool applyDefaults = false)
        {
            try
            {
                _log.Info($"GetLabReportPrintSetting called. BranchId={branchId}, ApplyDefaults={applyDefaults}");

                var row = LoadRow($"{CACHE_PREFIX}{branchId}_{(applyDefaults ? 1 : 0)}",
                    "S_GetLabReportPrintSetting",
                    new { @BranchId = branchId, @ApplyDefaults = applyDefaults });

                if (row == null)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    return ServiceResult<object>.Failure(alert.Type, "Print setting not found", 404);
                }

                var ok = _messageService.GetMessageAndTypeByAlertCode("OPERATION_COMPLETED_SUCCESSFULLY");
                return ServiceResult<object>.Success(row, ok.Type, "Print setting retrieved successfully", 200);
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<object>.Failure(alert.Type, alert.Message, 500);
            }
        }

        // ── GET 2 ────────────────────────────────────────────────────────────────────────
        public ServiceResult<object> GetLabReportPrintSettingDefaults()
        {
            try
            {
                _log.Info("GetLabReportPrintSettingDefaults called.");

                var row = LoadRow(CACHE_KEY_DEFAULTS, "S_GetLabReportPrintSettingDefaults", null);

                if (row == null)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("DATA_NOT_FOUND");
                    return ServiceResult<object>.Failure(alert.Type, "Default print setting not found", 404);
                }

                var ok = _messageService.GetMessageAndTypeByAlertCode("OPERATION_COMPLETED_SUCCESSFULLY");
                return ServiceResult<object>.Success(row, ok.Type, "Default print setting retrieved successfully", 200);
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<object>.Failure(alert.Type, alert.Message, 500);
            }
        }

        // ── POST ─────────────────────────────────────────────────────────────────────────
        public ServiceResult<object> SaveLabReportPrintSetting(SaveLabReportPrintSettingRequest request, AllGlobalValues globalValues)
        {
            try
            {
                _log.Info($"SaveLabReportPrintSetting called. BranchId={request.BranchId}");

                // Map every DTO property to a same-named SP parameter (null -> DBNull)
                var parameters = new List<IDataParameter>();
                foreach (var prop in typeof(SaveLabReportPrintSettingRequest).GetProperties(BindingFlags.Public | BindingFlags.Instance))
                    parameters.Add(new SqlParameter("@" + prop.Name, prop.GetValue(request) ?? DBNull.Value));

                parameters.Add(new SqlParameter("@UserId", globalValues.userId));
                parameters.Add(new SqlParameter("@IpAddress", (object)globalValues.ipAddress ?? DBNull.Value));
                parameters.Add(new SqlParameter("@Result", SqlDbType.Int) { Direction = ParameterDirection.Output });

                long result = _sqlHelper.RunProcedureInsert("IU_LabReportPrintSetting", parameters.ToArray());

                if (result <= 0)
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("OPERATION_FAILED");
                    return ServiceResult<object>.Failure(alert.Type, alert.Message, 400);
                }

                // clear every cached variant (raw / merged / defaults)
                GlobalFunctions.ClearCacheByPattern(_configuration, $"{CACHE_PREFIX}*");
                _log.Info("Cleared LabReportPrintSetting cache.");

                var ok = _messageService.GetMessageAndTypeByAlertCode("DATA_SAVED_SUCCESSFULLY");
                return ServiceResult<object>.Success(new { branchId = result }, ok.Type, ok.Message, 200);
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<object>.Failure(alert.Type, alert.Message, 500);
            }
        }

        // ── internal use by PDF service ──────────────────────────────────────────────────
        // Returns every column as string (null = not set) so ints, flags, colours and fonts can all be read.
        // refreshCache = true -> drop the cached row first so direct table edits are picked up (print-setting preview)
        public Dictionary<string, string> GetEffectiveSetting(int branchId, bool refreshCache = false)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                if (refreshCache)
                {
                    try { _distributedCache.Remove($"{CACHE_PREFIX}{branchId}_1"); }
                    catch (Exception ex) { _log.Warn($"Print setting cache remove failed. {ex.Message}"); }
                }

                var row = LoadRow($"{CACHE_PREFIX}{branchId}_1", "S_GetLabReportPrintSetting",
                    new { @BranchId = branchId, @ApplyDefaults = true });
                if (row == null) return result;

                foreach (var kv in row)
                {
                    string value = null;
                    if (kv.Value is JsonElement je)
                    {
                        if (je.ValueKind == JsonValueKind.String) value = je.GetString();
                        else if (je.ValueKind != JsonValueKind.Null && je.ValueKind != JsonValueKind.Undefined) value = je.GetRawText();
                    }
                    else if (kv.Value != null) value = kv.Value.ToString();
                    result[kv.Key] = value;
                }
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod().Name}");
            }
            return result;   // empty -> service keeps its hard-coded fallback values
        }
    }
}
