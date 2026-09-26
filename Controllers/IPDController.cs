using HISWEBAPI.Configuration;
using HISWEBAPI.DTO;
using HISWEBAPI.Repositories.Implementations;
using HISWEBAPI.Repositories.Interfaces;
using HISWEBAPI.Services;
using log4net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Reflection;

namespace HISWEBAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class IPDController : ControllerBase
    {
        private readonly IIPDRepository _ipdRepository;
        private readonly IResponseMessageService _messageService;
        private static readonly ILog _log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public IPDController(
            IIPDRepository ipdRepository,
            IResponseMessageService messageService)
        {
            _ipdRepository = ipdRepository;
            _messageService = messageService;
        }

        [HttpGet("getIPDPatientBedHistory")]
        [Authorize]
        public IActionResult GetIPDPatientBedHistory([FromQuery] int visitId)
        {
            _log.Info($"GetIPDPatientBedHistory called. VisitId={visitId}");

            if (visitId <= 0)
            {
                _log.Warn("Invalid VisitId provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "VisitId must be greater than 0",
                    errors = new { visitId }
                });
            }

            var serviceResult = _ipdRepository.GetIPDPatientBedHistory(visitId);

            if (serviceResult.Result)
                _log.Info($"IPD bed history fetched successfully: {serviceResult.Message}");
            else
                _log.Warn($"IPD bed history fetch failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpPatch("transferIPDPatientBed")]
        [Authorize]
        public IActionResult TransferIPDPatientBed([FromBody] TransferIPDPatientBedRequest request)
        {
            _log.Info($"TransferIPDPatientBed called. VisitId={request?.VisitId}, CurrentBedId={request?.CurrentBedId}, NewBedId={request?.NewBedId}");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for TransferIPDPatientBed.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("MODEL_VALIDATION_FAILED");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = alert.Message,
                    errors = ModelState
                });
            }

            if (request.NewBedId == request.CurrentBedId)
            {
                _log.Warn("NewBedId and CurrentBedId are the same.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "NewBedId must be different from CurrentBedId",
                    errors = new { request.NewBedId, request.CurrentBedId }
                });
            }

            var globalValues = GlobalFunctions.GetGlobalValues(HttpContext);
            var serviceResult = _ipdRepository.TransferIPDPatientBed(request, globalValues);

            if (serviceResult.Result)
                _log.Info($"IPD patient bed transferred successfully: {serviceResult.Message}");
            else
                _log.Warn($"IPD patient bed transfer failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getIPDPatientDoctorHistory")]
        [Authorize]
        public IActionResult GetIPDPatientDoctorHistory([FromQuery] int visitId)
        {
            _log.Info($"GetIPDPatientDoctorHistory called. VisitId={visitId}");

            if (visitId <= 0)
            {
                _log.Warn("Invalid VisitId provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "VisitId must be greater than 0",
                    errors = new { visitId }
                });
            }

            var serviceResult = _ipdRepository.GetIPDPatientDoctorHistory(visitId);

            if (serviceResult.Result)
                _log.Info($"IPD doctor history fetched successfully: {serviceResult.Message}");
            else
                _log.Warn($"IPD doctor history fetch failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpPatch("transferIPDPatientDoctor")]
        [Authorize]
        public IActionResult TransferIPDPatientDoctor([FromBody] TransferIPDPatientDoctorRequest request)
        {
            _log.Info($"TransferIPDPatientDoctor called. VisitId={request?.VisitId}, PrimaryDoctorId={request?.PrimaryDoctorId}, BranchId={request?.BranchId}");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for TransferIPDPatientDoctor.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("MODEL_VALIDATION_FAILED");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = alert.Message,
                    errors = ModelState
                });
            }

            if (request.SecondaryDoctorIds != null &&
                request.SecondaryDoctorIds.Contains(request.PrimaryDoctorId))
            {
                _log.Warn("PrimaryDoctorId cannot also appear in SecondaryDoctorIds.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "PrimaryDoctorId cannot be included in SecondaryDoctorIds",
                    errors = new { request.PrimaryDoctorId, request.SecondaryDoctorIds }
                });
            }

            if (request.SecondaryDoctorIds != null && request.SecondaryDoctorIds.Any(id => id <= 0))
            {
                _log.Warn("Invalid SecondaryDoctorIds provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "All SecondaryDoctorIds must be greater than 0",
                    errors = new { request.SecondaryDoctorIds }
                });
            }

            var globalValues = GlobalFunctions.GetGlobalValues(HttpContext);
            var serviceResult = _ipdRepository.TransferIPDPatientDoctor(request, globalValues);

            if (serviceResult.Result)
                _log.Info($"IPD patient doctor transferred successfully: {serviceResult.Message}");
            else
                _log.Warn($"IPD patient doctor transfer failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getIPDPatientCorporateHistory")]
        [Authorize]
        public IActionResult GetIPDPatientCorporateHistory([FromQuery] int visitId)
        {
            _log.Info($"GetIPDPatientCorporateHistory called. VisitId={visitId}");

            if (visitId <= 0)
            {
                _log.Warn("Invalid VisitId provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "VisitId must be greater than 0",
                    errors = new { visitId }
                });
            }

            var serviceResult = _ipdRepository.GetIPDPatientCorporateHistory(visitId);

            if (serviceResult.Result)
                _log.Info($"IPD corporate history fetched successfully: {serviceResult.Message}");
            else
                _log.Warn($"IPD corporate history fetch failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpPatch("updateIPDPatientTariffDetails")]
        [Authorize]
        public IActionResult UpdateIPDPatientTariffDetails([FromBody] UpdateIPDPatientTariffDetailsRequest request)
        {
            _log.Info($"UpdateIPDPatientTariffDetails called. VisitId={request?.VisitId}, PatientId={request?.PatientId}, CorporateId={request?.CorporateId}, IsChangeTariff={request?.IsChangeTariff}");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for UpdateIPDPatientTariffDetails.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("MODEL_VALIDATION_FAILED");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = alert.Message,
                    errors = ModelState
                });
            }

            if (request.IsChangeTariff != 0 && request.IsChangeTariff != 1)
            {
                _log.Warn("Invalid IsChangeTariff value provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "IsChangeTariff must be 0 or 1",
                    errors = new { request.IsChangeTariff }
                });
            }

            if (request.IsChangeTariff == 1)
            {
                if (string.IsNullOrWhiteSpace(request.ChangeTariffFromDate))
                {
                    _log.Warn("ChangeTariffFromDate is missing.");
                    var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                    return BadRequest(new
                    {
                        result = false,
                        messageType = alert.Type,
                        message = "Change Tariff From Date is required when Change Tariff is Enable",
                        errors = new { request.ChangeTariffFromDate }
                    });
                }

                if (string.IsNullOrWhiteSpace(request.ChangeTariffToDate))
                {
                    _log.Warn("ChangeTariffToDate is missing.");
                    var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                    return BadRequest(new
                    {
                        result = false,
                        messageType = alert.Type,
                        message = "Change Tariff To Date is required when Change Tariff is Enable",
                        errors = new { request.ChangeTariffToDate }
                    });
                }

                if (!DateTime.TryParse(request.ChangeTariffFromDate, out _) ||
                    !DateTime.TryParse(request.ChangeTariffToDate, out _))
                {
                    _log.Warn("Invalid date format for ChangeTariffFromDate/ChangeTariffToDate.");
                    var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                    return BadRequest(new
                    {
                        result = false,
                        messageType = alert.Type,
                        message = "ChangeTariffFromDate and ChangeTariffToDate must be valid dates",
                        errors = new { request.ChangeTariffFromDate, request.ChangeTariffToDate }
                    });
                }
            }

            var globalValues = GlobalFunctions.GetGlobalValues(HttpContext);
            var serviceResult = _ipdRepository.UpdateIPDPatientTariffDetails(request, globalValues);

            if (serviceResult.Result)
                _log.Info($"IPD patient tariff details updated successfully: {serviceResult.Message}");
            else
                _log.Warn($"IPD patient tariff details update failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpPost("saveCorporateTransferRequestApproval")]
        [Authorize]
        public IActionResult SaveCorporateTransferRequestApproval([FromBody] SaveCorporateTransferRequestApprovalRequest request)
        {
            _log.Info($"SaveCorporateTransferRequestApproval called. PatientId={request?.PatientId}, BranchId={request?.BranchId}, VisitId={request?.VisitId}");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for SaveCorporateTransferRequestApproval.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("MODEL_VALIDATION_FAILED");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = alert.Message,
                    errors = ModelState
                });
            }

            if (request.PatientId <= 0)
            {
                _log.Warn("Invalid PatientId provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "PatientId must be greater than 0",
                    errors = new { patientId = request.PatientId }
                });
            }

            if (request.BranchId <= 0)
            {
                _log.Warn("Invalid BranchId provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "BranchId must be greater than 0",
                    errors = new { branchId = request.BranchId }
                });
            }

            if (request.VisitId <= 0)
            {
                _log.Warn("Invalid VisitId provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "VisitId must be greater than 0",
                    errors = new { visitId = request.VisitId }
                });
            }

            if (request.CorporateId <= 0)
            {
                _log.Warn("Invalid CorporateId provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "CorporateId must be greater than 0",
                    errors = new { corporateId = request.CorporateId }
                });
            }

            var globalValues = GlobalFunctions.GetGlobalValues(HttpContext);
            var serviceResult = _ipdRepository.SaveCorporateTransferRequestApproval(request, globalValues);

            if (serviceResult.Result)
                _log.Info($"SaveCorporateTransferRequestApproval succeeded: {serviceResult.Message}");
            else
                _log.Warn($"SaveCorporateTransferRequestApproval failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpPatch("approveCorporateTransferRequest")]
        [Authorize]
        public IActionResult ApproveCorporateTransferRequest([FromBody] ApproveCorporateTransferRequestRequest request)
        {
            _log.Info($"ApproveCorporateTransferRequest called. CorporateTransferId={request?.CorporateTransferId}, Flag={request?.Flag}");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for ApproveCorporateTransferRequest.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("MODEL_VALIDATION_FAILED");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = alert.Message,
                    errors = ModelState
                });
            }

            var globalValues = GlobalFunctions.GetGlobalValues(HttpContext);
            var serviceResult = _ipdRepository.ApproveCorporateTransferRequest(request, globalValues);

            if (serviceResult.Result)
                _log.Info($"ApproveCorporateTransferRequest succeeded: {serviceResult.Message}");
            else
                _log.Warn($"ApproveCorporateTransferRequest failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpPatch("cancelCorporateTransferRequest")]
        [Authorize]
        public IActionResult CancelCorporateTransferRequest([FromBody] CancelCorporateTransferRequestRequest request)
        {
            _log.Info($"CancelCorporateTransferRequest called. CorporateTransferId={request?.CorporateTransferId}");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for CancelCorporateTransferRequest.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("MODEL_VALIDATION_FAILED");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = alert.Message,
                    errors = ModelState
                });
            }

            var globalValues = GlobalFunctions.GetGlobalValues(HttpContext);
            var serviceResult = _ipdRepository.CancelCorporateTransferRequest(request, globalValues);

            if (serviceResult.Result)
                _log.Info($"CancelCorporateTransferRequest succeeded: {serviceResult.Message}");
            else
                _log.Warn($"CancelCorporateTransferRequest failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpPatch("confirmCorporateTransferRequest")]
        [Authorize]
        public IActionResult ConfirmCorporateTransferRequest([FromBody] ConfirmCorporateTransferRequestRequest request)
        {
            _log.Info($"ConfirmCorporateTransferRequest called. CorporateTransferId={request?.CorporateTransferId}");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for ConfirmCorporateTransferRequest.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("MODEL_VALIDATION_FAILED");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = alert.Message,
                    errors = ModelState
                });
            }

            var globalValues = GlobalFunctions.GetGlobalValues(HttpContext);
            var serviceResult = _ipdRepository.ConfirmCorporateTransferRequest(request, globalValues);

            if (serviceResult.Result)
                _log.Info($"ConfirmCorporateTransferRequest succeeded: {serviceResult.Message}");
            else
                _log.Warn($"ConfirmCorporateTransferRequest failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getCorporateTransferRequestListForApproval")]
        [Authorize]
        public IActionResult GetCorporateTransferRequestListForApproval(
            [FromQuery] string fromDate,
            [FromQuery] string toDate,
            [FromQuery] int branchId)
        {
            _log.Info($"GetCorporateTransferRequestListForApproval called. FromDate={fromDate}, ToDate={toDate}, BranchId={branchId}");

            if (string.IsNullOrWhiteSpace(fromDate))
            {
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new { result = false, messageType = alert.Type, message = "FromDate is required" });
            }

            if (string.IsNullOrWhiteSpace(toDate))
            {
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new { result = false, messageType = alert.Type, message = "ToDate is required" });
            }

            if (branchId <= 0)
            {
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new { result = false, messageType = alert.Type, message = "BranchId must be greater than 0", errors = new { branchId } });
            }

            var globalValues = GlobalFunctions.GetGlobalValues(HttpContext);
            var serviceResult = _ipdRepository.GetCorporateTransferRequestListForApproval(fromDate, toDate, branchId, globalValues);

            if (serviceResult.Result)
                _log.Info($"GetCorporateTransferRequestListForApproval fetched successfully: {serviceResult.Message}");
            else
                _log.Warn($"GetCorporateTransferRequestListForApproval failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getCorporateTransferRequestDetailsByCorporateTransferId")]
        [Authorize]
        public IActionResult GetCorporateTransferRequestDetailsByCorporateTransferId([FromQuery] int corporateTransferId)
        {
            _log.Info($"GetCorporateTransferRequestDetailsByCorporateTransferId called. CorporateTransferId={corporateTransferId}");

            if (corporateTransferId <= 0)
            {
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "CorporateTransferId must be greater than 0",
                    errors = new { corporateTransferId }
                });
            }

            var serviceResult = _ipdRepository.GetCorporateTransferRequestDetailsByCorporateTransferId(corporateTransferId);

            if (serviceResult.Result)
                _log.Info($"GetCorporateTransferRequestDetailsByCorporateTransferId fetched successfully: {serviceResult.Message}");
            else
                _log.Warn($"GetCorporateTransferRequestDetailsByCorporateTransferId failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getCorporateTransferRequestApprovalDetails")]
        [Authorize]
        public IActionResult GetCorporateTransferRequestApprovalDetails([FromQuery] int corporateTransferId)
        {
            _log.Info($"GetCorporateTransferRequestApprovalDetails called. CorporateTransferId={corporateTransferId}");

            if (corporateTransferId <= 0)
            {
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "CorporateTransferId must be greater than 0",
                    errors = new { corporateTransferId }
                });
            }

            var serviceResult = _ipdRepository.GetCorporateTransferRequestApprovalDetails(corporateTransferId);

            if (serviceResult.Result)
                _log.Info($"GetCorporateTransferRequestApprovalDetails fetched successfully: {serviceResult.Message}");
            else
                _log.Warn($"GetCorporateTransferRequestApprovalDetails failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getCorporateTransferRequestDetailsByVisitId")]
        [Authorize]
        public IActionResult GetCorporateTransferRequestDetailsByVisitId([FromQuery] int visitId)
        {
            _log.Info($"GetCorporateTransferRequestDetailsByVisitId called. VisitId={visitId}");

            if (visitId <= 0)
            {
                _log.Warn("Invalid VisitId provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "VisitId must be greater than 0",
                    errors = new { visitId }
                });
            }

            var serviceResult = _ipdRepository.GetCorporateTransferRequestDetailsByVisitId(visitId);

            if (serviceResult.Result)
                _log.Info($"Corporate transfer request details fetched successfully: {serviceResult.Message}");
            else
                _log.Warn($"Corporate transfer request details fetch failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpPost("saveIPDBilling")]
        [Authorize]
        public IActionResult SaveIPDBilling([FromBody] SaveIPDBillingRequest request)
        {
            _log.Info($"SaveIPDBilling called. PatientId={request?.VisitDetails?.PatientId}, VisitId={request?.VisitDetails?.VisitId}, BranchId={request?.VisitDetails?.BranchId}");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for SaveIPDBilling.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("MODEL_VALIDATION_FAILED");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = alert.Message,
                    errors = ModelState
                });
            }

            if (request.BillingItems == null || request.BillingItems.Count == 0)
            {
                _log.Warn("No billing items provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "At least one billing item is required",
                    errors = new[] { "BillingItems cannot be empty" }
                });
            }

            if (request.VisitDetails.PatientId <= 0)
            {
                _log.Warn("Invalid PatientId provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "PatientId must be greater than 0",
                    errors = new { patientId = request.VisitDetails.PatientId }
                });
            }

            if (request.VisitDetails.BranchId <= 0)
            {
                _log.Warn("Invalid BranchId provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "BranchId must be greater than 0",
                    errors = new { branchId = request.VisitDetails.BranchId }
                });
            }

            if (request.VisitDetails.VisitId <= 0)
            {
                _log.Warn("Invalid VisitId provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "VisitId must be greater than 0",
                    errors = new { visitId = request.VisitDetails.VisitId }
                });
            }

            var globalValues = GlobalFunctions.GetGlobalValues(HttpContext);
            var serviceResult = _ipdRepository.SaveIPDBilling(request, globalValues);

            if (serviceResult.Result)
                _log.Info($"SaveIPDBilling succeeded: {serviceResult.Message}");
            else
                _log.Warn($"SaveIPDBilling failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getIPDBillingSummary")]
        [Authorize]
        public IActionResult GetIPDBillingSummary(
    [FromQuery] int branchId,
    [FromQuery] int visitId)
        {
            _log.Info($"GetIPDBillingSummary called. BranchId={branchId}, VisitId={visitId}");

            if (branchId <= 0)
            {
                _log.Warn("Invalid BranchId provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "BranchId must be greater than 0",
                    errors = new { branchId }
                });
            }

            if (visitId <= 0)
            {
                _log.Warn("Invalid VisitId provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "VisitId must be greater than 0",
                    errors = new { visitId }
                });
            }

            var serviceResult = _ipdRepository.GetIPDBillingSummary(branchId, visitId);

            if (serviceResult.Result)
                _log.Info($"IPD billing summary fetched successfully: {serviceResult.Message}");
            else
                _log.Warn($"IPD billing summary fetch failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getIPDPatientBillAmounts")]
        [Authorize]
        public IActionResult GetIPDPatientBillAmounts(
            [FromQuery] int visitId,
            [FromQuery] int patientId)
        {
            _log.Info($"GetIPDPatientBillAmounts called. VisitId={visitId}, PatientId={patientId}");

            if (visitId <= 0)
            {
                _log.Warn("Invalid VisitId provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "VisitId must be greater than 0",
                    errors = new { visitId }
                });
            }

            if (patientId <= 0)
            {
                _log.Warn("Invalid PatientId provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "PatientId must be greater than 0",
                    errors = new { patientId }
                });
            }

            var serviceResult = _ipdRepository.GetIPDPatientBillAmounts(visitId, patientId);

            if (serviceResult.Result)
                _log.Info($"IPD patient bill amounts fetched successfully: {serviceResult.Message}");
            else
                _log.Warn($"IPD patient bill amounts fetch failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getIPDPatientOrderDetails")]
        [Authorize]
        public IActionResult GetIPDPatientOrderDetails([FromQuery] int ftid)
        {
            _log.Info($"GetIPDPatientOrderDetails called. FTID={ftid}");

            if (ftid <= 0)
            {
                _log.Warn("Invalid FTID provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "FTID must be greater than 0",
                    errors = new { ftid }
                });
            }

            var globalValues = GlobalFunctions.GetGlobalValues(HttpContext);
            var serviceResult = _ipdRepository.GetIPDPatientOrderDetails(ftid, globalValues);

            if (serviceResult.Result)
                _log.Info($"IPD patient order details fetched successfully: {serviceResult.Message}");
            else
                _log.Warn($"IPD patient order details fetch failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }


        [HttpPatch("removeIPDServiceItem")]
        [Authorize]
        public IActionResult RemoveIPDServiceItem([FromBody] RemoveIPDServiceItemRequest request)
        {
            _log.Info($"RemoveIPDServiceItem called. VisitId={request?.VisitId}, FTDIdList={request?.FTDIdList}");

            if (!ModelState.IsValid)
            {
                var alert = _messageService.GetMessageAndTypeByAlertCode("MODEL_VALIDATION_FAILED");
                return BadRequest(new { result = false, messageType = alert.Type, message = alert.Message, errors = ModelState });
            }

            var globalValues = GlobalFunctions.GetGlobalValues(HttpContext);
            var serviceResult = _ipdRepository.RemoveIPDServiceItem(request, globalValues);

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpPatch("updateIPDServicePackage")]
        [Authorize]
        public IActionResult UpdateIPDServicePackage([FromBody] UpdateIPDServicePackageRequest request)
        {
            _log.Info($"UpdateIPDServicePackage called. VisitId={request?.VisitId}, FTDIdList={request?.FTDIdList}, PackageId={request?.PackageId}");

            if (!ModelState.IsValid)
            {
                var alert = _messageService.GetMessageAndTypeByAlertCode("MODEL_VALIDATION_FAILED");
                return BadRequest(new { result = false, messageType = alert.Type, message = alert.Message, errors = ModelState });
            }

            var globalValues = GlobalFunctions.GetGlobalValues(HttpContext);
            var serviceResult = _ipdRepository.UpdateIPDServicePackage(request, globalValues);

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpPatch("updateIPDServiceCorporateNonPayable")]
        [Authorize]
        public IActionResult UpdateIPDServiceCorporateNonPayable([FromBody] UpdateIPDServiceCorporateNonPayableRequest request)
        {
            _log.Info($"UpdateIPDServiceCorporateNonPayable called. VisitId={request?.VisitId}, FTDIdList={request?.FTDIdList}, IsNonPayable={request?.IsNonPayable}");

            if (!ModelState.IsValid)
            {
                var alert = _messageService.GetMessageAndTypeByAlertCode("MODEL_VALIDATION_FAILED");
                return BadRequest(new { result = false, messageType = alert.Type, message = alert.Message, errors = ModelState });
            }

            var globalValues = GlobalFunctions.GetGlobalValues(HttpContext);
            var serviceResult = _ipdRepository.UpdateIPDServiceCorporateNonPayable(request, globalValues);

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpPatch("updateIPDServiceDiscAmt")]
        [Authorize]
        public IActionResult UpdateIPDServiceDiscAmt([FromBody] UpdateIPDServiceDiscAmtRequest request)
        {
            _log.Info($"UpdateIPDServiceDiscAmt called. VisitId={request?.VisitId}, FTDIdList={request?.FTDIdList}, DiscAmt={request?.DiscAmt}");

            if (!ModelState.IsValid)
            {
                var alert = _messageService.GetMessageAndTypeByAlertCode("MODEL_VALIDATION_FAILED");
                return BadRequest(new { result = false, messageType = alert.Type, message = alert.Message, errors = ModelState });
            }

            var globalValues = GlobalFunctions.GetGlobalValues(HttpContext);
            var serviceResult = _ipdRepository.UpdateIPDServiceDiscAmt(request, globalValues);

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpPatch("updateIPDServiceDiscPer")]
        [Authorize]
        public IActionResult UpdateIPDServiceDiscPer([FromBody] UpdateIPDServiceDiscPerRequest request)
        {
            _log.Info($"UpdateIPDServiceDiscPer called. VisitId={request?.VisitId}, FTDIdList={request?.FTDIdList}, DiscPer={request?.DiscPer}");

            if (!ModelState.IsValid)
            {
                var alert = _messageService.GetMessageAndTypeByAlertCode("MODEL_VALIDATION_FAILED");
                return BadRequest(new { result = false, messageType = alert.Type, message = alert.Message, errors = ModelState });
            }

            var globalValues = GlobalFunctions.GetGlobalValues(HttpContext);
            var serviceResult = _ipdRepository.UpdateIPDServiceDiscPer(request, globalValues);

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpPatch("updateIPDServiceRate")]
        [Authorize]
        public IActionResult UpdateIPDServiceRate([FromBody] UpdateIPDServiceRateRequest request)
        {
            _log.Info($"UpdateIPDServiceRate called. VisitId={request?.VisitId}, FTDIdList={request?.FTDIdList}, Rate={request?.Rate}");

            if (!ModelState.IsValid)
            {
                var alert = _messageService.GetMessageAndTypeByAlertCode("MODEL_VALIDATION_FAILED");
                return BadRequest(new { result = false, messageType = alert.Type, message = alert.Message, errors = ModelState });
            }

            var globalValues = GlobalFunctions.GetGlobalValues(HttpContext);
            var serviceResult = _ipdRepository.UpdateIPDServiceRate(request, globalValues);

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpPatch("updateIPDServiceQty")]
        [Authorize]
        public IActionResult UpdateIPDServiceQty([FromBody] UpdateIPDServiceQtyRequest request)
        {
            _log.Info($"UpdateIPDServiceQty called. VisitId={request?.VisitId}, FTDIdList={request?.FTDIdList}, Qty={request?.Qty}");

            if (!ModelState.IsValid)
            {
                var alert = _messageService.GetMessageAndTypeByAlertCode("MODEL_VALIDATION_FAILED");
                return BadRequest(new { result = false, messageType = alert.Type, message = alert.Message, errors = ModelState });
            }

            var globalValues = GlobalFunctions.GetGlobalValues(HttpContext);
            var serviceResult = _ipdRepository.UpdateIPDServiceQty(request, globalValues);

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpPost("saveIPDPatientAdvance")]
        [Authorize]
        public IActionResult SaveIPDPatientAdvance([FromBody] SaveIPDPatientAdvanceRequest request)
        {
            _log.Info($"SaveIPDPatientAdvance called. Type={request?.Type}, PatientId={request?.PatientId}, VisitId={request?.VisitId}");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for SaveIPDPatientAdvance.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("MODEL_VALIDATION_FAILED");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = alert.Message,
                    errors = ModelState
                });
            }

            if (request.PaymentDetails == null || request.PaymentDetails.Count == 0)
            {
                _log.Warn("No payment details provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "At least one payment detail is required",
                    errors = new[] { "PaymentDetails cannot be empty" }
                });
            }

            var globalValues = GlobalFunctions.GetGlobalValues(HttpContext);
            var serviceResult = _ipdRepository.SaveIPDPatientAdvance(request, globalValues);

            if (serviceResult.Result)
                _log.Info($"SaveIPDPatientAdvance succeeded: {serviceResult.Message}");
            else
                _log.Warn($"SaveIPDPatientAdvance failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getIPDReceiptDetails")]
        [Authorize]
        public IActionResult GetIPDReceiptDetails([FromQuery] int receiptId)
        {
            _log.Info($"GetIPDReceiptDetails called. ReceiptId={receiptId}");

            if (receiptId <= 0)
            {
                _log.Warn("Invalid ReceiptId provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "ReceiptId must be greater than 0",
                    errors = new { receiptId }
                });
            }

            var serviceResult = _ipdRepository.GetIPDReceiptDetails(receiptId);

            if (serviceResult.Result)
                _log.Info($"IPD receipt details fetched successfully: {serviceResult.Message}");
            else
                _log.Warn($"IPD receipt details fetch failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }
        // ─── Patient Workflow ──────────────────────────────────────────────

        [HttpPost("initializePatientDischargeProcess")]
        [Authorize]
        public IActionResult InitializePatientDischargeProcess([FromBody] InitializePatientDischargeProcessRequest request)
        {
            _log.Info($"InitializePatientDischargeProcess called. VisitId={request?.VisitId}, CorporateId={request?.CorporateId}");

            if (!ModelState.IsValid)
            {
                var alert = _messageService.GetMessageAndTypeByAlertCode("MODEL_VALIDATION_FAILED");
                return BadRequest(new { result = false, messageType = alert.Type, message = alert.Message, errors = ModelState });
            }

            var globalValues = GlobalFunctions.GetGlobalValues(HttpContext);
            var serviceResult = _ipdRepository.InitializePatientDischargeProcess(request, globalValues);

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getPatientDischargeProcess")]
        [Authorize]
        public IActionResult GetPatientDischargeProcess([FromQuery] int visitId, [FromQuery] int branchId)
        {
            _log.Info($"GetPatientDischargeProcess called. VisitId={visitId}");

            if (visitId <= 0)
            {
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new { result = false, messageType = alert.Type, message = "VisitId must be greater than 0", errors = new { visitId } });
            }

            if (branchId <= 0)
            {
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new { result = false, messageType = alert.Type, message = "BranchId must be greater than 0", errors = new { branchId } });
            }
            var globalValues = GlobalFunctions.GetGlobalValues(HttpContext);
            var serviceResult = _ipdRepository.GetPatientDischargeProcess(visitId,branchId, globalValues);

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getCurrentDischargeProcess")]
        [Authorize]
        public IActionResult GetCurrentDischargeProcess([FromQuery] int visitId)
        {
            _log.Info($"GetCurrentDischargeProcess called. VisitId={visitId}");

            if (visitId <= 0)
            {
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new { result = false, messageType = alert.Type, message = "VisitId must be greater than 0", errors = new { visitId } });
            }

            var serviceResult = _ipdRepository.GetCurrentDischargeProcess(visitId);

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpPatch("startPatientDischargeProcess")]
        [Authorize]
        public IActionResult StartPatientDischargeProcess([FromBody] StartPatientDischargeProcessRequest request)
        {
            _log.Info($"StartPatientDischargeProcess called. VisitId={request?.VisitId}, DischargeProcessId={request?.DischargeProcessId}");

            if (!ModelState.IsValid)
            {
                var alert = _messageService.GetMessageAndTypeByAlertCode("MODEL_VALIDATION_FAILED");
                return BadRequest(new { result = false, messageType = alert.Type, message = alert.Message, errors = ModelState });
            }

            var globalValues = GlobalFunctions.GetGlobalValues(HttpContext);
            var serviceResult = _ipdRepository.StartPatientDischargeProcess(request, globalValues);

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpPatch("completePatientDischargeProcess")]
        [Authorize]
        public IActionResult CompletePatientDischargeProcess([FromBody] CompletePatientDischargeProcessRequest request)
        {
            _log.Info($"CompletePatientDischargeProcess called. VisitId={request?.VisitId}, DischargeProcessId={request?.DischargeProcessId}");

            if (!ModelState.IsValid)
            {
                var alert = _messageService.GetMessageAndTypeByAlertCode("MODEL_VALIDATION_FAILED");
                return BadRequest(new { result = false, messageType = alert.Type, message = alert.Message, errors = ModelState });
            }

            var globalValues = GlobalFunctions.GetGlobalValues(HttpContext);
            var serviceResult = _ipdRepository.CompletePatientDischargeProcess(request, globalValues);

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("validatePatientDischarge")]
        [Authorize]
        public IActionResult ValidatePatientDischarge([FromQuery] int visitId)
        {
            _log.Info($"ValidatePatientDischarge called. VisitId={visitId}");

            if (visitId <= 0)
            {
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new { result = false, messageType = alert.Type, message = "VisitId must be greater than 0", errors = new { visitId } });
            }

            var serviceResult = _ipdRepository.ValidatePatientDischargeProcess(visitId);

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

       

        [HttpPatch("saveIPDDischarge")]
        [Authorize]
        public IActionResult SaveIPDDischarge([FromBody] SaveIPDDischargeRequest request)
        {
            _log.Info($"SaveIPDDischarge called. VisitId={request?.VisitId}, BedId={request?.BedId}, DischargeType={request?.DischargeType}");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for SaveIPDDischarge.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("MODEL_VALIDATION_FAILED");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = alert.Message,
                    errors = ModelState
                });
            }

            if (string.IsNullOrWhiteSpace(request.DischargeDate))
            {
                _log.Warn("DischargeDate is missing.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "DischargeDate is required",
                    errors = new { request.DischargeDate }
                });
            }

            if (string.IsNullOrWhiteSpace(request.DischargeTime))
            {
                _log.Warn("DischargeTime is missing.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "DischargeTime is required",
                    errors = new { request.DischargeTime }
                });
            }

            var globalValues = GlobalFunctions.GetGlobalValues(HttpContext);
            var serviceResult = _ipdRepository.SaveIPDDischarge(request, globalValues);

            if (serviceResult.Result)
                _log.Info($"IPD patient discharged successfully: {serviceResult.Message}");
            else
                _log.Warn($"IPD patient discharge failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }
        [HttpPost("createSupplementaryBillFromMainBill")]
        [Authorize]
        public IActionResult CreateSupplementaryBillFromMainBill([FromBody] CreateSupplementaryBillFromMainBillRequest request)
        {
            _log.Info($"SaveIPDBilling called. PatientId={request?.VisitDetails?.PatientId}, VisitId={request?.VisitDetails?.VisitId}, BranchId={request?.VisitDetails?.BranchId}");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for SaveIPDBilling.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("MODEL_VALIDATION_FAILED");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = alert.Message,
                    errors = ModelState
                });
            }

            if (request.BillingItems == null || request.BillingItems.Count == 0)
            {
                _log.Warn("No billing items provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "At least one billing item is required",
                    errors = new[] { "BillingItems cannot be empty" }
                });
            }

            if (request.VisitDetails.PatientId <= 0)
            {
                _log.Warn("Invalid PatientId provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "PatientId must be greater than 0",
                    errors = new { patientId = request.VisitDetails.PatientId }
                });
            }

            if (request.VisitDetails.BranchId <= 0)
            {
                _log.Warn("Invalid BranchId provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "BranchId must be greater than 0",
                    errors = new { branchId = request.VisitDetails.BranchId }
                });
            }

            if (request.VisitDetails.VisitId <= 0)
            {
                _log.Warn("Invalid VisitId provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "VisitId must be greater than 0",
                    errors = new { visitId = request.VisitDetails.VisitId }
                });
            }


            var globalValues = GlobalFunctions.GetGlobalValues(HttpContext);
            var serviceResult = _ipdRepository.CreateSupplementaryBillFromMainBill(request, globalValues);

            if (serviceResult.Result)
                _log.Info($"SaveIPDBilling succeeded: {serviceResult.Message}");
            else
                _log.Warn($"SaveIPDBilling failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        // ─── Patient OT Workflow ───────────────────────────────────────────────────

        [HttpPost("initializePatientOTProcess")]
        [Authorize]
        public IActionResult InitializePatientOTProcess([FromBody] InitializePatientOTProcessRequest request)
        {
            _log.Info($"InitializePatientOTProcess called. VisitId={request?.VisitId}");

            if (!ModelState.IsValid)
            {
                var alert = _messageService.GetMessageAndTypeByAlertCode("MODEL_VALIDATION_FAILED");
                return BadRequest(new { result = false, messageType = alert.Type, message = alert.Message, errors = ModelState });
            }

            var globalValues = GlobalFunctions.GetGlobalValues(HttpContext);
            var serviceResult = _ipdRepository.InitializePatientOTProcess(request, globalValues);

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getPatientOTProcess")]
        [Authorize]
        public IActionResult GetPatientOTProcess([FromQuery] int visitId)
        {
            _log.Info($"GetPatientOTProcess called. VisitId={visitId}");

            if (visitId <= 0)
            {
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new { result = false, messageType = alert.Type, message = "VisitId must be greater than 0", errors = new { visitId } });
            }

            var serviceResult = _ipdRepository.GetPatientOTProcess(visitId);

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getCurrentOTProcess")]
        [Authorize]
        public IActionResult GetCurrentOTProcess([FromQuery] int visitId)
        {
            _log.Info($"GetCurrentOTProcess called. VisitId={visitId}");

            if (visitId <= 0)
            {
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new { result = false, messageType = alert.Type, message = "VisitId must be greater than 0", errors = new { visitId } });
            }

            var serviceResult = _ipdRepository.GetCurrentOTProcess(visitId);

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpPatch("completePatientOTProcess")]
        [Authorize]
        public IActionResult CompletePatientOTProcess([FromBody] CompletePatientOTProcessRequest request)
        {
            _log.Info($"CompletePatientOTProcess called. VisitId={request?.VisitId}, OTProcessId={request?.OTProcessId}");

            if (!ModelState.IsValid)
            {
                var alert = _messageService.GetMessageAndTypeByAlertCode("MODEL_VALIDATION_FAILED");
                return BadRequest(new { result = false, messageType = alert.Type, message = alert.Message, errors = ModelState });
            }

            var globalValues = GlobalFunctions.GetGlobalValues(HttpContext);
            var serviceResult = _ipdRepository.CompletePatientOTProcess(request, globalValues);

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("validatePatientOTProcess")]
        [Authorize]
        public IActionResult ValidatePatientOTProcess([FromQuery] int visitId)
        {
            _log.Info($"ValidatePatientOTProcess called. VisitId={visitId}");

            if (visitId <= 0)
            {
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new { result = false, messageType = alert.Type, message = "VisitId must be greater than 0", errors = new { visitId } });
            }

            var serviceResult = _ipdRepository.ValidatePatientOTProcess(visitId);

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