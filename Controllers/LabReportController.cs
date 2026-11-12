using HISWEBAPI.Configuration;
using HISWEBAPI.DTO;
using HISWEBAPI.Repositories.Implementations;
using HISWEBAPI.Repositories.Interfaces;
using HISWEBAPI.Services;
using HISWEBAPI.Services.Implementations;
using HISWEBAPI.Services.Interfaces;
using log4net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Reflection;

namespace HISWEBAPI.Controllers
{
    // Same prefix as the existing report endpoint (api/Lab/...), action names are unique.
    [Route("api/Lab")]
    [ApiController]
    public class LabReportController : ControllerBase
    {
        private readonly ILabReportRepository _repository;
        private readonly IResponseMessageService _messageService;
        private readonly IPatientInvestigationReportPdfService _patientInvestigationReportPdfService;
        private static readonly ILog _log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public LabReportController(ILabReportRepository repository, IResponseMessageService messageService, IPatientInvestigationReportPdfService patientInvestigationReportPdfService)
        {
            _repository = repository;
            _messageService = messageService;
            _patientInvestigationReportPdfService = patientInvestigationReportPdfService;
        }


        [HttpGet("printPatientInvestigationReport")]
        [AllowAnonymous]
        public IActionResult printPatientInvestigationReport([FromQuery] PatientInvestigationReportRequest request)
        {
            _log.Info($"printPatientInvestigationReport called. PatientInvestigationIds={request?.PatientInvestigationIds}");

            if (string.IsNullOrWhiteSpace(request?.PatientInvestigationIds))
            {
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new { result = false, messageType = alert.Type, message = "PatientInvestigationIds is required" });
            }

            var globalValues = GlobalFunctions.GetGlobalValues(HttpContext);


            var baseUrl = $"{Request.Scheme}://{Request.Host}{Request.PathBase}";
            var pdfResult = _patientInvestigationReportPdfService.GenerateReport(request, globalValues, baseUrl);

            Response.Headers["Content-Disposition"] = $"{(request.Download ? "attachment" : "inline")}; filename=\"{pdfResult.FileName}\"";
            return File(pdfResult.Content, "application/pdf");
        }

        /// <summary>
        /// Print-setting preview: renders the normal lab report (same layout / code) with DUMMY data
        /// from the LabReportDummy* tables (7 tabular + 3 free-text tests, 10 pages).
        /// Use it to check changes made in LabReportPrintSettingMaster for a branch.
        /// Header/footer, letter head and signature come from the branch's real setup.
        /// No patient data is involved.
        /// </summary>
        /// <param name="branchId">branch whose print setting / header / letter head is used</param>
        /// <param name="isHeaderPng">1 = print the letter head image</param>
        /// <param name="investigationIds">optional comma separated dummy ids (1-10); empty = all 10</param>
        /// <param name="download">true = attachment, false = open inline</param>
        [HttpGet("printSettingPreviewReport")]
        [AllowAnonymous]
        public IActionResult PrintSettingPreviewReport(
            [FromQuery] int branchId,
            [FromQuery] int isHeaderPng = 0,
            [FromQuery] string? investigationIds = null,
            [FromQuery] bool download = false)
        {
            _log.Info($"PrintSettingPreviewReport called. BranchId={branchId}, IsHeaderPng={isHeaderPng}, Ids={investigationIds}");

            if (branchId <= 0)
            {
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new { result = false, messageType = alert.Type, message = "branchId must be greater than 0" });
            }

            var request = new PatientInvestigationReportRequest
            {
                PatientInvestigationIds = string.IsNullOrWhiteSpace(investigationIds) ? "0" : investigationIds.Trim(),
                BranchId = branchId,
                IsHeaderPng = isHeaderPng,
                Download = download,
                DummyMode = PatientLabReport.PrintSettingPreviewMode
            };

            var globalValues = GlobalFunctions.GetGlobalValues(HttpContext);
            var baseUrl = $"{Request.Scheme}://{Request.Host}{Request.PathBase}";
            var pdfResult = _patientInvestigationReportPdfService.GenerateReport(request, globalValues, baseUrl);

            Response.Headers["Content-Disposition"] = $"{(download ? "attachment" : "inline")}; filename=\"PrintSettingPreview_Branch{branchId}.pdf\"";
            return File(pdfResult.Content, "application/pdf");
        }

        /// <summary>Saved values for a branch. applyDefaults=0 -> raw (null = not overridden) for input VALUE.</summary>
        [HttpGet("getLabReportPrintSetting")]
        [Authorize]
        public IActionResult GetLabReportPrintSetting([FromQuery] int branchId, [FromQuery] int applyDefaults = 0)
        {
            _log.Info($"GetLabReportPrintSetting called. BranchId={branchId}, ApplyDefaults={applyDefaults}");

            if (branchId <= 0)
            {
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new { result = false, messageType = alert.Type, message = "BranchId must be greater than 0", errors = new { branchId } });
            }

            var serviceResult = _repository.GetLabReportPrintSetting(branchId, applyDefaults == 1);

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        /// <summary>Default values -> use as input PLACEHOLDERS.</summary>
        [HttpGet("getLabReportPrintSettingDefaults")]
        [Authorize]
        public IActionResult GetLabReportPrintSettingDefaults()
        {
            _log.Info("GetLabReportPrintSettingDefaults called.");

            var serviceResult = _repository.GetLabReportPrintSettingDefaults();

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        /// <summary>Save/update branch print setting. Send null/omit a field to fall back to default.</summary>
        [HttpPost("saveLabReportPrintSetting")]
        [Authorize]
        public IActionResult SaveLabReportPrintSetting([FromBody] SaveLabReportPrintSettingRequest request)
        {
            _log.Info($"SaveLabReportPrintSetting called. BranchId={request?.BranchId}");

            if (!ModelState.IsValid)
            {
                var alert = _messageService.GetMessageAndTypeByAlertCode("MODEL_VALIDATION_FAILED");
                return BadRequest(new { result = false, messageType = alert.Type, message = alert.Message, errors = ModelState });
            }

            var globalValues = GlobalFunctions.GetGlobalValues(HttpContext);
            var serviceResult = _repository.SaveLabReportPrintSetting(request, globalValues);

            if (serviceResult.Result)
                _log.Info($"SaveLabReportPrintSetting succeeded: {serviceResult.Message}");
            else
                _log.Warn($"SaveLabReportPrintSetting failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }
    }
}