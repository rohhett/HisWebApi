using HISWEBAPI.Configuration;
using HISWEBAPI.DTO;
using HISWEBAPI.Repositories.Implementations;
using HISWEBAPI.Repositories.Interfaces;
using HISWEBAPI.Services;
using log4net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Data;
using System.Reflection;

namespace HISWEBAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PatientController : ControllerBase
    {
        private readonly IPatientRepository _patientRepository;
        private readonly IResponseMessageService _messageService;
        private static readonly ILog _log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public PatientController(
            IPatientRepository patientRepository,
            IResponseMessageService messageService)
        {
            _patientRepository = patientRepository;
            _messageService = messageService;
        }

        [HttpPost("createUpdatePatientMaster")]
        [Authorize]
        public IActionResult CreateUpdatePatientMaster([FromForm] CreateUpdatePatientMasterRequest request)
        {
            _log.Info($"CreateUpdatePatientMaster called. PatientId={request.PatientId}, FirstName={request.FirstName}");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for patient insert/update.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("MODEL_VALIDATION_FAILED");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = alert.Message,
                    errors = ModelState
                });
            }

            // Validate DOB format
            if (string.IsNullOrWhiteSpace(request.Dob))
            {
                _log.Warn("DOB is missing.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "Date of birth is required",
                    errors = new { dob = "DOB cannot be empty" }
                });
            }

            // Validate BranchId
            if (request.BranchId <= 0)
            {
                _log.Warn("Invalid BranchId.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "BranchId must be greater than 0",
                    errors = new { branchId = request.BranchId }
                });
            }

            var globalValues = GlobalFunctions.GetGlobalValues(HttpContext);
            var serviceResult = _patientRepository.CreateUpdatePatientMaster(request, globalValues);

            if (serviceResult.Result)
                _log.Info($"Patient operation completed: {serviceResult.Message}");
            else
                _log.Warn($"Patient operation failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpPost("uploadPatientDocument")]
        [Authorize]
        public IActionResult UploadPatientDocument([FromForm] UploadPatientDocumentRequest request)
        {
            _log.Info($"UploadPatientDocument called. PatientId={request.PatientId}, DocumentId={request.DocumentId}");

            if (!ModelState.IsValid)
            {
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
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "PatientId must be greater than 0",
                    errors = new { request.PatientId }
                });
            }

            if (request.DocumentId <= 0)
            {
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "DocumentId must be greater than 0",
                    errors = new { request.DocumentId }
                });
            }

            var globalValues = GlobalFunctions.GetGlobalValues(HttpContext);
            var serviceResult = _patientRepository.UploadPatientDocument(request, globalValues);

            if (serviceResult.Result)
                _log.Info($"Document uploaded successfully: {serviceResult.Message}");
            else
                _log.Warn($"Document upload failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getPatientDocumentMapping")]
        [Authorize]
        public IActionResult GetPatientDocumentMapping([FromQuery] int patientId)
        {
            _log.Info($"GetPatientDocumentMapping called. PatientId={patientId}");

            if (patientId < 0)
            {
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "PatientId must be greater than equal to 0",
                    errors = new { patientId }
                });
            }

            var serviceResult = _patientRepository.GetPatientDocumentMapping(patientId);

            if (serviceResult.Result)
                _log.Info($"Patient documents fetched successfully: {serviceResult.Message}");
            else
                _log.Warn($"Patient documents fetch failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }
        [HttpGet("getPatientMaster")]
        [Authorize]
        public IActionResult GetPatientMaster(
            [FromQuery] int? patientId = null,
            [FromQuery] string? uhid = null,
            [FromQuery] string? contactNumber = null,
            [FromQuery] int? branchId = null)
        {
            _log.Info($"GetPatientMaster called. PatientId={patientId?.ToString() ?? "All"}, Uhid={uhid ?? "All"}, ContactNumber={contactNumber ?? "All"}, BranchId={branchId?.ToString() ?? "All"}");

            // Validate patientId if provided
            if (patientId.HasValue && patientId.Value <= 0)
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

            // Validate branchId if provided
            if (branchId.HasValue && branchId.Value <= 0)
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

            var serviceResult = _patientRepository.GetPatientMaster(patientId, uhid, contactNumber, branchId);

            if (serviceResult.Result)
                _log.Info($"Patients fetched successfully from cache: {serviceResult.Message}");
            else
                _log.Warn($"No patients found: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("searchPatientMaster")]
        [Authorize]
        public IActionResult SearchPatientMaster(
    [FromQuery] int? patientId = null,
    [FromQuery] string? uhid = null,
    [FromQuery] string? firstName = null,
    [FromQuery] string? middleName = null,
    [FromQuery] string? lastName = null,
    [FromQuery] string? relativeName = null,
    [FromQuery] string? dob = null,
    [FromQuery] string? contactNumber = null,
    [FromQuery] string? emergencyContactNumber = null,
    [FromQuery] string? address = null,
    [FromQuery] string? registrationDate = null,
    [FromQuery] string? ipdNo = null,
    [FromQuery] int? branchId = null)
        {
            _log.Info($"SearchPatientMaster called.");

            if (patientId.HasValue && patientId.Value <= 0)
            {
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new { result = false, messageType = alert.Type, message = "PatientId must be greater than 0", errors = new { patientId } });
            }

            if (branchId.HasValue && branchId.Value <= 0)
            {
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new { result = false, messageType = alert.Type, message = "BranchId must be greater than 0", errors = new { branchId } });
            }

            var serviceResult = _patientRepository.SearchPatientMaster(
                patientId, uhid, firstName, middleName, lastName,
                relativeName, dob, contactNumber, emergencyContactNumber,
                address, registrationDate, ipdNo, branchId);

            if (serviceResult.Result)
                _log.Info($"Patients fetched successfully: {serviceResult.Message}");
            else
                _log.Warn($"No patients found: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getServiceAllDetailsForOPDBilling")]
        [Authorize]
        public IActionResult GetServiceAllDetailsForOPDBilling(
           [FromQuery] int branchId,
           [FromQuery] int corporateId,
           [FromQuery] int doctorId,
           [FromQuery] int serviceItemId,
           [FromQuery] int categoryId,
           [FromQuery] int subCategoryId,
           [FromQuery] int subSubCategoryId,
           [FromQuery] int bedTypeId = 0)
        {
            _log.Info($"GetServiceAllDetailsForOPDBilling called. CorporateId={corporateId}, DoctorId={doctorId}, ServiceItemId={serviceItemId}, CategoryId={categoryId}, SubCategoryId={subCategoryId}, SubSubCategoryId={subSubCategoryId}, BedTypeId={bedTypeId}");

            // Validate all required params must be > 0
            var validationErrors = new Dictionary<string, int>();

            if (branchId <= 0) validationErrors["branchId"] = branchId;
            if (corporateId <= 0) validationErrors["corporateId"] = corporateId;
            if (doctorId <= 0) validationErrors["doctorId"] = doctorId;
            if (serviceItemId <= 0) validationErrors["serviceItemId"] = serviceItemId;
            if (categoryId <= 0) validationErrors["categoryId"] = categoryId;
            if (subCategoryId <= 0) validationErrors["subCategoryId"] = subCategoryId;
            if (subSubCategoryId <= 0) validationErrors["subSubCategoryId"] = subSubCategoryId;

            if (validationErrors.Any())
            {
                _log.Warn($"Invalid parameters: {string.Join(", ", validationErrors.Keys)}");
                var alertVal = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alertVal.Type,
                    message = $"The following parameters must be greater than 0: {string.Join(", ", validationErrors.Keys)}",
                    errors = validationErrors
                });
            }

            var serviceResult = _patientRepository.GetServiceAllDetailsForOPDBilling(
                branchId,corporateId, doctorId, serviceItemId, categoryId, subCategoryId, subSubCategoryId, bedTypeId);

            if (serviceResult.Result)
                _log.Info($"Service billing details fetched successfully: {serviceResult.Message}");
            else
                _log.Warn($"Service billing details fetch failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpPost("saveOPDBilling")]
        [Authorize]
        public IActionResult SaveOPDBilling([FromBody] SaveOPDBillingRequest request)
        {
            _log.Info($"SaveOPDBilling called. PatientId={request?.VisitDetails?.PatientId}, BranchId={request?.VisitDetails?.BranchId}");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for SaveOPDBilling.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("MODEL_VALIDATION_FAILED");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = alert.Message,
                    errors = ModelState
                });
            }

            // Additional validation
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

            var globalValues = GlobalFunctions.GetGlobalValues(HttpContext);
            var serviceResult = _patientRepository.SaveOPDBilling(request, globalValues);

            if (serviceResult.Result)
                _log.Info($"SaveOPDBilling succeeded: {serviceResult.Message}");
            else
                _log.Warn($"SaveOPDBilling failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getPackageAllDetails")]
        [Authorize]
        public IActionResult GetPackageAllDetails([FromQuery] int packageId)
        {
            _log.Info($"GetPackageAllDetails called. PackageId={packageId}");

            if (packageId <= 0)
            {
                _log.Warn("Invalid PackageId provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "PackageId must be greater than 0",
                    errors = new { packageId }
                });
            }

            var serviceResult = _patientRepository.GetPackageAllDetails(packageId);

            if (serviceResult.Result)
                _log.Info($"Package details fetched successfully: {serviceResult.Message}");
            else
                _log.Warn($"Package details fetch failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getReceiptDetailsByFTID")]
        [Authorize]
        public IActionResult GetReceiptDetailsByFTID(
    [FromQuery] int ftid,
    [FromQuery] int isReceipt=0,
    [FromQuery] int receiptId = 0)
        {
            _log.Info($"GetReceiptDetailsByFTID called. FTID={ftid}, IsReceipt={isReceipt}, ReceiptId={receiptId}");

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

            if (isReceipt != 0 && isReceipt != 1)
            {
                _log.Warn($"Invalid isReceipt value: {isReceipt}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "isReceipt must be 0 or 1",
                    errors = new { isReceipt }
                });
            }

            if (receiptId < 0)
            {
                _log.Warn("Invalid ReceiptId provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "ReceiptId must be greater than or equal to 0",
                    errors = new { receiptId }
                });
            }

            var globalValues = GlobalFunctions.GetGlobalValues(HttpContext);
            var serviceResult = _patientRepository.GetReceiptDetailsByFTID(ftid, isReceipt, receiptId, globalValues);

            if (serviceResult.Result)
                _log.Info($"Receipt details fetched successfully: {serviceResult.Message}");
            else
                _log.Warn($"Receipt details fetch failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getOPDReceiptList")]
        [Authorize]
        public IActionResult GetOPDReceiptList([FromQuery] string visitNo)
        {
            _log.Info($"GetOPDReceiptList called. VisitNo={visitNo}");

     

            if (string.IsNullOrWhiteSpace(visitNo))
            {
                _log.Warn("Invalid visitNo provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "visitNo is required",
                    errors = new { visitNo }
                });
            }

            var serviceResult = _patientRepository.GetOPDReceiptList(visitNo);

            if (serviceResult.Result)
                _log.Info($"OPD receipt list fetched successfully: {serviceResult.Message}");
            else
                _log.Warn($"OPD receipt list fetch failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getOPDCardDetails")]
        [Authorize]
        public IActionResult GetOPDCardDetails([FromQuery] long ftid)
        {
            _log.Info($"GetOPDCardDetails called. FTID={ftid}");

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

            var serviceResult = _patientRepository.GetOPDCardDetails(ftid);

            if (serviceResult.Result)
                _log.Info($"OPD card details fetched successfully: {serviceResult.Message}");
            else
                _log.Warn($"OPD card details fetch failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("findDuplicateService")]
        [Authorize]
        public IActionResult FindDuplicateService(
    [FromQuery] int serviceItemId,
    [FromQuery] int patientId)
        {
            _log.Info($"FindDuplicateService called. ServiceItemId={serviceItemId}, PatientId={patientId}");

            if (serviceItemId <= 0)
            {
                _log.Warn("Invalid ServiceItemId provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "ServiceItemId must be greater than 0",
                    errors = new { serviceItemId }
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

            var serviceResult = _patientRepository.FindDuplicateService(serviceItemId, patientId);

            // Convert DataTable rows to list of dictionaries for raw JSON output
            object rawData = null;
            if (serviceResult.Result && serviceResult.Data != null)
            {
                rawData = serviceResult.Data.Rows
                    .Cast<DataRow>()
                    .Select(row => serviceResult.Data.Columns
                        .Cast<DataColumn>()
                        .ToDictionary(col => col.ColumnName, col => row[col] == DBNull.Value ? null : row[col])
                    ).ToList();
            }

            if (serviceResult.Result)
                _log.Info($"Duplicate service check completed: {serviceResult.Message}");
            else
                _log.Warn($"Duplicate service check result: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = rawData
            });
        }


        [HttpGet("getInvestigationObservationMappingDetails")]
        [Authorize]
        public IActionResult GetInvestigationObservationMappingDetails(
    [FromQuery] int investigationId,
    [FromQuery] int ageInDays,
    [FromQuery] string gender)
        {
            _log.Info($"GetInvestigationObservationMappingDetails called. InvestigationId={investigationId}, AgeInDays={ageInDays}, Gender={gender}");

            if (investigationId <= 0)
            {
                _log.Warn("Invalid InvestigationId provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "InvestigationId must be greater than 0",
                    errors = new { investigationId }
                });
            }

            if (ageInDays < 0)
            {
                _log.Warn("Invalid AgeInDays provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "AgeInDays must be greater than or equal to 0",
                    errors = new { ageInDays }
                });
            }

            if (string.IsNullOrWhiteSpace(gender))
            {
                _log.Warn("Invalid Gender provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "Gender is required",
                    errors = new { gender }
                });
            }

            if (gender.ToUpper() != "M" && gender.ToUpper() != "F" && gender.ToUpper() != "B")
            {
                _log.Warn($"Invalid Gender value provided: {gender}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "Gender must be M, F or B",
                    errors = new { gender }
                });
            }

            var serviceResult = _patientRepository.GetInvestigationObservationMappingDetails(investigationId, ageInDays, gender);

            if (serviceResult.Result)
                _log.Info($"GetInvestigationObservationMappingDetails fetched successfully: {serviceResult.Message}");
            else
                _log.Warn($"GetInvestigationObservationMappingDetails failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getUserDiscountRights")]
        [Authorize]
        public IActionResult GetUserDiscountRights([FromQuery] int userId)
        {
            _log.Info($"GetUserDiscountRights called. UserId={userId}");

            if (userId <= 0)
            {
                _log.Warn("Invalid UserId provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "UserId must be greater than 0",
                    errors = new { userId }
                });
            }

            var serviceResult = _patientRepository.GetUserDiscountRights(userId);

            if (serviceResult.Result)
                _log.Info($"GetUserDiscountRights fetched successfully: {serviceResult.Message}");
            else
                _log.Warn($"GetUserDiscountRights failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getPatientPreviousDues")]
        [Authorize]
        public IActionResult GetPatientPreviousDues(
    [FromQuery] int branchId,
    [FromQuery] int patientId)
        {
            _log.Info($"GetPatientPreviousDues called. BranchId={branchId}, PatientId={patientId}");

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

            var serviceResult = _patientRepository.GetPatientPreviousDues(branchId, patientId);

            if (serviceResult.Result)
                _log.Info($"GetPatientPreviousDues fetched successfully: {serviceResult.Message}");
            else
                _log.Warn($"GetPatientPreviousDues failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getPatientLastConsultationDetail")]
        [Authorize]
        public IActionResult GetPatientLastConsultationDetail([FromQuery] int patientId)
        {
            _log.Info($"GetPatientLastConsultationDetail called. PatientId={patientId}");

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

            var serviceResult = _patientRepository.GetPatientLastConsultationDetail(patientId);

            if (serviceResult.Result)
                _log.Info($"GetPatientLastConsultationDetail fetched successfully: {serviceResult.Message}");
            else
                _log.Warn($"GetPatientLastConsultationDetail failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getServiceItemDetailsByVisitId")]
        [Authorize]
        public IActionResult GetServiceItemDetailsByVisitId([FromQuery] int visitId)
        {
            _log.Info($"GetServiceItemDetailsByVisitId called. VisitId={visitId}");

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

            var serviceResult = _patientRepository.GetServiceItemDetailsByVisitId(visitId);

            if (serviceResult.Result)
                _log.Info($"GetServiceItemDetailsByVisitId fetched successfully: {serviceResult.Message}");
            else
                _log.Warn($"GetServiceItemDetailsByVisitId failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getPatientBalanceAmountOPD")]
        [Authorize]
        public IActionResult GetPatientBalanceAmountOPD([FromQuery] string uhid)
        {
            _log.Info($"GetPatientBalanceAmountOPD called. UHID={uhid}");

            if (string.IsNullOrWhiteSpace(uhid))
            {
                _log.Warn("Invalid UHID provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "UHID is required",
                    errors = new { uhid }
                });
            }

            var serviceResult = _patientRepository.GetPatientBalanceAmountOPD(uhid);

            if (serviceResult.Result)
                _log.Info($"GetPatientBalanceAmountOPD fetched successfully: {serviceResult.Message}");
            else
                _log.Warn($"GetPatientBalanceAmountOPD failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getPatientBalanceAmountIPD")]
        [Authorize]
        public IActionResult GetPatientBalanceAmountIPD([FromQuery] string uhid)
        {
            _log.Info($"GetPatientBalanceAmountIPD called. UHID={uhid}");

            if (string.IsNullOrWhiteSpace(uhid))
            {
                _log.Warn("Invalid UHID provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "UHID is required",
                    errors = new { uhid }
                });
            }

            var serviceResult = _patientRepository.GetPatientBalanceAmountIPD(uhid);

            if (serviceResult.Result)
                _log.Info($"GetPatientBalanceAmountIPD fetched successfully: {serviceResult.Message}");
            else
                _log.Warn($"GetPatientBalanceAmountIPD failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getPatientBalanceAmountPharmacy")]
        [Authorize]
        public IActionResult GetPatientBalanceAmountPharmacy([FromQuery] string uhid)
        {
            _log.Info($"GetPatientBalanceAmountPharmacy called. UHID={uhid}");

            if (string.IsNullOrWhiteSpace(uhid))
            {
                _log.Warn("Invalid UHID provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "UHID is required",
                    errors = new { uhid }
                });
            }

            var serviceResult = _patientRepository.GetPatientBalanceAmountPharmacy(uhid);

            if (serviceResult.Result)
                _log.Info($"GetPatientBalanceAmountPharmacy fetched successfully: {serviceResult.Message}");
            else
                _log.Warn($"GetPatientBalanceAmountPharmacy failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("searchPatientForConsultation")]
        [Authorize]
        public IActionResult SearchPatientForConsultation(
           [FromQuery] int branchId,
           [FromQuery] int typeId,
           [FromQuery] string fromDate,
           [FromQuery] string toDate,
           [FromQuery] string uhid = null,
           [FromQuery] int appNo = 0,
           [FromQuery] int doctorId = 0,
           [FromQuery] int doctorDepartmentId = 0,
           [FromQuery] int dateTypeId = 0,
           [FromQuery] int statusId = 0,
           [FromQuery] int bedTypeId = 0,
           [FromQuery] int isTempratureRoomOut = 0)
        {
            _log.Info($"SearchPatientForConsultation called. BranchId={branchId}, TypeId={typeId}, " +
                      $"FromDate={fromDate}, ToDate={toDate}");

            // Manual validation
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

            if (typeId != 1 && typeId != 2)
            {
                _log.Warn($"Invalid TypeId: {typeId}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "TypeId must be 1 (OPD) or 2 (IPD)",
                    errors = new { typeId }
                });
            }

            if (string.IsNullOrWhiteSpace(fromDate))
            {
                _log.Warn("FromDate is missing.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "FromDate is required",
                    errors = new { fromDate }
                });
            }

            if (string.IsNullOrWhiteSpace(toDate))
            {
                _log.Warn("ToDate is missing.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "ToDate is required",
                    errors = new { toDate }
                });
            }

            var request = new SearchPatientForConsultationRequest
            {
                BranchId = branchId,
                TypeId = typeId,
                FromDate = fromDate,
                ToDate = toDate,
                Uhid = uhid,
                AppNo = appNo,
                DoctorId = doctorId,
                DoctorDepartmentId = doctorDepartmentId,
                DateTypeId = dateTypeId,
                StatusId = statusId,
                BedTypeId = bedTypeId,
                isTempratureRoomOut = isTempratureRoomOut
            };

            var serviceResult = _patientRepository.SearchPatientForConsultation(request);

            if (serviceResult.Result)
                _log.Info($"SearchPatientForConsultation completed: {serviceResult.Message}");
            else
                _log.Warn($"SearchPatientForConsultation failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }



        [HttpGet("getPatientObservationResultsTrend")]
        [Authorize]
        public IActionResult GetPatientObservationResultsTrend(
    [FromQuery] int patientId,
    [FromQuery] int pageNumber = 1,
    [FromQuery] int pageSize = 10)
        {
            _log.Info($"GetPatientObservationResultsTrend called. PatientId={patientId}, PageNumber={pageNumber}, PageSize={pageSize}");

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

            if (pageNumber <= 0)
            {
                _log.Warn("Invalid PageNumber provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "PageNumber must be greater than 0",
                    errors = new { pageNumber }
                });
            }

            if (pageSize <= 0)
            {
                _log.Warn("Invalid PageSize provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "PageSize must be greater than 0",
                    errors = new { pageSize }
                });
            }

            var serviceResult = _patientRepository.GetPatientObservationResultsTrend(patientId, pageNumber, pageSize);

            if (serviceResult.Result)
                _log.Info($"Observation trend fetched successfully: {serviceResult.Message}");
            else
                _log.Warn($"Observation trend fetch failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpPost("saveIPDAdmission")]
        [Authorize]
        public IActionResult SaveIPDAdmission([FromBody] SaveIPDAdmissionRequest request)
        {
            _log.Info($"SaveIPDAdmission called. PatientId={request?.PatientId}, BranchId={request?.BranchId}");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for SaveIPDAdmission.");
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
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new { result = false, messageType = alert.Type, message = "PatientId must be greater than 0" });
            }

            if (request.BranchId <= 0)
            {
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new { result = false, messageType = alert.Type, message = "BranchId must be greater than 0" });
            }

            if (request.PrimaryDoctorId <= 0)
            {
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new { result = false, messageType = alert.Type, message = "PrimaryDoctorId must be greater than 0" });
            }

            if (request.BedId <= 0)
            {
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new { result = false, messageType = alert.Type, message = "BedId must be greater than 0" });
            }

            var globalValues = GlobalFunctions.GetGlobalValues(HttpContext);
            var serviceResult = _patientRepository.SaveIPDAdmission(request, globalValues);

            if (serviceResult.Result)
                _log.Info($"SaveIPDAdmission succeeded: {serviceResult.Message}");
            else
                _log.Warn($"SaveIPDAdmission failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("searchIPDPatient")]
        [Authorize]
        public IActionResult SearchIPDPatient(
    [FromQuery] int branchId,
    [FromQuery] string searchBy = null,
    [FromQuery] string searchValue = null,
    [FromQuery] int statusId = 0)
        {
            _log.Info($"SearchIPDPatient called. BranchId={branchId}, SearchBy={searchBy}, StatusId={statusId}");

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

            if (statusId < 0 || statusId > 10)
            {
                _log.Warn($"Invalid StatusId: {statusId}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "StatusId must be 0(All), 1(Admitted), 2(Discharged), 3(Bill Generated Pending), 4(File Closed Pending), 5(Today Admitted), 6(Today Discharged), 7(Zero Advance), 8(Cash), 9(Corporate), 10(Discharge Summary Ready)",
                    errors = new { statusId }
                });
            }

            if (!string.IsNullOrWhiteSpace(searchBy) && string.IsNullOrWhiteSpace(searchValue))
            {
                _log.Warn("SearchValue is required when SearchBy is provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "SearchValue is required when SearchBy is provided",
                    errors = new { searchValue }
                });
            }

            var request = new SearchIPDPatientRequest
            {
                BranchId = branchId,
                SearchBy = searchBy ?? string.Empty,
                SearchValue = searchValue ?? string.Empty,
                StatusId = statusId
            };

            var globalValues = GlobalFunctions.GetGlobalValues(HttpContext);
            var serviceResult = _patientRepository.SearchIPDPatient(request, globalValues);

            if (serviceResult.Result)
                _log.Info($"SearchIPDPatient completed: {serviceResult.Message}");
            else
                _log.Warn($"SearchIPDPatient failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }


        [HttpPost("uploadVisitWisePatientDocument")]
        [Authorize]
        public IActionResult UploadVisitWisePatientDocument([FromForm] UploadVisitWisePatientDocumentRequest request)
        {
            _log.Info($"UploadVisitWisePatientDocument called. PatientId={request.PatientId}, VisitId={request.VisitId}, DocumentId={request.DocumentId}, DocumentCategoryId={request.DocumentCategoryId}");

            if (!ModelState.IsValid)
            {
                var alert = _messageService.GetMessageAndTypeByAlertCode("MODEL_VALIDATION_FAILED");
                return BadRequest(new { result = false, messageType = alert.Type, message = alert.Message, errors = ModelState });
            }

            if (request.PatientId <= 0)
            {
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new { result = false, messageType = alert.Type, message = "PatientId must be greater than 0", errors = new { request.PatientId } });
            }

            if (request.VisitId <= 0)
            {
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new { result = false, messageType = alert.Type, message = "VisitId must be greater than 0", errors = new { request.VisitId } });
            }

            if (request.DocumentId <= 0)
            {
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new { result = false, messageType = alert.Type, message = "DocumentId must be greater than 0", errors = new { request.DocumentId } });
            }

            if (request.DocumentCategoryId <= 0)
            {
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new { result = false, messageType = alert.Type, message = "DocumentCategoryId must be greater than 0", errors = new { request.DocumentCategoryId } });
            }

            var globalValues = GlobalFunctions.GetGlobalValues(HttpContext);
            var serviceResult = _patientRepository.UploadVisitWisePatientDocument(request, globalValues);

            if (serviceResult.Result)
                _log.Info($"Visit-wise document uploaded successfully: {serviceResult.Message}");
            else
                _log.Warn($"Visit-wise document upload failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getVisitWisePatientDocumentMapping")]
        [Authorize]
        public IActionResult GetVisitWisePatientDocumentMapping(
            [FromQuery] int documentCategoryId,
            [FromQuery] int visitId = 0,
            [FromQuery] int patientId = 0)
        {
            _log.Info($"GetVisitWisePatientDocumentMapping called. DocumentCategoryId={documentCategoryId}, VisitId={visitId}, PatientId={patientId}");

            if (documentCategoryId <= 0)
            {
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new { result = false, messageType = alert.Type, message = "DocumentCategoryId must be greater than 0", errors = new { documentCategoryId } });
            }

            var serviceResult = _patientRepository.GetVisitWisePatientDocumentMapping(documentCategoryId, visitId, patientId);

            if (serviceResult.Result)
                _log.Info($"Visit-wise patient documents fetched successfully: {serviceResult.Message}");
            else
                _log.Warn($"Visit-wise patient documents fetch failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpPost("saveOPDBooking")]
        [Authorize]
        public IActionResult SaveOPDBooking([FromBody] SaveOPDBookingRequest request)
        {
            _log.Info($"SaveOPDBooking called. PatientId={request?.VisitDetails?.PatientId}, BranchId={request?.VisitDetails?.BranchId}");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for SaveOPDBooking.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("MODEL_VALIDATION_FAILED");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = alert.Message,
                    errors = ModelState
                });
            }

            // Additional validation
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

            var globalValues = GlobalFunctions.GetGlobalValues(HttpContext);
            var serviceResult = _patientRepository.SaveOPDBooking(request, globalValues);

            if (serviceResult.Result)
                _log.Info($"SaveOPDBooking succeeded: {serviceResult.Message}");
            else
                _log.Warn($"SaveOPDBooking failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }


        [HttpGet("getOPDBookingDetailsForPaymentCollection")]
        [Authorize]
        public IActionResult GetOPDBookingDetailsForPaymentCollection(
    [FromQuery] string fromDate,
    [FromQuery] string toDate,
    [FromQuery] int branchId,
[FromQuery] int corporateId=0)
        {
            _log.Info($"GetOPDBookingDetailsForPaymentCollection called. FromDate={fromDate}, ToDate={toDate}");

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
                _log.Warn("Invalid BranchId provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "BranchId must be greater than 0",
                    errors = new { branchId = branchId }
                });
            }

            var serviceResult = _patientRepository.GetOPDBookingDetailsForPaymentCollection(branchId, corporateId, fromDate, toDate);

            if (serviceResult.Result)
                _log.Info($"OPD booking details for payment collection fetched successfully: {serviceResult.Message}");
            else
                _log.Warn($"OPD booking details for payment collection fetch failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getOPDBookingDetailsForDiscountApproval")]
        [Authorize]
        public IActionResult GetOPDBookingDetailsForDiscountApproval(
            [FromQuery] string fromDate,
            [FromQuery] string toDate,
            [FromQuery] int branchId,
            [FromQuery] int corporateId=0
            )
        {
            _log.Info($"GetOPDBookingDetailsForDiscountApproval called. FromDate={fromDate}, ToDate={toDate}");

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
                _log.Warn("Invalid BranchId provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "BranchId must be greater than 0",
                    errors = new { branchId = branchId }
                });
            }
            var globalValues = GlobalFunctions.GetGlobalValues(HttpContext);

            var serviceResult = _patientRepository.GetOPDBookingDetailsForDiscountApproval( branchId,  corporateId, fromDate, toDate, globalValues);

            if (serviceResult.Result)
                _log.Info($"OPD booking details for discount approval fetched successfully: {serviceResult.Message}");
            else
                _log.Warn($"OPD booking details for discount approval fetch failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getOPDBookingDetailsByBookingId")]
        [Authorize]
        public IActionResult GetOPDBookingDetailsByBookingId([FromQuery] int bookingId)
        {
            _log.Info($"GetOPDBookingDetailsByBookingId called. BookingId={bookingId}");

            if (bookingId <= 0)
            {
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new { result = false, messageType = alert.Type, message = "BookingId must be greater than 0" });
            }

            var serviceResult = _patientRepository.GetOPDBookingDetailsByBookingId(bookingId);

            if (serviceResult.Result)
                _log.Info($"OPD booking details by BookingId fetched successfully: {serviceResult.Message}");
            else
                _log.Warn($"OPD booking details by BookingId fetch failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpPatch("cancelOPDBooking")]
        [Authorize]
        public IActionResult CancelOPDBooking([FromBody] CancelOPDBookingRequest request)
        {
            _log.Info($"CancelOPDBooking called. BookingId={request.BookingId}");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for CancelOPDBooking.");
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
            var serviceResult = _patientRepository.CancelOPDBooking(request, globalValues);

            if (serviceResult.Result)
                _log.Info($"OPD booking cancelled successfully: {serviceResult.Message}");
            else
                _log.Warn($"OPD booking cancellation failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpPatch("paymentCollectedForOPDBooking")]
        [Authorize]
        public IActionResult PaymentCollectedForOPDBooking([FromQuery] int bookingId)
        {
            _log.Info($"PaymentCollectedForOPDBooking called. BookingId={bookingId}");

            if (bookingId <= 0)
            {
                _log.Warn("Invalid BookingId provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "BookingId must be greater than 0",
                    errors = new { bookingId }
                });
            }

            var globalValues = GlobalFunctions.GetGlobalValues(HttpContext);
            var serviceResult = _patientRepository.PaymentCollectedForOPDBooking(bookingId, globalValues);

            if (serviceResult.Result)
                _log.Info($"Payment collected marked successfully: {serviceResult.Message}");
            else
                _log.Warn($"Payment collected update failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpPatch("approveOPDBookingDiscount")]
        [Authorize]
        public IActionResult ApproveOPDBookingDiscount([FromBody] ApproveOPDBookingDiscountRequest request)
        {
            _log.Info($"ApproveOPDBookingDiscount called. BookingId={request.BookingId}, Flag={request.Flag}");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for ApproveOPDBookingDiscount.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("MODEL_VALIDATION_FAILED");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = alert.Message,
                    errors = ModelState
                });
            }

            if (request.BookingId <= 0)
            {
                _log.Warn("Invalid BookingId provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "BookingId must be greater than 0",
                    errors = new { request.BookingId }
                });
            }

            if (request.ApprovedPer < 0 || request.ApprovedPer > 100)
            {
                _log.Warn($"Invalid ApprovedPer value: {request.ApprovedPer}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "ApprovedPer must be between 0 and 100",
                    errors = new { request.ApprovedPer }
                });
            }

            var globalValues = GlobalFunctions.GetGlobalValues(HttpContext);
            var serviceResult = _patientRepository.ApproveOPDBookingDiscount(request, globalValues);

            if (serviceResult.Result)
                _log.Info($"OPD booking discount approval processed: {serviceResult.Message}");
            else
                _log.Warn($"OPD booking discount approval failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getOPDBookingApprovalDetails")]
        [Authorize]
        public IActionResult GetOPDBookingApprovalDetails([FromQuery] long bookingId)
        {
            _log.Info($"GetOPDBookingApprovalDetails called. BookingId={bookingId}");

            if (bookingId <= 0)
            {
                _log.Warn("Invalid BookingId provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "BookingId must be greater than 0",
                    errors = new { bookingId }
                });
            }

            var serviceResult = _patientRepository.GetOPDBookingApprovalDetails(bookingId);

            if (serviceResult.Result)
                _log.Info($"OPD booking approval details fetched successfully: {serviceResult.Message}");
            else
                _log.Warn($"OPD booking approval details fetch failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpPost("savePatientAdvance")]
        [Authorize]
        public IActionResult SavePatientAdvance([FromBody] SavePatientAdvanceRequest request)
        {
            _log.Info($"SavePatientAdvance called. PatientId={request?.PatientId}, PatientLedgerId={request?.PatientLedgerId}");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for SavePatientAdvance.");
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

            if (request.PatientLedgerId < 0)
            {
                _log.Warn("Invalid PatientLedgerId provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "PatientLedgerId must be greater than or equal to 0",
                    errors = new { patientLedgerId = request.PatientLedgerId }
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
            var serviceResult = _patientRepository.SavePatientAdvance(request, globalValues);

            if (serviceResult.Result)
                _log.Info($"SavePatientAdvance succeeded: {serviceResult.Message}");
            else
                _log.Warn($"SavePatientAdvance failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getPatientLedgerReceiptDetails")]
        [Authorize]
        public IActionResult GetPatientLedgerReceiptDetails(
    [FromQuery] int receiptId,
    [FromQuery] int patientId,
    [FromQuery] int ledgerId)
        {
            _log.Info($"GetPatientLedgerReceiptDetails called. ReceiptId={receiptId}, PatientId={patientId}, LedgerId={ledgerId}");

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

            if (ledgerId <= 0)
            {
                _log.Warn("Invalid LedgerId provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "LedgerId must be greater than 0",
                    errors = new { ledgerId }
                });
            }

            var serviceResult = _patientRepository.GetPatientLedgerReceiptDetails(receiptId, patientId, ledgerId);

            if (serviceResult.Result)
                _log.Info($"PatientLedgerReceiptDetails fetched successfully: {serviceResult.Message}");
            else
                _log.Warn($"PatientLedgerReceiptDetails fetch failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getPatientAdvanceReceiptList")]
        [Authorize]
        public IActionResult GetPatientAdvanceReceiptList([FromQuery] int patientId, [FromQuery] int receiptId)
        {
            _log.Info($"GetPatientAdvanceReceiptList called. PatientId={patientId}");

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

            if (receiptId <= 0)
            {
                _log.Warn("Invalid receiptId provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "receiptId must be greater than 0",
                    errors = new { receiptId }
                });
            }

            var serviceResult = _patientRepository.GetPatientAdvanceReceiptList(patientId, receiptId);

            if (serviceResult.Result)
                _log.Info($"PatientAdvanceReceiptList fetched successfully: {serviceResult.Message}");
            else
                _log.Warn($"PatientAdvanceReceiptList fetch failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getBillToRefund")]
        [Authorize]
        public IActionResult GetBillToRefund(
      [FromQuery] string receiptNo = null,
      [FromQuery] string billNo = null,
      [FromQuery] string uhid = null,
      [FromQuery] string patientName = null)
        {
            _log.Info($"GetBillToRefund called. ReceiptNo={receiptNo}, BillNo={billNo}, UHID={uhid}, PatientName={patientName}");

            if (string.IsNullOrWhiteSpace(receiptNo) && string.IsNullOrWhiteSpace(billNo)
                && string.IsNullOrWhiteSpace(uhid) && string.IsNullOrWhiteSpace(patientName))
            {
                _log.Warn("No filters provided for GetBillToRefund.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "At least one of receiptNo, billNo, uhid, or patientName is required",
                    errors = new { receiptNo, billNo, uhid, patientName }
                });
            }

            var serviceResult = _patientRepository.GetBillToRefund(receiptNo, billNo, uhid, patientName);

            if (serviceResult.Result)
                _log.Info($"GetBillToRefund fetched successfully: {serviceResult.Message}");
            else
                _log.Warn($"GetBillToRefund failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getBillDetailsToRefund")]
        [Authorize]
        public IActionResult GetBillDetailsToRefund([FromQuery] int visitId)
        {
            _log.Info($"GetBillDetailsToRefund called. VisitId={visitId}");

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

            var serviceResult = _patientRepository.GetBillDetailsToRefund(visitId);

            if (serviceResult.Result)
                _log.Info($"GetBillDetailsToRefund fetched successfully: {serviceResult.Message}");
            else
                _log.Warn($"GetBillDetailsToRefund failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getOPDPackageServicesForRefund")]
        [Authorize]
        public IActionResult GetOPDPackageServicesForRefund(
            [FromQuery] int visitId,
            [FromQuery] int packageId)
        {
            _log.Info($"GetOPDPackageServicesForRefund called. VisitId={visitId}, PackageId={packageId}");

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

            if (packageId <= 0)
            {
                _log.Warn("Invalid PackageId provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "PackageId must be greater than 0",
                    errors = new { packageId }
                });
            }

            var serviceResult = _patientRepository.GetOPDPackageServicesForRefund(visitId, packageId);

            if (serviceResult.Result)
                _log.Info($"GetOPDPackageServicesForRefund fetched successfully: {serviceResult.Message}");
            else
                _log.Warn($"GetOPDPackageServicesForRefund failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpPost("saveOPDRefundBilling")]
        [Authorize]
        public IActionResult SaveOPDRefundBilling([FromBody] SaveOPDRefundBillingRequest request)
        {
            _log.Info($"SaveOPDRefundBilling called. PatientId={request?.VisitDetails?.PatientId}, BranchId={request?.VisitDetails?.BranchId}");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for SaveOPDRefundBilling.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("MODEL_VALIDATION_FAILED");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = alert.Message,
                    errors = ModelState
                });
            }

            if (request.RefundItems == null || request.RefundItems.Count == 0)
            {
                _log.Warn("No refund items provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "At least one refund item is required",
                    errors = new[] { "RefundItems cannot be empty" }
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

            var globalValues = GlobalFunctions.GetGlobalValues(HttpContext);
            var serviceResult = _patientRepository.SaveOPDRefundBilling(request, globalValues);

            if (serviceResult.Result)
                _log.Info($"SaveOPDRefundBilling succeeded: {serviceResult.Message}");
            else
                _log.Warn($"SaveOPDRefundBilling failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpPost("saveOPDRefundRequestApproval")]
        [Authorize]
        public IActionResult SaveOPDRefundRequestApproval([FromBody] SaveOPDRefundRequestApprovalRequest request)
        {
            _log.Info($"SaveOPDRefundRequestApproval called. PatientId={request?.VisitDetails?.PatientId}, BranchId={request?.VisitDetails?.BranchId}");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for SaveOPDRefundRequestApproval.");
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
                _log.Warn("No refund items provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "At least one refund item is required",
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
            var serviceResult = _patientRepository.SaveOPDRefundRequestApproval(request, globalValues);

            if (serviceResult.Result)
                _log.Info($"SaveOPDRefundRequestApproval succeeded: {serviceResult.Message}");
            else
                _log.Warn($"SaveOPDRefundRequestApproval failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpPatch("approveOPDRefundRequest")]
        [Authorize]
        public IActionResult ApproveOPDRefundRequest([FromBody] ApproveOPDRefundRequestRequest request)
        {
            _log.Info($"ApproveOPDRefundRequest called. RefundId={request?.RefundId}, Flag={request?.Flag}");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for ApproveOPDRefundRequest.");
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
            var serviceResult = _patientRepository.ApproveOPDRefundRequest(request, globalValues);

            if (serviceResult.Result)
                _log.Info($"ApproveOPDRefundRequest succeeded: {serviceResult.Message}");
            else
                _log.Warn($"ApproveOPDRefundRequest failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpPatch("cancelOPDRefundRequest")]
        [Authorize]
        public IActionResult CancelOPDRefundRequest([FromBody] CancelOPDRefundRequestRequest request)
        {
            _log.Info($"CancelOPDRefundRequest called. RefundId={request?.RefundId}");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for CancelOPDRefundRequest.");
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
            var serviceResult = _patientRepository.CancelOPDRefundRequest(request, globalValues);

            if (serviceResult.Result)
                _log.Info($"CancelOPDRefundRequest succeeded: {serviceResult.Message}");
            else
                _log.Warn($"CancelOPDRefundRequest failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpPatch("paymentOPDRefundRequest")]
        [Authorize]
        public IActionResult paymentOPDRefundRequest([FromBody] paymentOPDRefundRequestRequest request)
        {
            _log.Info($"paymentOPDRefundRequest called. RefundId={request?.RefundId}");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for paymentOPDRefundRequest.");
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
            var serviceResult = _patientRepository.paymentOPDRefundRequest(request, globalValues);

            if (serviceResult.Result)
                _log.Info($"paymentOPDRefundRequest succeeded: {serviceResult.Message}");
            else
                _log.Warn($"paymentOPDRefundRequest failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getOPDRefundRequestListForApproval")]
        [Authorize]
        public IActionResult GetOPDRefundRequestListForApproval(
            [FromQuery] string fromDate,
            [FromQuery] string toDate,
            [FromQuery] int branchId)
        {
            _log.Info($"GetOPDRefundRequestListForApproval called. FromDate={fromDate}, ToDate={toDate}, BranchId={branchId}");

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
            var serviceResult = _patientRepository.GetOPDRefundRequestListForApproval(fromDate, toDate, branchId, globalValues);

            if (serviceResult.Result)
                _log.Info($"GetOPDRefundRequestListForApproval fetched successfully: {serviceResult.Message}");
            else
                _log.Warn($"GetOPDRefundRequestListForApproval failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getOPDRefundRequestDetailsByRefundId")]
        [Authorize]
        public IActionResult GetOPDRefundRequestDetailsByRefundId([FromQuery] int refundId)
        {
            _log.Info($"GetOPDRefundRequestDetailsByRefundId called. RefundId={refundId}");

            if (refundId <= 0)
            {
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "RefundId must be greater than 0",
                    errors = new { refundId }
                });
            }

            var serviceResult = _patientRepository.GetOPDRefundRequestDetailsByRefundId(refundId);

            if (serviceResult.Result)
                _log.Info($"GetOPDRefundRequestDetailsByRefundId fetched successfully: {serviceResult.Message}");
            else
                _log.Warn($"GetOPDRefundRequestDetailsByRefundId failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getOPDRefundRequestApprovalDetails")]
        [Authorize]
        public IActionResult GetOPDRefundRequestApprovalDetails([FromQuery] int refundId)
        {
            _log.Info($"GetOPDRefundRequestApprovalDetails called. RefundId={refundId}");

            if (refundId <= 0)
            {
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "RefundId must be greater than 0",
                    errors = new { refundId }
                });
            }

            var serviceResult = _patientRepository.GetOPDRefundRequestApprovalDetails(refundId);

            if (serviceResult.Result)
                _log.Info($"GetOPDRefundRequestApprovalDetails fetched successfully: {serviceResult.Message}");
            else
                _log.Warn($"GetOPDRefundRequestApprovalDetails failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }
        [HttpGet("getBillReceiptReprintDetails")]
        [Authorize]
        public IActionResult GetBillReceiptReprintDetails(
    [FromQuery] string branchId,
    [FromQuery] string fromDate,
    [FromQuery] string toDate,
    [FromQuery] string uhid = null,
    [FromQuery] string name = null,
    [FromQuery] int type = 0,
    [FromQuery] string billNo = null,
    [FromQuery] string receiptNo = null)
        {
            _log.Info($"GetBillReceiptReprintDetails called. BranchId={branchId}, Type={type}, FromDate={fromDate}, ToDate={toDate}");

            if (string.IsNullOrWhiteSpace(branchId))
            {
                _log.Warn("BranchId is missing.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "BranchId is required",
                    errors = new { branchId }
                });
            }

            if (type < 0 || type > 2)
            {
                _log.Warn($"Invalid Type: {type}");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "Type must be 0 (All), 1 (OPD), or 2 (IPD)",
                    errors = new { type }
                });
            }

            if (string.IsNullOrWhiteSpace(fromDate))
            {
                _log.Warn("FromDate is missing.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "FromDate is required",
                    errors = new { fromDate }
                });
            }

            if (string.IsNullOrWhiteSpace(toDate))
            {
                _log.Warn("ToDate is missing.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "ToDate is required",
                    errors = new { toDate }
                });
            }

            var serviceResult = _patientRepository.GetBillReceiptReprintDetails(
                branchId, uhid, name, type, billNo, receiptNo, fromDate, toDate);

            if (serviceResult.Result)
                _log.Info($"GetBillReceiptReprintDetails fetched successfully: {serviceResult.Message}");
            else
                _log.Warn($"GetBillReceiptReprintDetails failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }
        [HttpGet("getBillForCreditNote")]
        [Authorize]
        public IActionResult GetBillForCreditNote(
    [FromQuery] string fromDate = null,
    [FromQuery] string toDate = null,
    [FromQuery] string billNo = null,
    [FromQuery] string uhid = null,
    [FromQuery] string patientName = null,
    [FromQuery] int typeId = 0)
        {
            _log.Info($"GetBillForCreditNote called. FromDate={fromDate}, ToDate={toDate}, BillNo={billNo}, Uhid={uhid}, PatientName={patientName}, TypeId={typeId}");

            // SP logic: when both uhid and billNo are empty, fromDate/toDate are used as the WHERE filter,
            // so they are required in that case.
            if (string.IsNullOrWhiteSpace(uhid) && string.IsNullOrWhiteSpace(billNo))
            {
                if (string.IsNullOrWhiteSpace(fromDate))
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                    return BadRequest(new
                    {
                        result = false,
                        messageType = alert.Type,
                        message = "FromDate is required when Uhid and BillNo are not provided"
                    });
                }

                if (string.IsNullOrWhiteSpace(toDate))
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                    return BadRequest(new
                    {
                        result = false,
                        messageType = alert.Type,
                        message = "ToDate is required when Uhid and BillNo are not provided"
                    });
                }
            }

            if (typeId < 0)
            {
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "TypeId must be greater than or equal to 0",
                    errors = new { typeId }
                });
            }

            var serviceResult = _patientRepository.GetBillForCreditNote(fromDate, toDate, billNo, uhid, patientName, typeId);

            if (serviceResult.Result)
                _log.Info($"GetBillForCreditNote fetched successfully: {serviceResult.Message}");
            else
                _log.Warn($"GetBillForCreditNote failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getBillDetailsForCreditNote")]
        [Authorize]
        public IActionResult GetBillDetailsForCreditNote([FromQuery] int visitId)
        {
            _log.Info($"GetBillDetailsForCreditNote called. VisitId={visitId}");

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

            var serviceResult = _patientRepository.GetBillDetailsForCreditNote(visitId);

            if (serviceResult.Result)
                _log.Info($"GetBillDetailsForCreditNote fetched successfully: {serviceResult.Message}");
            else
                _log.Warn($"GetBillDetailsForCreditNote failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }



        [HttpPost("saveCreditNote")]
        [Authorize]
        public IActionResult SaveCreditNote([FromBody] SaveCreditNoteRequest request)
        {
            _log.Info($"SaveCreditNote called. PatientId={request?.VisitDetails?.PatientId}, BranchId={request?.VisitDetails?.BranchId}, BillId={request?.VisitDetails?.BillId}");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for SaveCreditNote.");
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
                _log.Warn("No credit note items provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "At least one credit note item is required",
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

            if (request.VisitDetails.BillId <= 0)
            {
                _log.Warn("Invalid BillId provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "BillId must be greater than 0",
                    errors = new { billId = request.VisitDetails.BillId }
                });
            }

            var globalValues = GlobalFunctions.GetGlobalValues(HttpContext);
            var serviceResult = _patientRepository.SaveCreditNote(request, globalValues);

            if (serviceResult.Result)
                _log.Info($"SaveCreditNote succeeded: {serviceResult.Message}");
            else
                _log.Warn($"SaveCreditNote failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }


        [HttpPost("saveCreditNoteRequestApproval")]
        [Authorize]
        public IActionResult SaveCreditNoteRequestApproval([FromBody] SaveCreditNoteRequestApprovalRequest request)
        {
            _log.Info($"SaveCreditNoteRequestApproval called. PatientId={request?.VisitDetails?.PatientId}, BranchId={request?.VisitDetails?.BranchId}, BillId={request?.VisitDetails?.BillId}");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for SaveCreditNoteRequestApproval.");
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
                _log.Warn("No credit note items provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "At least one credit note item is required",
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

            if (request.VisitDetails.BillId <= 0)
            {
                _log.Warn("Invalid BillId provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "BillId must be greater than 0",
                    errors = new { billId = request.VisitDetails.BillId }
                });
            }

            var globalValues = GlobalFunctions.GetGlobalValues(HttpContext);
            var serviceResult = _patientRepository.SaveCreditNoteRequestApproval(request, globalValues);

            if (serviceResult.Result)
                _log.Info($"SaveCreditNoteRequestApproval succeeded: {serviceResult.Message}");
            else
                _log.Warn($"SaveCreditNoteRequestApproval failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpPatch("approveCreditNoteRequest")]
        [Authorize]
        public IActionResult ApproveCreditNoteRequest([FromBody] ApproveCreditNoteRequestRequest request)
        {
            _log.Info($"ApproveCreditNoteRequest called. CreditNoteId={request?.CreditNoteId}, Flag={request?.Flag}");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for ApproveCreditNoteRequest.");
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
            var serviceResult = _patientRepository.ApproveCreditNoteRequest(request, globalValues);

            if (serviceResult.Result)
                _log.Info($"ApproveCreditNoteRequest succeeded: {serviceResult.Message}");
            else
                _log.Warn($"ApproveCreditNoteRequest failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpPatch("cancelCreditNoteRequest")]
        [Authorize]
        public IActionResult CancelCreditNoteRequest([FromBody] CancelCreditNoteRequestRequest request)
        {
            _log.Info($"CancelCreditNoteRequest called. CreditNoteId={request?.CreditNoteId}");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for CancelCreditNoteRequest.");
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
            var serviceResult = _patientRepository.CancelCreditNoteRequest(request, globalValues);

            if (serviceResult.Result)
                _log.Info($"CancelCreditNoteRequest succeeded: {serviceResult.Message}");
            else
                _log.Warn($"CancelCreditNoteRequest failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpPatch("collectCreditNoteRequest")]
        [Authorize]
        public IActionResult CollectCreditNoteRequest([FromBody] CollectCreditNoteRequestRequest request)
        {
            _log.Info($"CollectCreditNoteRequest called. CreditNoteId={request?.CreditNoteId}");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for CollectCreditNoteRequest.");
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
            var serviceResult = _patientRepository.CollectCreditNoteRequest(request, globalValues);

            if (serviceResult.Result)
                _log.Info($"CollectCreditNoteRequest succeeded: {serviceResult.Message}");
            else
                _log.Warn($"CollectCreditNoteRequest failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getCreditNoteRequestListForApproval")]
        [Authorize]
        public IActionResult GetCreditNoteRequestListForApproval(
            [FromQuery] string fromDate,
            [FromQuery] string toDate,
            [FromQuery] int branchId)
        {
            _log.Info($"GetCreditNoteRequestListForApproval called. FromDate={fromDate}, ToDate={toDate}, BranchId={branchId}");

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
            var serviceResult = _patientRepository.GetCreditNoteRequestListForApproval(fromDate, toDate, branchId, globalValues);

            if (serviceResult.Result)
                _log.Info($"GetCreditNoteRequestListForApproval fetched successfully: {serviceResult.Message}");
            else
                _log.Warn($"GetCreditNoteRequestListForApproval failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getCreditNoteRequestDetailsByCreditNoteId")]
        [Authorize]
        public IActionResult GetCreditNoteRequestDetailsByCreditNoteId([FromQuery] int creditNoteId)
        {
            _log.Info($"GetCreditNoteRequestDetailsByCreditNoteId called. CreditNoteId={creditNoteId}");

            if (creditNoteId <= 0)
            {
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "CreditNoteId must be greater than 0",
                    errors = new { creditNoteId }
                });
            }

            var serviceResult = _patientRepository.GetCreditNoteRequestDetailsByCreditNoteId(creditNoteId);

            if (serviceResult.Result)
                _log.Info($"GetCreditNoteRequestDetailsByCreditNoteId fetched successfully: {serviceResult.Message}");
            else
                _log.Warn($"GetCreditNoteRequestDetailsByCreditNoteId failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getCreditNoteRequestApprovalDetails")]
        [Authorize]
        public IActionResult GetCreditNoteRequestApprovalDetails([FromQuery] int creditNoteId)
        {
            _log.Info($"GetCreditNoteRequestApprovalDetails called. CreditNoteId={creditNoteId}");

            if (creditNoteId <= 0)
            {
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "CreditNoteId must be greater than 0",
                    errors = new { creditNoteId }
                });
            }

            var serviceResult = _patientRepository.GetCreditNoteRequestApprovalDetails(creditNoteId);

            if (serviceResult.Result)
                _log.Info($"GetCreditNoteRequestApprovalDetails fetched successfully: {serviceResult.Message}");
            else
                _log.Warn($"GetCreditNoteRequestApprovalDetails failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getBillForWriteOff")]
        [Authorize]
        public IActionResult GetBillForWriteOff(
    [FromQuery] string fromDate = null,
    [FromQuery] string toDate = null,
    [FromQuery] string billNo = null,
    [FromQuery] string uhid = null,
    [FromQuery] string patientName = null,
    [FromQuery] int typeId = 0)
        {
            _log.Info($"GetBillForWriteOff called. FromDate={fromDate}, ToDate={toDate}, BillNo={billNo}, Uhid={uhid}, PatientName={patientName}, TypeId={typeId}");

            // SP logic: when both uhid and billNo are empty, fromDate/toDate are used as the WHERE filter,
            // so they are required in that case.
            if (string.IsNullOrWhiteSpace(uhid) && string.IsNullOrWhiteSpace(billNo))
            {
                if (string.IsNullOrWhiteSpace(fromDate))
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                    return BadRequest(new
                    {
                        result = false,
                        messageType = alert.Type,
                        message = "FromDate is required when Uhid and BillNo are not provided"
                    });
                }

                if (string.IsNullOrWhiteSpace(toDate))
                {
                    var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                    return BadRequest(new
                    {
                        result = false,
                        messageType = alert.Type,
                        message = "ToDate is required when Uhid and BillNo are not provided"
                    });
                }
            }

            if (typeId < 0)
            {
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "TypeId must be greater than or equal to 0",
                    errors = new { typeId }
                });
            }

            var serviceResult = _patientRepository.GetBillForWriteOff(fromDate, toDate, billNo, uhid, patientName, typeId);

            if (serviceResult.Result)
                _log.Info($"GetBillForWriteOff fetched successfully: {serviceResult.Message}");
            else
                _log.Warn($"GetBillForWriteOff failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getBillDetailsForWriteOff")]
        [Authorize]
        public IActionResult GetBillDetailsForWriteOff([FromQuery] int visitId)
        {
            _log.Info($"GetBillDetailsForWriteOff called. VisitId={visitId}");

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

            var serviceResult = _patientRepository.GetBillDetailsForWriteOff(visitId);

            if (serviceResult.Result)
                _log.Info($"GetBillDetailsForWriteOff fetched successfully: {serviceResult.Message}");
            else
                _log.Warn($"GetBillDetailsForWriteOff failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpPost("saveWriteOff")]
        [Authorize]
        public IActionResult SaveWriteOff([FromBody] SaveWriteOffRequest request)
        {
            _log.Info($"SaveWriteOff called. PatientId={request?.PatientId}, BranchId={request?.BranchId}, BillId={request?.BillId}");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for SaveWriteOff.");
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

            if (request.BillId <= 0)
            {
                _log.Warn("Invalid BillId provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "BillId must be greater than 0",
                    errors = new { billId = request.BillId }
                });
            }

            var globalValues = GlobalFunctions.GetGlobalValues(HttpContext);
            var serviceResult = _patientRepository.SaveWriteOff(request, globalValues);

            if (serviceResult.Result)
                _log.Info($"SaveWriteOff succeeded: {serviceResult.Message}");
            else
                _log.Warn($"SaveWriteOff failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpPost("saveWriteOffRequestApproval")]
        [Authorize]
        public IActionResult SaveWriteOffRequestApproval([FromBody] SaveWriteOffRequestApprovalRequest request)
        {
            _log.Info($"SaveWriteOffRequestApproval called. PatientId={request?.PatientId}, BranchId={request?.BranchId}, BillId={request?.BillId}");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for SaveWriteOffRequestApproval.");
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

            if (request.BillId <= 0)
            {
                _log.Warn("Invalid BillId provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "BillId must be greater than 0",
                    errors = new { billId = request.BillId }
                });
            }

            var globalValues = GlobalFunctions.GetGlobalValues(HttpContext);
            var serviceResult = _patientRepository.SaveWriteOffRequestApproval(request, globalValues);

            if (serviceResult.Result)
                _log.Info($"SaveWriteOffRequestApproval succeeded: {serviceResult.Message}");
            else
                _log.Warn($"SaveWriteOffRequestApproval failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpPatch("approveWriteOffRequest")]
        [Authorize]
        public IActionResult ApproveWriteOffRequest([FromBody] ApproveWriteOffRequestRequest request)
        {
            _log.Info($"ApproveWriteOffRequest called. WriteOffId={request?.WriteOffId}, Flag={request?.Flag}");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for ApproveWriteOffRequest.");
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
            var serviceResult = _patientRepository.ApproveWriteOffRequest(request, globalValues);

            if (serviceResult.Result)
                _log.Info($"ApproveWriteOffRequest succeeded: {serviceResult.Message}");
            else
                _log.Warn($"ApproveWriteOffRequest failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpPatch("cancelWriteOffRequest")]
        [Authorize]
        public IActionResult CancelWriteOffRequest([FromBody] CancelWriteOffRequestRequest request)
        {
            _log.Info($"CancelWriteOffRequest called. WriteOffId={request?.WriteOffId}");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for CancelWriteOffRequest.");
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
            var serviceResult = _patientRepository.CancelWriteOffRequest(request, globalValues);

            if (serviceResult.Result)
                _log.Info($"CancelWriteOffRequest succeeded: {serviceResult.Message}");
            else
                _log.Warn($"CancelWriteOffRequest failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpPatch("collectWriteOffRequest")]
        [Authorize]
        public IActionResult CollectWriteOffRequest([FromBody] CollectWriteOffRequestRequest request)
        {
            _log.Info($"CollectWriteOffRequest called. WriteOffId={request?.WriteOffId}");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for CollectWriteOffRequest.");
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
            var serviceResult = _patientRepository.CollectWriteOffRequest(request, globalValues);

            if (serviceResult.Result)
                _log.Info($"CollectWriteOffRequest succeeded: {serviceResult.Message}");
            else
                _log.Warn($"CollectWriteOffRequest failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getWriteOffRequestListForApproval")]
        [Authorize]
        public IActionResult GetWriteOffRequestListForApproval(
            [FromQuery] string fromDate,
            [FromQuery] string toDate,
            [FromQuery] int branchId)
        {
            _log.Info($"GetWriteOffRequestListForApproval called. FromDate={fromDate}, ToDate={toDate}, BranchId={branchId}");

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
            var serviceResult = _patientRepository.GetWriteOffRequestListForApproval(fromDate, toDate, branchId, globalValues);

            if (serviceResult.Result)
                _log.Info($"GetWriteOffRequestListForApproval fetched successfully: {serviceResult.Message}");
            else
                _log.Warn($"GetWriteOffRequestListForApproval failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getWriteOffRequestDetailsByWriteOffId")]
        [Authorize]
        public IActionResult GetWriteOffRequestDetailsByWriteOffId([FromQuery] int writeOffId)
        {
            _log.Info($"GetWriteOffRequestDetailsByWriteOffId called. WriteOffId={writeOffId}");

            if (writeOffId <= 0)
            {
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "WriteOffId must be greater than 0",
                    errors = new { writeOffId }
                });
            }

            var serviceResult = _patientRepository.GetWriteOffRequestDetailsByWriteOffId(writeOffId);

            if (serviceResult.Result)
                _log.Info($"GetWriteOffRequestDetailsByWriteOffId fetched successfully: {serviceResult.Message}");
            else
                _log.Warn($"GetWriteOffRequestDetailsByWriteOffId failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getWriteOffRequestApprovalDetails")]
        [Authorize]
        public IActionResult GetWriteOffRequestApprovalDetails([FromQuery] int writeOffId)
        {
            _log.Info($"GetWriteOffRequestApprovalDetails called. WriteOffId={writeOffId}");

            if (writeOffId <= 0)
            {
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "WriteOffId must be greater than 0",
                    errors = new { writeOffId }
                });
            }

            var serviceResult = _patientRepository.GetWriteOffRequestApprovalDetails(writeOffId);

            if (serviceResult.Result)
                _log.Info($"GetWriteOffRequestApprovalDetails fetched successfully: {serviceResult.Message}");
            else
                _log.Warn($"GetWriteOffRequestApprovalDetails failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpPost("saveOPDAppointment")]
        [Authorize]
        public IActionResult SaveOPDAppointment([FromBody] SaveOPDAppointmentRequest request)
        {
            _log.Info($"SaveOPDAppointment called. PatientId={request?.PatientDetails?.PatientId}, BranchId={request?.VisitDetails?.BranchId}, DoctorId={request?.VisitDetails?.DoctorId}");

            if (!ModelState.IsValid)
            {
                _log.Warn("Invalid model state for SaveOPDAppointment.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("MODEL_VALIDATION_FAILED");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = alert.Message,
                    errors = ModelState
                });
            }

            // Validate AppDateTime is not default
            if (request.VisitDetails.AppDateTime == default)
            {
                _log.Warn("AppDateTime is missing or invalid.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "AppDateTime is required and must be a valid date",
                    errors = new { appDateTime = request.VisitDetails.AppDateTime }
                });
            }

            // Validate DOB is present
            if (string.IsNullOrWhiteSpace(request.PatientDetails.Dob))
            {
                _log.Warn("DOB is missing.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "Date of birth is required",
                    errors = new { dob = "DOB cannot be empty" }
                });
            }

            // Validate payment details if provided
            if (request.PaymentDetails != null && request.PaymentDetails.Any())
            {
                var invalidPayments = request.PaymentDetails
                    .Where(p => p.PaymentModeId <= 0)
                    .ToList();

                if (invalidPayments.Any())
                {
                    _log.Warn("One or more payment details have invalid PaymentModeId.");
                    var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                    return BadRequest(new
                    {
                        result = false,
                        messageType = alert.Type,
                        message = "All payment details must have PaymentModeId greater than 0",
                        errors = new { invalidPaymentModeIds = invalidPayments.Select(p => p.PaymentModeId).ToList() }
                    });
                }

                var invalidAmounts = request.PaymentDetails
                    .Where(p => p.Amount < 0)
                    .ToList();

                if (invalidAmounts.Any())
                {
                    _log.Warn("One or more payment details have negative Amount.");
                    var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                    return BadRequest(new
                    {
                        result = false,
                        messageType = alert.Type,
                        message = "Payment amount cannot be negative",
                        errors = new { invalidAmounts = invalidAmounts.Select(p => p.Amount).ToList() }
                    });
                }
            }

            var globalValues = GlobalFunctions.GetGlobalValues(HttpContext);
            var serviceResult = _patientRepository.SaveOPDAppointment(request, globalValues);

            if (serviceResult.Result)
                _log.Info($"SaveOPDAppointment completed successfully: {serviceResult.Message}");
            else
                _log.Warn($"SaveOPDAppointment failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("GetDoctorAppointmentPreBookingDetails")]
        [Authorize]
        public IActionResult GetDoctorAppointmentPreBookingDetails(
        [FromQuery] int branchId,
        [FromQuery] int doctorId,
        [FromQuery] int id,
        [FromQuery] int dateTypeId,
        [FromQuery] string fromDate = null,
        [FromQuery] string toDate = null,
        [FromQuery] string tokenNo = null,
        [FromQuery] string sourceType = null
        )
        {
            _log.Info($"GetDoctorAppointmentPreBookingDetails called. FromDate={fromDate}, ToDate={toDate}, DateTypeId={dateTypeId}, BranchId={branchId}, DoctorId={doctorId}, TokenNo={tokenNo}");

            bool hasIdentifier = id > 0 || !string.IsNullOrWhiteSpace(tokenNo);

            DateTime parsedFromDate = DateTime.Now.Date;
            DateTime parsedToDate = DateTime.Now.Date;

            if (!hasIdentifier)
            {
                // Only enforce date/dateTypeId validation when neither id nor tokenNo is supplied
                if (string.IsNullOrWhiteSpace(fromDate) || !DateTime.TryParse(fromDate, out parsedFromDate))
                {
                    _log.Warn("Invalid or missing FromDate.");
                    var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                    return BadRequest(new
                    {
                        result = false,
                        messageType = alert.Type,
                        message = "FromDate is required and must be a valid date",
                        errors = new { fromDate }
                    });
                }

                if (string.IsNullOrWhiteSpace(toDate) || !DateTime.TryParse(toDate, out parsedToDate))
                {
                    _log.Warn("Invalid or missing ToDate.");
                    var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                    return BadRequest(new
                    {
                        result = false,
                        messageType = alert.Type,
                        message = "ToDate is required and must be a valid date",
                        errors = new { toDate }
                    });
                }

                if (dateTypeId != 1 && dateTypeId != 2)
                {
                    _log.Warn($"Invalid DateTypeId provided: {dateTypeId}");
                    var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                    return BadRequest(new
                    {
                        result = false,
                        messageType = alert.Type,
                        message = "DateTypeId must be 1 (CreatedOn) or 2 (AppDateTime)",
                        errors = new { dateTypeId }
                    });
                }
            }
            else
            {
                // id or tokenNo supplied — dates/dateTypeId are optional; fall back to today if not provided/invalid
                if (!string.IsNullOrWhiteSpace(fromDate) && DateTime.TryParse(fromDate, out var fd))
                    parsedFromDate = fd;

                if (!string.IsNullOrWhiteSpace(toDate) && DateTime.TryParse(toDate, out var td))
                    parsedToDate = td;
            }

            if (branchId < 0)
            {
                _log.Warn("Invalid BranchId provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "BranchId must be greater than or equal to 0",
                    errors = new { branchId }
                });
            }

            if (doctorId < 0)
            {
                _log.Warn("Invalid DoctorId provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "DoctorId must be greater than or equal to 0",
                    errors = new { doctorId }
                });
            }

            if (id < 0)
            {
                _log.Warn("Invalid id provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "Id must be greater than or equal to 0",
                    errors = new { id }
                });
            }

            var serviceResult = _patientRepository.GetDoctorAppointmentPreBookingDetails(
                parsedFromDate, parsedToDate, dateTypeId, branchId, doctorId, sourceType, id, tokenNo);

            if (serviceResult.Result)
                _log.Info($"GetDoctorAppointmentPreBookingDetails fetched successfully: {serviceResult.Message}");
            else
                _log.Warn($"GetDoctorAppointmentPreBookingDetails failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getDoctorAppointmentSlots")]
        [Authorize]
        public IActionResult GetDoctorAppointmentSlots(
    [FromQuery] int branchId,
    [FromQuery] int doctorId,
    [FromQuery] string appointmentDate)
        {
            _log.Info($"GetDoctorAppointmentSlots called. BranchId={branchId}, DoctorId={doctorId}, AppointmentDate={appointmentDate}");

            if (branchId <= 0)
            {
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new { result = false, messageType = alert.Type, message = "BranchId must be greater than 0", errors = new { branchId } });
            }

            if (doctorId <= 0)
            {
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new { result = false, messageType = alert.Type, message = "DoctorId must be greater than 0", errors = new { doctorId } });
            }

            if (string.IsNullOrWhiteSpace(appointmentDate) || !DateTime.TryParse(appointmentDate, out DateTime parsedDate))
            {
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new { result = false, messageType = alert.Type, message = "AppointmentDate is required and must be a valid date", errors = new { appointmentDate } });
            }

            var serviceResult = _patientRepository.GetDoctorAppointmentSlots(branchId, doctorId, parsedDate.Date);

            if (serviceResult.Result)
                _log.Info($"GetDoctorAppointmentSlots fetched successfully: {serviceResult.Message}");
            else
                _log.Warn($"GetDoctorAppointmentSlots failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpPatch("cancelDoctorAppointmentPreBooking")]
        [Authorize]
        public IActionResult CancelDoctorAppointmentPreBooking([FromQuery] int id, [FromQuery] string cancelReason)
        {
            _log.Info($"CancelDoctorAppointmentPreBooking called. Id={id}");

            if (id <= 0)
            {
                _log.Warn("Invalid Id provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "Id must be greater than 0",
                    errors = new { id }
                });
            }

            if (string.IsNullOrWhiteSpace(cancelReason))
            {
                _log.Warn("CancelReason is missing or empty.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "CancelReason is required",
                    errors = new { cancelReason = "Required" }
                });
            }

            var globalValues = GlobalFunctions.GetGlobalValues(HttpContext);
            var serviceResult = _patientRepository.CancelDoctorAppointmentPreBooking(id, cancelReason, globalValues);

            if (serviceResult.Result)
                _log.Info($"Appointment pre-booking cancelled successfully: {serviceResult.Message}");
            else
                _log.Warn($"Appointment pre-booking cancellation failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpPatch("confirmDoctorAppointmentPreBooking")]
        [Authorize]
        public IActionResult ConfirmDoctorAppointmentPreBooking([FromQuery] int id)
        {
            _log.Info($"ConfirmDoctorAppointmentPreBooking called. Id={id}");

            if (id <= 0)
            {
                _log.Warn("Invalid Id provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "Id must be greater than 0",
                    errors = new { id }
                });
            }

            var globalValues = GlobalFunctions.GetGlobalValues(HttpContext);
            var serviceResult = _patientRepository.ConfirmDoctorAppointmentPreBooking(id, globalValues);

            if (serviceResult.Result)
                _log.Info($"Appointment pre-booking confirmed successfully: {serviceResult.Message}");
            else
                _log.Warn($"Appointment pre-booking confirmation failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpPatch("rescheduleDoctorAppointmentPreBooking")]
        [Authorize]
        public IActionResult RescheduleDoctorAppointmentPreBooking(
     [FromQuery] int id,
     [FromQuery] int slotId,
     [FromQuery] DateTime appDateTime)
        {
            _log.Info($"RescheduleDoctorAppointmentPreBooking called. Id={id}, SlotId={slotId}, AppDateTime={appDateTime}");

            if (id <= 0)
            {
                _log.Warn("Invalid Id provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "Id must be greater than 0",
                    errors = new { id }
                });
            }

            if (slotId <= 0)
            {
                _log.Warn("Invalid SlotId provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "SlotId must be greater than 0",
                    errors = new { slotId }
                });
            }

            if (appDateTime == default)
            {
                _log.Warn("Invalid AppDateTime provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "AppDateTime is required",
                    errors = new { appDateTime }
                });
            }

            var globalValues = GlobalFunctions.GetGlobalValues(HttpContext);
            var serviceResult = _patientRepository.RescheduleDoctorAppointmentPreBooking(id, slotId, appDateTime, globalValues);

            if (serviceResult.Result)
                _log.Info($"Appointment pre-booking rescheduled: {serviceResult.Message}");
            else
                _log.Warn($"Appointment pre-booking reschedule failed: {serviceResult.Message}");

            return StatusCode(serviceResult.StatusCode, new
            {
                result = serviceResult.Result,
                messageType = serviceResult.MessageType,
                message = serviceResult.Message,
                data = serviceResult.Data
            });
        }

        [HttpGet("getServiceDetailsForCorporateRateComparison")]
        [Authorize]
        public IActionResult GetServiceDetailsForCorporateRateComparison(
    [FromQuery] int visitId,
    [FromQuery] int corporateId)
        {
            _log.Info($"GetServiceDetailsForCorporateRateComparison called. VisitId={visitId}, CorporateId={corporateId}");

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

            if (corporateId <= 0)
            {
                _log.Warn("Invalid CorporateId provided.");
                var alert = _messageService.GetMessageAndTypeByAlertCode("INVALID_PARAMETER");
                return BadRequest(new
                {
                    result = false,
                    messageType = alert.Type,
                    message = "CorporateId must be greater than 0",
                    errors = new { corporateId }
                });
            }

            var serviceResult = _patientRepository.GetServiceDetailsForCorporateRateComparison(visitId, corporateId);

            if (serviceResult.Result)
                _log.Info($"GetServiceDetailsForCorporateRateComparison fetched successfully: {serviceResult.Message}");
            else
                _log.Warn($"GetServiceDetailsForCorporateRateComparison failed: {serviceResult.Message}");

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