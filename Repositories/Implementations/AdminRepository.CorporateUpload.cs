using System.Data;
using System.Globalization;
using System.Reflection;
using HISWEBAPI.DTO;
using HISWEBAPI.Exceptions;
using HISWEBAPI.Models;
using HISWEBAPI.Utilities;
using Microsoft.Extensions.Caching.Distributed;

namespace HISWEBAPI.Repositories.Implementations
{
    public partial class AdminRepository
    {
        // ================= Corporate Master Excel upload =================

        // A corporate without an insurance company is saved as "Self".
        private const string SelfInsuranceName = "Self";

        /// <summary>
        /// Validates the uploaded corporates (SaveData = false) or validates and saves them (SaveData = true, all or nothing).
        /// Allowed payment modes are chosen once for all rows; uploaded corporates are Active.
        /// </summary>
        public ServiceResult<ExcelUploadResponse<CorporateUploadRow>> CorporateExcelUpload(
            CorporateExcelUploadRequest request, AllGlobalValues globalValues)
        {
            try
            {
                var rows = request.Rows;
                _log.Info($"CorporateExcelUpload called. Rows={rows.Count}, SaveData={request.SaveData}, PaymentModes={request.ActivePaymentModes}");

                for (int i = 0; i < rows.Count; i++)
                {
                    var r = rows[i];
                    r.RowNo = r.RowNo > 0 ? r.RowNo : i + 1;
                    r.Errors = new List<string>();
                    r.CorporateName = ExcelUploadHelper.Clean(r.CorporateName);
                    r.CorporateType = ExcelUploadHelper.Clean(r.CorporateType);
                    r.InsuranceCompany = ExcelUploadHelper.Clean(r.InsuranceCompany);
                    r.CorporateCode = ExcelUploadHelper.Clean(r.CorporateCode);
                    r.CorporateContact1 = ExcelUploadHelper.Clean(r.CorporateContact1);
                    r.CorporateContact2 = ExcelUploadHelper.Clean(r.CorporateContact2);
                    r.CorporateEmail = ExcelUploadHelper.Clean(r.CorporateEmail);
                    r.CorporateAddress1 = ExcelUploadHelper.Clean(r.CorporateAddress1);
                    r.CorporateAddress2 = ExcelUploadHelper.Clean(r.CorporateAddress2);
                    r.ContractStartFrom = ExcelUploadHelper.Clean(r.ContractStartFrom);
                    r.ContractExpiresOn = ExcelUploadHelper.Clean(r.ContractExpiresOn);
                    r.CopaymentPer = ExcelUploadHelper.Clean(r.CopaymentPer);
                    r.DiscountPerOut = ExcelUploadHelper.Clean(r.DiscountPerOut);
                    r.DiscountPerIn = ExcelUploadHelper.Clean(r.DiscountPerIn);
                    r.HikePerOut = ExcelUploadHelper.Clean(r.HikePerOut);
                    r.HikePerIn = ExcelUploadHelper.Clean(r.HikePerIn);
                    r.PaymentType = ExcelUploadHelper.Clean(r.PaymentType);
                    r.RegistrationCharge = ExcelUploadHelper.Clean(r.RegistrationCharge);
                    r.CaseBilling = ExcelUploadHelper.Clean(r.CaseBilling);
                }

                string json = System.Text.Json.JsonSerializer.Serialize(rows.Select(r => new { r.CorporateName }));
                DataSet ds = _sqlHelper.GetDataSet("S_ValidateCorporateUpload", CommandType.StoredProcedure, new { rowsJson = json });

                var types = ExcelUploadHelper.NameMap(ds.Tables[0], "CorporateTypeName", "CorporateTypeId");
                var insurers = ExcelUploadHelper.NameMap(ds.Tables[1], "InsuranceCompanyName", "InsuranceCompanyId");
                var paymentModes = ds.Tables[2].AsEnumerable().Select(x => x.Field<int>("PaymentModeId")).ToHashSet();
                var paymentTypes = ds.Tables[3].AsEnumerable()
                    .Select(x => (Key: int.TryParse(x.Field<string>("Key"), out int k) ? k : 0, Value: x.Field<string>("Value")!))
                    .ToList();
                var existing = ds.Tables[4].AsEnumerable()
                    .Select(x => $"{ExcelUploadHelper.Clean(x.Field<string>("CorporateName"))}|{x.Field<int>("InsuranceCompanyId")}")
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                // the id of the master row (the form's dropdown value); "Self" without a master row is 0
                int? InsurerId(string name)
                {
                    if (name == "") name = SelfInsuranceName;
                    if (insurers.TryGetValue(name, out int id)) return id;
                    return name.Equals(SelfInsuranceName, StringComparison.OrdinalIgnoreCase) ? 0 : null;
                }

                static decimal? Number(string value) =>
                    value == "" ? 0
                    : decimal.TryParse(value.TrimEnd('%').Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out decimal n) ? n : null;

                // name + insurance company must also be unique inside the file
                var repeated = rows.Where(r => r.CorporateName != "")
                    .GroupBy(r => $"{r.CorporateName}|{InsurerId(r.InsuranceCompany!)}", StringComparer.OrdinalIgnoreCase)
                    .Where(g => g.Count() > 1)
                    .SelectMany(g => g.Select(x => (x.RowNo, Rows: string.Join(", ", g.Select(y => y.RowNo)))))
                    .ToDictionary(x => x.RowNo, x => x.Rows);

                foreach (var r in rows)
                {
                    var e = r.Errors;

                    if (r.CorporateName == "") e.Add("Corporate Name is required");
                    else ExcelUploadHelper.CheckLength(e, "Corporate Name", r.CorporateName!, 256);

                    if (r.CorporateType == "") e.Add("Corporate Type is required");
                    else if (!types.ContainsKey(r.CorporateType!)) e.Add($"Corporate Type '{r.CorporateType}' not found");

                    int? insurerId = InsurerId(r.InsuranceCompany!);
                    if (insurerId == null) e.Add($"Insurance Company '{r.InsuranceCompany}' not found");
                    else if (r.InsuranceCompany == "") r.InsuranceCompany = SelfInsuranceName;

                    ExcelUploadHelper.CheckLength(e, "Corporate Code", r.CorporateCode!, 50);

                    if (r.CorporateContact1 == "") e.Add("Contact Number 1 is required");
                    else ExcelUploadHelper.CheckLength(e, "Contact Number 1", r.CorporateContact1!, 20);
                    ExcelUploadHelper.CheckLength(e, "Contact Number 2", r.CorporateContact2!, 20);

                    if (r.CorporateEmail == "") e.Add("Email is required");
                    else if (r.CorporateEmail!.Length > 100) e.Add("Email cannot exceed 100 characters");
                    else if (!ExcelUploadHelper.IsEmail(r.CorporateEmail)) e.Add("Email is not valid");

                    if (r.CorporateAddress1 == "") e.Add("Address Line 1 is required");
                    else ExcelUploadHelper.CheckLength(e, "Address Line 1", r.CorporateAddress1!, 500);
                    ExcelUploadHelper.CheckLength(e, "Address Line 2", r.CorporateAddress2!, 500);

                    DateTime start = default, expiry = default;
                    if (r.ContractStartFrom == "") e.Add("Contract Start From is required");
                    else if (!ExcelUploadHelper.TryDate(r.ContractStartFrom!, out start)) e.Add("Contract Start From must be a date (dd-MM-yyyy)");
                    if (r.ContractExpiresOn == "") e.Add("Contract Expires On is required");
                    else if (!ExcelUploadHelper.TryDate(r.ContractExpiresOn!, out expiry)) e.Add("Contract Expires On must be a date (dd-MM-yyyy)");
                    if (start != default && expiry != default && expiry < start) e.Add("Contract Expires On is before Start From");

                    foreach (var (label, value, max) in new[]
                    {
                        ("Co-payment %", r.CopaymentPer!, 100m),
                        ("Discount OPD %", r.DiscountPerOut!, 100m),
                        ("Discount IPD %", r.DiscountPerIn!, 100m),
                        ("Hike OPD %", r.HikePerOut!, 9999m),
                        ("Hike IPD %", r.HikePerIn!, 9999m)
                    })
                    {
                        if (Number(value) is not decimal n || n < 0 || n > max) e.Add($"{label} must be a number 0-{max}");
                    }

                    if (r.PaymentType != "")
                    {
                        var match = paymentTypes.FirstOrDefault(p => p.Value.Equals(r.PaymentType, StringComparison.OrdinalIgnoreCase));
                        if (match.Value == null) e.Add($"Payment Type '{r.PaymentType}' not found");
                        else r.PaymentType = match.Value;
                    }

                    if (ExcelUploadHelper.YesNo(r.RegistrationCharge!) == null) e.Add("Registration Charge must be Yes or No");
                    if (ExcelUploadHelper.YesNo(r.CaseBilling!) == null) e.Add("Case Billing must be Yes or No");

                    if (repeated.TryGetValue(r.RowNo, out string? sameRows)) e.Add($"Same corporate and insurance company in rows {sameRows}");
                    else if (insurerId != null && (existing.Contains($"{r.CorporateName}|{insurerId}")
                        // saved corporates hold "Self" as 0 or as the id of its master row
                        || (insurerId == InsurerId(SelfInsuranceName) && existing.Contains($"{r.CorporateName}|0"))))
                        e.Add("Corporate name already exists for this insurance company");
                }

                var chosen = ExcelUploadHelper.ParseIds(request.ActivePaymentModes);
                string activePaymentModes = string.Join(",", chosen);
                string? modeError = chosen.Count == 0 ? "Select at least one payment mode"
                    : chosen.Any(id => !paymentModes.Contains(id)) ? "One or more selected payment modes are not active"
                    : activePaymentModes.Length > 100 ? "Too many payment modes selected"
                    : null;

                return ExcelUploadHelper.Complete(
                    rows,
                    request.SaveData,
                    modeError,
                    _configuration.GetConnectionString("ConnectionString")!,
                    _messageService,
                    "Corporate",
                    r => r.CorporateName!,
                    (tnx, r) =>
                    {
                        ExcelUploadHelper.TryDate(r.ContractStartFrom!, out DateTime start);
                        ExcelUploadHelper.TryDate(r.ContractExpiresOn!, out DateTime expiry);
                        int insurerId = InsurerId(r.InsuranceCompany!)!.Value;

                        object raw = _sqlHelper.DML(tnx, "IU_CorporateMaster", CommandType.StoredProcedure, new
                        {
                            hospId = globalValues.hospId,
                            corporateId = 0,
                            corporateName = r.CorporateName,
                            insuranceCompanyName = r.InsuranceCompany,
                            insuranceCompanyId = insurerId,
                            corporateTypeName = r.CorporateType,
                            corporateTypeId = types[r.CorporateType!],
                            paymentTypeId = paymentTypes.FirstOrDefault(p => p.Value == r.PaymentType).Key,
                            corporateCode = r.CorporateCode,
                            corporateContact1 = r.CorporateContact1,
                            corporateContact2 = r.CorporateContact2,
                            corporateEmail = r.CorporateEmail,
                            corporateAddress1 = r.CorporateAddress1,
                            corporateAddress2 = r.CorporateAddress2,
                            isActive = 1,
                            contractStartFrom = start,
                            contractExpiresOn = expiry,
                            copaymentPer = Number(r.CopaymentPer!),
                            discountPerOut = Number(r.DiscountPerOut!),
                            discountPerIn = Number(r.DiscountPerIn!),
                            hikePerOut = Number(r.HikePerOut!),
                            hikePerIn = Number(r.HikePerIn!),
                            activePaymentModes,
                            isRegistrationChargeApplicable = ExcelUploadHelper.YesNo(r.RegistrationCharge!),
                            isCaseBillingApplicable = ExcelUploadHelper.YesNo(r.CaseBilling!),
                            userId = globalValues.userId,
                            IpAddress = globalValues.ipAddress
                        }, new { Result = 0 });
                        return ExcelUploadHelper.ScalarId(raw);
                    },
                    () =>
                    {
                        _distributedCache.Remove("_CorporateMaster_All");
                        _distributedCache.Remove("_Corporate_All");
                        _distributedCache.Remove("_BranchWiseCorporate_All");
                    });
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod()!.Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<ExcelUploadResponse<CorporateUploadRow>>.Failure(alert.Type, alert.Message, 500);
            }
        }
    }
}
