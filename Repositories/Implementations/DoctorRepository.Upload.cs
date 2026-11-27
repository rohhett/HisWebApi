using System.Data;
using System.Reflection;
using System.Text.RegularExpressions;
using HISWEBAPI.Configuration;
using HISWEBAPI.DTO;
using HISWEBAPI.Exceptions;
using HISWEBAPI.Models;
using HISWEBAPI.Utilities;
using Microsoft.Extensions.Caching.Distributed;

namespace HISWEBAPI.Repositories.Implementations
{
    public partial class DoctorRepository
    {
        // ================= Doctor Master Excel upload =================

        /// <summary>
        /// Validates the uploaded doctors (SaveData = false) or validates and saves them (SaveData = true, all or nothing).
        /// Branches are chosen once for all rows; uploaded doctors are Active, without login and without photo.
        /// </summary>
        public ServiceResult<ExcelUploadResponse<DoctorUploadRow>> DoctorExcelUpload(
            DoctorExcelUploadRequest request, AllGlobalValues globalValues)
        {
            try
            {
                var rows = request.Rows;
                _log.Info($"DoctorExcelUpload called. Rows={rows.Count}, SaveData={request.SaveData}, Branches={request.BranchList}");

                for (int i = 0; i < rows.Count; i++)
                {
                    var r = rows[i];
                    r.RowNo = r.RowNo > 0 ? r.RowNo : i + 1;
                    r.Errors = new List<string>();
                    r.Title = ExcelUploadHelper.Clean(r.Title);
                    r.Name = ExcelUploadHelper.Clean(r.Name);
                    r.Gender = ExcelUploadHelper.Clean(r.Gender);
                    r.Dob = ExcelUploadHelper.Clean(r.Dob);
                    r.ContactNo = ExcelUploadHelper.Clean(r.ContactNo).Replace(" ", "");
                    r.EmailId = ExcelUploadHelper.Clean(r.EmailId);
                    r.Address = ExcelUploadHelper.Clean(r.Address);
                    r.Specialization = ExcelUploadHelper.Clean(r.Specialization);
                    r.Department = ExcelUploadHelper.Clean(r.Department);
                    r.ProfileSummery = ExcelUploadHelper.Clean(r.ProfileSummery);
                    r.DoctorRole = ExcelUploadHelper.Clean(r.DoctorRole);
                    r.RegistrationNo = ExcelUploadHelper.Clean(r.RegistrationNo);
                    r.RoomNo = ExcelUploadHelper.Clean(r.RoomNo);
                    r.CanApproveLabReport = ExcelUploadHelper.Clean(r.CanApproveLabReport);
                    r.CanApproveDischargeSummary = ExcelUploadHelper.Clean(r.CanApproveDischargeSummary);
                }

                string json = System.Text.Json.JsonSerializer.Serialize(rows.Select(r => new { r.RowNo, r.Name, r.ContactNo }));
                DataSet ds = _sqlHelper.GetDataSet("S_ValidateDoctorUpload", CommandType.StoredProcedure,
                    new { hospId = globalValues.hospId, rowsJson = json });

                var specializations = ExcelUploadHelper.NameMap(ds.Tables[0], "Specialization", "SpecializationId");
                var departments = ExcelUploadHelper.NameMap(ds.Tables[1], "Department", "DepartmentId");
                var pick = ds.Tables[2].AsEnumerable()
                    .Select(x => (Field: x.Field<string>("FieldName")!, Key: x.Field<string>("Key")!, Value: x.Field<string>("Value")!))
                    .ToList();
                var existing = ds.Tables[3].AsEnumerable()
                    .ToDictionary(x => x.Field<int>("RowNo"), x => x.Field<int>("DoctorId"));
                var branches = ds.Tables[4].AsEnumerable().Select(x => x.Field<int>("BranchId")).ToHashSet();

                // value as stored in the pick list, or null
                string? Pick(string field, string text) => pick
                    .Where(p => p.Field.Equals(field, StringComparison.OrdinalIgnoreCase))
                    .Select(p => p.Value)
                    .FirstOrDefault(v => v.Equals(text, StringComparison.OrdinalIgnoreCase)
                        || v.TrimEnd('.').Equals(text.TrimEnd('.'), StringComparison.OrdinalIgnoreCase));

                var roleIds = pick
                    .Where(p => p.Field.Equals("DoctorRoleForDoctorMaster", StringComparison.OrdinalIgnoreCase))
                    .GroupBy(p => p.Value, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => int.TryParse(g.First().Key, out int id) ? id : 0, StringComparer.OrdinalIgnoreCase);

                // the same doctor (name + contact) repeated inside the file
                var repeated = rows.Where(r => r.Name != "" && r.ContactNo != "")
                    .GroupBy(r => $"{r.Name}|{r.ContactNo}", StringComparer.OrdinalIgnoreCase)
                    .Where(g => g.Count() > 1)
                    .SelectMany(g => g.Select(x => (x.RowNo, Rows: string.Join(", ", g.Select(y => y.RowNo)))))
                    .ToDictionary(x => x.RowNo, x => x.Rows);

                foreach (var r in rows)
                {
                    var e = r.Errors;

                    if (r.Title == "") e.Add("Title is required");
                    else if (Pick("title", r.Title!) is string title) r.Title = title;
                    else e.Add($"Title '{r.Title}' is not in the title list");

                    if (r.Name == "") e.Add("Doctor Name is required");
                    else ExcelUploadHelper.CheckLength(e, "Doctor Name", r.Name!, 100);

                    if (r.Gender == "") e.Add("Gender is required");
                    else if (Pick("gender", r.Gender!) is string gender) r.Gender = gender;
                    else e.Add($"Gender '{r.Gender}' is not in the gender list");

                    if (r.Dob == "") e.Add("DOB is required");
                    else if (!ExcelUploadHelper.TryDate(r.Dob!, out DateTime dob)) e.Add("DOB must be a date (dd-MM-yyyy)");
                    else if (dob.Year < 1900 || dob > DateTime.Today) e.Add("DOB must be between 1900 and today");

                    if (r.ContactNo == "") e.Add("Contact No. is required");
                    else if (!Regex.IsMatch(r.ContactNo!, @"^\d{10}$")) e.Add("Contact No. must be 10 digits");

                    if (r.EmailId != "")
                    {
                        if (r.EmailId!.Length > 50) e.Add("Email cannot exceed 50 characters");
                        else if (!ExcelUploadHelper.IsEmail(r.EmailId)) e.Add("Email is not valid");
                    }

                    if (r.Specialization == "") e.Add("Specialization is required");
                    else if (!specializations.ContainsKey(r.Specialization!)) e.Add($"Specialization '{r.Specialization}' not found");

                    if (r.Department == "") e.Add("Department is required");
                    else if (!departments.ContainsKey(r.Department!)) e.Add($"Department '{r.Department}' not found");

                    if (r.ProfileSummery == "") e.Add("Profile Summary is required");

                    if (r.DoctorRole == "") e.Add("Doctor Role is required");
                    else if (Pick("DoctorRoleForDoctorMaster", r.DoctorRole!) is string role) r.DoctorRole = role;
                    else e.Add($"Doctor Role '{r.DoctorRole}' not found");

                    ExcelUploadHelper.CheckLength(e, "Registration No.", r.RegistrationNo!, 50);
                    ExcelUploadHelper.CheckLength(e, "OPD Room", r.RoomNo!, 20);

                    if (ExcelUploadHelper.YesNo(r.CanApproveLabReport!) == null) e.Add("Can Approve Lab Reports must be Yes or No");
                    if (ExcelUploadHelper.YesNo(r.CanApproveDischargeSummary!) == null) e.Add("Can Approve Discharge Summary must be Yes or No");

                    if (repeated.TryGetValue(r.RowNo, out string? sameRows)) e.Add($"Same doctor (name and contact) in rows {sameRows}");
                    else if (existing.TryGetValue(r.RowNo, out int doctorId)) e.Add($"Doctor already exists (Doctor Id {doctorId})");
                }

                var chosen = ExcelUploadHelper.ParseIds(request.BranchList);
                string branchList = string.Join(",", chosen);
                string? branchError = chosen.Count == 0 ? "Select at least one branch"
                    : chosen.Any(id => !branches.Contains(id)) ? "One or more selected branches are not active branches"
                    : branchList.Length > 50 ? "Too many branches selected"
                    : null;

                return ExcelUploadHelper.Complete(
                    rows,
                    request.SaveData,
                    branchError,
                    _configuration.GetConnectionString("ConnectionString")!,
                    _messageService,
                    "Doctor",
                    r => r.Name!,
                    (tnx, r) =>
                    {
                        ExcelUploadHelper.TryDate(r.Dob!, out DateTime dob);
                        object raw = _sqlHelper.DML(tnx, "IU_DoctorMaster", CommandType.StoredProcedure, new
                        {
                            DoctorId = 0,
                            r.Title,
                            r.Name,
                            r.Gender,
                            Dob = dob.ToString("yyyy-MM-dd"),
                            r.ContactNo,
                            r.EmailId,
                            r.Address,
                            SpecializationId = specializations[r.Specialization!],
                            r.Specialization,
                            DepartmentId = departments[r.Department!],
                            r.Department,
                            r.ProfileSummery,
                            r.RegistrationNo,
                            IsActive = 1,
                            BranchId = branchList,
                            r.RoomNo,
                            CanApproveLabReport = ExcelUploadHelper.YesNo(r.CanApproveLabReport!),
                            CanApproveDischargeSummary = ExcelUploadHelper.YesNo(r.CanApproveDischargeSummary!),
                            DoctorPhotoFilePath = "",
                            IsDoctorUnit = 0,
                            r.DoctorRole,
                            DoctorRoleId = roleIds[r.DoctorRole!],
                            UserId = globalValues.userId,
                            HospId = globalValues.hospId,
                            IPAddress = globalValues.ipAddress
                        }, new { result = 0 });
                        return ExcelUploadHelper.ScalarId(raw);
                    },
                    () =>
                    {
                        GlobalFunctions.ClearCacheByPattern(_configuration, "_DoctorMaster_Branch*");
                        _distributedCache.Remove("_DoctorMaster_All");
                    });
            }
            catch (Exception ex)
            {
                LogErrors.WriteErrorLog(ex, $"{GetType().Name}.{MethodBase.GetCurrentMethod()!.Name}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("SERVER_ERROR_FOUND");
                return ServiceResult<ExcelUploadResponse<DoctorUploadRow>>.Failure(alert.Type, alert.Message, 500);
            }
        }
    }
}
