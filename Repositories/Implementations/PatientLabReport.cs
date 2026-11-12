using HISWEBAPI.Data.Helpers;
using HISWEBAPI.Services;
using log4net;
using Microsoft.Extensions.Caching.Distributed;
using System.Data;
using System.Reflection;

namespace HISWEBAPI.Repositories.Implementations
{
    public class PatientLabReport : Interfaces.IPatientLabReport
    {
        private readonly ICustomSqlHelper _sqlHelper;
        private readonly IResponseMessageService _messageService;
        private readonly IDistributedCache _distributedCache;
        private readonly IConfiguration _configuration;
        private static readonly ILog _log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public PatientLabReport(
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

        public DataTable GetLabHeaderFooter(int branchId, int typeId = 4, int dummyMode = 0)
        {
            return _sqlHelper.GetDataTable(
                GetReportStoredProcedure("S_GetPatientHeaderMaster", "S_GetPatientHeaderMasterDummy", dummyMode),
                CommandType.StoredProcedure,
                new
                {
                    @branchId = branchId,
                    @typeId = typeId
                }) ?? new DataTable();
        }

        public DataTable GetPatientInvestigationsForReportPrint(int branchId, int isHeaderPng, string patientInvestigationIdList, int userId, int dummyMode = 0)
        {
            return _sqlHelper.GetDataTable(
                GetReportStoredProcedure("S_GetPatientInvestigationsForReportPrint", "S_GetPatientInvestigationsForReportPrintDummy", dummyMode, "S_GetLabReportPrintPreviewInvestigations"),
                CommandType.StoredProcedure,
                new
                {
                    @PatientInvestigationIdList = patientInvestigationIdList,
                    @branchId = branchId,
                    @isHeaderPNG = isHeaderPng,
                    @PrintBy = userId
                }) ?? new DataTable();
        }

        public DataTable GetPatientTabularReportForPrint(int patientInvestigationId, int dummyMode = 0)
        {
            return _sqlHelper.GetDataTable(
                GetReportStoredProcedure("S_GetPatientTabularResultsForPrint", "S_GetPatientTabularResultsForPrintDummy", dummyMode, "S_GetLabReportPrintPreviewTabularResults"),
                CommandType.StoredProcedure,
                new { @PTInvstId = patientInvestigationId }) ?? new DataTable();
        }

        public DataTable GetPatientAllergyReportForPrint(int patientInvestigationId, int dummyMode = 0)
        {
            return _sqlHelper.GetDataTable(
                GetReportStoredProcedure("S_GetPatientAllergyResultsForPrint", "S_GetPatientAllergyResultsForPrintDummy", dummyMode),
                CommandType.StoredProcedure,
                new { @PTInvstId = patientInvestigationId }) ?? new DataTable();
        }

        public DataTable GetPatientFreeTextReportForPrint(int patientInvestigationId, int dummyMode = 0)
        {
            return _sqlHelper.GetDataTable(
                GetReportStoredProcedure("S_GetPatientFreeTextResultsForPrint", "S_GetPatientFreeTextResultsForPrintDummy", dummyMode, "S_GetLabReportPrintPreviewFreeTextResults"),
                CommandType.StoredProcedure,
                new { @PTInvstId = patientInvestigationId }) ?? new DataTable();
        }

        public DataTable GetPatientHistoReportForPrint(int patientInvestigationId, int dummyMode = 0)
        {
            return _sqlHelper.GetDataTable(
                GetReportStoredProcedure("S_GetPatientHistoResultsForPrint", "S_GetPatientHistoResultsForPrintDummy", dummyMode),
                CommandType.StoredProcedure,
                new { @PTInvstId = patientInvestigationId }) ?? new DataTable();
        }

        public DataTable GetPatientMicroReportForPrint(int patientInvestigationId, int dummyMode = 0)
        {
            return _sqlHelper.GetDataTable(
                GetReportStoredProcedure("S_GetPatientMicroResultsForPrint", "S_GetPatientMicroResultsForPrintDummy", dummyMode),
                CommandType.StoredProcedure,
                new { @PTInvstId = patientInvestigationId }) ?? new DataTable();
        }

        /// <summary>DummyMode used by the print-setting preview API (dummy data from LabReportDummy* tables).</summary>
        public const int PrintSettingPreviewMode = 3;

        /// <summary>
        /// dummyMode 2 -> investigation template preview SPs (…Dummy)
        /// dummyMode 3 -> print-setting preview SPs (LabReportDummy* tables); falls back to the normal SP
        ///               when no preview SP exists (e.g. header/footer, which must stay the real branch setup)
        /// </summary>
        private static string GetReportStoredProcedure(string normalSP, string dummySP, int dummyMode, string previewSP = null)
        {
            if (dummyMode == 2) return dummySP;
            if (dummyMode == PrintSettingPreviewMode && !string.IsNullOrEmpty(previewSP)) return previewSP;
            return normalSP;
        }
    }
}
