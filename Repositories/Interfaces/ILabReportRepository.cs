using HISWEBAPI.DTO;
using HISWEBAPI.Models;

namespace HISWEBAPI.Repositories.Interfaces
{
    public interface ILabReportRepository
    {
        // GET 1 - saved setting of a branch (raw SP data). applyDefaults=false -> only branch overrides (null = inherit)
        ServiceResult<object> GetLabReportPrintSetting(int branchId, bool applyDefaults = false);

        // GET 2 - default row (placeholders)
        ServiceResult<object> GetLabReportPrintSettingDefaults();

        // POST - upsert
        ServiceResult<object> SaveLabReportPrintSetting(SaveLabReportPrintSettingRequest request, AllGlobalValues globalValues);

        // Used internally by PatientInvestigationReportPdfService (branch value, falling back to default)
        Dictionary<string, int?> GetEffectiveSetting(int branchId);
    }
}