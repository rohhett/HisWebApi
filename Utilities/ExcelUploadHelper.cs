using System.Data;
using System.Globalization;
using System.Text.RegularExpressions;
using HISWEBAPI.Data.Helpers;
using HISWEBAPI.DTO;
using HISWEBAPI.Models;
using HISWEBAPI.Services;
using Microsoft.Data.SqlClient;

namespace HISWEBAPI.Utilities
{
    /// <summary>Field cleaning and the validate / save-all flow shared by the master Excel uploads.</summary>
    public static class ExcelUploadHelper
    {
        public const int MaxRows = 1000;

        private static readonly string[] DateFormats = { "dd-MM-yyyy", "d-M-yyyy", "dd/MM/yyyy", "d/M/yyyy", "yyyy-MM-dd" };

        public static string Clean(string? value) => Regex.Replace((value ?? string.Empty).Trim(), @"\s+", " ");

        public static bool IsEmail(string value) => Regex.IsMatch(value, @"^[^@\s]+@[^@\s]+\.[^@\s]+$");

        public static bool TryDate(string value, out DateTime date) =>
            DateTime.TryParseExact(value, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);

        /// <summary>Yes / No cell; blank = No. null = not understood.</summary>
        public static int? YesNo(string value) => value.ToLowerInvariant() switch
        {
            "" or "no" or "n" or "0" => 0,
            "yes" or "y" or "1" => 1,
            _ => null
        };

        public static void CheckLength(List<string> errors, string label, string value, int max)
        {
            if (value.Length > max) errors.Add($"{label} cannot exceed {max} characters");
        }

        public static List<int> ParseIds(string? csv) =>
            (csv ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(s => int.TryParse(s, out int id) ? id : -1)
                .Distinct()
                .ToList();

        /// <summary>Name -> id map (case-insensitive; the first of two equal names wins).</summary>
        public static Dictionary<string, int> NameMap(DataTable table, string nameColumn, string idColumn)
        {
            var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (DataRow row in table.Rows)
                map.TryAdd(Clean(Convert.ToString(row[nameColumn])), Convert.ToInt32(row[idColumn]));
            return map;
        }

        /// <summary>
        /// saveData = false: report the checked rows. saveData = true: save every row in one transaction,
        /// only when no row (and no shared selection) has an error. saveRow returns the new id, -1 = duplicate.
        /// </summary>
        public static ServiceResult<ExcelUploadResponse<TRow>> Complete<TRow>(
            List<TRow> rows,
            bool saveData,
            string? selectionError,
            string connectionString,
            IResponseMessageService messageService,
            string entity,
            Func<TRow, string> rowName,
            Func<SqlTransaction, TRow, long> saveRow,
            Action afterSave) where TRow : ExcelUploadRow
        {
            foreach (var row in rows) row.IsValid = row.Errors.Count == 0;

            var response = new ExcelUploadResponse<TRow>
            {
                Rows = rows,
                TotalRows = rows.Count,
                ValidRows = rows.Count(r => r.IsValid),
                ErrorRows = rows.Count(r => !r.IsValid)
            };

            if (!saveData)
            {
                string message = response.ErrorRows == 0
                    ? $"All {response.TotalRows} row(s) are valid"
                    : $"{response.ErrorRows} of {response.TotalRows} row(s) have errors";
                return ServiceResult<ExcelUploadResponse<TRow>>.Success(response, "Info", message, 200);
            }

            ServiceResult<ExcelUploadResponse<TRow>> Rejected(string alertCode, string message)
            {
                var alert = messageService.GetMessageAndTypeByAlertCode(alertCode);
                return new ServiceResult<ExcelUploadResponse<TRow>>
                {
                    Result = false,
                    Data = response,
                    MessageType = alert.Type,
                    Message = message,
                    StatusCode = 200
                };
            }

            if (selectionError != null || response.ErrorRows > 0)
                return Rejected("MODEL_VALIDATION_FAILED", selectionError != null
                    ? $"Nothing saved: {selectionError}"
                    : $"Nothing saved: fix the {response.ErrorRows} row(s) with errors first");

            using (var con = new SqlConnection(connectionString))
            {
                con.Open();
                using SqlTransaction tnx = CustomSqlHelper.getSqlTransaction(con);
                foreach (var row in rows)
                {
                    long id = saveRow(tnx, row);
                    if (id > 0) continue;

                    // e.g. the same record was added after the rows were validated
                    tnx.Rollback();
                    string reason = id == -1 ? $"{entity} already exists" : $"{entity} could not be saved";
                    row.IsValid = false;
                    row.Errors.Add(reason);
                    response.ValidRows--;
                    response.ErrorRows++;
                    return Rejected("OPERATION_FAILED", $"Nothing saved: row {row.RowNo} ({rowName(row)}) - {reason}");
                }
                tnx.Commit();
            }

            afterSave();
            response.Saved = true;
            response.SavedRows = rows.Count;

            var saved = messageService.GetMessageAndTypeByAlertCode("DATA_SAVED_SUCCESSFULLY");
            return ServiceResult<ExcelUploadResponse<TRow>>.Success(
                response, saved.Type, $"{response.SavedRows} {entity.ToLowerInvariant()}(s) saved successfully", 200);
        }

        /// <summary>Scalar returned by an IU_ procedure inside a transaction (SELECT @Result).</summary>
        public static long ScalarId(object? raw) => raw == null || raw == DBNull.Value ? 0 : Convert.ToInt64(raw);
    }
}
